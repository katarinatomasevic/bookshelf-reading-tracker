using Bookshelf.Application.Books;
using Bookshelf.Domain.Entities;
using Microsoft.Extensions.Logging;
using Pgvector;

namespace Bookshelf.Application.Recommendations;

/// <summary>
/// Turns a reader's shelf into a list of books to read next.
///
/// <para>
/// The shape of the algorithm is one decision: <b>query per book, then merge</b> — not one query
/// with the average of the reader's vectors. Averaging works only for a reader with a single
/// taste. Someone who likes both science fiction and historical novels gets a mean vector sitting
/// <em>between</em> the two, in a region that matches neither, and the recommendations drift into
/// blandness exactly as the shelf gets more interesting. Querying each book separately lets every
/// book pull its own neighbours, so two tastes stay two tastes. It also produces the attribution
/// for free (see <see cref="BasedOnDto"/>).
/// </para>
///
/// <para>
/// Nothing here is cached. Recommendations have to change the moment a reader rates a book —
/// a cache would mean rating something, going back, and seeing the same list, which reads as a
/// bug. The cost is a few dozen rows and ten indexed vector queries, which is far less than the
/// three or four calls across the internet the book details page makes without anyone minding.
/// </para>
/// </summary>
public class RecommendationService(
    IRecommendationRepository repository,
    IEmbeddingClient embeddingClient,
    ILogger<RecommendationService> logger) : IRecommendationService
{
    /// <summary>
    /// How many of the reader's own books seed the search. Ten is enough to represent a taste;
    /// beyond that the extra books are older and weaker signals, and each one costs a query.
    /// </summary>
    private const int MaxSourceBooks = 10;

    /// <summary>
    /// Neighbours fetched per source book. Comfortably more than the three that can survive
    /// diversification, so that books already on the shelf being filtered out inside the query
    /// still leaves plenty to choose from.
    /// </summary>
    private const int NeighboursPerSource = 20;

    /// <summary>
    /// The floor on how many recommendations a single source book may contribute. See
    /// <see cref="ResolveMaxPerSource"/> for why it is a floor rather than a fixed cap.
    /// </summary>
    private const int MinPerSource = 3;

    private const int MaxLimit = 50;

    /// <summary>The rungs, in the order they are tried.</summary>
    private static readonly RecommendationTier[] Tiers =
    [
        RecommendationTier.HighlyRated,
        RecommendationTier.Read,
        RecommendationTier.WantToRead,
    ];

    public async Task<RecommendationsDto> GetRecommendationsAsync(
        Guid userId, int limit, CancellationToken cancellationToken)
    {
        var safeLimit = Math.Clamp(limit, 1, MaxLimit);

        await TryBackfillEmbeddingsAsync(userId, cancellationToken);

        // Fetched once and handed to both branches: excluding books by row id is not enough when
        // Open Library holds two work records for the same novel. See ShelfIdentity.
        var shelfKeys = await repository.GetShelfIdentityKeysAsync(userId, cancellationToken);

        foreach (var tier in Tiers)
        {
            var items = await BuildForTierAsync(userId, tier, safeLimit, shelfKeys, cancellationToken);

            // Falling through on an empty result, not merely on a missing tier. A reader can own
            // ten rated books and still get nothing back — if they already own every neighbour of
            // all ten, the query legitimately returns an empty set. Stopping here would hand them
            // a blank page, which is the exact outcome the popular-books rung exists to prevent.
            if (items.Count > 0)
            {
                return new RecommendationsDto(items, false);
            }
        }

        return await BuildColdStartAsync(userId, safeLimit, shelfKeys, cancellationToken);
    }

    /// <summary>
    /// One rung: take the reader's books for this tier, collect each one's nearest neighbours,
    /// and merge them into a single ranked list.
    /// </summary>
    private async Task<IReadOnlyList<RecommendationDto>> BuildForTierAsync(
        Guid userId,
        RecommendationTier tier,
        int limit,
        IReadOnlySet<string> shelfKeys,
        CancellationToken cancellationToken)
    {
        var sources = await repository.GetSourceBooksAsync(
            userId, tier, MaxSourceBooks, cancellationToken);

        if (sources.Count == 0)
        {
            return [];
        }

        var best = new Dictionary<Guid, Match>();

        foreach (var source in sources)
        {
            var neighbours = await repository.GetNearestAsync(
                userId, source.Embedding, NeighboursPerSource, cancellationToken);

            foreach (var neighbour in neighbours)
            {
                // A different Open Library record of a book the reader already owns. The query
                // could not know: it is a separate row, with its own id, and nothing on the
                // shelf points at it.
                if (shelfKeys.Contains(ShelfIdentity.BuildKey(neighbour.Book.Title, neighbour.Book.Author)))
                {
                    continue;
                }

                // A book reachable from two source books keeps the closer of the two, and the
                // credit moves with it. That single comparison is the whole tie-breaking rule:
                // a candidate is attributed to the book it is genuinely most like, not to
                // whichever source happened to be processed first. No separate heuristic, and
                // the number that decides the attribution is the same one that decides the rank.
                if (best.TryGetValue(neighbour.Book.Id, out var existing)
                    && existing.Similarity >= neighbour.Similarity)
                {
                    continue;
                }

                best[neighbour.Book.Id] = new Match(neighbour.Book, source, neighbour.Similarity);
            }
        }

        return Diversify(best.Values, sources.Count, limit);
    }

    /// <summary>
    /// Ranks the merged candidates and stops any one source book from filling the list.
    ///
    /// <para>
    /// Walking the globally sorted list once and skipping candidates whose source is already
    /// full does both jobs at once: what survives is the best of each source, and the order is
    /// still by similarity.
    /// </para>
    /// </summary>
    private static IReadOnlyList<RecommendationDto> Diversify(
        IEnumerable<Match> candidates, int sourceCount, int limit)
    {
        var maxPerSource = ResolveMaxPerSource(sourceCount, limit);

        var perSource = new Dictionary<Guid, int>();
        var results = new List<RecommendationDto>(limit);

        foreach (var match in candidates.OrderByDescending(match => match.Similarity))
        {
            if (results.Count == limit)
            {
                break;
            }

            var used = perSource.GetValueOrDefault(match.Source.BookId);
            if (used >= maxPerSource)
            {
                continue;
            }

            perSource[match.Source.BookId] = used + 1;
            results.Add(match.Book.ToRecommendationDto(match.Source, match.Similarity));
        }

        return results;
    }

    /// <summary>
    /// How many recommendations one source book may contribute.
    ///
    /// <para>
    /// The decisions document fixes this at three, and for its own scenario — ten source books,
    /// ten recommendations — this returns exactly three. The formula exists for the scenarios it
    /// did not describe. A reader who has just rated their first book has one source, and a fixed
    /// cap of three would answer with three recommendations out of the ten they asked for: the
    /// recommender at its thinnest for the reader it most needs to convince, and for no benefit,
    /// since with one source there is no second taste being crowded out. The cap is there to stop
    /// one book from dominating a list, not to make the list shorter than requested.
    /// </para>
    /// </summary>
    private static int ResolveMaxPerSource(int sourceCount, int limit) =>
        Math.Max(MinPerSource, (int)Math.Ceiling((double)limit / sourceCount));

    /// <summary>
    /// The last rung: popular books, one from each of <paramref name="limit"/> different topics.
    ///
    /// <para>
    /// An empty page saying "nothing to recommend" would be the worst possible first impression,
    /// and untrue besides — there are thousands of books in the corpus, there is simply nothing
    /// personal to say about them yet.
    /// </para>
    /// </summary>
    private async Task<RecommendationsDto> BuildColdStartAsync(
        Guid userId, int limit, IReadOnlySet<string> shelfKeys, CancellationToken cancellationToken)
    {
        var popular = await repository.GetPopularByTopicAsync(userId, cancellationToken);

        var items = popular
            .Where(book => !shelfKeys.Contains(ShelfIdentity.BuildKey(book.Title, book.Author)))
            // The order is the whole design of this rung: taking any prefix of ColdStartTopics
            // yields that many different kinds of book. Taking the first `limit` after ordering,
            // rather than letting the database pick, is what makes five popular books look
            // chosen instead of sorted.
            .OrderBy(book => ColdStartTopics.RankOf(book.SeedTopic!))
            .Take(limit)
            .Select(book => book.ToPopularRecommendationDto())
            .ToList();

        return new RecommendationsDto(items, true);
    }

    /// <summary>
    /// Gives vectors to the reader's books that do not have one, just before they would be needed.
    ///
    /// <para>
    /// Books end up here because the embedding service was unavailable when they were added — the
    /// deliberate trade made when adding a book (see <c>ShelfService.TryEmbedAsync</c>). This is
    /// the other half of that trade, and it is why no background job or queue is needed: the one
    /// moment a vector actually matters is the moment recommendations are asked for, so that is
    /// when the gap gets filled.
    /// </para>
    ///
    /// <para>
    /// Failure is swallowed, for the same reason as when adding: the recommender is expected to
    /// work with what it has. A reader with thirty embedded books and two unembedded ones gets
    /// recommendations from thirty, rather than an error page about a service they have never
    /// heard of.
    /// </para>
    /// </summary>
    private async Task TryBackfillEmbeddingsAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var pending = await repository.GetBooksMissingEmbeddingAsync(userId, cancellationToken);
            if (pending.Count == 0)
            {
                return;
            }

            logger.LogInformation(
                "Backfilling embeddings for {Count} book(s) before recommending.", pending.Count);

            foreach (var chunk in pending.Chunk(IEmbeddingClient.MaxBatchSize))
            {
                var texts = chunk.Select(EmbeddingText.Build).ToList();
                var vectors = await embeddingClient.EmbedBatchAsync(texts, cancellationToken);

                var updates = new Dictionary<Guid, Vector>(chunk.Length);
                for (var index = 0; index < chunk.Length; index++)
                {
                    updates[chunk[index].Id] = new Vector(vectors[index]);
                }

                // Saved per chunk rather than once at the end, so a failure partway through keeps
                // the chunks that already succeeded instead of discarding all of the work.
                await repository.SetEmbeddingsAsync(updates, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Could not backfill embeddings; recommending from the books that already have one.");
        }
    }

    /// <summary>A candidate together with the source book it is currently credited to.</summary>
    private record Match(Book Book, RecommendationSource Source, double Similarity);
}
