# MOD-0151 FU09A — Visit/Route Readiness: Coverage, Contact Availability and Frequency Input Boundaries — Pack Authorization

- **Tarih:** 2026-08-01
- **Modül:** MOD-0151 — Territory Management (`Diten.CrmService`)
- **Task tipi:** Module pack authorization / boundary netleştirme (**kod değil, runtime değil, route planning algoritması değil**)
- **Target file:** `execution/domains/commercial-suite/module-packs/MOD-0151-territory-management.md`
- **Owner:** module-pack-author
- **Verdict:** **PASS**

---

## 1. Preflight

- Bu task, MOD-0151 module pack içinde **FU09A Visit/Route Readiness** kapsamını yetkilendirme ve **boundary
  netleştirme** task'ıdır. **Kod yazma / runtime implementation / route planning algoritması task'ı değildir**;
  hiçbir servis, gateway, frontend, seed veya test dosyası değiştirilmemiştir.
- Amaç: **MOD-0155 Visit Planning / Route Planning başlamadan önce** MOD-0151 tarafında hangi read model,
  endpoint ve sahiplik sınırlarının hazır olması gerektiğini yazılı hâle getirmek.
- Bu task **rota üretmez**, **günlük route planı oluşturmaz**, **optimizasyon algoritması yazmaz**, **GPS /
  check-in / check-out yapmaz**, **visit report üretmez**, **campaign engine kurmaz**. FU06 / workflow / approval /
  ChangeRequest işleri bilinçli olarak en sona bırakılmıştır.
- Otorite sırası korundu: Blueprint Excel > Module Pack > Domain Config > `AGENTS.md` > `.antigravity/rules/`.
- Referans desenler: **FU05A** (§22.2a) current-coverage guard'ı — readiness'in doğruluk temeli; **FU04B** (§22.4)
  read-only query FU'su — "yeni permission önerilmez, yeni menü sayfası açılmaz" deseni; **FU08** (§22.5) —
  blocking / non-blocking sinyal ayrımı ve dependency reconciliation deseni.

## 2. Dependency Confirmation

| Ön koşul | Durum | Kanıt |
|---|---|---|
| FU01 Backend Core | **PASS** | `mod-0151-fu01-contract-territory-model-node-backend-2026-07-23.md` |
| FU02 Territory UI / Model Viewer | **PASS** | pack §22 / FU02 closeout |
| FU02A Country + Business Unit Scope | **PASS** (2 follow-up ile) | `mod-0151-fu02a-country-business-unit-scope-selector-hardening-2026-07-25.md` |
| FU02B Lifecycle | **PASS** | `mod-0151-fu02b-live-smoke-closeout-retry-2-after-status-publish-2026-07-28.md` |
| FU03 Assignment Rules + Preview | **PASS** | pack §22.2 / FU03 closeout |
| FU04 / FU04A Resource Assignments | **operational** | pack §22.3 |
| FU04B Plan vs Current | **canlı zincir PASS** | `mod-0151-fu04b-resource-assignment-plan-vs-current-live-smoke-closeout-2026-07-31.md` |
| FU05 Account Assignment Apply + History | **PASS (90/90)** | `mod-0151-fu05-account-assignment-apply-history-live-smoke-closeout-2026-07-31.md` |
| FU05A CoverageSummary Model Lifecycle Guard | **PASS** | `mod-0151-fu05a-coverage-summary-model-lifecycle-guard-implementation-2026-07-31.md` |
| FU08 Import/Export Hardening | **PASS** | `mod-0151-fu08-import-export-hardening-implementation-2026-08-01.md` |
| MOD-0150 `AccountContactLink` (derived coverage zincirinin ikinci halkası) | **Closeout PASS / %100** | `execution/registries/module-implementation-status.md` MOD-0150 satırı; `mod-0150-final-validation-closeout.md` |
| FU06 / workflow / approval / ChangeRequest | **bilinçli olarak ertelendi** | pack §22.1 FU06 boundary |

**Ek bulgu (FU05A ile hizalama):** pack §22.2a, FU05A'yı açıkça *"contact-derived coverage için prerequisite; guard
FU09'dan **önce** kapanmalı"* diye tanımlıyordu. FU05A PASS olduğu için FU09A'nın doğruluk temeli hazırdır —
readiness, current olmayan coverage'ı candidate'a **alamaz**.

**Dependency uyuşmazlığı bulundu ve giderildi (governance hizalama):** §22 FU tablosunda FU09'un `Depends On` alanı
**`FU05, FU07`** idi. Readiness API'lerinin gerçek hard prerequisite'i **FU05 + FU05A + FU09A**'dır; **evidence pack
(FU07) readiness'in ön koşulu değildir** — FU08 için 2026-08-01'de kaydedilen gerekçenin aynısı. Alan
`FU05, FU05A, FU09A` olarak düzeltildi ve gerekçe §22.1 authorization update bloğuna yazıldı.

## 3. Business Need Summary

MOD-0155 Field Sales / Visit Planning, `legacy-value-preservation.md`'ye göre **legacy pharma sisteminin en olgun ve
kurala en zengin alanıdır**. Ancak MOD-0155 başladığında ihtiyaç duyacağı girdilerin çoğu MOD-0151, MOD-0150 ve
campaign/segmentation tarafında **sahipsiz** durumdadır. Sahiplik netleşmeden implementasyona başlanırsa iki tipik
hata kaçınılmazdır: (a) frequency ve availability bilgisinin **Contact üzerine düz alan** olarak gömülmesi,
(b) MOD-0151'in sessizce bir **mini route planner**'a dönüşmesi. Her ikisinin de geri dönüşü pahalıdır.

FU09A bu yüzden **kapsam açmaktan çok sınır çivilemeye** odaklanır: MOD-0151 neyi sağlayacak (current coverage,
current resource responsibility, derived contact coverage, candidate readiness + reason code), neyi **asla**
sağlamayacak (rota, plan, cadence hesabı, ziyaret geçmişi, availability master, frequency master).

## 4. Legacy CRM Frequency / Campaign Finding

`execution/domains/commercial-suite/legacy-value-preservation.md` incelendi. Doğrudan ilgili bulgular:

| Legacy Asset | Target Module | Not |
|---|---|---|
| **Frequency / cadence** | MOD-0155 (+ MOD-0167 hedefleme) | *"**Frequency veri kaynağı EA-TBD**"* — açık soru |
| MicroTarget | MOD-0155 | Targeting cadence + atama; controller/view taşınmaz |
| Activity / Visit · ActivityReport · Visit status lifecycle | MOD-0155 | Reference schema + rule capture |
| Ziyaret çakışma kontrolü · aynı gün aynı activity type engeli | MOD-0155 | Rule capture |
| Schedule engine · "hastane doktoru → yakın eczane rota önerisi" | MOD-0155 | Algoritma yeniden yazılır |
| MR zone / micro-zone yetkisi | **MOD-0151 (tanım)** + MOD-0155 (tüketim) | ABAC |
| Campaign / PromoCampaign / **CyclePeriod** | MOD-0165 | Cycle period kuralları |
| TargetCustomer / UCLN / StrategyTemplate | MOD-0167 | Segment eval greenfield |

Aynı dosyanın **Open questions (EA-TBD)** bölümü üç soruyu açık bırakıyor: *"Frequency verisi nereden beslenecek
(legacy tablo mu, yeni cadence config mi)?"*, *"Daywork / VisitMix kaynakları nerede?"*, *"HCP identity SoR CRM mi
MDM mi?"*.

**FU09A'nın bu bulguya cevabı:** frequency verisi MOD-0151'e **ait değildir** ve Contact'a gömülmez; ayrı bir
**`VisitFrequencyPolicy` / `CallCyclePolicy`** olarak modellenir — **üreticisi** campaign/segmentation
(MOD-0165 / MOD-0167), **tüketicisi** MOD-0155, MOD-0151'in katkısı yalnız *policy ↔ territory coverage eşleşme
anahtarı ve öncelik sözleşmesi*dir. Böylece legacy'nin haftalık/aylık/iki haftalık/campaign dönemli ziyaret sıklığı
hafızası **kaybolmadan**, yanlış modüle de yazılmadan kayıt altına alınmıştır (pack §22.6, follow-up F21).

## 5. Contact Availability / Working Schedule Requirement

Route planning "hangi doktoru ne zaman ziyaret edebilirim?" sorusunu cevaplayamazsa çalışmaz. Gerçek saha ihtiyacı:

```
Dr. Ayşe — Medicana Beylikdüzü
Pazartesi 09:00–13:00
Çarşamba  14:00–17:00
Preferred: 10:00–12:00
AppointmentRequired: true
```

**Kritik gözlem:** aynı doktor birden fazla hastane / klinik / eczanede çalışır ve müsaitliği **lokasyona göre
değişir**. Bu yüzden availability Contact üzerinde **tek bir alan olarak modellenemez**; doğru anahtar
**`AccountContactLink`**'tir (MOD-0150 D1: Contact↔Account **M:N**).

Boundary'ye yazılan alan sözleşmesi (MOD-0151'de **implement edilmez**): `AccountContactLinkId` · `ContactId` ·
`AccountId` · `Weekday` · `StartTime` · `EndTime` · `PreferredStartTime` · `PreferredEndTime` · `AvoidStartTime` ·
`AvoidEndTime` · `AppointmentRequired` · `AverageVisitDurationMinutes` · `AvailabilityType` · `EffectiveFrom` ·
`EffectiveTo` · `Notes` · `Source`.

Sahiplik: **MOD-0150** master · **MOD-0151** read-only boundary/tüketim · **MOD-0155** plan üretimi.
Follow-up önerisi: **`MOD-0150-FU — Contact Availability and Visit Preference`** (alternatif etiket:
`MOD-0155-PREREQ — Contact Availability and Visit Preference Readiness`).

## 6. Pack Changes

| Yer | Değişiklik |
|---|---|
| Frontmatter `runtime_code_scope` | **+`FU09A-visit-route-readiness-boundaries`** (additive). FU01, FU02, FU02A, FU02B, FU03, FU04, FU04A, FU04B, FU05, FU05A, **FU05B**, FU08 **korundu** |
| Header banner | Başlık "FU09A scope update 2026-08-01"; yetkili FU listesine FU09A eklendi; FU09A açıklama paragrafı (readiness + üç sahiplik boundary'si + "rota üretmez" cümlesi); yetkilendirilmemiş listesine visit/route **planning implementation** (MOD-0155), contact availability **master** (MOD-0150-FU), frequency **engine** ve coverage roll-up (FU09) eklendi |
| **§7.12 (yeni)** | `TerritoryRouteCandidateReadModel` — **query DTO, entity değil**; persist/cache edilmez; rota alanı taşımaz; eksik girdi `unknown` döner |
| **§11.4 (yeni)** | Visit / Route readiness **input boundary** tablosu: dört girdinin sahibi ve MOD-0151'in rolü |
| §15 | FU09A notu: coverage roll-up hâlâ FU09'dadır; FU09A agregat KPI / cadence compliance hesaplamaz |
| §17 | **FU09A permission kararı**: yalnız read → **yeni anahtar önerilmez**; `crm.territory.assignment.read` + `crm.territory.resource.read` (fallback `crm.territory.model.read`); contact görünürlüğü MOD-0150'nin permission yüzeyine tabidir |
| §18 | FU09A notu: **yeni sayfa / menü / ekran açılmaz** — readiness API/read-model yüzeyidir |
| §19 | API tablosuna FU09A satırları: account `coverage-readiness` · node `coverage-accounts` · resource `coverage-readiness` · `route-candidates`; mevcut contact `territory-coverage` satırı FU09A ile etiketlendi (çok-account → çoklu satır) |
| §21 | MOD-0150 satırı (availability read-only tüketim, master MOD-0150'de), MOD-0155 satırı (FU09A ile sağlanan read-only readiness vs. MOD-0155'in sahipliği) güncellendi; **MOD-0165/MOD-0167 satırı eklendi** (frequency policy üreticisi) |
| §22.1 | **FU09A authorization update (2026-08-01)** bloğu + FU09 dependency reconciliation notu |
| **§22.6 (yeni)** | **FU09A — Visit/Route Readiness**: allowed scope · coverage readiness policy · contact derived coverage boundary · contact availability boundary · frequency/call-cycle boundary · last visit/due-overdue boundary · route candidate readiness policy · reason code policy · permission decision · contract flags · test expectations · boundary · explicit exclusions |
| §22 FU tablosu | **FU09A satırı eklendi**; FU09 satırı "kalan kapsam" olarak yeniden yazıldı (`Depends On: FU05, FU05A, FU09A`) |
| §23 | **F21** follow-up'ı eklendi (FU09A + üç bağlı follow-up); F17 "kısmen kapatıldı" olarak güncellendi; **R9** (readiness'in gizli route planner'a dönüşmesi), **R10** (availability/frequency'nin yanlış yere gömülmesi), **R11** ("veri yok" ≠ "uygun değil") riskleri eklendi |
| §24 | FU09A acceptance criteria maddesi eklendi |
| §25 | FU09A implementation prompt'u (#4) ve **`MOD-0150-FU — Contact Availability and Visit Preference`** authorization önerisi (#5) eklendi |

**Silinen / daraltılan hiçbir mevcut scope yoktur.**

## 7. FU09A Authorized Scope

Hepsi **yalnız-okuma**:

1. **Territory coverage readiness** — account şu an hangi node / hangi active model / hangi BU scope / hangi
   position-resource sorumluluğunda; **FU05A guard'ı zorunlu**; `effectiveAt` destekli.
2. **Resource / MR responsibility readiness** — resource hangi node'lardan, hangi BU scope'unda, hangi position code
   ile **current** sorumlu; replacement/transfer sonrası current sahip; Plan vs Current farkının read-only tüketimi.
   *Route planning bu bilgiyi kullanır, değiştirmez.*
3. **Contact derived coverage** — `Contact → AccountContactLink → Account → current AccountTerritoryAssignment /
   CoverageSummary`; her satır provenance taşır.
4. **Route candidate readiness projeksiyonu** — §7.12 read model; karar değil **sinyal** + reason code.
5. **Reason code sözleşmesi** — stabil, lowercase-snake, lokalize mesajdan bağımsız.
6. **Boundary kayıtları (implementation değil)** — contact availability (MOD-0150), frequency/call-cycle policy
   (MOD-0165/0167 → MOD-0155), last visit / due-overdue (MOD-0155).
7. Backend testleri, contract readiness flag hizalaması, Gateway-only authenticated smoke, FU09A evidence report.

**Temel mimari kural (pack'e yazıldı):** *readiness bir **girdi yüzeyi**dir, bir planlayıcı değildir.*

## 8. FU09A Exclusions

Route optimization algoritması · günlük rota oluşturma · visit plan oluşturma · visit execution · check-in/check-out ·
GPS validation · visit report · digital detailing · survey · campaign engine · frequency/call-cycle **engine**
implementation · contact availability **master** implementation (ayrıca yetkilendirilmedikçe) · MOD-0150 Contact
master mutasyonu · Account master mutasyonu · `ContactTerritoryAssignment` · patient (hasta) verisi · workflow
approval · ChangeRequest · MOD-0023 entegrasyonu · evidence pack · import/export yeni scope · Brand/Product master ·
coverage roll-up (FU09) · hard delete · Mongo hand-edit · RBAC seed/grant (ayrıca yetkilendirilmedikçe) ·
MOD-0048 publish (ayrıca yetkilendirilmedikçe) · `crm.territory.delete` · `crm.micro-zone.manage` · request
payload'ında `TenantId` · direct port 5061 business API çağrısı.

## 9. Coverage Readiness Policy

| Soru | Karar |
|---|---|
| Account şu an hangi node'da? | Current coverage; **FU05A operationally-valid-model guard'ı zorunlu** |
| Hangi active model kapsamında? | Yalnız `active` + effective window içi model; archived/inactive/superseded düşer |
| Hangi BU scope altında? | FU02A normalized `BusinessScopes`; BU filtresi desteklenir |
| Bu account'tan kim sorumlu? | FU04A **current** responsibility (position code ile); `proposed` planning satırı current sayılmaz |
| Coverage current değilse? | Candidate'a **girmez**; `coverage_not_current` ile raporlanır — **sessizce düşürülmez** |
| Geçmiş/gelecek tarih? | `effectiveAt` desteklenir (FU05A §22.2a policy #3) |
| Cache? | **Yok** — türetilmiş projeksiyon; kaynak ile projeksiyon çelişemez |
| Mutasyon? | **Yok** — hiçbir readiness endpoint'i yazma yapmaz |

## 10. Contact Derived Coverage Boundary

- **`ContactTerritoryAssignment` eklenmez** (§11.2 kararı korunur); Contact'a `TerritoryId`/`ZoneId`/`MRId` alanı
  eklenmez.
- Tek yol: `Contact → AccountContactLink → Account → current AccountTerritoryAssignment / CoverageSummary`.
- **Sahiplik:** MOD-0151 account coverage'ın **current doğruluğundan**, MOD-0150 Contact + `AccountContactLink`
  **master'ından** sorumludur. Derived coverage endpoint'i MOD-0151'de veya ileride cross-module read model olarak
  tasarlanabilir; her iki durumda **Contact master mutate edilmez**.
- Bir contact birden fazla account'a bağlıysa coverage **çoklu döner** (union) — **tek coverage varsayımı yasak**.
- `IsPrimary` link **default** işaretlenebilir; bu bir görüntüleme tercihidir, filtre değildir.
- Her satır provenance taşır (`AccountContactLinkId` / `AccountId` / assignment).
- Inactive / süresi geçmiş link → `contact_not_linked_to_account`.

## 11. Contact Availability Boundary

| Katman | Sorumluluk |
|---|---|
| **MOD-0150** | `AccountContactLink` bazlı `ContactAvailability` / `VisitPreference` **master** (ayrı yetkilendirme) |
| **MOD-0151 (FU09A)** | Yalnız **boundary tanımı** + **read-only** tüketim; master açılmaz |
| **MOD-0155** | Availability'yi kullanarak visit/route plan **üretir** |

Kurallar: availability **link bazlıdır** (contact bazlı değil); `Avoid*` preferred'ın tersi değil, **ayrı ve daha
güçlü** bir kısıttır; veri yoksa `AvailabilityStatus=unknown` döner ve candidate **sessizce düşmez** —
`contact_not_available_on_day` yalnız veri **varsa ve uymuyorsa** üretilir. MOD-0151 bu veriyi yazmaz, türetmez,
persist etmez.

## 12. Frequency / Call-Cycle Boundary

**Karar:** frequency Contact içine düz alan olarak **gömülmez**; ayrı `VisitFrequencyPolicy` / `CallCyclePolicy`
olarak modellenir. Alanlar: `PolicyId` · `TenantId` · `TargetType` (`account`/`contact`/`account-contact-link`) ·
`TargetId` · `BusinessUnit` · `TerritoryNodeId?` · `CampaignId?` · `BrandId?`/`ProductId?` *(future)* ·
`FrequencyType` (`weekly`/`monthly`/`biweekly`/`cycle-based`/`custom`) · `RequiredVisitCount` · `PeriodType`
(`week`/`month`/`cycle`) · `EffectiveFrom` · `EffectiveTo` · `Priority` · `Source`
(`campaign`/`manual`/`segmentation`/`legacy-import`) · `Notes`.

Sahiplik: **MOD-0165 / MOD-0167 üretir** → **MOD-0155 tüketir** → **MOD-0151 yalnız birleşim anahtarını tanımlar**
(`TargetType/TargetId` + `BusinessUnit` + `TerritoryNodeId`, çakışmada `Priority` + `EffectiveFrom/To`). Seçilen
policy readiness cevabında **görünür** olmalıdır (sessiz seçim yasak). Policy yoksa `FrequencyStatus=unknown`;
**varsayılan sıklık uydurulmaz**. Frequency implementation'ı bu task'ta **yapılmamıştır**.

## 13. Last Visit / Due-Overdue Boundary

**Sahiplik:** last visit tarihi, visit status, visit history → **MOD-0155** (Activity / Visit). **MOD-0151 yazmaz,
saklamaz, türetmez.**

Due/overdue hesabının girdileri (yalnız sözleşme): target · frequency policy · last completed visit date ·
visit status · effective date · availability window · **current coverage (MOD-0151)** · **current resource
responsibility (MOD-0151)**.

**Karar:** FU09A implementation'ı açılırsa due/overdue yalnız **placeholder / readiness contract** olabilir —
`LastVisitDate` ve `DueStatus` şemada yer alır, girdi yoksa `unknown` döner. **Gerçek engine MOD-0155'e aittir**;
MOD-0151 içinde cadence compliance hesaplamak **scope ihlalidir**.

## 14. Route Candidate Readiness

Şartlar (karar değil **sinyal**; her ihlal reason code üretir): (1) account current coverage içinde [FU05+FU05A] ·
(2) account active [MOD-0149] · (3) lokasyon/lat-lon mevcut [MOD-0149 — **MOD-0151 adres persist etmez**] ·
(4) `AccountContactLink` active [MOD-0150] · (5) contact o gün/saatte müsait [MOD-0150 boundary] · (6) frequency
due/overdue sinyali [MOD-0165/0167 → MOD-0155 boundary] · (7) son ziyaret uygun [MOD-0155 boundary] · (8) resource o
territory'den **current** sorumlu [FU04A/FU04B] · (9) BU scope uyumlu [FU02A].

Response (§7.12): `AccountId` · `AccountName` · `TerritoryNodeId` · `TerritoryNodeCode` · `BusinessUnit` ·
`ResourceId` · `ResourceDisplayName` · `ContactId?` · `ContactName?` · `AccountContactLinkId?` ·
`AvailabilityStatus` · `PreferredVisitWindow?` · `FrequencyStatus` · `LastVisitDate?` · `DueStatus` ·
`LocationReadiness` · `ReasonCodes[]`.

**Sınırlar:** sıra / mesafe / süre / gün planı / stop listesi / optimizasyon skoru **yok**; "önerilen sıra" alanı
eklenemez; readiness çağrısı **hiçbir kayıt yazmaz** (candidate log dahil); eksik girdi candidate'ı sessizce
düşürmez.

## 15. Reason Code Policy

Stabil, lowercase-snake, makine-okunur, lokalize mesajdan bağımsız. Minimum küme: `readiness_ok` ·
`coverage_not_current` · `account_inactive` · `account_missing_location` · `contact_not_linked_to_account` ·
`contact_inactive` · `contact_not_available_on_day` · `outside_preferred_window` · `frequency_not_due` ·
`frequency_overdue` · `no_last_visit` · `resource_not_current_owner` · `business_scope_mismatch`.

Kurallar: bir satır **birden fazla** kod taşıyabilir; `readiness_ok` diğerleriyle birlikte dönmez;
`outside_preferred_window` gibi uyarı kodları candidate'ı tek başına elemez (FU08 blocking/non-blocking deseniyle
aynı ruh); **girdi eksikliği (`unknown`) ile kural ihlali ayrı kodlarla ifade edilir — "veri yok" asla "uygun değil"
olarak raporlanmaz**.

## 16. Contract Flag Notes

```json
{
  "supportsVisitRouteReadiness": true,
  "supportsContactDerivedCoverageReadiness": true,
  "supportsRouteCandidateReadiness": true,
  "supportsContactAvailabilityInputBoundary": true,
  "supportsVisitFrequencyInputBoundary": true,
  "supportsWorkflowActivation": false
}
```

**Korunan mevcut flag'ler:** `supportsCoverageSummary` · `supportsCoverageSummaryModelLifecycleGuard` ·
`supportsResourceAssignments` · `supportsResourceAssignmentPlanVsCurrent` · `supportsAccountAssignmentApply` ·
`supportsAssignmentHistory` · `supportsAssignmentRules` · `supportsAssignmentPreview` · `supportsTerritoryExport` ·
`supportsTerritoryImportExport` · `supportsTerritoryImportDryRun` · `supportsTerritoryImportApply` ·
`supportsResourceAssignmentImportApply=false` · `supportsWorkflowActivation=false`.

`supportsVisitRoutePlanning` gibi bir flag **eklenmedi** — MOD-0151 route planlamaz. `*InputBoundary` flag'leri
"bu girdinin **sözleşmesi** tanımlı" demektir; "bu **veri** MOD-0151'de vardır" demek **değildir**.

## 17. Guard Checks

| Kontrol | Sonuç |
|---|---|
| Runtime code changed? | **No** |
| Backend/frontend changed? | **No** |
| Gateway changed? | **No** |
| MOD-0150 code changed? | **No** |
| MOD-0155 code changed? | **No** |
| Workflow scope opened? | **No** |
| Visit/route implementation opened? | **No** |
| Route optimizer opened? | **No** |
| Campaign/frequency engine opened? | **No** |
| Contact availability master implementation opened? | **No** (yalnız MOD-0150 follow-up'ı önerildi) |
| Account/Contact mutation opened? | **No** |
| ContactTerritoryAssignment opened? | **No** |
| Patient data opened? | **No** |
| Import/export scope changed? | **No** |
| Evidence scope opened? | **No** |
| Brand/Product opened? | **No** |
| Hard delete allowed? | **No** |
| RBAC seed/grant changed? | **No** (yeni permission literal'i de önerilmedi) |
| MOD-0048 publish changed? | **No** |
| New master aggregate added to MOD-0151? | **No** (§7.12 yalnız query DTO) |
| New page/menu opened? | **No** |
| Coverage roll-up (FU09) opened? | **No** |
| FU09A scope added? | **Yes** |
| Existing FU scopes preserved? | **Yes** (FU01, FU02, FU02A, FU02B, FU03, FU04, FU04A, FU04B, FU05, FU05A, FU05B, FU08) |
| FU05A / FU04A guard'ları korundu mu? | **Yes** (readiness onları okur, kurallarını değiştirmez) |
| supportsWorkflowActivation remains false? | **Yes** |
| `crm.territory.delete` / `crm.micro-zone.manage` opened? | **No** |
| Request payload'ında `TenantId` opened? | **No** (claim'den) |
| Direct 5061 business call opened? | **No** |

## 18. Created / Updated Files

- **Updated:** `execution/domains/commercial-suite/module-packs/MOD-0151-territory-management.md`
  - frontmatter `runtime_code_scope` (**+FU09A additive**)
  - header banner (FU09A paragrafı + yetkili FU listesi + "rota üretmez" sınırı)
  - **yeni §7.12** `TerritoryRouteCandidateReadModel` (query DTO, entity değil)
  - **yeni §11.4** Visit / Route readiness input boundary tablosu
  - §15 FU09A notu (roll-up hâlâ FU09)
  - §17 FU09A permission kararı (yeni anahtar yok)
  - §18 FU09A notu (yeni sayfa/menü yok)
  - §19 API tablosu FU09A endpoint seti + contact coverage satırının etiketlenmesi
  - §21 MOD-0150 / MOD-0155 satırları güncellendi, MOD-0165/MOD-0167 satırı eklendi
  - §22.1 **FU09A authorization update (2026-08-01)** + FU09 dependency reconciliation
  - **yeni §22.6** FU09A — Visit/Route Readiness (scope · coverage readiness · derived contact coverage ·
    availability · frequency · last-visit/due-overdue · route candidate · reason code · permission · flags ·
    test expectations · boundary · exclusions)
  - §22 FU tablosu: **FU09A satırı** + FU09 satırının yeniden yazımı (`Depends On: FU05, FU05A, FU09A`)
  - §23 **F21** follow-up + F17 güncellemesi + **R9 / R10 / R11** riskleri
  - §24 FU09A acceptance criteria maddesi
  - §25 FU09A implementation prompt + `MOD-0150-FU` authorization önerisi
- **Created:** `docs/audits/mod-0151-fu09a-visit-route-readiness-boundaries-pack-authorization-2026-08-01.md` (bu rapor)

**Kod, test, gateway, frontend, seed veya reference-data dosyası değiştirilmemiştir.**

## 19. Final Verdict

**PASS**

- FU09A scope'u **additive** olarak eklendi; mevcut 12 FU scope'unun (FU01–FU05B, FU08) hiçbiri silinmedi veya
  daraltılmadı.
- **Visit/Route Readiness boundary netleşti:** MOD-0151 neyi sağlar (current coverage, current resource
  responsibility, derived contact coverage, candidate readiness + reason code), neyi asla sağlamaz (rota, plan,
  cadence, ziyaret geçmişi, availability master, frequency master).
- **Contact availability / working schedule requirement kaydedildi:** `AccountContactLink` bazlı, 17 alanlık
  sözleşme; sahiplik MOD-0150'de; MOD-0151 yalnız read-only tüketici.
- **Frequency / call-cycle / campaign input boundary kaydedildi:** `VisitFrequencyPolicy` / `CallCyclePolicy`;
  üretici MOD-0165/MOD-0167, tüketici MOD-0155; `legacy-value-preservation.md`'nin açık **EA-TBD frequency sorusu**
  territory tarafından cevaplandı.
- **Contact derived coverage policy korundu:** `ContactTerritoryAssignment` yok; çok-account'lu contact **çoklu
  coverage** döner; primary yalnız default gösterim.
- **Route candidate readiness policy netleşti:** 9 şart, §7.12 response sözleşmesi, sıralama/mesafe/optimizasyon
  alanı yasağı, "hiçbir kayıt yazmaz" kuralı.
- **Reason code policy netleşti:** 13 stabil kod; blocking/uyarı ayrımı; **"veri yok" ≠ "uygun değil"**.
- **Route implementation açılmadı · campaign/frequency engine açılmadı · Account/Contact mutation açılmadı ·
  patient data açılmadı · workflow açılmadı**; `supportsWorkflowActivation=false` korundu.
- Ek governance kazanımları: (a) FU09'un `Depends On` alanındaki **FU07 çelişkisi** giderildi ve FU09 "kalan kapsam"
  olarak yeniden tanımlandı; (b) §7.12 **entity değil, query DTO** olarak yazılarak MOD-0151'e yeni master
  sızması engellendi; (c) **F21** follow-up'ı üç bağlı authorization'ı (MOD-0150 availability, frequency policy
  sahipliği, MOD-0155 due/overdue engine) açıkça açtı; (d) readiness'e özgü üç risk (**R9** gizli route planner,
  **R10** yanlış yere gömülen availability/frequency, **R11** "veri yok" ≠ "uygun değil") risk tablosuna yazıldı.
- Implementation prompt'u hazırlanabilir.

## 20. Next Recommended Prompt

```
MOD-0150-FU — Contact Availability and Visit Preference Pack Authorization
```
