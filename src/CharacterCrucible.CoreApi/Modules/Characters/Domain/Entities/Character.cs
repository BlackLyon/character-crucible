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
    private readonly IList<AuditEntry> _auditEntries = [];   // write-only, no getter, like _xpLedgerEntries
    public int TotalEarnedXp { get; private set; }
    public int UnspentXp { get; private set; }
    private readonly IList<XpLedgerEntry> _xpLedgerEntries = [];
    public DateTimeOffset CreatedDate { get; private set; }
    public DateTimeOffset? LastRequestSubmittedAt { get; private set; }
    private readonly IList<AdvancementRequest> _requests = [];

    public void Approve(Actor actor, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (Status != CharacterStatus.PendingApproval)
        {
            throw new InvalidOperationException($"Cannot approve a character that is not in the {CharacterStatus.PendingApproval} status.");
        }

        var approvedAt = timeProvider.GetUtcNow();
        RecordAudit(actor, AuditAction.CharacterApproved, StatusChange(CharacterStatus.PendingApproval, CharacterStatus.Active), "Character approved", timeProvider);

        for (var i = 0; i < _abilities.Count; i++)
        {
            _abilities[i] = _abilities[i] with { AcquiredDate = approvedAt };
        }

        Status = CharacterStatus.Active;
    }

    public void SendBack(string reason)
    {
        if (Status != CharacterStatus.PendingApproval)
        {
            throw new InvalidOperationException($"Cannot send back a character that is not in the {CharacterStatus.PendingApproval} status.");
        }

        Status = CharacterStatus.Draft;
    }

    public void RemoveFromPlay(Actor actor, CharacterStatus status, string reason, TimeProvider timeProvider)
    {
        if (Status != CharacterStatus.Active)
        {
            throw new InvalidOperationException($"Cannot remove a character from play that is not in the {CharacterStatus.Active} status.");
        }

        if (status != CharacterStatus.Banned && status != CharacterStatus.Dead && status != CharacterStatus.Retired)
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, $"Invalid character status for removal from play. Must be {CharacterStatus.Banned}, {CharacterStatus.Dead}, or {CharacterStatus.Retired}.");
        }

        RecordAudit(actor, AuditAction.StatusChanged, StatusChange(CharacterStatus.Active, status), reason, timeProvider);
        Status = status;
    }

    public void ReturnToPlay(Actor actor, string reason, TimeProvider timeProvider)
    {
        if (Status != CharacterStatus.Banned && Status != CharacterStatus.Dead && Status != CharacterStatus.Retired)
        {
            throw new InvalidOperationException($"Cannot return a character to play that is not in the {CharacterStatus.Banned}, {CharacterStatus.Dead}, or {CharacterStatus.Retired} status.");
        }

        var previous = Status;
        RecordAudit(actor, AuditAction.StatusChanged, StatusChange(previous, CharacterStatus.Active), reason, timeProvider);
        Status = CharacterStatus.Active;
    }

    public void SubmitForApproval()
    {
        if (Status != CharacterStatus.Draft)
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

    public void GrantXp(Actor actor, int amount, string reason, TimeProvider timeProvider)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount, nameof(amount));
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (Status != CharacterStatus.Active)
        {
            throw new InvalidOperationException($"Cannot grant XP to a character that is not in the {CharacterStatus.Active} status.");
        }

        var previousTotal = TotalEarnedXp;
        var previousUnspent = UnspentXp;
        var newTotalEarnedXp = TotalEarnedXp + amount;
        var newUnspentXp = UnspentXp + amount;
        var ledgerEntry = new XpLedgerEntry(amount, newTotalEarnedXp, newUnspentXp, reason, null, timeProvider);

        RecordAudit(actor, AuditAction.XpGranted, XpChange(amount, previousTotal, newTotalEarnedXp, previousUnspent, newUnspentXp), reason, timeProvider);

        TotalEarnedXp = newTotalEarnedXp;
        UnspentXp = newUnspentXp;
        _xpLedgerEntries.Add(ledgerEntry);
    }

    public void UpdateDraft(CharacterDraft draft)
    {
        if (Status != CharacterStatus.Draft)
        {
            throw new InvalidOperationException($"Cannot update a character that is not in the {CharacterStatus.Draft} status.");
        }

        ApplyDraft(draft);
    }

    /// <summary>Accepts a new advancement request. Requires in-flight requests to be loaded.</summary>
    public void SubmitRequest(AdvancementRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        CheckRequest(request);

        LastRequestSubmittedAt = request.SubmittedDate;

        _requests.Add(request);
    }

    public void ApplyAdvancement(Actor actor, AdvancementRequest request, string engineVersion, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(engineVersion);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(engineVersion.Length, FieldLengths.EngineVersion, nameof(engineVersion));

        if (request.CharacterId != Id)
        {
            throw new InvalidOperationException("The request does not belong to this character.");
        }

        if (Status is not CharacterStatus.Active)
        {
            throw new InvalidOperationException($"Cannot apply the request to a character not in the {CharacterStatus.Active} status");
        }

        if (!request.EvaluatedRulesetVersionId.Equals(RulesetVersionId))
        {
            throw new InvalidOperationException("The request was evaluated against a different ruleset version than the character's current ruleset version.");
        }

        if (UnspentXp < request.QuotedCost)
        {
            throw new InvalidOperationException("The request cannot be applied because the character does not have enough unspent XP.");
        }

        var oldRating = 0;
        var newRating = 0;
        var trait = _traits.FirstOrDefault(t => t.TraitKey == request.TargetKey);

        switch (request.TargetKind)
        {
            case TargetKind.Trait:
                var targetRating = request.TargetRating ?? throw new InvalidOperationException("The request cannot be applied because the target rating is not specified for a trait advancement.");

                if (trait is not null && trait.Rating >= targetRating)
                {
                    throw new InvalidOperationException($"The request cannot be applied because the character's trait '{request.TargetKey}' already has a rating of {trait.Rating}, which is greater than or equal to the requested rating of {targetRating}.");
                }

                oldRating = trait?.Rating ?? 0;
                newRating = targetRating;
                break;

            case TargetKind.Ability:
                var ability = _abilities.FirstOrDefault(a => a.AbilityKey == request.TargetKey);

                if (ability is not null)
                {
                    throw new InvalidOperationException($"The request cannot be applied because the character already possesses the ability '{request.TargetKey}'.");
                }
                break;

            default:
                throw new InvalidOperationException($"The request cannot be applied because the target kind '{request.TargetKind}' is not supported.");
        }

        var newUnspentXp = UnspentXp - request.QuotedCost;
        var ledgerEntry = new XpLedgerEntry(-request.QuotedCost, TotalEarnedXp, newUnspentXp, $"Advancement: {request.TargetKey}", request.Id, timeProvider);

        RecordAudit(actor, AuditAction.AdvancementApplied, AdvancementChange(request.TargetKind, request.TargetKey, request.QuotedCost, oldRating, newRating), null, timeProvider, request.Id, engineVersion);

        UnspentXp = newUnspentXp;

        if (request.TargetKind == TargetKind.Trait)
        {
            var updated = new CharacterTrait(request.TargetKey, newRating);

            if (trait is null)
            {
                _traits.Add(updated);
            }
            else
            {
                _traits[_traits.IndexOf(trait)] = updated;
            }
        }
        else if (request.TargetKind == TargetKind.Ability)
        {
            _abilities.Add(new CharacterAbility(request.TargetKey, timeProvider.GetUtcNow()));
        }

        _xpLedgerEntries.Add(ledgerEntry);
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
        ArgumentOutOfRangeException.ThrowIfGreaterThan(draft.ArchetypeKey.Length, FieldLengths.Key, nameof(draft.ArchetypeKey));
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
        _traits.Clear();
        foreach (var trait in traits)
        {
            _traits.Add(trait);
        }

        _abilities.Clear();
        foreach (var ability in abilities)
        {
            _abilities.Add(ability);
        }
    }

    private List<CharacterTrait> ValidateTraits(IReadOnlyList<CharacterTrait> traits)
    {
        ArgumentNullException.ThrowIfNull(traits);

        var validated = new List<CharacterTrait>(traits.Count);
        var seenKeys = new HashSet<string>();

        foreach (var trait in traits)
        {
            ArgumentNullException.ThrowIfNull(trait);
            ArgumentException.ThrowIfNullOrWhiteSpace(trait.TraitKey, nameof(trait.TraitKey));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(trait.TraitKey.Length, FieldLengths.Key, nameof(trait.TraitKey));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(trait.Rating, nameof(trait.Rating));

            if (!seenKeys.Add(trait.TraitKey))
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
            ArgumentException.ThrowIfNullOrWhiteSpace(ability.AbilityKey, nameof(ability.AbilityKey));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(ability.AbilityKey.Length, FieldLengths.Key);

            if (ability.AcquiredDate is not null)
            {
                throw new ArgumentException($"Ability '{ability.AbilityKey}' has an AcquiredDate set. Abilities cannot have an acquired date in a draft.", nameof(abilities));
            }

            if (!seenKeys.Add(ability.AbilityKey))
            {
                throw new ArgumentException($"Duplicate ability '{ability.AbilityKey}'.", nameof(abilities));
            }
            validated.Add(ability);
        }

        return validated;
    }

    private void RecordAudit(Actor actor, AuditAction action, AuditChange change, string? reason, TimeProvider timeProvider, Guid? requestId = null, string? engineVersion = null)
    {
        _auditEntries.Add(new AuditEntry(actor, action, change, reason, requestId, RulesetVersionId, engineVersion, timeProvider));
    }

    private static AuditChange StatusChange(CharacterStatus from, CharacterStatus to) =>
    new($"Status changed from {from} to {to}", [new ChangeEntry("status", "Status", from.ToString(), to.ToString())]);

    private static AuditChange XpChange(int amount, int totalXpBefore, int totalXpAfter, int unspentXpBefore, int unspentXpAfter) =>
        new($"Character gained experience points.  Amount gained {amount}.  Total Earned XP from {totalXpBefore} to {totalXpAfter}.  Total Unspent XP from {unspentXpBefore} to {unspentXpAfter}.",
            [new ChangeEntry("totalXp", "Total Earned Xp", totalXpBefore.ToString(), totalXpAfter.ToString()), new ChangeEntry("unspentXp", "Unspent Xp", unspentXpBefore.ToString(), unspentXpAfter.ToString())]);

    private static AuditChange AdvancementChange(TargetKind kind, string targetKey, int quotedCost, int oldRating = 0, int targetRating = 0)
    {
        var changes = new List<ChangeEntry>();

        switch(kind)
        {
            case TargetKind.Trait:
                changes.Add(new ChangeEntry($"trait.{targetKey}", "Trait", oldRating.ToString(), targetRating.ToString()));
                break;
            case TargetKind.Ability:
                changes.Add(new ChangeEntry($"ability.{targetKey}", "Ability", null, targetKey));
                break;
            default:
                throw new InvalidOperationException($"Cannot track change for unsupported target kind '{kind}'.");
        }

        return new AuditChange($"Advancement applied for {kind} '{targetKey}' with quoted cost of {quotedCost}.", changes);
    }

    private void CheckRequest(AdvancementRequest request)
    {
        var inFlight = _requests.Where(r => r.IsInFlight).ToList();

        if (Status != CharacterStatus.Active)
        {
            throw new InvalidOperationException($"Cannot create an advancement request for a character not in {CharacterStatus.Active} status.");
        }

        if (inFlight.Count >= 2)
        {
            throw new InvalidOperationException("Not able to submit the request.  The character already has 2 requests pending.");
        }

        if (inFlight.Any(r => r.TargetKind == request.TargetKind && r.TargetKey == request.TargetKey))
        {
            throw new InvalidOperationException("This request is already pending and cannot be submitted again.");
        }

        if (UnspentXp - inFlight.Sum(r => r.QuotedCost) < request.QuotedCost)
        {
            throw new InvalidOperationException("This request cannot be completed as the character does not have enough XP.");
        }
    }
}
