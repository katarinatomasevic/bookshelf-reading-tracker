using Bookshelf.E2ETests.Support;

namespace Bookshelf.E2ETests.Pages;

/// <summary>The three tabs of the shelf, named as F3 names them.</summary>
public enum ShelfTab
{
    WantToRead,
    Reading,
    Read,
}

public sealed class ShelfPage(Browser browser)
{
    public void Open()
    {
        browser.Go("/shelf");
        WaitUntilShown();
    }

    public void WaitUntilShown() => browser.WaitForVisible(Find.TestId("shelf-page"));

    public bool IsEmpty() => browser.IsVisible(Find.TestId("shelf-empty"));

    public void OpenTab(ShelfTab tab)
    {
        browser.Click(Find.TestId(TabTestId(tab)));

        browser.WaitUntil(
            () => browser.IsVisible(Find.TestId(GridTestId(tab)))
                || browser.IsVisible(Find.TestId($"shelf-empty-{Slug(tab)}"))
                || browser.IsVisible(Find.TestId($"shelf-no-matches-{Slug(tab)}")),
            $"the '{tab}' tab to render");
    }

    /// <summary>
    /// The tab heading, which carries its own count — "Reading (1)". The count is what tells a
    /// test that a book has really moved rather than merely disappeared from where it was.
    /// </summary>
    public string TabLabel(ShelfTab tab) => browser.TextOf(Find.TestId(TabTestId(tab)));

    public bool HasBook(ShelfTab tab, string title) =>
        browser.IsPresent(Find.BookCardIn(GridTestId(tab), title));

    public void WaitForBook(ShelfTab tab, string title) =>
        browser.WaitUntil(
            () => HasBook(tab, title),
            $"'{title}' to appear in the '{tab}' tab");

    public void WaitForBookToLeave(ShelfTab tab, string title) =>
        browser.WaitUntil(
            () => !HasBook(tab, title),
            $"'{title}' to leave the '{tab}' tab");

    public int BookCount(ShelfTab tab) => browser.CountOf(Find.BookCardsIn(GridTestId(tab)));

    /// <summary>Opens the shelf modal for one book by clicking its card.</summary>
    public void OpenBook(ShelfTab tab, string title)
    {
        browser.Click(Find.BookCardIn(GridTestId(tab), title));
        browser.WaitForVisible(Find.TestId("shelf-modal"));
    }

    /// <summary>The reading position printed on a card in the "Reading" tab.</summary>
    public string ProgressLabel(string title)
    {
        var card = browser.WaitForVisible(Find.BookCardIn(GridTestId(ShelfTab.Reading), title));
        return card.FindElement(Find.TestId("reading-progress-label")).Text.Trim();
    }

    /// <summary>
    /// The header button, which stays put whether the shelf is full or empty. (The empty state
    /// offers a second link to the same dialog; one way in is enough for a test.)
    /// </summary>
    public void OpenManualBookDialog()
    {
        browser.Click(Find.Button("shelf-add-manually"));
        browser.WaitForVisible(Find.TestId("manual-book-form"));
    }

    public void SearchShelf(string term) => browser.Type(Find.Input("shelf-search"), term);

    private static string TabTestId(ShelfTab tab) => $"shelf-tab-{Slug(tab)}";

    private static string GridTestId(ShelfTab tab) => $"shelf-grid-{Slug(tab)}";

    private static string Slug(ShelfTab tab) => tab switch
    {
        ShelfTab.WantToRead => "want",
        ShelfTab.Reading => "reading",
        ShelfTab.Read => "read",
        _ => throw new ArgumentOutOfRangeException(nameof(tab)),
    };
}
