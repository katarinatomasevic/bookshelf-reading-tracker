namespace Bookshelf.Application.Dashboard;

/// <summary>
/// One bar of the "books finished" chart. Empty buckets are returned as zeros rather than
/// skipped — a grouped query only knows about the months that exist, which would put March and
/// June side by side and quietly misrepresent the gap between them.
/// </summary>
/// <param name="Month">
/// Null when the chart is grouped by year. That happens once the range grows past a couple of
/// years: someone who has been reading for a decade would otherwise get a hundred and twenty
/// bars, which is not a chart. Grouping by year keeps every finished book on screen — nothing is
/// hidden, which matters because the cards above the chart still count the whole period.
/// </param>
public record BooksFinishedDto(int Year, int? Month, int Count);
