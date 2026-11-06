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
        ArgumentOutOfRangeException.ThrowIfEqual(submittedBy, Guid.Empty, nameof(submittedBy));
        ArgumentOutOfRangeException.ThrowIfEqual(evaluatedRulesetVersionId, Guid.Empty, nameof(evaluatedRulesetVersionId));

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
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedDate { get; private set; }
    public bool IsInFlight => RequestStatus is RequestStatus.Queued or RequestStatus.PendingApproval or RequestStatus.Approved;

    public void Approve(Guid reviewer, TimeProvider timeProvider)
    {
        if (RequestStatus is not RequestStatus.PendingApproval)
        {
            throw new InvalidOperationException($"Cannot approve a request that is not pending approval. Current status: {RequestStatus}");
        }

        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfEqual(reviewer, Guid.Empty, nameof(reviewer));

        ReviewedBy = reviewer;
        ReviewedDate = timeProvider.GetUtcNow();
        RequestStatus = RequestStatus.Approved;
    }

    public void Reject(Guid reviewer, string reason, TimeProvider timeProvider)
    {
        if (RequestStatus is not RequestStatus.PendingApproval)
        {
            throw new InvalidOperationException($"Cannot reject a request that is not pending approval. Current status: {RequestStatus}");
        }

        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfEqual(reviewer, Guid.Empty, nameof(reviewer));
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(reason.Length, FieldLengths.Reason, nameof(reason));

        ReviewedBy = reviewer;
        ResolutionReason = reason;
        ReviewedDate = ResolvedDate = timeProvider.GetUtcNow();
        RequestStatus = RequestStatus.Rejected;
    }

    public void MarkApplied(TimeProvider timeProvider)
    {
        if (RequestStatus is not (RequestStatus.Queued or RequestStatus.Approved))
        {
            throw new InvalidOperationException($"Cannot mark a request as applied that is not either approved or queued. Current status: {RequestStatus}");
        }
        
        ArgumentNullException.ThrowIfNull(timeProvider);

        ResolvedDate = timeProvider.GetUtcNow();
        RequestStatus = RequestStatus.Applied;
    }

    public void MarkFailed(string reason, TimeProvider timeProvider)
    {
        if (RequestStatus is not (RequestStatus.Queued or RequestStatus.Approved))
        {
            throw new InvalidOperationException($"Cannot mark a request as failed that is not either approved or queued. Current status: {RequestStatus}");
        }

        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(reason.Length, FieldLengths.Reason, nameof(reason));
        
        ResolutionReason = reason;
        ResolvedDate = timeProvider.GetUtcNow();
        RequestStatus = RequestStatus.Failed;
    }

    public void MarkAbandoned(string reason, TimeProvider timeProvider)
    {
        if (RequestStatus is RequestStatus.PendingApproval)
        {
            throw new InvalidOperationException($"Cannot mark a request as abandoned that is pending approval. Current status: {RequestStatus}");
        }

        if (!IsInFlight)
        {
            return; // already resolved, nothing to do
        }

        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(reason.Length, FieldLengths.Reason, nameof(reason));
        
        ResolutionReason = reason;
        ResolvedDate = timeProvider.GetUtcNow();
        RequestStatus = RequestStatus.Abandoned;
    }
}
