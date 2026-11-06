using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.Inputs;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.ValueObjects;

namespace CharacterCrucible.CoreApi.Modules.Characters.Domain.Entities;

/// <summary>The character aggregate. One class split across Character.*.cs by concern, so its methods can be read one area at a time.</summary>
public partial class Character
{
    public Character(Guid ownerId, Guid rulesetVersionId, CharacterDraft draft, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfEqual(ownerId, Guid.Empty, nameof(ownerId));
        ArgumentOutOfRangeException.ThrowIfEqual(rulesetVersionId, Guid.Empty, nameof(rulesetVersionId));

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
    private const int MaxRequestsInFlight = 2;

    private void RecordAudit(Actor actor, AuditAction action, AuditChange change, string? reason, TimeProvider timeProvider, Guid? requestId = null, string? engineVersion = null)
    {
        _auditEntries.Add(new AuditEntry(actor, action, change, reason, requestId, RulesetVersionId, engineVersion, timeProvider));
    }
}
