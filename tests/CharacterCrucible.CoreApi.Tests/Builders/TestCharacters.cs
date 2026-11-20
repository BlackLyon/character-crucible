using CharacterCrucible.CoreApi.Modules.Characters.Domain.Entities;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.Inputs;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.ValueObjects;
using Microsoft.Extensions.Time.Testing;

namespace CharacterCrucible.CoreApi.Tests.Builders;

/// <summary>
/// Where test characters and requests start: <see cref="ACharacter"/>, <see cref="ATraitRequest"/> and
/// <see cref="AnAbilityRequest"/>, plus the values they share.
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

    /// <summary>A Draft character unless told otherwise; <c>.Build()</c> makes it.</summary>
    public static CharacterBuilder ACharacter() => new();

    /// <summary>A Queued request to raise strength to 3, free, unless told otherwise.</summary>
    public static AdvancementRequestBuilder ATraitRequest(Character character) => new(character, TargetKind.Trait);

    /// <summary>A Queued request for riposte, free, unless told otherwise.</summary>
    public static AdvancementRequestBuilder AnAbilityRequest(Character character) => new(character, TargetKind.Ability);

    public static CharacterDraft Sheet(
        IReadOnlyList<CharacterTrait>? traits = null,
        IReadOnlyList<CharacterAbility>? abilities = null) =>
        new("Ilsa Varn", CharacterSex.Female, "Human", 31, "warrior",
            traits ?? [new CharacterTrait("strength", 2), new CharacterTrait("swordsmanship", 1)],
            abilities ?? []);

    /// <summary>Every status except <paramref name="allowed"/>, for a theory that each one is refused. Character or request statuses.</summary>
    public static TheoryData<TStatus> StatusesExcept<TStatus>(params TStatus[] allowed) where TStatus : struct, Enum =>
        new(Enum.GetValues<TStatus>().Except(allowed));
}
