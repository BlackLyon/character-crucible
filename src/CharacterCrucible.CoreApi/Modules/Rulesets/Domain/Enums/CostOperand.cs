namespace CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Enums;

/// <summary>Which rating a cost rule applies its factor to.</summary>
// Numbered: persisted, so these values must survive a member being reordered.
public enum CostOperand
{
    CurrentRating = 1,
    TargetRating = 2,
}
