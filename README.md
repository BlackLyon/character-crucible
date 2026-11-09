# Character Crucible

[![CI](https://github.com/BlackLyon/character-crucible/actions/workflows/ci.yml/badge.svg)](https://github.com/BlackLyon/character-crucible/actions/workflows/ci.yml)

A character creation and advancement manager for tabletop RPGs.

Players create characters and submit advancement requests — spend XP to raise a trait or
acquire an ability. Most are evaluated and applied automatically; the exceptional ones are
routed to a narrator (the game master), who can also correct a sheet directly when something has gone
wrong. The rules — what traits exist, what they cost, what gates them — are data rather
than code, so a table's houserules and errata change by publishing a new ruleset version
rather than by deploying.

It runs a small original rule system, built for this project. It tracks what a character
*is*, not what is happening to it in a session: no play state, no damage, no dice.

> **Status: in development.** Started September 2026. Not yet deployed.

## What exists today

The project is being built in stages, domain first. Nothing runs end to end yet.

- **`Rulesets`:** written, mapped to Postgres with EF Core, and covered by Testcontainers
  persistence tests. Those include the database-level invariants and publishing an immutable
  version, which takes two saves. See
  [its README](src/CharacterCrucible.CoreApi/Modules/Rulesets/README.md).
- **`Characters`:** the domain is written. That covers characters, their review and play status, XP
  with a ledger and an audit trail, and advancement requests from submission to apply. It's mapped
  to Postgres and covered by Testcontainers mapping tests. Test builders and one worked example per
  method family are in place; the full tests of its methods are next. See
  [its README](src/CharacterCrucible.CoreApi/Modules/Characters/README.md).
- **EF probes:** a test project that pins down how EF Core and Npgsql behave, the facts the
  persistence design depends on, before the real configuration relies on them. See
  [probes/](probes/README.md).
- **Architecture tests** enforce the module boundaries, and that the Rules service ships no data
  access library.
- **The Rules service and the Worker** are running skeletons: they start, and the Rules service
  answers `/health`. Neither does its real work yet.
- **CI** builds at zero warnings and runs the whole suite, probes included, on every pull request
  and every merge to `main`. Nothing deploys yet.
- **Not started:** endpoints, authentication, the message pipeline, the Rules evaluator, the
  front end, and deployment.

## A note on scale, before anything else

**This is deliberately more architecture than the problem needs.**

A character manager with a few dozen trait definitions runs perfectly well as one ASP.NET
Core app against one Postgres database. No message bus, no second service, no distributed
tracing. If this were a product, that is what it would be — and that is the right answer
for the product.

It is built this way to exercise a specific set of things end to end: service-to-service
authentication with Entra ID, asynchronous processing with an outbox and idempotent
handlers, distributed tracing across process boundaries, and infrastructure as code. None
of those can be demonstrated without the moving parts that produce them.

What the domain *did* earn is where the seams are. The Rules service is split where a
policy decision point genuinely sits — a pure evaluator with no data access — rather than
wherever a boundary could be cut. That part is a real design decision. The number of
deployables is a teaching decision, and worth being honest about.

## Why it is shaped this way

The interesting problem here is that **the rules are not the application**. Every table
houserules, every system publishes errata, and two groups playing the same game rarely play it
identically. So a *ruleset* — trait definitions, cost curves, prerequisites — is authored
as content, published as an immutable version, and evaluated by a service that owns no
data at all.

That produces a **policy decision point**: the component that decides whether an
advancement is legal is not the component that stores characters or carries the decision
out.

| Role | Component | Responsibility |
|---|---|---|
| Decision | **Rules service** | Given a character, a proposed advancement and a ruleset version — is it legal, what does it cost, and does it need a human? Stateless. No database. |
| Enforcement | **Core API** | Owns persistence and identity. Asks Rules at submission, then queues the request or routes it to a narrator. |
| Application | **Worker** | Takes queued and approved requests, re-validates each, and has the Core API apply it. |
| Administration | **Admin portal** *(planned)* | Authors ruleset content and publishes versioned snapshots. |

Every evaluation is stamped with the ruleset version that produced it, so a character
advanced under v3 does not silently break when v4 is published — and "why was this
allowed?" has an answer that can be replayed.

## Approval is the exception, not the rule

Waiting on a human to approve a single point of Strength would make this worse than a
spreadsheet. So the worker applies routine advancement automatically, and narrators
see only two kinds of thing:

- **Escalations.** Some definitions are gated — a rare or powerful ability that should not
  be handed out casually. Whether something is gated is a property of the *ruleset*, so a
  narrator changes it by publishing a new version, not by anyone shipping code.
- **Corrections.** When a sheet is wrong — a defect, a failed call, state left half-applied
  — a narrator or admin edits it directly.

Correction is a **break-glass path**: it is applied immediately rather than queued behind the
worker, because it exists for when the pipeline produced bad state. It still answers to the
rules, though: no narrator can set Strength to 47. That's a rule about valid state, not a
permission check. Every manual edit is audited with a required reason, and is recorded in the
character's history as a different kind of event from advancement earned through the pipeline.
An unaudited break-glass path is indistinguishable from a narrator quietly buffing a friend's
character. *Designed; not yet built.*

## Architecture

```
Angular SPA
     │  (Entra ID — Player / Narrator / Admin)
     ▼
  Core API ──────── Service Bus ────────► Worker
 (characters,                               │
  traits,   ◄──── apply (app token) ────────┤
  requests,                                 │
  audit)              both call Rules       │
     │                with app tokens       │
     ├──────────► Rules service ◄───────────┘
     │            (stateless, no DB)
     ▼
  Postgres
```

Three deployables. Service-to-service calls carry Entra ID app tokens; the same mechanism
provides the Player / Narrator / Admin roles for users. Only the Core API touches Postgres: the
Worker applies a request by calling the Core API, so every write goes through one place.

The worker **re-validates at apply time** rather than trusting the earlier decision — the
character may have moved on since, and for an escalated request sitting in a narrator's
queue the gap is days, during which the ruleset itself may have been republished.

## Stack

**In use:** .NET 10 · ASP.NET Core · EF Core · PostgreSQL · xUnit v3 · Testcontainers ·
GitHub Actions

**Planned:** Entra ID · Azure Service Bus · Angular · OpenTelemetry · Bicep · Azure Container Apps

## Running locally

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download), 10.0.401 or a later 10.0 SDK
(pinned in `global.json`), and Docker.

```bash
docker compose up -d --wait     # Postgres 17 on :5432
dotnet tool restore             # pins dotnet-ef to the EF Core version in use
dotnet build
dotnet dotnet-ef database update --project src/CharacterCrucible.CoreApi
dotnet test
```

**Migrations are applied explicitly, never on startup.** Auto-migrating races when more than one
instance starts, and applies schema changes nobody reviewed. If the schema is behind the code the
app starts fine and then fails on the first query with `relation ... does not exist` — so
`/health/ready` checks for pending migrations rather than mere connectivity.

The integration tests apply migrations themselves against a throwaway Postgres, so `dotnet test`
does not depend on the step above.

The tests are on xUnit v3, which `global.json` runs on Microsoft Testing Platform rather than
VSTest. Two things differ from VSTest habits:
- **A project that runs zero tests fails the run.** That catches a project that silently stopped
  discovering its tests.
- **Filters use xUnit's own options, within one project.** A filter across the whole solution
  leaves the other projects with zero tests, and they fail:

```bash
dotnet test --project tests/CharacterCrucible.CoreApi.Tests --filter-class '*FieldLength*'
```

The suite also runs inside Linux, which needs only Docker — no local SDK, and no dependence on
whatever SDK version happens to be installed:

```bash
docker compose run --rm tests
```

It is behind a `test` profile, so `docker compose up` does not start it. Build output and the
NuGet cache live in named volumes, so they never collide with the `obj/` and `bin/` of a host
build.

Then run any of the three services:

```bash
dotnet run --project src/CharacterCrucible.CoreApi
dotnet run --project src/CharacterCrucible.Rules
dotnet run --project src/CharacterCrucible.Worker
```

The Core API and the Rules service expose `/health`, and OpenAPI at `/openapi/v1.json` in
development. The Core API also has `/health/ready`, which checks the database is migrated. The
Worker has no HTTP surface.

## Layout

```
src/
  CharacterCrucible.CoreApi      Persistence, identity, enforcement. Modular monolith.
  CharacterCrucible.Rules        Stateless policy evaluation. No database, by design.
  CharacterCrucible.Worker       Applies advancement; re-validates before it does.
  CharacterCrucible.Contracts    Types shared across the service boundary. Empty until the first
                                 service-to-service call.
tests/
  CharacterCrucible.CoreApi.Tests   Architecture, domain guard and Testcontainers persistence tests
  CharacterCrucible.Rules.Tests     The no-database architecture test, until the evaluator exists
probes/
  CharacterCrucible.EfProbes        What EF Core and Npgsql actually do, against stand-in types
.github/workflows/
  ci.yml                            Build at zero warnings, then every test and probe
```

The Core API's modules are folders, not projects — see
[src/CharacterCrucible.CoreApi/Modules](src/CharacterCrucible.CoreApi/Modules/README.md)
for the boundary rule and why it's enforced by a test rather than by assemblies.

## Scope

This is a proof of concept, deliberately narrow:

- **In:** characters, traits, abilities, the advancement request and approval pipeline, an
  admin surface for authoring rulesets
- **Out, for now:** equipment, dice rolling, combat resolution, campaign management, and
  any form of rules scripting language

The sample ruleset is original content. It is not a reproduction of any published game.

## How this was built

A learning project, built with Claude Code as a deliberate way of working. I design the system
and write the parts that carry the lessons: the domain model, the Rules evaluator, the outbox,
token validation, authorization, and the cases that test the domain's behaviour. Claude writes
the supporting code, under my review: scaffolding, EF Core configuration and migrations, the EF
probes, test builders with one worked example per method family, CI, and documentation. It also
reviews my code. The split is written down in [`CLAUDE.md`](CLAUDE.md), which is also the
project's instructions to Claude.

Pull requests that change what runs get a self-review first: six fresh reviewer agents, each
covering one concern (correctness, scope, prior art, tests, consistency, operability). A hook in
[`.claude/`](.claude/settings.json) refuses `gh pr create` on anything but a docs-only change
unless the command carries an explicit review-done override, and logs each decision locally. It
can't prove a review ran; it makes skipping one a deliberate act rather than a lapse. The review
itself is a personal Claude Code skill, not part of this repository, so a clone gets the gate
without the reviewer.

## Licence

MIT — see [LICENSE](LICENSE).
