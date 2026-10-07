namespace CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Enums;

/// <summary>Which namespace an archetype modifier's target key lives in.</summary>
// Numbered: persisted, so these values must survive a member being reordered.
public enum ArchetypeTargetKind
{
    Trait = 1,
    Ability = 2,
}
