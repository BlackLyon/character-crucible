namespace CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Enums;

/// <summary>What sort of release a version is.</summary>
// Numbered: persisted as a column, so these must survive a member being reordered. Starting
// at 1 also keeps default(T) from reading as a legitimate Errata.
public enum RulesetVersionKind
{
    Errata = 1,
    Playtest = 2,
    Release = 3,
}
