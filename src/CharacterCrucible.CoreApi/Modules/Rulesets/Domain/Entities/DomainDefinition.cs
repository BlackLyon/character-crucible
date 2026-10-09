using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.ValueObjects;

namespace CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Entities;

/// <summary>A domain — Body, Mind, Spirit — and the bands its aggregate score falls into.</summary>
public class DomainDefinition
{
    public DomainDefinition(Guid rulesetVersionId, string key, string name, string description, IList<DomainBand> bandValues, int sortOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(key.Length, FieldLengths.Key, nameof(key));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.Length, FieldLengths.Name, nameof(name));

        RulesetVersionId = rulesetVersionId;
        Key = key;
        Name = name;
        Description = description;
        BandValues = [.. bandValues];
        SortOrder = sortOrder;
    }

    /// <summary>For EF materialisation only. Rows in the database have already been validated.</summary>
    private DomainDefinition() { }

    public Guid Id { get; private set; }
    public Guid RulesetVersionId { get; private set; }
    public string Key { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public int SortOrder { get; private set; }
    private IList<DomainBand> BandValues { get; set; } = [];
    public IReadOnlyList<DomainBand> Bands => BandValues.AsReadOnly();
}
