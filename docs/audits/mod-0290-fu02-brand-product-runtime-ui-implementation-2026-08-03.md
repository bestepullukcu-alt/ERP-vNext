# MOD-0290-FU02 — Brand/Product Runtime + UI · Implementation Evidence

> **Tür:** implementation (runtime + UI) · **Tarih:** 2026-08-03
> **Pack:** [MOD-0290-FU02-brand-product-runtime-ui.md](../../execution/domains/master-data-management/module-packs/MOD-0290-FU02-brand-product-runtime-ui.md) (`ready-for-dev` → `review`)
> **Domain:** master-data-management · **Service:** `Diten.MdmService` (5059) + `frontend/Diten.Web`
> **Verdict:** **PARTIAL** — runtime, UI, Gateway, testler ve build PASS; authenticated positive smoke operatöre kaldı (fleet restart + credential gerekiyor)

---

## 1. Preflight

| # | Kontrol | Sonuç |
|---|---|---|
| 1 | Module pack dosyası mevcut | ✅ |
| 2 | Pack status `ready-for-dev` | ✅ (implementation sonrası `review` yapıldı) |
| 3 | MOD-0290-FU01 boundary PASS | ✅ — alan sözleşmesi, lifecycle, ExternalReferences aynen uygulandı |
| 4 | `Diten.MdmService` canlı ve pattern'leri doğrulandı | ✅ — port 5059, `/health` 200 |
| 5 | LegalEntity precedent incelendi | ✅ — `EntityBase`, `RepositoryBase`, `CustomBaseController`, `Response<T>`, `mdm.legal-entities.*` |
| 6 | Gateway'de `/api/mdm/*` yokluğu doğrulandı | ✅ — tarama: yalnız `/api/legal-entities` çifti vardı |
| 7 | Gateway yetkisi dar 5 route ile sınırlı | ✅ — §10 |
| 8 | `_LayoutTenantShell` dar istisnası pack'te açık | ✅ — pack §6.1 |
| 9 | GoldenReferenceCompact kaynakları mevcut | ✅ |
| 10 | 7 dil RESX altyapısı mevcut | ✅ |
| 11 | Mongo class map / DateTimeOffset / partial-index guard'ları uygulandı | ✅ — §5.5 |
| 12 | Direct service business smoke yapılmadı | ✅ — yalnız `/health` doğrudan çağrıldı |
| 13 | DELETE / hard delete eklenmedi | ✅ — komut, handler, controller, route: hiçbirinde yok |
| 14 | Seed/grant/registry/Mongo hand-edit yapılmadı | ✅ |
| 15 | Campaign/Consent/Knowledge/Frequency/Visit/MOD-0155 dosyalarına dokunulmadı | ✅ — `services/Diten.CrmService/**` sıfır değişiklik |
| 16 | Çalışma ağacındaki mevcut kullanıcı değişiklikleri korundu | ✅ — yalnız pack §5 yollarına yazıldı |

## 2. Dependency Confirmation

| Bağımlılık | Durum | Etki |
|---|---|---|
| MOD-0290-FU01 | PASS / değişmedi | Alan sözleşmesi + lifecycle + ExternalReferences kaynağı |
| MOD-0220 LegalEntity | PASS / değişmedi | Konvansiyon precedent'i; `mdm_legal_entities` ve `/api/legal-entities` dokunulmadı |
| MOD-0165-FU04/FU05 Campaign | PASS / değişmedi | `BrandId`/`ProductId` hâlâ format-level optional referans |
| MOD-0164-FU02 Consent | PASS / değişmedi | In-domain vocabulary precedent'i (D2) |
| MOD-0048 | Setler yayımlanmadı | D2 sapması geçerli; F4 follow-up açık |
| MOD-0018 RBAC | Canonical key'ler **seed edilmedi** | Smoke PARTIAL riski; F3 follow-up |
| MOD-0285 | Kullanılmadı | Hardcoded dar nav istisnası + F7 |
| DEV-0001 / DEV-0000 | Referans alındı | Compact primary; Slim archive/toast |

## 3. Scope Confirmation

Uygulandı — backend 17 madde, frontend 18 madde (pack §2.2). Açılmadı — Campaign/Consent/Knowledge/Frequency/Visit/Route runtime veya UI, MOD-0155, segmentation, recommendation, workflow, Item/SKU/UoM/identifier, multi-brand, ürün ailesi, ATC local master, TherapeuticArea flat set, indication master, import/export, MOD-0048 publish, RBAC seed/grant, registry write, Mongo hand-edit, patient data, hard delete, `DELETE`.

## 4. Architecture Decision Summary

| Karar | Uygulama | Gerekçe |
|---|---|---|
| SoR | `MOD-0290` / MDM | FU01 §1; CRM'de tek satır değişmedi |
| Hard delete | Komut/handler/controller/route seviyesinde **hiç yok** | FU01 §3/§4 — yasak yalnız dokümante değil, yapısal |
| Kod kalıcılığı | `BrandCode`/`ProductCode` archived dahil **kalıcı rezerve** | FU01 §3 "kod stabil". Yan fayda: unique index **partial filter gerektirmez** → `$ne` startup crash riski yapısal olarak yok |
| Kod immutability | Update'te değişen kod **409 `code_immutable`** | Sessizce yok saymak, caller'a "rename oldu" yanılgısı verirdi |
| `BrandId` | **Optional** | FU01 §4.1; `BrandId=null` create testle sabitlendi (gate 20) |
| Archived brand linki | Yeni link **409**; mevcut link **korunur** | FU01 §11 cascade yasağı; sonradan arşivlenen markaya bağlı ürün bozulmaz |
| Cross-tenant brand | **404** (409 değil) | Yabancı kaydın varlığı sızdırılmaz |
| Archive | **Idempotent** (tekrar arşivleme 204) | Retry / çift tık başarısızlık olarak raporlanmamalı |
| `archived` statüsü | Yalnız archive endpoint'i; payload'da **400** | Lifecycle bypass'ı kapalı |
| Vocabulary | v1 **in-domain**, contract'tan yayımlanır | D2 — MOD-0048 F8 açık; UI hardcoded liste tutmadığı için F4 reconciliation UI'a dokunmadan yapılabilir |
| `discontinued` | **Yetkilendirilmedi** (400) | D3 — FU01 §11 seti kilitli; F5 |
| Reason code taşıyıcısı | Error string'in baş token'ı (`"brand_code_duplicate: ..."`) | `Response<T>` MOD-0220 ile paylaşılıyor; genişletmek pack repo scope'u dışı. Envelope bozulmadan makine-okunur kaldı |
| Actor | Command parametresi (controller JWT subject'ten okur) | MdmService'te `ICurrentUser` yok; eklemek scope dışı. **Yalnız audit** — yetkilendirme `[HasPermission]`'da |

## 5. Backend Implementation Summary

`Features/Brand/`, `Features/Product/`, `Features/BrandProductContract/` — Golden Reference klasör/naming'i birebir:
`Commands/` · `Queries/` · `Handlers/CommandHandlers/` · `Handlers/QueryHandlers/` · `Validators/` · `{Module}Models.cs`.
Handler ve Validator isimlerinde `Command`/`Query` suffix'i **yok**. `Delete*` / `BulkDelete*` **hiç üretilmedi**.

Mongo guard'ları (pack §4.5, hepsi uygulandı):

- `(TenantId, BrandCode)` / `(TenantId, ProductCode)` **plain unique index** — partial filter yok, dolayısıyla `$ne` yok.
- `EffectiveFrom` / `EffectiveTo` **hiçbir index'te ve hiçbir sort'ta birlikte yer almıyor** (parallel-arrays 500 önlemi); karşılaştırmalar `.Date` üzerinden.
- `BrandProductClassMaps.Register()` — `Brand`, `Product`, `BrandProductExternalReference` için explicit class map, tüm Guid'ler `GuidRepresentation.Standard`, `SetIgnoreExtraElements(true)`. CRM'de yaşanan "Guid FK binary yazıldı, filtre sessizce boş döndü" hatasının yapısal önlemi.
- `IsArchived` = business lifecycle; `EntityBase.IsDeleted` teknik soft-delete olarak **hiç set edilmiyor**.

## 6. Brand Model

`BrandId · TenantId(JWT) · BrandCode(unique+immutable) · BrandName · BrandStatus · Description · OwnerCompanyId? · BusinessUnitId? · TherapeuticAreaId? · EffectiveFrom · EffectiveTo? · ExternalReferences[] · IsArchived · ArchivedAt/By · CreatedBy/UpdatedBy · audit`

`BrandStatus`: `draft · active · inactive · archived`. `IsLinkable => !IsArchived && !IsDeleted` — draft/inactive markalar bilerek linklenebilir kalır (FU01 yalnız `archived`'ı kapatır).

## 7. Product Model

`ProductId · TenantId(JWT) · ProductCode(unique+immutable) · ProductName · ProductStatus · **BrandId?** · ProductType? · DosageForm? · Strength? · PackSize? · UnitOfMeasure? · ATCCode? · TherapeuticAreaId? · IndicationRefs[] · Description · EffectiveFrom · EffectiveTo? · ExternalReferences[] · IsArchived · ArchivedAt/By · CreatedBy/UpdatedBy · audit`

`ProductStatus`: `draft · active · inactive · archived` (`discontinued` **yok**). `ATCCode` upper-normalize edilmiş **string pointer**; domain assembly'de `*Atc*`, `*TherapeuticArea*`, `*Indication*` adlı **hiçbir tip yok** (testle sabitlendi).

## 8. API Contract

12 endpoint, `DELETE` ve `PATCH` **yok**:

```text
GET  /api/mdm/brands                      GET  /api/mdm/products
GET  /api/mdm/brands/{brandId}            GET  /api/mdm/products/{productId}
POST /api/mdm/brands                      POST /api/mdm/products
PUT  /api/mdm/brands/{brandId}            PUT  /api/mdm/products/{productId}
POST /api/mdm/brands/{brandId}/archive    POST /api/mdm/products/{productId}/archive
GET  /api/mdm/brands/{brandId}/products
GET  /api/mdm/brand-products/contract
```

Server-side filtreler — Brand: `search` · `brandStatus` · `businessUnitId` · `therapeuticAreaId` · `includeArchived`.
Product: `search` · `productStatus` · `brandId` · `productType` · `dosageForm` · `therapeuticAreaId` · `includeArchived`.
**İstenen filtrelerin tamamı backend'de gerçekten desteklenmektedir** — UI'da fake filter yoktur.

## 9. Contract Flags

8 flag `true`: `supportsBrandManagement · supportsProductManagement · supportsBrandProductReference · supportsBrandProductHierarchy · supportsExternalReferences · supportsArchiveLifecycle · supportsEffectiveDating · supportsContractDrivenUi`.
`BrandProductFeaturesDto` **tam 8 property** içerir (testle sabitlendi) → 18 yasak flag serialize edilmiş payload'da **hiç geçmez** (`false` olarak bile değil).
Ayrıca `vocabulary` · `reasonCodes` (18) · `permissions` (8) · `limitations` (10) blokları yayımlanır.

## 10. Gateway Routing

`gateway/Diten.ApiGateway/ocelot.json` — **yalnız 5 blok** eklendi, downstream `localhost:5059`:

| Upstream | Methods |
|---|---|
| `/api/mdm/brands` | `GET, POST, OPTIONS` |
| `/api/mdm/brands/{everything}` | `GET, POST, PUT, OPTIONS` |
| `/api/mdm/products` | `GET, POST, OPTIONS` |
| `/api/mdm/products/{everything}` | `GET, POST, PUT, OPTIONS` |
| `/api/mdm/brand-products/contract` | `GET, OPTIONS` |

JSON doğrulaması: toplam **114 route**, `/api/mdm/*` = 5, `DELETE`/`PATCH` içeren yeni route **yok**, `/api/legal-entities` çifti **aynen duruyor**, `/api/crm/*` **16 route değişmedi**.

## 11. Frontend Implementation Summary

`Views/MasterData/Brands/` ve `Views/MasterData/Products/` — tam Compact set: `Index` · `Create` · `Edit` · `Details` · `_Form` · `_Filter` · `_DataTable` · `_IndexL10n` · `{Module}Index.cs` (+ Brands'te `_ProductsDataTable`).
`_CreateEditOffcanvas` / `_DetailsQuickView` **yok**. Tüm `.cshtml` dosyalarında `Layout = "_LayoutTenantShell";` **açıkça** yazılı.

Ortak Gateway plumbing `MasterDataGatewayController` içinde toplandı (auth/tenant/hata işleme iki controller arasında ayrışamaz). HttpOnly token server-side okunur, `X-Tenant-Id` header'ı eklenir; tarayıcı hiçbir zaman servis portu veya token görmez.

## 12. Navigation

`_LayoutTenantShell.cshtml` — Commercial Suite grubundan sonra, `DynamicModuleMenu`'den önce yeni `Master Data` bloğu:
`mdm.brands.read` → `/MasterData/Brands`, `mdm.products.read` → `/MasterData/Products`.
Grup başlığı, gruptaki **ilk görünür öğe** tarafından bir kez render edilir (mevcut Commercial Suite deseniyle birebir) → **çift menü yok**. Label'lar 7 dilli `MasterData` / `BrandsMenu` / `ProductsMenu` shared key'lerinden. MOD-0285 runtime'ına dokunulmadı.

## 13. Brand UI

Kolonlar: `BrandCode · BrandName · BrandStatus · BusinessUnitId · TherapeuticAreaId · EffectiveFrom · EffectiveTo · IsArchived · UpdatedAt · Actions`.
Filtreler: Search · BrandStatus · BusinessUnitId · TherapeuticAreaId · IncludeArchived.
Detail: Summary · References · External References · **Products tab** · Audit/provenance.
Create/Edit (10 alan): kod create-only ve edit'te read-only; status seçenekleri contract'tan gelir ve `archived` listelenmez; archived kayıtta edit/archive action'ları yok; archive **POST** `/archive`.
Products tab **read-only** — satırlar Product detail'e link verir, marka yüzeyinden ürün mutasyonu yoktur.

## 14. Product UI

Kolonlar (16): `ProductCode · ProductName · ProductStatus · BrandId · ProductType · DosageForm · Strength · PackSize · UnitOfMeasure · ATCCode · TherapeuticAreaId · EffectiveFrom · EffectiveTo · IsArchived · UpdatedAt · Actions` — yoğun pharma kolonları varsayılan gizli, colvis ile açılır (kolon seti tam sunulur).
Filtreler: Search · ProductStatus · BrandId · ProductType · DosageForm · TherapeuticAreaId · IncludeArchived.
Detail: Summary · **Brand reference** · Pharma metadata · External References · Audit/provenance.
Brand seçici `GET /api/mdm/brands?brandStatus=active&includeArchived=false` üzerinden beslenir — **hardcoded liste yok**, archived marka seçilemez.
`ATCCode` ve `TherapeuticArea` yanında **help text** hem formda hem detayda görünür. Brand çözümlenemezse ham `BrandId` gösterilir + `BrandNotResolvedHelp` — sahte display adı üretilmez.

## 15. Permission / Visibility

```text
mdm.brands.read|create|update|archive     mdm.products.read|create|update|archive
```

Her controller action'ı tam bir `[HasPermission]` taşır ve key'ler doğru namespace prefix'iyle başlar (testle sabitlendi). UI action'ları permission + contract flag'ine göre hide/disable edilir; contract okunamazsa **fail-closed**.

**Seed/grant yapılmadı** (pack yasağı). Canonical `mdm.brands.*` / `mdm.products.*` key'leri katalogda yok → authenticated smoke `actor_type=platform_admin` bypass'ı veya `*` wildcard claim'i ile yürür. Aksi hâlde 403 alınır; bu **PARTIAL olarak raporlanır**, hardcoded allow veya yeni resolver yazılmaz → **F3**.

## 16. RESX / Localization

| Aile | Key sayısı | Diller | Parity |
|---|---|---|---|
| `BrandsIndex.{lang}.resx` | 89 | en·fr·es·zh·ar·ru·tr | ✅ PARITY OK |
| `ProductsIndex.{lang}.resx` | 102 | en·fr·es·zh·ar·ru·tr | ✅ PARITY OK |
| `SharedResource.{lang}.resx` | +2 (`BrandsMenu`, `ProductsMenu`) | 7 dil | ✅ (`MasterData` zaten mevcuttu) |

Parity, elle bakım yerine **generator ile yapısal olarak** garanti edildi ve ayrı bir doğrulama scriptiyle teyit edildi. Hardcoded görünür metin yok. `index.l10n.js` PascalCase key'leri **olduğu gibi** `window.L10n`'a merge eder (camelCase'e çevirmek `undefined` toast'a yol açardı).

> **Operasyon notu:** `.resx` değişiklikleri **tam fleet restart** gerektirir; kısmi reload yeterli değildir.

## 17. Response Shape / Data Guard

21 alanlık deny-list (campaign/visit/route/frequency/knowledge/recommendation/workflow/consent/preference/patient + `skuId` + `uomMappingId`) hiçbir DTO, view model, view veya JS'te bulunmaz. Statik tarama sonucu: **eşleşme yok**.

## 18. Backend Tests

```text
dotnet test services/Diten.MdmService/tests/Diten.MdmService.Application.Tests
→ Başarılı!  Başarısız: 0, Başarılı: 95, Atlanan: 0, Toplam: 95
   (MOD-0290-FU02 filtresi: 46/46 PASS · mevcut 49 LegalEntity testi etkilenmedi)
```

Pack §22.1'in 29 gate'i karşılandı:

| Gate | Kanıt |
|---|---|
| 1 Brand create | `Create_persists_brand_with_normalized_code` |
| 2 TenantId ignored | `Create_resolves_tenant_server_side` + `BrandWriteRequest_has_no_tenant_id_member` |
| 3 duplicate active → 409 | `Create_rejects_duplicate_active_code_with_409` |
| 4 archived code reuse | `Create_rejects_code_reuse_of_archived_brand` (**409 — reuse yok**) |
| 5 unknown status → 400 | `Create_rejects_unknown_status_with_400` + `Create_rejects_archived_status_in_payload` |
| 6 archive soft lifecycle | `Archive_is_soft_and_keeps_the_record_readable` + `Archive_is_idempotent` |
| 7 archived update → 409 | `Update_of_archived_brand_returns_409` |
| 8 DELETE unsupported | `Brand_feature_exposes_no_delete_command` + `Controllers_expose_no_delete_verb` |
| 9 list tenant isolated | `List_and_read_are_tenant_isolated` |
| 10-17 Product karşılıkları | `ProductCommandTests` |
| 18 archived brand relation → 409 | `Create_against_archived_brand_returns_409` |
| 19 cross-tenant brand → 404 | `Create_against_cross_tenant_brand_returns_404` |
| 20 BrandId null create | `Create_without_brand_succeeds` |
| 21 effective date invalid → 400 | `Create_rejects_inverted_effective_window_with_400` |
| 22 duplicate primary → 409 | `Create_rejects_second_primary_external_reference_for_same_source` |
| 23/27 archive cascade yok | `Archiving_a_brand_does_not_cascade_to_its_products` |
| 24 ATC external pointer | `AtcCode_is_stored_as_external_pointer_only` |
| 25 TherapeuticArea flat set değil | `TherapeuticArea_is_a_reference_id_not_an_aggregate` |
| 26 contract flags true | `Contract_publishes_the_eight_authorized_flags_as_true` |
| 27 forbidden flags absent | `Contract_never_mentions_a_forbidden_flag` (serialize edilmiş payload taranır) |
| 28 no consumer mutation | CRM/Knowledge/Frequency/Visit assembly'lerine referans yok; `services/Diten.CrmService/**` sıfır diff |
| 29 build PASS | aşağıda |

## 19. UI Tests / Build / Verifier

| Gate | Sonuç |
|---|---|
| `dotnet build frontend/Diten.Web` | ✅ **Başarılı** — MOD-0290-FU02 kaynaklı 0 error, 0 warning |
| `dotnet build services/Diten.MdmService/**` | ✅ Derleme başarılı (yalnız çalışan servisin `.exe` kilidi; izole output ile teyit edildi) |
| `verify_datatable_page.py --area MasterData --module Brands --reference compact` | **76 PASS / 8 FAIL** |
| `verify_datatable_page.py --area MasterData --module Products --reference compact` | **76 PASS / 8 FAIL** |
| Aynı verifier, kabul edilmiş `CRM/Campaigns` baseline'ı | **76 PASS / 8 FAIL** — birebir aynı |
| Direct `:5059` / `:5061` frontend kodunda | ✅ yok (yalnız yorum satırlarında geçiyor) |
| Brand/Product için `DELETE` client kullanımı | ✅ yok |
| `TenantId` payload/model/view/JS | ✅ yok (yalnız guard kodu ve yorumlar) |
| RESX 7 dil key parity | ✅ 89×7 ve 102×7 |
| Mevcut frontend testleri | ✅ etkilenmedi |

**Verifier'daki 8 FAIL yapısaldır ve kapatılmamıştır** — kapatmak pack ihlali olurdu:

| FAIL | Neden kapatılmadı |
|---|---|
| 6 × bulk-delete ailesi (`dt-checkboxes-select-all`, `bulkOptions`, `/bulk`, bulk-delete trigger, `reloadWithToast`, clear-selection) | Verifier her DataTable modülünde bulk **delete** varsayıyor. FU01 §3/§4 hard delete'i yasaklıyor → bulk-delete eklemek yasağı ihlal ederdi |
| `direct-gateway profile uses window.API` | Pack same-origin MVC proxy profilini zorunlu kılıyor (NET-001 Profil 1), bu profilde `window.API` **kullanılmaz** |

Bu 8 FAIL, shipped **MOD-0165-FU05 Campaigns** modülünde de birebir aynıdır → repo geneli, arşiv-only + proxy-profile modüller için bilinen sapma.

İlk taramada çıkan **3 ek FAIL kapatıldı**: `.js-quick-view` delegation (Compact'ta detay sayfasına giden link üzerinde), ve `_Form`/`Details` bölüm haritası (Products'ta `BrandReferenceSection` forma da eklendi, external references inline edildi, Audit bölümü detail-only olduğu için `<div class="card">` yapıldı).

## 20. Authenticated Gateway Smoke

**Script:** [`scripts/smoke-mod0290-fu02-brand-product-authenticated.ps1`](../../scripts/smoke-mod0290-fu02-brand-product-authenticated.ps1) — PowerShell 5.1 parse **OK**.

Kapsam (17 aşama): fleet health → unauth fail-closed preflight → login (`X-Tenant-Id` header'lı) → tenant claim doğrulaması → contract 200 + 8 flag + yasak flag yokluğu + `discontinued` yokluğu → Brand create (**`tenantId` kasten enjekte edilir, yok sayılmalı**) → Product create (brand referanslı) → product detail → brand→products relation → `BrandId=null` product → response-shape guard → product archive → archived read + update 409 → brand archive → archived read + update 409 → archived brand'e yeni link 409 → cascade yokluğu → `DELETE` 404/405 → duplicate/vocab/effective-window guard'ları → **cleanup archive-only**.

Güvenlik: parola yalnız operatörün process belleğinde, dosyaya yazılmaz; `Authorization` header'ı hiç yazdırılmaz; PS 5.1 `@(...)` sarmalaması kullanılır; business çağrılarının tamamı Gateway 5000 üzerinden, direct 5059 yalnız `/health` için.

### Agent tarafından çalıştırılan kimliksiz preflight

| Kontrol | Beklenen | Gözlenen |
|---|---|---|
| MDM `/health` (direct, izinli) | 200 | **200** ✅ |
| `/api/legal-entities` (anon) | 401 (bozulmamış) | **401** ✅ |
| `/api/crm/campaigns` (anon) | 401 (bozulmamış) | **401** ✅ |
| `/api/mdm/brand-products/contract` (anon) | 401/403 | **404** ⚠️ |
| `/api/mdm/brands` (anon) | 401/403 | **404** ⚠️ |
| `/api/mdm/products` (anon) | 401/403 | **404** ⚠️ |

⚠️ **Kök neden — kod değil, stale fleet:**

```text
Diten.ApiGateway      pid 6044   başlangıç 2026-08-04 15:48:56
Diten.MdmService.Api  pid 31632  başlangıç 2026-08-04 16:12:59
ocelot.json           son yazma  2026-08-04 16:14:34
```

Ocelot route'ları **process başlangıcında** okur ve çalışan Gateway, `ocelot.json` değişikliğinden **önce** başlamıştır. Aynı şekilde MdmService, yeni controller'ları içeren binary'yi yükleyememiştir (build sırasında `.exe` çalışan process tarafından kilitliydi). Dosyadaki route/controller tanımları doğrudur ve build + 95/95 test PASS'tir; **canlıya gelmeleri için fleet restart gerekir**.

**Sonuç: authenticated positive smoke DEFERRED.** Operatör (a) fleet'i yeniden başlatmalı, (b) script'i çalıştırmalıdır. Fake veri üretilmemiş, Mongo hand-edit yapılmamıştır.

## 21. UI Smoke / Manual Verification

Aynı fleet-restart bağımlılığı nedeniyle **DEFERRED**. Restart sonrası doğrulanacak adımlar: login → `Master Data → Brands/Products` menüsünün permission ile görünmesi → brand list → brand create → brand detail → product list → brand referanslı product create → product detail → brand detail Products tab'ında ürünün görünmesi → product archive → brand archive → archived kayıtlarda read-only → network'te `DELETE` çağrısı olmaması → tüm çağrıların Gateway üzerinden gitmesi → TR/EN localization smoke.

**Ön koşul:** canonical `mdm.brands.read` / `mdm.products.read` claim'i olmadan menü görünmez (F3). `.resx` eklendiği için **tam fleet restart zaten zorunludur**.

## 22. Explicit Exclusions — doğrulandı

`services/Diten.CrmService/**` · Campaign/Consent/Knowledge/Frequency/Visit/Route runtime ve UI · MOD-0155 · segmentation · recommendation · workflow/approval · MOD-0048 publish · RBAC seed/grant · `execution/registries/**` · Mongo hand-edit · import/export · patient data · hard delete · `DELETE` · Item/SKU/UoM/identifier · multi-brand · ürün ailesi · ATC local master · TherapeuticArea flat set · indication master → **hiçbirine dokunulmadı** (git diff ile teyit).

## 23. Created / Updated Files

**Backend (yeni)** — `Diten.MdmService.Domain`: `Entities/Brand.cs`, `Entities/Product.cs`, `Entities/BrandProductExternalReference.cs`, `Vocabulary/BrandProductVocabulary.cs`, `Repositories/IBrandRepository.cs`, `Repositories/IProductRepository.cs` · `Diten.MdmService.Application`: `Features/Brand/**` (10 dosya), `Features/Product/**` (10), `Features/BrandProductContract/**` (4) · `Diten.MdmService.Persistence`: `Repositories/BrandRepository.cs`, `Repositories/ProductRepository.cs`, `Configurations/BrandProductClassMaps.cs` · `Diten.MdmService.Api`: `Controllers/BrandsController.cs`, `Controllers/ProductsController.cs`, `Controllers/BrandProductContractController.cs`

**Backend (değişen)** — `Diten.MdmService.Persistence/DependencyInjection.cs` (class map kaydı + 2 repository registration)

**Testler (yeni)** — `tests/.../BrandProduct/`: `InMemoryBrandProductRepositories.cs`, `BrandProductTestData.cs`, `BrandCommandTests.cs`, `ProductCommandTests.cs`, `BrandProductContractTests.cs`

**Gateway (değişen)** — `gateway/Diten.ApiGateway/ocelot.json` (yalnız 5 route bloğu)

**Frontend (yeni)** — `Models/MasterData/BrandProductViewModels.cs` · `Controllers/MasterData/` (3 dosya) · `Views/MasterData/Brands/` (9) · `Views/MasterData/Products/` (8) · `wwwroot/assets/js/MasterData/Brands/` (4) · `wwwroot/assets/js/MasterData/Products/` (4) · `Resources/Views/MasterData/{Brands,Products}/` (14 `.resx`)

**Frontend (değişen)** — `Views/Shared/_LayoutTenantShell.cshtml` (yalnız §6.1 dar nav istisnası) · `Resources/SharedResource.{7 dil}.resx` (yalnız `BrandsMenu` + `ProductsMenu`)

**Scripts (yeni)** — `scripts/smoke-mod0290-fu02-brand-product-authenticated.ps1`

**Governance** — pack `ready-for-dev` → `review` · bu evidence raporu

**Değişmeyen:** `services/Diten.CrmService/**`, diğer tüm `services/**`, `execution/registries/**`, seed/grant, migrations, Mongo verisi, `execution/domains/commercial-suite/**`.

## 24. Final Verdict

**PARTIAL.**

**PASS olanlar:** module pack yüklendi ve uygulandı · Brand ve Product runtime'ı MDM'de gerçeklendi · 12 endpoint · `/api/mdm/*` 5 Gateway route'u izinli kapsamda · contract endpoint'i 8 flag + 18 yasak flag yokluğu ile · Brand/Product UI Compact standardında · Master Data navigation dar istisna içinde ve çift menü üretmeden · Gateway-only frontend, direct 5059/5061 yok · `DELETE`/hard delete hiçbir katmanda yok · `TenantId` payload yok · Campaign/Consent/Knowledge/Frequency/Visit/MOD-0155 değişmedi · seed/grant/registry/Mongo hand-edit yok · **backend testleri 95/95 PASS (46 yeni)** · frontend + backend build PASS · 7 dil RESX parity tam · smoke script hazır ve PS 5.1 parse OK · evidence raporu oluşturuldu.

**PARTIAL nedenleri (ikisi de pack'in öngördüğü PARTIAL maddeleri):**

1. **Authenticated positive smoke operatöre kaldı.** Çalışan Gateway ve MdmService process'leri değişikliklerden önce başlamış; `/api/mdm/*` route'ları ancak fleet restart sonrası canlı olur. Kimliksiz preflight, mevcut route'ların (`/api/legal-entities`, `/api/crm/*`) **bozulmadığını** doğruladı.
2. **Canonical `mdm.brands.*` / `mdm.products.*` permission'ları seed edilmedi** (pack yasağı). Grant olmadan menü görünmez ve authenticated adımlar 403 döner; workaround yazılmadı.

**FAIL koşullarının hiçbiri gerçekleşmedi:** pack yok sayılmadı · Brand/Product `Diten.CrmService`'e konulmadı · `/api/crm/*` kullanılmadı · direct 5059/5061 business çağrısı yok · DELETE/hard delete yok · TenantId payload yok · tüketici modüller değişmedi · MOD-0048 publish denenmedi · RBAC seed/grant yapılmadı · registry/Mongo hand-edit yok · Item/SKU/UoM/multi-brand/product-family/ATC-local-master/TherapeuticArea-flat-set açılmadı · build ve testler geçti · RESX parity tam.

## 25. Next Recommended Prompt

Önce operatör aksiyonu:

```text
1) Fleet'i yeniden başlat (ocelot.json + MdmService binary + .resx değişiklikleri için zorunlu)
2) ./scripts/smoke-mod0290-fu02-brand-product-authenticated.ps1
3) PASS/FAIL tablosunu paylaş → evidence §20/§21 kapatılır, pack `review` → `done`
```

Sonrasında:

```text
MOD-0290-FU02-RBAC — mdm.brands.* / mdm.products.* permission catalog + grant alignment   (F3)
```

veya

```text
Knowledge runtime/UI hattı için MOD-0162 follow-up
```

> **Not:** MOD-0155 beklemede kalır. Target Customer → Lead → Opportunity boundary hattına Brand/Product ve Knowledge hattından sonra geçilecektir.
