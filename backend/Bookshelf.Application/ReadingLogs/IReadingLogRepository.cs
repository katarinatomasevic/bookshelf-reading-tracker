using Bookshelf.Domain.Entities;

namespace Bookshelf.Application.ReadingLogs;

public interface IReadingLogRepository
{
    /// <summary>
    /// Every entry for one book, newest first — the reading history, and at the same time the
    /// numbers the position formula needs.
    /// <para>
    /// Whole rows rather than a <c>SUM</c> in the database, because the sum has to include
    /// changes that are still only tracked in memory: an edited entry, one just added, one about
    /// to be removed. A database aggregate would be computed from the rows as they were before
    /// the change and quietly report the old position. A book's history is a few hundred rows at
    /// most, so adding them up here costs nothing.
    /// </para>
    /// <para>
    /// Not scoped by user id: the caller has already resolved the shelf entry by
    /// <c>(Id, UserId)</c>, and a log is reachable only through it.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<ReadingLog>> GetForUserBookAsync(Guid userBookId, CancellationToken cancellationToken);

    /// <summary>
    /// One entry by its own id, scoped to its owner. A reading log has no user column of its
    /// own, so ownership is checked through the shelf entry it hangs off — inside the lookup,
    /// not as a test afterwards, so another user's guid finds nothing rather than finding a row
    /// and then being refused.
    /// </summary>
    Task<ReadingLog?> GetByIdAsync(Guid userId, Guid logId, CancellationToken cancellationToken);

    /// <summary>How many entries each of a user's books has, for the shelf listing.</summary>
    Task<IReadOnlyDictionary<Guid, int>> GetCountsAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// How many entries one book has. Used where a shelf entry is returned on its own and no
    /// recomputation took place, so the rows themselves are not needed.
    /// </summary>
    Task<int> GetCountAsync(Guid userBookId, CancellationToken cancellationToken);

    /// <summary>Stages a new entry; nothing reaches the database until <see cref="SaveProgressAsync"/>.</summary>
    void Add(ReadingLog log);

    /// <summary>Stages a removal, on the same terms as <see cref="Add"/>.</summary>
    void Remove(ReadingLog log);

    /// <summary>
    /// Writes the staged entry together with the shelf entry it belongs to. Both are tracked by
    /// the same context, so this is a single save and therefore a single transaction — the log
    /// and the CurrentPage it implies can never end up disagreeing.
    /// </summary>
    Task SaveProgressAsync(CancellationToken cancellationToken);
}
