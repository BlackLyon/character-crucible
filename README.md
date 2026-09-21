# Character Crucible

A system-agnostic character manager for tabletop RPGs.

Players create characters and submit advancement requests — spend XP to raise a trait or
acquire a power. Most are evaluated and applied automatically; the exceptional ones are
routed to a storyteller. The rules of any given game live in data, not code.

> **Status: in development.** Started September 2026. Not yet deployed.

## Why it is shaped this way

The interesting problem here is that **the rules are not the application**. Every table
houserules, every system errata's, and two groups playing the same game rarely play it
identically. So a *ruleset* — trait definitions, cost curves, prerequisites — is authored
as content, published as an immutable version, and evaluated by a service that owns no
data at all.

That produces a **policy decision point**: the component that decides whether an
advancement is legal is not the component that stores characters or carries the decision
out.

| Role | Component | Responsibility |
|---|---|---|
| Decision | **Rules service** | Given a character, a proposed advancement and a ruleset version — is it legal, what does it cost, and does it need a human? Stateless. No database. |
| Enforcement | **Core API** | Owns persistence and identity. Asks Rules, then applies or routes. |
| Administration | **Admin portal** | Authors ruleset content and publishes versioned snapshots. |

Every evaluation is stamped with the ruleset version that produced it, so a character
advanced under v3 does not silently break when v4 is published — and "why was this
allowed?" has an answer that can be replayed.

## Approval is the exception, not the rule

Waiting on a human to approve a single point of Strength would make this worse than a
spreadsheet. So the worker applies routine advancement automatically, and storytellers
see only two kinds of thing:

- **Escalations.** Some definitions are gated — a rare or powerful ability that should not
  be handed out casually. Whether something is gated is a property of the *ruleset*, so a
  storyteller changes it by publishing a new version, not by anyone shipping code.
- **Corrections.** When a sheet is wrong — a defect, a failed call, state left half-applied
  — a storyteller or admin edits it directly.

Correction is a **break-glass path**: it bypasses the rules engine, because it exists
precisely for when the rules engine produced bad state. So every manual edit is audited with
a required reason, and overrides are marked on the character's history as distinct from
advancement earned through the pipeline. An unaudited break-glass path is indistinguishable
from a storyteller quietly buffing a friend's character.

## Architecture

```
Angular SPA
     │  (Entra ID — Player / Storyteller / Admin)
     ▼
  Core API ──────── Service Bus ────────► Worker
 (characters,                               │
  traits,             both call Rules       │
  requests,           with app tokens       │
  audit)                                    │
     │                                      │
     └──────────► Rules service ◄───────────┘
                  (stateless, no DB)
     │
  Postgres
```

Three deployables. Service-to-service calls carry Entra ID app tokens; the same mechanism
provides the Player / Storyteller / Admin roles for users.

The worker **re-validates at apply time** rather than trusting the earlier decision — the
character may have moved on since, and for an escalated request sitting in a storyteller's
queue the gap is days, during which the ruleset itself may have been republished.

## Stack

.NET 10 · EF Core · PostgreSQL · Azure Service Bus · Angular · OpenTelemetry ·
Bicep · Azure Container Apps · xUnit + Testcontainers

## Running locally

Not yet. `docker compose up` will bring up Postgres once the scaffold lands.

## Scope

This is a proof of concept, deliberately narrow:

- **In:** characters, traits, powers, the advancement request and approval pipeline, an
  admin surface for authoring rulesets
- **Out, for now:** equipment, dice rolling, combat resolution, campaign management, and
  any form of rules scripting language

The sample ruleset is original content. It is not a reproduction of any published game.

## Licence

MIT — see [LICENSE](LICENSE).
