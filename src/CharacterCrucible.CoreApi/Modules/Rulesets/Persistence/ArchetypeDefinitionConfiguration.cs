using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Constants;
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

        builder.Property(a => a.Key).HasMaxLength(FieldLengths.Key).IsRequired();
        builder.Property(a => a.Name).HasMaxLength(FieldLengths.Name).IsRequired();
        builder.Property(a => a.Description).IsRequired();
        builder.Property(a => a.SortOrder).IsRequired();
        builder.Property(a => a.IsAvailable).IsRequired();

        builder.HasIndex(a => new { a.RulesetVersionId, a.Key }).IsUnique();

        // Three jsonb collections. The mapped members are the private ILists the spec names,
        // but the COLUMN is named after the public property: "modifiers" beside "caps" would put
        // CostModifier in a column called modifiers and CapModifier in one called caps, and both
        // are modifiers. ToJson takes the column name, so the private name need not leak.
        // The public read-only views are ignored.
        builder.ComplexCollection<IList<GrantedRank>, GrantedRank>("Ranks").ToJson("granted_ranks");
        builder.ComplexCollection<IList<CostModifier>, CostModifier>("Modifiers").ToJson("cost_modifiers");
        builder.ComplexCollection<IList<CapModifier>, CapModifier>("Caps").ToJson("cap_modifiers");
        builder.Ignore(a => a.GrantedRanks);
        builder.Ignore(a => a.CostModifiers);
        builder.Ignore(a => a.CapModifiers);
    }
}
