---
id: MOD-0162-FU01B
name: EngagementJourney / Multi-Visit Content Progression Boundary
parent: MOD-0162
parent_name: Knowledge Base
siblings: MOD-0162-FU01, MOD-0162-FU01A
domain: commercial-suite
service: Diten.CrmService
shell: none
golden_reference: none
entity_base: EntityBase
status: approved
runtime_code_allowed: false
canonical_name: "ContentEngagementJourney / ContentEngagementJourneyStage (F1 resolved 2026-08-26)"
runtime_code_scope: "NONE — bu pack yalnız EngagementJourney / EngagementJourneyStage sahipliği ve multi-visit progression boundary'sidir. Aggregate, endpoint, progress state, stage advancement engine, branch evaluator, recommendation engine, UI ve migration ayrı bir implementation FU authorization'ı gerektirir."
owner: module-pack-author
branch: feature/crm/mod-0162-fu01b-engagement-journey-multi-visit-progression
started: 2026-08-02
target: TBD (implementation FU ayrı yetkilendirilir)
form_field_count: 0
dependencies:
  - MOD-0162-FU01 (KnowledgeContent / Subject / Topic / AudienceProfile — hard prerequisite)
  - MOD-0162-FU01A (KnowledgePath / KnowledgePathStep — hard prerequisite)
  - MOD-0162 (parent — Knowledge Base)
  - MOD-0166 (boundary — Journeys & Automation; otomasyon/trigger/run-log SoR'u orada)
  - MOD-0155 (consumer — visit execution, stage progress, usage evidence)
  - MOD-0309 (consumer — completion / score / attendance / certificate)
  - MOD-0165-FU01 / MOD-0167-FU01 (frequency policy — "ne sıklıkla"; journey "hangi aşamadayız")
  - MOD-0048 (reference data — journey status / stage / advancement vokabüleri)
  - MOD-0018 (RBAC — yalnız tüketim; seed/grant bu pack'te yok)
---

# MOD-0162-FU01B — EngagementJourney / Multi-Visit Content Progression Boundary

> **✅ APPROVED (2026-08-26) — F1 resolved; `status: approved`.** Canonical-name decision (F1, user/EA authority):
> the capability is **`ContentEngagementJourney` / `ContentEngagementJourneyStage`**, permanently separating it from
> MOD-0166 "Journeys & Automation" (automation orchestration) and MOD-0113 "Journey Mapping". The implementation FU
> (MOD-0162-FU05) and all runtime artifacts adopt this canonical name; vocab sets, endpoints and permission keys use the
> `content-engagement-journey` form. This boundary body's earlier `EngagementJourney` label is superseded by that name.
> The semantic boundary in §2.1 (no trigger/action/channel/suppression/run-log — those are MOD-0166) is unchanged.
> The historical hold note follows.
>
> **⏸️ (historical) APPROVAL HELD (2026-08-09) — was `status: draft`.** Governance review
> [mod-0162-boundary-approval-review-fu01-fu01a-fu01b-fu01c-2026-08-09.md](../../../../docs/audits/mod-0162-boundary-approval-review-fu01-fu01a-fu01b-fu01c-2026-08-09.md).
> Pack içerik-eksiksiz ve model/boundary sağlam; **ancak §15'te kendi belirttiği işaretsiz gating acceptance
> criterion** var: *"F1 adlandırma uzlaştırması (EA): `EngagementJourney` ↔ MOD-0166 'journey' ayrımı kalıcı
> olarak kayda geçmeli."* MOD-0166 Journeys & Automation, Blueprint'te *journey definitions* SoR'una sahip **canlı
> bir capability**dir; isim çakışması gerçek bir sahiplik riskidir (§2.1). Bu EA kararı verilmeden approve edilmez.
> **FU02 için blocker DEĞİLDİR:** MOD-0162-FU02 EngagementJourney runtime'ını **açmaz** (FU02 §18 exclusions;
> §20/F-A/B/C ayrı FU). Bu nedenle FU01B'nin draft kalması F-BND'yi FU02 için açık bırakmaz.
>
> **BOUNDARY AUTHORIZATION (2026-08-02) — `runtime_code_allowed: false`.**
> Bu pack **kod yazma yetkisi vermez**. Yetkilendirdiği tek şey, *"birden fazla ziyaret / oturum / temas boyunca
> hangi aşamadan hangi aşamaya geçilecek ve her aşamada hangi path uygulanacak?"* sorusunun **sahibi, modeli ve
> sınırıdır**. Journey progress state, stage advancement engine, branch evaluator, recommendation engine,
> visit execution, digital detailing, UI ve migration **açılmamıştır**.
>
> **Neden şimdi:** [MOD-0162-FU01](MOD-0162-FU01-knowledge-content-subject-taxonomy.md) tekil içeriği,
> [MOD-0162-FU01A](MOD-0162-FU01A-knowledge-path-content-sequence.md) **tek görüşme/ders içindeki sırayı** kapattı.
> Açık kalan üst seviye soru: *1. ziyarette hangi path, 2. ziyarette hangi path, doktor ikna olmadıysa 3.
> ziyarette hangi aşama?* Bu kapanmazsa çok-ziyaretli akış ya `VisitPlan` içine gömülür ya da FU01A path'ine
> `visit-1/visit-2/visit-3` mantığı sızar.
>
> **DCP-002 kimlik kapısı — PASS (2026-08-02):**
> `py .antigravity/scripts/verify_module_id.py . --check-id MOD-0162-FU01B --name "EngagementJourney / Multi-Visit Content Progression Boundary" --parent MOD-0162`
> → `OK  MOD-0162-FU01B: proven against Blueprint/registry.` (exit 0).
> MOD-0162-FU01'in kimlik notu (domain-nötr model, EA yatay capability göçü kararı — FU01 §18/F1) bu pack'i de
> kapsar.
>
> Otorite sırası: **Blueprint Excel** > Module Pack > [Domain Config](../domain-config.md) > `AGENTS.md` >
> `.antigravity/rules/`.

---

## 1. Module Summary

```text
KnowledgeContent        = tekil içerik
KnowledgePath           = tek visit/session içindeki içerik sırası          (FU01A)
EngagementJourney       = çoklu visit/session boyunca ilerleyen aşamalar    (bu FU)
EngagementJourneyStage  = journey içindeki bir aşama; bir KnowledgePath'e bağlanır
```

| Yanlış model | Neden yanlış |
|---|---|
| `VisitPlan` içine content journey hardcoded yazmak | Aşama sahipliğini MOD-0155'e gömer; aynı yolculuk eğitimde yeniden kullanılamaz |
| `KnowledgePath` içine visit-1 / visit-2 / visit-3 mantığı gömmek | Path tek oturumun sırasıdır; çok-oturumlu ilerleme onu şişirir ve tekrar kullanımını bitirir |
| `BrandProductJourney` | Modeli pharma'ya kilitler; kurs/eğitim/onboarding yolculukları dışarıda kalır |
| `Contact.CurrentJourneyStage` | Aynı kişi farklı journey'lerde farklı aşamada olabilir; tek alan bunu kaybeder |
| `Account.CurrentJourneyStage` | Aynı kurum farklı kişiler/journey'ler için farklı aşamadadır; ayrıca bu bir **runtime state**tir, şablon değil |

**Temel kural:** *`EngagementJourney` bir **şablon**dur, bir **execution** değildir.* Current stage, ilerletme,
visit execution ve usage tracking bu FU'da **yoktur**.

---

## 2. Ownership Decision

| Soru | Sahip |
|---|---|
| Ne anlatılacak / hangi içerik / kime uygun? | MOD-0162-FU01 |
| Tek görüşmede hangi sırayla? | MOD-0162-FU01A |
| **Çoklu görüşmede hangi aşamadan hangi aşamaya, her aşamada hangi path?** | **Bu FU (MOD-0162-FU01B)** |
| Ne sıklıkla gidilecek? | MOD-0165 / MOD-0167 |
| Visit/route plan, execution, **current stage progress**, gösterim evidence'ı | **MOD-0155** |
| Completion / score / attendance / certificate | **MOD-0309** |
| **Otomasyon journey'i: trigger, suppression, kanal, run log** | **MOD-0166** (§2.1) |
| Dosya / binary / controlled document | MOD-0028 / MOD-0029 |

### 2.1 MOD-0166 "Journeys & Automation" ile ad çakışması (kayda geçen karar)

**Bulgu:** Blueprint'te `MOD-0166 Journeys & Automation`'ın SoR'u zaten *journey definitions, trigger rules,
journey run logs*'tur ([crm-sor-boundary.md](../crm-sor-boundary.md) satır 20/43). "Journey" kelimesi tek başına
iki farklı nesneyi işaret edebilir ve bu, ileride sessiz bir sahiplik çakışmasına dönüşür.

**Karar (kesin sınır):**

| | **Bu FU — `EngagementJourney`** | **MOD-0166 — Automation Journey** |
|---|---|---|
| Doğası | **İçerik ilerleme şablonu** (anlatım/öğrenme aşamaları) | **Otomasyon orkestrasyonu** |
| Yürütücü | **İnsan** (MR ziyareti, eğitmen/öğrenci oturumu) | **Sistem** (tetiklenen otomatik akış) |
| İçerir | Stage + `RecommendedKnowledgePathId` | Trigger, wait, suppression, kanal aksiyonu, run log |
| İçermez | **Trigger yok · aksiyon yok · kanal yok · suppression yok · run log yok · runtime state yok** | — |
| Yön | MOD-0166 ileride bir `EngagementJourney` stage'ini **referans alabilir** | `EngagementJourney` otomasyon **yürütmez** |

Bu FU **hiçbir trigger/aksiyon/kanal/suppression/run-log tanımlamaz**. Ad karışıklığını kalıcı olarak çözmek için
EA'ya adlandırma uzlaştırması follow-up'ı açıldı (F1); alternatif adlar: `ContentEngagementJourney` ·
`EngagementProgression`. Pack gövdesi addan bağımsızdır.

---

## 3. Authorized `EngagementJourney` Model

| Alan | Zorunluluk | Not |
|---|---|---|
| `JourneyId` | Zorunlu | Aggregate kimliği |
| `TenantId` | Zorunlu | **JWT claim'inden**; payload'da **asla** |
| `JourneyCode` | Zorunlu | Sürümler arası **stabil** kimlik |
| `JourneyName` · `Description` | Zorunlu / optional | |
| `SubjectId` | **Zorunlu** | Journey her zaman bir anlatım alanına aittir |
| `TopicId` | Optional | Verilirse `SubjectId` ile tutarlı olmalı |
| `AudienceProfileId` | Optional | Yoksa journey **genel** kabul edilir; uydurma profil atanmaz |
| `Objective` | Zorunlu | Yolculuğun amacı (ör. "Almiba reçete niyeti", "A1 seviyesini tamamlama") |
| `Language` | Optional | Karışık dilli journey **görünür** olmalıdır |
| `Version` | Zorunlu | §5.1 |
| `Status` | Zorunlu | §5.2 |
| `EffectiveFrom` · `EffectiveTo` | Zorunlu / optional | `EffectiveTo < EffectiveFrom` → **400** |
| `Source` | Zorunlu | `manual` / `campaign` / `training` / `legacy-import` / `external` / `other` |
| `CampaignId` · `BrandId` · `ProductId` · `SegmentId` | **Optional / future** | Hiçbiri zorunlu değildir; boşken journey tam çalışır |
| `CreatedAt` / `CreatedBy` / `UpdatedAt` / `UpdatedBy` | Zorunlu | |

**Kurallar:** `JourneyCode` stabil · `Version` zorunlu · **hard delete yok** · `archived` journey yeni
planlama/öneri için kullanılmaz (history okunabilir kalır) · yalnız **published + effective** journey tüketilebilir ·
**draft/review journey MOD-0155'e aktif öneri olarak gitmez** · Brand/Product **zorunlu değil** · journey pharma
dışı subject'leri (kurs, SOP eğitimi, onboarding) **birinci sınıf** destekler.

---

## 4. Authorized `EngagementJourneyStage` Model

| Alan | Zorunluluk | Not |
|---|---|---|
| `StageId` | Zorunlu | |
| `JourneyId` | Zorunlu | Stage **journey version'a** aittir (§5.1) |
| `StageOrder` | Zorunlu | Journey içinde **unique**; duplicate → **409**; boşluk serbest (10/20/30) |
| `StageCode` | Zorunlu | Journey içinde stabil, makine-okunur kod |
| `StageName` · `StageObjective` | Zorunlu | Aşamanın adı ve amacı |
| `RecommendedKnowledgePathId` | Zorunlu | **published + effective** `KnowledgePath` (§10) |
| `PathVersionPinPolicy` | Zorunlu | `pinned` (varsayılan) \| `latest-published` — §10 |
| `MinVisitNumber` · `MaxVisitNumber` | Optional | **Yalnız boundary metadata**; `Max < Min` → **400**; runtime scheduling **yok** |
| `Repeatable` | Zorunlu | `true` = aynı stage birden fazla visit/session'da uygulanabilir |
| `IsRequired` | Zorunlu | `published` journey **en az bir** `IsRequired=true` stage içermeli → aksi **400** |
| `AdvancementRule` | **Optional / future** | §6 — değerlendirilmez |
| `FallbackStageId` | **Optional / future** | Aynı journey içinde olmalı, kendisi olamaz → aksi **400**; geriye işaret edebilir (itiraz → pekiştirmeye dönüş) |
| `BranchCondition` | **Optional / future** | §6 — değerlendirilmez |
| `Notes` | Optional | |
| `CreatedAt` / `CreatedBy` / `UpdatedAt` / `UpdatedBy` | Zorunlu | |

**Kurallar:** stage bir `KnowledgePath`'e **bağlanır**, path'in **step detaylarını kopyalamaz** · bir stage içinde
path tekrar kullanılabilir · **bir `KnowledgePath` birden fazla stage'de kullanılabilir** · stage
version/history korunur · **published journey'de stage seti dondurulur** (§5.1).

---

## 5. Journey Versioning / Status

### 5.1 Versioning

| Kural | Karar |
|---|---|
| `JourneyCode` | Sürümler arası stabil kimlik |
| `Version` | Zorunlu; aynı `JourneyCode` altında çoklu sürüm |
| Stage aidiyeti | Stage'ler **bir journey sürümüne** aittir |
| Published sürüm | **Stage seti dondurulur** — yayınlanmış sürümün aşamaları değiştirilemez; değişiklik **yeni sürüm** ister |
| Neden | "Bu doktora hangi yolculuk uygulandı?" sorusu ancak dondurulmuş sürümle cevaplanabilir (evidence beklentisi §8/§9) |
| Stage-path eşlemesi | Published journey sürümünde **deterministik** olmalıdır (§10) |
| Örtüşme | Aynı `(JourneyCode, Language)` için örtüşen effective window'da **iki published sürüm** → **409** |
| Archive | Archived sürüm silinmez; yeni kullanım için önerilmez, history için okunabilir |

### 5.2 Status

```text
draft · review · approved · published · inactive · archived
```

MOD-0162-FU01 §9.2 / FU01A §7.2 ile **birebir aynı** politika: bu FU **workflow implementation açmaz**;
`review`/`approved` yalnız **future-ready metadata**dır, gerçek approval MOD-0023'e **en sonda** bağlanır.

Vokabülerler MOD-0048 set'i olarak yönetilir (`engagement-journey-status` · `engagement-journey-stage-type`
*(opsiyonel)* · `engagement-journey-advancement-rule` · `engagement-journey-source`); hardcoded enum yasak,
set yayınlanmadan **fail-closed 400** (F3).

---

## 6. Stage Progression Boundary (future-ready, engine yok)

Bu FU **stage ilerletme engine'i yapmaz**. `AdvancementRule` ve `BranchCondition` **optional/future metadata**dır
ve **değerlendirilmez**.

Örnek kurallar (yalnız kayıt): `visit completed` · `all required steps acknowledged` ·
`doctor asked for clinical evidence` · `objection recorded` · `quiz passed` · `manager manually advanced` ·
`repeat stage until condition met`.

| Kural | Karar |
|---|---|
| Evaluator | **Bu FU'da yok** |
| `CurrentJourneyStage` runtime state | **Bu FU'da tutulmaz** — ne Contact'ta, ne Account'ta, ne journey aggregate'inde |
| Progress state sahibi | **MOD-0155 / Digital Detailing / Learning Execution / MOD-0309** boundary'si (ayrı authorization) |
| Zorunlu kısıt | Bir journey, advancement rule **olmadan da** `StageOrder` sırasıyla baştan sona yürünebilir olmalıdır — lineer geçiş **eksiksiz** |
| Görünürlük | `AdvancementRule` / `FallbackStageId` / `BranchCondition` tüketiciye **veri olarak** geçer; bu FU onları yorumlamaz |

---

## 7. Repeat / Revisit Policy

Doktor **tek görüşmede ikna olmayabilir**; öğrenci bir dersi **tekrar etmek** zorunda kalabilir. Model bunu
yasaklamaz, **görünür** kılar.

| Kural | Karar |
|---|---|
| Aynı `KnowledgePath` birden fazla stage'de | **Serbest** |
| Aynı `KnowledgeContent` farklı path/stage'lerde | **Serbest** — tekrar yasak değildir |
| Tekrarın raporlanabilirliği | Aynı içerik/path'in journey içinde kaç stage'de geçtiği **raporlanabilir** olmalıdır (implementation FU'sunda read projection) |
| `Repeatable` | **Açıkça işaretlenir**; varsayılan `false` |
| `Repeatable=false` | Aynı target/session bağlamında tekrar uygulanması **consumer tarafından** engellenebilir; **engine bu FU'da yok** |
| `MaxVisitNumber` | Varsa consumer buna **uymalıdır**; zorlayıcı runtime kontrolü bu FU'da yok |
| `MinVisitNumber` / `MaxVisitNumber` | **Yalnız boundary metadata**; scheduling MOD-0155'te |

---

## 8. Campaign / Frequency Integration Boundary

| Soru | Cevap veren |
|---|---|
| Ne sıklıkla gidilecek? | **MOD-0165 / MOD-0167** (`VisitFrequencyPolicy`) |
| **Bu temas serisinde hangi aşamadayız ve hangi path uygulanmalı?** | **Bu FU** |
| Kim, ne zaman, hangi rotayla, gerçekte ne oldu? | **MOD-0155** |

Campaign ileride bağlayabilir:

```text
Campaign = Almiba Q1 · Target = Kardiyoloji A segment doktorlar · Frequency = ayda 2
EngagementJourney = Almiba Q1 Doctor Engagement Journey
```

**Bu FU:** campaign engine yapmaz · frequency runtime yapmaz · due/overdue hesaplamaz · visit plan oluşturmaz ·
**journey target assignment yapmaz** (hangi doktora hangi journey atandığı bu FU'da **yoktur**; bu bir hedefleme
kararıdır → MOD-0165/MOD-0167 + MOD-0155).

Çift yönlü gömme yasağı (MOD-0165-FU01 §12 ile aynı ruh): journey frequency policy'ye gömülmez, frequency kuralı
journey'e gömülmez.

---

## 9. MOD-0155 Consumer Boundary

MOD-0155 ileride tüketebilir: selected journey · current/recommended stage · `RecommendedKnowledgePathId` ·
stage objective · `Repeatable` bilgisi · visit objective'e bağlanacak path.

**MOD-0155'te (ayrı implementation) kalanlar:** visit plan · route plan · daily/weekly schedule · visit execution ·
**current stage progress** · **stage advancement** · content usage evidence · doctor response / objection capture.

**Bu FU bunların hiçbirini yapmaz** ve "en uygun journey"i **seçmez** (öneri/skor motoru yoktur).

Önerilen tüketim seam'i (route'lar `integration-agent` yetkisindedir, bu pack route açmaz):

```text
GET /api/crm/engagement-journeys?subjectId=…&audienceProfileId=…&campaignId=…&effectiveAt=…&status=published
GET /api/crm/engagement-journeys/{journeyId}/stages   → sıralı stage'ler + çözülmüş KnowledgePathId/Version
```

---

## 10. KnowledgePath Dependency Boundary

| Kural | Karar |
|---|---|
| Bağlanma | Stage **`RecommendedKnowledgePathId`** ile bağlanır; **step detaylarını kopyalamaz** |
| Path durumu | **published + effective** olmalı → aksi **400** |
| Archived path | **Yeni published journey stage'e bağlanamaz**; mevcut journey'lerde tarihsel referans kalır |
| Version pinning | **`PathVersionPinPolicy`**: `pinned` (varsayılan — regüle içerik/akış) veya `latest-published` (açıkça seçilir) |
| Görünürlük | Çözülen `KnowledgePathId` + `Version` tüketiciye **görünür** olmalıdır |
| Sessiz kayma | **Yasak** — path yeni sürüm yayınladığında `pinned` stage değişmez |
| Determinizm | Published journey sürümünde stage→path eşlemesi **deterministik** olmalıdır |
| Subject tutarlılığı | Cross-subject stage (pharma journey'inde bir uyum hatırlatması) **yasak değil ama görünür** olmalı (FU01A §6 ile aynı) |

Bu politika FU01A §6.1'in (step→content pinning) **journey seviyesindeki karşılığıdır**; iki katman da aynı
determinizm kuralını taşır.

---

## 11. Permission Boundary

Canonical öneriler: `crm.knowledge.journey.read` · `crm.knowledge.journey.manage` ·
`crm.knowledge.journey.publish` (publish `manage`'den **ayrı** — SoD; FU01 §10 / FU01A §9 deseni).

**Bu pack hiçbir permission literal'ini kataloğa eklemez, seed/grant yapmaz.** Katalog hazır değilse
implementation FU'sunda anahtar tanımlanır ama `All` listesine eklenmez + geçici fallback + `-RBAC` follow-up (F5).

---

## 12. MOD-0309 Completion Boundary

Eğitim senaryolarında completion / score / attendance / certificate **MOD-0309 Learning / Training Records**
kapsamındadır.

**Bu FU:** course completion kaydı tutmaz · learner progress state tutmaz · score tutmaz · certificate üretmez ·
attendance tutmaz · quiz/assessment engine çalıştırmaz.

`EngagementJourney` yalnız **öğrenme/temas aşamalarını** tanımlar; "tamamlandı mı, kim tamamladı, ne skor aldı"
MOD-0309'a aittir. `AdvancementRule` bu iki tarafın **sözleşme alanıdır**: bu FU beyan eder, execution tarafı ölçer.

---

## 13. Explicit Exclusions

Runtime implementation · visit planning · route planning · digital detailing · content usage tracking ·
**journey progress engine** · **stage advancement engine** · branch evaluator · recommendation engine ·
campaign engine · segmentation engine · frequency engine · due/overdue engine · last visit history ·
visit execution · learning completion · quiz/assessment engine · journey target assignment ·
Brand/Product master implementation · approval workflow · MOD-0023 entegrasyonu · file upload/render/preview ·
Account/Contact mutation · territory mutation · patient data · hard delete · Mongo hand-edit · RBAC seed/grant ·
registry write · MOD-0048 publish · `TenantId` payload'da.

---

## 14. Contract Flags (öneri — implementation anlamına gelmez)

```json
{
  "supportsEngagementJourney": true,
  "supportsMultiVisitContentProgression": true,
  "supportsJourneyStages": true,
  "supportsRepeatableStages": true,
  "supportsFutureStageAdvancementMetadata": true
}
```

**Eklenmez:** `supportsVisitPlanning` · `supportsRoutePlanning` · `supportsDigitalDetailing` ·
`supportsJourneyRuntimeProgress` · `supportsRecommendationEngine` · `supportsWorkflowApproval`.
MOD-0162-FU01 / FU01A flag setleri ve MOD-0151 canlı contract'ı (`supportsWorkflowActivation=false` dahil)
**değişmez**.

---

## 15. Acceptance Criteria for Pack Approval

- [x] `EngagementJourney` / `EngagementJourneyStage` ayrı model olarak yetkilendirildi; `KnowledgePath` **tek
      visit/session içi sıra** olarak kaldı.
- [x] Beş yanlış model açıkça reddedildi (VisitPlan'a gömme · path'e visit-N mantığı · `BrandProductJourney` ·
      `Contact.CurrentJourneyStage` · `Account.CurrentJourneyStage`).
- [x] Journey ve Stage alan sözleşmeleri (§3, §4) yazıldı; Brand/Product/Campaign/Segment **opsiyonel/future** kaldı.
- [x] Versiyonlama + published sürümde **stage setinin dondurulması** kararı yazıldı.
- [x] Repeat/revisit policy tanımlandı; içerik/path tekrarı **yasaklanmadı ama görünür** kılındı.
- [x] Stage progression **future metadata** olarak sınırlandı; evaluator ve `CurrentJourneyStage` state açılmadı.
- [x] `KnowledgePath` bağımlılığı `PathVersionPinPolicy` ile deterministik hâle getirildi (sessiz sürüm kayması yasak).
- [x] MOD-0155 consumer, MOD-0309 completion, Campaign/Frequency ve **MOD-0166 otomasyon** sınırları yazıldı.
- [x] Runtime / planning / detailing / progress / workflow açılmadı; `runtime_code_allowed: false`.
- [x] Reviewer onayı → `status: approved` (2026-08-26); implementation FU (MOD-0162-FU05) ayrı yetkilendirilir.
- [x] **F1 adlandırma uzlaştırması** (2026-08-26): kanonik ad = **`ContentEngagementJourney`** — MOD-0166 automation "journey" ve MOD-0113 "journey mapping"den kalıcı ayrım (§2.1).

---

## 16. Implementation Notes (implementation FU'suna devir)

- Sıralama: **FU01 içerik → FU01A path → FU01B journey**. Journey, path aggregate'i olmadan implement edilemez.
- Öneri: **tek aggregate** (journey root + stage child) — published sürümde stage seti donduğu için stage'ler
  journey'den bağımsız yaşamaz (FU01A ile aynı gerekçe).
- Yeni aggregate `RegisterClassMaps`'e eklenmelidir (Guid FK'lar aksi hâlde binary yazılır ve filtreler sessizce
  boş döner — MOD-0151 FU05 dersi).
- İki `DateTimeOffset` alanı (`EffectiveFrom`/`EffectiveTo`) **birlikte index'lenmez/sort edilmez** (CRM
  parallel-array tuzağı).
- Stage okumaları `StageOrder` ile deterministik sıralanır; eşitlik `StageCode` ile stabil tie-break (duplicate
  zaten 409, okuma yolu asla rastgele olmamalı).
- "Tekrar raporu" (aynı content/path kaç stage'de geçiyor) bir **read projection**dır; yeni aggregate gerektirmez.

---

## 17. Follow-up Items

| # | Follow-up | Owner | Neden |
|---|---|---|---|
| F1 | ✅ **RESOLVED (2026-08-26) → `ContentEngagementJourney`** (was `EngagementJourney`; alternatives `EngagementProgression` closed). Runtime FU (MOD-0162-FU05) + vocab/endpoint/permission forms adopt `content-engagement-journey`. | EA / commercial-suite governance | İki farklı "journey" nesnesi aynı tenantta karışabilir (§2.1) — kalıcı olarak çözüldü |
| F2 | **`Brand/Product Master Boundary Pack Authorization`** | EA / MDM + commercial-suite | Pharma metadata hâlâ optional/future |
| F3 | **MOD-0048 journey reference set publish** (`engagement-journey-status` / `-advancement-rule` / `-source`) | MOD-0048 operator | Hardcoded enum yasağı |
| F4 | **`Digital Detailing / Learning Execution Pack Authorization`** — journey progress state, stage advancement, branching runtime, usage evidence | commercial-suite / EA | Bu FU'da kasıtlı kapalı (§6) — FU01A F4 ile **aynı** follow-up, kapsamı genişledi |
| F5 | **`MOD-0162-FU01B-RBAC — EngagementJourney Permission Catalog Alignment`** | MOD-0018 / commercial-suite | §11 anahtarları katalog + grant gerektirir |
| F6 | **Journey target assignment sahipliği** — hangi doktora/öğrenciye hangi journey atanır | MOD-0165 / MOD-0167 + MOD-0155 | Bu FU'da kasıtlı **yok** (§8) |
| F7 | **MOD-0309 completion ↔ `AdvancementRule` eşlemesi** | HCM / MOD-0309 | Beyan burada, ölçüm orada (§12) |
| F8 | **Journey approval workflow (MOD-0023)** | commercial-suite + MOD-0023 | `review`/`approved` bugün yalnız metadata |
| F9 | **EA kimlik kararı** (MOD-0162-FU01 §18/F1 ile ortak) | EA / registry owner | Yatay capability göçü bu pack'i de kapsar |

---

## 18. Next Recommended Prompt

1. **`Brand/Product Master Boundary Pack Authorization`**
2. **`MOD-0162-FU02 — Knowledge Content & Taxonomy Implementation`** (path ve journey implementasyonları bunun
   ardından gelir)
3. **`Digital Detailing / Learning Execution Pack Authorization`** — journey progress + stage advancement +
   runtime branching + gösterim/completion akışı.
