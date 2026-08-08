using Bookshelf.Application.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace Bookshelf.Infrastructure.Persistence.Repositories;

public class DashboardRepository(AppDbContext context) : IDashboardRepository
{
    /// <summary>
    /// The log has no UserId of its own — ownership lives on the shelf entry — so the join is
    /// both how the rows are found and how they are scoped to this reader.
    /// </summary>
    public async Task<IReadOnlyList<ReadingLogEntry>> GetReadingLogsAsync(
        Guid userId, CancellationToken cancellationToken) =>
        await (from log in context.ReadingLogs
               join userBook in context.UserBooks on log.UserBookId equals userBook.Id
               where userBook.UserId == userId
               select new ReadingLogEntry(log.UserBookId, log.Date, log.PagesRead))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Projected rather than loaded whole: the statistics need five columns, and pulling notes
    /// and descriptions for every book on the shelf to count pages would be wasteful.
    /// </summary>
    public async Task<IReadOnlyList<ShelfStatsEntry>> GetShelfStatsAsync(
        Guid userId, CancellationToken cancellationToken) =>
        await context.UserBooks
            .Where(userBook => userBook.UserId == userId)
            .Select(userBook => new ShelfStatsEntry(
                userBook.Id,
                userBook.Status,
                userBook.FinishedAt,
                userBook.Book.PageCount,
                userBook.Book.Subjects))
            .ToListAsync(cancellationToken);
}
