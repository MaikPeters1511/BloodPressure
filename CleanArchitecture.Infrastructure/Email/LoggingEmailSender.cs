using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace CleanArchitecture.Infrastructure.Email;

/// <summary>
/// Schreibt Bestätigungslinks ins Log statt sie zu versenden. Nur für lokale Entwicklung und Tests ohne SMTP-Server;
/// in Produktion ist der Start ohne SMTP-Konfiguration nicht möglich (siehe <see cref="EmailServiceCollectionExtensions"/>).
/// Der Link enthält ein Einmal-Token und darf deshalb nicht in produktiven Logs landen.
/// </summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender<IdentityUser>
{
    public Task SendConfirmationLinkAsync(IdentityUser user, string email, string confirmationLink)
    {
        logger.LogWarning("E-Mail nicht versendet (kein SMTP-Server konfiguriert). Bestätigungslink für {Email}: {Link}", email, confirmationLink);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetLinkAsync(IdentityUser user, string email, string resetLink)
    {
        logger.LogWarning("E-Mail nicht versendet (kein SMTP-Server konfiguriert). Link zum Zurücksetzen für {Email}: {Link}", email, resetLink);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetCodeAsync(IdentityUser user, string email, string resetCode)
    {
        logger.LogWarning("E-Mail nicht versendet (kein SMTP-Server konfiguriert). Code zum Zurücksetzen für {Email}: {Code}", email, resetCode);
        return Task.CompletedTask;
    }
}
