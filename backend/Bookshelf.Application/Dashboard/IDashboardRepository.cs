namespace Bookshelf.Application.Dashboard;

/// <summary>
/// Two queries feed the whole dashboard. Both are scoped by user id — the reading log has no
/// UserId column of its own, so it is reached through the shelf entry it belongs to, which is
/// also the only place that ownership is recorded.
/// </summary>
public interface IDashboardRepository
{
    /// <summary>
    /// Every log entry of this reader, unfiltered by date. The period, the streak window and the
    /// activity window are three different spans of the same few hundred rows, so fetching them
    /// once and slicing in memory costs one query instead of three.
    /// </summary>
    Task<IReadOnlyList<ReadingLogEntry>> GetReadingLogsAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// The whole shelf, projected down to the statistics columns. Subjects come along because
    /// the top-genres grouping happens in C#: EF Core does not translate LINQ over a text[].
    /// </summary>
    Task<IReadOnlyList<ShelfStatsEntry>> GetShelfStatsAsync(Guid userId, CancellationToken cancellationToken);
}
