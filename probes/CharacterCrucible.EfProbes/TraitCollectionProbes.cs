using CharacterCrucible.EfProbes.Model;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace CharacterCrucible.EfProbes;

/// <summary>Unknown 2: whether the private jsonb trait collection supports lookup by key.</summary>
[Collection(ProbeCollection.Name)]
public class TraitCollectionProbes(PostgresProbeFixture postgres, ITestOutputHelper output)
{
    private async Task<Guid> InsertAsync(string name, params (string Key, int Rating)[] traits)
    {
        await using var db = postgres.NewContext();
        var character = new ProbeCharacter(name);
        foreach (var (key, rating) in traits)
        {
            character.SetTrait(key, rating);
        }
        db.Characters.Add(character);
        await db.SaveChangesAsync();
        return character.Id;
    }

    [Fact]
    public async Task Replacing_an_entry_by_key_persists_in_place()
    {
        var id = await InsertAsync("Replace", ("strength", 2), ("dexterity", 4), ("wits", 1));

        await using (var db = postgres.NewContext())
        {
            var character = await db.Characters.SingleAsync(c => c.Id == id);
            postgres.Sql.Clear();
            character.SetTrait("dexterity", 5);
            await db.SaveChangesAsync();
            output.WriteLine(string.Join("\n---\n", postgres.Sql));
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
        var qualifies = await InsertAsync("Qualifies", ("strength", 3), ("athletics", 1));
        await InsertAsync("Too low", ("strength", 2));
        await InsertAsync("No such trait", ("athletics", 5));

        await using var db = postgres.NewContext();
        var query = db.Characters
            .Where(c => c.Name == "Qualifies" || c.Name == "Too low" || c.Name == "No such trait")
            .Where(c => EF.Property<IList<TraitValue>>(c, "TraitValues").Any(t => t.TraitKey == "strength" && t.Rating >= 3))
            .Select(c => c.Id);
        output.WriteLine(query.ToQueryString());

        Assert.Equal([qualifies], await query.ToListAsync());
    }

    [Fact]
    public async Task One_rating_can_be_projected_by_key_in_the_database()
    {
        var id = await InsertAsync("Project", ("strength", 2), ("dexterity", 4));

        await using var db = postgres.NewContext();
        var query = db.Characters
            .Where(c => c.Id == id)
            .Select(c => EF.Property<IList<TraitValue>>(c, "TraitValues").Where(t => t.TraitKey == "dexterity").Select(t => (int?)t.Rating).FirstOrDefault());
        output.WriteLine(query.ToQueryString());

        Assert.Equal(4, await query.SingleAsync());
    }
}
