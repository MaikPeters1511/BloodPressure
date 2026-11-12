using System.Net;
using CleanArchitecture.Contracts;
using CleanArchitecture.Presentation.Maui.Core.Services;
using CleanArchitecture.Presentation.Maui.Core.ViewModels;

namespace CleanArchitecture.Maui.Tests;

/// <summary>Zwei-Faktor-Verwaltung im Profil (ADR 0006, Phase 2).</summary>
[Category("Unit")]
public class TwoFactorViewModelTests
{
    private readonly FakeSecureStore _store = new();
    private readonly FakeAuthApi _auth = new();
    private readonly FakeReminderApi _api = new();
    private readonly FakeNavigator _nav = new();
    private readonly FakeDialogs _dialogs = new();
    private readonly FakeLauncher _launcher = new();
    private SessionService _session = null!;
    private int _focusRequests;

    private FakeTwoFactorBackend Backend => _api.TwoFactor;

    private async Task<TwoFactorViewModel> CreateAsync(bool enabled = false, int codesLeft = 0, string culture = "de", string? email = "a@example.test")
    {
        Backend.Enabled = enabled;
        Backend.CodesLeft = codesLeft;
        _session = new SessionService(_store, _auth, Clock.Fixed());
        if (email is not null)
        {
            await _session.LoginAsync(email, "pw");
        }

        var vm = new TwoFactorViewModel(_api, _session, _nav, _dialogs, new ResxLocalizer(culture == "de" ? Culture.De : Culture.En), _launcher);
        vm.FocusCodeRequested += () => _focusRequests++;
        await vm.InitializeCommand.ExecuteAsync(null);
        return vm;
    }

    private async Task<TwoFactorViewModel> EnableAsync()
    {
        var vm = await CreateAsync();
        await vm.StartSetupCommand.ExecuteAsync(null);
        vm.CodeText = FakeTwoFactorBackend.ValidCode;
        await vm.ConfirmSetupCommand.ExecuteAsync(null);
        return vm;
    }

    [Test]
    public async Task Initialize_Disabled_OffersSetup_WithoutKey()
    {
        var vm = await CreateAsync();

        await Assert.That(vm.ShowOff).IsTrue();
        await Assert.That(vm.ShowOn).IsFalse();
        await Assert.That(vm.SetupKey).IsNull();
        await Assert.That(vm.Title).IsEqualTo("Zwei-Faktor-Anmeldung");
    }

    [Test]
    public async Task Initialize_Enabled_ShowsStatusWithRemainingCodes()
    {
        var vm = await CreateAsync(enabled: true, codesLeft: 7);

        await Assert.That(vm.ShowOn).IsTrue();
        await Assert.That(vm.StatusText).Contains("7");
        await Assert.That(vm.SetupKey).IsNull();
    }

    [Test]
    public async Task Initialize_NotSignedIn_GoesToLogin()
    {
        var vm = await CreateAsync(email: null);

        await Assert.That(_nav.Visited).IsEquivalentTo(new[] { "login" });
        await Assert.That(Backend.Requests).IsEmpty();
        await Assert.That(vm.ShowOff).IsFalse();
    }

    [Test]
    public async Task Initialize_Unauthorized_SignsOut()
    {
        Backend.FailWith = ApiExceptions.Create(HttpStatusCode.Unauthorized);

        await CreateAsync();

        await Assert.That(_nav.Visited).IsEquivalentTo(new[] { "login" });
        await Assert.That(_session.IsAuthenticated).IsFalse();
    }

    [Test]
    public async Task Initialize_Failure_ShowsGenericMessage_WithoutDetails()
    {
        Backend.FailWith = new HttpRequestException($"secret {FakeTwoFactorBackend.Key}");

        var vm = await CreateAsync();

        await Assert.That(vm.ErrorMessage).IsEqualTo("Etwas ist schiefgegangen. Bitte versuche es später erneut.");
        await Assert.That(vm.ErrorMessage).DoesNotContain("secret");
        await Assert.That(_dialogs.Announcements.Any(a => a.Contains(FakeTwoFactorBackend.Key))).IsFalse();
    }

    [Test]
    public async Task StartSetup_ShowsGroupedKey_AndFocusesTheCodeField()
    {
        var vm = await CreateAsync();

        await vm.StartSetupCommand.ExecuteAsync(null);

        await Assert.That(vm.ShowSetup).IsTrue();
        await Assert.That(vm.SetupKey).IsEqualTo("JBSW Y3DP EHPK 3PXP JBSW Y3DP EHPK 3PXP");
        await Assert.That(_focusRequests).IsEqualTo(1);
    }

    [Test]
    public async Task OpenAuthenticator_LaunchesTheOtpAuthUri_WithIssuerAndEmail()
    {
        var vm = await CreateAsync();
        await vm.StartSetupCommand.ExecuteAsync(null);

        await vm.OpenAuthenticatorCommand.ExecuteAsync(null);

        await Assert.That(_launcher.Opened.Single().ToString()).IsEqualTo(
            "otpauth://totp/Blutdruck-Tagebuch:a%40example.test?secret=JBSWY3DPEHPK3PXPJBSWY3DPEHPK3PXP&issuer=Blutdruck-Tagebuch");
    }

    [Test]
    public async Task OpenAuthenticator_WithoutApp_ShowsHint_AndKeepsTheKey()
    {
        var vm = await CreateAsync();
        _launcher.Result = false;
        await vm.StartSetupCommand.ExecuteAsync(null);

        await vm.OpenAuthenticatorCommand.ExecuteAsync(null);

        await Assert.That(vm.ErrorMessage).Contains("keine Authenticator-App");
        await Assert.That(vm.ShowSetup).IsTrue();
    }

    [Test]
    public async Task ConfirmSetup_WithValidCode_EnablesAndShowsRecoveryCodes_WithReloginHint()
    {
        var vm = await EnableAsync();

        await Assert.That(Backend.Requests.Last()).IsEqualTo(new TwoFactorRequest(Enable: true, TwoFactorCode: "123456"));
        await Assert.That(vm.HasRecoveryCodes).IsTrue();
        await Assert.That(vm.RecoveryCodes.Count).IsEqualTo(10);
        await Assert.That(vm.SetupKey).IsNull();
        await Assert.That(vm.ShowSetup).IsFalse();
        await Assert.That(vm.RecoveryHint).Contains("Speichere sie jetzt");
        await Assert.That(vm.ReloginHint).Contains("neu anmelden");
        await Assert.That(_dialogs.Announcements.Last()).Contains("eingeschaltet");
    }

    [Test]
    public async Task AcknowledgeCodes_ClearsThemFromMemory()
    {
        var vm = await EnableAsync();

        vm.AcknowledgeCodesCommand.Execute(null);

        await Assert.That(vm.HasRecoveryCodes).IsFalse();
        await Assert.That(vm.RecoveryCodes).IsEmpty();
        await Assert.That(vm.ShowOn).IsTrue();
        await Assert.That(vm.StatusText).Contains("10");
    }

    [Test]
    [Arguments("12345")]
    [Arguments("abcdef")]
    [Arguments("")]
    public async Task ConfirmSetup_WithMalformedCode_ShowsFieldError_WithoutRequest(string code)
    {
        var vm = await CreateAsync();
        await vm.StartSetupCommand.ExecuteAsync(null);
        var before = Backend.Requests.Count;

        vm.CodeText = code;
        await vm.ConfirmSetupCommand.ExecuteAsync(null);

        await Assert.That(vm.FieldError).Contains("6-stellig");
        await Assert.That(Backend.Requests.Count).IsEqualTo(before);
    }

    [Test]
    public async Task ConfirmSetup_WithWrongCode_ShowsFieldError_FocusesIt_AndStaysInSetup()
    {
        var vm = await CreateAsync();
        await vm.StartSetupCommand.ExecuteAsync(null);

        vm.CodeText = "999999";
        await vm.ConfirmSetupCommand.ExecuteAsync(null);

        await Assert.That(vm.FieldError).Contains("Der Code ist ungültig");
        await Assert.That(vm.ShowSetup).IsTrue();
        await Assert.That(vm.HasRecoveryCodes).IsFalse();
        await Assert.That(_focusRequests).IsEqualTo(2);
        await Assert.That(_dialogs.Announcements.Last()).Contains("Der Code ist ungültig");
    }

    [Test]
    public async Task CancelSetup_DropsTheKey()
    {
        var vm = await CreateAsync();
        await vm.StartSetupCommand.ExecuteAsync(null);

        vm.CancelSetupCommand.Execute(null);

        await Assert.That(vm.SetupKey).IsNull();
        await Assert.That(vm.ShowOff).IsTrue();
    }

    [Test]
    public async Task Reset_AsksForTheCurrentCode_ThenShowsNewCodesOnce()
    {
        var vm = await CreateAsync(enabled: true, codesLeft: 2);

        vm.BeginResetCommand.Execute(null);
        await Assert.That(vm.ShowAction).IsTrue();
        await Assert.That(Backend.Requests.Count).IsEqualTo(1); // nur der Status
        await Assert.That(_focusRequests).IsEqualTo(1);
        vm.CodeText = "123456";
        await vm.ConfirmActionCommand.ExecuteAsync(null);

        await Assert.That(Backend.Requests.Last()).IsEqualTo(new TwoFactorRequest(ResetRecoveryCodes: true, TwoFactorCode: "123456"));
        await Assert.That(vm.RecoveryCodes.Count).IsEqualTo(10);
        await Assert.That(vm.ShowAction).IsFalse();
    }

    [Test]
    public async Task Reset_CanBeCancelled_WithoutRequest()
    {
        var vm = await CreateAsync(enabled: true, codesLeft: 2);

        vm.BeginResetCommand.Execute(null);
        vm.CancelActionCommand.Execute(null);

        await Assert.That(Backend.Requests.Count).IsEqualTo(1);
        await Assert.That(vm.ShowOn).IsTrue();
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Action_WithWrongCode_ShowsFieldError_AndFocusesIt(bool disable)
    {
        var vm = await CreateAsync(enabled: true, codesLeft: 2);
        if (disable)
        {
            vm.BeginDisableCommand.Execute(null);
        }
        else
        {
            vm.BeginResetCommand.Execute(null);
        }

        vm.CodeText = "000000";
        await vm.ConfirmActionCommand.ExecuteAsync(null);

        await Assert.That(vm.FieldError).Contains("Der Code ist ungültig");
        await Assert.That(vm.ShowAction).IsTrue();
        await Assert.That(Backend.Enabled).IsTrue();
        await Assert.That(vm.HasRecoveryCodes).IsFalse();
        await Assert.That(_focusRequests).IsEqualTo(2);
    }

    [Test]
    public async Task Disable_AsksForTheCurrentCode_ThenTurnsOff_WithReloginHint()
    {
        var vm = await CreateAsync(enabled: true, codesLeft: 10);

        vm.BeginDisableCommand.Execute(null);
        vm.CodeText = "123456";
        await vm.ConfirmActionCommand.ExecuteAsync(null);

        await Assert.That(Backend.Requests.Last()).IsEqualTo(new TwoFactorRequest(Enable: false, TwoFactorCode: "123456"));
        await Assert.That(Backend.Enabled).IsFalse();
        await Assert.That(vm.ShowOff).IsTrue();
        await Assert.That(vm.StatusMessage).Contains("ausgeschaltet");
        await Assert.That(vm.StatusMessage).Contains("neu anmelden");
        await Assert.That(_dialogs.Announcements.Last()).Contains("neu anmelden");
    }

    [Test]
    public async Task EnglishTexts_AreUsed()
    {
        var vm = await CreateAsync(culture: "en");

        await Assert.That(vm.Title).IsEqualTo("Two-factor sign-in");
        await vm.StartSetupCommand.ExecuteAsync(null);
        vm.CodeText = "1";
        await vm.ConfirmSetupCommand.ExecuteAsync(null);
        await Assert.That(vm.FieldError).Contains("6-digit");
    }

    [Test]
    public async Task OtpAuthUri_IsUrlEncoded_AndFallsBackToTheIssuerWithoutEmail()
    {
        await Assert.That(TotpSetup.BuildUri("Blutdruck-Tagebuch", "a+b@example.test", "ABCD EFGH"))
            .IsEqualTo("otpauth://totp/Blutdruck-Tagebuch:a%2Bb%40example.test?secret=ABCDEFGH&issuer=Blutdruck-Tagebuch");
        await Assert.That(TotpSetup.BuildUri("Blutdruck-Tagebuch", null, "ABCD"))
            .IsEqualTo("otpauth://totp/Blutdruck-Tagebuch?secret=ABCD&issuer=Blutdruck-Tagebuch");
        await Assert.That(TotpSetup.FormatKey("abcdefghij")).IsEqualTo("ABCD EFGH IJ");
    }
}
