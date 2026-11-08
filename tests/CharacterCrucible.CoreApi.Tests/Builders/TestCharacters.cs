using CharacterCrucible.CoreApi.Modules.Characters.Domain.Entities;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.Inputs;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.ValueObjects;
using Microsoft.Extensions.Time.Testing;

namespace CharacterCrucible.CoreApi.Tests.Builders;

/// <summary>
/// Characters in each starting state, built only through the domain's own methods and factories, so a
/// builder can't produce a state the domain can't reach. Change the arguments, not the path.
/// </summary>
internal static class TestCharacters
{
    /// <summary>Non-zero minutes and seconds, so a truncated timestamp can't pass by accident.</summary>
    public static readonly DateTimeOffset Now = new(2026, 3, 1, 14, 37, 9, TimeSpan.Zero);

    public static readonly Actor Player = new(Guid.CreateVersion7(), ActorRole.Player);
    public static readonly Actor Narrator = new(Guid.CreateVersion7(), ActorRole.Narrator);
    public static readonly Actor Worker = new(Guid.CreateVersion7(), ActorRole.Worker);

    public const string EngineVersion = "rules-test-1";

    /// <summary>A fresh clock per call, so a test that advances time can't move another's.</summary>
    public static FakeTimeProvider Clock() => new(Now);

    public static CharacterDraft Sheet(
        IReadOnlyList<CharacterTrait>? traits = null,
        IReadOnlyList<CharacterAbility>? abilities = null) =>
        new("Ilsa Varn", CharacterSex.Female, "Human", 31, "warrior",
            traits ?? [new CharacterTrait("strength", 2), new CharacterTrait("swordsmanship", 1)],
            abilities ?? []);

    /// <param name="rulesetVersionId">A real version's id when the character will be saved; the FK needs one.</param>
    public static Character Draft(Guid? rulesetVersionId = null, CharacterDraft? sheet = null) =>
        new(Player.Id, rulesetVersionId ?? Guid.CreateVersion7(), sheet ?? Sheet(), Clock());

    public static Character PendingApproval(Guid? rulesetVersionId = null, CharacterDraft? sheet = null)
    {
        var character = Draft(rulesetVersionId, sheet);
        character.SubmitForApproval();
        return character;
    }

    /// <summary>Approved by <see cref="Narrator"/>, then granted <paramref name="xp"/> if any.</summary>
    public static Character Active(int xp = 0, Guid? rulesetVersionId = null, CharacterDraft? sheet = null)
    {
        var character = PendingApproval(rulesetVersionId, sheet);
        character.Approve(Narrator, Clock());
        if (xp > 0)
        {
            character.GrantXp(Narrator, xp, "Session award", Clock());
        }
        return character;
    }

    /// <summary>A trait request priced under the character's own version, so the apply-time check passes.</summary>
    public static AdvancementRequest TraitRequest(Character character, string key = "strength", int rating = 3,
        int cost = 0, bool requiresApproval = false) =>
        AdvancementRequest.ForTrait(Player.Id, Clock(), key, rating, cost, character.RulesetVersionId,
            EngineVersion, requiresApproval);

    public static AdvancementRequest AbilityRequest(Character character, string key = "riposte",
        int cost = 0, bool requiresApproval = false) =>
        AdvancementRequest.ForAbility(Player.Id, Clock(), key, cost, character.RulesetVersionId,
            EngineVersion, requiresApproval);
}
