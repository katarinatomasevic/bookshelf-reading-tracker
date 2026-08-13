using Bookshelf.E2ETests.Support;
using OpenQA.Selenium;

namespace Bookshelf.E2ETests.Pages;

/// <summary>
/// The header, which is also the suite's evidence of who is signed in: the navigation swaps
/// between the guest links and the reader's own by the guest-mode decision in F1.
/// </summary>
public sealed class NavBar(Browser browser)
{
    public void WaitUntilSignedIn(string displayName)
    {
        browser.WaitUntil(
            () => browser.IsVisible(Find.TestId("nav-shelf")),
            "the header to show the links of a signed-in reader");

        var shown = browser.TextOf(Find.TestId("nav-display-name"));

        Assert.That(
            shown,
            Is.EqualTo(displayName),
            "The header should greet the reader who just signed in.");
    }

    public void WaitUntilSignedOut() =>
        browser.WaitUntil(
            () => browser.IsVisible(Find.TestId("nav-login")),
            "the header to fall back to the guest links");

    public void GoToShelf()
    {
        browser.Click(Find.TestId("nav-shelf"));
        browser.WaitForVisible(Find.TestId("shelf-page"));
    }

    /// <summary>
    /// Signs out through the profile menu. Its items come from a PrimeNG model rather than the
    /// template, so this is the one place the suite matches on a label instead of an attribute.
    /// </summary>
    public void SignOut()
    {
        browser.Click(Find.TestId("nav-profile-menu"));
        browser.Click(
            By.XPath("//a[contains(@class,'p-menu-item-link')][.//span[normalize-space()='Log out']]"));
        WaitUntilSignedOut();
    }
}
