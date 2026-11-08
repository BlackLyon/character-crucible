using static CharacterCrucible.CoreApi.Tests.Builders.TestCharacters;

namespace CharacterCrucible.CoreApi.Tests.Domain.Characters;

/// <summary>Guards on accepting a request within its limits, and on applying one.</summary>
public class CharacterAdvancementTests
{
    [Fact]
    public void SubmitRequest_refuses_a_second_request_for_an_item_already_in_flight()
    {
        var character = Active();
        character.SubmitRequest(TraitRequest(character, "strength"));
        var stamped = character.LastRequestSubmittedAt;

        Assert.Throws<InvalidOperationException>(() =>
            character.SubmitRequest(TraitRequest(character, "strength", rating: 4)));

        // Refused before the timestamp moved. The race guard depends on that write.
        Assert.Equal(stamped, character.LastRequestSubmittedAt);
    }
}
