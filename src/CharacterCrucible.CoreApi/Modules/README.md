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
while the suite stayed green.

### What the architecture test cannot see

Green is not proof. Four kinds of breach are invisible to it, all measured:

- **Transitive reaches.** `HaveDependencyOn` is not transitive, so a type *outside* `Modules/`
  — the shared `DbContext`, for instance — can reference one module's internals while another
  module references that type. Neither module shows a dependency on the other. **This is the
  realistic leak path for a modular monolith with one DbContext.**

  Closing it needs a second rule, and the obvious phrasing is wrong: *"nothing outside
  `Modules/` may depend on `Modules.*`"* is **unsatisfiable**, because the `DbContext` has to
  reference every module's entities to map them. The rule has to be **no module may depend on
  another module, and only the persistence layer may depend on all of them** — which means the
  test needs an explicit allow-list of one namespace rather than a blanket prohibition.
- **`public const` values**, which the compiler inlines, leaving no IL reference at all.
- **Enum members cast to their underlying type** — `(int)SomeEnum.Value` compiles to a bare
  numeric load with the type reference dropped. The nine enums are the most borrowable things
  in the module.
- **Reflection and service-locator lookups by string.** Unavoidable with any IL-based tool.

`NetArchTest.Rules` 1.3.2 was published in 2021, so these will not be fixed upstream.
`TngTech.ArchUnitNET` is the maintained alternative if that ever matters.

**Also still to add:** a rule asserting `CharacterCrucible.Rules` has no dependency on
`Npgsql`, `Microsoft.EntityFrameworkCore` or `System.Data` — the firmest invariant in
`CLAUDE.md` is that the Rules service never reads a database, and nothing currently enforces it.

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
