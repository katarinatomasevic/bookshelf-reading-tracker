using Bookshelf.Application.Dashboard;

namespace Bookshelf.UnitTests.Dashboard;

/// <summary>
/// The streak rule from F4, pinned down by example.
///
/// <para>
/// These tests need no database and no mocks: <see cref="StreakCalculator"/> is a pure function
/// over a set of dates, which was the reason for keeping it out of SQL in the first place. The
/// window function that would express the same thing in the database could not be tested this way.
/// </para>
/// </summary>
[TestFixture]
public class StreakCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 8, 13);

    private static IReadOnlySet<DateOnly> Days(params DateOnly[] days) => days.ToHashSet();

    private static DateOnly DaysAgo(int count) => Today.AddDays(-count);

    [Test]
    public void Counts_consecutive_days_ending_today()
    {
        var streak = StreakCalculator.Calculate(
            Days(Today, DaysAgo(1), DaysAgo(2)), Today);

        Assert.That(streak.Current, Is.EqualTo(3));
    }

    /// <summary>
    /// The rule that made the streak worth writing by hand: someone who opens the app at ten in
    /// the morning, before they have read anything that day, must not be told their run is over.
    /// </summary>
    [Test]
    public void Counts_back_from_yesterday_when_today_is_still_empty()
    {
        var streak = StreakCalculator.Calculate(
            Days(DaysAgo(1), DaysAgo(2), DaysAgo(3)), Today);

        Assert.That(streak.Current, Is.EqualTo(3));
    }

    /// <summary>Two empty days in a row is what actually ends a run.</summary>
    [Test]
    public void Breaks_when_both_today_and_yesterday_are_empty()
    {
        var streak = StreakCalculator.Calculate(
            Days(DaysAgo(2), DaysAgo(3), DaysAgo(4)), Today);

        Assert.That(streak.Current, Is.EqualTo(0));
    }

    [Test]
    public void Stops_at_the_first_gap_rather_than_counting_every_entry()
    {
        // Read today and yesterday, then a gap, then three more days further back.
        var streak = StreakCalculator.Calculate(
            Days(Today, DaysAgo(1), DaysAgo(3), DaysAgo(4), DaysAgo(5)), Today);

        Assert.That(streak.Current, Is.EqualTo(2));
    }

    /// <summary>The record is the longest run anywhere in the history, not the running one.</summary>
    [Test]
    public void Longest_streak_looks_past_the_current_run()
    {
        var streak = StreakCalculator.Calculate(
            Days(Today, DaysAgo(10), DaysAgo(11), DaysAgo(12), DaysAgo(13)), Today);

        Assert.Multiple(() =>
        {
            Assert.That(streak.Current, Is.EqualTo(1));
            Assert.That(streak.Longest, Is.EqualTo(4));
        });
    }

    [Test]
    public void A_reader_who_has_logged_nothing_has_no_streak()
    {
        var streak = StreakCalculator.Calculate(Days(), Today);

        Assert.Multiple(() =>
        {
            Assert.That(streak.Current, Is.EqualTo(0));
            Assert.That(streak.Longest, Is.EqualTo(0));
        });
    }

    /// <summary>
    /// A single day counts as a run of one — the boundary that a naive "count the gaps" loop
    /// tends to report as zero.
    /// </summary>
    [Test]
    public void A_single_logged_day_is_a_streak_of_one()
    {
        var streak = StreakCalculator.Calculate(Days(Today), Today);

        Assert.Multiple(() =>
        {
            Assert.That(streak.Current, Is.EqualTo(1));
            Assert.That(streak.Longest, Is.EqualTo(1));
        });
    }

    /// <summary>
    /// Dates arrive from the database in no guaranteed order, and the longest-run pass sorts
    /// them itself. Feeding them in backwards must not change the answer.
    /// </summary>
    [Test]
    public void Order_of_the_input_does_not_matter()
    {
        var forwards = StreakCalculator.Calculate(
            Days(DaysAgo(4), DaysAgo(3), DaysAgo(2), DaysAgo(1), Today), Today);

        var backwards = StreakCalculator.Calculate(
            Days(Today, DaysAgo(1), DaysAgo(2), DaysAgo(3), DaysAgo(4)), Today);

        Assert.That(forwards, Is.EqualTo(backwards));
    }
}
