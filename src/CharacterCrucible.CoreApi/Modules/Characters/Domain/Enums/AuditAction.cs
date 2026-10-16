namespace CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;

// Numbered: persisted, so these values must survive a member being reordered.
public enum AuditAction
{
    AdvancementApplied = 1,
    CharacterApproved = 2,
    ManualEdit = 3,
    Refunded = 4,
    RequestApproved = 5,
    RequestRejected = 6,
    StatusChanged = 7,
    StoryGrant = 8,
    XpGranted = 9
}
