---
id: MOD-0033-FU01
name: Tenant Quota Governance UI
slug: tenant-quota-governance-ui
domain: platform-shared-services
status: review
owner: module-pack-author
branch: feature/pss/mod-0033-consumer-quota-model
parent_module: MOD-0033
deprecated_aliases:
  - MOD-0046-QG
  - MOD-0046-tenant-quota-governance-ui
started: 2026-05-12
target: 2026-05-20
form_field_count: 0
golden_reference: not_applicable
---

# MOD-0033-FU01 - Tenant Quota Governance UI

This module pack is structured as six execution documents inside one tracked pack file:

1. `module-brief.md`
2. `execution-pack.md`
3. `acceptance-criteria.md`
4. `repo-scope.md`
5. `test-notes.md`
6. `notes.md`

---

# module-brief.md

## Module Summary
Tenant Quota Governance UI adds a read-only quota usage section to Tenant Management under:

`/Platform/Tenants/Details/{tenantId}` > `Commercial` > after `Module Entitlements`.

The UI consumes the MOD-0033 Consumer / Quota Model read-only quota status contracts and presents tenant quota limits, current usage, period information, warning state, and limit state in user-friendly language.

This pack is a MOD-0033 feature slice rendered inside the MOD-0046 Tenant Core UI surface. It does not replace MOD-0033 backend quota enforcement and does not own subscription lifecycle behavior.

## Purpose
Platform administrators need to inspect how much of each plan-backed quota a tenant has used without reading technical quota keys or backend payloads directly.

The MVP is read-only:
- Show quota limits from the tenant's active or trial subscription plan.
- Show current usage and usage percentage.
- Show warning and limit-exceeded states.
- Show quota period for resettable quotas such as monthly API calls.
- Keep API call gateway enforcement clearly outside MVP.

## Status Decision
`status: review`.

Rationale: implementation evidence exists for the read-only Tenant Quota Governance UI/proxy slice, and post-fix Platform/Gateway smoke is `PASS-with-gaps`. The slice is not `done` because live authenticated Web-cookie proof remains blocked and positive quota-row data was not available. Gateway route availability is proven through the existing `/api/platform/tenants/{tenantId}/quotas` path; `ocelot.json` was not edited.

Implementation reconciliation:
- Permission alignment fix committed locally as `44eb63fe`: Web same-origin quota proxy now checks canonical `platform.tenants.quotas.read`, matching Platform quota read endpoints.
- Focused validation passed: `frontend/Diten.Web` build succeeded, Platform permission/alias/quota focused tests passed `35/35`, and `git diff --check` passed.
- Post-fix read-only smoke result on 2026-08-07: Gateway and direct Platform quota list returned `200` with empty `data: []`; `users.max` read returned expected `404 QUOTA_USAGE_NOT_FOUND`.
- Remaining closeout gaps: RabbitMQ unavailable made Platform health degraded (`503`), Web login did not provide reusable curl cookies for `/Platform/Tenants/{tenantId}/QuotaStatus`, no safe restricted actor was available, and the tenant used had no quota rows.

## Ownership and Boundaries
Owned by this pack:
- Tenant Details `Commercial` tab quota governance section.
- Tenant Quota Governance section inserted after `Module Entitlements`.
- Read-only quota status frontend DTO/view model.
- Same-origin frontend proxy action for tenant quota status, if not already present.
- Tenant quota governance JavaScript view/controller module.
- `en` and `tr` localization resource entries for quota labels, states, and messages.

Not owned by this pack:
- MOD-0033 quota consume, release, reset, enforcement, or atomic update behavior.
- Subscription lifecycle ownership.
- Manual quota override editor.
- Billing, purchase, upgrade, invoice, payment, or quota add-on flows.
- Gateway API call throttling, request counting, or enforcement.
- Gateway `ocelot.json` route ownership.
- Full quota dashboard outside Tenant Details.

## Owned Objects
- `TenantQuotaGovernanceResponse` frontend response DTO/view model.
- `TenantQuotaGovernanceItemViewModel` frontend item DTO/view model.
- Tenant Details Commercial quota section partial or inline section, following existing Tenant Details composition.
- Same-origin frontend proxy endpoint, recommended: `GET /Platform/Tenants/{tenantId}/QuotaStatus`.
- Tenant quota governance JavaScript module under `frontend/Diten.Web/wwwroot/assets/js/Platform/Tenants/**`.
- `en` and `tr` resource keys under the existing Tenant resource structure.

---

# execution-pack.md

## Architecture
- Frontend: Razor MVC in `frontend/Diten.Web`.
- Target shell: existing Tenant Details shell and `Commercial` tab.
- Layout: preserve the existing Tenant Details layout; do not use `frontend/Diten.Web/Views/Shared/_Layout.cshtml`.
- Data access: browser code must call a same-origin frontend proxy endpoint only.
- Service access: frontend server/proxy calls must go through Gateway port `5000`, not direct service ports.
- Backend contract source: MOD-0033 read-only quota status API.
- Localization: this pack is scoped to `en` and `tr` only.

## Source Contracts
Expected Platform service contracts from MOD-0033:

- `GET /api/platform/tenants/{tenantId}/quotas`
- `GET /api/platform/tenants/{tenantId}/quotas/{quotaKey}`

Expected quota status fields:
- `TenantId`
- `QuotaKey`
- `CurrentValue`
- `LimitValue`
- `UsagePercent`
- `IsWarning`
- `IsLimitExceeded`
- `PeriodStart`
- `PeriodEnd`
- `Source`
- `SubscriptionId`
- `PlanId`
- `OverrideSource`
- `WarningNotificationSentForPeriod`
- `LimitBreachNotificationSentForPeriod`
- `LastWarningNotifiedAtUtc`
- `LastLimitBreachNotifiedAtUtc`

## Implementation Start Sequence
Implementation must start with dependency verification before UI coding:

1. Verify Gateway access to `GET /api/platform/tenants/{tenantId}/quotas`.
2. If the Gateway route is missing or cannot reach the MOD-0033 read-only endpoint, stop implementation and report an integration-agent dependency. Do not modify `gateway/Diten.ApiGateway/**/ocelot.json` in this pack.
3. If the Gateway route is available, verify the existing Tenant Details Commercial read permission key and Commercial tab composition.
4. If the permission key cannot be found, stop and report a blocker. Do not create a new permission in this pack.
5. If the Commercial `Module Entitlements` section/partial cannot be found, stop UI insertion work and report the current Commercial composition with a proposed insertion point. Do not invent a new Commercial design.
6. After these checks pass, implement the frontend proxy and read-only UI section.

## Frontend Proxy Contract
The browser must call only the same-origin frontend proxy endpoint.

Recommended endpoint:

`GET /Platform/Tenants/{tenantId}/QuotaStatus`

If the repo has an established Tenant Details proxy naming convention, use that convention instead, but keep the endpoint same-origin and tenant-scoped.

Proxy behavior:
- Validate route `tenantId`.
- Enforce the same authorization boundary as Tenant Details commercial read access.
- Call Gateway port `5000` server-side.
- Call `GET /api/platform/tenants/{tenantId}/quotas`.
- Map backend response into `TenantQuotaGovernanceResponse`.
- Reject or suppress rendering if any returned quota item `TenantId` does not match the route `tenantId`.
- Never proxy internal consume, release, reset, sync, or recalculate mutation endpoints for this MVP.

Response DTO examples:

`TenantQuotaGovernanceResponse`:
- `TenantId`
- `Items`
- `State`
- `Message`
- `LoadedAtUtc`

`TenantQuotaGovernanceItemViewModel`:
- `TenantId`
- `QuotaKey`
- `DisplayLabel`
- `CurrentValue`
- `FormattedCurrentValue`
- `LimitValue`
- `FormattedLimitValue`
- `UsagePercent`
- `IsWarning`
- `IsLimitExceeded`
- `Status`
- `PeriodStart`
- `PeriodEnd`
- `Source`
- `OverrideSource`
- `ApiCallsMvpNote`

Error mapping:
- `401` / `403` -> unauthorized state.
- `404` -> quota contract/gateway route missing state or no quota data state, depending on response shape.
- `409` -> tenant mismatch or configuration conflict state.
- `503` / timeout -> degraded/error state.
- Unexpected response shape -> controlled error state.

Raw backend exception text must not be displayed.

## UI Placement
Add `Tenant Quota Governance` after the existing `Module Entitlements` tab/section in:

`/Platform/Tenants/Details/{tenantId}` > `Commercial`.

The UI must remain part of Tenant Details. It must not introduce a standalone quota dashboard route in MVP.

Implementation must locate the existing Commercial tab `Module Entitlements` section or partial file and insert the new quota section after it. If that section cannot be located, implementation must not invent a new layout; it must report the current Commercial composition and propose an insertion point.

## Quota Presentation
Technical quota keys must not be the main user-facing labels.

Required label mapping:

| Quota Key | User Label | Display Requirements |
|---|---|---|
| `users.max` | Users | Current / Limit, percentage, warning/limit state. |
| `storage.gb.max` | Storage | Current GB / Limit GB, percentage, warning/limit state. |
| `api.calls.per.month` | API Calls This Month | Current / Limit, period, reset status, non-technical MVP note that live gateway enforcement is outside this scope. |
| `modules.max` | Enabled Modules | Current active/enabled modules / Limit, percentage, warning/limit state. |

Technical keys may be retained in data attributes, diagnostics, or optional developer-facing metadata, but they must not be the primary visible text.

## Visual Behavior
Each quota item must show:
- Friendly label.
- Current value.
- Limit value.
- Usage percentage.
- Progress bar.
- Status badge.
- Warning state at or above the MOD-0033 warning threshold.
- Limit exceeded or at-limit state when applicable.
- Period start and period end only when the quota is period-based.
- Source or override source as small secondary admin-support text or tooltip where useful.

UI rules:
- Use a compact card/list layout aligned with the existing Commercial tab density.
- Progress bar is supporting context only; it must not be the only status indicator.
- Warning and limit states must not be represented by color alone; badge/text is required.
- Storage values must be formatted as GB.
- API call values must be formatted as whole numbers.
- API call quota must not imply live gateway enforcement is active.

Recommended status language:
- `Healthy`
- `Warning`
- `At limit`
- `Over limit`
- `Configuration missing`
- `Subscription inactive`

## State Handling
The section must define controlled states:
- Loading state while quota status is being fetched.
- Empty state when no quota rows are available.
- Missing configuration state for `QUOTA_CONFIGURATION_MISSING`.
- Error/degraded state when Gateway route is missing or MOD-0033 quota endpoint returns `404`, `503`, timeout, or invalid response shape.
- Tenant mismatch state when API response `TenantId` does not match route `tenantId`.
- Unauthorized or expired-token state.
- Inactive subscription read-only inspection state when the API returns status data for admin inspection.

## RBAC / Visibility
The section must be visible only to:
- Authorized Platform Admin users.
- Authorized Commercial Admin users.
- Users with the existing Tenant Details commercial read permission.

Authorization must not rely on frontend visibility alone. The frontend proxy and any backend read-only endpoint used by this UI must also enforce authorization.

Verified permission key: `platform.tenants.quotas.read`. The Web same-origin quota proxy and Platform quota read endpoints are aligned on the canonical read key; legacy `platform.tenants.quotas.view` remains Platform-side alias compatibility only.

## Localization
Localization scope is `en` and `tr`.

Minimum resource keys:
- `tenant.commercial.quota.title`
- `tenant.commercial.quota.description`
- `tenant.commercial.quota.users`
- `tenant.commercial.quota.storage`
- `tenant.commercial.quota.apiCallsThisMonth`
- `tenant.commercial.quota.enabledModules`
- `tenant.commercial.quota.status.healthy`
- `tenant.commercial.quota.status.warning`
- `tenant.commercial.quota.status.atLimit`
- `tenant.commercial.quota.status.overLimit`
- `tenant.commercial.quota.status.configurationMissing`
- `tenant.commercial.quota.status.subscriptionInactive`
- `tenant.commercial.quota.empty`
- `tenant.commercial.quota.error`
- `tenant.commercial.quota.unauthorized`
- `tenant.commercial.quota.loading`
- `tenant.commercial.quota.apiCallsMvpNote`

## Admin Actions Decision
MVP default is read-only.

`Sync limits from subscription` and `Recalculate usage` are not included in this implementation scope. They may be proposed as separate future admin actions only after explicit approval.

If these actions are approved later, they must:
- Be explicit admin actions with confirmation.
- Use Gateway/proxy access only.
- Never call internal quota mutation endpoints directly from browser JavaScript.
- Be covered by separate acceptance criteria before implementation.

## Runtime Constraints
- Browser JavaScript must never call service ports `5056`, `5057`, or `5058` directly.
- Browser JavaScript must not call internal quota consume/release/reset endpoints.
- Read-only quota status calls must use the same-origin frontend proxy.
- Frontend server/proxy calls must use Gateway port `5000`.
- UI must not mutate quota usage.
- UI must not infer subscription lifecycle decisions; it only displays state returned by the backend.
- API call quota enforcement remains outside this pack's MVP.
- Gateway `ocelot.json` must not be modified by this pack. Missing route support is an integration-agent dependency/blocker.

## Entity Fields
This pack does not create a new persistence entity.

Frontend display model fields:

| Field | Type | Rule |
|---|---|---|
| TenantId | `Guid` | Required; route tenant id must match displayed tenant. |
| QuotaKey | `string` | Required internally; not the primary visible label. |
| DisplayLabel | `string` | Required; localized `en`/`tr`. |
| CurrentValue | `decimal` | Required; formatted by quota type. |
| FormattedCurrentValue | `string` | Required for display. |
| LimitValue | `decimal?` | Missing value shows `Configuration missing`. |
| FormattedLimitValue | `string` | Required when limit exists. |
| UsagePercent | `decimal?` | Required when current and limit are available. |
| IsWarning | `bool` | Controls warning badge and progress style. |
| IsLimitExceeded | `bool` | Controls limit badge and over-limit style. |
| Status | `string` | Required localized status key/value. |
| PeriodStart | `DateTimeOffset?` | Shown only for period-based quotas. |
| PeriodEnd | `DateTimeOffset?` | Shown only for period-based quotas. |
| Source | `string?` | Optional support metadata. |
| SubscriptionId | `Guid?` | Optional support metadata. |
| PlanId | `Guid?` | Optional support metadata. |
| OverrideSource | `string?` | Shown when an override is active. |
| WarningNotificationSentForPeriod | `bool` | Optional status detail. |
| LimitBreachNotificationSentForPeriod | `bool` | Optional status detail. |
| LastWarningNotifiedAtUtc | `DateTimeOffset?` | Optional status detail. |
| LastLimitBreachNotifiedAtUtc | `DateTimeOffset?` | Optional status detail. |

## CQRS / API Changes
No new backend CQRS object is expected if MOD-0033 read-only endpoints are available through Gateway.

If frontend proxy actions are missing, add frontend-only proxy actions in the existing tenant details controller surface. These proxy actions must call Gateway port `5000`.

If Platform service read-only quota endpoints are missing or incomplete, backend work must stay limited to read-only contract completion and must not alter MOD-0033 mutation/enforcement behavior.

If Gateway does not expose MOD-0033 read-only quota status endpoints, implementation must stop and report the integration-agent dependency. Do not modify `gateway/Diten.ApiGateway/**/ocelot.json` in this pack.

---

# acceptance-criteria.md

## Runtime Acceptance Criteria
- [ ] Tenant Details `Commercial` tab displays `Tenant Quota Governance` after `Module Entitlements`.
- [ ] The section loads quota status through the same-origin frontend proxy; browser JavaScript does not call service ports directly.
- [ ] The frontend proxy calls Gateway port `5000` server-side for `GET /api/platform/tenants/{tenantId}/quotas`.
- [ ] The section does not call internal consume, release, reset, sync, or recalculate mutation endpoints from browser JavaScript.
- [ ] The UI shows user-friendly labels instead of primary raw quota keys.
- [ ] `users.max` is displayed as `Users` with Current / Limit, usage percent, progress, warning state, and limit state.
- [ ] `storage.gb.max` is displayed as `Storage` with Current GB / Limit GB, usage percent, progress, warning state, and limit state.
- [ ] `api.calls.per.month` is displayed as `API Calls This Month` with Current / Limit, current period, reset/status context, and a non-technical note that live gateway enforcement is not part of MVP.
- [ ] `modules.max` is displayed as `Enabled Modules` with active/enabled module usage, limit, usage percent, warning state, and limit state.
- [ ] Current value, limit value, usage percent, period start/end, warning state, and limit exceeded state are visible when provided by the API.
- [ ] Period start/end are shown only for period-based quotas.
- [ ] Missing quota configuration or missing `LimitValue` is shown as `Configuration missing` for the affected row.
- [ ] Inactive subscription state is shown as read-only inspection state when status data is available.
- [ ] Loading state is visible while quota status is fetched.
- [ ] Empty state is visible when the tenant has no quota rows.
- [ ] Error/degraded state is visible when Gateway route is missing or quota status load fails.
- [ ] Tenant mismatch state is shown and data is not rendered when API response `TenantId` does not match route `tenantId`.
- [ ] Unauthorized or expired-token state is handled without exposing raw service errors.
- [ ] API call quota UI does not imply gateway enforcement or throttling is active.
- [ ] The section is visible only to authorized Platform Admin, Commercial Admin, or users with Tenant Details commercial read permission.
- [ ] Unauthorized users cannot access quota status through the frontend proxy.
- [ ] Permission checks are enforced server-side, not only through frontend visibility.
- [ ] ASSUMPTION: Existing Tenant Details commercial read permission will guard this section until final permission key is confirmed.
- [ ] Platform localization resources exist for `en` and `tr`.
- [ ] Required localization keys have `en` and `tr` parity.
- [ ] `_Layout.cshtml` is not used or modified.
- [ ] The existing Tenant Details shell is preserved.
- [ ] Full quota dashboard is not implemented in this pack.
- [ ] Manual override editor is not implemented in this pack.
- [ ] Billing purchase or upgrade flow is not implemented in this pack.
- [ ] Gateway throttling or API call enforcement is not implemented in this pack.

## Golden Flow Acceptance
- [ ] Platform Admin opens `/Platform/Tenants/Details/{tenantId}`.
- [ ] Platform Admin opens the `Commercial` tab.
- [ ] Existing `Module Entitlements` section remains unchanged.
- [ ] `Tenant Quota Governance` section appears below `Module Entitlements`.
- [ ] UI fetches quota status data through the same-origin frontend proxy.
- [ ] Quota rows render friendly label, current/limit, usage percent, progress bar, status badge, and period information where applicable.
- [ ] Page refresh loads the same read-only section correctly again.
- [ ] Browser network calls do not target service ports `5056`, `5057`, or `5058`.

## Failure Path Acceptance
- [ ] If Gateway route is missing, UI shows a controlled degraded/error state and does not show raw backend exception text.
- [ ] If MOD-0033 quota endpoint returns `404`, UI shows route/contract missing or no-data state according to response shape.
- [ ] If MOD-0033 quota endpoint returns `503` or times out, UI shows a controlled degraded/error state.
- [ ] If API response item `TenantId` does not match route `tenantId`, UI does not render quota data and shows controlled mismatch error.
- [ ] If `LimitValue` or quota configuration is missing, affected quota row shows `Configuration missing`.
- [ ] If the user is unauthorized or the token is expired, UI shows a user-friendly permission/session message.
- [ ] API call quota copy does not state or imply that gateway enforcement is active.

## DataTable / Golden Reference
- `form_field_count: 0`
- `golden_reference: not_applicable`

This module is not a CRUD DataTable module.

---

# repo-scope.md

## Repo Scope
- `execution/domains/platform-shared-services/module-packs/MOD-0033-FU01-tenant-quota-governance-ui.md`
- `frontend/Diten.Web/Views/Platform/Tenants/**`
- `frontend/Diten.Web/wwwroot/assets/js/Platform/Tenants/**`
- `frontend/Diten.Web/Controllers/Platform/TenantsController.cs` or the existing tenant details proxy/controller file.
- `frontend/Diten.Web/Resources/Views/Platform/Tenants/**`
- `services/Diten.Platform/**` only if an approved implementation discovers a missing read-only quota endpoint/proxy contract required for this UI.

## Protected Paths
- `.antigravity/**`
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`
- `gateway/Diten.ApiGateway/**/ocelot.json`
- MOD-0033 backend quota enforcement behavior.
- MOD-0033 quota consume/release/reset behavior.
- Subscription lifecycle ownership behavior.
- `services/Diten.AuthService/**` unless separately approved.
- `services/Diten.DevEnablementService/**`
- `services/Diten.EnterpriseStrategyService/**`

## Out Of Scope
- Full quota dashboard.
- Manual quota override editor.
- Quota consume, release, or reset UI.
- Sync limits from subscription action.
- Recalculate usage action.
- Billing purchase, plan upgrade, invoice, payment provider, or quota add-on flows.
- Gateway API request counting, throttling, and enforcement.
- Gateway configuration changes.
- Subscription lifecycle state transitions.
- New quota definition catalog.
- Direct browser calls to service ports `5056`, `5057`, or `5058`.

## Dependencies
- MOD-0033 Consumer / Quota Model for read-only quota status API.
- MOD-0046+ Tenant Core UI Extensions for Tenant Details shell and Commercial tab composition.
- Existing Subscription Plan UI/data model because quota limits originate from `SubscriptionPlan.DefaultQuotas`.
- Existing Tenant Management details route and authorization.
- Integration-agent if Gateway route support for MOD-0033 quota status endpoints is missing.

---

# test-notes.md

## Build Expectations
- `dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug`
- `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug` only if backend read-only quota contract work is changed.

## UI Smoke Tests
- Open Tenant Details.
- Open the `Commercial` tab.
- Confirm `Tenant Quota Governance` appears after `Module Entitlements`.
- Confirm loading state renders before data arrives.
- Confirm empty state renders when no quota rows exist.
- Confirm controlled error state renders when quota status load fails.
- Confirm unauthorized or expired-token state is handled.
- Confirm tenant mismatch response does not render quota data.
- Confirm quota percent and progress bar render correctly for configured quotas.
- Confirm warning and limit badges render from `IsWarning` and `IsLimitExceeded`.
- Confirm warning and limit states have badge/text, not color-only meaning.
- Confirm raw quota keys are not the main visible labels.
- Confirm browser network calls do not target service ports `5056`, `5057`, or `5058`.
- Refresh the page and confirm the read-only section loads correctly again.

## Failure Path Tests
- Gateway route missing or `404` response shows controlled route/contract missing or no-data state.
- MOD-0033 endpoint `503` or timeout shows degraded/error state.
- API response item with mismatched `TenantId` shows controlled mismatch error and does not render quota rows.
- Missing `LimitValue` or configuration shows row-level `Configuration missing`.
- Unauthorized or expired-token response shows user-friendly permission/session state.
- API call quota text does not imply live gateway enforcement.

## Localization Checks
- `en` and `tr` resource keys exist for:
  - `tenant.commercial.quota.title`
  - `tenant.commercial.quota.description`
  - `tenant.commercial.quota.users`
  - `tenant.commercial.quota.storage`
  - `tenant.commercial.quota.apiCallsThisMonth`
  - `tenant.commercial.quota.enabledModules`
  - `tenant.commercial.quota.status.healthy`
  - `tenant.commercial.quota.status.warning`
  - `tenant.commercial.quota.status.atLimit`
  - `tenant.commercial.quota.status.overLimit`
  - `tenant.commercial.quota.status.configurationMissing`
  - `tenant.commercial.quota.status.subscriptionInactive`
  - `tenant.commercial.quota.empty`
  - `tenant.commercial.quota.error`
  - `tenant.commercial.quota.unauthorized`
  - `tenant.commercial.quota.loading`
  - `tenant.commercial.quota.apiCallsMvpNote`
- `en` and `tr` resources have parity for all keys added by this pack.

## Regression Checks
- Existing Tenant Details tabs still render.
- Existing `Module Entitlements` tab/section remains in place before `Tenant Quota Governance`.
- Existing subscription/commercial UI behavior is unchanged except for the new quota governance section.
- No gateway config file is modified.
- MOD-0033 consume/release/reset/enforcement behavior is untouched.
- Subscription lifecycle behavior is untouched.
- Billing and payment flows are untouched.

## Output Contract
Implementation handoff must report:
- Changed files summary.
- Golden flow proof.
- Failure path proof.
- Build commands and results.
- UI smoke results.
- Verified permission key: `platform.tenants.quotas.read`.
- Verified Gateway route:
- Verified Commercial insertion point file:
- Verified proxy endpoint:
- Network proof screenshot/log summary:
- Browser network proof: no direct `5056`, `5057`, or `5058` calls.
- Localization parity proof for `en`/`tr`.
- Boundary/SoR check: MOD-0033 enforcement, subscription lifecycle, billing, and gateway config untouched.
- Open `ASSUMPTION` / `🔴 TBD` list.

---

# notes.md

## Implementation Notes
- Keep the first implementation read-only.
- Reuse the existing Tenant Details shell and styling conventions.
- Favor compact quota summary cards or rows that match existing Commercial tab density.
- Use progress bars and status badges, but do not turn the section into a full dashboard.
- Progress bars must not carry status meaning alone; pair them with badge/text.
- Format storage values as GB.
- Format API call values as whole numbers.
- Show period dates only for period-based quotas.
- Show source or override source as secondary admin-support text or tooltip.
- For inactive subscriptions, show the state clearly without offering mutation actions.
- For missing quota configuration, show a controlled message that the quota setup is incomplete.
- Do not expose raw backend exception text in the UI.
- Do not log or display sensitive token data.

## Admin Action Recommendation
`Sync limits from subscription` and `Recalculate usage` remain outside this MVP.

Recommended future handling:
- Add each action as a separate approved scope item.
- Require confirmation modals and admin authorization.
- Route through Gateway/proxy only.
- Never call internal quota mutation endpoints directly from browser JavaScript.
- Add dedicated audit and smoke acceptance criteria before implementation.

## ASSUMPTION
- Existing Tenant Details controller/proxy conventions can host `GET /Platform/Tenants/{tenantId}/QuotaStatus` or an equivalent repo-standard action.
- MOD-0033 response shape includes enough quota status fields to construct `TenantQuotaGovernanceResponse`.
- Source and override source labels can be displayed as secondary admin-support text without adding a new backend contract.

## 🔴 TBD
- Live authenticated Web-cookie proof for `/Platform/Tenants/{tenantId}/QuotaStatus` remains pending; curl login returned JSON but no reusable auth cookie.
- Positive quota-row render proof remains pending because the safe existing tenant used in smoke had no `quota_usages` rows.
- Restricted non-bypass actor proof remains pending because no safe existing restricted token/session was available.
- Platform health remains degraded when RabbitMQ is unavailable; this is an environment gap for full live closeout.

## Final Decisions
- This is a MOD-0046+ Tenant Core UI extension.
- This pack is in review / pending-web-smoke after local implementation evidence and post-fix smoke.
- This pack is read-only MVP.
- Technical quota keys are not primary user-facing labels.
- Browser calls only same-origin frontend proxy endpoints.
- Frontend proxy calls Gateway port `5000`.
- No direct browser calls to service ports `5056`, `5057`, or `5058`.
- No `ocelot.json` changes are allowed in this pack.
- No manual override editor is included.
- No sync/recalculate action is included.
- No billing or purchase flow is included.
- No gateway API enforcement or throttling is included.
- MOD-0033 consume/release/reset/enforcement behavior is untouched.
- Subscription lifecycle ownership is untouched.
- DataTable / Golden Reference is not applicable.
- Verified permission key is `platform.tenants.quotas.read`; legacy `.view` remains Platform-side alias compatibility only.
- Post-fix smoke is `PASS-with-gaps` as of 2026-08-07: Platform/Gateway read path passed with empty data; Web-cookie proof remains pending.
