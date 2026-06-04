---
id: MOD-0048
name: Reference Data Management
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: GlobalEntity
status: ready-for-dev
owner: module-pack-author
branch: feature/pss/pss-011-lookups-reference-data
started: 2026-05-14
target: 2026-05-28
form_field_count: 0
---

# MOD-0048 — Reference Data Management

> **Canonicalization (DCP-002):** Canonical ID is now **MOD-0048 Reference Data Management** (existing Blueprint module). Prior repo ID **PSS-011 (Lookups / Reference Data)** is a deprecated alias. Ref: `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`.

## Module Summary
This module completes the Platform/Admin lookup and reference-data API surface owned by `Diten.Platform`. It provides stable, cached, system-level lookup endpoints for Platform screens such as Tenant Management, Subscription Plans, Feature Management, and Module Catalog.

This module does not replace ERP Master Data Management. It only covers Platform-owned system lookups needed by SaaS administration: currency, locale/language, timezone, tenant tier, feature category, module domain, module service, subscription cycle, and similar Platform packaging/runtime enumerations.

Master Plan status before this pack: Partial, approximately 70%. Existing surface includes `Currency`, `FeatureCategory`, and `ModuleDomain`/`ModuleService` style lookups. Missing work includes removing hardcoded fallback currency lists, adding locale/timezone/tenant-tier lookups, and introducing a caching strategy.

## Current Implementation Snapshot (as of 2026-05-14)
Bu modül sıfırdan başlamıyor. Aşağıdaki kod halihazırda mevcut; geliştirici bu durumu **bilerek** ilerlemeli ve refactor olarak ele almalı:

**Backend — [LookupsController.cs](services/Diten.Platform/src/Diten.Platform.API/Controllers/LookupsController.cs):**
- `[ApiController]`, `[Route("api/lookups")]`, **`[AllowAnonymous]` (controller-level)** — refactor'da daraltılacak.
- Mevcut endpoint'ler:
  - `GET /api/lookups/module-catalog/domains` → `ModuleCatalogDomain` enum'u `LookupOption(Code, Name, Value)` record'u ile dönüyor.
  - `GET /api/lookups/module-catalog/services` → `ModuleCatalogService` enum'u aynı şekilde.
  - `GET /api/lookups/countries` → **20 ülke hardcoded** array (ISO-3166 tam liste değil).
  - `GET /api/lookups/currencies` → **14 currency hardcoded** array (`Code`, `Name` anonim obje, `Value` yok — `module-catalog` endpoint'lerinden farklı shape).
  - `GET /api/lookups/timezones` → `TimeZoneInfo.GetSystemTimeZones()` (OS bağımlı; ID format'ı işletim sistemine göre değişir).
- **Cache yok**, her istek tekrar hesaplıyor.
- Private nested record: `LookupOption(string Code, string Name, string Value)` — DTO olarak Application katmanına taşınacak.

**Mevcut enum'lar ([Diten.Platform.Domain/Enums/](services/Diten.Platform/src/Diten.Platform.Domain/Enums/)):**
- `ModuleCatalogDomain`, `ModuleCatalogService`, `ModuleCatalogStatus`, `ModulePageStatus`, `ModulePageType`, `ModulePageActionType`, `ModulePageActionStatus`, `TenantSubscriptionStatus`, `EntitlementSource`, `PlatformAdministratorEnums` — lookup-uygun adaylar.

**Frontend consumers (değişecek):**
- [SubscriptionPlansController.cs:250-279](frontend/Diten.Web/Controllers/Platform/SubscriptionPlansController.cs#L250-L279) — `LoadCurrencyLookupAsync()` gateway'i çağırıyor, **başarısızlıkta `USD/EUR/TRY/GBP` hardcoded fallback dönüyor**. Bu fallback kaldırılacak.
- [ModuleCatalogController.cs:167-170](frontend/Diten.Web/Controllers/Platform/ModuleCatalogController.cs#L167) — `api/lookups/module-catalog/{lookupName}` proxy. Korunacak.
- [TenantsController.cs:354-357](frontend/Diten.Web/Controllers/TenantsController.cs#L354) — `api/lookups/{**everything}` catch-all proxy. Korunacak.
- [Platform/Tenants/create.js](frontend/Diten.Web/wwwroot/assets/js/Platform/Tenants/create.js), `security.js` — lookup tüketicileri.

**Gateway ([ocelot.json:444-452](gateway/Diten.ApiGateway/ocelot.json#L444)):**
- `/api/lookups/{everything}` → Platform service `:5057`, `GET` + `OPTIONS`. Mevcut, doğru ordered.

**Response shape inconsistency (kritik):**
- `module-catalog/*` → `{ Code, Name, Value }` (record)
- `countries` / `currencies` → `{ Code, Name }` (anonymous object, `Value` yok)
- `timezones` → `{ Id, Name }` (farklı alan adı!)
- Refactor'da tek `LookupOptionDto { Code, Name, Value, ... }` shape'ine indirilecek; consumer'lar etkilenebilir → backward-compat geçişi `Acceptance Criteria`'da var.

**Önemli:** Yeni geliştirici dosyayı sıfırdan yazmamalı — `LookupsController.cs` thin'leştirilip MediatR query'lerine delegeleştirilecek; mevcut route'lar **break edilmeden** yeni shape'e geçilecek (gerekirse `?v=2` query param veya yeni endpoint paralel tutulup eski deprecated edilecek).

## Ownership and Boundaries
In scope:
- Platform-owned lookup endpoints under `Diten.Platform`.
- Read-only lookup contracts consumed by Platform/Admin screens through Gateway.
- System/global lookup catalog records when persistence is required.
- Enum-backed lookup adapters for existing Platform enums.
- Cache strategy for lookup reads.
- Removal of frontend hardcoded fallback currency options in Platform/Admin consumers.
- Gateway route verification for `/api/lookups/{everything}`.

Out of scope:
- ERP Account management.
- General Reference, Financial Reference, Territory Reference.
- Customer, Vendor, Account classification.
- Tenant-specific business lookup maintenance.
- MDM domain reference-data modules and any future `Diten.MdmService` implementation.
- Tenant-side ERP module lookups for Finance, Sales, Inventory, HR, CRM, PPM, or ESBP business workflows.
- Billing/invoicing domain behavior beyond exposing Platform subscription-cycle options.
- User-editable Platform Admin CRUD UI for reference data unless explicitly approved in a later pack.

Boundary decision:
- PSS-011 is a Platform system lookup module, not an ERP master-data module.
- MDM may later own ERP reference-data catalogs with richer tenant/business semantics. Those catalogs must not reuse this module as their source of record.
- PSS-011 may expose Platform values used by tenant provisioning, subscription packaging, module catalog, and entitlement workflows, but it must not create tenant-scoped ERP classifications.

## Owned Objects
Backend API:
- Existing controller to refactor/complete: `services/Diten.Platform/src/Diten.Platform.API/Controllers/LookupsController.cs`.
- Proposed route family: `GET /api/lookups/{lookupKey}` and grouped routes under `GET /api/lookups/platform/{lookupKey}` where clarity is needed.
- Existing route family to preserve: `GET /api/lookups/module-catalog/domains`, `GET /api/lookups/module-catalog/services`, `GET /api/lookups/currencies`.
- Required new or confirmed routes:
  - `GET /api/lookups/currencies`.
  - `GET /api/lookups/locales`.
  - `GET /api/lookups/languages` as alias only if existing UI vocabulary needs it.
  - `GET /api/lookups/timezones`.
  - `GET /api/lookups/tenant-tiers`.
  - `GET /api/lookups/feature-categories`.
  - `GET /api/lookups/module-catalog/domains`.
  - `GET /api/lookups/module-catalog/services`.
  - `GET /api/lookups/subscription-cycles`.
  - `GET /api/lookups/countries` remains Platform provisioning support, not Territory Reference ownership.

Application/services:
- `LookupOptionDto` as the single canonical serialized response model for all in-scope lookup endpoints.
- `IPlatformLookupProvider` / `PlatformLookupProvider` for composing lookup options outside the controller.
- `IPlatformLookupCache` or `IMemoryCache` based adapter for cached lookup reads.
- Optional `PlatformLookupSeed` for persisted system lookup values.

Canonical `LookupOptionDto` serialized shape:

```json
{
  "code": "USD",
  "name": "US Dollar",
  "value": "USD",
  "group": null,
  "sortOrder": 10,
  "metadata": {
    "symbol": "$"
  }
}
```

Rules:
- Required JSON fields: `code`, `name`, `value`.
- Optional JSON fields: `group`, `sortOrder`, `metadata`.
- JSON naming policy is camelCase. C# property names may be PascalCase, but serialized output must be camelCase.
- `code` is the stable lookup code and must never be blank.
- `name` is the display label for the current culture or the approved invariant display name.
- `value` is the machine value submitted by consumers; it defaults to `code` unless a route explicitly documents another value.
- `group` is omitted or `null` when unused; it must not be used for tenant/business taxonomy outside Platform scope.
- `sortOrder` is omitted or `null` when no explicit ordering exists; otherwise lower values sort first.
- `metadata` is omitted, `null`, or a small object with non-secret string values only.
- Ad hoc serialized shapes such as `{ "id": "...", "name": "..." }`, `{ "code": "...", "name": "..." }` without `value`, and mixed Pascal/camel variants are not accepted for in-scope endpoints after migration.

Domain/persistence, if persistence is introduced:
- `PlatformLookupItem : GlobalEntity`.
- `PlatformLookupType` enum or constants limited to Platform system types.
- Repository only if static/seeded values must be stored in MongoDB.
- MongoDB collection: `platform_lookup_items`.
- Indexes:
  - Unique active record by `LookupType + Code`.
  - Read index by `LookupType + IsActive + SortOrder`.

Existing consumed objects:
- `ModuleCatalogDomain` enum.
- `ModuleCatalogService` enum.
- `FeatureCategory` / `FeatureCategoryStatus` where feature-category lookup is backed by existing feature catalog data.
- `SubscriptionPlan` consumers that currently load currency options.
- Tenant create/edit Platform UI consumers for locale, timezone, currency, country, and tier options.

Frontend consumers:
- `frontend/Diten.Web/Controllers/Platform/SubscriptionPlansController.cs` currency lookup loader.
- `frontend/Diten.Web/Controllers/Platform/ModuleCatalogController.cs` module-catalog lookup proxy.
- `frontend/Diten.Web/Controllers/TenantsController.cs` lookup proxy for Platform Tenant screens.
- `frontend/Diten.Web/wwwroot/assets/js/Platform/Tenants/create.js` lookup consumers.
- `frontend/Diten.Web/wwwroot/assets/js/Platform/Tenants/security.js` lookup consumers.

Permissions:
- Read-only system lookups may be `[Authorize(Policy = "PlatformActor")]` or `[Authorize]` depending on existing public provisioning needs.
- Any Platform-admin-only lookup endpoint uses permission `Platform.Lookups.Read`.
- `[AllowAnonymous]` is not acceptable for non-public Platform lookup endpoints unless a route is explicitly documented as public bootstrap data.

## Entity Fields
Base type decision: `GlobalEntity`.

Justification:
- These are Platform-owned, cross-tenant system lookup values.
- They are not tenant-owned records and must not carry `TenantId` in request/response payloads.
- They are consumed by Platform/Admin workflows and tenant provisioning defaults.
- Tenant-scoped ERP lookup catalogs are intentionally excluded and belong to MDM.

If persistence is implemented, `PlatformLookupItem : GlobalEntity` has this schema:

| Field | Type | Rules |
|---|---|---|
| Base | `GlobalEntity` | No `TenantId`; inherits platform common base fields. |
| LookupType | `string` or enum | Required, max 64, Platform system type only. |
| Code | `string` | Required, max 64, normalized uppercase or stable IANA/BCP code as appropriate. |
| Name | `string` | Required, max 200, display name. |
| Value | `string` | Required, max 128, defaults to `Code` unless a different machine value is required. |
| Description | `string?` | Optional, max 500. |
| Group | `string?` | Optional, max 100, for UI grouping such as region or category. |
| SortOrder | `int` | Required, default 0. |
| IsActive | `bool` | Required, default true. |
| Metadata | `Dictionary<string, string>?` | Optional, small structured metadata such as symbol or UTC offset. |

Field notes:
- Currency codes use ISO 4217 style three-letter uppercase values.
- Locale/language codes use supported UI culture codes such as `en` and `tr`; future tenant-side 7-language support is not implied by this Platform pack.
- Timezone values use system/IANA or OS timezone IDs consistently; response must not mix ID formats within the same environment.
- Tenant tier values are Platform packaging tiers only, not ERP customer segmentation.
- Subscription cycle values are Platform subscription/billing cadence options only, not invoice or accounting configuration.

Create/edit form user field count: 0. This is a backend/API lookup module with no CRUD form in scope.

Golden Reference decision: `golden_reference: none`. No DataTable or create/edit UI is planned in this pack. If a future Platform Admin lookup management screen is approved, a new pack or pack revision must choose `platform-admin` shell and Slim/Compact based on the actual user-editable field count.

## Repo Scope
Allowed documentation scope:
- `execution/domains/platform-shared-services/module-packs/MOD-0048-lookups-reference-data.md`.

Allowed backend scope:
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/LookupsController.cs`.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Lookups/**`.
- `services/Diten.Platform/src/Diten.Platform.Application/Common/**` only if a shared lookup DTO/cache abstraction already belongs there.
- `services/Diten.Platform/src/Diten.Platform.Domain/Entities/PlatformLookupItem.cs` if persistence is implemented.
- `services/Diten.Platform/src/Diten.Platform.Domain/Enums/**` for Platform-only lookup enums.
- `services/Diten.Platform/src/Diten.Platform.Domain/Repositories/**` only for lookup-specific repository interfaces if persistence is implemented.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/**` for repository, seed, index, and cache-backed lookup data source registration.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/DependencyInjection.cs` for DI registration.

Allowed frontend consumer scope:
- `frontend/Diten.Web/Controllers/Platform/SubscriptionPlansController.cs` to remove hardcoded currency fallback behavior.
- `frontend/Diten.Web/Controllers/Platform/ModuleCatalogController.cs` only for lookup proxy alignment.
- `frontend/Diten.Web/Controllers/TenantsController.cs` only for lookup proxy alignment.
- `frontend/Diten.Web/wwwroot/assets/js/Platform/Tenants/create.js` only for lookup URL/shape alignment.
- `frontend/Diten.Web/wwwroot/assets/js/Platform/Tenants/security.js` only for lookup URL/shape alignment.

Frontend resources:
- No resource folder is in scope by default because this pack should not add a new UI surface.
- If a visible error/empty-state text change becomes unavoidable, the implementation task must name the exact affected Platform resource folder before editing it; broad `frontend/Diten.Web/Resources/Views/Platform/**` edits are not allowed.

Allowed gateway scope:
- Read-only verification of `gateway/Diten.ApiGateway/ocelot.json`.
- Route edits are protected and must be done by integration-agent or explicit approval if the existing `/api/lookups/{everything}` route is insufficient.

## Protected Paths
- `.antigravity/**`.
- `frontend/Diten.Web/Controllers/Archive/**`.
- `frontend/Diten.Web/Views/Archive/**`.
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`.
- `frontend/Diten.Web/Views/Shared/_LayoutPlatformAdmin.cshtml` unless a separate approved navigation task explicitly requires it.
- `gateway/Diten.ApiGateway/**/ocelot.json` unless handled by integration-agent or explicit route-change approval.
- `services/Diten.AuthService/**`.
- `services/Diten.DevEnablementService/**`.
- `services/Diten.EnterpriseStrategyService/**`.
- `services/Diten.MdmService/**` and any future MDM service folders.
- `execution/domains/master-data-management/**` unless a separate MDM planning task is requested.
- ERP Account, General Reference, Financial Reference, Territory Reference, Customer/Vendor/Account classification module paths if introduced elsewhere.
- Tenant-side ERP module folders and views outside the narrow consumer changes listed in Repo Scope.

## Dependencies
- `Diten.Platform` API, Application, Domain, Persistence, and Infrastructure projects.
- Existing Gateway route `/api/lookups/{everything}` to Platform service port 5057.
- `Diten.Web` Platform/Admin consumers that call lookup endpoints through Gateway or same-origin MVC proxies.
- Existing Platform enums: `ModuleCatalogDomain`, `ModuleCatalogService`, and subscription/feature enums where applicable.
- Existing feature-category persistence from PSS-007 when feature category lookup is data-backed.
- `IMemoryCache` or existing caching abstraction in the Platform service.
- `Response<T>` envelope and `CustomBaseController` conventions for non-trivial lookup handlers.
- Platform localization rule: only `en` and `tr` are mandatory for Platform/Admin visible strings.

## Runtime Constraints
- Frontend must never call Platform service port 5057 directly. Browser-facing requests go through `Diten.Web` same-origin proxy or Gateway port 5000.
- Lookup responses must have a stable shape across all endpoints; consumers must not special-case anonymous object variations.
- Platform lookup records are global and must not accept or emit `TenantId`.
- If persisted, normal reads filter inactive/deleted values and sort by `SortOrder`, then `Name` or `Code`.
- Hardcoded fallback currency options in frontend code must be removed; failure must surface as a controlled error/empty state instead of silently using stale `USD/EUR/TRY/GBP` data.
- Timezone lookup must be cached because `TimeZoneInfo.GetSystemTimeZones()` is stable during runtime and can be moderately expensive.
- Currency, locale, tenant tier, subscription cycle, and enum-backed lookups must use cache keys partitioned by lookup type and UI culture when display names are culture-dependent.
- Cache invalidation:
  - Static/system lookups: absolute expiration of 12 hours unless app restart/seed refresh invalidates.
  - Data-backed feature categories: shorter expiration of 5 minutes or explicit invalidation after feature-category writes.
- Platform Admin localization is `en` and `tr`; tenant-side ERP localization rules do not expand this module into MDM.

## Layout & Shell Contract
- `shell: none`.
- Razor layout: not applicable because this module is backend/API-only.
- No `Views/Platform/Lookups` folder is created in this pack.
- No Platform navigation entry is created in this pack.

Justification:
- The current Master Plan describes lookup endpoints, not a user-editable Platform Admin screen.
- Platform screens consume lookups inside their own existing shell pages.
- If a future lookup-management UI is approved, it must use `shell: platform-admin` with `Layout = "_LayoutPlatformAdmin"` explicitly set on every `.cshtml` page.

## Backend File Convention
Because `golden_reference: none`, full CRUD/DataTable folder scaffolding is not required. Backend changes still follow the same naming and layer rules:

```text
services/Diten.Platform/src/Diten.Platform.Application/Features/Lookups/
├── Queries/
│   ├── GetLookupOptionsQuery.cs
│   ├── GetCurrencyLookupQuery.cs
│   ├── GetLocaleLookupQuery.cs
│   ├── GetTimezoneLookupQuery.cs
│   └── GetTenantTierLookupQuery.cs
├── Handlers/
│   └── QueryHandlers/
│       ├── GetLookupOptionsHandler.cs
│       ├── GetCurrencyLookupHandler.cs
│       ├── GetLocaleLookupHandler.cs
│       ├── GetTimezoneLookupHandler.cs
│       └── GetTenantTierLookupHandler.cs
└── LookupModels.cs
```

Canonical CQRS routing decision:

| Endpoint group | CQRS path | Reason |
|---|---|---|
| `GET /api/lookups/currencies` | Explicit `GetCurrencyLookupQuery` + `GetCurrencyLookupHandler` | Currency has normalization, fallback-removal, and consumer migration risk. |
| `GET /api/lookups/locales` / `languages` | Explicit `GetLocaleLookupQuery` + `GetLocaleLookupHandler` | Locale support is a Platform UI dependency and must always include `en` and `tr`. |
| `GET /api/lookups/timezones` | Explicit `GetTimezoneLookupQuery` + `GetTimezoneLookupHandler` | Timezone source/caching and ID-format consistency need dedicated handling. |
| `GET /api/lookups/tenant-tiers` | Explicit `GetTenantTierLookupQuery` + `GetTenantTierLookupHandler` | Tenant tier must remain Platform packaging vocabulary, not ERP/customer classification. |
| `GET /api/lookups/feature-categories` | Generic `GetLookupOptionsQuery("feature-categories")` + `GetLookupOptionsHandler` | It consumes the existing Platform Feature Category source of record and applies generic active-only lookup projection. |
| `GET /api/lookups/countries` | Generic `GetLookupOptionsQuery("countries")` + `GetLookupOptionsHandler` | It is Platform provisioning/support data only and must stay read-only with stable DTO projection. |
| `GET /api/lookups/subscription-cycles` | Generic `GetLookupOptionsQuery("subscription-cycles")` + `GetLookupOptionsHandler` | It is enum/static Platform subscription cadence vocabulary. |
| `GET /api/lookups/module-catalog/domains` | Generic `GetLookupOptionsQuery("module-catalog/domains")` + `GetLookupOptionsHandler` | It is enum-backed Platform module catalog vocabulary. |
| `GET /api/lookups/module-catalog/services` | Generic `GetLookupOptionsQuery("module-catalog/services")` + `GetLookupOptionsHandler` | It is enum-backed Platform module catalog vocabulary. |

The generic lookup pipeline is canonical for `feature-categories`, `countries`, `subscription-cycles`, `module-catalog/domains`, and `module-catalog/services`. Implementers must not create ad hoc controller-only logic for these groups. Add a dedicated query/handler only when this table explicitly says the endpoint group is explicit or when this pack is revised.

Rules:
- Query records end with `Query`.
- Handler classes end with `Handler`; `QueryHandler` suffix is not used in class names.
- DTO records live in `LookupModels.cs` unless a specific DTO grows enough to justify a separate file under the established local pattern.
- Controller logic stays thin and delegates to MediatR or a Platform lookup service.
- Controller should inherit `CustomBaseController` when returning `Response<T>`.
- Business errors return `Response<T>.Fail()` rather than throwing exceptions.

## Frontend File Contract
No new frontend page is in scope.

Allowed frontend work is limited to consumer alignment:
- Remove hardcoded fallback currency options from `SubscriptionPlansController`.
- Ensure Platform/Tenants lookup calls consume Gateway or same-origin proxy endpoints, not service ports.
- Preserve existing Platform/Admin layouts in consuming pages.
- Add or update `en` and `tr` `.resx` keys only if new visible error/empty-state text is introduced.

DataTable verifier:
- Not required for PSS-011 because no DataTable page is created.
- If a future UI screen is added under this pack by explicit scope change, the pack must be revised with `golden_reference: slim` or `compact`, the required Golden Reference file set, and `verify_datatable_page.py` expectations.

## Validation Rules
| Field | Required | Format/Rule | DB-level | Pre-check |
|---|---|---|---|---|
| LookupType | Yes if persisted | Max 64; Platform system type only | Unique composite with `Code` | Reject unknown type |
| Code | Yes | Max 64; normalize per type; currency uppercase ISO-style; locale BCP-style; timezone stable ID | Unique composite with `LookupType` | Duplicate check |
| Name | Yes | Max 200; localized display if culture-aware | None | Validator |
| Value | Yes | Max 128; stable machine value | None | Defaults to `Code` when omitted |
| Description | No | Max 500 | None | Validator |
| Group | No | Max 100 | Indexed only if required by UI grouping | Validator |
| SortOrder | Yes | Integer, default 0 | Sort index | Validator |
| IsActive | Yes | Boolean, default true | Read filter | Validator/domain default |
| Metadata | No | Small dictionary; no secrets; no tenant-specific payload | None | Reject oversized or unknown metadata if persisted |

Endpoint-level validation:
- Unknown lookup keys return 404 or a controlled 400 with `Response<T>`; they must not return an empty success silently.
- Currency lookup must not include blank or duplicate codes.
- Locale lookup must include at least Platform-supported `en` and `tr`.
- Timezone lookup must include `UTC`.
- Tenant tier lookup must align with Platform tenant/subscription tier vocabulary, not ERP customer classification.
- Subscription cycle lookup must align with Platform subscription lifecycle vocabulary and not accounting invoice periods.

## Failure Path to Verify
- Missing or unknown lookup key:
  - Expected: controlled 400/404 response; no unhandled exception.
- Duplicate persisted lookup code:
  - Expected: 409 conflict or seed/index failure caught during startup/test; duplicate is not returned to consumers.
- Currency endpoint unavailable from frontend consumer:
  - Expected: Subscription Plan form does not silently fall back to stale hardcoded list; it shows a controlled empty/error state and logs a warning server-side.
- Unauthorized Platform lookup request where auth is required:
  - Expected: 401/403; no lookup data is returned.
- Anonymous request to a non-public lookup endpoint:
  - Expected: 401/403 after `[AllowAnonymous]` is removed or narrowed.
- Feature category lookup when no active category exists:
  - Expected: successful empty list; consumer handles empty state.
- Timezone provider failure:
  - Expected: controlled failure with log entry; no partial corrupt cache entry.
- Cache stale after feature-category update:
  - Expected: either explicit invalidation or expiry within documented TTL; stale category does not persist beyond the accepted window.
- Gateway route missing:
  - Expected: smoke test via Gateway returns 404/502 and blocks completion until route is fixed by integration-agent.

## Authorization Convention
- Platform service permission format: `Platform.Lookups.Read`.
- Platform actor policy: `[Authorize(Policy = "PlatformActor")]` for Platform/Admin system lookup endpoints.
- Public/bootstrap exceptions must be documented per endpoint. `[AllowAnonymous]` is not a blanket default for this controller.
- If a lookup endpoint is consumed by tenant provisioning before authentication, it must expose only non-sensitive public options and remain read-only.
- Mutating endpoints are out of scope. If introduced later, use `Platform.Lookups.Create`, `Platform.Lookups.Update`, `Platform.Lookups.Delete`, and `Platform.Lookups.BulkDelete` with a separate approved UI/admin pack.

Authorization/public matrix:

| Endpoint | Intended Consumer | Auth Policy | AllowAnonymous | Rationale |
|---|---|---|---|---|
| `GET /api/lookups/currencies` | Platform Subscription Plans, Tenant provisioning defaults | `[Authorize(Policy = "PlatformActor")]` by default | No | Currency is Platform/Admin configuration support and must not be exposed broadly unless a future bootstrap need is approved. |
| `GET /api/lookups/locales` | Platform Tenant create/edit and settings defaults | `[Authorize(Policy = "PlatformActor")]` by default | No | Locale options are Platform Admin setup data; public language switch data should use a separate public UI resource flow. |
| `GET /api/lookups/languages` | Alias for locale consumers if retained | `[Authorize(Policy = "PlatformActor")]` by default | No | Alias must inherit `/locales` auth and response shape. |
| `GET /api/lookups/timezones` | Platform Tenant create/edit and settings defaults | `[Authorize(Policy = "PlatformActor")]` by default | No | Timezone options are provisioning support for authenticated Platform Admin flows. |
| `GET /api/lookups/countries` | Platform Tenant provisioning/support screens only | `[Authorize(Policy = "PlatformActor")]` by default | No | Countries must not become a public Territory Reference API; any public country list requires a separate approved decision. |
| `GET /api/lookups/tenant-tiers` | Platform Tenant and subscription packaging screens | `[Authorize(Policy = "PlatformActor")]` + `Platform.Lookups.Read` | No | Tenant tier is Platform packaging vocabulary and should be protected. |
| `GET /api/lookups/feature-categories` | Platform Feature/Subscription management screens | `[Authorize(Policy = "PlatformActor")]` + `Platform.Lookups.Read` | No | Feature categories are Platform subscription packaging data. |
| `GET /api/lookups/module-catalog/domains` | Platform Module Catalog screens | `[Authorize(Policy = "PlatformActor")]` + `Platform.Lookups.Read` | No | Module catalog vocabulary is Platform/Admin system configuration. |
| `GET /api/lookups/module-catalog/services` | Platform Module Catalog screens | `[Authorize(Policy = "PlatformActor")]` + `Platform.Lookups.Read` | No | Module service vocabulary is Platform/Admin system configuration. |

Public/bootstrap rule:
- PSS-011 does not approve any new anonymous lookup endpoint.
- Existing controller-level `[AllowAnonymous]` must be removed or narrowed. Any retained anonymous endpoint requires a code comment, an acceptance test, and a future pack revision that names the exact public consumer and data exposure rationale.

## Gateway / API Routing Decision
Decision: no new Gateway route is expected if the existing route remains valid.

Current route observed:
- `gateway/Diten.ApiGateway/ocelot.json` contains `/api/lookups/{everything}` routed to Platform service port 5057 for `GET` and `OPTIONS`.

Acceptance impact:
- Verify the route still exists and is ordered correctly before completion.
- If the route is missing, too narrow, or needs additional HTTP methods for a future mutation scope, route edits are not owned by this module implementation task. They must be assigned to integration-agent or explicitly approved because `ocelot.json` is a protected path.

## Domain Invariants
Bu kurallar **her zaman** doğru kalmalı — kod, test ve review hepsi bunları korumalı:

- Bir lookup type için aktif kayıt yoksa endpoint **boş liste** döner (success), 200/empty. Silent fallback YASAK.
- `currencies` listesi `Code` alanında **duplicate ASLA bulunamaz** (case-insensitive).
- `IsActive=false` veya soft-deleted kayıtlar default read query'sinde **DÖNDÜRÜLEMEZ**.
- Platform lookup payload'ları (request/response) `TenantId` alanı **içermez**.
- `locale` lookup'ı `en` ve `tr` kayıtlarını **HER ZAMAN** içerir (Platform UI hard dependency).
- `timezones` lookup'ı **`UTC` kaydını her zaman içerir**.
- Tek environment içinde timezone ID format'ı **karışık olamaz** (ya tamamı IANA ya tamamı Windows ID).
- Cache hit ile cache miss **aynı response shape**'i döner; cache layer alan kaybetmez/eklemez.
- Tüm lookup endpoint'leri **read-only**; HTTP `POST/PUT/PATCH/DELETE` route bile **kayıtlı değildir** (405 Method Not Allowed).
- `LookupType` değerleri Platform-system kapsamında kalır; tenant business taxonomy ASLA buradan oluşturulmaz.

## Forbidden Operations
Bu modül kapsamında **ASLA implement edilmeyecek** olanlar (kapsam dışı değil — açıkça yasaklı):

- `DELETE /api/lookups/...` endpoint'i — silme operasyonu yok.
- `POST /api/lookups/...`, `PUT /api/lookups/...`, `PATCH /api/lookups/...` — mutation yok.
- Bulk-update / bulk-delete endpoint'leri.
- UI'dan lookup kaydı ekleme/silme (Platform Admin CRUD formu).
- Razor sayfası, navigation menü öğesi, DataTable verifier hedefi oluşturma.
- Tenant-scoped lookup yazma (`TenantId` alanı taşıyan kayıt).
- Yeni `[AllowAnonymous]` endpoint açma (mevcut blanket `[AllowAnonymous]` daraltılır; **eklenmez**).
- Currency / locale / timezone listesini controller içine hardcoded array olarak yazma (refactor'da bunu kaldırıyoruz; geri konulamaz).
- ERP master-data alanlarına (account, customer, vendor, territory) genişleme.
- API versioning prefix'i ekleme (`/api/v1/...`) — bu MOD-0032 Gateway Hardening'in işi.

## Past Incidents to Avoid
Daha önceki geliştirmelerde yaşanmış ve **bu pack'te tekrarlanmaması** gereken sorunlar:

- **Anonymous data leak:** Controller-level `[AllowAnonymous]` ile non-public lookup'lar yetkisiz tüketildi. → Bu pack: endpoint başına auth kararı zorunlu, blanket attribute kaldırılacak.
- **Stale hardcoded fallback:** Subscription Plan form'unda lookup API başarısız olunca eski `USD/EUR/TRY/GBP` listesi sessizce gösteriliyordu; gerçek currency güncellemeleri görünmedi. → Bu pack: fallback kaldırılır, controlled error/empty state gösterilir.
- **Response shape drift:** Bazı lookup endpoint'leri `{Code,Name,Value}`, bazıları `{Code,Name}`, biri `{Id,Name}` döndü; consumer'lar her endpoint'e özel parser yazdı. → Bu pack: tek `LookupOptionDto`.
- **"Son kayıt silindi" sınıfı bug** (kardeş NEW-002 modülünde yaşandı: silinmemesi gereken son platform admin silindi): Sistem kayıtları için **idempotent destructive operasyon = invariant kontrolü olmadan reddedilir**. → Bu pack zaten **DELETE expose etmiyor** (Forbidden Operations); ama future "lookup management UI" pack'i yazıldığında `last-active-record guard` zorunlu olacak.
- **Gereksiz bulk-edit:** `form_field_count = 0` olan modüllerde geçmişte boş bulk-update endpoint'leri implement edildi → kullanılmadan kaldı, surface area büyüttü. → Bu pack: golden_reference=none olduğu için bulk endpoint yok.
- **Magic string drift:** Lookup `Code` değerleri (`"Active"`, `"USD"`, `"tr"`) handler/validator içinde string literal olarak yayıldı; refactor'da kırıldı. → Bu pack: handler'larda domain enum/constant zorunlu, magic string yasak.

## Negative Test Cases (Acceptance'a Bağlı)
Aşağıdaki testler **geçmediği sürece** acceptance kapanmaz:

- `DELETE /api/lookups/currencies/TRY` → **405 Method Not Allowed** (route kayıtlı bile olmamalı).
- `POST /api/lookups/currencies` (body ile) → **405**.
- Aynı `Code` ile iki currency seed/import edilirse → seed/migration **fail** (startup'ta yakalanmalı), duplicate consumer'a leak etmemeli.
- `Origin: tenant-x` header'lı GET → response payload'ı `TenantId` alanı **içermemeli** (assert: JSON'da `tenantId` key yok).
- Cache miss + provider exception → `Response<T>.Fail()` veya controlled empty; **kısmen dolu cache entry yazılmamalı**.
- Bilinmeyen lookup key → **404/400 controlled**, silent `[]` success değil.
- `[Authorize]` daraltıldıktan sonra anonymous istek non-public endpoint'e → **401/403**.

## Acceptance Criteria
- [ ] All in-scope lookup endpoints return the approved `LookupOptionDto` response shape with stable `Code`, `Name`, and `Value` fields, plus only approved optional fields such as `Group`, `SortOrder`, or `Metadata`.
- [ ] Existing consumers are either migrated to the approved `LookupOptionDto` shape or remain backward-compatible during rollout; no consumer is left parsing endpoint-specific anonymous shapes.
- [ ] Shape expectations are explicit for `currencies`, `module-catalog/domains`, `module-catalog/services`, `feature-categories`, `locales`, `timezones`, `tenant-tiers`, `subscription-cycles`, and `countries`.
- [ ] `GET /api/lookups/currencies` returns a stable list of unique, non-empty currency options with approved lookup DTO fields.
- [ ] Platform Subscription Plan currency loading no longer returns hardcoded fallback options such as `USD/EUR/TRY/GBP` after lookup failure.
- [ ] `GET /api/lookups/locales` returns at least `en` and `tr` with stable code/value fields and display names.
- [ ] `GET /api/lookups/timezones` returns `UTC` and sorted timezone options using one consistent timezone ID format.
- [ ] `GET /api/lookups/tenant-tiers` returns Platform tenant/subscription tier options only, with no ERP customer/account classification values.
- [ ] `GET /api/lookups/subscription-cycles` returns Platform subscription cadence options only.
- [ ] `GET /api/lookups/module-catalog/domains` and `GET /api/lookups/module-catalog/services` preserve existing consumers and response compatibility.
- [ ] `GET /api/lookups/feature-categories` uses the existing Platform feature category source of record when available and returns only active categories by default.
- [ ] `GET /api/lookups/countries` remains Platform provisioning/support data only; it is not Territory Reference, does not expand into MDM/Territory ownership, and keeps the approved stable lookup response shape.
- [ ] Lookup controller no longer uses broad `[AllowAnonymous]` for Platform-only/system endpoints unless a public bootstrap exception is explicitly documented in code and tests.
- [ ] Lookup reads use a documented cache strategy with per-type keys and expiration/invalidation behavior.
- [ ] No DTO/request/form payload includes `TenantId`.
- [ ] No code is added under MDM, ERP account/reference, tenant-side ERP, or other domain service folders.
- [ ] Existing Gateway `/api/lookups/{everything}` route is verified through Gateway port 5000.
- [ ] No new Platform Admin page, navigation item, DataTable, or Razor layout change is created.

## Test Expectations
Backend build:
- `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug`

Frontend build, required because Platform consumers change:
- `dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug`

Gateway build/route check:
- `dotnet build gateway/Diten.ApiGateway/Diten.ApiGateway.csproj -c Debug`
- Verify `gateway/Diten.ApiGateway/ocelot.json` still contains `/api/lookups/{everything}` with `GET` and `OPTIONS`.
- Gateway smoke base URL: `http://localhost:5000/api/lookups`.
- Smoke these exact Gateway URLs:
  - `GET http://localhost:5000/api/lookups/currencies`.
  - `GET http://localhost:5000/api/lookups/locales`.
  - `GET http://localhost:5000/api/lookups/timezones`.
  - `GET http://localhost:5000/api/lookups/tenant-tiers`.
  - `GET http://localhost:5000/api/lookups/subscription-cycles`.
  - `GET http://localhost:5000/api/lookups/feature-categories`.
  - `GET http://localhost:5000/api/lookups/countries`.
  - `GET http://localhost:5000/api/lookups/module-catalog/domains`.
  - `GET http://localhost:5000/api/lookups/module-catalog/services`.

Backend tests:
- Unit tests for lookup provider normalization and duplicate filtering.
- Unit tests for cache hit/miss behavior and per-lookup cache keys.
- Unit tests for unknown lookup key failure path.
- Integration tests for lookup endpoints via Platform API.
- Integration or smoke tests for Gateway route pass-through.

Required negative and boundary tests:
- Unauthorized request to each non-public lookup endpoint returns 401/403 and does not leak lookup data.
- Invalid or unknown lookup key returns controlled 400/404, not silent success with `[]`.
- Disabled, inactive, or soft-deleted lookup items are not returned by default read endpoints.
- Countries lookup cannot be used or extended as MDM/Territory Reference; tests assert it remains Platform provisioning/support only and does not introduce MDM/Territory paths or ownership.
- Response shape drift is rejected: all in-scope endpoints return the approved `LookupOptionDto` fields and no endpoint returns ad hoc `{Id,Name}` or `{Code,Name}`-only payloads.
- Cache consistency is verified: cache hit and cache miss return the same response shape, and cache invalidation/expiry works for data-backed feature categories.
- `DELETE /api/lookups/currencies/TRY` returns 405 Method Not Allowed.
- `POST /api/lookups/currencies` returns 405 Method Not Allowed.
- Duplicate persisted or seeded `Code` values fail validation/seed checks and do not leak duplicate options to consumers.
- Lookup responses do not contain `TenantId` even when tenant-origin headers are present.
- Provider exception during cache miss returns a controlled failure or approved empty state and does not write a partially populated cache entry.

Frontend verifier:
- DataTable verifier is not required because PSS-011 creates no DataTable page.
- If scope changes to add a Platform Admin DataTable screen, run:
  - `python3 .antigravity/scripts/verify_datatable_page.py . --area Platform --module Lookups --reference slim|compact`

RESX/localization check:
- If visible frontend strings are added, create/update only Platform `en` and `tr` resource files.
- Confirm no hardcoded fallback display text is introduced for lookup failure states.

Smoke tests:
- Platform Subscription Plan create/edit loads currency options from lookup endpoint.
- Tenant create/edit lookup loading still works for countries, currencies, and timezones through Gateway/proxy.
- Module Catalog form still loads domain and service lookup options.
- Auth-required endpoint behavior returns expected 401/403 when called without valid Platform context, except documented public endpoints.

## Ready-for-dev Checklist
- [ ] User confirms `PSS-011` ID and branch name.
- [ ] User confirms `shell: none` backend/API-only decision.
- [ ] User confirms `entity_base: GlobalEntity` for any persisted Platform system lookup records.
- [ ] User confirms no Platform Admin CRUD UI is in scope.
- [ ] User confirms MDM reference modules remain out of scope.
- [ ] User confirms allowed lookup types for MVP: Currency, Locale/Language, Timezone, Tenant Tier, Feature Category, Module Domain, Module Service, Subscription Cycle.
- [ ] Public vs Platform-auth-only endpoint list is reviewed before implementation.
- [ ] Cache TTL/invalidation policy is accepted.
- [ ] Gateway route ownership remains integration-agent if changes are needed.
- [ ] Status is changed from `draft` to `approved` or `ready-for-dev` before `@orchestrator` starts coding.

## Implementation Notes
- Current critical file: `services/Diten.Platform/src/Diten.Platform.API/Controllers/LookupsController.cs`.
- Current controller already exposes currencies, countries, timezones, module-catalog domains, and module-catalog services, but it returns anonymous objects and is broadly `[AllowAnonymous]`.
- Master Plan still lists timezone as missing; implementation should verify current code against expected route, response shape, auth, caching, and Gateway behavior rather than assuming it is complete.
- `frontend/Diten.Web/Controllers/Platform/SubscriptionPlansController.cs` currently contains a hardcoded currency fallback. That fallback must be removed or replaced with a controlled error/empty-state flow.
- `countries` endpoint is allowed only as Platform tenant-provisioning support. It must not become Territory Reference ownership.
- If `FeatureCategory` is already owned by PSS-007, PSS-011 should consume it as a lookup source and not fork the entity or repository.
- Platform lookup values used in business logic should also have Domain enums/constants where applicable; avoid magic strings in handlers.
- Use structured parsing/providers for locale/timezone/currency sources instead of ad hoc string manipulation when a platform API exists.

## Follow-up Items
- MDM domain should receive separate module packs for General Reference, Financial Reference, Territory Reference, and ERP Account/classification references.
- API versioning (`/api/v1/...`) remains deferred to MOD-0032 Gateway Hardening and should not be introduced only for this module.
- A future Platform Admin lookup-management UI can be proposed if business users need editable system lookup records; that future pack must choose `shell: platform-admin` and Slim/Compact based on real form fields.
- Feature category cache invalidation may be revisited after PSS-007 reaches final done status.
- Billing-specific subscription cadence semantics remain part of MOD-0299 and should not expand this pack into invoicing behavior.
