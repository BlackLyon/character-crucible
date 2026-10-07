using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Enums;

namespace CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Entities;

/// <summary>A game system. Owns its versions, and which one is currently in force.</summary>
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

    // null! on these three: the EF constructor above does not set them, and EF fills them from
    // the row. Making them nullable would push the check onto every caller instead.
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public string Publisher { get; private set; } = null!;

    // Derived, not stored, so Origin cannot contradict DerivedFrom.
    public RulesetKind Origin => DerivedFrom is null ? RulesetKind.Base : RulesetKind.Derived;
    public Guid? DerivedFrom { get; private set; }

    // A navigation rather than a Guid: EF writes the FK, so no id is ever assigned by hand.
    // Assigning version.Id here would store Guid.Empty before the row exists.
    public RulesetVersion? CurrentVersion { get; private set; }

    private readonly IList<RulesetVersion> _versions = [];
    public IReadOnlyCollection<RulesetVersion> Versions => _versions.AsReadOnly();

    /// <summary>Promotes a published version to the one in force. Requires a two-phase save.</summary>
    public void SetCurrentVersion(RulesetVersion version)
    {
        // Membership, not an id comparison: Id is Guid.Empty until EF assigns it, so comparing
        // ids is vacuously true before the rows are persisted.
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

    /// <summary>Opens a new draft. The only way a version is created.</summary>
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
