using Bookshelf.E2ETests.Support;

namespace Bookshelf.E2ETests.Pages;

/// <summary>
/// Manual entry — the F2 fallback for books Open Library does not have.
///
/// <para>
/// The suite leans on it for a second reason: it is the one way to put a known book on a shelf
/// without going near the network. Tests whose subject is the shelf, the reading log or the
/// recommendation strip use it so that an Open Library outage cannot fail them for something they
/// are not testing. A controlled page count is a bonus — the reading-log test needs to know how
/// long the book is.
/// </para>
/// </summary>
public sealed class ManualBookDialog(Browser browser)
{
    public void Fill(string title, string author, int pageCount, string genres = "fiction")
    {
        browser.Type(Find.Input("manual-book-title"), title);
        browser.Type(Find.Input("manual-book-author"), author);
        browser.Type(Find.Input("manual-book-pages"), pageCount.ToString());
        browser.Type(Find.Input("manual-book-genres"), genres);
    }

    public void Submit()
    {
        browser.Click(Find.Button("manual-book-submit"));
        browser.WaitUntilGone(Find.TestId("manual-book-form"));
    }
}
