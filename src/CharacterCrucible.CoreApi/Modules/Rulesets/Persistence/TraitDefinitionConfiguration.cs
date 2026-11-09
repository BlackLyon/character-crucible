using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Constants;
using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CharacterCrucible.CoreApi.Modules.Rulesets.Persistence;

/// <summary>Maps <see cref="TraitDefinition"/>.</summary>
public class TraitDefinitionConfiguration : IEntityTypeConfiguration<TraitDefinition>
{
    public void Configure(EntityTypeBuilder<TraitDefinition> builder)
    {
        builder.ToTable("trait_definition", RulesetsSchema.Name);

        builder.Property(t => t.Key).HasMaxLength(FieldLengths.Key);
        builder.Property(t => t.Name).HasMaxLength(FieldLengths.Name);
        builder.Property(t => t.DomainKey).HasMaxLength(FieldLengths.Key);

        builder.HasIndex(t => new { t.RulesetVersionId, t.Key }).IsUnique();

        // Inline columns rather than jsonb, so cost rules stay queryable.
        builder.ComplexProperty(t => t.CostRule);
    }
}
