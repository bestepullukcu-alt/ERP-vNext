---
id: MOD-0021
name: Audit Trail Service
domain: platform-shared-services
service: Diten.Platform
shell: platform-admin
golden_reference: compact
entity_base: BaseEntity
status: ready-for-dev
owner: platform-shared-services
branch: feature/pss/mod-0021-general-audit-trail
started: 2026-05-14
target: 2026-06-11
form_field_count: 6
tier: 2
---

# MOD-0021 — Audit Trail Service

## Module Summary
MOD-0021 is the canonical parent Audit Trail Service capability for Platform & Shared Services. The General
Audit Trail scope in this file is its current delivery phase/slice; it is not a separate module or follow-up.
Do not create a new PSS or FU identity for this parent scope.

Purpose: provide a generic, immutable, tenant-aware audit trail for platform-significant operations across Diten ERP vNext. The module owns the append path, async/outbox audit write model, sensitive-field redaction, meta-audit, authorized query/export surfaces, and platform-level retention policy management.

Tier decision: Tier 2 audit implementation.

Golden Reference decision: `golden_reference: compact` is used as the DataTable/layout/file-structure discipline because `/Platform/AuditLog` is a DataTable screen with advanced filtering, export, and detail modal complexity, and `/Platform/AuditRetention` is a separate management page. This is a custom non-CRUD audit pattern: AuditEvent has no create/edit/delete UI even though the page must comply with DataTable v2 and Compact shell conventions. `form_field_count: 6` applies to the retention management form, not AuditEvent creation.

## Ownership and Boundaries
In-scope:
- Generic audit append infrastructure for platform-significant operations.
- `AuditBehavior` MediatR pipeline behavior for command audit instrumentation.
- Async audit write through `audit_outbox`.
- `AuditOutboxWorker` hosted service.
- Sensitive-field redaction registry and PII masking.
- Meta-audit for audit read/export/redaction/retention operations.
- Platform admin read/filter/detail/export UI.
- Platform admin retention policy UI.
- GDPR actor redaction endpoint that masks PII without deleting events.

Out-of-scope:
- Tenant-side ERP UI changes.
- Tenant-facing audit viewer.
- AuthService changes.
- Diten.MdmService changes.
- Dedicated `Diten.AuditService` extraction.
- Cryptographic hash chain.
- PGP-signed export.
- Dedicated cold-storage migration job.
- DR geo-replication.
- Formal evidence collection workflow.
- SOC2/ISO evidence package automation.

Tenant retention preference scope decision (confirmed):
- ✅ `TenantAuditPreference` backend entity + validation + persistence + repository **are in initial scope**.
- ✅ Platform admin retention policy UI (`/Platform/AuditRetention`) is in initial scope.
- ✅ Platform admin can update a tenant's preference on behalf of the tenant only via platform-admin controlled endpoints (no tenant-self-service yet).
- 🚫 Tenant-facing ERP UI for retention preference is **NOT** in initial scope; deferred to a follow-up pack.
- 🚫 Tenant-self-service API (a tenant user calling an endpoint to change their own retention) is **NOT** in initial scope; deferred to follow-up.
- 🚫 Tenant-side permission boundary (`Tenant.Audit.Retention.Update` or similar) is not introduced in this pack.

Result: tenant retention is *server-side configurable* (platform admin sets it per tenant), not *tenant-self-service* in MOD-0021 baseline.

Historical source note: the former `MOD-0021-audit-trail-service.md` skeletal artifact is historical reference
only and is not a competing source of truth. This file is the active parent pack for
`MOD-0021 — Audit Trail Service`; “General Audit Trail” names the current delivery phase/slice.

## Owned Objects
Domain objects:
- `AuditEvent` - tenant-aware immutable audit event record.
- `AuditCategory` enum - domain enum for category-based retention and filtering.
- `AuditOperation` enum - create/update/delete/activate/deactivate/suspend/reactivate/export/read/redact/retention-update/system. `AuditOperation.Delete` means deletion of the audited business entity or lifecycle state, not deletion of the `AuditEvent` record.
- `AuditActorType` enum - PlatformAdmin, PartnerAdmin, TenantUser, System.
- `AuditEventRetentionPolicy` - platform-level/global retention policy by category, plan/tier, and storage phase.
- `TenantAuditPreference` - tenant-scoped retention preference constrained by platform policy floor/ceiling.
- `AuditOutboxMessage` - queued audit write payload persisted in `audit_outbox`.
- `SensitiveFieldRule` / registry model - centralized redaction rule definition.

Application services and behaviors:
- `IAuditService`.
- `AuditService`.
- `IAuditOutboxWriter`.
- `AuditOutboxWorker : IHostedService`.
- `AuditBehavior<TRequest,TResponse>`.
- `ISensitiveFieldRedactionRegistry`.
- `SensitiveFieldRedactor`.
- `IAuditRecursionGuard`.
- `IAuditRetentionPolicyResolver`.
- `IAuditExportService`.

Commands:
- `UpdateAuditRetentionCommand`.
- `RedactAuditActorCommand`.
- `EnqueueAuditEventCommand` only if the implementation chooses a MediatR command for outbox append; direct service append is preferred. If this command exists, it must be excluded from `AuditBehavior` to avoid recursion.

Queries:
- `GetAuditEventListQuery`.
- `GetAuditEventByIdQuery`.
- `ExportAuditEventsQuery`.
- `GetAuditRetentionPolicyQuery`.

API endpoints:
- `GET /api/platform/audit/events`.
- `GET /api/platform/audit/events/{id}`.
- `GET /api/platform/audit/export`.
- `PUT /api/platform/audit/retention`.
- `POST /api/platform/audit/redact-actor`.

Forbidden endpoints and operations:
- `DELETE /api/platform/audit/events`.
- `DELETE /api/platform/audit/events/{id}`.
- PUT/PATCH audit event mutation.
- Bulk delete.
- Hard delete.
- Retention `0` days.
- Redaction that deletes events.
- Raw sensitive data export.
- Accepting `TenantId` from client payload.

Frontend:
- `PlatformAuditController` MVC proxy controller (same name as backend API controller).
- `/Platform/AuditLog`.
- `/Platform/AuditRetention`.
- Audit Log DataTable with advanced filters.
- Detail modal with before/after JSON diff.
- Export action for CSV/JSON.
- Retention policy management page for platform admin.

Permissions:
- `Platform.Audit.Read`.
- `Platform.Audit.Export`.
- `Platform.Audit.Retention.Update`.
- `Platform.Audit.RedactActor`.

## Entity Fields
### AuditEvent
Base decision: `AuditEvent : BaseEntity`.

Reason: the record is tenant-aware inside `Diten.Platform`, so the concrete platform base class is `BaseEntity`, not `EntityBase`. The inherited `TenantId` is the target tenant isolation key and is mandatory for tenant-scoped events. For platform-global events, use the explicit platform/system tenant convention already present in Platform service, not a client-supplied value. The inherited `IsDeleted` and `DeletedAt` fields exist because of the base contract, but MOD-0021 disables delete paths by invariant: audit events are append-only and must remain `IsDeleted=false`.

| Field | Type | Required | Rules / indexes |
|---|---|---|---|
| Base | `BaseEntity` | Yes | Inherits `Id`, `TenantId`, `IsDeleted`, `DeletedAt`, `CreatedAt`, `UpdatedAt`, `Version`. `IsDeleted` must never become true. |
| CorrelationId | `Guid` | Yes | Required. Index with `OccurredAtUtc`. |
| ActorType | `AuditActorType` | Yes | Domain enum, no magic strings. |
| ActorId | `Guid?` | Conditional | Required for user/platform actors; nullable only for system events. Index. |
| ActorEmailMasked | `string?` | No | PII-safe/masked denormalized actor label. Raw email cannot remain after actor redaction. |
| ActorDisplayNameMasked | `string?` | No | PII-safe/masked display label. |
| TargetTenantId | `Guid?` | Conditional | Target tenant for platform cross-tenant actions; must be server-resolved. Index. |
| Category | `AuditCategory` | Yes | Domain enum. Index for retention/export filters. |
| EntityType | `string` | Yes | Max 160. Index. |
| EntityId | `Guid?` | No | Required when event targets a concrete entity. |
| Operation | `AuditOperation` | Yes | Domain enum. Index with `OccurredAtUtc`. |
| BeforeState | `object?` / persisted document | No | Must be redacted before persistence. |
| AfterState | `object?` / persisted document | No | Must be redacted before persistence. |
| Metadata | `Dictionary<string, object?>` | No | Must pass redaction before persistence/export. |
| IpAddressMasked | `string?` | No | Masked or truncated according to privacy policy. |
| UserAgent | `string?` | No | Max length enforced. |
| OccurredAtUtc | `DateTimeOffset` | Yes | Set server-side. Primary descending list index. |
| WrittenAtUtc | `DateTimeOffset` | Yes | Set by outbox worker when persisted. |
| SourceService | `string` | Yes | Example: `Diten.Platform`. |
| SourceModule | `string?` | No | Example: `PlatformAdministrators`. |
| IsMetaAudit | `bool` | Yes | Required recursion guard input. |
| RedactionStatus | `string` or enum | Yes | None, ActorRedacted, PayloadRedacted. Prefer enum if used in logic. |
| RedactedAtUtc | `DateTimeOffset?` | No | Set only by GDPR redaction. |
| RedactedByActorId | `Guid?` | No | Platform actor who performed redaction. |
| RedactionReason | `string?` | No | Required for redaction operation. |

Required indexes:
- `{ TenantId, OccurredAtUtc desc }`.
- `{ TargetTenantId, OccurredAtUtc desc }`.
- `{ ActorId, OccurredAtUtc desc }`.
- `{ Category, OccurredAtUtc desc }`.
- `{ EntityType, EntityId, OccurredAtUtc desc }`.
- `{ Operation, OccurredAtUtc desc }`.
- `{ CorrelationId }`.

### AuditEventRetentionPolicy
Base decision: `AuditEventRetentionPolicy : GlobalEntity`.

Reason: retention policy is a platform-level system-of-record, not tenant-owned. It defines global floor/ceiling/defaults by category and plan/tier. DTO/request payload must not include `TenantId`. Normal list/detail still filters `IsDeleted=false`, but no audit event records may be deleted because of retention policy changes.

| Field | Type | Required | Rules / indexes |
|---|---|---|---|
| Base | `GlobalEntity` | Yes | Platform-global policy. |
| Category | `AuditCategory` | Yes | Unique with `PlanTierCode`. |
| PlanTierCode | `string` | Yes | Max 80; platform packaging vocabulary. |
| DefaultRetentionDays | `int` | Yes | Must be > 0 and within floor/ceiling. |
| MinimumRetentionDays | `int` | Yes | Must be > 0. |
| MaximumRetentionDays | `int` | Yes | Must be >= minimum. |
| HotStorageDays | `int` | Yes | Must be > 0 and <= default retention. Preparation only; no migration job. |
| ColdStoragePrepared | `bool` | Yes | Metadata only for Tier 2 hot/cold readiness. |
| AllowTenantOverride | `bool` | Yes | Controls `TenantAuditPreference`. |
| IsActive | `bool` | Yes | Active policy selector. |

### TenantAuditPreference
Base decision: `TenantAuditPreference : BaseEntity`.

Reason: this preference is tenant-scoped inside `Diten.Platform`; it inherits server-resolved `TenantId`. Tenant preference must never accept TenantId from request bodies and must obey platform floor/ceiling.

| Field | Type | Required | Rules / indexes |
|---|---|---|---|
| Base | `BaseEntity` | Yes | Tenant-scoped preference. |
| Category | `AuditCategory` | Yes | Unique with `TenantId`. |
| RetentionDays | `int` | Yes | Must be > 0 and within policy floor/ceiling. |
| EffectiveFromUtc | `DateTimeOffset` | Yes | Server-side or validated future/current date. |
| UpdatedByActorId | `Guid` | Yes | Server-side platform actor for this module scope. |
| Reason | `string?` | No | Max 500. |

### AuditOutboxMessage
Collection decision: persisted in MongoDB collection `audit_outbox`. It is an internal infrastructure record, not user-editable.

| Field | Type | Required | Rules / indexes |
|---|---|---|---|
| Id | `Guid` | Yes | Queue id. |
| TenantId | `Guid` | Yes | Server-resolved target tenant/platform tenant. |
| CorrelationId | `Guid` | Yes | Trace correlation across services. Index. |
| IdempotencyKey | `string` | Yes | Deterministic key from `CorrelationId + RequestType + EntityId + Operation + sequence`. Unique index for duplicate prevention. |
| RequestType | `string?` | No | MediatR request type full name; aids debugging and selective replay. |
| Operation | `AuditOperation?` | No | Mirrors planned `AuditEvent.Operation` for fast filtering before write. |
| EntityType | `string?` | No | Mirrors planned `AuditEvent.EntityType`. |
| EntityId | `Guid?` | No | Mirrors planned `AuditEvent.EntityId`. |
| Payload | `object` | Yes | Already redacted before or during enqueue. Raw sensitive data must never reach this field. |
| Status | enum | Yes | Pending, Processing, Completed, Failed, DeadLetter. |
| Attempts | `int` | Yes | Retry count. |
| NextAttemptAtUtc | `DateTimeOffset` | Yes | Worker scheduling index. |
| CreatedAtUtc | `DateTimeOffset` | Yes | Queue insert time. |
| LastError | `string?` | No | Truncated, must not include sensitive data. |

Required outbox indexes:
- `{ IdempotencyKey }` unique — duplicate enqueue prevention.
- `{ CorrelationId }` — replay and correlation trace.
- `{ Status, NextAttemptAtUtc }` — worker dispatch scan.
- `{ TenantId, CreatedAtUtc desc }` — operational diagnostics per tenant.

## Repo Scope
Allowed backend scope:
- `services/Diten.Platform/src/Diten.Platform.Domain/**` for Audit entities/enums/repository interfaces.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/**`.
- `services/Diten.Platform/src/Diten.Platform.Application/Common/Behaviors/**` for `AuditBehavior`.
- `services/Diten.Platform/src/Diten.Platform.Application/Interfaces/**` for audit abstractions.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/**` for MongoDB repositories, indexes, `audit_outbox` collection, and seed configuration. Do not create a parallel `Diten.Platform.Persistence` project; repo standard is `Infrastructure/Persistence/**`.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/**` for worker/redaction/export services if existing layering places hosted services there.
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/Platform/PlatformAuditController.cs` for the backend API controller. Same name (`PlatformAuditController`) is used on both backend API and frontend MVC proxy for symmetry (PSS-009 pattern). The generic name `AuditController` is forbidden because `Diten.EnterpriseStrategyService` already owns `AuditEvent`-named domain types and the ambiguity creates cross-service confusion. If a future resource-based rename is required, the only approved alternative is `PlatformAuditEventsController` with explicit pack revision.
- `services/Diten.Platform/src/Diten.Platform.API/Program.cs` or DI registration files for behavior/worker registration.

Allowed frontend scope:
- `frontend/Diten.Web/Controllers/Platform/PlatformAuditController.cs` — MVC same-origin proxy controller. Name matches backend API controller for symmetry.
- `frontend/Diten.Web/Views/Platform/AuditLog/**`.
- `frontend/Diten.Web/Views/Platform/AuditRetention/**`.
- `frontend/Diten.Web/wwwroot/assets/js/Platform/AuditLog/**`.
- `frontend/Diten.Web/wwwroot/assets/js/Platform/AuditRetention/**`.
- `frontend/Diten.Web/Resources/Views/Platform/AuditLog/**`.
- `frontend/Diten.Web/Resources/Views/Platform/AuditRetention/**`.

Gateway:
- `gateway/Diten.ApiGateway/**` inspection only.
- Direct `ocelot.json` edits are not allowed in this pack unless the integration-agent owns that change.

Documentation:
- This module pack only unless the user explicitly approves master-plan status updates after implementation.

## Protected Paths
- `.antigravity/**`.
- `frontend/Diten.Web/Controllers/Archive/**`.
- `frontend/Diten.Web/Views/Archive/**`.
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`.
- `gateway/Diten.ApiGateway/**/ocelot.json` except integration-agent route work.
- `services/Diten.AuthService/**`.
- `services/Diten.MdmService/**`.
- `services/Diten.DevEnablementService/**`.
- `services/Diten.EnterpriseStrategyService/**`.
- Tenant-side ERP module views/controllers/scripts.
- Any existing audit records in local MongoDB or seed data unless an explicit migration/test fixture is approved.

## Dependencies
- MongoDB single-instance, multi-tenant persistence.
- JWT + RBAC with PlatformActor policy.
- Existing `Response<T>` envelope and `CustomBaseController`.
- Existing four MediatR pipeline behaviors; `AuditBehavior` must be registered in a deterministic order that avoids auditing validation failures unless explicitly designed.
- Correlation ID propagation (`X-Correlation-Id`).
- Current user/platform actor context.
- Platform lookup/reference data only if UI needs category/tier option lists.

Lookup decision:
- `AuditCategory` and `AuditOperation` are Domain enums for invariant logic.
- UI filters may render enum values from localized resources or a Platform lookup proxy, but hardcoded fallback lists are forbidden.
- If implementation introduces new lookup keys, they must be declared through PSS-011 lookup flow and consumed as `GET /api/lookups/{key}` with `Response<IReadOnlyList<LookupOptionDto>>`.
- Browser JS must use same-origin MVC proxy or Gateway. It must not call Platform service port `5057`.

## Runtime Constraints
- Audit events are immutable. No DELETE endpoint, no update endpoint, no patch endpoint, and no repository delete path for `AuditEvent`.
- `AuditOperation.Delete` records deletion of a business entity or lifecycle operation performed by another module. It never means deleting an audit event. `AuditEvent` remains immutable and append-only.
- `AuditBehavior` is opt-in, not global. It must not audit every MediatR request.
- Opt-in decision: only commands implementing an explicit marker interface such as `IAuditableCommand`/`IAuditableRequest`, or commands explicitly registered in a central audit map, are captured by `AuditBehavior`.
- Queries are not audited by `AuditBehavior` by default. Query-side audit is explicit and limited to security/compliance-sensitive operations such as audit export, cross-tenant audit read, detail read if required by product/security, and redaction/retention meta-audit.
- System/internal commands are excluded by default. Infrastructure commands, outbox worker commands, retry/dead-letter operations, health checks, heartbeat jobs, seed/setup commands, and audit append commands must implement an exclusion marker such as `IAuditExcludedRequest` or be absent from the auditable registration map.
- Explicit exclusion wins over opt-in. If a request is both auditable and excluded, exclusion wins and the implementation must log a configuration warning.
- Duplicate audit prevention is required for behavior-driven writes: use a deterministic audit idempotency key based on `CorrelationId`, request type, entity id, operation, and a request-scoped sequence when needed. Outbox enqueue must reject or coalesce duplicate keys for the same completed command.
- Significant business commands are audited through `AuditBehavior` when they are marked/registered. Use explicit `IAuditService` calls only when pipeline capture is insufficient, and document the reason in the implementing module.
- Audit writes use async/outbox behavior so business command latency is not tied to MongoDB audit insert latency.
- Audit enqueue failure must not unnecessarily break the business command. Required behavior: log the failure, emit operational diagnostics, and only fail business command for explicitly critical audit categories approved in implementation notes.
- Sensitive fields are redacted before persistence and before export.
- Password, token, secret, API key, connection string, authorization header, refresh token, private key, and similarly named fields are never logged raw.
- `TenantId` is server-resolved. Client payloads cannot set or override it.
- Tenant isolation is a critical invariant. Tenant A cannot read Tenant B audit events. Cross-tenant reads require PlatformActor plus `Platform.Audit.Read`.
- Platform admin cross-tenant query is itself meta-audited.
- Export is meta-audited through an explicit audit write after authorization and before/after export completion as implementation chooses; it is not captured by default query auditing.
- GDPR actor redaction is meta-audited and must not delete events.
- Meta-audit recursion is blocked by all of these rules: meta-audit writes set `IsMetaAudit=true`, run inside an `IAuditRecursionGuard` scope, and audit append/outbox worker requests are excluded from `AuditBehavior`. A meta-audit event must never trigger another meta-audit event for the same operation.
- Retention cannot be `0` days.
- Tenant retention cannot be below platform floor or above platform ceiling.
- Volume guardrail baseline: heartbeat/noise/system retry events are excluded from Tier 2 audit capture unless explicitly registered as significant security/compliance events. Advanced rate limiting, sampling, aggregation, and retry-storm suppression are future guardrails and must not complicate the baseline implementation.
- Hot/cold storage is preparation metadata in Tier 2. Dedicated cold-storage migration job is Tier 3 and out of scope.

## Cross-service producer integration baseline

### Platform-local General Audit Trail phase

- The existing `AuditBehavior` and Platform `audit_outbox` behavior belong to the Platform-local General
  Audit Trail delivery phase.
- The rule that an audit enqueue failure does not break a business command by default applies only to that
  Platform-local behavior. It does not authorize a cross-service producer to commit a required audited
  mutation without first persisting its local audit intent.
- Frontmatter `status: ready-for-dev` describes this existing Platform-local General Audit Trail phase. It
  does not make the cross-service producer integration subset ready, grant production authority or close
  DCP-006 OD-04.

### Cross-service producers, including DWS

- A required audited mutation and its producer-local technical audit intent/outbox persist in the same
  replica-set transaction. If the local intent cannot be written, the mutation rolls back.
- After commit, the producer publishes through a versioned semantic provider/consumer contract. Delivery is
  asynchronous and durable at-least-once, and the MOD-0021 consumer is idempotent; exactly-once is not
  claimed.
- A broker, consumer or Platform failure after commit does not roll back the business mutation or sealed
  baseline. Retry, dead-letter, alarm and authorized replay are mandatory.
- Producers cannot access Platform `audit_outbox`, `audit_events` or other MOD-0021 collections directly.
  The shared-key `/api/internal/audit/append` endpoint is not the authoritative cross-service baseline.
- Publisher service identity, tenant and actor come only from authenticated server/transport context and
  are matched fail-closed to the allowed source/module. Client-supplied identity values are not trusted.
- Payloads are minimal, allowlist-based and redacted, with explicit byte, depth, collection-count and
  string-length limits. Full business/DWS tree or revision snapshots and unrestricted dictionaries are
  forbidden.
- `AuditIntentPersisted` and `AuditEventAcceptedByMOD0021` are technical observability states only. They
  cannot become a business lifecycle, revision status, task, workflow or approval state.
- For the MOD-0117 producer only, the event type is `PpmAuditIntentSubmittedV1` and EventName/routing key is
  `ppm.audit-intent.submitted.v1`. This locks the supplied type/name/version identity; only the remaining
  payload-schema/runtime evidence may be completed without allocating a new module/FU or widening authority.
- Compatibility fixtures, authenticated publisher credential and production rollout remain runtime
  evidence gates. The final payload and MOD-0021 consumer mapping are fixed below. The producer worker may
  use only MOD-0035's public `IEventBus`/outbox abstraction; PPM handlers/controllers cannot call RabbitMQ or
  MassTransit directly.
- PSS-C1 provides that public canonical payload/outbox/trusted-metadata seam in
  `Diten.BuildingBlocks.Eventing` and propagates its allowlisted headers through Platform transport
  adapters. It does not implement the PPM producer, allocate a signing key, or close the cross-service
  producer integration subset; live RabbitMQ delivery and producer-local transaction evidence remain open.
- During PSS-C2 expand–contract, shared and temporary legacy transport URNs feed the same MOD-0021
  consumer processor and the same `(ConsumerName, EventId)` inbox/idempotency record. Receiving both
  identities for one `EventId` therefore creates one business audit event; the legacy bridge neither
  republishes nor creates a second inbox/retry chain.
- This cross-service subset remains `PARTIAL`; no implementation permission or production authority is
  created here, and DCP-006 OD-04 remains open.

#### `PpmAuditIntentSubmittedV1` — Minimal Mutation Audit v1 (final)

MOD-0035 envelope values: `EventId` equals the immutable producer-local `AuditIntentId`; `EventName` is
`ppm.audit-intent.submitted.v1`; `EventVersion` is `1`; `TenantId` is required; `Producer` is exactly
`Diten.PpmService`; `CorrelationId`, optional `CausationId`, and UTC `OccurredAtUtc` use the shared envelope.

The HMAC signing input is versioned by `ppm-event-hmac-sha256.v1` and is the exact UTF-8 concatenation of
newline-terminated fields in this order: scheme, canonical `EventId`, `EventName`, invariant `EventVersion`,
canonical `TenantId`, canonical `CorrelationId`, exact `Producer`, canonical `CausationId` or literal `-`
when absent, UTC round-trip `OccurredAtUtc`, invariant canonical-payload byte length, then the exact canonical
payload bytes. The signature is exactly 64 lowercase hexadecimal characters (`[0-9a-f]{64}`); uppercase or
alternative representations fail closed.

Payload is an exact object with six properties and no extensions:

| Property | Type | Rule |
|---|---|---|
| `auditIntentId` | non-empty Guid | Must equal envelope `EventId` |
| `actorId` | non-empty Guid | From the authenticated mutation context persisted in the immutable local intent |
| `entityType` | closed ASCII string | `Portfolio`, `Initiative`, `Program`, `Project` |
| `entityId` | non-empty Guid | PPM aggregate identity only |
| `mutation` | closed ASCII string | `created`, `updated`, `lifecycle-changed`, `soft-deleted` |
| `occurredAtUtc` | UTC DateTime | Must equal envelope `OccurredAtUtc` |

Tenant and actor are never accepted from a client request at publish time: they originate in the
transactional local intent written from authenticated server context. The MOD-0035 transport authenticates
publisher service identity and authorizes only `Diten.PpmService` for this EventName. The consumer rejects
identity/envelope mismatches fail-closed.

Canonical UTF-8 payload is at most 2048 bytes, depth at most 2, exactly six properties, no arrays,
dictionaries or unknown fields; strings are ASCII and at most 32 bytes. Full aggregate snapshots,
before/after values, descriptions, permission inventories, tokens, secrets and exception text are forbidden.

Idempotency key is `ConsumerName + EventId` and `auditIntentId == EventId`. Delivery is at-least-once and
uses 5 total attempts. The delay after the first failed attempt is 10 seconds, then exponential backoff with
jitter applies up to a 5-minute maximum. Failure of the fifth attempt causes DLQ plus alarm; the initial
attempt is included, leaving four retry attempts.

Authorized replay preserves the same EventId and identical canonical payload bytes; changed bytes are
rejected. If the first delivery was not accepted, replay may create exactly one `AuditEvent`; if accepted,
replay creates none. Unauthorized replay is forbidden and no replay UI/API is authorized. Schema/identity
failure is not retried until a compatible consumer or corrected authorized disposition exists. V1 rejects
unknown properties/versions; any payload change requires compatibility fixtures and a new event version.

This six-field event provides limited evidence only of who performed which minimal mutation against which
PPM aggregate and when. It is not authorization/entitlement evidence, a business snapshot, before/after
record, permission inventory or complete lifecycle history.

Shared `EventEnvelope`, `IEventBus`, outbox and inbox mechanics are owned by
`Diten.BuildingBlocks.Eventing`. MOD-0117 owns the logical PPM event at the planned
`services/Diten.PpmService/src/Diten.PpmService.Contracts/Events/**` path. Platform is consumer-only for
this event; `Diten.Platform.Contracts` owns other Platform events where applicable, but not the PPM event.

Runtime repo scope for this proposed consumer is limited to
`services/Diten.Platform.Contracts/**`, narrow
`services/Diten.Platform/src/Diten.Platform.Infrastructure/Eventing/**`,
`services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/**` and their PSS tests. It does not
authorize PPM service changes, direct producer access to Platform collections, frontend or Gateway work.
The subset remains non-executable until MOD-0035's status gate and explicit user runtime approval close.

## Layout & Shell Contract
- `shell: platform-admin`.
- Razor layout: every `.cshtml` page in `Views/Platform/AuditLog/` and `Views/Platform/AuditRetention/` must explicitly set `Layout = "_LayoutPlatformAdmin";`.
- `_ViewStart.cshtml` must not be changed.
- View routes:
  - `/Platform/AuditLog`.
  - `/Platform/AuditRetention`.
- Browser-facing Platform/Admin JS uses same-origin MVC proxy routes, for example `/Platform/AuditLog/api/events`, not direct `http://localhost:5057`.
- DataTable pages must include `data-dt-standard="v2"` and the skeleton loader contract.

## Backend File Convention
Golden Reference Compact folder/naming applies where CQRS files are added:

```text
services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/
├── Commands/
│   ├── UpdateAuditRetentionCommand.cs
│   └── RedactAuditActorCommand.cs
├── Queries/
│   ├── GetAuditEventListQuery.cs
│   ├── GetAuditEventByIdQuery.cs
│   ├── ExportAuditEventsQuery.cs
│   └── GetAuditRetentionPolicyQuery.cs
├── Handlers/
│   ├── CommandHandlers/
│   │   ├── UpdateAuditRetentionHandler.cs
│   │   └── RedactAuditActorHandler.cs
│   └── QueryHandlers/
│       ├── GetAuditEventListHandler.cs
│       ├── GetAuditEventByIdHandler.cs
│       ├── ExportAuditEventsHandler.cs
│       └── GetAuditRetentionPolicyHandler.cs
├── Validators/
│   ├── UpdateAuditRetentionValidator.cs
│   ├── RedactAuditActorValidator.cs
│   └── ExportAuditEventsValidator.cs
└── AuditModels.cs
```

Naming rules:
- Commands are sealed records ending in `Command`.
- Queries are sealed records ending in `Query`.
- Handlers are sealed classes ending only in `Handler`; do not use `CommandHandler`, `QueryHandler`, or `RequestHandler` suffixes.
- Validators are sealed classes ending only in `Validator`; do not use `CommandValidator` suffixes.
- DTO/view model records live in `AuditModels.cs`.
- No grouped public command/query/handler classes in one file.
- Controller inherits `CustomBaseController` and delegates to MediatR only.

Non-CRUD exception:
- Do not add `CreateAuditEventCommand`, `UpdateAuditEventCommand`, `DeleteAuditEventCommand`, or `BulkDeleteAuditEventCommand` for user/API mutation. Internal append can be service-based and must preserve immutability.

## Frontend File Contract
AuditLog read-only DataTable file set:
- `frontend/Diten.Web/Views/Platform/AuditLog/Index.cshtml`.
- `frontend/Diten.Web/Views/Platform/AuditLog/_Filter.cshtml`.
- `frontend/Diten.Web/Views/Platform/AuditLog/_DataTable.cshtml`.
- `frontend/Diten.Web/Views/Platform/AuditLog/_IndexL10n.cshtml`.
- `frontend/Diten.Web/Views/Platform/AuditLog/_DetailsModal.cshtml`.
- `frontend/Diten.Web/Views/Platform/AuditLog/AuditLogIndex.cs`.
- `frontend/Diten.Web/wwwroot/assets/js/Platform/AuditLog/index.js`.
- `frontend/Diten.Web/wwwroot/assets/js/Platform/AuditLog/index.l10n.js`.
- `frontend/Diten.Web/Resources/Views/Platform/AuditLog/AuditLogIndex.en.resx`.
- `frontend/Diten.Web/Resources/Views/Platform/AuditLog/AuditLogIndex.tr.resx`.

AuditRetention management file set:
- `frontend/Diten.Web/Views/Platform/AuditRetention/Index.cshtml`.
- `frontend/Diten.Web/Views/Platform/AuditRetention/_Form.cshtml`.
- `frontend/Diten.Web/Views/Platform/AuditRetention/_IndexL10n.cshtml`.
- `frontend/Diten.Web/Views/Platform/AuditRetention/AuditRetentionIndex.cs`.
- `frontend/Diten.Web/wwwroot/assets/js/Platform/AuditRetention/index.js`.
- `frontend/Diten.Web/wwwroot/assets/js/Platform/AuditRetention/index.l10n.js`.
- `frontend/Diten.Web/Resources/Views/Platform/AuditRetention/AuditRetentionIndex.en.resx`.
- `frontend/Diten.Web/Resources/Views/Platform/AuditRetention/AuditRetentionIndex.tr.resx`.

Compact adaptation rules:
- AuditLog must not include create/edit/delete UI.
- AuditLog detail is a modal with before/after JSON diff, not a mutable details page.
- AuditRetention may use a shared `_Form.cshtml` for policy update.
- Platform localization is en + tr only.
- Use SweetAlert2 premium modal standard for confirmation/error states.
- Text and controls must not call service port `5057` directly.

## Validation Rules
| Field / request | Required | Rule | Failure |
|---|---|---|---|
| Audit query date range | No | Start <= end; maximum range must be enforced for export if needed | 400 |
| Audit query tenant filter | No | Platform admin only; server validates tenant access | 403 or 404 |
| Audit query actor | No | GUID/email search must be length-limited and sanitized | 400 |
| Audit query category | No | Must parse to `AuditCategory` enum | 400 |
| Audit query operation | No | Must parse to `AuditOperation` enum | 400 |
| Audit query entity type | No | Max 160, sanitized | 400 |
| Audit event id | Yes for detail | Existing event visible to actor scope | 404 for missing or cross-tenant |
| Export format | Yes | `csv` or `json` only | 400 |
| Export filters | Yes | Same as read filters; raw sensitive fields never exported | 400/403 |
| Retention category | Yes | Valid `AuditCategory` enum | 400 |
| Plan/tier code | Yes | Existing platform tier code or approved lookup | 400 |
| DefaultRetentionDays | Yes | > 0 and between floor/ceiling | 400 |
| MinimumRetentionDays | Yes | > 0 | 400 |
| MaximumRetentionDays | Yes | >= minimum and > 0 | 400 |
| HotStorageDays | Yes | > 0 and <= DefaultRetentionDays | 400 |
| Tenant RetentionDays | Yes if backend preference is implemented | > 0, >= platform floor, <= platform ceiling | 400 |
| Redact actor id | Yes | Actor id required; cannot be empty GUID | 400 |
| Redaction reason | Yes | Required, max 500 | 400 |
| Redaction payload | Yes | Must not request event deletion | 400 |
| TenantId in client body | No | Reject; server owns TenantId | 400 |
| Sensitive field registry | Yes | Field matching is case-insensitive and nested-path aware | Unit test failure if raw leak |

## Failure Path to Verify
- Missing permission for read returns 401/403 and no audit data.
- Missing permission for export returns 401/403 and no file.
- Missing permission for redaction returns 401/403 and no mutation.
- Missing permission for retention update returns 401/403 and no policy update.
- Tenant A cannot query Tenant B audit event; response is 404 or 403 according to active policy, never leaked data.
- Non-platform tenant actor cannot perform cross-tenant query.
- `DELETE /api/platform/audit/events` returns 405 or 404 and does not delete data.
- `DELETE /api/platform/audit/events/{id}` returns 405 or 404 and does not delete data.
- PUT/PATCH audit event mutation routes are absent or return 405.
- Bulk delete is absent or returns 405.
- Retention `0` days returns 400.
- Tenant retention below floor returns 400.
- Tenant retention above ceiling returns 400.
- Export with sensitive fields returns redacted values.
- Redaction masks actor PII and leaves the event row intact.
- Redaction creates a redaction trace/meta-audit event.
- Meta-audit does not recursively enqueue infinite audit records.
- A request implementing both auditable and excluded markers is excluded and emits a configuration warning.
- Duplicate auditable command processing does not create duplicate audit events for the same idempotency key.
- Heartbeat/health/retry/outbox worker operations are not audited unless explicitly registered as significant events.
- Audit outbox enqueue failure is handled according to runtime constraint and does not create a fake audit timeline.
- Platform-local enqueue failure follows the General Audit Trail phase rule; a cross-service required
  audit-intent insert failure instead rolls back the producer mutation.
- Cross-service duplicate delivery creates at most one effective MOD-0021 append through the idempotent
  consumer.
- Unknown contract major version fails closed to dead-letter; no guessed deserialization occurs.
- Post-commit broker/consumer/Platform failure leaves the business mutation committed and enters
  retry/dead-letter/alarm/authorized-replay handling.
- Forged publisher service, tenant or actor fails closed and cannot create an accepted audit event.
- Cross-service payload outside its allowlist or byte/depth/count/string limits fails closed before
  publication; no full business/DWS snapshot is emitted.
- Unauthorized export attempts are blocked and, where safe, security-audited without exposing data.
- Audit query/read/export latency remains bounded by pagination and export limits.

## Authorization Convention
- API controllers require `[Authorize(Policy = "PlatformActor")]`.
- Endpoint permissions:
  - `GET /api/platform/audit/events` -> `[HasPermission("Platform.Audit.Read")]`.
  - `GET /api/platform/audit/events/{id}` -> `[HasPermission("Platform.Audit.Read")]`.
  - `GET /api/platform/audit/export` -> `[HasPermission("Platform.Audit.Export")]`.
  - `PUT /api/platform/audit/retention` -> `[HasPermission("Platform.Audit.Retention.Update")]`.
  - `POST /api/platform/audit/redact-actor` -> `[HasPermission("Platform.Audit.RedactActor")]`.
- Permission format is `Platform.Audit.*` because this module is implemented in `Diten.Platform` and uses platform-admin shell.
- Tenant-level future access must use a separate boundary and must not reuse platform cross-tenant permissions.
- Platform admin cross-tenant query/export is allowed only with explicit permission and must be meta-audited.
- Admin safety guardrails apply to actor-affecting redaction or retention operations where they can alter visibility/accountability of platform admin records.

## Gateway / API Routing Decision
- Required upstream API surface:
  - `/api/platform/audit/events`.
  - `/api/platform/audit/events/{everything}`.
  - `/api/platform/audit/export`.
  - `/api/platform/audit/retention`.
  - `/api/platform/audit/redact-actor`.
- Gateway route inspection is in scope.
- Direct `gateway/Diten.ApiGateway/**/ocelot.json` edit is not in scope for the implementing agent unless routed to integration-agent.
- If routes are missing, create an integration-agent task to add explicit Ocelot routes to Platform service port `5057` with `GET`, `POST`, `PUT`, `PATCH`, `DELETE`, and `OPTIONS` as appropriate. Even forbidden methods may route to API 405 responses; the audit controller must still not expose forbidden handlers.
- Frontend Platform/Admin JS uses same-origin MVC proxy profile, not direct service ports.
- Phase 5C final hardening ownership note (2026-05-14): existing MOD-0021 Ocelot audit routes in `gateway/Diten.ApiGateway/ocelot.json` are recorded as integration-agent-owned route work. This final hardening task did not change gateway route configuration.

## Domain Invariants
Aşağıdaki kurallar **her zaman** doğru kalmalı — kod, test ve review hepsi bunları korumalı:

- **Audit events ASLA silinemez.** API/UI/repository/worker hiçbir path event'i delete edemez. `IsDeleted=false` invariant'tır.
- **`AuditEvent` immutable + append-only.** Mutate eden API/repo metodu yoktur. Yalnızca GDPR redaction PII field'larını maskeler (kayıt korunur).
- **`TenantId` ASLA client payload'dan alınmaz.** Server-side resolve edilir. Aksi davranış 400 ile reddedilir.
- **Tenant A, Tenant B audit event'lerini ASLA göremez.** Query/detail/export her yolda izolasyon zorlanır.
- **Platform cross-tenant query explicit permission gerektirir** (`Platform.Audit.Read`) ve meta-audit'lenir.
- **Sensitive field'lar ASLA raw persist veya export edilmez.** Redaction registry tarafından maskelenir.
- **Meta-audit recursive olamaz.** `IsMetaAudit=true` + `IAuditRecursionGuard` koruması altında yazılır. Meta-audit kendi meta-audit'ini tetikleyemez.
- **Retention `0` gün olamaz.** Platform floor < tenant value < platform ceiling.
- **Tenant retention floor altında veya ceiling üstünde olamaz.** Validation 400 ile reddeder.
- **GDPR redaction event'i silmez**, yalnızca PII field'larını maskeler ve `RedactionStatus`/`RedactedAtUtc`/`RedactedByActorId`/`RedactionReason` set eder.
- **Export işlemi audit edilir** (meta-audit). Authorization + filter snapshot + row count outcome kaydedilir.
- **`AuditOperation.Delete` business entity deletion anlamına gelir**, ASLA `AuditEvent` deletion anlamına gelmez.
- **`AuditBehavior` opt-in'dir.** Yalnızca `IAuditableRequest` ile işaretli command'lar capture edilir; query'ler ve sistem command'ları varsayılan olarak hariç.
- **Duplicate audit prevention zorunludur.** `IdempotencyKey` ile aynı command iki audit event üretemez.

## Forbidden Operations
Bu modülde **ASLA** implement edilmeyecek olanlar (kapsam dışı değil — açıkça yasaklı):

- `DELETE /api/platform/audit/events` — yok; route bile tanımlanmaz, 405 döner.
- `DELETE /api/platform/audit/events/{id}` — yok.
- `PUT` / `PATCH` audit event mutation route'ları — yok.
- Bulk delete endpoint'i — yok.
- Hard delete repository path'i — yok (`IAuditRepository`'de delete metodu yok).
- Retention `0` gün kabul eden API/UI path — yok.
- Redaction endpoint'inin event silmesi — yasak; yalnızca maskeleme.
- Raw sensitive data export — yasak; redaction defansif olarak export'ta tekrar uygulanır.
- `TenantId`'nin client payload'dan kabul edilmesi — yasak.
- `AuditEvent` için Create/Edit/Delete UI — yok (`golden_reference: compact` ama mutable UI değil).
- `CreateAuditEventCommand` / `UpdateAuditEventCommand` / `DeleteAuditEventCommand` / `BulkDeleteAuditEventCommand` — yok. Internal append service-based.
- `[AllowAnonymous]` audit endpoint — yok.
- `AuditBehavior`'ın global olarak tüm `IRequest`'lere takılması — yasak.
- Tenant-self-service retention API'si — initial scope dışı (follow-up).
- Tenant-facing audit viewer UI — initial scope dışı (follow-up).
- Cryptographic hash chain / PGP-signed export — Tier 3 follow-up.
- Cold-storage migration job — Tier 3 follow-up.

## Past Incidents to Avoid
Bu modül greenfield ama benzer/önceki sistemlerden çıkarılan dersler. **Tekrarlanmaması zorunlu**:

- **"Audit DELETE endpoint açıldı"** — Bir önceki proje audit log'a DELETE eklemiş, sonra compliance audit'i fail etti. Bu pack'te DELETE **route bile yok**.
- **Tenant cross-leak** — Bir bug filter compose'da tenant predicate'i atlamış, Tenant A'nın query'si Tenant B'nin event'lerini döndürmüştü. Bu pack: tenant isolation **critical invariant**, integration test zorunlu (Tenant A → 0 results when querying Tenant B id).
- **Sensitive payload export sızması** — Önceki sistemde export endpoint'i redaction'ı uygulamamış, password/token alanları CSV'ye düştü. Bu pack: redaction persistence'ta + export'ta **iki kez** uygulanır (defensive).
- **Meta-audit infinite recursion** — Audit yazımının kendisi audit'lenince worker sonsuz loop'a girdi. Bu pack: `IAuditRecursionGuard` + `IsMetaAudit` flag + `IAuditExcludedRequest` marker üçlüsü zorunlu.
- **`AuditBehavior` her query'ye takıldı** — Tüm read'ler audit'lendi, MongoDB write throughput patladı, latency arttı. Bu pack: **opt-in only**, query'ler hariç, system command'lar hariç. Volume guardrail invariant.
- **Duplicate audit enqueue** — Aynı command pipeline + explicit `IAuditService` çağrısı ile iki kez audit yazıldı. Bu pack: `IdempotencyKey` unique index ile **idempotent enqueue**.
- **Audit failure business command'i gereksiz bozdu** — MongoDB downtime sırasında her business operation 500 döndü. Bu pack: audit enqueue failure default olarak business command'i bozmaz; yalnızca explicit "critical category" listesi block eder (implementation notes'ta tanımlanır).
- **Unauthorized export** — Permission check eksik kalmış, herhangi bir auth'lu kullanıcı tüm audit'i indirebilmiş. Bu pack: `Platform.Audit.Export` permission **ayrı**, sadece read permission yetmiyor.
- **Fake audit timeline** — Audit data yokken UI "1 saat önce X kullanıcısı login oldu" gibi sahte event üretti (genelde dev-seed kaldı). Bu pack: AuditLog **yalnızca persisted `AuditEvent` rows** gösterir; static/seed fake timeline yasak.
- **Retention policy 0 days** — Bir admin yanlışlıkla 0 girdi, ertesi gün tüm audit silindi (archive job retention'ı baz alıyordu). Bu pack: validation `> 0` zorunlu + floor altı yasak + retention değişikliği meta-audit'lenir.

## Acceptance Criteria
- Audit events are immutable: no API, UI, repository, or worker path can delete or mutate `AuditEvent` except GDPR actor redaction of PII fields.
- `AuditEvent` uses `BaseEntity` with server-resolved `TenantId`; client payload cannot provide TenantId.
- `AuditEventRetentionPolicy` uses `GlobalEntity` and contains no TenantId payload.
- `TenantAuditPreference` uses `BaseEntity` and enforces tenant isolation.
- `AuditOperation.Delete` is documented and implemented as business entity deletion/lifecycle deletion, never audit event deletion.
- `AuditBehavior` is opt-in only and captures marked/registered significant commands, not all MediatR requests.
- Queries are excluded from `AuditBehavior` by default; export/cross-tenant audit read/redaction/retention meta-audit use explicit audit writes.
- System/internal commands, outbox worker work, retry/dead-letter operations, health checks, heartbeat jobs, seed/setup commands, and audit append commands are excluded from automatic auditing.
- Duplicate audit prevention uses an idempotency key so one completed significant command does not enqueue duplicate audit records.
- Sensitive fields are redacted before persistence and before export.
- `audit_outbox` collection stores pending audit writes and `AuditOutboxWorker` persists them asynchronously.
- The Platform-local General Audit Trail phase and cross-service producer integration are explicitly
  separated; Platform-local non-blocking enqueue behavior cannot be applied to a required cross-service
  audit intent.
- A required cross-service audited mutation and producer-local technical audit intent persist atomically;
  intent failure rolls back the mutation.
- Cross-service delivery is versioned, asynchronous, durable at-least-once and idempotently consumed without
  an exactly-once claim.
- Cross-service producers do not access Platform audit collections or treat the shared-key internal append
  endpoint as authoritative.
- Cross-service identity is server/transport-bound and payloads are minimal, allowlisted, redacted and
  explicitly bounded; full snapshots and unrestricted dictionaries are absent.
- Technical delivery observations do not become business lifecycle, revision, task, workflow or approval
  state.
- Audit failure does not unnecessarily break business commands; critical failure behavior is explicitly documented and tested.
- Tenant isolation works for list, detail, export, redaction, and retention preference paths.
- Platform admin can query/export cross-tenant audit events only with `Platform.Audit.Read` / `Platform.Audit.Export`.
- Platform admin cross-tenant query/export emits meta-audit.
- Meta-audit uses a recursion guard and does not create infinite audit loops.
- GDPR redaction masks actor PII, preserves event rows, and leaves redaction trace fields.
- Retention policy validates floor/ceiling/default and rejects `0` days.
- Tenant retention, if implemented, cannot go below floor or above ceiling.
- Export supports CSV and JSON and never includes raw sensitive data.
- Unauthorized read/export/redaction/retention update are blocked.
- `DELETE /api/platform/audit/events`, `DELETE /api/platform/audit/events/{id}`, audit-event PUT/PATCH mutation, bulk delete, and hard delete are absent or return 405.
- `/Platform/AuditLog` uses `_LayoutPlatformAdmin`, DataTable v2, advanced filters, detail modal with before/after JSON diff, and export buttons.
- `/Platform/AuditRetention` uses `_LayoutPlatformAdmin` and supports authorized retention policy update.
- Platform localization resources exist for en + tr.
- DataTable verifier passes for the AuditLog page or any intentional custom-read-only exception is documented with reviewer approval.

## Test Expectations
Builds:
- `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug`.
- `dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug`.
- `dotnet build gateway/Diten.ApiGateway/Diten.ApiGateway.csproj -c Debug`.

Static/UI verification:
- Run DataTable verifier for `AuditLog` when implementation exists: `python3 .antigravity/scripts/verify_datatable_page.py . --area Platform --module AuditLog --reference compact`.
- Run RESX checker used by the repo for Platform en/tr resources.
- Verify no browser JS calls `localhost:5057` or service port directly.

Cross-service architecture and contract tests:
- Architecture scan proves cross-service producers have no direct reference to Platform `audit_outbox`,
  `audit_events`, audit repositories or collection-name literals and do not use the shared-key internal
  append endpoint as an authoritative contract.
- Contract tests prove publisher service identity, tenant and actor are bound from authenticated
  server/transport context and reject forged or client-supplied values.
- Payload tests enforce allowlist, redaction and byte/depth/collection-count/string-length limits and reject
  full business/DWS snapshots and unrestricted dictionaries.
- Provider/consumer compatibility vectors cover supported version evolution and unknown-major
  fail-closed/dead-letter behavior. `PpmAuditIntentSubmittedV1` /
  `ppm.audit-intent.submitted.v1` is already allocated for MOD-0117; these vectors must define and verify the
  final Minimal Mutation Audit v1 contract without changing that event identity.

Unit tests:
- `AuditBehavior` opt-in marker/registration captures only marked significant commands.
- `AuditBehavior` excludes unmarked requests and all queries by default.
- Exclusion marker wins over auditable marker and emits a configuration warning.
- System/internal command exclusion covers outbox worker, retry/dead-letter, heartbeat, health, and seed/setup examples.
- Audit idempotency key prevents duplicate enqueue for the same completed command.
- Sensitive field redaction including nested password/token/secret/API key/connection-string fields.
- Redaction registry case-insensitive matching.
- Retention validation: zero, floor, ceiling, default, hot storage days.
- Tenant isolation filter composition.
- Meta-audit recursion guard.
- Outbox enqueue payload and failure handling.
- Export redaction.
- GDPR actor redaction preserves event and masks PII.

Integration tests:
- Marked significant command creates one audit outbox message; unmarked command creates none.
- Cross-service required audit-intent failure rolls back the producer mutation in the same replica-set
  transaction.
- Idempotent consumer tests cover duplicate delivery, concurrent duplicate handling, crash after append
  before acknowledgement and authorized replay.
- Post-commit unavailability tests prove retry, dead-letter, alarm and replay without rolling back the
  committed producer mutation.
- Observability tests distinguish `AuditIntentPersisted` from `AuditEventAcceptedByMOD0021` without creating
  business status or lifecycle fields.
- Export query creates explicit meta-audit without enabling blanket query auditing.
- `GET /api/platform/audit/events` filters by date range, actor, tenant, category, entity type, operation.
- `GET /api/platform/audit/events/{id}` returns detail and 404 for cross-tenant/missing.
- `GET /api/platform/audit/export` returns CSV and JSON.
- `POST /api/platform/audit/redact-actor` masks PII and creates trace/meta-audit.
- `PUT /api/platform/audit/retention` enforces floor/ceiling.
- Unauthorized read/export/redaction/retention update are blocked.
- 405/absence tests for forbidden DELETE, PUT/PATCH mutation, and bulk delete.
- Platform admin cross-tenant query is allowed only with permission and creates meta-audit.

Browser smoke:
- `/Platform/AuditLog` loads under Platform Admin shell.
- Date range, actor, tenant, category, entity type, and operation filters apply to DataTable.
- Detail modal opens and renders before/after JSON diff.
- CSV export button works for authorized actor.
- JSON export button works for authorized actor.
- `/Platform/AuditRetention` loads under Platform Admin shell.
- Retention validation errors display through the premium modal standard.
- Permission-denied states render without leaking audit data.

## Ready-for-dev Checklist
- [x] User approved this pack — `status: ready-for-dev` (2026-05-14).
- [ ] Confirm Tier 2 scope and Tier 3 exclusions remain correct.
- [x] Tenant retention preference scope confirmed: `TenantAuditPreference` backend entity + validation + persistence + platform-admin-controlled update IS in initial scope. Tenant-facing UI and tenant-self-service API are follow-up.
- [ ] Confirm category/tier options source: Domain enum localization vs PSS-011 lookup keys.
- [x] Confirm route ownership: MOD-0021 Ocelot audit routes recorded as integration-agent-owned route work; Phase 5C final hardening did not modify `ocelot.json`.
- [x] PSS owner/Enterprise Architect approved the cross-service producer integration governance baseline:
  producer-local transactional audit intent, asynchronous durable at-least-once delivery, idempotent
  consumer, fail-closed identity binding and bounded payload. This is not runtime evidence.
- [x] `status: ready-for-dev` is explicitly limited to the existing Platform-local General Audit Trail
  phase; the cross-service integration subset remains `PARTIAL` and DCP-006 OD-04 remains open.
- [ ] Exact versioned semantic provider/consumer contract is approved and implemented.
- [ ] Producer-local transactional outbox and real Mongo rollback/crash evidence are proven.
- [ ] Idempotent consumer duplicate-delivery and consumer-crash evidence are proven.
- [ ] Unknown-major-version fail-closed/dead-letter evidence is proven.
- [ ] Publisher service identity, tenant and actor negative tests pass.
- [ ] Payload allowlist, redaction and byte/depth/count/string-limit evidence passes.
- [ ] Dead-letter alarm, authorized replay and acceptance-observability evidence passes.
- [ ] Architecture test proves no direct Platform collection access or authoritative use of the shared-key
  internal append endpoint.
- [ ] Confirm maximum export range/row limit for CSV/JSON.
- [ ] Confirm critical audit categories, if any, that should fail the business command when audit enqueue fails.
- [ ] Confirm redaction policy for actor email/display name/IP/user agent.
- [ ] Confirm Platform Admin navigation placement for AuditLog and AuditRetention.
- [ ] Confirm legacy `MOD-0021-audit-trail-service.md` remains historical and this pack is used for development.

## Implementation Notes
- This module should retrofit pending audit hook follow-ups such as NEW-002 after core audit append/query is available, but those retrofits may be separate follow-up tasks if they touch many modules.
- Use `Response<T>` envelope for all API responses.
- Controller logic must remain thin and delegate to MediatR.
- Do not place MongoDB driver code in Domain/Application.
- Do not create fake timelines from non-audit metadata. AuditLog must show persisted `AuditEvent` records only.
- Store raw payload only after redaction. Export must perform redaction again defensively.
- Implement `AuditBehavior` as opt-in. Do not implement blanket "audit every request" behavior.
- Preferred implementation shape:
  - `IAuditableRequest` or `IAuditableCommand` marker for significant business commands.
  - `IAuditExcludedRequest` marker for infrastructure/system/internal requests.
  - Optional central audit definition registry for operation/category/entity mapping.
  - Exclusion marker takes precedence over auditable marker.
- Do not audit queries by default. `ExportAuditEventsQuery`, cross-tenant audit read, and other compliance-sensitive reads must call `IAuditService` explicitly for meta-audit.
- `EnqueueAuditEventCommand`, `AuditOutboxWorker`, outbox retries, dead-letter handling, heartbeat, health, and seed/setup flows must be excluded from `AuditBehavior`.
- Use an `IAuditRecursionGuard` ambient/request-scoped guard around meta-audit writes. Meta-audit writes must set `IsMetaAudit=true`.
- `AuditBehavior` should avoid auditing failed validation unless explicitly required; if denied/failed security operations are audited, use a separate safe path that does not leak payloads.
- Duplicate enqueue protection should live at the outbox boundary with a deterministic idempotency key. This protects retries and prevents double audit when a command has both pipeline and explicit `IAuditService` instrumentation.
- Retention policy update itself must be audited.
- GDPR redaction event must be audited without recursively redacting its own trace.
- High-frequency volume control beyond explicit exclusion, such as rate limiting, sampling, aggregation, or retry-storm suppression, is a follow-up guardrail and not part of Tier 2 baseline.
- Hot/cold fields prepare the schema for future archive work; no dedicated migration/archive worker in this pack.

### AuditBehavior MediatR pipeline position (deterministic)
Pipeline ordering (registration order in DI) must be:

```
1. LoggingBehavior
2. ValidationBehavior          ← validation failure → AuditBehavior tetiklenmez
3. AuthorizationBehavior       ← denial → security-safe meta-audit, payload yok
4. TransactionBehavior (varsa) ← business commit boundary
5. AuditBehavior               ← burası; opt-in marker + idempotency key check
6. Handler
```

Kurallar:
- **Validation failure → audit YOK.** Failed validation gürültüdür; `AuditBehavior` validation'dan sonra çalışır.
- **Authorization denial → security-safe audit event** yazılır. Payload field'ları **redact/omit** edilir; yalnızca `Outcome=Denied`, `ActorId`, `RequestType`, `Reason` taşınır.
- **Handler runtime exception → `Outcome=Failed` audit**. Exception type adı + safe message taşınır. **Stack trace ve raw payload audit'e yazılmaz.**
- **Handler success → `Outcome=Succeeded` audit** outbox'a enqueue edilir.
- **Transaction behavior yoksa** business commit ile audit outbox enqueue'sunun atomicity'si garanti edilemez. Bu durum **explicit risk** olarak Follow-up Items'a yazılmıştır; implementer mevcut platform pipeline'ında transaction behavior'ı tespit edemezse implementation'ı durdurup karar ister.
- **Aynı unit-of-work içinde outbox yazımı:** Mevcut pipeline'da transaction var ise, business write + outbox enqueue **aynı transaction commit'i** içinde tamamlanır (outbox pattern'in atomicity garantisi).

### Failed command audit outcome matrix
| Pipeline noktası | Davranış | Audit |
|---|---|---|
| Validation failure | `ValidationException` | **Audit YOK** |
| Authorization denial | `ForbiddenException` / policy fail | Audit var, payload redacted; `Outcome=Denied` |
| Handler runtime exception | Beklenmeyen exception | Audit var; `Outcome=Failed`, exception type + safe message, NO stack/payload |
| Handler logical failure (returns `Response<T>.Fail`) | Business rule denied | Audit var; `Outcome=Failed`, business reason kodu, payload redacted |
| Handler success | Normal commit | Audit var; `Outcome=Succeeded` |

### Export limit defaults (proposal)
Açıkça karar verilmiş initial limit'ler:
- **Max export row count:** 50,000 rows.
- **Max export date range:** 365 days (start–end inclusive).
- Limit aşılırsa: HTTP 400 + error code `MaxExportRangeExceeded` (mesaj key'i `AuditLog.Export.MaxRangeError`).
- Async/background large export (>50K) **follow-up** — bu pack'te yok; ileride dedicated `ExportAuditJob` (MOD-0026 sonrası).

### RetentionPolicy default tier handling
- `PlanTierCode = "Default"` özel bir reserved string'tir ve **global fallback policy**'yi tanımlar.
- Plan-specific tier code (`"Bronze"`, `"Silver"`, `"Gold"`, `"Enterprise"`, vb.) varsa **override** eder.
- Policy resolution: önce tenant'ın plan tier'ına bakılır; bulunamazsa `"Default"` policy uygulanır; o da yoksa **400 + ops alert** (seed bug).
- Seed migration mutlaka `"Default"` policy ile başlar — boş policy state yasak.

### Localization key catalog
RESX key prefix:
- AuditLog sayfası: `AuditLog.*`
- AuditRetention sayfası: `AuditRetention.*`

Initial required keys (en + tr eksiksiz):

```
AuditLog.Title
AuditLog.Subtitle
AuditLog.Filter.DateRange
AuditLog.Filter.Actor
AuditLog.Filter.Tenant
AuditLog.Filter.Category
AuditLog.Filter.Operation
AuditLog.Filter.EntityType
AuditLog.Filter.Apply
AuditLog.Filter.Reset
AuditLog.Table.OccurredAt
AuditLog.Table.Actor
AuditLog.Table.Tenant
AuditLog.Table.Category
AuditLog.Table.Operation
AuditLog.Table.EntityType
AuditLog.ExportCsv
AuditLog.ExportJson
AuditLog.Export.MaxRangeError
AuditLog.Detail.Title
AuditLog.Detail.BeforeState
AuditLog.Detail.AfterState
AuditLog.Detail.CorrelationId
AuditLog.Detail.RedactionStatus
AuditLog.Empty
AuditLog.Unauthorized

AuditRetention.Title
AuditRetention.Subtitle
AuditRetention.Category
AuditRetention.PlanTier
AuditRetention.DefaultRetentionDays
AuditRetention.MinimumRetentionDays
AuditRetention.MaximumRetentionDays
AuditRetention.HotStorageDays
AuditRetention.AllowTenantOverride
AuditRetention.FloorError
AuditRetention.CeilingError
AuditRetention.ZeroError
AuditRetention.SaveSuccess
AuditRetention.SaveError
```

Magic string yasaktır — view/JS tarafında string literal text yerine bu key'ler render edilir.

## Follow-up Items
- Tenant-facing audit viewer and tenant permission boundary.
- Tenant-side ERP UI for audit retention preference, if product decides tenants can self-manage it.
- Retrofitting existing Platform modules to emit richer audit payloads after MOD-0021 core is merged.
- Dedicated cold-storage migration/archive job.
- Audit volume guardrails for very high-frequency system events: rate limiting, sampling, aggregation, retry-storm suppression, and operational dashboards.
- Cryptographic hash chain for tamper evidence.
- PGP-signed export.
- Evidence package automation for SOC2/ISO.
- SIEM/event bus streaming integration after MOD-0035 or observability modules mature.
- Admin activity timeline replacement in PSS-009 after real audit feed exists.
- **Pipeline transaction behavior verification:** If the existing Platform MediatR pipeline does not yet expose an explicit `TransactionBehavior`, evaluate whether business write + audit outbox enqueue atomicity is guaranteed by Mongo session/multi-document transactions or must be added before MOD-0021 production rollout. Open follow-up if missing.
- Async / background bulk export job for exports above the 50K row / 365 day threshold (post MOD-0026).
- Tenant-self-service retention preference API (`PUT /api/tenant/audit/retention`) and tenant-facing UI for retention selection.

---

# Amendment 20 — Gate I Multi-Producer Audit Intake

> **Amendment status: READY-FOR-DEV / NON-RUNTIME.** This named slice authorizes only MOD-0021 consumer
> mapping and acceptance-test implementation handoff. It does not change the parent frontmatter
> `status: ready-for-dev`, authorize runtime or production use, or promote any producer pack/runtime slice.

## 1. Amendment Summary

Gate I defines the governance boundary by which MOD-0021 may eventually consume minimal mutation audit
intents from these producer/module pairs:

| Producer service | Producer module |
|---|---|
| `Diten.ManagementGovernanceService` | `MOD-0007` |
| `Diten.FpaService` | `MOD-0136` |
| `Diten.FpaService` | `MOD-0138` |
| `Diten.DecisionIntelligenceService` | `MOD-0072` |

This amendment standardizes only a versioned **Minimal Mutation Audit Profile**. It does not standardize,
name or create a generic business event.

## 2. Authority and Status Boundary

- The parent pack status remains unchanged and describes the already recorded parent scope only.
- This amendment is governance intake, not an implementation pack, runtime approval or production rollout.
- Producer pack amendments own executable event identity, schema version adoption and publication authority.
- MOD-0021 becomes consumer-ready for a producer only after every Gate I readiness item for that producer
  is approved; readiness is evaluated independently per mapping.
- The canonical MOD-0021 identity remains governed by the
  [module ID registry](../../../registries/module-id-registry.md); no new MOD/FU identity is minted here.

## 3. Ownership and Boundaries

- Business event identity remains producer-owned. Event type, event name/routing key and semantic meaning
  are declared by the corresponding producer pack amendment.
- PSS must not invent exact event names on behalf of any producer and must not place producer event DTOs in
  `Diten.Platform.Contracts` merely to complete this intake.
- MOD-0021 owns consumer validation, producer-specific mapping into immutable audit storage, the consumer
  transactional inbox, and the MOD-0021 audit outbox write.
- Shared transport mechanics remain under [MOD-0035](MOD-0035-event-bus-message-queue.md).

## 4. Producer Intake Register

The producer-owned reconciliation checkpoints were reviewed read-only at these exact commits:

| Producer/module | Fixture checkpoint | Exact event type / EventName | Schema identity | EventVersion / ModuleCode | Gate I disposition |
|---|---|---|---|---|---|
| `Diten.ManagementGovernanceService` / `MOD-0007` | `9968ecede48822f95a74461a4959c94b23abbc9b` | `DecisionRegistryAuditIntentSubmittedV1` / `management-governance.decision-registry-audit-intent.submitted.v1` | `urn:diten:management-governance:decision-registry-audit-intent-submitted:v1`; complete identity annotations | JSON integer `1` / `MOD-0007` | Fixture contract pass |
| `Diten.FpaService` / `MOD-0136` | `711962a3fdc1226d947672dc9b48d29296c960a0` | `BudgetingAuditIntentSubmittedV1` / `fpa.budgeting-audit-intent.submitted.v1` | `urn:diten:fpa:budgeting-audit-intent-submitted:v1`; complete identity annotations | JSON integer `1` / `MOD-0136` | Fixture contract pass |
| `Diten.FpaService` / `MOD-0138` | `3df680d6e006bfce19e382253ddd1f2f873c2295` | `ScenarioPlanningAuditIntentSubmittedV1` / `fpa.scenario-planning-audit-intent.submitted.v1` | `urn:diten:fpa:scenario-planning-audit-intent-submitted:v1`; complete identity annotations | JSON integer `1` / `MOD-0138` | Fixture contract pass |
| `Diten.DecisionIntelligenceService` / `MOD-0072` | `b4589139e8c9db544de5b66300640b214db3acf4` | `OutcomeTrackingAuditIntentSubmittedV1` / `decision-intelligence.outcome-tracking-audit-intent.submitted.v1` | `urn:diten:decision-intelligence:outcome-tracking-audit-intent-submitted:v1`; complete identity annotations | JSON integer `1` / `MOD-0072` | Fixture contract pass |

Event type and EventName are producer-owned and are accepted here exactly as supplied. Similar names,
producers, modules or payloads must not be collapsed into one generic event. The `.v1` routing suffix and
transport `EventVersion` both mean the numeric integer `1`; string/decimal `"1.0"`, aliases and normalization
are unsupported. The four fixture checkpoints now use JSON numeric integer `1`; the earlier MOD-0007 and
MOD-0072 `1.0` drift is closed.

## 5. Versioned Minimal Mutation Audit Profile

Each admitted event version maps exactly these canonical payload fields:

| Field | Minimum contract |
|---|---|
| `auditIntentId` | Non-empty immutable identifier; must equal envelope `EventId` |
| `actorId` | Non-empty effective actor identifier from authenticated mutation context |
| `entityType` | Producer allowlist value mapped to a MOD-0021 audit entity type |
| `entityId` | Non-empty producer aggregate/entity identifier |
| `mutation` | Producer allowlist value mapped to a canonical audit operation |
| `occurredAtUtc` | UTC timestamp; must equal envelope occurrence time |

The profile is versioned. A producer-specific breaking payload or semantic change requires a new
producer-owned event version, mapping and compatibility fixture.

## 6. Envelope and Transport Metadata

`TenantId`, `CorrelationId`, `EventId`, `Producer` and security metadata come from the authenticated
envelope/transport path, not the canonical payload. Consumer validation fails closed when payload identity,
time or authenticated metadata disagree. Client-supplied tenant, producer, correlation or security values
cannot override trusted metadata.

## 7. Producer-Specific Allowlist Contract

Every producer/event version must provide a closed allowlist for `entityType` and `mutation`, including the
exact mapping target. Unknown values, unknown properties and unbounded dictionaries/graphs are rejected.
One producer's allowlist never implicitly admits another producer or version. Full entities, before/after
snapshots, secrets, tokens, permission inventories and unrestricted business payloads are forbidden.

## 8. Producer-Specific Mapping Contract

Each mapping records at minimum: producer service, producer module, producer-owned event identity and
version, accepted entity/mutation allowlists, canonical audit category/operation/entity projection,
delegated-actor projection, redaction rules and fixture location. Mapping is explicit code/configuration;
event-name heuristics and a generic catch-all mapper are forbidden.

### Decision Registry — `MOD-0007`

All rows set `SourceService = Diten.ManagementGovernanceService`, `SourceModule = MOD-0007` and
`Category = AuditCategory.Integration`.

| Exact `entityType` | Exact `mutation` | AuditEvent `EntityType` | AuditEvent `Operation` |
|---|---|---|---|
| `DecisionDraft` | `Created` | `DecisionRegistry.DecisionDraft` | `AuditOperation.Create` |
| `DecisionDraft` | `Revised` | `DecisionRegistry.DecisionDraft` | `AuditOperation.Update` |
| `DecisionDraft` | `SoftDeleted` | `DecisionRegistry.DecisionDraft` | `AuditOperation.Delete` |
| `DecisionRecord` | `Published` | `DecisionRegistry.DecisionRecord` | `AuditOperation.Activate` |
| `DecisionRecord` | `Superseded` | `DecisionRegistry.DecisionRecord` | `AuditOperation.LifecycleTransition` |
| `DecisionRecord` | `Withdrawn` | `DecisionRegistry.DecisionRecord` | `AuditOperation.Deactivate` |

### Budgeting — `MOD-0136`

All rows set `SourceService = Diten.FpaService`, `SourceModule = MOD-0136` and
`Category = AuditCategory.System`, preserving the producer checkpoint without adding an enum value.

| Exact `entityType` | Exact allowed `mutation` values | AuditEvent `EntityType` | AuditEvent `Operation` |
|---|---|---|---|
| `Budget` | `created`, `updated`, `archived` | `Budgeting.Budget` | `Create`, `Update`, `Deactivate` respectively |
| `BudgetFiscalCalendar` | `created`, `updated`, `archived` | `Budgeting.BudgetFiscalCalendar` | `Create`, `Update`, `Deactivate` respectively |
| `BudgetVersionDraft` | `created`, `updated`, `abandoned` | `Budgeting.BudgetVersionDraft` | `Create`, `Update`, `Deactivate` respectively |
| `BudgetVersion` | `certified`, `retired`, `superseded` | `Budgeting.BudgetVersion` | `Activate`, `Deactivate`, `Deactivate` respectively |
| `FundingBaselineSelection` | `replaced`, `closed` | `Budgeting.FundingBaselineSelection` | `Update`, `Deactivate` respectively |

### Scenario Planning — `MOD-0138`

All rows set `SourceService = Diten.FpaService`, `SourceModule = MOD-0138` and
`Category = AuditCategory.Integration`.

| Exact `entityType` | Exact allowed `mutation` values | AuditEvent `EntityType` | AuditEvent `Operation` / outcome |
|---|---|---|---|
| `Scenario` | `Created`, `Updated`, `Archived` | `ScenarioPlanning.Scenario` | `Create`, `Update`, `LifecycleTransition` / `Succeeded` |
| `ScenarioVersionDraft` | `Created`, `Updated`, `Abandoned` | `ScenarioPlanning.ScenarioVersionDraft` | `Create`, `Update`, `Delete` / `Succeeded` |
| `ScenarioVersion` | `Published`, `Retired` | `ScenarioPlanning.ScenarioVersion` | `Create`, `LifecycleTransition` / `Succeeded` |
| `ComparatorRun` | `Requested`, `Started`, `Completed`, `Failed` | `ScenarioPlanning.ComparatorRun` | `Create`, `Execute`, `Execute`, `Execute`; only `Failed` maps outcome `Failed` |
| `ScenarioSelection` | `Replaced`, `Closed` | `ScenarioPlanning.ScenarioSelection` | `Update`, `LifecycleTransition` / `Succeeded` |

`Delete` for `ScenarioVersionDraft/Abandoned` describes abandonment of a draft and never deletion of an
AuditEvent.

### Outcome Tracking — `MOD-0072`

All rows set `SourceService = Diten.DecisionIntelligenceService`, `SourceModule = MOD-0072` and
`Category = AuditCategory.Integration`.

| Exact `entityType` | Exact `mutation` | AuditEvent `EntityType` | AuditEvent `Operation` |
|---|---|---|---|
| `Outcome` | `Created` | `OutcomeTracking.Outcome` | `AuditOperation.Create` |
| `Outcome` | `Retired` | `OutcomeTracking.Outcome` | `AuditOperation.Deactivate` |
| `OutcomeVersion` | `Published` | `OutcomeTracking.OutcomeVersion` | `AuditOperation.Activate` |
| `OutcomeMeasurement` | `Appended` | `OutcomeTracking.OutcomeMeasurement` | `AuditOperation.Create` |
| `OutcomeMeasurement` | `Corrected` | `OutcomeTracking.OutcomeMeasurement` | `AuditOperation.Update` |

`DecisionOutcomeLink`, Metric and Scale events are not admitted by v1. Any pair not shown in these four
tables is terminal contract failure and creates no AuditEvent projection.

## 9. Compatibility Fixture Contract

Each producer/event version supplies a producer-owned canonical-byte fixture plus expected envelope metadata
and expected MOD-0021 projection. Contract verification must prove accepted mapping, rejected unknown fields,
unsupported version handling, metadata mismatch handling, delegated provenance mapping and canonical-byte
stability. A mapping without its producer fixture is not executable.

The bilateral fixture artifact names are exact and case-sensitive:

| Module | Canonical payload fixture | Expected projection fixture | Schema fixture | Checksum fixture |
|---|---|---|---|---|
| `MOD-0007` | `decision-registry-audit-intent-submitted-v1.canonical.json` | `decision-registry-audit-intent-submitted-v1.expected-audit-event.json` | `decision-registry-audit-intent-submitted-v1.schema.json` | `decision-registry-audit-intent-submitted-v1.canonical.sha256` |
| `MOD-0136` | `budgeting-audit-intent-submitted-v1.canonical.json` | `budgeting-audit-intent-submitted-v1.expected-audit-event.json` | `budgeting-audit-intent-submitted-v1.schema.json` | `budgeting-audit-intent-submitted-v1.canonical.sha256` |
| `MOD-0138` | `scenario-planning-audit-intent-submitted-v1.canonical.json` | `scenario-planning-audit-intent-submitted-v1.expected-audit-event.json` | `scenario-planning-audit-intent-submitted-v1.schema.json` | `scenario-planning-audit-intent-submitted-v1.canonical.sha256` |
| `MOD-0072` | `outcome-tracking-audit-intent-submitted-v1.canonical.json` | `outcome-tracking-audit-intent-submitted-v1.expected-audit-event.json` | `outcome-tracking-audit-intent-submitted-v1.schema.json` | `outcome-tracking-audit-intent-submitted-v1.canonical.sha256` |

Each producer amendment must assign an exact producer-owned repository path to these basenames; MOD-0021
consumer tests consume the same bytes without copying or regenerating them under a different name. The schema
identity is the ordinal tuple `Producer + ModuleCode + EventName + numeric EventVersion(1) + MinimalMutationAuditV1`;
the schema file must declare those five constants and exactly the six required lower-camel-case properties with
`additionalProperties: false`. Property order in canonical bytes is exactly `auditIntentId`, `actorId`,
`entityType`, `entityId`, `mutation`, `occurredAtUtc`.

The `.canonical.sha256` file contains exactly the lowercase 64-hex SHA-256 digest of the exact UTF-8 bytes of
its sibling `.canonical.json`, followed by one LF and no filename decoration. The consumer must verify the
digest before deserialization and must bind the verified digest, schema identity, envelope identity and
expected projection fixture in one test case. A prose JSON example, computed-at-test fixture, missing digest,
different property order, BOM, CRLF, whitespace normalization or reserialization is not executable evidence.

### Fixture checkpoint evidence and remaining contract gaps

| Module | Canonical bytes | Computed SHA-256 | Checksum record | Schema / projection | Signing vector | Result |
|---|---:|---|---|---|---|---|
| `MOD-0007` | `252` | `0af26a132953b8ac0e364574482fffb04f4f50223a6095685837bac386ab55c4` | PASS: 64 lowercase hex + LF | PASS: complete identity, six-field schema, pair matrix and exact projection | PASS: `28204085cf426d46298b59c5439e41795f9deb914f8898ea7ad87a54ad8d36e9`; delegated/non-delegated and production rejection; no PPM reuse | PASS |
| `MOD-0136` | `254` | `de9534d5b6ce6f7ef7237e6bcf593dfaa460a79b63e6293495d0de25b5225fe8` | PASS: 64 lowercase hex + LF | PASS: common canonical order, complete identity, 14 positive/31 cross-pair negative mappings, 34 contract negatives and exact projection | PASS: `e4896c480405bebf25ba31732a1549136056f8addfc540bf4ac0b28fb9119579`; explicit production rejection; no PPM/MOD-0138 reuse | PASS |
| `MOD-0138` | `247` | `db9385def75d885a581554e10ce38877408ed445ed3c4f62e17283b30830462b` | PASS: 64 lowercase hex + LF | PASS: complete identity, 14 positive/46 cross-pair negative mappings, 25 contract negatives, bound manifest and exact projection | PASS: `05c7cdee1fa8933f00502460178a2b76fa4d68819a5d2e1801c118e0e68b47df`; delegated/non-delegated and production rejection; no PPM/MOD-0136 reuse | PASS |
| `MOD-0072` | `246` | `6e1e750ffddc6f65d45556e703b0aa282b8469dec00bcc516a7a0a5f823cc2a3` | PASS: 64 lowercase hex + LF | PASS: complete identity, 5 positive/10 cross-pair negative mappings, 31 contract negatives and exact projection | PASS: `0336b3630d4fa5e10c01cb7674681014d1a6e0d6df40ff3923b4555a9be3c07f`; delegated/non-delegated, production rejection including active/previous slots; no PPM/FP&A reuse | PASS |

All four canonical blobs are minified UTF-8 without BOM/trailing LF, contain exactly the six profile fields,
match their recorded payload byte length and computed digest, and bind JSON integer `EventVersion = 1` plus
the correct ModuleCode in their envelope/schema/projection/signing evidence. All four producer-specific HMAC
test vectors recompute to their committed expected signature. These cryptographic passes do not waive schema,
checksum-record, projection or delegated-provenance fixture requirements.

MOD-0136 now uses the common canonical order without exception. Its 254 exact bytes, SHA-256 and HMAC above
replace the prior checkpoint evidence; MOD-0021 must neither accept nor document an earlier ordering or
signature as compatible.

All four checkpoints contain delegated positive/negative evidence, explicit non-delegated structural-absence
evidence and complete allowlist/cross-pair coverage. Test signing identities, KeyIds and otherwise-correct
test HMACs are production-rejected. The PSS-owned
[Gate I bilateral consumer acceptance fixture](fixtures/MOD-0021/gate-i/gate-i-audit-intake-bilateral-acceptance-v1.json)
binds the four immutable `checkpoint:path` artifact sets without copying or regenerating producer bytes. It
also closes the consumer-side delegated-profile missing-proof and test identity/KeyId active/previous slot
rejection cross-product. These are governance expectations; live validator and credential evidence remains a
runtime gate.

## 10. Consumer Transaction Boundary

For an accepted message, the Platform consumer writes the transactional inbox record and MOD-0021 audit
outbox in one local transaction. A commit may contain zero or one new audit outbox item for the idempotency
key; it must never expose an accepted inbox record without the corresponding durable audit outbox result.
Producer mutation transactions never span into Platform.

## 11. Idempotency and Payload Conflict Contract

- Idempotency key: `ConsumerName + EventId`.
- Same `EventId` and same canonical payload is a duplicate no-op; no second audit outbox/event is created.
- Same `EventId` and different canonical payload is a terminal conflict; the original accepted projection is
  never overwritten.
- Canonical payload comparison uses the exact validated canonical bytes (or their collision-resistant stored
  digest plus required verification), not a lossy reserialization.

## 12. Mapping, Idempotency and Error Matrix

| Condition | Mapping/outbox effect | Disposition | Retry/replay |
|---|---|---|---|
| Supported version, valid allowlist/security, unseen key | Write inbox + one MOD-0021 audit outbox transactionally | Accepted | Normal delivery |
| Same key, same canonical payload | No new mapping side effect or outbox item | Duplicate no-op | Safe no-op |
| Same key, different canonical payload | Preserve original; write no new audit outbox item | Terminal conflict | No automatic retry |
| Unsupported version | No audit projection | Terminal | Only after compatible consumer/disposition |
| Malformed canonical payload or allowlist/mapping failure | No audit projection | Terminal | No automatic retry |
| Authentication, authorization, signature or trusted-metadata failure | No audit projection | Terminal security failure | Unauthorized replay forbidden |
| Transient broker, MongoDB or other dependency failure | No partial accepted state | Transient | Retry, then DLQ + alarm; authorized replay |

Terminal records and diagnostics contain bounded redacted reason metadata only; payload logging is forbidden.

## 13. Retry, DLQ, Alarm and Replay

Only transient dependency failures follow the MOD-0035 retry schedule and, when exhausted, DLQ plus alarm.
Authorized replay preserves `EventId`, producer-owned event identity/version, trusted envelope metadata and
identical canonical payload. Replay re-enters the same consumer and idempotency path. This amendment creates
no public replay endpoint or UI and grants no replay permission.

## 14. Security and Isolation

- Producer service identity is authenticated and must exactly match the mapping registration.
- `TenantId` is required and trusted only from authenticated transport context; cross-tenant mismatch fails
  closed without revealing record existence.
- Security failure is terminal and cannot be converted into a transient retry to bypass controls.
- Payload and security material are not logged; diagnostics use allowlisted metadata only.
- Producer credentials and delegation evidence are validated before an audit projection is accepted.

## 15. Delegated Actor Provenance Representation

`actorId` represents the effective actor who performed the mutation. When the authenticated security context
shows delegation/impersonation, MOD-0021 also projects trusted envelope security metadata into an immutable,
redacted provenance block: effective actor id, delegating actor id, delegation/grant or session reference,
authentication method and verification outcome as available under the producer mapping. The block must
distinguish **acted by** from **acted on behalf of** without copying credentials, tokens or unrestricted
claims. A delegated event missing mapping-required provenance is a terminal security failure. This
representation does not add fields to the six-field Minimal Mutation Audit Profile.

The exact AuditEvent mapping is: `ActorId = payload.actorId`; `Metadata.DelegatedActorId` equals the verified
delegated/effective actor and must equal `ActorId`; `Metadata.DelegatingPrincipalId` records the authenticated
principal on whose authority the delegated actor operated; `Metadata.DelegationId`,
`Metadata.DelegatedOperationId`, `Metadata.DelegatedPermission`, `Metadata.DelegationVerifiedAtUtc` and
`Metadata.ExecutingService` come only from the verified transport security profile. Non-delegated calls omit
the five `Delegated*`/delegating fields rather than writing invented null identities, while
`ExecutingService` remains the authenticated producer. For asynchronous ScenarioPlanning comparator work,
`ActorId` remains the originating effective actor and `ExecutingService` identifies the worker/service.
Mismatch, missing mandatory provenance, unverified delegation, tenant drift or actor substitution is terminal.

## 16. Producer Runtime Decoupling

The producer persists its own mutation and producer-local audit intent/outbox transactionally, then publishes
asynchronously through the approved eventing seam. The business mutation has no synchronous MOD-0021,
Platform API, Platform database or Platform availability dependency. Platform/broker unavailability after
producer commit cannot roll back the producer mutation.

## 17. Storage Access Prohibitions

No producer may read or write Platform `audit_events`, `audit_outbox`, consumer inbox or any other MOD-0021
collection directly. Shared database credentials, collection-level shortcuts and synchronous internal append
calls are not substitutes for the producer-owned event plus asynchronous consumer boundary.

## 18. PPM Regression Boundary

The existing `PpmAuditIntentSubmittedV1` / `ppm.audit-intent.submitted.v1` mapping and its final six-field
Minimal Mutation Audit v1 contract remain unchanged. Gate I does not rename, generalize, re-version or move
ownership of the PPM event, and does not relax its canonical-byte, signing, retry, replay, allowlist or
idempotency rules. Regression authority remains the
[MOD-0117 PPM pack](../../portfolio-delivery/module-packs/MOD-0117-project-portfolio-management.md) together
with this pack's existing PPM baseline and the [MOD-0035 PPM transport slice](MOD-0035-event-bus-message-queue.md).
The Gate I fixture basenames, schema tuple, categories, entity names and operation mappings above cannot be
used to rename, regenerate or reinterpret the existing PPM canonical payload/signature/expected-projection
fixtures. The complete pre-existing PPM consumer/signing test set must pass byte-for-byte before and after
each new mapping registration.

## 19. Acceptance and Verification Gates

- [x] All four producer checkpoints were reviewed read-only and their producer-owned exact event identities
      were reconciled without PSS generating or generalizing an event.
- [x] All four event-specific entity/mutation allowlists, SourceService/SourceModule, AuditCategory,
      AuditEvent entity/operation projection and delegated-actor mapping are closed in this amendment.
- [x] Exact case-sensitive fixture basenames, schema identity tuple, canonical property order and SHA-256 file
      format are closed.
- [x] All four fixture checkpoints use JSON numeric integer EventVersion `1`; the earlier MOD-0007/MOD-0072
      `1.0` drift is closed.
- [x] All four producers commit canonical/projection/schema/checksum and producer-specific signing evidence at
      a producer-owned path; canonical byte lengths and computed SHA-256 values match.
- [x] All checksum records comply with the exact lowercase 64-hex + one-LF/no-filename contract.
- [x] All schemas encode the complete producer/module/event/version/profile identity tuple and exact valid
      entity/mutation pair matrix; MOD-0136 uses the common canonical property order without exception.
- [x] Every expected projection uses exact MOD-0021 `SourceService` and `SourceModule` field names and values.
- [x] Every producer supplies delegated positive/negative provenance fixtures and positive/negative fixtures
      covering its complete entity/mutation allowlist matrix.
- [x] Every producer supplies explicit delegated and non-delegated positive provenance coverage, including
      structural absence of delegation metadata for non-delegated delivery.
- [x] Every test-only signing vector and bilateral consumer matrix prove production rejection of test signing
      identity, test KeyId and otherwise-correct test signature, including active/previous slot attempts.
- [x] All four producer compatibility fixtures pass the MOD-0021 governance consumer acceptance matrix.
- [ ] Transaction test proves inbox + MOD-0021 audit outbox atomicity.
- [ ] Duplicate/conflict tests prove the complete `ConsumerName + EventId` matrix.
- [ ] Unsupported version, malformed canonical payload and security failures prove terminal disposition.
- [ ] Transient dependency tests prove retry, DLQ, alarm and authorized replay behavior.
- [ ] Delegated-actor fixtures prove effective/delegating actor provenance without credential/claim leakage.
- [ ] Producer tests prove no synchronous MOD-0021 dependency and no direct Platform collection access.
- [ ] PPM regression fixtures pass unchanged.
- [x] `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0021 --name "Audit Trail Service"`
      passes, all relative links resolve, and `git diff --check` passes.

Unchecked runtime-evidence boxes block runtime/production promotion and completion, but do not reopen the
closed pre-development fixture/design gate or revoke ready-for-dev implementation handoff.

## 20. Implementation Notes and Follow-up Items

- Producer amendments must be authored in their owning domain/module packs before any event name, DTO,
  mapping implementation or fixture is added to PSS runtime scope.
- An eventual implementation authorization must name exact repo paths and tests; this governance amendment
  alone authorizes no service, frontend, Gateway, schema or deployment change.
- Track approval independently for `MOD-0007`, `MOD-0136`, `MOD-0138` and `MOD-0072`; one completed mapping
  does not promote the remaining mappings or the amendment as a whole.
- Reconciliation result is **FIXTURE PASS**. The four immutable producer fixture sets and the MOD-0021-owned
  bilateral consumer acceptance fixture close the Gate I pre-development contract/design gate.
- Amendment 20 is **READY-FOR-DEV / NON-RUNTIME**. This is an implementation handoff for the exact MOD-0021
  consumer mappings and acceptance tests only; it does not change the parent status, promote producer packs,
  authorize event publication, provision credentials or grant runtime/production authority.
- Production credentials, key provisioning/rotation, live broker delivery, transactional runtime evidence,
  alarms and authorized replay evidence remain post-development gates; they do not revoke this design-only
  ready-for-dev handoff and are mandatory before runtime/production promotion.
- Revisit profile versioning only through an explicit compatibility decision; do not evolve the six-field
  minimum implicitly.
- Parent status changes, if ever warranted, require a separate explicit governance decision.
