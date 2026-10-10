# SOP §22 — MVP6-SHIPMENT-LINE-ACCESSIBILITY-CT-DISPOSITION-01

**Date:** 2026-09-25  
**Branch / HEAD measured:** `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`  
**Verdict:** **ACCEPTED_BOUNDED — PRES-183-01 only**

## Decision

Control Tower accepts the exact Shipment repeated-line accessibility successor identified by manifest SHA-256 `dcc6662696db5e689d2d4f6facbb5106ca53d06a957caf21c5fabc912a0c7c88`. Acceptance is limited to unique dynamic input IDs, label/control and accessible-name binding, add/remove/reindex behavior, keyboard and focus behavior, validation-summary focus, and preservation of line payload/model-field semantics.

This is not complete Shipment UI acceptance. It is not common-checkout uptake, rollout, E5/G5 or full-module acceptance.

## Evidence reconciliation

All 16 DEV artifact entries and all 3 independent VER artifact entries re-hashed successfully. The successor archive reconstructs 354/354 manifest entries. The patch SHA-256 `4c153b42890f10e289e421c0428ff1c1eff252f39892505ed0556cfa4efe5055` changes exactly the three paths in `CHANGED-FILES.tsv`.

The independent verifier used a separate disposable source, native .NET 8 and separate ports. It reproduced:

- focused accessibility contract test: 1/1 PASS;
- bounded Shipment UI/controller set: 17/17 PASS;
- Root R2 narrow regression: 7/7 PASS;
- browser behavior at 390px and 768px for one and multiple lines, keyboard add, middle removal and reindex, focus recovery, label activation, Tab order, validation alert focus, no overflow, `data-field` order and absence of `name` drift.

The focused test is contained in the 17-test bounded set and is not added again. No unchanged test was rerun by CT; CT inspected the immutable TRX identities and raw evidence archive.

## Exact accepted source delta

- `frontend/Diten.Web/Views/SupplyChain/Shipments/_Form.cshtml`
- `frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Shipments/create.js`
- `frontend/Diten.Web.Tests/Forms/ShipmentFormContractTests.cs`

No shared layout, DataTable, Auth, Gateway, backend, pack or contract source is part of this accepted delta.

## Effort disposition

The current ledger has no separately allocated PRES-183-01 row. `0183-4-DELIVERED` already records `40/64/104` O/M/P hours for the bounded Shipment frontend, so this implementation receives **0/0/0 new delivered credit**. `0183-6-REMAINING` retains `14/24/44` O/M/P because it is an unsplit reserve covering durable PNG and broader UI/browser gaps. The independent VER also receives no invented transfer. The portfolio and module effort totals remain unchanged.

## Open gates preserved

- Durable PNG remains **OPEN** without waiver.
- `PRES-183-02` shared responsive-modal localization remains OPEN.
- `PRES-183-03` scope-aware DataTable quality-gate profile remains OPEN.
- Other partial UI183 criteria retain their previous dispositions.
- Platform aggregate health observation and all common-target, rollout, full-module and E5/G5 gates are unchanged.

## Repository boundary

Only this CT record directory was written. Product, shared source, pack, contract and prior evidence were not edited. No commit, push or stash operation was performed.
