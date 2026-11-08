using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Entities;
using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Enums;

namespace CharacterCrucible.CoreApi.Tests.Persistence;

/// <summary>Saves a published ruleset version, because every saved character needs a real one.</summary>
internal static class PublishedVersions
{
    /// <summary>A new ruleset each call, so tests sharing a database never share a version.</summary>
    public static async Task<Guid> SaveAsync(PostgresFixture postgres, CancellationToken ct)
    {
        await using var db = postgres.NewContext();
        var ruleset = new Ruleset($"Test system {Guid.CreateVersion7()}", "For tests.", "Blacklyon");
        var draft = ruleset.CreateDraft(1, 0, RulesetVersionKind.Release);
        db.Rulesets.Add(ruleset);
        await db.SaveChangesAsync(ct);                       // phase 1: both rows exist

        draft.Publish(ruleset.Name, ruleset.Publisher, TimeProvider.System, Guid.CreateVersion7(), "{}");
        ruleset.SetCurrentVersion(draft);
        await db.SaveChangesAsync(ct);                       // phase 2

        return draft.Id;
    }
}
