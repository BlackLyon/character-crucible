using CharacterCrucible.CoreApi.Modules.Characters.Domain.Constants;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.Entities;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CharacterCrucible.CoreApi.Modules.Characters.Persistence;

/// <summary>Maps <see cref="Character"/> and its three child collections.</summary>
public class CharacterConfiguration : IEntityTypeConfiguration<Character>
{
    public void Configure(EntityTypeBuilder<Character> builder)
    {
        builder.ToTable("character", CharactersSchema.Name);

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).HasMaxLength(FieldLengths.Name).IsRequired();
        builder.Property(c => c.ArchetypeKey).HasMaxLength(FieldLengths.Key).IsRequired();
        builder.Property(c => c.Race).HasMaxLength(FieldLengths.Race).IsRequired();

        // Postgres xmin: assigned by the database, so no mutator can forget to bump it.
        builder.Property(c => c.RowVersion).IsRowVersion();

        // jsonb, mapped through the private ILists. The read-only views are ignored or EF tries
        // to map them too.
        builder.ComplexCollection<IList<CharacterTrait>, CharacterTrait>("_traits").ToJson("traits");
        builder.ComplexCollection<IList<CharacterAbility>, CharacterAbility>("_abilities", abilities =>
        {
            abilities.ToJson("abilities");

            // The context's timestamptz convention reaches into JSON too, and the migration
            // snapshot then emits a HasColumnType call that doesn't compile (EF Core 10.0.12).
            // A JSON property has no column type, so clearing it loses nothing. Every DateTimeOffset
            // inside JSON needs this line until EF is fixed; without it, the next migration won't build.
            abilities.Property(a => a.AcquiredDate).HasAnnotation(RelationalAnnotationNames.ColumnType, null);
        });
        builder.Ignore(c => c.Traits);
        builder.Ignore(c => c.Abilities);

        // Field-only navigations, so EF has to be told the names. Restrict on all three: requests,
        // the ledger and the audit trail are history and outlive the character. EF's default for
        // a required FK is Cascade, which would delete it.
        builder.HasMany<AdvancementRequest>("_requests")
               .WithOne()
               .HasForeignKey(r => r.CharacterId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany<XpLedgerEntry>("_xpLedgerEntries")
               .WithOne()
               .HasForeignKey(l => l.CharacterId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany<AuditEntry>("_auditEntries")
               .WithOne()
               .HasForeignKey(a => a.CharacterId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
