using System.Reflection;
using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CharacterCrucible.CoreApi.Persistence;

/// <summary>
/// The Core API owns all persistence. One context across all modules, each module in its own
/// Postgres schema.
/// </summary>
/// <remarks>
/// One context rather than one per module because the advancement apply flow writes Character,
/// XpLedgerEntry and AuditEntry in a single transaction, which spans two modules. Separate
/// contexts can share a transaction via a shared connection, but they cannot express a
/// cross-module foreign key — and the apply-time version check assumes that pointer is valid.
/// See notes/stage-1-ef-config.md for the measurements behind that.
/// <para>
/// This type is the one place allowed to depend on every module, so the architecture test's
/// module-boundary rule has to exempt the persistence layer rather than forbid all outside
/// references. Configurations live in each module's own Persistence folder and are discovered
/// below, so splitting into one context per module later stays mechanical.
/// </para>
/// </remarks>
public class CharacterCrucibleDbContext(DbContextOptions<CharacterCrucibleDbContext> options)
    : DbContext(options)
{
    public DbSet<Ruleset> Rulesets => Set<Ruleset>();
    public DbSet<RulesetVersion> RulesetVersions => Set<RulesetVersion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Npgsql throws rather than misbehaving quietly when a DateTime has the wrong Kind, so
        // the convention is settled once here instead of per property.
        configurationBuilder.Properties<DateTimeOffset>().HaveColumnType("timestamptz");
    }
}
