---
id: MOD-0018
name: RBAC / ABAC Authorization
domain: platform-shared-services
service: Diten.Platform.Common
shell: none
golden_reference: none
entity_base: GlobalEntity
status: ready-for-dev
owner: platform-team
branch: feature/pss/mod-0018-rbac-entitlement-production-wiring
started: 2026-05-20
target: 2026-06-05
form_field_count: 0
---

# MOD-0018 - RBAC / ABAC Authorization

> **Draft note:** Bu pack, mevcut MOD-0018 enforcement altyapisini yeni kapsam kararina gore yeniden sozlesmelestirir. Kod yazimi bu pack `approved` veya `ready-for-dev` yapilmadan baslamaz.

> **Golden Reference karari:** Bu is UI/DataTable modulu degildir. `shell: none`, `golden_reference: none`, `form_field_count: 0`. Razor layout, DataTable verifier, RESX ve frontend dosya seti bu pack icin N/A'dir.

## 1. Module Summary

MOD-0018, permission check ile tenant/module/feature entitlement kararini birlestiren backend-only production wiring isidir. Amac; ileride HR, CRM, BPM gibi tenant modulleri geldiginde RBAC/entitlement, data scope ve process-based temporary access icin yeniden kokten refactor gerektirmeyecek contract foundation'i kurmaktir.

Bu pack mevcut authorization davranisini kirmadan ilerler:
- Mevcut `[RequiresModule]`, `[RequiresFeature]`, `IEntitlementChecker`, `EntitlementCacheService`, `AuthorizationProbeController` korunur.
- Ilk gelistirme adimi contract-only foundation'dir.
- Gercek tenant business controller wiring bu fazda yapilmaz.

### S2S authorization foundation amendment — approved 2026-08-29

The Control Tower approved a bounded, default-off and non-production reconciliation of **S2S Authorization,
Delegation and Permission Provisioning** under canonical parent `MOD-0018`. This amendment creates no new FU identity.
In particular, the historical label `MOD-0018-FU16 — S2S Authorization, Delegation and Permission Provisioning` is
retained only as provenance for earlier governance and implementation evidence. It is not a canonical module/runtime
identity and must not be emitted as a runtime `ModuleCode`, manifest owner or audit source. Current canonical
`MOD-0018-FU16` through `MOD-0018-FU20` keep their existing Product / Item / SKU Master permission-onboarding identities
and remain byte-for-byte outside this amendment.

The authorized handoff is an additive internal foundation only: AuthService principal/credential/proof/replay,
permission-manifest and explicit-grant contracts; protocol-only Platform.Common validation; shared signed-event
contracts; and non-activated Gate-I audit intake evidence. It authorizes no public endpoint, authentication-scheme
activation, listener/worker start, broker/live delivery, production key or secret, JWKS route, deployment, Gateway,
frontend or WorkCenter change.

## 2. Ownership and Boundaries

**In scope:**
- MOD-0018 RBAC / entitlement enforcement foundation.
- Data scope contract foundation: `Company`, `Country`, `Own`, `Assigned`, `ProcessRelatedRecord`.
- Process-based temporary access foundation: contract/interface/result model; gercek BPM implementasyonu yok.
- Cache invalidation hazirligi ve event publish/consume planinin kodlanabilir hale gelmesi.
- Audit proof: mevcut `PlatformEntitlementAuditSink` davranisinin testlerle kanitlanmasi.
- Test-only `AuthorizationProbeController` uzerinden smoke/integration stratejisi.

**Out of scope:**
- `Diten.AuthService` Roles/Permissions CRUD.
- RBAC Admin UI.
- CRM/HR/BPM modulu gelistirmek.
- Gercek tenant business controller wiring.
- Buyuk ABAC/policy DSL engine.
- Platform admin endpoint'lerine fake `[RequiresModule]` / `[RequiresFeature]` eklemek.
- Kalici role/permission grant uzerinden temporary access vermek.

### S2S amendment boundary

**In scope only through the exact Section 5 allow-list:**

- Separate `diten-delegated-actor-proof+jwt` token-family contracts with exact RS256, issuer/audience/`kid`, request
  binding and replay/freshness semantics; no HS256 fallback.
- Global `ServicePrincipal` and credential-descriptor metadata, replay receipts and transaction/fence seams. Private
  key or credential material is never persisted in these objects.
- Producer-owned exact operation/permission manifest registration and explicit tenant-scoped role grants. Entitlement
  alone never grants a permission; Admin/Viewer auto-grant and wildcard/alias inference remain prohibited.
- Protocol-only Platform.Common trusted-context/permission evaluation with no producer permission constants.
- BuildingBlocks signed-event and outbox transport contracts plus Platform Gate-I audit acceptance components for
  test/evidence construction only. Composition remains default-off and no live consumer is activated.

**Still out of scope:** Roles/Permissions CRUD/API redesign; user/session JWT changes; public token issuance or JWKS
endpoint; production key/secret/certificate/vault provisioning; authentication-scheme, API, DI listener, hosted-worker
or broker activation; producer-service implementation; permission seeds outside owner manifests; deployment,
Gateway, frontend and WorkCenter. The amendment cannot change any current MOD-0018 FU pack or use the historical
colliding FU16 label as an authorization identity.

## 3. Owned Objects

**Mevcut ve korunacak objeler:**
- `RequiresModuleAttribute`, `RequiresFeatureAttribute`.
- `EntitlementAuthorizationPolicyProvider`.
- `TenantModuleAuthorizationHandler`, `TenantFeatureAuthorizationHandler`.
- `IEntitlementChecker`, `EntitlementCheckResult`, `EntitlementDenyReason`, `EntitlementKind`.
- `EntitlementChecker`, `EntitlementCacheService`, `EntitlementCacheOptions`.
- `PlatformEntitlementAuditSink`.
- `AuthorizationProbeController`.

**Yeni contract-only foundation objeleri:**
- `EntitlementDataScopeKind`.
- `EntitlementDataScope`.
- `TemporaryAccessGrant`.
- `ITemporaryAccessProvider`.
- `NoOpTemporaryAccessProvider`.

**Sonraki adim objeleri:**
- Entitlement/subscription cache invalidation event contracts.
- `EntitlementCacheInvalidationConsumer`.
- Event publish noktalarinin command handler'lara eklenmesi.

**S2S amendment owned contracts (non-production/default-off):**

- `ServicePrincipal`, `ServiceCredentialDescriptor`, `S2SReplayReceipt` and their exact lifecycle/version/generation
  predicates.
- `DelegatedActorProofV1`, canonical request binding, typed issuance/validation results and trusted provenance projection.
- Dedicated FU-neutral S2S Mongo context and canonical UUID policy, isolated from the common AuthService Mongo client.
- `PermissionCatalogManifestV1` and explicit-role-grant provisioning/reconciliation contracts.
- Platform.Common `IPermissionClaimEvaluator` / `SignedJwtPermissionClaimEvaluator` protocol seam.
- BuildingBlocks signed-event, trusted-transport metadata, canonical outbox and delivery contracts.
- Platform Gate-I audit parser, adapter registry, consumer processor and atomic inbox/outbox acceptance repository.

These are parent-MOD-0018 contracts. Namespaces and test descriptions may retain `S2S` or `GateI`; they must not use
the historical colliding FU16 label as a canonical module identity.

## 4. Entity Fields

Bu pack'in 1. adimi persisted entity eklemez.

| Object | Field | Type | Required | Rule |
|---|---|---:|---:|---|
| `EntitlementDataScopeKind` | values | enum | yes | Sadece `Company`, `Country`, `Own`, `Assigned`, `ProcessRelatedRecord` |
| `EntitlementDataScope` | `Kind` | enum | yes | `EntitlementDataScopeKind` degeri |
| `EntitlementDataScope` | `Value` | string? | no | `Company/Country/Assigned/ProcessRelatedRecord` icin ileride kullanilabilir; `Own` icin null olabilir |
| `TemporaryAccessGrant` | `ProcessInstanceId` | string | yes | Bos olamaz; BPM implementasyonu bu fazda yok |
| `TemporaryAccessGrant` | `ModuleCode` | string | yes | Normalize edilebilir module code |
| `TemporaryAccessGrant` | `FeatureCode` | string? | no | Opsiyonel feature-level temporary access |
| `TemporaryAccessGrant` | `ExpiresAtUtc` | DateTimeOffset | yes | Gecmis tarih access vermemeli |
| `TemporaryAccessGrant` | `DataScopes` | IReadOnlyList<EntitlementDataScope> | yes | Empty olabilir ama null olamaz |

Kalici storage bu fazda yoktur. Persistent process access gerekiyorsa ayri pack ile `BaseEntity` tenant-scoped model olarak tasarlanir.

## 5. Repo Scope

**Dokunulabilecek yollar:**
- `execution/domains/platform-shared-services/module-packs/MOD-0018-rbac-abac-authorization.md`
- `services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/**`
- `services/Diten.Platform/src/Diten.Platform.Application/Services/**`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Entitlements/Handlers/CommandHandlers/**`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Subscriptions/Handlers/CommandHandlers/**`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Eventing/**`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/DependencyInjection.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/**`
- `services/Diten.Platform/tests/Diten.Platform.Eventing.Tests/**`

### S2S amendment exact implementation allow-list

No path outside this list is implied by a directory, namespace, project reference or historical commit. Dedicated
subtree wildcards below are exact ownership roots; every adjacent/shared file is named individually.

**AuthService — dedicated source roots:**

- `services/Diten.AuthService/src/Diten.AuthService.Domain/S2S/**`
- `services/Diten.AuthService/src/Diten.AuthService.Application/S2S/**`
- `services/Diten.AuthService/src/Diten.AuthService.Infrastructure/S2S/**`
- `services/Diten.AuthService/src/Diten.AuthService.Persistence/S2S/**`

**AuthService — exact shared seams:**

- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IDelegatedActorProofIssuer.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IDelegatedActorProofValidator.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IExplicitRoleGrantProvisioning.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IPermissionCatalogManifestRegistrar.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IS2SPrivateSigningKeyProvider.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IS2SProofAcceptanceCoordinator.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IS2SReplayReceiptStore.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IS2STrustedValidationKeyProvider.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IServiceCredentialDescriptorRepository.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IServicePrincipalRepository.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Application/DependencyInjection.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Domain/Entities/Permission.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Domain/Entities/Role.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Persistence/Configurations/MongoDbIndexConfigurations.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Persistence/Configurations/S2SMongoIndexInitializer.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Persistence/DependencyInjection.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Persistence/MongoGuidRepresentationPolicy.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/AuthCommonUuidCompatibilityGuard.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/ExplicitRoleGrantProvisioningCoordinator.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/PermissionCatalogManifestRegistrar.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/RoleAssignmentVersionRepository.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/S2SProofAcceptanceCoordinator.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/S2SReplayReceiptStore.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/ServiceCredentialDescriptorRepository.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/ServicePrincipalRepository.cs`

**AuthService — exact evidence roots/files:**

- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/S2S/**`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/RolePermissionRepositoryMongoRaceTests.cs`

**Platform.Common — exact protocol/evidence files:**

- `services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/IPermissionClaimEvaluator.cs`
- `services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/SignedJwtPermissionClaimEvaluator.cs`
- `services/Diten.Platform.Common/tests/Diten.Platform.Common.Tests/Diten.Platform.Common.Tests.csproj`
- `services/Diten.Platform.Common/tests/Diten.Platform.Common.Tests/SignedJwtPermissionClaimEvaluatorTests.cs`

**BuildingBlocks Eventing — exact transport files:**

- `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.Eventing/EventName.cs`
- `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.Eventing/EventOutboxContracts.cs`
- `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.Eventing/EventOutboxPublisherProcessor.cs`
- `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.Eventing/EventTransportExceptions.cs`
- `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.Eventing/EventTransportMessage.cs`
- `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.Eventing/HmacSha256EventSignature.cs`
- `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.Eventing/ICanonicalIntegrationEvent.cs`
- `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.Eventing/IEventTransportPublisher.cs`
- `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.Eventing/ITrustedTransportMetadataProvider.cs`
- `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.Eventing/OutboxEventBus.cs`
- `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.Eventing/RawSignatureHeaderExtractor.cs`
- `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.Eventing/SignedEventContracts.cs`
- `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.Eventing/TrustedTransportMetadata.cs`
- `services/Diten.Building.Blocks/tests/Diten.BuildingBlocks.Eventing.Tests/Diten.BuildingBlocks.Eventing.Tests.csproj`
- `services/Diten.Building.Blocks/tests/Diten.BuildingBlocks.Eventing.Tests/EventNameContractTests.cs`
- `services/Diten.Building.Blocks/tests/Diten.BuildingBlocks.Eventing.Tests/SharedOutboxDeliveryTests.cs`
- `services/Diten.Building.Blocks/tests/Diten.BuildingBlocks.Eventing.Tests/SignedEventSecurityFoundationTests.cs`

**Platform Eventing — exact default-off Gate-I intake/evidence files:**

- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Eventing/GateIAuditAcceptanceRepository.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Eventing/GateIAuditAdapterRegistry.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Eventing/GateIAuditConsumerProcessor.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Eventing/GateIAuditIntentParser.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Schema/PlatformCollections.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Schema/PlatformSchemaManifest.Eventing.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Eventing/GateIAuditBilateralFixtureTests.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Eventing/GateIAuditContractTests.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Eventing/GateIAuditDisposableReplicaSetTests.cs`
- `services/Diten.Platform/tests/Diten.Platform.Eventing.Tests/EventingMvpTests.cs`
- `services/Diten.Platform/tests/Diten.Platform.Eventing.Tests/RabbitMqEventingIntegrationTests.cs`

The two `Diten.Platform.Eventing.Tests` files are authorized only for explicit namespace/type-alias qualification that
resolves canonical BuildingBlocks versus legacy Platform transport-name ambiguity in the existing tests. This does not
authorize assertion or behavior changes, live RabbitMQ execution, transport wiring, runtime composition or activation.

The two schema files are an exact DB-010 exception only for the default-off Gate-I audit-intake evidence collection.
`PlatformCollections` remains the single collection-name authority and `PlatformSchemaManifest.Eventing` remains the
single Eventing-profile index authority. This exception does not authorize a second schema initializer, direct
`EnsureIndexesAsync` use in tests, per-run databases, production DI, listener/worker activation or any non-Eventing
schema change.

Producer-owned canonical fixtures and module-specific adapter mappings remain read-only inputs. A missing producer
mapping or fixture requires that producer/MOD-0021 owner's own authority; this parent amendment cannot invent or copy it.

**1. adimda degisecek dar scope:**
- `services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/**`
- `services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/**`

This legacy "first step" list continues to govern the original entitlement/data-scope slice only. It does not narrow,
expand or conflict with the separately approved S2S amendment; S2S work is governed exclusively by the exact allow-list
above.

## 6. Protected Paths

- `.antigravity/**` - global engineering system, kullanici onayi olmadan degismez.
- `services/Diten.AuthService/**` - Roles/Permissions CRUD ve token claim uretimi bu pack disinda.
- `frontend/Diten.Web/**` - RBAC Admin UI ve tenant UI bu pack disinda.
- `gateway/Diten.ApiGateway/**/ocelot.json` - gerekirse yalniz integration-agent scope'u.
- `services/Diten.MdmService/**`, `services/Diten.EnterpriseStrategyService/**`, `services/Diten.DevEnablementService/**` - diger domain servisleri.
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` - FROZEN.

**S2S amendment reconciliation:** `services/Diten.AuthService/**` is no longer wholly protected for this one bounded
foundation; only the exact Section 5 AuthService paths are exempt. All other AuthService files remain protected,
including API controllers, `Program.cs`, authentication-scheme/user-session JWT wiring, token endpoints, JWKS/public
key routes, appsettings, secret/key/certificate providers and existing Roles/Permissions CRUD surfaces. Platform.Common,
BuildingBlocks Eventing and Platform Eventing receive the same exact-path-only exception. Current MOD-0018-FU16 through
FU20 packs, producer packs/fixtures, MOD-0021/MOD-0035 governance, registry, DCPs and `.antigravity/**` remain protected.

## 7. Dependencies

- MOD-0298 Tenant Module Entitlement: effective module access source.
- PSS-007 Subscription Feature Management: feature catalog/source.
- MOD-0297 Tenant Subscription Lifecycle: plan/status degisikligi source'u.
- MOD-0035 Event Bus / Internal Events: outbox-backed publish/consume.
- MOD-0021 Audit Trail: deny audit proof.
- Existing Auth/JWT permission claims: `[HasPermission]` sistemi.
- `IPlatformCatalogContract`: assignable module metadata source'u.

**S2S amendment dependencies:** canonical parent MOD-0018 identity; producer-owner exact operation/permission manifests;
MOD-0021 audit-intake and MOD-0035 signed-event policy; existing HS256 user/session JWT regression boundary; Mongo
transaction-capable test topology for replay/grant evidence; and owner-supplied entitlement decisions. These are
dependencies, not authority to edit the dependent module packs or producer services.

Conditional:
- RabbitMQ live degilse cache invalidation TTL-only fallback ile calisir.
- BPM/process engine yoksa `NoOpTemporaryAccessProvider` empty/no access doner.

## 8. Runtime Constraints

- Existing authorization behavior degismeyecek.
- `EntitlementCheckResult` public imzasi degismeyecek.
- `platform_admin` module/feature entitlement check icin bypass kalir.
- `partner_admin` this release'de fail-closed kalir; signed `allowed_tenant_ids` yok.
- `tenant_user` icin `tenant_id` JWT claim zorunludur.
- Platform admin endpoint'lerine fake module/feature gate eklenmez.
- Temporary access kalici role/permission'a yazilmaz.
- Process kapandiginda veya `ExpiresAtUtc` gecildiginde temporary access access vermemelidir.
- Cache invalidation yoksa TTL fallback korunur; startup fail etmez.

**S2S amendment runtime constraints:**

- The foundation is default-off and non-production. No endpoint, scheme, listener, hosted worker, broker subscription,
  signing provider or key provider is activated by its presence.
- S2S proofs are a separate family: exact `typ=diten-delegated-actor-proof+jwt`, `alg=RS256`, one exact non-empty `kid`,
  RSA >=3072 bits, one receiver-specific audience and no algorithm/audience/HS256 fallback.
- Tenant, delegated actor, client/principal, credential generation, operation, permission and canonical request binding
  are exact/ordinal. Missing, wildcard, alias, case-folded or stale evidence fails closed.
- Replay receipt plus principal/credential freshness fences are atomic. Duplicate replay is typed `401`; authority or
  persistence uncertainty is typed `503`; cancellation propagates. Unknown commit never replays the transaction body.
- Permission manifests retain producer ownership. Parent MOD-0018 validates registration and provisions only explicit
  tenant-scoped grants; entitlement alone, Admin/Viewer role names or catalog presence never auto-grants.
- Shared Platform.Common and Eventing code contains no producer-specific permission or secret. Test-only identity/key
  material is rejected by production policy and is never promoted to production configuration.
- Gate-I audit intake accepts only verified trusted transport metadata and an owner-provided exact adapter profile; no
  live broker delivery or producer publication is authorized.

## 9. Layout & Shell Contract

`shell: none`. Bu pack backend/shared-contract isidir.

- Razor view yok.
- `Layout = "_LayoutPlatformAdmin"` veya `_LayoutTenantShell` kullanilmaz.
- Frontend route, DataTable, RESX ve Ctrl+K search registry N/A.

## 10. Backend File Convention

Bu pack CRUD/DataTable module olmadigi icin Golden Reference CQRS klasor seti birebir uygulanmaz. Degisiklikler mevcut authorization/service/eventing klasorlerine minimal ekleme olarak yapilir.

Kurallar:
- Her yeni public type kendi dosyasinda olur.
- Existing namespace pattern korunur.
- Runtime kodu Application/Common/Infrastructure sinirlarina gore yerlesir:
  - shared contracts: `Diten.Platform.Common/Authorization`
  - app service/DI: `Diten.Platform.Application`
  - event consumer/transport registration: `Diten.Platform.Infrastructure`
- Handler/consumer isimleri sorumluluk odakli ve tek amacli olur.

## 11. Frontend File Contract

Frontend dosyasi yoktur.

- RBAC Admin UI yok.
- DataTable yok.
- Razor partial yok.
- `golden_reference: none` bu nedenle dogrudur.

## 12. Validation Rules

| Rule | Applies to | Expected |
|---|---|---|
| Scope enum fixed set | `EntitlementDataScopeKind` | Sadece 5 deger: `Company`, `Country`, `Own`, `Assigned`, `ProcessRelatedRecord` |
| Process id required | `TemporaryAccessGrant.ProcessInstanceId` | null/empty access grant sayilmaz |
| Module code required | `TemporaryAccessGrant.ModuleCode` | null/empty access grant sayilmaz |
| Feature optional | `TemporaryAccessGrant.FeatureCode` | null ise module-level grant kabul edilir |
| Expiry required | `TemporaryAccessGrant.ExpiresAtUtc` | gecmis tarih access vermez |
| Data scopes non-null | `TemporaryAccessGrant.DataScopes` | null yerine empty list |
| NoOp provider | `NoOpTemporaryAccessProvider` | her zaman empty/no access |

## 13. Failure Path to Verify

- Anonymous probe request -> 401, audit yazilmaz.
- Tenant user missing `tenant_id` -> 403.
- Tenant user module not entitled -> 403 + `ModuleAccess` deny audit.
- Tenant user feature not enabled -> 403 + `FeatureAccess` deny audit.
- Audit service throws -> authorization deny sonucu degismez.
- NoOp temporary provider -> process access yok, existing authorization behavior degismez.
- Expired temporary grant model -> access verilmemesi test edilir.
- Cache stale event yoksa -> TTL fallback korunur.
- RabbitMQ disabled -> app startup fail etmez.
- Platform admin -> entitlement checker cagrilmadan pass.
- Partner admin -> fail-closed.

**S2S amendment failure paths:** wrong token family/typ/alg/kid/issuer/audience, weak/revoked/expired key, mismatched
request hash, tenant, actor, operation or permission, stale principal/credential/grant fence, duplicate replay,
cross-tenant grant, unknown manifest owner and test-only production identity all fail before protected work. Terminal
authentication/replay failures never become retryable allows; transient authority/persistence failures return typed 503
with zero protected-handler effect. Duplicate raw/case-duplicate signature headers fail before dictionary normalization.

## 14. Authorization Convention

- Permission gate mevcut `[HasPermission(...)]` sistemi ile devam eder.
- Module gate: `[RequiresModule("MODULE_CODE")]`.
- Feature gate: `[RequiresFeature("FEATURE_CODE")]`.
- Data scope bu fazda authorization kararini degistirmez; yalniz contract foundation olarak eklenir.
- Temporary access bu fazda role/permission grant degildir; `ITemporaryAccessProvider` uzerinden read-only runtime source olarak tasarlanir.
- Gercek tenant module controller wiring ilk pilot tenant modulune birakilir.

**S2S amendment convention:** service-principal authentication and delegated authorization are separate gates. A valid
signature does not grant an operation. The receiver requires its exact owner-manifest operation/permission mapping,
explicit tenant entitlement posture, delegated-actor freshness and explicit tenant-scoped grant. Trusted transport
metadata is server-created and cannot be supplied by producer payload fields. No producer permission constant enters
Platform.Common or BuildingBlocks.

## 15. Gateway / API Routing Decision

Gateway route degisikligi gerekli degildir.

- Yeni public endpoint acilmaz.
- `AuthorizationProbeController` test/dev-only yuzey olarak kalir.
- `gateway/Diten.ApiGateway/**/ocelot.json` bu pack tarafindan degistirilmez.

## 16. Acceptance Criteria

1. Existing authorization behavior degismez.
2. `EntitlementCheckResult` mevcut public yuzeyi korunur. **Additive, default-valued, backward-compatible extension'lara izin verilir** (revize 2026-05-21; bkz §19 "EntitlementCheckResult Extension Policy"). Mevcut `Allowed(EntitlementKind, string, DateTimeOffset?)` ve `Denied(...)` static factory overload'lari kirilmaz; mevcut `new EntitlementCheckResult(...)` positional constructor cagrilari derlenmeye devam eder; tum mevcut testler degismeden gecer. Yeni alanlar default'lu olarak record'a eklenir veya `init` accessor'li property olarak gelir.
3. `EntitlementDataScopeKind` mevcut be deger korunur: `Company`, `Country`, `Own`, `Assigned`, `ProcessRelatedRecord`. **Additive enum genisletmesi follow-up pack tarafindan yapilabilir** (MOD-0018-FU10a; mevcut degerlerin numeric ID'si degismez).
4. `TemporaryAccessGrant` su alanlari tasir: `ProcessInstanceId`, `ModuleCode`, optional `FeatureCode`, `ExpiresAtUtc`, `DataScopes`.
5. `ITemporaryAccessProvider` contract'i role/permission persistence yapmaz.
6. `NoOpTemporaryAccessProvider` empty/no access doner.
7. `NoOpTemporaryAccessProvider` DI'a default implementation olarak register edilir.
8. NoOp provider eklenmesi `[RequiresModule]` / `[RequiresFeature]` sonucunu degistirmez.
9. `PlatformEntitlementAuditSink` deny audit payload'i `SourceModule=MOD-0018`, `Operation=PermissionDenied`, `Outcome=Denied` olarak test edilir.
10. Entitlement/subscription mutation handler'larinda event publish planina uygun event contract listesi netlesir; bu adim 1'de publish kodu yazilmaz.
11. Platform admin endpoint'lerine fake gate eklenmez.
12. Gercek tenant business controller wiring yapilmaz.
13. The S2S foundation is parent-owned under MOD-0018 without creating a new FU or changing FU16-FU20.
14. Historical `MOD-0018-FU16` S2S evidence is provenance-only; no runtime/module identity uses that colliding label.
15. Runtime/test changes stay inside the exact Section 5 allow-list and composition remains default-off/non-production.
16. HS256 user/session JWT, current Roles/Permissions CRUD and existing entitlement behavior pass unchanged.
17. RS256-only proof validation, request binding, replay/freshness atomics, manifest exactness and explicit-grant
    isolation pass targeted positive/negative and real-Mongo evidence where persistence is claimed.
18. Platform.Common remains protocol-only; BuildingBlocks/Platform Eventing remain secret-free and non-activated.
19. Production key/secret/JWKS, endpoint/scheme/listener/worker/broker activation, deployment, Gateway, frontend and
    WorkCenter diffs are zero.
20. MOD-0018-FU16 through FU20 hashes remain byte-identical to the amendment baseline.
21. Gate-I audit persistence declares its collection name once in `PlatformCollections` and its indexes once in
    `PlatformSchemaManifest.Eventing`; focused tests apply only `SchemaProfile.Eventing`, never the full production
    initializer, and leave no per-run database or disposable-Mongo residue.
22. `EventingMvpTests.cs` and `RabbitMqEventingIntegrationTests.cs` may change only through explicit namespace/type
    aliases needed to distinguish canonical BuildingBlocks contracts from legacy Platform transport types; test
    behavior and live-RabbitMQ disposition remain unchanged.

## 17. Test Expectations

**1. adim unit tests:**
- `EntitlementDataScopeKind` enum exact value set testi.
- `TemporaryAccessGrant` data contract construction/default behavior testi.
- `NoOpTemporaryAccessProvider` empty/no access testi.
- DI smoke: `ITemporaryAccessProvider` default olarak `NoOpTemporaryAccessProvider` resolve olur.
- Existing authorization handler tests pass.

**Sonraki adim tests:**
- Cache eviction unit tests.
- Entitlement event contract validation tests.
- Subscription changed event publish tests.
- `EntitlementCacheInvalidationConsumer` idempotent consume tests.
- Probe-based HTTP tests: 401/403/200.
- Audit sink integration testleri.

**S2S amendment tests:**

- AuthService contract/crypto tests cover exact family/header/claims, RSA >=3072, issuer/audience, request binding,
  cancellation and terminal 401 versus transient 503 distinctions.
- Transaction-capable Mongo tests cover principal/credential/replay uniqueness, same-session atomic acceptance,
  explicit-grant participant rollback, unknown-commit body-once reconciliation, concurrency and cross-tenant isolation.
- Permission-manifest tests prove exact producer-owned mappings, ordinal/no-wildcard/no-alias behavior, owner collision
  rejection and zero Admin/Viewer/entitlement side effects during registration.
- Platform.Common tests prove authenticated principal, non-empty tenant/subject and exact permission evaluation without
  producer constants or HS256/S2S family fallback.
- BuildingBlocks Eventing tests prove raw duplicate/case-duplicate header rejection, canonical signing input,
  active/previous-key boundary, revocation/freshness and test-only production rejection.
- Platform Eventing tests prove exact adapter mapping, canonical payload/metadata/signing-identity conflict behavior,
  atomic inbox/outbox acceptance, cancellation zero residue and disposable replica-set cleanup.
- Gate-I schema tests prove the audit-intake collection is declared through `PlatformCollections`, included only in
  `SchemaProfile.Eventing`, has its exact indexes applied idempotently, and does not call the production-wide
  `EnsureIndexesAsync` path or create a database per test run.
- Existing Platform Eventing tests compile with explicit aliases for ambiguous canonical BuildingBlocks and legacy
  Platform transport names; their assertions and live-RabbitMQ skip/execution policy remain byte-for-byte semantic
  equivalents.
- Full AuthService, Platform.Common, BuildingBlocks Eventing and relevant Platform Eventing regressions plus API/project
  builds pass before any local implementation checkpoint. Mutation evidence uses non-equivalent expected-red guards;
  unsafe port/secret/activation mutants receive explicit security-policy-forbidden disposition instead of execution.

**Build/check commands:**
- `dotnet build services/Diten.Platform.Common/src/Diten.Platform.Common/Diten.Platform.Common.csproj -c Debug`
- `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug`
- `dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests`
- `dotnet test services/Diten.Platform/tests/Diten.Platform.Eventing.Tests`

## 18. Ready-for-dev Checklist

- [ ] Kullanici bu draft pack'i inceleyip scope'u onayladi.
- [ ] Status `approved` veya `ready-for-dev` yapildi.
- [ ] Contract-only first step karari onaylandi.
- [ ] `EntitlementCheckResult` mevcut public yuzeyinin korunmasi + additive-only backward-compatible extension'a izin verilmesi politikasi onaylandi (bkz §16 #2 + §19 "EntitlementCheckResult Extension Policy").
- [ ] AuthService Roles/Permissions CRUD out-of-scope kalacak.
- [ ] RBAC Admin UI out-of-scope kalacak.
- [ ] Gercek tenant controller wiring sonraki pilot tenant modulune kalacak.
- [ ] Event contract naming final onaylandi.
- [ ] Temporary access'in kalici role/permission yazmayacagi onaylandi.
- [ ] Data scope enum seti final onaylandi.
- [x] Control Tower approved the parent-MOD-0018 S2S amendment without a new FU identity (2026-08-29).
- [x] Exact AuthService, Platform.Common, BuildingBlocks Eventing, Platform Eventing and evidence allow-lists are locked.
- [x] Exact Gate-I DB-010 schema seams are limited to `PlatformCollections.cs` and
      `PlatformSchemaManifest.Eventing.cs`; production composition/activation remains prohibited.
- [x] Exact Platform Eventing alias-only reconciliation is limited to `EventingMvpTests.cs` and
      `RabbitMqEventingIntegrationTests.cs`; behavior and live RabbitMQ execution remain unauthorized.
- [x] Current MOD-0018-FU16 through FU20 identities and files are protected unchanged.
- [x] Default-off/non-production authority and production activation exclusions are explicit.
- [ ] Current-main semantic implementation and the Section 17 evidence matrix pass before an implementation checkpoint.

## 19. Implementation Notes

**Repo reconciliation notu:**
- Master-plan MOD-0018 bolumundeki "`IEntitlementAuditSink` sadece Null sink" ifadesi artik repo gercegiyle uyumlu degildir; `PlatformEntitlementAuditSink` mevcuttur.
- Current gap daha cok cache invalidation, event publish/consume ve future-proof contract foundation tarafindadir.

**Event naming taslagi:**
- `tenant.entitlement.added.v1`
- `tenant.entitlement.enabled.v1`
- `tenant.entitlement.disabled.v1`
- `tenant.entitlement.expiryupdated.v1`
- `tenant.entitlement.overrideremoved.v1`
- `tenant.subscription.changed.v1`

**Ilk adim stratejisi:**
- Once davranis degistirmeyen contract-only foundation yapilir.
- Sonra cache eviction API ve event invalidation eklenir.
- Event publish/consume tamamlanmadan production controller wiring yapilmaz.

**EntitlementCheckResult Extension Policy (revize 2026-05-21):**

Onceki taslakta §16 acceptance criteria #2 *"`EntitlementCheckResult` imzasi degismez"* ifadesini tasiyordu. Bu maddenin literal okunmasi enterprise authorization icin gerekli foundation extension'larini (`EffectiveScopes`, `ResolvedFrom`, `ResolvedAtUtc` gibi explainability alanlari) bloke etti. MOD-0018-FU10a/FU10b follow-up paketlerinin baslayabilmesi icin madde **additive-only backward-compatible extension'a izin verecek** sekilde revize edildi.

Politika:
- **Korunan yuzey:** Mevcut `EntitlementCheckResult.Allowed(EntitlementKind, string, DateTimeOffset?)` ve `Denied(EntitlementKind, string, EntitlementDenyReason, DateTimeOffset?, bool)` static factory imzalari. Mevcut positional constructor `new EntitlementCheckResult(bool, EntitlementKind, string, EntitlementDenyReason?, DateTimeOffset?, bool)` derlenebilir.
- **Izin verilen genisletmeler:** Record positional parameter'lara **sonradan default'lu** ek alan; `init` accessor'li yeni property; yeni static factory overload (mevcut overload'lar overload resolution'i bozmadan).
- **Yasaklar:** Mevcut alanin tipini degistirmek, mevcut parametrenin sirasini degistirmek, mevcut parametreyi kaldirmak, default degeri olmayan yeni positional parameter eklemek.
- **Test gate:** MOD-0018 mevcut tum testleri (TenantModuleAuthorizationHandlerTests, TenantFeatureAuthorizationHandlerTests, EntitlementCheckerFailureSemanticsTests, EntitlementAuthorizationPolicyProviderTests, PlatformEntitlementAuditSinkTests, AuthorizationProbeControllerIntegrationTests, TemporaryAccessFoundationTests) revize-extension uygulandiktan sonra **degismeden** gecmelidir.

`EntitlementDataScopeKind` icin paralel revize: mevcut be deger sabit kalir; yeni enum degerleri **sona** eklenir, mevcut numeric ID'ler degismez. FU10a bu kurali uygular.

Bu revize MOD-0018 freeze'ini bozmaz; "imza degismez" literal kuralini "yuzey korunur + additive izinli" semantik kuralina cevirir. Revize tarihi pack'in `Implementation Notes` bolumunde audit kaydidir; pack `done` olduktan sonra silinmez.

### S2S historical evidence provenance and canonical reconciliation — 2026-08-29

These checkpoints are immutable evidence inputs, not commits to cherry-pick wholesale and not authority to restore the
historical colliding FU16 pack or registry row:

| Evidence slice | Historical checkpoints |
|---|---|
| Governance definition / manifest / entitlement / approval | `40e38905e253c0d669b6282c65814527e90d533c`, `f2d12a9ab0f7ecf52da4384a3e78ae108b040397`, `0f8e2aa80d7231ddef01edd7a50c35bf152db15d`, `cfbfee00b24314819101faa88d7ed8f1c83c7bd2` |
| Auth principal, credential and replay foundation | `84ad2793f6d3f405205fa4467e581f220e78b041` |
| Auth delegated-proof validation and atomic acceptance | `56acefd9cacfa3cb57a54a6d6c193500a20acbcc` |
| Auth exact permission-manifest registration | `197f351a5e1e3c38db6698c95d2517428ddeb05c` |
| Auth explicit tenant-scoped grants and UUID/freshness closure | `f7dd4a61d8d8910dd1ede56e00e6aeb531ae7305` |
| Platform.Common exact permission evaluator | `5d183069c2c00a94cbe2f151d500dae7050bbc36` |
| Signed-event security foundation | `3bd29dd18d5f42ceaebc6ab44a13d2961458fbc8` |
| Canonical/shared outbox and delivery seams | `cd0af5f9d67bd35c027b4b2015637c580e90fd47`, `60b5306c4fcb98fc190bfb561129a84a10c465af`, `64c3bcab53f6a4c40cfcb7b96ebee935a7f62633` |
| Gate-I audit intake and bilateral evidence | `ac98ab3d7de043e62f2582313ec7793d8ca085c4`, `8349ca96c70b31fb92b84bb836b05a7a3d3f8d30` |

Current-main implementation must reconstruct only the allowed semantics against current files. Whole-file checkout or
bulk cherry-pick is prohibited where it would overwrite newer UUID, Mongo-index, entitlement, module-registration or
eventing behavior. Current canonical FU16-FU20 are unrelated product-permission follow-ups and are not superseded,
amended or implementation dependencies of this parent amendment.

### Gate-I DB-010 schema authority reconciliation — 2026-08-29

The Control Tower authorized only the two exact shared schema seams listed in Section 5 so the non-activated Gate-I
audit-intake evidence can use the current canonical Platform schema system. The runtime/evidence implementation must
reuse the existing Eventing schema profile and single collection-name registry; it may not introduce a parallel index
initializer, a direct collection-name literal, a per-test database, production DI wiring or any listener/broker
activation. This is governance authority for bounded local evidence, not production authority.

## 20. Follow-up Items

Canonical follow-up identity source: `execution/registries/module-id-registry.md`. This parent pack does not create or rename follow-up packs; it only mirrors the registry-controlled chain.

- MOD-0018-FU10: Authorization Decision Contract Extension parent record for FU10a/FU10b.
- MOD-0018-FU10a: Pure Authorization Decision Contract Extension.
- MOD-0018-FU10b: EntitlementChecker ResolvedFrom Mapping.
- MOD-0018-FU11: Temporary Access Pipeline Binding.
- MOD-0018-FU12: Tenant Authorization Context Foundation.
- MOD-0018-FU13: Permission Convention + Cache Invalidation Events.
- MOD-0018-FU14: Effective Access Explain + Allow Audit.
- MOD-0018-FU15: Real DataScopeResolver; replacement for deprecated `NEW-MOD-0041` alias.

The older FU1-FU9 notes are historical planning shorthand and are superseded for registry-controlled identity by the MOD-0018-FU10..FU15 chain above. No implementation status is changed by this reconciliation note.

The 2026-08-29 parent amendment adds no follow-up identity. Its next step is a current-main semantic reconstruction
inside the exact Section 5 allow-list, followed by the Section 17 evidence matrix and a separately verified local
checkpoint. Production key/secret provisioning, endpoint/scheme/listener/worker/broker activation, live delivery,
deployment, Gateway, frontend and WorkCenter remain separate prohibited gates.
