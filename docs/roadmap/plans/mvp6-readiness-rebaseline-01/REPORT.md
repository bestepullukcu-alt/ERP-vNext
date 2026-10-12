# MVP6 readiness rebaseline

**Cut-off:** 2026-09-24  
**Verdict:** New measurement baseline established.  
**Development completion:** **66.7%** (`42.00 / 63.00`)  
**Release readiness:** **28.9%** (`13 / 45` binary gates)

These numbers are not a delta from the historical “≈48%” estimate. The earlier estimate used a different, undocumented denominator. This report is baseline **R0**; later progress must be measured by rerunning `METHOD.md` against the same 108 rows in `DELIVERABLES.tsv`.

## Module percentages

Development values use the seven equally weighted deliverables D1–D7. Release values use the five binary gates R1–R5.

| Module | Development points | Development completion | Release gates passed | Release readiness | Current controlling boundary |
|---|---:|---:|---:|---:|---|
| MOD-0183 Shipment Tracking & POD | 5.75 / 7 | **82.1%** | 2 / 5 | **40.0%** | Bounded Shipment/root accepted; isolated integration writer evidence; common/G5/rollout open. |
| MOD-0184 Carrier Management | 6.50 / 7 | **92.9%** | 2 / 5 | **40.0%** | Backend accepted; 21-path UI and Auth token rework implemented; independent Auth successor and real-Auth browser E2E open. |
| MOD-0185 Routing & Load Planning | 5.75 / 7 | **82.1%** | 2 / 5 | **40.0%** | Bounded Loads accepted; live root/grouping, common integration, UI and G5 open. |
| MOD-0186 Reverse Logistics | 5.75 / 7 | **82.1%** | 2 / 5 | **40.0%** | Bounded isolated Returns accepted; shared pack remains draft and common integration is open. |
| MOD-0187 Claims Management | 5.75 / 7 | **82.1%** | 2 / 5 | **40.0%** | Bounded isolated Claims accepted; shared pack remains draft and common integration is open. |
| MOD-0190 S&OP Workflow & Sign-offs | 5.75 / 7 | **82.1%** | 2 / 5 | **40.0%** | Bounded isolated work package accepted; 422 target lacks independent integrated CT; live seams open. |
| MOD-0192 Capacity Planning | 5.25 / 7 | **75.0%** | 1 / 5 | **20.0%** | Pre-v3 hosted scope has narrow CT close; published v3 duplicate-name behavior is not uptaken or independently verified. |
| MOD-0147 Supplier Performance & Risk | 0.75 / 7 | **10.7%** | 0 / 5 | **0.0%** | Spec split only; five owner concurrences and all runtime gates open. |
| MOD-0148 Supplier Portal | 0.75 / 7 | **10.7%** | 0 / 5 | **0.0%** | Spec split only; actor/security seam and all runtime/UI gates open. |
| **Portfolio** | **42.00 / 63** | **66.7%** | **13 / 45** | **28.9%** | First fixed baseline. |

## Development part percentages

| Part | Complete-equivalent points | Percentage | Main remaining work |
|---|---:|---:|---|
| D1 Pack and owner design | 7.50 / 9 | **83.3%** | Supplier five-owner concurrences; shared pack drift for isolated promotions must be resolved during authorized integration. |
| D2 Phase 1.5 technical mapping | 7.50 / 9 | **83.3%** | Supplier producer/security seams and technical close. |
| D3 Contract and consumer pin | 7.50 / 9 | **83.3%** | Supplier successor disposition; Capacity v3 runtime repin remains an implementation issue, recorded in D4/D5. |
| D4 Backend core | 6.75 / 9 | **75.0%** | Capacity v3 three-path uptake; Supplier modules not started. |
| D5 Composed HTTP/process | 6.75 / 9 | **75.0%** | Capacity post-v3 independent runtime; Supplier modules not started. |
| D6 Frontend/final UI disposition | 0.75 / 9 | **8.3%** | Carrier awaits acceptance; all other modules have unknown final UI scope and remain in the denominator. |
| D7 Shared integration target | 5.25 / 9 | **58.3%** | 422 target has writer evidence only; Capacity successor, independent integrated VER/CT and Supplier source are missing. |

## Release gate percentages

| Gate | Modules passed | Percentage | Boundary |
|---|---:|---:|---|
| R1 Independent bounded runtime VER | 6 / 9 | **66.7%** | MOD-0192 final v3 runtime and both Supplier modules remain open. |
| R2 CT bounded acceptance | 7 / 9 | **77.8%** | MOD-0147/0148 are spec-only. Bounded acceptance is not full-module acceptance. |
| R3 Integrated/common-target acceptance | 0 / 9 | **0.0%** | The 422-source target is writer-tested but lacks independent integrated CT acceptance. |
| R4 MVP6 G5/golden-flow acceptance | 0 / 9 | **0.0%** | No complete lifecycle and Supplier feedback acceptance exists. |
| R5 Release/rollout acceptance | 0 / 9 | **0.0%** | No module has the full operational release gate closed. |

## What the baseline says

The portfolio has substantial product implementation: seven modules have bounded backend work, and six of those have source-bound independent runtime verification plus CT acceptance. That drives development completion above two thirds.

Release readiness is lower because none of the nine modules has an independently accepted common integration target, G5/golden-flow acceptance, or operational release. The difference between **66.7% development** and **28.9% release readiness** is intentional and traceable; it is not a confidence discount.

The selected 422-source target provides integration development credit for MOD-0183 through MOD-0192, but it is still an isolated writer result. It does not establish that the mutable shared checkout contains the accepted sources, and it does not pass R3.

MOD-0186, MOD-0187, MOD-0190 and MOD-0192 illustrate the same distinction: their accepted/promoted pack identities are recorded in isolated evidence while the pack files in the shared checkout remain `draft`. Their accepted behavior is preserved, while common-target delivery remains open.

Carrier is the only module with an implemented MVP6 UI slice. The latest Lane A state at cut-off is a writer-complete two-file Auth token rework with final 22-path manifest `6221006b5e4bdccff9eb567b49a3e6c111ecb611b9e96d169ee3ff5fe57c1ad2`. The available independent VER report was created before that handoff and did not execute Phase 2. Carrier therefore receives development credit for applied work, but no new release gate.

MOD-0147 and MOD-0148 are measurable rather than omitted: their approved policy/spec preparation earns 0.75 development points each, while all runtime and release rows remain zero. Their unknown frontend scope is retained in D6.

## Critical path

The shortest path to a materially higher release score is:

1. authorize/apply/independently verify the MOD-0192 v3 duplicate-name successor on the exact selected target;
2. independently verify and CT-dispose the integrated 422 target;
3. complete the Auth successor independent VER and Carrier real-Auth browser E2E;
4. close the five Supplier owner concurrences, then produce the supplier-owned artifacts, pack/Phase 1.5 closes and bounded implementations;
5. decide or implement final MVP6 UI surfaces;
6. execute G5/golden-flow and operational rollout gates.

Detailed dependencies and exclusions are in `OPEN-GATES.md`. Exact row evidence is in `DELIVERABLES.tsv`; reusable source hashes are indexed in `SOURCES.tsv`.

## SOP §22 handoff

- **Outcome:** measurement baseline created; no runtime or existing artifact was modified.
- **Evidence level:** E1/E2 document and hash inspection. Historical tests were not rerun.
- **Repository effect:** only `docs/roadmap/plans/mvp6-readiness-rebaseline-01/` was added.
- **Acceptance boundary:** this report is a management baseline, not CT acceptance, pack promotion, DEV GO, E5/G5, or release authority.
- **Next measurement:** update only changed rows under the unchanged rubric and publish the new cut-off plus exact hash delta.
