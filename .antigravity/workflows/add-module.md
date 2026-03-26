---
description: "WORKFLOW-000 — Yeni Modül Oluşturma Orkestrasyonu (Ana Senaryo)"
---

# /add-module - Yeni Modül Oluşturma

Bu workflow, bir modülün sıfırdan son kullanıcıya ulaşana kadarki tüm katmanlarını koordine eder. Ajan, bu adımları sırasıyla ve HİÇBİR inisiyatif almadan ZORUNLU olarak uygulamalıdır.

## 🎭 Görev Dağılımı (Orkestra)

1. **Phase 1: Analiz (business-analyst)**
   - Modülün alanlarını (fields), IFRS/KVKK gereksinimlerini belirle.
   - UI ve tablolarda kullanılacak anahtar kelimeleri (Keys) çıkar.

1.5. **Phase 1.5: Mimari Doğrulama (ORKESTRATOR)**
   - **KRİTİK ADIM:** Kod yazmadan ÖNCE, üretilecek kodun taslağını kural dosyalarıyla kıyasla.
   - **Mimari Onay Zorunluluğu:** Ajan, aşağıdaki kontrol listesini doldurup KULLANICIDAN ONAY ALMADAN kod yazamaz:
     ```
     □ PRD'deki TÜM alanlar Entity'ye eklendi mi?
     □ Alan isimleri global ERP standartlarına uygun mu? (PlateCode → Code)
     □ Repository interface'inde TenantId/Soft-Delete garantisi var mı?
     □ EntityBase'ten miras alınıyor mu? (TenantId, IsDeleted)
     □ CQRS yapısı (Command, Query, Handler, Validator) planlandı mı?
     ```
   - **Onay Formatı:** "Faz 1.5 Mimari Doğrulama tamamlandı. Onayınızı bekliyorum."
   - Kullanıcı onaylamazsa → Phase 1'e dön, düzelt.

2. **Phase 2: Veri Mimarisi (data-agent & backend-architect)**
   - MongoDB koleksiyonunu tasarla (`ITenantDocument` tabanlı).
   - Domain Entity ve Repository katmanını oluştur. (Soft Delete ve TenantId ZORUNLUDUR).

3. **Phase 3: İş Mantığı & Yerelleştirme (backend-architect & l10n-agent)**
   - `/add-endpoint-cqrs` akışını başlat (Request, Command, Handler, Validator).
   - API Controller'ı oluştur ve Ocelot Gateway rotasını ekle.
   - **ÖNCE `l10n-agent`:** `.antigravity/rules/localization-standard.md` kuralına göre 9 dil `.resx` senkronizasyonunu tamamla.
   - Sadece projede desteklenen dillerde (9 dil) `.resx` dosyalarını oluştur.
   - **⚠️ RESX DOSYA ADI KURALI (KRİTİK):** `.resx` dosya adı, Razor view'da kullanılan localization marker class adıyla **birebir eşleşmelidir**. Eğer marker class `CountriesIndex` ise dosya adı `CountriesIndex.{lang}.resx` olmalıdır. `Index.{lang}.resx` KULLANILMAZ. Yol: `Resources/Views/{AreaName}/{ModuleName}/{MarkerClassName}.{lang}.resx`
     - Örnek: Class = `LegalEntitiesIndex` → `LegalEntitiesIndex.en.resx`, `LegalEntitiesIndex.tr.resx`, ...
     - Örnek: Class = `CountriesIndex` → `CountriesIndex.en.resx`, `CountriesIndex.tr.resx`, ...
   - **Kritik:** Ortak kelimeleri (`Kaydet`, `Sil` vb.) `SharedResource`'tan al, yazma. Sayfaya özel olan başlıkları ve tablo kolon anahtarlarını ekle.
   - `.resx` dosyalarında `PageDescription` key'i her modül için tanımlanmalıdır. Alt başlıklar hardcoded yazılmaz.

3.5. **Phase 3.5: Gateway Doğrulama (integration-agent)**
   - **[KRİTİK]:** `.antigravity/rules/routes.md` dosyasını oku.
   - `ocelot.json`'a yeni modül için **iki explicit rota** ekle:
     - `UpstreamPathTemplate: "/api/{resource}"` → `DownstreamPathTemplate: "/api/{resource}"`  (Port: 5050)
     - `UpstreamPathTemplate: "/api/{resource}/{everything}"` → `DownstreamPathTemplate: "/api/{resource}/{everything}"` (Port: 5050)
   - Her iki rotaya da `UpstreamHttpMethod`: `["GET", "POST", "PUT", "PATCH", "DELETE"]` ekle.
   - Yeni rotaları catch-all rotasından (`/services/mdm/{everything}`) **ÖNCE** konumlandır.
   - Gateway rotası eklenmeden "İşlem tamam" denilmez.

4. **Phase 4: Arayüz (frontend-ui-ux)**
   - **[KRİTİK]:** `.antigravity/rules/frontend-datatable-template.md` dosyasını referans al.
   - **⚠️ `Areas/` KULLANILMAZ:** View dosyaları her zaman `Views/{AreaName}/{ModuleName}/` altına konur. `Areas/{AreaName}/Views/` yapısı ASP.NET Areas routing'dir ve projede KULLANILMAZ.
     - ✅ `Views/MDM/Countries/Index.cshtml`
     - ❌ `Areas/MDM/Views/Countries/Index.cshtml`
   - `Views/{AreaName}/{ModuleName}/Index.cshtml` sayfasını oluştururken SADECE bu şablonu kopyala.
   - **Localization marker class** oluştur: `Views/{AreaName}/{ModuleName}/{ModuleName}Index.cs`
     - Class adı = `{ModuleName}Index`
     - **Namespace = `Diten.Web.Views.{AreaName}.{ModuleName}`** (❌ `Diten.Web.Areas.{AreaName}.Views.{ModuleName}` DEĞİL)
     - Bu adın `.resx` dosya adlarıyla birebir eşleşmesi ZORUNLUDUR.
   - `Views/{AreaName}/{ModuleName}/_Filter.cshtml` partial view'ını oluştur.
   - `wwwroot/assets/js/{AreaName}/{ModuleName}/index.js` dosyasını `DtDefaults.create()` ve Module Pattern (IIFE) ile oluştur. Bakınız: `.antigravity/rules/frontend-js-standard.md`
   - **[ZORUNLU]** `colReorder: { columns: ':gt(1):not(:last-child)' }` DataTable config'e eklenmelidir (standart kolon yapısı için varsayılan; bkz. `frontend-js-standard.md §11`). `column-reorder.dt`/`columns-reordered.dt` event'leri dirty-state hesabına bağlanmalıdır.
   - `_LayoutBackbone` içine menü linkini ekle ve aktif state için `ViewContext.RouteData` dinamik kontrolü yap.
   - **Edit link formatı:** `/{ModuleName}/Edit/{id}` (Area prefix OLMADAN). ❌ `/{AreaName}/{ModuleName}/Edit/{id}` YANLIŞTIR.

4a. **Phase 4a: CRUD Alt Sayfaları (ZORUNLU)**
   - **Bu adım atlanamaz.** Index (liste) sayfası yapıldıktan sonra aşağıdaki CRUD sayfaları da oluşturulmalıdır:
   - `Views/{AreaName}/{ModuleName}/Create.cshtml` → `add-page.md §B Form Sayfası` şablonunu kullan.
   - `Views/{AreaName}/{ModuleName}/Details.cshtml` → `add-page.md §C Details Sayfası` şablonunu kullan.
   - Edit sayfası ayrı sayfa olabilir (`Edit.cshtml`) veya Details içinde edit modu olabilir — modülün karmaşıklığına göre karar ver.
   - **⚠️ Rebuild Guard:** Mevcut bir modül yeniden yapılırken Create/Edit/Details sayfaları silinirse **aynı çalışmada** yeniden yapılmak ZORUNDADIR. "Sadece Index'i düzelt" yorumu bu sayfaları silmeye izin vermez.

4.5. **Phase 4.5: Browser Smoke Test (ORKESTRATÖR — ZORUNLU)**
   - **Bu adım atlanamaz.** Sayfa teslim edilmeden önce agent browser'da sayfayı açarak aşağıdaki kontrolleri yapar:
     - [ ] Sayfa yükleniyor mu? (Login redirect olmadan)
     - [ ] DataTable toolbar render ediliyor mu? (Search, Export, Add New butonları)
     - [ ] Localization key'leri çözümleniyor mu? (Raw key görünmüyor mu?)
     - [ ] Console'da JS hatası yok mu?
     - [ ] Tablo boşsa "No records" mesajı düzgün gösteriliyor mu?
   - Herhangi bir madde başarısızsa → Phase 4'e geri dön ve düzelt.

5. **Phase 5: Kalite & Güvenlik (testing-agent & security-agent & code-quality-agent)**
   - xUnit testlerini yaz (Tenant isolation check).
   - `/tenant-audit` komutunu çalıştırarak sızıntı kontrolü yap.
   - `code-quality-agent` → İsimlendirme, dosya yapısı ve standart denetimi yap.

6. **Phase 6: Dokümantasyon (documentation-writer & user-manual-generator)**
   - `documentation-writer` → Yeni modülün API dokümanlarını (Swagger/README) güncelle.
   - `user-manual-generator` → Son kullanıcı kılavuzunu hazırla (modülün ekranları, alanları, adım adım rehber).
   - ⛔ **BLOCKER:** Bu faz atlanamaz. Orchestration Report'ta "Dokümantasyon yazıldı" işaretlenmeden modül **kapanmaz**. `documentation-writer` ve `user-manual-generator` tamamlanmadan "teslim edildi" denilmez.

## ⚖️ Altın Kurallar
- **Sıfır İnisiyatif Kuralı:** Ajan, standart Liste/CRUD (DataTable) sayfaları için arayüz uyduramaz, kesinlikle Master Template'i kullanmak zorundadır.
- Modül mutlaka `MDM/` (veya ilgili Area) klasörü altında olmalıdır.
- Soft Delete ve TenantId filtrelemesi asla atlanamaz.
- Details/Edit sayfaları `add-page.md §B/§C` şablonları ile yapılır; Sneat PRO standartlarına ve 3'lü kart düzenine sadık kalınır. Bu sayfalar opsiyonel değil, modülün zorunlu parçalarıdır.