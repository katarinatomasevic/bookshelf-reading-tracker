using Bookshelf.Domain.Entities;
using Pgvector;

namespace Bookshelf.Application.Recommendations;

/// <summary>
/// One of the reader's own books, used as the seed of a similarity search. Carries only what the
/// search and the attribution need: the vector to search with, and the title to say
/// "because you liked …".
/// </summary>
public record RecommendationSource(Guid BookId, string Title, string? Author, Vector Embedding);

/// <summary>
/// A book the corpus offers back, together with how close it was to the vector it was found with.
/// <para>
/// <see cref="Similarity"/> is cosine similarity in −1…1, converted from the cosine distance
/// Postgres returns. Higher is more alike. It is what the merge step compares when the same book
/// is reachable from two different source books, and therefore what decides which book gets the
/// credit in the attribution.
/// </para>
/// </summary>
public record RecommendationCandidate(Book Book, double Similarity);

/// <summary>A book the reader has rated, as the taste signal needs it.</summary>
public record RatedBook(int Rating, string? Author, Vector Embedding);

public interface IRecommendationRepository
{
    /// <summary>
    /// The reader's own books for one rung of the ladder, most recent first, capped at
    /// <paramref name="limit"/>.
    ///
    /// <para>
    /// Books without a vector are excluded here rather than skipped later: a source book exists
    /// only to be searched with, and one with no vector cannot be. Excluding them at the source
    /// also means a reader whose only rated book failed to embed falls through to the next rung
    /// instead of getting an empty answer.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<RecommendationSource>> GetSourceBooksAsync(
        Guid userId, RecommendationTier tier, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Every book this reader has given a rating to, with its author and vector.
    ///
    /// <para>
    /// One query for both directions of the taste signal. The service decides what a rating
    /// means — see <see cref="TasteSignal"/> — because that is policy, and because the two
    /// directions have to be read together: a reader who rated one Stephen King novel 1 and
    /// another 5 has said something more complicated than "no more Stephen King".
    /// </para>
    /// </summary>
    Task<IReadOnlyList<RatedBook>> GetRatedBooksAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// The <paramref name="limit"/> books closest to <paramref name="embedding"/> that are not
    /// already on this reader's shelf.
    ///
    /// <para>
    /// The shelf exclusion is part of the query, not a filter applied afterwards, because doing
    /// it afterwards would silently shrink the result: a reader who owns fifteen of a book's
    /// twenty nearest neighbours would be left with five, and the twenty-first onwards — which
    /// they do not own — would never be considered at all.
    /// </para>
    /// </summary>
    /// <param name="excludeAuthorPrefix">
    /// When given, books whose author begins with this string are left out. Used to ask the
    /// second half of the question the recommender actually has: "and what else is like this,
    /// by somebody else?"
    /// </param>
    Task<IReadOnlyList<RecommendationCandidate>> GetNearestAsync(
        Guid userId,
        Vector embedding,
        int limit,
        string? excludeAuthorPrefix,
        CancellationToken cancellationToken);

    /// <summary>
    /// The best-ranked corpus book from <b>every</b> seed topic, skipping anything already on the
    /// reader's shelf. This is the raw material for the cold-start rung.
    ///
    /// <para>
    /// One book per topic rather than the top N overall: <see cref="Book.PopularityRank"/> is a
    /// position <em>within</em> a topic, so a plain "order by rank" would return ten books all
    /// ranked first, all from whichever topic sorted first — a new reader's first impression of
    /// the application would be ten near-identical books.
    /// </para>
    ///
    /// <para>
    /// All topics rather than the first N, because which ones to show and in what order is a
    /// presentation decision (see <see cref="ColdStartTopics"/>) and belongs above the database.
    /// It costs nothing: the corpus has fifty topics, so this is fifty rows either way.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Book>> GetPopularByTopicAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Title-and-author keys for everything on the reader's shelf, as built by
    /// <see cref="ShelfIdentity.BuildKey"/>. Used to keep a duplicate Open Library record of a
    /// book they already own from being recommended back to them.
    /// </summary>
    Task<IReadOnlySet<string>> GetShelfIdentityKeysAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// The reader's books that still have no vector, oldest first. Feeds the lazy backfill.
    /// </summary>
    Task<IReadOnlyList<Book>> GetBooksMissingEmbeddingAsync(
        Guid userId, CancellationToken cancellationToken);

    /// <summary>Writes freshly computed vectors for books that were missing one.</summary>
    Task SetEmbeddingsAsync(
        IReadOnlyDictionary<Guid, Vector> embeddings, CancellationToken cancellationToken);
}
