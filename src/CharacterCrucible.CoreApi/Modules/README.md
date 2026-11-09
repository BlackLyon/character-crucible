# Modules

The Core API is a modular monolith. Each module owns a slice of the domain, keeps its own
Postgres schema, and talks to other modules through explicit contracts rather than by
reaching into their types.

Modules:

| Module | Owns |
|---|---|
| `Rulesets` | Ruleset content authoring, and publishing immutable versioned snapshots |
| `Characters` | Characters, their traits and abilities, the XP ledger, advancement requests and their lifecycle, and the audit trail |

**Two modules, not three.** Advancement was planned as its own module and folded into
`Characters` before any of it was written. Its central operation — applying an advancement —
always changes a request *and* a character in one transaction, so a separate module would have
put a contract across the boundary that every apply had to cross, mirroring `Character`'s own
operations. The audit trail was never advancement-specific either: approving a character,
removing it from play and granting XP all need audit rows.

Split it back out if the request workflow grows concerns `Characters` does not share — a
separate deployable, or requests that apply to something other than characters. The audit
trail gets its own module the day a second module needs auditing.

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
  numeric load with the type reference dropped. Enums are the most borrowable things in a
  module, and both modules have several.
- **Reflection and service-locator lookups by string.** Unavoidable with any IL-based tool.

`NetArchTest.Rules` 1.3.2 was published in 2021, so these will not be fixed upstream.
`TngTech.ArchUnitNET` is the maintained alternative if that ever matters.

**The Rules service never reads a database** is enforced separately, by `NoDatabaseTests` in
`CharacterCrucible.Rules.Tests`. It checks Rules' whole package graph, transitive dependencies
included, and the framework assemblies its code uses, against a list of data access libraries.
A library missing from that list, or one loaded by reflection, wouldn't be caught.

## Layout inside a module

Two levels: **by layer first, then by kind inside `Domain/`.**

```
Modules/Rulesets/
  Domain/
    Entities/       Ruleset, RulesetVersion, the four definitions
    ValueObjects/   CostRule, DomainBand, PublicationRecord, …
    Enums/          one per file
    Constants/      FieldLengths — one source for the guards and the EF configuration
  Persistence/      EF configurations          (added when needed)
  Endpoints/        minimal API endpoints      (added when needed)
```

`Characters` follows the same layout and adds one kind, `Inputs/`: `CharacterDraft`, what a caller
supplies to build or edit a sheet. It's not a value object, because it's never stored.

Namespaces follow folders — `CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Entities`.

Splitting `Domain/` by kind does put `TraitDefinition`, its `CostRule` and its `TraitCategory` in
three folders. It is still the right call: a flat `Domain/` reached about twenty files with the
first module alone, and entity files are the ones you navigate to by name.

The *outer* level is the load-bearing one. Grouping by layer keeps "what is this module"
answerable from the folder names, and it still works when endpoints and EF configurations
arrive. Grouping by concept (`Versioning/`, `Definitions/`) was rejected because those
boundaries are judgement calls that get re-litigated.

**Enums live in `Domain/Enums/`**, one per file, namespaced to match. They are part of the
domain model, so they stay inside `Domain/` rather than becoming a sibling of it.

**Watch the namespace.** A `.cs` file created outside the IDE can end up with no namespace
declaration at all, which puts the type in the **global namespace** — visible to every
module, and invisible to the module-boundary rule, since there is no namespace to assert
against. It still compiles, which is what makes it easy to miss, so `ModuleBoundaryTests` has a
separate check that no type sits in the global namespace.

## EF configuration

**State only what convention doesn't.** EF already makes a non-nullable type's column
`NOT NULL`, whether a value type or a non-nullable reference, and already keys on a property
named `Id`. So there's no `IsRequired()` on a non-nullable type, no `HasKey` on `Id`, and no bare
`Property(x => x.Y)` that configures nothing. `SchemaTests` fails if a configuration change alters
the model without a migration, so a removal that did change something shows up there.

## Why folders rather than projects

Namespace boundaries enforced by a test are as strong as assembly boundaries here, and
considerably cheaper: one project per deployable rather than one per module, so the modules
build and ship with the Core API. If a module
ever genuinely needs to ship separately, it can be extracted then — the boundary is
already real, it just isn't a `.csproj` yet.
