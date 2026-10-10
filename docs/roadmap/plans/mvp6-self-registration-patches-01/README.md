# MVP6 self-registration patches — Supply Chain service (CT queue Q44)

🤖 Applying knowledge of @module-pack-author (with @backend-architect, @l10n-agent for review).

**PATCHES FOR LATER OWNER SIGN-OFF — NOT APPROVED, NOT APPLIED.** Repo `/Users/natig/Projects/ERP-vNext-recovery`,
`feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. Authority: D1–D5 = A (AUTHORITY.md). Start 2026-09-26T09:15:42+03:00.

## Result

| # | Patch | Target | Adds | apply --check |
|---|---|---|---|---|
| 1 | `01-DCP-009-self-registration-foundation.patch` | DCP-009 | §21 follow-up: identity (D3), foundation spec (interface, options, hosted service, `X-Internal-Api-Key`, csproj / `Program.cs` / appsettings as specification only), F-01…F-06, shared guards and reconcile tests, D4 ship rule, 9 open gaps | exit 0 ×2 |
| 2 | `02-MOD-0183-self-registration.patch` | MOD-0183 | §22: 3 pages, 4 actions, 3 nav keys, M-01…M-08 | exit 0 ×2 |
| 3 | `03-MOD-0184-self-registration.patch` | MOD-0184 | §31: 1 page, 2 actions, 3 nav keys | exit 0 ×2 |
| 4 | `04-MOD-0185-self-registration-after-loads-ui.patch` | MOD-0185 | §29, marked **after Loads UI approval**: 1 page, 1 action, 2 nav keys | exit 0 ×2 |
| 5 | `05-MOD-0186-self-registration.patch` | MOD-0186 | §30: 1 page, 8 actions, 2 nav keys; D5 test reflects `ReturnPermissions.ForTarget` | exit 0 ×2 |
| 6 | `06-MOD-0187-self-registration.patch` | MOD-0187 | §33 (on `762ab533…`): 1 page, 7 actions, 2 nav keys | exit 0 ×2 |

"×2" = a clean repo seeded with the staged base files, and the live repository (APPLY-CHECK.txt). All 11 nav keys are covered once
(`Nav.Domain.SUPPLYCHAINEXECUTION` is listed in DCP-009 and in the Shipment and Carrier sections as "ships with the first provider").

## Stacking and rebase

- **Returns (0186)** is stacked on the current pack text `07a8a015…` (last section §29). The Returns sign-off (Q39) is still pending; if it is
  applied first (alignment-03 → `f4396e8a…`, §30/§31; UI revision → `6c8fbe28…`, §32), **patch 5 must be rebased** (its section becomes §33) and re-checked.
- **Claims (0187)** is stacked on `762ab533…`, which already has the Q35 changes (§32 GoldenReferenceSlim UI scope).
- The six patches touch six different files, so they are independent of each other and can be applied in any order.

## Rules kept

- Permission keys: existing only (constants, plus `ReturnPermissions.ForTarget` values for Returns). No new key, MOD or DCP ID.
- Nav keys: names and 7 languages from NAV-L10N-KEYS.tsv; no resx value is written or approved.
- D4: each section says its provider ships with the module's UI and nav keys, never ahead.
- Recorded gaps are carried as open gaps in every section.

## Observation (in scope)

The live working tree already differs from HEAD for MOD-0184…0187 (uncommitted changes from earlier lanes). The patches bind to the
working-tree hashes in BASE-HASHES.tsv. If CT commits or reverts those files first, the base hashes change and the patches must be re-checked.

## Files

| File | Purpose |
|---|---|
| `AUTHORITY.md` | Decision record, hash and boundaries (verbatim) |
| `BASE-HASHES.tsv` | Before/after sha256 and section numbers per target |
| `patches/*.patch` | Six unified diffs (append-only) |
| `APPLY-CHECK.txt` | `git apply --check -v` output, container and live repo |
| `SIGN-OFF-DECISION.md` | Exact sign-off text — NOT APPROVED |
| `SHA256SUMS` | Checksums (paths relative to this directory) |

Nothing outside this folder was written. No pack, DCP, code, `.antigravity`, gateway, `SharedResource` or record was edited; no commit, push, stash or add.
