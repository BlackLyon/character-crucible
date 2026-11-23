using CharacterCrucible.CoreApi.Modules.Characters.Domain.Entities;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.ValueObjects;
using static CharacterCrucible.CoreApi.Tests.Builders.TestCharacters;

namespace CharacterCrucible.CoreApi.Tests.Domain.Characters;

/// <summary>Guards on building and editing the sheet before approval.</summary>
public class CharacterDraftTests
{
    [Fact]
    public void Updating_a_draft_replaces_the_whole_sheet()
    {
        var original = ADraft().WithAbilities("riposte").Build();
        var replacement = ADraft().WithTraits(new CharacterTrait("wits", 3)).Build();
        var character = ACharacter().WithSheet(original).Build();

        character.UpdateDraft(replacement);

        // Replaced, not merged: traits and abilities missing from the new sheet are gone.
        Assert.Equal(replacement.Traits, character.Traits);
        Assert.Empty(character.Abilities);
    }

    [Fact]
    public void A_character_cannot_be_created_without_an_owner()
    {
        var versionId = Guid.CreateVersion7();

        var refused = Assert.Throws<ArgumentOutOfRangeException>(() => new Character(Guid.Empty, versionId, ADraft().Build(), Clock()));

        Assert.Equal("ownerId", refused.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Updating_a_draft_refuses_a_trait_rated_zero_or_below_and_keeps_the_sheet(int rating)
    {
        var original = ADraft().Build();
        var invalid = ADraft().WithTraits(new CharacterTrait("strength", rating)).Build();
        var character = ACharacter().WithSheet(original).Build();

        var refused = Assert.Throws<ArgumentOutOfRangeException>(() => character.UpdateDraft(invalid));

        Assert.Equal("Rating", refused.ParamName);
        Assert.Equal(original.Traits, character.Traits);
    }

    [Fact]
    public void Updating_a_draft_refuses_an_ability_that_already_has_an_acquired_date()
    {
        // Approval sets the date. A draft that brought its own could backdate an ability.
        var original = ADraft().WithAbilities("parry").Build();
        var dated = ADraft().WithDatedAbility("riposte", Now).Build();
        var character = ACharacter().WithSheet(original).Build();

        var refused = Assert.Throws<ArgumentException>(() => character.UpdateDraft(dated));

        Assert.Equal("abilities", refused.ParamName);
        Assert.Equal(original.Abilities, character.Abilities);
    }

    [Fact]
    public void Updating_a_draft_refuses_the_same_ability_twice()
    {
        var original = ADraft().WithAbilities("parry").Build();
        var duplicated = ADraft().WithAbilities("riposte", "riposte").Build();
        var character = ACharacter().WithSheet(original).Build();

        var refused = Assert.Throws<ArgumentException>(() => character.UpdateDraft(duplicated));

        Assert.Equal("abilities", refused.ParamName);
        Assert.Equal(original.Abilities, character.Abilities);
    }
}
