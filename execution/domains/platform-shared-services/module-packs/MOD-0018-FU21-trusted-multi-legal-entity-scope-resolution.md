---
id: MOD-0018-FU21
name: Trusted Multi-Legal-Entity Scope Resolution
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: N/A
status: review
owner: platform-security-owner
branch: feature/pss/mod-0018-fu21-trusted-legal-entity-scope
started: 2026-08-26
target: 2026-09-30
form_field_count: 0
parent: MOD-0018
consumer: MOD-0290-FU03
---

# MOD-0018-FU21 — Trusted Multi-Legal-Entity Scope Resolution

> **Delivery guard.** The exact Section 5 implementation was user-authorized on 2026-08-26 and is now in `review` with
> test/build evidence below. This status creates no configuration, secret, tenant, permission, data, process, branch,
> stage, commit or push authority. Secret provisioning and any operational smoke remain a later, separate gate.
> The user accepted the implementation result on 2026-08-26; `review` is retained because operational secret
> provisioning and live smoke are still separately gated and incomplete.
>
> **DCP-002 preflight.** The canonical parent is Master 8.1 `MOD-0018 — RBAC / ABAC Authorization`. Before this file
> was written, the exact command
> `verify_module_id.py . --check-id MOD-0018-FU21 --name "Trusted Multi-Legal-Entity Scope Resolution" --parent MOD-0018`
> returned `OK MOD-0018-FU21: proven against Blueprint/registry`; no pack or registry collision existed. The legacy
> verifier is mechanical compatibility evidence only; Master 8.1 remains the business/model authority.
>
> **Backend-only decision.** This follow-up owns no persisted entity and no UI. `entity_base: N/A`, `shell: none`, `golden_reference: none` and
> `form_field_count: 0` are intentional.

## 1. Module Summary

FU21 provides a generic Platform-owned, MDM-independent candidate Legal Entity scope resolution boundary for trusted
tenant-service consumers. It closes the current code-truth drift: `OrgDataScopeResolver` can derive multiple Legal
Entity candidates, while `JwtTenantAuthorizationContext` retains only the first value in its singular
`LegalEntityId`. FU21 does not widen that context and does not put scope IDs into JWT claims. It introduces a separate
internal endpoint that returns the full bounded candidate set for the authenticated human subject.

The exact endpoint is:

`POST /api/internal/v1/access-governance/legal-entity-scope/resolve`

The caller supplies only `module_code` and `permission_key`. Platform independently validates the delegated Bearer,
derives one tenant and one subject, requires actor type `tenant_user`, checks the exact requested permission, validates
a dedicated rotatable service credential bound to `DITENMDMSERVICE`, an exact audience and an allowed
module/permission pair, then resolves Platform-owned Organization/Position facts. The response is bounded, sorted and
distinct. A successful empty set is authoritative `200`, not an availability error.

## 2. Ownership and Boundaries

**FU21 / Platform-Security owns:**

- MDM-independent Organization/Position candidate resolution extracted from the existing FU15 implementation.
- Dedicated internal transport authentication: delegated Bearer plus separately rotatable MDM service credential.
- Exact module/permission pair restriction, strict request contract, two-second budget, tenant-scope restoration and
  request-scope-only memoization.
- A bounded candidate response containing the full multi-Legal-Entity set without truncation.

**Consumed but not owned:**

- MOD-0288 Organization Unit, Position and Position Assignment repositories in `Diten.Platform`.
- MOD-0018/FU13 JWT permission issuance and bounded-staleness behavior.
- Existing FU15 `OrgDataScopeResolver`; it may continue validating Legal Entity referenceability through MDM for its
  legacy in-process context consumer, but candidate extraction itself must not call MDM.

**Explicitly out of scope:**

- MDM adapter/client code, FU03 product-scope policy, product filtering, assignments, grants, UI or migration.
- Auth permission catalog, role/default-role, entitlement reconciliation or token issuance changes.
- Changes to `ITenantAuthorizationContext` or singular `JwtTenantAuthorizationContext.LegalEntityId`.
- JWT Legal Entity claims, signed scope tokens, browser-provided scope, cross-request cache and service-account scope.
- Legal Entity lifecycle/referenceability authority; the future MDM consumer must locally revalidate candidates.
- Public/Gateway exposure, navigation, appsettings values, committed secrets, Production enablement.

## 3. Owned Objects

| Object | Owner | Exact invariant |
|---|---|---|
| `IOrgDataScopeCandidateResolver` | Platform Application | Resolves Platform-only candidate facts; no HTTP/MDM dependency |
| `OrgDataScopeCandidateResolver` | Platform Application | Scoped, request-memoized, tenant-isolated Organization/Position resolver |
| `OrgDataScopeCandidateSet` | Platform Application | Bounded candidate facts; Legal Entity IDs are not MDM-referenceability proof |
| `ResolveTrustedLegalEntityScopeQuery` | Platform Application | Server-created tenant/subject plus exact body module/permission |
| `ResolveTrustedLegalEntityScopeHandler` | Platform Application | Returns sorted distinct Legal Entity IDs or fail-closed error |
| Strict resolve request/parser | Platform API | Accepts only exact `module_code` and `permission_key`; detects duplicates/extras |
| Dedicated credential options/authenticator | Platform API Security | Active/previous rotation, revocation, service/audience/pair binding |
| Dedicated JWT context | Platform API Security | Independently validates Bearer, exact one tenant/sub/actor and exact permission |
| Request executor | Platform API Security | Credential-first, JWT-second, tenant scope, two-second budget, restoration |
| Internal controller | Platform API | Exact POST route only; no public/Gateway route |

No entity, Mongo collection, index, migration, seed, permission or role is introduced.

## 4. Entity Fields

Entity schema is N/A. The transport contract is authoritative:

### Exact request body

| JSON field | Type | Required | Rule |
|---|---|---:|---|
| `module_code` | string | yes | Canonical lowercase module code, 3..100 characters; exact credential-allowed pair member |
| `permission_key` | string | yes | Canonical lowercase dotted key, 1..200 characters; exact JWT token and credential-pair match |

No tenant ID, subject/user ID, Legal Entity ID, actor, audience, credential or additional field is accepted in the
body. Duplicate JSON property names are invalid rather than last-value-wins. The complete UTF-8 request body is bounded
to 1,024 bytes before JSON binding.

### Exact successful response data

| Field | Type | Rule |
|---|---|---|
| `TenantId` | `Guid` | Derived from exactly one independently validated `tenant_id` claim |
| `SubjectId` | `Guid` | Derived from exactly one independently validated `sub` claim |
| `ModuleCode` | string | Exact validated request value |
| `PermissionKey` | string | Exact validated request value |
| `EvaluatedAtUtc` | `DateTimeOffset` | Server-generated UTC instant |
| `LegalEntityIds` | `IReadOnlyList<Guid>` | Sorted ordinal by canonical lowercase `Guid.ToString("D")`, distinct, non-empty GUIDs, maximum 200 |

The maximum Legal Entity candidate count is exactly 200. It is a compile/startup-validated hard bound; an absent, zero,
negative, greater or weakened bound fails before serving requests. `200` with `LegalEntityIds = []` means confirmed
empty. Candidate repository/storage unavailability returns 503 and never becomes confirmed empty. A malformed,
duplicate, unsorted, over-bound or internally inconsistent candidate contract returns 409 at the Platform boundary;
the future MDM provider adapter must map that provider inconsistency to fail-closed 503. The internal two-second budget
returns 504, while caller cancellation propagates unchanged.

### Dedicated credential facts

| Fact | Rule |
|---|---|
| Identifier | Non-secret opaque exact value |
| Active secret | Secure binding only; timing-safe comparison |
| Previous secret | Optional; valid only while `UtcNow < PreviousValidUntilUtc` |
| Revocation | Rejects active and previous immediately |
| Consumer service | Exact `DITENMDMSERVICE` |
| Audience | Exact `TRUSTED_LEGAL_ENTITY_SCOPE_RESOLVE` |
| Allowed pairs | Explicit bounded `(module_code, permission_key)` subset; no wildcard/prefix/alias |

## 5. Repo Scope

This planning task may create only this pack. A later separately authorized implementation is restricted to the exact
paths below; no directory wildcard is implied.

**Platform Application candidate extraction and existing FU15 adaptation:**

- `services/Diten.Platform/src/Diten.Platform.Application/Authorization/IOrgDataScopeCandidateResolver.cs` — new;
  candidate contract, typed unavailable contract and persistence-availability classifier abstraction.
- `services/Diten.Platform/src/Diten.Platform.Application/Authorization/OrgDataScopeCandidateSet.cs` — new.
- `services/Diten.Platform/src/Diten.Platform.Application/Authorization/OrgDataScopeCandidateResolver.cs` — new;
  Platform repositories only, no MDM validator/client.
- `services/Diten.Platform/src/Diten.Platform.Application/Authorization/OrgDataScopeResolver.cs` — delegate raw
  candidate derivation, retain its live MDM referenceability validation for the legacy FU15 consumer.
- `services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs` — scoped registrations only.

**R1 bounded persistence reads (2026-08-30 pre-acceptance audit):**

- `services/Diten.Platform/src/Diten.Platform.Application/Authorization/IOrgDataScopeCandidateFactReader.cs` — new,
  FU21-specific bounded read projection contract.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/OrgDataScopeCandidateFactReader.cs`
  — new, tenant-scoped server-side filter/projection/limit implementation.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/DependencyInjection.cs` — scoped fact-reader registration
  only.

R1 is required because applying `Take(201)` after `GetAllAsync` bounded only the returned result, not repository I/O or
memory. The shared Organization CRUD repository contracts remain untouched. Existing tenant-first Organization
indexes and Mongo `_id` indexes are reused; no speculative index is introduced. Real-Mongo coverage must prove the
current BSON array representation of `DateTimeOffset` evaluates `[EffectiveFrom, EffectiveTo)` by UTC instant across
different offsets rather than relying on Mongo's raw array ordering.

**Platform Application query surface:**

- `services/Diten.Platform/src/Diten.Platform.Application/Features/AccessGovernance/LegalEntityScopeResolution/LegalEntityScopeResolutionModels.cs` — new.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/AccessGovernance/LegalEntityScopeResolution/TrustedLegalEntityScopeResolutionLimits.cs` — new after owner locks the exact bound.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/AccessGovernance/LegalEntityScopeResolution/Queries/ResolveTrustedLegalEntityScopeQuery.cs` — new.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/AccessGovernance/LegalEntityScopeResolution/Handlers/QueryHandlers/ResolveTrustedLegalEntityScopeHandler.cs` — new.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/AccessGovernance/LegalEntityScopeResolution/Validators/ResolveTrustedLegalEntityScopeValidator.cs` — new.

**Platform API strict transport and security:**

- `services/Diten.Platform/src/Diten.Platform.API/Configuration/TrustedLegalEntityScopeCredentialOptions.cs` — new;
  definitions only, never secret values.
- `services/Diten.Platform/src/Diten.Platform.API/Security/ITrustedLegalEntityScopeCredentialAuthenticator.cs` — new.
- `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedLegalEntityScopeCredentialAuthenticator.cs` — new.
- `services/Diten.Platform/src/Diten.Platform.API/Security/ITrustedLegalEntityScopeJwtContext.cs` — new.
- `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedLegalEntityScopeJwtContext.cs` — new.
- `services/Diten.Platform/src/Diten.Platform.API/Security/ITrustedLegalEntityScopeRequestExecutor.cs` — new.
- `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedLegalEntityScopeRequestExecutor.cs` — new; ordered
  request executor plus composition-layer narrow Mongo availability classifier adapter.
- `services/Diten.Platform/src/Diten.Platform.API/Models/AccessGovernance/TrustedLegalEntityScopeResolveRequest.cs` — new.
- `services/Diten.Platform/src/Diten.Platform.API/Models/AccessGovernance/TrustedLegalEntityScopeResolveRequestParser.cs` — new, duplicate/additional-property detection.
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/Internal/InternalLegalEntityScopeResolutionController.cs` — new.
- `services/Diten.Platform/src/Diten.Platform.API/Program.cs` — options and scoped/singleton security registrations only.

**Exact tests:**

- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/OrgDataScopeCandidateResolverTests.cs` — new.
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/OrgDataScopeResolverTests.cs` — FU15 regression.
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/DataScopeResolverRegistrationTests.cs` — scoped wiring regression.
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/TenantAuthorizationContextDataScopeIntegrationTests.cs` — singular-context non-regression only.
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/ResolveTrustedLegalEntityScopeHandlerTests.cs` — new.
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/TrustedLegalEntityScopeCredentialAuthenticatorTests.cs` — new.
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/TrustedLegalEntityScopeJwtContextTests.cs` — new.
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/TrustedLegalEntityScopeRequestExecutorTests.cs` — new.
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/TrustedLegalEntityScopeResolveRequestParserTests.cs` — new.
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/InternalLegalEntityScopeResolutionAuthorizationTests.cs` — new.
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/TrustedLegalEntityScopeResolutionMongoTests.cs` — new, real `localhost:27017` Platform org facts.
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/DependencyInjectionSmokeTests.cs` — registration and no-hosted-service regression.

## 6. Protected Paths

- `.antigravity/**`, `AGENTS.md`, Blueprint workbooks, DCPs, registry, roadmap and unrelated packs.
- `services/Diten.MdmService/**`, including the future FU03 adapter, policy, repositories, handlers and tests.
- `services/Diten.AuthService/**`; token issuance, permission catalog, grants, roles and entitlements are unchanged.
- `gateway/**`, `frontend/**`, navigation/menu/layout and personalization files.
- Every committed `appsettings*.json`, launch profile, environment file, certificate and secret-store material.
- `services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/ITenantAuthorizationContext.cs`.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Authorization/JwtTenantAuthorizationContext.cs`.
- Existing verified GSKU/Market credential, JWT context, executor, controllers and options; they are patterns and
  regression evidence, not a shared credential or mutation surface.
- Platform org entities, shared repositories, indexes and CRUD remain protected. Only the FU21-specific R1 read
  projection and its exact DI/test paths listed in Section 5 may change.
- Branch, stage, commit, push, process restart and operational secret/data mutation.

## 7. Dependencies

| Dependency | Exact use |
|---|---|
| MOD-0018-FU15 | Existing FU15 resolver semantics and Platform org-scope source |
| MOD-0288 | Organization Unit, Position, effective Position Assignment and tenant isolation |
| MOD-0018-FU13 | Exact permission claims and bounded token staleness |
| FU16 verified resolver pattern | Credential rotation/revocation, independent Bearer validation, two-second scope and restoration pattern only |
| MOD-0290-FU03 | Future consumer; owns local Legal Entity referenceability and Product policy intersection |

The critical dependency direction is one-way: Platform candidate resolution reads only Platform persistence. It must not
call MDM. The later MDM consumer calls Platform, then locally revalidates each returned ID against current-tenant active,
non-deleted Legal Entity code truth. This prevents `MDM -> Platform -> MDM` callback loops.

## 8. Runtime Constraints

- Authentication order is credential first, delegated Bearer second, strict request third, candidate resolution last.
- The dedicated credential proves service/audience/allowed-pair only; it never chooses tenant, subject or Legal Entity.
- Tenant and subject come only from the independently authenticated Bearer. Exactly one non-empty GUID `tenant_id`,
  exactly one non-empty GUID `sub`, and exactly one ordinal `actor_type = tenant_user` are required.
- The requested permission must exist as the exact ordinal token permission; legacy alias or case-fold match is not
  accepted by this endpoint. Other unrelated token permissions do not widen the requested pair.
- `X-Tenant-Id`, `X-Legal-Entity-Id`, `X-Legal-Entity-Ids`, tenant/user/Legal Entity query parameters and body fields
  are rejected. Credential headers must each occur exactly once.
- Request query is empty; Content-Type is JSON; body has exactly two unique properties.
- The Platform tenant context is established from the Bearer and restored deterministically on success, failure,
  exception, timeout and cancellation.
- The linked provider budget is two seconds. Caller cancellation propagates; only budget expiry maps to `504`.
- Candidate resolution is Scoped and may memoize only within the current dependency-injection request scope, keyed by
  exact tenant/subject. No static/singleton/distributed/cross-request cache or stale-success fallback exists.
- Candidate IDs are non-empty, sorted and distinct. A malformed, duplicate-after-contract-boundary, unsorted or
  over-bound candidate result fails closed; no partial response is returned.
- Multiple active Positions in different Legal Entities preserve the union. Multiple paths to the same Legal Entity
  collapse to one canonical output ID.
- The endpoint performs no MDM referenceability call. Candidate means Platform-derived candidate, not confirmed Legal
  Entity lifecycle truth.
- No JWT Legal Entity claim, signed scope token, browser header or first-value fallback is introduced.

## 9. Layout & Shell Contract

`shell: none`. There is no Razor view, frontend route, layout, DataTable or navigation entry. `_LayoutPlatformAdmin`,
`_LayoutTenantShell` and the frozen `_Layout.cshtml` are all outside scope. `golden_reference: none` is required for this
backend-only internal transport.

## 10. Backend File Convention

This is a focused authorization/query slice, not Golden Reference CRUD.

- Public interfaces, records and classes use one file each as listed in Section 5.
- CQRS uses `Queries/`, `Handlers/QueryHandlers/`, `Validators/` and one models file under the exact feature folder.
- `ResolveTrustedLegalEntityScopeQuery` is a sealed record; `ResolveTrustedLegalEntityScopeHandler` and validator names
  have no `Query` suffix.
- The controller is thin: strict parse/security executor then `IMediator.Send`; it owns no org traversal.
- API Security derives server facts; Application never reads raw headers or `HttpContext`.
- `OrgDataScopeCandidateResolver` owns Platform-only traversal. `OrgDataScopeResolver` owns its legacy MDM validation
  wrapper. No resolver duplicates Legal Entity entities or MDM lifecycle rules.

## 11. Frontend File Contract

No frontend files, browser API, localization resources or Save View behavior exist. Browser JavaScript cannot call the
internal endpoint. A future MDM adapter is separately owned by FU03 and uses server-side delegated-token forwarding plus
its dedicated service credential.

## 12. Validation Rules

| Input/fact | Required | Exact rule | Failure |
|---|---:|---|---|
| Credential identifier/secret/audience | yes | Each header exactly once; exact configured mapping | 401/403 before JWT |
| Rotation | conditional | Previous only while `UtcNow < PreviousValidUntilUtc`; revoke rejects both | 401 |
| Consumer service | yes | `DITENMDMSERVICE` | 403 |
| Allowed pair | yes | Exact configured module+permission pair; no wildcard/prefix/alias | 403 |
| Bearer | yes | Independent JWT Bearer authentication succeeds | 401 |
| `tenant_id` | yes | Exactly one non-empty GUID | 403 |
| `sub` | yes | Exactly one non-empty GUID | 403 |
| `actor_type` | yes | Exactly one ordinal `tenant_user` | 403 |
| Permission claim | yes | Exact ordinal requested key present; alias/case variant is insufficient | 403 |
| Query/forbidden headers | yes | Empty query; no tenant/user/LE scope input | 400/403 before dispatch |
| JSON body | yes | Exactly one `module_code` and one `permission_key`; no extras/duplicates/nulls | 400 |
| Candidate IDs | yes | Valid GUIDs, sorted, distinct and within approved maximum | 409/503 fail closed |
| Empty candidate set | allowed | Authoritative confirmed empty | 200 with empty list |
| Time budget | yes | Two seconds; caller cancellation distinct from timeout | 504 / propagate |

Canonical module and permission grammar follow the repository lowercase/kebab/dotted conventions. The exact maximum
count and request bounds are frozen in Section 4. The first-consumer pair set is frozen in Section 14; assumptions,
wildcards, prefixes and aliases are prohibited.

## 13. Failure Path to Verify

- Missing/wrong/revoked service credential is rejected before JWT resolution and before candidate repository reads.
- Previous secret succeeds strictly inside overlap and fails at equality/after overlap or revocation.
- Wrong service, audience, module or permission pair is 403 and cannot be repaired by body/JWT content.
- Missing/invalid Bearer is 401; duplicate/missing/malformed tenant/sub, non-tenant actor or missing exact permission is 403.
- Alias/case-variant permission without the exact key is denied.
- Tenant/user/Legal Entity query, body or scope header input is rejected before candidate resolution.
- Duplicate/additional JSON properties, empty/malformed values and oversized body are rejected before MediatR.
- No active Position Assignment returns `200` confirmed empty; it never opens all Legal Entities.
- Two Positions under different Legal Entities return both IDs; repeated paths to one Legal Entity return one ID.
- Over-bound, malformed, duplicate or unsorted candidate-contract result returns no partial set.
- Candidate resolver repository failure is unavailable, not successful empty; no cached stale success is returned.
- Caller cancellation propagates; internal budget expiry is 504; prior tenant scope is restored on both paths.
- Strict mocks/static dependency tests prove the endpoint path never calls MDM or an HTTP Legal Entity validator.
- Consecutive Tenant A/Tenant B requests and exception paths prove request memo and tenant context do not leak.

## 14. Authorization Convention

This internal endpoint does not use a tenant UI role or add a new permission. It validates the exact business permission
already requested by the consuming operation.

The first consumer uses exactly `module_code = product-item-sku-master` and the following bounded 28-key permission
set; configuration may enable a strict subset but cannot introduce another pair without a separately approved pack
amendment:

- `mdm.global-products.read`, `mdm.global-products.create`
- `mdm.gskus.read`, `mdm.gskus.create`
- `mdm.gskus.update`, `mdm.gskus.submit`, `mdm.gskus.withdraw`
- `mdm.gskus.request-correction`, `mdm.gskus.request-retirement`, `mdm.gskus.retire`
- `mdm.lskus.read`, `mdm.lskus.create`
- `mdm.finished-goods.read`, `mdm.finished-goods.create`
- `mdm.product-abbreviations.read`, `mdm.product-abbreviations.request`
- `mdm.product-abbreviations.approve`, `mdm.product-abbreviations.reject`
- `mdm.product-abbreviations.correct`, `mdm.product-abbreviations.cancel`
- `mdm.product-abbreviations.retire`, `mdm.product-abbreviations.audit`
- `mdm.product-legal-entity-scopes.read`, `mdm.product-legal-entity-scopes.configure`
- `mdm.product-legal-entity-scopes.replace`, `mdm.product-legal-entity-scopes.end`
- `mdm.product-legal-entity-scope-rollout.activate`, `mdm.product-legal-entity-scope-rollout.rollback`

Authorization is conjunctive:

1. Dedicated credential authenticates `DITENMDMSERVICE`.
2. Credential audience is exactly `TRUSTED_LEGAL_ENTITY_SCOPE_RESOLVE`.
3. `(module_code, permission_key)` is in the credential's explicit allowed subset.
4. Delegated Bearer independently authenticates exactly one `tenant_user`, tenant and subject.
5. Bearer contains the exact requested permission key.

Failure of any factor denies before candidate reads. The credential carries no tenant authority. The Bearer carries no
service authority. Management/rollout permissions from FU03 do not imply consumer row access, and FU21 creates no
`ProductLegalEntityScopeSteward`, role, grant or entitlement record.

Dedicated headers are:

- `X-Legal-Entity-Scope-Credential-Id`
- `X-Legal-Entity-Scope-Credential`
- `X-Legal-Entity-Scope-Audience`

Secrets, hashes, fragments, lengths and comparison material are absent from source, committed configuration, logs,
traces and responses. Tests generate ephemeral in-memory secrets.

## 15. Gateway / API Routing Decision

Gateway change is unnecessary and prohibited. The endpoint is internal service-to-service Platform API only. It is not
exposed through Ocelot and no frontend calls it. The future MDM adapter uses a server-side Platform base address under a
separate FU03 amendment; FU21 does not authorize that adapter or its configuration.

## 16. Acceptance Criteria

- [x] DCP-002 exact ID/name/parent proof remains green after file creation and no collision exists.
- [x] MDM-independent candidate extraction is a separate scoped resolver and contains no MDM client/validator call.
- [x] Existing FU15 resolver delegates candidate derivation and preserves current validated four-kind behavior.
- [x] Existing singular `ITenantAuthorizationContext` and `JwtTenantAuthorizationContext` remain unchanged.
- [x] Exact internal POST endpoint accepts only `module_code` and `permission_key`; no scope identity is client supplied.
- [x] Credential authentication is timing-safe, rotatable, revocable and bound to exact service/audience/pair subset.
- [x] Bearer validation independently proves exact tenant, subject, `tenant_user` and requested permission.
- [x] Candidate results preserve multiple Legal Entities, collapse legitimate duplicate paths, sort deterministically and
      reject malformed/duplicate-boundary/over-bound results without partial success.
- [x] Confirmed empty returns 200; unavailable/failure never becomes successful empty.
- [x] Two-second timeout, caller cancellation propagation and tenant-context restoration pass.
- [x] Request-scope memoization cannot leak across requests or tenants; no cross-request cache is registered.
- [x] Callback-loop absence is proven: the endpoint path performs zero MDM/Legal Entity HTTP validation calls.
- [x] No JWT scope claim, signed scope token, Platform context widening or browser scope header is added.
- [x] No MDM/Auth/Gateway/frontend/appsettings/secret/policy/grant/UI file changes occur.
- [x] Platform API build and focused tests pass with zero skipped focused security tests; Authorization and full
      Platform regressions introduce no failure beyond the exact failure set reproduced on clean `origin/main`.

## 17. Test Expectations

| Area | Minimum proof |
|---|---|
| Candidate resolver | no assignment = empty; effective intervals; archived/missing/cross-tenant exclusion; multi-position multi-LE union; duplicate path collapse; deterministic sort |
| FU15 regression | four existing scope kinds preserved; live MDM validation remains only in legacy wrapper; singular context still takes its historic first value |
| Credential | active/previous boundary, revoked, wrong identifier/secret/service/audience/pair; timing-safe path; no secret leakage |
| JWT | independent authentication; exact one tenant/sub/actor; exact permission; alias/case mismatch; unrelated claims; forbidden scope headers |
| Strict body | missing/null/extra/duplicate properties, query, body-size and grammar bounds rejected before MediatR |
| Executor | credential-before-JWT, two-second timeout, caller cancellation, tenant scope restoration on success/throw/timeout/cancel |
| Handler | confirmed empty 200; sorted distinct bounded IDs; malformed/duplicate/over-bound candidate failure; no partial response |
| Callback loop | strict dependency graph and runtime mocks prove zero MDM/HTTP Legal Entity validator calls |
| Tenant isolation | Tenant A/B real-Mongo org facts, consecutive requests and request memo isolation |
| DI | candidate resolver/request executor Scoped; authenticator/TimeProvider appropriate lifetime; no hosted service/startup call |

Required later commands:

- `dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests --filter FullyQualifiedName~TrustedLegalEntityScope`
- `dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests --filter FullyQualifiedName~OrgDataScope`
- `dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests --filter FullyQualifiedName~Authorization`
- `dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests`
- `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Release`

Real-Mongo evidence uses `mongodb://127.0.0.1:27017` with the fixed FU21 integration database, unique per-test tenant
IDs and tenant-scoped cleanup as required by DB-010; fake/in-memory/skip cannot close tenant-isolation claims.
Frontend, RESX and DataTable verification are N/A.

## 18. Ready-for-dev Checklist

- [x] Parent `MOD-0018`, FU identity and pre-mutation registry collision checked.
- [x] FU15, FU16, FU20, FU03 and current resolver/security code truth reviewed.
- [x] Backend-only, no-entity, no-UI and no-Gateway decisions recorded.
- [x] Generic Platform candidate ownership and MDM-local referenceability boundary recorded.
- [x] Delegated Bearer plus dedicated rotatable S2S model recorded; credential has no tenant authority.
- [x] JWT claim/signed-scope-token and single-value context changes rejected for foundation.
- [x] Exact no-glob future runtime/test allow-list and protected paths recorded.
- [x] Exact maximum 200, 1,024-byte body, 100/200-character string bounds and first-consumer 22-pair set approved on 2026-08-26.
- [x] Confirmed-empty 200, unavailable 503, inconsistent contract 409/provider-to-MDM 503 and timeout 504 mapping approved.
- [x] User approved the Phase 1.5 architecture on 2026-08-26; this approval does not grant runtime code-start.
- [x] Pack promoted to `ready-for-dev` after the two Platform-Security decisions were frozen.
- [x] User authorized runtime code-start for the exact Section 5 paths on 2026-08-26; operational mutation remains separate.
- [ ] After implementation evidence, Security/Operations owner provisions dedicated Development secrets and allowed
      pairs under a separate operational-run approval.
- [ ] Production/Staging secret provisioning, rotation/revocation runbook and live smoke remain separately prohibited.

## 19. Implementation Notes

### 2026-08-26 implementation evidence

> Historical evidence below is superseded where the 2026-08-30 current-main evidence differs. In particular, the
> original `GetAllAsync` traversal and unique-database Mongo fixture were replaced by R1 bounded server-side reads and
> a fixed shared test database before acceptance.

- Security order is credential, independently validated delegated Bearer, bounded strict parser, exact pair/permission,
  tenant scope and candidate resolution. The parser reads at most 1,025 bytes to enforce the 1,024-byte contract.
- Module and permission keys use exact lowercase kebab/dotted-kebab grammar. Claim type names and permission values use
  ordinal matching; aliases and case variants fail closed. Allowed-pair configuration rejects empty, duplicate,
  malformed, unknown or over-22 sets before serving an authenticated request.
- Timeout mapping requires the internal budget to be cancelled while the caller token is not cancelled. Caller and
  unrelated dependency cancellation propagate. Programming exceptions are not converted to 503; only the explicit
  candidate-unavailable contract is mapped to 503.
- A composition-root persistence classifier recognizes only `MongoConnectionException`,
  `MongoExecutionTimeoutException` and `TimeoutException`. The Application project has no Mongo dependency. Candidate
  repository failures in that exact set become the typed unavailable contract; programming, argument, null,
  serialization and cancellation exceptions propagate to the global pipeline.
- Candidate derivation uses `TimeProvider`, one assignment read, one position read and one Organization Unit read per
  uncached request; it performs no per-row calls and bounds each derived working set to 200 before the next stage.
  Memoization is scoped to the resolver instance and therefore cannot cross requests.
- Focused FU21 tests: 49/49 passed. FU15/authorization-context and DI regression: 27/27 passed. Combined focused run:
  76/76 passed, zero skipped. The broader Authorization regression was 307/307 passed, zero skipped.
- **Historical/superseded:** Full Platform suite: 1,591/1,591 passed, zero failed and zero skipped. The original two
  FU21 Mongo tests used `mongodb://127.0.0.1:27017` with a unique disposable database; R1 replaced that fixture with
  the DB-010 fixed-database/tenant-isolation model recorded below. A bounded unreachable Mongo client proved the real
  repository-to-handler 503 production chain without creating data.
- Platform API Release build passed with zero errors and seven pre-existing out-of-scope warnings.
- No secret, configuration, tenant, permission, operational data, Auth, MDM, Gateway or frontend mutation occurred.

- `OrgDataScopeResolver` now delegates Platform candidate derivation to the scoped candidate resolver and retains MDM
  live validation only in the legacy FU15 wrapper. The new endpoint stops before that MDM callback.
- `JwtTenantAuthorizationContext` currently stores only the first Legal Entity. FU21 intentionally leaves it untouched;
  consumers needing the full set use this internal transport.
- The verified GSKU/Market flow is a security-pattern reference, not a reusable credential. FU21 needs a dedicated
  audience and pair-bound credential because reference-data authority and access-governance scope are distinct.
- `PermissionClaimEvaluator` accepts aliases/case-insensitive keys for existing enforcement compatibility. FU21 must not
  reuse that permissive matching for its exact internal permission proof.
- Candidate IDs are not product access decisions. The future FU03 MDM consumer intersects locally revalidated IDs with
  an effective Product scope policy and the already-authorized resource operation.
- No response is persisted or cached across requests. Audit/telemetry may record bounded reason/outcome and correlation,
  but never secret material or the full candidate ID set.

### 2026-08-30 current-main extraction and regression evidence

- The accepted implementation was extracted from the dirty MDM development worktree onto the isolated
  `feature/pss/mod-0018-fu21-trusted-legal-entity-scope` branch at local `origin/main` base `dcb6509f`. The diff is
  exactly the 36 Section 5 runtime/test paths plus this pack; missing and extra paths are both zero. `Program.cs` and
  Application dependency injection were integrated surgically, preserving current-main WorkCenter, Tasks, Document,
  CRM, startup-validation and module-registration changes.
- Pre-acceptance security/quality audit found and closed two real issues. Allowed-pair configuration now accepts an
  explicit non-empty strict subset of the frozen 22-pair universe and denies every unconfigured canonical pair. The
  candidate traversal now uses a dedicated FU21 fact reader with tenant-scoped server-side filters, projections and
  `max + 1` limits instead of materializing three complete tenant collections.
- The current focused security/candidate/FU15/DI matrix passes **84/84**, zero skipped. Real Mongo proves tenant
  isolation, the 201-candidate fail-closed boundary, and `[EffectiveFrom, EffectiveTo)` evaluation by UTC instant for
  different offsets using the current BSON array representation. It also locks direct tenant-context/argument
  mismatch rejection and consecutive Tenant A-to-B memo isolation on the same scoped resolver. The fixture uses fixed
  `diten_platform_fu21_itest`, unique tenant IDs and tenant-scoped cleanup; it does not create a database per run.
- The full current-main Platform suite result is **3573/3596**, with exactly 23 failures. A clean detached
  `origin/main` control run is **3517/3540** with the exact same 23 failures; FU21 adds 56 tests and no new failure.
  The inherited failures are confined to existing MOD-0029 permission-reflection, Document Management lifecycle/
  manifest/training/index and two BRD legacy-cardinality tests.
- Platform API Release build passes with zero errors and 18 current-main warnings. Configuration, secrets, tenant,
  permission, operational data, process startup, Auth, MDM, Gateway, frontend and push remain untouched.
- Authorization regression is **352/355**; its exact three MOD-0029 reflection failures reproduce on clean
  `origin/main`. The repository-wide `TenantArchitecture.ArchitectureTests` gate passes **11/11** after replacing the
  last GUID-named unreachable-client database literal. `git diff --check` is clean and no protected-scope file is
  changed. Final independent review reports P0=0/P1=0; its two P2 evidence gaps are closed by the same-scope A-to-B,
  explicit tenant mismatch and architecture-guard regressions above.

### 2026-09-08 approved GSKU exact-pair amendment

The user authorizes only this pack and these exact runtime/test paths:

- `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedLegalEntityScopeCredentialAuthenticator.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/TrustedLegalEntityScopeCredentialAuthenticatorTests.cs`

The original 22 pairs are preserved. Six GSKU pairs yield an exact 28-pair union, not the protected source's
cross-product 30-pair set. Each uses `module_code = product-item-sku-master`:

| GSKU operation | Exact scope permission |
|---|---|
| Draft edit | `mdm.gskus.update` |
| First-pair submit | `mdm.gskus.submit` |
| Pending maker withdrawal | `mdm.gskus.withdraw` |
| Approved correction request | `mdm.gskus.request-correction` |
| Approved retirement request | `mdm.gskus.request-retirement` |
| Hidden system pair-retire compatibility path | `mdm.gskus.retire` |

These keys are the existing MOD-0290 GSKU lifecycle permission contract, not new permission identities.
Protected source correction/retirement request handlers use a separate mutation permission check but request
`mdm.global-products.read` candidates. The current user prohibition on read/create substitution supersedes that
transport choice: the separately committed MDM integration must send each operation's exact request permission.
No other product lifecycle pair is admitted. Configured subsets, independent delegated human JWT, tenant/subject
binding, credential client/audience/rotation/revocation and fail-closed matching remain unchanged.
This source amendment provisions no credential, grant, tenant or allowed-pair configuration. Historical 22-pair
test results above remain historical evidence, not results for the amended set.

Amendment verification: focused trusted-scope/endpoint/OrgDataScope run passed 77, failed 0, skipped 0.
The overlapping real-Mongo TrustedLegalEntityScopeResolutionMongoTests subset passed 6, failed 0, skipped 0.
Platform API Release build passed with 0 warnings and 0 errors. These tests preserve tenant/subject/client binding;
the separately required MDM Enforced-scope client-to-provider evidence belongs to the GSKU backend slice, not
to this owner-side amendment result. No live acceptance was run.

## 20. Follow-up Items

- FU03 MDM typed client/adapter, local Legal Entity revalidation and Product policy intersection.
- FU03 special permission/grant profile, management UI, migration, rollout activation and descendant enforcement.
- Organization Unit targeting/hierarchy and precedence versus Legal Entity.
- Service-account/background-job scope semantics; this foundation accepts only direct `tenant_user` actors.
- Optional bulk decision/projection only after measured performance evidence; no cross-request scope cache by default.
- Production credential provisioning, rotation/revocation drill, observability, retention and operational runbook.
