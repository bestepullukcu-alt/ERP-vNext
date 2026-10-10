# MVP6-SHIPMENT-REMAINING-ACCEPTANCE-DISPOSITION-01 — SOP §22

**Date:** 2026-09-24  
**Role:** evidence/scope reviewer and verification planner  
**Verdict:** **DISPOSITION COMPLETE — SINGLE SUCCESSOR VER PLAN READY**

## Result

The remaining Shipment UI acceptance is reduced to one bounded successor verification after the policy/error successor (A) and presentation successor (B) are both source-complete and combined into one immutable manifest. No test was rerun and no product or guard source was changed.

Three browser gaps now have executable fixtures and two-session ordering:

- UI183-A08 stale transition race: same shipment/root, two real-Auth browser sessions in one tenant/LE, first transition succeeds and the stale second transition must return `422 INVALID_SHIPMENT_TRANSITION`, produce no false success or write, and refresh to the authoritative state.
- UI183-A09 stale POD: two Dispatched/InTransit fixtures cover duplicate POD `409 POD_ALREADY_CAPTURED` and eligibility-stale POD `422 INVALID_SHIPMENT_TRANSITION`; both refresh the authoritative detail and create no second/partial write.
- UI183-A12 cross-LE: two independent browser profiles for LE-A and LE-B prove positive ownership in A and indistinguishable safe 404 list/detail/mutation behavior in B without existence leakage.

Exact fixture and process sequences are in `EXECUTION-PLAN.md`.

## UI183-A10 fault disposition

Existing approved process control can stop the isolated upstream before a request and prove a precommit `503` with zero writes. It cannot deterministically prove:

1. an exact UI-visible `500` while retaining the same intent; or
2. a commit-success/response-loss ambiguity where the browser receives no response and retries the exact key and payload.

Process kill timing is nondeterministic and cannot distinguish committed from non-committed state. Existing Carrier/Claims/Loads commit probes are module-specific and are not authority to add a Shipment production fault seam.

The narrow candidate is an **evidence-only loopback HTTP fault proxy**, created and run only in the disposable verification workspace. It may return 500/503 before forwarding, or forward one exact mutation and drop the client response only after recording upstream completion. It must hash every request body and idempotency key, bind to lane-only ports, archive its transcript, store no reusable credential and be removed during cleanup. No production source, `Program.cs`, controller, handler, repository or shared probe change is allowed.

`DECISION-NEEDS.md` contains the exact authorization required before this control is used. Without it, A10 stays OPEN; the verifier may still execute the other rows.

## DataTable 35-FAIL disposition

The raw 49 PASS / 35 FAIL log remains unchanged and the repository gate remains FAIL. `DATATABLE-35-DISPOSITION.tsv` classifies every FAIL:

| Class | Count | Meaning |
|---|---:|---|
| Bounded real defect | 0 | The generic gate output alone proves no bounded Shipment defect. |
| Explicitly out-of-scope full-CRUD/generic expectation | 23 | Edit, QuickView, bulk/delete, generic statuses or optional generic tools conflict with or exceed UI183-A14. |
| Evidence gap | 8 | The bounded behavior exists through server/shared localization or browser evidence, but the generic gate checks a specific `_IndexL10n` declaration. |
| Comment/owner-decision conflict | 4 | Literal partial syntax, generic form/detail parity and direct-Gateway assumptions conflict with the approved same-origin bounded design. |

This classification is not a waiver and does not produce a general gate PASS. The independent successor must run an approved scope-aware profile and retain the original generic FAIL log beside it. Edit/delete/bulk/QuickView features must remain absent.

The two real presentation defects reported outside those 35 failures remain genuine successor-source obligations: repeatable-line accessible labels (`PRES-183-01`) and shared responsive-modal localization (`PRES-183-02`). Durable PNG remains OPEN unless a supported save/export capability exists.

## Single successor handoff

`INDEPENDENT-VER-HANDOFF.md` is the only execution handoff. It must wait for exact A and B successor manifests, materialize their combined source once, and execute only the affected/open rows. It must not treat either predecessor candidate or this plan as runtime evidence.

The resulting verdict must preserve raw FAIL records and report PASS/FAIL/OPEN per acceptance row. No automatic waiver, generic DataTable PASS, browser/PNG assumption, full-module acceptance, E5/G5 or rollout follows from this planning disposition.
