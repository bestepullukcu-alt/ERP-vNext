# Module Implementation Status (Code-Truth)

## Purpose
Developer-facing implementation tracker: for each **code-bearing** module, its real status **derived from code** (not from docs/registry claims). Lets any developer see — before picking up work — what is built, how complete it is, and what is missing.

## Relationship to the Module ID Registry
This file is **separate from** [`module-id-registry.md`](module-id-registry.md) **on purpose**. The Module ID Registry is an **identity** registry and, by its own charter, **must never contain completion percentages or "what's done / what's missing" lists**. So all progress/status tracking lives **here** instead. The registry remains the authority for IDs/names/slugs; this file is the authority for **implementation state**.

## How to read
- **Durum** (one of): `Bitti` · `Backend+Frontend` · `Sadece-backend` · `Kısmi` · `Başlanmadı`.
- **%** is a rough **estimate** (not exact), as agreed — paired with an explicit **eksik** (what's missing) list so it's actionable.
- **Source:** code audit of `services/**`, `frontend/Diten.Web/**`, `gateway/**` (entities, controllers, handlers, tests, views, js, `.resx`). Audit date: **2026-06-23**.
- **Scope:** only modules that have code today. Not-yet-started modules from the Blueprint are NOT listed here (their IDs live in the registry / Excel `Blueprint_Data`).

---

## Platform & Shared Services — Tenant / Subscription / Catalog

| Module ID | Durum | % | Var olan | Eksik |
|---|---|---|---|---|
| MOD-0009 Tenant / Environment Mgmt (+FU01/02/03) | Backend+Frontend | 95 | Full lifecycle entity+CQRS (Register/Update/Suspend/Reactivate/Delete), 4-status model, admin-invite; Tenants UI (Index/Create/Details 4-tab/Edit/Security); tests | Per-tenant quota override form (read-only today); minor |
| MOD-0008 Enterprise Capability / Product Catalog | Backend+Frontend | 100 | ModuleCatalogItem assignable/expose, domain/service/version, search; catalog UI exposes all metadata | — |
| CAND-CAP-0002-FU01 Tenant Module Catalog | Backend+Frontend | 100 | ModuleCatalogItem + ModulePageDescriptor + actions; full CRUD UI; permission-sync; tests | — |
| CAND-CAP-0002-FU02 Subscription Plan Catalog | Backend+Frontend | 100 | SubscriptionPlan (pricing/trial/quotas/features); full CRUD UI; l10n | — |
| CAND-CAP-0002-FU03 Subscription Feature Mgmt | Backend+Frontend | 100 | FeatureDefinition/Category/PlanFeatureMapping; two-tab UI (R2); archive; l10n | — |
| CAND-CAP-0002-FU04 Module Assignment Inspection | Kısmi | 95 | Read-only inspection API (overview/plans/tenants/detail); shown in module Details | No dedicated "all assignments" admin page; read-only only |
| CAND-CAP-0002-FU05 Tenant Module Entitlements | Backend+Frontend | 100 | TenantModuleEntitlement (source/expiry/effective-access) + entitlement→bridge; Commercial tab UI; tests | — |
| MOD-0033 API Consumer & Credential / Quota Model (+FU01) | Kısmi | 80 | Quota engine (QuotaEvent/Usage, reset job, init/sync); read-only Tenant Quota Governance tab/proxy exists in Tenant Details; local permission fix `d1ecd9c5` aligns Web proxy and Platform quota read endpoints on canonical `platform.tenants.quotas.read`; post-fix Platform/Gateway read-only smoke PASS-with-gaps on 2026-08-07 with empty quota data | Authenticated Web-cookie proof for `/Platform/Tenants/{tenantId}/QuotaStatus` still pending; no positive quota-row render proof because safe tenant had no quota rows; restricted non-bypass proof skipped; **quota override/admin mutation UI is outside FU01 and not implemented**; **API Consumer/credential mgmt NOT implemented** (quota-only) |

## Platform & Shared Services — Admin / Org / Infra

| Module ID | Durum | % | Var olan | Eksik |
|---|---|---|---|---|
| CAND-CAP-0003 Platform Administration (+FU01/02) | Backend+Frontend | 85 | PlatformAdministrator entity; 11 API endpoints (invite/suspend/reactivate/roles/reset); full admin UI + 7-locale l10n; rules tests | MFA/TOTP setup UI; password-policy UI; hardcoded TR error strings in controller (not resx); thin handler/integration tests |
| MOD-0288 Organization, Person & Position Directory (+FU01) | Backend+Frontend | 85 | OrgUnit/Position/PositionAssignment entities + 3 controllers (CRUD + manager-chain) + cross-tenant validators; **tenant-side frontend (2026-06-23): 3 CRUD screens in tenant shell** (Views/Organization/*), nav "Organization" group, manager-chain UI. Endpoints opened to `tenant_user` actor (gateway + Platform.Common middleware exclude org paths from admin-only). **Full e2e live-proven**: LE→OrgUnit→Position→reports-to→manager-chain→assignment. | Handler/CQRS unit tests still thin; org-tree (hierarchical) view is flat DataTable for now. (was the audit's "registry done / no UI" drift — now closed.) |
| MOD-0048 Reference Data Management | Backend+Frontend | 90 | Rich entities (sets/versions/values/mappings, draft→approve→publish governance); full UI (12+ views, wizards); 7-locale l10n; audit redaction | No dedicated ReferenceData test suite (lifecycle/approval/concurrency untested) |
| MOD-0026 Scheduler / Job Orchestration | Sadece-backend | 70 | Hangfire scheduler + IBackgroundJobScheduler, JobExecutionLog, audit outbox worker; 6 test files | **No UI** (Hangfire dashboard at /jobs not integrated into nav); no job-mgmt/retry/history UI |
| MOD-0027 Notification Service (+FU02 Template UI, +FU03 Event Catalog) | Bitti | 100 | Templates/Dispatch/Settings entities; 10+ API endpoints + render-preview + dispatch list filters; SMTP provider; async dispatch job; Platform Admin UI (FU02) — NotificationTemplates/NotificationSettings (compact) + NotificationDispatches (read-only/list-detail) proxy controllers/views/JS; en+tr resx; 4 PSS-011 lookup keys; sidebar nav; 7+ test files (1112 passing). **FU02 live smoke PASS (2026-07-08)** — see [FU02 smoke audit](../../docs/audits/pss-mod-0027-fu02-notification-template-ui-smoke-2026-07-08.md). **FU03 Notification Event Catalog & Template Binding — Bitti (PASS-with-note, 2026-07-08):** `NotificationEventDefinition` entity + `ModuleManifestDocument.NotificationEvents` additive contract (integration-agent PASS, guardrails) + manifest sync/validation service + `NotificationEventsController` (events read/manage, template-slots) + read-only `/Platform/NotificationEvents` UI + `platform.notifications.events.read/manage` perms; **1139/1139 tests**; live smoke PASS (sync `providersScanned:6, eventsDeclared:0` controlled empty); see [FU03 smoke audit](../../docs/audits/pss-mod-0027-fu03-notification-event-catalog-smoke-2026-07-08.md) | Optional follow-ups (non-blocking): FU02 visual browser confirmation / restricted-actor 403 seed; **FU03 producer opt-in** (declare `notificationEvents` in Workflow/invite/campaign manifests for a populated catalog); FU03 per-event persisted validation issues; FU02 template UI consuming active-template-slots. Out of scope: FU05 tenant self-service UI, FU04 InApp/SignalR, SMS/Push (MOD-0263); DataTable verifier lacks read-only/list-detail mode (tooling follow-up). **FU03A Notification Event SourceType & PlatformSeed Bridge — Bitti (PASS-with-note, 2026-07-08):** `NotificationEventSourceType {Manifest=0,PlatformSeed=1,SystemSeed=2}` + additive `NotificationEventDefinition` fields (SourceType/OwnerArea/OwnerDisplayName/TargetRoute/ModuleCatalogRef/RequiredPolicy) + manifest-sync clobber guard (create writes Manifest; PlatformSeed/SystemSeed not clobbered) + generic `NotificationEventSeedPlanner`/`NotificationEventSeeder` + **empty** `NotificationEventSeedCatalog` + startup `NotificationEventSeed.EnsureSeededAsync` (no-op) + additive read-only DTO/mapping/UI; **1148/1148 tests** (10 new); fleet live-boot-clean smoke (Web 302, API 403, seed no-op, no 500). No tenant content, no runtime adapter, no Module Catalog/ModulePages/PlatformNavigationCatalog/Gateway side-effect; see [FU03A smoke audit](../../docs/audits/pss-mod-0027-fu03a-notification-event-sourcetype-platformseed-bridge-smoke-2026-07-08.md). Note: authenticated DTO/UI round-trip not exercised live (compensated by tests). **FU04A Tenant Management Notification Event Opt-in — Bitti (PASS-with-note, 2026-07-08):** 3 tenant events (`tenant.user.invited` / `tenant.lifecycle.suspended` / `tenant.lifecycle.reactivated`) added to `NotificationEventSeedCatalog.PlatformSeedDefinitions` as **PlatformSeed** (RequiredPolicy=PlatformActor, RequiredPermissionKey=null, TargetRoute=/Platform/Tenants, OwnerModuleId=MOD-0009) bound to existing FU02 templates; **live-seeded Active** (Mongo-verified, exactly 3, no duplicate); **1153/1148→1153 tests** (5 new). No runtime adapter, no Module Catalog/ModulePages/PlatformNavigationCatalog/Gateway side-effect, no IModuleManifestProvider, no new template; see [FU04A smoke audit](../../docs/audits/pss-mod-0027-fu04a-tenant-management-notification-event-opt-in-smoke-2026-07-08.md). Note: authenticated API JSON list/slots fetch not exercised live (Mongo persist + unit tests compensate). **FU04B (runtime eventCode → dispatch adapter) + tenant producer migration + manifest-driven workflow/document/import opt-in remain follow-ups.** (Foundation+content on MOD-0027-FU03A — not its own code-truth row per this file's charter.) **FU04B EventCode Dispatch Adapter — Bitti (PASS-with-note, 2026-07-08):** `INotificationEventDispatchAdapter` (eventCode→Active event→defaultTemplateKey→requiredVariables/recipient validation→delegate to existing `QueueEmailNotificationCommand`, returns `Response<NotificationDispatchDto>` unchanged) + thin `DispatchNotificationByEventCodeCommand`/handler + DI; template existence left to FU02 handler (platform-default fallback). **Tests 13/0** (INVALID_EVENT_CODE 400 / EVENT_NOT_FOUND 404 / EVENT_NOT_ACTIVE 409 / TEMPLATE_KEY_MISSING 422 / REQUIRED_VARIABLE_MISSING 422 / RECIPIENT_MISSING 400 / provider-failure + success passthrough + 3 tenant-event resolution); **suite 1166/0**; Platform.API 0 error. `QueueEmailNotificationCommand`/handler UNCHANGED; no producer migration; no new pipeline/provider/template; no Module Catalog/ModulePages/PlatformNavigationCatalog/Gateway side-effect; see [FU04B smoke audit](../../docs/audits/pss-mod-0027-fu04b-eventcode-dispatch-adapter-smoke-2026-07-08.md). Note: unit proof = resolution+delegation only; real render/send owned by FU02 handler. **Follow-ups: FU04C (tenant producer migration) + FU04D (producer runtime wiring) + FU04M (manifest workflow/document/import opt-in).** Adapter on Application layer — not its own code-truth row per this file's charter. **FU04C Tenant Producer EventCode Migration — pack `ready-for-dev` (registry MOD-0027-FU04C, 2026-07-09; DCP + owner review PASS; was informal "FU04B-Tenant"), NO code yet (migrate AdminUserInvitationService → tenant.user.invited + TenantLifecycleNotificationConsumer suspended/reactivated → tenant.lifecycle.* via FU04B DispatchNotificationByEventCodeCommand). Karar A RESOLVED: consumer adds TenantDisplayName (tenant.DisplayName ?? tenant.Name), mapper signature unchanged. Karar B RESOLVED: ReasonCode-based non-retryable controlled-4xx (EVENT_NOT_FOUND/EVENT_NOT_ACTIVE/REQUIRED_VARIABLE_MISSING/TEMPLATE_KEY_MISSING) log+swallow, provider/transient throw/retry, no rollback, invite fail-soft. No adapter/handler/template/seed/created-branch change. Not a code-truth row per this file's no-code-no-row charter.** |
| MOD-0021 Audit Trail Service | Backend+Frontend | 85 | Append-only AuditEvent + retention; outbox; redaction; AuditLog + AuditRetention UI; 7-locale l10n; 7 test files | Tenant-scoped audit viewer (tenant self-audit); advanced filters; UI/redaction workflow tests |
| MOD-0285 System Navigation Management | Kısmi (loader done) | 55 | ModulePageDescriptor + self-reg + **runtime menu loader (2026-06-24)**: `GET /api/platform/navigation/menu` (tenant_user, entitled-modules ∩ nav-visible/Active descriptors, tenant-scoped) → Diten.Web `DynamicModuleMenuViewComponent` renders a dynamic "Modules" section in `_LayoutTenantShell` (augment — hardcoded sections untouched), per-item `Perms.Has` gating. **Live-proven in browser**: self-registered Golden Slim RECORDS appears in the tenant menu automatically (entitled+permitted), disappears when not. Closes the "code → menu" loop. | Admin menu-tree-builder + route-binding console + **governed publish/versioning** still missing (blocked on MOD-0023 Workflow); hierarchy beyond parent/child flat. |
| MOD-0002 Interface Registry | Backend+Frontend | 80 | Interface manifest import/diff/batch entities; 7 API endpoints; Index/Details UI; l10n | Only 1 test file; no visual diff renderer; no manifest-upload UI; no RabbitMQ discovery integration test |
| MOD-0035 Event Bus / Message Queue | Sadece-backend | 65 | IEventBus + RabbitMQ + InMemory fallback; lifecycle consumers; outbox; 6 integration tests | **No UI** (no DLQ/replay/subscription monitoring); unit tests for bus logic thin |

## Access Governance / Auth (Diten.AuthService + frontend)

| Module ID | Durum | % | Var olan | Eksik |
|---|---|---|---|---|
| MOD-0018 RBAC / ABAC (parent) | Backend+Frontend | 85 | Permission handler/requirement/policy provider; `[HasPermission]`; grant-source tracking; 5 admin screens | data-scope frontend; FU13 cache hooks |
| MOD-0018-FU9 RBAC Admin UI (5 screens) | Backend+Frontend | 95 | Users/Roles/Permissions/RoleAssignments/UserRoleAssignments full CRUD + grant-state JS + Vitest | RoleAssignments/UserRoleAssignments **row l10n minimal** |
| MOD-0018-FU10a Pure Auth Decision Contract | Backend+Frontend | 90 | PermissionAuthorizationHandler + AuthorizationSnapshot cache contract | AuthService self-explain (read via Platform/FU14 only) |
| MOD-0018-FU10b EntitlementChecker ResolvedFrom | Sadece-backend | 80 | EntitlementChecker + cache (Platform); integration tests | No entitlement-debug UI; batch-check TODO (perf) |
| MOD-0018-FU12 Tenant Authorization Context | Backend+Frontend | 85 | JwtTenantAuthorizationContext (lazy org-scope init); 6 test files | No "context browser" UI |
| MOD-0018-FU13 Permission Convention + Cache Invalidation | **Kısmi** | 40 | Cache key builder; event envelope | **No invalidation hook on role/perm mutation; TTL hardcoded 300s; no admin invalidate; no tests** (stale perms ≤5min) |
| MOD-0018-FU14 Effective Access Explain | Backend+Frontend | 80 | SelfAccessExplainService + SelfAccess UI (two-observation, no combined verdict by design); tested | Cross-user explain deferred |
| MOD-0018-FU15 Real DataScopeResolver | Sadece-backend | 90 | OrgDataScopeResolver (OrgUnit/Position/ManagerChain/LegalEntity from MOD-0288); tests | **No frontend** (org-scope browser); gated by MOD-0288 UI (C2) |
| MOD-0017-FU01 Tenant Login Security Settings | Backend+Frontend | 85 | TenantLoginSettings client/policy; used in handlers | **No tenant-admin config UI** (MFA/session/password policy not self-service) |
| CAND-CAP-0001 Tenant User / Identity Foundation | Backend+Frontend | 90 | User/Membership/Role/Permission entities; invitation+set-password flow; Users CRUD; ~15 tests | No bulk user import/export |

## Other services

| Module / Service | Durum | % | Var olan | Eksik |
|---|---|---|---|---|
| MOD-0220 Corporate Secretarial / Legal Entity (Diten.MdmService) | Backend+Frontend (slice) | 65 | LegalEntity entity + CRUD API + GET list; **tenant-side mini-slice (2026-06-23): Legal Entities screen** (create/activate/archive/delete, Views/MasterData/LegalEntities/*), gateway methods widened (POST/PATCH/DELETE), en+tr l10n. **READ-BACK BUG FIXED**: MDM was missing `clientSettings.GuidRepresentation=Standard` → all reads (list/by-id/activate/lookup-validation) returned empty against correctly-stored Standard docs; one-line fix + real-Mongo round-trip regression test. | Full legal-entity master-data UX (versions/hierarchy/wider fields) still minimal; MDM still has no launchSettings (needs ASPNETCORE_ENVIRONMENT=Development to read its dev JWT secret). |
| DEV-* Golden Slim / Compact (Diten.DevEnablementService) | Backend+Frontend | 80 | GoldenReferenceSlim+Compact full CRUD; self-reg manifest (GoldenSlimManifestProvider); 16 views; 7-locale l10n | **ZERO automated tests**; Compact less polished than Slim |
| Enterprise Strategy / Business Performance (Diten.EnterpriseStrategyService) — MOD-ID unconfirmed | Backend+Frontend (partial) | 70 | 256 .cs / 19 controllers (objectives/planning-cycles/strategy-periods/KPIs/goals/initiatives/projects); 91 views | **No l10n on 91 views**; monolithic pre-release test (`UnitTest1.cs` 1798 lines, known-red); **MOD-ID not assigned in code**; view folder name drift (`EnterpriseStrategyBusinessPerformance`) |
| MOD-0032 API Gateway (gateway/Diten.ApiGateway) | Thin proxy | 85 | Ocelot 69 routes; JWT handler (+ PreviousSecrets rotation); correlation-id; Serilog/Prometheus | No tests; hardcoded downstream hosts (localhost:505x); no rate-limiting; routes undocumented |

---

## Foundation gaps to stand up BEFORE vertical modules (HR/CRM, MOD-0297+)

Ordered by how much they block verticals:

1. 🔴 **MOD-0288 Org/Person/Position — frontend.** Backend done, UI absent. Data-scope (FU15) cannot be operated; verticals needing org structure/data-scope are blocked. (roadmap C2)
2. 🔴 **MOD-0023 Workflow + MOD-0024 Task — not built (0%).** Any vertical needing approvals/SLA/tasking (and MOD-0285 governed publish) is blocked until ≥ MVP.
3. 🔴 **MOD-0285 Navigation runtime loader.** Until built, every new module needs a hand-edited hardcoded menu entry.
4. 🟡 **MOD-0220 MDM Legal Entity — frontend.** Backend-only; HR/CRM commonly reference legal entities → needs UI.
5. 🟡 **MOD-0033 Quota override admin UI** + **MOD-0018-FU13 cache invalidation** (stale-perm correctness) + **MOD-0017-FU01 tenant security self-service UI**.
6. 🟡 **MOD-0014 Module Boundary Registry — not built** (only needed if menu/grouping must be formal-boundary-driven; catalog-domain is a lighter substitute).

## Quality debt (cross-cutting)

- **Tests missing:** DevEnablement (0), API Gateway (0); EnterpriseStrategy pre-release monolith (known-red); MDM sparse.
- **L10n missing:** EnterpriseStrategy (91 views), MDM (all).
- **Backend-without-frontend:** MOD-0288, MOD-0220, MOD-0026 (ops), MOD-0035 (infra), MOD-0018-FU15.

## Access Governance — partial items the owner flagged (confirmed in code)
- MOD-0018-FU13 cache invalidation: **no frontend, no tests** (40%).
- MOD-0017-FU01 tenant login security: **no self-service config UI** (85% backend).
- MOD-0018-FU15 data-scope: **no frontend** (gated by MOD-0288 UI).
- RoleAssignments / UserRoleAssignments: **row-level l10n incomplete**.
- (The 5 core RBAC CRUD screens themselves ARE built + tested — 95%.)

---

*Maintenance: update a module's row here whenever its implementation state changes (new frontend, tests added, feature completed). This file — not the identity registry — is the home for status. See the registry's link to this file.*
