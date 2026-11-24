using CharacterCrucible.CoreApi.Modules.Characters.Domain.Entities;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.ValueObjects;
using static CharacterCrucible.CoreApi.Tests.Builders.TestCharacters;

namespace CharacterCrucible.CoreApi.Tests.Domain.Characters;

/// <summary>Guards on an audit row and the change it records.</summary>
public class AuditEntryTests
{
    // Every case gives a reason and a request id, so the argument under test is the only one missing.
    private const string Reason = "Session award";
    private static readonly AuditChange Change = new("Strength raised.", [new ChangeEntry("traits.strength", "Strength", "2", "3")]);

    [Theory]
    [InlineData(AuditAction.AdvancementApplied)]
    [InlineData(AuditAction.StoryGrant)]
    [InlineData(AuditAction.ManualEdit)]
    [InlineData(AuditAction.Refunded)]
    public void An_action_that_ran_Rules_is_refused_without_an_engine_version(AuditAction action)
    {
        var requestId = Guid.CreateVersion7();
        var versionId = Guid.CreateVersion7();

        var refused = Assert.ThrowsAny<ArgumentException>(() =>
            new AuditEntry(Worker, action, Change, Reason, requestId, versionId, engineVersion: null, Clock()));

        Assert.Equal("engineVersion", refused.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_action_that_ran_Rules_is_refused_with_a_blank_engine_version(string engineVersion)
    {
        var requestId = Guid.CreateVersion7();
        var versionId = Guid.CreateVersion7();

        var refused = Assert.Throws<ArgumentException>(() =>
            new AuditEntry(Worker, AuditAction.AdvancementApplied, Change, Reason, requestId, versionId, engineVersion, Clock()));

        Assert.Equal("engineVersion", refused.ParamName);
    }

    [Theory]
    [InlineData(AuditAction.CharacterApproved)]
    [InlineData(AuditAction.StatusChanged)]
    [InlineData(AuditAction.XpGranted)]
    public void An_action_that_did_not_run_Rules_is_refused_with_an_engine_version(AuditAction action)
    {
        var requestId = Guid.CreateVersion7();
        var versionId = Guid.CreateVersion7();

        var refused = Assert.Throws<ArgumentException>(() =>
            new AuditEntry(Narrator, action, Change, Reason, requestId, versionId, EngineVersion, Clock()));

        Assert.Equal("engineVersion", refused.ParamName);
    }

    [Fact]
    public void An_applied_advancement_is_refused_without_its_request_id()
    {
        var versionId = Guid.CreateVersion7();

        var refused = Assert.Throws<ArgumentException>(() =>
            new AuditEntry(Worker, AuditAction.AdvancementApplied, Change, Reason, advancementRequestId: null, versionId, EngineVersion, Clock()));

        Assert.Equal("advancementRequestId", refused.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_change_without_a_summary_is_refused(string summary)
    {
        ChangeEntry[] changes = [new ChangeEntry("traits.strength", "Strength", "2", "3")];

        var refused = Assert.ThrowsAny<ArgumentException>(() => new AuditChange(summary, changes));

        Assert.Equal("summary", refused.ParamName);
    }

    [Fact]
    public void A_change_with_no_entries_is_refused()
    {
        ChangeEntry[] none = [];

        var refused = Assert.Throws<ArgumentException>(() => new AuditChange("Strength raised.", none));

        Assert.Equal("changes", refused.ParamName);
    }
}
