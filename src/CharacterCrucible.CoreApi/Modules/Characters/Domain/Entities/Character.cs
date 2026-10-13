using CharacterCrucible.CoreApi.Modules.Characters.Domain.Constants;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.ValueObjects;

namespace CharacterCrucible.CoreApi.Modules.Characters.Domain.Entities;

public class Character
{
    public Character(Guid ownerId, Guid rulesetVersionId, string name, string archetypeKey, CharacterSex sex, string race, int age, TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(archetypeKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(race);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(age, nameof(age));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.Length, FieldLengths.Name, nameof(name));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(archetypeKey.Length, FieldLengths.ArchetypeKey, nameof(archetypeKey));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(race.Length, FieldLengths.Race, nameof(race));
        
        if (!Enum.IsDefined(sex))
        {
            throw new ArgumentOutOfRangeException(nameof(sex), sex, "Invalid character sex.");
        }

        OwnerId = ownerId;
        RulesetVersionId = rulesetVersionId;
        Name = name;
        ArchetypeKey = archetypeKey;
        Sex = sex;
        Race = race;
        Age = age;
        CreatedDate = timeProvider.GetUtcNow();
    }
    /// <summary>For EF materialisation only. Rows in the database have already been validated.</summary>
    private Character() { }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid RulesetVersionId { get; private set; }
    public string Name { get; private set; } = null!;
    public string ArchetypeKey { get; private set; } = null!;
    public CharacterSex Sex { get; private set; }
    public string Race { get; private set; } = null!;
    public int Age { get; private set; }
    public CharacterStatus Status { get; private set; } = CharacterStatus.Draft;
    public uint RowVersion { get; private set; }
    private readonly IList<CharacterTrait> _traits = [];
    public IReadOnlyCollection<CharacterTrait> Traits => _traits.AsReadOnly();
    private readonly IList<CharacterAbility> _abilities = [];
    public IReadOnlyCollection<CharacterAbility> Abilities => _abilities.AsReadOnly();
    public int TotalEarnedXp { get; private set; }
    public int UnspentXp { get; private set; }
    private readonly IList<XpLedgerEntry> _xpLedgerEntries = [];
    public DateTimeOffset CreatedDate { get; private set; }

    public void Approve()
    {
        if(Status != CharacterStatus.PendingApproval)
        {
            throw new InvalidOperationException($"Cannot approve a character that is not in the {CharacterStatus.PendingApproval} status.");
        }

        Status = CharacterStatus.Active;
    }

    public void SendBack(string reason)
    {
        if(Status != CharacterStatus.PendingApproval)
        {
            throw new InvalidOperationException($"Cannot send back a character that is not in the {CharacterStatus.PendingApproval} status.");
        }

        Status = CharacterStatus.Draft;
    }

    public void RemoveFromPlay(CharacterStatus status, string reason)
    {
        if(Status != CharacterStatus.Active)
        {
            throw new InvalidOperationException($"Cannot remove a character from play that is not in the {CharacterStatus.Active} status.");
        }

        if (status != CharacterStatus.Banned && status != CharacterStatus.Dead && status != CharacterStatus.Retired)
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, $"Invalid character status for removal from play. Must be {CharacterStatus.Banned}, {CharacterStatus.Dead}, or {CharacterStatus.Retired}.");
        }

        Status = status;
    }

    public void ReturnToPlay()
    {
        if(Status != CharacterStatus.Banned && Status != CharacterStatus.Dead && Status != CharacterStatus.Retired)
        {
            throw new InvalidOperationException($"Cannot return a character to play that is not in the {CharacterStatus.Banned}, {CharacterStatus.Dead}, {CharacterStatus.Retired} status.");
        }

        Status = CharacterStatus.Active;
    }

    public void SubmitForApproval()
    {
        if(Status != CharacterStatus.Draft)
        {
            throw new InvalidOperationException($"Cannot submit a character for approval that is not in the {CharacterStatus.Draft} status.");
        }

        Status = CharacterStatus.PendingApproval;
    }

    public void BanDraft(string reason)
    {
        if (Status != CharacterStatus.PendingApproval)
        {
            throw new InvalidOperationException($"Cannot ban a character that is not in the {CharacterStatus.PendingApproval} status.");
        }

        Status = CharacterStatus.DraftBanned;
    }

    public void UnbanDraft(string reason)
    {
        if (Status != CharacterStatus.DraftBanned)
        {
            throw new InvalidOperationException($"Cannot unban a character that is not in the {CharacterStatus.DraftBanned} status.");
        }
        Status = CharacterStatus.Draft;
    }

    public void GrantXp(int amount, string reason, TimeProvider timeProvider)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount, nameof(amount));
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (Status != CharacterStatus.Active)
        {
            throw new InvalidOperationException($"Cannot grant XP to a character that is not in the {CharacterStatus.Active} status.");
        }

        TotalEarnedXp += amount;
        UnspentXp += amount;

        _xpLedgerEntries.Add(new XpLedgerEntry(amount, TotalEarnedXp, UnspentXp, reason, null, timeProvider));
    }


}
