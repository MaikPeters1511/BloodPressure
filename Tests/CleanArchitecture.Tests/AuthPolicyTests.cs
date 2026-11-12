using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Tests;

/// <summary>Belege zu Passwort-, Token- und Lockout-Regeln (OWASP-Prüfung Epic 1, R-01 und R-03) gegen PostgreSQL.</summary>
[Category("Integration")]
[ClassDataSource<PostgresFixture>(Shared = SharedType.PerTestSession)]
public class AuthPolicyTests(PostgresFixture postgres)
{
    // Zur Laufzeit erzeugt, kein festes Passwort im Quelltext.
    private static readonly string Password = $"Pw{Guid.NewGuid():N}!aA1";

    private async Task<ApiFactory> StartAsync()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using (var context = PostgresFixture.CreateContext(connectionString))
        {
            await context.Database.MigrateAsync();
        }

        return new ApiFactory(connectionString);
    }

    [Test]
    public async Task Register_TooShortPassword_IsRejected()
    {
        await using var factory = await StartAsync();

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register", new { email = "short@example.test", password = "Aa1!aaaa" });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains("PasswordTooShort");
    }

    [Test]
    public async Task Login_ReturnsConfiguredAccessTokenLifetime()
    {
        await using var factory = await StartAsync();
        var http = factory.CreateClient();
        (await http.PostAsJsonAsync("/api/auth/register", new { email = "ttl@example.test", password = Password })).EnsureSuccessStatusCode();

        var login = await http.PostAsJsonAsync("/api/auth/login", new { email = "ttl@example.test", password = Password });
        var expiresIn = JsonDocument.Parse(await login.Content.ReadAsStringAsync()).RootElement.GetProperty("expiresIn").GetInt32();

        await Assert.That(expiresIn).IsEqualTo(30 * 60);
    }

    [Test]
    public async Task Login_AfterFiveFailedAttempts_LocksAccountEvenWithCorrectPassword()
    {
        await using var factory = await StartAsync();
        var http = factory.CreateClient();
        (await http.PostAsJsonAsync("/api/auth/register", new { email = "lock@example.test", password = Password })).EnsureSuccessStatusCode();

        for (var i = 0; i < 5; i++)
        {
            var failed = await http.PostAsJsonAsync("/api/auth/login", new { email = "lock@example.test", password = "wrong" });
            await Assert.That(failed.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        }

        var locked = await http.PostAsJsonAsync("/api/auth/login", new { email = "lock@example.test", password = Password });

        await Assert.That(locked.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(await locked.Content.ReadAsStringAsync()).Contains("LockedOut");
    }

    [Test]
    public async Task Logout_InvalidatesTheRefreshToken()
    {
        await using var factory = await StartAsync();
        var http = factory.CreateClient();
        (await http.PostAsJsonAsync("/api/auth/register", new { email = "bye@example.test", password = Password })).EnsureSuccessStatusCode();
        var login = await http.PostAsJsonAsync("/api/auth/login", new { email = "bye@example.test", password = Password });
        var tokens = JsonDocument.Parse(await login.Content.ReadAsStringAsync()).RootElement;
        var refreshToken = tokens.GetProperty("refreshToken").GetString();
        var beforeLogout = await http.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });
        await Assert.That(beforeLogout.StatusCode).IsEqualTo(HttpStatusCode.OK);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.GetProperty("accessToken").GetString());
        var logout = await http.SendAsync(request);
        var afterLogout = await http.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });

        await Assert.That(logout.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(afterLogout.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Logout_WithoutToken_Returns401()
    {
        await using var factory = await StartAsync();

        var response = await factory.CreateClient().PostAsync("/api/auth/logout", content: null);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }
}
