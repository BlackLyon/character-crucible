using System.Linq.Expressions;
using CharacterCrucible.EfProbes.Model;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CharacterCrucible.EfProbes;

/// <summary>
/// At most two advancement requests in flight per character, never two for one item — enforced
/// against racing submissions.
/// </summary>
[Collection(ProbeCollection.Name)]
public class RequestLimitProbes(PostgresProbeFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset Now = new(2026, 10, 15, 9, 30, 0, TimeSpan.Zero);

    // IsInFlight is computed, so EF can't translate it: the include needs its own copy, as the real one will.
    private static readonly Expression<Func<ProbeRequest, bool>> InFlight = r =>
        r.RequestStatus == ProbeRequestStatus.Queued
        || r.RequestStatus == ProbeRequestStatus.PendingApproval
        || r.RequestStatus == ProbeRequestStatus.Approved;

    private async Task<Guid> InsertAsync(params (string Key, ProbeRequestStatus Status)[] requests)
    {
        await using var db = postgres.NewContext();
        var character = new ProbeCharacter("Requests");
        foreach (var (key, status) in requests)
        {
            character.SubmitRequest(ProbeTargetKind.Trait, key, Now).SetStatus(status);
        }
        db.Characters.Add(character);
        await db.SaveChangesAsync(Ct);
        return character.Id;
    }

    private static Task<ProbeCharacter> LoadWithInFlightAsync(ProbeDbContext db, Guid id) =>
        db.Characters
            .Include(c => EF.Property<IList<ProbeRequest>>(c, "_requests").AsQueryable().Where(InFlight))
            .SingleAsync(c => c.Id == id);

    private async Task<int> RequestCountAsync(Guid id)
    {
        await using var db = postgres.NewContext();
        return await db.Set<ProbeRequest>().CountAsync(r => r.CharacterId == id);
    }

    private Task InsertRawAsync(ProbeDbContext db, Guid characterId, ProbeTargetKind kind, string key) =>
        db.Database.ExecuteSqlAsync(
            $"insert into probes.advancement_request (id, character_id, target_kind, target_key, request_status) values ({Guid.NewGuid()}, {characterId}, {(int)kind}, {key}, {(int)ProbeRequestStatus.Queued})");

    [Theory]
    [InlineData(ProbeRequestStatus.Abandoned)]
    [InlineData(ProbeRequestStatus.Applied)]
    [InlineData(ProbeRequestStatus.Approved)]
    [InlineData(ProbeRequestStatus.Failed)]
    [InlineData(ProbeRequestStatus.PendingApproval)]
    [InlineData(ProbeRequestStatus.Queued)]
    [InlineData(ProbeRequestStatus.Rejected)]
    public async Task In_flight_means_the_same_to_IsInFlight_the_include_and_the_index(ProbeRequestStatus status)
    {
        var id = await InsertAsync(("strength", status));
        var probe = new ProbeRequest(ProbeTargetKind.Trait, "strength");
        probe.SetStatus(status);
        var inFlight = probe.IsInFlight;

        await using var db = postgres.NewContext();
        await LoadWithInFlightAsync(db, id);
        Assert.Equal(inFlight ? 1 : 0, db.ChangeTracker.Entries<ProbeRequest>().Count());

        // A second in-flight request for the same item, written around the entity.
        if (inFlight)
        {
            var exception = await Assert.ThrowsAsync<PostgresException>(() => InsertRawAsync(db, id, ProbeTargetKind.Trait, "strength"));
            Assert.Equal(PostgresErrorCodes.UniqueViolation, exception.SqlState);
        }
        else
        {
            await InsertRawAsync(db, id, ProbeTargetKind.Trait, "strength");
            Assert.Equal(2, await RequestCountAsync(id));
        }
    }

    [Fact]
    public async Task A_filtered_include_on_the_private_navigation_loads_only_in_flight_requests()
    {
        // Two finished, one in flight: the limit of two would wrongly trip if history loaded.
        var id = await InsertAsync(("strength", ProbeRequestStatus.Applied), ("wits", ProbeRequestStatus.Rejected), ("dexterity", ProbeRequestStatus.PendingApproval));

        await using var db = postgres.NewContext();
        var character = await LoadWithInFlightAsync(db, id);
        Assert.Single(db.ChangeTracker.Entries<ProbeRequest>());

        character.SubmitRequest(ProbeTargetKind.Trait, "athletics", Now.AddMinutes(1));
        Assert.Throws<InvalidOperationException>(() => character.SubmitRequest(ProbeTargetKind.Trait, "stamina", Now.AddMinutes(2)));
    }

    [Fact]
    public async Task Two_racing_submissions_cannot_both_pass_the_limit()
    {
        var id = await InsertAsync(("strength", ProbeRequestStatus.Queued));

        await using var first = postgres.NewContext();
        await using var second = postgres.NewContext();
        var a = await LoadWithInFlightAsync(first, id);
        var b = await LoadWithInFlightAsync(second, id);

        // Each sees one in flight, so each passes the check in memory.
        a.SubmitRequest(ProbeTargetKind.Trait, "wits", Now.AddMinutes(1));
        b.SubmitRequest(ProbeTargetKind.Trait, "dexterity", Now.AddMinutes(1));

        await first.SaveChangesAsync(Ct);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(Ct));
        Assert.Equal(2, await RequestCountAsync(id));
    }

    [Fact]
    public async Task Without_the_timestamp_write_two_racing_submissions_both_pass()
    {
        var id = await InsertAsync(("strength", ProbeRequestStatus.Queued));

        await using var first = postgres.NewContext();
        await using var second = postgres.NewContext();
        var a = await LoadWithInFlightAsync(first, id);
        var b = await LoadWithInFlightAsync(second, id);

        // Only the request rows change, so the character row is never written and RowVersion never compared.
        a.SubmitRequest(ProbeTargetKind.Trait, "wits", Now.AddMinutes(1), writeTimestamp: false);
        b.SubmitRequest(ProbeTargetKind.Trait, "dexterity", Now.AddMinutes(1), writeTimestamp: false);

        await first.SaveChangesAsync(Ct);
        await second.SaveChangesAsync(Ct);
        Assert.Equal(3, await RequestCountAsync(id));
    }

    [Fact]
    public async Task The_index_allows_a_trait_and_an_ability_with_the_same_key_in_flight_together()
    {
        var id = await InsertAsync(("shadow", ProbeRequestStatus.Queued));

        await using var db = postgres.NewContext();
        await InsertRawAsync(db, id, ProbeTargetKind.Ability, "shadow");

        Assert.Equal(2, await RequestCountAsync(id));
    }
}
