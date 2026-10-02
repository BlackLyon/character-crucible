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
erodes and tests do not. That test arrives in week 2; until then the convention is on
trust, which is exactly the period when a modular monolith usually stops being modular.

## Layout inside a module

Each module is a self-contained slice with its own internal layering:

```
Modules/Rulesets/
  Domain/         entities, value objects, enums
  Persistence/    EF configurations          (added when needed)
  Endpoints/      minimal API endpoints      (added when needed)
```

Namespaces follow folders — `CharacterCrucible.CoreApi.Modules.Rulesets.Domain`.

Chosen over grouping by technical kind (`Entities/`, `ValueObjects/`, `Enums/`) because that
separates things that are read together: `TraitDefinition`, its `CostRule` and its
`TraitCategory` would land in three folders. And over grouping by concept
(`Versioning/`, `Definitions/`) because those boundaries are judgement calls that get
re-litigated.

This one still makes sense when endpoints and EF configurations arrive, and keeps "what is
this module" answerable from the folder names alone.

**Enums live in `Domain/Enums/`**, one per file, namespaced to match —
`...Modules.Rulesets.Domain.Enums`. They are part of the domain model, so they stay inside
`Domain/` rather than becoming a sibling of it; a top-level `Enums/` folder would be grouping
by technical kind, which is the layout this one was chosen over.

**Watch the namespace.** A `.cs` file created outside the IDE can end up with no namespace
declaration at all, which puts the type in the **global namespace** — visible to every
module, and invisible to the architecture test, since there is no namespace to assert
against. It still compiles, which is what makes it easy to miss.

## Why folders rather than projects

Namespace boundaries enforced by a test are as strong as assembly boundaries here, and
considerably cheaper: six projects instead of twelve, one build, one deploy. If a module
ever genuinely needs to ship separately, it can be extracted then — the boundary is
already real, it just isn't a `.csproj` yet.
