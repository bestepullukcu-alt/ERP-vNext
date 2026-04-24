# MOD-0037-integration-monitoring — Integration Monitoring & Reconciliation

## 1. Module Summary
- **Module ID:** MOD-0037-integration-monitoring
- **Module Name:** Integration Monitoring & Reconciliation
- **Domain:** Platform & Shared Services
- **Subdomain:** Integration & Interoperability
- **Planned Wave:** W2/W3
- **UI:** YES (Ops)
- **Purpose:** Define the authoritative integration-monitoring and reconciliation boundary while the current MVP keeps the full module deferred.

## 2. Ownership and Boundaries
### Owned objects (SoR)
- IntegrationRun
- MessageRecord
- ReconciliationCase

### In-scope
- documentation of deferred state only in current MVP

### Out-of-scope
- integration-ops dashboard
- failed-message replay console
- reconciliation workbench

### Current MVP execution status
- Current MVP status: deferred / not implemented.

## 3. Dependencies and Interfaces
### Consumed dependencies
- MOD-0032-api-gateway API Gateway
- MOD-0035-event-bus-message-queue Event Bus
- MOD-0041-logging-monitoring Logging & Monitoring
- MOD-0018-rbac-abac-authorization RBAC / ABAC Authorization
- MOD-0021-audit-trail-service Audit Trail Service

### Primary consumers
- ops users
- integration owners
- future support workflows

### Interface stubs
- API `integrations.health` — future-state only
- API `integrations.replay` — future-state only
- Event `integration.failed|recovered` — future-state only

## 4. Repo Scope
**Inherited convention:** use the `PlatformSharedServices` folder convention defined in `domain-config.md`.

### Recommended backend scope
- `services/Diten.Platform/src/Diten.Platform.Domain/Aggregates/MOD-0037-integration-monitoring/`
- `services/Diten.Platform/src/Diten.Platform.Application/Commands/MOD-0037-integration-monitoring/`
- `services/Diten.Platform/src/Diten.Platform.Application/Queries/MOD-0037-integration-monitoring/`
- `services/Diten.Platform/src/Diten.Platform.Application/Handlers/MOD-0037-integration-monitoring/`
- `services/Diten.Platform/src/Diten.Platform.Application/Services/MOD-0037-integration-monitoring/`
- `services/Diten.Platform/src/Diten.Platform.Persistence/Repositories/MOD-0037-integration-monitoring/`
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/MOD-0037-integration-monitoring/`
- documentation/ADR notes only in current MVP

### Recommended frontend scope
- `frontend/Diten.Web/Controllers/PlatformSharedServices/MOD-0037-integration-monitoringController.cs`
- `frontend/Diten.Web/Views/PlatformSharedServices/MOD-0037-integration-monitoring/`
- `frontend/Diten.Web/wwwroot/js/platform-shared-services/mod-0037.js`

### Protected paths
- Inherit all protected paths from `domain-config.md`.
- Do not implement this module inside ES&BP, Demand, or Delivery feature trees.

## 5. UI Surfaces
- Integration Health — future-state dashboard
- Failed Messages Queue — future-state inbox

## 6. Runtime Constraints
- do not introduce partial replay/reconciliation features in current MVP

## 7. Acceptance Criteria
- Deferred state is explicit.
- No runtime monitoring/reconciliation surfaces are introduced.

## 8. Testing Notes
- Run targeted backend build: `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.WebAPI.csproj`
- Run targeted frontend build: `dotnet build frontend/Diten.Web/Diten.WebUI.csproj`
- documentation consistency checks only unless re-scoped

## 9. Implementation Notes
- Likely later-wave ops-hardening module.

## 10. Follow-up Items
- Activate only if runtime decisions and platform maturity justify it.
