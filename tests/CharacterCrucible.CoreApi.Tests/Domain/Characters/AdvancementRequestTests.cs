using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;
using static CharacterCrucible.CoreApi.Tests.Builders.TestCharacters;

namespace CharacterCrucible.CoreApi.Tests.Domain.Characters;

/// <summary>Guards on a request's own transitions: approve, reject, applied, failed, abandoned.</summary>
public class AdvancementRequestTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_rejection_without_a_reason_is_refused_and_the_request_stays_pending(string reason)
    {
        var request = TraitRequest(Active(), requiresApproval: true);

        Assert.ThrowsAny<ArgumentException>(() => request.Reject(Narrator.Id, reason, Clock()));

        Assert.Equal(RequestStatus.PendingApproval, request.RequestStatus);
        Assert.Null(request.ReviewedBy);
    }
}
