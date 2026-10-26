---
module: MOD-0192
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E4
decision: pass
commit:
modules: [MOD-0192]
---

# Wave 6.1 G6 — MOD-0192 Capacity tenant UI built; walked live through Web → Gateway → SupplyChain

    run       : 2026-10-18 by CT (gmg-10)
    authority : UI surface owner-approved (option A, `mvp6-sop-capacity-ui-scope-owner-decision-01`, bound to
                `UI-SCOPE-0192.md`); build scope `docs/roadmap/plans/mvp6-0190-0192-ui-wave-01/G6-UI-BUILD-SCOPE.md`;
                §24 from `mvp6-ui-pack-patch-190-192-01`; owner "start G6 UI build"; legal-entity scope per
                `mvp6-0190-0192-r2-legal-entity-alignment-owner-decision-01` (found while scoping this half).
    verdict   : **PASS** (composed, seeded non-production host). G6 complete for both modules.

## What was built

| Part | Files |
|---|---|
| Adapter (proxy-profile) | `Controllers/SupplyChainCapacityPlansController.cs` — the six bound operations only (create/get plan, create/get scenario, evaluate **202**, get evaluation); same header policy and re-enveloping as S&OP; the downstream URL carries the path only (the service refuses scope in the query). |
| Permissions | `Models/SupplyChain/CapacityPlans/CapacityPlanPermissions.cs`. |
| Entry page | `Views/SupplyChain/CapacityPlans/Index.cshtml` (Create plan + Open plan by ID; no list — F192-LIST) + `_CreateEditOffcanvas.cshtml` (7 fields, GoldenReferenceSlim). |
| Workspace | `Details.cshtml` (plan header incl. provenance; Open scenario by ID; scenario panel with constraint/adjustment tables and Evaluate) + `_EvaluationPanel.cshtml` (status, times, bottlenecks, manual Refresh) + `_CreateScenarioOffcanvas.cshtml` (two repeaters) + `_EvaluateOffcanvas.cshtml` (mode + resource repeater). |
| Scripts | `index.js`, `details.js` — camp `ERROR_KEY` + `t()`, one key per opened form, three versioned loads (plan/scenario/evaluation), scope on every call + fail closed, **decimals rendered as the returned strings** (never parsed), **no polling**; the opened scenario/evaluation ride the URL so reload and shared links show the same view (CP-22). |
| l10n | seven echo keys renamed (`PlanName`, `CreatedAt`, `PlanProvenance`, `EvaluateScenario`, `RefreshEvaluation`, `AdjustmentsHeader`, `BottlenecksHeader`); fr `PlanProvenance` no longer English; bridge/loader + `BackToList`, `FieldRequired`. Nav `Nav.Module.CAPACITYPLANNING`, `Nav.Page.CAPACITYPLANS` × 7 (normalized form, as for S&OP). |
| §24 manifest | `CapacityPlanningManifestProvider` (patch tables exactly), registered with S&OP's **inside the demand-seed block**; the `capacity-plans` "no provider" guard entry removed. |

## Live walk (same guarded lane; token **without** `legal_entity_id`, as AuthService issues since R-2)

| Step | Result |
|---|---|
| Create plan (7 fields, `dp-g6`/`1`/`sha256:g6-lane`) | **201** → workspace; provenance `DEMAND · v1` |
| Create scenario (constraint `line-4-hours`/`SUPPLY-CONSTRAINTS`/`8`, adjustment `line-4`/`2027-W03`/`80.000`/`HOUR`) | **201**; panel shown; URL gains `scenarioId`; `80.000` shown verbatim |
| Evaluate (Finite, `line-4`) | **202** → panel *Accepted*, pending note, Refresh; no "no bottlenecks" claim while open |
| Refresh (manual, ×2) | *Accepted* → **Completed**; bottleneck `line-4 · 2027-W03 · 520.000 · 480.000 · 40.000 · HOUR` (the fixture oracle, verbatim) |
| Reload of the URL | plan + scenario + completed evaluation restored |
| Unknown scenario ID | 404 → safe not-found + support reference; previous scenario, Evaluate and evaluation panel cleared; URL cleaned |

## Tests

- **Diten.Web.Tests 2614/2614.** **SupplyChain 573/574** — only the recorded `ClaimReplayTests…DurableRecovery` (restart mode).
- `SupplyChainCapacityPlansControllerTests`, `CapacityPlansPageTests` (incl. decimals-as-strings + no-polling,
  three versioned loads), `CapacityPlansPageRenderTests`, `CapacityPlanningManifestProviderTests` M-01…M-08,
  `CompositionRootGuardTests` (seeded host registers both providers).
- **Sabotage:** rendering the shortfall through a number conversion turns the decimals fact red; restored byte-identical.

## Re-pin

`Program.cs` → `23f1b55c4af4401bad090c5069bf8c83c6cdda0d3bb1217eda7fd59320c03b85`; MOD-0190 `:261`, MOD-0192 `:273`.

## Not covered

G7 permission seeding to tenant roles; G8 independent VER of both UIs; a real demand source (G3) and a live
SUPPLY-CONSTRAINTS producer (G2, MOD-0193) — the UI is source-agnostic and shows what the composed backend returns.
