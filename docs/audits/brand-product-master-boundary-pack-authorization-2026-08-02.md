# Brand / Product Master Boundary — Pack Authorization

> Tarih: 2026-08-02
> Kimlik: **MOD-0290-FU01 — Brand / Product Master Boundary** (parent `MOD-0290 Product / Item / SKU Master`)
> Domain: **master-data-management** (CRM değil)
> Kapsam: Sahiplik / alan sözleşmesi / lifecycle / tüketici sınırları — **kod yazma yok, runtime yok**
> Sonuç: **PASS**

---

## 1. Preflight

| Kontrol | Sonuç |
|---|---|
| Task türü | Documentation / boundary authorization (implementation değil) |
| Değiştirilen runtime kod | **0 dosya** |
| Çalışma alanı | `execution/domains/master-data-management/**`, `execution/domains/commercial-suite/module-packs/**`, `docs/audits/**` |
| DCP-002 `MOD-0290` | `OK  MOD-0290: proven against Blueprint/registry.` (exit 0) |
| DCP-002 `MOD-0290-FU01` | `OK  MOD-0290-FU01: proven against Blueprint/registry.` (exit 0, `--parent MOD-0290`) |
| Registry satırı | **`MOD-0290` registry'de YOK** (Blueprint'te var) → yazımı pack yetkisi dışı → F1 |
| Blueprint taraması | `Blueprint_Data` Module Name üzerinde `product\|brand\|item\|material\|sku\|catalog\|portfolio` |

---

## 2. Dependency Confirmation

| Ön koşul | Durum |
|---|---|
| MOD-0150 Contact Availability | **PASS** |
| MOD-0151 FU09A Visit/Route Readiness | **PASS** |
| MOD-0165 / MOD-0167 Visit Frequency Ownership | **PASS** |
| MOD-0162-FU01 Knowledge / Content & Subject Taxonomy | **PASS** |
| MOD-0162-FU01A KnowledgePath | **PASS** |
| MOD-0162-FU01B EngagementJourney | **PASS** |
| MOD-0162-FU01C Subject Concept Graph | **PASS** |
| MOD-0155 | **Başlamadı** |
| Workflow / approval | **En sona bırakıldı** |

Bu beş pack'in **hepsi** Brand/Product'ı optional/future bırakıp aynı follow-up'ı açmıştı
(MOD-0151 F6/F16 · MOD-0162-FU01 F2 · FU01A F2 · FU01B F2 · FU01C F2 · MOD-0165-FU01 F1). Bu task o
follow-up'ı kapatır.

---

## 3. Business Need Summary

Brand/Product boundary'si yazılmazsa master en kolay ama en yanlış üç yere sızar: `KnowledgeContent` içine
**zorunlu alan**, `Campaign` içine **local kopya**, ya da concept graph node'unun **master gibi davranması**.
Aynı anda karşıt risk de gerçektir: Brand/Product'ı içeriğin merkezine koymak, Almanca/QMS/onboarding
içeriklerini modelden dışlar.

Karar bu iki riski birlikte çözer: **Brand/Product bir master data'dır (MDM), içeriğin merkezi değildir; tüketiciler
yalnız referans verir.**

---

## 4. Ownership Decision

**Blueprint kanıtı:** `MOD-0290 · Product / Item / SKU Master · Master Data / Product Foundation ·
Capability Group: Product, Item & SKU Master Data · Placement: Domain App (Master Data) · W-3 ·
SoR: product master records, item master records, SKUs, UoM mappings, product identifiers, item lifecycle states ·
PRODUCT-MASTER-BUNDLE`.

| # | Seçenek | Karar | Gerekçe |
|---|---|---|---|
| 1 | **MDM / Product Master canonical SoR = MOD-0290** | ✅ **SEÇİLDİ** | Blueprint'te açık canonical capability; `crm-sor-boundary.md` sat. 31 "Brand / Product / SKU → MDM / Product · read-only consume"; commercial-suite domain-config "Brand/Product/SKU master → MDM" |
| 2 | Commercial Suite altında CRM-adjacent master | ❌ | CRM'de ikinci master; SoR matrisiyle çelişir |
| 3 | MOD-0165 Campaign içinde campaign-local master | ❌ | Duplicate master; kampanya bitince ürün gerçeği bozulur |
| 4 | KnowledgeContent içinde local brand/product alanı | ❌ | MOD-0162-FU01 §7'de zaten reddedilmişti |

**Kapsam sınırı:** bu FU yalnız **Brand + Product** katmanını yetkilendirir; **Item / SKU / UoM / identifier**
MOD-0290'ın ayrı FU'larıdır.

**EA notu (F2):** `Brand`, MOD-0290'ın Blueprint SoR cümlesinde **adıyla geçmiyor**. Brand pharma/commercial bir
ürün sınıflandırma/üst nesnesidir ve en savunulabilir yeri aynı ürün master'ıdır (CRM'de ikinci master açmamak
için). Bu, parent SoR ifadesinin küçük bir genişletmesidir ve EA teyidi ister.

---

## 5. Brand/Product Not Content-Center

Reddedilen beş model: `KnowledgeContent = Brand/Product content` · "Brand/Product olmadan content oluşturulamaz" ·
"Campaign yalnız Brand/Product üzerinden çalışır" · "Visit Planning yalnız Brand/Product üzerinden çalışır" ·
`Indication/Profile/Need/Benefit`'i Brand/Product içine gömmek.

```text
Brand/Product Master  = commercial/pharma master data       (MOD-0290 — SoR)
KnowledgeContent      = genel içerik                         (MOD-0162-FU01)
Subject Concept Graph = subject bazlı kavram zinciri         (MOD-0162-FU01C)
Brand/Product         = optional metadata veya ExternalRef   (tüketici taraf)
```

---

## 6. Brand Model

`BrandId` · `TenantId` (**JWT claim'inden**) · `BrandCode` (unique, **stabil**) · `BrandName` · `Description?` ·
`BusinessUnit?` · `TherapeuticArea?` · `Status` · `EffectiveFrom` · `EffectiveTo?` · `ExternalReferences[]` ·
audit dörtlüsü.

Kurallar: kod stabil, rename ad ile · **hard delete yok** · **archived brand yeni campaign/content/frequency
linking'inde kullanılamaz** (mevcut linkler history) · **Brand bir KnowledgeContent değildir** · **Brand bir
Campaign değildir**; içerik/mesaj/sıklık/ziyaret hedefi brand içine gömülmez.

---

## 7. Product Model

`ProductId` · `TenantId` · `ProductCode` (unique, stabil) · `ProductName` · `Description?` · **`BrandId?`** ·
`DosageForm?` · `Strength?` · `PackSize?` · `ATCCode?` · `IndicationRefs[]?` · `TherapeuticArea?` · `Status` ·
`EffectiveFrom` · `EffectiveTo?` · `ExternalReferences[]` · audit.

Kurallar: kod stabil, rename kodu değiştirmez · hard delete yok · archived product yeni linking'de kullanılamaz,
mevcut visit/content/campaign history korunur · **ATC ve Indication master SoR'u net olmadığı için yalnız
referans/metadata** · **Product, KnowledgeContent veya Campaign içine kopyalanmaz**.

---

## 8. Brand/Product Hierarchy

| Soru | Karar |
|---|---|
| Product mutlaka Brand altında mı? | **Hayır** — `BrandId` optional (item/SKU master, jenerik ve non-pharma kalemler); zorunluluk şemada değil, **ürün tipi bazlı iş kuralıyla** |
| Brand'siz product? | **Evet, mümkün** |
| Multi-brand product? | **v1 hayır → future** (association nesnesi + provenance ister) |
| `BusinessUnit` seviyesi | Her ikisinde optional; **ikisi doluysa product kazanır** (en spesifik kazanır) |
| `TherapeuticArea` seviyesi | Her ikisinde optional; brand **varsayılan**, product **override** |

Hiyerarşi **tek seviyelidir** (`Brand → Product`); ürün ailesi/portföy ağacı future (F5).

---

## 9. Indication / ATC / TherapeuticArea Boundary

MOD-0162-FU01C kararı korunur: `Indication → AudienceProfile → ProfileNeed → NeedBenefit` **hardcoded değildir**,
concept chain template olarak yaşar.

| Kavram | Karar |
|---|---|
| `ATCCode` | **External taxonomy** (WHO ATC) — master açılmaz; Product'ta kodlanmış referans veya ConceptNode+ExternalRef |
| `Indication` | Product metadata (`IndicationRefs[]`) **veya** ConceptNode; indication master bu FU'da **yok** |
| `TherapeuticArea` | Brand/Product metadata **veya** ConceptNode; SoR kararı **açık** (F6) |
| Belirsiz master | **ExternalRef/metadata** olarak tutulur — belirsiz master **uydurulmaz** |

Bu FU Subject Concept Graph'i **bypass etmez** ve KnowledgeContent'e sabit pharma alanı **dayatmaz**.

---

## 10. Knowledge / Content Integration

Brand/Product **opsiyonel** referanstır (örnek: Almiba Q1 Doctor Deck → BrandId/ProductId/Indication/ATC/
AudienceProfile). Brand/Product **yokken içerik oluşturulabilir**; Almanca/QMS/onboarding'de boş kalır; content
versioning MOD-0162-FU01 kararlarıyla aynı; `FileRef` MOD-0028/0029'da; `KnowledgePath` ve `EngagementJourney`
Brand/Product'a **zorunlu bağımlı değildir**. İçerik tarafında ad/kod kopyalanırsa **görüntüleme türevi** sayılır
ve master değişince **stale** kabul edilir.

---

## 11. Subject Concept Graph Integration

Üç bağlanma biçimi: **ConceptNode** (`ConceptType=brand`, `ExternalRefType=brand`, `ExternalRefId=BrandId`) ·
**metadata** (`KnowledgeContent.BrandId/ProductId`) · **consumer input** (`Campaign.BrandId`,
`FrequencyPolicy.BrandId`, `VisitObjective.BrandId`).

Kurallar: ConceptNode **master SoR değildir**; master SoR **Brand/Product'ta kalır**; node master'ın **kopyası
olamaz**; `ExternalRef` eksikse node yalnız **taxonomy/context**tir ve **master iddiası taşımaz**.

---

## 12. MOD-0165 / MOD-0167 Integration

Campaign/segment/frequency Brand/Product'a **referans** verir (Almiba Q1 → Almiba → Almiba 10mg → ayda 2 →
journey). Bu FU: campaign engine · segmentation engine · frequency runtime · target assignment · due/overdue
**yapmaz**. MOD-0165-FU01 §4'teki `BrandId`/`ProductId` future-optional alanları artık **master'a bağlanabilir**;
davranış değişmez.

---

## 13. MOD-0155 Consumer Boundary

Tüketebilir: visit objective brand/product · recommended content brand/product · campaign brand/product ·
need/benefit bağlamı · journey stage bağlamı.
**Bu FU:** visit/route plan · daily schedule · visit execution · content recommendation · digital detailing ·
usage tracking **yapmaz**.

---

## 14. MDM / Reference Data Boundary

| Soru | Karar |
|---|---|
| Brand/Product | **Master data** (MOD-0290) — MOD-0048 reference set **değil** |
| `DosageForm` / `Status` / ölçü birimi vokabülerleri | **MOD-0048 reference set** (`product-dosage-form` · `product-status` · `brand-status` · `product-uom`); hardcoded enum yasak → fail-closed 400 |
| `Strength` / `PackSize` | Değer + birim; birim vokabüleri UoM tarafında (MOD-0290'ın UoM FU'su) |
| `ATCCode` | **External taxonomy / reference** |
| `TherapeuticArea` | **Concept node veya controlled reference**; SoR kararı F6 |
| `BusinessUnit` | **Mevcut platform/commercial scope** (MOD-0288 / MOD-0151) referanslanır, yeniden üretilmez |
| `IndicationRefs[]` | Referans listesi; indication master yok |

---

## 15. Status / Lifecycle

`draft · active · inactive · archived` + effective window.
Yalnız **active + effective** kayıt yeni campaign/content/frequency linking'inde kullanılabilir · `inactive` yeni
kullanımda önerilmez, history okunur · `archived` yeni linking'e kapalı · **hard delete yok** · effective window
dışında yeni linking yok, mevcut linkler bozulmaz · rename **kod değiştirmez** ·
**Brand archive edilince altındaki product'lar otomatik archive edilmez** (sessiz cascade yasak) ama o brand'e
**yeni product bağlanamaz** ve durum raporda görünür.

---

## 16. External Reference / Legacy Migration

`ExternalReferences[]`: `SourceSystem` · `ExternalId` · `ExternalCode` · `ExternalName` · `ImportedAt` ·
`IsPrimary`.

Kurallar: legacy kod **canonical olmak zorunda değil**, canonical kod **stabil** · legacy mapping **korunur** ·
`(SourceSystem)` başına en fazla **bir primary** → ikincisi **409** · aynı `(SourceSystem, ExternalId)` iki farklı
canonical kayda işaret ediyorsa **deterministik conflict raporu** · **silent merge yasak** · hard delete yok ·
ExternalRef bir **iz kaydıdır, ikinci master değildir**.
Legacy kaynak: `crm-sor-boundary.md` sat. 31'de kayıtlı `Property/PropertyList` ürün kopyası (taşıma planı F9).

---

## 17. Explicit Exclusions

Runtime implementation · backend/frontend/Gateway değişikliği · **Brand/Product CRUD** · import/export ·
campaign engine · segmentation engine · frequency runtime · visit planning · route planning · digital detailing ·
content recommendation engine · Subject Concept Graph runtime engine · AI personalization · KnowledgeContent
implementation · file upload/render/preview · approval workflow · MOD-0023 · patient data · Account/Contact
mutation · territory mutation · **Item/SKU/UoM/identifier yönetimi** · hard delete · Mongo hand-edit ·
RBAC seed/grant · registry write · MOD-0048 publish · `TenantId` payload'da.

---

## 18. Contract Flags

```json
{
  "supportsBrandMaster": true,
  "supportsProductMaster": true,
  "supportsBrandProductHierarchy": true,
  "supportsBrandProductExternalReferences": true,
  "supportsBrandProductKnowledgeMetadata": true
}
```

Pack seviyesinde **öneri**; canlı contract'a yazılmadı. **Eklenmedi:** `supportsVisitPlanning` ·
`supportsRoutePlanning` · `supportsDigitalDetailing` · `supportsCampaignEngine` ·
`supportsRecommendationEngine` · `supportsWorkflowApproval`.

---

## 19. Guard Checks

| Kontrol | Sonuç |
|---|---|
| Runtime code changed? | **No** |
| Backend/frontend/Gateway changed? | **No** |
| **Brand/Product CRUD implemented?** | **No** |
| Campaign engine opened? | **No** |
| Segmentation engine opened? | **No** |
| Frequency runtime opened? | **No** |
| Visit planning opened? | **No** |
| Route planning opened? | **No** |
| Digital detailing opened? | **No** |
| Recommendation engine opened? | **No** |
| Subject Concept Graph runtime opened? | **No** |
| KnowledgeContent runtime opened? | **No** |
| Item/SKU/UoM scope opened? | **No** (MOD-0290'ın diğer FU'ları) |
| Workflow/approval opened? | **No** |
| Patient data opened? | **No** |
| Account/Contact mutation opened? | **No** |
| Territory mutation opened? | **No** |
| RBAC seed/grant changed? | **No** |
| Registry write? | **No** |
| MOD-0048 publish changed? | **No** |
| **Brand/Product boundary added?** | **Yes** |
| **Brand/Product kept optional for KnowledgeContent?** | **Yes** |
| **Pharma compatibility preserved?** | **Yes** |
| **Non-pharma content still supported?** | **Yes** |
| Follow-ups opened? | **Yes** (10 adet) |

Pack frontmatter doğrulaması: `status: draft` · `runtime_code_allowed: false` · `shell: none` ·
`golden_reference: none` · `form_field_count: 0`.

---

## 20. Created / Updated Files

| Dosya | İşlem |
|---|---|
| `execution/domains/master-data-management/module-packs/MOD-0290-FU01-brand-product-master-boundary.md` | **Oluşturuldu** |
| `execution/domains/master-data-management/domain-config.md` | **Güncellendi** (yalnız doküman) — MOD-0290 in-scope kaydı + "CRM/Knowledge/Campaign yalnız referansla tüketir" sınırı + eksik registry satırı notu |
| `execution/domains/commercial-suite/module-packs/MOD-0162-FU01-knowledge-content-subject-taxonomy.md` | **Güncellendi** — §18 F2 kapatıldı, yeni pack'e bağlandı |
| `execution/domains/commercial-suite/module-packs/MOD-0165-FU01-visit-frequency-call-cycle-policy.md` | **Güncellendi** — §20 F1 kapatıldı, yeni pack'e bağlandı |
| `docs/audits/brand-product-master-boundary-pack-authorization-2026-08-02.md` | **Oluşturuldu** (bu rapor) |

Runtime kod, config, gateway, RBAC, reference data ve registry **değiştirilmedi**.

---

## 21. Final Verdict

### **PASS**

- Brand/Product master boundary netleşti ve **canonical SoR Blueprint kanıtıyla MOD-0290** olarak belirlendi;
  dört seçenek açıkça değerlendirilip üçü gerekçeli reddedildi.
- Brand/Product **KnowledgeContent'in merkezi yapılmadı**; içerik Brand/Product olmadan da üretilebilir kaldı.
- Brand/Product **optional metadata / ExternalRef** olarak konumlandı; Campaign ve Content içinde **local/duplicate
  master yasaklandı**.
- `Indication/Profile/Need/Benefit` **hardcoded edilmedi**; Subject Concept Graph uyumu ve concept node'un
  "master değil" kuralı korundu.
- Pharma uyumluluğu korundu (ATC, indication, therapeutic area, dosage form, strength, pack size) ve **non-pharma
  içerik etkilenmedi**.
- Hiyerarşi kararları verildi (Brand opsiyonel, multi-brand future, BU/TA'da "en spesifik kazanır").
- MDM/reference-data ayrımı yapıldı (master vs MOD-0048 set vs external taxonomy vs mevcut BU scope).
- Status/lifecycle ve legacy `ExternalReferences[]` politikası (**silent merge yasağı**, deterministik conflict
  raporu) yazıldı.
- Runtime / CRUD / planning / campaign / detailing scope'u **açılmadı**; mevcut scope'lar bozulmadı.

FAIL kriterlerinin hiçbiri tetiklenmedi. Kayda geçen iki governance açığı (PASS'ı düşürmez, ikisi de EA/registry
aksiyonu): **F1** `MOD-0290` registry satırı eksik · **F2** `Brand` nesnesinin parent SoR cümlesine eklenmesi.

---

## 22. Next Recommended Prompt

`Campaign / Targeting Boundary Pack Authorization`
