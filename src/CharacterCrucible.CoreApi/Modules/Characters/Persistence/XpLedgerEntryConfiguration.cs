using CharacterCrucible.CoreApi.Modules.Characters.Domain.Constants;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CharacterCrucible.CoreApi.Modules.Characters.Persistence;

/// <summary>Maps <see cref="XpLedgerEntry"/>.</summary>
public class XpLedgerEntryConfiguration : IEntityTypeConfiguration<XpLedgerEntry>
{
    public void Configure(EntityTypeBuilder<XpLedgerEntry> builder)
    {
        builder.ToTable("xp_ledger_entry", CharactersSchema.Name);

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Reason).HasMaxLength(FieldLengths.Reason).IsRequired();

        // Every history read is one character's rows in order. The FK index alone covers the
        // filter but not the ordering.
        builder.HasIndex(l => new { l.CharacterId, l.OccurredAt });

        // The id is copied from request.Id, not written by EF. The FK makes a wrong copy, such as
        // Guid.Empty from an unsaved request, fail at save rather than be stored.
        builder.HasOne<AdvancementRequest>()
               .WithMany()
               .HasForeignKey(l => l.AdvancementRequestId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
