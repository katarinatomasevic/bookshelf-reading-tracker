using Bookshelf.Application.ReadingLogs;
using Bookshelf.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bookshelf.Infrastructure.Persistence.Repositories;

public class ReadingLogRepository(AppDbContext context) : IReadingLogRepository
{
    /// <summary>
    /// Newest first, which is both how the history is read and the direction the unique index
    /// on <c>(UserBookId, Date DESC)</c> already stores — no sort is performed for this.
    /// </summary>
    public async Task<IReadOnlyList<ReadingLog>> GetForUserBookAsync(
        Guid userBookId, CancellationToken cancellationToken) =>
        await context.ReadingLogs
            .Where(log => log.UserBookId == userBookId)
            .OrderByDescending(log => log.Date)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// The ownership test is part of the query. Expressed as an existence check against UserBook
    /// rather than a join, so the result stays a plain tracked ReadingLog.
    /// </summary>
    public Task<ReadingLog?> GetByIdAsync(
        Guid userId, Guid logId, CancellationToken cancellationToken) =>
        context.ReadingLogs
            .FirstOrDefaultAsync(
                log => log.Id == logId
                    && context.UserBooks.Any(ub => ub.Id == log.UserBookId && ub.UserId == userId),
                cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, int>> GetCountsAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        // Grouped in the database: the shelf needs one number per book, not the entries behind it.
        var counts = await context.ReadingLogs
            .Where(log => context.UserBooks.Any(ub => ub.Id == log.UserBookId && ub.UserId == userId))
            .GroupBy(log => log.UserBookId)
            .Select(group => new { UserBookId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(entry => entry.UserBookId, entry => entry.Count);
    }

    public Task<int> GetCountAsync(Guid userBookId, CancellationToken cancellationToken) =>
        context.ReadingLogs.CountAsync(log => log.UserBookId == userBookId, cancellationToken);

    public void Add(ReadingLog log) => context.ReadingLogs.Add(log);

    public void Remove(ReadingLog log) => context.ReadingLogs.Remove(log);

    /// <summary>
    /// One save for both the log and the shelf entry. The shelf entry was loaded through the
    /// same scoped context and is therefore already tracked, so its changed CurrentPage travels
    /// in the same statement batch — and Entity Framework wraps a save in a transaction, which
    /// is exactly the atomicity this needs and nothing more.
    /// </summary>
    public Task SaveProgressAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}
