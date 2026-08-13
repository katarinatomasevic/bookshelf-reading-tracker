using System.Net;

namespace Bookshelf.E2ETests;

/// <summary>
/// Runs once for the whole assembly, before any browser is opened, and checks that there is an
/// application there at all.
///
/// <para>
/// The suite deliberately does not start <c>ng serve</c> or <c>dotnet run</c> itself: owning the
/// lifetime of two long-running processes is a source of failures that have nothing to do with
/// the application under test. The cost of that choice is that a forgotten process shows up as
/// five unrelated "element not found" failures — so this fixture converts it into one sentence
/// that says which process is missing.
/// </para>
/// </summary>
[SetUpFixture]
public class AppFixture
{
    [OneTimeSetUp]
    public async Task VerifyTheApplicationIsRunning()
    {
        var baseUrl = Support.E2ESettings.BaseUrl;

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

        await ExpectAsync(
            client,
            baseUrl,
            HttpStatusCode.OK,
            $"The frontend is not answering at {baseUrl}. Start it with 'npm start' in the "
                + "frontend folder (or set E2E_BASE_URL to where it is running).");

        // A guarded endpoint, asked without a token, through the dev-server proxy. The 401 is the
        // point: it can only come from the API itself, so one request proves the proxy is wired,
        // the backend is up and its authentication pipeline is running.
        //
        // The API's own /health is not usable here. It sits outside /api, so the proxy does not
        // forward it — the dev server answers with index.html and a cheerful 200 while the
        // backend is stopped, which is worse than no check at all.
        await ExpectAsync(
            client,
            $"{baseUrl}/api/shelf",
            HttpStatusCode.Unauthorized,
            "The frontend is running but the API behind /api is not. Start it with 'dotnet run' "
                + "in backend/Bookshelf.Api, and make sure the database container is up "
                + "('docker compose up -d db').");
    }

    private static async Task ExpectAsync(
        HttpClient client,
        string url,
        HttpStatusCode expected,
        string message)
    {
        try
        {
            var response = await client.GetAsync(url);

            if (response.StatusCode != expected)
            {
                Assert.Fail(
                    $"{message}{Environment.NewLine}"
                        + $"Asked {url} and got HTTP {(int)response.StatusCode}, "
                        + $"expected {(int)expected}.");
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            Assert.Fail($"{message}{Environment.NewLine}({exception.Message})");
        }
    }
}
