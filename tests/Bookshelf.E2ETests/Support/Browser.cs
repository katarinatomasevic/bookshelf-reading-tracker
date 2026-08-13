using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace Bookshelf.E2ETests.Support;

/// <summary>
/// One browser, and the handful of things the page objects do with it.
///
/// <para>
/// Two choices here are worth defending. First, the driver binary is not committed or configured
/// anywhere: Selenium Manager (built into Selenium 4.6 and later) resolves a chromedriver that
/// matches the installed Chrome, so the suite does not break every time Chrome updates itself.
/// </para>
/// <para>
/// Second, the implicit wait is left at zero and every wait is explicit. Mixing the two makes
/// timeouts multiply in ways that are hard to predict, and an implicit wait is worst exactly
/// where it is most tempting — <see cref="IsPresent"/> would pause for the full timeout on every
/// call that correctly finds nothing.
/// </para>
/// </summary>
public sealed class Browser : IDisposable
{
    private readonly IWebDriver driver;

    public Browser()
    {
        var options = new ChromeOptions();

        if (E2ESettings.Headless)
        {
            options.AddArgument("--headless=new");
        }

        // A fixed, generous window: the shelf and the recommendation strip lay out by width, and
        // a narrow default would put controls off screen and turn styling into test failures.
        options.AddArgument("--window-size=1600,1000");

        driver = new ChromeDriver(options);
        driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
    }

    public IWebDriver Driver => driver;

    public string CurrentUrl => driver.Url;

    /// <summary>Opens a path relative to the configured base address.</summary>
    public void Go(string path = "/") =>
        driver.Navigate().GoToUrl($"{E2ESettings.BaseUrl}/{path.TrimStart('/')}");

    // ---------------------------------------------------------------- waiting

    public IWebElement WaitForVisible(By locator, TimeSpan? timeout = null) =>
        Wait(timeout).Until(_ =>
        {
            var element = FindOrNull(locator);
            return IsDisplayed(element) ? element : null;
        })!;

    public IWebElement WaitForClickable(By locator, TimeSpan? timeout = null) =>
        Wait(timeout).Until(_ =>
        {
            var element = FindOrNull(locator);
            return IsDisplayed(element) && IsEnabled(element) ? element : null;
        })!;

    public void WaitUntilGone(By locator, TimeSpan? timeout = null) =>
        Wait(timeout).Until(_ => !IsDisplayed(FindOrNull(locator)));

    /// <summary>An arbitrary condition, with a message that says what was being waited for.</summary>
    public void WaitUntil(Func<bool> condition, string description, TimeSpan? timeout = null)
    {
        var wait = Wait(timeout);
        wait.Message = $"Timed out waiting for: {description}";
        wait.Until(_ => condition());
    }

    // ------------------------------------------------------------ interaction

    public void Click(By locator) =>
        Retrying(locator, WaitForClickable, element =>
        {
            ScrollIntoView(element);

            try
            {
                element.Click();
            }
            catch (ElementClickInterceptedException)
            {
                // A dialog mask or a sticky header caught the click. Going through the DOM is a
                // deliberate fallback rather than the default: it would also "succeed" on a
                // control the reader could not actually reach.
                Execute("arguments[0].click();", element);
            }
        });

    public void Type(By locator, string text) =>
        Retrying(locator, WaitForVisible, element =>
        {
            ScrollIntoView(element);
            element.Clear();
            element.SendKeys(text);
        });

    /// <summary>
    /// Picks an option out of a PrimeNG select. The panel is appended to the body, so the option
    /// is looked for in the whole document rather than under the control.
    /// </summary>
    public void SelectOption(By selectLocator, string optionLabel)
    {
        Click(selectLocator);

        var option = By.XPath(
            $"//li[@role='option'][normalize-space(.)='{optionLabel}']");

        Click(option);
        WaitUntilGone(option);
    }

    public string TextOf(By locator)
    {
        var text = string.Empty;
        Retrying(locator, WaitForVisible, element => text = element.Text.Trim());
        return text;
    }

    public bool IsPresent(By locator) => FindOrNull(locator) is not null;

    public bool IsVisible(By locator) => IsDisplayed(FindOrNull(locator));

    public int CountOf(By locator) => driver.FindElements(locator).Count;

    public void ScrollIntoView(IWebElement element) =>
        Execute("arguments[0].scrollIntoView({block: 'center'});", element);

    public object? Execute(string script, params object[] arguments) =>
        ((IJavaScriptExecutor)driver).ExecuteScript(script, arguments);

    /// <summary>
    /// Wipes the browser's idea of who is logged in. The token lives in localStorage by the F1
    /// decision, so a stale one would otherwise leak from one test into the next.
    /// </summary>
    public void ClearSession()
    {
        Go("/");
        Execute("window.localStorage.clear(); window.sessionStorage.clear();");
        driver.Manage().Cookies.DeleteAllCookies();
    }

    public string? SaveScreenshot(string fileName)
    {
        try
        {
            Directory.CreateDirectory(E2ESettings.ScreenshotDirectory);
            var path = Path.Combine(E2ESettings.ScreenshotDirectory, fileName);
            ((ITakesScreenshot)driver).GetScreenshot().SaveAsFile(path);
            return path;
        }
        catch (Exception)
        {
            // A screenshot is a convenience while diagnosing a failure; failing to take one must
            // not replace the real failure with a less useful one.
            return null;
        }
    }

    public void Dispose()
    {
        try
        {
            driver.Quit();
        }
        finally
        {
            driver.Dispose();
        }
    }

    /// <summary>
    /// Finds an element and acts on it, looking it up again if Angular replaced the node in
    /// between. One retry is enough: the second lookup happens after the re-render that
    /// invalidated the first, so anything still failing is a real problem rather than a race.
    /// </summary>
    private void Retrying(By locator, Func<By, TimeSpan?, IWebElement> find, Action<IWebElement> act)
    {
        try
        {
            act(find(locator, null));
        }
        catch (StaleElementReferenceException)
        {
            act(find(locator, null));
        }
    }

    private WebDriverWait Wait(TimeSpan? timeout) =>
        new(driver, timeout ?? E2ESettings.Timeout)
        {
            PollingInterval = TimeSpan.FromMilliseconds(150),
        };

    private IWebElement? FindOrNull(By locator)
    {
        try
        {
            return driver.FindElement(locator);
        }
        catch (NoSuchElementException)
        {
            return null;
        }
        catch (StaleElementReferenceException)
        {
            return null;
        }
    }

    /// <summary>
    /// Asks whether an element is on screen, treating a node that has gone stale as "no".
    ///
    /// <para>
    /// Reading <c>Displayed</c> is a round trip to the browser, so Angular can re-render between
    /// finding the element and asking about it — and then the property, not the lookup, is what
    /// throws. Every caller here is inside a wait or a boolean check where "it is not there right
    /// now" is the honest answer and the loop should simply try again.
    /// </para>
    /// </summary>
    private static bool IsDisplayed(IWebElement? element)
    {
        try
        {
            return element is { Displayed: true };
        }
        catch (StaleElementReferenceException)
        {
            return false;
        }
    }

    private static bool IsEnabled(IWebElement? element)
    {
        try
        {
            return element is { Enabled: true };
        }
        catch (StaleElementReferenceException)
        {
            return false;
        }
    }
}
