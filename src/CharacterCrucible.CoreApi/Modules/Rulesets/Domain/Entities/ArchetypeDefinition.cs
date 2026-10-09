using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Constants;
using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.ValueObjects;

namespace CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Entities;

/// <summary>An archetype. Grants ranks, and overrides costs and caps.</summary>
public class ArchetypeDefinition
{
    public ArchetypeDefinition(Guid rulesetVersionId, string key, string name, string description, IList<GrantedRank> grantedRanks, IList<CostModifier> costModifiers, IList<CapModifier> capModifiers, int sortOrder, bool isAvailable)
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
        Ranks = [.. grantedRanks];
        Modifiers = [.. costModifiers];
        Caps = [.. capModifiers];
        SortOrder = sortOrder;
        IsAvailable = isAvailable;
    }

    /// <summary>For EF materialisation only. Rows in the database have already been validated.</summary>
    private ArchetypeDefinition() { }

    public Guid Id { get; private set; }
    public Guid RulesetVersionId { get; private set; }
    public string Key { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    private IList<GrantedRank> Ranks { get; set; } = [];
    public IReadOnlyList<GrantedRank> GrantedRanks => Ranks.AsReadOnly();
    private IList<CostModifier> Modifiers { get; set; } = [];
    public IReadOnlyList<CostModifier> CostModifiers => Modifiers.AsReadOnly();
    private IList<CapModifier> Caps { get; set; } = [];
    public IReadOnlyList<CapModifier> CapModifiers => Caps.AsReadOnly();
    public int SortOrder { get; private set; }
    public bool IsAvailable { get; private set; }
}
