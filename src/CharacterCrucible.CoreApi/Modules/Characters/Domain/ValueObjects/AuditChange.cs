namespace CharacterCrucible.CoreApi.Modules.Characters.Domain.ValueObjects;

public record AuditChange
{
    public AuditChange(string summary, IEnumerable<ChangeEntry> changes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentNullException.ThrowIfNull(changes);

        var changeData = changes.ToList();

        if (changeData.Count == 0)
        {
            throw new ArgumentException("An audit change must record at least one change.", nameof(changes));
        }

        Summary = summary;
        ChangeData = changeData;
    }

    // Not a positional record, and not collapsible to one: EF binds constructor parameters to
    // scalar properties only, so a collection parameter fails at model build. EF uses this.
    private AuditChange() {}

    public string Summary { get; init; } = null!;
    private IList<ChangeEntry> ChangeData { get; set; } = new List<ChangeEntry>();
    public IReadOnlyList<ChangeEntry> Changes => ChangeData.AsReadOnly();
}
