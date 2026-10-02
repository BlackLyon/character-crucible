using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Enums;
using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.ValueObjects;

namespace CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Entities;

public class TraitDefinition(Guid rulesetVersionId, string key, string name, string description, TraitCategory category, string? domainKey, int minValue, int maxValue, CostRule costRule, int sortOrder, bool isAvailable)
{
    public Guid Id { get; private set; }
    public Guid RulesetVersionId { get; private set; } = rulesetVersionId;
    public string Key { get; private set; } = key;
    public string Name { get; private set; } = name;
    public string Description { get; private set; } = description;
    public  TraitCategory Category { get; private set; } = category;
    public string? DomainKey { get; private set; } = domainKey;
    public int MinValue { get; private set; } = minValue;
    public int MaxValue { get; private set; } = maxValue;
    public CostRule CostRule { get; private set; } = costRule;
    public int SortOrder { get; private set; } = sortOrder;
    public bool IsAvailable { get; private set; } = isAvailable;
}
