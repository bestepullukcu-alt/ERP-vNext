# MOD-0187 Claims — owner promotion decision (NOT APPROVED)

**Status: DRAFT DECISION TEXT — NOT APPROVED.** This text grants nothing until the owner states it, or a named alternative, in their own words. An agent quoting it is not approval.

## Decision text to record

> I approve applying `docs/roadmap/plans/mvp6-pack-alignment-02-claims/proposed-pack.patch` to `execution/domains/supply-chain-execution/module-packs/MOD-0187-claims-management.md` only if the preimage SHA-256 is `a342054cddcd88a9fae67f21b51ca90eadcdf355e6119c77c9578bae744b1c1f` and the result SHA-256 is `f0e4d3bd6af5c16a0d0904721019bfb33f10818a6f9baffd92d3f50740bb1baf`.
>
> The result carries into the shared pack the delta `afe36ff9123088593ec920191657176e5343270655b7b0a2be4106bc05820187` I approved on 2026-09-20 and the isolated activation (`d035d42059141f17edb90bb97183892a3c98c171a16770795ce2557f834ac9b0`, status `ready-for-dev`). It protects the published SHIPMENT-BUNDLE YAML and Claims/root annexes, and records in a new §31 the Control Tower acceptance `MVP6-MOD0187-CT-ACCEPT-01`. It changes no business rule, acceptance row or owned-path list.
>
> This decision supersedes `docs/roadmap/plans/mod-0187-final-pack-delta-01/` as the promotion path; that package stays as a record. It does not approve: `done` status; common-checkout source uptake or any `Program.cs`, shared DI, permission or gateway change; a worker/publisher; finance posting; UI; migration/backfill; E5/G5; rollout; commit, push or stash. One named writer applies the patch after rechecking both hashes; any mismatch stops the application.

## Options

| Option | Effect |
|---|---|
| **A — Approve as written (recommended)** | Shared pack = owner-promoted pack + §6 contract line + §31; status `ready-for-dev`. Closes F-03 for Claims. |
| B — Approve with in-body cleanup | Also rewrite the stale §5/§29/§30 sentences. Cleaner reading, but it edits owner-promoted text; needs a new patch/hash and review. |
| C — Defer | F-03 stays open for Claims; new Claims DEV keeps citing lane-local authority. |

## After approval

The applying writer records before/after hashes and the patch hash (see `SHA256SUMS`). Updating `execution/registries/module-implementation-status.md` is a separate path and is not covered by this text.
