using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Entities;
using CharacterCrucible.CoreApi.Modules.Rulesets.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CharacterCrucible.CoreApi.Modules.Rulesets.Persistence;

/// <summary>Maps <see cref="TraitDefinition"/>.</summary>
public class TraitDefinitionConfiguration : IEntityTypeConfiguration<TraitDefinition>
{
    public void Configure(EntityTypeBuilder<TraitDefinition> builder)
    {
        builder.ToTable("trait_definition", RulesetsSchema.Name);

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Key).HasMaxLength(FieldLengths.Key).IsRequired();
        builder.Property(t => t.Name).HasMaxLength(FieldLengths.Name).IsRequired();
        builder.Property(t => t.Description).IsRequired();
        builder.Property(t => t.Category).IsRequired();
        builder.Property(t => t.DomainKey).HasMaxLength(FieldLengths.Key);
        builder.Property(t => t.MinValue).IsRequired();
        builder.Property(t => t.MaxValue).IsRequired();
        builder.Property(t => t.SortOrder).IsRequired();
        builder.Property(t => t.IsAvailable).IsRequired();

        builder.HasIndex(t => new { t.RulesetVersionId, t.Key }).IsUnique();

        // Inline columns rather than jsonb, so cost rules stay queryable.
        builder.ComplexProperty(t => t.CostRule);
    }
}
