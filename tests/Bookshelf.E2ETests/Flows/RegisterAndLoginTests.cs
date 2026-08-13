using Bookshelf.E2ETests.Support;

namespace Bookshelf.E2ETests.Flows;

/// <summary>
/// Flow 1 — the way in. Everything else in the suite depends on this working, which is why it is
/// tested on its own rather than assumed by the tests that need an account.
/// </summary>
[TestFixture]
[Category("E2E")]
public class RegisterAndLoginTests : E2ETestBase
{
    [Test]
    public void Registering_signs_the_reader_in_straight_away()
    {
        var user = TestUser.New("signup");

        Register.Open();
        Register.Submit(user);

        // No login step: registration returns a token and signs the reader in (F1), so the header
        // must already be showing their name.
        Nav.WaitUntilSignedIn(user.DisplayName);

        // And the proof that the token is real rather than cosmetic: a guarded route opens.
        Nav.GoToShelf();
        Assert.That(Browser.CurrentUrl, Does.Contain("/shelf"));
    }

    [Test]
    public void A_registered_reader_can_sign_out_and_back_in()
    {
        var user = RegisterNewReader("returning");

        Nav.SignOut();

        Login.Open();
        Login.Submit(user);

        Nav.WaitUntilSignedIn(user.DisplayName);
    }

    [Test]
    public void The_wrong_password_is_refused_with_a_message()
    {
        var user = RegisterNewReader("wrongpass");
        Nav.SignOut();

        Login.Open();
        Login.Submit(user.Email, "Wrong!Password9");

        Assert.Multiple(() =>
        {
            Assert.That(Login.ErrorMessage(), Is.Not.Empty);
            Assert.That(Browser.CurrentUrl, Does.Contain("/login"));
        });
    }

    /// <summary>
    /// The guard from F1, seen from the outside: no token, no shelf. The reader is sent to the
    /// login page with a <c>returnUrl</c> so they land back where they were aiming.
    /// </summary>
    [Test]
    public void A_guest_asking_for_the_shelf_is_sent_to_the_login_page()
    {
        Browser.ClearSession();
        Browser.Go("/shelf");

        Login.WaitUntilShown();

        Assert.Multiple(() =>
        {
            Assert.That(Browser.CurrentUrl, Does.Contain("/login"));
            Assert.That(
                Browser.CurrentUrl,
                Does.Contain("returnUrl"),
                "The guard should remember where the guest was going.");
        });
    }
}
