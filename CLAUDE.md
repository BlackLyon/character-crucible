# Character Crucible — working notes for Claude

A system-agnostic character manager for tabletop RPGs. Players submit advancement requests;
most are applied automatically, the gated ones go to a storyteller. Game rules live in data.

## Read these before doing anything

Local only — `notes/` is gitignored and holds the real decision record:

| File | What it carries |
|---|---|
| `notes/architecture-decision.md` | Why the system is shaped this way, the alternatives rejected, the falsifiers |
| `notes/eight-week-plan.md` | Week-by-week scope and what is deliberately out |
| `notes/<latest date>.md` | Where work stopped and what starts next |
| `notes/parking-lot.md` | Deferred ideas. New ones go here rather than into the plan |

**Decisions in those files are settled.** If one comes up again, point at the recorded
reasoning rather than reopening it — each significant decision carries a written falsifier,
and that is the legitimate path to changing it. New information updates the doc; it does not
restart the debate.

## Invariants

- **The Rules service must never read a database.** It is a policy decision point: given a
  character, a proposed advancement and a ruleset version, it answers *legal · cost · needs a
  human*. If it needs to query, the boundary is wrong — collapse it into the Core API.
- **Modules never reference each other's internal types.** The Core API is a modular
  monolith; `Characters`, `Rulesets` and `Advancement` are folders that talk through
  contracts. See `src/CharacterCrucible.CoreApi/Modules/README.md`.
- **No users or permissions service.** Entra owns identity and coarse roles. Fine-grained
  access is `AuthorizationHandler<TRequirement, TResource>` in the Core API.
- **Rulesets are data, not code.** No DSL, no expression language.
- **The worker re-validates at apply time.** The earlier decision may be stale.

## Conventions

- **Never push to `main`.** It is protected: PRs only, squash merge, linear history.
- Commits and PR bodies say *why*, not just *what*.
- Scope is a proof of concept. Breadth over depth — a lesson that turns out interesting goes
  in the parking lot, not into this week's work.

## Commands

```bash
docker compose up -d --wait     # Postgres 17 on :5432
dotnet build                    # expect 0 warnings
dotnet test
dotnet run --project src/CharacterCrucible.CoreApi
```

## How the work is split

This is a learning project. Claude writes boilerplate — scaffolding, wiring, config, build
setup. The author writes the parts that carry the lessons: the domain model, the rules
evaluator, the outbox, token validation and authorization. Claude advises, reviews and
explains those rather than producing them, and says so when a request crosses the line.
