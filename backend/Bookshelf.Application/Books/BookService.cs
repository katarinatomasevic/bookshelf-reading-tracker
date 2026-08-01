using System.Text.RegularExpressions;
using Bookshelf.Application.Common.Exceptions;
using Bookshelf.Application.Shelf;
using Bookshelf.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;

namespace Bookshelf.Application.Books;

public partial class BookService(
    IOpenLibraryClient openLibraryClient,
    IMemoryCache cache,
    IBookRepository bookRepository,
    IShelfRepository shelfRepository) : IBookService
{
    private const int MaxAuthorsToResolve = 3;
    private static readonly TimeSpan FullCacheDuration = TimeSpan.FromHours(24);
    private static readonly TimeSpan MissingRatingCacheDuration = TimeSpan.FromMinutes(15);

    public async Task<BookSearchPageResult> SearchAsync(
        string query, int page, Guid? userId, CancellationToken cancellationToken)
    {
        var result = await openLibraryClient.SearchAsync(query, page, cancellationToken);

        if (userId is not { } id || result.Items.Count == 0)
        {
            return result;
        }

        var onShelf = await shelfRepository.GetOpenLibraryIdsOnShelfAsync(
            id, result.Items.Select(item => item.OpenLibraryId).ToArray(), cancellationToken);

        if (onShelf.Count == 0)
        {
            return result;
        }

        var marked = result.Items
            .Select(item => item with { IsOnShelf = onShelf.Contains(item.OpenLibraryId) })
            .ToArray();

        return result with { Items = marked };
    }

    public async Task<BookDetailsDto> GetDetailsAsync(string id, Guid? userId, CancellationToken cancellationToken)
    {
        if (OpenLibraryWorkKeyPattern().IsMatch(id))
        {
            // A saved book is the better source: it has our own metadata (page count, manually
            // corrected values) and costs no round trip to Open Library.
            var stored = await bookRepository.GetByOpenLibraryIdAsync(id, cancellationToken);
            return stored is not null
                ? await MapStoredBookAsync(stored, userId, cancellationToken)
                : await GetOpenLibraryDetailsAsync(id, cancellationToken);
        }

        if (Guid.TryParse(id, out var bookId))
        {
            var stored = await bookRepository.GetByIdAsync(bookId, cancellationToken)
                ?? throw new NotFoundException("Book not found.");

            return await MapStoredBookAsync(stored, userId, cancellationToken);
        }

        throw new NotFoundException("Book not found.");
    }

    /// <summary>
    /// Details of a book we store ourselves. The Open Library rating is deliberately not stored
    /// (it would go stale and we never write to it), so it is fetched fresh — from cache when
    /// possible, and never at the cost of the page when the call fails.
    /// </summary>
    private async Task<BookDetailsDto> MapStoredBookAsync(
        Book book, Guid? userId, CancellationToken cancellationToken)
    {
        var ratings = book.OpenLibraryId is { } openLibraryId
            ? await GetRatingsCachedAsync(openLibraryId, cancellationToken)
            : new OpenLibraryRatingsData(null, null);

        var isOnShelf = userId is { } id
            && await shelfRepository.GetAsync(id, book.Id, cancellationToken) is not null;

        return new BookDetailsDto(
            book.OpenLibraryId,
            book.Title,
            book.Author,
            book.Description,
            book.CoverId,
            book.Subjects,
            ratings.Average,
            ratings.Count)
        {
            Id = book.Id,
            PageCount = book.PageCount,
            Isbn = book.Isbn,
            IsOnShelf = isOnShelf,
        };
    }

    /// <summary>
    /// Live details for a book nobody has saved yet. Such a book cannot be on anyone's shelf
    /// (a shelf entry needs a stored book), so the cached value is user-independent.
    /// </summary>
    private async Task<BookDetailsDto> GetOpenLibraryDetailsAsync(string id, CancellationToken cancellationToken)
    {
        var cacheKey = $"ol:details:{id}";
        if (cache.TryGetValue<BookDetailsDto>(cacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var ratingsTask = GetRatingsSafeAsync(id, cancellationToken);
        var workTask = openLibraryClient.GetWorkAsync(id, cancellationToken);
        await Task.WhenAll(workTask, ratingsTask);

        var work = workTask.Result;
        var ratings = ratingsTask.Result;

        // Open Library keeps redirect and other non-book records under work keys; they carry no
        // title. Such a record must not become a page — nor, once it can be shelved, a nameless
        // row in our catalogue.
        if (string.IsNullOrWhiteSpace(work.Title))
        {
            throw new NotFoundException("Book not found.");
        }

        var authorKeys = work.AuthorKeys.Take(MaxAuthorsToResolve).ToArray();
        var authorNames = await Task.WhenAll(
            authorKeys.Select(key => openLibraryClient.GetAuthorNameAsync(key, cancellationToken)));

        var details = new BookDetailsDto(
            id,
            work.Title,
            authorNames.Length > 0 ? string.Join(", ", authorNames) : null,
            work.Description,
            work.CoverId,
            work.Subjects,
            ratings.Average,
            ratings.Count);

        cache.Set(cacheKey, details, CacheOptionsFor(details.AverageRating));

        return details;
    }

    private async Task<OpenLibraryRatingsData> GetRatingsCachedAsync(
        string workKey, CancellationToken cancellationToken)
    {
        var cacheKey = $"ol:ratings:{workKey}";
        if (cache.TryGetValue<OpenLibraryRatingsData>(cacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var ratings = await GetRatingsSafeAsync(workKey, cancellationToken);
        cache.Set(cacheKey, ratings, CacheOptionsFor(ratings.Average));

        return ratings;
    }

    /// <summary>A missing rating may be a failed call rather than an unrated book, so it is kept briefly.</summary>
    private static MemoryCacheEntryOptions CacheOptionsFor(double? averageRating) => new()
    {
        AbsoluteExpirationRelativeToNow = averageRating is null ? MissingRatingCacheDuration : FullCacheDuration,
        Size = 1,
    };

    private async Task<OpenLibraryRatingsData> GetRatingsSafeAsync(string workKey, CancellationToken cancellationToken)
    {
        try
        {
            return await openLibraryClient.GetRatingsAsync(workKey, cancellationToken);
        }
        catch
        {
            return new OpenLibraryRatingsData(null, null);
        }
    }

    [GeneratedRegex(@"^OL\d+W$")]
    private static partial Regex OpenLibraryWorkKeyPattern();
}
