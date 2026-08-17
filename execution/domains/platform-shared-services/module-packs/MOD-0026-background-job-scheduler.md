---
id: MOD-0026
name: Background Job Scheduler
title: Background Job Scheduler
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: BaseEntity
status: done
owner: Platform
branch: feature/pss/mod-0026-background-job-scheduler
started: 2026-05-15
target: 2026-05-30
completed: 2026-05-15
form_field_count: 0
---

# MOD-0026 - Background Job Scheduler

## 1. Module Summary
- **Purpose:** Provide the central background job scheduler foundation for ERP-vNext. Platform and future ERP services should register fire-and-forget, delayed, and recurring jobs through shared abstractions instead of binding directly to Hangfire APIs.
- **Target technology:** Hangfire with MongoDB-backed storage.
- **Central abstraction rule:** ERP services depend on `IBackgroundJobScheduler` / registrar contracts, not on Hangfire directly.
- **Event Bus rule:** Jobs that need to publish or react to internal events use the existing MOD-0035 Event Bus abstraction. This module must not introduce a second event bus abstraction.
- **UI:** No custom MVC UI in MVP. Hangfire Dashboard is allowed only when protected by PlatformActor authorization.
- **Golden Reference decision:** `golden_reference: none` because this is an infrastructure module, not a CRUD/DataTable module.
- **Completion status:** PASS / 90% as scheduler foundation. Remaining work belongs to the owning business modules that implement the real job logic behind the registered descriptors.
- **Master-plan reconciliation:** `docs/platform/master-plan.md` should show MOD-0026 as PASS / 90%. Earlier master-plan values of missing/0% or partial/70% are superseded by the runtime smoke evidence below.

## Completion Evidence
- **Completion date:** 2026-05-15.
- **Batch status:** PASS.
- **Recommended master-plan status:** ✅ PASS / 90%.
- Live Platform API runtime smoke passed.
- Hangfire Dashboard is protected by PlatformActor authorization.
- Anonymous and non-PlatformActor dashboard access is rejected.
- PlatformActor dashboard access is accepted.
- Mongo-backed Hangfire enqueue worked.
- `SchedulerSmokeTestJob` success and controlled failure paths produced `JobExecutionLog` evidence.
- Failure redaction and retry metadata were verified.
- 8 standard recurring job descriptors were registered/declared and intentionally disabled by configuration until owning modules implement business logic.
- BuildingBlocks background job abstractions were created without duplicate package structure.
- No custom MVC UI or public arbitrary trigger endpoint was added.
- Event Bus boundary was preserved.
- Remaining work belongs to owning business modules, not the scheduler foundation.

## 2. Ownership and Boundaries
### In-scope
- Central background job scheduling contracts.
- Hangfire adapter and DI wiring in Platform service.
- Fire-and-forget, delayed/scheduled, and recurring job registration APIs.
- Service-level recurring job registrar contract so each service can own its registrations.
- Platform startup discovery/registration of known registrars.
- MongoDB-backed job execution log records grouped by service.
- Job failure metadata, retry metadata, correlation metadata, and no-payload logging rules.
- Hangfire Dashboard authorization using PlatformActor policy or an equivalent secure authorization filter.
- Standard initial job definitions as registration descriptors. Full business logic for the 8 standard jobs is out-of-scope.
- At least one real executable scheduler smoke job, including success and controlled failure paths.
- Integration with existing MOD-0035 `IEventBus` for event-driven job outcomes/triggers.

### Out-of-scope
- Implementing the business logic of dependent modules such as MOD-0027 EmailDispatch, MOD-0034 WebhookRetry, MOD-0033 QuotaReset, or MOD-0009 ProvisioningRetry.
- Creating a custom job dashboard UI in `Diten.Web`.
- Adding a second event bus abstraction.
- Replacing MOD-0035 outbox/inbox or RabbitMQ behavior.
- Adding public job execution endpoints for arbitrary job triggering.
- Gateway route changes unless Hangfire dashboard/API exposure requires an explicit integration-agent task.
- Secrets provider implementation beyond configuration/env variable consumption until NEW-001 is complete.

### Ownership rule
- MOD-0026 owns scheduling mechanics, registration standards, Hangfire hosting, execution logs, retry policy defaults, and dashboard security.
- Business modules own their actual job implementations and event payloads.
- Event Bus owns event publish/consume contracts and transport. Scheduler only consumes those contracts.

## 3. Owned Objects
### Shared abstractions
- `IBackgroundJobScheduler`
- `IRecurringJobRegistrar`
- `IBackgroundJobHandler<TArgs>` as the single executable job handler contract.
- `IBackgroundJob` only if a marker interface is needed; do not create a second execution model.
- `IJobExecutionLogWriter`
- `BackgroundJobContext`
- `BackgroundJobDescriptor`
- `JobRegistrationOptions`
- `RecurringJobRegistration`
- `BackgroundJobSchedulerOptions`

### Hangfire implementation
- `HangfireBackgroundJobScheduler`
- `HangfireRecurringJobRegistrationHostedService` or equivalent startup registrar
- `HangfireDashboardAuthorizationFilter`
- `HangfireJobActivator` / DI integration if needed
- MongoDB storage configuration for Hangfire

### Persistence records
- `JobExecutionLog`
- `JobExecutionStatus`
- `JobFailureMetadata`
- `JobRetryMetadata`

### Platform jobs / registration catalog
- `PlatformRecurringJobRegistrar`
- `SchedulerSmokeTestJob` for executable success/failure proof when business-dependent jobs are not ready.
- `TrialExpiryScanJob` registration: daily 02:00 UTC, owned by MOD-0297.
- `SubscriptionRenewalJob` registration: daily 03:00 UTC, owned by MOD-0297/MOD-0299.
- `QuotaResetJob` registration: monthly, owned by MOD-0033.
- `EntitlementCacheRefreshJob` registration: hourly, owned by MOD-0018.
- `WebhookRetryJob` registration: every 5 minutes, owned by MOD-0034.
- `AuditLogArchiveJob` registration: weekly, owned by MOD-0021.
- `EmailDispatchJob` registration: every 1 minute, owned by MOD-0027.
- `ProvisioningRetryJob` registration: every 2 minutes, owned by MOD-0009.

### Event Bus integration points
- Jobs may inject existing `IEventBus` from MOD-0035.
- Jobs publish reduced integration/internal events only, never full entities.
- Job-created events or job-failed events, if introduced, must use MOD-0035 naming/versioning and outbox rules.

## 4. Entity Fields
### JobExecutionLog
| Field | Type | Required | Rule |
|---|---|---|---|
| Base | `BaseEntity` | Yes | Platform persistence record. Includes Id, IsDeleted, DeletedAt, CreatedAt, UpdatedAt, and concurrency field from base contract. |
| TenantId | `Guid?` | No | Nullable for platform-wide jobs. Required only when a tenant-scoped job executes for a tenant. Not accepted from client payload. |
| ServiceName | `string` | Yes | Stable service identifier, e.g. `Diten.Platform`, `Diten.AuthService`; max 128; indexed. |
| JobName | `string` | Yes | Stable job type/name, e.g. `TrialExpiryScanJob`; max 200; indexed. |
| JobId | `string?` | No | Hangfire job id when available; max 128; indexed. |
| RecurringJobId | `string?` | No | Stable recurring registration id when applicable; max 200; indexed. |
| CorrelationId | `Guid` | Yes | Generated or propagated from request/event/job context; indexed. |
| CausationId | `Guid?` | No | Previous event/job/message id when available. |
| Status | `JobExecutionStatus` | Yes | `Started`, `Succeeded`, `Failed`, `Retrying`, `Cancelled`, `DeadLettered`, `Skipped`. |
| StartedAt | `DateTimeOffset` | Yes | UTC only; indexed. |
| FinishedAt | `DateTimeOffset?` | No | UTC only; set when job completes or fails. |
| DurationMs | `long?` | No | Non-negative duration; computed on completion. |
| Error | `string?` | No | Redacted; max 4000 chars; no payloads, secrets, tokens, or connection strings. |
| RetryCount | `int` | Yes | Starts at 0; non-negative. |
| TriggerType | `string` | Yes | `Recurring`, `Scheduled`, `FireAndForget`, `Manual`, `EventDriven`; max 64. |
| TriggeredBy | `string?` | No | Service/user/system/event origin; max 200; no secrets. |
| EventName | `string?` | No | MOD-0035 event name if job was caused by or published an event. |
| EventId | `Guid?` | No | MOD-0035 event id when available. |
| Metadata | `Dictionary<string,string>?` | No | Small redacted metadata only; no serialized payload/entity. |

### JobScheduleDefinition
| Field | Type | Required | Rule |
|---|---|---|---|
| RecurringJobId | `string` | Yes | Globally stable id: `{ServiceName}.{JobName}` or `{ServiceName}.{Module}.{JobName}`. |
| ServiceName | `string` | Yes | Owning service. |
| JobName | `string` | Yes | Job class/logical name. |
| Cron | `string` | Yes | Cron expression; configuration override allowed. |
| TimeZone | `string` | Yes | Default `UTC`; local server timezone forbidden. |
| IsEnabled | `bool` | Yes | Configuration-driven; disabled jobs do not register execution. |
| Queue | `string?` | No | Hangfire queue name when needed; default queue allowed. |
| MaxRetryAttempts | `int` | Yes | Default policy; must be >= 0. |

## 5. Repo Scope
- `execution/domains/platform-shared-services/module-packs/MOD-0026-background-job-scheduler.md`
- Preferred shared abstraction path: existing repo BuildingBlocks convention + `BackgroundJobs/**`.
- `services/Diten.Platform.Common/**` for central scheduler abstractions and cross-service contracts.
- `services/Diten.Platform/src/Diten.Platform.Domain/**` for Platform-owned scheduler persistence entities/enums when needed.
- `services/Diten.Platform/src/Diten.Platform.Application/**` for scheduler application contracts, job orchestration, log writer abstractions, and Platform registrar.
- `services/Diten.Platform/src/Diten.Platform.Persistence/**` or current equivalent infrastructure/persistence path for MongoDB job execution log repository and indexes.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/**` for Hangfire adapter, dashboard authorization filter, Mongo storage wiring, and registration hosted service.
- `services/Diten.Platform/src/Diten.Platform.API/**` for DI registration, dashboard pipeline wiring, health checks, and startup registration only.
- `services/Diten.AuthService/**` is optional and only allowed if the implementation needs a concrete example registrar or consumer integration. If touched, the reason must be documented in the PR.
- `frontend/Diten.Web/**` is out of MVP scope unless a separate Platform UI is approved. Hangfire Dashboard is the MVP ops surface.
- `gateway/Diten.ApiGateway/**` only if dashboard/API exposure requires routing. Any `ocelot.json` change is integration-agent owned.

## 6. Protected Paths
- `.antigravity/**`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- `gateway/Diten.ApiGateway/**/ocelot.json` unless a separate integration-agent task is approved.
- `services/Diten.DevEnablementService/**`
- `services/Diten.EnterpriseStrategyService/**`
- `services/Diten.MdmService/**` unless a separate domain module pack is approved.
- Runtime code outside the repo scope above.
- MOD-0035 Event Bus implementation files except for approved integration calls through existing public abstractions.

## 7. Dependencies
- **NEW-001 Secrets Management:** Hangfire MongoDB storage connection/config and dashboard options must not be hardcoded. Until NEW-001 is complete, appsettings + environment variables are the temporary path.
- **MOD-0035 Event Bus / Internal Events:** Existing event bus abstraction is used for event-driven job publish/consume paths. No duplicate abstraction is allowed.
- **MOD-0297 Tenant Subscription Lifecycle:** Owns TrialExpiryScan and part of SubscriptionRenewal job behavior.
- **MOD-0299 SaaS Billing & Invoicing:** Owns billing-related renewal behavior when implemented.
- **MOD-0033 Consumer / Quota Model:** Owns QuotaReset job behavior.
- **MOD-0018 RBAC / Entitlement Enforcement:** Owns EntitlementCacheRefresh job behavior.
- **MOD-0034 Webhook Delivery:** Owns WebhookRetry job behavior.
- **MOD-0021 General Audit Trail:** Owns AuditLogArchive job behavior and may later receive job failure audit events.
- **MOD-0027 Notification / Email Service:** Owns EmailDispatch job behavior.
- **MOD-0009 Tenant Registry Lifecycle Events:** Owns ProvisioningRetry job behavior.
- **MOD-0042 Alerting / Incident Runbooks:** Owns alerting on repeated job failure when implemented.

## 8. Runtime Constraints
- Hangfire is the chosen scheduler for this module.
- MongoDB storage is required for Hangfire state and job execution logs.
- Scheduler contracts must live in a shared, non-platform-specific abstraction package so future ERP services can reference the same abstraction.
- Preferred shared abstraction target is `Diten.BuildingBlocks.BackgroundJobs`.
- Implementation must inspect existing BuildingBlocks folder naming and use the repo's actual convention; do not create a parallel duplicate Building.Blocks/BuildingBlocks structure.
- If the current repo structure cannot support BuildingBlocks immediately, `Diten.Platform.Common.BackgroundJobs` may be used as a temporary compatibility path, but type names must remain generic and portable.
- Do not create both BuildingBlocks and Platform.Common background job shared contracts at the same time.
- Shared contracts must not contain platform business names such as TrialExpiry, SubscriptionRenewal, Tenant, Entitlement, or Provisioning.
- Service implementations must not depend directly on Hangfire unless they are inside the Hangfire adapter/infrastructure layer.
- Each service owns its own `IRecurringJobRegistrar` implementation and registers only its own recurring jobs.
- Platform startup discovers and executes registered `IRecurringJobRegistrar` instances.
- All schedules use UTC. Local machine timezone is forbidden for recurring jobs.
- Cron values, enable/disable flags, retry counts, dashboard path, dashboard enabled flag, and storage settings are configuration-driven.
- Hangfire MongoDB storage package/version/runtime compatibility is a hard gate. If it fails, implementation must not silently switch to SQL Server, in-memory storage, or Quartz.
- If Hangfire MongoDB storage is blocked, the implementation output must report: package name/version, error, affected files, recommended alternatives, and the user decision required.
- Job execution logs are service-scoped and must include `ServiceName`.
- Payload logging is forbidden. Logs contain metadata only.
- Job failure must create a `JobExecutionLog` failure record even when alert/audit modules are not available yet.
- Multi-instance deployment must rely on Hangfire distributed locking/concurrency controls and explicit job-level guards where needed.
- Event publish from jobs must use MOD-0035 `IEventBus`; direct RabbitMQ or MassTransit calls from jobs are forbidden.
- If a job is triggered by an event, `CorrelationId`, `CausationId`, `EventId`, and `EventName` must be propagated to the job execution log when available.
- `entity_base: BaseEntity` is used because scheduler persistence is in Diten.Platform. TenantId is nullable because jobs may be platform-wide or tenant-scoped.
- Custom MVC UI is not part of MVP. Operational-looking buttons, pages, or endpoints must not be added without backing behavior.
- Creating files/classes is not completion. PASS requires runtime proof for golden flow and failure path.

## Implementation Pre-Audit
- **Audit date:** 2026-05-15.
- **Scheduler/Hangfire state:** No existing Hangfire scheduler/runtime implementation was found in repo code. Master-plan MOD-0026 should be treated as 0% for implementation planning unless a later targeted audit finds hidden runtime code.
- **BuildingBlocks convention:** Existing repo convention is filesystem folder `services/Diten.Building.Blocks` with project/package names under `Diten.BuildingBlocks.*`, e.g. `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.Eventing/Diten.BuildingBlocks.Eventing.csproj`.
- **Shared abstraction decision:** Use BuildingBlocks first. Preferred implementation path is `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.BackgroundJobs/`. Do not create a parallel `services/Diten.BuildingBlocks` or duplicate BuildingBlocks structure.
- **Fallback decision:** Use `Diten.Platform.Common.BackgroundJobs` only if implementation discovers a hard blocker in creating/referencing the BuildingBlocks project.
- **Hangfire MongoDB storage compatibility:** Temporary net8.0 restore/build audit succeeded with `Hangfire.AspNetCore` 1.8.23 and `Hangfire.Mongo` 1.15.0. `Hangfire.Mongo` restored `MongoDB.Driver` 3.7.1. Repo Platform Infrastructure currently references `MongoDB.Driver` 2.27.0, so implementation must verify transitive driver impact before committing package changes.
- **SDK note:** Audit build used the installed .NET SDK reported as 10.0.100-rc.2 while targeting net8.0; implementation validation must still run the repo build commands.
- **Audit conclusion:** Implementation may proceed with BuildingBlocks-first design, but package changes must be validated early before writing the full scheduler surface.

## Implementation Order
1. Confirm the repo audit above at implementation start and report any drift.
2. Create shared abstractions in the repo's actual BuildingBlocks convention if compatible; otherwise document Platform.Common fallback reason.
3. Validate Hangfire MongoDB storage package restore/build compatibility in the target Platform projects before broad implementation.
4. Implement `SchedulerSmokeTestJob` success and controlled failure golden flow first.
5. Prove `JobExecutionLog` Started/Succeeded and Failed records before adding standard descriptors.
6. Add the 8 standard recurring job registration descriptors after smoke proof is working.
7. Return PASS only with runtime Golden Flow and Failure Path proof.

## Golden Flow
Platform API starts with scheduler enabled -> Hangfire storage/config loads from configuration/environment -> recurring registrar runs -> 8 standard recurring job definitions are registered or intentionally disabled by config with documented owner -> authorized PlatformActor opens Hangfire Dashboard -> a safe scheduler smoke job is triggered -> JobExecutionLog records Started and Succeeded with ServiceName, JobName, CorrelationId, StartedAt, FinishedAt, DurationMs, TriggerType -> execution can be queried by repository/service.

## Failure Path
A controlled failing smoke job is triggered -> the platform does not crash -> Hangfire retry metadata is visible -> JobExecutionLog records Failed with redacted Error, RetryCount, CorrelationId, StartedAt, FinishedAt, DurationMs -> no payload/secret/token/connection string is logged -> anonymous or non-PlatformActor dashboard access is rejected.

## 9. Layout & Shell Contract
- `shell: none`.
- No Razor layout is required for MVP.
- No `Diten.Web` view is created by this pack.
- Hangfire Dashboard is not a Razor module and must be protected by PlatformActor authorization.
- If a custom Platform Admin UI is later approved, it must use `Layout = "_LayoutPlatformAdmin"` explicitly and be introduced through a separate module pack or pack update.

## 10. Backend File Convention
This is a backend/infrastructure module, not a CRUD DataTable module. `golden_reference: none` is intentional.

### Shared abstraction location
- Preferred path: use the repo's existing BuildingBlocks folder/project naming convention and add `BackgroundJobs/**` there.
- Implementation must inspect existing BuildingBlocks folder naming and use the repo's actual convention; do not create a parallel duplicate Building.Blocks/BuildingBlocks structure.
- Temporary fallback path: `services/Diten.Platform.Common/src/Diten.Platform.Common/BackgroundJobs/**` only if BuildingBlocks is not available in the repo structure.
- Do not create both the preferred BuildingBlocks package and the Platform.Common fallback package.
- Contracts:
  - `IBackgroundJobScheduler`
  - `IRecurringJobRegistrar`
  - `IBackgroundJobHandler<TArgs>` as the executable job handler contract
  - `IBackgroundJob` only as an optional marker interface if there is a real need
  - `BackgroundJobContext`
  - `BackgroundJobDescriptor`
  - `JobRegistrationOptions`
  - `RecurringJobRegistration`

Shared contracts must be platform-neutral. Platform business names such as `TrialExpiry`, `SubscriptionRenewal`, `Tenant`, `Entitlement`, or `Provisioning` belong in Platform-owned job implementations/registrars, not in the shared abstraction package.
Implementation must not create two execution models. `IBackgroundJobHandler<TArgs>` is the executable contract; `IBackgroundJob` is marker-only if used.

### Platform implementation shape
- `services/Diten.Platform/src/Diten.Platform.Domain/Entities/JobExecutionLog.cs`
- `services/Diten.Platform/src/Diten.Platform.Domain/Enums/JobExecutionStatus.cs`
- `services/Diten.Platform/src/Diten.Platform.Domain/Repositories/IJobExecutionLogRepository.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/BackgroundJobs/**`
- `services/Diten.Platform/src/Diten.Platform.Application/Services/JobExecutionLogWriter.cs` or equivalent.
- `services/Diten.Platform/src/Diten.Platform.Persistence/Repositories/JobExecutionLogRepository.cs` or current equivalent persistence path.
- `services/Diten.Platform/src/Diten.Platform.Persistence/Configurations/JobExecutionLogConfiguration.cs` or current equivalent index setup path.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/BackgroundJobs/Hangfire/**`
- `services/Diten.Platform/src/Diten.Platform.API/**` only for DI, middleware/dashboard mapping, and health checks.

### Naming expectations
- Scheduler facade: `IBackgroundJobScheduler`
- Hangfire adapter: `HangfireBackgroundJobScheduler`
- Registrar contract: `IRecurringJobRegistrar`
- Platform registrar: `PlatformRecurringJobRegistrar`
- Dashboard auth: `PlatformActorHangfireAuthorizationFilter`
- Log writer: `IJobExecutionLogWriter`
- Jobs: `{BusinessName}Job`, e.g. `TrialExpiryScanJob`, `EmailDispatchJob`
- Smoke proof job: `SchedulerSmokeTestJob`
- Job methods must accept `CancellationToken` where Hangfire integration allows it, and internal async I/O must propagate cancellation.

### CQRS note
- No CRUD command/query surface is required in MVP.
- If admin-only scheduler APIs are added later, they must use the Golden Reference action-based separation and Response envelope.

## 11. Frontend File Contract
- No frontend files are in scope for MVP.
- No DataTable v2 contract applies.
- No `_CreateEditOffcanvas.cshtml`, `_DetailsQuickView.cshtml`, `Create.cshtml`, `Edit.cshtml`, or `Details.cshtml` files are created.
- Hangfire Dashboard is the only MVP operational surface.
- Any custom scheduler/job log UI requires a separate Platform Admin module pack and en/tr localization.

## 12. Validation Rules
| Field | Required | Format/Rule | DB-level | Pre-check |
|---|---|---|---|---|
| ServiceName | Yes | Trim, max 128, known service identifier | Index with `JobName`, `StartedAt` | Reject empty service names. |
| JobName | Yes | Trim, max 200, stable job class/logical name | Index with `ServiceName`, `StartedAt` | Reject empty job names. |
| RecurringJobId | Recurring only | `{ServiceName}.{ModuleOrArea}.{JobName}`; max 200 | Unique where persisted as definition | Reject duplicate registration ids. |
| Cron | Recurring only | Valid cron expression, UTC semantics | — | Validate before Hangfire registration. |
| TimeZone | Yes | Must be `UTC` unless explicitly justified | — | Reject local/unspecified timezone. |
| CorrelationId | Yes | Guid not empty | Indexed | Generate if ambient context is missing. |
| TenantId | No | Guid when present | Indexed optional | Required only for tenant-scoped execution. |
| Status | Yes | Known enum | Indexed | Only valid transitions: Started -> Succeeded/Failed/Retrying/Cancelled/Skipped/DeadLettered. |
| StartedAt | Yes | UTC timestamp | Indexed | Set before execution begins. |
| FinishedAt | Completion only | UTC timestamp >= StartedAt | Indexed optional | Set on final state. |
| DurationMs | Completion only | `>= 0` | — | Compute from timestamps; do not accept externally. |
| Error | Failure only | Max 4000 chars after redaction | — | Must redact secrets, tokens, payloads, connection strings. |
| RetryCount | Yes | `>= 0` | — | Increment through retry pipeline only. |
| EventName | No | MOD-0035 event naming when present | Indexed optional | Validate through existing event naming rules if available. |
| Metadata | No | Small redacted key/value fields only | — | Reject or omit serialized payload/entity fields. |

## 13. Failure Path to Verify
- **Repo audit before implementation**
  - Expected: report states whether scheduler code exists. If none exists, MOD-0026 is treated as 0%. If existing scheduler code exists, changed/reused/replaced parts are reconciled against this pack.
- **Hangfire storage config missing**
  - Expected: scheduler is disabled or application fails with a clear startup error according to the chosen config mode; no hardcoded fallback secret is used.
- **Hangfire MongoDB storage incompatibility**
  - Expected: work stops as `BLOCKED`; implementation does not switch to SQL Server, in-memory storage, or Quartz without user approval. Report includes package name/version, error, affected files, recommended alternatives, and required decision.
- **Duplicate recurring job id**
  - Expected: registration is idempotent for the same definition or rejected with a clear log; duplicate definitions do not create multiple schedules.
- **Invalid cron**
  - Expected: registrar logs a validation failure and the invalid job is not registered.
- **Unauthorized dashboard access**
  - Expected: non-PlatformActor request receives 401/403; dashboard is never anonymous.
- **Job execution failure**
  - Expected: `JobExecutionLog` row contains `Status=Failed`, redacted `Error`, `RetryCount`, `StartedAt`, `FinishedAt`, and `DurationMs`.
- **Repeated job failure**
  - Expected: Hangfire retry policy applies; when MOD-0042 is unavailable, failure remains visible in job log without requiring alert integration.
- **Event Bus unavailable or publish failure inside job**
  - Expected: job uses existing MOD-0035 failure/outbox behavior; direct broker calls are not made; execution log captures metadata-only failure state.
- **Multi-instance double execution risk**
  - Expected: recurring jobs use Hangfire distributed locks/concurrency guards so only one instance performs the critical section.
- **Missing correlation context**
  - Expected: scheduler generates a correlation id and writes it to log and any events published by the job.
- **Payload logging attempt**
  - Expected: tests or review reject logging serialized entities, payloads, tokens, secrets, connection strings, or credentials.
- **Fake completion attempt**
  - Expected: empty/no-op classes without executable success and failure smoke proof cannot be marked PASS.

## 14. Authorization Convention
- Hangfire Dashboard:
  - Policy: `PlatformActor` or equivalent custom Hangfire authorization filter that checks the same platform actor requirement.
  - Anonymous access is forbidden.
  - Non-PlatformActor access returns 401/403.
  - Hangfire's own authorization filter model must be used.
  - MVC `[Authorize]` attributes alone are insufficient for dashboard protection.
  - Dashboard path and enabled flag are configuration-driven.
- Internal scheduler APIs:
  - No public arbitrary job trigger endpoint in MVP.
  - `SchedulerSmokeTestJob` may be triggered only by integration-test enqueue, Hangfire Dashboard manual trigger by PlatformActor, or an internal test-only harness.
  - Free-form public API job trigger endpoints are forbidden.
  - If admin job management APIs are later added, they must use `[Authorize(Policy = "PlatformActor")]`.
  - Permission format for future Platform APIs:
    - `Platform.BackgroundJobs.Read`
    - `Platform.BackgroundJobs.Trigger`
    - `Platform.BackgroundJobs.Retry`
    - `Platform.BackgroundJobs.Cancel`
- Jobs:
  - Jobs run as system/background actors, not as browser users.
  - UserId must not be accepted from job payload. If needed, use a system actor identity and audit metadata.

## 15. Gateway / API Routing Decision
- Decision: Gateway change is **not required** for MVP unless the Hangfire Dashboard must be exposed through Gateway.
- Frontend still never calls service ports directly.
- Hangfire Dashboard can be mapped inside Platform API for local/admin access if protected by PlatformActor.
- If Gateway exposure is required:
  - `gateway/Diten.ApiGateway/**/ocelot.json` remains protected.
  - Route work must be handled by `integration-agent`.
  - `OPTIONS` must be included where browser preflight applies.
- No DataTable or MVC proxy route is created by this pack.

## 16. Acceptance Criteria
- [ ] `IBackgroundJobScheduler` central abstraction is defined and injectable from services.
- [ ] ERP service code depends on the scheduler abstraction/registrar contracts, not directly on Hangfire APIs.
- [ ] Preferred shared abstraction target is `Diten.BuildingBlocks.BackgroundJobs`.
- [ ] Implementation inspects repo BuildingBlocks naming and uses the actual existing convention.
- [ ] No parallel duplicate `Building.Blocks` / `BuildingBlocks` shared package structure is created.
- [ ] If BuildingBlocks is unavailable, temporary fallback to `Diten.Platform.Common.BackgroundJobs` is explicitly documented.
- [ ] Only one shared abstraction location is used: preferred BuildingBlocks path or fallback Platform.Common path, not both.
- [ ] Shared abstraction names are platform-neutral: `IBackgroundJobScheduler`, `IRecurringJobRegistrar`, `IBackgroundJobHandler<TArgs>`, `BackgroundJobContext`, `BackgroundJobDescriptor`.
- [ ] `IBackgroundJobHandler<TArgs>` is the executable job handler contract.
- [ ] `IBackgroundJob` is created only as a marker if needed; it does not introduce a second execution model.
- [ ] Shared contracts do not contain platform business names such as TrialExpiry, SubscriptionRenewal, Tenant, Entitlement, or Provisioning.
- [ ] Hangfire is configured with MongoDB storage through configuration/environment values.
- [ ] Hangfire MongoDB storage package/version/runtime result is reported.
- [ ] Hangfire package compatibility is validated early in target projects before full scheduler implementation.
- [ ] Transitive MongoDB.Driver impact from `Hangfire.Mongo` is checked against the Platform Infrastructure MongoDB.Driver version.
- [ ] If Hangfire MongoDB storage is incompatible, implementation reports `BLOCKED` and does not switch storage provider or scheduler without user approval.
- [ ] Hangfire Dashboard is protected by `[Authorize(Policy = "PlatformActor")]` or an equivalent secure Hangfire authorization filter.
- [ ] Hangfire Dashboard uses Hangfire's own authorization filter model; MVC `[Authorize]` alone is not accepted.
- [ ] Hangfire Dashboard anonymous access is blocked and covered by smoke/integration verification.
- [ ] PlatformActor-negative dashboard access returns 401/403 and is covered by smoke/integration or manual proof.
- [ ] Each service can provide its own `IRecurringJobRegistrar` or equivalent registration class.
- [ ] Platform service startup discovers and runs registered recurring job registrars.
- [ ] Registration supports fire-and-forget, delayed/scheduled, and recurring jobs.
- [ ] Recurring job definitions include stable `RecurringJobId`, `ServiceName`, `JobName`, `Cron`, `TimeZone=UTC`, enabled flag, and retry options.
- [ ] `JobExecutionLog` persistence includes `ServiceName`, `JobName`, `CorrelationId`, `Status`, `StartedAt`, `FinishedAt`, `DurationMs`, `Error`, and `RetryCount`.
- [ ] Job execution logs are queryable/filterable by service name at repository level.
- [ ] Job failure always creates a metadata-only failure log record.
- [ ] `Error` is redacted and truncated; no payload, secret, token, credential, connection string, or full entity is logged.
- [ ] Jobs that publish events use existing MOD-0035 `IEventBus`; no second event bus abstraction is introduced.
- [ ] Direct RabbitMQ/MassTransit publish from job classes is forbidden; jobs use `IEventBus`.
- [ ] The 8 standard jobs are registration descriptors only unless their owning module is ready; full business logic is out-of-scope for this pack.
- [ ] Empty/no-op job classes cannot satisfy PASS.
- [ ] At least one real executable smoke job is implemented and proves success and failure execution paths.
- [ ] If `ProvisioningRetryJob` or `TrialExpiryScanJob` business dependencies are not ready, `SchedulerSmokeTestJob` is used for executable proof.
- [ ] `SchedulerSmokeTestJob` is triggered through integration test enqueue, Hangfire Dashboard manual trigger by PlatformActor, or internal test-only harness.
- [ ] Public API endpoint for arbitrary job trigger is not created.
- [ ] PASS requires job execution + JobExecutionLog success proof + controlled failure proof.
- [ ] Standard initial job descriptors are registered or intentionally disabled by configuration with documented owners:
  - `TrialExpiryScanJob` - daily 02:00 UTC - MOD-0297.
  - `SubscriptionRenewalJob` - daily 03:00 UTC - MOD-0297/MOD-0299.
  - `QuotaResetJob` - monthly - MOD-0033.
  - `EntitlementCacheRefreshJob` - hourly - MOD-0018.
  - `WebhookRetryJob` - every 5 minutes - MOD-0034.
  - `AuditLogArchiveJob` - weekly - MOD-0021.
  - `EmailDispatchJob` - every 1 minute - MOD-0027.
  - `ProvisioningRetryJob` - every 2 minutes - MOD-0009.
- [ ] For jobs whose owning module is missing, the registration contract is ready and real execution is deferred to the owning module pack.
- [ ] Multi-instance distributed lock/concurrency guard is implemented or explicitly configured through Hangfire for recurring jobs.
- [ ] Cron/config/secrets are not hardcoded.
- [ ] All schedules use UTC.
- [ ] Missing config behavior is documented and tested as safe disabled mode or clear startup failure.
- [ ] Existing MOD-0035 Event Bus code is referenced only through public abstractions.
- [ ] Runtime Golden Flow is proven end-to-end.
- [ ] Runtime Failure Path is proven end-to-end.
- [ ] No custom MVC UI, operational-looking button, or trigger endpoint is added without backing runtime behavior.

## 17. Test Expectations
- Build:
  - `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug`
  - `dotnet build services/Diten.Platform.Common/src/Diten.Platform.Common/Diten.Platform.Common.csproj -c Debug`
  - If `Diten.BuildingBlocks.BackgroundJobs` is created, build its actual repo-path `.csproj` using the repo's existing BuildingBlocks naming convention.
  - If fallback `Diten.Platform.Common.BackgroundJobs` is used, the Platform.Common build command above remains the shared abstraction validation.
  - Do not create or build both shared abstraction paths in the same implementation.
  - `dotnet build gateway/Diten.ApiGateway/Diten.ApiGateway.csproj -c Debug` only if Gateway route changes are approved.
- Unit tests:
  - `IBackgroundJobScheduler` abstraction maps fire-and-forget/delayed/recurring requests to adapter calls.
  - Recurring registration validates cron expressions and UTC timezone.
  - Duplicate recurring job id registration is idempotent or rejected according to implementation decision.
  - Job execution log writer records Started/Succeeded/Failed transitions.
  - Error redaction removes secrets, tokens, credentials, connection strings, and payload-like data.
  - Missing correlation context generates a correlation id.
  - Event-publishing job uses fake/in-memory MOD-0035 `IEventBus`.
  - `SchedulerSmokeTestJob` or approved smoke job has success and controlled failure modes.
- Integration tests:
  - Hangfire registration test confirms the 8 standard job descriptors are registered or intentionally disabled by configuration with documented owning modules.
  - Hangfire MongoDB storage configuration loads from appsettings/environment.
  - Hangfire MongoDB storage package/runtime compatibility is verified or reported as BLOCKED.
  - Job execution log repository writes and queries by `ServiceName`.
  - Smoke job success creates Started and Succeeded log proof.
  - Controlled failing smoke job creates Failed log proof and does not crash the platform.
  - Hangfire retry metadata is visible for the controlled failing smoke job.
  - Dashboard authorization smoke test verifies anonymous/non-platform access is rejected.
  - Dashboard authorization uses Hangfire authorization filter, not only MVC `[Authorize]`.
  - Config missing test verifies safe disabled behavior or clear startup failure.
  - Multi-instance/concurrency guard is covered by a targeted test or documented Hangfire behavior verification.
- Event Bus tests:
  - Fake/in-memory event bus publish is called by an event-producing job.
  - Job execution log includes `EventName` and `EventId` when an event-caused job context is supplied.
- Manual smoke:
  - Run repo audit and report existing scheduler state before implementation.
  - Confirm BuildingBlocks convention and shared abstraction path decision.
  - Confirm Hangfire.AspNetCore/Hangfire.Mongo package restore/build result in target project context.
  - Start Platform API with scheduler enabled.
  - Confirm Hangfire storage/config loads from configuration/environment.
  - Confirm recurring registrar runs.
  - Confirm 8 standard recurring job descriptors are registered or intentionally disabled by config with documented owners.
  - Open Hangfire Dashboard as PlatformActor and verify access.
  - Attempt dashboard access without PlatformActor and verify rejection.
  - Trigger a safe executable smoke job through integration test enqueue, Hangfire Dashboard manual trigger by PlatformActor, or internal test-only harness.
  - Trigger a controlled failing smoke job through the same allowed trigger paths and verify failure log, retry metadata, redaction, and platform stability.
  - Query execution by repository/service.

## 18. Ready-for-dev Checklist
- [x] Frontmatter includes all required fields: service, shell, golden_reference, entity_base, status, owner, branch, started, target, completed, form_field_count.
- [x] Status is `done`; implementation completed as PASS after runtime smoke.
- [x] Master-plan MOD-0026 status inconsistency is resolved as PASS / 90%.
- [x] Hangfire package/storage choice is confirmed, including MongoDB storage library.
- [x] Pre-audit found no existing scheduler/Hangfire runtime implementation.
- [x] Pre-audit found BuildingBlocks convention: `services/Diten.Building.Blocks` folder + `Diten.BuildingBlocks.*` project names.
- [x] Pre-audit temp compatibility restore/build succeeded for `Hangfire.AspNetCore` 1.8.23 + `Hangfire.Mongo` 1.15.0 targeting net8.0.
- [x] Scheduler abstraction target is approved and implemented through repo-actual BuildingBlocks convention.
- [x] Only one shared abstraction path was created/used.
- [x] No duplicate Building.Blocks/BuildingBlocks package structure was created.
- [x] Shared abstraction names are platform-neutral and contain no Platform business job names.
- [x] Platform implementation paths are approved.
- [x] Dashboard exposure path and authorization approach are approved.
- [x] Gateway decision is confirmed as unnecessary for MVP.
- [x] Standard initial jobs are accepted as registration descriptor scope, not full business implementation.
- [x] At least one executable smoke job exists; empty/no-op job classes were not used for PASS.
- [x] Event Bus integration rule is accepted: use MOD-0035 `IEventBus`, no new event abstraction.
- [x] Job execution log schema and indexes are accepted.
- [x] Missing config behavior is decided as safe disabled mode / configuration-driven registration.
- [x] Test expectations include Platform build, shared abstraction build, registration tests, log repository tests, dashboard auth smoke, and event bus boundary verification.
- [x] Output report contract is accepted.

## 19. Implementation Notes
- This pack is now complete as scheduler foundation PASS / 90%.
- This pack uses `shell: none` and `golden_reference: none` because it is infrastructure, not a DataTable module.
- `entity_base: BaseEntity` is used for Platform service persistence records. `TenantId` is nullable in `JobExecutionLog` because background work can be platform-wide or tenant-scoped.
- MOD-0035 currently has event bus foundation in the repo, including in-memory transport, MassTransit/RabbitMQ publisher, outbox/inbox records, and worker foundation. MOD-0026 must integrate with that public abstraction rather than duplicating it.
- `docs/platform/master-plan.md` detail section still mentions "Hangfire or Quartz"; this pack makes Hangfire the module-level decision.
- Existing master-plan status mismatch must be checked before development. Do not assume 70% implementation exists without repo inspection.
- Pre-audit found no scheduler/Hangfire runtime code, so implementation planning treats MOD-0026 as 0%.
- BuildingBlocks is suitable for the shared abstraction package unless target project package compatibility blocks it.
- The 8 standard jobs are initial registration descriptors. Their full execution logic belongs to their owning modules.
- `SchedulerSmokeTestJob` is the allowed executable proof job when business-dependent jobs are not ready.
- Boş class/no-op job yazıp PASS demek yasaktır.
- Dashboard authorization must account for Hangfire's own authorization filter model, not only MVC attributes.
- Job execution logs should complement Hangfire storage; they are business/ops metadata, not a replacement for Hangfire internal state.
- PASS cannot be claimed from file/class creation alone. PASS requires runtime Golden Flow and Failure Path proof.
- PASS evidence was supplied by live Platform API runtime smoke, protected dashboard authorization checks, Mongo-backed Hangfire enqueue, `SchedulerSmokeTestJob` success/failure `JobExecutionLog` records, redaction/retry metadata verification, and descriptor registration.

## Output Contract
Implementation final report must use this format:
- Batch status: PASS / PARTIAL / FAIL / BLOCKED
- Chosen Hangfire storage result
- Shared abstraction location decision:
  - Used BuildingBlocks path OR fallback Platform.Common path
  - Reason
  - Confirmation that duplicate shared packages were not created
- Changed files
- Golden flow proof
- Failure path proof
- Registered jobs list and schedules
- Dashboard authorization proof
- JobExecutionLog proof
- Validation commands and results
- Boundary check
- Open blockers / assumptions
- Next recommended step

## 20. Follow-up Items
- [x] Verify current repo for any existing Hangfire/scheduler implementation and reconcile with this pack.
- [x] Update `docs/platform/master-plan.md` MOD-0026 status after implementation starts or actual repo state is confirmed.
- [ ] Decide whether job failure emits a `background_job.failed.v1` event through MOD-0035 or waits for MOD-0021/MOD-0042.
- [ ] Prepare module-specific packs/updates for real execution logic of the 8 standard jobs as their owning modules mature.
- [ ] Consider a later Platform Admin job log viewer pack if Hangfire Dashboard is insufficient for service-level operational reporting.
