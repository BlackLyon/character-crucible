# Modules

The Core API is a modular monolith. Each module owns a slice of the domain, keeps its own
Postgres schema, and talks to other modules through explicit contracts rather than by
reaching into their types.

Planned modules:

| Module | Owns |
|---|---|
| `Characters` | Characters, their trait ratings and acquired powers |
| `Rulesets` | Ruleset content authoring, and publishing immutable versioned snapshots |
| `Advancement` | Advancement requests, their lifecycle, approvals and the audit trail |

## The rule

**A module never references another module's internal types.** Cross-module communication
goes through a published contract or an event — not a direct type reference, not a shared
entity, not a join across schemas.

This is enforced by an architecture test rather than by discipline, because discipline
erodes and tests do not. That test is
[`ModuleBoundaryTests`](../../../tests/CharacterCrucible.CoreApi.Tests/Architecture/ModuleBoundaryTests.cs).

It derives the module roster from the namespaces actually present, so a module added later is
covered the day it gains its first type — a hardcoded list would leave it silently unchecked
while the suite stayed green. Read the class remarks before trusting it: it is blind to
transitive reaches through a shared type outside `Modules/`, to `const` inlining, to enum-to-int
casts, and to reflection.

## Layout inside a module

Two levels: **by layer first, then by kind inside `Domain/`.**

```
Modules/Rulesets/
  Domain/
    Entities/       Ruleset, RulesetVersion, the four definitions
    ValueObjects/   CostRule, DomainBand, PublicationRecord, …
    Enums/          one per file
  Persistence/      EF configurations          (added when needed)
  Endpoints/        minimal API endpoints      (added when needed)
```

Namespaces follow folders — `CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Entities`.

*Corrected 2026-10-05.* This section previously showed a flat `Domain/` and argued **against**
splitting it by kind, on the grounds that `TraitDefinition`, its `CostRule` and its
`TraitCategory` would land in three folders. They do, and the split is still the right call: a
flat `Domain/` reached about twenty files in one folder with the first module alone, and entity
files are the ones you navigate to by name. The earlier text described a layout the code never
used, which is a worse failure than either choice.

The *outer* level is the load-bearing one. Grouping by layer keeps "what is this module"
answerable from the folder names, and it still works when endpoints and EF configurations
arrive. Grouping by concept (`Versioning/`, `Definitions/`) was rejected because those
boundaries are judgement calls that get re-litigated.

**Enums live in `Domain/Enums/`**, one per file, namespaced to match. They are part of the
domain model, so they stay inside `Domain/` rather than becoming a sibling of it.

**Watch the namespace.** A `.cs` file created outside the IDE can end up with no namespace
declaration at all, which puts the type in the **global namespace** — visible to every
module, and invisible to the architecture test, since there is no namespace to assert
against. It still compiles, which is what makes it easy to miss.

## Why folders rather than projects

Namespace boundaries enforced by a test are as strong as assembly boundaries here, and
considerably cheaper: six projects instead of twelve, one build, one deploy. If a module
ever genuinely needs to ship separately, it can be extracted then — the boundary is
already real, it just isn't a `.csproj` yet.
