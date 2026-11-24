using CharacterCrucible.CoreApi.Modules.Characters.Domain.Entities;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;
using CharacterCrucible.CoreApi.Modules.Characters.Persistence;
using Microsoft.EntityFrameworkCore;
using static CharacterCrucible.CoreApi.Tests.Builders.TestCharacters;

namespace CharacterCrucible.CoreApi.Tests.Persistence;

/// <summary>Saves a character with one applied advancement, for tests that read what applying stored.</summary>
internal static class AppliedCharacters
{
    /// <summary>Earned 10 XP and spent 4 raising strength from 2 to 3, applied under <paramref name="engineVersion"/>.</summary>
    public static async Task<(Guid CharacterId, Guid RequestId, Guid VersionId)> SaveAsync(
        PostgresFixture postgres, string engineVersion, CancellationToken ct)
    {
        var versionId = await PublishedVersions.SaveAsync(postgres, ct);
        var character = ACharacter().InStatus(CharacterStatus.Active).OnVersion(versionId).WithXp(10).Build();
        var request = ATraitRequest(character).WithKey("strength").WithRating(3).WithCost(4).Submitted().Build();
        await using (var db = postgres.NewContext())
        {
            db.Characters.Add(character);
            await db.SaveChangesAsync(ct);
        }

        // Applied in a second context, once the request has the id the ledger and audit rows copy.
        await using (var db = postgres.NewContext())
        {
            var loaded = await db.Characters.WithInFlightRequests().SingleAsync(c => c.Id == character.Id, ct);
            var loadedRequest = db.Set<AdvancementRequest>().Local.Single();
            loaded.ApplyAdvancement(Worker, loadedRequest, engineVersion, Clock());
            loadedRequest.MarkApplied(Clock());
            await db.SaveChangesAsync(ct);
        }

        return (character.Id, request.Id, versionId);
    }
}
