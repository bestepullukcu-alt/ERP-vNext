# S&OP (MOD-0190) and Capacity (MOD-0192) — UI scope owner decisions (NOT APPROVED)

**STATUS: NOT APPROVED — prepared text only.** One decision per module, put one at a time. Copying a text into a record
does not approve it; only the owner's explicit answer does. Order: S&OP first (its UI is more complete on the published
operations), then Capacity.

## Decision 1 — MOD-0190 S&OP UI scope

**Question:** Which UI scope should MOD-0190 get in MVP6?

| Option | Effect | Estimate O/M/P (h) | Risk |
|---|---|---|---|
| **A — Bounded UI on the six published operations (recommended)** | Entry page (create plan, open plan by ID), plan workspace with snapshots and sign-offs, capture and record-sign-off actions. No plan list (F190-LIST); demand references typed as text (F190-DEMAND). No contract or backend change. | 52 / 86 / 148 | Medium: users need the plan ID or a link to reach a plan. |
| B — Contract first, then a standard list UI | A separate versioned SANDOP-CAPACITY task adds a plan list operation; backend + re-VER; then the UI of A plus a plan list DataTable. | UI 58 / 96 / 164 + contract/backend UNESTIMATED | High: reopens the frozen contract, both backends' pins and acceptance. |
| C — No UI in MVP6 | Module stays API-only (`shell: none`). | 0 | Sign-off remains an API-only activity. |

**Recommended: A.** Reason: sign-off is the part of S&OP that needs people, and all of it (plan, snapshots, sign-offs)
is available through the published operations today. The missing plan list is a real limitation but can be added later
by a versioned contract task without discarding A's screens; B would delay any UI behind a contract re-version.

> **Exact text (option A):** I approve the MOD-0190 S&OP Workflow & Sign-offs tenant UI **scope** in `docs/roadmap/plans/mvp6-ui-scope-190-192-01/UI-SCOPE-0190.md` (option A) for pack-revision preparation only: a UI revision inside the existing MOD-0190 pack (no new ID); tenant shell; GoldenReferenceSlim with `form_field_count: 5`; same-origin MVC adapter (`proxy-profile`); an entry page with "Create plan" and "Open plan by ID" and no plan list; a plan workspace with the snapshots and sign-offs lists and the "Capture snapshot" and "Record sign-off" actions, bound only to the published operations `createSandopPlan`, `getSandopPlan`, `captureSandopSnapshot`, `listSandopSnapshots`, `recordSandopSignOff`, `listSandopSignOffs` and the existing permission keys `supplychain.sandop-plans.read`, `.create`, `.snapshot.capture`, `.sign-off.record`; the OUT rows O-01…O-08 stay out; findings F190-LIST, F190-DEMAND, F190-PIN and F190-ENT stay open; seven tenant languages with RTL; self-registration as an SR-D4 overlay built with the UI. This authorizes preparing the exact pack revision patch and the Phase 1.5 table (`PH15-UI-190.md`) for sign-off. It does **not** authorize UI code, gateway, shared registration, permission, navigation or localization changes, contract or backend changes, pack promotion, rollout, commit or push.

Alternative texts: **B** — "…I approve option B: first a separate versioned SANDOP-CAPACITY specification task that adds a plan list operation (no other change), then a UI scope of option A plus a plan list; no contract, backend or UI work starts until that task is approved separately." **C** — "…I decide that MOD-0190 has no UI in MVP6; the pack stays `shell: none`."

## Decision 2 — MOD-0192 Capacity UI scope

**Question:** Which UI scope should MOD-0192 get in MVP6?

| Option | Effect | Estimate O/M/P (h) | Risk |
|---|---|---|---|
| **A — Bounded UI on the six published operations (recommended)** | Entry page (create, open plan by ID), plan workspace, create scenario, open scenario by ID, evaluate, evaluation panel with bottlenecks and manual refresh. No lists at any level (F192-LIST). | 58 / 98 / 168 | Medium to high: scenarios and evaluations are reachable only by ID or right after creating them. |
| B — Contract first, then list UI | Versioned change adding plan, scenario-by-plan and evaluation-by-scenario lists; backend + re-VER; then A + lists. | UI 66 / 110 / 188 + contract/backend UNESTIMATED | High: contract re-version and re-acceptance. |
| C — No UI in MVP6 | API-only. | 0 | Capacity scenarios stay API-only. |

**Recommended: A**, for the same reason as S&OP, and so that both planning modules share one UI pattern and one later
list extension. The owner should weigh that Capacity A is noticeably less browsable than S&OP A: if a browsable
Capacity UI is required for MVP6, B is the choice.

> **Exact text (option A):** I approve the MOD-0192 Capacity Planning tenant UI **scope** in `docs/roadmap/plans/mvp6-ui-scope-190-192-01/UI-SCOPE-0192.md` (option A) for pack-revision preparation only: a UI revision inside the existing MOD-0192 pack (no new ID); tenant shell; GoldenReferenceSlim with `form_field_count: 7`; same-origin MVC adapter (`proxy-profile`); an entry page with "Create plan" and "Open plan by ID" and no lists; a plan workspace with "Create scenario", "Open scenario by ID", "Evaluate" and an evaluation panel with the bottleneck table and a manual Refresh, bound only to the published operations `createCapacityPlan`, `getCapacityPlan`, `createCapacityScenario`, `getCapacityScenario`, `evaluateCapacityScenario`, `getCapacityEvaluation` and the existing permission keys `supplychain.capacity-plans.read`, `.create`, `.scenario.create`, `.evaluate`; decimal values handled as strings; the OUT rows O-01…O-08 stay out; findings F192-LIST, F192-DEMAND and F192-POLL stay open; seven tenant languages with RTL; self-registration as an SR-D4 overlay built with the UI. This authorizes preparing the exact pack revision patch and the Phase 1.5 table (`PH15-UI-192.md`) for sign-off. It does **not** authorize UI code, gateway, shared registration, permission, navigation or localization changes, contract or backend changes, pack promotion, rollout, commit or push.

Alternative texts: **B** and **C** as for S&OP, with "plan, scenario-by-plan and evaluation-by-scenario list operations" in B.

## After each approval

1. A chat lane records the decision.
2. A pack writer prepares the pack UI revision patch and the Phase 1.5 table for sign-off (chat lane).
3. After sign-off, the UI is written as a draft overlay in a chat lane (owner decision ~16:17). Build, tests and runtime
   VER follow on the local Mac.
