# MOD-0028-document-management — Document Management (Templates / Versioning)

## 1. Module Summary
- **Module ID:** MOD-0028-document-management
- **Module Name:** Document Management (Templates / Versioning)
- **Domain:** Platform & Shared Services
- **Subdomain:** Content, Records & Evidence
- **Planned Wave:** W1
- **UI:** YES (Core)
- **Purpose:** Provide the authoritative shared document capability for metadata, versioning, template management, and access-controlled artifact storage/use.

## 2. Ownership and Boundaries
### Owned objects (SoR)
- Document
- DocumentVersion
- Template
- Folder/Collection

### In-scope
- document upload/download
- document version publishing
- template CRUD/versioning
- library and detail UI
- RBAC-gated access

### Out-of-scope
- evidence semantics
- advanced DLP/records tooling
- secret storage inside metadata
- external ECM product scope

### Current MVP execution status
- Current repo has partial upload/template support; full document SoR is target-state for MOD-0028-document-management.

## 3. Dependencies and Interfaces
### Consumed dependencies
- MOD-0018-rbac-abac-authorization RBAC / ABAC Authorization
- MOD-0021-audit-trail-service Audit Trail Service

### Primary consumers
- all modules
- document-heavy approval flows
- evidence linking as downstream consumer

### Interface stubs
- API `docs.upload|download` — document version transfer
- API `docs.templates.*` — template CRUD
- Event `doc.version.published` — new version notification

## 4. Repo Scope
**Inherited convention:** use the `PlatformSharedServices` folder convention defined in `domain-config.md`.

### Recommended backend scope
- `services/Diten.Platform/src/Diten.Platform.Domain/Aggregates/MOD-0028-document-management/`
- `services/Diten.Platform/src/Diten.Platform.Application/Commands/MOD-0028-document-management/`
- `services/Diten.Platform/src/Diten.Platform.Application/Queries/MOD-0028-document-management/`
- `services/Diten.Platform/src/Diten.Platform.Application/Handlers/MOD-0028-document-management/`
- `services/Diten.Platform/src/Diten.Platform.Application/Services/MOD-0028-document-management/`
- `services/Diten.Platform/src/Diten.Platform.Persistence/Repositories/MOD-0028-document-management/`
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/MOD-0028-document-management/`
- storage seam implementation under infrastructure/persistence as needed

### Recommended frontend scope
- `frontend/Diten.Web/Controllers/PlatformSharedServices/MOD-0028-document-managementController.cs`
- `frontend/Diten.Web/Views/PlatformSharedServices/MOD-0028-document-management/`
- `frontend/Diten.Web/wwwroot/js/platform-shared-services/mod-0028.js`

### Protected paths
- Inherit all protected paths from `domain-config.md`.
- Do not implement this module inside ES&BP, Demand, or Delivery feature trees.

## 5. UI Surfaces
- Document Library — catalog with search/folders/bulk actions
- Document Detail — metadata, versions, permissions
- Template Manager — template catalog/versioning

## 6. Runtime Constraints
- every upload/publish is audited
- version history is immutable
- access is RBAC-gated
- module does not decide evidence completeness

## 7. Acceptance Criteria
- Uploads and version publishes emit audit events.
- Sensitive documents require explicit permission scope.
- Template/versioning flows are version-safe and discoverable.

## 8. Testing Notes
- Run targeted backend build: `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.WebAPI.csproj`
- Run targeted frontend build: `dotnet build frontend/Diten.Web/Diten.WebUI.csproj`
- document versioning tests
- permission tests
- template CRUD tests
- UI build/tests

## 9. Implementation Notes
- Keep storage seam abstract enough to swap underlying storage later.
- Folder/collection model can remain lightweight in MVP.

## 10. Follow-up Items
- Retention/DLP/records maturity can be layered later.
