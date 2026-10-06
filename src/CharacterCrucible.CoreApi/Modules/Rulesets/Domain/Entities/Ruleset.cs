using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Enums;

namespace CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Entities;

public class Ruleset
{
    public Ruleset(string name, string description, string publisher, Guid? derivedFrom = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(publisher);

        Name = name;
        Description = description;
        Publisher = publisher;
        DerivedFrom = derivedFrom;
    }

    /// <summary>For EF materialisation only. Rows in the database have already been validated.</summary>
    private Ruleset() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public string Publisher { get; private set; } = null!;
    public RulesetKind Origin => DerivedFrom is null ? RulesetKind.Base : RulesetKind.Derived;
    public Guid? DerivedFrom { get; private set; }
    public RulesetVersion? CurrentVersion { get; private set; }
    private readonly IList<RulesetVersion> _versions = [];
    public IReadOnlyCollection<RulesetVersion> Versions => _versions.AsReadOnly();

    public void SetCurrentVersion(RulesetVersion version)
    {
        if (!_versions.Contains(version))
        {
            throw new InvalidOperationException("The specified version does not exist.");
        }
        if (version.Status != RulesetVersionStatus.Published)
        {
            throw new InvalidOperationException("Only published versions can be set as the current version.");
        }
        CurrentVersion = version;
    }

    public RulesetVersion CreateDraft(int majorVersionNumber, int minorVersionNumber, RulesetVersionKind kind)
    {
        if (_versions.Any(v => v.MajorVersion == majorVersionNumber && v.MinorVersion == minorVersionNumber))
        {
            throw new InvalidOperationException("A version with the specified major and minor version numbers already exists.");
        }

        if (_versions.Any(v => v.Status == RulesetVersionStatus.Draft))
            throw new InvalidOperationException("This ruleset already has an open draft.");

        var draftVersion = new RulesetVersion(majorVersionNumber, minorVersionNumber, kind);
        _versions.Add(draftVersion);
        return draftVersion;
    }
}
