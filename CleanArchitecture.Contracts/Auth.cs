using Refit;

namespace CleanArchitecture.Contracts;

/// <summary>Zugangsdaten für die Registrierung.</summary>
public sealed record CredentialsRequest(string Email, string Password);

/// <summary>
/// Anmeldung. Ist die Zwei-Faktor-Anmeldung eingeschaltet, antwortet die API ohne Code mit 401 und <c>detail</c> "RequiresTwoFactor";
/// dann genau einen der beiden Codes mitsenden (<see cref="TwoFactorCode"/> aus der Authenticator-App oder einen einmaligen
/// <see cref="TwoFactorRecoveryCode"/>).
/// </summary>
public sealed record LoginRequest(string Email, string Password, string? TwoFactorCode = null, string? TwoFactorRecoveryCode = null);

/// <summary>
/// Anfrage für <c>POST /api/auth/manage/2fa</c> (angemeldet). Leere Anfrage = Status abfragen. <c>Enable</c> mit
/// <c>TwoFactorCode</c> schaltet ein bzw. (mit <c>false</c>) aus; <c>ResetSharedKey</c> erzeugt einen neuen Schlüssel,
/// <c>ResetRecoveryCodes</c> neue Wiederherstellungscodes.
/// </summary>
public sealed record TwoFactorRequest(bool? Enable = null, string? TwoFactorCode = null, bool ResetSharedKey = false, bool ResetRecoveryCodes = false, bool ForgetMachine = false);

/// <summary>
/// Antwort von <c>manage/2fa</c>. <c>SharedKey</c> ist der Schlüssel für die Authenticator-App (Base32). <c>RecoveryCodes</c> ist nur
/// beim Einschalten und beim Neuerzeugen gefüllt und wird nur dann angezeigt.
/// </summary>
public sealed record TwoFactorResponse(string SharedKey, int RecoveryCodesLeft, string[]? RecoveryCodes, bool IsTwoFactorEnabled, bool IsMachineRemembered);

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
    Task<TokenResponse> LoginAsync([Body] LoginRequest request, CancellationToken cancellationToken = default);

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

/// <summary>Zwei-Faktor-Anmeldung (TOTP) des angemeldeten Nutzers (mit Bearer-Token); siehe <see cref="TwoFactorRequest"/>.</summary>
public interface ITwoFactorApi
{
    [Post("/api/auth/manage/2fa")]
    Task<TwoFactorResponse> ManageTwoFactorAsync([Body] TwoFactorRequest request, CancellationToken cancellationToken = default);
}
