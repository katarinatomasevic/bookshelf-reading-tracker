using Bookshelf.Application.Common.Exceptions;
using Bookshelf.Application.ReadingLogs;
using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;

namespace Bookshelf.UnitTests.ReadingLogs;

/// <summary>
/// The position rule from F4: <c>CurrentPage = StartPage + SUM(PagesRead)</c>.
///
/// <para>
/// Three rules meet in this one class — the starting page, the status that is skipped, and the
/// end of the book — and all three have to be explained at the defence anyway. Like
/// <c>StreakCalculator</c> it is a pure function, here over an entity that is built in memory,
/// so nothing below touches a database or a repository.
/// </para>
/// </summary>
[TestFixture]
public class ReadingPositionTests
{
    private static UserBook Entry(
        ReadingStatus status = ReadingStatus.Reading,
        int startPage = 0,
        int? pageCount = 300,
        int? currentPage = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            BookId = Guid.NewGuid(),
            Status = status,
            StartPage = startPage,
            CurrentPage = currentPage,
            Book = new Book { Title = "Test book", PageCount = pageCount },
        };

    [Test]
    public void Position_is_the_start_page_plus_everything_logged()
    {
        var entry = Entry(startPage: 200);

        ReadingPosition.Apply(entry, loggedPages: 45);

        Assert.That(entry.CurrentPage, Is.EqualTo(245));
    }

    /// <summary>
    /// A book entered from the front is the ordinary case, and there the rule collapses to the
    /// plain sum of the logs.
    /// </summary>
    [Test]
    public void A_book_started_from_the_front_is_just_the_sum()
    {
        var entry = Entry(startPage: 0);

        ReadingPosition.Apply(entry, loggedPages: 120);

        Assert.That(entry.CurrentPage, Is.EqualTo(120));
    }

    /// <summary>
    /// The full sum, never an adjustment by the difference: recomputing after a correction has
    /// to land on the same number whatever the column held before.
    /// </summary>
    [Test]
    public void Recomputing_replaces_the_stored_position_instead_of_adjusting_it()
    {
        var entry = Entry(startPage: 50, currentPage: 500);

        ReadingPosition.Apply(entry, loggedPages: 30);

        Assert.That(entry.CurrentPage, Is.EqualTo(80));
    }

    /// <summary>
    /// The F3 transition rule, seen from this side: a book moved back to "want to read" keeps its
    /// logs — those days really happened and the streak counts them — but shows no position.
    /// Recomputing it here would silently put the reader back on page 240 of a book they had just
    /// returned to the queue.
    /// </summary>
    [Test]
    public void A_book_waiting_to_be_read_keeps_no_position()
    {
        var entry = Entry(status: ReadingStatus.WantToRead, startPage: 200, currentPage: null);

        ReadingPosition.Apply(entry, loggedPages: 40);

        Assert.That(entry.CurrentPage, Is.Null);
    }

    [Test]
    public void A_finished_book_still_gets_its_position_recomputed()
    {
        var entry = Entry(status: ReadingStatus.Read, startPage: 0);

        ReadingPosition.Apply(entry, loggedPages: 300);

        Assert.That(entry.CurrentPage, Is.EqualTo(300));
    }

    [Test]
    public void Reading_past_the_last_page_is_refused()
    {
        var entry = Entry(startPage: 0, pageCount: 300);

        Assert.Throws<ValidationException>(
            () => ReadingPosition.EnsureWithinBook(entry, loggedPages: 301));
    }

    /// <summary>The start page counts towards the limit — that is the whole point of the column.</summary>
    [Test]
    public void The_start_page_counts_towards_the_end_of_the_book()
    {
        var entry = Entry(startPage: 280, pageCount: 300);

        Assert.Throws<ValidationException>(
            () => ReadingPosition.EnsureWithinBook(entry, loggedPages: 25));
    }

    [Test]
    public void Landing_exactly_on_the_last_page_is_allowed()
    {
        var entry = Entry(startPage: 100, pageCount: 300);

        Assert.DoesNotThrow(() => ReadingPosition.EnsureWithinBook(entry, loggedPages: 200));
    }

    /// <summary>
    /// Open Library often has no page count. There is then no end to run past, and guessing one
    /// would refuse entries that are perfectly legitimate.
    /// </summary>
    [Test]
    public void Without_a_known_page_count_there_is_nothing_to_run_past()
    {
        var entry = Entry(startPage: 0, pageCount: null);

        Assert.DoesNotThrow(() => ReadingPosition.EnsureWithinBook(entry, loggedPages: 5000));
    }
}
