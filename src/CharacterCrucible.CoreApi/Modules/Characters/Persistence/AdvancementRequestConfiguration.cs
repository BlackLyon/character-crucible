using CharacterCrucible.CoreApi.Modules.Characters.Domain.Constants;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.Entities;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CharacterCrucible.CoreApi.Modules.Characters.Persistence;

/// <summary>Maps <see cref="AdvancementRequest"/>.</summary>
public class AdvancementRequestConfiguration : IEntityTypeConfiguration<AdvancementRequest>
{
    // "In flight" is also written in IsInFlight and CharacterQueries.InFlight. A mapping test
    // checks all three agree, status by status.
    internal static readonly string InFlightFilter =
        $"request_status IN ({(int)RequestStatus.Approved}, {(int)RequestStatus.PendingApproval}, {(int)RequestStatus.Queued})";

    public void Configure(EntityTypeBuilder<AdvancementRequest> builder)
    {
        builder.ToTable("advancement_request", CharactersSchema.Name);

        builder.Property(r => r.TargetKey).HasMaxLength(FieldLengths.Key);
        builder.Property(r => r.EvaluatedEngineVersion).HasMaxLength(FieldLengths.EngineVersion);
        builder.Property(r => r.ResolutionReason).HasMaxLength(FieldLengths.Reason);

        builder.Ignore(r => r.IsInFlight);

        // One in-flight request per item. The backstop for SubmitRequest, which only sees the
        // requests its caller loaded. Kind is in the key, so a trait and an ability may share one.
        builder.HasIndex(r => new { r.CharacterId, r.TargetKind, r.TargetKey })
               .IsUnique()
               .HasFilter(InFlightFilter)
               .HasDatabaseName("ux_advancement_request_one_in_flight_per_item");

        // EF treats the index above as covering the FK and drops the FK's own index, but a partial
        // index serves only in-flight queries. This one serves the rest, including the Restrict check.
        builder.HasIndex(r => r.CharacterId);
    }
}
