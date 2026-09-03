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
branch: feature/pss/mod-0033-fu02-workflow-audience
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
> **Planning authorization — 2026-08-30.** The user authorized Phase 1.5 planning for the planning-only named step
> `Service Client Operational Provisioning & Rotation`. Section 5 E freezes its proposed runtime/test allow-list and
> operational contract. Runtime code-start, Local Development execution and every credential/config/data mutation
> remained closed at that planning checkpoint and required separate explicit authorization.
>
> **Section 5 E runtime authorization — 2026-08-30.** The user explicitly authorized the exact Section 5 E runtime/test
> allow-list, Local Development operational provisioning and the Product/Item/SKU live WorkCenter acceptance sequence.
> Local commit is authorized; push and Production/Staging remain prohibited. The pack was therefore `ready-for-dev` for
> Section 5 E only. Local Development mutation may start only after the implementation and mandatory tests are green.
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

### C — Dedicated trusted Workflow consumer audience (named hardening step, 2026-08-29)

This additive step closes the code-truth gap discovered before the Product Identity Workflow consumer started:
Platform validates `TRUSTED_WORKFLOW_CONSUMER`, while Auth previously issued only
`TRUSTED_AUDIT_SOURCE_INGEST`. It does not weaken the audit audience or create a wildcard audience.

Exact runtime allow-list:

- new `services/Diten.AuthService/src/Diten.AuthService.Application/Features/ServiceIdentityTokens/ServiceIdentityTokenAudiencePolicy.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Domain/Entities/ServiceClientIdentity.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Application/Features/ServiceIdentityTokens/Validators/IssueServiceIdentityTokenValidator.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Application/Features/ServiceIdentityTokens/Handlers/CommandHandlers/IssueServiceIdentityTokenHandler.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Services/ServiceIdentityTokenIssuer.cs`

Exact test allow-list:

- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/ServiceIdentityTokens/ServiceIdentityTokenHandlerTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/ServiceIdentityTokens/ServiceIdentityTokenIssueTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/ServiceIdentityTokens/ServiceIdentityTokenSecurityContractTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/ServiceIdentityTokens/ServiceIdentityTokenMongoTests.cs`

Only two exact service/audience pairs are valid: `Diten.MDM` with `TRUSTED_AUDIT_SOURCE_INGEST`, and
`Diten.MDM` with `TRUSTED_WORKFLOW_CONSUMER`. Every other audience or service remains fail-closed. The Workflow
audience requires its own persisted service-client identity, credential lifecycle and exact tenant/audience grant at
operational onboarding; the audit client credential may not be reused. `ServiceClientIdentity.AllowedAudience` is a
single exact purpose binding: legacy records without the field remain audit-only, while Workflow issuance requires
the exact Workflow value. One identity can therefore never issue both audience tokens even if two grants are
misprovisioned. Malformed persisted purpose is inconsistent state and fails closed. This named step changes no
repository, index, endpoint, parser, claim, TTL, signing key, configuration, secret or data. Operational
identity/grant creation remains separately gated.

Acceptance requires exact-audience validation at the parser/validator, identity-purpose, handler and issuer layers;
a valid enabled Workflow grant; audit regression; same-identity dual-grant denial;
wrong/missing/disabled/cross-tenant grant denial; exact 300-second RS256 claims;
real-Mongo identity/grant isolation; full Auth regression and Release build. The standing non-push user authorization
grants this exact named-step code-start on 2026-08-29.

### E — Service Client Operational Provisioning & Rotation (Phase 1.5 planning only, 2026-08-30)

This named step supplies the missing supported operator path for service-client identity and tenant/audience grant
lifecycle. It is not a public or browser administration API, does not extend token issuance authority and does not
authorize an operational run. It replaces neither Vault/secret-manager ownership nor a Production runbook.

#### Proposed exact runtime allow-list

- `services/Diten.AuthService/src/Diten.AuthService.Api/Program.cs`
- new `services/Diten.AuthService/src/Diten.AuthService.Api/Configuration/ServiceClientOperationalProvisioningOptions.cs`
- new `services/Diten.AuthService/src/Diten.AuthService.Api/Services/ServiceIdentityTokens/DevelopmentServiceClientOperationalProvisioningEligibility.cs`
- new `services/Diten.AuthService/src/Diten.AuthService.Api/Services/ServiceIdentityTokens/ServiceClientOperationalActorAuthorizer.cs`
- new `services/Diten.AuthService/src/Diten.AuthService.Api/Services/ServiceIdentityTokens/ServiceClientOperationalProvisioningRunner.cs`
- new `services/Diten.AuthService/src/Diten.AuthService.Api/Services/ServiceIdentityTokens/ServiceClientSecretOutputSink.cs`
- new `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IServiceClientOperationalProvisioningEligibility.cs`
- new `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IServiceClientOperationalActorAuthorizer.cs`
- new `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IServiceClientSecretOutputSink.cs`
- new `services/Diten.AuthService/src/Diten.AuthService.Application/Features/ServiceIdentityTokens/Operational/ServiceClientOperationalProvisioningModels.cs`
- new `services/Diten.AuthService/src/Diten.AuthService.Application/Features/ServiceIdentityTokens/Operational/ServiceClientOperationalProvisioningService.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Application/DependencyInjection.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IServiceClientCredentialVerifier.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/DefaultRolePermissionTemplate.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Domain/Entities/ServiceClientIdentity.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Domain/Entities/ServiceClientTenantGrant.cs`
- new `services/Diten.AuthService/src/Diten.AuthService.Domain/Entities/ServiceClientOperationalProvisioningOperation.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Domain/Repositories/IServiceClientIdentityRepository.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Domain/Repositories/IServiceClientTenantGrantRepository.cs`
- new `services/Diten.AuthService/src/Diten.AuthService.Domain/Repositories/IServiceClientOperationalProvisioningOperationRepository.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/ServiceClientIdentityRepository.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/ServiceClientTenantGrantRepository.cs`
- new `services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/ServiceClientOperationalProvisioningOperationRepository.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Persistence/Configurations/MongoDbIndexConfigurations.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Persistence/DependencyInjection.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Infrastructure/DependencyInjection.cs`

#### Proposed exact test allow-list

- new `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/ServiceIdentityTokens/ServiceClientOperationalProvisioningEligibilityTests.cs`
- new `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/ServiceIdentityTokens/ServiceClientOperationalProvisioningRunnerTests.cs`
- new `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/ServiceIdentityTokens/ServiceClientOperationalActorAuthorizationTests.cs`
- new `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/ServiceIdentityTokens/ServiceClientOperationalProvisioningMongoTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/ServiceIdentityTokens/ServiceIdentityTokenDependencyInjectionTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/ServiceIdentityTokens/ServiceIdentityTokenHandlerTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/ServiceIdentityTokens/ServiceIdentityTokenMongoTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/ServiceIdentityTokens/ServiceClientCredentialRotationTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/DefaultRolePermissionTemplateTests.cs`

No glob beyond the exact files above is authorized. Appsettings, launch settings, public/internal HTTP
controllers, token parser/issuer, Gateway, Platform, MDM, frontend, `.antigravity/**`, registry and operational data are
outside this planning step. Runtime implementation may begin only after a separate exact Section 5 E code-start.

#### Frozen operational contract

- Invocation is an explicit one-shot CLI mode selected by exact `--service-client-operational-run`; it is never an
  `IHostedService`, startup seed, migration side effect or HTTP endpoint. Normal Auth startup performs no provisioning.
- Supported single-target operations are exactly: `read-identity`, `create-identity`, `rotate-credential`,
  `revoke-identity`, `read-grant`, `enable-grant` and `disable-grant`. One invocation performs one operation against one
  exact identity or one exact `(TenantId, IdentityId, Audience)` grant. Batch/wildcard/all-tenant operations are forbidden.
- `create-identity` binds one normalized `ClientCode`, exact `ServiceName` and exactly one `AllowedAudience` accepted by
  `ServiceIdentityTokenAudiencePolicy`; an identity cannot be broadened to a second purpose. Client code normalization
  is frozen before fingerprinting and persisted read-back; trim/case/alias fallback during lookup is forbidden.
- `create-identity` and `rotate-credential` generate at least 256 bits with `RandomNumberGenerator`, hash through the
  existing `IServiceClientCredentialVerifier.Hash` contract and persist only the hash. Operator-supplied raw secrets,
  deterministic secrets and secret values in arguments/environment/configuration/files are forbidden.
- Every mutation carries exact `CommandId`, canonical SHA-256 `CommandFingerprint`, `ExpectedOperationalVersion` and
  authorized actor identity. A dedicated durable operation record reserves the unique command/fingerprint, records
  sanitized immutable facts, state/checkpoints and a bounded append-only evidence trail; it never contains a secret,
  credential hash or JWT. Target documents retain technical `OperationalVersion` and last operation identity. Same
  command plus same fingerprint is stable replay; same command plus different fingerprint, stale version or purpose
  drift returns `409` before mutation. Atomic Mongo filters increment the version; whole-document replacement and
  read-then-unfenced write are forbidden.
- Create uniqueness races resolve by re-read: exact same command/fingerprint returns replay, any differing payload fails
  `409`. Rotation atomically moves active hash/version to previous, installs the new active hash/version and uses the
  fixed 300-second overlap dictated by the token TTL; callers cannot extend it. Concurrent rotation has one winner; the
  loser re-reads and returns replay or conflict.
- `revoke-identity` atomically sets revoked state and invalidates active/previous authentication without deleting the
  identity. A revoked identity cannot be re-enabled by this foundation; replacement requires a separately identified
  client or later owner-approved recovery policy. Revoke and grant disable stop fresh issuance immediately; already
  issued JWTs remain valid for at most their remaining 300-second TTL and are not claimed to be instantly revoked.
- Grant enable/disable is tenant-bound and exact-audience-bound. Enable creates or atomically restores the one active
  tuple; disable never deletes it. Missing identity, revoked identity, service/audience-purpose mismatch and cross-tenant
  access fail closed. Read-back must prove exact persisted ID, tenant, purpose, enabled/revoked state, operational version
  and last command fingerprint before success is returned.
- The mutation sequence is operation reserve, atomic target mutation, exact read-back, sanitized append-only evidence
  append and `COMPLETED`. An evidence failure cannot return false success: the operation remains `RECOVERY_REQUIRED` and
  the same command resumes from its persisted checkpoint without repeating the business mutation.
- A newly generated raw secret crosses exactly one injected `IServiceClientSecretOutputSink` boundary after verified
  persistence read-back and completed evidence. The CLI sink writes once to an inherited, pre-opened one-shot OS pipe;
  it refuses files, stdout, stderr, logs and repository/worktree paths. The parent process consumes it only in memory.
  Replay and concurrent losers never re-emit a secret. If first delivery is lost, recovery is a new authorized rotation
  command; plaintext is never persisted for replay.
- Success/log/audit output contains only identity/grant IDs, client code, audience, versions, fingerprint, replay flag
  and secret-delivered boolean. Raw secret, credential hash, authorization marker and signing material are always redacted.
- CLI exit mapping is deterministic: `0` completed/exact replay, `2` contract/configuration, `3` actor authentication or
  authorization denial, `4` drift/idempotency/concurrency conflict, `5` persistence/evidence unavailable, `6`
  recovery-required and `130` cancellation. Exact replay reports `secretDisposition=not-reissued`; only the winning
  first create/rotation reports `issued-once` through the protected pipe.

#### Eligibility and actor authorization

- Eligibility is fail-closed unless `IHostEnvironment.IsDevelopment()`, options `Enabled=true`, the exact CLI mode is
  present, the requested operation is in the frozen list and all required immutable facts are supplied.
- A fresh human operator JWT is mandatory and is read from bounded stdin, never argv/environment/config/file. The
  complete UTF-8 envelope remains capped at 64 KiB and the `accessToken` field is independently capped at 32 KiB;
  this admits the measured 20,995-byte seeded SuperAdmin JWT while rejecting larger tokens before authorization or
  repository mutation. Normal
  Auth issuer/signature/audience/lifetime validation applies; the actor must be active, `pwd_change_required=false`,
  exact `actor_type=platform_admin`, exact Platform System Tenant and hold the dedicated
  `auth.service-clients.provision` permission. Tenant/partner actors and JWT-claim-only stale users fail closed. The new
  permission is cataloged as Platform-scoped and granted only through existing explicit platform-role governance; it is
  never inferred from local process access or a broader RBAC permission.
- An opaque one-shot operational marker is an additional eligibility factor, not a replacement for the JWT permission.
  It is supplied through the same bounded stdin envelope and compared in fixed time with a process-injected hash; neither
  value is logged or returned. The authorized actor is written into target audit metadata.
- Options are default-disabled and bind only immutable operational eligibility facts; operation payload, JWT, raw marker
  and secret pipe are supplied through the bounded process-local invocation contract. Product appsettings files are not
  authorized; a later Local Development run must inject eligibility facts process-locally.
- Cancellation propagates. Eligibility, secret-output preflight and immutable-fact validation complete before the first
  repository mutation. Repository/configuration inconsistency is `503`; bounded operation timeout is `504`.

#### Phase gates

1. **Current gate:** planning only; this subsection and its allow-list are frozen by the 2026-08-30 user authorization.
2. **Runtime gate:** separate user code-start for this exact runtime/test allow-list; focused unit/security/real-Mongo,
   full Auth suite, architecture gate and Release build evidence are mandatory before operational use.
3. **Local Development operational gate:** separate authorization naming exact tenant, client code, service, singular
   audience, actor, command ID, expected version, one-shot pipe sink and requested operation. Run must use the supported
   CLI, verify read-back and leave no secret in repo/log/config/process arguments.
4. **Production/Staging:** prohibited. Deployment secret manager, dual control, break-glass, rotation cadence, monitoring,
   incident response and environment-specific signing/public-key operations require a separate owner-approved runbook.

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
- The Section 5 E human operator JWT authenticates only the explicit offline CLI and is never accepted by the service-token
  issuance endpoint as a client credential or authorization fallback.

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

For Section 5 E operational commands: invalid eligibility/JWT/actor/permission/marker is denied before mutation; stale version,
command-fingerprint drift, purpose drift and uniqueness races are `409`; unavailable persistence is `503`; timeout is
`504`. A secret-sink preflight failure creates no identity/rotation. A post-persistence secret-delivery interruption is
reported without secret replay and requires a new authorized rotation command.

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
8. Section 5 E, when separately authorized and implemented, exposes only the explicit one-shot CLI and seven frozen
   operations; normal API startup and HTTP surfaces remain unchanged.
9. Create/rotate/revoke and grant enable/disable are atomic, version-fenced and command-fingerprint-idempotent; concurrent
   same-command replay is stable and payload drift/stale version fails before mutation.
10. Generated plaintext crosses one protected secret sink only after persisted read-back, is never replayed and never
    appears in logs, configuration, process arguments, Mongo documents or ordinary stdout/stderr.
11. Local Development eligibility and trusted actor authorization fail before mutation; Production/Staging always deny.

## 17. Test Expectations

- Unit: claims, exact TTL, strict parser, fixed-time verification, active/previous boundaries, revocation and failure mapping.
- Security contract: RS256 signature, `kid`, issuer/audience/tenant cardinality, HS256/`none` confusion rejection and no user claims.
- Real Mongo: unique client/grant indexes, tenant A/B isolation, soft-delete behavior, replay and concurrent rotation fencing.
- Integration: Auth-issued token validates in Platform with current/previous public key; wrong tenant/audience/service/key fails.
- Regression: full Auth and Platform suites and Release builds; no skipped/fake/in-memory substitute for named Mongo tests.
- Section 5 E unit/security: Development/default-disabled matrix, exact operation parsing, JWT/active-actor/permission and
  marker denial, canonical fingerprint, expected-version checks, one-shot pipe fencing, redaction and cancellation/timeout mapping.
- Section 5 E real Mongo: create/read replay, uniqueness race, concurrent rotation one-winner, overlap boundary, revoke,
  grant create/enable/disable replay, tenant A/B isolation, purpose mismatch, stale/fingerprint conflict and exact
  post-mutation read-back. Existing token issuance must accept only the resulting enabled/non-revoked exact pair.
- Secret scan must prove no raw generated secret/marker/hash in captured logs, repository files, Mongo or exception text;
  sink tests must prove inherited one-shot pipe-only delivery, no file/stdout/stderr path and no replay emission.

## 18. Ready-for-dev Checklist

- [x] API Consumer/Credential ownership and the MOD-0033 follow-up boundary were accepted for this foundation.
- [x] RS256, exact claims, 300-second TTL and independent client/signing-key rotation models were accepted.
- [x] `Diten.MDM` / `TRUSTED_AUDIT_SOURCE_INGEST` was frozen as the first bounded pair.
- [x] Entity/index shapes and the global-identity plus tenant-scoped grant boundary were approved.
- [x] Pack was promoted to `ready-for-dev` before runtime work.
- [x] User separately authorized Section 5 A and then B code-start.
- [x] User authorized Section 5 E Phase 1.5 planning and exact allow-list preparation on 2026-08-30.
- [x] Section 5 E runtime code-start was separately authorized on 2026-08-30; planning approval alone did not permit code.

## 19. Implementation Notes

### Section E implementation evidence — 2026-08-30

Section 5 E is implemented and the pack returned to `review`. The Auth-only delivery contains the explicit one-shot CLI,
fresh Platform System Tenant operator JWT plus live permission recheck, second-factor marker, pipe-only one-time secret
delivery, atomic identity/grant CAS, fixed 300-second credential overlap and durable operation/checkpoint/evidence replay.
No HTTP/hosted/startup provisioning path, direct Mongo write, product appsettings, Platform/MDM/Gateway/frontend change,
operational data mutation or push was introduced.

Verification evidence:

- merged Section E/rotation/permission focused filter: `44/44`, zero skipped, including real localhost Mongo and concrete
  anonymous-pipe delivery/rejection tests;
- independent security/code-quality re-review: all identified P1/P2 findings closed, no remaining P0/P1;
- Auth API Release build: zero errors; one existing `MongoClientSettings.GuidRepresentation` warning in Persistence;
- full Auth suite: `680/682`, zero skipped. The two failures are pre-existing `UserLookupValidationContractTests`
  expectations for `MaskedName`/`MaskedEmail`; no Section E file participates in either failure;
- `git diff --check`: clean. Runtime secret/config/data and Local Development provisioning remain untouched at this point.

The Local Development preflight exposed a bounded-input contract defect: the valid seeded SuperAdmin JWT is 20,995
bytes, while the first implementation capped `accessToken` at 16 KiB. The field cap is now coherently 32 KiB in both
strict envelope parsing and actor authorization, while the total envelope remains 64 KiB. A real-shaped signed JWT over
16 KiB is authorized, a 20,995-character parser token reaches the authorizer, and a token over 32 KiB is rejected before
authorization or mutation. The updated focused operational/permission filter passes `43/43`, zero skipped.

Local Development operational provisioning and Product/Item/SKU WorkCenter acceptance are the next separately recorded
execution evidence; Production/Staging and push remain prohibited.

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

### Section C implementation evidence — 2026-08-29

- Auth now accepts exactly two bounded pairs and no others: `Diten.MDM/TRUSTED_AUDIT_SOURCE_INGEST` and
  `Diten.MDM/TRUSTED_WORKFLOW_CONSUMER`. Validator, handler and issuer share one exact ordinal policy; wildcard,
  trim, case-fold, comma/multi-audience and unknown service/audience values remain fail-closed.
- `ServiceClientIdentity.AllowedAudience` makes credential purpose a runtime invariant. A missing/empty legacy value
  is audit-only; an exact Workflow value is Workflow-only; malformed persisted purpose returns 409 inconsistent
  state. Even if one identity is incorrectly granted both audiences, it cannot issue both token types.
- Real-Mongo evidence uses distinct audit and Workflow identity IDs, client codes, credential hashes and exact tenant
  grants. Cross-purpose issuance is 403, using the audit credential against the Workflow identity is 401, and tenant
  or disabled-grant drift remains denied.
- Focused service-identity tests passed **61/61**, with no skipped tests. Auth API Release build passed with zero
  warnings and zero errors. The complete Auth suite recorded **649/651 passed**; the two unchanged failures are the
  pre-existing stale User Lookup two-field contract assertions against the already four-field DTO, outside this
  named-step allow-list. No service-identity test failed.
- Independent security re-review found no P0/P1/P2 issue. `git diff --check` remained clean. No repository, index,
  endpoint, parser, configuration, credential, grant or operational data mutation was performed.

### Section D — Trusted Reference Data Consumer purpose (Phase 1.5, 2026-08-29)

This additive named step closes the service-identity prerequisite for background LSKU approval revalidation. It does
not reuse or broaden the Workflow identity. `ServiceClientIdentity.AllowedAudience` is singular, so the exact new pair
is `Diten.MDM/TRUSTED_REFERENCE_DATA_CONSUMER` with a separate client identity, credential lifecycle and tenant grant.
The token remains RS256, `actor_type=service`, exact `service_name=Diten.MDM`, exact token-derived `tenant_id`, exact
300-second lifetime and current/previous signing-key rotation. Audit and Workflow audiences remain unchanged.

Exact runtime allow-list:

- `services/Diten.AuthService/src/Diten.AuthService.Application/Features/ServiceIdentityTokens/ServiceIdentityTokenAudiencePolicy.cs`
- `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedServiceTokenValidationExtensions.cs`

Exact test allow-list:

- existing Auth service-identity audience/validator/handler/issuer security and real-Mongo test files, extended only
  for the third exact pair, distinct-identity enforcement, wrong-purpose 403, wrong credential 401, missing/disabled
  tenant grant 403 and replay/cardinality evidence;
- existing Platform trusted-service-token validation and DI tests, extended only for the named
  `TrustedReferenceDataConsumerService` scheme and exact audience/claim rejection matrix.

No endpoint, service-client/grant provisioning, config, secret or operational mutation is authorized by Section D.
Runtime acceptance requires focused Auth/Platform security tests, real Mongo tenant/audience isolation, full suites,
Release builds and an independent no-P0/P1 review. Local Development identity/grant/credential provisioning remains a
separate operational gate.

## 20. Follow-up Items

1. Reconcile MOD-0033 parent pack/domain prose with Blueprint 8.1 canonical ownership without renaming runtime literals.
2. Run the implemented Section 5 E service-client/grant provisioning runner under the recorded exact-facts Local
   Development authorization and capture sanitized read-back evidence.
3. Revise MOD-0021-FU01 to consume this Platform public-key validator and run live audit ingestion smoke.
4. Implement the MOD-0290 MDM token client/cache and H1b durable audit delivery under its own exact allow-list.
5. Production/Staging signing keys, public-key distribution, credential rotation, monitoring and incident runbook remain separate gates.
