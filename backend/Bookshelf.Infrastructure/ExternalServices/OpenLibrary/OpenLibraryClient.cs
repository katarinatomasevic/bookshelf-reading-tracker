using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bookshelf.Application.Books;
using Bookshelf.Application.Common.Exceptions;
using Bookshelf.Infrastructure.ExternalServices.OpenLibrary.Models;

namespace Bookshelf.Infrastructure.ExternalServices.OpenLibrary;

public class OpenLibraryClient(HttpClient httpClient) : IOpenLibraryClient
{
    private const int PageSize = 20;
    private const int MaxAttempts = 2;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Shown to the reader verbatim, so it names the service and says what to do about it.
    /// "An unexpected error occurred" would be both less true and less useful.
    /// </summary>
    private const string UnavailableMessage =
        "Open Library is not responding right now. Please try again in a moment.";

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

    public async Task<BookSearchResult?> GetByWorkKeyAsync(string workKey, CancellationToken cancellationToken)
    {
        var uri = $"search.json?q=key:{Uri.EscapeDataString($"/works/{workKey}")}&limit=1" +
                  "&fields=key,title,author_name,first_publish_year,cover_i,number_of_pages_median,subject,isbn";

        var response = await GetJsonAsync<OpenLibrarySearchResponse>(uri, cancellationToken);

        return response.Docs?.FirstOrDefault()?.ToBookSearchResult();
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

    /// <summary>
    /// Every call to Open Library goes through here, which makes it the one place that decides
    /// what a failure means to the rest of the application.
    ///
    /// Three outcomes, deliberately distinguished, because collapsing them is what produced the
    /// bug this method was rewritten to fix — a slow Open Library used to surface as HTTP 500,
    /// blaming our code for someone else's outage:
    ///
    /// * <see cref="NotFoundException"/> — Open Library answered, and the answer is that the
    ///   record does not exist. That is not a failure of anything.
    /// * <see cref="UpstreamUnavailableException"/> — Open Library did not give a usable answer:
    ///   timeout, refused connection, 5xx, or unparseable body. Becomes a 503.
    /// * <see cref="OperationCanceledException"/>, propagated untouched — the caller went away.
    ///   There is nobody left to receive a status code, so this must not be dressed up as one.
    /// </summary>
    private async Task<T> GetJsonAsync<T>(string requestUri, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var response = await httpClient.GetAsync(requestUri, cancellationToken);

                // Checked before EnsureSuccessStatusCode so that a missing record never enters
                // the retry path: asking twice cannot make a work key exist, it only makes the
                // reader wait longer to be told the same thing.
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new NotFoundException("Book not found.");
                }

                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
                return result ?? throw new UpstreamUnavailableException(UnavailableMessage);
            }
            catch (HttpRequestException) when (attempt < MaxAttempts)
            {
                // Only fast, transient connection failures (refused/reset) are retried. A timeout
                // is not: that attempt already burned the full HttpClient.Timeout, so a second
                // one would double the worst-case wait for no real benefit.
                await Task.Delay(RetryDelay, cancellationToken);
            }
            catch (HttpRequestException exception)
            {
                throw new UpstreamUnavailableException(UnavailableMessage, exception);
            }
            catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                // HttpClient reports its own timeout as a cancellation. The token tells the two
                // apart: if the caller did not cancel, this is Open Library being too slow.
                throw new UpstreamUnavailableException(UnavailableMessage, exception);
            }
            catch (JsonException exception)
            {
                throw new UpstreamUnavailableException(UnavailableMessage, exception);
            }
        }
    }
}
