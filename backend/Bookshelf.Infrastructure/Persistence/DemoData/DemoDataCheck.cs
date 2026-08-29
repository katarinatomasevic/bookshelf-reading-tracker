using Bookshelf.Application.Dashboard;
using Bookshelf.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Bookshelf.Infrastructure.Persistence.DemoData;

/// <summary>
/// Reads the demo account back out of the database and checks it against the rules the application
/// itself enforces — the ones a direct write bypasses.
/// <para>
/// It exists because of where the data came from. Every one of these invariants is normally
/// guaranteed by <c>POST /api/reading-log</c>: the full-sum recomputation, the unique row per book
/// per day, the CHECK on pages read, the refusal to run past the last page. Seeding straight into
/// the tables skips all four, so the phase plan asks for them to be verified afterwards. Printing
/// the result rather than asserting quietly is deliberate: it turns "the demo data is consistent"
/// from a claim into something visible on screen.
/// </para>
/// <para>
/// It queries with no tracking so that it reads what was actually committed, not the objects still
/// held in memory by the seeder that just ran.
/// </para>
/// </summary>
public static class DemoDataCheck
{
    /// <summary>Comfortably above a heavy reading day, well below a number nobody would believe.</summary>
    private const int BelievablePagesInADay = 90;

    public static async Task<bool> RunAsync(
        AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        var user = await dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Email == DemoDataSeeder.Email, cancellationToken);

        if (user is null)
        {
            Console.WriteLine($"No {DemoDataSeeder.Email} in the database — nothing to check.");
            return false;
        }

        var shelf = await dbContext.UserBooks.AsNoTracking()
            .Where(userBook => userBook.UserId == user.Id)
            .Select(userBook => new
            {
                userBook.Id,
                userBook.Status,
                userBook.StartPage,
                userBook.CurrentPage,
                userBook.Book.Title,
                userBook.Book.PageCount,
            })
            .ToListAsync(cancellationToken);

        var logs = await dbContext.ReadingLogs.AsNoTracking()
            .Where(log => dbContext.UserBooks
                .Any(userBook => userBook.Id == log.UserBookId && userBook.UserId == user.Id))
            .Select(log => new { log.UserBookId, log.Date, log.PagesRead })
            .ToListAsync(cancellationToken);

        var pagesPerBook = logs
            .GroupBy(log => log.UserBookId)
            .ToDictionary(group => group.Key, group => group.Sum(log => log.PagesRead));

        var failures = new List<string>();

        // 1. CurrentPage = StartPage + SUM(PagesRead). A book waiting to be read has no position
        //    at all, which is the same exception ReadingPosition.Apply makes.
        foreach (var entry in shelf)
        {
            var logged = pagesPerBook.GetValueOrDefault(entry.Id);
            var expected = entry.Status == ReadingStatus.WantToRead
                ? (int?)null
                : entry.StartPage + logged;

            if (entry.CurrentPage != expected)
            {
                failures.Add(
                    $"\"{entry.Title}\": position is {Show(entry.CurrentPage)}, but StartPage "
                    + $"{entry.StartPage} + logged {logged} is {Show(expected)}");
            }
        }

        // 2. One entry per book per day.
        var duplicates = logs
            .GroupBy(log => (log.UserBookId, log.Date))
            .Where(group => group.Count() > 1)
            .ToList();

        failures.AddRange(duplicates.Select(group =>
            $"{group.Count()} entries share book {group.Key.UserBookId} on {group.Key.Date:yyyy-MM-dd}"));

        // 3. Every entry is a real day's reading.
        failures.AddRange(logs
            .Where(log => log.PagesRead <= 0)
            .Select(log => $"an entry on {log.Date:yyyy-MM-dd} records {log.PagesRead} pages"));

        // 4. Nobody is past the last page.
        failures.AddRange(shelf
            .Where(entry => entry.PageCount is not null && entry.CurrentPage > entry.PageCount)
            .Select(entry =>
                $"\"{entry.Title}\": position {entry.CurrentPage} is past page {entry.PageCount}"));

        // 5. A day nobody could have read. Not an inconsistency — the schema is perfectly happy
        //    with it — but the activity grid is read as a claim about how much was read, and a
        //    single entry of several hundred pages makes the whole account look invented.
        var heaviest = logs.Count == 0 ? 0 : logs.Max(log => log.PagesRead);

        if (heaviest > BelievablePagesInADay)
        {
            failures.Add(
                $"an entry records {heaviest} pages in one sitting, which reads as invented");
        }

        var days = logs.Select(log => log.Date).ToHashSet();
        var streak = StreakCalculator.Calculate(days, today);

        // 6. A streak nobody would believe is a failure of the demo even though no constraint
        //    forbids it. The upper bound is the window the history is generated over.
        if (streak.Longest > DemoReadingHistory.WindowDays / 2)
        {
            failures.Add($"the record streak of {streak.Longest} days is not a believable one");
        }

        Report(shelf.Count, logs.Count, days, streak, heaviest, today, failures);

        return failures.Count == 0;
    }

    private static void Report(
        int shelfCount,
        int logCount,
        IReadOnlySet<DateOnly> days,
        StreakDto streak,
        int heaviestEntry,
        DateOnly today,
        IReadOnlyList<string> failures)
    {
        var windowStart = today.AddDays(-(DemoReadingHistory.WindowDays - 1));
        var inWindow = days.Count(day => day >= windowStart);

        Console.WriteLine();
        Console.WriteLine("Demo data consistency check");
        Console.WriteLine("---------------------------");
        Console.WriteLine($"  books on the shelf            {shelfCount}");
        Console.WriteLine($"  reading log entries           {logCount}");
        Console.WriteLine($"  days read on, last 26 weeks   {inWindow}");
        Console.WriteLine($"  current streak                {streak.Current} day(s), ending {today:yyyy-MM-dd}");
        Console.WriteLine($"  record streak                 {streak.Longest} day(s)");
        Console.WriteLine($"  longest pause in the window   {LongestPause(days, today)} day(s)");
        Console.WriteLine($"  heaviest single entry         {heaviestEntry} page(s)");
        Console.WriteLine();
        Console.WriteLine("  CurrentPage = StartPage + SUM(PagesRead)   " + Mark(failures, "position"));
        Console.WriteLine("  one entry per book per day                 " + Mark(failures, "share book"));
        Console.WriteLine("  every entry records pages read             " + Mark(failures, "records"));
        Console.WriteLine("  no position past the last page             " + Mark(failures, "past page"));
        Console.WriteLine("  no day reads as invented                   " + Mark(failures, "invented"));
        Console.WriteLine("  the streak is a believable one             " + Mark(failures, "believable"));

        if (failures.Count == 0)
        {
            Console.WriteLine("\nAll checks passed.");
            return;
        }

        Console.WriteLine($"\n{failures.Count} problem(s):");
        foreach (var failure in failures)
        {
            Console.WriteLine($"  - {failure}");
        }
    }

    /// <summary>
    /// The longest run of unread days inside the window. Reported rather than judged: it is the
    /// quickest way to see that the activity grid has real pauses in it instead of one solid block.
    /// </summary>
    private static int LongestPause(IReadOnlySet<DateOnly> days, DateOnly today)
    {
        var longest = 0;
        var run = 0;

        for (var offset = 0; offset < DemoReadingHistory.WindowDays; offset++)
        {
            if (days.Contains(today.AddDays(-offset)))
            {
                run = 0;
                continue;
            }

            run++;
            longest = Math.Max(longest, run);
        }

        return longest;
    }

    private static string Mark(IReadOnlyList<string> failures, string marker) =>
        failures.Any(failure => failure.Contains(marker, StringComparison.Ordinal)) ? "FAILED" : "ok";

    private static string Show(int? value) => value?.ToString() ?? "none";
}
