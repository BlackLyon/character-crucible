using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.ValueObjects;

namespace CharacterCrucible.CoreApi.Modules.Characters.Domain.Inputs;

public record CharacterDraft(string Name, CharacterSex Sex, string Race, int Age, string ArchetypeKey,
    IReadOnlyList<CharacterTrait> Traits, IReadOnlyList<CharacterAbility> Abilities);
