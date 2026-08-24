---
id: MOD-0018-FU16
name: Global Product Permission Onboarding
domain: platform-shared-services
service: Diten.AuthService
shell: none
golden_reference: none
entity_base: GlobalEntityBase
status: review
owner: auth-owner / platform-owner
branch: feature/pss/mod-0018-fu16-global-product-permission-onboarding
started: 2026-08-04
target: ""
form_field_count: 0
parent_module: MOD-0018
consumer_module: MOD-0290
---

# MOD-0018-FU16 — Global Product Permission Onboarding

> **Review/code-truth guard (2026-08-09).** The Section 5 implementation and Local Development pilot reconciliation are
> complete. The exact Global Product permissions are present in the shared module contract; Admin read/create, Viewer
> read-only and tenant-isolation behavior are evidenced. This remains non-Production evidence.
> This document amendment itself seeds no permission, creates no grant, changes no role, enables
> no entitlement, registers no module, changes no token and enables no API/UI user. Approval does not extend to any file,
> permission, module, role, entitlement or surface outside that allow-list.
>
> **Identity proof.** Master 8.1 `Blueprint_Data!A19:AG19` assigns roles, entitlements and policies to canonical parent
> `MOD-0018` (`RBAC / ABAC Authorization`). Registry inspection found no `MOD-0018-FU16` collision. Fail-closed DCP-002
> preflight passed on 2026-08-04:
> `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0018-FU16 --name "Global Product Permission Onboarding" --parent MOD-0018`
> → exit `0`, `OK MOD-0018-FU16: proven against Blueprint/registry`.
>
> **Golden Reference decision.** This is backend-only permission/catalog/onboarding work, not a CRUD, Razor or
> DataTable module. Therefore `shell: none`, `golden_reference: none` and `form_field_count: 0` are intentional.

## 1. Module Summary

This follow-up prepares the least-privilege onboarding required to make the already-prepared MOD-0290 Global Product
API/UI safely usable by entitled tenant users. It onboards exactly two permission keys:

- `mdm.global-products.read`
- `mdm.global-products.create`

The user-approved canonical ModuleCode is `product-item-sku-master`. The permission-key namespace remains `mdm`, while
permission ownership attribution, tenant module grants and Platform entitlement/catalog attribution use
`product-item-sku-master`.

The pack separates the MDM-owned Product / Item / SKU Master manifest, Platform fail-closed catalog reconciliation, Auth
permission-catalog persistence and entitlement-aware default-role grants, existing MDM endpoint enforcement evidence,
and Gateway/UI production enablement. It does not reopen MOD-0290 business implementation or FU13
permission-convention/cache scope.

## 2. Ownership and Boundaries

**In scope:**

- Auth-owned idempotent global catalog/seed onboarding that defines each exact key once, independent of tenant.
- MDM-owned second self-registration manifest for exactly `product-item-sku-master`, using the existing multi-provider
  hosted-service seam without changing the existing Legal Entity manifest or its behavior.
- User-approved per-service credential sender binding for MDM module registration. MDM presents only its credential
  identifier and credential secret; Platform validates them against the MDM server-side mapping, derives canonical
  producer code `DITENMDMSERVICE`, and passes that code server-side into reconciliation. No owner value comes from MDM.
- Platform-owned manifest identity reconciliation that considers live and soft-deleted catalog rows and compares the
  exact canonical fields in Section 4 before any mutation. Immutable `ProducerOwnerCode` persistence and a trusted sender
  mechanism are separate, jointly mandatory layers; the approved contract is exact in Sections 4, 8 and 14.
- Auth-owned tenant-role behavior: Tenant Admin receives `read + create`; Tenant Viewer receives `read` only, and only
  through a successfully fetched active module entitlement/onboarding path. The two keys are excluded from Viewer's
  general tenant-read baseline.
- Platform-owned `ModuleCatalogItem`, permission-descriptor/catalog synchronization and tenant entitlement/onboarding
  evidence where required by the existing runtime pattern.
- Existing-tenant reconciliation and new-tenant provisioning behavior without blanket or cross-tenant grants.
- Token/refresh behavior aligned with MOD-0018-FU13's bounded-staleness policy.
- Read-only evidence that `GlobalProductsController` enforces the two exact keys.
- A fail-closed production/user-enablement gate for the already-prepared Gateway/UI/API surfaces.

**Out of scope:**

- MDM controllers, CQRS, domain, persistence, Global Product API behavior, frontend or Gateway code changes.
- Any permission beyond the two exact keys; especially `manage`, `update`, `delete`, `bulk-delete`, wildcard or
  platform-admin onboarding permissions.
- RBAC/ABAC engine redesign, data-scope logic, Explain Access, cache topology or generic permission convention changes.
- General MOD-0290 product/SKU permission onboarding.
- Automatic onboarding of future Product Definition, GSKU, LSKU or Finished Good permissions. Sharing the canonical
  ModuleCode does not authorize any additional permission, role grant or tenant entitlement without separate owner scope.
- Unconditional tenant-role grants that bypass module entitlement.
- Treating the existing generic `platform_admin` handler behavior as onboarding proof or creating a new bypass.
- Treating manifest `Service`, any caller-supplied owner header/claim without cryptographic authentication, the shared
  internal key, source IP, `Origin = SelfRegistered` or operator-owned catalog metadata as producer-owner proof.
- AuthService token issuance, mTLS, changes to the generic shared internal-key mechanism, or credential onboarding for
  any service other than MDM.
- MDM Global Product controllers, application CQRS, domain, persistence, UI and Gateway changes; the sender-binding scope
  is confined to module-registration transport and Platform internal registration/reconciliation.

## 3. Owned Objects

| Object / contract | Owner | Delivery boundary |
|---|---|---|
| `mdm.global-products.read` | Auth global permission catalog | One active global record; globally unique by key and tuple; tenant-assignable scope |
| `mdm.global-products.create` | Auth global permission catalog | One active global record; globally unique by key and tuple; tenant-assignable scope |
| Tenant Admin module grants | Auth role/grant runtime | Tenant-scoped `RolePermission` rows for both exact keys with `SourceModuleCode = product-item-sku-master`, module-sourced and idempotent |
| Tenant Viewer module grants | Auth role/grant runtime | Tenant-scoped read `RolePermission` row with `SourceModuleCode = product-item-sku-master`, module-sourced and idempotent |
| Product / Item / SKU Master module catalog entry | Platform module catalog | Canonical `ModuleCode = product-item-sku-master`; active/assignable semantics explicitly recorded |
| Permission descriptors | Platform catalog/page descriptor owner | Under `product-item-sku-master`, declare exactly the two accepted Global Product keys and synchronize through the existing contract if selected |
| Tenant module entitlement | Platform entitlement owner | Tenant-scoped active/disabled entitlement for `product-item-sku-master`; no client-supplied tenant trust |
| Product / Item / SKU Master manifest | MDM owner | Second independent manifest with `ModuleCode = product-item-sku-master` and exactly the two Global Product descriptors; Legal Entity remains unchanged |
| Canonical manifest identity input | MDM owner | Exact incoming tuple: `ModuleCode`, `ModuleName`, `Domain`, `Service`; normalized and compared as specified in Section 4 |
| Trusted S2S sender identity | Platform internal API / Security owner | Authenticated independently of manifest content; shared internal key alone is insufficient; canonical result for MDM is `DITENMDMSERVICE` |
| MDM credential identifier | Platform credential map / MDM transport | Non-secret lookup key carried separately from the secret; may be logged for audit correlation but cannot establish owner without a valid mapped secret |
| MDM credential secrets | Platform + MDM secure configuration owners | Active secret plus optional previous secret during bounded rotation overlap; values never persist in catalog/domain data and never enter manifest JSON, logs or responses |
| MDM credential-owner mapping | Platform internal authentication owner | Server-side mapping from credential identifier to `DITENMDMSERVICE`; this pack's new immutable owner binding is authorized only for `PRODUCT-ITEM-SKU-MASTER` |
| `ModuleCatalogItem.ProducerOwnerCode` | Platform owner | New immutable string on the existing aggregate; created only from trusted sender identity, never from manifest body; exact stored value `DITENMDMSERVICE` |
| Manifest catalog reconciliation | Platform owner | Only trusted sender identity = persisted `ProducerOwnerCode` = `DITENMDMSERVICE`, plus the full Section 4 canonical tuple, may replay; active or soft-deleted mismatch fails before mutation |
| Entitlement fetch outcome | Auth owner | Distinguishes confirmed authoritative success, including empty, from unavailable/uncertain failure before grant/revoke decisions |
| MDM enforcement proof | MOD-0290 owner, evidence-only | Existing controller attributes; no controller/CQRS/domain/persistence mutation under this pack |

No new persisted entity type is introduced. The existing `ModuleCatalogItem` gains the explicit immutable string field
`ProducerOwnerCode`; its source is authenticated server-side sender context, not `ModuleManifestDocument.Service`.
Legacy rows with a missing/blank value are conflicts and are never inferred or backfilled from mutable catalog metadata.
Credential identifier and credential secret are authentication configuration, not `ModuleCatalogItem` fields and not
operator-owned catalog metadata. Neither is stored on the catalog row.
`Permission : GlobalEntityBase` is the global Auth catalog record and has
no `TenantId`; `Permission.Scope = Tenant` expresses tenant-role assignability, not record ownership. Tenant-specific
state remains in `RolePermission`, tenant role assignments and `TenantModuleEntitlement`. Existing
`Permission`, `RolePermission`, `RoleAssignmentVersion`, `ModuleCatalogItem` and `TenantModuleEntitlement` types are reused.

## 4. Entity Fields

| Existing object | Field / value | Rule |
|---|---|---|
| `Permission` | Base / tenancy | `GlobalEntityBase`; no `TenantId`; each catalog definition exists globally once |
| `Permission` | `Key` | Exactly one of the two keys; globally unique; lowercase dotted; no alias/wildcard |
| `Permission` | `Module` | `product-item-sku-master`, set through the existing `moduleOverride` pattern while the key namespace remains `mdm` |
| `Permission` | `Resource` | `global-products` |
| `Permission` | `Action` | `read` or `create` only |
| `Permission` | `Module + Resource + Action` | Globally unique tuple; no per-tenant duplicate definition |
| `Permission` | `Scope` | `Tenant` means tenant-role assignable authorization scope; it does not make the catalog record tenant-owned |
| `RolePermission` | `TenantId` | Server-owned tenant scope; cross-tenant rows prohibited |
| `RolePermission` | `GrantSource` | `Module`; entitlement disable/revoke removes only matching source-module grants |
| `RolePermission` | `SourceModuleCode` | Exactly `product-item-sku-master`; no alias |
| `ModuleCatalogItem` | `ModuleCode` | Exactly `product-item-sku-master`; runtime implementation must recheck the current Platform catalog store for collisions |
| `ModuleCatalogItem` | `ProducerOwnerCode` | New required immutable string; exact canonical value `DITENMDMSERVICE`; set once from trusted authenticated sender context and never accepted from manifest/request content or operator metadata |
| `ModuleCatalogItem` | `IsTenantAssignable` | `true` only after owner approval and tests |
| `ModuleCatalogItem` | `IsBaseline` | `false`; Global Product is entitlement-gated |
| `TenantModuleEntitlement` | `TenantId + ModuleCode` | Tenant-scoped entitlement with `ModuleCode = product-item-sku-master`; disabled/expired is not active entitlement |

Credential configuration fields are not persisted domain/entity fields:

| Configuration concept | Required contract |
|---|---|
| Credential identifier | Non-secret opaque identifier; transported as `X-Module-Registration-Credential-Id`; exact value is deployment-provisioned and never used as `ProducerOwnerCode` |
| Credential secret | Secret transported as `X-Module-Registration-Credential`; supplied only by secure configuration/environment/secret-store binding; never committed or logged |
| Active secret | Current accepted secret for the identifier; timing-safe comparison; rejected when missing, expired or mapping is revoked |
| Previous secret | Optional rotation-only secret; accepted only while current UTC time is strictly before configured `PreviousValidUntilUtc` and mapping is not revoked |
| `PreviousValidUntilUtc` | Required whenever a previous secret exists; explicit UTC instant; equality or later means the previous secret is rejected |
| Mapping result | Server-side `ProducerOwnerCode = DITENMDMSERVICE`; this value is never supplied by MDM headers/body |

### Canonical manifest owner-identity comparison

The collision guard must evaluate the complete table below for both active and soft-deleted catalog rows. "Same owner",
"semantic owner" and "same manifest identity" are shorthand only after every required comparison succeeds.

| Field / evidence | Real source | Required manifest value | Normalization and comparison | Mismatch result |
|---|---|---|---|---|
| `ModuleCode` | `ModuleManifestDocument.ModuleCode` and `ModuleCatalogItem.ModuleCode` | Raw manifest literal `product-item-sku-master` | Apply the existing `ModuleCatalogCodeNormalizer.Normalize` to both sides, then compare with `StringComparison.Ordinal`; the exact canonical key is `PRODUCT-ITEM-SKU-MASTER` | Explicit conflict; stop before every mutation or downstream sync |
| `ModuleName` | `ModuleManifestDocument.ModuleName` and `ModuleCatalogItem.ModuleName` | `ProductItemSkuMaster` | `Trim()` both values, then compare with `StringComparison.Ordinal`; no case-folding, alias or fallback | Explicit conflict; do not rewrite the stored name |
| `Domain` | `ModuleManifestDocument.Domain` and owner proof; `ModuleCatalogItem.Domain` is operator-owned after seed and is not proof | `MasterDataManagement` | Apply existing `ModuleTaxonomyCanonicalizer.NormalizeKey`; require exact key `MASTERDATAMANAGEMENT` with `StringComparison.Ordinal` | Explicit conflict; do not auto-register a domain or mutate catalog metadata |
| `Service` | `ModuleManifestDocument.Service` and owner proof; `ModuleCatalogItem.Service` is operator-owned after seed and is not proof | `DitenMdmService` | Apply existing `ModuleTaxonomyCanonicalizer.NormalizeKey`; require exact key `DITENMDMSERVICE` with `StringComparison.Ordinal` | Explicit conflict; do not resolve/reseed service metadata or mutate the row |
| `ProducerOwnerCode` | Trusted authenticated sender identity and new `ModuleCatalogItem.ProducerOwnerCode`; it is not a `ModuleManifestDocument` field | `DITENMDMSERVICE` | Normalize only the authenticated server-side producer identity with `ModuleTaxonomyCanonicalizer.NormalizeKey`, persist the result once, and compare to stored value with `StringComparison.Ordinal`; body `Service` is only a separate tuple assertion | Missing, blank or unequal sender/persisted value is an explicit conflict, including for legacy rows |
| `Origin` | `ModuleCatalogItem.Origin` | `SelfRegistered` is required state for a successful replay | Exact enum equality only, evaluated in addition to immutable owner proof | `Manual` fails; `SelfRegistered` alone never proves ownership and never permits adoption |

Only the first four rows are canonical manifest fields. `Origin` is catalog provenance classification, not canonical
ownership proof. `DisplayName`, `Description`, `SortOrder`, `Icon`, `Status` and `IsTenantAssignable` are operator-owned
metadata and must neither prove nor defeat ownership. `ModuleVersion` and `IsBaseline` are code-owned reconciliation
fields but are intentionally excluded from owner identity so legitimate same-owner version/baseline changes can reconcile
only after the owner guard passes. `Pages`, actions and `NotificationEvents` are payload content, not owner proof, and may
be inspected or mutated only after the guard passes.

**Two-layer owner-binding contract:** persistence and authentication solve different problems and neither substitutes for
the other. `ProducerOwnerCode` records the immutable owner of the catalog row; trusted S2S authentication proves who is
making the current call. Before reconcile, Platform must require authenticated sender code `DITENMDMSERVICE`, stored
`ProducerOwnerCode = DITENMDMSERVICE` for an existing row, and normalized body `Service = DITENMDMSERVICE`. On first create,
Platform persists `ProducerOwnerCode` from trusted sender context only. It never copies owner identity from the manifest,
a free-form header or operator metadata. The field is write-once: update, restore and replay cannot change it.

The current model and endpoint do not satisfy either layer: `ModuleCatalogItem` lacks `ProducerOwnerCode`, while
`InternalModuleRegistrationController` is `[AllowAnonymous]` and validates only the shared `X-Internal-Api-Key`. The key
does not distinguish MDM from any other holder. The user-approved replacement for this registration path is the exact
per-service credential contract in Section 14. AuthService token issuance, mTLS and generic internal-key redesign remain
outside this pack.

## 5. Repo Scope

This amendment changes only this pack. The user-approved per-service credential implementation may later start only in
the following exact allow-list. No directory wildcard, adjacent file or committed secret/configuration value is implied.

**MDM owner — module-registration infrastructure only:**

- `services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/ProductItemSkuMasterManifestProvider.cs`
  (new provider; exact Global Product slice only).
- `services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/ModuleRegistrationHostedService.cs`
  (enumerate and send registered providers independently and idempotently).
- `services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/PlatformRegistrationOptions.cs`
  (credential identifier/secret configuration binding only; no secret value committed).
- `services/Diten.MdmService/src/Diten.MdmService.Api/Program.cs`
  (provider registration and existing hosted-service wiring only).
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ModuleRegistration/ProductItemSkuMasterManifestProviderTests.cs`
  (new focused manifest tests).
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ModuleRegistration/LegalEntityManifestProviderTests.cs`
  (regression evidence only).
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ModuleRegistration/ModuleRegistrationHostedServiceTests.cs`
  (new multi-provider delivery tests).

**Auth owner:**

- `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/DefaultRolePermissionTemplate.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/ITenantEntitlementClient.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Services/PlatformTenantEntitlementClient.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/EntitlementPermissionSyncService.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Eventing/EntitlementSyncConsumer.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/DefaultRolePermissionTemplateTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/EntitlementPermissionSyncServiceTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/EntitlementSyncConsumerTests.cs`

**Platform owner:**

- `services/Diten.Platform/src/Diten.Platform.API/Configuration/ModuleRegistrationCredentialOptions.cs`
  (new exact options model for MDM identifier, active/previous secret, validity and revocation state).
- `services/Diten.Platform/src/Diten.Platform.API/Security/ModuleRegistrationCredentialAuthenticator.cs`
  (new exact server-side identifier lookup, timing-safe active/previous comparison and owner derivation component).
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/Internal/InternalModuleRegistrationController.cs`
  (authenticate sender, derive trusted producer identity server-side and reject before MediatR dispatch).
- `services/Diten.Platform/src/Diten.Platform.API/Program.cs`
  (bind/register only the module-registration credential options/authenticator).
- `services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleRegistration/RegisterModuleManifestCommand.cs`
  (carry trusted producer identity from the controller; never bind it from request JSON).
- `services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleRegistration/RegisterModuleManifestCommandHandler.cs`
- `services/Diten.Platform/src/Diten.Platform.Domain/Entities/ModuleCatalogItem.cs`
  (add immutable `ProducerOwnerCode` only).
- `services/Diten.Platform/src/Diten.Platform.Domain/Repositories/IModuleCatalogRepository.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/ModuleCatalogRepository.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/DependencyInjection.cs`
  (startup secret requirements for active/previous MDM registration secrets only; no secret values).
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/ModuleRegistration/ModuleRegistrationCredentialAuthenticatorTests.cs`
  (new identifier, timing-safe comparison, rotation, expiry, revocation and redaction tests).
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/ModuleRegistration/InternalModuleRegistrationControllerTests.cs`
  (new trusted-sender boundary tests).
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/ModuleRegistration/RegisterModuleManifestCommandHandlerTests.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/ModuleRegistration/ModuleManifestReconcilePruneTests.cs`

No committed appsettings file is authorized. Secret values enter both services only through secure configuration,
environment or secret-store binding at deployment/runtime. AuthService token issuance, certificate/mTLS files, generic
internal-key components and any additional configuration/authentication file remain outside this exact list.

## 6. Protected Paths

- `.antigravity/**`
- `AGENTS.md`
- `docs/System Capability & Implementation Blueprint - master 8.1.xlsx`
- `docs/product-backlog.md`
- `execution/portfolio/delivery-capability-packs/**`
- `execution/domains/master-data-management/module-packs/MOD-0290-product-item-sku-master.md`
- `services/Diten.MdmService/**`, except the seven exact MDM module-registration implementation/test files in Section 5
- In particular, all MDM controllers, application CQRS, domain, persistence, Global Product API behavior and unrelated
  module-registration providers remain protected; `LegalEntityManifestProvider.cs` is read-only and unchanged.
- AuthService and Platform files outside the exact Section 5 allow-list, including `DataSeeder.cs`, catalog seed files,
  token issuance/refresh code and entitlement mutation handlers
- All authentication/configuration surfaces not explicitly listed in Section 5. In particular, AuthService dependency
  injection and `Program.cs`, every committed appsettings file, secret-provider implementation/settings,
  certificate/Kestrel configuration, generic internal-key components and Auth token issuance remain protected
- Any new caller-controlled producer header, manifest field or query/body property presented as authenticated identity
- `frontend/**`
- `gateway/**`
- Other domain services and module packs
- Archive/frozen paths and `frontend/Diten.Web/Views/Shared/_Layout.cshtml`

## 7. Dependencies

- MOD-0018 parent authority: Master 8.1 `Blueprint_Data!A19:AG19` and `SoR_Map!A96:E96`.
- MOD-0018-FU9: existing shared default-role template and the deferred entitlement-aware MDM grant question; referenced,
  not expanded.
- MOD-0018-FU13: permission convention is closed; permission/token removal staleness is bounded by access-token TTL
  and refresh re-evaluation.
- MOD-0290: exact consumer keys and existing endpoint attributes.
- DCP-004 G8A: Auth catalog/seed + Platform module catalog/entitlement + MDM enforcement before exposure/readiness.
- Existing Auth unique indexes on `Permission.Key` and `(Module, Resource, Action)`.
- Existing Platform descriptor-to-Auth sync and entitlement-to-role-grant bridges.
- Existing MDM `IModuleManifestProvider` contract and Legal Entity provider; the hosted service must support both registered
  providers independently without changing either manifest contract.
- Current MDM transport evidence: `PlatformRegistrationOptions` contains only `BaseUrl` and a shared `InternalApiKey`, and
  `ModuleRegistrationHostedService` sends only `X-Internal-Api-Key` plus the manifest JSON.
- Current Platform evidence: `InternalModuleRegistrationController` is `[AllowAnonymous]`, compares the same shared key,
  and dispatches only `ModuleManifestDocument`; it has no authenticated producer principal or sender-bound claim.
- Platform has JWT bearer validation for the AuthService issuer, but AuthService `TokenService` emits tenant/platform-user
  tokens only. No client-credentials/service-token issuance, producer claim, per-service credential mapping, certificate
  authentication or mTLS configuration was found in the inspected runtime.
- Existing BuildingBlocks secret handling supplies secure configuration/environment resolution, production environment
  enforcement, startup requirement validation and redaction. The new MDM registration secrets reuse these existing
  primitives through the exact Section 5 binding/validation files; the shared generic key is not extended or repurposed.
- User-approved canonical ModuleCode: `product-item-sku-master`. Runtime implementation must recheck the then-current
  Platform catalog store for collisions before insertion/synchronization; a collision blocks implementation.

## 8. Runtime Constraints

- Default deny: missing/unknown permission, missing tenant context, inactive entitlement or failed onboarding never opens access.
- Only the two exact keys may be created or synchronized.
- MDM is the SoR for the `product-item-sku-master` manifest. The second provider declares only the Global Product slice;
  the existing Legal Entity provider remains behaviorally identical. Each registered provider is sent and retried
  independently so one manifest's result cannot suppress or duplicate the other.
- MDM uses only its per-service registration credential for both provider sends. `PRODUCT-ITEM-SKU-MASTER` is the only
  new immutable owner-binding authorization created by this pack. Legal Entity remains an existing registration contract:
  its provider/document/reconcile semantics do not change, and it receives no new `ProducerOwnerCode` rule here.
- `product-item-sku-master` registration requires `X-Module-Registration-Credential-Id` and
  `X-Module-Registration-Credential`. The first is a non-secret identifier; the second contains the secret and is treated
  as sensitive at every boundary. The shared `X-Internal-Api-Key` is not a fallback for this manifest.
- MDM reads identifier/secret only from `PlatformRegistration` options backed by secure configuration/environment/secret
  store. It sends no owner code/header and writes neither value into manifest JSON. Platform maps a validated identifier
  and secret server-side to `DITENMDMSERVICE`; request body `Service`/`Domain` remain untrusted assertions.
- Platform holds one active secret and optionally one previous secret for the MDM identifier. Every secret comparison uses
  `CryptographicOperations.FixedTimeEquals` (or the existing timing-safe equivalent) and never logs comparison material.
  Identifier lookup is non-secret; a matching identifier without a valid secret is unauthorized.
- Rotation overlap is bounded by explicit `PreviousValidUntilUtc`. The previous secret is accepted only when configured,
  not revoked, and `UtcNow < PreviousValidUntilUtc`; it is rejected at equality and afterward. Active secret plus previous
  secret may overlap only during that window. Missing, unknown, expired, revoked or non-overlap credentials fail closed.
- Logs and error responses may contain only credential identifier, a masked/non-reversible audit correlation and generic
  failure reason/status. Secret values, hashes, prefixes, suffixes and candidate-length details are forbidden.
- Local-development/test credentials are isolated from production provisioning. Tests generate ephemeral in-memory
  secrets at runtime and label identifiers test-only; no fixed test secret is committed in fixtures or examples.
- Trusted authenticated sender code, persisted immutable `ProducerOwnerCode` and body `Service` are three separate checks.
  For this manifest all normalize to `DITENMDMSERVICE`; equality of body fields without authenticated sender binding fails.
- Platform reconciliation must inspect matching live and soft-deleted catalog rows before mutation and apply every
  Section 4 comparison. The check must occur before domain auto-registration/resolution, catalog create/update or
  soft-delete restore, page/action prune or upsert, permission sync, and any operator-owned metadata mutation.
- A replay is idempotent only when trusted sender identity and immutable `ProducerOwnerCode` match, and normalized
  `ModuleCode`, trimmed ordinal `ModuleName`, normalized
  `Domain`, normalized `Service` and `Origin = SelfRegistered` all match. Any missing or mismatching value is an explicit
  failure; no adoption, resurrection, metadata rewrite, alias, fallback or partial reconciliation is allowed.
- Current code does not yet meet that contract: it has only a shared key, resolves/registers Domain before catalog lookup,
  fetches only the normal repository view, overwrites `ModuleName`, and flips `Origin` to `SelfRegistered`. Runtime
  implementation must add the approved per-service credential flow, immutable persistence and live/soft-deleted query
  path only through the exact Section 5 allow-list.
- `mdm` remains the immutable permission-key namespace. The existing `moduleOverride` pattern attributes both global
  catalog rows to `Permission.Module = product-item-sku-master` without changing either key.
- Platform `ModuleCatalogItem.ModuleCode`, tenant entitlement ModuleCode and module-sourced
  `RolePermission.SourceModuleCode` must all equal `product-item-sku-master`.
- Each key is a single global Auth catalog definition. Tenant onboarding must never seed another `Permission` row per tenant.
- `Permission.Scope = Tenant` means the global permission can be assigned to tenant roles; it is not a tenant ownership marker.
- Global Product is not a baseline module. Catalog presence alone never grants access.
- `mdm.global-products.read` is explicitly excluded from the generic Viewer tenant-read baseline; both keys enter Admin
  or Viewer roles only through the entitlement-aware module-grant path.
- Tenant Admin gets read + create; Tenant Viewer gets read only; other/custom roles get no automatic grant.
- Role grants are tenant-scoped, module-sourced, retry-safe and idempotent.
- Entitlement disable/removal revokes only matching module-sourced grants; manual/system grants are not silently rewritten.
- Auth must distinguish a successfully fetched authoritative entitlement result from unavailable/uncertain Platform state.
  Confirmed empty, disabled or expired state may revoke matching module grants. Timeout, 5xx, malformed response,
  configuration error or other unavailable/uncertain state performs neither new grant nor revoke.
- Existing access tokens are immutable. New grants appear only after a refresh/new token. Removed grants are absent on
  refresh; any already-issued token risk stays bounded by the current MOD-0018-FU13 policy (maximum 15-minute access-token TTL).
- No permission or entitlement check trusts request-body/header TenantId without authenticated server binding.
- No `platform_admin` permission, wildcard, alias or broad action is added. Existing generic bypass behavior is not
  accepted as evidence that Admin/Viewer/entitlement gates work.
- A best-effort catalog sync failure blocks production enablement until reconciliation proves both Auth keys exist.

## 9. Layout & Shell Contract

`shell: none`; this pack owns no view or route.

- No Razor file is created or changed.
- No layout is selected by this backend-only artifact.
- The existing MOD-0290 tenant UI remains owned by its approved delivery and uses `_LayoutTenantShell`; that UI is
  evidence/consumer scope only here.

## 10. Backend File Convention

This is not a Golden Reference CRUD feature. Later implementation extends existing seed/catalog/grant/entitlement
seams with the smallest owner-specific changes.

- No new feature folder is required unless an owner-approved implementation plan proves one necessary.
- Each new public type, if any, has one responsibility and one file.
- Existing repository/handler/service naming and DI boundaries are preserved.
- Auth owns global permission-catalog persistence and tenant-scoped role-grant persistence; Platform owns module catalog
  and tenant-scoped entitlement persistence.
- MDM remains the permission declarer/enforcer and manifest SoR. Only the exact Section 5 module-registration files may
  change; controllers, CQRS, domain, persistence and Global Product API behavior remain untouched.

## 11. Frontend File Contract

No frontend file is in scope. The prepared Global Product UI cannot be called production/user-enabled until this pack's
runtime evidence and the separate Gateway evidence pass. UI hiding is never authorization enforcement.

## 12. Validation Rules

| Input / decision | Required | Rule | Failure |
|---|---:|---|---|
| Permission key | Yes | Exact allow-list of two keys | Reject; no partial seed/sync |
| Key format | Yes | lowercase dotted, three segments, kebab-case resource | Reject malformed key |
| Catalog tenancy | Yes | `Permission` is global, has no `TenantId`, and each key is seeded/synchronized once globally | Reject any per-tenant catalog duplication |
| Key uniqueness | Yes | globally unique by `Key` and by `(Module, Resource, Action)` | Idempotent same-definition replay; conflict on divergent definition |
| Permission scope | Yes | `Tenant` means tenant-role assignable, not tenant-owned | Reject PlatformAdmin scope or tenant-owned catalog interpretation |
| ModuleCode | Yes | Exactly `product-item-sku-master` across Permission attribution, Platform catalog, entitlement and module grants | Reject `global-product`, `mdm`, `MOD-0290` or any other alias/candidate |
| ModuleCode collision | Yes at runtime start | Recheck the current Platform catalog store before insertion/sync | Block runtime implementation on conflicting ownership |
| Credential identifier | Yes | Non-secret `X-Module-Registration-Credential-Id`; exact server-side lookup; never interpreted as owner code by itself | Missing/unknown/revoked mapping returns generic unauthorized before command dispatch |
| Credential secret | Yes | Sensitive `X-Module-Registration-Credential`; timing-safe compare against eligible active and previous candidates; never committed/logged/returned | Missing/wrong/expired/revoked/non-overlap returns generic unauthorized before command dispatch |
| Rotation overlap | Conditional | Previous secret accepted only when present and `UtcNow < PreviousValidUntilUtc`; active remains accepted while valid; equality/outside rejects previous | Fail closed with no reconcile or mutation |
| Sender/owner binding | Yes | Trusted producer code must normalize to `DITENMDMSERVICE` and exactly equal persisted `ProducerOwnerCode`; body `Service` must separately normalize to the same value | Any mismatch returns explicit conflict/forbidden result before reconciliation |
| Secret redaction | Yes | Logs/responses contain no secret, hash, prefix/suffix or candidate-length data; identifier/masked correlation only | Test failure and production enablement block |
| Environment separation | Yes | Production secret comes only from secure environment/configuration/secret store; tests generate ephemeral in-memory test-only secrets | Reject committed/fallback/default secret values |
| Canonical manifest tuple | Yes | Compare all four Section 4 fields with their exact normalizers: `ModuleCode`, `ModuleName`, `Domain`, `Service` | Any missing/mismatching field returns explicit conflict before mutation |
| `ProducerOwnerCode` persistence | Yes | New immutable string, created only from trusted sender context; exact value `DITENMDMSERVICE`; legacy missing/blank is not auto-backfilled | Missing/mismatch fails closed; update/restore cannot change it |
| Catalog origin | Yes | Exact `ModuleCatalogOrigin.SelfRegistered`, checked only alongside immutable owner proof | `Manual` fails; never auto-flip as adoption, and `SelfRegistered` alone is insufficient |
| Role name | Yes | `Admin` or `Viewer` only for automatic grants | Unknown role receives none |
| Admin grant set | Yes | read + create | Missing/extra key fails |
| Viewer grant set | Yes | read only | Create or extra key fails |
| Entitlement state | Yes | active/effective only | Disabled/expired/missing grants none and fails closed |
| Entitlement fetch result | Yes | Confirmed authoritative success is distinct from unavailable/uncertain failure | Failure causes no grant and no revoke |
| TenantId | Yes | server-bound, non-empty and consistent | Reject/no-op without cross-tenant disclosure |

## 13. Failure Path to Verify

- Anonymous Global Product request → 401.
- Authenticated user without the required exact key → 403; no handler/repository call.
- Tenant Viewer calls list/detail/selector → allowed with read; calls reservation/create → 403.
- Tenant Admin with active entitlement and refreshed token → read/create allowed.
- Tenant without active Global Product entitlement → no automatic grant; refreshed/new token lacks both keys; endpoint 403.
- Entitlement disabled after grant → matching module-sourced grants removed; refresh cannot reintroduce them; already-issued
  access-token behavior remains bounded and documented per FU13, never “valid until logout.”
- Global catalog sync/seed replay → no duplicate `Permission` row; processing another tenant never seeds the same key again.
- Tenant grant/revoke replay → no duplicate `RolePermission` row and no mutation in another tenant.
- MDM startup with both providers → Legal Entity and Product / Item / SKU Master manifests are sent independently;
  retries/restarts remain idempotent and one provider's failure does not suppress the other.
- Missing/unknown/revoked credential identifier, missing/wrong/expired secret, or shared-key-only request → generic
  unauthorized before MediatR dispatch; no catalog read/write, restore, page/action operation, permission sync or metadata mutation.
- Previous credential inside configured overlap (`UtcNow < PreviousValidUntilUtc`) → accepted and mapped to
  `DITENMDMSERVICE`; at equality or outside overlap → rejected before dispatch. Active credential remains independently valid.
- Trusted authenticated producer is `DITENMDMSERVICE` and new/existing immutable `ProducerOwnerCode` matches → continue to
  the full canonical tuple guard; an exact replay is idempotent.
- Trusted producer is `DITENMDMSERVICE` but body `Service` or persisted `ProducerOwnerCode` differs → explicit fail-closed
  response before create/update, soft-delete restore, page/action prune/upsert, permission sync or metadata mutation.
- Active or soft-deleted Platform row with missing/mismatching immutable owner proof or any mismatch in normalized
  `ModuleCode`, trimmed ordinal `ModuleName`, normalized `Domain`, normalized `Service` or exact `Origin` → explicit
  failure before domain registration/resolution, create/update, restore, page/action prune or upsert, permission sync or
  metadata mutation; no silent adoption, resurrection, alias or fallback.
- Existing row with matching immutable owner proof and the complete Section 4 tuple → idempotent replay only;
  operator-owned metadata remains preserved. Matching `Origin = SelfRegistered` without owner proof still fails.
- Existing legacy row with no immutable owner proof → explicit conflict even if all current catalog fields appear equal;
  no backfill may be inferred from mutable metadata.
- Any authentication failure log/response inspection → credential secret and all derived secret material absent;
  credential identifier or masked audit correlation may appear, with a generic failure result only.
- Legal Entity registration with the MDM credential → existing manifest content and reconcile behavior remain unchanged;
  repeated delivery remains idempotent and no new Legal Entity owner-persistence rule is introduced.
- Same key with divergent module/scope/action definition → fail closed and log conflict; do not overwrite ownership silently.
- Existing Platform catalog row conflicts with `product-item-sku-master` ownership → block runtime implementation; do not
  choose `global-product`, `mdm`, `MOD-0290` or another fallback ModuleCode.
- Cross-tenant role/grant/entitlement input → no cross-tenant mutation or existence disclosure.
- Viewer general baseline selection sees the two Global Product keys → selects neither; only active entitlement may add read.
- Confirmed empty/disabled/expired entitlement → matching module grants may be revoked without affecting another tenant.
- Platform entitlement fetch timeout, 5xx, malformed response, configuration error or other uncertainty → no new grant,
  no revoke and no false-success completion; onboarding remains incomplete and UI/API is not production-enabled.
- `platform_admin`, wildcard, `manage`, `delete` or `bulk-delete` proposal → rejected as outside allow-list.

## 14. Authorization Convention

- Internal registration endpoint authorization is separate from tenant/user RBAC. The user-approved sender mechanism for
  MDM is per-service credential authentication; AuthService tokens, mTLS and generic internal-key redesign are not involved.
- Transport contract:
  - `X-Module-Registration-Credential-Id`: non-secret opaque identifier.
  - `X-Module-Registration-Credential`: secret value; sensitive header, never logged or returned.
  - MDM sends neither `ProducerOwnerCode` nor any owner header/body field.
- Platform performs server-side identifier lookup, rejects disabled/revoked/unknown mappings, and timing-safely compares
  the supplied secret with the active secret and with the previous secret only during its configured overlap. A successful
  mapping yields `DITENMDMSERVICE`; identifier text itself is never treated as the owner.
- `RegisterModuleManifestCommand` carries the trusted producer code as server-created context alongside the manifest. Model
  binding must not populate or override it. Body `Service` remains a separate assertion and must normalize to the same code.
- For normalized `PRODUCT-ITEM-SKU-MASTER`, the server-side authorization map requires producer
  `DITENMDMSERVICE`. The generic shared key is not accepted as fallback. Unknown module-to-owner mapping fails closed.
- MDM uses the same service credential transport for its pre-existing Legal Entity provider, but this pack changes neither
  the Legal Entity document nor its existing reconcile semantics and introduces no Legal Entity `ProducerOwnerCode` migration.
- Secret source boundary: values exist only in secure configuration/environment/secret store. No source literal, Module
  Pack example, committed appsettings value, fixed test fixture, log or error payload may contain a credential secret.
- Rotation contract: credential configuration exposes active secret, optional previous secret and
  `PreviousValidUntilUtc`. Previous is accepted only while `UtcNow < PreviousValidUntilUtc`; equality/outside rejects it.
  Revocation disables both active and previous immediately. Rotation owner/runbook and production provisioning remain
  operational follow-ups, not permission to weaken this runtime contract.
- Exact permissions:
  - read endpoints (`GET` list, detail, selector): `mdm.global-products.read`
  - create flow (`POST` reservation and draft): `mdm.global-products.create`
- Attribution contract:
  - permission-key namespace: `mdm`
  - `Permission.Module`: `product-item-sku-master` through the existing `moduleOverride` pattern
  - Platform `ModuleCatalogItem.ModuleCode` and `RolePermission.SourceModuleCode`: `product-item-sku-master`
- Actor baselines:
  - Tenant Admin: read + create after active entitlement and token refresh/new issuance.
  - Tenant Viewer: read only after active entitlement and token refresh/new issuance.
  - Other roles: no automatic grant.
- MDM enforcement evidence is the existing `[Authorize]` plus exact `[HasPermission]` attributes on
  `GlobalProductsController`; the only authorized MDM changes are the Section 5 module-registration files.
- No broad `manage`, update, delete, bulk-delete, wildcard, alias or platform-admin onboarding key exists.
- Existing `platform_admin` bypass code is neither created nor expanded by this pack and cannot satisfy tenant-role or
  entitlement acceptance tests.

## 15. Gateway / API Routing Decision

Gateway and API route changes are unnecessary and prohibited in this follow-up.

- The existing Global Product API and prepared Gateway/UI are consumers only.
- No Ocelot route, method, header rule or frontend proxy is changed.
- Production user enablement requires separate evidence that Gateway/UI delivery is complete and that this pack's Auth
  and Platform onboarding is effective. One cannot substitute for the other.

## 16. Acceptance Criteria

- [ ] DCP-002 preflight and collision evidence remain valid for `MOD-0018-FU16`.
- [x] Canonical ModuleCode is locked by user decision to `product-item-sku-master`; `global-product`, `mdm`, `MOD-0290`
      and every other candidate are closed as canonical ModuleCode alternatives.
- [x] The user-approved trusted-sender mechanism is the Section 14 per-service credential contract, restricted to
      `DITENMDMSERVICE` and `product-item-sku-master`; issuer-bound tokens, mTLS, generic internal-key redesign and
      other-service onboarding are closed as alternatives in this delivery.
- [ ] Missing, unknown, disabled or revoked credential identifier; missing/wrong credential secret; expired previous
      credential; and shared `X-Internal-Api-Key` without a valid MDM per-service credential are rejected before command
      dispatch. Manifest `Service`, `Domain` or any caller-supplied owner value cannot establish identity.
- [ ] A valid active MDM credential maps server-side to `DITENMDMSERVICE`; MDM sends no owner code, and successful
      registration with the complete matching canonical identity is idempotent.
- [ ] The previous MDM credential is accepted only while `UtcNow < PreviousValidUntilUtc`; it is rejected at equality,
      after overlap, or when revoked. Active and previous comparisons are timing-safe.
- [ ] Credential secrets, hashes, fragments, lengths and comparison material are absent from source, committed
      configuration, Module Pack examples, fixed test fixtures, logs and error responses; tests use ephemeral test-only values.
- [ ] Platform derives trusted sender code server-side as `DITENMDMSERVICE`; new catalog creation persists immutable
      `ProducerOwnerCode = DITENMDMSERVICE` from that context only, and replay/restore never changes it.
- [ ] Runtime implementation rechecks active and soft-deleted Platform rows and proves immutable owner identity plus the
      complete Section 4 canonical tuple before catalog insertion/synchronization.
- [ ] MDM registers a second owner manifest for exactly `product-item-sku-master`; the hosted service sends it and the
      existing Legal Entity manifest independently and idempotently, and Legal Entity behavior is unchanged.
- [ ] Platform checks live and soft-deleted catalog rows before reconciliation. Missing/mismatching immutable owner proof,
      normalized `ModuleCode`, trimmed ordinal `ModuleName`, normalized `Domain`, normalized `Service` or exact `Origin`
      returns explicit failure before domain resolution/registration, create/update, restoration, metadata mutation,
      page/action prune/upsert or permission sync.
- [ ] Trusted sender, immutable `ProducerOwnerCode` and body `Service` must all normalize to `DITENMDMSERVICE`; mismatch in
      any layer fails before catalog create/update, soft-delete restore, page/action prune/upsert or permission sync.
- [ ] Replay with matching trusted sender, immutable owner proof and the complete Section 4 tuple is idempotent and preserves
      operator-owned soft metadata; no alias, fallback ModuleCode, `SelfRegistered` shortcut or silent overwrite exists.
- [ ] Auth global catalog contains exactly one active record for each exact key; repeat seed/sync changes nothing, including
      when multiple tenants are onboarded.
- [ ] `Permission.Key` and `(Module, Resource, Action)` are globally unique; the same catalog key is never seeded once per tenant.
- [ ] Both global permissions have `Scope = Tenant` only to express tenant-role assignability; no tenant-owned catalog,
      platform-admin scope or wildcard definition is created.
- [ ] Platform catalog has one active, tenant-assignable, non-baseline Global Product module entry and descriptors declare
      exactly the two permission keys under `ModuleCode = product-item-sku-master` through one owner-approved source-of-truth path.
- [ ] Both global Auth permissions retain their `mdm.global-products.*` keys while `Permission.Module` equals
      `product-item-sku-master` through the existing `moduleOverride` pattern.
- [ ] Active entitlement grants Tenant Admin read + create and Tenant Viewer read only as tenant-scoped, module-sourced
      `RolePermission` rows with `SourceModuleCode = product-item-sku-master`, without creating additional catalog records.
- [ ] The two Global Product permissions are excluded from Viewer's general tenant-read baseline and are never granted
      to Admin or Viewer before a successfully fetched active entitlement confirms this module.
- [ ] Missing/disabled/expired entitlement produces no `RolePermission` grant and refreshed/new tokens cannot call the API.
- [ ] Confirmed empty/disabled/expired authoritative entitlement may revoke matching module-sourced grants; unavailable,
      timed-out, 5xx, malformed or misconfigured Platform state performs neither revoke nor new grant.
- [ ] Grant and revoke behavior is isolated per tenant; entitlement changes for one tenant do not mutate another tenant's grants.
- [ ] Existing tenants with active entitlement reconcile idempotently; tenants without entitlement are unchanged.
- [ ] New-tenant provisioning and later entitlement activation converge on the same Admin/Viewer result.
- [ ] Entitlement removal deletes only the matching module-sourced grants; unrelated/system/manual grants remain intact.
- [ ] Permission change token/refresh behavior matches MOD-0018-FU13: grants require refresh/new token; removals are absent
      after refresh and old access-token exposure is bounded by the configured maximum 15-minute TTL.
- [ ] Anonymous requests return 401; missing-key requests return 403; cross-tenant access/mutation fails closed.
- [ ] Read-only controller evidence proves all GET actions use read and both POST actions use create, with no broader key.
- [ ] No Product Definition, GSKU, LSKU or Finished Good permission, grant or entitlement is inferred from the shared
      ModuleCode; each requires a separately authorized owner scope before onboarding.
- [ ] No MDM file outside the exact registration allow-list, and no frontend, Gateway, `.antigravity`, Blueprint, DCP,
      registry or backlog file changes occur in runtime delivery.
- [ ] No production/user-enablement claim is made until Auth, Platform, MDM enforcement and separate Gateway/UI evidence pass.

## 17. Test Expectations

**Auth tests:**

- Global catalog seed/sync idempotency for both keys; global key and tuple uniqueness; divergent-definition conflict.
- Constructor/seed evidence proves keys remain `mdm.global-products.read|create` while `moduleOverride` persists
  `Permission.Module = product-item-sku-master`.
- Multi-tenant onboarding proves the same catalog keys remain single global records and are not seeded per tenant.
- Permission parser/scope classification proves `Scope = Tenant` means tenant-role assignability, not catalog tenancy.
- Admin selection/grant equals `{read, create}`; Viewer equals `{read}`; unknown/custom role gets none automatically.
- Viewer baseline selection explicitly excludes both Global Product keys before entitlement reconciliation.
- Active entitlement creates the expected tenant-scoped module grants; duplicate event/reconciliation creates no duplicate
  `RolePermission` row.
- Existing-tenant backfill/reconcile and new-tenant entitlement activation converge on the same grant set.
- Confirmed empty/disabled/expired entitlement creates no grant and removes matching module grants only, leaving
  unrelated/system/manual grants intact.
- Platform unavailable, timeout, 5xx, malformed response or configuration error is distinguishable from confirmed empty;
  it produces no new grant, no revoke and no false-success log/result.
- Grant/revoke isolation across two tenants and cancellation propagation.
- Token issuance after active entitlement includes the correct role-specific claims; refresh after removal excludes them;
  existing token behavior is asserted against the current ≤15-minute FU13 policy.

**Platform tests:**

- Credential boundary: missing, unknown, disabled or revoked identifier; missing/wrong secret; and shared-key-only request
  are rejected before `IMediator.Send` and before every catalog/page/action/permission mutation.
- Timing-safe comparison is exercised for eligible active and previous secrets without exposing candidate content, length
  or comparison result detail in logs or responses.
- Active MDM credential maps only server-side to `DITENMDMSERVICE`; matching immutable `ProducerOwnerCode` plus the
  complete canonical identity reaches reconciliation, and repeated exact manifest replay is idempotent.
- Previous credential succeeds strictly inside the configured overlap and fails at `PreviousValidUntilUtc`, afterward,
  or when revoked; the active credential remains independently valid while configured and not revoked.
- Correct MDM credential with mismatching body `Service` or persisted `ProducerOwnerCode` is rejected before any
  domain/catalog/page/action repository call or permission sync.
- Parameterized active-row and soft-deleted-row tests independently mismatch each required identity component:
  trusted sender, `ProducerOwnerCode`, `ModuleCode`, `ModuleName`, `Domain`, `Service` and `Origin`; each returns explicit failure and
  proves zero domain registration/resolution, catalog create/update/restore, page/action prune/upsert, permission sync and
  operator-metadata mutation.
- Missing immutable owner proof on a legacy row fails even when all current catalog fields match; `Origin = SelfRegistered`
  alone also fails.
- Exact raw/canonical normalization tests prove `product-item-sku-master` → `PRODUCT-ITEM-SKU-MASTER`,
  `MasterDataManagement` → `MASTERDATAMANAGEMENT`, `DitenMdmService` → `DITENMDMSERVICE`, and `ModuleName` uses trimmed
  ordinal comparison with no case-folding.
- Matching immutable owner proof plus the complete canonical tuple replays idempotently for active and soft-deleted
  lookup paths and preserves operator-owned metadata.
- ModuleCatalogItem uniqueness for exact `ModuleCode = product-item-sku-master`, plus active/assignable/non-baseline flags.
- Permission descriptor union returns exactly both keys; permission sync is idempotent and reports failure without false success.
- Entitlement/grant projection uses `product-item-sku-master` consistently as ModuleCode/SourceModuleCode and does not
  pull undeclared future MOD-0290 permissions into the two-key Global Product descriptor set.
- Active versus disabled/expired/missing tenant-entitlement projection is tenant-isolated and fail-closed.
- Entitlement add/enable/disable/reconcile event flow produces the expected Auth grant/revoke request without cross-tenant data.
- Authentication failure logs and HTTP responses contain no credential secret, hash, prefix/suffix, length or derived
  comparison material; only the identifier or a masked/non-reversible audit correlation may be asserted.

**Consumer/evidence tests:**

- MDM sends Legal Entity and Product / Item / SKU Master manifests independently; repeated startup/send is idempotent,
  one provider failure does not suppress the other, and the existing Legal Entity document is unchanged.
- Legal Entity regression proves its manifest content, independent delivery/retry behavior and successful registration
  remain unchanged under the selected trusted-sender mechanism; no Legal Entity provider code change is permitted.
- MDM hosted-service tests prove the identifier and secret come from secure options binding, are applied only as the two
  Section 14 transport headers, never enter manifest JSON/logs/errors, and cannot be replaced by a caller-supplied owner.
- Hosted-service credential fixtures generate ephemeral in-memory test-only secrets; no production-shaped fallback,
  default or fixed credential secret is committed.
- Static/controller contract proof: GET actions require read; POST actions require create; no manage/delete/bulk/wildcard key.
- HTTP authorization matrix: 401 anonymous; 403 missing permission; Viewer read 200/create 403; Admin read/create 2xx;
  wrong tenant non-leaking failure; entitlement-disabled refreshed token 403.
- Build and full suites run only during later runtime implementation. This document-preparation task runs no build/test.

## 18. Ready-for-dev Checklist

- [x] User approved this pack's scope, two-key allow-list, tenant grant boundary and canonical ModuleCode on 2026-08-05; status is `approved`.
- [x] Master 8.1 parent evidence reviewed.
- [x] Registry collision check and DCP-002 preflight passed.
- [x] Existing MOD-0018 and FU scopes reviewed; FU9/FU13 boundaries are not widened.
- [x] Exact permission allow-list locked to read/create.
- [x] User-approved canonical ModuleCode is locked to `product-item-sku-master` with the `mdm` key namespace preserved.
- [x] On 2026-08-05 the user approved per-service credential sender binding for owner `DITENMDMSERVICE` and authorized
      only the mechanism-specific exact Section 5 runtime/test paths; this contract amendment itself starts no code work.
- [x] Platform delivery boundary is locked to the MDM owner manifest and fail-closed live/soft-deleted identity check;
      runtime collision evidence remains an acceptance result, not a code-start prerequisite.
- [x] Exact canonical manifest tuple and normalization rules are locked in Section 4; operator-owned metadata and
      `Origin = SelfRegistered` are explicitly rejected as standalone owner proof.
- [x] Immutable persistence contract is locked to write-once string `ProducerOwnerCode`, exact MDM value
      `DITENMDMSERVICE`, trusted-context-only creation and fail-closed missing/legacy behavior.
- [x] The sender mechanism, headers, server-side credential-to-owner derivation, timing-safe comparison, active/previous
      overlap semantics, revocation behavior and secret boundary are closed by Sections 4, 8 and 14.
- [x] Runtime implementation authority is bounded to the exact Section 5 allow-list. A separate implementation start may
      use only those paths; no wildcard, committed appsettings file, AuthService token, mTLS or generic-key change is implied.
- [ ] Runtime evidence covers active credential success; missing/unknown/wrong/revoked credentials; previous credential
      inside/outside overlap; body/persisted owner mismatch; active/soft-deleted collision; secret redaction; and unchanged
      Legal Entity behavior.
- [x] Auth delivery boundary is locked to global catalog sync and entitlement-aware grant/reconciliation with explicit
      authoritative-versus-uncertain fetch semantics; runtime evidence remains pending.
- [x] The authorized security test matrix covers 401/403, tenant isolation, Viewer baseline exclusion and token staleness.
- [x] Non-baseline entitlement semantics and confirmed-state-only grant/revoke behavior are locked.
- [x] Existing `platform_admin` bypass is explicitly excluded from onboarding proof; no new bypass is approved.
- [ ] Separate Gateway/UI completion evidence is linked before production/user enablement.
- [x] Layout, DataTable, RESX and frontend contracts are N/A for this backend-only pack.

## 19. Implementation Notes

- `DefaultRolePermissionTemplate` currently gives Viewer every tenant-scoped `read` permission except a small self-service
  exclusion set. The two Global Product keys must be explicitly excluded from that general baseline; neither Admin nor
  Viewer receives them outside the declared-key + active-entitlement module-grant bridge.
- Platform permission descriptors are important because `GetTenantEntitledModulePermissionsQueryHandler` returns each
  active module's declared permission keys and Auth grants Admin all declared keys while filtering Viewer to `read`.
- `Permission` catalog rows are global and carry no `TenantId`. The existing `moduleOverride` pattern allows their
  owner-attribution `Module` to differ from the `mdm` key namespace. For these two keys the locked construction contract
  is namespace module `mdm` plus `moduleOverride: "product-item-sku-master"`; this records the decision without selecting
  a concrete seed path or changing runtime code.
- `product-item-sku-master` represents the full MOD-0290 Product / Item / SKU Master capability. Global Product is only
  the first permission-onboarding slice. Product Definition, GSKU, LSKU and Finished Good permissions may later share
  this ModuleCode only through separately authorized owner scope; this decision creates none of those permissions,
  grants or entitlements automatically.
- `CatalogPermissionSyncService` is best-effort. A logged sync failure cannot be treated as onboarding completion;
  reconciliation/verification must prove both Auth records before enablement.
- The real contracts establish the persistence gap: `ModuleManifestDocument` exposes `ModuleCode`, `ModuleName`, `Domain`
  and `Service` but no authenticated/persisted owner identifier; `ModuleCatalogItem` exposes the same catalog fields plus
  `Origin`, while its comments and current handler make Domain/Service/operator metadata mutable and allow Manual →
  SelfRegistered conversion. None is a safe immutable owner proof.
- The inspected S2S path proves the sender gap: MDM sends only manifest JSON plus shared `X-Internal-Api-Key`; Platform's
  `[AllowAnonymous]` controller validates that common key and dispatches no authenticated producer identity. Platform JWT
  validation does not close the gap because AuthService has no service-token/client-credentials or producer-claim issuance.
- `ProducerOwnerCode` now closes only the persistence half: string, canonical `DITENMDMSERVICE`, sourced from authenticated
  server context, write-once, and never inferred for legacy rows. It does not authenticate the caller by itself.
- The user-approved mechanism is per-service credential authentication because it is the minimum extension consistent
  with the existing direct MDM-to-Platform options transport and timing-safe shared-key comparison. It does not claim that
  the mechanism already exists: Section 5 is the complete authority for adding its identifier/secret binding,
  authenticator, controller context and focused tests.
- Runtime flow is fixed: MDM obtains identifier/secret only from secure binding and sends the two Section 14 headers;
  Platform looks up the identifier, timing-safely validates an eligible active/previous secret, derives
  `DITENMDMSERVICE` server-side, then dispatches the manifest with non-bindable trusted context. Reconciliation begins only
  after that context matches the manifest/module authorization map and immutable catalog owner.
- Production secret material is not a repository artifact. Deployment must provision it through the existing secure
  configuration/environment/secret-store boundary. The active/previous values, overlap deadline and revocation state are
  operational inputs; none receives a committed default or example value.
- `PlatformTenantEntitlementClient` currently collapses confirmed empty and transport/configuration/malformed failures to
  the same empty list. Runtime delivery must preserve an explicit authoritative-versus-uncertain outcome through the
  client/consumer path so uncertainty cannot trigger either fallback grant or stale-grant revoke.
- `PermissionAuthorizationHandler` in MDM currently recognizes the exact claims and also contains a generic
  `platform_admin` shortcut. This pack does not change or expand that code. Tenant Admin/Viewer/entitlement tests must
  use explicit permission claims; platform-admin success is not acceptance evidence and no new bypass may be added.
- Current access-token TTL default is 15 minutes. The refresh path re-reads current role permissions. This pack does
  not invent a deny-list or change FU13 cache/token policy.
- This pack amendment itself seeded no permission, registered no manifest and granted no user or role.

### Local Development reconciliation evidence — 2026-08-09

- `product-item-sku-master` is active for the existing pilot tenant and the exact Global Product read/create descriptors
  are present in the runtime manifest/catalog chain.
- A refreshed Admin session proved Global Product read/create; Viewer proved read and create denial. The retained live
  create/read smoke and tenant-isolation checks close the earlier `permission/smoke open` Development drift.
- No Production/Staging enablement, committed secret, navigation change or new permission is claimed. Production
  credential rotation/runbook and environment-specific enablement remain open.

## 20. Follow-up Items

- Platform/Security and MDM operations owners must provision the production identifier and secret through the approved
  secret boundary and own a rotation/revocation runbook, including overlap duration, `PreviousValidUntilUtc`, emergency
  revocation and audit-correlation procedure. This is a production-enablement follow-up, not permission to commit secrets.
- Runtime implementation is complete within the Section 5 allow-list. No trusted-sender design decision remains open;
  Production provisioning/runbook evidence remains required before enablement.
- Onboarding a future producer requires a separately approved credential identifier-to-owner-to-manifest mapping,
  lifecycle/provisioning decision and exact allow-list amendment. This MDM mapping is not a generic service registry.
- Recheck `product-item-sku-master` against the current Platform catalog store. A live or soft-deleted conflicting owner
  blocks delivery; do not adopt, restore or rename it to `global-product`, `mdm`, `MOD-0290` or another fallback.
- Future Product Definition, GSKU, LSKU and Finished Good permission onboarding requires separately authorized scope;
  the shared ModuleCode is attribution, not blanket permission/grant/entitlement authority.
- If security requires removal of the pre-existing generic MDM `platform_admin` bypass, that remains a separately approved
  MDM/security owner delivery outside the exact MDM registration allow-list.
- Gateway/UI Local Development evidence is recorded; Production enablement remains a separate blocked gate.
