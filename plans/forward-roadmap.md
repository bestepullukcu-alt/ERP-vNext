# ERP-vNext — Forward Roadmap (canonical)

> Last updated: 2026-06-24 (PHASE A COMPLETE — A1+A2+A3 done & live-verified, uncommitted/held). Supersedes `foundation-roadmap-2026-06-24.md`.
> Owner direction: finish **Access Governance + infrastructure** first, then build **WorkCenter**. HR is owned by another team → HR-foundation (Evidence/Masking) is OFF this critical path.
> Operating model: Claude = CONTROL TOWER (writes execution prompts, read-only/live verifies, does not author product code). Execution = Antigravity agents.
> Companion: code-truth status tracker `execution/registries/module-implementation-status.md`.

## Current state (done / in main+branch)
- ✅ MOD-0288 Org/Person/Position (tenant-side, e2e-proven) · ✅ MOD-0220 Legal Entity FULL **Phase 1** (rich entity + 8-step wizard + reference lookups; evidence/workflow gating deferred) · ✅ MOD-0285 Navigation menu loader (code→menu, browser-proven).
- ✅ Landed from main (other dev, PR #35): **MOD-0023 Workflow** + **MOD-0028 Document Management** (QMS baselines + collection instantiation).
- ✅ **PHASE A COMPLETE (this effort, uncommitted on `feature/mod-0285-navigation`, HELD):**
  - **A1** Platform `[HasPermission]` auto-registration → auth catalog + SuperAdmin auto-grant (kills the recurring 403 trap; live-verified — doc-mgmt's 20 perms auto-registered).
  - **A2** Access Governance debts: FU13 cache-invalidation mechanism (7 mutation handlers bump roleAssignmentVersion) + FU17 tenant security self-service UI (GET/PUT live-verified) + RoleAssignments/UserRoleAssignments row l10n.
  - **A3** Workflow completion: recurring escalation sweep job (cross-tenant, Hangfire `*/5`, live-registered) + per-user `tasks/mine` query (live 200) + `IWorkflowTransitionGate` (in-process, fail-closed) wired into Module-Catalog activate as reference (gate live-invoked: NotApplicable→Allowed→commit). 824/824 tests.
- Foundation already solid: MOD-0018 RBAC, MOD-0021 Audit, MOD-0032 Gateway, MOD-0035 Event Bus, tenant lifecycle, subscription/catalog/entitlement, AG 5 RBAC screens, self-registration + dynamic menu.

## Architecture decisions relayed to doc-mgmt dev (this session)
- MOD-0028/0029 permissions → **central RBAC** (A1 auto-registers `platform.document-management.*`), NOT self-contained.
- MOD-0029 file/folder access → **two-layer**: Layer 1 central RBAC capability + Layer 2 `FolderDocumentAccessPolicy`/`DocumentAccessPolicy` instance-level (data in doc module, AND-checked). Approved.

---

# ROADMAP

## PHASE A — Access Governance + Infrastructure (BEFORE WorkCenter) — ✅ COMPLETE (live-verified, HELD uncommitted)

### A1 — ✅ DONE — Permission registration (systemic)
- Platform `[HasPermission]` auto-registration worker → auth catalog on boot + `FullCatalogPermissionGrantService` auto-grants new perms to default-tenant SuperAdmin. Kills the recurring 403 trap permanently. Live-verified (doc-mgmt's 20 perms auto-registered).

### A2 — ✅ DONE — Access Governance debts
- MOD-0018-FU13 **cache invalidation** mechanism (roleAssignmentVersion `$inc` on 7 mutation handlers; mechanism-ready, no live staleness today). MOD-0017-FU01 **tenant security self-service UI** (backend GET/PUT + frontend, live-verified). RoleAssignments/UserRoleAssignments **row l10n** done.

### A3 — ✅ DONE — Finish Workflow (landed MOD-0023 gaps)
- Recurring escalation sweep job (cross-tenant, Hangfire `*/5`, config-gated, live-registered) + per-user `tasks/mine` query (live 200) + `IWorkflowTransitionGate` in-process fail-closed helper + standard doc, wired into Module-Catalog activate as the reference consumer (gate live-invoked, Allowed→commit; Blocked→no-commit unit-proven). 824/824.
- ⏳ Deferred to natural pickup: notification hook (MOD-0027) when WorkCenter needs it; legal-entity cross-service gate (MDM→Platform HTTP) in MOD-0220 Phase 2.

### A5 — Other infra + quality debt  [size: S–M, interleave/parallel]
- MOD-0033 quota override UI · MDM `launchSettings` (so it runs without manual `ASPNETCORE_ENVIRONMENT=Development`).
- Tests for DevEnablement + Gateway (zero today); EnterpriseStrategy l10n (91 views) + MOD-ID assignment + monolithic-test refactor.

> **A4 (HR-foundation) intentionally NOT on this path.** MOD-0031 Evidence + MOD-0019 Data Masking are HR prerequisites owned by another team. WorkCenter does not need them. We pick up MOD-0031 later only for MOD-0220 Phase 2.
> ⚠️ Coordination note for the HR team: build MOD-0031 Evidence + MOD-0019 Data Masking as **shared platform services**, not HR-specific — other modules (MOD-0220 Phase 2, etc.) consume them.

## PHASE B — WorkCenter (after tenant-side audit) — owner's module — **FRONTEND-FIRST**
> Owner strategy: WorkCenter is large/fuzzy → build frontend-first to SEE the UX, then backend to match (no churn). See [[project-workcenter-frontend-first]]. Starts only when owner says the tenant-side audit is done.
- **Stage 1 (NOW when Phase B opens):** finish WorkCenter frontend on MOCK (`mock-work-items.js`) — design fixes + missing UI (meeting notes, create/edit, subtask/dependency/time-entry). LOCK UX. NO backend.
- **Stage 2:** extract contract from locked UX (data shapes, actions, state transitions).
- **Stage 3:** build **MOD-0024 Task & Checklist Engine** to the contract (entities, CRUD, state, per-user my-work query, MOD-0023 integration, self-register). Backend built ONCE.  [size: M–L]
- **Stage 4:** wire mock → real. Inbox = ApprovalTask (MOD-0023, done) + Task/Checklist (MOD-0024); entitlement-gated tabs.  [size: M]
- Current: WorkCenter FE prototype exists (Index tabs Inbox/AllWork, Task detail, Meeting; ~24 partials) on mock; MOD-0024 backend = none.

## PHASE C — MOD-0220 Phase 2 + verticals (later)
- MOD-0220 **Phase 2**: evidence-gated lifecycle (needs MOD-0031), Corporate Action Workspace, Filing Calendar, Save View (MOD-0287).  [size: L]
- Then **HR/CRM verticals** once foundation is complete.  [size: XL]

---

## Debt register
| Debt | Type | Severity |
|---|---|---|
| Platform-module `[HasPermission]` auto-registration | systemic | 🔴 |
| MOD-0023 permission seed/grant + workflow gaps (job/query/notify) | foundation | 🔴 |
| MOD-0018-FU13 cache invalidation | correctness | 🟡 |
| MOD-0017-FU01 tenant security self-service UI | UX | 🟡 |
| RoleAssignments/UserRoleAssignments row l10n | l10n | 🟡 |
| DevEnablement + Gateway tests (zero) | quality | 🟡 |
| EnterpriseStrategy l10n (91 views) + MOD-ID + test refactor | quality | 🟡 |
| MDM launchSettings (dev-env) | dev-env | 🟢 |
| MOD-0220 UI styling refinement (owner to restyle wizard) | UX | 🟢 |

## Sequencing ("when" — dependency-ordered, not velocity)
**Now → A1** (systemic permission fix + MOD-0023 seed — makes workflow usable, closes the 403 trap for every future module) → **A2** (access debts) → **A3** (finish workflow) → **PHASE B WorkCenter** (MOD-0024 → wiring) → **PHASE C** (MOD-0220 P2 + verticals). A5 quality interleaves throughout. A4 runs in parallel by the HR team, off our path.

## Notes
- Everything accumulates on `feature/mod-0285-navigation` until owner says otherwise (no new branches).
- Dev configs (`appsettings.Development.json`, `.claude/settings.local.json`) stay local, never committed.
- Pre-push sync recipe: commit local → `git stash` dev-config → `git merge origin/main` → resolve → `git stash pop` → re-gate → fix merge incompatibilities → commit → push.
