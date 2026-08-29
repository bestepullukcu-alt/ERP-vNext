---
id: MOD-0162-FU05
name: ContentEngagementJourney Runtime + UI
parent: MOD-0162
parent_name: Knowledge Base
implements_boundary: MOD-0162-FU01B
siblings: MOD-0162-FU01, MOD-0162-FU01A, MOD-0162-FU01B, MOD-0162-FU01C, MOD-0162-FU02, MOD-0162-FU03, MOD-0162-FU04
domain: commercial-suite
service: Diten.CrmService + frontend/Diten.Web
shell: tenant
golden_reference: compact
entity_base: EntityBase
status: ready-for-dev
runtime_code_allowed: true
canonical_name: "ContentEngagementJourney / ContentEngagementJourneyStage (F1 resolved 2026-08-26 — MOD-0162-FU01B §17/F1)"
runtime_code_scope: "NONE (draft). Kullanıcı onayı ile `ready-for-dev` + `runtime_code_allowed: true` olduğunda kapsam: ContentEngagementJourney aggregate root runtime — aşamalar journey DOKÜMANI İÇİNDE embedded (FU04/D2 deseni) — (CRUD-minus-delete + archive + effective dating + journey versiyonlama + published aşama-seti dondurma + stage→KnowledgePath bağlama + PathVersionPinPolicy çözümleme + in-domain vokabüler + contract) `Diten.CrmService` içinde VE CRM Admin → Knowledge → Content Engagement Journeys TEK Compact sayfası (gömülü aşama alt-editörü + aşama-içi BranchCondition repeater) `frontend/Diten.Web` içinde. Stage advancement engine, branch evaluator, recommendation engine, journey progress / current-stage state, journey target assignment, visit/route planning, digital detailing, completion tracking, FU02 `KnowledgeContent` alan/imza değişikliği, FU03 concept aggregate mutation, FU04 `KnowledgePath` / `IKnowledgePathReader` mutation, MDM write, Gateway config değişikliği, RBAC seed/grant, MOD-0048 publish, registry write ve Mongo hand-edit YASAKTIR."
owner: module-pack-author
branch: feature/crm/mod-0162-fu05-content-engagement-journey-runtime-ui
started: 2026-08-26
target: TBD (kullanıcı onayı sonrası)
form_field_count: 12   # ContentEngagementJourney = TEK golden-reference yüzeyi; kullanıcı-form alanı sayımı §11.1'de türetilir (12 > 8 → Compact). Gömülü aşama alt-editörü ayrı yüzey DEĞİLDİR.
dependencies:
  - MOD-0162-FU01B (approved boundary, 2026-08-26 — §3–§13 sözleşmesi BURADA implement edilir; F1 kanonik adı uygulanır)
  - MOD-0162-FU02 (KnowledgeContent + Subject/Topic/AudienceProfile runtime — SHIPPED; Subject/Topic/AudienceProfile referansları buradan gelir, sözleşmesi KIRILMAZ)
  - MOD-0162-FU04 (KnowledgePath runtime + UI — SHIPPED/DONE; stage → published+effective path bağı; FU04 yüzeyi ve `IKnowledgePathReader` imzası DEĞİŞMEZ)
  - MOD-0162-FU03 (Concept Graph runtime + UI — DONE; bu FU concept aggregate'lerine DOKUNMAZ)
  - MOD-0166 (boundary — Journeys & Automation; trigger/suppression/kanal/run-log SoR'u ORADA, bu FU'da YOK — FU01B §2.1)
  - MOD-0155 (consumer — visit execution, current-stage progress, gösterim evidence'ı; bu FU yalnız read-only seam yayınlar)
  - MOD-0309 (consumer — completion / score / attendance / certificate; `AdvancementRule` beyanı burada, ölçüm orada)
  - MOD-0165 / MOD-0167 (frequency + targeting — "ne sıklıkla" ve "kime"; journey target assignment bu FU'da YOK)
  - MOD-0048 (reference data — D-VOCAB=A: runtime ön koşulu DEĞİL; setler ayrı operatör işi → F-RD; §4.4)
  - MOD-0018 (RBAC — yalnız tüketim; seed/grant bu pack'te YOK, en sona bırakıldı)
  - DEV-0001 (Golden Reference Compact — tek yüzey, tek klasör)
---

# MOD-0162-FU05 — ContentEngagementJourney Runtime + UI

> **⛔ DRAFT — KOD YETKİSİ YOKTUR (`runtime_code_allowed: false`).**
> Bu pack, MOD-0162-FU01B **boundary**'sinin (approved, 2026-08-26) **implementation** karşılığıdır. AGENTS.md onay
> kapısı gereği `draft` pack yalnızca planlama dokümanıdır; `@orchestrator` bu pack ile kod yazamaz.
>
> **✅ `D-VOCAB` RESOLVED = A (in-domain fail-closed) — kullanıcı kararı 2026-08-26.** Vokabüler
> `Domain/Entities/ContentEngagementJourney.cs` içinde static class; set dışı değer → 400; MOD-0048 publish'i runtime
> ön koşulu **değildir** (FU02/FU03/FU04 emsali; setler ayrı operatör işi olarak aynı vokabülerle yayınlanır → F-RD).
> Pack gövdesi zaten A'ya göre yazılmıştı — içerik değişmedi. Tek açık karar kapandı.
>
> **Desen:** FU01 (boundary) → **FU02**, FU01C (boundary) → **FU03**, FU01A (boundary) → **FU04** ile birebir
> aynı. FU01B §15 *"implementation FU (MOD-0162-FU05) ayrı yetkilendirilir"* der; FU04 §20/F-JOURNEY bu FU'yu
> adıyla ister. Bu dosya odur.
>
> **Kanonik ad (F1, kullanıcı/EA kararı 2026-08-26):** **`ContentEngagementJourney` / `ContentEngagementJourneyStage`**.
> Vokabüler, endpoint ve permission anahtarları **`content-engagement-journey`** formunu kullanır; FU01B gövdesindeki
> tarihsel `EngagementJourney` etiketi bu adla **süperseded**'dir. Ad, MOD-0166 "Journeys & Automation" (otomasyon
> orkestrasyonu) ve MOD-0113 "Journey Mapping"den **kalıcı olarak** ayrılmıştır (FU01B §2.1).
>
> **DCP-002 kimlik kapısı — PASS (2026-08-26):**
> `py .antigravity/scripts/verify_module_id.py . --check-id MOD-0162-FU05 --name "ContentEngagementJourney Runtime + UI" --parent MOD-0162`
> → `OK  MOD-0162-FU05: proven against Blueprint/registry.` (**exit 0**).
>
> Otorite sırası: **Blueprint Excel** > MOD-0162-FU01B (approved boundary) > bu pack >
> [Domain Config](../domain-config.md) > `AGENTS.md` > `.antigravity/rules/`.

---

## 1. Module Summary

FU02 **tekil içeriği**, FU03 **kavram bağlarını**, FU04 **tek oturum içindeki sırayı** kapattı. Açık kalan tek soru
**çok-oturumlu ilerlemedir**: *1. ziyarette hangi path, 2. ziyarette hangi path, doktor ikna olmadıysa 3. ziyarette
hangi aşama?* Bu FU onu — ve yalnız onu — çalışır hâle getirir.

```text
KnowledgeContent               = tekil içerik                                    (FU02, mevcut)
ConceptNode/Graph              = kavramlar nasıl bağlanır                        (FU03, mevcut)
KnowledgePath                  = TEK oturum içindeki içerik sırası               (FU04, mevcut)
ContentEngagementJourney       = ÇOKLU oturum boyunca ilerleyen aşamalar         (BU FU — aggregate root)
  └── Stages[]                 = journey DOKÜMANI İÇİNDE embedded aşama listesi  (BU FU — FU04/D2 deseni)
        └── RecommendedKnowledgePathId → FU04 published+effective KnowledgePath  (BAĞ; adım detayı KOPYALANMAZ)
        └── BranchConditions[] = aşama içinde embedded repeater                  (BU FU — veri-only, evaluator YOK)
```

| Cevapladığı soru | Sahip |
|---|---|
| Bu journey hangi subject/topic/audience için, hangi sürümü geçerli, ne zaman geçerli? | **Bu FU** |
| Aşamalar hangi sırada, hangisi zorunlu, hangisi tekrarlanabilir? | **Bu FU** |
| Her aşamada **hangi path** uygulanacak ve hangi path sürümüne sabitlenmiş? | **Bu FU** |
| Aşama ilerletme kuralı ne olarak **beyan** edildi (`AdvancementRule`)? | **Bu FU** (beyan; **değerlendirilmez**) |

| Cevaplamadığı soru | Sahip |
|---|---|
| Tek oturumda hangi içerik hangi sırayla | MOD-0162-FU04 |
| **Şu an hangi aşamadayız / aşama nasıl ilerler / dal nasıl değerlendirilir** | **YOK** — F-DETAIL (Digital Detailing / Learning Execution) |
| Hangi doktora/öğrenciye hangi journey atanır | MOD-0165 / MOD-0167 + MOD-0155 (F-TARGET) |
| Ne sıklıkla gidilecek | MOD-0165 / MOD-0167 (`VisitFrequencyPolicy`) |
| Kime, ne zaman gidilecek; gerçekte ne oldu | MOD-0155 |
| Tamamlandı mı, kim tamamladı, ne skor aldı | MOD-0309 |
| Trigger / suppression / kanal / run log (**otomasyon journey'i**) | **MOD-0166** — ad çakışması FU01B §2.1'de kalıcı çözüldü |
| Dosya / binary / controlled document | MOD-0028 / MOD-0029 |

**Temel mimari kural (FU01B §1):** *`ContentEngagementJourney` bir **şablon**dur, bir **execution** değildir.*
Current stage, ilerletme, visit execution ve usage tracking bu FU'da **yoktur**.

**Reddedilen beş model (FU01B §1 — burada da geçerli):** `VisitPlan`'a gömülü journey · `KnowledgePath`'e sızmış
`visit-1/visit-2/visit-3` mantığı · `BrandProductJourney` · `Contact.CurrentJourneyStage` ·
`Account.CurrentJourneyStage`.

---

## 2. Ownership and Boundaries

**In-scope:** `ContentEngagementJourney` aggregate root'u ve **içindeki embedded `Stages[]`** · CRUD-minus-delete
(create/read/update/archive; **DELETE ve PATCH yok**) · journey versiyonlama + published sürümde **aşama setinin
dondurulması** · ayrı publish endpoint'i (SoD) + `new-version` klonu · effective dating ·
`StageOrder` / `FallbackStageId` / required↔optional doğrulaması · **stage → FU04 published+effective
`KnowledgePath` bağı** + `PathVersionPinPolicy` ile deterministik sürüm çözümleme · tekrar (repeat/revisit)
görünürlüğü · in-domain vokabüler + contract endpoint · read-only tüketim seam'i
(`IContentEngagementJourneyReader`) · CRM Admin **tek** Compact sayfa · 7 dil RESX.

**Out-of-scope (§13 tam liste):** stage advancement engine · branch evaluator · recommendation / best-next-stage ·
**journey progress / current-stage state** · journey target assignment · campaign/frequency/segmentation engine ·
visit & route planning · digital detailing · content usage tracking · completion/score/certificate · quiz motoru ·
MOD-0023 approval workflow · MOD-0166 otomasyon (trigger/suppression/kanal/run-log) · file upload/render/preview ·
Account/Contact/territory/MDM mutation · hard delete.

### 2.1 Boundary'den sapma kararları (FU01B ↔ bu pack)

Boundary `approved` olduğu için **boundary kazanır**; sapmalar burada gerekçelenir (FU03 §2 / FU04 §2.1 deseni).

| # | Boundary ifadesi | Bu pack (kazanan) | Gerekçe / durum |
|---|---|---|---|
| **S1** | FU01B §3 alan adı: `Version` | Alan adı **`JourneyVersion`** | `module-pack-standard.md` §14 (satır 387): *"iş mantığına ait versiyon alanları kesinlikle `Version` olarak adlandırılamaz; `Version` teknik concurrency için rezerve"*. Emsal: FU02 `ContentVersion`, FU04 `PathVersion`. `EntityBase.Version` **tek** optimistic-concurrency token'ı olarak kalır |
| **S2** | FU01B §4: `EngagementJourneyStage` ayrı satır tablosu | Aşama = **embedded entity** (`ContentEngagementJourney.Stages[]`) | **SAPMA DEĞİL — boundary önerisi benimsendi.** FU01B §16: *"öneri: tek aggregate (journey root + stage child)"*. FU04/D2 ile birebir aynı desen: tek collection · tek `EntityBase.Version` · stage repository/controller/DataTable **YOK** · çok-doküman transaction ve cascade **yapısal olarak gereksiz** |
| **S3** | FU01B §9 örnek route: `/api/crm/engagement-journeys` | Route **`/api/crm/knowledge/content-engagement-journeys`** | (a) F1 kanonik adı `content-engagement-journey` formunu **zorunlu** kılar; (b) Gateway'de `/api/crm/knowledge/{everything}` wildcard'ı **zaten var** (`ocelot.json:2245-2260`, `GET/POST/PUT/OPTIONS`) — `/api/crm/engagement-journeys` bu wildcard'ın **dışına düşer** ve protected `ocelot.json` değişikliği isterdi. FU01B §9 zaten *"route'lar integration-agent yetkisindedir, bu pack route açmaz"* der. FU04/S3 ile aynı gerekçe |
| **S4** | FU01B §4: aşama tablosunda lifecycle alanı **yok** | Embedded aşamaya **`StageStatus`** (`active`/`archived`) + `ArchivedAt`/`ArchivedBy` **eklenir** | FU01B §3 hard delete'i **yasaklar**. Lifecycle alanı olmayan gömülü nesne yalnız **diziden silinerek** kaldırılabilirdi → de-facto hard delete. Archive = tek kaldırma yolu; archived aşama sıralamadan ve tüketimden düşer, **aynı dokümanda** history olarak kalır (FU04/S4 emsali) |
| **S5** | FU01B §4: `BranchCondition` **tekil** optional alan | **`BranchConditions[]`** — aşama içinde embedded repeater (`ConditionCode` + `Description?` + `TargetStageId?`) | FU04/D7 ile **birebir hizalama**: aynı ailede iki farklı dallanma veri şekli olmaz. Anlamsal genişleme yok — **hiçbir koşul değerlendirilmez**; `TargetStageId` yalnız **aynı journey** içinde olabilir (referansel akıl sağlığı). Aşama başına **max 20** (§4.2) |
| **S6** | FU01B §3: `CampaignId` · `BrandId` · `ProductId` · `SegmentId` = *optional / future* | Bu FU'da **hiç açılmaz** (entity'de **yok**, DTO'da **yok**) | Dört doğrulanmayan `Guid` kolonu = **sahte FK**. Gerekçe zinciri: Brand/Product master boundary **hâlâ yetkilendirilmemiş** (FU01B/F2), Segment SoR'u MOD-0167'de ve **kurulmamış**, `CampaignId` doğrulaması MOD-0165 Campaign runtime'ını knowledge pack'ine çekerdi ve FU01B §8/§13 **journey target assignment'ı açıkça yasaklar**. Emsal: FU04 path'e **hiçbir** pharma/campaign alanı koymadı. Boundary "zorunlu değildir, boşken journey tam çalışır" der → **yokluk sözleşmeyi bozmaz**. Gerekirse additive follow-up → **F-CAMPAIGN-LINK** |
| **S7** | FU01B §4: `MinVisitNumber` / `MaxVisitNumber` | Aynen alınır, **yalnız boundary metadata** | Runtime scheduling **yok** (FU01B §7); yalnız `Max < Min` → **400** ve `>= 1` aralık doğrulaması yapılır. Tüketiciye **veri olarak** geçer |
| **S8** | FU01B §5.2: *"vokabülerler MOD-0048 set'i olarak yönetilir, set yayınlanmadan fail-closed 400"* | ✅ **RESOLVED = A (in-domain fail-closed), kullanıcı kararı 2026-08-26.** Bilinçli, belgeli sapma | FU02/FU03/FU04'ün üçü de in-domain yürüyor; MOD-0164-FU02'nin MOD-0048 sapması **vokabüler çelişkisi** üretmişti; MOD-0048 bağımlılığı feature'ı operatör publish'ine kilitlerdi. Setler ayrı yayınlanır (F-RD) |

### 2.2 FU04 sözleşme koruması (kırmızı çizgi)

- **AC-FU04-1** `KnowledgePath` / `KnowledgePathStep` / `KnowledgePathBranchCondition` **okunur, yazılmaz**;
  hiçbir alan eklenmez/yeniden adlandırılmaz; FU04 endpoint'leri, view'ları ve JS'i **değişmez**.
- **AC-FU04-2** `IKnowledgePathReader` **imzası ve davranışı değişmez**; seam **genişletilmez**. FU05'in
  `latest-published` çözümlemesi kendi read-only resolver'ı ile `IKnowledgePathRepository` üzerinden yapılır —
  seam'e **`PathCode` ile arama parametresi eklenmez** (FU04'ün `KnowledgePathCriteria`'sı
  subject/topic/audience/language/effectiveAt taşır; kod teyidi: `Features/Knowledge/Path/IKnowledgePathReader.cs`).
- **AC-FU04-3** FU04 contract'ının **13 flag'i ve 11 yasak-flag disiplini değişmez**; FU05 kendi contract'ını
  **ayrı** endpoint'te yayınlar.
- **AC-FU04-4** Stage, path'in **adım detaylarını kopyalamaz** — yalnız `RecommendedKnowledgePathId` +
  provenance `PathCode` taşır (FU01B §4/§10).

### 2.3 FU02 / FU03 sözleşme koruması

- FU02 `KnowledgeContent` · `Subject` · `Topic` · `AudienceProfile` **okunur, yazılmaz**; `ContentVersion`,
  `IKnowledgeContentLinkageReader` ve FU02 view'ları **dokunulmaz**. FU05 içeriğe **doğrudan referans vermez** —
  içerik yalnız **path'in içindedir** (iki katman arası sızma yok).
- FU03 concept aggregate'leri (`ConceptNode`, `ConceptType`, `ConceptRelationship`, `ConceptChainTemplate`,
  `KnowledgeContentConceptLink`) bu FU'da **ne okunur ne yazılır** — kavram bağı **adım** seviyesindedir (FU04),
  **aşama** seviyesinde değildir. Diff'te `Features/Knowledge/Concept/**` **bulunmaz**.

### 2.4 MOD-0166 ad/sahiplik sınırı (canlı capability)

| | **Bu FU — `ContentEngagementJourney`** | **MOD-0166 — Automation Journey** |
|---|---|---|
| Doğası | **İçerik ilerleme şablonu** | **Otomasyon orkestrasyonu** |
| Yürütücü | **İnsan** (MR ziyareti, eğitmen/öğrenci oturumu) | **Sistem** (tetiklenen otomatik akış) |
| İçerir | Stage + `RecommendedKnowledgePathId` | Trigger, wait, suppression, kanal aksiyonu, run log |
| Bu FU'da | — | **Trigger yok · aksiyon yok · kanal yok · suppression yok · run log yok · runtime state yok** |

`journey` kelimesi tek başına hiçbir runtime literal'inde kullanılmaz: collection, route, permission, vokabüler ve
contract flag'lerinin tamamı **`content-engagement-journey` / `ContentEngagementJourney`** formundadır. Bu, FU01B
§2.1 ve F1'in kalıcı uygulamasıdır.

---

## 3. Owned Objects

| Nesne | Tip | Sahiplik |
|---|---|---|
| `ContentEngagementJourney` | **Aggregate root** (`EntityBase`) — tek collection | **Bu FU** |
| `ContentEngagementJourneyStage` | **Embedded entity** (`…Journey.Stages[]`) — aggregate **değil**, kendi `EntityBase`'i **yok** (S2) | **Bu FU** |
| `ContentEngagementJourneyBranchCondition` | Embedded value object (`Stages[].BranchConditions[]`) — S5 | **Bu FU** |
| `ContentEngagementJourneyStatuses` / `Sources` / `StageStatuses` / `StageTypes` / `AdvancementRules` / `PathPinPolicies` | Vokabüler (§4.4 / D-VOCAB) | **Bu FU** |
| `ContentEngagementJourneyReasonCodes` | Reason code kataloğu | **Bu FU** |
| `IContentEngagementJourneyReader` | Read-only tüketim seam'i (MOD-0155 / MOD-0309 için) | **Bu FU** |
| `ContentEngagementJourneyContract` | Contract endpoint DTO'ları + flag'ler + vokabüler + limitler | **Bu FU** |
| Commands / Queries / Handlers / Validators / **tek** repository | §10 | **Bu FU** |
| `/CRM/ContentEngagementJourneys` | **Tek** frontend proxy controller + **tek** Compact view seti | **Bu FU** |
| `crm.knowledge.content-engagement-journey.{read,manage,publish}` | **TANIM ONLY** (seed/grant YOK) | **Bu FU** |

**Sahiplenilmeyen (yalnız referans/okuma):** `KnowledgePath` + gömülü adımları (FU04) · `Subject` / `Topic` /
`AudienceProfile` (FU02) · `KnowledgeContent` (FU02 — **dolaylı**, path üzerinden) · concept aggregate'leri (FU03 —
**hiç dokunulmaz**) · `Campaign` (MOD-0165) · `VisitFrequencyPolicy` (MOD-0165/0167) · visit/route (MOD-0155) ·
completion/score (MOD-0309) · otomasyon journey'i (MOD-0166) · dosya/binary (MOD-0028/0029) · reference data (MOD-0048).

---

## 4. Entity Fields

Ortak kurallar: `TenantId` **JWT claim'inden** (payload'da **asla**; gönderilirse sessizce yok sayılır) ·
`CreatedAt/By` + `UpdatedAt/By` zorunlu · `EntityBase.Version` **teknik concurrency token**'dır, iş versiyonu
değildir (§2.1/S1) · **hard delete YOK** · archived kayıt update kabul etmez (**409**) ·
`EffectiveTo < EffectiveFrom` → **400** · iki `DateTimeOffset` alanı **birlikte index'lenmez/sort edilmez**
(CRM parallel-array tuzağı).

### 4.1 `ContentEngagementJourney` (aggregate root)

| Alan | Tip | Zorunlu | Kural |
|---|---|---|---|
| `Id` (JourneyId) | `Guid` | ✔ | `EntityBase.Id` |
| `TenantId` | `Guid` | ✔ | claim; DTO'da **yok** |
| `JourneyCode` | `string` | ✔ | Sürümler arası **stabil** iş anahtarı; rename edilmez (rename `JourneyName`'den yapılır); max 100 |
| `JourneyName` | `string` | ✔ | max 200 |
| `Description` | `string?` | ✖ | max 2000 |
| `SubjectId` | `Guid` | ✔ | **Zorunlu** (FU01B §3) — FU02 Subject; archived subject → **400** |
| `TopicId` | `Guid?` | ✖ | Verilirse `SubjectId`'ye ait olmalı → aksi **400** |
| `AudienceProfileId` | `Guid?` | ✖ | Yoksa journey **genel**; uydurma profil atanmaz |
| `Objective` | `string` | ✔ | max 500 — yolculuğun amacı (FU01B §3) |
| `LanguageCode` | `string?` | ✖ | Yoksa aşamaların path dili belirleyicidir; karışık dilli journey **görünür** olur (`IsMixedLanguage`) |
| `JourneyVersion` | `string` | ✔ | **İş versiyonu** (§2.1/S1); aynı `JourneyCode` altında çoklu sürüm |
| `JourneyStatus` | `string` | ✔ | `draft` · `review` · `approved` · `published` · `inactive` · `archived` — fail-closed (§4.4) |
| `EffectiveFrom` | `DateTimeOffset` | ✔ | |
| `EffectiveTo` | `DateTimeOffset?` | ✖ | null = açık uçlu |
| `Source` | `string` | ✔ | `manual` · `campaign` · `training` · `legacy-import` · `external` · `other` |
| **`Stages`** | **`List<ContentEngagementJourneyStage>`** | ✔ (boş olabilir) | **EMBEDDED aşama listesi (S2)** — §4.2; ayrı collection **yok** |
| `StageSetFrozenAt` | `DateTimeOffset?` | ✖ (türetilmiş) | Publish anında set edilir; §7/§12 dondurma kanıtı (FU01B §5.1) |
| `PublishedAt` / `PublishedBy` | `DateTimeOffset?` / `string?` | ✖ | Publish audit'i |
| `SupersedesJourneyId` | `Guid?` | ✖ | `new-version` ile üretilen sürümün kaynağı; **provenance**, zincir motoru değil |
| `ArchivedAt` / `ArchivedBy` | `DateTimeOffset?` / `string?` | ✖ | Soft lifecycle |
| `CreatedAt/By` · `UpdatedAt/By` | | ✔ | Standart audit seti |
| `Version` (`EntityBase`) | `int/long` | ✔ | **Tek** optimistic concurrency token — **aşama düzenlemeleri de bunu artırır** (S2 kazancı) |

**Türetilmiş (persist edilmez, response'ta görünür):** `ActiveStageCount` · `RequiredStageCount` ·
`RepeatableStageCount` · `IsMixedLanguage` · `IsMixedSubject` · `HasUnresolvedStagePath` · `HasRepeatedPaths`
(FU01B §7 "tekrar raporlanabilir olmalı" — read projection, yeni aggregate **gerektirmez**).

**Pharma/campaign alanı yok (§2.1/S6):** `CampaignId` · `BrandId` · `ProductId` · `SegmentId` **tanımlanmaz**.
Journey pharma dışı subject'leri (Almanca kursu, SOP/QMS eğitimi, onboarding) **birinci sınıf** destekler.

### 4.2 `ContentEngagementJourneyStage` — **embedded entity** (`…Journey.Stages[]`, S2)

> **Aggregate değildir.** Kendi collection'ı, kendi `TenantId`'si, kendi `EntityBase.Version`'ı ve kendi
> repository'si **yoktur**; `JourneyId` alanı da **yoktur** (aşama zaten journey dokümanının içindedir). Tüm aşama
> yazımları **journey root'u üzerinden**, **tek doküman** yazımı olarak yapılır.

| Alan | Tip | Zorunlu | Kural |
|---|---|---|---|
| `StageId` | `Guid` | ✔ | Doküman içinde üretilir; journey içinde unique; `new-version` kopyasında **yeniden üretilir** (§12/V-J14) |
| `StageOrder` | `int` | ✔ | Journey içinde **unique** (active aşamalar arası) → aksi **409**; boşluk serbest (10/20/30) |
| `StageCode` | `string` | ✔ | Journey içinde stabil, makine-okunur; sıra eşitliğinde **tie-break anahtarı** |
| `StageName` | `string` | ✔ | max 200 |
| `StageObjective` | `string` | ✔ | max 500 — aşamanın amacı (FU01B §4) |
| `StageType` | `string?` | ✖ | Opsiyonel vokabüler (FU01B §5.2 *"opsiyonel"*) — §4.4; set dışı değer → **400** |
| `RecommendedKnowledgePathId` | `Guid` | ✔ | **published + effective** FU04 `KnowledgePath` (§8.3) → aksi **400**; archived path → **400** |
| `PathCode` | `string` | ✔ (türetilir) | Yazımda path'ten kopyalanır; `latest-published` çözümlemesinin anahtarı + provenance. **Path'in adımları kopyalanmaz** (§2.2/AC-FU04-4) |
| `PathVersionPinPolicy` | `string` | ✔ | `pinned` (**varsayılan**) · `latest-published` — §8.3 |
| `IsRequired` | `bool` | ✔ | `published` journey **en az bir** `active` + `IsRequired=true` aşama içermeli → aksi **400** (FU01B §4) |
| `Repeatable` | `bool` | ✔ | Varsayılan **`false`** (FU01B §7); `true` = aynı aşama birden fazla oturumda uygulanabilir |
| `MinVisitNumber` | `int?` | ✖ | ≥ 1; **yalnız boundary metadata** (§2.1/S7) |
| `MaxVisitNumber` | `int?` | ✖ | ≥ 1 ve `>= MinVisitNumber` → aksi **400**; scheduling **yok** |
| `AdvancementRule` | `string?` | ✖ | **Optional/future metadata — DEĞERLENDİRİLMEZ** (§6); vokabüler dışı değer → **400** |
| `FallbackStageId` | `Guid?` | ✖ | **Aynı journey** içinde + **kendisi olamaz** → aksi **400**; **geriye işaret edebilir** (itiraz → pekiştirmeye dönüş; FU01B §4) — **yorumlanmaz** |
| `BranchConditions` | `List<…BranchCondition>` | ✖ | §4.3 — **değerlendirilmez** (S5); aşama başına **max 20** |
| `Notes` | `string?` | ✖ | max 2000 |
| `StageStatus` | `string` | ✔ | `active` · `archived` (§2.1/S4) — form alanı değil, archive aksiyonuyla değişir |
| `ArchivedAt` / `ArchivedBy` | `DateTimeOffset?` / `string?` | ✖ | Archived aşama **diziden silinmez**, dokümanda kalır |
| `CreatedAt/By` · `UpdatedAt/By` | | ✔ | Aşama-seviyesi audit (aynı doküman içinde) |

**Türetilmiş (persist edilmez, response'ta görünür — sessiz çözümleme yasak):**
`ResolvedKnowledgePathId` · `ResolvedPathVersion` · `ResolvedPathName` · `PathResolutionStatus`
(`pinned` · `resolved-latest` · **`unresolved`**) · `ResolvedPathStepCount` (yalnız sayaç — **adım listesi
kopyalanmaz**) · `IsCrossSubjectStage` · `IsCrossLanguageStage` · `PathUsageCountInJourney` (FU01B §7 tekrar raporu).

**Doküman büyüme sınırı (embedded model gereği):** journey başına **max 100 aşama**, aşama başına **max 20 branch
condition** → aşımda **400** (V-S18). Gerekçe: Mongo 16MB doküman limiti + tek-sayfa editör okunabilirliği. Limitler
contract'ın `limitations` listesinde **yayınlanır** (sürpriz yok).

### 4.3 `ContentEngagementJourneyBranchCondition` (embedded repeater — S5: authorable, veri-only)

| Alan | Tip | Zorunlu | Kural |
|---|---|---|---|
| `ConditionCode` | `string` | ✔ | Serbest kod (ör. `asks-clinical-evidence`, `price-objection`, `not-convinced`) |
| `Description` | `string?` | ✖ | max 500 |
| `TargetStageId` | `Guid?` | ✖ | **Aynı journey'de** olmalı → aksi **400** (V-S15; referansel akıl sağlığı, **yorum yok**) |

**Zorunlu invariant (FU01B §6):** bir journey, `AdvancementRule` / `BranchCondition` / `FallbackStageId`
**olmadan da** `StageOrder` sırasıyla **baştan sona yürünebilir** olmalıdır — lineer geçiş **eksiksizdir**. Bu FU
koşulları **veri olarak** taşır ve tüketiciye **veri olarak** geçirir; **hiçbir dal değerlendirilmez**
(`supportsStageAdvancementEngine` ve `supportsBranchEvaluator` contract'ta **absent**).

### 4.4 Vokabüler — ✅ **`D-VOCAB` RESOLVED = A (in-domain fail-closed) — kullanıcı kararı 2026-08-26**

**Seçenek A — in-domain fail-closed (ÖNERİLEN; gövde buna göre yazıldı).** FU02/FU03/FU04 emsali: vokabüler
`Domain/Entities/ContentEngagementJourney.cs` içinde static class olarak yaşar, set dışı değer → **400**, runtime
**hiçbir zaman** yayınlanmamış bir MOD-0048 seti yüzünden fail-open **olmaz** ve MOD-0048 publish'i **runtime ön
koşulu değildir** (ayrı operatör işi → **F-RD**; setler bu vokabülerin **aynısıyla** yayınlanır).

```text
ContentEngagementJourneyStatuses      : draft · review · approved · published · inactive · archived
ContentEngagementJourneySources       : manual · campaign · training · legacy-import · external · other
ContentEngagementJourneyStageStatuses : active · archived
ContentEngagementJourneyPathPin       : pinned · latest-published
ContentEngagementJourneyAdvancementRules (7, FU01B §6 örneklerinden):
        none · visit-completed · required-steps-acknowledged · objection-recorded ·
        assessment-passed · manager-manual · repeat-until-condition-met
ContentEngagementJourneyStageTypes (opsiyonel alan, 12):
        awareness · interest · clinical-evidence · objection-handling · reinforcement · commitment ·
        follow-up · onboarding · lesson · practice · assessment · closing
```

**Seçenek B — MOD-0048 fail-closed (FU01B §5.2'nin lafzı).** Vokabüler MOD-0048 reference-data set'lerinden
(`content-engagement-journey-status` · `-source` · `-stage-type` · `-advancement-rule` · `-path-pin-policy`)
okunur; set **yayınlanmadan** hiçbir create/update geçmez (**400**).

**B seçilirse tek kalemde değişecekler:** ① §4.4 static class'lar **kaldırılır**, MOD-0048 reader bağımlılığı
eklenir · ② §7 **Dependencies**: MOD-0048 `ileride tüketir` → **hard prerequisite** · ③ §12 V-J08 / V-S08
gerekçesi "in-domain set" → "yayınlanmış MOD-0048 set" · ④ §16 **AC-VOCAB-1** metni ve §17.2/küme 21 testi
MOD-0048 stub'ı ile yazılır · ⑤ **F-RD** follow-up **blocker**'a döner ve smoke öncesi 5 set **yayınlanmalıdır** ·
⑥ contract `vocabularies` bloğu set kimliklerini (`setKey` + `versionId`) **de** yayınlar.

**Öneri gerekçesi (karar sizin):** MOD-0164-FU02 bu ailede MOD-0048'e bağlanan tek modüldü ve sonuç **vokabüler
çelişkisi** oldu (shipped legal-basis/source değerleri boundary ile çakıştı, iki set bugün hâlâ publish edilemiyor).
FU02/FU03/FU04 in-domain yürüdü ve hiçbir çelişki üretmedi. **`review`/`approved` her iki seçenekte de bugün yalnız
metadata**dır; gerçek approval MOD-0023'e **en sonda** bağlanır (F-WF).

### 4.5 Mongo / persistence kararı — **TEK collection** (S2)

| Collection | Index | Not |
|---|---|---|
| `content_engagement_journeys` | `(TenantId, JourneyCode, JourneyVersion)` unique, **partial**: archived hariç | Partial filter'da **`$ne` YASAK** → `Filter.Type(...)` / `$lt` deseni (Platform 5057 crash-loop dersi) |
| `content_engagement_journeys` | `(TenantId, SubjectId, JourneyStatus)` | Liste yolu |
| `content_engagement_journeys` | `(TenantId, Stages.RecommendedKnowledgePathId)` *(multikey, opsiyonel)* | "Bu path hangi journey'lerde kullanılıyor?" okuması; **tek** `DateTimeOffset` bile içermez |
| ~~`content_engagement_journey_stages`~~ | — | **YOK** (S2: ikinci collection yaratılmaz) |
| **Yasak** | `(EffectiveFrom, EffectiveTo)` bileşik index / iki-`DateTimeOffset` sort | Parallel-array 500 tuzağı → gerekirse **in-memory sort** |

**Yazma modeli:** aşama ekleme/güncelleme/arşivleme **tek doküman** yazımıdır (journey root'u `EntityBase.Version`
kontrolüyle replace edilir). Bu nedenle **çok-doküman transaction / `SupportsTransactionsAsync` guard'ı /
compensation GEREKMEZ** (dev standalone Mongo riski yapısal olarak yok) ve **cross-collection cascade GEREKMEZ**.

⚠️ **Dizi-içi unique index YOK:** Mongo bir dizi içindeki `StageOrder`/`StageCode` tekilliğini index ile zorlayamaz
→ **tek savunma hattı handler + validator'dır** (§12); §17.2/küme 9 bu yüzden **zorunlu testtir**.

`RegisterClassMaps`'e **`ContentEngagementJourney` ile birlikte embedded tipler de**
(`ContentEngagementJourneyStage`, `ContentEngagementJourneyBranchCondition`) kaydedilmelidir; aksi hâlde gömülü
`Guid` alanları (`StageId`, `RecommendedKnowledgePathId`, `FallbackStageId`, `TargetStageId`) **binary** yazılır ve
filtreler **sessizce boş döner** (MOD-0151 FU05 / `AccountTerritoryAssignment` dersi).

---

## 5. Repo Scope

```text
# --- backend (TEK aggregate, TEK repository) ---
services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/ContentEngagementJourney.cs                        (yeni; embedded stage + branch condition + vokabüler + reason-code static class'ları)
services/Diten.CrmService/src/Diten.CrmService.Domain/Repositories/IContentEngagementJourneyRepository.cs         (yeni — TEK repository)
services/Diten.CrmService/src/Diten.CrmService.Application/Features/Knowledge/ContentEngagementJourney/**         (yeni — §10)
services/Diten.CrmService/src/Diten.CrmService.Application/Features/Knowledge/ContentEngagementJourney/Contract/  (yeni ContentEngagementJourneyContract)
services/Diten.CrmService/src/Diten.CrmService.Persistence/Repositories/ContentEngagementJourneyRepository.cs     (yeni — TEK dosya)
services/Diten.CrmService/src/Diten.CrmService.Persistence/DependencyInjection.cs                                 (RegisterClassMaps: journey + 2 embedded tip; index; DI — additive)
services/Diten.CrmService/src/Diten.CrmService.Api/Controllers/CRM/ContentEngagementJourneysController.cs         (yeni — journey + gömülü aşama alt-route'ları)
services/Diten.CrmService/src/Diten.CrmService.Api/Controllers/CRM/ContentEngagementJourneyContractController.cs  (yeni)
services/Diten.CrmService/src/Diten.CrmService.Api/Models/CRM/ContentEngagementJourneyRequests.cs                 (yeni)
services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/Knowledge/ContentEngagementJourneyTests.cs           (yeni)
services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/Knowledge/ContentEngagementJourneyStageRulesTests.cs (yeni — embedded aşama + path bağı kuralları)

# --- frontend: TEK proxy controller + viewmodel ---
frontend/Diten.Web/Controllers/CRM/ContentEngagementJourneysController.cs                                         (yeni, proxy-only)
frontend/Diten.Web/Models/CRM/ContentEngagementJourneyViewModels.cs                                               (yeni; journey VM + gömülü stage VM + branch VM)

# --- frontend: Views/CRM/ContentEngagementJourneys/ — DEV-0001 Compact kanonik 9 dosya (§11.2) ---
frontend/Diten.Web/Views/CRM/ContentEngagementJourneys/Index.cshtml                                               (Layout="_LayoutTenantShell" AÇIKÇA)
frontend/Diten.Web/Views/CRM/ContentEngagementJourneys/Create.cshtml                                              (Compact-özel)
frontend/Diten.Web/Views/CRM/ContentEngagementJourneys/Edit.cshtml                                                (Compact-özel)
frontend/Diten.Web/Views/CRM/ContentEngagementJourneys/Details.cshtml                                             (Compact-özel; salt-okunur aşama listesi + çözülmüş path + branch koşulları)
frontend/Diten.Web/Views/CRM/ContentEngagementJourneys/_Form.cshtml                                               (Compact-özel; journey formu + GÖMÜLÜ aşama alt-editörü + aşama-içi branch repeater)
frontend/Diten.Web/Views/CRM/ContentEngagementJourneys/_Filter.cshtml
frontend/Diten.Web/Views/CRM/ContentEngagementJourneys/_DataTable.cshtml                                          (data-dt-standard="v2" + skeleton; TEK DataTable = journey listesi)
frontend/Diten.Web/Views/CRM/ContentEngagementJourneys/_IndexL10n.cshtml
frontend/Diten.Web/Views/CRM/ContentEngagementJourneys/ContentEngagementJourneysIndex.cs                          (marker class)

# --- frontend: JS + RESX + nav ---
frontend/Diten.Web/wwwroot/assets/js/CRM/ContentEngagementJourneys/{index.js, index.l10n.js, form.js}             (yeni; form.js aşama repeater + branch repeater + path seçici)
frontend/Diten.Web/Resources/Views/CRM/ContentEngagementJourneys/ContentEngagementJourneysIndex.{ar,en,es,fr,ru,tr,zh}.resx  (7 dil)
frontend/Diten.Web/Resources/SharedResource.{ar,en,es,fr,ru,tr,zh}.resx                                           (ContentEngagementJourneysMenu anahtarı ×7)
frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml                                                         (TEK <li>, dar istisna — §6)

# --- doğrulama ---
scripts/smoke-mod0162-fu05-content-engagement-journey-authenticated.ps1                                           (yeni; FU04 script'i şablon)
docs/audits/mod-0162-fu05-content-engagement-journey-runtime-ui-*.md                                              (evidence)
```

> **S2 ile repo scope'a HİÇ girmeyenler:** `IContentEngagementJourneyStageRepository` ·
> `ContentEngagementJourneyStagesController` (backend **ve** frontend) · `Views/CRM/…Stages/**` ·
> ikinci JS/RESX seti · `content_engagement_journey_stages` collection'ı ve index'leri.

---

## 6. Protected Paths

`.antigravity/**` · `gateway/Diten.ApiGateway/**/ocelot.json` (**değişmez** — wildcard yeterli, §15) ·
`services/Diten.MdmService/**` · `services/Diten.Platform/**` · `services/Diten.AuthService/**` ·
`services/Diten.HcmService/**` · `services/Diten.EnterpriseStrategyService/**` ·
`services/Diten.DevEnablementService/**` (Golden Reference — okunur, değiştirilmez) ·
**FU04 yüzeyi**: `Features/Knowledge/Path/**` (`IKnowledgePathReader.cs` imzası dâhil),
`Domain/Entities/KnowledgePath.cs`, `Api/Controllers/CRM/KnowledgePath*.cs`, `Views/CRM/KnowledgePaths/**`,
`wwwroot/assets/js/CRM/KnowledgePaths/**` ·
**FU02 yüzeyi**: `Features/Knowledge/Content/**`, `Features/Knowledge/{Subject,Topic,AudienceProfile}/**`,
`Domain/Entities/KnowledgeContent.cs`, `Views/CRM/Knowledge/**` ·
**FU03 yüzeyi**: `Features/Knowledge/Concept/**`, `Domain/Entities/Concept*.cs`,
`Domain/Entities/KnowledgeContentConceptLink.cs`, `Views/CRM/KnowledgeConcepts/**` ·
MOD-0165 Campaign runtime · MOD-0164 Consent/Preference · MOD-0155 · MOD-0166 · MOD-0309 ·
RBAC seed / role template / permission catalog (`crm.knowledge.content-engagement-journey.*` **kataloğa yazılmaz**) ·
MOD-0048 publish · Mongo hand-edit · `execution/registries/**` (yalnız closeout'ta, kullanıcı onayıyla) ·
`execution/portfolio/**` · **FU01 / FU01A / FU01B / FU01C pack dosyaları** (okunur, değiştirilmez) ·
`frontend/Diten.Web/Views/Shared/_Layout.cshtml` (FROZEN) · `frontend/Diten.Web/Controllers/Archive/**` +
`frontend/Diten.Web/Views/Archive/**` (FROZEN).

**Kasıtlı dokunulan tek istisna (protected DEĞİL — dar kapsam):**
`frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml` — CRM Admin → Knowledge nav'ına **tek `<li>`**
(*Content Engagement Journeys* → `/CRM/ContentEngagementJourneys`, permission-guard'lı) eklenir; mevcut *Knowledge*
(satır ~403), *Knowledge Concepts* (~417) ve *Knowledge Paths* (~431) `<li>`'leri, `active` yol mantığı ve oturum
davranışı **değişmez**.

---

## 7. Dependencies

Frontmatter listesinin açıklamalı karşılığı.

| Bağımlılık | Yön | Sözleşme / etki |
|---|---|---|
| **MOD-0162-FU01B** (approved boundary) | implement eder | §3–§13 sözleşmesi BURADA runtime'a döner; §16'nın **tek-aggregate önerisi** S2 ile birebir uygulanır; F1 kanonik adı **tüm literal'lerde**; kalan sapmalar §2.1'de gerekçeli |
| **MOD-0162-FU02** (SHIPPED) | **hard prerequisite** | `SubjectId` **zorunlu**; `Topic`/`AudienceProfile` opsiyonel referans. FU02 sözleşmesi **kırılmaz** (§2.3); içerik bu FU'da **doğrudan referanslanmaz** |
| **MOD-0162-FU04** (SHIPPED / DONE) | **hard prerequisite + read-only tüketir** | Aşama → **published+effective** `KnowledgePath`; path seçici FU04 endpoint'ini proxy üzerinden okur; `IKnowledgePathReader` imzası **genişletilmez** (§2.2) |
| **MOD-0162-FU03** (DONE) | **dokunulmaz** | Kavram bağı **adım** seviyesindedir (FU04); aşama seviyesinde concept referansı **yoktur** |
| **MOD-0166** (Journeys & Automation) | **boundary — ad/sahiplik** | Trigger/suppression/kanal/run-log **orada**; bu FU otomasyon **yürütmez**. MOD-0166 ileride bir aşamayı **referans alabilir** (§2.4) |
| **MOD-0155** (Field Sales / Visit) | **consumer** | `IContentEngagementJourneyReader` + `?status=published&effectiveAt=` seam'i; **current stage progress + stage advancement MOD-0155/F-DETAIL tarafındadır**. MOD-0155 kodu bu FU'da **değişmez** |
| **MOD-0309** (Learning / Training Records) | **consumer** | `AdvancementRule` iki tarafın sözleşme alanı: bu FU **beyan eder**, MOD-0309 **ölçer ve kaydeder** (FU01B §12) |
| **MOD-0165 / MOD-0167** | boundary | "Ne sıklıkla" ve "kime" oradadır; **journey target assignment bu FU'da YOK** (FU01B §8; F-TARGET) |
| **MOD-0048** (reference data) | **D-VOCAB'a bağlı** | A: ileride tüketir (F-RD, blocker değil) · B: **hard prerequisite** (§4.4) |
| **MOD-0028 / MOD-0029** | boundary | Aşama dosya taşımaz; `FileRef` yalnız FU02 içeriğinin alanıdır |
| **MOD-0018** (RBAC) | yalnız tüketim | seed/grant **YOK**; dev fallback §14; F-RBAC en sonda |
| **DEV-0001** (Golden Reference Compact) | golden reference | **Tek** yüzey, **tek** klasör (§11); Slim dosya seti **kullanılmaz** |

---

## 8. Runtime Constraints

- **Servis:** `Diten.CrmService` (port **5061**), **yeni servis yaratılmaz**.
- **Gateway:** tüm çağrılar `:5000` üzerinden; browser JS **servis portuna gitmez** (same-origin MVC proxy).
- **Soft delete:** `DELETE` ve `PATCH` **yoktur** — kaldırma = archive (journey **ve** gömülü aşama); archived kayıt
  update kabul etmez (**409**). Archived aşama **diziden silinmez**.
- **Tenant:** `EntityBase` tenant-owned; `TenantId` **server-side** claim'den, DTO/payload'da yer almaz;
  cross-tenant erişim **404 / boş liste**.
- **Concurrency:** **tek** `EntityBase.Version` (journey root); **aşama düzenlemeleri de** bu token'a tabidir →
  uyuşmazlık **409**, sessiz overwrite yasak.
- **Atomiklik (S2):** her yazma **tek doküman** yazımıdır → çok-doküman transaction, `SupportsTransactionsAsync`
  guard'ı ve compensation **gerekmez**; `new-version` bile *(oku → klonla → tek insert)* iki bağımsız yazımdır ve
  **yarım journey** üretmez.
- **Runtime state YOK:** `CurrentStageId` / `JourneyProgress` / `AssignedContactId` gibi hiçbir alan **ne journey'de,
  ne aşamada, ne Contact'ta, ne Account'ta** tutulmaz (FU01B §6).
- **Doküman büyümesi:** journey başına max **100** aşama, aşama başına max **20** branch condition (§4.2, V-S18).

### 8.1 API Contract

Tüm route'lar mevcut Gateway wildcard'ı `/api/crm/knowledge/{everything}` altındadır → **`ocelot.json` DEĞİŞMEZ**
(§15). **Aşama route'ları journey'in ALT KAYNAĞIDIR** (S2); düz `/…-stages` ailesi **yoktur**.

```text
GET    /api/crm/knowledge/content-engagement-journey/contract

GET    /api/crm/knowledge/content-engagement-journeys           ?subjectId&topicId&audienceProfileId&language&status
                                                                &effectiveAt&journeyCode&knowledgePathId&search&includeArchived
POST   /api/crm/knowledge/content-engagement-journeys
GET    /api/crm/knowledge/content-engagement-journeys/{journeyId}            → journey + gömülü aşamalar (çözülmüş path'le)
PUT    /api/crm/knowledge/content-engagement-journeys/{journeyId}            → journey ALANLARI; `stages` dizisi KABUL EDİLMEZ
POST   /api/crm/knowledge/content-engagement-journeys/{journeyId}/publish    (SoD — ayrı izin)
POST   /api/crm/knowledge/content-engagement-journeys/{journeyId}/new-version
POST   /api/crm/knowledge/content-engagement-journeys/{journeyId}/archive

GET    /api/crm/knowledge/content-engagement-journeys/{journeyId}/stages     ?includeArchived — StageOrder sıralı + çözülmüş path
POST   /api/crm/knowledge/content-engagement-journeys/{journeyId}/stages     → gömülü diziye aşama ekler (tek doküman yazımı)
PUT    /api/crm/knowledge/content-engagement-journeys/{journeyId}/stages/{stageId}
POST   /api/crm/knowledge/content-engagement-journeys/{journeyId}/stages/{stageId}/archive
```

**Yasaklar:** `DELETE` yok · `PATCH` yok · **düz `/api/crm/knowledge/content-engagement-journey-stages` ailesi yok**
(S2) · `PUT /{journeyId}` içinde `stages` dizisi ile toplu aşama yazımı **yok** (aynı veri için iki yazma yolu
olmaz) · payload'da `TenantId` yok (gönderilirse **sessizce yok sayılır**, claim kazanır) · service-to-service
doğrudan iş çağrısı yok (yalnız Gateway) · **hiçbir endpoint "en uygun journey"i, "sonraki aşama"yı veya bir
ilerleme durumunu döndürmez** — `recommend` / `nextStage` / `currentStage` / `advance` / `score` gibi bir
route/parametre **yoktur**.

### 8.2 Contract flags

```json
{ "supportsContentEngagementJourney": true, "supportsContentEngagementJourneyStage": true,
  "supportsMultiVisitContentProgression": true, "supportsJourneyVersioning": true,
  "supportsPublishedStageSetFreeze": true, "supportsRequiredOptionalStages": true,
  "supportsRepeatableStages": true, "supportsStageKnowledgePathBinding": true,
  "supportsPathVersionPinPolicy": true, "supportsFutureStageAdvancementMetadata": true,
  "supportsFutureBranchingMetadata": true, "supportsArchiveLifecycle": true,
  "supportsEffectiveDating": true, "supportsContractDrivenUi": true }
```

**ASLA eklenmez (`false` olarak bile) — 15 yasak flag:** `supportsStageAdvancementEngine` ·
`supportsBranchEvaluator` · `supportsRecommendationEngine` · `supportsBestNextStage` ·
`supportsJourneyRuntimeProgress` · `supportsCurrentStageState` · `supportsJourneyTargetAssignment` ·
`supportsCompletionTracking` · `supportsDigitalDetailing` · `supportsVisitPlanning` · `supportsRoutePlanning` ·
`supportsCampaignEngine` · `supportsFrequencyEngine` · `supportsWorkflowApproval` · `supportsHardDelete`.
Gerekçe (FU03/FU04 emsali): bir yeteneği `false` ile bile ilan etmek boundary'yi **yanlış temsil eder**.

**`limitations` listesinde yayınlanır:** `maxStagesPerJourney: 100` · `maxBranchConditionsPerStage: 20` ·
`stagesAreEmbeddedInJourneyDocument: true` · `noStageAdvancementEvaluation` · `noBranchEvaluation` ·
`noJourneyProgressState` · `noJourneyTargetAssignment` · `noRecommendation`.

**FU02 / FU03 / FU04 contract'ları değişmez** — kendi flag setleri ve endpoint'leri olduğu gibi kalır.

### 8.3 `PathVersionPinPolicy` çözümleme semantiği (sessiz sürüm kayması YASAK — FU01B §10)

| Politika | Davranış | Response |
|---|---|---|
| `pinned` *(varsayılan)* | Aşama yazımdaki `RecommendedKnowledgePathId`'ye **sabitlenir**; path yeni sürüm yayınlasa bile journey **değişmez** | `PathResolutionStatus = pinned` |
| `latest-published` | Okuma anında `PathCode` üzerinden **published + effective** sürüm çözülür (FU04 `IsPublished()` + `IsEffectiveAt(effectiveAt)`) | `PathResolutionStatus = resolved-latest` |
| her ikisi — çözülemezse | Aşama **düşmez, gizlenmez, uydurulmaz** | `PathResolutionStatus = unresolved` + `ResolvedKnowledgePathId = null` (**görünür**, fail-closed) |

Her durumda çözülen `ResolvedKnowledgePathId` + `ResolvedPathVersion` **cevapta yer alır** (FU01B §10). Çözümleme
FU04 `IKnowledgePathReader` imzasını **genişletmez** (§2.2/AC-FU04-2); FU05'in kendi read-only resolver'ı
`IKnowledgePathRepository`'yi okur. **Path'in adımları hiçbir koşulda journey dokümanına kopyalanmaz** —
tüketici adımları FU04'ten okur.

**Determinizm (FU01B §5.1):** published journey sürümünde stage→path eşlemesi **deterministiktir**; `pinned`
aşamalar sürüm kaymasına **kapalıdır**, `latest-published` aşamalar ise politikayı **açıkça beyan ettiği için**
kayma **sessiz değildir** (UI'da rozetle görünür).

### 8.4 Tüketim seam'i — `IContentEngagementJourneyReader` (motor DEĞİL)

```csharp
Task<IReadOnlyList<ContentEngagementJourneyDto>> ResolvePublishedJourneysAsync(
    ContentEngagementJourneyCriteria criteria, CancellationToken ct);   // subject/topic/audience/language/effectiveAt
Task<IReadOnlyList<ContentEngagementJourneyStageDto>> GetOrderedStagesAsync(
    Guid journeyId, DateTimeOffset effectiveAt, CancellationToken ct);
```

**Döndürür:** yalnız `published` + effective journey'ler ve `StageOrder` → `StageCode` ile **deterministik sıralı**,
`StageStatus = active`, path'i çözülmüş gömülü aşamalar. **Yapmaz:** skorlama · "en uygun journey" seçimi · öneri ·
**current stage hesaplama** · **aşama ilerletme** · dal değerlendirmesi · completion okuma/yazma · target
assignment. Veri yoksa **boş döner** — varsayılan uydurulmaz (MOD-0151 R11 ruhu). `draft` / `review` / `approved` /
`inactive` / `archived` journey **tüketiciye asla gitmez** (FU01B §3).

---

## 9. Layout & Shell Contract

- `shell: tenant` → **tüm** `.cshtml` dosyalarında Razor bloğunda **AÇIKÇA**:

```cshtml
@{
    ViewData["Title"] = Localizer["PageTitle"];
    Layout = "_LayoutTenantShell";   // shell: tenant
}
```

- View klasörü: **`Views/CRM/ContentEngagementJourneys/`** (tek klasör)
- Frontend route'u: **`/CRM/ContentEngagementJourneys`** (tek sayfa; aşama yönetimi bu sayfanın **içindedir**)
- `_ViewStart.cshtml` varsayılanı **değiştirilmez**; `_Layout.cshtml` FROZEN.
- Partial çağrıları **absolute path** ile: `~/Views/CRM/ContentEngagementJourneys/_Filter.cshtml`
- Index bölüm sırası (Golden Compact): ① Filter → ② BulkActionBar → ③ DataTable; **offcanvas panel YOK**
  (Compact yasağı, §11.2).
- Nav: `_LayoutTenantShell.cshtml` içinde CRM Admin → Knowledge grubunda mevcut *Knowledge* (~403),
  *Knowledge Concepts* (~417) ve *Knowledge Paths* (~431) `<li>`'lerinden sonra **dördüncü ve tek yeni `<li>`**
  (*Content Engagement Journeys*), `@if (Perms.Has(...))` guard'lı, `SharedResource` anahtarı
  `ContentEngagementJourneysMenu` (7 dil). Yeni yol **`/CRM/ContentEngagementJourneys`** olduğu için mevcut
  *Knowledge* `<li>`'sinin `/CRM/Knowledge` prefix mantığı ve negatif prefix listesi **değişmez**.

---

## 10. Backend File Convention

**Naming Golden Reference ile birebir** (`module-pack-standard.md` §4): Command/Query **record**,
Handler/Validator **class** ve isimlerinde **`Command` / `Query` / `Request` suffix YOK**.
**Tek aggregate → tek feature klasörü; aşama komutları da journey root'unu mutasyona uğratır ve tek repository'yi
kullanır.**

```text
services/Diten.CrmService/src/Diten.CrmService.Application/Features/Knowledge/ContentEngagementJourney/
├── Commands/
│   └── ContentEngagementJourneyCommands.cs   → CreateContentEngagementJourneyCommand · UpdateContentEngagementJourneyCommand ·
│                                                PublishContentEngagementJourneyCommand · CreateContentEngagementJourneyVersionCommand ·
│                                                ArchiveContentEngagementJourneyCommand ·
│                                                AddContentEngagementJourneyStageCommand · UpdateContentEngagementJourneyStageCommand ·
│                                                ArchiveContentEngagementJourneyStageCommand
│                                                (hepsi sealed record; aşama komutları JourneyId taşır ve journey root'unu yazar)
├── Queries/
│   └── ContentEngagementJourneyQueries.cs    → ListContentEngagementJourneysQuery · GetContentEngagementJourneyQuery ·
│                                                GetContentEngagementJourneyStagesQuery (sealed record)
├── Handlers/
│   ├── ContentEngagementJourneyCommandHandlers.cs → CreateContentEngagementJourneyHandler · UpdateContentEngagementJourneyHandler ·
│   │                                                PublishContentEngagementJourneyHandler · CreateContentEngagementJourneyVersionHandler ·
│   │                                                ArchiveContentEngagementJourneyHandler · AddContentEngagementJourneyStageHandler ·
│   │                                                UpdateContentEngagementJourneyStageHandler ·
│   │                                                ArchiveContentEngagementJourneyStageHandler   (sealed class, suffix YOK)
│   └── ContentEngagementJourneyQueryHandlers.cs  → ListContentEngagementJourneysHandler · GetContentEngagementJourneyHandler ·
│                                                    GetContentEngagementJourneyStagesHandler
├── Validators/
│   ├── CreateContentEngagementJourneyValidator.cs · UpdateContentEngagementJourneyValidator.cs   (suffix YOK)
│   └── AddContentEngagementJourneyStageValidator.cs · UpdateContentEngagementJourneyStageValidator.cs
├── Contract/
│   └── ContentEngagementJourneyContract.cs   → GetContentEngagementJourneyContractQuery + DTO + flags + vokabüler + limitler
├── IContentEngagementJourneyReader.cs        → §8.4 read-only seam + default implementation
├── ContentEngagementJourneyDtos.cs           → TEK dosyada tüm DTO / ViewModel (journey + gömülü stage + branch)
├── ContentEngagementJourneyMapper.cs
├── ContentEngagementJourneyPathResolver.cs   → §8.3 read-only path çözümleyici (FU04 repository'sini OKUR, seam'i genişletmez)
├── ContentEngagementJourneyValidation.cs     → §12 kurallarının ortak yardımcıları (sıra, fallback, freeze, limitler)
└── ContentEngagementJourneyPermissions.cs    → §14 (TANIM ONLY)
```

> **⚠️ Dosya gruplama — açık sapma beyanı.** Golden Reference `Create{Module}Command.cs` gibi **komut başına tek
> dosya** ister. FU02/FU03/FU04, `Diten.CrmService/.../Features/Knowledge/**` altında **aggregate başına
> gruplanmış** dosya kullanır (kanıt: `Features/Knowledge/Path/Commands/`, `Concept/Node/ConceptNodeHandlers.cs`)
> ve bu artık in-domain yerleşik konvansiyondur. Bu pack **yerleşik Knowledge konvansiyonunu sürdürür**;
> **sınıf/record isimleri Golden Reference ile birebirdir**. Sapma yalnız **dosya gruplamasındadır** ve
> bilinçlidir → **F-FILE** (tüm Knowledge ailesi için tek seferde yapılmalı).

`BulkDelete{Module}Command` **YOK** — bu modülde hard delete ve bulk delete yoktur (archive-only; FU02/FU03/FU04 emsali).

---

## 11. Frontend File Contract

### 11.1 Golden reference kararı — kullanıcı-form alanı türetmesi (GÖSTERİLİR)

Sayım kuralı (`module-pack-standard.md` §3): yalnız kullanıcının create/edit formunda **doldurduğu** modül alanları
sayılır. `Id`, `TenantId`, audit alanları, türetilmiş alanlar, DataTable checkbox/action kolonları **sayılmaz**.
Effective window (`EffectiveFrom` + `EffectiveTo`) tek kontrol olarak render edilir → **1** sayılır (FU03/FU04 yöntemi).

**Golden-reference yüzeyi (TEK) — `ContentEngagementJourney`:** §4.1'de 23 satır; form-dışı olanlar düşüldükten
sonra kalan **12**:

| # | Kullanıcı-form alanı | # | Kullanıcı-form alanı |
|---|---|---|---|
| 1 | `JourneyCode` | 7 | `Objective` |
| 2 | `JourneyName` | 8 | `LanguageCode` |
| 3 | `Description` | 9 | `JourneyVersion` |
| 4 | `SubjectId` | 10 | `JourneyStatus` |
| 5 | `TopicId` | 11 | `EffectiveFrom` (+`EffectiveTo` **aynı kontrol**) |
| 6 | `AudienceProfileId` | 12 | `Source` |

*Form-dışı (11):* `Id` · `TenantId` · `EffectiveTo` (aynı kontrol) · `Stages` (alt-editör, alan değil) ·
`StageSetFrozenAt` · `PublishedAt` · `PublishedBy` · `SupersedesJourneyId` · `ArchivedAt/By` · `CreatedAt/By` ·
`UpdatedAt/By` · `EntityBase.Version` (+ tüm türetilmişler).

→ **12 > 8 ⇒ `golden_reference: compact`** (frontmatter `form_field_count: 12`).

**Gömülü aşama alt-editörü — ayrı golden-reference yüzeyi DEĞİLDİR (S2).** Aşama, kendi sayfası/DataTable'ı olan
bağımsız bir modül değil, journey Compact formunun **içindeki bir repeater**dır; bu yüzden **ikinci bir
Slim/Compact kararı doğurmaz** ve verifier için **ikinci bir referans koşusu gerektirmez**. Tamlık için alan
sayımı (15 — `JourneyId` **yok**, bağlam gömülü):

| # | Alt-editör alanı | # | Alt-editör alanı |
|---|---|---|---|
| 1 | `StageOrder` | 9 | `Repeatable` |
| 2 | `StageCode` | 10 | `MinVisitNumber` |
| 3 | `StageName` | 11 | `MaxVisitNumber` |
| 4 | `StageObjective` | 12 | `AdvancementRule` (beyan — değerlendirilmez) |
| 5 | `StageType` (opsiyonel) | 13 | `FallbackStageId` (aynı journey'in aşamaları) |
| 6 | `RecommendedKnowledgePathId` (Subject→Path zincirli) | 14 | `Notes` |
| 7 | `PathVersionPinPolicy` | 15 | `BranchConditions` (aşama-içi repeater, S5 — 1 grup) |
| 8 | `IsRequired` | | |

*Alt-editör dışı:* `StageId` (üretilir) · `PathCode` (türetilir) · `StageStatus` (archive aksiyonu) ·
`ArchivedAt/By` · aşama audit alanları · tüm `Resolved*` / `IsCross*` / `PathUsageCountInJourney` türetilmişleri.

**Sonuç:** modülde **tek** golden-reference yüzeyi, **tek** klasör, **tek** verifier koşusu vardır ve **hiç Slim
dosyası yoktur** (`_CreateEditOffcanvas.cshtml` / `_DetailsQuickView.cshtml` **YASAK**). FU03'ün hibrit
konsolundaki 2 yapısal verifier FAIL'i burada **yapısal olarak oluşmaz** (FU04 emsali).

### 11.2 Dosya seti — TEK klasör, kanonik Compact 9 dosya (TEK TEK enumerasyon)

**`Views/CRM/ContentEngagementJourneys/` (DEV-0001 Compact — tam ve tek set):**

| # | Dosya | Rol |
|---|---|---|
| 1 | `Index.cshtml` | Liste kabuğu; `Layout = "_LayoutTenantShell"` **açıkça**; Filter → BulkActionBar → DataTable sırası |
| 2 | `Create.cshtml` | **Compact-özel** sayfa kabuğu + `_Form` |
| 3 | `Edit.cshtml` | **Compact-özel** sayfa kabuğu + `_Form` |
| 4 | `Details.cshtml` | **Compact-özel** detay sayfası; **salt-okunur** aşama listesi (`StageOrder` sıralı, çözülmüş path + `AdvancementRule` + `FallbackStageId` + branch koşulları) + publish / new-version aksiyonları |
| 5 | `_Form.cshtml` | Create/Edit ortak formu: **journey'in 12 alanı** + **gömülü aşama alt-editörü (repeater)** + **aşama içinde BranchCondition repeater** (S5) — ayrı partial açılmaz, klasör kanonik 9 dosyada kalır |
| 6 | `_Filter.cshtml` | Inline collapsible filter (`subject`, `topic`, `audience`, `language`, `status`, `effectiveAt`, `knowledgePathId`, `includeArchived`) |
| 7 | `_DataTable.cshtml` | `data-dt-standard="v2"` + skeleton loader; **TEK DataTable** = journey listesi (aşama kolonları: `ActiveStageCount` / `RequiredStageCount` / `HasUnresolvedStagePath` rozetleri) |
| 8 | `_IndexL10n.cshtml` | JSON payload bridge |
| 9 | `ContentEngagementJourneysIndex.cs` | Marker class (RESX kökü) |

**JS (Golden Compact seti — 3 dosya):**

```text
wwwroot/assets/js/CRM/ContentEngagementJourneys/index.js       → DataTable (DtDefaults + v2), filtre, archive
wwwroot/assets/js/CRM/ContentEngagementJourneys/index.l10n.js  → camelCase→PascalCase L10n köprüsü
wwwroot/assets/js/CRM/ContentEngagementJourneys/form.js        → path zincirli seçici + AŞAMA repeater + BRANCH repeater
```

`index.l10n.js` **camelCase→PascalCase** dönüşümünü atlamaz (aksi hâlde `window.L10n` anahtarları `undefined` döner
ve toast "(undefined: corrId)" olur). DataTable JS **HttpOnly cookie okumaz, Bearer token kurmaz**; API profili
**`proxy`** (same-origin `/CRM/ContentEngagementJourneys/api/...`). Sayfada **tek** DataTable vardır →
`updateVisualState` global selector çakışması **yapısal olarak yok**.

**RESX (tek klasör × 7 dil + shared):**

```text
Resources/Views/CRM/ContentEngagementJourneys/ContentEngagementJourneysIndex.{ar,en,es,fr,ru,tr,zh}.resx
Resources/SharedResource.{ar,en,es,fr,ru,tr,zh}.resx        → ContentEngagementJourneysMenu
```

**YASAK dosyalar:** `_CreateEditOffcanvas.cshtml` · `_DetailsQuickView.cshtml` (Compact yasağı) ·
`Views/CRM/ContentEngagementJourneyStages/**` (S2) · Index içinde create/edit offcanvas · hardcoded vokabüler
listesi (tüm dropdown'lar `content-engagement-journey/contract`'tan beslenir).

**Kullanılan mevcut yüzeyler (yeni dosya değil):** path seçici FU04'ün
`/api/crm/knowledge/paths?status=published&effectiveAt=…` endpoint'ini, subject/topic/audience seçicileri FU02'nin
`/api/crm/knowledge/subjects|topics|audience-profiles` endpoint'lerini **proxy üzerinden okur** — bu modüllerin
view/JS/controller dosyalarına **dokunulmaz** (§6).

---

## 12. Validation Rules

### 12.1 `ContentEngagementJourney` (aggregate root)

| # | Kural | Sonuç |
|---|---|---|
| V-J01 | `TenantId` payload'da → yok sayılır, claim kazanır | 2xx (sessiz ignore) |
| V-J02 | `JourneyCode` boş / max 100 aşımı; `JourneyName` boş / max 200; `Objective` boş / max 500 | **400** |
| V-J03 | Aynı `(TenantId, JourneyCode, JourneyVersion)` ikinci **non-archived** kayıt | **409** |
| V-J04 | `SubjectId` yok / archived / başka tenant | **400** |
| V-J05 | `TopicId` verildi ama `SubjectId`'ye ait değil | **400** |
| V-J06 | `AudienceProfileId` archived / başka tenant | **400** |
| V-J07 | `EffectiveTo < EffectiveFrom` | **400** |
| V-J08 | Bilinmeyen `JourneyStatus` / `Source` (§4.4 set dışı) | **400** (fail-closed) |
| V-J09 | Archived journey update (aşama yazımı dâhil) | **409** |
| V-J10 | Aynı `(JourneyCode, LanguageCode)` için **örtüşen** effective pencerede **ikinci `published`** sürüm | **409** (FU01B §5.1) |
| V-J11 | Publish denemesi, journey'de **hiç `active` + `IsRequired=true` aşama yokken** | **400** (FU01B §4) |
| V-J12 | `Update` ile `JourneyStatus = published` geçişi (publish **ayrı** endpoint — SoD) | **400** |
| V-J13 | `published` journey'de `EffectiveTo` ve `JourneyStatus` (`inactive`/`archived`) **dışında** herhangi bir alan değişimi | **409** (sürüm dondurulmuştur; değişiklik `new-version` ister) |
| V-J14 | `new-version` kaynak journey `published` değil | **400** (kopyalanacak dondurulmuş sürüm yok) |
| V-J15 | `EntityBase.Version` uyuşmazlığı — **journey VE aşama yazımlarının ortak token'ı** | **409** |
| V-J16 | `PUT /{journeyId}` gövdesinde `stages` dizisi gönderildi | **400** (aşamalar yalnız alt-route'lardan yönetilir; iki yazma yolu yok) |
| V-J17 | Payload'da `campaignId` / `brandId` / `productId` / `segmentId` (§2.1/S6 — alan yok) | **400** (bilinmeyen alan sessizce yutulmaz) |

### 12.2 Gömülü aşama (`ContentEngagementJourney.Stages[]`)

| # | Kural | Sonuç |
|---|---|---|
| V-S01 | `journeyId` yok / başka tenant | **404** · archived journey → **409** (V-J09) |
| V-S02 | Journey `published` (`StageSetFrozenAt` dolu) → aşama ekleme / güncelleme / arşivleme | **409** (aşama seti dondu — FU01B §5.1) |
| V-S03 | Aynı journey içinde duplicate `StageOrder` (**active** aşamalar) — **DB index'i YOK, handler tek savunma** (§4.5) | **409** |
| V-S04 | `StageCode` boş / journey içinde duplicate (**active**) — aynı şekilde handler doğrular | **409** |
| V-S05 | `RecommendedKnowledgePathId` yok / başka tenant / **published+effective değil** | **400** (FU01B §10) |
| V-S06 | `RecommendedKnowledgePathId` **archived** path'e işaret ediyor (yeni veya **değişen** değer) | **400** (FU01B §10: archived path yeni published journey stage'e bağlanamaz) |
| V-S07 | `RecommendedKnowledgePathId` PUT'ta **değişmedi** (aynı değer / payload'da yok) | path yeniden doğrulanmaz, **400 üretilmez** (FU03 V22 / FU04 V-S07 dirty-check emsali) |
| V-S08 | Bilinmeyen `PathVersionPinPolicy` / `StageType` / `AdvancementRule` / `StageStatus` (§4.4 set dışı) | **400** (fail-closed) |
| V-S09 | `StageName` / `StageObjective` boş veya max aşımı (200 / 500) | **400** |
| V-S10 | `FallbackStageId` **aynı journey'de değil** · **kendisi** | **400**; **geriye işaret etmek serbesttir** (FU01B §4) |
| V-S11 | `MinVisitNumber` veya `MaxVisitNumber` < 1 · `MaxVisitNumber < MinVisitNumber` | **400** (§2.1/S7) |
| V-S12 | `Repeatable` gönderilmedi | varsayılan **`false`** (FU01B §7) — 2xx |
| V-S13 | Aynı `RecommendedKnowledgePathId` birden fazla aşamada | **kabul edilir** (FU01B §7: tekrar yasak değil) + `PathUsageCountInJourney` > 1 **görünür** |
| V-S14 | Aşamanın path Subject'i journey Subject'inden farklı · path dili journey dilinden farklı | **kabul edilir**, `IsCrossSubjectStage` / `IsCrossLanguageStage = true` (**görünür**; sessiz karışım yasak — FU01B §10) |
| V-S15 | **(S5)** `BranchConditions[].TargetStageId` **aynı journey'de değil** · `ConditionCode` boş | **400** (**değerlendirme yok**, yalnız referansel akıl sağlığı) |
| V-S16 | Archived aşama update | **409** |
| V-S17 | Bir aşama, **active** bir aşamanın `FallbackStageId`'si veya `BranchConditions[].TargetStageId`'si iken archive ediliyor | **409** (dangling referans yasağı) |
| V-S18 | Journey'de **100**'den fazla aşama · aşamada **20**'den fazla branch condition | **400** (§4.2 doküman büyüme sınırı; contract'ta ilan edilir) |
| V-S19 | Herhangi bir aşama yazımı → **journey'in** `EntityBase.Version`'ı artar; eş zamanlı ikinci yazım | **409** (V-J15 ile aynı token) |
| V-S20 | Journey archive → gömülü aşamalar **archived kabul edilir**, **diziden silinmez** | okunur kalır (**aynı doküman** — ayrı cascade yazımı YOK) |
| V-S21 | Payload'da `currentStage` / `progress` / `assignedContactId` / `advance` benzeri runtime-state alanı | **400** (FU01B §6: runtime state bu FU'da **yok**) |

**Reason code'lar:** her yazma sonucu ve red, `ContentEngagementJourneyReasonCodes` kataloğundan bir kod taşır
(`content_engagement_journey_created` · `_updated` · `_published` · `_archived` · `_version_created` ·
`_duplicate_code` · `_overlapping_published_version` · `_stage_added` · `_stage_updated` · `_stage_archived` ·
`_stage_order_conflict` · `_stage_set_frozen` · `_no_required_stage` · `_fallback_invalid` ·
`_branch_target_invalid` · `_path_not_consumable` · `_path_unresolved` · `_visit_range_invalid` ·
`_stage_limit_exceeded` · `_archived_no_mutation` · `_reference_archived` · `_runtime_state_not_supported`).
**Hiçbir şey sessiz değildir.**

---

## 13. Failure Path to Verify

- **Duplicate — aynı `JourneyCode` + `JourneyVersion`**
  - Expected: **409** + UI field-level hata + kayıt **oluşmaz** + reload sonrası temiz state.
- **Duplicate — aynı journey içinde aynı `StageOrder`**
  - Expected: **409** + alt-editör satırında hata + doküman **yazılmaz**. ⚠️ DB unique index **yok** (dizi-içi
    tekillik index'lenemez) → handler doğrulaması **tek savunma hattı**, test zorunlu.
- **Missing — `Objective` / `StageObjective` / `RecommendedKnowledgePathId` boş**
  - Expected: **400** + validator mesajı + save engellenir; sunucu tarafında da reddedilir (client-only doğrulama yok).
- **Concurrency — iki kullanıcı aynı journey'i (veya aşamalarını) düzenliyor**
  - Expected: **409** + UI "veri değişti, yeniden yükleyin" + **sessiz overwrite YOK**. Aşama düzenlemesi de
    **journey'in** `EntityBase.Version`'ını kullanır (S2).
- **Frozen — published journey'in aşamasını düzenleme denemesi**
  - Expected: **409** + UI'da aşama alt-editörü **disabled** + "bu sürüm yayınlandı, yeni sürüm oluşturun" gerekçesi
    + `new-version` aksiyonu görünür.
- **Unauthorized — permission'ı olmayan aktör**
  - Expected: **403** + UI aksiyon **disabled** / permission-denied state; liste boş listeyle **maskelenmez**.
    Publish yetkisi olmayan (`manage` var, `publish` yok) aktör: publish **403**, düzenleme **200** (SoD).
- **Cross-tenant — başka tenant'ın `journeyId`'si ile GET/PUT**
  - Expected: **404** (varlık sızdırılmaz), yazma **gerçekleşmez**.
- **Path uygun değil — `draft` veya archived path ile aşama oluşturma**
  - Expected: **400** + `content_engagement_journey_path_not_consumable`; FU04 kaydı **değişmez**.
- **Unresolved path — `latest-published` aşamanın path'i yayından kalktı**
  - Expected: **200** + `PathResolutionStatus = unresolved` + UI'da uyarı rozeti; aşama **gizlenmez**,
    **başka path ile doldurulmaz**.
- **Dangling fallback — hedef aşama archive ediliyor**
  - Expected: **409** + hangi aşamanın bağımlı olduğu mesajda; archive **gerçekleşmez**.
- **Boş journey publish — `IsRequired=true` active aşama yok**
  - Expected: **400** + gerekçeli hata; journey `draft` kalır.
- **Örtüşen ikinci published sürüm**
  - Expected: **409**; ilk published sürüm **değişmez**.
- **Limit aşımı — 101. aşama eklenmesi**
  - Expected: **400** + limitin contract'tan okunabildiği gerekçe mesajı.
- **Runtime state sızması — payload'da `currentStage`**
  - Expected: **400** (V-S21); boundary'nin "state yok" kuralı **sessizce** delinmez.

---

## 14. Authorization Convention

```text
Policy:     [Authorize]                                                       // shell: tenant
Permission: [HasPermission("crm.knowledge.content-engagement-journey.{action}")]  // PKS-001: lowercase-dotted,
                                                                              // hyphen-in-segment, depth >= 3
Actor type: tenant_user
```

| Anahtar | Kapsam |
|---|---|
| `crm.knowledge.content-engagement-journey.read` | Journey + gömülü aşama listeleme/detay + contract |
| `crm.knowledge.content-engagement-journey.manage` | Journey create/update/archive + **gömülü aşama** ekleme/güncelleme/arşivleme + `new-version` |
| `crm.knowledge.content-engagement-journey.publish` | **Yalnız** `POST /{journeyId}/publish` — SoD: yazan ≠ yayınlayan (FU01B §11) |

> **S2 sonucu:** ayrı `…-journey-stage.manage` anahtarı **tanımlanmaz**. Aşama, journey aggregate'inin
> **içindedir**; aynı dokümanın parçası için ikinci bir yetki sınırı tanımlamak yanıltıcı olurdu. Anahtar seti
> FU01B §11'in kanonik önerisiyle (**read / manage / publish**) birebir örtüşür; yalnız kaynak adı F1 kanonik
> formuna (`content-engagement-journey`) çevrilmiştir.

**TANIM ONLY — seed/grant YOK** (FU01B §11; RBAC en sona bırakıldı). Katalogda `crm.knowledge.*` henüz yok →
FU02/FU03/FU04'ün **belgelenmiş fallback'i** kullanılır: `crm.territory.read` (read) /
`crm.territory.model.manage` (manage + publish) — kod teyidi:
`Features/Knowledge/Path/KnowledgePathPermissions.cs`. Fallback **hiçbir guard'ı gevşetmez** — endpoint'ler yine
authenticated + policy-korumalı, tüm §12 kuralları fail-closed çalışır.

> **⚠️ Fallback = YALNIZ dev/smoke, geçici.** `crm.territory.*` yeniden kullanımı, territory yetkisi olan bir
> kullanıcının journey yönetebilmesi demektir; **prod'a taşınamaz**, prod tenant'a grant **YASAK**. Ayrıca fallback
> altında `publish` ile `manage` **aynı anahtara** düşer → **SoD dev'de uygulanamaz**; bu bilinçli ve belgeli bir
> boşluktur, kanonik anahtarlarla kapanır → **F-RBAC**.

**Cross-service izin bağımlılığı: YOK.** FU05'in tükettiği tüm endpoint'ler (FU04 path, FU02 subject/topic/audience)
**aynı serviste** ve aynı fallback izinleriyle korunur.

---

## 15. Gateway / API Routing Decision

**Karar: Gateway değişikliği GEREKSİZ.**

- Mevcut `ocelot.json` kaydı: `"/api/crm/knowledge/{everything}"` → `localhost:5061`,
  `["GET","POST","PUT","OPTIONS"]` (`gateway/Diten.ApiGateway/ocelot.json:2245-2260`, **doğrulandı**).
- §8.1'deki **tüm** route'lar bu wildcard'ın altındadır
  (`/api/crm/knowledge/content-engagement-journeys…`, aşama alt-route'ları ve
  `/api/crm/knowledge/content-engagement-journey/contract` dâhil) → **yeni Upstream/Downstream çifti gerekmez**.
- Bu, FU01B §9'daki illüstratif `/api/crm/engagement-journeys` yerine
  `/api/crm/knowledge/content-engagement-journeys` seçilmesinin somut sebebidir (§2.1/S3).
- `DELETE`/`PATCH` wildcard'da **zaten yok** → bu metotlar Gateway seviyesinde de **404**.
- `gateway/Diten.ApiGateway/**/ocelot.json` **protected path**'tir; bu pack oraya yazmaz. İleride explicit route
  istenirse **ayrı `integration-agent` task'ı** açılır.
- Browser JS **`:5061`'e gitmez**; same-origin MVC proxy (`/CRM/ContentEngagementJourneys/api/...`) → Gateway `:5000`.

---

## 16. Acceptance Criteria

Tüm maddeler **test edilebilir** — her biri §17'deki bir backend testi veya smoke adımıyla eşlenir.

**Model & boundary**
- **AC-MODEL-1** `ContentEngagementJourney` bir **şablondur**: hiçbir yerde `CurrentStageId` / journey progress /
  target assignment tutulmaz ve `Contact`/`Account`'a **hiçbir alan eklenmez**.
  *Test:* repo'da `CurrentJourneyStage` / `JourneyProgress` **yok**; `Contact.cs` ve `Account.cs` diff'te
  **değişmemiş**; V-S21 → **400**.
- **AC-MODEL-2** İş versiyonu alanı **`JourneyVersion`**'dır; `EntityBase.Version` iş alanı olarak kullanılmaz.
  *Test:* entity'de `JourneyVersion` var; concurrency 409 testi geçer (V-J15).
- **AC-EMBED-1 (S2)** Aşamalar **journey dokümanının içinde** saklanır: ikinci collection, ikinci repository,
  ikinci controller ve stage-seviyesi `EntityBase` **yoktur**; aşama yazımı **tek doküman** yazımıdır.
  *Test:* persistence'ta yalnız `content_engagement_journeys` kaydedilir; aşama ekleme sonrası **journey**
  `Version`'ı artar; `RegisterClassMaps` journey + 2 embedded tipi kaydeder.
- **AC-EMBED-2 (S2)** Aşama archive'ı **diziden silme değildir**: `StageStatus = archived` + `ArchivedAt` set
  edilir, eleman dokümanda kalır. *Test:* archive sonrası `includeArchived=true` ile aşama **görünür**, `active`
  listede yok.
- **AC-SEQ-1** `StageOrder` journey içinde **unique**, boşluklu numaralamaya izin verir; okuma sırası
  `StageOrder` → `StageCode` ile **deterministiktir**. *Test:* duplicate **409** (handler; DB index yok);
  liste sırası iki çağrıda **aynı**.
- **AC-SEQ-2** `published` journey **en az bir `active` + `IsRequired=true`** aşama içerir.
  *Test:* boş journey publish → **400**.
- **AC-FREEZE-1** `published` sürümün **aşama seti dondurulur**; değişiklik `new-version` ister.
  *Test:* published journey'de aşama ekleme/güncelleme/arşivleme → **409**; `new-version` → yeni `draft` +
  **yeni `StageId`**'li aşama kopyaları + `SupersedesJourneyId` dolu + `JourneyVersion` artmış +
  **otomatik publish yok**; kaynak sürüm **değişmemiş**.
- **AC-FREEZE-2** `new-version` klonunda **iç referanslar yeniden eşlenir**: kopyalanan aşamaların
  `FallbackStageId` ve `BranchConditions[].TargetStageId` değerleri **yeni `StageId`'lere remap edilir** —
  eski sürümün aşamalarına işaret eden **hiçbir** referans kalmaz. *Test:* klonda tüm iç referanslar klonun
  kendi `StageId` kümesindedir (FU04/D5 dersi).
- **AC-VER-1** Aynı `(JourneyCode, LanguageCode)` için örtüşen pencerede **iki published sürüm olamaz**.
  *Test:* ikinci publish → **409**.
- **AC-PUB-1** Publish **yalnız** `POST /{journeyId}/publish` ile yapılır ve
  `crm.knowledge.content-engagement-journey.publish` ister; `Update` ile `published`'a geçiş **400**.
  *Test:* V-J12 **400**; publish izni olmayan aktör **403**.
- **AC-PATH-1** Aşama **published + effective** bir `KnowledgePath`'e bağlanır ve **path'in adımlarını
  kopyalamaz**. *Test:* draft/archived path → **400**; başarılı aşama response'unda `steps` benzeri bir dizi
  **yok**, yalnız `ResolvedPathStepCount` sayacı var.
- **AC-PIN-1** Path sürümü determinizmi: `pinned` sabit kalır, `latest-published` çözülür, çözülemeyen aşama
  **`unresolved`** olarak **görünür** — sessiz sürüm kayması ve sessiz düşme **yok**.
  *Test:* path yeni sürüm yayınlar → `pinned` aşama **değişmez**, `latest-published` aşama **yeni sürümü**
  gösterir; path yayından kalkar → `PathResolutionStatus = unresolved` + `ResolvedKnowledgePathId = null`.
- **AC-REPEAT-1** Tekrar **yasak değil, görünür**: aynı path birden fazla aşamada kullanılabilir, `Repeatable`
  açıkça işaretlenir (varsayılan `false`) ve tekrar **raporlanabilir**.
  *Test:* iki aşama aynı path → **201/201**, `PathUsageCountInJourney = 2`, journey'de `HasRepeatedPaths = true`.
- **AC-BRANCH-1 (S5)** `AdvancementRule` / `FallbackStageId` / `BranchCondition` **authorable ama yalnız
  veridir**: hiçbiri değerlendirilmez ve bir journey bunlar olmadan **baştan sona yürünebilir**.
  *Test:* contract'ta `supportsStageAdvancementEngine` ve `supportsBranchEvaluator` **absent**; koşullu aşamalar
  lineer listede **tam** görünür; `TargetStageId` yabancı journey'de → **400**; koşul verisi response'ta **aynen** döner.
- **AC-LIMIT-1** Doküman büyüme sınırları uygulanır ve **contract'ta ilan edilir** (`maxStagesPerJourney: 100`,
  `maxBranchConditionsPerStage: 20`). *Test:* 101. aşama → **400**; contract limitleri döner.
- **AC-ENGINE-0** Hiçbir endpoint/parametre öneri, skor, next-stage, current-stage, ilerletme veya completion
  **döndürmez**. *Test:* 15 yasak flag contract'ta **absent** (false bile değil);
  `recommend`/`nextStage`/`currentStage`/`advance`/`score` parametresi **yok**; progress/completion alanı **yok**.
- **AC-SCOPE-1 (S6)** `CampaignId` / `BrandId` / `ProductId` / `SegmentId` **entity'de ve DTO'da yoktur**;
  gönderilirse **400**. *Test:* V-J17; entity dosyasında bu alanlar **yok**.

**Sözleşme koruması**
- **AC-FU04-1..4** `KnowledgePath` ailesi **okunur, yazılmaz**; `IKnowledgePathReader` **imza/davranışı
  değişmez**; FU04 contract'ı, view'ları ve JS'i **dokunulmaz**; aşama path adımlarını **kopyalamaz** (§2.2).
  *Test:* FU04 test suite'i **değişmeden PASS**; diff'te `Features/Knowledge/Path/**`, `KnowledgePath.cs`,
  `Views/CRM/KnowledgePaths/**` **yok**.
- **AC-FU02-1** FU02 (`KnowledgeContent`, `Subject`, `Topic`, `AudienceProfile`) **okunur, yazılmaz**.
  *Test:* FU02'nin 23 testi değişmeden PASS; `KnowledgeContent.cs` diff'te **yok**.
- **AC-FU03-1** FU03 concept aggregate'lerine **hiç dokunulmaz** (ne okuma ne yazma).
  *Test:* diff'te `Features/Knowledge/Concept/**` ve `Concept*.cs` **yok**; FU03 suite'i PASS.
- **AC-BOUNDARY-1** MOD-0155 / MOD-0166 / MOD-0309 / MOD-0165 / MOD-0167 / MDM **mutate edilmez**.
  *Test:* smoke'ta before/after diff **identical**; repo scope dışına yazma **yok**.

**Vokabüler & fail-closed**
- **AC-VOCAB-1** `JourneyStatus` / `Source` / `StageType` / `AdvancementRule` / `PathVersionPinPolicy` /
  `StageStatus` **fail-closed** doğrulanır; set dışı değer **400**; contract vokabüler listesini **yayınlar**.
  **(D-VOCAB=A)** doğrulama in-domain'dir ve MOD-0048 yayını **runtime ön koşulu değildir**;
  **(D-VOCAB=B)** doğrulama yayınlanmış MOD-0048 setine bağlanır ve set yoksa **hiçbir yazma geçmez**.
  *Test:* her vokabüler için 1 geçersiz değer → **400**.

**UI**
- **AC-UI-1** Tüm `Views/CRM/ContentEngagementJourneys/*.cshtml` dosyalarında `Layout = "_LayoutTenantShell"`
  **AÇIKÇA** yazılıdır. *Test:* grep ile **9 dosyada** açık layout.
- **AC-UI-2** Klasör kanonik **Compact 9 dosya** setini taşır; `_CreateEditOffcanvas.cshtml` /
  `_DetailsQuickView.cshtml` **yoktur**; **ikinci klasör/DataTable/RESX seti yoktur**.
  *Test:* `verify_datatable_page.py --area CRM --module ContentEngagementJourneys --reference compact
  --api-profile proxy` **tek koşu**, **yapısal FAIL yok** (kontrol-kulesi re-run: **85 PASS / 9 FAIL** = **7
  archive-only bulk/select-all kontrolü N/A** + **2 proxy-profile false-positive** — FU02/FU03/FU04 baseline ile diff ∅).
- **AC-UI-3** Aşama alt-editörü journey formunun **içinde**dir (repeater); aşama eklemek için **ayrı sayfaya
  gidilmez**; her aşama satırı `StageOrder` ile sıralanır ve zorunlu/opsiyonel + `Repeatable` +
  `unresolved` path **rozetle** ayırt edilir. *Test:* `_Form.cshtml` repeater render'ı + sıralama + rozetler.
- **AC-UI-4** Aşama içindeki **BranchCondition repeater** (S5) authorable'dır; `ConditionCode`/`Description`/
  `TargetStageId` girilebilir, `TargetStageId` ve `FallbackStageId` seçenekleri **aynı journey'in aşamalarıyla**
  sınırlıdır. *Test:* repeater render'ı + hedef listesi yalnız aynı journey aşamaları.
- **AC-UI-5** Path seçici **published + effective** FU04 path'lerini listeler; draft/archived path
  **listelenmez**; seçilen path'in `PathCode` + `PathVersion` + aktif adım sayısı kullanıcıya **görünür**.
  *Test:* draft/archived path listede yok; seçim sonrası kod/sürüm/adım sayısı render edilir.
- **AC-UI-6** `published` journey'in aşama alt-editörü UI'da **disabled** + gerekçe notu + `new-version` aksiyonu
  görünür (sessiz 409 sürprizi yok). *Test:* frozen journey'de repeater disabled ve açıklama render edilir.
- **AC-UI-7** `AdvancementRule` ve branch alanlarının yanında **"beyandır, sistem değerlendirmez"** yardım metni
  görünür — kullanıcı motor sanmaz. *Test:* alt-editörde bu metin 7 dilde render edilir.
- **AC-UI-8** Hiçbir dropdown hardcoded vokabüler taşımaz; tümü `content-engagement-journey/contract`'tan beslenir.
  *Test:* view/JS'te sabit `stageType`/`status`/`advancementRule` dizisi **yok**.
- **AC-L10N-1** 7 dil (`ar/en/es/fr/ru/tr/zh`) RESX **anahtar paritesi**; `SharedResource` menü anahtarı ×7;
  `index.l10n.js` camelCase→PascalCase köprüsü çalışır. *Test:* parite scripti + UI'da `undefined` anahtar yok.
  **Not (FU03/F-L10N dersi):** 5 dil İngilizce placeholder ile **geçilmez** — çeviriler gerçek olmalıdır.

**Yetkilendirme & routing**
- **AC-AUTH-1** Her endpoint `[Authorize]` + `[HasPermission(...)]` taşır; permission **seed edilmez**;
  anahtar seti **read / manage / publish** (stage için ayrı anahtar yok).
  *Test:* controller'larda öznitelik var; diff'te RBAC seed/role-template **yok**.
- **AC-GW-1** `ocelot.json` **değişmemiştir** ve tüm route'lar Gateway `:5000` üzerinden çalışır.
  *Test:* diff'te gateway dosyası yok; smoke tüm çağrıları `:5000`'den yapar; `DELETE`/`PATCH` → **404**.

---

## 17. Test Expectations

### 17.1 Build & statik doğrulama

- `dotnet build` **PASS**: `Diten.CrmService` + `frontend/Diten.Web` (+ Gateway derlenmeye devam eder).
- `verify_datatable_page.py . --area CRM --module ContentEngagementJourneys --reference compact --api-profile proxy`
  → **TEK koşu**, PASS.
  - **Beklenen N/A (kontrol-kulesi doğruladı, 85/9):** archive-only modülde **7 bulk/select-all kontrolü** N/A + **2 proxy-profile false-positive** = toplam **9 FAIL**, FU02/FU03/FU04 baseline ile birebir. Not: eski "6 bulk-delete" yanlış sayımdı — select-all checkbox header de bu N/A sete dâhildir.
  - **Beklenen yapısal FAIL: YOK** — tek yüzey + tek klasör olduğu için FU03'ün 2 hibrit FAIL'i burada
    **oluşmaz** (§11.1).
- RESX **parite** kontrolü: 1 modül × 7 dil + `SharedResource` ×7 → eksik/fazla anahtar **yok**.
- `verify_module_id.py --check-id MOD-0162-FU05 …` → exit 0 (closeout'ta tekrar).
- **Regresyon guard'ı:** FU02 + FU03 + FU04 test suite'leri **değişmeden** PASS (ekleme additive'dir).

### 17.2 Backend unit/integration testleri (`Diten.CrmService.Application.Tests`) — hedef **≥ 45 test**

| # | Küme | Adet |
|---|---|---|
| 1 | Journey × create/update/archive/publish/new-version mutlu yol | 5 |
| 2 | Gömülü aşama × add/update/archive mutlu yol (+ sıralı liste + çözülmüş path) | 5 |
| 3 | **AC-EMBED-1**: aşama yazımı **tek dokümana** gider · **journey** `Version`'ı artar · ikinci collection **yok** | 3 |
| 4 | **AC-EMBED-2**: archive aşamayı **diziden silmez** (`includeArchived` ile görünür, `active` listede yok) | 2 |
| 5 | V-J03 duplicate code+version · V-J04/05/06 archived/tutarsız FU02 referansları | 4 |
| 6 | V-J07 effective window · V-J08 vokabüler · V-J09 archived journey'e yazım · V-J16 `stages` dizisi reddi | 4 |
| 7 | V-J10 örtüşen published (409) · V-J11 zorunlu-aşamasız publish (400) · V-J12 update-ile-publish (400) · V-J14 | 4 |
| 8 | **AC-FREEZE-1**: frozen journey'de aşama add/update/archive → 409 (3) + `new-version` kopyası (yeni `StageId`, `SupersedesJourneyId`, `JourneyVersion`++, kaynak değişmedi) (2) | 5 |
| 9 | **AC-FREEZE-2**: `new-version` klonunda `FallbackStageId` **ve** `BranchConditions[].TargetStageId` yeni `StageId`'lere **remap** edilir; eski kümeye referans **kalmaz** | 2 |
| 10 | V-S03/S04 duplicate order & code — **DB index olmadan handler doğrulaması** (§4.5) | 3 |
| 11 | V-S05/S06 path uygunluğu: draft path → 400 · archived path → 400 · published+effective → 201 | 3 |
| 12 | V-S07 **dirty-check**: dokunulmamış `RecommendedKnowledgePathId` ile PUT → **200**; değiştirilip archived path'e → **400** | 2 |
| 13 | V-S10 fallback (yabancı journey / kendisi / **geriye işaret = geçerli**) | 3 |
| 14 | V-S11 visit aralığı (`Max < Min` → 400 · `< 1` → 400 · geçerli aralık → 201) | 3 |
| 15 | **V-S15 (S5)** branch condition: yabancı `TargetStageId` → 400 · aynı journey → OK · **veri aynen döner** | 3 |
| 16 | **AC-REPEAT-1**: aynı path iki aşamada → kabul + `PathUsageCountInJourney = 2` + `HasRepeatedPaths` · `Repeatable` varsayılan `false` | 3 |
| 17 | V-S14 cross-subject / cross-language **kabul edilir ve flag'i görünür** | 2 |
| 18 | V-S17 dangling fallback/branch archive → 409 · V-S20 journey archive → gömülü aşamalar archived kabul (ayrı yazım yok) | 2 |
| 19 | **V-S18** limit: 101. aşama → 400 · 21. branch condition → 400 | 2 |
| 20 | **AC-PIN-1**: pinned sabit · latest-published çözülür · unresolved görünür | 3 |
| 21 | **V-J15/V-S19 concurrency**: eş zamanlı iki aşama yazımı → ikincisi **409** (ortak journey token'ı) | 2 |
| 22 | Tenant isolation: başka tenant'ın journey'i **görünmez, yazılamaz** · V-J01 payload injection | 3 |
| 23 | **AC-SCOPE-1 / AC-MODEL-1**: `campaignId`/`brandId`/`productId`/`segmentId` → 400 (V-J17) · `currentStage`/`progress` → 400 (V-S21) | 2 |
| 24 | Contract: 14 flag `true`, **15 yasak flag absent** (false bile değil), vokabüler + **limitler** yayınlanır | 2 |
| 25 | **RegisterClassMaps:** journey + `…Stage` + `…BranchCondition` kayıtlı (aksi hâlde gömülü Guid'ler binary → filtreler sessizce boş) | 1 |
| 26 | `IContentEngagementJourneyReader`: yalnız published+effective + `active` aşama döner; sıralama deterministik; **skor/seçim/ilerletme yok** | 3 |
| 27 | **Regression:** FU02 (23 test) + FU03 + FU04 suite'leri **değişmeden PASS** | mevcut suite |

### 17.3 Authenticated smoke (Gateway) — `scripts/smoke-mod0162-fu05-content-engagement-journey-authenticated.ps1`

Tenant `97c59330…`, login **`X-Tenant-Id` header ile** (aksi hâlde platform `…0001` token'ı gelir).
FU04 script'i şablondur. PowerShell 5.1 tuzağı: `@(… | Where-Object …).Count` sarmalayıcısı zorunlu.
**Ön koşul:** FU04'te en az **iki published+effective** path (biri `latest-published` senaryosu için aynı
`PathCode` altında yeni sürüm alacak) + **bir draft** + **bir archived** path.

```text
 1 login → token
 2 GET  content-engagement-journey/contract                        → 200, 14 flag true, 15 yasak flag absent, limitler dolu
 3 POST content-engagement-journeys (FU02 subject'i ile)           → 201  (draft)
 4 POST content-engagement-journeys duplicate code+version         → 409
 5 POST content-engagement-journeys { effectiveTo < effectiveFrom }→ 400
 6 POST content-engagement-journeys { campaignId: … }              → 400  (V-J17 / S6)
 7 POST …/{id}/stages (order 10, required, pinned, published path) → 201  (tek doküman yazımı)
 8 POST …/{id}/stages duplicate order 10                           → 409
 9 POST …/{id}/stages (draft path ile)                             → 400
10 POST …/{id}/stages (archived path ile)                          → 400
11 POST …/{id}/stages (order 20, latest-published, aynı PathCode)  → 201
12 POST …/{id}/stages (order 30, repeatable=true, aynı path)       → 201  (tekrar serbest)
13 GET  …/{id}/stages                                              → 200, PathUsageCountInJourney=2, HasRepeatedPaths=true
14 PUT  …/{id}/stages/{stageId} { fallbackStageId = kendisi }      → 400
15 PUT  …/{id}/stages/{stageId} { fallbackStageId = önceki aşama } → 200  (geriye işaret GEÇERLİ)
16 PUT  …/{id}/stages/{stageId} + branchConditions[]               → 200  (S5: veri aynen döner)
17 PUT  …/{id}/stages/{stageId} branch → yabancı TargetStageId     → 400
18 PUT  …/{id}/stages/{stageId} { maxVisitNumber < minVisitNumber }→ 400
19 PUT  …/{id}/stages/{stageId} { advancementRule: "uydurma" }     → 400  (fail-closed)
20 PUT  …/{id} { stages: [...] }                                   → 400  (V-J16)
21 PUT  …/{id} { journeyStatus: "published" }                      → 400  (V-J12)
22 POST …/{id}/publish (zorunlu aşama yokken — ayrı draft journey) → 400  (V-J11)
23 POST …/{id}/publish                                             → 200  (StageSetFrozenAt dolu)
24 POST …/{id}/stages (published journey'e yeni aşama)             → 409
25 PUT  …/{id}/stages/{stageId} (published journey)                → 409
26 POST …/{id}/publish (örtüşen ikinci sürüm)                      → 409  (V-J10)
27 FU04'te aynı PathCode'un YENİ sürümü publish edilir → GET …/{id} → pinned aşama DEĞİŞMEZ, latest-published aşama YENİ sürümü gösterir
28 FU04'te o path inactive/archived → GET …/{id}                   → PathResolutionStatus = unresolved, ResolvedKnowledgePathId = null (aşama GÖRÜNÜR)
29 POST …/{id}/new-version                                          → 201 (draft, JourneyVersion++, aşamalar YENİ StageId ile kopyalandı, fallback/branch hedefleri REMAP, SupersedesJourneyId dolu)
30 GET  content-engagement-journeys?status=published&effectiveAt=…  → 200, yalnız published+effective journey'ler
31 PUT  …/{id} { tenantId: "<yabancı>" }                            → claim kazanır (yabancı tenant yazılmaz)
32 GET  content-engagement-journeys/{başka tenant id}               → 404
33 POST …/{id}/stages/{stageId}/archive (fallback hedefi iken)      → 409
34 POST …/{id}/stages/{stageId}/archive (bağımsız aşama)            → 200, GET ?includeArchived=true → aşama GÖRÜNÜR (silinmedi)
35 POST …/{id}/archive → GET …/{id}                                 → aşamalar aynı dokümanda archived kabul
36 POST …/{id}/stages { currentStage: … }                           → 400  (V-S21 runtime state)
37 DELETE / PATCH herhangi bir route                                → 404
38 GET  /api/crm/knowledge/content-engagement-journey-stages         → 404 (düz aile yok — S2)
39 FU04 path kaydı + FU02 subject kaydı DEĞİŞMEDİ                    → before/after diff identical
40 cleanup: archive-only (**hard delete YOK**)
```

### 17.4 Browser smoke

`/CRM/ContentEngagementJourneys` açılır; liste/filtre/create/edit/details/archive akışları çalışır; **aşama
alt-editörü ve aşama-içi branch repeater aynı sayfada** çalışır (ekle/düzenle/arşivle, sıralama, rozetler);
path seçici yalnız published+effective path'leri gösterir; `unresolved` aşama rozetle görünür; frozen journey'de
alt-editör disabled; publish/new-version aksiyonları çalışır; `AdvancementRule` yanında "beyandır,
değerlendirilmez" notu görünür; dil değiştirince (7 dil) `undefined` anahtar yok; konsolda hata yok.
**Not:** `.resx` değişiklikleri **tam fleet restart** ister.

---

## 18. Ready-for-dev Checklist

- [x] Boundary (FU01B) `approved` ve okundu; sapmalar §2.1'de (S1–S8) gerekçelendi; **S2 sapma değil**
      (boundary §16 önerisi benimsendi)
- [x] DCP-002 kimlik kapısı **PASS** (exit 0, 2026-08-26) — komut ve çıktı §başlıkta
- [x] F1 kanonik adı (`ContentEngagementJourney` / `content-engagement-journey`) **tüm** literal yüzeylerine
      (collection · route · permission · vokabüler · flag · view klasörü) uygulandı; MOD-0166 ayrımı §2.4
- [x] Prerequisite FU02 **shipped**, FU04 **done** kod üzerinden doğrulandı
      (`Features/Knowledge/Path/**`, `Domain/Entities/KnowledgePath.cs`, `KnowledgePathsController.cs`)
- [x] Golden Reference (DEV-0001 Compact) referans alındı; **tek yüzey** için alan sayımı **gösterildi**
      (§11.1 — 12 > 8 ⇒ Compact); gömülü aşama alt-editörü ayrı yüzey **değildir** (15 alan, bilgi amaçlı)
- [x] Frontend dosya seti **tek tek** enumere edildi (§11.2 — **tek klasör**, kanonik 9 dosya + 3 JS + 7 RESX)
- [x] Frontmatter zorunlu alanların tümü dolu (`service`, `shell`, `golden_reference`, `entity_base`,
      `form_field_count`)
- [x] Layout & Shell Contract'ta Razor `Layout` **açıkça** yazıldı ve AC-UI-1'de test edilebilir madde oldu
- [x] Backend File Convention Golden Reference **naming**'i ile birebir; **dosya gruplama sapması açıkça beyan
      edildi** (§10 uyarı kutusu, F-FILE)
- [x] Validation Rules her alan/kural için yazıldı (§12 — 17 journey + 21 aşama kuralı); **dizi-içi unique index
      olmadığı** ve tek savunmanın handler olduğu açıkça yazıldı
- [x] Failure Path ≥ 4 senaryo (§13 — 14 senaryo: duplicate/missing/concurrency/unauthorized/frozen/cross-tenant/
      path-uygunsuz/unresolved/dangling/limit/runtime-state/…)
- [x] Authorization Convention: **3 anahtar** (read/manage/publish) + policy + actor + **fallback'in SoD boşluğu**
      açıkça yazıldı; PKS-001 hyphen-in-segment kuralıyla uyumlu
- [x] Gateway kararı **açık**: değişiklik **gereksiz**, wildcard doğrulandı (`ocelot.json:2245-2260`), route
      seçimi §2.1/S3'te gerekçeli
- [x] Acceptance Criteria test edilebilir maddelere bağlandı (§16 — her madde §17'de bir teste eşlenir)
- [x] Test Expectations build + verifier (**tek** compact koşusu) + 7 dil RESX + ≥45 backend testi +
      authenticated smoke (40 adım) kapsıyor
- [x] Protected Paths eksiksiz (FU02/FU03/FU04 yüzeyleri, diğer domain servisleri, ocelot, RBAC, registry,
      Mongo dâhil) — §6
- [ ] 🔶 **`D-VOCAB` kullanıcı kararı** (§4.4: in-domain **A** mı, MOD-0048 **B** mi) — **AÇIK**
- [ ] `status: ready-for-dev` + `runtime_code_allowed: true` — **AÇIK**; pack kullanıcı incelemesi için `draft`
      bırakıldı, flip ayrı kullanıcı aksiyonudur (D-VOCAB kapandıktan sonra)

---

## 19. Implementation Notes

Repo'dan çıkarılmış, bu FU'yu doğrudan vuran tuzaklar:

1. **RegisterClassMaps** — `ContentEngagementJourney` **ve embedded tipler** (`…Stage`, `…BranchCondition`)
   `Persistence/DependencyInjection.cs`'e eklenmezse gömülü `Guid` alanları (`StageId`,
   `RecommendedKnowledgePathId`, `FallbackStageId`, `TargetStageId`) binary yazılır ve filtreler **sessizce boş
   döner** (MOD-0151 FU05 / `AccountTerritoryAssignment` dersi).
2. **Tek doküman yazımı** — aşama ekleme/güncelleme/arşivleme journey root'unu `EntityBase.Version` kontrolüyle
   **replace** eder. Pozisyonel dizi güncellemesi (`$set: stages.$[…]`) ile tam-doküman replace **karıştırılmaz**;
   tek kod yolu korunur, aksi hâlde iki farklı concurrency davranışı doğar.
3. **Transaction gerekmez** — çok-doküman atomiklik yok; `SupportsTransactionsAsync` guard'ı ve compensation
   **bu FU'da yazılmaz**. `new-version` iki bağımsız yazımdır ve yarım journey üretmez.
4. **`new-version` remap'i unutulmaz** — klonlanan aşamalar **yeni `StageId`** alır; `FallbackStageId` ve
   `BranchConditions[].TargetStageId` **eski** id'lere işaret etmeye devam ederse klon sessizce bozuk doğar
   (FU04/D5'te aynı tuzak yaşandı) → AC-FREEZE-2 ve §17.2/küme 9 bu yüzden zorunludur.
5. **Dizi-içi unique index YOK** — `StageOrder`/`StageCode` tekilliği Mongo index'iyle zorlanamaz; handler +
   validator **tek savunma hattıdır**.
6. **Doküman büyümesi** — 16MB Mongo limiti; §4.2 sınırları (100 aşama / 20 koşul) contract'ta ilan edilir ve
   V-S18 ile zorlanır. Journey listesi sorgularında `Stages` alanı **projeksiyonla dışarıda bırakılır**
   (DataTable yalnız sayaç rozetleri gösterir).
7. **Parallel-array tuzağı** — `EffectiveFrom` + `EffectiveTo` (ikisi de `DateTimeOffset`) **birlikte
   index'lenmez, birlikte sort edilmez**; gerekirse in-memory sort. DateTimeOffset BSON dizisi olarak saklandığı
   için tarih karşılaştırmalarında `.Date` tuzağına da dikkat (CRM DateTimeOffset dersi).
8. **Partial index `$ne` yasak** — `Filter.Ne(x, null)` içeren partial index servisi başlangıçta **crash-loop**'a
   sokar; `Filter.Type(...)` / `$lt` kullan (Platform 5057 dersi).
9. **Path çözümlemesi FU04 seam'ini genişletmez** — `IKnowledgePathReader.KnowledgePathCriteria`'da `PathCode`
   **yoktur** ve eklenmeyecektir; FU05 kendi resolver'ı ile `IKnowledgePathRepository`'yi okur (§2.2/AC-FU04-2).
10. **`unresolved` aşama gizlenmez** — çözülemeyen aşama listeden düşürülürse kullanıcı yolculuktaki boşluğu
    göremez; FU01B §10'un "sessiz sürüm kayması yasak" kuralı bunu kapsar.
11. **Freeze mantığı tek yerde** — `StageSetFrozenAt` kontrolü `ContentEngagementJourneyValidation`'da toplanmalı;
    hem aşama handler'ları hem journey update handler'ı aynı yardımcıyı çağırmalı (iki kopya = iki farklı davranış).
12. **"Motor değil" disiplini UI'da da** — `AdvancementRule` / `FallbackStageId` / `BranchCondition` alanları
    kullanıcıya **beyan** olarak sunulur (AC-UI-7). Aksi hâlde saha ekibi sistemin aşama ilerlettiğini sanar;
    ilerletme **F-DETAIL** kapsamındadır.
13. **Endpoint'ler fleet restart'a kadar 404** — yeni controller'lar servis yeniden başlamadan görünmez; `.resx`
    değişiklikleri de **tam restart** ister. Build kilidinde `-t:CoreCompile` yöntemi.
14. **L10n bridge** — `index.l10n.js` camelCase→PascalCase dönüşümü atlanırsa `window.L10n` anahtarları
    `undefined` döner (toast "(undefined: corrId)").
15. **MOD-0155 / MOD-0166 açılmaz** — bu FU'nun tüketicileri ileride MOD-0155 ve (referans yönünde) MOD-0166'dır;
    ancak `IContentEngagementJourneyReader`'ı **tüketen** kod bu FU'da yazılmaz (yalnız seam yayınlanır).

---

## 20. Follow-up Items

| # | Follow-up | Neden |
|---|---|---|
| 🔶 **D-VOCAB** | **Kullanıcı kararı (pack içi, AÇIK)** — vokabüler in-domain (A) mı MOD-0048 (B) mi | §4.4; `ready-for-dev` flip'inin ön koşulu |
| **F-RBAC** | **MOD-0162-FU05-RBAC** — `crm.knowledge.content-engagement-journey.{read,manage,publish}` katalog + grant; dev fallback'in kaldırılması | RBAC en sona bırakıldı (FU01B §11); fallback altında **SoD uygulanamıyor** (§14) |
| **F-RD** | MOD-0048 set publish: `content-engagement-journey-status` / `-source` / `-stage-type` / `-advancement-rule` / `-path-pin-policy` | D-VOCAB=A ise blocker **değil**; B ise **blocker** (§4.4) |
| **F-DETAIL** | **Digital Detailing / Learning Execution** — stage advancement engine, branch evaluator, **current stage state**, gösterim evidence'ı | FU01B §6/§13'te kasıtlı kapalı; bu FU'nun yazdığı `AdvancementRule` + `BranchCondition` verisi **orada** değerlendirilir (FU01B/F4) |
| **F-TARGET** | **Journey target assignment sahipliği** — hangi doktora/öğrenciye hangi journey atanır | FU01B §8/F6: MOD-0165/MOD-0167 + MOD-0155; bu FU'da kasıtlı **yok** |
| **F-155** | MOD-0155 tüketimi: visit objective → önerilen aşama/path, gösterim evidence'ı | Seam bu FU'da yayınlanır, tüketici kod **açılmaz** (FU01B §9) |
| **F-COMPLETION** | **MOD-0309 completion sözleşmesi** — `AdvancementRule` ↔ completion/score kaydı eşlemesi | Beyan burada, ölçüm orada (FU01B §12/F7) |
| **F-0166** | MOD-0166 otomasyon journey'inin bir `ContentEngagementJourney` aşamasını **referans alması** | Yön tek taraflı: MOD-0166 → bu FU (§2.4); bugün **hiçbir kod yok** |
| **F-CAMPAIGN-LINK** | `CampaignId` / `BrandId` / `ProductId` / `SegmentId` bağlarının **doğrulanmış** biçimde açılması | §2.1/S6: bugün sahte FK yaratmamak için **hiç açılmadı**; Brand/Product master (FU01B/F2) ve MOD-0167 kurulduğunda additive |
| **F-WF** | Journey approval workflow (MOD-0023) — `review`/`approved` bugün yalnız metadata | FU01B §5.2/F8; en sona bırakıldı |
| **F-FILE** | Knowledge feature klasöründe komut-başına-dosya'ya dönüş (Golden Reference birebir) | §10 sapması FU02/FU03/FU04 ile ortak; tek seferde tüm Knowledge ailesi için yapılmalı |
| **F-CAP** | Aşama/branch limitlerinin (100/20) gerçek kullanım verisiyle gözden geçirilmesi | Embedded model doküman boyutuna bağlıdır; limit contract'ta ilan edilir, ileride ayarlanabilir |
| **F-MIG** | Legacy çok-ziyaretli akış crosswalk'u (legacy kampanya/ziyaret serisi → `ContentEngagementJourney`) | FU03/FU04/F-MIG ile ortak; bu pack yalnız **greenfield authoring** açar |
| **F-STATUS** | Closeout'ta `execution/registries/module-implementation-status.md` satırı | Kod-izli modül durum takibi (module-pack-standard §16) — **yalnız kullanıcı onayıyla** |

---

## Handoff

Module pack `draft` olarak hazır. Lütfen inceleyip gerekli alan/scope düzeltmelerini yapın ve **§4.4'teki
`D-VOCAB` kararını** verin. Geliştirme için `status` `approved` veya `ready-for-dev` olmalı ve
`runtime_code_allowed: true` yapılmalıdır; sonra `@orchestrator MOD-0162-FU05` çağrılır.

Hazırlık sırasında Golden Reference **compact** şablon olarak alındı — sapma yok (dosya gruplaması hariç, §10'da
açıkça beyan edildi).
