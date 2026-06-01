---
id: PSS-007
name: Platform Subscription Feature Management
domain: platform-shared-services
service: Diten.Platform
status: review
owner: module-pack-author
branch: feature/pss/pss-007-subscription-feature-management
started: 2026-05-07
target: 2026-05-28
ui_pattern: card-grid
datatable: false
golden_reference: none
---

# PSS-007 - Platform Subscription Feature Management

## Module Summary
Platform Subscription Feature Management modülü, Diten ERP-vNext SaaS platformunda yer alan abonelik planlarına ait özelliklerin (features) kataloglanmasını, kategorilendirilmesini, aktif/pasif durumlarının yönetilmesini ve bu özelliklerin (SubscriptionPlan) abonelik planlarıyla eşleştirilmesini (mapping) sağlar.

- **Domain:** Platform & Shared Services
- **Capability Group:** Subscription, Feature Catalog & SaaS Packaging Governance
- **Primary Purpose:** Platform Admin’in SaaS özellik kataloğunu oluşturmasını, özellikleri kategorilere ayırmasını, aktif/pasif durumlarını yönetmesini ve bu özellikleri subscription planlara bağlamasını sağlamak.
- **Primary Users:** Platform Admin / Subscription Admin

## Scope and Boundaries

### In Scope
- `FeatureDefinition`, `FeatureCategory`, `PlanFeatureMapping` persistence ve API sözleşmeleri.
- Özellik (feature) oluşturma, düzenleme, arşivleme/devre dışı bırakma akışları.
- Özellik kategorisi yönetimi.
- Özelliklerin var olan `SubscriptionPlan` kayıtları ile eşleştirilmesi (mapping).
- Duplicate feature code ve slug engelleme, concurrency (RowVersion) çakışma kontrolleri.
- Platform Admin "Subscription Features / Feature Catalog" card-grid layout UI tasarımı.
- MVP seviyesinde Plan Mapping UI akışı (Feature Edit içerisinde planlara atama).

### Out of Scope (For MVP)
- Billing, pricing, invoice, payment provider entegrasyonu.
- Tenant-specific override (Tenant'a özel entitlement esnetme).
- Usage metering & quota enforcement (Kullanım kotası ölçümü).
- Runtime entitlement enforcement / Feature access enforcement (Modül erişim yetkisi zorlama).
- AuthService login enforcement.
- Public tenant self-service upgrade (Tenant'ın kendi kendine paket yükseltmesi).
- Advanced approval workflow (Onaylı plan değişimi süreçleri).
- Feature usage analytics (Özellik kullanım analizleri).
- Full Plan-Feature matrix (İleri fazlara bırakılmıştır).
- Hard delete operasyonları.

## ERP Tenant Usage / Entitlement Boundary

Bu modülün Tenant ve ERP runtime tarafı ile olan etkileşim sınırları aşağıdaki gibidir:

**PSS-007 ne yapar:**
- Platform genelinde feature catalog tutar.
- Feature’ları subscription planlara bağlar.
- Hangi planın hangi feature’lara sahip olduğunu belirler.
- Platform Admin için SaaS packaging yönetimi sağlar.

**PSS-007 MVP’de ne yapmaz:**
- Tenant runtime access enforcement yapmaz.
- ERP modüllerinde buton/sayfa/işlem yetkisi kontrol etmez.
- `_Tenant` ekranlarında doğrudan erişim kısıtlaması uygulamaz.
- Tenant özel override yönetmez.
- AuthService login veya token claim enforcement yapmaz.

**ERP/Tenant tarafında kullanım nasıl olmalı:**

- **A. Tenant Detail ekranı:** Tenant Detail içinde ileride “Subscription / Entitlements / Features” gibi bir sekme olabilir. Bu sekme PSS-007’den doğrudan feature catalog yönetmez. Sadece tenant’ın bağlı olduğu `SubscriptionPlan` üzerinden effective feature listesini read-only veya summary olarak gösterebilir. (Örnek: `Tenant -> SubscriptionPlanId -> PlanFeatureMappings -> Features`).
- **B. Tenant Create/Edit ekranı:** Tenant oluştururken veya düzenlerken tenant’a bir `SubscriptionPlan` atanabilir. Bu işlem PSS-007’nin owned objectlerini değiştirmez. Sadece tenant’ın plan seçimi üzerinden hangi feature’lara sahip olacağı hesaplanabilir.
- **C. ERP runtime menü / modül görünürlüğü:** ERP tarafında menü ya da modül görünürlüğü feature’a göre kısıtlanacaksa bu PSS-007’nin işi değildir. Bu, ileride ayrı bir Tenant Entitlement / Runtime Enforcement modülü veya mevcut authorization/entitlement mekanizması üzerinden yapılmalıdır.
- **D. `_Tenant` partial/view/component kullanımı:** Eğer projede `_Tenant` partial/view/component alanları tenant context göstermek için kullanılıyorsa: PSS-007 burada feature catalog yönetimi yapmamalıdır. Sadece tenant’ın planından türeyen feature summary gösterilecekse read-only olarak consume etmeli, create/edit/delete/mapping action’ları `_Tenant` alanına konulmamalı, platform admin Feature Catalog ekranı ayrı kalmalıdır.
- **E. Future integration contract önerisi:** İleride tenant tarafı için ayrı read-only contract önerilir: `GET /api/platform/tenants/{tenantId}/effective-features` veya `GET /api/platform/subscription-plans/{planId}/features`. Bu contract sadece okuma amaçlı olmalı. Tenant entitlement enforcement için ayrı modül/batch gerekir.

### Deferred: Tenant Entitlement Enforcement
ERP modüllerinde feature bazlı erişim kontrolü gerekiyorsa bu PSS-007’den sonra ayrı bir module pack olarak ele alınmalıdır. Bu yeni scope muhtemel olarak `TenantSubscription`, `TenantEntitlementSnapshot`, `EffectiveFeatureResolver` gibi kavramlar içerebilir. Ancak PSS-007 MVP içinde bu geliştirilmemelidir.

## Owned Objects

**1. FeatureDefinition**
SaaS kataloğundaki her bir özelliği temsil eder.
- `Id` (Guid): System generated.
- `FeatureCode` (string): Zorunlu, uppercase, stable ve unique olmalı.
- `FeatureSlug` (string): Zorunlu, lowercase kebab-case ve unique olmalı.
- `DisplayName` (string): UI'da gösterilen ad (Active için zorunlu).
- `Description` (string): Kısa açıklama.
- `CategoryId` (Guid?): Kategori bağlantısı (Active için zorunlu).
- `Status` (enum): Draft, Active, Inactive, Deprecated, Archived.
- `IsCoreFeature` (bool): Çekirdek özellik olup olmadığı.
- `SortOrder` (int): Sıralama.
- `OptionalFeatureFlagKey` (string?): Var olan Feature Flag sistemine referans.
- `CreatedAtUtc` (DateTimeOffset): System generated.
- `CreatedBy` (Guid?): Oluşturan kullanıcı.
- `UpdatedAtUtc` (DateTimeOffset?): System updated.
- `UpdatedBy` (Guid?): Güncelleyen kullanıcı.
- `RowVersion` (byte[]): Concurrency kontrolü için.

**2. FeatureCategory**
Özelliklerin gruplandığı kategoriler (örn: Security, Reporting, vb.)
- `Id` (Guid): System generated.
- `CategoryCode` (string): Unique olmalı.
- `DisplayName` (string): UI gösterim adı.
- `Description` (string): Kısa açıklama.
- `SortOrder` (int): Sıralama.
- `Status` (enum): Active, Inactive, Archived.
- `CreatedAtUtc` (DateTimeOffset): System generated.
- `UpdatedAtUtc` (DateTimeOffset?): System updated.
- `RowVersion` (byte[]): Concurrency kontrolü için.

**3. PlanFeatureMapping (FeaturePlanAvailability)**
Hangi özelliğin hangi subscription planda ne şekilde yer aldığını tutar.
- `Id` (Guid): System generated.
- `SubscriptionPlanId` (Guid): Plan ID.
- `FeatureDefinitionId` (Guid): Özellik ID.
- `AvailabilityStatus` (enum): Included, AddOn, NotAvailable, Preview.
- `EffectiveFromUtc` (DateTimeOffset?): Geçerlilik başlangıcı.
- `EffectiveToUtc` (DateTimeOffset?): Geçerlilik bitişi.
- `CreatedAtUtc` (DateTimeOffset): System generated.
- `UpdatedAtUtc` (DateTimeOffset?): System updated.
- `RowVersion` (byte[]): Concurrency kontrolü için.

### Consumed Dependencies: SubscriptionPlan
`SubscriptionPlan` nesnesi Diten.Platform altında mevcuttur ve aşağıdaki somut yapı üzerinden consume edilecektir. **Kesinlikle yeniden oluşturulmayacaktır.**

- **Entity Path:** `services/Diten.Platform/src/Diten.Platform.Domain/Entities/SubscriptionPlan.cs`
- **Repository Interface:** `services/Diten.Platform/src/Diten.Platform.Domain/Repositories/ISubscriptionPlanRepository.cs`
- **Controller:** `services/Diten.Platform/src/Diten.Platform.API/Controllers/Platform/SubscriptionPlansController.cs`
- **Status Field:** Sınıf içinde `public bool IsActive { get; set; }` alanı mevcuttur.
- **Inactive/Archived Plan Kontrolü:** Plan mapping işlemi sırasında `IsActive == false` olan planlar inactive/archived kabul edilerek filtrelemede veya engellemede kullanılacaktır.

## Entity Schema Rules & Persistence Pattern
- **Persistence Pattern:** Diten.Platform içindeki mevcut persistence pattern olan **MongoDB** (Mongo collection, index ve repository yaklaşımı) kullanılacaktır. `MongoDbIndexConfigurations.cs` vb. sınıflara eklentiler yapılacaktır.
- Base Type olarak `GlobalEntity` kullanılacaktır. Çünkü özellik kataloğu tenant spesifik bir veri değil, tüm platformu ilgilendiren system-of-record master datasıdır.
- `Version` ismi kullanılmamış, `RowVersion` tercih edilmiştir.

## Validation Rules
- **FeatureCode**: Required, uppercase, unique, stable.
- **FeatureSlug**: Required, lowercase kebab-case, unique.
- **DisplayName**: Active durumunda required.
- **CategoryId**: Active durumunda required.
- **CategoryCode**: Unique.
- Aynı `SubscriptionPlanId` + `FeatureDefinitionId` için birden fazla (duplicate) mapping olamaz.
- `SubscriptionPlan` mevcut değilse mapping yapılamaz.
- Inactive veya Archived plana mapping bağlanamaz.
- Archived feature, yeni bir plana bağlanamaz.
- `RowVersion` conflict durumunda 409 benzeri kontrollü hata fırlatılmalı; UI raw exception göstermemeli.

## Failure Path to Verify
Geliştirme sırasında aşağıdaki hata senaryolarının kontrollü çalıştığı doğrulanmalıdır:

- **Duplicate FeatureCode**
  - **Expected:** API controlled 409/validation response döner. UI field-level error gösterir. Kayıt oluşmaz. Reload sonrası corrupt/duplicate veri görünmez.
- **Duplicate FeatureSlug**
  - **Expected:** API controlled 409/validation response döner. UI slug alanında hata gösterir. Kayıt oluşmaz. Reload sonrası veri bozulmaz.
- **Missing DisplayName**
  - **Expected:** Save engellenir. UI DisplayName için validation mesajı gösterir.
- **Active feature için CategoryId eksik**
  - **Expected:** Active status’a geçiş veya save engellenir. UI category alanında validation mesajı gösterir.
- **Missing veya inactive SubscriptionPlan’a mapping**
  - **Expected:** Mapping save engellenir. API controlled validation error döner. Existing mappings bozulmadan kalır.
- **Archived feature’a yeni plan mapping**
  - **Expected:** Mapping save engellenir. UI “Archived feature cannot be mapped to a plan” benzeri kontrollü mesaj gösterir.
- **Unauthorized user create/edit/archive/mapping denemesi**
  - **Expected:** API 403 döner. UI action disabled veya permission-denied state gösterir. Backend tarafında işlem yapılmaz.
- **RowVersion concurrency conflict**
  - **Expected:** API controlled 409 conflict döner. UI kullanıcıya “data changed, reload required” tarzı mesaj gösterir. Son yazan kişinin verisi sessizce ezilmez.

## Gateway / API Routing Decision
**Karar:** Gateway değişikliği gereklidir (Ayrı task olarak ele alınmalıdır).
- `frontend/Diten.Web` API çağrılarını doğrudan `Diten.Platform` (Port 5057) servislerine **yapmaz**, Gateway (Ocelot - Port 5000) üzerinden yapar.
- `gateway/Diten.ApiGateway/ocelot.json` incelendiğinde `/api/platform/{everything}` şeklinde bir catch-all route **bulunmamaktadır.** Mevcut rotalar (`subscription-plans`, `module-catalog` vb.) spesifik olarak eklenmiştir.
- Bu nedenle, `/api/platform/subscription-features` ve `/api/platform/feature-categories` için yeni rotaların `ocelot.json`'a eklenmesi **gereklidir.**
- Gateway klasörü (`gateway/Diten.ApiGateway/**/ocelot.json`) protected path olduğundan, bu ekleme PSS-007 batch işi içinde değil, ayrı bir Integration/Gateway task olarak yürütülmelidir.

## Authorization Convention
Proje genelinde Platform API tarafında uygulanan Authorization modeli doğrulanmıştır:
- **Kullanılan Attribute:** Controller seviyesinde `[Authorize(Policy = "PlatformActor")]`, Action (metot) seviyesinde `[HasPermission("...")]` attribute'u kullanılmaktadır.
- **Permission Key Formatı:** PascalCase.PascalCase.Action (örneğin: `Platform.SubscriptionPlans.Read`).
- Bu modül için kullanılacak kesin **Permission Key'ler**:
  - `Platform.SubscriptionFeatures.Read`
  - `Platform.SubscriptionFeatures.Create`
  - `Platform.SubscriptionFeatures.Update`
  - `Platform.SubscriptionFeatures.Archive`
  - `Platform.SubscriptionFeatures.ManageMappings`
  - `Platform.SubscriptionFeatures.Audit.Read`

## API Endpoint Proposal (Backend Scope)
Varsayılan PSS ve CQRS yaklaşımlarına göre:
- `GET /api/platform/subscription-features` -> `GetFeatureCatalogQuery`
- `GET /api/platform/subscription-features/{id}` -> `GetFeatureDefinitionByIdQuery`
- `POST /api/platform/subscription-features` -> `CreateFeatureDefinitionCommand`
- `PUT /api/platform/subscription-features/{id}` -> `UpdateFeatureDefinitionCommand`
- `POST /api/platform/subscription-features/{id}/archive` -> `ArchiveFeatureDefinitionCommand`
- `GET /api/platform/feature-categories` -> `GetFeatureCategoriesQuery`
- `POST /api/platform/feature-categories` -> `CreateFeatureCategoryCommand`
- `GET /api/platform/subscription-plans/{planId}/features` -> `GetPlanFeatureMappingsQuery`
- `PUT /api/platform/subscription-plans/{planId}/features` -> `UpdatePlanFeatureMappingsCommand`

## UI Scope (Platform Admin)
- **Sayfa:** Subscription Features / Feature Catalog
- **Layout:** Platform Admin Layout (`_LayoutPlatformAdmin.cshtml` veya `_LayoutBackbone.cshtml`). Kesinlikle Tenant'lara açık olmamalı.
- **Pattern:** İki kolonlu feature card grid yapısı. DataTable kullanılmamalı.
- **Filtreler:** All Features, Analytics, Integration, Security vb. (Üstte yer alan kategori filtreleri), Plan/Status filter, Search input.
- **Card Content:** Feature DisplayName, Code/Slug, Description, Status/Category badge'leri, included plans, edit, archive/deactivate aksiyon ikonları.
- **Save State:** Kaydetme sonrası sayfa reload olduğunda verinin aynı state'te kalması (Golden Flow).
- **No-shell rule:** Backend/API ve persistence tarafında henüz hazır olmayan özellikler için UI'a buton konulmamalı (veya konulursa disabled olmalı).

### Plan Mapping UI Akışı (MVP vs Deferred)
- **MVP Akışı:** Feature Catalog listesi card-grid olur. Feature edit modal/drawer içerisinde bir **"Available in Plans"** bölümü yer alır. Burada sistemdeki mevcut subscription planlar listelenir. Her plan için `Included`, `AddOn`, `NotAvailable`, `Preview` status'lerinden biri seçilir ve feature özelinde mapping yapılır.
- **Deferred (İleri Fazlar):** Feature ve Subscription planların tek ekranda tablo biçiminde kesiştiği Full Plan-Feature Matrix sayfası MVP kapsamında **değildir**, P1/P2'ye bırakılmıştır.

### Delete Aksiyonu ve MVP Sınırları
- MVP kapsamında **hard delete operasyonları out-of-scope bırakılmıştır.**
- Kullanılacak aksiyonlar sadece **Archive** ve **Deactivate** olmalıdır.
- **Gerekçe:** Feature geçmişi, plan mapping geçmişi ve audit/governance açısından bir özelliği (özellikle ilişkili bir varlığı) hard delete ile veritabanından kalıcı olarak silmek yüksek risk taşır.

## Repo Scope (Protected and Target Paths)

### Protected Paths
- `.antigravity/**`
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`
- `gateway/Diten.ApiGateway/**/ocelot.json` (Ayrı bir Integration task olarak yönetilecektir)
- Diğer domain servis klasörleri (`services/Diten.AuthService`, `services/Diten.DevEnablementService`, vs.)

### Target (Expected) Paths
- `services/Diten.Platform/src/Diten.Platform.Domain/Features/SubscriptionFeatures/**`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/SubscriptionFeatures/**`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Configurations/**`
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/Platform/SubscriptionFeaturesController.cs`
- `frontend/Diten.Web/Controllers/Platform/SubscriptionFeaturesController.cs`
- `frontend/Diten.Web/Views/Platform/SubscriptionFeatures/**`
- `frontend/Diten.Web/wwwroot/assets/js/Platform/SubscriptionFeatures/**`

## Acceptance Criteria
### Runtime Criteria
- [x] Platform Admin persisted feature kartlarını listeleyebilir.
- [x] Platform Admin yeni feature oluşturabilir, düzenleyebilir ve subscription planlara bağlayabilir.
- [x] Save sonrası reload edildiğinde kayıtlı veri korunur (Golden Flow başarılı işler).
- [x] Platform dışı kullanıcılar (Tenant) bu ekrana erişemez.

### Integrity Criteria
- [x] Duplicate FeatureCode ve FeatureSlug kayıtları engellenir.
- [x] Invalid plan mapping (örn: var olmayan veya inactive plan) engellenir.
- [x] Archived feature yeni plana bağlanamaz.
- [x] RowVersion conflict durumunda API controlled hata üretir (raw exception göstermez).
- [x] `SubscriptionPlan` varlıkları bu modül aracılığıyla kazara duplicate edilmez veya yaratılmaz.
- [x] Runtime entitlement enforcement (yetki zorlama) bu batch içerisinde yanlışlıkla uygulanmaz.

### UX Criteria
- [x] Loading, Empty ve No-result state'leri mevcuttur.
- [x] Validation error, Permission denied ve Backend failure state'leri kontrollü bir şekilde render edilir.
- [x] Action disable olduğunda veya çalışmadığında nedeninin tooltipleri/açıklamaları bulunur.
- [x] Raw exception veya stack trace UI'da kesinlikle görünmez.

## Implementation Audits

- [x] Batch 1 audit: `docs/audits/pss-007-subscription-feature-management-batch1-audit.md`
- [x] Batch 2 audit: `docs/audits/pss-007-subscription-feature-management-batch2-audit.md`

## Next Implementation Batch

**Recommended Option A — Safer split (Önerilen)**

*Batch 1: Data Foundation + FeatureDefinition/FeatureCategory Create-Save-Reload*
- **Kapsam:** FeatureDefinition entity/persistence, FeatureCategory entity/persistence, unique FeatureCode/FeatureSlug validation, create feature API, get feature catalog API, update feature API temel hali, reload persistence proof, minimal tests.
- **Kapsam dışı:** PlanFeatureMapping, archive/deactivate, full UI, entitlement enforcement, billing.

*Batch 2: Plan Mapping + Archive/Deactivate + RowVersion Hardening*
- **Kapsam:** PlanFeatureMapping persistence, plan exists/active validation, archived feature mapping block, archive/deactivate API, RowVersion conflict handling, mapping tests.

**Recommended Option B — Single first batch**
- Data Foundation + Create/Save/Reload + Basic Mapping işlemlerinin tamamını tek seferde yapmak (Eğer repo durumu uygunsa ve task büyük olmayacaksa tercih edilebilir, ancak A seçeneği daha güvenlidir).

## Ready-for-dev Checklist
Modülün durumunun `draft` seviyesinden `ready-for-dev` veya `approved` aşamasına geçebilmesi için aşağıdaki maddelerin onaylanması gerekir:
- [x] SubscriptionPlan source path confirmed (Doğrulandı)
- [x] SubscriptionPlan active/inactive status rule confirmed (Doğrulandı, `IsActive` alanı var)
- [x] Diten.Platform Mongo persistence pattern confirmed (Doğrulandı)
- [x] Platform Admin layout/route confirmed (Doğrulandı)
- [x] Authorization attribute and permission key convention confirmed (Doğrulandı, `[HasPermission]` & PascalCase.Dot format)
- [x] Gateway route decision confirmed (Doğrulandı, yeni Ocelot rotası eklenecek, ayrı task)
- [x] ERP/Tenant usage boundary documented (Doğrulandı)
- [x] Runtime entitlement enforcement explicitly deferred (Doğrulandı)
- [x] First implementation batch split decision confirmed (Doğrulandı, Option A önerildi)
- [x] MVP out-of-scope list confirmed (Doğrulandı)
