using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;

namespace CharacterCrucible.CoreApi.Modules.Characters.Domain.Entities;

public class AdvancementRequest
{
    public AdvancementRequest(Guid characterId, int requestedXp, string reason)
    {
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
