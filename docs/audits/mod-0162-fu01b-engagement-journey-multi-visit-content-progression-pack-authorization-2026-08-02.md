# MOD-0162-FU01B — EngagementJourney / Multi-Visit Content Progression Boundary Pack Authorization

> Tarih: 2026-08-02
> Kimlik: **MOD-0162-FU01B — EngagementJourney / Multi-Visit Content Progression Boundary** (parent `MOD-0162`)
> Kapsam: Çoklu visit/session içerik ilerleme modeli + boundary — **kod yazma yok, runtime yok**
> Sonuç: **PASS**

---

## 1. Preflight

| Kontrol | Sonuç |
|---|---|
| Task türü | Documentation / boundary authorization (implementation değil) |
| Değiştirilen runtime kod | **0 dosya** |
| Çalışma alanı | `execution/domains/commercial-suite/module-packs/**` + `docs/audits/**` |
| DCP-002 `MOD-0162-FU01B` | `OK  MOD-0162-FU01B: proven against Blueprint/registry.` (exit 0, `--parent MOD-0162`) |
| Kimlik notu | MOD-0162-FU01 identity note'u geçerli (domain-nötr model; EA yatay capability göçü kararı FU01 §18/F1) |
| Registry satırı yazımı | **Yapılmadı** (pack yetkisi dışı) |

Komut:

```
py .antigravity/scripts/verify_module_id.py . --check-id MOD-0162-FU01B \
   --name "EngagementJourney / Multi-Visit Content Progression Boundary" --parent MOD-0162
```

**Ek preflight bulgusu (governance):** Blueprint'te `MOD-0166 Journeys & Automation`'ın SoR'u
*journey definitions, trigger rules, journey run logs*'tur ve `crm-sor-boundary.md` satır 20/43 bunu teyit eder.
"Journey" adı çakışma riski taşıdığı için pack §2.1'de kesin bir sınır kararı yazıldı ve EA adlandırma
uzlaştırması follow-up'ı (F1) açıldı.

---

## 2. Dependency Confirmation

| Ön koşul | Durum | Not |
|---|---|---|
| MOD-0162-FU01 Knowledge / Content & Subject Taxonomy | **PASS** | [rapor](knowledge-content-subject-taxonomy-pack-authorization-2026-08-02.md) — hard prerequisite |
| MOD-0162-FU01A KnowledgePath / Content Sequence | **PASS** | [rapor](mod-0162-fu01a-knowledge-path-content-sequence-pack-authorization-2026-08-02.md) — hard prerequisite |
| MOD-0165 / MOD-0167 Visit Frequency Ownership | **PASS** | [rapor](mod-0165-mod-0167-visit-frequency-call-cycle-policy-ownership-pack-authorization-2026-08-02.md) |
| MOD-0150 Contact Availability | **PASS** | |
| MOD-0151 FU09A Visit/Route Readiness | **PASS** | |
| MOD-0155 | **Başlamadı** | Consumer boundary sözleşme olarak yazıldı |
| Brand/Product Master | **Başlamadı** | Journey'de **optional/future** kaldı |
| Workflow / approval | **En sona bırakıldı** | `review`/`approved` yalnız future-ready metadata |
| MOD-0166 Journeys & Automation | **Pack yok (reserved/planned)** | Ad/kapsam sınırı §4.1'de yazıldı |

---

## 3. Business Need Summary

FU01A **tek görüşme/ders içindeki sırayı** kapattı. Açık kalan üst seviye sorular:

- 1. visit/session'da hangi path kullanılacak?
- 2. visit/session'da hangi path **tekrar** veya **devam** olarak kullanılacak?
- 3. visit/session'da doktor/öğrenci tepkisine göre hangi aşamaya geçilecek?
- Aynı içerik bazı visitlerde **tekrar edilebilir mi**?
- Doktor tek görüşmede ikna olmadıysa sonraki görüşmede hangi anlatım aşaması uygulanacak?

Kapanmazsa çok-ziyaretli akış ya `VisitPlan` içine gömülür (MOD-0155'e sahiplik kayması), ya FU01A path'ine
`visit-1/visit-2/visit-3` mantığı sızar (path'in tekrar kullanılabilirliği biter), ya da `Contact`/`Account`
üzerinde bir `CurrentJourneyStage` alanı açılır (şablon ile runtime state karışır).

---

## 4. Ownership Decision

| Soru | Sahip |
|---|---|
| Ne anlatılacak / kime uygun? | MOD-0162-FU01 |
| Tek görüşmede hangi sırayla? | MOD-0162-FU01A |
| **Çoklu görüşmede hangi aşama, hangi path?** | **Bu FU (MOD-0162-FU01B)** |
| Ne sıklıkla gidilecek? | MOD-0165 / MOD-0167 |
| Visit/route plan, execution, **current stage progress**, usage evidence | **MOD-0155** |
| Completion / score / attendance / certificate | **MOD-0309** |
| **Otomasyon journey'i (trigger, suppression, kanal, run log)** | **MOD-0166** |
| Dosya / doküman | MOD-0028 / MOD-0029 |

**Temel kural:** *`EngagementJourney` bir şablondur, bir execution değildir.*

### 4.1 MOD-0166 ad çakışması — kesin sınır

| | **Bu FU — `EngagementJourney`** | **MOD-0166 — Automation Journey** |
|---|---|---|
| Doğası | İçerik ilerleme **şablonu** | **Otomasyon orkestrasyonu** |
| Yürütücü | **İnsan** (MR ziyareti, ders/oturum) | **Sistem** (tetiklenen akış) |
| İçerir | Stage + `RecommendedKnowledgePathId` | Trigger, wait, suppression, kanal aksiyonu, run log |
| İçermez | **Trigger/aksiyon/kanal/suppression/run-log/runtime state yok** | — |
| Yön | MOD-0166 ileride bir stage'i **referans alabilir** | `EngagementJourney` otomasyon **yürütmez** |

EA adlandırma uzlaştırması F1 follow-up'ı olarak açıldı (alternatifler: `ContentEngagementJourney` /
`EngagementProgression`); pack gövdesi addan bağımsızdır.

---

## 5. KnowledgePath vs EngagementJourney

```text
KnowledgeContent        = tekil içerik
KnowledgePath           = tek visit/session içindeki içerik sırası          (FU01A)
EngagementJourney       = çoklu visit/session boyunca ilerleyen aşamalar    (bu FU)
EngagementJourneyStage  = journey içindeki bir aşama; bir KnowledgePath'e bağlanır
```

Reddedilen beş model: `VisitPlan` içine gömme · path'e `visit-N` mantığı · `BrandProductJourney` ·
`Contact.CurrentJourneyStage` · `Account.CurrentJourneyStage`. Son ikisi ayrıca **runtime state**tir ve bu FU'nun
kapsamı dışındadır (§10).

---

## 6. Authorized EngagementJourney Model

`JourneyId` · `TenantId` (**JWT claim'inden**, payload'da asla) · `JourneyCode` (stabil) · `JourneyName` ·
`Description?` · **`SubjectId` (zorunlu)** · `TopicId?` · `AudienceProfileId?` · **`Objective` (zorunlu)** ·
`Language?` · `Version` · `Status` · `EffectiveFrom` · `EffectiveTo?` · `Source` ·
`CampaignId?` / `BrandId?` / `ProductId?` / `SegmentId?` **(optional/future)** · audit dörtlüsü.

Kurallar: `Version` zorunlu · **hard delete yok** · archived journey yeni planlama/öneri için kullanılmaz ·
yalnız **published + effective** tüketilebilir · **draft/review MOD-0155'e aktif öneri gitmez** · Brand/Product
zorunlu değil · pharma dışı subject'ler birinci sınıf desteklenir.
Vokabülerler MOD-0048 set'i olarak yönetilir (hardcoded enum yasak → fail-closed 400).

---

## 7. Authorized EngagementJourneyStage Model

`StageId` · `JourneyId` · **`StageOrder`** (unique, duplicate → **409**, boşluk serbest 10/20/30) · `StageCode` ·
`StageName` · `StageObjective` · **`RecommendedKnowledgePathId`** (published+effective) ·
**`PathVersionPinPolicy`** (`pinned` varsayılan / `latest-published`) · `MinVisitNumber?` · `MaxVisitNumber?`
(`Max < Min` → **400**) · `Repeatable` · `IsRequired` · `AdvancementRule?` *(future)* · `FallbackStageId?`
*(future; aynı journey içinde, kendisi olamaz → 400)* · `BranchCondition?` *(future)* · `Notes?` · audit dörtlüsü.

Kurallar: stage path'e **bağlanır**, step detaylarını **kopyalamaz** · bir path birden fazla stage'de kullanılabilir ·
`published` journey **en az bir `IsRequired=true` stage** içermeli (aksi 400) · stage version/history korunur ·
**published journey'de stage seti dondurulur**; aynı `(JourneyCode, Language)` örtüşen pencerede iki published
sürüm → **409**.

---

## 8. Pharma Multi-Visit Example

```text
EngagementJourney: Almiba Q1 Doctor Engagement Journey
Stage 1 — Awareness / First Visit     → Almiba Visit 1 Awareness Flow
Stage 2 — Reinforcement / Follow-up   → Almiba Visit 2 Reinforcement Flow
Stage 3 — Objection Handling          → Almiba Objection Handling Flow
Stage 4 — Closing / Commitment        → Almiba Closing Flow
```

Stage 2'nin path'i (FU01A modeli): endikasyon özeti **tekrar** → önceki itiraza cevap → yeni klinik kanıt →
hasta profili/vaka → FAQ → sonraki aksiyon.

**Karar:** aynı içerik farklı stage/path'te **tekrar kullanılabilir**; doktor bir defada ikna olmayabilir.
Tekrar modelde **yasaklanmaz**, ancak **bilinçli ve görünür** olmalıdır (§11).

---

## 9. Learning / Course Journey Example

```text
EngagementJourney: German A1 Beginner Course Journey
Stage 1 — Greetings           → German A1 Greetings Path
Stage 2 — Self Introduction   → German A1 Self Introduction Path
Stage 3 — Numbers and Time    → German A1 Numbers Path
Stage 4 — Daily Conversation  → German A1 Daily Conversation Path
```

Brand/Product **boş kalır** ve bu bir eksiklik değildir — journey modeli pharma'ya bağımlı değildir.

---

## 10. Stage Progression Boundary

Bu FU **stage ilerletme engine'i yapmaz**. `AdvancementRule`, `FallbackStageId` ve `BranchCondition`
**optional/future metadata**dır ve **değerlendirilmez**.

Örnek kurallar (yalnız kayıt): `visit completed` · `all required steps acknowledged` ·
`doctor asked for clinical evidence` · `objection recorded` · `quiz passed` · `manager manually advanced` ·
`repeat stage until condition met`.

| Kural | Karar |
|---|---|
| Evaluator | **Yok** |
| `CurrentJourneyStage` runtime state | **Tutulmaz** — ne Contact'ta, ne Account'ta, ne journey aggregate'inde |
| Progress state sahibi | **MOD-0155 / Digital Detailing / Learning Execution / MOD-0309** (ayrı authorization) |
| Zorunlu kısıt | Journey, advancement rule olmadan da `StageOrder` sırasıyla **baştan sona yürünebilir** olmalı |
| Görünürlük | Bu alanlar tüketiciye **veri olarak** geçer; bu FU onları yorumlamaz |

---

## 11. Repeat / Revisit Policy

| Kural | Karar |
|---|---|
| Aynı `KnowledgePath` birden fazla stage'de | **Serbest** |
| Aynı `KnowledgeContent` farklı path/stage'lerde | **Serbest** — tekrar yasak değil |
| Raporlanabilirlik | Aynı içerik/path'in journey içinde kaç stage'de geçtiği **raporlanabilir** olmalı (read projection) |
| `Repeatable` | **Açıkça işaretlenir**; varsayılan `false` |
| `Repeatable=false` | Tekrar uygulanması **consumer** tarafından engellenebilir; **engine bu FU'da yok** |
| `MaxVisitNumber` | Varsa consumer **uymalıdır**; zorlayıcı runtime kontrolü yok |
| `MinVisitNumber` / `MaxVisitNumber` | **Yalnız boundary metadata**; scheduling MOD-0155'te |

---

## 12. Campaign / Frequency Integration Boundary

| Soru | Cevap veren |
|---|---|
| Ne sıklıkla gidilecek? | MOD-0165 / MOD-0167 |
| **Bu temas serisinde hangi aşamadayız, hangi path uygulanmalı?** | **Bu FU** |
| Kim, ne zaman, hangi rotayla, gerçekte ne oldu? | MOD-0155 |

Campaign ileride `EngagementJourney`'i bağlayabilir (Almiba Q1 → target → ayda 2 → journey). Ancak bu FU:
campaign engine yapmaz · frequency runtime yapmaz · due/overdue hesaplamaz · visit plan oluşturmaz ·
**journey target assignment yapmaz** (hangi doktora hangi journey atandığı bu FU'da **yok** → F6).

Çift yönlü gömme yasağı korundu: journey frequency policy'ye gömülmez, frequency kuralı journey'e gömülmez.

---

## 13. MOD-0155 Consumer Boundary

MOD-0155 tüketebilir: selected journey · current/recommended stage · `RecommendedKnowledgePathId` ·
stage objective · `Repeatable` bilgisi · visit objective'e bağlanacak path.

MOD-0155'te kalanlar (ayrı implementation): visit plan · route plan · daily/weekly schedule · visit execution ·
**current stage progress** · **stage advancement** · content usage evidence · doctor response / objection capture.

Bu FU "en uygun journey"i **seçmez** (öneri/skor motoru yok); sıralı **şablon** döndürür.

---

## 14. MOD-0309 Completion Boundary

Completion / score / attendance / certificate **MOD-0309** kapsamındadır. Bu FU: course completion kaydı ·
learner progress state · score · certificate · attendance · quiz/assessment engine **tutmaz/üretmez**.
`AdvancementRule` iki tarafın sözleşme alanıdır: bu FU **beyan eder**, execution tarafı **ölçer**.

---

## 15. KnowledgePath Dependency Boundary

Stage `RecommendedKnowledgePathId` ile bağlanır ve **step detaylarını kopyalamaz**. Path **published + effective**
olmalı (aksi 400); **archived path yeni published journey stage'e bağlanamaz** (mevcutlarda tarihsel referans
kalır). **`PathVersionPinPolicy`**: `pinned` (varsayılan) veya `latest-published` (açıkça seçilir); çözülen
`KnowledgePathId` + `Version` **görünür**; **sessiz sürüm kayması yasak**; published journey sürümünde stage→path
eşlemesi **deterministik**. Bu, FU01A §6.1'deki step→content pinning kuralının **journey seviyesindeki
karşılığıdır**.

---

## 16. Explicit Exclusions

Runtime implementation · visit planning · route planning · digital detailing · content usage tracking ·
**journey progress engine** · **stage advancement engine** · branch evaluator · recommendation engine ·
campaign engine · segmentation engine · frequency engine · due/overdue engine · last visit history ·
visit execution · learning completion · quiz/assessment engine · journey target assignment ·
Brand/Product master implementation · approval workflow · MOD-0023 entegrasyonu · file upload/render/preview ·
Account/Contact mutation · territory mutation · patient data · hard delete · Mongo hand-edit · RBAC seed/grant ·
registry write · MOD-0048 publish · `TenantId` payload'da.

---

## 17. Contract Flags

```json
{
  "supportsEngagementJourney": true,
  "supportsMultiVisitContentProgression": true,
  "supportsJourneyStages": true,
  "supportsRepeatableStages": true,
  "supportsFutureStageAdvancementMetadata": true
}
```

Pack seviyesinde **öneri**; canlı contract'a yazılmadı. **Eklenmedi:** `supportsVisitPlanning` ·
`supportsRoutePlanning` · `supportsDigitalDetailing` · `supportsJourneyRuntimeProgress` ·
`supportsRecommendationEngine` · `supportsWorkflowApproval`. MOD-0162-FU01 / FU01A flag setleri ve MOD-0151 canlı
contract'ı (`supportsWorkflowActivation=false` dahil) **değişmedi**.

---

## 18. Guard Checks

| Kontrol | Sonuç |
|---|---|
| Runtime code changed? | **No** |
| Backend/frontend changed? | **No** |
| Gateway changed? | **No** |
| MOD-0155 code changed? | **No** |
| MOD-0162 runtime changed? | **No** (yalnız FU01A pack dokümanında not + F9 satırı) |
| Digital detailing opened? | **No** |
| Visit planning opened? | **No** |
| Route planning opened? | **No** |
| **Journey progress engine opened?** | **No** |
| **Stage advancement engine opened?** | **No** |
| Branch evaluator opened? | **No** (yalnız future metadata) |
| Recommendation engine opened? | **No** |
| Campaign/frequency engine opened? | **No** |
| Journey target assignment opened? | **No** |
| Brand/Product implementation opened? | **No** |
| Workflow/approval opened? | **No** |
| Patient data opened? | **No** |
| Account/Contact mutation opened? | **No** |
| Territory mutation opened? | **No** |
| RBAC seed/grant changed? | **No** |
| Registry write? | **No** |
| MOD-0048 publish changed? | **No** |
| Multi-visit journey boundary added? | **Yes** |
| Repeat/revisit policy added? | **Yes** |
| MOD-0166 ad/kapsam sınırı yazıldı mı? | **Yes** (§4.1) |
| Follow-ups opened? | **Yes** (9 adet) |

Pack frontmatter doğrulaması: `status: draft` · `runtime_code_allowed: false` · `shell: none` ·
`golden_reference: none` · `form_field_count: 0`.

---

## 19. Created / Updated Files

| Dosya | İşlem |
|---|---|
| `execution/domains/commercial-suite/module-packs/MOD-0162-FU01B-engagement-journey-multi-visit-content-progression.md` | **Oluşturuldu** |
| `execution/domains/commercial-suite/module-packs/MOD-0162-FU01A-knowledge-path-content-sequence.md` | **Güncellendi** (yalnız doküman) — §1'e "path tek oturumdur / `visit-N` sızma yasağı" notu + §17'ye F9 (kapatıldı) |
| `docs/audits/mod-0162-fu01b-engagement-journey-multi-visit-content-progression-pack-authorization-2026-08-02.md` | **Oluşturuldu** (bu rapor) |

Runtime kod, config, gateway, RBAC, reference data ve registry **değiştirilmedi**.

---

## 20. Final Verdict

### **PASS**

- `EngagementJourney` / `EngagementJourneyStage` **ayrı model** olarak yetkilendirildi; `KnowledgePath` **tek
  visit/session içi sıra** olarak kaldı.
- Beş yanlış model reddedildi; özellikle `Contact`/`Account` üzerinde `CurrentJourneyStage` alanı **kalıcı olarak**
  yasaklandı (şablon ≠ runtime state).
- Stage modeli tam tanımlandı (order/unique/409, path referansı, `Repeatable`, `IsRequired`, min/max visit
  metadata, fallback/branch future alanları).
- Repeat/revisit policy tanımlandı: aynı content/path farklı stage'lerde **tekrar kullanılabilir**, tekrar
  **yasak değil ama raporlanabilir** olmalı.
- Pharma (Almiba Q1 4 aşama) ve learning (German A1 kurs) örnekleri **aynı modelle** desteklendi; Brand/Product
  boşken journey tam çalışıyor.
- Stage progression **future metadata** olarak sınırlandı; evaluator ve `CurrentJourneyStage` state açılmadı.
- `KnowledgePath` bağımlılığı `PathVersionPinPolicy` ile deterministik hâle getirildi; published journey sürümünde
  stage seti donduruldu.
- MOD-0155 consumer, MOD-0309 completion, Campaign/Frequency ve **MOD-0166 otomasyon** sınırları netleşti.
- Runtime / planning / detailing / progress / workflow **açılmadı**; mevcut scope'lar bozulmadı.

FAIL kriterlerinin hiçbiri tetiklenmedi. Kayda geçen tek governance açığı: **MOD-0166 ile "journey" adı
çakışması** — sınır §4.1'de kesin olarak yazıldı, kalıcı adlandırma kararı EA'ya (F1) bırakıldı; bu PASS'ı
düşürmez çünkü kapsam ayrımı bugün nettir.

---

## 21. Next Recommended Prompt

`Brand/Product Master Boundary Pack Authorization`
