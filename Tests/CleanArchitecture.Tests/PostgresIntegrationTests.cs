using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.IRepository;
using CleanArchitecture.Infrastructure.Repository;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Tests;

[Category("Integration")]
[ClassDataSource<PostgresFixture>(Shared = SharedType.PerTestSession)]
public class PostgresIntegrationTests(PostgresFixture postgres)
{
    private static readonly DateTime Measured = new(2026, 11, 4, 10, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task Migrations_OnEmptyDatabase_ApplyWithoutError()
    {
        await using var context = PostgresFixture.CreateContext(await postgres.CreateDatabaseAsync());

        await context.Database.MigrateAsync();

        await Assert.That(await context.Database.GetPendingMigrationsAsync()).IsEmpty();
    }

    [Test]
    public async Task Crud_AgainstRealDatabase_ReturnsWrittenValues()
    {
        await using var context = PostgresFixture.CreateContext(await postgres.CreateDatabaseAsync());
        await context.Database.MigrateAsync();
        var repository = new BloodPressureRepository(context);

        var entity = new BloodPressure { UserId = "u1", Systolisch = 120, Diastolisch = 80, DateTime = Measured, Pulse = 65, Oxygen = 98 };
        await repository.AddAsync(entity);
        var loaded = await repository.GetByIdAsync(entity.Id, "u1");
        loaded!.Systolisch = 130;
        await repository.UpdateAsync(loaded);
        var updated = (await repository.GetPageAsync("u1", new BloodPressureFilter(null, null, 1, 50, false))).Items;
        await repository.DeleteAsync(loaded);

        await Assert.That(updated.Single().Systolisch).IsEqualTo(130);
        await Assert.That(updated.Single().DateTime).IsEqualTo(Measured);
        await Assert.That(await repository.GetByIdAsync(entity.Id, "u1")).IsNull();
    }

    [Test]
    public async Task Model_HasNoPendingChanges_WithoutMigration()
    {
        // Schlägt fehl, wenn das EF-Modell geändert wurde, ohne eine Migration zu erzeugen.
        await using var context = PostgresFixture.CreateContext(await postgres.CreateDatabaseAsync());

        await Assert.That(context.Database.HasPendingModelChanges()).IsFalse();
    }

    [Test]
    public async Task DailyAggregates_GroupByUtcDay_AgainstRealDatabase()
    {
        await using var context = PostgresFixture.CreateContext(await postgres.CreateDatabaseAsync());
        await context.Database.MigrateAsync();
        var repository = new BloodPressureRepository(context);
        foreach (var (time, systolic, diastolic, pulse) in new[]
        {
            (new DateTime(2026, 10, 1, 23, 30, 0, DateTimeKind.Utc), 120, 80, 60),
            (new DateTime(2026, 10, 2, 0, 30, 0, DateTimeKind.Utc), 130, 90, 70),
            (new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc), 140, 100, 80)
        })
        {
            await repository.AddAsync(new BloodPressure
            {
                UserId = "u1", Systolisch = systolic, Diastolisch = diastolic, DateTime = time, Pulse = pulse, Oxygen = 98
            });
        }

        var days = await repository.GetDailyAggregatesAsync("u1", null, null);

        await Assert.That(days.Count).IsEqualTo(2);
        await Assert.That(days[0].Day).IsEqualTo(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        await Assert.That(days[0].Count).IsEqualTo(1);
        await Assert.That(days[1].Count).IsEqualTo(2);
        await Assert.That(days[1].SumSystolisch).IsEqualTo(270L);
        await Assert.That(days[1].MinDiastolisch).IsEqualTo(90);
        await Assert.That(days[1].MaxPulse).IsEqualTo(80);
    }
}
