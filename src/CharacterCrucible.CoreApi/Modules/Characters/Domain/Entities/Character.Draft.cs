using CharacterCrucible.CoreApi.Modules.Characters.Domain.Constants;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.Inputs;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.ValueObjects;

namespace CharacterCrucible.CoreApi.Modules.Characters.Domain.Entities;

public partial class Character
{
    public void UpdateDraft(CharacterDraft draft)
    {
        if (Status != CharacterStatus.Draft)
        {
            throw new InvalidOperationException($"Cannot update a character that is not in the {CharacterStatus.Draft} status. Current status: {Status}.");
        }

        ApplyDraft(draft);
    }

    private void ApplyDraft(CharacterDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(draft.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(draft.Race);
        ArgumentException.ThrowIfNullOrWhiteSpace(draft.ArchetypeKey);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(draft.Age, nameof(draft.Age));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(draft.Name.Length, FieldLengths.Name, nameof(draft.Name));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(draft.Race.Length, FieldLengths.Race, nameof(draft.Race));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(draft.ArchetypeKey.Length, FieldLengths.Key, nameof(draft.ArchetypeKey));
        var traits = ValidateTraits(draft.Traits);
        var abilities = ValidateAbilities(draft.Abilities);

        if (!Enum.IsDefined(draft.Sex))
        {
            throw new ArgumentOutOfRangeException(nameof(draft.Sex), draft.Sex, "Invalid character sex.");
        }

        Name = draft.Name;
        ArchetypeKey = draft.ArchetypeKey;
        Sex = draft.Sex;
        Race = draft.Race;
        Age = draft.Age;
        _traits.Clear();
        foreach (var trait in traits)
        {
            _traits.Add(trait);
        }

        _abilities.Clear();
        foreach (var ability in abilities)
        {
            _abilities.Add(ability);
        }
    }

    private static List<CharacterTrait> ValidateTraits(IReadOnlyList<CharacterTrait> traits)
    {
        ArgumentNullException.ThrowIfNull(traits);

        var validated = new List<CharacterTrait>(traits.Count);
        var seenKeys = new HashSet<string>();

        foreach (var trait in traits)
        {
            ArgumentNullException.ThrowIfNull(trait);
            ArgumentException.ThrowIfNullOrWhiteSpace(trait.TraitKey, nameof(trait.TraitKey));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(trait.TraitKey.Length, FieldLengths.Key, nameof(trait.TraitKey));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(trait.Rating, nameof(trait.Rating));

            if (!seenKeys.Add(trait.TraitKey))
            {
                throw new ArgumentException($"Duplicate trait '{trait.TraitKey}'.", nameof(traits));
            }

            validated.Add(trait);
        }

        return validated;
    }

    private static List<CharacterAbility> ValidateAbilities(IReadOnlyList<CharacterAbility> abilities)
    {
        ArgumentNullException.ThrowIfNull(abilities);

        var validated = new List<CharacterAbility>(abilities.Count);
        var seenKeys = new HashSet<string>();

        foreach (var ability in abilities)
        {
            ArgumentNullException.ThrowIfNull(ability);
            ArgumentException.ThrowIfNullOrWhiteSpace(ability.AbilityKey, nameof(ability.AbilityKey));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(ability.AbilityKey.Length, FieldLengths.Key, nameof(ability.AbilityKey));

            if (ability.AcquiredDate is not null)
            {
                throw new ArgumentException($"Ability '{ability.AbilityKey}' has an AcquiredDate set. Abilities cannot have an acquired date in a draft.", nameof(abilities));
            }

            if (!seenKeys.Add(ability.AbilityKey))
            {
                throw new ArgumentException($"Duplicate ability '{ability.AbilityKey}'.", nameof(abilities));
            }

            validated.Add(ability);
        }

        return validated;
    }
}
