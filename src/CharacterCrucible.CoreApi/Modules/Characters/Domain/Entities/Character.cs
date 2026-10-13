using CharacterCrucible.CoreApi.Modules.Characters.Domain.Constants;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.ValueObjects;

namespace CharacterCrucible.CoreApi.Modules.Characters.Domain.Entities;

public class Character
{
    public Character(Guid ownerId, Guid rulesetVersionId, string name, string archetypeKey, CharacterSex sex, string race, int age, TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(archetypeKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(race);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(age, nameof(age));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.Length, FieldLengths.Name, nameof(name));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(archetypeKey.Length, FieldLengths.ArchetypeKey, nameof(archetypeKey));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(race.Length, FieldLengths.Race, nameof(race));
        
        if (!Enum.IsDefined(sex))
        {
            throw new ArgumentOutOfRangeException(nameof(sex), sex, "Invalid character sex.");
        }

        OwnerId = ownerId;
        RulesetVersionId = rulesetVersionId;
        Name = name;
        ArchetypeKey = archetypeKey;
        Sex = sex;
        Race = race;
        Age = age;
        CreatedDate = timeProvider.GetUtcNow();
    }
    /// <summary>For EF materialisation only. Rows in the database have already been validated.</summary>
    private Character() { }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid RulesetVersionId { get; private set; }
    public string Name { get; private set; } = null!;
    public string ArchetypeKey { get; private set; } = null!;
    public CharacterSex Sex { get; private set; }
    public string Race { get; private set; } = null!;
    public int Age { get; private set; }
    public CharacterStatus Status { get; private set; } = CharacterStatus.Draft;
    public uint RowVersion { get; private set; }
    private readonly IList<CharacterTrait> _traits = [];
    public IReadOnlyCollection<CharacterTrait> Traits => _traits.AsReadOnly();
    private readonly IList<CharacterAbility> _abilities = [];
    public IReadOnlyCollection<CharacterAbility> Abilities => _abilities.AsReadOnly();
    public int TotalEarnedXp { get; private set; }
    public int UnspentXp { get; private set; }
    private readonly IList<XpLedgerEntry> _xpLedgerEntries = [];
    public DateTimeOffset CreatedDate { get; private set; }

    public void Approve()
    {
    }

    public void SendBack(string reason)
    {
    }
    public void RemoveFromPlay(CharacterStatus status, string reason)
    {
    }

    public void ReturnToPlay()
    {
    }

    public void SubmitForApproval()
    {
    }
    public void GrantXp(int amount, string reason, TimeProvider timeProvider)
    {
    }


}
