using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Tests;

/// <summary>Startet die API mit echter, migrierter PostgreSQL-Datenbank und meldet Nutzer über die echten Identity-Endpunkte an.</summary>
public static class RealApiHarness
{
    // Zur Laufzeit erzeugt (erfüllt die Identity-Passwortregeln), kein festes Passwort im Quelltext.
    private static readonly string Password = $"Pw{Guid.NewGuid():N}!aA1";

    public static async Task<ApiFactory> StartAsync(PostgresFixture postgres)
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using (var context = PostgresFixture.CreateContext(connectionString))
        {
            await context.Database.MigrateAsync();
        }

        return new ApiFactory(connectionString);
    }

    /// <summary>Registriert den Nutzer und liefert einen HTTP-Client mit dessen Bearer-Token.</summary>
    public static async Task<HttpClient> SignInAsync(ApiFactory factory, string email)
    {
        var http = factory.CreateClient();
        (await http.PostAsJsonAsync("/api/auth/register", new { email, password = Password })).EnsureSuccessStatusCode();
        var login = await http.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        login.EnsureSuccessStatusCode();
        var tokens = await login.Content.ReadFromJsonAsync<Tokens>();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        return http;
    }

    private sealed record Tokens(string AccessToken);
}
