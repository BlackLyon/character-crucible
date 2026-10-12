namespace CharacterCrucible.CoreApi.Modules.Characters.Domain.Constants;

/// <summary>
/// Maximum lengths for the module's string columns. One source for the entity guards and the EF
/// configuration: kept apart they drift, and the drift surfaces as a DbUpdateException at save
/// time rather than an ArgumentException at the call.
/// </summary>
internal static class FieldLengths
{
    internal const int Name = 200;
}
