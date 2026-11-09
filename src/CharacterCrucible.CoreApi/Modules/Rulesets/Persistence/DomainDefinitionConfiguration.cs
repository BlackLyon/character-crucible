using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Constants;
using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Entities;
using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CharacterCrucible.CoreApi.Modules.Rulesets.Persistence;

/// <summary>Maps <see cref="DomainDefinition"/>.</summary>
public class DomainDefinitionConfiguration : IEntityTypeConfiguration<DomainDefinition>
{
    public void Configure(EntityTypeBuilder<DomainDefinition> builder)
    {
        builder.ToTable("domain_definition", RulesetsSchema.Name);

        builder.Property(d => d.Key).HasMaxLength(FieldLengths.Key);
        builder.Property(d => d.Name).HasMaxLength(FieldLengths.Name);

        builder.HasIndex(d => new { d.RulesetVersionId, d.Key }).IsUnique();

        // jsonb. The mapped member is the private IList; the public read-only view has to
        // be ignored or EF tries to map it too. The string overload is required: the
        // single-generic form binds to the primitive-collection overload and fails.
        builder.ComplexCollection<IList<DomainBand>, DomainBand>("BandValues").ToJson("bands");
        builder.Ignore(d => d.Bands);
    }
}
