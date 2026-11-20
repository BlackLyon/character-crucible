using CharacterCrucible.CoreApi.Modules.Characters.Domain.Entities;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;
using static CharacterCrucible.CoreApi.Tests.Builders.TestCharacters;

namespace CharacterCrucible.CoreApi.Tests.Domain.Characters;

/// <summary>Guards on a request: how it is built, and its own transitions (approve, reject, applied, failed, abandoned).</summary>
public class AdvancementRequestTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_rejection_without_a_reason_is_refused_and_the_request_stays_pending(string reason)
    {
        var character = ACharacter().InStatus(CharacterStatus.Active).Build();
        var request = ATraitRequest(character).Gated().Build();

        Assert.ThrowsAny<ArgumentException>(() => request.Reject(Narrator.Id, reason, Clock()));

        Assert.Equal(RequestStatus.PendingApproval, request.RequestStatus);
        Assert.Null(request.ReviewedBy);
    }

    [Fact]
    public void ForAbility_builds_an_ability_request_with_no_rating()
    {
        var versionId = Guid.CreateVersion7();

        var request = AdvancementRequest.ForAbility(Player.Id, Clock(), "riposte", 4, versionId, EngineVersion, requiresApproval: false);

        Assert.Equal((TargetKind.Ability, "riposte", null), (request.TargetKind, request.TargetKey, request.TargetRating));
    }

    [Fact]
    public void A_request_cannot_be_approved_without_a_reviewer_and_stays_pending()
    {
        var character = ACharacter().InStatus(CharacterStatus.Active).Build();
        var request = ATraitRequest(character).Gated().Build();

        var refused = Assert.Throws<ArgumentOutOfRangeException>(() => request.Approve(Guid.Empty, Clock()));

        Assert.Equal("reviewer", refused.ParamName);
        Assert.Equal(RequestStatus.PendingApproval, request.RequestStatus);
        Assert.Null(request.ReviewedBy);
    }
}
