using CleanArchitecture.Application;
using CleanArchitecture.Domain.IRepository;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
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
public sealed class ApiFactory(string? connectionString = null) : WebApplicationFactory<Program>
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
        builder.ConfigureLogging(logging => logging.AddProvider(Logs));

        if (connectionString is not null)
        {
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
}
