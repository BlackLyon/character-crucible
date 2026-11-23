using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.Inputs;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.ValueObjects;

namespace CharacterCrucible.CoreApi.Tests.Builders;

/// <summary>
/// Builds the sheet a character is created or updated with: strength 2, swordsmanship 1 and no
/// abilities unless told otherwise. Immutable, like <see cref="CharacterBuilder"/>.
/// </summary>
internal sealed record DraftBuilder
{
    private IReadOnlyList<CharacterTrait> Traits { get; init; } =
        [new CharacterTrait("strength", 2), new CharacterTrait("swordsmanship", 1)];
    private IReadOnlyList<CharacterAbility> Abilities { get; init; } = [];

    /// <summary>Replaces the default traits.</summary>
    public DraftBuilder WithTraits(params CharacterTrait[] traits) => this with { Traits = [.. traits] };

    /// <summary>Undated, as every ability in a draft must be.</summary>
    public DraftBuilder WithAbilities(params string[] keys) =>
        this with { Abilities = [.. keys.Select(key => new CharacterAbility(key, null))] };

    /// <summary>A dated ability, which a draft refuses. Only for proving that it does.</summary>
    public DraftBuilder WithDatedAbility(string key, DateTimeOffset acquiredDate) =>
        this with { Abilities = [new CharacterAbility(key, acquiredDate)] };

    public CharacterDraft Build() =>
        new("Ilsa Varn", CharacterSex.Female, "Human", 31, "warrior", Traits, Abilities);
}
