using Bookshelf.Application.Recommendations;
using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace Bookshelf.Infrastructure.Persistence.Repositories;

/// <summary>
/// Every database query the recommender makes. Following the project's rule that the repository
/// carries the queries, the service above holds the algorithm and never sees a DbSet.
/// </summary>
public class RecommendationRepository(AppDbContext context) : IRecommendationRepository
{
    public async Task<IReadOnlyList<RecommendationSource>> GetSourceBooksAsync(
        Guid userId, RecommendationTier tier, int limit, CancellationToken cancellationToken)
    {
        var query = context.UserBooks
            .AsNoTracking()
            .Where(ub => ub.UserId == userId && ub.Book.Embedding != null);

        query = tier switch
        {
            // No status condition on purpose — see RecommendationTier.HighlyRated.
            RecommendationTier.HighlyRated => query.Where(ub => ub.Rating >= 4),
            RecommendationTier.Read => query.Where(ub => ub.Status == ReadingStatus.Read),
            RecommendationTier.WantToRead => query.Where(ub => ub.Status == ReadingStatus.WantToRead),
            _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown recommendation tier."),
        };

        // "Most recently finished first", with books that were never finished falling to the back
        // rather than to the front — Postgres sorts NULLs first on a descending order, so the
        // HasValue key is what actually puts them last. Adding date breaks the ties, including
        // the all-NULL case of a shelf where nothing has been finished.
        var sources = await query
            .OrderByDescending(ub => ub.FinishedAt.HasValue)
            .ThenByDescending(ub => ub.FinishedAt)
            .ThenByDescending(ub => ub.AddedAt)
            .Take(limit)
            .Select(ub => new RecommendationSource(ub.BookId, ub.Book.Title, ub.Book.Embedding!))
            .ToListAsync(cancellationToken);

        return sources;
    }

    public async Task<IReadOnlyList<RecommendationCandidate>> GetNearestAsync(
        Guid userId, Vector embedding, int limit, CancellationToken cancellationToken)
    {
        // Cosine distance, which is what the HNSW index was built for (vector_cosine_ops). Using
        // any other operator here would quietly stop using the index.
        var rows = await context.Books
            .AsNoTracking()
            .Where(book => book.Embedding != null)
            .Where(book => !context.UserBooks.Any(ub => ub.UserId == userId && ub.BookId == book.Id))
            .OrderBy(book => book.Embedding!.CosineDistance(embedding))
            .Take(limit)
            .Select(book => new
            {
                Book = book,
                Distance = book.Embedding!.CosineDistance(embedding),
            })
            .ToListAsync(cancellationToken);

        // Distance to similarity, so that "higher is better" holds everywhere above this line.
        // Both vectors are unit length (the embedding service normalises), so cosine distance is
        // 1 − the dot product and this inversion is exact rather than an approximation.
        return rows
            .Select(row => new RecommendationCandidate(row.Book, 1.0 - row.Distance))
            .ToList();
    }

    public async Task<IReadOnlyList<Book>> GetPopularByTopicAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        // Step one: for each topic, the best rank still available to this reader. Grouping in the
        // database returns one row per topic — fifty at most — instead of the whole corpus.
        var bestPerTopic = await context.Books
            .AsNoTracking()
            .Where(book => book.SeedTopic != null && book.PopularityRank != null)
            .Where(book => !context.UserBooks.Any(ub => ub.UserId == userId && ub.BookId == book.Id))
            .GroupBy(book => book.SeedTopic!)
            .Select(group => new
            {
                Topic = group.Key,
                BestRank = group.Min(book => book.PopularityRank!.Value),
            })
            .ToListAsync(cancellationToken);

        if (bestPerTopic.Count == 0)
        {
            return [];
        }

        var topics = bestPerTopic.Select(entry => entry.Topic).ToList();
        var ranks = bestPerTopic.Select(entry => entry.BestRank).Distinct().ToList();

        // Step two: fetch those books. Both lists are used as filters so the database returns a
        // few dozen rows rather than every book in ten topics; the exact pairing is then made in
        // memory, because "(topic, rank) IN ((…),(…))" is not something EF composes.
        var candidates = await context.Books
            .AsNoTracking()
            .Where(book => book.SeedTopic != null
                && topics.Contains(book.SeedTopic)
                && book.PopularityRank != null
                && ranks.Contains(book.PopularityRank.Value))
            .ToListAsync(cancellationToken);

        var wanted = bestPerTopic.ToDictionary(entry => entry.Topic, entry => entry.BestRank);

        return candidates
            .Where(book => wanted.TryGetValue(book.SeedTopic!, out var rank) && book.PopularityRank == rank)
            .ToList();
    }

    public async Task<IReadOnlySet<string>> GetShelfIdentityKeysAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        // Only the two columns the key is built from, for one shelf — tens of rows, not a join
        // the recommender has to think about.
        var shelf = await context.UserBooks
            .AsNoTracking()
            .Where(ub => ub.UserId == userId)
            .Select(ub => new { ub.Book.Title, ub.Book.Author })
            .ToListAsync(cancellationToken);

        return shelf
            .Select(book => ShelfIdentity.BuildKey(book.Title, book.Author))
            .ToHashSet();
    }

    public async Task<IReadOnlyList<Book>> GetBooksMissingEmbeddingAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        // Tracked, unlike every other query here: these rows are about to be written back.
        return await context.UserBooks
            .Where(ub => ub.UserId == userId && ub.Book.Embedding == null)
            .Select(ub => ub.Book)
            .Distinct()
            .OrderBy(book => book.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task SetEmbeddingsAsync(
        IReadOnlyDictionary<Guid, Vector> embeddings, CancellationToken cancellationToken)
    {
        if (embeddings.Count == 0)
        {
            return;
        }

        var ids = embeddings.Keys.ToList();

        var books = await context.Books
            .Where(book => ids.Contains(book.Id))
            .ToListAsync(cancellationToken);

        foreach (var book in books)
        {
            if (embeddings.TryGetValue(book.Id, out var embedding))
            {
                book.Embedding = embedding;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
