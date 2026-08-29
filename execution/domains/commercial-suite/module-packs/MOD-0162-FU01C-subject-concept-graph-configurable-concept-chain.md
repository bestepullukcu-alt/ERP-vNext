---
id: MOD-0162-FU01C
name: Subject Concept Graph / Configurable Concept Chain Boundary
parent: MOD-0162
parent_name: Knowledge Base
siblings: MOD-0162-FU01, MOD-0162-FU01A, MOD-0162-FU01B
domain: commercial-suite
service: Diten.CrmService
shell: none
golden_reference: none
entity_base: EntityBase
status: approved
runtime_code_allowed: false
runtime_code_scope: "NONE — bu pack yalnız ConceptType / ConceptNode / ConceptRelationship / ConceptChainTemplate sahipliği ve boundary'sidir. Aggregate, endpoint, graph traversal engine, resolution/recommendation engine, AI personalization, UI ve migration ayrı bir implementation FU authorization'ı gerektirir. Runtime yetkisi bu pack'te DEĞİLDİR; MOD-0162-FU02 yalnız ConceptNodeId'yi format-level referans olarak taşır."
owner: module-pack-author
branch: feature/crm/mod-0162-fu01c-subject-concept-graph
started: 2026-08-02
target: TBD (implementation FU ayrı yetkilendirilir)
form_field_count: 0
dependencies:
  - MOD-0162-FU01 (Subject / Topic / AudienceProfile / KnowledgeContent — hard prerequisite)
  - MOD-0162-FU01A (KnowledgePath — linkage hedefi)
  - MOD-0162-FU01B (EngagementJourney / Stage — linkage hedefi)
  - MOD-0162 (parent — Knowledge Base)
  - MOD-0058 (boundary — Knowledge Graph / Entity Linking; enterprise entity graph SoR'u orada)
  - MOD-0057 (boundary — Semantic Tagging & Taxonomy Management; data-asset etiketleme orada)
  - MOD-0155 (consumer)
  - MOD-0165-FU01 / MOD-0167-FU01 (boundary — campaign/segment/frequency)
  - MOD-0048 (reference data — concept status/relationship type vokabüleri)
  - MOD-0018 (RBAC — yalnız tüketim; seed/grant bu pack'te yok)
---

# MOD-0162-FU01C — Subject Concept Graph / Configurable Concept Chain Boundary

> **✅ BOUNDARY APPROVAL (2026-08-09) — `status: draft → approved`.** Governance review
> [mod-0162-boundary-approval-review-fu01-fu01a-fu01b-fu01c-2026-08-09.md](../../../../docs/audits/mod-0162-boundary-approval-review-fu01-fu01a-fu01b-fu01c-2026-08-09.md)
> ile onaylandı. `runtime_code_allowed` **`false` kalır**; concept-graph runtime MOD-0162-FU02 **kapsamı dışıdır**
> (FU02 yalnız `ConceptNodeId`'yi format-level referans olarak taşır, resolve etmez). §2.1'in MOD-0058/MOD-0057
> sınır kararı **kesin ve kendi kendine yeterli**dir (node hiçbir varlığın SoR'u değil; graph motoru açılmıyor);
> F1 adlandırma uzlaştırması **non-blocking follow-up**tur (gating acceptance criterion değildir).
>
> **BOUNDARY AUTHORIZATION (2026-08-02) — `runtime_code_allowed: false`.**
> Bu pack **kod yazma yetkisi vermez**. Yetkilendirdiği tek şey, *"bir subject içinde hangi kavram tipleri var,
> bunlar birbirine nasıl bağlanır ve beklenen zincir nedir?"* sorusunun **sahibi, modeli ve sınırıdır**.
> Graph traversal engine, concept resolution, recommendation engine, AI personalization, best-next-content,
> visit planning ve digital detailing **açılmamıştır**.
>
> **Neden şimdi:** eski pharma sistemindeki **`Indication → Profile → Need → Benefit`** zinciri yeni mimaride
> **korunmalı ama hardcoded olmamalıdır**. Bu zincir bir **örnektir**, sistemin sabit veri modeli değildir:
> Almanca kursu `LanguageLevel → Skill → Topic → LearningNeed → Exercise`, QMS
> `ProcessArea → SOP → Role → TrainingNeed → ControlPoint`, teknik eğitim
> `Technology → Concept → Prerequisite → PracticeNeed → LabExercise` zincirini ister.
> Zincir **konfigüre edilebilir** olmazsa her yeni subject yeni bir tablo/kolon talebi doğurur.
>
> **DCP-002 kimlik kapısı — PASS (2026-08-02):**
> `py .antigravity/scripts/verify_module_id.py . --check-id MOD-0162-FU01C --name "Subject Concept Graph / Configurable Concept Chain Boundary" --parent MOD-0162`
> → `OK  MOD-0162-FU01C: proven against Blueprint/registry.` (exit 0).
> MOD-0162-FU01 kimlik notu (domain-nötr model; EA yatay capability göçü kararı — FU01 §18/F1) bu pack'i de kapsar.
>
> Otorite sırası: **Blueprint Excel** > Module Pack > [Domain Config](../domain-config.md) > `AGENTS.md` >
> `.antigravity/rules/`.

---

## 1. Module Summary

```text
Subject
  → ConceptType            (bu subject'te hangi kavram tipleri var?)
    → ConceptNode          (o tipin gerçek değeri)
      → ConceptRelationship (node'lar arası yönlü bağlantı)
  → ConceptChainTemplate   (beklenen tip sırası — zincir şablonu)
      ↘ KnowledgeContent / KnowledgePath / EngagementJourneyStage bağlantıları
```

| Yanlış model | Neden yanlış |
|---|---|
| `IndicationProfileNeedBenefit` aggregate'i | Zinciri şemaya çakar; yeni subject her seferinde şema değişikliği ister |
| `DoctorProfileNeedBenefit` | Modeli pharma'ya kilitler; kurs/QMS/teknik eğitim dışarıda kalır |
| Need/Benefit zincirini Brand/Product içine gömmek | Brand/Product master'ı zincirin **zorunlu** parçası yapar; master yokken sistem kurulamaz |
| `KnowledgeContent` içine sabit `NeedBenefit` alanı | Aynı içerik farklı need/benefit bağlamlarında kullanılır; tek alan bunu kaybeder |
| `VisitPlan` içine concept chain hardcoded yazmak | Kavram sahipliğini MOD-0155'e gömer; eğitim tarafında yeniden kullanılamaz |

| Doğru model | Sahip |
|---|---|
| **Subject Concept Graph + Configurable Concept Chain** | Bu FU (MOD-0162-FU01C) |

**Temel kural:** *pharma zinciri bu modelin bir **template**'idir; sistemin **veri modeli** değildir.*

---

## 2. Ownership Decision

| Soru | Sahip |
|---|---|
| Ne anlatılacak / hangi içerik / kime uygun? | MOD-0162-FU01 |
| Tek oturumda hangi sırayla? | MOD-0162-FU01A |
| Çoklu oturumda hangi aşama? | MOD-0162-FU01B |
| **Bu subject'te hangi kavramlar var, nasıl bağlanır, beklenen zincir ne?** | **Bu FU (MOD-0162-FU01C)** |
| **Enterprise entity graph, entity resolution, link audit** | **MOD-0058** (§2.1) |
| Data-asset etiketleme / tag governance | **MOD-0057** (§2.1) |
| Ne sıklıkla / kime hedeflenecek | MOD-0165 / MOD-0167 |
| Plan, execution, gösterim kaydı | MOD-0155 |
| Brand / Product master | Ayrı master (MDM/Product) |

### 2.1 MOD-0058 / MOD-0057 sınırı (kayda geçen karar)

**Bulgu:** Blueprint'te `MOD-0058 Knowledge Graph / Entity Linking`'in SoR'u *KG entities/links, link audit trails*;
soft page'leri *KG Explorer, Entity Resolution Workbench, Link Audits*. `MOD-0057 Semantic Tagging & Taxonomy
Management`'in SoR'u *taxonomies, tags, tagging rules*. "Graph" ve "taxonomy" adları çakışma riski taşır.

| | **Bu FU — Subject Concept Graph** | **MOD-0058 — Knowledge Graph / Entity Linking** |
|---|---|---|
| Node'lar | **İş kavramları** (indication, need, benefit, skill, control point) | **Gerçek dünya varlıkları** (Account, Person, Document, Product…) |
| Kapsam | **Subject-scoped, tenant-authored** | **Enterprise-wide**, veri varlıkları üzerinde |
| Amaç | İçerik/anlatım seçimine **girdi bağlamı** | İlişki zekâsı, entity resolution, izlenebilirlik |
| Motor | **Yok** (traversal/resolution/inference bu FU'da yapılmaz) | Graph/traversal/resolution motoru orada |
| SoR iddiası | Node **hiçbir gerçek varlığın SoR'u değildir**; master'a `ExternalRef` ile bağlanır (§4) | KG entities/links SoR'u (benimsenirse) |

**Kural:** enterprise ölçekli traversal, entity resolution veya inference gerekirse **MOD-0058**'e gidilir;
bu FU asla bir graph motoru açmaz. Data-asset etiketleme **MOD-0057**'de kalır (FU01 §4 ile tutarlı).
Adlandırma uzlaştırması EA follow-up'ı olarak açıldı (F1).

---

## 3. Authorized `ConceptType` Model

Bir subject içinde kullanılacak **node tipi**.

| Alan | Zorunluluk | Not |
|---|---|---|
| `ConceptTypeId` | Zorunlu | |
| `TenantId` | Zorunlu | **JWT claim'inden**; payload'da **asla** |
| `SubjectId` | **Zorunlu** | ConceptType **subject bazlıdır** |
| `ConceptTypeCode` | Zorunlu | Subject içinde unique, **stabil** |
| `ConceptTypeName` · `Description` | Zorunlu / optional | |
| `SortOrder` | Zorunlu | Yönetim/görüntüleme sırası (zincir sırası **değildir** — o §6'dadır) |
| `Status` | Zorunlu | `draft` · `active` · `inactive` · `archived` |
| `CreatedAt` / `CreatedBy` / `UpdatedAt` / `UpdatedBy` | Zorunlu | |

Örnek değerler:

```text
Pharma  : indication · audience-profile · profile-need · need-benefit · atc-code · therapeutic-area · specialty
Almanca : language-level · skill · grammar-topic · vocabulary-set · learning-need · exercise-type
QMS     : process-area · sop · role · training-need · control-point
```

**Kurallar:** tenant-scoped ve subject bazlı · **hardcoded enum olarak gömülmez** (tenant kendi tiplerini kurar) ·
`ConceptTypeCode` **stabil**; rename `ConceptTypeName`/alias ile yapılır, kod bozulmaz · **archived ConceptType
yeni node için kullanılamaz** (mevcut node'lar bağlı kalır) · **hard delete yok** · history korunur.

> **Not:** `audience-profile` bir ConceptType olabilir; bu, MOD-0162-FU01'deki `AudienceProfile` **master
> nesnesinin yerine geçmez**. Concept node ilgili profile kaydına `ExternalRef` ile bağlanır (§4) — profil SoR'u
> FU01'de kalır.

---

## 4. Authorized `ConceptNode` Model

ConceptType'ın **gerçek değeri**.

| Alan | Zorunluluk | Not |
|---|---|---|
| `ConceptNodeId` | Zorunlu | |
| `TenantId` | Zorunlu | JWT claim'inden |
| `SubjectId` · `ConceptTypeId` | Zorunlu | Node, tipinin **subject'i** ile tutarlı olmalı → aksi **400** |
| `ConceptNodeCode` | Zorunlu | `(SubjectId, ConceptTypeId)` içinde unique, **stabil** |
| `ConceptNodeName` · `Description` | Zorunlu / optional | |
| `Status` | Zorunlu | `draft` · `active` · `inactive` · `archived` |
| `EffectiveFrom` · `EffectiveTo` | Zorunlu / optional | `EffectiveTo < EffectiveFrom` → **400** |
| `ExternalRefType` · `ExternalRefId` | **Optional** | Master kaydına açık referans (`brand` / `product` / `document` / `audience-profile` / `reference-data-value` / `other`) — **master SoR olarak kalır**, node kopya tutmaz |
| `MetadataJson` | Optional | Domain'e özgü ek bilgi; **core davranış buna hardcoded bağlanmaz** |
| `CreatedAt` / `CreatedBy` / `UpdatedAt` / `UpdatedBy` | Zorunlu | |

Örnekler:

```text
Pharma  : Indication=Hipertansiyon · AudienceProfile=Kardiyoloji A segment doktor
          ProfileNeed=Klinik kanıt ihtiyacı · NeedBenefit=Güven sağlayan çalışma verisi
Almanca : LanguageLevel=A1 · Skill=Speaking · Topic=Selamlaşma
          LearningNeed=Günlük konuşmaya başlama · ExerciseType=Role-play
```

**Kurallar:** effective window desteklenir · **archived node yeni content/path/journey bağlantısında
kullanılamaz** (mevcut linkler history olarak korunur) · **hard delete yok** ·
`MetadataJson` bir **kaçış kapısıdır, sözleşme değildir**: iş kuralı buradan okunmaz, ihtiyaç kalıcıysa
ConceptType/alan olarak modellenir.

---

## 5. Authorized `ConceptRelationship` Model

Node'lar arasındaki **yönlü** bağlantı.

| Alan | Zorunluluk | Not |
|---|---|---|
| `RelationshipId` | Zorunlu | |
| `TenantId` · `SubjectId` | Zorunlu | |
| `FromConceptNodeId` · `ToConceptNodeId` | Zorunlu | Aynı subject içinde olmalı → aksi **400** (§5.1) |
| `RelationshipType` | Zorunlu | MOD-0048 set'i (`leads-to` / `requires` / `addresses` / `evidences` / `belongs-to` / `custom`) |
| `RelationshipCode` · `RelationshipName` | Zorunlu | Stabil kod + okunur ad |
| `Direction` | Zorunlu | `outbound` (varsayılan) \| `bidirectional` — **görünür** olmalı; ters kenar otomatik türetilmez, `bidirectional` açık beyandır |
| `Priority` | Zorunlu | **Küçük değer önce gelir**; deterministik (§5.1) |
| `Status` | Zorunlu | `draft` · `active` · `inactive` · `archived` |
| `EffectiveFrom` · `EffectiveTo` | Zorunlu / optional | `EffectiveTo < EffectiveFrom` → **400** |
| `CreatedAt` / `CreatedBy` / `UpdatedAt` / `UpdatedBy` | Zorunlu | |

Örnekler:

```text
Pharma  : Hipertansiyon → Kardiyoloji A segment doktor → Klinik kanıt ihtiyacı → Güven sağlayan çalışma verisi
Almanca : A1 → Speaking → Selamlaşma → Role-play egzersizi
```

### 5.1 İlişki kuralları

| Kural | Karar |
|---|---|
| Yön | `Direction` **görünür**; ters kenar sessizce türetilmez |
| Subject | Aynı subject içinde çalışır; **cross-subject ilişki varsayılan olarak yasak** → **400** |
| Cross-subject ihtiyacı | Ayrı, açık bir **bridge** nesnesi olarak **future**'a bırakıldı (F6); sessiz köprü kurulmaz |
| Döngü | **Cycle detection zorunlu** — `active` ilişkilerde döngü oluşturan kayıt **400** |
| Kendine referans | `From == To` → **400** |
| Duplicate | Aynı `(From, To, RelationshipType)` için ikinci **active** kayıt → **409** |
| Öncelik | `Priority` deterministik; eşitlikte **stabil `RelationshipCode` sırası** ile tie-break |
| Sessiz seçim | **Yasak** — birden fazla aday kenar varsa seçim gerekçesi (`Priority` / `code-order`) tüketiciye **görünür** olmalıdır |
| Hard delete | **Yok**; `archived` ilişki yeni resolution'da kullanılmaz, history korunur |

---

## 6. Authorized `ConceptChainTemplate` Model

Bir subject içinde **beklenen zincir tiplerini** tanımlar — node'ları değil, **node tiplerinin sırasını**.

| Alan | Zorunluluk | Not |
|---|---|---|
| `ChainTemplateId` | Zorunlu | |
| `TenantId` · `SubjectId` | Zorunlu | |
| `ChainCode` | Zorunlu | Sürümler arası **stabil** |
| `ChainName` · `Description` | Zorunlu / optional | |
| `OrderedConceptTypes` | **Zorunlu** | Sıralı `ConceptTypeId` listesi; en az **2** tip; hepsi **aynı subject'te** olmalı → aksi **400** |
| `Status` | Zorunlu | `draft` · `review` · `approved` · `published` · `inactive` · `archived` |
| `Version` | Zorunlu | |
| `EffectiveFrom` · `EffectiveTo` | Zorunlu / optional | |
| `CreatedAt` / `CreatedBy` / `UpdatedAt` / `UpdatedBy` | Zorunlu | |

Örnekler:

```text
Pharma   : Indication → AudienceProfile → ProfileNeed → NeedBenefit
Learning : LanguageLevel → Skill → Topic → LearningNeed → Exercise
QMS      : ProcessArea → SOP → Role → TrainingNeed → ControlPoint
```

**Kurallar:** aynı subject içinde **birden fazla** chain template olabilir · template **versioned** ·
yalnız **published + effective** template tüketilebilir · **draft/review template consumer'a aktif öneri gitmez** ·
template **node değil tip sırası** tanımlar · kullanıcı yeni subject için kendi zincirini kurabilir ·
**pharma template varsayılan olabilir ama zorunlu/core model değildir** · aynı ConceptType v1'de bir template
içinde **iki kez geçemez** → **400** (özyinelemeli zincir ayrı bir karardır, F7) ·
published sürümde `OrderedConceptTypes` **dondurulur**, değişiklik yeni sürüm ister ·
aynı `ChainCode` için örtüşen effective window'da iki published sürüm → **409**.

### 6.1 Template ↔ ilişki uyumu (karar)

Template **beklentidir, zorlayıcı şema değildir**. Bir `ConceptRelationship`'in `(fromType → toType)` çifti o
subject'in **hiçbir published template**'inde geçmiyorsa kayıt **reddedilmez**, ancak **non-conforming olarak
işaretlenir** ve diagnostics/raporda **görünür** olur. Sessizce kabul de, sessizce ret de yasaktır.

---

## 7. Content / Path / Journey Linkage Boundary

İzin verilen bağlantılar:

```text
ConceptNode            → KnowledgeContent · KnowledgePath · EngagementJourneyStage
ConceptRelationship    → KnowledgeContent · KnowledgePath
ConceptChainTemplate   → EngagementJourney · (optional) KnowledgePath
```

| Kural | Karar |
|---|---|
| Linkin doğası | **Metadata / selection input**tur; karar değildir |
| Recommendation engine | **Bu FU'da yok** |
| AI personalization | **Bu FU'da yok** |
| Best-next-content | **Bu FU'da yok** |
| Görünürlük | Link varsa response/projection'da **görünür** olmalıdır |
| Veri yokluğu | Link yoksa sistem **default content uydurmaz** (MOD-0151 R11 ile aynı ruh: "veri yok" ≠ "uygun değil") |
| Hedef durumu | Link kurulan içerik/path/journey **archived** ise yeni link kurulamaz; mevcut linkler history olarak kalır |
| Kopyalama | Link **referanstır**; içerik/path/stage alanları concept tarafına kopyalanmaz |

---

## 8. Pharma Compatibility

Eski zincir korunur:

```text
Indication → Profile → Need → Benefit
     ↓          ↓        ↓        ↓
Indication → AudienceProfile → ProfileNeed → NeedBenefit   (ConceptType'lar olarak)
```

| Karar | Sonuç |
|---|---|
| `Profile` | **Generic `AudienceProfile`** kullanılır (FU01 §6 ile aynı karar) |
| `DoctorProfileId` | Yalnız **optional pharma metadata** veya bir **ConceptNode**; birinci sınıf alan değildir |
| `ProfileNeed` / `NeedBenefit` | **Hardcoded field değil**, ConceptType/ConceptNode olarak modellenir |
| Brand/Product | **Zorunlu değil** (§10) |
| `ATCCode` · `TherapeuticArea` · `Specialty` | ConceptNode **veya** FU01 §7 opsiyonel metadata olarak bağlanabilir |
| Pharma dışı subject'ler | **Aynı modelle** desteklenir (§9) |

Pharma zinciri, sistemin `ConceptChainTemplate` kayıtlarından **biridir** — çekirdek şema değildir.

---

## 9. Non-pharma Examples

```text
Learning
  Subject = Almanca
  Chain   = LanguageLevel → Skill → Topic → LearningNeed → Exercise
  Nodes   = A1 → Speaking → Selamlaşma → Günlük konuşmaya başlama → Role-play
  Link    = German A1 Greetings Path (KnowledgePath)

QMS
  Subject = QMS
  Chain   = ProcessArea → SOP → Role → TrainingNeed → ControlPoint
  Nodes   = Document Control → SOP-0001 → Document Controller →
            Controlled copy handling → Effective baseline check
  Link    = SOP-0001 Training Article (KnowledgeContent)

Technical training
  Subject = Backend Engineering
  Chain   = Technology → Concept → Prerequisite → PracticeNeed → LabExercise
  Nodes   = .NET → CQRS → MediatR Basics → Pipeline Behavior Practice → Validation Behavior Lab
  Link    = CQRS Pipeline Lesson Path (KnowledgePath)
```

QMS örneğinde `SOP-0001` node'u, controlled document'a **`ExternalRef` ile** bağlanır — SOP'un SoR'u
**MOD-0029**'dur, concept node kopya tutmaz (§4).

---

## 10. Brand / Product Boundary

- Brand/Product bu graph'ta **optional ConceptNode veya metadata** olarak bağlanabilir.
- **Brand/Product master bu FU'da yapılmaz**; core chain'in **zorunlu parçası değildir**.
- Pharma subject'i için `brand` / `product` bir **ConceptType olabilir** — bu bir tercih, zorunluluk değil.
- Master geldiğinde ConceptNode `ExternalRefType=brand|product` ile master'a bağlanır; **master SoR olarak kalır**.
- **Sistem Brand/Product olmadan da concept chain kurabilmelidir** — bu bir kabul kriteridir (§13).
- **Follow-up:** `Brand/Product Master Boundary Pack Authorization` (F2).

---

## 11. Runtime / Engine Boundary

Bu FU **yalnız model ve boundary** tanımlar. **Yapmaz:** concept resolution engine · graph traversal engine ·
recommendation engine · AI personalization · scoring · best-next-content · automatic journey selection ·
automatic path selection · visit plan üretimi · digital detailing · runtime execution.

İleride consumer'lar bu graph'ı **okuyabilir**; ancak **karar motoru ayrı authorization** gerektirir
(F4 — Digital Detailing / Learning Execution; enterprise traversal gerekirse **MOD-0058**).

---

## 12. Consumer Boundaries

### 12.1 MOD-0155

Tüketebilir: visit objective bağlamını anlamak · doctor/profile need'e uygun content/path seçmek ·
campaign hedefini journey stage ile eşleştirmek · ziyarette hangi need/benefit işlendiğini **evidence** olarak
saklamak.

**Bu FU:** MOD-0155 implementation yapmaz · visit/route plan yapmaz · **content selection engine yapmaz** ·
doctor response capture yapmaz · objection handling execution yapmaz.

### 12.2 MOD-0165 / MOD-0167

Campaign hedefi ileride concept zinciriyle ifade edilebilir (indication + audience profile + need + benefit +
frequency + journey). Ancak bu FU: campaign engine · segmentation engine · frequency runtime · due/overdue ·
**target assignment** yapmaz (MOD-0162-FU01B §8/F6 ile aynı sınır).

### 12.3 MOD-0309

Öğrenme tarafında completion/score/attendance/certificate **MOD-0309**'da kalır; concept graph yalnız
**bağlam** verir.

---

## 13. Governance / Versioning Policy

| Kural | Karar |
|---|---|
| Versiyon/effective window | Dört nesne de (**Type / Node / Relationship / ChainTemplate**) effective window semantiği taşır; `ChainTemplate` ayrıca `Version` taşır |
| Hard delete | **Yok** (hiçbir nesnede) |
| Archived | Yeni linking/resolution için kullanılamaz; **mevcut linkler history olarak korunur** |
| Rename | **Kod değişmez**; `Name`/alias ile yapılır |
| Priority | Deterministik; sessiz/rastgele zincir seçimi **yasak** |
| Cycle detection | **Zorunlu** |
| Cross-subject | Varsayılan **yasak**; açık **bridge** olarak future (F6) |
| Template tüketimi | Yalnız **published + effective**; draft/review consumer'a aktif öneri gitmez |
| Vokabüler | `concept-status` · `concept-relationship-type` · `concept-chain-status` MOD-0048 set'i; hardcoded enum yasak, set yayınlanmadan **fail-closed 400** (F3) |
| Subject silme | Subject archive edilirse bağlı Type/Node/Relationship/Template **archived** kabul edilir; silinmez |

---

## 14. Permission Boundary

Canonical öneriler: `crm.knowledge.concept.read` · `crm.knowledge.concept.manage` ·
`crm.knowledge.concept-template.manage` · `crm.knowledge.concept-link.manage`.

**Bu pack hiçbir permission literal'ini kataloğa eklemez, seed/grant yapmaz.** Katalog hazır değilse
implementation FU'sunda anahtar tanımlanır ama `All` listesine eklenmez + geçici fallback + `-RBAC` follow-up (F5).

---

## 15. Explicit Exclusions

Runtime implementation · **graph traversal engine** · **recommendation engine** · **AI personalization** ·
best-next-content · visit planning · route planning · digital detailing · content usage tracking ·
journey progress engine · stage advancement engine · campaign engine · segmentation engine · frequency engine ·
due/overdue engine · target assignment · Brand/Product master implementation · workflow approval ·
MOD-0023 entegrasyonu · file upload/render/preview · learning completion · patient data ·
Account/Contact mutation · territory mutation · hard delete · Mongo hand-edit · RBAC seed/grant ·
registry write · MOD-0048 publish · `TenantId` payload'da.

---

## 16. Contract Flags (öneri — implementation anlamına gelmez)

```json
{
  "supportsSubjectConceptGraph": true,
  "supportsConfigurableConceptChain": true,
  "supportsConceptType": true,
  "supportsConceptNode": true,
  "supportsConceptRelationship": true,
  "supportsConceptChainTemplate": true
}
```

**Eklenmez:** `supportsRecommendationEngine` · `supportsAiPersonalization` · `supportsVisitPlanning` ·
`supportsRoutePlanning` · `supportsDigitalDetailing` · `supportsWorkflowApproval`.
FU01 / FU01A / FU01B flag setleri ve MOD-0151 canlı contract'ı **değişmez**.

---

## 17. Acceptance Criteria for Pack Approval

- [x] Static `Indication → Profile → Need → Benefit` aggregate'i **reddedildi**; zincir bir **template** olarak korundu.
- [x] `ConceptType` / `ConceptNode` / `ConceptRelationship` / `ConceptChainTemplate` alan sözleşmeleri yazıldı.
- [x] Subject bazlı **configurable** zincir yetkilendirildi; pharma dışı üç örnek (Almanca, QMS, teknik eğitim)
      aynı modelle karşılandı.
- [x] İlişki kuralları (yön görünürlüğü, cross-subject yasağı, **cycle detection**, duplicate 409, deterministik
      priority, sessiz seçim yasağı) yazıldı.
- [x] Template ↔ ilişki uyumu **görünür non-conforming** olarak çözüldü (sessiz kabul/ret yok).
- [x] Content/Path/Journey linkage **selection input** olarak sınırlandı; recommendation/AI/best-next-content
      açılmadı.
- [x] Brand/Product **optional ConceptNode/metadata**; sistem Brand/Product olmadan da zincir kurabiliyor.
- [x] `ExternalRef` ile master'lara bağlanma kuralı yazıldı — **node hiçbir varlığın SoR'u değildir**.
- [x] MOD-0058 / MOD-0057 sınırı yazıldı (enterprise graph & tagging orada kalır).
- [x] Runtime/graph/recommendation/AI/visit-planning scope'u açılmadı; `runtime_code_allowed: false`.
- [x] Reviewer onayı → `status: approved` (2026-08-09); ardından Concept Graph implementation FU'su ayrı yetkilendirilir (FU02 kapsamı dışı; F1 adlandırma non-blocking).

---

## 18. Implementation Notes (implementation FU'suna devir)

- Sıralama: **FU01 içerik → FU01A path → FU01B journey → FU01C concept graph**. Concept graph, linkage hedefleri
  (content/path/stage) olmadan tam implement edilemez; ancak Type/Node/Relationship/Template **bağımsız** olarak
  daha erken çıkabilir.
- Öneri: `ConceptType`, `ConceptNode`, `ConceptRelationship` ve `ConceptChainTemplate` **ayrı aggregate**'ler
  (journey/path'ten farklı olarak burada node'lar bağımsız yaşar ve çok sayıda ilişkiye girer).
- Cycle detection ve "aday kenar seçimi" **read-time** hesaplanır; denormalize edilmiş traversal cache **v1'de
  yoktur** (o bir engine kararıdır → F4/MOD-0058).
- Yeni aggregate'ler `RegisterClassMaps`'e eklenmelidir (Guid FK'lar aksi hâlde binary yazılır ve filtreler
  sessizce boş döner — MOD-0151 FU05 dersi).
- İki `DateTimeOffset` alanı (`EffectiveFrom`/`EffectiveTo`) **birlikte index'lenmez/sort edilmez** (CRM
  parallel-array tuzağı).
- `MetadataJson` şemasız tutulur; **sorgulanabilir** olması gerekiyorsa alan/ConceptType'a terfi ettirilir.

---

## 19. Follow-up Items

| # | Follow-up | Owner | Neden |
|---|---|---|---|
| F1 | **Adlandırma/kapsam uzlaştırması — Subject Concept Graph ↔ MOD-0058 Knowledge Graph, MOD-0057 Taxonomy** | EA / Data & Knowledge Plane + commercial-suite | İki "graph" ve iki "taxonomy" nesnesi karışabilir (§2.1) |
| F2 | **`Brand/Product Master Boundary Pack Authorization`** | EA / MDM + commercial-suite | Brand/Product optional ConceptNode/metadata kaldı (§10) |
| F3 | **MOD-0048 concept reference set publish** (`concept-status` / `concept-relationship-type` / `concept-chain-status`) | MOD-0048 operator | Hardcoded enum yasağı |
| F4 | **`Digital Detailing / Learning Execution Pack Authorization`** — concept resolution, best-next-content, journey progress, runtime branching | commercial-suite / EA | Karar motoru bu FU'da kasıtlı kapalı (§11); FU01A F4 / FU01B F4 ile **aynı** follow-up, kapsamı genişledi |
| F5 | **`MOD-0162-FU01C-RBAC — Concept Graph Permission Catalog Alignment`** | MOD-0018 / commercial-suite | §14 anahtarları katalog + grant gerektirir |
| F6 | **Cross-subject concept bridge** — açık, denetlenebilir köprü nesnesi | commercial-suite | Varsayılan yasak; ihtiyaç doğarsa sessiz köprü yerine açık model (§5.1) |
| F7 | **Özyinelemeli / dallanan chain template** (aynı tipin tekrarı, ağaç şeklinde zincir) | commercial-suite | v1'de tip tekrarı yasak (§6) |
| F8 | **EA kimlik kararı** (MOD-0162-FU01 §18/F1 ile ortak) | EA / registry owner | Yatay capability göçü bu pack'i de kapsar |

---

## 20. Next Recommended Prompt

1. **`Brand/Product Master Boundary Pack Authorization`**
2. **`MOD-0162-FU02 — Knowledge Content & Taxonomy Implementation`** (path/journey/concept implementasyonları
   bunun ardından gelir)
3. **`Digital Detailing / Learning Execution Pack Authorization`** — concept resolution + best-next-content +
   journey progress + runtime branching.
