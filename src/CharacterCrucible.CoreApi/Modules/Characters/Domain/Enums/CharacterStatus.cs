namespace CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;

// Numbered: persisted, so these values must survive a member being reordered.
public enum CharacterStatus
{
    Active = 1,
    Banned = 2,
    Dead = 3,
    Draft = 4,
    DraftBanned = 5,
    PendingApproval = 6,
    Retired = 7,
}
