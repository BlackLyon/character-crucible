using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.ValueObjects;
using static CharacterCrucible.CoreApi.Tests.Builders.TestCharacters;

namespace CharacterCrucible.CoreApi.Tests.Domain.Characters;

/// <summary>Guards on review and play status: approve, send back, ban, remove from and return to play.</summary>
public class CharacterLifecycleTests
{
    public static TheoryData<CharacterStatus> NotPendingApproval => StatusesExcept(CharacterStatus.PendingApproval);

    [Fact]
    public void An_active_character_cannot_be_sent_back_and_stays_active()
    {
        var character = ACharacter().InStatus(CharacterStatus.Active).Build();

        Assert.Throws<InvalidOperationException>(() => character.SendBack("Needs a backstory."));

        Assert.Equal(CharacterStatus.Active, character.Status);
    }

    [Fact]
    public void Only_a_character_in_pending_approval_can_be_approved()
    {
        var character = ACharacter().InStatus(CharacterStatus.PendingApproval).Build();

        character.Approve(Narrator, Clock());

        Assert.Equal(CharacterStatus.Active, character.Status);
    }

    [Theory]
    [MemberData(nameof(NotPendingApproval))]
    public void A_character_not_in_pending_approval_cannot_be_approved(CharacterStatus status)
    {
        var character = ACharacter().InStatus(status).Build();

        Assert.Throws<InvalidOperationException>(() => character.Approve(Narrator, Clock()));

        Assert.Equal(status, character.Status);
    }

    [Fact]
    public void Approval_dates_every_ability_with_the_approval_time()
    {
        var draft = Sheet(abilities: [new CharacterAbility("riposte", null), new CharacterAbility("parry", null)]);

        var character = ACharacter().InStatus(CharacterStatus.PendingApproval).WithSheet(draft).Build();

        character.Approve(Narrator, Clock());

        Assert.All(character.Abilities, ability => Assert.Equal(Now, ability.AcquiredDate));
    }
}
