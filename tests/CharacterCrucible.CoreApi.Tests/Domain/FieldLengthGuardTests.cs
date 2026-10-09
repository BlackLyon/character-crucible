using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Entities;
using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Enums;
using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.StaticObjects;
using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.ValueObjects;

namespace CharacterCrucible.CoreApi.Tests.Domain;

/// <summary>
/// Every string column with a maximum length has a guard that rejects an overlong value at the
/// call. Without them the column rejects it instead, which surfaces as a DbUpdateException after
/// the entity has already mutated -- a published version that cannot be persisted or reversed.
/// </summary>
/// <remarks>
/// Each guard is tested at the limit and one past it, because an off-by-one here is invisible:
/// the length that fails is the one no test ever supplies.
/// </remarks>
public class FieldLengthGuardTests
{
    private static string Chars(int count) => new('x', count);

    private static readonly CostRule AnyCost = new(CostOp.Flat, CostOperand.TargetRating, 1);

    private static RulesetVersion NewDraft() =>
        new Ruleset("Guards", "d", "p").CreateDraft(1, 0, RulesetVersionKind.Release);

    [Fact]
    public void A_ruleset_name_at_the_limit_is_accepted() =>
        Assert.Equal(FieldLengths.Name, new Ruleset(Chars(FieldLengths.Name), "d", "p").Name.Length);

    [Fact]
    public void A_ruleset_name_over_the_limit_is_rejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Ruleset(Chars(FieldLengths.Name + 1), "d", "p"));

    [Fact]
    public void A_ruleset_publisher_over_the_limit_is_rejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Ruleset("n", "d", Chars(FieldLengths.Publisher + 1)));

    [Fact]
    public void A_published_ruleset_name_over_the_limit_is_rejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => NewDraft().Publish(
            Chars(FieldLengths.Name + 1), "p", TimeProvider.System, Guid.CreateVersion7(), "{}"));

    [Fact]
    public void A_published_publisher_over_the_limit_is_rejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => NewDraft().Publish(
            "n", Chars(FieldLengths.Publisher + 1), TimeProvider.System, Guid.CreateVersion7(), "{}"));

    [Fact]
    public void A_rejected_publish_leaves_the_version_a_draft()
    {
        var draft = NewDraft();

        Assert.Throws<ArgumentOutOfRangeException>(() => draft.Publish(
            Chars(FieldLengths.Name + 1), "p", TimeProvider.System, Guid.CreateVersion7(), "{}"));

        // The point of guarding at the call rather than at the column: the aggregate is
        // untouched, so there is nothing to reverse and no divergence from the database.
        Assert.Equal(RulesetVersionStatus.Draft, draft.Status);
        Assert.Null(draft.Publication);
    }

    public static TheoryData<string, Func<string, string, object>> Definitions => new()
    {
        { nameof(TraitDefinition), (key, name) => new TraitDefinition(
            Guid.Empty, key, name, "d", TraitCategory.Attribute, null, 1, 5, AnyCost, 0, true) },
        { nameof(AbilityDefinition), (key, name) => new AbilityDefinition(
            Guid.Empty, key, name, "d", AbilityDefinitionKind.Ability, AnyCost, false, [], 0, true) },
        { nameof(ArchetypeDefinition), (key, name) => new ArchetypeDefinition(
            Guid.Empty, key, name, "d", [], [], [], 0, true) },
        { nameof(DomainDefinition), (key, name) => new DomainDefinition(
            Guid.Empty, key, name, "d", [], 0) },
    };

    [Theory]
    [MemberData(nameof(Definitions))]
    public void A_definition_key_over_the_limit_is_rejected(string _, Func<string, string, object> create) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => create(Chars(FieldLengths.Key + 1), "n"));

    [Theory]
    [MemberData(nameof(Definitions))]
    public void A_definition_name_over_the_limit_is_rejected(string _, Func<string, string, object> create) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => create("k", Chars(FieldLengths.Name + 1)));

    [Theory]
    [MemberData(nameof(Definitions))]
    public void A_definition_at_the_limit_is_accepted(string _, Func<string, string, object> create) =>
        Assert.NotNull(create(Chars(FieldLengths.Key), Chars(FieldLengths.Name)));

    [Fact]
    public void A_trait_domain_key_over_the_limit_is_rejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new TraitDefinition(
            Guid.Empty, "k", "n", "d", TraitCategory.Skill,
            Chars(FieldLengths.Key + 1), 1, 5, AnyCost, 0, true));
}
