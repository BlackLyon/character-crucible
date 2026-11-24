using CharacterCrucible.CoreApi.Modules.Characters.Domain.Entities;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.Inputs;
using static CharacterCrucible.CoreApi.Tests.Builders.TestCharacters;

namespace CharacterCrucible.CoreApi.Tests.Builders;

/// <summary>
/// Builds a character through the domain's own transitions. Immutable, so a partly configured
/// builder can be shared between tests without one changing it under another.
/// </summary>
internal sealed record CharacterBuilder
{
    private CharacterStatus Status { get; init; } = CharacterStatus.Draft;
    private Guid? RulesetVersionId { get; init; }
    private CharacterDraft? Sheet { get; init; }
    private int Xp { get; init; }

    public CharacterBuilder InStatus(CharacterStatus status) => this with { Status = status };

    /// <summary>A real version's id when the character will be saved; the foreign key needs one.</summary>
    public CharacterBuilder OnVersion(Guid rulesetVersionId) => this with { RulesetVersionId = rulesetVersionId };

    public CharacterBuilder WithSheet(CharacterDraft sheet) => this with { Sheet = sheet };

    /// <summary>Granted by the narrator while the character is active, so only a status reached from play allows it.</summary>
    public CharacterBuilder WithXp(int xp) => this with { Xp = xp };

    public Character Build()
    {
        var reachedFromPlay = Status is CharacterStatus.Active or CharacterStatus.Banned
            or CharacterStatus.Dead or CharacterStatus.Retired;
        if (Xp < 0)
        {
            throw new InvalidOperationException($"WithXp grants XP, so it can't be negative: {Xp}.");
        }
        if (Xp > 0 && !reachedFromPlay)
        {
            throw new InvalidOperationException($"WithXp needs a status reached from play, not {Status}.");
        }

        var character = new Character(Player.Id, RulesetVersionId ?? Guid.CreateVersion7(),
            Sheet ?? ADraft().Build(), Clock());

        switch (Status)
        {
            case CharacterStatus.Draft:
                break;
            case CharacterStatus.PendingApproval:
                character.SubmitForApproval();
                break;
            case CharacterStatus.DraftBanned:
                character.SubmitForApproval();
                character.BanDraft("Doesn't fit the setting");
                break;
            case CharacterStatus.Active or CharacterStatus.Banned or CharacterStatus.Dead or CharacterStatus.Retired:
                character.SubmitForApproval();
                character.Approve(Narrator, Clock());
                if (Xp > 0)
                {
                    character.GrantXp(Narrator, Xp, "Session award", Clock());
                }
                if (Status != CharacterStatus.Active)
                {
                    character.RemoveFromPlay(Narrator, Status, "Left the campaign", Clock());
                }
                break;
            default:
                throw new InvalidOperationException($"No builder path to {Status} yet.");
        }

        return character;
    }
}
