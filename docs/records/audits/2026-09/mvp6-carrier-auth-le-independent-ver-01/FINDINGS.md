# Independent findings

## F1 — internal scope endpoint cannot establish tenant context

**Severity:** blocking  
**Disposition:** independently reproduced

The approved endpoint authenticates the internal key and then calls the tenant-filtered resolver. The common tenant middleware explicitly bypasses every `/api/internal` request, so `TenantContext.TenantId` remains unresolved and throws before scope resolution.

Independent runtime results from the fresh Platform build:

- wrong internal key: HTTP `401`;
- correct internal key: HTTP `400`, `TenantId has not been resolved yet`.

This prevents the candidate's required single-active-LE behavior. The zero, multiple, inactive/revoked, tenant and actor variants were not independently executed after this controlling blocker was confirmed.

Source evidence is preserved in `evidence/source-path-evidence.txt`:

- `InternalTenantLegalEntityScopeController.cs:28-48`;
- `TenantResolutionMiddleware.cs:254-262`;
- `TenantContext.cs:14-18`.

## F2 — login and refresh do not carry authorization into MDM validation

**Severity:** blocking successor seam  
**Disposition:** source-path confirmed; runtime path not reached because F1 occurs first

The Auth-to-Platform internal client sends the internal key and correlation only. Platform's MDM validator forwards an `Authorization` header only when the incoming request already contains one. The internal Auth request has none. The MDM lookup-validation endpoint has both `[Authorize]` and `mdm.legal-entities.read` permission enforcement.

Source evidence is preserved in `evidence/source-path-evidence.txt`:

- `PlatformTenantLegalEntityScopeClient.cs:43-54`;
- `MdmLegalEntityReferenceValidator.cs:104-109,142-151`;
- `LegalEntitiesController.cs:11-14,48-53`.

The independent run does not relabel this source proof as runtime reproduction. An authorized server-to-server MDM lookup design remains outside the approved ten-path patch.

## Writer evidence disposition

The writer's redacted login and refresh evidence is content-bound to the exact approved source package: both HTTP requests succeeded but both decoded claim sets had `legal_entity_id: null`. This verifier did not archive or reuse the underlying bearer tokens and did not claim those calls as a fresh independent run.
