# MOD-0012-secrets-configuration-vault — Secrets & Configuration Vault

## 1. Module Summary
- **Module ID:** MOD-0012-secrets-configuration-vault
- **Module Name:** Secrets & Configuration Vault
- **Domain:** Platform & Shared Services
- **Subdomain:** Platform Foundation
- **Planned Wave:** W1
- **UI:** YES (Thin Admin)
- **Purpose:** Provide the authoritative platform seam for secret/configuration governance while the current MVP uses local appsettings/environment-variable backing rather than an external vault provider.

## 2. Ownership and Boundaries
### Owned objects (SoR)
- Secret
- ConfigProfile
- RotationPolicy

### In-scope
- thin admin catalog for secrets/config profiles/rotation state
- service-side secret read seam (`vault.read`)
- audited secret create/update/read operations
- masked UI behavior and logging hygiene

### Out-of-scope
- external vault providers
- advanced enterprise rotation orchestration
- plaintext secret display
- provider-console cloning

### Current MVP execution status
- Current MVP mode: native local configuration/environment abstraction; no external vault provider.

## 3. Dependencies and Interfaces
### Consumed dependencies
- MOD-0018-rbac-abac-authorization RBAC / ABAC Authorization
- MOD-0021-audit-trail-service Audit Trail Service

### Primary consumers
- integration services
- credential-governance flows
- operations admins

### Interface stubs
- API `vault.secrets.*` — admin CRUD/governance
- API `vault.read` — service identity read path
- Event `vault.secret.rotated` — optional rotation event

## 4. Repo Scope
**Inherited convention:** use the `PlatformSharedServices` folder convention defined in `domain-config.md`.

### Recommended backend scope
- `services/Diten.Platform/src/Diten.Platform.Domain/Aggregates/MOD-0012-secrets-configuration-vault/`
- `services/Diten.Platform/src/Diten.Platform.Application/Commands/MOD-0012-secrets-configuration-vault/`
- `services/Diten.Platform/src/Diten.Platform.Application/Queries/MOD-0012-secrets-configuration-vault/`
- `services/Diten.Platform/src/Diten.Platform.Application/Handlers/MOD-0012-secrets-configuration-vault/`
- `services/Diten.Platform/src/Diten.Platform.Application/Services/MOD-0012-secrets-configuration-vault/`
- `services/Diten.Platform/src/Diten.Platform.Persistence/Repositories/MOD-0012-secrets-configuration-vault/`
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/MOD-0012-secrets-configuration-vault/`
- services/Diten.Platform/src/Diten.Platform.API/Program.cs for config wiring
- services/Diten.Platform/src/Diten.Platform.Infrastructure/DependencyInjection.cs for adapter registration

### Recommended frontend scope
- `frontend/Diten.Web/Controllers/PlatformSharedServices/MOD-0012-secrets-configuration-vaultController.cs`
- `frontend/Diten.Web/Views/PlatformSharedServices/MOD-0012-secrets-configuration-vault/`
- `frontend/Diten.Web/wwwroot/js/platform-shared-services/mod-0012.js`

### Protected paths
- Inherit all protected paths from `domain-config.md`.
- Do not implement this module inside ES&BP, Demand, or Delivery feature trees.

## 5. UI Surfaces
- Vault Admin — thin catalog/workspace for secret list, access policy, rotation status

## 6. Runtime Constraints
- never expose plaintext to UI
- mask secrets in logs and audit payloads
- treat config/env seam as replaceable abstraction

## 7. Acceptance Criteria
- Secret read is audited with requester identity and reason/context where available.
- Unauthorized UI users never receive plaintext secret values.
- Vault seams remain replaceable without changing consumer contracts.

## 8. Testing Notes
- Run targeted backend build: `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.WebAPI.csproj`
- Run targeted frontend build: `dotnet build frontend/Diten.Web/Diten.WebUI.csproj`
- service read authorization tests
- masking and serialization tests
- admin UI permission tests

## 9. Implementation Notes
- Prefer a provider-agnostic interface from day one.
- Keep rotation metadata lightweight in MVP.

## 10. Follow-up Items
- Future-state external vault integration can reuse the same module contract.
