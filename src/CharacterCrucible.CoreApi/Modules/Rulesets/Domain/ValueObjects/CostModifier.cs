using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Enums;

namespace CharacterCrucible.CoreApi.Modules.Rulesets.Domain.ValueObjects;

/// <summary>An archetype's override of what a trait or ability costs.</summary>
public record CostModifier(string TargetKey, ArchetypeTargetKind TargetKind, int Factor);
