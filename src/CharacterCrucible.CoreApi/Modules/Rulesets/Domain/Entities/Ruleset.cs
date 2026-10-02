using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Enums;

namespace CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Entities;

public class Ruleset
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string Publisher { get; set; }
    public RulesetKind Kind => DerivedFrom is null ? RulesetKind.Original : RulesetKind.Homebrew;
    public Guid? DerivedFrom { get; set; }
    public Guid? CurrentVersionId { get; set; }
    private readonly List<RulesetVersion> _versions = [];
    public IReadOnlyCollection<RulesetVersion> Versions => _versions.AsReadOnly();
}
