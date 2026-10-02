using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.ValueObjects;

namespace CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Entities;

public class DomainDefinition
{
    public Guid Id { get; set; }
    public Guid RulesetVersionId { get; set; }
    public required string Key { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public int SortOrder { get; set; }
    private IList<DomainBand> BandValues { get; set; } = new List<DomainBand>();
    public IReadOnlyList<DomainBand> Bands => BandValues.AsReadOnly();

    public void AddBand(DomainBand band)
    {
        BandValues.Add(band);
    }

    public void RemoveBand(DomainBand band) 
    { 
        BandValues.Remove(band); 
    }
}
