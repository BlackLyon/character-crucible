namespace CharacterCrucible.CoreApi.Modules.Rulesets.Domain.ValueObjects;

public record PrerequisiteGroup
{
    public PrerequisiteGroup(int requiredCount, IEnumerable<PrerequisiteEntry> entries)  
    {
        RequiredCount = requiredCount;
        EntryValues = entries?.ToList() ?? new List<PrerequisiteEntry>();
    }

    private PrerequisiteGroup() { }      // EF materialises through this

    public int RequiredCount { get; init; }
    private IList<PrerequisiteEntry> EntryValues { get; set; } = new List<PrerequisiteEntry>();
    public IReadOnlyList<PrerequisiteEntry> Entries => EntryValues.AsReadOnly();

}
