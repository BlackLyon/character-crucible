using CharacterCrucible.EfProbes.Model;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace CharacterCrucible.EfProbes;

/// <summary>
/// Unknown 2: lookup by key in the private jsonb collections. The Rules evaluator's own lookup is
/// in memory on a payload, so these cover the Core API side: replacing by key, and querying.
/// </summary>
[Collection(ProbeCollection.Name)]
public class TraitCollectionProbes(PostgresProbeFixture postgres, ITestOutputHelper output)
{
    private async Task<Guid> InsertAsync(params (string Key, int Rating)[] traits)
    {
        await using var db = postgres.NewContext();
        var character = new ProbeCharacter("Probe");
        foreach (var (key, rating) in traits)
        {
            character.SetTrait(key, rating);
        }
        db.Characters.Add(character);
        await db.SaveChangesAsync();
        return character.Id;
    }

    [Fact]
    public async Task Replacing_an_entry_by_key_persists_keeps_order_and_rewrites_the_whole_column()
    {
        var id = await InsertAsync(("strength", 2), ("dexterity", 4), ("wits", 1));

        await using (var db = postgres.NewContext())
        {
            var character = await db.Characters.SingleAsync(c => c.Id == id);
            postgres.Sql.Clear();
            character.SetTrait("dexterity", 5);
            await db.SaveChangesAsync();
            output.WriteLine(string.Join("\n---\n", postgres.Sql));
            Assert.Contains("SET traits = @", Assert.Single(postgres.Sql, s => s.Contains("UPDATE")));
        }

        await using var reload = postgres.NewContext();
        var traits = (await reload.Characters.SingleAsync(c => c.Id == id)).Traits;
        Assert.Equal(
            [new TraitValue("strength", 2), new TraitValue("dexterity", 5), new TraitValue("wits", 1)],
            traits);
    }

    // The "who qualifies for ability X" query from the persistence falsifier in characters.md.
    [Fact]
    public async Task A_filter_on_trait_key_and_rating_runs_in_the_database()
    {
        var qualifies = await InsertAsync(("strength", 3), ("athletics", 1));
        var tooLow = await InsertAsync(("strength", 2));
        var noSuchTrait = await InsertAsync(("athletics", 5));
        Guid[] ids = [qualifies, tooLow, noSuchTrait];

        await using var db = postgres.NewContext();
        var query = db.Characters
            .Where(c => ids.Contains(c.Id))
            .Where(c => EF.Property<IList<TraitValue>>(c, "_traits").Any(t => t.TraitKey == "strength" && t.Rating >= 3))
            .Select(c => c.Id);
        var sql = query.ToQueryString();
        output.WriteLine(sql);

        Assert.Contains("jsonb_to_recordset", sql);
        Assert.Equal([qualifies], await query.ToListAsync());
    }

    // A projection, unlike a Where, may silently fall back to client evaluation, so the SQL is
    // what proves this ran in the database.
    [Fact]
    public async Task One_rating_is_projected_by_key_in_the_database_and_a_missing_key_is_null()
    {
        var id = await InsertAsync(("strength", 2), ("dexterity", 4));

        await using var db = postgres.NewContext();
        IQueryable<int?> RatingOf(string key) => db.Characters
            .Where(c => c.Id == id)
            .Select(c => EF.Property<IList<TraitValue>>(c, "_traits").Where(t => t.TraitKey == key).Select(t => (int?)t.Rating).FirstOrDefault());

        var sql = RatingOf("dexterity").ToQueryString();
        output.WriteLine(sql);
        Assert.Contains("jsonb_to_recordset", sql);

        Assert.Equal(4, await RatingOf("dexterity").SingleAsync());
        Assert.Null(await RatingOf("wits").SingleAsync());
    }

    // Character holds two jsonb collections, and timestamptz conventions do not reach inside JSON.
    [Fact]
    public async Task A_second_jsonb_collection_round_trips_a_DateTimeOffset_with_its_offset()
    {
        var acquired = new DateTimeOffset(2026, 3, 1, 14, 37, 9, TimeSpan.FromHours(5.5));
        Guid id;
        await using (var db = postgres.NewContext())
        {
            var character = new ProbeCharacter("Abilities");
            character.SetTrait("strength", 2);
            character.AddAbility("hadoken", acquired);
            db.Characters.Add(character);
            await db.SaveChangesAsync();
            id = character.Id;
        }

        await using var reload = postgres.NewContext();
        var held = Assert.Single((await reload.Characters.SingleAsync(c => c.Id == id)).Abilities);
        Assert.Equal("hadoken", held.AbilityKey);
        Assert.Equal(acquired, held.AcquiredDate);
        Assert.Equal(acquired.Offset, held.AcquiredDate.Offset);
    }
}
