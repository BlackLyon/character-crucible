using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CharacterCrucible.CoreApi.Modules.Rulesets.Persistence;

/// <summary>Maps <see cref="Ruleset"/> into the rulesets schema.</summary>
public class RulesetConfiguration : IEntityTypeConfiguration<Ruleset>
{
    public void Configure(EntityTypeBuilder<Ruleset> builder)
    {
        builder.ToTable("ruleset", RulesetsSchema.Name);

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Description).IsRequired();
        builder.Property(r => r.Publisher).HasMaxLength(200).IsRequired();

        // Derived from DerivedFrom, so there is no column. EF maps get-only properties by
        // convention and would otherwise try to persist it.
        builder.Ignore(r => r.Origin);

        // Relationship 1: the versions a ruleset owns. The backing field is found by
        // convention from the property name, so no HasField is needed.
        builder.HasMany(r => r.Versions)
               .WithOne()
               .HasForeignKey(v => v.RulesetId)
               .OnDelete(DeleteBehavior.Cascade);

        // Relationship 2: which of those versions is in force. A navigation rather than a bare
        // Guid so EF writes the FK — assigning version.Id by hand stored Guid.Empty before the
        // row existed. Restrict, because deleting the current version out from under a ruleset
        // should fail loudly rather than null the pointer.
        builder.HasOne(r => r.CurrentVersion)
               .WithMany()
               .HasForeignKey(r => r.CurrentVersionId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
