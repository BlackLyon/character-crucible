using System.Text.Json;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.Entities;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.ValueObjects;
using CharacterCrucible.CoreApi.Modules.Characters.Persistence;
using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Entities;
using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using static CharacterCrucible.CoreApi.Tests.Builders.TestCharacters;

namespace CharacterCrucible.CoreApi.Tests.Persistence;

/// <summary>
/// Proves the Characters mapping against a real Postgres: the jsonb sheet and audit diff, the
/// in-flight index, the copied request id, and the Restrict FKs decided for PR B.
/// </summary>
public class CharacterPersistenceTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<Guid> SaveAsync(Character character)
    {
        await using var db = postgres.NewContext();
        db.Characters.Add(character);
        await db.SaveChangesAsync(Ct);
        return character.Id;
    }

    private static PostgresException ViolationIn(Exception exception) =>
        Assert.IsType<PostgresException>(exception is DbUpdateException ? exception.InnerException : exception);

    [Fact]
    public async Task A_draft_round_trips_its_sheet_and_a_null_acquired_date()
    {
        var versionId = await PublishedVersions.SaveAsync(postgres, Ct);
        var draft = Draft(versionId, Sheet(abilities: [new CharacterAbility("riposte", null)]));
        var id = await SaveAsync(draft);

        await using var db = postgres.NewContext();
        var reloaded = await db.Characters.SingleAsync(c => c.Id == id, Ct);

        Assert.Equal(CharacterStatus.Draft, reloaded.Status);
        Assert.Equal(Player.Id, reloaded.OwnerId);
        Assert.Equal(versionId, reloaded.RulesetVersionId);
        Assert.Equal(("Ilsa Varn", CharacterSex.Female, "Human", 31, "warrior"),
            (reloaded.Name, reloaded.Sex, reloaded.Race, reloaded.Age, reloaded.ArchetypeKey));
        Assert.Equal(Now, reloaded.CreatedDate);
        Assert.Equal(draft.Traits, reloaded.Traits);

        // A draft's abilities have no date until approval. The probe's stand-in was non-nullable.
        var ability = Assert.Single(reloaded.Abilities);
        Assert.Null(ability.AcquiredDate);

        // xmin, read back by the save. Zero would mean the concurrency token isn't mapped.
        Assert.NotEqual(0u, reloaded.RowVersion);
    }

    [Fact]
    public async Task Approval_round_trips_the_acquired_date_and_the_audit_diff()
    {
        var versionId = await PublishedVersions.SaveAsync(postgres, Ct);
        var id = await SaveAsync(Active(rulesetVersionId: versionId,
            sheet: Sheet(abilities: [new CharacterAbility("riposte", null)])));

        await using var db = postgres.NewContext();
        var reloaded = await db.Characters.SingleAsync(c => c.Id == id, Ct);
        Assert.Equal(Now, Assert.Single(reloaded.Abilities).AcquiredDate);

        var audit = await db.Set<AuditEntry>().SingleAsync(a => a.CharacterId == id, Ct);
        Assert.Equal(AuditAction.CharacterApproved, audit.Action);
        Assert.Equal(Narrator, new Actor(audit.ActorId, audit.ActorRole));
        Assert.Equal(versionId, audit.RulesetVersionId);
        Assert.Null(audit.EngineVersion);
        Assert.Null(audit.AdvancementRequestId);

        // AuditChange keeps its entries in a private IList inside a jsonb complex property.
        Assert.False(string.IsNullOrWhiteSpace(audit.Change.Summary));
        var entry = Assert.Single(audit.Change.Changes);
        Assert.Equal(new ChangeEntry("status", "Status", "PendingApproval", "Active"), entry);

        // The stored document, which the history view and any later reader depend on. The
        // private member name must not leak into it.
        var json = await db.Database
            .SqlQuery<string>($"select change::text as \"Value\" from characters.audit_entry where character_id = {id}")
            .SingleAsync(Ct);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(["Changes", "Summary"],
            document.RootElement.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal(["From", "Label", "Path", "To"],
            document.RootElement.GetProperty("Changes")[0].EnumerateObject().Select(p => p.Name).Order());
    }

    /// <summary>Each status, reached through the request's own transitions.</summary>
    private static AdvancementRequest RequestIn(RequestStatus status, Character character)
    {
        var request = TraitRequest(character, requiresApproval: status is
            RequestStatus.PendingApproval or RequestStatus.Approved or RequestStatus.Rejected);
        character.SubmitRequest(request);

        switch (status)
        {
            case RequestStatus.Approved: request.Approve(Narrator.Id, Clock()); break;
            case RequestStatus.Rejected: request.Reject(Narrator.Id, "Not this session.", Clock()); break;
            case RequestStatus.Applied: request.MarkApplied(Clock()); break;
            case RequestStatus.Failed: request.MarkFailed("The cost changed.", Clock()); break;
            case RequestStatus.Abandoned: request.MarkAbandoned("Dead-lettered.", Clock()); break;
        }

        Assert.Equal(status, request.RequestStatus);
        return request;
    }

    [Theory]
    [InlineData(RequestStatus.Abandoned)]
    [InlineData(RequestStatus.Applied)]
    [InlineData(RequestStatus.Approved)]
    [InlineData(RequestStatus.Failed)]
    [InlineData(RequestStatus.PendingApproval)]
    [InlineData(RequestStatus.Queued)]
    [InlineData(RequestStatus.Rejected)]
    public async Task In_flight_means_the_same_to_IsInFlight_the_include_and_the_index(RequestStatus status)
    {
        var versionId = await PublishedVersions.SaveAsync(postgres, Ct);
        var character = Active(rulesetVersionId: versionId);
        var inFlight = RequestIn(status, character).IsInFlight;
        var id = await SaveAsync(character);

        // The include.
        await using (var db = postgres.NewContext())
        {
            await db.Characters.WithInFlightRequests().SingleAsync(c => c.Id == id, Ct);
            Assert.Equal(inFlight ? 1 : 0, db.ChangeTracker.Entries<AdvancementRequest>().Count());
        }

        // The index. Loaded without its requests, the character can't see the first one, so only
        // the database stands between it and a second request for the same item.
        await using (var db = postgres.NewContext())
        {
            var blind = await db.Characters.SingleAsync(c => c.Id == id, Ct);
            blind.SubmitRequest(TraitRequest(blind));

            if (inFlight)
            {
                var violation = ViolationIn(await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct)));
                Assert.Equal(PostgresErrorCodes.UniqueViolation, violation.SqlState);
                Assert.Equal("ux_advancement_request_one_in_flight_per_item", violation.ConstraintName);
            }
            else
            {
                await db.SaveChangesAsync(Ct);
            }
        }
    }

    [Fact]
    public async Task Applying_copies_the_saved_request_id_onto_the_ledger_and_audit_rows()
    {
        var versionId = await PublishedVersions.SaveAsync(postgres, Ct);
        var character = Active(xp: 10, rulesetVersionId: versionId);
        var request = TraitRequest(character, cost: 4);
        character.SubmitRequest(request);
        var id = await SaveAsync(character);
        Assert.NotEqual(Guid.Empty, request.Id);

        // As the worker will: a fresh load, then apply.
        await using (var db = postgres.NewContext())
        {
            var loaded = await db.Characters.WithInFlightRequests().SingleAsync(c => c.Id == id, Ct);
            var loadedRequest = db.Set<AdvancementRequest>().Local.Single();
            loaded.ApplyAdvancement(Worker, loadedRequest, EngineVersion, Clock());
            loadedRequest.MarkApplied(Clock());
            await db.SaveChangesAsync(Ct);
        }

        await using (var db = postgres.NewContext())
        {
            var spend = await db.Set<XpLedgerEntry>().SingleAsync(l => l.AdvancementRequestId == request.Id, Ct);
            Assert.Equal((id, -4, 6), (spend.CharacterId, spend.Delta, spend.UnspentXpAfter));

            var audit = await db.Set<AuditEntry>().SingleAsync(a => a.AdvancementRequestId == request.Id, Ct);
            Assert.Equal((id, AuditAction.AdvancementApplied), (audit.CharacterId, audit.Action));
        }
    }

    [Fact]
    public async Task A_character_on_a_ruleset_version_that_does_not_exist_is_rejected()
    {
        await using var db = postgres.NewContext();
        db.Characters.Add(Draft(rulesetVersionId: Guid.CreateVersion7()));

        var violation = ViolationIn(await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct)));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, violation.SqlState);
        Assert.Equal("fk_character_ruleset_version_ruleset_version_id", violation.ConstraintName);
    }

    [Fact]
    public async Task A_draft_version_a_character_points_at_cannot_be_deleted()
    {
        // The case that decided Q5. Nothing yet stops a character being saved on a draft, which is
        // why the FK has to: the delete would otherwise orphan its history.
        Guid draftId;
        await using (var db = postgres.NewContext())
        {
            var ruleset = new Ruleset($"Draft only {Guid.CreateVersion7()}", "For tests.", "Blacklyon");
            var draft = ruleset.CreateDraft(1, 0, RulesetVersionKind.Release);
            db.Rulesets.Add(ruleset);
            await db.SaveChangesAsync(Ct);
            draftId = draft.Id;
        }
        await SaveAsync(Draft(rulesetVersionId: draftId));

        await using (var db = postgres.NewContext())
        {
            var violation = ViolationIn(await Assert.ThrowsAsync<PostgresException>(() =>
                db.RulesetVersions.Where(v => v.Id == draftId).ExecuteDeleteAsync(Ct)));
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, violation.SqlState);
        }
    }

    [Fact]
    public async Task A_character_with_an_xp_ledger_cannot_be_deleted()
    {
        var versionId = await PublishedVersions.SaveAsync(postgres, Ct);
        var id = await SaveAsync(Active(xp: 5, rulesetVersionId: versionId));

        await using var db = postgres.NewContext();

        // The audit rows would block the delete first. Removing them isolates the ledger's own FK.
        await db.Set<AuditEntry>().Where(a => a.CharacterId == id).ExecuteDeleteAsync(Ct);

        var violation = ViolationIn(await Assert.ThrowsAsync<PostgresException>(() =>
            db.Characters.Where(c => c.Id == id).ExecuteDeleteAsync(Ct)));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, violation.SqlState);
        Assert.Equal("fk_xp_ledger_entry_character_character_id", violation.ConstraintName);
    }
}
