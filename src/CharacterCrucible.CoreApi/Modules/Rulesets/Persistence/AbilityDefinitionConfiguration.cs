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

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Key).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Name).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Description).IsRequired();
        builder.Property(a => a.AbilityType).IsRequired();
        builder.Property(a => a.RequiresApproval).IsRequired();
        builder.Property(a => a.SortOrder).IsRequired();
        builder.Property(a => a.IsAvailable).IsRequired();

        builder.HasIndex(a => new { a.RulesetVersionId, a.Key }).IsUnique();

        builder.ComplexProperty(a => a.CostRule);

        // A complex collection nested inside a complex collection: prerequisite groups, each
        // holding entries. ToJson has to go inside the lambda — the action overload returns the
        // entity builder, not the collection builder, so chaining it outside does not compile.
        builder.ComplexCollection<IList<PrerequisiteGroup>, PrerequisiteGroup>(
            "PrerequisiteGroups",
            group =>
            {
                group.ToJson();
                group.ComplexCollection<IList<PrerequisiteEntry>, PrerequisiteEntry>("EntryValues");
                group.Ignore(nameof(PrerequisiteGroup.Entries));
            });

        builder.Ignore(a => a.Prerequisites);
    }
}
