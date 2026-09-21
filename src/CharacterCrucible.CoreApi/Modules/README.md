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

## Why folders rather than projects

Namespace boundaries enforced by a test are as strong as assembly boundaries here, and
considerably cheaper: six projects instead of twelve, one build, one deploy. If a module
ever genuinely needs to ship separately, it can be extracted then — the boundary is
already real, it just isn't a `.csproj` yet.
