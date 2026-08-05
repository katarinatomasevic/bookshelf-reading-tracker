using Bookshelf.Application.ReadingLogs;
using Bookshelf.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bookshelf.Infrastructure.Persistence.Repositories;

public class ReadingLogRepository(AppDbContext context) : IReadingLogRepository
{
    public Task<ReadingLog?> GetByDateAsync(
        Guid userBookId, DateOnly date, CancellationToken cancellationToken) =>
        context.ReadingLogs
            .FirstOrDefaultAsync(log => log.UserBookId == userBookId && log.Date == date, cancellationToken);

    public void Add(ReadingLog log) => context.ReadingLogs.Add(log);

    /// <summary>
    /// One save for both the log and the shelf entry. The shelf entry was loaded through the
    /// same scoped context and is therefore already tracked, so its changed CurrentPage travels
    /// in the same statement batch — and Entity Framework wraps a save in a transaction, which
    /// is exactly the atomicity this needs and nothing more.
    /// </summary>
    public Task SaveProgressAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}
