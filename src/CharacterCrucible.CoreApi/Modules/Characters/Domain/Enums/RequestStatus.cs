namespace CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;

// Numbered: persisted, so these values must survive a member being reordered.
public enum RequestStatus
{
    Abandoned =1,
    Applied = 2,
    Approved = 3,
    Failed = 4,
    PendingApproval = 5,
    Queued = 6,
    Rejected = 7
}
