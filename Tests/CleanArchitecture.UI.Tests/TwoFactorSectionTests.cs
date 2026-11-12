using System.Net;
using Bunit;
using CleanArchitecture.Presentation.UI.Pages;
using CleanArchitecture.Presentation.UI.Services;

namespace CleanArchitecture.UI.Tests;

/// <summary>Abschnitt „Zwei-Faktor-Anmeldung“ im Profil (ADR 0006, Phase 2).</summary>
[Category("Unit")]
public class TwoFactorSectionTests
{
    private static async Task<(UiHost Host, IRenderedComponent<Profile> Cut)> OpenAsync(bool enabled = false, int codesLeft = 0, string culture = "de")
    {
        var host = new UiHost(culture);
        host.TwoFactor.Enabled = enabled;
        host.TwoFactor.CodesLeft = codesLeft;
        await host.SignInAsync();
        var cut = host.Context.Render<Profile>();
        cut.WaitForState(() => cut.FindAll("#twofactor-setup, #twofactor-status").Count > 0);
        return (host, cut);
    }

    private static async Task<IRenderedComponent<Profile>> EnableAsync(UiHost host, IRenderedComponent<Profile> cut)
    {
        cut.Find("#twofactor-setup").Click();
        cut.WaitForState(() => cut.FindAll("#twofactor-key").Count == 1);
        cut.Find("#twofactor-code").Change(FakeTwoFactorBackend.ValidCode);
        cut.Find("#twofactor-form").Submit();
        cut.WaitForState(() => cut.FindAll("#twofactor-recovery").Count == 1);
        await Task.CompletedTask;
        return cut;
    }

    [Test]
    public async Task Disabled_ShowsHeading_AndSetupButton_WithoutKey()
    {
        var (host, cut) = await OpenAsync();
        using var _ = host;

        await Assert.That(cut.Find("#twofactor h2").TextContent).IsEqualTo("Zwei-Faktor-Anmeldung");
        await Assert.That(cut.Find("#twofactor-setup").TextContent).IsEqualTo("Einrichten");
        await Assert.That(cut.FindAll("#twofactor-key").Count).IsEqualTo(0);
        await Assert.That(cut.Markup).DoesNotContain(FakeTwoFactorBackend.Key);
    }

    [Test]
    public async Task Setup_ShowsGroupedKey_InlineSvgQr_AndFocusesTheCodeField()
    {
        var (host, cut) = await OpenAsync();
        using var _ = host;

        cut.Find("#twofactor-setup").Click();

        cut.WaitForState(() => cut.FindAll("#twofactor-key").Count == 1);
        await Assert.That(cut.Find("#twofactor-key").TextContent).IsEqualTo("JBSW Y3DP EHPK 3PXP JBSW Y3DP EHPK 3PXP");
        var qr = cut.Find("#twofactor-qr");
        await Assert.That(qr.InnerHtml).Contains("<svg");
        await Assert.That(qr.InnerHtml).DoesNotContain("style=");
        await Assert.That(qr.InnerHtml).DoesNotContain("<script");
        await Assert.That(qr.GetAttribute("aria-hidden")).IsEqualTo("true");
        var input = cut.Find("#twofactor-code");
        await Assert.That(input.GetAttribute("inputmode")).IsEqualTo("numeric");
        await Assert.That(input.GetAttribute("autocomplete")).IsEqualTo("one-time-code");
        await Assert.That(cut.Find("label[for=twofactor-code]").TextContent).IsEqualTo("Code aus der Authenticator-App");
        host.Context.JSInterop.VerifyFocusAsyncInvoke();
    }

    [Test]
    public async Task Confirm_WithValidCode_EnablesAndShowsRecoveryCodesOnce_WithHints()
    {
        var (host, cut) = await OpenAsync();
        using var _ = host;

        await EnableAsync(host, cut);

        await Assert.That(host.TwoFactor.Requests.Last()).IsEqualTo(new CleanArchitecture.Contracts.TwoFactorRequest(Enable: true, TwoFactorCode: "123456"));
        var items = cut.FindAll("#twofactor-recovery li");
        await Assert.That(items.Count).IsEqualTo(10);
        await Assert.That(items[0].TextContent).IsEqualTo("code1-01");
        await Assert.That(cut.Find("#twofactor-recovery-hint").TextContent).Contains("Speichere sie jetzt");
        await Assert.That(cut.Find("#twofactor-relogin").TextContent).Contains("musst du dich nach Ablauf der aktuellen Sitzung neu anmelden");
        await Assert.That(cut.FindAll("#twofactor-key").Count).IsEqualTo(0);
        await Assert.That(cut.Find("#twofactor [role=status]").TextContent).Contains("eingeschaltet");
    }

    [Test]
    public async Task Done_HidesTheCodes_AndShowsEnabledStatus()
    {
        var (host, cut) = await OpenAsync();
        using var _ = host;
        await EnableAsync(host, cut);

        cut.Find("#twofactor-codes-done").Click();

        await Assert.That(cut.FindAll("#twofactor-recovery").Count).IsEqualTo(0);
        await Assert.That(cut.Markup).DoesNotContain("code1-01");
        await Assert.That(cut.Find("#twofactor-status").TextContent).Contains("10");
    }

    [Test]
    [Arguments("12345")]
    [Arguments("abcdef")]
    [Arguments("")]
    public async Task Confirm_WithMalformedCode_ShowsFieldError_WithoutRequest(string code)
    {
        var (host, cut) = await OpenAsync();
        using var _ = host;
        cut.Find("#twofactor-setup").Click();
        cut.WaitForState(() => cut.FindAll("#twofactor-code").Count == 1);
        var before = host.TwoFactor.Requests.Count;

        cut.Find("#twofactor-code").Change(code);
        cut.Find("#twofactor-form").Submit();

        await Assert.That(cut.Find("#twofactor-error[role=alert]").TextContent).Contains("6-stellig");
        await Assert.That(host.TwoFactor.Requests.Count).IsEqualTo(before);
    }

    [Test]
    public async Task Confirm_WithWrongCode_ShowsErrorAtTheField_AndFocusesIt()
    {
        var (host, cut) = await OpenAsync();
        using var _ = host;
        cut.Find("#twofactor-setup").Click();
        cut.WaitForState(() => cut.FindAll("#twofactor-code").Count == 1);

        cut.Find("#twofactor-code").Change("999999");
        cut.Find("#twofactor-form").Submit();

        cut.WaitForState(() => cut.FindAll("#twofactor-error").Count == 1);
        await Assert.That(cut.Find("#twofactor-error").GetAttribute("role")).IsEqualTo("alert");
        await Assert.That(cut.Find("#twofactor-error").TextContent).Contains("Der Code ist ungültig");
        await Assert.That(cut.Find("#twofactor-code").GetAttribute("aria-invalid")).IsEqualTo("true");
        await Assert.That(cut.Find("#twofactor-code").GetAttribute("aria-describedby")).Contains("twofactor-error");
        await Assert.That(cut.FindAll("#twofactor-recovery").Count).IsEqualTo(0);
        host.Context.JSInterop.VerifyInvoke("Blazor._internal.domWrapper.focus", 2); // beim Öffnen und beim Fehler
    }

    [Test]
    public async Task Cancel_DropsTheKey()
    {
        var (host, cut) = await OpenAsync();
        using var _ = host;
        cut.Find("#twofactor-setup").Click();
        cut.WaitForState(() => cut.FindAll("#twofactor-key").Count == 1);

        cut.Find("#twofactor-cancel").Click();

        await Assert.That(cut.FindAll("#twofactor-key").Count).IsEqualTo(0);
        await Assert.That(cut.Markup).DoesNotContain("JBSW");
        await Assert.That(cut.FindAll("#twofactor-setup").Count).IsEqualTo(1);
    }

    [Test]
    public async Task Enabled_ShowsStatusWithRemainingCodes()
    {
        var (host, cut) = await OpenAsync(enabled: true, codesLeft: 7);
        using var _ = host;

        await Assert.That(cut.Find("#twofactor-status").TextContent).Contains("7");
        await Assert.That(cut.FindAll("#twofactor-setup").Count).IsEqualTo(0);
        await Assert.That(cut.FindAll("#twofactor-reset").Count).IsEqualTo(1);
        await Assert.That(cut.FindAll("#twofactor-disable").Count).IsEqualTo(1);
        await Assert.That(cut.Markup).DoesNotContain(FakeTwoFactorBackend.Key);
    }

    [Test]
    public async Task Reset_AsksForTheCurrentCode_ThenShowsNewCodesOnce()
    {
        var (host, cut) = await OpenAsync(enabled: true, codesLeft: 2);
        using var _ = host;

        cut.Find("#twofactor-reset").Click();
        var input = cut.Find("#twofactor-action-code");
        await Assert.That(input.GetAttribute("inputmode")).IsEqualTo("numeric");
        await Assert.That(input.GetAttribute("autocomplete")).IsEqualTo("one-time-code");
        await Assert.That(cut.Find("label[for=twofactor-action-code]").TextContent).IsEqualTo("Code aus der Authenticator-App");
        host.Context.JSInterop.VerifyFocusAsyncInvoke();
        var before = host.TwoFactor.Requests.Count;
        await Assert.That(before).IsEqualTo(1); // nur die Statusabfrage, noch nichts erzeugt

        input.Change("123456");
        cut.Find("#twofactor-action-form").Submit();

        cut.WaitForState(() => cut.FindAll("#twofactor-recovery").Count == 1);
        await Assert.That(host.TwoFactor.Requests.Last()).IsEqualTo(new CleanArchitecture.Contracts.TwoFactorRequest(ResetRecoveryCodes: true, TwoFactorCode: "123456"));
        await Assert.That(cut.FindAll("#twofactor-recovery li").Count).IsEqualTo(10);
    }

    [Test]
    public async Task Reset_CanBeCancelled_WithoutRequest()
    {
        var (host, cut) = await OpenAsync(enabled: true, codesLeft: 2);
        using var _ = host;

        cut.Find("#twofactor-reset").Click();
        cut.Find("#twofactor-action-cancel").Click();

        await Assert.That(host.TwoFactor.Requests.Count).IsEqualTo(1);
        await Assert.That(cut.FindAll("#twofactor-action-form").Count).IsEqualTo(0);
    }

    [Test]
    [Arguments("reset")]
    [Arguments("disable")]
    public async Task Action_WithWrongCode_ShowsErrorAtTheField_AndFocusesIt(string action)
    {
        var (host, cut) = await OpenAsync(enabled: true, codesLeft: 2);
        using var _ = host;
        cut.Find($"#twofactor-{action}").Click();

        cut.Find("#twofactor-action-code").Change("000000");
        cut.Find("#twofactor-action-form").Submit();

        cut.WaitForState(() => cut.FindAll("#twofactor-action-error").Count == 1);
        await Assert.That(cut.Find("#twofactor-action-error").GetAttribute("role")).IsEqualTo("alert");
        await Assert.That(cut.Find("#twofactor-action-error").TextContent).Contains("Der Code ist ungültig");
        await Assert.That(cut.Find("#twofactor-action-code").GetAttribute("aria-invalid")).IsEqualTo("true");
        await Assert.That(cut.FindAll("#twofactor-recovery").Count).IsEqualTo(0);
        await Assert.That(host.TwoFactor.Enabled).IsTrue();
        host.Context.JSInterop.VerifyInvoke("Blazor._internal.domWrapper.focus", 2); // beim Öffnen und beim Fehler
    }

    [Test]
    public async Task Disable_AsksForTheCurrentCode_ThenTurnsOff_WithReloginHint()
    {
        var (host, cut) = await OpenAsync(enabled: true, codesLeft: 10);
        using var _ = host;

        cut.Find("#twofactor-disable").Click();
        await Assert.That(host.TwoFactor.Enabled).IsTrue();
        cut.Find("#twofactor-action-code").Change("123456");
        cut.Find("#twofactor-action-form").Submit();

        cut.WaitForState(() => cut.FindAll("#twofactor-setup").Count == 1);
        await Assert.That(host.TwoFactor.Requests.Last()).IsEqualTo(new CleanArchitecture.Contracts.TwoFactorRequest(Enable: false, TwoFactorCode: "123456"));
        await Assert.That(host.TwoFactor.Enabled).IsFalse();
        var status = cut.Find("#twofactor [role=status]").TextContent;
        await Assert.That(status).Contains("ausgeschaltet");
        await Assert.That(status).Contains("neu anmelden");
    }

    [Test]
    public async Task LoadFailure_ShowsGenericMessage_WithoutDetails()
    {
        using var host = new UiHost();
        host.TwoFactor.FailWith = new HttpRequestException($"secret {FakeTwoFactorBackend.Key}");
        await host.SignInAsync();

        var cut = host.Context.Render<Profile>();

        cut.WaitForState(() => cut.FindAll("#twofactor-error").Count == 1);
        await Assert.That(cut.Find("#twofactor-error").TextContent).Contains("Etwas ist schiefgegangen");
        await Assert.That(cut.Markup).DoesNotContain("secret");
        await Assert.That(cut.Markup).DoesNotContain(FakeTwoFactorBackend.Key);
    }

    [Test]
    public async Task Unauthorized_SignsOutToLogin()
    {
        using var host = new UiHost();
        host.TwoFactor.FailWith = ApiExceptions.Create(HttpStatusCode.Unauthorized);
        await host.SignInAsync();

        host.Context.Render<Profile>();

        await Task.Delay(100);
        await Assert.That(host.Session.IsAuthenticated).IsFalse();
        await Assert.That(host.CurrentPath).IsEqualTo("/login");
    }

    [Test]
    public async Task English_RendersEnglishTexts()
    {
        var (host, cut) = await OpenAsync(culture: "en");
        using var _ = host;

        await Assert.That(cut.Find("#twofactor h2").TextContent).IsEqualTo("Two-factor sign-in");
        await Assert.That(cut.Find("#twofactor-setup").TextContent).IsEqualTo("Set up");
    }

    [Test]
    public async Task OtpAuthUri_UsesIssuerAndEmail_UrlEncoded()
    {
        var uri = TotpSetup.BuildUri("Blutdruck-Tagebuch", "a+b@example.test", "ABCD EFGH");

        await Assert.That(uri).IsEqualTo("otpauth://totp/Blutdruck-Tagebuch:a%2Bb%40example.test?secret=ABCDEFGH&issuer=Blutdruck-Tagebuch");
        await Assert.That(TotpSetup.BuildUri("Blutdruck-Tagebuch", null, "ABCD")).IsEqualTo("otpauth://totp/Blutdruck-Tagebuch?secret=ABCD&issuer=Blutdruck-Tagebuch");
        await Assert.That(TotpSetup.FormatKey("abcdefghij")).IsEqualTo("ABCD EFGH IJ");
    }
}
