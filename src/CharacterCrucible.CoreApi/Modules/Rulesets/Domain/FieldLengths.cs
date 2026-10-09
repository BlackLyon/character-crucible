namespace CharacterCrucible.CoreApi.Modules.Rulesets.Domain;

/// <summary>
/// Maximum lengths for the module's string columns. One source for the entity guards and the EF
/// configuration: kept apart they drift, and the drift surfaces as a DbUpdateException at save
/// time rather than an ArgumentException at the call.
/// </summary>
internal static class FieldLengths
{
    /// <summary>Definition keys. DomainKey shares it because it holds a Key.</summary>
    internal const int Key = 100;

    internal const int Name = 200;
    internal const int Publisher = 200;

    /// <summary>SHA-256 hex. Exactly this, not a maximum, so nothing guards against it.</summary>
    internal const int ContentHash = 64;
}
