using Bookshelf.E2ETests.Support;

namespace Bookshelf.E2ETests.Pages;

public sealed class RegisterPage(Browser browser)
{
    public void Open()
    {
        browser.Go("/register");
        browser.WaitForVisible(Find.TestId("register-form"));
    }

    public void Submit(TestUser user)
    {
        browser.Type(Find.Input("register-display-name"), user.DisplayName);
        browser.Type(Find.Input("register-email"), user.Email);
        browser.Type(Find.Input("register-password"), user.Password);
        browser.Click(Find.Button("register-submit"));
    }

    public string ErrorMessage() => browser.TextOf(Find.TestId("register-error"));
}
