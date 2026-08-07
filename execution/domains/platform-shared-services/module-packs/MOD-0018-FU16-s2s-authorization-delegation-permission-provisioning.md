---
id: MOD-0018-FU16
name: S2S Authorization, Delegation and Permission Provisioning
domain: platform-shared-services
service: Diten.AuthService
shell: none
golden_reference: none
entity_base: GlobalEntity
status: ready-for-dev
owner: platform-team / security-architect
branch: feature/pss/mod-0018-gate-i-s2s
started: 2026-08-04
target: ""
form_field_count: 0
parent_module: MOD-0018
execution_authority: none
production_authority: none
execution_activation: none
---

# MOD-0018-FU16 — S2S Authorization, Delegation and Permission Provisioning

> **READY-FOR-DEV / NON-RUNTIME.** This governance pack closes the Gate I pre-development design contract. It
> authorizes no runtime, AuthService, Platform.Common, producer-service, frontend, gateway, seed, migration,
> credential-provisioning or deployment change. Implementation still requires separately approved executable
> packs and explicit implementation authority; production activation requires the open evidence gates in §18.

> **Identity (DCP-002, proven).** `MOD-0018-FU16` is a follow-up of Blueprint-canonical parent `MOD-0018 —
> RBAC / ABAC Authorization`. The registry contains no other FU16 and FU1/FU9/FU10/FU10a/FU10b/FU11–FU15
> remain distinct. Preflight:
> `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0018-FU16 --name "S2S Authorization, Delegation and Permission Provisioning" --parent MOD-0018`
> returns exit `0`. This pack creates no independent MOD or CAND identity.

> **Golden Reference decision.** This is a backend/shared-security governance slice, not a UI or DataTable
> module. `shell: none`, `golden_reference: none`, `form_field_count: 0`; layout, Razor, RESX and DataTable
> contracts are N/A. `entity_base: GlobalEntity` satisfies frontmatter convention only; implementation may
> introduce a global service-principal registry, but this draft creates no entity.

## 1. Module Summary

Gate I standardizes three related but separate controls for producer services associated with `MOD-0007`,
`MOD-0136`, `MOD-0138` and `MOD-0072`:

1. authenticated service identity with lifecycle-managed credentials;
2. tenant- and operation-bound proof when a service acts for a human tenant actor; and
3. owner-declared permission catalog registration plus explicit tenant role grants.

The target onboarding profiles are `Diten.ManagementGovernanceService`, `Diten.FpaService` and
`Diten.DecisionIntelligenceService`. Gate I does not define their business APIs or permission lists. It
defines the protocol by which owner packs supply those lists and by which AuthService records catalog and
grant state without conflating module entitlement, service identity or delegated user authority.

## 2. Ownership and Boundaries

**PSS / MOD-0018-FU16 owns:**

- the ServicePrincipal registry contract and credential lifecycle policy;
- service-token and delegated-actor proof schema, validation order and failure semantics;
- protocol scopes, token audiences and canonical claim names;
- replay, revocation, credential rotation, permission freshness and audit requirements;
- manifest registration and explicit tenant-scoped role-grant provisioning protocol;
- onboarding-profile schema and conformance gates.

**Producer owner packs own:**

- their exact lowercase dotted permission keys and descriptions;
- operation-to-permission mapping, API operation IDs and receiving-service audience mapping;
- which tenant roles receive which permissions, under an authorized tenant administrator workflow;
- removal/deprecation of producer-owned permissions and compatibility policy.

**Not owned here:** business aggregates; module entitlement lifecycle; producer API routes; business authorization
rules; producer code; Gateway routes; secrets storage implementation; certificate authority; user/role CRUD; PPM
objects; MDM objects; a generic policy DSL; or any permission guessed by PSS.

## 3. Owned Objects

| Contract object | Ownership | Required identity/invariant |
|---|---|---|
| `ServicePrincipalRegistration` | AuthService global security registry | Immutable `ServicePrincipalId` Guid and unique `ClientId`; status `Pending/Active/Suspended/Revoked/Retired` |
| `ServiceCredentialDescriptor` | AuthService metadata; secret material stays in approved vault | `CredentialId`, `ServicePrincipalId`, type, key id/thumbprint, validity, status, rotation generation; never stores plaintext secret |
| `DelegatedActorProofV1` | AuthService-issued signed JWT contract | One tenant, one actor, one producer, one audience and one operation per proof |
| `PermissionCatalogManifestV1` | Producer-owned content, AuthService-validated registration | Owner module ID + module entitlement identity + service identity + exact permission entries are distinct fields |
| `ExplicitRoleGrantProvisioningV1` | AuthService tenant authorization SoR | Exact `(TenantId, RoleId, PermissionId)` grant; never derived merely from entitlement |
| `S2SReplayReceipt` | Security replay store | Unique `(Issuer, Jti)` and nonce/request binding until proof expiry plus skew |
| `AuthorizationVersionVector` | AuthService | Tenant grant version + service-principal version + credential generation used for freshness checks |

AuthService is the binding system of record for these logical persistence contracts. Exact collection/table names,
physical placement and operational retention are deferred to an executable implementation pack; that deferral does
not reopen ownership or contract approval.

## 4. Entity Fields

### ServicePrincipal registry minimum

| Field | Rule |
|---|---|
| `ServicePrincipalId` | Non-empty immutable Guid; JWT `sub` |
| `ClientId` | Exact stable lowercase identifier; JWT `client_id` and `azp`; globally unique |
| `DisplayName` | Operator label; not authorization input |
| `OwnerModuleIds` | Blueprint IDs only; onboarding profile supplies the closed set |
| `AllowedAudiences` | Exact receiver identifiers; no wildcard |
| `AllowedProtocolScopes` | Closed protocol scope set; no business permission substitution |
| `Status` | Active required; suspended/revoked/retired fail 401 |
| `NotBeforeUtc` / `ExpiresAtUtc` | Explicit lifecycle bounds |
| `PrincipalVersion` | Monotonic; suspension, revocation or audience/scope change increments it |
| `CredentialGeneration` | Monotonic active generation; token must match an accepted rotation generation |

### DelegatedActorProofV1 exact claims

| Claim | Exact rule |
|---|---|
| `iss` | `diten-auth-service` |
| `aud` | Exactly one of `diten-management-governance-service`, `diten-fpa-service`, `diten-decision-intelligence-service`; arrays and generic `diten-erp` are rejected for this proof |
| `sub` | Non-empty `ServicePrincipalId` Guid |
| `client_id` | Registered exact client identifier |
| `azp` | Must equal `client_id` and resolve to `sub` |
| `actor_type` | Exact value `service` |
| `tenant_id` | Non-empty tenant Guid; must equal route/resource tenant resolved server-side |
| `delegated_actor_id` | Non-empty tenant-user Guid |
| `delegated_actor_type` | Exact value `tenant_user` |
| `delegation_id` | Non-empty immutable Guid tying issue/audit/revocation records |
| `operation_id` | Exact producer-owner manifest operation ID; no wildcard or route-prefix match |
| `permission` | One or more repeated exact owner-manifest permission claims; requested operation's required key must be present |
| `scope` | Exact protocol value `diten.s2s.delegated.invoke`; it never substitutes for `permission` |
| `request_hash` | Base64url SHA-256 of canonical method + canonical path + tenant + operation + body digest; mismatch is 401 |
| `nonce` | At least 128 bits of cryptographic randomness; single-use with `jti` |
| `jti` | Non-empty unique 128-bit-or-stronger identifier; single-use |
| `iat` / `nbf` / `exp` | NumericDate; `exp - iat <= 300s`; `nbf <= now + 30s`; absolute clock skew maximum 30s |
| `tenant_grant_version` | Exact current AuthService tenant role/permission version |
| `service_principal_version` | Exact current registry principal version |
| `credential_generation` | Accepted active/overlap credential generation |

The proof contains no email, display name, role name, raw credential, refresh token or business payload.

### Separate production S2S token family and signing profile

- `DelegatedActorProofV1` is a separate token family/profile from the existing AuthService user/session JWT. The
  existing HS256 user JWT issuer/audience, signing, validation and refresh behavior is frozen: it is neither migrated
  nor reused by Gate I, and its symmetric secret must never sign or validate an S2S proof.
- The S2S family uses exact protected header `typ = diten-delegated-actor-proof+jwt`, a dedicated AuthService issuance
  application contract and a dedicated consumer authentication/validation scheme. The S2S scheme rejects user/session
  JWTs, and the user/session scheme does not accept S2S proofs. Neither scheme falls through to the other. Exact
  physical endpoint/route names remain an implementation-pack decision.
- Production Gate I S2S signing is exactly `RS256` with an RSA key of at least 3072 bits. The protected JWT header
  requires `alg = RS256` and one exact non-empty `kid`. `none`, HS256, any other asymmetric algorithm, a missing,
  unknown, retired or duplicate `kid`, algorithm/key-type confusion, and token-supplied `jku`, `jwk` or `x5c` key
  sources fail closed as `401`.
- The private key remains only within an approved vault/HSM boundary. Consumers obtain public validation keys only
  from the configured/pinned trusted AuthService HTTPS JWKS/validation-key provider. JWKS exposes public material for
  current or explicitly overlap-valid credentials only. Static repository keys, embedded PEM material, retired-key
  fallback, generic `diten-erp` validation, `X-Internal-Api-Key`, token-directed discovery and default/fallback secrets,
  keys or authentication schemes are forbidden. If a required trusted key cannot be resolved and no valid trusted
  cache entry exists, the authority is indeterminate and returns `503`; an invalid/untrusted key or signature is `401`.
- `iat`, `nbf`, `exp`, `iss`, single `aud`, `jti`, `sub`, `client_id` and `azp` are mandatory. Maximum lifetime is
  five minutes and maximum clock skew is 30 seconds. NumericDate ordering is `iat <= nbf < exp`, `exp - iat <= 300s`,
  and neither `iat` nor `nbf` may be more than 30 seconds in the future. Acceptance is bounded by `nbf - 30s` and
  `exp + 30s`; skew never permits a proof whose declared lifetime exceeds five minutes. Each receiver uses its exact
  audience profile; generic or multiple audiences fail `401`.

### Atomic replay receipt contract

- One replay receipt records issuer, `jti`, nonce, request hash and `exp`. The datastore enforces unique `(Issuer,Jti)`
  and unique `(Issuer,Nonce)` constraints. After signature and standard-claim validation but before protected
  execution, the authority performs one atomic insert-if-absent; read-then-write is forbidden. It retains the receipt
  until at least `exp + 30 seconds`; TTL cleanup is housekeeping and not the uniqueness correctness mechanism.
- Reuse of the same `jti` or nonce, a changed method/path/body/tenant/actor/operation binding, and concurrent
  duplicate consumption are terminal `401` outcomes.
- Replay authority unavailable or indeterminate is `503`, never allow. No protected handler or repository may run
  until token validation, freshness and atomic replay consumption all succeed.

## 5. Repo Scope

**This authoring change may touch only:**

- this module pack; and
- `execution/registries/module-id-registry.md` for the proven identity reservation.

**A future approved implementation may propose, but is not authorized by this draft:**

- `services/Diten.AuthService/**` for principal, token, catalog and explicit-grant SoR;
- `services/Diten.Platform.Common/**` for shared validation contracts that contain no service-specific permission;
- the three producer services only through their separately approved owner packs.

## 6. Protected Paths

- All `services/**`, `frontend/**`, `gateway/**`, `.antigravity/**` and runtime configuration are protected during
  this governance-only task.
- `gateway/Diten.ApiGateway/**/ocelot.json` remains integration-agent-only.
- Existing MDM generic entitlement-to-grant behavior and all non-PPM legacy modules are frozen against behavioral
  change by this FU.
- PPM explicit-only grant behavior, its 16-key Phase 2A closed set and dormant-grant semantics are frozen.
- Producer-specific permissions may not be placed in PSS seeds, defaults or shared code.

## 7. Dependencies

- Parent [MOD-0018](MOD-0018-rbac-abac-authorization.md) and follow-ups
  [FU12](MOD-0018-FU12-tenant-authorization-context-foundation.md) (tenant authorization context) and
  [FU13](MOD-0018-FU13-permission-convention-cache-invalidation.md) (permission-change invalidation), plus
  existing signed JWT permission evaluation.
- MOD-0012-approved vault/rotation mechanism for credential material; this pack stores metadata only.
- Producer owner packs for MOD-0007, MOD-0136, MOD-0138 and MOD-0072 before any permission or operation manifest
  becomes executable.
- AuthService permission/role/grant SoR and tenant entitlement events.
- [PPM MOD-0117](../../portfolio-delivery/module-packs/MOD-0117-project-portfolio-management.md) regression baseline
  as a mandatory explicit-grant reference, not as a producer owned by this pack.

## 8. Runtime Constraints

- Authentication, service authorization, delegated user authorization and module entitlement are independent
  gates. All required gates must allow; one success never implies another.
- `ModuleCode`/module entitlement identity is commercial capability identity. `ServicePrincipalId`/`ClientId` and
  token `aud` are workload/receiver identities. A `ModuleCode` is never a service client ID or audience alias; these
  values are never compared as strings to infer access.
- Gate I fixes the producer `ModuleCode` values as `MOD-0007`, `MOD-0136`, `MOD-0138` and `MOD-0072`. Each is
  tenant-assignable, non-baseline, entitlement-gated and explicit-grant-only. `MOD-0136` and `MOD-0138` remain two
  independent entitlement gates inside `Diten.FpaService`, despite sharing its client ID and audience.
- A service-only call has no delegated human authority. An endpoint requiring user visibility or mutation must
  reject a service token without a valid delegated proof.
- Each delegated proof is single tenant, single receiver and single operation. No cross-tenant, multi-audience,
  wildcard operation or bearer-forwarding reuse is allowed.
- Validation is online against principal, credential and grant version state or an invalidation-backed cache whose
  maximum staleness is less than the proof lifetime. Dependency uncertainty fails closed.
- Logs, traces, metrics and audit events redact Authorization headers, token bytes, secrets, private keys, nonces and
  request bodies. They may record hashed `jti`, principal/client IDs, tenant, operation, permission, result and reason.

## 9. Layout & Shell Contract

N/A. `shell: none`; this FU defines no Razor page, layout, browser route, DataTable, localization surface or frontend
asset. A future administrative UI requires a separate approved pack and explicit shell decision.

## 10. Backend File Convention

No backend file creation is authorized. A future implementation must preserve five-layer/CQRS boundaries and keep:

- token/manifest request contracts in Application/Contracts or a dedicated Contracts assembly;
- principal/grant invariants in Domain;
- Mongo/vault/signing/replay implementations in Persistence/Infrastructure;
- thin API endpoints with validators and authorization policies;
- shared Platform.Common types protocol-only and free of producer permission constants.

Producer onboarding adapters remain in their owner services and must not copy AuthService grant resolution or token
validation logic.

## 11. Frontend File Contract

N/A. No frontend files, MVC proxies, JavaScript, navigation, RESX, offcanvas, modal or DataTable are in scope.

## 12. Validation Rules

| Input/state | Validation |
|---|---|
| Principal registration | Active, within lifecycle bounds, exact client/sub binding, audience and protocol scope allowlisted |
| Credential | RS256 signature valid with trusted AuthService public key, RSA >=3072 bits, active generation, not expired/revoked, rotation overlap valid |
| JWT header | Exact `alg=RS256` and one exact non-empty known `kid`; `none`, HS256, other algorithms, missing/unknown/duplicate kid and algorithm confusion rejected 401 |
| JWT time | Required `iat/nbf/exp`; maximum five-minute lifetime and 30-second skew |
| Delegation | Exact tenant, delegated actor, operation, permission and request hash; actor is active/referenceable |
| Replay | Atomic unique `(Issuer,Jti)` and `(Issuer,Nonce)` first-write with request-hash binding retained through at least `exp + 30s`; duplicate, changed or concurrent reuse is 401; unavailable/indeterminate authority is 503 |
| Manifest | Blueprint owner ID exists; module entitlement code separately declared; producer profile matches; keys are exact lowercase dotted owner values; duplicate/conflicting owner rejected |
| Grant request | Server-derived tenant; exact role and permission IDs; tenant role may hold permission; authorized administrator; idempotency key required |
| Freshness | Token versions equal current tenant grant/principal/credential versions; stale token rejected and reissue required |

## 13. Failure Path to Verify

| HTTP | Exact class |
|---|---|
| `400` | Malformed manifest/grant request; unknown operation syntax; invalid Guid; unsupported contract version; missing idempotency key |
| `401` | Missing/invalid/expired token; wrong issuer/audience/signature; inactive principal/credential; request-hash mismatch; replay; stale principal/credential generation |
| `403` | Authenticated principal is not allowed for audience/scope; delegated actor lacks exact permission; tenant entitlement denies; role/grant policy denies |
| `404` | After authentication/authorization, missing/soft-deleted/cross-tenant resource or non-referenceable actor; responses are indistinguishable |
| `409` | Client ID/credential collision; conflicting manifest ownership/version; idempotency-key payload mismatch; concurrent grant mutation/version conflict |
| `503` | Principal/grant/replay/freshness authority unavailable or indeterminate; never mapped to allow, 401, 403 or 404 |

Validation order is syntactic contract (`400`) → authentication/replay/freshness (`401`) → audience/scope,
entitlement and delegated permission (`403`) → resource non-disclosure (`404`) → mutation conflict (`409`). An
authority dependency outage at any security decision point is `503` and no protected operation runs.

## 14. Authorization Convention

### Exact Gate I decision chain

1. Validate signed `DelegatedActorProofV1` header, issuer, single audience, times, key and credential generation.
2. Resolve `sub/client_id/azp`; require active ServicePrincipal and exact audience/scope allowance.
3. Atomically consume `(iss,jti,nonce,request_hash)` replay record.
4. Match server tenant and exact owner-declared `operation_id`.
5. Require the receiving endpoint's exact owner-manifest `permission` claim.
6. Require current tenant entitlement for the permission's module when that owner pack declares entitlement-gated
   access.
7. Require the delegated actor's current explicit tenant role grant and version; token claim alone is insufficient
   when current state is narrower.
8. Apply producer-owned resource visibility and return non-disclosing 404 where required.

### Permission catalog and provisioning

- `PermissionCatalogManifestV1` registers catalog definitions only. Catalog presence grants no tenant access.
- PSS validates schema, owner identity, collision and scope but never authors a producer permission.
- Every tenant role grant is explicit, tenant-scoped, auditable and attributable to an authorized actor/idempotency
  key. Enable and reconcile create zero automatic grants; no entitlement event may auto-grant Admin, Viewer or any
  other role for Gate I producers.
- Explicit grant provisioning requires an authenticated authorized actor and an idempotency key. Repeating the same
  key with the exact same payload is a stable no-op returning the original result; the same key with a different
  payload is `409` and creates no mutation.
- Grant idempotency identity is exact `(TenantId, AuthenticatedActorId, Operation, IdempotencyKey)` and binds a
  canonical hash of role, permission and requested mutation. AuthService atomically persists the idempotency receipt,
  grant mutation and tenant authorization-version increment as one commit. An indeterminate commit is reconciled by
  that identity and payload hash before retry; it never blindly reapplies. The immutable receipt remains for at least
  the resulting grant/audit record retention period, including after grant removal, so an old key cannot acquire new
  meaning.
- Entitlement removal makes existing explicit grants dormant: rows remain, are excluded from effective claims and
  authorization, and are visible to authorized administrators. Disable/removal never deletes an explicit grant.
  Re-entitlement alone never makes a dormant grant effective and never creates or reconstructs a grant: effective
  authorization requires the current role membership, the still-existing explicit grant and current authorization
  version to be re-evaluated successfully.
- Permission removal/deprecation makes matching grants dormant or invalid under owner-approved migration; silent
  remap and wildcard fallback are forbidden.
- AuthService permission/role changes increment the tenant authorization version immediately. Previously issued
  proofs fail freshness validation; access requires a new proof. Cache invalidation follows FU13 and must reach every
  instance.

## 15. Gateway / API Routing Decision

No Gateway or browser route is authorized. Gate I calls are internal service-to-service contracts. Exact physical
routes, ports, mTLS topology and endpoint ownership must be locked in a separate implementation pack. The logical
token contract is exact now: issuer `diten-auth-service`, receiver-specific audience, protocol scope
`diten.s2s.delegated.invoke`, and owner-manifest operation/permission values. `X-Internal-Api-Key` alone is explicitly
insufficient for delegated actor proof.

## 16. Acceptance Criteria

- [ ] ServicePrincipal lifecycle covers registration, activation, suspension, revocation, retirement and immutable audit.
- [ ] Credential rotation supports overlap without accepting retired generations; emergency revocation denies immediately.
- [ ] Exact issuer, audiences, protocol scope and every required delegated claim match §4.
- [ ] Tenant, delegated actor, operation and request bytes are cryptographically bound; replay tests prove single use.
- [ ] Module entitlement identity and service identity are separate fields and gates.
- [ ] Manifest registration cannot grant access and cannot accept a PSS-invented producer permission.
- [ ] Tenant grants require explicit role-permission provisioning; automatic Admin/Viewer grants are absent.
- [ ] Entitlement removal leaves explicit grants dormant without deleting them; re-entitlement creates no grant and
  cannot make one effective without current role membership, the still-existing explicit grant and current version.
- [ ] Grant/principal/credential change invalidates stale proofs immediately across instances.
- [ ] 400/401/403/404/409/503 classes and ordering pass without existence disclosure or fail-open.
- [ ] Tokens, secrets, credentials, nonce and request bodies are redacted from logs/traces/errors.
- [ ] MDM and every other legacy generic module retain current auto-grant/revoke behavior.
- [ ] PPM retains `ExplicitOnlyPreserveOnEntitlementRemoval`, its exact 16-key contract and no default Admin/Viewer grant.
- [ ] All four producer profiles pass governance closure with exact ModuleCodes and entitlement policies; executable
  onboarding remains blocked only until fixture/runtime evidence proves those decisions.
- [ ] No runtime code/config/seed/gateway change is present in this governance promotion commit.

## 17. Test Expectations

**Identity/token:** exact issuer; each single audience; wrong/generic/multiple audience rejection; scope rejection;
client/sub/azp mismatch; invalid algorithm/kid/signature; expiry/skew; suspended/revoked principal; old credential
generation; rotation overlap and cutoff; separate HS256 user-JWT regression; RS256-only S2S, RSA key-size, exact-kid,
trusted-JWKS and no-static/fallback-key negatives.

**Delegation/replay:** tenant mismatch, actor mismatch, operation mismatch, permission mismatch, request-body/method/path
change, duplicate jti, duplicate nonce, concurrent replay, receipt retention through `exp + 30s`, revoked delegation,
stale tenant-grant version, authority outage/indeterminate 503 and proof that no protected handler/repository ran.

**Catalog/grants:** manifest owner collision, invalid key, permission removal, explicit grant idempotency, conflict,
cross-tenant role, unauthorized provisioner, no Admin/Viewer auto-grant, dormant removal and no-create re-entitlement.

**Regression:** snapshot/behavior tests for all non-PPM generic modules, including MDM; PPM policy and token-claim tests;
catalog presence without grant; current membership only; deleted grant not reconstructed; permission-change invalidation.

**Failure/security:** complete 400/401/403/404/409/503 matrix, dependency indeterminate 503, cross-tenant 404 after
authorization, structured-log capture proving redaction, and no secret/token in exception payloads.

## 18. Ready-for-dev Checklist

- [x] Parent identity and Blueprint canonical name verified.
- [x] FU16 collision check and DCP-002 preflight pass.
- [x] Backend code reality and existing PPM explicit-grant policy inspected.
- [x] Governance-only boundaries and 20 sections present.
- [x] Security Architect approves the governance-level RS256-only asymmetric validation, freshness and atomic
  replay-store design recorded in §§4, 12–14; this grants no runtime, deployment or production authority.
- [x] AuthService owner approves AuthService as SoR for ServicePrincipal, credential metadata, manifest registry and
  explicit tenant-role grant persistence contracts; this grants no runtime, deployment or production authority.
- [x] MOD-0007 owner pack supplies the exact eight-operation / seven-permission manifest at checkpoint `7bdbd37e16c72cd80f081612a104cc3af7e2b4cd`.
- [x] MOD-0136 owner pack supplies the exact fifteen-operation / fifteen-permission manifest at checkpoint `937aabf43683eac9a240f9101ee84c66db55423a`.
- [x] MOD-0138 owner pack supplies the exact sixteen delegated mappings / sixteen permissions plus separate accepted-run worker authority at checkpoint `066d16c80b966a63aaa7430ee8dd14c120e7a4c2`.
- [x] MOD-0072 owner pack supplies the exact nine-operation / seven-permission manifest at checkpoint `5e5088ef6a5298b09b1dfcece9cf10ad2375aa29`.
- [x] Producer owners approve the exact audiences and onboarding client identities recorded below.
- [x] Exact ModuleCodes and mandatory entitlement posture are closed for all four producer profiles: `MOD-0007`,
  `MOD-0136`, `MOD-0138` and `MOD-0072`; all are tenant-assignable, non-baseline, entitlement-gated and
  explicit-grant-only.
- [x] MDM/legacy and PPM regression boundaries and required executable suites are named in §§6, 16–17 and 19.
- [x] Physical routes, mTLS/network controls, vault/HSM keys, deployment retention and operational runbooks are
  explicitly deferred to separately authorized implementation/activation packs rather than inferred here.
- [x] Control Tower human review approves the formal Security Architect/AuthService-owner governance decisions and
  promotes this pack to `ready-for-dev` without runtime authority.

All pre-development design decisions are closed. `ready-for-dev` means design handoff only; it does not authorize
implementation or production use.

### Open implementation/review and production-evidence gates (not ready-for-dev blockers)

- [ ] Executable packs and explicit user authority exist for every runtime change.
- [ ] Fixture/runtime evidence proves each profile's ModuleCode registration, separate entitlement evaluation,
  zero-grant enable/reconcile, grant-preserving disable/removal and guarded dormant-grant effectiveness.
- [ ] Executable MDM/legacy and PPM regression suites pass without behavioral change.
- [ ] Approved vault/HSM generates and protects RSA >=3072-bit production keys; trusted JWKS/validation-key delivery,
  exact `kid`, rotation, revocation and no-fallback behavior are proven.
- [ ] Atomic replay-store concurrency, retention, 401/503 behavior and no-handler-before-validation are proven live.
- [ ] Physical routes, mTLS/network controls, deployment retention, monitoring and operational runbooks pass review.

## 19. Implementation Notes

Current code reality uses human access tokens with `iss = diten-auth-service`, `aud = diten-erp`,
`actor_type = tenant_user`, `tenant_id`, roles and repeated `permission` claims. It has no `jti`, nonce,
request binding, service principal or delegated actor proof. Platform.Common's signed-JWT permission evaluator
requires authenticated principal, non-empty tenant and subject and checks one exact permission claim; its module/
feature handlers currently reject actors other than `tenant_user` (except platform-admin bypass). Existing internal
routes use a shared `X-Internal-Api-Key`; Gate I does not promote that key into service/delegation proof.

The existing user/session JWT implementation signs and validates with HS256 and the configured user-token secret.
That is a protected regression boundary, not the Gate I implementation starting point. Gate I must add a separate
RS256 S2S issuer/validation profile and key source without changing, migrating or sharing secret material with the
existing HS256 user/session flow.

AuthService already has `GrantSource` (`System/Module/Manual`), tenant role-permission rows, a role-assignment version
and a closed `EntitlementPermissionPolicy`. `PPM` resolves to `ExplicitOnlyPreserveOnEntitlementRemoval`; other modules
resolve to `LegacyAutoGrantAndRevoke`. Gate I must extend by additive policy/profile selection and must not change the
default branch. Protocol scope is not a business permission, role or entitlement.

Identity authority is [DCP-002](../../../portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md),
with reservation in the [module ID registry](../../../registries/module-id-registry.md) and Blueprint reconciliation
in the [ledger](../../../portfolio/blueprint-master-plan-reconciliation.md).

### Onboarding profiles

| Service | Blueprint owner modules | Exact audience | Client ID reservation | Producer permission source | Bilateral manifest | Remaining gate |
|---|---|---|---|---|---|---|
| `Diten.ManagementGovernanceService` | `MOD-0007` | `diten-management-governance-service` | `diten.management-governance` | MOD-0007 checkpoint `7bdbd37e16c72cd80f081612a104cc3af7e2b4cd` | ACCEPTED — 8 operations / 7 permissions | **IMPLEMENTATION EVIDENCE OPEN:** fixture/runtime proof absent |
| `Diten.FpaService` / Budgeting | `MOD-0136` | `diten-fpa-service` | `diten.fpa` | MOD-0136 checkpoint `937aabf43683eac9a240f9101ee84c66db55423a` | ACCEPTED — 15 operations / 15 permissions | **IMPLEMENTATION EVIDENCE OPEN:** fixture/runtime proof absent |
| `Diten.FpaService` / ScenarioPlanning | `MOD-0138` | `diten-fpa-service` | `diten.fpa` | MOD-0138 checkpoint `066d16c80b966a63aaa7430ee8dd14c120e7a4c2` | ACCEPTED — 16 delegated mappings / 16 permissions plus accepted-run worker authority | **IMPLEMENTATION EVIDENCE OPEN:** fixture/runtime proof absent |
| `Diten.DecisionIntelligenceService` | `MOD-0072` | `diten-decision-intelligence-service` | `diten.decision-intelligence` | MOD-0072 checkpoint `5e5088ef6a5298b09b1dfcece9cf10ad2375aa29` | ACCEPTED — 9 operations / 7 permissions | **IMPLEMENTATION EVIDENCE OPEN:** fixture/runtime proof absent |

These client IDs and audiences identify workloads/receivers only. A ModuleCode is neither a service client ID nor an
audience alias, and none of these values confers a producer permission. Shared FPA audience/client values do not merge
entitlement gates or ownership: MOD-0136 owns only `budgeting.*` and MOD-0138 owns only
`fpa.scenario-planning.*`. The producer manifests use disjoint operation and permission namespaces;
no exact operation ID, permission key or owner-module collision was found across the four checkpoints. PSS accepts
the owner-authored values below verbatim and invents none.

### Bilaterally accepted owner manifests

All operation and permission comparisons are ordinal, case-sensitive and exact. Wildcard, prefix, trim, case-fold,
alias and fallback matching remain forbidden. Catalog registration grants no tenant access; an explicit current
tenant role grant and the complete FU16 `DelegatedActorProofV1` remain mandatory. No manifest below authorizes an
automatic Admin/Viewer grant. The protocol scope for every delegated entry is exactly
`diten.s2s.delegated.invoke`.

#### MOD-0007 — Decision & Rationale Log

| Exact `operation_id` | Exact permission |
|---|---|
| `decision-registry.decisions.read.v1` | `management-governance.decisions.read` |
| `decision-registry.drafts.create.v1` | `management-governance.decisions.create` |
| `decision-registry.drafts.revise.v1` | `management-governance.decisions.revise` |
| `decision-registry.drafts.soft-delete.v1` | `management-governance.decisions.revise` |
| `decision-registry.drafts.publish.v1` | `management-governance.decisions.publish` |
| `decision-registry.decisions.supersede.v1` | `management-governance.decisions.supersede` |
| `decision-registry.decisions.withdraw.v1` | `management-governance.decisions.withdraw` |
| `decision-registry.decision-references.validate.v1` | `management-governance.decision-references.validate` |

Distinct permission set: exactly seven values represented above. The binding ModuleCode is `MOD-0007`; it is
tenant-assignable, non-baseline, entitlement-gated and explicit-grant-only for every listed operation. **Profile
result: GOVERNANCE PASS / runtime execution BLOCKED:** fixture/runtime evidence must prove the closed entitlement policy.

#### MOD-0136 — Budgeting

| Exact `operation_id` | Exact permission |
|---|---|
| `budgeting.budgets.read` | `budgeting.budgets.read` |
| `budgeting.budgets.create` | `budgeting.budgets.create` |
| `budgeting.budgets.update` | `budgeting.budgets.update` |
| `budgeting.budgets.archive` | `budgeting.budgets.archive` |
| `budgeting.budget-version-drafts.read` | `budgeting.budget-version-drafts.read` |
| `budgeting.budget-version-drafts.create` | `budgeting.budget-version-drafts.create` |
| `budgeting.budget-version-drafts.update` | `budgeting.budget-version-drafts.update` |
| `budgeting.budget-version-drafts.abandon` | `budgeting.budget-version-drafts.abandon` |
| `budgeting.budget-versions.read` | `budgeting.budget-versions.read` |
| `budgeting.budget-versions.certify` | `budgeting.budget-versions.certify` |
| `budgeting.budget-versions.retire` | `budgeting.budget-versions.retire` |
| `budgeting.funding-baseline-selections.read` | `budgeting.funding-baseline-selections.read` |
| `budgeting.funding-baseline-selections.replace` | `budgeting.funding-baseline-selections.replace` |
| `budgeting.funding-baseline-selections.close` | `budgeting.funding-baseline-selections.close` |
| `budgeting.budget-version-references.validate` | `budgeting.budget-version-references.validate` |

The binding ModuleCode is `MOD-0136`; it is tenant-assignable, non-baseline, entitlement-gated and
explicit-grant-only for all fifteen operations. Its entitlement gate is independent from `MOD-0138`, including when
both run in `Diten.FpaService`. **Profile result: GOVERNANCE PASS / runtime execution BLOCKED:** fixture/runtime evidence must
prove the closed entitlement policy and the independent FPA gate.

#### MOD-0138 — Scenario Planning

| Exact delegated `operation_id` | Exact permission | Execution rule |
|---|---|---|
| `fpa.scenario-planning.scenarios.read` | `fpa.scenario-planning.scenarios.read` | delegated |
| `fpa.scenario-planning.scenarios.create` | `fpa.scenario-planning.scenarios.create` | delegated |
| `fpa.scenario-planning.scenarios.update` | `fpa.scenario-planning.scenarios.update` | delegated |
| `fpa.scenario-planning.version-drafts.read` | `fpa.scenario-planning.version-drafts.read` | delegated |
| `fpa.scenario-planning.version-drafts.create` | `fpa.scenario-planning.version-drafts.create` | delegated |
| `fpa.scenario-planning.version-drafts.update` | `fpa.scenario-planning.version-drafts.update` | delegated |
| `fpa.scenario-planning.version-drafts.abandon` | `fpa.scenario-planning.version-drafts.abandon` | delegated |
| `fpa.scenario-planning.versions.read` | `fpa.scenario-planning.versions.read` | delegated |
| `fpa.scenario-planning.versions.publish` | `fpa.scenario-planning.versions.publish` | delegated |
| `fpa.scenario-planning.versions.retire` | `fpa.scenario-planning.versions.retire` | delegated |
| `fpa.scenario-planning.comparators.read` | `fpa.scenario-planning.comparators.read` | delegated |
| `fpa.scenario-planning.comparators.run` | `fpa.scenario-planning.comparators.run` | delegated request; accepted immutable run may later use worker authority below |
| `fpa.scenario-planning.selections.read` | `fpa.scenario-planning.selections.read` | delegated |
| `fpa.scenario-planning.selections.replace` | `fpa.scenario-planning.selections.replace` | delegated |
| `fpa.scenario-planning.selections.close` | `fpa.scenario-planning.selections.close` | delegated |
| `fpa.scenario-planning.references.validate` | `fpa.scenario-planning.references.validate` | delegated S2S |

Separate worker authority is exact operation `fpa.scenario-planning.comparators.execute`. It is not a seventeenth
tenant permission and cannot submit a run or acquire the delegated actor's permission; it may process only an already
accepted immutable comparator run carrying the original actor/request binding. The binding ModuleCode is `MOD-0138`;
it is tenant-assignable, non-baseline, entitlement-gated and explicit-grant-only for the delegated acceptance path.
Its entitlement gate is independent from `MOD-0136`, including when both run in `Diten.FpaService`; worker execution
cannot bypass the entitlement decision captured at accepted-run creation. **Profile result: GOVERNANCE PASS /
runtime execution BLOCKED:** fixture/runtime evidence must prove the closed entitlement policy, independent FPA gate and
worker-after-acceptance enforcement.

#### MOD-0072 — Decision Logs & Outcome Tracking

| Exact `operation_id` | Exact permission |
|---|---|
| `outcome-tracking.outcomes.read` | `decision-intelligence.outcomes.read` |
| `outcome-tracking.outcomes.create` | `decision-intelligence.outcomes.create` |
| `outcome-tracking.outcomes.publish-version` | `decision-intelligence.outcomes.version` |
| `outcome-tracking.outcomes.retire` | `decision-intelligence.outcomes.version` |
| `outcome-tracking.measurements.append` | `decision-intelligence.measurements.append` |
| `outcome-tracking.measurements.correct` | `decision-intelligence.measurements.correct` |
| `outcome-tracking.decision-links.create` | `decision-intelligence.decision-links.manage` |
| `outcome-tracking.decision-links.retire` | `decision-intelligence.decision-links.manage` |
| `outcome-tracking.outcome-references.validate` | `decision-intelligence.outcome-references.validate` |

Distinct permission set: exactly seven values represented above. The binding ModuleCode is `MOD-0072`; it is
tenant-assignable, non-baseline, entitlement-gated and explicit-grant-only for all nine operations. **Profile result:
GOVERNANCE PASS / runtime execution BLOCKED:** fixture/runtime evidence must prove the closed entitlement policy.

### Collision and regression disposition

- Owner IDs are exact and distinct: MOD-0007, MOD-0136, MOD-0138 and MOD-0072.
- Operation namespaces are disjoint: `decision-registry.*`, `budgeting.*`, `fpa.scenario-planning.*` and
  `outcome-tracking.*`.
- Permission namespaces/sets are disjoint. Shared FPA service identity does not merge MOD-0136 and MOD-0138
  manifests, ownership, permissions or entitlement decisions.
- No exact operation ID or permission key is registered by two owner modules in these checkpoints.
- PPM remains `ExplicitOnlyPreserveOnEntitlementRemoval`; its exact Phase 2A grant behavior is unchanged.
- MDM and every other legacy module remain on the existing generic auto-grant/revoke branch; FU16 reconciliation
  changes no runtime dispatch, seed or default-role behavior.

## 20. Follow-up Items

1. Executable owner/AuthService packs supply fixture/runtime evidence for the four closed ModuleCode profiles,
   including independent `MOD-0136`/`MOD-0138` FPA gates, zero automatic grant on enable/reconcile, grant preservation
   on disable/removal, and current membership/grant/version checks before any dormant grant can become effective.
2. Security Architecture carries the approved RS256/RSA >=3072, trusted JWKS, exact-kid and atomic-replay contract
   into executable security/operations packs; mTLS binding and emergency revoke SLO remain activation evidence.
3. AuthService owner prepares separate executable slices for the approved registry/token, credential-metadata,
   manifest-registration and explicit-grant persistence contracts.
4. Platform.Common owner decides the minimal additive protocol contract without weakening tenant-user handlers.
5. Producer owners prepare separate onboarding implementations and negative interoperability tests.
6. Integration owner defines internal routes/network policy only after service contracts are approved; no Gateway change is implied.
7. Operations owner defines rotation, compromise response, redaction verification, audit retention and 503 alerting.
8. PPM and MDM owners sign regression evidence before Gate I production rollout.

### Gate I Model A amendment — Auth FU16-B2B Attestation Consumer Enforcement

Control Tower selects **Model A**: FU16-B2B consumes the short-lived signed Platform entitlement attestation defined
by [CAND-CAP-0002-FU05](CAND-CAP-0002-FU05-tenant-module-entitlements.md). This is a bounded-stale design with an
absolute maximum accepted stale window of 15 seconds; it is not and must never be represented as zero-stale. Model B
or a claim that Platform entitlement state participates in the Auth MongoDB transaction is outside this decision.

The bounded implementation slice is named **Auth FU16-B2B Attestation Consumer Enforcement**. It may implement only
the following additive enforcement contract:

1. `platform.entitlement-attestation` version `1.0` is a dedicated entitlement-attestation token/contract family.
   It must not be issued, accepted, logged or relabeled as `DelegatedActorProofV1`; neither validation scheme may
   fall through to the other.
2. The consumer requires exact issuer `diten-platform-service`, sole audience `diten-auth-service`, protected header
   `typ=diten-entitlement-attestation+jwt`, `alg=RS256`, one exact trusted active `kid`, and exact claims
   `contract_id=platform.entitlement-attestation` and `contract_version=1.0`. Wrong family, issuer, audience, type,
   algorithm, key identity, signature or version is rejected before authorization.
3. The attestation `tenant_id`, normalized `module_code` and canonical `request_hash` must exactly equal the
   server-derived tenant, owner-profile ModuleCode and FU16 canonical request hash. Alias, wildcard, trim,
   case-fold-at-validation, alternate serialization and request replay do not match.
4. The complete `EntitlementStateVersionV1` vector and hard `valid_until_utc` boundary are mandatory. All three
   monotonic components must be trustworthy, comparable and no older than the consumer's observed fence.
   `valid_until_utc` may not be extended by clock skew. Missing, incomparable or unverifiable version authority,
   invalid signature/family, or an expired attestation can never authorize execution.
5. Only `Allowed` proceeds to the remaining FU16 gates. Authoritative `Missing`, `Disabled`, `Expired` or
   `NotApplicable` maps to `403`. Provider unavailable/timeout/malformed/indeterminate, invalid or expired
   attestation, and unavailable/indeterminate trustworthy key or version authority map to `503`, except that a
   cryptographically verified authentication/identity failure attributable to supplied credentials or bytes
   remains the existing pack's `401` class. Specifically wrong issuer/audience/typ/alg/kid, bad signature or
   TenantId/ModuleCode/request-hash binding is `401`; inability to obtain trustworthy key/version/provider state is
   `503`. No protected handler runs in either case.
6. Cache and last-known-good data must never produce allow. A cache may retain only an already verified attestation
   under its exact tenant/module/request-hash/version key and only until its unextended `valid_until_utc`; stale,
   superseded, incomparable, expired or invalidated entries are rejected. There is no offline allow mode.
7. During the local Auth authorization transaction, service principal, credential generation, delegated actor's
   current membership, still-existing explicit grant, authorization version and replay receipt consumption are read
   or written under one Auth snapshot/fence. Any changed component aborts and retries or fails closed; token claims
   alone are insufficient. The verified Platform attestation is immutable external input to this transaction, not a
   Platform record enlisted in it.
8. Although the common Mongo client is now Standard, a dedicated session may reach common collections only through
   the exact allowlisted wrapper `IFu16AuthorizationTransactionSession`. Its closed operations are principal read,
   credential-generation read, actor-membership read, explicit-role-grant read, authorization-version read and
   replay-receipt insert. Activation requires a BSON serializer/representation compatibility gate for every shared
   identifier/version field and executable proof that every operation uses the same `IClientSessionHandle` and the
   same underlying `MongoClient`; arbitrary common repository access or a second client/session is forbidden.
9. Platform entitlement state, its version vector and its signing-key lifecycle remain Platform-owned and are not
   part of the Auth Mongo transaction. Model A supplies only signed bounded-stale evidence; transaction language,
   diagrams and tests must not imply cross-service ACID or zero-stale entitlement revocation.
10. Offline UUID representation migration remains a separate deployment blocker. No online fallback, dual-format
    comparison or implicit conversion is authorized. Production activation stays blocked until the separately
    approved migration proves backup/restore, full collection conversion, BSON compatibility and rollback evidence.

**Slice acceptance and evidence boundary:** tests must cover exact family/header/claim validation, all four
authoritative deny results as `403`, the `401` identity failures above, every uncertainty/expiry/version failure as
`503`, no cache/LKG allow, the 15-second hard boundary, vector rollback/incomparability, same-session/same-client
transaction evidence, snapshot changes across every local authorization component, and the offline UUID blocker.
Existing PPM `ExplicitOnlyPreserveOnEntitlementRemoval`, its exact Phase 2A grant set, deny behavior and dormant-grant
semantics remain unchanged.

This amendment is `ready-for-dev` governance only. `execution_authority: none`, `production_authority: none` and
`execution_activation: none` remain binding. It creates no endpoint/runtime code, route, credential, key, migration
or deployment and grants no production activation authority.
