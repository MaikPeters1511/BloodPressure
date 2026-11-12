using System.Net;
using CleanArchitecture.Presentation.Maui.Core.Services;
using CleanArchitecture.Presentation.Maui.Core.ViewModels;

namespace CleanArchitecture.Maui.Tests;

[Category("Unit")]
public class LoginViewModelTests
{
    private readonly FakeSecureStore _store = new();
    private readonly FakeAuthApi _auth = new();
    private readonly FakeNavigator _nav = new();
    private readonly FakeDialogs _dialogs = new();

    private LoginViewModel Create()
    {
        var session = new SessionService(_store, _auth, Clock.Fixed());
        return new LoginViewModel(session, _nav, _dialogs, new ResxLocalizer(Culture.De));
    }

    [Test]
    public async Task Login_Success_NavigatesToMeasurements()
    {
        var vm = Create();
        vm.Email = "a@example.test";
        vm.Password = "pw";

        await vm.LoginCommand.ExecuteAsync(null);

        await Assert.That(_nav.Visited).IsEquivalentTo(new[] { "measurements" });
        await Assert.That(vm.ErrorMessage).IsNull();
        await Assert.That(_store.Items.Count).IsEqualTo(3);
    }

    [Test]
    public async Task Login_WrongPassword_ShowsLocalizedError_AndStoresNothing()
    {
        _auth.OnLogin = _ => Task.FromException<CleanArchitecture.Contracts.TokenResponse>(ApiExceptions.Create(HttpStatusCode.Unauthorized));
        var vm = Create();
        vm.Email = "a@example.test";
        vm.Password = "bad";

        await vm.LoginCommand.ExecuteAsync(null);

        await Assert.That(vm.ErrorMessage).IsEqualTo("E-Mail oder Passwort ist falsch.");
        await Assert.That(_store.Items).IsEmpty();
        await Assert.That(_nav.Visited).IsEmpty();
        await Assert.That(_dialogs.Announcements).Contains("E-Mail oder Passwort ist falsch.");
    }

    [Test]
    public async Task Login_EmptyFields_ShowsMessage_WithoutApiCall()
    {
        var vm = Create();

        await vm.LoginCommand.ExecuteAsync(null);

        await Assert.That(vm.ErrorMessage).IsEqualTo("Bitte gib E-Mail und Passwort ein.");
        await Assert.That(_auth.Calls).IsEmpty();
    }

    [Test]
    public async Task Register_ValidationError_ShowsServerMessages()
    {
        _auth.OnRegister = _ => Task.FromException(ApiExceptions.Validation("PasswordTooShort", "Passwort zu kurz."));
        var vm = Create();
        vm.Email = "a@example.test";
        vm.Password = "x";

        await vm.RegisterCommand.ExecuteAsync(null);

        await Assert.That(vm.ErrorMessage).IsEqualTo("Passwort zu kurz.");
        await Assert.That(_nav.Visited).IsEmpty();
    }

    [Test]
    public async Task Register_Success_ShowsConfirmationNotice_WithoutSigningInOrNavigating()
    {
        var vm = Create();
        vm.Email = "a@example.test";
        vm.Password = "pw";

        await vm.RegisterCommand.ExecuteAsync(null);

        await Assert.That(vm.ConfirmationPending).IsTrue();
        await Assert.That(vm.NoticeMessage).IsEqualTo("Wir haben dir eine E-Mail an a@example.test geschickt. Bestätige sie, bevor du dich anmeldest.");
        await Assert.That(vm.ErrorMessage).IsNull();
        await Assert.That(_nav.Visited).IsEmpty();
        await Assert.That(_store.Items).IsEmpty();
        await Assert.That(_auth.Calls).IsEquivalentTo(new[] { "register" });
        await Assert.That(_dialogs.Announcements).Contains(vm.NoticeMessage!);
    }

    [Test]
    public async Task Login_NotAllowed_ShowsConfirmationNotice_InsteadOfWrongCredentials()
    {
        _auth.OnLogin = _ => Task.FromException<CleanArchitecture.Contracts.TokenResponse>(ApiExceptions.LoginRejected("NotAllowed"));
        var vm = Create();
        vm.Email = "a@example.test";
        vm.Password = "pw";

        await vm.LoginCommand.ExecuteAsync(null);

        await Assert.That(vm.ConfirmationPending).IsTrue();
        await Assert.That(vm.NoticeMessage).Contains("a@example.test");
        await Assert.That(vm.ErrorMessage).IsNull();
        await Assert.That(_nav.Visited).IsEmpty();
    }

    [Test]
    public async Task Login_Failed_ShowsWrongCredentials_WithoutResend()
    {
        _auth.OnLogin = _ => Task.FromException<CleanArchitecture.Contracts.TokenResponse>(ApiExceptions.LoginRejected("Failed"));
        var vm = Create();
        vm.Email = "a@example.test";
        vm.Password = "bad";

        await vm.LoginCommand.ExecuteAsync(null);

        await Assert.That(vm.ErrorMessage).IsEqualTo("E-Mail oder Passwort ist falsch.");
        await Assert.That(vm.ConfirmationPending).IsFalse();
        await Assert.That(vm.NoticeMessage).IsNull();
    }

    [Test]
    public async Task ResendConfirmation_CallsApi_AndAnnouncesNeutralAnswer()
    {
        var vm = Create();
        vm.Email = "a@example.test";
        vm.Password = "pw";
        await vm.RegisterCommand.ExecuteAsync(null);

        await vm.ResendConfirmationCommand.ExecuteAsync(null);

        await Assert.That(_auth.Resent).IsEquivalentTo(new[] { "a@example.test" });
        await Assert.That(vm.NoticeMessage).IsEqualTo("Falls die Adresse bekannt ist, haben wir die Mail erneut gesendet.");
        await Assert.That(_dialogs.Announcements).Contains("Falls die Adresse bekannt ist, haben wir die Mail erneut gesendet.");
    }

    [Test]
    public async Task ResendConfirmation_WhileBusy_IsIgnored()
    {
        var gate = new TaskCompletionSource();
        _auth.OnResend = _ => gate.Task;
        var vm = Create();
        vm.Email = "a@example.test";
        vm.Password = "pw";
        await vm.RegisterCommand.ExecuteAsync(null);

        var first = vm.ResendConfirmationCommand.ExecuteAsync(null);
        await Assert.That(vm.IsBusy).IsTrue();
        await vm.ResendConfirmationCommand.ExecuteAsync(null);
        gate.SetResult();
        await first;

        await Assert.That(_auth.Resent.Count).IsEqualTo(1);
        await Assert.That(vm.IsBusy).IsFalse();
    }

    [Test]
    public async Task ResendConfirmation_Failure_ShowsGenericMessage_WithoutDetails()
    {
        _auth.OnResend = _ => Task.FromException(new HttpRequestException("secret host"));
        var vm = Create();
        vm.Email = "a@example.test";
        vm.Password = "pw";
        await vm.RegisterCommand.ExecuteAsync(null);

        await vm.ResendConfirmationCommand.ExecuteAsync(null);

        await Assert.That(vm.ErrorMessage).IsEqualTo("Etwas ist schiefgegangen. Bitte versuche es später erneut.");
    }

    [Test]
    public async Task EnglishCulture_UsesEnglishNotice()
    {
        var session = new SessionService(_store, _auth, Clock.Fixed());
        var vm = new LoginViewModel(session, _nav, _dialogs, new ResxLocalizer(Culture.En));
        vm.Email = "a@example.test";
        vm.Password = "pw";

        await vm.RegisterCommand.ExecuteAsync(null);

        await Assert.That(vm.NoticeMessage).IsEqualTo("We sent an email to a@example.test. Confirm it before you sign in.");
    }

    [Test]
    public async Task UnexpectedError_ShowsGenericMessage_WithoutDetails()
    {
        _auth.OnLogin = _ => throw new HttpRequestException("secret host name down");
        var vm = Create();
        vm.Email = "a@example.test";
        vm.Password = "pw";

        await vm.LoginCommand.ExecuteAsync(null);

        await Assert.That(vm.ErrorMessage).IsEqualTo("Etwas ist schiefgegangen. Bitte versuche es später erneut.");
        await Assert.That(vm.ErrorMessage!).DoesNotContain("secret");
    }
}
