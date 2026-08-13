using Bookshelf.E2ETests.Pages;
using Bookshelf.E2ETests.Support;

namespace Bookshelf.E2ETests.Flows;

/// <summary>
/// Flow 4 — logging a day's reading.
///
/// <para>
/// The book is entered by hand with a known page count, because the assertions are about numbers:
/// a book of unknown length has no percentage to show and no end to run past.
/// </para>
/// </summary>
[TestFixture]
[Category("E2E")]
public class LogReadingProgressTests : E2ETestBase
{
    private const string Title = "A Short History of Test Data";

    private const int PageCount = 400;

    [Test]
    public void Logging_pages_moves_the_reading_position()
    {
        RegisterNewReader("progress");
        StartReading();

        BookModal.LogProgress(pagesRead: 40);

        Assert.That(
            BookModal.ProgressLabel(),
            Does.Contain("Page 40").And.Contain(PageCount.ToString()),
            "The position should follow the pages just logged.");

        BookModal.Close();

        // The card on the shelf reads from the same value, so it must agree with the modal.
        Shelf.OpenTab(ShelfTab.Reading);

        Assert.That(Shelf.ProgressLabel(Title), Does.Contain("Page 40"));
    }

    /// <summary>
    /// The rule behind <c>UNIQUE (UserBookId, Date)</c> from F4: a second entry on the same day is
    /// added to the first rather than becoming a second row. Without it the activity grid would
    /// count one day twice.
    /// </summary>
    [Test]
    public void Two_entries_on_the_same_day_add_up()
    {
        RegisterNewReader("sameday");
        StartReading();

        BookModal.LogProgress(pagesRead: 30);
        BookModal.LogProgress(pagesRead: 25);

        Assert.That(
            BookModal.ProgressLabel(),
            Does.Contain("Page 55"),
            "Two entries on one day should sum to a single position.");
    }

    /// <summary>
    /// The other half of that rule: the confirmation line names the day's running total, which is
    /// not the number just typed. Without it the reader has no way of seeing that the two entries
    /// were merged.
    /// </summary>
    [Test]
    public void The_confirmation_names_the_total_for_the_day()
    {
        RegisterNewReader("daytotal");
        StartReading();

        BookModal.LogProgress(pagesRead: 30);
        BookModal.LogProgress(pagesRead: 25);

        Assert.That(BookModal.SavedMessage(), Does.Contain("55"));
    }

    /// <summary>
    /// Puts a freshly registered reader in front of an open modal for a book they are reading —
    /// the state every test in this fixture starts from.
    /// </summary>
    private void StartReading()
    {
        Shelf.Open();
        Shelf.OpenManualBookDialog();
        ManualBook.Fill(Title, "Test Author", PageCount);
        ManualBook.Submit();

        Shelf.OpenTab(ShelfTab.WantToRead);
        Shelf.WaitForBook(ShelfTab.WantToRead, Title);
        Shelf.OpenBook(ShelfTab.WantToRead, Title);

        // The entry section is keyed to the status, and the status has to be saved before it
        // appears — a book waiting to be read has no position to move (F4, step 2).
        Assert.That(
            BookModal.HasProgressSection(),
            Is.False,
            "A book that is only wanted should not offer a progress field.");

        BookModal.ChooseStatus(BookDetailModal.Reading);
        BookModal.SaveChanges();
        BookModal.WaitForProgressSection();
    }
}
