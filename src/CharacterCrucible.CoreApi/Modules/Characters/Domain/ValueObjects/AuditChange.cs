namespace CharacterCrucible.CoreApi.Modules.Characters.Domain.ValueObjects;

public record AuditChange
{
    public AuditChange(string summary, IEnumerable<ChangeEntry> changes)
    {
        Summary = summary;
        ChangeData = changes?.ToList() ?? new List<ChangeEntry>();
    }

    // Not a positional record, and not collapsible to one: EF binds constructor parameters to
    // scalar properties only, so a collection parameter fails at model build. EF uses this.
    private AuditChange() {}

    public string Summary { get; init; } = null!;
    private IList<ChangeEntry> ChangeData { get; set; } = new List<ChangeEntry>();
    public IReadOnlyList<ChangeEntry> Changes => ChangeData.AsReadOnly();
}
