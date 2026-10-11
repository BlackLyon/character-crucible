using CharacterCrucible.EfProbes.Model;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace CharacterCrucible.EfProbes;

/// <summary>
/// The XP ledger as a navigation collection Character writes through but never loads, with the
/// running totals on Character itself.
/// </summary>
[Collection(ProbeCollection.Name)]
public class XpLedgerProbes(PostgresProbeFixture postgres, ITestOutputHelper output)
{
    private async Task<Guid> InsertWithHistoryAsync()
    {
        await using var db = postgres.NewContext();
        var character = new ProbeCharacter("Ledger");
        character.AwardXp(20);
        character.AwardXp(10);
        db.Characters.Add(character);
        await db.SaveChangesAsync();
        return character.Id;
    }

    private async Task<int> LedgerCountAsync(Guid id)
    {
        await using var db = postgres.NewContext();
        return await db.Set<ProbeLedgerEntry>().CountAsync(l => l.CharacterId == id);
    }

    [Fact]
    public async Task A_normal_load_leaves_the_ledger_unloaded_and_empty()
    {
        var id = await InsertWithHistoryAsync();

        await using var db = postgres.NewContext();
        var character = await db.Characters.SingleAsync(c => c.Id == id);

        Assert.Empty(character.XpLedgerEntries);
        Assert.Equal(30, character.UnspentXp);
        Assert.Equal(2, await LedgerCountAsync(id));
    }

    [Fact]
    public async Task Adding_to_the_unloaded_ledger_inserts_one_row_and_reads_no_history()
    {
        var id = await InsertWithHistoryAsync();

        await using (var db = postgres.NewContext())
        {
            var character = await db.Characters.SingleAsync(c => c.Id == id);
            postgres.Sql.Clear();
            character.SpendXp(12);
            await db.SaveChangesAsync();
            output.WriteLine(string.Join("\n---\n", postgres.Sql));

            Assert.Single(postgres.Sql, s => s.Contains("INSERT INTO probes.xp_ledger_entry"));
            Assert.Single(postgres.Sql, s => s.Contains("UPDATE probes.character"));
            Assert.DoesNotContain(postgres.Sql, s => s.Contains("SELECT"));
        }

        await using var reload = postgres.NewContext();
        var entries = await reload.Set<ProbeLedgerEntry>()
            .Where(l => l.CharacterId == id).OrderBy(l => l.UnspentAfter).ToListAsync();
        Assert.Equal(3, entries.Count);
        Assert.Equal((-12, 18), (entries[0].Delta, entries[0].UnspentAfter));
        Assert.Equal(18, (await reload.Characters.SingleAsync(c => c.Id == id)).UnspentXp);
    }

    // The totals live on the Character row, so xmin guards them; the losing writer's ledger row
    // must roll back with its failed UPDATE, or the ledger and the totals disagree.
    [Fact]
    public async Task A_losing_concurrent_spend_rolls_back_its_ledger_entry_too()
    {
        var id = await InsertWithHistoryAsync();

        await using var first = postgres.NewContext();
        await using var second = postgres.NewContext();
        var a = await first.Characters.SingleAsync(c => c.Id == id);
        var b = await second.Characters.SingleAsync(c => c.Id == id);

        a.SpendXp(25);
        await first.SaveChangesAsync();

        b.SpendXp(25);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());

        Assert.Equal(3, await LedgerCountAsync(id));
        await using var reload = postgres.NewContext();
        Assert.Equal(5, (await reload.Characters.SingleAsync(c => c.Id == id)).UnspentXp);
    }
}
