using CharacterCrucible.CoreApi.Modules.Characters.Domain.ValueObjects;
using static CharacterCrucible.CoreApi.Tests.Builders.TestCharacters;

namespace CharacterCrucible.CoreApi.Tests.Domain.Characters;

/// <summary>Guards on building and editing the sheet before approval.</summary>
public class CharacterDraftTests
{
    [Fact]
    public void Updating_a_draft_replaces_the_whole_sheet()
    {
        var character = Draft(sheet: Sheet(
            traits: [new CharacterTrait("strength", 2), new CharacterTrait("swordsmanship", 1)],
            abilities: [new CharacterAbility("riposte", null)]));

        character.UpdateDraft(Sheet(traits: [new CharacterTrait("wits", 3)], abilities: []));

        // Replaced, not merged: traits and abilities missing from the new sheet are gone.
        Assert.Equal([new CharacterTrait("wits", 3)], character.Traits);
        Assert.Empty(character.Abilities);
    }
}
