using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Enums;
using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.ValueObjects;
using System.Security.Cryptography;
using System.Text;

namespace CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Entities;

/// <summary>One state of a ruleset. Editable while draft, frozen once published.</summary>
public class RulesetVersion
{
    /// <summary>For EF materialisation only. Rows in the database have already been validated.</summary>
    private RulesetVersion() { }

    // internal, so Ruleset.CreateDraft is the only door: the one-draft and version-number
    // invariants live there, and a public constructor would let callers skip them.
    internal RulesetVersion(int majorVersion, int minorVersion, RulesetVersionKind releaseType)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(majorVersion, nameof(majorVersion));
        ArgumentOutOfRangeException.ThrowIfNegative(minorVersion, nameof(minorVersion));

        MajorVersion = majorVersion;
        MinorVersion = minorVersion;
        ReleaseType = releaseType;
    }


    public Guid Id { get; private set; }
    public Guid RulesetId { get; private set; }
    public int MajorVersion { get; private set; }
    public int MinorVersion { get; private set; }
    public RulesetVersionKind ReleaseType { get; private set; }
    public string? ReleaseNotes { get; private set; }
    // Derived, not stored: published and "has a publication record" are one fact.
    public RulesetVersionStatus Status => Publication is null ? RulesetVersionStatus.Draft : RulesetVersionStatus.Published;
    public PublicationRecord? Publication { get; private set; }
    private readonly List<DomainDefinition> _domainDefinitions = [];
    public IReadOnlyCollection<DomainDefinition> DomainDefinitions => _domainDefinitions.AsReadOnly();
    private readonly List<TraitDefinition> _traitDefinitions = [];
    public IReadOnlyCollection<TraitDefinition> TraitDefinitions => _traitDefinitions.AsReadOnly();
    private readonly List<AbilityDefinition> _abilityDefinitions = [];
    public IReadOnlyCollection<AbilityDefinition> AbilityDefinitions => _abilityDefinitions.AsReadOnly();
    private readonly List<ArchetypeDefinition> _archetypeDefinitions = [];
    public IReadOnlyCollection<ArchetypeDefinition> ArchetypeDefinitions => _archetypeDefinitions.AsReadOnly();

    public string GetVersionString() => $"{MajorVersion}.{MinorVersion}";

    public void AddDefinition(DomainDefinition domainDefinition)
    {
        EnsureDraft();
        _domainDefinitions.Add(domainDefinition);
    }

    public void AddDefinition(TraitDefinition traitDefinition)
    {
        EnsureDraft();
        _traitDefinitions.Add(traitDefinition);
    }

    public void AddDefinition(AbilityDefinition abilityDefinition)
    {
        EnsureDraft();
        _abilityDefinitions.Add(abilityDefinition);
    }

    public void AddDefinition(ArchetypeDefinition archetypeDefinition)
    {
        EnsureDraft();
        _archetypeDefinitions.Add(archetypeDefinition);
    }

    public void RemoveDefinition(DomainDefinition domainDefinition)
    {
        EnsureDraft();
        _domainDefinitions.Remove(domainDefinition);
    }

    public void RemoveDefinition(TraitDefinition traitDefinition)
    {
        EnsureDraft();
        _traitDefinitions.Remove(traitDefinition);
    }

    public void RemoveDefinition(AbilityDefinition abilityDefinition)
    {
        EnsureDraft();
        _abilityDefinitions.Remove(abilityDefinition);
    }

    public void RemoveDefinition(ArchetypeDefinition archetypeDefinition)
    {
        EnsureDraft();
        _archetypeDefinitions.Remove(archetypeDefinition);
    }

    public void UpdateReleaseNotes(string releaseNotes)
    {
        EnsureDraft();
        ReleaseNotes = releaseNotes;
    }

    /// <summary>Freezes the version. Irreversible, and cannot be called twice.</summary>
    public void Publish(string rulesetName, string publisher, TimeProvider timeProvider, Guid publishedBy, string publishedContent)
    {
        if (Status == RulesetVersionStatus.Published)
        {
            throw new InvalidOperationException("The version has already been published.");
        }

        // A published version is immutable, so a blank value here freezes into a snapshot
        // nothing can correct afterwards.
        ArgumentException.ThrowIfNullOrWhiteSpace(rulesetName);
        ArgumentException.ThrowIfNullOrWhiteSpace(publisher);
        ArgumentException.ThrowIfNullOrWhiteSpace(publishedContent);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var contentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(publishedContent)));
        var publishedDate = timeProvider.GetUtcNow();

        Publication = new PublicationRecord(rulesetName, publisher, publishedDate, publishedBy, publishedContent, contentHash, PublicationRecord.CurrentSchemaVersion);
    }

    /// <summary>Guards every mutator. A published version never changes.</summary>
    private void EnsureDraft()
    {
        if (Status == RulesetVersionStatus.Published)
        {
            throw new InvalidOperationException("Not able to edit the version as it has already been published");
        }
    }
}
