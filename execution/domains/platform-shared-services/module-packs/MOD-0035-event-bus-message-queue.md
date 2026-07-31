---
id: MOD-0035
name: Event Bus / Message Queue
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: BaseEntity
status: partial
owner: platform-shared-services
branch: feature/pss/mod-0035-event-bus
started: 2026-05-14
target: 2026-05-30
form_field_count: 0
---

# MOD-0035 — Event Bus / Message Queue

## 1. Module Summary
- **Purpose:** Platform & Shared Services domain owns the central event bus capability for ERP-vNext. Platform modules publish domain/integration events through a shared eventing standard; other ERP domains consume/produce through the same standard and must not fork their own broker infrastructure.
- **Target technology:** MassTransit + RabbitMQ.
- **Development default:** In-memory bus.
- **Integration-test default:** External/local RabbitMQ using configuration/environment variables.
- **Testcontainers/Docker decision:** Testcontainers is not used in this project; Docker is not required and not expected for MOD-0035 validation.
- **Production default:** RabbitMQ via MassTransit.
- **Routing key / EventName format:** dot-separated lowercase segments followed by `.v{positive-version}`; each normal segment may use internal lowercase kebab-case.
- **MVP outbox decision:** Custom MongoDB `outbox_events` + `OutboxPublisherWorker` is the single source for outbound publish state.
- **First implementation golden flow:** A test producer or seed command writes `tenant.activated.v1` to outbox, `OutboxPublisherWorker` publishes it, a test consumer processes it once, duplicate delivery is skipped, and correlation id is preserved.
- **UI:** None in this pack. Event topology, DLQ viewer, replay screens, or Event Catalog UI require separate module packs.

## 2. Ownership and Boundaries
### In-scope
- Central eventing infrastructure contracts and publish/consume standards.
- `IEventBus.PublishAsync<T>(T @event, CancellationToken ct)` publishing contract.
- `IEventHandler<T>` consumer handler contract.
- Event envelope metadata, correlation, causation, versioning, and routing-key rules.
- MongoDB outbox collection: `outbox_events`.
- MongoDB inbox/consumed-event collection for idempotency.
- Outbox publisher worker.
- MassTransit + RabbitMQ adapter.
- In-memory adapter for development/unit tests.
- Custom MongoDB outbox/inbox standard. The MVP does not use MassTransit native outbox/inbox.
- RabbitMQ retry/dead-letter behavior.
- Consumer registration standard and naming convention.
- Audit/observability event names and minimum structured fields.

### Out-of-scope
- Frontend UI.
- Gateway/Ocelot route changes.
- MOD-0009 Tenant Lifecycle event emit implementation.
- MassTransit native outbox/inbox implementation. It may be evaluated later through a separate technical spike.
- Notification, audit, and scheduler module implementation.
- Event contract creation/editing from UI.
- Event taxonomy UI.
- Schema registry UI.
- Webhook delivery.
- External provider console cloning.

### Ownership rule
- Event Bus infrastructure owns publish/consume mechanics, delivery standards, retries, outbox/inbox behavior, and broker adapters.
- Event Bus infrastructure does **not** own business event contracts.
- Platform-owned event contracts are owned by Platform & Shared Services.
- ERP domain event contracts are owned by their own domain contract packages.
- No service/domain may create a parallel RabbitMQ abstraction without an approved module pack.
- Public publish endpoints are forbidden. Publishing is an internal application capability only.

## 3. Owned Objects
### Eventing infrastructure contracts
- `IDomainEvent` (only if retained for platform internal envelope semantics; must not be confused with aggregate-local domain events)
- `IIntegrationEvent` / `IInternalEvent` (recommended marker for cross-service payload contracts)
- `IEventBus`
- `IEventHandler<T>`
- `EventEnvelope<TPayload>`
- `EventMetadata`
- `EventName`
- `EventPublishOptions`

### Naming clarification
- `IDomainEvent` can mean aggregate-local domain event in DDD terminology. If retained in this module, it must be documented as an eventing-envelope marker only and must not blur aggregate-local domain events with cross-service integration/internal events.
- Cross-service payload contracts should prefer `IIntegrationEvent` or `IInternalEvent`.
- Platform aggregate-local domain events may exist separately inside `Diten.Platform.Domain`; they are not the same thing as broker-published integration/internal events.

### Persistence records
- `OutboxEvent`
- `InboxEvent` / `ConsumedEvent`
- `DeadLetterEvent` metadata mirror, if implementation chooses an application-level DLQ collection in addition to broker DLQ.

### Adapters, workers, and stores
- `MassTransitRabbitMqEventBus`
- `InMemoryEventBus`
- `OutboxPublisherWorker`
- `IOutboxEventRepository`
- `IConsumedEventRepository`
- `ConsumedEventStore`

### Initial event names
- `tenant.created.v1`
- `tenant.activated.v1`
- `tenant.suspended.v1`
- `tenant.reactivated.v1`
- `tenant.cancelled.v1`
- `tenant.provisioning.started.v1`
- `tenant.provisioning.failed.v1`

## 4. Entity Fields
### EventEnvelope
| Field | Type | Required | Rule |
|---|---|---|---|
| EventId | Guid | Yes | Generated once per envelope; idempotency key. |
| EventName | string | Yes | Dot-separated lowercase segments followed by `.v{positive-version}`; examples `tenant.activated.v1` and `ppm.audit-intent.submitted.v1`. |
| EventVersion | int | Yes | Numeric contract version; must match the `.v{version}` suffix in `EventName`. Starts at `1`; do not name this field `Version`. |
| CorrelationId | Guid | Yes | Propagates request/job/event chain. |
| CausationId | Guid? | No | Previous event/message id when this event is caused by another event. |
| OccurredAtUtc | DateTimeOffset | Yes | UTC only. |
| TenantId | Guid? | No | Nullable because platform-wide events may not be tenant-owned. |
| Producer | string | Yes | Service/module name, e.g. `Diten.Platform`. |
| Payload | object/document | Yes | Event-specific contract fields only; never full entity graphs. |

### OutboxEvent
| Field | Type | Required | Rule |
|---|---|---|---|
| Id | Guid | Yes | Storage identity. |
| EventId | Guid | Yes | Unique index. |
| EventName | string | Yes | Indexed; routing key with version suffix. |
| EventVersion | int | Yes | Required for contract compatibility and must match EventName suffix. |
| CorrelationId | Guid | Yes | Indexed for audit/trace. |
| CausationId | Guid? | No | Propagated from command/event context when available. |
| TenantId | Guid? | No | Nullable. |
| Producer | string | Yes | Producer service/module. |
| Payload | BsonDocument/string | Yes | Serialized envelope payload; not logged. |
| Status | enum | Yes | `Pending`, `Publishing`, `Published`, `Failed`, `DeadLettered`. |
| AttemptCount | int | Yes | Starts at `0`; incremented by publisher worker. |
| NextAttemptAtUtc | DateTimeOffset? | No | Exponential backoff scheduling. |
| LastError | string? | No | Max 4000 chars; sensitive data redacted. |
| CreatedAt | DateTime | Yes | BaseEntity/system timestamp. |
| UpdatedAt | DateTime? | No | BaseEntity/system timestamp. |

### ConsumedEvent
| Field | Type | Required | Rule |
|---|---|---|---|
| Id | Guid | Yes | Storage identity. |
| EventId | Guid | Yes | Unique with `ConsumerName`. |
| EventName | string | Yes | Routing key with version suffix. |
| EventVersion | int | Yes | Numeric version. |
| ConsumerName | string | Yes | Consumer class/service name. |
| ConsumedAtUtc | DateTimeOffset? | No | UTC timestamp set only after successful handler completion. |
| CorrelationId | Guid | Yes | Traceability. |
| Status | enum | Yes | `Started`, `Consumed`, `SkippedDuplicate`, `Failed`. |
| AttemptCount | int | Yes | Consumer-side retry visibility. |
| LastError | string? | No | Max 4000 chars; sensitive data redacted. |

## 5. Repo Scope
- `execution/domains/platform-shared-services/module-packs/MOD-0035-event-bus-message-queue.md`
- `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.Eventing/**` (new recommended infrastructure contract package)
- `services/Diten.Platform.Contracts/Events/**` (new recommended platform-owned event contract package/folder)
- `services/Diten.Platform.Common/**` only for compatibility or migration from existing shared event/outbox code.
- `services/Diten.Platform/**`
- `services/Diten.AuthService/**` only if an approved implementation task requires producer/consumer integration.
- `docs/platform/master-plan.md` only after explicit user approval for decision/status updates.

## 6. Protected Paths
- `.antigravity/**`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- `gateway/Diten.ApiGateway/**/ocelot.json`
- `services/Diten.DevEnablementService/**`
- `services/Diten.EnterpriseStrategyService/**`
- `services/Diten.MdmService/**` unless a separate domain module pack is approved.
- Frontend views/controllers/assets unless a separate ops viewer pack is approved.

## 7. Dependencies
- **NEW-001 Secrets Management:** RabbitMQ connection/config must not be hardcoded. Until NEW-001 is implemented, appsettings + environment variables are the temporary configuration path.
- **MOD-0009 Tenant Registry Lifecycle Events:** First producer/consumer validation scenario after event bus implementation.
- **MOD-0018 RBAC / Entitlement Enforcement:** Event-driven invalidation consumer candidate.
- **MOD-0021 General Audit Trail:** Correlation chain and audit-worthy event stream integration candidate.
- **MOD-0026 Background Job Scheduler:** Worker hosting/retry coordination candidate; not required for MVP event bus worker if hosted service is used.
- **MOD-0027 Notification / Email Service:** Future event-driven dispatch consumer.
- **MOD-0038 Event Taxonomy / Naming:** Future event catalog/naming governance.
- **MOD-0039 Schema Compatibility Governance:** Future schema versioning and breaking-change checks.
- **MOD-0041 Logging / Monitoring:** Correlation-aware structured logs, health checks, and metrics.

## 8. Runtime Constraints
- Domain-config currently states: **Event Bus: In-process MediatR lightweight internal seam; cross-service broker deferred.**
- This pack is the module-level decision override: **MassTransit + RabbitMQ is the target central event bus for MOD-0035.**
- Production runtime uses RabbitMQ through MassTransit.
- Development runtime defaults to in-memory bus.
- Integration tests default to external/local RabbitMQ using configuration/environment variables.
- Testcontainers is not used in this project; Docker is not a required or expected validation dependency.
- At-least-once delivery is accepted. Consumers must be idempotent.
- HTTP requests and command handlers must never publish directly to RabbitMQ.
- RabbitMQ publish is performed only by `OutboxPublisherWorker`.
- The single outbound publish source of truth is the custom MongoDB `outbox_events` collection.
- MassTransit native outbox/inbox is not used in this MVP.
- Any decision to evaluate MassTransit native outbox/inbox requires a separate technical spike and must not silently replace the custom MongoDB outbox in this pack.
- Event handlers must not synchronously wait for other event handler results.
- Event payloads must not contain full entity snapshots.
- `EventVersion` remains in the envelope and version is also visible in `EventName` / routing key.
- Breaking changes create a new routing key, e.g. `tenant.activated.v2`; existing `*.v1` consumers must keep working.
- RabbitMQ credentials, host, vhost, username, password, TLS flags, retry values, and DLQ retention must be configuration-driven and redacted in logs.
- `entity_base: BaseEntity` is used for platform persistence records such as outbox/inbox rows. `TenantId` is nullable because events may be platform-wide or tenant-related.

## 9. Layout & Shell Contract
- `shell: none`
- No Razor layout is required.
- No frontend route is created by this module pack.
- If an operations UI is later needed, it must be introduced through a separate platform-admin module pack using `Layout = "_LayoutPlatformAdmin"` explicitly.

## 10. Backend File Convention
This is a backend/infrastructure module, not a CRUD DataTable module. `golden_reference: none` is intentional.

### Contract ownership and location
- Eventing infrastructure contracts:
  - Recommended package: `Diten.BuildingBlocks.Eventing`
  - Recommended repo path: `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.Eventing/`
  - Owns: `IIntegrationEvent` / `IInternalEvent`, optional envelope-scoped `IDomainEvent`, `IEventBus`, `IEventHandler<T>`, `EventEnvelope<TPayload>`, `EventMetadata`, event naming validation, publish/consume abstractions.
  - `IDomainEvent` must be documented carefully if retained because aggregate-local domain events may use the same term elsewhere.
- Platform-owned event contracts:
  - Recommended package/folder: `Diten.Platform.Contracts/Events`
  - Recommended repo path: `services/Diten.Platform.Contracts/Events/`
  - Owns: `TenantActivatedV1`, `TenantCreatedV1`, and other Platform event payload contracts.
- ERP domain event contracts:
  - Sales: `Diten.Sales.Contracts/Events`
  - Inventory: `Diten.Inventory.Contracts/Events`
  - Finance: `Diten.Finance.Contracts/Events`
  - Other domains follow the same pattern.
- Event Bus infrastructure must not own business event payload contracts.

### Expected implementation shape
- `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.Eventing/**` for eventing abstractions, envelope, naming validation, and shared options.
- `services/Diten.Platform.Contracts/Events/**` for Platform-owned event payload contracts.
- `services/Diten.Platform/src/Diten.Platform.Application/**` for outbox orchestration contracts and application-level policies.
- `services/Diten.Platform/src/Diten.Platform.Domain/**` for Platform aggregate domain events, when needed.
- `services/Diten.Platform/src/Diten.Platform.Persistence/**` for MongoDB outbox/inbox repositories and indexes.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/**` for MassTransit/RabbitMQ adapter and in-memory adapter registrations, if this project layer exists in the current service structure.
- `services/Diten.Platform/src/Diten.Platform.API/**` only for DI registration, health checks, and hosted worker wiring if needed.

### Naming expectations
- Interface: `IEventBus`, `IEventHandler<T>`, optional envelope-scoped `IDomainEvent`
- Cross-service marker: `IIntegrationEvent` or `IInternalEvent`
- Envelope/model: `EventEnvelope<TPayload>`, `EventMetadata`, `OutboxEvent`, `ConsumedEvent`
- Adapter: `MassTransitRabbitMqEventBus`, `InMemoryEventBus`
- Worker: `OutboxPublisherWorker`
- Repository abstractions: `IOutboxEventRepository`, `IConsumedEventRepository`
- Consumer classes: `{EventPayloadName}Consumer`, e.g. `TenantActivatedV1Consumer`, `SubscriptionChangedV1Consumer`

## 11. Frontend File Contract
- No frontend files are in scope.
- No DataTable v2 contract applies in this pack.
- `golden_reference: none` because this is not a UI CRUD module.
- MVP Event Catalog UI does not exist.
- Event contracts are not created or edited from UI.
- Future Event Catalog may be read-only or governance-oriented.
- Future catalog data may be generated from code contract metadata, attributes, or a build-time manifest.
- UI must not add/remove payload fields.

## 12. Validation Rules
| Field | Required | Format/Rule | DB-level | Pre-check |
|---|---|---|---|---|
| EventId | Yes | Guid not empty | Unique index in `outbox_events`; unique composite with `ConsumerName` in consumed records | Reject duplicate outbox insert unless retrying same message intentionally. |
| EventName | Yes | Dot-separated lowercase segments, optional segment-internal lowercase kebab-case, and `.v{positive-version}` suffix | Indexed | Validate against regex and ensure suffix matches `EventVersion`. |
| EventVersion | Yes | Integer >= 1 | Indexed with `EventName` | Reject zero/negative values; must match `.v{version}` suffix. |
| CorrelationId | Yes | Guid not empty | Indexed | Generate from request/job context when missing. |
| CausationId | No | Guid when present | Indexed optional | Propagate from inbound envelope when present. |
| OccurredAtUtc | Yes | UTC timestamp | Indexed | Reject local/unspecified time. |
| TenantId | No | Guid when present | Indexed optional | Required only for tenant-related event contracts. |
| Producer | Yes | Max 128, service/module identifier | Indexed | Must be known service/module name. |
| ConsumerName | Consumer side | Max 200, class/service name | Unique with `EventId` | Must be stable across deployments. |
| Payload | Yes | Serialized event-specific contract, not full entity graph | — | Contract test verifies no full entity payload anti-pattern. |
| Status | Yes | Known enum value | Indexed | Only valid state transitions allowed. |
| AttemptCount | Yes | >= 0 | — | Increment only by publisher/consumer retry logic. |
| LastError | No | Max 4000 chars; sensitive data redacted | — | Never include payload or secrets. |

## 13. Failure Path to Verify
- **Duplicate EventId**
  - Expected: inbox detects `ConsumerName + EventId`; business side effect does not run again; status is `SkippedDuplicate`.
- **Missing EventName**
  - Expected: publish is rejected before outbox persistence; validation error is logged with correlation id.
- **Invalid EventName**
  - Expected: event name without `.v{version}` suffix or with non-matching `EventVersion` is rejected; no outbox row is created.
- **Invalid EventVersion**
  - Expected: publish/consume is rejected; no message is sent to RabbitMQ.
- **RabbitMQ unavailable**
  - Expected: business transaction is not rolled back after the outbox row is committed; outbox remains `Pending` or moves to `Failed` with retry metadata.
- **Consumer failure**
  - Expected: retry policy applies; after 5 total attempts the message transitions to dead-letter behavior
    and raises an alarm.
- **Unauthorized direct API access**
  - Expected: no public publish endpoint exists; event publishing is service/internal code path only.
- **Full entity payload**
  - Expected: contract test rejects the event; payload must be reduced to ID + required primitive/value-object fields.
- **CorrelationId missing from ambient context**
  - Expected: publisher generates a new correlation id and stores it in envelope, outbox, logs, and message headers.

## 14. Authorization Convention
- This module does not expose user-facing CRUD endpoints.
- Event publishing is an internal application/service capability, not a public API.
- No public publish endpoint is allowed.
- Publish can only be invoked through DI from application services, command handlers, or internal workers.
- No frontend permission is required in this pack.
- If admin-only event diagnostics are added later, permissions must use Platform format:
  - `Platform.EventBus.Read`
  - `Platform.EventBus.Replay`
  - `Platform.EventBus.DeadLetter.Read`
- Any future diagnostics controller must use platform actor authorization and must redact sensitive payload fields.
- Future ops replay endpoints require a separate module pack and `Platform.EventBus.Replay`.
- If payload replay is ever added, sensitive field redaction is mandatory before display, logs, or replay metadata are persisted.

## 15. Gateway / API Routing Decision
- Karar: Gateway değişikliği bu pack için **gereksiz**.
- Frontend Gateway 5000 üzerinden çağrı yapma kuralı korunur, fakat bu module pack frontend/API route üretmez.
- `gateway/Diten.ApiGateway/**/ocelot.json` protected path olarak kalır.
- Event bus internal service infrastructure olduğu için Ocelot route eklenmez.
- Future ops UI/API gerekirse ayrı module pack + integration-agent task gerekir.

## 16. Acceptance Criteria
- [ ] `Diten.BuildingBlocks.Eventing` package/project exists or is wired through the approved equivalent path.
- [ ] Platform-owned event contracts are placed under `Diten.Platform.Contracts/Events` or the approved equivalent path.
- [ ] `IEventBus.PublishAsync<T>(T @event, CancellationToken ct)` contract is defined in the eventing infrastructure package.
- [ ] `IEventHandler<T>` consumer contract is defined and documented.
- [ ] Cross-service payload contracts use `IIntegrationEvent` or `IInternalEvent`, or `IDomainEvent` is explicitly documented as envelope-only and not aggregate-local.
- [ ] `IDomainEvent` and `EventEnvelope<TPayload>` include `EventId`, `EventName`, `EventVersion`, `CorrelationId`, `CausationId`, `OccurredAtUtc`, nullable `TenantId`, `Producer`, and `Payload`.
- [ ] Event naming uses dot-separated lowercase segments, permits only segment-internal lowercase kebab-case, and ends with `.v{positive-version}`.
- [ ] Breaking changes create new event names such as `tenant.activated.v2`; existing v1 consumers remain compatible.
- [ ] Event payload guidance explicitly forbids full entity payloads.
- [ ] Business aggregate save and `OutboxEvent` insert happen inside the same logical transaction/unit-of-work.
- [ ] HTTP request handlers and command handlers do not publish directly to RabbitMQ.
- [ ] RabbitMQ publish is performed only by `OutboxPublisherWorker`.
- [ ] Custom MongoDB `outbox_events` is the single outbound publish source of truth.
- [ ] MassTransit native outbox/inbox is not used in MVP implementation.
- [ ] A test producer or seed command writes `tenant.activated.v1` event to `outbox_events`.
- [ ] Outbox worker publishes `tenant.activated.v1` to RabbitMQ via MassTransit.
- [ ] A registered consumer handles `tenant.activated.v1` exactly once for a given `ConsumerName + EventId`.
- [ ] Duplicate delivery does not create repeated business side effects.
- [ ] RabbitMQ unavailable scenario moves outbox to pending/failed retry state without rolling back the already committed business transaction.
- [ ] Invalid `EventName` or `EventVersion` is rejected before publish.
- [ ] CorrelationId is preserved across publish-consume chain.
- [ ] Full entity payload is rejected by contract test.
- [ ] Payload DTO contract tests reject `BaseEntity`, navigation properties, large collections, binary/blob fields, password/token/secret fields, and aggregate graphs.
- [ ] No public publish endpoint exists.
- [ ] Retry defaults are configured: 5 total attempts (initial attempt included), exponential backoff with jitter, initial delay 10 seconds, max delay 5 minutes.
- [ ] Dead-letter transition happens after max retry; DLQ retention default is 30 days.
- [ ] Logs never include payload; only approved metadata fields are logged.

## 17. Test Expectations
- Build:
  - `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug`
  - `dotnet build services/Diten.Platform.Common/src/Diten.Platform.Common/Diten.Platform.Common.csproj -c Debug`
  - `dotnet build services/Diten.Building.Blocks/src/Diten.BuildingBlocks.Eventing/Diten.BuildingBlocks.Eventing.csproj -c Debug` after the package exists.
- Unit tests:
  - EventEnvelope metadata creation includes all required fields.
  - EventName regex accepts `tenant.activated.v1` and rejects missing/invalid version suffix.
  - EventName version suffix must match `EventVersion`.
  - `PublishAsync` creates an outbox record and does not call RabbitMQ directly.
  - Consumer idempotency prevents duplicate side effects for the same `EventId + ConsumerName`.
  - Full entity payload anti-pattern is covered by contract test.
  - Payload DTO anti-pattern tests reject `BaseEntity`, navigation properties, large collections, binary/blob fields, password/token/secret fields, and aggregate graphs by reflection or fixture-based checks.
  - LastError truncates to 4000 chars and redacts sensitive values.
- Integration tests:
  - In-memory bus publish-consume flow succeeds.
  - Test producer or seed command writes `tenant.activated.v1` without requiring MOD-0009 implementation.
  - External/local RabbitMQ publish-consume flow succeeds through MassTransit when `Eventing__RabbitMq__IntegrationTestsEnabled=true`.
  - RabbitMQ integration test is skipped with a clear reason when `Eventing__RabbitMq__IntegrationTestsEnabled` is not `true`.
  - RabbitMQ unavailable scenario keeps outbox retry state.
  - Outbox worker publishes pending event when RabbitMQ becomes available.
  - Consumer failure uses 5 total attempts and transitions to DLQ plus alarm after the fifth failed attempt.
- Contract tests:
  - `EventName` and `EventVersion` are mandatory.
  - `CorrelationId` is propagated from publish to consume.
  - Payload contains only event contract fields, not aggregate/entity graphs.
  - `tenant.activated.v1` and future `tenant.activated.v2` are independent contracts/routing keys.

## 18. Ready-for-dev Checklist
- [x] Branch set to `feature/pss/mod-0035-event-bus`.
- [x] Dev default set to in-memory bus.
- [x] Integration default set to external/local RabbitMQ using configuration/environment variables.
- [x] Testcontainers/Docker removed as required validation path.
- [x] Production default set to RabbitMQ via MassTransit.
- [x] Contract location set to `Diten.BuildingBlocks.Eventing` + `Diten.Platform.Contracts/Events`.
- [x] Cross-service event marker naming clarified: prefer `IIntegrationEvent` / `IInternalEvent`; `IDomainEvent` is envelope-only if retained.
- [x] MVP outbox decision set: custom MongoDB `outbox_events` + `OutboxPublisherWorker`; MassTransit native outbox/inbox out-of-scope.
- [x] Retry defaults set: 5 total attempts, 10s after the first failure, exponential backoff with jitter,
  5m maximum delay, then DLQ plus alarm after the fifth failed attempt.
- [x] DLQ retention default set to 30 days.
- [x] Event naming/versioning standard set to dot-separated lowercase segments with optional segment-internal lowercase kebab-case and a `.v{positive-version}` suffix.
- [x] Outbox transaction boundary is specified.
- [x] Inbox/idempotency standard is specified.
- [x] Consumer registration/naming standard is specified.
- [x] Event Catalog MVP decision is specified: no UI and no dynamic payload editing.
- [x] Logging/audit-worthy events and metadata fields are specified.
- [x] First golden flow test strategy uses test producer/seed command; real MOD-0009 emit remains out-of-scope.
- [x] Contract validation anti-patterns are specified.
- [x] Public publish endpoint is forbidden.
- [x] No frontend or gateway work is expected in this pack.

## 19. Implementation Notes
- This pack replaces the previous MVP note that treated MOD-0035 only as a MediatR seam. The module-level decision is central broker-backed eventing with MassTransit + RabbitMQ.
- Authority rule: this approved/ready-for-dev module pack overrides the older domain-config Event Bus note for MOD-0035 implementation scope.
- Domain-config should be updated after implementation approval to remove the stale broker-deferred statement.
- Existing event/outbox code under `Diten.Platform.Common` and `Diten.Platform.Application.Contracts` should be treated as migration input, not as the final contract ownership model.
- MassTransit native outbox/inbox is not part of MVP. A separate spike may compare it with the custom MongoDB outbox after this implementation is stable.
- The first implementation flow must use a test producer or seed command for `tenant.activated.v1`; real MOD-0009 Tenant Registry emission remains a follow-up.
- No code was written as part of module-pack preparation.

## 20. Follow-up Items
- [ ] Update `execution/domains/platform-shared-services/domain-config.md` Event Bus runtime decision after user approval.
- [ ] Update `docs/platform/master-plan.md` MOD-0035 status after implementation begins.

### PPM runtime slice authority gate

**Named slice status: `ready-for-dev`.** The parent MOD-0035 frontmatter and overall module status remain
`partial`; only the **PPM Audit Transport Slice** is promoted. Runtime still requires the recorded explicit
user approval and the separate PSS worktree.

The existing MOD-0035 identity owns mechanics for
`PpmAuditIntentSubmittedV1` / `ppm.audit-intent.submitted.v1`; no new identity is needed. A PPM handler or
controller cannot publish directly. The PPM producer worker writes/publishes only through the existing
producer-local outbox and public `IEventBus`; the MOD-0021 consumer uses inbox idempotency, retry, DLQ and
authorized replay.

The event/type is `PpmAuditIntentSubmittedV1` / `ppm.audit-intent.submitted.v1`. Scope is exactly shared
eventing mechanics, the PPM producer contract boundary and the Platform MOD-0021 consumer integration.
Frontend, Gateway, public replay endpoints/UI, generic Admin/Viewer grant-revoke changes, grant migration,
and unrelated events/modules are excluded. Explicit user runtime approval, the PSS worktree boundary and
the complete AuthService plus PPM regression gate remain mandatory and fail closed.

PSS-C1 implements the shared producer seam without changing the parent or slice status:
`Diten.BuildingBlocks.Eventing` owns the single public `IEventBus`, reusable `OutboxEventBus`,
`ICanonicalIntegrationEvent`, `IEventOutboxWriter` and trusted transport-metadata provider contracts.
Canonical events supply bounded exact UTF-8 bytes that are never reserialized. Business publish options
cannot inject raw headers; an infrastructure DI provider may emit only the allowlisted signature scheme,
key-id and derived signature after duplicate, CR/LF and byte-limit validation. Domain services retain their
own transactional Mongo persistence adapter. `EventId` plus identical immutable envelope, canonical bytes
and trusted metadata is an idempotent no-op; different immutable content is a fail-closed conflict. The
custom Mongo state sequence remains Pending/Publishing/Published/Failed/DeadLettered; MassTransit native
outbox remains forbidden. Platform's MassTransit and in-memory adapters propagate the persisted trusted
metadata while legacy unsigned events remain source-compatible. The PPM-specific event DTO and signing
provider remain MOD-0117-owned follow-up work and are not moved into Platform.

**PSS-C2 expand–contract baseline (2026-07-31):** the permanent transport identity is
`Diten.BuildingBlocks.Eventing.EventTransportMessage`; the former
`Diten.Platform.Application.Contracts.Eventing.EventTransportMessage` remains an inbound-only,
obsolete legacy identity. Platform consumers bind both URNs and map both into the same business,
inbox/idempotency and acceptance path without republishing the legacy message. New producers publish only
the shared identity. The legacy bridge cannot be removed until old ready/unacked/retry/error queues and
pending legacy outbox rows are zero, the longest retention/stale-recovery window plus observation window
has elapsed with zero legacy consumption, shared live-broker evidence passes, rollback closes, and
user/EA removal approval is recorded.

`IEventOutboxStore` exposes a distinct terminal disposition for the closed
Contract/Security/Validation/Unsupported set. Terminal failures go directly and atomically to
`DeadLettered`, have no next attempt, persist only a stable reason and redacted bounded description, are
idempotent on repetition, and cannot convert `Published`. Transient transport failures alone use the
existing producer retry schedule. Caller/stopping-token cancellation propagates unchanged and performs no
failure/dead-letter write or attempt increment. Producer retry count and deterministic delay remain
unchanged in this slice; PPM consumer jitter is a separate mechanism.
- [ ] Prepare/update MOD-0009 Tenant Registry Lifecycle Events pack to emit events through `IEventBus`.
- [ ] Optional technical spike: evaluate MassTransit native outbox/inbox against the custom MongoDB outbox after MVP.
- [ ] Prepare MOD-0038 Event Taxonomy/Naming pack for machine-readable event catalog.
- [ ] Prepare MOD-0039 Schema Compatibility pack for event versioning governance.
- [ ] Consider separate ops UI pack for DLQ/replay/event topology if operational visibility becomes MVP scope.

## Event Naming and Versioning Standard
- EventName and RabbitMQ routing key use dot-separated lowercase segments followed by `.v{positive-version}`.
- A normal segment matches `[a-z][a-z0-9]*(?:-[a-z][a-z0-9]*)*`; an internal hyphen is segment-local lowercase kebab-case and does not merge dot segments.
- `ppm.audit-intent.submitted.v1` is a canonical valid example. This reconciliation does not rename, version, or alias that existing event identity.
- Uppercase, underscore, consecutive hyphens, and leading/trailing segment hyphens are forbidden.
- Examples:
  - `tenant.created.v1`
  - `tenant.activated.v1`
  - `ppm.audit-intent.submitted.v1`
  - `tenant.suspended.v1`
  - `tenant.provisioning.started.v1`
  - `tenant.provisioning.failed.v1`
- `EventVersion` remains a numeric envelope field and must match the routing-key suffix.
- Breaking changes are published as new event names, e.g. `*.v2`.
- Existing `*.v1` consumers must not break when `*.v2` is introduced.
- Non-breaking additive changes may remain under the same version only if consumers tolerate missing/new optional fields.

## Outbox Transaction Boundary Standard
- Business aggregate save and `OutboxEvent` insert must happen in the same logical transaction/unit-of-work.
- Custom MongoDB `outbox_events` is the single source of truth for outbound publish state.
- HTTP requests and command handlers enqueue events to the outbox only.
- HTTP requests and command handlers must not publish directly to RabbitMQ.
- `OutboxPublisherWorker` is the only component that publishes outbox rows to RabbitMQ.
- Broker unavailability after business commit must not roll back the business transaction.
- Broker unavailability before publish keeps outbox rows in `Pending` or `Failed` with retry metadata.
- Outbox worker must use metadata-only logs and must never log payload.
- MassTransit native outbox/inbox is not used in MVP.
- MassTransit native outbox/inbox may be evaluated later only through a separate technical spike.

## Inbox / Idempotency Standard
- `ConsumerName + EventId` must be unique.
- Inbox check must happen before handler side effects begin.
- Duplicate delivery must skip business side effects and record/log `event.consumer.duplicate_skipped`.
- Handler success writes `Consumed` status.
- Handler failure writes failure metadata and lets MassTransit retry policy apply.
- Consumer side effects must be transactionally protected with the consumer's local state changes whenever the consuming service has persistence.

## Contract Validation Standard
- Event payload DTOs must not contain full entities or aggregate graphs.
- Payload DTOs must not inherit from or include `BaseEntity`, `EntityBase`, `GlobalEntity`, or persistence entity types.
- Payload DTOs must not expose navigation properties.
- Payload DTOs must not contain large collections.
- Payload DTOs must not contain binary/blob fields.
- Payload DTOs must not contain password, token, secret, credential, connection string, or key material fields.
- Payload DTOs must contain only IDs plus required primitive/value-object fields.
- Contract tests must detect these anti-patterns through reflection or explicit fixture-based checks.
- Contract tests must fail the build when an event payload violates these rules.

## Service/Internal Publish Security Standard
- Public publish endpoints are forbidden.
- Publish is allowed only from application services, command handlers, or internal workers through DI.
- Command handlers enqueue outbox rows; they do not publish to RabbitMQ directly.
- Future ops replay endpoint requires a separate module pack and `Platform.EventBus.Replay` permission.
- Payload replay, if introduced later, requires sensitive field redaction before display, log, replay, or persistence.

## Retry / DLQ Defaults
- Total delivery attempts: `5` (initial attempt included; four retry attempts)
- Delay after first failed attempt: `10 seconds`
- Retry strategy: exponential backoff with jitter
- Maximum retry delay: `5 minutes`
- Dead-letter transition: after the fifth failed attempt, with alarm
- DLQ retention default: `30 days`
- `LastError` max length: `4000` chars
- Sensitive data must be redacted from errors.
- Payload logging is forbidden.
- Allowed log fields: `EventId`, `EventName`, `EventVersion`, `CorrelationId`, `CausationId`, `TenantId`, `Producer`, `ConsumerName`, `Status`, `AttemptCount`, `OccurredAtUtc`.

## MassTransit Consumer Registration Standard
- Consumer registration may use assembly scanning.
- Each service registers its own consumer assembly.
- Event Bus infrastructure does not reference or know consumer implementations.
- Consumer naming convention:
  - `TenantActivatedV1Consumer`
  - `SubscriptionChangedV1Consumer`
- Consumers must be idempotent.
- Consumers must protect side effects transactionally where the service has local persistence.
- Consumers must use the shared envelope metadata for correlation, logging, and inbox checks.

## Event Catalog Decision
- MVP has no Event Catalog UI.
- Event contracts are not created from UI.
- Event payload fields are not added/removed from UI.
- Future Event Catalog may be read-only or governance-oriented.
- Catalog data may be generated from code contract metadata, attributes, or build-time manifests.
- MOD-0038 and MOD-0039 own taxonomy/governance expansion, not this MVP pack.

## Audit / Observability Standard
### Structured event names
- `event.outbox.created`
- `event.outbox.published`
- `event.outbox.publish_failed`
- `event.consumer.started`
- `event.consumer.completed`
- `event.consumer.failed`
- `event.consumer.duplicate_skipped`
- `event.deadlettered`
- `event.retry_scheduled`

### Minimum structured fields
- `EventId`
- `EventName`
- `EventVersion`
- `CorrelationId`
- `CausationId`
- `TenantId`
- `Producer`
- `ConsumerName`
- `Status`
- `AttemptCount`
- `OccurredAtUtc`

### Logging restrictions
- Payload logging is forbidden.
- Secrets, tokens, connection strings, credentials, and sensitive payload-derived fields must be redacted.
- `LastError` is truncated to 4000 chars after redaction.

## Final Readiness
- **Overall module status:** `partial`.
- **PPM Audit Transport Slice:** `ready-for-dev`, subject to its explicit user-runtime and worktree gates.
- **Docker/Testcontainers:** N/A by project decision. They are not required or expected validation paths for this project.
- **Core in-memory/fake transport tests:** PASS.
- **External RabbitMQ test:** Implemented; skipped unless `Eventing__RabbitMq__IntegrationTestsEnabled=true`.
- **Live broker proof:** Pending external/local RabbitMQ credentials and reachable broker.
- **Accepted blocker:** RabbitMQ environment is not available yet.
- **Final readiness score:** Core foundation 90/100; overall module remains partial until external/local RabbitMQ live publish-consume proof passes.
- **Remaining non-blocking notes:** Domain-config still contains stale broker-deferred language; MassTransit native outbox/inbox is intentionally out-of-scope and can be evaluated later by spike; live broker proof uses external/local RabbitMQ.
- **First next task recommendation:** Run the external/local RabbitMQ integration test with broker credentials; only after it passes, start broker-backed MOD-0009 Tenant Registry Lifecycle emission.

### PPM Audit Transport Slice final contract

Shared `EventEnvelope`, `IEventBus`, outbox and inbox mechanics belong to
`Diten.BuildingBlocks.Eventing`. MOD-0117 owns the logical PPM event, planned at
`services/Diten.PpmService/src/Diten.PpmService.Contracts/Events/**`. Platform is consumer-only for this
event. `Diten.Platform.Contracts` owns other Platform events where applicable, but not this PPM event.

The final payload is **Minimal Mutation Audit v1**, with exactly `auditIntentId`, `actorId`, `entityType`,
`entityId`, `mutation` and `occurredAtUtc`. It proves only actor, minimal mutation, PPM aggregate and time;
it does not prove authorization/entitlement and is not an aggregate snapshot or lifecycle history.

Authorized replay preserves the same `EventId` and identical canonical payload bytes; changed bytes are
rejected. If the first delivery was not accepted, replay may create exactly one `AuditEvent`; if accepted,
replay creates none. Idempotency is `ConsumerName + EventId`. Unauthorized replay is forbidden; no replay
UI/API is in scope.
