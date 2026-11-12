using System.Net;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using MimeKit;

namespace CleanArchitecture.Infrastructure.Email;

/// <summary>Versendet die Identity-Mails über einen beliebigen SMTP-Server (MailKit).</summary>
public sealed class MailKitEmailSender(IOptions<EmailOptions> options) : IEmailSender<IdentityUser>
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Baut den Link für die Mail. Ist eine Seite konfiguriert (<see cref="EmailOptions.ConfirmationBaseUrl"/>), werden
    /// <c>userId</c> und <c>code</c> aus dem API-Link dorthin übernommen, sonst bleibt der API-Link bestehen.
    /// </summary>
    public static string BuildConfirmationLink(string apiLink, string? pageUrl)
    {
        // MapIdentityApi übergibt die Links HTML-kodiert ("&amp;"); für eine Text-Mail und zum Auslesen wieder dekodieren.
        apiLink = WebUtility.HtmlDecode(apiLink);
        if (string.IsNullOrWhiteSpace(pageUrl))
        {
            return apiLink;
        }

        var query = QueryHelpers.ParseQuery(new Uri(apiLink).Query);
        return QueryHelpers.AddQueryString(pageUrl, new Dictionary<string, string?>
        {
            ["userId"] = query["userId"].ToString(),
            ["code"] = query["code"].ToString()
        });
    }

    public Task SendConfirmationLinkAsync(IdentityUser user, string email, string confirmationLink)
    {
        var link = BuildConfirmationLink(confirmationLink, options.Value.ConfirmationBaseUrl);
        return SendAsync(email,
            "E-Mail-Adresse bestätigen / Confirm your email address",
            $"Bitte bestätige deine E-Mail-Adresse mit diesem Link:\n{link}\n\n"
            + $"Please confirm your email address with this link:\n{link}\n\n"
            + "Wenn du dich nicht registriert hast, ignoriere diese Nachricht. / If you did not register, ignore this message.");
    }

    public Task SendPasswordResetLinkAsync(IdentityUser user, string email, string resetLink)
        => SendAsync(email, "Passwort zurücksetzen / Reset your password",
            $"Link zum Zurücksetzen des Passworts / Password reset link:\n{WebUtility.HtmlDecode(resetLink)}");

    public Task SendPasswordResetCodeAsync(IdentityUser user, string email, string resetCode)
        => SendAsync(email, "Passwort zurücksetzen / Reset your password",
            $"Code zum Zurücksetzen des Passworts / Password reset code:\n{resetCode}");

    private async Task SendAsync(string to, string subject, string body)
    {
        var settings = options.Value;
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.From));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient { Timeout = (int)Timeout.TotalMilliseconds };
        var tls = Enum.TryParse<SecureSocketOptions>(settings.Smtp.Tls, ignoreCase: true, out var parsed)
            ? parsed
            : SecureSocketOptions.Auto;
        await client.ConnectAsync(settings.Smtp.Host, settings.Smtp.Port, tls);
        if (!string.IsNullOrEmpty(settings.Smtp.User))
        {
            await client.AuthenticateAsync(settings.Smtp.User, settings.Smtp.Password);
        }

        await client.SendAsync(message);
        await client.DisconnectAsync(quit: true);
    }
}
