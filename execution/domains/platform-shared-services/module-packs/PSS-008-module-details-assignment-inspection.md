---
id: PSS-008
name: Module Details Assignment Inspection
domain: platform-shared-services
service: Diten.Platform
status: review
owner: module-pack-author
branch: feature/pss/pss-008-module-details-assignment-inspection
started: 2026-05-08
target: 2026-05-29
ui_pattern: details-tab-readonly
datatable: false
golden_reference: none
ui_reference: frontend/Diten.Web/Views/Platform/ModuleCatalog/Details.cshtml
---

# PSS-008 - Module Details Assignment Inspection

## Module Brief
Module Details Assignment Inspection, Platform Admin'in Module Catalog icindeki bir modulun detay ekraninda modulu hangi Subscription Plan'larin icerdiğini ve hangi tenantlara atanmis oldugunu read-only olarak incelemesini saglar.

- **Domain:** Platform & Shared Services
- **Capability Group:** Module Catalog / Entitlement Visibility
- **Purpose:** Bir catalog modulunun hangi planlarda ve tenantlarda kullanildigini gostermek.
- **Primary Actors:** Platform Admin, Support/Admin Auditor; Tenant Admin sadece ileride ve yalnizca kendi tenant context'i ile degerlendirilebilir.
- **UI Surface:** `Module Details -> Assignments` tab.
- **UI Reference:** Existing Module Catalog details tab pattern in `frontend/Diten.Web/Views/Platform/ModuleCatalog/Details.cshtml` (`nav-pills`, `tab-content`, card/table sections, skeleton/loading state).
- **Mutation Policy:** Bu pack create/update/delete, plan assignment, tenant provisioning veya enforcement islemi yapmaz.

## Business Explanation
Assignments tab asagidaki sorulara cevap vermelidir:

- "Bu modul hangi planlarda var?"
- "Bu modul su anda hangi tenantlara atanmis?"
- "Tenant bu modulu plan uzerinden mi aldi, manuel mi atandi, trial mi, override mi?"
- "Assignment su anda aktif mi pasif mi?"
- "Support/Admin bir module degisikligi yapmadan once etki alanini anlayabiliyor mu?"

Bu ekran operasyonel degisiklik araci degildir. Gorevi backed kaynaklardan gelen assignment bilgisini okunabilir, filtrelenebilir ve denetlenebilir sekilde gostermektir.

## Scope and Boundaries

### In Scope
- Module Details icinde backed read-only `Assignments` tab.
- Subscription Plan assignments ve Tenant Module Assignments bilgilerinin ayri bolumler halinde gosterimi.
- Summary cards: plan count, tenant assignment count, enabled tenant count, manual/override count.
- Plan assignment ve tenant assignment listeleri icin source/status/search/filter contract'i.
- Tenant assignment row detail icin read-only inspection contract'i.
- Loading, empty, no-result, error, degraded ve permission-denied state'leri.
- Eksik backend contract varsa fake UI yerine eksik endpoint/service/DTO/repository raporlama.
- Backed loading, empty state, degraded dependency ve filter behavior test beklentileri.
- First implementation step olarak Module Catalog route/key, Subscription Plan module entitlement source ve Tenant Module Assignment dependency durumunun zorunlu kontrolu.

### Out of Scope
- Subscription billing, payment, invoice veya usage-based billing.
- Runtime feature/module enforcement.
- Quota enforcement.
- Tenant lifecycle provisioning veya plan degisikligi.
- Module assignment create/update/delete.
- Navigation visibility logic.
- RBAC/permission catalog ownership duplicate etmek.
- SubscriptionPlan veya TenantModuleAssignment ownership'unu Module Catalog'a tasimak.
- Internal database ID'lerini route URL'lerde veya business UI'da gostermek.

## System-of-Record Ownership Map
| Capability / Data | System of Record | PSS-008 Role |
|---|---|---|
| Module definition / module identity | Module Catalog (`PSS-005`) | Validate `moduleCode`; module name/status display. |
| Plan-to-module entitlement | Subscription Plan / plan entitlement model (`PSS-006` + related mappings) | Read-only consume; mutate etmez. |
| Tenant-level module assignment state | Tenant Module Assignment capability (planned/owned separately) | Read-only consume; eksikse degraded state raporlar. |
| RBAC permission catalog | Auth/Authorization capability (`PSS-0018` / current Auth conventions) | Permission keys consume eder; ownership almaz. |
| Audit/evidence records | Audit Trail / observability capability (`MOD-0021`) | Read evidence consume eder; viewing audit policy gerekiyorsa event uretir. |

## Owned Objects
Bu pack bir mutation aggregate'i sahiplenmez. Sahiplenilen nesneler read model, query ve presentation contract'laridir.

- DTO / Read Models:
  - `ModuleAssignmentOverviewDto`
  - `ModulePlanAssignmentRowDto`
  - `ModuleTenantAssignmentRowDto`
  - `ModuleTenantAssignmentDetailDto`
  - `ModuleAssignmentQueryResult<T>`
  - `ModuleAssignmentDependencyStateDto`
- Application Queries:
  - `GetModuleAssignmentOverviewQuery`
  - `GetModulePlanAssignmentsQuery`
  - `GetModuleTenantAssignmentsQuery`
  - `GetModuleTenantAssignmentDetailQuery`
- Read Services / Adapters:
  - Module Catalog validator/read accessor.
  - Subscription Plan assignment read accessor.
  - Tenant Module Assignment read accessor, optional/dependency-aware.
- Frontend:
  - Module Details `Assignments` tab markup.
  - Read-only assignment summary cards, plan section, tenant assignment section and tenant assignment detail drawer/modal/page.
  - Gateway-backed JS/proxy only; no direct 5057 calls.
- Tests:
  - Application query tests for counts, filters, invalid enums, missing module and dependency failure.
  - UI smoke for details -> assignments tab -> filter -> tenant detail -> refresh.

## Ready-for-dev Gate
Bu pack `draft` kalir. Backend/frontend implementation baslamadan once asagidaki gate tamamlanmis olmalidir:

- User Approval Checklist kullanici tarafindan kabul edilmis olmalidir.
- Frontmatter `status` kullanici onayi ile `approved` veya `ready-for-dev` yapilmis olmalidir.
- Existing Module Details route/key karari doğrulanmalidir: `moduleCode` mu, mevcut public slug mi, yoksa baska non-sensitive key mi?
- Subscription Plan tarafinda module entitlement source zorunlu olarak tespit edilmelidir:
  - `SubscriptionPlan.IncludedModuleKeys`,
  - dedicated plan-module mapping entity/repository,
  - veya baska backed entitlement contract.
- Subscription Plan entitlement source bulunamaz veya backed olarak okunamazsa implementation blocked sayilir; fake plan assignment UI olusturulmaz.
- Tenant Module Assignment SoR/API/repository varligi zorunlu olarak kontrol edilmelidir.
- Tenant Module Assignment SoR/API/repository yoksa bu durum explicit degraded dependency olarak kayda gecirilir; tenant rows/detail implement edilmez, yalnizca tenant bolumu degraded state gosterir.
- Gateway route coverage kontrol edilir; gerekiyorsa integration-agent icin ayri route task'i acilir.
- Approval checklist kabul edilmeden backend/frontend implementation baslatilmaz.

## Entity Fields
No new persisted entity is required for this pack unless implementation discovery proves that a read-optimized projection is already an approved platform pattern. If a projection is proposed later, it must remain read-only derived data and must not become the system of record for plan or tenant assignment state.

### ModuleAssignmentOverviewDto
| Field | Type | Rules |
|---|---|---|
| moduleCode | `string` | Required; validated against Module Catalog; business code, not internal database id. |
| moduleName | `string` | Required; sourced from Module Catalog. |
| moduleStatus | `string` | Required; sourced from Module Catalog. |
| planAssignmentCount | `int` | Backed count only; fake count forbidden. |
| tenantAssignmentCount | `int` | Backed count only; if tenant dependency unavailable, expose degraded state instead of fake count. |
| enabledTenantCount | `int` | Count where effective assignment status is `Enabled`. |
| disabledTenantCount | `int` | Count where effective assignment status is `Disabled`. |
| manualOverrideCount | `int` | Count where effective source is `Manual` or `Override`. |
| planDerivedCount | `int` | Count where effective source is `Plan`. |
| lastAssignmentChangedAtUtc | `DateTimeOffset?` | Max known update timestamp across backed assignment sources. |
| dependencyState | `ModuleAssignmentDependencyStateDto` | Required when one source is unavailable/degraded. |
| correlationId | `string` | Required in API response envelope/log context. |

### ModulePlanAssignmentRowDto
| Field | Type | Rules |
|---|---|---|
| planCode | `string` | Preferred public route/display reference. |
| planId | `string?` | Optional backend reference; must not be the primary UI display value if project convention hides internal IDs. |
| planName | `string` | Required. |
| planStatus | `string` | Required; sourced from plan catalog. |
| entitlementStatus | `string` | Required; allowed values defined by plan entitlement owner. |
| includedByDefault | `bool` | True when plan includes the module by default. |
| effectiveFromUtc | `DateTimeOffset?` | Optional. |
| effectiveToUtc | `DateTimeOffset?` | Optional. |
| lastUpdatedAtUtc | `DateTimeOffset?` | Optional but preferred. |

### ModuleTenantAssignmentRowDto
| Field | Type | Rules |
|---|---|---|
| tenantCode | `string` | Preferred public route/display reference. |
| tenantId | `string?` | Optional backend reference; must not be exposed as primary business identifier. |
| tenantName | `string` | Required when tenant is accessible to caller. |
| tenantStatus | `string` | Required when tenant is accessible to caller. |
| assignmentStatus | `AssignmentStatus` | `Enabled`, `Disabled`, `Suspended`, `Pending`, `Expired`. |
| assignmentSource | `AssignmentSource` | Effective source: `Plan`, `Manual`, `Trial`, `Override`, `System`. |
| sourcePlanCode | `string?` | Required when source is `Plan` if source plan is known. |
| effectiveFromUtc | `DateTimeOffset?` | Optional. |
| effectiveToUtc | `DateTimeOffset?` | Optional. |
| assignedAtUtc | `DateTimeOffset?` | Preferred for `Manual`/`Override`. |
| assignedBy | `string?` | Preferred for `Manual`/`Override`; avoid raw internal user id if display name/email is available. |
| lastUpdatedAtUtc | `DateTimeOffset?` | Optional but preferred. |

### ModuleTenantAssignmentDetailDto
The detail response extends `ModuleTenantAssignmentRowDto` with read evidence fields:

- `assignmentReason`
- `effectiveStatusReason`
- `sourceEvidenceType`
- `sourceEvidenceReference`
- `createdAtUtc`
- `createdBy`
- `lastChangedAtUtc`
- `lastChangedBy`
- `auditEvidenceAvailable`
- `correlationId`

## Assignment Source Semantics
Tenant assignment display must show effective source, not merely the raw stored record source.

| Source | Meaning |
|---|---|
| Plan | Tenant received the module through an active subscription plan. |
| Manual | Platform Admin assigned the module directly to the tenant. |
| Trial | Temporary assignment for a trial period. |
| Override | Explicit tenant-level override differs from plan-derived entitlement. |
| System | Seeded/core/internal assignment. |

## Assignment Status Semantics
Allowed display/status filter values:

- `Enabled`
- `Disabled`
- `Suspended`
- `Pending`
- `Expired`

Expired assignments are hidden from default active views unless the inactive/expired filter is selected or audit mode is enabled.

## API Contract
All endpoints are read-only and Gateway-backed. Frontend must call Gateway port `5000`; it must not call Platform service port `5057` directly.

| Method | Route | Response |
|---|---|---|
| GET | `/api/platform/modules/{moduleCode}/assignments/overview` | `Response<ModuleAssignmentOverviewDto>` |
| GET | `/api/platform/modules/{moduleCode}/assignments/plans` | `Response<PagedResult<ModulePlanAssignmentRowDto>>` |
| GET | `/api/platform/modules/{moduleCode}/assignments/tenants` | `Response<PagedResult<ModuleTenantAssignmentRowDto>>` or degraded response |
| GET | `/api/platform/modules/{moduleCode}/assignments/tenants/{tenantCode}` | `Response<ModuleTenantAssignmentDetailDto>` |

### Query Parameters
Plan assignments:

- `status`
- `search`
- `page`
- `pageSize`

Tenant assignments:

- `source`
- `status`
- `tenantStatus`
- `search`
- `page`
- `pageSize`

### API Rules
- `moduleCode` is required and must be validated through Module Catalog.
- Missing module returns 404.
- Invalid `source`, `status`, or `tenantStatus` filter returns controlled validation error.
- Canonical partial dependency behavior:
  - If Module Catalog validation and plan assignments can be loaded, the Assignments tab remains usable.
  - If only Tenant Module Assignment dependency is unavailable, tenant assignments endpoint/section returns controlled degraded state; the whole screen must not become 503.
  - If Module Catalog source is unavailable, module identity cannot be validated and the screen returns controlled error/503 according to current API convention.
  - If Subscription Plan entitlement source is unavailable, plan section returns controlled error and the screen cannot claim a complete assignment overview; tenant section may still show only if independently backed and authorized, but summary must clearly mark plan source unavailable.
- All responses/log entries include `correlationId`.
- Fake row, fake count or synthetic successful data is forbidden.
- Response envelope follows existing `Response<T>` and `CustomBaseController` conventions.
- Endpoint names use `moduleCode` rather than internal database id unless existing public Module Catalog route convention requires another non-sensitive key.

## UI Specification
### Module Details -> Assignments Tab
The tab is read-only. It must not show edit/remove/create buttons unless a real backed mutation API is separately approved in another module pack.

Use the current Module Catalog details tab pattern as the UI reference:

- File: `frontend/Diten.Web/Views/Platform/ModuleCatalog/Details.cshtml`
- Pattern: `_LayoutPlatformAdmin`, breadcrumb header, compact `nav-pills` tab buttons, `tab-content p-0 bg-transparent shadow-none`, card/table sections, backed loading state.
- Existing Assignments tab skeleton must be upgraded rather than replaced with a different page pattern.
- `golden_reference: none` remains correct because this is not a create/edit DataTable module; the UI reference is the existing Module Details tab surface.

### Summary Cards
- Plans using this module
- Assigned tenants
- Enabled tenants
- Manual / Override assignments

Cards must render backed values only. If tenant assignment dependency is degraded, tenant-related cards show an explicit degraded/unknown state instead of zero or fake counts.

### Section 1: Subscription Plans
Columns:

- Plan Name
- Plan Code
- Plan Status
- Entitlement Status
- Included by Default
- Effective Dates
- Last Updated

Empty state:

- `Bu modul henuz herhangi bir plana dahil edilmemis.`

### Section 2: Tenant Assignments
Columns:

- Tenant Name
- Tenant Code
- Tenant Status
- Assignment Status
- Source
- Source Plan
- Effective Dates
- Assigned By
- Last Updated
- Details

Empty state:

- `Bu modul henuz herhangi bir tenant'a atanmamis.`

Partial degraded state:

- `Tenant assignment verisi su anda gecici olarak alinamiyor.`

### UI States
- Loading skeleton while backed calls are pending.
- Empty plan state.
- Empty tenant state.
- No-result state after filters.
- Partial degraded state for tenant assignment dependency failure while plan assignments remain visible.
- Permission denied state; sensitive tenant assignment rows must not render without permission.
- Controlled retry state for transient error.
- Detail drawer/modal/page is read-only and hides internal ids unless project conventions explicitly allow them.

## Validation Rules
- `moduleCode` is required and must match an existing Module Catalog record.
- `source` filter must be one of `Plan`, `Manual`, `Trial`, `Override`, `System`.
- `status` filter must be one of `Enabled`, `Disabled`, `Suspended`, `Pending`, `Expired`.
- `tenantStatus` filter must use the existing Tenant status enum/contract; free-text values are rejected.
- Inaccessible tenant records must not render as tenant assignment rows.
- Expired assignments are shown only when inactive/expired filter is selected or audit mode is enabled.
- If source is `Plan`, `sourcePlanCode` should be populated when available.
- If source is `Manual` or `Override`, `assignedBy` and `assignedAtUtc` should be shown when available.
- Counts must be computed from the same backed filter/query semantics used by the displayed datasets or from authoritative query totals.

## RBAC
Recommended permissions, aligned to existing PascalCase dot convention where implementation patterns require it:

- `Platform.Modules.Read` / business alias `platform.modules.view`
- `Platform.Modules.Assignments.Read` / business alias `platform.modules.assignments.view`
- `Platform.Modules.Assignments.Tenants.Read` / business alias `platform.modules.assignments.tenant.view`
- `Platform.Modules.Assignments.Audit.Read` / business alias `platform.modules.assignments.audit.view`, optional

Rules:

- Platform Admin can view all assignment information.
- Auditor can view historical metadata only when audit permission is granted.
- Support can view limited read-only data when explicitly permitted.
- Tenant Admin is future-only and must be restricted to own tenant context; no cross-tenant rows.
- Unauthorized users must receive 403 or permission-denied UI state, not empty fake data.

## Audit / Observability
This pack is read-only; normal viewing may not require business audit unless policy says otherwise.

Audit required when:

- Tenant assignment detail viewing is policy-audited.
- Access is denied.
- Dependency failure is detected and materially affects inspection.

Export:

- Assignment list export is future/out-of-scope for PSS-008.
- If export is later approved in a separate pack, export action must have explicit permission, audit event, correlationId and no hidden/sensitive tenant leakage.

Logs / metrics:

- `module_assignment_overview_load_duration`
- `module_assignment_plan_query_duration`
- `module_assignment_tenant_query_duration`
- `module_assignment_dependency_failure_count`
- `module_assignment_permission_denied_count`
- `correlationId` in all API/log records.

## Repo Scope
Planning-only current change:

- `execution/domains/platform-shared-services/module-packs/PSS-008-module-details-assignment-inspection.md`

Later approved implementation scope:

- `services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleAssignments/**`
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/Platform/ModuleAssignmentsController.cs`
- `services/Diten.Platform/src/Diten.Platform.Domain/Repositories/**` only for read repository interfaces needed by this inspection flow.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/**` only for read-side implementations or adapters.
- `services/Diten.Platform/tests/**`
- `frontend/Diten.Web/Controllers/Platform/ModuleCatalogController.cs`
- `frontend/Diten.Web/Models/ModuleCatalog/**` or existing ModuleCatalog view model file.
- `frontend/Diten.Web/Views/Platform/ModuleCatalog/Details.cshtml`
- `frontend/Diten.Web/wwwroot/assets/js/Platform/ModuleCatalog/module-assignments.js`
- `frontend/Diten.Web/Resources/Views/Platform/ModuleCatalog/**`
- `gateway/Diten.ApiGateway/**` only for route validation/coordination; `ocelot.json` remains integration-agent owned.

## Protected Paths
- `.antigravity/**`
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`
- `gateway/Diten.ApiGateway/**/ocelot.json` unless explicitly handled by integration-agent after approval.
- `services/Diten.AuthService/**`
- `services/Diten.DevEnablementService/**`
- `services/Diten.EnterpriseStrategyService/**`
- `services/Diten.MdmService/**`
- Any mutation ownership for SubscriptionPlan, Tenant lifecycle/provisioning, billing, quota or runtime entitlement enforcement.
- Any backend/frontend/gateway/runtime file during this planning-only task.

## Dependencies
- `PSS-005-tenant-module-catalog` for Module Catalog identity and Module Details surface.
- `PSS-006-tenant-subscription-plan-catalog` for Subscription Plan catalog.
- `PSS-007-subscription-feature-management` for entitlement visibility boundaries and no-enforcement precedent.
- Subscription Plan module entitlement source. This is a mandatory first implementation check, not a best-effort discovery item. If no backed source exists, plan assignment delivery is blocked until a safe read contract exists.
- Tenant registry / Tenant Module Assignment capability. If not implemented, this is an explicit degraded dependency: tenant rows/detail are not implemented, but the plan section can still ship backed/read-only.
- Existing Platform API `Response<T>`, `CustomBaseController`, MediatR/CQRS and validation pipeline conventions.
- Existing Gateway URL proxy pattern in `frontend/Diten.Web`.
- Existing Platform Admin authorization policy and `[HasPermission]` convention.
- Localization standard: `en`, `fr`, `es`, `zh`, `ar`, `ru`, `tr`.

## Runtime Constraints
- Read-only only; no create/update/delete assignment operations.
- Module Catalog remains the module identity source.
- Subscription Plan remains the plan entitlement source.
- Tenant Module Assignment remains tenant-level assignment state source.
- API and UI must not generate fake rows or fake counts.
- Tenant Module Assignment SoR/API/repository absence is an explicit blocker for tenant rows/detail and an explicit degraded dependency for the tenant section; it is not a reason to invent UI data.
- Subscription Plan module entitlement source must be verified before implementation; if no backed source exists, plan assignment section is blocked.
- Frontend calls only Gateway port `5000`; direct 5057 calls are forbidden.
- API responses use `Response<T>` envelope and current controller conventions.
- JWT + RBAC is mandatory.
- MongoDB is the persistence source for existing owning models; PSS-008 may read through repositories/adapters only.
- DataTable verifier does not apply; this is a details-tab read-only inspection surface, not a DataTable module.
- `golden_reference: none`.
- UI reference is existing Module Catalog Details tab pattern in `frontend/Diten.Web/Views/Platform/ModuleCatalog/Details.cshtml`.
- Internal database ID must not be shown in business UI or route URL when a public `moduleCode`, `tenantCode`, or `planCode` exists.

## Golden Flow
1. Platform Admin opens Module Catalog.
2. Admin opens a module's Module Details screen.
3. Admin clicks `Assignments` tab.
4. System loads assignment summary from backed sources.
5. Admin sees plans containing the module.
6. Admin sees tenants assigned to the module when tenant assignment dependency is available and authorized.
7. Admin sees assignment source/status, summary counts and last assignment metadata.
8. Admin filters by source/status/search.
9. Admin opens a tenant assignment row detail.
10. Admin refreshes the page and the same backed data reloads without relying on fake client state.

## Failure Path to Verify
If Tenant Module Assignment API/service/repository is missing or unavailable:

- Subscription Plans section continues to render backed plan assignments when available.
- Tenant Assignments section renders controlled degraded state.
- Tenant-related counts render unknown/degraded state, not zero unless the backed tenant query returns zero.
- No fake tenant rows are shown.
- No edit/remove/action buttons are shown.
- Error is logged with `correlationId` and dependency failure metric.
- The whole Assignments tab does not return 503 solely because tenant assignment dependency is unavailable.

If Subscription Plan module entitlement source is missing or unavailable:

- Plan assignment section cannot be implemented as backed and must report the exact missing source.
- Fake plan rows/counts are forbidden.
- This is a blocker for a complete ready-for-dev implementation unless a safe read contract is added first.

If Module Catalog source is missing or unavailable:

- `moduleCode` cannot be validated.
- The details assignment flow is blocked and must return controlled error/404/503 according to the actual failure.

## Acceptance Criteria

### Runtime Criteria
- [ ] Platform Admin opens Module Details and sees a read-only `Assignments` tab.
- [ ] Assignments tab loads backed plan assignment rows for the selected `moduleCode`.
- [ ] Tenant assignment rows load from backed tenant assignment source when the dependency exists and caller is authorized.
- [ ] Source/status/search filters work for tenant assignments.
- [ ] Status/search filters work for plan assignments.
- [ ] Tenant assignment row detail opens read-only and shows effective source/status/evidence metadata when backed fields exist.
- [ ] Refresh reloads the same backed data through API calls.

### Integrity Criteria
- [ ] `moduleCode` is validated against Module Catalog and missing module returns 404.
- [ ] Count cards match authoritative query totals or displayed backed datasets.
- [ ] Tenant assignment dependency failure does not create fake zero counts, fake rows or fake success.
- [ ] Read models do not mutate Module Catalog, Subscription Plans or Tenant Assignments.
- [ ] Subscription Plan ownership and Tenant Module Assignment ownership remain outside Module Catalog.
- [ ] No billing, quota, runtime feature enforcement, tenant provisioning or navigation visibility logic is implemented.
- [ ] UI and routes prefer public codes over internal database IDs.

### UX Criteria
- [ ] Loading skeleton exists.
- [ ] Empty plan state exists with the approved copy.
- [ ] Empty tenant state exists with the approved copy.
- [ ] No-result state exists after filters.
- [ ] Error state includes controlled retry.
- [ ] Degraded tenant assignment state exists and does not hide backed plan data.
- [ ] Permission-denied state prevents sensitive tenant rows from rendering.
- [ ] Source badges distinguish `Plan`, `Manual`, `Trial`, `Override`, and `System`.
- [ ] Status badges distinguish `Enabled`, `Disabled`, `Suspended`, `Pending`, and `Expired`.
- [ ] Raw technical tokens, stack traces and internal IDs are not displayed to business users.

## Test Expectations
Backend build after approved implementation:

- `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug`

Backend tests:

- `dotnet test services/Diten.Platform`
- Overview query for module with no assignments.
- Module with only plan assignments.
- Module with `Plan` source tenant assignment.
- Module with `Manual` and `Override` tenant assignments.
- Missing module returns 404.
- Invalid source/status filter returns validation error.
- Tenant assignment dependency failure returns degraded state or canonical 503 according to selected convention.
- Unauthorized user cannot see tenant assignment rows.
- Counts remain consistent with query totals.
- Expired assignments are hidden by default and shown only under inactive/expired filter or audit mode.

Frontend build after approved UI implementation:

- `dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug`

Gateway build/validation if route changes are required:

- `dotnet build gateway/Diten.ApiGateway/Diten.ApiGateway.csproj -c Debug`

JavaScript:

- `node --check frontend/Diten.Web/wwwroot/assets/js/Platform/ModuleCatalog/module-assignments.js`

Browser smoke:

- Open Module Catalog.
- Open Module Details.
- Open Assignments tab.
- Verify loading -> backed rows or empty states.
- Apply source/status/search filters.
- Open tenant assignment detail.
- Refresh and verify backed data reload.
- Simulate tenant assignment dependency unavailable and verify degraded tenant section while plan section remains usable.

Localization:

- Resource keys exist for `en`, `fr`, `es`, `zh`, `ar`, `ru`, and `tr`.

DataTable:

- No DataTable verifier is expected because this module is not a DataTable surface.

## Implementation Notes
- First implementation step must verify the backed Subscription Plan module entitlement source. If plan-to-module data currently exists only as `IncludedModuleKeys` on `SubscriptionPlan`, the read adapter may project rows from that field while preserving Subscription Plan as SoR. If no backed source exists, stop and report the missing source before building plan assignment UI.
- First implementation step must verify whether Tenant Module Assignment SoR/API/repository exists. If missing, do not build fake tenant assignment data; implement only backed plan assignment display plus explicit degraded/missing dependency state for the tenant section.
- If tenant assignment records do not exist yet, create a separate Tenant Module Assignment module pack before implementing mutation/persistence ownership.
- Keep tenant assignment detail read-only. Do not introduce management actions through the detail drawer.
- Permission key names must align with the repository's actual convention during implementation; aliases in this pack are business recommendations.
- Gateway route coverage must be checked before requesting integration-agent route work.

## Output Contract
Completion report for implementation must include:

- Changed files.
- Exact golden flow proof.
- Failure path proof.
- API/DTO/schema changes.
- Boundary/SoR check.
- Tests and exact commands.
- Audit/observability impact.
- `ASSUMPTION` and `TBD` items.
- Remaining gaps outside this pack.

## Failure Protocol
If implementation is blocked, stop and report:

- Exact missing endpoint/service/repository/DTO.
- Why it blocks the golden flow.
- Smallest safe remediation prompt.
- Whether partial read-only UI/docs were created.
- Whether Subscription Plans section can still be backed and delivered without tenant assignment rows.

## ASSUMPTIONS
- `PSS-008` is the next local PSS sequence after `PSS-007`.
- Module Details currently exists under Module Catalog and can accept a new tab without changing navigation placement logic.
- `moduleCode` is the preferred public identifier for Module Catalog routes.
- Subscription Plan assignment data can be read from existing plan/module entitlement structures or from a dedicated plan entitlement repository if one exists.
- Tenant Module Assignment SoR may not exist yet; degraded state is acceptable until a separate ownership pack/API exists.
- This pack is an inspection/read model feature, not an entitlement enforcement feature.

## TBD
- Confirm exact existing route for Module Details and whether `moduleCode` or another public slug is the current route key.
- Verify exact SubscriptionPlan module entitlement storage (`IncludedModuleKeys`, mapping entity, or repository) as mandatory first implementation step.
- Verify whether Tenant Module Assignment persistence/API exists and its owner module pack; if absent, tenant assignment section is degraded dependency.
- Confirm final permission key names according to current AuthService permission catalog.
- Confirm whether tenant assignment detail viewing must create audit events under policy.

## User Approval Checklist
- [ ] Module purpose and read-only scope are correct.
- [ ] `PSS-008` id and filename are acceptable.
- [ ] Module Catalog, Subscription Plan and Tenant Module Assignment ownership boundaries are accepted.
- [ ] No billing/enforcement/quota/provisioning/navigation scope is accepted.
- [ ] Gateway-backed read endpoint proposal is accepted.
- [ ] Tenant assignment degraded behavior is accepted when dependency is missing.
- [ ] Canonical partial failure behavior is accepted: tenant dependency failure degrades only tenant section; full 503 is reserved for module catalog or plan source failures that block backed inspection.
- [ ] Subscription Plan module entitlement source must be verified before implementation starts.
- [ ] Existing Module Details tab UI pattern is accepted as the UI reference.
- [ ] Permission model is accepted as recommendation pending implementation discovery.
- [ ] User may change status from `draft` to `approved` or `ready-for-dev` after accepting this checklist.
- [ ] Backend/frontend implementation will not start before this checklist is accepted.

## Approval Readiness
This module pack is ready for user approval when:

- User accepts the approval checklist.
- Ready-for-dev gate is satisfied or explicitly accepted as the first implementation checklist.
- Development starts with read-only backed contracts only.
- Any missing Tenant Module Assignment SoR is handled as a blocker/degraded dependency, not by fake UI data.
- Backend/frontend implementation is not started while status remains `draft`.
