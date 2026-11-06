using CharacterCrucible.CoreApi.Modules.Characters.Domain.Constants;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.Enums;
using CharacterCrucible.CoreApi.Modules.Characters.Domain.ValueObjects;

using static CharacterCrucible.CoreApi.Modules.Characters.Domain.ValueObjects.AuditChanges;

namespace CharacterCrucible.CoreApi.Modules.Characters.Domain.Entities;

public partial class Character
{
    /// <summary>Accepts a new advancement request. Requires in-flight requests to be loaded.</summary>
    public void SubmitRequest(AdvancementRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        CheckRequest(request);

        LastRequestSubmittedAt = request.SubmittedDate;

        _requests.Add(request);
    }

    /// <summary>Applies a queued or approved request. The request must be loaded through this character's in-flight requests.</summary>
    public void ApplyAdvancement(Actor actor, AdvancementRequest request, string engineVersion, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(engineVersion);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(engineVersion.Length, FieldLengths.EngineVersion, nameof(engineVersion));

        // Membership, not ids: Id is Guid.Empty until persisted, so an id comparison passes vacuously in a unit test.
        if (!_requests.Contains(request))
        {
            throw new InvalidOperationException("The request does not belong to this character.");
        }

        if (Status is not CharacterStatus.Active)
        {
            throw new InvalidOperationException($"Cannot apply the request to a character not in the {CharacterStatus.Active} status. Current status: {Status}.");
        }

        if (request.EvaluatedRulesetVersionId != RulesetVersionId)
        {
            throw new InvalidOperationException("The request was evaluated against a different ruleset version than the character's current ruleset version.");
        }

        if (UnspentXp < request.QuotedCost)
        {
            throw new InvalidOperationException("The request cannot be applied because the character does not have enough unspent XP.");
        }

        var oldRating = 0;
        var newRating = 0;
        CharacterTrait? existingTrait = null;

        switch (request.TargetKind)
        {
            case TargetKind.Trait:
                existingTrait = _traits.FirstOrDefault(t => t.TraitKey == request.TargetKey);
                var targetRating = request.TargetRating ?? throw new InvalidOperationException("The request cannot be applied because the target rating is not specified for a trait advancement.");

                if (existingTrait is not null && existingTrait.Rating >= targetRating)
                {
                    throw new InvalidOperationException($"The request cannot be applied because the character's trait '{request.TargetKey}' already has a rating of {existingTrait.Rating}, which is greater than or equal to the requested rating of {targetRating}.");
                }

                oldRating = existingTrait?.Rating ?? 0;
                newRating = targetRating;
                break;

            case TargetKind.Ability:

                if (_abilities.Any(a => a.AbilityKey == request.TargetKey))
                {
                    throw new InvalidOperationException($"The request cannot be applied because the character already possesses the ability '{request.TargetKey}'.");
                }
                break;

            default:
                throw new InvalidOperationException($"The request cannot be applied because the target kind '{request.TargetKind}' is not supported.");
        }

        var newUnspentXp = UnspentXp - request.QuotedCost;
        var ledgerEntry = new XpLedgerEntry(-request.QuotedCost, TotalEarnedXp, newUnspentXp, $"Advancement: {request.TargetKey}", request.Id, timeProvider);

        RecordAudit(actor, AuditAction.AdvancementApplied, AdvancementChange(request.TargetKind, request.TargetKey, request.QuotedCost, oldRating, newRating), null, timeProvider, request.Id, engineVersion);

        UnspentXp = newUnspentXp;

        if (request.TargetKind == TargetKind.Trait)
        {
            var updated = new CharacterTrait(request.TargetKey, newRating);

            if (existingTrait is null)
            {
                _traits.Add(updated);
            }
            else
            {
                _traits[_traits.IndexOf(existingTrait)] = updated;
            }
        }
        else if (request.TargetKind == TargetKind.Ability)
        {
            _abilities.Add(new CharacterAbility(request.TargetKey, timeProvider.GetUtcNow()));
        }

        _xpLedgerEntries.Add(ledgerEntry);
    }

    private void CheckRequest(AdvancementRequest request)
    {
        var inFlight = _requests.Where(r => r.IsInFlight).ToList();

        if (Status != CharacterStatus.Active)
        {
            throw new InvalidOperationException($"Cannot create an advancement request for a character not in {CharacterStatus.Active} status. Current status: {Status}.");
        }

        if (inFlight.Count >= MaxRequestsInFlight)
        {
            throw new InvalidOperationException($"Not able to submit the request.  The character already has {MaxRequestsInFlight} requests pending.");
        }

        if (inFlight.Any(r => r.TargetKind == request.TargetKind && r.TargetKey == request.TargetKey))
        {
            throw new InvalidOperationException("This request is already pending and cannot be submitted again.");
        }

        if (UnspentXp - inFlight.Sum(r => r.QuotedCost) < request.QuotedCost)
        {
            throw new InvalidOperationException("This request cannot be completed as the character does not have enough XP.");
        }
    }
}
