using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Enums;

namespace CharacterCrucible.CoreApi.Modules.Rulesets.Domain.ValueObjects;

/// <summary>What a purchase costs: an operation over an operand and a factor.</summary>
public record CostRule(CostOp Op, CostOperand Operand, int Factor);
