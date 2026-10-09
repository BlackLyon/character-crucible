# Rulesets

Authoring game rules, and publishing them as immutable versioned snapshots.

A **ruleset** is a game system. A **ruleset version** is one published state of it. Everything
a version contains — domains, traits, abilities, archetypes — is **data**, never code: no DSL,
no expression language. The Rules service evaluates against this data and never reads a
database.

> The full design record lives in `notes/`, which is **not** in this repository. This file
> carries the reasoning a reader needs to work on the code without it.

## The shape

```
Ruleset                     the system. Name, Publisher, lineage
  └── RulesetVersion        one published state. 1.0, 1.1, 2.0
        ├── DomainDefinition      Body / Mind / Spirit, with their score bands
        ├── TraitDefinition       rated things — attributes and skills
        ├── AbilityDefinition     binary things — perks and abilities
        └── ArchetypeDefinition   Warrior, Mage. Grants, cost and cap modifiers
```

`Ruleset.CurrentVersionId` is the version in force now. Older published versions stay
addressable forever, because characters are pinned to the version they were evaluated under.

## Every version is a complete snapshot, not a delta

`PublishedContent` holds **all** the rules of the system, not only what changed. A character
on v2 is evaluated against v2's document and nothing else. What makes that true is that a new
draft starts as a clone of the last published version.

This is the decision the rest of the module falls out of. Because a published version can
never change, the Rules service can cache its content by id **forever** — no polling, no
invalidation, no staleness budget. Those problems do not exist rather than being solved.

## Immutability is enforced by the type, not by a service layer

Trusting callers to leave published versions alone would make every future caller a risk.
Instead:

- **Definitions have no public setters.** Values arrive through the constructor; the setters are
  private, and the complex collections are private settable properties because EF requires it.
  Editing a published definition would silently rewrite rules characters were already
  evaluated against.
- **`RulesetVersion.AddDefinition` / `RemoveDefinition` call `EnsureDraft()`.** Mutating a
  published version throws.
- **Changes happen in a draft**, which is then published as a new version.

## Derived state cannot contradict stored state

Two properties are computed rather than stored, for the same reason in both cases — a stored
copy is a second source of truth that can drift:

| Property | Derived from |
|---|---|
| `Ruleset.Origin` | `DerivedFrom is null ? Base : Derived` |
| `RulesetVersion.Status` | `Publication is null ? Draft : Published` |

`Status` being derived is what makes "published" and "has a publication record" the same fact
rather than two facts that must be kept in step.

`Origin` is lineage only — **`Base` means "has no parent ruleset,"** nothing about whether the
content was commercially published. An Official/Unofficial authority axis is deliberately
absent: it is a different dimension, since a system can be derived *and* official.

## Invariants, and where each one lives

| # | Invariant | Enforced by |
|---|---|---|
| 0 | `(RulesetId, MajorVersion, MinorVersion)` is unique | `Ruleset.CreateDraft`. A composite unique index is **planned, not yet written** — no EF configuration exists |
| 1 | At most one `Draft` per ruleset | `Ruleset.CreateDraft` |
| 2 | A published version has every stamp field set | **Structural** — `PublicationRecord` is all-or-nothing |
| 3 | A published version never changes | `EnsureDraft()`, and no public setters |
| 4 | `CurrentVersion` is a *published* version *of this ruleset* | `Ruleset.SetCurrentVersion` |

**Invariants 0, 1 and 4 belong on `Ruleset`** because it is the only type that can see its
versions and its current pointer together. That is also why `SetCurrentVersion` takes a
`RulesetVersion` and not a `Guid`: an id carries neither publication status nor ownership, so
a method taking one *cannot* check the invariant.

**And `Ruleset.CreateDraft` is the only way to create a version at all.** `RulesetVersion`'s
creating constructor is `internal` and its parameterless one is `private`. Before that,
invariants 0 and 1 were enforced *in* `CreateDraft` while `CreateDraft` itself was optional —
`new RulesetVersion(...)` was public and went around both. `internal` is assembly-wide, so the
seed routine is the place to watch; another module doing it is caught by the architecture test.

**Ownership is checked by collection membership, not by comparing ids.** `Id` is
`Guid.Empty` until EF assigns it on insert, so an id comparison is vacuously true before
persistence — exactly the state a unit test is in.

**`CurrentVersion` is a navigation for the same reason.** It was a bare `Guid?` and
`SetCurrentVersion` copied `version.Id` by hand — which, before the row existed, wrote
`Guid.Empty` into a **non-null** column. `HasValue` then said a version was in force while
resolving to nothing, and with no navigation EF fixup could never repair it. Holding the object
means no id is written by hand, so the bad state is unreachable rather than guarded.

⚠️ **Publishing is therefore a two-phase save, and that is not optional.** With both rows new,
one `SaveChanges` fails: the ruleset's FK needs the version to exist and the version's FK needs
the ruleset to exist. Save the ruleset and its draft, then publish and promote. The cycle comes
from the FK *constraint*, so keeping a bare `Guid?` and merely configuring it as an FK hits the
same wall — the old design allowed a single-pass insert only because it had no constraint, which
is the same reason nothing caught `Guid.Empty`.

⚠️ **An aggregate invariant is only as strong as the loaded graph.** A `Ruleset` loaded
without `Include(r => r.Versions)` cannot see its own drafts, so invariant 1 silently passes.
Any handler that calls `CreateDraft` or `SetCurrentVersion` should still load the versions, for
a readable error rather than a constraint violation.

**The database is the real backstop, and it exists.** `ux_ruleset_version_one_draft_per_ruleset`
is a partial unique index — `UNIQUE (ruleset_id) WHERE publication_schema_version IS NULL` — so
a second draft is rejected even on the path that goes around `CreateDraft` entirely. Tested both
ways: it rejects a second draft inserted by raw SQL, and it permits a new draft alongside a
published version. That second test is the one that matters — without it, deleting the `WHERE`
clause would leave every other test green while making multi-version rulesets impossible.

**Invariant 4 has no such backstop.** The FK proves `current_version_id` names *some* version;
nothing below `SetCurrentVersion` proves it is published or belongs to this ruleset. One `UPDATE`
can point a ruleset at another ruleset's draft. The *"of this ruleset"* half is reachable with a
composite FK; the *"is published"* half is not, while `Status` is derived.

## `PublicationRecord` and the content hash

The publish stamp is one value object of **seven** fields rather than seven nullable columns, so
invariant 2 holds: either the record is absent (draft) or every field is present.

**But that guarantee is the `CHECK` constraint, not the type.** `PublicationRecord`'s constructor
cannot build a partial one — and EF still maps it to seven *independent nullable* columns,
because "absent" can only be expressed as all of them being NULL. So the C# guarantee stops at
the boundary. `ck_ruleset_version_publication_all_or_nothing` re-imposes it below:
`num_nulls(…seven…) IN (0, 7)`. Without it, EF and the one-draft index disagreed about what a
draft is — EF decides from `content_hash`, the index filters on `schema_version`.

It carries a **SHA-256 `ContentHash`** over `PublishedContent`, computed inside `Publish()`
and never accepted from a caller. Replayability is the central argument for the Rules service
boundary — an audit entry naming a version must mean the same rules it meant then. If a
published snapshot were ever mutated, every audit entry referencing it would silently become
a false record. The hash turns that from undetectable into one assertion.

`GetHashCode()` is **not** usable for this: string hashing is randomised per process, so a
value stored today cannot be compared tomorrow.

⚠️ **Which is why `PublishedContent` is `text` and not `jsonb`.** `jsonb` stores a *parsed
document* and re-serialises on read, so `{"traits":[]}` comes back as `{"traits": []}` — one
space, a different hash, and the integrity check could never pass again. It also reorders keys
and **discards duplicates**, which is data loss in a column the design calls immutable. Mapped
as `jsonb` the hash was permanently wrong, and the test asserting only
`ContentHash.Length == 64` could not catch it. The test now recomputes SHA-256 from what the
database returned, and fails if the column type ever changes back. Validating that the content
is JSON belongs in `Publish`, not in the column type.

## Entities and value objects

**Entities** have identity and an `Id`. **Value objects** are defined entirely by their
values — one `CostRule` of *multiply by 3* is interchangeable with any other — so they have
no `Id` and map as EF complex types, inline as columns or into `jsonb`.

Value objects carry no mutators. A `DomainBand` cannot see the cap, the archetype grants or
the cost, so a method on it could only set a field while implying a guarantee it has no way
to make. Replace them rather than mutating them.

## Four EF Core 10 constraints this code is shaped by

Each was established by running code against Postgres, not assumed:

1. **A collection cannot be a constructor parameter.** EF binds constructor parameters to
   *scalar* properties only, so `record PrerequisiteGroup(int RequiredCount, IList<Entry>
   Entries)` fails at model build. The collection must be a settable property.
2. **Complex types have no inheritance.** A record per prerequisite kind maps as "a type with
   no properties" and is rejected — hence `PrerequisiteEntry` is one flat record with a `Kind`
   discriminator, with static factories (`TraitAtMinimum`, `AbilityHeld`, `ArchetypeIs`) that
   supply the kind and null the fields that do not apply.
3. **Complex collections must be `IList<T>`,** but the property may be `private` with a
   read-only public view — so encapsulation survives. `readonly record struct` is rejected
   outright; value objects in collections must be reference types.
4. **A record wrapping a collection has value equality in name only** — `IList<T>` compares
   by reference. Do not assert that two structurally identical `PrerequisiteGroup` values are
   equal.

Each entity also has a **private parameterless constructor** used only by EF. EF prefers it over
the public one, so the validating constructor's guards do not run on every row load — rows were
validated on the way in, and re-validating would make a legacy blank value unloadable. It also
sidesteps constraint 1 for the three entities that take collection parameters.

Two further things verified rather than assumed, both of which reduce the EF configuration still
to be written:

- **Navigation collections need no `HasField`.** `private readonly List<T> _versions` behind a
  read-only `Versions` property is discovered by convention — the navigation from the property,
  the backing field from its name, with `PreferField` access. No configuration at all.
- **`IList<T>` and `List<T>` behave identically** for that discovery and materialisation. The
  backing fields should still agree with each other for consistency, but nothing breaks.

## Persisted enum values start at 1

*Corrected 2026-10-05.* This rule previously read "enum values that cross the snapshot boundary",
which was wrong in a way that misclassified a real case. **The criterion is whether the value is
persisted at all**, not whether it reaches `PublishedContent` — a stored ordinal has to survive a
member being reordered for exactly the same reason a serialised one does.

So `TraitCategory`, `AbilityDefinitionKind`, `CostOp`, `CostOperand`, `PrerequisiteEntryKind` and
`ArchetypeTargetKind` are numbered because they end up inside `PublishedContent`, **and
`RulesetVersionKind` is numbered because `ReleaseType` is a stored column.** The old wording left
it unnumbered, which also made `Errata` the zero value — so any path failing to set a release
type would have read back as a legitimate "Errata" rather than an obvious sentinel.

Starting at 1 is deliberate for that reason: `default(T)` is then not a valid member.

Only `RulesetKind` and `RulesetVersionStatus` stay unnumbered, because both are **derived and
never stored** — `Origin` and `Status` are computed properties with no column behind them.

## Enum-typed properties are never called `Kind`

`Ruleset.Origin`, `RulesetVersion.ReleaseType`, `AbilityDefinition.AbilityType` and
`TraitDefinition.Category` are four unrelated axes. Naming them all `Kind` compiles fine —
the type disambiguates — but structured logging destructures the *property* name, so all four
would land in logs as `Kind`. The enum *types* still carry older names; nothing outside the
code reads those.
