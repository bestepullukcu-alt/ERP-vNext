---
module: PLATFORM
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E3
decision: conditional
commit:
---

# BL-649 — a load-sensitive wall-clock ceiling made a correct product go red; move the test off the wall clock

    item      : BL-649 · owner: Platform / Tasks (test owner) · CT role: measurement + prepared fix
    author    : CT (gmg-10)
    measured  : 2026-10-17 on feature/mvp6-logistics at HEAD
    status    : NOT applied — the test is under tests/ (a protected path), so this is a git apply-ready
                patch for the owner, like the other lane packages.

## The defect

`DecisionMakerSearchHttpMongoTests.A_hanging_name_source_costs_the_search_at_most_the_bound` (`:204`) sets a
hanging AuthService and asserts, on a **`Stopwatch`**, that the whole HTTP+Mongo request finished in
`< 4 seconds` (`:213`). The production bound is `BoundedDisplayNames.Timeout = 2 s`
(`services/Diten.Platform/src/Diten.Platform.Application/Common/BoundedDisplayNames.cs:16`), applied with a
real `Task.Delay` / `CancelAfter` — **not** an injectable clock.

So the ceiling gives only 2× headroom over a 2 s timer that fires late under CPU starvation. Measured in
the backlog: the full 8650-test suite run took `00:00:04.8444922` → **red**, while the same tree and
assemblies run **3/3 green alone** (load average still 10.44 on the re-run, so not a clean box either). The
product behaves identically; the wall clock does not.

## Why the ceiling is the wrong instrument here

The guarantee the comment wants — "a hang is **bounded**, not left to run" — is already proven
**deterministically**, with no wall clock, by `BoundedDisplayNamesTests`
(`A_checked_call_that_times_out_is_incomplete_never_complete_with_gaps`, `The_resolver_is_handed_a_token_that_the_bound_cancels`):
a resolver that hangs on `Task.Delay(5 min, ct)` comes back **incomplete** because the bound cancels its
token. The exact 2 s value and the cancellation semantics live there, where they are not load-sensitive.

What *this* HTTP+Mongo test uniquely owns is the **end-to-end surfacing**: a hanging name source becomes a
`503 DirectoryUnavailable` on the wire. That is asserted on `:214-215` and is not touched. The `< 4 s`
line adds nothing the unit test does not already prove, except a brittle wall-clock claim.

## The fix (the owner's option "take the measurement off the wall clock")

`patches/bl649-wallclock-to-deadline.patch` (+9/−3, `git apply --check` clean):

- removes the `Stopwatch` and the `< 4 s` assertion;
- keeps the `503` + `DirectoryUnavailable` assertions (the contract this test owns);
- bounds the request with `.WaitAsync(TimeSpan.FromSeconds(30))` — a deadline **far above** the 2 s bound
  (so realistic load, which will not turn 2 s into 30 s, never trips it) yet **far below** the 5-minute
  hang (so a genuinely lost bound still fails the test fast and clearly, instead of hanging the suite for
  five minutes).

`WaitAsync(TimeSpan)` is already used in this test assembly (e.g.
`TimesheetReadVersionRaceMongoTests:263`) and is net8.0-native, so the change introduces no new dependency.

### Verified (static — tests/ is protected, so no in-place build)

The modified file was built in the scratchpad by a single exact-block replacement and checked: the block
matched verbatim and uniquely (no silent drift), no `Stopwatch` / `var clock` / `clock.` token remains,
brace balance is preserved, and the `WaitAsync(30 s)` deadline is present. The patch `git apply --check`
passes against HEAD. The owner compile-confirms by running `Diten.Platform.Application.Tests` in CI; the
point of the change is that that run is no longer load-sensitive.

## Of the three backlog options

Raising the ceiling (option a) only moves the flake threshold; a serial CI bucket (option c) spends a
scheduling slot to keep a brittle instrument. This is option (b) — and it is the cheapest of the three
*and* the one that leaves the deterministic guarantee where it already is.

## Files

- `patches/bl649-wallclock-to-deadline.patch` — the change.
