# Character Crucible — working notes for Claude

A system-agnostic character manager for tabletop RPGs. Players submit advancement requests;
most are applied automatically, the gated ones go to a narrator. Game rules live in data.

## Invariants

- **The Rules service must never read a database.** It is a policy decision point: given a
  character, a proposed advancement and a ruleset version, it answers *legal · cost · needs a
  human*. If it needs to query, the boundary is wrong — collapse it into the Core API.
- **Modules never reference each other's internal types.** The Core API is a modular
  monolith; `Characters` and `Rulesets` are folders that talk through contracts.
  Advancement lives inside `Characters` — folded in, not a third module. See
  `src/CharacterCrucible.CoreApi/Modules/README.md`.
- **No users or permissions service.** Entra owns identity and coarse roles. Fine-grained
  access is `AuthorizationHandler<TRequirement, TResource>` in the Core API.
- **Rulesets are data, not code.** No DSL, no expression language.
- **The worker re-validates at apply time.** The earlier decision may be stale.

## Conventions

- **Never push to `main`.** It is protected: PRs only, squash merge, linear history.
- Commits and PR bodies say *why*, not just *what*.
- **Comments are short and earn their place.** Two kinds only: a one- or two-line statement of
  what a class or method is *for*, and a note on anything not intuitively clear from the code.
  Nothing that restates the mechanism. **A twenty-line comment block does not get read**, so if
  an explanation needs that much room it belongs in the module README or the commit message.
  `GenerateDocumentationFile` is off, so `///` is IDE tooltips only — use it for the purpose
  line on a type or member, plain `//` for an inline note.
- Scope is a proof of concept. Breadth over depth — a lesson that turns out interesting goes
  in a list of deferred ideas, not into the current work.

## Commands

```bash
docker compose up -d --wait     # Postgres 17 on :5432
dotnet build                    # expect 0 warnings
dotnet test
docker compose run --rm tests   # same suite inside Linux, needing only Docker
dotnet run --project src/CharacterCrucible.CoreApi
```

## How the work is split

This is a learning project. Claude writes boilerplate — scaffolding, wiring, config, build
setup, EF Core configuration and migrations, CI, and documentation. The author writes the parts
that carry the lessons: the domain model, the rules evaluator, the outbox, token validation and
authorization. For the domain's tests, Claude writes the builders and one worked example per
method family, and the author writes the cases. Claude advises, reviews and
explains those rather than producing them, and says so when a request crosses the line.

**The EF probes in `probes/` are Claude's**: writing them, updating the
stand-ins when the real entities change something a probe covers, and fixing them when they go
red. The author gets the answer and what it means for the design, not the code. Stand-in
mutators exist to drive EF and are never a draft of the domain's methods.
