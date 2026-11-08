using static CharacterCrucible.CoreApi.Tests.Builders.TestCharacters;

namespace CharacterCrucible.CoreApi.Tests.Domain.Characters;

/// <summary>Guards on granting XP.</summary>
public class CharacterXpTests
{
    [Fact]
    public void GrantXp_refuses_a_character_not_yet_in_play_and_leaves_its_balances()
    {
        var character = PendingApproval();

        Assert.Throws<InvalidOperationException>(() => character.GrantXp(Narrator, 5, "Session award", Clock()));

        Assert.Equal((0, 0), (character.TotalEarnedXp, character.UnspentXp));
    }
}
