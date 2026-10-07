using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Entities;
using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CharacterCrucible.CoreApi.Modules.Rulesets.Persistence;

/// <summary>Maps <see cref="ArchetypeDefinition"/>.</summary>
public class ArchetypeDefinitionConfiguration : IEntityTypeConfiguration<ArchetypeDefinition>
{
    public void Configure(EntityTypeBuilder<ArchetypeDefinition> builder)
    {
        builder.ToTable("archetype_definition", RulesetsSchema.Name);

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Key).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Name).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Description).IsRequired();
        builder.Property(a => a.SortOrder).IsRequired();
        builder.Property(a => a.IsAvailable).IsRequired();

        builder.HasIndex(a => new { a.RulesetVersionId, a.Key }).IsUnique();

        // Three jsonb collections. Mapped members are the private ILists named in the
        // spec; the public read-only views are ignored.
        builder.ComplexCollection<IList<GrantedRank>, GrantedRank>("Ranks").ToJson();
        builder.ComplexCollection<IList<CostModifier>, CostModifier>("Modifiers").ToJson();
        builder.ComplexCollection<IList<CapModifier>, CapModifier>("Caps").ToJson();
        builder.Ignore(a => a.GrantedRanks);
        builder.Ignore(a => a.CostModifiers);
        builder.Ignore(a => a.CapModifiers);
    }
}
