---
id: MOD-0162-FU01
name: Knowledge Content & Subject Taxonomy Foundation
parent: MOD-0162
parent_name: Knowledge Base
domain: commercial-suite
service: Diten.CrmService
shell: none
golden_reference: none
entity_base: EntityBase
status: approved
runtime_code_allowed: false
runtime_code_scope: "NONE — bu pack yalnız Knowledge/Content + Subject/Topic taxonomy sahipliği ve boundary yetkilendirmesidir. Aggregate, endpoint, dosya yükleme, arama, UI ve migration ayrı bir implementation FU authorization'ı gerektirir. Runtime yetkisi MOD-0162-FU02'ye aittir; approval bu pack'e runtime yetkisi VERMEZ."
owner: module-pack-author
branch: feature/crm/mod-0162-fu01-knowledge-content-subject-taxonomy
started: 2026-08-02
target: TBD (implementation FU ayrı yetkilendirilir)
form_field_count: 0
dependencies:
  - MOD-0162 (parent — Knowledge Base; Blueprint SoR "knowledge articles")
  - MOD-0028 / MOD-0029 (binary/controlled document SoR — dosya bu pack'te üretilmez)
  - MOD-0155 (consumer — visit objective / detailing içeriği)
  - MOD-0165-FU01 / MOD-0167-FU01 (frequency policy — "ne sıklıkla"; içerik "ne anlatılacak")
  - MOD-0309 (consumer — Learning / Training Records; completion SoR orada)
  - MOD-0048 (reference data — content type / status / language vokabüleri)
  - MOD-0018 (RBAC — yalnız tüketim; seed/grant bu pack'te yok)
  - MOD-0056 / MOD-0057 (future — enterprise search + semantic tagging seam)
---

# MOD-0162-FU01 — Knowledge Content & Subject Taxonomy Foundation

> **✅ BOUNDARY APPROVAL (2026-08-09) — `status: draft → approved`.** Governance review
> [mod-0162-boundary-approval-review-fu01-fu01a-fu01b-fu01c-2026-08-09.md](../../../../docs/audits/mod-0162-boundary-approval-review-fu01-fu01a-fu01b-fu01c-2026-08-09.md)
> ile onaylandı. Bu, MOD-0162-FU02'nin **F-BND** blocker'ını karşılayan SoT sözleşmesidir. `runtime_code_allowed`
> **`false` kalır** — runtime/UI yetkisi yalnız MOD-0162-FU02'ye aittir. EA kimlik kararı (§18/F1) **non-blocking
> follow-up**tur; pack gövdesini değiştirmez. Bu onay, FU02'nin `KnowledgeContent`/`Subject`/`Topic`/
> `AudienceProfile` §4–§9 sözleşmesiyle uyumluluğu doğrulanarak verilmiştir.
>
> **BOUNDARY / OWNERSHIP AUTHORIZATION (2026-08-02) — `runtime_code_allowed: false`.**
> Bu pack **kod yazma yetkisi vermez**. Yetkilendirdiği tek şey, *"gidince ne anlatılacak / hangi içerik
> gösterilecek / hangi subject-topic işlenecek / hangi versiyon geçerli / kime uygun?"* sorularının **sahibi,
> kavramsal modeli ve tüketim boundary'sidir**. Aggregate, endpoint, dosya yükleme, arama indeksi, digital
> detailing, UI, migration, reference set publish ve RBAC grant **açılmamıştır**.
>
> **Neden şimdi:** MOD-0165-FU01 / MOD-0167-FU01 (PASS, 2026-08-02) *"ne sıklıkla gidilecek?"* sorusunu kapattı ve
> içerik sahipliğini açık bir follow-up olarak bıraktı (MOD-0165-FU01 §12/F2). MOD-0150 *"ne zaman müsait?"*,
> MOD-0151 FU09A *"kim sorumlu / coverage current mı?"* sorularını kapattı. MOD-0155 Visit Planning başlamadan
> kapanması gereken son içerik sorusu **"gidince ne anlatılacak?"**tir.
>
> ### Identity note (DCP-002 — GATE PASSED, 2026-08-02)
> `py .antigravity/scripts/verify_module_id.py . --check-id MOD-0162-FU01 --name "Knowledge Content & Subject Taxonomy Foundation" --parent MOD-0162`
> → `OK  MOD-0162-FU01: proven against Blueprint/registry.` (exit 0).
> **Neden MOD-0162:** Blueprint'te bu yeteneğin tam karşılığı olan yatay bir "Knowledge Content & Subject Taxonomy"
> capability'si **yoktur**; SoR'u *knowledge articles* olan tek Blueprint capability'si `MOD-0162 Knowledge Base`'dir
> ve DCP-002 kimlik kuralı **"Blueprint lookup → mevcut MOD veya FU → ancak yoksa CAND-CAP"** sırasını dayatır.
> **Denenen alternatif:** yatay bir aday kimlik `CAND-CAP-0006` — gate **fail-closed BLOCKED** döndü
> (`CAND-CAP-0006 has no registry row` · `not recorded in the reconciliation ledger`, exit 2); registry satırı
> yazmak bu pack'in yetkisi dışıdır.
> **Sonuç:** kimlik **MOD-0162-FU01** olarak alındı; ancak bu pack'in içerik modeli **kasıtlı olarak
> domain-nötr**dür (pharma + eğitim + QMS + onboarding). Yetenek MOD-0162'nin Service/self-service KB kapsamından
> **geniştir**; EA bunu yatay bir platform capability'sine taşımak isterse kimlik göçü **F1 follow-up**'ıdır ve
> pack gövdesi (model, taxonomy, boundary) değişmeden taşınabilir.
>
> Otorite sırası: **Blueprint Excel** > Module Pack > [Domain Config](../domain-config.md) > `AGENTS.md` >
> `.antigravity/rules/`.

---

## 1. Module Summary

Bu FU, **`KnowledgeContent`** merkezli bir içerik kataloğunu ve onun **`Subject` / `Topic` / `AudienceProfile`**
taksonomisini yetkilendirir.

**Temel mimari karar:** içerik **Brand/Product content olarak modellenmez**. İçerik yalnızca pharma ürün anlatımı
değildir:

```text
Almiba ürün sunumu · Tutukon Neo doktor broşürü · indication bazlı klinik özet · ATC bazlı sınıflama ·
doktor profiline göre mesaj seti · Almanca A1 dersi · SOP eğitimi · QMS Document Control training ·
onboarding içeriği · teknik eğitim · regülasyon eğitimi · satış argümanı · FAQ · objection handling notu
```

| Yanlış model | Doğru model |
|---|---|
| `BrandContent` / `ProductContent` | **`KnowledgeContent`** + `Subject`/`Topic`/`AudienceProfile` + **opsiyonel** pharma / learning / campaign metadata |

Brand/Product **merkez değil, opsiyonel bir metadata boyutudur**.

---

## 2. Ownership Decision

Bu FU şu soruların sahibidir:

```text
Ne anlatılacak?
Hangi içerik gösterilecek?
Hangi subject/topic işlenecek?
Hangi versiyon geçerli?
Hangi audience/profile için uygun?
```

| Soru | Sahip |
|---|---|
| **Ne sıklıkla gidilecek?** | MOD-0165 / MOD-0167 (`VisitFrequencyPolicy`) |
| **Ne zaman ziyaret edilebilir?** | MOD-0150 (`ContactAvailability`) |
| **Kim sorumlu / coverage current mı?** | MOD-0151 (territory readiness) |
| **Gidince ne anlatılacak?** | **Bu FU (Knowledge/Content)** |
| **Ziyaret nasıl planlanır, ne gösterildi, ne oldu?** | MOD-0155 (visit/route/detailing/usage) |

**Komşu SoR sınırları (çakışma önleme):**

| Nesne | Sahip | Bu FU'daki rolü |
|---|---|---|
| Binary dosya / controlled document / versiyonlu doküman deposu | **MOD-0028 / MOD-0029** (Platform Content Service — repoda **canlı**) | `FileRef` ile **referans verilir**; bu FU **dosya yükleme/depolama yapmaz** |
| Training completion / mandatory training evidence | **MOD-0309** Learning / Training Records | İçeriği **tüketir**; completion SoR'u burada değildir |
| Customer self-service KB article publishing / feedback / search sync | **MOD-0162** parent (future) | `ContentType=knowledge-article` bu kataloğun bir tipidir; yayın kanalı parent'ta kalır |
| Enterprise search / semantic tagging | **MOD-0056 / MOD-0057** (future) | Yalnız indeksleme/etiketleme **seam**'i; business Subject/Topic taksonomisi burada kalır |
| Campaign / Segment | **MOD-0165 / MOD-0167** | `CampaignId` / `SegmentId` yalnız opsiyonel metadata |
| Brand / Product master | **Ayrı master (MDM/Product — henüz yok)** | Yalnız opsiyonel metadata (§9) |

---

## 3. Authorized Conceptual Model

```text
Subject            (en üst anlatım alanı)
  └── Topic        (hiyerarşik alt konu, parent-child)
AudienceProfile    (kime anlatılacak — generic)
KnowledgeContent   (asıl içerik nesnesi, versioned)
    ├── Optional Pharma Metadata     (brand/product/indication/ATC/specialty/doctor profile…)
    ├── Optional Learning Metadata   (level/skill/objective/prerequisite/duration/assessment…)
    └── Optional Campaign Metadata   (campaign/segment/medical message)
```

Dört nesne de **tenant-scoped**tir; `TenantId` **JWT claim'inden** gelir, request payload'ında **asla** bulunmaz.

> **Sıralama bu modelde yoktur (2026-08-02 kararı).** `KnowledgeContent` **tekil** içeriktir; "önce bunu, sonra
> bunu" zinciri ayrı bir modeldir ve [MOD-0162-FU01A — KnowledgePath / Content
> Sequence](MOD-0162-FU01A-knowledge-path-content-sequence.md) ile yetkilendirilmiştir. `NextContentId` benzeri
> bir alan `KnowledgeContent`'e **eklenmez** (aynı içerik farklı path'lerde farklı sırada geçer).

---

## 4. Subject / Topic Taxonomy Policy

**`Subject`** — en üst anlatım alanı. Örnek: `Pharma` · `Almanca` · `QMS` · `Onboarding` · `Sales Training` ·
`Technical Training` · `Regulatory` · `Product Training` · `CRM Training`.

**`Topic`** — subject'in alt konusu, **hiyerarşik**:

```text
Pharma
  Cardiology
    Hypertension
    Heart Failure
German
  A1
    Greetings
    Numbers
QMS
  SOP
    Document Control
```

| Kural | Karar |
|---|---|
| Subject kimliği | **Unique `SubjectCode`** (tenant içinde) |
| Topic kimliği | **Stabil `TopicCode`**; hiyerarşi `ParentTopicId` ile (parent-child destekli) |
| Rename | Kod **değişmez**; yeniden adlandırma `DisplayName` veya `Alias` üzerinden yapılır → mevcut içerik path'i bozulmaz |
| Alias | Eski ad/kod alias olarak korunur (arama ve geçmiş referanslar için) |
| Hard delete | **Yasak** |
| Archived topic | Yeni içerik **bağlanamaz**; mevcut içerik bağlı kalır ve **history korunur** |
| Döngü | Kendi kendine/ata döngüsü oluşturan `ParentTopicId` → **400** |
| Cross-subject parent | Topic yalnız **kendi subject'i** içinde parent alabilir → aksi **400** |
| Derinlik | Sınırsız değildir; implementation FU'sunda açık bir maksimum derinlik sabitlenir (öneri: 5) |
| Vokabüler | Subject/Topic **tenant-owned business taksonomisidir**; MOD-0057 semantic tagging'in yerine geçmez, onunla çakışmaz |
| Concept graph ile ilişki | Subject/Topic **içerik sınıflandırmasıdır**; subject içindeki kavram tipleri/düğümleri ve beklenen zincir ayrı bir modeldir → [MOD-0162-FU01C](MOD-0162-FU01C-subject-concept-graph-configurable-concept-chain.md). `Indication`/`Need`/`Benefit` gibi kavramlar **Topic ağacına gömülmez** |

---

## 5. KnowledgeContent Policy

Minimum alan sözleşmesi (**yetkilendirildi, implement edilmedi**):

| Alan | Zorunluluk | Not |
|---|---|---|
| `ContentId` | Zorunlu | Aggregate kimliği |
| `TenantId` | Zorunlu | **JWT claim'inden**; payload'da asla |
| `ContentCode` | Zorunlu | Versiyonlar arasında **ortak, stabil** kimlik |
| `Title` · `Description` | Zorunlu / optional | |
| `ContentType` | Zorunlu | §5.1 |
| `SubjectId` · `TopicId` | Zorunlu | Archived subject/topic'e **yeni** içerik bağlanamaz |
| `AudienceProfileId` | Optional | Yoksa içerik **genel** kabul edilir; uydurma profil atanmaz |
| `Language` | Zorunlu | Aynı `ContentCode` altında çok dilli sürümler mümkündür |
| `Version` | Zorunlu | §7 |
| `Status` | Zorunlu | §8 |
| `EffectiveFrom` · `EffectiveTo` | Zorunlu / optional | `EffectiveTo < EffectiveFrom` → **400** |
| `OwnerUserId` / `OwnerTeamId` | Zorunlu (en az biri) | İçerik sahipliği/sorumluluğu |
| `Tags[]` | Optional | Serbest etiket; taksonominin **yerine geçmez** |
| `FileRef` / `Url` / `BodyRef` | En az biri zorunlu | §5.2 |
| `Source` | Zorunlu | `manual` / `campaign` / `legacy-import` / `training` / `external` / `other` |
| `CreatedAt` / `CreatedBy` / `UpdatedAt` / `UpdatedBy` | Zorunlu | Standart audit seti |

### 5.1 `ContentType` (minimum)

```text
presentation · brochure · lesson · faq · clinical-summary · objection-handling · quiz · video ·
pdf · html-detail · sop · training-material · message-script · knowledge-article
```

Vokabüler **MOD-0048 reference set**'i olacaktır (`knowledge-content-type` · `knowledge-content-status` ·
`knowledge-content-source` · `audience-profile-type`); **hardcoded fallback listesi kabul edilmez**, set
yayınlanmadan create/update **fail-closed 400** döner. Publish MOD-0048 operator aksiyonudur (F3).

### 5.2 Payload / dosya boundary (kritik)

| Alan | Anlam | Sahip |
|---|---|---|
| `FileRef` | **MOD-0028/MOD-0029 doküman referansı** (documentId + versionId) | Platform Content Service |
| `Url` | Dış kaynak bağlantısı | — |
| `BodyRef` | Yapılandırılmış gövde referansı (ör. HTML/markdown kayıt anahtarı) | Implementation FU'da netleşir |

**Bu FU dosya yükleme, dosya depolama, önizleme üretimi veya içerik render'ı yapmaz.** Yeni bir binary depo
açılmaz — repoda **canlı** olan Document Management yeniden kullanılır. Aynı dosyanın ikinci bir kopyası
KnowledgeContent içinde tutulmaz.

---

## 6. AudienceProfile Policy

Profil **generic** olmalıdır:

| Yanlış | Doğru |
|---|---|
| `DoctorProfile` (yalnız) | **`AudienceProfile`** |

Örnekler: `Kardiyoloji A segment doktor` · `Eczacı` · `Yeni başlayan çalışan` · `A1 Almanca öğrencisi` ·
`Satış temsilcisi` · `Medical representative` · `Manager` · `Admin user`.

| Kural | Karar |
|---|---|
| Kimlik | `AudienceProfileId` + stabil `ProfileCode` |
| Anlam | Pharma'da doktor profili, eğitimde learner profili gibi çalışır — **tek nesne, iki bağlam** |
| Zorunluluk | İçerikte **optional**; yoksa içerik genel kabul edilir |
| Eşleştirme | Profil ↔ contact/segment/pozisyon eşleştirme **kuralı bu FU'da yazılmaz** (tüketici tarafı — MOD-0155/MOD-0167 boundary) |
| Hard delete | **Yasak**; `archived` profil yeni içeriğe bağlanamaz |

---

## 7. Optional Pharma Metadata

Pharma alanları modelin **merkezinde değil**, opsiyonel metadata olarak yetkilendirilir:

`BrandId` *(optional/future)* · `ProductId` *(optional/future)* · `IndicationId` · `IndicationName` · `ATCCode` ·
`TherapeuticArea` · `Specialty` · `DoctorProfileId` · `CampaignId` · `SegmentId` · `MedicalMessageCode`

Örnek:

```text
Content : Almiba Q1 Doctor Deck
Metadata: Brand=Almiba · Indication=Hipertansiyon · ATCCode=C09AA
          AudienceProfile=Kardiyoloji A segment doktor · Campaign=Almiba Q1
```

**Kurallar:** Brand/Product **zorunlu değildir** · Indication/ATC yalnız pharma içerikte anlamlıdır ·
Brand/Product master yokken alanlar **future optional** kalır · **içerik sistemi Brand/Product master'a bağımlı
başlatılmaz** · master geldiğinde metadata linkage eklenir ve mevcut içerik geçersizleşmez ·
`DoctorProfileId` bir **kısayol değildir**: generic `AudienceProfileId` her zaman birincil alandır.

---

## 8. Optional Learning Metadata

Pharma dışı eğitim/öğrenme senaryoları için:

`LearningLevel` · `Skill` · `LearningObjective` · `PrerequisiteTopicId` · `LessonType` ·
`EstimatedDurationMinutes` · `AssessmentRequired` · `VocabularySetId` · `GrammarTopic` — **hepsi optional**.

```text
Content : German A1 Greetings Lesson
Metadata: Subject=Almanca · Topic=A1/Selamlaşma · LearningLevel=A1 · Skill=Speaking
          LearningObjective=Kendini tanıtabilmek
```

`AssessmentRequired=true` bir **işaret**tir; quiz motoru, puanlama ve completion kaydı bu FU'da **yoktur**
(completion SoR'u **MOD-0309**).

---

## 9. Versioning / Status Policy

### 9.1 Versioning

| Kural | Karar |
|---|---|
| `Version` | **Zorunlu** |
| Çoklu sürüm | Aynı `ContentCode` altında birden fazla version bulunabilir |
| Geçerlilik | `EffectiveFrom` / `EffectiveTo` penceresiyle yönetilir |
| Archived version | **Silinmez** |
| Hard delete | **Yasak** |
| Kullanılabilir sürüm | Yalnız **published + effective** sürüm ziyaret/eğitim sırasında kullanılabilir |
| Evidence | Visit/Training execution ileride **hangi content version gösterildiyse onu** evidence olarak saklamalıdır (tüketici sorumluluğu — MOD-0155 / MOD-0309); bu FU evidence üretmez |
| Çakışma | Aynı `(ContentCode, Language)` için **örtüşen effective window'da iki published sürüm** → kontrollü **409** (sessiz seçim yok) |

### 9.2 Status

```text
draft · review · approved · published · inactive · archived
```

| Kural | Karar |
|---|---|
| Workflow | Bu FU **workflow implementation açmaz**; `review` / `approved` yalnız **future-ready metadata**dır |
| Gerçek approval | MOD-0023 / workflow entegrasyonuna **en sonda** bağlanır (ayrı authorization) |
| MOD-0155'e öneri | **Published olmayan içerik** active recommendation olarak **gitmez** |
| Archived | Yeni kullanım için önerilmez; **history için okunabilir** kalır |
| Geçiş | Her durum geçişi audit'lenir |

---

## 10. Permission Boundary

Canonical öneriler (PKS-001 formatı): `crm.knowledge.content.read` · `crm.knowledge.content.manage` ·
`crm.knowledge.content.publish` · `crm.knowledge.taxonomy.manage`.

**Bu pack hiçbir permission literal'ini kataloğa eklemez, seed/grant yapmaz.** Katalog hazır değilse
implementation FU'sunda MOD-0151 FU08 precedent'i uygulanır: anahtar **tanımlanır ama `All` listesine
eklenmez**, geçici olarak mevcut bir read/manage anahtarına fallback edilir ve `-RBAC` follow-up'ı açılır (F5).
`publish` anahtarı `manage`'den **ayrıdır** (SoD: içerik yazan ≠ yayınlayan).

---

## 11. MOD-0155 Consumer Boundary

MOD-0155 ileride `KnowledgeContent`'i şunlar için tüketir: visit objective için önerilen içerik · campaign content ·
doctor/profile bazlı mesaj · product/brand bazlı materyal · digital detailing sırasında gösterilecek içerik ·
ziyaret sonrası content usage evidence.

**Bu FU bunların hiçbirini yapmaz.** MOD-0155'e ait olanlar: visit plan · route plan · visit execution ·
ziyarette gösterilen içerik · digital detailing · content usage tracking · visit report.

Önerilen tüketim seam'i (route'lar `integration-agent` yetkisindedir, bu pack route açmaz):

```text
GET /api/crm/knowledge-content?subjectId=…&topicId=…&audienceProfileId=…&language=…
    &campaignId=…&effectiveAt=…&status=published
→ yalnız published + effective satırlar (+ Version, ContentCode, FileRef/Url)
```

Katalog **liste döndürür, karar vermez**: sıralama/skor/"en iyi içerik"/ziyaret planı üretmez.

---

## 12. MOD-0165 / MOD-0167 Integration Boundary

```text
Campaign        = Almiba Q1
Target          = Kardiyoloji A segment doktorlar     (MOD-0167)
Frequency       = ayda 2                              (MOD-0165-FU01 VisitFrequencyPolicy)
RecommendedContent = Almiba Q1 Doctor Deck v1.3       (bu FU — yalnız boundary)
```

- Campaign engine, segmentation engine ve frequency policy runtime'ı **bu FU'da yapılmaz**.
- Content selection policy yalnız **boundary olarak** tanımlanır: campaign/segment ileride içerikle
  ilişkilendirilebilir (`CampaignId` / `SegmentId` metadata), ancak **içerik listesi frequency policy'ye
  gömülmez** (MOD-0165-FU01 §12) ve **frequency kuralı KnowledgeContent'e gömülmez** — iki yön de yasaktır.
- İki soru ayrı kalır: *"ne sıklıkla"* (MOD-0165/0167) ve *"ne anlatılacak"* (bu FU); ikisini **MOD-0155**
  birleştirir.

---

## 13. Brand / Product Boundary

- Brand/Product **ayrı master** olarak kalır; bu FU master implementasyonu **yapmaz** ve sahiplik talep etmez.
- Brand/Product `KnowledgeContent` için **optional metadata**dır, merkezi değildir.
- `KnowledgeContent` **pharma dışı** subject'leri (Almanca, QMS, onboarding, teknik/regülasyon eğitimi)
  **birinci sınıf** olarak destekler.
- **Follow-up:** `Brand/Product Master Boundary Pack Authorization` (F2).

---

## 14. Explicit Exclusions

Runtime implementation · file upload implementation · içerik render/preview · arama indeksi · digital detailing ·
visit planning · route planning · visit execution · content usage tracking implementation · campaign engine ·
segmentation engine · frequency policy runtime · Brand/Product master implementation · approval workflow
implementation · MOD-0023 entegrasyonu · e-signature · evidence pack · patient data · Account/Contact mutation ·
ContactAvailability mutation · territory mutation · yeni import/export scope · hard delete · Mongo hand-edit ·
RBAC seed/grant · MOD-0048 publish · registry satırı yazımı · `TenantId` payload'da · doğrudan servis portuna
business API çağrısı.

---

## 15. Contract Flags (öneri — implementation anlamına gelmez)

```json
{
  "supportsKnowledgeContent": true,
  "supportsSubjectTaxonomy": true,
  "supportsAudienceProfile": true,
  "supportsContentVersioning": true,
  "supportsOptionalPharmaMetadata": true,
  "supportsOptionalLearningMetadata": true
}
```

Bu flag'ler **visit planning · digital detailing · workflow approval yapıldığı anlamına gelmez**.
**Eklenmez:** `supportsVisitPlanning` · `supportsRoutePlanning` · `supportsDigitalDetailing` ·
`supportsWorkflowApproval`. MOD-0151 canlı contract'ı ve `supportsWorkflowActivation=false` değişmez.

---

## 16. Acceptance Criteria for Pack Approval

- [x] `KnowledgeContent` **merkezi model** olarak tanımlandı; `BrandContent`/`ProductContent` daraltması reddedildi.
- [x] `Subject` / `Topic` taksonomisi (stabil kod, hiyerarşi, alias/rename, archive, hard-delete yasağı) yazıldı.
- [x] `AudienceProfile` **generic** profil olarak tanımlandı (pharma doktor profili ≠ ayrı nesne).
- [x] Pharma metadata (Brand/Product/Indication/ATC/Specialty/DoctorProfile/Campaign/Segment) **opsiyonel** olarak
      kaybolmadan yazıldı.
- [x] Learning metadata (level/skill/objective/prerequisite/duration/assessment) **opsiyonel** olarak yazıldı;
      Almanca/QMS/onboarding gibi pharma dışı subject'ler desteklendi.
- [x] Versioning + status policy (published/effective, archive, hard-delete yasağı, evidence beklentisi) yazıldı.
- [x] Dosya/doküman sınırı MOD-0028/0029'a, completion sınırı MOD-0309'a bağlandı.
- [x] MOD-0155 consumer ve MOD-0165/0167 integration boundary'leri yazıldı.
- [x] Runtime / digital detailing / visit-route planning / workflow scope'u açılmadı; `runtime_code_allowed: false`.
- [x] Reviewer onayı → `status: approved` (2026-08-09); ardından **implementation FU** (MOD-0162-FU02) ayrı yetkilendirilir.
- [ ] EA kimlik kararı (F1): yetenek MOD-0162 altında mı kalacak, yoksa yatay capability'ye mi taşınacak. *(non-blocking; pack gövdesini değiştirmez)*

---

## 17. Implementation Notes (implementation FU'suna devir)

- Golden Reference kararı implementation FU zamanında verilir: `KnowledgeContent` formu §5'e göre 8 alanın
  üzerindedir → beklenen `golden_reference: compact`, `shell: tenant`. Bu pack'te `none` olması, **hiçbir UI
  yetkilendirilmediği** içindir.
- Aggregate `Diten.CrmService` içinde açılır; **yeni servis yaratılmaz**. Yetenek EA tarafından yatay bir
  platform capability'sine taşınırsa servis kararı yeniden değerlendirilir (F1).
- Yeni CRM aggregate'i eklenirken `RegisterClassMaps` kaydı zorunludur (aksi hâlde Guid FK'lar binary yazılır ve
  filtreler sessizce boş döner — MOD-0151 FU05 dersi).
- İki `DateTimeOffset` alanı (`EffectiveFrom`/`EffectiveTo`) **birlikte index'lenmez/sort edilmez** (CRM
  parallel-array tuzağı); effective window sorguları buna göre tasarlanır.
- Taksonomi ağacı okumaları için materyalize edilmiş path (`TopicPath`) düşünülmelidir; **path kodlardan üretilir**,
  display name'den değil (rename path'i bozmasın).

---

## 18. Follow-up Items

| # | Follow-up | Owner | Neden |
|---|---|---|---|
| F1 | **EA kimlik kararı / olası yatay capability göçü** — `CAND-CAP-0006` gate'i BLOCKED döndü (registry satırı + ledger yok); yetenek MOD-0162'nin Service-KB kapsamından geniştir | EA / registry owner | Kimlik göçü pack gövdesini değiştirmez ama servis/domain kararını etkiler |
| F2 | **`Brand/Product Master Boundary Pack Authorization`** — ✅ **KAPATILDI 2026-08-02** → [MOD-0290-FU01 — Brand / Product Master Boundary](../../master-data-management/module-packs/MOD-0290-FU01-brand-product-master-boundary.md) (SoR = **MOD-0290**, MDM) | EA / MDM + commercial-suite | Brand/Product optional metadata kaldı (§13); master CRM'de değil MDM'de |
| F3 | **MOD-0048 knowledge reference set authoring + publish** (`knowledge-content-type` / `-status` / `-source` / `audience-profile-type`) | MOD-0048 operator | Hardcoded enum yasağı; implementation runtime prereq'i |
| F4 | **`MOD-0162-FU02 — Knowledge Content & Taxonomy Implementation`** (aggregate + CRUD + versioning + UI + tests) | commercial-suite | Bu pack'in runtime devamı |
| F5 | **`MOD-0162-FU01-RBAC — Knowledge/Content Permission Catalog Alignment`** | MOD-0018 / commercial-suite | §10 anahtarları katalog + grant gerektirir |
| F6 | **Content ↔ Document Management linkage sözleşmesi** (`FileRef` = documentId + versionId; MOD-0029 controlled doc versiyon kilidi) | MOD-0028/0029 + commercial-suite | Dosya SoR'u Platform'da; çift kopya yasağı sözleşmeye bağlanmalı |
| F7 | **`MOD-0309` Learning/Training Records tüketim boundary'si** (completion + mandatory training expiry) | HCM | Eğitim içeriği burada, completion orada |
| F8 | **Content approval workflow (MOD-0023) entegrasyonu** — `review`/`approved` bugün yalnız metadata | commercial-suite + MOD-0023 | En sona bırakıldı (kullanıcı direktifi) |
| F9 | **Enterprise search / semantic tagging seam** (MOD-0056 / MOD-0057) | Data & Knowledge Plane | Taksonomi ↔ tagging çakışmasını önlemek için |
| F10 | **Content sequence / anlatım akışı** — ✅ **KAPATILDI 2026-08-02** → [MOD-0162-FU01A — KnowledgePath / Content Sequence](MOD-0162-FU01A-knowledge-path-content-sequence.md) | commercial-suite | Tekil içerik ≠ içerik zinciri; `NextContentId` gömme yasağı orada sabitlendi |
| F11 | **Multi-visit/session progression** — ✅ **KAPATILDI 2026-08-02** → [MOD-0162-FU01B — EngagementJourney](MOD-0162-FU01B-engagement-journey-multi-visit-content-progression.md) | commercial-suite | Tek oturum sırası ≠ çok oturumlu aşama zinciri |
| F12 | **Subject concept chain (`Indication → Profile → Need → Benefit` genelleştirmesi)** — ✅ **KAPATILDI 2026-08-02** → [MOD-0162-FU01C — Subject Concept Graph](MOD-0162-FU01C-subject-concept-graph-configurable-concept-chain.md) | commercial-suite | Pharma zinciri hardcoded şema değil, konfigüre edilebilir template olarak korundu |

---

## 19. Next Recommended Prompt

1. **`Brand/Product Master Boundary Pack Authorization`**
2. **`MOD-0162-FU02 — Knowledge Content & Taxonomy Implementation`** — yalnız §4–§9 sözleşmesi; digital detailing,
   visit/route planning ve workflow approval **açılmaz**.
