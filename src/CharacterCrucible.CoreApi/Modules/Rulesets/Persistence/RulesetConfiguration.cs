using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Entities;
using CharacterCrucible.CoreApi.Modules.Rulesets.Domain;
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

        builder.Property(r => r.Name).HasMaxLength(FieldLengths.Name).IsRequired();
        builder.Property(r => r.Description).IsRequired();
        builder.Property(r => r.Publisher).HasMaxLength(FieldLengths.Publisher).IsRequired();

        // Derived from DerivedFrom, so there is no column. EF maps get-only properties by
        // convention and would otherwise try to persist it.
        builder.Ignore(r => r.Origin);

        // Relationship 1: the versions a ruleset owns. The backing field is found by
        // convention from the property name, so no HasField is needed.
        //
        // Restrict, NOT Cascade. Published versions are immutable snapshots that characters are
        // pinned to and audit entries stamp, so a ruleset that has ever published must be
        // permanent. Cascade made deleting a ruleset either throw or silently destroy every
        // snapshot, decided by whether the caller had written Include(r => r.Versions) --
        // because Cascade here and Restrict below form an ordering cycle EF resolves against
        // whatever graph it happens to have loaded. Restrict removes the cycle and the choice.
        //
        // A ruleset holding only a draft is still deletable: delete the draft through
        // AbandonDraft, then the ruleset.
        builder.HasMany(r => r.Versions)
               .WithOne()
               .HasForeignKey(v => v.RulesetId)
               .OnDelete(DeleteBehavior.Restrict);

        // Relationship 2: which of those versions is in force. A navigation rather than a bare
        // Guid so EF writes the FK — assigning version.Id by hand stored Guid.Empty before the
        // row existed. Restrict, because deleting the current version out from under a ruleset
        // should fail loudly rather than null the pointer.
        builder.HasOne(r => r.CurrentVersion)
               .WithMany()
               .HasForeignKey(r => r.CurrentVersionId)
               .OnDelete(DeleteBehavior.Restrict);

        // Relationship 3: lineage. The spec calls DerivedFrom a self-FK and it shipped as a bare
        // nullable Guid, so an UPDATE could point it at a ruleset that does not exist — and Origin
        // is derived from it, which would report Derived from nothing. No navigation property,
        // because nothing walks the lineage; the FK alone is what makes the value meaningful.
        builder.HasOne<Ruleset>()
               .WithMany()
               .HasForeignKey(r => r.DerivedFrom)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
