---
file_name: CAND-CAP-0002-FU05-tenant-module-entitlements.md
id: CAND-CAP-0002-FU05
name: Tenant Module Entitlements
domain: platform-shared-services
status: ready-for-dev
owner: platform-team
branch: feature/pss/mod-0298-tenant-module-entitlements
created_at: 2026-05-11
golden_reference: slim
production_authority: none
execution_activation: none
---

# CAND-CAP-0002-FU05 — Tenant Module Entitlements

> **Canonicalization (DCP-002):** Governance identity is now **CAND-CAP-0002-FU05**, a child of **CAND-CAP-0002 (SaaS Subscription, Plan & Entitlement Management)**. Prior repo ID **MOD-0298** is a deprecated alias. Temporary candidate identity pending EA MOD-xxxx; never written into runtime literals. Ref: `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`.
> **Ledger preflight:** Parent `CAND-CAP-0002`, child `CAND-CAP-0002-FU05`, name
> `Tenant Module Entitlements`, deprecated alias `MOD-0298`, owner `platform-shared-services`, and
> governance-only/pending-EA/no-runtime-literal constraints are reconciled in the canonical ledger.

### PPM audit transport dependency lock

The separately owned transport uses **5 total attempts**: 10 seconds after the first failure, then
exponential backoff with jitter up to 5 minutes; the fifth failed attempt causes DLQ plus alarm. The initial
attempt is included, leaving four retry attempts. Shared `EventEnvelope`, `IEventBus`, outbox and inbox mechanics belong to
`Diten.BuildingBlocks.Eventing`. MOD-0117 owns the logical PPM event at the planned
`services/Diten.PpmService/src/Diten.PpmService.Contracts/Events/**` path; Platform is consumer-only and
`Diten.Platform.Contracts` does not own this PPM event.

The final **Minimal Mutation Audit v1** payload contains exactly `auditIntentId`, `actorId`, `entityType`,
`entityId`, `mutation` and `occurredAtUtc`, and evidences only actor, minimal mutation, PPM aggregate and
time. Authorized replay preserves the same `EventId` and identical canonical bytes; changed bytes are
rejected. A first delivery not accepted may yield exactly one `AuditEvent`; an accepted delivery yields
none on replay. Idempotency is `ConsumerName + EventId`. Unauthorized replay is forbidden; no replay UI/API
is authorized.
Bu module pack, tenant bazlı modül yetkilendirme (Module Entitlement) yönetiminin tasarım, kural ve entegrasyon sınırlarını belirler. Amacı, tenant'ların sistemdeki modüllere (ör: MDM, İK, CRM) erişim haklarını tutmak, plan kaynaklı haklarla manuel eklentileri (override/addon) birleştirerek gerçek zamanlı bir "Effective Access" (Geçerli Erişim) kararı üretmek ve bu kararı arka uç (backend) yetkilendirme altyapısı ile arayüz görünümlerine entegre etmektir. 

# 2. Business Objective
Tenant Details ekranında `Commercial` tabı altında mevcut `Plan / Subscription` tabının yanına `Module Entitlements` adında yeni bir alt sekme eklenmesi planlanmaktadır. Bu sekme, yalnızca görsel bir liste olmaktan öte, tenant-modül ilişkisinin "RBAC öncesi ilk güvenlik kapısı" (tenant-level gate) olarak işlev gören bir erişim yönetimi (access management) ekranı olacaktır. 

# 3. Current Context
Mevcut sistemde bir tenant subscription ve subscription plan yapısı bulunmaktadır. 
**Karar:**
- `SubscriptionPlan`, default module haklarının ana kaynağıdır.
- Plan kaynaklı module hakları `Module Entitlements` tabında projection/read-model (sanal kayıtlar) olarak gösterilir.
- Plan kaynaklı haklar doğrudan silinmez ve plan kaydı tenant entitlement kaydı gibi mutate edilmez.
- `TenantModuleEntitlement` fiziksel kayıtları tenant özelindeki `ManualOverride`, `Addon`, `Trial` ve gerekirse `System` kararlarını tutar.
- **Effective Access**, plan projection ile tenant-specific fiziksel entitlement kayıtlarının birleştirilmesiyle hesaplanır. Runtime erişim kararı da bu effective access sonucuna göre verilir.

# 4. Target Page and UI Placement
**URL:** `http://localhost:5001/Platform/Tenants/Details/{id}`
**Navigasyon:** `Platform > Tenants > Details > Commercial tab`
Mevcut `Plan / Subscription` tabının yanına `Module Entitlements` tabı eklenecektir. Tüm yönetim ve DataTable (Slim formatında) bu sekme içerisinde gerçekleştirilecektir.

# 5. Domain Model

## Entity: `TenantModuleEntitlement`
Tenant'ın fiziksel olarak atanmış manuel override, addon, trial durumlarını tutar. **Plan hakları entity olarak tutulmaz, projection olarak hesaplanır.**

**Minimum Alanlar:**
- `Id` (Guid)
- `TenantId` (Guid)
- `ModuleCode` (string)
- **Source:** (Physical enum values only: `ManualOverride`, `Addon`, `Trial`, `System`)
- `IsEnabled` (bool)
- `ExpiryDateUtc` (DateTime? - nullable)
- `Reason` (string? - nullable, disable veya override işlemlerinde zorunlu)
- `CreatedAtUtc` / `CreatedBy` (Audit)
- `UpdatedAtUtc` / `UpdatedBy` (Audit)
- `RowVersion` (Concurrency token / byte[])

**Not:** `Plan` source, fiziksel entity'de tutulmaz. Plan source sadece read-model/projection response içinde gösterilir.

## DTO / Read Model: `TenantModuleEntitlementRowDto` (veya `TenantModuleEffectiveAccessDto`)
UI tablosuna dönülecek birleştirilmiş (projection + physical) liste verisi için öneri:
- `TenantId` (Guid)
- `ModuleCode` (string)
- `ModuleName` (string)
- `DisplaySource` (Enum/String: `Plan`, `ManualOverride`, `Addon`, `Trial`, `System`)
- `PhysicalEntitlementId` (Guid? - nullable. Sadece fiziksel kayıt ise dolu, projection ise null)
- `IsEnabled` (bool)
- `ExpiryDateUtc` (DateTime? - nullable)
- `EffectiveAccess` (Enum/String)
- `Reason` (string?)
- `IsProjectionRow` (bool)
- `HasManualOverride` (bool)
- `LastUpdatedAtUtc` (DateTime? - nullable)

# 6. Effective Access Rules
Aynı tenant ve module kombinasyonu için birden fazla source (kaynak) olabilir (örn. Plandan gelebilir ve üzerine ManualOverride eklenebilir). Ancak **aynı source ve aynı active period içinde çakışan iki fiziksel tenant entitlement kaydı olamaz.**
Effective decision tek olmalıdır. 

**Effective decision precedence:**
1. **System lock / non-disableable core rule:** (System source core modüller asla disable edilemez kuralı)
2. **ManualOverride:** (Physical entitlement)
3. **Addon:** (Physical entitlement)
4. **Trial:** (Physical entitlement)
5. **Plan:** (Projection)
6. **NoAccess:** (Yukarıdaki hiçbir kural erişim vermiyorsa veya ExpiryDateUtc geçmişse erişim yoktur.)

*Not: Plan projection fiziksel TenantModuleEntitlement kaydı değildir. Effective access hesaplamasına SubscriptionPlan üzerinden dahil edilir. ExpiryDateUtc geçmiş olan Addon/Trial/ManualOverride effective decision’a dahil edilmez.*

### Effective Access Örnekleri
- **Scenario A:** Plan CRM verir, tenant override yoktur -> `CRM Active / Source Plan`.
- **Scenario B:** Plan FINANCE verir, ManualOverride disabled vardır -> `FINANCE Blocked by Override`.
- **Scenario C:** Plan INVENTORY vermez, Addon enabled vardır -> `INVENTORY Active by Addon`.
- **Scenario D:** Trial enabled vardır ama ExpiryDateUtc geçmiş -> `Expired / No Access`.
- **Scenario E:** RBAC permission vardır ama tenant module effective access blocked -> Backend 403 döner.

# 7. Plan Sync and Manual Override Behavior
- **Default Plan:** Tenant'ın aktif bir planı varsa, bu plandan gelen haklar listeye projection/read-model olarak (`DisplaySource = Plan`) yansıtılır. UI tablosunda Source=Plan görünebilir ama bu satırın fiziksel TenantModuleEntitlement Id değeri olmak zorunda değildir. Bu satırlar `TenantId + ModuleCode + Source=Plan` action key ile temsil edilir.
- **Priority:** `ManualOverride`, plan kaynaklı kurallar üzerinde mutlak önceliğe sahiptir.
- **Delete Constraint:** Plan kaynaklı haklar (`Source = Plan`) doğrudan silinmez (DELETE edilmez). Plandan gelen modül kapatılacaksa, tenant için o modülü `IsEnabled = false` yapan bir fiziksel `ManualOverride` kaydı oluşturulur.
- **Plan Değişikliği (Upgrade/Downgrade):** 
  - **Karar:** Default davranış query-time projection’dır. Plan değiştiğinde effective access query yeni planı anlık yansıtır.
  - Eğer projection cache veya read-model kullanılıyorsa plan değişikliği event’i cache invalidation veya refresh tetikler.
  - Bu işlem destructive değildir. Plan değişikliği hiçbir fiziksel tenant entitlement kaydını silent delete yapmaz.
  - `ManualOverride`, `Addon` ve `Trial` fiziksel kayıtları korunur.
  - Plan downgrade sonrası `Addon` veya `ManualOverride` enabled varsa, ilgili modül açık kalabilir.
  - Bu davranış auditlenmelidir. 
  - Süresi dolmuş (expired) kayıtlar access vermez ama audit/history takibi için saklanır.

# 8. UI Requirements
`Module Entitlements` tabı içerisindeki veri tablosu aşağıdaki kolonları içermelidir:
- **Module Code:** Örn. CRM, FINANCE, INVENTORY, HR
- **Module Name:** Modül adı
- **Source:** Plan, Addon, ManualOverride vb. (DisplaySource)
- **Status:** Active, Blocked, Expired, Blocked by Override, Enabled by Override
- **Enabled:** Check veya Cross ikonu.
- **Expiry Date:** Bitiş tarihi.
- **Effective Access:** Geçerli durum sonucu.
- **Reason:** Açıklama / Neden.
- **Last Updated:** Son güncellenme tarihi.
- **Actions:** Add Module Entitlement, Enable, Disable, Edit Expiry, Edit Reason, Remove manual override, View audit/history (varsa).

Sayfa düzeyinde eylem:
- **Add Module Entitlement:** Modal/Offcanvas tetikler (Alanlar: `Module selector`, `Source` [default: Addon/ManualOverride], `Enabled` [true/false], `Expiry Date`, `Reason`).

UI Görsel Kurallar:
- Bootstrap ve DataTables v2 (Slim) standartlarına uyulacaktır.
- Sadece backing API contract'ı hazır olan action'lar buton olarak aktif olacaktır. Karşılığı olmayan action'lar render edilmeyecek ya da disabled + tooltip ile açıklanacaktır. Mock data ile operational UI yapılmaz.
- Plan'dan gelen ama override edilen kayıtlar için UI'da "Plan’dan geliyor, tenant özelinde override uygulandı" ayrımı net gösterilecektir.

# 9. Backend Contracts
Aşağıdaki kontratlar eksiksiz olarak oluşturulmalı ve Controller seviyesine bağlanmalıdır.

**Queries:**
- `GetTenantModuleEntitlementsQuery` (Plan projection'ı ile fiziksel Override kayıtlarını birleştirip DTO döner)
- `GetTenantModuleEffectiveAccessQuery` (Tekil effective access kararı döner)
- `GetTenantVisibleModulesQuery` (Menü/Sidebar görünürlüğü için salt okuma/projection döner)
- `GetTenantAvailableModulesForAssignmentQuery` (Manuel atama için boşta modülleri döner)

**Commands:**
- `AddTenantModuleEntitlementCommand`
- `EnableTenantModuleEntitlementCommand`
- `DisableTenantModuleEntitlementCommand` (Projection satır için çağrıldığında ManualOverride yaratır)
- `UpdateTenantModuleEntitlementExpiryCommand`
- `RemoveTenantManualModuleOverrideCommand`
- `RefreshTenantModuleEntitlementProjectionCommand` (Yalnızca cache/read-model kullanılıyorsa invalidation veya refresh amaçlıdır. Fiziksel Plan entitlement kaydı oluşturmaz.)

**Service Layer:**
- `ITenantModuleAccessService`
  - `Task<bool> HasAccessAsync(Guid tenantId, string moduleCode)`
  - `Task<EntitlementStatus> GetEffectiveAccessAsync(Guid tenantId, string moduleCode)`
  - `Task EnsureAccessOrThrowAsync(Guid tenantId, string moduleCode)`

# 10. RBAC and Runtime Enforcement
`TenantModuleEntitlement`, RBAC öncesi bir **tenant-level gate**'dir. Sadece menüde gizlemek güvenlik için yeterli kabul edilemez, backend API seviyesinde doğrudan enforcement zorunludur.

### PPM-specific entitlement strategy

- Canonical module identity is exactly `ModuleCode = PPM`.
- The Phase 2A PPM permission catalog contract is the following exact closed set:
  `ppm.portfolios.read`, `ppm.portfolios.create`, `ppm.portfolios.update`,
  `ppm.portfolios.change-lifecycle`, `ppm.initiatives.read`, `ppm.initiatives.create`,
  `ppm.initiatives.update`, `ppm.initiatives.change-lifecycle`, `ppm.programs.read`,
  `ppm.programs.create`, `ppm.programs.update`, `ppm.programs.change-lifecycle`,
  `ppm.projects.read`, `ppm.projects.create`, `ppm.projects.update`, and
  `ppm.projects.change-lifecycle`.
- Wildcards, aliases and additional keys are forbidden in this PSS-A slice.
  `ppm.portfolios.archive` is non-canonical and remains a PPM-branch reconciliation blocker; no PSS
  runtime alias may be created. Phase 2B investment/benefit permissions and external-context validation
  permissions are excluded.
- Entitlement enablement creates no `ppm.*` permission grant. Permission assignment is a separate,
  tenant-scoped, explicit and auditable AuthService administration operation.
- Entitlement removal denies immediately at the entitlement gate and does not delete explicit
  `RolePermission` rows. Existing grants become dormant.
- Re-entitlement makes only still-existing grants held by current role memberships effective again; deleted
  grants are never reconstructed and no new grant is generated. The re-entitlement operation is audited and
  exposes a read-only current grant/role inventory to the authorized administrator.
- The current generic Admin/Viewer auto-grant and destructive module-revoke bridge remains unchanged for MDM
  and other existing modules; it cannot process `PPM`.
- Registration of the 16 catalog entries proves availability only: it creates no grant, does not modify
  `DataSeeder` or the FU9 default-role template, and grants neither Admin nor Viewer implicit PPM access.

**Runtime kontrol sırası:**
1. Tenant active check
2. Tenant module effective access check
3. Module catalog active check
4. RBAC permission check
5. Business action execution

**Enforcement Uygulamaları:**
- **API Endpoint:** `TenantModuleRequirement` / `AuthorizationHandler` standardı ile korunmalıdır.
- **MVC/Razor Pages:** Tenant module authorization policy veya Controller filter önerilir.
- **MediatR:** Command/Query için `TenantModuleAccessBehavior` veya handler seviyesinde `EnsureAccessOrThrowAsync` çağrısı zorunludur.
- **Menü/Sidebar Visibility:** Diten.Web tarafında `GetTenantVisibleModulesQuery` kullanılarak sadece `Effective Access` kararı aktif olan modüller gösterilmelidir.

# 11. Data Validation Rules
- Duplicate kontrolü sadece **fiziksel** `TenantModuleEntitlement` kayıtları için geçerlidir.
- Plan projection satırı duplicate conflict sayılmaz.
- Aynı tenant + module + source + overlapping active period için birden fazla fiziksel kayıt engellenmelidir.
- `ManualOverride` disabled kaydı, Plan projection ile conflict sayılmaz; plan kararını override eder.
- Disable veya Override işlemlerinde `Reason` alanı kesinlikle zorunludur.
- Projection satır üzerinde Enable/Disable işlemi yapıldığında backend action `TenantId` + `ModuleCode` ile çalışır.
- Concurrency token kontrolü yapılarak aynı kayda eşzamanlı değişiklik yapılması durumunda `ConcurrencyException` fırlatılmalıdır.
- System source core modüller (ör. Identity Core) disable edilemez kuralı sistem taraflı açıkça korunmalıdır.

# 12. Audit and Observability
Aşağıdaki olaylar sistem audit altyapısına kaydedilmeli ve gerekirse integration event olarak (EventBus) fırlatılmalıdır:
- Manual entitlement added (`tenant.module_entitlement.added`)
- Entitlement enabled (`tenant.module_entitlement.updated` / `enabled`)
- Entitlement disabled (`tenant.module_entitlement.disabled`)
- Expiry changed (`tenant.module_entitlement.updated`)
- Override removed (`tenant.module_entitlement.updated`)
- Plan projection cache refreshed (`tenant.module_entitlement.projection_refreshed`)
- Access denied because module blocked/expired (Güvenlik / Auth audit logları)

# 13. Golden Flow
1. Platform Admin, Tenant Detail sayfasını açar.
2. `Commercial` tabına girer, `Module Entitlements` sekmesine geçer.
3. Plan kaynaklı mevcut modülleri listede `Active` (Source: Plan) olarak projection satırları şeklinde görür.
4. `Add Module Entitlement` butonuna basar, plan dışı yeni bir modül (Addon) seçer, opsiyonel olarak bir `Expiry Date` belirler ve kaydeder.
5. Tab reload edildiğinde tablo güncellenir; eklenen entitlement `Active` (Source: Addon) olarak görünür.
6. İlgili modül, Runtime Access karar noktasında enabled kabul edilir ve menüde / API'de kullanılabilir hale gelir.

# 14. Failure Paths
- **Failure Path 1 (Conflict):** Platform Admin aynı tenant + module için aynı kaynakla çakışan bir aktif fiziksel entitlement (örneğin aynı Addon) eklemeye çalışırsa sistem kayıt oluşturmayı reddeder, UI'a kontrollü bir validation error mesajı döner.
- **Failure Path 2 (Enforcement):** Tenant için modül `disabled` veya `expired` durumundaysa, bir kullanıcı direkt URL'yi veya API'yi çağırmaya çalıştığında backend pipeline'ı devreye girerek `403 Forbidden / Error` döner ve erişimi reddeder. RBAC yetkisi olsa bile bu engelleme gerçekleşir.

# 15. Acceptance Criteria
**Runtime criteria:**
- Given tenant has active plan with CRM module, When Platform Admin opens Module Entitlements tab, Then CRM appears with Source=Plan and EffectiveAccess=Active.
- Given tenant has plan FINANCE and ManualOverride disabled, When runtime access decision is evaluated, Then FINANCE is denied even if RBAC permission exists.
- Given tenant has Addon INVENTORY with future expiry, When Module Entitlements tab loads, Then INVENTORY appears as Source=Addon and EffectiveAccess=Active.
- Given tenant has expired Trial HR, When user tries to open HR API, Then backend returns 403 and audit event is written.

**Integrity criteria:**
- Given Platform Admin adds duplicate Addon for same tenant/module/source/active period, When saving, Then validation blocks save and no duplicate record is persisted.
- Manual override, plan source’dan üstün kabul edilir ve Effective Decision buna göre verilir.
- Plan değişikliği yapıldığında projection yeniden hesaplanır, fiziksel override'lar korunur.

**UX criteria:**
- DataTables (v2) empty, loading ve error state'leri başarılı şekilde çalışır.
- Validation mesajları kullanıcı dostu ve kontrollüdür.
- Teknik token/guid değerleri son kullanıcıya gösterilmez. Butonlar yalnızca backend contract'ı varsa aktif render edilir.

# 16. Repo Scope / Target Files
Bu özellik geliştirilirken dokunulacak ve dokunulmaması gereken öncelikli hedefler:

**Backend (Platform Service):**
- `services/Diten.Platform/src/Diten.Platform.Domain/Entities/Tenants/TenantModuleEntitlement.cs`
- `services/Diten.Platform/src/Diten.Platform.Domain/Enums/EntitlementSource.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Configurations/Tenants/TenantModuleEntitlementConfiguration.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commands/Entitlements/...`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Queries/Entitlements/...`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Behaviors/TenantModuleAccessBehavior.cs` (MediatR Pipeline)
- `services/Diten.Platform/src/Diten.Platform.Application/Models/DTOs/TenantModuleEntitlementRowDto.cs` (Read model / DTO)
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/TenantModuleEntitlementsController.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Services/ITenantModuleAccessService.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Features/Tenants/Entitlements/...` (Tests folder)

**Common/Gateway/Shared:**
- `services/Diten.Platform.Common/Authorization/TenantModuleRequirement.cs` (Authorization Handler)
- `services/Diten.Platform.Common/Authorization/TenantModuleAuthorizationHandler.cs` (Handler implementation)

**Frontend (Diten.Web):**
- `frontend/Diten.Web/Views/Platform/Tenants/Commercial/_ModuleEntitlementsTab.cshtml`
- `frontend/Diten.Web/Views/Platform/Tenants/Commercial/_AddModuleEntitlementOffcanvas.cshtml`
- `frontend/Diten.Web/Models/Platform/Tenants/TenantModuleEntitlementViewModel.cs` (View Model)
- `frontend/Diten.Web/wwwroot/assets/js/Platform/Tenants/module-entitlements.js` (DataTable ve Offcanvas dosyası)
- `frontend/Diten.Web/Controllers/Platform/TenantsController.cs` (Kısmi view render için endpoint'ler)

# 17. Test Plan
**Unit Tests:**
- Plan projection physical entity olarak persist edilmez.
- Physical source enum Plan içermez veya Plan persist edilmez.
- Effective access Plan projection + physical override ile doğru hesaplanır (System > ManualOverride > Addon > Trial > Plan).
- ManualOverride disabled, Plan projection active olsa bile erişimi kapatır.
- Addon, Plan projection yokken erişim verir.
- Expired Trial erişimi engelliyor mu.
- System source module'ün disable edilmesine izin verilmiyor mu.
- Duplicate control only applies to physical records.

**API Tests:**
- `GetTenantModuleEntitlementsQuery` returns Plan projection rows with `PhysicalEntitlementId=null`.
- Disable action on Plan projection row creates `ManualOverride` disabled physical record.
- `RemoveTenantManualModuleOverrideCommand` restores Plan projection effective access.
- `RefreshTenantModuleEntitlementProjectionCommand`, eğer varsa, physical Plan entitlement oluşturmaz.
- `AddTenantModuleEntitlementCommand` başarılı Addon kaydediyor mu.
- `GetTenantModuleEffectiveAccessQuery` doğru statüyü dönüyor mu.
- `EnsureAccessOrThrowAsync` beklenen sonucu dönüyor/fırlatıyor mu.

**UI Smoke:**
- Commercial > Module Entitlements sekmesi yükleniyor mu.
- Plan projection satırları listede görünüyor mu.
- Offcanvas üzerinden Addon eklenebiliyor mu.
- Plan source disable edildiğinde override başarılı şekilde arayüze yansıyor mu.
- Reload sonrasında effective access kararı aynı kalıyor mu.
- Permission denied / read-only durumları doğru çalışıyor mu.

**Enforcement Smoke:**
- Diten.Web menüsü, engellenmiş modülü gizliyor mu.
- Disabled/Blocked modül için direkt URL/API çağrısı yapıldığında engelleniyor mu.
- RBAC yetkisi, bloke edilmiş modül kuralını aşmayı engelleyebiliyor mu.
- Süresi dolmuş (expired) modüle erişim engelleniyor ve işlem audit'e yazılıyor mu.
- PPM enablement creates zero RolePermission rows; PPM removal preserves explicit rows but access is 403.
- PPM re-entitlement restores only access represented by current explicit grants/current role membership,
  creates no grant, and records the re-entitlement plus inventory visibility audit.

# 18. Assumptions
- UI için DataTable v2 (Slim) standartı benimsenecek olup, 8'den az form alanı olduğu için `Offcanvas` yapısı kullanılacaktır.
- `SubscriptionPlan` modeline dokunulmayacak, sadece o model üzerinden "okuma (read/projection)" yapılarak Plan kaynaklı default haklar alınacaktır.
- Frontend'deki menü gizleme (visibility) mantığı, `ITenantModuleAccessService` üzerinden alınan projection verisine bağlanacaktır.

# 19. Open Questions
**Karara Bağlanan (Eski) Sorular:**
- *Projection satırın Id'si yoksa ne olacak?* 
  **Karar:** Projection rows, action key olarak `TenantId + ModuleCode + Source=Plan` kullanır. UI'dan projection satırı üzerinde Disable action'ı çalıştırıldığında `DisableTenantModuleEntitlementCommand`, `TenantId + ModuleCode` alarak çalışır ve backend'de `ManualOverride` disabled kaydı yaratır.
- *Plan değişikliklerindeki sync işlemi nasıl olacak?*
  **Karar:** Default davranış query-time projection’dır. Plan kaynaklı haklar DB'ye yazılmaz.

# 20. Out of Scope
- Fatura (Billing), fatura hesaplama ve ödeme ağ geçidi entegrasyonu.
- `SubscriptionPlan` modelini tenant runtime access store'a çevirmek.
- Menü / Navigation modülünü entitlement deposu gibi kullanmak.
- RBAC yetki kataloğunun mülkiyetini (ownership) değiştirmek.
- Plan ile gelen hakların veritabanından doğrudan silinmesi (Hard delete).

# 21. Ready-for-Dev Checklist
- [ ] Domain modeli (`TenantModuleEntitlement`) fiziksel ve projection yapısına uygun tanımlandı.
- [ ] Command ve Query kontratları belirlendi.
- [ ] Effective Access precedence kuralları netleştirildi.
- [ ] CQRS handler'larında Validation logic kuralları ve conflict kuralları yazıldı.
- [ ] UI'da DataTable ve Offcanvas yerleşimleri Bootstrap standardında kurgulandı.
- [ ] RBAC öncesi `TenantModuleRequirement` / `ITenantModuleAccessService` entegrasyon rotası netleştirildi.
- [ ] Golden flow ve Failure paths üzerinden test senaryoları doğrulandı.

---

## No-shell & Contract Blocker Rules (MANDATORY)
- **No-shell rule:** Backend contract yoksa buton aktif olamaz. Mock data ile operational UI yapılmaz. Save/Enable/Disable/Remove gibi eylemler backing command olmadan render edilmez veya disabled + tooltip açıklamalı olur.
- **Contract blocker rule:** Gerekli backend contract, DTO, command, query, service, authorization handler veya persistence eksikse, bunu açık blocker olarak raporlayın. Eksik contract yerine fake UI veya mock data oluşturmak kesinlikle yasaktır.

---

## PSS-B1 — PPM authoritative decision provider

The PSS-owned physical contract is `platform.ppm-entitlement-decision.v1` at
`GET /api/internal/ppm/tenants/{tenantId:guid}/entitlement-decision`. It evaluates only the fixed canonical
`ModuleCode = PPM` through the existing `IEntitlementChecker`; it neither evaluates RBAC nor mutates
`RolePermission`.

- The caller uses an endpoint-specific PPM service credential validated by the shared secret-validation
  baseline. The AuthService internal key is not accepted and no literal or development fallback is allowed.
- `PpmEntitlementDecision:Enabled` defaults to `false`. A disabled deployment returns `503` before
  entitlement lookup and can start without the PPM credential; this is infrastructure unavailability, not
  business entitlement denial. Enabling the provider makes the dedicated credential mandatory and
  fail-closed under the shared minimum-length and forbidden-placeholder validation rules.
- The exact response is `tenantId: Guid`, `moduleCode: "PPM"`, `isAllowed: bool`,
  `resolvedAtUtc: UTC DateTimeOffset`, and `expiresAtUtc: nullable UTC DateTimeOffset`. Deny reason and
  commercial-plan detail are not disclosed.
- Missing/wrong credentials return `401`; empty/invalid tenant input returns `400`; authoritative allow or
  deny returns `200`; an `IsCacheable=false` dependency/indeterminate result returns `503` and is never
  represented as a cacheable business denial.
- Entitlement cache invalidation is a local idempotent side effect on every Platform instance. It is not
  guarded by the shared persistent `(EventId, ConsumerName)` business-consumer dedupe. Recognized malformed
  payloads fail closed into retry/DLQ; unknown event names remain safely ignored.
- This provider contract does not authorize or implement the PPM-service consumer, Gateway routing, PPM
  permission enforcement, or audit delivery.

### Gate I Model A amendment — Platform Entitlement Decision/Attestation Foundation

This amendment is an additive, typed and versioned extension to PSS-B1. The existing
`platform.ppm-entitlement-decision.v1` endpoint, its exact response shape, explicit-grant separation, authoritative
allow/deny `200` behavior and indeterminate `503` behavior remain unchanged. No existing PPM v1 field, status,
credential rule, route or runtime behavior is renamed, removed or reinterpreted.

The new logical contract identity is exactly `platform.entitlement-attestation`, contract version exactly `1.0`,
and JWT `typ` exactly `diten-entitlement-attestation+jwt`. It is a separate contract/token family and is not the PPM
v1 response. Physical route selection remains an executable-pack decision. The bounded implementation slice is
named **Platform Entitlement Decision/Attestation Foundation** and may implement only the following closed contract:

1. The input and signed payload bind the exact non-empty `TenantId`, the exact normalized `ModuleCode`, and the
   consumer-supplied canonical request hash. `ModuleCode` normalization is invariant uppercase using Unicode NFC;
   leading/trailing whitespace, aliases, wildcard, culture-sensitive casing and post-signature normalization are
   rejected. The canonical request hash is base64url SHA-256 of the FU16 canonical method, path, tenant, operation
   and body-digest bytes; it is copied exactly into the attestation.
2. The authoritative decision enum is closed to `Allowed`, `Missing`, `Disabled`, `Expired` and `NotApplicable`.
   `Allowed` is the sole allow value. The other four are authoritative business-deny values and may be signed so the
   consumer can map them to `403`; they are never collapsed into `Allowed` or infrastructure uncertainty.
3. Provider unavailable, timeout, malformed authoritative data or an indeterminate decision returns a typed `503`
   response and emits no attestation. A last-known-good decision, cache default or synthetic deny cannot substitute
   for the missing authoritative evaluation.
4. Every decision carries exact monotonic vector `EntitlementStateVersionV1 = { physicalEntitlementVersion,
   subscriptionPlanVersion, moduleApplicabilityVersion }`. The three unsigned 64-bit components respectively fence
   physical tenant entitlement changes, subscription/selected-plan changes, and module-catalog applicability
   changes. Each relevant authoritative mutation increments its component before a decision can be issued; wrap,
   reset, missing component or incomparable vector is indeterminate `503`.
5. Cache keys include `TenantId`, normalized `ModuleCode`, contract identity/version and the complete version vector.
   A cache write whose vector is older or incomparable to the current fence is rejected. Invalidation applies only
   monotonically; duplicate invalidation is idempotent, while out-of-order/incomparable invalidation fails closed and
   cannot resurrect an older allow.
6. The issuer is exactly `diten-platform-service`, the sole audience is exactly `diten-auth-service`, and signing is
   exactly `RS256` with a Platform-owned RSA key of at least 3072 bits. Protected header `kid` is mandatory and exact.
   Platform owns key generation, private-key custody, publication of trusted public validation keys, overlap,
   retirement, emergency revocation and audit. Unknown, retired, duplicated or unavailable trustworthy `kid` state
   cannot issue an attestation.
7. Signed claims use exact lower-snake-case names: `contract_id`, `contract_version`, `tenant_id`, `module_code`,
   `request_hash`, `decision`, the three version-vector members, `resolved_at_utc`, `valid_until_utc`, `iss`, `aud`,
   `iat`, `jti`. The protected header and payload are UTF-8 JSON canonicalized with RFC 8785 JCS before base64url
   encoding; duplicate keys, insignificant-field aliases, non-NFC strings, non-integer versions and non-canonical
   timestamps are malformed. UTC instants use RFC 3339 with exactly three fractional digits and `Z`; the signature
   covers the exact canonical header and payload bytes.
8. `valid_until_utc - resolved_at_utc` and `valid_until_utc - iat` are each at most 15 seconds. Revocation/invalidation
   propagation plus acceptance may expose at most a 15-second stale window. `valid_until_utc` is a hard authorization
   boundary and must not be extended by validator clock skew. Clock-skew handling may reject early; it may never allow
   after `valid_until_utc`.
9. `effective_from_utc` is absent in version `1.0`. It may be added only by a future additive contract version after
   an authoritative effective-dating field and mutation rule actually exist; current storage must not be described
   as if it already provides that fact.
10. PPM regression is binding: explicit grants remain separate from entitlement; authoritative deny remains `200` in
    the existing PPM v1 provider; provider/dependency indeterminate remains `503`; enablement creates no grant;
    removal preserves dormant explicit grants. Model A adds no mutation to those behaviors.

**Slice acceptance and evidence boundary:** contract tests must cover all five business outcomes, typed no-token
`503`, exact bindings, canonical-byte/signature fixtures, all three version components, stale-write and out-of-order
invalidation rejection, key overlap/retirement, the hard 15-second validity boundary, and unchanged PPM v1 snapshots.
This amendment is `ready-for-dev` governance only. `production_authority: none` and `execution_activation: none`
remain binding: no endpoint, runtime code, route, credential, key, migration or deployment is created here. Consumer
enforcement is separately bounded by [MOD-0018-FU16](MOD-0018-FU16-s2s-authorization-delegation-permission-provisioning.md).

## Revision Summary
- **Physical Source ile Projection Source Ayrımı:** `Source` enumunun fiziksel kayıtlarda (`ManualOverride`, `Addon`, `Trial`, `System`) kullanılması ile read-model/UI üzerinde görüntülenen (`Plan` dahil) ayrımı netleştirildi.
- **Plan Kayıt Persistansı:** Plan source’un fiziksel `TenantModuleEntitlement` entity'si olarak asla DB'ye kaydedilmeyeceği açıklandı ve `TenantModuleEntitlementRowDto` eklendi.
- **Sync Command Yanlış Anlaşılması:** Plandan fiziksel bir kayıt aktarımı (sync) olmadığı için eski `Sync` komutu, yanlış anlaşılmayı engellemek amacıyla `RefreshTenantModuleEntitlementProjectionCommand` olarak güncellendi.
- **Plan Değişikliği Davranışı:** "Plan değişikliklerindeki sync işlemi nasıl olacak?" sorusu Open Questions'tan çıkarılarak, query-time projection üzerinden çalışacağı karara bağlandı.
- **UI Module Code Örnekleri:** Tablo örneklerindeki `PSS-001` gibi teknik kodlar, `CRM`, `FINANCE`, `INVENTORY`, `HR` gibi domain seviyesindeki anlamsal (semantic) örneklerle değiştirildi.
- **Duplicate/Conflict Kuralları:** Çakışma kontrolünün sadece fiziksel kayıtlarda geçerli olduğu, Plan projection satırının `ManualOverride` kaydı ile bir "conflict" yaratmayıp aksine "override" edildiği net olarak açıklandı.
