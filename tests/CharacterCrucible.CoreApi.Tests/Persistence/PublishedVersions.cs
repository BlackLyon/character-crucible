using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Entities;
using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Enums;
using CharacterCrucible.CoreApi.Persistence;

namespace CharacterCrucible.CoreApi.Tests.Persistence;

/// <summary>Saves ruleset versions for persistence tests, because every saved character needs a real one.</summary>
/// <remarks>A new ruleset each call, so tests sharing a database never share a version.</remarks>
internal static class PublishedVersions
{
    public static async Task<Guid> SaveAsync(PostgresFixture postgres, CancellationToken ct)
    {
        await using var db = postgres.NewContext();
        var (ruleset, draft) = await SaveRulesetWithDraftAsync(db, ct);   // phase 1: both rows exist

        draft.Publish(ruleset.Name, ruleset.Publisher, TimeProvider.System, Guid.CreateVersion7(), "{}");
        ruleset.SetCurrentVersion(draft);
        await db.SaveChangesAsync(ct);                                     // phase 2

        return draft.Id;
    }

    /// <summary>An unpublished version: the kind that can be deleted, so the kind the FKs protect.</summary>
    public static async Task<Guid> SaveDraftAsync(PostgresFixture postgres, CancellationToken ct)
    {
        await using var db = postgres.NewContext();
        var (_, draft) = await SaveRulesetWithDraftAsync(db, ct);
        return draft.Id;
    }

    private static async Task<(Ruleset, RulesetVersion)> SaveRulesetWithDraftAsync(
        CharacterCrucibleDbContext db, CancellationToken ct)
    {
        var ruleset = new Ruleset($"Test system {Guid.CreateVersion7()}", "A test system.", "Blacklyon");
        var draft = ruleset.CreateDraft(1, 0, RulesetVersionKind.Release);
        db.Rulesets.Add(ruleset);
        await db.SaveChangesAsync(ct);
        return (ruleset, draft);
    }
}
