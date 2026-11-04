using CleanArchitecture.Infrastructure.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using TUnit.Core.Interfaces;

namespace CleanArchitecture.Tests;

/// <summary>Ein PostgreSQL-Container pro Testlauf; jeder Test bekommt eine eigene, frisch migrierte Datenbank.</summary>
public sealed class PostgresFixture : IAsyncInitializer, IAsyncDisposable
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();

    public async Task InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Legt eine neue leere Datenbank an und liefert deren Connection String.</summary>
    public async Task<string> CreateDatabaseAsync()
    {
        var name = $"test_{Guid.NewGuid():N}";
        await _container.ExecScriptAsync($"CREATE DATABASE {name};");
        return new Npgsql.NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = name }.ConnectionString;
    }

    public static AddPostgresSqlDbContext CreateContext(string connectionString)
        => new(new DbContextOptionsBuilder<AddPostgresSqlDbContext>().UseNpgsql(connectionString).Options);
}
