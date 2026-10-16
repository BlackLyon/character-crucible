namespace CharacterCrucible.CoreApi.Modules.Characters.Domain.ValueObjects;

public record ChangeEntry(string Path, string Label, string? From, string? To);
