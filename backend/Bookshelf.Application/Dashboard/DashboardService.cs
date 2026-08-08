using Bookshelf.Domain.Enums;

namespace Bookshelf.Application.Dashboard;

/// <summary>
/// Turns one reader's shelf and reading log into the numbers behind the dashboard. Every
/// aggregation happens here rather than in SQL: a personal shelf is tens of books and a few
/// hundred log rows, and the rules below (which pile a book is in, whether its subjects say
/// anything) are far clearer as C# than as a query.
/// </summary>
public class DashboardService(IDashboardRepository dashboardRepository) : IDashboardService
{
    /// <summary>Half a year, which fits on a laptop and on a phone without horizontal scrolling.</summary>
    private const int ActivityWeeks = 26;

    private const int TopGenreCount = 5;

    /// <summary>Matches the window the reading log itself accepts, today included.</summary>
    private const int RollingWindowDays = 30;

    /// <summary>
    /// Past two years of monthly bars the chart stops being readable, so it switches to years.
    /// Only "all time" can ever reach this: the other periods are a year or less by definition.
    /// </summary>
    private const int MaxMonthlyBars = 24;

    public async Task<DashboardDto> GetDashboardAsync(
        Guid userId, string? period, DateOnly? today, CancellationToken cancellationToken)
    {
        var day = ResolveToday(today);
        var selectedPeriod = ParsePeriod(period);
        var (from, to) = ResolveRange(selectedPeriod, day);

        var logs = await dashboardRepository.GetReadingLogsAsync(userId, cancellationToken);
        var shelf = await dashboardRepository.GetShelfStatsAsync(userId, cancellationToken);

        // The streak looks at the whole history and ignores the period selector: a run of days is
        // not something a calendar year can cut in half.
        var streak = StreakCalculator.Calculate(logs.Select(log => log.Date).ToHashSet(), day);

        var logsInPeriod = logs.Where(log => IsWithin(log.Date, from, to)).ToArray();

        var pagesPerBookInPeriod = SumByBook(logsInPeriod);
        var pagesPerBookEver = SumByBook(logs);

        // Books are placed in the period by when they were finished, log entries by their own
        // date. That is not an inconsistency: a book finished in March belongs to March.
        var finished = shelf
            .Where(entry => entry.Status == ReadingStatus.Read && IsWithin(entry.FinishedAt, from, to))
            .ToArray();

        var stats = new DashboardStatsDto(
            finished.Length,
            TotalPages(shelf, finished, pagesPerBookInPeriod, pagesPerBookEver),
            AveragePagesPerDay(logsInPeriod));

        var (granularity, booksFinished) = BuildBooksFinished(finished, selectedPeriod, day);

        return new DashboardDto(
            ToQueryValue(selectedPeriod),
            streak,
            stats,
            granularity,
            booksFinished,
            BuildTopGenres(finished));
    }

    public async Task<ActivityDto> GetActivityAsync(
        Guid userId, DateOnly? today, CancellationToken cancellationToken)
    {
        var day = ResolveToday(today);

        // Whole weeks, so the grid is a clean 7 x 26. The last column is the current week, which
        // means it can reach a few days past today; those cells simply stay empty.
        var currentWeek = StartOfWeek(day);
        var to = currentWeek.AddDays(6);
        var from = currentWeek.AddDays(-7 * (ActivityWeeks - 1));

        var logs = await dashboardRepository.GetReadingLogsAsync(userId, cancellationToken);

        var totals = logs
            .Where(log => log.Date >= from && log.Date <= to)
            .GroupBy(log => log.Date)
            .ToDictionary(group => group.Key, group => group.Sum(log => log.Pages));

        // Every day is returned, zeros included, so the client can lay the grid out by index
        // instead of working out which days are missing.
        var days = new List<ActivityDayDto>(ActivityWeeks * 7);
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            days.Add(new ActivityDayDto(date, totals.GetValueOrDefault(date)));
        }

        return new ActivityDto(from, to, days);
    }

    /// <summary>
    /// Each book counts exactly once. A finished book counts for its whole length whether or not
    /// it was ever logged — a book read five years ago and entered as "Read" was still read —
    /// while a book in progress counts only the pages actually recorded in this period. Without
    /// that split, a book both logged and finished would be counted twice.
    /// </summary>
    private static int TotalPages(
        IReadOnlyList<ShelfStatsEntry> shelf,
        IReadOnlyList<ShelfStatsEntry> finished,
        IReadOnlyDictionary<Guid, int> pagesPerBookInPeriod,
        IReadOnlyDictionary<Guid, int> pagesPerBookEver)
    {
        var finishedIds = finished.Select(entry => entry.UserBookId).ToHashSet();

        var total = 0;

        foreach (var entry in shelf)
        {
            switch (entry.Status)
            {
                case ReadingStatus.Read when finishedIds.Contains(entry.UserBookId):
                    // An unknown page count falls back to whatever was logged, so the book
                    // contributes less rather than disappearing.
                    total += entry.PageCount ?? pagesPerBookEver.GetValueOrDefault(entry.UserBookId);
                    break;

                case ReadingStatus.Reading:
                    total += pagesPerBookInPeriod.GetValueOrDefault(entry.UserBookId);
                    break;
            }
        }

        return total;
    }

    /// <summary>
    /// Pages actually logged, divided by the days they were logged on. Deliberately a different
    /// numerator from the card above: this one is about the rhythm of reading, so a finished book
    /// that was never logged has nothing to say about it.
    /// </summary>
    private static int AveragePagesPerDay(IReadOnlyList<ReadingLogEntry> logsInPeriod)
    {
        var daysWithEntry = logsInPeriod.Select(log => log.Date).Distinct().Count();
        if (daysWithEntry == 0)
        {
            return 0;
        }

        return (int)Math.Round(logsInPeriod.Sum(log => log.Pages) / (double)daysWithEntry);
    }

    /// <summary>
    /// The bars, with the empty buckets filled in. This is done here rather than on the client
    /// because the period is what decides where the row starts and ends, and the backend is the
    /// side that knows it — and for the same reason the choice between months and years is made
    /// here too.
    /// </summary>
    private static (string Granularity, IReadOnlyList<BooksFinishedDto> Buckets) BuildBooksFinished(
        IReadOnlyList<ShelfStatsEntry> finished, DashboardPeriod period, DateOnly today)
    {
        var counts = finished
            .Where(entry => entry.FinishedAt is not null)
            .GroupBy(entry => new DateOnly(entry.FinishedAt!.Value.Year, entry.FinishedAt.Value.Month, 1))
            .ToDictionary(group => group.Key, group => group.Count());

        var thisMonth = new DateOnly(today.Year, today.Month, 1);

        DateOnly start;
        DateOnly end;

        switch (period)
        {
            // A rolling month covers one or two calendar months; both are shown so a book
            // finished at the end of last month does not silently drop off the chart.
            case DashboardPeriod.Last30Days:
                start = new DateOnly(today.AddDays(-(RollingWindowDays - 1)).Year,
                    today.AddDays(-(RollingWindowDays - 1)).Month, 1);
                end = thisMonth;
                break;

            case DashboardPeriod.LastYear:
                start = new DateOnly(today.Year - 1, 1, 1);
                end = new DateOnly(today.Year - 1, 12, 1);
                break;

            case DashboardPeriod.AllTime:
                // Nothing finished, nothing to plot: the client shows "no data yet" rather than
                // a row of empty bars stretching back to an arbitrary date.
                if (counts.Count == 0)
                {
                    return ("month", []);
                }

                start = counts.Keys.Min();
                end = Later(thisMonth, counts.Keys.Max());
                break;

            default:
                start = new DateOnly(today.Year, 1, 1);
                // Normally the current month, but a finish date typed further ahead should not
                // make its own bar vanish.
                end = Later(thisMonth, counts.Keys.DefaultIfEmpty(thisMonth).Max());
                break;
        }

        // Ten years of reading is a hundred and twenty bars, which is no longer a chart. Past the
        // limit each bar becomes a year: the same books, counted the same way, just grouped
        // coarsely enough to be read at a glance.
        if (MonthsBetween(start, end) > MaxMonthlyBars)
        {
            var years = new List<BooksFinishedDto>();
            for (var year = start.Year; year <= end.Year; year++)
            {
                var count = counts
                    .Where(entry => entry.Key.Year == year)
                    .Sum(entry => entry.Value);

                years.Add(new BooksFinishedDto(year, null, count));
            }

            return ("year", years);
        }

        var months = new List<BooksFinishedDto>();
        for (var month = start; month <= end; month = month.AddMonths(1))
        {
            months.Add(new BooksFinishedDto(month.Year, month.Month, counts.GetValueOrDefault(month)));
        }

        return ("month", months);
    }

    /// <summary>Buckets from the first month to the last, both included.</summary>
    private static int MonthsBetween(DateOnly start, DateOnly end) =>
        (end.Year - start.Year) * 12 + end.Month - start.Month + 1;

    /// <summary>
    /// Top five subjects among the books finished in the period. Grouped in C# because Npgsql
    /// maps subjects to a text[] that EF Core cannot query, and grouped case-insensitively
    /// because Open Library's volunteers write "Fiction" and "fiction" for the same thing.
    ///
    /// No stop list: the tags are free text and filtering them well would take a normalisation
    /// map that is never finished, so the noise is left visible rather than half-hidden.
    /// </summary>
    private static IReadOnlyList<GenreCountDto> BuildTopGenres(IReadOnlyList<ShelfStatsEntry> finished)
    {
        return finished
            .SelectMany(entry => entry.Subjects ?? [])
            .Select(subject => subject.Trim())
            .Where(subject => subject.Length > 0)
            .GroupBy(subject => subject.ToLowerInvariant())
            .Select(group => new GenreCountDto(MostCommonSpelling(group), group.Count()))
            .OrderByDescending(genre => genre.Count)
            .ThenBy(genre => genre.Subject, StringComparer.OrdinalIgnoreCase)
            .Take(TopGenreCount)
            .ToArray();
    }

    /// <summary>Shows the spelling most of the books use, rather than whichever came first.</summary>
    private static string MostCommonSpelling(IEnumerable<string> spellings) =>
        spellings
            .GroupBy(spelling => spelling)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .First()
            .Key;

    private static Dictionary<Guid, int> SumByBook(IReadOnlyList<ReadingLogEntry> logs) =>
        logs
            .GroupBy(log => log.UserBookId)
            .ToDictionary(group => group.Key, group => group.Sum(log => log.Pages));

    private static (DateOnly? From, DateOnly? To) ResolveRange(DashboardPeriod period, DateOnly today) =>
        period switch
        {
            DashboardPeriod.ThisYear => (new DateOnly(today.Year, 1, 1), new DateOnly(today.Year, 12, 31)),
            DashboardPeriod.LastYear => (new DateOnly(today.Year - 1, 1, 1), new DateOnly(today.Year - 1, 12, 31)),
            // Today counts as one of the thirty, so the window starts 29 days back.
            DashboardPeriod.Last30Days => (today.AddDays(-(RollingWindowDays - 1)), today),
            _ => (null, null),
        };

    private static bool IsWithin(DateOnly date, DateOnly? from, DateOnly? to) =>
        (from is not { } start || date >= start) && (to is not { } end || date <= end);

    /// <summary>
    /// A finished book with no finish date cannot be placed in a year, so it only counts when the
    /// period has no boundaries. Dropping it from "all time" as well would hide a book the reader
    /// definitely read.
    /// </summary>
    private static bool IsWithin(DateOnly? date, DateOnly? from, DateOnly? to)
    {
        if (from is null && to is null)
        {
            return true;
        }

        return date is { } value && IsWithin(value, from, to);
    }

    /// <summary>Monday, not Sunday: the reader's week starts where their calendar does.</summary>
    private static DateOnly StartOfWeek(DateOnly date) =>
        date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    private static DateOnly Later(DateOnly first, DateOnly second) => first >= second ? first : second;

    /// <summary>
    /// The reader's own calendar day. Unlike the write endpoints this one is not validated: the
    /// request only reads, so the worst a wrong date can do is show the reader their own numbers
    /// through a window they chose themselves.
    /// </summary>
    private static DateOnly ResolveToday(DateOnly? today) =>
        today ?? DateOnly.FromDateTime(DateTime.UtcNow);

    private static DashboardPeriod ParsePeriod(string? period) => period switch
    {
        "last_30_days" => DashboardPeriod.Last30Days,
        "last_year" => DashboardPeriod.LastYear,
        "all_time" => DashboardPeriod.AllTime,
        _ => DashboardPeriod.ThisYear,
    };

    private static string ToQueryValue(DashboardPeriod period) => period switch
    {
        DashboardPeriod.Last30Days => "last_30_days",
        DashboardPeriod.LastYear => "last_year",
        DashboardPeriod.AllTime => "all_time",
        _ => "this_year",
    };
}
