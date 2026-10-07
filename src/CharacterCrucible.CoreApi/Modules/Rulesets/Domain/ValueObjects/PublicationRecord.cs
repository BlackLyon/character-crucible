namespace CharacterCrucible.CoreApi.Modules.Rulesets.Domain.ValueObjects;

/// <summary>The publish stamp. Present exactly when a version is published.</summary>
public record PublicationRecord(string RulesetName, string Publisher, DateTimeOffset PublishedDate, Guid PublishedBy, string PublishedContent, string ContentHash);
