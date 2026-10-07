namespace CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Enums;

/// <summary>How a cost rule combines its operand and factor.</summary>
// Numbered: these values are serialised into PublishedContent, so they must survive reordering.
public enum CostOp
{
    Add = 1,
    Flat = 2,
    Multiply = 3,
}
