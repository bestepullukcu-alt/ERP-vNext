# SOP §22 — MVP6-EFFORT-UPDATE-08 (Q51)

**Date:** 2026-09-26 · **Branch / HEAD:** `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`
**Lane:** AL-MVP6-EFFORT-08 (INS), chat lane on the linked Mac folder · **Verdict:** REPORTING PASS (agent) — CT decides

## Changed files
Only `docs/roadmap/plans/mvp6-effort-update-08/` (new).

## Evidence and calculation
Frozen baseline: update-07 ledger (106 rows) with four rows changed as two pure transfers (0183-R2-04 2/4/8; 0185-2 4.8/8/12.8), both tied to CT records. Result 1,464 delivered / 1,378 remaining / 2,842 total M (51.5 %); CT-accepted 1,140 (40.1 %). Forecast: + Returns/Claims UI deltas and self-registration rows → 1,469 / 1,485 / 2,954 M (49.7 %; accepted 38.6 %). Calculated by `RECALCULATE.py` and checked by `VERIFY.py` in the Mac-linked Linux VM under /tmp; results in `VALIDATION.txt`.

## Open gates
Shipment broader UI rows and PNG, Carrier PNG, Loads uptake/detail/lookup/multi-root, S&OP/Capacity UI scope, Supplier scope, evidence kit and overlay reconciliation remain open or unestimated (UNESTIMATED-SCOPE.tsv).

## Items for CT confirmation
ASSUMPTIONS A2 (R2-04 credit), A3 (0185-2 transfer), A4 (accepted baseline source), A7 (nav-key overlap).

## Out-of-scope changes
None. No product, pack, ledger, record or Git mutation.
