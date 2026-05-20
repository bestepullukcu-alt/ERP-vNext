---
id: MOD-0018
name: RBAC / Entitlement Enforcement
domain: platform-shared-services
service: Diten.Platform.Common
shell: none
golden_reference: none
entity_base: GlobalEntity
status: ready-for-dev
owner: dev2
branch: feature/pss/mod-0018-rbac-entitlement-enforcement
started: 2026-05-18
target: 2026-06-08
form_field_count: 0
---

> **Scope drift notu (2026-05-18):** Pack'in eski backend scope taslağı `Diten.AuthService` merkezliydi; ancak mevcut RBAC/entitlement enforcement scaffold (`TenantModuleAuthorizationHandler`, `TenantModuleRequirement`, `TenantModuleAccessService`) `services/Diten.Platform.Common/Authorization/` ve `services/Diten.Platform/.../Application/Services/` altında. Implementation `Diten.Platform.Common` authorization scaffold üzerinden devam edecek; AuthService Roles/Permissions CRUD zaten ayrı sahiplikte (`AuthService/Features/{Roles,Permissions}`) kalır ve MOD-0018'in ana implementation scope'u olarak ele alınmaz.

> **UI scope kararı (2026-05-18):** MOD-0018 sadece **enforcement** modülüdür (no UI). RBAC Admin UI (role/permission yönetimi ekranları) AuthService'in mevcut Roles/Permissions CRUD'ı üzerine ileride ayrı bir pack ile (örn. PSS-012) çıkılır. Bu pack şu an UI yüzeyi YAZMAZ; `shell: none`, `golden_reference: none`, `form_field_count: 0` bu kararla uyumludur.

> **Master-plan kontrat referansı:** Bu pack'in implementation detaylarını `docs/platform/master-plan.md` §3.21 (MOD-0018) tamamlar — `[RequiresModule]`, `[RequiresFeature]` attribute imzaları, `IEntitlementChecker` contract (batch + deny reason + cache), acceptance criteria genişletilmiş.

# MOD-0018 — RBAC / Entitlement Enforcement

## 1. Module Summary

- **Module ID:** MOD-0018
- **Module Name:** RBAC / Entitlement Enforcement
- **Domain:** Platform & Shared Services
- **Subdomain:** Identity, Access & Trust
- **Planned Wave:** W1
- **UI:** NO (enforcement-only; RBAC Admin UI ayrı bir pack ile çıkar)
- **Purpose:** Permission check (`[HasPermission]`) tek başına yeterli değildir. Bir tenant kullanıcısı belirli bir modüle/feature'a ancak `(rolü yetkili) ∧ (tenant'ı o modüle entitled)` koşulları birlikte sağlandığında erişebilir. Bu pack ikinci kapıyı (entitlement gate) sisteme ekler: yeni `[RequiresModule("CODE")]` / `[RequiresFeature("FEATURE_X")]` attribute'ları, `IEntitlementChecker` contract'ı ve cache-backed authorization handler'ı. Mevcut `TenantModuleAuthorizationHandler` scaffold'u tamamlanır ve deterministic test-only `AuthorizationProbeController` üzerinden integration testlerle doğrulanır.

## 2. Ownership and Boundaries

### Owned objects (SoR)
- `RequiresModuleAttribute`
- `RequiresFeatureAttribute`
- `IEntitlementChecker` contract + cache impl
- `EntitlementCheckResult` record (deny reason taşır)
- `TenantModuleAuthorizationHandler` (mevcut scaffold genişletilir)
- `TenantFeatureAuthorizationHandler` (yeni)
- `EntitlementCacheService` (in-memory L1)
- Cache invalidation event consumer (MOD-0035 hazır olduğunda subscription/entitlement event'lerine subscribe)

### In-scope
- `[RequiresModule]` / `[RequiresFeature]` attribute imzaları ve policy registration
- Authorization handler'lar (deny reason ile)
- Cache strategy (L1 in-memory + TTL + event invalidation)
- Platform admin bypass kuralı
- Partner admin MVP fail-closed behavior; `AllowedTenantIds` allow path AuthService signed scope claims gelene kadar follow-up
- Tenant isolation (JWT `tenant_id` vs request payload `TenantId` mismatch)
- Test-only `AuthorizationProbeController` üzerinde enforcement uygulaması (integration test ile)
- Deny edilen access denemelerinin MOD-0021 audit'e yazılması
- `IPlatformCatalogContract.GetAssignableModulesAsync()` — MOD-0008 alt görevi (MOD-0018 ile aynı sprint'te stabilize edilir)

### Out-of-scope
- AuthService Roles/Permissions CRUD (zaten ayrı sahiplikte, dokunulmaz)
- RBAC Admin UI (ayrı pack — PSS-012 önerisi)
- User directory / IdP ownership
- Complex policy DSL / ABAC condition language
- Advanced analytics/reporting
- Organization-master ownership
- Tenant lifecycle event emission (MOD-0009 işi)
- Live RabbitMQ broker validation (MOD-0035 işi — bu pack TTL-only fallback ile ilerler; consumer broker yoksa inert/no-op kalır)

### Current MVP execution status
- RBAC-first; ABAC minimal (sadece tenant-scope condition).
- Cache strategy: in-memory L1 + TTL + event invalidation (MOD-0035 hazır olunca).

## 3. Dependencies and Interfaces

### Consumed dependencies (mevcut)
- `services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/TenantModuleAuthorizationHandler.cs` (scaffold genişletilir)
- `services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/TenantModuleRequirement.cs` (mevcut)
- `services/Diten.Platform/src/Diten.Platform.Application/Services/TenantModuleAccessService.cs` (entitlement projection okuma)
- `services/Diten.Platform/src/Diten.Platform.Application/Services/ITenantModuleAccessService.cs` (mevcut interface)
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Entitlements/TenantModuleEntitlementAccessEvaluator.cs` (effective access evaluator)
- MOD-0298 Tenant Module Entitlement (mevcut, `EntitlementSource` enum + `EffectiveAccess`)
- PSS-007 Subscription Features (feature definition)
- MOD-0021 Audit Trail (deny event audit'e yazılır)
- `[HasPermission]` attribute (AuthService Permission claim, mevcut)
- `ITenantContext` (TenantId JWT claim'inden)

### Conditional dependencies
- **MOD-0035 Event Bus**: cache invalidation event consumer için. **Eğer broker live değilse** TTL-only cache fallback (aşağıdaki Runtime Constraints §6'ya bak).
- **MOD-0008 IPlatformCatalogContract**: bu pack'in alt görevi — ayrı pack çekilmez, MOD-0018 commit'lerinde stabilize edilir.

### Primary consumers
- Tenant ERP modülleri (MDM, CRM, HR, vs. — gelecek)
- Platform admin endpoint'leri (ek bir koruma kapısı; platform_admin actor zaten bypass)
- Tenant API controller'ları

### Interface stubs
- Attribute `[RequiresModule("CODE")]`
- Attribute `[RequiresFeature("FEATURE_CODE")]`
- Service `IEntitlementChecker` (batch + deny reason)
- Event consumer `EntitlementCacheInvalidationConsumer` (MOD-0035 üzerine)

## 4. Repo Scope

**Inherited convention:** `domain-config.md` ve `module-pack-standard.md` §4 (Backend File Convention).

### Backend implementation scope
- `services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/` — shared authorization attributes, requirements, handlers, entitlement check contract
  - **Yeni:** `RequiresModuleAttribute.cs`, `RequiresFeatureAttribute.cs`, `IEntitlementChecker.cs`, `EntitlementCheckResult.cs`, `EntitlementDenyReason.cs`, `TenantFeatureRequirement.cs`, `TenantFeatureAuthorizationHandler.cs`
  - **Genişler:** `TenantModuleAuthorizationHandler.cs` (deny reason + audit hook + platform admin bypass)
- `services/Diten.Platform.Common/src/Diten.Platform.Common/Catalog/` — MOD-0008 alt görev (yeni klasör)
  - **Yeni:** `IPlatformCatalogContract.cs`, `AssignableModuleInfo.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Services/` — entitlement access service genişletme
  - **Genişler:** `TenantModuleAccessService.cs` (batch query + cache integration)
  - **Yeni:** `EntitlementCacheService.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Eventing/` — MOD-0035 consumer
  - **Yeni:** `EntitlementCacheInvalidationConsumer.cs` (subscription/entitlement event'lerine subscribe)
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/` — test-only `AuthorizationProbeController` eklenir; production business controller'lara fake module gate eklenmez
- `services/Diten.Platform/src/Diten.Platform.API/Program.cs` — policy registration (`AddAuthorization` içinde dynamic policy provider)
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/` — integration test klasörü (yeni)

### Frontend implementation scope
**Bu pack frontend YAZMAZ.** RBAC Admin UI ayrı bir pack (PSS-012 önerisi) ile çıkar; bu pack yalnızca enforcement attribute'ları yazar.

### Protected paths
- `.antigravity/**` (working-agreement zorunlu)
- `services/Diten.AuthService/**` — Roles/Permissions CRUD MOD-0018 scope'unda DEĞİL, dokunulmaz
- `services/Diten.MdmService/**`, `services/Diten.EnterpriseStrategyService/**`, `services/Diten.DevEnablementService/**` (diğer domain'ler)
- `gateway/Diten.ApiGateway/**/ocelot.json` — yeni route gerekirse integration-agent task'i
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` (FROZEN)

## 5. UI Surfaces

**YOK.** MOD-0018 enforcement-only. RBAC Admin UI (Roles/Permissions yönetim ekranları) AuthService'in mevcut CRUD'ı üzerine ileride ayrı bir pack ile (örn. PSS-012 RBAC Admin UI) çıkılır.

## 6. Runtime Constraints

### Deny by default
- Tüm `[RequiresModule]` / `[RequiresFeature]` attribute'lı endpoint'lerde authorization handler fail-closed çalışır.
- Cache miss + DB unreachable durumunda → 503 (Service Unavailable), 200 değil.

### Platform admin bypass
- `actor_type=platform_admin` ise `[RequiresModule]` ve `[RequiresFeature]` her zaman pass.
- Current MVP behavior: `actor_type=partner_admin` fail-closed kalır; allow path aktif değildir.
- Reason: AuthService JWT token içinde henüz signed `partner_id` + `allowed_tenant_ids` claim standardı üretmiyor. Partner admin token'ındaki `tenant_id` hedef müşteri tenant'ı değil, platform tenant id'dir; handler bunu tenant scope olarak kullanamaz.
- `partner_admin` için `AllowedTenantIds` kontrolü, MOD-0018-FU7 AuthService Partner Scope Claims tamamlandıktan sonra açılır.
- `actor_type=tenant_user` ise JWT `tenant_id` ile request payload `TenantId` eşleşmesi tasarımsal olarak (Secure-by-Design) sağlanır.
- Diten.Platform tenant_user endpoint'lerinde (ör. `SavedViewsController`) client'tan `TenantId` kabul edilmediği için mismatch kontrolü tasarım gereği gereksizdir.
- Authorization handler body/query/route verilerini okuyarak bir mismatch kontrolü yapmaz (body stream'in tükenmesini ve platform admin'lerinin yanlışlıkla engellenmesini önlemek için).

### TenantId payload'dan ASLA alınmaz
- `ITenantContext` üzerinden JWT claim'den çekilir.
- Request body / query string'deki `TenantId` alanı authorization veya entity resolution kararlarında client-supplied olarak kullanılamaz (sec-jwt.md kuralı).

### Cache stratejisi

**L1 (in-memory, her API process):**
- Key: `entitlement:module:{tenantId}:{moduleCode}` / `entitlement:feature:{tenantId}:{featureCode}`
- TTL: varsayılan 5 dakika, `appsettings.json` `Authorization:CacheTtlSeconds` ile konfigürable
- Implementation: `IMemoryCache` (Microsoft.Extensions.Caching.Memory)

**Invalidation (MOD-0035 hazır olduğunda):**
- `subscription.plan.changed` → tenant'ın tüm entitlement cache key'leri evict
- `entitlement.added` / `entitlement.disabled` / `entitlement.expired` → ilgili tek key evict
- `tenant.suspended` → tenant'ın tüm cache key'leri evict
- Consumer: `EntitlementCacheInvalidationConsumer` (idempotent, `IEventHandler<T>`)

**Event-less MVP fallback (MOD-0035 broker live değilse):**
- L1 cache TTL-only çalışır (5 dakika içinde plan değişikliği yansır)
- Cache invalidation event consumer kayıt olur ama broker bağlı olmadığı için inert kalır
- Production'a çıkmadan MOD-0035 live olmalı; aksi halde "plan değişti ama 5 dk hâlâ eski entitlement" davranışı kabul edilir

### Performance
- Cached check p99 < 5ms hedef
- Cache miss check p99 < 50ms (DB read)
- Batch check (`CheckBatchAsync`) tek DB roundtrip ile N entitlement çözer

### Audit
- Her deny edilen authorization denemesi MOD-0021 audit'e yazılır
- AuditEvent fields: `Outcome: AccessDenied`, `EntityType: ModuleAccess` / `FeatureAccess`, `Metadata: { moduleCode, featureCode, denyReason }`
- AuditEnums.cs'e `AuditOutcome.AccessDenied` değeri eklenmeli (varsa kontrol edilecek)

## 7. Authorization Convention

```text
Policy:     [Authorize(Policy = "PlatformActor")]   // mevcut, değişmez
Permission: [HasPermission("Modules.{ModuleCode}.{Action}")]   // mevcut, değişmez

YENİ (MOD-0018):
Module gate:      [RequiresModule("{MODULE_CODE}")]
Feature gate:     [RequiresFeature("{FEATURE_CODE}")]
Kombinasyon:      [HasPermission("Modules.HR.Read"), RequiresModule("HR")]
```

### Attribute imza şeması

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequiresModuleAttribute : AuthorizeAttribute
{
    public RequiresModuleAttribute(string moduleCode)
    {
        ModuleCode = moduleCode;
        Policy = $"RequiresModule:{moduleCode}";
    }
    public string ModuleCode { get; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequiresFeatureAttribute : AuthorizeAttribute
{
    public RequiresFeatureAttribute(string featureCode)
    {
        FeatureCode = featureCode;
        Policy = $"RequiresFeature:{featureCode}";
    }
    public string FeatureCode { get; }
}
```

### Policy registration (Program.cs)

Dynamic policy provider pattern: `IAuthorizationPolicyProvider` implementasyonu `RequiresModule:*` / `RequiresFeature:*` prefix'li policy isteklerini yakalar ve runtime'da `AuthorizationPolicy` üretir. Statik `AddPolicy(...)` her module için yazılmaz.

### Actor type davranışı

| Actor type | Module check | Feature check | Tenant scope |
|---|---|---|---|
| `platform_admin` | ✅ bypass | ✅ bypass | bypass (cross-tenant) |
| `partner_admin` | MVP'de fail-closed → 403 | MVP'de fail-closed → 403 | signed `allowed_tenant_ids` claim gelene kadar allow path yok; mümkünse `PartnerScopeViolation`, aksi halde mevcut fail-closed reason |
| `tenant_user` | tenant entitlement check | feature entitlement check | JWT tenant_id ≠ request TenantId → 403 `TenantIsolationViolation` |
| `null/anonymous` | 401 | 401 | 401 |

### Module-specific permission listesi (MOD-0018 owned)

```text
(MOD-0018 yeni permission tanımlamaz; mevcut [HasPermission] sistemi üzerine attribute ekler.)
```

## 8. Failure Path to Verify

Her senaryo için: HTTP status, audit log içeriği, deny reason.

- **Anonymous / token yok**
  - Expected: 401, audit yazılmaz, response `{ "error": "Unauthorized" }`

- **Token var, permission yok**
  - Expected: 403 (`[HasPermission]` denied), audit `Outcome=Denied, EntityType=Permission`, response `{ "error": "Forbidden", "denyReason": "MissingPermission" }`

- **Permission var, module entitlement YOK**
  - Expected: 403, audit `Outcome=AccessDenied, EntityType=ModuleAccess, Metadata.denyReason=ModuleNotEntitled`, response `{ "error": "Forbidden", "denyReason": "ModuleNotEntitled", "moduleCode": "HR" }`

- **Permission var, feature flag YOK (module var ama özellik kapalı)**
  - Expected: 403, audit `EntityType=FeatureAccess, Metadata.denyReason=FeatureNotEnabled`, response `{ "denyReason": "FeatureNotEnabled", "featureCode": "ADVANCED_REPORTING" }`

- **Expired entitlement (`ExpiresAtUtc < now`)**
  - Expected: 403, audit `Metadata.denyReason=EntitlementExpired, Metadata.expiresAtUtc=...`, cache evict tetiklenir

- **Disabled entitlement (manual override Disabled)**
  - Expected: 403, audit `Metadata.denyReason=EntitlementDisabled`

- **Platform admin → her şey**
  - Expected: 200, audit `Outcome=Success, Actor=PlatformAdmin, BypassReason=PlatformAdmin` (read action'larda da audit zorunlu değil — runtime karar)

- **Partner admin → allowed tenant claim henüz yok**
  - Expected: 403, audit varsa `Metadata.denyReason=PartnerScopeViolation` veya mevcut fail-closed reason. `AllowedTenantIds` signed claim gelene kadar partner_admin allow path aktif değildir.

- **Partner admin → not-allowed / missing scope**
  - Expected: 403, audit varsa `Metadata.denyReason=PartnerScopeViolation` veya mevcut fail-closed reason. Missing/malformed/empty `allowed_tenant_ids` ileride de deny sayılır.

- **Tenant A user → Tenant B kaynak (JWT/payload mismatch)**
  - *Diten.Platform current scope'unda bu senaryo production endpoint'lerinde üretilemez.* Çünkü `tenant_user` endpoint'lerinde client-controlled TenantId parametresi yoktur (Secure-by-Design).
  - Gerçek tenant-side ERP servisleri geldiğinde bu senaryo tekrar ele alınacaktır.

- **Cache stale (event bus down)**
  - Expected: TTL süresi (5 dk) içinde eski karar uygulanır; TTL bitince DB fresh read; davranış kabul edilir (event-less MVP fallback)

- **DB unreachable + cache miss**
  - Expected: 503 Service Unavailable, audit `Outcome=Error, Metadata.error=...`; 200 ASLA dönmez (fail-closed)

- **Gateway smoke (curl :5000)**
  - Expected: mümkünse test-only `AuthorizationProbeController` veya uygun test-only route üzerinden non-entitled tenant ile 403; entitled tenant ile 200. Production route'a fake gate eklenmez.

## 9. Acceptance Criteria

1. `RequiresModuleAttribute` ve `RequiresFeatureAttribute` `services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/` altında implement edilir; `AuthorizeAttribute`'tan miras alır ve dynamic policy ismi üretir.

2. `IEntitlementChecker` contract'ı şu imzayı taşır:
   ```csharp
   Task<EntitlementCheckResult> IsModuleEntitledAsync(Guid tenantId, string moduleCode, CancellationToken ct);
   Task<EntitlementCheckResult> IsFeatureEnabledAsync(Guid tenantId, string featureCode, CancellationToken ct);
   Task<IReadOnlyList<EntitlementCheckResult>> CheckBatchAsync(Guid tenantId, IEnumerable<(string code, EntitlementKind kind)> targets, CancellationToken ct);
   ```
   `EntitlementCheckResult` `{ bool IsAllowed, EntitlementDenyReason? DenyReason, DateTimeOffset? ExpiresAtUtc }` taşır.

3. `Program.cs`'de dynamic `IAuthorizationPolicyProvider` kayıtlıdır; `RequiresModule:*` ve `RequiresFeature:*` prefix'li policy istekleri çözülür.

4. `TenantModuleAuthorizationHandler` (mevcut scaffold) ve `TenantFeatureAuthorizationHandler` (yeni):
   - `platform_admin` actor'ü her zaman pass
   - `partner_admin` current release'de fail-closed kalır; `AllowedTenantIds` allow check MOD-0018-FU7 sonrasına taşınır
   - `tenant_user` için Diten.Platform kapsamında client-controlled TenantId yokluğu (Secure-by-Design) doğrulanmıştır, handler seviyesinde ekstra bir body/query/route mismatch kontrolü kodlanmayacaktır (Future ERP tenant-side services için follow-up'a taşınmıştır).
   - Deny durumlarında `EntitlementDenyReason` ile beraber `AuthorizationFailureReason` kayıtlanır

5. `EntitlementCacheService` `IMemoryCache` üzerine kurulur; TTL `appsettings.json` `Authorization:CacheTtlSeconds` (default 300) ile konfigürable.

6. MOD-0035 broker live olduğunda `EntitlementCacheInvalidationConsumer` `subscription.plan.changed`, `entitlement.added`, `entitlement.disabled`, `entitlement.expired`, `tenant.suspended` event'lerine idempotent subscribe eder. Broker yoksa consumer kayıt olur ama inert kalır (test edilebilir).

7. MOD-0018 current platform scope içinde gerçek tenant-facing business controller bulunmadığı için, §8 Failure Path'teki 12 senaryo test-only `AuthorizationProbeController` üzerinden doğrulanır. Probe yalnızca integration test/dev-test ortamında kullanılır; production business controller'ları kirletmez. Production ortamında probe business response dönmez; MVC authorization filter action guard'dan önce çalışabildiği için anonymous production isteği 404 yerine 401, unauthorized authenticated istek 403 dönebilir. Probe endpoint'leri `[RequiresModule("HR")]` ve `[RequiresFeature("ADVANCED_REPORTING")]` gibi deterministic test code'larıyla çalışır. `TenantsController` PlatformActor ağırlıklı olduğu ve platform_admin bypass nedeniyle gerçek tenant_user denied senaryosu üretmediği için sample değildir; `TenantModuleEntitlementsController` entitlement yönetiminin kendisi olduğu için circular dependency riski taşır; `ModuleCatalogController` global sistem kataloğudur ve tenant'a satılan business module gibi davranmaz. `SavedViewsController` yalnızca `CorePlatform` gibi gerçek ve katalogda mevcut bir core module code doğrulanırsa opsiyonel cross-cutting sample olarak kullanılabilir. Gerçek tenant-side ERP controller'lar geldiğinde `[RequiresModule]` / `[RequiresFeature]` production endpoint'lerine follow-up olarak uygulanır.

8. **Deny edilen access** MOD-0021 audit trail'e yazılır:
   - `AuditEvent.Outcome = AccessDenied`
   - `AuditEvent.EntityType = "ModuleAccess"` veya `"FeatureAccess"`
   - `AuditEvent.Metadata` deny reason + module/feature code + tenant ID

9. **Performance:** cached check p99 < 5ms (BenchmarkDotNet veya basit load test ile doğrulanır).

10. **Integration test coverage:** §8 Failure Path'teki **12 senaryonun her biri** için en az 1 xUnit testi:
    - `dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/` PASS
    - WebApplicationFactory tabanlı HTTP integration test (mümkünse PSS-011 ile aynı infra'yı kullanır)

11. **MOD-0008 alt görev:** `IPlatformCatalogContract.GetAssignableModulesAsync()` interface'i `services/Diten.Platform.Common/Catalog/` altında oluşturulur; mevcut 3 ad-hoc query (`GetTenantAvailableModulesForAssignmentQuery`, `GetModuleAssignmentOverviewQuery`, `GetTenantVisibleModulesQuery`) bu interface'i implement eder / consume eder. PR description'ında "MOD-0008 alt görev burada stabilize edildi" notu zorunlu.

12. **Gateway smoke**: `curl :5000` ile mümkünse `AuthorizationProbeController` veya uygun test-only route üzerinden non-entitled tenant token'ı ile istek atıldığında 403, entitled token ile 200 döner (manuel doğrulama dökümante edilir). Production route'a fake gate eklenmez ve Gateway route zorunluluğu doğmaz.

13. **Documentation update:** `docs/platform/master-plan.md` §9.1'de MOD-0018 status `🟡 20` → `🟢 90+` revize edilir; §3.21 detay bloğu güncel yüzde + reconciliation notu ile imzalanır.

## 10. Test Expectations

### Unit tests
- `EntitlementCheckResult` constructor / deny reason serialization
- `RequiresModuleAttribute` / `RequiresFeatureAttribute` policy string üretimi
- `IAuthorizationPolicyProvider` dynamic policy çözümü (prefix matching)
- Cache TTL davranışı (memory cache mock)
- Platform admin bypass (handler unit test)

### Integration tests (WebApplicationFactory)
- 12 failure path senaryosu (§8'den)
- Test-only `AuthorizationProbeController` üzerinde `[RequiresModule]` / `[RequiresFeature]` end-to-end
- Cache hit/miss DB roundtrip sayımı
- Batch check tek roundtrip doğrulaması

### Build & verifier
- `dotnet build services/Diten.Platform.Common/src/Diten.Platform.Common/Diten.Platform.Common.csproj` PASS
- `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj` PASS
- `dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/` PASS (yeni klasör)

### Smoke
- Gateway smoke: mümkünse `curl :5000/api/...` test-only probe route × {entitled, non-entitled, expired} senaryo

## 11. Gateway / API Routing Decision

```text
Karar: Gateway değişikliği GEREKSİZ.

- Bu pack production endpoint açmaz; test-only `AuthorizationProbeController` yalnız integration test/dev-test ortamında kullanılabilir.
- `ocelot.json` zaten Platform service'in tüm route'larını forward ediyor.
- Token passthrough (Bearer) mevcut Gateway davranışıyla uyumlu.
- Probe üzerinden smoke yapılabiliyorsa mevcut Gateway davranışı kullanılır; yeni Gateway route zorunluluğu doğmaz.
- `gateway/Diten.ApiGateway/**/ocelot.json` protected; bu pack doğrudan yazmaz.
```

## 12. Implementation Notes

### Master-plan saplamaları
- Master-plan `docs/platform/master-plan.md` §3.21 (MOD-0018) bu pack ile aynı kontratı taşır; sapma olursa master-plan güncellenir (pack truth, master-plan reconciliation).
- §9.1 status tablosu MOD-0018 satırı geliştirme bitiminde revize edilir.

### Contract referansları
- `IEntitlementChecker` Diten.Platform.Common'da yaşar (cross-service tüketilir).
- `EntitlementDenyReason` enum: `MissingPermission`, `ModuleNotEntitled`, `FeatureNotEnabled`, `EntitlementExpired`, `EntitlementDisabled`, `PartnerScopeViolation`, `TenantIsolationViolation`.
- `EntitlementKind` enum: `Module`, `Feature`.

### MOD-0035 cache invalidation event'lerine bağımlılık
- Event consumer registration MOD-0018 işidir; event publish MOD-0009 ve MOD-0298 işi.
- Consumer **idempotent** olmalı (aynı event tekrar gelirse cache evict idempotent).
- Broker live olmadığında consumer no-op davranır (TTL-only fallback).

### Platform admin bypass — neden?
- Platform admin tüm tenant'lara erişebilir (tenant management, audit, vs.). `[RequiresModule]` her endpoint'e konulduğunda platform admin'i de bloklarsa platform admin hiçbir tenant detail'a giremez. Bu yüzden bypass kuralı zorunlu.

### NEW-002 audit retrofit ilişkisi
- NEW-002-FU1 (admin command'lara `IAuditableCommand` retrofit) bu pack'ten bağımsız ama paralel yapılabilir.
- MOD-0018 deny audit'i NEW-002 retrofit'inden ayrı; aynı `AuditService.RecordAsync(...)` API'sini kullanır.

### Naming
- Attribute: `RequiresModuleAttribute` (mevcut `TenantModuleRequirement` ile tutarlı isim)
- Handler: `TenantModuleAuthorizationHandler` (mevcut, genişler) + `TenantFeatureAuthorizationHandler` (yeni)
- Service: `IEntitlementChecker` (Common library, public)
- Cache: `EntitlementCacheService` (private, Application katmanı)

## 13. Ready-for-dev Checklist

- [x] Frontmatter tüm zorunlu alanlar dolu (id, name, domain, service, shell, golden_reference, entity_base, status, owner, branch, started, target, form_field_count)
- [x] Scope drift notu yazılı (Diten.Platform.Common merkezli)
- [x] UI scope kararı yazılı (enforcement-only, no UI)
- [x] Master-plan §3.21 referansı yazılı
- [x] Owned Objects MOD-0018'e ait gerçek obje listesi (Roles/Permissions değil)
- [x] Repo Scope backend klasörleri net (yeni dosyalar + genişleyen dosyalar)
- [x] Protected Paths AuthService dahil
- [x] Dependencies mevcut + conditional ayrımı net
- [x] Runtime Constraints cache stratejisi + event-less fallback + performance hedefi
- [x] Authorization Convention attribute imzaları + policy registration + actor type davranışı
- [x] Failure Path 12 senaryo (anonymous, missing permission, missing entitlement, expired, disabled, platform admin bypass, partner admin fail-closed, tenant isolation, cache stale, DB unreachable, gateway smoke)
- [x] Acceptance Criteria 13 madde, test edilebilir (somut endpoint/response/log)
- [x] Test Expectations unit + integration + build + smoke kapsıyor
- [x] Gateway routing decision yazılı (gereksiz)
- [x] Implementation Notes contract imzaları + enum tanımları + naming
- [x] Sample controller seçimi onaylandı: primary test surface test-only `AuthorizationProbeController`; production controller'lara fake `[RequiresModule]` eklenmez
- [x] `appsettings.json` `Authorization:CacheTtlSeconds` default değeri ekibe onaylatıldı: 300 saniye kabul edildi
- [x] MOD-0035 broker live beklenmeyecek; MOD-0018 TTL-only fallback ile ilerleyecek. Consumer yazılabilir ama broker yoksa inert/no-op kalır. Production öncesi live broker doğrulaması Dev1 sorumluluğunda takip edilir.

## 14. Follow-up Items

- **MOD-0018-FU1:** RBAC Admin UI ayrı pack (PSS-012 önerisi) — AuthService Roles/Permissions CRUD üzerine compact DataTable shell.
- **MOD-0018-FU2:** ABAC condition language genişletme (resource tag bazlı policy) — sonraki maturity wave.
- **MOD-0018-FU3:** Policy Tester UI (kullanıcı/aksiyon/kaynak simülasyonu) — opsiyonel, müşteri talebi gelirse.
- **MOD-0018-FU4:** Delegation modeling (kullanıcı A, kullanıcı B adına işlem yapabilir) — gelecek wave.
- **MOD-0018-FU5:** Multi-region cache (L2 distributed cache, Redis) — production ölçeği gerektirirse.
- **MOD-0018-FU6:** İlk gerçek tenant-side ERP controller'lar geldiğinde `[RequiresModule]` / `[RequiresFeature]` production endpoint'lerine uygulanacak ve en az 3 gerçek business controller ile smoke test yapılacak.
- **MOD-0018-FU7:** AuthService Partner Scope Claims — AuthService token üretimi ve refresh akışı signed `partner_id` + `allowed_tenant_ids` claim üretmeli. Claim formatı: `partner_id` GUID string; `allowed_tenant_ids` JSON array of GUID strings. Missing/malformed/empty `allowed_tenant_ids` → deny. Bu claim contract geldikten sonra MOD-0018 handler `partner_admin` için allowed scope check implement eder; claim gelene kadar `partner_admin` fail-closed kalır.
- **MOD-0018-FU8:** Tenant-side ERP Services İçin TenantId Mismatch Standardı — Eğer gelecekte yazılacak tenant-side ERP servislerinde (MDM, ESBP vb.) tenant_user endpoint'leri dışarıdan route/query/body TenantId kabul edecek olursa, JWT tenant_id ile uyuşmazlık kontrolü yapılması zorunlu olacaktır. Bu kontrol body okuma stream riski nedeniyle authorization handler yerine generic filter, DTO validation veya middleware standardı ile ayrıca tanımlanacaktır.

---

## Karar Logu

- **2026-05-18:** Pack ilk hali (status alanı yok, 10 section minimal içerik) yeniden yazıldı. 13. Ready-for-dev Checklist eklendi. UI scope çelişkisi çözüldü (enforcement-only). Owned Objects standardize edildi (Roles/Permissions çıkarıldı). Authorization Convention + Failure Path + Implementation Notes contract imzaları eklendi. Cache strategy + event-less MVP fallback yazıldı. Master-plan §3.21 ile birebir uyumlu.
- **2026-05-18:** Partner admin `AllowedTenantIds` keşfi sonrası MVP kararı güncellendi: AuthService signed `partner_id` + `allowed_tenant_ids` claim üretmediği için `partner_admin` allow path bu release'de aktif değildir; fail-closed davranış korunur ve claim işi MOD-0018-FU7 follow-up'ına taşındı.
- **2026-05-18:** Platform tenant mismatch keşfi yapıldı ve Diten.Platform tenant_user endpoint'lerinin (ör. SavedViewsController) tasarımsal olarak client'tan TenantId parametresi almadığı doğrulandı. Bu nedenle mismatch kontrolü Secure-by-Design olarak kapatıldı; body stream tükenme riski nedeniyle handler'a body parsing kodlanmayacak, ilerideki tenant-side ERP servisleri için MOD-0018-FU8 follow-up standardı tanımlandı.
