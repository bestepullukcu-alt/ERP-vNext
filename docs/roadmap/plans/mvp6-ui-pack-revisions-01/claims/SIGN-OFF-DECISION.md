# MOD-0187 Claims — owner sign-off for the UI pack revision

**STATUS: NOT APPROVED.** This text grants nothing until the owner states it, or a named alternative, in their own words. An agent quoting it is not approval.

## Decision text to record

> I approve applying two patches to `execution/domains/supply-chain-execution/module-packs/MOD-0187-claims-management.md`, **in this order and only in this order**:
>
> 1. `docs/roadmap/plans/mvp6-pack-alignment-02-claims/proposed-pack.patch` (SHA-256 `00dee2b1fd202f1adc25ca4d3c2aeae532e1bf8d944aca0c68a7fe0417ca5c59`), only if the pack's SHA-256 before it is `a342054cddcd88a9fae67f21b51ca90eadcdf355e6119c77c9578bae744b1c1f` and after it is `f0e4d3bd6af5c16a0d0904721019bfb33f10818a6f9baffd92d3f50740bb1baf`;
> 2. `docs/roadmap/plans/mvp6-ui-pack-revisions-01/claims/ui-revision.patch` (SHA-256 `5644479a77db172adf9d24f5a7031eed82c4eb82067d326403a37058270dae92`), only if the pack's SHA-256 before it is `f0e4d3bd…baf` and after it is `762ab5337af85b965216d41a212217db3681af3a31bbd1514544b58dbfc7786b`.
>
> The first patch is the Claims pack alignment (`status: ready-for-dev`, §31 accepted bounded scope binding). The second adds §32, the tenant UI scope I approved in `mvp6-returns-claims-ui-scope-owner-decision-01`: GoldenReferenceSlim with 6 fields, list + create panel + transition, no detail page, the `supplychain.shipments.read` prerequisite for create/transition, and the OUT rows CU-SCR-01…06. It sets `shell: tenant`, `golden_reference: slim` and `form_field_count: 6`, keeps `status: ready-for-dev`, and changes no business, contract or backend rule.
>
> One named writer applies both patches after rechecking every hash; any mismatch stops the application and nothing is partially applied. This decision also closes the alignment decision in `mvp6-pack-alignment-02-claims/PROMOTION-DECISION.md` as option A.
>
> This decision does **not** authorize: UI code until an integrated target (Q14/Q15) exists and a versioned UI dispatch is released; any gateway, permission, navigation, localization, icon-map, `Program.cs` or other shared edit except by the single CT-appointed integration owner; a contract change (including the approved-amount list gap); resolving the G-MODAL/G-DATETIME/verifier gaps without Phase 1.5; `done` status; a registry/status-tracker update; migration, rollout, E5/G5; commit, push or stash.

## Options

| Option | Effect |
|---|---|
| **A — Approve both patches in order (recommended)** | Shared pack becomes `762ab533…` (`ready-for-dev`, §31 + §32). UI remains HELD for code until the integrated target and dispatch exist |
| B — Approve the alignment only | Shared pack `f0e4d3bd…`; the UI revision waits (still valid while the preimage is unchanged) |
| C — Defer both | Nothing changes; the patches stay as proposals |

The Returns sign-off is not included: no Returns UI patch exists yet (see `../returns/`).
