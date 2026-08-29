---
id: MOD-0167-FU04
name: Strategy Template - Segment x Product SKU Mix x Content Playbook
parent: MOD-0167
parent_name: Segmentation / CDP
implements_boundary: MOD-0167-FU02 §1.2 (FU-C satırı) + DCP-006 (CRM SoR boundary) + legacy CrmV2 SubjectList/ForWhom analizi
siblings: MOD-0167-FU01, MOD-0167-FU02
domain: commercial-suite
service: Diten.CrmService + frontend/Diten.Web
shell: tenant
golden_reference: compact
entity_base: EntityBase
status: ready-for-dev
runtime_code_allowed: true
runtime_code_scope: "AÇIK (ready-for-dev, flip 2026-08-28 kullanıcı kararı; D-SKU-LINK author-asserted containment kabul edildi). Yetkilendirilen kapsam: `StrategyTemplate` aggregate (yeniden kullanılabilir playbook: Segment(ler) IN-SERVICE + Frequency niyeti REFERANS/ifade + `ProductLines[]→SkuAllocations[]` MDM GlobalProduct/Gsku CROSS-SERVICE FAIL-CLOSED + içerik KnowledgePath/ContentEngagementJourney IN-SERVICE), lifecycle + versiyon, CQRS + persistence + Compact UI (`Diten.CrmService` + `frontend/Diten.Web`), cross-service `MdmStrategyTemplateReferenceValidator` (GlobalProduct+Gsku, CreateAsync/ReplaceAsync öncesi, 503 no-partial). YASAK (flip sonrası da): VisitFrequencyPolicy yazımı (create/update/Source=segmentation), CampaignTarget üretimi, apply/generate/snapshot to cycle (→ MOD-0155), UCLN loyalty/promo/hasta planı (→ MOD-0155), SubjectList/UCLN'i ayrı aggregate kurmak, Brand kullanımı/BrandId, Segment/MDM/MOD-0162 mutation, yeni MDM okuma yüzeyi, RBAC seed/grant, MOD-0048 publish, ocelot/registry write, Mongo hand-edit."
owner: module-pack-author
branch: feature/crm/mod-0167-fu04-strategy-template
started: 2026-08-28
target: TBD (kullanıcı onayı sonrası)
form_field_count: 13   # türetme §11.1'de GÖSTERİLİR (13 > 8 → Compact). Gömülü 4 repeater ayrı yüzey DEĞİLDİR.
dependencies:
  - MOD-0167 (parent — Segment / TargetCustomer / StrategyTemplate SoR)
  - MOD-0167-FU02 (FU-A, SHIPPED — `Segment` aggregate'i; BURADA yalnız SALT-OKUNUR referanslanır)
  - MOD-0165-FU01 / MOD-0165-FU03 (boundary — `VisitFrequencyPolicy` SoR; bu FU policy YAZMAZ, yalnız referanslar/ifade eder)
  - MOD-0162-FU04 (in-service — `KnowledgePath`, SALT-OKUNUR referans)
  - MOD-0162-FU05 (in-service — `ContentEngagementJourney`, SALT-OKUNUR referans)
  - MDM / MOD-0290 (cross-service, FAIL-CLOSED — `GlobalProduct` + `Gsku` referans doğrulaması; MDM'e YAZILMAZ)
  - MOD-0155 (consumer — "apply/generate to cycle" MicroTarget'ta; bu FU'da YOK)
  - MOD-0048 (reference data — D-VOCAB=A: in-domain vokabüler, runtime ön koşulu DEĞİL)
  - MOD-0018 (RBAC — yalnız tüketim; seed/grant bu pack'te YOK)
  - DEV-0001 (Golden Reference Compact — tek yüzey, tek klasör)
---

# MOD-0167-FU04 — StrategyTemplate (Segment × Frequency Niyeti × Ürün/SKU Karması × İçerik)

> **✅ READY-FOR-DEV — KOD YETKİSİ AÇIK (flip 2026-08-28 kullanıcı kararı; 15 D-kararı + D-SKU-LINK kabul).**
> `status: ready-for-dev`, `runtime_code_allowed: true`. `@orchestrator` bu pack ile kod yazabilir; kapsam yalnızca
> yukarıdaki `runtime_code_scope` ile sınırlıdır ve oradaki YASAK maddeleri (VisitFrequencyPolicy yazımı,
> apply/cycle/UCLN, SubjectList/UCLN aggregate, Brand, yeni MDM okuma yüzeyi, ocelot/registry write) flip sonrası da bağlayıcıdır.
>
> **FU numarası — DCP-002 kimlik kapısı PASS (2026-08-28):**
> `py .antigravity/scripts/verify_module_id.py . --check-id MOD-0167-FU04 --name "Strategy Template - Segment x Product SKU Mix x Content Playbook" --parent MOD-0167`
> → `OK  MOD-0167-FU04: proven against Blueprint/registry.` (**exit 0**).
> Parent `MOD-0167 | Segmentation / CDP` Blueprint canonical'dır (registry satırı 239).
> **FU-C ≡ MOD-0167-FU04** — bu eşleme MOD-0167-FU02 §1.2'de zaten **önerilmiş** ve §20/F-STRATEGY'de
> `MOD-0167-FU04` olarak **adıyla** rezerve edilmiştir; bu pack o satırı doldurur. Registry satırı bu pack
> tarafından **yazılmaz** (§20/F-REG).
>
> **⚠️ FU02 §1.2'den SAPMA (kasıtlı, legacy analizine dayanır):** FU02, FU-C'yi
> *"StrategyTemplate + SubjectList + ForWhom"* olarak öngörmüştü. Legacy CrmV2 şemasının okunmasıyla bu tarif
> **yanlış** olduğu görüldü (§2.4): `SubjectList` bir **audience** değil **ürün + SKU % dağılımı**dır,
> `ForWhom` bir segment değil **içerik audience'ı**dır (MOD-0162'de **zaten var**), `UCLN` ise **MicroTarget'a**
> (MOD-0155) ait bir sadakat/promo/hasta planıdır. Bu nedenle bu pack `SubjectList` ve `UCLN` adında
> **hiçbir aggregate açmaz**; onların **gerçek rollerini** StrategyTemplate'in **bağları** olarak modeller.
>
> Otorite sırası: **Blueprint Excel** > bu pack > MOD-0167-FU02 (shipped runtime) > MOD-0167-FU01 (draft boundary) >
> [Domain Config](../domain-config.md) > `AGENTS.md` > `.antigravity/rules/`.

---

## 1. Module Summary

MOD-0167-FU02 **"kim?"** sorusunu cevapladı. MOD-0165 **"ne sıklıkta?"**yi, MDM **"hangi ürün/SKU?"**yu,
MOD-0162 **"hangi sunum?"**u cevaplıyor. Bugün repoda bu dört cevabı **bir arada, yeniden kullanılabilir bir
adla** tutan hiçbir nesne yok. Sahada bir yönetici "Kardiyoloji A-segmenti için standart oyun planımız" dediğinde
bunun karşılığı dört ayrı ekranda dağınık duruyor.

Bu FU **tam olarak o adı** açar ve **başka hiçbir şey yapmaz**:

```text
StrategyTemplate  = yeniden kullanılabilir PLAYBOOK — adlandırılmış bir BAĞ DEMETİ        (BU FU)
  ├── Segment(ler)        → "kim"          → MOD-0167-FU02   (in-service, SALT-OKUNUR)
  ├── Frequency niyeti    → "ne sıklıkta"  → MOD-0165        (REFERANS veya İFADE — policy YAZILMAZ)
  ├── Ürün + SKU %        → "ne satılıyor" → MDM             (cross-service, FAIL-CLOSED)
  └── İçerik              → "hangi sunum"  → MOD-0162 FU04/FU05 (in-service, SALT-OKUNUR)
```

**Tek cümlelik mimari kural:** *StrategyTemplate **bağlar**, **üretmez**.* Bir template'ten hiçbir üye, hiçbir
policy, hiçbir campaign target, hiçbir cycle satırı, hiçbir MicroTarget **doğmaz**. Üretim (apply/generate)
**MOD-0155'in** işidir (§Ek D/D-APPLY).

| Cevapladığı soru | Sahip |
|---|---|
| Bu oyun planının adı, sürümü, geçerlilik penceresi nedir? | **Bu FU** |
| Bu plan **hangi segment(ler)** için yazıldı? | **Bu FU** (bağ) — segmentin kendisi **FU02** |
| Bu planın **frekans niyeti** nedir (hangi policy'ye atıf / hangi ritim beyanı)? | **Bu FU** (bağ) — policy **MOD-0165** |
| Bu planda **hangi ürün**, ürün içinde **hangi SKU'lar hangi yüzdeyle**? | **Bu FU** (bağ + yüzde) — ürün/SKU master **MDM** |
| Bu planda **hangi içerik akışı** anlatılacak? | **Bu FU** (bağ) — path/journey **MOD-0162** |

| Cevaplamadığı soru | Sahip |
|---|---|
| Bu segmentte **kimler var** (üyelik çözümlemesi) | MOD-0167-FU02 — bu FU **resolve etmez**, üye listesi **döndürmez** |
| Ziyaret sıklığı **kuralı** (policy kaydı) | MOD-0165 — bu FU policy **yazmaz**, `Source=segmentation` policy **üretmez** |
| Bu plan **bir döneme nasıl uygulanır** (apply / generate) | **MOD-0155 (MicroTarget)** — bu FU'da **YOK** |
| `CyclePeriod` / call-cycle takvimi | MOD-0165 — **henüz yapılmadı**; bu FU onu **açmaz** |
| Sadakat yüzdesi / promo haftası / hasta sayısı planı (legacy **UCLN**) | **MOD-0155 (MicroTarget)** — bu FU'da **YOK** |
| Marka (Brand) bağı | **HİÇ KİMSE — bu üründe kullanılmıyor** (kullanıcı kararı, §2.1 / D-BRAND) |
| Ürün / SKU master'ı | MDM — bu FU **okur ve doğrular**, kopyalamaz |
| İçerik audience'ı (legacy **ForWhom**) | MOD-0162 (AudienceProfile / ConceptGraph) — **zaten var**, tekrarlanmaz |

**Reddedilen dört model:** `Segment`'e gömülü strateji alanları (FU02 aggregate'ini kirletir) ·
ayrı bir `SubjectList` **audience** aggregate'i (legacy'de zaten audience değildi) ·
`Campaign`'e gömülü ürün karması (MOD-0165 SoR ihlali) ·
template'ten üretilen `VisitFrequencyPolicy`/`CampaignTarget` satırları (MOD-0165 SoR + MOD-0167-FU01/D2 ihlali).

### 1.1 D-Karar özeti (onayınıza sunulur — tam gerekçe: [Ek D](#ek-d--karar-gerekçeleri-tam))

| # | Karar | Öneri |
|---|---|---|
| **D-FU** | FU numarası | **FU-C ≡ MOD-0167-FU04** (DCP-002 gate exit 0; FU02 §20/F-STRATEGY bu id'yi zaten rezerve etmiş) |
| **D-BIND** | Template'in doğası | **Yalnız bağ (authoring-only).** Üretim/snapshot/engine **yok**; consumer seam `IStrategyTemplateReader` **salt-okunur** |
| **D-SEG** | Segment bağı | **Somut `SegmentId`'ye PIN** (FU-A'da her sürüm ayrı dokümandır → id pinlemek sürüm pinlemektir). Lineage'a bağlanıp "en son aktif"e kayma **reddedildi**. Çoklu segment **serbest**, ama hepsi **aynı `SubjectType`** olmak zorunda |
| **D-FREQ** | Frekans niyeti | **Üç modlu, tek şekilli:** `policy-reference` (mevcut aktif policy'ye atıf) \| `declared-intent` (MOD-0165 vokabüleriyle **beyan**, BAĞLAYICI DEĞİL) \| `none`. **Her üç modda da policy YAZILMAZ** |
| **D-MIX** | Ürün → SKU % yapısı | **Gömülü iki katman:** `ProductLines[]` (GlobalProduct) → `SkuAllocations[]` (Gsku + yüzde). SKU satırı olan her hat için toplam **tam 100.00**; otomatik normalize **yok** |
| **D-SKU-LINK** | SKU'nun ürüne aidiyeti | **v1'de DOĞRULANAMAZ ve doğrulanmış GİBİ yapılmaz.** `Gsku`'da `GlobalProductId` **yoktur** (`ProductDefinitionRevisionId` taşır) ve mevcut selector ürün filtresi **sunmaz**; yeni MDM okuma yüzeyi açmak **yasak**. İki id **ayrı ayrı** var-yok doğrulanır; aidiyet → **F-SKU-PRODUCT-LINK** |
| **D-LSKU** | Lsku (yerel SKU) | **v1'de ERTELENİR.** Lsku market-scoped'tur; bağlamak template'e olmayan bir "market" boyutu ekler → **F-LSKU** |
| **D-CONTENT** | Hangi MOD-0162 entity'si | **İKİSİ de** — tiplenmiş satır (`knowledge-path` \| `content-engagement-journey`), **published** ve **pinlenmiş** olmak zorunda (MOD-0162-FU05 emsali). Ham `KnowledgeContent` bağı **v1'de yok** (F-CONTENT-ITEM) |
| **D-VER** | Sürüm / effective dating | FU-A/D-VER **birebir**: `TemplateVersion` (iş alanı) + `VersionLineageId` + `new-version` klonu + `activate` anında **bağların DONDURULMASI** (`BindingsFrozenAt`) |
| **D-APPLY** | "apply / generate to cycle" | **ERTELENİR → MOD-0155-FU05 (MicroTarget).** Contract flag `supportsStrategyApply: false` — sessiz varsayım yasağı |
| **D-BRAND** | Marka | **KULLANILMAZ** (kullanıcı kararı — Brand sayfası kullanılmıyor). Şemada `BrandId` **yoktur**, FU-A validator'ının brand yolu **tüketilmez** |
| **D-VOCAB** | Vokabüler | **A = in-domain fail-closed** (FU02/FU03/FU04/FU05 + MOD-0164-FU02 emsali); MOD-0048 publish runtime ön koşulu **değil** |
| **D-TENANT** | Tenant izolasyonu | `EntityBase` tenant-owned; `TenantId` server-side; cross-tenant **404 / boş liste** |
| **D-RBAC** | Yetki | 3 kanonik anahtar **tanımlanır**, seed/grant **YOK**; belgelenmiş fallback (FU-A emsali) |
| **D-GOLDEN** | Golden reference | **Compact** (13 kullanıcı alanı — §11.1 türetmesi), **tek** sayfa `/CRM/StrategyTemplates` |

### 1.2 FU decomposition teyidi (MOD-0167 yol haritası — FU02 §1.2 güncellemesi)

| Kullanıcı etiketi | Kanonik FU | Kapsam | Durum |
|---|---|---|---|
| **FU-A** | MOD-0167-FU02 | `Segment` + kriter + real-time çözümleme + `TargetCustomer` + UI | **SHIPPED** (bu pack'in authority'si) |
| **FU-B** | MOD-0167-FU03 *(önerilen)* | Ölçek katmanı: materialized membership + refresh + üyelik geçmişi | önerilir; **bu FU'nun ön koşulu DEĞİL** |
| **FU-C** | **MOD-0167-FU04** | **`StrategyTemplate` = Segment × frekans niyeti × ürün/SKU % × içerik bağı** | **BU PACK (draft)** |
| **FU-D** | MOD-0167-FU05 *(önerilen)* | CDP türev nitelikleri (RFM, ICP score) + segment usage log | önerilir |
| — | MOD-0167-FU01 | Segment→frequency co-author boundary'si | mevcut, **BOZULMAZ** |
| — | MOD-0167-FU-RBAC | `crm.segment.*` + `crm.strategy-template.*` katalog + rol ataması | önerilir, en sona |

> **FU-B bu FU'nun ön koşulu değildir:** StrategyTemplate segment **üyeliğini** hiç çözmez, yalnız segmentin
> **kimliğine** atıf yapar. Ölçek borcu (FU-B) bu FU'nun davranışını **değiştirmez**.

### 1.3 Bu FU'nun MOD-0155'e (MicroTarget) sağladığı şey

```text
MOD-0155-FU05 sorusu : "Bu dönemde, bu temsilcinin, bu hedef için planı ne?"
FU04'ün cevabı       : IStrategyTemplateReader.GetActiveBindingsAsync(templateId, effectiveAt)
                       → { segmentIds[], frequencyIntent, productLines[+skuMix], contentBindings[] }
                       — SALT-OKUNUR. Üye YOK, satır üretimi YOK, yazma YOK.
```

MicroTarget bu demeti okur ve **kendi** satırlarını üretir. Legacy'nin `UCLN` planlaması
(`PlannedPromoWeek` / `TargetLoyaltyPercentage` / `PatientNumber`) **orada** modellenir, burada değil (§2.4).

---

## 2. Ownership and Boundaries

**In-scope:** `StrategyTemplate` aggregate root'u ve **içindeki 4 gömülü bağ listesi**
(`SegmentBindings` · `FrequencyIntent` · `ProductLines[→SkuAllocations]` · `ContentBindings`) ·
CRUD-minus-delete (create/read/update/activate/archive; **DELETE ve PATCH yok**) ·
sürümleme (`new-version` klonu) + `activate` anında bağ dondurma · effective dating ·
in-service referans doğrulaması (Segment / KnowledgePath / ContentEngagementJourney / VisitFrequencyPolicy — **hepsi salt-okunur**) ·
cross-service **fail-closed** MDM referans doğrulaması (`GlobalProduct` + `Gsku`) ·
SKU yüzde toplamı doğrulaması · in-domain vokabüler · salt-okunur tüketim seam'i (`IStrategyTemplateReader`) ·
CRM Admin **tek** Compact sayfa · 7 dil RESX.

**Out-of-scope (§13 + Ek D):** apply / generate / snapshot · MicroTarget satırı üretimi · `CyclePeriod` ·
`VisitFrequencyPolicy` yazımı · `CampaignTarget` üretimi · segment üyelik çözümlemesi · UCLN sadakat/promo/hasta planı ·
`SubjectList` adında ayrı audience aggregate'i · `ForWhom` adında yeni audience aggregate'i · Brand bağı ·
Lsku bağı · MDM write · MOD-0162 mutation · MOD-0167-FU02 mutation · MOD-0165 mutation · hard delete ·
RBAC seed/grant · MOD-0048 publish · `ocelot.json` yazımı · registry yazımı · Mongo hand-edit.

### 2.1 Kilitli sınırlar (kullanıcı talebinden — değiştirilemez)

| Sınır | Karar |
|---|---|
| Template'in doğası | **BAĞLAR, ÜRETMEZ.** Authoring-only; hiçbir çıktı satırı doğurmaz |
| MOD-0167-FU02 | `Segment` **SALT-OKUNUR** referanslanır; segment/kriter/üyelik **mutate edilmez**, `resolve` **çağrılmaz** |
| MOD-0165 | `VisitFrequencyPolicy` **SoR MOD-0165'te**. Bu FU policy **YAZMAZ** — ne create, ne update, ne `Source=segmentation` üretimi. Segment→frequency **yazımı** MOD-0167-FU01 co-author'ın işidir, bu FU'nun değil |
| MDM | `GlobalProduct` + `Gsku` **cross-service**, **fail-closed**, **CreateAsync/ReplaceAsync ÖNCESİ**. MDM'e **yazılmaz** |
| **Brand** | **KULLANILMAZ.** Şemada `BrandId` yok, UI'da marka seçici yok, validator'da brand kind yok |
| MOD-0162 | `KnowledgePath` / `ContentEngagementJourney` **SALT-OKUNUR** referanslanır; içerik **mutate edilmez** |
| Legacy CrmV2 | **adapt-not-copy.** Legacy controller/view/şema **taşınmaz** (§2.4) |
| Apply / cycle / UCLN | **ERTELENİR** (MOD-0155 / MOD-0165). Contract flag'lerle **açıkça kapalı** ilan edilir |
| Golden reference | **Compact** (§11.1 türetmesi) |
| RBAC | Anahtarlar **tanımlanır**; seed/grant **YOK** (§14) |
| Registry / Gateway config | **YAZILMAZ**. Route ihtiyacı `integration-agent` task'ı (§15) |

### 2.2 MOD-0167-FU02 sözleşme koruması (kırmızı çizgi)

- FU02'nin **hiçbir** dosyası değişmez (§6 protected): `Features/Segmentation/**`, `Domain/Entities/Segment.cs`,
  `Domain/Entities/TargetCustomer.cs`, `ISegmentRepository`, `SegmentsController`, `Views/CRM/Segments/**`.
- `ISegmentRepository` imzası **genişletilmez**; bu FU yalnız **var olan** `GetByIdAsync` ile bir segmentin
  *"var mı, arşivli mi, hangi `SubjectType`, hangi `SegmentStatus`"* sorusunu sorar.
- `ISegmentMembershipReader` **çağrılmaz**: template üye **görmez**. Bir template'i okumak, o segmentin
  kişilerini görme yetkisi **vermez** (FU02'nin `.resolve` ↔ `.read` PII ayrımı burada da korunur).
- FU02'nin `supportsStrategyTemplate: false` contract flag'i, bu FU ship edildiğinde FU02 tarafında
  güncellenebilir — ama **bu pack o dosyayı yazmaz** (§20/F-FU02-FLAG).

### 2.3 MOD-0165 sözleşme koruması (kırmızı çizgi)

- `VisitFrequencyPolicy` **yazılmaz.** Bu FU'nun `IVisitFrequencyPolicyRepository` kullanımı **yalnız**
  `GetByIdAsync` (okuma) ile sınırlıdır; `InsertAsync`/`ReplaceAsync` **çağrılmaz** ve testle sabitlenir (§17.2).
- MOD-0165'in resolve provider'ı bu FU'nun `declared-intent` beyanını **okumaz** ve **okumayacaktır**:
  beyan **bağlayıcı değildir**, yalnız yazarın niyetini kayda geçirir. Bir şeyin "policy gibi davrandığı"
  izlenimi contract flag `supportsFrequencyPolicyWrite: false` ile **açıkça** reddedilir.
- `Campaign` / `CampaignTarget` **hiç dokunulmaz**; template kampanya **üretmez**.

### 2.4 Legacy CrmV2 — adapt-not-copy (bu FU'nun zemini)

Legacy `C:\CRM2\DitenCrmV2` şeması okunarak (2026-08-28) şu **düzeltmeler** yapıldı:

| Legacy nesne | Legacy'de **gerçekte** ne | vNext karşılığı |
|---|---|---|
| `SubjectList` + `SkuAllocation` | **Ürün + SKU % dağılımı** (`GlobalBrandId` + `SkuId` + `Percentage` + `TotalPercentage`) — audience **değil** | **BU FU**: `ProductLines[] → SkuAllocations[]` (Brand **düşürüldü**, ürün = MDM `GlobalProduct`, SKU = MDM `Gsku`) |
| `ForWhom` | **İçerik audience'ı** (`Diten.Content/ContentList.ForWhomId`) — segment **değil** | **MOD-0162** (AudienceProfile / ConceptGraph) — **zaten var**, bu FU **tekrarlamaz**; yalnız içeriğe **atıf** yapar |
| `UCLN` + `UCLNListPriority(Detail)` | **Ürün sadakat sınıflandırması + hedef-başına plan** (promo haftası, sadakat %, hasta sayısı) | **MOD-0155 (MicroTarget)** per-target plan + **MDM** ürün sınıflandırması — **bu FU'da YOK** |
| `TargetCustomer` (legacy) | **Temsilci-başına promosyon planı** (Zone/Applicable/Workplace/Client) | **MOD-0155 (MicroTarget)** — ⚠️ **AD ÇAKIŞMASI**: MOD-0167-FU02'nin `TargetCustomer`'ı (manuel segment üyeliği) **başka bir şeydir** |

**Kural:** legacy tablo/controller/view **taşınmaz**; yalnız **iş sorusu** taşınır.
`frontend/Diten.Web/Controllers/Archive/**` ve `Views/Archive/**` **FROZEN**'dır (§6).

---

## 3. Owned Objects

| Tür | Nesne |
|---|---|
| **Entity** | `StrategyTemplate` (aggregate root) · gömülü: `StrategyTemplateSegmentBinding` · `StrategyTemplateFrequencyIntent` · `StrategyTemplateProductLine` · `StrategyTemplateSkuAllocation` · `StrategyTemplateContentBinding` |
| **Repository** | `IStrategyTemplateRepository` (**1 repo, 1 collection** — §4.7) |
| **Commands** | `CreateStrategyTemplate` · `UpdateStrategyTemplate` · `ActivateStrategyTemplate` · `ArchiveStrategyTemplate` · `CreateStrategyTemplateVersion` |
| **Queries** | `ListStrategyTemplates` · `GetStrategyTemplateById` · `GetStrategyTemplateContract` · `GetStrategyTemplateBindings` |
| **Services** | `StrategyTemplateBindingValidator` (in-service FK doğrulaması) · `IStrategyTemplateProductReferenceValidator` (cross-service sözleşme) · `MdmStrategyTemplateReferenceValidator` (impl, Infrastructure) · `StrategyTemplateAllocationRules` (yüzde kuralları, saf fonksiyon) |
| **Consumer seam** | `IStrategyTemplateReader` (**salt-okunur**; MOD-0155 MicroTarget tüketicisi için) |
| **API** | §8.1 — 9 endpoint, hepsi `/api/crm/strategy-templates…` altında |
| **Frontend route** | `/CRM/StrategyTemplates` (tek Compact sayfa) |
| **Permissions** | `crm.strategy-template.read` · `.manage` · `.activate` (§14) |

---

## 4. Entity Fields

### 4.1 `StrategyTemplate` (aggregate root)

| Alan | Tip | Zorunlu | Kural |
|---|---|---|---|
| `Id` | Guid | otomatik | `StrategyTemplateId`. `EntityBase` |
| `TenantId` | Guid | server-side | Payload'da **yer almaz** (D-TENANT) |
| `TemplateCode` | string | **Evet** | Kararlı iş anahtarı; tenant içinde arşivlenmemişler arasında **unique** (**handler'da** doğrulanır — §4.7); **rename edilmez** |
| `TemplateName` | string | **Evet** | max 200, trim |
| `SubjectType` | string | **Evet** | `account` \| `contact` — planın **neyi** hedeflediği. **Create sonrası IMMUTABLE**; **tüm** segment bağları bununla eşleşmek zorunda (§12.2) |
| `TemplateStatus` | string | **Evet** | `draft` \| `active` \| `archived` (§4.6). Varsayılan `draft` |
| `TemplateVersion` | int | **Evet** | **İş** sürümü; ilk sürüm `1`. `EntityBase.Version` ile **karıştırılmaz** (`entity-base-template.md`) |
| `VersionLineageId` | Guid | **Evet** | Tüm sürümleri bağlayan kök kimlik. İlk sürümde `= Id` |
| `SupersededByTemplateId` | Guid? | Hayır | `new-version` + `activate` ile **server-side** dolar |
| `BusinessUnitId` | string? | Hayır | Opak MOD-0048 business-unit kodu (boş-olmayan string doğrulaması; master **okunmaz**) |
| `Description` | string? | Hayır | max 2000 |
| `EffectiveFrom` | DateTimeOffset | **Evet** | Sürümün geçerlilik başlangıcı |
| `EffectiveTo` | DateTimeOffset? | Hayır | Boş = açık uçlu. `EffectiveTo > EffectiveFrom` |
| `SegmentBindings` | `List<StrategyTemplateSegmentBinding>` | **Evet (≥1)** | §4.2. Segmentsiz playbook **authorable değildir** — "kim" cevapsız kalır |
| `FrequencyIntent` | `StrategyTemplateFrequencyIntent` | **Evet** | §4.3. `Mode=none` **geçerli bir cevaptır**; alanın kendisi opsiyonel **değildir** (sessiz varsayım yasağı) |
| `ProductLines` | `List<StrategyTemplateProductLine>` | Hayır | §4.4. Boş = ürün boyutu **yok** (uydurulmaz) |
| `ContentBindings` | `List<StrategyTemplateContentBinding>` | Hayır | §4.5.1. Boş = içerik boyutu **yok** |
| `Notes` | string? | Hayır | max 2000 |
| `BindingsFrozenAt` | DateTimeOffset? | Hayır | `activate` anında server-side damgalanır; dolu iken **tüm bağ listeleri** değişmez (D-VER) |
| `ActivatedAt` / `ActivatedBy` | DateTimeOffset? / string? | Hayır | Audit damgası |
| `ArchivedAt` / `ArchivedBy` | DateTimeOffset? / string? | Hayır | Soft lifecycle; **hard delete yok** |
| `CreatedBy` / `UpdatedBy` | string? | Hayır | Actor damgası |
| `Version` | int | otomatik | **Teknik** concurrency token (`EntityBase`) |

> `StrategyTemplate` üzerinde **bulunmayan ve bulunamayacak** alanlar: `BrandId` (D-BRAND) ·
> `MemberIds[]` / `MemberCount` (üyelik FU02'nin, PII) · `GeneratedPolicyIds[]` / `GeneratedTargetIds[]`
> (üretim yok) · `CycleId` / `CyclePeriodId` (MOD-0165, yapılmadı) · `LoyaltyPercentage` / `PlannedPromoWeek` /
> `PatientNumber` (legacy UCLN → MOD-0155) · `AppliedAt` / `LastAppliedCycle` (apply yok) ·
> ürün/SKU adının **kopyası SoT olarak** (yalnız display — §4.4/§4.5).

### 4.2 `StrategyTemplateSegmentBinding` — gömülü ("kim")

| Alan | Tip | Zorunlu | Kural |
|---|---|---|---|
| `BindingId` | Guid | otomatik | Template içinde unique |
| `SegmentId` | Guid | **Evet** | **MOD-0167-FU02 `Segment.Id`** — yani **belirli bir sürüm satırı** (D-SEG: id pinlemek sürüm pinlemektir) |
| `SegmentLineageId` | Guid | server-side | Bağ anında segmentten **okunarak** damgalanır; yalnız izlenebilirlik (SoT segmentin kendisi) |
| `SegmentVersionAtBinding` | int | server-side | Aynı şekilde damgalanır; **sonradan güncellenmez** — kayma görünür olsun diye |
| `SegmentCodeDisplay` | string? | server-side | **Yalnız görüntü/audit**, SoT **değil** (FU02 `TargetCustomer.SubjectDisplayName` emsali) |
| `BindingRole` | string? | Hayır | `primary` \| `secondary` \| `exclusion-note` (§4.6) — **yalnız etiket**; hiçbir küme cebiri **uygulanmaz** (§8.3) |
| `SortOrder` | int | **Evet** | Template içinde unique; determinizmin parçası |
| `Notes` | string? | Hayır | max 500 |

**Kurallar:** aynı `SegmentId` **iki kez** bağlanamaz (400) · bağlanan segment **arşivli olamaz** (400) ·
segmentin `SubjectType`'ı template'inkiyle **eşleşmek zorunda** (400) · `activate` anında **her** bağlı segment
`active` olmalı (409) · **max 20** bağ.

### 4.3 `StrategyTemplateFrequencyIntent` — gömülü, TEKİL ("ne sıklıkta")

> **Bu nesne bir policy DEĞİLDİR ve policy ÜRETMEZ.** MOD-0165 resolve provider'ı onu **okumaz**.

| Alan | Tip | Zorunlu | Kural |
|---|---|---|---|
| `Mode` | string | **Evet** | `policy-reference` \| `declared-intent` \| `none` (§4.6). **Tam olarak bir** şekil geçerlidir |
| `VisitFrequencyPolicyId` | Guid? | koşullu | `Mode=policy-reference` iken **zorunlu**, diğerlerinde **boş**. Policy tenant içinde var olmalı ve `active` olmalı (400) |
| `PolicyCodeDisplay` | string? | server-side | Yalnız görüntü; SoT MOD-0165 |
| `FrequencyType` | string? | koşullu | `Mode=declared-intent` iken zorunlu; **MOD-0165'in `FrequencyType` sabitleriyle** doğrulanır (salt-okunur yeniden kullanım: `weekly` \| `biweekly` \| `monthly` \| `cycle-based` \| `custom`) |
| `RequiredVisitCount` | int? | koşullu | `declared-intent` iken zorunlu, **> 0**, ≤ 365 |
| `PeriodType` | string? | koşullu | `declared-intent` iken zorunlu; MOD-0165 `FrequencyPeriodType` sabitleri (`day` … `custom`) |
| `IntentNote` | string? | Hayır | max 1000 — "neden bu ritim" |

**`policy-reference` ek kuralı (kasıtlı katılık):** referans edilen policy'nin `TargetType`'ı `segment` ise,
`TargetId`'si template'in **bağlı segmentlerinden biri** olmak zorundadır; değilse **400**
`frequency_policy_target_mismatch`. Aksi hâlde playbook kendi kendisiyle çelişir.
`TargetType` başka bir şeyse (`account`, `territory-node`, …) referans **kabul edilir** ve
"bu ritim bu segmentten daha dar/geniş bir hedefe yazılmış" bilgisi `/bindings` cevabında **görünür** taşınır.

### 4.4 `StrategyTemplateProductLine` — gömülü ("ne satılıyor", legacy `SubjectList`'in gerçek rolü)

| Alan | Tip | Zorunlu | Kural |
|---|---|---|---|
| `LineId` | Guid | otomatik | Template içinde unique |
| `GlobalProductId` | Guid | **Evet** | **MDM `GlobalProduct.Id`** — cross-service **fail-closed** doğrulanır (§8.4) |
| `GlobalProductCodeDisplay` | string? | Hayır | Yalnız görüntü; SoT MDM. Ad/kod **kopyası SoT değildir** |
| `LineWeightPercentage` | decimal? | koşullu | Ürün hatları arası ağırlık. **Ya hepsinde dolu ya hiçbirinde** (yarım belirtilmiş ağırlık **yasak**); doluysa toplam **tam 100.00** |
| `SkuAllocationMode` | string | **Evet** | `product-only` (SKU kırılımı yok) \| `sku-allocated` (§4.6) |
| `SkuAllocations` | `List<StrategyTemplateSkuAllocation>` | koşullu | `sku-allocated` iken **≥1**; `product-only` iken **boş olmak zorunda** |
| `SortOrder` | int | **Evet** | Template içinde unique |
| `Notes` | string? | Hayır | max 500 |

**Kurallar:** aynı `GlobalProductId` **iki kez** bağlanamaz (400) · **max 50** hat · **max 50** SKU/hat.

### 4.5 `StrategyTemplateSkuAllocation` — gömülü (SKU % dağılımı)

| Alan | Tip | Zorunlu | Kural |
|---|---|---|---|
| `AllocationId` | Guid | otomatik | Hat içinde unique |
| `GskuId` | Guid | **Evet** | **MDM `Gsku.Id`** — cross-service **fail-closed** doğrulanır (§8.4) |
| `GskuCanonicalCodeDisplay` | string? | Hayır | Yalnız görüntü; SoT MDM |
| `Percentage` | decimal | **Evet** | `0 < p ≤ 100`, **2 ondalık** (`decimal(5,2)`). Otomatik normalize/yuvarlama **YOK** |
| `SortOrder` | int | **Evet** | Hat içinde unique |

**`TotalPercentage` kuralı (bu FU'nun ana sayısal AC'si):**
`SkuAllocationMode = sku-allocated` olan **her** hat için `Σ Percentage` **tam olarak 100.00** olmalıdır.
Sapma → **400** `sku_allocation_total_invalid`, cevapta **hesaplanan toplam ve `lineId` görünür**.
Sessiz normalize, "kalanı sonuncuya ekle", 99.99/100.01 toleransı **YOKTUR**.
Karşılaştırma `decimal` üzerinde yapılır — `double` **kullanılmaz** (kayan nokta toleransı bir tolerans
kararıdır ve bu pack toleransı **reddeder**).

> ⚠️ **D-SKU-LINK (dürüstlük maddesi):** `Gsku` şemasında `GlobalProductId` **yoktur**
> (`Gsku.ProductDefinitionRevisionId` taşır) ve `GET /api/finished-goods/gsku-selector` **ürün filtresi sunmaz**
> (yalnız `Search` + sayfalama). Yeni MDM okuma yüzeyi açmak bu pack'te **yasaktır** (§6). Bu nedenle v1
> *"bu SKU bu ürüne ait mi"* sorusunu **doğrulamaz** ve doğruladığını **iddia etmez**: her iki id **ayrı ayrı**
> var-yok doğrulanır, aidiyet **yazarın sorumluluğundadır** ve UI'da bu **açıkça yazılır**. → **F-SKU-PRODUCT-LINK**.

### 4.5.1 `StrategyTemplateContentBinding` — gömülü ("hangi sunum")

| Alan | Tip | Zorunlu | Kural |
|---|---|---|---|
| `BindingId` | Guid | otomatik | Template içinde unique |
| `ContentRefType` | string | **Evet** | `knowledge-path` \| `content-engagement-journey` (§4.6). Tipsiz id **kabul edilmez** |
| `ContentRefId` | Guid | **Evet** | MOD-0162 `KnowledgePath.Id` veya `ContentEngagementJourney.Id` — **belirli bir sürüm satırı** (pinli) |
| `ContentCodeDisplay` | string? | server-side | `PathCode` / `JourneyCode`; yalnız görüntü |
| `ContentVersionAtBinding` | string? | server-side | `PathVersion` / `JourneyVersion` damgası (iş sürümü, `Version` değil) |
| `SortOrder` | int | **Evet** | Template içinde unique |
| `Notes` | string? | Hayır | max 500 |

**Kurallar:** referans **var olmalı**, **arşivli olmamalı** ve **`published`** olmalı (400
`content_not_published`) — MOD-0162-FU05'in *"pinned published KnowledgePath"* emsali ·
aynı `(ContentRefType, ContentRefId)` iki kez bağlanamaz (400) · **max 50** bağ.

### 4.6 Vokabüler — **D-VOCAB = A (in-domain fail-closed)**

`Domain/Entities/StrategyTemplate.cs` içinde `static class` olarak; set dışı değer → **400**.
MOD-0048 publish'i runtime ön koşulu **değildir** (FU02/FU03/FU04/FU05 + MOD-0164-FU02 emsali → §20/F-RD).

```text
StrategyTemplateStatuses      : draft | active | archived
StrategyTemplateSubjectTypes  : account | contact                    # FU02 SegmentSubjectTypes ile AYNI küme (yeniden beyan, yeni anlam DEĞİL)
StrategySegmentBindingRoles   : primary | secondary | exclusion-note # YALNIZ etiket — küme cebiri YOK
StrategyFrequencyIntentModes  : policy-reference | declared-intent | none
StrategySkuAllocationModes    : product-only | sku-allocated
StrategyContentRefTypes       : knowledge-path | content-engagement-journey
StrategyTemplateReasonCodes   : segment_reference_not_found | segment_archived | segment_subject_type_mismatch |
                                segment_not_active | frequency_policy_not_found | frequency_policy_not_active |
                                frequency_policy_target_mismatch | frequency_intent_shape_invalid |
                                content_reference_not_found | content_not_published | content_archived |
                                product_reference_not_found | sku_reference_not_found |
                                sku_allocation_total_invalid | line_weight_partially_specified |
                                strategy_reference_fanout_exceeded | strategy_dependency_unavailable | bindings_frozen

# MOD-0165'ten SALT-OKUNUR yeniden kullanılan sabitler (yeniden TANIMLANMAZ, kopyalanmaz):
#   FrequencyType.*        (weekly | biweekly | monthly | cycle-based | custom)
#   FrequencyPeriodType.*  (day | week | month | quarter | cycle | campaign-period | custom)
```

### 4.7 Persistence kararı — **1 collection**

| Collection | İçerik | Gerekçe |
|---|---|---|
| `strategy_templates` | `StrategyTemplate` + 4 gömülü bağ listesi | Bağlar **sınırlıdır** (20 segment + 50 hat × 50 SKU + 50 içerik) ve template ile **aynı ömrü, aynı concurrency token'ı** paylaşır → embedded (MOD-0162-FU04/D2 + FU05/S2 + MOD-0167-FU02/§4.6 emsali). Her yazma **tek doküman** → transaction / `SupportsTransactionsAsync` guard'ı / compensation **gerekmez** (`crm-standalone-mongo-transaction-fallback` riski doğmaz) |

**Index'ler** (`Persistence/DependencyInjection.cs`, additive):
- `(TenantId, TemplateCode)` — **unique DEĞİL**; kod tekliği **handler'da** `ListByCodeAsync` ile karara bağlanır
  (`mongo-partial-index-ne-crash`: partial index filtresinde `$ne` servisi crash-loop'a sokar — FU02'nin
  `ISegmentRepository.ListByCodeAsync` çözümü **birebir** tekrarlanır)
- `(TenantId, TemplateStatus)` · `(TenantId, VersionLineageId, TemplateVersion)` · `(TenantId, SubjectType)`
- `SegmentBindings.SegmentId` (multikey — "bu segment hangi playbook'larda geçiyor?" ters sorusu için)
- **Kritik:** `crm-datetimeoffset-array-pitfalls` — `EffectiveFrom` + `EffectiveTo` **aynı index'e konmaz ve
  birlikte sort edilmez** (parallel-arrays 500).
- **Class-map:** `StrategyTemplate` **ve 5 gömülü tipin hepsi** `RegisterClassMaps`'e eklenir — aksi hâlde Guid
  FK'lar binary yazılır, filtreler string serialize eder ve sorgular **sessizce boş döner**
  (`crm-new-aggregate-classmap-guid`).

---

## 5. Repo Scope

```text
# --- backend ---
services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/StrategyTemplate.cs                                  (yeni; aggregate + 5 gömülü tip + vokabüler + reason-code static class'ları)
services/Diten.CrmService/src/Diten.CrmService.Domain/Repositories/IStrategyTemplateRepository.cs                   (yeni)
services/Diten.CrmService/src/Diten.CrmService.Application/Features/StrategyTemplate/**                             (yeni — §10)
services/Diten.CrmService/src/Diten.CrmService.Persistence/Repositories/StrategyTemplateRepository.cs               (yeni)
services/Diten.CrmService/src/Diten.CrmService.Persistence/DependencyInjection.cs                                   (RegisterClassMaps + index + DI — additive)
services/Diten.CrmService/src/Diten.CrmService.Infrastructure/StrategyTemplate/MdmStrategyTemplateReferenceValidator.cs  (yeni — cross-service fail-closed; MdmSegmentProductReferenceValidator deseni BİREBİR)
services/Diten.CrmService/src/Diten.CrmService.Infrastructure/DependencyInjection.cs                                (HttpClient + DI — additive)
services/Diten.CrmService/src/Diten.CrmService.Api/Controllers/CRM/StrategyTemplatesController.cs                   (yeni)
services/Diten.CrmService/src/Diten.CrmService.Api/Controllers/CRM/StrategyTemplateContractController.cs            (yeni)
services/Diten.CrmService/src/Diten.CrmService.Api/Models/CRM/StrategyTemplateRequests.cs                           (yeni)
services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/StrategyTemplate/StrategyTemplateAggregateTests.cs         (yeni)
services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/StrategyTemplate/StrategyTemplateBindingValidationTests.cs (yeni)
services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/StrategyTemplate/StrategyTemplateAllocationRulesTests.cs   (yeni — TotalPercentage)
services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/StrategyTemplate/StrategyTemplateLifecycleTests.cs         (yeni — activate/freeze/new-version)
services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/StrategyTemplate/StrategyTemplateFailClosedTests.cs        (yeni — MDM 404 vs unavailable, persist YOK)
services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/StrategyTemplate/StrategyTemplateNoWriteGuardTests.cs      (yeni — policy/target/segment/content YAZILMADIĞININ kanıtı)

# --- frontend: TEK proxy controller + viewmodel ---
frontend/Diten.Web/Controllers/CRM/StrategyTemplatesController.cs                                                   (yeni, proxy-only)
frontend/Diten.Web/Models/CRM/StrategyTemplateViewModels.cs                                                         (yeni)

# --- frontend: Views/CRM/StrategyTemplates/ — DEV-0001 Compact kanonik 9 dosya (§11.2) ---
frontend/Diten.Web/Views/CRM/StrategyTemplates/Index.cshtml                                                         (Layout="_LayoutTenantShell" AÇIKÇA)
frontend/Diten.Web/Views/CRM/StrategyTemplates/Create.cshtml
frontend/Diten.Web/Views/CRM/StrategyTemplates/Edit.cshtml
frontend/Diten.Web/Views/CRM/StrategyTemplates/Details.cshtml                                                       (salt-okunur 4 bağ bloğu + staleness ipuçları)
frontend/Diten.Web/Views/CRM/StrategyTemplates/_Form.cshtml                                                         (template formu + 4 GÖMÜLÜ repeater)
frontend/Diten.Web/Views/CRM/StrategyTemplates/_Filter.cshtml
frontend/Diten.Web/Views/CRM/StrategyTemplates/_DataTable.cshtml                                                    (data-dt-standard="v2" + skeleton; TEK DataTable)
frontend/Diten.Web/Views/CRM/StrategyTemplates/_IndexL10n.cshtml
frontend/Diten.Web/Views/CRM/StrategyTemplates/StrategyTemplatesIndex.cs                                            (marker class)

# --- frontend: JS + RESX + nav ---
frontend/Diten.Web/wwwroot/assets/js/CRM/StrategyTemplates/{index.js, index.l10n.js, form.js}                       (form.js: 4 repeater + canlı TotalPercentage göstergesi + Select2 picker'lar)
frontend/Diten.Web/Resources/Views/CRM/StrategyTemplates/StrategyTemplatesIndex.{ar,en,es,fr,ru,tr,zh}.resx          (7 dil)
frontend/Diten.Web/Resources/SharedResource.{ar,en,es,fr,ru,tr,zh}.resx                                             (StrategyTemplatesMenu anahtarı ×7)
frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml                                                           (TEK <li>, dar istisna — §6)

# --- doğrulama ---
scripts/smoke-mod0167-fu04-strategy-template-authenticated.ps1                                                      (yeni; FU02 script'i şablon)
docs/audits/mod-0167-fu04-strategy-template-*.md                                                                    (evidence)
```

> **Repo scope'a HİÇ girmeyenler:** `SubjectList*` / `Ucln*` / `MicroTarget*` / `CyclePeriod*` dosyaları ·
> `VisitFrequencyPolicy*` yazan hiçbir dosya · `CampaignTarget*` · `Features/Segmentation/**` ·
> `Features/Knowledge/**` · `Views/CRM/Segments/**` · MDM'in **hiçbir** dosyası · ikinci golden-reference sayfası ·
> `ocelot.json` (§15).

---

## 6. Protected Paths

`.antigravity/**` · `gateway/Diten.ApiGateway/**/ocelot.json` (**bu pack yazmaz** — §15/F-GATEWAY-STRATEGY) ·
`services/Diten.MdmService/**` (**değiştirilmez — yalnız HTTP ile sorgulanır**) · `services/Diten.Platform/**` ·
`services/Diten.AuthService/**` · `services/Diten.HcmService/**` · `services/Diten.EnterpriseStrategyService/**` ·
`services/Diten.DevEnablementService/**` (Golden Reference — okunur, değiştirilmez) ·
**MOD-0167-FU02 yüzeyi (SALT-OKUNUR):** `Features/Segmentation/**` (`ISegmentMembershipReader` **dâhil**),
`Domain/Entities/{Segment,TargetCustomer}.cs`, `Domain/Repositories/{ISegmentRepository,ITargetCustomerRepository}.cs`
(**imza genişletilmez**), `Api/Controllers/CRM/Segment*.cs`, `Views/CRM/Segments/**`,
`wwwroot/assets/js/CRM/Segments/**` ·
**MOD-0165 yüzeyi:** `Features/{Campaign,VisitFrequencyPolicy}/**`, `Domain/Entities/{Campaign,VisitFrequencyPolicy}.cs`,
`Domain/Repositories/IVisitFrequencyPolicyRepository.cs` (**yalnız `GetByIdAsync` çağrılır; imza genişletilmez**),
`Api/Controllers/CRM/{Campaigns,VisitFrequencyPolicies}Controller.cs`, `Views/CRM/Campaigns/**` ·
**MOD-0162 yüzeyi:** `Features/Knowledge/**`, `Domain/Entities/{KnowledgePath,ContentEngagementJourney,KnowledgeContent,Subject,Topic,AudienceProfile,Concept*}.cs`,
`Domain/Repositories/{IKnowledgePathRepository,IContentEngagementJourneyRepository}.cs` (**yalnız `GetByIdAsync`**),
`Api/Controllers/CRM/Knowledge*.cs`, `Api/Controllers/CRM/ContentEngagementJourney*.cs`,
`Views/CRM/{Knowledge,KnowledgePaths,KnowledgeConcepts,ContentEngagementJourneys}/**` ·
**MOD-0164 / MOD-0151 / MOD-0149 / MOD-0150 yüzeyleri** (bu FU onları **hiç** tüketmez) ·
RBAC seed / role template / permission catalog (`crm.strategy-template.*` **kataloğa yazılmaz**) ·
MOD-0048 publish · Mongo hand-edit · `execution/registries/**` (yalnız closeout'ta, kullanıcı onayıyla) ·
`execution/portfolio/**` · **MOD-0167-FU01 / FU02 pack dosyaları** (okunur, değiştirilmez) ·
`frontend/Diten.Web/Views/Shared/_Layout.cshtml` (FROZEN) ·
`frontend/Diten.Web/Controllers/Archive/**` + `frontend/Diten.Web/Views/Archive/**` (FROZEN — legacy CrmV2 buradan **taşınmaz**, §2.4).

**Kasıtlı dokunulan tek istisna (protected DEĞİL — dar kapsam):**
`frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml` — CRM Admin nav'ına **tek `<li>`**
(*Strategy Templates* → `/CRM/StrategyTemplates`, permission-guard'lı) eklenir; mevcut `<li>`'ler ve `active`
yol mantığı **değişmez**.

---

## 7. Dependencies

| Bağımlılık | Yön | Sözleşme / etki |
|---|---|---|
| **MOD-0167-FU02** (SHIPPED) | **hard prerequisite (read-only)** | `Segment` var olmalı; `ISegmentRepository.GetByIdAsync` **olduğu gibi** çağrılır. `resolve`/`is-member` **çağrılmaz** — template üye görmez |
| **MOD-0165-FU01/FU03** | boundary, read-only | `VisitFrequencyPolicy` **okunur** (`GetByIdAsync`), **yazılmaz**. `FrequencyType`/`FrequencyPeriodType` sabitleri salt-okunur yeniden kullanılır |
| **MOD-0162-FU04 / FU05** | in-service, read-only | `KnowledgePath` / `ContentEngagementJourney` `GetByIdAsync` ile doğrulanır; **published + arşivsiz** şartı burada uygulanır. MOD-0162 kodu **değişmez** |
| **MDM / MOD-0290** | **cross-service, FAIL-CLOSED** | `GET api/global-products/{id}` (`mdm.global-products.read`) + `GET api/gskus/{id}` (`mdm.gskus.read`), Gateway üzerinden, **CreateAsync/ReplaceAsync öncesi**. MDM'e **yazılmaz**. Brand yolu **tüketilmez** (D-BRAND) |
| **MOD-0155** (MicroTarget) | **consumer (gelecek)** | `IStrategyTemplateReader`'ı ileride tüketir; **bu FU'da bağlanmaz** (F-APPLY). MOD-0155 kodu **değişmez** |
| **MOD-0048** | D-VOCAB=A | Runtime ön koşulu **değil**; `BusinessUnitId` opak kod olarak doğrulanır |
| **MOD-0018** (RBAC) | yalnız tüketim | seed/grant **YOK**; belgelenmiş fallback §14; F-RBAC en sonda |
| **MOD-0167-FU03 (FU-B)** | **yok** | Ölçek katmanı bu FU'nun **ön koşulu değildir** (§1.2) |
| **DEV-0001** | golden reference | **Tek** yüzey, **tek** klasör (§11); Slim dosya seti **kullanılmaz** |

**Veri ön koşulu (KOD DEĞİL — operatör işi, F-SKU-DATA):** tenant `97c5…`'te en az bir **referanslanabilir**
`GlobalProduct` ve `Gsku` bulunmalıdır ki template canlı test edilebilsin. Yoksa bu bir **hata değildir**:
picker **boş** döner, form ürün boyutu olmadan kaydedilebilir (MOD-0162 / MOD-0048 emsali).
`Gsku` picker'ı lifecycle filtresi uygular (`GskuRepository.ReferenceableFilter`) — bazı SKU'lar görünmeyebilir;
bu **MDM'in kararıdır**, burada değiştirilmez.

---

## 8. Runtime Constraints

- **Servis:** `Diten.CrmService` (port **5061**), **yeni servis yaratılmaz** — bağların üçü (Segment, içerik,
  policy) zaten bu serviste; dördüncüsü (MDM) cross-service'tir.
- **Gateway:** tüm çağrılar `:5000` üzerinden; browser JS **servis portuna gitmez** (same-origin MVC proxy).
- **Soft delete:** `DELETE` ve `PATCH` **yoktur** — kaldırma = archive; archived kayıt update kabul etmez (**409**).
- **Tenant (D-TENANT):** `EntityBase` tenant-owned; `TenantId` **server-side** claim'den; cross-tenant **404 /
  boş liste**. Bağlanan **her** referans **aynı tenant** içinde doğrulanır (başka tenant'ın segmenti → 400).
- **Concurrency:** tek `EntityBase.Version` (root). Gömülü bağ düzenlemeleri **de** bu token'a tabidir.
  Uyuşmazlık **409**, sessiz overwrite **yasak**.
- **Atomiklik:** her yazma **tek doküman** yazımıdır → transaction/compensation **gerekmez**.
  `new-version` *(oku → klonla → tek insert)* iki bağımsız yazımdır ve **yarım template** üretmez;
  klonlanan bağ satırları **yeni `BindingId`/`LineId`/`AllocationId`** alır (MOD-0162-FU04/D5 emsali).
- **Hiçbir üretim yok:** template **hiçbir** koleksiyona (policy, campaign target, micro target, membership)
  satır **yazmaz**; testle sabitlenir (§17.2 — `StrategyTemplateNoWriteGuardTests`).
- **Hiçbir çözümleme yok:** bu FU segment üyeliği **hesaplamaz** ve üye kimliği (PII) **döndürmez**.

### 8.1 API Contract

```text
GET    /api/crm/strategy-templates/contract              → contract flags + vokabüler + reason codes + limitler
GET    /api/crm/strategy-templates                       → liste (?templateStatus&subjectType&businessUnitId&segmentId&includeArchived=true)
POST   /api/crm/strategy-templates                       → create (draft, TemplateVersion=1)
GET    /api/crm/strategy-templates/{id}                  → detay (4 bağ bloğu dâhil)
PUT    /api/crm/strategy-templates/{id}                  → update (bağlar dâhil; BindingsFrozenAt dolu ise bağ alanları 409)
POST   /api/crm/strategy-templates/{id}/activate         → draft → active (+ BindingsFrozenAt damgası)      [SoD: .activate]
POST   /api/crm/strategy-templates/{id}/archive          → draft|active → archived
POST   /api/crm/strategy-templates/{id}/new-version      → active sürümden yeni draft klon (TemplateVersion+1, aynı VersionLineageId)
GET    /api/crm/strategy-templates/{id}/bindings         → SALT-OKUNUR bağ görünümü + staleness ipuçları (§8.5). ÜYE DÖNDÜRMEZ
```

Tümü `Response<T>` envelope + `CustomBaseController` (`response-envelope.md`). `TenantId` **hiçbir payload'da yok**.
**`/resolve`, `/apply`, `/generate`, `/preview-targets` gibi bir endpoint YOKTUR** ve eklenmesi bu pack'in
kapsam ihlalidir.

### 8.2 Contract flags

```text
# açık (bu FU)
supportsStrategyTemplateDefinition        : true
supportsSegmentBinding                    : true
supportsMultiSegmentBinding               : true
supportsFrequencyIntentPolicyReference    : true
supportsFrequencyIntentDeclared           : true
supportsProductSkuMix                     : true
supportsSkuAllocationTotalValidation      : true
supportsContentBindingKnowledgePath       : true
supportsContentBindingEngagementJourney   : true
supportsTemplateVersioning                : true
supportsEffectiveDating                   : true
supportsCrossServiceProductValidation     : true
supportsBindingStalenessHints             : true

# KAPALI (motor/üretim yok — sessiz varsayım yasağı)
supportsStrategyApply                     : false   # → MOD-0155-FU05 (MicroTarget)
supportsMicroTargetGeneration             : false   # → MOD-0155
supportsCyclePeriod                       : false   # → MOD-0165 (yapılmadı)
supportsFrequencyPolicyWrite              : false   # → MOD-0165 (SoR)
supportsCampaignTargetGeneration          : false   # → MOD-0165
supportsSegmentMembershipResolution       : false   # → MOD-0167-FU02 (.resolve)
supportsUcln                              : false   # → MOD-0155 (plan) + MDM (sınıflandırma)
supportsLoyaltyPlanning                   : false   # → MOD-0155
supportsPromoWeekPlanning                 : false   # → MOD-0155
supportsPatientNumberPlanning             : false   # → MOD-0155
supportsSubjectListAggregate              : false   # legacy adı; gerçek rolü ProductLines olarak karşılandı
supportsAudienceAggregate                 : false   # ForWhom → MOD-0162 (zaten var)
supportsBrandBinding                      : false   # D-BRAND — üründe kullanılmıyor
supportsLskuBinding                       : false   # → F-LSKU
supportsProductSkuContainmentValidation   : false   # D-SKU-LINK — DOĞRULANMIYOR, doğrulanıyormuş gibi de yapılmıyor
supportsStrategyEngine                    : false   # skorlama / öneri / en-iyi-plan YOK
```

### 8.3 Bağ semantiği — **"liste bir küme cebiri değildir"**

Çoklu segment bağı bir **enumerasyon**dur, bir **ifade** değil:

1. Bağlar **birleştirilmez** (union), **kesiştirilmez** (intersect), **çıkarılmaz** (minus).
2. `BindingRole` (`primary`/`secondary`/`exclusion-note`) **yalnız etikettir**; hiçbir handler ona göre
   davranış değiştirmez. `exclusion-note` bile **hiçbir şeyi dışlamaz** — yazarın notudur.
3. Tüketici (MOD-0155) bağları okur ve **kendi** birleştirme kuralını uygular; o kural **orada** tanımlanır.
4. Sıralama her okumada **deterministiktir**: `SortOrder ASC, BindingId ASC`
   (`DateTimeOffset` üzerinden sıralama **yasak** — `mongo-datetimeoffset-parallel-arrays-sort`).

> **Neden:** bir küme cebiri eklemek, template'i sessizce bir **segmentation motoruna** çevirir ve
> MOD-0167-FU02'nin `MatchMode`/kriter ağacıyla **ikinci bir üyelik dili** yaratır. Bir soru, bir sahip.

### 8.4 Fail-closed matrisi — **in-service gerekçelenir, cross-process 503'tür**

| Durum | Sınıf | Davranış |
|---|---|---|
| Bağlanan segment tenant'ta **yok** | in-service | **400** `segment_reference_not_found` — kayıt **oluşmaz** |
| Segment **arşivli** | in-service | **400** `segment_archived` |
| Segment `SubjectType` ≠ template `SubjectType` | in-service | **400** `segment_subject_type_mismatch` |
| `activate` anında bağlı segment `active` değil | in-service | **409** `segment_not_active` (hangi segment olduğu **görünür**) |
| Frekans policy'si yok / aktif değil | in-service | **400** `frequency_policy_not_found` / `frequency_policy_not_active` |
| Policy `TargetType=segment` ama `TargetId` bağlı segmentlerden biri değil | in-service | **400** `frequency_policy_target_mismatch` |
| İçerik referansı yok / arşivli / `published` değil | in-service | **400** `content_reference_not_found` / `content_archived` / `content_not_published` |
| **MDM `GlobalProduct` 404** | **cross-service** | **400** `product_reference_not_found` — bağ authorable değil, **persist YOK** |
| **MDM `Gsku` 404** | **cross-service** | **400** `sku_reference_not_found` — **persist YOK** |
| **MDM ulaşılamıyor / timeout / 5xx / auth reddi (401/403) / gövde bozuk** | **cross-service** | **503** `strategy_dependency_unavailable` — **kısmi kayıt YOK, persist YOK** |
| SKU yüzde toplamı ≠ 100.00 | in-service | **400** `sku_allocation_total_invalid` + hesaplanan toplam **cevapta görünür** |
| `LineWeightPercentage` bazı hatlarda dolu bazılarında boş | in-service | **400** `line_weight_partially_specified` |
| `BindingsFrozenAt` dolu iken bağ değişikliği | in-service | **409** `bindings_frozen` |
| Tenant context yok | — | **400** (`ITenantContext` çözülemedi) — varsayılan tenant **kullanılmaz** |

**Cross-service çağrı profili** (`MdmSegmentProductReferenceValidator` deseni **birebir**, shipped kod referans):
**cache YOK** · toplam timeout **3 sn** · **1** transient retry (502/503/504, 75 ms) ·
`Authorization` + `X-Tenant-Id` + `X-Correlation-Id` **forward** · Gateway (`:5000`) üzerinden, servis portuna
**doğrudan gidilmez** · **404 ile "bilmiyorum" AYRI cevaplardır** (404 → 400, bilinmiyor → 503) ·
**doğrulama başarısızsa hiçbir şey persist edilmez** — validator **`InsertAsync`/`ReplaceAsync` ÖNCESİNDE** çağrılır ·
`200 + boş/başarısız envelope` **var olma kanıtı değildir**.

**Toplu doğrulama kuralı (N+1 yasağı):** bir kaydetmede **her benzersiz** `GlobalProductId`/`GskuId` **bir kez**
doğrulanır (istek içi dedup); aynı id iki hatta geçiyorsa iki çağrı yapılmaz. Cache **yoktur** — dedup istek
ömrüyle sınırlıdır. Toplam çağrı tavanı: **100** referans/istek (aşım → **422** `strategy_reference_fanout_exceeded`).

### 8.5 `/bindings` — salt-okunur bağ görünümü + staleness ipuçları

`GET /{id}/bindings` template'in bağlarını **olduğu gibi** döner ve her satıra **türetilmiş** (persist edilmeyen)
bir tazelik ipucu ekler:

```text
segmentBinding   → { segmentId, bound: { lineageId, versionAtBinding }, current: { status, superseded: bool } }
frequencyIntent  → { mode, policyId?, policyStatus?, targetMatchesBoundSegment?: bool }
productLine      → { globalProductId, skuAllocations[], totalPercentage, containmentVerified: false }
contentBinding   → { contentRefType, contentRefId, currentStatus, archived: bool }
```

- İpuçları **uyarıdır, engel değildir**: aktif bir template, bağlı bir içerik sonradan arşivlenirse **geçersiz
  olmaz** — geçmiş açıklanabilir kalmalıdır. UI bunu rozetle gösterir, silmez.
- `containmentVerified: false` **her zaman** false'tur (D-SKU-LINK) — dürüstlük alanı; F-SKU-PRODUCT-LINK
  kapandığında anlam kazanır.
- **Üye/PII yok:** bu endpoint segment **üyesi** döndürmez, üye **sayısı** bile döndürmez.

### 8.6 Tüketim seam'i — `IStrategyTemplateReader` (read-only)

```csharp
public interface IStrategyTemplateReader
{
    // MOD-0155 (MicroTarget) için: "bu playbook'un o an geçerli bağları neler?"
    Task<StrategyTemplateBindingSet?> GetActiveBindingsAsync(
        Guid templateId, DateTimeOffset effectiveAt, CancellationToken ct);

    // "bu segment hangi aktif playbook'larda geçiyor?" (bounded: MaxTemplatesPerSegment = 200)
    Task<IReadOnlyList<StrategyTemplateSummary>> ListBySegmentAsync(
        Guid segmentId, DateTimeOffset effectiveAt, CancellationToken ct);
}
```

- **Motor değildir, rapor eder:** yazmaz, üye çözmez, policy üretmez, MicroTarget üretmez.
- `active` **olmayan** veya effective penceresi dışındaki template için `GetActiveBindingsAsync` → **null**
  (varsayılan playbook **uydurulmaz**).
- Tüketiciler ham collection okuma yetkisine **ihtiyaç duymaz**; seam üzerinden gider.

---

## 9. Layout & Shell Contract

- `shell: tenant` → **tüm** `frontend/Diten.Web/Views/CRM/StrategyTemplates/*.cshtml` dosyalarında
  `Layout = "_LayoutTenantShell";` **AÇIKÇA** yazılır (`_ViewStart.cshtml` varsayılanına güvenilmez).
- View klasörü: `frontend/Diten.Web/Views/CRM/StrategyTemplates/`
- Frontend route: `/CRM/StrategyTemplates` · Create `/CRM/StrategyTemplates/Create` ·
  Edit `/CRM/StrategyTemplates/Edit/{id}` · Details `/CRM/StrategyTemplates/Details/{id}`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` **FROZEN** — kullanılmaz.
- Nav: `_LayoutTenantShell.cshtml` içine **tek** permission-guard'lı `<li>`
  (`@if (Perms.Has("crm.strategy-template.read"))` — dev fallback §14).
- Partial path'leri **absolute**: `~/Views/CRM/StrategyTemplates/_Filter.cshtml` vb.
- Bölüm sırası (Index): ① `_Filter` → ② `_BulkActionBar` (shared VM) → ③ `_DataTable`.

---

## 10. Backend File Convention

Golden Reference **Compact** (DEV-0001) birebir; handler/validator adlarında **Command/Query suffix YOK**.

```text
services/Diten.CrmService/src/Diten.CrmService.Application/Features/StrategyTemplate/
├── Commands/
│   ├── CreateStrategyTemplateCommand.cs            (sealed record, IRequest<Response<Guid>>)
│   ├── UpdateStrategyTemplateCommand.cs            (sealed record, IRequest<Response<NoContent>>)
│   ├── ActivateStrategyTemplateCommand.cs
│   ├── ArchiveStrategyTemplateCommand.cs
│   └── CreateStrategyTemplateVersionCommand.cs
├── Queries/
│   ├── ListStrategyTemplatesQuery.cs
│   ├── GetStrategyTemplateByIdQuery.cs
│   ├── GetStrategyTemplateContractQuery.cs
│   └── GetStrategyTemplateBindingsQuery.cs
├── Handlers/
│   ├── CommandHandlers/                            ← AYRI klasör (zorunlu)
│   │   ├── CreateStrategyTemplateHandler.cs        (sealed class, suffix YOK)
│   │   ├── UpdateStrategyTemplateHandler.cs
│   │   ├── ActivateStrategyTemplateHandler.cs
│   │   ├── ArchiveStrategyTemplateHandler.cs
│   │   ├── CreateStrategyTemplateVersionHandler.cs
│   │   └── StrategyTemplateWriteGuards.cs          (lifecycle/freeze/kod-tekliği ortak guard'ları — FU02 SegmentWriteGuards emsali)
│   └── QueryHandlers/                              ← AYRI klasör (zorunlu)
│       ├── ListStrategyTemplatesHandler.cs
│       ├── GetStrategyTemplateByIdHandler.cs
│       ├── GetStrategyTemplateContractHandler.cs
│       └── GetStrategyTemplateBindingsHandler.cs
├── Validators/
│   ├── CreateStrategyTemplateValidator.cs          (Command suffix YOK)
│   └── UpdateStrategyTemplateValidator.cs
├── Binding/
│   ├── StrategyTemplateBindingValidator.cs         (in-service FK: segment / policy / içerik — hepsi SALT-OKUNUR)
│   ├── StrategyTemplateAllocationRules.cs          (saf fonksiyon: yüzde toplamı + ağırlık bütünlüğü)
│   ├── IStrategyTemplateProductReferenceValidator.cs (cross-service sözleşme; impl Infrastructure'da)
│   ├── IStrategyTemplateReader.cs                  (tüketim seam'i — §8.6)
│   └── StrategyTemplateBindingSet.cs               (seam DTO'ları)
├── Contract/
│   └── StrategyTemplateContract.cs
├── StrategyTemplatePermissions.cs
└── StrategyTemplateModels.cs                       ← TEK dosyada tüm DTO/ViewModel'ler
```

**Yasaklar:** tek dosyada birden fazla `public class`/`record` (`StrategyTemplateModels.cs` hariç) ·
`*CommandHandler.cs` / `*QueryHandler.cs` suffix'i · `CommandHandlers`/`QueryHandlers` ayrımını yapmamak ·
`Requests/Commands/` gibi ekstra alt klasör.

---

## 11. Frontend File Contract

### 11.1 Golden reference kararı — kullanıcı-form alanı türetmesi (GÖSTERİLİR)

Sayılan: create/edit formunda kullanıcının doldurduğu **StrategyTemplate** alanları.
Sayılmayan: `Id`, `TenantId`, `IsDeleted`, `CreatedAt`, `UpdatedAt`, `DeletedAt`, `Version`, audit alanları,
server-side damgalar (`BindingsFrozenAt`, `ActivatedAt`, `ArchivedAt`, `SupersededByTemplateId`,
`VersionLineageId`, `TemplateVersion`, `*Display`, `*AtBinding`), DataTable checkbox/action kolonları.

| # | Alan | # | Alan |
|---|---|---|---|
| 1 | `TemplateCode` | 8 | `Notes` |
| 2 | `TemplateName` | 9 | `SegmentBindings` (gömülü repeater — **tek** alan) |
| 3 | `SubjectType` | 10 | `FrequencyIntent` (gömülü blok — **tek** alan) |
| 4 | `TemplateStatus` | 11 | `ProductLines` (gömülü repeater — **tek** alan) |
| 5 | `BusinessUnitId` | 12 | `SkuAllocations` (hat içi alt-repeater — **tek** alan) |
| 6 | `EffectiveFrom` | 13 | `ContentBindings` (gömülü repeater — **tek** alan) |
| 7 | `EffectiveTo` | | |

**13 > 8 → `golden_reference: compact`.** Gömülü repeater'lar **ayrı yüzey değildir**;
`_CreateEditOffcanvas.cshtml` ve `_DetailsQuickView.cshtml` **YASAKTIR** (Compact kuralı).

### 11.2 Dosya seti — TEK klasör, kanonik Compact 9 dosya

```text
frontend/Diten.Web/Views/CRM/StrategyTemplates/
├── Index.cshtml                     (Layout AÇIKÇA; ① _Filter ② _BulkActionBar ③ _DataTable)
├── Create.cshtml                    (sayfa kabuğu + _Form)
├── Edit.cshtml                      (sayfa kabuğu + _Form)
├── Details.cshtml                   (salt-okunur 4 bağ bloğu + staleness rozetleri)
├── _Form.cshtml                     (template formu + 4 GÖMÜLÜ repeater)
├── _Filter.cshtml                   (inline collapsible; dt-inline-filter-host sınıfı ile)
├── _DataTable.cshtml                (data-dt-standard="v2" + skeleton; TEK DataTable)
├── _IndexL10n.cshtml                (JSON payload bridge)
└── StrategyTemplatesIndex.cs        (marker class)

frontend/Diten.Web/wwwroot/assets/js/CRM/StrategyTemplates/
├── index.js                         (DataTable + filtre + bulk action)
├── index.l10n.js                    (camelCase→PascalCase köprüsü ZORUNLU — l10n-bridge-pascalcase-loader)
└── form.js                          (4 repeater + CANLI TotalPercentage göstergesi + Select2 picker'lar)
```

### 11.3 Picker kararı — **hepsi MEVCUT yüzeylerin pass-through'u; yeni endpoint AÇILMAZ**

| Form alanı | Kaynak (Gateway) | Frontend proxy | Gerekli izin |
|---|---|---|---|
| Segment seçici | `GET /api/crm/segments` (FU02) | `StrategyTemplatesController` pass-through | `crm.segment.read` (fallback: `crm.territory.read`) |
| Frekans policy seçici | `GET /api/crm/visit-frequency-policies` (MOD-0165) | pass-through | `crm.visit-frequency-policy.read` (fallback aynı) |
| **Ürün seçici** | `GET /api/global-products/selector` (MDM) | pass-through — `SegmentsController` / `KnowledgeConceptsController` ile **aynı yüzey** | `mdm.global-products.read` |
| **SKU seçici** | `GET /api/finished-goods/gsku-selector` (MDM) | pass-through | ⚠️ `mdm.finished-goods.create` (§20/F-GSKU-PICKER-PERM) |
| İçerik seçici (path) | `GET /api/crm/knowledge/paths` (MOD-0162-FU04) | pass-through | `crm.knowledge.path.read` (fallback aynı) |
| İçerik seçici (journey) | `GET /api/crm/knowledge/content-engagement-journeys` (MOD-0162-FU05) | pass-through | `crm.knowledge.content-engagement-journey.read` |

- **Hardcoded fallback liste YASAK** (`platform-lookups-reference-data.md`). Yetki yoksa alan **serbest-metin
  GUID'e düşmez** — **devre dışı** kalır ve gerekçe gösterilir (FU02'nin `concept.affinity` picker kuralı birebir).
- ⚠️ **`gsku-selector` bir `create` izniyle korunuyor** (`FinishedGoodsController.cs:33` →
  `[HasPermission("mdm.finished-goods.create")]`). Bu, salt-okunur bir picker için yanlış anahtardır ama
  **MDM'in kararıdır ve burada değiştirilemez** (§6). v1 davranışı: 403 → SKU alanı devre dışı + açıklama.
  Kalıcı çözüm **F-GSKU-PICKER-PERM**.
- **TotalPercentage göstergesi:** `form.js` her hat için toplamı **canlı** hesaplar; 100.00 değilse kaydetmeyi
  **engellemez** (sunucu karar verir) ama **görünür** uyarı gösterir. İstemci **normalize etmez**.

**Verifier:** `python3 .antigravity/scripts/verify_datatable_page.py . --area CRM --module StrategyTemplates --reference compact`
→ mevcut CRM Compact baseline'ı (`--module Segments`) ile **karşılaştırmalı** raporlanır; yeni FAIL **açıklanır
veya kapatılır** (§17.1). Baseline'ın kendisi 0 FAIL olmayabilir; **kendi çalıştırmanla doğrula, rapor edilen
sayıya güvenme**.

---

## 12. Validation Rules

### 12.1 `StrategyTemplate`

| Field | Required | Format/Rule | DB-level | Pre-check |
|---|---|---|---|---|
| `TemplateCode` | Evet | trim, max 60, `^[A-Za-z0-9._-]+$`, tenant içi arşivsiz **unique** | index (unique **değil** — §4.7) | `ListByCodeAsync` (handler) |
| `TemplateName` | Evet | trim, max 200 | — | — |
| `SubjectType` | Evet | `account` \| `contact`; **create sonrası immutable** | — | update'te değişim → 400 |
| `TemplateStatus` | Evet | `draft` \| `active` \| `archived`; geçişler yalnız endpoint'lerle | — | lifecycle guard |
| `TemplateVersion` | Evet | ≥1, server-side; payload'dan **kabul edilmez** | — | — |
| `BusinessUnitId` | Hayır | trim, max 60, boş-olmayan string; MOD-0048 set'i **okunmaz** | — | — |
| `EffectiveFrom` | Evet | geçerli instant | — | — |
| `EffectiveTo` | Hayır | `> EffectiveFrom` | (birlikte index'lenmez) | — |
| `Description` / `Notes` | Hayır | max 2000 | — | — |
| `SegmentBindings` | Evet | **≥1**, max 20 | — | §12.2 |
| `FrequencyIntent` | Evet | tam olarak bir şekil (§12.3) | — | — |
| `ProductLines` | Hayır | max 50 | — | §12.4 |
| `ContentBindings` | Hayır | max 50 | — | §12.5 |

### 12.2 `SegmentBinding`

| Field | Required | Rule |
|---|---|---|
| `SegmentId` | Evet | tenant içinde **var**, **arşivsiz**; `SubjectType` template ile eşleşir; template içinde **tekil**; `activate`'te `active` olmalı |
| `BindingRole` | Hayır | vokabülerde; **davranışsal etkisi YOK** |
| `SortOrder` | Evet | template içinde unique, ≥0 |

### 12.3 `FrequencyIntent`

| Mode | Zorunlu alanlar | Boş olmak zorunda |
|---|---|---|
| `policy-reference` | `VisitFrequencyPolicyId` (var + `active` + target uyumu) | `FrequencyType`, `RequiredVisitCount`, `PeriodType` |
| `declared-intent` | `FrequencyType`, `RequiredVisitCount` (1..365), `PeriodType` | `VisitFrequencyPolicyId` |
| `none` | — | hepsi |

Karışık şekil → **400** `frequency_intent_shape_invalid`. **Hiçbir modda policy yazılmaz.**

### 12.4 `ProductLine` + `SkuAllocation`

| Field | Required | Rule |
|---|---|---|
| `GlobalProductId` | Evet | MDM'de **var** (fail-closed); template içinde **tekil** |
| `SkuAllocationMode` | Evet | `product-only` → `SkuAllocations` **boş**; `sku-allocated` → **≥1** |
| `LineWeightPercentage` | koşullu | ya **tüm** hatlarda dolu (toplam **100.00**) ya **hiçbirinde** |
| `GskuId` | Evet | MDM'de **var** (fail-closed); hat içinde **tekil** |
| `Percentage` | Evet | `decimal(5,2)`, `0 < p ≤ 100`; hat toplamı **tam 100.00** |
| `SortOrder` | Evet | kapsam içinde unique |

### 12.5 `ContentBinding`

| Field | Required | Rule |
|---|---|---|
| `ContentRefType` | Evet | `knowledge-path` \| `content-engagement-journey` |
| `ContentRefId` | Evet | tipine göre tenant içinde **var**, **arşivsiz**, **`published`** |
| — | — | `(ContentRefType, ContentRefId)` template içinde **tekil** |

---

## 13. Failure Path to Verify

- **Duplicate `TemplateCode`** → **409** + UI alan-düzeyi hata + kayıt **oluşmaz** + reload sonrası temiz state.
- **Missing `SegmentBindings`** (boş liste) → **400** + validator mesajı; segmentsiz playbook **kaydedilmez**.
- **Concurrency Conflict** (iki sekme aynı template'i düzenledi) → **409** + UI "veri değişti, yeniden yükleyin";
  sessiz overwrite **YOK**.
- **Unauthorized Actor** (`crm.strategy-template.manage` yok) → **403** + UI aksiyonu disabled/permission-denied.
- **Frozen bindings** (`active` template'te bağ değişikliği) → **409** `bindings_frozen`; `new-version` önerilir.
- **Archived template update** → **409**.
- **Cross-tenant `{id}`** → **404** (varlık sızdırılmaz).
- **MDM 404** (`GlobalProduct`/`Gsku` yok) → **400** + hangi satır olduğu görünür + **hiçbir şey persist edilmez**.
- **MDM down / 403 / timeout** → **503** `strategy_dependency_unavailable` + **kısmi kayıt YOK** (doğrulama
  `InsertAsync` **öncesinde**; test: repo `InsertAsync` **hiç çağrılmaz**).
- **SKU toplamı 99.99** → **400** `sku_allocation_total_invalid` + hesaplanan toplam cevapta; **normalize edilmez**.
- **Segment `SubjectType` uyuşmazlığı** (contact template'e account segmenti) → **400**.
- **İçerik `draft`** → **400** `content_not_published`.
- **Policy `TargetType=segment` ama başka segmenti işaret ediyor** → **400** `frequency_policy_target_mismatch`.
- **`activate`'te bağlı segment `draft`** → **409** `segment_not_active` (hangi segment görünür).
- **Yazma sızıntısı denemesi** (herhangi bir handler'ın policy/target/segment/content yazması) → **test FAIL**
  (§17.2 `StrategyTemplateNoWriteGuardTests`).

---

## 14. Authorization Convention

```text
Policy:     [Authorize]                                        // shell: tenant
Permission: [HasPermission("crm.strategy-template.{action}")]  // PKS-001 lowercase-dotted, ≥3 segment, kebab-case
Actor type: tenant_user  (platform SuperAdmin tüm permission'lardan geçer)
```

| Anahtar | Kapsam |
|---|---|
| `crm.strategy-template.read` | liste / detay / contract / `bindings` |
| `crm.strategy-template.manage` | create / update / archive / new-version |
| `crm.strategy-template.activate` | `activate` — **SoD**: planı yazan ile canlıya alan ayrılabilsin (FU02 `.activate` emsali) |

**`.resolve` benzeri bir anahtar YOKTUR** — çünkü bu FU **üye döndürmez**. Bir template'i okumak, bağlı segmentin
kişilerini görme yetkisi **vermez**; o yetki FU02'nin `crm.segment.resolve` anahtarında kalır.

**Bu pack seed/grant YAPMAZ.** `StrategyTemplatePermissions.cs` **yalnız tanım** dosyasıdır (DB yazımı yok,
rol şablonu yok). RBAC kataloğu `crm.strategy-template.*` taşımadığı için endpoint'ler FU02 / MOD-0165-FU04 /
MOD-0164-FU02 ile **aynı belgelenmiş fallback** üzerinde çalışır: okumalar `crm.territory.read`,
yazmalar `crm.territory.model.manage`. **Fallback hiçbir guard'ı genişletmez** — tenant izolasyonu, lifecycle,
freeze ve fail-closed doğrulamalar aynen çalışır; fallback altında `.activate` **manage'e çöker** (SoD dev'de
uygulanamaz — belgelenmiş boşluk, §20/F-RBAC).

**MDM izinleri (dikkat):** cross-service doğrulama **çağıranın token'ıyla** yapılır. Yazar `mdm.global-products.read`
ve `mdm.gskus.read` taşımıyorsa MDM **403** döner → bu FU için **503** (`Unavailable`) demektir, 400 değil.
Bu, fail-closed'ın doğru davranışıdır ama **operatör aksiyonu gerektirir** → §20/F-MDM-PERM.

---

## 15. Gateway / API Routing Decision

**Karar: Gateway değişikliği GEREKLİ (yalnız CRM tarafı).**

`gateway/Diten.ApiGateway/ocelot.json` bugün `/api/crm/` altında şu ailelere sahiptir: `accounts` · `contacts` ·
`territory-management` · `territory-models` · `resources` · `visit-frequency-policies` · `consents` ·
`preferences` · `campaigns` · `knowledge` · **`segments`** · **`subjects`**.
**`strategy-templates` YOKTUR** → yeni route olmadan endpoint'ler Gateway'de **404 + boş `{}` gövde** döner
(`gateway-404-empty-body-signature`).

Gerekli çiftler (`segments` bloğu birebir şablon; `OPTIONS` **dâhil**):

```text
/api/crm/strategy-templates                 ↔ 5061   (GET, POST, OPTIONS)
/api/crm/strategy-templates/{everything}    ↔ 5061   (GET, POST, PUT, OPTIONS)
```

**MDM tarafı — RAPOR (F-GATEWAY-SKU): ek route GEREKMEZ.** Bu pack `ocelot.json`'ı okudu ve şu çiftlerin
**zaten mevcut** olduğunu doğruladı (2026-08-28):

| Yol | ocelot.json satırı | Metodlar | Kapsadığı çağrı |
|---|---|---|---|
| `/api/global-products` | 235 / 243 | GET, POST, OPTIONS | — |
| `/api/global-products/{everything}` | 252 / 260 | GET, POST, OPTIONS | `/selector` (picker) **ve** `/{id}` (doğrulama) |
| `/api/finished-goods/{everything}` | 322 / 330 | GET, POST, OPTIONS | `/gsku-selector` (SKU picker) |
| `/api/gskus` | 339 / 347 | GET, POST, OPTIONS | — |
| `/api/gskus/{everything}` | 356 / 364 | GET, POST, OPTIONS | `/{id}` (SKU doğrulaması) |

→ **F-GATEWAY-SKU açılmasına gerek yoktur; KAPALI raporlanır.** Kalan risk route değil **izindir**
(→ F-MDM-PERM / F-GSKU-PICKER-PERM).

- `ocelot.json` **protected path**'tir; **bu pack yazmaz** (§6). CRM çifti ayrı bir `integration-agent` task'ıdır
  → **§20/F-GATEWAY-STRATEGY**.
- Frontend (5001) **doğrudan 5061'e gitmez**; `frontend/Diten.Web/Controllers/CRM/StrategyTemplatesController.cs`
  same-origin proxy'dir. Proxy'nin forward'ı **204/205/304/1xx için gövdesiz** dönmelidir
  (`proxy-forward-204-content-length-crash`) — `archive`/`activate` 204 dönebilir.
- **Kabul kapısı:** CRM route eklenmeden authenticated smoke (§17.3) çalıştırılmaz; 404 + `{}` görülürse
  **kod hatası değil, eksik route** olarak teşhis edilir (probe **OPTIONS** ile yapılır).

---

## 16. Acceptance Criteria

**Kimlik & kapsam**
- [ ] `py .antigravity/scripts/verify_module_id.py . --check-id MOD-0167-FU04 --name "Strategy Template - Segment x Product SKU Mix x Content Playbook" --parent MOD-0167` **exit 0**.
- [ ] Repoda `SubjectList`, `Ucln`, `MicroTarget`, `CyclePeriod`, `StrategyApply`, `StrategyGenerate` adında
      **hiçbir** tip/dosya/endpoint **yoktur** (grep ile kanıtlanır).
- [ ] `BrandId` bu FU'nun **hiçbir** entity alanında, DTO'sunda, validator'ında, UI alanında geçmez
      (grep: yeni dosyalarda **0 eşleşme**) — **D-BRAND**.

**Bağlar SALT-REFERANStır (bu FU'nun ana AC'si)**
- [ ] `StrategyTemplate` bir segment, policy, içerik veya MDM kaydı **oluşturmaz/güncellemez**: test
      `StrategyTemplateNoWriteGuardTests` `ISegmentRepository`, `ITargetCustomerRepository`,
      `IVisitFrequencyPolicyRepository`, `IKnowledgePathRepository`, `IContentEngagementJourneyRepository`
      üzerindeki **her** yazma metodunun **hiç çağrılmadığını** (mock verify) kanıtlar.
- [ ] `ISegmentMembershipReader` bu feature'ın **hiçbir** dosyasında inject **edilmez** (grep: 0 eşleşme) —
      template üye görmez.
- [ ] `/api/crm/strategy-templates/**` altında **üye/kişi kimliği döndüren hiçbir alan yoktur**;
      `/bindings` cevabı `subjectId`, `memberCount`, `members[]` **içermez**.
- [ ] `git diff --stat`: `Features/Segmentation/**`, `Features/Knowledge/**`, `Features/VisitFrequencyPolicy/**`,
      `Features/Campaign/**`, `services/Diten.MdmService/**` altında **0 değişiklik**.

**MDM `GlobalProduct` / `Gsku` cross-service FAIL-CLOSED, CreateAsync ÖNCESİ**
- [ ] MDM `GlobalProduct` **404** → **400** `product_reference_not_found`; `Gsku` **404** → **400**
      `sku_reference_not_found`; **her ikisinde de** repo `InsertAsync`/`ReplaceAsync` **hiç çağrılmaz**.
- [ ] MDM **timeout / 5xx / 403 / bozuk gövde** → **503** `strategy_dependency_unavailable`; **kısmi kayıt yok**,
      repo yazma metodu **hiç çağrılmaz**.
- [ ] Doğrulama profili: **cache yok** (aynı id iki farklı istekte iki çağrı), **3 sn** toplam timeout,
      **1** transient retry (502/503/504), `Authorization`/`X-Tenant-Id`/`X-Correlation-Id` **forward**,
      Gateway `:5000` (servis portu `5059` **hiçbir yerde** geçmez — grep).
- [ ] `200 + isSuccessful:false` veya boş `data` **var olma kanıtı sayılmaz** (→ 400).
- [ ] İstek içi dedup: aynı `GskuId` iki hatta geçiyorsa **tek** HTTP çağrısı yapılır; >100 referans → **422**.

**Frequency policy YAZILMAZ**
- [ ] `policy-reference`, `declared-intent` ve `none` modlarının **hiçbirinde** `VisitFrequencyPolicy`
      collection'ına yazma olmaz (mock verify) ve contract `supportsFrequencyPolicyWrite: false` döner.
- [ ] `declared-intent` değerleri MOD-0165'in **mevcut** `FrequencyType`/`FrequencyPeriodType` sabitleriyle
      doğrulanır; MOD-0165 dosyalarında **0 satır** değişiklik (git diff).
- [ ] `policy-reference` + policy `TargetType=segment` + `TargetId` bağlı segmentlerden biri **değil** → **400**.

**Apply / cycle / UCLN ERTELENİR**
- [ ] Contract cevabında `supportsStrategyApply`, `supportsMicroTargetGeneration`, `supportsCyclePeriod`,
      `supportsUcln`, `supportsLoyaltyPlanning`, `supportsPromoWeekPlanning`, `supportsPatientNumberPlanning`,
      `supportsCampaignTargetGeneration`, `supportsSegmentMembershipResolution` **hepsi `false`**.
- [ ] `/apply`, `/generate`, `/resolve`, `/preview-targets` yollarında **404** (route yok, action yok).

**SKU % — TotalPercentage doğrulaması**
- [ ] `sku-allocated` bir hatta toplam **100.00** → başarılı; **99.99** ve **100.01** → **400**
      `sku_allocation_total_invalid` + cevapta **hesaplanan toplam** ve `lineId` görünür.
- [ ] Hesaplama `decimal` ile yapılır; `double`/`float` **kullanılmaz** (grep + test).
- [ ] `product-only` hatta SKU satırı gönderilirse **400**; `sku-allocated` hatta 0 SKU → **400**.
- [ ] `LineWeightPercentage` hatların bir kısmında dolu → **400** `line_weight_partially_specified`;
      hepsinde dolu ama toplam ≠ 100.00 → **400**.
- [ ] Sunucu **hiçbir koşulda** yüzdeleri normalize etmez / kalanı son satıra eklemez (gönderilen değerler
      **aynen** persist edilir).
- [ ] `containmentVerified` alanı `/bindings` cevabında **her zaman `false`** (D-SKU-LINK dürüstlük maddesi).

**Lifecycle / sürüm**
- [ ] `activate` → `BindingsFrozenAt` damgalanır; sonrasında bağ değişikliği **409** `bindings_frozen`
      (kod/ad/notes gibi bağ-dışı alanlar güncellenebilir).
- [ ] `activate` anında bağlı segmentlerden biri `active` değilse **409** ve **damga atılmaz**.
- [ ] `new-version` → yeni `Id`, `TemplateVersion+1`, aynı `VersionLineageId`, **yeni** `BindingId`/`LineId`/
      `AllocationId` değerleri; `activate` sonrası kaynak sürümde `SupersededByTemplateId` dolar.
- [ ] Archived template update/activate → **409**; hiçbir endpoint **hard delete** yapmaz (DELETE action yok).

**Tenant / güvenlik**
- [ ] Başka tenant'ın `{id}`'si → **404**; başka tenant'ın segmenti/içeriği bağlanmaya çalışılırsa → **400**.
- [ ] `TenantId` **hiçbir** request/response payload'ında yer almaz.

**Frontend**
- [ ] Tüm `Views/CRM/StrategyTemplates/*.cshtml` dosyalarında `Layout = "_LayoutTenantShell";` **açıkça** yazılı.
- [ ] `_CreateEditOffcanvas.cshtml` ve `_DetailsQuickView.cshtml` **yoktur** (Compact kuralı).
- [ ] `_DataTable.cshtml` `data-dt-standard="v2"` + skeleton taşır; sayfada **tek** DataTable vardır.
- [ ] 4 repeater (`segment`, `product`, `sku`, `content`) `_Form.cshtml` içinde **gömülüdür**; ayrı sayfa/offcanvas yok.
- [ ] Ürün/SKU/segment/içerik/policy seçicilerinin **hiçbiri** JS'te hardcoded liste kullanmaz; hepsi §11.3
      tablosundaki **mevcut** endpoint'lerden beslenir; yeni MDM/CRM endpoint'i **açılmaz**.
- [ ] Yetki yoksa (`mdm.global-products.read` / `gsku-selector` 403) ilgili alan **devre dışı** olur ve gerekçe
      gösterilir; serbest-metin GUID girişine **düşmez**.
- [ ] Hat başına canlı toplam göstergesi 100.00 değilken **görünür uyarı** verir ama istemci **normalize etmez**.
- [ ] `python3 .antigravity/scripts/verify_datatable_page.py . --area CRM --module StrategyTemplates --reference compact`
      çalıştırılır; sonuç CRM Compact baseline (`--module Segments`) ile **karşılaştırmalı** raporlanır ve
      **yeni** FAIL kalmaz (veya her biri pack'te gerekçelendirilir).
- [ ] 7 dil RESX paritesi (ar/en/es/fr/ru/tr/zh) + `SharedResource` menü anahtarı ×7; eksik anahtar **yok**.
- [ ] `index.l10n.js` camelCase→PascalCase köprüsünü uygular (`l10n-bridge-pascalcase-loader`).

---

## 17. Test Expectations

### 17.1 Build & statik doğrulama
- `dotnet build` — `Diten.CrmService` (+ `frontend/Diten.Web`) **PASS** (fleet lock'ta
  `-p:BaseOutputPath=.tmp-x/` kaçışı kullanılır).
- `verify_module_id.py` (§16) **exit 0**.
- `verify_datatable_page.py --area CRM --module StrategyTemplates --reference compact` — baseline
  karşılaştırmalı; **kendi çalıştırmanı raporla**, başka bir ajanın bildirdiği sayıya güvenme.
- `git diff --stat` ile "0 değişiklik" kanıtları (§16).

### 17.2 Backend unit/integration testleri (`Diten.CrmService.Application.Tests`) — hedef **≥ 45 test**
- **Aggregate/lifecycle** (≥10): create defaults, kod tekliği, immutable `SubjectType`, activate+freeze,
  archive, new-version klonu (yeni alt-id'ler), superseded damgası, archived update 409, concurrency 409.
- **Bağ doğrulaması** (≥12): segment yok/arşivli/tip uyuşmaz/aktif değil, tekrar bağ, policy yok/aktif değil/
  target uyuşmaz, şekil karışıklığı, içerik yok/arşivli/published değil, tekrar içerik bağı.
- **Allocation kuralları** (≥8): 100.00 tam, 99.99, 100.01, tek satır 100, 0/negatif/100'den büyük yüzde,
  `product-only` + SKU satırı, `sku-allocated` + 0 satır, `LineWeightPercentage` kısmi/tam/toplam.
- **Fail-closed** (≥8): MDM 404 (product/gsku) → 400 + **yazma yok**; 5xx/timeout/403/bozuk gövde → 503 +
  **yazma yok**; retry **1** kez; header forward; dedup tek çağrı; fanout tavanı 422.
- **No-write guard** (≥4): segment/target/policy/path/journey repolarının **hiçbir** yazma metodu çağrılmaz;
  `ISegmentMembershipReader` inject **edilmez**.
- **Seam** (≥3): `GetActiveBindingsAsync` aktif olmayan/pencere dışı template için **null**;
  `ListBySegmentAsync` tavanı; deterministik sıralama (`SortOrder ASC, Id ASC`).
- Mevcut suite **regresyonsuz** kalır (FU02 sonrası baseline ile aynı FAIL kümesi — sayı **kendi koşunda** doğrulanır).

### 17.3 Authenticated smoke (Gateway) — `scripts/smoke-mod0167-fu04-strategy-template-authenticated.ps1`
Ön koşullar: **F-GATEWAY-STRATEGY** route'ları eklenmiş · fleet restart · tenant `97c5…` token
(`X-Tenant-Id` başlığı ile — `tenant-scoped-token-multi-tenant-login`) · en az 1 `active` Segment ·
(varsa) 1 published KnowledgePath · (varsa) referanslanabilir GlobalProduct + Gsku (**F-SKU-DATA**).

Kapsam (≥25 assert): contract flags · create draft (segment bağlı) · duplicate kod 409 · segment tip uyuşmazlığı 400 ·
SKU toplamı 99.99 → 400 (mesajda toplam görünür) · SKU toplamı 100.00 → başarı · MDM'de olmayan id → 400 ·
içerik draft → 400 · activate → freeze · frozen bağ update → 409 · new-version → yeni id + version+1 ·
`/bindings` üye alanı **içermiyor** · archive → sonraki update 409 · cross-tenant 404 · `/apply` → 404.

> **PowerShell 5.1 tuzağı:** `@(Where-Object).Count` tek elemanlı sonuçta yanlış sayar — assert sayaçları
> `@(...)` ile sarılır (`mod0164-fu02-consent-preference-runtime` dersi).

### 17.4 Browser smoke
`/CRM/StrategyTemplates` açılır: liste render · filtre · Create → 4 repeater görünür · segment/ürün/SKU/içerik
picker'ları **boş değilse** seçim yapılabilir (boşsa hata değil, **F-SKU-DATA**) · canlı toplam göstergesi ·
Details staleness rozetleri · dil değiştirince RESX anahtarları (undefined **yok**) · console **hatasız**.

---

## 18. Ready-for-dev Checklist

- [ ] Golden Reference **Compact** (DEV-0001 pack + gerçek `GoldenReferenceCompact` kodu) referans olarak okundu
- [ ] Frontmatter tüm zorunlu alanlar dolu (`service`, `shell`, `golden_reference`, `entity_base`, `form_field_count`)
- [ ] Layout & Shell Contract'ta Razor `Layout = "_LayoutTenantShell"` açıkça yazılı (§9)
- [ ] Backend File Convention'da `Handlers/CommandHandlers/` + `Handlers/QueryHandlers/` ayrımı var, suffix yok (§10)
- [ ] Frontend File Contract'ta Compact 9 dosya tam; offcanvas/QuickView **yok** (§11.2)
- [ ] Validation Rules her alan için yazılı (§12)
- [ ] Failure Path ≥4 senaryo (duplicate / missing / unauthorized / concurrency) + fail-closed matrisi (§13)
- [ ] Authorization Convention: 3 anahtar + policy + actor + fallback (§14)
- [ ] Gateway kararı açık: CRM çifti **gerekli** (F-GATEWAY-STRATEGY), MDM tarafı **gerekmiyor** (raporlandı) (§15)
- [ ] Acceptance criteria test edilebilir (§16)
- [ ] Test expectations build/verifier/RESX/smoke kapsıyor (§17)
- [ ] **D-listesi kullanıcı tarafından onaylandı** (D-FU, D-BIND, D-SEG, D-FREQ, D-MIX, D-SKU-LINK, D-LSKU,
      D-CONTENT, D-VER, D-APPLY, D-BRAND, D-VOCAB, D-TENANT, D-RBAC, D-GOLDEN)
- [ ] `status` → `approved`/`ready-for-dev` **ve** `runtime_code_allowed: true` flip'i yapıldı

---

## 19. Implementation Notes

- **Neden ayrı aggregate, neden `Segment`'e alan değil:** bir segment birden çok playbook'ta geçer ve bir
  playbook birden çok segmenti bağlar (N:M). Alan olarak gömmek FU02 aggregate'ini kirletir, sürümleme
  semantiğini çakıştırır ve `Segment` yazma yetkisini strateji yazarlarına açar.
- **Neden pinli `SegmentId`:** FU02'de her sürüm **ayrı bir doküman**dır (`Id` farklı, `VersionLineageId` aynı) ve
  `resolve` **her zaman** çağrılan id'nin kendi sürümüyle çalışır. Lineage'a bağlanmak, playbook'un kimden
  bahsettiğini **sessizce** değiştirirdi — MOD-0162-FU05'in "pinned published" kararıyla aynı gerekçe.
- **Neden `declared-intent` var:** her tenant gün-1'de policy yazmış olmayacak; niyeti kaydedememek yazarı
  policy yazmaya **zorlar** (SoR ihlali) ya da niyeti `Notes`'a serbest metin olarak gömer (makine okunamaz).
  Üçüncü yol: **beyan et, bağlayıcı olduğunu iddia etme.**
- **Neden `TotalPercentage` toleranssız:** legacy `GetSkuAllocationResponse.TotalPercentage` alanı zaten toplamı
  **gösteriyordu**; tolerans eklemek hangi satırın yuvarlandığını kaybettirir. 100.00 kuralı yazarın
  aritmetiğini görünür kılar.
- **Bilinen dürüstlük boşluğu (D-SKU-LINK):** ürün ↔ SKU aidiyeti v1'de **doğrulanmıyor**. Bunu doğrularmış gibi
  göstermek (ör. picker'ı ürüne göre filtrelenmiş gibi sunmak) sahte güven yaratır; UI aidiyetin **yazarın
  sorumluluğunda** olduğunu açıkça yazar ve `/bindings` `containmentVerified: false` döner.
- **Ölçek:** template başına bağ sayıları küçüktür (≤20/50/50/50) ve okuma tek dokümandır. Bu FU'nun ölçek
  riski **MDM çağrı sayısındadır**, doküman boyutunda değil — bu yüzden istek-içi dedup + fanout tavanı var.
- **Fleet:** `.resx` değişiklikleri **tam restart** gerektirir (`local-fleet-and-resx-rebuild`); endpoint'ler
  route + restart olmadan **404 + `{}`** döner.

---

## 20. Follow-up Items

| # | Follow-up | Sahip |
|---|---|---|
| **F-GATEWAY-STRATEGY** | `ocelot.json`'a `/api/crm/strategy-templates` + `/{everything}` çiftleri (OPTIONS dâhil) — **runtime ön koşulu** | `integration-agent` |
| ~~F-GATEWAY-SKU~~ | ✅ **KAPALI (2026-08-28 raporu, §15):** `global-products` (+`{everything}`), `finished-goods/{everything}`, `gskus` (+`{everything}`) rotaları **zaten mevcut**; ocelot yazımı **gerekmiyor** | — |
| **F-SKU-DATA** | **Operatör (kod değil):** tenant `97c5…`'te referanslanabilir `GlobalProduct` + `Gsku` bulunması. Eksikse **hata değil**, picker **boş** (MOD-0162 / MOD-0048 emsali) | MDM operatörü |
| **F-MDM-PERM** | Strateji yazarı rolüne `mdm.global-products.read` + `mdm.gskus.read` grant'i (`manual-grant-*` marker + re-login). Yoksa MDM 403 → bu FU'da **503** | MOD-0018 / operatör |
| **F-GSKU-PICKER-PERM** | `GET /api/finished-goods/gsku-selector` salt-okunur bir picker olmasına rağmen **`mdm.finished-goods.create`** ile korunuyor (`FinishedGoodsController.cs:33`). Ya bu anahtar CRM yazarına verilir (kötü) ya MDM read-permission'lı bir selector açar (**MDM tarafı**, bu pack'te değil) | MDM / EA |
| **F-SKU-PRODUCT-LINK** | Ürün ↔ SKU **aidiyet** doğrulaması: `Gsku`'da `GlobalProductId` yok (`ProductDefinitionRevisionId` var) ve selector ürün filtresi sunmuyor. Ya MDM `?globalProductId=` filtreli bir selector/read açar ya EA aidiyet zincirini (`GlobalProduct → ProductDefinitionRevision → Gsku`) sözleşmeye bağlar | EA / MDM (MOD-0290) |
| **F-LSKU** | `Lsku` (yerel/market SKU) bağı — template'e "market" boyutu eklenmesi kararıyla birlikte değerlendirilir | commercial-suite / EA |
| **F-APPLY** | **MOD-0155-FU05 (MicroTarget):** template'in bir döneme uygulanması (`apply`/`generate`), `IStrategyTemplateReader` tüketimi. **Bu FU'da değil** | MOD-0155 |
| **F-UCLN** | Legacy UCLN planlaması (promo haftası / sadakat % / hasta sayısı) → MicroTarget per-target alanları; UCLN-sınıflandırması → MDM ürün sınıflandırması | MOD-0155 / MDM |
| **F-CYCLE** | `CyclePeriod` / call-cycle takvimi (MOD-0165'te **yapılmadı**); `declared-intent`'in `cycle-based`/`cycle` değerleri o gelene kadar **beyan** düzeyinde kalır | MOD-0165 |
| **F-CONTENT-ITEM** | Ham `KnowledgeContent` (tek içerik) bağı — v1'de yalnız path/journey bağlanıyor | commercial-suite |
| **F-RBAC** | `crm.strategy-template.*` 3 anahtarın RBAC kataloğuna alınması + tenant Admin rolüne grant + re-login (fallback altında `.activate` SoD'si **uygulanamıyor**) | MOD-0018 / operatör |
| **F-REG** | `execution/registries/module-id-registry.md` + `module-implementation-status.md` satırları | registry / governance owner |
| **F-RD** | MOD-0048'de strateji vokabüler setlerinin publish'i (runtime blocker **değil** — D-VOCAB=A) | MOD-0048 operatör |
| **F-FU02-FLAG** | MOD-0167-FU02'nin `supportsStrategyTemplate: false` flag'inin bu FU ship olunca güncellenmesi (**FU02 tarafında**, bu pack'te değil) | commercial-suite |

---

## Ek D — Karar Gerekçeleri (tam)

### D-FU — FU-C ≡ **MOD-0167-FU04**
DCP-002 gate exit 0 (§başlık). FU02 §1.2 tablosu ve §20/F-STRATEGY zaten `MOD-0167-FU04`'ü *"StrategyTemplate"*
için **adıyla** rezerve etti. Yeni bir MOD uydurmak (DCP-002 ihlali) ve `CAND-CAP` açmak (parent zaten Blueprint
canonical) **reddedildi**.

### D-BIND — Template **bağlar**, üretmez
**Seçenek A (seçilen):** authoring-only bağ demeti + salt-okunur seam.
**Seçenek B (reddedildi):** template "apply" edildiğinde policy/target/plan satırları üretsin. Bu, MOD-0165 ve
MOD-0155 SoR'unu MOD-0167 içine çeker, MOD-0167-FU01/D1–D2'yi ihlal eder ve "kim yazdı?" sorusunu cevapsız bırakır.
**Seçenek C (reddedildi):** template bir "snapshot" tutsun (üye + hedef listesi). Bu, FU02/D3'ün ("üyelik persist
edilmez") tam zıddıdır ve PII'yi ikinci bir yere kopyalar.

### D-SEG — Somut `SegmentId`'ye pin
FU02'de sürümler **ayrı doküman**dır; `resolve` id'nin kendi sürümüyle çalışır ve sessizce en sona kaymaz.
Playbook'un "kim"i de aynı katılıkta olmalıdır. Lineage'a bağlanma **reddedildi** (sessiz kayma); "her okumada
en son aktif sürümü bul" **reddedildi** (aynı gerekçe + belirsiz geçmiş).
Çoklu segmentte **homojen `SubjectType`** şartı: aksi hâlde tek playbook hem hesapları hem kişileri hedefler ve
tüketici hangi boyutta çalıştığını bilemez.

### D-FREQ — Üç mod, tek şekil
`policy-reference` gerçek SoR'a atıftır (en güçlü bağ). `declared-intent` niyeti **makine-okunur** kılar ama
**bağlayıcı değildir** — MOD-0165 resolve provider'ı onu okumaz ve okumayacaktır; bu, flag ile **ilan edilir**.
`none` meşrudur: her playbook ritim içermez. Karışık şekil (hem policy hem beyan) **reddedildi** — hangisinin
kazandığı sorusu bir çakışma çözümleyicisi (yani motor) gerektirirdi.

### D-MIX — İki katmanlı gömülü yapı + toleranssız 100.00
Legacy `SubjectList → SkuAllocation(SubjectListId, SkuId, Percentage)` şeması **birebir** bu şekildeydi; tek fark
Brand'in düşürülmesi (D-BRAND) ve ürünün MDM `GlobalProduct`'a bağlanması. `product-only` modu, SKU kırılımı
olmayan planları **yalan söylemeden** ifade eder (0 satırlık "sku-allocated" hat yerine).

### D-SKU-LINK — Aidiyet doğrulanmıyor (ve doğrulanıyormuş gibi yapılmıyor)
Kanıt: `Gsku.ProductDefinitionRevisionId` var, **`GlobalProductId` yok**; `GetFinishedGoodGskuSelectorQuery`
yalnız `PageNumber`/`PageSize`/`Search` alır. Aidiyeti doğrulamak **yeni bir MDM okuma yüzeyi** gerektirir; MDM
bu pack'te **protected**'tır. Sahte bir "doğrulandı" hissi vermek yerine boşluk **contract flag** + `/bindings`
alanı + UI metni ile **görünür** kılınır → F-SKU-PRODUCT-LINK.

### D-LSKU — v1'de ertelenir
`Lsku` market-scoped'tur (`CreateLskuDraftRequest.MarketCode`). Bağlamak, template'e olmayan bir "market"
boyutu ekler ve "aynı playbook farklı marketlerde farklı SKU" sorusunu açar — bu bir **kapsam genişlemesidir**,
teknik bir eksiklik değil. → F-LSKU.

### D-CONTENT — Path **ve** Journey, tiplenmiş ve published
İkisi de MOD-0162'nin **sunum** birimleridir (FU04 sıra, FU05 çok-ziyaret ilerleyişi) ve ikisi de sürümlenip
publish edilir. Tipsiz tek `contentId` **reddedildi** (çözümlenemez). `published` şartı MOD-0162-FU05'in
"pinned published KnowledgePath" kararının aynısıdır: taslak içerik sahaya vaat edilemez.

### D-VER — FU-A/D-VER birebir
`TemplateVersion` (iş) + `VersionLineageId` + `new-version` klonu + `activate` freeze. `Version` adı **teknik
concurrency** için rezervedir (`entity-base-template.md`). Freeze olmadan aktif bir playbook altından
değiştirilebilir ve saha neye göre çalıştığını bilemez.

### D-APPLY — MOD-0155'e ertelenir
"Apply" bir **üretim** eylemidir: dönem, temsilci, hedef ve sayı üretir — hepsi MicroTarget'ın alanıdır
(legacy'de de öyleydi: `TargetCustomer` + `UCLNListPriorityDetail`). MOD-0167'de yapmak, segmentasyon modülünü
bir saha-planlama motoruna çevirirdi. `supportsStrategyApply: false` bunu **sessiz varsayım** olmaktan çıkarır.

### D-BRAND — Kullanılmıyor
Kullanıcı kararı: Brand sayfası üründe kullanılmıyor. Legacy `SubjectList.GlobalBrandId` bu yüzden **taşınmadı**.
Şemada alan **açılmaz** — sonradan doldurulmayan nullable bir FK, veri modelinde kalıcı bir yalandır.

### D-VOCAB — A (in-domain fail-closed)
FU02/FU03/FU04/FU05 + MOD-0164-FU02 emsali. MOD-0048 publish'ini runtime ön koşulu yapmak, modülü operatör
takvimine bağlar. Setlerin publish'i ayrı iş → F-RD.

### D-TENANT — `EntityBase`, server-side `TenantId`
Playbook tenant'a özeldir; cross-tenant erişim 404. Bağlanan **her** referans aynı tenant içinde doğrulanır —
aksi hâlde bir tenant başka tenant'ın segment id'sinin varlığını **sızdırabilirdi**.

### D-RBAC — Tanım var, seed yok
FU02 emsali birebir. `.activate` SoD için ayrı; `.resolve` **yok** çünkü bu FU üye döndürmez. Fallback altında
`.activate` manage'e çöker — belgelenmiş, kapatılacak boşluk (F-RBAC).

### D-GOLDEN — Compact
13 kullanıcı alanı (§11.1) > 8. Gömülü repeater'lar ayrı yüzey sayılmaz (FU02'nin kriter ağacı emsali);
`_CreateEditOffcanvas.cshtml` / `_DetailsQuickView.cshtml` **yasak**.

---

## Handoff

Module pack `draft` olarak hazır. Lütfen inceleyip gerekli alan/scope düzeltmelerini yapın —
özellikle **D-FREQ** (frekans niyetinin üç modu), **D-SKU-LINK** (ürün ↔ SKU aidiyetinin v1'de
doğrulanamaması) ve **D-CONTENT** (path + journey birlikte) onayınızı bekliyor.
Geliştirme için `status` `approved` veya `ready-for-dev` **ve** `runtime_code_allowed: true` olmalıdır;
sonra `@orchestrator MOD-0167-FU04` çağrılır.

Hazırlık sırasında Golden Reference **Compact** (DEV-0001 + gerçek `GoldenReferenceCompact` kodu) şablon olarak
alındı — sapma yok.
