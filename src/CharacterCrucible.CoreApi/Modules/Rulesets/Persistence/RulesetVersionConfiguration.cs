using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CharacterCrucible.CoreApi.Modules.Rulesets.Persistence;

/// <summary>Maps <see cref="RulesetVersion"/>.</summary>
public class RulesetVersionConfiguration : IEntityTypeConfiguration<RulesetVersion>
{
    public void Configure(EntityTypeBuilder<RulesetVersion> builder)
    {
        builder.ToTable("ruleset_version", RulesetsSchema.Name);

        builder.HasKey(v => v.Id);

        builder.Property(v => v.MajorVersion).IsRequired();
        builder.Property(v => v.MinorVersion).IsRequired();
        builder.Property(v => v.ReleaseType).IsRequired();
        builder.Property(v => v.ReleaseNotes);

        // Derived from whether Publication is set, so there is no column. Published and "has a
        // publication record" are one fact; a stored copy could disagree with it.
        builder.Ignore(v => v.Status);

        // The four definition collections. Cascade, because a definition has no meaning apart
        // from the version that owns it. Backing fields are found by convention from the
        // property names, so no HasField is needed.
        builder.HasMany(v => v.DomainDefinitions)
               .WithOne()
               .HasForeignKey(d => d.RulesetVersionId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(v => v.TraitDefinitions)
               .WithOne()
               .HasForeignKey(t => t.RulesetVersionId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(v => v.AbilityDefinitions)
               .WithOne()
               .HasForeignKey(a => a.RulesetVersionId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(v => v.ArchetypeDefinitions)
               .WithOne()
               .HasForeignKey(a => a.RulesetVersionId)
               .OnDelete(DeleteBehavior.Cascade);

        // Optional complex property: the whole publish stamp, or nothing. That is what makes
        // "a published version has every field set" structural rather than a rule. Status is
        // derived from whether this is null, so this mapping is load-bearing for every guard
        // in the module.
        builder.ComplexProperty(v => v.Publication, publication =>
        {
            // The frozen snapshot. jsonb rather than text so Postgres validates it is JSON and
            // the Rules service's document can be queried into if ever needed. The column type
            // has to be set explicitly: an inline complex property does not inherit it.
            publication.Property(p => p.PublishedContent).HasColumnType("jsonb");
            publication.Property(p => p.ContentHash).HasMaxLength(64);
            publication.Property(p => p.RulesetName).HasMaxLength(200);
            publication.Property(p => p.Publisher).HasMaxLength(200);

            // Added before the first publish deliberately. Snapshots are immutable, so a
            // document written today has to stay readable forever — this is the only thing that
            // tells a reader which shape to expect. Added later it becomes a permanent
            // "null means version 1" special case in rows that cannot be migrated.
            publication.Property(p => p.SchemaVersion).IsRequired();
        });

        // Invariant 0. Implicit when the version was a single sequential int; needs saying with
        // two columns.
        builder.HasIndex(v => new { v.RulesetId, v.MajorVersion, v.MinorVersion })
               .IsUnique()
               .HasDatabaseName("ix_ruleset_version_ruleset_id_major_minor");

        // Invariant 1, at most one draft per ruleset. A partial index, because the constraint
        // only applies to drafts — and draft-ness is derived from Publication being null, so the
        // filter targets schema_version, which is present-or-absent and cannot be half-written,
        // rather than a Status value that does not exist in the database. This is the backstop
        // the module README promises: CreateDraft is blind whenever a Ruleset was loaded
        // without its versions.
        builder.HasIndex(v => v.RulesetId)
               .IsUnique()
               .HasFilter("publication_schema_version IS NULL")
               .HasDatabaseName("ux_ruleset_version_one_draft_per_ruleset");
    }
}
