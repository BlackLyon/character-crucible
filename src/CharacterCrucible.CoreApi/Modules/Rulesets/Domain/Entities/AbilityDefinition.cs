using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Enums;
using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.StaticObjects;
using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.ValueObjects;

namespace CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Entities;

/// <summary>A binary thing: a perk or an ability. Held or not, gated by prerequisites.</summary>
public class AbilityDefinition
{
    public AbilityDefinition(Guid rulesetVersionId, string key, string name, string description, AbilityDefinitionKind abilityType, CostRule costRule, bool requiresApproval, IList<PrerequisiteGroup> prerequisiteGroups, int sortOrder, bool isAvailable)
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
        AbilityType = abilityType;
        CostRule = costRule;
        RequiresApproval = requiresApproval;
        PrerequisiteGroups = [.. prerequisiteGroups];
        SortOrder = sortOrder;
        IsAvailable = isAvailable;
    }

    /// <summary>For EF materialisation only. Rows in the database have already been validated.</summary>
    private AbilityDefinition() { }

    public Guid Id { get; private set; }
    public Guid RulesetVersionId { get; private set; }
    public string Key { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public AbilityDefinitionKind AbilityType { get; private set; }
    public CostRule CostRule { get; private set; } = null!;
    public bool RequiresApproval { get; private set; }
    private IList<PrerequisiteGroup> PrerequisiteGroups { get; set; } = [];
    public IReadOnlyList<PrerequisiteGroup> Prerequisites => PrerequisiteGroups.AsReadOnly();
    public int SortOrder { get; private set; }
    public bool IsAvailable { get; private set; }
}
