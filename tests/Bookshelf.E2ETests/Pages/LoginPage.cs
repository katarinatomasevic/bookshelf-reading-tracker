using Bookshelf.E2ETests.Support;

namespace Bookshelf.E2ETests.Pages;

public sealed class LoginPage(Browser browser)
{
    public void Open()
    {
        browser.Go("/login");
        WaitUntilShown();
    }

    public void WaitUntilShown() => browser.WaitForVisible(Find.TestId("login-form"));

    public void Submit(string email, string password)
    {
        browser.Type(Find.Input("login-email"), email);
        browser.Type(Find.Input("login-password"), password);
        browser.Click(Find.Button("login-submit"));
    }

    public void Submit(TestUser user) => Submit(user.Email, user.Password);

    public string ErrorMessage() => browser.TextOf(Find.TestId("login-error"));
}
