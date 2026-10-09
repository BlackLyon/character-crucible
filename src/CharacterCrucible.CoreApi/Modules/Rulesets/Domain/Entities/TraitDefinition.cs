using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Constants;
using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Enums;
using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.ValueObjects;

namespace CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Entities;

/// <summary>A rated thing: an attribute or a skill. A key with a numeric rating.</summary>
public class TraitDefinition
{
    public TraitDefinition(Guid rulesetVersionId, string key, string name, string description, TraitCategory category, string? domainKey, int minValue, int maxValue, CostRule costRule, int sortOrder, bool isAvailable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(key.Length, FieldLengths.Key, nameof(key));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.Length, FieldLengths.Name, nameof(name));

        // Null is legal — skills carry no domain — but blank is not: it would fail every
        // domain lookup while looking like a value.
        if (domainKey is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(domainKey);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(domainKey.Length, FieldLengths.Key, nameof(domainKey));
        }

        RulesetVersionId = rulesetVersionId;
        Key = key;
        Name = name;
        Description = description;
        Category = category;
        DomainKey = domainKey;
        MinValue = minValue;
        MaxValue = maxValue;
        CostRule = costRule;
        SortOrder = sortOrder;
        IsAvailable = isAvailable;
    }

    /// <summary>For EF materialisation only. Rows in the database have already been validated.</summary>
    private TraitDefinition() { }

    public Guid Id { get; private set; }
    public Guid RulesetVersionId { get; private set; }
    public string Key { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public TraitCategory Category { get; private set; }
    public string? DomainKey { get; private set; }
    public int MinValue { get; private set; }
    public int MaxValue { get; private set; }
    public CostRule CostRule { get; private set; } = null!;
    public int SortOrder { get; private set; }
    public bool IsAvailable { get; private set; }
}
