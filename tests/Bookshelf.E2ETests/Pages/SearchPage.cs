using Bookshelf.E2ETests.Support;
using OpenQA.Selenium;

namespace Bookshelf.E2ETests.Pages;

/// <summary>
/// The search page, and the only page object in the suite that waits on the internet: the results
/// come from Open Library live, through the API proxy, so everything here is given the longer
/// network allowance rather than the ordinary render timeout.
/// </summary>
public sealed class SearchPage(Browser browser)
{
    public void Open()
    {
        browser.Go("/");
        browser.WaitForVisible(Find.Input("search-query"));
    }

    /// <summary>
    /// Runs a search and waits for the grid. Search is explicit by the F2 decision — no debounce —
    /// so the test presses the button exactly as a reader would.
    /// </summary>
    public void SearchFor(string query)
    {
        browser.Type(Find.Input("search-query"), query);
        browser.Click(Find.Button("search-submit"));

        browser.WaitUntil(
            () => browser.IsVisible(Find.TestId("search-results"))
                || browser.IsVisible(Find.TestId("search-no-results")),
            $"Open Library to answer the search for '{query}'",
            E2ESettings.NetworkTimeout);

        Assert.That(
            browser.IsVisible(Find.TestId("search-results")),
            Is.True,
            $"Open Library returned no books for '{query}'. The search itself worked, so this "
                + "is about the query or about Open Library, not about the application.");
    }

    public int ResultCount() => browser.CountOf(Find.BookCardsIn("search-results"));

    /// <summary>The title on the first result, which is what the add-to-shelf test then follows.</summary>
    public string FirstResultTitle()
    {
        var card = browser.WaitForVisible(
            By.CssSelector("[data-testid='search-results'] [data-testid='book-card']"),
            E2ESettings.NetworkTimeout);

        return card.GetAttribute("data-book-title") ?? string.Empty;
    }

    public void AddToShelf(string title)
    {
        var card = browser.WaitForVisible(Find.BookCard(title));
        browser.ScrollIntoView(card);

        card.FindElement(Find.Button("add-to-shelf")).Click();

        browser.WaitUntil(
            () => browser.IsPresent(Find.OnShelfBadge(title)),
            $"'{title}' to be marked as being on the shelf");
    }

    public bool ShowsOnShelfBadge(string title) => browser.IsVisible(Find.OnShelfBadge(title));
}
