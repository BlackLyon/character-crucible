namespace CharacterCrucible.CoreApi.Modules.Rulesets.Domain.ValueObjects;

/// <summary>Ranks an archetype grants in a trait. These sit outside the trait's cap.</summary>
public record GrantedRank(string TraitKey, int Ranks);
