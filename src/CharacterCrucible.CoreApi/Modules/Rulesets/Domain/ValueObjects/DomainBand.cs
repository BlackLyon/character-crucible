namespace CharacterCrucible.CoreApi.Modules.Rulesets.Domain.ValueObjects;

/// <summary>A named band a domain score falls into, identified by its lower bound.</summary>
public record DomainBand(string Label, int MinimumScore);
