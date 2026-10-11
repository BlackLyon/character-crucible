# EF probes

Characterisation tests of EF Core and Npgsql behaviour that the real code depends on. Each one
answers a question about **what EF does**, against stand-in types rather than the real entities.
The mapping tests in `tests/CharacterCrucible.CoreApi.Tests` are the other half: whether **our**
configuration does what we meant.

They run with every `dotnet test`, and take their EF and Npgsql packages from `CoreApi`, so a
package bump re-checks every answer below.

## If a probe goes red

EF's behaviour changed under a decision that relied on it. Read the row, check the real code it
protects, and revisit the decision. Do not edit the probe until green.

| Probe | The decision it backs | Real code at risk |
|---|---|---|
| `Xmin_maps_to_the_system_column…` | `Character.Version` is Postgres `xmin`, not a stored column | `Character` EF configuration |
| `A_jsonb_only_change_bumps_xmin…` | A trait-only change still trips the concurrency check, and the new version comes back without a reload | Every `Character` mutator that touches only jsonb |
| `A_stale_write_from_a_second_context_throws` | Two concurrent writers cannot both succeed | Any `Character` write |
| `The_workers_version_as_original_value…` | Apply sets the token's original value to the worker's `ExpectedCharacterVersion`, so the compare and the write are one statement | The advancement apply path |
| `An_app_managed_int_token…` | The alternative that was weighed; kept for comparison | None |
| `Replacing_an_entry_by_key…` | Trait changes replace an entry by key; EF rewrites the whole jsonb column | `Character` trait mutators |
| `A_filter_on_trait_key_and_rating…` | "Who qualifies for ability X" is possible as SQL over jsonb, without child tables | The jsonb persistence choice for traits |
| `One_rating_is_projected_by_key…` | A single rating can be read by key in SQL; a missing key is null | Any query reading one trait |
| `A_second_jsonb_collection_round_trips…` | Two jsonb collections coexist, and `DateTimeOffset` keeps its offset inside JSON | `HeldAbility.AcquiredDate` |

## Adding a probe

A new EF question goes here as a test, not into a scratch project, so its answer stays runnable.
All probe classes share one Postgres through the `ProbeCollection` fixture.

The stand-ins carry only what a probe asks about. They change when the real entity takes a shape
no probe covers, such as a new jsonb collection or a different value-object shape, not when it
gains ordinary properties. Owned by Claude; see the work split in `CLAUDE.md`.
