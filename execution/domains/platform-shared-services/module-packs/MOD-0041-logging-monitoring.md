# MOD-0041-logging-monitoring — Logging & Monitoring

## 1. Module Summary
- **Module ID:** MOD-0041-logging-monitoring
- **Module Name:** Logging & Monitoring
- **Domain:** Platform & Shared Services
- **Subdomain:** Observability & Continuity
- **Planned Wave:** W2/W3
- **UI:** OPTIONAL (Prefer external)
- **Purpose:** Provide the authoritative observability boundary while the current MVP uses lightweight `ILogger` + correlation hooks instead of a full external telemetry product.

## 2. Ownership and Boundaries
### Owned objects (SoR)
- LogSignal
- MetricSignal
- TraceLink (optional)

### In-scope
- structured logging conventions
- correlation propagation
- basic health/triage navigation
- minimal monitoring overview if needed

### Out-of-scope
- SIEM/APM platform
- advanced telemetry analytics
- external-provider console cloning

### Current MVP execution status
- Current MVP mode: native lightweight telemetry seam using `ILogger`, correlation middleware/context, and basic health checks.

## 3. Dependencies and Interfaces
### Consumed dependencies
- MOD-0018-rbac-abac-authorization RBAC / ABAC Authorization
- MOD-0021-audit-trail-service Audit Trail Service
- correlation middleware
- health endpoints

### Primary consumers
- ops users
- platform services
- future alerting layer

### Interface stubs
- API `telemetry.query` — thin future-state query surface if needed
- Event `telemetry.threshold.breached` — future-state/optional

## 4. Repo Scope
**Inherited convention:** use the `PlatformSharedServices` folder convention defined in `domain-config.md`.

### Recommended backend scope
- `services/Diten.Platform/src/Diten.Platform.Domain/Aggregates/MOD-0041-logging-monitoring/`
- `services/Diten.Platform/src/Diten.Platform.Application/Commands/MOD-0041-logging-monitoring/`
- `services/Diten.Platform/src/Diten.Platform.Application/Queries/MOD-0041-logging-monitoring/`
- `services/Diten.Platform/src/Diten.Platform.Application/Handlers/MOD-0041-logging-monitoring/`
- `services/Diten.Platform/src/Diten.Platform.Application/Services/MOD-0041-logging-monitoring/`
- `services/Diten.Platform/src/Diten.Platform.Persistence/Repositories/MOD-0041-logging-monitoring/`
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/MOD-0041-logging-monitoring/`
- services/Diten.Platform/src/Diten.Platform.API/Middleware/CorrelationIdMiddleware.cs
- services/Diten.Platform/src/Diten.Platform.API/Health
- services/Diten.Platform/src/Diten.Platform.API/Program.cs

### Recommended frontend scope
- `frontend/Diten.Web/Controllers/PlatformSharedServices/MOD-0041-logging-monitoringController.cs`
- `frontend/Diten.Web/Views/PlatformSharedServices/MOD-0041-logging-monitoring/`
- `frontend/Diten.Web/wwwroot/js/platform-shared-services/mod-0041.js`

### Protected paths
- Inherit all protected paths from `domain-config.md`.
- Do not implement this module inside ES&BP, Demand, or Delivery feature trees.

## 5. UI Surfaces
- Monitoring Overview — thin dashboard/deep-link surface if activated

## 6. Runtime Constraints
- keep telemetry light and reusable
- do not claim external monitoring capability where none exists

## 7. Acceptance Criteria
- All platform modules emit structured logs with correlation IDs.
- Monitoring seam supports fast triage navigation even when tooling remains lightweight.

## 8. Testing Notes
- Run targeted backend build: `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.WebAPI.csproj`
- Run targeted frontend build: `dotnet build frontend/Diten.Web/Diten.WebUI.csproj`
- correlation middleware tests
- logging contract tests
- health endpoint smoke checks

## 9. Implementation Notes
- This is a governance/glue layer in current MVP, not a full observability product.

## 10. Follow-up Items
- External observability adoption later may move runtime SoR out of the app.
