namespace Bookshelf.Application.Dashboard;

/// <summary>
/// What the cards and the two charts cover. The streak and the activity grid deliberately ignore
/// it: they answer "am I reading these days", a question a calendar year has nothing to do with.
/// </summary>
public enum DashboardPeriod
{
    ThisYear = 0,
    LastYear = 1,
    AllTime = 2,

    /// <summary>
    /// A rolling window, not a calendar month: it matches the 30 days progress may be logged for,
    /// so what the cards cover and what the reader can still enter are the same span.
    /// </summary>
    Last30Days = 3,
}
