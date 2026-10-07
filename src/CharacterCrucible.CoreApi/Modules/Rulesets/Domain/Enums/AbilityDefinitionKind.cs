namespace CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Enums;

/// <summary>Magnitude: a small situational perk, or a major dramatic ability.</summary>
// Numbered: persisted, so these values must survive a member being reordered.
public enum AbilityDefinitionKind
{
    Ability = 1,
    Perk = 2,
}
