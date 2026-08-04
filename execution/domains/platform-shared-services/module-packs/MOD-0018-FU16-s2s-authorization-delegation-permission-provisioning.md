---
id: MOD-0018-FU16
name: S2S Authorization, Delegation and Permission Provisioning
domain: platform-shared-services
service: Diten.AuthService
shell: none
golden_reference: none
entity_base: GlobalEntity
status: draft
owner: platform-team / security-architect
branch: feature/pss/mod-0018-gate-i-s2s
started: 2026-08-04
target: ""
form_field_count: 0
parent_module: MOD-0018
execution_authority: none
---

# MOD-0018-FU16 — S2S Authorization, Delegation and Permission Provisioning

> **GOVERNANCE-ONLY / NON-EXECUTABLE.** This draft defines the Gate I contract. It authorizes no runtime,
> AuthService, Platform.Common, producer-service, frontend, gateway, seed, migration or deployment change. It
> must be reviewed and promoted to `approved` or `ready-for-dev`, followed by separately authorized execution
> packs, before implementation begins.

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

No runtime names above are authorized until an implementation pack locks persistence placement and retention.

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
- `ModuleCode`/module entitlement identity is commercial capability identity. `ServicePrincipalId`/`ClientId` is
  workload identity. They are never aliases and are never compared as strings to infer access.
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
| Credential | Signature valid, accepted algorithm/key id, active generation, not expired/revoked, rotation overlap valid |
| JWT header | Approved asymmetric algorithm only for production; `kid` required; `none` and algorithm confusion rejected |
| JWT time | Required `iat/nbf/exp`; maximum five-minute lifetime and 30-second skew |
| Delegation | Exact tenant, delegated actor, operation, permission and request hash; actor is active/referenceable |
| Replay | Atomic first-write of issuer+jti and nonce binding; any duplicate or changed request is rejected |
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
  key. No entitlement event may auto-grant Admin or Viewer for Gate I producers.
- Entitlement removal makes existing explicit grants dormant: rows remain, are excluded from effective claims and
  authorization, and are visible to authorized administrators. Re-entitlement reactivates only still-existing grants
  for current role memberships; it creates no grant and reconstructs none that was removed.
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
- [ ] Entitlement removal leaves explicit grants dormant; re-entitlement creates no new grant.
- [ ] Grant/principal/credential change invalidates stale proofs immediately across instances.
- [ ] 400/401/403/404/409/503 classes and ordering pass without existence disclosure or fail-open.
- [ ] Tokens, secrets, credentials, nonce and request bodies are redacted from logs/traces/errors.
- [ ] MDM and every other legacy generic module retain current auto-grant/revoke behavior.
- [ ] PPM retains `ExplicitOnlyPreserveOnEntitlementRemoval`, its exact 16-key contract and no default Admin/Viewer grant.
- [ ] Three onboarding profiles pass, while missing owner permission lists block executable onboarding.
- [ ] No runtime code/config/seed/gateway change is present in this draft authoring commit.

## 17. Test Expectations

**Identity/token:** exact issuer; each single audience; wrong/generic/multiple audience rejection; scope rejection;
client/sub/azp mismatch; invalid algorithm/kid/signature; expiry/skew; suspended/revoked principal; old credential
generation; rotation overlap and cutoff.

**Delegation/replay:** tenant mismatch, actor mismatch, operation mismatch, permission mismatch, request-body/method/path
change, duplicate jti, duplicate nonce, concurrent replay, revoked delegation, stale tenant-grant version and authority
outage.

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
- [ ] Security Architect approves asymmetric signing/validation and replay-store design.
- [ ] AuthService owner approves ServicePrincipal, manifest and explicit-grant persistence contracts.
- [ ] MOD-0007 owner pack supplies exact permission and operation manifest.
- [ ] MOD-0136 owner pack supplies exact permission and operation manifest.
- [ ] MOD-0138 owner pack supplies exact permission and operation manifest.
- [ ] MOD-0072 owner pack supplies exact permission and operation manifest.
- [ ] Producer owners approve audiences and onboarding client identities.
- [ ] MDM/legacy and PPM regression suites are named in executable implementation packs.
- [ ] Physical routes, mTLS/network controls, vault keys, retention and operational runbooks are approved.
- [ ] Human review promotes status to `approved` or `ready-for-dev`.

Until every unchecked item is closed, this pack is NON-EXECUTABLE and runtime work is blocked.

## 19. Implementation Notes

Current code reality uses human access tokens with `iss = diten-auth-service`, `aud = diten-erp`,
`actor_type = tenant_user`, `tenant_id`, roles and repeated `permission` claims. It has no `jti`, nonce,
request binding, service principal or delegated actor proof. Platform.Common's signed-JWT permission evaluator
requires authenticated principal, non-empty tenant and subject and checks one exact permission claim; its module/
feature handlers currently reject actors other than `tenant_user` (except platform-admin bypass). Existing internal
routes use a shared `X-Internal-Api-Key`; Gate I does not promote that key into service/delegation proof.

AuthService already has `GrantSource` (`System/Module/Manual`), tenant role-permission rows, a role-assignment version
and a closed `EntitlementPermissionPolicy`. `PPM` resolves to `ExplicitOnlyPreserveOnEntitlementRemoval`; other modules
resolve to `LegacyAutoGrantAndRevoke`. Gate I must extend by additive policy/profile selection and must not change the
default branch. Protocol scope is not a business permission, role or entitlement.

Identity authority is [DCP-002](../../../portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md),
with reservation in the [module ID registry](../../../registries/module-id-registry.md) and Blueprint reconciliation
in the [ledger](../../../portfolio/blueprint-master-plan-reconciliation.md).

### Onboarding profiles

| Service | Blueprint owner modules | Exact audience | Client ID reservation | Producer permission source | Gate |
|---|---|---|---|---|---|
| `Diten.ManagementGovernanceService` | `MOD-0007` | `diten-management-governance-service` | `diten.management-governance` | MOD-0007 owner pack manifest | BLOCKED until owner list exists |
| `Diten.FpaService` | `MOD-0136`, `MOD-0138` | `diten-fpa-service` | `diten.fpa` | Separate MOD-0136 and MOD-0138 owner manifests | BLOCKED until both applicable lists exist |
| `Diten.DecisionIntelligenceService` | `MOD-0072` | `diten-decision-intelligence-service` | `diten.decision-intelligence` | MOD-0072 owner pack manifest | BLOCKED until owner list exists |

These client IDs and audiences identify workloads/receivers only. They do not confer module entitlement or any
producer permission. No producer permission key is listed because no authoritative owner pack exists in current code
reality; inventing one in PSS would violate the ownership boundary.

## 20. Follow-up Items

1. Each producer owner authors/approves its own module pack or FU containing the exact operation-to-permission manifest.
2. Security Architecture locks asymmetric key type, JWKS/discovery, mTLS binding, replay retention and emergency revoke SLO.
3. AuthService owner prepares separate executable slices for registry/token, manifest registration and explicit grants.
4. Platform.Common owner decides the minimal additive protocol contract without weakening tenant-user handlers.
5. Producer owners prepare separate onboarding implementations and negative interoperability tests.
6. Integration owner defines internal routes/network policy only after service contracts are approved; no Gateway change is implied.
7. Operations owner defines rotation, compromise response, redaction verification, audit retention and 503 alerting.
8. PPM and MDM owners sign regression evidence before Gate I production rollout.
