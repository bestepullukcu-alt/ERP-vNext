---
file_name: MOD-0298-tenant-module-entitlements.md
id: MOD-0298
name: Tenant Module Entitlements
domain: platform-shared-services
status: approved
owner: platform-team
branch: feature/pss/mod-0298-tenant-module-entitlements
created_at: 2026-05-11
golden_reference: slim
---

# 1. Module Pack Summary
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

## Revision Summary
- **Physical Source ile Projection Source Ayrımı:** `Source` enumunun fiziksel kayıtlarda (`ManualOverride`, `Addon`, `Trial`, `System`) kullanılması ile read-model/UI üzerinde görüntülenen (`Plan` dahil) ayrımı netleştirildi.
- **Plan Kayıt Persistansı:** Plan source’un fiziksel `TenantModuleEntitlement` entity'si olarak asla DB'ye kaydedilmeyeceği açıklandı ve `TenantModuleEntitlementRowDto` eklendi.
- **Sync Command Yanlış Anlaşılması:** Plandan fiziksel bir kayıt aktarımı (sync) olmadığı için eski `Sync` komutu, yanlış anlaşılmayı engellemek amacıyla `RefreshTenantModuleEntitlementProjectionCommand` olarak güncellendi.
- **Plan Değişikliği Davranışı:** "Plan değişikliklerindeki sync işlemi nasıl olacak?" sorusu Open Questions'tan çıkarılarak, query-time projection üzerinden çalışacağı karara bağlandı.
- **UI Module Code Örnekleri:** Tablo örneklerindeki `PSS-001` gibi teknik kodlar, `CRM`, `FINANCE`, `INVENTORY`, `HR` gibi domain seviyesindeki anlamsal (semantic) örneklerle değiştirildi.
- **Duplicate/Conflict Kuralları:** Çakışma kontrolünün sadece fiziksel kayıtlarda geçerli olduğu, Plan projection satırının `ManualOverride` kaydı ile bir "conflict" yaratmayıp aksine "override" edildiği net olarak açıklandı.
