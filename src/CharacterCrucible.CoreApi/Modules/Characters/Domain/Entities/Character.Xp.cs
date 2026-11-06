using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.ValueObjects;

using static CharacterCrucible.CoreApi.Modules.Characters.Domain.ValueObjects.AuditChanges;

namespace CharacterCrucible.CoreApi.Modules.Characters.Domain.Entities;

public partial class Character
{
    public void GrantXp(Actor actor, int amount, string reason, TimeProvider timeProvider)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount, nameof(amount));
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (Status != CharacterStatus.Active)
        {
            throw new InvalidOperationException($"Cannot grant XP to a character that is not in the {CharacterStatus.Active} status. Current status: {Status}.");
        }

        var previousTotal = TotalEarnedXp;
        var previousUnspent = UnspentXp;
        var newTotalEarnedXp = TotalEarnedXp + amount;
        var newUnspentXp = UnspentXp + amount;
        var ledgerEntry = new XpLedgerEntry(amount, newTotalEarnedXp, newUnspentXp, reason, null, timeProvider);

        RecordAudit(actor, AuditAction.XpGranted, XpChange(amount, previousTotal, newTotalEarnedXp, previousUnspent, newUnspentXp), reason, timeProvider);

        TotalEarnedXp = newTotalEarnedXp;
        UnspentXp = newUnspentXp;
        _xpLedgerEntries.Add(ledgerEntry);
    }
}
