using CleanArchitecture.Contracts;
using CleanArchitecture.Presentation.Maui.Core.Services;
using CleanArchitecture.Presentation.Maui.Core.ViewModels;

namespace CleanArchitecture.Maui.Tests;

/// <summary>Zweiter Anmeldeschritt bei eingeschalteter Zwei-Faktor-Anmeldung (ADR 0006, Phase 2).</summary>
[Category("Unit")]
public class LoginTwoFactorViewModelTests
{
    private readonly FakeSecureStore _store = new();
    private readonly FakeAuthApi _auth = new();
    private readonly FakeNavigator _nav = new();
    private readonly FakeDialogs _dialogs = new();
    private int _focusRequests;

    private async Task<LoginViewModel> StartAsync(string culture = "de", string email = "a@example.test", string password = "geheim")
    {
        // Ohne Code antwortet die API mit 401 "RequiresTwoFactor"; mit Code oder Wiederherstellungscode gibt es Tokens.
        _auth.OnLogin = request => request is { TwoFactorCode: null, TwoFactorRecoveryCode: null }
            ? Task.FromException<TokenResponse>(ApiExceptions.RequiresTwoFactor())
            : Task.FromResult(FakeAuthApi.Tokens("access-1", "refresh-1"));
        var session = new SessionService(_store, _auth, Clock.Fixed());
        var vm = new LoginViewModel(session, _nav, _dialogs, new ResxLocalizer(culture == "de" ? Culture.De : Culture.En));
        vm.FocusCodeRequested += () => _focusRequests++;
        vm.Email = email;
        vm.Password = password;
        await vm.LoginCommand.ExecuteAsync(null);
        return vm;
    }

    [Test]
    public async Task RequiresTwoFactor_ShowsCodeStep_WithoutError_AnnouncesIt_AndFocusesTheField()
    {
        var vm = await StartAsync();

        await Assert.That(vm.RequiresTwoFactor).IsTrue();
        await Assert.That(vm.IsCredentialsStep).IsFalse();
        await Assert.That(vm.ShowAuthenticatorCode).IsTrue();
        await Assert.That(vm.ShowRecoveryCode).IsFalse();
        await Assert.That(vm.CodeLabel).IsEqualTo("Code aus der Authenticator-App");
        await Assert.That(vm.ToggleText).IsEqualTo("Wiederherstellungscode verwenden");
        await Assert.That(vm.ErrorMessage).IsNull();
        await Assert.That(_dialogs.Announcements).Contains(vm.CodeHint);
        await Assert.That(_focusRequests).IsEqualTo(1);
        await Assert.That(_nav.Visited).IsEmpty();
        await Assert.That(_store.Items).IsEmpty();
    }

    [Test]
    public async Task SubmittingTheCode_SendsItWithTheRememberedCredentials_AndSignsIn()
    {
        var vm = await StartAsync(email: "x@example.test", password: "geheim");

        vm.TwoFactorCode = "123 456";
        await vm.LoginCommand.ExecuteAsync(null);

        await Assert.That(_auth.LoginRequests[1]).IsEqualTo(new LoginRequest("x@example.test", "geheim", "123456"));
        await Assert.That(_nav.Visited).IsEquivalentTo(new[] { "measurements" });
        await Assert.That(vm.Password).IsEqualTo("");
        await Assert.That(vm.TwoFactorCode).IsEqualTo("");
        await Assert.That(vm.RequiresTwoFactor).IsFalse();
        await Assert.That(_store.Items.Values).DoesNotContain("geheim");
    }

    [Test]
    public async Task Toggle_SwitchesToRecoveryCode_AndSendsIt()
    {
        var vm = await StartAsync();

        vm.ToggleRecoveryCodeCommand.Execute(null);
        await Assert.That(vm.UseRecoveryCode).IsTrue();
        await Assert.That(vm.ShowRecoveryCode).IsTrue();
        await Assert.That(vm.ShowAuthenticatorCode).IsFalse();
        await Assert.That(vm.CodeLabel).IsEqualTo("Wiederherstellungscode");
        await Assert.That(vm.ToggleText).IsEqualTo("Code aus der Authenticator-App verwenden");
        await Assert.That(_focusRequests).IsEqualTo(2);

        vm.TwoFactorCode = "abcde-fghij";
        await vm.LoginCommand.ExecuteAsync(null);

        await Assert.That(_auth.LoginRequests[1].TwoFactorRecoveryCode).IsEqualTo("abcde-fghij");
        await Assert.That(_auth.LoginRequests[1].TwoFactorCode).IsNull();
        await Assert.That(_nav.Visited).IsEquivalentTo(new[] { "measurements" });
    }

    [Test]
    public async Task WrongCode_ShowsOwnMessage_NotWrongCredentials_AndStaysInStepTwo()
    {
        var vm = await StartAsync();
        _auth.OnLogin = _ => Task.FromException<TokenResponse>(ApiExceptions.LoginRejected("Failed"));

        vm.TwoFactorCode = "000000";
        await vm.LoginCommand.ExecuteAsync(null);

        await Assert.That(vm.ErrorMessage).Contains("Der Code ist ungültig");
        await Assert.That(vm.ErrorMessage).DoesNotContain("E-Mail oder Passwort");
        await Assert.That(vm.RequiresTwoFactor).IsTrue();
        await Assert.That(_dialogs.Announcements.Last()).Contains("Der Code ist ungültig");
        await Assert.That(_focusRequests).IsEqualTo(2);
        await Assert.That(_nav.Visited).IsEmpty();
    }

    [Test]
    public async Task LockedOut_ShowsOwnMessage_AndDiscardsThePassword()
    {
        var vm = await StartAsync();
        _auth.OnLogin = _ => Task.FromException<TokenResponse>(ApiExceptions.LoginRejected("LockedOut"));

        vm.TwoFactorCode = "000000";
        await vm.LoginCommand.ExecuteAsync(null);

        await Assert.That(vm.ErrorMessage).Contains("vorübergehend gesperrt");
        await Assert.That(vm.RequiresTwoFactor).IsFalse();
        await Assert.That(vm.Password).IsEqualTo("");
        await Assert.That(_dialogs.Announcements.Last()).Contains("vorübergehend gesperrt");
    }

    [Test]
    public async Task EmptyCode_ShowsHint_WithoutRequest()
    {
        var vm = await StartAsync();

        await vm.LoginCommand.ExecuteAsync(null);

        await Assert.That(vm.ErrorMessage).Contains("Bitte gib den Code ein");
        await Assert.That(_auth.LoginRequests.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Back_DiscardsThePassword_AndShowsTheCredentialsAgain()
    {
        var vm = await StartAsync(email: "keep@example.test");

        vm.BackCommand.Execute(null);

        await Assert.That(vm.RequiresTwoFactor).IsFalse();
        await Assert.That(vm.IsCredentialsStep).IsTrue();
        await Assert.That(vm.Password).IsEqualTo("");
        await Assert.That(vm.TwoFactorCode).IsEqualTo("");
        await Assert.That(vm.Email).IsEqualTo("keep@example.test");
        await Assert.That(vm.ErrorMessage).IsNull();
    }

    [Test]
    public async Task BusyGuard_IgnoresASecondSubmit()
    {
        var vm = await StartAsync();
        var gate = new TaskCompletionSource<TokenResponse>();
        _auth.OnLogin = _ => gate.Task;
        vm.TwoFactorCode = "123456";

        var first = vm.LoginCommand.ExecuteAsync(null);
        await vm.LoginCommand.ExecuteAsync(null);
        gate.SetResult(FakeAuthApi.Tokens("a", "r"));
        await first;

        await Assert.That(_auth.LoginRequests.Count).IsEqualTo(2);
    }

    [Test]
    public async Task English_UsesEnglishTexts()
    {
        var vm = await StartAsync("en");

        await Assert.That(vm.CodeLabel).IsEqualTo("Code from the authenticator app");
        await Assert.That(vm.ToggleText).IsEqualTo("Use a recovery code");
    }
}
