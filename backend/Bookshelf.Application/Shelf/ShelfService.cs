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
