namespace CharacterCrucible.CoreApi.Modules.Characters.Domain.Constants;

/// <summary>
/// Maximum lengths for the module's string columns. One source for the entity guards and the EF
/// configuration: kept apart they drift, and the drift surfaces as a DbUpdateException at save
/// time rather than an ArgumentException at the call.
/// </summary>
internal static class FieldLengths
{
    internal const int Name = 50;
    internal const int Reason = 250;
    internal const int Race = 20;
    internal const int KeyName = 100;
}
