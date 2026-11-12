namespace CleanArchitecture.Infrastructure.Email;

/// <summary>
/// Einstellungen für den E-Mail-Versand aus dem Konfigurationsabschnitt <c>Email</c> (Umgebungsvariablen
/// <c>Email__Smtp__Host</c>, <c>Email__From</c> usw.). Zugangsdaten gehören nie in den Code oder ins Repository.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>Absenderadresse, z. B. <c>noreply@example.org</c> (<c>Email__From</c>).</summary>
    public string? From { get; set; }

    /// <summary>Anzeigename des Absenders (<c>Email__FromName</c>).</summary>
    public string FromName { get; set; } = "Blutdruck-Tagebuch";

    /// <summary>
    /// Adresse der Blazor-Seite, die den Bestätigungslink aufnimmt (<c>Email__ConfirmationBaseUrl</c>, z. B.
    /// <c>https://app.example.org/confirm-email</c>). Ohne Angabe enthält die Mail den Link auf den API-Endpunkt.
    /// </summary>
    public string? ConfirmationBaseUrl { get; set; }

    public SmtpOptions Smtp { get; set; } = new();
}

public sealed class SmtpOptions
{
    /// <summary>SMTP-Server (<c>Email__Smtp__Host</c>). Ohne Wert wird kein Server angesprochen.</summary>
    public string? Host { get; set; }

    public int Port { get; set; } = 587;

    public string? User { get; set; }

    public string? Password { get; set; }

    /// <summary>Verschlüsselung: <c>Auto</c> (Standard), <c>StartTls</c>, <c>SslOnConnect</c> oder <c>None</c> (nur lokal).</summary>
    public string Tls { get; set; } = "Auto";
}
