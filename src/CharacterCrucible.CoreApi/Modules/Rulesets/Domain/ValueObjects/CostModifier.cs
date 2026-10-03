using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Enums;

namespace CharacterCrucible.CoreApi.Modules.Rulesets.Domain.ValueObjects;

public record CostModifier(string TargetKey, ArchetypeTargetKind TargetKind, int Factor);
