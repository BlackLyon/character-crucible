using CharacterCrucible.EfProbes.Model;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace CharacterCrucible.EfProbes;

/// <summary>Unknown 1: how the optimistic concurrency token on Character behaves.</summary>
[Collection(ProbeCollection.Name)]
public class ConcurrencyTokenProbes(PostgresProbeFixture postgres, ITestOutputHelper output)
{
    private async Task<Guid> InsertAsync(string name)
    {
        await using var db = postgres.NewContext();
        var character = new ProbeCharacter(name);
        character.SetTrait("strength", 2);
        db.Characters.Add(character);
        await db.SaveChangesAsync();
        return character.Id;
    }

    [Fact]
    public async Task Xmin_maps_to_the_system_column_and_adds_no_real_one()
    {
        await using var db = postgres.NewContext();
        var version = db.Model.FindEntityType(typeof(ProbeCharacter))!.FindProperty(nameof(ProbeCharacter.Version))!;

        Assert.Equal("xmin", version.GetColumnName());
        Assert.Equal("xid", version.GetColumnType());
        Assert.True(version.IsConcurrencyToken);

        var columns = await db.Database
            .SqlQuery<string>($"select column_name as \"Value\" from information_schema.columns where table_schema = 'probes' and table_name = 'characters'")
            .ToListAsync();
        output.WriteLine(string.Join(", ", columns));
        Assert.Contains("trait_values", columns);
        Assert.DoesNotContain("xmin", columns);
        Assert.DoesNotContain("version", columns);
    }

    [Fact]
    public async Task A_change_only_to_the_jsonb_traits_bumps_xmin_and_EF_reads_it_back()
    {
        var id = await InsertAsync("Jsonb only");

        await using var db = postgres.NewContext();
        var character = await db.Characters.SingleAsync(c => c.Id == id);
        var before = character.Version;
        Assert.NotEqual(0u, before);

        postgres.Sql.Clear();
        character.SetTrait("strength", 3);
        await db.SaveChangesAsync();
        output.WriteLine(string.Join("\n---\n", postgres.Sql));

        var after = character.Version;
        Assert.NotEqual(before, after);

        await using var reload = postgres.NewContext();
        Assert.Equal(after, (await reload.Characters.SingleAsync(c => c.Id == id)).Version);
    }

    [Fact]
    public async Task A_stale_write_from_a_second_context_throws()
    {
        var id = await InsertAsync("Two writers");

        await using var first = postgres.NewContext();
        await using var second = postgres.NewContext();
        var a = await first.Characters.SingleAsync(c => c.Id == id);
        var b = await second.Characters.SingleAsync(c => c.Id == id);

        a.SetTrait("strength", 3);
        await first.SaveChangesAsync();

        b.SetTrait("dexterity", 4);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    // The apply path: the worker evaluated against an older version, and the Core API loads fresh.
    // Setting the original value makes the UPDATE's WHERE carry the worker's version, so the
    // compare and the write are one statement.
    [Fact]
    public async Task The_workers_expected_version_can_drive_the_check_on_a_fresh_load()
    {
        var id = await InsertAsync("Moved under the request");

        uint evaluatedAgainst;
        await using (var worker = postgres.NewContext())
        {
            evaluatedAgainst = (await worker.Characters.SingleAsync(c => c.Id == id)).Version;
        }

        await using (var meanwhile = postgres.NewContext())
        {
            (await meanwhile.Characters.SingleAsync(c => c.Id == id)).SetTrait("wits", 2);
            await meanwhile.SaveChangesAsync();
        }

        await using var apply = postgres.NewContext();
        var character = await apply.Characters.SingleAsync(c => c.Id == id);
        Assert.NotEqual(evaluatedAgainst, character.Version);

        apply.Entry(character).Property(c => c.Version).OriginalValue = evaluatedAgainst;
        character.SetTrait("strength", 3);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => apply.SaveChangesAsync());
    }

    [Fact]
    public async Task An_app_managed_int_token_rejects_a_stale_write_the_same_way()
    {
        Guid id;
        await using (var db = postgres.NewContext())
        {
            var created = new CountedCharacter("Counted");
            db.CountedCharacters.Add(created);
            await db.SaveChangesAsync();
            id = created.Id;
        }

        await using var first = postgres.NewContext();
        await using var second = postgres.NewContext();
        var a = await first.CountedCharacters.SingleAsync(c => c.Id == id);
        var b = await second.CountedCharacters.SingleAsync(c => c.Id == id);

        a.SetTrait("strength", 3);
        await first.SaveChangesAsync();
        Assert.Equal(1, a.Version);

        b.SetTrait("dexterity", 4);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }
}
