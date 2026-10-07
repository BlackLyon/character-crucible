namespace CharacterCrucible.CoreApi.Modules.Rulesets.Domain.ValueObjects;

/// <summary>An archetype's override of a trait's maximum. Zero means unavailable.</summary>
public record CapModifier(string TargetKey, int MaxOverride);
