using Bookshelf.E2ETests.Pages;
using Bookshelf.E2ETests.Support;

namespace Bookshelf.E2ETests.Flows;

/// <summary>
/// Flow 2 — find a book and put it on the shelf.
///
/// <para>
/// The only fixture in the suite that depends on the internet. Search proxies through the API to
/// Open Library live (F2), so a failure here can mean their service rather than this code — which
/// is why the rest of the suite gets its books from manual entry instead. The query is a
/// deliberately unmissable one, and the waits are given the longer network allowance.
/// </para>
/// </summary>
[TestFixture]
[Category("E2E")]
public class SearchAndAddToShelfTests : E2ETestBase
{
    private const string Query = "Dune Frank Herbert";

    [Test]
    public void A_book_found_on_open_library_can_be_added_to_the_shelf()
    {
        RegisterNewReader("search");

        Search.Open();
        Search.SearchFor(Query);

        Assert.That(Search.ResultCount(), Is.GreaterThan(0), "The search should return a grid.");

        var title = Search.FirstResultTitle();
        Search.AddToShelf(title);

        // The badge is the search page's own answer; the shelf is the one that matters.
        Nav.GoToShelf();
        Shelf.OpenTab(ShelfTab.WantToRead);
        Shelf.WaitForBook(ShelfTab.WantToRead, title);

        Assert.That(
            Shelf.TabLabel(ShelfTab.WantToRead),
            Does.Contain("(1)"),
            "A book added from search arrives under 'Want to read' (F2).");
    }

    /// <summary>
    /// The badge from F2: the search endpoint is public but reads the token when there is one, so
    /// a book already on the reader's shelf is marked as such on the way back.
    /// </summary>
    [Test]
    public void A_book_already_on_the_shelf_is_marked_in_later_searches()
    {
        RegisterNewReader("badge");

        Search.Open();
        Search.SearchFor(Query);

        var title = Search.FirstResultTitle();
        Search.AddToShelf(title);

        // A fresh search, so the badge has to come from the server rather than from what the page
        // happens to remember about the click.
        Search.Open();
        Search.SearchFor(Query);

        Assert.That(
            Search.ShowsOnShelfBadge(title),
            Is.True,
            $"'{title}' is on this reader's shelf, so the result should say so.");
    }
}
