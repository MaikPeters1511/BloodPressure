using System.Net;
using System.Net.Http.Json;
using CleanArchitecture.Application;
using CleanArchitecture.Infrastructure;
using CleanArchitecture.Infrastructure.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace CleanArchitecture.Tests;

/// <summary>Phase 1 der ADR 0006: Registrierung nur mit bestätigter E-Mail-Adresse (gegen PostgreSQL).</summary>
[Category("Integration")]
[ClassDataSource<PostgresFixture>(Shared = SharedType.PerTestSession)]
public class EmailConfirmationTests(PostgresFixture postgres)
{
    // Zur Laufzeit erzeugt, kein festes Passwort im Quelltext.
    private static readonly string Password = $"Pw{Guid.NewGuid():N}!aA1";

    private sealed class RecordingClaimer : ILegacyDataClaimer
    {
        public List<string> Claimed { get; } = [];

        public Task ClaimForFirstUserAsync(string userId, CancellationToken cancellationToken = default)
        {
            lock (Claimed)
            {
                Claimed.Add(userId);
            }

            return Task.CompletedTask;
        }
    }

    private async Task<(HttpClient Http, CapturingEmailSender Mails, RecordingClaimer Claimer, IAsyncDisposable Disposable)> StartAsync()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using (var context = PostgresFixture.CreateContext(connectionString))
        {
            await context.Database.MigrateAsync();
        }

        var mails = new CapturingEmailSender();
        var claimer = new RecordingClaimer();
        var factory = new ApiFactory(connectionString);
        var http = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender<IdentityUser>>();
            services.AddSingleton<IEmailSender<IdentityUser>>(mails);
            services.RemoveAll<ILegacyDataClaimer>();
            services.AddSingleton<ILegacyDataClaimer>(claimer);
        })).CreateClient();
        return (http, mails, claimer, factory);
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient http, string email, string password)
        => http.PostAsJsonAsync("/api/auth/login", new { email, password });

    private static async Task<(string UserId, string Code, string PathAndQuery)> ParseLinkAsync(string link)
    {
        // MapIdentityApi übergibt den Link HTML-kodiert; der echte Sender dekodiert ihn ebenfalls.
        var uri = new Uri(System.Net.WebUtility.HtmlDecode(link));
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
        await Task.CompletedTask;
        return (query["userId"].ToString(), query["code"].ToString(), uri.PathAndQuery);
    }

    [Test]
    public async Task Register_DoesNotAllowLoginUntilTheEmailIsConfirmed()
    {
        var (http, mails, _, disposable) = await StartAsync();
        await using var _ = disposable;

        (await http.PostAsJsonAsync("/api/auth/register", new { email = "new@example.test", password = Password })).EnsureSuccessStatusCode();
        var login = await LoginAsync(http, "new@example.test", Password);

        await Assert.That(login.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(await login.Content.ReadAsStringAsync()).Contains("NotAllowed");
        await Assert.That(mails.Confirmations.Count).IsEqualTo(1);
        await Assert.That(mails.Confirmations[0].Email).IsEqualTo("new@example.test");
    }

    [Test]
    public async Task Login_UnconfirmedAccountWithWrongPassword_LooksLikeAnUnknownAccount()
    {
        var (http, _, _, disposable) = await StartAsync();
        await using var _ = disposable;
        (await http.PostAsJsonAsync("/api/auth/register", new { email = "known@example.test", password = Password })).EnsureSuccessStatusCode();

        var unconfirmedWrong = await LoginAsync(http, "known@example.test", "falsch");
        var unknown = await LoginAsync(http, "unknown@example.test", "falsch");

        await Assert.That(unconfirmedWrong.StatusCode).IsEqualTo(unknown.StatusCode);
        var unconfirmedBody = await unconfirmedWrong.Content.ReadAsStringAsync();
        await Assert.That(unconfirmedBody).Contains("Failed");
        await Assert.That(unconfirmedBody).DoesNotContain("NotAllowed");
        await Assert.That(await unknown.Content.ReadAsStringAsync()).Contains("Failed");
    }

    [Test]
    public async Task ConfirmEmail_WithTheLinkFromTheMail_AllowsLogin()
    {
        var (http, mails, _, disposable) = await StartAsync();
        await using var _ = disposable;
        (await http.PostAsJsonAsync("/api/auth/register", new { email = "ok@example.test", password = Password })).EnsureSuccessStatusCode();
        var (_, _, pathAndQuery) = await ParseLinkAsync(mails.Confirmations[0].Link);

        var confirm = await http.GetAsync(pathAndQuery);
        var login = await LoginAsync(http, "ok@example.test", Password);

        await Assert.That(confirm.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(login.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task ConfirmEmail_WithAnInvalidCode_IsRejected()
    {
        var (http, mails, _, disposable) = await StartAsync();
        await using var _ = disposable;
        (await http.PostAsJsonAsync("/api/auth/register", new { email = "bad@example.test", password = Password })).EnsureSuccessStatusCode();
        var (userId, _, _) = await ParseLinkAsync(mails.Confirmations[0].Link);

        var confirm = await http.GetAsync($"/api/auth/confirmEmail?userId={userId}&code=ungueltig");
        var login = await LoginAsync(http, "bad@example.test", Password);

        await Assert.That(confirm.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(login.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Resend_AnswersTheSameForKnownAndUnknownAddresses_AndMailsOnlyKnownOnes()
    {
        var (http, mails, _, disposable) = await StartAsync();
        await using var _ = disposable;
        (await http.PostAsJsonAsync("/api/auth/register", new { email = "again@example.test", password = Password })).EnsureSuccessStatusCode();

        var known = await http.PostAsJsonAsync("/api/auth/resendConfirmationEmail", new { email = "again@example.test" });
        var unknown = await http.PostAsJsonAsync("/api/auth/resendConfirmationEmail", new { email = "nobody@example.test" });

        await Assert.That(known.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(unknown.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(mails.Confirmations.Count).IsEqualTo(2);
        await Assert.That(mails.Confirmations.All(c => c.Email == "again@example.test")).IsTrue();
    }

    [Test]
    public async Task LegacyData_IsClaimedOnlyAfterTheEmailIsConfirmed()
    {
        var (http, mails, claimer, disposable) = await StartAsync();
        await using var _ = disposable;
        (await http.PostAsJsonAsync("/api/auth/register", new { email = "first@example.test", password = Password })).EnsureSuccessStatusCode();
        var claimedAfterRegister = claimer.Claimed.ToList();
        var (userId, _, pathAndQuery) = await ParseLinkAsync(mails.Confirmations[0].Link);

        (await http.GetAsync(pathAndQuery)).EnsureSuccessStatusCode();

        await Assert.That(claimedAfterRegister).IsEmpty();
        await Assert.That(claimer.Claimed).Contains(userId);
    }

    [Test]
    public async Task Migration_MarksExistingAccountsAsConfirmed()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var context = PostgresFixture.CreateContext(connectionString);
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync("20261105130000_AddUserProfile");
        await context.Database.ExecuteSqlRawAsync(@"INSERT INTO ""AspNetUsers"" (""Id"",""UserName"",""NormalizedUserName"",""Email"",""NormalizedEmail"",""EmailConfirmed"",""PhoneNumberConfirmed"",""TwoFactorEnabled"",""LockoutEnabled"",""AccessFailedCount"")
            VALUES ('old-user','old@example.test','OLD@EXAMPLE.TEST','old@example.test','OLD@EXAMPLE.TEST',FALSE,FALSE,FALSE,TRUE,0);");

        await migrator.MigrateAsync();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(@"SELECT ""EmailConfirmed"" FROM ""AspNetUsers"" WHERE ""Id"" = 'old-user'", connection);
        await Assert.That((bool)(await command.ExecuteScalarAsync())!).IsTrue();
    }
}

/// <summary>Auswahl und Konfiguration des E-Mail-Versands (ADR 0006, Phase 1). Ohne Datenbank.</summary>
[Category("Unit")]
public class EmailSendingConfigurationTests
{
    private static IHostApplicationBuilder CreateBuilder(string environment, Dictionary<string, string?>? settings = null)
    {
        var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(new Microsoft.AspNetCore.Builder.WebApplicationOptions
        {
            EnvironmentName = environment
        });
        builder.Configuration.AddInMemoryCollection(settings ?? []);
        return builder;
    }

    [Test]
    public async Task Production_WithoutSmtpHost_FailsFastWithAClearMessage()
    {
        var builder = CreateBuilder(Environments.Production);

        var error = Record.Exception(() => builder.AddEmailSending());

        await Assert.That(error).IsTypeOf<InvalidOperationException>();
        await Assert.That(error!.Message).Contains("Email__Smtp__Host");
    }

    [Test]
    public async Task Development_WithoutSmtpHost_UsesTheLoggingSender()
    {
        var builder = CreateBuilder(Environments.Development);
        builder.AddEmailSending();

        using var provider = builder.Services.BuildServiceProvider();

        await Assert.That(provider.GetRequiredService<IEmailSender<IdentityUser>>()).IsTypeOf<LoggingEmailSender>();
    }

    [Test]
    public async Task WithSmtpHost_UsesMailKit()
    {
        var builder = CreateBuilder(Environments.Production, new Dictionary<string, string?>
        {
            ["Email:Smtp:Host"] = "smtp.example.test",
            ["Email:From"] = "noreply@example.test"
        });
        builder.AddEmailSending();

        using var provider = builder.Services.BuildServiceProvider();

        await Assert.That(provider.GetRequiredService<IEmailSender<IdentityUser>>()).IsTypeOf<MailKitEmailSender>();
    }

    [Test]
    public async Task ConfirmationLink_IsHtmlDecoded_BeforeItIsSent()
    {
        var link = MailKitEmailSender.BuildConfirmationLink(
            "https://api.example.test/api/auth/confirmEmail?userId=u1&amp;code=abc", null);

        await Assert.That(link).IsEqualTo("https://api.example.test/api/auth/confirmEmail?userId=u1&code=abc");
    }

    [Test]
    public async Task ConfirmationLink_PointsToTheConfiguredPage_WhenSet()
    {
        var link = MailKitEmailSender.BuildConfirmationLink(
            "https://api.example.test/api/auth/confirmEmail?userId=u1&code=abc%2Bdef", "https://app.example.test/confirm-email");

        await Assert.That(link).IsEqualTo("https://app.example.test/confirm-email?userId=u1&code=abc%2Bdef");
    }

    [Test]
    public async Task ConfirmationLink_StaysTheApiLink_WhenNoPageIsConfigured()
    {
        const string api = "https://api.example.test/api/auth/confirmEmail?userId=u1&code=abc";

        await Assert.That(MailKitEmailSender.BuildConfirmationLink(api, null)).IsEqualTo(api);
    }

    private static class Record
    {
        public static Exception? Exception(Action action)
        {
            try
            {
                action();
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }
    }
}
