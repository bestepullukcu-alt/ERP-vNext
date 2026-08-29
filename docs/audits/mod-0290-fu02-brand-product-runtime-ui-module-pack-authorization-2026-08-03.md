# MOD-0290-FU02 — Brand/Product Runtime + UI · Module Pack Authorization

> **Tür:** module pack authorization / scope definition — **implementation değildir**
> **Tarih:** 2026-08-03 · **Branch:** `feature/crm-integration` (pack yazımı; implementation branch'i ayrıdır)
> **Kimlik:** `MOD-0290-FU02 — Brand / Product Runtime + UI` (parent `MOD-0290 Product / Item / SKU Master`)
> **Verdict:** **PASS** — pack `ready-for-dev`

---

## 1. Preflight

| Kapı | Sonuç |
|---|---|
| DCP-002 `MOD-0290-FU02` | `OK  MOD-0290-FU02: proven against Blueprint/registry.` (exit 0, `--parent MOD-0290`) |
| Runtime kod yazıldı mı? | **Hayır** — `services/**`, `frontend/**`, `gateway/**` değişmedi |
| Seed / grant / registry / Mongo | **Hayır** |
| Değişen dosya sayısı | 3 (1 yeni pack · 1 yeni audit · 1 domain-config senkronizasyonu) |

**Okunan kaynaklar:** `AGENTS.md` · `.antigravity/rules/module-pack-standard.md` · `routes.md` (NET-001) ·
`permission-key-standard.md` (PKS-001) · `execution/domains/master-data-management/domain-config.md` ·
`execution/domains/commercial-suite/domain-config.md` · `crm-sor-boundary.md` ·
`MOD-0290-FU01-brand-product-master-boundary.md` + audit raporu · `MOD-0165-FU05-campaign-targeting-admin-ui.md` ·
MOD-0165-FU04 runtime evidence · MOD-0164-FU02 runtime evidence · MOD-0048 reference-set authoring/reconciliation
raporları · `gateway/Diten.ApiGateway/ocelot.json` · `services/Diten.MdmService/**` (LegalEntity precedent) ·
`services/Diten.CrmService/**` (feature envanteri) · `frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml` ·
`Views/DevEnablement/GoldenReferenceCompact/` · `Views/MasterData/LegalEntities/`.

## 2. Dependency Confirmation

| Bağımlılık | Durum | Bu task'taki etkisi |
|---|---|---|
| MOD-0290-FU01 Brand/Product Master Boundary | **PASS (kullanıcı kabulü)** | Alan sözleşmesi, lifecycle, ExternalReferences ve SoR kararı aynen uygulandı |
| MOD-0220 / `Diten.MdmService` | **Canlı** (port 5059, LegalEntity + Lookups) | Route, permission, `EntityBase` ve CQRS konvansiyonu buradan alındı |
| MOD-0165-FU04 Campaign runtime | **PASS** | `BrandId`/`ProductId` optional referans olarak kalır; **değiştirilmedi** |
| MOD-0165-FU05 Campaign Admin UI | **review** | Pack format şablonu ve navigation istisnası deseni referans alındı; **değiştirilmedi** |
| MOD-0164-FU02 Consent runtime | **PASS** | In-domain vocabulary precedent'i (D2) buradan geldi; **değiştirilmedi** |
| MOD-0048 reference data | Setler **yayımlanmadı** (FU01 F8 açık) | D2 sapması + F4 follow-up |
| MOD-0018 RBAC | Canonical key'ler **seed edilmedi** | F3 follow-up; smoke PARTIAL riski |
| MOD-0285 dynamic navigation | Mevcut | Kullanılmadı; hardcoded dar istisna + F7 migration follow-up |
| DEV-0001 / DEV-0000 Golden Reference | Mevcut | `compact` primary, `slim` archive/toast |

## 3. Scope Confirmation

Yetkilendirildi: Brand + Product aggregate · CRUD-minus-delete · archive lifecycle · list/detail/contract/relation
endpointleri · ExternalReferences · pharma metadata · tenant isolation · dar Gateway route · MasterData tenant-shell
UI (list/detail/create/edit/archive, Brand→Products tab, Product→Brand reference) · contract-driven UI ·
Gateway-only entegrasyon · Golden Compact/Slim · 7 dil RESX · testler · authenticated smoke · evidence.

Yetkilendirilmedi: Campaign/Consent/Knowledge/Frequency/Visit/Route runtime veya UI · MOD-0155 · segmentation ·
recommendation · digital detailing · workflow/approval · Item/SKU/UoM/identifier · multi-brand product · ürün
ailesi hiyerarşisi · ATC local master · TherapeuticArea flat set · Indication master · import/export engine ·
MOD-0048 publish · RBAC seed/grant · registry write · Mongo hand-edit · patient data · hard delete · `DELETE`.

## 4. Module Identity

```text
Module            : MOD-0290
Follow-up         : FU02
Title             : Brand / Product Runtime + UI
Domain            : master-data-management        ← FU01 SoR kararı (CRM değil)
Service           : Diten.MdmService (5059) + frontend/Diten.Web
Shell             : tenant  (_LayoutTenantShell)
Golden reference  : compact  (Brand 10 alan · Product 16 alan)
Entity base       : EntityBase (tenant-owned)
Primary surface   : Master Data → Brands · Master Data → Products
Status            : ready-for-dev
Branch            : feature/mdm/mod-0290-fu02-brand-product-runtime-ui
```

### 4.1 Yerleşim kararı — brief'ten sapma (kullanıcı onaylı)

Task brief'i `domain: commercial-suite`, `service: Diten.CrmService`, `/api/crm/*` ve `crm.brand-product.*`
istiyordu. Üç bağımsız governance artefaktı bunu **reddediyor**:

| Kaynak | İfade |
|---|---|
| MOD-0290-FU01 §1, Seçenek 2 | *"Commercial Suite altında CRM-adjacent Brand/Product boundary — ❌ CRM'de ikinci bir master doğurur; SoR matrisiyle çelişir"* |
| `commercial-suite/domain-config.md` (Out-of-Scope) | *"Brand / Product / SKU master → **MDM / Product**"* |
| `crm-sor-boundary.md` satır 31 | *"Brand / Product / SKU → MDM / Product · **read-only consume**"* |

Kullanıcıya üç seçenek sunuldu (MDM'de aç · CRM'de aç · CRM'de host et + SoR MDM kalsın) ve
**"MDM'de aç (FU01'e sadık)"** seçildi. Brief'in tüm scope, model, UI, test ve smoke maddeleri **aynen
korundu**; yalnız domain klasörü, servis, route prefix'i ve permission namespace'i MDM'e hizalandı.

## 5. Governance Need

- FU01 `runtime_code_allowed: false` idi; yalnız sahiplik/sınır kararı verdi.
- FU02 **yeni aggregate + yeni API + yeni Gateway route + yeni frontend navigation** açar → `AGENTS.md` §10
  uyarınca `approved`/`ready-for-dev` pack olmadan `@orchestrator` başlayamaz.
- `_LayoutTenantShell.cshtml` protected path'tir; menü girdisi için **dar, test edilebilir istisna** pack içinde
  verilmek zorundaydı (§6.1).
- MDM domain-config frontend ve gateway'i "ilk implementation slice dışında" tutmuştu; pack bu kısıtı **yalnız
  bu modül için** ve dar kapsamda kaldırır (Module Pack > Domain Config).

## 6. Brand/Product Ownership

```text
MOD-0290 Brand/Product Master, Brand ve Product için Source of Truth'tur.
```

Campaign · Knowledge · Frequency · Visit/Route Planning master **oluşturmaz**, yalnız `BrandId`/`ProductId`
referansı tutar. Master data başka aggregate içine **kopyalanmaz**. Historical references **korunur**. Archive
geçmiş kayıtları **silmez** ve **cascade etmez**. MOD-0155 ileride tüketebilir; **bu pack MOD-0155'i açmaz**.

## 7. Backend Runtime Scope

16 maddelik backend kapsamı pack §2.2'de sabitlendi. Kritik yapısal kararlar:

- `Handlers/CommandHandlers` + `Handlers/QueryHandlers` ayrımı; Handler/Validator'da `Command`/`Query` suffix'i **yok**.
- **`Delete*` / `BulkDelete*` komutu üretilmez** — Golden Reference setinden bilinçli, gerekçeli tek sapma
  (FU01 hard-delete yasağı transport ve komut katmanında da uygulanır).
- Mongo guard'ları zorunlu: unique index'te `$ne` partial filter **yasak** · iki `DateTimeOffset` birlikte
  index/sort **yasak** · `Brand`/`Product` **`RegisterClassMaps`'e eklenir** · transaction öncesi
  `SupportsTransactionsAsync` guard'ı.
- `BrandCode`/`ProductCode` archived kayıtlar dahil **kalıcı rezerve** → partial index ihtiyacı ortadan kalkar.

## 8. Brand Model Scope

`BrandId` · `TenantId` (JWT) · `BrandCode` (unique, immutable) · `BrandName` · `BrandStatus` · `Description` ·
`OwnerCompanyId?` · `BusinessUnitId?` · `TherapeuticAreaId?` · `EffectiveFrom` · `EffectiveTo?` ·
`ExternalReferences[]` · `IsArchived` · `ArchivedAt/By` · audit.

`BrandStatus`: `draft · active · inactive · archived`. Archived brand okunur, update **409**, product bağlama
**409**, archive **cascade yapmaz**.

## 9. Product Model Scope

`ProductId` · `TenantId` (JWT) · `ProductCode` (unique, immutable) · `ProductName` · `ProductStatus` ·
**`BrandId?` optional** · `ProductType?` · `DosageForm?` · `Strength?` · `PackSize?` · `UnitOfMeasure?` ·
`ATCCode?` (external taxonomy pointer) · `TherapeuticAreaId?` (concept/reference) · `IndicationRefs[]` ·
`Description` · `EffectiveFrom` · `EffectiveTo?` · `ExternalReferences[]` · `IsArchived` · `ArchivedAt/By` · audit.

`ProductStatus`: `draft · active · inactive · archived`. `ProductType`: `medicine · medical-device · service ·
training-material · other`.

**`BrandId` optional/required sorusu AÇIK DEĞİLDİR:** FU01 §4.1 zaten *optional* kararını vermişti
(markasız/jenerik/non-pharma ürünler); FU02 bunu uygular ve testle sabitler (backend gate 20).

## 10. API Contract

12 endpoint, `DELETE` yok:

```text
GET  /api/mdm/brands                          GET  /api/mdm/products
GET  /api/mdm/brands/{brandId}                GET  /api/mdm/products/{productId}
POST /api/mdm/brands                          POST /api/mdm/products
PUT  /api/mdm/brands/{brandId}                PUT  /api/mdm/products/{productId}
POST /api/mdm/brands/{brandId}/archive        POST /api/mdm/products/{productId}/archive
GET  /api/mdm/brands/{brandId}/products
GET  /api/mdm/brand-products/contract
```

`TenantId` payload yok · `Response<T>`/`reasonCode`/`correlationId` korunur · business smoke yalnız Gateway
üzerinden · direct `:5059` yasak.

## 11. Contract Flags

Yayımlanan (8, hepsi `true`): `supportsBrandManagement` · `supportsProductManagement` ·
`supportsBrandProductReference` · `supportsBrandProductHierarchy` · `supportsExternalReferences` ·
`supportsArchiveLifecycle` · `supportsEffectiveDating` · `supportsContractDrivenUi`.

Yasak (false olarak bile yayımlanmaz — 18 anahtar): campaign/knowledge/visit/route/frequency/recommendation/
digital-detailing/workflow/segmentation runtime flag'leri + `supportsAtcLocalMaster` ·
`supportsTherapeuticAreaFlatReferenceSet` · `supportsIndicationMaster` · `supportsItemSkuMaster` ·
`supportsUomMapping` · `supportsImportExport` · `supportsHardDelete` · `supportsMultiBrandProduct`.

Contract ayrıca `vocabulary` · `reasonCodes` · `permissions` · `limitations` bloklarını yayımlar; UI hardcoded
liste tutmaz.

## 12. Gateway / Routing Decision

**Mevcut mu?** Hayır. `ocelot.json` taraması: `/api/mdm/*` route'u **yok**; MDM'in tek rotası
`/api/legal-entities` çiftidir; `/api/mdm/{everything}` catch-all'ı da yok.

**Karar: Gateway değişikliği GEREKLİ ve dar kapsamda YETKİLENDİRİLDİ** — `integration-agent` sorumluluğunda,
`gateway/Diten.ApiGateway/ocelot.json` içinde **yalnız beş blok**:

| Upstream | Methods |
|---|---|
| `/api/mdm/brands` | `GET, POST, OPTIONS` |
| `/api/mdm/brands/{everything}` | `GET, POST, PUT, OPTIONS` |
| `/api/mdm/products` | `GET, POST, OPTIONS` |
| `/api/mdm/products/{everything}` | `GET, POST, PUT, OPTIONS` |
| `/api/mdm/brand-products/contract` | `GET, OPTIONS` |

Downstream `localhost:5059` · `DELETE` ve `PATCH` **listelenmez** (NET-001'in "tüm metotlar" tavsiyesinden
bilinçli sapma: hard-delete yasağı transport katmanında da uygulanır) · `OPTIONS` zorunlu (CORS preflight) ·
explicit route'lar catch-all'dan önce · mevcut `/api/legal-entities` ve tüm `/api/crm/*` **değişmez**.

`/api/mdm/*` prefix'i seçildi çünkü paylaşılan gateway'de `/api/brands` çakışma riskli ve sahipsiz görünür;
`/api/crm/*` ve `/api/platform/*` baskın desendir. Mevcut prefix'siz `/api/legal-entities` taşınmaz → F8.

## 13. UI Scope

Primary surface: **Master Data → Brands** ve **Master Data → Products**, `_LayoutTenantShell`, tenant shell.
Routes `/MasterData/Brands` · `/MasterData/Products` (+ `Create`/`Edit/{id}`/`Details/{id}` deep link'leri).
Brand detail'de **Products tab**, product detail'de **Brand reference** bölümü. Contract-driven capability
gating, Gateway-only same-origin proxy, DataTable v2, 7 dil parity.

## 14. Protected Path / Navigation Authorization

`_LayoutTenantShell.cshtml` protected'tır. Pack **dar istisna** verir: Commercial Suite grubundan sonra,
`DynamicModuleMenu` çağrısından önce **yeni `Master Data` menu-header** + iki `menu-item` `<li>`; header
gruptaki ilk görünür öğe tarafından bir kez render edilir (mevcut Commercial Suite deseniyle birebir).

Guard `mdm.brands.read` / `mdm.products.read` · label 7 dilli `MasterData`/`BrandsMenu`/`ProductsMenu` shared
key'leri · aktif route `currentPath.StartsWith("/MasterData/…")`. Layout yapısı, mevcut menü öğeleri,
`DynamicModuleMenu`, token/cookie akışı, navigation API ve CSS/JS bundle **değişmez**.

**MOD-0285 değerlendirildi ve kullanılmadı:** descriptor publish Platform/backend değişikliği ister; FU02 bunu
açmaz. Data-driven migration ileride yapılırsa hardcoded `<li>`'ler kaldırılır (F7) — **çift menü kabul edilmez**.

## 15. Golden UI Decision

```text
golden_reference: compact
```

Alan sayımı: **Brand 10** · **Product 16** — ikisi de `> 8`. FU01 §17 "Brand ≈ 8, slim adayı" demişti; FU01'in
zorunlu kıldığı `EffectiveFrom`/`EffectiveTo` alanları (D4) sayımı 10'a çıkardı, dolayısıyla **her iki modül de
compact** ve tek UI standardı korunuyor — Campaign (FU05) ve Consent UI ile aynı.

Compact'ta `_CreateEditOffcanvas.cshtml` ve `_DetailsQuickView.cshtml` **yasak**; Create/Edit/Details full-page,
ortak `_Form.cshtml`. Archive confirmation ve toast Golden Slim (`window.showConfirm` / `window.showToast`).

## 16. Brand UI Scope

Kolonlar: `BrandCode` · `BrandName` · `BrandStatus` · `BusinessUnitId` · `TherapeuticAreaId` · `EffectiveFrom` ·
`IsArchived` · `UpdatedAt` · Actions.
Filtreler: Search · BrandStatus · BusinessUnitId · TherapeuticAreaId · IncludeArchived.
Detail: Summary · References · External References · **Products tab** · Audit/provenance.
Create/Edit (10 alan): §17 pack.
Kurallar: `TenantId` gönderilmez · code/name/status/EffectiveFrom required · `BrandCode` edit'te immutable ·
archived'da edit disabled · archive POST · product cascade **yok**.

## 17. Product UI Scope

Kolonlar: `ProductCode` · `ProductName` · `ProductStatus` · `BrandId` · `ProductType` · `DosageForm` ·
`Strength` · `PackSize` · `UnitOfMeasure` · `ATCCode` · `TherapeuticAreaId` · `EffectiveFrom` · `IsArchived` ·
`UpdatedAt` · Actions (yoğunluk nedeniyle bir kısmı colvis ile gizlenebilir).
Filtreler: Search · ProductStatus · BrandId · ProductType · DosageForm · TherapeuticAreaId · IncludeArchived.
Detail: Summary · Brand reference · Pharma metadata · External References · Audit/provenance.
Create/Edit (16 alan): §18 pack.
Kurallar: `TenantId` gönderilmez · code/name/status/EffectiveFrom required · **`BrandId` optional** · brand
seçici `GET /api/mdm/brands?brandStatus=active`'ten beslenir (hardcoded liste yasak) · archived'da edit
disabled · archive POST · `ATCCode` ve `TherapeuticArea` yanında **help text** zorunlu.

## 18. Permission / Visibility

**Ayrı Brand/Product permission seçildi** (birleşik `mdm.brand-product.*` değil):

```text
mdm.brands.read    mdm.brands.create    mdm.brands.update    mdm.brands.archive
mdm.products.read  mdm.products.create  mdm.products.update  mdm.products.archive
```

Gerekçe: Brand ve Product iki ayrı aggregate, iki ayrı controller, iki ayrı menü sayfasıdır; birleşik anahtar
ürün yöneticisine zorunlu olarak marka yazma yetkisi verirdi. FU01 §14'ün "tüketiciler yalnız `*.read` ister"
kuralıyla da uyumlu.

**Çoğul resource segmenti** shipped `mdm.legal-entities.*` precedent'ini ve PKS-001 §1 canonical örneğini izler
(FU01 §14'ün tekil önerisinden sapma D1). `archive` PKS-001 Tier-2 onaylı; `delete` kullanılmaz.

**Seed/grant bu task'ta yapılmadı.** Canonical key'ler kataloğa eklenmediği için ilk smoke `platform_admin`
bypass'ı veya mevcut fallback ile yürütülebilir; bu durum evidence'ta PARTIAL olarak raporlanır (F3).

## 19. RESX / Localization

7 dil zorunlu (`en · fr · es · zh · ar · ru · tr`), üç dosya ailesi: `BrandsIndex.{lang}.resx` ·
`ProductsIndex.{lang}.resx` · `SharedResource.{lang}.resx` (yalnız `MasterData`/`BrandsMenu`/`ProductsMenu`).
13 key grubu pack §20'de sabitlendi; **help text'ler** (`ATCCode` external taxonomy · `TherapeuticArea`
concept/reference) zorunlu key grubudur. `index.l10n.js` camelCase→PascalCase dönüşümü yapmazsa `window.L10n`
key'leri `undefined` döner. `.resx` değişikliği **tam fleet restart** ister.

## 20. Response Shape / Data Guard

21 alanlık deny-list pack §19'da: campaign/visit/route/frequency/knowledge/recommendation/workflow/consent/
preference/patient alanları + `skuId` + `uomMappingId`. Bunlar sessizce ignore edilir; hiçbiri DOM, view model,
log, toast veya detail paneline taşınmaz ve **bunlardan feature açılmaz**.

## 21. Tests / Smoke Acceptance

- **Backend:** 29 gate (brief'in 24 maddesi + cross-tenant brand link 404, `BrandId=null` create başarılı,
  effective-date 400, external-reference primary 409, archive-cascade-yok). Tenant isolation, archived-readonly,
  vocabulary 400, duplicate 409 (archived dahil), `DELETE` 404/405, contract flag assert'leri dahil.
- **UI:** 19 gate (brief'in 18 maddesi + Products/Brands için ayrı `verify_datatable_page.py --reference compact`).
- **Authenticated Gateway smoke:** 16 adım, hedef tenant `97c59330-dbc4-4665-b29c-0c26dbb5cc93`; login
  `X-Tenant-Id` header'ı ile yapılır (aksi hâlde platform `…0001` token'ı gelir). **Cleanup yalnız archive** ile;
  hiçbir kayıt silinmez, Mongo hand-edit yapılmaz.

## 22. Explicit Exclusions

Campaign runtime/UI · Consent runtime/UI · Knowledge runtime/UI · Frequency runtime · Visit planning · Route
planning · **MOD-0155** · segmentation engine · recommendation engine · digital detailing · workflow/approval ·
MOD-0048 publish · RBAC seed/grant · registry write · Mongo hand-edit · import/export engine · patient data ·
hard delete · `DELETE` kullanımı · Item/SKU/UoM/identifier · multi-brand product · ürün ailesi hiyerarşisi ·
ATC local master · TherapeuticArea flat reference set · Indication master · `services/Diten.CrmService/**`.

## 23. Acceptance Criteria

| # | Kriter | Durum |
|---|---|---|
| 1 | Module pack oluşturuldu | ✅ |
| 2 | Pack status `ready-for-dev` | ✅ |
| 3 | Domain net | ✅ `master-data-management` (FU01 SoR kararı; brief'ten sapma kullanıcı onaylı) |
| 4 | Service net | ✅ `Diten.MdmService` + `frontend/Diten.Web` |
| 5 | Brand/Product ownership net | ✅ §2.1 |
| 6 | Backend runtime scope net | ✅ §2.2 / §10 |
| 7 | UI scope net | ✅ §11 / §17 / §18 |
| 8 | Gateway route kararı net | ✅ **gerekli**, 5 route, port 5059, `DELETE` yok |
| 9 | Protected navigation kararı net | ✅ dar `_LayoutTenantShell` istisnası + MOD-0285 follow-up |
| 10 | Golden reference `compact` | ✅ Brand 10 / Product 16 alan |
| 11 | Brand model alanları net | ✅ §4.1 |
| 12 | Product model alanları net | ✅ §4.2 (`BrandId` optional, FU01 §4.1) |
| 13 | API endpoints net | ✅ 12 endpoint |
| 14 | Contract flags net | ✅ 8 true / 18 yasak |
| 15 | Exclusions net | ✅ §2.3 |
| 16 | Permission strategy net | ✅ ayrı `mdm.brands.*` / `mdm.products.*` |
| 17 | RESX parity net | ✅ 7 dil, 13 key grubu |
| 18 | Backend tests acceptance net | ✅ 29 gate |
| 19 | UI tests acceptance net | ✅ 19 gate |
| 20 | Smoke acceptance net | ✅ 16 adım |
| 21 | MOD-0155 excluded | ✅ |
| 22 | `@orchestrator` çağrısı verildi | ✅ §26 |

## 24. Created / Updated Files

| Dosya | İşlem |
|---|---|
| `execution/domains/master-data-management/module-packs/MOD-0290-FU02-brand-product-runtime-ui.md` | **Oluşturuldu** — 25 bölümlük ready-for-dev pack |
| `docs/audits/mod-0290-fu02-brand-product-runtime-ui-module-pack-authorization-2026-08-03.md` | **Oluşturuldu** — bu rapor |
| `execution/domains/master-data-management/domain-config.md` | **Güncellendi** (yalnız doküman) — MOD-0290-FU02 authorization kaydı + frontend/gateway kısıtının bu modül için dar kapsamda kaldırıldığı notu |

Değişmeyen: tüm `services/**` · `frontend/**` · `gateway/**` · `execution/registries/**` · Auth seed/grant ·
migrations · Mongo verisi · `execution/domains/commercial-suite/**`.

## 25. Final Verdict

**PASS.**

- Module pack oluşturuldu ve `ready-for-dev`.
- Brand/Product ownership net: SoR **MOD-0290 / MDM**; tüketiciler yalnız referans verir.
- Backend runtime scope net (16 madde, hard-delete yasağı komut ve transport katmanında).
- UI scope net (Master Data → Brands/Products, Compact, 7 dil).
- Gateway route kararı net: **gerekli**, 5 route, port 5059, `DELETE` yok, mevcut route'lar korunur.
- Protected navigation kararı net: dar `_LayoutTenantShell` istisnası, MOD-0285 migration follow-up.
- Golden reference kararı net: `compact` (alan sayımıyla gerekçeli).
- Permission strategy net: ayrı `mdm.brands.*` / `mdm.products.*`; seed/grant yok.
- Exclusions net; MOD-0155 açıkça dışarıda.
- Runtime/UI/Gateway/seed/registry/Mongo **değişmedi** → FAIL koşullarının hiçbiri gerçekleşmedi.

**PARTIAL'a düşmedi:** Gateway route kararı verildi (EA'ya bırakılmadı) · `BrandId` kararı FU01 §4.1 ile zaten
kapalıydı · protected navigation kararı verildi · status `ready-for-dev`.

**Reviewer dikkatine (§0 sapmaları):** D1 permission çoğullaştırma · D2 in-domain vocabulary (MOD-0048
reconciliation F4) · D3 `discontinued` yetkilendirilmedi (F5) · D4 effective-dating alanları geri eklendi ·
D5 `/api/mdm/*` prefix'i. Hepsi reddedilebilir; reddedilen madde pack'ten çıkarılır, gerisi geçerli kalır.

## 26. Next Recommended Prompt

```text
@orchestrator execution/domains/master-data-management/module-packs/MOD-0290-FU02-brand-product-runtime-ui.md

MOD-0290-FU02 — Brand/Product Runtime + UI Implementation
```

> **Not:** MOD-0155 beklemede kalır. Campaign, Consent, Knowledge ve Frequency runtime'ları bu task kapsamında
> değişmez ve implementation sırasında da değişmeyecektir.
