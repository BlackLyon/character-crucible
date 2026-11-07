using CharacterCrucible.EfProbes.Model;
using Microsoft.EntityFrameworkCore;

namespace CharacterCrucible.EfProbes;

/// <summary>Unknown 1: how the optimistic concurrency token on Character behaves.</summary>
[Collection(ProbeCollection.Name)]
public class ConcurrencyTokenProbes(PostgresProbeFixture postgres, ITestOutputHelper output)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<Guid> InsertAsync()
    {
        await using var db = postgres.NewContext();
        var character = new ProbeCharacter("Probe");
        character.SetTrait("strength", 2);
        db.Characters.Add(character);
        await db.SaveChangesAsync(Ct);
        return character.Id;
    }

    private async Task<uint> CurrentVersionAsync(Guid id)
    {
        await using var db = postgres.NewContext();
        return (await db.Characters.SingleAsync(c => c.Id == id, Ct)).RowVersion;
    }

    private string SingleUpdate()
    {
        output.WriteLine(string.Join("\n---\n", postgres.Sql));
        return Assert.Single(postgres.Sql, s => s.Contains("UPDATE"));
    }

    [Fact]
    public async Task Xmin_maps_to_the_system_column_and_adds_no_real_one()
    {
        await using var db = postgres.NewContext();
        var version = db.Model.FindEntityType(typeof(ProbeCharacter))!.FindProperty(nameof(ProbeCharacter.RowVersion))!;

        Assert.Equal("xmin", version.GetColumnName());
        Assert.Equal("xid", version.GetColumnType());
        Assert.True(version.IsConcurrencyToken);

        var columns = await db.Database
            .SqlQuery<string>($"select column_name as \"Value\" from information_schema.columns where table_schema = 'probes' and table_name = 'character'")
            .ToListAsync(Ct);
        output.WriteLine(string.Join(", ", columns));
        Assert.Contains("traits", columns);
        Assert.DoesNotContain("xmin", columns);
        Assert.DoesNotContain("row_version", columns);
    }

    [Fact]
    public async Task A_jsonb_only_change_bumps_xmin_and_one_UPDATE_reads_it_back()
    {
        var id = await InsertAsync();

        await using var db = postgres.NewContext();
        var character = await db.Characters.SingleAsync(c => c.Id == id, Ct);
        var before = character.RowVersion;
        Assert.NotEqual(0u, before);

        postgres.Sql.Clear();
        character.SetTrait("strength", 3);
        await db.SaveChangesAsync(Ct);

        var update = SingleUpdate();
        Assert.Contains("xmin = @", update);
        Assert.Contains("RETURNING xmin", update);
        Assert.NotEqual(before, character.RowVersion);
        Assert.Equal(character.RowVersion, await CurrentVersionAsync(id));
    }

    [Fact]
    public async Task A_stale_write_from_a_second_context_throws()
    {
        var id = await InsertAsync();

        await using var first = postgres.NewContext();
        await using var second = postgres.NewContext();
        var a = await first.Characters.SingleAsync(c => c.Id == id, Ct);
        var b = await second.Characters.SingleAsync(c => c.Id == id, Ct);

        a.SetTrait("strength", 3);
        await first.SaveChangesAsync(Ct);

        b.SetTrait("dexterity", 4);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(Ct));
    }

    // The apply path: the Core API loads fresh, then checks against the version the worker
    // evaluated, inside the UPDATE itself rather than as a separate read.
    [Fact]
    public async Task The_workers_version_as_original_value_applies_when_current_and_throws_when_stale()
    {
        var id = await InsertAsync();
        var evaluatedAgainst = await CurrentVersionAsync(id);

        await using (var apply = postgres.NewContext())
        {
            var character = await apply.Characters.SingleAsync(c => c.Id == id, Ct);
            apply.Entry(character).Property(c => c.RowVersion).OriginalValue = evaluatedAgainst;
            character.SetTrait("strength", 3);
            postgres.Sql.Clear();
            await apply.SaveChangesAsync(Ct);
            Assert.Contains("xmin = @", SingleUpdate());
        }

        // That apply moved the character on, so the worker's version is now stale.
        await using (var apply = postgres.NewContext())
        {
            var character = await apply.Characters.SingleAsync(c => c.Id == id, Ct);
            Assert.NotEqual(evaluatedAgainst, character.RowVersion);
            apply.Entry(character).Property(c => c.RowVersion).OriginalValue = evaluatedAgainst;
            character.SetTrait("strength", 4);
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => apply.SaveChangesAsync(Ct));
        }
    }

    [Fact]
    public async Task An_app_managed_int_token_rejects_a_stale_write_the_same_way()
    {
        Guid id;
        await using (var db = postgres.NewContext())
        {
            var created = new CountedCharacter("Counted");
            db.CountedCharacters.Add(created);
            await db.SaveChangesAsync(Ct);
            id = created.Id;
        }

        await using var first = postgres.NewContext();
        await using var second = postgres.NewContext();
        var a = await first.CountedCharacters.SingleAsync(c => c.Id == id, Ct);
        var b = await second.CountedCharacters.SingleAsync(c => c.Id == id, Ct);

        a.SetTrait("strength", 3);
        await first.SaveChangesAsync(Ct);

        b.SetTrait("dexterity", 4);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(Ct));

        await using var reload = postgres.NewContext();
        Assert.Equal(1, (await reload.CountedCharacters.SingleAsync(c => c.Id == id, Ct)).RowVersion);
    }
}
