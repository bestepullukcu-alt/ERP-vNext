# MOD-0186 Returns — owner sign-off: pack alignment + UI revision (NOT APPROVED)

**Status: DRAFT DECISION TEXT — NOT APPROVED.** This text grants nothing until the owner states it, or a named alternative, in their own words. An agent quoting it is not approval.

## Decision text to record

> I approve applying two patches to `execution/domains/supply-chain-execution/module-packs/MOD-0186-reverse-logistics.md`, **in this order and only in this order**:
>
> 1. `docs/roadmap/plans/mvp6-pack-alignment-03-returns/alignment.patch` (SHA-256 `8af3287ce76a363bbe5bf9c7c2bea0b2250d7ca3bdc88f30603f339551c68c3b`), only if the pack's SHA-256 before it is `07a8a0159b5ab02fecc7b1aa6314106d81b1584150169f457cb9a395614a04b7` and after it is `f4396e8a03f663e34526e3bdaa4c85a4f3c96683ae73bae608f2981e87b33b1f`;
> 2. `docs/roadmap/plans/mvp6-pack-alignment-03-returns/ui-revision.patch` (SHA-256 `62c7b16417bb63a35589f790336f649d332bf0e97095aca05d0d910a156b9d63`), only if the pack's SHA-256 before it is `f4396e8a…b33b1f` and after it is `6c8fbe28f0b058cacf08aaf3941e82845b0842db3f8b8f295a753e746a65a5a0`.
>
> The first patch carries into the shared pack the owner-promoted isolated pack `1c80cca70ef774d18f5d30174566e68c9b6332b61dd1f564d54de3429886482f` (my conditional grant of 2026-09-20, the exact replacement `2a1843eb…` I approved on 2026-09-21, and the recorded Phase 1.5 closure), keeps `status: ready-for-dev`, protects the published SHIPMENT-BUNDLE YAML and Returns/root annexes, and records in a new §31 the Control Tower acceptance `MVP6-MOD0186-WP-ACCEPTANCE-01`. The second adds §32, the tenant UI scope I approved in `mvp6-returns-claims-ui-scope-owner-decision-01`: GoldenReferenceSlim with 6 fields, list + create panel + transition, no detail page, `supplychain.shipments.read` for create/transition, Received shown as a manual assertion, and the OUT rows RU-SCR-01…06. It sets `shell: tenant`, `golden_reference: slim` and `form_field_count: 6`. Neither patch changes a business, contract, backend or owned-path rule.
>
> This decision replaces `docs/roadmap/plans/mvp6-final-pack-delta-01/proposed-pack.patch` (`731622d2…`) as the Returns promotion path; that package stays as a record. One named writer applies both patches after rechecking every hash; any mismatch stops the application and nothing is partially applied.
>
> This decision does **not** authorize: UI code until an integrated target (Q14/Q15) exists and a versioned UI dispatch is released; common-checkout source uptake or any `Program.cs`, shared DI, gateway, permission, navigation, localization or icon-map change except by the single CT-appointed integration owner; a worker/publisher; verified warehouse receiving or Inventory posting; a contract change; resolving the G-MODAL/G-DATETIME/verifier gaps without Phase 1.5; `done` status; a registry/status-tracker update; migration/backfill, rollout, E5/G5; commit, push or stash.

## Options

| Option | Effect |
|---|---|
| **A — Approve both patches in order (recommended)** | Shared pack becomes `6c8fbe28…` (`ready-for-dev`, §31 + §32). Returns promotion (Q16) is closed; UI remains HELD for code until the integrated target and dispatch exist |
| B — Approve the alignment only | Shared pack `f4396e8a…` (`ready-for-dev`, §31); the UI revision waits (still valid while the preimage is unchanged) |
| C — Approve with in-body cleanup | Also rewrite the stale §§28–30 sentences; needs new patches and hashes |
| D — Defer | Nothing changes |

## After approval

The applying writer records before/after hashes and both patch hashes. CT may import or re-record the missing `mvp6-mod0186-phase15-close-01/` package (gap 1 in SOP-22-PACK-DELTA.md). The module-implementation-status update is a separate path.
