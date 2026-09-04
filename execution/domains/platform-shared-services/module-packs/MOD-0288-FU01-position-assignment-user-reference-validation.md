---
id: MOD-0288-FU01
name: Position Assignment User Reference Validation
domain: platform-shared-services
service: Diten.Platform
owner: platform-shared-services
entity_base: BaseEntity
status: done
branch: feature/pss/mod-0040-fu01-position-assignment-user-reference-validation
shell: none
golden_reference: none
started: ""
target: ""
form_field_count: 0
---

# MOD-0288-FU01 - Position Assignment User Reference Validation

> **Canonicalization (DCP-002):** Canonical ID is now **MOD-0288-FU01**, the child follow-up of **MOD-0288 Organization, Person & Position Directory** (formerly MOD-0040). Prior repo ID **MOD-0040-FU01** is a deprecated alias retained for traceability. Body text below predates canonicalization and references MOD-0040 as the parent; the parent is now MOD-0288. Scope and meaning are unchanged. Ref: `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`.

## 1. Module Summary

MOD-0040-FU01 is a narrow backend-only integration follow-up for MOD-0040 Tenant Organization Foundation.
It validates `PositionAssignment.UserId` in the Platform create/update flows through the AuthService Tenant User
lookup-validation contract completed by MOD-0047:

`GET /api/users/{userId:guid}/lookup-validation`

The Platform service must not accept or persist `PositionAssignment.UserId` as authoritative unless remote
AuthService validation succeeds with matching `UserId` and `Referenceable == true`.

This pack authorizes implementation planning only while `status: ready-for-dev`; production implementation still
requires an explicit orchestrator handoff on the governed feature branch.

> **Promotion note:** `draft -> ready-for-dev`
>
> Reason: draft pack review completed; frontmatter reconciliation completed; scope and dependency review passed.
> Promotion is approved for implementation planning only. Backend implementation still requires an explicit
> orchestrator handoff on the governed feature branch.

## 2. Ownership and Boundaries

Owned integration surface:

- Platform-side `IUserReferenceValidator` contract.
- AuthService typed HTTP client implementation.
- Dependency injection registration.
- PositionAssignment create handler integration.
- PositionAssignment update handler integration.
- Fail-closed response validation.
- Tenant header propagation.
- Bearer forwarding.
- Cancellation preservation.
- Focused tests.

This follow-up owns no AuthService endpoint, no AuthService persistence, no frontend, no gateway route, and no
new cross-service framework.

## 3. Owned Objects

No new aggregate is owned. This follow-up owns only the Platform consumer integration for an external Tenant User
reference.

## 4. Repo Scope

Expected future create paths:

- `services/Diten.Platform/src/Diten.Platform.Application/Features/TenantOrganization/Services/IUserReferenceValidator.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/Auth/AuthServiceUserReferenceValidator.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/TenantOrganization/AuthServiceUserReferenceValidatorTests.cs`

Expected future modify paths:

- `services/Diten.Platform/src/Diten.Platform.Application/Features/TenantOrganization/Handlers/CommandHandlers/CreatePositionAssignmentCommandHandler.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/TenantOrganization/Handlers/CommandHandlers/UpdatePositionAssignmentCommandHandler.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/DependencyInjection.cs`
- Existing Platform tenant-organization tests, if needed, to cover create/update handler integration.

These paths follow the existing MOD-0040 MDM Legal Entity validation pattern:
`ILegalEntityReferenceValidator`, `MdmLegalEntityReferenceValidator`, `MdmServiceOptions`,
`TenantOnTheWire`, typed `HttpClient` registration, bearer forwarding, `X-Tenant-Id` propagation,
fail-closed mapping, malformed response handling, network failure handling, and caller cancellation preservation.

## 5. Protected Paths

- `services/Diten.AuthService/**` - the MOD-0047 contract is consumed, not changed.
- `services/Diten.Platform.Common/**` - no shared framework change.
- `services/Diten.MdmService/**`, `services/Diten.DevEnablementService/**`, `services/Diten.EnterpriseStrategyService/**`.
- `frontend/**` and `gateway/**`.
- `.antigravity/**`, `archive/**`, `docs/**`, `execution/delivery/**`.
- Other module pack files unless a separate governance task explicitly authorizes them.

## 6. Dependencies

- MOD-0040 Tenant Organization Foundation - done / merged as the Platform Position Assignment source.
- MOD-0047 Tenant User Foundation lookup-validation first slice - done / merged as the AuthService contract.
- Existing Platform MDM Legal Entity typed HTTP client pattern.
- Existing tenant propagation and bearer forwarding pattern.

AuthService referenceability requires:

- User exists.
- `User.TenantId == current TenantId`.
- `IsDeleted == false`.
- `IsActive == true`.

AuthService success response contains only:

- `UserId`
- `Referenceable = true`

## 7. Runtime Constraints

- Platform must not connect directly to AuthService persistence.
- User validation must use the AuthService HTTP lookup-validation contract.
- Remote validation is fail-closed.
- If remote validation fails, times out, returns malformed content, returns mismatched `UserId`, or returns
  `Referenceable != true`, the Position Assignment write is rejected and the `UserId` is not persisted.
- Missing, cross-tenant, soft-deleted, and inactive users must not produce existence leakage.
- Caller cancellation must remain cancellation, not be converted to business rejection.
- `X-Tenant-Id` must be written by the calling class itself, from the request scope, using `TenantOnTheWire`
  to decide which tenant may travel. **⚠ Corrected 2026-08-29 (BL-316).** This line used to say "existing
  `TenantPropagationHandler` behavior should be reused". That was wrong and cost a round: `IHttpClientFactory`
  caches a client's handler chain in its OWN scope, so a `DelegatingHandler` injecting the request-scoped
  `ITenantContext` holds an instance belonging to no request, answers `IsResolved == false`, and adds no header
  — silently. The handler has been deleted from all three services. Do not reintroduce one.
- Bearer token forwarding should mirror the existing MDM Legal Entity validator pattern.

## 8. Locked Behavior

Create and update PositionAssignment flows:

- valid same-tenant active user -> allow
- missing user -> reject
- cross-tenant user -> reject
- soft-deleted user -> reject
- inactive user -> reject
- AuthService non-success -> reject
- timeout -> reject
- network failure -> reject
- malformed JSON -> reject
- response `UserId` mismatch -> reject
- `Referenceable != true` -> reject
- caller cancellation -> preserve cancellation semantics
- `X-Tenant-Id` -> propagate
- bearer token -> forward

## 9. Layout and Shell Contract

`shell: none`. This is a backend-only integration slice, not a frontend DataTable or form module.

## 10. Backend File Convention

Follow existing Diten.Platform five-layer conventions and the current MOD-0040 folder shape. Reuse the typed HTTP
client style already used for the MDM Legal Entity reference validator. Do not introduce a new cross-service
framework, API key framework, or shared Platform.Common abstraction.

## 11. Frontend File Contract

No frontend files are in scope. `golden_reference: none` and `form_field_count: 0` are intentional.

## 12. Validation Rules

- `PositionAssignment.UserId` remains required.
- Required Guid validation remains in the existing request validator.
- Runtime referenceability is enforced by AuthService lookup-validation before create/update persistence.
- A failed validation blocks the write.

## 13. Failure Path

- AuthService returns non-success -> reject fail-closed.
- HTTP timeout or network failure -> reject fail-closed.
- Malformed JSON or unexpected envelope -> reject fail-closed.
- Response `UserId` differs from requested `UserId` -> reject fail-closed.
- Response `Referenceable` is false or missing -> reject fail-closed.
- Caller cancellation token is cancelled -> preserve cancellation.

## 14. Authorization Convention

No new Platform permission is proposed. Existing Position Assignment create/update authorization remains
responsible for caller authorization. The remote AuthService endpoint requires `auth.users.lookup-validation`,
and bearer forwarding must satisfy that endpoint contract.

## 15. Gateway Routing

No gateway route is required. This is service-to-service Platform -> AuthService HTTP consumption using internal
service configuration and existing propagation patterns.

## 16. Acceptance Criteria

1. Create PositionAssignment with a valid active same-tenant user succeeds.
2. Update PositionAssignment with a valid active same-tenant user succeeds.
3. Missing user is rejected.
4. Cross-tenant user is rejected.
5. Soft-deleted user is rejected.
6. Inactive user is rejected.
7. AuthService non-success is rejected.
8. Timeout is rejected.
9. Network failure is rejected.
10. Malformed JSON is rejected.
11. Response `UserId` mismatch is rejected.
12. `Referenceable == false` is rejected.
13. Caller cancellation is preserved.
14. `X-Tenant-Id` propagation is verified.
15. Bearer forwarding is verified.
16. No direct AuthService persistence dependency exists.
17. Frontend, gateway, and Platform.Common are unchanged.
18. FU15 guard remains in force.

## 17. Test Expectations

- Platform build.
- Platform test suite.
- Typed HTTP client tests.
- Create/update handler integration tests.
- Network failure tests.
- Malformed payload tests.
- Response mismatch tests.
- Cancellation tests.
- Tenant header propagation test.
- Bearer forwarding test.
- `git diff --check`.
- Protected-path verification.

## 18. Ready-for-dev Checklist

- [ ] User reviewed this draft pack.
- [ ] Registry reservation exists for `MOD-0040-FU01`.
- [ ] DCP-001 sequence places this follow-up after MOD-0047 and before MOD-0018-FU15.
- [ ] Branch name confirmed: `feature/pss/mod-0040-fu01-position-assignment-user-reference-validation`.
- [ ] Scope excludes AuthService endpoint changes, frontend, gateway, and Platform.Common.
- [ ] Existing MDM Legal Entity HTTP validation pattern is accepted as the implementation reference.

## 19. Implementation Notes

MOD-0040 already validates Position existence and interval overlap before writing Position Assignments. This
follow-up should add User reference validation before the assignment is persisted or updated. The implementation
should be as small as the existing MDM Legal Entity reference validator, with a DTO that contains only fields
needed to validate the AuthService response.

## 20. Follow-up Items

- MOD-0018-FU15 real `IDataScopeResolver` remains blocked until this integration is complete.
- Tenant Role read-only validation contract.
- Position-role assignment integration.
- Tenant User CRUD / lifecycle reconciliation.
- User Directory frontend.
- Invite User pipeline.
- Gateway and frontend surfaces remain deferred.
- Global TenantResolutionMiddleware hardening remains separate.
- Tenant LoginCommandHandler IsActive enforcement audit remains separate.
- Documentation drift cleanup remains separate: MOD-0220 stale master-plan wording, AGENTS.md stale MDM scaffold
  wording, and domain-config stale master-plan reference.
