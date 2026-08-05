using Bookshelf.Domain.Entities;

namespace Bookshelf.Application.ReadingLogs;

public interface IReadingLogRepository
{
    /// <summary>
    /// The entry already recorded for this book on this day, if any. A second entry on the same
    /// day adds to it rather than becoming a new row.
    /// </summary>
    Task<ReadingLog?> GetByDateAsync(Guid userBookId, DateOnly date, CancellationToken cancellationToken);

    /// <summary>Stages a new entry; nothing reaches the database until <see cref="SaveProgressAsync"/>.</summary>
    void Add(ReadingLog log);

    /// <summary>
    /// Writes the staged entry together with the shelf entry it belongs to. Both are tracked by
    /// the same context, so this is a single save and therefore a single transaction — the log
    /// and the CurrentPage it implies can never end up disagreeing.
    /// </summary>
    Task SaveProgressAsync(CancellationToken cancellationToken);
}
