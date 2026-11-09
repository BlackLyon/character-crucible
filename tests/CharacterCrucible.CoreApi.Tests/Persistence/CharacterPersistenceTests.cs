using System.Text.Json;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.Entities;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.ValueObjects;
using CharacterCrucible.CoreApi.Modules.Characters.Persistence;
using CharacterCrucible.CoreApi.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using static CharacterCrucible.CoreApi.Tests.Builders.TestCharacters;

namespace CharacterCrucible.CoreApi.Tests.Persistence;

/// <summary>
/// Proves the Characters mapping against a real Postgres: the jsonb sheet and audit diff, the
/// row version, the in-flight index, the copied request id, and that every FK is Restrict.
/// </summary>
/// <remarks>
/// These assert what is stored, not what the domain decides. The domain's rules are the guard
/// tests' job, in Domain/Characters.
/// </remarks>
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

    /// <summary>Runs a bulk statement the database must refuse, and returns why it did.</summary>
    private async Task<PostgresException> RefusedAsync(Func<CharacterCrucibleDbContext, Task> statement)
    {
        await using var db = postgres.NewContext();
        return await Assert.ThrowsAsync<PostgresException>(() => statement(db));
    }

    private static void AssertForeignKey(string constraint, PostgresException violation)
    {
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, violation.SqlState);
        Assert.Equal(constraint, violation.ConstraintName);
    }

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
    }

    [Fact]
    public async Task Approval_round_trips_the_acquired_date_and_the_audit_diff()
    {
        var versionId = await PublishedVersions.SaveAsync(postgres, Ct);
        var approved = Active(rulesetVersionId: versionId,
            sheet: Sheet(abilities: [new CharacterAbility("riposte", null)]));
        var id = await SaveAsync(approved);

        await using var db = postgres.NewContext();
        var reloaded = await db.Characters.SingleAsync(c => c.Id == id, Ct);
        var stamped = Assert.Single(approved.Abilities).AcquiredDate;
        Assert.NotNull(stamped);
        Assert.Equal(stamped, Assert.Single(reloaded.Abilities).AcquiredDate);

        // Approval writes the character's only audit row; every stored field comes back.
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

        // The stored document, which the history view will read. The private member name must not
        // leak into it.
        var json = await db.Database
            .SqlQuery<string>($"select change::text as \"Value\" from characters.audit_entry where character_id = {id}")
            .SingleAsync(Ct);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(["Changes", "Summary"],
            document.RootElement.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal(["From", "Label", "Path", "To"],
            document.RootElement.GetProperty("Changes")[0].EnumerateObject().Select(p => p.Name).Order());
    }

    [Fact]
    public async Task A_stale_character_cannot_overwrite_a_newer_save()
    {
        var versionId = await PublishedVersions.SaveAsync(postgres, Ct);
        var id = await SaveAsync(Active(rulesetVersionId: versionId));

        await using var first = postgres.NewContext();
        await using var second = postgres.NewContext();
        var a = await first.Characters.SingleAsync(c => c.Id == id, Ct);
        var b = await second.Characters.SingleAsync(c => c.Id == id, Ct);

        a.GrantXp(Narrator, 5, "Session award", Clock());
        await first.SaveChangesAsync(Ct);

        // b still holds the xmin it loaded, so its update matches no row.
        b.GrantXp(Narrator, 3, "Session award", Clock());
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(Ct));
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

    // Every member, so a status added later is covered without anyone remembering to.
    public static TheoryData<RequestStatus> EveryStatus => new(Enum.GetValues<RequestStatus>());

    [Theory]
    [MemberData(nameof(EveryStatus))]
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
        // the database stands between it and a second request for the same item. The second is
        // always Queued, so dropping Queued from the filter fails every in-flight case at once.
        await using (var db = postgres.NewContext())
        {
            var blind = await db.Characters.SingleAsync(c => c.Id == id, Ct);
            blind.SubmitRequest(TraitRequest(blind));

            if (inFlight)
            {
                var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
                var violation = Assert.IsType<PostgresException>(exception.InnerException);
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
    public async Task A_trait_and_an_ability_with_the_same_key_can_be_in_flight_together()
    {
        var versionId = await PublishedVersions.SaveAsync(postgres, Ct);
        var character = Active(rulesetVersionId: versionId);
        character.SubmitRequest(TraitRequest(character, "riposte"));
        character.SubmitRequest(AbilityRequest(character, "riposte"));

        var id = await SaveAsync(character);

        await using var db = postgres.NewContext();
        Assert.Equal(2, await db.Set<AdvancementRequest>().CountAsync(r => r.CharacterId == id, Ct));
    }

    /// <summary>Saves an active character, then applies a saved request to it as the worker will.</summary>
    private async Task<(Guid CharacterId, Guid RequestId)> SaveAppliedAsync()
    {
        var versionId = await PublishedVersions.SaveAsync(postgres, Ct);
        var character = Active(xp: 10, rulesetVersionId: versionId);
        var request = TraitRequest(character, cost: 4);
        character.SubmitRequest(request);
        var id = await SaveAsync(character);
        Assert.NotEqual(Guid.Empty, request.Id);

        await using var db = postgres.NewContext();
        var loaded = await db.Characters.WithInFlightRequests().SingleAsync(c => c.Id == id, Ct);
        var loadedRequest = db.Set<AdvancementRequest>().Local.Single();
        loaded.ApplyAdvancement(Worker, loadedRequest, EngineVersion, Clock());
        loadedRequest.MarkApplied(Clock());
        await db.SaveChangesAsync(Ct);

        return (id, request.Id);
    }

    [Fact]
    public async Task Applying_copies_the_saved_request_id_onto_the_ledger_and_audit_rows()
    {
        var (id, requestId) = await SaveAppliedAsync();

        await using var db = postgres.NewContext();
        var spend = await db.Set<XpLedgerEntry>().SingleAsync(l => l.AdvancementRequestId == requestId, Ct);
        Assert.Equal(id, spend.CharacterId);
        var audit = await db.Set<AuditEntry>().SingleAsync(a => a.AdvancementRequestId == requestId, Ct);
        Assert.Equal(id, audit.CharacterId);
    }

    [Fact]
    public async Task Applying_a_request_that_was_never_saved_fails_at_save()
    {
        // The copy happens before EF assigns the request an id, so the rows carry Guid.Empty. The
        // FK is what turns that into an error instead of a stored row.
        var versionId = await PublishedVersions.SaveAsync(postgres, Ct);
        var character = Active(xp: 10, rulesetVersionId: versionId);
        var request = TraitRequest(character, cost: 4);
        character.SubmitRequest(request);
        character.ApplyAdvancement(Worker, request, EngineVersion, Clock());

        await using var db = postgres.NewContext();
        db.Characters.Add(character);
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
        var violation = Assert.IsType<PostgresException>(exception.InnerException);

        // Ledger or audit, whichever EF inserts first. Each FK is pinned by name below.
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, violation.SqlState);
        Assert.EndsWith("_advancement_request_advancement_request_id", violation.ConstraintName);
    }

    [Theory]
    [InlineData("fk_xp_ledger_entry_advancement_request_advancement_request_id")]
    [InlineData("fk_audit_entry_advancement_request_advancement_request_id")]
    public async Task A_copied_request_id_must_exist_and_its_request_cannot_be_deleted(string constraint)
    {
        var (id, requestId) = await SaveAppliedAsync();
        var onLedger = constraint.StartsWith("fk_xp_ledger_entry", StringComparison.Ordinal);

        // Exists: pointing the row at no request is refused.
        AssertForeignKey(constraint, await RefusedAsync(db => onLedger
            ? db.Set<XpLedgerEntry>().Where(l => l.AdvancementRequestId == requestId)
                .ExecuteUpdateAsync(s => s.SetProperty(l => l.AdvancementRequestId, Guid.CreateVersion7()), Ct)
            : db.Set<AuditEntry>().Where(a => a.AdvancementRequestId == requestId)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.AdvancementRequestId, Guid.CreateVersion7()), Ct)));

        // Restrict: with the other row's reference cleared, this one alone blocks the delete.
        await using (var db = postgres.NewContext())
        {
            if (onLedger)
            {
                await db.Set<AuditEntry>().Where(a => a.AdvancementRequestId == requestId)
                    .ExecuteUpdateAsync(s => s.SetProperty(a => a.AdvancementRequestId, (Guid?)null), Ct);
            }
            else
            {
                await db.Set<XpLedgerEntry>().Where(l => l.AdvancementRequestId == requestId)
                    .ExecuteUpdateAsync(s => s.SetProperty(l => l.AdvancementRequestId, (Guid?)null), Ct);
            }
        }

        AssertForeignKey(constraint, await RefusedAsync(db =>
            db.Set<AdvancementRequest>().Where(r => r.Id == requestId).ExecuteDeleteAsync(Ct)));
    }

    [Fact]
    public async Task A_character_on_a_ruleset_version_that_does_not_exist_is_rejected()
    {
        await using var db = postgres.NewContext();
        db.Characters.Add(Draft(rulesetVersionId: Guid.CreateVersion7()));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
        AssertForeignKey("fk_character_ruleset_version_ruleset_version_id",
            Assert.IsType<PostgresException>(exception.InnerException));
    }

    [Theory]
    [InlineData("fk_character_ruleset_version_ruleset_version_id")]
    [InlineData("fk_advancement_request_evaluated_ruleset_version")]
    [InlineData("fk_audit_entry_ruleset_version_ruleset_version_id")]
    public async Task A_draft_version_that_history_points_at_cannot_be_deleted(string constraint)
    {
        // Published versions are never deleted; drafts can be. Without these FKs, deleting one
        // would leave history pointing at nothing.
        var publishedId = await PublishedVersions.SaveAsync(postgres, Ct);
        var character = Active(rulesetVersionId: publishedId);
        character.SubmitRequest(TraitRequest(character));
        var id = await SaveAsync(character);
        var draftId = await PublishedVersions.SaveDraftAsync(postgres, Ct);

        // Point exactly one kind of row at the draft, so only its FK can block the delete.
        await using (var db = postgres.NewContext())
        {
            await (constraint switch
            {
                "fk_character_ruleset_version_ruleset_version_id" => db.Characters.Where(c => c.Id == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.RulesetVersionId, draftId), Ct),
                "fk_advancement_request_evaluated_ruleset_version" => db.Set<AdvancementRequest>().Where(r => r.CharacterId == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.EvaluatedRulesetVersionId, draftId), Ct),
                _ => db.Set<AuditEntry>().Where(a => a.CharacterId == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(a => a.RulesetVersionId, draftId), Ct),
            });
        }

        AssertForeignKey(constraint, await RefusedAsync(db =>
            db.RulesetVersions.Where(v => v.Id == draftId).ExecuteDeleteAsync(Ct)));
    }

    [Theory]
    [InlineData("fk_advancement_request_character_character_id")]
    [InlineData("fk_xp_ledger_entry_character_character_id")]
    [InlineData("fk_audit_entry_character_character_id")]
    public async Task Each_kind_of_history_blocks_deleting_its_character(string constraint)
    {
        var versionId = await PublishedVersions.SaveAsync(postgres, Ct);
        var character = Active(xp: 5, rulesetVersionId: versionId);
        character.SubmitRequest(TraitRequest(character));
        var id = await SaveAsync(character);

        // Remove the other two kinds, so only the one under test can block the delete.
        await using (var db = postgres.NewContext())
        {
            if (!constraint.StartsWith("fk_advancement_request", StringComparison.Ordinal))
            {
                await db.Set<AdvancementRequest>().Where(r => r.CharacterId == id).ExecuteDeleteAsync(Ct);
            }
            if (!constraint.StartsWith("fk_xp_ledger_entry", StringComparison.Ordinal))
            {
                await db.Set<XpLedgerEntry>().Where(l => l.CharacterId == id).ExecuteDeleteAsync(Ct);
            }
            if (!constraint.StartsWith("fk_audit_entry", StringComparison.Ordinal))
            {
                await db.Set<AuditEntry>().Where(a => a.CharacterId == id).ExecuteDeleteAsync(Ct);
            }
        }

        AssertForeignKey(constraint, await RefusedAsync(db =>
            db.Characters.Where(c => c.Id == id).ExecuteDeleteAsync(Ct)));
    }
}
