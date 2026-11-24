using CharacterCrucible.CoreApi.Modules.Characters.Domain.Entities;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.ValueObjects;
using CharacterCrucible.CoreApi.Modules.Characters.Persistence;
using Microsoft.EntityFrameworkCore;
using static CharacterCrucible.CoreApi.Tests.Builders.TestCharacters;

namespace CharacterCrucible.CoreApi.Tests.Persistence;

/// <summary>
/// What the domain writes to the ledger and audit trail. The character keeps those rows in private
/// fields, so the only place a test can read them is the database.
/// </summary>
public class LedgerAndAuditRowTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    // Not the request's EngineVersion, so the audit row shows which of the two it recorded.
    private const string ApplyEngineVersion = "rules-test-2";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A character that earned 10 XP and spent 4 raising strength from 2 to 3, saved.</summary>
    private async Task<(Guid CharacterId, Guid RequestId)> SaveAppliedAsync()
    {
        var versionId = await PublishedVersions.SaveAsync(postgres, Ct);
        var character = ACharacter().InStatus(CharacterStatus.Active).OnVersion(versionId).WithXp(10).Build();
        var request = ATraitRequest(character).WithKey("strength").WithRating(3).WithCost(4).Submitted().Build();
        await using (var db = postgres.NewContext())
        {
            db.Characters.Add(character);
            await db.SaveChangesAsync(Ct);
        }

        // Applied in a second context, once the request has the id the rows copy.
        await using (var db = postgres.NewContext())
        {
            var loaded = await db.Characters.WithInFlightRequests().SingleAsync(c => c.Id == character.Id, Ct);
            var loadedRequest = db.Set<AdvancementRequest>().Local.Single();
            loaded.ApplyAdvancement(Worker, loadedRequest, ApplyEngineVersion, Clock());
            loadedRequest.MarkApplied(Clock());
            await db.SaveChangesAsync(Ct);
        }

        return (character.Id, request.Id);
    }

    [Fact]
    public async Task Applying_writes_a_ledger_entry_for_the_cost_with_the_new_balance()
    {
        var (_, requestId) = await SaveAppliedAsync();

        await using var db = postgres.NewContext();
        var spend = await db.Set<XpLedgerEntry>().SingleAsync(l => l.AdvancementRequestId == requestId, Ct);

        Assert.Equal(-4, spend.Delta);
        Assert.Equal(6, spend.UnspentXpAfter);
        Assert.Equal(10, spend.TotalEarnedXpAfter);
    }

    [Fact]
    public async Task Applying_writes_an_audit_row_with_the_request_and_the_apply_time_engine_version()
    {
        var (_, requestId) = await SaveAppliedAsync();

        await using var db = postgres.NewContext();
        var audit = await db.Set<AuditEntry>().SingleAsync(a => a.AdvancementRequestId == requestId, Ct);

        Assert.Equal(AuditAction.AdvancementApplied, audit.Action);
        Assert.Equal(Worker, new Actor(audit.ActorId, audit.ActorRole));
        Assert.Equal(ApplyEngineVersion, audit.EngineVersion);
        Assert.Equal(new ChangeEntry("trait.strength", "Trait", "2", "3"), Assert.Single(audit.Change.Changes));
    }

    [Fact]
    public async Task Granting_xp_writes_a_ledger_entry_and_an_audit_row_with_the_new_totals()
    {
        // After the spend, total and unspent differ (10 and 6), so a swapped pair can't pass.
        var (id, _) = await SaveAppliedAsync();
        await using (var db = postgres.NewContext())
        {
            var loaded = await db.Characters.SingleAsync(c => c.Id == id, Ct);
            loaded.GrantXp(Narrator, 7, "Rescued the envoy", Clock());
            await db.SaveChangesAsync(Ct);
        }

        await using var reader = postgres.NewContext();
        var grant = await reader.Set<XpLedgerEntry>().SingleAsync(l => l.CharacterId == id && l.Reason == "Rescued the envoy", Ct);
        var audit = await reader.Set<AuditEntry>().SingleAsync(a => a.CharacterId == id && a.Reason == "Rescued the envoy", Ct);

        Assert.Equal(7, grant.Delta);
        Assert.Equal(17, grant.TotalEarnedXpAfter);
        Assert.Equal(13, grant.UnspentXpAfter);
        Assert.Null(grant.AdvancementRequestId);

        Assert.Equal(AuditAction.XpGranted, audit.Action);
        Assert.Equal(Narrator, new Actor(audit.ActorId, audit.ActorRole));
        Assert.Null(audit.EngineVersion);
        Assert.Equal(
            [new ChangeEntry("totalEarnedXp", "Total Earned XP", "10", "17"), new ChangeEntry("unspentXp", "Unspent XP", "6", "13")],
            audit.Change.Changes);
    }
}
