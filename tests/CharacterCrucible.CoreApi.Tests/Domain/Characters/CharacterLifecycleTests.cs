using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;
using static CharacterCrucible.CoreApi.Tests.Builders.TestCharacters;

namespace CharacterCrucible.CoreApi.Tests.Domain.Characters;

/// <summary>Guards on review and play status: approve, send back, ban, remove from and return to play.</summary>
public class CharacterLifecycleTests
{
    [Fact]
    public void An_active_character_cannot_be_sent_back_and_stays_active()
    {
        var character = Active();

        Assert.Throws<InvalidOperationException>(() => character.SendBack("Needs a backstory."));

        Assert.Equal(CharacterStatus.Active, character.Status);
    }
}
