using System.Linq.Expressions;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.Entities;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CharacterCrucible.CoreApi.Modules.Characters.Persistence;

/// <summary>How a <see cref="Character"/> is loaded for the methods that need its in-flight requests.</summary>
public static class CharacterQueries
{
    // IsInFlight is computed, so EF can't translate it and the include needs its own copy.
    internal static readonly Expression<Func<AdvancementRequest, bool>> InFlight = r =>
        r.RequestStatus == RequestStatus.Approved
        || r.RequestStatus == RequestStatus.PendingApproval
        || r.RequestStatus == RequestStatus.Queued;

    /// <summary>Loads in-flight requests only, which SubmitRequest and ApplyAdvancement require.</summary>
    public static IQueryable<Character> WithInFlightRequests(this IQueryable<Character> characters) =>
        characters.Include(c => EF.Property<IList<AdvancementRequest>>(c, "_requests").AsQueryable().Where(InFlight));
}
