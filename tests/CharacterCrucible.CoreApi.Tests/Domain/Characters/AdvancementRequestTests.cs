using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;
using static CharacterCrucible.CoreApi.Tests.Builders.TestCharacters;

namespace CharacterCrucible.CoreApi.Tests.Domain.Characters;

/// <summary>Guards on a request's own transitions: approve, reject, applied, failed, abandoned.</summary>
public class AdvancementRequestTests
{
    [Fact]
    public void Reject_records_the_reviewer_the_reason_and_when()
    {
        var request = TraitRequest(Active(), requiresApproval: true);

        request.Reject(Narrator.Id, "Not this session.", Clock());

        Assert.Equal(RequestStatus.Rejected, request.RequestStatus);
        Assert.Equal(Narrator.Id, request.ReviewedBy);
        Assert.Equal("Not this session.", request.ResolutionReason);
        Assert.Equal((Now, Now), (request.ReviewedDate, request.ResolvedDate));
    }
}
