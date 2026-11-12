using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CleanArchitecture.Infrastructure.Email;

public static class EmailServiceCollectionExtensions
{
    /// <summary>
    /// Registriert den E-Mail-Versand für Identity. Mit <c>Email:Smtp:Host</c> (Umgebungsvariable <c>Email__Smtp__Host</c>)
    /// versendet MailKit die Mails. Ohne Server läuft nur Entwicklung und Test mit einem Log-Sender; in Produktion bricht
    /// der Start ab, weil sich sonst niemand registrieren könnte (die Anmeldung verlangt eine bestätigte Adresse).
    /// </summary>
    public static IHostApplicationBuilder AddEmailSending(this IHostApplicationBuilder builder)
    {
        builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));

        if (!string.IsNullOrWhiteSpace(builder.Configuration["Email:Smtp:Host"]))
        {
            builder.Services.AddSingleton<IEmailSender<IdentityUser>, MailKitEmailSender>();
        }
        else if (builder.Environment.IsProduction())
        {
            throw new InvalidOperationException(
                "Kein SMTP-Server konfiguriert: Email__Smtp__Host und Email__From setzen (siehe README). "
                + "Ohne E-Mail-Versand kann sich in Produktion niemand registrieren.");
        }
        else
        {
            builder.Services.AddSingleton<IEmailSender<IdentityUser>, LoggingEmailSender>();
        }

        return builder;
    }
}
