using CharacterCrucible.CoreApi.Modules.Characters.Domain.Constants;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;

namespace CharacterCrucible.CoreApi.Modules.Characters.Domain.Entities;

public class AdvancementRequest
{
    private AdvancementRequest(Guid submittedBy, TimeProvider timeProvider, TargetKind targetKind, string targetKey, int? targetRating, int quotedCost, Guid evaluatedRulesetVersionId, string evaluatedEngineVersion, bool requiresApproval)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(evaluatedEngineVersion);
        ArgumentOutOfRangeException.ThrowIfNegative(quotedCost, nameof(quotedCost));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(targetKey.Length, FieldLengths.Key, nameof(targetKey));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(evaluatedEngineVersion.Length, FieldLengths.EngineVersion, nameof(evaluatedEngineVersion));
        ArgumentNullException.ThrowIfNull(timeProvider);

        SubmittedBy = submittedBy;
        SubmittedDate = timeProvider.GetUtcNow();
        TargetKind = targetKind;
        TargetKey = targetKey;
        TargetRating = targetRating;
        QuotedCost = quotedCost;
        EvaluatedRulesetVersionId = evaluatedRulesetVersionId;
        EvaluatedEngineVersion = evaluatedEngineVersion;
        RequestStatus = requiresApproval ? RequestStatus.PendingApproval : RequestStatus.Queued;
    }

    internal static AdvancementRequest ForTrait(Guid submittedBy, TimeProvider timeProvider, string traitKey, int targetRating, int quotedCost, Guid rulesetVersionId, string engineVersion, bool requiresApproval)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetRating);   // the trait-only rule

        return new AdvancementRequest(submittedBy, timeProvider, TargetKind.Trait, traitKey, targetRating, quotedCost, rulesetVersionId, engineVersion, requiresApproval);
    }

    internal static AdvancementRequest ForAbility(Guid submittedBy, TimeProvider timeProvider, string abilityKey, int quotedCost, Guid rulesetVersionId, string engineVersion, bool requiresApproval)
    {
        return new AdvancementRequest(submittedBy, timeProvider, TargetKind.Ability, abilityKey, null, quotedCost, rulesetVersionId, engineVersion, requiresApproval);
    }

    private AdvancementRequest() { }

    public Guid Id { get; private set; }
    public Guid CharacterId { get; private set; }
    public Guid SubmittedBy { get; private set; }
    public DateTimeOffset SubmittedDate { get; private set; }
    public TargetKind TargetKind { get; private set; }
    public string TargetKey { get; private set; } = null!;
    public int? TargetRating { get; private set; }
    public int QuotedCost { get; private set; }
    public Guid EvaluatedRulesetVersionId { get; private set; }
    public string EvaluatedEngineVersion { get; private set; } = null!;
    public RequestStatus RequestStatus { get; private set; }
    public DateTimeOffset? ResolvedDate { get; private set; }
    public string? ResolutionReason { get; private set; }
}
