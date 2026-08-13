namespace Bookshelf.E2ETests.Support;

/// <summary>
/// A fresh account for one test.
///
/// <para>
/// Every test registers its own reader rather than sharing a fixture account. That is what makes
/// the suite repeatable: a shared account accumulates the shelf of every run before it, so the
/// second run of "the shelf is empty for a new reader" would fail on data the first run left
/// behind.
/// </para>
/// <para>
/// Nothing is cleaned up afterwards. Deleting a reader would mean building an endpoint for it,
/// which is a feature the application does not otherwise have and would exist only to serve its
/// own tests. The <c>bookshelf.test</c> domain makes the leftovers obvious to anyone looking at
/// the database.
/// </para>
/// </summary>
public sealed record TestUser(string DisplayName, string Email, string Password)
{
    public static TestUser New(string label = "reader")
    {
        // Timestamp for readability when scanning the table, random suffix so two tests started
        // in the same second cannot collide on the unique email index.
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var suffix = Guid.NewGuid().ToString("N")[..6];

        return new TestUser(
            DisplayName: $"E2E {label}",
            Email: $"e2e-{label}-{stamp}-{suffix}@bookshelf.test",

            // Meets the F1 rule: at least eight characters, with an upper case letter, a lower
            // case letter, a digit and a symbol. The backend enforces it, so a password that
            // missed one of these would fail registration for a reason unrelated to the test.
            Password: $"E2e!test{suffix}");
    }
}
