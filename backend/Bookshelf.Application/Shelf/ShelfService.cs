using System.Globalization;
using Bookshelf.Application.Books;
using Bookshelf.Application.Common.Exceptions;
using Bookshelf.Application.ReadingLogs;
using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;
using Microsoft.Extensions.Logging;
using Pgvector;

namespace Bookshelf.Application.Shelf;

public class ShelfService(
    IBookRepository bookRepository,
    IShelfRepository shelfRepository,
    IReadingLogRepository readingLogRepository,
    IBookService bookService,
    IOpenLibraryClient openLibraryClient,
    IEmbeddingClient embeddingClient,
    ILogger<ShelfService> logger) : IShelfService
{
    /// <summary>
    /// Roughly a page of text: enough for why a book is on the shelf and what the reader thought
    /// of it. A cap is needed at all because every note travels with every shelf load, and
    /// without one the API would accept an arbitrarily large string.
    /// </summary>
    private const int MaxNoteLength = 2000;

    public async Task<ShelfItemDto> AddAsync(Guid userId, AddToShelfRequest request, CancellationToken cancellationToken)
    {
        var book = await ResolveBookAsync(request, cancellationToken);

        // Adding a book that is already there is not an error: the client gets the entry it
        // would have created, which is exactly what it needs to show the "on shelf" state.
        var existing = await shelfRepository.GetAsync(userId, book.Id, cancellationToken);
        if (existing is not null)
        {
            var logCount = await readingLogRepository.GetCountAsync(existing.Id, cancellationToken);

            return existing.ToShelfItemDto(logCount);
        }

        return await AddShelfEntryAsync(userId, book, cancellationToken);
    }

    public async Task<ShelfItemDto> AddManualAsync(
        Guid userId, ManualBookRequest request, CancellationToken cancellationToken)
    {
        var title = request.Title?.Trim();
        if (string.IsNullOrEmpty(title))
        {
            throw new ValidationException("Title is required.");
        }

        var book = new Book
        {
            Id = Guid.NewGuid(),
            OpenLibraryId = null,
            Title = title,
            Author = NormalizeOptional(request.Author),
            Description = NormalizeOptional(request.Description),
            PageCount = request.PageCount,
            Subjects = SubjectFilter.Clean(request.Subjects),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await TryEmbedAsync(book, cancellationToken);
        await bookRepository.AddAsync(book, cancellationToken);

        return await AddShelfEntryAsync(userId, book, cancellationToken);
    }

    public async Task<IReadOnlyList<ShelfItemDto>> GetShelfAsync(
        Guid userId, ReadingStatus? status, string? sort, CancellationToken cancellationToken)
    {
        var shelf = await shelfRepository.GetShelfAsync(userId, status, ParseSort(sort), cancellationToken);

        // One grouped count for the whole shelf rather than a count per book: the modal needs to
        // know whether a reading history exists before it offers to open one, and a book with no
        // entries shows no history section at all.
        var logCounts = await readingLogRepository.GetCountsAsync(userId, cancellationToken);

        return shelf
            .Select(userBook => userBook.ToShelfItemDto(logCounts.GetValueOrDefault(userBook.Id)))
            .ToArray();
    }

    public async Task<ShelfCountsDto> GetCountsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var counts = await shelfRepository.GetCountsAsync(userId, cancellationToken);

        return new ShelfCountsDto(
            counts.GetValueOrDefault(ReadingStatus.WantToRead),
            counts.GetValueOrDefault(ReadingStatus.Reading),
            counts.GetValueOrDefault(ReadingStatus.Read));
    }

    public async Task<ShelfItemDto> UpdateAsync(
        Guid userId, Guid userBookId, UpdateUserBookRequest request, CancellationToken cancellationToken)
    {
        var userBook = await shelfRepository.GetByIdAsync(userId, userBookId, cancellationToken)
            ?? throw new NotFoundException("Book not found on your shelf.");

        var today = ResolveToday(request.Today);
        var previousStatus = userBook.Status;

        if (request.Status is { } status)
        {
            ApplyStatusTransition(userBook, status, today);
        }

        if (request.Rating is { } rating)
        {
            userBook.Rating = ParseRating(rating);
        }

        if (request.Note is not null)
        {
            userBook.Note = ParseNote(request.Note);
        }

        // Deliberately after the transition: the automatic dates are a default, not a lock, so
        // a date the user typed in the same save has to win over the one the status implied.
        if (request.StartedAt is not null)
        {
            userBook.StartedAt = ParseDate(request.StartedAt, "startedAt");
        }

        if (request.FinishedAt is not null)
        {
            userBook.FinishedAt = ParseDate(request.FinishedAt, "finishedAt");
        }

        ApplyPageCount(userBook.Book, request.PageCount);

        if (request.StartPage is { } startPage)
        {
            userBook.StartPage = ParseStartPage(startPage);
        }

        var logCount = await ApplyPositionAsync(userBook, previousStatus, request, cancellationToken);

        await shelfRepository.UpdateAsync(userBook, cancellationToken);

        return userBook.ToShelfItemDto(logCount);
    }

    /// <summary>
    /// Recomputes the reading position when this save could have moved it, and returns the number
    /// of log entries either way.
    /// <para>
    /// Two things move it. Setting the starting page is the obvious one — a book entered at page
    /// 200 must show page 200 straight away, without a fabricated log entry claiming 200 pages
    /// were read in a day. The second is a book coming back out of "want to read": that status
    /// clears the position while keeping the logs, so the position has to be rebuilt from those
    /// logs at the moment the book is picked up again. The reader gets their real page back
    /// rather than starting from zero with a history that says otherwise.
    /// </para>
    /// <para>
    /// A book moving <em>into</em> "want to read" needs nothing here: the transition above has
    /// already cleared the position, and the recomputation deliberately skips that status.
    /// </para>
    /// </summary>
    private async Task<int> ApplyPositionAsync(
        UserBook userBook,
        ReadingStatus previousStatus,
        UpdateUserBookRequest request,
        CancellationToken cancellationToken)
    {
        var pickedBackUp = previousStatus == ReadingStatus.WantToRead
            && userBook.Status != ReadingStatus.WantToRead;

        if (request.StartPage is null && !pickedBackUp)
        {
            return await readingLogRepository.GetCountAsync(userBook.Id, cancellationToken);
        }

        var logs = await readingLogRepository.GetForUserBookAsync(userBook.Id, cancellationToken);
        var loggedPages = logs.Sum(log => log.PagesRead);

        // Checked before it is written, so a starting page that would push the reader past the
        // last page is refused outright rather than half applied.
        ReadingPosition.EnsureWithinBook(userBook, loggedPages);
        ReadingPosition.Apply(userBook, loggedPages);

        return logs.Count;
    }

    /// <summary>
    /// A book cannot be started from before its own beginning. There is no upper bound here —
    /// that is <see cref="ReadingPosition.EnsureWithinBook"/>'s job, because the limit depends on
    /// what has been logged as well.
    /// </summary>
    private static int ParseStartPage(int startPage)
    {
        if (startPage < 0)
        {
            throw new ValidationException("Start page cannot be negative.");
        }

        return startPage;
    }

    public async Task RemoveAsync(Guid userId, Guid userBookId, CancellationToken cancellationToken)
    {
        var userBook = await shelfRepository.GetByIdAsync(userId, userBookId, cancellationToken)
            ?? throw new NotFoundException("Book not found on your shelf.");

        await shelfRepository.DeleteAsync(userBook, cancellationToken);
    }

    /// <summary>
    /// The dates a status change implies, so the common case needs no typing. Nothing happens
    /// when the status is resent unchanged — these are transitions, not invariants.
    /// </summary>
    private static void ApplyStatusTransition(UserBook userBook, ReadingStatus status, DateOnly today)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ValidationException("Unknown reading status.");
        }

        var previous = userBook.Status;
        if (previous == status)
        {
            return;
        }

        userBook.Status = status;

        switch (status)
        {
            case ReadingStatus.Reading:
                userBook.StartedAt ??= today;

                // Re-reading, or a book marked finished by mistake: it is no longer finished.
                if (previous == ReadingStatus.Read)
                {
                    userBook.FinishedAt = null;
                }

                break;

            case ReadingStatus.Read:
                userBook.FinishedAt = today;

                // A book added and finished in one go still deserves a start date.
                userBook.StartedAt ??= today;
                break;

            case ReadingStatus.WantToRead:
                // Back to the queue: nothing about a past reading of it holds any more.
                userBook.StartedAt = null;
                userBook.FinishedAt = null;
                userBook.CurrentPage = null;

                // StartPage survives on purpose, and so do the logs. Both describe a reading that
                // really happened — the logs are days on the streak and the activity grid, and
                // the starting page is where this copy of the book was picked up. Wiping them to
                // match "nothing holds any more" would rewrite history the reader can see
                // elsewhere; instead the position is rebuilt from them if the book is started
                // again, and the reader who truly wants a clean slate deletes the entries.
                break;
        }
    }

    /// <summary>
    /// Page count lives on the shared Book row, so this fills a gap Open Library left and
    /// never overwrites a value other users already see.
    /// </summary>
    private static void ApplyPageCount(Book book, int? pageCount)
    {
        if (pageCount is not { } value)
        {
            return;
        }

        if (value <= 0)
        {
            throw new ValidationException("Page count must be greater than 0.");
        }

        if (book.PageCount is not null)
        {
            return;
        }

        book.PageCount = value;
    }

    /// <summary>0 is the client's way of saying "no rating", since null already means "leave it".</summary>
    private static int? ParseRating(int rating)
    {
        if (rating == 0)
        {
            return null;
        }

        if (rating is < 1 or > 5)
        {
            throw new ValidationException("Rating must be between 1 and 5.");
        }

        return rating;
    }

    /// <summary>
    /// An empty note clears it. The length is enforced here as well as in the form, because the
    /// form's own limit is only a courtesy — a direct API call bypasses it entirely.
    /// </summary>
    private static string? ParseNote(string note)
    {
        var trimmed = note.Trim();

        if (trimmed.Length == 0)
        {
            return null;
        }

        if (trimmed.Length > MaxNoteLength)
        {
            throw new ValidationException($"Note cannot be longer than {MaxNoteLength} characters.");
        }

        return trimmed;
    }

    /// <summary>An empty string clears the date, the same way it clears the note.</summary>
    private static DateOnly? ParseDate(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed))
        {
            throw new ValidationException($"'{fieldName}' must be a date in yyyy-MM-dd format.");
        }

        return parsed;
    }

    /// <summary>
    /// The reader's calendar day, not the server's: a user finishing a book at 01:30 local time
    /// would otherwise get yesterday's date. One day of slack is allowed because time zones run
    /// ahead of UTC — anything beyond that is a clock that cannot be trusted.
    /// </summary>
    private static DateOnly ResolveToday(DateOnly? today)
    {
        var serverToday = DateOnly.FromDateTime(DateTime.UtcNow);

        if (today is not { } value)
        {
            return serverToday;
        }

        if (value > serverToday.AddDays(1))
        {
            throw new ValidationException("'today' cannot be in the future.");
        }

        return value;
    }

    /// <summary>
    /// Finds the book being added, creating it from Open Library only when we have never seen
    /// it before. Dedup is by Open Library key alone, so two users adding the same book share
    /// one row — and the second one costs no external call.
    /// </summary>
    private async Task<Book> ResolveBookAsync(AddToShelfRequest request, CancellationToken cancellationToken)
    {
        var openLibraryId = NormalizeOptional(request.OpenLibraryId);

        var hasOpenLibraryId = openLibraryId is not null;
        var hasBookId = request.BookId is not null;
        if (hasOpenLibraryId == hasBookId)
        {
            throw new ValidationException("Provide either an openLibraryId or a bookId.");
        }

        if (request.BookId is { } bookId)
        {
            return await bookRepository.GetByIdAsync(bookId, cancellationToken)
                ?? throw new NotFoundException("Book not found.");
        }

        var existing = await bookRepository.GetByOpenLibraryIdAsync(openLibraryId!, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var details = await bookService.GetDetailsAsync(openLibraryId!, null, cancellationToken);

        // Neither page count nor ISBN comes from the work endpoint. Coming from search the
        // client already has both and passes them along; coming from the book details page it
        // has neither, and only then is the search index worth one extra call.
        var searchDoc = request.PageCount is null && request.Isbn is null
            ? await GetSearchDocSafeAsync(openLibraryId!, cancellationToken)
            : null;

        var book = new Book
        {
            Id = Guid.NewGuid(),
            OpenLibraryId = openLibraryId,
            Title = details.Title,
            Author = details.Author,
            Description = details.Description,
            CoverId = details.CoverId,
            PageCount = request.PageCount ?? searchDoc?.PageCount,
            Isbn = NormalizeOptional(request.Isbn) ?? NormalizeOptional(searchDoc?.Isbn),
            // The work is the primary source for subjects; the search index is a fallback for
            // works that list none.
            Subjects = details.Subjects is { Length: > 0 } subjects ? subjects : searchDoc?.Subjects,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await TryEmbedAsync(book, cancellationToken);
        await bookRepository.AddAsync(book, cancellationToken);

        return book;
    }

    /// <summary>
    /// Gives a brand new book its vector, if the embedding service can be reached.
    ///
    /// <para>
    /// Called before the insert rather than after, so a successful embedding is written by the
    /// same INSERT that creates the row — one round trip instead of an insert followed by an
    /// update, and no window in which the book exists without a vector it was about to get.
    /// </para>
    ///
    /// <para>
    /// Failure is swallowed on purpose, and this is the decision worth defending: the embedding
    /// service is optional infrastructure for a feature the reader did not ask for at this
    /// moment. They asked to put a book on their shelf. A recommender being unavailable must
    /// never be the reason that fails — so the book is saved with <c>Embedding = NULL</c>, which
    /// the recommender reads as "not a candidate yet", and the lazy backfill picks it up the next
    /// time recommendations are requested. Nothing is lost but a few milliseconds.
    /// </para>
    ///
    /// <para>
    /// The one exception that is <em>not</em> swallowed is the caller's own cancellation: the
    /// reader closed the tab, there is nobody to save a book for, and turning that into "carry on
    /// without a vector" would fabricate work nobody wants.
    /// </para>
    /// </summary>
    private async Task TryEmbedAsync(Book book, CancellationToken cancellationToken)
    {
        try
        {
            var text = EmbeddingText.Build(book);
            var embedding = await embeddingClient.EmbedAsync(text, cancellationToken);

            book.Embedding = new Vector(embedding);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Warning, not error: nothing here is a defect to go and fix, and logging it at
            // error level would train us to ignore errors.
            logger.LogWarning(
                exception,
                "Could not embed book {Title}; it is saved without a vector and will be picked up by the next backfill.",
                book.Title);
        }
    }

    private async Task<ShelfItemDto> AddShelfEntryAsync(Guid userId, Book book, CancellationToken cancellationToken)
    {
        var userBook = new UserBook
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BookId = book.Id,
            Status = ReadingStatus.WantToRead,
            AddedAt = DateTimeOffset.UtcNow,
            Book = book,
        };

        await shelfRepository.AddAsync(userBook, cancellationToken);

        // A shelf entry that has just been created cannot have a reading history behind it.
        return userBook.ToShelfItemDto(0);
    }

    /// <summary>Enriching a book must never be the reason adding it fails.</summary>
    private async Task<BookSearchResult?> GetSearchDocSafeAsync(
        string openLibraryId, CancellationToken cancellationToken)
    {
        try
        {
            return await openLibraryClient.GetByWorkKeyAsync(openLibraryId, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private static ShelfSort ParseSort(string? sort) => sort switch
    {
        "title" => ShelfSort.Title,
        "rating_desc" => ShelfSort.RatingDesc,
        "finished_desc" => ShelfSort.FinishedDesc,
        _ => ShelfSort.AddedDesc,
    };

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
