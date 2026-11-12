using System.Net;
using CleanArchitecture.Presentation.Maui.Core.Services;

namespace CleanArchitecture.Maui.Tests;

[Category("Unit")]
public class SessionServiceTests
{
    private readonly FakeSecureStore _store = new();
    private readonly FakeAuthApi _auth = new();
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider _time = Clock.Fixed();

    private SessionService Create() => new(_store, _auth, _time);

    [Test]
    public async Task Login_StoresTokensOnlyInSecureStore_AndAuthenticates()
    {
        var session = Create();

        await session.LoginAsync("a@example.test", "pw");

        await Assert.That(session.IsAuthenticated).IsTrue();
        await Assert.That(_store.Items.Count).IsEqualTo(3);
        await Assert.That(await session.GetAccessTokenAsync(CancellationToken.None)).IsEqualTo("access-1");
    }

    [Test]
    public async Task Login_SendsLoginRequest_WithOptionalCodes()
    {
        var session = Create();

        await session.LoginAsync("a@example.test", "pw");
        await session.LoginAsync("a@example.test", "pw", twoFactorCode: "123456");
        await session.LoginAsync("a@example.test", "pw", recoveryCode: "abcde-fghij");

        await Assert.That(_auth.LoginRequests[0]).IsEqualTo(new CleanArchitecture.Contracts.LoginRequest("a@example.test", "pw"));
        await Assert.That(_auth.LoginRequests[1].TwoFactorCode).IsEqualTo("123456");
        await Assert.That(_auth.LoginRequests[2].TwoFactorRecoveryCode).IsEqualTo("abcde-fghij");
        await Assert.That(_auth.LoginRequests[2].TwoFactorCode).IsNull();
    }

    [Test]
    public async Task Login_RemembersTheEmailInMemoryOnly()
    {
        var session = Create();

        await session.LoginAsync("a@example.test", "pw");

        await Assert.That(session.Email).IsEqualTo("a@example.test");
        await Assert.That(_store.Items.Values).DoesNotContain("a@example.test");
        await Assert.That(_store.Items.Values).DoesNotContain("pw");
    }

    [Test]
    public async Task ApiErrors_RecognizeTwoFactorAndLockout_ByExactDetail()
    {
        await Assert.That(await ApiErrors.IsTwoFactorRequiredAsync(ApiExceptions.RequiresTwoFactor())).IsTrue();
        await Assert.That(await ApiErrors.IsTwoFactorRequiredAsync(ApiExceptions.LoginRejected("Failed"))).IsFalse();
        await Assert.That(await ApiErrors.IsTwoFactorRequiredAsync(ApiExceptions.LoginRejected("requirestwofactor"))).IsFalse();
        await Assert.That(await ApiErrors.IsTwoFactorRequiredAsync(ApiExceptions.Create(HttpStatusCode.Unauthorized))).IsFalse();
        await Assert.That(await ApiErrors.IsLockedOutAsync(ApiExceptions.LoginRejected("LockedOut"))).IsTrue();
        await Assert.That(await ApiErrors.IsLockedOutAsync(ApiExceptions.LoginRejected("Failed"))).IsFalse();
    }

    [Test]
    public async Task Login_Fails_StoresNothing()
    {
        _auth.OnLogin = _ => Task.FromException<CleanArchitecture.Contracts.TokenResponse>(ApiExceptions.Create(HttpStatusCode.Unauthorized));
        var session = Create();

        await Assert.That(async () => await session.LoginAsync("a@example.test", "bad")).Throws<Refit.ApiException>();

        await Assert.That(session.IsAuthenticated).IsFalse();
        await Assert.That(_store.Items).IsEmpty();
    }

    [Test]
    public async Task Initialize_RestoresSessionFromStore()
    {
        var first = Create();
        await first.LoginAsync("a@example.test", "pw");

        var restored = Create();
        await restored.InitializeAsync();

        await Assert.That(restored.IsAuthenticated).IsTrue();
        await Assert.That(await restored.GetAccessTokenAsync(CancellationToken.None)).IsEqualTo("access-1");
    }

    [Test]
    public async Task ExpiredAccessToken_IsRefreshed()
    {
        var session = Create();
        await session.LoginAsync("a@example.test", "pw");
        _time.Advance(TimeSpan.FromHours(2));

        var token = await session.GetAccessTokenAsync(CancellationToken.None);

        await Assert.That(token).IsEqualTo("access-2");
        await Assert.That(_auth.Calls).Contains("refresh");
    }

    [Test]
    public async Task RefreshFails_ClearsSession()
    {
        var session = Create();
        await session.LoginAsync("a@example.test", "pw");
        _auth.OnRefresh = _ => Task.FromException<CleanArchitecture.Contracts.TokenResponse>(ApiExceptions.Create(HttpStatusCode.Unauthorized));
        _time.Advance(TimeSpan.FromHours(2));

        var token = await session.GetAccessTokenAsync(CancellationToken.None);

        await Assert.That(token).IsNull();
        await Assert.That(session.IsAuthenticated).IsFalse();
        await Assert.That(_store.Items).IsEmpty();
    }

    [Test]
    public async Task Logout_RemovesAllTokens()
    {
        var session = Create();
        await session.LoginAsync("a@example.test", "pw");

        await session.LogoutAsync();

        await Assert.That(session.IsAuthenticated).IsFalse();
        await Assert.That(_store.Items).IsEmpty();
    }

    [Test]
    public async Task Register_OnlyRegisters_AndDoesNotSignIn()
    {
        var session = Create();

        await session.RegisterAsync("a@example.test", "pw");

        await Assert.That(_auth.Calls).IsEquivalentTo(new[] { "register" });
        await Assert.That(session.IsAuthenticated).IsFalse();
        await Assert.That(_store.Items).IsEmpty();
    }

    [Test]
    public async Task ConfirmEmail_DelegatesToTheApi()
    {
        var session = Create();

        await session.ConfirmEmailAsync("u1", "code");

        await Assert.That(_auth.Confirmed).IsEquivalentTo(new[] { ("u1", "code") });
    }

    [Test]
    public async Task ResendConfirmation_DelegatesToTheApi()
    {
        var session = Create();

        await session.ResendConfirmationAsync("a@example.test");

        await Assert.That(_auth.Resent).IsEquivalentTo(new[] { "a@example.test" });
    }

    [Test]
    public async Task Logout_TellsTheApiToRevokeTokens()
    {
        var session = Create();
        await session.LoginAsync("a@example.test", "pw");
        string? header = null;
        _auth.OnLogout = authorization =>
        {
            header = authorization;
            return Task.CompletedTask;
        };

        await session.LogoutAsync();

        await Assert.That(header).IsEqualTo("Bearer access-1");
        await Assert.That(session.LastLogoutRevokedOnServer).IsTrue();
        await Assert.That(session.IsAuthenticated).IsFalse();
    }

    [Test]
    public async Task Logout_WhenApiUnreachable_StillSignsOutLocally()
    {
        var session = Create();
        await session.LoginAsync("a@example.test", "pw");
        _auth.OnLogout = _ => Task.FromException(new HttpRequestException("offline"));

        await session.LogoutAsync();

        await Assert.That(session.LastLogoutRevokedOnServer).IsFalse();
        await Assert.That(session.IsAuthenticated).IsFalse();
        await Assert.That(_store.Items).IsEmpty();
    }
}
