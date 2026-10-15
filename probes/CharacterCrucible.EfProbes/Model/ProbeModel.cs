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

    /// <summary>Written on every submission so the Character row changes and RowVersion guards the limit.</summary>
    public DateTimeOffset? LastRequestSubmittedAt { get; private set; }

    // Loaded filtered to in-flight requests only; never the request history.
    private readonly IList<ProbeRequest> _requests = [];

    public ProbeRequest RequestAdvancement(string targetKey, DateTimeOffset now)
    {
        var inFlight = _requests.Where(r => r.Status == ProbeRequestStatus.Queued).ToList();
        if (inFlight.Count >= 2)
        {
            throw new InvalidOperationException("At most two requests in flight.");
        }
        if (inFlight.Any(r => r.TargetKey == targetKey))
        {
            throw new InvalidOperationException("A request for this item is already in flight.");
        }

        var request = new ProbeRequest(targetKey);
        _requests.Add(request);
        LastRequestSubmittedAt = now;
        return request;
    }
}

public enum ProbeRequestStatus
{
    Queued = 1,
    Applied = 2,
}

/// <summary>Stand-in for AdvancementRequest: just what the in-flight limit touches.</summary>
public class ProbeRequest
{
    public ProbeRequest(string targetKey) => TargetKey = targetKey;

    private ProbeRequest() { }

    public Guid Id { get; private set; }
    public Guid CharacterId { get; private set; }
    public string TargetKey { get; private set; } = null!;
    public ProbeRequestStatus Status { get; private set; } = ProbeRequestStatus.Queued;

    public void MarkApplied() => Status = ProbeRequestStatus.Applied;
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

        modelBuilder.Entity<ProbeCharacter>()
            .HasMany<ProbeRequest>("_requests").WithOne().HasForeignKey(r => r.CharacterId);

        modelBuilder.Entity<ProbeRequest>(e =>
        {
            e.ToTable("advancement_request");
            // The database refuses a second in-flight request for one item, whatever the code does.
            e.HasIndex(r => new { r.CharacterId, r.TargetKey }).IsUnique().HasFilter("status = 1");
        });

        modelBuilder.Entity<CountedCharacter>(e =>
        {
            e.ToTable("counted_character");
            e.Property(c => c.RowVersion).IsConcurrencyToken();
            e.ComplexCollection<IList<TraitValue>, TraitValue>("_traits").ToJson("traits");
            e.Ignore(c => c.Traits);
        });
    }
}
