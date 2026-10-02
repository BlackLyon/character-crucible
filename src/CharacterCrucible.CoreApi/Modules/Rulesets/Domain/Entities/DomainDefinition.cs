using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.ValueObjects;

namespace CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Entities;

public class DomainDefinition(Guid rulesetVersionId, string key, string name, string description, IList<DomainBand> bandValues, int sortOrder)
{
    public Guid Id { get; private set; }
    public Guid RulesetVersionId { get; private set; } = rulesetVersionId;
    public string Key { get; private set; } = key;
    public string Name { get; private set; } = name;
    public string Description { get; private set; } = description;
    public int SortOrder { get; private set; } = sortOrder;
    private IList<DomainBand> BandValues { get; set; } = [.. bandValues];
    public IReadOnlyList<DomainBand> Bands => BandValues.AsReadOnly();
}
