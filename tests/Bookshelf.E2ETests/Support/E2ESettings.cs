namespace Bookshelf.E2ETests.Support;

/// <summary>
/// Everything the suite needs to know about the machine it is running on, read from environment
/// variables with usable defaults.
///
/// <para>
/// Configuration is environment-driven here for the same reason it is in the API and the frontend
/// (see the project conventions): the tests must run unchanged against <c>ng serve</c> on a
/// developer's laptop and against any other address without a recompile.
/// </para>
/// </summary>
public static class E2ESettings
{
    /// <summary>
    /// Where the application is served. The frontend calls the API through relative paths, so
    /// this single address covers both — the dev server proxies <c>/api</c> to the backend.
    /// </summary>
    public static string BaseUrl =>
        Environment.GetEnvironmentVariable("E2E_BASE_URL")?.TrimEnd('/')
        ?? "http://localhost:4200";

    /// <summary>
    /// A visible browser by default. Watching the suite click through the application is the
    /// point of demonstrating it, and a headless run hides exactly the thing being shown.
    /// </summary>
    public static bool Headless =>
        string.Equals(
            Environment.GetEnvironmentVariable("E2E_HEADLESS"),
            "true",
            StringComparison.OrdinalIgnoreCase);

    /// <summary>How long to wait for anything the application itself renders.</summary>
    public static TimeSpan Timeout => TimeSpan.FromSeconds(ReadInt("E2E_TIMEOUT_SECONDS", 15));

    /// <summary>
    /// The longer allowance for the one flow that reaches Open Library over the internet. Their
    /// search takes one to three seconds on a good day, which is why the application asks for it
    /// explicitly rather than as-you-type (F2) — and why a test cannot hold it to the same clock
    /// as a local render.
    /// </summary>
    public static TimeSpan NetworkTimeout =>
        TimeSpan.FromSeconds(ReadInt("E2E_NETWORK_TIMEOUT_SECONDS", 30));

    /// <summary>Where a screenshot goes when a test fails.</summary>
    public static string ScreenshotDirectory =>
        Environment.GetEnvironmentVariable("E2E_SCREENSHOT_DIR")
        ?? Path.Combine(TestContext.CurrentContext.WorkDirectory, "screenshots");

    private static int ReadInt(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out var value) && value > 0
            ? value
            : fallback;
}
