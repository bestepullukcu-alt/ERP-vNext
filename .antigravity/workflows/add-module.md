---
description: "WORKFLOW-000 — Yeni Modül Oluşturma Orkestrasyonu (Ana Senaryo)"
---

# /add-module - Yeni Modül Oluşturma

Bu workflow, bir modülün sıfırdan son kullanıcıya ulaşana kadarki tüm katmanlarını koordine eder. Ajan, bu adımları sırasıyla ve HİÇBİR inisiyatif almadan ZORUNLU olarak uygulamalıdır.

## 🎭 Görev Dağılımı (Orkestra)

0. **Phase 0: Onaylı Module Pack Kapısı (ORKESTRATOR)**
   - `AGENTS.md` dosyasını oku ve repo-level guardrail'leri uygula.
   - İlgili domain'i tespit et ve `execution/domains/{domain}/domain-config.md` dosyasını oku.
   - Hedef modül için module pack var mı kontrol et: `execution/domains/{domain}/module-packs/{ID}.md`
   - Module pack yoksa kod yazma; kullanıcıya önce `/prepare-module-pack` veya `module-pack-author` ile module pack hazırlatmasını söyle.
   - Module pack status `draft` ise kod yazma; kullanıcı incelemesi ve `approved` veya `ready-for-dev` status beklenir.
   - DataTable modülü ise module pack içinde `form_field_count` ve `golden_reference: slim|compact` kararını kontrol et.
   - Platform/Admin modülü ise `.antigravity/rules/platform-lookups-reference-data.md` dosyasını oku ve lookup dependency kararını module pack içinde doğrula.
   - Platform/Admin UI modülü ise `.antigravity/rules/platform-global-search-registry.md` dosyasını oku; Ctrl+K registry ve `en/tr` search localization kararını module pack/scope ile doğrula.
   - Yetki hiyerarşisini uygula:
     - `Module Pack > Domain Config > AGENTS.md > .antigravity/`
   - Çakışma varsa kullanıcı onayı al; onaysız fazlara geçme.

1. **Phase 1: Analiz (business-analyst)**
   - Onaylı module pack'teki alanları, IFRS/KVKK gereksinimlerini ve acceptance criteria'yı doğrula.
   - UI ve tablolarda kullanılacak anahtar kelimeleri (Keys) çıkar.
   - DataTable modülünde create/edit form alan sayısı değişirse geliştirmeye başlama; module pack güncellemesi için kullanıcıya geri dön.

1.5. **Phase 1.5: Mimari Doğrulama (ORKESTRATOR)**
   - **KRİTİK ADIM:** Kod yazmadan ÖNCE, üretilecek kodun taslağını kural dosyalarıyla kıyasla.
   - **Mimari Onay Zorunluluğu:** Orchestrator, aşağıdaki kontrol listesini her maddenin yanına `Plan: ...` cevabı koyarak DOLDURUR ve sonucu Orchestration Report'un "Mimari Onay (Phase 1.5)" satırına yapıştırır. Doldurulmuş tabloyu görmeden kullanıcı bu fazı onaylayamaz.

     | # | Kontrol | Cevap formatı |
     |---|---------|----------------|
     | 1 | Module pack'teki TÜM alanlar Entity'ye eklendi mi? | Evet/Hayır + alan listesi |
     | 2 | Alan isimleri global ERP standartlarına uygun mu? (örn `PlateCode → Code`) | Evet/Hayır + sapma listesi |
     | 3 | Repository izolasyon/Soft-Delete garantisi var mı? Tenant-owned ise TenantId filtresi, module pack'te açık global katalog istisnası varsa `GlobalEntity` + RBAC + `IsDeleted=false` filtresi doğrulandı mı? | Evet/Hayır + repo dosya yolu + izolasyon modeli |
     | 4 | Entity base type doğru mu? Tenant-owned modülde `BaseEntity`/TenantId; onaylı Platform global katalogda `GlobalEntity`. | Evet/Hayır + entity dosya yolu + gerekçe |
     | 5 | CQRS yapısı (Command, Query, Handler, Validator — her biri ayrı dosya) planlandı mı? | Evet/Hayır + planlanan dosya listesi |
     | 6 | DataTable ise `golden_reference` kararı doğru mu? (≤8 slim, >8 compact) | Slim/Compact + form alan sayısı |
     | 7 | Compact DataTable ise Create/Edit/Details logical section haritası planlandı mı? | Evet/Hayır + section listesi + `_Form.cshtml`/`Details.cshtml` parite notu |
     | 8 | Required alan kontratı Backend Validator + Web ViewModel + Razor + tracker için aynı mı? | Evet/Hayır + required alan listesi + opsiyonel nullable alan listesi + ilk açılış progress beklentisi |
     | 9 | Platform lookup dependency checked mi? Dropdown/filter/select/default alanları PSS `/api/lookups/{key}` kullanıyor mu, yeni lookup key pack'te açık mı, MDM/reference boundary korunuyor mu? | Evet/Hayır/Yok + endpoint listesi veya gerekçe |

   - **Onay Mekaniği:** Orchestrator, doldurulmuş tabloyu kullanıcıya `AskUserQuestion` ile (ya da CLI'da düz mesaj olarak) sunar ve "Onaylıyor musunuz?" sorusuyla bekler. **Kullanıcıdan açık `evet/onay/approved` cevabı alınmadan Phase 2'ye geçilemez.**
   - **Sapma Halinde:** Tek bir madde "Hayır" ise Phase 1'e dön, module pack ya da plan üzerinde düzelt; tabloyu yeniden doldur.

2. **Phase 2: Veri Mimarisi (data-agent & backend-architect)**
   - MongoDB koleksiyonunu tasarla. Varsayılan model tenant-owned `ITenantDocument`/`BaseEntity` tabanıdır.
   - Domain Entity ve Repository katmanını oluştur. Tenant-owned veride Soft Delete ve TenantId ZORUNLUDUR. Yalnızca module pack'te açıkça gerekçelendirilmiş Platform global kataloglarında `GlobalEntity` kullanılabilir; bu durumda repository `IsDeleted=false`, global unique index ve RBAC kontrollerini belgelemek zorundadır.

3. **Phase 3: İş Mantığı & Yerelleştirme (backend-architect & l10n-agent)**
   - `/add-endpoint-cqrs` akışını başlat (Request, Command, Handler, Validator).
   - API Controller'ı oluştur ve Ocelot Gateway rotasını ekle.
   - **ÖNCE `l10n-agent`:** `.antigravity/rules/localization-standard.md` kuralına göre modül tipine uygun (Platform: 2 dil, Tenant: 7 dil) `.resx` senkronizasyonunu tamamla.
   - Sadece projede desteklenen dillerde `.resx` dosyalarını oluştur.
   - **⚠️ RESX DOSYA ADI KURALI (KRİTİK):** `.resx` dosya adı, Razor view'da kullanılan localization marker class adıyla **birebir eşleşmelidir**. Eğer marker class `GoldenReferenceSlimIndex` ise dosya adı `GoldenReferenceSlimIndex.{lang}.resx` olmalıdır. `Index.{lang}.resx` KULLANILMAZ. Yol: `Resources/Views/{AreaName}/{ModuleName}/{MarkerClassName}.{lang}.resx`
     - Örnek: Class = `GoldenReferenceSlimIndex` → `GoldenReferenceSlimIndex.en.resx`, `GoldenReferenceSlimIndex.tr.resx`, ...
   - **Kritik:** Ortak kelimeleri (`Kaydet`, `Sil` vb.) `SharedResource`'tan al, yazma. Sayfaya özel olan başlıkları ve tablo kolon anahtarlarını ekle.
   - `.resx` dosyalarında `PageDescription` key'i her modül için tanımlanmalıdır. Alt başlıklar hardcoded yazılmaz.

3.5. **Phase 3.5: Gateway Doğrulama (integration-agent)**
   - **[KRİTİK]:** `.antigravity/rules/routes.md` dosyasını oku.
   - Portu hardcoded seçme; `AGENTS.md` port şeması ve domain-config'e göre hedef servisi belirle. DevEnablement için port `5058`, Platform için `5057`, Auth için `5056` kullanılır.
   - `ocelot.json` protected path'tir; yalnızca `integration-agent` route ekleyebilir. Orchestrator, backend veya frontend agent bu dosyayı değiştirmez.
   - Route eksikse bunu **BLOCKER/NOT** olarak Orchestration Report'a yaz; integration-agent phase'i tamamlanmadan modülü "tamamlandı" sayma.
   - integration-agent, `ocelot.json`'a yeni modül için **iki explicit rota** ekler:
     - `UpstreamPathTemplate: "/api/{resource}"` → `DownstreamPathTemplate: "/api/{resource}"`
     - `UpstreamPathTemplate: "/api/{resource}/{everything}"` → `DownstreamPathTemplate: "/api/{resource}/{everything}"`
   - Her iki rotaya da `UpstreamHttpMethod`: `["GET", "POST", "PUT", "PATCH", "OPTIONS", "DELETE"]` ekle.
   - Yeni rotaları ilgili catch-all rotalarından **ÖNCE** konumlandır.
   - Gateway rotası eklenmeden "İşlem tamam" denilmez.

4. **Phase 4: Arayüz (frontend-ui-ux)**
   - **[KRİTİK]:** `.antigravity/rules/frontend-datatable-template.md` dosyasını referans al.
   - Module pack'teki `golden_reference` kararını uygula:
     - `slim`: `8 ve altı` form alanı, Index içinde `_CreateEditOffcanvas.cshtml` ile create/edit.
     - `compact`: `8'den fazla` form alanı, ayrı `Create.cshtml`, `Edit.cshtml`, `Details.cshtml`, `_Form.cshtml`.
   - Required alan kontratını uygulamadan form teslim edilmez:
     - Backend validator `NotEmpty()`/`NotNull()` alanları UI label yıldızı ve Razor `required` attribute'u ile eşleşmelidir.
     - Opsiyonel alanlarda label yıldızı, `[Required]`, HTML `required` veya otomatik `data-val-required` üretecek non-nullable value type bulunamaz.
     - Opsiyonel numeric/date alanlar Web ViewModel ve save payload içinde nullable olmalıdır (`int?`, `decimal?`, `DateTime?`, vb.).
     - Create ekranı ilk açıldığında required progress değeri raporda açıklanır: payda gerçek required alan sayısı, pay ise yalnızca bilinçli default dolu required alan sayısıdır.
   - Compact modüllerde `Details.cshtml` ile `_Form.cshtml` için ortak logical section haritası üret:
     - Aynı section sayısı.
     - Aynı section başlıkları veya birebir anlam eşdeğeri.
     - Aynı alanların aynı section altında yer alması.
     - Details dört card/section ise Create/Edit de dört card/section olmalıdır; section birleştirme yasaktır.
   - **⚠️ `Areas/` KULLANILMAZ:** View dosyaları her zaman `Views/{AreaName}/{ModuleName}/` altına konur. `Areas/{AreaName}/Views/` yapısı ASP.NET Areas routing'dir ve projede KULLANILMAZ.
     - ✅ `Views/DevEnablement/GoldenReferenceSlim/Index.cshtml`
     - ❌ `Areas/DevEnablement/Views/GoldenReferenceSlim/Index.cshtml`
   - `Views/{AreaName}/{ModuleName}/Index.cshtml` sayfasını oluştururken SADECE bu şablonu kopyala.
   - **Localization marker class** oluştur: `Views/{AreaName}/{ModuleName}/{ModuleName}Index.cs`
     - Class adı = `{ModuleName}Index`
     - **Namespace = `Diten.Web.Views.{AreaName}.{ModuleName}`** (❌ `Diten.Web.Areas.{AreaName}.Views.{ModuleName}` DEĞİL)
     - Bu adın `.resx` dosya adlarıyla birebir eşleşmesi ZORUNLUDUR.
   - `Views/{AreaName}/{ModuleName}/_Filter.cshtml` partial view'ını oluştur.
   - `Views/{AreaName}/{ModuleName}/_DataTable.cshtml` partial view'ını oluştur; DataTable v2 marker, skeleton loader, checkbox ve action kolonları burada tutulur.
   - `Views/{AreaName}/{ModuleName}/_IndexL10n.cshtml` partial view'ını oluştur; JSON payload üretir.
   - `wwwroot/assets/js/{AreaName}/{ModuleName}/index.js` dosyasını `DtDefaults.create()` ve Module Pattern (IIFE) ile oluştur. Bakınız: `.antigravity/rules/frontend-js-standard.md`
   - **Frontend Proxy Contract (ZORUNLU):**
     - Platform/admin MVC modüllerinde API profili `proxy-profile`dır. JS endpoint'i `/{AreaName}/{ModuleName}/api` olur; browser JS `window.API.platform`, `window.ApiBaseUrl`, `document.cookie`, `access_token` veya `Authorization: Bearer` kullanmaz.
     - Frontend controller route'u ve JS endpoint'i birlikte tasarlanır. Controller en az `GET api`, `DELETE api/{id}`, `DELETE api/bulk` proxy action'larını içerir; module pack'te varsa `activate/deactivate` gibi action'lar da eklenir.
     - Proxy action'ları `Request.Cookies["access_token"]` değerini server-side okuyup Gateway çağrısına `Authorization: Bearer` olarak ekler ve `X-Tenant-Id` header'ını Platform/admin context'te göndermez.
     - Tenant/public shell modüllerinde `direct-gateway-profile` kullanılacaksa bu karar module pack veya orchestration report'ta açık yazılır ve `window.API.{service}` kullanılır.
   - **[ZORUNLU]** `colReorder: { columns: ':gt(1):not(:last-child)' }` DataTable config'e eklenmelidir (standart kolon yapısı için varsayılan; bkz. `frontend-js-standard.md §11`). `column-reorder.dt`/`columns-reordered.dt` event'leri dirty-state hesabına bağlanmalıdır.
   - Shell tipine göre `_LayoutPlatformAdmin` veya `_LayoutTenantShell` içine menü linkini ekle ve aktif state için `ViewContext.RouteData` dinamik kontrolü yap.
   - **Tenant Ctrl+K (data-driven, otomatik):** `shell: tenant` modüllerde ayrı Ctrl+K işi YOK — nav-visible self-registered sayfalar tenant aramasına **otomatik** dahil olur (arama tenant'ın data-driven nav'ından beslenir; bkz. `module-self-registration-standard.md §7`). Yalnız her nav-visible sayfaya anlamlı `DisplayName` + gerçek `RoutePath` ver; statik JSON güncellemesi gerekmez.
   - **Tenant nav yerelleştirmesi (ZORUNLU):** `shell: tenant` modül, sidebar + Ctrl+K adları için `Nav.Module.{ModuleCode}` + her nav-visible sayfaya `Nav.Page.{PageCode}` (+ yeni domain ise `Nav.Domain.{Domain}`) key'lerini **7 tenant dilinde** `SharedResource.{lang}.resx`'e eklemeli (l10n gate). Eksik key İngilizce default'a düşer (menü kırılmaz) ama tenant modülü için **defect**; guard: `NavL10nContractTests`. Kural: `module-self-registration-standard.md §8`.
   - **Platform Global Search Registry + Localization (Platform/Admin için ZORUNLU):**
     - `shell: platform-admin` veya `/Platform/...` route'u üreten kullanıcıya açık UI modülleri `.antigravity/rules/platform-global-search-registry.md` standardına göre Ctrl+K registry'ye eklenir.
     - Stable list/index route eklenir; kullanıcıya açık stable create route varsa eklenebilir.
     - Dynamic `{id}`/GUID isteyen detail/edit route'ları, internal API endpoint'leri, audit/docs/module-pack linkleri ve backend-only altyapılar eklenmez.
     - Search sonuçları iki dilde teslim edilir: `platform-search.en.json` ve `platform-search.tr.json`. `url`/`icon` çevrilmez; `name`/`group`/`keywords` çevrilir.
     - Eğer uygulama kodu henüz iki dilli registry dosyalarını yüklemiyorsa bu durum blocker olarak raporlanır; yalnız legacy `platform-search.json` güncellenerek madde tamamlanmış sayılamaz.
   - **Controller route + link formatı (BİRBİRİNE BAĞLI):**
     - `frontend/Diten.Web/Controllers/{ModuleName}Controller.cs` üzerine sınıf düzeyinde **standart**: `[Route("[controller]")]` veya literal `[Route("{ModuleName}")]` (örn `[Route("GoldenReferenceCompact")]`). **Area prefix yazma** (örn `[Route("DevEnablement/[controller]")]` YASAK).
     - Bu standart altında menü/link formatı **otomatik olarak** `/{ModuleName}/Edit/{id}`, `/{ModuleName}/Create`, `/{ModuleName}/Details/{id}` olur.
     - ❌ Yanlış: `/{AreaName}/{ModuleName}/Edit/{id}` — Controller route'unda area prefix olmadığı için 404 döner.
     - Frontend agent menü/link üretmeden önce `{ModuleName}Controller.cs` `[Route(...)]` attribute'unu okumalı ve link formatını oradan türetmelidir.

4a. **Phase 4a: CRUD Surface (Golden Reference'a Göre ZORUNLU)**
   - **Slim (`golden_reference: slim`):**
     - `Views/{AreaName}/{ModuleName}/_CreateEditOffcanvas.cshtml` zorunludur.
     - Create/Edit form alanları bu partial içinde olur.
     - Index içinde create/edit offcanvas kullanılır.
   - **Compact (`golden_reference: compact`):**
     - `Views/{AreaName}/{ModuleName}/Create.cshtml` → `add-page.md §B Form Sayfası`
     - `Views/{AreaName}/{ModuleName}/Edit.cshtml` → `add-page.md §B Form Sayfası`
     - `Views/{AreaName}/{ModuleName}/Details.cshtml` → `add-page.md §C Details Sayfası`
     - `Views/{AreaName}/{ModuleName}/_Form.cshtml` ortak form partial'ı
     - Index içinde create/edit offcanvas YASAKTIR.
     - `_Form.cshtml` ve `Details.cshtml` aynı logical section/card haritasını paylaşır. Alanlar daha az sayıda card altında toplanamaz; Create/Edit yüzeyi Details yüzeyiyle bilgi mimarisi olarak eşleşmeden Phase 4a tamamlanmış sayılmaz.
   - **⚠️ Rebuild Guard:** Mevcut bir modül yeniden yapılırken Slim/Compact surface parçaları silinirse aynı çalışmada geri yapılmak ZORUNDADIR.

4.5. **Phase 4.5: Runtime Smoke Test (ORKESTRATÖR — ZORUNLU)**
   - **Bu adım atlanamaz.** Sayfa teslim edilmeden önce orchestrator, aşağıdaki üç kanaldan **birini** seçip kontrolleri yürütür ve raporun "Runtime Smoke Test (Phase 4.5)" satırına seçilen kanalı + sonucu yazar:

     | Kanal | Kullanım | Çıktı |
     |---|---|---|
     | **A — MCP Browser** (varsa) | Orchestrator MCP tabanlı browser tool'una sahipse sayfayı yükler, console hatalarını ve toolbar render'ını doğrular. | Ekran görüntüsü + console log özeti |
     | **B — Playwright Script** | `frontend/Diten.Web/tests/smoke/{module}.spec.ts` altında smoke testi çalıştırır (yoksa testing-agent oluşturur). | Test pass/fail çıktısı |
     | **C — Kullanıcı Doğrulaması** | Otomasyon yoksa orchestrator, aşağıdaki checklist'i kullanıcıya yönlendirir ve onay cevabını bekler. | Kullanıcının evet/hayır + not yanıtı |

   - **Her üç kanalda ortak checklist:**
     - [ ] Sayfa yükleniyor mu? (Login redirect olmadan)
     - [ ] DataTable toolbar render ediliyor mu? (Search, Export, Add New, Filter, Save View)
     - [ ] Localization key'leri çözümleniyor mu? (Raw key görünmüyor)
     - [ ] Console'da JS hatası yok mu?
     - [ ] Tablo boşsa "No records" mesajı düzgün gösteriliyor mu?
     - [ ] Slim ise create/edit offcanvas açılıp kapanıyor mu? Compact ise `Create`/`Edit`/`Details` sayfaları yükleniyor mu?
     - [ ] Bulk delete onayı + silme akışı çalışıyor mu?
     - [ ] Platform/Admin modülü ise Ctrl+K `en` ve `tr` kültürlerinde açılıyor mu, yeni modül adı/grup/keyword araması sonuç döndürüyor mu ve sonuç doğru `/Platform/...` route'una gidiyor mu?
   - **Kanal A/B otomasyon başarısız ya da kullanılamıyorsa Kanal C zorunludur — orchestrator, browser kontrolünü uydurarak "yapıldı" diyemez.**
   - Herhangi bir madde başarısızsa → Phase 4'e geri dön ve düzelt.

5. **Phase 5: Kalite & Güvenlik (testing-agent & security-agent & code-quality-agent)**
   - xUnit testlerini yaz (Tenant isolation check).
   - `/tenant-audit` komutunu çalıştırarak sızıntı kontrolü yap.
   - `code-quality-agent` → İsimlendirme, dosya yapısı ve standart denetimi yap.

6. **Phase 6: Dokümantasyon ve Denetim (documentation-writer & user-manual-generator)**
   - `documentation-writer` → Yeni modülün API dokümanlarını (Swagger/README) güncelle.
   - `user-manual-generator` → Son kullanıcı kılavuzunu hazırla (modülün ekranları, alanları, adım adım rehber).
   - **Mimari Denetim (Audit Report):** Geliştirilen modülün standartlara uygunluğunu belgeleyen bir denetim raporu oluştur ve `/docs/audits/{module-name}-audit.md` adresine kaydet.
   - ⛔ **BLOCKER:** Bu faz atlanamaz. Orchestration Report'ta "Dokümantasyon ve Denetim tamamlandı" işaretlenmeden modül **kapanmaz**. `documentation-writer`, `user-manual-generator` ve Audit Report tamamlanmadan "teslim edildi" denilmez.

## ⚖️ Altın Kurallar
- **Sıfır İnisiyatif Kuralı:** Ajan, standart Liste/CRUD (DataTable) sayfaları için arayüz uyduramaz, kesinlikle Master Template'i kullanmak zorundadır.
- Modül mutlaka module pack'te belirtilen `Views/{AreaName}/{ModuleName}` klasörü altında olmalıdır.
- Soft Delete asla atlanamaz. Tenant-owned modüllerde TenantId filtrelemesi zorunludur; module pack'te açık Platform global katalog istisnası varsa TenantId yerine `GlobalEntity` gerekçesi, global index ve RBAC kontrolü doğrulanır.
- Details/Edit sayfaları Compact modüllerde zorunludur; Slim modüllerde create/edit offcanvas zorunludur.
- **⛔ SELF-REGISTRATION ZORUNLU (BLOCKER):** Her tenant-assignable modül bir `ModuleManifestProvider` ile gelmek ZORUNDADIR — modül kataloğa elle değil, KODDAN otomatik düşer. Manifest, modülün **gerçek frontend controller view-route'larını + UI satır/toolbar aksiyon menüsünü** birebir aynalar (sadece API değil). Wiring + iki-yönlü completeness testi şart. **RoutePath güvenlik taşır (§2c):** yetki scope'u (tenant/platform-admin escalation sınırı) sayfanın route'undan türetilir — platform-admin sayfaları `/Platform/…` route'unda olMALI, tenant sayfaları olMAMALI; yanlış route = yanlış scope = sızıntı/kilit. Tam kurallar: `.antigravity/rules/module-self-registration-standard.md`. Manifest + completeness testi yeşil olmadan modül **kapanmaz**.


---

## Module ID Canonicalization Gate (DCP-002)

The Blueprint (`docs/System Capability & Implementation Blueprint - master 7.xlsx` :: `Blueprint_Data`) is the canonical authority for every `MOD-xxxx` ID and canonical name. Before creating or reserving any `MOD-xxxx` (new module, FU/child, or reservation):

1. **Blueprint lookup** — the ID + canonical name must exist in `Blueprint_Data`, or the ID must be an FU/child of an existing Blueprint MOD parent.
2. **Registry collision** — it must not already map to a different capability in `execution/registries/module-id-registry.md`.
3. **Canonical-name validation** — the pack `name` must match the Blueprint canonical name (or an approved alias).
4. **Parent/FU/child decision** — decide explicitly whether the work is a new module or an FU/child of an existing module before minting an ID.
5. **Repo-only reservation** — a capability absent from the Blueprint requires an explicit Enterprise Architect reservation recorded in the registry; no placeholder or next-free ID may be invented.
6. **Preflight (fail-closed)** — run `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-XXXX --name "Canonical Name" [--parent MOD-YYYY] [--repo-only]`. A non-zero exit BLOCKS pack creation.

Authority and policy: `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`. Legacy (`PSS-*`, `NEW-*`) and repo-only IDs are valid only as deprecated aliases pending Enterprise Architect reservation.

### CAND-CAP candidate namespace (DCP-002)

When the Blueprint has no capability and no existing MOD/FU fits, use a temporary candidate identity `CAND-CAP-####` — a governance/documentation identity ONLY, never written into runtime literals. Validate with the fail-closed candidate gate:

`python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-#### --name "Capability Name"`

Lifecycle: `legacy ID → deprecated alias to CAND-CAP-#### → later deprecated alias to the EA-assigned canonical MOD-xxxx`. New-module identity rule: **Blueprint lookup → existing MOD or FU when available → otherwise CAND-CAP only → never invent a MOD / PSS / NEW identity.**
