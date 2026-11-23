using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;
using static CharacterCrucible.CoreApi.Tests.Builders.TestCharacters;

namespace CharacterCrucible.CoreApi.Tests.Domain.Characters;

/// <summary>Guards on accepting a request within its limits, and on applying one.</summary>
public class CharacterAdvancementTests
{
    public static TheoryData<RequestStatus> NotSubmittable =>
        StatusesExcept(RequestStatus.Queued, RequestStatus.PendingApproval);

    [Fact]
    public void A_character_awaiting_review_cannot_submit_a_request()
    {
        var character = ACharacter().InStatus(CharacterStatus.PendingApproval).Build();
        var request = ATraitRequest(character).Build();

        Assert.Throws<InvalidOperationException>(() => character.SubmitRequest(request));

        // Null, not merely unchanged: it has never been set, so any write would show.
        Assert.Null(character.LastRequestSubmittedAt);
    }

    [Fact]
    public void Submitting_a_request_records_when_it_was_submitted()
    {
        var character = ACharacter().InStatus(CharacterStatus.Active).Build();
        var request = ATraitRequest(character).Build();

        character.SubmitRequest(request);

        Assert.Equal(Now, character.LastRequestSubmittedAt);
    }

    [Theory]
    [MemberData(nameof(NotSubmittable))]
    public void A_request_that_is_already_decided_cannot_be_submitted(RequestStatus status)
    {
        var character = ACharacter().InStatus(CharacterStatus.Active).Build();
        var request = ATraitRequest(character).InStatus(status).Build();

        Assert.Throws<InvalidOperationException>(() => character.SubmitRequest(request));

        Assert.Null(character.LastRequestSubmittedAt);
    }

    [Fact]
    public void Applying_refuses_a_request_submitted_to_another_character()
    {
        // Same version, so the mismatch guard can't be what refuses it. Both unsaved, so both ids are
        // Guid.Empty and a check by id would let it through.
        var versionId = Guid.CreateVersion7();
        var character = ACharacter().InStatus(CharacterStatus.Active).OnVersion(versionId).Build();
        var other = ACharacter().InStatus(CharacterStatus.Active).OnVersion(versionId).Build();
        var othersRequest = ATraitRequest(other).Submitted().Build();
        var before = character.Traits.ToList();

        Assert.Throws<InvalidOperationException>(() => character.ApplyAdvancement(Worker, othersRequest, EngineVersion, Clock()));

        Assert.Equal(before, character.Traits);
    }

    [Fact]
    public void Applying_refuses_a_request_priced_under_another_ruleset_version()
    {
        var character = ACharacter().InStatus(CharacterStatus.Active).Build();
        var request = ATraitRequest(character).OnVersion(Guid.CreateVersion7()).Submitted().Build();
        var before = character.Traits.ToList();

        Assert.Throws<InvalidOperationException>(() => character.ApplyAdvancement(Worker, request, EngineVersion, Clock()));

        Assert.Equal(before, character.Traits);
    }

    [Theory]
    [InlineData(CharacterStatus.Banned)]
    [InlineData(CharacterStatus.Dead)]
    [InlineData(CharacterStatus.Retired)]
    public void Applying_refuses_a_request_once_the_character_has_left_play(CharacterStatus removedTo)
    {
        // Submitted while active, removed before the worker got to it: why the worker re-validates.
        var character = ACharacter().InStatus(CharacterStatus.Active).Build();
        var request = ATraitRequest(character).Submitted().Build();
        character.RemoveFromPlay(Narrator, removedTo, "Left the campaign.", Clock());
        var before = character.Traits.ToList();

        Assert.Throws<InvalidOperationException>(() => character.ApplyAdvancement(Worker, request, EngineVersion, Clock()));

        Assert.Equal(before, character.Traits);
    }

    [Theory]
    [InlineData(5, 3)]
    [InlineData(3, 3)]
    [InlineData(0, 0)]
    public void SubmitRequest_accepts_a_request_the_character_can_afford(int unspentXp, int requestCost)
    {
        var character = ACharacter().InStatus(CharacterStatus.Active).WithXp(unspentXp).Build();
        var request = ATraitRequest(character).WithCost(requestCost).Build();

        character.SubmitRequest(request);

        Assert.Equal(Now, character.LastRequestSubmittedAt);
    }

    [Theory]
    [InlineData(2, 3)]
    public void SubmitRequest_refuses_a_request_the_character_cannot_afford(int unspentXp, int requestCost)
    {
        var character = ACharacter().InStatus(CharacterStatus.Active).WithXp(unspentXp).Build();
        var request = ATraitRequest(character).WithCost(requestCost).Build();

        Assert.Throws<InvalidOperationException>(() => character.SubmitRequest(request));

        Assert.Null(character.LastRequestSubmittedAt);
    }
}
