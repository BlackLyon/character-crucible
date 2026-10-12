using CharacterCrucible.CoreApi.Modules.Characters.Domain.Constants;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.ValueObjects;

namespace CharacterCrucible.CoreApi.Modules.Characters.Domain.Entities;

public class Character
{
    public Character(Guid ownerId, Guid rulesetVersionId, string name, string archetypeKey, string sex, string race, int age)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(archetypeKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(sex);
        ArgumentException.ThrowIfNullOrWhiteSpace(race);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(age, nameof(age));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.Length, FieldLengths.Name, nameof(name));

        OwnerId = ownerId;
        RulesetVersionId = rulesetVersionId;
        Name = name;
        ArchetypeKey = archetypeKey;
        Sex = sex;
        Race = race;
        Age = age;
    }
    /// <summary>For EF materialisation only. Rows in the database have already been validated.</summary>
    private Character() { }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid RulesetVersionId { get; private set; }
    public string Name { get; private set; } = null!;
    public string ArchetypeKey { get; private set; } = null!;
    public string Sex { get; private set; } = null!;
    public string Race { get; private set; } = null!;
    public int Age { get; private set; }
    public CharacterStatus Status { get; private set; } = CharacterStatus.PendingApproval;
    public uint RowVersion { get; private set; }
    private readonly IList<CharacterTrait> _traits = [];
    public IReadOnlyCollection<CharacterTrait> Traits => _traits.AsReadOnly();
    private readonly IList<CharacterAbility> _abilities = [];
    public IReadOnlyCollection<CharacterAbility> Abilities => _abilities.AsReadOnly();
    public int TotalEarnedXp { get; private set; }
    public int UnspentXp { get; private set; }
    private readonly IList<XpLedgerEntry> _xpLedgerEntries = [];
    public DateTimeOffset CreatedDate { get; private set; }
}
