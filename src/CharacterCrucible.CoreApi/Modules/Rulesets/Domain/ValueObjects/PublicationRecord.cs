namespace CharacterCrucible.CoreApi.Modules.Rulesets.Domain.ValueObjects;

/// <summary>The publish stamp. Present exactly when a version is published.</summary>
public record PublicationRecord(string RulesetName, string Publisher, DateTimeOffset PublishedDate, Guid PublishedBy, string PublishedContent, string ContentHash, int SchemaVersion)
{
    /// <summary>The shape the current serialiser produces. Bump when PublishedContent's layout changes.</summary>
    public const int CurrentSchemaVersion = 1;

}
