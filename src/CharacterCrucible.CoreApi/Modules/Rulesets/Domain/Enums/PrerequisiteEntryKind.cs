namespace CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Enums;

/// <summary>Which kind of condition a prerequisite entry expresses.</summary>
// Numbered: persisted, so these values must survive a member being reordered.
public enum PrerequisiteEntryKind 
{ 
    TraitAtMinimum = 1, 
    AbilityHeld = 2, 
    ArchetypeIs = 3 
}
