---
id: MOD-0165-FU02
name: Campaign / Targeting Boundary
parent: MOD-0165
parent_name: Campaign Management
siblings: MOD-0165-FU01, MOD-0167-FU01
domain: commercial-suite
service: Diten.CrmService
shell: none
golden_reference: none
entity_base: EntityBase
status: draft
runtime_code_allowed: false
runtime_code_scope: "NONE — bu pack yalnız Campaign / CampaignTarget sahipliği ve boundary'sidir. Aggregate, CRUD, endpoint, segment resolution, target auto-refresh, recommendation engine, import/export, UI ve migration ayrı bir implementation FU authorization'ı gerektirir."
owner: module-pack-author
branch: feature/crm/mod-0165-fu02-campaign-targeting-boundary
started: 2026-08-02
target: TBD (implementation FU ayrı yetkilendirilir)
form_field_count: 0
dependencies:
  - MOD-0165 (parent — Blueprint SoR: campaigns, campaign versions, campaign results)
  - MOD-0165-FU01 (VisitFrequencyPolicy SoR — campaign-source policy boundary)
  - MOD-0167-FU01 (segment co-author + membership seam)
  - MOD-0164 (Blueprint dependency gate — Consent & Preference Management; consent SoR orada)
  - MOD-0162-FU01 / FU01A / FU01B / FU01C (content · path · journey · concept context)
  - MOD-0290-FU01 (Brand/Product master — referansla tüketim)
  - MOD-0151 (territory coverage / readiness — campaign target üretmez)
  - MOD-0155 (consumer — visit/route planning, execution)
  - MOD-0048 (reference data — campaign type / status / target source vokabülerleri)
  - MOD-0018 (RBAC — yalnız tüketim; seed/grant bu pack'te yok)
---

# MOD-0165-FU02 — Campaign / Targeting Boundary

> **BOUNDARY AUTHORIZATION (2026-08-02) — `runtime_code_allowed: false`.**
> Bu pack **kod yazma yetkisi vermez**. Yetkilendirdiği tek şey şu sorunun sahipliği ve sözleşmesidir:
> *"Hangi hedef kitleye, hangi amaçla, hangi dönem içinde, hangi campaign bağlamı uygulanacak?"*
> Campaign engine, segment resolution, target auto-refresh, frequency runtime, due/overdue, recommendation
> engine, digital detailing, visit/route planning ve execution **açılmamıştır**.
>
> **Neden şimdi:** MOD-0165-FU01 (frequency policy SoR), MOD-0162-FU01/A/B/C (content · path · journey · concept)
> ve MOD-0290-FU01 (Brand/Product master) PASS oldu. Hepsi campaign'e **referansla** bağlanıyor; campaign
> boundary'si yazılmazsa bu referanslar campaign içine **kopyalanır** (duplicate master, hardcoded content
> sequence, düz frequency alanı) ve MOD-0155 başlamadan mimari bozulur.
>
> **DCP-002 kimlik kapısı — PASS (2026-08-02):**
> `py .antigravity/scripts/verify_module_id.py . --check-id MOD-0165-FU02 --name "Campaign / Targeting Boundary" --parent MOD-0165`
> → `OK  MOD-0165-FU02: proven against Blueprint/registry.` (exit 0).
> **FU numaralandırma notu:** MOD-0165-FU01 §20/F6'da rezerve edilen "Visit Frequency Policy Implementation"
> FU'su **MOD-0165-FU03** olarak yeniden etiketlendi; FU02 bu boundary pack'ine ayrıldı (henüz hiçbir registry
> satırı veya runtime literal etkilenmedi).
>
> Otorite sırası: **Blueprint Excel** > Module Pack > [Domain Config](../domain-config.md) > `AGENTS.md` >
> `.antigravity/rules/`.

---

## 1. Ownership Decision

**Blueprint kanıtı:**

| Modül | SoR | Integration contract | Dependency gate |
|---|---|---|---|
| **MOD-0165 Campaign Management** | *campaigns, campaign versions, campaign results (if hosted)* | `CAMPAIGN-BUNDLE` (campaign schema, **audience references**, KPI bindings, audit/export) | **Consent & Preference Mgmt**; Metric & Semantic Registry |
| **MOD-0167 Segmentation / CDP** | *segments, segment versions, **segment usage logs*** | `CDP-BUNDLE` (segment schema, lineage hooks, **consent filters**, data product linkage) | DWH/Lakehouse; Data Product Registry; **Consent & Preference Mgmt** |

`CAMPAIGN-BUNDLE`'ın **"audience references"** ifadesi bu pack'in temel kararını doğrudan destekler:
**campaign target bir referanstır, ikinci bir kitle master'ı değildir.**

| Katman | Sorumluluk |
|---|---|
| **MOD-0165** Campaign Management | Campaign aggregate **SoR** · objective · period · scope · **campaign target list boundary** · Brand/Product/Subject/Concept/Content/Journey **referansları** · `Source=campaign` frequency policy üretme boundary'si |
| **MOD-0167** Segmentation / CDP | Segment tanımı · membership source · segment-based targeting · **segment → campaign target resolution boundary** · `Source=segmentation` frequency authoring |
| **MOD-0155** Visit Planning | Campaign target + frequency + availability + territory coverage + content/journey bilgisini **tüketir**; visit/route/execution kararlarını **verir** |
| **MOD-0151** Territory | Coverage/resource readiness sağlar; **campaign target üretmez**, campaign engine **değildir** |
| **MOD-0162** Knowledge/Content | Content · path · journey · concept **context** sağlar; campaign engine **değildir** |
| **MOD-0290** Brand/Product | Brand/Product master **SoR**; campaign içinde **duplicate master açılmaz** |
| **MOD-0164** Consent & Preference | **Consent SoR** — targeting consent'i sahiplenmez, **filtreler** (§9.1) |

### 1.1 Kapsam sınırı (bu FU parent içinde neyi açmaz)

MOD-0165'in Blueprint SoR'u *campaign results*'ı da içerir. **Bu FU campaign results / KPI ölçümünü açmaz** —
KPI linkage ve sonuç raporlaması MOD-0165'in ayrı bir FU'sudur (F6). Bu FU yalnız **campaign tanımı + hedef
listesi boundary'si**dir.

---

## 2. Campaign Model

| Alan | Zorunluluk | Not |
|---|---|---|
| `CampaignId` | Zorunlu | |
| `TenantId` | Zorunlu | **JWT claim'inden**; payload'da **asla** |
| `CampaignCode` | Zorunlu | Tenant içinde unique, **stabil** |
| `CampaignName` · `Description` | Zorunlu / optional | |
| `CampaignType` | Zorunlu | MOD-0048 set'i (`detailing` · `awareness` · `launch` · `training` · `retention` · `other`) |
| `Objective` | Zorunlu | Kampanyanın amacı (ölçüm/KPI **değil** — §1.1) |
| `BusinessUnit` | Optional | Mevcut platform/commercial BU vokabüleri **referanslanır** |
| `BrandId` · `ProductId` | Optional | **MOD-0290 referansı** (§10) |
| `SubjectId` · `TopicId` | Optional | MOD-0162-FU01 referansı |
| `ConceptChainTemplateId` | Optional | MOD-0162-FU01C referansı (§13) |
| `EngagementJourneyId` | Optional | MOD-0162-FU01B referansı |
| `DefaultKnowledgePathId` · `DefaultContentId` | Optional | MOD-0162-FU01A / FU01 referansı |
| `StartDate` · `EndDate` | Zorunlu / optional | `EndDate < StartDate` → **400** |
| `Status` | Zorunlu | §16 |
| `Source` | Zorunlu | `manual` · `import` · `legacy-import` · `external` · `other` |
| `ExternalReferences[]` | Optional | §17 |
| `CreatedAt` / `CreatedBy` / `UpdatedAt` / `UpdatedBy` | Zorunlu | |

**Kurallar:** `CampaignCode` **stabil**; rename `CampaignName` ile yapılır, kod bozulmaz · **hard delete yok** ·
**archived campaign yeni target/frequency/visit-plan input'u olmaz** ·
**Campaign bir `KnowledgeContent` değildir · bir Brand/Product master değildir · bir `EngagementJourney`
değildir** — hepsine yalnız **referans** verir · campaign içinde içerik sırası, ürün ana verisi veya sıklık kuralı
**gömülmez**.

---

## 3. CampaignTarget Model

| Alan | Zorunluluk | Not |
|---|---|---|
| `CampaignTargetId` | Zorunlu | |
| `TenantId` · `CampaignId` | Zorunlu | |
| `TargetType` | Zorunlu | §4 |
| `TargetId` | Zorunlu | `TargetType`'a göre çözümlenir |
| `TargetSource` | Zorunlu | §4.2 |
| `SegmentId` | Optional | `TargetSource=segment` ise **zorunlu** |
| `AccountId` · `ContactId` · `AccountContactLinkId` | Optional | Denormalize **çözüm anahtarları** (SoR değil, §3.1) |
| `TerritoryNodeId` | Optional | MOD-0151'den **okunur**, kopyalanmaz |
| `BusinessUnit` | Optional | |
| `Priority` | Zorunlu | Küçük değer önce (MOD-0165-FU01 §9 ile aynı yön) |
| `Status` | Zorunlu | §16 |
| `EffectiveFrom` · `EffectiveTo` | Zorunlu / optional | `EffectiveTo < EffectiveFrom` → **400** |
| `ReasonCode` / `SelectionReason` | **Zorunlu** | Neden hedef seçildi — §4.2 (sessiz seçim yasağı) |
| `CreatedAt` / `CreatedBy` / `UpdatedAt` / `UpdatedBy` | Zorunlu | |

**Kurallar:** **hard delete yok** · target history **korunur** · aynı `(CampaignId, TargetType, TargetId)` için
ikinci **active** kayıt → **409** · archived/inactive campaign'e yeni target eklenemez.

### 3.1 Target çözüm anahtarları (karar)

`AccountId` / `ContactId` / `AccountContactLinkId` alanları **kolaylık amaçlı çözüm anahtarlarıdır**, ikinci bir
kitle master'ı değildir: `TargetType=account-contact-link` olduğunda link'in `AccountId`/`ContactId` değerleri
**türetilmiş kopyalardır** ve SoR MOD-0149/MOD-0150'de kalır. Ad/adres/telefon gibi hiçbir master alanı campaign
target'a **kopyalanmaz** (`CAMPAIGN-BUNDLE` = *audience **references***).

---

## 4. TargetType / TargetSource Policy

### 4.1 `TargetType`

| Değer | Anlam | Not |
|---|---|---|
| `account-contact-link` | **Saha ziyareti için en net hedef** — "Dr. Ayşe + Medicana Beylikdüzü" | Lokasyon bağlamı taşır; **tercih edilen** tip |
| `account` | Kurum/eczane/hastane | |
| `contact` | Kişi (lokasyondan bağımsız) | **Lokasyon bağlamı eksiktir**; ziyaret tüketiminde link'e çözülmesi gerekir (§4.3) |
| `segment` | Segment tanımı üzerinden hedef | `SegmentId` zorunlu |
| `territory-node` | Territory kapsamındaki hedefler | |
| `concept-node` | Concept graph düğümü üzerinden hedef (ör. indication) | MOD-0162-FU01C |
| `audience-profile` | Profil üzerinden hedef | MOD-0162-FU01 |

### 4.2 `TargetSource`

```text
manual · segment · import · legacy-import · business-rule · manager-selection · campaign-rule · other
```

| Kural | Karar |
|---|---|
| `segment` | Target'lar **MOD-0167'den** gelir (§9) |
| `manual` | Campaign içinde **author edilebilir** |
| `import` / `legacy-import` | Yalnız **boundary olarak** yazılır; **implementation bu FU'da yok** |
| Gerekçe | `ReasonCode`/`SelectionReason` **zorunlu** ve tüketiciye **görünür** |
| **Sessiz/rastgele target selection** | **YASAK** — her hedefin kaynağı ve gerekçesi izlenebilir olmalı |

### 4.3 `contact` hedefinin ziyaret bağlamı (karar)

`TargetType=contact` **geçerlidir** ama ziyaret tüketiminde **tek başına yeterli değildir**: hangi lokasyonda
ziyaret edileceği `AccountContactLink` ile belirlenir. Bu çözüm **MOD-0155'in sorumluluğudur**; bu FU çözümü
yapmaz, yalnız kuralı yazar: *contact target, link'e çözülemiyorsa **sessizce düşürülmez** — görünür bir eksik
bağlam olarak raporlanır* (MOD-0151 R11 ile aynı ruh).

---

## 5. Static vs Dynamic Target Policy

| Model | Davranış | Karar |
|---|---|---|
| **Static target snapshot** | Campaign başlarken hedef listesi **sabitlenir**; history/audit için uygundur; visit execution geçmişi değişmez | ✅ **MVP kararı** |
| **Dynamic target rule** | Segment/rule **her resolution'da** yeniden değerlendirilir; esnek ama deterministik resolution ister | **Future-ready metadata** |

**Kurallar:**

- MVP **static snapshot**'tır; snapshot **ne zaman, hangi segment sürümünden, kim tarafından** alındığı
  (`SnapshotAt`, `SegmentVersion`, `CreatedBy`) target satırında **görünür** olmalıdır.
- **Dynamic resolution engine bu FU'da yapılmaz**; `TargetRuleRef` gibi alanlar yalnız **beyan**dır.
- Bir campaign dynamic modda işaretlenirse bu **response'ta açıkça görünmelidir** — tüketici "bu liste sabit mi,
  değişken mi" sorusunu **tahmin etmek zorunda kalmamalıdır**.
- **Target auto-refresh yoktur**; snapshot yenilemek **açık bir kullanıcı/işlem eylemidir** ve yeni bir
  snapshot kaydı üretir (eskisi history olarak kalır).

---

## 6. Segment Integration Boundary (MOD-0167)

- `SegmentId` campaign target'a **referans** olabilir.
- Segment membership, **snapshot üretmek için** kullanılabilir (§5).
- `Source=segmentation` **VisitFrequencyPolicy** üretimi MOD-0167-FU01'de yetkilendirildi.
- **Segment membership policy'ye veya target'a kopyalanmaz**; resolution/snapshot anında kullanılır ve
  hangi segment **sürümünden** türetildiği kaydedilir.

**Bu FU yapmaz:** segment engine · CDP runtime · segment membership calculation · dynamic audience resolution ·
target auto-refresh.

> MOD-0167 Blueprint SoR'u **segment usage logs**'u içerir: bir campaign target snapshot'ı bir **segment
> kullanımıdır** ve ileride MOD-0167 tarafında loglanabilir olmalıdır (F7).

---

## 7. Brand/Product Integration Boundary (MOD-0290)

| Kural | Karar |
|---|---|
| Referans | Campaign `BrandId`/`ProductId` **referanslar**; master **MOD-0290**'dadır |
| Duplicate | Campaign içinde Brand/Product **duplicate edilmez** (ad/kod kopyası görüntüleme türevidir, master değişince **stale**) |
| Archived/inactive master | Yeni campaign linking **engellenir veya görünür uyarı** üretir (MOD-0290-FU01 §11) |
| Brand/Product'sız campaign | **Oluşturulabilir** |
| Non-pharma | Subject/Topic/Concept üzerinden çalışır |

```text
Campaign: German A1 Speaking Practice
Subject : Almanca · Topic: A1 / Speaking
BrandId/ProductId: null
```

---

## 8. Knowledge / Content / Journey Integration Boundary (MOD-0162)

Campaign referans verebilir: `KnowledgeContent` · `KnowledgePath` · `EngagementJourney` ·
`EngagementJourneyStage` *(optional)* · `ConceptChainTemplate` · `ConceptNode`.

| Kural | Karar |
|---|---|
| Sahiplik | Campaign content/path/journey'nin **sahibi değildir** |
| Kopyalama | Content **kopyalanmaz** · Path/Journey **kopyalanmaz** |
| Durum | **Published + effective olmayan** content/path/journey **active campaign recommendation'a giremez** |
| Sequence | Campaign içinde "content sequence" **hardcoded yazılmaz** — `KnowledgePath`/`EngagementJourney` referansı kullanılır |
| Görünürlük | Campaign target için **hangi journey/path önerildiği görünür** olmalıdır |
| Engine | **Recommendation engine bu FU'da yok** |

---

## 9. Frequency / Call-Cycle Integration Boundary (MOD-0165-FU01 / MOD-0167-FU01)

Campaign üretebilir veya referanslayabilir:

```text
VisitFrequencyPolicy · Source = campaign
TargetType = campaign-target | account-contact-link | segment | concept-node
```

| Kural | Karar |
|---|---|
| Gömme | Frequency policy campaign içine **düz alan olarak gömülmez** |
| İfade | Campaign target frequency'si **`VisitFrequencyPolicy` üzerinden** ifade edilir |
| Due/overdue | **MOD-0155'e** aittir |
| Last visit history | Bu FU'da **yoktur** |
| Policy yoksa | MOD-0151 FU09A'daki **`unknown`** davranışı korunur; varsayılan sıklık **uydurulmaz** |

### 9.1 Consent boundary (Blueprint dependency gate — kayda geçen bulgu)

Blueprint hem MOD-0165 hem MOD-0167 için **"Consent & Preference Mgmt"**'i dependency gate olarak listeler ve
`CDP-BUNDLE` **consent filters** içerir. Karar:

- **Consent SoR MOD-0164'tedir**; campaign/targeting consent **sahiplenmez**.
- Campaign target üretimi ileride **consent-filtrelenebilir** olmalıdır; filtre uygulanmadıysa bu durum
  **görünür** olmalıdır (sessiz "hepsi uygun" varsayımı yasak).
- MOD-0164 pack'i **henüz yok** (MOD-0150 D7 read-only seam precedent'i geçerli) → **F8 follow-up**.
- Bu FU consent engine, opt-out yönetimi veya suppression list **açmaz**.

---

## 10. Subject Concept Graph Integration Boundary (MOD-0162-FU01C)

```text
Pharma   : Almiba Q1 → Indication=Hipertansiyon → AudienceProfile=Kardiyoloji A segment doktor
           → ProfileNeed=Klinik kanıt ihtiyacı → NeedBenefit=Almiba clinical evidence message
Learning : German A1 Speaking Practice → LanguageLevel=A1 → Skill=Speaking → Topic=Selamlaşma
           → LearningNeed=Günlük konuşmaya başlama → Exercise=Role-play
```

| Kural | Karar |
|---|---|
| Engine | Concept graph runtime engine **bu FU'da yok** |
| Rol | Concept linkleri campaign'e **bağlam** sağlar |
| Otomatik seçim | **Automatic best target/content selection yapılmaz** |
| Veri yokluğu | Concept chain yoksa **default concept uydurulmaz** |

---

## 11. Territory / Readiness Integration Boundary (MOD-0151)

- Campaign target'ın **ziyaret edilebilir** olması için territory coverage gerekir.
- **MOD-0151 campaign target üretmez**; yalnız **readiness ve coverage** sağlar.
- Campaign target ↔ Account/Contact/AccountContactLink eşleşmesinin **ziyaret uygunluğu** MOD-0155
  tüketiminde değerlendirilir.
- **Campaign target territory'ye gömülmez**; `TerritoryNodeId` yalnız okunan bir daraltma anahtarıdır.

---

## 12. MOD-0155 Consumer Boundary

MOD-0155 ileride tüketir: Campaign · CampaignTarget · seçilen/bağlı `VisitFrequencyPolicy` ·
account/contact/link hedefi · availability (MOD-0150) · territory readiness (MOD-0151) · Brand/Product bağlamı ·
Subject/Concept bağlamı · `KnowledgePath` / `EngagementJourney` · last visit history · due/overdue sonucu.

MOD-0155'te (ayrı implementation) kalanlar: visit plan · route plan · daily/weekly schedule · execution ·
content usage · stage progress · objection capture · **due/overdue engine** · route optimization.

**Bu FU bunların hiçbirini yapmaz.**

---

## 13. Lifecycle / Status Policy

**Campaign:** `draft` · `active` · `paused` · `completed` · `cancelled` · `archived`
**CampaignTarget:** `draft` · `active` · `inactive` · `completed` · `excluded` · `archived`

| Kural | Karar |
|---|---|
| MOD-0155 input'u | Yalnız **active + effective** campaign target |
| `paused` | **Yeni visit candidate üretmez**; mevcut history bozulmaz |
| `completed` / `cancelled` | History için okunur; **yeni plan girdisi olmaz** |
| `archived` | Yeni linking/target **üretmez** |
| `excluded` (target) | Bilinçli hariç tutma — **gerekçe (`ReasonCode`) zorunlu**, sessiz düşürme yasak |
| Hard delete | **Yok** |
| History | Mevcut visit/campaign history **korunur** |
| Campaign ↔ target tutarlılığı | Campaign `paused`/`completed`/`cancelled`/`archived` iken target'lar **otomatik değiştirilmez** (sessiz cascade yasak); ancak yeni target eklenemez ve tüketici campaign durumunu **görür** |

Vokabülerler MOD-0048 set'i (`campaign-type` · `campaign-status` · `campaign-target-status` ·
`campaign-target-source`); hardcoded enum yasak, set yayınlanmadan **fail-closed 400** (F4).

---

## 14. External Reference / Legacy Migration Boundary

`ExternalReferences[]`: `SourceSystem` · `ExternalId` · `ExternalCode` · `ExternalName` · `ImportedAt` ·
`IsPrimary` (MOD-0290-FU01 §12 ile **aynı** sözleşme).

Kurallar: legacy kod **canonical olmak zorunda değil**; canonical `CampaignCode` **stabil** · duplicate mapping
**conflict olarak raporlanır** · **silent merge yasak** · **campaign target import/export bu FU'da yapılmaz** ·
migration implementation ayrı follow-up (F9) · hard delete yok.

Legacy kaynak notu: eski Campaign/PromoCampaign/CyclePeriod ve TargetCustomer/UCLN/SubjectList yapıları
[legacy-value-preservation.md](../legacy-value-preservation.md) sat. 27–28'de MOD-0165/MOD-0167'ye bağlanmıştır.

---

## 15. Permission Boundary

Canonical öneriler: `crm.campaign.read` · `crm.campaign.manage` · `crm.campaign.target.read` ·
`crm.campaign.target.manage` · `crm.campaign.publish` (publish `manage`'den **ayrı** — SoD).

**Bu pack hiçbir permission literal'ini kataloğa eklemez, seed/grant yapmaz** → `-RBAC` follow-up (F5).

---

## 16. Explicit Exclusions

Runtime implementation · backend/frontend/Gateway değişikliği · **Campaign CRUD** · **campaign target runtime** ·
segmentation engine · frequency runtime engine · visit planning · route planning · **due/overdue engine** ·
last visit history · digital detailing · content recommendation engine · concept graph runtime ·
AI personalization · **target auto-refresh** · campaign import/export implementation · **campaign results / KPI
ölçümü** · Brand/Product master implementation · KnowledgeContent implementation · consent engine ·
workflow approval · MOD-0023 entegrasyonu · file upload/render/preview · patient data · Account/Contact
mutation · territory mutation · hard delete · Mongo hand-edit · RBAC seed/grant · registry write ·
MOD-0048 publish · `TenantId` payload'da.

---

## 17. Contract Flags (öneri — implementation anlamına gelmez)

```json
{
  "supportsCampaignBoundary": true,
  "supportsCampaignTargets": true,
  "supportsCampaignBrandProductReferences": true,
  "supportsCampaignKnowledgeContentReferences": true,
  "supportsCampaignJourneyReferences": true,
  "supportsCampaignFrequencyPolicyReferences": true,
  "supportsStaticTargetSnapshot": true,
  "supportsFutureDynamicTargetRules": true
}
```

**Eklenmez:** `supportsVisitPlanning` · `supportsRoutePlanning` · `supportsDueOverdueEngine` ·
`supportsDigitalDetailing` · `supportsRecommendationEngine` · `supportsAiPersonalization` ·
`supportsWorkflowApproval`. MOD-0151 / MOD-0162 / MOD-0165-FU01 flag setleri **değişmez**.

---

## 18. Acceptance Criteria for Pack Approval

- [x] Campaign ve CampaignTarget alan sözleşmeleri yazıldı; campaign'in **content/master/journey olmadığı**
      kayda geçti.
- [x] `TargetType` (7 değer) ve `TargetSource` (8 değer) politikaları, **`ReasonCode` zorunluluğu** ve
      **sessiz seçim yasağı** yazıldı.
- [x] **Static snapshot MVP / dynamic rule future** ayrımı ve snapshot provenance (segment sürümü, zaman, kişi)
      kararı yazıldı; **auto-refresh yasaklandı**.
- [x] MOD-0167 segment, MOD-0290 Brand/Product, MOD-0162 content/path/journey/concept, MOD-0165-FU01 frequency,
      MOD-0151 readiness ve MOD-0155 consumer sınırları yazıldı.
- [x] **Consent dependency gate (MOD-0164)** bulgusu kayda geçti ve follow-up açıldı — sessiz "hepsi uygun"
      varsayımı yasaklandı.
- [x] Lifecycle/status politikası (paused → yeni candidate yok, excluded → gerekçe zorunlu, sessiz cascade yasak)
      yazıldı.
- [x] Legacy `ExternalReferences[]` politikası MOD-0290-FU01 ile **aynı sözleşmeye** hizalandı.
- [x] Runtime / campaign engine / visit planning / detailing scope'u açılmadı; `runtime_code_allowed: false`.
- [ ] Reviewer onayı → `status: approved`; ardından implementation FU ayrı yetkilendirilir.

---

## 19. Implementation Notes (implementation FU'suna devir)

- Sıralama önerisi: **MOD-0165-FU03 (frequency policy implementation) → campaign/target implementation**;
  campaign target frequency'si policy aggregate'i olmadan ifade edilemez.
- Öneri: `Campaign` ve `CampaignTarget` **ayrı aggregate**'ler (target sayısı büyür, bağımsız lifecycle taşır).
- Snapshot üretimi **büyük yazma** demektir: batch/all-or-nothing davranışı ve idempotency (aynı snapshot iki kez
  üretilmemeli) implementation FU'sunda netleşmelidir — MOD-0151 FU08 dry-run/apply deseni referans alınabilir.
- Yeni aggregate'ler `RegisterClassMaps`'e eklenmelidir (Guid FK'lar aksi hâlde binary yazılır, filtreler sessizce
  boş döner — MOD-0151 FU05 dersi).
- İki `DateTimeOffset` alanı (`EffectiveFrom`/`EffectiveTo`, `StartDate`/`EndDate`) **birlikte
  index'lenmez/sort edilmez** (CRM parallel-array tuzağı).

---

## 20. Follow-up Items

| # | Follow-up | Owner | Neden |
|---|---|---|---|
| F1 | **`MOD-0165-FU03 — Visit Frequency / Call-Cycle Policy Implementation`** (MOD-0165-FU01'in runtime devamı; eski etiketi FU02 idi) | commercial-suite | Campaign target frequency'si buna bağımlı (§19) |
| F2 | **`MOD-0165-FU04 — Campaign / Targeting Implementation`** (aggregate + CRUD + snapshot + UI + tests) | commercial-suite | Bu pack'in runtime devamı |
| F3 | **`MOD-0167-FU02 — Segment Definition & Membership Resolution`** | commercial-suite | Segment source'lu target snapshot'ın prereq'i |
| F4 | **MOD-0048 campaign reference set publish** (`campaign-type` · `campaign-status` · `campaign-target-status` · `campaign-target-source`) | MOD-0048 operator | Hardcoded enum yasağı |
| F5 | **`MOD-0165-FU02-RBAC — Campaign/Target Permission Catalog Alignment`** | MOD-0018 / commercial-suite | §15 anahtarları katalog + grant gerektirir |
| F6 | **Campaign results / KPI linkage boundary** (Blueprint SoR "campaign results", soft page "KPI Linkage View") | commercial-suite / EA | Bu FU kasıtlı olarak açmadı (§1.1) |
| F7 | **Segment usage logging** — campaign target snapshot'ı bir segment kullanımıdır (MOD-0167 SoR) | commercial-suite / MOD-0167 | Lineage/kanıt zinciri için |
| F8 | **`MOD-0164 Consent & Preference Management` boundary pack** + targeting consent filtresi — ✅ **KAPATILDI 2026-08-02** → [MOD-0164-FU01](MOD-0164-FU01-consent-preference-management-boundary.md); target üzerinde yalnız **evaluation sonucu + provenance** tutulur, filtre uygulanmadıysa `consent_filter_not_applied` görünür | commercial-suite / EA | **Blueprint dependency gate**; pack yoktu (§9.1) |
| F9 | **Legacy campaign/target migration mapping planı** (Campaign/PromoCampaign/CyclePeriod, TargetCustomer/UCLN/SubjectList) | commercial-suite | ExternalReferences politikası yazıldı, taşıma planı ayrı (§14) |
| F10 | **Dynamic target rule authorization** (deterministik resolution + görünürlük şartıyla) | commercial-suite | v1 static snapshot (§5) |

---

## 21. Next Recommended Prompt

1. **`MOD-0165/MOD-0167-FU — Visit Frequency / Call-Cycle Policy Implementation`** (= `MOD-0165-FU03`)
2. **`MOD-0164 Consent & Preference Management Boundary Pack Authorization`** (Blueprint dependency gate)
3. **`MOD-0165-FU04 — Campaign / Targeting Implementation`**
