# Single owner decision set — PROPOSED / UNAPPROVED

Accountable decision coordinator: **Central Control Tower Supplier Seam Owner**. The decisions below must be approved as one set, with MOD-0140 contract-owner and platform auth/security-owner concurrence where indicated.

| ID | Exact decision required | Recommended choice | Required concurrence | Acceptance consequence |
|---|---|---|---|---|
| SS-01 | Durable owner domain/service for MOD-0147 and MOD-0148 | Place their backend-only slices in `supply-chain-execution` / `Diten.SupplyChainService`, then align domain-config, DCP membership/exception and registry ownership through their proper governance writers | Enterprise/domain owner | Removes the current ownership blocker; does not promote either pack |
| SS-02 | Meaning of `SUPPLIER-BASE` | Treat existing FROZEN `SUPPLIER` v1 as the identity/status base; replace only stale “absent” assertions in future pack amendments | MOD-0140 + both consumers | Prevents a second Supplier master or redundant contract |
| SS-03 | Exact Supplier eligibility seam | Select `getSupplier` or `validateSuppliers` per command, define Tenant+LE eligibility, allowed base statuses, completeness/order, unknown/malformed/outage/correlation mapping | MOD-0140 | Makes 0147/0148 dependency checks executable and fail-closed |
| SS-04 | Supplier contract strictness repair | Prepare a separately reviewed additive successor/amendment for nullable/required/correlation/503 and LegalEntity eligibility needs; preserve current FROZEN bytes until approval | MOD-0140 + consumers | Current v1 remains historical authority; no silent reinterpretation |
| SS-05 | Authenticated supplier actor mapping | Name the authoritative binding source and exact actor claim/key; define tenant+LE membership, 1:1 vs 1:N selection, revocation, mapping outage and 401/403/404 precedence | Platform auth/security + MOD-0140 | Enables 0148 server-side identity and own-record authorization without client override |
| SS-06 | MOD-0148 replay scope | Include the exact approved tenant, LegalEntity, mapped Supplier, operation/target and idempotency-key tuple; decide how a mapped-supplier change/revocation affects replay | Platform security + MOD-0148 | Resolves the pack's index/replay inconsistency before spec promotion |
| SS-07 | Metric Registry seam and scoring oracle | Name a repo-resolvable contract owner/pointer; define metric identity/version/scope/UoM, scoring formula, weight rules, precision/rounding, thresholds and unavailable/stale outcomes | Metric Registry owner + MOD-0147 | Makes evaluation/scorecard acceptance deterministic |
| SS-08 | Risk Register role and SupplierRisk lifecycle | Decide whether the external seam is taxonomy/reference validation or an external register; freeze transitions, source reference validation and dependency failure behavior | Risk owner + MOD-0147 | Prevents a shadow Risk Register and makes risk commands testable |
| SS-09 | Fixture and producer boundary | Approve only a versioned test fixture after SS-03/05/07/08; keep live producer uptake and G5 separate | All seam owners | Fixture PASS cannot be reported as live integration |

## Proposed approval text

> I approve SS-01 through SS-09 in `MVP6-SUPPLIER-SEAM-READINESS-01` as the single Supplier seam decision set, subject to the named MOD-0140, platform auth/security, Metric Registry and Risk owner concurrences. I confirm that existing `SUPPLIER` v1 is the identity-only base and does not itself provide LegalEntity eligibility or actor mapping. MOD-0147 and MOD-0148 remain draft until exact amendments and consumer approvals are produced. This approval would not authorize contract publication, pack promotion, runtime, fixture-as-live substitution, gateway/auth changes or DEV GO.

Until a real owner decision is recorded, every row remains **UNAPPROVED**.
