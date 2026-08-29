using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;

namespace Bookshelf.Infrastructure.Persistence.DemoData;

internal sealed record DemoLogEntry(DateOnly Date, int PagesRead);

/// <summary>What one demo book ends up looking like: where it starts, and every day it was read.</summary>
internal sealed record DemoBookPlan(
    DemoBookSlot Slot,
    Book Book,
    int StartPage,
    IReadOnlyList<DemoLogEntry> Logs)
{
    /// <summary>
    /// The one rule, applied here exactly as <c>ReadingPosition</c> applies it in the application:
    /// the position is the full sum of the increments on top of the starting page, never a number
    /// chosen to look plausible. A book waiting to be read has no position at all.
    /// </summary>
    public int? CurrentPage => Slot.Status == ReadingStatus.WantToRead
        ? null
        : StartPage + Logs.Sum(entry => entry.PagesRead);
}

/// <summary>
/// Builds the reading history behind the demo account.
/// <para>
/// The order of work is what matters here. The <b>calendar comes first</b> — which days of the
/// last twenty-six weeks were read on at all — because that is what the streak and the activity
/// grid are made of, and it is the thing that has to look designed rather than random: a run of
/// five ending today, a record of seventeen a few months back, and two pauses that a real reader
/// would recognise as a busy fortnight and a short trip. Only then is each active day handed to
/// whichever book was being read then, and each book's pages spread across the days it owns.
/// </para>
/// <para>
/// Doing it the other way round — generating each book's reading and seeing what streak falls out
/// — gives no control over the one number the dashboard puts in the largest font on the page.
/// </para>
/// </summary>
internal static class DemoReadingHistory
{
    /// <summary>Twenty-six weeks, matching the activity grid the dashboard draws.</summary>
    public const int WindowDays = 182;

    /// <summary>Days read in a row ending today. Day five back is left empty to end the run there.</summary>
    public const int CurrentStreak = 5;

    /// <summary>The record run, and where it sits — inside the window of the book being read then.</summary>
    public const int RecordStreak = 17;

    private const int RecordRunStartsDaysAgo = 112;

    /// <summary>A busy stretch and a short trip. Both fall inside a single book's window.</summary>
    private static readonly (int From, int To) LongPause = (60, 68);
    private static readonly (int From, int To) ShortPause = (30, 34);

    /// <summary>Roughly every other day outside the fixed runs, which lands the total near ninety.</summary>
    private const double FillDensity = 0.5;

    /// <summary>
    /// The most pages a demo day may claim. The calendar is drawn before any book is costed, so an
    /// unlucky stretch can leave a long book with very few days to be read over — the first run of
    /// this produced a four-hundred page novel spread across five days, at eighty-three pages a
    /// day. Nothing in the schema forbids that and the consistency check cannot catch it, because
    /// it is not an inconsistency: it is simply not what reading looks like. Books short of days
    /// are topped up until they fall under this.
    /// </summary>
    private const int MaxPagesPerDay = 55;

    /// <summary>
    /// Fixed, so that two runs on the same day produce the same shelf. The demo is something to
    /// rehearse against; a grid that reshuffles itself between runs would make that impossible.
    /// </summary>
    private const int RandomSeed = 20_260_829;

    public static IReadOnlyList<DemoBookPlan> Build(
        IReadOnlyList<(DemoBookSlot Slot, Book Book)> resolved, DateOnly today)
    {
        var random = new Random(RandomSeed);
        var calendar = BuildCalendar(random);

        // Only finished books are topped up. The three books in progress share one stretch and
        // divide its days between them, so their pace is set by how the stretch is split rather
        // than by how the calendar happened to fall.
        foreach (var (slot, book) in resolved.Where(entry => entry.Slot is
                     { Logged: true, FinishedDaysAgo: not null }))
        {
            TopUpDays(calendar, slot, book);
        }

        var tailSlots = resolved.Where(entry => entry.Slot.Logged
                                                && entry.Slot.FinishedDaysAgo is null).ToList();
        var tailDays = AssignTailDays(calendar, tailSlots.Count, random);

        var plans = new List<DemoBookPlan>(resolved.Count);

        foreach (var (slot, book) in resolved)
        {
            var pageCount = book.PageCount
                ?? throw new InvalidOperationException(
                    $"\"{book.Title}\" reached the demo shelf without a page count.");

            var startPage = (int)Math.Round(pageCount * slot.StartPageFraction);

            if (!slot.Logged)
            {
                plans.Add(new DemoBookPlan(slot, book, startPage, []));
                continue;
            }

            var dayOffsets = slot.FinishedDaysAgo is null
                ? tailDays[tailSlots.FindIndex(entry => entry.Slot == slot)]
                : DaysWithin(calendar, slot.FinishedDaysAgo.Value, slot.WindowStartDaysAgo);

            if (dayOffsets.Count == 0)
            {
                throw new InvalidOperationException(
                    $"The calendar left \"{book.Title}\" without a single day to be read on.");
            }

            var target = (int)Math.Round((pageCount - startPage) * slot.ProgressFraction);
            var pages = Distribute(target, dayOffsets.Count, random);

            var logs = dayOffsets
                .Select((offset, index) => new DemoLogEntry(today.AddDays(-offset), pages[index]))
                .OrderBy(entry => entry.Date)
                .ToList();

            plans.Add(new DemoBookPlan(slot, book, startPage, logs));
        }

        return plans;
    }

    /// <summary>
    /// Which days were read on, as offsets back from today. Two runs are placed deliberately and
    /// the rest is filled in at roughly half density, with one guard: no filled day may create a
    /// run as long as the record. That is what keeps the record at seventeen — it also quietly
    /// protects the two days on either side of the record run, which would otherwise extend it.
    /// </summary>
    private static HashSet<int> BuildCalendar(Random random)
    {
        var active = new HashSet<int>();

        for (var day = 0; day < CurrentStreak; day++)
        {
            active.Add(day);
        }

        // The day right after the run stays empty, which is what ends it at exactly five. The fill
        // loop below starts past it for the same reason.
        for (var day = RecordRunStartsDaysAgo - RecordStreak + 1; day <= RecordRunStartsDaysAgo; day++)
        {
            active.Add(day);
        }

        for (var day = CurrentStreak + 1; day < WindowDays; day++)
        {
            if (active.Contains(day) || IsPause(day) || random.NextDouble() >= FillDensity)
            {
                continue;
            }

            if (RunLengthIncluding(active, day) >= RecordStreak)
            {
                continue;
            }

            active.Add(day);
        }

        return active;
    }

    /// <summary>
    /// Gives a book enough days for its length, adding them one at a time in the widest gap it
    /// has. Filling the widest gap rather than the first free day keeps the reading spread across
    /// the book's stretch instead of clumping it at one end — the activity grid shows the
    /// difference plainly.
    /// <para>
    /// The deliberate pauses stay empty and no addition may build a run as long as the record, so
    /// the shape the calendar was drawn for survives being topped up.
    /// </para>
    /// </summary>
    private static void TopUpDays(HashSet<int> calendar, DemoBookSlot slot, Book book)
    {
        var from = slot.FinishedDaysAgo!.Value;
        var to = slot.WindowStartDaysAgo;

        var startPage = (int)Math.Round((book.PageCount ?? 0) * slot.StartPageFraction);
        var target = (int)Math.Round(((book.PageCount ?? 0) - startPage) * slot.ProgressFraction);
        var needed = (int)Math.Ceiling(target / (double)MaxPagesPerDay);

        while (DaysWithin(calendar, from, to).Count < needed)
        {
            var candidate = Enumerable.Range(from, to - from + 1)
                .Where(day => !calendar.Contains(day) && !IsPause(day))
                .Where(day => RunLengthIncluding(calendar, day) < RecordStreak)
                .OrderByDescending(day => DistanceToNearestActive(calendar, day))
                .ThenBy(day => day)
                .Cast<int?>()
                .FirstOrDefault();

            if (candidate is null)
            {
                throw new InvalidOperationException(
                    $"\"{book.Title}\" needs {needed} reading day(s) to stay under "
                    + $"{MaxPagesPerDay} pages a day, and its stretch cannot hold that many.");
            }

            calendar.Add(candidate.Value);
        }
    }

    private static int DistanceToNearestActive(HashSet<int> calendar, int day)
    {
        var distance = 1;

        while (distance < WindowDays)
        {
            if (calendar.Contains(day - distance) || calendar.Contains(day + distance))
            {
                return distance;
            }

            distance++;
        }

        return distance;
    }

    private static bool IsPause(int day) =>
        (day >= LongPause.From && day <= LongPause.To)
        || (day >= ShortPause.From && day <= ShortPause.To);

    /// <summary>How long the run through <paramref name="day"/> would be if the day were added.</summary>
    private static int RunLengthIncluding(HashSet<int> active, int day)
    {
        var length = 1;

        for (var lower = day - 1; active.Contains(lower); lower--)
        {
            length++;
        }

        for (var higher = day + 1; active.Contains(higher); higher++)
        {
            length++;
        }

        return length;
    }

    private static List<int> DaysWithin(HashSet<int> calendar, int fromDaysAgo, int toDaysAgo) =>
        calendar.Where(day => day >= fromDaysAgo && day <= toDaysAgo).OrderBy(day => day).ToList();

    /// <summary>
    /// Splits the days of the current stretch between the books being read at the same time.
    /// Every active day goes to exactly one book as its primary, so the union is the whole stretch
    /// and the live streak survives; a second book joins on some days, because someone reading
    /// three books at once does occasionally pick up two of them in an evening.
    /// </summary>
    private static List<List<int>> AssignTailDays(HashSet<int> calendar, int slotCount, Random random)
    {
        var assignments = Enumerable.Range(0, slotCount).Select(_ => new List<int>()).ToList();

        if (slotCount == 0)
        {
            return assignments;
        }

        var days = DaysWithin(calendar, 0, DemoBooks.TailWindowDaysAgo);

        for (var index = 0; index < days.Count; index++)
        {
            assignments[index % slotCount].Add(days[index]);

            if (slotCount > 1 && random.NextDouble() < 0.4)
            {
                assignments[(index + 1) % slotCount].Add(days[index]);
            }
        }

        return assignments;
    }

    /// <summary>
    /// Spreads <paramref name="total"/> pages over <paramref name="dayCount"/> days, hitting the
    /// total exactly and never writing a day of zero pages — the column has a CHECK for that, and
    /// a finished book whose entries add up to anything but its last page would fail the
    /// consistency check that runs after the seed.
    /// </summary>
    private static int[] Distribute(int total, int dayCount, Random random)
    {
        if (total < dayCount)
        {
            throw new InvalidOperationException(
                $"Cannot spread {total} page(s) over {dayCount} day(s) without an empty day.");
        }

        var pages = new int[dayCount];
        var remaining = total;

        for (var index = 0; index < dayCount; index++)
        {
            var daysLeft = dayCount - index;

            if (daysLeft == 1)
            {
                pages[index] = remaining;
                break;
            }

            var average = (double)remaining / daysLeft;
            var jitter = 1.0 + ((random.NextDouble() - 0.5) * 0.7);

            // The upper bound leaves at least one page for every day still to come.
            pages[index] = Math.Clamp((int)Math.Round(average * jitter), 1, remaining - (daysLeft - 1));
            remaining -= pages[index];
        }

        return pages;
    }
}
