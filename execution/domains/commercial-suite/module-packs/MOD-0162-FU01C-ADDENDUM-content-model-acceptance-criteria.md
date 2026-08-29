---
id: MOD-0162-FU01C-ADDENDUM
name: ConceptGraph Implementation FU — Content-Model Boundary & Acceptance Criteria
parent: MOD-0162-FU01C
parent_name: Subject Concept Graph / Configurable Concept Chain Boundary
domain: commercial-suite
service: Diten.CrmService
shell: none
status: draft
runtime_code_allowed: false
runtime_code_scope: "NONE — bu addendum yalnız gelecekteki ConceptGraph implementation FU'su için KABUL KRİTERLERİ ve içerik-modeli boundary'sidir. Aggregate, endpoint, migration, UI açmaz. Runtime yetkisi ayrı bir implementation FU authorization'ı gerektirir."
owner: module-pack-author
supersedes: none
amends: MOD-0162-FU01C (additive; approved gövde değişmez)
started: 2026-08-24
---

# MOD-0162-FU01C — ConceptGraph Implementation FU: İçerik-Modeli Boundary & Kabul Kriterleri (Addendum)

> **Bu doküman nedir:** MOD-0162-FU01C `approved` boundary pack'ine **additive** bir eklentidir. Onaylı pack gövdesini
> **değiştirmez**; ConceptGraph **implementation** FU'su yetkilendirildiğinde uyulması gereken içerik-modeli kararlarını
> ve kabul kriterlerini yazılı hâle getirir.
>
> **Bu doküman ne DEĞİLdir:** Runtime authorization değildir (`runtime_code_allowed: false`). Aggregate/endpoint/UI/
> migration açmaz. Kod yazma yetkisi vermez.
>
> **Kaynak:** 2026-08-24 tasarım oturumu (legacy CRM2 ProjectSettings/UCLN + Pages/PromoSubject analizi ↔ ERP-vNext
> MOD-0162-FU02 karşılaştırması).

---

## 1. Amaç

ConceptGraph runtime'a geçmeden önce, legacy detailing/UCLN mantığının vNext'te **hangi katmana** oturacağını ve
mevcut `KnowledgeContent` (FU02, shipped) modelinin **nasıl korunacağını / genişletileceğini** boundary olarak sabitlemek.
Amaç, implementation başladığında (a) FU02 sözleşmesinin kırılmasını ve (b) sonradan pahalı schema/migration
refactor'unu önlemektir.

---

## 2. Canonical Legacy → vNext Eşlemesi (boundary)

| Legacy (CRM2) | vNext karşılığı | Not |
|---|---|---|
| `Subject` (kategori/type; BRAND, LANGUAGE) | **Yeni `Subject` DEĞİL** | Brand/Language, Subject taxonomy'sinde yeniden üretilmez |
| `SubjectList` = BRAND değeri | **MDM `Brand`** → `KnowledgeContent.BrandId` | Otoriter master MDM'dedir |
| `SubjectList` = LANGUAGE değeri | **`LanguageCode`** | Ayrı alan; taxonomy düğümü değil |
| `SubjectList` = kavramsal değer | **`ConceptNode`** | |
| `UCLEType` | **`ConceptType`** | |
| `UCLNList` | **`ConceptNode`** | |
| `UCLNConnection` | **`ConceptRelationship`** (yönlü edge) | |
| `UCLNDesign` | **`ConceptChainTemplate`** (zincir şablonu = template) | |
| `UCLNBook` / `UclnBookDetail` | **`KnowledgePath` / `ContentPackage` instance** (ConceptGraph'ın kendisi DEĞİL) | §4 |
| `PromoSubject` (bütün) | **`KnowledgePath` / `ContentPackage`** | Her sayfa ayrı `KnowledgeContent` |
| PromoSubject içindeki tek brochure/HTML sayfası | **`KnowledgeContent`** (atomik, yeniden kullanılabilir) | §3 |
| Legacy loyalty (TargetLoyalty%, SKU allocation, patient number) | **TargetCustomer / MicroTarget** (MOD-0155/0167), MDM Product okuyarak | Subject/Topic'e taşınmaz |

**AC-MAP-1:** Implementation, Brand/Product'ı MDM referansı (`BrandId`/`ProductId`), Language'i `LanguageCode` olarak
tüketmelidir; hiçbirini ConceptGraph/Subject içinde **yeniden üretmemelidir**.

---

## 3. Üç-Katmanlı İçerik Modeli — Kütüphane ↔ Zincir ↔ Paket (boundary)

```
KATMAN 1 — KÜTÜPHANE
KnowledgeContent = yeniden kullanılabilir tek doküman (brochure / HTML / video)
   · atomik; bir concept için "kullanılabilecek dökümanlar havuzu"
   · ContentType + ContentStatus + Content Pointer (Url/FileRef/...) BURADA yaşar
   · 1 doküman → N concept'e uygun olabilir

KATMAN 2 — ŞABLON (TEMPLATE)
ConceptChainTemplate = beklenen tip sırası (örn. ATC → Profile → Need → Benefit)
   · UCLNDesign karşılığı; "template" olan budur, KnowledgeContent değil

KATMAN 3 — PAKET / INSTANCE
KnowledgePath / ContentPackage = zinciri seç → her node için kütüphaneden UYGUN
   dökümanı seç → sırala (SortOrder). UCLNBook karşılığı.
```

**AC-MODEL-1:** `KnowledgeContent`, bir Promo Subject'in **tamamına** değil, içindeki **tek bir sayfaya** karşılık gelir.
**AC-MODEL-2:** "Zincir seç + node'ları sırala + her node'a sayfa bağla" akışı `KnowledgeContent` ekranında **değil**,
`KnowledgePath` / `ContentPackage` ekranında kurulur.
**AC-MODEL-3:** `ContentType` / `ContentStatus` / `Content Pointer` alanları **kaldırılmaz**; sayfa (KnowledgeContent)
seviyesinde kalır (bir doküman tip + lifecycle + konum olmadan tüketilemez).

---

## 4. UCLNBook-Home Kararı (boundary)

Legacy `UclnBookDetail` içeriği Book seviyesinde tek parça değil, **`UclnTypeId` + `UclnListId` (node) bağlamında**
saklıyordu → legacy gerçek mantık: `Book → node → o node'a özel content`.

**AC-BOOK-1:** UCLNBook, **ConceptGraph aggregate'ine gömülmez.** Ayrı bir `KnowledgePath` / `ContentPackage`
aggregate'i olarak modellenir (ConceptChainTemplate + seçilmiş node bağlamı + sıralı content referansları + lifecycle).
**AC-BOOK-2:** `ConceptChainTemplate` = **template** (design); `KnowledgePath`/`ContentPackage` = **instance** (book).
İkisi tek aggregate'e birleştirilmez.

---

## 5. İçerik ↔ Concept Kardinalite Kararı (boundary — kritik)

Bugün: `KnowledgeContent.ConceptNodeId : Guid?` → **1 içerik = 1 node** (tekil).

Bu yalnızca "bir içerik yalnız bir node'a aittir" kuralı **kesin** geçerliyse yeterlidir. Pharma detailing'de bu kural
çoğunlukla **geçmez**:
- Aynı brochure hem Need hem Benefit bağlamında kullanılabilir.
- İçerik bir node yerine bir **ilişkiye** (Profile→Need) ait olabilir.
- Bir paketin sayfalarının **sırası** korunmalıdır.

**AC-LINK-1 (önerilen model):** İçerik ↔ concept bağı **many-to-many** olmalıdır:

```
KnowledgeContent
  └── KnowledgeContentConceptLink[]
        ├── ConceptNodeId
        ├── ConceptRelationshipId (opsiyonel)
        ├── LinkRole
        └── SortOrder
```

**AC-LINK-2:** Mevcut `ConceptNodeId` **silinmez**; geriye dönük uyum için **"primary node" kısayolu** olarak korunur.
Asıl bağlar `KnowledgeContentConceptLink` üzerinden yürür (zorunlu 1 primary FK + N esnek link deseni). Bu, FU02
verisini bozmadan geçişi **additive** kılar.
**AC-LINK-3:** Eğer implementation "1 içerik = 1 node" kuralını **kanıtla** sabitlerse, tekil `ConceptNodeId` korunabilir;
aksi hâlde AC-LINK-1 uygulanır. Karar, implementation pack'inde **açıkça gerekçelendirilmelidir**.

---

## 6. FU02 Sözleşme Koruması (boundary)

**AC-FU02-1:** `KnowledgeContent` **atomik** kalır; "zincir + çoklu sayfa paketi"ne dönüştürülmez.
**AC-FU02-2:** `IKnowledgeContentLinkageReader.ResolvePublishedContentAsync(...)` sözleşmesi (Campaign tüketimi)
**kırılmaz**; provider tek published+effective içerik döndürmeye devam eder.
**AC-FU02-3:** DELETE/PATCH yok; archive lifecycle korunur; tenant claim server-resolved kalır; `ContentVersion` iş
versiyonu, `EntityBase.Version` concurrency ayrımı korunur.
**AC-FU02-4:** Değişiklikler **additive**'dir (yeni link tablosu + yeni Package aggregate); mevcut FU02 alanları/endpoint'leri
kaldırılmaz.

---

## 7. Ön Koşul / Sıra (boundary)

**AC-SEQ-1:** Sıra: **(1) ConceptGraph runtime (FU01C impl)** → **(2) KnowledgePath / ContentPackage** → **(3)**
KnowledgeContent per-node sayfa olarak tüketilir. Package, ConceptGraph olmadan açılamaz (zincir/node veri kaynağı yok).
**AC-SEQ-2:** ConceptGraph implementation başlamadan **legacy migration crosswalk** (Subject/SubjectList/UCLEType/
UCLNList/UCLNConnection/UCLNDesign/UCLNBook/PromoSubject → external-ID) tanımlanmalıdır.
**AC-SEQ-3:** RBAC alignment (`crm.knowledge.concept.*` / `crm.knowledge.path.*`) **en sona** bırakılır (kullanıcı
kararıyla deferred).

---

## 8. Knowledge UI Semantic Clarification (ConceptGraph öncesi, additive)

**AC-UI-1:** Knowledge UI'da `Subject` = "bilgi/konu alanı (knowledge domain)" olarak netleştirilir; legacy "Subject Type"
ile karıştırılmaz.
**AC-UI-2:** `BrandId`/`ProductId` raw-GUID input'ları → MDM'den beslenen picker'a hazırlanır (Brand seç → Product filtrele).
**AC-UI-3:** `ConceptNodeId`, ConceptGraph runtime gelene kadar **gizlenir veya "henüz çözümlenmiyor" notu taşır**; geldikten
sonra subject/type-zincirli selector olur.
**AC-UI-4:** `AudienceProfile` ↔ "Profile adlı ConceptType" SoR sınırı UI'da görünür kılınır.

---

## 9. Kapsam Dışı (bu addendum ve boundary aşaması yapmaz)

- Kod / aggregate / endpoint / migration / UI (runtime yetkisi yok).
- FU01C `approved` pack gövdesinin değiştirilmesi (bu dosya additive'dir).
- Registry / tracker güncellemesi, Mongo hand-edit, seed/grant.
- MOD-0155 (Visit/Route/MicroTarget) açılışı.
- KnowledgeContent'ten ContentType/Status/Pointer'ın kaldırılması (AC-MODEL-3 ile açıkça yasak).

---

## 10. Kabul Kriterleri — Özet Kontrol Listesi

- [ ] AC-MAP-1 — Brand/Product→MDM, Language→LanguageCode; Subject'te yeniden üretim yok
- [ ] AC-MODEL-1/2/3 — KnowledgeContent=sayfa; paket=KnowledgePath; type/status/pointer sayfada kalır
- [ ] AC-BOOK-1/2 — UCLNBook = ayrı ContentPackage/KnowledgePath; template≠instance
- [ ] AC-LINK-1/2/3 — many-to-many `KnowledgeContentConceptLink`; `ConceptNodeId` primary shortcut korunur (veya 1:1 kanıtla sabitlenir)
- [ ] AC-FU02-1..4 — FU02 atomikliği + Campaign reader sözleşmesi + additive değişiklik korunur
- [ ] AC-SEQ-1/2/3 — ConceptGraph→Package sırası; migration crosswalk; RBAC deferred
- [ ] AC-UI-1..4 — semantic clarification + picker-readiness + ConceptNodeId gizle/not

> **Sıradaki adım:** Bu addendum'u temel alan bir **ConceptGraph implementation FU pack taslağı** (`/prepare-module-pack`),
> yukarıdaki AC'leri "Definition of Done" olarak taşımalıdır. Runtime yetkisi ayrı authorization ile açılır.
