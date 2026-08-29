---
id: MOD-0162-FU04
name: KnowledgePath Runtime + UI
parent: MOD-0162
parent_name: Knowledge Base
implements_boundary: MOD-0162-FU01A
siblings: MOD-0162-FU01, MOD-0162-FU01A, MOD-0162-FU01B, MOD-0162-FU01C, MOD-0162-FU02, MOD-0162-FU03
domain: commercial-suite
service: Diten.CrmService + frontend/Diten.Web
shell: tenant
golden_reference: compact
entity_base: EntityBase
status: done
runtime_code_allowed: true
runtime_code_scope: "NONE (draft). Kullanıcı onayı ile `ready-for-dev` + `runtime_code_allowed: true` olduğunda kapsam: KnowledgePath aggregate root runtime — adımlar path DOKÜMANI İÇİNDE embedded (D2) — (CRUD-minus-delete + archive + effective dating + path versiyonlama + published adım-seti dondurma + prerequisite/sıra doğrulaması + in-domain vokabüler + contract) `Diten.CrmService` içinde VE CRM Admin → Knowledge → Paths TEK Compact sayfası (gömülü adım alt-editörü + adım-içi BranchCondition repeater) `frontend/Diten.Web` içinde. Branch evaluator, recommendation / best-next-content, completion / progress engine, AI personalization, digital detailing, visit/route planning, MOD-0155 & MOD-0309 mutation, FU02 `KnowledgeContent` alan/imza değişikliği, FU03 concept aggregate mutation, MDM write, Gateway config değişikliği, RBAC seed/grant, MOD-0048 publish, registry write ve Mongo hand-edit YASAKTIR."
owner: module-pack-author
branch: feature/crm/mod-0162-fu04-knowledge-path-runtime-ui
started: 2026-08-26
target: TBD (kullanıcı onayı sonrası)
form_field_count: 12   # KnowledgePath = TEK golden-reference yüzeyi; kullanıcı-form alanı sayımı §11.1'de türetilir (12 > 8 → Compact). Gömülü adım alt-editörü ayrı yüzey DEĞİLDİR (D2/D3).
dependencies:
  - MOD-0162-FU01A (KnowledgePath / Content Sequence boundary — APPROVED 2026-08-09; §3–§13 sözleşmesi BURADA implement edilir)
  - MOD-0162-FU02 (KnowledgeContent + Subject/Topic/AudienceProfile runtime — SHIPPED; hard prerequisite, sözleşmesi KIRILMAZ)
  - MOD-0162-FU03 (Concept Graph runtime — DONE; adım → ConceptNode referansı BURADA tüketilir, concept aggregate'leri MUTATE EDİLMEZ)
  - MOD-0162-FU01B (EngagementJourney boundary — BU FU'DA IMPLEMENT EDİLMEZ; journey path'e referans verir, adımlarını kopyalamaz)
  - MOD-0162-FU01C (Subject Concept Graph boundary — okunur, değiştirilmez)
  - MOD-0048 (reference data — knowledge-path-step-type / -status / -completion-rule / -source; publish AYRI operatör işi, dev'de in-domain fail-closed)
  - MOD-0155 (consumer — read-only tüketim seam'i; bu FU visit/route planlamaz)
  - MOD-0309 (consumer — completion/score/attendance SoR; bu FU ölçmez)
  - MOD-0028 / MOD-0029 (file SoR — adım dosya taşımaz, yalnız FU02 içeriğine referans verir)
  - MOD-0018 (RBAC — yalnız tüketim; seed/grant bu pack'te YOK, en sona bırakıldı)
  - DEV-0001 (Golden Reference Compact — tek yüzey, tek klasör)
---

# MOD-0162-FU04 — KnowledgePath Runtime + UI

> **⛔ DRAFT — KOD YETKİSİ YOKTUR (`runtime_code_allowed: false`).**
> Bu pack, MOD-0162-FU01A **boundary**'sinin (approved, 2026-08-09) **implementation** karşılığıdır. AGENTS.md onay
> kapısı gereği `draft` pack yalnızca planlama dokümanıdır; `@orchestrator` bu pack ile kod yazamaz.
>
> **📌 REVİZYON 2026-08-26 — D1–D7 KULLANICI KARARLARI UYGULANDI (§"Açık Kararlar" = kapandı).**
> En büyük yapısal etki **D2 = EMBEDDED**: `KnowledgePathStep` artık **ayrı aggregate değil**, `KnowledgePath`
> dokümanı içinde **embedded entity listesi**dir. Sonuç olarak pack **tek collection · tek proxy controller ·
> TEK Compact sayfa**'ya indirgendi; ikinci klasör, ikinci DataTable, cross-collection cascade ve çok-doküman
> transaction makinesi **kaldırıldı**. `status` **`draft` kalır** — `ready-for-dev` flip'i ayrı kullanıcı
> aksiyonudur.
>
> **Desen:** FU01 (boundary) → **FU02** (implementation) ve FU01C (boundary) → **FU03** (implementation) ile birebir
> aynı. FU01A §15 *"reviewer onayı → ardından KnowledgePath implementation FU'su ayrı yetkilendirilir"* der;
> FU03 §18/F-PATH bu FU'yu adıyla ister. Bu dosya odur.
>
> **DCP-002 kimlik kapısı — PASS (2026-08-26):**
> `py .antigravity/scripts/verify_module_id.py . --check-id MOD-0162-FU04 --name "KnowledgePath Runtime + UI" --parent MOD-0162`
> → `OK  MOD-0162-FU04: proven against Blueprint/registry.` (**exit 0**).
>
> Otorite sırası: **Blueprint Excel** > MOD-0162-FU01A (approved boundary) > bu pack >
> [Domain Config](../domain-config.md) > `AGENTS.md` > `.antigravity/rules/`.

---

## 1. Module Summary

FU02 **tekil içeriği**, FU03 **kavramların nasıl bağlandığını** kapattı. Açık kalan tek soru sıradır:
**"hangi içerik önce anlatılacak / öğrenilecek / gösterilecek?"** Bu FU onu — ve yalnız onu — çalışır hâle getirir.

```text
KnowledgeContent    = tekil içerik                                   (FU02, mevcut)
ConceptNode/Graph   = kavramlar nasıl bağlanır                       (FU03, mevcut)
KnowledgePath       = içeriklerin anlatım / öğrenme sıralama zinciri (BU FU — aggregate root)
  └── Steps[]       = path DOKÜMANI İÇİNDE embedded adım listesi     (BU FU — D2, ayrı aggregate DEĞİL)
        └── BranchConditions[] = adım içinde embedded repeater        (BU FU — D7, veri-only)
```

| Cevapladığı soru | Sahip |
|---|---|
| Bu path hangi subject/topic/audience için, hangi sürümü geçerli, ne zaman geçerli? | **Bu FU** |
| Adımlar hangi sırada, hangisi zorunlu, hangisi hangisinden sonra gelmeli? | **Bu FU** |
| Adım hangi içeriği gösterir, hangi içerik sürümüne sabitlenmiştir? | **Bu FU** |
| Adım hangi kavramı öğretir (FU03 node referansı)? | **Bu FU** (referans; node MUTATE EDİLMEZ) |

| Cevaplamadığı soru | Sahip |
|---|---|
| Kavramlar birbirine nasıl bağlanır | MOD-0162-FU03 |
| Hangi ziyarette / çok-oturumlu aşama | MOD-0162-FU01B (EngagementJourney) |
| **Sonraki en iyi içerik hangisi / dallanma nasıl değerlendirilir** | **YOK** — F-DETAIL (Digital Detailing) |
| Tamamlandı mı, kim tamamladı, ne skor aldı | MOD-0309 |
| Ne sıklıkla / kime | MOD-0165 / MOD-0167 |
| Kime, ne zaman gidilecek; gösterim kaydı | MOD-0155 |
| Dosya / binary / controlled document | MOD-0028 / MOD-0029 |

**Temel mimari kural (FU01A §2):** *path bir **şablon**dur, bir **çalıştırma** değildir.* Bir adımın gerçekten
gösterildiği, tamamlandığı veya atlandığı bilgisi bu FU'da **yoktur**.

**Reddedilen üç model (FU01A §1 — burada da geçerli):** `KnowledgeContent.NextContentId` · `BrandContentFlow` ·
MOD-0155 `VisitPlan` içine gömülmüş hardcoded content sırası.

---

## 2. Ownership and Boundaries

**In-scope:** `KnowledgePath` aggregate root'u ve **içindeki embedded `Steps[]`** · CRUD-minus-delete
(create/read/update/archive, **DELETE ve PATCH yok**) · path versiyonlama + published sürümde adım setinin
dondurulması · effective dating · `StepOrder` / `PrerequisiteStepId` / required↔optional zincir doğrulaması ·
`VersionPinPolicy` ile deterministik içerik sürümü çözümleme · FU02 içeriğine ve FU03 concept node'una
**read-only referans** · in-domain vokabüler + contract endpoint · read-only tüketim seam'i
(`IKnowledgePathReader`) · CRM Admin **tek** Compact sayfa · 7 dil RESX.

**Out-of-scope (§14 tam liste):** branch evaluator · recommendation / best-next-content · completion / progress
engine · AI personalization · digital detailing · content usage tracking · visit/route planning · quiz motoru ·
MOD-0023 approval workflow · file upload/render/preview · Campaign/Consent/Account/Contact/territory mutation ·
MDM write · hard delete.

### 2.1 Boundary'den sapma kararları (kullanıcı prompt'u / FU01A ↔ bu pack)

Boundary `approved` olduğu için **boundary kazanır**; sapmalar burada gerekçelenir (FU03 §2 deseni).

| # | Kaynak ifade | Bu pack (kazanan) | Gerekçe / durum |
|---|---|---|---|
| **S1** | Kullanıcı görevi: *"Step'ler KnowledgeContent **ve/veya** ConceptNode referansı tutar"* | `ContentId` **ZORUNLU** kalır; `ConceptNodeId` **opsiyonel ek** referanstır | FU01A §4: `ContentId` Zorunlu. "İçeriksiz, yalnız-kavram adımı" boundary'de yok; açmak sıralama nesnesini kavram nesnesine dönüştürür (FU03 ile çakışır). Gerekirse **boundary değişikliği** ister → F-CONCEPT-STEP |
| **S2** | FU01A §3: alan adı `Version` | Alan adı **`PathVersion`** | `entity-base-template.md` + module-pack-standard §14: `Version` teknik concurrency token'ı için **rezerve**; iş versiyonu bu adı alamaz. FU02 emsali: `ContentVersion` ≠ `EntityBase.Version` |
| **S3** | FU01A §10 örnek route: `/api/crm/knowledge-paths` | Route **`/api/crm/knowledge/paths`** | Gateway'de `/api/crm/knowledge/{everything}` wildcard'ı **zaten var** (`ocelot.json:2245`, GET/POST/PUT/OPTIONS); `/api/crm/knowledge-paths` **wildcard'ın dışına düşer** ve `ocelot.json` değişikliği gerektirirdi — o dosya protected. FU01A §10 zaten *"route'lar integration-agent yetkisindedir, bu pack route açmaz"* der |
| **S4** | FU01A §4: adım tablosunda lifecycle alanı **yok** | Embedded adım **`StepStatus`** (`active` / `archived`) + `ArchivedAt`/`ArchivedBy` **kazanır** | FU01A §6 hard delete'i **yasaklar**; lifecycle alanı olmayan bir gömülü nesne yalnız diziden **silinerek** kaldırılabilirdi — bu de-facto hard delete olurdu. Archive = tek kaldırma yolu; archived adım sıralamadan ve tüketimden düşer, **aynı dokümanda** history olarak kalır |
| **S5** | ~~Ayrı aggregate önerisi~~ | ✅ **SAPMA DEĞİL — boundary önerisi benimsendi.** `KnowledgePathStep` = `KnowledgePath` içinde **embedded** entity | **D2 = EMBEDDED (kullanıcı kararı 2026-08-26)**. FU01A §16'nın *"öneri: tek aggregate (path root + step child); çünkü published sürümde adım seti dondurulur ve adımlar path'ten bağımsız yaşamaz"* ifadesi **birebir uygulanır**. "Adım path sürümüne aittir" invariant'ı artık **validasyonla değil, yapısal olarak** garanti edilir |
| **S6** | FU01A §5.1: `assessment-passed` için *"içerikte `AssessmentRequired=true` olmalı"* | ✅ **ÇÖZÜLDÜ (D6 = A)** — kural, referanslanan içeriğin **`ContentType == "quiz"`** olmasıyla doğrulanır; aksi **400** | FU02'de `AssessmentRequired` alanı **yok** (kod teyidi: `Domain/Entities/KnowledgeContent.cs`), ama `KnowledgeContentTypes.Quiz = "quiz"` **mevcut** ve in-domain tek değerlendirme tipidir. **FU02'ye alan EKLENMEZ.** Quiz dışı bir değerlendirme kavramı gerekirse → F-ASSESS |

### 2.2 FU02 sözleşme koruması (kırmızı çizgi)

- **AC-FU02-1** `KnowledgeContent` **atomik** kalır — pakete/zincire dönüştürülmez, path dokümanına **kopyalanmaz**
  (adım yalnız `ContentId` + provenance `ContentCode` taşır).
- **AC-FU02-2** `IKnowledgeContentLinkageReader.ResolvePublishedContentAsync(...)` **imzası ve davranışı değişmez**;
  Campaign (MOD-0165-FU04) tüketimi kırılmaz. FU04'ün `latest-published` çözümlemesi bu seam'i **genişletmez** —
  kendi read-only resolver'ı `IKnowledgeContentRepository` üzerinden `IsConsumableAt(...)` uygular.
- **AC-FU02-3** FU02 alanları (`ContentCode`, `ContentVersion`, `ContentStatus`, `ConceptNodeId`, `ContentType`, …)
  **kaldırılmaz, yeniden adlandırılmaz, yenisi eklenmez** (D6 dâhil); FU02 endpoint'leri ve view'ları değişmez.
- **AC-FU02-4** Tüm değişiklik **additive**: **1** yeni collection + **1** yeni controller ailesi + 1 contract
  endpoint + **1** yeni view klasörü.

### 2.3 FU03 sözleşme koruması (kırmızı çizgi)

- `ConceptType` / `ConceptNode` / `ConceptRelationship` / `ConceptChainTemplate` / `KnowledgeContentConceptLink`
  **okunur, yazılmaz**. FU04 hiçbir concept aggregate'ine alan eklemez, hiçbirini archive/update etmez.
- FU03 contract'ının **12 flag'i ve 9 yasak-flag disiplini değişmez**; FU04 kendi contract'ını **ayrı** endpoint'te
  yayınlar.
- Embedded adımın `ConceptNodeId`'si **FU03 `ConceptNode.Id`'ye** işaret eder — `KnowledgeContentConceptLink`'in
  yerine geçmez, onu çoğaltmaz. (İçerik↔kavram bağı FU03'ün; **adım**↔kavram bağı bu FU'nun.)
- FU03 `/concept-graph` **motor değildir** ve FU04 onu traversal için çağırmaz; yalnız node **doğrulama/etiket**
  okuması yapar (`GET /concept-nodes/{id}`).

---

## Açık Kararlar — D1–D7 ✅ **KAPANDI (kullanıcı kararı, 2026-08-26)**

Pack gövdesi aşağıdaki yedi karara göre **revize edildi**; açık madde kalmadı.

| # | Konu | **KARAR (kapandı)** | Pack'e yansıması |
|---|---|---|---|
| **D1** | Bu FU'nun kimliği: `MOD-0162-FU04` mü, `MOD-0162-FU01A-IMPL` mi? | ✅ **FU04** — FU01→FU02 ve FU01C→FU03 emsali; DCP-002 gate PASS (exit 0) | Frontmatter aynen korundu |
| **D2** | `KnowledgePathStep`: ayrı aggregate mi, **embedded child** mı? | ✅ **EMBEDDED** — FU01A §16 önerisi benimsendi (§2.1/S5) | **Tek collection** (`knowledge_paths`) · adım = `KnowledgePath.Steps[]` · **tek** `EntityBase.Version` (path'te) · step repository/controller/DataTable **YOK** · cross-collection cascade ve çok-doküman transaction makinesi **KALDIRILDI** (§4.2, §4.5, §5, §8, §10) |
| **D3** | UI: iki Compact sayfa mı, **tek** Compact sayfa mı? | ✅ **TEK Compact sayfa** — `/CRM/KnowledgePaths` + gömülü adım alt-editörü + adım-içi BranchCondition repeater | **Tek klasör**, kanonik **9 dosyalık** Compact seti (§11.2); ikinci klasör/RESX/JS seti **yok**; verifier `--reference compact` **tek klasörde bir kez** koşar |
| **D4** | Publish: ayrı endpoint mi, `Update` içinde status geçişi mi? | ✅ **Ayrı endpoint** — `POST /paths/{id}/publish` + `crm.knowledge.path.publish` (SoD: yazan ≠ yayınlayan, FU01A §9) | §8.1 endpoint + §14 izin + **V-P12**: `Update` ile `published`'a geçiş → **400** |
| **D5** | `new-version` açılsın mı? | ✅ **Evet** — `POST /paths/{id}/new-version` | Kopya **`draft`** doğar · `PathVersion` **artar** · embedded adımlar **yeni `StepId`** ile kopyalanır · **otomatik publish yok** · `SupersedesPathId` provenance (§8.1, V-P14, AC-FREEZE-1) |
| **D6** | `assessment-passed` doğrulamasının dayanağı | ✅ **A** — referanslanan içeriğin **`ContentType == "quiz"`** olması şartı (in-domain; `KnowledgeContentTypes.Quiz` FU02'de **mevcut**, teyit edildi); değilse **400**. **FU02'ye alan EKLENMEZ** | **V-S12** aktif; §17.2 küme 10'da test edilir; artık kısmen-karşılanan boşluk değil |
| **D7** | `BranchCondition` authorable mı? | ✅ **Authorable, veri-only** — `ConditionCode` + `Description` + `TargetStepId?`; **evaluator YOK** | Adım alt-editörü içinde **repeater** (§11.2) · `TargetStepId` aynı path'te olmalı → **V-S14** aksi **400** · contract'ta `supportsFutureBranchingMetadata: true`, `supportsBranchEvaluator` **absent** |

---

## 3. Owned Objects

| Nesne | Tip | Sahiplik |
|---|---|---|
| `KnowledgePath` | **Aggregate root** (`EntityBase`) — tek collection | **Bu FU** |
| `KnowledgePathStep` | **Embedded entity** (`KnowledgePath.Steps[]`) — aggregate **değil**, kendi `EntityBase`'i **yok** (D2) | **Bu FU** |
| `KnowledgePathBranchCondition` | Embedded value object (`Steps[].BranchConditions[]`) — D7 | **Bu FU** |
| `KnowledgePathStatuses` / `Sources` / `StepTypes` / `CompletionRules` / `VersionPinPolicies` / `StepStatuses` | In-domain vokabüler (static class) | **Bu FU** |
| `KnowledgePathReasonCodes` | Reason code kataloğu | **Bu FU** |
| `IKnowledgePathReader` | Read-only tüketim seam'i (MOD-0155 / MOD-0309 için) | **Bu FU** |
| `KnowledgePathContract` | Contract endpoint DTO'ları + flag'ler + vokabüler + limitler | **Bu FU** |
| Commands / Queries / Handlers / Validators / **tek** repository | §10 | **Bu FU** |
| `/CRM/KnowledgePaths` | **Tek** frontend proxy controller + **tek** Compact view seti | **Bu FU** |
| `crm.knowledge.path.{read,manage,publish}` | **TANIM ONLY** (seed/grant YOK) | **Bu FU** |

**Sahiplenilmeyen (yalnız referans/okuma):** `KnowledgeContent` · `Subject` · `Topic` · `AudienceProfile` (FU02) ·
`ConceptNode` ve tüm concept aggregate'leri (FU03) · `Campaign` (MOD-0165) · `EngagementJourney` (FU01B — açılmadı) ·
completion/score (MOD-0309) · visit/route (MOD-0155) · dosya/binary (MOD-0028/0029) · reference data (MOD-0048).

---

## 4. Entity Fields

Ortak kurallar: `TenantId` **JWT claim'inden** (payload'da asla — gönderilirse sessizce yok sayılır) ·
`CreatedAt/By` + `UpdatedAt/By` zorunlu · `EntityBase.Version` **teknik concurrency token**'dır, iş versiyonu
değildir (§2.1/S2) · **hard delete YOK** · archived kayıt update kabul etmez (**409**) ·
`EffectiveTo < EffectiveFrom` → **400** · iki `DateTimeOffset` alanı **birlikte index'lenmez/sort edilmez**
(CRM parallel-array tuzağı).

### 4.1 `KnowledgePath` (aggregate root)

| Alan | Tip | Zorunlu | Kural |
|---|---|---|---|
| `Id` (PathId) | `Guid` | ✔ | `EntityBase.Id` |
| `TenantId` | `Guid` | ✔ | claim; DTO'da **yok** |
| `PathCode` | `string` | ✔ | Sürümler arası **stabil** iş anahtarı; rename edilmez (rename `PathName`'den yapılır) |
| `PathName` | `string` | ✔ | max 200 |
| `Description` | `string?` | ✖ | max 2000 |
| `SubjectId` | `Guid` | ✔ | FU02 Subject; archived subject → **400** |
| `TopicId` | `Guid?` | ✖ | Verilirse `SubjectId`'ye ait olmalı → aksi **400** |
| `AudienceProfileId` | `Guid?` | ✖ | Yoksa path **genel**; uydurma profil atanmaz |
| `Objective` | `string` | ✔ | max 500 — path'in amacı (FU01A §3) |
| `LanguageCode` | `string?` | ✖ | Yoksa adımların içerik dili belirleyicidir; karışık dilli path **görünür** olur (`IsMixedLanguage`) |
| `PathVersion` | `string` | ✔ | **İş versiyonu** (§2.1/S2); aynı `PathCode` altında çoklu sürüm |
| `PathStatus` | `string` | ✔ | `draft` · `review` · `approved` · `published` · `inactive` · `archived` — in-domain fail-closed |
| `EffectiveFrom` | `DateTimeOffset` | ✔ | |
| `EffectiveTo` | `DateTimeOffset?` | ✖ | null = açık uçlu |
| `Source` | `string` | ✔ | `manual` · `campaign` · `training` · `legacy-import` · `external` · `other` |
| **`Steps`** | **`List<KnowledgePathStep>`** | ✔ (boş olabilir) | **EMBEDDED adım listesi (D2)** — §4.2; ayrı collection **yok** |
| `StepSetFrozenAt` | `DateTimeOffset?` | ✖ (türetilmiş) | Publish anında set edilir; §7.1 dondurma kanıtı |
| `PublishedAt` / `PublishedBy` | `DateTimeOffset?` / `string?` | ✖ | Publish audit'i (D4) |
| `SupersedesPathId` | `Guid?` | ✖ | `new-version` ile üretilen sürümün kaynağı (D5); provenance, zincir motoru değil |
| `ArchivedAt` / `ArchivedBy` | `DateTimeOffset?` / `string?` | ✖ | Soft lifecycle |
| `CreatedAt/By` · `UpdatedAt/By` | | ✔ | Standart audit seti |
| `Version` (`EntityBase`) | `int/long` | ✔ | **Tek** optimistic concurrency token — **adım düzenlemeleri de bunu artırır** (D2 kazancı) |

**Türetilmiş (persist edilmez, response'ta görünür):** `ActiveStepCount` · `RequiredStepCount` ·
`IsMixedLanguage` · `IsMixedSubject` · `HasUnresolvedStepContent`.

**Pharma alanı yok:** Brand/Product/Indication/ATC/Campaign path seviyesinde **tanımlanmaz** — bunlar FU02
içeriğinin opsiyonel metadata'sıdır (FU01A §3). Path **hiçbir pharma alanını zorunlu kılmaz**; Almanca dersi,
SOP/QMS eğitimi ve onboarding akışları birinci sınıf vatandaştır.

### 4.2 `KnowledgePathStep` — **embedded entity** (`KnowledgePath.Steps[]`, D2)

> **Aggregate değildir.** Kendi collection'ı, kendi `TenantId`'si, kendi `EntityBase.Version`'ı ve kendi
> repository'si **yoktur**; `PathId` alanı da **yoktur** (adım zaten path dokümanının içindedir). Tüm adım
> yazımları **path root'u üzerinden**, **tek doküman** yazımı olarak yapılır.

| Alan | Tip | Zorunlu | Kural |
|---|---|---|---|
| `StepId` | `Guid` | ✔ | Doküman içinde üretilir; path içinde unique; `new-version` kopyasında **yeniden üretilir** (D5) |
| `StepOrder` | `int` | ✔ | Path içinde **unique** (active adımlar arası) → aksi **409**; boşluk serbest (10/20/30 önerilir) |
| `StepCode` | `string` | ✔ | Path içinde stabil, makine-okunur; sıra eşitliğinde **tie-break anahtarı** |
| `StepTitle` | `string` | ✔ | İçerik başlığından bağımsız olabilir (aynı içerik farklı path'te farklı çerçevelenir) |
| `StepType` | `string` | ✔ | §4.4 vokabüleri (19 değer) — in-domain fail-closed |
| `ContentId` | `Guid` | ✔ | **published + effective** `KnowledgeContent` (§2.1/S1); archived içerik → yeni adımda **400** |
| `ContentCode` | `string` | ✔ (türetilir) | Yazımda içerikten kopyalanır; `latest-published` çözümlemesinin anahtarı + provenance. **İçeriğin kendisi kopyalanmaz** |
| `VersionPinPolicy` | `string` | ✔ | `pinned` (**varsayılan**) · `latest-published` — §8.3 |
| `IsRequired` | `bool` | ✔ | `true` = zorunlu adım |
| `CompletionRule` | `string` | ✔ | `none` · `viewed` · `acknowledged` · `assessment-passed` · `duration-met` — **beyandır, motor değildir** |
| `PrerequisiteStepId` | `Guid?` | ✖ | **Aynı path içinde** + **daha küçük `StepOrder`** + döngü yasak → aksi **400** |
| `ConceptNodeId` | `Guid?` | ✖ | FU03 `ConceptNode.Id`; canlı + archived-değil + aynı tenant → aksi **400**. **Node MUTATE EDİLMEZ** |
| `EstimatedDurationMinutes` | `int?` | ✖ | 1–600; `duration-met` ise **zorunlu** → aksi **400** |
| `Notes` | `string?` | ✖ | max 2000 |
| `BranchConditions` | `List<KnowledgePathBranchCondition>` | ✖ | §4.3 — **değerlendirilmez** (D7); adım başına **max 20** |
| `StepStatus` | `string` | ✔ | `active` · `archived` (§2.1/S4) — form alanı değil, adım archive aksiyonuyla değişir |
| `ArchivedAt` / `ArchivedBy` | `DateTimeOffset?` / `string?` | ✖ | Archived adım **diziden silinmez**, dokümanda kalır |
| `CreatedAt/By` · `UpdatedAt/By` | | ✔ | Adım-seviyesi audit (aynı doküman içinde) |

**Türetilmiş (persist edilmez, response'ta görünür — sessiz çözümleme yasak):**
`ResolvedContentId` · `ResolvedContentVersion` · `ResolvedContentTitle` · `ContentResolutionStatus`
(`pinned` · `resolved-latest` · **`unresolved`**) · `IsCrossSubjectStep` · `IsCrossLanguageStep` ·
`ConceptNodeCode` / `ConceptNodeName` (yalnız etiket okuması).

**Doküman büyüme sınırı (embedded model gereği):** path başına **max 200 adım**, adım başına **max 20 branch
condition** → aşımda **400** (V-S20). Gerekçe: Mongo 16MB doküman limiti + tek-sayfa editör okunabilirliği.
Limit contract'ın `limitations` listesinde **yayınlanır** (sürpriz yok).

### 4.3 `KnowledgePathBranchCondition` (embedded repeater — D7: authorable, veri-only)

| Alan | Tip | Zorunlu | Kural |
|---|---|---|---|
| `ConditionCode` | `string` | ✔ | Serbest kod (ör. `asks-clinical-evidence`, `price-objection`, `low-quiz-score`) |
| `Description` | `string?` | ✖ | max 500 |
| `TargetStepId` | `Guid?` | ✖ | **Aynı path'te** olmalı → aksi **400** (V-S14; referansel akıl sağlığı, **yorum yok**) |

**Zorunlu invariant (FU01A §8):** bir path, branch condition **olmadan da baştan sona yürünebilir** olmalıdır —
lineer geçiş eksiksizdir. Bu FU koşulları **veri olarak** taşır ve tüketiciye **veri olarak** geçirir;
**hiçbir dal değerlendirilmez** (`supportsBranchEvaluator` contract'ta **absent**).

### 4.4 In-domain vokabüler (FU02/FU03 deseni — MOD-0048 publish'ine bağımlı DEĞİL)

```text
KnowledgePathStatuses       : draft · review · approved · published · inactive · archived
KnowledgePathSources        : manual · campaign · training · legacy-import · external · other
KnowledgePathStepStatuses   : active · archived
KnowledgePathVersionPin     : pinned · latest-published
KnowledgePathCompletionRules: none · viewed · acknowledged · assessment-passed · duration-met
KnowledgePathStepTypes      : intro · core-message · clinical-evidence · indication · brand-message ·
                              objection-handling · faq · practice · quiz · assignment · summary · closing ·
                              lesson · vocabulary · grammar · listening · speaking · reading · homework   (19)
```

Doğrulama **in-domain fail-closed**: set dışı değer → **400**. Runtime **hiçbir zaman** yayınlanmamış bir MOD-0048
seti yüzünden fail-open olmaz (MOD-0164-FU02 sapması tekrar etmez). MOD-0048 publish'i
(`knowledge-path-step-type` / `-status` / `-completion-rule` / `-source`) **ayrı operatör işidir** → F-RD; setler
bu vokabülerin **aynısıyla** yayınlanır. `review`/`approved` bugün **yalnız metadata**; gerçek approval MOD-0023'e
en sonda bağlanır (F-WF).

**D6 bağlantısı:** `assessment-passed` doğrulaması FU02'nin **mevcut** `KnowledgeContentTypes.Quiz = "quiz"`
değerini okur — yeni vokabüler **açılmaz**, FU02'ye alan **eklenmez**.

### 4.5 Mongo / persistence kararı — **TEK collection** (D2)

| Collection | Index | Not |
|---|---|---|
| `knowledge_paths` | `(TenantId, PathCode, PathVersion)` unique, **partial**: archived hariç | Partial filter'da **`$ne` YASAK** → `Filter.Type(...)` / `$lt` deseni (Platform crash-loop dersi) |
| `knowledge_paths` | `(TenantId, SubjectId, PathStatus)` | Liste yolu |
| `knowledge_paths` | `(TenantId, Steps.ContentId)` *(multikey, opsiyonel)* | "Bu içerik hangi path'lerde kullanılıyor?" okuması için; **tek** DateTimeOffset içermez |
| ~~`knowledge_path_steps`~~ | — | **YOK** (D2: ikinci collection kaldırıldı) |
| **Yasak** | `(EffectiveFrom, EffectiveTo)` bileşik index / iki-`DateTimeOffset` sort | Parallel-array 500 tuzağı → gerekirse **in-memory sort** |

**Yazma modeli:** adım ekleme/güncelleme/arşivleme **tek doküman** yazımıdır (path root'u
`EntityBase.Version` kontrolüyle replace edilir). Bu nedenle:
- **Çok-doküman transaction / `SupportsTransactionsAsync` guard'ı / compensation makinesi GEREKMEZ** — dev
  standalone Mongo riski **yapısal olarak yok** (D2 kazancı).
- **Cross-collection cascade GEREKMEZ** — path archive edildiğinde adımlar zaten aynı dokümandadır.
- ⚠️ **Dizi-içi unique index YOK:** Mongo bir dizi içindeki `StepOrder`/`StepCode` tekilliğini index ile
  zorlayamaz → **tek savunma hattı handler + validator'dır** (§12), bu yüzden §17.2'de bu kurallar **zorunlu
  testtir** (DB ikinci savunma hattı yoktur).

`RegisterClassMaps`'e **`KnowledgePath` ile birlikte embedded tipler de** (`KnowledgePathStep`,
`KnowledgePathBranchCondition`) kaydedilmelidir; aksi hâlde gömülü `Guid` alanları (`ContentId`,
`ConceptNodeId`, `PrerequisiteStepId`, `StepId`) binary yazılır ve filtreler **sessizce boş döner**
(MOD-0151 FU05 / `AccountTerritoryAssignment` dersi).

---

## 5. Repo Scope

```text
# --- backend (TEK aggregate, TEK repository) ---
services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/KnowledgePath.cs                       (yeni; embedded KnowledgePathStep + KnowledgePathBranchCondition + vokabüler + reason-code static class'ları)
services/Diten.CrmService/src/Diten.CrmService.Domain/Repositories/IKnowledgePathRepository.cs        (yeni — TEK repository)
services/Diten.CrmService/src/Diten.CrmService.Application/Features/Knowledge/Path/**                 (yeni — §10)
services/Diten.CrmService/src/Diten.CrmService.Application/Features/Knowledge/Path/Contract/          (yeni KnowledgePathContract)
services/Diten.CrmService/src/Diten.CrmService.Persistence/Repositories/KnowledgePathRepository.cs    (yeni — TEK dosya)
services/Diten.CrmService/src/Diten.CrmService.Persistence/DependencyInjection.cs                     (RegisterClassMaps: path + 2 embedded tip; index; DI — additive)
services/Diten.CrmService/src/Diten.CrmService.Api/Controllers/CRM/KnowledgePathsController.cs        (yeni — path + gömülü adım alt-route'ları)
services/Diten.CrmService/src/Diten.CrmService.Api/Controllers/CRM/KnowledgePathContractController.cs (yeni)
services/Diten.CrmService/src/Diten.CrmService.Api/Models/CRM/KnowledgePathRequests.cs                (yeni)
services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/Knowledge/KnowledgePathTests.cs          (yeni)
services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/Knowledge/KnowledgePathStepRulesTests.cs (yeni — embedded adım kuralları)

# --- frontend: TEK proxy controller + viewmodel ---
frontend/Diten.Web/Controllers/CRM/KnowledgePathsController.cs                                        (yeni, proxy-only)
frontend/Diten.Web/Models/CRM/KnowledgePathViewModels.cs                                              (yeni; path VM + gömülü step VM + branch VM)

# --- frontend: Views/CRM/KnowledgePaths/ — DEV-0001 Compact kanonik 9 dosya (§11.2) ---
frontend/Diten.Web/Views/CRM/KnowledgePaths/Index.cshtml                                              (Layout="_LayoutTenantShell" AÇIKÇA)
frontend/Diten.Web/Views/CRM/KnowledgePaths/Create.cshtml                                             (Compact-özel)
frontend/Diten.Web/Views/CRM/KnowledgePaths/Edit.cshtml                                               (Compact-özel)
frontend/Diten.Web/Views/CRM/KnowledgePaths/Details.cshtml                                            (Compact-özel; salt-okunur adım listesi + branch koşulları)
frontend/Diten.Web/Views/CRM/KnowledgePaths/_Form.cshtml                                              (Compact-özel; path formu + GÖMÜLÜ adım alt-editörü + adım-içi branch repeater)
frontend/Diten.Web/Views/CRM/KnowledgePaths/_Filter.cshtml
frontend/Diten.Web/Views/CRM/KnowledgePaths/_DataTable.cshtml                                         (data-dt-standard="v2" + skeleton; TEK DataTable = path listesi)
frontend/Diten.Web/Views/CRM/KnowledgePaths/_IndexL10n.cshtml
frontend/Diten.Web/Views/CRM/KnowledgePaths/KnowledgePathsIndex.cs                                    (marker class)

# --- frontend: JS + RESX + nav ---
frontend/Diten.Web/wwwroot/assets/js/CRM/KnowledgePaths/{index.js, index.l10n.js, form.js}            (yeni; form.js adım repeater + branch repeater'ı barındırır)
frontend/Diten.Web/Resources/Views/CRM/KnowledgePaths/KnowledgePathsIndex.{ar,en,es,fr,ru,tr,zh}.resx (7 dil)
frontend/Diten.Web/Resources/SharedResource.{ar,en,es,fr,ru,tr,zh}.resx                               (KnowledgePathsMenu anahtarı ×7)
frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml                                             (TEK <li>, dar istisna — §6)

# --- doğrulama ---
scripts/smoke-mod0162-fu04-knowledge-path-authenticated.ps1                                           (yeni; FU03 script'i şablon)
docs/audits/mod-0162-fu04-knowledge-path-runtime-ui-*.md                                              (evidence)
```

> **D2 ile repo scope'tan ÇIKARILANLAR:** `IKnowledgePathStepRepository` · `KnowledgePathStepRepository` ·
> `KnowledgePathStepsController` (backend **ve** frontend) · `Views/CRM/KnowledgePathSteps/**` (9 dosya) ·
> `wwwroot/assets/js/CRM/KnowledgePathSteps/**` (3 dosya) ·
> `Resources/Views/CRM/KnowledgePathSteps/**` (7 dosya) · `knowledge_path_steps` collection ve index'leri.

---

## 6. Protected Paths

`.antigravity/**` · `gateway/Diten.ApiGateway/**/ocelot.json` (**değişmez** — wildcard yeterli, §15) ·
`services/Diten.MdmService/**` · `services/Diten.Platform/**` · `services/Diten.AuthService/**` ·
`services/Diten.HcmService/**` · `services/Diten.EnterpriseStrategyService/**` ·
`services/Diten.DevEnablementService/**` (Golden Reference — okunur, değiştirilmez) ·
**FU02 yüzeyi**: `Features/Knowledge/Content/**`, `Features/Knowledge/{Subject,Topic,AudienceProfile}/**`,
`Domain/Entities/KnowledgeContent.cs` (**D6 dâhil alan eklenmez**), `IKnowledgeContentLinkageReader` imzası,
`Views/CRM/Knowledge/**`, `wwwroot/assets/js/CRM/Knowledge/**` ·
**FU03 yüzeyi**: `Features/Knowledge/Concept/**`, `Domain/Entities/Concept*.cs`,
`Domain/Entities/KnowledgeContentConceptLink.cs`, `Views/CRM/KnowledgeConcepts/**` ·
MOD-0165 Campaign runtime · MOD-0164 Consent/Preference · MOD-0155 · MOD-0309 ·
RBAC seed / role template / permission catalog (`crm.knowledge.path.*` **kataloğa yazılmaz**) · MOD-0048 publish ·
Mongo hand-edit · `execution/registries/**` (yalnız closeout'ta, kullanıcı onayıyla) · `execution/portfolio/**` ·
**FU01A / FU01B / FU01C pack dosyaları** (okunur, değiştirilmez) ·
`frontend/Diten.Web/Views/Shared/_Layout.cshtml` (FROZEN) · `frontend/Diten.Web/Controllers/Archive/**` +
`frontend/Diten.Web/Views/Archive/**` (FROZEN).

**Kasıtlı dokunulan tek istisna (protected DEĞİL — dar kapsam):**
`frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml` — CRM Admin → Knowledge nav'ına **tek `<li>`**
(*Knowledge Paths* → `/CRM/KnowledgePaths`, permission-guard'lı) eklenir; mevcut *Knowledge* / *Knowledge Concepts*
`<li>`'leri, `active` yol mantığı ve oturum davranışı **değişmez**.

---

## 7. Dependencies

Frontmatter listesinin açıklamalı karşılığı.

| Bağımlılık | Yön | Sözleşme / etki |
|---|---|---|
| **MOD-0162-FU01A** (approved boundary) | implement eder | §3–§13 sözleşmesi BURADA runtime'a döner; **§16'nın tek-aggregate önerisi D2 ile birebir uygulanır**; kalan sapmalar §2.1'de gerekçeli |
| **MOD-0162-FU02** (SHIPPED) | **hard prerequisite** | `KnowledgeContent` + `Subject`/`Topic`/`AudienceProfile` olmadan path kurulamaz; FU02 sözleşmesi **kırılmaz** (§2.2). D6 yalnız **mevcut** `ContentType` değerini okur |
| **MOD-0162-FU03** (DONE) | **tüketir (read-only)** | Gömülü adımın `ConceptNodeId`'si → FU03 `ConceptNode`; node picker FU03 endpoint'ini okur, concept aggregate'leri **mutate edilmez** (§2.3) |
| **MOD-0162-FU01B** (EngagementJourney) | **implement ETMEZ** | Journey path'e **referans** verir, adımlarını kopyalamaz; `visit-1/2/3` mantığı path'e **gömülmez** (FU01A §1) |
| **MOD-0162-FU01C** | boundary | Concept sözleşmesi okunur; "hangi içerik önce" (FU04) ≠ "kavramlar nasıl bağlanır" (FU01C/FU03) |
| **MOD-0048** (reference data) | ileride tüketir | 4 set publish **ayrı operatör işi** (F-RD); dev'de in-domain fail-closed yürür, vokabüler çelişkisi yok |
| **MOD-0155** (Field Sales / Visit) | **consumer** | `IKnowledgePathReader` + `?status=published&effectiveAt=` liste seam'i; path **seçmez**, skorlamaz. MOD-0155 kodu bu FU'da **değişmez** |
| **MOD-0309** (Learning / Training Records) | **consumer** | `CompletionRule` iki tarafın sözleşme alanı: bu FU **beyan eder**, MOD-0309 **ölçer ve kaydeder** (FU01A §11) |
| **MOD-0028 / MOD-0029** | boundary | Adım dosya taşımaz; `FileRef` yalnız FU02 içeriğinin alanıdır (FU01A §12) |
| **MOD-0018** (RBAC) | yalnız tüketim | seed/grant **YOK**; dev fallback §14; F-RBAC en sonda |
| **DEV-0001** (Golden Reference Compact) | golden reference | **Tek** yüzey, **tek** klasör (§11); Slim dosya seti **kullanılmaz** |

---

## 8. Runtime Constraints

- **Servis:** `Diten.CrmService` (port **5061**), **yeni servis yaratılmaz** (FU01A §16).
- **Gateway:** tüm çağrılar `:5000` üzerinden; browser JS **servis portuna gitmez** (same-origin MVC proxy).
- **Soft delete:** `DELETE` ve `PATCH` **yoktur** — kaldırma = archive (path **ve** gömülü adım); archived kayıt
  update kabul etmez (**409**). Archived adım **diziden silinmez**.
- **Tenant:** `EntityBase` tenant-owned; `TenantId` **server-side** claim'den, DTO/payload'da yer almaz;
  cross-tenant erişim **404 / boş liste**.
- **Concurrency:** **tek** `EntityBase.Version` (path root); **adım düzenlemeleri de** bu token'a tabidir →
  uyuşmazlık **409**, sessiz overwrite yasak.
- **Atomiklik (D2):** her yazma **tek doküman** yazımıdır → çok-doküman transaction, `SupportsTransactionsAsync`
  guard'ı ve compensation **gerekmez**; `new-version` bile *(oku → klonla → tek insert)* iki bağımsız yazımdır ve
  **yarım kalmış path** üretmez (kopya insert edilmezse kaynak değişmemiştir).
- **Vokabüler:** in-domain fail-closed (§4.4) — yayınlanmamış MOD-0048 seti runtime'ı fail-open bırakmaz.
- **Doküman büyümesi:** path başına max **200** adım, adım başına max **20** branch condition (§4.2, V-S20).

### 8.1 API Contract

Tüm route'lar mevcut Gateway wildcard'ı `/api/crm/knowledge/{everything}` altındadır → **`ocelot.json` DEĞİŞMEZ**
(§15; `GET/POST/PUT/OPTIONS` — DELETE/PATCH zaten yok). **Adım route'ları path'in ALT KAYNAĞIDIR** (D2); düz
`/path-steps` ailesi **yoktur**.

```text
GET    /api/crm/knowledge/path/contract

GET    /api/crm/knowledge/paths                      ?subjectId&topicId&audienceProfileId&language&status
                                                     &effectiveAt&pathCode&search&includeArchived
POST   /api/crm/knowledge/paths
GET    /api/crm/knowledge/paths/{pathId}                       → path + gömülü adımlar (çözülmüş içerikle)
PUT    /api/crm/knowledge/paths/{pathId}                       → path ALANLARI; `steps` dizisi KABUL EDİLMEZ
POST   /api/crm/knowledge/paths/{pathId}/publish               (D4)
POST   /api/crm/knowledge/paths/{pathId}/new-version           (D5)
POST   /api/crm/knowledge/paths/{pathId}/archive

GET    /api/crm/knowledge/paths/{pathId}/steps                 ?includeArchived — StepOrder sıralı + çözülmüş içerik
POST   /api/crm/knowledge/paths/{pathId}/steps                 → gömülü diziye adım ekler (tek doküman yazımı)
PUT    /api/crm/knowledge/paths/{pathId}/steps/{stepId}
POST   /api/crm/knowledge/paths/{pathId}/steps/{stepId}/archive
```

**Yasaklar:** `DELETE` yok · `PATCH` yok · **düz `/api/crm/knowledge/path-steps` ailesi yok** (D2) ·
`PUT /paths/{id}` içinde `steps` dizisi ile toplu adım yazımı **yok** (aynı veri için iki yazma yolu olmaz) ·
payload'da `TenantId` yok (gönderilirse **sessizce yok sayılır**, claim kazanır) · service-to-service doğrudan iş
çağrısı yok (yalnız Gateway) · **hiçbir endpoint "en uygun path"i veya "sonraki en iyi adım"ı döndürmez** —
`recommend` / `bestNext` / `score` gibi bir sorgu parametresi **yoktur**.

### 8.2 Contract flags

```json
{ "supportsKnowledgePath": true, "supportsKnowledgePathStep": true,
  "supportsContentSequence": true, "supportsKnowledgePathVersioning": true,
  "supportsPublishedStepSetFreeze": true, "supportsRequiredOptionalSteps": true,
  "supportsPrerequisiteChain": true, "supportsVersionPinPolicy": true,
  "supportsStepConceptNodeReference": true, "supportsFutureBranchingMetadata": true,
  "supportsArchiveLifecycle": true, "supportsEffectiveDating": true,
  "supportsContractDrivenUi": true }
```

**ASLA eklenmez (`false` olarak bile) — 11 yasak flag:** `supportsBranchEvaluator` ·
`supportsRecommendationEngine` · `supportsBestNextContent` · `supportsCompletionTracking` ·
`supportsProgressEngine` · `supportsAiPersonalization` · `supportsDigitalDetailing` · `supportsVisitPlanning` ·
`supportsRoutePlanning` · `supportsWorkflowApproval` · `supportsHardDelete`. Gerekçe FU03 emsali: bir yeteneği
`false` ile bile ilan etmek boundary'yi yanlış temsil eder.

**`limitations` listesinde yayınlanır:** `maxStepsPerPath: 200` · `maxBranchConditionsPerStep: 20` ·
`stepsAreEmbeddedInPathDocument: true` · `noBranchEvaluation` · `noCompletionTracking` · `noRecommendation`.

**FU02 ve FU03 contract'ları değişmez** — kendi flag setleri ve endpoint'leri olduğu gibi kalır.

### 8.3 `VersionPinPolicy` çözümleme semantiği (sessiz sürüm kayması YASAK)

| Politika | Davranış | Response |
|---|---|---|
| `pinned` *(varsayılan)* | Adım yazımdaki `ContentId`'ye **sabitlenir**; içerik yeni sürüm yayınlasa bile path değişmez | `ContentResolutionStatus = pinned` |
| `latest-published` | Okuma anında `ContentCode` üzerinden **published + effective** sürüm çözülür (`IsConsumableAt(effectiveAt)`) | `ContentResolutionStatus = resolved-latest` |
| her ikisi — çözülemezse | Adım **düşmez, gizlenmez, uydurulmaz** | `ContentResolutionStatus = unresolved` + `ResolvedContentId = null` (**görünür**, fail-closed) |

Her durumda çözülen `ResolvedContentId` + `ResolvedContentVersion` **cevapta yer alır** (FU01A §6.1). Çözümleme
FU02 `IKnowledgeContentLinkageReader` imzasını **genişletmez** (§2.2/AC-FU02-2); FU04'ün kendi read-only
resolver'ı içerik repository'sini okur.

### 8.4 Tüketim seam'i — `IKnowledgePathReader` (motor DEĞİL)

```csharp
Task<IReadOnlyList<KnowledgePathDto>> ResolvePublishedPathsAsync(
    KnowledgePathCriteria criteria, CancellationToken ct);   // subject/topic/audience/language/effectiveAt
Task<IReadOnlyList<KnowledgePathStepDto>> GetOrderedStepsAsync(
    Guid pathId, DateTimeOffset effectiveAt, CancellationToken ct);
```

**Döndürür:** yalnız `published` + effective path'ler ve `StepOrder` → `StepCode` ile **deterministik sıralı**,
`StepStatus = active`, içeriği çözülmüş gömülü adımlar. **Yapmaz:** skorlama · "en uygun path" seçimi · öneri ·
dal değerlendirmesi · completion okuma/yazma. Veri yoksa **boş döner** — varsayılan uydurulmaz (MOD-0151 R11 ruhu).
`draft` / `review` / `approved` / `inactive` / `archived` path **tüketiciye asla gitmez** (FU01A §6).

---

## 9. Layout & Shell Contract

- `shell: tenant` → **tüm** `.cshtml` dosyalarında Razor bloğunda **AÇIKÇA**:

```cshtml
@{
    ViewData["Title"] = Localizer["PageTitle"];
    Layout = "_LayoutTenantShell";   // shell: tenant
}
```

- View klasörü: **`Views/CRM/KnowledgePaths/`** (tek klasör — D3)
- Frontend route'u: **`/CRM/KnowledgePaths`** (tek sayfa; adım yönetimi bu sayfanın **içindedir**)
- `_ViewStart.cshtml` varsayılanı **değiştirilmez**; `_Layout.cshtml` FROZEN.
- Partial çağrıları **absolute path** ile: `~/Views/CRM/KnowledgePaths/_Filter.cshtml`
- Index bölüm sırası (Golden Compact): ① Filter → ② BulkActionBar → ③ DataTable; **offcanvas panel YOK**
  (Compact yasağı, §11.2).
- Nav: `_LayoutTenantShell.cshtml` içinde CRM Admin → Knowledge grubunda mevcut *Knowledge* (satır ~403) ve
  *Knowledge Concepts* (satır ~417) `<li>`'lerinden sonra **üçüncü ve tek yeni `<li>`** (*Knowledge Paths*),
  `@if (Perms.Has(...))` guard'lı, `SharedResource` anahtarı `KnowledgePathsMenu` (7 dil).

---

## 10. Backend File Convention

**Naming Golden Reference ile birebir** (`module-pack-standard.md` §4): Command/Query **record**,
Handler/Validator **class** ve isimlerinde **`Command` / `Query` / `Request` suffix YOK**.
**Tek aggregate → tek feature klasörü; adım komutları da path root'unu mutasyona uğratır ve tek repository'yi
kullanır.**

```text
services/Diten.CrmService/src/Diten.CrmService.Application/Features/Knowledge/Path/
├── Commands/
│   └── KnowledgePathCommands.cs        → CreateKnowledgePathCommand · UpdateKnowledgePathCommand ·
│                                         PublishKnowledgePathCommand · CreateKnowledgePathVersionCommand ·
│                                         ArchiveKnowledgePathCommand ·
│                                         AddKnowledgePathStepCommand · UpdateKnowledgePathStepCommand ·
│                                         ArchiveKnowledgePathStepCommand      (hepsi sealed record;
│                                         adım komutları PathId taşır ve path root'unu yazar)
├── Queries/
│   └── KnowledgePathQueries.cs         → ListKnowledgePathsQuery · GetKnowledgePathQuery ·
│                                         GetKnowledgePathStepsQuery           (sealed record)
├── Handlers/
│   ├── KnowledgePathCommandHandlers.cs → CreateKnowledgePathHandler · UpdateKnowledgePathHandler ·
│   │                                     PublishKnowledgePathHandler · CreateKnowledgePathVersionHandler ·
│   │                                     ArchiveKnowledgePathHandler · AddKnowledgePathStepHandler ·
│   │                                     UpdateKnowledgePathStepHandler · ArchiveKnowledgePathStepHandler
│   │                                     (sealed class, suffix YOK)
│   └── KnowledgePathQueryHandlers.cs   → ListKnowledgePathsHandler · GetKnowledgePathHandler ·
│                                         GetKnowledgePathStepsHandler
├── Validators/
│   ├── CreateKnowledgePathValidator.cs · UpdateKnowledgePathValidator.cs      (suffix YOK)
│   └── AddKnowledgePathStepValidator.cs · UpdateKnowledgePathStepValidator.cs
├── Contract/
│   └── KnowledgePathContract.cs        → GetKnowledgePathContractQuery + DTO + flags + vokabüler + limitler
├── IKnowledgePathReader.cs             → §8.4 read-only seam + default implementation
├── KnowledgePathDtos.cs                → TEK dosyada tüm DTO / ViewModel (path + gömülü step + branch)
├── KnowledgePathMapper.cs
├── KnowledgePathValidation.cs          → §12 kurallarının ortak yardımcıları (sıra, prerequisite, freeze, limitler)
└── KnowledgePathPermissions.cs         → §14 (TANIM ONLY)
```

**D2 ile kaldırılan dosyalar:** `KnowledgePathStepCommands.cs` · `KnowledgePathStepQueries.cs` ·
`KnowledgePathStepCommandHandlers.cs` · `KnowledgePathStepQueryHandlers.cs` ·
`IKnowledgePathStepRepository` / `KnowledgePathStepRepository`.

> **⚠️ Dosya gruplama — açık sapma beyanı.** Golden Reference `Create{Module}Command.cs` gibi **komut başına tek
> dosya** ister. FU02 ve FU03, `Diten.CrmService/.../Features/Knowledge/**` altında **aggregate başına gruplanmış**
> dosya kullanır (`Concept/Node/ConceptNodeCommands.cs` · `ConceptNodeHandlers.cs`) ve bu artık in-domain yerleşik
> konvansiyondur. Bu pack **yerleşik Knowledge konvansiyonunu sürdürür**; **sınıf/record isimleri Golden Reference
> ile birebirdir** (`CreateConceptNodeHandler` deseni — kanıt: `Concept/Node/ConceptNodeHandlers.cs:9`). Sapma
> yalnız **dosya gruplamasındadır** ve bilinçlidir. Kullanıcı komut-başına-dosya isterse §10 tek kalemde
> değiştirilir (kayıt: **F-FILE**).

`BulkDelete{Module}Command` **YOK** — bu modülde hard delete ve bulk delete yoktur (archive-only; FU02/FU03 emsali).

---

## 11. Frontend File Contract

### 11.1 Golden reference kararı — kullanıcı-form alanı türetmesi (GÖSTERİLİR)

Sayım kuralı (`module-pack-standard.md` §3): yalnız kullanıcının create/edit formunda **doldurduğu** modül alanları
sayılır. `Id`, `TenantId`, audit alanları, türetilmiş alanlar, DataTable checkbox/action kolonları **sayılmaz**.
Effective window (`EffectiveFrom` + `EffectiveTo`) tek kontrol olarak render edilir → **1** sayılır (FU03 yöntemi).

**Golden-reference yüzeyi (TEK) — `KnowledgePath`:** §4.1'de 23 satır; form-dışı olanlar düşüldükten sonra
kalan **12**:

| # | Kullanıcı-form alanı | # | Kullanıcı-form alanı |
|---|---|---|---|
| 1 | `PathCode` | 7 | `Objective` |
| 2 | `PathName` | 8 | `LanguageCode` |
| 3 | `Description` | 9 | `PathVersion` |
| 4 | `SubjectId` | 10 | `PathStatus` |
| 5 | `TopicId` | 11 | `EffectiveFrom` (+`EffectiveTo` **aynı kontrol**) |
| 6 | `AudienceProfileId` | 12 | `Source` |

*Form-dışı (11):* `Id` · `TenantId` · `EffectiveTo` (aynı kontrol) · `Steps` (alt-editör, alan değil) ·
`StepSetFrozenAt` · `PublishedAt` · `PublishedBy` · `SupersedesPathId` · `ArchivedAt/By` · `CreatedAt/By` ·
`UpdatedAt/By` · `EntityBase.Version` (+ tüm türetilmişler).

→ **12 > 8 ⇒ `golden_reference: compact`** (frontmatter `form_field_count: 12`).

**Gömülü adım alt-editörü — ayrı golden-reference yüzeyi DEĞİLDİR (D2/D3).** Adım, kendi sayfası/DataTable'ı
olan bağımsız bir modül değil, path Compact formunun **içindeki bir repeater**dır; bu yüzden **ikinci bir
Slim/Compact kararı doğurmaz** ve verifier için **ikinci bir referans koşusu gerektirmez**. Tamlık için alan
sayımı (13 — `PathId` artık **yok**, bağlam gömülü):

| # | Alt-editör alanı | # | Alt-editör alanı |
|---|---|---|---|
| 1 | `StepOrder` | 8 | `PrerequisiteStepId` |
| 2 | `StepCode` | 9 | `ConceptNodeId` (Subject→Type→Node zincirli) |
| 3 | `StepTitle` | 10 | `EstimatedDurationMinutes` |
| 4 | `StepType` | 11 | `Notes` |
| 5 | `ContentId` (Subject→Content zincirli) | 12 | `BranchConditions` (adım-içi repeater, D7 — 1 grup) |
| 6 | `VersionPinPolicy` | 13 | `IsRequired` |
| 7 | `CompletionRule` | | |

*Alt-editör dışı:* `StepId` (üretilir) · `ContentCode` (türetilir) · `StepStatus` (archive aksiyonu) ·
`ArchivedAt/By` · adım audit alanları · tüm `Resolved*` / `IsCross*` türetilmişleri.

**Sonuç:** modülde **tek** golden-reference yüzeyi, **tek** klasör, **tek** verifier koşusu vardır ve
**hiç Slim dosyası yoktur** (`_CreateEditOffcanvas.cshtml` / `_DetailsQuickView.cshtml` **YASAK**). FU03'ün hibrit
konsolunda kalan **2 yapısal verifier FAIL'i (FU03/F-VERIFY) burada yapısal olarak oluşmaz**.

### 11.2 Dosya seti — TEK klasör, kanonik Compact 9 dosya (TEK TEK enumerasyon)

**`Views/CRM/KnowledgePaths/` (DEV-0001 Compact — tam ve tek set):**

| # | Dosya | Rol |
|---|---|---|
| 1 | `Index.cshtml` | Liste kabuğu; `Layout = "_LayoutTenantShell"` **açıkça**; Filter → BulkActionBar → DataTable sırası |
| 2 | `Create.cshtml` | **Compact-özel** sayfa kabuğu + `_Form` |
| 3 | `Edit.cshtml` | **Compact-özel** sayfa kabuğu + `_Form` |
| 4 | `Details.cshtml` | **Compact-özel** detay sayfası; **salt-okunur** adım listesi (`StepOrder` sıralı, çözülmüş içerik + branch koşulları) + publish / new-version aksiyonları |
| 5 | `_Form.cshtml` | Create/Edit ortak formu: **path'in 12 alanı** + **gömülü adım alt-editörü (repeater)** + **adım içinde BranchCondition repeater** (D3/D7) — ayrı partial açılmaz, klasör kanonik 9 dosyada kalır |
| 6 | `_Filter.cshtml` | Inline collapsible filter (`subject`, `topic`, `audience`, `language`, `status`, `effectiveAt`, `includeArchived`) |
| 7 | `_DataTable.cshtml` | `data-dt-standard="v2"` + skeleton loader; **TEK DataTable** = path listesi (adım kolonları: `ActiveStepCount` / `RequiredStepCount` rozetleri) |
| 8 | `_IndexL10n.cshtml` | JSON payload bridge |
| 9 | `KnowledgePathsIndex.cs` | Marker class (RESX kökü) |

**JS (Golden Compact seti — 3 dosya):**

```text
wwwroot/assets/js/CRM/KnowledgePaths/index.js       → DataTable (DtDefaults + v2), filtre, archive
wwwroot/assets/js/CRM/KnowledgePaths/index.l10n.js  → camelCase→PascalCase L10n köprüsü
wwwroot/assets/js/CRM/KnowledgePaths/form.js        → içerik/kavram zincirli seçiciler + ADIM repeater + BRANCH repeater
```

`index.l10n.js` **camelCase→PascalCase** dönüşümünü atlamaz (aksi hâlde `window.L10n` anahtarları `undefined`
döner ve toast "(undefined: corrId)" olur). DataTable JS **HttpOnly cookie okumaz, Bearer token kurmaz**;
API profili **`proxy`** (same-origin `/CRM/KnowledgePaths/api/...`). Sayfada **tek** DataTable vardır →
`updateVisualState` global selector çakışması **yapısal olarak yok**.

**RESX (tek klasör × 7 dil + shared):**

```text
Resources/Views/CRM/KnowledgePaths/KnowledgePathsIndex.{ar,en,es,fr,ru,tr,zh}.resx
Resources/SharedResource.{ar,en,es,fr,ru,tr,zh}.resx        → KnowledgePathsMenu
```

**YASAK dosyalar:** `_CreateEditOffcanvas.cshtml` · `_DetailsQuickView.cshtml` (Compact yasağı) ·
**`Views/CRM/KnowledgePathSteps/**` (D2/D3 ile kaldırıldı)** · Index içinde create/edit offcanvas ·
hardcoded vokabüler listesi (tüm dropdown'lar `path/contract`'tan beslenir).

**Kullanılan mevcut yüzeyler (yeni dosya değil):** içerik seçici FU02'nin `/api/crm/knowledge/contents`,
kavram seçici FU03'ün `/api/crm/knowledge/concept-types|concept-nodes` endpoint'lerini **proxy üzerinden okur** —
her iki modülün view/JS/controller dosyalarına **dokunulmaz** (§6).

---

## 12. Validation Rules

### 12.1 `KnowledgePath` (aggregate root)

| # | Kural | Sonuç |
|---|---|---|
| V-P01 | `TenantId` payload'da → yok sayılır, claim kazanır | 2xx (sessiz ignore) |
| V-P02 | `PathCode` boş / max 100 aşımı; `PathName` boş / max 200; `Objective` boş / max 500 | **400** |
| V-P03 | Aynı `(TenantId, PathCode, PathVersion)` ikinci **non-archived** kayıt | **409** |
| V-P04 | `SubjectId` yok / archived | **400** |
| V-P05 | `TopicId` verildi ama `SubjectId`'ye ait değil | **400** |
| V-P06 | `AudienceProfileId` archived / başka tenant | **400** |
| V-P07 | `EffectiveTo < EffectiveFrom` | **400** |
| V-P08 | Bilinmeyen `PathStatus` / `Source` (in-domain set dışı) | **400** (fail-closed) |
| V-P09 | Archived path update (adım yazımı dâhil) | **409** |
| V-P10 | Aynı `(PathCode, LanguageCode)` için **örtüşen** effective pencerede **ikinci `published`** sürüm | **409** |
| V-P11 | Publish denemesi, path'te **hiç `active` + `IsRequired=true` adım yokken** | **400** |
| V-P12 | `Update` ile `PathStatus = published` geçişi (**D4**: publish ayrı endpoint) | **400** |
| V-P13 | `published` path'te `EffectiveTo` ve `PathStatus` (`inactive`/`archived`) **dışında** herhangi bir alan değişimi | **409** (sürüm dondurulmuştur; değişiklik `new-version` ister) |
| V-P14 | `new-version` kaynak path `published` değil | **400** (kopyalanacak dondurulmuş sürüm yok) |
| V-P15 | `EntityBase.Version` uyuşmazlığı — **path VE adım yazımlarının ortak token'ı** | **409** |
| V-P16 | `PUT /paths/{id}` gövdesinde `steps` dizisi gönderildi | **400** (adımlar yalnız alt-route'lardan yönetilir; iki yazma yolu yok) |

### 12.2 Gömülü adım (`KnowledgePath.Steps[]`)

| # | Kural | Sonuç |
|---|---|---|
| V-S01 | `pathId` yok / başka tenant | **404** · archived path → **409** (V-P09) |
| V-S02 | Path `published` (`StepSetFrozenAt` dolu) → adım ekleme / güncelleme / arşivleme | **409** (adım seti dondu) |
| V-S03 | Aynı path içinde duplicate `StepOrder` (**active** adımlar) — **DB index'i YOK, handler tek savunma** (§4.5) | **409** |
| V-S04 | `StepCode` boş / path içinde duplicate (**active**) — aynı şekilde handler doğrular | **409** |
| V-S05 | `ContentId` yok / başka tenant / **published+effective değil** | **400** |
| V-S06 | `ContentId` archived içeriğe işaret ediyor (yeni veya **değişen** değer) | **400** |
| V-S07 | `ContentId` PUT'ta **değişmedi** (aynı değer / payload'da yok) | içerik yeniden doğrulanmaz, **400 üretilmez** (FU03 V22 dirty-check emsali) |
| V-S08 | Bilinmeyen `StepType` / `CompletionRule` / `VersionPinPolicy` / `StepStatus` | **400** (fail-closed) |
| V-S09 | `PrerequisiteStepId` **aynı path'te değil** · kendisi · `StepOrder`'ı **büyük veya eşit** · döngü oluşturuyor | **400** |
| V-S10 | `IsRequired=true` adımın prerequisite'i `IsRequired=false` | **400** (zorunlu zincir atlanabilir adıma bağlanamaz) |
| V-S11 | `CompletionRule = duration-met` ama `EstimatedDurationMinutes` null / aralık dışı (1–600) | **400** |
| V-S12 | **(D6=A)** `CompletionRule = assessment-passed` ama referanslanan içeriğin **`ContentType != "quiz"`** | **400** |
| V-S13 | `ConceptNodeId` yok / archived / başka tenant | **400** |
| V-S14 | **(D7)** `BranchConditions[].TargetStepId` **aynı path'te değil** · `ConditionCode` boş | **400** (**değerlendirme yok**, yalnız referansel akıl sağlığı) |
| V-S15 | Adımın içerik Subject'i path Subject'inden farklı · içerik dili path dilinden farklı | **kabul edilir**, `IsCrossSubjectStep` / `IsCrossLanguageStep = true` (**görünür**; sessiz karışım yasak — FU01A §6) |
| V-S16 | Archived adım update | **409** |
| V-S17 | Bir adım, **active** bir adımın prerequisite'i iken archive ediliyor | **409** (dangling prerequisite yasağı) |
| V-S18 | Path archive → gömülü adımlar **archived kabul edilir**, **diziden silinmez** | okunur kalır (**aynı doküman** — ayrı cascade yazımı YOK) |
| V-S19 | Herhangi bir adım yazımı → **path'in** `EntityBase.Version`'ı artar; eş zamanlı ikinci yazım | **409** (V-P15 ile aynı token) |
| V-S20 | Path'te **200**'den fazla adım · adımda **20**'den fazla branch condition | **400** (§4.2 doküman büyüme sınırı; contract'ta ilan edilir) |

**Reason code'lar:** her yazma sonucu ve red, `KnowledgePathReasonCodes` kataloğundan bir kod taşır
(`knowledge_path_created` · `_updated` · `_published` · `_archived` · `_version_created` · `_duplicate_code` ·
`_step_added` · `_step_updated` · `_step_archived` · `_step_order_conflict` · `_step_set_frozen` ·
`_prerequisite_invalid` · `_required_step_optional_prerequisite` · `_assessment_content_not_quiz` ·
`_content_not_consumable` · `_content_unresolved` · `_step_limit_exceeded` · `_archived_no_mutation` ·
`_reference_archived`). **Hiçbir şey sessiz değildir.**

---

## 13. Failure Path to Verify

- **Duplicate — aynı `PathCode` + `PathVersion`**
  - Expected: **409** + UI field-level hata + kayıt **oluşmaz** + reload sonrası temiz state.
- **Duplicate — aynı path içinde aynı `StepOrder`**
  - Expected: **409** + alt-editör satırında hata + doküman **yazılmaz**. ⚠️ DB unique index **yok** (dizi-içi
    tekillik index'lenemez) → handler doğrulaması **tek savunma hattı**, test zorunlu.
- **Missing — `Objective` / `ContentId` / `StepType` boş**
  - Expected: **400** + validator mesajı + save engellenir; sunucu tarafında da reddedilir (client-only doğrulama yok).
- **Concurrency — iki kullanıcı aynı path'i (veya adımlarını) düzenliyor**
  - Expected: **409** + UI "veri değişti, yeniden yükleyin" + **sessiz overwrite YOK**. Adım düzenlemesi de
    **path'in** `EntityBase.Version`'ını kullanır (D2).
- **Frozen — published path'in adımını düzenleme denemesi**
  - Expected: **409** + UI'da adım alt-editörü **disabled** + "bu sürüm yayınlandı, yeni sürüm oluşturun" gerekçesi
    + `new-version` aksiyonu görünür.
- **Unauthorized — permission'ı olmayan aktör**
  - Expected: **403** + UI aksiyon **disabled** / permission-denied state; liste boş listeyle **maskelenmez**.
    Publish yetkisi olmayan (`manage` var, `publish` yok) aktör: publish **403**, düzenleme **200** (SoD, D4).
- **Cross-tenant — başka tenant'ın `pathId`'si ile GET/PUT**
  - Expected: **404** (varlık sızdırılmaz), yazma **gerçekleşmez**.
- **Unresolved content — `latest-published` adımın içeriği yayından kalktı**
  - Expected: **200** + `ContentResolutionStatus = unresolved` + UI'da uyarı rozeti; adım **gizlenmez**,
    **başka içerikle doldurulmaz**.
- **Dangling prerequisite — prerequisite adım archive ediliyor**
  - Expected: **409** + hangi adımın bağımlı olduğu mesajda; archive **gerçekleşmez**.
- **Boş path publish — `IsRequired=true` active adım yok**
  - Expected: **400** + gerekçeli hata; path `draft` kalır.
- **Assessment uyumsuzluğu (D6=A)** — `assessment-passed` seçildi, içerik `ContentType = "brochure"`
  - Expected: **400** + `knowledge_path_assessment_content_not_quiz` reason code; FU02 içeriği **değişmez**.
- **Limit aşımı — 201. adım eklenmesi**
  - Expected: **400** + limitin contract'tan okunabildiği gerekçe mesajı.

---

## 14. Authorization Convention

```text
Policy:     [Authorize]                                   // shell: tenant
Permission: [HasPermission("crm.knowledge.path.{action}")] // PKS-001: lowercase-dotted, >= 3 segment
Actor type: tenant_user
```

| Anahtar | Kapsam |
|---|---|
| `crm.knowledge.path.read` | Path + gömülü adım listeleme/detay + contract |
| `crm.knowledge.path.manage` | Path create/update/archive + **gömülü adım** ekleme/güncelleme/arşivleme + `new-version` |
| `crm.knowledge.path.publish` | **Yalnız** `POST /paths/{id}/publish` — SoD: yazan ≠ yayınlayan (FU01A §9, **D4**) |

> **D2 sonucu:** ayrı `crm.knowledge.path-step.manage` anahtarı **kaldırıldı**. Adım, path aggregate'inin
> **içindedir**; aynı dokümanın parçası için ikinci bir yetki sınırı tanımlamak yanıltıcı olurdu. Anahtar seti
> böylece FU01A §9'un kanonik önerisiyle (**read / manage / publish**) birebir örtüşür.

**TANIM ONLY — seed/grant YOK** (FU01A §9 + AC-SEQ-3; RBAC en sona bırakıldı). Katalogda `crm.knowledge.*` henüz
yok → FU02/FU03'ün **belgelenmiş fallback'i** kullanılır: `crm.territory.read` (read) /
`crm.territory.model.manage` (manage + publish). Fallback **hiçbir guard'ı gevşetmez** — endpoint'ler yine
authenticated + policy-korumalı, tüm §12 kuralları fail-closed çalışır.

> **⚠️ Fallback = YALNIZ dev/smoke, geçici.** `crm.territory.*` yeniden kullanımı, territory yetkisi olan bir
> kullanıcının knowledge path yönetebilmesi demektir; **prod'a taşınamaz**, prod tenant'a grant **YASAK**.
> Ayrıca fallback altında `publish` ile `manage` **aynı anahtara** düşer → **D4'ün SoD'si dev'de uygulanamaz**;
> bu bilinçli ve belgeli bir boşluktur, kanonik anahtarlarla kapanır → **F-RBAC**.

**Cross-service izin bağımlılığı: YOK.** FU04'ün tükettiği tüm endpoint'ler (FU02 içerik, FU03 concept) **aynı
serviste** ve aynı fallback izinleriyle korunur — FU03'teki `mdm.global-products.read` gibi bir dış izin
gereksinimi bu FU'da **yoktur**.

---

## 15. Gateway / API Routing Decision

**Karar: Gateway değişikliği GEREKSİZ.**

- Mevcut `ocelot.json` kaydı: `"/api/crm/knowledge/{everything}"` → `localhost:5061`,
  `["GET","POST","PUT","OPTIONS"]` (`gateway/Diten.ApiGateway/ocelot.json:2245-2258`).
- §8.1'deki **tüm** route'lar bu wildcard'ın altındadır (`/api/crm/knowledge/paths…`, adım alt-route'ları
  `/paths/{id}/steps…` dâhil, `/api/crm/knowledge/path/contract`) → **yeni Upstream/Downstream çifti gerekmez**.
  D2 ile adım route'ları path'in altına indiği için wildcard uyumu **daha da güçlendi**.
- Bu, `/api/crm/knowledge-paths` (FU01A §10'daki illüstratif route) yerine `/api/crm/knowledge/paths`
  seçilmesinin somut sebebidir (§2.1/S3).
- `DELETE`/`PATCH` wildcard'da **zaten yok** → bu metotlar Gateway seviyesinde de **404**.
- `gateway/Diten.ApiGateway/**/ocelot.json` **protected path**'tir; bu pack oraya yazmaz. İleride explicit route
  istenirse **ayrı `integration-agent` task'ı** açılır.
- Browser JS **`:5061`'e gitmez**; same-origin MVC proxy (`/CRM/KnowledgePaths/api/...`) → Gateway `:5000`.

---

## 16. Acceptance Criteria

Tüm maddeler **test edilebilir** — her biri §17'deki bir backend testi veya smoke adımıyla eşlenir.

**Model & boundary**
- **AC-MODEL-1** `KnowledgePath` ve adımlar **ayrı kavramlardır**; `KnowledgeContent`'e `NextContentId` eklenmez,
  `BrandContentFlow` yaratılmaz, sıra MOD-0155 `VisitPlan`'a gömülmez.
  *Test:* repo'da `NextContentId` / `BrandContentFlow` **yok**; MOD-0155 dosyaları diff'te **değişmemiş**.
- **AC-EMBED-1 (D2)** Adımlar **`KnowledgePath` dokümanının içinde** saklanır: ikinci collection, ikinci
  repository, ikinci controller ve step-seviyesi `EntityBase` **yoktur**; adım yazımı **tek doküman** yazımıdır.
  *Test:* persistence'ta yalnız `knowledge_paths` collection'ı kaydedilir; `IKnowledgePathStepRepository` **yok**;
  adım ekleme sonrası **path** `Version`'ı artar; `RegisterClassMaps` path + 2 embedded tipi kaydeder.
- **AC-EMBED-2 (D2)** Adım archive'ı **diziden silme değildir**: `StepStatus = archived` + `ArchivedAt` set edilir,
  eleman dokümanda kalır. *Test:* archive sonrası `includeArchived=true` ile adım **görünür**, `active` listede yok.
- **AC-MODEL-2** İş versiyonu alanı **`PathVersion`**'dır; `EntityBase.Version` iş alanı olarak kullanılmaz.
  *Test:* entity'de `PathVersion` var; concurrency 409 testi geçer (V-P15).
- **AC-SEQ-1** `StepOrder` path içinde **unique**, boşluklu numaralamaya izin verir; okuma sırası
  `StepOrder` → `StepCode` ile **deterministiktir**. *Test:* duplicate **409** (handler; DB index yok);
  liste sırası iki çağrıda **aynı**.
- **AC-SEQ-2** Prerequisite yönü ileri-doğru zorunlu, döngü yasak, zorunlu adım opsiyonel adıma bağlanamaz.
  *Test:* V-S09 ve V-S10 için **400**.
- **AC-SEQ-3** `published` path **en az bir `active` + `IsRequired=true`** adım içerir. *Test:* boş path publish → **400**.
- **AC-FREEZE-1** `published` sürümün **adım seti dondurulur**; değişiklik `new-version` ister.
  *Test:* published path'te adım ekleme/güncelleme/arşivleme → **409**; `new-version` → yeni `draft` +
  **yeni `StepId`**'li adım kopyaları + `SupersedesPathId` dolu + `PathVersion` artmış + **otomatik publish yok**;
  kaynak sürüm **değişmemiş**.
- **AC-VER-1** Aynı `(PathCode, LanguageCode)` için örtüşen pencerede **iki published sürüm olamaz**.
  *Test:* ikinci publish → **409**.
- **AC-PUB-1 (D4)** Publish **yalnız** `POST /paths/{id}/publish` ile yapılır ve `crm.knowledge.path.publish`
  ister; `Update` ile `published`'a geçiş **400**. *Test:* V-P12 **400**; publish izni olmayan aktör **403**.
- **AC-PIN-1** İçerik sürümü determinizmi: `pinned` sabit kalır, `latest-published` çözülür, çözülemeyen adım
  **`unresolved`** olarak **görünür** — sessiz sürüm kayması ve sessiz düşme **yok**.
  *Test:* içerik yeni sürüm yayınlar → `pinned` adım **değişmez**, `latest-published` adım **yeni sürümü**
  gösterir; içerik yayından kalkar → `ContentResolutionStatus = unresolved` + `ResolvedContentId = null`.
- **AC-ASSESS-1 (D6=A)** `CompletionRule = assessment-passed` **yalnız** `ContentType == "quiz"` içerikle kabul
  edilir; aksi **400**. FU02'ye **alan eklenmez**. *Test:* quiz içerikle **201/200**, brochure içerikle **400**;
  `KnowledgeContent.cs` diff'te **yok**.
- **AC-BRANCH-1 (D7)** `BranchCondition` **authorable ama yalnız veridir**: hiçbir dal değerlendirilmez ve bir path
  branch condition olmadan **baştan sona yürünebilir**. *Test:* contract'ta `supportsBranchEvaluator` **absent**;
  koşullu adımlar lineer listede **tam** görünür; `TargetStepId` yabancı path'te → **400**;
  koşul verisi response'ta **aynen** döner.
- **AC-LIMIT-1** Doküman büyüme sınırları uygulanır ve **contract'ta ilan edilir** (`maxStepsPerPath: 200`,
  `maxBranchConditionsPerStep: 20`). *Test:* 201. adım → **400**; contract limitleri döner.
- **AC-ENGINE-0** Hiçbir endpoint/parametre öneri, skor, best-next veya completion **döndürmez**.
  *Test:* 11 yasak flag contract'ta **absent** (false bile değil); `recommend`/`bestNext`/`score` parametresi
  **yok**; completion/progress alanı **yok**.

**Sözleşme koruması**
- **AC-FU02-1..4** `KnowledgeContent` atomik kalır; `ResolvePublishedContentAsync` **imza/davranışı değişmez**;
  FU02 alanları/endpoint'leri/view'ları **dokunulmaz** (D6 dâhil); tüm değişiklik additive (§2.2).
  *Test:* FU02'nin **23 testi değişmeden PASS**; `IKnowledgeContentLinkageReader` ve `KnowledgeContent.cs`
  diff'te **yok**.
- **AC-FU03-1** Concept aggregate'leri **okunur, yazılmaz**; FU03 contract'ı ve 12 flag'i **değişmez**.
  *Test:* FU03 test suite'i **değişmeden PASS**; diff'te `Features/Knowledge/Concept/**` ve `Concept*.cs`
  **yok**; `ConceptNodeId` doğrulaması yalnız **okuma** yapar.
- **AC-BOUNDARY-1** MOD-0155 / MOD-0309 / MOD-0028-0029 / MDM / Campaign **mutate edilmez**.
  *Test:* smoke'ta before/after diff **identical**; repo scope dışına yazma **yok**.

**Vokabüler & fail-closed**
- **AC-VOCAB-1** `PathStatus` / `Source` / `StepType` / `CompletionRule` / `VersionPinPolicy` / `StepStatus`
  **in-domain** doğrulanır; set dışı değer **400**; MOD-0048 yayını **runtime ön koşulu değildir**.
  *Test:* her vokabüler için 1 geçersiz değer → **400**; contract vokabüler listesini **yayınlar**.

**UI**
- **AC-UI-1** Tüm `Views/CRM/KnowledgePaths/*.cshtml` dosyalarında `Layout = "_LayoutTenantShell"` **AÇIKÇA**
  yazılıdır. *Test:* grep ile **9 dosyada** açık layout.
- **AC-UI-2 (D3)** Klasör kanonik **Compact 9 dosya** setini taşır; `_CreateEditOffcanvas.cshtml` /
  `_DetailsQuickView.cshtml` **yoktur**; **ikinci klasör/DataTable/RESX seti yoktur**.
  *Test:* `verify_datatable_page.py --area CRM --module KnowledgePaths --reference compact --api-profile proxy`
  **tek koşu**, **yapısal FAIL yok** (archive-only modül olduğu için **6 bulk-delete kontrolü N/A**,
  FU02/FU03 emsali); `Views/CRM/KnowledgePathSteps/` **mevcut değil**.
- **AC-UI-3** Adım alt-editörü path formunun **içinde**dir (repeater); adım eklemek için **ayrı sayfaya
  gidilmez**; her adım satırı `StepOrder` ile sıralanır ve zorunlu/opsiyonel + `unresolved` içerik **rozetle**
  ayırt edilir. *Test:* `_Form.cshtml` repeater render'ı + sıralama + rozetler.
- **AC-UI-4** Adım içindeki **BranchCondition repeater** (D7) authorable'dır; `ConditionCode`/`Description`/
  `TargetStepId` girilebilir, `TargetStepId` seçenekleri **aynı path'in adımlarıyla** sınırlıdır.
  *Test:* repeater render'ı + hedef listesi yalnız aynı path adımları.
- **AC-UI-5** İçerik seçici **published + effective** FU02 içeriğini listeler; archived içerik **listelenmez**;
  seçilen içeriğin `ContentCode` + sürümü + `ContentType`'ı kullanıcıya **görünür** (D6 gerekçesi anlaşılsın).
  *Test:* archived içerik listede yok; seçim sonrası kod/sürüm/tip render edilir.
- **AC-UI-6** Kavram seçici Subject→Type→Node zincirlidir, **archived node listelenmez**; FU03 verisi **yalnız
  okunur**. *Test:* archived node hariç; hiçbir POST/PUT concept endpoint'ine gitmez.
- **AC-UI-7** `published` path'in adım alt-editörü UI'da **disabled** + gerekçe notu + `new-version` aksiyonu
  görünür (sessiz 409 sürprizi yok). *Test:* frozen path'te repeater disabled ve açıklama render edilir.
- **AC-UI-8** Hiçbir dropdown hardcoded vokabüler taşımaz; tümü `path/contract`'tan beslenir.
  *Test:* view/JS'te sabit `stepType`/`status` dizisi **yok**.
- **AC-L10N-1** 7 dil (`ar/en/es/fr/ru/tr/zh`) RESX **anahtar paritesi**; `SharedResource` menü anahtarı ×7;
  `index.l10n.js` camelCase→PascalCase köprüsü çalışır. *Test:* parite scripti + UI'da `undefined` anahtar yok.
  **Not (FU03/F-L10N dersi):** 5 dil İngilizce placeholder ile **geçilmez** — çeviriler gerçek olmalıdır.

**Yetkilendirme & routing**
- **AC-AUTH-1** Her endpoint `[Authorize]` + `[HasPermission(...)]` taşır; permission **seed edilmez**;
  anahtar seti **read / manage / publish** (D2 sonrası `path-step.manage` yok).
  *Test:* controller'larda öznitelik var; diff'te RBAC seed/role-template **yok**.
- **AC-GW-1** `ocelot.json` **değişmemiştir** ve tüm route'lar Gateway `:5000` üzerinden çalışır.
  *Test:* diff'te gateway dosyası yok; smoke tüm çağrıları `:5000`'den yapar; `DELETE`/`PATCH` → **404**.

---

## 17. Test Expectations

### 17.1 Build & statik doğrulama

- `dotnet build` **PASS**: `Diten.CrmService` + `frontend/Diten.Web` (+ Gateway derlenmeye devam eder).
- `verify_datatable_page.py . --area CRM --module KnowledgePaths --reference compact --api-profile proxy`
  → **TEK koşu**, PASS.
  - **Beklenen N/A:** archive-only modülde **6 bulk-delete kontrolü** (FU02/FU03 emsali).
  - **Beklenen yapısal FAIL: YOK** — tek yüzey + tek klasör olduğu için FU03'ün 2 hibrit FAIL'i burada
    **oluşmaz** (§11.1).
- RESX **parite** kontrolü: 1 modül × 7 dil + `SharedResource` ×7 → eksik/fazla anahtar **yok**.
- `verify_module_id.py --check-id MOD-0162-FU04 …` → exit 0 (closeout'ta tekrar).

### 17.2 Backend unit/integration testleri (`Diten.CrmService.Application.Tests`) — hedef **≥ 42 test**

| # | Küme | Adet |
|---|---|---|
| 1 | Path × create/update/archive/publish/new-version mutlu yol | 5 |
| 2 | Gömülü adım × add/update/archive mutlu yol (+ sıralı liste + çözülmüş içerik) | 5 |
| 3 | **AC-EMBED-1**: adım yazımı **tek dokümana** gider · **path** `Version`'ı artar · ikinci collection **yok** | 3 |
| 4 | **AC-EMBED-2**: archive adımı **diziden silmez** (`includeArchived` ile görünür, `active` listede yok) | 2 |
| 5 | V-P03 duplicate code+version · V-P04/05/06 archived/tutarsız referanslar | 4 |
| 6 | V-P07 effective window · V-P08 vokabüler · V-P09 archived path'e yazım · V-P16 `steps` dizisi reddi | 4 |
| 7 | V-P10 örtüşen published (409) · V-P11 boş path publish (400) · **V-P12 update-ile-publish (400, D4)** · V-P14 | 4 |
| 8 | **AC-FREEZE-1**: frozen path'te adım add/update/archive → 409 (3) + `new-version` kopyası (yeni `StepId`, `SupersedesPathId`, `PathVersion`++, kaynak değişmedi) (2) | 5 |
| 9 | V-S03/S04 duplicate order & code — **DB index olmadan handler doğrulaması** · V-S05/S06 içerik uygunluğu | 4 |
| 10 | V-S07 **dirty-check**: dokunulmamış `ContentId` ile PUT → **200**; değiştirilip archived içeriğe → **400** | 2 |
| 11 | V-S09 prerequisite (yabancı path / ileri sıra / döngü) · V-S10 required↔optional | 4 |
| 12 | V-S11 duration-met · **V-S12 assessment-passed: quiz → OK, quiz-değil → 400 (D6=A)** | 3 |
| 13 | V-S13 concept node (archived / yabancı tenant / geçerli) | 3 |
| 14 | **V-S14 (D7)** branch condition: yabancı `TargetStepId` → 400 · aynı path → OK · **veri aynen döner** | 3 |
| 15 | V-S15 cross-subject / cross-language **kabul edilir ve flag'i görünür** | 2 |
| 16 | V-S17 dangling prerequisite archive → 409 · V-S18 path archive → gömülü adımlar archived kabul (ayrı yazım yok) | 2 |
| 17 | **V-S20** limit: 201. adım → 400 · 21. branch condition → 400 | 2 |
| 18 | **AC-PIN-1**: pinned sabit · latest-published çözülür · unresolved görünür | 3 |
| 19 | **V-P15/V-S19 concurrency**: eş zamanlı iki adım yazımı → ikincisi **409** (ortak path token'ı) | 2 |
| 20 | Tenant isolation: başka tenant'ın path'i **görünmez, yazılamaz** · V-P01 payload injection | 3 |
| 21 | Contract: 13 flag `true`, **11 yasak flag absent** (false bile değil), vokabüler + **limitler** yayınlanır | 2 |
| 22 | **RegisterClassMaps:** path + `KnowledgePathStep` + `KnowledgePathBranchCondition` kayıtlı (aksi hâlde gömülü Guid'ler binary → filtreler sessizce boş) | 1 |
| 23 | `IKnowledgePathReader`: yalnız published+effective + `active` adım döner; sıralama deterministik; **skor/seçim yok** | 3 |
| 24 | **Regression:** FU02 (23 test) + FU03 test suite'i **değişmeden PASS** | mevcut suite |

### 17.3 Authenticated smoke (Gateway) — `scripts/smoke-mod0162-fu04-knowledge-path-authenticated.ps1`

Tenant `97c59330…`, login **`X-Tenant-Id` header ile** (aksi hâlde platform `…0001` token'ı gelir).
FU03 script'i şablondur. PowerShell 5.1 tuzağı: `@(… | Where-Object …).Count` sarmalayıcısı zorunlu.

```text
 1 login → token
 2 GET  path/contract                                        → 200, 13 flag true, 11 yasak flag absent, limitler dolu
 3 POST paths (FU02 subject'i ile)                           → 201  (draft)
 4 POST paths duplicate code+version                         → 409
 5 POST paths { effectiveTo < effectiveFrom }                → 400
 6 POST paths/{id}/steps (order 10, required, pinned)        → 201  (tek doküman yazımı)
 7 POST paths/{id}/steps duplicate order 10                  → 409
 8 POST paths/{id}/steps (order 20, prerequisite = step#1)   → 201
 9 POST paths/{id}/steps (prerequisite ileri sıraya)         → 400
10 POST paths/{id}/steps (required, prerequisite = optional) → 400
11 POST paths/{id}/steps (archived içerik ile)               → 400
12 POST paths/{id}/steps (assessment-passed + quiz içerik)   → 201  (D6=A)
13 POST paths/{id}/steps (assessment-passed + brochure)      → 400  (D6=A)
14 POST paths/{id}/steps (conceptNodeId = FU03 node)         → 201
15 POST paths/{id}/steps (archived concept node)             → 400
16 PUT  paths/{id}/steps/{stepId} + branchConditions[]       → 200  (D7: veri aynen döner)
17 PUT  paths/{id}/steps/{stepId} branch → yabancı TargetStepId → 400
18 GET  paths/{id}/steps                                     → 200, StepOrder sırası + ResolvedContentId dolu
19 PUT  paths/{id} { steps: [...] }                          → 400  (V-P16)
20 PUT  paths/{id} { pathStatus: "published" }               → 400  (V-P12 / D4)
21 POST paths/{id}/publish                                   → 200  (StepSetFrozenAt dolu)
22 POST paths/{id}/steps (published path'e yeni adım)        → 409
23 PUT  paths/{id}/steps/{stepId} (published path)           → 409
24 POST paths/{id}/publish (örtüşen ikinci sürüm)            → 409
25 POST paths/{id}/new-version                               → 201 (draft, PathVersion++, adımlar YENİ StepId ile kopyalandı, SupersedesPathId dolu)
26 GET  paths?status=published&effectiveAt=…                 → 200, yalnız published+effective path'ler
27 PUT  paths/{id} { tenantId: "<yabancı>" }                 → claim kazanır (yabancı tenant yazılmaz)
28 GET  paths/{başka tenant id}                              → 404
29 POST paths/{id}/steps/{stepId}/archive (prerequisite iken)→ 409
30 POST paths/{id}/steps/{stepId}/archive (bağımsız adım)    → 200, GET ?includeArchived=true → adım GÖRÜNÜR (silinmedi)
31 POST paths/{id}/archive → GET paths/{id}                  → adımlar aynı dokümanda archived kabul
32 DELETE / PATCH herhangi bir route                         → 404
33 GET  /api/crm/knowledge/path-steps                        → 404 (düz aile yok — D2)
34 FU02 içerik kaydı + FU03 concept kaydı DEĞİŞMEDİ          → before/after diff identical
35 cleanup: archive-only (**hard delete YOK**)
```

### 17.4 Browser smoke

`/CRM/KnowledgePaths` açılır; liste/filtre/create/edit/details/archive akışları çalışır; **adım alt-editörü ve
adım-içi branch repeater aynı sayfada** çalışır (ekle/düzenle/arşivle, sıralama, rozetler); frozen path'te
alt-editör disabled; publish/new-version aksiyonları çalışır; dil değiştirince (7 dil) `undefined` anahtar yok;
konsolda hata yok. **Not:** `.resx` değişiklikleri **tam fleet restart** ister.

---

## 18. Ready-for-dev Checklist

- [x] Boundary (FU01A) `approved` ve okundu; kalan sapmalar §2.1'de (S1–S4) gerekçelendi, **S5 sapma olmaktan
      çıktı** (D2 = boundary §16 önerisi), **S6 çözüldü** (D6=A)
- [x] DCP-002 kimlik kapısı **PASS** (exit 0, 2026-08-26)
- [x] Prerequisite FU02 **shipped**, FU03 **done** doğrulandı (kod üzerinden: `Features/Knowledge/Content/**`,
      `Features/Knowledge/Concept/**`, 6 concept controller'ı mevcut)
- [x] **D1–D7 kullanıcı kararları ALINDI (2026-08-26) ve pack gövdesine işlendi** — §"Açık Kararlar" kapandı
- [x] Golden Reference (DEV-0001 Compact) referans alındı; **tek yüzey** için alan sayımı **gösterildi**
      (§11.1 — 12 > 8 ⇒ Compact); gömülü adım alt-editörü ayrı yüzey **değildir** (13 alan, bilgi amaçlı)
- [x] Frontend dosya seti **tek tek** enumere edildi (§11.2 — **tek klasör**, kanonik 9 dosya + 3 JS + 7 RESX)
- [x] Frontmatter zorunlu alanların tümü dolu (`service`, `shell`, `golden_reference`, `entity_base`,
      `form_field_count`)
- [x] Layout & Shell Contract'ta Razor `Layout` **açıkça** yazıldı ve AC-UI-1'de test edilebilir madde oldu
- [x] Backend File Convention Golden Reference **naming**'i ile birebir; **dosya gruplama sapması açıkça beyan
      edildi** (§10 uyarı kutusu, F-FILE); D2 ile 4 dosya + 1 repository kaldırıldı
- [x] Validation Rules her alan/kural için yazıldı (§12 — 16 path + 20 adım kuralı); **dizi-içi unique index
      olmadığı** ve tek savunmanın handler olduğu açıkça yazıldı
- [x] Failure Path ≥ 4 senaryo (§13 — 12 senaryo: duplicate/missing/concurrency/unauthorized/frozen/cross-tenant/
      assessment/limit/…)
- [x] Authorization Convention: **3 anahtar** (read/manage/publish — D2 ile `path-step.manage` kaldırıldı) +
      policy + actor + **fallback'in SoD boşluğu** açıkça yazıldı
- [x] Gateway kararı **açık**: değişiklik **gereksiz**, wildcard doğrulandı (`ocelot.json:2245`), route seçimi
      §2.1/S3'te gerekçeli
- [x] Acceptance Criteria test edilebilir maddelere bağlandı (§16 — her madde §17'de bir teste eşlenir)
- [x] Test Expectations build + verifier (**tek** compact koşusu) + 7 dil RESX + ≥42 backend testi +
      authenticated smoke kapsıyor
- [x] Protected Paths eksiksiz (`MdmService/**` ve diğer domain servisleri, ocelot, RBAC, registry, Mongo,
      FU02/FU03 yüzeyleri dâhil) — §6
- [ ] `status: ready-for-dev` + `runtime_code_allowed: true` — **bugün AÇIK**; pack kullanıcı incelemesi için
      `draft` bırakıldı, flip ayrı kullanıcı aksiyonudur

---

## 19. Implementation Notes

Repo'dan çıkarılmış, bu FU'yu doğrudan vuran tuzaklar (D2 = embedded modele göre güncellendi):

1. **RegisterClassMaps** — `KnowledgePath` **ve embedded tipler** (`KnowledgePathStep`,
   `KnowledgePathBranchCondition`) `Persistence/DependencyInjection.cs`'e eklenmezse gömülü `Guid` alanları
   (`StepId`, `ContentId`, `ConceptNodeId`, `PrerequisiteStepId`, `TargetStepId`) binary yazılır ve filtreler
   **sessizce boş döner** (MOD-0151 FU05 / `AccountTerritoryAssignment` dersi).
2. **Tek doküman yazımı** — adım ekleme/güncelleme/arşivleme path root'unu `EntityBase.Version` kontrolüyle
   **replace** eder. Pozisyonel dizi güncellemesi (`$set: steps.$[…]`) ile tam-doküman replace **karıştırılmaz**;
   tek kod yolu korunur, aksi hâlde iki farklı concurrency davranışı doğar.
3. **Transaction gerekmez** — çok-doküman atomiklik yok; `SupportsTransactionsAsync` guard'ı ve compensation
   **bu FU'da yazılmaz** (D2 kazancı). `new-version` iki bağımsız yazımdır ve yarım path üretmez.
4. **Dizi-içi unique index YOK** — `StepOrder`/`StepCode` tekilliği Mongo index'iyle zorlanamaz; handler +
   validator **tek savunma hattıdır** ve §17.2/küme 9 bu yüzden zorunludur.
5. **Doküman büyümesi** — 16MB Mongo limiti; §4.2 sınırları (200 adım / 20 koşul) contract'ta ilan edilir ve
   V-S20 ile zorlanır. Path listesi sorgularında `Steps` alanı **projeksiyonla dışarıda bırakılır**
   (DataTable yalnız sayaç rozetleri gösterir).
6. **Parallel-array tuzağı** — `EffectiveFrom` + `EffectiveTo` (ikisi de `DateTimeOffset`) **birlikte
   index'lenmez, birlikte sort edilmez**; gerekirse in-memory sort. DateTimeOffset BSON dizisi olarak saklandığı
   için tarih karşılaştırmalarında `.Date` tuzağına da dikkat.
7. **Partial index `$ne` yasak** — `Filter.Ne(x, null)` içeren partial index servisi başlangıçta **crash-loop**'a
   sokar; `Filter.Type(...)` / `$lt` kullan (Platform 5057 dersi).
8. **Endpoint'ler fleet restart'a kadar 404** — yeni controller'lar servis yeniden başlamadan görünmez; `.resx`
   değişiklikleri de **tam restart** ister. Build kilidinde `-t:CoreCompile` yöntemi.
9. **L10n bridge** — `index.l10n.js` camelCase→PascalCase dönüşümü atlanırsa `window.L10n` anahtarları
   `undefined` döner (toast "(undefined: corrId)").
10. **Freeze mantığı tek yerde** — `StepSetFrozenAt` kontrolü `KnowledgePathValidation`'da toplanmalı; hem adım
    handler'ları hem path update handler'ı aynı yardımcıyı çağırmalı (iki kopya = iki farklı davranış).
11. **`unresolved` içerik gizlenmez** — çözülemeyen adım listeden düşürülürse kullanıcı sıradaki boşluğu göremez;
    FU01A §6.1'in "sessiz sürüm kayması yasak" kuralı bunu kapsar.
12. **D6 dokunuş sınırı** — `assessment-passed` doğrulaması FU02 içeriğini **okur**; `KnowledgeContent.cs`
    **açılmaz**, `AssessmentRequired` **eklenmez**.
13. **MOD-0155 açılmaz** — bu FU'nun tüketicisi ileride MOD-0155'tir; ancak `IKnowledgePathReader`'ı **tüketen**
    kod bu FU'da yazılmaz (yalnız seam yayınlanır).

---

## 20. Follow-up Items

| # | Follow-up | Neden |
|---|---|---|
| **F-RBAC** | **MOD-0162-FU04-RBAC** — `crm.knowledge.path.{read,manage,publish}` katalog + grant; dev fallback'in kaldırılması | RBAC en sona bırakıldı (FU01A §9); fallback altında **D4'ün SoD'si uygulanamıyor** (§14) |
| **F-RD** | MOD-0048 set publish: `knowledge-path-step-type` / `-status` / `-completion-rule` / `-source` | Hardcoded enum yasağı (FU01A §5); dev'de in-domain yürür, çelişki yok |
| **F-ASSESS** | Quiz **dışında** bir değerlendirme kavramı gerekirse (`AssessmentRequired` benzeri) FU02 sözleşme değişikliği | D6=A boundary kuralını `ContentType == "quiz"` ile karşılar; daha geniş bir değerlendirme tanımı **FU02 yetkisi** ister |
| **F-CONCEPT-STEP** | "İçeriksiz, yalnız-kavram adımı" isteniyorsa **FU01A boundary değişikliği** | Bugün `ContentId` zorunlu (§2.1/S1); model değişikliği boundary yetkisi ister |
| **F-CAP** | Adım/branch limitlerinin (200/20) gerçek kullanım verisiyle gözden geçirilmesi | Embedded model doküman boyutuna bağlıdır; limit contract'ta ilan edilir, ileride ayarlanabilir |
| **F-JOURNEY** | **MOD-0162-FU01B EngagementJourney implementation FU** — çok-oturumlu aşama zinciri | Path tek oturumun sırasıdır; `visit-N` mantığı buraya gömülmez |
| **F-DETAIL** | **Digital Detailing / Learning Execution** — branch evaluator, dinamik öneri, gösterim kaydı | FU01A §8/§13'te kasıtlı kapalı; D7 ile yazılan `BranchCondition` verisi **orada** değerlendirilir |
| **F-COMPLETION** | **MOD-0309 completion sözleşmesi** — `CompletionRule` ↔ completion record eşlemesi | Beyan burada, ölçüm orada (FU01A §11) |
| **F-155** | MOD-0155 tüketimi: visit objective → önerilen path, gösterim evidence'ı | Seam bu FU'da yayınlanır, tüketici kod **açılmaz** (FU01A §10) |
| **F-WF** | Path approval workflow (MOD-0023) — `review`/`approved` bugün yalnız metadata | FU01A §7.2; en sona bırakıldı |
| **F-FILE** | Knowledge feature klasöründe komut-başına-dosya'ya dönüş (Golden Reference birebir) | §10 sapması FU02/FU03 ile ortak; tek seferde tüm Knowledge ailesi için yapılmalı |
| **F-MIG** | Legacy path/sequence crosswalk (legacy `UCLNBook` → `KnowledgePath`) | FU03/F-MIG ile ortak; bu pack yalnız **greenfield authoring** açar |
| **F-STATUS** | Closeout'ta `execution/registries/module-implementation-status.md` satırı | Kod-izli modül durum takibi (module-pack-standard §16) — **yalnız kullanıcı onayıyla** |
