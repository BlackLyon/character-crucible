namespace CharacterCrucible.CoreApi.Modules.Rulesets.Domain.ValueObjects;

/// <summary>Satisfy <see cref="RequiredCount"/> of <see cref="Entries"/> — an "any 2 of these" gate.</summary>
public record PrerequisiteGroup
{
    public PrerequisiteGroup(int requiredCount, IEnumerable<PrerequisiteEntry> entries)
    {
        RequiredCount = requiredCount;
        EntryValues = entries?.ToList() ?? new List<PrerequisiteEntry>();
    }

    // Not a positional record, and not collapsible to one: EF binds constructor parameters to
    // scalar properties only, so a collection parameter fails at model build. EF uses this.
    private PrerequisiteGroup() { }

    public int RequiredCount { get; init; }
    private IList<PrerequisiteEntry> EntryValues { get; set; } = new List<PrerequisiteEntry>();
    public IReadOnlyList<PrerequisiteEntry> Entries => EntryValues.AsReadOnly();
}
