using Bookshelf.E2ETests.Pages;
using NUnit.Framework.Interfaces;

namespace Bookshelf.E2ETests.Support;

/// <summary>
/// One browser per test, and the page objects that drive it.
///
/// <para>
/// A browser per test rather than per fixture, because a leftover dialog or a stale token from
/// the previous test is exactly the kind of hidden coupling that makes a suite pass in one order
/// and fail in another.
/// </para>
/// </summary>
public abstract class E2ETestBase
{
    protected Browser Browser { get; private set; } = null!;

    protected NavBar Nav { get; private set; } = null!;

    protected RegisterPage Register { get; private set; } = null!;

    protected LoginPage Login { get; private set; } = null!;

    protected SearchPage Search { get; private set; } = null!;

    protected ShelfPage Shelf { get; private set; } = null!;

    protected ManualBookDialog ManualBook { get; private set; } = null!;

    protected BookDetailModal BookModal { get; private set; } = null!;

    protected RecommendationStrip Recommendations { get; private set; } = null!;

    [SetUp]
    public void StartBrowser()
    {
        Browser = new Browser();

        Nav = new NavBar(Browser);
        Register = new RegisterPage(Browser);
        Login = new LoginPage(Browser);
        Search = new SearchPage(Browser);
        Shelf = new ShelfPage(Browser);
        ManualBook = new ManualBookDialog(Browser);
        BookModal = new BookDetailModal(Browser);
        Recommendations = new RecommendationStrip(Browser);
    }

    [TearDown]
    public void StopBrowser()
    {
        if (TestContext.CurrentContext.Result.Outcome.Status == TestStatus.Failed)
        {
            CaptureFailure();
        }

        Browser.Dispose();
    }

    /// <summary>
    /// Registers a new reader through the form and leaves them logged in — registration signs the
    /// reader in straight away by the F1 decision, so no separate login step is needed.
    /// </summary>
    protected TestUser RegisterNewReader(string label = "reader")
    {
        var user = TestUser.New(label);

        Register.Open();
        Register.Submit(user);
        Nav.WaitUntilSignedIn(user.DisplayName);

        return user;
    }

    /// <summary>
    /// A screenshot and the address at the moment of failure. Between them they usually say
    /// whether the application broke or the test looked in the wrong place.
    /// </summary>
    private void CaptureFailure()
    {
        var name = $"{TestContext.CurrentContext.Test.Name}-{DateTime.Now:HHmmss}.png";
        var path = Browser.SaveScreenshot(name);

        if (path is not null)
        {
            TestContext.AddTestAttachment(path, "Screenshot at failure");
            TestContext.Out.WriteLine($"Screenshot: {path}");
        }

        try
        {
            TestContext.Out.WriteLine($"URL at failure: {Browser.CurrentUrl}");
        }
        catch (Exception)
        {
            // The browser may already be gone; the original failure is what matters.
        }
    }
}
