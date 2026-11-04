using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.ValueObjects;

using static CharacterCrucible.CoreApi.Modules.Characters.Domain.ValueObjects.AuditChanges;

namespace CharacterCrucible.CoreApi.Modules.Characters.Domain.Entities;

public partial class Character
{
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
}
