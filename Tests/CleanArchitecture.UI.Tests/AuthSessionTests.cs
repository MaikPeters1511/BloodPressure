using System.Net;
using CleanArchitecture.Contracts;
using CleanArchitecture.Presentation.UI.Services;
using Microsoft.Extensions.Time.Testing;

namespace CleanArchitecture.UI.Tests;

[Category("Unit")]
public class AuthSessionTests
{
    private readonly FakeTokenStorage _storage = new();
    private readonly FakeAuthApi _api = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero));

    private AuthSession Session() => new(_storage, _api, _time);

    [Test]
    public async Task Login_StoresTokensAndMarksUserAsAuthenticated()
    {
        var session = Session();

        await session.LoginAsync("a@example.test", "pw");

        await Assert.That(session.IsAuthenticated).IsTrue();
        await Assert.That(await session.GetAccessTokenAsync(CancellationToken.None)).IsEqualTo("access-1");
        await Assert.That(_storage.Items["bp.refresh"]).IsEqualTo("refresh-1");
    }

    [Test]
    public async Task Login_SendsLoginRequest_WithoutCodes_ByDefault()
    {
        await Session().LoginAsync("a@example.test", "pw");

        await Assert.That(_api.LoginRequests.Single()).IsEqualTo(new LoginRequest("a@example.test", "pw"));
    }

    [Test]
    public async Task Login_SendsTheAuthenticatorCode_OrTheRecoveryCode()
    {
        var session = Session();

        await session.LoginAsync("a@example.test", "pw", twoFactorCode: "123456");
        await session.LoginAsync("a@example.test", "pw", recoveryCode: "abcde-fghij");

        await Assert.That(_api.LoginRequests[0].TwoFactorCode).IsEqualTo("123456");
        await Assert.That(_api.LoginRequests[0].TwoFactorRecoveryCode).IsNull();
        await Assert.That(_api.LoginRequests[1].TwoFactorRecoveryCode).IsEqualTo("abcde-fghij");
        await Assert.That(_api.LoginRequests[1].TwoFactorCode).IsNull();
    }

    [Test]
    public async Task Login_RemembersTheEmailInMemoryOnly()
    {
        var session = Session();

        await session.LoginAsync("a@example.test", "pw");

        await Assert.That(session.Email).IsEqualTo("a@example.test");
        await Assert.That(_storage.Items.Values).DoesNotContain("a@example.test");
        await Assert.That(_storage.Items.Values).DoesNotContain("pw");
    }

    [Test]
    public async Task IsTwoFactorRequired_OnlyForTheExactDetail()
    {
        await Assert.That(await ApiErrors.IsTwoFactorRequiredAsync(ApiExceptions.RequiresTwoFactor())).IsTrue();
        await Assert.That(await ApiErrors.IsTwoFactorRequiredAsync(ApiExceptions.LoginRejected("Failed"))).IsFalse();
        await Assert.That(await ApiErrors.IsTwoFactorRequiredAsync(ApiExceptions.LoginRejected("requirestwofactor"))).IsFalse();
        await Assert.That(await ApiErrors.IsTwoFactorRequiredAsync(ApiExceptions.Create(HttpStatusCode.Unauthorized))).IsFalse();
        await Assert.That(await ApiErrors.IsLockedOutAsync(ApiExceptions.LoginRejected("LockedOut"))).IsTrue();
        await Assert.That(await ApiErrors.IsLockedOutAsync(ApiExceptions.LoginRejected("Failed"))).IsFalse();
    }

    [Test]
    public async Task Login_Failure_LeavesUserSignedOut_AndRethrows()
    {
        _api.OnLogin = _ => throw ApiExceptions.Create(HttpStatusCode.Unauthorized);
        var session = Session();

        await Assert.That(async () => await session.LoginAsync("a@example.test", "wrong")).Throws<Refit.ApiException>();

        await Assert.That(session.IsAuthenticated).IsFalse();
        await Assert.That(_storage.Items).IsEmpty();
    }

    [Test]
    public async Task Register_OnlyRegisters_AndDoesNotSignIn()
    {
        var session = Session();

        await session.RegisterAsync("a@example.test", "pw");

        await Assert.That(_api.Calls.ToArray()).IsEquivalentTo(new[] { "register" });
        await Assert.That(session.IsAuthenticated).IsFalse();
        await Assert.That(_storage.Items).IsEmpty();
    }

    [Test]
    public async Task ConfirmEmail_DelegatesToTheApi()
    {
        var session = Session();

        await session.ConfirmEmailAsync("u1", "code");

        await Assert.That(_api.Confirmed).IsEquivalentTo(new[] { ("u1", "code") });
    }

    [Test]
    public async Task ResendConfirmation_DelegatesToTheApi()
    {
        var session = Session();

        await session.ResendConfirmationAsync("a@example.test");

        await Assert.That(_api.Resent).IsEquivalentTo(new[] { "a@example.test" });
    }

    [Test]
    public async Task ExpiredAccessToken_IsRefreshed()
    {
        var session = Session();
        await session.LoginAsync("a@example.test", "pw");
        _time.Advance(TimeSpan.FromHours(2));

        var token = await session.GetAccessTokenAsync(CancellationToken.None);

        await Assert.That(token).IsEqualTo("access-2");
        await Assert.That(_storage.Items["bp.refresh"]).IsEqualTo("refresh-2");
    }

    [Test]
    public async Task ValidAccessToken_IsNotRefreshed()
    {
        var session = Session();
        await session.LoginAsync("a@example.test", "pw");
        _time.Advance(TimeSpan.FromMinutes(10));

        await session.GetAccessTokenAsync(CancellationToken.None);

        await Assert.That(_api.Calls).DoesNotContain("refresh");
    }

    [Test]
    public async Task FailedRefresh_ClearsTokensAndSignsOut()
    {
        _api.OnRefresh = _ => throw ApiExceptions.Create(HttpStatusCode.Unauthorized);
        var session = Session();
        await session.LoginAsync("a@example.test", "pw");
        _time.Advance(TimeSpan.FromHours(2));

        var token = await session.GetAccessTokenAsync(CancellationToken.None);

        await Assert.That(token).IsNull();
        await Assert.That(session.IsAuthenticated).IsFalse();
        await Assert.That(_storage.Items).IsEmpty();
    }

    [Test]
    public async Task Initialize_RestoresSessionAfterReload()
    {
        await Session().LoginAsync("a@example.test", "pw");

        var reloaded = Session();
        await reloaded.InitializeAsync();

        await Assert.That(reloaded.IsAuthenticated).IsTrue();
        await Assert.That(await reloaded.GetAccessTokenAsync(CancellationToken.None)).IsEqualTo("access-1");
    }

    [Test]
    public async Task Logout_ClearsEverything_AndRaisesChanged()
    {
        var session = Session();
        await session.LoginAsync("a@example.test", "pw");
        var changed = 0;
        session.Changed += () => changed++;

        await session.LogoutAsync();

        await Assert.That(session.IsAuthenticated).IsFalse();
        await Assert.That(_storage.Items).IsEmpty();
        await Assert.That(changed).IsEqualTo(1);
    }

    [Test]
    public async Task Logout_TellsTheApiToRevokeTokens()
    {
        var session = Session();
        await session.LoginAsync("a@example.test", "pw");
        string? header = null;
        _api.OnLogout = authorization =>
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
        var session = Session();
        await session.LoginAsync("a@example.test", "pw");
        _api.OnLogout = _ => Task.FromException(new HttpRequestException("offline"));

        await session.LogoutAsync();

        await Assert.That(session.LastLogoutRevokedOnServer).IsFalse();
        await Assert.That(session.IsAuthenticated).IsFalse();
        await Assert.That(_storage.Items).IsEmpty();
    }
}
