using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CleanArchitecture.Contracts;
using CleanArchitecture.Infrastructure.Persistence.PostgreSql;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Refit;

namespace CleanArchitecture.Tests;

/// <summary>Echte Registrierung, Anmeldung und Mandantentrennung gegen PostgreSQL.</summary>
[Category("Integration")]
[ClassDataSource<PostgresFixture>(Shared = SharedType.PerTestSession)]
public class AuthIntegrationTests(PostgresFixture postgres)
{
    // Zur Laufzeit erzeugt (erfüllt die Identity-Passwortregeln), kein festes Passwort im Quelltext.
    private static readonly string Password = $"Pw{Guid.NewGuid():N}!aA1";
    private static readonly DateTime Measured = new(2026, 11, 4, 10, 0, 0, DateTimeKind.Utc);
    private static readonly BloodPressureRequest Request = new(120, 80, Measured, 65, 98);

    private async Task<(ApiFactory Factory, string ConnectionString)> StartAsync()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using (var context = PostgresFixture.CreateContext(connectionString))
        {
            await context.Database.MigrateAsync();
        }

        return (new ApiFactory(connectionString), connectionString);
    }

    private static async Task<IBloodPressureApi> SignInAsync(ApiFactory factory, string email)
    {
        var http = factory.CreateClient();
        (await http.PostAsJsonAsync("/api/auth/register", new { email, password = Password })).EnsureSuccessStatusCode();
        var login = await http.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        login.EnsureSuccessStatusCode();
        var tokens = await login.Content.ReadFromJsonAsync<LoginResponse>();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        return RestService.For<IBloodPressureApi>(http);
    }

    [Test]
    public async Task Request_WithoutToken_Returns401()
    {
        var (factory, _) = await StartAsync();
        await using var _ = factory;

        var response = await factory.CreateClient().GetAsync("/api/v1/BloodPressure");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Login_RegisteredUser_ReceivesAccessAndRefreshToken()
    {
        var (factory, _) = await StartAsync();
        await using var _ = factory;
        var http = factory.CreateClient();
        await http.PostAsJsonAsync("/api/auth/register", new { email = "a@example.test", password = Password });

        var login = await http.PostAsJsonAsync("/api/auth/login", new { email = "a@example.test", password = Password });
        var tokens = await login.Content.ReadFromJsonAsync<LoginResponse>();

        await Assert.That(login.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(tokens!.AccessToken).IsNotNull().And.IsNotEmpty();
        await Assert.That(tokens.RefreshToken).IsNotNull().And.IsNotEmpty();
    }

    [Test]
    public async Task OtherUser_CannotReadOrListForeignMeasurement()
    {
        var (factory, _) = await StartAsync();
        await using var _ = factory;
        var userA = await SignInAsync(factory, "a@example.test");
        var userB = await SignInAsync(factory, "b@example.test");
        var created = await userA.CreateAsync(Request);

        var ex = await Assert.That(async () => await userB.GetByIdAsync(created.Id)).Throws<ApiException>();
        var listB = await userB.GetAllAsync();
        var listA = await userA.GetAllAsync();

        await Assert.That((int)ex!.StatusCode).IsEqualTo(404);
        await Assert.That(listB.Items).IsEmpty();
        await Assert.That(listA.TotalCount).IsEqualTo(1);
    }

    [Test]
    public async Task OtherUser_CannotUpdateOrDeleteForeignMeasurement()
    {
        var (factory, _) = await StartAsync();
        await using var _ = factory;
        var userA = await SignInAsync(factory, "a@example.test");
        var userB = await SignInAsync(factory, "b@example.test");
        var created = await userA.CreateAsync(Request);

        var update = await Assert.That(async () => await userB.UpdateAsync(created.Id, Request)).Throws<ApiException>();
        var delete = await Assert.That(async () => await userB.DeleteAsync(created.Id)).Throws<ApiException>();

        await Assert.That((int)update!.StatusCode).IsEqualTo(404);
        await Assert.That((int)delete!.StatusCode).IsEqualTo(404);
        await Assert.That((await userA.GetAllAsync()).TotalCount).IsEqualTo(1);
    }

    [Test]
    public async Task LegacyMeasurements_AreAssignedToFirstUserOnly()
    {
        var (factory, connectionString) = await StartAsync();
        await using var _ = factory;
        await using (var context = PostgresFixture.CreateContext(connectionString))
        {
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"BloodPressure\" (\"Systolisch\",\"Diastolisch\",\"DateTime\",\"Pulse\",\"Oxygen\") " +
                "VALUES (118, 76, '2026-01-01T08:00:00Z', 60, 97)");
        }

        // Die Altdaten gehen an den ersten Nutzer, der seine E-Mail-Adresse bestätigt (Link aus der Mail anklicken).
        var mails = new CapturingEmailSender();
        var withMails = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender<IdentityUser>>();
            services.AddSingleton<IEmailSender<IdentityUser>>(mails);
        }));
        var first = await SignInWithLinkAsync(withMails.CreateClient(), mails, "first@example.test");
        var second = await SignInWithLinkAsync(withMails.CreateClient(), mails, "second@example.test");

        await Assert.That((await first.GetAllAsync()).Items.Single().Systolisch).IsEqualTo(118);
        await Assert.That((await second.GetAllAsync()).Items).IsEmpty();
    }

    private static async Task<IBloodPressureApi> SignInWithLinkAsync(HttpClient http, CapturingEmailSender mails, string email)
    {
        (await http.PostAsJsonAsync("/api/auth/register", new { email, password = Password })).EnsureSuccessStatusCode();
        var link = new Uri(System.Net.WebUtility.HtmlDecode(mails.Confirmations.Last(c => c.Email == email).Link));
        (await http.GetAsync(link.PathAndQuery)).EnsureSuccessStatusCode();
        var login = await http.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        login.EnsureSuccessStatusCode();
        var tokens = await login.Content.ReadFromJsonAsync<LoginResponse>();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        return RestService.For<IBloodPressureApi>(http);
    }

    [Test]
    public async Task FromTo_WithTimeZoneOffsets_FiltersCorrectlyAgainstPostgres()
    {
        var (factory, _) = await StartAsync();
        await using var _ = factory;
        var user = await SignInAsync(factory, "a@example.test");
        foreach (var day in new[] { 1, 5, 10 })
        {
            await user.CreateAsync(new BloodPressureRequest(120, 80, new DateTime(2026, 10, day, 8, 0, 0, DateTimeKind.Utc), 65, 98));
        }

        // Lokale Zeiten mit Offset werden als UTC-Zeitpunkte verstanden.
        var result = await user.GetAllAsync(
            from: new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc).ToLocalTime(),
            to: new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc).ToLocalTime());

        await Assert.That(result.TotalCount).IsEqualTo(1);
        await Assert.That(result.Items[0].DateTime).IsEqualTo(new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public async Task PagedQuery_With100000Rows_UsesIndexAndIsFast()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var context = PostgresFixture.CreateContext(connectionString);
        await context.Database.MigrateAsync();
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"BloodPressure\" (\"UserId\",\"Systolisch\",\"Diastolisch\",\"DateTime\",\"Pulse\",\"Oxygen\") " +
            "SELECT 'u1', 120, 80, TIMESTAMPTZ '2020-01-01' + (g * INTERVAL '1 minute'), 60, 98 FROM generate_series(1, 100000) g");
        await context.Database.ExecuteSqlRawAsync("ANALYZE \"BloodPressure\"");
        var repository = new Infrastructure.Repository.BloodPressureRepository(context);

        var started = System.Diagnostics.Stopwatch.StartNew();
        var page = await repository.GetPageAsync("u1", new Domain.IRepository.BloodPressureFilter(null, null, 1, 50, true));
        started.Stop();
        var plan = string.Join('\n', await context.Database
            .SqlQueryRaw<string>("EXPLAIN SELECT * FROM \"BloodPressure\" WHERE \"UserId\" = 'u1' ORDER BY \"DateTime\" DESC LIMIT 50")
            .ToListAsync());

        await Assert.That(page.TotalCount).IsEqualTo(100000);
        await Assert.That(page.Items.Count).IsEqualTo(50);
        await Assert.That(plan).Contains("IX_BloodPressure_UserId_DateTime");
        await Assert.That(started.ElapsedMilliseconds).IsLessThan(5000); // grobe Schutzgrenze; der Nachweis ist der Plan (Index)
    }

    [Test]
    public async Task Migration_OnDatabaseWithLegacyData_KeepsRowsWithoutOwner()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var context = PostgresFixture.CreateContext(connectionString);
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync("20240330114350_Initial");
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"BloodPressure\" (\"Systolisch\",\"Diastolisch\",\"DateTime\",\"Pulse\",\"Oxygen\") " +
            "VALUES (118, 76, '2026-01-01T08:00:00Z', 60, 97)");

        await context.Database.MigrateAsync();

        var row = await context.BloodPressures.SingleAsync();
        await Assert.That(row.Systolisch).IsEqualTo(118);
        await Assert.That(row.UserId).IsNull();
    }

    private sealed record LoginResponse(string AccessToken, string RefreshToken);
}
