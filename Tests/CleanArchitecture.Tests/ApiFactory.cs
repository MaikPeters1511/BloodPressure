using CleanArchitecture.Application;
using CleanArchitecture.Domain.IRepository;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace CleanArchitecture.Tests;

/// <summary>
/// Startet die API im Speicher. Ohne Connection String: Fake-Repository und Test-Anmeldung (Header <c>X-Test-User</c>).
/// Mit Connection String: echte Datenbank und echte Identity-Endpunkte.
/// </summary>
public sealed class ApiFactory(string? connectionString = null, IReadOnlyDictionary<string, string>? settings = null)
    : WebApplicationFactory<Program>
{
    public FakeBloodPressureRepository Repository { get; } = new();

    public CapturingLoggerProvider Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Nicht "Development": dort würde die Migration gegen Postgres laufen.
        builder.UseEnvironment("Testing");
        // UseSetting statt ConfigureAppConfiguration: Aspire liest den Connection String schon beim Registrieren in Program.cs.
        builder.UseSetting("ConnectionStrings:bloodpressuredb",
            connectionString ?? "Host=localhost;Database=unused;Username=unused;Password=unused");

        builder.UseSetting("Reminders:WorkerEnabled", "false");
        foreach (var (key, value) in settings ?? new Dictionary<string, string>())
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureLogging(logging => logging.AddProvider(Logs));

        if (connectionString is not null)
        {
            // Echtmodus: Die Anmeldung verlangt eine bestätigte E-Mail-Adresse. Damit die übrigen Tests nicht jedes Mal den
            // Link anklicken müssen, bestätigt dieser Sender die Adresse sofort. Die Bestätigungstests ersetzen ihn.
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEmailSender<IdentityUser>>();
                services.AddSingleton<IEmailSender<IdentityUser>, AutoConfirmEmailSender>();
            });
            return;
        }

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IBloodPressureRepository>();
            services.AddSingleton<IBloodPressureRepository>(Repository);
            services.RemoveAll<ILegacyDataClaimer>();
            services.AddSingleton<ILegacyDataClaimer, NoOpClaimer>();
            services.AddAuthentication(TestAuthHandler.Scheme)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Scheme, _ => { });
            services.Configure<AuthenticationOptions>(o =>
            {
                o.DefaultScheme = TestAuthHandler.Scheme;
                o.DefaultAuthenticateScheme = TestAuthHandler.Scheme;
                o.DefaultChallengeScheme = TestAuthHandler.Scheme;
            });
        });
    }

    /// <summary>Client, der sich als der angegebene Benutzer ausgibt (nur im Fake-Modus).</summary>
    public HttpClient CreateClientFor(string userId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.Header, userId);
        return client;
    }

    private sealed class NoOpClaimer : ILegacyDataClaimer
    {
        public Task ClaimForFirstUserAsync(string userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>Bestätigt die E-Mail-Adresse beim Versand sofort, als hätte der Nutzer den Link angeklickt.</summary>
    private sealed class AutoConfirmEmailSender(IServiceProvider services) : IEmailSender<IdentityUser>
    {
        public async Task SendConfirmationLinkAsync(IdentityUser user, string email, string confirmationLink)
        {
            using var scope = services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            if (await users.FindByIdAsync(user.Id) is { } stored)
            {
                stored.EmailConfirmed = true;
                await users.UpdateAsync(stored);
            }
        }

        public Task SendPasswordResetLinkAsync(IdentityUser user, string email, string resetLink) => Task.CompletedTask;

        public Task SendPasswordResetCodeAsync(IdentityUser user, string email, string resetCode) => Task.CompletedTask;
    }
}
