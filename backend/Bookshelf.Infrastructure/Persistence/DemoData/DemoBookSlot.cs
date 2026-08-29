using Bookshelf.Domain.Enums;

namespace Bookshelf.Infrastructure.Persistence.DemoData;

/// <summary>
/// One place on the demo shelf, described by what it has to demonstrate rather than by a database
/// row. The seeder resolves each slot against the seeded corpus at run time — see
/// <see cref="DemoBookPicker"/> — so nothing here is a hard dependency on a particular book
/// existing.
/// <para>
/// Page-dependent numbers are stored as <b>fractions</b>, never as absolute page numbers. If the
/// preferred title is missing and the picker falls back to another book from the same topic, that
/// book has a different length: a hard-coded starting page of 180 could then sit past the end of a
/// 150-page replacement and the seed would produce data the application itself would refuse.
/// Fractions of the real page count cannot.
/// </para>
/// </summary>
internal sealed record DemoBookSlot
{
    /// <summary>Preferred title, matched case-insensitively and exactly.</summary>
    public required string Title { get; init; }

    /// <summary>
    /// Enough of the author's name to tell two books with the same title apart. Matched as a
    /// substring, because Open Library often lists translators and transliterations alongside the
    /// author ("Frank Herbert, Френк Герберт").
    /// </summary>
    public required string Author { get; init; }

    /// <summary>Topic the picker draws from when the preferred title is not in the corpus.</summary>
    public required string FallbackTopic { get; init; }

    public required ReadingStatus Status { get; init; }

    /// <summary>1-5, or null for a book the reader has not rated.</summary>
    public int? Rating { get; init; }

    public string? Note { get; init; }

    /// <summary>
    /// Keeps a fallback pick from being too short to carry this slot's story — a book started
    /// from the middle needs enough pages for a starting page to be meaningful.
    /// </summary>
    public int MinPageCount { get; init; } = 150;

    /// <summary>
    /// How many days ago the book was finished, for a book that has been read. Null for anything
    /// still in progress or still waiting.
    /// </summary>
    public int? FinishedDaysAgo { get; init; }

    /// <summary>
    /// The oldest day this book may hold a log entry on. Together with
    /// <see cref="FinishedDaysAgo"/> it fences the reading off in time, so that no book is ever
    /// logged after the day it was finished — nothing in the schema forbids that, but a reader who
    /// logged pages a month after finishing the book would look like a bug during the defence.
    /// </summary>
    public int WindowStartDaysAgo { get; init; }

    /// <summary>
    /// Whether this book's reading is backed by log rows. At least one read book deliberately has
    /// none: F4 counts a finished book by its page count and ignores its logs, precisely so that a
    /// book read years before the application existed still counts. That rule is invisible unless
    /// the demo contains such a book.
    /// </summary>
    public bool Logged { get; init; }

    /// <summary>Starting page, as a fraction of the book's real length. See the note above.</summary>
    public double StartPageFraction { get; init; }

    /// <summary>
    /// How much of what is left after <see cref="StartPageFraction"/> gets logged. 1.0 for a
    /// finished book, so that the sum of its entries lands exactly on the last page and the
    /// "position never exceeds the page count" check is tight rather than accidentally satisfied.
    /// </summary>
    public double ProgressFraction { get; init; }
}
