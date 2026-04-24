# MOD-0035-event-bus-message-queue — Event Bus / Message Queue

## 1. Module Summary
- **Module ID:** MOD-0035-event-bus-message-queue
- **Module Name:** Event Bus / Message Queue
- **Domain:** Platform & Shared Services
- **Subdomain:** Integration & Interoperability
- **Planned Wave:** W2 (W1 if event-first)
- **UI:** OPTIONAL
- **Purpose:** Provide the authoritative eventing boundary for platform modules while the current MVP uses an internal lightweight MediatR-based dispatch seam instead of an external broker.

## 2. Ownership and Boundaries
### Owned objects (SoR)
- Topic
- Subscription
- DeadLetterQueueRecord

### In-scope
- explicit internal event seam
- handler registration discipline
- schema/version governance for internal events
- correlation propagation across handlers

### Out-of-scope
- external message broker
- DLQ/replay mechanics
- provider-console cloning

### Current MVP execution status
- Current MVP mode: native lightweight internal event dispatch via MediatR; no broker, DLQ, or replay subsystem.

## 3. Dependencies and Interfaces
### Consumed dependencies
- MOD-0018-rbac-abac-authorization RBAC / ABAC Authorization
- MOD-0021-audit-trail-service Audit Trail Service
- MOD-0032-api-gateway API Gateway (ops only, future)
- MOD-0041-logging-monitoring Logging & Monitoring

### Primary consumers
- internal handlers
- integration adapters
- future ops tooling

### Interface stubs
- API `events.publish` — service-only publish seam
- API `events.subscribe` — registration pattern / service seam
- Event `dlq.recorded` — future-state only if broker mode is adopted

## 4. Repo Scope
**Inherited convention:** use the `PlatformSharedServices` folder convention defined in `domain-config.md`.

### Recommended backend scope
- `services/Diten.Platform/src/Diten.Platform.Domain/Aggregates/MOD-0035-event-bus-message-queue/`
- `services/Diten.Platform/src/Diten.Platform.Application/Commands/MOD-0035-event-bus-message-queue/`
- `services/Diten.Platform/src/Diten.Platform.Application/Queries/MOD-0035-event-bus-message-queue/`
- `services/Diten.Platform/src/Diten.Platform.Application/Handlers/MOD-0035-event-bus-message-queue/`
- `services/Diten.Platform/src/Diten.Platform.Application/Services/MOD-0035-event-bus-message-queue/`
- `services/Diten.Platform/src/Diten.Platform.Persistence/Repositories/MOD-0035-event-bus-message-queue/`
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/MOD-0035-event-bus-message-queue/`
- src/Backend/Diten.Application/Handlers
- src/Backend/Diten.Application/Commands
- DI registration in application/web startup

### Recommended frontend scope
- `frontend/Diten.Web/Controllers/PlatformSharedServices/MOD-0035-event-bus-message-queueController.cs`
- `frontend/Diten.Web/Views/PlatformSharedServices/MOD-0035-event-bus-message-queue/`
- `frontend/Diten.Web/wwwroot/js/platform-shared-services/mod-0035.js`

### Protected paths
- Inherit all protected paths from `domain-config.md`.
- Do not implement this module inside ES&BP, Demand, or Delivery feature trees.

## 5. UI Surfaces
- Event Topology — future-state thin catalog if needed
- DLQ Viewer — future-state only

## 6. Runtime Constraints
- do not model broker-only behavior into MediatR-only environment
- keep schema_version and correlation_id on internal event contracts

## 7. Acceptance Criteria
- Internal event dispatch is explicit and reusable.
- Correlation IDs flow through handler boundaries.
- No broker/DLQ assumptions are implemented in current MVP.

## 8. Testing Notes
- Run targeted backend build: `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.WebAPI.csproj`
- Run targeted frontend build: `dotnet build frontend/Diten.Web/Diten.WebUI.csproj`
- handler invocation tests
- repeat-invocation/idempotency tests
- contract-shape tests

## 9. Implementation Notes
- Treat this as a seam, not a product, in current MVP.

## 10. Follow-up Items
- External provider adoption later requires decision-log update and module-pack expansion.
