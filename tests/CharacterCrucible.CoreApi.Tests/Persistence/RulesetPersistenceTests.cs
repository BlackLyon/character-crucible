using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Entities;
using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Enums;
using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CharacterCrucible.CoreApi.Tests.Persistence;

/// <summary>
/// Proves the Rulesets mapping against a real Postgres: the complex collections, the optional
/// publish stamp, the two-phase save, and the indexes that back invariants 0 and 1.
/// </summary>
public class RulesetPersistenceTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static Ruleset NewRuleset(string name) => new(name, "A test system.", "Blacklyon");

    private static DomainDefinition Body(Guid versionId) =>
        new(versionId, "body", "Body", "Physical capability.",
            [new DomainBand("Average", 8), new DomainBand("Good", 12)], 0);

    private static TraitDefinition Strength(Guid versionId) =>
        new(versionId, "strength", "Strength", "Raw physical power.", TraitCategory.Attribute,
            "body", 1, 5, new CostRule(CostOp.Multiply, CostOperand.TargetRating, 3), 0, true);

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
            sortOrder: 0, isAvailable: true);

    private static ArchetypeDefinition Warrior(Guid versionId) =>
        new(versionId, "warrior", "Warrior", "A dedicated combatant.",
            [new GrantedRank("strength", 1)],
            [new CostModifier("hadoken", ArchetypeTargetKind.Ability, 4)],
            [new CapModifier("strength", 6)],
            sortOrder: 0, isAvailable: true);

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
            Assert.Equal(3, strength.CostRule.Factor);
            Assert.Equal("body", strength.DomainKey);

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

            // Three jsonb collections on one entity.
            var warrior = Assert.Single(version.ArchetypeDefinitions);
            Assert.Equal(("strength", 1), (warrior.GrantedRanks[0].TraitKey, warrior.GrantedRanks[0].Ranks));
            Assert.Equal(ArchetypeTargetKind.Ability, warrior.CostModifiers[0].TargetKind);
            Assert.Equal(6, warrior.CapModifiers[0].MaxOverride);
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

            draft.Publish("Publish and promote", "Blacklyon", DateTimeOffset.UtcNow,
                Guid.CreateVersion7(), """{"traits":[]}""");
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
            Assert.Equal(PublicationRecord.CurrentSchemaVersion, version.Publication.SchemaVersion);

            // SHA-256 of {"traits":[]}, computed inside Publish and never supplied by a caller.
            Assert.Equal(64, version.Publication.ContentHash.Length);

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
}
