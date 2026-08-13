namespace Bookshelf.Application.Books;

/// <summary>
/// Talks to the Python embedding service. Every implementation detail of that service — its URL,
/// its JSON shape, its batch ceiling — stops at this interface, so the application layer only
/// ever asks "turn this text into a vector".
/// </summary>
public interface IEmbeddingClient
{
    /// <summary>
    /// The largest batch the service accepts in one request; it rejects anything larger with a
    /// 422. Callers chunk against this number, which is why it is exposed rather than hidden.
    /// </summary>
    const int MaxBatchSize = 256;

    /// <summary>
    /// Embeds one text. Returns a 384-dimension unit-length vector.
    /// <para>
    /// Throws when the service cannot answer. Callers decide what that means: adding a book
    /// treats it as "no vector yet" and carries on, because a book must never fail to be added
    /// because an optional service is down.
    /// </para>
    /// </summary>
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken);

    /// <summary>
    /// Embeds up to <see cref="MaxBatchSize"/> texts in one request. Vectors come back in the
    /// order the texts went in — the caller matches them by index, which is the only thing
    /// keeping a batch attached to the books it came from.
    /// </summary>
    Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts, CancellationToken cancellationToken);
}
