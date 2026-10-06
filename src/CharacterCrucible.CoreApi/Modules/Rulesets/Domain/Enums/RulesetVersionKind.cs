namespace CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Enums;

// Numbered explicitly because ReleaseType is a stored column: a persisted ordinal has to
// survive a member being reordered or inserted. Starting at 1 also means default(T) is not a
// valid member, so an unset value is detectable rather than reading as a legitimate "Errata".
public enum RulesetVersionKind
{
    Errata = 1,
    Playtest = 2,
    Release = 3,
}
