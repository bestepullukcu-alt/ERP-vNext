---
id: PSS-006
name: Tenant Subscription Plan Catalog
domain: platform-shared-services
service: Diten.Platform
status: approved
owner: module-pack-author
branch: feature/pss/pss-006-subscription-plans
started: 2026-04-30
target: 2026-05-21
ui_pattern: card-grid
datatable: false
golden_reference: none
---

# PSS-006 - Tenant Subscription Plan Catalog

## Purpose
Tenant Subscription Plan Catalog modulu, Platform Admin tarafindan tenantlara atanabilecek subscription plan kayitlarini yonetir ve Tenant Create akisi icin plan secimi/provisioning sozlesmesini hazirlar.

Bu pack plan katalog nesnesini tenant abonelik durumundan ayirir. `SubscriptionPlan` platform-level, MongoDB'de yonetilebilir bir katalog entity'sidir; tenant uzerindeki `PlanId`, `SubscriptionStatus`, `TrialStartDateUtc` ve `TrialEndDateUtc` ise tenant'in erisim/abonelik durumunu temsil eder.

## Business Rules
- Planlar statik enum olmayacak; MongoDB'de yonetilebilir platform-level katalog kayitlari olacak.
- MVP plan seti: `Free`, `Starter`, `Professional`, `Enterprise`.
- `Free` ayri bir "Trial" plani degildir; sureli ucretsiz erisim plani olarak modellenir.
- Free plan default duration is 14 days.
- MVP default: `Free` secilen tenant `SubscriptionStatus = Trialing` ile acilir.
- MVP default: backend `Free` icin `TrialStartDateUtc = now` ve `TrialEndDateUtc = now + 14 days` hesaplar.
- MVP default: `Starter`, `Professional`, `Enterprise` secilen tenantlar `SubscriptionStatus = Active` ile acilir.
- Gelecek uyumluluk: ucretli planlar da trial olarak acilabilmelidir, ornek `Plan = Starter`, `SubscriptionStatus = Trialing`, `TrialEndDateUtc = today + 14 days`.
- Trial suresi bitince otomatik billing/payment yapilmaz.
- Trial suresi bitince tenant `TrialExpired` durumuna alinabilir; MVP'de otomatik suspend yoktur.
- Platform Admin gelecek fazda `Extend`, `Convert to Paid`, `Suspend` aksiyonlarini kullanabilir.
- `TenantType` is tipi/tenant kategorisi icin kalmalidir; subscription lifecycle ayri `TenantSubscriptionStatus` ile temsil edilmelidir.

## In Scope
- `SubscriptionPlan` katalog entity contract'i.
- Plan create/update/list/detail/summary API contract'i.
- Activate/deactivate kararlari.
- Plan unique code, validation, Mongo index ve seed beklentileri.
- Platform Admin Subscription Plans card-grid UI planlamasi.
- Tenant Create ekranina daha sonra eklenecek plan selector ve quota/feature preview entegrasyon sozlesmesi.
- Plan secilince default quota ve feature/module entitlement uretimine hazir sozlesme.
- Audit event beklentileri.
- 7 dil localization beklentileri.

## Out of Scope
- Billing/payment gateway entegrasyonu.
- Invoice generation.
- Usage-based billing.
- Otomatik odeme alma.
- Trial expiry worker veya scheduler implementasyonu.
- Trial expiry sonrasi otomatik tenant suspend.
- Tenant provisioning implementasyonu bu pack'in ilk fazinda tamamlanmayacak; ayri faz olarak planlanacak.
- MVP delete endpoint'i.
- Plan delete davranisi. Delete daha sonra yalnizca soft delete olarak, dependency ve tenant assignment impact analizi sonrasinda tekrar degerlendirilebilir.
- DataTable sayfasi, DataTable v2 verifier ve GoldenReferenceSlim/Compact uygulamasi.
- Yeni CSS, inline style, local style block veya custom stylesheet.

## Owned Objects
- Domain/Persistence:
  - `SubscriptionPlan` aggregate.
  - Mongo collection: `platform_subscription_plans`.
- DTO/Contracts:
  - `SubscriptionPlanDto`
  - `SubscriptionPlanListItemDto`
  - `SubscriptionPlanSummaryDto`
  - `CreateSubscriptionPlanRequest`
  - `UpdateSubscriptionPlanRequest`
  - `SubscriptionPlanFilterRequest`
- Application Commands:
  - `CreateSubscriptionPlanCommand`
  - `UpdateSubscriptionPlanCommand`
  - `ActivateSubscriptionPlanCommand`
  - `DeactivateSubscriptionPlanCommand`
  - `SeedDefaultSubscriptionPlansCommand` or project-pattern seed initializer.
- Application Queries:
  - `GetSubscriptionPlansQuery`
  - `GetActiveSubscriptionPlansQuery`
  - `GetSubscriptionPlanByIdQuery`
  - `GetSubscriptionPlanSummaryQuery`
- Validation:
  - `SubscriptionPlan` create/update validation rules.
- Audit Events:
  - `SubscriptionPlanCreated`
  - `SubscriptionPlanUpdated`
  - `SubscriptionPlanActivated`
  - `SubscriptionPlanDeactivated`

## Integration Contracts / Later Phase Touchpoints
PSS-006 owns the plan catalog. It does not fully own Tenant lifecycle, `TenantQuota`, `TenantFeatureFlag`, tenant provisioning, billing, or trial expiry automation.

- Tenant extensions:
  - Existing `Tenant` entity icin `PlanId`, `SubscriptionStatus`, `TrialStartDateUtc`, `TrialEndDateUtc` alanlari.
  - `TenantSubscriptionStatus` enum: `Trialing`, `TrialExpired`, `Active`, `Suspended`, `Cancelled`.
- Quota/feature touchpoints:
  - `TenantQuota` veya mevcut quota yapisi ile default quota uretim contract'i.
  - `TenantFeatureFlag` veya plan included feature/module listesi ile default entitlement uretim contract'i.
- Tenant commands:
  - `AssignPlanToTenantCommand`
  - `ChangeTenantPlanCommand`
- Tenant queries:
  - `GetTenantPlanAndQuotaPreviewQuery`
  - `GetPlansForTenantCreateQuery`
- Tenant DTO/contracts:
  - `TenantPlanPreviewDto`
  - `PlanQuotaDto`
  - `PlanFeatureDto`
- Tenant audit events:
  - `TenantPlanAssigned`
  - `TenantPlanChanged`
  - `TenantTrialStarted`
  - `TenantTrialExpired`

## Consumed Objects / Services
- Existing Platform Tenant registry from `MOD-0044` and Tenant UI from `MOD-0046`.
- Existing Module Catalog from `PSS-005` for `IncludedModuleKeys` alignment and future module entitlement generation.
- Existing Platform API `Response<T>` envelope and `CustomBaseController` style.
- Existing Gateway URL proxy pattern in `frontend/Diten.Web`; frontend calls Gateway port `5000` only.
- Existing Platform Admin authorization policy and `[HasPermission]` convention.
- Existing localization standard: `en`, `fr`, `es`, `zh`, `ar`, `ru`, `tr`.

## Repository Scope
- Planning-only current change:
  - `execution/domains/platform-shared-services/module-packs/PSS-006-tenant-subscription-plan-catalog.md`
- Later approved implementation scope:
  - `services/Diten.Platform/src/Diten.Platform.Domain/**`
  - `services/Diten.Platform/src/Diten.Platform.Application/**`
  - `services/Diten.Platform/src/Diten.Platform.Infrastructure/**`
  - `services/Diten.Platform/src/Diten.Platform.API/**`
  - `services/Diten.Platform/tests/**`
  - `services/Diten.Platform.Common/**` only if cross-service stable contracts are required.
  - `frontend/Diten.Web/Controllers/Platform/SubscriptionPlansController.cs`
  - `frontend/Diten.Web/Views/Platform/SubscriptionPlans/**`
  - `frontend/Diten.Web/wwwroot/assets/js/Platform/SubscriptionPlans/**`
  - `frontend/Diten.Web/Resources/Views/Platform/SubscriptionPlans/**`
  - `frontend/Diten.Web/Views/Platform/Tenants/Create.cshtml` only in tenant integration phase.
  - `frontend/Diten.Web/wwwroot/assets/js/Platform/Tenants/**` only in tenant integration phase.
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
- Any backend/frontend/gateway/runtime file during this planning-only task.

## Backend Contract
### Entity Fields
`SubscriptionPlan` contract:

| Field | Type | Rules |
|---|---|---|
| Base | `GlobalEntity` | Platform-level catalog record; tenant-owned degildir. |
| Id | `Guid` | System generated. |
| Code | `string` | Required, uppercase, unique, normalized. |
| Name | `string` | Required, user-facing localized/display value support planned. |
| Description | `string?` | Optional. |
| IsActive | `bool` | Inactive plans cannot be selected for new tenant create. |
| IsDefault | `bool` | Supported in MVP. At most one active default plan may exist. Default seeded plan must be `FREE`. |
| SortOrder | `int` | Default `0`; must be `>= 0`. |
| PriceMonthly | `decimal?` | Nullable; cannot be negative when provided. |
| PriceYearly | `decimal?` | Nullable; cannot be negative when provided. |
| Currency | `string?` | Required only when `PriceMonthly` or `PriceYearly` has a value; suggested ISO code. |
| IsTrialPlan | `bool` | True means default provisioning should create Trialing subscription. |
| TrialDurationDays | `int?` | Required and `> 0` when `IsTrialPlan=true`; null/ignored otherwise. |
| DefaultQuotas | `object/list` | Structured quota defaults; no ad hoc string blob. |
| IncludedFeatures | `string[]` | Feature keys included by default. |
| IncludedModuleKeys | `string[]` | Module keys aligned with Module Catalog where available. |
| CreatedAtUtc | `DateTimeOffset` | System generated. |
| UpdatedAtUtc | `DateTimeOffset?` | System updated. |

Tenant extension contract:

| Field | Type | Rules |
|---|---|---|
| PlanId | `Guid?` | References active `SubscriptionPlan`; required for new tenant create after integration phase. |
| SubscriptionStatus | `TenantSubscriptionStatus` | Subscription/access state, separate from `TenantType` and tenant lifecycle status. |
| TrialStartDateUtc | `DateTimeOffset?` | Backend generated for trialing tenant. |
| TrialEndDateUtc | `DateTimeOffset?` | Backend generated from selected plan trial duration unless explicitly extended by admin. |

`TenantType` decision:

| Concept | Responsibility |
|---|---|
| `TenantType` | Business/category type such as `Customer`, `Demo`, `Internal`; existing values must be reviewed and migrated carefully. |
| `TenantSubscriptionStatus` | Access/subscription state: `Trialing`, `TrialExpired`, `Active`, `Suspended`, `Cancelled`. |

### Commands and Queries
- Plan commands and queries listed in `Owned Objects` are required for MVP plan catalog.
- Tenant integration commands/queries are planned for later phase and must not block initial catalog implementation.
- MVP does not expose delete. Plans are managed with Activate / Deactivate only.
- All commands return existing `Response<T>` envelope.
- Controllers align with current Platform API style: `[Route("api/platform/...")]`, `[Authorize(Policy = "PlatformActor")]`, `[HasPermission(...)]`, MediatR command/query dispatch, and `CustomBaseController`/current action result envelope behavior.

## Frontend Contract
### Routes
- Preferred route: `/Platform/SubscriptionPlans`
- Controller convention should align with existing `frontend/Diten.Web/Controllers/Platform/ModuleCatalogController.cs` route pattern.
- Legacy redirects are optional only if existing navigation requires them.

### Pages
- Index page:
  - `Views/Platform/SubscriptionPlans/Index.cshtml`
  - Uses `_LayoutBackbone.cshtml`.
  - Page header with title `Subscription Plans`, subtitle `Manage subscription plans and pricing tiers`, and Create Plan button.
  - Summary cards: Total Plans, Active Plans, Trial Plans, Paid Plans.
  - Plan card grid with name, code, status badge, description, monthly price, yearly price, features, quota highlights, edit/deactivate action icons.
  - Loading, empty, and error states.
  - No raw technical token shown to user.
- Create/Edit:
  - Full page forms are the preferred decision.
  - Rationale: the form has more than 8 user-editable fields and includes structured quota/features/modules; a modal would be cramped and harder to validate.
  - `Create.cshtml`, `Edit.cshtml`, optional `Details.cshtml`, and shared `_Form.cshtml` are allowed after approval.
- Tenant Create integration:
  - Add plan selector/card in later phase.
  - Show selected plan quota preview.
  - If selected plan is trial: display `Trial End Date` / `Free Access End Date` based on backend-calculated date.
  - If selected plan is paid: default status is `Active`.
  - Future optional mode: paid plan can be opened as `Trialing`.

### Styling Rules
- This is not a DataTable module.
- `GoldenReferenceSlim` and `GoldenReferenceCompact` do not apply.
- Use Bootstrap grid/card/badge/button/form classes.
- Use Sneat/Bootstrap existing utility classes only.
- No extra CSS.
- No inline style.
- No local `<style>` block.
- No new custom stylesheet.
- Use existing layout/component language.
- Frontend API calls must go through Gateway port `5000`; no direct calls to Platform service port `5057`.

## Gateway Contract
- Gateway route is expected for MVP base path `/api/platform/subscription-plans`.
- If an existing catch-all Platform route already covers this path, no route change is needed.
- If the route is missing, implementation must coordinate with integration-agent; this module pack does not authorize direct `ocelot.json` edits.
- Frontend proxy must call `GatewayUrl` configuration, matching current Module Catalog and Tenants patterns.

## UI Design Decision
- Decision: card-grid management screen, not DataTable.
- Reason: subscription plans are low-count, visual pricing/feature cards are clearer than tabular management.
- Create/Edit decision: full page forms, not modal.
- Reason: fields include pricing, trial rules, quota defaults, features and module keys; full pages provide better validation and future expansion.
- UI must stay within Bootstrap/Sneat existing classes; no bespoke CSS surface is allowed.

## Trial / Free Plan Decision
- `Free` is modeled as a normal `SubscriptionPlan` with `IsTrialPlan = true`, `PriceMonthly = 0`, `PriceYearly = 0`, and `TrialDurationDays = 14`.
- Selecting `Free` during tenant create later produces `SubscriptionStatus = Trialing`.
- The backend calculates `TrialStartDateUtc = now` and `TrialEndDateUtc = now + 14 days`; UI must not be the source of truth for trial dates.
- Paid plans default to `SubscriptionStatus = Active`.
- The model must still support paid-plan trialing in a later workflow.

## Validation Rules
- `Code` required.
- `Code` stored uppercase and unique.
- `Code` normalization should follow existing catalog normalization patterns where feasible.
- `Name` required.
- `PriceMonthly` is nullable; when provided it cannot be negative.
- `PriceYearly` is nullable; when provided it cannot be negative.
- `Currency` is required only when `PriceMonthly` or `PriceYearly` has a value.
- `SortOrder` cannot be negative.
- If `IsTrialPlan = true`, `TrialDurationDays` is required and must be `> 0`.
- If `IsTrialPlan = false`, `TrialDurationDays` must be null or ignored by backend behavior.
- `IsDefault` is supported in MVP.
- At most one active default plan may exist.
- Default seeded plan must be `FREE`.
- If another plan is marked default, backend must unset or block conflicting default based on existing project convention.
- If no existing convention exists, prefer blocking conflict with a controlled validation error.
- `Free` seed/default must have zero prices, `IsTrialPlan = true`, and `TrialDurationDays = 14`.
- Tenant create must not proceed when selected `PlanId` does not exist.
- Tenant create must not proceed when selected plan is inactive.
- Duplicate `SubscriptionPlan.Code` must be blocked before persistence when possible and by Mongo unique index at persistence level.

## API Endpoint Proposal
MVP route decision is explicit. `/api/platform/subscription-plans` owns the plan catalog API. Do not use `/api/admin/plans` for MVP.

| Method | Route | Scope |
|---|---|---|
| GET | `/api/platform/subscription-plans` | MVP |
| GET | `/api/platform/subscription-plans/active` | MVP |
| GET | `/api/platform/subscription-plans/summary` | MVP |
| GET | `/api/platform/subscription-plans/{id}` | MVP |
| POST | `/api/platform/subscription-plans` | MVP |
| PUT | `/api/platform/subscription-plans/{id}` | MVP |
| POST | `/api/platform/subscription-plans/{id}/activate` | MVP |
| POST | `/api/platform/subscription-plans/{id}/deactivate` | MVP |

MVP does not expose delete. Plans are managed with Activate / Deactivate only. Delete may be reconsidered later as soft delete only after dependency and tenant assignment impact analysis.

Tenant-specific plan assignment/change APIs remain under tenant admin routes in a later phase:

| Method | Route | Scope |
|---|---|---|
| GET | `/api/platform/subscription-plans/for-tenant-create` | Later phase |
| GET | `/api/admin/tenants/plan-preview?planId={id}` | Later phase |
| POST | `/api/admin/tenants/{id}/plan` | Later phase |
| POST | `/api/admin/tenants/{id}/plan/change` | Later phase |

Permissions should follow current naming style, for example:

- `Platform.SubscriptionPlans.Read`
- `Platform.SubscriptionPlans.Create`
- `Platform.SubscriptionPlans.Update`
- `Platform.SubscriptionPlans.Activate`
- `Platform.SubscriptionPlans.Deactivate`

Note: `Platform.SubscriptionPlans.Assign` belongs to tenant plan assignment/change phase, not MVP plan catalog CRUD.

## Mongo Index Plan
- `SubscriptionPlan.Code` unique.
- `SubscriptionPlan.IsActive`.
- `SubscriptionPlan.IsTrialPlan`.
- `SubscriptionPlan.SortOrder`.
- Required in MVP: ensure at most one active default plan.
  - Prefer a partial/filtered unique index equivalent for `IsDefault = true AND IsActive = true` if the project's Mongo persistence patterns support it.
  - If partial index is not feasible in the current repository patterns, enforce via validation + a transactional/consistent update strategy and add a non-unique query index on `IsDefault`.
- Tenant indexes if tenant fields are added:
  - `Tenant.PlanId`
  - `Tenant.SubscriptionStatus`
  - `Tenant.TrialEndDateUtc`

## Seed Data Plan
- Seed default plans only through the existing project seed pattern discovered during implementation.
- MVP seed candidates:
  - `FREE`: active, default plan (`IsDefault = true`), `PriceMonthly = 0`, `PriceYearly = 0`, `Currency = USD` or the project default currency, `IsTrialPlan = true`, `TrialDurationDays = 14`.
  - `STARTER`: active, paid plan.
  - `PROFESSIONAL`: active, paid plan.
  - `ENTERPRISE`: active plan; may have custom pricing, so `PriceMonthly` and `PriceYearly` may be null.
- Seed operation must be idempotent by `Code`.
- Seed must not overwrite admin-edited pricing/features unless an explicit maintenance command is approved.

## Acceptance Criteria
- [ ] Module pack remains `status: draft` until user approval (status is not changed automatically).
- [ ] `SubscriptionPlan` is implemented as a platform-level MongoDB catalog entity, not a static enum.
- [ ] Tenant subscription fields are separated from `SubscriptionPlan` catalog fields.
- [ ] `TenantSubscriptionStatus` exists or equivalent contract is documented with `Trialing`, `TrialExpired`, `Active`, `Suspended`, `Cancelled`.
- [ ] `TenantType` is not reused as subscription/access status.
- [ ] `PriceMonthly` and `PriceYearly` are nullable but cannot be negative when provided.
- [ ] `Currency` is required when `PriceMonthly` or `PriceYearly` has a value.
- [ ] `Free` has `IsTrialPlan = true`, `TrialDurationDays = 14`, `PriceMonthly = 0`, `PriceYearly = 0`, and `Currency = USD` or the project default currency.
- [ ] `Enterprise` supports custom pricing by allowing null `PriceMonthly` and `PriceYearly`.
- [ ] `IsDefault` is supported in MVP and only one active default plan is allowed.
- [ ] `FREE` is the default seeded plan.
- [ ] MVP tenant integration behavior is `Free = Trialing`, paid plans = `Active`.
- [ ] Future paid-plan trialing remains possible without introducing a separate Trial plan.
- [ ] Duplicate `SubscriptionPlan.Code` is rejected and protected by unique Mongo index.
- [ ] Trial plan without positive `TrialDurationDays` is rejected.
- [ ] Negative price values are rejected when provided.
- [ ] Deactivated plans cannot be selected for new tenant create in the tenant integration phase.
- [ ] Missing `PlanId` blocks tenant create in the tenant integration phase.
- [ ] No DELETE endpoint is exposed in MVP.
- [ ] Platform API exposes plan list/detail/create/update/activate/deactivate/summary endpoints under Gateway-backed `/api/platform/subscription-plans`.
- [ ] API responses use existing `Response<T>` envelope and Platform controller conventions.
- [ ] MVP audits plan create/update/activate/deactivate.
- [ ] Tenant plan assignment/change/trial audit events are required only in tenant integration phase.
- [ ] Activate/deactivate permissions exist and are enforced.
- [ ] Assign permission is later-phase only (`Platform.SubscriptionPlans.Assign`).
- [ ] UI uses card-grid plan layout, not DataTable.
- [ ] UI uses `_LayoutBackbone.cshtml`.
- [ ] UI has summary cards, Create Plan button, plan cards, loading, empty and error states.
- [ ] UI uses only Bootstrap/Sneat existing classes and no extra CSS/inline style/custom stylesheet.
- [ ] Frontend calls Gateway port `5000` only and never calls service port `5057` directly.
- [ ] Localization resources exist for 7 required cultures for labels, validation messages, statuses and actions.
- [ ] Billing, payment, invoice generation, usage billing, automatic suspend and trial expiry worker remain unimplemented.

## Test Expectations
- Backend build after approved implementation:
  - `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug`
- Backend tests:
  - Unit tests for code normalization, duplicate code blocking, price validation, trial duration validation, active/inactive selection guards, one-default-plan guard if enabled.
  - Unit tests for `PriceMonthly`/`PriceYearly` nullable behavior and negative value rejection.
  - Unit tests for `Currency` required only when price exists.
  - Unit tests for Enterprise custom pricing allowing null price values.
  - Unit tests for default plan rule: only one active default; `FREE` seeded default; conflicting default behavior follows convention or blocks with controlled validation error.
  - Unit tests for Free plan provisioning derivation in tenant integration phase: `Trialing`, generated `TrialStartDateUtc`, generated `TrialEndDateUtc = now + 14 days`.
  - Unit tests for paid plan default derivation: `Active`, no default trial dates unless explicit trial mode is requested.
  - Integration/API tests for list, active list, summary, detail, create, update, activate, deactivate, authorization failure, and response envelope shape.
  - API test proving `DELETE /api/platform/subscription-plans/{id}` is not exposed in MVP.
  - Tenant integration tests for missing/inactive `PlanId` rejection when that phase is implemented.
- Frontend build after approved UI implementation:
  - `dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug`
- Gateway build/validation if route changes are required:
  - `dotnet build gateway/Diten.ApiGateway/Diten.ApiGateway.csproj -c Debug`
- JavaScript:
  - `node --check` for new Subscription Plans JS files.
- Browser smoke:
  - Open `/Platform/SubscriptionPlans`.
  - Verify summary cards, create/edit flow, persisted card reload, activate/deactivate behavior, loading/empty/error states.
  - Verify Tenant Create later shows active Free/Starter plans and preview behavior through Gateway-backed calls.
- DataTable:
  - No DataTable verifier is expected because this module explicitly does not use DataTable.

## Implementation Phases
1. Phase 0 - Approval gate:
   - User reviews this draft.
   - Verify business decision is documented: `Free` default trial duration = 14 days.
   - Confirm deactivate-only MVP lifecycle remains acceptable.
2. Phase 1 - Backend catalog foundation:
   - Add `SubscriptionPlan` entity, repository, indexes, validators, commands, queries and API endpoints.
   - Add seed/default plan strategy.
   - Add audit events for plan changes.
3. Phase 2 - Subscription Plans UI:
   - Add `/Platform/SubscriptionPlans` card-grid pages and Gateway proxy.
   - Add localization resources.
   - Implement create/edit full-page forms without custom CSS.
4. Phase 3 - Tenant Create integration:
   - Add plan selector/card and quota preview.
   - Add backend validation for active `PlanId`.
   - Generate tenant subscription status and trial dates.
5. Phase 4 - Admin subscription operations:
   - Plan change/assignment endpoints.
   - Extend trial / convert to paid / suspend flow planning.
6. Future phase - Trial expiry:
   - Trial expiry worker and notifications, only after explicit approval.

## Implementation Notes
- Existing tenant contracts currently expose `Plan`/`Tier` style strings; implementation must migrate carefully toward `PlanId` and `SubscriptionStatus` without breaking existing UI contracts.
- Existing Tenant entity is `GlobalEntity`; plan catalog should also be `GlobalEntity` because it is a platform-wide catalog.
- `DefaultQuotas`, `IncludedFeatures`, and `IncludedModuleKeys` should use structured DTOs/arrays, not comma-separated strings.
- `IncludedModuleKeys` should align with `PSS-005` Module Catalog codes where available.
- Existing Gateway route coverage must be checked before requesting integration-agent route work.
- This pack does not authorize direct edits to `gateway/Diten.ApiGateway/**/ocelot.json`.
- Delete may be reconsidered later as soft delete only after dependency and tenant assignment impact analysis.

## ASSUMPTIONS
- Next local PSS module id after `PSS-005` is `PSS-006`; repo contains mixed `MOD-XXXX` and `PSS-XXX` packs, and this pack follows the most recent PSS sequence plus module-pack-standard format.
- Platform service remains the system of record for subscription plan catalog and tenant plan assignment.
- `Free` default duration is 14 days.
- MVP uses deactivate-only plan lifecycle and does not expose delete.
- Current `TenantType` values may need later cleanup because existing code includes `Trial` and `Paid`; this pack recommends moving access state to `TenantSubscriptionStatus`.

## 🔴 TBD
- Confirm exact quota schema for `DefaultQuotas`.
- Confirm exact feature key registry for `IncludedFeatures`.
- Confirm whether `IncludedModuleKeys` must reference `ModuleCatalogItem.ModuleCode` strictly in MVP.
- Confirm repository naming convention for permission keys if current repo differs from proposed `Platform.SubscriptionPlans.*` naming.
- Confirm whether route `/api/platform/subscription-plans` is already covered by Gateway configuration.
- Confirm final `TenantType` enum values: `Customer`, `Demo`, `Internal` vs current implementation values.

## User Approval Checklist
- [ ] Module purpose and scope are correct.
- [ ] `PSS-006` id and filename are acceptable.
- [ ] `Free` default trial duration = 14 days is approved.
- [ ] Deactivate-only MVP lifecycle is approved.
- [ ] Card-grid UI and full-page Create/Edit forms are approved.
- [ ] No-DataTable and no-custom-CSS decisions are approved.
- [ ] Tenant subscription state separation from `TenantType` is approved.
- [ ] Gateway route expectation is approved.
- [ ] Out-of-scope billing/payment/trial-worker constraints are approved.
- [ ] User may change status from `draft` to `approved` or `ready-for-dev` after accepting this checklist.

## Approval Readiness
This module pack is ready for user approval when:
- User accepts the approval checklist.
- Remaining TBD items are accepted as non-blocking for Phase 1 backend catalog foundation.
- Development will start with Phase 1 only: backend catalog foundation.
- Tenant Create integration remains Phase 3.
- Trial expiry worker remains future phase.
