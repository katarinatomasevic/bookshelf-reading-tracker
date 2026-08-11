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
        // Both calls may reach Open Library and neither depends on the other, so they run at the
        // same time — the same two-wave reasoning the live details path uses. Sequentially this
        // page would pay for two round trips instead of one.
        var ratingsTask = book.OpenLibraryId is { } openLibraryId
            ? GetRatingsCachedAsync(openLibraryId, cancellationToken)
            : Task.FromResult(new OpenLibraryRatingsData(null, null));
        var descriptionTask = GetDescriptionAsync(book, cancellationToken);

        await Task.WhenAll(ratingsTask, descriptionTask);

        var ratings = ratingsTask.Result;
        var description = descriptionTask.Result;

        var isOnShelf = userId is { } id
            && await shelfRepository.GetAsync(id, book.Id, cancellationToken) is not null;

        return new BookDetailsDto(
            book.OpenLibraryId,
            book.Title,
            book.Author,
            description,
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

    /// <summary>
    /// The description of a stored book, fetched from Open Library the first time it is needed
    /// and written back so that it is needed only once.
    ///
    /// Why this exists at all: the AI phase seeded ~15.5K books from Open Library's search API,
    /// which does not return descriptions. Fetching them during the harvest would have meant one
    /// request per book — some four hours of traffic against a volunteer-run service — for text
    /// that never enters an embedding anyway (embeddings are built from title, author and
    /// subjects on purpose). So the corpus was stored without descriptions, and the cost moved
    /// here: one request for a book somebody actually opened, paid once in its lifetime.
    ///
    /// A failure is not allowed to cost the page, exactly as with ratings. The book still has a
    /// title, author, cover and subjects; a missing paragraph is not a reason to show an error.
    /// </summary>
    private async Task<string?> GetDescriptionAsync(Book book, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(book.Description) || book.OpenLibraryId is not { } workKey)
        {
            return book.Description;
        }

        var cacheKey = $"ol:description:{workKey}";
        if (cache.TryGetValue<string>(cacheKey, out var cached))
        {
            // Empty string is the cached form of "Open Library has none either", stored instead
            // of null so that a cache hit is never confused with a cache miss.
            return string.IsNullOrEmpty(cached) ? null : cached;
        }

        string? description;
        try
        {
            description = (await openLibraryClient.GetWorkAsync(workKey, cancellationToken)).Description;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Not cached: a network failure says nothing about whether a description exists, and
            // caching it would keep the book blank long after Open Library came back.
            //
            // A cancellation is let through rather than swallowed: it means the reader closed the
            // page, and there is no point finishing the work of rendering it.
            return null;
        }

        cache.Set(cacheKey, description ?? string.Empty, new MemoryCacheEntryOptions
        {
            // Unlike a rating, a description is written once by a volunteer and then sits there,
            // so there is no reason to re-check it sooner than any other Open Library data.
            AbsoluteExpirationRelativeToNow = FullCacheDuration,
            Size = 1,
        });

        if (!string.IsNullOrWhiteSpace(description))
        {
            await bookRepository.FillMissingDescriptionAsync(book.Id, description, cancellationToken);
        }

        return description;
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
