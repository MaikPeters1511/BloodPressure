using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CleanArchitecture.Tests;

/// <summary>
/// Prüft die Migrationen auf einer Datenbank mit bereits vorhandenen Daten (nicht nur auf einer leeren): Messungen und Hinweise
/// im Stand vor der ESC-2024-Umstellung müssen erhalten bleiben und korrekt umgestellt werden.
/// </summary>
[Category("Integration")]
[ClassDataSource<PostgresFixture>(Shared = SharedType.PerTestSession)]
public class MigrationDataTests(PostgresFixture postgres)
{
    private const string BeforeEsc = "20261104155546_AddBloodPressureRowVersion";

    private static async Task<string?> ScalarAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (await command.ExecuteScalarAsync())?.ToString();
    }

    private static async Task SeedOldDataAsync(DbContext context)
    {
        await context.Database.ExecuteSqlRawAsync(@"INSERT INTO ""BloodPressure"" (""Systolisch"",""Diastolisch"",""DateTime"",""Pulse"",""Oxygen"",""UserId"")
            VALUES (185, 115, '2026-10-01T08:00:00Z', 65, 98, 'user-a');");
        await context.Database.ExecuteSqlRawAsync(@"INSERT INTO ""Notification"" (""Id"",""UserId"",""Type"",""Category"",""Message"",""CreatedAt"",""ReadAt"") VALUES
            ('00000000-0000-0000-0000-000000000001','user-a','CriticalReading','Grade3','Eine Messung liegt in der Kategorie Hypertonie Grad 3. Das ist keine Diagnose. Bitte bei Beschwerden oder anhaltend hohen Werten ärztlichen Rat einholen.','2026-10-01T08:00:01Z',NULL),
            ('00000000-0000-0000-0000-000000000002','user-a','CriticalReading','Grade3','Eine Messung liegt in der Kategorie Hypertonie Grad 3. Das ist keine Diagnose. Bitte bei Beschwerden oder anhaltend hohen Werten ärztlichen Rat einholen.','2026-10-02T08:00:01Z','2026-10-02T09:00:00Z'),
            ('00000000-0000-0000-0000-000000000003','user-b','MeasurementReminder',NULL,'Zeit für deine Blutdruckmessung.','2026-10-03T08:00:00Z',NULL);");
        await context.Database.ExecuteSqlRawAsync(@"INSERT INTO ""Reminder"" (""Id"",""UserId"",""TimeOfDay"",""TimeZoneId"") VALUES
            ('00000000-0000-0000-0000-0000000000a1','user-a','08:00','Europe/Berlin');");
    }

    [Test]
    public async Task ExistingData_SurvivesTheLatestMigrations_AndNoticesAreConverted()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var context = PostgresFixture.CreateContext(connectionString);
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(BeforeEsc);
        await SeedOldDataAsync(context);

        await migrator.MigrateAsync();

        // Hinweise: Kategorie und Text umgestellt, Lesestatus und Zuordnung unverändert.
        await Assert.That(await ScalarAsync(connectionString, @"SELECT COUNT(*) FROM ""Notification"" WHERE ""Category"" = 'Hypertension'")).IsEqualTo("2");
        await Assert.That(await ScalarAsync(connectionString, @"SELECT COUNT(*) FROM ""Notification"" WHERE ""Category"" LIKE 'Grade%'")).IsEqualTo("0");
        await Assert.That(await ScalarAsync(connectionString, @"SELECT ""Message"" FROM ""Notification"" WHERE ""Id"" = '00000000-0000-0000-0000-000000000001'"))
            .Contains("stark erhöhten Blutdrucks");
        await Assert.That(await ScalarAsync(connectionString, @"SELECT ""ReadAt"" IS NOT NULL FROM ""Notification"" WHERE ""Id"" = '00000000-0000-0000-0000-000000000002'")).IsEqualTo("True");

        // Nicht betroffene Daten bleiben unverändert.
        await Assert.That(await ScalarAsync(connectionString, @"SELECT ""Message"" FROM ""Notification"" WHERE ""Id"" = '00000000-0000-0000-0000-000000000003'")).IsEqualTo("Zeit für deine Blutdruckmessung.");
        await Assert.That(await ScalarAsync(connectionString, @"SELECT ""Category"" IS NULL FROM ""Notification"" WHERE ""Id"" = '00000000-0000-0000-0000-000000000003'")).IsEqualTo("True");
        await Assert.That(await ScalarAsync(connectionString, @"SELECT COUNT(*) FROM ""BloodPressure""")).IsEqualTo("1");
        await Assert.That(await ScalarAsync(connectionString, @"SELECT COUNT(*) FROM ""Reminder""")).IsEqualTo("1");

        // Neue Tabelle ist da und leer.
        await Assert.That(await ScalarAsync(connectionString, @"SELECT COUNT(*) FROM ""UserProfile""")).IsEqualTo("0");
    }

    [Test]
    public async Task Rollback_RestoresTheOldNoticeCategory_AndTheProfileTableIsRemoved()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var context = PostgresFixture.CreateContext(connectionString);
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(BeforeEsc);
        await SeedOldDataAsync(context);
        await migrator.MigrateAsync();

        await migrator.MigrateAsync(BeforeEsc);

        await Assert.That(await ScalarAsync(connectionString, @"SELECT COUNT(*) FROM ""Notification"" WHERE ""Category"" = 'Grade3'")).IsEqualTo("2");
        await Assert.That(await ScalarAsync(connectionString, @"SELECT COUNT(*) FROM ""BloodPressure""")).IsEqualTo("1");
        await Assert.That(await ScalarAsync(connectionString, @"SELECT to_regclass('""UserProfile""') IS NULL")).IsEqualTo("True");
    }

    [Test]
    public async Task Migrating_Twice_IsANoOp()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var context = PostgresFixture.CreateContext(connectionString);
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(BeforeEsc);
        await SeedOldDataAsync(context);
        await migrator.MigrateAsync();

        await migrator.MigrateAsync();

        await Assert.That(await ScalarAsync(connectionString, @"SELECT COUNT(*) FROM ""Notification"" WHERE ""Category"" = 'Hypertension'")).IsEqualTo("2");
    }
}
