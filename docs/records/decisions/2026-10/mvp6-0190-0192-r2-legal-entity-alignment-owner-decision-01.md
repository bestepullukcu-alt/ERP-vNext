---
module: MOD-0190
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E4
decision: approved
commit:
modules: [MOD-0190, MOD-0192]
---

# MOD-0190 / MOD-0192 — legal-entity scope aligned to R-2 (owner decision)

    date      : 2026-10-18 · decided by the owner in session · recorded by CT (gmg-10)
    found by  : G6 (Capacity half), reading CapacityContextMiddleware while scoping its adapter

## Finding

R-2 (`66250b1a1`) removed `legal_entity_id` from the tokens AuthService issues and moved the five shipment-bundle
modules to a request-chosen legal entity (`X-Legal-Entity-Id`, validated by MDM). S&OP (`SandopContextMiddleware`) and
Capacity (`CapacityContextMiddleware`) were never aligned: both still **required** the claim, so with a real sign-in
every S&OP and Capacity call answered 403. The G4 live run and the G6 S&OP walk passed only because the lane minter
adds the claim; the shared HTTP test host minted it too, so no test could see it.

## Options put to the owner

1. **Align to R-2 now** (recommended) — 2. keep the claim and record the limitation — 3. pause the Capacity UI.

## Decision

**Option 1.** Both gates read tenant and actor from the token and the legal entity from `X-Legal-Entity-Id` only,
validated by `ILegalEntityScopeValidator` (MDM), fail-closed: not referenceable → the module's own not-found
(`UNKNOWN_SANDOP_PLAN` / `UNKNOWN_CAPACITY_PLAN`), unavailable → 503 `DEPENDENCY_UNAVAILABLE`. A tenant header that
disagrees with the token stays 403. Pack note added under §23.4 of both packs.

## Evidence

The shared S&OP/Capacity HTTP host now mints R-2-shaped tokens (no `legal_entity_id`), so every HTTP test in both
suites runs on what AuthService issues; new gate facts (missing header 400, MDM not-referenceable 404, MDM unavailable
503, foreign legal entity 404, tenant mismatch 403). Live re-walk in `mvp6-0190-g6-sop-ui-01` / the Capacity G6 record.
