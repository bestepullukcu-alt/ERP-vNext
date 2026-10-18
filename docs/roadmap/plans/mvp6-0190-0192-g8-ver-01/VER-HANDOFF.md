---
module: MOD-0190
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E4
decision: conditional
commit:
modules: [MOD-0190, MOD-0192]
---

# Wave 6.1 G8 — independent VER of the S&OP + Capacity tenant UIs — hand-off (Phase A read-only-auditor → Phase B persisted by CT)

    prepared  : 2026-10-18 by CT (gmg-10) · owner: "start G8"
    target    : AL-MVP6-0190-0192-G8-VER, VER, HIGH
    repo      : /Users/natig/Projects/ERP-vNext-recovery · branch `feature/mvp6-logistics`
    freeze    : HEAD `fcf42be05` (G6 Capacity). Re-hash every in-scope file at start; fail the run if the tree differs.
    shape     : as `mvp6-6ui-ver-handoff-01` (SOP-22 PASS precedent).

## AGENT CONTRACT (G3)

- **Phase A — `read-only-auditor`** (mode **strict**: no fetch/pull, no `.git` write, no Edit/Write, no fixes).
  `Read` FIRST, in order: `AGENTS.md` (full) · `.antigravity/agents/read-only-auditor.md` ·
  `.antigravity/workflows/read-only-audit.md` · `.antigravity/rules/git-safety.md` ·
  `.antigravity/rules/frontend-standards.md` + `.antigravity/rules/frontend-js-standard.md` ·
  `.antigravity/rules/localization-standard.md` + `.antigravity/rules/dynamic-localization-standard.md` ·
  `.antigravity/rules/unauthorized-surface-standard.md` (UAS-001).
  **Write-scope check:** read-only tools cover an audit; it owns no write. PASS.
- **Phase B — CT** persists `REPORT.md` + `SHA256SUMS` into this folder only and re-verifies the headline
  independently (6-UI VER precedent: the auditor cannot Write). §17.4 fields for the Phase-B persister are supplied
  by analogy (no documentation-writer row).

## Scope (frozen at fcf42be05)

| area | files |
|---|---|
| S&OP UI | `frontend/Diten.Web/Controllers/SupplyChainSandopPlansController.cs`, `Models/SupplyChain/SandopPlans/*`, `Views/SupplyChain/SandopPlans/*`, `wwwroot/assets/js/SupplyChain/SandopPlans/*`, `Resources/Views/SupplyChain/SandopPlans/*` (7) |
| Capacity UI | the same five areas under `CapacityPlans` |
| Nav keys | `Resources/SharedResource.{7}.resx`: `Nav.Module.SOPWORKFLOWSIGNOFFS`, `Nav.Page.SANDOPPLANS`, `Nav.Module.CAPACITYPLANNING`, `Nav.Page.CAPACITYPLANS` |
| Manifests | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/ModuleRegistration/{SopWorkflowSignoffs,CapacityPlanning}ManifestProvider.cs` + the registration in `Program.cs` (demand-seed block) |
| R-2 gates | `…Api/Features/SandopPlans/SandopContextMiddleware.cs`, `…Api/Features/CapacityPlans/CapacityContextMiddleware.cs`, `SandopPlansController.cs` |
| Tests | `frontend/Diten.Web.Tests/{Controllers/SupplyChain{SandopPlans,CapacityPlans}ControllerTests,JavaScript/{SandopPlans,CapacityPlans}PageTests,Lists/{SandopPlans,CapacityPlans}PageRenderTests}.cs`; service: `ModuleRegistration/*ManifestProviderTests.cs`, `CapacityPlans/CapacityLegalEntityScopeTests.cs`, `SandopPlans/SandopContractTests.cs`, `Common/SandopCapacityHttpHost.cs` |
| Authority | `docs/roadmap/plans/mvp6-ui-scope-190-192-01/UI-SCOPE-0190.md`, `UI-SCOPE-0192.md`; `docs/roadmap/plans/mvp6-0190-0192-ui-wave-01/G6-UI-BUILD-SCOPE.md`; `docs/roadmap/plans/mvp6-ui-pack-patch-190-192-01/patches/*` (§23/§24); `docs/records/decisions/2026-10/mvp6-0190-0192-r2-legal-entity-alignment-owner-decision-01.md` |

## NE (verify independently, `path:line` evidence)

1. **Surface = approved scope.** Each adapter exposes exactly the six bound operations of its UI-SCOPE §2 (no list,
   edit, delete, status change); view routes = the §24 manifest RoutePaths; manifest identity/pages/actions = the patch
   tables. Note and judge: the patch names `Nav.Page.SANDOP_PLANS`/`CAPACITY_PLANS`; the code uses the normalized form
   (`NavNameLocalizer.Normalize`) — confirm which one the runtime resolves.
2. **l10n.** 7 languages, identical key sets by `<data name>` MATCH count; no empty, echo (key = value) or English
   placeholder (genuine same words listed and justified); every bridge key resolves; bridge list = `index.l10n.js`
   `requiredKeys` (F-1); every `t('…')` in the scripts is bridged; nav keys present ×7.
3. **Error mapping.** Every code the service can emit (`SandopContractError.cs`, `CapacityContractError.cs`, the gates)
   and every code the adapter emits maps to an `Err*` key; unknown → `ErrInternalError`; no raw server text on screen.
4. **UAS-001.** Without `.read`, both pages render only the denied card (no skeleton, form, script payload); action
   buttons/offcanvases only for the held permission; 403 on load → denied card, no redirect.
5. **Tenant / legal-entity isolation.** Tenant only from the token; legal entity from `?legalEntityId=` → `X-Legal-Entity-Id`,
   validated by MDM in both gates (fail-closed: not referenceable → module 404; unavailable → 503); no claim
   dependence left; a missing scope makes the page send nothing; no call to another module's endpoint; Capacity's
   downstream URL carries no scope query.
6. **Contract fidelity.** Bodies forwarded byte-exact; one Idempotency-Key per opened form; decimals shown as returned
   strings (never parsed); no polling; sign-off confirm; no `innerHTML` with server data.
7. **Composition rule.** Both providers are registered only inside the demand-seed block (production: none).
8. **No secrets / PII** in code, URLs, logs, records or tests (test signing keys are test-only literals).
9. **Build & tests on a disposable copy.** `node --check` all four scripts; build Web + SupplyChain; run the S&OP/Capacity
   web tests. Service HTTP tests need a lane mongod — only on a loopback port ≠ 27017–27021 in a new folder; if not
   provisioned, report them UNPROVEN, not green.

## YAPMA

No source/resx/JS repair, no git mutation, no owner acceptance. A defect is a **finding** → a separate DEV lane.

## DOĞRULA (gate)

SOP §22 verdict with evidence, severity per finding, required rework, and an unchanged-repo proof (`git status`
tracked-clean, `git diff --name-only` empty after the run).
