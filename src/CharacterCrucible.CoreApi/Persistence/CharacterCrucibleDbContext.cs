using System.Reflection;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.Entities;
using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CharacterCrucible.CoreApi.Persistence;

/// <summary>
/// The Core API owns all persistence. One context across all modules, each module in its own
/// Postgres schema.
/// </summary>
/// <remarks>
/// One context rather than one per module because Character.RulesetVersionId points into the
/// Rulesets module, and separate contexts cannot express a cross-module foreign key — the
/// apply-time version check assumes that pointer is valid.
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
    public DbSet<Character> Characters => Set<Character>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        ConfigureCrossModuleForeignKeys(modelBuilder);
    }

    /// <summary>
    /// Database FKs from Characters into Rulesets. Here because neither module's configuration may
    /// name the other's types. The C# side stays a bare Guid with no navigation.
    /// </summary>
    private static void ConfigureCrossModuleForeignKeys(ModelBuilder modelBuilder)
    {
        // Restrict: a referenced version can't be deleted. Published ones never are; this stops a
        // draft being deleted out from under history.
        modelBuilder.Entity<Character>()
                    .HasOne<RulesetVersion>()
                    .WithMany()
                    .HasForeignKey(c => c.RulesetVersionId)
                    .OnDelete(DeleteBehavior.Restrict);

        // Named, because the default passes Postgres's 63-character limit and is truncated.
        modelBuilder.Entity<AdvancementRequest>()
                    .HasOne<RulesetVersion>()
                    .WithMany()
                    .HasForeignKey(r => r.EvaluatedRulesetVersionId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("fk_advancement_request_evaluated_ruleset_version");

        modelBuilder.Entity<AuditEntry>()
                    .HasOne<RulesetVersion>()
                    .WithMany()
                    .HasForeignKey(a => a.RulesetVersionId)
                    .OnDelete(DeleteBehavior.Restrict);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Npgsql throws rather than misbehaving quietly when a DateTime has the wrong Kind, so
        // the convention is settled once here instead of per property.
        configurationBuilder.Properties<DateTimeOffset>().HaveColumnType("timestamptz");
    }
}
