using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;
using static CharacterCrucible.CoreApi.Tests.Builders.TestCharacters;

namespace CharacterCrucible.CoreApi.Tests.Domain.Characters;

/// <summary>Guards on review and play status: approve, send back, ban, remove from and return to play.</summary>
public class CharacterLifecycleTests
{
    [Fact]
    public void SendBack_refuses_an_active_character_and_leaves_it_active()
    {
        var character = Active();

        Assert.Throws<InvalidOperationException>(() => character.SendBack("Needs a backstory."));

        Assert.Equal(CharacterStatus.Active, character.Status);
    }
}
