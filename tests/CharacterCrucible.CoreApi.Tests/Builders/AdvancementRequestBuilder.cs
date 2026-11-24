using CharacterCrucible.CoreApi.Modules.Characters.Domain.Entities;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;
using Microsoft.Extensions.Time.Testing;
using static CharacterCrucible.CoreApi.Tests.Builders.TestCharacters;

namespace CharacterCrucible.CoreApi.Tests.Builders;

/// <summary>
/// Builds a request for one character, priced under that character's own version by default, through
/// the domain's factories and transitions. The builder itself is immutable, but with
/// <see cref="Submitted"/> each <c>Build()</c> submits to the character, so build it once.
/// </summary>
internal sealed record AdvancementRequestBuilder
{
    public AdvancementRequestBuilder(Character character, TargetKind kind)
    {
        Character = character;
        Kind = kind;
        Key = kind == TargetKind.Trait ? "strength" : "riposte";
    }

    private Character Character { get; }
    private TargetKind Kind { get; }
    private string Key { get; init; }
    private int? Rating { get; init; }
    private int Cost { get; init; }
    private bool IsGated { get; init; }
    private Guid? RulesetVersionId { get; init; }
    private RequestStatus? Status { get; init; }
    private bool IsSubmitted { get; init; }
    private DateTimeOffset? SubmittedDate { get; init; }

    public AdvancementRequestBuilder WithKey(string key) => this with { Key = key };

    /// <summary>Traits only: an ability has no rating.</summary>
    public AdvancementRequestBuilder WithRating(int rating) => this with { Rating = rating };

    public AdvancementRequestBuilder WithCost(int cost) => this with { Cost = cost };

    /// <summary>Needs a narrator's approval, so it starts as PendingApproval.</summary>
    public AdvancementRequestBuilder Gated() => this with { IsGated = true };

    /// <summary>Priced under another version, for the apply-time mismatch check.</summary>
    public AdvancementRequestBuilder OnVersion(Guid rulesetVersionId) => this with { RulesetVersionId = rulesetVersionId };

    /// <summary>Approved, Rejected and PendingApproval imply <see cref="Gated"/>.</summary>
    public AdvancementRequestBuilder InStatus(RequestStatus status) => this with { Status = status };

    /// <summary>Submitted to its character before any later transition, which is the only order the domain allows.</summary>
    public AdvancementRequestBuilder Submitted() => this with { IsSubmitted = true };

    /// <summary>
    /// Made at another time than <see cref="TestCharacters.Now"/>, so a test can tell this request's
    /// submission from one made before it. Sets the date only; <see cref="Submitted"/> still submits.
    /// </summary>
    public AdvancementRequestBuilder WithSubmittedDate(DateTimeOffset submittedDate) => this with { SubmittedDate = submittedDate };

    public AdvancementRequest Build()
    {
        var requiresApproval = IsGated
            || Status is RequestStatus.PendingApproval or RequestStatus.Approved or RequestStatus.Rejected;
        if (IsGated && Status == RequestStatus.Queued)
        {
            throw new InvalidOperationException("A gated request starts as PendingApproval, never Queued.");
        }
        if (Kind == TargetKind.Ability && Rating is not null)
        {
            throw new InvalidOperationException("WithRating is for trait requests; an ability has no rating.");
        }

        var versionId = RulesetVersionId ?? Character.RulesetVersionId;
        var clock = SubmittedDate is { } at ? new FakeTimeProvider(at) : Clock();
        var request = Kind == TargetKind.Trait
            ? AdvancementRequest.ForTrait(Player.Id, clock, Key, Rating ?? 3, Cost, versionId, EngineVersion, requiresApproval)
            : AdvancementRequest.ForAbility(Player.Id, clock, Key, Cost, versionId, EngineVersion, requiresApproval);

        if (IsSubmitted)
        {
            Character.SubmitRequest(request);
        }

        // A gated request reaches Approved, Applied, Failed or Abandoned only through approval.
        if (requiresApproval && Status is not (null or RequestStatus.PendingApproval or RequestStatus.Rejected))
        {
            request.Approve(Narrator.Id, clock);
        }

        switch (Status)
        {
            case null or RequestStatus.Queued or RequestStatus.PendingApproval or RequestStatus.Approved:
                break;
            case RequestStatus.Rejected:
                request.Reject(Narrator.Id, "Not this session.", clock);
                break;
            case RequestStatus.Applied:
                request.MarkApplied(clock);
                break;
            case RequestStatus.Failed:
                request.MarkFailed("The cost changed.", clock);
                break;
            case RequestStatus.Abandoned:
                request.MarkAbandoned("Dead-lettered.", clock);
                break;
            default:
                throw new InvalidOperationException($"No builder path to {Status} yet.");
        }

        return request;
    }
}
