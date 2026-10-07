namespace CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Enums;

/// <summary>Whether a trait feeds a domain aggregate.</summary>
// Numbered: persisted, so these values must survive a member being reordered.
public enum TraitCategory
{
    Attribute = 1,
    Skill = 2,
}
