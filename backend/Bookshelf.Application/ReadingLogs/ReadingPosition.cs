using Bookshelf.Application.Common.Exceptions;
using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;

namespace Bookshelf.Application.ReadingLogs;

/// <summary>
/// The one rule that says where a reader stands in a book:
/// <c>CurrentPage = StartPage + SUM(PagesRead)</c>.
/// <para>
/// It is always the full sum, never an adjustment by the difference. Adding and subtracting
/// deltas is immeasurably faster and can drift apart from the rows it claims to summarise;
/// re-adding a few hundred integers cannot. It also means every writer — logging a day, editing
/// one, deleting one, or setting the starting page — goes through the same line of code instead
/// of each maintaining its own arithmetic.
/// </para>
/// <para>
/// A pure function over an entity, with no repository behind it, so it can be reasoned about
/// (and later tested) without a database — the same shape as <c>StreakCalculator</c>.
/// </para>
/// </summary>
public static class ReadingPosition
{
    /// <summary>
    /// Writes the recomputed position onto the shelf entry.
    /// <para>
    /// A book waiting to be read is deliberately left alone. Moving a book back to "want to
    /// read" clears its position by the F3 transition rule, while its logs stay — those days
    /// were really read and the streak and the activity grid must keep them. Recomputing such
    /// an entry would silently put the reader back on page 240 of a book they had just returned
    /// to the queue. The position comes back the moment the book is set to "reading" again, at
    /// which point the sum is once more the honest answer.
    /// </para>
    /// </summary>
    public static void Apply(UserBook userBook, int loggedPages)
    {
        if (userBook.Status == ReadingStatus.WantToRead)
        {
            return;
        }

        userBook.CurrentPage = userBook.StartPage + loggedPages;
    }

    /// <summary>
    /// Refuses a position past the end of the book. Only checkable when Open Library gave us a
    /// page count — without one there is no end to run past, and guessing would block entries
    /// that are perfectly legitimate.
    /// </summary>
    public static void EnsureWithinBook(UserBook userBook, int loggedPages)
    {
        if (userBook.Book.PageCount is not { } total)
        {
            return;
        }

        if (userBook.StartPage + loggedPages > total)
        {
            throw new ValidationException(
                $"That would take you past page {total}, the last page of this book.");
        }
    }
}
