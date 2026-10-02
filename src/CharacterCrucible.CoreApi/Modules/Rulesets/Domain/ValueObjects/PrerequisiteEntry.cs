using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Enums;

namespace CharacterCrucible.CoreApi.Modules.Rulesets.Domain.ValueObjects;

public record PrerequisiteEntry(PrerequisiteEntryKind Kind, string TargetKey, int? MinimumRating)
{
    public static PrerequisiteEntry TraitAtMinimum(string traitKey, int minimum) =>
        new(PrerequisiteEntryKind.TraitAtMinimum, traitKey, minimum);

    public static PrerequisiteEntry AbilityHeld(string abilityKey) =>
        new(PrerequisiteEntryKind.AbilityHeld, abilityKey, null);

    public static PrerequisiteEntry ArchetypeIs(string archetypeKey) =>
        new(PrerequisiteEntryKind.ArchetypeIs, archetypeKey, null);
}