using CharacterCrucible.CoreApi.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace CharacterCrucible.CoreApi.Tests.Persistence;

/// <summary>
/// A throwaway Postgres for the integration tests, migrated once and shared by the class.
/// </summary>
/// <remarks>
/// Migrations are applied rather than EnsureCreated, so the tests exercise the same SQL that
/// will run against a real database — including the partial unique index, which EnsureCreated
/// would also produce but which only the migration path proves is reproducible.
/// </remarks>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = NewContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>A fresh context, so a reload is a genuine round-trip and not the change tracker.</summary>
    public CharacterCrucibleDbContext NewContext() =>
        new(new DbContextOptionsBuilder<CharacterCrucibleDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options);
}
