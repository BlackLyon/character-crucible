using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Entities;
using CharacterCrucible.CoreApi.Modules.Rulesets.Domain;
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
            // text, NOT jsonb. ContentHash is SHA-256 over the exact string Publish was
            // given; jsonb stores a parsed document and re-serialises on read, so
            // {"traits":[]} comes back as {"traits": []} -- one space, different hash,
            // and the integrity check could never pass. jsonb also reorders keys and
            // DISCARDS duplicates, which is data loss in a supposedly frozen snapshot.
            // Validating the content is JSON belongs in Publish, not the column type.
            publication.Property(p => p.PublishedContent).HasColumnType("text");
            publication.Property(p => p.ContentHash).HasMaxLength(FieldLengths.ContentHash);
            publication.Property(p => p.RulesetName).HasMaxLength(FieldLengths.Name);
            publication.Property(p => p.Publisher).HasMaxLength(FieldLengths.Publisher);

            // Nullable by necessity: a draft has no publication, and the partial index
            // below filters on this column being NULL. The CHECK constraint is what makes
            // it required-when-present. Added before the first publish so the index has an
            // anchor, not because a later add-and-backfill would be unsafe.
            publication.Property(p => p.SchemaVersion);
        });

        // Invariant 0. Implicit when the version was a single sequential int; needs saying with
        // two columns.
        builder.HasIndex(v => new { v.RulesetId, v.MajorVersion, v.MinorVersion })
               .IsUnique()
               .HasDatabaseName("ix_ruleset_version_ruleset_id_major_minor");

        // Invariant 2, made real. PublicationRecord's constructor cannot build a partial stamp,
        // but EF maps it to seven INDEPENDENT NULLABLE columns -- "absent" for an optional
        // complex property can only be expressed as all of them being NULL. So the type's
        // guarantee stops at the boundary, and nothing below it stopped a half-written row.
        // This re-imposes the record's own rule where it was lost. It also makes the index
        // filter below safe whichever column it names, because all seven now move together.
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_ruleset_version_publication_all_or_nothing",
            "num_nulls(publication_ruleset_name, publication_publisher, " +
            "publication_published_date, publication_published_by, " +
            "publication_published_content, publication_content_hash, " +
            "publication_schema_version) IN (0, 7)"));

        // Invariant 1, at most one draft per ruleset. Partial because the rule applies only to
        // drafts, and draft-ness lives in the publication columns rather than a Status value,
        // which is derived and has no column. The backstop the module README promises:
        // CreateDraft is blind whenever a Ruleset was loaded without its versions.
        //
        // The filter column is interchangeable *only because of the CHECK above*. Without it EF
        // and this index disagreed: EF decides Publication is null from content_hash alone (the
        // first required property alphabetically -- an implementation detail, not a contract),
        // while this filter names schema_version. See rulesets.md for the measurements.
        builder.HasIndex(v => v.RulesetId)
               .IsUnique()
               .HasFilter("publication_schema_version IS NULL")
               .HasDatabaseName("ux_ruleset_version_one_draft_per_ruleset");
    }
}
