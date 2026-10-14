using System.ComponentModel.DataAnnotations;

using CharacterCrucible.CoreApi.Modules.Characters.Domain.Constants;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.Inputs;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.ValueObjects;

namespace CharacterCrucible.CoreApi.Modules.Characters.Domain.Entities;

public class Character
{
    public Character(Guid ownerId, Guid rulesetVersionId, CharacterDraft draft, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        OwnerId = ownerId;
        RulesetVersionId = rulesetVersionId;
        CreatedDate = timeProvider.GetUtcNow();
        ApplyDraft(draft);
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

    public void Approve(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (Status != CharacterStatus.PendingApproval)
        {
            throw new InvalidOperationException($"Cannot approve a character that is not in the {CharacterStatus.PendingApproval} status.");
        }

        var approvedAt = timeProvider.GetUtcNow();      // once

        for (var i = 0; i < _abilities.Count; i++)
        {
            _abilities[i] = _abilities[i] with { AcquiredDate = approvedAt };
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

    public void UpdateDraft(CharacterDraft draft)
    {
        if(Status != CharacterStatus.Draft)
        {
            throw new InvalidOperationException($"Cannot update a character that is not in the {CharacterStatus.Draft} status.");
        }

        ApplyDraft(draft);
    }

    private void ApplyDraft(CharacterDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(draft.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(draft.Race);
        ArgumentException.ThrowIfNullOrWhiteSpace(draft.ArchetypeKey);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(draft.Age, nameof(draft.Age));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(draft.Name.Length, FieldLengths.Name, nameof(draft.Name));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(draft.Race.Length, FieldLengths.Race, nameof(draft.Race));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(draft.ArchetypeKey.Length, FieldLengths.KeyName, nameof(draft.ArchetypeKey));
        var traits = ValidateTraits(draft.Traits);
        var abilities = ValidateAbilities(draft.Abilities);

        if (!Enum.IsDefined(draft.Sex))
        {
            throw new ArgumentOutOfRangeException(nameof(draft.Sex), draft.Sex, "Invalid character sex.");
        }

        Name = draft.Name;
        ArchetypeKey = draft.ArchetypeKey;
        Sex = draft.Sex;
        Race = draft.Race;
        Age = draft.Age;
        _traits.Clear(); foreach (var trait in traits) _traits.Add(trait);
        _abilities.Clear(); foreach (var ability in abilities) _abilities.Add(ability);
    }

    private List<CharacterTrait> ValidateTraits(IReadOnlyList<CharacterTrait> traits)
    {
        ArgumentNullException.ThrowIfNull(traits);
        
        var validated = new List<CharacterTrait>(traits.Count);
        var seenKeys = new HashSet<string>();

        foreach (var trait in traits)
        {
            ArgumentNullException.ThrowIfNull(trait);
            ArgumentException.ThrowIfNullOrWhiteSpace(trait.TraitKey);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(trait.TraitKey.Length, FieldLengths.KeyName);

            if (!seenKeys.Add(trait.TraitKey))   // Add returns false if the key was already there
            {
                throw new ArgumentException($"Duplicate trait '{trait.TraitKey}'.", nameof(traits));
            }

            validated.Add(trait);
        }

        return validated;
    }

    private List<CharacterAbility> ValidateAbilities(IReadOnlyList<CharacterAbility> abilities)
    {
        ArgumentNullException.ThrowIfNull(abilities);

        var validated = new List<CharacterAbility>(abilities.Count);
        var seenKeys = new HashSet<string>();

        foreach (var ability in abilities)
        {
            ArgumentNullException.ThrowIfNull(ability);
            ArgumentException.ThrowIfNullOrWhiteSpace(ability.AbilityKey);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(ability.AbilityKey.Length, FieldLengths.KeyName);
            
            if(ability.AcquiredDate is not null) throw new ArgumentException($"Ability '{ability.AbilityKey}' has an AcquiredDate set. Abilities cannot have an acquired date in a draft.", nameof(abilities));

            if (!seenKeys.Add(ability.AbilityKey))   // Add returns false if the key was already there
            {
                throw new ArgumentException($"Duplicate ability '{ability.AbilityKey}'.", nameof(abilities));
            }
            validated.Add(ability);
        }

        return validated;
    }
}
