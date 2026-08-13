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
    /// Neighbours fetched per source book, for each of the two questions asked about it.
    /// See <see cref="CollectCandidatesAsync"/>.
    /// </summary>
    private const int NeighboursPerSource = 20;

    /// <summary>
    /// The floor on how many recommendations a single source book may contribute. See
    /// <see cref="ResolveMaxPerSource"/> for why it is a floor rather than a fixed cap.
    /// </summary>
    private const int MinPerSource = 3;

    /// <summary>
    /// How many books by one author may appear in a list.
    ///
    /// <para>
    /// The per-source cap does not cover this, and the gap was visible in use: a reader with two
    /// Stephen King books on their shelf has two source books, each allowed several
    /// recommendations, and ended up with five King novels out of ten. The cause is that the
    /// author's name is part of the text each book is embedded from, so everything by one writer
    /// sits very close together in the vector space.
    /// </para>
    /// </summary>
    private const int MaxPerAuthor = 2;

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

        // Also once: the reader's ratings mean the same thing on every tier.
        var rated = await repository.GetRatedBooksAsync(userId, cancellationToken);
        var taste = TasteSignal.From(rated, PrimaryAuthor);

        foreach (var tier in Tiers)
        {
            var items = await BuildForTierAsync(
                userId, tier, safeLimit, shelfKeys, taste, cancellationToken);

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
        TasteSignal taste,
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
            var neighbours = await CollectCandidatesAsync(userId, source, cancellationToken);

            foreach (var neighbour in neighbours)
            {
                // A different Open Library record of a book the reader already owns. The query
                // could not know: it is a separate row, with its own id, and nothing on the
                // shelf points at it.
                if (shelfKeys.Contains(ShelfIdentity.BuildKey(neighbour.Book.Title, neighbour.Book.Author)))
                {
                    continue;
                }

                // A writer the reader has rejected outright. The only hard exclusion the taste
                // signal makes; everything subtler about it is a penalty on the score instead,
                // so that a list can never collapse. See TasteSignal.
                if (taste.IsRejectedAuthor(AuthorKey(neighbour.Book)))
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

        return Diversify(best.Values, sources.Count, limit, taste);
    }

    /// <summary>
    /// Everything one source book has to offer, gathered by asking two questions instead of one.
    ///
    /// <para>
    /// <b>What is nearest to this book?</b> — which for a prolific writer answers mostly with more
    /// of their own work, and that is worth having: the best single recommendation for a reader of
    /// The Secret History is The Goldfinch, and for a reader of Dune it is the rest of the Dune
    /// saga. The author cap keeps it to two of them.
    /// </para>
    ///
    /// <para>
    /// <b>What is nearest to this book by somebody else?</b> — which is the question the first one
    /// stops answering as soon as the cap is full, and the reason this method exists. Now that the
    /// embedded text is "author. subjects." the author's name carries real weight in so short a
    /// string, and a writer with a large back catalogue simply fills the neighbourhood: measured
    /// on this corpus, <em>all forty</em> nearest neighbours of Stephen King's It are other
    /// Stephen King books. With one query that source contributed nothing at all once the cap had
    /// taken its two, and the list came back at six recommendations out of ten.
    /// </para>
    ///
    /// <para>
    /// Widening the first query does not fix that — position forty is still Stephen King, and so
    /// is position four hundred. The pool has to be built from a different question, not a longer
    /// answer to the same one. Two indexed queries per source, at most twenty in all, and the
    /// merge below de-duplicates whatever they have in common.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyList<RecommendationCandidate>> CollectCandidatesAsync(
        Guid userId, RecommendationSource source, CancellationToken cancellationToken)
    {
        var nearest = await repository.GetNearestAsync(
            userId, source.Embedding, NeighboursPerSource, null, cancellationToken);

        var primaryAuthor = PrimaryAuthor(source.Author);
        if (primaryAuthor is null)
        {
            return nearest;
        }

        var byOthers = await repository.GetNearestAsync(
            userId, source.Embedding, NeighboursPerSource, primaryAuthor, cancellationToken);

        return [.. nearest, .. byOthers];
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
        IEnumerable<Match> candidates, int sourceCount, int limit, TasteSignal taste)
    {
        var maxPerSource = ResolveMaxPerSource(sourceCount, limit);

        // Similarity plus a small popularity term. Similarity still decides everything that is
        // not a near-tie; popularity settles the ties and sinks obscure books that matched on a
        // word in the title rather than on what the book is about. See PopularityScore.
        var ranked = candidates.OrderByDescending(match => Score(match, taste)).ToList();

        var perSource = new Dictionary<Guid, int>();
        var perAuthor = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var selected = new List<Match>(limit);

        // One pass, both caps binding, and a short list if that is what the caps leave.
        //
        // An earlier version relaxed the author cap whenever the list would come back short, on
        // the theory that a reader whose only rated book is Dune would otherwise be given two
        // recommendations out of ten. That theory did not survive contact with real data: the
        // relaxation fired constantly, and it fired hardest in exactly the case it was supposed
        // to help — a shelf with two Stephen King novels ended up with four King books again,
        // because the filler could only ever come from the same author-heavy pool the cap had
        // just trimmed. The real problem was pool depth, fixed at NeighboursPerSource above.
        //
        // Returning fewer than asked is the same rule already settled for the tiers: show what
        // there is, do not pad it with something worse.
        foreach (var match in ranked)
        {
            if (selected.Count == limit)
            {
                break;
            }

            if (IsSourceFull(match, perSource, maxPerSource) || IsAuthorFull(match, perAuthor))
            {
                continue;
            }

            Take(match, perSource, perAuthor, selected);
        }

        return selected
            .Select(match => match.Book.ToRecommendationDto(match.Source, match.Similarity))
            .ToList();
    }

    /// <summary>
    /// What the list is ranked by. Similarity is the signal; popularity lifts a near-tie
    /// (<see cref="PopularityScore"/>) and a resemblance to something the reader rejected pushes
    /// one down (<see cref="TasteSignal"/>). Neither can overturn a real difference in similarity.
    /// </summary>
    private static double Score(Match match, TasteSignal taste) =>
        match.Similarity
        + PopularityScore.Weight * PopularityScore.Of(match.Book)
        - taste.PenaltyFor(match.Book);

    private static bool IsSourceFull(
        Match match, Dictionary<Guid, int> perSource, int maxPerSource) =>
        perSource.GetValueOrDefault(match.Source.BookId) >= maxPerSource;

    /// <summary>
    /// Books with no author are never capped against each other: an unknown author is not an
    /// author they have in common, and treating null as one name would cap unrelated books.
    /// </summary>
    private static bool IsAuthorFull(Match match, Dictionary<string, int> perAuthor) =>
        AuthorKey(match.Book) is { } author && perAuthor.GetValueOrDefault(author) >= MaxPerAuthor;

    private static void Take(
        Match match,
        Dictionary<Guid, int> perSource,
        Dictionary<string, int> perAuthor,
        List<Match> selected)
    {
        perSource[match.Source.BookId] = perSource.GetValueOrDefault(match.Source.BookId) + 1;

        if (AuthorKey(match.Book) is { } author)
        {
            perAuthor[author] = perAuthor.GetValueOrDefault(author) + 1;
        }

        selected.Add(match);
    }

    /// <summary>
    /// The author a book is capped under: the <b>first</b> name in the field, not the whole
    /// string.
    ///
    /// <para>
    /// Open Library credits translators, editors and illustrators alongside the writer, and this
    /// project joins them into one comma-separated field (F2). Comparing the whole string
    /// therefore let the cap be walked straight past: "Stephen King" and "Stephen King, José
    /// Óscar Hernández Sendín" are different strings, so a reader who had already been given
    /// their two King novels was handed a third under the name of its translator.
    /// </para>
    ///
    /// <para>
    /// The first name is the one that matters, because that is the order Open Library returns
    /// them in and the one this project stores. A genuinely co-written book is filed under its
    /// first author, which is the same thing a library would do.
    /// </para>
    /// </summary>
    private static string? AuthorKey(Book book) => PrimaryAuthor(book.Author);

    /// <summary>The first name in a comma-separated author field, or null if there is none.</summary>
    private static string? PrimaryAuthor(string? author)
    {
        if (string.IsNullOrWhiteSpace(author))
        {
            return null;
        }

        var primary = author.Split(',')[0].Trim();

        return primary.Length > 0 ? primary : null;
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

        // The order is the whole design of this rung: taking any prefix of ColdStartTopics yields
        // that many different kinds of book, mainstream ones first. Ordering here rather than
        // letting the database pick is what makes ten popular books look chosen instead of
        // sorted.
        var ordered = popular
            .Where(book => !shelfKeys.Contains(ShelfIdentity.BuildKey(book.Title, book.Author)))
            .OrderBy(book => ColdStartTopics.RankOf(book.SeedTopic!));

        // The same author cap as the personalised rungs. One book per topic does not prevent one
        // writer from owning two topics — Stephen King is currently the best-ranked book in both
        // "thriller" and "horror" — and an opening screen that repeats an author has wasted one
        // of its ten chances to interest somebody.
        var perAuthor = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var items = new List<RecommendationDto>(limit);

        foreach (var book in ordered)
        {
            if (items.Count == limit)
            {
                break;
            }

            var author = AuthorKey(book);
            if (author is not null && perAuthor.GetValueOrDefault(author) >= MaxPerAuthor)
            {
                continue;
            }

            if (author is not null)
            {
                perAuthor[author] = perAuthor.GetValueOrDefault(author) + 1;
            }

            items.Add(book.ToPopularRecommendationDto());
        }

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
