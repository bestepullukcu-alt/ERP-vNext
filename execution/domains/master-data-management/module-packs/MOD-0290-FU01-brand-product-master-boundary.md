---
id: MOD-0290-FU01
name: Brand / Product Master Boundary
parent: MOD-0290
parent_name: Product / Item / SKU Master
domain: master-data-management
service: TBD (implementation FU kararı — MDM tarafı; `Diten.MdmService` adayı)
shell: none
golden_reference: none
entity_base: EntityBase
status: draft
runtime_code_allowed: false
runtime_code_scope: "NONE — bu pack yalnız Brand/Product master sahipliği ve boundary'sidir. Aggregate, CRUD, endpoint, import/export, UI ve migration ayrı bir implementation FU authorization'ı gerektirir."
owner: module-pack-author
branch: feature/mdm/mod-0290-fu01-brand-product-master-boundary
started: 2026-08-02
target: TBD (implementation FU ayrı yetkilendirilir)
form_field_count: 0
dependencies:
  - MOD-0290 (parent — Blueprint SoR: product master, item master, SKU, UoM, identifiers, lifecycle)
  - MOD-0162-FU01 / FU01A / FU01B / FU01C (consumer — Knowledge/Content, Path, Journey, Concept Graph)
  - MOD-0165-FU01 / MOD-0167-FU01 (consumer — campaign / segmentation / frequency policy)
  - MOD-0155 (consumer — visit objective / detailing bağlamı)
  - MOD-0159 (consumer — Product Configuration; SoR burada değil)
  - MOD-0048 (reference data — dosage-form / status / uom vokabülerleri)
  - MOD-0288 / MOD-0151 (BusinessUnit scope vokabüleri — referans, yeniden üretilmez)
  - MOD-0018 (RBAC — yalnız tüketim; seed/grant bu pack'te yok)
---

# MOD-0290-FU01 — Brand / Product Master Boundary

> **BOUNDARY AUTHORIZATION (2026-08-02) — `runtime_code_allowed: false`.**
> Bu pack **kod yazma yetkisi vermez**. Yetkilendirdiği tek şey, **Brand/Product master'ının sahipliği, alan
> sözleşmesi, yaşam döngüsü ve tüketici sınırlarıdır**. CRUD, endpoint, import/export, UI, migration,
> campaign/frequency/visit runtime ve Knowledge/Content implementasyonu **açılmamıştır**.
>
> **Neden şimdi:** MOD-0151 (F6/F16), MOD-0162-FU01/FU01A/FU01B/FU01C ve MOD-0165-FU01 zincirlerinin **hepsi**
> Brand/Product'ı *optional/future* bırakıp aynı follow-up'ı açtı. Boundary yazılmazsa Brand/Product en kolay ama
> en yanlış üç yere sızar: `KnowledgeContent` içine zorunlu alan, `Campaign` içine local kopya, ya da concept
> graph node'unun master gibi davranması.
>
> **DCP-002 kimlik kapısı — PASS (2026-08-02):**
> `py .antigravity/scripts/verify_module_id.py . --check-id MOD-0290 --name "Product / Item / SKU Master"` → `OK` (exit 0)
> `py .antigravity/scripts/verify_module_id.py . --check-id MOD-0290-FU01 --name "Brand / Product Master Boundary" --parent MOD-0290` → `OK` (exit 0)
> **Registry satırı `MOD-0290` için henüz yoktur** — registry yazımı pack yetkisi dışıdır → F1 follow-up.
>
> Otorite sırası: **Blueprint Excel** > Module Pack > [Domain Config](../domain-config.md) > `AGENTS.md` >
> `.antigravity/rules/`.

---

## 1. Ownership Decision

**Blueprint kanıtı (`Blueprint_Data`):**

| Alan | Değer |
|---|---|
| Module ID | **MOD-0290** |
| Module Name | **Product / Item / SKU Master** |
| Suite / Platform | Master Data / Product Foundation |
| Capability Group | Product, Item & SKU Master Data |
| Placement | Domain App (Master Data) |
| **SoR** | *Product master records, item master records, SKUs, UoM mappings, product identifiers, item lifecycle states* |
| Integration contract | `PRODUCT-MASTER-BUNDLE` |
| Wave | W-3 |

### Değerlendirilen dört seçenek

| # | Seçenek | Karar | Gerekçe |
|---|---|---|---|
| 1 | **MDM / Product Master altında canonical SoR (MOD-0290)** | ✅ **SEÇİLDİ** | Blueprint'te açık canonical capability var; [crm-sor-boundary.md](../../commercial-suite/crm-sor-boundary.md) satır 31 zaten "Brand / Product / SKU → MDM / Product · **read-only consume**" diyor; commercial-suite domain-config "Brand/Product/SKU master → MDM" diyor |
| 2 | Commercial Suite altında CRM-adjacent Brand/Product boundary | ❌ | CRM'de ikinci bir master doğurur; SoR matrisiyle çelişir |
| 3 | MOD-0165 Campaign içinde campaign-local brand/product referansı | ❌ **duplicate master** | Campaign yalnız **referans** verir; local kopya kampanya bittiğinde ürün gerçeğini bozar |
| 4 | KnowledgeContent içinde local brand/product alanı | ❌ | MOD-0162-FU01 §7'de zaten reddedildi: Brand/Product **opsiyonel metadata**dır, içerik modelinin merkezi değil |

**Karar:** Brand/Product **master data**dır ve SoR'u **MOD-0290**'dır. Campaign, KnowledgeContent, Concept Graph,
Frequency Policy ve Visit Planning **yalnız referans verir**; hiçbiri local/duplicate master açmaz.

### Bu FU'nun parent içindeki yeri (kapsam sınırı)

MOD-0290'ın Blueprint SoR listesi **product master · item master · SKU · UoM · identifier · lifecycle state**'i
kapsar. **Bu FU yalnız `Brand` ve `Product` katmanının boundary'sini** yetkilendirir; **Item / SKU / UoM mapping /
identifier yönetimi bu pack'te açılmaz** — onlar MOD-0290'ın ayrı FU'larıdır.

> **EA notu (F2):** `Brand`, MOD-0290'ın Blueprint SoR cümlesinde **adıyla geçmez**. Brand pharma/commercial bir
> **ürün sınıflandırma/üst nesnesidir** ve en savunulabilir yeri aynı ürün master'ıdır (CRM'de ikinci master
> açmamak için). Bu, parent SoR ifadesinin küçük bir genişletmesidir ve EA teyidi ister; boundary'nin geri kalanı
> bu karardan bağımsızdır.

---

## 2. Brand/Product Is Not the Content Center

| Yanlış model | Karar |
|---|---|
| `KnowledgeContent = Brand/Product content` | ❌ Reddedildi (MOD-0162-FU01) |
| Brand/Product olmadan content oluşturulamaz | ❌ Reddedildi — Almanca/QMS/onboarding içeriği Brand/Product'sız üretilir |
| Campaign yalnız Brand/Product üzerinden çalışır | ❌ Reddedildi — campaign hedefi segment/indication/audience üzerinden de kurulabilir |
| Visit Planning yalnız Brand/Product üzerinden çalışır | ❌ Reddedildi — visit hedefi coverage + frequency + journey ile kurulur |
| `Indication/Profile/Need/Benefit` Brand/Product içine gömülür | ❌ Reddedildi (MOD-0162-FU01C concept chain) |

**Doğru model:**

```text
Brand/Product Master   = commercial/pharma master data          (MOD-0290 — SoR)
KnowledgeContent       = genel içerik                            (MOD-0162-FU01)
Subject Concept Graph  = subject bazlı kavram zinciri            (MOD-0162-FU01C)
Brand/Product          = optional metadata veya ExternalRef      (tüketici taraf)
```

---

## 3. Authorized `Brand` Model

| Alan | Zorunluluk | Not |
|---|---|---|
| `BrandId` | Zorunlu | Aggregate kimliği |
| `TenantId` | Zorunlu | **JWT claim'inden**; payload'da **asla** |
| `BrandCode` | Zorunlu | Tenant içinde unique, **stabil** |
| `BrandName` · `Description` | Zorunlu / optional | |
| `BusinessUnit` | Optional | Mevcut platform/commercial BU vokabüleri **referanslanır**, yeniden üretilmez (§10) |
| `TherapeuticArea` | Optional | §5 — master SoR netleşene kadar referans/metadata |
| `Status` | Zorunlu | §11 |
| `EffectiveFrom` · `EffectiveTo` | Zorunlu / optional | `EffectiveTo < EffectiveFrom` → **400** |
| `ExternalReferences[]` | Optional | §12 |
| `CreatedAt` / `CreatedBy` / `UpdatedAt` / `UpdatedBy` | Zorunlu | |

**Kurallar:** `BrandCode` **stabil**; rename `BrandName` ile yapılır, kod bozulmaz · **hard delete yok** ·
**archived brand yeni campaign/content/frequency linking'inde kullanılamaz** (mevcut linkler history olarak
korunur) · **Brand bir `KnowledgeContent` değildir** · **Brand bir `Campaign` değildir** · brand içine içerik,
mesaj, sıklık kuralı veya ziyaret hedefi **gömülmez**.

---

## 4. Authorized `Product` Model

| Alan | Zorunluluk | Not |
|---|---|---|
| `ProductId` | Zorunlu | |
| `TenantId` | Zorunlu | JWT claim'inden |
| `ProductCode` | Zorunlu | Tenant içinde unique, **stabil** |
| `ProductName` · `Description` | Zorunlu / optional | |
| `BrandId` | **Optional** (§4.1 kararı) | Brand'siz ürün mümkündür |
| `DosageForm` · `Strength` · `PackSize` | Optional | Vokabüler/ölçü birimleri MOD-0048 (§10) |
| `ATCCode` | Optional | **External taxonomy referansı** (§5) — burada master açılmaz |
| `IndicationRefs[]` | Optional | Referans listesi (concept node veya external taxonomy) — **indication master burada yok** |
| `TherapeuticArea` | Optional | Brand'den devralınabilir; product değeri **daha spesifiktir** (§4.1) |
| `Status` | Zorunlu | §11 |
| `EffectiveFrom` · `EffectiveTo` | Zorunlu / optional | `EffectiveTo < EffectiveFrom` → **400** |
| `ExternalReferences[]` | Optional | §12 |
| `CreatedAt` / `CreatedBy` / `UpdatedAt` / `UpdatedBy` | Zorunlu | |

**Kurallar:** `ProductCode` **stabil**; rename kodu değiştirmez · **hard delete yok** · **archived product yeni
linking'de kullanılamaz**; mevcut visit/content/campaign history **korunur** · ATC ve Indication master'larının
SoR'u net olmadığı için Product içinde **yalnız referans/metadata** olarak durur (§5) · **Product,
`KnowledgeContent` veya `Campaign` içine kopyalanmaz** — oralarda yalnız `ProductId` referansı bulunur.

### 4.1 Brand ↔ Product hiyerarşi kararları

| Soru | Karar | Gerekçe |
|---|---|---|
| Product mutlaka Brand altında mı? | **Hayır** — `BrandId` **optional** | MOD-0290 item/SKU master'ı da kapsar; markasız/jenerik ürünler ve non-pharma kalemler vardır. Zorunluluk **şemada değil**, ürün tipi bazlı iş kuralıyla uygulanır (implementation FU) |
| Brand olmadan product olabilir mi? | **Evet** | Yukarıdaki gerekçe |
| Aynı product birden fazla brand altında olabilir mi? | **v1: Hayır — future** (F4) | Multi-brand ilişki ayrı bir association nesnesi + provenance ister; CRM boundary'si için gerekli değil |
| `BusinessUnit` hangi seviyede? | **Her ikisinde optional**; ikisi de doluysa **product değeri kazanır** | "En spesifik kazanır" kuralı MOD-0165-FU01 §9 ve MOD-0162 zinciriyle tutarlı |
| `TherapeuticArea` hangi seviyede? | **Her ikisinde optional**; brand değeri **varsayılan**, product değeri **override** | Aynı marka altında farklı TA'lı ürünler olabilir |

Hiyerarşi **tek seviyelidir** (`Brand → Product`); ürün ailesi/portföy ağacı **bu FU'da yoktur** (F5).

---

## 5. Indication / ATC / TherapeuticArea Boundary

MOD-0162-FU01C kararı **aynen korunur**: `Indication → AudienceProfile → ProfileNeed → NeedBenefit`
**hardcoded değildir**, subject bazlı `ConceptChainTemplate` olarak yaşar.

Bu FU **yapmaz:** Indication/Profile/Need/Benefit'i Brand/Product içine hardcoded gömmek · Subject Concept
Graph'i bypass etmek · `KnowledgeContent`'e sabit pharma alanları dayatmak.

| Kavram | Karar |
|---|---|
| `ATCCode` | **External taxonomy** (WHO ATC) — bu FU ATC master'ı **açmaz**; Product'ta kodlanmış referans, istenirse `ConceptNode` + `ExternalRef` |
| `Indication` | Product metadata (`IndicationRefs[]`) **veya** ConceptNode; **indication master bu FU'da yok** |
| `TherapeuticArea` | Brand/Product metadata **veya** ConceptNode; SoR kararı **açık** (F6) |
| Master SoR belirsizse | **ExternalRef/metadata** olarak tutulur — belirsiz bir master **uydurulmaz** |
| Gelecek | Subject Concept Graph üzerinden Brand/Product ↔ concept node linkleri kurulabilir (§7) |

---

## 6. Knowledge / Content Integration Boundary

```text
KnowledgeContent : Almiba Q1 Doctor Deck
Optional metadata: BrandId=Almiba · ProductId=Almiba 10mg · Indication=Hipertansiyon
                   ATCCode=C09AA · AudienceProfile=Kardiyoloji A segment doktor
```

| Kural | Karar |
|---|---|
| Brand/Product yokken içerik | **Oluşturulabilir** — Almanca, QMS, onboarding içeriği Brand/Product'sız yaşar |
| Merkezîlik | Brand/Product içerik modelinin **merkezi değildir** |
| Versiyonlama | MOD-0162-FU01 kararlarıyla aynı kalır (`pinned`/published+effective) |
| `FileRef` | **MOD-0028/0029** boundary'si korunur — bu FU dosya/render açmaz |
| Path / Journey | `KnowledgePath` ve `EngagementJourney` Brand/Product'a **zorunlu bağımlı değildir** |
| Kopyalama | İçerik tarafında yalnız `BrandId`/`ProductId` **referansı** tutulur; ad/kod/metadata kopyalanırsa **görüntüleme amaçlı türev** sayılır ve master değişince **stale kabul edilir** |

---

## 7. Subject Concept Graph Integration Boundary

Üç bağlanma biçimi:

```text
1) ConceptNode olarak : ConceptType=brand · ConceptNode=Almiba
                        ExternalRefType=brand · ExternalRefId=BrandId
2) Metadata olarak    : KnowledgeContent.BrandId · KnowledgeContent.ProductId
3) Consumer input     : Campaign.BrandId · FrequencyPolicy.BrandId · VisitObjective.BrandId
```

| Kural | Karar |
|---|---|
| ConceptNode | **Master SoR değildir**; master'a `ExternalRef` ile bağlanır (MOD-0162-FU01C §4 ile aynı) |
| SoR | **Brand/Product master SoR olarak kalır** |
| Duplicate | Concept node'lar master'ın **kopyası olamaz** (ad/kod dışında alan taşımaz) |
| `ExternalRef` eksikse | Node yalnız **taxonomy/context** olarak değerlendirilir; **master iddiası taşımaz** ve raporlarda master ile eşleştirilmiş sayılmaz |

---

## 8. MOD-0165 / MOD-0167 Integration Boundary

```text
Campaign=Almiba Q1 · Brand=Almiba · Product=Almiba 10mg
Target=Kardiyoloji A segment doktor · Frequency=ayda 2 · Journey=Almiba Q1 Doctor Engagement
```

Bu FU **yapmaz:** campaign engine · segmentation engine · frequency runtime · target assignment · due/overdue.
Brand/Product yalnız **referans boundary'si** sağlar; `VisitFrequencyPolicy.BrandId`/`ProductId` alanları
MOD-0165-FU01 §4'te zaten **future optional** olarak duruyor ve bu pack onları **master'a bağlanabilir** hâle
getirir — davranış değişmez.

---

## 9. MOD-0155 Consumer Boundary

Tüketebilir: visit objective brand/product · recommended content brand/product · campaign brand/product ·
doctor/profile need-benefit bağlamı · journey stage brand/product bağlamı.

**Bu FU:** visit plan · route plan · daily schedule · visit execution · content recommendation · digital
detailing · usage tracking **yapmaz**.

---

## 10. MDM / Reference Data Boundary

| Soru | Karar |
|---|---|
| Brand/Product reference set mi, master data mı? | **Master data** (MOD-0290 SoR) — MOD-0048 reference set **değildir** |
| `DosageForm`, `Status`, ölçü birimleri | **MOD-0048 reference set** (`product-dosage-form` · `product-status` · `brand-status` · `product-uom`); hardcoded enum yasak, set yayınlanmadan **fail-closed 400** |
| `Strength` / `PackSize` | Değer + **birim**; birim vokabüleri MOD-0048/UoM tarafında (MOD-0290'ın UoM FU'su) |
| `ATCCode` | **External taxonomy / reference** — bu FU master açmaz |
| `TherapeuticArea` | **Subject Concept Graph node'u veya controlled reference**; SoR kararı F6 |
| `BusinessUnit` | **Mevcut platform/commercial scope** (MOD-0288 / MOD-0151 BU vokabüleri) **referanslanır**, yeniden üretilmez |
| `IndicationRefs[]` | Referans listesi; indication master bu FU'da **yok** |

---

## 11. Status / Lifecycle Policy

```text
draft · active · inactive · archived
```

| Kural | Karar |
|---|---|
| Linking uygunluğu | Yalnız **active + effective** Brand/Product yeni campaign/content/frequency linking'inde kullanılabilir |
| `inactive` | Yeni kullanımda önerilmez; **history okunur** |
| `archived` | Yeni linking'e **kapalı**; okunabilir kalır |
| Hard delete | **Yok** |
| Effective window | Dışında **yeni linking yapılmaz**; mevcut linkler bozulmaz |
| History | Mevcut content/campaign/visit history **bozulmaz** |
| Rename | **Kod değişmez**; ad/alias ile yapılır |
| Brand ↔ Product tutarlılığı | Brand `archived` iken altındaki product'lar **otomatik archive edilmez**; ancak yeni product o brand'e **bağlanamaz** ve durum raporda **görünür** olur (sessiz cascade yasak) |

---

## 12. External Reference / Legacy Migration Boundary

Legacy CRM'den gelen brand/product/ATC/indication kodları **korunur**
([crm-sor-boundary.md](../../commercial-suite/crm-sor-boundary.md) satır 31: legacy `Property/PropertyList` ürün
kopyası).

`ExternalReferences[]` minimum alanları: `SourceSystem` · `ExternalId` · `ExternalCode` · `ExternalName` ·
`ImportedAt` · `IsPrimary`.

| Kural | Karar |
|---|---|
| Canonical kod | Legacy kod **canonical olmak zorunda değildir**; canonical `BrandCode`/`ProductCode` **stabildir** |
| Mapping | Legacy mapping **korunur** (silinmez, üzerine yazılmaz) |
| `IsPrimary` | `(SourceSystem)` başına **en fazla bir** primary → ikincisi **409** |
| Duplicate | Aynı `(SourceSystem, ExternalId)` iki farklı canonical kayda işaret ediyorsa **deterministik conflict raporu** üretilir |
| **Silent merge** | **Yasak** — otomatik birleştirme yok; çakışma görünür kalır ve insan kararı ister |
| Hard delete | **Yok** |
| Yön | ExternalRef bir **iz kaydıdır**, ikinci bir master değildir |

---

## 13. Explicit Exclusions

Runtime implementation · backend/frontend/Gateway değişikliği · **Brand/Product CRUD** · import/export ·
campaign engine · segmentation engine · frequency runtime · visit planning · route planning · digital detailing ·
content recommendation engine · Subject Concept Graph runtime engine · AI personalization · KnowledgeContent
implementation · file upload/render/preview · approval workflow · MOD-0023 entegrasyonu · patient data ·
Account/Contact mutation · territory mutation · **Item/SKU/UoM/identifier yönetimi** (MOD-0290'ın diğer FU'ları) ·
hard delete · Mongo hand-edit · RBAC seed/grant · registry write · MOD-0048 publish · `TenantId` payload'da.

---

## 14. Permission Boundary

Canonical öneriler: `mdm.product.read` · `mdm.product.manage` · `mdm.brand.read` · `mdm.brand.manage`.
Tüketici modüller (CRM/Knowledge/Campaign) yalnız **`*.read`** anahtarına ihtiyaç duyar.
**Bu pack hiçbir permission literal'ini kataloğa eklemez, seed/grant yapmaz** → `-RBAC` follow-up (F7).

---

## 15. Contract Flags (öneri — implementation anlamına gelmez)

```json
{
  "supportsBrandMaster": true,
  "supportsProductMaster": true,
  "supportsBrandProductHierarchy": true,
  "supportsBrandProductExternalReferences": true,
  "supportsBrandProductKnowledgeMetadata": true
}
```

**Eklenmez:** `supportsVisitPlanning` · `supportsRoutePlanning` · `supportsDigitalDetailing` ·
`supportsCampaignEngine` · `supportsRecommendationEngine` · `supportsWorkflowApproval`.
MOD-0162 / MOD-0165 / MOD-0151 flag setleri **değişmez**.

---

## 16. Acceptance Criteria for Pack Approval

- [x] Canonical SoR **MOD-0290** olarak Blueprint kanıtıyla belirlendi; dört seçenek açıkça değerlendirildi.
- [x] Brand/Product **master data**dır; Campaign/KnowledgeContent içinde local/duplicate master **yasaklandı**.
- [x] Brand ve Product alan sözleşmeleri (§3, §4) yazıldı; hiyerarşi kararları (§4.1) verildi.
- [x] Indication/ATC/TherapeuticArea **hardcoded gömülmedi**; concept graph uyumu korundu.
- [x] KnowledgeContent Brand/Product **olmadan da** üretilebilir olarak kaldı; non-pharma subject'ler etkilenmedi.
- [x] Concept Graph entegrasyonu `ExternalRef` kuralıyla yazıldı — node **master iddiası taşımaz**.
- [x] MOD-0165/0167 ve MOD-0155 consumer sınırları yazıldı.
- [x] MDM/reference-data ayrımı (master vs MOD-0048 set vs external taxonomy vs BU scope) netleşti.
- [x] Status/lifecycle ve legacy `ExternalReferences[]` politikası (silent merge yasağı dahil) yazıldı.
- [x] Runtime/CRUD/planning/campaign/detailing scope'u açılmadı; `runtime_code_allowed: false`.
- [ ] Reviewer onayı → `status: approved`; ardından implementation FU ayrı yetkilendirilir.
- [ ] **F1 registry** (`MOD-0290` satırı) ve **F2 EA notu** (Brand'in parent SoR'a eklenmesi) kapanmalı.

---

## 17. Implementation Notes (implementation FU'suna devir)

- Servis kararı **implementation FU zamanında** verilir: `Diten.MdmService` mevcut MDM servisidir ve en güçlü
  adaydır; MOD-0220 precedent'i (tenant-scoped, `TenantId` server-side) izlenmelidir.
- Golden Reference: Brand formu ≈ 8 alan (**slim** adayı), Product formu > 8 alan (**compact** adayı) — kesin
  sayım implementation FU'sunda yapılır.
- Yeni aggregate'ler class-map/GUID kaydı gerektirir (CRM'de yaşanan "Guid FK binary yazıldı, filtre sessizce boş
  döndü" hatası MDM'de de mümkündür).
- İki `DateTimeOffset` alanı (`EffectiveFrom`/`EffectiveTo`) **birlikte index'lenmez/sort edilmez**.
- Tüketiciler için **read-only lookup contract** (kod + ad + status + effective) ilk teslimin parçası olmalıdır;
  CRM/Knowledge tarafının ihtiyacı budur, tam master ekranları sonra gelebilir.

---

## 18. Follow-up Items

| # | Follow-up | Owner | Neden |
|---|---|---|---|
| F1 | **Registry satırı `MOD-0290` (+ `MOD-0290-FU01`)** `module-id-registry.md`'ye eklensin | registry / governance owner | Blueprint'te var, registry'de yok; pack yetkisi dışı |
| F2 | **EA notu — `Brand` nesnesinin MOD-0290 SoR cümlesine eklenmesi** | EA | Blueprint SoR listesi product/item/SKU/UoM/identifier diyor; Brand adıyla geçmiyor (§1) |
| F3 | **`MOD-0290-FU02 — Brand/Product Master Implementation`** (aggregate + CRUD + lookup contract + UI + tests) | MDM | Bu pack'in runtime devamı |
| F4 | **Multi-brand product** (aynı ürünün birden fazla marka altında olması) | MDM / EA | v1'de kapalı (§4.1) |
| F5 | **Ürün ailesi / portföy hiyerarşisi** (`ProductFamily`, `Portfolio`) | MDM / commercial | v1 tek seviye `Brand → Product` |
| F6 | **TherapeuticArea / Indication / ATC SoR kararı** (controlled reference mi, concept node mu, external taxonomy mi) | EA / MDM + commercial-suite | Bugün metadata/ExternalRef (§5) |
| F7 | **`MOD-0290-FU01-RBAC — Brand/Product Permission Catalog Alignment`** | MOD-0018 / MDM | §14 anahtarları katalog + grant gerektirir |
| F8 | **MOD-0048 product reference set publish** (`product-dosage-form` · `product-status` · `brand-status` · `product-uom`) | MOD-0048 operator | Hardcoded enum yasağı |
| F9 | **Legacy `Property/PropertyList` migration mapping planı** | commercial-suite + MDM | ExternalReferences politikası yazıldı, taşıma planı ayrı (§12) |
| F10 | **MOD-0159 Product Configuration sınırı** — konfigürasyon MOD-0159'da, master burada | commercial-suite / MDM | Çift sahiplik riskini önlemek için |

---

## 19. Next Recommended Prompt

1. **`Campaign / Targeting Boundary Pack Authorization`** (MOD-0165 / MOD-0167 zincirinin devamı)
2. **`MOD-0290-FU02 — Brand/Product Master Implementation`** — yalnız §3/§4/§11/§12 sözleşmesi; campaign,
   frequency, visit ve content runtime'ı **açılmaz**.

## Product-five entitlement end-to-end fix amendment — approved 2026-09-29

This amendment records only `PRODUCT-FIVE-ENTITLEMENT-END-TO-END-FIX-02`. It preserves this pack's frontmatter,
`status: draft`, `runtime_code_allowed: false` and all historical evidence. It authorizes no Brand/Product runtime,
grant execution or live reconciliation; the runtime authority for the shared checkpoint remains in MOD-0018-FU18/FU19.

### Bounded contract

- `mdm.brands.read` remains owned by `brand-product-master`. The `product-item-sku-master` manifest is only a
  consumer and carries that owner through explicit `PermissionOwnerModuleCode`; permission-key prefix inference is
  forbidden. Registration order and replay must converge on the same owner and cannot re-attribute the permission to
  the consumer module.
- `mdm.product-identity.lifecycle-operations.recover` remains catalogued, but no new human-role grant may be created
  through default-role selection (including Admin, Viewer and SuperAdmin), tenant-assignability, catalog
  create/reactivate full-catalog auto-grant, or manual assignment in tenant or platform-admin context. Existing
  historical grants are not scanned or automatically deleted; any remediation/revocation is a separate operation.
- The policy is exact-key bounded. Existing Finished Good behavior, adjacent permissions, the previously approved
  six-row GSKU/LSKU add delta and provenance-bounded stale LSKU direct-retire removal are not widened or redefined.
  No live catalog/role mutation, token refresh, service restart or Production enablement is authorized.

### Exact 24-path checkpoint allow-list

1. `execution/domains/platform-shared-services/module-packs/MOD-0018-FU18-gsku-permission-onboarding.md`
2. `execution/domains/platform-shared-services/module-packs/MOD-0018-FU19-lsku-permission-onboarding.md`
3. `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/EntitlementPermissionSyncService.cs`
4. `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/ProductIdentityLifecycleEntitlementGrantProfile.cs`
5. `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/EntitlementPermissionSyncServiceTests.cs`
6. `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/EntitlementSyncConsumerTests.cs`
7. `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/ProductIdentityLifecycleEntitlementGrantProfileTests.cs`
8. `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/ProductIdentityLifecyclePermissionOnboardingMongoTests.cs`
9. `services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/ProductItemSkuMasterManifestProvider.cs`
10. `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ModuleRegistration/ProductItemSkuMasterManifestProviderTests.cs`
11. `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/DefaultRolePermissionTemplate.cs`
12. `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IFullCatalogPermissionGrantService.cs`
13. `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/FullCatalogPermissionGrantService.cs`
14. `services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/InternalPermissionsController.cs`
15. `services/Diten.AuthService/src/Diten.AuthService.Application/Features/Roles/Handlers/CommandHandlers/AssignPermissionCommandHandler.cs`
16. `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/DefaultRolePermissionTemplateTests.cs`
17. `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Permissions/FullCatalogPermissionGrantServiceTests.cs`
18. `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Permissions/InternalPermissionsControllerTests.cs`
19. `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/AssignPermissionCommandHandlerTests.cs`
20. `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.ModuleRegistration.Abstractions/ModuleManifestDocument.cs`
21. `services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleRegistration/RegisterModuleManifestCommandHandler.cs`
22. `services/Diten.Platform/tests/Diten.Platform.Application.Tests/ModuleRegistration/RegisterModuleManifestCommandHandlerTests.cs`
23. `services/Diten.Platform/tests/Diten.Platform.Application.Tests/ModuleCatalog/CatalogPermissionSyncServiceTests.cs`
24. `execution/domains/master-data-management/module-packs/MOD-0290-FU01-brand-product-master-boundary.md`

Every other tracked path is out of scope; `.local/**` and `.testoutput/**` remain non-checkpoint artifacts.

### Test gates before checkpoint

- MDM manifest tests prove exact unique canonical descriptors, recovery as non-interactive System-only metadata,
  unchanged Finished Good contract, and explicit `brand-product-master` ownership for `mdm.brands.read`.
- Platform registration/catalog tests prove page/action owner propagation, no prefix inference, descriptor-order
  independence, replay convergence and the generic Auth payload retaining `brand-product-master`.
- Auth tests prove recovery stays catalogued while every default/SuperAdmin/full-catalog/manual human grant-creation
  path rejects it in tenant and platform-admin contexts; adjacent permission behavior and historical grants remain
  unchanged.
- Profile/sync/Mongo/consumer tests retain the six-row delta, zero recovery grants, provenance-bounded LSKU revoke,
  replay idempotency, tenant/source isolation, Manual/System/other-source preservation and failure/retry semantics.
- Focused affected suites plus Auth, MDM, Platform and Building Blocks Release builds must pass. Test evidence does
  not claim live reconciliation, general authorization expansion, Finished Good acceptance or Production enablement.
