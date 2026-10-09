using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Entities;
using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Enums;
using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Security.Cryptography;
using System.Text;

namespace CharacterCrucible.CoreApi.Tests.Persistence;

/// <summary>
/// Proves the Rulesets mapping against a real Postgres: the complex collections, the optional
/// publish stamp, the two-phase save, and the indexes that back invariants 0 and 1.
/// </summary>
public class RulesetPersistenceTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    /// <summary>Deliberately has no space after the colon — jsonb would add one.</summary>
    private const string Snapshot = """{"traits":[],"abilities":[]}""";

    /// <summary>
    /// A fixed instant with non-zero minutes and seconds, so a truncated or unmapped column
    /// cannot pass by accident. Must be UTC: Npgsql rejects any other offset outright.
    /// </summary>
    private static readonly DateTimeOffset PublishedAt =
        new(2026, 3, 1, 14, 37, 9, TimeSpan.Zero);

    private static Ruleset NewRuleset(string name) => new(name, "A test system.", "Blacklyon");

    private static DomainDefinition Body(Guid versionId) =>
        new(versionId, "body", "Body", "Physical capability.",
            [new DomainBand("Average", 8), new DomainBand("Good", 12)], sortOrder: 7);

    private static TraitDefinition Strength(Guid versionId) =>
        new(versionId, "strength", "Strength", "Raw physical power.", TraitCategory.Attribute,
            "body", 1, 5, new CostRule(CostOp.Multiply, CostOperand.TargetRating, 3),
            sortOrder: 4, isAvailable: false);

    private static AbilityDefinition Hadoken(Guid versionId) =>
        new(versionId, "hadoken", "Hadoken", "A projectile of focused will.",
            AbilityDefinitionKind.Ability, new CostRule(CostOp.Flat, CostOperand.TargetRating, 8),
            requiresApproval: true,
            [
                new PrerequisiteGroup(1,
                [
                    PrerequisiteEntry.TraitAtMinimum("strength", 3),
                    PrerequisiteEntry.ArchetypeIs("world-warrior"),
                ]),
            ],
            sortOrder: 9, isAvailable: true);

    private static ArchetypeDefinition Warrior(Guid versionId) =>
        new(versionId, "warrior", "Warrior", "A dedicated combatant.",
            [new GrantedRank("strength", 1)],
            [new CostModifier("hadoken", ArchetypeTargetKind.Ability, 4)],
            [new CapModifier("strength", 6)],
            sortOrder: 3, isAvailable: false);

    [Fact]
    public async Task A_draft_round_trips_with_definitions_of_all_four_kinds()
    {
        var rulesetId = Guid.Empty;

        await using (var db = postgres.NewContext())
        {
            var ruleset = NewRuleset("Round trip");
            var draft = ruleset.CreateDraft(1, 0, RulesetVersionKind.Release);
            db.Rulesets.Add(ruleset);
            await db.SaveChangesAsync();

            // Ids exist only after the insert, which is why definitions are added in a second
            // step rather than while building the graph.
            draft.AddDefinition(Body(draft.Id));
            draft.AddDefinition(Strength(draft.Id));
            draft.AddDefinition(Hadoken(draft.Id));
            draft.AddDefinition(Warrior(draft.Id));
            await db.SaveChangesAsync();

            rulesetId = ruleset.Id;
        }

        await using (var db = postgres.NewContext())
        {
            var reloaded = await db.Rulesets
                .Include(r => r.Versions).ThenInclude(v => v.DomainDefinitions)
                .Include(r => r.Versions).ThenInclude(v => v.TraitDefinitions)
                .Include(r => r.Versions).ThenInclude(v => v.AbilityDefinitions)
                .Include(r => r.Versions).ThenInclude(v => v.ArchetypeDefinitions)
                .SingleAsync(r => r.Id == rulesetId);

            var version = Assert.Single(reloaded.Versions);

            Assert.Equal(RulesetVersionStatus.Draft, version.Status);
            Assert.Null(version.Publication);
            Assert.Equal("1.0", version.GetVersionString());
            Assert.Equal(rulesetId, version.RulesetId);

            // jsonb complex collection, flat.
            var body = Assert.Single(version.DomainDefinitions);
            Assert.Collection(body.Bands,
                b => Assert.Equal(("Average", 8), (b.Label, b.MinimumScore)),
                b => Assert.Equal(("Good", 12), (b.Label, b.MinimumScore)));

            // Complex property mapped inline as columns, not jsonb.
            var strength = Assert.Single(version.TraitDefinitions);
            Assert.Equal(CostOp.Multiply, strength.CostRule.Op);
            Assert.Equal(CostOperand.TargetRating, strength.CostRule.Operand);
            Assert.Equal(3, strength.CostRule.Factor);
            Assert.Equal("body", strength.DomainKey);

            // Every scalar the configuration maps, asserted at least once with a value that is
            // NOT the CLR default. An unmapped bool reads false and an unmapped int reads 0,
            // which look like legitimate data rather than a mapping fault -- RequiresApproval
            // silently false would bypass the approval gate for every ability, and IsAvailable
            // silently false would make character creation offer nothing.
            Assert.Equal(TraitCategory.Attribute, strength.Category);
            Assert.Equal(1, strength.MinValue);
            Assert.Equal(5, strength.MaxValue);
            Assert.Equal(4, strength.SortOrder);
            Assert.False(strength.IsAvailable);

            Assert.Equal(7, body.SortOrder);
            Assert.Equal(RulesetVersionKind.Release, version.ReleaseType);
            Assert.Equal("Round trip", reloaded.Name);
            Assert.Equal("A test system.", reloaded.Description);
            Assert.Equal("Blacklyon", reloaded.Publisher);
            Assert.Null(reloaded.DerivedFrom);
            Assert.Equal(RulesetKind.Base, reloaded.Origin);

            // A complex collection nested inside a complex collection.
            var hadoken = Assert.Single(version.AbilityDefinitions);
            var group = Assert.Single(hadoken.Prerequisites);
            Assert.Equal(1, group.RequiredCount);
            Assert.Collection(group.Entries,
                e =>
                {
                    Assert.Equal(PrerequisiteEntryKind.TraitAtMinimum, e.Kind);
                    Assert.Equal("strength", e.TargetKey);
                    Assert.Equal(3, e.MinimumRating);
                },
                e =>
                {
                    Assert.Equal(PrerequisiteEntryKind.ArchetypeIs, e.Kind);
                    Assert.Null(e.MinimumRating);
                });

            // The ability's own scalars, including the escalation flag.
            Assert.Equal(AbilityDefinitionKind.Ability, hadoken.AbilityType);
            Assert.True(hadoken.RequiresApproval);
            Assert.Equal(9, hadoken.SortOrder);
            Assert.Equal(CostOp.Flat, hadoken.CostRule.Op);

            // Three jsonb collections on one entity. Assert.Single first, so an empty collection
            // reports as empty rather than throwing IndexOutOfRange from [0].
            var warrior = Assert.Single(version.ArchetypeDefinitions);
            var rank = Assert.Single(warrior.GrantedRanks);
            Assert.Equal(("strength", 1), (rank.TraitKey, rank.Ranks));
            var costModifier = Assert.Single(warrior.CostModifiers);
            Assert.Equal(("hadoken", ArchetypeTargetKind.Ability, 4),
                (costModifier.TargetKey, costModifier.TargetKind, costModifier.Factor));
            var capModifier = Assert.Single(warrior.CapModifiers);
            Assert.Equal(("strength", 6), (capModifier.TargetKey, capModifier.MaxOverride));
            Assert.Equal(3, warrior.SortOrder);
            Assert.False(warrior.IsAvailable);
        }
    }

    [Fact]
    public async Task Publishing_and_promoting_survives_a_reload()
    {
        var rulesetId = Guid.Empty;

        await using (var db = postgres.NewContext())
        {
            var ruleset = NewRuleset("Publish and promote");
            var draft = ruleset.CreateDraft(1, 0, RulesetVersionKind.Release);
            db.Rulesets.Add(ruleset);
            await db.SaveChangesAsync();                       // phase 1

            // A deliberately non-UTC offset. timestamptz stores an instant and discards
            // the offset, so this comes back as +00:00 -- the test states that rather than
            // leaving it unexercised. UtcNow would have made the assertion vacuous.
            draft.Publish("Publish and promote", "Blacklyon", PublishedAt,
                Guid.CreateVersion7(), Snapshot);
            ruleset.SetCurrentVersion(draft);
            await db.SaveChangesAsync();                       // phase 2

            rulesetId = ruleset.Id;
        }

        await using (var db = postgres.NewContext())
        {
            var reloaded = await db.Rulesets
                .Include(r => r.Versions)
                .SingleAsync(r => r.Id == rulesetId);

            var version = Assert.Single(reloaded.Versions);

            // The derivation every guard in the module reads. An optional complex property
            // materialising as a default-valued record instead of null would make every draft
            // report Published and break editing entirely.
            Assert.Equal(RulesetVersionStatus.Published, version.Status);
            Assert.NotNull(version.Publication);
            Assert.Equal("Blacklyon", version.Publication!.Publisher);
            Assert.Equal("Publish and promote", version.Publication.RulesetName);
            Assert.NotEqual(Guid.Empty, version.Publication.PublishedBy);
            Assert.Equal(PublicationRecord.CurrentSchemaVersion, version.Publication.SchemaVersion);

            // Exact round-trip, which is also the only test of the timestamptz convention.
            // Npgsql rejects a non-UTC offset rather than converting it, so Publish can accept
            // a date that only fails at SaveChanges — see finding 18.
            Assert.Equal(PublishedAt, version.Publication.PublishedDate);

            // The snapshot must come back byte-identical, because ContentHash is SHA-256 over
            // the exact string Publish was given. This is why the column is text and not jsonb:
            // jsonb re-serialises on read, so {"traits":[]} returned as {"traits": []} and the
            // hash could never match the stored content again.
            Assert.Equal(Snapshot, version.Publication!.PublishedContent);

            // Recomputed from what the database returned, not from what we sent. This is the
            // whole feature: it is the assertion an integrity check would make later.
            var recomputed = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                version.Publication.PublishedContent)));
            Assert.Equal(recomputed, version.Publication.ContentHash);

            // Readable without Include(r => r.CurrentVersion), so "is anything published?"
            // needs no join.
            Assert.Equal(version.Id, reloaded.CurrentVersionId);
        }
    }

    [Fact]
    public async Task A_second_draft_is_rejected_by_the_database_not_only_by_the_entity()
    {
        await using var db = postgres.NewContext();

        var ruleset = NewRuleset("Partial index");
        ruleset.CreateDraft(1, 0, RulesetVersionKind.Release);
        db.Rulesets.Add(ruleset);
        await db.SaveChangesAsync();

        // Going around CreateDraft deliberately: its guard is blind whenever a Ruleset was
        // loaded without its versions, and the partial unique index is what catches that.
        var insertSecondDraft = async () => await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO rulesets.ruleset_version
                (id, ruleset_id, major_version, minor_version, release_type)
            VALUES ({0}, {1}, 2, 0, 3)
            """,
            Guid.CreateVersion7(), ruleset.Id);

        var ex = await Assert.ThrowsAsync<PostgresException>(insertSecondDraft);
        Assert.Equal("23505", ex.SqlState);     // unique_violation
        Assert.Contains("one_draft", ex.ConstraintName ?? string.Empty);
    }

    [Fact]
    public async Task A_duplicate_version_number_is_rejected_by_the_database()
    {
        await using var db = postgres.NewContext();

        var ruleset = NewRuleset("Composite index");
        var draft = ruleset.CreateDraft(3, 1, RulesetVersionKind.Release);
        db.Rulesets.Add(ruleset);
        await db.SaveChangesAsync();

        draft.Publish("Composite index", "Blacklyon", DateTimeOffset.UtcNow,
            Guid.CreateVersion7(), "{}");
        await db.SaveChangesAsync();

        // 3.1 again for the same ruleset. Published, so the one-draft index does not apply and
        // this tests the composite index alone.
        var insertDuplicate = async () => await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO rulesets.ruleset_version
                (id, ruleset_id, major_version, minor_version, release_type,
                 publication_schema_version, publication_ruleset_name, publication_publisher,
                 publication_published_date, publication_published_by,
                 publication_published_content, publication_content_hash)
            VALUES ({0}, {1}, 3, 1, 3, 1, 'x', 'y', now(), {0}, '{{}}'::jsonb, 'h')
            """,
            Guid.CreateVersion7(), ruleset.Id);

        var ex = await Assert.ThrowsAsync<PostgresException>(insertDuplicate);
        Assert.Equal("23505", ex.SqlState);
        Assert.Contains("major_minor", ex.ConstraintName ?? string.Empty);
    }

    /// <summary>
    /// The half the other index tests cannot reach: that the filter EXCLUDES published versions.
    /// </summary>
    /// <remarks>
    /// Without this, deleting <c>HasFilter</c> from the one-draft index leaves every other test
    /// green while making multi-version rulesets impossible — the index would become a plain
    /// UNIQUE(ruleset_id), so a ruleset could never hold more than one version. Verified: with
    /// the filter removed this test fails and the other seven pass.
    /// </remarks>
    [Fact]
    public async Task A_published_version_does_not_occupy_the_draft_slot()
    {
        Guid rulesetId;

        await using (var db = postgres.NewContext())
        {
            var ruleset = NewRuleset("Draft after publish");
            var first = ruleset.CreateDraft(1, 0, RulesetVersionKind.Release);
            db.Rulesets.Add(ruleset);
            await db.SaveChangesAsync();

            first.Publish("Draft after publish", "Blacklyon", DateTimeOffset.UtcNow,
                Guid.CreateVersion7(), Snapshot);
            ruleset.SetCurrentVersion(first);
            await db.SaveChangesAsync();

            // 1.0 is published, so it should no longer match the draft filter and 1.1 should
            // save without violating the unique index.
            ruleset.CreateDraft(1, 1, RulesetVersionKind.Errata);
            await db.SaveChangesAsync();

            rulesetId = ruleset.Id;
        }

        await using (var db = postgres.NewContext())
        {
            var reloaded = await db.Rulesets
                .Include(r => r.Versions)
                .SingleAsync(r => r.Id == rulesetId);

            Assert.Equal(2, reloaded.Versions.Count);
            Assert.Single(reloaded.Versions, v => v.Status == RulesetVersionStatus.Draft);
            Assert.Single(reloaded.Versions, v => v.Status == RulesetVersionStatus.Published);

            // The published one stays current; opening a draft does not promote anything.
            Assert.Equal(
                reloaded.Versions.Single(v => v.Status == RulesetVersionStatus.Published).Id,
                reloaded.CurrentVersionId);
        }
    }

    /// <summary>
    /// The authoring loop: definitions added to a draft that is already persisted, then one
    /// removed. Everything else in this file inserts a complete graph in one go.
    /// </summary>
    /// <remarks>
    /// RemoveDefinition severs a required relationship, which throws when the relationship is
    /// configured Restrict — as Ruleset.Versions now is. The definition collections are Cascade,
    /// so severing should delete the orphan instead. That difference is behaviour worth seeing
    /// rather than assuming, because the Restrict case surprised us once already.
    /// </remarks>
    [Fact]
    public async Task Definitions_can_be_added_to_and_removed_from_a_persisted_draft()
    {
        Guid rulesetId;
        Guid draftId;

        await using (var db = postgres.NewContext())
        {
            var ruleset = NewRuleset("Authoring loop");
            var draft = ruleset.CreateDraft(1, 0, RulesetVersionKind.Playtest);
            db.Rulesets.Add(ruleset);
            await db.SaveChangesAsync();

            // Added in a SECOND save, against a row that already exists.
            draft.AddDefinition(Body(draft.Id));
            draft.AddDefinition(Strength(draft.Id));
            await db.SaveChangesAsync();

            rulesetId = ruleset.Id;
            draftId = draft.Id;
        }

        await using (var db = postgres.NewContext())
        {
            var draft = await db.RulesetVersions
                .Include(v => v.DomainDefinitions)
                .Include(v => v.TraitDefinitions)
                .SingleAsync(v => v.Id == draftId);

            Assert.Single(draft.DomainDefinitions);
            Assert.Single(draft.TraitDefinitions);

            // Severing a Cascade relationship should delete the row, not orphan it or throw.
            draft.RemoveDefinition(draft.TraitDefinitions.Single());
            await db.SaveChangesAsync();
        }

        await using (var db = postgres.NewContext())
        {
            var draft = await db.RulesetVersions
                .Include(v => v.DomainDefinitions)
                .Include(v => v.TraitDefinitions)
                .SingleAsync(v => v.Id == draftId);

            Assert.Single(draft.DomainDefinitions);
            Assert.Empty(draft.TraitDefinitions);

            // Gone from the table, not merely detached from the navigation.
            Assert.Equal(0, await db.Set<TraitDefinition>().CountAsync(t => t.RulesetVersionId == draftId));

            // Still a draft throughout, so the one-draft index was never in play.
            Assert.Equal(RulesetVersionStatus.Draft, draft.Status);
            Assert.Equal(rulesetId, draft.RulesetId);
        }
    }
}
