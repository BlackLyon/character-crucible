using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;

namespace CharacterCrucible.CoreApi.Modules.Characters.Domain.ValueObjects;

/// <summary>Builds the <see cref="AuditChange"/> each kind of character change records. Reads nothing from the character.</summary>
internal static class AuditChanges
{
    internal static AuditChange StatusChange(CharacterStatus from, CharacterStatus to) =>
    new($"Status changed from {from} to {to}", [new ChangeEntry("status", "Status", from.ToString(), to.ToString())]);

    internal static AuditChange XpChange(int amount, int totalXpBefore, int totalXpAfter, int unspentXpBefore, int unspentXpAfter) =>
        new($"Character gained experience points.  Amount gained {amount}.  Total Earned XP from {totalXpBefore} to {totalXpAfter}.  Total Unspent XP from {unspentXpBefore} to {unspentXpAfter}.",
            [new ChangeEntry("totalXp", "Total Earned Xp", totalXpBefore.ToString(), totalXpAfter.ToString()), new ChangeEntry("unspentXp", "Unspent Xp", unspentXpBefore.ToString(), unspentXpAfter.ToString())]);

    internal static AuditChange AdvancementChange(TargetKind kind, string targetKey, int quotedCost, int oldRating = 0, int targetRating = 0)
    {
        var changes = new List<ChangeEntry>();

        switch(kind)
        {
            case TargetKind.Trait:
                changes.Add(new ChangeEntry($"trait.{targetKey}", "Trait", oldRating.ToString(), targetRating.ToString()));
                break;
            case TargetKind.Ability:
                changes.Add(new ChangeEntry($"ability.{targetKey}", "Ability", null, targetKey));
                break;
            default:
                throw new InvalidOperationException($"Cannot track change for unsupported target kind '{kind}'.");
        }

        return new AuditChange($"Advancement applied for {kind} '{targetKey}' with quoted cost of {quotedCost}.", changes);
    }
}
