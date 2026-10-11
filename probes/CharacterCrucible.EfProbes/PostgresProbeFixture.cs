using CharacterCrucible.EfProbes.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Testcontainers.PostgreSql;

namespace CharacterCrucible.EfProbes;

/// <summary>One Postgres for every probe class, so adding a probe class adds no container start.</summary>
public sealed class PostgresProbeFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    /// <summary>SQL of the most recent commands, for probes that assert on what EF sent.</summary>
    public List<string> Sql { get; } = [];

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = NewContext();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public ProbeDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ProbeDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .LogTo(Sql.Add, [RelationalEventId.CommandExecuted])
            .Options);
}

[CollectionDefinition(Name)]
public class ProbeCollection : ICollectionFixture<PostgresProbeFixture>
{
    public const string Name = "Postgres probes";
}
