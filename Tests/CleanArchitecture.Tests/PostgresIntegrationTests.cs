using CleanArchitecture.Domain.Entities;
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
        var updated = await repository.GetAllAsync("u1");
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
}
