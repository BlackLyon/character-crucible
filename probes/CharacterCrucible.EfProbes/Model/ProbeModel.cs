using Microsoft.EntityFrameworkCore;

namespace CharacterCrucible.EfProbes.Model;

/// <summary>A trait rating, as notes/domain/characters.md specifies it.</summary>
public record TraitValue(string TraitKey, int Rating);

/// <summary>An acquired ability, as notes/domain/characters.md specifies it.</summary>
public record HeldAbility(string AbilityKey, DateTimeOffset AcquiredDate);

/// <summary>
/// Stand-in for Character, carrying only what the probes touch. Not the entity, and not a model
/// for its methods: the mutators here exist to drive EF, not to state domain rules.
/// </summary>
public class ProbeCharacter
{
    public ProbeCharacter(string name) => Name = name;

    private ProbeCharacter() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;

    /// <summary>Postgres xmin, which the database bumps on every row write.</summary>
    public uint Version { get; private set; }

    private IList<TraitValue> TraitValues { get; set; } = [];
    public IReadOnlyCollection<TraitValue> Traits => TraitValues.AsReadOnly();

    private IList<HeldAbility> AbilityValues { get; set; } = [];
    public IReadOnlyCollection<HeldAbility> Abilities => AbilityValues.AsReadOnly();

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

    public void AddAbility(string key, DateTimeOffset acquired) => AbilityValues.Add(new HeldAbility(key, acquired));
}

/// <summary>The same stand-in with an app-managed version, the alternative to xmin that was weighed.</summary>
public class CountedCharacter
{
    public CountedCharacter(string name) => Name = name;

    private CountedCharacter() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public int Version { get; private set; }

    private IList<TraitValue> TraitValues { get; set; } = [];
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
            e.ToTable("character");
            e.Property(c => c.Version).IsRowVersion();
            e.ComplexCollection<IList<TraitValue>, TraitValue>("TraitValues").ToJson("traits");
            e.ComplexCollection<IList<HeldAbility>, HeldAbility>("AbilityValues").ToJson("abilities");
            e.Ignore(c => c.Traits);
            e.Ignore(c => c.Abilities);
        });

        modelBuilder.Entity<CountedCharacter>(e =>
        {
            e.ToTable("counted_character");
            e.Property(c => c.Version).IsConcurrencyToken();
            e.ComplexCollection<IList<TraitValue>, TraitValue>("TraitValues").ToJson("traits");
            e.Ignore(c => c.Traits);
        });
    }
}
