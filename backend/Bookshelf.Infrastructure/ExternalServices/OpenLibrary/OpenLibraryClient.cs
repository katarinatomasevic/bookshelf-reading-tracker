using System.Net.Http.Json;
using Bookshelf.Application.Books;
using Bookshelf.Infrastructure.ExternalServices.OpenLibrary.Models;

namespace Bookshelf.Infrastructure.ExternalServices.OpenLibrary;

public class OpenLibraryClient(HttpClient httpClient) : IOpenLibraryClient
{
    private const int PageSize = 20;
    private const int MaxAttempts = 2;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);

    public async Task<BookSearchPageResult> SearchAsync(string query, int page, CancellationToken cancellationToken)
    {
        var safePage = Math.Max(page, 1);
        var uri = $"search.json?q={Uri.EscapeDataString(query)}&page={safePage}&limit={PageSize}" +
                  "&fields=key,title,author_name,first_publish_year,cover_i,number_of_pages_median,subject,isbn";

        var response = await GetJsonAsync<OpenLibrarySearchResponse>(uri, cancellationToken);
        var items = (response.Docs ?? []).Select(doc => doc.ToBookSearchResult()).ToList();
        var hasMore = safePage * PageSize < response.NumFound;

        return new BookSearchPageResult(items, safePage, hasMore);
    }

    public async Task<OpenLibraryWorkData> GetWorkAsync(string workKey, CancellationToken cancellationToken)
    {
        var response = await GetJsonAsync<OpenLibraryWorkResponse>($"works/{workKey}.json", cancellationToken);
        return response.ToWorkData();
    }

    public async Task<OpenLibraryRatingsData> GetRatingsAsync(string workKey, CancellationToken cancellationToken)
    {
        var response = await GetJsonAsync<OpenLibraryRatingsResponse>($"works/{workKey}/ratings.json", cancellationToken);
        return new OpenLibraryRatingsData(response.Summary?.Average, response.Summary?.Count);
    }

    public async Task<string> GetAuthorNameAsync(string authorKey, CancellationToken cancellationToken)
    {
        var response = await GetJsonAsync<OpenLibraryAuthorResponse>($"authors/{authorKey}.json", cancellationToken);
        return response.Name ?? "Unknown author";
    }

    private async Task<T> GetJsonAsync<T>(string requestUri, CancellationToken cancellationToken)
    {
        Exception? lastError = null;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                var response = await httpClient.GetAsync(requestUri, cancellationToken);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
                return result ?? throw new InvalidOperationException(
                    $"Open Library returned an empty response for '{requestUri}'.");
            }
            catch (HttpRequestException ex) when (attempt < MaxAttempts)
            {
                // Only retry fast, transient connection failures (refused/reset). A TaskCanceledException
                // means this attempt already burned the full HttpClient.Timeout (or the caller cancelled) —
                // retrying it would double the worst-case wait for no real benefit.
                lastError = ex;
                await Task.Delay(RetryDelay, cancellationToken);
            }
        }

        throw lastError ?? new InvalidOperationException($"Failed to call Open Library endpoint '{requestUri}'.");
    }
}
