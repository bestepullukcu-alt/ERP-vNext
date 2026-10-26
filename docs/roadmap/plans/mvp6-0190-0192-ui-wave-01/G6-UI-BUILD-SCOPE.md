---
module: MOD-0190
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E4
decision: conditional
commit:
---

# G6 — MOD-0190 S&OP + MOD-0192 Capacity tenant UI build — scope

    scoped    : 2026-10-18 by CT (gmg-10) · author: CT
    status    : SCOPE. The UI surface is owner-approved (option A for both,
                `mvp6-sop-capacity-ui-scope-owner-decision-01`, bound to `UI-SCOPE-0190.md`/`UI-SCOPE-0192.md`).
                This scopes the build; it does not write UI code here.
    depends   : **G4 (composed backend) live** and **G5 (gateway routes active)** — the UI needs real
                endpoints to run against. The views/JS/resx can be AUTHORED against the approved surface
                before G4/G5 land, but cannot be verified/shipped until they do.
    deviation : these are NOT the five shipment-bundle DataTable list pages — both are **workspace** UIs
                with **no list** (entry = Create + Open-by-ID). Reuse the shipment-bundle conventions
                (shell, l10n, error handling) but not the GoldenReference list/_DataTable/_Filter scaffold.

## Shared conventions (both modules)

- **Tenant shell** (`_LayoutTenantShell`), 7 languages (en, tr, fr, es, zh, ar, ru) **+ RTL**, l10n via the
  `_IndexL10n.cshtml` bridge + `index.l10n.js` loader, following the **{Claims,Returns} camp canon**
  ([[supplychain-l10n-canonical]]): `Err*` error keys, `EmptyState`/`ActionsHeader`/`ListErrorState`, and
  the camp's error dispatcher shape (`ERROR_KEY`+`t()`). No hardcoded text, no `|| 'fallback'`.
- **GoldenReferenceSlim**, `form_field_count` 5 (0190) / 7 (0192) → both **Slim (≤8)** ⇒ create/edit via an
  in-page `_CreateEditOffcanvas`, NOT a route-based Create page.
- **proxy-profile** (same-origin MVC adapter): JS calls `/SupplyChain/<Module>/api/...`; the MVC proxy reads
  the HttpOnly token server-side and forwards to the gateway. Browser JS never touches `document.cookie` /
  `Authorization`. (This differs from any direct-gateway module.)
- **UAS-001**: server-side `@inject IPermissionSnapshot Perms` + `Perms.Has(...)` + `_AccessDenied`
  early-return before any surface draws; 403 → denied card (fail closed); 401 → shared
  `DtDefaults.handleUnauthorized()`.
- **Tenant isolation**: every call LE-scoped (`legalEntityId` + `withScope`), own endpoints only.
- **Self-registration** as an SR-D4 overlay built with the UI (the §24 manifest provider — note it does not
  exist yet, G4 item C; build it here).
- **No list / no demand / no poll** stay OUT (findings F190/F192-LIST, -DEMAND, -PIN/-ENT/-POLL remain open;
  O-01…O-08 stay out).

## MOD-0190 S&OP — surface (ops: createSandopPlan, getSandopPlan, captureSandopSnapshot, listSandopSnapshots, recordSandopSignOff, listSandopSignOffs)

- **Entry page** (`SandopPlans/Index.cshtml`): "Create plan" (offcanvas) + "Open plan by ID". No plan list.
- **Plan workspace** (view or in-page section keyed by planId): the **snapshots list** and **sign-offs
  list**, with actions **"Capture snapshot"** and **"Record sign-off"**.
- Permissions: `supplychain.sandop-plans.read` / `.create` / `.snapshot.capture` / `.sign-off.record`.
- File set: `Views/SupplyChain/SandopPlans/{Index.cshtml, _Workspace.cshtml, _CreateEditOffcanvas.cshtml,
  _IndexL10n.cshtml}` + `SandopPlansIndex.cs` marker + `wwwroot/assets/js/SupplyChain/SandopPlans/{index.js,
  index.l10n.js}` + `Resources/Views/SupplyChain/SandopPlans/SandopPlansIndex.{7 langs}.resx`.
- Estimate (approved): O/M/P **52 / 86 / 148 h**.

## MOD-0192 Capacity — surface (ops: createCapacityPlan, getCapacityPlan, createCapacityScenario, getCapacityScenario, evaluateCapacityScenario, getCapacityEvaluation)

- **Entry page** (`CapacityPlans/Index.cshtml`): "Create plan" (offcanvas) + "Open plan by ID". No lists.
- **Plan workspace**: **"Create scenario"**, **"Open scenario by ID"**, **"Evaluate"**, and an **evaluation
  panel** with the **bottleneck table** (resource / period / required / available / shortfall / UoM) and a
  **manual Refresh** (NO polling — F192-POLL stays out).
- **Decimal values handled as strings** (no float) — the bottleneck/adjustment measures, matching the domain
  and the SUPPLY-CONSTRAINTS contract's `^-?[0-9]+\.[0-9]{3}$`.
- Permissions: `supplychain.capacity-plans.read` / `.create` / `.scenario.create` / `.evaluate`.
- File set: `Views/SupplyChain/CapacityPlans/{Index.cshtml, _Workspace.cshtml, _CreateEditOffcanvas.cshtml,
  _EvaluationPanel.cshtml, _IndexL10n.cshtml}` + `CapacityPlansIndex.cs` + `wwwroot/assets/js/SupplyChain/
  CapacityPlans/{index.js, index.l10n.js}` + 7 resx.
- Estimate (approved): O/M/P **58 / 98 / 168 h**.

## Build order & who

1. Author resx (7 langs, camp canon) + bridges + marker `.cs` — **l10n-agent** (resx) + CT/frontend.
2. Author the entry + workspace views, offcanvas, (0192) evaluation panel — frontend lane, GoldenReferenceSlim.
3. Author `index.js` (proxy-profile fetch, `withScope`, camp `ERROR_KEY` dispatcher, UAS handling) +
   `index.l10n.js` loader (requiredKeys = the current bridge keys — do not repeat the F-1 stale-guard bug).
4. Write the §24 manifest providers (`SopWorkflowSignoffsManifestProvider`, `CapacityPlanningManifestProvider`)
   + register them (ties to G4 item C).
5. Verify against the **live G4/G5 backend**: the six ops each exercised, decimals render as strings
   (0192), RTL, UAS-001 denied card, tenant isolation; then independent VER (same shape as the 6-UI VER).

## Dependencies & gates still ahead

- **G4 + G5 must be live** for a working UI (fixture-demand is fine for the interim — the UI is demand-source
  agnostic; it shows whatever the composed backend returns).
- **G7 permissions**: the four `supplychain.sandop-plans.*` / `.capacity-plans.*` keys already exist; confirm
  they are seeded to the tenant roles that should see the UI.
- Independent **VER (G8)** on the two new UIs before acceptance.

## Files

- `G6-UI-BUILD-SCOPE.md` — this. Approved surface: `mvp6-sop-capacity-ui-scope-owner-decision-01` +
  `UI-SCOPE-0190.md` / `UI-SCOPE-0192.md`. Pack UI-revision patches: `mvp6-ui-pack-patch-190-192-01/`.
