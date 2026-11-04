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

    public void ApplyAdvancement(Actor actor, AdvancementRequest request, string engineVersion, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(engineVersion);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(engineVersion.Length, FieldLengths.EngineVersion, nameof(engineVersion));

        if (request.CharacterId != Id)
        {
            throw new InvalidOperationException("The request does not belong to this character.");
        }

        if (Status is not CharacterStatus.Active)
        {
            throw new InvalidOperationException($"Cannot apply the request to a character not in the {CharacterStatus.Active} status");
        }

        if (!request.EvaluatedRulesetVersionId.Equals(RulesetVersionId))
        {
            throw new InvalidOperationException("The request was evaluated against a different ruleset version than the character's current ruleset version.");
        }

        if (UnspentXp < request.QuotedCost)
        {
            throw new InvalidOperationException("The request cannot be applied because the character does not have enough unspent XP.");
        }

        var oldRating = 0;
        var newRating = 0;
        var trait = _traits.FirstOrDefault(t => t.TraitKey == request.TargetKey);

        switch (request.TargetKind)
        {
            case TargetKind.Trait:
                var targetRating = request.TargetRating ?? throw new InvalidOperationException("The request cannot be applied because the target rating is not specified for a trait advancement.");

                if (trait is not null && trait.Rating >= targetRating)
                {
                    throw new InvalidOperationException($"The request cannot be applied because the character's trait '{request.TargetKey}' already has a rating of {trait.Rating}, which is greater than or equal to the requested rating of {targetRating}.");
                }

                oldRating = trait?.Rating ?? 0;
                newRating = targetRating;
                break;

            case TargetKind.Ability:
                var ability = _abilities.FirstOrDefault(a => a.AbilityKey == request.TargetKey);

                if (ability is not null)
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

            if (trait is null)
            {
                _traits.Add(updated);
            }
            else
            {
                _traits[_traits.IndexOf(trait)] = updated;
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
            throw new InvalidOperationException($"Cannot create an advancement request for a character not in {CharacterStatus.Active} status.");
        }

        if (inFlight.Count >= 2)
        {
            throw new InvalidOperationException("Not able to submit the request.  The character already has 2 requests pending.");
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
