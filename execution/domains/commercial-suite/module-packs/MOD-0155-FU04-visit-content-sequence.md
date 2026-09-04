---
id: MOD-0155-FU04
name: Visit Content Sequence
parent: MOD-0155
parent_name: Field Sales / Visit Planning
siblings: MOD-0155-FU01 (Planned Visit — ready-for-dev) · MOD-0155-FU02 (Visit Report) · MOD-0155-FU03 (Route Planning) · MOD-0155-FU05 (MicroTarget) · MOD-0155-FU06 (Cycle Capacity — SHIPPED, review) · MOD-0155-FU06B (Activity Time Budget — ready-for-dev) · MOD-0155-FU07 (Cycle Capacity Monthly Redesign — SHIPPED, review)
domain: commercial-suite
service: Diten.CrmService
frontend: frontend/Diten.Web
shell: tenant
golden_reference: n/a (in-process resolver + one thin read-only JSON preview endpoint — NOT a Golden DataTable CRUD surface; D-SURFACE=E)
entity_base: n/a (no persisted aggregate — a pure resolver over FU01 / MOD-0162 / MOD-0167 read seams; output transient, never persisted by FU04)
status: ready-for-dev
runtime_code_allowed: true
flip_approved_by: "user via Control Tower — 2026-08-29 (3 D-questions LOCKED: D-CONTENT-SPLIT=StrategyTemplate ProductLines join, D-END-OF-JOURNEY=flag, D-SURFACE=thin read-only preview endpoint crm.visit-content.preview; user ordered after FU03, now built+verified 34/0)"
owner: module-pack-author
branch: feature/crm-integration
started: 2026-08-29
target: 2026-08-29 (flipped for build)
predecessor: MOD-0155-FU01 (PlannedVisit + PlannedVisitContentRef — ready-for-dev) · MOD-0162-FU05 (ContentEngagementJourney reader — SHIPPED) · MOD-0167-FU04 (StrategyTemplate reader — SHIPPED) · MOD-0155-FU06B (ActivityTimeBudgetCalculator — ready-for-dev)
consumers: MOD-0155-FU05 (packing engine — çözülen içerik + süre girdisini in-process resolver ile paketler) · MOD-0155-FU03 (route optimizer — durationMinutes girdisi) · MOD-0155-FU01 (PlannedVisitContentRef default-fill) · operator/QA (thin read-only preview endpoint `crm.visit-content.preview`)
dependencies:
  - MOD-0155 (parent — Field Sales / Visit Planning; SoR = saha planlama)
  - MOD-0155-FU01 (ZORUNLU ÖNCÜL — PlannedVisit + gömülü PlannedVisitContentRef derive-default/override modelini ZATEN kurdu; bu FU o default'u ÜRETEN resolver'ı sağlar, FU01 depolamasını DUPLİKE ETMEZ)
  - MOD-0162-FU05 (SALT-OKUNUR — IContentEngagementJourneyReader; sıralı published stage listesi. SHIPPED, imza DEĞİŞMEZ)
  - MOD-0167-FU04 (SALT-OKUNUR — IStrategyTemplateReader; doctor→segment→play. SHIPPED, imza DEĞİŞMEZ)
  - MOD-0155-FU06B (SALT-OKUNUR — ActivityTimeBudgetCalculator.VisitDuration(capacity, promoCount, nonPromoCount); aritmetiği YAPAR, bu FU sayıları VERİR)
  - MOD-0164-FU02 / MOD-0165-FU03 (KAPSAM DIŞI — consent/frequency FU01+FU05'in işi, bu FU içerik+süre)
  - MOD-0167-FU04 StrategyTemplate ProductLines (KİLİTLİ promo/non-promo sınıflandırma kaynağı — D-CONTENT-SPLIT=C, salt-okunur join)
  - MOD-0162 / MOD-0290 (BOUNDARY — content→product/SKU bağı bu servislerde yaşar; FU04 kurmaz, okur)
  - MOD-0018 (RBAC — yeni anahtar kararı D-SURFACE'e bağlı; §14)
  - DEV-0001 (Golden Reference Compact — şablon)
---

# MOD-0155-FU04 — Visit Content Sequence

> **✅ READY FOR DEV — kod yetkisi verildi.**
> Flip 2026-08-29 (kullanıcı via Control Tower): `status: ready-for-dev` + `runtime_code_allowed: true`. Üç D-sorusu
> **KİLİTLİ** (§0.3): D-CONTENT-SPLIT=StrategyTemplate ProductLines, D-END-OF-JOURNEY=flag, D-SURFACE=ince
> read-only preview endpoint. FU03 build+doğrulandı (34/0); `@orchestrator` bu pack ile kod yazabilir.
>
> FU01 planı kurdu ("kim, ne zaman, kim tarafından") ve içerik konumunu **saklayacak** gömülü
> `PlannedVisitContentRef`'i (strateji-default + manuel override, `StageIndex` "future FU04 next-stage için okunur —
> FU01 asla ilerletmez") **şekil olarak** açtı. FU06B süre **aritmetiğini** (`VisitDuration(capacity, promoCount,
> nonPromoCount)`) verecek. Bu FU o iki boşluğu bağlayan **çözümleyicidir (resolver)**:
> *"bir doktora yapılacak planlı ziyaret için **sıradaki içerik hangisi** ve bunun sonucu **ziyaret kaç dakika
> sürer**?"*
>
> **Üç D-sorusu Control-Tower + kullanıcı tarafından KİLİTLENDİ (2026-08-29):** promo/non-promo sınıflandırması
> **StrategyTemplate ProductLines**'tan gelir (D-CONTENT-SPLIT=C), journey sonu **flag**'lenir (D-END-OF-JOURNEY),
> ve FU04 **ince read-only bir preview endpoint** açar ama **saf in-process resolver** olarak da kalır
> (D-SURFACE=E). Kararlar §0.3'te; kapsam buna göre kesinleşti. **Pack yine `status: draft`'tır ve `runtime_code_
> allowed` YOKTUR** — flip, kullanıcı bu güncellemeyi gözden geçirdikten sonra Control-Tower'ın ayrı adımıdır.
>
> **Bu FU bir MOTOR DEĞİLDİR ve içerik/segment/strateji master'ına SAHİP DEĞİLDİR.** Journey **ilerletir** (bir
> sonraki aşamayı deterministik seçer) ama içerik **üretmez**; süre **sayılarını çıkarır** ama aritmetiği FU06B
> yapar; sonucu FU01'in `PlannedVisitContentRef`'ine + FU03'ün `durationMinutes` girdisine **besler** ama planı
> **paketlemez** (FU05). Tüm içerik-kaynağı zinciri **SALT-OKUNUR seam'lerdir** — MOD-0155 hiçbir content/segment/
> strategy master'ına sahip değildir.
>
> Otorite sırası: **Blueprint Excel** > bu pack > [Domain Config](../domain-config.md) >
> [crm-sor-boundary.md](../crm-sor-boundary.md) > `AGENTS.md` > `.antigravity/rules/`.

---

## 0. Kimlik Geçidi, Ev Kararı ve Açık Sorular

### 0.1 DCP-002 — PASS (2026-08-29)

```text
$ py .antigravity/scripts/verify_module_id.py . --check-id MOD-0155-FU04 --name "Visit Content Sequence" --parent MOD-0155
OK  MOD-0155-FU04: proven against Blueprint/registry.
REAL_EXIT=0
```

Geçit **ilk denemede exit 0** verdi; **id/name ayarlaması gerekmedi**. `MOD-0155-FU04` parent `MOD-0155 | Field
Sales / Visit Planning`'e karşı kanonik doğrulandı. FU numarası kardeş listesinde zaten **"Visit Content Sequence
Execution"** olarak rezerveydi (FU01/FU06B frontmatter'ları buna atıf yapar); bu pack o rezervasyonu **"Visit
Content Sequence"** repo-tarafı adıyla açar. Parent'ın kanonik adı **"Field Sales / Visit Planning"**'dir ve
değişmez. **Registry satırı bu pack tarafından EKLENMEZ** (FU01/FU06B emsali) → §20/F-REGISTRY.

### 0.2 D-HOME — Ev **MOD-0155**'tir

İçerik **sırası** ("bu doktora sıradaki konu hangisi") ve o içeriğin **ziyaret süresi**, bir **saha planlama**
ölçüsüdür — `crm-sor-boundary.md`'nin *"Visit content sequence execution → **MOD-0155** (tanım MOD-0162-FU01A/FU04)"*
satırının tam içinde. Çözümleyici, FU01'in `PlannedVisit` aggregate'i ve FU06B'nin `CycleCapacity` süre alanları
üstünde çalışır — ikisi de MOD-0155'e ait, `Diten.CrmService`'te yaşar. İçeriğin **kendisi** (journey, knowledge
path, strateji, segment) MOD-0162/0167'ye aittir ve bu FU onları **yalnız okur**.

### 0.3 ✅ KİLİTLİ D-KARARLARI (Control-Tower + kullanıcı, 2026-08-29)

Üç açık soru da **çözüldü ve LOCKED'dır**. Draft yine `status: draft` kalır — flip Control-Tower'ın ayrı adımıdır —
ama bu üç karar artık **tasarımın parçasıdır**, yeniden tartışılmaz:

| # | Karar | Sonuç (özet) |
|---|---|---|
| **D-CONTENT-SPLIT = StrategyTemplate (option C)** | Promo/non-promo sınıflandırması **MOD-0167-FU04 StrategyTemplate'in ProductLines / SKU allocation**'ından gelir | Çözülen içerik seti (journey aşamalarından) doktorun segment'inin **StrategyTemplate promoted product line'larıyla** join edilir (`IStrategyTemplateReader`, salt-okunur): promoted ürün/SKU'ya bağlı item **promo**, kalan **non-promo**. Fail-closed default korunur: strateji/binding çözülmezse `content_split_unresolved` → sayılar 0 → süre yalnız `ReportDuration`. **FU06B'nin F-CONTENT-PROMO-SPLIT'i kapanır.** (§4.5, §19.1) |
| **D-END-OF-JOURNEY = flag** | Son aşamadan sonra resolver `end-of-journey` **statüsü döndürür**; sessiz loop / wrap-around **YOK** | FU05/planlayıcı sonrasını kararlaştırır; FU04 yalnız işaretler. (§4.4) |
| **D-SURFACE = thin preview endpoint (Variant E)** | FU04 **read-only** bir resolver preview endpoint açar **VE** saf in-process resolver olarak da kalır | `POST /api/crm/visit-content/preview` (read-only, **hiçbir şey persist etmez**) + **YENİ** permission `crm.visit-content.preview` + **YENİ** Ocelot route çifti (integration-agent build'de tel çeker; pack **beyan eder**). Endpoint ek bir ince yüzeydir, tek yol değildir — FU05 aynı resolver'ı **in-process DI** ile çağırır. (§14, §15) |

### 0.4 Kapsam — kilitli kararlara göre kesinleşti

Kapsam artık **tek forma sabitlendi** (önceki R/E varyant ikilemi D-SURFACE=E ile kapandı): saf
`VisitContentSequenceResolver` **+** onu saran ince read-only preview endpoint. Resolver hem endpoint'ten hem de
FU05 motorundan (in-process DI) tüketilir. İçerik depolama hâlâ FU01'in `PlannedVisitContentRef`'idir; süre asla
persist edilmez. Yeni bir kullanıcı-form yüzeyi **açılmaz** (endpoint operatör/QA preview içindir, DataTable
sayfası değil) → `golden_reference: compact` verifier'ı **N/A** kalır.

---

## 1. Module Summary

### 1.1 Ne yapar

FU04, bir doktora yapılacak **planlı ziyaret** için **iki türetilmiş değeri çözer** ve bunu **yalnız SALT-OKUNUR
seam'ler** üzerinden yapar:

1. **Sıradaki içerik** — doktor → (segment →) StrategyTemplate → ContentEngagementJourney/KnowledgePath zinciriyle
   **hangi journey** ve o journey'in **sıralı aşamalarında bir sonraki hangi aşama** (next stage) gösterilecek.
2. **Ziyaret süresi** — çözülen içerik setinin **promo/non-promo item sayıları** → FU06B
   `ActivityTimeBudgetCalculator.VisitDuration(capacity, promoCount, nonPromoCount)` → **dakika**. FU04 **sayıları
   verir**, FU06B **aritmetiği yapar**.

Sonuç iki tüketiciye **beslenir**: FU01'in gömülü `PlannedVisitContentRef`'i (form yüklendiğinde **default-fill**;
rep override edebilir) ve FU03 route optimizer'ının `durationMinutes` girdisi + FU05 paketleme motoru.

### 1.2 Gerçek iş akışı (kullanıcı-onaylı, locked)

> Bir rep bir doktora düzenli ziyaret eder ve **her ziyarette bir sonraki konuyu** sunar. Bir sonraki ziyaretin
> içeriği **otomatik olarak journey'deki bir sonraki aşamaya ilerler**. Ve **ziyaret süresi = f(içerik)** — sunulan
> promo/non-promo item sayısı süreyi belirler.

FU04 bu akışın **çözümleyicisidir**: "bu doktor için şu an hangi aşamadayız, bir sonraki aşama hangisi, ve o
aşamanın içeriği ziyareti kaç dakika yapar" sorusunu cevaplar. **İçeriği sunmaz, ziyareti gerçekleştirmez, planı
paketlemez.**

### 1.3 Çözüm zinciri (normatif)

```text
doctor (PlannedVisit hedefi: contact / account-contact-link)
   │
   │  (opsiyonel) segment üyeliği  →  ISegmentMembershipReader  (MOD-0167, salt-okunur)
   ▼
StrategyTemplate  ("play")        →  IStrategyTemplateReader.ListBySegmentAsync / GetActiveBindingsAsync
   │                                  (MOD-0167-FU04, salt-okunur — hangi journey + product mix + content bindings)
   ▼
ContentEngagementJourney / KnowledgePath
   │                                →  IContentEngagementJourneyReader.GetOrderedStagesAsync
   │                                  (MOD-0162-FU05, salt-okunur — sıralı, published, ACTIVE aşamalar)
   ▼
NEXT STAGE seçimi  (deterministik) →  FU01'in stored PlannedVisitContentRef.StageIndex'inden bir SONRAKİ ordinal
   │                                  (doktorun son ziyaretinin aşaması + 1; end-of-journey = D-END-OF-JOURNEY)
   ▼
content set (bu aşamanın promo + non-promo item'leri)
   │                                → promo/non-promo SPLIT = StrategyTemplate ProductLines join (D-CONTENT-SPLIT=C)
   ▼
promoCount, nonPromoCount          →  FU06B ActivityTimeBudgetCalculator.VisitDuration(capacity, promoCount, nonPromoCount)
   ▼
visitDurationMinutes  +  resolved (journeyId, stageId, stageIndex, contentSource=strategy)
   │
   ├──►  FU01 PlannedVisitContentRef  (default-fill; rep override edebilir → contentSource=manual)
   └──►  FU03 durationMinutes  /  FU05 packing engine
```

### 1.4 Hedef kullanıcı

Doğrudan bir insan **DataTable/form yüzeyi değildir**; yeni yüzey yalnız operatör/QA preview endpoint'idir.
Tüketicileri **makinelerdir**: FU01 form default-fill
handler'ı ve FU05 paketleme motoru. Dolaylı fayda saha temsilcisine gider (formu açtığında "sıradaki konu" ve
tahmini süre önceden dolu gelir; isterse override eder).

### 1.5 Bu FU bir MOTOR DEĞİLDİR (D8 — FU01/FU06B ile aynı ilke)

Plan **üretmez**, ziyaret **paketlemez**, rota **çıkarmaz**, süreyi **persist etmez** (FU06B ilkesi: duration asla
saklanmaz), içeriği **sunmaz/execute etmez**, journey/segment/strateji **mutate etmez**. Deterministik bir
**okuma + türetme**dir: seam'lerden okur, bir sonraki aşamayı seçer, sayıları FU06B'ye geçirir, sonucu döner.
"Auto-advance" **deterministiktir** (skorlama/öneri/branch değerlendirmesi YOK) — pinlenmiş published journey'in
sıralı yolunda bir ordinal ilerlemedir.

---

## 2. Ownership and Boundaries

### 2.1 In-scope / Out-of-scope

| Kapsam | Karar |
|---|---|
| **In-scope** | Saf `VisitContentSequenceResolver` (doctor→play→journey→next-stage→content-set→**promo/nonPromo split (StrategyTemplate ProductLines join)**→FU06B süre) + çözüm DTO'ları · üç mevcut seam'in in-process tüketimi (`IStrategyTemplateReader`, `IContentEngagementJourneyReader`, `ISegmentMembershipReader`) · FU06B `ActivityTimeBudgetCalculator` tüketimi · FU01 `PlannedVisitContentRef` default-fill besleme sözleşmesi · **ince read-only preview endpoint** `POST /api/crm/visit-content/preview` + **yeni** permission `crm.visit-content.preview` + **yeni** Ocelot route beyanı + proxy · testler |
| **Out-of-scope (AÇIKÇA ERTELENİR)** | Süre **aritmetiği** (FU06B'ye ait) · süre/aşamanın **persist edilmesi** (FU01'in `PlannedVisitContentRef` yazması; FU04 hesaplar, saklamaz) · **paketleme** + slot/sıra atama (**FU05**) · **rota/yol süresi** (**FU03**) · consent/frequency (FU01/FU05) · journey/knowledge-path/strateji/segment master **CRUD**'u (MOD-0162/0167) · içerik **sunumu/execution**/detailing/survey · yeni journey **stage** modeli · content→product/SKU bağının **kurulması** (MOD-0162/0290/0167 sahipliğinde; FU04 yalnız mevcut bağı **okur**, §19.1) |

### 2.2 SoR sınırı — sahiplenilen vs. yalnız tüketilen

| Nesne | Sahip | Bu FU'da |
|---|---|---|
| `VisitContentSequenceResolver` (+ çözüm DTO'ları) | **MOD-0155** | **AÇILIR** — bu FU'nun tek yeni yapısı (saf resolver) |
| `PlannedVisit` / `PlannedVisitContentRef` / `StageIndex` | MOD-0155-FU01 | **OKUNUR + default-fill BESLENİR** — FU04 depolamayı DUPLİKE ETMEZ; FU01 handler'ı resolver çıktısını `PlannedVisitContentRef`'e yazar |
| `CycleCapacity` süre alanları + `ActivityTimeBudgetCalculator` | MOD-0155-FU06B | **SALT-OKUNUR** — FU04 `VisitDuration(...)`'ı çağırır, alanlara/aritmetiğe dokunmaz |
| `StrategyTemplate` + bindings (play, product mix, content) | MOD-0167-FU04 | **SALT-OKUNUR** — `IStrategyTemplateReader` seam'i (reader ZATEN MOD-0155 tüketicisini öngörüyor); mutate/validate YOK |
| `ContentEngagementJourney` / `KnowledgePath` / stages | MOD-0162-FU05 | **SALT-OKUNUR** — `IContentEngagementJourneyReader.GetOrderedStagesAsync`; ilerletme FU04'ün OKUMA türevi, journey mutate edilmez |
| `Segment` / membership | MOD-0167 | **SALT-OKUNUR (opsiyonel)** — `ISegmentMembershipReader`; segment evaluate/mutate YOK |
| Promo/non-promo **sınıflandırması** | **KİLİTLİ = StrategyTemplate** | Kaynak **MOD-0167-FU04 StrategyTemplate ProductLines/SKU** (D-CONTENT-SPLIT=C); FU04 çözülen içerik setini promoted product line'larla **join eder** (salt-okunur, `IStrategyTemplateReader`). StrategyTemplate mutate/validate edilmez (§4.5) |
| Packing / slot / route / duration persist | MOD-0155-FU03/FU05 + FU01 | **AÇILMAZ** — FU04 motor değil |

### 2.3 Komşu ölçülerle tek cümlelik sınır

> **FU01** planı **saklar** (kim/ne zaman + içerik konumunun override yüzeyi) · **FU04 (bu FU)** o içerik
> konumunun **default'unu çözer** (sıradaki aşama) ve **süresini türetir** (FU06B aritmetiğiyle) · **FU06B**
> süre **aritmetiğini** yapar · **FU05** çözülen içerik + süreyi çalışma gününe **paketler** · **FU03** ziyaretler
> arası **yol süresini** hesaplar. Beşi ayrı sorumluluktur; FU04 yalnız **"sıradaki içerik + süresi"** çözümüdür.

### 2.4 FU01/FU06B/MOD-0162/MOD-0167'den DEĞİŞMEYENLER (kilitli — additive garantisi)

| Kural | Durum |
|---|---|
| `PlannedVisitContentRef` şekli + `StageIndex` "FU01 asla ilerletmez" | **KORUNUR** — FU04 ilerletmeyi yapar ama `PlannedVisitContentRef`'i FU01 handler'ı yazar; FU01 depolaması değişmez |
| `IContentEngagementJourneyReader` / `IStrategyTemplateReader` / `ISegmentMembershipReader` imzaları | **DEĞİŞMEZ** — yalnız çağrılır |
| FU06B `ActivityTimeBudgetCalculator` (saf, duration asla persist edilmez) | **DEĞİŞMEZ** — yalnız çağrılır; FU04 de sonucu saklamaz |
| MOD-0162 journey/stage, MOD-0167 strateji/segment master davranışı | **DEĞİŞMEZ** — salt-okunur |
| `services/Diten.Platform/**` protected | **KORUNUR** — bu FU dokunmaz |

---

## 3. Owned Objects

| Katman | Nesne |
|---|---|
| **Rules (saf)** | `VisitContentSequenceResolver` (`Features/VisitContentSequence/VisitContentSequenceResolver.cs`) — I/O sınırlı (yalnız seam çağrıları, mutasyon yok); doctor→play→journey→next-stage→content-set→promo/nonPromo→FU06B süre çözer |
| **DTOs** | `VisitContentSequenceResult` (resolved journeyId/stageId/stageIndex/stageCode/contentSource + promoCount/nonPromoCount + `VisitDurationMinutes` + reasonCodes/status) · `VisitContentSequenceRequest` (plannedVisitId veya doctor+context girdisi) · `VisitContentSequenceReasonCodes` |
| **Seam tüketimi** | `IStrategyTemplateReader` · `IContentEngagementJourneyReader` · `ISegmentMembershipReader` · FU06B `ActivityTimeBudgetCalculator` — **hepsi mevcut, in-process DI, salt-okunur** |
| **Vokabüler (in-domain)** | `VisitContentSequenceStatus` (`resolved` · `no-strategy` · `no-journey` · `end-of-journey` · `not-applicable`) · `VisitContentSourceMarker` (FU01 `PlannedVisitContentSource` = `strategy`\|`manual` ile hizalı — yeniden BEYAN edilmez, okunur) |
| **API (KİLİTLİ — D-SURFACE=E)** | `POST /api/crm/visit-content/preview` (read-only, **hiçbir şey persist etmez**) + same-origin proxy. FU05 aynı resolver'ı in-process DI ile de çağırır (endpoint tek yol değil) |
| **Permission (YENİ)** | `crm.visit-content.preview` (read-only preview yetkisi). **Seed/grant bu pack'te YOK** → F-RBAC |
| **Ocelot (YENİ — beyan, integration-agent tel çeker)** | `/api/crm/visit-content/{everything}` → CrmService upstream (POST/OPTIONS). Pack **beyan eder**, `ocelot.json`'a yazmaz |
| **AÇIKÇA sahiplenilmeyen** | Journey/stage master · strateji/segment master · süre aritmetiği (FU06B) · içerik konumu depolaması (FU01) · content→product/SKU bağının kurulması (MOD-0162/0290/0167) · packing/route/slot |

---

## 4. Çözüm Modeli (Entity yerine — bu FU aggregate AÇMAZ)

> **Bu FU yeni bir aggregate/koleksiyon açmaz.** Çıktısı **türetilmiş, geçicidir** (transient) ve **asla persist
> edilmez** (FU06B duration-never-persisted ilkesiyle aynı). Depolama FU01'in `PlannedVisitContentRef`'idir; FU04
> onu **hesaplar ve döndürür**, yazmayı FU01 handler'ı yapar. Aşağıdaki alanlar bir **DTO**'nun (VO) alanlarıdır,
> bir Mongo dokümanının değil.

### 4.1 `VisitContentSequenceResult` (türetilmiş, saklanmaz)

| # | Alan | Tip | Kaynak / Not |
|---|---|---|---|
| 1 | `Status` | string | `VisitContentSequenceStatus` (§3) — çözüm sonucu; `no-strategy`/`no-journey`/`end-of-journey` sessiz-fail değil, **kodlanmış** durumdur |
| 2 | `JourneyId` | Guid? | Çözülen journey (MOD-0162, published+effective); `IContentEngagementJourneyReader` üzerinden |
| 3 | `StageId` | Guid? | **Sıradaki** aşama (next-stage seçimi) |
| 4 | `StageIndex` | int? | Aşamanın sıralı yoldaki ordinal konumu — FU01 `PlannedVisitContentRef.StageIndex` ile aynı semantik |
| 5 | `StageCode` / `StageDisplayName` | string? | Gösterim snapshot'ı (kopya değil, salt gösterim) |
| 6 | `ContentSource` | string | `strategy` (default-fill) — FU04 default üretir; rep override ederse FU01 `manual` yazar |
| 7 | `StrategyTemplateId` | Guid? | Default'u üreten play (MOD-0167); menşe snapshot'ı, doğrulanmaz |
| 8 | `PromoItemCount` | int | Bu aşamanın **promo** item sayısı — içerik seti **StrategyTemplate ProductLines/SKU** ile join edilerek (D-CONTENT-SPLIT=C, §4.5); ≥0 |
| 9 | `NonPromoItemCount` | int | Bu aşamanın **non-promo** item sayısı (promoted product line'a bağlı OLMAYAN item'ler); ≥0 |
| 10 | `VisitDurationMinutes` | int | **FU06B `ActivityTimeBudgetCalculator.VisitDuration(capacity, PromoItemCount, NonPromoItemCount)`** çıktısı — FU04 hesaplamaz, çağırır |
| 11 | `ReasonCodes` | string[] | `strategy_not_found` / `journey_not_published` / `journey_completed` / `capacity_not_found` / `content_split_unresolved` (strateji/binding çözülmediğinde fail-closed) |
| 12 | `ResolvedAt` | DateTimeOffset | Çözümleme anı |

### 4.2 Next-stage seçimi (deterministik — normatif)

```text
priorStageIndex = FU01 PlannedVisit'in doktora ait EN SON (planned/confirmed) ziyaretinin
                  PlannedVisitContentRef.StageIndex'i   (yoksa "başlangıç": journey ilk aşaması, index 0)

orderedStages   = IContentEngagementJourneyReader.GetOrderedStagesAsync(journeyId, now)
                  (published + effective + ACTIVE, StageOrder→StageCode deterministik sırada)

nextIndex       = priorStageIndex + 1
```

- **Auto-advance = deterministik ordinal ilerleme.** Skorlama, "en iyi journey" seçimi, öneri, branch
  değerlendirmesi **YOKTUR** — reader zaten bunları yapmaz, FU04 de yapmaz.
- `nextIndex` journey'in **son aşamasını aşarsa** → **D-END-OF-JOURNEY** (§4.4) devreye girer.
- İlk ziyarette (`priorStageIndex` yok) → journey'in **ilk aşaması** (index 0) seçilir.
- FU04 `StageIndex`'i **hesaplar**; **yazmaz** — FU01 `PlannedVisitContentRef`'e yazar (2.2 sınırı).

### 4.3 Süre türetme (FU06B'ye devir — normatif)

```text
capacity        = doktorun bağlı olduğu cycle-period'un CycleCapacity'si (FU06B okuma; yoksa Status=capacity_not_found)
promoCount      = PromoItemCount      (D-CONTENT-SPLIT'ten)
nonPromoCount   = NonPromoItemCount   (D-CONTENT-SPLIT'ten)
VisitDurationMinutes = ActivityTimeBudgetCalculator.VisitDuration(capacity, promoCount, nonPromoCount)
```

- FU04 **aritmetik yapmaz** — sayıları verir, FU06B `(promo×PromoProductTime)+(nonPromo×NonPromoProductTime)+
  ReportDuration` hesabını yapar. Yol süresi ve between-visit **dahil değildir** (FU06B kuralı).
- Sonuç **persist edilmez**; FU01 `PlannedVisit.PlannedDurationMinutes`'e (manuel override alanı) default-fill
  olarak **önerilir**, yazmayı FU01 yapar.

### 4.4 D-END-OF-JOURNEY (KİLİTLİ = **flag**)

Journey'in **son aşamasından** sonra resolver **sessizce loop YAPMAZ, wrap-around YAPMAZ, aynı aşamayı
TEKRARLAMAZ**. Bunun yerine:

```text
nextIndex > lastStageIndex  ⇒  Status = end-of-journey,  StageId = null,  ReasonCodes += journey_completed
```

Otomatik ilerleme durur; **sonrasını FU05 / planlayıcı kararlaştırır** (yeni journey / yeni strateji / kapat).
FU04 yalnız **işaretler** — D8 (no-engine, no silent behaviour) ile tam uyumlu. Reddedilen alternatifler: loop
(başa dön) ve stop (aynı aşamada kal) — ikisi de içeriği sessizce tekrar ettirir, `end-of-journey` sinyalini
gizlerdi.

### 4.5 D-CONTENT-SPLIT (KİLİTLİ = **StrategyTemplate ProductLines**, option C)

Alan 8/9 (`PromoItemCount`/`NonPromoItemCount`) bir içerik item'inin **promo mu non-promo mu** olduğuna dayanır.
Kaynak **kilitlendi: MOD-0167-FU04 StrategyTemplate.** Kampanya "play"i neyin promote edildiğini söyler; bir
içerik item'i, **StrategyTemplate'in ProductLines / SKU allocation'ında görünen bir ürün/SKU'ya bağlıysa promo**
sayılır, sunulan setteki geri kalan her şey **non-promo**'dur.

```text
resolvedContent  = journey aşamasının içerik item'leri (IContentEngagementJourneyReader)
promotedProducts = doktorun segment'inin aktif StrategyTemplate'inin ProductLines'ı
                   (IStrategyTemplateReader.GetActiveBindingsAsync / ListBySegmentAsync — SALT-OKUNUR)
                   → { GlobalProductId } ∪ { SkuAllocations.GskuId }

PromoItemCount    = resolvedContent'te promotedProducts'a bağlı item sayısı
NonPromoItemCount = resolvedContent geri kalanı
```

- Join **salt-okunur**dur; StrategyTemplate mutate/validate **edilmez** (sahte-FK açılmaz, MOD-0167 SoR'una
  dokunulmaz).
- **Fail-closed default KORUNUR:** doktor için aktif StrategyTemplate/binding **çözülmezse** →
  `content_split_unresolved`, sayılar **0**, süre yalnız `ReportDuration`. Yanlış promo süresi **asla üretilmez**.
- Bu karar **FU06B'nin F-CONTENT-PROMO-SPLIT flag'ini KAPATIR.**

> **Bağımlılık notu (§19.1):** Bir içerik item'inin hangi ürüne/SKU'ya bağlı olduğu bilgisi (content→product
> bağı) MOD-0162/0290/0167 sahipliğindedir. FU04 bu bağı **kurmaz**, mevcut olanı **okur**. Bağın hangi alanda
> yaşadığı build sırasında doğrulanır; yoksa fail-closed default devreye girer (yanlış figür yerine ReportDuration).

---

## 5. Repo Scope

```text
── Backend: services/Diten.CrmService/ ──
src/Diten.CrmService.Application/Features/VisitContentSequence/
├── VisitContentSequenceResolver.cs          (YENİ — saf resolver; üç seam + FU06B calculator okur)
├── VisitContentSequenceModels.cs            (YENİ — Result/Request DTO'ları + reasonCodes + vokabüler)
├── VisitContentSequencePermissions.cs       (YENİ — crm.visit-content.preview sabiti)
├── Queries/PreviewVisitContentQuery.cs      (YENİ — read-only preview query)
└── Handlers/QueryHandlers/PreviewVisitContentHandler.cs   (YENİ — resolver'ı sarar, hiçbir şey persist etmez)
src/Diten.CrmService.Application/DependencyInjection.cs        (DEĞİŞİR — resolver DI kaydı; seam'ler ZATEN kayıtlı)
src/Diten.CrmService.Api/Controllers/CRM/VisitContentController.cs    (YENİ — 1 read-only POST preview action, [HasPermission("crm.visit-content.preview")])
src/Diten.CrmService.Api/Models/CRM/VisitContentRequests.cs          (YENİ — preview request modeli)
tests/Diten.CrmService.Application.Tests/VisitContentSequence/VisitContentSequenceTests.cs   (YENİ)

── Frontend: frontend/Diten.Web/ ──
Controllers/CrmVisitContentController.cs      (YENİ — same-origin proxy; yalnız preview route, yeni View YOK)

── Gateway (BEYAN — integration-agent tel çeker, pack yazmaz) ──
gateway/**/ocelot.json                        (BEYAN: /api/crm/visit-content/{everything} route çifti — §15)

── Bu pack (bugün geçerli tek yazma alanı) ──
execution/domains/commercial-suite/module-packs/MOD-0155-FU04-visit-content-sequence.md
```

---

## 6. Protected Paths

| Path | Neden |
|---|---|
| `.antigravity/**` | Global engineering system |
| `services/Diten.CrmService/**/Features/Knowledge/ContentEngagementJourney/**` | MOD-0162-FU05 reader **imzası** — okunur, değişmez |
| `services/Diten.CrmService/**/Features/StrategyTemplate/**` | MOD-0167-FU04 reader **imzası** — okunur, değişmez |
| `services/Diten.CrmService/**/Features/Segmentation/**` | MOD-0167 membership reader — okunur, evaluate/mutate YOK |
| `services/Diten.CrmService/**/Features/CycleCapacity/**/ActivityTimeBudgetCalculator.cs` | FU06B saf hesap — okunur, değişmez |
| `services/Diten.CrmService/**/Domain/Entities/PlannedVisit.cs` (`PlannedVisitContentRef`, `StageIndex`) | FU01 depolaması — FU04 yazmayı FU01 handler'ına devreder; şekil değişmez |
| `services/Diten.CrmService/**/Features/{PlannedVisit,Campaign,VisitFrequencyPolicy,Territory,Account,Contact}/**` | Komşu/tüketilen FU'lar; FU04 dokunmaz (kendi `Features/VisitContentSequence/` klasöründe yaşar) |
| `services/Diten.Platform/**` | Başka domain servisi |
| `gateway/**/ocelot.json` | Yeni route çifti GEREKLİ (§15) → **integration-agent owned**, ayrı task; pack yalnız BEYAN eder, yazmaz |
| `frontend/Diten.Web/Views/Shared/_Layout*.cshtml`, `Archive/**` | FROZEN |
| RBAC katalog/seed + `rolePermissions` | **F-RBAC** — pack seed yazmaz |
| `execution/registries/**` | **F-REGISTRY** — registry yazımı pack yetkisi dışı |
| Mongo hand-edit | Yasak (GUID subtype tuzağı login'leri kırar) |

---

## 7. Dependencies

| Bağımlılık | Yön | Durum | Not |
|---|---|---|---|
| **MOD-0155-FU01** `PlannedVisit` + `PlannedVisitContentRef` | okunur + default-fill beslenir | ready-for-dev | FU04 next-stage'i FU01'in stored `StageIndex`'inden türetir; yazmayı FU01 yapar |
| **MOD-0162-FU05** `IContentEngagementJourneyReader` | salt-okunur, in-process | **SHIPPED** | `GetOrderedStagesAsync` — sıralı published ACTIVE aşamalar; imza değişmez |
| **MOD-0167-FU04** `IStrategyTemplateReader` | salt-okunur, in-process | **SHIPPED** | `ListBySegmentAsync`/`GetActiveBindingsAsync` — reader zaten MOD-0155/MicroTarget tüketicisini öngörüyor; ProductLines/ContentBindings okunur |
| **MOD-0167** `ISegmentMembershipReader` | salt-okunur, opsiyonel | SHIPPED | doctor→segment; evaluate/mutate YOK |
| **MOD-0155-FU06B** `ActivityTimeBudgetCalculator` | salt-okunur çağrı | ready-for-dev | `VisitDuration(capacity, promoCount, nonPromoCount)`; FU04 sayıları verir |
| **Promo/non-promo sınıflandırması** | **KİLİTLİ — StrategyTemplate** | çözüldü | D-CONTENT-SPLIT=C; `IStrategyTemplateReader.ProductLines` join (§4.5). **F-CONTENT-PROMO-SPLIT kapandı** |
| **MOD-0018** RBAC | tüketim | **yeni anahtar** | **`crm.visit-content.preview`** (YENİ) — seed/grant bu pack'te YOK (F-RBAC) |
| **DEV-0001** Golden Compact | şablon | N/A | Yeni kullanıcı-form yüzeyi yok (preview = operatör/QA endpoint), verifier N/A |

---

## 8. Runtime Constraints

- **Saf/türev.** `VisitContentSequenceResolver` yalnız seam okur + FU06B çağırır; **hiçbir şey persist etmez**,
  mutate etmez, `DateTime.UtcNow` dışında yan etkisi yoktur. Duration ve stageIndex sonucu **saklanmaz**.
- **Fail-closed, sessiz-fail değil.** Strateji yok / journey published değil / capacity yok / content-split
  çözülmemiş → **kodlanmış `Status` + reasonCode**; asla uydurulmuş default üretmez (reader'ların
  "no default is invented" ilkesiyle aynı). Bu, `content_split_unresolved` durumunda sayıları **0** tutar.
- **Tenant.** Tüm seam'ler `ITenantContext` üzerinden tenant-scoped okur; cross-tenant sızıntı yok. FU04 kendi
  tenant filtresi kurmaz — seam'lerinkine güvenir.
- **DateTimeOffset tuzağı N/A.** FU04 kendi koleksiyonunu açmadığı için parallel-arrays/index tuzağı yoktur.
- **Motor yok (D8).** İçerik ilerletme deterministik okuma türevidir; skorlama/branch/öneri yoktur.

---

## 9. Layout & Shell Contract

Yeni **kullanıcı-form/DataTable yüzeyi YOK** → §9 büyük ölçüde **N/A**. Kilitli D-SURFACE=E yüzeyi bir **operatör/QA
preview endpoint**'idir (`POST /api/crm/visit-content/preview`), bir Razor sayfası değil; FU04 hiçbir
`Views/CRM/**` view'i veya DataTable JS'i eklemez. FU01'in Compact formu (`shell: tenant`, `_LayoutTenantShell`)
default-fill verisini bu endpoint'ten (veya FU01 handler'ının in-process çağrısından) okur; FU01'in layout/section
haritası değişmez.

`golden_reference: compact` frontmatter'da **şablon uyumu** için taşınır; FU04 kendi golden-reference yüzeyi
açmadığından verifier **N/A**'dır (FU06B'nin "duration bir API değil, in-process çağrılır" emsali).

---

## 10. Backend File Convention

FU01/FU06B klasör yapısı (Golden Compact) korunur. FU04 kendi **`Features/VisitContentSequence/`** klasöründe
yaşar: `VisitContentSequenceResolver.cs` **saf sınıf** (`ActivityTimeBudgetCalculator`/`PlannedVisitJourneyProbe`
emsali — sealed, Command/Query suffix yok) + 1 read-only Query (`PreviewVisitContentQuery`) + Handler
(`PreviewVisitContentHandler`, ayrı `Queries`/`Handlers/QueryHandlers` klasörlerinde) + `VisitContentController`
(1 read-only POST action). Handler ve controller **hiçbir şey persist etmez**. Naming değişmez.

---

## 11. Frontend File Contract

**Tek yeni frontend dosyası = same-origin proxy** (`Controllers/CrmVisitContentController.cs`) — preview
endpoint'ini `/CRM/VisitContent/api/...` altından geçirir. **Yeni View / DataTable JS / RESX YOK.** FU01 form.js'i
journey→stage seçicisini strateji-default'la doldururken FU04 çözümünü (proxy preview'den veya FU01 handler'ının
in-process çağrısından) okur; FU01'in mevcut "default-fill rozet: strategy / override rozet: manual" davranışı
FU04'ün çözümüyle beslenir.

**Compact yasağı (değişmez):** `_CreateEditOffcanvas.cshtml` · `_DetailsQuickView.cshtml` — zaten FU01'e ait,
FU04 bunlara dokunmaz.

---

## 12. Validation Rules

FU04 bir **çözümleyicidir**, form doğrulaması değil. Doğrulama = **çözüm ön koşulları**, hepsi fail-closed ve
`Status`/reasonCode ile kodlanır (400 fırlatma değil; resolver çıktısıdır):

| # | Ön koşul | Sonuç |
|---|---|---|
| V1 | Doktora bağlı aktif StrategyTemplate yok | `Status=no-strategy`, `strategy_not_found` |
| V2 | Play'in bağladığı journey published/effective değil | `Status=no-journey`, `journey_not_published` |
| V3 | Journey'in ACTIVE aşaması yok | `Status=no-journey` |
| V4 | Next-stage journey sonunu aşar | `Status=end-of-journey`, `StageId=null`, `journey_completed` (KİLİTLİ = flag; loop/stop YOK — §4.4) |
| V5 | Cycle-period CycleCapacity bulunamaz | `Status`'a `capacity_not_found`; süre 0 döner (hesap yapılmaz) |
| V6 | Aktif StrategyTemplate/binding çözülmez (promoted product line'lar okunamaz) | `content_split_unresolved`; sayılar 0 → süre = yalnız `ReportDuration` (fail-closed, §4.5) |
| V7 | Rep manuel override yaptı (FU01 `ContentSource=manual`) | FU04 önerisi **ezilir**; auto-advance override'a saygı duyar (FU01 `IsOverridden` sinyali) |

---

## 13. Failure Path to Verify

| Senaryo | Beklenen |
|---|---|
| Strateji yok | `no-strategy` + reasonCode; uydurulmuş journey YOK |
| Journey published değil | `journey_not_published`; reader zaten draft/archived döndürmez |
| End-of-journey | `Status=end-of-journey`, `StageId=null`, `journey_completed`; **sessiz döngü/wrap-around YOK** (flag) |
| Strateji çözülmez (promoted product line yok) | `content_split_unresolved`, süre sayıları 0 → FU06B `VisitDuration` = `ReportDuration` (yanlış promo süresi ÜRETİLMEZ) |
| Preview endpoint yetkisiz | `crm.visit-content.preview` yoksa **403**; endpoint hiçbir şey persist etmez |
| Capacity yok | `capacity_not_found`, süre 0 |
| Manuel override var | FU04 auto-advance önerisi uygulanmaz; FU01 `manual` konumu korunur |
| Süre persist denemesi | YASAK — resolver hiçbir yere yazmaz (reflection testi) |

---

## 14. Authorization Convention

**İki tüketim yolu, iki yetki modeli:**

- **In-process resolver (FU05 motoru / FU01 handler'ı):** FU04'ün kendi RBAC'ı **yoktur** — çağıran kendi yetkisini
  uygular (FU06B calculator emsali).
- **Preview endpoint (D-SURFACE=E):** **YENİ** permission `crm.visit-content.preview` (read-only) ile korunur.
  Controller `[HasPermission("crm.visit-content.preview")]` + `[Authorize]` (tenant shell), actor `tenant_user`.
  Anahtar **read-only preview** içindir; yazma yetkisi taşımaz (endpoint zaten hiçbir şey persist etmez).

**F-RBAC:** yeni anahtar **beyan edilir** ama seed/grant bu pack'te **yazılmaz** (RBAC katalog + `rolePermissions`
protected). Flip sonrası ayrı bir grant adımı gerekir.

---

## 15. Gateway / API Routing Decision

**Karar (KİLİTLİ, D-SURFACE=E): YENİ Ocelot route çifti GEREKLİ.** Preview endpoint yeni bir top-level yol
(`/api/crm/visit-content/...`) altında yaşadığından FU01/FU06B'nin mevcut wildcard route'larına **düşmez**. Pack
şu route'u **BEYAN eder** (yazmaz):

```text
Upstream:   /api/crm/visit-content/{everything}      (POST, OPTIONS)
Downstream: {CrmService}/api/crm/visit-content/{everything}
Auth:       tenant-scoped; permission crm.visit-content.preview controller'da uygulanır
```

`ocelot.json` **protected, integration-agent owned** — tel çekme build sırasında ayrı task olarak yürütülür; bu
pack yalnızca ihtiyacı beyan eder. Preview endpoint'i **read-only**dir ve **hiçbir şey persist etmez**; upstream
204 → 500 proxy tuzağı (bilinen `IsBodilessStatus` gap) preview 200-body döndürdüğü için **N/A**'dır.

---

## 16. Acceptance Criteria

### 16.1 Çözüm doğruluğu

- **AC-SEQ-1** Doktorun son ziyaretinin `StageIndex=k` olduğunda resolver `StageIndex=k+1`'i (published journey'in
  sıralı yolunda) döndürür; ilk ziyarette `StageIndex=0`.
- **AC-SEQ-2** Auto-advance **deterministiktir** — aynı girdi aynı çıktı; skorlama/öneri/branch YOK.
- **AC-SEQ-3** Journey published/effective değilse veya strateji yoksa **kodlanmış Status + reasonCode**; uydurulmuş
  journey/aşama YOK.
- **AC-SEQ-4** Son aşamadan sonra resolver **`Status=end-of-journey` + `StageId=null` + `journey_completed`** döner;
  **loop / wrap-around / aşama tekrarı YOK** (D-END-OF-JOURNEY=flag; §4.4).

### 16.1b Promo/non-promo split (D-CONTENT-SPLIT=StrategyTemplate)

- **AC-SPLIT-1** Bir içerik item'i StrategyTemplate ProductLines/SKU'sunda görünen ürüne bağlıysa **promo**, değilse
  **non-promo** sayılır (join testi; `IStrategyTemplateReader` test-double ile).
- **AC-SPLIT-2** Aktif StrategyTemplate/binding çözülmezse `content_split_unresolved` + sayılar **0** → süre yalnız
  `ReportDuration` (fail-closed; yanlış promo figürü ÜRETİLMEZ).
- **AC-SPLIT-3** FU04 StrategyTemplate'i **mutate/validate etmez** (yalnız reader; reflection/no-write).

### 16.2 Süre türetme (FU06B devir)

- **AC-DUR-1** `capacity{Promo=5,NonPromo=3,Report=3}`, `PromoItemCount=2`, `NonPromoItemCount=1` →
  `VisitDurationMinutes = 2×5 + 1×3 + 3 = 16` (FU06B calculator'dan; FU04 aritmetik yapmaz).
- **AC-DUR-2** FU04 `ActivityTimeBudgetCalculator`'ı **çağırır**, aritmetiği **inline etmez** (yapısal —
  calculator DI ile enjekte edilir).
- **AC-DUR-3** `content_split_unresolved` iken sayılar 0 → süre = yalnız `ReportDuration` (yanlış promo süresi YOK).

### 16.3 Sınır (additive garantisi) + endpoint

- **AC-BND-1** Resolver hiçbir şey **persist etmez** (reflection: repository write çağrısı yok).
- **AC-EP-1** `POST /api/crm/visit-content/preview` **read-only**dir; handler/controller hiçbir yere yazmaz
  (reflection) ve `crm.visit-content.preview` olmadan **403** döner.
- **AC-EP-2** Preview endpoint çıktısı **aynı resolver'ın** in-process çıktısıyla birebir aynıdır (endpoint ince
  bir sarmalayıcıdır, ikinci bir mantık yolu değildir).
- **AC-BND-2** Resolver journey/strateji/segment/capacity master'larını **mutate etmez** (yalnız reader/getter).
- **AC-BND-3** FU01 `PlannedVisitContentRef` **yazması FU04'te değildir** — FU04 Result döndürür, yazan FU01
  handler'ıdır (kod ayrımı).
- **AC-BND-4** Manuel override (`ContentSource=manual`) FU04 önerisini **ezmez** — auto-advance override'a saygı
  duyar (V7).

---

## 17. Test Expectations

- **Unit (saf):** `VisitContentSequenceResolver` — AC-SEQ-1..4, AC-DUR-1..3, AC-BND-1..4; seam'ler test-double
  (`PlannedVisitTestDoubles`/`StrategyTemplateTestDoubles` emsali) ile beslenir.
- **Additive:** FU06B `ActivityTimeBudgetCalculator` çıktısının FU04 tarafından **değiştirilmediği**; FU01
  `PlannedVisitContentRef` şeklinin dokunulmadığı.
- **Build:** `dotnet build services/Diten.CrmService/src/Diten.CrmService.Api` → 0 hata.
- **Suite:** `dotnet test --filter PlannedVisit` (+ ContentSequence) → 0 fail; tam suite 0 fail.
- **Endpoint:** AC-EP-1/EP-2 — read-only + 403-without-permission + resolver-parity (WebApplicationFactory veya
  authed smoke).
- **Verifier:** `verify_module_id --check-id MOD-0155-FU04` exit 0. `verify_datatable_page` **N/A** (yeni
  golden-reference/DataTable yüzeyi yok; preview = operatör/QA endpoint).
- **Smoke (kullanıcı):** `POST /api/crm/visit-content/preview` gerçek doktor+cycle ile → next-content + hesaplanan
  süre döner, hiçbir şey yazılmaz. FU01 formunu aç → sıradaki aşama + tahmini süre default-fill gelir; rep
  override eder → rozet `manual`; sonraki ziyaret formunda aşama bir sonrakine ilerlemiş görünür.

---

## 18. Ready-for-dev Checklist

- [x] **D-CONTENT-SPLIT** = StrategyTemplate ProductLines (option C) — KİLİTLİ (§0.3/§4.5/§19.1)
- [x] **D-END-OF-JOURNEY** = flag — KİLİTLİ (§4.4)
- [x] **D-SURFACE** = thin read-only preview endpoint (Variant E) + in-process resolver — KİLİTLİ (§0.4/§14/§15)
- [ ] **(flip adımı — Control-Tower)** Frontmatter'a `runtime_code_allowed` + `runtime_code_scope` + flip damgası eklenecek (bu pack'te BİLİNÇLİ YOK)
- [ ] `VisitContentSequenceResolver` **saf**, seam'leri **okur-mutate-etmez**, FU06B calculator'ı **çağırır**
- [ ] Next-stage **deterministik**, FU01 `StageIndex`'inden türer; FU04 `PlannedVisitContentRef` **yazmaz**
- [ ] Süre **persist edilmez**; FU01/FU03/FU05 besleme sözleşmesi netleşti
- [ ] Preview endpoint **read-only**, `crm.visit-content.preview` ile korunur, resolver-parity (AC-EP-1/2)
- [ ] Yeni Ocelot route çifti **beyan edildi** (integration-agent tel çeker); RBAC anahtarı **beyan** (seed ayrı)
- [ ] Acceptance criteria test edilebilir; additive garantisi (FU06B/FU01/reader'lar değişmez) kilitli
- [ ] Golden Reference Compact yüzey N/A gerekçesi (kullanıcı-form yüzeyi yok) kabul edildi

---

## 19. Implementation Notes / Decisions

- **D-HOME = MOD-0155** — içerik sırası + süresi bir saha planlama ölçüsü (§0.2).
- **D-NO-AGGREGATE.** FU04 koleksiyon açmaz; çıktısı türev/transient, depolama FU01'in `PlannedVisitContentRef`'i.
- **D-NO-ENGINE (D8).** Auto-advance = deterministik ordinal okuma; skorlama/branch/öneri yok.
- **D-REUSE-SEAMS.** `IStrategyTemplateReader` + `IContentEngagementJourneyReader` + `ISegmentMembershipReader`
  **zaten mevcut ve MOD-0155 tüketicisini öngörüyor**; **yeni seam açılmaz**. `IStrategyTemplateReader`'ın xml-doc'u
  açıkça *"the READ-ONLY consumption seam a future MOD-0155 (MicroTarget) reads"* der — bu FU o tüketiciyi ilk kez
  canlı çağırır. Yeni bir "StrategyTemplate reader seam" **tanımlanmaz**; mevcut olan reuse edilir.
- **D-DURATION-DELEGATE.** Süre aritmetiği FU06B'nindir; FU04 yalnız `promoCount`/`nonPromoCount` verir. Sonuç
  saklanmaz.
- **D-CONTENT-SPLIT = StrategyTemplate ProductLines (option C) — KİLİTLİ (2026-08-29).** Promo/non-promo,
  StrategyTemplate'in promoted product line/SKU'suyla join'den gelir; fail-closed default korunur (§4.5).
  **F-CONTENT-PROMO-SPLIT kapandı.**
- **D-END-OF-JOURNEY = flag — KİLİTLİ.** Son aşamadan sonra `end-of-journey` statüsü; loop/stop/wrap-around
  reddedildi (§4.4).
- **D-SURFACE = thin read-only preview endpoint (Variant E) — KİLİTLİ.** `POST /api/crm/visit-content/preview` +
  yeni `crm.visit-content.preview` + yeni Ocelot route beyanı; resolver aynı zamanda in-process kalır (§14/§15).

### 19.1 ✅ KİLİTLİ — D-CONTENT-SPLIT = StrategyTemplate ProductLines (option C)

Süre sayıları (`PromoItemCount`/`NonPromoItemCount`), bir içerik item'inin **promo mu non-promo mu** olduğuna
dayanır. **Karar kilitlendi (2026-08-29): kaynak MOD-0167-FU04 StrategyTemplate'tir.** Kampanya "play"i neyin
promote edildiğini söyler; çözülen içerik seti (journey aşamalarından), doktorun segment'inin aktif
StrategyTemplate'inin **ProductLines / SKU allocation'ıyla** join edilir:

```text
promotedRefs = StrategyTemplateBindingSet.ProductLines flatten →
               { GlobalProductId } ∪ { SkuAllocations.GskuId }
promo item   = içerik item'i promotedRefs'te görünen bir ürüne/SKU'ya bağlı
non-promo    = sunulan setteki geri kalan her şey
```

Seçilen kaynak **bedavadır** (play zaten `IStrategyTemplateReader` ile okunuyor) ve kampanya niyetiyle en tutarlı
olandır. **Fail-closed default korunur:** doktor için aktif StrategyTemplate/binding çözülmezse →
`content_split_unresolved`, sayılar 0, süre yalnız `ReportDuration` (yanlış promo figürü üretilmez). Bu karar
**FU06B'nin F-CONTENT-PROMO-SPLIT flag'ini KAPATIR.**

> **Kalan build-zamanı doğrulaması (bağımlılık, açık item DEĞİL — implementasyon detayı):** bir içerik item'inin
> **hangi ürün/SKU'ya bağlı** olduğu bilgisi (content→product bağı) MOD-0162/0290/0167 sahipliğindedir. Bu bağın
> hangi alanda yaşadığı build sırasında doğrulanır (`ContentEngagementJourneyStageDto` / KnowledgePath item'inin
> product ref'i). Bağ yoksa fail-closed default devreye girer. FU04 bu bağı **kurmaz**, yalnız **okur**. Reddedilen
> alternatifler: MOD-0162 `isPromotional` bayrağı (böyle bir bayrak yok/doğrulanamadı) ve MOD-0290 product/brand
> promotion durumu (kampanya niyetini değil ürün durumunu yansıtır). İlgili:
> [[legacy-crmv2-ucln-subjectlist-forwhom-analysis]] (SubjectList = brand+SKU% promo).

---

## 20. Follow-up Items

| ID | İş | Durum / Neden |
|---|---|---|
| ~~F-CONTENT-PROMO-SPLIT~~ | Promo/non-promo sınıflandırma kaynağı | **KAPANDI** — D-CONTENT-SPLIT=StrategyTemplate (§19.1). FU06B'nin devrettiği flag çözüldü |
| ~~F-END-OF-JOURNEY~~ | Journey-sonu davranışı | **KAPANDI** — flag (§4.4) |
| ~~F-SURFACE~~ | Resolver-only vs endpoint | **KAPANDI** — thin preview endpoint + in-process resolver (§14/§15) |
| **F-RBAC-VISIT-CONTENT** | `crm.visit-content.preview` seed/grant | Pack anahtarı **beyan eder**, seed yazmaz; flip sonrası ayrı grant |
| **F-OCELOT-VISIT-CONTENT** | `/api/crm/visit-content/{everything}` route çifti tel çekme | integration-agent owned, build-zamanı ayrı task (§15) |
| **F-CONTENT-CONSUME-FU05** | FU05 paketleme motorunun resolver çıktısını (in-process) tüketmesi | FU05 kapsamı |
| **F-DURATION-FEED-FU03** | FU03 route optimizer'a `durationMinutes` beslemesi | FU03 kapsamı |
| **F-REGISTRY** | `module-implementation-status.md` satırı | Registry yazımı pack yetkisi dışı |

---

## 21. Legacy reference (frozen — kod taşınmaz)

Legacy DitenCRM'de "sıradaki konu" ve içerik-sıralaması `MicroTarget` (per-rep/Year/Month/Week + VisitMix gate) +
`SubjectList` (brand + SKU% promo) + Campaign `PromoCampaign` (per-spec monthly frequency) üzerinden dağıtıktı;
süre `CyclePeriodCalendar`'ın minute-budget'larından türetiliyordu. vNext bunları böler: journey/stage sırası
**MOD-0162**'ye, strateji/segment play'i **MOD-0167**'ye, minute-budget **FU06/FU06B**'ye, plan satırı
**FU01**'e taşındı. Bu FU **hiçbirini yeniden taşımaz** — hepsini **okur** ve "sıradaki içerik + süresi"
türevini üretir. Kod/kolon/`OldSystem` coupling **taşınmaz**. İlgili: [[legacy-visit-planning-analysis]],
[[mod0155-visit-route-planning-program]], [[mod0155-fu06-cycle-capacity-pack]],
[[mod0162-fu05-content-engagement-journey-runtime-ui]], [[mod0167-fu04-strategy-template]].
