namespace Bookshelf.Application.Dashboard;

/// <summary>
/// One row of the reading log, reduced to what the dashboard needs. Because the table already
/// holds one row per book per day, this is at the same time the daily total for that book — the
/// service groups it by date for the streak and the grid, and by book for the page totals.
/// </summary>
public record ReadingLogEntry(Guid UserBookId, DateOnly Date, int Pages);
