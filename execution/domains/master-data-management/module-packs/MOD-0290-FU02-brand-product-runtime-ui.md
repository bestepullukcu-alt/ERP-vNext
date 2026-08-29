---
id: MOD-0290-FU02
name: Brand / Product Runtime + UI
parent: MOD-0290
parent_name: Product / Item / SKU Master
domain: master-data-management
service: Diten.MdmService + frontend/Diten.Web
shell: tenant
golden_reference: compact
entity_base: EntityBase
status: review
runtime_code_allowed: true
runtime_code_scope: "RUNTIME + UI — Brand ve Product aggregate'leri, CRUD-minus-delete, archive lifecycle, list/detail/contract endpointleri, dar Gateway route eklemesi, MasterData tenant-shell UI yüzeyi, testler, authenticated Gateway smoke ve evidence. Campaign/Knowledge/Frequency/Visit/MOD-0155 runtime, Item/SKU/UoM/identifier yönetimi, import/export engine, RBAC seed/grant, MOD-0048 publish, registry write ve Mongo hand-edit yasaktır."
owner: module-pack-author
branch: feature/mdm/mod-0290-fu02-brand-product-runtime-ui
started: 2026-08-03
target: 2026-08-03
form_field_count: 16
dependencies:
  - MOD-0290-FU01 (Brand / Product Master Boundary — alan sözleşmesi, lifecycle, ExternalReferences, SoR kararı)
  - MOD-0220 (Diten.MdmService LegalEntity precedent — tenant-scoped EntityBase, route/permission konvansiyonu)
  - MOD-0018 (RBAC — yalnız tüketim; seed/grant bu pack'te yok)
  - MOD-0048 (reference data — vocabulary reconciliation follow-up; publish bu pack'te yok)
  - MOD-0288 / MOD-0151 (BusinessUnit scope vokabüleri — referans, yeniden üretilmez)
  - MOD-0162-FU01C (TherapeuticArea ConceptNode boundary — flat reference set açılmaz)
  - MOD-0285 (mevcut tenant navigation pattern; Platform/backend runtime değişmez)
  - DEV-0001 (Golden Reference Compact — primary surface)
  - DEV-0000 (Golden Reference Slim — archive confirmation, toast, alt-canvas davranışı)
---

# MOD-0290-FU02 — Brand / Product Runtime + UI

> **READY-FOR-DEV RUNTIME + UI AUTHORIZATION (2026-08-03).**
> Bu pack, MOD-0290-FU01'de **PASS** kabul edilen Brand/Product master boundary'sini çalışır hâle getirir:
> aggregate, CRUD-minus-delete, archive lifecycle, list/detail/contract API'leri, dar Gateway route eklemesi ve
> tenant-shell master data UI yüzeyi. Campaign, Knowledge, Frequency, Visit/Route Planning ve MOD-0155
> runtime'ları **açılmaz**; onlar FU02 sonrası yalnız **referansla** tüketir.
>
> **DCP-002 kimlik kapısı — PASS (2026-08-03):**
> `py .antigravity/scripts/verify_module_id.py . --check-id MOD-0290-FU02 --name "Brand / Product Runtime + UI" --parent MOD-0290`
> → `OK  MOD-0290-FU02: proven against Blueprint/registry.` (exit 0)
>
> **Yerleşim kararı (kullanıcı onayı, 2026-08-03):** Runtime ve UI **MDM domain'inde** açılır.
> MOD-0290-FU01 §1 Seçenek-2'yi (Commercial Suite / CRM-adjacent Brand-Product master) *"CRM'de ikinci bir master
> doğurur"* gerekçesiyle **reddetmişti**; [commercial-suite/domain-config.md](../../commercial-suite/domain-config.md)
> Brand/Product/SKU master'ı Out-of-Scope sayar ve [crm-sor-boundary.md](../../commercial-suite/crm-sor-boundary.md)
> satır 31 *"Brand / Product / SKU → MDM / Product · read-only consume"* der. Bu pack o üç kararla **tam
> tutarlıdır**; CRM tarafında hiçbir dosya değişmez.
>
> **Neden gerekli:** FU01 `runtime_code_allowed: false` idi ve yalnız sahiplik/sınır kararı verdi. FU02 yeni
> aggregate, yeni API, yeni Gateway route ve yeni frontend navigation açtığı için `AGENTS.md` §10 uyarınca
> `approved`/`ready-for-dev` module pack olmadan `@orchestrator` implementasyona başlayamaz. MDM domain-config
> ayrıca frontend ve gateway'i ilk slice dışında tutmuştu; bu pack §5/§6/§15'te o kısıtı **bu modül için,
> dar ve test edilebilir biçimde** kaldırır.
>
> Otorite sırası: **Blueprint Excel** > Module Pack > [Domain Config](../domain-config.md) > `AGENTS.md` >
> `.antigravity/rules/`.

---

## 0. FU01'den Sapmalar (reviewer dikkatine)

FU01 otoritedir. FU02 aşağıdaki **beş** noktada FU01'i genişletir veya somutlaştırır. Hepsi bilinçlidir ve
reviewer tarafından reddedilebilir; reddedilirse ilgili madde pack'ten çıkarılır, gerisi geçerli kalır.

| # | Konu | FU01 | FU02 kararı | Gerekçe |
|---|---|---|---|---|
| D1 | Permission adlandırma | §14: `mdm.brand.read` (tekil) | **`mdm.brands.*` / `mdm.products.*` (çoğul)** | Shipped MDM precedent `mdm.legal-entities.*` çoğul; PKS-001 §1 canonical örneği de çoğul. Tekil kalırsa MDM içinde iki farklı adlandırma yaşar. |
| D2 | Vocabulary kaynağı | §10: MOD-0048 reference set; hardcoded enum yasak, set yayınlanmadan fail-closed 400 | **v1 in-domain controlled vocabulary constants**, MOD-0048 reconciliation follow-up (F4) | FU01 F8 (`brand-status`/`product-status`/`product-dosage-form`/`product-uom` publish) **açıktır** ve MOD-0048 publish bu pack'te yasaktır. In-domain vocab olmadan FU02 hiç çalışmaz. MOD-0164-FU02 aynı domain zincirinde bu deseni uygulayıp PASS aldı. |
| D3 | `ProductStatus` sözlüğü | §11: `draft · active · inactive · archived` | **FU01'in 4 değeri aynen**; `discontinued` **yetkilendirilmedi** | `discontinued` gerçek bir pharma ihtiyacıdır ama FU01 §11 lifecycle setini kilitledi. Eklenmesi FU01 §11 amendment'i ister → F5. |
| D4 | Effective dating | §3/§4: `EffectiveFrom` zorunlu, `EffectiveTo` opsiyonel | **Aynen korunur ve UI'da yüzeye çıkar** | Task brief'inin alan listesinde yoktu; FU01 zorunlu kıldığı için geri eklendi. Brand form alan sayısını 8→10'a, Product'ı 16'ya taşır (→ `compact`). |
| D5 | Gateway path prefix | Karar yok | **`/api/mdm/*`** (mevcut tek MDM rotası `/api/legal-entities` prefix'siz) | Paylaşılan gateway'de `/api/brands` çakışma riskli ve sahipsiz görünür. `/api/crm/*` ve `/api/platform/*` baskın desendir. `/api/legal-entities` **değiştirilmez**. |

`BrandId` required/optional sorusu **açık değildir**: FU01 §4.1 zaten **optional** kararını vermiştir; FU02 onu
uygular (§4.2).

---

## 1. Module Summary

Amaç, MOD-0290-FU01'de sözleşmesi yazılmış Brand ve Product master'ını çalışır, tenant-izole, yetkilendirilmiş
ve UI'dan yönetilebilir hâle getirmektir:

- `Brand` ve `Product` aggregate'leri, tenant-scoped persistence ve soft archive lifecycle.
- Brand ve Product için **CRUD-minus-delete** (create · read · update · archive); hard delete ve `DELETE` yok.
- List/detail endpointleri, opsiyonel `brand → products` relation endpointi.
- Tüketicilerin (Campaign, Knowledge, Frequency, Visit Planning) referans verebilmesi için capability contract endpointi.
- `Master Data → Brands` ve `Master Data → Products` permission-controlled tenant-shell UI yüzeyleri.
- Golden Compact list/create/edit/details + Golden Slim archive confirmation/toast; DataTable v2; yedi dil parity.

Hedef kullanıcı, tenant içindeki yetkili master-data yöneticisidir. FU02 hiçbir tüketici modülün runtime'ını
değiştirmez; onlara **okunabilir bir master** ve **bir contract** verir.

## 2. Ownership and Boundaries

### 2.1 SoR kararı (FU01'den devralınır, değiştirilmez)

```text
MOD-0290 Brand/Product Master, Brand ve Product için Source of Truth'tur.
```

| Kural | Karar |
|---|---|
| Campaign · Knowledge · Frequency · Visit/Route Planning | Brand/Product master **oluşturmaz** |
| Campaign | Yalnız `BrandId`/`ProductId` **referansı** tutar |
| Knowledge / Content | Yalnız `BrandId`/`ProductId` referansı tutabilir; Brand/Product'sız içerik geçerlidir |
| Frequency policy | Yalnız Brand/Product **scope/reference** tüketir |
| MOD-0155 | İleride referans tüketebilir; **bu pack MOD-0155'i açmaz** |
| Master data kopyalama | Başka aggregate içine **kopyalanmaz**; ad/kod kopyalanırsa görüntüleme türevidir ve master değişince **stale** sayılır |
| Historical references | **Korunur** |
| Archive | Geçmiş Campaign/Knowledge/Frequency kayıtlarını **silmez** ve cascade etmez |

### 2.2 In-scope

**Backend / runtime**

1. `Brand` aggregate (tenant-owned, `EntityBase`).
2. `Product` aggregate (tenant-owned, `EntityBase`).
3. Brand CRUD-minus-delete (create · list · detail · update).
4. Product CRUD-minus-delete (create · list · detail · update).
5. Brand archive (soft, POST endpoint).
6. Product archive (soft, POST endpoint).
7. Brand/Product list + detail endpointleri, filtre ve sayfalama.
8. `GET /api/mdm/brand-products/contract` capability contract endpointi.
9. `ExternalReferences[]` (FU01 §12 sözleşmesi, `IsPrimary` tekliği dahil).
10. FU01 §4 ile uyumlu regulatory/pharma metadata: `ProductType` · `DosageForm` · `Strength` · `PackSize` ·
    `UnitOfMeasure` · `ATCCode` (external taxonomy pointer) · `TherapeuticAreaId` (concept/reference) ·
    `IndicationRefs[]`.
11. Tenant isolation; cross-tenant erişim fail-closed 404.
12. Soft archive lifecycle + effective window.
13. Hard delete **yok**.
14. Unit/integration testler.
15. Authenticated Gateway smoke script.
16. Evidence raporu.

**Gateway (dar)**

17. `ocelot.json` içine yalnız §15'teki beş route; mevcut route'lar bozulmaz.

**Frontend / UI**

18. `Master Data` navigation grubu + `Brands` ve `Products` menü girdileri (permission-guarded).
19. Brand List · Detail · Create/Edit · Archive.
20. Product List · Detail · Create/Edit · Archive.
21. Brand Detail içinde **Products** tab'ı (relation endpointi üzerinden).
22. Product Detail içinde **Brand reference** section'ı.
23. Contract-driven UI capability gating.
24. Gateway-only API entegrasyonu (same-origin MVC proxy).
25. Golden Compact/Slim pattern + DataTable v2.
26. Yedi dil RESX parity.
27. UI testleri, build, verifier.
28. UI smoke / manuel doğrulama.

### 2.3 Out-of-scope / kesinlikle yetkisiz

- Campaign runtime veya Campaign UI değişikliği.
- Consent/Preference runtime veya UI değişikliği.
- Knowledge / Content / Path / Journey / Concept Graph runtime veya UI.
- Frequency runtime · Visit planning · Route planning · due/overdue · last-visit.
- Segmentation engine · Recommendation engine · Digital detailing · Next-best-action.
- **MOD-0155** herhangi bir kapsamı.
- Workflow / approval / MOD-0023 entegrasyonu.
- **Item / SKU / UoM mapping / product identifier yönetimi** — MOD-0290'ın diğer FU'ları.
- Multi-brand product (FU01 F4) · ürün ailesi/portföy hiyerarşisi (FU01 F5).
- ATC **local master** açmak · TherapeuticArea'yı **flat reference set** yapmak · Indication master açmak.
- MOD-0048 publish/write · RBAC seed/grant · registry write · Mongo hand-edit · migration.
- Import/export engine · legacy `Property/PropertyList` migration çalıştırma (FU01 F9).
- Patient data.
- Hard delete · `DELETE` endpoint · `DELETE` client kullanımı · bulk-delete.
- `services/Diten.CrmService/**` ve diğer domain servisleri.
- Yeni shell/navigation pattern · MOD-0285 runtime değişikliği.

## 3. Owned Objects

**Aggregates (yeni, tenant-owned):** `Brand` · `Product`
**Repositories:** `IBrandRepository` / `BrandRepository` · `IProductRepository` / `ProductRepository`

**Commands:** `CreateBrandCommand` · `UpdateBrandCommand` · `ArchiveBrandCommand` ·
`CreateProductCommand` · `UpdateProductCommand` · `ArchiveProductCommand`
**Queries:** `GetBrandListQuery` · `GetBrandByIdQuery` · `GetBrandProductsQuery` ·
`GetProductListQuery` · `GetProductByIdQuery` · `GetBrandProductContractQuery`

> **Delete/BulkDelete komutu yoktur.** Golden Reference DataTable seti normalde `Delete{Module}Command` ve
> `BulkDelete{Module}Command` içerir; FU01 §3/§4 hard delete'i yasakladığı için bu iki komut **kasıtlı olarak
> üretilmez** ve DataTable bulk-action bar'ı **archive** dışında yıkıcı aksiyon sunmaz. Bu, standarttan bilinçli
> ve gerekçeli tek backend sapmasıdır.

**API controllers:** `BrandsController` (`api/mdm/brands`) · `ProductsController` (`api/mdm/products`) ·
`BrandProductContractController` (`api/mdm/brand-products/contract`)

**Frontend routes:** `/MasterData/Brands` · `/MasterData/Products`
**Frontend controllers:** `MasterData/BrandsController` · `MasterData/ProductsController` (same-origin proxy)

**Permissions (canonical, PKS-001 lowercase-dotted):**

```text
mdm.brands.read      mdm.brands.create      mdm.brands.update      mdm.brands.archive
mdm.products.read    mdm.products.create    mdm.products.update    mdm.products.archive
```

Seed/grant **bu pack'te yapılmaz** (F3).

## 4. Entity Fields

Her iki aggregate `EntityBase`'den türer (tenant-owned). `TenantId` **server-side** JWT claim'inden çözülür ve
hiçbir DTO/request/form/JSON payload'da **bulunmaz**.

### 4.1 `Brand`

| Field | Type | Required | Rules |
|---|---|---|---|
| `BrandId` | Guid | Evet | Aggregate kimliği; server-generated |
| `TenantId` | Guid | Evet | JWT claim'inden; payload'da **asla**; cross-tenant erişim 404 |
| `BrandCode` | string | Evet | Trim, max 64, upper-normalize; tenant içinde **unique (archived dahil)**; **immutable** — update ile değişmez |
| `BrandName` | string | Evet | Trim, max 200 |
| `BrandStatus` | string | Evet | Controlled vocabulary (§4.3) |
| `Description` | string? | Hayır | Max 2000 |
| `OwnerCompanyId` | Guid? | Hayır | Format-level referans; master resolve edilmez |
| `BusinessUnitId` | Guid? | Hayır | MOD-0288/MOD-0151 BU vokabüleri **referanslanır**, yeniden üretilmez |
| `TherapeuticAreaId` | Guid? | Hayır | ConceptNode/reference; **flat reference set açılmaz** |
| `EffectiveFrom` | DateTimeOffset | Evet | FU01 §3 |
| `EffectiveTo` | DateTimeOffset? | Hayır | `EffectiveTo < EffectiveFrom` → **400** |
| `ExternalReferences[]` | collection | Hayır | §4.4 |
| `IsArchived` | bool | Evet | Server-managed |
| `ArchivedAt` / `ArchivedBy` | DateTimeOffset? / string? | Hayır | Archive anında set edilir |
| `CreatedAt` / `CreatedBy` / `UpdatedAt` / `UpdatedBy` | audit | Evet | `EntityBase` |

**Brand kuralları**

- `BrandCode` **stabil**; rename `BrandName` ile yapılır, kod bozulmaz.
- Hard delete **yok**.
- Archived brand **okunabilir**; update denemesi **409**.
- Product create/update **archived brand'e bağlanamaz** → **409**.
- Brand archive **cascade yapmaz**; altındaki product'lar otomatik archive/delete edilmez, ancak yeni product o
  brand'e bağlanamaz (sessiz cascade FU01 §11'de yasaktır).
- Brand bir `KnowledgeContent` veya `Campaign` **değildir**; içine içerik, mesaj, sıklık kuralı veya ziyaret
  hedefi **gömülmez**.

**Create/edit form alanları (10):** `BrandCode` · `BrandName` · `BrandStatus` · `Description` ·
`OwnerCompanyId` · `BusinessUnitId` · `TherapeuticAreaId` · `EffectiveFrom` · `EffectiveTo` · `ExternalReferences`
→ 8'den fazla → **compact**.

### 4.2 `Product`

| Field | Type | Required | Rules |
|---|---|---|---|
| `ProductId` | Guid | Evet | Server-generated |
| `TenantId` | Guid | Evet | JWT claim'inden; payload'da **asla** |
| `ProductCode` | string | Evet | Trim, max 64, upper-normalize; tenant içinde **unique (archived dahil)**; **immutable** |
| `ProductName` | string | Evet | Trim, max 200 |
| `ProductStatus` | string | Evet | Controlled vocabulary (§4.3) |
| `BrandId` | Guid? | **Hayır — optional** | FU01 §4.1 kararı: markasız/jenerik/non-pharma ürün mümkündür. Doluysa brand **aynı tenant'ta ve archived değil** olmalı → aksi hâlde **409** |
| `ProductType` | string? | Hayır | Controlled vocabulary (§4.3) |
| `DosageForm` | string? | Hayır | Controlled vocabulary (§4.3) |
| `Strength` | string? | Hayır | Max 100; değer + birim serbest metin (UoM master MOD-0290'ın ayrı FU'su) |
| `PackSize` | string? | Hayır | Max 100 |
| `UnitOfMeasure` | string? | Hayır | Controlled vocabulary (§4.3); UoM **mapping** açılmaz |
| `ATCCode` | string? | Hayır | **External taxonomy pointer** (WHO ATC); max 16; format-level validate; **local ATC master açılmaz** |
| `TherapeuticAreaId` | Guid? | Hayır | ConceptNode/reference; brand değeri varsayılan, product değeri **override** (FU01 §4.1) |
| `IndicationRefs[]` | Guid[] | Hayır | Referans listesi; **indication master açılmaz** |
| `Description` | string? | Hayır | Max 2000 |
| `EffectiveFrom` | DateTimeOffset | Evet | FU01 §4 |
| `EffectiveTo` | DateTimeOffset? | Hayır | `EffectiveTo < EffectiveFrom` → **400** |
| `ExternalReferences[]` | collection | Hayır | §4.4 |
| `IsArchived` | bool | Evet | Server-managed |
| `ArchivedAt` / `ArchivedBy` | DateTimeOffset? / string? | Hayır | |
| `CreatedAt` / `CreatedBy` / `UpdatedAt` / `UpdatedBy` | audit | Evet | `EntityBase` |

**Product kuralları**

- `ProductCode` **stabil**; rename kodu değiştirmez.
- Hard delete **yok**.
- Archived product **okunabilir**; update denemesi **409**.
- Product archive Campaign/Knowledge referanslarını **silmez**.
- `ATCCode` ve `IndicationRefs[]` **yalnız referans/metadata**; bunlardan taxonomy master doğmaz.
- Aynı product birden fazla brand altında **olamaz** (FU01 §4.1 / F4 — v1'de kapalı).

**Create/edit form alanları (16):** `ProductCode` · `ProductName` · `ProductStatus` · `BrandId` ·
`ProductType` · `DosageForm` · `Strength` · `PackSize` · `UnitOfMeasure` · `ATCCode` · `TherapeuticAreaId` ·
`IndicationRefs` · `Description` · `EffectiveFrom` · `EffectiveTo` · `ExternalReferences`
→ **compact**; `form_field_count: 16` frontmatter'da bu (daha geniş) yüzeyi yansıtır.

### 4.3 Controlled vocabulary (v1 — in-domain; D2)

```text
BrandStatus    : draft · active · inactive · archived
ProductStatus  : draft · active · inactive · archived
ProductType    : medicine · medical-device · service · training-material · other
DosageForm     : v1 in-domain başlangıç seti; contract endpointinde yayımlanır
UnitOfMeasure  : v1 in-domain başlangıç seti; contract endpointinde yayımlanır
```

- Vokabüler dışı değer → **400** + `reasonCode`.
- `archived` durumu **yalnız archive endpointi** ile oluşur; update payload'ıyla set edilemez → **400**.
- Vokabüler değerleri **contract endpointinden** yayımlanır; UI hardcoded liste tutmaz.
- `discontinued` **yetkilendirilmedi** (D3/F5).
- MOD-0048 `brand-status` · `product-status` · `product-dosage-form` · `product-uom` setleri yayımlandığında
  reconciliation **F4** ile yapılır; o güne kadar in-domain sabitler otoritedir ve bu durum evidence'a yazılır.

### 4.4 `ExternalReferences[]` (FU01 §12 — değiştirilmez)

| Field | Required | Rule |
|---|---|---|
| `SourceSystem` | Evet | Trim, max 100 |
| `ExternalId` | Evet | Trim, max 200 |
| `ExternalCode` | Hayır | Max 100 |
| `ExternalName` | Hayır | Max 200 |
| `ImportedAt` | Hayır | DateTimeOffset |
| `IsPrimary` | Evet (default false) | `SourceSystem` başına **en fazla bir** primary → ikincisi **409** |

- Aynı `(SourceSystem, ExternalId)` iki farklı canonical kayda işaret ediyorsa **deterministik conflict** raporlanır.
- **Silent merge yasaktır** — otomatik birleştirme yok; çakışma görünür kalır.
- ExternalRef bir **iz kaydıdır**, ikinci bir master değildir.
- Hard delete yok; koleksiyon replace ile güncellenir.

### 4.5 MongoDB index ve serialization guard'ları (zorunlu)

| Konu | Kural |
|---|---|
| Unique index | `brands`: `(TenantId, BrandCode)` unique · `products`: `(TenantId, ProductCode)` unique. **Partial filter kullanılmaz** — kod archived kayıtlar dahil kalıcı olarak rezervedir. |
| Partial index `$ne` yasağı | Zorunlu bir partial index ortaya çıkarsa filtre **asla** `$ne`/`$not` içermez (servis startup'ta crash-loop'a girer); `$type`/`$lt` ile ifade edilir. |
| `DateTimeOffset` | `EffectiveFrom`/`EffectiveTo` BSON array olarak saklanır. **İki `DateTimeOffset` alanı birlikte index'lenmez ve birlikte sort edilmez** ("parallel arrays" 500). Gerekirse in-memory sort. |
| `DateTimeOffset` karşılaştırma | Instant-vs-date karşılaştırmalarında `.Date` üzerinden karşılaştırılır; ham instant karşılaştırması yanlış reddetme üretir. |
| Guid class map | `Brand` ve `Product` **`RegisterClassMaps`'e eklenir**. Eksik kayıt hâlinde Guid FK'lar binary yazılır, filtre string serialize eder ve sorgular **sessizce boş döner**. |
| Transactions | Çok-dokümanlı atomik yazım gerekiyorsa `StartTransaction` öncesi `SupportsTransactionsAsync` guard'ı + compensation; standalone dev Mongo aksi hâlde 500 verir. |
| Soft archive | `IsArchived` business lifecycle; `IsDeleted` teknik soft-delete olarak dokunulmaz kalır. |

## 5. Repo Scope

Yalnız aşağıdaki yollarda değişiklik yapılabilir:

**Governance**
- `execution/domains/master-data-management/module-packs/MOD-0290-FU02-brand-product-runtime-ui.md`
- `docs/audits/mod-0290-fu02-brand-product-runtime-ui-*.md` (implementation evidence)

**Backend**
- `services/Diten.MdmService/src/Diten.MdmService.Domain/**` — yalnız `Brand`, `Product` ve value object'leri
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/Brand/**`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/Product/**`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/BrandProductContract/**`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/**` — yalnız Brand/Product repository, class map
  kaydı ve index tanımı; mevcut LegalEntity map'leri değiştirilmez
- `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/BrandsController.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/ProductsController.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/BrandProductContractController.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/**` — yalnız Brand/Product DI kaydı
- repo-standard `Diten.MdmService` test yolları — yalnız MOD-0290-FU02 testleri

**Gateway (dar)**
- `gateway/Diten.ApiGateway/ocelot.json` — yalnız §15'teki beş route bloğu

**Frontend**
- `frontend/Diten.Web/Controllers/MasterData/BrandsController.cs`
- `frontend/Diten.Web/Controllers/MasterData/ProductsController.cs`
- `frontend/Diten.Web/Models/MasterData/BrandViewModels.cs`
- `frontend/Diten.Web/Models/MasterData/ProductViewModels.cs`
- `frontend/Diten.Web/Views/MasterData/Brands/**`
- `frontend/Diten.Web/Views/MasterData/Products/**`
- `frontend/Diten.Web/Resources/Views/MasterData/Brands/**`
- `frontend/Diten.Web/Resources/Views/MasterData/Products/**`
- `frontend/Diten.Web/wwwroot/assets/js/MasterData/Brands/**`
- `frontend/Diten.Web/wwwroot/assets/js/MasterData/Products/**`
- `frontend/Diten.Web/Resources/SharedResource.{en,fr,es,zh,ar,ru,tr}.resx` — yalnız `MasterData`,
  `BrandsMenu`, `ProductsMenu` ve gerekli paylaşılan archive/validation key'leri; var olan key'ler tekrar eklenmez
- `frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml` — yalnız §6'daki dar navigation istisnası
- `frontend/Diten.Web/tests/**` — yalnız MOD-0290-FU02 test dosyaları veya doğrudan ilgili test registration

Var olan ortak frontend/backend helper'ları **tüketilebilir**; değiştirilmeleri bu pack tarafından
yetkilendirilmez. Ortak helper değişikliği zorunlu görünürse orchestrator **durur** ve ayrı authorization ister.

## 6. Protected Paths

### 6.1 Dar navigation istisnası (tek izinli shared-layout değişikliği)

- **Dosya:** `frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml`
- **Section:** mevcut hardcoded menü listesi; `Commercial Suite` grubundan **sonra**, `DynamicModuleMenu`
  ViewComponent çağrısından **önce**.
- **İzinli değişiklik:** yeni bir `Master Data` `menu-header` `<li>` + iki `menu-item` `<li>`
  (`/MasterData/Brands`, `/MasterData/Products`), mevcut Commercial Suite bloklarındaki **birebir aynı** desenle:
  header yalnız gruptaki ilk görünür öğe tarafından bir kez render edilir.

```cshtml
@* MOD-0290-FU02 — Brand / Product Master (Master Data). FE-B UX guard only: shown with
   mdm.brands.read / mdm.products.read; MdmService ([Authorize] + HasPermission) stays authoritative. *@
@if (Perms.Has("mdm.brands.read"))
{
    <li class="menu-header small text-uppercase">
        <span class="menu-header-text">@SharedLocalizer["MasterData"]</span>
    </li>
    <li class="menu-item @(currentPath.StartsWith("/MasterData/Brands", StringComparison.OrdinalIgnoreCase) ? "active" : "")">
        <a href="/MasterData/Brands" class="menu-link">
            <i class="menu-icon tf-icons bx bx-purchase-tag"></i>
            <div>@SharedLocalizer["BrandsMenu"]</div>
        </a>
    </li>
}
@if (Perms.Has("mdm.products.read"))
{
    @if (!Perms.Has("mdm.brands.read"))
    {
        <li class="menu-header small text-uppercase">
            <span class="menu-header-text">@SharedLocalizer["MasterData"]</span>
        </li>
    }
    <li class="menu-item @(currentPath.StartsWith("/MasterData/Products", StringComparison.OrdinalIgnoreCase) ? "active" : "")">
        <a href="/MasterData/Products" class="menu-link">
            <i class="menu-icon tf-icons bx bx-package"></i>
            <div>@SharedLocalizer["ProductsMenu"]</div>
        </a>
    </li>
}
```

- **Guard:** canonical `mdm.brands.read` / `mdm.products.read`. RBAC fallback nedeniyle claim yoksa mevcut
  frontend resolver davranışı **raporlanır**; seed/grant yapılmaz, genişleten yeni resolver yazılmaz.
- **Label:** yedi dilli `MasterData` · `BrandsMenu` · `ProductsMenu` shared key'leri; hardcoded görünür metin yok.
- **Yasak:** layout yapısı, mevcut menü öğeleri, `Commercial Suite` blokları, `DynamicModuleMenu` ViewComponent,
  token/cookie akışı, navigation API, CSS/JS bundle, impersonation veya shell davranışı değişikliği.

**MOD-0285 değerlendirmesi:** dynamic navigation loader incelendi. Descriptor publish/self-registration Platform
ve backend descriptor değişikliği gerektirir; FU02 bunu **açmaz**. İleride MOD-0285 data-driven migration
yapılırsa hardcoded Brand/Product `<li>`'lerinin kaldırılması ayrı follow-up'tır (F7); **çift menü kabul edilmez**.

### 6.2 Diğer protected alanlar

- `.antigravity/**`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` (FROZEN) · `_LayoutPlatformAdmin.cshtml`
- `frontend/Diten.Web/Controllers/Archive/**` · `frontend/Diten.Web/Views/Archive/**` (FROZEN)
- `services/Diten.CrmService/**` — **CRM bu pack'te hiç değişmez**
- `services/Diten.AuthService/**` · `services/Diten.Platform/**` · `services/Diten.HcmService/**` ·
  `services/Diten.EnterpriseStrategyService/**` · `services/Diten.DevEnablementService/**`
- `services/Diten.MdmService/**` içinde **LegalEntity ve Lookups feature'ları** — okunabilir referans, değişmez
- `gateway/Diten.ApiGateway/ocelot.json` — §15 dışındaki her satır; özellikle mevcut `/api/legal-entities` çifti
- `execution/registries/**` — module-id registry ve implementation-status yazımı bu pack'te yok
- Auth seed/grant dosyaları, migrations, Mongo verisi
- MOD-0048 reference-data set/publish yolları
- CRM Accounts / Contacts / Territory / Campaigns frontend dosyaları — yalnız okunabilir referans

## 7. Dependencies

- **MOD-0290-FU01** — alan sözleşmesi (§3/§4), hiyerarşi kararları (§4.1), Indication/ATC/TherapeuticArea sınırı
  (§5), lifecycle (§11) ve `ExternalReferences[]` politikası (§12). FU02 bunları uygular, **değiştirmez**.
- **MOD-0220 / `Diten.MdmService`** — çalışan precedent: tenant-scoped `EntityBase`, `TenantId` server-side,
  `[Authorize]` + `[HasPermission("mdm.…")]`, `CustomBaseController`, `Response<T>` envelope, Handlers
  `CommandHandlers`/`QueryHandlers` ayrımı, `{Module}Models.cs` tek dosya.
- **MOD-0018** — permission evaluation; FU02 yalnız tüketir, RBAC engine kurmaz, seed/grant yapmaz.
- **MOD-0048** — vocabulary reconciliation hedefi (F4); publish/write **yapılmaz**.
- **MOD-0288 / MOD-0151** — `BusinessUnitId` scope vokabüleri; referanslanır, yeniden üretilmez.
- **MOD-0162-FU01C** — `TherapeuticAreaId` ConceptNode boundary; flat set açılmaz.
- **MOD-0285** — mevcut tenant navigation; runtime değişmez.
- **DEV-0001 Golden Reference Compact** — list + full-page Create/Edit/Details + `_Form`.
- **DEV-0000 Golden Reference Slim** — archive confirmation, toast, alt-canvas davranışı.
- Frontend altyapısı: `_LayoutTenantShell`, `IPermissionSnapshot`, `PermissionClaims.HasPermission`,
  controller `RequirePage` pattern'i, `window.showConfirm`, `window.showToast`, `window.L10n` bridge.

## 8. Runtime Constraints

- **Servis portu:** `Diten.MdmService` → `5059`. **Gateway portu:** `5000`.
- Browser JS ve MVC proxy tüm business çağrılarını **Gateway `5000`** üzerinden yapar.
  Direct `http(s)://localhost:5059` veya herhangi bir `:5059` business URL **yasaktır**.
- Same-origin MVC proxy tercih edilir; HttpOnly access token server-side Gateway isteğine aktarılır.
- `TenantId` hiçbir DTO/request/form/JSON payload'ında **oluşturulmaz ve gönderilmez**; mevcut auth
  header/claim akışı korunur. Payload'da `tenantId` gelirse **yok sayılır** (server claim kazanır) ve bu
  davranış testle sabitlenir.
- Cross-tenant read/write **fail-closed 404**.
- Lifecycle **archive endpointleriyle** yürür; `DELETE` hiçbir katmanda kullanılmaz.
- `Response<T>` + `reasonCode` + `correlationId` envelope pattern'i korunur.
- Contract okunamazsa UI action'ları **varsayılan olarak kapalıdır** (fail-closed) ve kontrollü error state gösterilir.
- Contract flag'i yok/false ise ilgili action **hide veya disable** edilir; yasak capability türetilmez.
- Backend'in desteklemediği filtre **client-side fake filter olarak uygulanmaz**; disabled/omitted edilir ve
  evidence'a limitation yazılır.
- Unknown/future response alanları sessizce **ignore** edilir; onlardan yeni feature açılmaz.
- Master resolve edilemeyen referans ID'ler (OwnerCompanyId, BusinessUnitId, TherapeuticAreaId, IndicationRefs)
  **format-level** gösterilir; sahte display resolution yapılmaz.

## 9. Layout & Shell Contract

- `shell: tenant`.
- **Tüm** Brand ve Product Razor sayfalarında **açıkça** `Layout = "_LayoutTenantShell";` yazılır;
  `_ViewStart.cshtml` varsayılanına güvenilmez.
- View kökleri: `frontend/Diten.Web/Views/MasterData/Brands/` ve `frontend/Diten.Web/Views/MasterData/Products/`.
- MVC route'ları ve deep link'ler:

```text
/MasterData/Brands
/MasterData/Brands/Create
/MasterData/Brands/Edit/{brandId}
/MasterData/Brands/Details/{brandId}
/MasterData/Products
/MasterData/Products/Create
/MasterData/Products/Edit/{productId}
/MasterData/Products/Details/{productId}
```

- Breadcrumb mevcut tenant-shell pattern'iyle `Master Data → Brands|Products → Detail/Create/Edit` gösterir.
- Yeni shell, table, modal, toast veya breadcrumb pattern'i **icat edilmez**.
- §6.1 dar istisnası dışında shared layout **değiştirilmez**.

## 10. Backend File Convention

Golden Reference Compact backend yapısı **birebir** (`services/Diten.DevEnablementService/.../Features/GoldenReferenceCompact/`)
ve çalışan MDM precedent'i (`Features/LegalEntity/`) ile aynı:

```text
services/Diten.MdmService/src/Diten.MdmService.Application/Features/Brand/
├── Commands/
│   ├── CreateBrandCommand.cs              (sealed record, IRequest<Response<Guid>>)
│   ├── UpdateBrandCommand.cs              (sealed record, IRequest<Response<NoContent>>)
│   └── ArchiveBrandCommand.cs             (sealed record, IRequest<Response<NoContent>>)
├── Queries/
│   ├── GetBrandListQuery.cs               (sealed record)
│   ├── GetBrandByIdQuery.cs               (sealed record)
│   └── GetBrandProductsQuery.cs           (sealed record)
├── Handlers/
│   ├── CommandHandlers/                   ← AYRI klasör (zorunlu)
│   │   ├── CreateBrandHandler.cs          (sealed class, Command suffix YOK)
│   │   ├── UpdateBrandHandler.cs
│   │   └── ArchiveBrandHandler.cs
│   └── QueryHandlers/                     ← AYRI klasör (zorunlu)
│       ├── GetBrandListHandler.cs
│       ├── GetBrandByIdHandler.cs
│       └── GetBrandProductsHandler.cs
├── Validators/
│   ├── CreateBrandValidator.cs            (Command suffix YOK)
│   └── UpdateBrandValidator.cs
└── BrandModels.cs                         ← TEK dosyada tüm DTO/ViewModel'ler

services/Diten.MdmService/src/Diten.MdmService.Application/Features/Product/
├── Commands/{CreateProductCommand,UpdateProductCommand,ArchiveProductCommand}.cs
├── Queries/{GetProductListQuery,GetProductByIdQuery}.cs
├── Handlers/CommandHandlers/{CreateProductHandler,UpdateProductHandler,ArchiveProductHandler}.cs
├── Handlers/QueryHandlers/{GetProductListHandler,GetProductByIdHandler}.cs
├── Validators/{CreateProductValidator,UpdateProductValidator}.cs
└── ProductModels.cs

services/Diten.MdmService/src/Diten.MdmService.Application/Features/BrandProductContract/
├── Queries/GetBrandProductContractQuery.cs
├── Handlers/QueryHandlers/GetBrandProductContractHandler.cs
└── BrandProductContractModels.cs
```

**Naming (tartışmasız):** Command `{Verb}{Module}Command` (record) · Query `Get{Module}{Qualifier}Query` (record) ·
Handler `{Verb}{Module}Handler` (class, **Command/Query suffix YOK**) · Validator `{Verb}{Module}Validator`
(**Command suffix YOK**).

**Yasaklar:** tek dosyada birden fazla `public class`/`record` (`{Module}Models.cs` hariç) ·
`*CommandHandler.cs`/`*QueryHandler.cs` suffix'i · `CommandHandlers`/`QueryHandlers` ayrımını yapmamak ·
`Requests/Commands/` gibi ekstra alt klasör · **`Delete*` / `BulkDelete*` komut veya handler'ı** (§3).

## 11. Frontend File Contract

`golden_reference: compact` → her iki modül için tam Compact seti:

```text
frontend/Diten.Web/
├── Controllers/MasterData/
│   ├── BrandsController.cs
│   └── ProductsController.cs
├── Models/MasterData/
│   ├── BrandViewModels.cs
│   └── ProductViewModels.cs
├── Views/MasterData/Brands/
│   ├── Index.cshtml                  (Layout = "_LayoutTenantShell" AÇIKÇA)
│   ├── Create.cshtml
│   ├── Edit.cshtml
│   ├── Details.cshtml
│   ├── _Form.cshtml                  (Create/Edit ortak)
│   ├── _Filter.cshtml
│   ├── _DataTable.cshtml             (data-dt-standard="v2" + skeleton)
│   ├── _ProductsDataTable.cshtml     (Brand detail → Products tab)
│   ├── _ExternalReferences.cshtml    (Create/Edit/Details ortak repeater)
│   ├── _IndexL10n.cshtml
│   └── BrandsIndex.cs                (marker class)
├── Views/MasterData/Products/
│   ├── Index.cshtml
│   ├── Create.cshtml
│   ├── Edit.cshtml
│   ├── Details.cshtml
│   ├── _Form.cshtml
│   ├── _Filter.cshtml
│   ├── _DataTable.cshtml
│   ├── _ExternalReferences.cshtml
│   ├── _IndexL10n.cshtml
│   └── ProductsIndex.cs
├── wwwroot/assets/js/MasterData/Brands/
│   ├── index.js
│   ├── index.l10n.js
│   ├── form.js
│   └── details.js
├── wwwroot/assets/js/MasterData/Products/
│   ├── index.js
│   ├── index.l10n.js
│   ├── form.js
│   └── details.js
└── Resources/Views/MasterData/{Brands,Products}/
    └── {Brands,Products}Index.{en,fr,es,zh,ar,ru,tr}.resx
```

**Compact zorunlulukları**

- `_CreateEditOffcanvas.cshtml` ve `_DetailsQuickView.cshtml` **YASAK**; Create/Edit/Details **full-page**.
- Index içinde create/edit offcanvas **yasak**.
- Partial path'leri absolute: `~/Views/MasterData/Brands/_Filter.cshtml`.
- Bölüm sırası: ① `_Filter` → ② BulkActionBar (yalnız non-destructive; delete/bulk-delete **yok**) →
  ③ `_DataTable`.
- Her partial çağrısından önce verifier'ın aradığı **contract marker yorum satırı**.
- Tüm tablolarda `data-dt-standard="v2"`, skeleton loader, toolbar, filter, pagination, sort, save-view marker
  ve loading/empty/error state.
- Archive confirmation `window.showConfirm`; toast `window.showToast`. Ham `alert`, `confirm` veya doğrudan
  `Swal.fire` **yasak**.
- `index.l10n.js` camelCase→PascalCase key dönüşümünü yapar; aksi hâlde `window.L10n` key'leri `undefined` döner.
- `.resx` değişiklikleri **tam fleet restart** ister; kısmi reload yeterli değildir.
- `_ProductsDataTable.cshtml` yalnız `GET /api/mdm/brands/{brandId}/products` üzerinden beslenir; ürün mutasyonu
  Brand detayından yapılmaz (link `/MasterData/Products/Details/{productId}`'ye gider).

## 12. Validation Rules

### Brand

| Field | Required | Format/Rule | DB-level | Pre-check |
|---|---|---|---|---|
| `BrandCode` | Evet (create) | Trim, max 64, `^[A-Za-z0-9._-]+$`, upper-normalize; **update'te immutable** | Unique `(TenantId, BrandCode)` | `ExistsByBrandCodeAsync` |
| `BrandName` | Evet | Trim, max 200, boş/whitespace reddedilir | — | — |
| `BrandStatus` | Evet | §4.3 vokabüleri; `archived` payload ile set edilemez | — | Vocabulary check |
| `Description` | Hayır | Max 2000 | — | — |
| `OwnerCompanyId` | Hayır | Boş veya geçerli GUID | — | Master resolve **yok** |
| `BusinessUnitId` | Hayır | Boş veya geçerli GUID | — | Master resolve **yok** |
| `TherapeuticAreaId` | Hayır | Boş veya geçerli GUID (ConceptNode ref) | — | Flat set lookup **yok** |
| `EffectiveFrom` | Evet | Geçerli tarih | — | — |
| `EffectiveTo` | Hayır | Varsa `EffectiveTo >= EffectiveFrom`; `.Date` üzerinden karşılaştır | — | — |
| `ExternalReferences[]` | Hayır | §4.4; `SourceSystem` başına tek `IsPrimary` | — | Primary uniqueness pre-check |
| `TenantId` | **Forbidden** | Payload'da bulunmaz; gelirse yok sayılır | — | — |

### Product

| Field | Required | Format/Rule | DB-level | Pre-check |
|---|---|---|---|---|
| `ProductCode` | Evet (create) | Trim, max 64, `^[A-Za-z0-9._-]+$`, upper-normalize; **update'te immutable** | Unique `(TenantId, ProductCode)` | `ExistsByProductCodeAsync` |
| `ProductName` | Evet | Trim, max 200 | — | — |
| `ProductStatus` | Evet | §4.3 vokabüleri; `archived` payload ile set edilemez | — | Vocabulary check |
| `BrandId` | **Hayır** | Boş veya geçerli GUID; doluysa brand **aynı tenant + archived değil** | — | `GetBrandForLinkAsync` (same-tenant, not-archived) |
| `ProductType` | Hayır | §4.3 vokabüleri | — | Vocabulary check |
| `DosageForm` | Hayır | §4.3 vokabüleri | — | Vocabulary check |
| `Strength` | Hayır | Max 100 | — | — |
| `PackSize` | Hayır | Max 100 | — | — |
| `UnitOfMeasure` | Hayır | §4.3 vokabüleri | — | Vocabulary check |
| `ATCCode` | Hayır | Max 16, `^[A-Za-z0-9]+$`; **format-level yalnız** — ATC master lookup'ı **yok** | — | — |
| `TherapeuticAreaId` | Hayır | Boş veya geçerli GUID | — | Flat set lookup **yok** |
| `IndicationRefs[]` | Hayır | GUID listesi; duplicate reddedilir | — | Master resolve **yok** |
| `Description` | Hayır | Max 2000 | — | — |
| `EffectiveFrom` | Evet | Geçerli tarih | — | — |
| `EffectiveTo` | Hayır | Varsa `EffectiveTo >= EffectiveFrom` | — | — |
| `ExternalReferences[]` | Hayır | §4.4 | — | Primary uniqueness pre-check |
| `TenantId` | **Forbidden** | Payload'da bulunmaz | — | — |

Client validation erken geri bildirimdir; backend validation ve `reasonCode`'lar **korunur ve gösterilir**.

## 13. Failure Path to Verify

- **Duplicate `BrandCode` (aktif veya archived)**
  → 409 + `reasonCode` + field-level UI hatası + kayıt oluşmaz + reload sonrası temiz state.
- **Duplicate `ProductCode` (aktif veya archived)**
  → 409 + aynı davranış.
- **Missing `BrandName` / `ProductName` / status / `EffectiveFrom`**
  → 400 + validator mesajı + submit engellenir.
- **Unknown `BrandStatus` / `ProductStatus` / `ProductType` / `DosageForm` / `UnitOfMeasure`**
  → 400 + vocabulary `reasonCode`; UI seçenekleri contract'tan geldiği için normalde erişilemez.
- **`EffectiveTo < EffectiveFrom`**
  → 400; UI tarih alanına bağlı localized validation.
- **Archived brand update denemesi**
  → 409 + "archived record is read-only"; UI'da edit action disabled, yarış durumundaki 409 ayrıca görünür.
- **Archived product update denemesi**
  → 409 + aynı davranış.
- **Product create/update ile archived brand'e bağlanma**
  → 409 + `brand_archived` reasonCode; product **yazılmaz**.
- **Product create/update ile başka tenant'ın brand'ine bağlanma**
  → 404 (fail-closed; varlık sızdırılmaz).
- **Cross-tenant brand/product read/update/archive**
  → 404; kayıt varlığı ifşa edilmez.
- **`DELETE /api/mdm/brands/{id}` veya `/products/{id}`**
  → 404/405; hiçbir katmanda `DELETE` route/handler/client kodu yoktur.
- **İkinci `IsPrimary` external reference (aynı `SourceSystem`)**
  → 409 + `external_reference_primary_conflict`; silent merge **yok**.
- **Unauthorized actor (permission yok)**
  → 403; UI action disabled veya permission-denied state; menü girdisi görünmez.
- **Contract endpoint 401/403/5xx/timeout**
  → UI kontrollü error state; tüm capability action'ları **fail-closed** kapalı.
- **Concurrency — aynı kaydın eşzamanlı update'i**
  → 409 + "data changed, reload required"; **sessiz overwrite YOK**.
- **Brand archive sonrası mevcut product'lar**
  → Product'lar **archive edilmez ve silinmez**; listede görünür kalır; yeni link 409 verir (sessiz cascade yasak).
- **Unknown backend response alanı**
  → sessizce ignore; visit/route/campaign/frequency/recommendation feature'ı **açılmaz**.

## 14. Authorization Convention

```text
Policy:     [Authorize]                                  // shell: tenant, MdmService
Permission: [HasPermission("mdm.{resource}.{action}")]   // PKS-001 lowercase-dotted, >= 3 segment
Actor type: tenant_user  (actor_type=platform_admin mevcut runtime davranışı gereği bypass eder)
```

**Canonical permission listesi**

| Yüzey | Permission |
|---|---|
| Brand list / detail / products relation | `mdm.brands.read` |
| Brand create | `mdm.brands.create` |
| Brand update | `mdm.brands.update` |
| Brand archive | `mdm.brands.archive` |
| Product list / detail | `mdm.products.read` |
| Product create | `mdm.products.create` |
| Product update | `mdm.products.update` |
| Product archive | `mdm.products.archive` |
| Contract endpoint | `mdm.brands.read` **veya** `mdm.products.read` (biri yeterli) |

**Kararlar**

- **Ayrı Brand/Product permission** seçildi (birleşik `mdm.brand-product.*` **değil**): Brand ve Product iki ayrı
  aggregate, iki ayrı controller ve iki ayrı menü sayfasıdır; birleşik anahtar, ürün yöneticisine marka yazma
  yetkisi vermek zorunda kalırdı. Bu, FU01 §14'ün "tüketiciler yalnız `*.read` ister" kuralıyla da uyumludur.
- **Çoğul resource segmenti** (`brands`/`products`) shipped `mdm.legal-entities.*` precedent'ini izler (D1).
- `archive` PKS-001 **Tier-2** onaylı aksiyondur; `delete` **kullanılmaz**.
- Tüketici modüller (CRM/Knowledge/Campaign) ileride yalnız `mdm.brands.read` / `mdm.products.read` ister.
- **Seed/grant bu pack'te yapılmaz.** Canonical key'ler kataloğa eklenmediği için ilk smoke'ta yalnız
  `actor_type=platform_admin` veya mevcut fallback ile erişim mümkün olabilir; bu durum **PARTIAL/follow-up**
  olarak evidence'a yazılır, hardcoded allow veya genişletilmiş resolver yazılmaz (F3).
- UI action görünürlüğü permission'a göre **hide veya disable** edilir; backend guard her hâlükârda otoritedir.

## 15. Gateway / API Routing Decision

**Karar: Gateway değişikliği GEREKLİ ve bu pack tarafından DAR KAPSAMDA yetkilendirilmiştir.**

Mevcut `ocelot.json` taraması: `/api/mdm/*` route'u **yoktur**; MDM'in tek rotası `/api/legal-entities` çiftidir.
Catch-all `/api/mdm/{everything}` de yoktur. Dolayısıyla explicit route eklenmeden UI çalışamaz.

### 15.1 API contract (yetkilendirilen endpointler)

```text
GET    /api/mdm/brands
GET    /api/mdm/brands/{brandId}
POST   /api/mdm/brands
PUT    /api/mdm/brands/{brandId}
POST   /api/mdm/brands/{brandId}/archive
GET    /api/mdm/brands/{brandId}/products

GET    /api/mdm/products
GET    /api/mdm/products/{productId}
POST   /api/mdm/products
PUT    /api/mdm/products/{productId}
POST   /api/mdm/products/{productId}/archive

GET    /api/mdm/brand-products/contract
```

- **`DELETE` yoktur** — hiçbir upstream method listesinde bulunmaz.
- Controller route'ları downstream ile birebir: `[Route("api/mdm/brands")]` · `[Route("api/mdm/products")]` ·
  `[Route("api/mdm/brand-products")]`.
- `TenantId` payload yok; `X-Tenant-Id` header + JWT claim akışı korunur.
- `Response<T>` / `reasonCode` / `correlationId` envelope korunur.
- `201 Created` yanıtlarında `Location` header'ı Gateway adresini gösterir (`PublicBaseUrl`).

### 15.2 İzinli `ocelot.json` route'ları (tam liste — beş blok)

| Upstream | Downstream | Host:Port | Methods |
|---|---|---|---|
| `/api/mdm/brands` | `/api/mdm/brands` | `localhost:5059` | `GET, POST, OPTIONS` |
| `/api/mdm/brands/{everything}` | `/api/mdm/brands/{everything}` | `localhost:5059` | `GET, POST, PUT, OPTIONS` |
| `/api/mdm/products` | `/api/mdm/products` | `localhost:5059` | `GET, POST, OPTIONS` |
| `/api/mdm/products/{everything}` | `/api/mdm/products/{everything}` | `localhost:5059` | `GET, POST, PUT, OPTIONS` |
| `/api/mdm/brand-products/contract` | `/api/mdm/brand-products/contract` | `localhost:5059` | `GET, OPTIONS` |

**Kurallar**

- Downstream **yalnız** `Diten.MdmService` → port **5059**.
- `DELETE` ve `PATCH` **listelenmez** (`PATCH` bu modülde kullanılmaz; `DELETE` yasaktır). NET-001'in "tüm
  metotları listele" tavsiyesinden bilinçli sapmadır: hard-delete yasağı transport katmanında da uygulanır.
- `OPTIONS` **zorunlu** (CORS preflight; aksi hâlde DataTables "Ajax error (tn/7)").
- Explicit route'lar catch-all rotalardan **ÖNCE** konumlanır.
- Mevcut hiçbir route (özellikle `/api/legal-entities` ve tüm `/api/crm/*`) **değiştirilmez veya taşınmaz**.
- Gateway dışı direct frontend call **yasak**.
- Bu değişiklik `integration-agent` sorumluluğundadır; orchestrator bu beş blok dışında `ocelot.json`'a dokunmaz.

## 16. Contract Flags

**Endpoint:** `GET /api/mdm/brand-products/contract`

### 16.1 Yayımlanan capability flag'leri

```json
{
  "supportsBrandManagement": true,
  "supportsProductManagement": true,
  "supportsBrandProductReference": true,
  "supportsBrandProductHierarchy": true,
  "supportsExternalReferences": true,
  "supportsArchiveLifecycle": true,
  "supportsEffectiveDating": true,
  "supportsContractDrivenUi": true
}
```

### 16.2 Contract payload'ının diğer blokları

- `vocabulary`: `brandStatus` · `productStatus` · `productType` · `dosageForm` · `unitOfMeasure` değer listeleri.
- `reasonCodes`: validation/conflict reason code sözlüğü.
- `permissions`: §14 canonical key listesi (bilgi amaçlı; enforcement backend'dedir).
- `limitations`: desteklenmeyen filtreler, resolve edilmeyen referanslar, MOD-0048 reconciliation durumu.

### 16.3 Yasak flag'ler — **false olarak bile yayımlanmaz; hiç bulunmaz**

```text
supportsCampaignRuntime          supportsCampaignEngine
supportsKnowledgeRuntime         supportsVisitPlanning
supportsRoutePlanning            supportsFrequencyRuntime
supportsRecommendationEngine     supportsDigitalDetailing
supportsWorkflowApproval         supportsSegmentationEngine
supportsAtcLocalMaster           supportsTherapeuticAreaFlatReferenceSet
supportsIndicationMaster         supportsItemSkuMaster
supportsUomMapping               supportsImportExport
supportsHardDelete               supportsMultiBrandProduct
```

MOD-0162 / MOD-0165 / MOD-0164 / MOD-0151 flag setleri **değişmez**.

## 17. Brand UI Scope

**List kolonları:** `BrandCode` · `BrandName` · `BrandStatus` · `BusinessUnitId` · `TherapeuticAreaId` ·
`EffectiveFrom` · `IsArchived` · `UpdatedAt` · Actions

**Filtreler:** `Search` (code/name) · `BrandStatus` · `BusinessUnitId` · `TherapeuticAreaId` · `IncludeArchived`

> Backend'in gerçekten desteklemediği bir filtre çıkarsa **fake client-side filter uygulanmaz**; alan disabled
> edilir ve evidence'a limitation yazılır.

**Detail bölümleri:** `Summary` · `References` (OwnerCompany/BusinessUnit/TherapeuticArea, format-level) ·
`External References` · `Products` tab · `Audit / provenance` (Created/Updated/Archived by-at)

**Create/Edit alanları (10):** `BrandCode` · `BrandName` · `BrandStatus` · `Description` · `OwnerCompanyId` ·
`BusinessUnitId` · `TherapeuticAreaId` · `EffectiveFrom` · `EffectiveTo` · `ExternalReferences`

**Kurallar**

- `TenantId` **gönderilmez**.
- `BrandCode` · `BrandName` · `BrandStatus` · `EffectiveFrom` **required**.
- `BrandCode` edit ekranında **read-only/immutable**.
- Archived brand'de edit **disabled**; archive action **disabled**.
- Archive **POST** `/archive` endpointini kullanır; `DELETE` yok.
- Product cascade delete/archive **yok**; Products tab'ında yıkıcı aksiyon sunulmaz.
- Products tab boşsa Golden empty-state; API destekli değilse "limitation" mesajı gösterilir, sahte satır yok.

## 18. Product UI Scope

**List kolonları:** `ProductCode` · `ProductName` · `ProductStatus` · `BrandId` · `ProductType` · `DosageForm` ·
`Strength` · `PackSize` · `UnitOfMeasure` · `ATCCode` · `TherapeuticAreaId` · `EffectiveFrom` · `IsArchived` ·
`UpdatedAt` · Actions

> Kolon yoğunluğu nedeniyle `Strength` · `PackSize` · `UnitOfMeasure` · `ATCCode` · `TherapeuticAreaId` ·
> `EffectiveFrom` varsayılan olarak **colvis ile gizlenebilir**; kolon seti yine de tam sunulur.

**Filtreler:** `Search` (code/name) · `ProductStatus` · `BrandId` · `ProductType` · `DosageForm` ·
`TherapeuticAreaId` · `IncludeArchived`

**Detail bölümleri:** `Summary` · `Brand reference` · `Pharma metadata` (ProductType/DosageForm/Strength/
PackSize/UnitOfMeasure/ATCCode/TherapeuticArea/IndicationRefs) · `External References` · `Audit / provenance`

**Create/Edit alanları (16):** `ProductCode` · `ProductName` · `ProductStatus` · `BrandId` · `ProductType` ·
`DosageForm` · `Strength` · `PackSize` · `UnitOfMeasure` · `ATCCode` · `TherapeuticAreaId` · `IndicationRefs` ·
`Description` · `EffectiveFrom` · `EffectiveTo` · `ExternalReferences`

**Kurallar**

- `TenantId` **gönderilmez**.
- `ProductCode` · `ProductName` · `ProductStatus` · `EffectiveFrom` **required**.
- `BrandId` **optional** (FU01 §4.1); brand seçimi aktif, archived olmayan brand'lerden yapılır. Brand seçici
  `GET /api/mdm/brands?brandStatus=active` üzerinden beslenir; **hardcoded liste yasak**.
- `ProductCode` edit ekranında **read-only/immutable**.
- Archived product'ta edit **disabled**.
- Archive **POST** `/archive` endpointini kullanır; `DELETE` yok.
- `ATCCode` alanının yanında **help text**: dış taksonomi referansıdır, lokal ATC master değildir.
- `TherapeuticAreaId` alanının yanında **help text**: concept/reference'tır, flat lokal set değildir.
- Brand reference bölümü brand adını gösterebilir (detail response'unda geliyorsa); gelmiyorsa **GUID
  format-level** gösterilir, sahte resolution yapılmaz.

## 19. Response Shape / Data Guard

Brand/Product response'larında ve UI'da şu alanlar **beklenmez, gösterilmez ve bunlardan feature üretilmez**:

```text
campaignTargetId          visitPlanId               routePlanId
routeId                   dueStatus                 overdue
lastVisitDate             requiredVisitCount        periodType
frequencyPolicyId         segmentMembership         knowledgeContentPayload
contentRenderUrl          recommendationId          nextBestAction
workflowApprovalId        consentRecordPayload      preferenceRecordPayload
patientId                 skuId                     uomMappingId
```

Bunlar future/unknown olarak **sessizce ignore** edilir. Consent/Preference/Knowledge payload'ları hiçbir DOM,
view model, log, toast veya detail paneline taşınmaz.

## 20. RESX / Localization

**Yedi dil parity zorunlu:** `en` · `fr` · `es` · `zh` · `ar` · `ru` · `tr`

**Dosyalar**
- `Resources/Views/MasterData/Brands/BrandsIndex.{lang}.resx`
- `Resources/Views/MasterData/Products/ProductsIndex.{lang}.resx`
- `Resources/SharedResource.{lang}.resx` — yalnız `MasterData` · `BrandsMenu` · `ProductsMenu` menü key'leri

**Key grupları**

| Grup | Kapsam |
|---|---|
| Menu | `MasterData` · `BrandsMenu` · `ProductsMenu` |
| Brand ekranları | list · detail · create · edit · archive başlık/aksiyon metinleri |
| Product ekranları | list · detail · create · edit · archive başlık/aksiyon metinleri |
| Brand alanları | §17 create/edit + list kolon etiketleri |
| Product alanları | §18 create/edit + list kolon etiketleri |
| Status etiketleri | `draft` · `active` · `inactive` · `archived` (Brand + Product) |
| Product type etiketleri | `medicine` · `medical-device` · `service` · `training-material` · `other` |
| DosageForm / UoM etiketleri | contract vokabülerine karşılık gelen görünür etiketler |
| External references | tablo başlıkları, `IsPrimary`, ekle/kaldır aksiyonları, primary-conflict mesajı |
| Archive modal | başlık, gövde, onay/iptal, geri alınamazlık uyarısı |
| Validation | required, max-length, format, tarih aralığı, immutable-code, vocabulary, duplicate-code, archived-readonly, archived-brand-link |
| State | empty · loading · error · no-permission · contract-unavailable |
| Toast | create/update/archive başarı ve hata mesajları |
| Help text | `ATCCode` dış taksonomi referansıdır · `TherapeuticArea` concept/reference'tır, flat lokal set değildir |

- Hardcoded görünür metin **yasak**.
- `index.l10n.js` camelCase→PascalCase dönüşümünü yapmalıdır; aksi hâlde `window.L10n` key'leri `undefined`
  döner ve toast `(undefined: <corrId>)` gösterir.
- RESX değişiklikleri **tam fleet restart** gerektirir.

## 21. Acceptance Criteria

**Governance / boundary**

- [ ] Pack `execution/domains/master-data-management/module-packs/` altındadır; **CRM tarafında hiçbir dosya değişmemiştir**.
- [ ] `services/Diten.CrmService/**`, `execution/registries/**`, MOD-0048 set/publish yolları ve Mongo verisi değişmemiştir.
- [ ] MOD-0155, Campaign, Consent, Knowledge, Frequency, Visit/Route Planning runtime ve UI'ları değişmemiştir.
- [ ] Item/SKU/UoM mapping/identifier yönetimi, multi-brand product ve ürün ailesi hiyerarşisi açılmamıştır.

**Backend**

- [ ] `Brand` ve `Product` aggregate'leri `EntityBase`'den türer; `TenantId` server-side çözülür ve hiçbir DTO'da yoktur.
- [ ] Backend klasör/naming yapısı §10 ile birebirdir; `Handlers/CommandHandlers` ve `Handlers/QueryHandlers` ayrımı vardır.
- [ ] Handler/Validator isimlerinde `Command`/`Query` suffix'i **yoktur**.
- [ ] `Delete*` veya `BulkDelete*` command/handler/endpoint **hiç üretilmemiştir**.
- [ ] `(TenantId, BrandCode)` ve `(TenantId, ProductCode)` unique index'leri vardır ve partial filter'da `$ne` **kullanmaz**.
- [ ] `Brand` ve `Product` `RegisterClassMaps`'e eklenmiştir (Guid string-representation kaydı).
- [ ] `EffectiveFrom`/`EffectiveTo` birlikte index'lenmemiş ve birlikte sort edilmemiştir.
- [ ] §15.1'deki 12 endpoint çalışır ve `Response<T>`/`reasonCode`/`correlationId` envelope'unu korur.
- [ ] Cross-tenant erişim 404 döner.
- [ ] Archived kayıt okunur; update 409 döner.
- [ ] Archived brand'e product bağlama 409 döner; brand archive **cascade yapmaz**.
- [ ] `ATCCode` yalnız string pointer olarak saklanır; ATC master/lookup collection'ı **yoktur**.
- [ ] `TherapeuticAreaId` yalnız reference olarak saklanır; flat reference set **yaratılmamıştır**.
- [ ] Contract endpointi §16.1'deki 8 flag'i `true` döner ve §16.3'teki yasak flag'lerden **hiçbirini içermez**.

**Gateway**

- [ ] `ocelot.json`'a yalnız §15.2'deki beş route eklenmiştir; downstream port **5059**'dur.
- [ ] Hiçbir route'ta `DELETE` methodu yoktur.
- [ ] Tüm route'larda `OPTIONS` vardır.
- [ ] Mevcut `/api/legal-entities` ve `/api/crm/*` route'ları değişmemiştir; gateway build/başlatma PASS'tir.

**Frontend**

- [ ] `/MasterData/Brands` ve `/MasterData/Products` route'ları render olur; Create/Edit/Details deep link'leri çalışır.
- [ ] **Tüm** `Views/MasterData/{Brands,Products}/*.cshtml` dosyalarında `Layout = "_LayoutTenantShell"` **açıkça** yazılıdır.
- [ ] `_LayoutTenantShell.cshtml` değişikliği yalnız §6.1 dar istisnasıdır; `Master Data` header'ı çift render olmaz.
- [ ] Menü girdileri `mdm.brands.read` / `mdm.products.read` ile guard'lıdır; permission yokken görünmez.
- [ ] Compact dosya seti tamdır; `_CreateEditOffcanvas.cshtml` ve `_DetailsQuickView.cshtml` **yoktur**.
- [ ] Her iki DataTable `data-dt-standard="v2"`, skeleton, toolbar, filter, pagination, sort ve
      loading/empty/error state contract'ına uyar.
- [ ] Brand list kolonları §17, Product list kolonları §18 ile birebirdir.
- [ ] Brand detail `Products` tab'ını gösterir veya API sınırlamasını açıkça belgeler; sahte satır yoktur.
- [ ] Product detail `Brand reference`'ı gösterir; resolve edilemiyorsa GUID format-level gösterilir.
- [ ] `ATCCode` ve `TherapeuticArea` help text'leri görünürdür.
- [ ] Create/Edit Compact full-page'dir ve ortak `_Form.cshtml` kullanır; required/date/immutable validation'ları vardır.
- [ ] Archive akışı `window.showConfirm` + POST `/archive` + `window.showToast` kullanır; **`DELETE` yoktur**.
- [ ] Archived kayıtlarda edit action'ları disabled'dır.
- [ ] Contract flag'leri action'ları fail-closed enable/disable/hide eder.
- [ ] Frontend kaynağında direct `:5059`, `DELETE` client kullanımı, `TenantId` payload'ı veya §19 yasak alanı **yoktur**.
- [ ] Tüm yeni görünür metin yedi dilde RESX/L10n parity taşır.
- [ ] Brand seçici hardcoded liste değil, `GET /api/mdm/brands` üzerinden beslenir.

## 22. Test Expectations

### 22.1 Backend (minimum 24 gate)

1. Brand create — geçerli payload 201 + `BrandId` döner.
2. Brand create — payload'daki `tenantId` **yok sayılır**; kayıt JWT tenant'ına yazılır.
3. `BrandCode` duplicate (aktif) → **409**.
4. `BrandCode` duplicate (**archived** kayıt) → **409** — archived kod yeniden kullanılamaz (davranış sabitlenir).
5. Bilinmeyen `BrandStatus` → **400** + vocabulary reasonCode.
6. Brand archive → `IsArchived=true`, `ArchivedAt`/`ArchivedBy` set, kayıt **silinmez**, tekrar archive idempotent/deterministik.
7. Archived brand update → **409**.
8. Brand `DELETE` → route/handler yok; **404/405**.
9. Brand list **tenant-izole**; başka tenant'ın brand'i dönmez.
10. Product create — geçerli payload 201.
11. Product create — payload'daki `tenantId` yok sayılır.
12. `ProductCode` duplicate (aktif ve archived) → **409**.
13. Bilinmeyen `ProductStatus` / `ProductType` / `DosageForm` / `UnitOfMeasure` → **400**.
14. Product archive → soft lifecycle; kayıt okunabilir kalır.
15. Archived product update → **409**.
16. Product `DELETE` → **404/405**.
17. Product list **tenant-izole**.
18. Product create/update ile **archived brand** ilişkilendirme → **409**.
19. Product create/update ile **cross-tenant brand** ilişkilendirme → **404**.
20. `BrandId` **null** ile product create → **başarılı** (FU01 §4.1 optional kararı).
21. `ATCCode` yalnız string pointer olarak saklanır; ATC master collection/endpoint'i yoktur (statik tarama).
22. `TherapeuticAreaId` flat reference set olarak yaratılmaz (statik tarama).
23. `EffectiveTo < EffectiveFrom` → **400**.
24. İkinci `IsPrimary` external reference (aynı `SourceSystem`) → **409**.
25. Contract endpointi §16.1'in 8 flag'ini `true` döner.
26. Contract endpointi §16.3'teki yasak flag'lerin **hiçbirini** içermez (statik + runtime assert).
27. Brand archive sonrası mevcut product'lar **archive edilmemiştir** (cascade yok).
28. Campaign/Knowledge/Frequency/Visit koleksiyonlarında **hiçbir mutasyon** yoktur.
29. `dotnet build services/Diten.MdmService/**` PASS.

### 22.2 UI (minimum 18 gate)

1. `/MasterData/Brands` ve `/MasterData/Products` route'ları `_LayoutTenantShell` ile render olur.
2. Navigation girdileri permission-guarded; permission yokken menüde görünmez.
3. Contract Gateway üzerinden yüklenir; yüklenemezse action'lar fail-closed kapalıdır.
4. Brand list yüklenir; loading/empty/error state'leri Golden contract'a uyar.
5. Brand create/edit required + tarih aralığı + immutable-code validation'ı çalışır.
6. Brand archive **POST `/archive`** kullanır; `DELETE` **kullanılmaz**.
7. Product list yüklenir; filtreler render olur.
8. Product create/edit validation'ı çalışır; `BrandId` boş bırakılabilir.
9. Product archive **POST `/archive`** kullanır; `DELETE` **kullanılmaz**.
10. Brand detail Products tab'ını gösterir **veya** API sınırlamasını açıkça belgeler.
11. Product detail Brand reference ID'sini gösterir.
12. `ATCCode` / `TherapeuticArea` help text'leri görünürdür.
13. Hiçbir istek payload'ında `TenantId` yoktur (network/statik tarama).
14. Kaynak kodda direct `:5059` URL'i **yoktur**.
15. Kaynak kodda Brand/Product için `DELETE` client kullanımı **yoktur**.
16. Yedi locale dosyasında **aynı key seti** vardır (RESX parity).
17. `dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug` PASS.
18. `python3 .antigravity/scripts/verify_datatable_page.py . --area MasterData --module Brands --reference compact` PASS
    ve aynı komut `--module Products` için PASS.
19. Mevcut frontend testleri etkilenmez.

### 22.3 Authenticated Gateway smoke (hedef tenant `97c59330-dbc4-4665-b29c-0c26dbb5cc93`)

1. Fleet health (Gateway 5000 · MdmService 5059 · Auth 5056 · Platform 5057 · Web).
2. Login — `X-Tenant-Id` header ile tenant-scoped token alınır (aksi hâlde platform `…0001` token'ı gelir).
3. `GET /api/mdm/brand-products/contract` → **200** + 8 flag `true` + yasak flag yok.
4. `POST /api/mdm/brands` → **201**.
5. `POST /api/mdm/products` (yeni brand'e referansla) → **201**.
6. `GET /api/mdm/products/{productId}` → **200**, brand referansı görünür.
7. `GET /api/mdm/brands/{brandId}/products` → **200**, oluşturulan product listede.
8. `POST /api/mdm/products/{productId}/archive` → **200/204**.
9. Archived product `GET` → **200**; `PUT` → **409**.
10. `POST /api/mdm/brands/{brandId}/archive` → **200/204**.
11. Archived brand `GET` → **200**; `PUT` → **409**; archived brand'e yeni product → **409**.
12. `DELETE /api/mdm/brands/{brandId}` ve `DELETE /api/mdm/products/{productId}` → **404/405**.
13. Response shape guard temiz (§19 alanlarının hiçbiri yok).
14. Campaign/Knowledge/Frequency/Visit koleksiyonlarında mutasyon yok.
15. UI smoke: menü → list → create → detail → edit → archive → disabled mutation → locale değişimi.
16. Cleanup **yalnız archive** ile yapılır; hiçbir kayıt silinmez, Mongo hand-edit yapılmaz.

Permission seed edilmediği için smoke `actor_type=platform_admin` veya mevcut fallback ile yürütülebilir;
canonical key'lerle doğrulanamayan adımlar **açıkça deferred/PARTIAL** yazılır. Sahte veri veya Mongo
hand-edit **yapılmaz**.

## 23. Ready-for-dev Checklist

- [x] `AGENTS.md`, MDM domain-config, commercial-suite domain-config, master-development-plan ve module-id-registry okundu.
- [x] `.antigravity/rules/module-pack-standard.md` okundu; 20 zorunlu bölüm karşılandı.
- [x] `.antigravity/rules/routes.md` (NET-001) ve `permission-key-standard.md` (PKS-001) okundu.
- [x] **DCP-002 identity preflight PASS** (`MOD-0290-FU02`, `--parent MOD-0290`, exit 0).
- [x] MOD-0290-FU01 boundary pack'i ve audit raporu okundu; sapmalar §0'da açıkça listelendi.
- [x] Golden Reference Compact pack'i ve canlı `Views/DevEnablement/GoldenReferenceCompact/` dosya seti okundu.
- [x] Golden Reference Slim archive/toast davranışı referans olarak okundu.
- [x] Çalışan MDM precedent'i (`Features/LegalEntity/`, `LegalEntitiesController`, `mdm.legal-entities.*`) incelendi.
- [x] `ocelot.json` tarandı — `/api/mdm/*` route'unun **yokluğu** doğrulandı; route kararı §15'te verildi.
- [x] `_LayoutTenantShell.cshtml` navigation deseni incelendi; dar istisna dosya/section/guard düzeyinde kesinleşti.
- [x] Frontmatter tüm zorunlu alanları içeriyor (`service` · `shell` · `golden_reference` · `entity_base` · `status`).
- [x] Form alan sayımı yapıldı: Brand 10, Product 16 → **> 8** → `golden_reference: compact`.
- [x] Layout & Shell Contract'ta Razor `Layout = "_LayoutTenantShell"` **açıkça** yazılı.
- [x] Backend File Convention Golden Reference ile birebir; `Delete`/`BulkDelete` yokluğu gerekçelendirildi.
- [x] Frontend File Contract Compact dosya setini tam listeliyor; Slim-özel partial'lar yasaklandı.
- [x] Validation Rules her alan için yazıldı (Brand + Product ayrı tablo).
- [x] Failure Path duplicate · missing · unauthorized · concurrency · archived · cross-tenant · cascade senaryolarını içeriyor.
- [x] Authorization Convention canonical key listesi + policy + actor type + fallback sınırı ile yazıldı.
- [x] Gateway routing kararı açık: **gerekli**, dar kapsam, beş route, `integration-agent` sorumluluğunda.
- [x] Protected navigation kararı verildi (hardcoded dar istisna; MOD-0285 migration follow-up).
- [x] Contract flags ve yasak flag listesi yazıldı.
- [x] Response shape / data guard yazıldı.
- [x] Acceptance criteria test edilebilir maddeler hâlinde.
- [x] Test expectations backend + UI + verifier + RESX + authenticated smoke'u kapsıyor.
- [x] Explicit exclusions yazıldı; MOD-0155 açıkça dışarıda.
- [x] Kullanıcı yerleşim kararını (MDM) ve `ready-for-dev` hedefini açıkça onayladı.

## 24. Implementation Notes

- **CRM'e hiç dokunulmaz.** Bu pack CRM tarafında tek bir dosya bile değiştirmez; Campaign'in `BrandId`/`ProductId`
  alanları FU02'den sonra da **format-level optional referans** olarak kalır. Campaign'in bu master'ı gerçekten
  resolve etmesi ayrı bir consumer FU'sudur (F6).
- **`Diten.MdmService` bugün canlıdır** (port 5059, `DitenERP_Dev`, JWT hizalı, launchSettings ve fleet içinde).
  LegalEntity ilk slice'ında Guid `GuidRepresentationMode` V3 düzeltmesi gerekmişti; Brand/Product da aynı
  serialization yolundan geçer (§4.5).
- **Gateway ilk kez `/api/mdm/*` prefix'ini alır.** Mevcut `/api/legal-entities` bilinçli olarak taşınmaz —
  taşımak protected bir route'u kırar ve bu pack'in kapsamı dışıdır. İki prefix'in bir arada yaşaması
  kabul edilir; hizalama ayrı bir integration follow-up'ıdır (F8).
- **Golden Compact sayımı:** Brand 10, Product 16. FU01 §17 "Brand ≈ 8 alan, slim adayı" demişti; effective-dating
  alanları (D4) sayımı 10'a çıkardığı için **her iki modül de compact**tır ve tek bir UI standardı korunur.
- **Vocabulary:** in-domain sabitler contract endpointinden yayımlanır (D2). MOD-0048 setleri yayımlandığında
  UI'ın hiç değişmemesi hedeflenir — bu yüzden UI **hardcoded liste tutmaz**, yalnız contract'tan okur.
- **Permission seed edilmemiştir.** İlk smoke'ta `mdm.brands.*` / `mdm.products.*` claim'i bulunmayabilir;
  bu durum PARTIAL olarak raporlanır, seed/grant veya hardcoded allow yapılmaz (F3).
- **Çalışma ağacı** çok sayıda mevcut kullanıcı değişikliği içeriyor. Orchestrator yalnız §5 yollarında
  çalışmalı, mevcut değişiklikleri korumalı ve layout değişikliğini minimal tutmalıdır.
- **Status akışı:** implementation başladığında `in-progress`, testlerden sonra `review`, kabulden sonra `done`.
  Kod ilk kez kazanılacağı için `execution/registries/module-implementation-status.md` güncellemesi
  **implementation closeout** kapsamındadır; bu hazırlık task'ı registry write yapmaz.

## 25. Follow-up Items

| # | Follow-up | Owner | Neden |
|---|---|---|---|
| F1 | **Registry satırı `MOD-0290` (+ FU01/FU02)** `module-id-registry.md`'ye eklensin | registry / governance owner | FU01 F1 hâlâ açık; Blueprint'te var, registry'de yok |
| F2 | **EA notu — `Brand` nesnesinin MOD-0290 SoR cümlesine eklenmesi** | EA | FU01 F2 devam ediyor |
| F3 | **`MOD-0290-FU02-RBAC` — `mdm.brands.*` / `mdm.products.*` katalog + grant** | MOD-0018 / MDM | §14 anahtarları seed edilmemiştir |
| F4 | **MOD-0048 reconciliation** — `brand-status` · `product-status` · `product-dosage-form` · `product-uom` yayımlanınca in-domain vokabülerin devri | MOD-0048 operator + MDM | D2 sapmasının kapanışı |
| F5 | **`ProductStatus: discontinued`** — FU01 §11 lifecycle amendment'i | EA / MDM | D3; pharma ihtiyacı gerçek, FU01 seti kilitli |
| F6 | **Consumer resolve FU'ları** — Campaign/Knowledge/Frequency'nin `BrandId`/`ProductId`'yi gerçekten resolve etmesi ve display adı göstermesi | commercial-suite | FU02 yalnız master + contract verir |
| F7 | **MOD-0285 navigation migration** — Brand/Product page descriptor data-driven olunca hardcoded `<li>`'lerin kaldırılması + no-double-menu smoke | Platform / MDM | §6.1 |
| F8 | **MDM route prefix hizalaması** — `/api/legal-entities`'in `/api/mdm/legal-entities`'e taşınması (breaking; ayrı authorization) | integration-agent | §15 / D5 |
| F9 | **Item / SKU / UoM mapping / product identifier FU'ları** | MDM | MOD-0290'ın kalan Blueprint SoR kapsamı |
| F10 | **Multi-brand product (FU01 F4) ve ürün ailesi/portföy hiyerarşisi (FU01 F5)** | MDM / EA | v1'de kapalı |
| F11 | **TherapeuticArea / Indication / ATC SoR kararı** (FU01 F6) | EA / MDM + commercial-suite | Bugün metadata/ExternalRef |
| F12 | **Legacy `Property/PropertyList` migration mapping planı** (FU01 F9) | commercial-suite + MDM | ExternalReferences politikası yazıldı, taşıma ayrı |
| F13 | **Brand/Product import/export** | MDM | FU02 import/export engine açmaz |

### Orchestrator handoff

```text
@orchestrator execution/domains/master-data-management/module-packs/MOD-0290-FU02-brand-product-runtime-ui.md

MOD-0290-FU02 — Brand/Product Runtime + UI Implementation
```
