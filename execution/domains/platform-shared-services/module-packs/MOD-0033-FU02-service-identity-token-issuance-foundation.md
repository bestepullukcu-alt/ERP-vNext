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
> Operational configuration, secret, credential, tenant-grant, data, Production/Staging and push mutations remain
> unauthorized. The user separately authorized a local, no-push delivery commit on 2026-08-29.
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
- Focused Platform validator and DI tests: `25/25`; the current full Platform rerun: `2741/2741`.
- Auth and Platform API Release builds completed with zero errors. The current rerun reports zero Auth warnings and
  four pre-existing Platform nullable warnings outside this follow-up.
- Independent adversarial reviews found and closed dummy-secret authentication, partial previous-credential state,
  repository/issuer failure mapping, caller-cancellation and exact temporal-claim gaps. Final review found no P0/P1.
- A direct cross-project A-issuer-to-B-validator executable test would require a test-project reference outside the
  approved allow-list. Wire compatibility is proven from the frozen claim/signature contract and both focused suites;
  the live cross-service proof remains part of the separately gated operational onboarding.
- `git diff --check` is clean. No appsettings, secret, credential, tenant grant, business data, startup provisioning,
  push mutation was performed. The verified allow-list was later authorized for one local delivery commit only.

## 20. Follow-up Items

1. Reconcile MOD-0033 parent pack/domain prose with Blueprint 8.1 canonical ownership without renaming runtime literals.
2. Add separately gated supported service-client/grant provisioning and rotation runbook; then perform Local Development onboarding.
3. Revise MOD-0021-FU01 to consume this Platform public-key validator and run live audit ingestion smoke.
4. Implement the MOD-0290 MDM token client/cache and H1b durable audit delivery under its own exact allow-list.
5. Production/Staging signing keys, public-key distribution, credential rotation, monitoring and incident runbook remain separate gates.


### Section D — Trusted Reference Data Consumer integration (2026-09-08)

User-authorized integration of the protected final Section D into the delivery branch.
The exact pair is `Diten.MDM/TRUSTED_REFERENCE_DATA_CONSUMER`; identity AllowedAudience remains singular.
Audit and Workflow identities cannot substitute for this identity, even when a wrong-purpose grant exists.
RS256, exact service/tenant claims, 300-second lifetime, rotation and revocation contracts are unchanged.

Exact runtime paths:

- `services/Diten.AuthService/src/Diten.AuthService.Application/Features/ServiceIdentityTokens/ServiceIdentityTokenAudiencePolicy.cs`
- `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedServiceTokenValidationExtensions.cs`

Exact Auth test paths under `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/ServiceIdentityTokens/`:
`ServiceIdentityTokenHandlerTests.cs`, `ServiceIdentityTokenIssueTests.cs`,
`ServiceIdentityTokenSecurityContractTests.cs`, `ServiceIdentityTokenMongoTests.cs`.
Platform uses `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Security/TrustedServiceTokenValidationTests.cs`.

Evidence: Auth focused service-token tests 73 passed / 0 failed / 0 skipped. The fixed, test-owned
`diten_auth_service_identity_itest` Mongo grant/identity test independently passed 1/0/0; it overlaps the 73,
not an additional success. Auth and Platform Release builds passed. No operational identity, grant, key or config
was provisioned. This records code integration, not live resolver acceptance or independent review completion.

### Section E — P5-01 bounded operational reuse amendment (2026-09-29, APPROVED CODE/TEST START)

**Superseding approval record (2026-09-29):** the user explicitly answered `onaylıyorum` to the consolidated three-step
code/test approval. Only this Section E Phase 1.5 design and exact **19 runtime + 6 test** paths are approved for bounded
implementation and test-owned verification. Foundation frontmatter/status and Sections A/B/D remain unchanged.
This grants no live credential/DB operation, collection/index creation, configuration, service-start, commit, push or
merge authority. Live operation requires a separate exact target, actor, process and full consumer-impact approval.

**Reuse, not redevelopment:** source commits `886a1ae61edac062835fccd7959c87671bc0bc5e` and
`48154c4f18c2070d0b912bd86a5a62cbb6805158` already implement operational provisioning in the older integration lineage.
The target baseline is `d5f811ad7d10426498c7d0460af65ae573df4382` in
`C:/dev/ERP-vNext/.worktrees/product-pv-delivery-integration-20260907`. Historical source test results are not target
verification. Reuse the source JWT/persisted-permission checks, secret hashing/output pipe and operation-journal seams;
do not wholesale-port its normal-startup dispatch, seed, index DDL or broader mutation commands.

#### E.1 Frozen boundary and design decision

- Only exact `read-identity`, `read-grant` and `rotate-credential` operations are candidates. Create/revoke identity,
  enable/disable grant, membership, role, permission/catalog sync and business-data mutation are excluded.
- Exact `--service-client-operational-run` dispatch must occur before `WebApplication.CreateBuilder`, normal Auth
  `AddPersistence` or normal host construction. Duplicate, case-variant, unknown and conflicting operational commands
  fail closed before side effects. No-argument Auth startup keeps its existing behavior.
- Use isolated minimal composition, default-disabled and Development-only, with immutable, process-environment-derived
  eligibility facts. No normal seed/index ensure, MassTransit, hosted workers, login host or generic provisioning runs.
  Reuse the existing source options contract; no new configuration model or appsettings change is authorized.
- Authority is a fresh validated `platform_admin` JWT in the Platform system tenant, matching active persisted actor
  and current persisted `auth.service-clients.provision` permission, plus an exact one-shot operational marker. The
  permission constant already exists in the target; no permission/role/grant is created to make authorization pass.
  An OS account or caller-supplied actor GUID is not application authority. Missing/duplicate/contradictory actor facts,
  disabled/deleted actor or permission, expired token or invalid marker fail before mutation.
- The marker enables one exact command envelope in one isolated invocation; it is not a generic reusable grant or a
  claimed global revocation fence. Durable command identity/fingerprint and CAS remain necessary across processes.
- The existing journal `serviceClientOperationalProvisioningOperations`, identity/grant collections and required index
  specifications must already exist and be verified read-only before reservation. No `EnsureIndexes`, DDL, upsert-created
  collection, alternate journal or fallback storage is allowed. Source journal specifications are
  `ux_service_client_operational_command_active` (CommandId ascending, unique, partial IsDeleted=false) and
  `ix_service_client_operational_state_updated` (State then UpdatedAt ascending); absence or key/order/unique/partial/TTL
  mismatch blocks operation. Reuse does not authorize creating these indexes or purging/reusing journal command IDs.
- Reuse current `IServiceClientCredentialVerifier.Hash()` and the source-generated 32-byte random secret. Accept no
  caller-supplied secret. Deliver only through the existing inherited anonymous-pipe sink, once, after exact persisted
  outcome read-back. Standard streams, paths, argv, logs, files and report artefacts cannot carry the raw secret.
  Preserve handle/type validation, bounded payload, disposal and buffer-clearing behavior; test the actual OS pipe.
- `ServiceClientIdentity` is global, not tenant-owned. Rotation affects **every tenant and consumer using that identity**.
  Live approval must name exact identity/client/service/audience, active and overlap credentials, every affected
  tenant/audience grant and consumer deployment. A tenant-scoped `read-grant` does not make rotation tenant-local.

**Trade-off:** a bounded source adaptation preserves the existing credential model while excluding known broad writes;
it may remain operationally blocked when journal/index/actor permission prerequisites are absent. Neither a whole-branch
cherry-pick nor direct Mongo patch is an acceptable shortcut. No new permission, collection, index or config model is
invented by this amendment. The workflow-definition bridge is a separate P5-01 dependency, not part of this allow-list.

#### E.2 State, concurrency, replay and failure contract

1. Freeze the command fingerprint over exact command/operation/target identity, ClientCode, ServiceName, AllowedAudience,
   expected credential and operational version, actor and authorized rotation parameters. Persisted purpose must match
   the process-authorized purpose, including ClientCode; pre-read validation alone is insufficient.
2. The identity mutation CAS includes exact ID/code/service/purpose, non-deleted/non-revoked state and expected credential
   version/hash plus operational version. No blind replacement, force mode or unconditional retry is permitted.
   Existing documents without `OperationalVersion` use an explicit absent-or-zero predicate only for expected zero,
   additionally bound to the exact current credential version/hash. No migration/backfill is introduced.
3. Journal states/checkpoints reuse the source pending/recovery/completed model and bounded evidence; persisted operation
   identity and recorded outcome are immutable. Two commands racing on the same expected identity version yield at most
   one rotation winner. A same-key changed fingerprint/purpose/actor is rejected without another rotation.
4. Completed replay returns the **recorded outcome**, not a later current identity state masquerading as that outcome.
   Current-state observations are separately identified; a later rotation cannot rewrite the earlier command's result.
   Replay never generates or re-delivers a raw secret. Pending/ambiguous/partial state cannot be promoted to success
   solely because the current identity happens to look compatible.
5. Lost acknowledgement, crash or pipe delivery failure after mutation is a bounded recovery/manual-reconciliation
   result, not authorization to rotate again. Distinguish credential mutation/read-back from consumer receipt; a flushed
   pipe proves the sink write, not downstream installation. No plaintext recovery storage or automatic restore exists.
6. `read-identity`/`read-grant` are sanitized, zero-mutation reads. Wrong target/purpose, absent identity, disabled/revoked
   state and inconsistent prerequisites fail closed. Grant reads retain exact tenant/identity/audience and soft-delete
   filters; global identity reads require platform authority and never expose hashes or secrets.

#### E.3 Exact candidate implementation allow-list

Paths are repository-relative to the exact target above; `Existing`/`New` is verified against the target baseline.
This is the approved bounded writer scope. Runtime: **19 paths (4 Existing / 15 New)**.

| State | Exact runtime path |
| --- | --- |
| Existing | `services/Diten.AuthService/src/Diten.AuthService.Api/Program.cs` |
| New | `services/Diten.AuthService/src/Diten.AuthService.Api/Configuration/ServiceClientOperationalProvisioningOptions.cs` |
| New | `services/Diten.AuthService/src/Diten.AuthService.Api/Services/ServiceIdentityTokens/DevelopmentServiceClientOperationalProvisioningEligibility.cs` |
| New | `services/Diten.AuthService/src/Diten.AuthService.Api/Services/ServiceIdentityTokens/ServiceClientOperationalActorAuthorizer.cs` |
| New | `services/Diten.AuthService/src/Diten.AuthService.Api/Services/ServiceIdentityTokens/ServiceClientOperationalProvisioningRunner.cs` |
| New | `services/Diten.AuthService/src/Diten.AuthService.Api/Services/ServiceIdentityTokens/ServiceClientSecretOutputSink.cs` |
| New | `services/Diten.AuthService/src/Diten.AuthService.Api/Services/ServiceIdentityTokens/ServiceClientOperationalBootstrap.cs` |
| New | `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IServiceClientOperationalProvisioningEligibility.cs` |
| New | `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IServiceClientOperationalActorAuthorizer.cs` |
| New | `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IServiceClientSecretOutputSink.cs` |
| New | `services/Diten.AuthService/src/Diten.AuthService.Application/Features/ServiceIdentityTokens/Operational/ServiceClientOperationalProvisioningModels.cs` |
| New | `services/Diten.AuthService/src/Diten.AuthService.Application/Features/ServiceIdentityTokens/Operational/ServiceClientOperationalProvisioningService.cs` |
| Existing | `services/Diten.AuthService/src/Diten.AuthService.Domain/Entities/ServiceClientIdentity.cs` |
| New | `services/Diten.AuthService/src/Diten.AuthService.Domain/Entities/ServiceClientOperationalProvisioningOperation.cs` |
| Existing | `services/Diten.AuthService/src/Diten.AuthService.Domain/Repositories/IServiceClientIdentityRepository.cs` |
| New | `services/Diten.AuthService/src/Diten.AuthService.Domain/Repositories/IServiceClientOperationalProvisioningOperationRepository.cs` |
| Existing | `services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/ServiceClientIdentityRepository.cs` |
| New | `services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/ServiceClientOperationalProvisioningOperationRepository.cs` |
| New | `services/Diten.AuthService/src/Diten.AuthService.Persistence/ServiceClientOperationalPersistenceRegistration.cs` |

Tests: **6 paths (1 Existing / 5 New)**.

| State | Exact test path |
| --- | --- |
| New | `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/ServiceIdentityTokens/ServiceClientOperationalProvisioningEligibilityTests.cs` |
| New | `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/ServiceIdentityTokens/ServiceClientOperationalProvisioningRunnerTests.cs` |
| New | `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/ServiceIdentityTokens/ServiceClientOperationalActorAuthorizationTests.cs` |
| New | `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/ServiceIdentityTokens/ServiceClientOperationalProvisioningMongoTests.cs` |
| New | `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/ServiceIdentityTokens/ServiceClientOperationalBootstrapTests.cs` |
| Existing | `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/ServiceIdentityTokens/ServiceClientCredentialRotationTests.cs` |

Protected in this slice: normal Application/Infrastructure/Persistence `DependencyInjection.cs`, `DataSeeder.cs`,
`MongoDbIndexConfigurations.cs`, `DefaultRolePermissionTemplate.cs`, grant entity/repositories, issuance/parser, human
token implementation, appsettings, Platform/MDM/Gateway/frontend and all other paths. Existing FU18/FU19 dirty amendments
belong to a different writer and remain untouched. Minimal bootstrap/persistence registration must not silently invoke
the protected normal startup path. The existing options/model names are reused as an explicit operational CLI exception
to UI/CQRS scaffolding; no public endpoint, browser surface or new Gateway route is created.

#### E.4 Phase 1.5 owner review — nine-point design approved 2026-09-29

| # | Check | Plan |
| --- | --- | --- |
| 1 | All fields mapped | Reuse identity credential/overlap/revocation fields; add source OperationalVersion; journal command/fingerprint/target/purpose/actor, checkpoints and recorded outcome. JWT+persisted records supply actor; exact process envelope supplies authorized inputs; repository supplies current state. Raw secret is transient only. |
| 2 | Global ERP naming | Preserve canonical ClientCode, ServiceName, AllowedAudience, OperationalVersion and CommandId; no local naming or business `Version` shadowing. |
| 3 | Isolation / soft delete | Global identity/journal require platform authorization and exact command/target filters; identity CAS includes IsDeleted=false and IsRevoked=false. Existing grant repository supplies tenant/identity/audience filtering. |
| 4 | Entity base | Existing `ServiceClientIdentity : GlobalEntityBase`; reused operational journal also global because a rotation is identity-wide. `ServiceClientTenantGrant` remains unchanged and tenant-owned. |
| 5 | CQRS / composition | Existing operational CLI runner/service/interface/repository pattern; E.3 exact files. Pre-builder isolated bootstrap, no web command/controller or normal DI registration changes. |
| 6 | Golden / form count | N/A: backend CLI, zero UI fields, shell:none, golden_reference:none. |
| 7 | Compact section map | N/A: no Razor/Create/Edit/Details surface. |
| 8 | Required-field parity | CLI parser/eligibility/service enforce identical exact operation, target/purpose, actor, marker, command/version and rotation pipe contract; conditional grant TenantId applies only to read-grant. Web ViewModel/Razor/tracker N/A. |
| 9 | Lookup dependencies | None: no lookup UI/key/endpoint, no hardcoded fallback list and no MDM/reference ownership change. |

#### E.5 Measurable verification gates and handoff

- Source hash -> Release build -> DLL/PDB hash -> focused test -> final source hash chain; original no-argument Auth
  startup and existing token issuance/rotation behavior remain regression-tested without running normal local startup.
- Real Release child CLI with **test-owned dynamic 127.0.0.1 Mongo replica**, port not 27017, fixed test DB and
  tenant-owned grant and exact fixture-owned global identity/journal cleanup with zero read-back. No application DB,
  fallback URI or drop of a shared fixed database.
- Mutation/command-monitor evidence: no normal seed, index DDL, bus, hosted-worker, grant/catalog/role/UserRole mutation;
  only the approved target credential and exact journal writes occur on a successful test rotation. Read operations,
  invalid environment/JWT/marker/operation/sink and absent/mismatched journal/index preflight produce zero mutation.
- Actual anonymous-pipe successful one-shot delivery and standard-stream/path/closed-pipe rejection; no raw secret in
  stdout/stderr, logs, TRX or captured artefacts. Pipe failure after credential mutation reports uncertainty distinctly.
- Concurrent rotation: one winner, no lost update; purpose/ClientCode/service/revoked/credential drift fails CAS. Missing
  legacy OperationalVersion, explicit zero and nonzero cases are separate tests, without a data migration.
- Same-key exact replay produces no second rotation/secret/journal; changed fingerprint fails; replay after a later
  rotation preserves immutable old outcome and labels current state separately. Crash/ambiguous-write paths remain
  recovery/manual until exact evidence resolves them, never optimistic completion.
- Security review checks global blast radius, fresh persisted authority, secret channel and unchanged grant permissions;
  writer-independent verification checks exact path set and test provenance. Historical source tests are not counted
  as new runs. E.5 lists required future tests, not tests executed during this planning amendment.

**Approval gate closed:** explicit Phase 1.5 + E.3 code/test start. **Open gates:** implementation/security/independent
verification, followed by live prerequisite existence and exact full-impact operational approval. Missing journal/index/
permission blocks live operation without broadening this scope. Foundation `review` and this bounded code-start do not
mark P5-01 implemented, first-five acceptance complete, merge-ready or Production/Staging authorized.

#### E.6 Bounded implementation and test evidence (2026-09-30)

**Scope verdict:** P5-01's approved **19 runtime + 6 test** path implementation is complete and the bounded code/test
verification verdict is **PASS**. The independent read-only review also returned **PASS for this code/test boundary**
after the three final evidence gaps were closed. This is not a live-operation, checkpoint, first-five acceptance,
merge-readiness or Production/Staging verdict. Foundation frontmatter/status and the earlier Sections A/B/D remain
unchanged.

The implementation preserves the E.1/E.2 design: pre-builder exact CLI dispatch, immutable Development-only eligibility,
fresh signed-and-persisted Platform-system-tenant actor authority, pre-existing journal/index read-only preflight,
identity CAS plus durable command journal, immutable replay outcome and anonymous-pipe-only one-shot secret delivery.
It creates no identity, grant, permission, role, collection or index. It performs no normal seed/index ensure, hosted
worker, bus, login-host, application-DB fallback or startup auto-provisioning. The only source change after the first
green run was within the already approved Mongo test path to close the three evidence gaps below; runtime was unchanged.

**Authoritative raw evidence (repository-relative):**

- `.testoutput/p5-credential-reuse-implementation-01/testing/verification-summary-final2.md`
- `.testoutput/p5-credential-reuse-implementation-01/testing/final2/focused-final2.trx`
  (`SHA-256 E0016B93E76785EFCA6097F810EB13049C51CD5A97A24029A2BCEF603630EC58`)
- `.testoutput/p5-credential-reuse-implementation-01/testing/final2/gap-closure-initial.trx`
  (`SHA-256 D78A83368B598D7CB0888686A691C17F45325EDA51A5B1A62227D57733980909`)
- `.testoutput/p5-credential-reuse-implementation-01/testing/final2-verification.json`
- `.testoutput/p5-credential-reuse-implementation-01/testing/source-before-final2.json` and
  `source-after-final2.json`
- `.testoutput/p5-credential-reuse-implementation-01/testing/binary-before-final2.json` and
  `binary-after-final2.json`

Raw final2 evidence reports **201 discovered / 201 executed / 201 passed / 0 failed / 0 skipped**. The approved six test
classes contribute 133 cases; the selected existing issuance/parser/handler/DI regressions contribute 68. The separate
12/12 gap-closure run is a targeted precursor and is not added again to the 201 total. The isolated Release build passed
with zero errors and one test-fixture `MongoClientSettings.GuidRepresentation` obsolescence warning. It is explicitly
not classified as pre-existing.

The source-before/source-after manifests contain **321/321** entries with zero hash mismatch and identical manifest-file
SHA-256 `6272CEDD5F786C8F0647E670754F72D9607405AE422FA890310D55990F4DF54E`. The matching isolated
binary-before/binary-after manifests contain **104/104** entries with zero mismatch and identical manifest-file SHA-256
`14E543E0DDA0641C6579AADB3C19C18ABA93B8CDC40B9AE5BC4D5762C6F075F0`. All 11 recorded owned PIDs (two
test-owned replica processes across targeted/full runs and nine Release CLI children) were absent after the run; the
owned temporary-directory inventory was empty. No application process, application database or fixed shared database
was stopped, dropped or cleaned.

The three independently required gap proofs are now present:

1. Two server-injected ambiguous-write cases use one-shot `failCommand` write-concern errors with retryable writes off.
   Journal reservation ambiguity remains durable `Pending/Reserved`; credential `findAndModify` ambiguity leaves the
   changed target and `RecoveryRequired`. Both yield zero secret delivery, one original write attempt and fail-closed
   replay without a second mutation.
2. Nine real existing-index metadata mismatches cover wrong key, key order, unique flag, missing/changed partial filter,
   TTL, hidden, sparse and collation. Every mismatch fails before insert/update/delete/findAndModify/DDL and the fixture
   does not let production preflight repair it.
3. A genuinely completed command is tombstoned and replayed. Replay fails closed with no secret, no extra journal and no
   identity mutation.

**Preserved RED and initial-harness provenance:** `testing/red/broken-peer-red.trx` remains the actual one-test RED
(`1/1 executed, 0 passed, 1 failed`; SHA-256
`655DA472005D602CD1A5FD9ACF4F40122146C1EB87613CF0A151CA4732099C11`) that proved the original pipe preflight
accepted a broken peer. The unchanged expectation is green in final2. The initial Mongo-child TRX remains
`34 executed / 16 passed / 18 failed`: 17 actual cases included one parent-writer-handle EOF failure plus 17 cleanup
failure records caused by the same harness defect. Initial non-Mongo and selected regression runs remained 92/92 and
68/68. The owned replica PID was confirmed absent before its exact disposable temporary directory was removed; no
application data was touched.

**Corrected metadata disclosure:** the original `final1-verification.json` serialized counters as `[]` and was manually
corrected before the preserve instruction arrived; it is not represented as untouched original evidence. The separate
`final1-counters-correction-20260930.json` records namespace-independent raw-TRX counters and the raw TRX/manifests were
not rewritten. Final2 was created with the corrected parser, so its 201/201/0/0 counters and hashes are the current
authoritative chain.

Bounded reproduction (code/test only; no live operational invocation):

```text
dotnet build services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Diten.AuthService.Application.Tests.csproj -c Release --no-restore -v minimal -p:OutputPath=<absolute-isolated-output>

dotnet test services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Diten.AuthService.Application.Tests.csproj -c Release --no-build --no-restore -p:OutputPath=<absolute-isolated-output> --filter "FullyQualifiedName~ServiceClientOperational|FullyQualifiedName~ServiceClientCredentialRotationTests|FullyQualifiedName~ServiceIdentityTokenIssueTests|FullyQualifiedName~ServiceIdentityTokenHandlerTests|FullyQualifiedName~ServiceIdentityTokenSecurityContractTests|FullyQualifiedName~ServiceIdentityTokenDependencyInjectionTests" --logger "trx;LogFileName=focused-final2.trx"
```

No live credential, identity, tenant grant, configuration, service or provisioning mutation was performed. No commit or
push was authorized or executed. Live prerequisite/readback, full affected-consumer inventory, installation/rollback
and first-five acceptance remain separately gated; this evidence must not be used to claim those gates closed.
