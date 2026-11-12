using Bunit;
using CleanArchitecture.Contracts;
using CleanArchitecture.Presentation.UI.Pages;

namespace CleanArchitecture.UI.Tests;

/// <summary>Zweiter Anmeldeschritt bei eingeschalteter Zwei-Faktor-Anmeldung (ADR 0006, Phase 2).</summary>
[Category("Unit")]
public class LoginTwoFactorTests
{
    private static IRenderedComponent<Login> StartLogin(UiHost host, string email = "a@example.test", string password = "pw")
    {
        // Ohne Code antwortet die API mit 401 "RequiresTwoFactor"; mit Code oder Wiederherstellungscode gibt es Tokens.
        host.Auth.OnLogin = request => request is { TwoFactorCode: null, TwoFactorRecoveryCode: null }
            ? throw ApiExceptions.RequiresTwoFactor()
            : Task.FromResult(FakeAuthApi.Tokens("access-1", "refresh-1"));
        var cut = host.Context.Render<Login>();
        cut.Find("#email").Change(email);
        cut.Find("#password").Change(password);
        cut.Find("#login-button").Click();
        cut.WaitForAssertion(() => cut.Find("#two-factor-code"));
        return cut;
    }

    [Test]
    public async Task RequiresTwoFactor_ShowsCodeStep_WithoutAlert_AndFocusesTheField()
    {
        using var host = new UiHost();

        var cut = StartLogin(host);

        var input = cut.Find("#two-factor-code");
        await Assert.That(input.GetAttribute("inputmode")).IsEqualTo("numeric");
        await Assert.That(input.GetAttribute("autocomplete")).IsEqualTo("one-time-code");
        await Assert.That(cut.Find("label[for=two-factor-code]").TextContent).IsEqualTo("Code aus der Authenticator-App");
        await Assert.That(cut.FindAll("[role=alert]").Count).IsEqualTo(0);
        await Assert.That(cut.FindAll("#password").Count).IsEqualTo(0);
        await Assert.That(host.Session.IsAuthenticated).IsFalse();
        host.Context.JSInterop.VerifyFocusAsyncInvoke();
    }

    [Test]
    public async Task Submitting_TheCode_SendsItWithTheRememberedCredentials_AndSignsIn()
    {
        using var host = new UiHost();
        var cut = StartLogin(host, "x@example.test", "geheim");

        cut.Find("#two-factor-code").Change("123 456");
        cut.Find("#login-form").Submit();

        cut.WaitForAssertion(() => _ = host.CurrentPath.EndsWith("/measurements") ? 0 : throw new InvalidOperationException("keine Weiterleitung"));
        await Assert.That(host.Auth.LoginRequests[1]).IsEqualTo(new LoginRequest("x@example.test", "geheim", "123456"));
        await Assert.That(host.Session.IsAuthenticated).IsTrue();
        await Assert.That(host.Storage.Items.Values).DoesNotContain("geheim");
    }

    [Test]
    public async Task Toggle_SwitchesToRecoveryCode_AndSendsIt()
    {
        using var host = new UiHost();
        var cut = StartLogin(host);

        await Assert.That(cut.Find("#two-factor-toggle").TextContent).IsEqualTo("Wiederherstellungscode verwenden");
        cut.Find("#two-factor-toggle").Click();

        var input = cut.Find("#recovery-code");
        await Assert.That(cut.Find("label[for=recovery-code]").TextContent).IsEqualTo("Wiederherstellungscode");
        await Assert.That(input.GetAttribute("inputmode")).IsNull();
        await Assert.That(cut.FindAll("#two-factor-code").Count).IsEqualTo(0);
        await Assert.That(cut.Find("#two-factor-toggle").TextContent).IsEqualTo("Code aus der Authenticator-App verwenden");

        input.Change("abcde-fghij");
        cut.Find("#login-form").Submit();

        cut.WaitForAssertion(() => _ = host.Session.IsAuthenticated ? 0 : throw new InvalidOperationException("nicht angemeldet"));
        await Assert.That(host.Auth.LoginRequests[1].TwoFactorRecoveryCode).IsEqualTo("abcde-fghij");
        await Assert.That(host.Auth.LoginRequests[1].TwoFactorCode).IsNull();
    }

    [Test]
    public async Task WrongCode_ShowsOwnMessage_NotWrongCredentials_AndStaysInStepTwo()
    {
        using var host = new UiHost();
        var cut = StartLogin(host);
        host.Auth.OnLogin = _ => throw ApiExceptions.LoginRejected("Failed");

        cut.Find("#two-factor-code").Change("000000");
        cut.Find("#login-form").Submit();

        cut.WaitForAssertion(() => cut.Find("[role=alert]"));
        var text = cut.Find("[role=alert]").TextContent;
        await Assert.That(text).Contains("Der Code ist ungültig");
        await Assert.That(text).DoesNotContain("E-Mail oder Passwort");
        await Assert.That(cut.FindAll("#two-factor-code").Count).IsEqualTo(1);
        await Assert.That(host.Session.IsAuthenticated).IsFalse();
    }

    [Test]
    public async Task LockedOut_ShowsOwnMessage_AndDiscardsThePassword()
    {
        using var host = new UiHost();
        var cut = StartLogin(host);
        host.Auth.OnLogin = _ => throw ApiExceptions.LoginRejected("LockedOut");

        cut.Find("#two-factor-code").Change("000000");
        cut.Find("#login-form").Submit();

        cut.WaitForAssertion(() => cut.Find("[role=alert]"));
        await Assert.That(cut.Find("[role=alert]").TextContent).Contains("vorübergehend gesperrt");
        await Assert.That(cut.Find("#password").GetAttribute("value") ?? "").IsEqualTo("");
    }

    [Test]
    public async Task EmptyCode_ShowsHint_WithoutRequest()
    {
        using var host = new UiHost();
        var cut = StartLogin(host);

        cut.Find("#login-form").Submit();

        await Assert.That(cut.Find("[role=alert]").TextContent).Contains("Bitte gib den Code ein");
        await Assert.That(host.Auth.LoginRequests.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Back_DiscardsThePassword_AndShowsTheCredentialsAgain()
    {
        using var host = new UiHost();
        var cut = StartLogin(host, "keep@example.test", "geheim");

        cut.Find("#two-factor-back").Click();

        await Assert.That(cut.Find("#email").GetAttribute("value")).IsEqualTo("keep@example.test");
        await Assert.That(cut.Find("#password").GetAttribute("value") ?? "").IsEqualTo("");
        await Assert.That(cut.FindAll("#two-factor-code").Count).IsEqualTo(0);
        await Assert.That(cut.Markup).DoesNotContain("geheim");
    }

    [Test]
    public async Task StepTwo_ButtonsAreDisabledWhileSending()
    {
        using var host = new UiHost();
        var cut = StartLogin(host);
        var gate = new TaskCompletionSource<TokenResponse>();
        host.Auth.OnLogin = _ => gate.Task;

        cut.Find("#two-factor-code").Change("123456");
        cut.Find("#login-form").Submit();

        await Assert.That(cut.Find("#login-button").HasAttribute("disabled")).IsTrue();
        await Assert.That(cut.Find("#two-factor-toggle").HasAttribute("disabled")).IsTrue();
        await Assert.That(cut.Find("#two-factor-back").HasAttribute("disabled")).IsTrue();
        gate.SetResult(FakeAuthApi.Tokens("a", "r"));
    }

    [Test]
    public async Task StepTwo_EveryInputHasALabel_AndTheEnglishTextsExist()
    {
        using var host = new UiHost("en");
        var cut = StartLogin(host);

        foreach (var input in cut.FindAll("input"))
        {
            await Assert.That(cut.FindAll($"label[for={input.GetAttribute("id")}]").Count).IsEqualTo(1);
        }

        await Assert.That(cut.Find("label[for=two-factor-code]").TextContent).IsEqualTo("Code from the authenticator app");
        await Assert.That(cut.Find("#two-factor-toggle").TextContent).IsEqualTo("Use a recovery code");
        await Assert.That(cut.Find("#two-factor-back").TextContent).IsEqualTo("Back");
    }
}
