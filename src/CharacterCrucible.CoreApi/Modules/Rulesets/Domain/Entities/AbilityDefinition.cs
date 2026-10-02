using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Enums;
using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.ValueObjects;

namespace CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Entities;

public class AbilityDefinition(Guid rulesetVersionId, string key, string name, string description, AbilityDefinitionKind kind, CostRule costRule, bool requiresApproval, IList<PrerequisiteGroup> prerequisiteGroups, int sortOrder, bool isAvailable)
{
    public Guid Id { get; private set; }
    public Guid RulesetVersionId { get; private set; } = rulesetVersionId;
    public string Key { get; private set; } = key;
    public string Name { get; private set; } = name;
    public string Description { get; private set; } = description;
    public AbilityDefinitionKind Kind { get; private set; } = kind;
    public CostRule CostRule { get; private set; } = costRule;
    public bool RequiresApproval { get; private set; } = requiresApproval;
    private IList<PrerequisiteGroup> PrerequisiteGroups { get; set; } = [.. prerequisiteGroups];
    public IReadOnlyList<PrerequisiteGroup> Prerequisites => PrerequisiteGroups.AsReadOnly();
    public int SortOrder { get; private set; } = sortOrder;
    public bool IsAvailable { get; private set; } = isAvailable;
}
