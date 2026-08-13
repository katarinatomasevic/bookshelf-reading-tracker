using OpenQA.Selenium;

namespace Bookshelf.E2ETests.Support;

/// <summary>
/// Turns a <c>data-testid</c> into a locator.
///
/// <para>
/// Every locator in the suite goes through here, which is what keeps the tests off brittle CSS
/// paths: a test says <c>Find.Button("modal-save")</c>, and any restyling of the shelf modal
/// leaves it alone as long as the attribute stays.
/// </para>
/// <para>
/// The pair of selectors in <see cref="Button"/> and <see cref="Input"/> exists because of
/// PrimeNG. An attribute written on <c>&lt;p-button data-testid="x"&gt;</c> stays on that host
/// element — the real <c>&lt;button&gt;</c> is rendered inside it — while the same attribute on a
/// plain <c>&lt;button&gt;</c> is already on the element itself. Accepting both spellings means a
/// test never has to know which kind of control it is clicking.
/// </para>
/// </summary>
public static class Find
{
    /// <summary>The element carrying the attribute, whatever it is.</summary>
    public static By TestId(string testId) => By.CssSelector(Attribute(testId));

    /// <summary>A clickable button, whether it is a bare button or one wrapped by PrimeNG.</summary>
    public static By Button(string testId) =>
        By.CssSelector($"button{Attribute(testId)}, {Attribute(testId)} button");

    /// <summary>A text field, whether it is a bare input or one wrapped by PrimeNG.</summary>
    public static By Input(string testId) =>
        By.CssSelector(
            $"input{Attribute(testId)}, textarea{Attribute(testId)}, "
                + $"{Attribute(testId)} input, {Attribute(testId)} textarea");

    /// <summary>A book card anywhere on the page, addressed by the title it is showing.</summary>
    public static By BookCard(string title) =>
        By.CssSelector($"[data-testid='book-card'][data-book-title='{Escape(title)}']");

    /// <summary>A book card inside one named container — a single shelf tab, say.</summary>
    public static By BookCardIn(string containerTestId, string title) =>
        By.CssSelector(
            $"{Attribute(containerTestId)} [data-testid='book-card'][data-book-title='{Escape(title)}']");

    /// <summary>Every book card inside one named container.</summary>
    public static By BookCardsIn(string containerTestId) =>
        By.CssSelector($"{Attribute(containerTestId)} [data-testid='book-card']");

    /// <summary>The "On shelf" badge on the card for one book.</summary>
    public static By OnShelfBadge(string title) =>
        By.CssSelector(
            $"[data-testid='book-card'][data-book-title='{Escape(title)}'] "
                + "[data-testid='book-card-on-shelf-badge']");

    private static string Attribute(string testId) => $"[data-testid='{Escape(testId)}']";

    /// <summary>
    /// Titles come from test data, but a stray apostrophe would silently break the selector
    /// rather than fail loudly, so it is escaped rather than trusted.
    /// </summary>
    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("'", "\\'");
}
