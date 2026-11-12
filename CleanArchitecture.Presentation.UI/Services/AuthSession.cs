using System.Globalization;
using CleanArchitecture.Contracts;
using Refit;

namespace CleanArchitecture.Presentation.UI.Services;

/// <summary>
/// Hält Access- und Refresh-Token, erneuert ein abgelaufenes Access-Token und dient dem Refit-Client als
/// <see cref="IAccessTokenProvider"/>.
/// </summary>
public sealed class AuthSession(ITokenStorage storage, IAuthApi authApi, TimeProvider time) : IAccessTokenProvider
{
    private const string AccessKey = "bp.access";
    private const string RefreshKey = "bp.refresh";
    private const string ExpiresKey = "bp.expires";

    // Sicherheitsabstand, damit ein Token nicht kurz vor dem Ablauf noch gesendet wird.
    private static readonly TimeSpan Skew = TimeSpan.FromSeconds(30);

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private string? _access;
    private string? _refresh;
    private DateTimeOffset _expires;

    public bool IsAuthenticated => _access is not null || _refresh is not null;

    public event Action? Changed;

    /// <summary>Stellt die Anmeldung nach einem Neuladen aus dem Speicher wieder her.</summary>
    public async Task InitializeAsync()
    {
        _access = await storage.GetAsync(AccessKey);
        _refresh = await storage.GetAsync(RefreshKey);
        _expires = DateTimeOffset.TryParse(await storage.GetAsync(ExpiresKey), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : DateTimeOffset.MinValue;
        if (!IsAuthenticated)
        {
            return;
        }

        Changed?.Invoke();
    }

    /// <summary>E-Mail-Adresse der aktuellen Anmeldung; nur im Speicher (für die Beschriftung im Authenticator), nach einem Neuladen <c>null</c>.</summary>
    public string? Email { get; private set; }

    /// <summary>
    /// Meldet an. Bei eingeschalteter Zwei-Faktor-Anmeldung wirft die API zunächst 401 "RequiresTwoFactor"; dann erneut mit
    /// <paramref name="twoFactorCode"/> (Authenticator) oder <paramref name="recoveryCode"/> (einmalig) aufrufen.
    /// </summary>
    public async Task LoginAsync(string email, string password, string? twoFactorCode = null, string? recoveryCode = null, CancellationToken cancellationToken = default)
    {
        await StoreAsync(await authApi.LoginAsync(new LoginRequest(email, password, twoFactorCode, recoveryCode), cancellationToken));
        Email = email;
    }

    /// <summary>Registriert den Nutzer. Angemeldet wird nicht: erst nach der Bestätigung der E-Mail-Adresse.</summary>
    public Task RegisterAsync(string email, string password, CancellationToken cancellationToken = default)
        => authApi.RegisterAsync(new CredentialsRequest(email, password), cancellationToken);

    /// <summary>Bestätigt die E-Mail-Adresse mit den Werten aus dem Link der Bestätigungsmail.</summary>
    public Task ConfirmEmailAsync(string userId, string code, CancellationToken cancellationToken = default)
        => authApi.ConfirmEmailAsync(userId, code, cancellationToken);

    /// <summary>Sendet die Bestätigungsmail erneut (die API antwortet für bekannte und unbekannte Adressen gleich).</summary>
    public Task ResendConfirmationAsync(string email, CancellationToken cancellationToken = default)
        => authApi.ResendConfirmationEmailAsync(new ResendConfirmationRequest(email), cancellationToken);

    /// <summary>
    /// <c>true</c>, wenn die API den Widerruf bestätigt hat. Ohne Netz oder bei einem Fehler wird nur lokal abgemeldet
    /// (die Refresh-Token laufen dann erst nach ihrer Lebensdauer ab).
    /// </summary>
    public bool LastLogoutRevokedOnServer { get; private set; }

    public async Task LogoutAsync()
    {
        LastLogoutRevokedOnServer = false;
        try
        {
            if (await GetAccessTokenAsync(CancellationToken.None) is { } token)
            {
                await authApi.LogoutAsync($"Bearer {token}");
                LastLogoutRevokedOnServer = true;
            }
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException)
        {
            LastLogoutRevokedOnServer = false; // Abmelden darf nicht am Netz scheitern: lokal wird trotzdem gelöscht
        }

        await ClearAsync();
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (!IsAuthenticated)
        {
            return null;
        }

        if (_access is not null && time.GetUtcNow() < _expires - Skew)
        {
            return _access;
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            // Ein anderer Aufruf kann inzwischen erneuert oder abgemeldet haben.
            if (!IsAuthenticated)
            {
                return null;
            }

            if (_access is not null && time.GetUtcNow() < _expires - Skew)
            {
                return _access;
            }

            if (_refresh is null)
            {
                await ClearAsync();
                return null;
            }

            try
            {
                await StoreAsync(await authApi.RefreshAsync(new RefreshRequest(_refresh), cancellationToken));
                return _access;
            }
            catch (ApiException)
            {
                await ClearAsync();
                return null;
            }
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task StoreAsync(TokenResponse tokens)
    {
        _access = tokens.AccessToken;
        _refresh = tokens.RefreshToken;
        _expires = time.GetUtcNow().AddSeconds(tokens.ExpiresIn);
        await storage.SetAsync(AccessKey, _access);
        await storage.SetAsync(RefreshKey, _refresh);
        await storage.SetAsync(ExpiresKey, _expires.ToString("o", CultureInfo.InvariantCulture));
        Changed?.Invoke();
    }

    private async Task ClearAsync()
    {
        _access = null;
        _refresh = null;
        _expires = DateTimeOffset.MinValue;
        Email = null;
        await storage.RemoveAsync(AccessKey);
        await storage.RemoveAsync(RefreshKey);
        await storage.RemoveAsync(ExpiresKey);
        Changed?.Invoke();
    }
}
