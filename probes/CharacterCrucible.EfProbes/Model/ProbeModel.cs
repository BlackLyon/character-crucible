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
    public uint RowVersion { get; private set; }

    private readonly IList<TraitValue> _traits = [];
    public IReadOnlyCollection<TraitValue> Traits => _traits.AsReadOnly();

    private readonly IList<HeldAbility> _abilities = [];
    public IReadOnlyCollection<HeldAbility> Abilities => _abilities.AsReadOnly();

    public void SetTrait(string key, int rating)
    {
        var index = _traits.ToList().FindIndex(t => t.TraitKey == key);
        if (index < 0)
        {
            _traits.Add(new TraitValue(key, rating));
        }
        else
        {
            _traits[index] = new TraitValue(key, rating);
        }
    }

    public void AddAbility(string key, DateTimeOffset acquired) => _abilities.Add(new HeldAbility(key, acquired));

    public int TotalEarnedXp { get; private set; }
    public int UnspentXp { get; private set; }

    // Write-only: no getter, so "not loaded" can never be read as "no history". History is a query.
    private readonly IList<ProbeLedgerEntry> _xpLedgerEntries = [];

    public void AwardXp(int amount)
    {
        TotalEarnedXp += amount;
        UnspentXp += amount;
        _xpLedgerEntries.Add(new ProbeLedgerEntry(amount, TotalEarnedXp, UnspentXp));
    }

    public void SpendXp(int amount)
    {
        UnspentXp -= amount;
        _xpLedgerEntries.Add(new ProbeLedgerEntry(-amount, TotalEarnedXp, UnspentXp));
    }
}

/// <summary>Stand-in for XpLedgerEntry: one row per XP transaction, with running balances.</summary>
public class ProbeLedgerEntry
{
    public ProbeLedgerEntry(int delta, int totalEarnedAfter, int unspentAfter)
    {
        Delta = delta;
        TotalEarnedAfter = totalEarnedAfter;
        UnspentAfter = unspentAfter;
    }

    private ProbeLedgerEntry() { }

    public Guid Id { get; private set; }
    public Guid CharacterId { get; private set; }
    public int Delta { get; private set; }
    public int TotalEarnedAfter { get; private set; }
    public int UnspentAfter { get; private set; }
}

/// <summary>The same stand-in with an app-managed version, the alternative to xmin that was weighed.</summary>
public class CountedCharacter
{
    public CountedCharacter(string name) => Name = name;

    private CountedCharacter() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public int RowVersion { get; private set; }

    private readonly IList<TraitValue> _traits = [];
    public IReadOnlyCollection<TraitValue> Traits => _traits.AsReadOnly();

    public void SetTrait(string key, int rating)
    {
        _traits.Add(new TraitValue(key, rating));
        RowVersion++;
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
            e.Property(c => c.RowVersion).IsRowVersion();
            e.ComplexCollection<IList<TraitValue>, TraitValue>("_traits").ToJson("traits");
            e.ComplexCollection<IList<HeldAbility>, HeldAbility>("_abilities").ToJson("abilities");
            e.Ignore(c => c.Traits);
            e.Ignore(c => c.Abilities);
            e.HasMany<ProbeLedgerEntry>("_xpLedgerEntries").WithOne().HasForeignKey(l => l.CharacterId);
        });

        modelBuilder.Entity<ProbeLedgerEntry>().ToTable("xp_ledger_entry");

        modelBuilder.Entity<CountedCharacter>(e =>
        {
            e.ToTable("counted_character");
            e.Property(c => c.RowVersion).IsConcurrencyToken();
            e.ComplexCollection<IList<TraitValue>, TraitValue>("_traits").ToJson("traits");
            e.Ignore(c => c.Traits);
        });
    }
}
