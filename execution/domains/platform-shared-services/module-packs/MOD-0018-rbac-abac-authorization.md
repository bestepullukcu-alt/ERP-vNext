---
id: MOD-0018
name: RBAC / Entitlement Production Wiring
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

# MOD-0018 - RBAC / Entitlement Production Wiring

> **Draft note:** Bu pack, mevcut MOD-0018 enforcement altyapisini yeni kapsam kararina gore yeniden sozlesmelestirir. Kod yazimi bu pack `approved` veya `ready-for-dev` yapilmadan baslamaz.

> **Golden Reference karari:** Bu is UI/DataTable modulu degildir. `shell: none`, `golden_reference: none`, `form_field_count: 0`. Razor layout, DataTable verifier, RESX ve frontend dosya seti bu pack icin N/A'dir.

## 1. Module Summary

MOD-0018, permission check ile tenant/module/feature entitlement kararini birlestiren backend-only production wiring isidir. Amac; ileride HR, CRM, BPM gibi tenant modulleri geldiginde RBAC/entitlement, data scope ve process-based temporary access icin yeniden kokten refactor gerektirmeyecek contract foundation'i kurmaktir.

Bu pack mevcut authorization davranisini kirmadan ilerler:
- Mevcut `[RequiresModule]`, `[RequiresFeature]`, `IEntitlementChecker`, `EntitlementCacheService`, `AuthorizationProbeController` korunur.
- Ilk gelistirme adimi contract-only foundation'dir.
- Gercek tenant business controller wiring bu fazda yapilmaz.

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

**1. adimda degisecek dar scope:**
- `services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/**`
- `services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/**`

## 6. Protected Paths

- `.antigravity/**` - global engineering system, kullanici onayi olmadan degismez.
- `services/Diten.AuthService/**` - Roles/Permissions CRUD ve token claim uretimi bu pack disinda.
- `frontend/Diten.Web/**` - RBAC Admin UI ve tenant UI bu pack disinda.
- `gateway/Diten.ApiGateway/**/ocelot.json` - gerekirse yalniz integration-agent scope'u.
- `services/Diten.MdmService/**`, `services/Diten.EnterpriseStrategyService/**`, `services/Diten.DevEnablementService/**` - diger domain servisleri.
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` - FROZEN.

## 7. Dependencies

- MOD-0298 Tenant Module Entitlement: effective module access source.
- PSS-007 Subscription Feature Management: feature catalog/source.
- MOD-0297 Tenant Subscription Lifecycle: plan/status degisikligi source'u.
- MOD-0035 Event Bus / Internal Events: outbox-backed publish/consume.
- MOD-0021 Audit Trail: deny audit proof.
- Existing Auth/JWT permission claims: `[HasPermission]` sistemi.
- `IPlatformCatalogContract`: assignable module metadata source'u.

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

## 14. Authorization Convention

- Permission gate mevcut `[HasPermission(...)]` sistemi ile devam eder.
- Module gate: `[RequiresModule("MODULE_CODE")]`.
- Feature gate: `[RequiresFeature("FEATURE_CODE")]`.
- Data scope bu fazda authorization kararini degistirmez; yalniz contract foundation olarak eklenir.
- Temporary access bu fazda role/permission grant degildir; `ITemporaryAccessProvider` uzerinden read-only runtime source olarak tasarlanir.
- Gercek tenant module controller wiring ilk pilot tenant modulune birakilir.

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

**S2S / attestation reconciliation amendment (bounded):**

- `MOD-0018-FU16` current registry identity remains reserved for **Global Product Permission Onboarding**. Historical S2S material that used the same FU16 identity is not carried forward under that identity and creates no replacement FU identifier.
- The S2S authorization, delegated-proof and entitlement-attestation foundation is a bounded MOD-0018 parent amendment. It remains default-disabled, non-production and non-activating until separately reconciled executable slices prove their own scope and evidence gates.
- This amendment does not authorize a production key, secret, vault configuration, endpoint, authentication-scheme activation, deployment, Gateway route or automatic permission grant. User/session HS256 and current FU16 Product behavior remain separate and unchanged.
- Future executable work must preserve: exact tenant/module/request binding; explicit tenant-scoped grants only; dormant-grant semantics on entitlement disable; authoritative indeterminacy as fail-closed; and no cache or last-known-good allow path.

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

`MOD-0018-FU16` is registry-owned by Global Product Permission Onboarding. The bounded S2S / attestation reconciliation amendment above intentionally does not mint or claim another FU identity.

The older FU1-FU9 notes are historical planning shorthand and are superseded for registry-controlled identity by the MOD-0018-FU10..FU15 chain above. No implementation status is changed by this reconciliation note.
