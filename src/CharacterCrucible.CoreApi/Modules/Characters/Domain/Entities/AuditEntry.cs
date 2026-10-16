using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.ValueObjects;

namespace CharacterCrucible.CoreApi.Modules.Characters.Domain.Entities;

public class AuditEntry
{
    public AuditEntry(Guid characterId)
    {
    }

    private AuditEntry() { }

    public Guid Id { get; private set; }
    public Guid CharacterId { get; private set; }
    public Guid ActorId { get; private set; }
    public ActorRole ActorRole { get; private set; }
    public AuditAction Action { get; private set; }
    public AuditChange? Change { get; private set; }
    public string? Reason { get; private set; }
    public Guid? AdvancementRequestId { get; private set; }
    public Guid RulesetVersionId { get; private set; }
    public string? EngineVersion { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
}
