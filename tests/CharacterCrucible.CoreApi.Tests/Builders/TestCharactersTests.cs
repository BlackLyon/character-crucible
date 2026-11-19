using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;
using static CharacterCrucible.CoreApi.Tests.Builders.TestCharacters;

namespace CharacterCrucible.CoreApi.Tests.Builders;

/// <summary>The builders themselves, so a status theory can't run against the wrong character.</summary>
public class TestCharactersTests
{
    public static TheoryData<CharacterStatus> EveryStatus => StatusesExcept();

    [Theory]
    [MemberData(nameof(EveryStatus))]
    public void Every_status_has_a_builder_that_reaches_it(CharacterStatus status)
    {
        Assert.Equal(status, InStatus(status).Status);
    }

    [Fact]
    public void Excluding_a_status_leaves_every_other_one()
    {
        var statuses = StatusesExcept(CharacterStatus.PendingApproval).Select(row => row.Data).ToList();

        Assert.DoesNotContain(CharacterStatus.PendingApproval, statuses);
        Assert.Equal(Enum.GetValues<CharacterStatus>().Length - 1, statuses.Count);
    }
}
