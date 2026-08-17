# MOD-0042-alerting-incident-runbooks — Alerting & Incident Runbooks

## 1. Module Summary
- **Module ID:** MOD-0042-alerting-incident-runbooks
- **Module Name:** Alerting & Incident Runbooks
- **Domain:** Platform & Shared Services
- **Subdomain:** Observability & Continuity
- **Planned Wave:** W2/W3
- **UI:** OPTIONAL (Prefer external)
- **Purpose:** Define the authoritative alerting/runbook boundary while the current MVP defers a dedicated module surface.

## 2. Ownership and Boundaries
### Owned objects (SoR)
- AlertRule
- IncidentRunbook
- NotificationRoute

### In-scope
- documentation of deferred status only in current MVP

### Out-of-scope
- alert inbox
- runbook catalog
- incident operations console
- external alerting-product clone

### Current MVP execution status
- Current MVP status: deferred as a dedicated operational surface; basic observability hooks only.

## 3. Dependencies and Interfaces
### Consumed dependencies
- MOD-0041-logging-monitoring Logging & Monitoring
- MOD-0027 Notifications (optional)
- MOD-0018-rbac-abac-authorization RBAC / ABAC Authorization
- MOD-0021-audit-trail-service Audit Trail Service

### Primary consumers
- ops users
- future incident response workflows

### Interface stubs
- API `alerts.rules.*` — future-state only
- API `runbooks.*` — future-state only
- Event `alert.triggered|resolved` — future-state only

## 4. Repo Scope
**Inherited convention:** use the `PlatformSharedServices` folder convention defined in `domain-config.md`.

### Recommended backend scope
- `services/Diten.Platform/src/Diten.Platform.Domain/Aggregates/MOD-0042-alerting-incident-runbooks/`
- `services/Diten.Platform/src/Diten.Platform.Application/Commands/MOD-0042-alerting-incident-runbooks/`
- `services/Diten.Platform/src/Diten.Platform.Application/Queries/MOD-0042-alerting-incident-runbooks/`
- `services/Diten.Platform/src/Diten.Platform.Application/Handlers/MOD-0042-alerting-incident-runbooks/`
- `services/Diten.Platform/src/Diten.Platform.Application/Services/MOD-0042-alerting-incident-runbooks/`
- `services/Diten.Platform/src/Diten.Platform.Persistence/Repositories/MOD-0042-alerting-incident-runbooks/`
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/MOD-0042-alerting-incident-runbooks/`
- documentation/ADR notes only in current MVP

### Recommended frontend scope
- `frontend/Diten.Web/Controllers/PlatformSharedServices/MOD-0042-alerting-incident-runbooksController.cs`
- `frontend/Diten.Web/Views/PlatformSharedServices/MOD-0042-alerting-incident-runbooks/`
- `frontend/Diten.Web/wwwroot/js/platform-shared-services/mod-0042.js`

### Protected paths
- Inherit all protected paths from `domain-config.md`.
- Do not implement this module inside ES&BP, Demand, or Delivery feature trees.

## 5. UI Surfaces
- Alerts — future-state inbox
- Runbooks — future-state catalog

## 6. Runtime Constraints
- do not create partial alerting consoles without decision change

## 7. Acceptance Criteria
- Deferred state is explicit.
- No dedicated alerting/runbook runtime surface is introduced in current MVP.

## 8. Testing Notes
- Run targeted backend build: `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.WebAPI.csproj`
- Run targeted frontend build: `dotnet build frontend/Diten.Web/Diten.WebUI.csproj`
- documentation consistency checks only unless re-scoped

## 9. Implementation Notes
- Runbook linkage can remain conceptual until ops-hardening wave.

## 10. Follow-up Items
- Revisit when observability strategy changes or external tool adoption is approved.
