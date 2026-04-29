---
id: PSS-005
name: Tenant Module Catalog
domain: platform-shared-services
status: review
owner: codex
branch: feature/pss/pss-005-tenant-module-catalog
started: 2026-04-29
target: 2026-05-20
form_field_count: 12
golden_reference: compact
---

# PSS-005 — Tenant Module Catalog

## Module Summary
Tenant Module Catalog modulu, Platform Shared Services domain'i icinde ERP modullerinin platform seviyesindeki katalog kayitlarini yonetir. MVP kapsami, hangi ERP modullerinin mevcut oldugunu, aktif/pasif durumunu, tenant'a atanabilir olup olmadigini ve ileride tenant feature, plan ve quota yapilariyla iliskilendirilebilecek temel catalog contract'ini hazirlamaktir.

Bu module pack kalici execution sozlesmesidir. Kod gelistirmesi yalnizca frontmatter `status` degeri `approved` veya `ready-for-dev` iken baslar; `in-progress`, `review` ve `done` durumlari teslimat yasam dongusunu belgelemek icindir.

## Ownership and Boundaries
- SoR:
  - ERP module catalog kayitlari icin Platform-owned catalog contract.
  - Module code/name/display metadata ve lifecycle status.
  - `IsCoreModule` ve `IsTenantAssignable` karar alanlari.
  - Future-ready feature/plan/quota iliski notlari ve contract yer ayiricilari.
- In-scope:
  - Platform service icinde Module Catalog CRUD/list/detail sozlesmesi.
  - Platform Admin UI icinde Module Catalog DataTable, create/edit/detail akis tasarimi.
  - 7 dil localization key seti.
  - Gateway route zorunludur: `/api/platform/module-catalog`.
- Out-of-scope:
  - Tenant module assignment CRUD.
  - Tenant feature entitlement yonetimi.
  - Plan enforcement.
  - Quota enforcement.
  - Billing/pricing entegrasyonu.
  - AuthService tarafinda permission generation veya runtime entitlement enforcement.
  - MOD-0014 kapsamindaki Domain / Suite / Capability Group hiyerarsi import omurgasini yeniden yazmak.

## Owned Objects
- Domain/Persistence:
  - `ModuleCatalogItem` standalone aggregate.
  - Mongo collection: `platform_module_catalog`.
  - Unique index: `ModuleCode`.
  - Query indexes: `Status`, `Domain`, `Service`, `Category`, `IsTenantAssignable`, `SortOrder`.
- Application Commands:
  - `CreateModuleCatalogItemCommand`
  - `UpdateModuleCatalogItemCommand`
  - `ActivateModuleCatalogItemCommand`
  - `DeactivateModuleCatalogItemCommand`
  - `DeleteModuleCatalogItemCommand` soft delete only.
- Application Queries:
  - `GetModuleCatalogItemsQuery` with search/filter/page/sort.
  - `GetModuleCatalogItemByIdQuery`
  - `GetModuleCatalogItemByCodeQuery`
  - `GetAssignableModuleCatalogItemsQuery`
- DTO/Contracts:
  - `ModuleCatalogItemDto`
  - `ModuleCatalogListItemDto`
  - `CreateModuleCatalogItemRequest`
  - `UpdateModuleCatalogItemRequest`
  - `ModuleCatalogFilterRequest`
  - Common contract types in `services/Diten.Platform.Common` only if they are consumed outside Platform service.
- API Endpoints:
  - `GET /api/platform/module-catalog`
  - `GET /api/platform/module-catalog/assignable`
  - `GET /api/platform/module-catalog/{id}`
  - `GET /api/platform/module-catalog/by-code/{moduleCode}`
  - `POST /api/platform/module-catalog`
  - `PUT /api/platform/module-catalog/{id}`
  - `POST /api/platform/module-catalog/{id}/activate`
  - `POST /api/platform/module-catalog/{id}/deactivate`
  - `DELETE /api/platform/module-catalog/{id}` soft delete.
- Frontend/UI:
  - Platform Module Catalog controller/proxy surface in `frontend/Diten.Web`.
  - `Views/Platform/ModuleCatalog/Index.cshtml`
  - `Views/Platform/ModuleCatalog/Create.cshtml`
  - `Views/Platform/ModuleCatalog/Edit.cshtml`
  - `Views/Platform/ModuleCatalog/Details.cshtml`
  - `Views/Platform/ModuleCatalog/_Form.cshtml`
  - DataTable v2 scripts under `wwwroot/assets/js/Platform/ModuleCatalog/**`.
- Localization:
  - `frontend/Diten.Web/Resources/Views/Platform/ModuleCatalog/*.resx`
  - Required cultures: `en`, `fr`, `es`, `zh`, `ar`, `ru`, `tr`.

## Entity Fields
`ModuleCatalogItem` contract:

| Field | Type | Rules |
|---|---|---|
| Base | `GlobalEntity` | Platform-level catalog kaydi; tenant-owned degildir. |
| ModuleCode | `string` | Required, unique, normalized. Min length: 3. Max length: 50. |
| ModuleName | `string` | Required. |
| DisplayName | `string` | Required. |
| Description | `string?` | Optional metadata. |
| Domain | `string` | Required. |
| Service | `string` | Required. |
| Category | `string?` | Optional. |
| Status | `enum` | Required. Strict values only: `Draft`, `Active`, `Inactive`, `Deprecated`. |
| ModuleVersion | `string` | Required semantic version using `major.minor.patch`; `Version` is reserved for technical concurrency fields. |
| IsCoreModule | `bool` | Core platform module flag. |
| IsTenantAssignable | `bool` | Tenant assignment eligibility flag. |
| SortOrder | `int` | Default `0`; must be `>= 0`; negative values are rejected. |

## Repo Scope
- `execution/domains/platform-shared-services/module-packs/PSS-005-tenant-module-catalog.md`
- `services/Diten.Platform/src/**`
- `services/Diten.Platform/tests/**`
- `services/Diten.Platform.Common/**` only for cross-service DTO/contract definitions required by future tenant feature/plan/quota consumers.
- `frontend/Diten.Web/Controllers/Platform/ModuleCatalogController.cs`
- `frontend/Diten.Web/Views/Platform/ModuleCatalog/**`
- `frontend/Diten.Web/wwwroot/assets/js/Platform/ModuleCatalog/**`
- `frontend/Diten.Web/Resources/Views/Platform/ModuleCatalog/**`
- `gateway/Diten.ApiGateway/**` for required route validation/coordination only; `ocelot.json` remains integration-agent owned.

## Protected Paths
- `.antigravity/**`
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`
- `gateway/Diten.ApiGateway/**/ocelot.json` unless explicitly handled by integration-agent in a later approved implementation phase.
- `services/Diten.AuthService/**`
- `services/Diten.DevEnablementService/**`
- `services/Diten.EnterpriseStrategyService/**`
- `services/Diten.MdmService/**`
- Any non-Platform domain service internals.

## Dependencies
- `MOD-0014-module-boundary-registry` for global platform catalog terminology and hierarchy alignment.
- `MOD-0043-tenant-architecture-foundation` for tenant context rules and gateway propagation.
- `MOD-0044-tenant-manager` for future tenant module assignment integration points.
- `MOD-0046-tenant-core-ui` for Platform Tenant UI navigation/detail integration expectations.
- `PSS-004-tenant-login-security-settings` for tenant settings UI separation pattern.
- AGENTS runtime rules for MongoDB, `Response<T>`, CQRS/MediatR, RBAC, soft delete and localization.

## Runtime Constraints
- API requests from frontend must go through Gateway port `5000`; frontend must not call Platform service port `5057` directly.
- API responses use `Response<T>` envelope and `CustomBaseController` conventions.
- JWT + RBAC is mandatory; Platform admin permissions must gate create/update/delete/activate/deactivate operations.
- MongoDB is the persistence store.
- Soft delete is mandatory; hard delete is not exposed.
- `TenantId` must not be accepted from create/edit form payloads.
- MVP catalog records are platform-level records and use `GlobalEntity`. Rationale: the catalog describes ERP modules globally and is referenced by tenant-specific assignments later; it is not itself tenant-owned data. Future `TenantModuleAssignment`, tenant feature, tenant plan and tenant quota records should be tenant-scoped and reference catalog item ids/codes.
- Gateway route is required: `YES`.
- Gateway base path: `/api/platform/module-catalog`.
- Frontend must call only Gateway port `5000`; Platform service port `5057` must never be called directly from frontend code.
- DataTable contract v2 is mandatory with `data-dt-standard="v2"`.
- `form_field_count: 12`.
- `golden_reference: compact` / `GoldenReferenceCompact`, because create/edit has more than 8 user-editable fields.
- Create/Edit user fields:
  - Module Code
  - Module Name
  - Display Name
  - Description
  - Domain
  - Service
  - Category
  - Status
  - ModuleVersion
  - IsCoreModule
  - IsTenantAssignable
  - SortOrder

## Normalization Rules
- `ModuleCode` is trimmed before validation and persistence.
- `ModuleCode` is stored uppercase.
- Internal whitespace and underscores are normalized to dash (`-`).
- Consecutive separators normalize to a single dash (`-`), for example `MODULE--CATALOG`, `MODULE__CATALOG`, and `MODULE-_-CATALOG` persist as `MODULE-CATALOG`.
- Leading/trailing separators are removed, for example `-MODULE-CATALOG-` and `_MODULE_CATALOG_` persist as `MODULE-CATALOG`.
- Final persisted `ModuleCode` canonical format is uppercase, trimmed, dash-separated only, has no consecutive separators, and has no leading/trailing separator.
- After canonical normalization, `ModuleCode` length must be minimum 3 and maximum 50 characters.
- `ModuleCode` shorter than 3 characters after canonical normalization is rejected.
- `ModuleCode` longer than 50 characters after canonical normalization is rejected.
- `ModuleCode` uniqueness is enforced by a unique Mongo index.
- Duplicate `ModuleCode` create/update requests are rejected before persistence when possible and by index protection at persistence level.
- Future note: if global uniqueness becomes too restrictive, `Domain + ModuleCode` composite uniqueness may be evaluated in a later phase.

## Status Transition Rules
- Status enum strict values are `Draft`, `Active`, `Inactive`, `Deprecated`.
- Free string values are not accepted.
- Mixed-case or lowercase status values are not normalized; validators reject them.
- UI presents status as a select/dropdown, not free text input.
- `Draft` -> `Active`
- `Active` -> `Inactive`
- `Inactive` -> `Active`
- `Active` -> `Deprecated`
- `Deprecated` -> read-only
- Deprecated records may update only metadata fields such as `DisplayName`, `Description`, and `SortOrder`.
- Invalid transitions are rejected with validation errors.

## ModuleVersion Rules
- `ModuleVersion` is required.
- Semantic version format must be used.
- Valid example: `1.0.0`.
- Free text values such as `latest`, `final`, and `v1` are rejected.
- Minimum validation pattern is `major.minor.patch`.

## SortOrder Rules
- `SortOrder` default value is `0`.
- `SortOrder` must be `>= 0`.
- Negative values are rejected.

## Deletion Rules
- `IsCoreModule=true` records cannot be deleted.
- Future tenant assignment references must block delete.
- MVP has no hard delete; delete action performs soft delete only.

## Assignable Endpoint Rules
`GET /api/platform/module-catalog/assignable` returns only records where:

- `Status = Active`
- `IsTenantAssignable = true`
- `IsDeleted = false`

## Gateway Route
- Required: `YES`.
- Base path: `/api/platform/module-catalog`.
- Frontend must route all Module Catalog API calls through Gateway port `5000`.
- Frontend must not call Platform service port `5057` directly.

## Acceptance Criteria
- [ ] Platform API exposes list/detail/create/update/activate/deactivate/soft-delete endpoints for Module Catalog under the required `/api/platform/module-catalog` Gateway base path.
- [ ] List endpoint supports `search`, `domain`, `service`, `category`, `status`, `isCoreModule`, `isTenantAssignable`, `page`, `pageSize`, and `sort` filters.
- [ ] `ModuleCatalogItem` persists as `GlobalEntity` with the explicit entity fields listed in this pack.
- [ ] `ModuleCode` is trimmed, stored uppercase, normalizes whitespace to dash, remains unique, and cannot produce duplicate records.
- [ ] `ModuleCode` consecutive separators normalize to a single dash.
- [ ] `ModuleCode` leading/trailing separators are removed.
- [ ] Persisted `ModuleCode` final format is uppercase dash-separated canonical format.
- [ ] Create/update is rejected when canonical normalized `ModuleCode` is outside the 3-50 character range.
- [ ] Create/update validation enforces required fields, allowed status values, `ModuleVersion` semantic format, boolean flags, and non-negative `SortOrder`.
- [ ] Status accepts only strict enum values: `Draft`, `Active`, `Inactive`, `Deprecated`.
- [ ] Invalid status strings and mixed-case/lowercase variants are rejected instead of normalized.
- [ ] `ModuleVersion` is rejected on create/update when it does not match semantic `major.minor.patch` format.
- [ ] Free text version values such as `latest`, `final`, and `v1` are rejected.
- [ ] Empty `SortOrder` defaults to `0` or is assigned `0` by the system.
- [ ] Negative `SortOrder` is rejected.
- [ ] Invalid status transitions are rejected.
- [ ] Deprecated records behave as read-only except allowed metadata updates (`DisplayName`, `Description`, `SortOrder`).
- [ ] Soft-deleted records are excluded from normal list/detail responses.
- [ ] `IsCoreModule=true` records cannot be deleted.
- [ ] Future tenant assignment references block delete when that relation exists.
- [ ] Cross-tenant payload injection is impossible because `TenantId` is not accepted by create/edit requests.
- [ ] Assignable endpoint returns only `Status=Active`, `IsTenantAssignable=true`, `IsDeleted=false` records.
- [ ] Core modules can be marked with `IsCoreModule=true`; MVP does not auto-assign them to tenants.
- [ ] Frontend Module Catalog uses DataTable v2 with GoldenReferenceCompact structure: Index plus separate Create/Edit/Details pages and shared `_Form.cshtml`.
- [ ] UI create/edit form contains exactly the 12 listed user fields and does not expose `TenantId`, audit fields, `IsDeleted`, `CreatedAt` or `UpdatedAt`.
- [ ] UI actions call the Gateway-backed frontend proxy only; no direct 5057 service calls exist.
- [ ] Localization resources exist for `en`, `fr`, `es`, `zh`, `ar`, `ru`, and `tr`, including table columns, filters, validation messages, statuses and action labels.
- [ ] Gateway route is treated as required; if `/api/platform/module-catalog` is missing, implementation is blocked until integration-agent updates route config.
- [ ] Scope-out items remain unimplemented in this pack: tenant assignment, plan enforcement, quota enforcement and billing/pricing.

## Test Expectations
- Backend build:
  - `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug`
- Backend tests:
  - `dotnet test services/Diten.Platform`
  - Unit coverage for validation, code normalization, duplicate prevention, status transition and soft delete.
  - Unit coverage for status transition validation, core module delete blocking, assignable endpoint filtering rules, `ModuleCode` normalization, duplicate `ModuleCode` prevention and deprecated read-only behavior.
  - Unit coverage for invalid status string rejection, `ModuleVersion` semantic validation, invalid `ModuleVersion` rejection (`latest`, `final`, `v1`), `SortOrder` default behavior and negative `SortOrder` rejection.
  - Unit coverage for consecutive separator normalization, leading/trailing separator cleanup and canonical `ModuleCode` persistence format.
  - Unit coverage for `ModuleCode` min length validation, max length validation and length validation after canonical normalization.
  - API/integration coverage for list filters, detail by id/code, create/update, activate/deactivate, invalid transition rejection, core delete blocking, soft delete, authorization failure and assignable-only query.
- Common contracts build if `services/Diten.Platform.Common` is touched:
  - Build the relevant Platform/Common project(s) discovered during implementation.
- Frontend build:
  - `dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug`
- Gateway build:
  - `dotnet build gateway/Diten.ApiGateway/Diten.ApiGateway.csproj -c Debug`
  - Required if any gateway route dependency is introduced or validated.
- Localization:
  - Run the repo RESX checker used by current project standards for the 7 required cultures.
- DataTable:
  - `python3 .antigravity/scripts/verify_datatable_page.py . --area Platform --module ModuleCatalog --reference compact`
- JavaScript:
  - Run syntax checks for new Module Catalog JS files when applicable, for example `node --check`.
- Browser smoke:
  - Verify Index, filters, create, edit, details, activate/deactivate and soft delete/error states through Gateway-backed frontend routes.

## Implementation Notes
- This pack intentionally separates global module catalog management from tenant module assignment. Tenant-specific assignment should be a later pack that references this catalog and `MOD-0044` tenant records.
- Temporary Decision: MVP uses a standalone `ModuleCatalogItem` aggregate.
- MOD-0014 module boundary / hierarchy terminology alignment must be preserved.
- Phase 2 will reevaluate whether this aggregate should merge with MOD-0014, reference MOD-0014 records, or be refactored into a shared model.
- This pack must not rewrite MOD-0014.
- Status values are strict and localized in UI labels only; persisted/API values remain exactly `Draft`, `Active`, `Inactive`, `Deprecated`.
- Suggested categories should be controlled values only if a shared reference data source exists; otherwise category remains a validated string in MVP.
- Future feature/plan/quota relation should be represented as design-ready references only, not enforced behavior.

## Follow-up Items
- Prepare a separate Tenant Module Assignment module pack.
- Prepare a separate Tenant Feature Entitlement module pack.
- Prepare a separate Tenant Plan and Quota module pack.
- Decide whether module catalog status changes should emit domain events for future entitlement cache invalidation.
- Decide whether `services/Diten.Platform.Common` should expose a stable `ModuleCode` value object for cross-service use.

## Open Questions
- Should `Domain` and `Service` be free text in MVP, enum-backed values, or sourced from MOD-0014 hierarchy records?
- Should `Category` be a controlled reference list in Platform, or remain a string until reference data catalog decisions are finalized?
- Should core modules have additional lifecycle restrictions beyond delete blocking, for example preventing deactivation while assigned in future tenant packs?
