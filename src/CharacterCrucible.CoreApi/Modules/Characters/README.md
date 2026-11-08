# Characters

Characters, and how they change: creation and review, XP, and advancement requests from submission
to apply. Advancement lives here rather than in a module of its own. See
[`Modules/README.md`](../README.md) for why.

> The full design record lives in `notes/`, which is **not** in this repository. This file
> carries the reasoning a reader needs to work on the code without it.

**State:** the domain is written and mapped to Postgres, with mapping tests against a real
database. The guard tests of these methods come next. Builders and one worked example per method
family are in `tests/CharacterCrucible.CoreApi.Tests`, under `Builders/` and `Domain/Characters/`.

## The shape

```
Character                       the aggregate root. Every change goes through it
  ├── Traits     [jsonb]        CharacterTrait(key, rating). Attributes and skills
  ├── Abilities  [jsonb]        CharacterAbility(key, acquired date)
  ├── _requests                 AdvancementRequest. Loaded with in-flight requests only
  ├── _xpLedgerEntries          XpLedgerEntry. Write-only
  └── _auditEntries             AuditEntry. Write-only
```

`RulesetVersionId` and every trait, ability and archetype **key** point into `Rulesets`, and they
mean something only *within that version*. `Character` never sees a definition. Legality, cost,
caps and display names all come from the Rules service, or from the version itself.

`Character` is one class split across files by concern, so its methods can be read one area at
a time:

| File | What it holds |
|---|---|
| `Character.cs` | Properties, constructors, the one audit helper |
| `Character.Lifecycle.cs` | Review and play status: approve, send back, ban, remove from and return to play |
| `Character.Draft.cs` | Building and editing the sheet before approval, all or nothing |
| `Character.Xp.cs` | Granting XP |
| `Character.Advancement.cs` | Accepting a request within its limits, and applying one |

## Two collections can never be read

The XP ledger and the audit trail have **no public getter**. A loaded `Character` only ever
appends to them, and history is read by its own query. An unloaded collection is empty, and if
it could be read, "not loaded" would look exactly like "no history". With no getter, nothing can
decide anything from it.

The request collection is read, but it's **loaded with in-flight requests only**. That covers
the limits on submission (two in flight, one per item, affordable) and the ownership check at
apply. It never holds the request history.

## Rules every method follows

- **Check, then change.** Every guard runs before the first assignment, and the ledger and audit
  rows are built before any state moves. A method that throws leaves the character exactly as it
  was.
- **Time comes from an injected `TimeProvider`.** Nothing calls `DateTimeOffset.UtcNow`.
- **Every change to an `Active` character writes an audit row** with an `Actor` (id and role at
  the time). Approval writes the first row; drafts and review before it are not audited. The audit trail
  records only changes to the character: a request being approved or rejected changes nothing,
  so the request records that decision itself.
- **Status errors name the allowed status and the current one**, so a logged failure says what
  state the character was in.

## Ownership is checked by membership, not by id

`ApplyAdvancement` checks `_requests.Contains(request)`, not `request.CharacterId == Id`. An
entity's `Id` is `Guid.Empty` until EF inserts it, so in a unit test both sides are empty and an
id comparison passes every time. `Rulesets` learned this first.

The same reasoning sets the **id rule**: an id passed in from outside (owner, ruleset version,
submitter, reviewer, actor) must not be `Guid.Empty`. Ids EF assigns are exempt, including an
unsaved request's `Id` passed into a ledger or audit row.

## Advancement: who decides what

A request is built by `AdvancementRequest.ForTrait` or `ForAbility`, so an ability request can't
carry a rating and a trait request always has one. `Character.SubmitRequest` accepts it.

At apply time the worker re-validates. By design, the **orchestrator** (not built yet) decides
whether the request has `Failed`: the cost changed, the ruleset moved, or it's no longer
affordable. The player is never charged a price they didn't see. The orchestrator is also what
catches a duplicate delivery, before the character is touched.

`Character.ApplyAdvancement` repeats those checks as guards that throw, so it can't be misused on
its own. That includes refusing a request that isn't `Queued` or `Approved`, so a request still
waiting for a narrator can't be applied. Then it changes the rating or ability, the balance, the
ledger and the audit row together. `SubmitRequest` likewise accepts only a request fresh from a
factory, either `Queued` or `PendingApproval`.

**A skill rated 0 is a skill not held**, so every trait on a sheet is rated 1 or more, and buying a
new skill records a change from `0` to `n`.

## The audit diff

`AuditEntry.Change` is an `AuditChange`: a summary a person can read, plus a list of
`ChangeEntry(path, label, from, to)`. Paths use keys, never positions (`trait.swordsmanship`), and
every row has at least one entry.

Labels are generic (`"Trait"`). The display name is resolved when the history is read, from the
row's `RulesetVersionId`. Published versions never change, so that gives the name **as it was
then**, exactly, without storing a copy.

`EngineVersion` is set exactly when Rules evaluated the change, and `AdvancementApplied` must name
its request. Both are enforced in the constructor.

## Persistence

Four tables in the `characters` schema. The sheet's traits and abilities, and each audit row's
diff, are jsonb. Requests, the ledger and the audit trail are child tables of `character`.

- **Every FK is `Restrict`.** Requests, the ledger and the audit trail are history and outlive the
  character. Deletion is designed but not built, and that design decides what happens to them;
  until then the database refuses.
- **The FKs into `Rulesets` are declared in the shared `DbContext`**, not here, because this
  module's configuration may not name `RulesetVersion`. In C# they stay bare `Guid`s. They stop a
  character, request or audit row pointing at a version that doesn't exist, and stop a draft
  version being deleted while anything points at it. They don't stop a character being created on
  a draft; character creation will have to.
- **"In flight" is written three times**: `IsInFlight`, the include in
  `CharacterQueries.WithInFlightRequests`, and the filter on the one-request-per-item index. A
  mapping test checks all three agree for every status. Change one, change all three.
- **`AdvancementRequestId` on the ledger and audit rows is copied** from `request.Id`, not written
  by EF. A request is always saved before it's applied, so the copy is real, and the FK makes a
  wrong one fail at save. Only an in-memory test sees `Guid.Empty` there.
- **JSON keys keep their C# names** (`TraitKey`, `Summary`): the snake-case convention renames
  columns, not keys. The one exception is `Changes`, named explicitly so the
  private `ChangeData` member doesn't become the stored format.

## Not built yet

- **The orchestrator**: the application code that loads a character with its in-flight requests,
  calls Rules, and decides between `ApplyAdvancement` and `MarkFailed`. It comes with the
  endpoints and the worker.
- **Moving a character to a new ruleset version.** `RulesetVersionId` is designed to move, but no
  method changes it yet. Until one does, the apply-time ruleset check can't fail. It's parked with
  the question of what happens to a sheet the new version makes illegal.
- **Narrator edits** (story grants, corrections, refunds) are designed but parked as one feature.
  They raise refund policy questions that are best answered once the core works.
- **Character notes** and **notifications** are placed in later stages. Until then, the `reason`
  passed to `SendBack`, `BanDraft` and `UnbanDraft` is validated but not stored.
