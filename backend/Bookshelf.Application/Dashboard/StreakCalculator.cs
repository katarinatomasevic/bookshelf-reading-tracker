namespace Bookshelf.Application.Dashboard;

/// <summary>
/// The streak, worked out in memory rather than in SQL: the input is a set of distinct dates,
/// a few hundred at most for one reader, and the walk backwards is far easier to read — and to
/// unit test — than the window function that would express the same thing in the database.
///
/// Deliberately a pure static function with no dependencies, so it can be tested on its own.
/// </summary>
public static class StreakCalculator
{
    public static StreakDto Calculate(IReadOnlySet<DateOnly> days, DateOnly today) =>
        new(CurrentStreak(days, today), LongestStreak(days));

    /// <summary>
    /// Counts back from today, or from yesterday when today has no entry yet. Someone opening the
    /// app at ten in the morning has not broken anything — the run ends only when both days are
    /// empty. This is what readers expect, because it is what every habit tracker does.
    /// </summary>
    private static int CurrentStreak(IReadOnlySet<DateOnly> days, DateOnly today)
    {
        var cursor = days.Contains(today) ? today : today.AddDays(-1);

        var streak = 0;
        while (days.Contains(cursor))
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }

        return streak;
    }

    /// <summary>The longest run anywhere in the history: one pass over the same dates, sorted.</summary>
    private static int LongestStreak(IReadOnlySet<DateOnly> days)
    {
        if (days.Count == 0)
        {
            return 0;
        }

        var ordered = days.Order().ToArray();

        var longest = 1;
        var run = 1;

        for (var index = 1; index < ordered.Length; index++)
        {
            run = ordered[index] == ordered[index - 1].AddDays(1) ? run + 1 : 1;

            if (run > longest)
            {
                longest = run;
            }
        }

        return longest;
    }
}
