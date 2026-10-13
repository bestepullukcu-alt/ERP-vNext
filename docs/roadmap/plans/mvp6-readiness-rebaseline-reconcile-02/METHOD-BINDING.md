# R1 method binding

**Successor:** `MVP6-READINESS-REBASELINE-RECONCILE-02`  
**Cut-off:** 2026-09-24, after BC successor and Auth token VER-02 records became available  
**Repository:** `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`

R1 reuses the R0 method byte-for-byte by reference:

- R0 `METHOD.md` SHA-256: `54437a64a8687877a98eb1d45c1d2199968cd6c81c2a620dc4be8d0550ab0bf6`.
- R0 `DELIVERABLES.tsv` SHA-256: `c6b6151800091d26113e46e8a98f36fba714889f7176ba9d0fb385a00006d0d7`.
- R0 `REPORT.md` SHA-256: `fbac4043a9527b1dc5b9e7ca09101fd009768ff37ae7a54483c69b5c641d0c1e`.
- R0 `SOURCES.tsv` SHA-256: `28dbf7145adc93eaf97876385d981cc9fceaaf95bc2e9c8575d2bc84c3184a08`.

The denominator remains **63 development points** and **45 binary release gates**. The corrected table still has exactly **108 rows**. No weight, module, deliverable, state value, or scoring rule was added or removed.

## R3 boundary clarification

R0 names R3 “Integrated/common-target acceptance,” while its evidence rules distinguish isolated targets from the mutable shared checkout. R1 resolves that ambiguity conservatively without changing the rubric:

1. **Selected integration target technical boundary:** exact source and shared composition applied to the registered isolated target, followed by source-bound independent verification.
2. **Mutable common-checkout boundary:** the accepted bytes are delivered to the mutable common target and receive the required integration/CT disposition.

R3 remains a conjunctive gate and receives a point only when both boundaries are satisfied, unless a later owner decision explicitly narrows the gate. B independent VER and the narrow BC successor acceptance close the selected-target boundary only. They do not establish mutable common-checkout integration, full-module acceptance, E5/G5, or rollout.

D7 is narrower: it asks whether source and shared composition are applied to the selected integration target. B independent VER therefore advances D7 for modules whose D7 scope is represented by B. Carrier D7 retains its UI/Auth/Gateway scope and does not advance from the backend-only B result.

## Evidence reconciliation rules

- `mvp6-three-scope-application-01` supersedes R0’s “422 writer evidence only” description: B is independently verified; C is independently verified for the exact three-file Capacity uptake.
- `mvp6-bc-successor-exec-02`, its independent VER and its narrow CT handoff bind the final `dec28b6…` 422-row successor source.
- The BC handoff is not propagated into another module’s R3 or full-module acceptance.
- Auth VER-02 supersedes the stale pre-writer verifier record: F-01 is closed; F-02 remains open with a reproduced NumericDate JSON-type defect. No Carrier score changes.
- All other R0 DONE rows retain their original bounded definitions. No bounded result is relabeled as a final module, common-checkout, G5, or rollout result.

This is evidence reconciliation, not effort, schedule, cost, or full-finish estimation.
