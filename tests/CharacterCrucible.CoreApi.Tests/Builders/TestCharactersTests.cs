using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.ValueObjects;
using static CharacterCrucible.CoreApi.Tests.Builders.TestCharacters;

namespace CharacterCrucible.CoreApi.Tests.Builders;

/// <summary>The builders themselves, so a theory can't run against a character or request it didn't ask for.</summary>
public class TestCharactersTests
{
    public static TheoryData<CharacterStatus> EveryStatus => StatusesExcept<CharacterStatus>();
    public static TheoryData<RequestStatus> EveryRequestStatus => new(Enum.GetValues<RequestStatus>());

    [Theory]
    [MemberData(nameof(EveryStatus))]
    public void Every_character_status_has_a_builder_path(CharacterStatus status)
    {
        Assert.Equal(status, ACharacter().InStatus(status).Build().Status);
    }

    [Theory]
    [MemberData(nameof(EveryRequestStatus))]
    public void Every_request_status_has_a_builder_path(RequestStatus status)
    {
        var character = ACharacter().InStatus(CharacterStatus.Active).Build();

        Assert.Equal(status, ATraitRequest(character).InStatus(status).Submitted().Build().RequestStatus);
    }

    [Fact]
    public void A_gated_applied_request_passes_through_approval()
    {
        var character = ACharacter().InStatus(CharacterStatus.Active).Build();

        var request = ATraitRequest(character).Gated().InStatus(RequestStatus.Applied).Build();

        Assert.Equal(RequestStatus.Applied, request.RequestStatus);
        Assert.Equal(Narrator.Id, request.ReviewedBy);
    }

    [Fact]
    public void A_later_submitted_date_reaches_the_request_and_the_character()
    {
        var character = ACharacter().InStatus(CharacterStatus.Active).Build();
        var later = Now.AddHours(1);

        var request = ATraitRequest(character).WithSubmittedDate(later).Gated().InStatus(RequestStatus.Approved).Submitted().Build();

        Assert.Equal(later, request.SubmittedDate);
        Assert.Equal(later, request.ReviewedDate);
        Assert.Equal(later, character.LastRequestSubmittedAt);
    }

    [Fact]
    public void Abilities_named_by_key_are_undated_and_kept_in_order()
    {
        var draft = ADraft().WithAbilities("riposte", "parry").Build();

        Assert.Equal([new CharacterAbility("riposte", null), new CharacterAbility("parry", null)], draft.Abilities);
    }

    [Fact]
    public void A_shared_builder_is_not_changed_by_a_test_that_extends_it()
    {
        var active = ACharacter().InStatus(CharacterStatus.Active);

        _ = active.WithXp(10).Build();

        Assert.Equal(0, active.Build().UnspentXp);
    }

    [Fact]
    public void Options_that_do_not_fit_are_refused_at_build()
    {
        var character = ACharacter().InStatus(CharacterStatus.Active).Build();

        Assert.Throws<InvalidOperationException>(() => ACharacter().WithXp(10).Build());
        Assert.Throws<InvalidOperationException>(() => ACharacter().InStatus(CharacterStatus.Active).WithXp(-3).Build());
        Assert.Throws<InvalidOperationException>(() => AnAbilityRequest(character).WithRating(2).Build());
        Assert.Throws<InvalidOperationException>(() =>
            ATraitRequest(character).Gated().InStatus(RequestStatus.Queued).Build());
    }

    [Fact]
    public void Excluding_a_status_leaves_every_other_one()
    {
        var statuses = StatusesExcept(CharacterStatus.PendingApproval).Select(row => row.Data).ToList();

        Assert.DoesNotContain(CharacterStatus.PendingApproval, statuses);
        Assert.Equal(Enum.GetValues<CharacterStatus>().Length - 1, statuses.Count);
    }
}
