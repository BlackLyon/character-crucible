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

- **Definitions have no setters.** Values arrive through the constructor and never change.
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
| 0 | `(RulesetId, MajorVersion, MinorVersion)` is unique | `Ruleset.CreateDraft`, plus a composite unique index |
| 1 | At most one `Draft` per ruleset | `Ruleset.CreateDraft` |
| 2 | A published version has every stamp field set | **Structural** — `PublicationRecord` is all-or-nothing |
| 3 | A published version never changes | `EnsureDraft()`, and no public setters |
| 4 | `CurrentVersionId` points at a *published* version *of this ruleset* | `Ruleset.SetCurrentVersion` |

**Invariants 0, 1 and 4 belong on `Ruleset`** because it is the only type that can see its
versions and its current pointer together. That is also why `SetCurrentVersion` takes a
`RulesetVersion` and not a `Guid`: an id carries neither publication status nor ownership, so
a method taking one *cannot* check the invariant.

**Ownership is checked by collection membership, not by comparing ids.** `Id` is
`Guid.Empty` until EF assigns it on insert, so an id comparison is vacuously true before
persistence — exactly the state a unit test is in.

⚠️ **An aggregate invariant is only as strong as the loaded graph.** A `Ruleset` loaded
without `Include(r => r.Versions)` cannot see its own drafts, so invariant 1 silently passes.
Any handler that calls `CreateDraft` or `SetCurrentVersion` must load the versions. The
database indexes are the real backstop.

## `PublicationRecord` and the content hash

The five publish-stamp fields are one value object rather than five nullable columns, so
invariant 2 is unbreakable: either the record is absent (draft) or every field is present.

It carries a **SHA-256 `ContentHash`** over `PublishedContent`, computed inside `Publish()`
and never accepted from a caller. Replayability is the central argument for the Rules service
boundary — an audit entry naming a version must mean the same rules it meant then. If a
published snapshot were ever mutated, every audit entry referencing it would silently become
a false record. The hash turns that from undetectable into one assertion.

`GetHashCode()` is **not** usable for this: string hashing is randomised per process, so a
value stored today cannot be compared tomorrow.

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

Each entity also has a **private parameterless constructor** used only by EF. That is what
lets the public constructor validate its arguments without those guards running on every row
load, and it sidesteps constraint 1 for entities that take collections.

## Enum values that cross the snapshot boundary start at 1

`TraitCategory`, `AbilityDefinitionKind`, `CostOp`, `CostOperand`, `PrerequisiteEntryKind` and
`ArchetypeTargetKind` are serialised into `PublishedContent`, so their numeric values become
part of a frozen document and must survive reordering. Enums that stay in columns —
`RulesetKind`, `RulesetVersionKind`, `RulesetVersionStatus` — are left unnumbered.

## Enum-typed properties are never called `Kind`

`Ruleset.Origin`, `RulesetVersion.ReleaseType`, `AbilityDefinition.AbilityType` and
`TraitDefinition.Category` are four unrelated axes. Naming them all `Kind` compiles fine —
the type disambiguates — but structured logging destructures the *property* name, so all four
would land in logs as `Kind`. The enum *types* still carry older names; nothing outside the
code reads those.
