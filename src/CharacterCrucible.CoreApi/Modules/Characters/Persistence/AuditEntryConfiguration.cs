using CharacterCrucible.CoreApi.Modules.Characters.Domain.Constants;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.Entities;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CharacterCrucible.CoreApi.Modules.Characters.Persistence;

/// <summary>Maps <see cref="AuditEntry"/>.</summary>
public class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_entry", CharactersSchema.Name);

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Reason).HasMaxLength(FieldLengths.Reason);
        builder.Property(a => a.EngineVersion).HasMaxLength(FieldLengths.EngineVersion);

        // The diff, as one jsonb document. The entries are the private IList, so the JSON key is
        // named here; otherwise the private name would become the stored format.
        builder.ComplexProperty(a => a.Change, change =>
        {
            change.ToJson("change");
            change.ComplexCollection<IList<ChangeEntry>, ChangeEntry>("ChangeData").HasJsonPropertyName("Changes");
            change.Ignore(c => c.Changes);
        });

        // As on the ledger: one character's history, in order.
        builder.HasIndex(a => new { a.CharacterId, a.OccurredAt });

        // Copied from request.Id, as on the ledger, so the FK is what catches a wrong copy.
        builder.HasOne<AdvancementRequest>()
               .WithMany()
               .HasForeignKey(a => a.AdvancementRequestId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
