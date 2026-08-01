using System.Text.RegularExpressions;
using Bookshelf.Application.Common.Exceptions;
using Microsoft.Extensions.Caching.Memory;

namespace Bookshelf.Application.Books;

public partial class BookService(IOpenLibraryClient openLibraryClient, IMemoryCache cache) : IBookService
{
    private const int MaxAuthorsToResolve = 3;
    private static readonly TimeSpan FullCacheDuration = TimeSpan.FromHours(24);
    private static readonly TimeSpan MissingRatingCacheDuration = TimeSpan.FromMinutes(15);

    public Task<BookSearchPageResult> SearchAsync(string query, int page, CancellationToken cancellationToken) =>
        openLibraryClient.SearchAsync(query, page, cancellationToken);

    public async Task<BookDetailsDto> GetDetailsAsync(string id, CancellationToken cancellationToken)
    {
        if (!OpenLibraryWorkKeyPattern().IsMatch(id))
        {
            throw new NotFoundException("Book not found.");
        }

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

        var duration = details.AverageRating is null ? MissingRatingCacheDuration : FullCacheDuration;
        cache.Set(cacheKey, details, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = duration, Size = 1 });

        return details;
    }

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
