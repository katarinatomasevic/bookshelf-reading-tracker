using Bookshelf.E2ETests.Support;

namespace Bookshelf.E2ETests.Pages;

/// <summary>
/// The shelf modal from F3 — one form for everything this reader records about a book.
///
/// <para>
/// The order of the methods here follows the decision that made the modal one form: changes are
/// made to the fields, and nothing reaches the backend until <see cref="SaveChanges"/>. The
/// reading log is the exception, and has its own section and its own button, because a day's
/// reading is an event rather than a field.
/// </para>
/// </summary>
public sealed class BookDetailModal(Browser browser)
{
    /// <summary>The labels of the status dropdown, exactly as F3 names the three statuses.</summary>
    public const string WantToRead = "Want to read";

    public const string Reading = "Reading";

    public const string Read = "Read";

    public void WaitUntilShown() => browser.WaitForVisible(Find.TestId("shelf-modal"));

    public string Title() => browser.TextOf(Find.TestId("modal-title"));

    public void ChooseStatus(string label) =>
        browser.SelectOption(Find.TestId("modal-status"), label);

    public void SetStartPage(int page) => browser.Type(Find.Input("modal-start-page"), page.ToString());

    /// <summary>
    /// Saves and waits for the button to say so. The button is disabled until the form is dirty
    /// (F3), so a test that changed nothing would hang here rather than silently pass — which is
    /// the honest outcome.
    /// </summary>
    public void SaveChanges()
    {
        browser.Click(Find.Button("modal-save"));

        browser.WaitUntil(
            () => browser.TextOf(Find.Button("modal-save")).Contains("Saved", StringComparison.Ordinal),
            "the shelf modal to confirm the save");
    }

    public void Close()
    {
        browser.Click(Find.Button("modal-close"));
        browser.WaitUntilGone(Find.TestId("shelf-modal"));
    }

    /// <summary>
    /// The progress section only exists while the book is being read — the field is keyed to the
    /// status, which is why the status change has to be saved before this appears.
    /// </summary>
    public bool HasProgressSection() => browser.IsVisible(Find.TestId("modal-progress-section"));

    public void WaitForProgressSection() =>
        browser.WaitUntil(
            HasProgressSection,
            "the reading-progress section to appear once the book is being read");

    /// <summary>
    /// Logs a day's reading. The date field defaults to today, and today is what the test wants,
    /// so it is left alone: typing into a native date input is locale-dependent and would test
    /// the browser rather than the application.
    /// </summary>
    public void LogProgress(int pagesRead)
    {
        browser.Type(Find.Input("progress-amount"), pagesRead.ToString());
        browser.Click(Find.Button("progress-submit"));

        browser.WaitForVisible(Find.TestId("progress-saved"));
    }

    /// <summary>
    /// The quiet line under the entry field. Its job is to name the day's total, which is not
    /// always what was just typed — a second entry on the same day is added to the first.
    /// </summary>
    public string SavedMessage() => browser.TextOf(Find.TestId("progress-saved"));

    public string ProgressLabel() =>
        browser.WaitForVisible(Find.TestId("modal-progress-section"))
            .FindElement(Find.TestId("reading-progress-label"))
            .Text.Trim();
}
