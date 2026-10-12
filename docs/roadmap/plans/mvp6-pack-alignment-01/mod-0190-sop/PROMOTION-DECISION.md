# MOD-0190 — owner promotion decision (NOT APPROVED)

**Status: DRAFT DECISION TEXT — NOT APPROVED.** This text grants nothing until the owner states it, or a named alternative, in their own words. An agent quoting it is not approval.

## Decision text to record

> I approve applying `docs/roadmap/plans/mvp6-pack-alignment-01/mod-0190-sop/proposed-pack.patch` to `execution/domains/supply-chain-execution/module-packs/MOD-0190-sop-workflow-signoffs.md` only if the preimage SHA-256 is `637690f32c1fa03e3039a4542bd53f7bb9b12f6b18d856a7d0e1f23d740c6877` and the result SHA-256 is `8403d8f46059c99ef34d3fe08b3cacdbbbefb321f30f921b620eceda9180ea40`.
>
> The result aligns the shared pack with the isolated pack I promoted on 2026-09-22 (`6a57769ced4396d2bc4228749a7e24b0daf36ce279930bb77c5dfdfe19fd0983`, status `ready-for-dev`) and records the Control Tower acceptance `MVP6-MOD0190-401-DISPOSITION-CT-02` in a new §22. It changes no business rule, acceptance criterion or owned-path list.
>
> This decision does not approve: `done` status; common-checkout source uptake or any `Program.cs`, shared DI, permission or gateway change; re-pinning MOD-0190 to SANDOP-CAPACITY 3.0.0; live DEMAND, Workflow or Event Bus integration; UI; E5/G5; rollout; commit, push or stash. One named writer applies the patch after rechecking both hashes; any mismatch stops the application.

## Options

| Option | Effect |
|---|---|
| **A — Approve as written (recommended)** | Shared pack = owner-promoted pack + §22; status `ready-for-dev`. Closes F-03 for MOD-0190 without inventing a status. |
| B — Approve with `status: draft` kept | Records §22 only; the pack keeps saying runtime is unauthorised, which contradicts the accepted WP. A new patch/hash would be needed. |
| C — Defer | F-03 stays open; new DEV on MOD-0190 must keep citing lane-local authority. |

## After approval

The applying writer records: before/after hashes, the patch hash (see `../SHA256SUMS`), and an update to `execution/registries/module-implementation-status.md` if the owner wants that tracker aligned in the same step (that is a separate path and is not covered by this text).
