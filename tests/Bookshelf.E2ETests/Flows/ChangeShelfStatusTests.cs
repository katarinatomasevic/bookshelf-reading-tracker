using Bookshelf.E2ETests.Pages;
using Bookshelf.E2ETests.Support;

namespace Bookshelf.E2ETests.Flows;

/// <summary>
/// Flow 3 — moving a book between the tabs of the shelf.
///
/// <para>
/// The book is entered by hand rather than searched for. That keeps this fixture off the network
/// entirely: the subject is the shelf modal, and an Open Library outage has no business failing a
/// test about it.
/// </para>
/// </summary>
[TestFixture]
[Category("E2E")]
public class ChangeShelfStatusTests : E2ETestBase
{
    private const string Title = "The E2E Guide to Shelving";

    private const string Author = "Test Author";

    [Test]
    public void A_book_moves_to_the_reading_tab_when_its_status_changes()
    {
        RegisterNewReader("status");
        AddBookByHand();

        Shelf.OpenTab(ShelfTab.WantToRead);
        Shelf.WaitForBook(ShelfTab.WantToRead, Title);

        Shelf.OpenBook(ShelfTab.WantToRead, Title);
        Assert.That(BookModal.Title(), Is.EqualTo(Title));

        BookModal.ChooseStatus(BookDetailModal.Reading);
        BookModal.SaveChanges();
        BookModal.Close();

        // It has to be gone from where it was as well as present where it went — a book showing
        // in two tabs at once would pass a test that only looked at the destination.
        Shelf.WaitForBookToLeave(ShelfTab.WantToRead, Title);

        Shelf.OpenTab(ShelfTab.Reading);
        Shelf.WaitForBook(ShelfTab.Reading, Title);

        Assert.Multiple(() =>
        {
            Assert.That(Shelf.TabLabel(ShelfTab.Reading), Does.Contain("(1)"));
            Assert.That(Shelf.TabLabel(ShelfTab.WantToRead), Does.Contain("(0)"));
        });
    }

    /// <summary>
    /// The whole way across the shelf, which also exercises the automatic dates from F3 — going
    /// to "Read" fills in a finish date, and a started date if there was not one.
    /// </summary>
    [Test]
    public void A_book_can_be_taken_all_the_way_to_read()
    {
        RegisterNewReader("finish");
        AddBookByHand();

        Shelf.OpenTab(ShelfTab.WantToRead);
        Shelf.OpenBook(ShelfTab.WantToRead, Title);

        BookModal.ChooseStatus(BookDetailModal.Read);
        BookModal.SaveChanges();
        BookModal.Close();

        Shelf.OpenTab(ShelfTab.Read);
        Shelf.WaitForBook(ShelfTab.Read, Title);

        Assert.That(Shelf.TabLabel(ShelfTab.Read), Does.Contain("(1)"));
    }

    private void AddBookByHand()
    {
        Shelf.Open();
        Shelf.OpenManualBookDialog();
        ManualBook.Fill(Title, Author, pageCount: 320);
        ManualBook.Submit();
    }
}
