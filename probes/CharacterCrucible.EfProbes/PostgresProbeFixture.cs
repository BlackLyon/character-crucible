using CharacterCrucible.EfProbes.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Testcontainers.PostgreSql;

namespace CharacterCrucible.EfProbes;

/// <summary>One Postgres for every probe class, so adding a probe class adds no container start.</summary>
/// <remarks>
/// EnsureCreated rather than migrations: these probe EF's behaviour against stand-in types, not
/// our schema. The mapping tests in CoreApi.Tests are the ones that go through migrations.
/// </remarks>
public sealed class PostgresProbeFixture : IAsyncLifetime
{
    /// <summary>Keep in step with the postgres service in docker-compose.yml and CoreApi.Tests' fixture.</summary>
    private const string Image = "postgres:17-alpine";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Image).Build();

    /// <summary>
    /// Every command executed since the last Clear(). Safe only because the probes share one
    /// collection, which xUnit runs serially.
    /// </summary>
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
