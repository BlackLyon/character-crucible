using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.ValueObjects;

namespace CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Entities;

public class ArchetypeDefinition(Guid rulesetVersionId, string key, string name, string description, IList<GrantedRank> grantedRanks, IList<CostModifier> costModifiers, IList<CapModifier> capModifiers, int sortOrder, bool isAvailable)
{
    public Guid Id { get; private set; }
    public Guid RulesetVersionId { get; private set; } = rulesetVersionId;
    public string Key { get; private set; } = key;
    public string Name { get; private set; } = name;
    public string Description { get; private set; } = description;
    private IList<GrantedRank> Ranks { get; set; } = [.. grantedRanks];
    public IReadOnlyList<GrantedRank> GrantedRanks => Ranks.AsReadOnly();
    private IList<CostModifier> Modifiers { get; set; } = [.. costModifiers];
    public IReadOnlyList<CostModifier> CostModifiers => Modifiers.AsReadOnly();
    private IList<CapModifier> Caps { get; set; } = [.. capModifiers];
    public IReadOnlyList<CapModifier> CapModifiers => Caps.AsReadOnly();
    public int SortOrder { get; private set; } = sortOrder;
    public bool IsAvailable { get; private set; } = isAvailable;
}
