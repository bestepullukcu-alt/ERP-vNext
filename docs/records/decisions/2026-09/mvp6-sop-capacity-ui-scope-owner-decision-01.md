---
decision_id: MVP6-SOP-CAPACITY-UI-SCOPE-OWNER-DECISION-01
status: approved — Decision 1 (MOD-0190 S&OP) option A; Decision 2 (MOD-0192 Capacity) option A; scope only
decided_at_local: 2026-09-26, ~17:20 +03:00 (approximate, as given by CT; the minute of each single answer was not given)
decided_by: Natig Yusubov, Chief Executive Officer (repository owner)
decision_source: owner answers in the MVP6 Control Tower conversation (in-app question tool), 2026-09-26, one decision per module
approvedBy: current-role-user-message-2026-09-26
bound_to: docs/roadmap/plans/mvp6-ui-scope-190-192-01/DECISION-TEXT.md sha256 8ae31266f5d5552effd75b093ba2dab873abe6a1ca3012a04730732d13d73e23; docs/roadmap/plans/mvp6-ui-scope-190-192-01/SHA256SUMS sha256 83c6b7e96331e246cacc9f75890b8282c32fb6bda066b2d504d66e22e0f1e985 (7/7 OK); docs/roadmap/plans/mvp6-ui-scope-190-192-01/UI-SCOPE-0190.md sha256 2447669e1050d3bcd04817fc8cc956afbcfdba42b252a51f27307b54d7facb82; docs/roadmap/plans/mvp6-ui-scope-190-192-01/UI-SCOPE-0192.md sha256 656ef7701798dd7cd7a8cd2367a19f8d16f1bc645cdecd33a21db66c9c9c03b3; docs/roadmap/plans/mvp6-ui-scope-190-192-01/ESTIMATE.tsv sha256 03669f33abb67a6be2ebdd1ae4e17fa26a7566d9190dc5d551f4969076a109e3
recorded_by: AL-MVP6-Q79-REC-01 (Q79, chat lane on the Linux VM bridge) at 2026-09-26T17:30:43+0300 on CT instruction; CT writes no files
---

# MOD-0190 S&OP and MOD-0192 Capacity — tenant UI scope, option A for both

**This is a scope decision only.** It does **not** approve the Phase 1.5 tables (`PH15-UI-190.md`, `PH15-UI-192.md`), any
pack text change, or UI code. Those go to the owner as queue item Q80 (pack UI revision patches + both Phase 1.5 tables).

## Decision 1 — MOD-0190 S&OP: option A

Owner answer: **A — bounded UI on the six published operations, no plan list.** Estimate O/M/P **52 / 86 / 148 h**
(`ESTIMATE.tsv`, MOD-0190 A TOTAL).

Exact decision text (source `docs/roadmap/plans/mvp6-ui-scope-190-192-01/DECISION-TEXT.md` sha256 `8ae31266f5d5552effd75b093ba2dab873abe6a1ca3012a04730732d13d73e23`, Decision 1, option A):

> **Exact text (option A):** I approve the MOD-0190 S&OP Workflow & Sign-offs tenant UI **scope** in `docs/roadmap/plans/mvp6-ui-scope-190-192-01/UI-SCOPE-0190.md` (option A) for pack-revision preparation only: a UI revision inside the existing MOD-0190 pack (no new ID); tenant shell; GoldenReferenceSlim with `form_field_count: 5`; same-origin MVC adapter (`proxy-profile`); an entry page with "Create plan" and "Open plan by ID" and no plan list; a plan workspace with the snapshots and sign-offs lists and the "Capture snapshot" and "Record sign-off" actions, bound only to the published operations `createSandopPlan`, `getSandopPlan`, `captureSandopSnapshot`, `listSandopSnapshots`, `recordSandopSignOff`, `listSandopSignOffs` and the existing permission keys `supplychain.sandop-plans.read`, `.create`, `.snapshot.capture`, `.sign-off.record`; the OUT rows O-01…O-08 stay out; findings F190-LIST, F190-DEMAND, F190-PIN and F190-ENT stay open; seven tenant languages with RTL; self-registration as an SR-D4 overlay built with the UI. This authorizes preparing the exact pack revision patch and the Phase 1.5 table (`PH15-UI-190.md`) for sign-off. It does **not** authorize UI code, gateway, shared registration, permission, navigation or localization changes, contract or backend changes, pack promotion, rollout, commit or push.

## Decision 2 — MOD-0192 Capacity: option A

Owner answer: **A — bounded UI, open by ID, manual Refresh.** Estimate O/M/P **58 / 98 / 168 h**
(`ESTIMATE.tsv`, MOD-0192 A TOTAL).

Exact decision text (source `docs/roadmap/plans/mvp6-ui-scope-190-192-01/DECISION-TEXT.md` sha256 `8ae31266f5d5552effd75b093ba2dab873abe6a1ca3012a04730732d13d73e23`, Decision 2, option A):

> **Exact text (option A):** I approve the MOD-0192 Capacity Planning tenant UI **scope** in `docs/roadmap/plans/mvp6-ui-scope-190-192-01/UI-SCOPE-0192.md` (option A) for pack-revision preparation only: a UI revision inside the existing MOD-0192 pack (no new ID); tenant shell; GoldenReferenceSlim with `form_field_count: 7`; same-origin MVC adapter (`proxy-profile`); an entry page with "Create plan" and "Open plan by ID" and no lists; a plan workspace with "Create scenario", "Open scenario by ID", "Evaluate" and an evaluation panel with the bottleneck table and a manual Refresh, bound only to the published operations `createCapacityPlan`, `getCapacityPlan`, `createCapacityScenario`, `getCapacityScenario`, `evaluateCapacityScenario`, `getCapacityEvaluation` and the existing permission keys `supplychain.capacity-plans.read`, `.create`, `.scenario.create`, `.evaluate`; decimal values handled as strings; the OUT rows O-01…O-08 stay out; findings F192-LIST, F192-DEMAND and F192-POLL stay open; seven tenant languages with RTL; self-registration as an SR-D4 overlay built with the UI. This authorizes preparing the exact pack revision patch and the Phase 1.5 table (`PH15-UI-192.md`) for sign-off. It does **not** authorize UI code, gateway, shared registration, permission, navigation or localization changes, contract or backend changes, pack promotion, rollout, commit or push.

## What follows

1. Pack UI revision patches for MOD-0190 and MOD-0192 are drafted (Q79 Step C, `docs/roadmap/plans/mvp6-ui-pack-patch-190-192-01/`, NOT applied).
2. Owner sign-off of those patches and of PH15-UI-190 / PH15-UI-192 (Q80, DECISION-REQUIRED).
3. Exact-hash apply + independent VER (Q81, HELD until Q80).

The quoted texts are copied byte-for-byte from the source file (including its `>` markers); the source still carries its
"NOT APPROVED — prepared text only" heading, which this record supersedes for option A of each decision. The source file
is not edited (K4).
