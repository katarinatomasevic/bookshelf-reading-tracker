namespace Bookshelf.Application.Dashboard;

public interface IDashboardService
{
    /// <param name="period">this_year, last_year or all_time; anything else falls back to this_year.</param>
    /// <param name="today">The reader's own calendar day, which decides where "this year" ends.</param>
    Task<DashboardDto> GetDashboardAsync(
        Guid userId, string? period, DateOnly? today, CancellationToken cancellationToken);

    Task<ActivityDto> GetActivityAsync(Guid userId, DateOnly? today, CancellationToken cancellationToken);
}
