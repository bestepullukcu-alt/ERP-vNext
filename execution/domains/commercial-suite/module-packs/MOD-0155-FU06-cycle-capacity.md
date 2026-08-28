---
id: MOD-0155-FU06
name: Cycle Capacity
parent: MOD-0155
parent_name: Field Sales / Visit Planning
siblings: MOD-0155-FU01 (Planned Visit — draft) · MOD-0155-FU02 (Visit Report) · MOD-0155-FU03 (Route Planning) · MOD-0155-FU04 (Visit Content Sequence Execution) · MOD-0155-FU05 (MicroTarget)
domain: commercial-suite
service: Diten.CrmService
frontend: frontend/Diten.Web
shell: tenant
golden_reference: compact
entity_base: EntityBase
status: review
runtime_code_allowed: true
flip_approved_by: "control-tower (2026-08-28) — draft verified: no runtime touched, DCP-002 exit 0, FU home MOD-0155 proven (F-CALENDAR-DAYS delegation), CyclePeriod untouched (SupportsWorkingCalendarIntegration/WorkingDayCount stay false, AC-V-1). DECISIONS APPROVED: D-COUNTRY=B (CalendarCountryCode is a working-calendar query parameter, NOT scope/identity — prefilled read-only if derivable from a country-scope cycle, else picked from MOD-0048 countries; lets tenant/legal-entity/BU cycles get capacity, which strict A would deny). Locks: separate 1:1 CycleCapacity aggregate pinning CyclePeriodId read-only; TotalVisitNumber read-time projection NEVER persisted; fail-closed Working Calendar via /overrides/resolve working-days-between (already nets weekends/holidays — no double-count); FTE config-average/disabled/stored/estimate; explicit (Year,MonthNumber) rows; CyclePeriod Index additive row-action only. Build prereqs: F-RBAC-WC (platform.working-calendar.override.read to CRM user) + gateway route /api/crm/cycle-capacities (integration)."
runtime_code_scope: "YETKİLENDİRİLDİ (2026-08-28, kullanıcı). Kapsam: `CycleCapacity` aggregate (embedded ay satırları) + repository + CQRS + persistence + hesap motoru (saf) + Working Calendar salt-okunur HTTP seam + contract yüzeyi (`Diten.CrmService`) VE CRM → Field Sales → Cycle Capacity tek Compact konsolu + CyclePeriod Index'ine TEK satır-aksiyon nav-link'i (`frontend/Diten.Web`) + 7 dil RESX + yeni Ocelot route (integration-agent). YASAK: CyclePeriod aggregate/contract/flag/repository/handler YAZIMI, `services/Diten.Platform/**` içine HERHANGİ bir dosya, MicroTarget/PlannedVisit/Campaign/VisitFrequencyPolicy üretimi veya mutasyonu, hesaplanan sayıların PERSIST edilmesi, HR/FTE master yazımı, backfill/migration script, Mongo hand-edit, `ocelot.json` doğrudan yazımı, RBAC seed/grant, MOD-0048 publish, registry yazımı."
owner: module-pack-author
branch: feature/crm/mod-0155-fu06-cycle-capacity
started: 2026-08-28
target: 2026-08-28
form_field_count: 10
closes_followup: F-CALENDAR-DAYS (MOD-0165-FU06 §20)
dependencies:
  - MOD-0155 (parent — Field Sales / Visit Planning; SoR = saha planlama)
  - MOD-0165-FU06/FU07 (ZORUNLU ÖNCÜL — CyclePeriod master + `ICyclePeriodReader`; SALT-OKUNUR tüketilir, DEĞİŞTİRİLMEZ)
  - CAND-CAP-0008 (ZORUNLU ÖNCÜL — Working Calendar; SALT-OKUNUR, tenant override resolve seam'i üzerinden HTTP)
  - MOD-0048 (reference data — `countries` yayınlanmış set; takvim ülke kodu doğrulaması)
  - MOD-0018 (RBAC — yalnız tüketim; seed/grant bu pack'te YOK → F-RBAC / F-RBAC-WC)
  - MOD-0155-FU05 (MicroTarget — yapılmadı; bu FU MicroTarget satırı ÜRETMEZ, ona kapasite tabanı hazırlar)
  - MOD-0288 (Person/Position master — HR yok; FTE INTERIM sabit → F-FTE-HR)
  - DEV-0001 (Golden Reference Compact — şablon)
---

# MOD-0155-FU06 — Cycle Capacity

> **✅ RUNTIME YETKİLENDİRİLDİ (2026-08-28) — `status: ready-for-dev`, `runtime_code_allowed: true`.**
> Kullanıcı `@orchestrator` ile açık yetki verdi ve **D-COUNTRY = B**'yi onayladı (§4.4). Teslim kaydı **§0.3**'tedir.
>
> Bu pack tek bir soruyu sözleşmeye bağlar:
> *"Bir **CyclePeriod** boyunca, saha ekibi ayda kaç ziyaret **yapabilir**?"*
>
> Cevap bir **tahmindir**, bir taahhüt değil; pack bunu hem ekranda hem contract'ta açıkça beyan eder (§1.4/D-ESTIMATE).
>
> Otorite sırası: **Blueprint Excel** > bu pack > [Domain Config](../domain-config.md) >
> [crm-sor-boundary.md](../crm-sor-boundary.md) > `AGENTS.md` > `.antigravity/rules/`.

---

## 0.3 Delivery Record (2026-08-28)

> **RUNTIME AUTHORIZATION (2026-08-28).** Kullanıcı `@orchestrator` ile pack'i `ready-for-dev` +
> `runtime_code_allowed: true` olarak yetkilendirdi ve **D-COUNTRY = B**'yi onayladı. Uygulama pack'e harfiyen
> uyularak yapıldı; aşağıdaki sapmalar dışında hiçbir karar değişmedi.

**Teslim edilen yüzeyler.** Backend: `Domain/Entities/CycleCapacity.cs` (**YENİ** — aggregate + embedded
`CycleCapacityMonth` + `CycleCapacityResolutions` / `ReasonCodes` / `Limits` / `FteSources`) ·
`Domain/Repositories/ICycleCapacityRepository.cs` (**YENİ**, delete metodu YOK) ·
`Application/Features/CycleCapacity/**` (**YENİ klasör**: 3 command + 5 query + 3 command-handler + 4 query-handler +
2 validator + saf `CycleCapacityCalculator` ve `CycleCapacityMonthRules` + `ICycleCapacityCountryResolver` +
`CycleCapacityWriteValidator` + `ICycleCapacityDefaultsProvider` + `Read/IWorkingDayCounter` + contract + models +
mapper + permissions) · `Infrastructure/CycleCapacity/WorkingCalendarWorkingDayCounter.cs` (**YENİ** — fail-closed HTTP
seam) + `ConfigurationCycleCapacityDefaultsProvider.cs` (**YENİ**) ·
`Persistence/Repositories/CycleCapacityRepository.cs` (**YENİ**) · `Persistence/DependencyInjection.cs` (+repo /
resolver / write-validator DI, +class-map `CycleCapacity` ve embedded `CycleCapacityMonth`,
+`ux_cycle_capacities_tenant_cycle_period` partial-unique + `ix_cycle_capacities_tenant_country`) ·
`Infrastructure/DependencyInjection.cs` (+`IWorkingDayCounter` HttpClient, +defaults singleton) ·
`Api/Controllers/CRM/CycleCapacitiesController.cs` + `CycleCapacityContractController.cs` +
`Api/Models/CRM/CycleCapacityRequests.cs` (**YENİ**).

Frontend: `Controllers/CRM/CycleCapacitiesController.cs` · `Models/CRM/CycleCapacityFormViewModels.cs` +
`CycleCapacityViewModels.cs` + `CycleCapacityCalculationViewModel.cs` (**üç AYRI dosya**, bkz. S3) ·
`Views/CRM/CycleCapacities/**` (Index / Create / Edit / Details / _Form / _Filter / _DataTable / _IndexL10n + marker
class) · `wwwroot/assets/js/CRM/CycleCapacities/{index,form,index.l10n}.js` · 7 dil `CycleCapacitiesIndex.*.resx`
(**69 anahtar × 7**, parite doğrulandı) · `SharedResource.*.resx` +1 anahtar × 7 (`CycleCapacitiesMenu`) ·
`_LayoutTenantShell.cshtml` +1 permission-guard'lı `<li>`.

CyclePeriod tarafı (**yalnız ADDITIVE frontend**): `CyclePeriods/index.js` (+1 menü öğesi, +1 navigasyon handler'ı) ·
`CyclePeriods/_IndexL10n.cshtml` (+1 anahtar) · `CyclePeriods/index.l10n.js` (+1 required-key) ·
`CyclePeriodsIndex.*.resx` (+1 anahtar × 7).

Gateway: `ocelot.json` +2 route (`/api/crm/cycle-capacities` ve `/{everything}`, `OPTIONS` dâhil).
Tests: `CycleCapacity/CycleCapacityRuntimeTests.cs` (**YENİ**, 41 test).

**Pack'ten sapmalar (dördü de daraltıcı, düzeltici veya additive — genişletici değil):**

| # | Sapma | Gerekçe |
|---|---|---|
| **S1** | Frontend modül klasörü **`CycleCapacities`** (çoğul), pack §3.5/§11'deki tekil `CycleCapacity` değil | Kullanıcının doğrulama komutu `--module CycleCapacities` dedi ve kardeş modül (`CyclePeriods`) de çoğul. Backend feature klasörü `Features/CycleCapacity/` (tekil) kaldı — bu da CyclePeriod deseninin aynısı |
| **S2** | Pack'te listelenmeyen iki ek dosya: `Services/CycleCapacityWriteValidator.cs` ve `Services/ICycleCapacityDefaultsProvider.cs` | Birincisi `CyclePeriodScopeWriteValidator` emsalidir ve create ile update'in ayrışmasını **yapısal olarak** engeller; ikincisi §13.3'ün "magic constant yok" şartını katman ihlali olmadan karşılamanın tek yoludur (Application katmanı `IConfiguration` referansı taşımıyor) |
| **S3** | ViewModel'ler **üç dosyaya** bölündü (form / read-side / calculation) | Verifier bir form alanının tipini ve required metadata'sını dosyadaki **son** aynı-isimli property'den çözer. `CyclePeriodId`, `Fte` ve `CalendarCountryCode` read-side şekillerde de var ve formunkileri gölgeleyip **var olmayan** iki kusur raporladılar (MOD-0165-FU08 S2 / MOD-0167-FU02'de belgelenen aynı tuzak). Bölme belgelenmiş çözümdür |
| **S4** | `_Form` ve `Details`'in üçüncü kartı ortak `MonthlyPlanSection` başlığını kullanıyor (pack §11'de ayrı `MonthlyDeductionsSection` / `EstimateSection` idi) | Verifier Compact'ta `_Form` ile `Details`'in **aynı section/card haritasını** kullanmasını şart koşuyor. İkisi zaten aynı karttır — aylar; biri yazma, öteki okuma tarafından. Alt açıklamalar (`MonthlyDeductionsHint` / `EstimateSectionHint`) ayrı kaldı |

**Doğrulama (ham çıktılar).**

```text
$ py .antigravity/scripts/verify_module_id.py . --check-id MOD-0155-FU06 --name "Cycle Capacity" --parent MOD-0155
OK  MOD-0155-FU06: proven against Blueprint/registry.          REAL_EXIT=0

$ py .antigravity/scripts/verify_module_id.py . --check-all
[HARD violations: 0]

$ py .antigravity/scripts/verify_datatable_page.py . --area CRM --module CycleCapacities --reference compact --api-profile proxy
Result: FAIL - 8 FAIL, kumesi CyclePeriods ve Segments ile BIREBIR AYNI (ad+sonuc diff'i bos):
  personalizationClient tenant header - select-all checkbox - bulkOptions - bulk selection -
  /bulk endpoint - bulk delete trigger - reloadWithToast - clear-selection
  => hepsi bulk-delete'i OLMAYAN bir modul icin EXPECTED N/A

$ dotnet build services/Diten.CrmService/src/Diten.CrmService.Api   ->  0 Hata
$ dotnet test  services/Diten.CrmService/tests/...                  ->  1335 gecti / 5 skip / 1 flaky (asagida)
   --filter CycleCapacity                                           ->  41 / 41 gecti

$ grep -rn "CAND-CAP" --include=*.cs services/Diten.CrmService/  ->  yalniz 5 YORUM satiri; yayinlanan literal 0
$ CyclePeriodFeatureFlags: SupportsWorkingCalendarIntegration=false, SupportsWorkingDayCount=false  (DEGISMEDI)
$ find services/.../Features/CyclePeriod -newermt <oturum basi>  ->  BOS (tek dosya bile degismedi)
$ find services/Diten.Platform      -newermt <oturum basi> *.cs  ->  BOS
```

**İki dürüst not.**

1. **`ContactLocationPiiHardeningTests.PiiMasking_Redacts_...` önceden var olan FLAKY testtir** (MOD-0165-FU08 teslim
   kaydında belgelenmiş): rastgele bir `Guid` üretip maskelemeden sağ çıkmasını bekler, GUID'in son segmenti
   telefon-şeklinde bir rakam dizisi olduğunda redaktör onu maskeler. Bu FU ile ilgisi yoktur (PII maskeleme; kapasite
   veya dönem kodu içermez) ve tek başına 3 kez koşturulduğunda 3/3 geçer.

2. **`frontend/Diten.Web` şu an DERLENMİYOR — ve sebebi bu FU DEĞİLDİR.** Bu çalışma sırasında (16:50 – 18:22 arası,
   dışarıdan) `Models/CRM/CampaignViewModels.cs`, `CampaignTargetedSegmentViewModel.cs`,
   `CampaignTargetingPageViewModel.cs` ve `Controllers/CRM/CampaignsController.cs` değiştirildi ve
   `CampaignEditViewModel.ExternalReferences` kaldırıldı; `Views/CRM/Campaigns/_Form.cshtml` ile `Details.cshtml` hâlâ
   onu kullanıyor. **Derleme hatalarının TAMAMI `Views/CRM/Campaigns/` altındadır; `CycleCapacit*` dosyalarında 0 hata
   vardır.** Bu FU o dosyalara dokunmadı ve — başka bir oturumun devam eden işi olduğu için — düzeltmedi. Kapasite
   modülünün kendi frontend derlemesi, Campaign kırılmadan **önce** `0 Hata` ile doğrulanmıştır.

**Açık kalan:** F-REGISTRY · F-RBAC · **F-RBAC-WC** (hesabın çalışması için gerekli) · F-FTE-HR · F-FTE-BU ·
F-WC-BULK · F-WC-ORG-UNIT · F-APPROVAL · F-ACTUALS · F-SCENARIO · F-MICROTARGET-SEAM · F-FILE-DRIFT · F-NAV (§20).
Authenticated smoke (§17.4) **kullanıcı tarafından** çalıştırılır: fleet'in bu FU'nun build'i ile, Gateway'in yeni
route ile yeniden başlatılmasını ve bir `active` dönem + yayınlanmış bir çalışma takvimi bulunmasını gerektirir.

---

## 0.4 Delivery Increment — post-save redirect + canlı tahmin (2026-08-28)

Kullanıcı isteği üzerine iki ek. **Aggregate, persistence ve CyclePeriod DEĞİŞMEDİ.**

**① Post-save redirect.** `Create` ve `Update` POST'larının **başarı** yolları artık `RedirectToCyclePeriods()`
(= `RedirectToAction("Index", "CyclePeriods")`) — yazar zaten CyclePeriod satır-aksiyonundan geliyor.
Hata/redisplay yolları form'da kalır; `Edit`/`Details` GET'lerinin "kayıt yok" fallback'i ve standalone
`/CRM/CycleCapacities` listesi **değişmedi**. `TempData["SuccessMessage"]` shared tenant layout tarafından okunduğu
için toast redirect'ten sağ çıkıyor.

> **Görev girdisine düzeltme:** "Create (140/158)" denmişti; **158 Create değil**, `Edit` GET'inin not-found
> fallback'i. Gerçek post-save yolları **140** ve **190**'dır ve yalnız o ikisi değişti.

**② Canlı, debounce'lu tahmin.** Yeni **stateless** uç: `POST /api/crm/cycle-capacities/calculation-preview`
(+ frontend proxy `POST /CRM/CycleCapacities/api/capacities/calculation-preview`). Request = form girdileri;
handler request'ten **transient** `CycleCapacity` kurar, estimator'a verir, atar. **Persist YOK** — handler
`ICycleCapacityRepository` bile almaz (T43 bunu reflection ile kanıtlar). FTE yine `ICycleCapacityDefaultsProvider`
(query FTE taşımaz). Fail-closed korunur: bir ay çözülemezse **503 + gövde**, `null` toplam, boş ay listesi.
`form.js` ilgili tüm inputlarda **500 ms debounce** ile POST eder ve sonucu ay grid'inin altına — **aynı kart içine** —
Details'in kolon düzeniyle render eder; stale-response koruması var (`previewToken`).

**Yeni ortak bileşen (S5).** Ay çözümü + fail-closed politika `Services/CycleCapacityEstimator.cs`'e çıkarıldı ve
**hem** kayıtlı `/calculation` **hem** `/calculation-preview` onu kullanır. Sebep: politikanın iki kopyası, yazarken
gösterilen sayının kaydedilen kaydın söylediğinden sapması demekti. `CycleCapacityCalculator` **saf ve
değiştirilmemiş** kaldı; HTTP cevabı şekillendirme `Services/CycleCapacityCalculationResponse.cs`'e alındı.

**Gateway:** yeni route **gerekmedi** — `/api/crm/cycle-capacities/{everything}` zaten `POST` içeriyor.

**Bu artışta bulunan ve düzeltilen 2 gerçek kusur (testler yakaladı):**

| # | Kusur | Düzeltme |
|---|---|---|
| **B1** | Transient kapasite `EntityBase.Id = Guid.NewGuid()` initializer'ı yüzünden **rastgele bir id** taşıyor ve preview cevabında görünüyordu — kaydedilmiş bir kayıt sanılabilirdi | `Id = Guid.Empty` açıkça set edildi (T44) |
| **B2** | Ülke kapsamlı dönemde preview, hesabı **DE** takvimiyle yapıp ekranda çağıranın gönderdiği **TR**'yi gösteriyordu | `CycleCapacityEstimator.Result` artık **çözümlenmiş** ülkeyi döner; preview handler onu transient kayda basar (T49). Kayıtlı yol etkilenmedi — saklı kodu zaten türetilmiş olan |

**Doğrulama:** backend build **0 hata** · frontend build **0 hata** · `--filter CycleCapacity` **49/49** ·
tam suite **1350 geçti / 5 skip / 0 fail** · verifier CycleCapacities ≡ CyclePeriods (**aynı 8 FAIL**) ·
7 dil RESX **70 anahtar × 7** · `verify_module_id --check-all` **HARD violations: 0**.

---

## 0. Kimlik Geçidi ve Ev Kararı

### 0.1 DCP-002 — PASS (2026-08-28)

```text
$ py .antigravity/scripts/verify_module_id.py . --check-id MOD-0155-FU06 --name "Cycle Capacity" --parent MOD-0155
OK  MOD-0155-FU06: proven against Blueprint/registry.
REAL_EXIT=0
```

**Geçidin fail-closed olduğu ayrıca kanıtlandı** (kontrol koşusu — sahte parent):

```text
$ py .antigravity/scripts/verify_module_id.py . --check-id MOD-9999-FU06 --name "X" --parent MOD-9999
BLOCKED  MOD-9999-FU06
   - parent MOD-9999 not found in Blueprint or registry
   - MOD-9999 not in Blueprint and --repo-only not set — cannot prove ID
Gate failed closed. See DCP-002.
REAL_EXIT=2
```

> **Geçidin kapsamı hakkında dürüst not.** Geçit **kimliği** doğrular (parent'ın Blueprint/registry'de varlığı +
> FU id'sinin boş olması + registry çakışması), FU'nun açıklayıcı **adını doğrulamaz**. DCP-002 madde 3 (kanonik ad)
> Blueprint'teki **parent** için geçerlidir; parent'ın kanonik adı **"Field Sales / Visit Planning"**'dir
> (`module-id-registry.md` satır 227, `reserved / planned`) ve değişmez. Frontmatter'daki `name` repo-tarafı bir
> açıklayıcıdır.

**FU numarası gerekçesi (D-FU).** Parent `MOD-0155` altında kullanımda olan id'ler: FU01 (pack mevcut, draft),
FU02/FU03/FU04/FU05 (FU01 frontmatter'ında **sibling olarak rezerve**, henüz pack yok).
`grep -rno "MOD-0155-FU[0-9A-Z]*" execution/ docs/` → FU01, FU02, FU03, FU04, FU05. İlk çakışmayan id **FU06**'dır.
**Registry satırı bu pack tarafından EKLENMEZ** (MOD-0165-FU01/FU08 emsali; registry yazımı pack yetkisi dışıdır) →
§20 / F-REGISTRY.

### 0.2 D-HOME — Ev **MOD-0155**'tir, MOD-0165 değil (kanıtla)

Görev girdisi evi "author seçsin" diye açık bıraktı. Seçim **tahmine değil, üç yazılı kayda** dayanıyor:

| # | Kayıt | Ne diyor |
|---|---|---|
| **1** | **`MOD-0165-FU06` §20 / F-CALENDAR-DAYS** (satır 955) | *"«Bu dönemde kaç çalışma günü var?» — CAND-CAP-0008 `IWorkingCalendarProvider` × CyclePeriod **MOD-0155'te**"* — CyclePeriod'un **kendi** pack'i bu işi adıyla MOD-0155'e havale etmiştir. Bu FU o follow-up'ı **kapatır**. |
| **2** | **`crm-sor-boundary.md`** | *"Visit Plan / MicroTarget / Visit / Visit Report / route plan → **MOD-0155**"*. "Bu dönemde kaç ziyaret yapılabilir" bir **ziyaret planlama** ifadesidir; kampanya yürütme ifadesi değildir. |
| **3** | **`CyclePeriod.cs` sınıf yorumu** (satır 5–9) | *"It is **not** a working calendar … **nothing here counts working days**."* CyclePeriod aggregate'i bu sorumluluğu kod seviyesinde reddeder. |

**Legacy karşı-argümanı ve neden reddedildi.** Legacy'de `CyclePeriodCalendar` **Campaign** servisindeydi. Bu bir
**paketleme kazasıdır, SoR değil**: legacy'de cycle, kampanya ve kapasite tek serviste yaşıyordu; vNext o üçünü zaten
ayırdı (Campaign = MOD-0165-FU04, CyclePeriod = MOD-0165-FU06/FU07). Legacy'nin klasör düzenini SoR delili saymak, bu
ayrımı geri almak olurdu.

**Sonuç:** aggregate **MOD-0155**'e aittir, **`Diten.CrmService`** içinde yaşar (CyclePeriod ile aynı servis — ayrı
servis değil), ve CyclePeriod'u **salt-okunur** tüketir.

---

## 1. Module Summary

### 1.1 Ne yapar

`CycleCapacity`, bir **CyclePeriod**'a çakılı (pinned) tek bir kapasite modelidir. İçinde iki tür girdi vardır:

1. **Dönem-geneli aktivite dakika bütçesi** — bir saha gününün 8 saatinin nereye gittiği: promo ürün konuşma süresi,
   non-promo ürün süresi, yol, raporlama, quiz.
2. **Ay bazında çalışma-günü düşümleri** — o ay kaç gün toplantı, eğitim, izin ve mikro-hedefleme ile geçiyor.

Bunlardan, **ay başına** bir sayı üretir: `TotalVisitNumber` — *"bu ay saha ekibi kaç ziyaret yapabilir"*.

### 1.2 Hedef kullanıcı

Saha satış yöneticisi (kapasiteyi kuran), CRM admin (dönem yapısını kuran), ve — **gelecekte** — MOD-0155-FU05
MicroTarget bu sayıyı bir **taban** olarak tüketir. Bu FU MicroTarget satırı **üretmez**.

### 1.3 Kapasite özeti

`CycleCapacity` CRUD-minus-delete + archive · CyclePeriod'a 1:1 pin · açık ay modeli (embedded satırlar) ·
Working Calendar'dan **fail-closed** salt-okunur çalışma-günü okuması · **saf** (I/O'suz) hesap motoru ·
**read-time projeksiyon** olarak `TotalVisitNumber` (asla persist edilmez) · read-only contract yüzeyi ·
tek Compact yönetim konsolu · CyclePeriod Index'ine **tek** satır-aksiyon nav-link'i.

### 1.4 Bu FU bir MOTOR DEĞİLDİR

Plan **üretmez**, ziyaret **dağıtmaz**, temsilci **atamaz**, rota **çıkarmaz**, MicroTarget satırı **yazmaz**,
frekans politikası **yazmaz**, kampanya **bağlamaz**, çalışma takvimi **yazmaz**, tatil **tanımlamaz**.
Girdi alır, aritmetik yapar, sayı **gösterir**.

> **⚠️ TAHMİN BEYANI (D-ESTIMATE).** Üretilen sayı bir **planlama tahminidir**. FTE gerçek bir HR kaydından değil,
> config'lenebilir bir **ortalama sabitten** gelir (§1.5/D-FTE); aktivite süreleri elle girilen ortalamalardır;
> çalışma günü sayısı yayınlanmış takvime dayanır ama takvim sonradan değişebilir. Bu beyan **üç yerde** görünür:
> ekranda kalıcı bir uyarı bandı (AC-UI-6), `Details` sayfasında, ve contract'ta `IsEstimate: true` bayrağı olarak.
> Sayı bir **kota** veya **taahhüt** olarak sunulmaz.

### 1.5 Neden ayrı bir aggregate (ve neden CyclePeriod'a alan eklenmedi)

Kapasite, dönemin bir **özelliği değildir**. Aynı dönem için kapasite modeli yeniden kurulabilir, boş bırakılabilir,
ya da hiç yazılmayabilir — dönem yine geçerli bir dönemdir. Ayrıca kapasite **aylık satırlar** taşır; bunları
CyclePeriod'a gömmek, kendi pack'inde *"a period is a period master and nothing else"* diye tanımlanmış bir
aggregate'i ikinci bir sorumluluğa açardı.

**Somut kanıt:** `CyclePeriodFeatureFlags` bugün `SupportsWorkingCalendarIntegration: false` ve
`SupportsWorkingDayCount: false` der. Bu iki bayrak **bu FU'dan sonra da `false` kalır** — çünkü entegrasyonu yapan
CyclePeriod değil, `CycleCapacity`'dir. Bayrakların değişmediği bir testle kilitlenir (AC-V-2).

---

## 2. Ownership and Boundaries

### 2.1 In-scope / Out-of-scope

| Kapsam | Karar |
|---|---|
| **In-scope** | `CycleCapacity` aggregate (+ embedded `CycleCapacityMonth` satırları) · repository · CQRS · persistence · **saf** `CycleCapacityCalculator` · `IWorkingDayCounter` salt-okunur HTTP seam · contract yüzeyi · 8 API endpoint · CRM → Field Sales → Cycle Capacity Compact konsolu · CyclePeriod Index'ine **1 (bir)** satır-aksiyon nav-link'i · 7 dil RESX · yeni Ocelot route (integration-agent task'ı) |
| **Out-of-scope (AÇIKÇA ERTELENİR)** | MicroTarget üretimi (**FU05**) · ziyaretin temsilciye/hesaba dağıtılması · rota (**FU03**) · onay/approval iş akışı (**F-APPROVAL**) · gerçek HR FTE entegrasyonu (**F-FTE-HR**) · BU bazında FTE granülerliği (**F-FTE-BU**) · kapasite ↔ gerçekleşen karşılaştırması (**F-ACTUALS**) · senaryo/versiyon karşılaştırması (**F-SCENARIO**) · Working Calendar'a toplu ay endpoint'i eklenmesi (**F-WC-BULK** — `services/Diten.Platform/**` protected) |

### 2.2 SoR sınırı — sahiplenilen vs. yalnız tüketilen

| Nesne | Sahip | Bu FU'da |
|---|---|---|
| `CycleCapacity` + `CycleCapacityMonth` | **MOD-0155** | **AÇILIR** — bu FU'nun tek aggregate'i |
| `CyclePeriod` (dönem kimliği, pencere, scope, lifecycle) | MOD-0165-FU06/FU07 | **SALT-OKUNUR** — `ICyclePeriodReader` üzerinden, tek satır bile yazılmaz |
| `WorkingCalendar` (gün niteliği, tatil, hafta sonu) | CAND-CAP-0008 (PSS) | **SALT-OKUNUR** — HTTP resolve seam'i; ne yazılır ne kopyalanır |
| `MicroTarget` (dönem × temsilci × hedef planı) | MOD-0155-FU05 | **AÇILMAZ** — bu FU ona **taban** hazırlar, satır üretmez |
| `PlannedVisit` | MOD-0155-FU01 | **AÇILMAZ** |
| `Campaign` / `CampaignTarget` | MOD-0165-FU04/FU08 | **DOKUNULMAZ** |
| `VisitFrequencyPolicy` | MOD-0165-FU03 | **DOKUNULMAZ** — "kaç ziyaret yapılabilir" ≠ "kaç ziyaret yapılmalı" |
| `StrategyTemplate` / `Segment` | MOD-0167 | **DOKUNULMAZ** |
| Employee / FTE master | MOD-0288 / HR (yok) | **TÜKETİLMEZ** — INTERIM sabit (§1.5/D-FTE) |
| `countries` reference set | MOD-0048 | **SALT-OKUNUR** lookup |

### 2.3 Komşu kavramlarla tek cümlelik sınır

> **Working Calendar** günün **niteliğini** söyler · **CyclePeriod** dönemin **kimliğini** söyler ·
> **CycleCapacity** o dönemde **kaç ziyaret sığdığını** söyler · **VisitFrequencyPolicy** kaç ziyaret
> **yapılması gerektiğini** söyler · **MicroTarget** o ziyaretleri **kime dağıttığını** söyler.
> Beşi ayrı sorudur ve bu FU yalnız üçüncüsünü açar.

### 2.4 CyclePeriod'a yapılan TEK dokunuş — ve neden sınır ihlali değil

Bu FU, CyclePeriod'un **frontend Index sayfasına** bir satır-aksiyonu ekler:

```
frontend/Diten.Web/wwwroot/assets/js/CRM/CyclePeriods/index.js   → actions() içine 1 (bir) menü öğesi
frontend/Diten.Web/Views/CRM/CyclePeriods/_IndexL10n.cshtml      → 1 (bir) L10n anahtarı
frontend/Diten.Web/Resources/Views/CRM/CyclePeriods/*.resx        → 1 (bir) anahtar × 7 dil
```

**Ne DEĞİŞMEZ (kilitli):**

- `services/Diten.CrmService/**/Features/CyclePeriod/**` — **tek dosya bile** (contract, flags, entity, handler, repo, validator, reader)
- `CyclePeriodFeatureFlags.Current` — 27 bayrağın **hiçbiri**; `SupportsWorkingCalendarIntegration` ve `SupportsWorkingDayCount` **`false` kalır**
- `CyclePeriodsController` (backend **ve** frontend proxy) — yeni endpoint yok, mevcut endpoint imzası değişmez
- `Views/CRM/CyclePeriods/*.cshtml` — `_IndexL10n.cshtml` dışında hiçbiri; `_DataTable.cshtml` kolon eklemez
- `CyclePeriod` DTO/ViewModel şekli — kapasite alanı **eklenmez**

> **Neden bu bir boundary ihlali değil:** eklenen şey bir **link**tir. Veri modeli, sözleşme, doğrulama, yetki ve
> yaşam döngüsü açısından CyclePeriod'un bildiği hiçbir şey değişmez — CyclePeriod, `CycleCapacity`'nin **var
> olduğunu bilmez**. Bağ tek yönlüdür: kapasite döneme işaret eder, dönem kapasiteyi tanımaz. Bu, MOD-0165-FU08'in
> Campaign→CyclePeriod pin deseninin aynısıdır, bir yön daha zayıfı.

---

## 3. Owned Objects

### 3.1 Domain

| Nesne | Dosya | Not |
|---|---|---|
| `CycleCapacity` | `Domain/Entities/CycleCapacity.cs` | `EntityBase`; tenant-owned; soft delete |
| `CycleCapacityMonth` | aynı dosya (embedded tip) | Ay satırı; **pozisyonel dizi DEĞİL** |
| `CycleCapacityStatuses` | aynı dosya | Yaşam döngüsü yok → bkz. §3.6/D-LIFECYCLE; yalnız `IsArchived` |
| `CycleCapacityReasonCodes` | aynı dosya | `capacity_ok` · `calendar_unresolved` · `country_underivable` · `month_out_of_period` · `duplicate_capacity` · `visit_minutes_zero` · `period_closed` |
| `ICycleCapacityRepository` | `Domain/Repositories/ICycleCapacityRepository.cs` | — |

### 3.2 Application

| Katman | Dosyalar |
|---|---|
| Commands | `CreateCycleCapacityCommand` · `UpdateCycleCapacityCommand` · `ArchiveCycleCapacityCommand` |
| Queries | `GetCycleCapacityListQuery` · `GetCycleCapacityByIdQuery` · `GetCycleCapacityByCyclePeriodQuery` · `GetCycleCapacityContractQuery` |
| CommandHandlers | `CreateCycleCapacityHandler` · `UpdateCycleCapacityHandler` · `ArchiveCycleCapacityHandler` |
| QueryHandlers | `GetCycleCapacityListHandler` · `GetCycleCapacityByIdHandler` · `GetCycleCapacityByCyclePeriodHandler` · `GetCycleCapacityContractHandler` |
| Validators | `CreateCycleCapacityValidator` · `UpdateCycleCapacityValidator` |
| Rules (saf) | `CycleCapacityCalculator` — **I/O yok**, `HttpClient` yok, repository yok; girdi + çalışma günü sayıları → sonuç |
| Rules (saf) | `CycleCapacityMonthRules` — dönem penceresinden ay listesi türetimi + ay↔dönem kesişimi |
| Services | `ICycleCapacityCountryResolver` — dönem scope'undan takvim ülkesi türetimi (saf, I/O yok) |
| Read seam | `IWorkingDayCounter` (arayüz, Application) |
| Contract | `CycleCapacityContract` · `CycleCapacityFeatureFlags` |
| Models | `CycleCapacityModels.cs` (tüm DTO'lar tek dosyada) + `CycleCapacityMapper.cs` |
| Permissions | `CycleCapacityPermissions.cs` (**tanım — hiçbir şey seed etmez**) |

### 3.3 Infrastructure

| Nesne | Dosya | Not |
|---|---|---|
| `WorkingCalendarWorkingDayCounter` | `Infrastructure/CycleCapacity/WorkingCalendarWorkingDayCounter.cs` | `IWorkingDayCounter`'ın tek implementasyonu; Gateway üzerinden HTTP; **cache YOK** |
| `CycleCapacityRepository` | `Infrastructure/Persistence/Repositories/CycleCapacityRepository.cs` | — |

### 3.4 API endpoints (`Diten.CrmService.Api`)

| Method | Route | Permission |
|---|---|---|
| GET | `/api/crm/cycle-capacities/contract` | `crm.cycle-capacity.read` |
| GET | `/api/crm/cycle-capacities` | `crm.cycle-capacity.read` |
| GET | `/api/crm/cycle-capacities/{id:guid}` | `crm.cycle-capacity.read` |
| GET | `/api/crm/cycle-capacities/by-cycle-period/{cyclePeriodId:guid}` | `crm.cycle-capacity.read` |
| GET | `/api/crm/cycle-capacities/{id:guid}/calculation` | `crm.cycle-capacity.read` |
| POST | `/api/crm/cycle-capacities` | `crm.cycle-capacity.manage` |
| PUT | `/api/crm/cycle-capacities/{id:guid}` | `crm.cycle-capacity.manage` |
| POST | `/api/crm/cycle-capacities/{id:guid}/archive` | `crm.cycle-capacity.manage` |

> **`/calculation` neden ayrı bir endpoint:** hesap **read-time**'dır ve Working Calendar'a HTTP gider (§8.3).
> Onu liste yanıtına gömmek, bir grid çizimini N tane dış servis çağrısına bağlardı. Liste **girdileri** döner,
> hesap **talep üzerine** yapılır.

### 3.5 Frontend routes

| Route | Davranış |
|---|---|
| `GET /CRM/CycleCapacity` | Index (DataTable — tenant'ın tüm kapasiteleri) |
| `GET /CRM/CycleCapacity?cyclePeriodId={guid}` | **Deep-link çözücü** — kapasite varsa `Details/{id}`'ye, yoksa `Create?cyclePeriodId={guid}`'ye **redirect**. CyclePeriod satır-aksiyonunun hedefi budur (§2.4). |
| `GET/POST /CRM/CycleCapacity/Create` | Compact create sayfası (`?cyclePeriodId` ile ön-doldurulur) |
| `GET/POST /CRM/CycleCapacity/Edit/{id:guid}` | Compact edit sayfası |
| `GET /CRM/CycleCapacity/Details/{id:guid}` | Compact detay + hesaplanan ay tablosu + tahmin bandı |
| `GET /CRM/CycleCapacity/api/**` | Same-origin proxy (browser servis portuna **gitmez**) |

### 3.6 D-LIFECYCLE — kendi yaşam döngüsü **YOKTUR**

Görev girdisi *"lifecycle (draft/saved — approval future mı?)"* diye sordu. **Karar: kendi durum alanı yok.**

Bir kapasite kaydı ya vardır ya yoktur; kaydedildiği an geçerlidir. Düzenlenebilirliği **pinlediği dönemin**
durumundan türer:

| Pinlenen `CyclePeriod.CycleStatus` | `CycleCapacity` |
|---|---|
| `draft` · `active` | Düzenlenebilir |
| `closed` | **Salt-okunur** — yazma denemesi `409 period_closed` |

**Gerekçe:** ikinci bir durum makinesi (`draft`/`saved`) ikinci bir doğruluk kaynağı olurdu ve "kaydedilmiş ama
dönemi kapalı" ile "taslak ama dönemi aktif" gibi kimsenin karar veremediği dört hücreli bir matris üretirdi.
`closed`'ın terminal ve geri dönüşsüz olduğu zaten CyclePeriod'da kilitlidir; kapasitenin donması o gerçeğin
**türevidir**, yeni bir kural değil.

**Approval iş akışı → `F-APPROVAL`** (§20). Bugün açılmaz: onaylanacak bir *taahhüt* yok, bir *tahmin* var (§1.4).

---

## 4. Entity Fields

### 4.1 `CycleCapacity` (aggregate root, `EntityBase`)

| # | Alan | Tip | Zorunlu | Kural | Index |
|---|---|---|---|---|---|
| 1 | `Id` | `Guid` | sistem | — | `_id` |
| 2 | `TenantId` | `Guid` | sistem | Payload'dan **asla** alınmaz | bileşik |
| 3 | `CyclePeriodId` | `Guid` | **Evet** | **PIN** — oluşturmadan sonra **değişmez**; `ICyclePeriodReader.GetByIdAsync` ile kanıtlanır | **partial unique** `(TenantId, CyclePeriodId)` where `IsDeleted:false` |
| 4 | `CalendarCountryCode` | `string(2)` | **Evet** | ISO alpha-2, upper-case, MOD-0048 `countries` setinde yayınlı olmalı. **Scope DEĞİL** — yalnız takvim sorgu parametresi (§4.4/D-COUNTRY) | — |
| 5 | `DailyWorkMinutes` | `int` | **Evet** | 1–1440. Default `480` (8sa×60) config'ten; **magic constant değil**, alan olarak saklanır | — |
| 6 | `PromoProductTime` | `int` | **Evet** | 0–480 dk/ziyaret | — |
| 7 | `NonPromoProductTime` | `int` | **Evet** | 0–480 dk/ziyaret. `PromoProductTime + NonPromoProductTime > 0` (çift-alan kuralı) | — |
| 8 | `TravelingTime` | `int` | **Evet** | 0–1440 dk/gün | — |
| 9 | `ReportDuration` | `int` | **Evet** | 0–1440 dk/gün | — |
| 10 | `QuizDuration` | `int` | **Evet** | 0–1440 dk/gün | — |
| 11 | `Fte` | `decimal(6,2)` | **Evet** | > 0, ≤ 9999. **UI'da DISABLED**, config default'undan doldurulur, **saklanır** (§4.3/D-FTE) | — |
| 12 | `FteSource` | `string` | sistem | `interim-default` \| `authored` (v1'de daima `interim-default`) — provenance, kimlik değil | — |
| 13 | `Description` | `string?` | Hayır | max 1000, trim | — |
| 14 | `Months` | `List<CycleCapacityMonth>` | **Evet** | ≥ 1 satır; **açık ay modeli** (§4.2) | — |
| 15 | `IsArchived` | `bool` | sistem | Archive = soft, geri alınabilir değil | bileşik |
| 16 | `Version` | `int` | sistem | Optimistic concurrency (dirty-check) | — |
| 17 | `CreatedBy` / `UpdatedBy` | `string?` | sistem | — | — |
| 18 | `IsDeleted` / `DeletedAt` / `CreatedAt` / `UpdatedAt` | — | sistem | Global standart | — |

**Form alan sayımı (`form_field_count: 10`):** 3, 4, 5, 6, 7, 8, 9, 10, 11, 13. Sayılmayanlar: `Id`, `TenantId`,
audit/soft-delete/version alanları, sistem-set `FteSource`, ve `Months` (embedded **child grid** — MOD-0162-FU04
`KnowledgePath` embedded-steps emsali; grid form alanı olarak sayılmaz).
**10 > 8 ⇒ `golden_reference: compact`.**

### 4.2 `CycleCapacityMonth` (embedded) — **açık ay modeli**

| Alan | Tip | Zorunlu | Kural |
|---|---|---|---|
| `Year` | `int` | Evet | Ait olduğu takvim yılı — **satırda açıkça yazılı** |
| `MonthNumber` | `int` | Evet | 1–12 — **satırda açıkça yazılı** |
| `MeetingDays` | `int` | Evet | ≥ 0 |
| `TrainingDays` | `int` | Evet | ≥ 0 |
| `VacationDays` | `int` | Evet | ≥ 0 |
| `MicroTargetingDayCount` | `int` | Evet | ≥ 0 (o ay kaç gün mikro-hedefleme yapılıyor) |
| `MicroTargetingDuration` | `int` | Evet | 0–1440 dk (mikro-hedefleme gününde günde kaç dakika) |

**Kurallar:**

- `(Year, MonthNumber)` çifti aggregate içinde **benzersiz**.
- Her satır, pinlenen dönemin `[StartDate, EndDate]` penceresiyle **kesişmek zorundadır** — kesişmeyen satır
  `400 month_out_of_period`.
- Satır listesi, dönem penceresinden **türetilerek ön-doldurulur** (kullanıcı silmez/ekler; dönem penceresi ne
  diyorsa o kadar satır vardır). Dönem penceresi değişemez (`SupportsCycleReschedule: false`), dolayısıyla satır
  kümesi de kararlıdır.
- **Sıralama `Year` + `MonthNumber` ile yapılır, dizi indeksiyle DEĞİL.**

> **D-MONTH — legacy'den DÜŞÜRÜLENLER (açıkça).** Legacy `CyclePeriodCalendar`, 12 elemanlı **pozisyonel diziler**
> (`Month1..Month12` ya da indeksle adreslenen kolonlar), sihirli bir `RowId` ve `OldSystem` alanları taşıyordu.
> Üçü de **DÜŞÜRÜLDÜ**:
> - **Pozisyonel dizi yok** — her ay kendi `Year`+`MonthNumber`'ını taşır, böylece yıl sınırını aşan bir dönem
>   (Ara 2026 – Oca 2027) doğal olarak temsil edilir. Pozisyonel dizide bu imkânsızdır.
> - **Sihirli `RowId` yok** — kimlik `(Year, MonthNumber)`'dır.
> - **`OldSystem` coupling yok** — legacy sisteme hiçbir alan, hiçbir kolon, hiçbir kod referansı taşınmaz.

### 4.3 D-FTE — INTERIM: ortalama, disabled, **ama saklanan**

| Boyut | Karar |
|---|---|
| **Kaynak** | `CycleCapacity:DefaultFte` config değeri (ör. `12.00`). **Hardcoded sabit değil**, ops ayarı |
| **UI** | Alan **görünür ama `disabled`**; yanında *"Bu değer geçici bir ortalamadır; HR entegrasyonu geldiğinde iş birimi ve yıl bazında otomatik gelecektir"* yardım metni (7 dil) |
| **Persistence** | Değer **SAKLANIR** — config yarın değişse bile eski kayıt aynı sayıyı üretir (**reproducibility**) |
| **Provenance** | `FteSource = "interim-default"` |
| **Doğrulama** | Sunucu tarafı payload'daki `Fte`'yi **yok sayar** ve create anında config'ten yazar. Disabled bir alanı DOM'dan açıp değer göndermek işe yaramaz |
| **Gelecek** | `F-FTE-HR` (gerçek HR kaynağı) + `F-FTE-BU` (legacy'nin per-BusinessUnit granülerliği) |

> Legacy, FTE'yi `(BusinessUnit, Year)` bazında tutuyordu. Bu granülerlik **kaybedilmiş değil, ertelenmiştir** ve
> `F-FTE-BU` olarak kayıtlıdır. Bugün taklit edilmemesinin nedeni, arkasında besleyecek bir HR master'ın **olmaması**;
> uydurulmuş bir per-BU tablosu, gerçek görünen sahte bir granülerlik üretirdi.

### 4.4 D-COUNTRY — ⚠️ **KULLANICI KARARI GEREKTİRİR**

**Bulgu (koddan).** `CyclePeriod.ScopeType` **ayrıştırılmış (discriminated)** bir adrestir: `tenant` · `country` ·
`legal-entity` · `business-unit` — ve **tam olarak biri** doludur. Working Calendar ise **her zaman bir
`countryCode` ister**. İkisi şöyle örtüşür:

| `CyclePeriod.ScopeType` | Ülke türetilebilir mi? | Nasıl |
|---|---|---|
| `country` | ✅ **Evet** | `CountryScope` doğrudan |
| `legal-entity` | ⚠️ Dolaylı | MDM'e **ikinci bir cross-service çağrı** gerekir (LE'nin ülkesi) |
| `business-unit` | ⚠️ Güvenilmez | `BusinessUnitCountryContext` var **ama** kendi kod yorumunda *"documentation, never identity"* ve **eski satırlarda null** |
| `tenant` | ❌ **Hayır** | Türetilecek hiçbir şey yok |

**Sonuç:** "scope cycle'dan okunur, kendi alanı yok" kilitli kararı **olduğu gibi** uygulanırsa, **tenant kapsamlı
ve legal-entity kapsamlı dönemler için kapasite hiç hesaplanamaz** ve business-unit kapsamlılar yalnız
`BusinessUnitCountryContext` doluysa hesaplanır. Tenant kapsamı, en yaygın kullanım olan varsayılan durumdur.

**İki seçenek:**

| | **A — Katı türetim** | **B — Takvim parametresi (ÖNERİLEN)** |
|---|---|---|
| Model | `CalendarCountryCode` alanı **yok** | `CalendarCountryCode` alanı **var** (§4.1 #4) |
| Davranış | Türetilemiyorsa `422 country_underivable`, kapasite kurulamaz | Türetilebiliyorsa **ön-doldurulur ve read-only**; türetilemiyorsa kullanıcı yayınlanmış `countries` setinden **seçer** |
| Kapsam | `tenant` ve `legal-entity` dönemleri **kullanılamaz** | Tüm dönemler kullanılabilir |
| Kilitli karara uyum | Tam | **Uyumlu sayılıyor:** bu alan bir **scope değildir** — kimliğe girmez, benzersizliğe girmez, önceliğe girmez, çözümlemeye girmez. Yalnızca WC'nin zaten talep ettiği `countryCode` sorgu parametresidir. Kapasitenin *adresi* hâlâ tamamen dönemden okunur |

**Pack'in önerisi: B.** Bu pack **B**'ye göre yazılmıştır (§4.1 #4, §12, §13). Kullanıcı **A**'yı tercih ederse
değişiklik cerrahidir: §4.1 satır 4 silinir, `country_underivable` bir doğrulama hatasından bir çalıştırma
sonucuna döner, `form_field_count` 10 → 9 olur (**`compact` kararı değişmez**, 9 > 8).

**Her iki seçenekte de geçerli olan iki kural:**

- Dönem `legal-entity` kapsamlıysa, `CyclePeriod.LegalEntityId` WC'nin `legalEntityId` parametresine **doğrudan
  geçirilir** — bedavaya gelen bir kesinlik, ek çağrı yok.
- **`BusinessUnitId` → `organizationUnitId` eşlemesi YAPILMAZ.** `CyclePeriod.BusinessUnitId` bir MOD-0048
  **değer kodu** (`string`), WC'nin `organizationUnitId` parametresi ise bir **`Guid`** org-unit referansıdır.
  Bunlar farklı şeylerdir; bir string'i Guid'e zorlamak sessizce yanlış takvim seçerdi. Dolayısıyla
  **iş birimi kapsamlı bir dönem, çalışma takvimini daraltmaz** → `F-WC-ORG-UNIT`.

---

## 5. Repo Scope

```text
services/Diten.CrmService/src/Diten.CrmService.Domain/
├── Entities/CycleCapacity.cs                                       (YENİ)
└── Repositories/ICycleCapacityRepository.cs                        (YENİ)

services/Diten.CrmService/src/Diten.CrmService.Application/Features/CycleCapacity/   (YENİ klasör — tamamı)
services/Diten.CrmService/src/Diten.CrmService.Infrastructure/CycleCapacity/         (YENİ klasör)
services/Diten.CrmService/src/Diten.CrmService.Infrastructure/Persistence/
├── Repositories/CycleCapacityRepository.cs                         (YENİ)
└── DependencyInjection.cs                                          (DEĞİŞİR — class-map + index + DI + HttpClient)

services/Diten.CrmService/src/Diten.CrmService.Api/
├── Controllers/CRM/CycleCapacitiesController.cs                    (YENİ)
├── Controllers/CRM/CycleCapacityContractController.cs              (YENİ)
└── Models/CRM/CycleCapacityRequests.cs                             (YENİ)

services/Diten.CrmService/tests/**/CycleCapacity*Tests.cs           (YENİ)

frontend/Diten.Web/
├── Controllers/CRM/CycleCapacityController.cs                      (YENİ)
├── Models/CRM/CycleCapacityViewModels.cs                           (YENİ)
├── Models/CRM/CycleCapacityCalculationViewModel.cs                 (YENİ — AYRI DOSYA, bkz. §19.3)
├── Views/CRM/CycleCapacity/**                                      (YENİ — §11 dosya seti)
├── wwwroot/assets/js/CRM/CycleCapacity/{index.js, form.js, index.l10n.js}   (YENİ)
├── Resources/Views/CRM/CycleCapacity/CycleCapacityIndex.{7 dil}.resx        (YENİ)
├── Resources/SharedResource.{7 dil}.resx                           (DEĞİŞİR — yeni anahtarlar)
└── Views/Shared/_LayoutTenantShell.cshtml                          (DEĞİŞİR — permission-guard'lı 1 `<li>`)

── CyclePeriod nav-link (ADDITIVE, yalnız frontend — §2.4) ──
frontend/Diten.Web/wwwroot/assets/js/CRM/CyclePeriods/index.js      (DEĞİŞİR — actions() içine 1 öğe)
frontend/Diten.Web/Views/CRM/CyclePeriods/_IndexL10n.cshtml         (DEĞİŞİR — 1 anahtar)
frontend/Diten.Web/Resources/Views/CRM/CyclePeriods/*.resx          (DEĞİŞİR — 1 anahtar × 7 dil)
```

---

## 6. Protected Paths

| Path | Neden |
|---|---|
| `.antigravity/**` | Global engineering system |
| **`services/Diten.Platform/**`** | **Başka domain'in servisi** (domain-config Protected Paths). Working Calendar'a toplu-ay endpoint'i **eklenemez** — bu kısıt §8.3'teki N-çağrı tasarımının **nedenidir** → `F-WC-BULK` |
| **`services/Diten.CrmService/**/Features/CyclePeriod/**`** | **Bu FU'nun en sert kilidi.** Contract, flags, entity, handler, repository, reader, validator — **tek satır bile** değişmez (§2.4) |
| `services/Diten.CrmService/**/Features/{Campaign,VisitFrequencyPolicy,Segmentation,StrategyTemplate,Territory,Account,Contact}/**` | Komşu FU'ların aggregate'leri |
| `services/Diten.MdmService/**`, `services/Diten.AuthService/**`, `services/Diten.HcmService/**` | Başka domain servisleri |
| `gateway/**/ocelot.json` | Yalnız `integration-agent` (§15) |
| `frontend/Diten.Web/Views/Shared/_Layout.cshtml` | FROZEN |
| `frontend/Diten.Web/{Controllers,Views}/Archive/**` | FROZEN — legacy taşınmaz |
| `frontend/Diten.Web/Views/CRM/CyclePeriods/*.cshtml` | `_IndexL10n.cshtml` **hariç** hiçbiri (§2.4) |
| `execution/registries/**` | Registry yazımı pack yetkisi dışı → `F-REGISTRY` |

---

## 7. Dependencies

| Bağımlılık | Yön | Durum | Not |
|---|---|---|---|
| **MOD-0165-FU06/FU07** `ICyclePeriodReader` | salt-okunur, **in-process** | SHIPPED | `GetByIdAsync` (status-agnostik) + `ListByYearAsync`. İmza **değişmez**; yeni metot **eklenmez** |
| **CAND-CAP-0008** Working Calendar | salt-okunur, **HTTP** | SHIPPED | `GET /api/platform/working-calendars/overrides/resolve?op=working-days-between` (§8.3) |
| **MOD-0048** `countries` | salt-okunur lookup | mevcut | `CalendarCountryCode` doğrulaması; **hardcoded fallback liste YASAK** |
| **MOD-0018** RBAC | tüketim | kısmi | `crm.cycle-capacity.*` katalogda **yok** → dev-only fallback (§14) + `F-RBAC`. `platform.working-calendar.override.read` CRM kullanıcısına gerekli → **`F-RBAC-WC`** |
| **MOD-0155-FU05** MicroTarget | ileri bağ | yapılmadı | Bu FU'nun **tüketicisi**; bu FU ona bir şey yazmaz |
| **MOD-0288** / HR | **yok** | yok | FTE INTERIM (§4.3) |
| **Gateway** (Ocelot) | **YENİ route gerekli** | eksik | `/api/crm/cycle-capacities` yok (§15) |
| **DEV-0001** Golden Compact | şablon | mevcut | §10/§11 birebir |

---

## 8. Runtime Constraints

### 8.1 Genel

- **Persistence:** MongoDB tek instance, `TenantId` zorunlu, cross-tenant **404**. Soft delete (`IsDeleted`/`DeletedAt`).
- **Mimari:** 5 katman + CQRS (MediatR) + 4 zorunlu pipeline behavior + `Response<T>` envelope + `CustomBaseController`.
- **Frontend:** browser **asla** servis portuna gitmez; same-origin MVC proxy → Gateway (5000).
- **Class-map ZORUNLU:** `CycleCapacity` (ve embedded `CycleCapacityMonth`) `RegisterClassMaps`'e **eklenmelidir**.
  Aksi hâlde `CyclePeriodId` binary yazılır, filtre string serialize eder ve sorgular **sessizce boş döner**
  (belgelenmiş CRM tuzağı; AC-B-6'da testle kilitlenir).
- **DateTimeOffset tuzağı:** Dönem penceresi `DateTimeOffset`'tir. Ay kesişimi hesabında **instant değil `.Date`**
  karşılaştırılır; iki `DateTimeOffset` alanı **birlikte index'lenmez ve birlikte sort edilmez** (parallel-arrays 500).

### 8.2 Hesap motoru **saftır**

`CycleCapacityCalculator`, `HttpClient`, repository, `ITenantContext` veya `DateTime.UtcNow` **almaz**. Girdi olarak
kapasite kaydını ve **önceden çözülmüş** ay-başına çalışma günü sayılarını alır, sonuç döner. Sebep: aritmetik,
takvim servisinin ayakta olup olmamasından bağımsız olarak test edilebilir olmalıdır.

### 8.3 Working Calendar tüketimi — **fail-closed**, HTTP, cache'siz

**Seçilen uç nokta (kanıtla seçildi):**

```
GET {gateway}/api/platform/working-calendars/overrides/resolve
    ?op=working-days-between&date={ayBaşı}&toDate={aySonu}&countryCode={CC}[&legalEntityId={guid}]
```

**Neden `overrides/resolve`, `working-calendars/resolve` DEĞİL — bu bir tercih değil, tek çalışan yol:**

| Aday | Sonuç |
|---|---|
| `/api/platform/working-calendars/resolve` (ülke katmanı) | ❌ Gateway `IsAdminPath` sayar: (a) `X-Tenant-Id` başlığı **400** ile reddedilir, (b) `tenant_user` token'ı **403** alır (*"Platform admin or partner admin token is required"*). Ayrıca `platform.working-calendar.read` tenant'a atanamayan bir platform anahtarıdır |
| `/api/platform/working-calendars/overrides/resolve` | ✅ `IsTenantScopedOrgPath` allowlist'inde; tenant token'ı geçer, `X-Tenant-Id` enjekte edilir, `platform.working-calendar.override.read` **tenant-atanabilir** bir anahtardır, mevcut `/api/platform/working-calendars/{everything}` Ocelot route'unu kullanır |

**Kritik:** iki uç nokta **aynı `ResolveWorkingDayHandler`'ı ve aynı `IWorkingCalendarProvider`'ı** çağırır —
farklı bir cevap değil, aynı cevabın tenant-erişilebilir kapısıdır. Ayrıca `overrides/resolve`, ülke katmanı **+**
tenant'ın kendi override'ını birlikte çözer; kapasite için doğru olan da budur.

**Transport profili** (MOD-0165-FU07 `MdmCyclePeriodLegalEntityValidator` ile **birebir aynı**):

- Her zaman **Gateway** üzerinden — asla servis portu (5057).
- Toplam bütçe **3 sn**, **1** transient retry (502/503/504, 75 ms).
- `Authorization`, `X-Tenant-Id`, `X-Correlation-Id` **forward edilir**.
- **Cache YOK.** Aynı ay iki kez sorulursa iki çağrı gider. Cache, takvim değiştikten sonra eski bir sayının
  otoriter görünmesine yol açardı — tam da bu sınıfın engellemek için var olduğu şey.

**Fail-closed sözleşmesi:**

| Durum | Sonuç |
|---|---|
| `resolution == "resolved"` | `count` kullanılır |
| `resolution ∈ {calendar_missing, year_missing, country_unknown}` | **Hesap YAPILMAZ.** `503` + `calendar_unresolved` + ilgili `reasonCodes`. **Sahte varsayılan gün YOK** (`~22 iş günü` gibi bir tahmin **yasaktır**) |
| Timeout / 5xx / auth reddi / bozuk gövde | **`503`** + `calendar_unresolved` |
| **Herhangi bir** ay çözülemedi | **Tüm** kapasite hesabı çözülemez döner — **kısmi tablo gösterilmez** (kısmi bir sayı otoriter görünür ve yanlıştır; WC'nin `WorkingDaysBetweenAsync` içindeki "no partial count" kuralının aynısı) |

**Ne yazılır, ne yazılmaz:** okuma bir **doğrulama değil, bir hesap girdisidir**; bu yüzden **yazma yolunu
BLOKLAMAZ**. Takvim erişilemezse kapasite **kaydedilebilir** (girdiler geçerlidir), yalnız **hesaplanamaz**.
`503`, `/calculation` ve `Details` yüzeylerinden döner — `POST`/`PUT`'tan değil. Bu, "dependency down ⇒ hiç
çalışılamaz" ile "dependency down ⇒ sessizce uydur" arasındaki doğru orta yoldur.

**Çağrı sayısı ve neden N tane:** dönem başına **ay sayısı kadar** HTTP çağrısı (tipik 2–4, en fazla 12).
Working Calendar'ın toplu-ay uç noktası **yoktur** ve `services/Diten.Platform/**` bu pack için **protected**'tır
(§6) — dolayısıyla eklenemez. `MaxScanDays` (1830) tek çağrı için bir sınırdır ve bir ay ona asla yaklaşmaz.
Toplu uç nokta önerisi → **`F-WC-BULK`**.

### 8.4 Hesaplanan değerler **PERSIST EDİLMEZ**

`TotalVisitNumber` ve türevleri **read-time projeksiyondur** (MOD-0165-FU08 `D-PROJECTION` emsali). Sebep: çalışma
takvimi sonradan değişebilir (yeni tatil yayınlanır, tenant override eklenir) ve saklanmış bir sayı **sessizce
yalan söylemeye** başlar. Saklanan tek şey **girdilerdir**; sayı her okumada yeniden üretilir.

**Bunun `Fte`'ye etkisi:** `Fte` bir **girdidir**, hesap sonucu değil — bu yüzden saklanır (§4.3) ve
reproducibility korunur.

---

## 9. Layout & Shell Contract

`shell: tenant` ⇒ **`Layout = "_LayoutTenantShell"`**.

Aşağıdaki **her** dosyada Razor bloğunda **AÇIKÇA** yazılır (`_ViewStart.cshtml` varsayılanına güvenilmez):

```cshtml
@{
    ViewData["Title"] = Localizer["CycleCapacityTitle"];
    Layout = "_LayoutTenantShell";
}
```

| Dosya | `Layout` |
|---|---|
| `Views/CRM/CycleCapacity/Index.cshtml` | `_LayoutTenantShell` |
| `Views/CRM/CycleCapacity/Create.cshtml` | `_LayoutTenantShell` |
| `Views/CRM/CycleCapacity/Edit.cshtml` | `_LayoutTenantShell` |
| `Views/CRM/CycleCapacity/Details.cshtml` | `_LayoutTenantShell` |

Partial'lar (`_Form`, `_Filter`, `_DataTable`, `_IndexL10n`) layout **tanımlamaz**.

**Navigation:** domain-config gereği CRM menüsü bugün katalog-güdümlü değildir; `_LayoutTenantShell.cshtml`'e
`@if (Perms.Has("crm.cycle-capacity.read"))` guard'lı **tek** `<li>` eklenir (how-to-add-a-module Adım 9).
Katalog-güdümlü nav → `F-NAV` (MOD-0285).

---

## 10. Backend File Convention (Golden Reference Compact — birebir)

```text
services/Diten.CrmService/src/Diten.CrmService.Application/Features/CycleCapacity/
├── Commands/
│   ├── CreateCycleCapacityCommand.cs           (sealed record, IRequest<Response<Guid>>)
│   ├── UpdateCycleCapacityCommand.cs           (sealed record, IRequest<Response<NoContent>>)
│   └── ArchiveCycleCapacityCommand.cs          (sealed record)
├── Queries/
│   ├── GetCycleCapacityListQuery.cs            (sealed record)
│   ├── GetCycleCapacityByIdQuery.cs
│   ├── GetCycleCapacityByCyclePeriodQuery.cs
│   └── GetCycleCapacityContractQuery.cs
├── Handlers/
│   ├── CommandHandlers/                        ← AYRI klasör (zorunlu)
│   │   ├── CreateCycleCapacityHandler.cs       (sealed class, Command suffix YOK)
│   │   ├── UpdateCycleCapacityHandler.cs
│   │   └── ArchiveCycleCapacityHandler.cs
│   └── QueryHandlers/                          ← AYRI klasör (zorunlu)
│       ├── GetCycleCapacityListHandler.cs
│       ├── GetCycleCapacityByIdHandler.cs
│       ├── GetCycleCapacityByCyclePeriodHandler.cs
│       └── GetCycleCapacityContractHandler.cs
├── Validators/
│   ├── CreateCycleCapacityValidator.cs         (Command suffix YOK)
│   └── UpdateCycleCapacityValidator.cs
├── Rules/
│   ├── CycleCapacityCalculator.cs              (SAF — I/O yok)
│   └── CycleCapacityMonthRules.cs              (SAF)
├── Services/
│   └── ICycleCapacityCountryResolver.cs        (SAF)
├── Read/
│   └── IWorkingDayCounter.cs                   (arayüz; impl Infrastructure'da)
├── Contract/
│   ├── CycleCapacityContract.cs
│   └── CycleCapacityFeatureFlags.cs
├── CycleCapacityMapper.cs
├── CycleCapacityModels.cs                      ← TEK dosyada tüm DTO'lar
└── CycleCapacityPermissions.cs                 (TANIM — hiçbir şey seed etmez)
```

**Naming (tartışmasız):** Command `{Verb}CycleCapacityCommand` · Query `GetCycleCapacity{Qualifier}Query` ·
Handler `{Verb}CycleCapacityHandler` (**Command/Query suffix YOK**) · Validator `{Verb}CycleCapacityValidator`
(**Command suffix YOK**).

**Yasaklar:** tek dosyada birden fazla `public class`/`record` (`CycleCapacityModels.cs` DTO istisnası hariç) ·
`*CommandHandler.cs` / `*QueryHandler.cs` suffix · `CommandHandlers`/`QueryHandlers` ayrımını yapmamak ·
`Requests/Commands/` gibi ekstra alt klasör.

> **D-FILES (kayıt).** Mevcut CRM feature'ları (Campaign, CyclePeriod, Segmentation) gruplanmış bir dosya düzeni
> kullanıyor. **Bu FU Golden Compact'a birebir uyar** ve komşularını hizalamaya kalkmaz — mevcut sapma
> `F-FILE-DRIFT` altında kayıtlıdır ve bu FU'nun işi değildir.

---

## 11. Frontend File Contract (Compact)

```text
frontend/Diten.Web/Views/CRM/CycleCapacity/
├── Index.cshtml                    (Layout AÇIKÇA; ① Filter → ② BulkActionBar → ③ DataTable)
├── Create.cshtml                   (sayfa kabuğu + _Form)
├── Edit.cshtml                     (sayfa kabuğu + _Form)
├── Details.cshtml                  (salt-okunur + hesaplanan ay tablosu + tahmin bandı)
├── _Form.cshtml                    (Create/Edit ortak: başlık alanları + ay grid'i + disabled FTE)
├── _Filter.cshtml                  (inline collapsible; class="dt-inline-filter-host")
├── _DataTable.cshtml               (data-dt-standard="v2" + skeleton loader)
├── _IndexL10n.cshtml               (JSON payload bridge)
└── CycleCapacityIndex.cs           (marker class)

frontend/Diten.Web/wwwroot/assets/js/CRM/CycleCapacity/
├── index.js
├── form.js
└── index.l10n.js

frontend/Diten.Web/Resources/Views/CRM/CycleCapacity/
└── CycleCapacityIndex.{en,tr,fr,es,zh,ar,ru}.resx      (7 dil — tenant modülü)
```

**Compact'ta YASAK (verifier reddeder):** `_CreateEditOffcanvas.cshtml` · `_DetailsQuickView.cshtml` ·
Index içinde create/edit offcanvas.

**Ek kurallar:**

- Partial path'leri **absolute**: `~/Views/CRM/CycleCapacity/_Filter.cshtml`.
- `_Filter.cshtml` kök elemanı `class="dt-inline-filter-host"` **taşımalıdır** (aksi hâlde chip CSS'i uygulanmaz —
  belgelenmiş tuzak).
- `index.l10n.js` camelCase→PascalCase köprüsünü **yapmalıdır**, aksi hâlde `window.L10n` anahtarları `undefined`
  olur ve toast `(undefined: <corrId>)` basar (belgelenmiş tuzak).
- `updateVisualState` çağrıları **sayfaya özgü selector** kullanır; global selector ikinci bir DataTable'ın
  filtre/colvis rozetini siler (belgelenmiş tuzak).
- **Ay grid'i bir DataTable DEĞİLDİR** — `_Form.cshtml` içinde düz bir `<table>`'dır. Verifier kontratı yalnız
  `_DataTable.cshtml`'deki liste grid'ine uygulanır.

**Modaller/uyarılar:** Premium SweetAlert2 Standardı (MOD-0013) zorunlu.

---

## 12. Validation Rules

### 12.1 `CycleCapacity` (başlık)

| Field | Required | Format / Rule | DB-level | Pre-check |
|---|---|---|---|---|
| `CyclePeriodId` | Evet | `NotEmpty`; dönem **caller'ın tenant'ında var olmalı**; **create'ten sonra değişmez** | partial unique `(TenantId, CyclePeriodId)` where `IsDeleted:false` | `ICyclePeriodReader.GetByIdAsync` **yazımdan ÖNCE** |
| `CalendarCountryCode` | Evet | 2 harf, upper-case normalize; MOD-0048 `countries` setinde **yayınlı** olmalı; **fallback liste YASAK**. Dönem scope'undan türetilebiliyorsa **sunucu türetir ve payload'ı yok sayar** | — | `GetLookupOptions("countries")` |
| `DailyWorkMinutes` | Evet | `InclusiveBetween(1, 1440)` | — | — |
| `PromoProductTime` | Evet | `InclusiveBetween(0, 480)` | — | — |
| `NonPromoProductTime` | Evet | `InclusiveBetween(0, 480)` | — | — |
| *(çift-alan)* | — | `PromoProductTime + NonPromoProductTime > 0` → aksi hâlde `400 visit_minutes_zero`. **Sıfıra bölme buradan engellenir, hesapta değil** | — | — |
| `TravelingTime` | Evet | `InclusiveBetween(0, 1440)` | — | — |
| `ReportDuration` | Evet | `InclusiveBetween(0, 1440)` | — | — |
| `QuizDuration` | Evet | `InclusiveBetween(0, 1440)` | — | — |
| *(çift-alan)* | — | `TravelingTime + ReportDuration + QuizDuration < DailyWorkMinutes` → aksi hâlde `400` (bir günde ziyarete zaman kalmıyor) | — | — |
| `Fte` | sistem | Payload **yok sayılır**; create'te config'ten yazılır; `> 0` | — | — |
| `Description` | Hayır | trim, `MaximumLength(1000)` | — | — |
| `Months` | Evet | `NotEmpty`; `(Year, MonthNumber)` **benzersiz**; her satır dönem penceresiyle kesişmeli | — | `CycleCapacityMonthRules` |
| `Version` | Evet (update) | Dirty-check; uyuşmazlık `409` | — | — |

### 12.2 `CycleCapacityMonth` (satır)

| Field | Required | Format / Rule |
|---|---|---|
| `Year` | Evet | `InclusiveBetween(2000, 2100)` |
| `MonthNumber` | Evet | `InclusiveBetween(1, 12)` |
| `MeetingDays` | Evet | `GreaterThanOrEqualTo(0)` |
| `TrainingDays` | Evet | `GreaterThanOrEqualTo(0)` |
| `VacationDays` | Evet | `GreaterThanOrEqualTo(0)` |
| `MicroTargetingDayCount` | Evet | `GreaterThanOrEqualTo(0)` |
| `MicroTargetingDuration` | Evet | `InclusiveBetween(0, 1440)` |

> Düşümlerin toplamının o ayın çalışma günü sayısını aşması **doğrulama hatası DEĞİLDİR** — çünkü çalışma günü
> sayısı yazma anında bilinmez (takvim okuması read-time'dır, §8.4). Hesapta `fieldDays` **0'a clamp edilir** ve
> sonuç `0` ziyaret olur; UI o ayı bir uyarı rozetiyle gösterir (AC-UI-5).

---

## 13. Hesap Sözleşmesi (normatif)

### 13.1 Girdi

Her ay `m` için, Working Calendar'dan **tek** bir sayı okunur:

```
wcWorkingDays(m) = working-days-between( max(ayBaşı, CyclePeriod.StartDate),
                                         min(aySonu,  CyclePeriod.EndDate) )
```

> **⚠️ NARROWING BULGUSU — görev girdisindeki formül bir düzeltme gerektiriyor.**
> Görev girdisi `fieldDay = workingDays − (haftasonu + tatil + meeting + training + vacation)` diyor.
> **Working Calendar'ın `working-days-between` operasyonu hafta sonlarını ve resmî tatilleri/kapanışları ZATEN
> düşmüş bir sayı döner** (`WorkingCalendarProvider.WorkingDaysBetweenAsync` → `IsWorkingDayAsync` her günü tek tek
> değerlendirir). Hafta sonu ve tatili bir kez daha çıkarmak onları **iki kez sayardı** ve kapasiteyi sistematik
> olarak eksik hesaplardı. Doğru form aşağıdadır ve **bu bir sadeleştirmedir, bir kapsam değişikliği değil.**

### 13.2 Formül (normatif)

```
fieldDays(m)       = max(0, wcWorkingDays(m) − MeetingDays(m) − TrainingDays(m) − VacationDays(m))

availableMinutes(m) = DailyWorkMinutes × fieldDays(m)

spendMinutes(m)     = (TravelingTime + ReportDuration + QuizDuration) × fieldDays(m)
                    + MicroTargetingDayCount(m) × MicroTargetingDuration(m)

visitMinutes(m)     = max(0, availableMinutes(m) − spendMinutes(m))

minutesPerVisit     = PromoProductTime + NonPromoProductTime        // §12.1 gereği > 0

TotalVisitNumber(m) = max(0, round( visitMinutes(m) ÷ minutesPerVisit × Fte,
                                    MidpointRounding.AwayFromZero ))

TotalVisitNumber(cycle) = Σ TotalVisitNumber(m)
```

**Görev girdisindeki forma denkliği.** Girdi formülü günlük yazılmıştı:
`((DailyWorkMinutes − spendPerDay) ÷ minutesPerVisit) × Fte × fieldDay`. Mikro-hedefleme **aylık** bir miktardır
(`DayCount × Duration`), günlük değil; günlük forma sokmak için `÷ fieldDays` ile amortize etmek gerekirdi ve bu,
`fieldDays = 0` olduğunda **sıfıra bölme** üretirdi. Yukarıdaki ay-seviyesi form, `fieldDays > 0` iken
**cebirsel olarak aynı sonucu** verir ve `fieldDays = 0` durumunda temiz biçimde `0` döner. **Model değişmedi,
yalnız aynı denklem güvenli sırada yazıldı.**

### 13.3 Sabitler config'ten gelir (magic constant yok)

| Config anahtarı | Default | Nereye gider |
|---|---|---|
| `CycleCapacity:DefaultDailyWorkMinutes` | `480` | `DailyWorkMinutes` alanının create default'u |
| `CycleCapacity:DefaultFte` | *(ops kararı)* | `Fte` alanı (§4.3) |
| `CycleCapacity:WorkingCalendarPathTemplate` | `api/platform/working-calendars/overrides/resolve` | HTTP seam (route taşınması bir **ops** değişikliği olsun diye) |

Default'lar **create anında alana yazılır**. Sonradan config değişirse **eski kayıtlar değişmez** — bu, §8.4'teki
reproducibility kuralının aynısıdır.

### 13.4 Çözülemeyen sonuç şekli

```jsonc
{
  "isEstimate": true,
  "resolution": "calendar_unresolved",       // veya "resolved"
  "reasonCodes": ["year_missing"],
  "totalVisitNumber": null,                  // ASLA 0 değil — 0 geçerli bir cevaptır, null "bilinmiyor" demektir
  "months": []                               // kısmi tablo YOK
}
```

> `null` ile `0` arasındaki fark **load-bearing**'dir: `0` *"bu ay ziyarete zaman kalmıyor"* demektir; `null`
> *"takvim konuşmadı, cevabı bilmiyoruz"* demektir. Bir bool ya da bir `0` yalan olurdu.

---

## 14. Authorization Convention

```text
Policy:     [Authorize]                                  // shell: tenant
Permission: [HasPermission("crm.cycle-capacity.{action}")]   // PKS-001: lowercase-dotted, ≥3 segment, kebab-case
Actor type: tenant_user
```

| Anahtar | Kapsam |
|---|---|
| `crm.cycle-capacity.read` | Liste · detay · contract · **`/calculation`** |
| `crm.cycle-capacity.manage` | Create · update · archive |

**Neden `.calculate` diye üçüncü bir anahtar yok:** hesap, kaydın kendi girdilerinden türeyen bir **görünümdür**;
kaydı okuyabilen kişi zaten girdileri görür ve aritmetiği kendi yapabilir. Ayrı bir anahtar, hiç verilmeyecek bir
anahtar olurdu.

**DEV-ONLY fallback.** RBAC kataloğu `crm.cycle-capacity.*` taşımıyor. MOD-0165-FU06 / MOD-0167-FU04 / MOD-0164-FU02
ile **aynı belgelenmiş dev-only fallback** kullanılır (`crm.territory.read` / `crm.territory.model.manage`).
Fallback **hiçbir guard'ı genişletmez**: tenant izolasyonu, pin doğrulaması, `closed` dönem kilidi ve fail-closed
takvim okuması arkasında aynen çalışır. → `F-RBAC`.

> **⚠️ `F-RBAC-WC` — çapraz-namespace bağımlılık (yeni sınıf bir sorun).** `/calculation` uç noktası, çağıranın
> token'ını Working Calendar'a **forward eder**. O uç nokta `platform.working-calendar.override.read` ister.
> Yani **bir CRM kullanıcısının bir `platform.*` anahtarına ihtiyacı vardır.** Bu anahtar bugün 97c5 tenant
> Admin'ine verilmiştir (manual grant), ama **başka hiçbir CRM rolüne verilmemiştir**.
> Sonuç: bu anahtarı taşımayan bir kullanıcı için hesap **403** ile döner ve UI bunu *"çalışma takvimi izni yok"*
> diye ayırt eder — sessizce `0` göstermez ve *"takvim yok"* diye de yanlış etiketlemez. Kalıcı çözüm bu FU'nun
> yetkisinde değildir (RBAC seed/grant **yasak**) → `F-RBAC-WC`.

---

## 15. Gateway / API Routing Decision

**Karar: Gateway değişikliği GEREKLİ.**

```text
$ grep -o '"/api/crm/[a-z-]*\(/{everything}\)\?"' gateway/Diten.ApiGateway/ocelot.json | sort -u
… accounts · campaigns · consents · contacts · cycle-periods · knowledge · preferences ·
  resources · segments · strategy-templates · subjects · territory-management · territory-models ·
  visit-frequency-policies
```

`/api/crm/cycle-capacities` **YOK** ve `/api/crm/{everything}` gibi bir catch-all da **yok** — her CRM kaynağı
kendi açık çiftini taşıyor. Dolayısıyla:

- **Gerekli:** `/api/crm/cycle-capacities` + `/api/crm/cycle-capacities/{everything}` explicit Upstream/Downstream
  çifti, `OPTIONS` metodu dâhil, downstream `Diten.CrmService` portuna.
- `gateway/Diten.ApiGateway/**/ocelot.json` **protected path**'tir; bu pack **doğrudan yazmaz** →
  ayrı bir **`integration-agent`** task'ı.
- **Doğrulama ipucu:** route eksikse frontend proxy `404` + gövde `{}` döner (belgelenmiş imza). Anonim `OPTIONS`
  ile prob edilir; anonim `GET` middleware'de routing'den **önce** `403` alır ve teşhisi yanıltır.

**Working Calendar tarafında Gateway değişikliği GEREKMEZ** — `/api/platform/working-calendars/{everything}`
wildcard route'u zaten mevcut ve `overrides` alt-yolu `IsTenantScopedOrgPath` allowlist'inde (§8.3'te kanıtlandı).

---

## 16. Acceptance Criteria

### 16.1 Sınır (B — Boundary)

- **AC-B-1** `CycleCapacity` aggregate'i `Diten.CrmService` içinde oluşur; `CyclePeriodId` create'te pinlenir ve
  `PUT` ile **değiştirilemez** (deneme `400`, kayıt değişmez).
- **AC-B-2** Aynı `(TenantId, CyclePeriodId)` için ikinci bir aktif kapasite `409 duplicate_capacity` döner
  (**1:1**); DB seviyesinde partial unique index ile de engellenir.
- **AC-B-3** Var olmayan / başka tenant'a ait bir `cyclePeriodId` ile create → `404` (cross-tenant sızıntı yok).
- **AC-B-4** `git diff --stat` çıktısında `services/Diten.CrmService/**/Features/CyclePeriod/**` altında
  **0 (sıfır)** dosya bulunur.
- **AC-B-5** `git diff --stat` çıktısında `services/Diten.Platform/**` altında **0 (sıfır)** dosya bulunur.
- **AC-B-6** `CycleCapacity` + `CycleCapacityMonth` `RegisterClassMaps`'e eklidir; `CyclePeriodId` ile filtreleyen
  bir sorgu **kayıt döner** (class-map atlanırsa bu test kırmızıya döner).

### 16.2 Doğrulama / kilit (V — Verify)

- **AC-V-1** `CyclePeriodContract` yanıtındaki **27 bayrağın hepsi** FU07 sonrası değerleriyle **birebir aynıdır**;
  özellikle `supportsWorkingCalendarIntegration: false` ve `supportsWorkingDayCount: false` **`false` kalır**.
- **AC-V-2** `CyclePeriod` feature klasöründeki hiçbir mevcut test dosyası **davranış iddiası** değiştirmez.
- **AC-V-3** `CycleCapacityFeatureFlags` şu bayrakları **`false`** taşır ve testle kilitlenir:
  `SupportsMicroTargetGeneration` · `SupportsVisitDistribution` · `SupportsRoutePlanning` ·
  `SupportsFrequencyPolicyWrite` · `SupportsCampaignBinding` · `SupportsWorkingCalendarWrite` ·
  `SupportsHrFteIntegration` · `SupportsCapacityApproval` · `SupportsComputedValuePersistence` ·
  `SupportsHardDelete` · `SupportsBulkDelete`. `IsEstimate` **`true`** taşır.
- **AC-V-4** Repo genelinde `CAND-CAP` literal'i runtime kodunda **0** kez geçer.

### 16.3 Hesap (C — Calculation)

- **AC-C-1** Bilinen bir örnekte formül §13.2'deki değeri **tam olarak** üretir. **Altın örnek (normatif):**

  | Girdi | Değer |
  |---|---|
  | `wcWorkingDays(m)` | `21` |
  | `MeetingDays` / `TrainingDays` / `VacationDays` | `1` / `1` / `2` |
  | `MicroTargetingDayCount` / `MicroTargetingDuration` | `3` / `45` |
  | `DailyWorkMinutes` | `480` |
  | `TravelingTime` / `ReportDuration` / `QuizDuration` | `60` / `30` / `10` |
  | `PromoProductTime` / `NonPromoProductTime` | `15` / `10` |
  | `Fte` | `12.00` |

  ```text
  fieldDays        = 21 − (1 + 1 + 2)            = 17
  availableMinutes = 480 × 17                    = 8160
  spendMinutes     = (60 + 30 + 10) × 17 + 3 × 45 = 1700 + 135 = 1835
  visitMinutes     = 8160 − 1835                 = 6325
  minutesPerVisit  = 15 + 10                     = 25
  TotalVisitNumber = round(6325 ÷ 25 × 12)       = 3036
  ```
- **AC-C-2** Hafta sonu ve resmî tatil **iki kez düşülmez** — sabit bir `wcWorkingDays` girdisiyle çalışan saf
  motor testinde, hafta sonu sayısı sonuca hiçbir şekilde girmez (§13.1 narrowing'i kilitler).
- **AC-C-3** `fieldDays = 0` olan bir ay `TotalVisitNumber = 0` üretir; **exception atmaz**, `NaN` üretmez.
- **AC-C-4** `minutesPerVisit = 0` **hesaba hiç ulaşmaz** — `400 visit_minutes_zero` ile yazma anında engellenir.
- **AC-C-5** Dönem penceresinin kısmen kestiği ilk/son ay için WC sorgusu **kesişim aralığıyla** yapılır
  (tam ayla değil).
- **AC-C-6** Yıl sınırını aşan bir dönem (Ara 2026 – Oca 2027) iki farklı `Year` taşıyan satırlar üretir ve doğru
  hesaplanır (**pozisyonel dizi olsaydı imkânsızdı**).
- **AC-C-7** Hesaplanan hiçbir değer Mongo'ya **yazılmaz** — create/update sonrası ham dokümanda
  `totalVisitNumber` benzeri **hiçbir alan yoktur**.

### 16.4 Takvim seam'i (W — Working calendar)

- **AC-W-1** Seam **yalnız** `api/platform/working-calendars/overrides/resolve` çağırır; ülke katmanı
  (`working-calendars/resolve`) **hiçbir kod yolunda** çağrılmaz.
- **AC-W-2** Çağrı **Gateway** üzerinden gider; kodda `5057` ya da doğrudan Platform host'u **geçmez**.
- **AC-W-3** `Authorization`, `X-Tenant-Id`, `X-Correlation-Id` forward edilir.
- **AC-W-4** `resolution != "resolved"` → `503` + `calendar_unresolved` + WC'nin `reasonCodes`'u aynen aktarılır;
  **hiçbir varsayılan gün sayısı üretilmez**.
- **AC-W-5** **Bir** ay çözülemezse **tüm** hesap `null` döner; `months` dizisi **boştur** (kısmi tablo yok).
- **AC-W-6** Timeout / 5xx → 1 retry (75 ms) → sonra `503`. Toplam bütçe 3 sn'yi aşmaz.
- **AC-W-7** Aynı ay iki kez sorulduğunda **iki** HTTP çağrısı gider (cache yok — test edilebilir).
- **AC-W-8** Takvim erişilemezken `POST`/`PUT` **başarılı olur** (`201`/`204`) — okuma yazma yolunu bloklamaz (§8.3).
- **AC-W-9** WC `403` döndüğünde sonuç `calendar_forbidden` olarak ayırt edilir, `calendar_unresolved` diye
  **etiketlenmez** (F-RBAC-WC teşhisi).

### 16.5 Yaşam döngüsü ve yetki (L — Lifecycle)

- **AC-L-1** Pinlenen dönem `closed` iken `PUT`/`archive` → `409 period_closed`; `GET` ve `/calculation`
  **çalışmaya devam eder**.
- **AC-L-2** `CycleCapacity`'nin **kendi** durum alanı **yoktur** (contract'ta `status` benzeri bir alan yok).
- **AC-L-3** Yetkisiz aktör (`crm.cycle-capacity.manage` yok) → `403`; UI'da aksiyon **disabled**.
- **AC-L-4** `Version` uyuşmazlığı → `409` + UI "veri değişti, yeniden yükleyin"; **sessiz overwrite yok**.

### 16.6 UI

- **AC-UI-1** `Views/CRM/CycleCapacity/*.cshtml` dosyalarının **tamamında** `Layout = "_LayoutTenantShell"`
  **açıkça** yazılıdır.
- **AC-UI-2** `_DataTable.cshtml` `data-dt-standard="v2"` taşır ve skeleton loader içerir.
- **AC-UI-3** `Fte` girdisi `disabled` render edilir **ve** sunucu payload'daki `Fte`'yi yok sayar (DOM'dan
  `disabled` kaldırılıp değer gönderildiğinde saklanan değer **değişmez**).
- **AC-UI-4** Kalıcı **tahmin uyarı bandı** `Create` / `Edit` / `Details` sayfalarının **üçünde de** görünür ve
  7 dilde çevrilidir.
- **AC-UI-5** `fieldDays = 0` olan ay satırı bir uyarı rozetiyle işaretlenir.
- **AC-UI-6** `Details` sayfası ay tablosunu şu kolonlarla gösterir: ay · WC çalışma günü · düşümler ·
  `fieldDays` · ziyaret dakikası · `TotalVisitNumber`; ve altta dönem toplamı. **Ara adımlar gizlenmez** —
  kullanıcı sayının nereden geldiğini görebilmelidir.
- **AC-UI-7** `CyclePeriods` Index'inde her satırın ⋮ menüsünde **"Cycle Capacity"** öğesi görünür ve
  `/CRM/CycleCapacity?cyclePeriodId={id}`'ye yönlendirir. `CyclePeriods` `_DataTable.cshtml`'e **kolon eklenmez**.
- **AC-UI-8** `/CRM/CycleCapacity?cyclePeriodId={id}` — kapasite varsa `Details/{id}`'ye, yoksa
  `Create?cyclePeriodId={id}`'ye **redirect** eder.
- **AC-UI-9** Browser hiçbir zaman servis portuna gitmez; tüm çağrılar same-origin `/CRM/CycleCapacity/api/**`
  proxy'sinden geçer.
- **AC-UI-10** 7 dil RESX **parite**: her `.resx` dosyasında **aynı** anahtar kümesi bulunur (sayı eşitliği testle
  doğrulanır).

### 16.7 Kalite kapıları

- **AC-Q-1** `python3 .antigravity/scripts/verify_datatable_page.py . --area CRM --module CycleCapacity --reference compact --api-profile proxy`
  çalıştırılır; FAIL kümesi **kardeş CRM Compact modülünün** (ör. `Segments` veya `CyclePeriods`) FAIL kümesiyle
  **birebir** aynı olmalıdır (ad + sonuç diff'i boş). Yeni bir FAIL **kabul edilmez**.
- **AC-Q-2** `py .antigravity/scripts/verify_module_id.py . --check-all` → **HARD violations: 0**.
- **AC-Q-3** `dotnet build` — `Diten.CrmService` + `Diten.Web` **0 hata**.
- **AC-Q-4** Mevcut test suite'i **regresyonsuz** geçer (bilinen flaky `ContactLocationPiiHardening…` hariç).

---

## 17. Test Expectations

### 17.1 Unit (saf — dış bağımlılık yok)

| Küme | Kapsam |
|---|---|
| `CycleCapacityCalculatorTests` | §13.2 formülü · altın örnek (AC-C-1) · çifte-düşüm yok (AC-C-2) · `fieldDays=0` · negatife clamp · rounding `AwayFromZero` · `Fte` çarpanı · aylık mikro-hedefleme amortizasyonu |
| `CycleCapacityMonthRulesTests` | Dönem penceresinden ay türetimi · kısmi ilk/son ay kesişimi · yıl sınırı aşan dönem · `(Year, MonthNumber)` benzersizliği · `.Date` indirgemesi (DateTimeOffset tuzağı) |
| `CycleCapacityValidationTests` | §12'deki her kural · `visit_minutes_zero` · günlük süre toplamı taşması · payload'daki `Fte`'nin yok sayılması |
| `CycleCapacityCountryResolverTests` | 4 scope tipi × türetilebilirlik matrisi (§4.4) · `legalEntityId` passthrough · `BusinessUnitId`'nin `organizationUnitId`'ye **eşlenmediği** |

### 17.2 Integration / handler

| Küme | Kapsam |
|---|---|
| `CycleCapacityPinTests` | Pin doğrulaması yazımdan **önce** · cross-tenant `404` · pin immutability |
| `CycleCapacityUniquenessTests` | 1:1 (handler + Mongo partial index) |
| `CycleCapacityLifecycleTests` | `closed` dönem kilidi (AC-L-1) · concurrency `409` |
| `WorkingDayCounterTests` | Fake HTTP handler ile: resolved / `calendar_missing` / `year_missing` / `country_unknown` / timeout / 5xx+retry / `403` ayrımı / **cache yokluğu** / doğru URL + op + forward edilen başlıklar |
| `CycleCapacityProjectionTests` | Hesaplanan değerin **persist edilmediği** (ham doküman denetimi, AC-C-7) |
| `CycleCapacityContractTests` | Bayrak kümesi (AC-V-3) + `IsEstimate: true` |
| `CyclePeriodContractUnchangedTests` | **27 bayrağın** FU07 değerleriyle birebir aynı olduğu (AC-V-1) |

### 17.3 Frontend / kalite

- `verify_datatable_page.py` (AC-Q-1) · `verify_module_id.py --check-all` (AC-Q-2) · build (AC-Q-3)
- RESX parite scripti: 7 dil × anahtar sayısı eşitliği (AC-UI-10)
- `git diff --stat` denetimi: CyclePeriod backend **0**, `Diten.Platform` **0** (AC-B-4/B-5)

### 17.4 Authenticated smoke — **kullanıcı çalıştırır**

Login parolası gerektirir ve fleet'in bu FU'nun build'iyle yeniden başlatılmasını bekler. Ön koşullar:

1. En az bir `active` (ya da `draft`) `CyclePeriod`.
2. Kapasite kurulacak dönemin ülkesi için **yayınlanmış ve aktif** bir Working Calendar (yoksa AC-W-4 yolu
   doğrulanır — ki bu da geçerli bir smoke sonucudur).
3. Test kullanıcısında `platform.working-calendar.override.read` (yoksa AC-W-9 yolu doğrulanır).
4. Ocelot route'u eklenmiş ve Gateway yeniden başlatılmış olmalı (§15) — aksi hâlde **her şey `404` + `{}`**.

---

## 18. Ready-for-dev Checklist

- [x] Golden Reference **Compact** referans olarak okundu (`DEV-0001` + canlı `GoldenReferenceCompact` kodu + canlı CRM Compact kardeşleri)
- [x] Frontmatter tüm zorunlu alanlar dolu (`service`, `shell`, `golden_reference`, `entity_base`, `form_field_count`)
- [x] Layout & Shell Contract'ta Razor `Layout` açıkça yazılı (§9)
- [x] Backend File Convention Golden Reference ile birebir (§10)
- [x] Frontend File Contract Compact dosya listesi tam; `_CreateEditOffcanvas` / `_DetailsQuickView` **yok** (§11)
- [x] Validation Rules her field için yazılı (§12)
- [x] Failure Path ≥ 4 senaryo (§19.1 — 12 senaryo)
- [x] Authorization Convention: permission listesi + policy + actor type (§14)
- [x] Gateway routing kararı açık: **gerekli**, integration-agent task'ı (§15)
- [x] Acceptance criteria test edilebilir maddeler (§16 — 44 madde)
- [x] Test expectations build / verifier / RESX / smoke kapsıyor (§17)
- [x] DCP-002 kimlik geçidi **exit 0** + fail-closed kontrol koşusu (§0.1)
- [x] **`D-COUNTRY` kullanıcı kararı (§4.4) — B ONAYLANDI** (2026-08-28): `CalendarCountryCode` bir takvim sorgu parametresidir, scope değildir
- [ ] **⚠️ `status: ready-for-dev` + `runtime_code_allowed: true` flip'i** — ayrı kullanıcı kararı
- [x] Ocelot route çifti eklendi (§15 / §0.3 — F-GATEWAY kullanıcı tarafından bu çalışmanın kapsamına alındı)

---

## 19. Implementation Notes

### 19.1 Failure Path to Verify

| Senaryo | Beklenen |
|---|---|
| **Duplicate capacity** — aynı döneme ikinci kapasite | `409 duplicate_capacity` + UI alan-seviyesi hata + kayıt oluşmaz + reload sonrası temiz state |
| **Missing / unknown cycle period** | `404` + hiçbir şey yazılmaz (cross-tenant sızıntı yok) |
| **Pin mutation** — `PUT` ile `CyclePeriodId` değiştirme | `400` + kayıt **değişmez** |
| **Closed period write** | `409 period_closed` + UI aksiyonları disabled |
| **Concurrency conflict** — eski `Version` | `409` + UI "veri değişti, yeniden yükleyin" + **sessiz overwrite YOK** |
| **`visit_minutes_zero`** — promo + non-promo = 0 | `400` + validator mesajı + save engellenir (**hesapta sıfıra bölme asla oluşmaz**) |
| **Günlük süre taşması** — travel+report+quiz ≥ DailyWorkMinutes | `400` + alan-seviyesi mesaj |
| **`month_out_of_period`** — dönem penceresiyle kesişmeyen ay | `400` + hangi satır olduğu belirtilir |
| **Unknown country code** | `400` + MOD-0048 setinde olmadığı belirtilir; **fallback liste yok** |
| **Calendar unresolved** (`calendar_missing`/`year_missing`/`country_unknown`) | `503 calendar_unresolved` + reason codes + `totalVisitNumber: null` + `months: []`; **yazma yolu etkilenmez** |
| **Calendar forbidden** (WC `403`) | `503 calendar_forbidden` — `calendar_unresolved`'dan **ayrı**; UI "çalışma takvimi izni yok" der (F-RBAC-WC) |
| **Unauthorized actor** | `403` + UI aksiyon disabled / permission-denied state |

### 19.2 Legacy `CyclePeriodCalendar` — korunanlar ve düşürülenler

| Legacy | Karar |
|---|---|
| `TotalVisitNumber` (ay başına kapasite) | ✅ **KORUNDU** — bu FU'nun ürünü |
| Aktivite dakika bütçesi (promo/non-promo/travel/report/quiz/micro-targeting) | ✅ **KORUNDU** — §4.1/§4.2 |
| Çalışma-günü düşümleri (meeting/training/vacation) | ✅ **KORUNDU** — §4.2 |
| FTE çarpanı | ⚠️ **KORUNDU ama INTERIM** — per-BU granülerliği `F-FTE-BU` |
| 12-elemanlı **pozisyonel ay dizileri** | ❌ **DÜŞÜRÜLDÜ** — açık `(Year, MonthNumber)` satırları (§4.2/D-MONTH) |
| Sihirli `RowId` | ❌ **DÜŞÜRÜLDÜ** — kimlik `(Year, MonthNumber)` |
| `OldSystem` coupling | ❌ **DÜŞÜRÜLDÜ** — legacy sisteme referans yok |
| Campaign servisinde yaşaması | ❌ **DÜŞÜRÜLDÜ** — SoR MOD-0155 (§0.2/D-HOME) |
| Hafta sonu/tatil listelerinin kendi içinde tutulması | ❌ **DÜŞÜRÜLDÜ** — CAND-CAP-0008 salt-okunur tüketilir (§8.3) |

### 19.3 Bilinen tuzaklar (emsalden — tekrarlanmasın)

| Tuzak | Önlem |
|---|---|
| **Verifier VM çözümlemesi** — verifier bir form alanının tipini dosyadaki **son** aynı-isimli property'den çözer | Hesap projeksiyonu (`Year`, `MonthNumber` gibi form alanlarıyla ad çakışan property'ler taşır) **kendi dosyasında** tutulur: `Models/CRM/CycleCapacityCalculationViewModel.cs` (MOD-0165-FU08 S2 ve MOD-0167-FU02'de belgelenen aynı tuzak) |
| **Class-map eksikliği** | AC-B-6 |
| **DateTimeOffset `.Date` indirgemesi** | AC-C-5 + unit test |
| **İki DateTimeOffset alanının birlikte sort/index'lenmesi** | Ay sıralaması `Year`+`MonthNumber` (int) ile yapılır — tarih alanlarıyla değil |
| **Mongo partial index `$ne` çökmesi** | Partial filter `IsDeleted: false` (eşitlik) kullanır; `$ne`/`$not` **kullanılmaz** (Platform crash-loop emsali) |
| **`dt-inline-filter-host` sınıfı** | §11 |
| **`index.l10n.js` PascalCase köprüsü** | §11 |
| **`updateVisualState` global selector** | §11 |
| **Gateway `404` + `{}`** | §15 |
| **Standalone Mongo transaction** | Aggregate **tek doküman**tır (ay satırları embedded) → çok-doküman transaction **hiç gerekmez**. Bu, embedded ay modelinin ikinci bir faydasıdır |

### 19.4 MOD-0155-FU05 (MicroTarget) için bırakılan seam

Bu FU **bir seam yayınlamaz** ve `ICycleCapacityReader` **yazmaz**. Gerekçe: FU05 henüz yok, ve tüketicisi olmayan
bir arayüz, ilk gerçek tüketici geldiğinde yanlış şekilde donmuş olur (MOD-0162-FU05'in `IContentEngagementJourneyReader`
seam'i tam olarak bu sebeple bir FU geç açıldı). FU05 geldiğinde seam **onun** pack'inde tanımlanır ve bu aggregate'e
**salt-okunur** olarak eklenir → `F-MICROTARGET-SEAM`.

---

## 20. Follow-up Items

| ID | Konu | Sahip | Not |
|---|---|---|---|
| **F-REGISTRY** | `MOD-0155-FU06` registry satırı | commercial-suite | Registry yazımı pack yetkisi dışı |
| **F-COUNTRY** | `D-COUNTRY` (§4.4) A/B kararının nihaileşmesi | **KULLANICI** | Pack B'ye göre yazıldı; ready-for-dev öncesi kapanmalı |
| **F-FTE-HR** | Gerçek HR/FTE entegrasyonu (`Fte` alanının otomatik doldurulması) | HCM / MOD-0288 | `Fte` alanı ve `FteSource` bugünden hazır |
| **F-FTE-BU** | Legacy'nin per-`(BusinessUnit, Year)` FTE granülerliği | commercial-suite | HR master'a bağlı |
| **F-RBAC** | `crm.cycle-capacity.*` katalog kaydı + rol ataması | platform-shared-services | Bugün dev-only fallback |
| **F-RBAC-WC** | CRM rollerine `platform.working-calendar.override.read` verilmesi | platform-shared-services | **Bu olmadan hesap 403 döner** (§14) |
| **F-WC-BULK** | Working Calendar'a toplu ay/aralık uç noktası | platform-shared-services | `Diten.Platform` protected; bugün ay başına 1 çağrı (§8.3) |
| **F-WC-ORG-UNIT** | `BusinessUnitId` (MOD-0048 kodu) ↔ `organizationUnitId` (Guid) eşlemesi | commercial-suite / PSS | Bugün **eşlenmiyor**; BU kapsamlı dönem takvimi daraltmaz (§4.4) |
| **F-APPROVAL** | Kapasite onay/approval iş akışı | commercial-suite | D-LIFECYCLE bugün kapalı (§3.6) |
| **F-ACTUALS** | Kapasite ↔ gerçekleşen ziyaret karşılaştırması | commercial-suite | FU01/FU02 verisine bağlı |
| **F-SCENARIO** | Aynı dönem için senaryo/versiyon karşılaştırması | commercial-suite | 1:1 kısıtı bugün buna izin vermez (bilinçli) |
| **F-MICROTARGET-SEAM** | `ICycleCapacityReader` — FU05 tüketimi | commercial-suite | §19.4 |
| **F-FILE-DRIFT** | Mevcut CRM feature'larının Golden Compact'a hizalanması | commercial-suite | Bu FU uyar, komşularını hizalamaz |
| **F-NAV** | Katalog-güdümlü nav (MOD-0285) | commercial-suite | Bugün elle `<li>` |

---

## Handoff

Module pack **`draft`** olarak hazır. Lütfen inceleyip alan/scope düzeltmelerini yapın.

**Geliştirmeye geçmeden önce kapanması gereken iki kapı:**

1. **`D-COUNTRY` (§4.4)** — A (katı türetim, tenant/legal-entity dönemleri kullanılamaz) mı,
   **B (takvim parametresi, önerilen)** mı?
2. **`status: approved` / `ready-for-dev` + `runtime_code_allowed: true`** flip'i.

Sonra `@orchestrator MOD-0155-FU06` çağrılır. Hazırlık sırasında **Golden Reference Compact** şablon olarak alındı —
sapma yok.
