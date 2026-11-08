using static CharacterCrucible.CoreApi.Tests.Builders.TestCharacters;

namespace CharacterCrucible.CoreApi.Tests.Domain.Characters;

/// <summary>Guards on accepting a request within its limits, and on applying one.</summary>
public class CharacterAdvancementTests
{
    [Fact]
    public void A_character_awaiting_review_cannot_submit_a_request()
    {
        var character = PendingApproval();

        Assert.Throws<InvalidOperationException>(() => character.SubmitRequest(TraitRequest(character)));

        // Null, not merely unchanged: it has never been set, so any write would show.
        Assert.Null(character.LastRequestSubmittedAt);
    }
}
