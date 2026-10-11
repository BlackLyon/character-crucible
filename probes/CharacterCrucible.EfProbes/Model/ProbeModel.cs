using Microsoft.EntityFrameworkCore;

namespace CharacterCrucible.EfProbes.Model;

/// <summary>A trait rating, as notes/domain/characters.md specifies it.</summary>
public record TraitValue(string TraitKey, int Rating);

/// <summary>
/// Stand-in for Character, carrying only what the probes touch. Not the entity: the real one is
/// written in the Characters module, and this follows it.
/// </summary>
public class ProbeCharacter
{
    public ProbeCharacter(string name) => Name = name;

    private ProbeCharacter() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;

    /// <summary>Postgres xmin, which the database bumps on every row write.</summary>
    public uint Version { get; private set; }

    private IList<TraitValue> TraitValues { get; set; } = new List<TraitValue>();
    public IReadOnlyCollection<TraitValue> Traits => TraitValues.AsReadOnly();

    /// <summary>Replaces the entry for the key rather than mutating it.</summary>
    public void SetTrait(string key, int rating)
    {
        var index = TraitValues.ToList().FindIndex(t => t.TraitKey == key);
        if (index < 0)
        {
            TraitValues.Add(new TraitValue(key, rating));
        }
        else
        {
            TraitValues[index] = new TraitValue(key, rating);
        }
    }
}

/// <summary>The same stand-in with an app-managed version, the other option the spec allows.</summary>
public class CountedCharacter
{
    public CountedCharacter(string name) => Name = name;

    private CountedCharacter() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public int Version { get; private set; }

    private IList<TraitValue> TraitValues { get; set; } = new List<TraitValue>();
    public IReadOnlyCollection<TraitValue> Traits => TraitValues.AsReadOnly();

    public void SetTrait(string key, int rating)
    {
        TraitValues.Add(new TraitValue(key, rating));
        Version++;
    }
}

public class ProbeDbContext(DbContextOptions<ProbeDbContext> options) : DbContext(options)
{
    public DbSet<ProbeCharacter> Characters => Set<ProbeCharacter>();
    public DbSet<CountedCharacter> CountedCharacters => Set<CountedCharacter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("probes");

        modelBuilder.Entity<ProbeCharacter>(e =>
        {
            e.Property(c => c.Version).IsRowVersion();
            e.ComplexCollection<IList<TraitValue>, TraitValue>("TraitValues").ToJson();
            e.Ignore(c => c.Traits);
        });

        modelBuilder.Entity<CountedCharacter>(e =>
        {
            e.Property(c => c.Version).IsConcurrencyToken();
            e.ComplexCollection<IList<TraitValue>, TraitValue>("TraitValues").ToJson();
            e.Ignore(c => c.Traits);
        });
    }
}
