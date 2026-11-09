using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Constants;
using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Entities;
using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CharacterCrucible.CoreApi.Modules.Rulesets.Persistence;

/// <summary>Maps <see cref="AbilityDefinition"/>.</summary>
public class AbilityDefinitionConfiguration : IEntityTypeConfiguration<AbilityDefinition>
{
    public void Configure(EntityTypeBuilder<AbilityDefinition> builder)
    {
        builder.ToTable("ability_definition", RulesetsSchema.Name);

        builder.Property(a => a.Key).HasMaxLength(FieldLengths.Key);
        builder.Property(a => a.Name).HasMaxLength(FieldLengths.Name);

        builder.HasIndex(a => new { a.RulesetVersionId, a.Key }).IsUnique();

        builder.ComplexProperty(a => a.CostRule);

        // A complex collection nested inside a complex collection: prerequisite groups, each
        // holding entries. Both go into the one jsonb document.
        var groups = builder.ComplexCollection<IList<PrerequisiteGroup>, PrerequisiteGroup>(
            "PrerequisiteGroups");
        groups.ToJson("prerequisites");
        groups.ComplexCollection<IList<PrerequisiteEntry>, PrerequisiteEntry>("EntryValues");
        groups.Ignore(g => g.Entries);

        builder.Ignore(a => a.Prerequisites);
    }
}
