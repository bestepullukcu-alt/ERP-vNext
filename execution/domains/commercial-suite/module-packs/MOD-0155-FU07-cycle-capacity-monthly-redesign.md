---
id: MOD-0155-FU07
name: Cycle Capacity Monthly Redesign
parent: MOD-0155
parent_name: Field Sales / Visit Planning
siblings: MOD-0155-FU01 (Planned Visit — draft) · MOD-0155-FU02 (Visit Report) · MOD-0155-FU03 (Route Planning) · MOD-0155-FU04 (Visit Content Sequence Execution) · MOD-0155-FU05 (MicroTarget) · MOD-0155-FU06 (Cycle Capacity — SHIPPED, status review)
domain: commercial-suite
service: Diten.CrmService
frontend: frontend/Diten.Web
shell: tenant
golden_reference: compact
entity_base: EntityBase
status: review
runtime_code_allowed: true
flip_approved_by: "control-tower (2026-08-28) — draft verified: DCP-002 exit 0, no runtime touched, services/Diten.Platform/** forbidden (WC boundary), CyclePeriod untouched. DECISIONS APPROVED: D-HOLIDAY=C-deferred (column = 'Çalışılmayan gün'/NonWorkingDays derived free from the single existing WC call = (rangeEnd−rangeStart+1)−wcWorkingDays, counts weekends+holidays+closures together so NOT labelled 'Tatil'; holiday DATES/tooltip DEFERRED to F-WC-HOLIDAY-RANGE, a future Working Calendar op=holidays-between — NOT added here since Platform is protected). Other locks: FTE moves root→CycleCapacityMonth.Fte (still disabled/config-default, read-time migration like CyclePeriod EnsureScopeType, no backfill, old root FTE copied to each month, AC-F-5); CycleCapacityCalculator stays pure (Visits reads the month's own FTE, CapacityCalculation.Fte removed); merged monthly table (13 cols, 4-8 editable rest read-only, 500ms debounce updates only read-only cells); FieldForceSection card kept (parity) as description; no h6.text-uppercase subtitle (FU06 trap). TotalVisitNumber never persisted."
runtime_code_scope: "NONE (draft). `ready-for-dev` + `runtime_code_allowed: true` flip'i AYRI bir kullanıcı kararıdır. Flip SONRASI kapsam: `CycleCapacityMonth.Fte` (root `CycleCapacity.Fte` KALDIRILIR) + class-map + read-time migration + `CycleCapacityCalculator` per-ay FTE + non-working-day kolonu + birleşik Aylık Plan tablosu (computed kolonlar satır-içi, canlı) + preview genişletmesi + 7 dil RESX + boundary testleri. YASAK: `services/Diten.Platform/**` içine HERHANGİ bir dosya (tatil-aralığı operasyonu dâhil — bkz. D-HOLIDAY), CyclePeriod aggregate/contract/flag yazımı, hesaplanan değerlerin PERSIST edilmesi, WC precedence mantığının CRM'de yeniden yazılması, backfill/migration script, Mongo hand-edit, ocelot.json yazımı, RBAC seed/grant, registry yazımı."
owner: module-pack-author
branch: feature/crm/mod-0155-fu07-cycle-capacity-monthly-redesign
started: 2026-08-28
target: TBD (kullanıcı onayı + ready-for-dev flip sonrası)
form_field_count: 9
predecessor: MOD-0155-FU06 (Cycle Capacity — SHIPPED, status review)
dependencies:
  - MOD-0155-FU06 (ZORUNLU ÖNCÜL — aggregate, calculator, estimator, preview, Compact UI; bu FU onu YENİDEN ŞEKİLLENDİRİR)
  - MOD-0165-FU06/FU07 (CyclePeriod — SALT-OKUNUR, DEĞİŞMEZ)
  - CAND-CAP-0008 (Working Calendar — SALT-OKUNUR; tatil TARİHLERİ için yeni bir operasyon gerekir → D-HOLIDAY / F-WC-HOLIDAY-RANGE)
  - MOD-0048 (reference data — değişiklik yok)
  - MOD-0018 (RBAC — yeni anahtar YOK)
  - DEV-0001 (Golden Reference Compact — 9 alan, Compact kararı DEĞİŞMEZ)
---

# MOD-0155-FU07 — Cycle Capacity Monthly Redesign

> **✅ RUNTIME YETKİLENDİRİLDİ ve TESLİM EDİLDİ (2026-08-28).** `status: review`. Teslim kaydı **§0.4**'tedir;
> **D-HOLIDAY = C-ertelenmiş** onaylandı ve uygulandı.
>
> FU06 kapasiteyi kurdu; bu FU onu **okunabilir** hâle getirir. Tek cümlelik amaç:
> *"Bir ayın girdisi ve o girdinin sonucu AYNI satırda görünsün."*
>
> ✅ **D-HOLIDAY KARARA BAĞLANDI (§0.3): C-ertelenmiş.** Kolon *"Çalışılmayan gün"* olarak teslim edildi (çıkarım,
> ek çağrı yok); tatil **tarihleri/tooltip** `F-WC-HOLIDAY-RANGE`'e bırakıldı ve `services/Diten.Platform/**`
> içine hiçbir dosya yazılmadı.

---

## 0.4 Delivery Record (2026-08-28)

> **RUNTIME AUTHORIZATION (2026-08-28).** Kullanıcı `@orchestrator` ile `ready-for-dev` + `runtime_code_allowed: true`
> yetkisi verdi ve **D-HOLIDAY = C-ertelenmiş**'i onayladı. Uygulama pack'e harfiyen uyularak yapıldı; aşağıdaki üç
> sapma dışında hiçbir karar değişmedi.

**Teslim edilen yüzeyler.** Backend: `Domain/Entities/CycleCapacity.cs` (root `Fte`/`FteSource` **KALDIRILDI** →
`CycleCapacityMonth.Fte`/`FteSource`; +`LegacyElements` +`EnsureMonthlyFte` +`LegacyRootFte` +`MonthFteInvalid`) ·
`Rules/CycleCapacityCalculator.cs` (+`CalendarDays` +`NonWorkingDays` +per-ay `Fte`; `CapacityCalculation.Fte`
**KALDIRILDI**; `Visits(...)` artık satırın kendi FTE'sini okur) · `CycleCapacityValidation.cs`
(+`ValidateStampedMonthFte`) · `Services/CycleCapacityWriteValidator.cs` (FTE'yi **her aya** damgalar) ·
`CycleCapacityModels.cs` + `CycleCapacityMapper.cs` · `Handlers/**` (Create/Preview) ·
`Persistence/Repositories/CycleCapacityRepository.cs` (**her okumada** `EnsureMonthlyFte`) ·
`Persistence/DependencyInjection.cs` (`MapExtraElementsProperty`).
Frontend: `Models/CRM/CycleCapacity{FormViewModels,ViewModels,CalculationViewModel}.cs` ·
`Views/CRM/CycleCapacities/{_Form,Details}.cshtml` (**tek tablo, 13 kolon**; ayrı `#livePreview` bloğu **SİLİNDİ**;
`FieldForceSection` kartı **KORUNDU**, açıklamaya döndü) · `wwwroot/assets/js/CRM/CycleCapacities/form.js`
(satır-içi render, **yalnız read-only hücreler**) · 7 dil RESX (**72 anahtar × 7**; +`NonWorkingDays`,
+`FteMovedToMonthly`, −`LivePreviewTitle`).
Tests: `CycleCapacityRuntimeTests.cs` — 49 → **58** (9 yeni: per-ay FTE, NonWorkingDays çıkarımı/fail-closed/
sıfır-ek-çağrı, read-time migration ×4, cycle-wide FTE yokluğu).

**Pack'ten sapmalar (üçü de daraltıcı veya düzeltici):**

| # | Sapma | Gerekçe |
|---|---|---|
| **S1** | Pack §3, `CycleCapacityEstimator`'a "takvim günü sayısı" eklenmesini öngörüyordu; **eklenmedi** | `ResolvedMonth` zaten `MonthWindow`'u (RangeStart/RangeEnd) taşıyor, `CalendarDays` ondan **türetiliyor**. Bir veriyi iki yerden taşımak, ikisinin ayrışabileceği anlamına gelirdi. Estimator hiç değişmedi |
| **S2** | Pack §12, ay FTE'sini **girdi** üzerinde doğrulamayı öngörüyordu; doğrulama **damgalanmış değere** taşındı (`ValidateStampedMonthFte`) | Request FTE **taşımıyor** (taşımamalı da). Gelmeyen bir alanı doğrulamak, guard gibi görünen ölü koddur. Gerçek risk config'in aralık dışı bir değer vermesi — o da tam olarak burada yakalanıyor |
| **S3** | `LivePreviewTitle` RESX anahtarı **silindi** | Başlığı olduğu blok tabloya birleşince tüketicisi kalmadı. Kullanılmayan anahtarı 7 dilde taşımak, çeviri borcunu sahte gösterir |

**Doğrulama (ham çıktılar).**

```text
$ py .antigravity/scripts/verify_module_id.py . --check-id MOD-0155-FU07 --name "Cycle Capacity Monthly Redesign" --parent MOD-0155
OK  MOD-0155-FU07: proven against Blueprint/registry.        REAL_EXIT=0
$ py .antigravity/scripts/verify_module_id.py . --check-all  →  [HARD violations: 0]

$ py .antigravity/scripts/verify_datatable_page.py . --area CRM --module CycleCapacities --reference compact --api-profile proxy
   8 FAIL — kumesi CyclePeriods ile BIREBIR AYNI (baseline korundu)
   [PASS] Compact _Form.cshtml matches Details.cshtml section/card map
   [PASS] Optional numeric/date fields use nullable ViewModel types
   [PASS] Required label markers match ViewModel required metadata

$ dotnet build services/Diten.CrmService/src/Diten.CrmService.Api   ->  0 error
$ dotnet build frontend/Diten.Web                                   ->  exit=0, 0 error
$ dotnet test --filter CycleCapacity   ->  Basarisiz: 0, Basarili: 58
$ dotnet test (tam suite)              ->  Basarisiz: 0, Basarili: 1399, Atlanan: 5

$ grep -rn "CAND-CAP" --include=*.cs services/Diten.CrmService/ (yayinlanan literal)  ->  0
$ CyclePeriodFeatureFlags: SupportsWorkingCalendarIntegration=false, SupportsWorkingDayCount=false  (DEGISMEDI)
$ Protected SOURCE dosyalari (Features/CyclePeriod, Diten.Platform/src, Views+js/CRM/CyclePeriods)  ->  FU07 hicbirine dokunmadi
   (CyclePeriods/index.js'in mtime'i onceki returnTo artisindan; icerigindeki tek "FU07" gecisi MOD-0165-FU07 basligi)
$ RESX parity: 72 anahtar x 7 dil
```

**AC-F-5 kanıtı (en kritik iddia).** `T55_Migrated_Legacy_Row_Reproduces_The_Fu06_Figure`: kök FTE'si 12.00 olan,
ay FTE'si olmayan bir FU06 dokümanı, config-default'u **bilerek farklı** (1.00) verilerek okunuyor ve FU06'nın altın
örnek sayısını — **3036** — birebir üretiyor. Migration config'i kullansaydı bu test kırmızı olurdu.

**Açık kalan:** **F-WC-HOLIDAY-RANGE** (tatil tarihleri/tooltip — §8.4'te sözleşme hazır) · F-FTE-HR · F-FTE-BU ·
F-REGISTRY · F-RBAC · F-RBAC-WC · F-WC-BULK · F-APPROVAL · F-ACTUALS · F-SCENARIO · F-MICROTARGET-SEAM ·
F-WC-ORG-UNIT · F-FILE-DRIFT · F-NAV (§20).
Authenticated smoke (§17) **kullanıcı tarafından** çalıştırılır: fleet'in bu FU'nun build'iyle yeniden başlatılmasını,
bir dönem ve yayınlanmış bir çalışma takvimi bulunmasını gerektirir.

---

## 0.5 Delivery Increment — donmuş kolonlar + fill-down (2026-08-28)

Kullanıcı isteği üzerine iki ek. **SAF FRONTEND**: backend, aggregate, calculator, contract ve CyclePeriod
DEĞİŞMEDİ (içerik taraması: `dt-frozen` / `fillDown` / `dataset.touched` `services/` altında **0 kaynak dosyada**;
yalnız eski geçici build klasörlerindeki üçüncü-parti bir DLL'de byte olarak geçiyor).

**① Donmuş kolonlar (Excel freeze-panes).** `AY` + `ÇALIŞMA GÜNÜ` `position: sticky` ile sabitlendi; yatay kaydırma
`ÇALIŞILMAYAN GÜN`'den itibaren başlıyor. `thead` ve `tbody` aynı `left` offset'lerini paylaşıyor, `z-index` başlığı
gövde hücrelerinin, ikisini de kayan içeriğin üstünde tutuyor.

Kurallar **`backbone-custom.css`**'e yazıldı, view'a gömülmedi — tekrar kullanılabilir tablo stilinin sayfaya
gömülmemesi yerleşik frontend kuralı, ve `_Form` ile `Details`'in donmayı **birebir aynı** render etmesi ancak tek bir
kaynaktan gelirse garanti edilir. Sınıf sözleşmesi: `.dt-frozen-table` (kapsayıcı) + `.dt-frozen.dt-frozen-1` /
`.dt-frozen.dt-frozen-2` (hücreler).

Üç karar gerekçesiyle:

| Karar | Neden |
|---|---|
| Kolon genişlikleri **CSS değişkeni** (`--dt-frozen-col-1/2`) | Sticky bir hücrenin `left`'i mutlak bir offset olmak zorunda; ikinci kolon ancak birincinin genişliği bilinirse konumlanabilir. Değişken, sayfanın bu kuralı düzenlemeden ayarlayabilmesi için |
| Arka plan **opak** (`--bs-card-bg`) | Şeffaf bir sticky hücrenin altından kayan kolonlar görünür ve bu bir render hatası gibi okunur |
| `max-width: 575.98px` altında **freeze BIRAKILIR** | Ekranın çoğunu yiyen bir dondurma, dondurmamaktan kötüdür; tablo bütün olarak kayar |

**② Fill-down.** İlk ayın bir girdi kolonuna (Toplantı / Eğitim / İzin / Mikro-hedefleme günü / dk) yazılan değer,
alttaki ayların **aynı** kolonuna kopyalanır — yazarın elle yapacağı şey. Üç kural taşıyor:

1. **Yazarın dokunduğu hücre ASLA ezilmez.** İlk ay dışındaki bir hücreye yazıldığı anda `data-touched="1"` işaretlenir
   ve bir daha doldurulmaz. Kolaylık, yazılmış bir kararı yok edebiliyorsa kolaylık değildir.
2. **Kopyalama, yazma gibi görünmez.** `.value` ataması `input` olayı üretmez; üretseydi doldurulan her hücre anında
   kendini "touched" işaretler ve bir sonraki fill-down hiçbir şey yapmazdı.
3. **Kolon adı NAME'den okunur** (`Months[3].MeetingDays` → `MeetingDays`), pozisyondan değil — ayın `(Year, Month)` ile
   adreslenmesiyle aynı gerekçe: yeri değişen bir kolon sessizce başka bir alanı doldurmaya başlamamalı.

Fill-down mevcut **500 ms debounce**'lu canlı tahmini de tetikler (ayrı bir yol yok).

**Doğrulama.**

```text
$ dotnet build frontend/Diten.Web                     ->  exit=0, 0 error
$ node --check .../CycleCapacities/form.js            ->  OK
$ node --check .../CycleCapacities/index.js           ->  OK
$ verify_datatable_page CycleCapacities compact proxy ->  8 FAIL (CyclePeriods baseline ile BIREBIR AYNI)
       [PASS] Compact _Form.cshtml matches Details.cshtml section/card map
       [PASS] Optional numeric/date fields use nullable ViewModel types
       [PASS] Required label markers match ViewModel required metadata
$ dotnet test (--no-build; backend degismedi)         ->  Basarisiz: 0, Basarili: 1399, Atlanan: 5
$ grep -rln "dt-frozen|fillDown|dataset.touched" services/  ->  0 kaynak dosya
```

Donmuş kolonların ve fill-down'ın **görsel/etkileşimli** doğrulaması kullanıcıdadır: yatay kaydırmada AY + Çalışma
günü sabit kalmalı; ilk aya değer girilince alttakiler dolmalı; elle düzenlenmiş bir hücre bir daha ezilmemeli.

---

## 0.6 Delivery Increment — iki-uç freeze + full-width aylık plan (2026-08-28)

Kullanıcı isteği üzerine üç düzen değişikliği. **SAF FRONTEND** (view + CSS): backend, aggregate, calculator ve
CyclePeriod DEĞİŞMEDİ — içerik taraması `dt-frozen-r` / `Round-2 layout` için `services/` altında **0 kaynak dosya**.

**① Sağ freeze — iki-uç (Excel) dondurma.** Sol blok (AY + ÇALIŞMA GÜNÜ) yerinde kalırken sonuç bloğu da sağ kenara
sabitlendi: **DÜŞÜLEN · SAHA GÜNÜ · ZİYARET DK · ZİYARET**. Böylece kimlik solda, cevap sağda sabit; yazarın
düzenlediği yedi kolon (Çalışılmayan gün, Toplantı, Eğitim, İzin, Mikro-hedefleme günü/dk, FTE) ikisinin arasında
kayar. `r1` en sağdaki kolondur; offset'ler sağ kenardan sola doğru birikir.

| Karar | Neden |
|---|---|
| Sağ bloğun kolon genişlikleri **eşit** (`--dt-frozen-right-col`) | Eşit genişlik, sticky offset'leri basit katlara çevirir: kolon eklemek/çıkarmak dört sayı yerine **bir** sayı değiştirmek olur |
| Breakpoint `sm` (576px) → **`lg` (992px)** | Artık **altı** kolon donuyor (2 sol + 4 sağ). Tablet genişliğinde kayacak neredeyse hiçbir şey kalmazdı, ve kaydırması olmayan bir freeze yalnızca dar bir tablodur |
| `tfoot`'taki `colspan="12"` **bölündü** | Kapsayan (spanning) bir hücre sticky olamaz. Etiket sol donmuş bloğa, dönem toplamı **topladığı kolonun altına** alındı: `1 + 1 + colspan 7 + 4 = 13` |

**② Aylık Plan kartı full-width.** Kendi satırında `col-12`. On üç kolonluk bir tablodan sayfanın üçte ikisinde
yaşaması isteniyordu — yatay kaydırmayı okunmaz yapan asıl sebep buydu.

**③ Bağlı dönem kartı sağ sidebar'a.** Sidebar artık **üç bağlam kartı** taşıyor: bu kapasite neye ait, kim çalışıyor,
ne not düşülmüş. Sol ana sütunda aktivite bütçesi kaldı.

Yeni düzen: **üst satır** (sol: Aktivite bütçesi · sağ: Bağlı dönem + Saha ekibi + Notlar) → **alt satır**: Aylık Plan
tam genişlik.

**Parite.** Her iki dosya da AYNI blok sırasından yeniden kuruldu, bu yüzden ilan ettikleri section haritası birebir
aynı — verifier bunu doğruluyor:

```text
_Form    ActivityBudgetSection PinnedPeriodSection FieldForceSection NotesSection MonthlyPlanSection
Details  ActivityBudgetSection PinnedPeriodSection FieldForceSection NotesSection MonthlyPlanSection
```

FU06 tuzağı korundu: yeni hiçbir `h6.text-uppercase.text-heading.fw-semibold` eklenmedi.

**Doğrulama.**

```text
$ dotnet build frontend/Diten.Web                     ->  exit=0, 0 error
$ node --check form.js / index.js                     ->  OK
$ verify_datatable_page CycleCapacities compact proxy ->  8 FAIL (CyclePeriods baseline ile BIREBIR AYNI)
       [PASS] Compact _Form.cshtml matches Details.cshtml section/card map
       [PASS] Optional numeric/date fields use nullable ViewModel types
       [PASS] Required label markers match ViewModel required metadata
$ kolon sayimi: _Form thead 13 / tbody 13 / tfoot 13 ; Details thead 13 / tbody 13
$ dotnet test (--no-build)                            ->  Basarisiz: 0, Basarili: 1399, Atlanan: 5
$ grep -rln "dt-frozen-r|Round-2 layout" services/    ->  0 kaynak dosya
```

Görsel doğrulama kullanıcıdadır: yatay kaydırmada hem sol (AY + Çalışma günü) hem sağ (Düşülen → Ziyaret) sabit
kalmalı, Aylık Plan tam genişlik olmalı, sağ sidebar'da üç kart bulunmalı.

---

## 0.7 Delivery Increment — Notlar sol ana sütuna (2026-08-28)

Tek taşıma, **saf frontend**: `NotesSection` kartı sağ sidebar'dan sol ana sütuna, Aktivite bütçesinin altına alındı.

**Sonuç düzen.** Sol ana sütun artık yazarın **yazdığı** iki kartı taşıyor (Aktivite bütçesi + Notlar); sağ sidebar
**bağlam** kartlarına indi (Bağlı dönem + Saha ekibi = **2 kart**); Aylık Plan alttaki tam genişlik satırında kaldı.

**Parite.** Her iki dosyada da aynı iki işlem uygulandı (Notlar bloğunu kaldır → Aktivite bloğunun hemen ardına koy),
bu yüzden ilan edilen section haritası birebir aynı kaldı:

```text
_Form    ActivityBudgetSection NotesSection PinnedPeriodSection FieldForceSection MonthlyPlanSection
Details  ActivityBudgetSection NotesSection PinnedPeriodSection FieldForceSection MonthlyPlanSection
```

**Doğrulama.**

```text
$ dotnet build frontend/Diten.Web                     ->  exit=0, 0 error
$ verify_datatable_page CycleCapacities compact proxy ->  8 FAIL, CyclePeriods baseline ile BIREBIR AYNI
       [PASS] Compact _Form.cshtml matches Details.cshtml section/card map
       [PASS] Optional numeric/date fields use nullable ViewModel types
       [PASS] Required label markers match ViewModel required metadata
$ etiket dengesi   _Form: div 43/43, section 5/5    Details: div 61/61, section 5/5
$ yasak h6 sayisi degismedi: her iki dosyada 5 (mevcut bes section basligi; yeni eklenmedi)
```

---

## 0.1 DCP-002 — PASS (2026-08-28)

```text
$ py .antigravity/scripts/verify_module_id.py . --check-id MOD-0155-FU07 --name "Cycle Capacity Monthly Redesign" --parent MOD-0155
OK  MOD-0155-FU07: proven against Blueprint/registry.
REAL_EXIT=0
```

**Fail-closed kanıtı** (kontrol koşusu):

```text
$ py .antigravity/scripts/verify_module_id.py . --check-id MOD-9999-FU07 --name "X" --parent MOD-9999
BLOCKED  MOD-9999-FU07
   - parent MOD-9999 not found in Blueprint or registry
Gate failed closed. See DCP-002.
REAL_EXIT=2
```

**FU numarası:** `grep -rno "MOD-0155-FU[0-9A-Z]*" execution/ docs/` → FU01–FU06 kullanımda. İlk çakışmayan id
**FU07**. Geçit **kimliği** doğrular, açıklayıcı adı değil (FU06 §0.1'de belgelendiği gibi); parent'ın kanonik adı
**"Field Sales / Visit Planning"**'dir ve değişmez. **Registry satırı bu pack tarafından EKLENMEZ** → F-REGISTRY.

---

## 0.2 Neden ayrı bir FU (ve neden FU06'ya yama değil)

Üç şeyin üçü de **davranış değiştiriyor**, ikisi de **veri modeline dokunuyor**:

1. `Fte` root'tan aya taşınıyor — **şema değişikliği** ve hesap girdisinin anlamı değişiyor.
2. Hesap motorunun imzası değişiyor — `CycleCapacityCalculator` per-ay FTE alacak.
3. Ayrı "Canlı Tahmin" tablosu **kaldırılıyor** — FU06'nın teslim ettiği bir yüzey siliniyor.

FU06 `status: review`'da ve bir teslim kaydı taşıyor. Onun içine yazmak, ne teslim edildiğini geriye dönük
belirsizleştirirdi. Bu FU **FU06'yı yeniden şekillendirir** ve bunu açıkça yapar.

---

## 0.3 ⚠️ D-HOLIDAY — KULLANICI KARARI GEREKTİRİR (tek açık madde)

Kilitli karar şöyleydi: *"Tatiller (b): YENİ WC seam/query — ayın tatil **tarihlerini** döndürür … Tabloda: tatil
sayısı kolonu + tooltip'te tarihler."* Kodu okudum; **tarihler bugün bu domain'den alınamıyor.**

### Bulgu 1 — Working Calendar'ın aralık operasyonu YOK

`WorkingCalendarOperations` tam olarak beşini tanır:

```text
is-working-day · is-holiday · next-working-day · add-working-days · working-days-between
```

`is-holiday` **tek bir günü** alır. `working-days-between` yalnız bir **sayı** döner
(`WorkingDayCountResult(Resolution, int? Count, …)`) — hangi günlerin neden düştüğünü söylemez.

### Bulgu 2 — üç yol var, üçü de sorunlu

| Yol | Maliyet | Doğruluk | Sınır |
|---|---|---|---|
| **A — gün gün `is-holiday`** | Ay başına **28–31** HTTP çağrısı; 12 aylık dönemde **~365**. Her biri 3 sn bütçeli | ✅ Doğru (WC'nin kendi resolve engine'i) | ✅ Temiz | 
| **B — iki takvim katmanını okuyup CRM'de birleştir** | (ülke, yıl) başına **2–3** çağrı | ⚠️ **CRM, WC'nin precedence'ını yeniden yazar** | ❌ **Sınır ihlali** |
| **C — WC'ye aralık operasyonu ekle** (`op=holidays-between`) | Ay başına **1** çağrı (mevcut çağrıyla birleşebilir) | ✅ Doğru | ❌ **`services/Diten.Platform/**` bu domain için PROTECTED** |

**B neden ihlal:** `WorkingCalendarResolveEngine` 174 satırlık, **6 kademeli** bir öncelik zinciridir —
tenant working-day-override > ülke working-day-override > tenant tatil/kapanış > ülke tatil > efektif hafta sonu >
çalışma günü; artı `ObservedDate`, `Recurrence`, `DayStatus` ve "null = miras al, boş liste = hafta sonu yok" ayrımı.
Bunu CRM'de kopyalamak, WC capability'sinin **var olma sebebini** ortadan kaldırır ve ilk WC değişikliğinde
sessizce sapar. (Not: tenant `GET /overrides/{id}` **aktif ülke satırlarını da** salt-okunur döndürüyor ve
`WorkingCalendarDto.Days` gerçekten dolu — yani B **teknik olarak mümkün**. Reddedilme sebebi teknik değil,
mimaridir.)

### Bulgu 3 — SAYI zaten bedava ve KESİN, ama adı "tatil" değil

Ayın **çalışılmayan gün** sayısı, hâlihazırda yaptığımız tek çağrıdan **çıkarımla** elde edilir:

```
nonWorkingDays(m) = (rangeEnd − rangeStart + 1) − wcWorkingDays(m)
```

Bu **kesindir** (WC'nin kendi sayısından türer, tahmin değil) ve **sıfır ek çağrı** maliyetindedir. Ama hafta sonu +
resmî tatil + şirket kapanışını **birlikte** sayar. Ona "Tatil" demek yanlış olur; **"Çalışılmayan gün"** demek
tamamen doğrudur.

### Öneri — **D-HOLIDAY = C-ertelenmiş + interim**

| | Bu FU'da |
|---|---|
| **Kolon** | **"Çalışılmayan gün"** (`NonWorkingDays`), yukarıdaki çıkarımla. Kesin, bedava, yanıltıcı değil |
| **Tooltip (tarihler)** | **AÇILMAZ** — `F-WC-HOLIDAY-RANGE` altında Working Calendar capability'sine bırakılır |
| **Hazırlanan** | Seam sözleşmesi bu pack'te **tam olarak yazılıdır** (§8.4), böylece WC operasyonu geldiği gün CRM tarafı tek dosyalık iş olur |

**Kullanıcı yine de (b)'yi isterse** iki alt seçenek var ve ikisi de bu FU'nun dışına taşar:
**(b-1)** Ayrı bir Working Calendar FU'su açılır (`op=holidays-between`, §8.4'teki sözleşme) ve bu FU ona bağlanır —
**önerilen**; **(b-2)** A yolu (gün gün) kabul edilir ve tooltip **yalnız talep üzerine** (tıklayınca) yüklenir, ki
bu ay başına ~30 çağrıyı kullanıcı eylemine bağlar. **B hiçbir koşulda önerilmez.**

---

## 1. Module Summary

FU06'nın kapasite ekranı iki tablo gösteriyor: yazarın doldurduğu **Aylık Plan** ve altında ayrı bir **Canlı Tahmin**.
Aynı ay iki satırda, iki tabloda. Bu FU onları **tek tabloda** birleştirir ve tek bir soruyu her satırda cevaplar:
*"Bu ayda ne var, ve bundan kaç ziyaret çıkıyor?"*

Üç değişiklik:

1. **FTE aya iner.** `CycleCapacity.Fte` (tek, root) → `CycleCapacityMonth.Fte` (her ay). Hâlâ **DISABLED** ve hâlâ
   config-ortalaması; ama modelin şekli artık gerçeği taşıyor — saha ekibi mevsimlik değişir, tek sayı bunu
   söyleyemez. HR entegrasyonu geldiğinde **alan zaten yerinde** olacak (F-FTE-HR).
2. **Çalışılmayan gün görünür olur.** Yazar 21 çalışma gününü nereden çıktığını göremeden 4 gün düşüyordu; artık ayın
   kaç günü zaten çalışılmıyor, tabloda.
3. **Tablolar birleşir.** Girdi kolonları editable, hesaplanan kolonlar read-only, hepsi **canlı** (FU06'nın 500 ms
   debounce preview'ı; ayrı tablo yerine satır-içi).

**Bu FU yeni bir yetenek AÇMAZ.** Hesabın anlamı, fail-closed politikası ve sınırları FU06'da ne ise o kalır.

---

## 2. Ownership and Boundaries

### 2.1 In-scope / Out-of-scope

| Kapsam | Karar |
|---|---|
| **In-scope** | `CycleCapacityMonth.Fte` (+ root `Fte`'nin kaldırılması) · class-map · **read-time** migration · `CycleCapacityCalculator` per-ay FTE · `NonWorkingDays` çıkarımı · birleşik Aylık Plan tablosu (computed kolonlar satır-içi, canlı) · preview'ın aynı kolonları döndürmesi · contract/limit güncellemesi · 7 dil RESX · boundary testleri |
| **Out-of-scope (AÇIKÇA ERTELENİR)** | Tatil **tarihleri** / tooltip (**F-WC-HOLIDAY-RANGE**, §0.3) · per-ay FTE'nin **düzenlenebilir** olması (**F-FTE-HR** — alan açılır, kilit açılmaz) · gerçek HR entegrasyonu · per-BusinessUnit FTE (**F-FTE-BU**) · approval (**F-APPROVAL**) · actuals (**F-ACTUALS**) · senaryo (**F-SCENARIO**) · WC toplu ay endpoint'i (**F-WC-BULK**) |

### 2.2 FU06'dan DEĞİŞMEYENLER (kilitli)

| Kural | Durum |
|---|---|
| `TotalVisitNumber` **read-time projeksiyon**, ASLA persist edilmez | **KORUNUR** — aggregate'te hâlâ hiçbir visit/working-day alanı yok |
| Preview **transient**, hiçbir şey yazmaz (handler repository almaz) | **KORUNUR** |
| WC **fail-closed**: `/overrides/resolve`, bir ay çözülemezse tümü 503, sahte gün YOK | **KORUNUR** — `NonWorkingDays` de aynı sayıdan türediği için aynı kaderi paylaşır |
| **CyclePeriod backend DOKUNULMAZ**; `SupportsWorkingCalendarIntegration` / `SupportsWorkingDayCount` **`false`** | **KORUNUR** — bu FU CyclePeriod'un frontend'ine bile dokunmaz (FU06'nın satır-aksiyonu zaten yerinde) |
| `CalendarCountryCode` = takvim parametresi, **scope değil** | **KORUNUR** |
| 1:1 pin, archive-frees-period, kendi lifecycle'ı yok | **KORUNUR** |
| Estimate beyanı (3 yerde) + `IsEstimate: true` | **KORUNUR** |
| `CycleCapacityEstimator` **tek** fail-closed politika (saved + preview) | **KORUNUR ve genişletilir** |
| `services/Diten.Platform/**` protected | **KORUNUR** — D-HOLIDAY'in tüm sebebi budur |

---

## 3. Owned Objects (değişenler)

| Nesne | Dosya | Değişiklik |
|---|---|---|
| `CycleCapacity` | `Domain/Entities/CycleCapacity.cs` | `Fte` ve `FteSource` **KALDIRILIR** (aya taşınır); `MonthlyFteSource()` yardımcıları eklenir |
| `CycleCapacityMonth` | aynı dosya | **`Fte`** (+`FteSource`) **EKLENİR** |
| `CycleCapacityLimits` | aynı dosya | değişmez (`MinFte`/`MaxFte` zaten var) |
| `CycleCapacityCalculator` | `Rules/CycleCapacityCalculator.cs` | `Visits(...)` çağrısı `capacity.Fte` yerine **`input.Fte`**; `CapacityCalculation.Fte` → `MonthCalculation.Fte`; `MonthCalculation` **+`NonWorkingDays`** |
| `CycleCapacityEstimator` | `Services/CycleCapacityEstimator.cs` | `ResolvedMonth`'a **takvim günü sayısı** eklenir (NonWorkingDays çıkarımı için) |
| `CycleCapacityValidation` | `CycleCapacityValidation.cs` | ay satırı doğrulamasına FTE aralığı eklenir |
| DTO'lar | `CycleCapacityModels.cs` | `CycleCapacityMonthDto` +`Fte`; `CycleCapacityMonthCalculationDto` +`NonWorkingDays` +`Fte`; `CycleCapacityDetailDto` −`Fte`/−`FteSource`; `CycleCapacityCalculationDto` −`Fte` |
| Handler'lar | Create / Update / Preview | FTE'yi **her aya** damgalar (config default) |
| Persistence | `DependencyInjection.cs` | `CycleCapacityMonth` class-map'i FTE ile birlikte (decimal — Guid serializer gerekmez) |
| Frontend | `_Form.cshtml` · `Details.cshtml` · `form.js` · ViewModel'ler · 7 RESX | birleşik tablo; ayrı `#livePreview` bloğu **SİLİNİR** |

---

## 4. Entity Fields (değişen kısım)

### 4.1 `CycleCapacity` — kaldırılan

| Alan | Karar |
|---|---|
| `Fte` (decimal) | **KALDIRILIR** → `CycleCapacityMonth.Fte` |
| `FteSource` (string) | **KALDIRILIR** → `CycleCapacityMonth.FteSource` |

### 4.2 `CycleCapacityMonth` — eklenen

| Alan | Tip | Zorunlu | Kural |
|---|---|---|---|
| `Fte` | `decimal(6,2)` | Evet | `MinFte`(0.01) ≤ Fte ≤ `MaxFte`(9999). **UI'da DISABLED**, sunucu config-default'undan damgalar, payload **yok sayılır** — FU06'daki root FTE ile **birebir aynı rejim**, yalnız satır başına |
| `FteSource` | `string` | sistem | `CycleCapacityFteSources.InterimDefault` (v1'de daima) |

**Form alan sayımı (`form_field_count: 9`).** FTE başlıktan çıktı: `CyclePeriodId`, `CalendarCountryCode`,
`DailyWorkMinutes`, `PromoProductTime`, `NonPromoProductTime`, `TravelingTime`, `ReportDuration`, `QuizDuration`,
`Description`. **9 > 8 ⇒ `golden_reference: compact` DEĞİŞMEZ.** Ay grid'i (FTE kolonu dâhil) embedded child
grid'dir ve sayılmaz.

### 4.3 D-MIGRATION — **read-time**, backfill YOK

FU07-CyclePeriod'un `EnsureScopeType()` deseninin birebir aynısı, ve aynı sebeple:

```csharp
// Okuma anında normalleştirme — MIGRATION DEĞİL. Hiçbir şey geri yazılmaz.
// FU06 satırlarında ay FTE'si yoktur; kök FTE'leri vardı ve o değer HER aya kopyalanır.
// Böylece eski bir kayıt, düzenlenene kadar FU06'daki sayının AYNISINI üretmeye devam eder.
public CycleCapacity EnsureMonthlyFte(decimal configuredDefault) { … }
```

| Durum | Davranış |
|---|---|
| Ay satırında `Fte > 0` | Olduğu gibi bırakılır |
| Ay satırında FTE yok **ve** dokümanda eski kök `Fte` var | Kök değer **her aya kopyalanır** (veri kaybı yok, sayı değişmez) |
| İkisi de yok (teorik) | Config-default kullanılır |

Eski kök `Fte` alanı entity'den kaldırılacağı için Mongo dokümanındaki değere **ham BSON üzerinden** erişmek gerekir;
bu, class-map'te `SetIgnoreExtraElements(false)` + bir `ExtraElements` sözlüğü ya da bir `[BsonExtraElements]` alanı
ile yapılır ve **§18'de açık bir ready-for-dev maddesidir**. Değer yalnız **okunur**; geri yazılması yalnızca satır
kendi sebebiyle güncellendiğinde olur.

> **Neden backfill script yok:** repo'nun yerleşik kuralı (CyclePeriod FU07 emsali). Bir script, Mongo'ya elle
> dokunmayı gerektirir ve `runtime_code_scope` bunu yasaklar.

---

## 5. Hesap Sözleşmesi (değişen kısım)

### 5.1 Per-ay FTE

```diff
- TotalVisitNumber(m) = round( visitMinutes(m) ÷ minutesPerVisit × capacity.Fte )
+ TotalVisitNumber(m) = round( visitMinutes(m) ÷ minutesPerVisit × month.Fte )
```

`minutesPerVisit` ve günlük dakika bütçesi **dönem-geneli kalır** — bunlar bir ziyaretin/bir günün şekli, ekibin
büyüklüğü değil. Değişen yalnız çarpan.

**`CycleCapacityCalculator` saf kalır.** İmza değişmez (`Calculate(capacity, resolvedMonths)`); yalnız `Visits(...)`
çağrısı satırın kendi FTE'sini okur. `CapacityCalculation.Fte` (dönem-geneli tek değer) **kaldırılır** — artık böyle
bir sayı yoktur; onun yerine her `MonthCalculation` kendi `Fte`'sini taşır.

### 5.2 `NonWorkingDays` — çıkarım, ölçüm değil

```
calendarDays(m)    = (RangeEnd − RangeStart) + 1        // dönem penceresine kırpılmış
nonWorkingDays(m)  = calendarDays(m) − wcWorkingDays(m)
```

**Kesin** (WC'nin kendi sayısından türer) ve **ek çağrı yok**. Hafta sonu + resmî tatil + kapanışı **birlikte** sayar,
bu yüzden kolon adı **"Çalışılmayan gün"**dür, "Tatil" değil (§0.3).

`wcWorkingDays` çözülemezse — yani ay unresolved ise — `nonWorkingDays` de **hesaplanmaz**: fail-closed politika
bölünmez.

---

## 6. Repo Scope

```text
services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/CycleCapacity.cs          (DEĞİŞİR)
services/Diten.CrmService/src/Diten.CrmService.Application/Features/CycleCapacity/
├── Rules/CycleCapacityCalculator.cs                                                     (DEĞİŞİR)
├── Services/CycleCapacityEstimator.cs                                                   (DEĞİŞİR)
├── CycleCapacityValidation.cs · CycleCapacityModels.cs · CycleCapacityMapper.cs          (DEĞİŞİR)
├── Contract/CycleCapacityContract.cs · Contract/CycleCapacityFeatureFlags.cs             (DEĞİŞİR)
└── Handlers/{CommandHandlers,QueryHandlers}/**                                           (DEĞİŞİR)
services/Diten.CrmService/src/Diten.CrmService.Persistence/DependencyInjection.cs         (DEĞİŞİR — class-map)
services/Diten.CrmService/tests/**/CycleCapacityRuntimeTests.cs                           (DEĞİŞİR + yeni testler)

frontend/Diten.Web/
├── Models/CRM/CycleCapacityFormViewModels.cs                                             (DEĞİŞİR — Fte aya iner)
├── Models/CRM/{CycleCapacityViewModels,CycleCapacityCalculationViewModel}.cs             (DEĞİŞİR)
├── Views/CRM/CycleCapacities/{_Form,Details}.cshtml                                      (DEĞİŞİR — tablolar birleşir)
├── wwwroot/assets/js/CRM/CycleCapacities/form.js                                          (DEĞİŞİR — satır-içi render)
└── Resources/Views/CRM/CycleCapacities/CycleCapacitiesIndex.{7 dil}.resx                  (DEĞİŞİR)
```

---

## 7. Protected Paths

| Path | Neden |
|---|---|
| **`services/Diten.Platform/**`** | **Bu FU'nun en sert kilidi ve D-HOLIDAY'in tüm sebebi.** Tatil-aralığı operasyonu buraya yazılamaz |
| `services/Diten.CrmService/**/Features/CyclePeriod/**` | CyclePeriod DEĞİŞMEZ (tek dosya bile) |
| `frontend/Diten.Web/Views/CRM/CyclePeriods/**`, `.../js/CRM/CyclePeriods/**` | FU06'nın satır-aksiyonu yerinde; bu FU **dokunmaz** |
| `.antigravity/**`, `gateway/**/ocelot.json`, Archive, `_Layout.cshtml` | Global |
| Diğer domain servisleri | Global |
| `execution/registries/**` | F-REGISTRY |

---

## 8. Runtime Constraints

### 8.1 Class-map

`CycleCapacityMonth` zaten kayıtlı (`AutoMap`). Yeni `Fte` **decimal**'dir — Guid serializer gerekmez, ama
`CycleCapacity` class-map'i eski kök `Fte` alanını okuyabilmek için **extra-elements** taşımalıdır (§4.3).

### 8.2 Index

**Değişiklik yok.** `ux_cycle_capacities_tenant_cycle_period` (partial-unique, eşitlik-only) ve
`ix_cycle_capacities_tenant_country` aynen kalır. FTE hiçbir index'e girmez.

### 8.3 WC tüketimi

**Aynen FU06.** `GET /api/platform/working-calendars/overrides/resolve?op=working-days-between&…` —
ülke katmanı `/resolve` **KULLANILMAZ** (gateway `IsAdminPath`: `X-Tenant-Id` → 400, `tenant_user` → 403).
Cache yok, 3 sn bütçe, 1 transient retry, ay başına 1 çağrı. **Bu FU yeni bir WC çağrısı EKLEMEZ.**

### 8.4 F-WC-HOLIDAY-RANGE — hazır bekleyen sözleşme (bu FU'da AÇILMAZ)

Working Calendar capability'si bu operasyonu kazandığı gün CRM tarafı tek dosyalık iştir. Sözleşme:

```text
GET /api/platform/working-calendars/overrides/resolve
    ?op=holidays-between&date={from}&toDate={to}&countryCode={CC}[&legalEntityId={guid}]

→ WorkingDayResolveDto + IReadOnlyList<HolidayInfo> Holidays
  (aralıktaki her tatil/kapanış: Date, DayName, DayType, IsHalfDay, FromTenantOverride)
  Resolution semantiği working-days-between ile AYNI: kısmi liste YOK, çözülemezse Holidays boş + resolution != resolved
```

CRM tarafı: `IWorkingDayCounter`'a `HolidaysAsync(...)`, `MonthCalculation`'a `Holidays` listesi, tabloda tooltip.
**Bu pack o kodu yetkilendirmez.**

---

## 9. Layout & Shell Contract

`shell: tenant` ⇒ **`Layout = "_LayoutTenantShell"`**, dört sayfada da **AÇIKÇA** yazılı (FU06'da öyle; değişmez):
`Views/CRM/CycleCapacities/{Index,Create,Edit,Details}.cshtml`.

**Section/card haritası DEĞİŞMEZ** — `_Form` ve `Details` aynı beş kartı aynı sırada ilan etmeye devam eder:
`PinnedPeriodSection` → `ActivityBudgetSection` → `MonthlyPlanSection` → `FieldForceSection` → `NotesSection`.

> ⚠️ **Kayıtlı tuzak:** verifier, `h6.text-uppercase.text-heading.fw-semibold` şeklindeki başlıkları **section
> başlığı** sayar. Birleşik tabloya alt başlık eklenecekse o şekil **kullanılmamalıdır** (FU06'da `LivePreviewTitle`
> tam olarak bu yüzden düz bir `div`'e indirilmişti).

**`FieldForceSection` kartı ne olacak?** FTE aya indiği için kart **boşalır**. Karar: kart **KALIR** ve içeriği
*"FTE artık aylık plan tablosunda; HR entegrasyonu geldiğinde buradan iş birimi bazında yönetilecek"* açıklamasına
dönüşür. Silmek section haritasını bozar ve verifier'ı kırar; boş bırakmak da kötüdür — açıklama dürüst olanıdır.

---

## 10. Backend File Convention

FU06'nın klasör yapısı **aynen** kalır (Golden Compact birebir). **Yeni dosya yok**; yalnız mevcutlar değişir.
Naming kuralları değişmez: Handler/Validator'da `Command`/`Query` suffix **YOK**.

---

## 11. Frontend File Contract (Compact)

FU06 dosya seti **aynen** kalır. **Yeni dosya yok, dosya silinmez.** Değişen:

- `_Form.cshtml` — ayrı `#livePreview` bloğu **SİLİNİR**; ay tablosu computed kolonları kazanır.
- `Details.cshtml` — ayrı estimate tablosu **SİLİNİR**; ay tablosu tek tablo olur.
- `form.js` — satır-içi render (`#capacityMonthsTable` satırlarındaki read-only hücreleri günceller).

**Compact'ta YASAK (değişmez):** `_CreateEditOffcanvas.cshtml` · `_DetailsQuickView.cshtml`.

### 11.1 Birleşik tablo — kolon sözleşmesi

| # | Kolon | Tip | Kaynak |
|---|---|---|---|
| 1 | Ay | read-only | `(Year, MonthNumber)` |
| 2 | **Çalışma günü** | **read-only (computed)** | WC |
| 3 | **Çalışılmayan gün** | **read-only (computed)** | çıkarım (§5.2) |
| 4 | Toplantı günü | **editable** | girdi |
| 5 | Eğitim günü | **editable** | girdi |
| 6 | İzin günü | **editable** | girdi |
| 7 | Mikro-hedefleme günü | **editable** | girdi |
| 8 | Mikro-hedefleme dk/gün | **editable** | girdi |
| 9 | **FTE** | **disabled** | config-default (§4.2) |
| 10 | **Düşülen** | read-only (computed) | 4+5+6 |
| 11 | **Saha günü** | read-only (computed) | hesap |
| 12 | **Ziyaret dk** | read-only (computed) | hesap |
| 13 | **Ziyaret** | read-only (computed) | hesap |

Altında dönem toplamı (`Σ TotalVisitNumber`) + minutes-per-visit rozeti + **tahmin uyarısı** (korunur).

**Canlı davranış:** editable kolonlardan biri değişince FU06'nın **500 ms debounce**'u çalışır, preview çağrılır ve
**yalnız read-only hücreler** güncellenir — yazarın imleci hiçbir zaman kaybolmaz. Stale-response koruması
(`previewToken`) korunur. Çözülemeyen sonuçta computed hücreler **"—"** gösterir ve tablonun üstünde
`calendar_unresolved` / `calendar_forbidden` bildirimi çıkar (FU06 deseni).

---

## 12. Validation Rules (değişen)

| Field | Required | Format/Rule | Pre-check |
|---|---|---|---|
| `Months[i].Fte` | sistem | Payload **yok sayılır**; sunucu config'ten yazar. `MinFte ≤ Fte ≤ MaxFte` | — |
| `Months[i].*` (diğer) | — | **FU06 ile aynı** | — |
| Başlık alanları | — | **FU06 ile aynı** (`visit_minutes_zero`, `daily_spend_exceeds_day`, ay-pencere kuralı) | — |

**Yeni reason code:** `cycle_capacity_month_fte_invalid`. Diğerleri aynen kalır.

---

## 13. Failure Path to Verify

| Senaryo | Beklenen |
|---|---|
| **Eski kayıt (kök FTE, ay FTE yok)** | Read-time migration kök değeri her aya kopyalar; **hesaplanan sayı FU06'daki ile birebir aynı** |
| **Ay FTE'si payload'da gönderilir** | **Yok sayılır**; saklanan değer config-default (disabled alanı DOM'dan açmak işe yaramaz) |
| **Bir ay çözülemez** | Tüm hesap 503; **`NonWorkingDays` dâhil** hiçbir computed kolon değer göstermez (kısmi tablo yok) |
| **WC 403** | `calendar_forbidden`, `calendar_unresolved`'dan ayrı (F-RBAC-WC) |
| **`visit_minutes_zero`** | 400, yazma engellenir (FU06 ile aynı) |
| **Concurrency** | 409, sessiz overwrite yok |
| **Ay penceresi dışı satır** | 400 `month_out_of_period` |

---

## 14. Authorization Convention

**DEĞİŞİKLİK YOK.** `crm.cycle-capacity.read` / `.manage`, DEV-ONLY territory fallback, `.calculate` anahtarı yok.
Hesap için çağıranın `platform.working-calendar.override.read` ihtiyacı **devam eder** (F-RBAC-WC).

---

## 15. Gateway / API Routing Decision

**Karar: Gateway değişikliği GEREKSİZ.** `/api/crm/cycle-capacities` + `/{everything}` (GET/POST/PUT/OPTIONS)
FU06'da eklendi ve preview dâhil her şeyi taşıyor. Bu FU **yeni endpoint eklemez** — mevcutların gövdesi değişir.

---

## 16. Acceptance Criteria

### 16.1 Per-ay FTE

- **AC-F-1** `CycleCapacity`'de `Fte`/`FteSource` property'si **yoktur**; `CycleCapacityMonth`'ta **vardır**.
- **AC-F-2** İki farklı FTE taşıyan iki ay, **farklı** `TotalVisitNumber` üretir (per-ay çarpan gerçekten çalışıyor).
- **AC-F-3** Create/Update/Preview payload'ları FTE **taşımaz**; sunucu her aya config-default'u damgalar.
- **AC-F-4** UI'da FTE hücresi `disabled`; DOM'dan açılıp değer gönderildiğinde saklanan değer **değişmez**.
- **AC-F-5** **Read-time migration:** kök FTE'si 12.00 olan, ay FTE'si olmayan bir FU06 dokümanı okunduğunda her ay
  12.00 raporlar ve **FU06'nın ürettiği sayının aynısını** verir. Mongo'ya **hiçbir şey yazılmaz** (ham doküman
  denetimi).

### 16.2 Çalışılmayan gün

- **AC-N-1** `NonWorkingDays = calendarDays − workingDays`; kırpılmış ilk/son ayda **kırpılmış** aralık üzerinden.
- **AC-N-2** Ay çözülemezse `NonWorkingDays` **üretilmez** (fail-closed bölünmez).
- **AC-N-3** Kolon **"Tatil" diye etiketlenmez**; RESX anahtarı `NonWorkingDays` semantiğini taşır.
- **AC-N-4** Bu kolon için **hiçbir ek WC çağrısı yapılmaz** (çağrı sayısı FU06 ile aynı: ay başına 1).

### 16.3 Birleşik tablo

- **AC-T-1** `_Form.cshtml` ve `Details.cshtml`'de **ayrı estimate tablosu YOKTUR** (`#livePreview` bloğu silinmiş).
- **AC-T-2** Ay tablosu §11.1'deki 13 kolonu taşır; 4–8 editable, 2/3/9–13 read-only/disabled.
- **AC-T-3** Editable bir hücre değişince ~500 ms sonra read-only hücreler güncellenir ve **input focus kaybolmaz**.
- **AC-T-4** Çözülemeyen sonuçta computed hücreler "—", tablo üstünde bildirim; **sıfır gösterilmez**.
- **AC-T-5** `_Form` ve `Details` **aynı beş section başlığını aynı sırada** ilan eder (verifier section-map).

### 16.4 Korunanlar (regresyon)

- **AC-V-1** `CyclePeriodFeatureFlags` **27 bayrak, ad kümesi değişmemiş**;
  `SupportsWorkingCalendarIntegration` / `SupportsWorkingDayCount` **`false`**.
- **AC-V-2** `git diff --stat` → `Features/CyclePeriod/**` **0 dosya**; `services/Diten.Platform/**` **0 dosya**;
  `Views/CRM/CyclePeriods/**` ve `js/CRM/CyclePeriods/**` **0 dosya**.
- **AC-V-3** Aggregate'te visit/working-day/field-day adı geçen **hiçbir property yok** (reflection testi korunur).
- **AC-V-4** Preview handler'ı **`ICycleCapacityRepository` almaz**; transient kapasitenin `Id`'si `Guid.Empty`.
- **AC-V-5** Saved `/calculation` ile `/calculation-preview` **aynı girdide birebir aynı** sonucu verir.
- **AC-V-6** `CycleCapacityFeatureFlags`: `SupportsComputedValuePersistence` vb. **hepsi `false` kalır**;
  `IsEstimate: true`.

### 16.5 Kalite kapıları

- **AC-Q-1** `verify_datatable_page.py . --area CRM --module CycleCapacities --reference compact --api-profile proxy`
  → FAIL kümesi **CyclePeriods baseline'ı ile birebir aynı (8 FAIL)**. Yeni FAIL kabul edilmez.
- **AC-Q-2** `verify_module_id.py . --check-all` → **HARD violations: 0**.
- **AC-Q-3** `dotnet build` CrmService + Diten.Web → **0 hata**.
- **AC-Q-4** Test suite regresyonsuz (bilinen flaky `ContactLocationPiiHardening…` hariç).
- **AC-Q-5** 7 dil RESX **parite** (anahtar sayısı eşit).

---

## 17. Test Expectations

| Küme | Kapsam |
|---|---|
| `CycleCapacityCalculatorTests` | Per-ay FTE (AC-F-2) · altın örnek **güncellenmiş** hâliyle · `NonWorkingDays` çıkarımı · fail-closed'ın bölünmemesi · rounding hâlâ AwayFromZero ve bir kez |
| `CycleCapacityMigrationCompatTests` (**YENİ**) | Kök-FTE'li FU06 dokümanı → her aya kopya · **sayı değişmiyor** · geri yazma yok (AC-F-5) |
| `CycleCapacityRuntimeTests` (mevcut, 49 test) | Hepsi geçmeye devam etmeli; FTE assert'leri per-ay'a taşınır |
| `CycleCapacityPreviewTests` | Saved ≡ preview (AC-V-5) · no-persist · `Guid.Empty` · fail-closed · 403 ayrımı |
| Boundary | CyclePeriod contract değişmedi (AC-V-1) · protected path diff'leri (AC-V-2) |
| Frontend | verifier (AC-Q-1) · RESX parite (AC-Q-5) · build (AC-Q-3) |

**Authenticated smoke — kullanıcı çalıştırır.** Ön koşullar: fleet bu FU'nun build'iyle yeniden başlatılmış · en az
bir `active`/`draft` CyclePeriod · ülke+yıl için yayınlanmış çalışma takvimi · test kullanıcısında
`platform.working-calendar.override.read` (yoksa `calendar_forbidden` yolu doğrulanır — o da geçerli bir sonuçtur).

---

## 18. Ready-for-dev Checklist

- [x] DCP-002 geçidi **exit 0** + fail-closed kontrol koşusu (§0.1)
- [x] Golden Reference **Compact** korunuyor; `form_field_count: 9` (> 8) ile karar değişmiyor (§4.2)
- [x] Layout & Shell Contract ve **section/card haritası** açıkça yazılı (§9)
- [x] Frontend File Contract: yeni dosya yok, Compact yasakları korunuyor (§11)
- [x] Validation Rules + yeni reason code (§12)
- [x] Failure Path ≥ 4 senaryo (§13 — 7 senaryo)
- [x] Authorization: değişiklik yok, açıkça beyan (§14)
- [x] Gateway kararı açık: **gereksiz** (§15)
- [x] Acceptance criteria test edilebilir (§16 — 23 madde)
- [x] Test expectations build/verifier/RESX/smoke kapsıyor (§17)
- [ ] **⚠️ `D-HOLIDAY` kullanıcı kararı (§0.3)** — C-ertelenmiş (önerilen) mi, (b-1) ayrı WC FU'su mu, (b-2) talep-üzerine gün-gün mü?
- [ ] **⚠️ `status: ready-for-dev` + `runtime_code_allowed: true`** flip'i — ayrı kullanıcı kararı
- [ ] Extra-elements okuma yaklaşımı (§4.3) ready-for-dev'de teyit edildi

---

## 19. Implementation Notes

### 19.1 Bilinen tuzaklar (FU06'dan devralınan — tekrarlanmasın)

| Tuzak | Önlem |
|---|---|
| Verifier **son** aynı-isimli property'yi okur | ViewModel'ler zaten üç dosyaya bölünmüş; `Fte` aya inerken **read-side şekillerin** form'unkini gölgelememesine dikkat |
| `h6.text-uppercase.text-heading.fw-semibold` = section başlığı | Birleşik tabloya alt başlık eklenirse **düz div** kullanılmalı (§9) |
| `_Form` ↔ `Details` section haritası eşit olmalı | `FieldForceSection` kartı **silinmez** (§9) |
| Mongo partial index `$ne` crash | Index'ler değişmiyor; yeni index eklenmiyor |
| Embedded tipin class-map'ten atlanması | `CycleCapacityMonth` zaten kayıtlı; FTE decimal, ek serializer gerekmez |
| DateTimeOffset `.Date` / parallel-arrays | Ay sıralaması hâlâ `Year`+`MonthNumber` (int) |
| Fleet çalışırken build kilidi | Test için `--no-build` veya fleet durdurma |

### 19.2 Neden `NonWorkingDays` bir ÇIKARIM ve neden bu dürüst

`wcWorkingDays` WC'nin **kendi** sayısıdır; takvim gün sayısı ise aritmetiktir. Farkları, WC'nin "çalışılmıyor"
dediği gün sayısına **tanım gereği** eşittir. Bu bir tahmin değil, aynı verinin ikinci okunuşudur — ve tam da bu
yüzden onu "tatil" diye etiketlemek yanlış olurdu: WC hafta sonunu, tatili ve kapanışı ayırt eder, çıkarım etmez.

---

## 20. Follow-up Items

| ID | Konu | Sahip | Not |
|---|---|---|---|
| **F-WC-HOLIDAY-RANGE** | `op=holidays-between` — aralıktaki tatil **tarihleri** (§8.4'te sözleşme hazır) | platform-shared-services | **D-HOLIDAY'in kilidi**; tooltip buna bağlı |
| **F-FTE-HR** | Gerçek HR kaynağı; per-ay FTE alanının **düzenlenebilir/otomatik** hâle gelmesi | HCM / MOD-0288 | Alan bu FU ile **yerinde**; yalnız kilit açılacak |
| **F-FTE-BU** | Per-(BusinessUnit, Year) FTE granülerliği (legacy) | commercial-suite | HR master'a bağlı |
| **F-REGISTRY** | `MOD-0155-FU07` registry satırı | commercial-suite | Pack yetkisi dışı |
| **F-RBAC** / **F-RBAC-WC** | `crm.cycle-capacity.*` katalog + `platform.working-calendar.override.read` grant | platform-shared-services | FU06'dan devam |
| **F-WC-BULK** | WC toplu ay/aralık endpoint'i (çağrı sayısı) | platform-shared-services | FU06'dan devam |
| **F-APPROVAL** · **F-ACTUALS** · **F-SCENARIO** · **F-MICROTARGET-SEAM** · **F-WC-ORG-UNIT** · **F-FILE-DRIFT** · **F-NAV** | — | — | FU06'dan devam, kapsam dışı |

---

## Handoff

Module pack **`draft`** olarak hazır. Geliştirmeye geçmeden önce **iki kapı**:

1. **`D-HOLIDAY` (§0.3)** — tatil **tarihleri** bu domain'den alınamıyor. Öneri: **C-ertelenmiş** (kolonu
   *"Çalışılmayan gün"* olarak şimdi teslim et, tarihleri `F-WC-HOLIDAY-RANGE`'e bırak). Alternatifler (b-1) ayrı bir
   Working Calendar FU'su, (b-2) talep-üzerine gün-gün yükleme. **(B) katmanları CRM'de birleştirmek önerilmiyor.**
2. **`status: ready-for-dev` + `runtime_code_allowed: true`** flip'i.

Sonra `@orchestrator MOD-0155-FU07` çağrılır. Golden Reference **Compact** şablon olarak korundu — sapma yok.
