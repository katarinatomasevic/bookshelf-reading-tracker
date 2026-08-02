using System.Globalization;
using Bookshelf.Application.Books;
using Bookshelf.Application.Common.Exceptions;
using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;

namespace Bookshelf.Application.Shelf;

public class ShelfService(
    IBookRepository bookRepository,
    IShelfRepository shelfRepository,
    IBookService bookService,
    IOpenLibraryClient openLibraryClient) : IShelfService
{
    /// <summary>Same cap as the Open Library mapping uses — subject lists are free text and long.</summary>
    private const int MaxSubjects = 15;

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
            return existing.ToShelfItemDto();
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
            Subjects = NormalizeSubjects(request.Subjects),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await bookRepository.AddAsync(book, cancellationToken);

        return await AddShelfEntryAsync(userId, book, cancellationToken);
    }

    public async Task<IReadOnlyList<ShelfItemDto>> GetShelfAsync(
        Guid userId, ReadingStatus? status, string? sort, CancellationToken cancellationToken)
    {
        var shelf = await shelfRepository.GetShelfAsync(userId, status, ParseSort(sort), cancellationToken);

        return shelf.Select(userBook => userBook.ToShelfItemDto()).ToArray();
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

        await shelfRepository.UpdateAsync(userBook, cancellationToken);

        return userBook.ToShelfItemDto();
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

        await bookRepository.AddAsync(book, cancellationToken);

        return book;
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

        return userBook.ToShelfItemDto();
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

    private static string[]? NormalizeSubjects(string[]? subjects)
    {
        if (subjects is null)
        {
            return null;
        }

        var cleaned = subjects
            .Select(subject => subject?.Trim())
            .Where(subject => !string.IsNullOrEmpty(subject))
            .Select(subject => subject!)
            .Take(MaxSubjects)
            .ToArray();

        return cleaned.Length > 0 ? cleaned : null;
    }
}
