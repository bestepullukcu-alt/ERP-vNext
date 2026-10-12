# MVP6-CARRIER-AUTH-LE-IMPLEMENT-01 — Findings

## F1 — Internal resolver has no tenant context

**Severity:** blocking  
**Evidence type:** runtime + source  
**Disposition:** OPEN / REWORK

The approved candidate exposes `GET /api/internal/tenants/{tenantId}/actors/{actorId}/legal-entity-scope` and validates the internal API key before calling `IDataScopeResolver`. Platform's tenant middleware bypasses every `/api/internal` path. The endpoint therefore reaches tenant-filtered repositories without setting `ITenantContext`.

- Endpoint call: `services/Diten.Platform/src/Diten.Platform.API/Controllers/Internal/InternalTenantLegalEntityScopeController.cs:36-42`
- Internal-path bypass: `services/Diten.Platform.Common/src/Diten.Platform.Common/Tenancy/TenantResolutionMiddleware.cs:260-262`
- Fail-closed tenant access: `services/Diten.Platform.Common/src/Diten.Platform.Common/Tenancy/TenantContext.cs:14-16`
- Runtime response with the correct internal key: HTTP 400, `TenantId has not been resolved yet` (`raw-evidence.tar.gz::raw/runtime-evidence/internal-scope-correct-key.*`).
- Wrong internal key: HTTP 401 (`raw-evidence.tar.gz::raw/runtime-evidence/internal-scope-wrong-key.*`).
- Tenant/actor variants all stop at the same HTTP 400 before scope evaluation (`raw-evidence.tar.gz::raw/runtime-evidence/tenant-actor-boundaries.tsv`).

The endpoint authentication check works, but the approved implementation cannot resolve any Legal Entity scope.

## F2 — MDM revalidation has no server-to-server authorization path

**Severity:** blocking after F1  
**Evidence type:** exact source-path analysis; not reached in the runtime run because F1 fails first  
**Disposition:** OPEN / NEW AUTHORITY REQUIRED

`OrgDataScopeResolver` revalidates candidate Legal Entities through `MdmLegalEntityReferenceValidator`. That validator forwards the caller `Authorization` header when one exists. The Auth-to-Platform internal request contains only the internal API key and correlation header. Login and refresh therefore provide no bearer token for the downstream MDM request. The real MDM lookup endpoint is protected by both `[Authorize]` and `mdm.legal-entities.read`.

- Auth internal request headers: `services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Services/PlatformTenantLegalEntityScopeClient.cs:43-51`
- MDM request and caller-header forwarding: `services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/Mdm/MdmLegalEntityReferenceValidator.cs:104-109,142-151`
- Protected MDM endpoint: `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/LegalEntitiesController.cs:11,48-50`

The isolated MDM contract mock received no request because F1 prevented the resolver from reaching revalidation. F2 is therefore a source-proven latent blocker, not a claimed end-to-end reproduction.

## Observed token result

Real Auth login and refresh both returned HTTP 200, but the redacted decoded claims contain `legal_entity_id: null` (`raw-evidence.tar.gz::raw/runtime-evidence/auth-issued-safe-claims.json`). The evidence stores no bearer token or signing secret.

## Required rework boundary

The exact approved ten-path patch cannot be accepted as implemented. A successor needs two separately reviewable changes:

1. establish tenant context only after authenticated internal-request validation and before tenant-filtered resolver access, without trusting a client-selected Legal Entity or weakening the global internal-path policy;
2. add an authorized server-to-server MDM lookup-validation seam usable during login and refresh, preserving fail-closed behavior.

The second item necessarily exceeds the approved ten Auth/Platform paths and requires explicit MDM/security ownership. No repair was made under this work package.
