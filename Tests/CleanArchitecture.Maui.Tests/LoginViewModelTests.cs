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
    public async Task Register_Success_NavigatesToMeasurements()
    {
        var vm = Create();
        vm.Email = "a@example.test";
        vm.Password = "pw";

        await vm.RegisterCommand.ExecuteAsync(null);

        await Assert.That(_nav.Visited).IsEquivalentTo(new[] { "measurements" });
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
