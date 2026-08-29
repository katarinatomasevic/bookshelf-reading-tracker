using Bookshelf.Domain.Enums;

namespace Bookshelf.Infrastructure.Persistence.DemoData;

/// <summary>
/// The demo shelf: fourteen books, each of which exists to make one rule of the application
/// visible on screen.
/// <para>
/// The reading windows are <b>contiguous</b> and cover the whole of the last twenty-six weeks
/// without a gap: 0-25 days ago for the three books in progress, then 26-53, 54-84, 85-117,
/// 118-149 and 150-182 for the five most recently finished ones. That is not decoration. The
/// activity calendar in <see cref="DemoReadingHistory"/> is built first, as a shape — a live
/// streak, a record run, two real pauses — and every active day is then handed to whichever book
/// was being read that day. A gap between windows would be a day the calendar planned for and no
/// book could claim, which would quietly shorten a run and leave the streak reading something
/// other than what was designed.
/// </para>
/// </summary>
internal static class DemoBooks
{
    /// <summary>The three books in progress share this window, ending today.</summary>
    public const int TailWindowDaysAgo = 25;

    public static IReadOnlyList<DemoBookSlot> All { get; } =
    [
        // ---- Read, oldest first -------------------------------------------------------------
        // The three oldest carry no log entries at all. They fall outside the twenty-six week
        // activity window anyway, and they are what makes the F4 rule visible: the statistics
        // count them by their full page count, because a book read before the application existed
        // was still read.
        new()
        {
            Title = "The Hobbit",
            Author = "Tolkien",
            FallbackTopic = "fantasy",
            Status = ReadingStatus.Read,
            Rating = 5,
            Note = "Reread it after years. Still the best opening chapter I know.",
            FinishedDaysAgo = 335,
            Logged = false,
            ProgressFraction = 1.0,
        },
        new()
        {
            Title = "Fahrenheit 451",
            Author = "Bradbury",
            FallbackTopic = "science fiction",
            Status = ReadingStatus.Read,
            Rating = 4,
            FinishedDaysAgo = 270,
            Logged = false,
            ProgressFraction = 1.0,
        },
        new()
        {
            Title = "The Hunger Games",
            Author = "Collins",
            FallbackTopic = "science fiction",
            Status = ReadingStatus.Read,
            Rating = 5,
            FinishedDaysAgo = 205,
            Logged = false,
            ProgressFraction = 1.0,
        },

        // The five below are logged day by day, and each one's entries add up to exactly its page
        // count — so CurrentPage lands on the last page rather than near it.
        new()
        {
            // Rated 2 on purpose. From Faza 9 a rating of 1-2 turns a book into a negative
            // source: its author stops being recommended. Without a low rating on the demo shelf
            // that half of the recommender cannot be shown at all.
            Title = "The Da Vinci Code",
            Author = "Brown",
            FallbackTopic = "mystery",
            Status = ReadingStatus.Read,
            Rating = 2,
            Note = "Finished it out of stubbornness. Not for me.",
            FinishedDaysAgo = 150,
            WindowStartDaysAgo = 182,
            Logged = true,
            ProgressFraction = 1.0,
        },
        new()
        {
            Title = "The Song of Achilles",
            Author = "Miller",
            FallbackTopic = "historical fiction",
            Status = ReadingStatus.Read,
            Rating = 5,
            Note = "Knew how it ends. Cried anyway.",
            FinishedDaysAgo = 118,
            WindowStartDaysAgo = 149,
            Logged = true,
            ProgressFraction = 1.0,
        },
        new()
        {
            // Unrated, and deliberately so: F3 allows a rating on any status but never requires
            // one, and the shelf has to look right with the stars empty.
            Title = "Meditations",
            Author = "Aurelius",
            FallbackTopic = "philosophy",
            Status = ReadingStatus.Read,
            FinishedDaysAgo = 85,
            WindowStartDaysAgo = 117,
            Logged = true,
            ProgressFraction = 1.0,
        },
        new()
        {
            Title = "Where the Crawdads Sing",
            Author = "Owens",
            FallbackTopic = "mystery",
            Status = ReadingStatus.Read,
            Rating = 4,
            FinishedDaysAgo = 54,
            WindowStartDaysAgo = 84,
            Logged = true,
            ProgressFraction = 1.0,
        },
        new()
        {
            Title = "Educated",
            Author = "Westover",
            FallbackTopic = "biography",
            Status = ReadingStatus.Read,
            Rating = 4,
            Note = "Borrowed from Milica. Have to give it back.",
            FinishedDaysAgo = 26,
            WindowStartDaysAgo = 53,
            Logged = true,
            ProgressFraction = 1.0,
        },

        // ---- Reading ------------------------------------------------------------------------
        new()
        {
            // The book started from the middle. StartPage is what lets CurrentPage be recomputed
            // as StartPage + SUM(PagesRead) for a reader who was already on page 180 when the
            // book reached the shelf — no invented log entry stands in for those pages, and they
            // deliberately count for nothing on the dashboard.
            Title = "A Game of Thrones",
            Author = "Martin",
            FallbackTopic = "fantasy",
            Status = ReadingStatus.Reading,
            Note = "Picked it back up from where the paperback was left.",
            MinPageCount = 400,
            WindowStartDaysAgo = TailWindowDaysAgo,
            Logged = true,
            StartPageFraction = 0.225,
            ProgressFraction = 0.40,
        },
        new()
        {
            // Rated while still unfinished. F3 allows a rating on any status, and F4 counts only
            // finished books, so this rating shows on the shelf card and touches no statistic.
            // It is also a recommender source: F5 draws on ratings of 4-5 whatever the status.
            Title = "Sapiens",
            Author = "Harari",
            FallbackTopic = "history",
            Status = ReadingStatus.Reading,
            Rating = 4,
            WindowStartDaysAgo = TailWindowDaysAgo,
            Logged = true,
            ProgressFraction = 0.47,
        },
        new()
        {
            Title = "Normal People",
            Author = "Rooney",
            FallbackTopic = "romance",
            Status = ReadingStatus.Reading,
            WindowStartDaysAgo = TailWindowDaysAgo,
            Logged = true,
            ProgressFraction = 0.32,
        },

        // ---- Want to read -------------------------------------------------------------------
        // No dates, no position, no logs. A note on one of them, because F3 allows a note before
        // the reading starts and that is exactly what this status is for.
        new()
        {
            Title = "Great Expectations",
            Author = "Dickens",
            FallbackTopic = "classic literature",
            Status = ReadingStatus.WantToRead,
        },
        new()
        {
            Title = "Wonder",
            Author = "Palacio",
            FallbackTopic = "young adult fiction",
            Status = ReadingStatus.WantToRead,
            Note = "Recommended by Milica.",
        },
        new()
        {
            Title = "Pride and Prejudice",
            Author = "Austen",
            FallbackTopic = "romance",
            Status = ReadingStatus.WantToRead,
        },
    ];
}
