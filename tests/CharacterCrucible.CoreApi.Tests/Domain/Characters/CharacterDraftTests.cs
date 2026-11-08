using CharacterCrucible.CoreApi.Modules.Characters.Domain.ValueObjects;
using static CharacterCrucible.CoreApi.Tests.Builders.TestCharacters;

namespace CharacterCrucible.CoreApi.Tests.Domain.Characters;

/// <summary>Guards on building and editing the sheet before approval.</summary>
public class CharacterDraftTests
{
    [Fact]
    public void UpdateDraft_refuses_a_character_awaiting_review_and_keeps_its_sheet()
    {
        var character = PendingApproval();
        var before = character.Traits.ToList();

        Assert.Throws<InvalidOperationException>(() =>
            character.UpdateDraft(Sheet(traits: [new CharacterTrait("strength", 4)])));

        Assert.Equal(before, character.Traits);
    }
}
