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
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 9, 30, 0, TimeSpan.Zero);

    private async Task<Guid> InsertAsync(params (string Key, bool Applied)[] requests)
    {
        await using var db = postgres.NewContext();
        var character = new ProbeCharacter("Requests");
        foreach (var (key, applied) in requests)
        {
            var request = character.RequestAdvancement(key, Now);
            if (applied)
            {
                request.MarkApplied();
            }
        }
        db.Characters.Add(character);
        await db.SaveChangesAsync();
        return character.Id;
    }

    private static Task<ProbeCharacter> LoadWithInFlightAsync(ProbeDbContext db, Guid id) =>
        db.Characters
            .Include(c => EF.Property<IList<ProbeRequest>>(c, "_requests").Where(r => r.Status == ProbeRequestStatus.Submitted))
            .SingleAsync(c => c.Id == id);

    private async Task<int> RequestCountAsync(Guid id)
    {
        await using var db = postgres.NewContext();
        return await db.Set<ProbeRequest>().CountAsync(r => r.CharacterId == id);
    }

    [Fact]
    public async Task A_filtered_include_on_the_private_navigation_loads_only_in_flight_requests()
    {
        // Two finished, one in flight: the limit of two would wrongly trip if history loaded.
        var id = await InsertAsync(("strength", true), ("wits", true), ("dexterity", false));

        await using var db = postgres.NewContext();
        var character = await LoadWithInFlightAsync(db, id);
        Assert.Single(db.ChangeTracker.Entries<ProbeRequest>());

        character.RequestAdvancement("athletics", Now.AddMinutes(1));
        Assert.Throws<InvalidOperationException>(() => character.RequestAdvancement("stamina", Now.AddMinutes(2)));
    }

    [Fact]
    public async Task Two_racing_submissions_cannot_both_pass_the_limit()
    {
        var id = await InsertAsync(("strength", false));

        await using var first = postgres.NewContext();
        await using var second = postgres.NewContext();
        var a = await LoadWithInFlightAsync(first, id);
        var b = await LoadWithInFlightAsync(second, id);

        // Each sees one in flight, so each passes the check in memory.
        a.RequestAdvancement("wits", Now.AddMinutes(1));
        b.RequestAdvancement("dexterity", Now.AddMinutes(1));

        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        Assert.Equal(2, await RequestCountAsync(id));
    }

    [Fact]
    public async Task The_index_refuses_a_second_in_flight_request_for_one_item_even_bypassing_the_entity()
    {
        var id = await InsertAsync(("strength", false));

        await using var db = postgres.NewContext();
        var exception = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlAsync(
            $"insert into probes.advancement_request (id, character_id, target_key, status) values ({Guid.NewGuid()}, {id}, 'strength', 1)"));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, exception.SqlState);
    }

    [Fact]
    public async Task The_index_allows_the_same_item_again_once_the_earlier_request_has_finished()
    {
        var id = await InsertAsync(("strength", true));

        await using var db = postgres.NewContext();
        var character = await LoadWithInFlightAsync(db, id);
        character.RequestAdvancement("strength", Now.AddMinutes(1));
        await db.SaveChangesAsync();

        Assert.Equal(2, await RequestCountAsync(id));
    }
}
