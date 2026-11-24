using CharacterCrucible.CoreApi.Modules.Characters.Domain.Entities;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;
using CharacterCrucible.CoreApi.Modules.Characters.Persistence;
using Microsoft.EntityFrameworkCore;
using static CharacterCrucible.CoreApi.Tests.Builders.TestCharacters;

namespace CharacterCrucible.CoreApi.Tests.Persistence;

/// <summary>
/// Apply-time guards whose refused case no sequence of domain calls can reach: only stored state
/// that changed after the request was accepted, or was written without going through the domain.
/// </summary>
public class ApplyRevalidationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Applying_refuses_a_request_the_character_can_no_longer_afford()
    {
        var versionId = await PublishedVersions.SaveAsync(postgres, Ct);
        var character = ACharacter().InStatus(CharacterStatus.Active).OnVersion(versionId).WithXp(3).Build();
        var request = ATraitRequest(character).WithCost(3).Submitted().Build();
        await using (var db = postgres.NewContext())
        {
            db.Characters.Add(character);
            await db.SaveChangesAsync(Ct);
        }

        // Nothing in the domain lowers XP, so a stored change stands in for whatever did: a
        // redelivered apply, a correction, a fix made in SQL.
        await using (var db = postgres.NewContext())
        {
            await db.Characters.Where(c => c.Id == character.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.UnspentXp, 2), Ct);
        }

        await using var worker = postgres.NewContext();
        var loaded = await worker.Characters.WithInFlightRequests().SingleAsync(c => c.Id == character.Id, Ct);
        var loadedRequest = worker.Set<AdvancementRequest>().Local.Single();

        // Exact type: without the check, the ledger entry still refuses the negative balance, but as an
        // ArgumentOutOfRangeException, which reads as a bug rather than as a player who cannot afford it.
        Assert.Throws<InvalidOperationException>(() => loaded.ApplyAdvancement(Worker, loadedRequest, EngineVersion, Clock()));

        Assert.Equal(2, loaded.UnspentXp);
        Assert.Equal(character.Traits, loaded.Traits);
    }

    [Fact]
    public async Task Applying_refuses_a_trait_request_with_no_rating()
    {
        var versionId = await PublishedVersions.SaveAsync(postgres, Ct);
        var character = ACharacter().InStatus(CharacterStatus.Active).OnVersion(versionId).WithXp(5).Build();
        // A trait the sheet doesn't hold: for a held one, the rating comparison would refuse it anyway.
        var request = ATraitRequest(character).WithKey("agility").WithCost(3).Submitted().Build();
        await using (var db = postgres.NewContext())
        {
            db.Characters.Add(character);
            await db.SaveChangesAsync(Ct);
        }

        // ForTrait requires a rating, but the column is nullable for ability requests, so a row
        // written outside the domain can still hold a trait without one.
        await using (var db = postgres.NewContext())
        {
            await db.Set<AdvancementRequest>().Where(r => r.Id == request.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.TargetRating, (int?)null), Ct);
        }

        await using var worker = postgres.NewContext();
        var loaded = await worker.Characters.WithInFlightRequests().SingleAsync(c => c.Id == character.Id, Ct);
        var loadedRequest = worker.Set<AdvancementRequest>().Local.Single();

        Assert.Throws<InvalidOperationException>(() => loaded.ApplyAdvancement(Worker, loadedRequest, EngineVersion, Clock()));

        Assert.Equal(5, loaded.UnspentXp);
        Assert.Equal(character.Traits, loaded.Traits);
    }
}
