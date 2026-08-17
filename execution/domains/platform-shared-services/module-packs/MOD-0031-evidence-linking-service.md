# MOD-0031-evidence-linking-service — Evidence Linking Service (object ↔ evidence)

## 1. Module Summary
- **Module ID:** MOD-0031-evidence-linking-service
- **Module Name:** Evidence Linking Service (object ↔ evidence)
- **Domain:** Platform & Shared Services
- **Subdomain:** Content, Records & Evidence
- **Planned Wave:** W1
- **UI:** YES (Embedded)
- **Purpose:** Provide the authoritative evidence-linking capability and reusable UI surfaces that connect governed objects to supporting documents/evidence without duplicating document storage.

## 2. Ownership and Boundaries
### Owned objects (SoR)
- EvidenceLink
- EvidenceBundle (optional)
- EvidenceRequirement (optional)

### In-scope
- object ↔ document/version linking
- evidence query surface
- reusable Evidence Panel
- evidence register/search view
- required/optional completeness rules when configured

### Out-of-scope
- document storage duplication
- hardcoded business-page evidence rules
- graph analytics-heavy evidence intelligence

### Current MVP execution status
- Current MVP mode: service + embeddable UI component with policy/template-driven completeness logic.

## 3. Dependencies and Interfaces
### Consumed dependencies
- MOD-0028-document-management Document Management
- MOD-0018-rbac-abac-authorization RBAC / ABAC Authorization
- MOD-0021-audit-trail-service Audit Trail Service

### Primary consumers
- all governed workflows
- ERP and ES&BP pages needing evidence context
- compliance-sensitive flows

### Interface stubs
- API `evidence.link` — link object and document/version
- API `evidence.query` — query evidence for object
- Event `evidence.linked|unlinked` — evidence lifecycle events

## 4. Repo Scope
**Inherited convention:** use the `PlatformSharedServices` folder convention defined in `domain-config.md`.

### Recommended backend scope
- `services/Diten.Platform/src/Diten.Platform.Domain/Aggregates/MOD-0031-evidence-linking-service/`
- `services/Diten.Platform/src/Diten.Platform.Application/Commands/MOD-0031-evidence-linking-service/`
- `services/Diten.Platform/src/Diten.Platform.Application/Queries/MOD-0031-evidence-linking-service/`
- `services/Diten.Platform/src/Diten.Platform.Application/Handlers/MOD-0031-evidence-linking-service/`
- `services/Diten.Platform/src/Diten.Platform.Application/Services/MOD-0031-evidence-linking-service/`
- `services/Diten.Platform/src/Diten.Platform.Persistence/Repositories/MOD-0031-evidence-linking-service/`
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/MOD-0031-evidence-linking-service/`
- shared MVC partial/component pattern under views/shared or PlatformSharedServices as appropriate

### Recommended frontend scope
- `frontend/Diten.Web/Controllers/PlatformSharedServices/MOD-0031-evidence-linking-serviceController.cs`
- `frontend/Diten.Web/Views/PlatformSharedServices/MOD-0031-evidence-linking-service/`
- `frontend/Diten.Web/wwwroot/js/platform-shared-services/mod-0031.js`

### Protected paths
- Inherit all protected paths from `domain-config.md`.
- Do not implement this module inside ES&BP, Demand, or Delivery feature trees.

## 5. UI Surfaces
- Evidence Panel — embedded attach/link/status surface
- Evidence Register — cross-object list/search view

## 6. Runtime Constraints
- link/unlink history must be auditable
- evidence semantics remain separate from document storage
- requirements must be config/policy-driven

## 7. Acceptance Criteria
- Evidence links are immutable in history terms and auditable.
- Evidence panel supports required/optional rules per workflow state when configured.
- Evidence can be queried across objects without forcing changes into protected business views.

## 8. Testing Notes
- Run targeted backend build: `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.WebAPI.csproj`
- Run targeted frontend build: `dotnet build frontend/Diten.Web/Diten.WebUI.csproj`
- link/unlink service tests
- evidence query tests
- embedded panel behavior tests
- UI build/tests

## 9. Implementation Notes
- This module should be consumable as both service and reusable UI component.
- Do not over-model optional bundle/requirement objects unless needed for active flows.

## 10. Follow-up Items
- Advanced evidence analytics remain future-state.
