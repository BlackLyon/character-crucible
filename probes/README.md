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
| `Xmin_maps_to_the_system_column…` | `Character.RowVersion` is Postgres `xmin`, not a stored column | `Character` EF configuration |
| `A_jsonb_only_change_bumps_xmin…` | A trait-only change still trips the concurrency check, and the new version comes back without a reload | Every `Character` mutator that touches only jsonb |
| `A_stale_write_from_a_second_context_throws` | Two concurrent writers cannot both succeed | Any `Character` write |
| `The_workers_version_as_original_value…` | Apply sets the token's original value to the worker's `ExpectedRowVersion`, so the compare and the write are one statement | The advancement apply path |
| `An_app_managed_int_token…` | The alternative that was weighed; kept for comparison | None |
| `Replacing_an_entry_by_key…` | Trait changes replace an entry by key; EF rewrites the whole jsonb column | `Character` trait mutators |
| `A_filter_on_trait_key_and_rating…` | "Who qualifies for ability X" is possible as SQL over jsonb, without child tables | The jsonb persistence choice for traits |
| `One_rating_is_projected_by_key…` | A single rating can be read by key in SQL; a missing key is null | Any query reading one trait |
| `A_second_jsonb_collection_round_trips…` | Two jsonb collections coexist, and `DateTimeOffset` keeps its offset inside JSON | `CharacterAbility.AcquiredDate` — nullable in the real type (null in a draft); the stand-in's is not, so the null case is covered by `CharacterPersistenceTests` |
| `A_field_only_navigation_maps…` | The XP ledger is a navigation on `Character` with **no public getter**, so an unloaded collection can never be misread as no history; history is read by its own query | `Character` EF configuration; the XP history read |
| `Adding_to_the_unloaded_ledger…` | `Character` writes ledger entries through the private field: one INSERT, no history read | `Character`'s XP mutators |
| `A_losing_concurrent_spend_rolls_back…` | The totals on `Character` and the ledger row commit or roll back together, because they share one `SaveChanges` | Any XP write split across two saves |
| `A_filtered_include_on_the_private_navigation…` | `Character` loads only its **in-flight** requests through a private navigation, never the request history | The submission limit check |
| `Two_racing_submissions_cannot_both_pass…` | Every submission writes `Character.LastRequestSubmittedAt`, so `RowVersion` stops a racing second submission exceeding two in flight | `Character.SubmitRequest` |
| `Without_the_timestamp_write…` | **The counter-case, measured:** without that write only request rows change, the character row is never updated, and both racing submissions pass. The one line in `SubmitRequest` carries the guarantee | `LastRequestSubmittedAt = …` in `Character.SubmitRequest` |
| `In_flight_means_the_same_to_IsInFlight…` | One case per status, all seven: the filtered include, the partial unique index (raw SQL, around the entity) and `IsInFlight` agree on which statuses are in flight. The stand-in carries the real `RequestStatus` numbers, because the index filter is SQL over them. Broken on purpose to `Queued` only, it fails for `PendingApproval` and `Approved` | The include predicate and the index filter in the request EF configuration — both hand-written copies of `IsInFlight` |
| `The_index_allows_a_trait_and_an_ability…` | The index includes `target_kind`, so a trait and an ability with the same key can be in flight together, as the domain allows | The index's columns |

## Adding a probe

A new EF question goes here as a test, not into a scratch project, so its answer stays runnable.
All probe classes share one Postgres through the `ProbeCollection` fixture. Async EF calls take
the class's `Ct` (the test's cancellation token): xUnit v3's analyzer requires it, and CI builds
with warnings as errors.

The stand-ins carry only what a probe asks about. They change when the real entity takes a shape
no probe covers, such as a new jsonb collection or a different value-object shape, not when it
gains ordinary properties. Owned by Claude; see the work split in `CLAUDE.md`.
