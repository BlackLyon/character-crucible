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
    /// <summary>Keep in step with the postgres service in docker-compose.yml.</summary>
    private const string Image = "postgres:17-alpine";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Image).Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        // The run's token, so --timeout can stop a container that never becomes ready.
        var ct = TestContext.Current.CancellationToken;
        await _container.StartAsync(ct);
        await using var db = NewContext();
        await db.Database.MigrateAsync(ct);
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();

    /// <summary>A fresh context, so a reload is a genuine round-trip and not the change tracker.</summary>
    public CharacterCrucibleDbContext NewContext() => ContextFor(ConnectionString);

    /// <summary>A context configured as the app configures one. A null connection string is enough to read the model.</summary>
    internal static CharacterCrucibleDbContext ContextFor(string? connectionString)
    {
        var options = new DbContextOptionsBuilder<CharacterCrucibleDbContext>();
        options.UseCharacterCrucibleDatabase(connectionString);
        return new(options.Options);
    }
}
