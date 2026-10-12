namespace CharacterCrucible.CoreApi.Modules.Characters.Domain.Entities;

public class XpLedgerEntry
{
    public XpLedgerEntry(Guid characterId, int delta, int totalEarnedXpAfter, int unspentXpAfter, string reason, Guid? advancementRequestId, DateTimeOffset occurredAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentOutOfRangeException.ThrowIfNegative(totalEarnedXpAfter, nameof(totalEarnedXpAfter));
        ArgumentOutOfRangeException.ThrowIfNegative(unspentXpAfter, nameof(unspentXpAfter));

        CharacterId = characterId;
        Delta = delta;
        TotalEarnedXpAfter = totalEarnedXpAfter;
        UnspentXpAfter = unspentXpAfter;
        Reason = reason;
        AdvancementRequestId = advancementRequestId;
        OccurredAt = occurredAt;
    }

    /// <summary>For EF materialisation only. Rows in the database have already been validated.</summary>
    private XpLedgerEntry()
    {
    }

    public Guid Id { get; private set; }
    public Guid CharacterId { get; private set; }
    public int Delta { get; private set; }
    public int TotalEarnedXpAfter { get; private set; }
    public int UnspentXpAfter { get; private set; }
    public string Reason { get; private set; } = null!;
    public Guid? AdvancementRequestId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
}
