using Refit;

namespace CleanArchitecture.Contracts;

public sealed record CredentialsRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

/// <summary>Anfrage für <c>POST /api/auth/resendConfirmationEmail</c>.</summary>
public sealed record ResendConfirmationRequest(string Email);

/// <summary>Antwort von Login und Refresh. <c>ExpiresIn</c> ist die Lebensdauer des Access-Tokens in Sekunden.</summary>
public sealed record TokenResponse(string TokenType, string AccessToken, int ExpiresIn, string RefreshToken);

/// <summary>
/// Anmeldung gegen die Identity-Endpunkte der API. Bewusst ohne Token-Handler registriert, damit die Erneuerung
/// des Tokens nicht von sich selbst abhängt. Fehler: 401 (Zugangsdaten), 400 (Validierung, z. B. Passwortregeln).
/// </summary>
public interface IAuthApi
{
    [Post("/api/auth/register")]
    Task RegisterAsync([Body] CredentialsRequest request, CancellationToken cancellationToken = default);

    [Post("/api/auth/login")]
    Task<TokenResponse> LoginAsync([Body] CredentialsRequest request, CancellationToken cancellationToken = default);

    [Post("/api/auth/refresh")]
    Task<TokenResponse> RefreshAsync([Body] RefreshRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bestätigt die E-Mail-Adresse mit den Werten aus dem Link der Bestätigungsmail (<c>userId</c> und <c>code</c>).
    /// 200 bei Erfolg, sonst 401 (ungültiger oder abgelaufener Code).
    /// </summary>
    [Get("/api/auth/confirmEmail")]
    Task ConfirmEmailAsync([Query] string userId, [Query] string code, CancellationToken cancellationToken = default);

    /// <summary>Sendet die Bestätigungsmail erneut. Die Antwort ist für bekannte und unbekannte Adressen gleich (200).</summary>
    [Post("/api/auth/resendConfirmationEmail")]
    Task ResendConfirmationEmailAsync([Body] ResendConfirmationRequest request, CancellationToken cancellationToken = default);

    /// <summary>Macht alle Refresh-Tokens des Nutzers ungültig. Verlangt den Access-Token im Authorization-Header ("Bearer ...").</summary>
    [Post("/api/auth/logout")]
    Task LogoutAsync([Header("Authorization")] string authorization, CancellationToken cancellationToken = default);
}
