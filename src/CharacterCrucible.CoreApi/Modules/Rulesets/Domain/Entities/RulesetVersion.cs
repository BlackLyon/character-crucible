using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Enums;

namespace CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Entities;

public class RulesetVersion(int majorVersion, int minorVersion, Guid rulesetId, RulesetVersionKind kind)
{
    public Guid Id { get; private set; }
    public Guid RulesetId { get; private set; } = rulesetId;
    public int MajorVersion { get; private set; } = majorVersion;
    public int MinorVersion { get; private set; } = minorVersion;
    public RulesetVersionKind Kind { get; private set; } = kind;
    public string? ReleaseNotes { get; private set; }
    public RulesetVersionStatus Status { get; private set; } = RulesetVersionStatus.Draft;
    public string? RulesetName { get; private set; }
    public string? Publisher { get; private set; }
    public DateTimeOffset? PublishedDate { get; private set; }
    public Guid? PublishedBy { get; private set; }
    public string? PublishedContent { get; private set; }
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

    public void Publish(string publishedContent, Guid publishedBy, string rulesetName, string publisher)
    {
        EnsureDraft();
        PublishedContent = publishedContent;
        PublishedBy = publishedBy;
        PublishedDate = DateTimeOffset.UtcNow;
        Status = RulesetVersionStatus.Published;
        Publisher = publisher;
        RulesetName = rulesetName;
    }

    public void UpdateReleaseNotes(string releaseNotes)
    {
        EnsureDraft();
        ReleaseNotes = releaseNotes;
    }

    private void EnsureDraft()
    {
        if (Status == RulesetVersionStatus.Published)
        {
            throw new InvalidOperationException("Not able to edit the version as it has already been published");
        }
    }
}
