namespace Bookshelf.Application.Dashboard;

/// <summary>
/// Everything the dashboard shows apart from the activity grid, in one response. All of it comes
/// out of the same two queries, so five separate endpoints would mean five authentications and
/// five round trips for one screen.
/// </summary>
/// <param name="Period">Echoed back so the client can tell which selection the numbers belong to.</param>
/// <param name="BooksFinishedGranularity">
/// "month" or "year" — the chart's own heading follows it, so a reader always knows what one bar
/// stands for. Decided here rather than on the client because it depends on the range, and the
/// range is what the backend works out.
/// </param>
public record DashboardDto(
    string Period,
    StreakDto Streak,
    DashboardStatsDto Stats,
    string BooksFinishedGranularity,
    IReadOnlyList<BooksFinishedDto> BooksFinished,
    IReadOnlyList<GenreCountDto> TopGenres);
