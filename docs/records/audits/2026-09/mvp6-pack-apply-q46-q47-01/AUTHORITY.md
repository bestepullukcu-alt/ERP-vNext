# Authority — MVP6-PACK-APPLY-Q46-Q47-01

Copied boundaries; the decision records are controlling. Nothing here widens them.

## A. Returns pack sign-off (Q46)

- Record: `docs/records/decisions/2026-09/mvp6-returns-pack-signoff-owner-decision-01.md`, SHA-256 `649269c3bc9bed88fa3c5eb019c18849367f47cdc6bbdc771c7cfbbf45a54611`. Decided 2026-09-26T09:22+0300 by the owner (CEO Natig Yusubov), option A.
- Source text: `docs/roadmap/plans/mvp6-pack-alignment-03-returns/SIGN-OFF-DECISION.md` `83f592a9…92e3`; package `SHA256SUMS` `64620aa1…506` (verified 5/5 by this lane).
- Approved exactly: to `execution/domains/supply-chain-execution/module-packs/MOD-0186-reverse-logistics.md`, in this order only: (1) `alignment.patch` `8af3287c…8c3b`, before `07a8a015…614a04b7`, after `f4396e8a…b33b1f`; (2) `ui-revision.patch` `62c7b164…9d63`, before `f4396e8a…`, after `6c8fbe28…a5a0`.
- Terms: one named writer; recheck every hash; any mismatch stops; nothing partially applied.
- Not authorized: UI code until an integrated target (Q14/Q15) and versioned UI dispatch; common-checkout source uptake or any `Program.cs`, shared DI, gateway, permission, navigation, localization or icon-map change except by the single integration owner; worker/publisher; verified receiving or Inventory posting; contract change; resolving G-MODAL/G-DATETIME/verifier gaps without Phase 1.5; `done` status; registry/status-tracker update; migration/backfill, rollout, E5/G5; commit, push or stash.
- Consequence: self-registration patch 5 must be rebased onto `6c8fbe28…` (section §33) and re-checked before its own sign-off.

## B. Self-registration patches sign-off (Q47)

- Record: `docs/records/decisions/2026-09/mvp6-self-registration-patches-signoff-owner-decision-01.md`, SHA-256 `7707329eec22aaf1d1f71b0b8d9680c555b08f906328e2dcf54dc38542023e58`. Decided 2026-09-26T09:23+0300 by the owner: sign patches 1, 2, 3, 4, 6; patch 5 later.
- Source text: `docs/roadmap/plans/mvp6-self-registration-patches-01/SIGN-OFF-DECISION.md` `bb848710…daf8`; package `SHA256SUMS` `2acd3241…2230` (verified 11/11 by this lane).
- Signed: 01 (DCP-009), 02 (MOD-0183), 03 (MOD-0184), 04 (MOD-0185, recorded as a section inactive until the Loads UI scope is approved and built), 06 (MOD-0187), each only on its exact before and after hashes (see `HASHES.tsv`).
- Terms: on any mismatch the patch is rebased and brought back, never applied by hand. Pack and DCP text only. No code, `Program.cs`, `.csproj`, appsettings, `SharedResource`, Platform or gateway change; no new permission key or ID; no commit, push or stash by an agent. Code stays with the single CT-appointed integration owner after Q14/Q15, each provider shipping with its module's UI and nav keys (D4). Recorded open gaps stay open.
- Not signed: patch 05 (MOD-0186). It is rebased onto the applied Returns pack (§33), re-checked and brought back for a separate sign-off. It is **not applied** by this lane.

## Lane dispatch limits

`GIT_OPTIONAL_LOCKS=0`; only `git apply --check` / `git apply` of the listed patches; no add, commit, push or stash; no other file edit; no code, `.antigravity`, gateway, contracts, registry/status tracker; no overwriting records. The rebase is written as a new file; the original package is not changed.
