---
id: MOD-0165-FU06
name: Cycle Period
parent: MOD-0165
parent_name: Campaign Management
siblings: MOD-0165-FU01 (frequency SoR), MOD-0165-FU02 (campaign boundary), MOD-0165-FU03 (frequency runtime), MOD-0165-FU04 (campaign runtime), MOD-0165-FU05 (campaign admin UI)
domain: commercial-suite
service: Diten.CrmService
frontend: frontend/Diten.Web
shell: tenant
golden_reference: slim
entity_base: EntityBase
status: review
runtime_code_allowed: true
runtime_code_scope: "Aggregate (CyclePeriod), repository, CQRS handlers, endpoints (CRUD + lifecycle activate/close + read-only /resolve-active), ICyclePeriodReader read seam, tenant-shell UI (Golden Slim), RESX (en+tr platform-parity değil — tenant CRM => 7 dil), Gateway route (/api/crm/cycle-periods), RBAC consumption. YASAK: MicroTarget/Campaign/VFP/StrategyTemplate yazımı, auto-close job, versiyon klonu, WorkingCalendar iş-günü hesabı, bulk-delete. Boundary flag'lerinin tamamı false KALIR."
flip_approved_by: control-tower (2026-08-28) — boundary flags all false, ICyclePeriodReader read-only/no-write, D-OVERLAP active-only same-scope 409, §2.4 legacy correction (no CyclePeriod aggregate in legacy) verified in file
owner: module-pack-author
branch: feature/crm/mod-0165-fu06-cycle-period
started: 2026-08-28
target: 2026-08-28
form_field_count: 8
dependencies:
  - MOD-0165 (parent — Blueprint SoR; registry: "Owns Campaign/CyclePeriod execution")
  - MOD-0165-FU01 (frequency policy boundary — `CycleId` / `CyclePeriodId` alanlarını TANIMLADI, master'ı açmadı)
  - MOD-0165-FU03 (frequency policy runtime — SHIPPED; `VisitFrequencyPolicy.CyclePeriodId` bu FU ile ÇÖZÜMLENEBİLİR hâle gelir; imza DEĞİŞMEZ)
  - MOD-0165-FU02 / FU04 / FU05 (Campaign / CampaignTarget — DOKUNULMAZ)
  - MOD-0167-FU04 (StrategyTemplate — `supportsStrategyApply: false` beyanının karşı tarafı; template DEĞİŞMEZ)
  - MOD-0155-FU05 (MicroTarget — birincil TÜKETİCİ; bu pack MicroTarget YAZMAZ)
  - MOD-0048 (reference data — business-unit kodu opak tüketim; vokabüler in-domain, publish ön koşul DEĞİL)
  - CAND-CAP-0008 (Working Calendar — AYRI kavram; bu FU çalışma-günü hesabı YAPMAZ)
  - MOD-0018 (RBAC — yalnız tüketim; seed/grant bu pack'te YOK)
  - DEV-0000 (Golden Reference Slim — şablon)
---

# MOD-0165-FU06 — Cycle Period

> **TASLAK / BOUNDARY + CONTRACT PACK (2026-08-28) — `status: draft`, `runtime_code_allowed: false`.**
> Bu pack **kod yazma yetkisi vermez**. Yetkilendirmeye sunduğu tek şey şu sorunun sahibi, veri sözleşmesi,
> yaşam döngüsü ve salt-okunur tüketim seam'idir:
> *"Bu planlama **dönemi** hangisidir; şu an hangi dönem geçerlidir?"*
>
> **Neden şimdi:** MOD-0155-FU05 (MicroTarget) **"cycle başına"** çalışır; `MOD-0167-FU04` (StrategyTemplate)
> `supportsStrategyApply: false` bayrağıyla *"bir play'i bir döneme uygulamak"* işini açıkça MOD-0155'e erteledi;
> `MOD-0165-FU03` (SHIPPED) `VisitFrequencyPolicy` üzerinde `FrequencyType=cycle-based`, `PeriodType=cycle`,
> `CycleId` ve `CyclePeriodId` alanlarını **çözümlenemez referanslar** olarak taşıyor
> (`services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/VisitFrequencyPolicy.cs:50-53`).
> Yani **üç ayrı yerde "dönem" varsayılıyor ama hiçbir yerde bir dönem master'ı yok.** Bu FU o master'ı açar.
> Açılmazsa dönem, en kolay ama en yanlış yere gömülür: `Campaign.StartDate/EndDate` alanlarına
> (kampanya = dönem sanılır) veya MicroTarget satırına düz bir `int CycleNo` alanına — legacy'nin
> `PlannedPromoWeek` hatasının birebir tekrarı (§2.4).
>
> **DCP-002 kimlik kapısı — PASS (2026-08-28):**
> `py .antigravity/scripts/verify_module_id.py . --check-id MOD-0165-FU06 --name "Cycle Period" --parent MOD-0165`
> → `OK  MOD-0165-FU06: proven against Blueprint/registry.` (exit 0).
> **FU numarası gerekçesi (D-FU):** parent `MOD-0165` altında **FU01/FU02/FU05 pack olarak**, **FU03/FU04 runtime
> kodu olarak** kullanımdadır (`Features/VisitFrequencyPolicy/`, `Features/Campaign/`). İlk çakışmayan id **FU06**'dır.
> **Registry satırı bu pack tarafından EKLENMEZ** (registry yazımı pack yetkisi dışıdır) — §20/F-REGISTRY.
>
> Otorite sırası: **Blueprint Excel** > Module Pack > [Domain Config](../domain-config.md) > `AGENTS.md` >
> `.antigravity/rules/`.

---

## 0. Delivery Record (2026-08-28)

> **RUNTIME AUTHORIZATION (2026-08-28).** Kullanıcı bu pack'i `ready-for-dev` + `runtime_code_allowed: true`
> olarak yetkilendirdi ve §1.2 D-kararlarının tamamı kilitli kapsam olarak teyit edildi. Uygulama bu pack'e
> **harfiyen** uyularak yapıldı; aşağıdaki sapmalar dışında hiçbir karar değişmedi.

**Teslim edilen yüzeyler:** `Domain/Entities/CyclePeriod.cs` · `Domain/Repositories/ICyclePeriodRepository.cs` ·
`Application/Features/CyclePeriod/**` (Golden kanonik düzen — §10) · `Persistence/Repositories/CyclePeriodRepository.cs`
+ DI/class-map/index · `Api/Controllers/CRM/CyclePeriodsController.cs` + `CyclePeriodContractController.cs` ·
`Views/CRM/CyclePeriods/**` (Slim 7 dosya) + `wwwroot/assets/js/CRM/CyclePeriods/**` + 7 dil RESX ·
`Controllers/CRM/CyclePeriodsController.cs` (same-origin proxy, bodyless-status guard'lı) ·
`_LayoutTenantShell.cshtml` tek permission-guard'lı `<li>` · `ocelot.json` iki route (F-GATEWAY kapandı) ·
`scripts/smoke-mod0165-fu06-cycle-period-authenticated.ps1`.

**Pack'ten sapmalar (ikisi de daraltıcı, genişletici değil):**

| # | Sapma | Gerekçe |
|---|---|---|
| S1 | §17.1'de "beklenen N/A" olarak yazılan bulk-delete verifier FAIL seti **7 kontrol** çıktı (pack "6" demiyordu, sayı vermemişti). Bunlara **`reloadWithToast` kontrolü de dâhildir**: paylaşılan `DitenDataTable.reloadWithToast` helper'ı `dt.ajax.reload()` çağırır, bu sayfa ise kardeş CRM sayfaları gibi **client-side** (`data: allRows`) bir tablodur — helper'ı çağırmak exception atardı. Aynı adı taşıyan yerel bir fonksiyon yazıp kontrolü yeşile boyamak **reddedildi**; yerel yardımcı `reloadAndToast` adını taşır ve kontrol dürüstçe FAIL kalır | Yeşil bir kontrol, olmayan bir davranışı iddia edemez |
| S2 | `CAND-CAP-0008` kimliği **hiçbir runtime dosyasında geçmez** — contract'ın `limitations` metni dâhil. Çalışma takvimi yeteneğine işlevsel olarak atıf yapılır ("platform working-calendar capability") | DCP-002: candidate kimlik runtime literal'ına yazılmaz; kullanıcı talebi "CAND literal grep = 0" |

**Kapanan follow-up:** F-GATEWAY (ocelot route çifti eklendi). **Açık kalan:** F-REGISTRY · F-MICROTARGET ·
F-CYCLE-CALENDAR · F-VFP-FK · F-CAMPAIGN-BIND · F-CALENDAR-DAYS · F-RESCHEDULE · F-RBAC · F-VOCAB-0048 ·
F-FILE-DRIFT · F-NAV (§20).

**Doğrulama:** derleme/test/verifier çıktıları ham hâliyle teslim raporunda verilmiştir; authenticated smoke
(§17.3) **kullanıcı tarafından** çalıştırılır (login parolası gerektirir) ve fleet'in FU06 build'i ile yeniden
başlatılmasını bekler.

---

## 1. Module Summary

`CyclePeriod`, tenant'ın **adlandırılmış planlama dönemi**dir: *"2026 / 3. Dönem, 01.03.2026 – 30.04.2026"*.
Tek bir soruyu cevaplar — **"hangi dönem?"** — ve başka hiçbir şeyi cevaplamaz.

Hedef kullanıcı, saha planlama takvimini kuran tenant CRM yöneticisidir. Yüzey tek bir Golden **Slim** sayfadır
(`/CRM/CyclePeriods`): dönem listesi, oluşturma/düzenleme (offcanvas), quick view ve iki yaşam döngüsü aksiyonu
(`activate` / `close`).

### 1.1 Ne DEĞİLDİR (kavram ayrımı — bu pack'in en kritik cümlesi)

| Kavram | Sahibi | Sorusu | Bu FU ile ilişkisi |
|---|---|---|---|
| **`CyclePeriod`** (bu FU) | MOD-0165 | *"Hangi **iş dönemi**?"* — adlandırılmış, sıralı, tarihli planlama penceresi | **BU FU** |
| **Working Calendar** | CAND-CAP-0008 (PSS) | *"Bu **gün** çalışma günü mü, tatil mi?"* | **AYRI KAVRAM.** Bu FU çalışma günü saymaz, tatil bilmez, `IWorkingCalendarProvider` **tüketmez** (§8.2, F-CALENDAR-DAYS) |
| **`Campaign`** | MOD-0165-FU04 (SHIPPED) | *"Hangi kampanya, hangi amaçla?"* | Kampanyanın **kendi** `StartDate`/`EndDate`'i vardır; o kampanyanın **kendi** penceresidir, dönem **değildir**. Campaign **DEĞİŞTİRİLMEZ** (§2.3) |
| **`VisitFrequencyPolicy`** | MOD-0165-FU03 (SHIPPED) | *"Ne sıklıkta ziyaret edilmeli?"* | `PeriodType=cycle` / `CyclePeriodId` referansı bu FU ile **çözümlenebilir** hâle gelir; policy **YAZILMAZ** (§2.3) |
| **`MicroTarget`** | MOD-0155-FU05 (yapılmadı) | *"Bu dönemde, bu temsilcinin, bu hedef için planı ne?"* | Birincil **tüketici**. Bu FU MicroTarget satırı **üretmez** (§2.2) |
| **`StrategyTemplate`** | MOD-0167-FU04 | *"Standart play nedir?"* | Play'i bir döneme **uygulamak** MOD-0155'in işidir; bu FU `apply` **açmaz** |

> **Tek cümlelik sınır:** *Working Calendar günün **niteliğini** söyler; CyclePeriod dönemin **kimliğini** söyler.
> İkisini birleştirip "bu dönemde kaç çalışma günü var?" sorusunu cevaplamak **MOD-0155'in** işidir, bu FU'nun değil.*

### 1.2 D-Karar özeti (onayınıza sunulur — tam gerekçe: [Ek D](#ek-d--karar-gerekçeleri-tam))

| # | Karar | **Önerilen** |
|---|---|---|
| **D-FU** | FU numarası | **MOD-0165-FU06** (FU01/02/05 pack, FU03/04 runtime kodu; ilk boş id — DCP-002 exit 0) |
| **D-GOLDEN** | Golden reference | **Slim** — 8 kullanıcı alanı (§11.1 türetmesi gösterilir). `CycleStatus` **form alanı değildir** (lifecycle aksiyonu) |
| **D-HIER** | Cycle hiyerarşisi | **DÜZ (flat).** `Year` + `SequenceInYear` **alandır**, ayrı `CycleCalendar`/`CyclePlan` aggregate'i **AÇILMAZ**. `VisitFrequencyPolicy.CycleId` **çözümlenmemiş kalır** ve bu açıkça ilan edilir (`supportsCycleCalendarHierarchy: false`, F-CYCLE-CALENDAR) |
| **D-OVERLAP** | Çakışma | **`active` dönemler arasında AYNI scope'ta çakışma YASAK (409).** `draft` dönemler serbestçe çakışır (planlama alanı); `closed` dönemler engellemez. Kontrol `activate` anında fail-closed çalışır |
| **D-ACTIVE** | Tek-aktif kuralı | **"Tek aktif satır" kuralı YOK** (tüm yılı planlamayı yasaklardı). Bunun yerine D-OVERLAP'ten **türetilen** garanti: *herhangi bir ana, scope başına en fazla BİR aktif dönem denk gelir* → `resolve-active` deterministik olarak 0 veya 1 döner |
| **D-STATUS** | Lifecycle | **`draft → active`**, **`draft → closed`**, **`active → closed`**. Geri dönüş **YOK** (`closed` terminaldir, 409). **Zaman durumu değiştirmez** — auto-close job **YOK** (`supportsCycleAutoClose: false`); süresi geçmiş `active` dönem sadece `resolve` etmez |
| **D-DATING** | Effective dating | **Dönemin kendi `StartDate`/`EndDate`'i EFFECTIVE PENCERESİDİR.** Ayrı `EffectiveFrom`/`EffectiveTo` çifti **AÇILMAZ** (iki tarih çifti = iki gerçek). FU-A/FU04'ten devralınan şey **disiplindir**: kayıt yalnız penceresi `at`'i kapsıyorsa seçilebilir |
| **D-VER** | Sürümleme | **v1'de `new-version` klonu YOK** (`supportsCyclePeriodVersioning: false`). Dönem yazılmış bir *play* değil, bir **takvim olgusudur**; klonlamak MicroTarget'ın id ile bağlandığı geçmişi çatallar. Tarih düzeltmesi yalnız `draft`'ta; aktif dönemde tarih **immutable** → F-RESCHEDULE |
| **D-SCOPE** | Tenant mi global reference mi | **Tenant-scoped `EntityBase`.** Dönem takvimi tenant'ın iş kararıdır; MOD-0048 reference data **değildir** (dönem = tarih + lifecycle, kod-etiket çifti değil) |
| **D-BU** | Business unit | **Opsiyonel `BusinessUnitId`** (opak MOD-0048 kodu). `null` = tenant geneli. Çakışma ve benzersizlik kuralları **scope başına** işler; `resolve` BU-özel → tenant-geneli **specificity** sırasıyla seçer, **birleştirmez** (VFP `BusinessUnit` emsali) |
| **D-VOCAB** | Vokabüler | **A = in-domain fail-closed** (`CyclePeriodStatuses`). MOD-0048 publish runtime **ön koşulu değildir** (FU02/FU04/FU05 + MOD-0164-FU02 + MOD-0167-FU04 emsali) |
| **D-SEAM** | Tüketim seam'i | **`ICyclePeriodReader`** — in-process, **salt-okunur**, HTTP self-call **YOK**. Sonuç `resolved` / `none` / `ambiguous` (VFP resolve engine emsali: *unknown ≠ default*) |
| **D-FILES** | Backend dosya düzeni | **Golden Reference kanonik düzeni** (`Commands/`, `Queries/`, `Handlers/CommandHandlers/`, `Handlers/QueryHandlers/`, `Validators/`, `CyclePeriodModels.cs`). Mevcut CRM feature'larındaki gruplanmış dosya sapması **devralınmaz**; sapma F-FILE-DRIFT olarak kaydedilir |
| **D-RBAC** | Yetki | 3 kanonik anahtar **tanımlanır**, seed/grant **YOK**; belgelenmiş DEV-ONLY fallback (MOD-0167-FU04 emsali) |

### 1.3 Bu FU'nun MOD-0155'e (MicroTarget) sağladığı şey

```text
MOD-0155-FU05 sorusu : "Bu dönemde, bu temsilcinin, bu hedef için planı ne?"
FU06'nın cevabı      : ICyclePeriodReader.ResolveActiveAsync(at, businessUnitId)
                       → resolved { CyclePeriodId, CycleCode, Year, SequenceInYear, StartDate, EndDate }
                       | none | ambiguous
                       — SALT-OKUNUR. Satır üretimi YOK, yazma YOK, çalışma-günü hesabı YOK.
```

MicroTarget bu demeti okur ve **kendi** satırlarını üretir; satırına `CyclePeriodId` **referansı** koyar,
dönemin adını/tarihlerini **kopyalamaz** (kopya bayatlar — MOD-0165-FU04 `CampaignTarget` *provenance-only* emsali).

---

## 2. Ownership and Boundaries

**In-scope:** `CyclePeriod` aggregate root'u · CRUD-minus-delete (create / read / update / activate / close;
**DELETE ve PATCH yok**) · çakışma ve benzersizlik doğrulaması · in-domain vokabüler ·
salt-okunur tüketim seam'i (`ICyclePeriodReader`) + HTTP `resolve-active` yüzeyi · contract endpoint'i ·
CRM Admin **tek** Slim sayfa (`/CRM/CyclePeriods`) · 7 dil RESX · Gateway route **talebi** (yazımı değil).

**Out-of-scope (YASAK):** `Campaign` / `CampaignTarget` **mutation** · MicroTarget satırı **üretimi** ·
`VisitFrequencyPolicy` **yazımı** (create/update/`Source` üretimi dâhil) · visit / route / schedule planlama ·
çalışma günü / tatil hesabı · `CycleCalendar` / `CyclePlan` hiyerarşi aggregate'i · auto-close scheduler / job ·
`StrategyTemplate` apply/generate · segment üyelik çözümlemesi · sürüm klonu · hard delete · bulk-delete ·
RBAC seed / grant · MOD-0048 publish · `ocelot.json` yazımı · registry yazımı · Mongo hand-edit · migration.

### 2.1 Kilitli sınırlar (kullanıcı talebinden — değiştirilemez)

| Sınır | Karar |
|---|---|
| CyclePeriod'un doğası | **Dönem master'ı.** Kampanya, hedef ve frekans **SAHİPLENİLMEZ** |
| Working Calendar | **AYRI KAVRAM.** Bu FU tatil/çalışma-günü **bilmez**, CAND-CAP-0008 provider'ını **tüketmez** |
| MOD-0165-FU04 (Campaign) | `Campaign` / `CampaignTarget` **hiç dokunulmaz**; `Campaign`'e `CyclePeriodId` alanı **eklenmez** (F-CAMPAIGN-BIND) |
| MOD-0165-FU03 (Frequency) | Policy **YAZILMAZ**. `IVisitFrequencyPolicyResolver` imzası **genişletilmez**; resolver bu FU'yu **çağırmaz** |
| MOD-0155 (MicroTarget) | Satır **üretilmez**; yalnız READ contract sunulur |
| MOD-0167-FU04 | `StrategyTemplate` **değişmez**; `supportsStrategyApply` bayrağı bu pack tarafından **çevrilmez** |
| SoR | **MOD-0165.** Dönem MOD-0155'e, MOD-0167'ye veya MOD-0048'e **taşınmaz** |
| Legacy CrmV2 | **adapt-not-copy.** Legacy tablo/controller/view **taşınmaz** (§2.4) |
| Golden reference | **Slim** (§11.1 türetmesi) |
| RBAC | Anahtarlar **tanımlanır**; seed/grant **YOK** (§14) |
| Registry / Gateway config | **YAZILMAZ**. Route ihtiyacı `integration-agent` task'ı (§15) |

### 2.2 MOD-0155 sözleşme koruması (kırmızı çizgi)

- Bu FU **hiçbir** MicroTarget / PlannedVisit satırı üretmez ve MOD-0155 repository'lerine **erişmez**.
- `ICyclePeriodReader` **yalnız okur**; hiçbir metodu kayıt oluşturmaz, güncellemez, silmez.
- Seam **in-process**'tir: tüketici HTTP ile kendi servisine geri çağrı yapmaz (MOD-0165-FU03'ün
  *"no consumer re-implements the engine, and there is no HTTP self-call"* kuralı burada da geçerlidir).
- Tüketici dönemi **id ile** referanslar; `CycleCode` / `CycleName` / tarih alanlarını **kopyalamaz**.

### 2.3 MOD-0165 kardeş runtime koruması (kırmızı çizgi)

- `Features/Campaign/**`, `Domain/Entities/Campaign.cs`, `CampaignTarget`, `CampaignsController` — **protected** (§6).
- `Features/VisitFrequencyPolicy/**` — **protected**. Bu FU `IVisitFrequencyPolicyRepository`'yi **hiç** kullanmaz;
  `VisitFrequencyPolicy.CyclePeriodId` alanının *"var olan bir döneme mi işaret ediyor?"* sorusunu **bu FU sormaz**
  (o doğrulama VFP tarafında bir değişiklik gerektirir → F-VFP-FK).
- `VisitFrequencyPolicy.CycleId` **çözümlenmez ve çözümleniyormuş gibi yapılmaz**: `supportsCycleCalendarHierarchy: false`
  bayrağıyla açıkça reddedilir (sessiz varsayım yasağı).

### 2.4 Legacy CrmV2 — adapt-not-copy (ve bir **düzeltme**)

Legacy kaynak (`C:\CRM2\DitenCrmV2\Diten.Crm.V2`) 2026-08-28'de okundu. Görev girdisindeki
*"legacy'de `Applicable` = cycle"* varsayımı **kodla doğrulanmadı** — bulgular:

| Legacy nesne | Legacy'de **gerçekte** ne (kanıt) | vNext karşılığı |
|---|---|---|
| `Applicable` | **Uygulanabilirlik ayırıcısı** — `TargetCustomer.ApplicableId` üzerinde `//ApplicableId == Who is this loyalty for` yorumu ve `if (request.ApplicableId==1)` dalı **workplace ↔ client** ayrımını yapar (`CreateTargetCustomerHandler.cs:19,41`). DTO'su yalnız `Id` + `Name` + audit taşır (`ApplicableResponse.cs`); **tarih alanı YOK** | **Dönem DEĞİL.** vNext'te bu ayrım zaten `SubjectType` (`account` \| `contact`) olarak `Segment` / `StrategyTemplate` içinde vardır — **yeni nesne gerekmez** |
| `PlannedPromoWeek` | `UCLNListPriorityDetail.PlannedPromoWeek` — **master'ı olmayan çıplak `int` hafta numarası** | **Bu FU'nun kapattığı gerçek borç:** adsız/tarihsiz bir dönem numarası → adlandırılmış, tarihli, yaşam döngülü `CyclePeriod` **referansı** |
| `GetSalesByPeriodQuery` | `TimeRange` (`Yearly/Quarterly/Monthly/Weekly`) + `StartDate` + `EndDate` — **ad-hoc rapor parametresi**, master değil | Raporlama penceresi ≠ planlama dönemi. Bu FU rapor penceresi **üretmez** |
| `TargetCustomer` (legacy) | Temsilci-başına promosyon planı (Zone/Applicable/Workplace/Client) | **MOD-0155 (MicroTarget)** — ⚠️ MOD-0167-FU02'nin `TargetCustomer`'ı **başka bir şeydir** (MOD-0167-FU04 §2.4 uyarısı geçerli) |

> **Sonuç (onaya sunulur):** Legacy'de **hiçbir `CyclePeriod` aggregate'i yoktur.** Bu FU legacy'den bir tablo
> taşımaz; legacy'nin **eksiğini** kapatır. `Applicable` bu FU'nun kapsamına **girmez** ve bu FU'da
> `Applicable` adında bir alan/kavram **açılmaz**. Kullanıcının aksi bir bilgisi varsa (ör. legacy DB'de
> koda yansımamış bir `Applicable` tarih kolonu) bu satır D-kararı olarak **yeniden değerlendirilir**.

---

## 3. Owned Objects

| Tür | Nesne |
|---|---|
| **Entity** | `CyclePeriod` (aggregate root — gömülü tip **yok**) |
| **Vokabüler** | `CyclePeriodStatuses` (in-domain sabit sınıfı: `draft` \| `active` \| `closed`) |
| **Repository** | `ICyclePeriodRepository` (**1 repo, 1 collection**: `cycle_periods`) |
| **Commands** | `CreateCyclePeriodCommand` · `UpdateCyclePeriodCommand` · `ActivateCyclePeriodCommand` · `CloseCyclePeriodCommand` |
| **Queries** | `GetCyclePeriodListQuery` · `GetCyclePeriodByIdQuery` · `GetCyclePeriodContractQuery` · `ResolveActiveCyclePeriodQuery` · `GetCyclePeriodSelectorQuery` |
| **Services** | `CyclePeriodOverlapRules` (saf fonksiyon — çakışma/benzersizlik kararı) · `CyclePeriodResolveEngine` (saf fonksiyon — specificity + ambiguity) |
| **Consumer seam** | `ICyclePeriodReader` (**salt-okunur**, in-process; MOD-0155-FU05 tüketicisi için) |
| **API** | §8.1 — 9 endpoint, hepsi `/api/crm/cycle-periods…` altında |
| **Frontend route** | `/CRM/CyclePeriods` (tek Slim sayfa) + same-origin proxy `/CRM/CyclePeriods/api` |
| **Permissions** | `crm.cycle-period.read` · `crm.cycle-period.manage` · `crm.cycle-period.activate` (§14) |

---

## 4. Entity Fields

### 4.1 `CyclePeriod` (aggregate root)

| Alan | Tip | Zorunlu | Kural |
|---|---|---|---|
| `Id` | Guid | otomatik | `CyclePeriodId`. `EntityBase` |
| `TenantId` | Guid | server-side | Payload'da **yer almaz** (D-SCOPE) |
| `CycleCode` | string | **Evet** | Kararlı iş anahtarı; tenant içinde **silinmemiş TÜM satırlar** arasında unique (`closed` dâhil — kapanmış dönemin kodu kalıcı tarihsel kimliktir, yeniden kullanılamaz). **Rename edilmez**. Handler'da doğrulanır (§4.4) |
| `CycleName` | string | **Evet** | max 200, trim. Görünen ad; rename **buradan** yapılır |
| `Year` | int | **Evet** | **Planlama yılı.** `StartDate`'ten **TÜRETİLMEZ** — yıl sınırını aşan dönem (Ara 2026 – Oca 2027) gerçektir ve hangi yıla ait sayıldığı **iş kararıdır**, takvim kararı değil. Aralık: 2000–2100 |
| `SequenceInYear` | int | **Evet** | Yıl-içi sıra (1, 2, 3…). ≥ 1, ≤ 99. `(TenantId, BusinessUnitId-scope, Year, SequenceInYear)` **unique** (silinmemişler arasında; `closed` dâhil) |
| `StartDate` | DateTimeOffset | **Evet** | **Tarih semantiği**: server-side UTC gün başına normalize edilir (`.Date`, `TimeSpan.Zero`) |
| `EndDate` | DateTimeOffset | **Evet** | **DÂHİL (inclusive)** son gün; UTC gün başına normalize. `EndDate > StartDate` (**eşit olamaz** — sıfır günlük dönem plan değildir) |
| `BusinessUnitId` | string? | Hayır | Opak MOD-0048 business-unit kodu (yalnız boş-olmayan string doğrulaması; master **okunmaz**). `null` = tenant geneli (D-BU) |
| `Description` | string? | Hayır | max 2000 |
| `CycleStatus` | string | **Evet** | `CyclePeriodStatuses`: `draft` (varsayılan) \| `active` \| `closed`. **Form alanı değildir** — yalnız `activate` / `close` endpoint'leriyle değişir |
| `ActivatedAt` / `ActivatedBy` | DateTimeOffset? / string? | server-side | `activate` anında dolar |
| `ClosedAt` / `ClosedBy` | DateTimeOffset? / string? | server-side | `close` anında dolar |
| `CreatedBy` / `UpdatedBy` | string? | server-side | Audit |
| `Version` | int | teknik | `EntityBase` **concurrency token'ı**. İş sürümü **değildir** ve bu FU'da iş sürümü **yoktur** (D-VER) |
| `IsDeleted` / `DeletedAt` | bool / DateTimeOffset? | teknik | `EntityBase` soft-delete alanları; bu FU **hiçbir yerde `true` yapmaz** (kapatma = `closed`) |

**Davranış (saf, engine değil):**

```csharp
public bool CoversInstant(DateTimeOffset at) => StartDate <= at && at <= EndDate;
public bool OverlapsWith(CyclePeriod other) => StartDate <= other.EndDate && other.StartDate <= EndDate;
```

### 4.2 Vokabüler — **D-VOCAB = A (in-domain fail-closed)**

```csharp
public static class CyclePeriodStatuses
{
    public const string Draft  = "draft";
    public const string Active = "active";
    public const string Closed = "closed";
    public static readonly IReadOnlyList<string> All = new[] { Draft, Active, Closed };
}
```

- Listede **olmayan** bir değer → **400** (fail-closed). Bilinmeyen değer **asla** `draft`'a düşürülmez.
- MOD-0048 reference-set publish'i bu FU'nun **runtime ön koşulu değildir** (FU02/FU04/FU05, MOD-0164-FU02,
  MOD-0167-FU04 emsali). MOD-0048'e taşıma kararı → §20/F-VOCAB-0048.

### 4.3 Persistence kararı — **1 collection**

- Collection: `cycle_periods`. Gömülü tip yok, ikinci repo yok.
- **Class-map zorunlu:** yeni aggregate `RegisterClassMaps` içine eklenmezse `Guid` alanlar binary yazılır ama
  filtre string serialize eder → **sorgular sessizce BOŞ döner** (bilinen CRM tuzağı, §19.2).
- **DateTimeOffset tuzağı:** `StartDate`/`EndDate` BSON'da `[ticks, offset]` **dizisi** olarak durur.
  → İkisi **birlikte index'lenmez** ve **birlikte sort edilmez** (parallel-arrays 500). Liste varsayılan sıralaması
  `Year DESC, SequenceInYear DESC` (int alanlar) — tarih **değil** (§19.2).

### 4.4 Index & benzersizlik kararı

| Kural | Nerede uygulanır | Neden |
|---|---|---|
| `CycleCode` unique (tenant, silinmemiş) | **Handler** (`ExistsByCodeAsync`) | Mongo **partial index filtresinde `$ne` desteklenmez** ve servis crash-loop'a girer (bilinen tuzak). MOD-0167-FU04 `TemplateCode` emsali |
| `(Year, SequenceInYear)` unique (tenant + BU scope) | **Handler** (`ExistsBySequenceAsync`) | Aynı gerekçe + kural **scope'a** bağlı (BU-null ve BU-dolu ayrı uzaylar) |
| Aktif çakışma yasağı | **Handler** (`activate` öncesi + `update` öncesi) | Küme kuralı; DB index ile ifade edilemez |
| Sorgu index'i | `{ TenantId: 1, IsDeleted: 1, CycleStatus: 1, Year: -1 }` (tek DateTimeOffset içermez) | Liste + resolve yolu; parallel-array tuzağından uzak |

---

## 5. Repo Scope

**Backend — `services/Diten.CrmService/`**

```text
src/Diten.CrmService.Domain/Entities/CyclePeriod.cs                                   (YENİ)
src/Diten.CrmService.Domain/Repositories/ICyclePeriodRepository.cs                     (YENİ)
src/Diten.CrmService.Application/Features/CyclePeriod/**                               (YENİ — §10)
src/Diten.CrmService.Infrastructure/Persistence/CyclePeriodRepository.cs               (YENİ)
src/Diten.CrmService.Infrastructure/Persistence/MongoClassMaps.cs                      (yalnız +1 kayıt)
src/Diten.CrmService.Api/Controllers/CRM/CyclePeriodsController.cs                     (YENİ)
src/Diten.CrmService.Api/Program.cs / DependencyInjection                              (yalnız +DI kaydı)
tests/Diten.CrmService.Application.Tests/CyclePeriod/**                                (YENİ)
```

**Frontend — `frontend/Diten.Web/`**

```text
Controllers/CRM/CyclePeriodsController.cs                                              (YENİ — same-origin proxy)
Views/CRM/CyclePeriods/**                                                              (YENİ — §11.2)
wwwroot/assets/js/CRM/CyclePeriods/index.js                                            (YENİ)
wwwroot/assets/js/CRM/CyclePeriods/index.l10n.js                                       (YENİ)
Resources/Views/CRM/CyclePeriods/CyclePeriodsIndex.{ar,en,es,fr,ru,tr,zh}.resx         (YENİ — 7 dil)
Views/Shared/_LayoutTenantShell.cshtml                                                 (DAR İSTİSNA — §9.2)
```

**Scripts**

```text
scripts/smoke-mod0165-fu06-cycle-period-authenticated.ps1                              (YENİ)
```

---

## 6. Protected Paths

- `.antigravity/**` (global engineering system)
- `gateway/Diten.ApiGateway/**/ocelot.json` (**yalnız** `integration-agent` — §15)
- `execution/registries/**` (registry yazımı pack yetkisi dışında — F-REGISTRY)
- `services/Diten.CrmService/**/Features/Campaign/**` · `Domain/Entities/Campaign.cs` · `CampaignTarget` · `CampaignsController` (**MOD-0165-FU04/FU05**)
- `services/Diten.CrmService/**/Features/VisitFrequencyPolicy/**` · `Domain/Entities/VisitFrequencyPolicy.cs` (**MOD-0165-FU03**)
- `services/Diten.CrmService/**/Features/StrategyTemplate/**` · `Domain/Entities/StrategyTemplate.cs` (**MOD-0167-FU04**)
- `services/Diten.CrmService/**/Features/Segmentation/**` · `Territory/**` · `Knowledge/**` · `ConsentPreference/**`
- Diğer servisler: `services/Diten.Platform/**`, `Diten.AuthService/**`, `Diten.MdmService/**`, `Diten.HcmService/**`, `Diten.EnterpriseStrategyService/**`, `Diten.DevEnablementService/**`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` (**FROZEN**)
- `frontend/Diten.Web/Controllers/Archive/**`, `Views/Archive/**` (**FROZEN** — legacy taşınmaz)
- MOD-0048 reference-data publish yüzeyleri (bu pack publish **yapmaz**)

---

## 7. Dependencies

| Bağımlılık | Yön | Durum | Not |
|---|---|---|---|
| **MOD-0165** (parent) | SoR | Blueprint | Registry: *"Owns Campaign/CyclePeriod execution"* |
| **MOD-0165-FU03** VisitFrequencyPolicy | bu FU **çözümlenebilir kılar** | SHIPPED | `CyclePeriodId` sarkan referansı artık bir master'a işaret edebilir; **VFP değişmez**, FK doğrulaması F-VFP-FK |
| **MOD-0165-FU04/FU05** Campaign | komşu | SHIPPED | **Dokunulmaz**; Campaign'e cycle alanı eklenmez (F-CAMPAIGN-BIND) |
| **MOD-0167-FU04** StrategyTemplate | komşu | SHIPPED | `supportsStrategyApply: false` beyanının karşı tarafı; template **değişmez** |
| **MOD-0155-FU05** MicroTarget | **tüketici** | yapılmadı | Bu FU'nun birincil müşterisi; seam §8.6 |
| **CAND-CAP-0008** Working Calendar | **ayrı kavram** | SHIPPED (PSS) | Bu FU **tüketmez** (F-CALENDAR-DAYS) |
| **MOD-0048** Reference Data | opak tüketim | mevcut | `BusinessUnitId` yalnız string; vokabüler in-domain |
| **MOD-0018** RBAC | tüketim | mevcut | Seed/grant **yok**; DEV-ONLY fallback (§14) |
| **DEV-0000** Golden Slim | şablon | mevcut | §10/§11 birebir |
| **Gateway** (Ocelot) | **route gerekli** | eksik | `/api/crm` wildcard **yok** (§15) |

---

## 8. Runtime Constraints

- **Multi-tenancy:** `TenantId` server-side çözümlenir, payload'da **asla** yer almaz; cross-tenant erişim **404**
  (boş liste / bulunamadı), 403 değil (varlık sızdırmaz).
- **Soft lifecycle:** hard delete **yok**, bulk-delete **yok**, PATCH **yok**. Kapatma = `closed`.
- **Concurrency:** güncelleme/lifecycle çağrıları `expectedVersion` ile `ReplaceAsync`; uyuşmazlık → **409**,
  sessiz overwrite **yasak** (MOD-0162-FU04 emsali).
- **Transaction:** tek doküman yazımı — çok-dokümanlı atomik yazım **yok**, dolayısıyla standalone Mongo
  transaction fallback'i **gerekmez** (yine de `StartTransaction` **çağrılmaz**).
- **Engine yok:** scheduler, job, auto-close, auto-generate **yoktur**. Zaman bir kaydı **değiştirmez**.
- **Frontend transport:** browser JS **yalnız** same-origin proxy `/CRM/CyclePeriods/api` çağırır; Gateway URL'i
  veya bearer token **kullanmaz** (api-profile = `proxy`, Segments/Campaigns emsali).

### 8.1 API Contract

| # | Method + Path | Permission | Davranış |
|---|---|---|---|
| 1 | `GET /api/crm/cycle-periods` | `.read` | DataTable v2 sayfalama; filtreler: `year`, `status`, `businessUnitId`, `q` (code/name), `coversDate` |
| 2 | `GET /api/crm/cycle-periods/{id}` | `.read` | Tekil; cross-tenant **404** |
| 3 | `POST /api/crm/cycle-periods` | `.manage` | Oluşturur; **her zaman `draft`** (status payload'dan **alınmaz**) |
| 4 | `PUT /api/crm/cycle-periods/{id}` | `.manage` | `draft`: tüm alanlar. `active`: **yalnız** `CycleName` + `Description` (tarih/yıl/sıra/BU **immutable**, ihlal → **409**). `closed`: **hiçbir** alan (**409**) |
| 5 | `POST /api/crm/cycle-periods/{id}/activate` | `.activate` | `draft → active`. Çakışma kontrolü **burada** fail-closed çalışır (§13) |
| 6 | `POST /api/crm/cycle-periods/{id}/close` | `.activate` | `draft → closed` **veya** `active → closed`. Terminal |
| 7 | `GET /api/crm/cycle-periods/resolve-active?at=&businessUnitId=` | `.read` | §8.6 seam'inin HTTP yüzeyi; `resolved` / `none` / `ambiguous` döner. **Kayıt oluşturmaz** |
| 8 | `GET /api/crm/cycle-periods/selector?year=&status=` | `.read` | Gelecek tüketici UI'ları için hafif liste (`id`, `code`, `name`, `year`, `sequence`, `start`, `end`) |
| 9 | `GET /api/crm/cycle-periods/contract` | `.read` | §8.2 bayrakları |

**Yok (kasten):** `DELETE`, `PATCH`, `POST /bulk-delete`, `POST /{id}/reopen`, `POST /{id}/apply`,
`POST /{id}/generate`, `GET /{id}/working-days`.

### 8.2 Contract flags

```jsonc
{
  "supportsCyclePeriod": true,
  "supportsCyclePeriodLifecycle": true,       // draft → active → closed
  "supportsActiveCycleResolution": true,      // resolve-active (0 veya 1)
  "supportsBusinessUnitScopedCycles": true,   // D-BU

  "supportsCycleOverlap": false,              // aktif çakışma YASAK (D-OVERLAP)
  "supportsCycleCalendarHierarchy": false,    // VFP.CycleId master'ı YOK → F-CYCLE-CALENDAR
  "supportsCyclePeriodVersioning": false,     // D-VER
  "supportsCycleReschedule": false,           // aktif dönemde tarih immutable → F-RESCHEDULE
  "supportsCycleAutoClose": false,            // scheduler/job YOK
  "supportsWorkingCalendarIntegration": false,// CAND-CAP-0008 → F-CALENDAR-DAYS
  "supportsWorkingDayCount": false,

  "supportsMicroTargetGeneration": false,     // MOD-0155-FU05
  "supportsCampaignBinding": false,           // Campaign DEĞİŞMEZ → F-CAMPAIGN-BIND
  "supportsFrequencyPolicyWrite": false,      // MOD-0165-FU03 SoR
  "supportsFrequencyPolicyBackReference": false, // VFP.CyclePeriodId FK doğrulaması → F-VFP-FK
  "supportsStrategyApply": false,             // MOD-0167-FU04 D-APPLY hâlâ kapalı
  "supportsHardDelete": false,
  "supportsBulkDelete": false
}
```

> **Bayrak kuralı:** `false` bir **beyandır**, bir eksiklik itirafı değildir. Tüketici, kapalı bir yeteneği
> "herhalde vardır" diye varsaymak yerine contract'ı okur (sessiz varsayım yasağı — MOD-0167-FU04 emsali).

### 8.3 Çakışma semantiği (D-OVERLAP) — kesin kural

**Scope** = `(TenantId, BusinessUnitId)`; `BusinessUnitId = null` **kendi başına bir scope'tur** (tenant geneli),
BU-dolu scope'larla **çakışma kontrolüne girmez**.

| Durum | Kural |
|---|---|
| İki `active` dönem, **aynı** scope, kesişen tarih | **YASAK → 409** (`cycle_period_overlap`) |
| İki `active` dönem, **farklı** scope | **Serbest** (farklı iş birimleri kendi takvimlerini kurar) |
| `draft` ↔ herhangi bir dönem | **Serbest** (planlama alanı; alternatif senaryolar çizilebilir) |
| `closed` ↔ herhangi bir dönem | **Serbest** (geçmiş engellemez) |
| Bitişik dönemler | `EndDate` **dâhil** olduğu için bir sonraki dönem **`EndDate + 1 gün`** veya sonrasında başlar; `StartDate(n+1) == EndDate(n)` **çakışmadır → 409** |

Kontrol **iki** yerde çalışır: `activate` sırasında (asıl kapı) ve `active` bir kaydın güncellenmesi sırasında
(ki §8.1/#4 gereği tarih zaten immutable olduğundan pratikte savunma katmanıdır).

### 8.4 `resolve-active` kararı (deterministik, engine değil)

```text
girdi : at (DateTimeOffset, zorunlu), businessUnitId (opsiyonel)
adım 1: TenantId + IsDeleted=false + CycleStatus=active + StartDate <= at <= EndDate
adım 2: specificity —
        businessUnitId dolu  → önce BusinessUnitId == businessUnitId; hiç yoksa BusinessUnitId == null
        businessUnitId boş   → yalnız BusinessUnitId == null   (BU-özel dönem tenant geneline SIZMAZ)
adım 3: seçilen kümede
        0 kayıt  → outcome = "none"       (tahmin YOK, en yakın dönem YOK)
        1 kayıt  → outcome = "resolved"
        >1 kayıt → outcome = "ambiguous"  (D-OVERLAP ihlal edilmiş veri) + reason + aday id listesi
```

- **`none` ≠ varsayılan.** Dönem yoksa "bir dönem uydurulmaz"; tüketici kendi kararını verir
  (MOD-0165-FU03 *"unknown ≠ default"* ve *"tie = conflict"* emsali).
- **BU-özel ile tenant-geneli BİRLEŞTİRİLMEZ** — specificity seçer, merge etmez.
- Süresi geçmiş ama `close` edilmemiş `active` dönem, `at` penceresi dışında kaldığı için **doğal olarak**
  seçilmez; hiçbir job durumunu değiştirmez.

### 8.5 Fail-closed matrisi

| Durum | Sonuç |
|---|---|
| Bilinmeyen `CycleStatus` değeri | **400** — asla `draft`'a düşürülmez |
| `EndDate <= StartDate` | **400** |
| `activate` sırasında çakışma | **409** `cycle_period_overlap` — kayıt **aktifleşmez** |
| `closed` kayda herhangi bir mutasyon | **409** `cycle_period_closed` |
| `active` kayıtta tarih/yıl/sıra/BU değişikliği | **409** `cycle_period_dates_immutable` |
| `resolve-active` çoklu aday | **`ambiguous`** — 200 + açık sonuç; **tahmin YOK** |
| Cross-tenant id | **404** |
| `expectedVersion` uyuşmazlığı | **409** |

### 8.6 Tüketim seam'i — `ICyclePeriodReader` (read-only)

```csharp
namespace Diten.CrmService.Application.Features.CyclePeriod.Read;

public interface ICyclePeriodReader
{
    Task<CyclePeriodResolution> ResolveActiveAsync(
        DateTimeOffset at, string? businessUnitId, CancellationToken ct);

    Task<CyclePeriodSnapshot?> GetByIdAsync(Guid cyclePeriodId, CancellationToken ct);

    Task<IReadOnlyList<CyclePeriodSnapshot>> ListByYearAsync(
        int year, string? businessUnitId, CancellationToken ct);
}

public sealed record CyclePeriodSnapshot(
    Guid CyclePeriodId, string CycleCode, string CycleName,
    int Year, int SequenceInYear,
    DateTimeOffset StartDate, DateTimeOffset EndDate,
    string CycleStatus, string? BusinessUnitId);

public sealed record CyclePeriodResolution(
    string Outcome,                       // resolved | none | ambiguous
    CyclePeriodSnapshot? Snapshot,
    IReadOnlyList<Guid> CandidateIds,     // ambiguous'ta dolu
    string? Reason);
```

**Seam kuralları (testle sabitlenir — §17.2):**

1. **Salt-okunur.** Hiçbir metot yazmaz; implementasyon `InsertAsync`/`ReplaceAsync` **çağırmaz**.
2. **In-process.** `HttpClient` **kullanmaz** (HTTP self-call yasağı).
3. **Tenant bağlamı** çağıran isteğin bağlamıdır; seam kendi başına tenant seçmez.
4. **Motor tek yerdedir.** Tüketici `active + tarih penceresi` mantığını **yeniden yazmaz**; seam çağırır.
5. `Outcome` string'i contract ile **aynı** vokabülerdir; tüketici `none`'ı bir döneme **çevirmez**.

---

## 9. Layout & Shell Contract

`shell: tenant` → **Razor layout AÇIKÇA yazılır**; `_ViewStart.cshtml` varsayılanına güvenilmez.

```cshtml
@{
    ViewData["Title"] = Localizer["PageTitle"];
    Layout = "_LayoutTenantShell";   // shell: tenant — AÇIKÇA
}
```

| Öğe | Değer |
|---|---|
| Layout | `_LayoutTenantShell` (tüm `Views/CRM/CyclePeriods/*.cshtml` içinde açıkça) |
| View klasörü | `frontend/Diten.Web/Views/CRM/CyclePeriods/` |
| MVC route | `/CRM/CyclePeriods` |
| Proxy route | `/CRM/CyclePeriods/api` (same-origin; api-profile = `proxy`) |
| Canlı emsal | `Views/CRM/Segments/` (CRM tenant sayfası) + `Views/DevEnablement/GoldenReferenceSlim/` (Slim şablonu) |
| `_Layout.cshtml` | **FROZEN** — dokunulmaz |

### 9.2 Navigation (dar istisna)

`_LayoutTenantShell.cshtml` domain config'te korumalıdır. Bu pack **yalnızca** aşağıdaki tek `<li>` için dar,
test edilebilir bir istisna talep eder (how-to-add-a-module Adım 9; MOD-0165-FU05 emsali):

```cshtml
@if (Perms.Has("crm.cycle-period.read")) { /* CRM Admin → Cycle Periods */ }
```

Menü girdisi **permission-guard'lı** olmak zorundadır; guard'sız `<li>` kabul edilmez. Nav loader/engine
değişikliği **yoktur** (MOD-0285 kapsamı — bu pack'in dışında).

---

## 10. Backend File Convention

**D-FILES = Golden Reference kanonik düzeni.** `services/Diten.DevEnablementService/.../Features/GoldenReferenceSlim/`
birebir taklit edilir:

```text
services/Diten.CrmService/src/Diten.CrmService.Application/Features/CyclePeriod/
├── Commands/
│   ├── CreateCyclePeriodCommand.cs          (sealed record)
│   ├── UpdateCyclePeriodCommand.cs          (sealed record)
│   ├── ActivateCyclePeriodCommand.cs        (sealed record)
│   └── CloseCyclePeriodCommand.cs           (sealed record)
├── Queries/
│   ├── GetCyclePeriodListQuery.cs           (sealed record)
│   ├── GetCyclePeriodByIdQuery.cs
│   ├── GetCyclePeriodContractQuery.cs
│   ├── GetCyclePeriodSelectorQuery.cs
│   └── ResolveActiveCyclePeriodQuery.cs
├── Handlers/
│   ├── CommandHandlers/                     ← AYRI klasör (zorunlu)
│   │   ├── CreateCyclePeriodHandler.cs      (sealed class, suffix YOK)
│   │   ├── UpdateCyclePeriodHandler.cs
│   │   ├── ActivateCyclePeriodHandler.cs
│   │   └── CloseCyclePeriodHandler.cs
│   └── QueryHandlers/                       ← AYRI klasör (zorunlu)
│       ├── GetCyclePeriodListHandler.cs
│       ├── GetCyclePeriodByIdHandler.cs
│       ├── GetCyclePeriodContractHandler.cs
│       ├── GetCyclePeriodSelectorHandler.cs
│       └── ResolveActiveCyclePeriodHandler.cs
├── Validators/
│   ├── CreateCyclePeriodValidator.cs        (suffix YOK)
│   └── UpdateCyclePeriodValidator.cs
├── Rules/
│   ├── CyclePeriodOverlapRules.cs           (saf fonksiyon — §8.3)
│   └── CyclePeriodResolveEngine.cs          (saf fonksiyon — §8.4)
├── Read/
│   ├── ICyclePeriodReader.cs                (§8.6 seam)
│   └── CyclePeriodReader.cs
├── CyclePeriodPermissions.cs                (§14 — DEFINITION ONLY)
└── CyclePeriodModels.cs                     ← TEK dosyada tüm DTO/ViewModel'ler
```

**Naming (tartışmasız):** Command `{Verb}{Module}Command` · Query `Get{Module}{Qualifier}Query` ·
Handler `{Verb}{Module}Handler` (**Command/Query suffix YOK**) · Validator `{Verb}{Module}Validator`
(**Command suffix YOK**).

**Response envelope:** tüm endpoint'ler `Response<T>` döner (`response-envelope.md`).

**Bilinen sapma (kayda geçirilir):** mevcut CRM feature'ları (`Features/VisitFrequencyPolicy/`,
`Features/Campaign/`) komut/sorgu/handler'ları **gruplanmış tek dosyalarda** tutar. Bu pack o sapmayı
**devralmaz** (standart Golden Reference'ı otorite sayar) ve mevcut feature'ları **düzeltmez** —
tutarsızlık F-FILE-DRIFT olarak açılır.

---

## 11. Frontend File Contract

### 11.1 Golden reference kararı — kullanıcı-form alanı türetmesi (GÖSTERİLİR)

| # | Create/Edit formundaki kullanıcı alanı | Kontrol |
|---|---|---|
| 1 | `CycleCode` | text (create'te editable, edit'te read-only) |
| 2 | `CycleName` | text |
| 3 | `Year` | number |
| 4 | `SequenceInYear` | number |
| 5 | `StartDate` | date |
| 6 | `EndDate` | date |
| 7 | `BusinessUnitId` | text/select (opak kod) |
| 8 | `Description` | textarea |
| — | ~~`CycleStatus`~~ | **Form alanı DEĞİL** — `activate` / `close` aksiyonlarıyla değişir; formda gösterilirse kullanıcı onu "yazılabilir" sanır |

**Toplam = 8 ≤ 8 → `golden_reference: slim`.**

> **Uyarı:** 8, Slim sınırının **tam** üstüdür. İleride **tek bir** kullanıcı alanı eklenirse pack Compact'a
> düşer ve bu **yeniden yetkilendirme** gerektirir (dosya seti tümüyle değişir). Alan eklemek isteyen her
> follow-up bunu hesaba katmalıdır.

### 11.2 Dosya seti — kanonik Slim 7 dosya (+ JS + RESX)

```text
frontend/Diten.Web/Views/CRM/CyclePeriods/
├── Index.cshtml                    (Layout = "_LayoutTenantShell" AÇIKÇA)
├── _Filter.cshtml
├── _DataTable.cshtml               (data-dt-standard="v2" + skeleton)
├── _IndexL10n.cshtml
├── _CreateEditOffcanvas.cshtml     (Slim-özel)
├── _DetailsQuickView.cshtml        (Slim-özel)
└── CyclePeriodsIndex.cs            (marker class)

frontend/Diten.Web/wwwroot/assets/js/CRM/CyclePeriods/
├── index.js                        (DtDefaults + DataTables v2 ctor; endpoint = '/CRM/CyclePeriods/api')
└── index.l10n.js                   (camelCase → PascalCase köprüsü — atlanırsa window.L10n undefined döner)

frontend/Diten.Web/Resources/Views/CRM/CyclePeriods/
└── CyclePeriodsIndex.{ar,en,es,fr,ru,tr,zh}.resx     (7 dil, parite zorunlu)
```

**Compact'a ait dosyalar (`Create.cshtml`, `Edit.cshtml`, `Details.cshtml`, `_Form.cshtml`) bu pack'te YASAKTIR.**

### 11.3 UI davranış kararları

| Konu | Karar |
|---|---|
| Quick View | Event delegation (`.js-quick-view`); **inline `onclick` yasak** |
| Lifecycle aksiyonları | Satır aksiyonu olarak `Activate` (yalnız `draft`) ve `Close` (yalnız `draft`/`active`); `closed` satırda **hiçbir** mutasyon aksiyonu render edilmez |
| Bulk action bar | **Bulk-delete YOK** (§8.1). Bar yalnız seçim sayacı gösterir veya hiç render edilmez |
| Çakışma hatası | `409 cycle_period_overlap` → alan-üstü (form-level) hata + çakışan dönemin **kodu ve tarih aralığı** mesajda gösterilir (kullanıcı hangi kayıtla çakıştığını görmeden düzeltemez) |
| `resolve-active` | Sayfa üstünde salt-okunur "Şu an geçerli dönem" rozeti; `none` → *"Bu tarih için aktif dönem yok"*, `ambiguous` → **uyarı** rozeti (sessizce ilk kaydı seçmek **yasak**) |
| Tarih girişi | Gün hassasiyeti; saat/zaman dilimi kullanıcıya **gösterilmez** (normalize server-side) |
| Filtre | `Year` (select), `Status` (select), `BusinessUnitId` (text), `q` (arama) — `dt-inline-filter-host` sınıfı zorunlu |

---

## 12. Validation Rules

| Field | Required | Format/Rule | DB-level | Pre-check |
|---|---|---|---|---|
| `CycleCode` | Evet | trim, max 40, `^[A-Za-z0-9._-]+$`, create sonrası **immutable** | index yok (§4.4) | `ExistsByCodeAsync(tenant, code)` |
| `CycleName` | Evet | trim, max 200, boş-değil | — | — |
| `Year` | Evet | int, 2000 ≤ Year ≤ 2100 | — | — |
| `SequenceInYear` | Evet | int, 1 ≤ n ≤ 99 | — | `ExistsBySequenceAsync(tenant, bu, year, seq)` |
| `StartDate` | Evet | geçerli tarih; UTC gün başına normalize | — | — |
| `EndDate` | Evet | geçerli tarih; **`EndDate > StartDate`**; UTC gün başına normalize | — | — |
| `BusinessUnitId` | Hayır | dolu ise trim + boş-olmayan string, max 60 (master **okunmaz**) | — | — |
| `Description` | Hayır | max 2000 | — | — |
| `CycleStatus` | Evet (server) | `CyclePeriodStatuses.All` içinde; payload'dan **kabul edilmez** | — | — |
| — (küme kuralı) | — | `activate` sırasında aynı scope'ta `active` çakışma **yok** | — | `GetActiveOverlappingAsync(tenant, bu, start, end, excludeId)` |
| — (lifecycle) | — | geçiş `D-STATUS` matrisinde olmalı | — | mevcut `CycleStatus` okunur |

**İlişkili kontroller:** `EndDate` → `StartDate` bağımlıdır · `SequenceInYear` benzersizliği `Year` **ve**
`BusinessUnitId` scope'una bağlıdır · `activate` çakışma kontrolü **yalnız** `active` kayıtlara bakar.

---

## 13. Failure Path to Verify

- **Duplicate `CycleCode`** (aynı tenant, `closed` bir kayıtta bile)
  - Expected: **409** + form alan-seviyesi hata + kayıt oluşmaz + reload sonrası temiz state
- **Duplicate `(Year, SequenceInYear)`** aynı BU scope'unda
  - Expected: **409** `cycle_period_sequence_taken` + kayıt oluşmaz
- **Missing `CycleName` / `StartDate` / `EndDate`**
  - Expected: **400** + validator mesajı + save engellenir
- **`EndDate <= StartDate`**
  - Expected: **400** + alan-seviyesi hata
- **Overlap on `activate`** (aynı scope, kesişen `active` dönem)
  - Expected: **409** `cycle_period_overlap` + kayıt **`draft` kalır** + mesajda çakışan dönemin kodu/aralığı
- **`closed` kayda update/activate**
  - Expected: **409** `cycle_period_closed` + hiçbir alan değişmez
- **`active` kayıtta tarih değişikliği**
  - Expected: **409** `cycle_period_dates_immutable` (isim/açıklama değişikliği ise **200**)
- **Concurrency conflict** (`expectedVersion` eski)
  - Expected: **409** + UI *"veri değişti, yeniden yükleyin"* + sessiz overwrite **YOK**
- **Unauthorized actor** (`.read` yok / `.manage` yok / `.activate` yok)
  - Expected: **403** + UI aksiyonu disabled veya permission-denied state; menü girdisi görünmez
- **Cross-tenant id**
  - Expected: **404** (403 değil — varlık sızdırmaz); liste **boş**
- **`resolve-active` çoklu aday** (bozuk veri)
  - Expected: **200** + `outcome: "ambiguous"` + aday id listesi; **hiçbir dönem seçilmez**
- **Upstream 204 proxy'den geçerken** (`activate` / `close` gövdesiz dönerse)
  - Expected: frontend proxy **500 vermez** — bodyless status guard uygulanır (§19.2, bilinen tuzak)

---

## 14. Authorization Convention

```text
Policy:     [Authorize]                                   // shell: tenant
Permission: [HasPermission("crm.cycle-period.{action}")]   // PKS-001: lowercase-dotted, ≥3 segment, kebab-case
Actor type: tenant_user (platform_admin otomatik geçer)
```

| Anahtar | Kapsam |
|---|---|
| `crm.cycle-period.read` | list · byId · selector · contract · **resolve-active** |
| `crm.cycle-period.manage` | create · update |
| `crm.cycle-period.activate` | **activate** ve **close** (ikisi de yaşam döngüsü geçişidir) |

**Kararlar:**

- **`.resolve` anahtarı YOK.** Dönem çözümlemek PII içermez (segment üyeliği gibi değildir); `.read` yeterlidir.
  Bu, MOD-0167-FU02'nin `.resolve` ↔ `.read` PII ayrımıyla **çelişmez** — orada ayrım kişileri görmekle ilgiliydi.
- **`close` ayrı anahtar DEĞİL.** Aktivasyon ve kapatma aynı yönetişim sorumluluğudur; dördüncü bir anahtar
  hiç kimseye verilmeyen ölü bir anahtar olurdu. SoD gerekirse F-RBAC'te ayrıştırılır.
- **Seed/grant bu pack'te YOK.** `CyclePeriodPermissions.cs` **yalnız tanım** dosyasıdır: DB yazımı yok,
  rol şablonu yok, katalog kaydı yok.
- **Belgelenmiş DEV-ONLY fallback** (MOD-0167-FU04 / MOD-0165-FU04 / MOD-0164-FU02 emsali): RBAC kataloğu
  `crm.cycle-period.*` taşımadığı sürece endpoint'ler mevcut CRM anahtarları üzerinden çalışır
  (`ReadFallback = crm.territory.read`, `ManageFallback = crm.territory.model.manage`).
  **Fallback hiçbir guard'ı genişletmez** — tenant izolasyonu, lifecycle, çakışma ve fail-closed vokabüler
  aynen çalışır. Fallback altında `activate` **manage'e çöker**; bu **bilinen ve belgelenmiş** bir dev boşluğudur,
  F-RBAC ile kapanır.

---

## 15. Gateway / API Routing Decision

**Karar: Gateway değişikliği GEREKLİ.**

- Kanıt: `gateway/Diten.ApiGateway/ocelot.json` içinde **`/api/crm` wildcard'ı YOKTUR**; her CRM kaynağı için
  **açık** Upstream/Downstream çifti tanımlıdır (`/api/crm/accounts`, `/api/crm/contacts`,
  `/api/crm/visit-frequency-policies`, `/api/crm/campaigns`, …).
- Gerekli route çifti (OPTIONS dâhil):

```text
/api/crm/cycle-periods                 → Diten.CrmService
/api/crm/cycle-periods/{everything}    → Diten.CrmService
```

- **Bu pack `ocelot.json`'a YAZMAZ.** Route ekleme ayrı bir **`integration-agent`** task'ıdır (F-GATEWAY).
- Route eklenene kadar frontend proxy **404 + `{}` gövdesi** alır — bu, "endpoint yok" değil
  **"route yok"** imzasıdır (bilinen teşhis deseni; §19.2).
- Browser JS **servis portuna gitmez**; yalnız same-origin `/CRM/CyclePeriods/api` proxy'sini çağırır.

---

## 16. Acceptance Criteria

**Kimlik & yönetişim**

1. `py .antigravity/scripts/verify_module_id.py . --check-id MOD-0165-FU06 --name "Cycle Period" --parent MOD-0165` → **exit 0**.
2. Pack `status: draft` ve `runtime_code_allowed: false` iken **hiçbir** runtime dosyası değişmemiştir
   (`git status` temiz — yalnız bu pack dosyası).

**Aggregate & kurallar**

3. `POST /api/crm/cycle-periods` her zaman `CycleStatus = "draft"` üretir; payload'daki `cycleStatus` **yok sayılır**.
4. `EndDate <= StartDate` → **400**; `EndDate == StartDate` de **400**.
5. Aynı tenant'ta ikinci kez aynı `CycleCode` → **409**, kapanmış (`closed`) bir kayıt için de **409**.
6. Aynı `(Year, SequenceInYear, BU-scope)` → **409**; farklı `BusinessUnitId` scope'unda **201**.
7. `activate`, aynı scope'ta kesişen `active` dönem varken → **409** ve kayıt **`draft` kalır**;
   farklı scope'ta veya `draft`/`closed` kayıtla kesişme → **200**.
8. `StartDate(n+1) == EndDate(n)` ile `activate` → **409** (EndDate dâhil olduğu için çakışmadır).
9. `closed` kayda `PUT` / `activate` / `close` → **409**; `active` kayda tarih `PUT`'u → **409**,
   yalnız `CycleName`/`Description` `PUT`'u → **200**.
10. `close`, `draft` ve `active` kayıtlarda çalışır; `closed` → `active` geçişi **hiçbir yolla** mümkün değildir.
11. Bilinmeyen `cycleStatus` filtresi/değeri → **400** (sessizce `draft` varsayılmaz).
12. Eski `expectedVersion` ile `PUT` → **409**, kayıt değişmez.
13. Başka tenant'ın id'si → **404**; liste sorgusu o kaydı **hiç** döndürmez.

**Resolve seam**

14. `resolve-active?at=X` — kapsayan tek `active` dönem varken `outcome: "resolved"` + doğru `cyclePeriodId`.
15. Kapsayan dönem yokken `outcome: "none"`; **en yakın dönem döndürülmez**, `null` bir döneme çevrilmez.
16. BU-özel dönem varken `businessUnitId` **boş** çağrı, o dönemi **döndürmez** (yalnız tenant-geneli).
17. `businessUnitId` dolu çağrıda BU-özel dönem yoksa tenant-geneli döneme düşer; **ikisi birleştirilmez**.
18. Çakışan iki `active` kayıt (elle bozulmuş veri) → `outcome: "ambiguous"` + `candidateIds` dolu; **200**, seçim yok.
19. `ICyclePeriodReader` implementasyonu `HttpClient` **içermez** ve hiçbir yazma metodu **çağırmaz** (test §17.2).

**Sınır ihlali yokluğu (yapısal)**

20. Çözüm ağacında `Features/CyclePeriod/**` altından `Campaign`, `CampaignTarget`, `VisitFrequencyPolicy`,
    `StrategyTemplate`, `Segment` veya MOD-0155 repository'lerine **hiçbir yazma çağrısı** yoktur.
21. `Domain/Entities/Campaign.cs` ve `VisitFrequencyPolicy.cs` **diff'te yer almaz**.
22. `contract` endpoint'i §8.2 bayraklarını **birebir** döner; `supports*: false` olan hiçbir yetenek için
    endpoint/kod yoktur.

**Frontend**

23. `Views/CRM/CyclePeriods/*.cshtml` dosyalarının **hepsinde** `Layout = "_LayoutTenantShell";` **açıkça** yazılıdır.
24. Slim dosya seti tamdır (`_CreateEditOffcanvas.cshtml` + `_DetailsQuickView.cshtml` **var**);
    `Create.cshtml` / `Edit.cshtml` / `Details.cshtml` / `_Form.cshtml` **yoktur**.
25. `py .antigravity/scripts/verify_datatable_page.py . --area CRM --module CyclePeriods --reference slim --api-profile proxy`
    çalıştırılır; sonuç **kaydedilir** ve **bulk-delete kontrolleri hariç** PASS olur (§17.1 — beklenen delta).
26. Browser JS **hiçbir yerde** `5000`/`5058` portunu, Gateway URL'ini veya `Bearer` token'ı kurmaz;
    tek endpoint `'/CRM/CyclePeriods/api'`dır.
27. 7 dil RESX parite: `{ar,en,es,fr,ru,tr,zh}` aynı anahtar kümesine sahiptir; eksik anahtar **yoktur**.
28. `window.L10n` köprüsü çalışır (toast metinlerinde `(undefined: …)` görülmez — camelCase→PascalCase loader).
29. `closed` satırda `Activate`/`Close`/`Edit` aksiyonları **render edilmez**.
30. `ambiguous` resolve sonucu UI'da **uyarı** olarak görünür; sessizce bir dönem seçilmez.
31. Menü girdisi `crm.cycle-period.read` guard'ı **olmadan** render edilmez.

**Kalite kapıları**

32. `dotnet build` (CrmService + Diten.Web + Gateway) **PASS**.
33. Mevcut test paketi **regresyonsuz**; yeni testler §17.2 hedefini karşılar.
34. `scripts/smoke-mod0165-fu06-cycle-period-authenticated.ps1` **tüm adımlar PASS**
    (script çalıştırılmadan "PASS" **rapor edilmez**).

---

## 17. Test Expectations

### 17.1 Build & statik doğrulama

| Kontrol | Komut / beklenti |
|---|---|
| Backend build | `dotnet build services/Diten.CrmService/Diten.CrmService.sln` → **0 error** |
| Frontend build | `dotnet build frontend/Diten.Web/Diten.Web.csproj` → **0 error** (fleet kilidi varsa `-p:BaseOutputPath=.tmp-x/`) |
| Gateway build | **0 error** |
| DataTable verifier | `py .antigravity/scripts/verify_datatable_page.py . --area CRM --module CyclePeriods --reference slim --api-profile proxy` |
| RESX parite | 7 dil anahtar kümesi eşit |
| Module id gate | `verify_module_id.py … --check-id MOD-0165-FU06` → exit 0 |

> **Beklenen verifier delta (ÖNEMLİ):** bu modül **archive/close-only**dir; **bulk-delete yoktur**.
> Verifier'ın bulk-delete ile ilgili kontrolleri bu nedenle **beklenen N/A FAIL** üretir
> (MOD-0162-FU02 emsali). Bu FAIL'ler **"düzeltilmez"** — pack'e **sayı olarak** kaydedilir ve
> implementasyon raporunda baseline ile **birebir** karşılaştırılır. Verifier sayıları **her zaman
> yeniden çalıştırılarak** doğrulanır; hiçbir ajanın kendi bildirdiği sayı kanıt sayılmaz.

### 17.2 Backend unit/integration testleri — hedef **≥ 30 test**

| Grup | Kapsam |
|---|---|
| Validator | zorunlu alanlar · `EndDate > StartDate` · `Year` aralığı · `SequenceInYear` aralığı · `CycleCode` regex/uzunluk · `Description` uzunluğu |
| Benzersizlik | `CycleCode` (aktif + closed) · `(Year, Sequence)` scope başına · farklı BU scope'unda serbest |
| Çakışma | aynı scope `active` çakışma → 409 · farklı scope serbest · `draft`/`closed` engellemez · bitişik gün (`==`) çakışır · `excludeId` kendini saymaz |
| Lifecycle | `draft→active` · `draft→closed` · `active→closed` · `closed→*` 409 · `active→draft` 409 · `activate` iki kez 409 |
| Immutability | `active` kayıtta tarih/yıl/sıra/BU 409 · isim/açıklama 200 · `CycleCode` her durumda immutable |
| Resolve | resolved · none · ambiguous · BU specificity (özel → genel) · BU-boş çağrı BU-özel dönemi **görmez** · pencere sınırları (`StartDate`, `EndDate` **dâhil**) · süresi geçmiş `active` **resolve etmez** |
| Tenant | cross-tenant 404 · liste izolasyonu · `TenantId` payload'dan **alınmaz** |
| Concurrency | `expectedVersion` uyuşmazlığı 409 · sessiz overwrite yok |
| Vokabüler | bilinmeyen status 400 · `CyclePeriodStatuses.All` dışına yazım imkânsız |
| **Sınır (yapısal)** | Reader `HttpClient` **kullanmaz** · Reader hiçbir write metodu **çağırmaz** · handler'lar `IVisitFrequencyPolicyRepository` / `ICampaignRepository` / `IStrategyTemplateRepository`'yi **enjekte etmez** (compile-time + reflection testi) |
| Normalizasyon | `StartDate`/`EndDate` UTC gün başına normalize · saat bileşeni yok sayılır |

### 17.3 Authenticated smoke (Gateway)

`scripts/smoke-mod0165-fu06-cycle-period-authenticated.ps1` — tenant-scoped token
(`X-Tenant-Id` header'ı **zorunlu**; yoksa platform tenant'ı için token alınır ve testler yanıltıcı olur):

1. Login (tenant-scoped) → 2. `contract` bayrakları → 3. create (draft) → 4. duplicate code 409 →
5. duplicate sequence 409 → 6. `EndDate <= StartDate` 400 → 7. activate 200 → 8. çakışan ikinci dönemi
activate 409 → 9. `active` kayıtta tarih PUT 409 → 10. isim PUT 200 → 11. `resolve-active` resolved →
12. pencere dışı `at` → none → 13. BU specificity → 14. close 200 → 15. closed'a PUT/activate 409 →
16. cross-tenant 404 → 17. concurrency 409 → 18. selector shape.

> PowerShell 5.1 tuzağı: `@(Where-Object).Count` sayımı — tek elemanlı sonuçta `.Count` **yok sayılır**;
> sayım her zaman `@()` ile sarılır.

### 17.4 Browser smoke

- `/CRM/CyclePeriods` yüklenir, DataTable v2 render eder, skeleton kaybolur.
- Create offcanvas → kayıt oluşur → tablo yenilenir → toast **doğru dilde** (undefined **yok**).
- Quick View açılır, `closed` satırda mutasyon aksiyonu **yok**.
- Çakışma 409 → form-level hata + çakışan dönem kodu görünür.
- Filtre chip'leri (`dt-inline-filter-host`) çalışır; ikinci bir DataTable yoksa colvis/filter rozeti bozulmaz.
- 7 dilden en az `tr` + `en` + `ar` (RTL) gözle doğrulanır.

---

## 18. Ready-for-dev Checklist

- [ ] **D-listesi (§1.2) kullanıcı tarafından onaylandı** — özellikle D-OVERLAP, D-STATUS, D-DATING, D-VER, D-BU
- [ ] §2.4'teki **legacy `Applicable` düzeltmesi** kabul edildi (ya da aksi kanıt sunuldu)
- [ ] Golden Reference **Slim** referans olarak okundu (`GoldenReferenceSlim` backend + frontend)
- [ ] Frontmatter tüm zorunlu alanlar dolu (`service`, `shell`, `golden_reference`, `entity_base`, `form_field_count`)
- [ ] Layout & Shell Contract'ta Razor `Layout = "_LayoutTenantShell"` açıkça yazılı (§9)
- [ ] Backend File Convention'da `Handlers/CommandHandlers/` + `Handlers/QueryHandlers/` ayrımı var (§10)
- [ ] Frontend File Contract'ta Slim dosya listesi tam; Compact dosyaları listelenmemiş (§11.2)
- [ ] Validation Rules her field için yazılı (§12)
- [ ] Failure Path ≥ 4 senaryo (duplicate, missing, unauthorized, concurrency) — **12 senaryo var** (§13)
- [ ] Authorization Convention: permission listesi + policy + actor type + fallback gerekçesi (§14)
- [ ] Gateway routing kararı açık: **gerekli**, `integration-agent` task'ı (§15)
- [ ] Acceptance criteria test edilebilir (§16, 34 madde)
- [ ] Test expectations build/verifier/RESX/smoke kapsıyor + **beklenen bulk-delete N/A delta'sı** yazılı (§17)
- [ ] `status` → `approved` / `ready-for-dev` ve `runtime_code_allowed` → `true` **kullanıcı tarafından** çevrildi
- [ ] Registry satırı ve Gateway route'u için follow-up task'lar açıldı (F-REGISTRY, F-GATEWAY)

---

## 19. Implementation Notes

### 19.1 Sıralama önerisi

1. **Bu FU (MOD-0165-FU06)** — dönem master'ı + seam.
2. **F-GATEWAY** (integration-agent) — route çifti; bu olmadan UI 404 alır.
3. **MOD-0155-FU05 (MicroTarget)** — seam'in ilk gerçek tüketicisi; `apply` mantığı **orada** yaşar.
4. **F-VFP-FK / F-CAMPAIGN-BIND** — geriye dönük referans doğrulamaları, ancak MicroTarget gerçek bir
   ihtiyaç ortaya koyduktan **sonra**.

### 19.2 Bilinen tuzaklar (bu servis üzerinde daha önce yaşandı — tekrarlanmamalı)

| Tuzak | Önlem |
|---|---|
| Yeni aggregate `RegisterClassMaps`'e eklenmezse `Guid` filtreleri **sessizce boş** döner | Class-map kaydı ilk commit'te; ilk integration testi bir kayıt yazıp **id ile** okur |
| `DateTimeOffset` BSON'da `[ticks, offset]` dizisidir → iki DTO alanını birlikte index/sort etmek **500** (parallel arrays) | `StartDate`/`EndDate` birlikte index/sort **edilmez**; sıralama `Year`/`SequenceInYear` (int) üzerinden |
| `DateTimeOffset` "instant vs date" karşılaştırması yanlış reddeder | Tüm tarih karşılaştırmaları **normalize edilmiş gün başı** değerler üzerinde |
| Mongo partial index filtresinde `$ne` → servis **crash-loop** | Benzersizlik **handler'da**; partial index **kurulmaz** |
| Frontend proxy `ForwardAsync` upstream 204'ü **500'e** çevirir | `activate`/`close` proxy'lerinde bodyless status guard (204/205/304/1xx) |
| `index.l10n.js` camelCase→PascalCase dönüşümünü atlarsa `window.L10n` **undefined** | Loader deseni Segments/Campaigns'ten birebir kopyalanır |
| İkinci DataTable'ın `drawCallback`'i global selector kullanırsa ilk tablonun rozetlerini siler | Container-scoped selector zorunlu |
| `.resx` değişiklikleri fleet yeniden başlatılmadan görünmez | Smoke öncesi fleet restart |
| Yeni endpoint'ler Gateway route'u eklenene kadar **404 + `{}`** döner | Bu, kod hatası değil **route eksikliği** imzasıdır (§15) |

### 19.3 Master-plan bağlantısı

- MOD-0165 parent'ın Blueprint SoR'u (`campaigns, campaign versions, campaign results` + registry
  *"Owns Campaign/CyclePeriod execution"*) bu FU ile **ikinci** parçasını kazanır.
- `MOD-0167-FU04` §1.1/D-APPLY *"CyclePeriod — MOD-0165, **henüz yapılmadı**; bu FU onu **açmaz**"*
  satırının karşılığı **budur**; ancak `apply` hâlâ **açılmaz** (o MOD-0155'tir).
- `crm-build-lanes.md` **crm-campaign-core** lane'i (`Campaign, CyclePeriod, execution, results`) bu FU ile
  ikinci kalemini kapatır; `execution` ve `results` **hâlâ açık**.

---

## 20. Follow-up Items

| # | İş | Domain | Neden |
|---|---|---|---|
| **F-REGISTRY** | `module-id-registry.md`'ye MOD-0165-FU06 satırı (registry yazımı pack yetkisi dışı) | portfolio-delivery | DCP-002 izlenebilirliği |
| **F-GATEWAY** | `ocelot.json`'a `/api/crm/cycle-periods` + `{everything}` route çifti (OPTIONS dâhil) | integration-agent | §15 — bu olmadan UI 404 alır |
| **F-MICROTARGET** | **MOD-0155-FU05 MicroTarget** — seam'in tüketicisi; `apply`/`generate` mantığı orada | commercial-suite | Bu FU'nun **varlık nedeni** |
| **F-CYCLE-CALENDAR** | `VisitFrequencyPolicy.CycleId`'nin master'ı (`CycleCalendar` / `CyclePlan` hiyerarşisi) — **gerçekten gerekli mi?** önce ihtiyaç kanıtı | commercial-suite | D-HIER: bugün çözümlenmemiş referans, açıkça ilan edildi |
| **F-VFP-FK** | `VisitFrequencyPolicy.CyclePeriodId`'nin var-olan bir döneme işaret ettiğinin doğrulanması (VFP tarafında değişiklik) | commercial-suite | Bu FU VFP'ye **dokunmaz** |
| **F-CAMPAIGN-BIND** | `Campaign` ↔ `CyclePeriod` bağı gerekli mi? (kampanyanın kendi penceresi zaten var) | commercial-suite | Campaign bu FU'da **değişmez** |
| **F-CALENDAR-DAYS** | "Bu dönemde kaç çalışma günü var?" — CAND-CAP-0008 `IWorkingCalendarProvider` × CyclePeriod **MOD-0155'te** | commercial-suite / PSS | İki kavram bu FU'da **birleştirilmez** |
| **F-RESCHEDULE** | Aktif dönemin tarihlerinin yönetişimli değiştirilmesi (kimin yetkisi, tüketicilere etkisi) | commercial-suite | D-VER: v1'de immutable |
| **F-RBAC** | `crm.cycle-period.*` katalog kaydı + rol ataması; `activate`/`close` SoD ayrımı | platform-shared-services | Fallback dev-only |
| **F-VOCAB-0048** | `cycle-period-status` setinin MOD-0048'e taşınıp taşınmayacağı | commercial-suite | D-VOCAB in-domain |
| **F-FILE-DRIFT** | Mevcut CRM feature'larının gruplanmış dosya düzeninin Golden Reference'a hizalanması | commercial-suite | D-FILES sapma kaydı |
| **F-NAV** | Nav'ın katalog-güdümlü hâle gelmesi (MOD-0285) — bugün elle `<li>` | commercial-suite | Domain config kısıtı |

---

## Ek D — Karar Gerekçeleri (tam)

### D-FU — **MOD-0165-FU06**
`MOD-0165` altında FU01 (frequency boundary), FU02 (campaign boundary) ve FU05 (campaign admin UI) **pack**
olarak; FU03 (`Features/VisitFrequencyPolicy/`) ve FU04 (`Features/Campaign/`) **runtime kodu** olarak
kullanımdadır. Pack dosyası olmayan bir FU numarasını yeniden kullanmak, shipped kodun kimliğini çalar.
İlk çakışmayan id **FU06**'dır; DCP-002 gate exit 0.

### D-GOLDEN — **Slim**
§11.1'de sayılan kullanıcı alanı **8** ≤ 8. `CycleStatus` bilerek form dışıdır: statüyü forma koymak,
kullanıcıya *"buradan aktive edebilirim"* yanılgısı verir ve çakışma kontrolünü (yalnız `activate` yolunda
çalışan) atlatılabilir gösterir.

### D-HIER — **Düz model**
İki katmanlı (`CycleCalendar → CyclePeriod`) bir model bugün **hiçbir** somut soruyu cevaplamıyor:
`Year` + `SequenceInYear` gruplamayı zaten sağlıyor, ve tek tüketici (MicroTarget) *"şu an hangi dönem?"*
soruyor, *"hangi takvim ailesinden?"* değil. `VisitFrequencyPolicy.CycleId` alanı bu kararı **dayatmaz**;
o alan bugün de sarkıyordu, bu FU'dan sonra da sarkacak — fark şu ki artık **açıkça ilan edilmiş** bir
boşluk (`supportsCycleCalendarHierarchy: false`), sessiz bir varsayım değil.

### D-OVERLAP — **Aktifler çakışamaz, draft'lar çakışabilir**
Eğer iki `active` dönem aynı günü kapsayabilseydi, *"şu an hangi dönem?"* sorusunun **iki** cevabı olurdu ve
MicroTarget hangi döneme satır üreteceğini bilemezdi — determinizm kaybı. Öte yandan çakışmayı **her** statüde
yasaklamak planlamayı öldürürdü: planlamacı alternatif senaryolar çizemez, gelecek yılı taslak olarak
kuramazdı. Ayrım bu yüzden **statüye** göredir: `draft` = planlama alanı, `active` = tek gerçek.

### D-ACTIVE — **"Tek aktif satır" kuralı YOK**
Yaygın ama yanlış kısayol *"aynı anda yalnız bir dönem `active` olabilir"*dir; bu, tüm yılın önceden
aktifleştirilmesini imkânsız kılar ve her dönem başında elle bir "aktifleştirme" ritüeli dayatır.
Gerçek ihtiyaç *"her **an** için tek dönem"*tir — ve bu, D-OVERLAP'ten **zaten** türer. Kural sayısı azalır,
garanti aynı kalır.

### D-STATUS — **draft → active → closed, geri dönüş yok**
`closed` terminaldir çünkü kapanmış bir döneme MicroTarget satırları, ziyaret kayıtları ve raporlar bağlanmıştır;
yeniden açmak *"geçmişte ne planlanmıştı?"* sorusunu geriye dönük olarak bozar. `draft → closed` yolu, hiç
çalışmamış bir planın iptali içindir (silmek yerine **iz bırakarak** kapatmak). **Auto-close job yoktur**:
bir arka plan işi statü değiştirseydi, o iş bir *engine* olurdu ve bu FU'nun sınırını aşardı; ayrıca
`resolve-active` zaten tarih penceresine bakar, dolayısıyla statüyü zamanla senkronlamaya **ihtiyaç yoktur**.

### D-DATING — **İkinci bir tarih çifti açılmaz**
`StrategyTemplate`/`Segment` gibi *authored* nesnelerde `EffectiveFrom`/`EffectiveTo`, kaydın **içeriğinin**
ne zaman geçerli olduğunu söyler — kaydın kendisi tarihsizdir. `CyclePeriod` ise **kendisi bir tarih
aralığıdır**. İkinci bir çift eklemek, *"dönem 1 Mart'ta başlıyor ama 15 Mart'ta mı geçerli oluyor?"* gibi
cevabı olmayan bir soru yaratır ve `resolve-active`'in hangi çifte bakacağını belirsizleştirir.
FU-A'dan devralınan şey **disiplindir** (pencere kapsamıyorsa kayıt seçilemez), alan çifti değil.

### D-VER — **Sürüm klonu yok**
`StrategyTemplate`'te `new-version` doğrudur: bir *play* değişir, eski play'in ne olduğu açıklanabilir kalmalıdır.
`CyclePeriod` bir play değil, bir **takvim olgusudur**. Klonlamak, MicroTarget'ın `CyclePeriodId` ile bağlandığı
geçmişi çatallar ("hangi sürümdeki 3. dönem?"). Bunun yerine: `draft`'ta serbest düzeltme, `active`'te tarih
immutable, hata varsa `close` + yeni dönem. Gerçek bir yeniden planlama ihtiyacı çıkarsa F-RESCHEDULE
**yönetişimli** bir çözüm tasarlar — sessiz bir `PUT` değil.

### D-SCOPE — **Tenant-scoped, reference data değil**
Dönem takvimi tenant'ın ticari planlama kararıdır; iki tenant'ın dönemleri aynı olmak zorunda değildir.
MOD-0048 reference data ise **kod–etiket** çiftleri içindir; `StartDate`/`EndDate`/lifecycle/çakışma kuralı
olan bir nesne reference set'e sığmaz (ve orada çakışma doğrulaması yapılamaz).

### D-BU — **Opsiyonel business unit, specificity ile çözümleme**
Pharma tenant'larında farklı iş birimleri (ör. Rx vs OTC) **farklı dönem takvimleri** işletir; bunu
desteklememek, tenant'ı sahte dönemler yaratmaya iter. `null` = tenant geneli olduğu için tek takvimli
tenant'lar hiçbir karmaşıklık ödemez. `resolve` **specificity** kullanır (özel → genel) ve **birleştirmez**:
VFP'nin `BusinessUnit` alanındaki emsalle aynı davranış, dolayısıyla tüketici iki farklı kural öğrenmez.

### D-VOCAB — **A (in-domain fail-closed)**
FU02/FU04/FU05, MOD-0164-FU02 ve MOD-0167-FU04'ün hepsi in-domain vokabüler kullanıyor; buradan sapmak,
üç statülük bir liste uğruna runtime'ı MOD-0048 publish operasyonuna bağımlı kılardı. Taşıma kararı
F-VOCAB-0048'de, ihtiyaç kanıtıyla verilir.

### D-SEAM — **`ICyclePeriodReader`, resolved/none/ambiguous**
`null` dönmek *"dönem yok"* ile *"veri bozuk"*u aynı kefeye koyar; tüketici ikisini ayırt edemezse bozuk
veriyi sessizce "dönem yok" diye işler. Üç sonuçlu tip, MOD-0165-FU03 resolve engine'inin *"unknown ≠ default,
tie = conflict"* kararının birebir aynısıdır — tüketici tek bir zihinsel model öğrenir.

### D-FILES — **Golden Reference kanonik düzeni**
`module-pack-standard.md` Golden Reference'ı **tek gerçek standart** ilan ediyor ve ajan anti-pattern listesi
`CommandHandlers/`+`QueryHandlers/` ayrımı olmayan pack'i reddediyor. Mevcut CRM feature'larının gruplanmış
düzeni bir **teknik borçtur**; yeni bir feature'ı o borca uydurmak borcu kalıcılaştırır. Mevcut dosyalar
**bu pack'te düzeltilmez** (protected), sapma F-FILE-DRIFT olarak kaydedilir.

### D-RBAC — **Tanım var, seed yok**
Pack'ler RBAC seed/grant yetkisi taşımaz (domain config + kardeş pack emsali). Fallback **belgelenir**
ki kimse onu bir güvenlik kararı sanmasın: guard'ları genişletmez, yalnız dev ortamda `activate`'i `manage`'e
çöker ve bu **bilinen** boşluk F-RBAC ile kapanır.

---

## Handoff

Module pack `draft` olarak hazır. Lütfen inceleyip §1.2'deki **D-kararlarını** (özellikle D-OVERLAP, D-STATUS,
D-DATING, D-VER, D-BU) ve §2.4'teki **legacy `Applicable` düzeltmesini** onaylayın; gerekli alan/scope
düzeltmelerini yapın.

Geliştirme için `status` **`approved`** veya **`ready-for-dev`** olmalı ve `runtime_code_allowed` **`true`**
çevrilmelidir; sonra `@orchestrator MOD-0165-FU06-cycle-period` çağrılır.

Hazırlık sırasında Golden Reference **Slim** şablon olarak alındı — sapma yok.
