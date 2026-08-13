using System.Net.Http.Json;
using System.Text.Json;
using Bookshelf.Application.Books;
using Bookshelf.Application.Common.Exceptions;
using Bookshelf.Infrastructure.ExternalServices.Embedding.Models;

namespace Bookshelf.Infrastructure.ExternalServices.Embedding;

/// <summary>
/// HTTP client for the Python embedding service.
///
/// <para>
/// Structurally a twin of <see cref="OpenLibrary.OpenLibraryClient"/> — one private method owns
/// every call, so there is one place that decides what a failure means. The difference is what
/// callers do with that failure. Open Library being down stops a book details page from
/// existing; the embedding service being down costs a vector, and a book without a vector is
/// simply not a recommendation candidate yet. So this client throws honestly and lets each
/// caller decide, and every caller in this phase chooses to carry on.
/// </para>
///
/// <para>
/// No retry, unlike the Open Library client. That client retries because it crosses the public
/// internet; this one talks to a container on the same host or Compose network, where a refused
/// connection means the service is not running — and asking a second time will not start it.
/// A retry would only double the wait before adding a book succeeds anyway.
/// </para>
/// </summary>
public class EmbeddingClient(HttpClient httpClient) : IEmbeddingClient
{
    /// <summary>
    /// Width of all-MiniLM-L6-v2's output, and of the <c>vector(384)</c> column. Checked on the
    /// way in: a vector of the wrong width is rejected here, where the message can say what
    /// happened, instead of by Postgres halfway through saving a book.
    /// </summary>
    private const int ExpectedDimensions = 384;

    private const string UnavailableMessage =
        "The embedding service is not responding right now.";

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        var response = await PostAsync<EmbedRequest, EmbedResponse>(
            "embed", new EmbedRequest(text), cancellationToken);

        var embedding = response.Embedding
            ?? throw new UpstreamUnavailableException(UnavailableMessage);

        EnsureDimensions(embedding);

        return embedding;
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        if (texts.Count == 0)
        {
            return [];
        }

        if (texts.Count > IEmbeddingClient.MaxBatchSize)
        {
            throw new ArgumentException(
                $"At most {IEmbeddingClient.MaxBatchSize} texts can be embedded in one request.",
                nameof(texts));
        }

        var response = await PostAsync<EmbedBatchRequest, EmbedBatchResponse>(
            "embed/batch", new EmbedBatchRequest(texts), cancellationToken);

        var embeddings = response.Embeddings
            ?? throw new UpstreamUnavailableException(UnavailableMessage);

        // The caller matches vectors to books by index, so a short batch would silently attach
        // the wrong vector to the wrong book — far worse than no vector at all.
        if (embeddings.Length != texts.Count)
        {
            throw new UpstreamUnavailableException(
                $"The embedding service returned {embeddings.Length} vectors for {texts.Count} texts.");
        }

        foreach (var embedding in embeddings)
        {
            EnsureDimensions(embedding);
        }

        return embeddings;
    }

    private static void EnsureDimensions(float[] embedding)
    {
        if (embedding.Length != ExpectedDimensions)
        {
            throw new UpstreamUnavailableException(
                $"The embedding service returned a {embedding.Length}-dimension vector, " +
                $"but {ExpectedDimensions} are required.");
        }
    }

    /// <summary>
    /// The one path every request takes. Anything that is not a usable answer — refused
    /// connection, timeout, 5xx, unparseable body — becomes
    /// <see cref="UpstreamUnavailableException"/>, so callers need to know about exactly one
    /// failure type. A caller's own cancellation propagates untouched: nobody is waiting for a
    /// result, so dressing it up as a service failure would be a lie in the logs.
    /// </summary>
    private async Task<TResponse> PostAsync<TRequest, TResponse>(
        string requestUri, TRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync(requestUri, request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken);

            return result ?? throw new UpstreamUnavailableException(UnavailableMessage);
        }
        catch (HttpRequestException exception)
        {
            throw new UpstreamUnavailableException(UnavailableMessage, exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient reports its own timeout as a cancellation; the token tells the two apart.
            throw new UpstreamUnavailableException(UnavailableMessage, exception);
        }
        catch (JsonException exception)
        {
            throw new UpstreamUnavailableException(UnavailableMessage, exception);
        }
    }
}
