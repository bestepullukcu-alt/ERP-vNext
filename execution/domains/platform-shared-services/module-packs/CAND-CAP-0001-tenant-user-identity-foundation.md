---
id: CAND-CAP-0001
name: Tenant User / Identity Foundation
domain: platform-shared-services
service: Diten.AuthService
shell: none
golden_reference: none
entity_base: EntityBase
status: done
owner: platform-team
branch: feature/pss/mod-0047-tenant-user-foundation
started: ""
target: ""
form_field_count: 0
---

# CAND-CAP-0001 — Tenant User / Identity Foundation

> **Canonicalization (DCP-002):** Governance identity is now the temporary candidate capability **CAND-CAP-0001 (Tenant User / Identity Foundation)**. Prior repo ID **MOD-0047** is a deprecated alias (it squatted Blueprint MOD-0047 = Business Continuity). The enterprise Blueprint has no exact tenant-user identity capability; CAND-CAP-0001 is a temporary documentation/governance identity pending Enterprise Architect MOD-xxxx allocation and is never written into runtime literals. Body below predates canonicalization. Ref: `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`.

> **Promotion note:** `draft -> ready-for-dev`
>
> Reason: Minimal AuthService-owned Tenant User lookup-validation contract reviewed and explicitly approved.
> Route, auth model, permission key, tenant mismatch fail-closed policy, `IsActive` referenceability rule,
> minimal response shape, failure matrix, test expectations, protected paths, and deferred MOD-0040 integration
> boundary are locked.
>
> **Closure note:** `ready-for-dev -> done`
>
> Reason: The locked first slice is implemented and validated as the AuthService-owned read-only Tenant User
> lookup-validation contract. Build passed, 15 AuthService tests passed, strict read-only pre-commit scope audit
> passed, response leakage review passed, protected paths remained clean, and deferred integration boundaries remain
> intact.

## 1. Module Summary

MOD-0047 owns the Tenant User primitive in `Diten.AuthService`. The first delivery slice is a minimal,
read-only Tenant User lookup-validation contract that other services can consume without reading AuthService
persistence directly and without receiving user profile, role, permission, or claims data.

## 2. Ownership and Boundaries

**Owned by MOD-0047:**

- Tenant User aggregate governance in AuthService.
- AuthService-owned read-only Tenant User lookup-validation contract.
- Minimal referenceability response for downstream service validation.

**Not owned by MOD-0047 first slice:**

- Tenant User CRUD rewrite.
- Tenant Role.
- Role assignment.
- Permission assignment.
- Position-role binding.
- Permission evaluation.
- Frontend UI.
- Gateway route.
- `IDataScopeResolver`.
- Profile API changes, search endpoint, or bulk operations.
- Tenant user invitation pipeline.
- Global AuthService tenant middleware hardening.
- MOD-0040 Platform integration.

## 3. Existing AS-IS User Aggregate

Current code already contains the Tenant User aggregate:

- Aggregate owner: `Diten.AuthService`.
- Entity: `services/Diten.AuthService/src/Diten.AuthService.Domain/Entities/User.cs`.
- Canonical identifier: `User.Id`.
- Tenant ownership: `EntityBase.TenantId`.
- Technical soft delete marker: `IsDeleted` inherited from `GlobalEntityBase`.
- Business referenceability marker: `IsActive`.
- Existing repository lookup: `IUserRepository.GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct)`.

The existing public `GET /api/users/{id:guid}` endpoint must not be used as the validation contract because it
returns profile and role data through `UserDto`.

## 4. First Delivery Slice

The first delivery slice is backend-only and adds an AuthService-owned read-only lookup-validation contract:

```text
UserId exists
AND User.TenantId == current TenantId
AND IsDeleted == false
AND IsActive == true
```

The contract is intended for service-to-service validation. It does not rewrite Tenant User CRUD and does not add
frontend, gateway, or MOD-0040 Platform integration scope.

## 5. Validation Contract

Validation input:

```text
UserId from route
current TenantId from server-side tenant context
```

Validation behavior:

```text
UserId exists
AND User.TenantId == current TenantId
AND User.IsDeleted == false
AND User.IsActive == true
```

Cross-tenant, soft-deleted, and inactive users must fail closed. TenantId must not be accepted from route, query,
body, or DTO.

## 6. Minimal Return Shape

Return only:

```text
UserId
referenceable = true
```

Do not return:

```text
TenantId
email
first name
last name
profile
roles
permissions
claims
status details
```

TenantId is validated server-side through the resolved tenant context and must not be returned.

## 7. Repo Scope

Governance authoring scope:

```text
execution/domains/platform-shared-services/module-packs/CAND-CAP-0001-tenant-user-identity-foundation.md
execution/registries/module-id-registry.md
execution/portfolio/master-development-plan.md
execution/portfolio/delivery-capability-packs/DCP-001-access-governance.md
```

Implementation scope after ready-for-dev:

```text
services/Diten.AuthService/**
repo-standard Diten.AuthService test paths
```

## 8. Protected Paths

Protected unless explicitly approved by a later module/follow-up pack:

```text
services/Diten.Platform/**
services/Diten.MdmService/**
services/Diten.DevEnablementService/**
services/Diten.EnterpriseStrategyService/**
frontend/**
gateway/**
.antigravity/**
archive / frozen paths
```

`services/Diten.Platform/**` is protected for the first AuthService-owned validation contract slice. MOD-0040
integration is a separate follow-up.

## 9. Dependencies

- DCP-001 Access Governance is the orchestration context.
- MOD-0040 Tenant Organization Foundation shape must be locked before Tenant User / Tenant Role implementation work.
- MOD-0040 Position Assignment `UserId` validation integration depends on this validation contract and remains a
  separate follow-up slice.

## 10. Runtime Constraints

- AuthService remains the Tenant User owner.
- `Diten.Platform` must not reference AuthService persistence repositories or collections directly.
- TenantId must be resolved server-side from AuthService tenant context.
- Cross-tenant validation must fail closed.
- Soft-deleted users must fail closed.
- Inactive users must fail closed.
- JWT/header tenant mismatch must fail closed for this lookup-validation contract.
- Network, timeout, malformed response, and invalid response shape must fail closed for consumers.
- The implementation should prefer an endpoint-specific narrow tenant mismatch guard. Global AuthService
  `TenantResolutionMiddleware` hardening is out of scope unless a later audit/follow-up explicitly approves it.

## 11. Endpoint Contract

Locked route:

```text
GET /api/users/{userId:guid}/lookup-validation
```

The endpoint is not a public profile endpoint, not a CRUD endpoint, and not a search endpoint. It is a read-only
validation contract. It should be thin-controller, MediatR delegated, and return the standard AuthService
`Response<T>` envelope.

## 12. Authorization and Propagation Contract

Locked minimal v1 model:

```text
Bearer token forwarding
X-Tenant-Id propagation
[Authorize]
permission: auth.users.lookup-validation
```

Do not introduce a new internal API key framework or service identity framework in this slice.

Tenant propagation must use the repo-standard `X-Tenant-Id` flow where applicable. Authorization and tenant
propagation failures must fail closed.

TenantId must not appear in route, query, body, or DTO. If JWT `tenant_id` and `X-Tenant-Id` are both present and
their values differ, this lookup-validation contract must return `400 Bad Request` and fail closed.

## 13. Failure Paths

| Scenario | Endpoint result | Consumer behavior |
|---|---:|---|
| Invalid / empty `UserId` | 400 | fail-closed |
| Missing `UserId` | 404 | fail-closed |
| Cross-tenant `UserId` | 404 | fail-closed; no existence leak |
| Soft-deleted `UserId` | 404 | fail-closed |
| Inactive `UserId` | 404 | fail-closed |
| Missing tenant context | 400 | fail-closed |
| JWT/header tenant mismatch | 400 | fail-closed |
| Missing authentication | 401 | fail-closed |
| Missing permission | 403 | fail-closed |
| Network failure | consumer-side follow-up | fail-closed |
| Timeout | consumer-side follow-up | fail-closed |
| Malformed response | consumer-side follow-up | fail-closed |
| `UserId` mismatch | consumer-side follow-up | fail-closed |
| `referenceable != true` | consumer-side follow-up | fail-closed |

## 14. Acceptance Criteria

- AuthService-owned minimal read-only lookup-validation contract is defined.
- `User.Id` is locked as canonical identifier.
- TenantId is resolved from server-side tenant context.
- `UserId + TenantId + IsDeleted=false + IsActive=true` validation is implemented.
- Minimal return shape returns only `UserId` and `referenceable = true`.
- TenantId, email, names, profile, roles, permissions, claims, and status details are not returned.
- Existing public `GET /api/users/{id}` is not used as the validation contract.
- The endpoint requires `[Authorize]` and `auth.users.lookup-validation`.
- JWT/header tenant mismatch returns `400 Bad Request` for the lookup-validation contract.
- `Diten.Platform` does not bind directly to AuthService persistence.
- MOD-0040 integration is a separate follow-up slice.
- FU15/runtime guard remains explicit.
- Frontend and gateway are out of scope.

## 15. Test Expectations

AuthService implementation tests:

- Valid same-tenant active non-deleted user succeeds.
- Missing user returns 404.
- Cross-tenant user returns 404.
- Soft-deleted user returns 404.
- Inactive user returns 404.
- Invalid tenant context returns 400.
- JWT/header tenant mismatch returns 400.
- TenantId is absent from route, query, body, and DTO.
- Response contains only `UserId` and `referenceable`.
- Email, first name, last name, profile, roles, permissions, claims, TenantId, and status details do not leak.
- `[Authorize]` is present.
- `auth.users.lookup-validation` permission is required.
- Public `GET /api/users/{id}` is not used as the validation contract.

MOD-0040 consumer follow-up tests:

- Network failure fails closed.
- Timeout fails closed.
- Malformed JSON fails closed.
- Null payload fails closed.
- Invalid response shape fails closed.
- `UserId` mismatch fails closed.
- `referenceable == false` fails closed.
- Valid response succeeds.
- Authorization header is propagated.
- `X-Tenant-Id` is propagated.
- Caller cancellation is preserved.

## 16. Ready-for-dev Checklist

- [x] MOD ID reserved.
- [x] AuthService ownership locked.
- [x] `User.Id` canonical identifier locked.
- [x] Route locked.
- [x] Referenceability rule locked.
- [x] `IsActive == true` locked.
- [x] Minimal return shape locked.
- [x] No profile leakage locked.
- [x] Bearer forwarding locked.
- [x] `X-Tenant-Id` propagation locked.
- [x] `auth.users.lookup-validation` permission locked.
- [x] JWT/header mismatch fail-closed locked.
- [x] Endpoint-specific narrow guard scope locked.
- [x] MOD-0040 integration deferred.
- [x] FU15 runtime guard preserved.
- [x] Repo scope locked.
- [x] Protected paths locked.
- [x] Failure matrix locked.
- [x] AuthService test matrix locked.

## 17. Implementation Closure Evidence

Completed first slice:

```text
MOD-0047 Tenant User Foundation — read-only lookup-validation contract
```

Implemented endpoint:

```text
GET /api/users/{userId:guid}/lookup-validation
```

Implementation evidence:

- AuthService endpoint added in `services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/UsersController.cs`.
- Minimal response DTO added as `TenantUserLookupValidationDto` with only `UserId` and `Referenceable`.
- Tenant-isolated lookup delegates to existing `IUserRepository.GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct)`.
- `IsActive == true` referenceability enforcement is explicit in the query handler.
- Endpoint-specific JWT `tenant_id` / `X-Tenant-Id` mismatch guard returns `400 Bad Request`.
- Permission seed `auth.users.lookup-validation` added using the existing AuthService lowercase dotted convention.
- Minimal `Diten.AuthService.Application.Tests` xUnit project covers handler behavior, response leakage, endpoint surface,
  mismatch guard, and permission seed assertion.

Validation summary:

- `dotnet build services/Diten.AuthService/Diten.AuthService.sln -c Debug` — PASS.
- `dotnet test services/Diten.AuthService/Diten.AuthService.sln -c Debug --no-build` — PASS, 15 passed, 0 failed, 0 skipped.
- `git diff --check` — clean.
- Strict read-only pre-commit scope audit — PASS.
- Protected paths — clean.

## 18. Deferred Follow-ups

- MOD-0040 PositionAssignment `UserId` validation integration.
- Tenant User Directory CRUD / lifecycle reconciliation.
- Tenant Role Foundation pack and ID reservation.
- Position-role binding integration slice.
- Tenant User CRUD governance hardening.
- Tenant user invitation pipeline.
- Tenant IAM UI.
- Frontend UI.
- Gateway route.
- Search and bulk operations.
- Global AuthService `TenantResolutionMiddleware` hardening.
- Tenant LoginCommandHandler `IsActive` enforcement audit.
- `IDataScopeResolver` consumption after validation and MOD-0040 integration are complete.

## FU15 / Runtime Guard

MOD-0018-FU15 real `IDataScopeResolver` or any other runtime authorization consumer must not consume
MOD-0040 Position Assignment `UserId` as authoritative until the MOD-0047 Tenant User lookup-validation contract
and the MOD-0040 `PositionAssignment.UserId` validation integration are complete.
