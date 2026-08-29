---
id: MOD-0162-FU01A
name: KnowledgePath / Content Sequence Boundary
parent: MOD-0162
parent_name: Knowledge Base
sibling: MOD-0162-FU01
domain: commercial-suite
service: Diten.CrmService
shell: none
golden_reference: none
entity_base: EntityBase
status: approved
runtime_code_allowed: false
runtime_code_scope: "NONE — bu pack yalnız KnowledgePath / KnowledgePathStep sahipliği ve sıralama boundary'sidir. Aggregate, endpoint, branch evaluator, recommendation engine, completion, UI ve migration ayrı bir implementation FU authorization'ı gerektirir. Runtime yetkisi bu pack'te DEĞİLDİR; ayrı KnowledgePath implementation FU'su gerektirir."
owner: module-pack-author
branch: feature/crm/mod-0162-fu01a-knowledge-path-content-sequence
started: 2026-08-02
target: TBD (implementation FU ayrı yetkilendirilir)
form_field_count: 0
dependencies:
  - MOD-0162-FU01 (KnowledgeContent / Subject / Topic / AudienceProfile sözleşmesi — hard prerequisite)
  - MOD-0162 (parent — Knowledge Base)
  - MOD-0028 / MOD-0029 (file/doküman SoR — dosya bu pack'te de üretilmez)
  - MOD-0155 (consumer — visit objective / detailing akışı)
  - MOD-0309 (consumer — completion / score / attendance SoR)
  - MOD-0165-FU01 / MOD-0167-FU01 (frequency policy — "ne sıklıkla"; path "hangi sırayla")
  - MOD-0048 (reference data — step type / path status / completion rule vokabüleri)
  - MOD-0018 (RBAC — yalnız tüketim; seed/grant bu pack'te yok)
---

# MOD-0162-FU01A — KnowledgePath / Content Sequence Boundary

> **✅ BOUNDARY APPROVAL (2026-08-09) — `status: draft → approved`.** Governance review
> [mod-0162-boundary-approval-review-fu01-fu01a-fu01b-fu01c-2026-08-09.md](../../../../docs/audits/mod-0162-boundary-approval-review-fu01-fu01a-fu01b-fu01c-2026-08-09.md)
> ile onaylandı. `runtime_code_allowed` **`false` kalır**; KnowledgePath runtime, MOD-0162-FU02 **kapsamı dışıdır**
> ve ayrı bir implementation FU'su gerektirir (FU02 §20/F-A/B/C). İçerik-tekil (FU01) ↔ path-zincir ayrımı ve
> `NextContentId`/`BrandContentFlow`/VisitPlan-gömme yasağı doğrulandı. MOD-0155 açılmadı.
>
> **BOUNDARY AUTHORIZATION (2026-08-02) — `runtime_code_allowed: false`.**
> Bu pack **kod yazma yetkisi vermez**. Yetkilendirdiği tek şey, *"içerikler hangi sırayla anlatılacak /
> öğrenilecek / gösterilecek?"* sorusunun **sahibi, modeli ve sınırıdır**. Aggregate, endpoint, branch evaluator,
> recommendation engine, quiz motoru, completion kaydı, digital detailing, UI ve migration **açılmamıştır**.
>
> **Neden şimdi:** [MOD-0162-FU01](MOD-0162-FU01-knowledge-content-subject-taxonomy.md) (PASS, 2026-08-02)
> **tekil içerik** nesnesini, taksonomiyi ve profil modelini kapattı; ancak *"önce bunu, sonra bunu"* zinciri
> ayrı bir model olarak kapanmadı. Bu boşluk kapanmazsa sıra en kolay ama en yanlış üç yere sızar:
> `KnowledgeContent.NextContentId`, pharma'ya kilitli bir `BrandContentFlow`, ya da MOD-0155 `VisitPlan` içine
> gömülmüş hardcoded content sırası.
>
> **DCP-002 kimlik kapısı — PASS (2026-08-02):**
> `py .antigravity/scripts/verify_module_id.py . --check-id MOD-0162-FU01A --name "KnowledgePath / Content Sequence Boundary" --parent MOD-0162`
> → `OK  MOD-0162-FU01A: proven against Blueprint/registry.` (exit 0).
> MOD-0162-FU01'in **kimlik notu aynen geçerlidir**: yetenek domain-nötrdür, EA yatay capability göçü kararı
> (FU01 §18/F1) bu pack'i de kapsar; göç pack gövdesini değiştirmez.
>
> Otorite sırası: **Blueprint Excel** > Module Pack > [Domain Config](../domain-config.md) > `AGENTS.md` >
> `.antigravity/rules/`.

---

## 1. Module Summary

```text
KnowledgeContent   = tekil içerik
KnowledgePath      = içeriklerin anlatım / öğrenme / sunum zinciri
KnowledgePathStep  = path içindeki tek adım
```

| Yanlış model | Neden yanlış |
|---|---|
| `KnowledgeContent.NextContentId` | Aynı içerik farklı path'lerde farklı sırada geçer; tek "next" bunu kaybeder ve içeriği zincire kilitler |
| `BrandContentFlow` | Akışı pharma'ya kilitler; Almanca dersi / SOP eğitimi / onboarding akışları dışarıda kalır |
| `VisitPlan` içinde hardcoded content sırası | Anlatım akışını MOD-0155'e gömer; aynı akış eğitimde yeniden kullanılamaz ve iki yerde iki farklı sıralama mantığı doğar |

| Doğru model | Sahip |
|---|---|
| **`KnowledgePath` + `KnowledgePathStep`** | Bu FU (MOD-0162-FU01A) |

> **Path tek bir görüşme/oturumun sırasıdır (2026-08-02 kararı).** Birden fazla ziyaret/oturum boyunca ilerleyen
> aşamalar ayrı bir modeldir ve [MOD-0162-FU01B — EngagementJourney / Multi-Visit Content
> Progression](MOD-0162-FU01B-engagement-journey-multi-visit-content-progression.md) ile yetkilendirilmiştir.
> `KnowledgePath` içine `visit-1 / visit-2 / visit-3` mantığı **gömülmez**; journey stage'i path'e **referans**
> verir, adımlarını kopyalamaz.

---

## 2. Ownership Decision

| Soru | Sahip |
|---|---|
| Ne anlatılacak / hangi içerik / hangi subject-topic / hangi versiyon / kime uygun? | **MOD-0162-FU01** |
| **Hangi sırayla anlatılacak / öğrenilecek / gösterilecek?** | **Bu FU (MOD-0162-FU01A)** |
| Ne sıklıkla gidilecek? | MOD-0165 / MOD-0167 |
| Ne zaman müsait? | MOD-0150 |
| Kim sorumlu / coverage current mı? | MOD-0151 |
| Ziyaret planı, rota, detailing, gösterim kaydı | **MOD-0155** |
| Tamamlandı mı / kim tamamladı / ne skor aldı | **MOD-0309** |
| Dosya / binary / controlled document | **MOD-0028 / MOD-0029** |

**Temel mimari kural:** *path bir **şablon**dur, bir **çalıştırma** değildir.* Bir adımın gerçekten gösterildiği,
tamamlandığı veya atlandığı bilgisi bu FU'da **yoktur** — o, execution tarafının (MOD-0155 / MOD-0309) kaydıdır.

---

## 3. Authorized `KnowledgePath` Model

| Alan | Zorunluluk | Not |
|---|---|---|
| `PathId` | Zorunlu | Aggregate kimliği |
| `TenantId` | Zorunlu | **JWT claim'inden**; payload'da **asla** |
| `PathCode` | Zorunlu | Versiyonlar arasında **ortak, stabil** kimlik |
| `PathName` · `Description` | Zorunlu / optional | |
| `SubjectId` | **Zorunlu** | Path her zaman bir anlatım alanına aittir |
| `TopicId` | Optional | Verilirse `SubjectId` ile tutarlı olmalı (§7) |
| `AudienceProfileId` | Optional | Yoksa path **genel** kabul edilir; uydurma profil atanmaz |
| `Objective` | Zorunlu | Path'in amacı (ör. "Almiba endikasyon farkındalığı", "A1 selamlaşma yetkinliği") |
| `Language` | Optional | Verilmezse adımların içerik dili belirleyicidir; karışık dilli path **görünür** olmalıdır (§7) |
| `Version` | Zorunlu | §7.1 |
| `Status` | Zorunlu | §7.2 |
| `EffectiveFrom` · `EffectiveTo` | Zorunlu / optional | `EffectiveTo < EffectiveFrom` → **400** |
| `Source` | Zorunlu | `manual` / `campaign` / `training` / `legacy-import` / `external` / `other` |
| `CreatedAt` / `CreatedBy` / `UpdatedAt` / `UpdatedBy` | Zorunlu | Standart audit seti |

Path **hiçbir pharma alanı zorunlu kılmaz**; Brand/Product/Indication/ATC/Campaign yalnız MOD-0162-FU01 §7'deki
**opsiyonel metadata** üzerinden gelir ve path seviyesinde de **opsiyoneldir**.

---

## 4. Authorized `KnowledgePathStep` Model

| Alan | Zorunluluk | Not |
|---|---|---|
| `StepId` | Zorunlu | |
| `PathId` | Zorunlu | Adım **path version'a** aittir (§7.1) |
| `StepOrder` | Zorunlu | Path içinde **unique**; deterministik sıra (§6) |
| `ContentId` | Zorunlu | **Published + effective** `KnowledgeContent` sürümü (§6) |
| `ContentCode` | Türetilir | Provenance/okunabilirlik için birlikte taşınır |
| `VersionPinPolicy` | Zorunlu | `pinned` (varsayılan) \| `latest-published` — §6.1 |
| `StepCode` | Zorunlu | Path içinde stabil, makine-okunur adım kodu |
| `StepTitle` | Zorunlu | İçerik başlığından **bağımsız** olabilir (aynı içerik farklı path'te farklı çerçevelenir) |
| `StepType` | Zorunlu | §5 |
| `IsRequired` | Zorunlu | `true` = zorunlu adım, `false` = opsiyonel adım |
| `CompletionRule` | Zorunlu | §5.1 — **beyan**dır, motor değildir |
| `PrerequisiteStepId` | Optional | Aynı path içinde, **daha küçük `StepOrder`**'a sahip adım (§6) |
| `BranchCondition` | **Optional / future** | §8 — değerlendirilmez |
| `EstimatedDurationMinutes` | Optional | |
| `Notes` | Optional | |
| `CreatedAt` / `CreatedBy` / `UpdatedAt` / `UpdatedBy` | Zorunlu | |

---

## 5. StepType Policy

Minimum değerler:

```text
intro · core-message · clinical-evidence · indication · brand-message · objection-handling · faq ·
practice · quiz · assignment · summary · closing · lesson · vocabulary · grammar · listening ·
speaking · reading · homework
```

Bu liste **hardcoded enum olarak gömülmez**; MOD-0048 reference set'i olarak yönetilir
(`knowledge-path-step-type` · `knowledge-path-status` · `knowledge-path-completion-rule` ·
`knowledge-path-source`). Set yayınlanmadan create/update **fail-closed 400** döner (MOD-0149/0150/0151/0162-FU01
precedent'i). Publish MOD-0048 operator aksiyonudur (F3).

**Not:** liste bilinçli olarak hem pharma (`clinical-evidence`, `indication`, `brand-message`,
`objection-handling`) hem eğitim (`lesson`, `vocabulary`, `grammar`, `listening`, `speaking`, `reading`,
`homework`) değerlerini içerir — tek vokabüler, iki bağlam.

### 5.1 `CompletionRule` (beyan, motor değil)

| Değer | Anlamı |
|---|---|
| `none` | Adımın tamamlanma tanımı yok (yalnız akışta yer alır) |
| `viewed` | Gösterildiğinde tamamlanmış sayılır |
| `acknowledged` | Kullanıcı/temsilci açık onay verir |
| `assessment-passed` | Değerlendirmeden geçmek gerekir — içerikte `AssessmentRequired=true` **olmalı** (aksi → **400**) |
| `duration-met` | Asgari süre şartı — `EstimatedDurationMinutes` **zorunlu** (aksi → **400**) |

`CompletionRule` **ne olduğu**nu söyler; **olup olmadığını ölçmez**. Ölçüm ve kayıt execution tarafındadır
(§10 / §11).

---

## 6. Sequence Rules

| Kural | Karar |
|---|---|
| `StepOrder` | **Zorunlu** ve path version içinde **deterministik** |
| Duplicate `StepOrder` | Aynı path version içinde **yasak** → kontrollü **409** |
| Sıra aralığı | Boşluk serbesttir (10/20/30 önerilir) — araya adım eklemek tüm adımları yeniden numaralamayı gerektirmesin |
| `ContentId` | **published + effective** içeriğe referans vermeli → aksi **400** |
| Archived içerik | **Yeni active/published path'e eklenemez**; mevcut path'lerde tarihsel referans olarak kalır (history korunur) |
| Hard delete | **Yasak** (path, step, içerik — hiçbiri) |
| Path versiyonlama | **Zorunlu** (§7.1) |
| Tüketilebilirlik | Yalnız **published + effective** path tüketilebilir |
| Draft / review path | MOD-0155'e **recommendation olarak gitmez** |
| Content version referansı | **Deterministik** olmalı (§6.1) — sessiz sürüm kayması yasak |
| `PrerequisiteStepId` | Aynı path içinde olmalı · `StepOrder`'ı **daha küçük** olmalı · döngü **yasak** → aksi **400** |
| Required ↔ optional zinciri | **Zorunlu bir adım, opsiyonel bir adıma prerequisite olamaz** → **400** (aksi hâlde zorunlu zincir atlanabilir içeriğe bağlanır) |
| Boş path | `published` bir path **en az bir `IsRequired=true` adım** içermelidir → aksi **400** |
| Subject tutarlılığı | Adım içeriğinin Subject'i path Subject'inden farklı olabilir (ör. pharma akışında bir QMS hatırlatması), ancak bu **görünür** olmalıdır — sessiz cross-subject karışım yasak (§7) |
| History | Path ve adım geçmişi korunur; hangi sürümde hangi adım vardı sorusu cevaplanabilir olmalıdır |

### 6.1 Content version determinizmi (karar)

| `VersionPinPolicy` | Davranış | Ne zaman |
|---|---|---|
| **`pinned`** *(varsayılan)* | Adım **belirli bir içerik sürümüne** (`ContentId`) sabitlenir; içerik yeni sürüm yayınlasa bile path değişmez | Regüle içerik (pharma mesajı, SOP/QMS eğitimi) — gösterilenin ne olduğu kanıtlanabilir olmalı |
| `latest-published` | Adım `ContentCode` üzerinden **o anki published+effective** sürümü çözer | Hızlı değişen, düşük riskli materyal — **açıkça seçilmelidir** |

Her iki durumda da **çözülen sürüm tüketiciye görünür** olmalıdır (`ContentId` + `Version` cevapta yer alır).
Sessiz/örtük sürüm seçimi **yasaktır**.

---

## 7. Path Versioning / Status

### 7.1 Versioning

| Kural | Karar |
|---|---|
| `PathCode` | Sürümler arası stabil kimlik |
| `Version` | Zorunlu; aynı `PathCode` altında çoklu sürüm |
| Adım aidiyeti | Adımlar **bir path sürümüne** aittir |
| Published sürüm | **Adım seti dondurulur** — yayınlanmış bir path sürümünün adımları değiştirilemez; değişiklik **yeni sürüm** gerektirir |
| Neden | Ziyarette/eğitimde "hangi akış uygulandı?" sorusu ancak dondurulmuş sürümle cevaplanabilir (evidence beklentisi §10/§11) |
| Örtüşme | Aynı `(PathCode, Language)` için örtüşen effective window'da **iki published sürüm** → kontrollü **409** |
| Archive | Archived sürüm silinmez; yeni kullanım için önerilmez, history için okunabilir kalır |

### 7.2 Status

```text
draft · review · approved · published · inactive · archived
```

MOD-0162-FU01 §9.2 ile **birebir aynı** politika: bu FU **workflow implementation açmaz**; `review`/`approved`
yalnız **future-ready metadata**dır, gerçek approval MOD-0023'e **en sonda** bağlanır (ayrı authorization).

---

## 8. Branching Boundary (future-ready, engine yok)

`BranchCondition` **optional/future metadata**dır. Bu FU'da **hiçbir evaluator, hiçbir dinamik öneri** yoktur.

Beyan edilen (değerlendirilmeyen) şekil: `ConditionCode` + `Description` + `TargetStepId?`.

Örnek koşullar (yalnız kayıt): doktor klinik kanıt sorarsa · fiyat itirazı varsa · quiz skoru düşükse ·
öğrenci önceki dersi tamamlamadıysa.

| Kural | Karar |
|---|---|
| Evaluator | **Bu FU'da yok** |
| Zorunluluk | Bir path, branch condition **olmadan da baştan sona yürünebilir** olmalıdır — lineer geçiş **eksiksiz** olmalı |
| Görünürlük | `BranchCondition` tüketiciye **veri olarak** geçer; bu FU onu yorumlamaz |
| Runtime sahibi | Runtime branching / dynamic recommendation ileride **Digital Detailing / Learning Execution / MOD-0155** tarafında ele alınır (ayrı authorization) |

---

## 9. Permission Boundary

Canonical öneriler: `crm.knowledge.path.read` · `crm.knowledge.path.manage` · `crm.knowledge.path.publish`
(publish `manage`'den **ayrı** — SoD: yazan ≠ yayınlayan; MOD-0162-FU01 §10 ile aynı desen).

**Bu pack hiçbir permission literal'ini kataloğa eklemez, seed/grant yapmaz.** Katalog hazır değilse
implementation FU'sunda anahtar tanımlanır ama `All` listesine eklenmez + geçici fallback kullanılır ve
`-RBAC` follow-up'ı açılır (F5).

---

## 10. MOD-0155 Consumer Boundary

MOD-0155 ileride `KnowledgePath`'i şöyle tüketebilir: visit objective için **önerilen path** · campaign target için
content sequence · doctor profile için anlatım akışı · ziyaret sırasında **hangi step/content gösterildiğinin
evidence'ı**.

**Bu FU:** visit plan oluşturmaz · route plan oluşturmaz · digital detailing yapmaz · content usage tracking
yapmaz · visit execution yapmaz · "en uygun path"i seçmez (öneri/skor motoru **yoktur**).

Önerilen tüketim seam'i (route'lar `integration-agent` yetkisindedir, bu pack route açmaz):

```text
GET /api/crm/knowledge-paths?subjectId=…&topicId=…&audienceProfileId=…&language=…&effectiveAt=…&status=published
GET /api/crm/knowledge-paths/{pathId}/steps        → sıralı adımlar + çözülmüş ContentId/Version
```

Katalog **sıralı şablon** döndürür; hangi path'in seçileceği, ne zaman gösterileceği ve ne kaydedileceği
tüketicinin kararıdır.

---

## 11. MOD-0309 Completion Boundary

Training completion **MOD-0309 Learning / Training Records** kapsamındadır.

**Bu FU:** completion kaydı tutmaz · learner score tutmaz · attendance tutmaz · certificate üretmez ·
quiz motoru çalıştırmaz.

`KnowledgePath` yalnız **anlatım/öğrenme akışını** tanımlar. "Tamamlandı mı, kim tamamladı, ne skor aldı,
ne zaman expire olur" soruları MOD-0309 veya ilgili execution modülüne aittir. `CompletionRule` bu iki tarafın
**sözleşme alanıdır**: bu FU beyan eder, MOD-0309 ölçer ve kaydeder.

---

## 12. MOD-0028 / MOD-0029 File Boundary

`KnowledgePathStep` yalnız `ContentId`'ye referans verir; `FileRef` hâlâ **MOD-0028/MOD-0029** doküman/file SoR'u
üzerinden gelir (MOD-0162-FU01 §5.2).

**Bu FU:** file upload yapmaz · binary storage açmaz · render/preview yapmaz · doküman kopyalamaz ·
içeriğin ikinci bir kopyasını path içinde tutmaz.

---

## 13. Explicit Exclusions

Runtime implementation · visit planning · route planning · digital detailing · content usage tracking ·
learning completion · quiz engine · **branch evaluator** · recommendation engine · campaign engine ·
segmentation engine · frequency engine · Brand/Product master implementation · approval workflow ·
MOD-0023 entegrasyonu · file upload/render/preview · Account/Contact mutation · territory mutation ·
patient data · hard delete · Mongo hand-edit · RBAC seed/grant · registry write · MOD-0048 publish ·
`TenantId` payload'da.

---

## 14. Contract Flags (öneri — implementation anlamına gelmez)

```json
{
  "supportsKnowledgePath": true,
  "supportsContentSequence": true,
  "supportsKnowledgePathVersioning": true,
  "supportsRequiredOptionalSteps": true,
  "supportsFutureBranchingMetadata": true
}
```

**Eklenmez:** `supportsVisitPlanning` · `supportsRoutePlanning` · `supportsDigitalDetailing` ·
`supportsLearningCompletion` · `supportsRecommendationEngine` · `supportsWorkflowApproval`.
MOD-0162-FU01 flag seti ve MOD-0151 canlı contract'ı (`supportsWorkflowActivation=false` dahil) **değişmez**.

---

## 15. Acceptance Criteria for Pack Approval

- [x] `KnowledgeContent` (tekil) ↔ `KnowledgePath` (zincir) ↔ `KnowledgePathStep` (adım) ayrımı yazıldı;
      `NextContentId` / `BrandContentFlow` / VisitPlan'a gömme **reddedildi**.
- [x] Path ve Step alan sözleşmeleri (§3, §4) yetkilendirildi.
- [x] `StepType` vokabüleri pharma + eğitim değerleriyle tanımlandı ve **MOD-0048'e** bağlandı.
- [x] Sıralama kuralları (unique `StepOrder`, prerequisite yönü/döngü yasağı, required↔optional zincir kuralı,
      published+effective içerik şartı, archived içerik yasağı, hard-delete yasağı) yazıldı.
- [x] Content version determinizmi `VersionPinPolicy` ile çözüldü (varsayılan `pinned`; sessiz sürüm kayması yasak).
- [x] Path versiyonlama + **published sürümde adım setinin dondurulması** kararı yazıldı.
- [x] Branching **future metadata** olarak sınırlandı; evaluator açılmadı ve lineer yürünebilirlik zorunlu kılındı.
- [x] MOD-0155 consumer, MOD-0309 completion ve MOD-0028/0029 file boundary'leri yazıldı.
- [x] Runtime / detailing / planning / completion / workflow açılmadı; `runtime_code_allowed: false`.
- [x] Reviewer onayı → `status: approved` (2026-08-09); ardından KnowledgePath implementation FU'su ayrı yetkilendirilir (FU02 kapsamı dışı).

---

## 16. Implementation Notes (implementation FU'suna devir)

- `KnowledgePath` ve `KnowledgePathStep` **ayrı aggregate**'ler mi, yoksa path aggregate'i adımları **child
  collection** olarak mı taşır — implementation FU kararıdır. Öneri: **tek aggregate (path root + step child)**;
  çünkü published sürümde adım seti dondurulur ve adımlar path'ten bağımsız yaşamaz.
- `Diten.CrmService` içinde açılır; **yeni servis yaratılmaz**. Yatay capability göçü olursa (FU01 §18/F1)
  servis kararı yeniden değerlendirilir.
- Yeni aggregate `RegisterClassMaps`'e eklenmelidir (aksi hâlde Guid FK'lar binary yazılır ve filtreler sessizce
  boş döner — MOD-0151 FU05 dersi).
- İki `DateTimeOffset` alanı (`EffectiveFrom`/`EffectiveTo`) **birlikte index'lenmez/sort edilmez** (CRM
  parallel-array tuzağı).
- Adım listesi okumaları için `StepOrder` üzerinden deterministik sıralama; eşitlik durumunda `StepCode` ile
  stabil tie-break (duplicate zaten 409, ama okuma yolu asla rastgele olmamalı).

---

## 17. Follow-up Items

| # | Follow-up | Owner | Neden |
|---|---|---|---|
| F1 | **`MOD-0162-FU02 — Knowledge Content & Taxonomy Implementation`** ile **ortak sıralama** — path implementasyonu içerik implementasyonundan sonra gelmeli | commercial-suite | Path, content aggregate'i olmadan implement edilemez |
| F2 | **`Brand/Product Master Boundary Pack Authorization`** | EA / MDM + commercial-suite | Pharma metadata hâlâ optional/future |
| F3 | **MOD-0048 path reference set publish** (`knowledge-path-step-type` / `-status` / `-completion-rule` / `-source`) | MOD-0048 operator | Hardcoded enum yasağı |
| F4 | **`Digital Detailing / Learning Execution Pack Authorization`** — runtime branching, dinamik öneri, gösterim kaydı | commercial-suite / EA | Branch evaluator ve execution bu FU'da kasıtlı kapalı (§8) |
| F5 | **`MOD-0162-FU01A-RBAC — KnowledgePath Permission Catalog Alignment`** | MOD-0018 / commercial-suite | §9 anahtarları katalog + grant gerektirir |
| F6 | **MOD-0309 completion sözleşmesi** — `CompletionRule` ↔ completion record eşlemesi | HCM / MOD-0309 | Beyan burada, ölçüm orada (§11) |
| F7 | **Path approval workflow (MOD-0023)** — `review`/`approved` bugün yalnız metadata | commercial-suite + MOD-0023 | En sona bırakıldı |
| F8 | **EA kimlik kararı** (MOD-0162-FU01 §18/F1 ile ortak) | EA / registry owner | Yatay capability göçü bu pack'i de kapsar |
| F9 | **Multi-visit/session progression** — ✅ **KAPATILDI 2026-08-02** → [MOD-0162-FU01B — EngagementJourney](MOD-0162-FU01B-engagement-journey-multi-visit-content-progression.md) | commercial-suite | Tek oturum sırası ≠ çok oturumlu aşama zinciri; `visit-N` mantığının path'e sızma yasağı orada sabitlendi |

---

## 18. Next Recommended Prompt

1. **`Brand/Product Master Boundary Pack Authorization`**
2. **`MOD-0162-FU02 — Knowledge Content & Taxonomy Implementation`** (path implementasyonu bunun ardından gelir)
3. **`Digital Detailing / Learning Execution Pack Authorization`** — runtime branching + gösterim/completion akışı.
