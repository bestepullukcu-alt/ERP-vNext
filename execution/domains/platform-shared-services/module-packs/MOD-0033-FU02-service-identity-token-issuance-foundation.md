---
id: MOD-0033-FU02
name: Service Identity Token Issuance Foundation
domain: platform-shared-services
service: Diten.AuthService
shell: none
golden_reference: none
entity_base: GlobalEntityBase
status: review
owner: api-consumer-credential-owner / auth-security-owner / platform-owner
branch: feature/pss/mod-0033-fu02-service-identity-token-issuance-foundation
started: 2026-08-28
target: 2026-09-04
form_field_count: 0
parent_module: MOD-0033
consumers: MOD-0021-FU01 / MOD-0290
---

# MOD-0033-FU02 — Service Identity Token Issuance Foundation

> **Implementation evidence.** The user approved the Phase 1.5 architecture, promotion to `ready-for-dev`, creation of
> an isolated current-remote-main worktree and Section 5 A followed by B runtime code-start on 2026-08-28. Both
> code-start steps are implemented and independently security-reviewed; this pack is now `review`.
> Operational configuration, secret, credential, tenant-grant, data, Production/Staging, commit and push mutations
> remain unauthorized.
>
> **DCP-002 proof.** Master 8.1 `Blueprint_Data!A34:AG34` identifies parent MOD-0033 as `API Consumer & Credential
> Management (Developer Portal)` and assigns API consumers/apps, credential metadata, credential issuance and
> subscriptions/grants to it. `SoR_Map!A2:E2` maps `API consumers/apps` to MOD-0033 with collision count zero.
> `verify_module_id.py . --check-id MOD-0033-FU02 --name "Service Identity Token Issuance Foundation" --parent MOD-0033`
> returned `OK MOD-0033-FU02: proven against Blueprint/registry`; pre-mutation registry/pack collision search was empty.

## 1. Module Summary

FU02 supplies short-lived Auth-issued service tokens for trusted server-to-server consumers. The first bounded use is
`Diten.MDM` calling MOD-0021-FU01 trusted audit ingestion for one granted tenant and audience. It is a prerequisite for
MOD-0290/FU03 H1b durable audit delivery, not a user-login token extension.

## 2. Ownership and Boundaries

- MOD-0033 owns service client identity, credential lifecycle, tenant/audience grants and issuance policy.
- AuthService stores credential hashes and signs tokens; it is the technical issuer, not the audit business owner.
- Platform validates issuer public keys and token claims; MOD-0021-FU01 still owns endpoint/source authorization and audit semantics.
- MOD-0290 owns its MDM token client, caching and audit delivery retry. Those files are outside this pack.
- Canonical drift is explicit: the existing parent pack/domain prose emphasizes `Consumer / Quota Model`, while Master
  8.1 assigns the broader API consumer and credential SoR to MOD-0033. Parent reconciliation is a follow-up; no parent
  pack or runtime literal is changed here.

## 3. Owned Objects

- `ServiceClientIdentity`: global service identity, exact service name, active/previous credential hashes, overlap and revocation state.
- `ServiceClientTenantGrant`: tenant-scoped exact audience grant for one client identity.
- `ServiceIdentityAccessToken`: ephemeral signed response; never persisted as plaintext.
- Signing-key metadata: active/previous `kid` and public verification material; private key remains Auth-only.

## 4. Entity Fields

`ServiceClientIdentity : GlobalEntityBase` contains `ClientCode`, `ServiceName`, active credential hash/version,
previous hash/version, `PreviousValidUntilUtc`, `IsRevoked` and audit metadata. `ServiceClientTenantGrant : EntityBase`
contains `ServiceClientIdentityId`, exact `Audience`, `IsEnabled` and audit metadata. Unique active keys are client code
and `(TenantId, ServiceClientIdentityId, Audience)`. Raw secrets and access tokens are never stored or logged.

## 5. Repo Scope

### A — Auth identity, grant and issuance (separate code-start)

- `services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/Internal/InternalServiceIdentityTokensController.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Api/Security/ServiceIdentityTokenRequestParser.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Application/Features/ServiceIdentityTokens/**`
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IServiceIdentityTokenIssuer.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IServiceClientCredentialVerifier.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Domain/Entities/ServiceClientIdentity.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Domain/Entities/ServiceClientTenantGrant.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Domain/Repositories/IServiceClientIdentityRepository.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Domain/Repositories/IServiceClientTenantGrantRepository.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/ServiceClientIdentityRepository.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/ServiceClientTenantGrantRepository.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Persistence/Configurations/MongoDbIndexConfigurations.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Services/ServiceIdentityTokenIssuer.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Services/ServiceClientCredentialVerifier.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Settings/ServiceIdentityTokenIssuerOptions.cs`
- Auth `DependencyInjection.cs` registration files only where code truth requires them.
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/ServiceIdentityTokens/**`

### B — Platform asymmetric validator foundation (separate code-start)

- `services/Diten.Platform/src/Diten.Platform.API/Configuration/TrustedServiceTokenValidationOptions.cs`
- `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedServiceTokenValidationExtensions.cs`
- `services/Diten.Platform/src/Diten.Platform.API/Program.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Security/TrustedServiceTokenValidationTests.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/DependencyInjectionSmokeTests.cs`

## 6. Protected Paths

MDM, Gateway, frontend, MOD-0021-FU01 controller/handler, user-token `ITokenService`/`TokenService`, shared
`JwtSettings.Secret`, appsettings, secret stores, production data, `.antigravity/**`, parent pack and every file not
listed in the approved Section 5 step are protected. No direct Mongo write or startup auto-provisioning is allowed.

## 7. Dependencies

Master 8.1 MOD-0033 ownership, existing Auth JWT infrastructure only as a non-reuse reference, MOD-0021-FU01 audit
contract, MOD-0290 H1b consumer plan, MongoDB and platform tenant context. Operational enablement additionally needs
owner-approved client identity, tenant grant, audience, signing keys and rotation runbook.

## 8. Runtime Constraints

- Endpoint: `POST /api/internal/v1/auth/service-tokens/issue`.
- Headers: exact `X-Service-Client-Id` and `X-Service-Client-Secret`; body: only `tenantId` and `audience`.
- First exact pair: `service_name=Diten.MDM`, `aud=TRUSTED_AUDIT_SOURCE_INGEST`.
- RS256 only, active Auth private key and mandatory `kid`; Platform receives current/previous public keys only.
- Claims: `iss`, `aud`, `sub`, `actor_type=service`, `service_name`, exactly one `tenant_id`, `jti`, `iat`, `nbf`, `exp`.
- TTL exactly 300 seconds; no refresh token. Maximum request body 1 KiB and total issuance budget two seconds.
- Previous client credential is valid only strictly before `PreviousValidUntilUtc`; revocation rejects both credentials.
- Signing-key rotation is independent of client credential rotation. Unknown `kid`, HS256, `none` and algorithm confusion fail closed.
- Shared signing secrets, user JWT fallback, roles/permissions/email claims, tenant headers and hardcoded grants are forbidden.

## 9. Layout & Shell Contract

Backend-only. `shell: none`, `golden_reference: none`, `form_field_count: 0`; no navigation or browser surface.

## 10. Backend File Convention

Five-layer CQRS boundaries remain mandatory. Controllers parse strict transport input and send one command. Domain
entities enforce lifecycle invariants; repositories own tenant/soft-delete predicates; infrastructure owns hashing and
RS256 signing. `TimeProvider` controls expiry and overlap boundary tests.

## 11. Frontend File Contract

None. A developer portal, client registration UI or secret-display page requires a later separately approved slice.

## 12. Validation Rules

Reject missing/duplicate/unknown JSON fields, empty GUID, non-exact audience, disabled/missing tenant grant, revoked
client, invalid active/previous secret, expired overlap, invalid issuer/key configuration and body over budget. Compare
credential hashes in fixed time. The requested tenant becomes authoritative only after persisted grant validation.

## 13. Failure Path to Verify

- `400`: malformed headers/body, duplicate/unknown fields or invalid tenant/audience syntax.
- `401`: missing, wrong, expired or revoked client credential.
- `403`: service/audience mismatch or missing/disabled tenant grant.
- `409`: persisted identity/grant/key version drift or uniqueness conflict.
- `413`: request exceeds 1 KiB.
- `503`: issuer/key/grant repository configuration unavailable or inconsistent.
- `504`: two-second issuance budget exceeded. Cancellation propagates unchanged.

## 14. Authorization Convention

This endpoint does not accept user JWT authority. Client authentication proves service identity; the persisted
tenant/audience grant authorizes issuance. The resulting token grants only the named audience for one tenant and does
not grant UI permissions, roles, entitlements or arbitrary Platform access. MOD-0021-FU01 independently revalidates
signature, issuer, audience, service and tenant/source mapping.

## 15. Gateway / API Routing Decision

The issuance endpoint is internal and is not added to Ocelot or browser routes. Any deployment routing/mTLS decision is
a separate security-owner gate. Platform public-key validation is local middleware/authentication configuration, not a
new public endpoint or JWKS surface in this foundation.

## 16. Acceptance Criteria

1. Exact registered client + active credential + enabled tenant/audience grant returns one RS256 token valid for 300 seconds.
2. Token contains only the frozen service claims and no user authorization claims.
3. Active/previous credential and signing-key rotation obey exact overlap boundaries and revocation.
4. Cross-tenant, wrong-audience, unknown-client and algorithm-confusion attempts fail closed without leakage.
5. Platform accepts current/overlap-valid previous public key and rejects unknown/expired `kid`.
6. Existing human token issuance remains byte/claim behavior compatible; shared HMAC is not reused.
7. No startup provisioning, plaintext secret persistence/logging, Gateway/frontend or MDM mutation occurs.

## 17. Test Expectations

- Unit: claims, exact TTL, strict parser, fixed-time verification, active/previous boundaries, revocation and failure mapping.
- Security contract: RS256 signature, `kid`, issuer/audience/tenant cardinality, HS256/`none` confusion rejection and no user claims.
- Real Mongo: unique client/grant indexes, tenant A/B isolation, soft-delete behavior, replay and concurrent rotation fencing.
- Integration: Auth-issued token validates in Platform with current/previous public key; wrong tenant/audience/service/key fails.
- Regression: full Auth and Platform suites and Release builds; no skipped/fake/in-memory substitute for named Mongo tests.

## 18. Ready-for-dev Checklist

- [x] API Consumer/Credential ownership and the MOD-0033 follow-up boundary were accepted for this foundation.
- [x] RS256, exact claims, 300-second TTL and independent client/signing-key rotation models were accepted.
- [x] `Diten.MDM` / `TRUSTED_AUDIT_SOURCE_INGEST` was frozen as the first bounded pair.
- [x] Entity/index shapes and the global-identity plus tenant-scoped grant boundary were approved.
- [x] Pack was promoted to `ready-for-dev` before runtime work.
- [x] User separately authorized Section 5 A and then B code-start.

## 19. Implementation Notes

Current Auth `TokenService` emits human tenant/platform tokens using HS256 and `JwtSettings.Secret`; it must not be
extended for service identity. Existing Platform active/previous credential authenticators are behavioral references,
not authority or code-reuse mandates. Persist hashes only; secret generation/provisioning/rotation/revoke is a separate
supported operational step and must never be direct Mongo insertion.

Implementation evidence on the isolated `feature/pss/mod-0033-fu02-service-identity-token-issuance-foundation`
worktree, based on remote-main commit `c2cc8e10dcfc54b08f21cd258bc63e4a33449824`:

- Section A adds the strict internal issuance endpoint, global service-client identity, tenant/audience grant,
  salted/versioned PBKDF2-SHA256 credential verification, RS256 issuer and current/previous credential lifecycle.
- Section B adds a separate named Platform `TrustedServiceToken` JwtBearer scheme with current/previous public-key
  validation. Existing human/default authentication remains unchanged.
- Focused Auth issuance and real-Mongo tests: `47/47`; full AuthService: `393/393`; Auth architecture/DB-010: `7/7`.
- Focused Platform validator and DI tests: `25/25`; full Platform: `2738/2738`.
- Auth and Platform API Release builds completed with zero errors. Reported warnings were pre-existing and outside
  this follow-up (`1` Auth Mongo GUID warning; `13` Platform warnings).
- Independent adversarial reviews found and closed dummy-secret authentication, partial previous-credential state,
  repository/issuer failure mapping, caller-cancellation and exact temporal-claim gaps. Final review found no P0/P1.
- A direct cross-project A-issuer-to-B-validator executable test would require a test-project reference outside the
  approved allow-list. Wire compatibility is proven from the frozen claim/signature contract and both focused suites;
  the live cross-service proof remains part of the separately gated operational onboarding.
- `git diff --check` is clean. No appsettings, secret, credential, tenant grant, business data, startup provisioning,
  commit or push mutation was performed.

## 20. Follow-up Items

1. Reconcile MOD-0033 parent pack/domain prose with Blueprint 8.1 canonical ownership without renaming runtime literals.
2. Add separately gated supported service-client/grant provisioning and rotation runbook; then perform Local Development onboarding.
3. Revise MOD-0021-FU01 to consume this Platform public-key validator and run live audit ingestion smoke.
4. Implement the MOD-0290 MDM token client/cache and H1b durable audit delivery under its own exact allow-list.
5. Production/Staging signing keys, public-key distribution, credential rotation, monitoring and incident runbook remain separate gates.
