---
id: CAND-CAP-0002-FU03
name: Subscription Feature Management
domain: platform-shared-services
service: Diten.Platform
status: draft
owner: module-pack-author
branch: feature/pss/pss-007-subscription-feature-management
started: 2026-05-07
target: 2026-05-28
ui_pattern: tabbed-datatable
datatable: true
golden_reference: slim+compact
revision: R2 (2026-06-19) — card-grid → two-tab DataTable redesign (Categories | Features)
---

# CAND-CAP-0002-FU03 — Subscription Feature Management

> **Canonicalization (DCP-002):** Governance identity is now **CAND-CAP-0002-FU03**, a child of **CAND-CAP-0002**. Prior repo ID **PSS-007** is a deprecated alias. Temporary candidate; pending EA. Ref: `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`.

## R2 Revision — Two-Tab DataTable Redesign (2026-06-19)

> **Revize gerekçesi:** İlk teslim (`card-grid`, DataTable yok) kategorinin yönetim yüzeyini eksik bıraktı: kategori **oluşturulabiliyor ama düzenlenemiyor/arşivlenemiyor** (immutable `CategoryCode`, değiştirilemez `Status`/`DisplayName`). Bu R2 revizyonu sayfayı iki sekmeli, GoldenReference DataTable tabanlı bir yönetim ekranına dönüştürür ve kategori lifecycle'ını (Update + Archive/Deactivate) tamamlar.

### Yeni Layout Kararı
- **Sayfa kabuğu:** `_LayoutPlatformAdmin.cshtml` (değişmez).
- **Sekme deseni — referans: Tenant Details (`Views/Platform/Tenants/Details.cshtml`).** Üstte `card > card-header` içinde `nav nav-pills` + `wc-tab-compact` buton stili, ikon + `data-bs-toggle="tab"` / `data-bs-target`, altta `tab-content > tab-pane fade`. Yeni tab CSS icat edilmez; Tenant Details'taki birebir sınıflar kullanılır.
- **İki sekme:**
  1. **Categories** — kategori GoldenReference DataTable (Code, Name, Sort, Status, Actions) + kendi Add/Edit/Archive akışı.
  2. **Features** — feature GoldenReference DataTable (Feature, Code/Slug, Category, Status, Actions) + kendi Add/Edit/Archive + plan-mapping akışı.
- **DataTable standardı:** Her iki tablo da `data-dt-standard="v2"` + `DtDefaults.create()` + `_Filter.cshtml` inline filter + skeleton loading ile GoldenReference'a uyar (Tenant Details `dtTenantAdminUsers` deseni gibi). `card-grid` ve `sf-grid` tamamen kaldırılır.

### Slim/Compact Create-Edit Surface Kararı (form_field_count)
- **Category = Slim** (5 alan: CategoryCode, DisplayName, Description, SortOrder, Status) → Categories tab içinde `_CreateEditOffcanvas.cshtml` (offcanvas create/edit). Mevcut `_CategoryEditor.cshtml` modal'ı bu offcanvas'a taşınır ve **edit modunu** destekler.
- **Feature = Compact** (≥9 alan: FeatureCode, FeatureSlug, DisplayName, Description, CategoryId, Status, IsCoreFeature, SortOrder, OptionalFeatureFlagKey + plan-mapping bölümü) → route tabanlı `/Platform/SubscriptionFeatures/Create` + `/{id}/Edit` full-page form. `_Form.cshtml` + `Details.cshtml` aynı section haritasını kullanır.
  > ✅ **Karar (2026-06-19, kullanıcı onayı):** Feature editor full-page Compact olacak; mevcut `_FeatureEditor` modal'ı kaldırılır. Golden reference field-count kuralına uyumludur.

### Kategori Lifecycle Tamamlama (yeni backend scope)
İlk pakette kategori sadece GET + POST'tu. R2 ile eklenir:
- `PUT /api/platform/feature-categories/{id}` → `UpdateFeatureCategoryCommand` (DisplayName, Description, SortOrder, Status güncellenebilir; `CategoryCode` immutable kalır).
- `POST /api/platform/feature-categories/{id}/archive` → `ArchiveFeatureCategoryCommand` (hard delete YOK; mevcut feature archive/deactivate deseninin aynısı, RowVersion concurrency ile).
- Kategori entity'sine `RowVersion` zaten tanımlı; Update/Archive RowVersion conflict → kontrollü 409.
- Yeni permission key: `Platform.SubscriptionFeatures.Categories.Manage` (veya mevcut `.Update`/`.Archive` anahtarlarının kategoriye genişletilmesi — security-agent netleştirir).

### Bu revizyonla geçersiz kılınan ilk-paket kararları
- ❌ "İki kolonlu feature card grid" / "DataTable kullanılmamalı" → ✅ İki-tab GoldenReference DataTable.
- ❌ Kategori create-only → ✅ Kategori tam lifecycle (Create/Update/Archive).

### Kullanıcı onaylı uygulama kararları (2026-06-19)
- **Tablo init:** Lazy — açılışta aktif tab (Categories) init; Features tab ilk gösterimde init (gizli tab DataTable genişlik sorununu önlemek için `columns.adjust()` + `responsive.recalc()` tab `shown.bs.tab` event'inde).
- **Golden parite:** Her iki tab tam golden (inline filter + save-view/personalization + colReorder). Categories tab'ında bulk-delete YOK (kategori bulk backend yok); feature tab'ında bulk-delete mevcut feature endpoint'iyle.
- **Plan mapping:** Feature Compact full-page `_Form.cshtml` içinde bir section olarak taşınır; mevcut `_FeatureEditor` modal mantığı `form.js`'e taşınır.
- **Personalization pageKey:** İki ayrı view scope — `SubscriptionFeaturesCategories` ve `SubscriptionFeaturesFeatures` (moduleKey `Platform`).

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
- `PUT /api/platform/feature-categories/{id}` -> `UpdateFeatureCategoryCommand` *(R2 — yeni)*
- `POST /api/platform/feature-categories/{id}/archive` -> `ArchiveFeatureCategoryCommand` *(R2 — yeni)*
- `GET /api/platform/subscription-plans/{planId}/features` -> `GetPlanFeatureMappingsQuery`
- `PUT /api/platform/subscription-plans/{planId}/features` -> `UpdatePlanFeatureMappingsCommand`

## UI Scope (Platform Admin) — R2 Two-Tab DataTable

> **NOT (R2):** Aşağıdaki ilk-paket "card-grid / DataTable kullanılmamalı" kararı **geçersizdir**. Bkz. üstteki *R2 Revision — Two-Tab DataTable Redesign* bölümü.

- **Sayfa:** Subscription Features / Feature Catalog
- **Layout:** `_LayoutPlatformAdmin.cshtml`. Kesinlikle Tenant'lara açık olmamalı.
- **Pattern:** **İki sekmeli** ekran (Tenant Details tab deseni referans). `card-header > nav nav-pills` + `wc-tab-compact`, `tab-content > tab-pane fade`.
  - **Tab 1 — Categories:** GoldenReference DataTable (`data-dt-standard="v2"`, `DtDefaults.create()`, `_Filter.cshtml` inline filter, skeleton). Kolonlar: Code, Display Name, Sort, Status, Actions. Add/Edit = Slim `_CreateEditOffcanvas.cshtml`; Archive = ortak confirm wrapper.
  - **Tab 2 — Features:** GoldenReference DataTable. Kolonlar: Feature (DisplayName), Code/Slug, Category, Status, Actions. Add/Edit = Compact route `/Create` + `/{id}/Edit` (full-page `_Form.cshtml` + `Details.cshtml`); plan-mapping bölümü feature form içinde. Archive/Deactivate = ortak confirm.
- **Filtreler (inline `_Filter.cshtml`):** Features tab'ında Category + Status Select2 chip; Categories tab'ında Status. Text-input enum filtresi YASAK (Select2 chip — orchestrator [RULE]).
- **Save State:** Kaydetme/silme sonrası `dt.ajax.reload(..., false)` → success toast (create/bulk delete baseline). Reload sonrası state korunur (Golden Flow).
- **No-shell rule:** Backend/API hazır olmayan aksiyon için buton konulmaz veya disabled olur.
- **Ctrl+K:** `/Platform/SubscriptionFeatures` stable route'u zaten registry'de ise sekme değişimi yeni route üretmez (tek sayfa, tab state client-side). Yeni `/Create` route'u `platform-global-search-registry.md` kapsamına göre değerlendirilir (dynamic değilse eklenebilir).

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

### R2 Redesign Criteria
**Faz 2a (tamamlandı — build-verified 2026-06-19):**
- [x] Sayfa iki sekmeli açılır (Categories | Features), Tenant Details tab deseniyle birebir stil.
- [x] Categories tab GoldenReference DataTable (`data-dt-standard="v2"`, `createCrudTable`, inline filter, skeleton) kullanır.
- [x] Kategori düzenlenebilir (DisplayName/Description/SortOrder/Status); `CategoryCode` immutable (edit modunda readonly).
- [x] Kategori arşivlenebilir (hard delete yok); RowVersion conflict → kontrollü 409.
- [x] en/tr resx yeni anahtarlar (CategoriesTab, FeaturesTab, EditCategory, ArchiveCategory) için dolu.
- [x] `dotnet build` web PASS (0 hata).
- [ ] Runtime smoke test (Kanal C — kullanıcı doğrulaması bekleniyor).

**Faz 2b (tamamlandı — build-verified 2026-06-19):**
- [x] Features tab GoldenReference DataTable'a dönüştü (`createCrudTable` client-mode + custom `dataSrc` → `{data:{items}}`, inline filter status+category, colReorder); `card-grid`/`sf-grid` kaldırıldı, `_FeatureEditor` modal silindi.
- [x] Feature create/edit Compact full-page (`/Create` + `/{id}/Edit`, `_Form.cshtml` 4 section); No-ViewModel + JS-fetch (form.js). Plan-mapping form section olarak taşındı.
- [x] Feature Details full-page (`/Details/{id}`, `subscription-feature-details` + `backbone-preview-section`, details.js); _Form ile aynı 4 section haritası.
- [x] `dotnet build` web PASS (0 hata).
- [ ] Runtime smoke test (Kanal C — kullanıcı doğrulaması bekleniyor).

**Faz 2c (kullanıcı kararıyla kapsam dışı — 2026-06-19):**
- ~~Save-view/personalization~~ → Kullanıcı "gerek yok" dedi; kapsam dışı bırakıldı.

**Kalan açık maddeler:**
- [ ] Runtime smoke test (Kanal C) — tam stack ile tarayıcı doğrulaması (kullanıcı UI incelemesiyle).
- [x] Dokümantasyon tazeleme: `api.md` (kategori PUT/archive + two-tab/full-page routes) + `user-manual.md` (two-tab + full-page) güncellendi.

### Verify Script İstisnası (gerekçeli)
`verify_datatable_page.py` tek-tablolu kanonik dosya adları (`_Filter.cshtml`, `_DataTable.cshtml`, `_CreateEditOffcanvas.cshtml`) varlığını arar. Bu sayfa **onaylı iki-tab çift-tablo** tasarımı olduğundan tek kanonik isim 1:1 karşılanamaz; partial'lar tab-özel adlandırılır (`_CategoriesTab`, `_FeaturesTab`, `_CategoryOffcanvas`, `_Form`). Script bir dosya-varlık heuristic'idir (davranışsal değil); bu FAIL false-negative'dir. Golden davranış (skeleton, inline filter, colReorder, `data-dt-standard="v2"`, `DtDefaults.create`, ortak confirm) korunur.

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
