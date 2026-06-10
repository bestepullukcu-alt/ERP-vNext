# ERP-vNext Access Governance Completion Plan

> **Authority boundary.** This document is the Access Governance execution-roadmap reference. It does not override Module Pack, Domain Config, AGENTS.md or .antigravity standards. For implementation and scope decisions, the repository authority order remains: Module Pack > Domain Config > AGENTS.md > .antigravity standards > live governance records > archive/external references.

> **Mode:** strict repository-read-only audit + single canonical execution plan. This roadmap was originally produced from a strict repository-read-only audit. It was later persisted and refreshed through governance-only branches. No runtime code, schema, DTO, endpoint, frontend, gateway or build artifact changes are introduced by this roadmap document. Evidence captured on `main` @ `b1e6c33` (= `origin/main`), working tree clean, single worktree (plus the in-flight `feature/governance/mod-0288-drift-reconciliation` worktree, uncommitted).
> **Output note:** This plan is **persisted in the repository** at `execution/portfolio/access-governance-completion-plan.md` and **merged to `main` via PR #26**. **AG-STEP-000 completed.**

---

## 0. Authority Model (two separate layers — never conflated)

Every decision resolves against **one** of two distinct authority chains. Do not mix them: an identity question is never answered by a module pack, and a scope/runtime question is never answered by the Blueprint.

### A. Module Identity Authority
*Layer A scope: MOD-xxxx identifier, canonical name, and parent / FU / child relationship — nothing else.*
1. **Blueprint** — `docs/System Capability & Implementation Blueprint - master 5.xlsx`, sheet `Blueprint_Data`.
2. `execution/registries/module-id-registry.md`.
3. `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`.
4. `verify_module_id.py` fail-closed result.

Rules: never guess a new MOD-xxxx. If an ID is **not in the Blueprint**, write **`CAND-CAP REQUIRED — EA DECISION`**. Deprecated aliases (`MOD-0040 → MOD-0288`, `MOD-0040-FU01 → MOD-0288-FU01`, …) are never used as new identities.

### B. Implementation & Scope Authority
*Layer B scope: repo scope, protected paths, acceptance criteria, runtime limits, implementation decisions.*
1. **Module Pack**
2. Domain Config
3. AGENTS.md
4. .antigravity standards
5. Registry, master plan, DCP, delivery board (live repo records)
6. Archive / external references (reference only)

**Conflict rule (within either chain):** the more specific source in the relevant chain wins.

**Worked examples:** MOD-0019/0020/0151 — identity settled via layer A (Blueprint + `verify_module_id.py` OK) though absent from the registry; scope/pack is a layer-B gap. Tenant Role / Tenant Group — no Blueprint ID (layer A) → `CAND-CAP REQUIRED — EA DECISION`. NoOp resolver / allowed paths / fail-closed AC — pure layer-B. MOD-0288/FU01 — identity settled; only layer-B governance status open. DCP-002 sits in both chains and is `draft`; identity resolution does not depend on its promotion.

---

## Live State Refresh Rule (mandatory before every AG-STEP)

This plan is a **fixed roadmap**. Live branch, HEAD, diff, or pack state are **never assumed from the plan text** — they are re-read at execution time. Before starting any `AG-STEP-XXX`, run a strict read-only preflight and record it in the handoff:

- current branch
- HEAD (short)
- `origin/main` HEAD (short)
- `git status --short --untracked-files=all`
- staged diff (`git diff --cached --name-status`)
- unstaged diff (`git diff --name-status`)
- untracked files
- `git worktree list`
- active branch list (`git branch -vv`)
- the relevant module-pack status (frontmatter)
- dependency state (predecessor AG-STEPs / gates)
- concurrent write-capable agent risk (is another lane open on a shared file/worktree?)

If the live state contradicts the plan (drifted HEAD, unexpected dirty tree, pack status moved), **stop and reconcile** before writing.

---

## 1. Executive Summary

**What is complete (runtime):** Permission evaluation (MOD-0018 `EntitlementChecker`), tenant authorization context (MOD-0018-FU12 `JwtTenantAuthorizationContext`), entitlement audit sink (→ MOD-0021), AuthService RBAC primitives (Role/Permission/UserRole/RolePermission CRUD), Tenant User lookup-validation (CAND-CAP-0001), organization master data (MOD-0288: OrganizationUnit/Position/PositionAssignment + controllers + repositories), fail-closed PositionAssignment.UserId validation (MOD-0288-FU01), **action-aware authorization already real** (distinct `[HasPermission(...Read/Create/Update/Delete/Archive/Export/BulkDelete)]`, 87+ keys), and strong tenant isolation (gateway `TenantResolutionMiddleware` + `X-Tenant-Id`).

**What is missing / not real:** real `IDataScopeResolver` (MOD-0018-FU15) — `NoOpDataScopeResolver` is still the registered default, so the context hydrates **empty** scopes; **Explain Access (FU14)** absent; **cache-invalidation convention (FU13)** partial; **temporary access (FU11)** NoOp; **Tenant Group / Group→Role** absent in code with no Blueprint ID; **permission-key naming inconsistent** (4 styles); `[HasPermission]` **not universal** (Platform-only; AuthService/MDM ungated); **governance drift (MOD-0288/FU01) is now closed** — packs/registry/master-plan/DCP-001 reconciled to `done` and merged via PR #25; **DCP-002 promotion (`draft → approved`) remains an open backlog item**.

**Critical path (one line):** persist+approve this plan → close MOD-0288 governance drift → lock permission-key convention → ship real DataScopeResolver (FU15) replacing NoOp → define+verify business-module enforcement contract → verify Business-Domain Start Gate → **business-domain backend may start**.

**When can business-domain modules start?** At the **Business-Domain Start Gate** (§8). *Business-domain examples include CRM, HR, Sales, Procurement, Finance, Inventory, Warehouse, Project, Asset and Service modules. The actual next module is selected later from the Blueprint after live dependency, canonical ID and module-pack readiness checks. This plan does not preselect CRM or HR as the mandatory first domain.* Field masking (MOD-0019), SoD (MOD-0020), Territory (MOD-0151), Explain Access and temporary access are later-gate items, **not** backend-start blockers.

**Verdict:** `ACCESS GOVERNANCE PLAN READY FOR FINAL REVIEW` — EA decisions pending on Tenant Role / Tenant Group identity and the permission-key convention (§14).

---

## 2. Historical Audit Snapshot

| Item | Current State | Evidence | Risk |
|---|---|---|---|
| Branch | `main` | `git branch --show-current` | — |
| HEAD | `b1e6c33` = `origin/main` | `git rev-parse` | in sync |
| Worktrees | main + `feature/governance/mod-0288-drift-reconciliation` (uncommitted) | `git worktree list` | drift fix not committed |
| Staged diff | none | `git diff --cached` empty | — |
| Unstaged diff (main) | none | `git status` clean (2 untracked: control-tower doc + worktree dir) | low |
| Active member packs | MOD-0018 (+FU10/10a/10b `ready-for-dev`), FU12 `done`, MOD-0288 `ready-for-dev`, MOD-0288-FU01 `ready-for-dev`, CAND-CAP-0001 `done`, MOD-0220 `ready-for-dev` | frontmatter | drift (below) |
| Delivery board | active queue; spine not represented as line items | platform-delivery-board.md | board ≠ reality |
| DCP-001 | `approved` (governance-only) | DCP-001 §20 | OK |
| DCP-002 | **`draft`** — yet cited as canonicalization authority | DCP-002 frontmatter | authority-on-draft |
| NoOp seams (prod DI) | `IDataScopeResolver`→`NoOpDataScopeResolver` (DI 51); `ITemporaryAccessProvider`→`NoOpTemporaryAccessProvider` (DI 50) | `Diten.Platform.Application/DependencyInjection.cs` | silent no-enforcement |
| Governance drift | MOD-0288 + FU01 runtime merged; packs/registry/master-plan `ready-for-dev` | PR #24 / `d816db6` | board misrepresents reality |
| Concurrent-agent risk | one branch+worktree open; two write-capable AIs must not share a worktree nor edit the same governance file in parallel | this session | medium |

> **Historical Audit Snapshot — NOT live repository state.** This table is the original audit baseline (captured at `main` @ `b1e6c33`) and is preserved as historical record. It does **not** reflect the current repository; for live status see **Current Execution State** below, and always apply the **Live State Refresh Rule** before each AG-STEP.

---

## Current Execution State

- AG-STEP-000: completed — plan persisted and merged via PR #26
- AG-STEP-001: completed — MOD-0288 + MOD-0288-FU01 governance drift closed via PR #25
- AG-STEP-003: completed — MOD-0220 LegalEntityId read-only contract **verified present both sides** (read-only audit); governance reconciliation only, no follow-up pack needed
- AG-STEP-004: completed — canonical permission-key standard PKS-001 committed (`.antigravity/rules/permission-key-standard.md`)
- AG-STEP-008: completed — MOD-0018-FU15 Real DataScopeResolver pack authored, reviewed, revised, promoted `draft → ready-for-dev`
- AG-STEP-009: **implemented in integration branch** (commit `26d4fe7`) — real `OrgDataScopeResolver`; production DI `IDataScopeResolver → OrgDataScopeResolver`, `NoOpDataScopeResolver` no longer the production default; **G4 integration audit PASSED**; tests **528 passed, 0 failed**; blocker **none**. **Not pushed / no PR / not merged to `main`** (merge-freeze; awaits batch merge)
- AG-STEP-006A: **completed in integration branch** (commit `91a2604`) — BME-001 mandatory `[HasPermission]` enforcement rule for new business-domain modules (`.antigravity/rules/business-module-enforcement-standard.md`). **Not pushed / no PR / not merged to `main`**
- AG-STEP-013: **completed in integration branch** (commit `45561e7`) — BME-001 Part II business-module permission + data-scope enforcement contract (C1–C8) **and** repo-grounded reference pattern documented. **Not pushed / no PR / not merged to `main`**
- AG-STEP-021: **Business-Domain Start Gate — integration-branch PASS** (audit @ `d55a564`); all Backend Start Gate criteria (000/001/004/009/013/006A + tenant isolation) verified against the live repo. **Main unlock `PENDING BATCH MERGE`** — `main` still `d3ab4a4`, branch local-only; **no production business-domain rollout until merged**; no automatic rollout
- AG-STEP-002: **completed in integration branch** (commit `3b9d342`) — DCP-002 `status: draft → approved`; §16/§21/§14/§19 wording reconciled with §16a; `verify_module_id.py --check-all` → **EXIT 0**. **Not yet pushed** (ahead of pushed upstream `01219a0`) / **no PR / not merged to `main`**
- AG-STEP-004B: **IN PROGRESS** — migration design + compatibility map authored (commits `ba57460`, `257a68c`); **Slice 1A (D-5) done** (runtime commit `64417c2`; security audit PASS; 25 tests) — MDM Legal Entity controller switched to canonical `mdm.legal-entities.*`, no alias seam needed; **Slice 2 (D-6 validator) done** (runtime commit `20d9306`; audit PASS; 532 tests) — both ModulePage `IsCanonicalPermission` validators relaxed `== 3 → >= 3`, uppercase normalized-accepted; **Slice 1B (Platform-only alias seam) done** (Commit A `129c62e`, Commit B `a800407`; security audit PASS; 554 tests) — 55-entry immutable Platform alias map + dual-read in `HasPermissionAttribute`; canonical OR legacy alias claim → allow; bypass unchanged; AuthService/DevEnablement/MDM untouched; **Slice 3 (Platform.* rename) done** (audit PASS; 554 tests; legacy `Platform.*` = 0) — 80 uses / 32 keys / 8 controllers → canonical, 100% map coverage; 6 commits (`ac12223`…`2768b2c`); alias map/wiring unchanged; **Slice 4 (Modules.* org rename) done** (audit PASS; 554 tests; Platform legacy `Modules.*` = 0) — 45 uses / 20 keys / 6 controllers → canonical, 100% map coverage; 4 commits `749fc3d` module-catalog · `d3d3fda` organization-units · `172c837` positions+manager-chain · `4395125` position-assignments; alias map/wiring unchanged, dual-read fixture preserved; **Slice 5A (tenant verb view→read) done** (runtime commit `cb3fda5`; 554 tests) — 3 keys / 10 enforcement points / 3 files → canonical `.read`, dual-read still supports legacy `.view` claims; **Slice 5B (EnterpriseStrategy `_→-`) BLOCKED — Option C: external evidence required** (grant-source audit done; no runtime change; EnterpriseStrategy untouched) — `[EnterpriseStrategyPermission]` → `DefaultEnterpriseStrategyAuthorizationService.HasPermissionAsync`: dev-bootstrap path uses the const as single source (rename self-consistent) but the enforced path exact-matches an **external JWT `permission` claim with no alias/normalization**; no ES-side seam, no in-repo issuer/seeder. **External fact needed:** does the production issuer/AuthService grant these 4 keys with `_` or `-`, and is ESBP enforced-mode live? → Option A (1-file atomic rename) if no live `_` grant; Option B (ES compatibility shim first) if it can; **Slice 6 (standards reconciliation) done** (docs commit `0dd362d`; no runtime/test change) — 7 `.antigravity/**` standards/agent docs: stale PascalCase `Platform.*`/`Modules.*` examples → PKS-001 lowercase-dotted; PKS-001 & BME-001 intentional records preserved; **Slice 7 (alias retirement) BLOCKED — Option C: external evidence required** (readiness audit done; alias map NOT removed/narrowed; no runtime/test change) — production legacy `[HasPermission]` = 0 (criterion 4 met), but the 55 aliases bridge the JWT `permission` claim whose spelling is external (AuthService `TokenService` pass-through; real strings in the `Permission`/`RolePermission` DB; no in-repo Platform permission catalog), and no deprecation window has elapsed (unmerged; merge-freeze) → 0 aliases safely removable from repo evidence. **Both 5B and 7 are BLOCKED pending external grant-catalog evidence.** **Latest Slice 7 blocker-doc commit not yet pushed / no PR / not merged to `main`**
- Current main baseline: `d3ab4a4`
- Next critical step: **AG-STEP-004B** — permission-key migration (PascalCase → PKS-001), plus the pending **batch merge to `main`** (merge-freeze) to make the integration-branch milestones live
- Parallel-safe candidates:
  - AG-STEP-002 — DCP-002 `draft → approved`
- **Operating mode — MERGE FREEZE:** no reviewer/admin available (~1 week), so branch→`main` merges are paused. All remaining work consolidates on a **single integration branch** and merges in one batch when the freeze lifts. Per-step branch-off-`main` is suspended until then.
- **Active execution branch:** `feature/governance/access-governance-execution` — created off the plan-refresh tip `adfa140`, so it already contains AG-STEP-000 + AG-STEP-001 (via `main` ancestry) **and** the plan refresh. Each AG-STEP lands as its own commit on this one branch; under the freeze, the parallel-safe steps run **sequentially** here.
- **Plan refresh:** committed + pushed (`adfa140`, branch `…-completion-plan-refresh`); PR/merge pending (freeze). Superseded for ongoing work by the execution branch above, which already includes it.
- **Control model:** this CONTROL TOWER chat routes steps, verifies handoffs, and maintains this plan file; the actual development for each AG-STEP runs in a **separate execution chat** working on the execution branch above.

---

## 3. Architecture Decisions (locked v1 baseline)

| Decision | Final Rule | Reason | Future Extension |
|---|---|---|---|
| Role → Permission | Permissions attach to Role only | single auditable grant path | flat composition |
| User → Role | permissions only via Role | no raw user grants | — |
| Group → Role | allowed model; **no code + no Blueprint ID today** | RBAC is MOD-0018; Group not its own Blueprint module | EA: MOD-0018-FUxx or CAND-CAP |
| Position → direct Permission | **forbidden** | Position is org structure, not a permission store | never |
| Position → Role binding | deferred (absent; correct) | keep org/auth decoupled in v1 | optional later FU |
| Position hierarchy visibility | source for Data Scope + Manager Chain | org tree drives scope, not permission | depth (OD-4) |
| Data Scope | layer separate from Role; decides *which records* | Role = action; Scope = rows | resolver = FU15 |
| Territory | **Data-Scope target type**, own master data = MOD-0151 | avoid parallel permission engine | MOD-0151 |
| Process Context | Permission + Process Context (+ Temporary Access) | one decision pipeline | MOD-0023/0024 + FU11 |
| Temporary Access | time-bound, process/expiry-revoked; **never persistent Role write** | no grant leakage | FU11 |
| Explicit Deny | fail-closed; deny wins | security baseline | explicit deny rules later |
| Deep Role Inheritance | **not** in v1 (flat + composition) | complexity/perf | later |
| Action-aware permissions | read/update/approve/export/bulk distinct — **already implemented** | least privilege | extend to new actions |
| Module opt-in | business modules **explicitly** opt into scope; no auto-open | prevent accidental exposure | per-module contract |
| Cache invalidation | MOD-0018 + CAND-CAP-0002-FU05 today; convention = FU13 | coherent eviction | event-driven (FU13) |
| Explain trace | required; **missing today** | debuggability, audit | FU14 |
| Audit | deny logged (real sink → MOD-0021); allow policy open (OD-9) | volume vs traceability | FU14 + policy |

---

## 4. Completed Foundations

| Step ID | Module / Capability | Canonical ID | Status | Evidence | Closure Needed? |
|---|---|---|---|---|---|
| F-01 | Permission evaluation | MOD-0018 (+FU10/10a/10b) | runtime real | `EntitlementChecker.cs`; DI 136 | No |
| F-02 | Tenant authorization context | MOD-0018-FU12 | `done` | `JwtTenantAuthorizationContext.cs` | No |
| F-03 | Entitlement audit sink | MOD-0018 → MOD-0021 | runtime real | `PlatformEntitlementAuditSink.cs`; DI 113 | No |
| F-04 | AuthService RBAC primitives | RBAC = MOD-0018 (no own pack/ID) | runtime real | Role/Permission/UserRole/RolePermission CRUD | **Yes — governance** |
| F-05 | Tenant User lookup-validation | CAND-CAP-0001 (alias MOD-0047) | `done` | `GET /api/users/{id}/lookup-validation`, 15 tests | No |
| F-06 | Org/Person/Position directory | MOD-0288 (alias MOD-0040) | `done` | entities/controllers/repos; governance reconciled (PR #25) | No |
| F-07 | PositionAssignment.UserId validation | MOD-0288-FU01 | `done` | PR #24, fail-closed, 510 tests; governance reconciled (PR #25) | No |
| F-08 | Action-aware enforcement (Platform) | MOD-0018 | runtime real | distinct `[HasPermission]`, 87+ keys | partial (universalize) |
| F-09 | Tenant isolation | platform | runtime real | gateway `TenantResolutionMiddleware` | minor (404 vs 403) |

---

## 5. Remaining Phases

Step format `AG-STEP-NNN`. "Parallel?" references §7. The **Live State Refresh Rule** runs before each step.

### PHASE-01 — Plan Persistence, Identity & Reference Closure
| Plan Step | Capability / Module | Canonical ID | Current State | Pack Needed? | Dependencies | Parallel? | Owner Agent | Repo Scope | Build/Test Gate | Done Criteria | Unlocks |
|---|---|---|---|---|---|---|---|---|---|---|---|
| **AG-STEP-000** | **Persist and Approve Access Governance Completion Plan** | — (governance) | **completed** — persisted & merged (PR #26) | no (this audit) | none | n/a | ACCESS GOVERNANCE EXECUTION + user | none now; later a governance-only write to `execution/portfolio/access-governance-completion-plan.md` | none | (a) plan reviewed by user; (b) open-decision list recorded and tracked; (c) ready to persist via a separate write prompt; (d) subsequent AG-STEPs read this file as the execution-roadmap reference. This plan never overrides Module Pack, Domain Config, AGENTS.md or .antigravity standards. | the whole roadmap |
| AG-STEP-001 | MOD-0288 + FU01 governance drift closure | MOD-0288, MOD-0288-FU01 | **completed** — drift closed (PR #25) | no (status edit) | AG-STEP-000 | no (registry/DCP-001/master-plan) | read-only-auditor + module-pack-author | docs (packs, registry, master-plan, DCP-001 §20) | none | all 4 sources `done`; verify gate OK | accurate board |
| AG-STEP-002 | DCP-002 promotion `draft → approved` | DCP-002 | **completed in integration branch** (commit `3b9d342`; **not yet pushed / no PR / not merged to `main`**) | no | AG-STEP-000 | yes (after 001) | read-only-auditor → orchestrator | DCP-002 only | none | **DONE (integration branch).** DCP-002 `status: draft → approved`; §16/§21/§14/§19 wording reconciled with §16a (CAND-CAP-0001…0005 recorded; final EA canonical-`MOD-xxxx` allocation is a separate downstream decision, not an approval blocker; no new MOD-xxxx invented). Read-only audit verified deprecated-alias chains resolve; **`verify_module_id.py --check-all` → EXIT 0** (0 hard violations). | trustworthy alias chain |
| AG-STEP-003 | MDM Legal Entity read-only `LegalEntityId` contract — **verify, do not assume** | MOD-0220 | **verified / complete** — contract present both sides (read-only audit) | no (governance reconciliation only) | MDM domain-config | done | read-only-auditor + orchestrator | read-only inspection of `Diten.MdmService` + `Diten.Platform`; docs-only reconciliation | none | **DONE.** Live audit confirmed the read-only lookup-validation contract on both sides → governance reconciliation only; **no narrow MOD-0220 follow-up pack needed**, no implementation on assumption. **Provider (MOD-0220):** `GET /api/legal-entities/{id}/lookup-validation` → `ValidateLegalEntityReferenceQuery` → `ValidateLegalEntityReferenceHandler` → `RepositoryBase.GetByIdAsync` (`TenantFilter` = same-tenant + `IsDeleted==false`) + `LifecycleStatus==Active`; returns `LegalEntityLookupDto(LegalEntityId, LegalName, DisplayName, LifecycleState, Referenceable)` — matches MOD-0288 §7 locked contract 1:1. **Consumer (MOD-0288):** `ILegalEntityReferenceValidator` / `MdmLegalEntityReferenceValidator` (HTTP GET, **fail-closed** on non-2xx / ID-mismatch / non-ACTIVE / `Referenceable!=true` / network+JSON errors), wired Scoped with `TenantPropagationHandler`, consumed by Create/UpdateOrganizationUnitCommandHandler. **Note:** provider permission `Modules.LegalEntity.Read` is PascalCase → **AG-STEP-004B** migration target only (no rename here). | MOD-0288 LegalEntityId scope; FU15 LegalEntity scope **ungated at governance level** (runtime stays fail-closed) |

### PHASE-02 — Permission Catalog, Tenant Role & Group Foundations
| Plan Step | Capability / Module | Canonical ID | Current State | Pack Needed? | Dependencies | Parallel? | Owner Agent | Repo Scope | Build/Test Gate | Done Criteria | Unlocks |
|---|---|---|---|---|---|---|---|---|---|---|---|
| AG-STEP-004 | **Permission-key convention decision and catalog baseline** | MOD-0018 (governance) | 4 styles coexist (`auth.users.create`, `Modules.OrganizationUnit.Read`, `platform.tenants.quotas.view`, `mdm.legal-entities.read`) | no (decision + catalog) | AG-STEP-000; EA OD-C | no (cross-cutting) | read-only-auditor → backend-architect | catalog doc | none | locks **only**: canonical format · immutable-key rule · deprecated-alias rule · module ownership · action-suffix dictionary · migration principles | safe key growth |
| **AG-STEP-004B** | **Existing permission-key migration plan and compatibility strategy** | MOD-0018 (governance → impl) | **IN PROGRESS (integration branch).** Migration design + compatibility map authored (`access-governance-permission-key-migration-plan.md`); slice ordering reconciled. **Slice 1A (D-5) done** (runtime commit `64417c2`; security audit PASS; 25 tests) — MDM `LegalEntitiesController` 6 legacy attrs → canonical `mdm.legal-entities.*`, **no alias seam (not needed for D-5)**. **Slice 2 (D-6 validator) done** (runtime commit `20d9306`; audit PASS; 532 tests) — **both** `IsCanonicalPermission` validators relaxed `== 3 → >= 3`; uppercase normalized-accepted. **Slice 1B (Platform-only alias seam) done** (Commit A `129c62e` map+resolver; Commit B `a800407` `HasPermissionAttribute` dual-read wiring; security audit PASS; 554 tests) — **55-entry** immutable Platform alias map; canonical requirement satisfied by canonical OR legacy alias claim; unknown/no/unauthenticated → deny; legacy requirement **not auto-upgraded**; `platform_admin`/`partner_admin` bypass + admin-lifecycle **unchanged**; AuthService/DevEnablement/MDM untouched. **Slice 3 (Platform.* rename) done** (audit PASS; 554 tests; production-controller legacy `Platform.*` = 0) — **80 attribute uses / 32 distinct keys / 8 controllers** → canonical `platform.*`, alias-map coverage 100%; 6 resource-group commits `ac12223` admins · `4e94454` audit · `6f6673a` interface-registry · `8ba4aeb` lookups · `67518de` notifications · `2768b2c` subscriptions; alias map/wiring unchanged. **Slice 4 (Modules.* org rename) done** (audit PASS; 554 tests; Platform production-source legacy `Modules.*` = 0) — **45 attribute uses / 20 distinct keys / 6 controllers** (ModuleCatalog, ModulePages, ModuleAssignments, OrganizationUnits, Positions, PositionAssignments) → canonical `platform.*`, alias-map coverage 100%; 4 resource-group commits `749fc3d` module-catalog · `d3d3fda` organization-units · `172c837` positions+manager-chain · `4395125` position-assignments; alias map/wiring unchanged, `Modules.OrganizationUnit.Read` dual-read fixture preserved. **Slice 5 split (live-repo audit):** **5A (Platform tenant verb `view→read`) done** (runtime commit `cb3fda5`; 554 tests) — 3 distinct keys / 10 enforcement points / 3 files (Quotas, TenantCommercialSubscriptions, TenantModuleEntitlements) → canonical `.read`, dual-read still supports legacy `.view`; alias map/wiring unchanged, EnterpriseStrategy untouched. **5B (EnterpriseStrategy `_→-`) BLOCKED — Option C: external evidence required** (grant-source audit completed; runtime rename NOT done; EnterpriseStrategy untouched). Repo evidence: `[EnterpriseStrategyPermission]` → `DefaultEnterpriseStrategyAuthorizationService.HasPermissionAsync` (only impl) — dev-bootstrap path builds `KnownPermissions` from the consts (const = single source, rename self-consistent), but enforced path (`DITEN_ESBP_ENFORCE_PERMISSIONS`) exact-matches an **external JWT `permission` claim with no normalization/alias**; **no ES-side alias/dual-read seam; no in-repo issuer/seeder grants these keys** (27 controller refs use the const NAME, no test hardcodes the strings). **External fact required:** production issuer/AuthService grants these 4 keys with `_` or `-`, and is ESBP enforced-mode live? → **Option A** (1-file/4-line atomic const rename, à la 1A) if no live `_` grant / enforced off; **Option B** (ES-side `_↔-` compatibility shim + tests, then rename) if a live `_` grant can exist; **no rename without external evidence**. **Slice 6 (standards reconciliation) done** (docs commit `0dd362d`; no runtime/test change) — 7 `.antigravity/**` standards/agent docs (erp-architecture, module-pack-standard, security-jwt, response-envelope, agents: backend-architect/security-agent/product-manager): stale PascalCase `Platform.*`/`Modules.*` examples → PKS-001 lowercase-dotted (kebab-case, `View→read`); **PKS-001 & BME-001 intentional census/counter-example records preserved untouched**; `module-pack-author.md` already clean. **Slice 7 (alias retirement) BLOCKED — Option C: external evidence required** (readiness audit completed; alias map NOT removed/narrowed; no runtime/test change). PKS-001 §9: production legacy `[HasPermission]` = 0 (criterion 4 ✅), but the 55 aliases bridge the JWT `permission` claim whose spelling is **external** — AuthService `TokenService` is pass-through, no in-repo Platform permission catalog, real strings live in the `Permission`/`RolePermission` DB (criteria 1/2/3 unverifiable); no deprecation window has elapsed (unmerged, merge-freeze) → criteria 6/7 unmet; **0 of 55 aliases safely removable from repo evidence**. **External evidence required:** DB permission catalog canonical? all RolePermission grants migrated to canonical? live JWT emits canonical-only? post-merge/deploy dual-read deprecation window elapsed? **5B and 7 are independent code surfaces (EnterpriseStrategy is a separate service) but both blocked on external grant-source verification before rollout.** **Both 5B and 7 BLOCKED.** **Latest Slice 7 blocker-doc commit not pushed / no PR / not merged to `main`.** | yes (migration milestone) | AG-STEP-004 | no | backend-architect | migration design doc; impl per slice | tests per slice | **no blind mass-rename**; identifies every impacted surface (seed, role-permission, JWT claims, HasPermission attrs, frontend nav, tests, audit); uses deprecated alias / compatibility map where needed; runtime migration is a separate controlled implementation milestone | clean migration |
| AG-STEP-005 | Tenant Role governed identity | EA: MOD-0018-FUxx **or** CAND-CAP — **EA DECISION** | runtime primitives exist (AuthService), no pack/ID | yes | AG-STEP-004; EA OD-A | no | module-pack-author | pack only | none | reserved ID + pack `ready-for-dev` | governed roles |
| AG-STEP-006 | `[HasPermission]` enforcement — two tracks (see below) | MOD-0018 | **006A done; 006B verified complete (AuthService + MDM already canonical-gated — audit PASS)** | no (pattern + retrofit) | AG-STEP-004 | A‖packs; B separate lane | backend-architect | controllers (per service) | per-service tests | see split below | uniform enforcement |
| AG-STEP-007 | Tenant Group + Group→Role | CAND-CAP REQUIRED — EA DECISION (no Blueprint RBAC Group ID) | **MISSING in code** | yes | AG-STEP-005; EA OD-B | no | module-pack-author → backend-architect | new pack + AuthService | tests | reserved identity + pack; **deferred from backend-start gate; retained as post-pilot / pre-production capability** | group-based grants |

**AG-STEP-006 split:**
- **006A — Mandatory rule for new business-domain modules:** every privileged endpoint is `[HasPermission]`-guarded from the first commit. **Required for the Business-Domain Start Gate.** **✅ completed in integration branch (commit `91a2604`)** — authored `.antigravity/rules/business-module-enforcement-standard.md` (**BME-001**, Part I): mandatory `[HasPermission("<module>.<resource>.<action>")]` rule, PKS-001 format, default-deny, tenant isolation, frontend-visibility-is-UX-only. **Not pushed / no PR / not merged to `main`.**
- **006B — Existing AuthService / MDM retrofit:** systematic hardening of currently-ungated endpoints. **Required before the Pilot Gate; required before production. Does not, by itself, block the first business-domain backend scaffold.** **✅ verified complete in integration branch — runtime retrofit not required (read-only audit @ `95a1b97`, PASS).** Genuine ungated privileged user-context endpoints in scope = **0**; stale/non-canonical permission keys = **0**.
  - **AuthService:** **16** privileged endpoints gated with **11** distinct canonical `auth.*` keys (`auth.users.*`, `auth.roles.*`); keys are **seeded + role-granted in-repo** via `Diten.AuthService.Persistence/Seed/DataSeeder.cs` (catalog + grants self-consistent). **4** internal service-to-service endpoints (`PlatformAuth.Provision/Sync`, `InternalEvents.TenantActivated/TenantAdminInvited`) are protected by `X-Internal-Api-Key` — `[HasPermission]` **N/A** (no user/permission claim). **2** self-service password endpoints (`Auth.ChangePassword`, `PlatformAuth.ForcedChangePassword`) operate only on the **caller's own identity** — permission gate **N/A**.
  - **MDM:** **6** Legal Entity endpoints gated with canonical `mdm.legal-entities.{read,create,update,delete}`; **Slice 1A hotfix intact** (commit `64417c2`); legacy `Modules.LegalEntity.*` usage = **0**.
  - **Scope:** 006B roadmap scope closed for **AuthService + MDM**; **DevEnablement not auto-included** (separate audit if required). Internal-API-key hardening is a separate **security-review** topic, **not a 006B permission gap**. **No runtime code changed; not pushed / no PR / not merged to `main`.** *(AG-STEP-008 stale roadmap row separately reported; not corrected in this commit.)*

### PHASE-03 — Data Scope Foundation & Real Resolver  *(critical path)*
| Plan Step | Capability / Module | Canonical ID | Current State | Pack Needed? | Dependencies | Parallel? | Owner Agent | Repo Scope | Build/Test Gate | Done Criteria | Unlocks |
|---|---|---|---|---|---|---|---|---|---|---|---|
| AG-STEP-008 | FU15 Real DataScopeResolver — pack | MOD-0018-FU15 (alias NEW-MOD-0041) | `planned`/reserved, no pack | yes | AG-STEP-001; MOD-0288 data (exists) | yes (read-only analysis now; pack after 001 merges) | module-pack-author | pack only | none | pack `ready-for-dev`; resolver shape + scope kinds locked | step 009 |
| AG-STEP-009 | FU15 Real DataScopeResolver — implementation (replace NoOp) | MOD-0018-FU15 | **implemented in integration branch** (commit `26d4fe7`; **not pushed / no PR / not merged to `main`**) | (impl) | AG-STEP-008; FU12 (done) | no | backend-architect | `Diten.Platform.Application/Authorization/OrgDataScopeResolver.cs` + Platform DI | **fail-closed integration test mandatory → met** | **DONE (integration branch).** Real `OrgDataScopeResolver` implemented; production DI `IDataScopeResolver → OrgDataScopeResolver` (Scoped); `NoOpDataScopeResolver` retained as test/dev seam, **not the production default**. FU12 hydrates real scopes (once-per-request, memoized, fail-safe). Resolver emits only `OrgUnit` (own + subtree, flat) / `Position` / `ManagerChain` (Position IDs) / `LegalEntity` (live MOD-0220 lookup-validation, fail-closed); invalid/expired/no assignment ⇒ no scope. **G4 integration audit: PASSED.** Tests: **528 passed, 0 failed**. **Blocker: none.** Merge-freeze: awaiting batch merge to `main`. | data-scope enforcement |

### PHASE-04 — Runtime Hardening, Cache Invalidation & Explain Access
| Plan Step | Capability / Module | Canonical ID | Current State | Pack Needed? | Dependencies | Parallel? | Owner Agent | Repo Scope | Build/Test Gate | Done Criteria | Unlocks |
|---|---|---|---|---|---|---|---|---|---|---|---|
| AG-STEP-010 | Cache-invalidation convention | MOD-0018-FU13 (**Permission Convention + Cache Invalidation Events**) | **pack `ready-for-dev` + runtime implementation completed in integration branch; integration audit PASS** (pack `ba13177`; runtime `3a8f9dd` Group A Platform per-instance fan-out · `a9ad416` Group B AuthService user-role revoke · `34e38cc` Group C AuthService role-permission revoke + server-side tenant-scoped holder seam). **Build/test:** Platform.API 0 errors, Platform.Application.Tests **557/0**, Platform.Eventing.Tests **56/0** (3 pre-existing skip); AuthService Application+Persistence+API 0 errors, AuthService.Application.Tests **30/0**. **Locked decisions (all met):** OD-FU13-01 access-token staleness **≤15 min** + revoke-on-removal (existing `RevokeAllByUserAsync` for user-role; new `GetUserIdsByRoleAsync` for role-permission; deny-list/event deferred); OD-FU13-02 **per-instance fan-out** (entitlement consumer only; `IMemoryCache` retained; Redis deferred); OD-FU13-03 **no cross-request scope cache** (resolver request-fresh). **Open rollout gate (NOT done):** RabbitMQ 2-instance fan-out verification (both local caches evict; no duplicate binding) — **mandatory before horizontal scaling**, not yet verified; integration-branch completion is not blocked by it. **Bounded fallback:** sequential fail-fast → after a per-user revoke failure some holder tokens may stay valid ≤15 min (grant not re-opened; earlier revokes not rolled back). **Perf:** holder query server-side filtered+projected; `{RoleId,TenantId,IsDeleted}` index optional, not written. **No open decision remains. Not pushed / no PR / not merged to `main`** (`main` still `d3ab4a4`). | yes | AG-STEP-009 | yes (pack ‖ 009) | module-pack-author → backend-architect | pack + Platform cache wiring | tests | scope/context caches evicted on change events | live scope changes |
| AG-STEP-011 | Explain Access (decision trace) | MOD-0018-FU14 | **pack `ready-for-dev` + self-explain v1 runtime COMPLETED in integration branch; post-fix integration audit PASS** (pack `7f1d005`; milestone `8c1d8fb`; self-explain-only scope `bc9435e`/`c150ac7`; runtime `a2445b8` Group A shared pure `PermissionClaimEvaluator` + `HasPermissionAttribute` delegation · `9ddb36f` Group B self-explain route + API-layer service + bounded DTO + audit · `bba038a` Group C remove combined `Allowed`, separate permission + scope observations). **Build/test:** Platform.API 0 errors; Platform.Application.Tests **603/0**; Platform.Eventing.Tests **56/0** (3 pre-existing skip). **Scope split:** AG-STEP-011 = Explain Access decision trace; **AG-STEP-012 = Allow Audit policy (separate EA `OD-D` — NOT complete)**; explain-request audit ≠ allow-decision audit. **v1 = self-explain only:** authenticated caller explains only its OWN access; **no caller-controlled `subjectUserId`** (subject from JWT `sub` + tenant context); route `GET api/platform/access/explain/me`, plain `[Authorize]`; tenant isolation from context; not a grant-inventory endpoint. **Side-effect boundary:** the observer does NOT call the real `HasPermissionAttribute` filter; it reuses the shared pure `PermissionClaimEvaluator` (canonical/legacy-alias/missing + bypass-platform/partner-admin); enforcement preserved byte-for-byte by regression. **Response model (RECONCILED):** TWO SEPARATE observations, NO combined `Allowed` verdict — `PermissionSatisfied` (permission-gate only; unaffected by scope/resolver/module) + descriptive `ScopeKinds`/`ScopeCounts`/`ScopeNotes`. **Empty scope is NOT a universal deny** (data-scope opt-in per resource, BME-001; platform/partner admins not row-scoped) → empty scope does not flip `PermissionSatisfied` (no false negatives). **No permissionKey↔moduleCode binding catalog** → independent observations; no binding claim, no catalog invented. **Token-freshness:** expiry + bounded refresh-required note only (no version/revocation/cache-debug). **Audit (honestly bounded):** success/app-deny/validation-deny/invalid-context/diagnostic-failure audited via `IAuditService.AppendAsync`; metadata dropped `allowed`, added `permissionSatisfied` + `scopeNotes`; Outcome = diagnostic?Failed:permissionSatisfied?Succeeded:Denied; audit-write failure never changes the result; pre-handler `[Authorize]` 401 not guaranteed FU14 audit (no new middleware seam). **Company wording CORRECTED:** `EntitlementDataScopeKind.Company` exists in the enum but the current `OrgDataScopeResolver` does not emit it; the observer projects only resolver output. **Reserved marker:** `platform.access.explain` RESERVED for the future cross-user route, NOT active on self-v1 (no seed/migration/alias-row). **Cross-user explain DEFERRED:** `OD-FU14-08B` (non-bypass extension) + `OD-FU14-09` (target effective-grants contract; cross-user data-scope-only alternative REJECTED; no AuthService contract invented) — both NOT v1 blockers, NOT complete, distinct from AG-STEP-012. `permission-key-unknown` reason deferred (no known-key catalog in the API layer). FU13 RabbitMQ horizontal-scaling rollout gate unchanged; Slice 5B/7 blockers unchanged; AG-STEP-008 stale row not fixed here. **Pushed to integration branch (`a2445b8`/`9ddb36f`/`bba038a`); no PR / not merged to `main` (origin/main `d3ab4a4`).** | yes | AG-STEP-009 | yes | module-pack-author → backend-architect | pack + Platform | tests | allow/deny provenance surfaced (basic flow) | debuggable access |
| AG-STEP-012 | Audit allow/deny policy | MOD-0018-FU14 / MOD-0021 | **OD-D policy LOCKED in integration branch — deny-only baseline** (policy in FU14 pack §17): existing deny logging preserved; **no allow-decision audit runtime implementation** (no new sink/flag/dedup/package/event-contract). **Rationale:** the existing `IAuditService.AppendAsync` async-outbox pipeline could log allows, but ≈174 `[HasPermission]` enforcement points (Platform 151 · AuthService 17 · MDM 6, many read/list/query) make all-allows high-volume; production QPS/storage metrics not in-repo. **all-allows rejected as baseline; sampled/configurable + bypass-only deferred.** **Future narrow allow-audit pilot DEFERRED** (Platform-first, high-signal only — privileged-mutating + `platform_admin`/`partner_admin` bypass allows — behind a feature flag, via the existing async sink, bounded metadata; gated on production volume/retention metrics + AuthService/MDM sink architecture decision). Internal API-key successes (not user allows) + EnterpriseStrategy (Slice 5B blocker) out of scope. **Explain-request audit (AG-STEP-011/FU14) remains distinct** from allow-decision audit; FU14 completion does not complete this. **No PR / merge; main unchanged (origin/main `d3ab4a4`).** | no (decision) | AG-STEP-011; EA OD-D | yes | read-only-auditor | policy doc | none | **policy chosen: deny-only baseline** (allow logging not started; volume control = deferred follow-up; explain-request audit separate) | audit volume control |

### PHASE-05 — Business-Module Row Enforcement Baseline
| Plan Step | Capability / Module | Canonical ID | Current State | Pack Needed? | Dependencies | Parallel? | Owner Agent | Repo Scope | Build/Test Gate | Done Criteria | Unlocks |
|---|---|---|---|---|---|---|---|---|---|---|---|
| AG-STEP-013 | Business-module enforcement contract (§9) | MOD-0018 (standard) | **completed in integration branch** (commit `45561e7`; **not pushed / no PR / not merged to `main`**) | no (standard doc) | AG-STEP-009, 006A, 004 | yes | backend-architect | `.antigravity/rules/business-module-enforcement-standard.md` (BME-001 Part II) | none | **DONE (integration branch).** BME-001 extended with the uniform business-module enforcement contract (C1–C8): `[HasPermission]` action gate; scoped-resource **explicit opt-in** consuming existing `ITenantAuthorizationContext` / `EffectiveScopes` (**no module-authored resolver**); **empty scope ⇒ fail-closed**, auto-open forbidden; tenant isolation server-side only; row filters limited to `OrgUnit`/`LegalEntity`/`ManagerChain`/`Position`; permission vs data-scope separation; frontend-visibility-is-UX-only; audit/Explain (011/FU14) + cache (010/FU13) referenced. **Repo-grounded reference pattern** (request → permission gate → tenant filter → EffectiveScopes filter → empty-scope deny → response) documented. | uniform business modules |
| AG-STEP-014 | First row-level scope consumer (pilot) | pilot business-domain module | none consume scope yet | yes | AG-STEP-013; G4 (009) | no | backend-architect | one pilot module | enforcement tests | one module filters rows by `EffectiveScopes`, opt-in. **Sequenced after business-domain backend start, before the Pilot Gate (mandatory).** | pattern proven |

### PHASE-06 — Workflow, Temporary Access & Process Context
| Plan Step | Capability / Module | Canonical ID | Current State | Pack Needed? | Dependencies | Parallel? | Owner Agent | Repo Scope | Build/Test Gate | Done Criteria | Unlocks |
|---|---|---|---|---|---|---|---|---|---|---|---|
| AG-STEP-015 | Temporary access binding | MOD-0018-FU11 | `NoOpTemporaryAccessProvider` | yes | MOD-0023/0024 emit; AG-STEP-009 | yes (pack) | module-pack-author → backend-architect | pack + Platform | tests | time-bound grants consumed, expiry-revoked, no persistent role writes | process access |
| AG-STEP-016 | Process-context authorization wiring | MOD-0023 / MOD-0024 | planned | yes | AG-STEP-015 | no | backend-architect | workflow/task emit | tests | Permission + Process Context decision path | BPM access |

### PHASE-07 — Enterprise Extensions (Blueprint-canonical, registry reservation required)
| Plan Step | Capability / Module | Canonical ID | Current State | Pack Needed? | Dependencies | Parallel? | Owner Agent | Repo Scope | Build/Test Gate | Done Criteria | Unlocks |
|---|---|---|---|---|---|---|---|---|---|---|---|
| AG-STEP-017 | Field/Row Security & Masking | **MOD-0019** (Blueprint OK; **not in registry**) | not started | yes (+ reservation) | AG-STEP-009 | yes | module-pack-author | registry row + pack | none | reserved + pack; impl in modules that need it | field-level security |
| AG-STEP-018 | Segregation of Duties (SoD) | **MOD-0020** (Blueprint OK; **not in registry**) | not started | yes (+ reservation) | AG-STEP-005 | yes | module-pack-author | registry row + pack | none | reserved + pack | SoD controls |
| AG-STEP-019 | Territory Management (Data-Scope target) | **MOD-0151** (Blueprint OK; **not in registry**; CRM Core) | not started | yes (+ reservation) | AG-STEP-009 | yes | module-pack-author | registry row + pack | none | reserved + pack; modeled as scope target | territory scoping |
| AG-STEP-020 | Delegation / Substitution / Emergency Access | CAND-CAP REQUIRED — EA DECISION (no Blueprint ID) | not started | yes | EA | yes | module-pack-author | pack | none | reserved candidate + pack | break-glass / delegation |

### PHASE-08 — Business-Domain Unlock & Rollout
| Plan Step | Capability / Module | Canonical ID | Current State | Pack Needed? | Dependencies | Parallel? | Owner Agent | Repo Scope | Build/Test Gate | Done Criteria | Unlocks |
|---|---|---|---|---|---|---|---|---|---|---|---|
| AG-STEP-021 | Business-Domain Start Gate verification | — | **integration-branch PASS** (audit HEAD `d55a564`); **main unlock PENDING BATCH MERGE** | no (audit) | AG-STEP-000, 001, 004, 009, 013, 006A | — | read-only-auditor | read-only | none | **DONE (integration branch).** All Backend Start Gate criteria verified against live repo @ `d55a564`: AG-STEP-000 ✓, 001 (MOD-0288 + FU01 `done`) ✓, 004 (PKS-001 OD-C locked) ✓, 009 (`IDataScopeResolver → OrgDataScopeResolver`; NoOp not prod default) ✓, 013 (BME-001 Part II contract) ✓, 006A (BME-001 Part I rule) ✓, tenant isolation (`ExecutionFilter` tenant+soft-delete; server-side `tenant_id` claim; BME-001 C4 "no TenantId from request") ✓. **`main` still `d3ab4a4`; integration branch is local-only (not pushed). Real unlock gated on batch merge — not unlocked on `main`.** | business-domain backend start |
| AG-STEP-022 | **Blueprint-selected business-domain module rollout** | Blueprint-selected at runtime | not started | yes (each) | AG-STEP-021 | yes (separate modules/worktrees) | per-domain bootstrap | per business-domain | full | each module follows §9 contract. *The first business-domain module is not hardcoded in this plan. the execution chat selects it later using Blueprint priority, dependency readiness, canonical ID verification and approved/ready-for-dev module-pack status.* | business value |

> *Business-domain examples include CRM, HR, Sales, Procurement, Finance, Inventory, Warehouse, Project, Asset and Service modules. The actual next module is selected later from the Blueprint after live dependency, canonical ID and module-pack readiness checks. This plan does not preselect CRM or HR as the mandatory first domain.*

---

## 6. Critical Path

```
AG-STEP-000 (persist + approve plan)            — completed (PR #26)
AG-STEP-001 (MOD-0288/FU01 governance closure)  — completed (PR #25)

Remaining critical path:
AG-STEP-004 (lock permission-key convention)
 -> AG-STEP-008 (FU15 resolver pack)
 -> AG-STEP-009 (FU15 real resolver, replace NoOp)   [G4]
 -> AG-STEP-013 (business-module enforcement contract)
 -> AG-STEP-021 (Business-Domain Start Gate)
 -> business-domain backend start
```

**AG-STEP-014** (first row-level scope consumer pilot) is **after business-domain backend start and before the Pilot Gate — mandatory.**

**Non-critical / parallel:** AG-STEP-002 (DCP-002), 003 (MOD-0220 verify), 004B (key migration), 006A/006B (enforcement rule + retrofit), 010 (cache), 011/012 (Explain/audit policy), 015/016 (temporary/process), 017/018/019/020 (extensions), 005/007 (Tenant Role/Group governance).

---

## 7. Parallel Work Matrix

| Workstream | Can Run With | Cannot Run With | Separate Worktree? | Reason |
|---|---|---|---|---|
| AG-STEP-001 governance closure | read-only analyses | 002/005/008/017–019 (registry/DCP) | yes (open) | shared governance files |
| AG-STEP-002 DCP-002 promote | 003, 006B, read-only | 001 (registry overlap) | yes | governance file |
| AG-STEP-004B key migration | 006B design | 004 still open | yes | depends on locked convention |
| AG-STEP-006A new-module rule | 008/010/011 packs | — (doc/pattern) | n/a | rule only |
| AG-STEP-006B retrofit | 010/011 packs | 009 if same service files | yes | code lane |
| AG-STEP-008 FU15 pack | 006, extension packs | 001 (registry/DCP), 009 | yes | pack + registry |
| AG-STEP-009 FU15 impl | 010/011 packs (doc) | 006B if same files; 014 | yes (critical lane) | core resolver |
| AG-STEP-017/018/019 reservations | each other only if one writer serializes registry | 001/008 (registry) | yes | all touch registry |

**Two-AI recommendation:**
- **Lane A (critical, write):** AG-STEP-000 → 001 → 004 → 008 → 009 → 013 → 021. One write-capable AI, one worktree/branch at a time.
- **Lane B (parallel, write, separate worktree):** AG-STEP-006B retrofit, then 010/011 packs + Explain Access — only when not touching Lane A's files.
- **Hard rules:** never two write-capable AIs in one worktree; never two steps editing the **same** governance file (registry / DCP-001 / master-plan) in parallel — serialize through Lane A.

---

## 8. Business-Domain Start Gate

*Business-domain examples include CRM, HR, Sales, Procurement, Finance, Inventory, Warehouse, Project, Asset and Service modules. The actual next module is selected later from the Blueprint after live dependency, canonical ID and module-pack readiness checks. This plan does not preselect CRM or HR as the mandatory first domain.*

Three thresholds:

### Backend Start Gate (business-domain backend development may begin)
- AG-STEP-000 — plan approval
- AG-STEP-001 — MOD-0288 / FU01 closure
- AG-STEP-004 — permission-key convention locked
- AG-STEP-009 — real DataScopeResolver active _(met on integration branch `26d4fe7`, G4 PASSED; not yet merged to `main`)_
- `NoOpDataScopeResolver` removed from production DI _(done on integration branch; demoted to test/dev seam, not the production default)_
- AG-STEP-013 — business-module enforcement contract
- mandatory `[HasPermission]` for new business-domain modules (AG-STEP-006A)
- tenant isolation verified

> **Gate result (AG-STEP-021, audit @ `d55a564`):** **integration-branch PASS** — every Backend Start Gate
> criterion above is verified present on `feature/governance/access-governance-execution`.
>
> **Main unlock: `PENDING BATCH MERGE`.** `main` is still `d3ab4a4` and contains none of these commits; the
> integration branch is **local-only (not yet pushed to origin)**. On `main` the production `IDataScopeResolver`
> default is still `NoOpDataScopeResolver` and BME-001 is absent. The gate is therefore **met-pending-merge**, not a
> live `main` unlock.
>
> **Rollout policy:**
> - **Business-domain production rollout does NOT start until the integration branch is merged to `main`.** Building
>   on `main` today would run against `NoOpDataScopeResolver` (empty scopes) with no BME-001 — unsafe.
> - On the integration branch the substrate exists, so **preparation / scaffold work may proceed only by explicit
>   user/EA decision** under the merge-freeze — it is **not** an automatic unlock.
> - **Do not auto-start any business-domain rollout.** Module selection (AG-STEP-022) remains a later, explicit step
>   from the Blueprint after merge.

### Pilot Gate
- AG-STEP-006B — existing AuthService/MDM endpoint retrofit complete
- AG-STEP-010 — cache invalidation
- AG-STEP-011 — Explain Access basic flow
- AG-STEP-014 — first row-level scope consumer pilot
- audit policy chosen (AG-STEP-012)

### Enterprise Production Gate
- Tenant Role governance (AG-STEP-005)
- Tenant Group / Group→Role (AG-STEP-007) **or an explicitly documented defer decision**
- field masking (MOD-0019) in modules that need it
- SoD (MOD-0020) in modules that need it
- Territory (MOD-0151) in modules using it
- temporary / process access (FU11) in flows that need it
- cross-tenant response policy compliance (OD-H)

| Gate Item | Threshold | Reason | Deferrable? |
|---|---|---|---|
| AG-STEP-000/001/004/009/013/006A + tenant isolation | Backend Start | build only on accurate, enforced foundation | No |
| 006B retrofit, 010 cache, 011 Explain, 014 pilot, 012 audit policy | Pilot | prove the pattern + harden existing surface | until pilot |
| Tenant Role, Tenant Group(or defer), MOD-0019/0020/0151, FU11, 404 policy | Production | enterprise-grade controls | until production |

---

## 9. Business Module Enforcement Contract

Applies to **every business-domain module** (CRM, HR, Sales, Procurement, Finance, Inventory, Warehouse, Project, Asset, Service, …). Each MUST:

1. **Permission key standard** — single locked convention (AG-STEP-004); action-scoped keys; no ad-hoc formats.
2. **`HasPermission`** — every privileged endpoint carries `[HasPermission("<module>.<resource>.<action>")]`; default-deny; nothing ships ungated (AG-STEP-006A).
3. **Data Scope opt-in** — module **explicitly** opts into scope filtering; never auto-opened. Consumes `EffectiveScopes`; never builds its own resolver/scope storage.
4. **Action-aware checks** — read/update/approve/export/bulk independent (pattern exists).
5. **Tenant isolation** — `TenantId` only from `ITenantContext` server-side; never from body/DTO/query; reads filter by `TenantId`, writes set it server-side.
6. **Row-level filter** — apply resolved scopes (org-unit / legal-entity / own / assigned / manager-chain), fail-closed on empty scope for a scoped resource.
7. **Audit event** — privileged + denied ops emit audit (→ MOD-0021) via the entitlement sink.
8. **Explain trace** — expose decision provenance via FU14 once available (required at production).
9. **Cache invalidation hook** — subscribe to FU13 convention; no independent scope cache.
10. **Frontend visibility is UX only** — backend enforcement is authoritative; UI hiding never substitutes for `[HasPermission]` + scope filtering.
11. **No raw permissions on User; no permissions on Position** — grants flow Role→User only.
12. **Process-context extension** — time-bound/process access via FU11/process-context; never persistent role grants for temporary needs.

---

## 10. Governance Preparation Backlog

| Capability | Blueprint ID (layer A) | Registry Status | Pack Status | EA Decision? | Recommended Action |
|---|---|---|---|---|---|
| Tenant Role | none (RBAC = MOD-0018) | absent | none | **Yes** | allocate `MOD-0018-FUxx` (pref.) or CAND-CAP; pack (AG-STEP-005) |
| Tenant Group / Group→Role | none (Group only in MOD-0285 nav) | absent | none | **Yes** | CAND-CAP — EA DECISION; **defer from backend-start, retain pre-production** (AG-STEP-007) |
| Permission Catalog convention | MOD-0018 scope | n/a | n/a | **Yes** | lock format + catalog (AG-STEP-004); migration plan (AG-STEP-004B) |
| Real DataScopeResolver | MOD-0018-FU15 | reserved/`planned` | none | No | pack + implement (AG-STEP-008/009) |
| Cache-invalidation convention | MOD-0018-FU13 | `planned` | none | No | pack + wire (AG-STEP-010) |
| Explain Access | MOD-0018-FU14 | `planned` | none | OD-D | pack + implement (AG-STEP-011/012) |
| Temporary access binding | MOD-0018-FU11 | `planned` | none | No | pack + implement (AG-STEP-015) |
| Field/Row Security & Masking | **MOD-0019** (gate OK) | **absent** | none | No (reserve) | reservation + pack (AG-STEP-017) |
| Segregation of Duties | **MOD-0020** (gate OK) | **absent** | none | No (reserve) | reservation + pack (AG-STEP-018) |
| Territory Management | **MOD-0151** (gate OK) | **absent** | none | No (reserve) | reservation + pack (AG-STEP-019) |
| Tenant User Foundation | CAND-CAP-0001 (squats MOD-0047=BCM) | present | `done` (slice) | Yes (final EA ID) | keep candidate; EA assigns Blueprint ID later |
| Legal Entity contract | MOD-0220 | present | `ready-for-dev` | No | **verified present both sides (AG-STEP-003 complete)** — read-only lookup-validation contract confirmed; no follow-up pack needed |
| Business-module row enforcement | MOD-0018 standard | n/a | n/a | No | contract + pilot (AG-STEP-013/014) |
| DCP-002 canonicalization | n/a | n/a | **`approved`** (AG-STEP-002, integration branch; not yet merged to `main`) | No | promoted `draft → approved` (commit `3b9d342`) |

---

## 11. Execution Operating Model

Run the whole plan as a **single long-lived execution chat**:

- **01 — ACCESS GOVERNANCE EXECUTION** (single long-lived chat): executes steps sequentially in **one** module branch at a time; commits only when a module is fully done (owner policy: never commit to `main`; one branch per module).
- **Parallel independent work** (only when genuinely independent): use a **separate worktree + separate branch + separate temporary chat**, never touching the main lane's open governance files.
- **Live preflight is mandatory before every AG-STEP** (see the Live State Refresh Rule above).
- **Stage, commit, push, PR and merge happen only with explicit user approval.**

Drive steps with:
```
PLAN STEP START:    AG-STEP-XXX
PLAN STEP COMPLETE: AG-STEP-XXX
PLAN STEP BLOCKED:  AG-STEP-XXX
```
Open a **new** temporary chat only for: a separate worktree/branch lane, a real scope change, an integration audit, or a different capability phase.

---

## 12. Per-Step Task Template

```
Plan Step:           AG-STEP-XXX
Live preflight:      <Live State Refresh Rule output — branch/HEAD/origin-main/status/staged/unstaged/untracked/worktrees/branches/pack status/deps/concurrent-agent risk>
Canonical ID:        <layer-A verified via verify_module_id.py --check-id … --name …; or CAND-CAP REQUIRED — EA DECISION>
Purpose:             <one line>
Dependencies:        <AG-STEP-… / gates>
Current branch:      <module branch; never main>
Worktree:            <dedicated; never shared with another write-capable AI>
Allowed paths:       <layer-B explicit list>
Protected paths:     <unrelated services, other packs, DCP-002, control-tower doc, etc.>
Agent / Workflow:    <read-only-auditor | module-pack-author | backend-architect | …>
Stop conditions:     <live-state contradiction; canonical gate fail; before any stage/commit/push/PR/merge>
Completion criteria: <measurable>
Required handoff:    <§13 format back to ACCESS GOVERNANCE EXECUTION>
```

---

## 13. Handoff Template

```
Plan Step:                AG-STEP-XXX
Module / Capability:
Canonical ID:
Purpose:
Live preflight summary:
Branch:
Worktree:
Base HEAD:
Final HEAD:
Changed files summary:
Build:
Tests:
Audit:
Working-tree state:
Staged diff:
Commit:
Push:
PR:
Merge:
Main sync:
Deferred follow-ups:
Blockers:
Next unlocked steps:
Final status:  COMPLETED | USER APPROVAL REQUIRED | BLOCKED  — RETURN TO ACCESS GOVERNANCE EXECUTION
```

---

## 14. Open Decisions (user / EA only)

| Decision ID | Question | Options | Recommended | Blocks |
|---|---|---|---|---|
| OD-A | Tenant Role governed identity | (a) `MOD-0018-FUxx`; (b) CAND-CAP; (c) wait | **(a) MOD-0018-FUxx** | AG-STEP-005, 007, 018 |
| OD-B | Tenant Group / Group→Role identity | (a) CAND-CAP; (b) MOD-0018-FUxx; (c) defer | **defer from minimum backend-start gate; retain as post-pilot / pre-production capability** (canonical ID still needs EA) | AG-STEP-007 |
| OD-C | Permission-key convention | (a) `module.resource.action` lowercase-dotted; (b) `Modules.X.Y` PascalCase | **(a)** — majority + REST-friendly; migrate `Modules.*` | AG-STEP-004/004B, all business modules |
| OD-D | Audit allow policy (OD-9) | (a) deny-only; (b) all allows; (c) sampled | **(a) deny-only baseline** | AG-STEP-012, FU14 |
| OD-E | Manager-chain depth (OD-4) | (a) bounded (e.g. 5); (b) unbounded | **(a) bounded** | AG-STEP-008/009 |
| OD-F | Business-country SoR (OD-5) | (a) MDM owns; (b) defer | **(a) MDM owns; never PSS-011/MOD-0048 default** | AG-STEP-003, MOD-0288 scope |
| OD-G | MOD-0019/0020/0151 registry reservation timing | (a) now; (b) at PHASE-07 | **(b)** — keep registry lean | AG-STEP-017/018/019 |
| OD-H | Cross-tenant response code | (a) 404; (b) keep 403 | **(a) 404** | production gate |
| OD-I | Final EA Blueprint ID for Tenant User (CAND-CAP-0001 squats MOD-0047=BCM) | (a) assign new MOD; (b) keep candidate | **(a) assign** before broader Tenant IAM | broader Tenant IAM |

---

## 15. Final Exit Criteria

Access Governance Completion is reached when:
1. Role→Permission and User→Role are **governed** (Tenant Role pack/ID exists; no ungoverned primitives).
2. The **real DataScopeResolver is active** and `NoOpDataScopeResolver` is **removed from production DI** (`ITemporaryAccessProvider` likewise once FU11 lands).
3. The **business-module enforcement contract is verified** by at least one pilot consuming `EffectiveScopes` with fail-closed row filtering.
4. **Cache invalidation** evicts scope/context on change events (FU13).
5. **Audit trace + Explain Access** (FU14) surface allow/deny provenance per the chosen policy.
6. **Permission-key convention** is uniform, a migration plan exists (004B), and `[HasPermission]` is universal across services.
7. **Business-Domain Start Gate** passes (business-domain backend may begin); Pilot and Enterprise Production gates tracked.
8. Governance drift-free: MOD-0288/FU01 `done`, DCP-002 `approved`, MOD-0019/0020/0151 reserved when scheduled, registry ↔ master-plan ↔ packs ↔ DCP consistent.

---

## Backtrack Analysis

1. **MOD-0220 must change?** No. `ready-for-dev`, MDM-owned; MOD-0288 only consumes its read-only `LegalEntityId` contract. AG-STEP-003 **verifies** the contract first and only authors a narrow follow-up if it is genuinely absent — never implementation on assumption.
2. **MOD-0288 must change?** Not the runtime. Only **governance status** (`ready-for-dev → done`) closure (AG-STEP-001).
3. **Tenant User lookup-validation must change?** No. CAND-CAP-0001 first slice `done`. Only a future EA Blueprint-ID reassignment (OD-I) — governance.
4. **Beyond FU01 closure, more fixes?** No runtime fix for FU01. Adjacent gaps: real resolver (FU15), universal `[HasPermission]` (006A/B), permission-key convention (004/004B).
5. **Governance-only work?** AG-STEP-000, 001, 002, 003 (if contract exists), 004, 005/007 reservations, 012, 017–020 reservations, OD-* decisions.
6. **New runtime implementation?** AG-STEP-004B (migration), 006B (retrofit), 009 (resolver), 010 (cache), 011 (Explain), 014 (pilot), 015/016 (temporary/process), PHASE-07/08 impls.
7. **Blocks business-domain backend start?** Backend Start Gate items: AG-STEP-000, 001, 004, 009, 013, 006A + tenant isolation. Nothing else blocks **backend start**.
8. **Deferrable as enterprise hardening?** Pilot-gate items (006B, 010, 011, 014, 012) before pilot; production-gate items (Tenant Role, Tenant Group/defer, MOD-0019/0020/0151, FU11/016, cross-tenant 404) before production.

---

## Final Gate Summary

1. **Backend Start Gate** — AG-STEP-000 (+approval), 001, 004, 009 (NoOp removed), 013, 006A, tenant isolation → **business-domain backend development may begin** (module chosen later from the Blueprint).
2. **Pilot Gate** — AG-STEP-006B retrofit, 010 cache invalidation, 011 Explain Access, 014 first row-level pilot, 012 audit policy.
3. **Enterprise Production Gate** — Tenant Role governance, Tenant Group/Group→Role (or documented defer), MOD-0019 (where needed), MOD-0020 (where needed), MOD-0151 (where used), FU11 temporary/process access, cross-tenant response policy.

**Recommended first execution command:**
```
PLAN STEP START: AG-STEP-004
```

**Final verdict:** `ACCESS GOVERNANCE PLAN READY FOR FINAL REVIEW`

---

## Appendix — Gaps/errors found in the source (ChatGPT) prompts

1. Treated MOD-0019/0020/0151 as risky/unverified — they are **Blueprint-canonical and pass `verify_module_id.py`**; only **absent from the registry** → reservation, not EA invention.
2. Listed "Field Security" and "Masking" as separate workstreams — Blueprint **MOD-0019 covers both**.
3. Assumed Tenant Group / Group→Role as a given foundation — **no Blueprint RBAC Group ID, no code today** → CAND-CAP/EA; now **deferred (not dropped)** to pre-production.
4. Missed the permission-key inconsistency (4 styles) → now a hard Backend-Start prerequisite (AG-STEP-004) + dedicated migration step (AG-STEP-004B, no blind rename).
5. Did not flag that `[HasPermission]` is non-universal → split into 006A (new-module mandatory) and 006B (existing retrofit).
6. Did not account for DCP-002 being `draft` while treated as canonicalization authority → AG-STEP-002.
7. Over-weighted data-scope integration risk — the JWT context seam already consumes the resolver; FU15 lights up a wired context (fail-closed test still mandatory).
8. "Don't create the file" + a large doc → plan lived only in scratch; addressed by AG-STEP-000 (persist via a separate governance write) — **now completed: persisted to `execution/portfolio/access-governance-completion-plan.md` and merged via PR #26.**
9. Implied `[HasPermission]` everywhere is already the rule — audit shows frontend does **zero** permission gating; §9 makes frontend visibility UX-only and backend enforcement mandatory.
10. Preselected CRM/HR as the first domain — generalized to **business-domain**; the first module is Blueprint-selected later (AG-STEP-022) and CRM/HR are examples only.
