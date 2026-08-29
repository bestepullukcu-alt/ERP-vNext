---
id: MOD-0165-FU08
name: Campaign Cycle Period Binding
parent: MOD-0165
parent_name: Campaign Management
siblings: MOD-0165-FU01 (frequency SoR) · MOD-0165-FU02 (campaign boundary) · MOD-0165-FU03 (frequency runtime) · MOD-0165-FU04 (campaign runtime — bu FU onu genişletir) · MOD-0165-FU05 (campaign admin UI) · MOD-0165-FU06 (cycle period — SHIPPED) · MOD-0165-FU07 (cycle period scope enrichment — SHIPPED)
domain: commercial-suite
service: Diten.CrmService
frontend: frontend/Diten.Web
shell: tenant
golden_reference: compact
entity_base: EntityBase
status: review
runtime_code_allowed: true
flip_approved_by: "control-tower (2026-08-28) — draft verified from source: no FU08 runtime touched, GetByIdAsync exists (status-agnostic read-only), supportsCampaignBinding stays false (CyclePeriod untouched), Campaign.EndDate nullable (D-OPENEND premise real), DCP-002 exit 0. DECISIONS APPROVED: D-OPENEND=(a) open-ended campaign cannot bind → 400; D-RECHECK (bind-active on binding-change only, B2 on every write while bound); D-GUARD (separate async CampaignCycleBindingGuard); D-PROJECTION (read-time, never persisted, batch GetByIdsAsync); D-PROXY (Campaigns passthrough, CyclePeriods protected); D-PICKER (active-only); D-FILES (Campaign grouped layout preserved); D-SCOPE-MATCH (NO scope-match — out of scope for phase 1). Traps locked: DateTimeOffset .Date reduction (AC-B2-4), no composite index, silent-unbind option injection (AC-UI-3)."
runtime_code_scope: "Kapsam: Campaign aggregate'ine tek nullable alan (CyclePeriodId) + class-map, CampaignCycleBindingGuard (YENİ, async write-path guard), CampaignValidation +1 saf kural, Create/Update handler'larında guard çağrısı, 3 yeni reason code, CampaignFeatureFlags +1 bayrak + limitations satırları, ICyclePeriodReader'a SALT-OKUNUR GetByIdsAsync (batch projeksiyon), Campaign DTO/VM + liste projeksiyonu, Campaigns frontend proxy'sine salt-okunur selector passthrough, Compact form'da dönem seçici + pencere gösterimi, DataTable/Details'te bağlı dönem, 7 dil RESX, boundary testleri. YASAK: CyclePeriod aggregate/contract/engine/repository YAZIMI, CyclePeriod'a Campaign referansı, campaign scope-mirror (country/LE/BU), segment-targeting, BU-picker, auto-close, cascade, backfill/migration script, Mongo hand-edit, ocelot.json yazımı, registry yazımı, RBAC seed/grant."
owner: module-pack-author
branch: feature/crm/mod-0165-fu08-campaign-cycle-period-binding
started: 2026-08-28
target: 2026-08-28
form_field_count: 21
predecessor: MOD-0165-FU04 (Campaign runtime — SHIPPED) + MOD-0165-FU06/FU07 (CyclePeriod — SHIPPED)
closes_followup: F-CAMPAIGN-BIND (MOD-0165-FU06 §20)
dependencies:
  - MOD-0165-FU04 (ZORUNLU ÖNCÜL — Campaign aggregate + write path; bu FU ona TEK alan ekler)
  - MOD-0165-FU06 (ZORUNLU ÖNCÜL — CyclePeriod master + ICyclePeriodReader; SALT-OKUNUR tüketilir)
  - MOD-0165-FU07 (CyclePeriod scope enrichment — SHIPPED; scope EŞLEŞTİRİLMEZ, bkz. F-SCOPE-MATCH)
  - MOD-0165-FU05 (Campaign admin UI — Compact restyle sonrası; form/DataTable/Details bu FU ile genişler)
  - MOD-0164 (consent — DEĞİŞMEZ; snapshot yolu bu FU'da hiç açılmaz)
  - MOD-0167 (Segment — DEĞİŞMEZ; segment-targeting bu FU DEĞİL)
  - MOD-0155-FU05 (MicroTarget — yapılmadı; bu FU MicroTarget satırı üretmez)
  - MOD-0018 (RBAC — yalnız tüketim; yeni anahtar YOK, seed/grant YOK)
  - DEV-0001 (Golden Reference Compact — Campaign zaten Compact; şablon değişmez)
---

# MOD-0165-FU08 — Campaign Cycle Period Binding

> **TASLAK / BOUNDARY + CONTRACT PACK (2026-08-28) — `status: draft`, `runtime_code_allowed: false`.**
> Bu pack **kod yazma yetkisi vermez.** Onaya sunduğu tek şey şu sorunun sahibi, doğrulama kuralı ve
> tüketim yönüdür:
> *"Bu kampanya hangi planlama **dönemine** aittir ve penceresi o dönemin içinde mi?"*
>
> **Neden şimdi:** `MOD-0165-FU06` §20 bu soruyu **F-CAMPAIGN-BIND** olarak açık bırakmıştı
> (*"`Campaign` ↔ `CyclePeriod` bağı gerekli mi? (kampanyanın kendi penceresi zaten var)"*). Bu FU o
> follow-up'ı **kapatır** ve cevabı şudur: bağ gereklidir, ama **yalnız tek yönlü bir pin olarak** —
> kampanya döneme *işaret eder*, dönem kampanyaları *tanımaz*.
>
> **Neden yeni aggregate yok:** Dönem zaten vardır (`CyclePeriod`, FU06/FU07 SHIPPED). Kampanyanın
> penceresi de zaten vardır (`Campaign.StartDate/EndDate`, FU04 SHIPPED). Eksik olan tek şey ikisi
> arasındaki **doğrulanmış referanstır**. Araya bir `CampaignCyclePeriodAssignment` aggregate'i koymak,
> tek bir nullable alanın taşıyabileceği bir gerçeği üç dosyaya dağıtır ve ilk gerçek soruyu
> (*"bu kampanya hangi dönemde?"*) iki okumaya böler.

---

## 0. Delivery Record (2026-08-28)

> **RUNTIME AUTHORIZATION (2026-08-28).** Kullanıcı pack'i `ready-for-dev` + `runtime_code_allowed: true` olarak
> yetkilendirdi ve §1.3'teki tüm D-kararlarını onayladı — **D-OPENEND = (a)** (açık uçlu kampanya bindlenemez → 400).
> Uygulama pack'e harfiyen uyularak yapıldı; aşağıdaki üç sapma dışında hiçbir karar değişmedi.

**Teslim edilen yüzeyler.** Backend: `Domain/Entities/Campaign.cs` (+`CyclePeriodId`, +3 reason code) ·
`Features/Campaign/CampaignCycleBindingGuard.cs` (**YENİ** — D-RECHECK'in tek yeri) ·
`CampaignValidation.cs` (+`ValidateCyclePeriodReference`, saf) · `Commands/CampaignCommands.cs` ·
`Handlers/CampaignCommandHandlers.cs` (guard çağrısı, yazımdan ÖNCE) · `Handlers/CampaignQueryHandlers.cs`
(batch projeksiyon + tekil projeksiyon) · `CampaignDtos.cs` + `CampaignMapper.cs` (+`CampaignCyclePeriodDto`) ·
`Contract/CampaignContract.cs` (+`SupportsCyclePeriodBinding: true`, +3 reason code, +6 limitations satırı,
`RuntimeScope` genişledi) · `Features/CyclePeriod/Read/ICyclePeriodReader.cs` + `CyclePeriodReader.cs`
(**yalnız** salt-okunur `GetByIdsAsync`) · `Persistence/DependencyInjection.cs` (class-map string-Guid, tek-alanlı
`ix_campaigns_tenant_cycle_period`, guard DI) · `Api/Models/CRM/CampaignRequests.cs` +
`Api/Controllers/CRM/CampaignsController.cs`. Frontend: `Controllers/CRM/CampaignsController.cs`
(salt-okunur `api/cycle-periods` passthrough + `LoadCyclePeriodAsync`) · `Models/CRM/CampaignViewModels.cs` +
`Models/CRM/CampaignCyclePeriodViewModel.cs` (**YENİ dosya**, bkz. S2) · `_Form.cshtml` (seçici + pencere
gösterimi) · `Details.cshtml` · `_DataTable.cshtml` (+1 kolon) · `_IndexL10n.cshtml` · `index.js` (kolon +
index kaydırma) · `form.js` (seçici + AC-UI-3 enjeksiyonu) · 7 dil RESX **+9 anahtar** (148×7, parite doğrulandı).
Tests: `CampaignCycleBindingTests.cs` (**YENİ**, 38 test).

**Pack'ten sapmalar (üçü de daraltıcı veya düzeltici, genişletici değil):**

| # | Sapma | Gerekçe |
|---|---|---|
| **S1** | **AC-V-3 ihlali:** FU04 test dosyası (`CampaignTargetingRuntimeTests.cs`) DEĞİŞTİ. İki yerde: (a) fixture, handler'ların kazandığı bağımlılığı geçirmek zorunda — derleme gereği, kaçınılmaz; (b) `T35_Forbidden_Contract_Flags_Are_Absent` `Assert.Equal(6, flagNames.Count)` diyordu, FU08 yedinci bayrağı **meşru olarak** ekledi | Yasak-bayrak listesi **değişmedi** ve hepsi hâlâ yok. Sayı yerine **ada göre** kümeyi assert edecek şekilde güçlendirildi, böylece bir sonraki ekleme de bilinçli beyan gerektirir. **Hiçbir FU04 davranış iddiası değişmedi** |
| **S2** | `CampaignCyclePeriodViewModel` `CampaignViewModels.cs` içinde değil, **kendi dosyasında** | Verifier bir form alanının tipini dosyadaki **son** aynı-isimli property'den çözer. Projeksiyonun non-nullable `EndDate`'i formun nullable `EndDate`'ini gölgeledi ve *"Optional numeric/date fields use nullable ViewModel types"* kontrolü **var olmayan** bir kusur raporladı (MOD-0167-FU02'de belgelenen tuzağın aynısı). Ayrı dosya, belgelenmiş çözümdür |
| **S3** | `GetByIdsAsync`, `ICyclePeriodRepository`'ye yeni bir metot eklemek yerine mevcut `ListAsync` + bellek-içi daraltma ile uygulandı | Repository dosyaları §2.1'de **protected**. Seçici handler'ı zaten aynı deseni kullanıyor; bir tenant'ın dönem takvimi onlarca satırdır. Tek round-trip garantisi (N+1 yasağı) korundu |

**Doğrulama** (ham çıktılar teslim raporunda): verifier proxy profili **87 PASS / 8 FAIL**, FAIL kümesi CRM
kardeşi Segments ile **birebir aynı** (95 kontrolün tamamında ad+sonuç diff'i boş) · `verify_module_id --check-all`
**HARD violations: 0** · `--check-id MOD-0165-FU08` **exit 0** · derleme **0 hata** (CrmService + Diten.Web) ·
test **1213/1218** (5 önceden var olan skip) · CAND literal **0** · CyclePeriod contract'ının 18 boundary bayrağı
**değişmedi** (`SupportsCampaignBinding: false` dâhil).

**FU08 dışı bulgu:** `ContactLocationPiiHardeningTests.PiiMasking_Redacts_Email_And_Phone_But_Keeps_Guid_And_Country`
**önceden var olan flaky** bir testtir (~7 koşuda 1 düşer): rastgele `Guid.NewGuid()` üretip GUID'in maskelemeden
sağ çıkmasını bekler, GUID'in son segmenti telefon-şeklinde bir rakam dizisi olduğunda redaktör onu maskeler.
FU08 ile ilgisi yoktur (PII maskeleme; kampanya/dönem kodu içermez) ve bu FU'da **düzeltilmemiştir** — ayrı iş.

**Açık kalan:** F-REGISTRY · F-SCOPE-MATCH · F-CAMPAIGN-SCOPE · F-CYCLE-FILTER · F-CYCLE-CONTRACT-NOTE ·
F-VFP-FK · F-MICROTARGET · F-CAMPAIGN-CYCLE-REPORT · F-FILE-DRIFT · F-RBAC (§20). Authenticated smoke (§17.2)
**kullanıcı tarafından** çalıştırılır: fleet'in FU08 build'i ile yeniden başlatılmasını ve bir `active` dönem
bulunmasını gerektirir.

---

## 0.1 Kimlik Geçidi ve Bulgular

### 0.1.1 DCP-002 — PASS (2026-08-28)

```text
$ py .antigravity/scripts/verify_module_id.py . --check-id MOD-0165-FU08 --name "Cycle Period" --parent MOD-0165
OK  MOD-0165-FU08: proven against Blueprint/registry.
REAL_EXIT=0
```

**Geçidin fail-closed olduğu ayrıca kanıtlandı** (kontrol koşusu — sahte parent):

```text
$ py .antigravity/scripts/verify_module_id.py . --check-id MOD-9999-FU08 --name "X" --parent MOD-9999
BLOCKED  MOD-9999-FU08
   - parent MOD-9999 not found in Blueprint or registry
   - MOD-9999 not in Blueprint and --repo-only not set — cannot prove ID
Gate failed closed. See DCP-002.
REAL_EXIT=2
```

> **Geçidin kapsamı hakkında dürüst not.** Üçüncü bir kontrol koşusunda geçit,
> `--name "Totally Bogus Capability"` ile de `exit 0` verdi. Yani geçit **kimliği** (parent'ın Blueprint'te
> varlığı + FU id'sinin boşluğu + registry çakışması) doğrular, **FU açıklayıcı adını doğrulamaz.**
> DCP-002'nin 3. maddesi (kanonik ad) Blueprint'teki **parent** için geçerlidir; parent'ın kanonik adı
> **"Campaign Management"**'tır ve değişmez. Frontmatter'daki `name` bir repo-tarafı açıklayıcıdır.
> Bu yüzden "geçit adı onayladı" **denmiyor** — geçit id'yi onayladı.

**FU numarası gerekçesi (D-FU):** parent `MOD-0165` altında FU01/FU02/FU05 pack, FU03/FU04 runtime kodu,
FU06/FU07 pack + runtime olarak kullanımdadır. `grep -rn "MOD-0165-FU08" execution/` → **0 sonuç**.
İlk çakışmayan id **FU08**'dir. **Registry satırı bu pack tarafından EKLENMEZ** (registry yazımı pack
yetkisi dışıdır) — §20 / F-REGISTRY.

### 0.1.2 Kod okumasından çıkan üç bulgu (görev girdisini DARALTAN)

Görev girdisi üç yerde "gerekiyorsa eklenir" dedi. Kod okundu; üçünün de cevabı **zaten var** veya
**daha az iş**:

| # | Görev girdisinin varsayımı | Kodda bulunan gerçek | Sonuç |
|---|---|---|---|
| **B1** | *"`ICyclePeriodReader`'a read-by-id metodu **gerekiyorsa** salt-okunur eklenir"* | `Task<CyclePeriodSnapshot?> GetByIdAsync(Guid, CancellationToken)` **zaten var** (`Features/CyclePeriod/Read/ICyclePeriodReader.cs`). Tenant-scoped, **statü-agnostik** (closed dönemi de döner), salt-okunur, in-process, `HttpClient` tutmuyor | **Seam'e tekil okuma metodu EKLENMEZ.** `ResolveActiveAsync` imzası da değişmez. Tek istisna: batch projeksiyon → D-PROJECTION |
| **B2** | Dönem seçicisi için yeni bir read endpoint'i gerekebilir | `GET /api/crm/cycle-periods/selector?cycleStatus=active&…` **zaten var** (FU06/FU07), *"the lightweight picker a consumer UI binds to"*. Ocelot route çifti de FU06'da açıldı | **Yeni backend read endpoint'i YOK, yeni Gateway route'u YOK.** Yalnız Campaigns frontend proxy'sine salt-okunur bir passthrough gerekir → D-PROXY |
| **B3** | Doğrulama Campaign write path'ine eklenir | `CampaignWrite.Validate(...)` **saf statik** bir metottur (async değil, repository tutmaz). Cycle okuması **async**tır | Doğrulama saf metoda **gömülmez**; ayrı bir async guard olur → D-GUARD |

> Bu üç bulgu pack'in kapsamını **genişletmez, daraltır**: FU06/FU07 yüzeyleri hiç değişmez
> (tek istisna D-PROJECTION'ın salt-okunur batch metodu), Gateway'e dokunulmaz.

### 0.1.3 Kilitli kararların teyidi

Görev girdisindeki kullanıcı-onaylı kararlar (2026-08-28) bu pack'te **aynen** taşınmıştır ve
tartışmaya açılmamıştır: nullable `CyclePeriodId` · bind-anında-active · close'a dayanıklılık ·
draft bindlenmez · B2 ⊆ INCLUSIVE · unbind kısıtı kaldırır · yön Campaign→CyclePeriod ·
`supportsCampaignBinding` false kalır · in-service salt-okunur resolve, HTTP self-call yok ·
geçersiz/bulunamayan id → 400 fail-closed · UTC-midnight kanonik gün karşılaştırması.

**Kilitli kümede olmayan ve karar bekleyen tek şey → [D-OPENEND](#d-openend--açık-uçlu-kampanya-karar-beklİyor).**

---

## 1. Module Summary

`Campaign.CyclePeriodId`, bir kampanyayı tenant'ın **adlandırılmış planlama dönemine** bağlayan
**opsiyonel, tek yönlü bir pin**dir. Tek bir soruyu cevaplar — ***"bu kampanya hangi döneme ait?"*** — ve
başka hiçbir şeyi cevaplamaz.

Bağ kurulduğu anda tek bir kısıt doğar (**B2**): kampanyanın penceresi dönemin penceresinin **içinde**
kalmalıdır. Bağ yoksa kısıt da yoktur; kampanya bugünkü davranışını harfiyen sürdürür.

### 1.1 Ne DEĞİLDİR (kavram ayrımı — bu pack'in en kritik cümlesi)

| Kavram | Sahibi | Sorusu | Bu FU ile ilişkisi |
|---|---|---|---|
| **`Campaign.CyclePeriodId`** (bu FU) | MOD-0165-FU04 (Campaign) | *"Bu kampanya hangi **döneme** ait?"* | **BU FU** — tek nullable alan + tek kısıt |
| **`CyclePeriod`** | MOD-0165-FU06/FU07 | *"Hangi dönem, hangi adreste?"* | **SALT OKUNUR tüketilir.** Aggregate, contract, engine, repository, UI — **hiçbiri değişmez** |
| **`Campaign.StartDate/EndDate`** | MOD-0165-FU04 | *"Bu kampanya ne zaman yürürlükte?"* | **Kampanyanın kendi penceresi kalır.** Dönemden **türetilmez**, dönemle **doldurulmaz**, dönem değişince **güncellenmez** |
| **Campaign scope** (country/LE/BU) | — | *"Bu kampanya hangi adreste?"* | **KAPSAM DIŞI** — ayrı FU. Bu FU dönemin scope'u ile kampanyanın `BusinessUnitId`'sini **eşleştirmez** (§2.5, F-SCOPE-MATCH) |
| **`VisitFrequencyPolicy.CyclePeriodId`** | MOD-0165-FU03 | *"Ne sıklıkta?"* | **AYRI ALAN, AYRI AGGREGATE.** Bu FU VFP'ye dokunmaz; VFP'nin FK doğrulaması hâlâ F-VFP-FK'dır |
| **`MicroTarget`** | MOD-0155-FU05 (yapılmadı) | *"Bu dönemde, bu temsilcinin planı ne?"* | Bu FU MicroTarget satırı **üretmez**; kampanya-dönem bağı MicroTarget'ın işini **yapmaz** |
| **Working Calendar** | CAND-CAP-0008 (PSS) | *"Bu **gün** çalışma günü mü?"* | **AYRI KAVRAM.** Bu FU çalışma günü **saymaz**, tatil **bilmez** |

> **Tek cümlelik sınır:** *Kampanyanın penceresi kampanyanın gerçeğidir; dönemin penceresi dönemin
> gerçeğidir. Bu FU ikisini **birleştirmez** — yalnızca birincinin ikincinin içinde kaldığını **doğrular**.*

### 1.2 Yönün tek cümlelik gerekçesi

```text
Campaign ──CyclePeriodId──▶ CyclePeriod        (PIN — bu FU)
Campaign ◀──────────────── CyclePeriod         (YOK — hiç açılmaz)
```

`CyclePeriod`, kampanyalarını **bilmez**: üzerinde `CampaignId` alanı, kampanya listesi, kampanya sayacı
veya cascade yoktur. Bu, `supportsCampaignBinding: false` bayrağının FU06 contract'ında **doğru kalmasının**
sebebidir (§13.3). Ters yön açılsaydı dönem kapatma işlemi kampanyaları etkilemek zorunda kalırdı —
tam olarak kaçınılan şey.

### 1.3 D-Karar özeti

| # | Karar | Durum |
|---|---|---|
| **D-FIELD** | `Campaign` üzerinde nullable `Guid? CyclePeriodId`, ayrı aggregate YOK | **KİLİTLİ** (kullanıcı) |
| **D-BIND-STATUS** | Bind anında dönem **active** olmalı; `draft` ve `closed` bindlenemez | **KİLİTLİ** (kullanıcı) |
| **D-CLOSE-RESILIENT** | Bağlı dönem sonradan `closed` olursa binding **korunur**; kampanya tarihleri değişmez | **KİLİTLİ** (kullanıcı) |
| **D-B2** | Bağlıyken `[Campaign.Start, Campaign.End] ⊆ [Period.Start, Period.End]`, INCLUSIVE; ihlal → 400 `campaign_outside_cycle_window` | **KİLİTLİ** (kullanıcı) |
| **D-UNBIND** | `CyclePeriodId = null` → kısıt kalkar, her zaman serbest | **KİLİTLİ** (kullanıcı) |
| **D-DIRECTION** | Tek yön. `CyclePeriod.supportsCampaignBinding` **false kalır**, CyclePeriod dosyaları değişmez | **KİLİTLİ** (kullanıcı) |
| **D-READ** | In-service, salt-okunur, HTTP self-call yok; dönem **yazılmaz** | **KİLİTLİ** (kullanıcı) |
| **D-FAILCLOSED** | Bulunamayan / başka tenant'ın / geçersiz id → 400, binding yazılmaz | **KİLİTLİ** (kullanıcı) |
| **D-DATE** | ⊆ karşılaştırması `UtcDateTime.Date` kanonik gününde | **KİLİTLİ** (kullanıcı) |
| **D-RECHECK** | Bind-active **yalnız binding DEĞİŞTİĞİNDE**; B2 **bağlı olan her yazımda** (§12.2) | **ÖNERİ** — D-CLOSE-RESILIENT'ten türetilmiş zorunlu sonuç |
| **D-OPENEND** | `EndDate = null` (açık uçlu) kampanya **bindlenemez** → 400 | **KARAR BEKLİYOR** ⚠️ |
| **D-GUARD** | Doğrulama saf `CampaignWrite.Validate`'e gömülmez; ayrı async `CampaignCycleBindingGuard` | **ÖNERİ** |
| **D-PROJECTION** | Liste/Details'te dönem etiketi **read-time projeksiyon**; seam'e salt-okunur `GetByIdsAsync` eklenir | **ÖNERİ** |
| **D-PROXY** | Seçici, Campaigns proxy'sine eklenen salt-okunur passthrough üzerinden; CyclePeriods proxy'si değişmez | **ÖNERİ** |
| **D-FILES** | Campaign feature'ının mevcut **gruplanmış** dosya düzeni korunur; Golden kanonik bölme YAPILMAZ | **ÖNERİ** |
| **D-PICKER** | Seçici `cycleStatus=active` ile dolar; **mevcut seçim closed olsa bile round-trip'te korunur** | **ÖNERİ** |
| **D-SCOPE-MATCH** | Dönemin scope'u ile kampanyanın `BusinessUnitId`'si **eşleştirilmez** (kapsam dışı, açıkça ilan edilir) | **ÖNERİ** |
| **D-VOCAB** | Yeni vokabüler yok; 3 yeni reason code in-domain sabit | **ÖNERİ** |
| **D-RBAC** | Yeni permission anahtarı **YOK**; mevcut `crm.campaign.*` yeterli | **ÖNERİ** |

---

## 2. Ownership and Boundaries

**In-scope:** `Campaign` aggregate'ine tek nullable `CyclePeriodId` alanı + class-map · bind-active
doğrulaması · B2 pencere kısıtı · fail-closed okuma · 3 reason code · contract bayrağı + limitations ·
`ICyclePeriodReader`'a **salt-okunur** batch okuma (D-PROJECTION) · Campaign DTO/VM/liste projeksiyonu ·
Campaigns frontend proxy'sine salt-okunur selector passthrough · Compact form'da dönem seçici +
pencere gösterimi · DataTable ve Details'te bağlı dönem · 7 dil RESX · boundary testleri.

**Out-of-scope (YASAK):**

| Yasak | Neden |
|---|---|
| `CyclePeriod` aggregate / repository / handler / validator / contract / engine **yazımı** | D-DIRECTION: yön tek |
| `CyclePeriod` üzerine `CampaignId` / kampanya listesi / sayaç | Ters yön hiç açılmaz |
| `CyclePeriod.supportsCampaignBinding` bayrağının çevrilmesi | §13.3 — false **doğru** kalır |
| `CyclePeriods` frontend view / js / RESX / proxy controller değişikliği | D-PROXY: FU06/FU07 yüzeyi korunur |
| Campaign **scope-mirror** (country / legal-entity / business-unit alanları) | Ayrı FU — kullanıcı kapsam dışı bıraktı |
| Campaign **BU-picker** | Ayrı FU |
| Segment-targeting / `CampaignTarget` mutasyonu / snapshot yolu | Ayrı FU'lar; bu FU hedeflere dokunmaz |
| Kampanya tarihlerinin dönemden **türetilmesi / otomatik doldurulması** | Türetilen tarih iki gerçek yaratır (FU06 D-DATING emsali) |
| Dönem kapanınca kampanyaya **cascade** (arşivleme, statü değişimi, tarih kırpma) | D-CLOSE-RESILIENT: close tarihleri değiştirmez |
| `VisitFrequencyPolicy` yazımı veya `VFP.CyclePeriodId` FK doğrulaması | Hâlâ F-VFP-FK (ayrı iş) |
| MicroTarget satırı üretimi · çalışma günü / tatil hesabı | MOD-0155 / CAND-CAP-0008 sınırı |
| Backfill / migration script · Mongo hand-edit | Alan nullable; mevcut satırlar `null` ile geçerli (§4.2) |
| `ocelot.json` yazımı · registry yazımı · RBAC seed/grant | Pack yetkisi dışı (§15, §14, §20) |
| Hard delete · bulk-delete | Campaign FU04'ten devralınan kural |

### 2.1 MOD-0165-FU06/FU07 koruması (kırmızı çizgi)

**Protected — bu FU tarafından okunur, asla yazılmaz:**

```text
services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/CyclePeriod.cs
services/Diten.CrmService/src/Diten.CrmService.Domain/Repositories/ICyclePeriodRepository.cs
services/Diten.CrmService/src/Diten.CrmService.Application/Features/CyclePeriod/**
    └── TEK İSTİSNA: Read/ICyclePeriodReader.cs + Read/CyclePeriodReader.cs — yalnız D-PROJECTION'ın
        salt-okunur GetByIdsAsync metodu eklenir; mevcut üç metodun İMZASI DEĞİŞMEZ
services/Diten.CrmService/src/Diten.CrmService.Persistence/Repositories/CyclePeriodRepository.cs
services/Diten.CrmService/src/Diten.CrmService.Api/Controllers/CRM/CyclePeriodsController.cs
frontend/Diten.Web/Controllers/CRM/CyclePeriodsController.cs
frontend/Diten.Web/Views/CRM/CyclePeriods/**
frontend/Diten.Web/wwwroot/assets/js/CRM/CyclePeriods/**
frontend/Diten.Web/Resources/Views/CRM/CyclePeriods/**
```

- Bu FU `ICyclePeriodRepository`'yi **hiç** kullanmaz — yalnız `ICyclePeriodReader` seam'ini kullanır.
- Seam'e eklenecek tek metot **salt-okunurdur**: `InsertAsync` / `ReplaceAsync` çağırmaz, `HttpClient`
  tutmaz, `ICyclePeriodLegalEntityValidator` almaz (FU07'nin *"proving an MDM reference is a WRITE-path
  concern"* kuralı korunur).
- Kampanya bir dönemi **id ile** referanslar; `CycleCode` / `CycleName` / tarihleri **kopyalamaz**
  (FU06'nın *"a consumer stores the ID and re-reads"* kuralı; kopya bayatlar).

### 2.2 MOD-0165-FU04 sözleşme koruması

- `CampaignTarget`, `CampaignTargetConsentEvaluation`, snapshot yolu ve MOD-0164 entegrasyonu
  **hiç değişmez**. Dönem bağı hedefleri, consent'i veya snapshot'ı **etkilemez**.
- `Campaign` üzerindeki mevcut 20 alanın hiçbirinin tipi, adı veya doğrulaması değişmez.
- Kampanya arşivleme davranışı değişmez; arşivli kampanya bugün olduğu gibi mutasyon kabul etmez.

### 2.3 Yön asimetrisinin ilanı

FU06 contract'ı `supportsCampaignBinding: false` yayımlar. Bu FU'dan **sonra da yayımlamaya devam eder**
ve bu bir tutarsızlık **değildir**: bayrak *"CyclePeriod kampanya bağı **destekler mi**"* sorusunun
cevabıdır ve CyclePeriod hâlâ desteklemez — kampanyayı bilmez, tutmaz, doğrulamaz.

Yanlış okunma riski gerçektir ("demek ki hiçbir yerde kampanya-dönem bağı yok"). Bu risk, **FU08'in
kendi sahip olduğu yüzeyde** giderilir: Campaign contract'ının `limitations` listesine yönü açıkça
söyleyen bir satır eklenir (§13.2). FU06'nın limitations metnine **dokunulmaz** — o dosya protected'tır.
İsteğe bağlı bir netleştirme follow-up olarak kaydedilir (F-CYCLE-CONTRACT-NOTE).

### 2.4 Legacy CrmV2

Legacy'de kampanya-dönem bağı yoktur; legacy `PlannedPromoWeek` bir **kampanya alanına gömülmüş dönem**
anti-pattern'idir ve FU06 §2.4'te zaten reddedilmiştir. Bu FU o hatayı tekrarlamaz: dönem hâlâ ayrı bir
aggregate'tir, kampanya yalnızca **ona işaret eder**. Legacy tablo/controller/view **taşınmaz**.

### 2.5 Bilinçli olarak doğrulanMAYAN şey (açık ilan)

Bu FU, dönemin **scope'u** ile kampanyanın **`BusinessUnitId`**'sini karşılaştırmaz. Yani:

> `BusinessUnitId = "alpha"` olan bir kampanya, `ScopeType = business-unit / BusinessUnitId = "beta"`
> olan aktif bir döneme **bağlanabilir** ve bu bugün **kabul edilir**.

Bu bir gözden kaçma değil, **kapsam kararıdır**: kullanıcı Campaign scope-mirror'ı ayrı bir FU'ya
bıraktı ve kampanyanın bugünkü `BusinessUnitId`'si opak bir MOD-0048 kodudur — dönemin FU07'de
doğrulanmış scope'u ile aynı anlam düzeyinde değildir. Sessizce doğruluyormuş gibi yapmak yerine
**açıkça reddediliyor**: contract limitations'a yazılır (§13.2), F-SCOPE-MATCH olarak kaydedilir (§20).

---

## 3. Owned Objects

| Nesne | Tür | Sahiplik | Not |
|---|---|---|---|
| `Campaign.CyclePeriodId` | Alan (`Guid?`) | **YENİ — bu FU** | Tek yeni kalıcı alan |
| `CampaignCycleBindingGuard` | Application servisi | **YENİ — bu FU** | Async write-path guard (D-GUARD) |
| `CampaignValidation.ValidateCyclePeriodReference` | Saf kural | **YENİ — bu FU** | Format düzeyi (boş GUID reddi) |
| `CampaignReasonCodes.CampaignOutsideCycleWindow` / `.CampaignCyclePeriodNotActive` / `.CampaignCyclePeriodNotFound` | Sabit | **YENİ — bu FU** | §12.3 |
| `CampaignFeatureFlags.SupportsCyclePeriodBinding` | Contract bayrağı | **YENİ — bu FU** | `true` |
| `CampaignCyclePeriodDto` (projeksiyon) | Read DTO | **YENİ — bu FU** | Kalıcı DEĞİL (§8.3) |
| `ICyclePeriodReader.GetByIdsAsync` | Seam metodu | **YENİ — salt-okunur** | Tek FU06/FU07 dosya dokunuşu |
| `CyclePeriod` (aggregate) | Aggregate | **MOD-0165-FU06/FU07** | Salt okunur tüketim |
| `Campaign` (aggregate) | Aggregate | **MOD-0165-FU04** | Bu FU +1 alan ekler |

---

## 4. Entity Fields

### 4.1 `Campaign` — eklenen tek alan

| Alan | Tip | Zorunlu | Kısıt | Açıklama |
|---|---|---|---|---|
| `CyclePeriodId` | `Guid?` | Hayır | Boş GUID reddedilir; verildiğinde çağıranın tenant'ında var olan bir `CyclePeriod`'a işaret etmeli | Kampanyanın ait olduğu planlama dönemi. `null` = bu kampanya cycle-bound değil (varsayılan). Yalnız **id** tutulur — kod/ad/tarih **kopyalanmaz** |

**Değişmeyen alanlar:** `CampaignCode` · `CampaignName` · `CampaignType` · `CampaignStatus` ·
`ObjectiveType` · `BusinessUnitId` · `BrandId` · `ProductId` · `SubjectId` · `TopicId` ·
`ConceptChainTemplateId` · `EngagementJourneyId` · `DefaultKnowledgePathId` ·
`DefaultKnowledgeContentId` · `DefaultConsentChannel` · `DefaultConsentPurpose` · `StartDate` ·
`EndDate` · `Description` · `OwnerUserId` · `ExternalReferences` · audit alanları. **Hiçbirinin tipi,
adı, varsayılanı veya doğrulaması değişmez.**

### 4.2 Migration YOK — gerekçe

Alan **nullable**tır ve varsayılanı `null`dır. Mongo'da var olmayan bir alan okunduğunda `Guid?` zaten
`null` gelir. Dolayısıyla:

- backfill script **yok**, migration **yok**, Mongo hand-edit **yok**;
- mevcut her kampanya `CyclePeriodId = null` olarak geçerlidir ve **B2 kısıtına tabi değildir**;
- eski bir kampanya, biri onu bir döneme **açıkça bağlayana kadar** bugünkü davranışını harfiyen sürdürür.

Bu, FU07'nin `EnsureScopeType()` desenindeki *"read-time normalisation, not a migration"* disiplininin
aynısıdır — ama burada normalizasyona bile gerek yoktur, çünkü `null` zaten anlamlı bir cevaptır.

### 4.3 Index kararı — ve bir tuzak

`CyclePeriodId` üzerine **tekil, tek-alanlı** bir index (tenant + cyclePeriodId) yeterlidir ve
*"bu döneme bağlı kampanyalar"* sorgusu için gerekecektir.

> ⚠️ **Tuzak (bu serviste daha önce yaşandı):** `CyclePeriodId` **iki `DateTimeOffset` alanıyla birlikte
> bileşik bir index'e KONMAMALIDIR**. CRM'de `DateTimeOffset` BSON'da `[ticks, offset]` **dizisi** olarak
> saklanır; iki DTO alanını aynı index'e veya aynı sort'a koymak
> *"cannot sort with keys that are parallel arrays"* 500'üne yol açar. `CyclePeriodId` bir `Guid`dir,
> tek başına güvenlidir — bileşik hâle getirilmemelidir.

---

## 5. Repo Scope

### 5.1 Backend — `services/Diten.CrmService/`

```text
src/Diten.CrmService.Domain/Entities/Campaign.cs                       [DEĞİŞİR] +1 alan (Guid? CyclePeriodId)
src/Diten.CrmService.Application/Features/Campaign/
├── CampaignValidation.cs                                              [DEĞİŞİR] +1 saf kural
├── CampaignDtos.cs                                                    [DEĞİŞİR] +CyclePeriodId, +CampaignCyclePeriodDto
├── CampaignMapper.cs                                                  [DEĞİŞİR] alan taşıma + projeksiyon
├── Commands/CampaignCommands.cs                                       [DEĞİŞİR] Create/Update +CyclePeriodId
├── Handlers/CampaignCommandHandlers.cs                                [DEĞİŞİR] guard çağrısı (create + update)
├── Handlers/CampaignQueryHandlers.cs                                  [DEĞİŞİR] batch projeksiyon (list + byId)
├── Contract/CampaignContract.cs                                       [DEĞİŞİR] +1 bayrak, +limitations, RuntimeScope
└── CampaignCycleBindingGuard.cs                                       [YENİ]   async write-path guard
src/Diten.CrmService.Application/Features/CyclePeriod/Read/
├── ICyclePeriodReader.cs                                              [DEĞİŞİR] +GetByIdsAsync (SALT-OKUNUR)
└── CyclePeriodReader.cs                                               [DEĞİŞİR] implementasyon
src/Diten.CrmService.Persistence/…/CrmClassMaps.cs (veya eşdeğeri)     [DEĞİŞİR] Campaign.CyclePeriodId map
src/Diten.CrmService.Persistence/…/CampaignRepository.cs               [DEĞİŞİR] tek-alanlı index (§4.3)
tests/Diten.CrmService.Application.Tests/CampaignCycleBindingTests.cs  [YENİ]   §17
```

### 5.2 Frontend — `frontend/Diten.Web/`

```text
Controllers/CRM/CampaignsController.cs                                 [DEĞİŞİR] +1 salt-okunur passthrough
Models/CRM/CampaignViewModels.cs                                       [DEĞİŞİR] +CyclePeriodId + seçici verisi
Views/CRM/Campaigns/_Form.cshtml                                       [DEĞİŞİR] dönem seçici + pencere gösterimi
Views/CRM/Campaigns/Details.cshtml                                     [DEĞİŞİR] bağlı dönem alanı
Views/CRM/Campaigns/_DataTable.cshtml                                  [DEĞİŞİR] +1 kolon
Views/CRM/Campaigns/_IndexL10n.cshtml                                  [DEĞİŞİR] +anahtarlar
wwwroot/assets/js/CRM/Campaigns/index.js                               [DEĞİŞİR] kolon tanımı + index kaydırma
wwwroot/assets/js/CRM/Campaigns/form.js                                [DEĞİŞİR] seçici doldurma + select2 + pencere
Resources/Views/CRM/Campaigns/CampaignIndex.{ar,en,es,fr,ru,tr,zh}.resx [DEĞİŞİR] 7 dil (§18)
```

> **`_Form.cshtml` ↔ `Details.cshtml` bölüm parite uyarısı.** Campaign Compact yüzeyi verifier'ın
> *"Compact `_Form.cshtml` matches `Details.cshtml` section/card map"* kontrolünü geçmektedir
> (bölüm sırası: Summary → References → ExternalReferences → ConsentContext). Dönem seçicisi
> **mevcut bir bölümün içine** konur (öneri: **Summary**, tarih alanlarının yanına — kısıt tarihlerle
> ilgilidir). **Yeni bir `<section>` açılırsa iki dosyada da aynı sırada açılmalıdır**, aksi hâlde
> parite kontrolü kırılır.

---

## 6. Protected Paths

§2.1'deki CyclePeriod yüzeylerine ek olarak:

```text
services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/Campaign.cs :: CampaignTarget,
    CampaignTargetConsentEvaluation ve tüm Campaign* vokabüler sınıfları    [OKUNUR, YAZILMAZ]
Features/Campaign/Snapshot/**                                               [DOKUNULMAZ]
Features/Campaign/Handlers/CampaignTargetCommandHandlers.cs                 [DOKUNULMAZ]
Features/ConsentPreference/**                                               [DOKUNULMAZ]
Features/VisitFrequencyPolicy/**                                            [DOKUNULMAZ]
Features/Segment/**                                                         [DOKUNULMAZ]
gateway/**/ocelot.json                                                      [DOKUNULMAZ — yeni route gerekmiyor]
execution/registries/module-id-registry.md                                  [DOKUNULMAZ — F-REGISTRY]
```

---

## 7. Dependencies

| Bağımlılık | Rol | Durum | Not |
|---|---|---|---|
| **MOD-0165-FU04** Campaign | genişletilen | SHIPPED | +1 alan, +1 guard; hedef/snapshot/consent **değişmez** |
| **MOD-0165-FU06** CyclePeriod | **tüketilen** | SHIPPED | `ICyclePeriodReader.GetByIdAsync` zaten var (§0.2/B1) |
| **MOD-0165-FU07** Scope enrichment | tüketilen | SHIPPED | Snapshot `ScopeType`/`ScopeRef` taşır; bu FU **eşleştirmez** (§2.5) |
| **MOD-0165-FU05** Campaign admin UI | genişletilen | SHIPPED (Compact) | Form/DataTable/Details bu FU ile büyür |
| `GET /api/crm/cycle-periods/selector` | **tüketilen** | SHIPPED | Yeni endpoint gerekmez (§0.2/B2) |
| **Gateway** (Ocelot) | — | route **mevcut** | FU06 `/api/crm/cycle-periods` + `{everything}` çiftini açtı |
| **MOD-0164** Consent | komşu | SHIPPED | **Değişmez** |
| **MOD-0167** Segment | komşu | SHIPPED | **Değişmez** |
| **MOD-0155-FU05** MicroTarget | gelecek tüketici | yapılmadı | Bu FU satır üretmez |
| **MOD-0018** RBAC | tüketim | mevcut | Yeni anahtar **yok** (§14) |
| **DEV-0001** Golden Compact | şablon | mevcut | Campaign zaten Compact; 21 alan → Compact kalır |

---

## 8. Runtime Constraints

### 8.1 Salt-okunurluk (kırmızı çizgi)

- Bu FU'nun hiçbir kod yolu bir `CyclePeriod` satırını **oluşturmaz, güncellemez, silmez**.
- Okuma **in-process**tir: `ICyclePeriodReader` doğrudan enjekte edilir. Kendi servisine Gateway
  üzerinden HTTP ile dönmek **yasaktır** (MOD-0165-FU03'ün *"no HTTP self-call"* kuralı).
- Okuma **tenant-scoped**tur: seam tenant'ı request context'ten çözer, çağıran tenant seçemez →
  çapraz-tenant bağ **yapısal olarak** imkânsızdır (başka tenant'ın id'si `null` döner → 400).

### 8.2 Erişilemezlik davranışı — FU07'den bilinçli fark

FU07'nin MDM legal-entity doğrulaması **cross-service**tir ve erişilemezlikte `503` + fail-closed
davranır. **Bu FU'nun okuması cross-service DEĞİLDİR** — aynı servis, aynı Mongo. Dolayısıyla:

| Durum | Cevap | Gerekçe |
|---|---|---|
| Dönem yok / başka tenant'ta / silinmiş | **400** `campaign_cycle_period_not_found` | Fail-closed, kullanıcı hatası |
| Dönem `draft` veya `closed`, **binding değişiyor** | **400** `campaign_cycle_period_not_active` | D-BIND-STATUS |
| Kampanya penceresi dönemin dışında | **400** `campaign_outside_cycle_window` | D-B2 |
| Mongo erişilemez | **500** (mevcut davranış) | 503/retry/timeout katmanı **eklenmez** — cross-service değil, sahte bir dayanıklılık katmanı yanıltıcı olur |

> `CyclePeriod`'da **hard delete yoktur** (FU06: *"Nothing here is ever hard-deleted"*). Bu yüzden
> "bulunamayan dönem" ancak (a) hiç var olmamış bir id, (b) başka tenant'ın id'si veya (c) veri
> bozulması olabilir. Üçünde de 400 doğrudur ve mevcut bir kampanyayı kilitleme riski yoktur.

### 8.3 Projeksiyon asla kalıcı değildir

Liste/Details'te gösterilen `cycleCode` / `cycleName` / dönem penceresi / dönem statüsü **okuma
anında** üretilir ve **hiçbir zaman `Campaign` dokümanına yazılmaz**. FU06'nın kuralı aynen geçerlidir:
*kopyalanan etiket, dönem yeniden adlandırıldığı anda bayatlar.*

### 8.4 Tarih normalizasyonu (D-DATE) — ve bir tuzak

`CyclePeriod.StartDate/EndDate` **UTC gece yarısına normalize** saklanır ve `EndDate` **INCLUSIVE**tir.
`Campaign.StartDate/EndDate` ise ham bir **an**dır (normalize edilmez ve bu FU onu normalize **etmez** —
kampanyanın alan semantiği değişmez).

Bu yüzden ⊆ karşılaştırması **her iki tarafta da aynı kanonik güne indirgenerek** yapılır:

```text
campaignStartDay = campaign.StartDate.UtcDateTime.Date
campaignEndDay   = campaign.EndDate.Value.UtcDateTime.Date
periodStartDay   = period.StartDate.UtcDateTime.Date
periodEndDay     = period.EndDate.UtcDateTime.Date

B2  ⇔  periodStartDay <= campaignStartDay  &&  campaignEndDay <= periodEndDay
```

> ⚠️ **Tuzak (bu serviste daha önce yaşandı):** CRM'de `DateTimeOffset` an-düzeyinde karşılaştırıldığında
> **yanlış reddetme** üretir — dönemin son günü `00:00Z`, kampanyanın bitişi aynı günün `18:00Z`'ı
> olduğunda ham karşılaştırma "dışarıda" der, oysa aynı gündür. `.Date`'e indirgeme bu yüzden
> **zorunludur**, stil tercihi değildir. Test AC-B2-4 tam olarak bu vakayı kilitler.

### 8.5 Kampanya statüsünden bağımsızlık

B2, kampanyanın `CampaignStatus`'undan **bağımsızdır**: `draft` bir kampanya da bağlıysa penceresi
dönemin içinde olmalıdır. Gerekçe: kısıt **veri bütünlüğü** kuralıdır, yayın kuralı değil. Aksi hâlde
bir kampanya `draft`ken pencere dışına kaydırılıp sonra `active` edilerek kısıt atlatılabilirdi.

Arşivli kampanya zaten mutasyon kabul etmez (FU04) → B2 arşivli kampanyada hiç çalışmaz.

---

## 9. Layout & Shell Contract

| Öğe | Değer |
|---|---|
| `shell` | `tenant` |
| Razor layout | **`Layout = "_LayoutTenantShell";`** — `Index.cshtml`, `Create.cshtml`, `Edit.cshtml`, `Details.cshtml` |
| View klasörü | `frontend/Diten.Web/Views/CRM/Campaigns/` |
| Golden reference | **Compact** (`DEV-0001`) — mevcut, değişmez |
| Nav | **Yeni nav girdisi YOK** — Campaigns zaten menüde |

Bu FU **hiçbir yeni sayfa açmaz**: mevcut Campaigns Compact yüzeyini genişletir. `_LayoutTenantShell`
dışında bir layout kullanılamaz ve bu AC-UI-0'da test edilir.

---

## 10. Backend File Convention

### 10.1 D-FILES — mevcut gruplanmış düzen KORUNUR

Golden Reference kanonik düzeni `Commands/` · `Queries/` · `Handlers/CommandHandlers/` ·
`Handlers/QueryHandlers/` · `Validators/` · `{Module}Models.cs` şeklindedir. **Ancak
`Features/Campaign/` bu düzende değildir** — FU04 gruplanmış dosyalar yazmıştır
(`CampaignCommands.cs`, `CampaignCommandHandlers.cs`, `CampaignValidation.cs`, `CampaignDtos.cs`).

**Karar: bu FU mevcut gruplanmış düzeni izler, kanonik bölmeyi YAPMAZ.**

| Seçenek | Değerlendirme |
|---|---|
| (a) Mevcut gruplanmış düzeni izle | ✅ **SEÇİLEN.** Tek alan + tek guard için 8 dosyalık bir yeniden düzenleme, bu FU'nun kapsamını 10 katına çıkarır ve FU04'ün çalışan write path'ini gereksiz riske sokar |
| (b) Feature'ı Golden kanonik düzene taşı | ❌ Ayrı iş; zaten **F-FILE-DRIFT** (FU06 §20) olarak kayıtlı |
| (c) Yeni dosyaları kanonik, eskileri gruplanmış bırak | ❌ **En kötüsü** — aynı klasörde iki düzen, okuyucuya iki kural öğretir |

Yeni tek dosya `Features/Campaign/CampaignCycleBindingGuard.cs`, feature kökünde ve mevcut
`CampaignValidation.cs` ile aynı seviyede durur — gruplanmış düzenle tutarlı.

### 10.2 D-GUARD — neden ayrı bir guard

`CampaignWrite.Validate(...)` **saf statik** bir metottur: async değildir, repository/seam tutmaz,
17 parametreyi alıp `(string? Error, int StatusCode)` döner. Cycle doğrulaması **async bir okuma**
gerektirir.

| Seçenek | Değerlendirme |
|---|---|
| (a) Ayrı `CampaignCycleBindingGuard` (async, seam enjekte) | ✅ **SEÇİLEN.** Saf doğrulama saf kalır; I/O yapan kural I/O yapan sınıfta durur. Test edilebilirliği en yüksek olan da budur (seam mock'lanır) |
| (b) `CampaignWrite.Validate`'i async yap + seam parametresi ekle | ❌ Saf bir metodu I/O'ya bulaştırır; 17 parametreli imza 18 olur; her çağıran değişir |
| (c) Handler'ların içine gömülü inline kontrol | ❌ Create ve Update'te **iki kopya** kural → iki kural. FU06'nın *"an order written twice is two orders"* disiplinine aykırı |

**Guard'ın imzası (öneri):**

```text
CampaignCycleBindingGuard.EvaluateAsync(
    Guid? requestedCyclePeriodId,   // yeni istenen bağ (null = unbind / bağsız)
    Guid? currentCyclePeriodId,     // create'te null; update'te mevcut satırın değeri
    DateTimeOffset campaignStart,
    DateTimeOffset? campaignEnd,
    CancellationToken ct)
  → (string? Error, int StatusCode, string? ReasonCode)
```

Guard **yalnızca okur ve karar verir**; yazma işini handler yapar. Guard hata dönerse handler
`Response<T>.Fail(...)` ile çıkar ve **hiçbir şey persist etmez** (D-FAILCLOSED).

---

## 11. Frontend File Contract

### 11.1 Golden karar — Compact KALIR

| Sayım | Değer |
|---|---|
| FU04/FU05 sonrası mevcut kullanıcı alanı | **20** (CampaignCode, CampaignName, CampaignType, CampaignStatus, ObjectiveType, StartDate, EndDate, OwnerUserId, Description, BusinessUnitId, BrandId, ProductId, SubjectId, TopicId, ConceptChainTemplateId, EngagementJourneyId, DefaultKnowledgePathId, DefaultKnowledgeContentId, DefaultConsentChannel, DefaultConsentPurpose) |
| Bu FU'nun eklediği | **+1** (CyclePeriodId) |
| **Toplam** | **21** → `> 8` → **Compact** |

Golden referans **değişmez**. `_CreateEditOffcanvas.cshtml` ve `_DetailsQuickView.cshtml`
Compact'ta **yasaktır** ve bu FU onları açmaz.

### 11.2 Compact dosya seti (mevcut — bu FU yalnız içeriklerini genişletir)

```text
Views/CRM/Campaigns/
├── Index.cshtml            [var]
├── Create.cshtml           [var]
├── Edit.cshtml             [var]
├── Details.cshtml          [DEĞİŞİR] bağlı dönem alanı
├── _Form.cshtml            [DEĞİŞİR] dönem seçici + pencere gösterimi
├── _Filter.cshtml          [var — bu FU filtre EKLEMEZ, bkz. F-CYCLE-FILTER]
├── _DataTable.cshtml       [DEĞİŞİR] +1 kolon
├── _IndexL10n.cshtml       [DEĞİŞİR] +anahtarlar
└── CampaignIndex.cs        [var]

wwwroot/assets/js/CRM/Campaigns/
├── index.js                [DEĞİŞİR] kolon tanımı + hedef index kaydırma
├── index.l10n.js           [var]
├── form.js                 [DEĞİŞİR] seçici doldurma + select2 + pencere gösterimi
└── details.js              [var — targeting/snapshot DOKUNULMAZ]
```

### 11.3 D-PICKER — seçicinin üç kuralı

1. **Kaynak:** `GET /CRM/Campaigns/api/cycle-periods?cycleStatus=active` (proxy → FU06 selector).
   Hardcoded liste **yasak**.
2. **Mevcut seçim round-trip'te KORUNUR.** Kampanya `closed` bir döneme bağlıysa (D-CLOSE-RESILIENT)
   o dönem `active` listesinde **yoktur**. Form onu listeye **ayrıca enjekte etmeli** ve `closed`
   rozetiyle göstermelidir.
   > ⚠️ Aksi hâlde form açılıp kaydedildiğinde seçici boş gelir, `CyclePeriodId` sessizce `null`
   > post edilir ve **kullanıcı hiç istemeden kampanyayı unbind eder.** Bu tam olarak MOD-0162-FU03'te
   > yaşanan *"arşivlenmiş seçeneğin round-trip'te hayatta kalması"* tuzağıdır — orada dirty-check
   > ile çözülmüştü, burada seçenek enjeksiyonu ile çözülür.
3. **Select2 init sırası:** seçenekler fetch ile **doldurulduktan SONRA** select2 initialize edilir ve
   `change.select2` native `change` olarak yeniden yayımlanır. Aksi hâlde select2 boş bir listeyi
   snapshot'lar (CAND-CAP-0008 form.js emsali).

### 11.4 Pencere gösterimi (B2'yi görünür kılan tek şey)

Seçicinin hemen altında, seçili dönemin penceresi **salt-okunur** gösterilir:

```text
2026 / Dönem 3 · 01.03.2026 – 30.04.2026 · active
```

Gerekçe: B2 ihlali sunucudan `400` olarak döner. Pencereyi göstermeyen bir form, kullanıcıya
*"kampanya dönemin dışında"* der ama **dönemin ne olduğunu söylemez** — düzeltilemez bir hata mesajı
hatadan beterdir. Bu bir **gösterim**dir: tarihler kampanyaya **kopyalanmaz** ve tarih alanlarını
**otomatik doldurmaz** (§2 yasağı).

---

## 12. Validation Rules

### 12.1 Alan düzeyi

| Alan | Kural | Hata | HTTP |
|---|---|---|---|
| `CyclePeriodId` | Verilmemişse (null) → geçerli, hiçbir kontrol çalışmaz | — | — |
| `CyclePeriodId` | Verilmişse `Guid.Empty` olamaz | `CyclePeriodId must be a non-empty identifier when supplied (omit the field instead).` | 400 |
| `CyclePeriodId` | Verilmişse çağıranın tenant'ında var olmalı | `campaign_cycle_period_not_found` | 400 |
| `CyclePeriodId` | **Binding değişiyorsa** dönem `active` olmalı | `campaign_cycle_period_not_active` | 400 |
| `CyclePeriodId` + tarihler | Bağlıysa B2 sağlanmalı | `campaign_outside_cycle_window` | 400 |
| `EndDate` | Bağlıysa `null` olamaz (**D-OPENEND — karar bekliyor**) | `campaign_outside_cycle_window` (açık uçlu varyant) | 400 |

Boş-GUID kuralı, FU04'ün mevcut `ValidateOptionalReference` desenine **birebir** uyar:
*"an explicitly supplied but empty GUID is a caller error, not a 'no reference' signal"*.

### 12.2 D-RECHECK — hangi kontrol ne zaman çalışır (bu pack'in en kritik tablosu)

D-BIND-STATUS ("bind anında active") ile D-CLOSE-RESILIENT ("sonradan close olursa korunur") ancak
**bind-active kontrolü yalnız binding DEĞİŞTİĞİNDE** çalışırsa aynı anda doğru olabilir. Aksi hâlde
dönem kapandığı gün, ona bağlı her kampanya **düzenlenemez** hâle gelirdi — bu, kapanmanın kampanyayı
etkilememesi kilitli kararının ihlali olurdu.

| Senaryo | `CyclePeriodId` | bind-active? | B2? | Sonuç |
|---|---|---|---|---|
| Create, bağsız | `null` | ✗ | ✗ | Serbest |
| Create, aktif döneme bağlı, pencere içinde | `X` | ✓ geçer | ✓ geçer | **201** |
| Create, aktif döneme bağlı, pencere dışında | `X` | ✓ geçer | ✗ **ihlal** | **400** `campaign_outside_cycle_window` |
| Create, `draft` döneme bağlanmaya çalışıyor | `X` | ✗ **ihlal** | — | **400** `campaign_cycle_period_not_active` |
| Create, `closed` döneme bağlanmaya çalışıyor | `X` | ✗ **ihlal** | — | **400** `campaign_cycle_period_not_active` |
| Create, olmayan/başka tenant id'si | `X` | — | — | **400** `campaign_cycle_period_not_found` |
| Update, `null` → `null` | değişmedi | ✗ | ✗ | Serbest |
| Update, `null` → `X` (**yeni bağ**) | **değişti** | ✓ çalışır | ✓ çalışır | Dönem active değilse 400 |
| Update, `X` → `Y` (**bağ değişti**) | **değişti** | ✓ `Y` için çalışır | ✓ `Y` için çalışır | `Y` active değilse 400 |
| Update, `X` → `null` (**unbind**) | değişti (null'a) | ✗ | ✗ | **Her zaman serbest** (D-UNBIND) |
| Update, `X` → `X`, dönem hâlâ `active`, sadece açıklama değişti | değişmedi | ✗ | ✓ çalışır (geçer) | Serbest |
| Update, `X` → `X`, dönem **artık `closed`**, sadece açıklama değişti | değişmedi | ✗ **çalışmaz** | ✓ çalışır (tarihler değişmedi → geçer) | **Serbest** ← D-CLOSE-RESILIENT'in kalbi |
| Update, `X` → `X`, dönem **artık `closed`**, tarihler **pencere dışına** çekiliyor | değişmedi | ✗ çalışmaz | ✗ **ihlal** | **400** `campaign_outside_cycle_window` |
| Update, `X` → `X`, dönem satırı bulunamıyor | değişmedi | — | — | **400** `campaign_cycle_period_not_found` (fail-closed) |

**İki cümlelik kural:**
> **bind-active**, `requestedCyclePeriodId != currentCyclePeriodId` **ve** yeni değer `null` **değilse**
> çalışır.
> **B2**, sonuçtaki `CyclePeriodId` **`null` değilse** çalışır — binding değişsin veya değişmesin.

### 12.3 Yeni reason code'lar

`CampaignReasonCodes` içine, mevcut snake_case konvansiyonuna uygun **üç** sabit:

| Sabit | Değer | Anlamı |
|---|---|---|
| `CampaignOutsideCycleWindow` | `campaign_outside_cycle_window` | Kampanya penceresi bağlı dönemin penceresinin dışında |
| `CampaignCyclePeriodNotActive` | `campaign_cycle_period_not_active` | Bağlanmak istenen dönem `draft` veya `closed` |
| `CampaignCyclePeriodNotFound` | `campaign_cycle_period_not_found` | Dönem çağıranın tenant'ında yok |

Üçü de `GetCampaignContractHandler.AllReasonCodes` listesine eklenir (contract'ın *"nothing in this
feature is silent"* disiplini).

### 12.4 D-OPENEND — açık uçlu kampanya (KARAR BEKLİYOR)

> ⚠️ **Bu, kullanıcının kilitli karar kümesinde bulunmayan tek boşluktur ve onay gerektirir.**

`Campaign.EndDate` **nullable**dır ve FU04'te açıkça *"Open-ended when null"* olarak belgelenmiştir.
`CyclePeriod.EndDate` ise **zorunlu ve INCLUSIVE**tir. Dolayısıyla açık uçlu bir kampanya için
`[Start, ∞) ⊆ [PeriodStart, PeriodEnd]` **hiçbir zaman sağlanamaz**.

| Seçenek | Değerlendirme |
|---|---|
| **(a) Açık uçlu kampanya BİNDLENEMEZ → 400** | ✅ **ÖNERİLEN.** ⊆ ilişkisinin dürüst sonucu. Kullanıcı ya bir bitiş tarihi verir ya da bağı kaldırır — ikisi de bilinçli bir karardır. Hata mesajı sebebi söyler: *"bağlı bir kampanyanın bitiş tarihi zorunludur"* |
| (b) `EndDate` null iken dönemin `EndDate`'i **ima edilir** | ❌ Türetilmiş tarih = ikinci gerçek. FU06 D-DATING'in reddettiği şeyin aynısı. Ayrıca kampanya penceresi dönem düzenlenince sessizce değişirdi |
| (c) `EndDate` null iken **yalnız `StartDate`** kontrol edilir | ❌ B2'nin ⊆ vaadini sessizce bozar: contract "içinde kalır" der, runtime "başlangıcı içindeydi" der. Sessiz varsayım yasağı |
| (d) `EndDate`'i bağlıyken **zorunlu alan** yap (ViewModel `[Required]`) | ⚠️ (a)'nın UI tarafı. Sunucu kuralı yine (a) olmalı — UI kuralı sunucu kuralının yerine geçemez |

**Öneri: (a) + (d)** — sunucu 400 verir, form da bağ seçildiğinde `EndDate`'i zorunlu işaretler.
Karşı-karar verilirse (c) seçilirse contract'ın B2 ifadesi *"⊆"* yerine *"başlangıç dönem içinde"*
olarak **düzeltilmelidir**; ifadeyi olduğu gibi bırakmak yanıltıcı olur.

### 12.5 Failure Path to Verify

| Yol | Beklenen |
|---|---|
| Duplicate | **N/A** — `CyclePeriodId` benzersiz değildir; bir döneme **çok** kampanya bağlanabilir (ve bağlanmalıdır) |
| Missing | Olmayan id → 400 `campaign_cycle_period_not_found`; hiçbir şey persist edilmez |
| Cross-tenant | Başka tenant'ın id'si → seam `null` → 400 `campaign_cycle_period_not_found` (var olduğu **sızdırılmaz**) |
| Unauthorized | Mevcut `crm.campaign.*` guard'ları; yeni anahtar yok (§14) |
| Concurrency | Campaign'in mevcut yazma yolu değişmez; bu FU yeni bir concurrency yüzeyi açmaz |
| Half-applied | **İmkânsız:** guard yazımdan **önce** çalışır; hata → `Fail` → repository'ye hiç gidilmez |
| Bozuk veri (dangling id) | 400 fail-closed. `CyclePeriod`'da hard delete olmadığı için normal işleyişte oluşamaz (§8.2) |

---

## 13. Contract Surface

### 13.1 Bayrak

```jsonc
// CampaignFeatureFlags — mevcut 6 bayrağa +1
{
  "supportsCampaignManagement": true,
  "supportsCampaignTargetManagement": true,
  "supportsStaticTargetSnapshot": true,
  "supportsConsentEvaluationIntegration": true,
  "supportsTargetExclusionReason": true,
  "supportsTargetSourceProvenance": true,

  "supportsCyclePeriodBinding": true          // ← FU08
}
```

FU04 contract'ının kuralı — *"a capability is never emitted as false; advertising even a false flag
would misrepresent the boundary"* — burada **korunur**: bayrak `true` olarak eklenir çünkü yetenek
gerçekten açılmaktadır. Kapalı hiçbir yetenek `false` olarak yayımlanmaz.

`RuntimeScope` metnine `FU08-campaign-cycle-period-binding` eklenir.

### 13.2 `limitations` — eklenen satırlar (dördü de bir gerçeği ilan eder)

1. *"a campaign may PIN a cycle period (`CyclePeriodId`) but the binding is ONE-DIRECTIONAL: `CyclePeriod`
   holds no campaign reference, no campaign list and no cascade — its own `supportsCampaignBinding` flag
   stays false and remains correct as a statement about `CyclePeriod`'s surface"*
2. *"while bound, the campaign window must be CONTAINED in the period window (both ends inclusive,
   compared on the canonical UTC day); the campaign's own StartDate/EndDate are never derived from,
   filled from, or updated by the period"*
3. *"a period must be ACTIVE at the moment the binding is set or changed; a period that CLOSES afterwards
   keeps its bindings and changes no campaign date — closing a period never cascades"*
4. *"the binding does NOT validate scope: a campaign's `BusinessUnitId` is not matched against the
   period's `ScopeType`/`ScopeRef`, so a campaign may be bound to a period at a different address —
   campaign scope is a separate follow-up and is not silently implied here"*

### 13.3 CyclePeriod contract'ına DOKUNULMAZ

FU06 contract'ının `supportsCampaignBinding: false` satırı **olduğu gibi kalır** (§2.3 gerekçesi).
Netleştirici bir yorum satırı istenirse **F-CYCLE-CONTRACT-NOTE** olarak ayrı ele alınır — bu FU
protected bir dosyaya girmez.

---

## 14. Authorization Convention

| Konu | Karar |
|---|---|
| Yeni permission anahtarı | **YOK.** Dönem bağlamak bir **kampanya düzenleme** işidir, ayrı bir yetki değil |
| Yazma yolu | Mevcut `crm.campaign.*` guard'ları (FU04) — değişmez |
| Dönem okuma (seçici) | FU06'nın mevcut `Perms.ReadFallback` guard'ı — değişmez |
| SoD | **YOK.** Bind ayrı bir onay adımı değildir; kampanyayı düzenleyebilen bağlayabilir |
| RBAC seed / grant | **YASAK** (pack yetkisi dışı) |

> **Bilinçli asimetri:** Kampanyayı düzenleyebilen bir kullanıcı, dönemleri **yönetemese** bile
> (`crm.cycle-period.manage` yoksa) bir döneme bağlanabilir. Bu doğrudur: bağlamak dönemi
> **değiştirmez**, yalnızca ona **işaret eder**. Dönemi okuyabilmesi yeterlidir. Okuma yetkisi de
> yoksa seçici boş gelir ve kullanıcı bağ kuramaz — fail-closed.

---

## 15. Gateway / API Routing Decision

| Soru | Cevap |
|---|---|
| Yeni backend endpoint gerekli mi? | **HAYIR.** `GET /api/crm/cycle-periods/selector` zaten var (§0.2/B2) |
| Yeni Ocelot route gerekli mi? | **HAYIR.** FU06 `/api/crm/cycle-periods` + `/api/crm/cycle-periods/{everything}` çiftini açtı (F-GATEWAY kapandı) |
| `integration-agent` görevi gerekli mi? | **HAYIR** |
| Frontend'de ne gerekiyor? | **Evet — tek şey:** Campaigns proxy'sine salt-okunur passthrough |

**D-PROXY.** Campaigns Compact form'u, dönem listesini **kendi** same-origin proxy'sinden alır:

```text
GET /CRM/Campaigns/api/cycle-periods?cycleStatus=active
    → Gateway: GET /api/crm/cycle-periods/selector?cycleStatus=active
```

| Seçenek | Değerlendirme |
|---|---|
| (a) Campaigns proxy'sine salt-okunur passthrough | ✅ **SEÇİLEN.** FU06 yüzeyi hiç değişmez; Campaign sayfası kardeş bir modülün proxy'sine bağımlı olmaz |
| (b) CyclePeriods proxy'sine `selector` passthrough ekle | ❌ Protected dosya (§2.1); ayrıca Campaign sayfasını kardeş modülün URL'ine bağlar |
| (c) Tarayıcıdan doğrudan Gateway | ❌ **Yasak** — browser JS servis portuna/Gateway'e doğrudan gitmez, same-origin proxy kullanır |

Passthrough **yalnız GET**tir, yalnız `cycleStatus` / `year` sorgu parametrelerini iletir ve
`ForwardAsync` deseninin **bodyless-status guard**'ını kullanır (204 → 500 tuzağı; FU06 proxy'sinde
zaten uygulanmıştır).

---

## 16. Acceptance Criteria

### Backend

| # | Kriter |
|---|---|
| **AC-B-1** | `Campaign` üzerinde `Guid? CyclePeriodId` alanı vardır, class-map'e kayıtlıdır ve mevcut kampanyalar `null` ile okunur — **backfill/migration script yoktur** |
| **AC-B-2** | `CyclePeriodId = null` olan kampanya create/update edilebilir; hiçbir cycle kontrolü çalışmaz, hiçbir ek okuma yapılmaz |
| **AC-B-3** | `active` bir döneme, penceresi dönem içinde olan kampanya create edilir → **201** |
| **AC-B-4** | `draft` bir döneme bağlanma → **400** `campaign_cycle_period_not_active`; kayıt **persist edilmez** |
| **AC-B-5** | `closed` bir döneme **yeni** bağlanma → **400** `campaign_cycle_period_not_active` |
| **AC-B-6** | Olmayan / başka tenant'ın dönem id'si → **400** `campaign_cycle_period_not_found`; başka tenant'ta var olduğu **sızdırılmaz** |
| **AC-B-7** | `Guid.Empty` verilmesi → **400** (format kuralı), `null` ile karıştırılmaz |
| **AC-B2-1** | Kampanya penceresi dönem penceresinin dışında → **400** `campaign_outside_cycle_window` |
| **AC-B2-2** | Kampanya `StartDate` = dönem `StartDate` **ve** kampanya `EndDate` = dönem `EndDate` → **geçer** (INCLUSIVE, her iki uç) |
| **AC-B2-3** | Kampanya `StartDate` dönemin bir gün öncesi → **400**; kampanya `EndDate` dönemin bir gün sonrası → **400** |
| **AC-B2-4** | Kampanya `EndDate` = dönem son gününün `18:00Z`'ı (dönem `00:00Z` saklıyor) → **GEÇER** (kanonik gün karşılaştırması; §8.4 tuzağı) |
| **AC-B2-5** | Bağsız kampanya pencere dışı tarihlerle create edilebilir → kısıt yalnız bağlıyken vardır |
| **AC-CR-1** | Dönem `active`→`closed` olduktan sonra, bağlı kampanya **açıklama** güncellemesi → **200**; bind-active **çalışmaz**; kampanya tarihleri **değişmez** |
| **AC-CR-2** | Aynı durumda tarihler pencere **dışına** çekilirse → **400** `campaign_outside_cycle_window` |
| **AC-CR-3** | Dönem kapatıldığında hiçbir kampanya arşivlenmez / statüsü değişmez / tarihi kırpılmaz — **cascade yoktur** |
| **AC-U-1** | `X` → `null` (unbind) her koşulda **200**; sonrasında pencere dışı tarihler serbesttir |
| **AC-U-2** | `X` → `Y` (bağ değişimi) `Y` için bind-active **ve** B2 çalışır |
| **AC-D-1** | Bu FU'nun hiçbir yolu `CyclePeriod` yazmaz — `ICyclePeriodRepository` Campaign feature'ında **hiç** referanslanmaz (grep ile test edilir) |
| **AC-D-2** | `ICyclePeriodReader`'ın mevcut üç metodunun **imzası değişmez**; eklenen tek metot salt-okunurdur |
| **AC-D-3** | Campaign write path'inde `HttpClient` **yoktur** (in-process seam; HTTP self-call yasağı) |
| **AC-C-1** | Contract `supportsCyclePeriodBinding: true` yayımlar; kapalı hiçbir yetenek `false` olarak yayımlanmaz |
| **AC-C-2** | Contract `limitations` §13.2'deki **dört** satırı içerir; `reasonCodes` üç yeni kodu içerir |
| **AC-C-3** | `CyclePeriod` contract'ının `supportsCampaignBinding` bayrağı **hâlâ `false`**tur ve dosyası **değişmemiştir** |
| **AC-S-1** | Dönem scope'u ile kampanya `BusinessUnitId`'si **eşleştirilmez**; farklı adresteki bir döneme bağlanma **geçer** ve bu contract'ta ilan edilmiştir |

### Frontend

| # | Kriter |
|---|---|
| **AC-UI-0** | Dört sayfa da `Layout = "_LayoutTenantShell"` kullanır; `_CreateEditOffcanvas.cshtml` / `_DetailsQuickView.cshtml` **açılmamıştır** |
| **AC-UI-1** | `_Form.cshtml`'de dönem seçici vardır, seçenekleri **yalnız** proxy'den gelir; hardcoded liste **yoktur** |
| **AC-UI-2** | Seçici varsayılan olarak `cycleStatus=active` doldurur |
| **AC-UI-3** | `closed` bir döneme bağlı kampanya Edit'te açılır → seçili dönem **görünür** (`closed` rozetiyle), form değiştirilmeden kaydedilirse `CyclePeriodId` **korunur** — sessiz unbind **olmaz** (§11.3/2) |
| **AC-UI-4** | Seçili dönemin penceresi + statüsü salt-okunur gösterilir; kampanya tarih alanları **otomatik doldurulmaz** |
| **AC-UI-5** | Select2, seçenekler fetch ile doldurulduktan **sonra** initialize edilir; `change` yeniden yayımlanır |
| **AC-UI-6** | `_Form.cshtml` ↔ `Details.cshtml` bölüm/kart haritası **paritesi korunur** (verifier kontrolü yeşil kalır) |
| **AC-UI-7** | DataTable'da bağlı dönem kolonu vardır; bağsız satır `—` gösterir; kolon export/colvis index'leri güncellenmiştir |
| **AC-UI-8** | Details'te bağlı dönem kodu + adı + penceresi + statüsü görünür; bağsızsa `—` |
| **AC-UI-9** | 400 `campaign_outside_cycle_window` kullanıcıya **anlaşılır** biçimde gösterilir (dönem penceresi ekranda görünür durumdadır) |
| **AC-L10N-1** | Yeni anahtarlar **7 dilde** (`ar, en, es, fr, ru, tr, zh`) mevcuttur; XML dengeli, anahtar **paritesi tam** |
| **AC-L10N-2** | Yeni anahtar değerleri gerçekten çevrilmiştir (tr dosyasında İngilizce değer yoktur) |

### Doğrulama

| # | Kriter |
|---|---|
| **AC-V-1** | `verify_datatable_page.py . --area CRM --module Campaigns --reference compact` sonucu **CRM baseline'ından (85/9) gerilemez**; FAIL kümesi genişlemez |
| **AC-V-2** | `dotnet build` **0 hata**; Campaign/CyclePeriod dosyalarında yeni uyarı yok |
| **AC-V-3** | Test süiti yeşil; FU04 ve FU06/FU07 testlerinin **hiçbiri değişmemiştir** |
| **AC-V-4** | `grep -rn "CAND-" ` dokunulan runtime dosyalarında **0** (DCP-002 literal yasağı) |
| **AC-V-5** | `py .antigravity/scripts/verify_module_id.py . --check-id MOD-0165-FU08 --parent MOD-0165` **exit 0** |

---

## 17. Test Expectations

Yeni dosya: `tests/Diten.CrmService.Application.Tests/CampaignCycleBindingTests.cs`
(mevcut `CampaignTargetingRuntimeTests.cs` ve `CyclePeriod/*Tests.cs` **değiştirilmez**).

### 17.1 Kapsam matrisi

| Grup | Test | Beklenen |
|---|---|---|
| **Bağsız** | create/update `CyclePeriodId=null` | Geçer; seam **hiç çağrılmaz** (mock: 0 çağrı) |
| **Bind-active** | `draft` döneme bind | 400 `campaign_cycle_period_not_active` |
| | `closed` döneme **yeni** bind | 400 `campaign_cycle_period_not_active` |
| | `active` döneme bind | Geçer |
| **Fail-closed** | olmayan id | 400 `campaign_cycle_period_not_found`, repository'ye **hiç yazılmaz** |
| | başka tenant'ın id'si (seam `null` döner) | 400, varlık sızdırılmaz |
| | `Guid.Empty` | 400 format hatası |
| **B2 sınırları** | kampanya == dönem (iki uç eşit) | **Geçer** (INCLUSIVE) |
| | kampanya start = dönem start − 1 gün | 400 |
| | kampanya end = dönem end + 1 gün | 400 |
| | kampanya tamamen dönem içinde | Geçer |
| | kampanya dönemi tamamen kapsıyor | 400 |
| | **kısmi örtüşme** (start içeride, end dışarıda) | 400 — örtüşme ⊆ değildir |
| **Kanonik gün** | kampanya end = dönem son günü `18:00Z`, dönem `00:00Z` | **Geçer** (§8.4) |
| | kampanya start = dönem ilk günü `06:00Z` | **Geçer** |
| | farklı offset (`+03:00`) ile aynı UTC gün | **Geçer** — offset'e bakılmaz, UTC güne bakılır |
| **Close-resilience** | bind → dönem close → açıklama update | **200**, bind-active çağrılmaz, tarih değişmez |
| | bind → dönem close → tarih pencere içinde update | 200 |
| | bind → dönem close → tarih pencere dışına update | 400 `campaign_outside_cycle_window` |
| **Unbind** | `X` → `null`, sonra pencere dışı tarih | Her ikisi de geçer |
| **Bağ değişimi** | `X` (active) → `Y` (draft) | 400 — yeni bağ için bind-active çalışır |
| | `X` (active) → `Y` (active, pencere uyumsuz) | 400 `campaign_outside_cycle_window` |
| **D-OPENEND** | `EndDate=null` + bind | Onaylanan karara göre 400 (öneri (a)) |
| **Yön** | Campaign feature'ında `ICyclePeriodRepository` referansı | **0** (mimari test / grep) |
| | Campaign write path'inde `HttpClient` | **0** |
| | Seam mock'unda write metodu | **Yok** (arayüzde write metodu bulunmadığı yapısal olarak doğrulanır) |
| **Contract** | `supportsCyclePeriodBinding` | `true` |
| | kapalı yetenek `false` olarak yayımlanıyor mu | **Hayır** |
| | 3 yeni reason code `reasonCodes` içinde | Evet |
| | 4 yeni limitations satırı | Evet |
| **Scope (ilan)** | farklı scope'lu döneme bind | **Geçer** (§2.5 bilinçli) + limitations satırı mevcut |

### 17.2 Frontend / manuel

| # | Adım |
|---|---|
| S1 | Fleet yeniden başlatılır (RESX + yeni JS) |
| S2 | Bir `active` CyclePeriod hazırlanır (yoksa `/CRM/CyclePeriods`'ten oluşturulup activate edilir) |
| S3 | Campaign Create → seçicide yalnız `active` dönemler görünür |
| S4 | Pencere dışı tarihlerle kaydet → anlaşılır 400; dönem penceresi ekranda görünür |
| S5 | Pencere içi tarihlerle kaydet → 201; Details'te dönem görünür; DataTable kolonunda kod görünür |
| S6 | `/CRM/CyclePeriods`'ten dönem **close** edilir |
| S7 | Kampanya Edit → seçici bağlı dönemi `closed` rozetiyle **gösterir**; açıklama değiştirilip kaydedilir → 200 ve **bağ korunur** (sessiz unbind yok) |
| S8 | Aynı formda tarih pencere dışına çekilir → 400 |
| S9 | Seçici temizlenip kaydedilir → unbind; sonra pencere dışı tarih → 200 |
| S10 | Targeting/snapshot sekmesi **etkilenmemiştir** (regresyon kontrolü) |

> Authenticated smoke script'i (`scripts/smoke-mod0165-fu08-campaign-cycle-binding-authenticated.ps1`)
> uygulama sırasında yazılır ve **kullanıcı tarafından** çalıştırılır (login parolası gerektirir).

---

## 18. Localization

Yeni anahtarlar **7 dilde** (`CampaignIndex.{ar,en,es,fr,ru,tr,zh}.resx`), XML dengeli, parite tam:

| Anahtar | Kullanım |
|---|---|
| `CyclePeriod` | Form etiketi · DataTable kolon başlığı · Details alanı |
| `CyclePeriodHelp` | *"Kampanya bir planlama dönemine bağlandığında, kampanya penceresi dönemin penceresinin içinde kalmalıdır."* |
| `NoCyclePeriod` | Seçicinin boş seçeneği · bağsız satır gösterimi |
| `CyclePeriodWindow` | Pencere gösterimi etiketi |
| `CyclePeriodClosedBadge` | Seçicideki `closed` rozeti (§11.3/2) |
| `CampaignOutsideCycleWindow` | 400 hatasının kullanıcı metni |
| `CyclePeriodNotActive` | 400 hatasının kullanıcı metni |
| `CyclePeriodNotFound` | 400 hatasının kullanıcı metni |
| `EndDateRequiredWhenBound` | D-OPENEND (a) seçilirse form uyarısı |

Anahtarlar `_IndexL10n.cshtml` köprüsüne eklenir (`form.js` ve `index.js` aynı köprüden okur).
**Parite testi zorunludur:** yedi dosyada da anahtar sayısı ve kümesi aynı olmalıdır.

---

## 19. Ready-for-dev Checklist

| # | Madde | Durum |
|---|---|---|
| 1 | DCP-002 kimlik geçidi exit 0 + fail-closed kanıtı | ✅ §0.1 |
| 2 | FU numarası çakışması yok (`grep` → 0) | ✅ §0.1 |
| 3 | Golden reference kararı (Compact, 21 alan) | ✅ §11.1 |
| 4 | Layout açıkça yazıldı (`_LayoutTenantShell`) | ✅ §9 |
| 5 | Backend dosya konvansiyonu kararı (D-FILES) | ✅ §10.1 |
| 6 | Frontend dosya seti (Compact) | ✅ §11.2 |
| 7 | Validation Rules tablosu | ✅ §12 |
| 8 | Failure Path | ✅ §12.5 |
| 9 | Authorization | ✅ §14 |
| 10 | Gateway kararı (yeni route gerekmiyor) | ✅ §15 |
| 11 | Acceptance Criteria | ✅ §16 |
| 12 | Test Expectations | ✅ §17 |
| 13 | Protected paths | ✅ §2.1, §6 |
| 14 | Migration/backfill gerekmediği kanıtlandı | ✅ §4.2 |
| 15 | **D-OPENEND kararı** | ✅ **(a) SEÇİLDİ** — açık uçlu kampanya bindlenemez → 400 (§12.4) |
| 16 | D-RECHECK / D-GUARD / D-PROJECTION / D-PROXY / D-PICKER / D-FILES / D-SCOPE-MATCH onayı | ✅ **ONAYLANDI** (§1.3) |
| 17 | `status: ready-for-dev` + `runtime_code_allowed: true` | ✅ **YETKİLENDİRİLDİ** (2026-08-28) |

> **15–17 kapandı; pack `ready-for-dev`.**

---

## 20. Follow-up Items

| # | İş | Domain | Neden |
|---|---|---|---|
| **F-REGISTRY** | `module-id-registry.md`'ye MOD-0165-FU08 satırı | portfolio-delivery | Registry yazımı pack yetkisi dışı; DCP-002 izlenebilirliği |
| **F-SCOPE-MATCH** | Kampanya scope'u (country / legal-entity / BU) + dönem scope'u ile **eşleştirme** | commercial-suite | §2.5 — bu FU eşleştirmez, açıkça ilan eder. Campaign scope-mirror FU'sundan sonra |
| **F-CAMPAIGN-SCOPE** | Campaign'e FU07 tarzı ayrımlı scope alanları (kullanıcının "ayrı FU" dediği iş) | commercial-suite | F-COUNTRY-SOT sonrası |
| **F-CYCLE-FILTER** | Campaign listesinde "döneme göre filtrele" chip'i | commercial-suite | Bu FU kolon ekler, filtre eklemez |
| **F-CYCLE-CONTRACT-NOTE** | FU06 contract'ının `supportsCampaignBinding: false` satırına yön netleştirmesi | commercial-suite | §13.3 — protected dosya, ayrı ele alınır |
| **F-VFP-FK** | `VisitFrequencyPolicy.CyclePeriodId`'nin FK doğrulaması | commercial-suite | Hâlâ açık; bu FU VFP'ye dokunmaz |
| **F-MICROTARGET** | MOD-0155-FU05 MicroTarget — seam'in asıl tüketicisi | commercial-suite | FU06'nın varlık nedeni |
| **F-CAMPAIGN-CYCLE-REPORT** | *"Bu döneme bağlı kampanyalar"* okuma yüzeyi | commercial-suite | Index (§4.3) hazır olur; rapor ayrı iş |
| **F-FILE-DRIFT** | `Features/Campaign/` gruplanmış düzeninin Golden kanonik düzene hizalanması | commercial-suite | D-FILES sapma kaydı (FU06'dan devralındı) |
| **F-RBAC** | `crm.campaign.*` katalog kaydı + rol ataması | platform-shared-services | FU04'ten devralınan; bu FU yeni anahtar eklemez |

---

## Ek A — Bu pack'in reddettiği beş kolay yol

| # | Kolay yol | Neden reddedildi |
|---|---|---|
| A1 | `Campaign`'e `CycleCode` / `CycleName` / dönem tarihlerini **kopyala** (join'siz okuma) | Kopya bayatlar. FU06'nın *"a consumer stores the ID and re-reads"* kuralı; dönem yeniden adlandırıldığında kampanya yalan söyler |
| A2 | Kampanya tarihlerini dönemden **otomatik doldur** | İki gerçek. Kullanıcı tarihi değiştirince hangisi kazanır? FU06 D-DATING'in reddettiği desen |
| A3 | Dönem kapanınca bağlı kampanyaları **arşivle / kırp** | Cascade, geçmiş bir planlamanın ne anlama geldiğini yeniden yazar. D-CLOSE-RESILIENT'in tam karşıtı |
| A4 | `CyclePeriod`'a `CampaignIds` listesi ekle (çift yön) | Dönem kampanyaları bilirse, kampanya yazımı dönem yazımına dönüşür ve iki aggregate birbirine kilitlenir |
| A5 | B2'yi yalnız UI'da uygula (sunucuda kontrol etme) | UI kuralı sunucu kuralı değildir; API doğrudan çağrılabilir. FU07'nin `platform_surface_is_country_only` emsali: *kontrolü kaldırmak kuralı bir UI geleneğine çevirmez* |

## Ek B — Kabul edilen üç bilinçli boşluk (sessiz değil, ilan edilmiş)

| # | Boşluk | Nerede ilan edildi |
|---|---|---|
| B1 | Dönem scope'u ile kampanya `BusinessUnitId`'si eşleştirilmez | §2.5 · contract limitations #4 · AC-S-1 · F-SCOPE-MATCH |
| B2 | `VisitFrequencyPolicy.CyclePeriodId` hâlâ doğrulanmıyor | §2 yasak listesi · F-VFP-FK |
| B3 | *"Bu döneme bağlı kampanyalar"* sorgusu bir yüzey olarak açılmıyor | F-CAMPAIGN-CYCLE-REPORT (index §4.3'te hazırlanır) |

---

**Otorite sırası:** Blueprint Excel > Module Pack > [Domain Config](../domain-config.md) > `AGENTS.md` >
`.antigravity/rules/`.
