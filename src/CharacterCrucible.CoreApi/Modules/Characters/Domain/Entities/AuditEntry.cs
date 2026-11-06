using CharacterCrucible.CoreApi.Modules.Characters.Domain.Constants;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.ValueObjects;

namespace CharacterCrucible.CoreApi.Modules.Characters.Domain.Entities;

/// <summary>One change to an active character: who made it, what changed, and under which ruleset. Player-facing history.</summary>
public class AuditEntry
{
    private static readonly HashSet<AuditAction> ReasonRequired = [AuditAction.StatusChanged, AuditAction.XpGranted, AuditAction.StoryGrant, AuditAction.ManualEdit, AuditAction.Refunded];
    private static readonly HashSet<AuditAction> RulesRan = [AuditAction.AdvancementApplied, AuditAction.StoryGrant, AuditAction.ManualEdit, AuditAction.Refunded];

    internal AuditEntry(Actor actor, AuditAction action, AuditChange change, string? reason, Guid? advancementRequestId, Guid rulesetVersionId, string? engineVersion, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(change);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentOutOfRangeException.ThrowIfEqual(actor.Id, Guid.Empty, nameof(actor.Id));

        if (ReasonRequired.Contains(action))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        }

        if (!Enum.IsDefined(actor.Role))
        {
            throw new ArgumentOutOfRangeException(nameof(actor.Role), actor.Role, "Invalid actor role.");
        }

        if (!Enum.IsDefined(action))
        {
            throw new ArgumentOutOfRangeException(nameof(action), action, "Invalid audit action.");
        }

        if (RulesRan.Contains(action))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(engineVersion);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(engineVersion.Length, FieldLengths.EngineVersion, nameof(engineVersion));
        }
        else if (engineVersion is not null)
        {
            throw new ArgumentException($"{action} involves no Rules evaluation, so it records no engine version.", nameof(engineVersion));
        }

        if (action is AuditAction.AdvancementApplied && advancementRequestId is null)
        {
            throw new ArgumentException($"{action} must record the request it applied.", nameof(advancementRequestId));
        }

        if (reason is not null)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(reason.Length, FieldLengths.Reason, nameof(reason));
        }

        ActorId = actor.Id;
        ActorRole = actor.Role;
        Action = action;
        Change = change;
        Reason = reason;
        AdvancementRequestId = advancementRequestId;
        RulesetVersionId = rulesetVersionId;
        EngineVersion = engineVersion;
        OccurredAt = timeProvider.GetUtcNow();
    }

    /// <summary>For EF materialisation only. Rows in the database have already been validated.</summary>
    private AuditEntry() { }

    public Guid Id { get; private set; }
    public Guid CharacterId { get; private set; }
    public Guid ActorId { get; private set; }
    public ActorRole ActorRole { get; private set; }
    public AuditAction Action { get; private set; }
    public AuditChange Change { get; private set; } = null!;
    public string? Reason { get; private set; }
    public Guid? AdvancementRequestId { get; private set; }
    public Guid RulesetVersionId { get; private set; }
    public string? EngineVersion { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
}
