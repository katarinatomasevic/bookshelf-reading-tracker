using Bookshelf.E2ETests.Support;

namespace Bookshelf.E2ETests.Pages;

/// <summary>
/// The recommendation strip at the foot of the shelf.
///
/// <para>
/// This is where the recommendations live — the whole of them. Phase 9 removed the separate
/// <c>/recommendations</c> route after it turned out to be the same grid behind a second address,
/// so a test that navigated there would be testing a page the application no longer has.
/// </para>
/// </summary>
public sealed class RecommendationStrip(Browser browser)
{
    /// <summary>
    /// Waits for the strip to settle on one of its three outcomes: books, nothing to suggest, or
    /// gone altogether (it hides itself rather than showing an error, by the F5 decision).
    /// </summary>
    public void WaitUntilLoaded() =>
        browser.WaitUntil(
            () => browser.IsVisible(Find.TestId("recommendation-strip-track"))
                || browser.IsVisible(Find.TestId("recommendation-strip-empty"))
                || !browser.IsPresent(Find.TestId("recommendation-strip")),
            "the recommendation strip to finish loading");

    public bool IsShown() => browser.IsVisible(Find.TestId("recommendation-strip"));

    public string Heading() => browser.TextOf(Find.TestId("recommendation-strip-title"));

    /// <summary>
    /// Which rung of F5 produced this list. Cold start reads "Popular on Bookshelf" and carries
    /// no attribution, because there is no book on the shelf to attribute it to.
    /// </summary>
    public bool IsColdStart() =>
        browser.WaitForVisible(Find.TestId("recommendation-strip-title"))
            .GetAttribute("data-cold-start") == "true";

    public int CardCount() => browser.CountOf(Find.TestId("recommendation-card"));

    /// <summary>The titles on offer, in the order the strip shows them.</summary>
    public IReadOnlyList<string> Titles() =>
        browser.Driver
            .FindElements(Find.BookCardsIn("recommendation-strip-track"))
            .Select(card => card.GetAttribute("data-book-title") ?? string.Empty)
            .ToList();

    /// <summary>The line under each card — "Because you liked X", or "Popular" for cold start.</summary>
    public IReadOnlyList<string> Reasons() =>
        browser.Driver
            .FindElements(Find.TestId("recommendation-card-reason"))
            .Select(reason => reason.Text.Trim())
            .ToList();

    /// <summary>
    /// Asks for the next ten rather than the same ten again — the <c>?offset=</c> that made the
    /// refresh button defensible in Phase 9.
    /// </summary>
    public void ShowDifferentBooks()
    {
        browser.Click(Find.Button("recommendation-strip-refresh"));
        WaitUntilLoaded();
    }
}
