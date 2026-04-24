# MOD-0021-audit-trail-service — Audit Trail Service

## 1. Module Summary
- **Module ID:** MOD-0021-audit-trail-service
- **Module Name:** Audit Trail Service
- **Domain:** Platform & Shared Services
- **Subdomain:** Identity, Access & Trust
- **Planned Wave:** W1
- **UI:** YES (Viewer)
- **Purpose:** Provide the authoritative append-only audit infrastructure for platform and consumer modules, including immutable writes and read-only query surfaces.

## 2. Ownership and Boundaries
### Owned objects (SoR)
- AuditEvent
- AuditQueryView

### In-scope
- server-side audit append path (`audit.write`)
- audit search/query surface (`audit.search`)
- Audit Log list/search UI
- Audit Event Detail UI with correlated objects/evidence references

### Out-of-scope
- audit event mutation
- PII/secret leakage into payloads
- advanced analytics lake/reporting

### Current MVP execution status
- Current repo has a partial enterprise-strategy audit seam; target-state ownership stays with MOD-0021-audit-trail-service.

## 3. Dependencies and Interfaces
### Consumed dependencies
- MOD-0018-rbac-abac-authorization RBAC / ABAC Authorization
- optional attachment references from MOD-0028-document-management Document Management

### Primary consumers
- all modules
- auditors
- operations admins
- governance readers

### Interface stubs
- API `audit.write` — append audit event
- API `audit.search` — authorized read/filter
- Event `audit.event.appended` — optional downstream notification

## 4. Repo Scope
**Inherited convention:** use the `PlatformSharedServices` folder convention defined in `domain-config.md`.

### Recommended backend scope
- `services/Diten.Platform/src/Diten.Platform.Domain/Aggregates/MOD-0021-audit-trail-service/`
- `services/Diten.Platform/src/Diten.Platform.Application/Commands/MOD-0021-audit-trail-service/`
- `services/Diten.Platform/src/Diten.Platform.Application/Queries/MOD-0021-audit-trail-service/`
- `services/Diten.Platform/src/Diten.Platform.Application/Handlers/MOD-0021-audit-trail-service/`
- `services/Diten.Platform/src/Diten.Platform.Application/Services/MOD-0021-audit-trail-service/`
- `services/Diten.Platform/src/Diten.Platform.Persistence/Repositories/MOD-0021-audit-trail-service/`
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/MOD-0021-audit-trail-service/`
- services/Diten.Platform/src/Diten.Platform.API/Middleware/CorrelationIdMiddleware.cs for correlation continuity
- existing persistence audit seam as pattern input only

### Recommended frontend scope
- `frontend/Diten.Web/Controllers/PlatformSharedServices/MOD-0021-audit-trail-serviceController.cs`
- `frontend/Diten.Web/Views/PlatformSharedServices/MOD-0021-audit-trail-service/`
- `frontend/Diten.Web/wwwroot/js/platform-shared-services/mod-0021.js`

### Protected paths
- Inherit all protected paths from `domain-config.md`.
- Do not implement this module inside ES&BP, Demand, or Delivery feature trees.

## 5. UI Surfaces
- Audit Log — list/inbox with search, filters, export
- Audit Event Detail — full payload and correlation view

## 6. Runtime Constraints
- append-only semantics
- correlation_id mandatory
- before/after snapshots only where appropriate and safe
- viewer is read-only

## 7. Acceptance Criteria
- Updates/deletes of audit events are not permitted.
- Workflow approvals and other privileged state transitions emit audit events with correlation IDs.
- Readers can trace events by actor, tenant, object reference, and correlation ID.

## 8. Testing Notes
- Run targeted backend build: `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.WebAPI.csproj`
- Run targeted frontend build: `dotnet build frontend/Diten.Web/Diten.WebUI.csproj`
- append-only repository tests
- query/filter tests
- correlation propagation tests
- viewer UI tests/build

## 9. Implementation Notes
- Treat the current audit seam as a transitional pattern, not final ownership proof.
- Protect secrets and sensitive fields from payload leakage.

## 10. Follow-up Items
- A broader audit lake can be layered later without moving audit ownership away from this module.
