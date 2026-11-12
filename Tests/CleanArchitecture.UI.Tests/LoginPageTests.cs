using System.Net;
using Bunit;
using CleanArchitecture.Presentation.UI.Pages;

namespace CleanArchitecture.UI.Tests;

[Category("Unit")]
public class LoginPageTests
{
    private static void Fill(IRenderedComponent<Login> cut, string email = "a@example.test", string password = "pw")
    {
        cut.Find("#email").Change(email);
        cut.Find("#password").Change(password);
    }

    [Test]
    public async Task Login_WithValidCredentials_OpensMeasurements()
    {
        using var host = new UiHost();
        var cut = host.Context.Render<Login>();
        Fill(cut);

        cut.Find("#login-button").Click();

        cut.WaitForAssertion(() => { });
        await Assert.That(host.Session.IsAuthenticated).IsTrue();
        await Assert.That(host.CurrentPath).IsEqualTo("/measurements");
    }

    [Test]
    public async Task Login_WithWrongPassword_ShowsLocalizedAlert_AndDoesNotNavigate()
    {
        using var host = new UiHost();
        host.Auth.OnLogin = _ => throw ApiExceptions.Create(HttpStatusCode.Unauthorized);
        var cut = host.Context.Render<Login>();
        Fill(cut, password: "wrong");

        cut.Find("#login-button").Click();

        cut.WaitForAssertion(() => cut.Find("[role=alert]"));
        await Assert.That(cut.Find("[role=alert]").TextContent).Contains("E-Mail oder Passwort ist falsch");
        await Assert.That(host.CurrentPath).IsNotEqualTo("/measurements");
        await Assert.That(host.Session.IsAuthenticated).IsFalse();
    }

    [Test]
    public async Task Register_WithWeakPassword_ShowsTheApiErrors()
    {
        using var host = new UiHost();
        host.Auth.OnRegister = _ => throw ApiExceptions.Validation("PasswordTooShort", "Das Passwort ist zu kurz.");
        var cut = host.Context.Render<Login>();
        Fill(cut, password: "x");

        cut.Find("#register-button").Click();

        cut.WaitForAssertion(() => cut.Find("[role=alert]"));
        await Assert.That(cut.Find("[role=alert]").TextContent).Contains("Das Passwort ist zu kurz.");
        await Assert.That(host.Session.IsAuthenticated).IsFalse();
    }

    [Test]
    public async Task UnexpectedError_ShowsGenericMessage_WithoutDetails()
    {
        using var host = new UiHost();
        host.Auth.OnLogin = _ => throw new InvalidOperationException("Connection string: secret");
        var cut = host.Context.Render<Login>();
        Fill(cut);

        cut.Find("#login-button").Click();

        cut.WaitForAssertion(() => cut.Find("[role=alert]"));
        var text = cut.Find("[role=alert]").TextContent;
        await Assert.That(text).Contains("Etwas ist schiefgegangen");
        await Assert.That(text).DoesNotContain("secret");
    }

    [Test]
    public async Task EnglishCulture_RendersEnglishTexts()
    {
        using var host = new UiHost("en");

        var cut = host.Context.Render<Login>();

        await Assert.That(cut.Find("h1").TextContent).IsEqualTo("Sign in");
        await Assert.That(cut.Find("label[for=email]").TextContent).IsEqualTo("Email");
        await Assert.That(cut.Find("#login-button").TextContent).IsEqualTo("Sign in");
    }

    [Test]
    public async Task GermanCulture_RendersGermanTexts()
    {
        using var host = new UiHost("de");

        var cut = host.Context.Render<Login>();

        await Assert.That(cut.Find("h1").TextContent).IsEqualTo("Anmelden");
        await Assert.That(cut.Find("label[for=email]").TextContent).IsEqualTo("E-Mail");
    }

    [Test]
    public async Task EveryInput_HasALabelBoundByFor()
    {
        using var host = new UiHost();

        var cut = host.Context.Render<Login>();

        foreach (var input in cut.FindAll("input"))
        {
            var id = input.GetAttribute("id");
            await Assert.That(id).IsNotNull();
            await Assert.That(cut.FindAll($"label[for={id}]").Count).IsEqualTo(1);
        }
    }

    [Test]
    public async Task Buttons_AreDisabledWhileSigningIn()
    {
        using var host = new UiHost();
        var gate = new TaskCompletionSource<CleanArchitecture.Contracts.TokenResponse>();
        host.Auth.OnLogin = _ => gate.Task;
        var cut = host.Context.Render<Login>();
        Fill(cut);

        cut.Find("#login-button").Click();

        await Assert.That(cut.Find("#login-button").HasAttribute("disabled")).IsTrue();
        await Assert.That(cut.Find("#register-button").HasAttribute("disabled")).IsTrue();
        gate.SetResult(FakeAuthApi.Tokens("a", "r"));
    }

    [Test]
    public async Task Register_Success_ShowsConfirmationNotice_WithoutSigningIn()
    {
        using var host = new UiHost();
        var cut = host.Context.Render<Login>();
        Fill(cut, email: "neu@example.test");

        cut.Find("#register-button").Click();

        cut.WaitForAssertion(() => cut.Find("[role=status]"));
        var status = cut.Find("[role=status]");
        await Assert.That(status.TextContent).Contains("Wir haben dir eine E-Mail an neu@example.test geschickt");
        await Assert.That(status.TextContent).Contains("Bestätige sie, bevor du dich anmeldest.");
        await Assert.That(cut.FindAll("[role=alert]").Count).IsEqualTo(0);
        await Assert.That(cut.Find("#resend-button").TextContent).IsEqualTo("Bestätigungsmail erneut senden");
        await Assert.That(host.Session.IsAuthenticated).IsFalse();
        await Assert.That(host.Auth.Calls).IsEquivalentTo(new[] { "register" });
        await Assert.That(host.CurrentPath).IsNotEqualTo("/measurements");
    }

    [Test]
    public async Task Login_NotAllowed_ShowsConfirmationNotice_InsteadOfWrongCredentials()
    {
        using var host = new UiHost();
        host.Auth.OnLogin = _ => throw ApiExceptions.LoginRejected("NotAllowed");
        var cut = host.Context.Render<Login>();
        Fill(cut, email: "offen@example.test");

        cut.Find("#login-button").Click();

        cut.WaitForAssertion(() => cut.Find("[role=status]"));
        await Assert.That(cut.Find("[role=status]").TextContent).Contains("offen@example.test");
        await Assert.That(cut.FindAll("[role=alert]").Count).IsEqualTo(0);
        await Assert.That(cut.Markup).DoesNotContain("E-Mail oder Passwort ist falsch");
        await Assert.That(cut.FindAll("#resend-button").Count).IsEqualTo(1);
    }

    [Test]
    public async Task Login_Failed_ShowsWrongCredentialsAlert_WithoutResend()
    {
        using var host = new UiHost();
        host.Auth.OnLogin = _ => throw ApiExceptions.LoginRejected("Failed");
        var cut = host.Context.Render<Login>();
        Fill(cut);

        cut.Find("#login-button").Click();

        cut.WaitForAssertion(() => cut.Find("[role=alert]"));
        await Assert.That(cut.Find("[role=alert]").TextContent).Contains("E-Mail oder Passwort ist falsch");
        await Assert.That(cut.FindAll("#resend-button").Count).IsEqualTo(0);
        await Assert.That(cut.FindAll("[role=status]").Count).IsEqualTo(0);
    }

    [Test]
    public async Task Resend_CallsTheApi_AndShowsTheNeutralAnswer()
    {
        using var host = new UiHost();
        var cut = host.Context.Render<Login>();
        Fill(cut, email: "neu@example.test");
        cut.Find("#register-button").Click();
        cut.WaitForAssertion(() => cut.Find("#resend-button"));

        cut.Find("#resend-button").Click();

        cut.WaitForAssertion(() => _ = cut.Find("[role=status]").TextContent.Contains("Falls die Adresse bekannt ist, haben wir die Mail erneut gesendet.") ? 0 : throw new InvalidOperationException("Hinweis fehlt"));
        await Assert.That(host.Auth.Resent).IsEquivalentTo(new[] { "neu@example.test" });
    }

    [Test]
    public async Task Resend_IsDisabledWhileSending()
    {
        using var host = new UiHost();
        var gate = new TaskCompletionSource();
        host.Auth.OnResend = _ => gate.Task;
        var cut = host.Context.Render<Login>();
        Fill(cut);
        cut.Find("#register-button").Click();
        cut.WaitForAssertion(() => cut.Find("#resend-button"));

        cut.Find("#resend-button").Click();

        await Assert.That(cut.Find("#resend-button").HasAttribute("disabled")).IsTrue();
        gate.SetResult();
        cut.WaitForAssertion(() => _ = !cut.Find("#resend-button").HasAttribute("disabled") ? 0 : throw new InvalidOperationException("noch deaktiviert"));
    }

    [Test]
    public async Task Resend_Failure_ShowsGenericMessage_WithoutDetails()
    {
        using var host = new UiHost();
        host.Auth.OnResend = _ => throw new HttpRequestException("secret host");
        var cut = host.Context.Render<Login>();
        Fill(cut);
        cut.Find("#register-button").Click();
        cut.WaitForAssertion(() => cut.Find("#resend-button"));

        cut.Find("#resend-button").Click();

        cut.WaitForAssertion(() => cut.Find("[role=alert]"));
        await Assert.That(cut.Find("[role=alert]").TextContent).Contains("Etwas ist schiefgegangen");
        await Assert.That(cut.Markup).DoesNotContain("secret");
    }

    [Test]
    public async Task EnglishCulture_RendersEnglishConfirmationNotice()
    {
        using var host = new UiHost("en");
        var cut = host.Context.Render<Login>();
        Fill(cut, email: "neu@example.test");

        cut.Find("#register-button").Click();

        cut.WaitForAssertion(() => cut.Find("[role=status]"));
        await Assert.That(cut.Find("[role=status]").TextContent).Contains("neu@example.test");
        await Assert.That(cut.Find("#resend-button").TextContent).IsEqualTo("Resend confirmation email");
    }
}
