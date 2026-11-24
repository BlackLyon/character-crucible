using CharacterCrucible.CoreApi.Modules.Characters.Domain.Entities;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;
using CharacterCrucible.CoreApi.Tests.Builders;
using Microsoft.Extensions.Time.Testing;
using static CharacterCrucible.CoreApi.Tests.Builders.TestCharacters;

namespace CharacterCrucible.CoreApi.Tests.Domain.Characters;

/// <summary>Guards on a request: how it is built, and its own transitions (approve, reject, applied, failed, abandoned).</summary>
public class AdvancementRequestTests
{
    public static TheoryData<RequestStatus> NotPendingApproval => StatusesExcept(RequestStatus.PendingApproval);
    // A day after the builder's clock, so a transition that wrongly went through would leave a new date.
    private static readonly DateTimeOffset LaterTime = Now.AddDays(1);
    private static FakeTimeProvider Later() => new(LaterTime);

    private static AdvancementRequest ARequestIn(RequestStatus status)
    {
        var character = ACharacter().InStatus(CharacterStatus.Active).Build();
        return ATraitRequest(character).InStatus(status).Build();
    }

    [Theory]
    [MemberData(nameof(NotPendingApproval))]
    public void Approve_refuses_a_request_not_pending_approval(RequestStatus status)
    {
        var request = ARequestIn(status);
        var reviewedBy = request.ReviewedBy;

        Assert.Throws<InvalidOperationException>(() => request.Approve(Player.Id, Later()));

        Assert.Equal(status, request.RequestStatus);
        Assert.Equal(reviewedBy, request.ReviewedBy);
    }

    [Theory]
    [MemberData(nameof(NotPendingApproval))]
    public void Reject_refuses_a_request_not_pending_approval(RequestStatus status)
    {
        var request = ARequestIn(status);
        var reviewedBy = request.ReviewedBy;

        Assert.Throws<InvalidOperationException>(() => request.Reject(Player.Id, "Too late.", Later()));

        Assert.Equal(status, request.RequestStatus);
        Assert.Equal(reviewedBy, request.ReviewedBy);
    }

    [Theory]
    [MemberData(nameof(NotAppliable), MemberType = typeof(TestCharacters))]
    public void MarkApplied_refuses_a_request_not_queued_or_approved(RequestStatus status)
    {
        var request = ARequestIn(status);
        var resolvedDate = request.ResolvedDate;

        Assert.Throws<InvalidOperationException>(() => request.MarkApplied(Later()));

        Assert.Equal(status, request.RequestStatus);
        Assert.Equal(resolvedDate, request.ResolvedDate);
    }

    [Theory]
    [MemberData(nameof(NotAppliable), MemberType = typeof(TestCharacters))]
    public void MarkFailed_refuses_a_request_not_queued_or_approved(RequestStatus status)
    {
        var request = ARequestIn(status);
        var resolvedDate = request.ResolvedDate;

        Assert.Throws<InvalidOperationException>(() => request.MarkFailed("The cost changed.", Later()));

        Assert.Equal(status, request.RequestStatus);
        Assert.Equal(resolvedDate, request.ResolvedDate);
    }

    [Fact]
    public void MarkAbandoned_refuses_a_request_pending_approval()
    {
        var request = ARequestIn(RequestStatus.PendingApproval);

        Assert.Throws<InvalidOperationException>(() => request.MarkAbandoned("Dead-lettered.", Later()));

        Assert.Equal(RequestStatus.PendingApproval, request.RequestStatus);
        Assert.Null(request.ResolvedDate);
    }

    [Theory]
    [InlineData(RequestStatus.Queued)]
    [InlineData(RequestStatus.Approved)]
    public void MarkAbandoned_abandons_a_request_still_in_flight(RequestStatus status)
    {
        var request = ARequestIn(status);

        request.MarkAbandoned("Dead-lettered.", Later());

        Assert.Equal(RequestStatus.Abandoned, request.RequestStatus);
        Assert.Equal("Dead-lettered.", request.ResolutionReason);
        Assert.Equal(LaterTime, request.ResolvedDate);
    }

    [Theory]
    [MemberData(nameof(Finished), MemberType = typeof(TestCharacters))]
    public void MarkAbandoned_on_a_finished_request_changes_nothing_and_checks_nothing(RequestStatus status)
    {
        var request = ARequestIn(status);
        var reason = request.ResolutionReason;
        var resolvedDate = request.ResolvedDate;

        // A redelivered dead letter is a no-op, so even a blank reason and no clock are not refused.
        request.MarkAbandoned("", null!);

        Assert.Equal(status, request.RequestStatus);
        Assert.Equal(reason, request.ResolutionReason);
        Assert.Equal(resolvedDate, request.ResolvedDate);
    }

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

        Assert.Equal(TargetKind.Ability, request.TargetKind);
        Assert.Equal("riposte", request.TargetKey);
        Assert.Null(request.TargetRating);
        Assert.Equal(4, request.QuotedCost);
        Assert.Equal(versionId, request.EvaluatedRulesetVersionId);
        Assert.Equal(EngineVersion, request.EvaluatedEngineVersion);
        Assert.Equal(RequestStatus.Queued, request.RequestStatus);
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

    [Fact]
    public void A_request_cannot_be_rejected_without_a_reviewer_and_stays_pending()
    {
        var character = ACharacter().InStatus(CharacterStatus.Active).Build();
        var request = ATraitRequest(character).Gated().Build();

        var refused = Assert.Throws<ArgumentOutOfRangeException>(() => request.Reject(Guid.Empty, "Not this session.", Clock()));

        Assert.Equal("reviewer", refused.ParamName);
        Assert.Equal(RequestStatus.PendingApproval, request.RequestStatus);
        Assert.Null(request.ReviewedBy);
    }
}
