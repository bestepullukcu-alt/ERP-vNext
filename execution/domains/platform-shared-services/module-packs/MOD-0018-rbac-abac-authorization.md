# MOD-0018-rbac-abac-authorization — RBAC / ABAC Authorization

## 1. Module Summary
- **Module ID:** MOD-0018-rbac-abac-authorization
- **Module Name:** RBAC / ABAC Authorization
- **Domain:** Platform & Shared Services
- **Subdomain:** Identity, Access & Trust
- **Planned Wave:** W1
- **UI:** YES (Admin)
- **Purpose:** Provide the authoritative shared authorization capability for roles, permissions, assignments, and minimal ABAC policies consumed by all platform and business modules.

## 2. Ownership and Boundaries
### Owned objects (SoR)
- Role
- Permission
- Assignment
- ABAC Policy

### In-scope
- role catalog and role detail
- permission matrix and assignment management
- `authz.check` authorization service/API
- optional policy test/simulation surface if capacity allows
- audit on create/update/publish and denied protected actions where applicable

### Out-of-scope
- user directory / IdP ownership
- complex policy DSL
- advanced analytics/reporting
- organization-master ownership

### Current MVP execution status
- Current MVP mode: implement RBAC-first with minimal ABAC conditions and resource tags.

## 3. Dependencies and Interfaces
### Consumed dependencies
- current WebAPI security wiring
- MOD-0021-audit-trail-service Audit Trail Service

### Primary consumers
- all modules
- admin users
- approval and document access flows

### Interface stubs
- API `authz.check` — authorization decision for action/resource
- API `rbac.roles.*` — role/permission/assignment CRUD
- Event `authz.policy.changed` — cache invalidation / consumer notification

## 4. Repo Scope
**Inherited convention:** use the `PlatformSharedServices` folder convention defined in `domain-config.md`.

### Recommended backend scope
- `services/Diten.AuthService/src/Diten.AuthService.Domain/Aggregates/MOD-0018-rbac-abac-authorization/`
- `services/Diten.AuthService/src/Diten.AuthService.Application/Commands/MOD-0018-rbac-abac-authorization/`
- `services/Diten.AuthService/src/Diten.AuthService.Application/Queries/MOD-0018-rbac-abac-authorization/`
- `services/Diten.AuthService/src/Diten.AuthService.Application/Handlers/MOD-0018-rbac-abac-authorization/`
- `services/Diten.AuthService/src/Diten.AuthService.Application/Services/MOD-0018-rbac-abac-authorization/`
- `services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/MOD-0018-rbac-abac-authorization/`
- `services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/MOD-0018-rbac-abac-authorization/`
- services/Diten.AuthService/src/Diten.AuthService.Api/Security for integration hooks
- services/Diten.AuthService/src/Diten.AuthService.Api/Program.cs for authz service registration

### Recommended frontend scope
- `frontend/Diten.Web/Controllers/PlatformSharedServices/MOD-0018-rbac-abac-authorizationController.cs`
- `frontend/Diten.Web/Views/PlatformSharedServices/MOD-0018-rbac-abac-authorization/`
- `frontend/Diten.Web/wwwroot/js/platform-shared-services/mod-0018.js`

### Protected paths
- Inherit all protected paths from `domain-config.md`.
- Do not implement this module inside ES&BP, Demand, or Delivery feature trees.

## 5. UI Surfaces
- RBAC Admin — workspace for roles, permissions, assignments
- Policy Tester — optional tool for user/action/resource simulation

## 6. Runtime Constraints
- deny by default
- keep ABAC intentionally minimal
- do not bypass existing security infrastructure
- audit all privileged changes

## 7. Acceptance Criteria
- Protected actions are denied when permission is missing, and denial is auditable.
- Role/policy updates invalidate or refresh downstream authorization decisions through the agreed mechanism.
- Authorization contracts use Contract Envelope v1 where applicable.

## 8. Testing Notes
- Run targeted backend build: `dotnet build services/Diten.AuthService/src/Diten.AuthService.Api/Diten.AuthService.Api.csproj`
- Run targeted frontend build: `dotnet build frontend/Diten.Web/Diten.Web.csproj`
- permission resolution tests
- assignment CRUD tests
- denied-action audit tests
- admin UI build/tests

## 9. Implementation Notes
- Attribute conditions should remain simple in MVP.
- Keep permission-scope taxonomy normalized and reusable.

## 10. Follow-up Items
- Future policy DSL or delegation modeling belongs in a later maturity wave.
