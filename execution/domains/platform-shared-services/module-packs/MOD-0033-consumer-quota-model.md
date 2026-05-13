---
id: MOD-0033
name: Consumer / Quota Model
domain: platform-shared-services
status: in-progress
owner: module-pack-author
branch: feature/pss/mod-0033-consumer-quota-model
started: 2026-05-12
target: 2026-05-26
form_field_count: 0
golden_reference: not_applicable
---

# MOD-0033 - Consumer / Quota Model

This module pack is structured as six execution documents inside one tracked pack file:

1. `module-brief.md`
2. `execution-pack.md`
3. `acceptance-criteria.md`
4. `repo-scope.md`
5. `test-notes.md`
6. `notes.md`

---

# module-brief.md

## Purpose
Consumer / Quota Model provides runtime quota enforcement for tenant usage limits defined by subscription plans. It belongs to Platform & Shared Services and is implemented primarily in `Diten.Platform`.

The module enforces plan-backed quotas for tenant operations such as user creation, document/file upload, API calls, and module entitlement assignment. It does not create a new quota definition catalog.

## Status Decision
`status: approved`.

Rationale: scope, golden flow, bounded MVP behavior, CQRS/API contracts, error codes, audit payload, runtime acceptance criteria, and tests are defined enough for implementation. Any gateway configuration work remains a coordinated dependency and must not be implemented by this pack without integration-agent handling.

## Source Of Truth
Quota limits are resolved from existing subscription and tenant configuration sources in this order:

1. Tenant-specific quota override, if present.
2. Active `TenantSubscription` plan quota value from `SubscriptionPlan.DefaultQuotas`.
3. If no plan quota exists, do not fall back to an implicit system default; return `QUOTA_CONFIGURATION_MISSING`.

Manual quota overrides require an audit reason. The audit payload must identify the override source.

## TenantSubscription Lifecycle Policy
Quota service is not the owner of subscription lifecycle. It only resolves limits from an active or trial subscription and safely blocks mutating quota operations when subscription state is not eligible.

- Active subscription: quota enforcement works normally.
- Trial subscription: trial plan quota values apply; whichever plan backs the trial provides `SubscriptionPlan.DefaultQuotas`.
- Trial expired: quota service does not independently close tenant access. Access/subscription lifecycle remains owned by TenantSubscription lifecycle. Mutating consume operations must fail closed with `QUOTA_SUBSCRIPTION_INACTIVE` unless upstream lifecycle enforcement has already blocked the request.
- Cancelled, suspended, or expired subscription: quota service must not produce new usable limits for consume. Mutating consume/release/reset operations are blocked according to subscription policy with `QUOTA_SUBSCRIPTION_INACTIVE`.
- Read-only quota status queries may remain available for admin inspection.

## Owned Objects
- `QuotaUsage`
- `QuotaEvent`
- `IQuotaService`
- `QuotaStatus`

## Standard Quota Keys
- `users.max`
- `storage.gb.max`
- `api.calls.per.month`
- `modules.max`

## Quota Key Semantics
- `users.max`: maximum active tenant users or tenant-user assignments.
- `storage.gb.max`: maximum tenant-owned Document/File upload storage in MVP.
- `api.calls.per.month`: contract/seam for monthly API call usage; gateway request counting/enforcement is outside this pack's MVP implementation scope.
- `modules.max`: maximum active/enabled tenant module assignments.

`modules.max` does not limit the global Module Catalog size and does not decide which modules a plan entitles. Entitlement decisions belong to TenantModuleAssignment / plan entitlement structures. This quota only enforces the numeric upper limit. MVP counts only active/enabled tenant module assignments; disabled/removed assignments are not counted.

## MVP Storage Scope
`storage.gb.max` applies only to tenant-owned Document/File upload storage in MVP.

Out of MVP storage scope:
- Database size metering.
- Log storage metering.
- Cache storage metering.
- Analytics data storage metering.

When upload storage quota is full:
- New upload is blocked.
- File delete remains allowed.
- File download remains allowed.
- Plan upgrade remains allowed.

## Golden Flow
1. Platform Admin or system action reads quota limits from the tenant's active subscription plan.
2. Tenant currently has `15/15` usage for `users.max`.
3. Admin attempts to add the 16th user.
4. User create flow calls quota enforcement for `users.max`.
5. Quota service detects that the requested consume would exceed the limit.
6. User create is rejected.
7. `QuotaUsage.CurrentValue` is not increased.
8. `QuotaEvent` is written for the rejected attempt.
9. General Audit Trail receives a quota rejection audit event.
10. API returns a controlled limit-exceeded response.
11. UI shows a clear, user-facing quota limit message.

## UI Boundary
MVP implementation is backend-enforcement focused.

Allowed UI scope:
- Controlled limit-exceeded error messages for affected flows.
- Read-only quota status contract needed by future UI.

Deferred UI scope:
- Full quota dashboard.
- Future `Tenant Details > Commercial / Plan` read-only Quota Summary tab or section, linked to `MOD-0046+ Tenant Core UI Extensions`.

## API Calls Quota MVP Boundary
`api.calls.per.month` is included in this pack as a model, status, reset, event, and audit contract. Actual gateway-level request counting, throttling, rate limiting, and enforcement are not part of this pack's MVP implementation scope.

This pack must not change gateway config or `ocelot.json`. Gateway request counting/enforcement requires a separate integration-agent or gateway-owned implementation task.

## Notification Boundary
Quota module does not own Notification / Email Service behavior. It only emits notification event/command seams for `MOD-0027`.

- 80% usage creates soft warning state.
- 100% usage creates hard limit/breach state.
- Warning and breach notification state must be visible in quota status.
- Notification dispatch failure must not roll back a successful consume transaction.
- Notification failure must be recorded through quota event/audit; retry ownership belongs to Notification Service and/or Scheduler.

Dedup/cooldown policy:
- For the same `TenantId + QuotaKey + Period`, 80% warning notification is emitted at most once.
- For the same `TenantId + QuotaKey + Period`, 100% hard limit breach notification is emitted at most once.
- Notification state resets when a new period starts.
- Notification state may be re-evaluated when `LimitValue` changes because of plan upgrade/downgrade or override sync.

---

# execution-pack.md

## Architecture
- Storage: MongoDB, tenant-owned documents.
- Runtime pattern: CQRS/MediatR.
- API response: existing `Response<T>` envelope and `CustomBaseController` style.
- Auth: JWT + RBAC permission checks for user-facing quota status endpoints.
- Internal consume/release endpoints are not public user endpoints; they are for backend internal or service-to-service use.
- Frontend must use Gateway port `5000`; it must not call service ports directly.

## Entity Contracts
`QuotaUsage`:

| Field | Type | Rule |
|---|---|---|
| Base | `EntityBase` | Tenant-owned MongoDB entity. |
| Id | `Guid` | System generated. |
| TenantId | `Guid` | Required. |
| QuotaKey | `string` | Required; must match known quota key. |
| CurrentValue | `decimal` | Required; cannot be negative. |
| LimitValue | `decimal` | Required; resolved by source precedence. |
| PeriodStart | `DateTimeOffset` | Required for resettable quota windows. |
| PeriodEnd | `DateTimeOffset` | Required for resettable quota windows. |
| LastUpdatedUtc | `DateTimeOffset` | Updated on consume/release/reset/recalculate. |
| LastWarningNotifiedAtUtc | `DateTimeOffset?` | Set when warning notification seam is emitted for current period. |
| LastLimitBreachNotifiedAtUtc | `DateTimeOffset?` | Set when hard-limit breach notification seam is emitted for current period. |
| WarningNotificationSentForPeriod | `bool` | Prevents repeated 80% notifications in the same period. |
| LimitBreachNotificationSentForPeriod | `bool` | Prevents repeated 100% notifications in the same period. |
| IsDeleted | `bool` | Required soft-delete flag. |
| DeletedAt | `DateTimeOffset?` | Required when soft-deleted. |

`QuotaEvent`:

| Field | Type | Rule |
|---|---|---|
| Base | `EntityBase` | Tenant-owned MongoDB entity. |
| Id | `Guid` | System generated. |
| TenantId | `Guid` | Required. |
| QuotaKey | `string` | Required. |
| Delta | `decimal` | Requested or applied delta. |
| Reason | `string` | Required for manual override and operational source context. |
| Source | `string` | Required; examples: `UserCreate`, `FileUpload`, `GatewayApiCall`, `ModuleEntitlement`, `ManualOverride`, `ResetJob`. |
| OperationId | `string?` | Correlates consume/release for the same business operation. |
| SourceReference | `string?` | Stable source-side reference such as user id, file id, assignment id, or request id. |
| OccurredAtUtc | `DateTimeOffset` | Required. |
| IsRejected | `bool` | True when a consume attempt is denied. |
| ErrorCode | `string?` | Set for rejected or failed quota operations. |
| IsDeleted | `bool` | Required soft-delete flag. |
| DeletedAt | `DateTimeOffset?` | Required when soft-deleted. |

`QuotaStatus` must include:
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
- `OverrideSource`, if applicable.
- `WarningNotificationSentForPeriod`
- `LimitBreachNotificationSentForPeriod`
- `LastWarningNotifiedAtUtc`
- `LastLimitBreachNotifiedAtUtc`

## Service Contract
```csharp
Task<bool> TryConsumeAsync(Guid tenantId, string quotaKey, decimal amount, CancellationToken ct);
Task<QuotaStatus> GetStatusAsync(Guid tenantId, string quotaKey);
Task ReleaseAsync(Guid tenantId, string quotaKey, decimal amount);
```

Implementation may add richer internal result types, but public behavior must map to the error code catalog and `Response<T>` envelope.

## CQRS Contracts
- `GetTenantQuotaStatusQuery`
- `GetTenantQuotaStatusByKeyQuery`
- `InitializeTenantQuotasCommand`
- `SyncTenantQuotaLimitsFromSubscriptionCommand`
- `TryConsumeQuotaCommand`
- `ReleaseQuotaCommand`
- `ResetQuotaPeriodCommand`
- `RecalculateQuotaUsageCommand`

## API Contract Draft
- `GET /api/platform/tenants/{tenantId}/quotas`
- `GET /api/platform/tenants/{tenantId}/quotas/{quotaKey}`
- `POST /api/platform/internal/quotas/consume`
- `POST /api/platform/internal/quotas/release`
- `POST /api/platform/internal/quotas/reset-period`

Internal consume/release/reset endpoints must not be exposed as public UI endpoints. They are backend internal or service-to-service contracts and require internal authorization policy.

## Internal Endpoint Authorization Policy
Internal quota mutation endpoints use service authorization, not normal user RBAC.

Policy name options:
- `PlatformInternalOnly`
- `RequireInternalServiceToken`

Internal request requirements:
- Trusted internal service token/API key, mTLS identity, or equivalent service identity.
- Allowed service/client list validation.
- `TenantId` required.
- `CorrelationId` required or generated by middleware before handler execution.
- `ActorId` or `SystemActor` required.
- `Source` required.

UI must not call internal consume/release/reset endpoints directly. Public user tokens must not be accepted for internal mutation endpoints. Unauthorized internal calls return `403` or the platform canonical internal auth error. Failed internal auth must produce structured log/audit metadata, but sensitive token/API key values must never be logged or written to audit payloads.

User-facing quota status endpoints remain protected by JWT + RBAC permission checks.

## Atomic Consume Requirement
MongoDB consume must use a single atomic conditional update. Do not use a race-prone read-then-update pattern.

Filter must include:
- `TenantId` equals requested tenant.
- `QuotaKey` equals requested quota key.
- `IsDeleted == false`.
- `CurrentValue + requestedAmount <= LimitValue`.

Update must include:
- Increment `CurrentValue` by requested amount.
- Set `LastUpdatedUtc` to current UTC time.

If the atomic filter does not match:
- Return `QUOTA_LIMIT_EXCEEDED` when the usage row exists and the request would exceed the limit.
- Return `QUOTA_USAGE_NOT_FOUND` when no active usage state exists.
- Return `QUOTA_CONCURRENCY_CONFLICT` only when implementation can distinguish a concurrent write conflict from normal limit rejection.

## Failure Paths
- Concurrent user create requests must not over-consume; if only one quota slot remains, only one request succeeds.
- Limit exceeded must not increase `CurrentValue`.
- Unknown quota key returns `QUOTA_KEY_UNKNOWN`.
- Missing tenant id returns `QUOTA_TENANT_REQUIRED`.
- Missing subscription or missing plan quota returns `QUOTA_CONFIGURATION_MISSING` and fails closed.
- Inactive, cancelled, suspended, or expired subscription blocks mutating consume with `QUOTA_SUBSCRIPTION_INACTIVE`.
- Manual override without reason is rejected.
- Invalid release amount returns `QUOTA_RELEASE_INVALID_AMOUNT`.
- Release that would reduce usage below zero returns `QUOTA_RELEASE_EXCEEDS_CURRENT_USAGE`.
- Duplicate consume/release with the same operation reference must not double-apply mutation; return or treat as `QUOTA_DUPLICATE_OPERATION` according to existing command style.
- Missing required operation reference for idempotent mutation returns `QUOTA_OPERATION_REFERENCE_REQUIRED` when the operation type requires it.
- Period reset on a non-resettable or invalid quota returns `QUOTA_PERIOD_RESET_NOT_ALLOWED`.

## Error Code Catalog
- `QUOTA_USAGE_NOT_FOUND`
- `QUOTA_KEY_UNKNOWN`
- `QUOTA_LIMIT_EXCEEDED`
- `QUOTA_CONFIGURATION_MISSING`
- `QUOTA_TENANT_REQUIRED`
- `QUOTA_CONCURRENCY_CONFLICT`
- `QUOTA_RELEASE_INVALID_AMOUNT`
- `QUOTA_PERIOD_RESET_NOT_ALLOWED`
- `QUOTA_SUBSCRIPTION_INACTIVE`
- `QUOTA_INITIALIZATION_FAILED`
- `QUOTA_RECALCULATION_NOT_SUPPORTED`
- `QUOTA_OVERRIDE_REASON_REQUIRED`
- `QUOTA_LIMIT_SYNC_REQUIRED`
- `QUOTA_RELEASE_EXCEEDS_CURRENT_USAGE`
- `QUOTA_DUPLICATE_OPERATION`
- `QUOTA_OPERATION_REFERENCE_REQUIRED`

## Quota Initialization And Limit Sync
Quota usage initialization belongs to TenantSubscription activation/provisioning.

- On tenant subscription activation, read supported quota keys from the active plan's `SubscriptionPlan.DefaultQuotas`.
- Seed one `QuotaUsage` row per supported quota key.
- Initialization must be idempotent; existing active records are not duplicated.
- Lazy create may exist only as fallback. The primary flow is provisioning/subscription activation.
- On plan change, `SyncTenantQuotaLimitsFromSubscriptionCommand` updates `LimitValue` from the new plan or override source.
- Plan change preserves `CurrentValue`.
- If downgrade makes `CurrentValue > LimitValue`, existing tenant data is not deleted, new consume is blocked, quota status shows over-limit, and audit/event records are emitted.

## Recalculate Quota Usage Sources
`RecalculateQuotaUsageCommand` is tenant-scoped and requires `TenantId`.

- `users.max`: calculate from active tenant users or active tenant-user assignments.
- `storage.gb.max`: calculate from trusted Document/File metadata total size. Physical blob scan is not required for MVP.
- `modules.max`: calculate from active/enabled `TenantModuleAssignment` count.
- `api.calls.per.month`: if gateway/event counter integration does not exist, return `QUOTA_RECALCULATION_NOT_SUPPORTED`; once integrated, calculate from the gateway request counter/event store.

Recalculate compares computed value with `QuotaUsage.CurrentValue`. If different, it updates `CurrentValue`, writes `QuotaEvent`, and emits an audit record. Recalculate must be non-destructive: it must not delete tenant data or cross tenant boundaries.

## Release And Idempotency Policy
Release returns previously consumed usage back to the quota.

Release examples:
- User deleted or deactivated: release `users.max`.
- File deleted: release `storage.gb.max`.
- Module assignment disabled or removed: release `modules.max`.
- Failed business operation rollback: release a previously successful consume.

Release rules:
- Amount must be positive.
- Release must be tenant-scoped.
- Cross-tenant release is blocked.
- Release cannot reduce `CurrentValue` below zero.
- Release does not produce limit exceeded; invalid amount or insufficient consumed usage returns controlled error.
- Release should be idempotent by `OperationId` and/or `SourceReference`.
- Duplicate release for the same business operation must not reduce `CurrentValue` a second time.
- Consume and release events should be linkable through the same `OperationId`.
- If idempotency cannot be implemented in the first code pass, duplicate release risk is an implementation blocker and must be covered before production readiness.

## Audit Event Payload
Quota consume, release, reset, recalculate, reject, and override events must emit audit payload with:
- `TenantId`
- `QuotaKey`
- `CurrentValueBefore`
- `CurrentValueAfter`
- `LimitValue`
- `Delta`
- `Source`
- `Reason`
- `ActorId` or `SystemActor`
- `CorrelationId`
- `OccurredAtUtc`
- `SubscriptionId`
- `PlanId`
- `OverrideSource`, when present.
- `OperationId`
- `SourceReference`
- `NotificationState`, when present.
- `IdempotencyKey`, when present.
- `InternalCallerService`, for internal endpoint calls.
- `AuthorizationPolicy`, for internal endpoint calls.

## Dependencies
- `MOD-0026 Background Job Scheduler` for monthly reset execution.
- `MOD-0027 Notification / Email Service` for warning and breach notifications.
- `MOD-0021 General Audit Trail` for quota audit events.
- `MOD-0046+ Tenant Core UI Extensions` for future read-only quota summary UI.
- `PSS-006 Subscription Plan Catalog` because `SubscriptionPlan.DefaultQuotas` is the source of plan quota defaults.

---

# acceptance-criteria.md

## Runtime Acceptance Criteria
- [ ] Active subscription quota limits are resolved using tenant override then active plan `SubscriptionPlan.DefaultQuotas` precedence.
- [ ] Trial subscription uses the trial plan's quota values.
- [ ] Inactive, expired, cancelled, or suspended subscription blocks new mutating consume with `QUOTA_SUBSCRIPTION_INACTIVE`.
- [ ] Given tenant usage is `14/15` for `users.max`, when a user create consumes `1`, then the request succeeds and `CurrentValue` becomes `15`.
- [ ] Given tenant usage is `15/15` for `users.max`, when a user create consumes `1`, then the request is rejected with `QUOTA_LIMIT_EXCEEDED`.
- [ ] Limit-exceeded rejection does not increase `QuotaUsage.CurrentValue`.
- [ ] Limit-exceeded rejection writes `QuotaEvent` and emits General Audit Trail payload.
- [ ] Concurrent consume requests cannot push `CurrentValue` above `LimitValue`.
- [ ] Tenant subscription activation idempotently seeds `QuotaUsage` records for supported quota keys.
- [ ] Plan change syncs `LimitValue` from subscription/override source and preserves `CurrentValue`.
- [ ] Downgrade over-limit state does not delete existing data, but new consume is blocked and status shows over-limit.
- [ ] Unknown quota key is rejected with `QUOTA_KEY_UNKNOWN`.
- [ ] Quota operation without `TenantId` is rejected with `QUOTA_TENANT_REQUIRED`.
- [ ] Missing subscription or plan quota fails closed with `QUOTA_CONFIGURATION_MISSING`.
- [ ] Tenant-specific override takes precedence over plan default quota and requires audit reason.
- [ ] `storage.gb.max` full state blocks new Document/File upload.
- [ ] `storage.gb.max` full state does not block file delete, file download, or plan upgrade.
- [ ] Given tenant has 10 active module assignments and `modules.max` is 10, when admin enables one more module assignment, then request is rejected and assignment is not activated.
- [ ] `modules.max` only counts active/enabled `TenantModuleAssignment` records.
- [ ] `api.calls.per.month` quota status and period reset contract are supported.
- [ ] Gateway-level request counting/enforcement is not this pack's MVP implementation scope and requires a separate integration task.
- [ ] `api.calls.per.month` reset sets `CurrentValue` to `0` and opens a new period.
- [ ] Recalculate can correct `CurrentValue` from real sources for `users.max`, `storage.gb.max`, and `modules.max`.
- [ ] `api.calls.per.month` recalculation returns `QUOTA_RECALCULATION_NOT_SUPPORTED` when gateway counter integration is absent.
- [ ] Soft warning is reported at 80% usage and hard limit at 100% usage.
- [ ] First crossing of 80% threshold emits warning notification seam.
- [ ] 80% warning notification is not repeatedly emitted in the same quota period.
- [ ] First hard-limit/breach state at 100% emits breach notification seam.
- [ ] Notification failure does not roll back successful consume and leaves controlled audit/event evidence.
- [ ] File delete releases `storage.gb.max` and decreases `CurrentValue`.
- [ ] Duplicate release with the same `OperationId` does not decrease `CurrentValue` a second time.
- [ ] Release cannot reduce `CurrentValue` below zero.
- [ ] Cross-tenant release is blocked.
- [ ] Invalid release amount returns controlled error.
- [ ] Internal consume/release/reset endpoints are not exposed to external users as public endpoints.
- [ ] Internal consume endpoint without valid internal service authorization is rejected.
- [ ] Internal release/reset endpoints cannot be called with public user tokens.
- [ ] Internal request without `TenantId`, `Source`, or `CorrelationId` is rejected or completed only after middleware generates `CorrelationId`.
- [ ] User-facing status endpoints are protected by JWT + RBAC.
- [ ] Frontend quota-related calls use Gateway port `5000`, not direct service ports.

## Golden Flow Acceptance
- [ ] In the 15/15 `users.max` scenario, 16th user create is rejected through backend quota enforcement.
- [ ] UI receives a controlled response and displays a clear limit exceeded message.
- [ ] Quota usage remains unchanged after rejection.
- [ ] Rejection creates both `QuotaEvent` and audit record with `CorrelationId`.

## DataTable / Golden Reference
- `form_field_count: 0`
- `golden_reference: not_applicable`

This module is not a CRUD DataTable module. Future read-only quota summary UI belongs under `MOD-0046+ Tenant Core UI Extensions`.

---

# repo-scope.md

## In Scope
- `execution/domains/platform-shared-services/module-packs/MOD-0033-consumer-quota-model.md`
- `services/Diten.Platform/**`
- `frontend/Diten.Web/**` only for quota error display and future read-only quota status consumption if explicitly approved.
- `gateway/Diten.ApiGateway/**` only as a dependency seam for `api.calls.per.month` enforcement; protected gateway config ownership still applies.

## Protected Paths
- `.antigravity/**`
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`
- `services/Diten.AuthService/**` unless explicitly justified by an approved implementation plan.
- `services/Diten.DevEnablementService/**`
- `services/Diten.EnterpriseStrategyService/**`
- `gateway/Diten.ApiGateway/**/ocelot.json` unless explicitly handled by integration-agent after approval.

## Out Of Scope
- Full quota dashboard.
- Billing invoice calculation.
- Payment provider integration.
- Database size metering.
- Log storage metering.
- Cache storage metering.
- Analytics storage metering.
- Gateway API rate limiting implementation; this pack may define dependency/seam only.
- Gateway request counting and throttling implementation for `api.calls.per.month`.
- Tenant self-service quota purchase flow.
- New quota definition catalog that competes with `SubscriptionPlan.DefaultQuotas`.
- Subscription lifecycle ownership or tenant access lifecycle decisions.
- Notification Service delivery, retry engine, template ownership, or email provider integration.
- Public UI calls to internal quota mutation endpoints.

## Build Expectations
- `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug`
- `dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug` only if UI files are changed.
- `dotnet build gateway/Diten.ApiGateway/Diten.ApiGateway.csproj -c Debug` only if approved gateway scope is changed.

---

# test-notes.md

## Unit Tests
- Inactive subscription consume is blocked with `QUOTA_SUBSCRIPTION_INACTIVE`.
- Trial subscription quota is resolved from trial plan defaults.
- Quota initialization is idempotent.
- Plan upgrade syncs `LimitValue`.
- Plan downgrade preserves `CurrentValue` and blocks over-limit new consume.
- Successful consume increments usage from `14/15` to `15/15`.
- Limit exceeded at `15/15` returns `QUOTA_LIMIT_EXCEEDED`.
- Limit exceeded does not mutate `CurrentValue`.
- Release decreases usage and rejects invalid release amount.
- Unknown quota key returns `QUOTA_KEY_UNKNOWN`.
- Missing tenant returns `QUOTA_TENANT_REQUIRED`.
- Missing plan quota returns `QUOTA_CONFIGURATION_MISSING`.
- Tenant override without reason is rejected.
- Atomic concurrency prevents over-consume.
- Period reset creates a new period and resets `CurrentValue` to `0`.
- `modules.max` counts only active/enabled assignment records.
- Recalculate corrects `users.max` from active user source.
- Recalculate corrects `storage.gb.max` from Document/File metadata.
- Recalculate corrects `modules.max` from active/enabled module assignments.
- `api.calls.per.month` recalculation returns `QUOTA_RECALCULATION_NOT_SUPPORTED` without gateway counter integration.
- Warning notification seam is emitted on first 80% threshold crossing.
- Warning notification is deduped within the same period.
- Breach notification seam is emitted on first hard-limit crossing.
- Notification failure does not roll back consume.
- Release decreases current value.
- Release cannot reduce current value below zero.
- Duplicate release with the same operation id is idempotent.
- Cross-tenant release is blocked.
- Internal endpoint missing `Source` is rejected.
- Internal endpoint missing `TenantId` is rejected.

## Integration Tests
- Tenant subscription activation seeds quota usage.
- Plan change syncs quota limits.
- User create flow enforces `users.max`.
- Module assignment enable enforces `modules.max`.
- Document/File upload flow enforces `storage.gb.max`.
- Document/File upload storage recalculation matches trusted metadata.
- File delete and download remain allowed when `storage.gb.max` is full.
- Internal consume API applies quota and returns controlled response.
- Internal release API releases quota and returns controlled response.
- Reset-period API resets monthly quota.
- Audit event is emitted for consume, release, reset, and reject.
- Tenant isolation prevents cross-tenant quota reads or mutations.
- Inactive subscription prevents mutating consume operation.
- Internal consume requires `PlatformInternalOnly` or `RequireInternalServiceToken` policy.
- Public user token cannot call internal consume/release/reset.
- User-facing quota status requires JWT + RBAC.
- File delete releases storage quota.
- Module assignment disable releases `modules.max`.
- Notification seam is emitted once per period threshold.

## Failure Tests
- Missing active subscription or missing plan quota fails closed.
- Override without reason fails validation.
- Concurrent over-consume cannot exceed `LimitValue`.
- Invalid release amount returns `QUOTA_RELEASE_INVALID_AMOUNT`.
- Reset on non-resettable quota returns `QUOTA_PERIOD_RESET_NOT_ALLOWED`.
- Unknown quota key returns controlled validation error.

## Smoke / Manual Checks
- API response uses `Response<T>` envelope.
- Controlled error includes stable error code and user-safe message.
- UI flow shows understandable quota exceeded message when backend rejects an operation.
- No direct frontend calls to service ports `5056`, `5057`, or `5058`.

---

# notes.md

## Implementation Notes
- Quota enforcement must happen on the backend; UI checks are advisory only.
- Use atomic MongoDB update for consume.
- Do not use read-then-update consume logic.
- Store `QuotaEvent` for successful and rejected quota operations.
- Emit General Audit Trail event for consume, release, reset, reject, and manual override.
- Keep quota keys aligned with `SubscriptionPlan.DefaultQuotas`.
- Missing plan default quota is a configuration error, not an invitation to silently invent a default.
- Gateway API call quota enforcement should be integrated through a later integration-agent-owned step if gateway config changes are required.
- Quota initialization should be triggered by subscription activation/provisioning and remain idempotent.
- Recalculate operations must be tenant-scoped and non-destructive.
- Warning/breach notification seams must be deduped per quota period.
- Notification failure should be observable but must not undo an accepted consume.
- Release must be safe, tenant-scoped, and idempotent by operation reference where possible.
- Internal quota mutation endpoints must use service authorization and must not accept public UI calls.
- Do not log or audit sensitive internal token/API key values.

## Open Questions / Risks
- `execution/domains/platform-shared-services/domain-config.md` does not currently list `MOD-0033` in PSS in-scope modules, while `docs/platform/master-plan.md` places Consumer / Quota Model in the platform plan. This is recorded as a domain config gap; do not edit domain-config as part of this pack revision.
- Gateway-level `api.calls.per.month` enforcement crosses protected gateway ownership and needs integration-agent coordination.
- Future quota summary UI must stay bounded to `MOD-0046+ Tenant Core UI Extensions`.
- Atomic consume behavior is the highest-risk implementation detail and requires concurrency tests.

## Final Decisions
- MVP storage scope is Document/File upload storage only.
- Quota enforcement is backend-side and does not rely on UI-only validation.
- Limit exceeded never increments usage.
- Missing plan default quota produces controlled configuration error.
- Tenant override requires audit reason.
- Internal consume/release/reset endpoints are not public endpoints.
- `SubscriptionPlan.DefaultQuotas` remains the plan-level quota default source.
- No second quota definition catalog is introduced by this module.
- Quota module is not the owner of subscription lifecycle.
- Inactive subscription state makes mutating consume operations fail closed.
- `QuotaUsage` initialization is idempotent during subscription activation/provisioning.
- Plan changes sync `LimitValue` and preserve `CurrentValue`.
- `modules.max` limits only active/enabled tenant module assignment count.
- `api.calls.per.month` remains contract/seam only in this pack, not gateway enforcement.
- Recalculate is non-destructive and tenant-scoped.
- Warning/breach notifications are deduplicated per `TenantId + QuotaKey + Period`.
- Notification failure does not roll back successful quota consume.
- Release operations must be safe, tenant-scoped, and preferably idempotent by `OperationId`/`SourceReference`.
- Internal consume/release/reset endpoints use service authorization, not normal user RBAC.
- Public UI must not call internal quota mutation endpoints directly.
- Sensitive internal tokens must never be written to audit/log payloads.
