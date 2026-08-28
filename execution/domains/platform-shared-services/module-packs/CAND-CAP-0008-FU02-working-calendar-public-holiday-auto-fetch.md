---
id: CAND-CAP-0008-FU02
name: Working Calendar Public Holiday Auto-Fetch
parent: CAND-CAP-0008
runtime_slug: working-calendar-import
domain: platform-shared-services
service: Diten.Platform + frontend/Diten.Web
shell: platform-admin          # TEK yüzey — auto-fetch YALNIZ ülke katmanına yazar (§2.3/1)
golden_reference: slim         # türetme §11.1 (5 kullanıcı-form alanı) — v1'in Compact'ından FARKLI, gerekçe §11.1
entity_base: GlobalEntity      # gerekçe §4.1 — batch'in tenant ekseni YOKTUR (v1 HybridEntity'sinden bilinçli farklı)
status: ready-for-dev
runtime_code_allowed: true
runtime_code_scope: "AÇIK (ready-for-dev, flip 2026-08-27 kullanıcı kararı). Yetkilendirilen kapsam yalnız aşağıdaki maddeler + sondaki YASAK listesidir. `status: approved|ready-for-dev` + `runtime_code_allowed: true` flip'i AYRI bir kullanıcı kararıdır. Flip sonrası yetkilendirilecek kapsam (bugün YETKİLİ DEĞİL): `WorkingCalendarImportBatch` staging aggregate'i + gömülü aday listesi + CQRS + persistence + 10 endpoint (1 controller) + `IHolidayProvider` soyutlaması + Nager.Date adaptörü + offline stub adaptörü + **MOD-0026 Hangfire recurring job'u ile YILLIK ZAMANLANMIŞ FETCH (yalnız fetch→staging; auto-review/auto-apply ASLA)** + FU02 contract genişletmesi (`Diten.Platform`) VE Platform Admin → Working Calendar Imports Slim konsolu (`frontend/Diten.Web`). YASAK (flip sonrası da): FU01 aggregate semantiğini değiştirmek, tenant override katmanına fetch açmak, **zamanlanmış işin review/apply yapması**, RBAC seed/grant, Gateway config yazımı, registry write, MOD-0048 set publish, Mongo hand-edit."
owner: module-pack-author
branch: feature/pss/cand-cap-0008-fu02-working-calendar-auto-fetch
started: 2026-08-27
revised: 2026-08-27            # D-F02 = MANUEL + ZAMANLANMIŞ · D-F10 kesinleşti · 12 D-kararının tamamı KAPALI
target: TBD (ready-for-dev flip sonrası)
form_field_count: 5            # fetch tetikleme formu — §11.1 türetmesi
dependencies:
  - CAND-CAP-0008 (FU01 — PARENT; aggregate, write path, vokabüler, provider seam. DEĞİŞTİRİLMEZ, yalnız TÜKETİLİR + additive genişletilir)
  - MOD-0048 (read-only — `countries` lookup; `IPlatformLookupProvider`, HTTP self-call YOK)
  - MOD-0018 (RBAC — gerçek `[HasPermission]`; katalog/grant bu pack'te YOK → F02-RBAC)
  - MOD-0021 (audit — fetch/decision/apply olayları → F02-AUDIT)
  - MOD-0026 (job scheduler — **TÜKETİLİR, yalnız FETCH ZAMANLAMASI için**: `IRecurringJobRegistrar` + `IBackgroundJobHandler<T>` + `JobExecutionLog`. İş **yalnız** fetch→staging yapar; review/apply'a **hiç dokunmaz** — D-F02, §4.9)
  - MOD-0012 (secrets — **tüketilmez**; Nager.Date API-key'siz olduğu için secret ihtiyacı YOK, §4.8)
  - MOD-0032 (gateway — **yeni route GEREKMEZ**, §15)
  - DEV-0000 (Golden Reference Slim — tek yüzey, tek klasör)
consumers:
  - (yeni tüketici YOK) — FU02 `IWorkingCalendarProvider` okuma yüzeyini DEĞİŞTİRMEZ; tüketiciler FU02'yi görmez
---

# CAND-CAP-0008-FU02 — Working Calendar Public Holiday Auto-Fetch

> **✅ READY-FOR-DEV — KOD YETKİSİ AÇIK (flip 2026-08-27 kullanıcı kararı).** `status: ready-for-dev` ve
> `runtime_code_allowed: true`. `@orchestrator` bu pack ile kod yazabilir; kapsam yalnızca yukarıdaki
> `runtime_code_scope` ile sınırlıdır ve oradaki YASAK maddeleri (FU01 semantik değişimi, tenant override'a fetch,
> zamanlanmış işin review/apply yapması, RBAC seed/grant, Gateway/registry write, MOD-0048 publish) flip sonrası da bağlayıcıdır.
>
> **📌 REVİZYON (kullanıcı kararları, 2026-08-27) — 12 D-kararının TAMAMI KAPANDI.**
> İki karar bu revizyonun konusudur:
> - **D-F02 = MANUEL **+** ZAMANLANMIŞ.** Yıllık otomatik fetch (MOD-0026 Hangfire) **v1 kapsamındadır**;
>   artık ertelenmiyor. Zamanlanmış iş **YALNIZCA fetch→staging** yapar — **auto-review YOK, auto-apply YOK**.
>   Canlı takvime yazan tek yol **insan checker + `apply` anahtarı** olarak **aynen** kalır (§12.5, AC-SOD).
> - **D-F10 = KESİNLEŞTİ.** `active` hedefe apply, `.apply` **VE** `platform.working-calendar.activate`
>   ister (author önerisi onaylandı; davranış değişmedi).
>
> Ayrıca **D-F04** (yalnız `type=Public`), **D-F07** (additive gün provenance), **D-F08** (tek-doküman atomik
> `ReplaceAsync`), **D-F11** (parent DCP-002 kapısı, FU'ya registry satırı **açılmaz**) **ONAYLANDI**.
> Gerekçeler §19.1'de **korunmuştur**.
>
> **Bu pack FU01'i (v1) DEĞİŞTİRMEZ.** v1'in boundary ve provider-seam kararları — D1 (tek zaman ekseni),
> D2 (`HybridEntity` tek aggregate), D3 (gömülü gün + `WeekendDays` devralma), D4 (gerçek FK), D5 (`Resolution`
> + `ReasonCodes`, çözülemezse `null`), D6 (motor yok), D7 (in-domain vokabüler) — **aynen geçerlidir ve bu
> pack hiçbirini gevşetmez**. FU02'nin FU01 aggregate'ine tek dokunuşu **additive alan eklemesidir** (§4.4,
> D-F07) ve `IWorkingCalendarProvider`'ın **beş metodunun imzası, davranışı ve çözümleme sırası aynen kalır**
> (§2.4 / AC-BOUNDARY-1).

---

## 🔒 Kimlik — `CAND-CAP-0008` ve `CAND-CAP-0008-FU02` runtime literal'e ASLA yazılmaz

**DCP-002 candidate kapısı — GERÇEK KOŞUM (2026-08-27, çıktı birebir):**

```text
$ py .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0008 \
      --name "Working Calendar & Public Holidays"
OK  candidate CAND-CAP-0008: temporary governance identity, pending EA, not Blueprint-backed, not in runtime.
EXIT=0

$ py .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0008-FU02 \
      --name "Working Calendar Public Holiday Auto-Fetch"
BLOCKED  candidate CAND-CAP-0008-FU02
   - CAND-CAP-0008-FU02 has no registry row
   - CAND-CAP-0008-FU02 not recorded in the reconciliation ledger
Candidate gate failed closed. See DCP-002.
EXIT=2
```

**Bu sonuç gizlenmiyor ve rasyonalize edilmiyor.** `verify_module_id.py:196` FU sonekini **formatça kabul
eder** (`^CAND-CAP-\d{4}(-FU\d+)?$`) ama aynı fonksiyon her candidate id için **kendi registry satırını** ve
**reconciliation ledger kaydını** arar. `CAND-CAP-0008-FU02` için ikisi de yoktur — çünkü registry
(`module-id-registry.md:213`) parent capability'yi **tek satır** olarak taşır ve v1 pack'i registry yazmayı
**açıkça kapsam dışı** bırakmıştır (v1 §6 + §20/F-REG).

**İki yol vardır ve seçim kullanıcınındır → D-F11 (§19.1).** Bu pack **varsayılan olarak** parent kapısına
dayanır: FU02 yeni bir kimlik **mint etmez**, kayıtlı `CAND-CAP-0008` candidate'inin **FU çocuğudur** ve
kapı parent id ile (exit 0) koşulur.

**Runtime nötr adları (baştan sabit — governance kimliği runtime'a asla sızmaz):**

```text
Governance identity (yalnız doküman/registry) : CAND-CAP-0008-FU02
Runtime slug (FU01, değişmez)                 : working-calendar
Runtime slug (FU02, yeni)                     : working-calendar-import
Koleksiyon (yeni)                             : working_calendar_import_batches
Route (yeni, mevcut Gateway çiftinin ALTINDA) : /api/platform/working-calendars/imports
Permission (yeni, 4 anahtar)                  : platform.working-calendar.auto-fetch.{read|run|review|apply}
Aggregate / sınıf (yeni)                      : WorkingCalendarImportBatch · WorkingCalendarImportCandidate
Sağlayıcı seam (yeni)                         : IHolidayProvider · NagerHolidayProvider · OfflineHolidayProvider
View klasörü (yeni)                           : Views/Platform/WorkingCalendarImports/
```

`CAND-CAP-0008` **veya** `CAND-CAP-0008-FU02` dizesi `services/`, `frontend/`, `gateway/`, `tests/` altına
**tek bir yorum satırında bile** girerse parent kapısı kalıcı olarak kırmızıya döner (`runtime_hits` →
`check_candidate` fail). AC-ID-1 / AC-ID-2 bunu makine ile zorlar.

Otorite sırası: **FU01 pack'i (v1) > bu pack > [Domain Config](../domain-config.md) > `AGENTS.md` >
`.antigravity/rules/`.** Bu pack ile v1 arasında bir çelişki bulunursa **v1 kazanır** ve çelişki bir
follow-up olarak kaydedilir — sessizce v1 yeniden yorumlanmaz.

---

## 1. Module Summary

FU01 şu soruyu cevaplıyor: **"Bu tarih bir çalışma günü müdür?"** — ama takvimin **içini insan dolduruyor**.
Türkiye'nin 2027 resmî tatillerini bir operatörün elle, tek tek, doğru tarihlerle girmesi gerekiyor. FU02
bu **veri girişi yükünü** kaldırır ve **tek bir şey** ekler: **dış bir sağlayıcıdan çekilen resmî tatil
listesinin, insan onayından geçerek** FU01 takvimine düşmesi.

**FU02'nin tek cümlelik sözleşmesi:** *dış sağlayıcıya asla körlemesine güvenilmez; çekilen hiçbir gün insan
onayı olmadan canlı takvime yazılmaz.*

**İKİ TETİK, TEK AKIŞ (D-F02).** Fetch iki yoldan başlayabilir — **manuel** (operatör, `auto-fetch.run`) veya
**zamanlanmış** (yıllık Hangfire recurring job). İkisi de **aynı** komutu çalıştırır, **aynı** staging'e düşer
ve **aynı** insan onayı kapısına çarpar. Zamanlanmış iş **hiçbir SoD kapısını atlamaz**: yaptığı tek şey
**hazırlanmış bir teklif kuyruğu** üretmektir.

Dört durak, ve üçüncüsü **her zaman** insandır:

```text
0. TETİK             → manuel (operatör) VEYA zamanlanmış (yıllık job). Fark yalnız TriggerSource'tadır.
1. FETCH   (maker)   → dış sağlayıcı çağrısı; sonuç STAGING'e düşer. Canlı takvim ETKİLENMEZ.
2. STAGING           → her gün bir ADAY satırdır; hedef takvimle DIFF'lenir (yeni / zaten var / tarih kaymış /
                       manuel günle çakışıyor / bayraklı). Karar verilmemiş aday = uygulanamaz.
3. REVIEW  (insan)   → GÜN DÜZEYİNDE approve/reject. Reddedilen satır iz bırakır, silinmez.
4. APPLY   (checker) → YALNIZ onaylı günler, TEK bir atomik yazımla hedef takvimin gömülü gün listesine
                       merge olur. Maker ≠ checker (SoD).
```

**FU02'nin ürünü bir UI değil, bir GÜVENLİK SIRASIDIR.** Sağlayıcı çökerse batch `failed` olur ve canlı
takvimde **hiçbir şey değişmez** (§8.2). Aynı ülke/yıl tekrar çekilirse aday satırlar mevcut günlerle
**tarihe göre** eşleşir ve **DUP üretilmez** (§12.4). Sağlayıcı değişirse **pipeline değişmez** — çünkü
pipeline sağlayıcıyı `IHolidayProvider` arkasından görür (§4.5).

**Hedef kullanıcı:** platform admin (maker: fetch tetikler; checker: onaylar ve uygular) **ve zamanlanmış iş**
(yalnız maker rolünde, sistem kimliğiyle — asla checker olamaz, §14.2). **Tenant admin bu yüzeyi görmez** —
gerekçe §2.3/1.

**MOTOR YOK (FU01/D6 aynen geçerli).** FU02 bir tatil **üretmez**, `Recurrence`'tan gün **türetmez**, gelecek
yılı **hesaplamaz**. Yalnız **taşır**: sağlayıcının söylediğini staging'e, insanın onayladığını takvime.

---

## 2. Ownership and Boundaries

### 2.1 Kapsam

| Kapsam | Karar |
|---|---|
| **In-scope (FU02)** | `WorkingCalendarImportBatch` staging aggregate'i (gömülü `WorkingCalendarImportCandidate` listesi) + repository + CQRS + persistence + **10 API endpoint** (1 controller) + **`IHolidayProvider` soyutlaması** + **Nager.Date adaptörü** + **offline stub adaptörü** + host-allowlist'li egress konfigürasyonu + **YILLIK ZAMANLANMIŞ FETCH** (MOD-0026 recurring job: `HolidayAutoFetchJob` + args + registrar kaydı, **yalnız fetch→staging**) + FU02 contract genişletmesi (`Diten.Platform`) + **Platform Admin → Working Calendar Imports** Slim konsolu (`frontend/Diten.Web`) + FU01 gününe **additive provenance alanları** (D-F07) |
| **Out-of-scope (AÇIKÇA ERTELENİR)** | **Zamanlanmış işin review/apply yapması** (kalıcı yasak, §2.4) · zamanlanmış iş için ayrı bir "şimdi çalıştır" endpoint'i (§15) · uygulanmış hedef için **otomatik yenileme** (`applied ⇒ skip`, §12.8/V41 → **F02-SCHED-REFRESH**) · tenant override katmanına fetch · çok-ülke/çok-yıl tek batch → **D-F05** · sağlayıcı-tarafı bölge (subdivision) takvimleri → **F02-SUBDIV** · `ObservedDate` otomasyonu → **D-F06** · takvim **oluşturma** (FU01'in işi, D-F09) · bulk-archive · provider cache · MOD-0048 set publish · RBAC seed/grant · Gateway config yazımı |

### 2.2 Ne sahiplenir, ne yalnız tüketir

| Nesne | Sahip | Bu FU'da |
|---|---|---|
| `WorkingCalendarImportBatch` + gömülü aday | **FU02** | **AÇILIR** — yeni aggregate, yeni koleksiyon (v1 §3'ün *"auto-fetch staging aggregate'i FU02"* rezervi) |
| `IHolidayProvider` + adaptörler | **FU02** | **AÇILIR** — dış dünya sınırı tek bir arayüzün arkasında |
| Aday → gün eşlemesi (mapping) | **FU02** | **AÇILIR** — deterministik, tek yerde (`Mapping/`) |
| `WorkingCalendar` aggregate'i + gömülü gün | **FU01** | **TÜKETİLİR** — yazma **FU01 write path'i** üzerinden (D-F08); şema **additive** genişler (D-F07) |
| `WorkingCalendarValidation` gün guard'ları | **FU01** | **TÜKETİLİR** — ikinci bir kopya **YASAK** |
| `IWorkingCalendarProvider` (5 metot) | **FU01** | **DOKUNULMAZ** — imza, davranış, çözümleme sırası **birebir aynı** |
| Ülke listesi / ISO kodu | **MOD-0048** | **read-only** — `IPlatformLookupProvider` `"countries"` |
| Job scheduler (Hangfire) | **MOD-0026** | **TÜKETİLİR — yalnız fetch zamanlaması** (D-F02). Seam'ler (`IRecurringJobRegistrar`, `IBackgroundJobHandler<T>`, `JobExecutionLog`) **değiştirilmez**; FU02 yalnız **bir iş kaydı** ekler (§4.9) |
| Zamanlanmış fetch işi (`HolidayAutoFetchJob`) | **FU02** | **AÇILIR** — iş **yalnız** `StartWorkingCalendarImportCommand`'ı çağırır; review/apply komutlarına **hiç erişmez** |
| Secret / API key yönetimi | **MOD-0012** | **TÜKETİLMEZ** — Nager.Date API-key'sizdir (§4.8) |

### 2.3 Sınır netleştirmeleri (karıştırılması kolay dört şey)

1. **Auto-fetch YALNIZ ülke katmanına yazar — bu bir tercih değil, FU01'den TÜREYEN bir zorunluluktur.**
   Sağlayıcı **resmî tatil** verir; resmî tatil `public-holiday` tipindedir; FU01
   `WorkingCalendarDayType.CountryLayerOnly` kümesini (`public-holiday` · `religious-holiday` ·
   `moveable-holiday`) **ülke katmanına rezerve etmiştir** ve override yüzeyinden bu tiple yazmayı **400
   `day_type_reserved_for_country_layer`** ile reddeder (v1 AC-SEC-8; canlı kodda
   `WorkingCalendarDayType.OverrideAuthorable`). Dolayısıyla FU02'nin hedefi **her zaman** `ScopeType=country`
   + `TenantId=null` bir takvimdir. **Tenant yüzeyi, tenant RESX'i, tenant controller'ı AÇILMAZ.**
2. **Staging ≠ takvim.** Staging kaydı bir **öneridir**. `IWorkingCalendarProvider` staging'i **görmez**;
   hiçbir tüketici bir aday günü "tatil" olarak okuyamaz. Tanım gereği izoledir.
3. **Fetch ≠ apply.** Sağlayıcıdan veri gelmesi bir **olay** değil, bir **teklif**tir. Batch `pending-review`
   durumundayken canlı sistemde değişen **hiçbir bayt yoktur**. **Bu, zamanlanmış fetch için de aynen
   geçerlidir**: yıllık iş yılda bir kez **teklif kuyruğu** üretir, karar üretmez.
3a. **Zamanlanmış ≠ otomatik.** "Otomatik tatil çekme" ifadesi **veri getirmeyi** anlatır, **karar vermeyi**
   değil. Bir tatilin canlı takvime girmesi **her zaman** bir insanın `apply` tıklamasıdır — takvim yılda bir
   kez kendi kendine dolmaz.
4. **Auto-fetch ≠ motor.** FU02 tatil **üretmez**; sağlayıcının söylediğini taşır. `Recurrence` alanı FU02
   tarafından da **yalnız bir beyandır** — FU01/D6 aynen geçerlidir.

### 2.4 Kalıcı yasaklar

```text
Canlı takvime doğrudan yazan fetch yolu             ❌  staging atlanamaz (§8.2)
Zamanlanmış işin aday ONAYLAMASI (auto-review)      ❌  D-F02 guard 1 — kalıcı
Zamanlanmış işin APPLY etmesi (auto-apply)          ❌  D-F02 guard 1 — kalıcı
Sistem kimliğinin AppliedBy olması                  ❌  checker HER ZAMAN insandır (§14.2)
Kısmi merge (N günün K'sı yazıldı)                  ❌  tek atomik replace (D-F08)
Karar verilmemiş adayın uygulanması                 ❌  implicit approve YOK (§12.3)
Maker'ın kendi batch'ini apply etmesi               ❌  SoD (§14)
Tenant override katmanına fetch                     ❌  §2.3/1
IWorkingCalendarProvider imzası/davranışı değişimi  ❌  FU01 seam DONDURULMUŞ
date.nager.at dışında bir host'a egress             ❌  allowlist fail-closed (§4.8)
Sağlayıcı API key / secret alanı (config veya kod)  ❌  Nager API-key'sizdir; alan açmak secret davetidir
"CAND-CAP-0008" / "CAND-CAP-0008-FU02" (runtime)    ❌  MAKİNE İLE ZORLANIR
```

---

## 3. Owned Objects

| Katman | Nesne |
|---|---|
| **Entity** | `WorkingCalendarImportBatch : GlobalEntity` (aggregate root) + gömülü `WorkingCalendarImportCandidate` |
| **Repository** | `IWorkingCalendarImportBatchRepository` → `GlobalRepository<WorkingCalendarImportBatch>` (mevcut altyapı) |
| **Commands** | `StartWorkingCalendarImportCommand` · `DecideWorkingCalendarImportCandidateCommand` · `DecideWorkingCalendarImportBatchCommand` (fan-out) · `ApplyWorkingCalendarImportCommand` · `DiscardWorkingCalendarImportCommand` |
| **Queries** | `ListWorkingCalendarImportsQuery` · `GetWorkingCalendarImportByIdQuery` · `GetWorkingCalendarImportContractQuery` · `GetHolidayProviderStatusQuery` |
| **Sağlayıcı seam** | **`IHolidayProvider`** + `HolidayFetchResult` / `ProviderHoliday` / `HolidayProviderOutcome` (§4.5) |
| **Adaptörler** | `NagerHolidayProvider` (HTTP) · `OfflineHolidayProvider` (stub, ağsız geliştirme) — **seçim config ile** (§4.8) |
| **Mapping** | `HolidayCandidateMapper` (sağlayıcı → aday) · `ImportDiffEngine` (aday ↔ hedef takvim farkı) — **TEK yer** |
| **Zamanlanmış iş (D-F02)** | `HolidayAutoFetchJob : IBackgroundJobHandler<HolidayAutoFetchJobArgs>` + `HolidayAutoFetchJobArgs` + `PlatformRecurringJobRegistrar`'a **tek additive kayıt satırı** (§4.9). İş **yalnız** `StartWorkingCalendarImportCommand` çağırır |
| **DTOs** | `WorkingCalendarImportDto` · `WorkingCalendarImportListDto` · `WorkingCalendarImportCandidateDto` · `WorkingCalendarImportContractDto` |
| **Controllers** | `WorkingCalendarImportsController` (`PlatformActor` policy) — **tek controller** (§14) |
| **API endpoints** | §15 tablosu — **9 endpoint**, hepsi `/api/platform/working-calendars/imports*` |
| **Frontend route** | `/Platform/WorkingCalendarImports` (platform shell) |
| **Permissions** | `platform.working-calendar.auto-fetch.read` · `.run` · `.review` · `.apply` (§14) |
| **Vokabüler (in-domain)** | `WorkingCalendarImportStatus` · `WorkingCalendarImportDecision` · `WorkingCalendarImportChangeKind` · `WorkingCalendarImportFlags` · `HolidayProviderOutcome` · **`WorkingCalendarImportTriggerSource`** |
| **FU01'e additive** | `WorkingCalendarDay.Source` · `.ProviderBatchId` · `.ProviderRef` (D-F07) + `WorkingCalendarSource.Writable` genişlemesi + contract `Limitations` satırı |
| **AÇIKÇA sahiplenilmeyen** | takvim oluşturma · takvim activate/archive · provider okuma seam'i · ülke listesi · scheduler · secret yönetimi |

---

## 4. Entity Fields

### 4.1 `entity_base: GlobalEntity` — gerekçe

FU01 `HybridEntity` seçti çünkü **takvim** iki katmanlıdır. **Batch iki katmanlı DEĞİLDİR**: §2.3/1 gereği
auto-fetch **yalnız** ülke katmanına yazar, dolayısıyla bir batch'in `TenantId`'si **her zaman** `null`
olurdu. Var olmayan bir eksen için nullable bir alan taşımak, `HybridRepository`'nin
`Or(TenantId==null, TenantId==current)` filtresini **yanıltıcı** biçimde devreye sokar ve v1 §8.2'nin
*"tek gerçek risk"* dediği **`TenantId=null` sızıntısını** anlamsız yere yeniden davet eder.

`GlobalEntity` seçimi `module-pack-standard.md`'nin *"`GlobalEntity` kullanan pack'te gerekçe olmalı"*
kuralını bu paragrafla karşılar: **bu kayıt kavramsal olarak cross-tenant değildir — hiç tenant boyutu
yoktur.** Erişim tenant filtresiyle değil, `PlatformActor` policy + `auto-fetch.*` permission ile çevrilir
(§14).

Miras alınan alanlar (`Diten.Platform.Common.Persistence.BaseEntity`): `Guid Id` · `DateTimeOffset CreatedAt`
· `string CreatedBy` · `DateTimeOffset? UpdatedAt` · `string? UpdatedBy` · `bool IsDeleted` · **`int Version`**.

> **Dikkat — v1 §19.2/5 tuzağı aynen geçerli.** `Diten.Platform.Domain.Common.BaseEntity` (legacy:
> `string Id`, `DateTime`) **KULLANILMAZ**; doğru aile `Diten.Platform.Common.Persistence`'tır.

### 4.2 `WorkingCalendarImportBatch` — aggregate root

| # | Alan | Tip | Zorunlu | Form? | Kural / Not |
|---|---|---|---|---|---|
| 1 | `Id` | Guid | Evet | ✗ | `BaseEntity` |
| 2 | `BatchCode` | string | Evet | ✗ (**üretilir**) | Okunabilir iş anahtarı; deterministik: `IMP-{CountryCode}-{Year}-{yyyyMMddHHmmss}`. Unique |
| 3 | `CountryCode` | string | Evet | ✓ | ISO-3166-1 alpha-2; MOD-0048 `countries`'ten **doğrulanır**; hedef takvimin ülkesiyle **aynı olmak zorunda** |
| 4 | `CalendarYear` | int | Evet | ✓ | `1900..2200`; hedef takvimin yılıyla **aynı olmak zorunda** |
| 5 | `TargetCalendarId` | Guid | Evet | ✓ | **GERÇEK FK** — `WorkingCalendar`; `ScopeType=country`, `TenantId=null`, `CalendarStatus ∈ {draft, active}` olmak **zorunda** (D-F09) |
| 6 | `TargetCalendarCodeSnapshot` | string | Evet | ✗ | Denetim için kod anlık görüntüsü; FK değil, **kanıt** |
| 7 | `IncludeNonPublicTypes` | bool | Evet | ✓ | `false` (varsayılan) ⇒ sağlayıcının `Public` dışı tipleri **hiç staging'e girmez**; `true` ⇒ **bayraklı ve varsayılan-RET** olarak girer (D-F04) |
| 8 | `ImportStatus` | string | Evet | ✗ (**akış**) | `fetching` · `pending-review` · `in-review` · `applied` · `discarded` · `failed` (§4.4, §12.3) |
| 9 | `ProviderKey` | string | Evet | ✗ (**config**) | `nager.date` · `offline-stub`. **Form alanı DEĞİLDİR** — sunucu konfigürasyonundan gelir (§4.8) |
| 10 | `ProviderEndpoint` | string | Evet | ✗ | Çağrılan **tam URL** (provenance). Secret taşımaz — sorgu dizesi yoktur |
| 11 | `ProviderFetchedAt` | DateTimeOffset? | Hayır | ✗ | Sağlayıcı cevabının alındığı an |
| 12 | `ProviderPayloadHash` | string? | Hayır | ✗ | Ham cevabın SHA-256'sı (**ham payload'ın kendisi saklanmaz** — §8.5) |
| 13 | `ProviderOutcome` | string? | Hayır | ✗ | `fetched` · `provider_unavailable` · `country_not_supported` · `invalid_response` (§4.4) |
| 14 | `Candidates` | `WorkingCalendarImportCandidate[]` | Hayır | ✗ (**review ekranı**) | Gömülü liste (§4.3). Alan değil, **review grid'i** |
| 15 | `CandidateCount` / `ApprovedCount` / `RejectedCount` / `UndecidedCount` / `SkippedNonPublicCount` / `DuplicateSourceRowCount` | int | Evet | ✗ | **Türetilmiş sayaçlar** — her karar yazımında yeniden hesaplanır; liste sorgusu `Candidates`'ı projeksiyonla dışarıda bırakır (§8.6) |
| 15a | `TriggerSource` | string | Evet | ✗ (**tetikten türer**) | `manual` · `scheduled` (§4.4). Payload'dan **asla** okunmaz; manuel uçta sabit `manual`, iş içinde sabit `scheduled` |
| 16 | `RequestedBy` / `RequestedAt` | string / DateTimeOffset | Evet | ✗ | **Maker kimliği** — SoD'nin dayanağı (§14). Manuel: JWT aktörü. Zamanlanmış: **sistem kimliği** `system:auto-fetch-scheduler` (§14.2) |
| 16a | `ScheduledRunKey` | string? | Hayır | ✗ | Yalnız `scheduled` batch'lerde: `{CountryCode}:{CalendarYear}:{TargetCalendarId}` — işin idempotency anahtarı (§12.8/V40) |
| 17 | `AppliedBy` / `AppliedAt` | string? / DateTimeOffset? | Hayır | ✗ | **Checker kimliği**; `RequestedBy` ile **aynı olamaz** (§12.5) |
| 18 | `AppliedDayIds` | Guid[]? | Hayır | ✗ | Merge sonucu **gerçekten yazılan** `DayId` listesi — geri izlenebilirlik |
| 19 | `TargetCalendarVersionAtApply` | int? | Hayır | ✗ | Merge'ün hangi `Version` üzerine yazıldığı (concurrency kanıtı) |
| 20 | `FailureReason` | string? | Hayır | ✗ | `failed` batch'in **kullanıcıya gösterilen** nedeni; uydurma/parafraz **yok** |
| 21 | `Notes` | string? | Hayır | ✓ | Max 2000 |
| 22 | `CreatedAt/By` · `UpdatedAt/By` · `IsDeleted` · `Version` | — | — | ✗ | `BaseEntity` |

### 4.3 `WorkingCalendarImportCandidate` — gömülü (D-F01)

| # | Alan | Tip | Zorunlu | Not |
|---|---|---|---|---|
| 1 | `CandidateId` | Guid | Evet | Gömülü kimlik |
| 2 | `ProviderDayKey` | string | Evet | Sağlayıcı tarafında **stabil** kimlik: `{countryCode}:{yyyy-MM-dd}:{normalized-name}`. Batch içinde unique (handler+validator; **dizi-içi DB index YOK**) |
| 3 | `Date` | DateOnly | Evet | Sağlayıcının verdiği tarih. Yılı `CalendarYear` ile **aynı olmak zorunda**, değilse aday bayraklanır (`date_outside_calendar_year`) |
| 4 | `ProviderName` | string | Evet | Sağlayıcının İngilizce adı (`name`) |
| 5 | `ProviderLocalName` | string? | Hayır | Sağlayıcının yerel adı (`localName`) — **gün adı olarak bunu tercih ederiz** (D-F04) |
| 6 | `ProviderTypes` | string[] | Evet | Ham tip listesi (`Public`, `Bank`, `Optional`, …) — **eşlenmemiş hâliyle** saklanır (kanıt) |
| 7 | `ProviderIsNationwide` | bool | Evet | Nager `global` alanı |
| 8 | `ProviderSubdivisions` | string[]? | Hayır | Nager `counties` — doluysa aday `flagged` + `subdivision_scoped` (D-F04) |
| 9 | `MappedDayType` | string? | Hayır | Eşleme sonucu (`public-holiday`) — eşlenemiyorsa **`null`** ve aday **varsayılan-RET** |
| 10 | `MappedDayCode` | string | Evet | Hedef takvimde kullanılacak kod: `PF-{yyyyMMdd}`; çakışırsa `-2`, `-3` … (§12.6) |
| 11 | `MappedDayName` | string | Evet | `ProviderLocalName ?? ProviderName`, trim, max 200 |
| 12 | `ChangeKind` | string | Evet | `new` · `already-present` · `date-shift` · `conflicts-manual` (§4.4, §12.4) |
| 13 | `ExistingDayId` | Guid? | Hayır | `already-present` / `date-shift` / `conflicts-manual` durumunda eşleşen mevcut gün |
| 14 | `Flags` | string[] | Evet (boş olabilir) | `type_not_public` · `subdivision_scoped` · `date_outside_calendar_year` · `existing_manual_day` · `day_code_collision` · `unmapped_type` (§4.4) |
| 15 | `Decision` | string | Evet | `undecided` (varsayılan) · `approved` · `rejected` (§4.4) |
| 16 | `DecisionReason` | string? | Hayır | Reddeden kişinin gerekçesi; max 500. **Ret sessizce kaybolmaz** |
| 17 | `DecidedBy` / `DecidedAt` | string? / DateTimeOffset? | Hayır | Karar izleri |
| 18 | `AppliedDayId` | Guid? | Hayır | Merge'te yazılan gerçek `DayId` (yalnız `approved` + apply sonrası) |

> **Varsayılan karar `undecided`'dır ve bu bir tasarım kararıdır.** Sistem hiçbir adayı **kendiliğinden
> onaylamaz**; `Flags` dolu adaylar UI'da **varsayılan-RET olarak işaretlenir ama yine `undecided` kalır** —
> yani "reddedildi" bilgisi bile bir insan tıklamasından gelir (§12.3, AC-SOD-4).

### 4.4 In-domain vokabüler (FU01/D7 deseni — fail-closed)

```text
WorkingCalendarImportStatus     : fetching · pending-review · in-review · applied · discarded · failed
WorkingCalendarImportDecision   : undecided · approved · rejected
WorkingCalendarImportChangeKind : new · already-present · date-shift · conflicts-manual
WorkingCalendarImportFlags      : type_not_public · subdivision_scoped · date_outside_calendar_year ·
                                  existing_manual_day · day_code_collision · unmapped_type
HolidayProviderOutcome          : fetched · provider_unavailable · country_not_supported · invalid_response
WorkingCalendarImportTriggerSource : manual · scheduled          (D-F02 — tetiği ayırır, AKIŞI ayırmaz)
```

> **`TriggerSource` neden bir statü değil de ayrı bir alan:** iki tetik **aynı** state machine'i, **aynı**
> kapıları ve **aynı** apply kurallarını kullanır. Tetiği statüye karıştırmak (`scheduled-pending` gibi bir
> değer) ikinci bir akış yolu doğururdu — D-F02'nin guard 1'i tam olarak bunu yasaklıyor.

Vokabüler `Domain/Entities/WorkingCalendar/WorkingCalendarImportBatch.cs` içinde `static class` olarak yaşar;
**set dışı değer → 400**; **hardcoded fallback listesi yasaktır** — tüm dropdown/rozetler `contract`
endpoint'inden beslenir.

**FU01 vokabülerine additive dokunuş (D-F07 onaylanırsa):**

```text
WorkingCalendarSource.Writable  : manual · imported            →  manual · imported · provider-fetch
                                  (yalnız FU02 apply yolundan; kullanıcı formundan HÂLÂ yazılamaz — §12.7)
WorkingCalendarDay.Source       : YENİ alan, varsayılan "manual"    (gün düzeyi provenance)
WorkingCalendarDay.ProviderBatchId / .ProviderRef : YENİ alanlar, varsayılan null
```

**`WorkingCalendarDayType` DEĞİŞMEZ**, `WorkingCalendarStatus` **DEĞİŞMEZ**, `WorkingCalendarReasonCodes`
**DEĞİŞMEZ**. Provider çözümleme sırası (v1 §4.5) **tek bir satır bile değişmez** — merge sonrası günler
FU01'in gözünde **sıradan `public-holiday` günleridir**.

### 4.5 `IHolidayProvider` — dış dünya sınırı (FU02'nin asıl soyutlaması)

```csharp
/// Dış tatil sağlayıcısının TEK sınırı. Pipeline bu arayüzün ARKASINI görmez: adaptör değişirse
/// (Nager → başka bir kaynak) mapping, diff, review ve merge kodu DEĞİŞMEZ.
/// ASLA exception fırlatmaz — her başarısızlık bir Outcome değeridir (FU01/D5 ilkesi).
public interface IHolidayProvider
{
    string ProviderKey { get; }                        // "nager.date" | "offline-stub"
    Task<HolidayFetchResult> FetchAsync(string countryCode, int year, CancellationToken ct);
}

public sealed record HolidayFetchResult(
    string Outcome,                                    // HolidayProviderOutcome
    IReadOnlyList<ProviderHoliday> Holidays,           // Outcome != fetched ⇒ BOŞ
    string ProviderKey,
    string ProviderEndpoint,
    DateTimeOffset FetchedAt,
    string? PayloadHash,
    string? FailureDetail);

public sealed record ProviderHoliday(
    DateOnly Date,
    string Name,
    string? LocalName,
    IReadOnlyList<string> Types,
    bool IsNationwide,
    IReadOnlyList<string> Subdivisions);
```

**Neden `bool`/exception değil de `Outcome` (FU01/D5 ile aynı ilke):** sağlayıcı erişilemediğinde "boş liste
döndü" ile "hiç tatili yok" **aynı şey değildir**. Boş liste dönen bir sağlayıcı, ayırt edilmezse, bir
ülkenin tüm tatillerini **sessizce silme** teklifine dönüşür. `Outcome` bu iki durumu **yapısal olarak**
ayırır: `provider_unavailable` ⇒ batch `failed`, aday üretilmez, canlı takvim **görülmez**.

**Sağlayıcının yapmadıkları:** yazmaz · takvim bilmez · tenant bilmez · retry politikası taşımaz (tek
deneme + timeout, §8.3) · cache tutmaz · exception fırlatmaz.

### 4.6 Nager.Date adaptör sözleşmesi ve eşleme (D-F04 / D-F06)

```text
GET https://date.nager.at/api/v3/PublicHolidays/{year}/{countryCode}
Auth: YOK (API key yok, header yok, secret yok)
Accept: application/json
```

Beklenen dizi elemanı (yalnız **kullanılan** alanlar; bilinmeyen alanlar **yok sayılır**, hata değildir):

| Nager alanı | Aday alanı | Not |
|---|---|---|
| `date` | `Date` | `DateOnly`. Yıl `CalendarYear` değilse → `flagged` + `date_outside_calendar_year` |
| `localName` | `MappedDayName` (öncelik) | Ülkenin kendi dilindeki ad — operatörün tanıyacağı ad budur |
| `name` | `ProviderName` / `MappedDayName` (fallback) | |
| `types[]` | `ProviderTypes` | **Ham saklanır.** `Public` içeriyorsa → `MappedDayType = public-holiday` |
| `global` | `ProviderIsNationwide` | `false` → `flagged` + `subdivision_scoped` |
| `counties[]` | `ProviderSubdivisions` | Doluysa aynı bayrak |
| `countryCode` | (doğrulama) | İstenen ülkeyle uyuşmuyorsa **tüm batch** `invalid_response` |
| `fixed`, `launchYear` | — | **Kullanılmaz.** `Recurrence` **her zaman `none`** yazılır (D-F04 gerekçesi) |

**Eşleme kuralları (deterministik, tek yerde — `HolidayCandidateMapper`):**

1. `types` **`Public` içeriyorsa** → `MappedDayType = public-holiday`.
2. `types` `Public` **içermiyorsa** (`Bank`/`School`/`Authorities`/`Optional`/`Observance`) →
   `IncludeNonPublicTypes=false` ise aday **hiç üretilmez**; `true` ise `MappedDayType = null` +
   `flagged: type_not_public, unmapped_type` + **varsayılan-RET**.
3. **`religious-holiday` ve `moveable-holiday` ASLA otomatik atanmaz.** Sağlayıcı bir günün dinî olup
   olmadığını **söylemez**; tahmin etmek uydurmaktır. Operatör isterse merge sonrası FU01 gün editöründen
   tipi değiştirir → **F02-RECLASS**.
4. **`Recurrence` her zaman `none`.** Nager'ın `fixed` alanı v3'te güvenilmezdir ve FU01/D6 zaten
   `Recurrence`'tan **hiçbir şey türetmez**; `annual-fixed` yazmak yanlış bir kesinlik iddiası olurdu.
5. **`ObservedDate` her zaman `null` (D-F06).** Nager **ayrı bir "observed" alanı vermez** — kaydırılan
   tatili çoğu ülkede *ayrı bir kayıt* olarak döner. `Date`'ten `ObservedDate` üretmek **veri uydurmaktır**.
   Operatör gerekirse merge sonrası FU01 editöründen doldurur.
6. `IsHalfDay` **her zaman `false`** — sağlayıcı yarım gün bilgisi vermez.

### 4.7 MongoDB index ihtiyacı

| Index | Alanlar | Not |
|---|---|---|
| Batch kodu | `BatchCode` | Unique, partial `IsDeleted=false` |
| Liste/filtre | `CountryCode` + `CalendarYear` + `ImportStatus` | Ana liste sorgusu |
| Hedef takvim izi | `TargetCalendarId` + `ImportStatus` | "bu takvime hangi batch'ler uygulandı" + V10 açık-batch kontrolü |
| **YASAK** | iki `DateTimeOffset` alanının **birlikte** index'lenmesi/sort'u | v1 §4.6 dersi — parallel-arrays 500. Liste sort'u **tek** `DateTimeOffset` (`RequestedAt`) üzerindedir |
| **YASAK** | `Candidates` dizisi içinde unique index | Mongo'da ifade edilemez (v1 §19.2/7) — `ProviderDayKey` tekilliği **handler + validator** ile korunur |
| **YASAK** | partial index filtresinde `$ne` | Servisi başlangıçta crash-loop'a sokar (v1 §19.2/8) — küme **pozitif** (`$in`) yazılır |

### 4.8 Egress, konfigürasyon ve secret sınırı

```jsonc
// appsettings.json — "WorkingCalendar:HolidayProvider"
{
  "WorkingCalendar": {
    "HolidayProvider": {
      "Enabled": false,                       // varsayılan KAPALI — açmak bilinçli bir karardır
      "Provider": "offline-stub",             // "nager.date" | "offline-stub"
      "BaseUrl": "https://date.nager.at",     // koda GÖMÜLMEZ (configuration-safety.md)
      "AllowedHosts": [ "date.nager.at" ],    // ALLOWLIST — tek üye
      "TimeoutSeconds": 10,
      "MaxCandidatesPerBatch": 400,           // FU01 MaxDaysPerCalendar ile aynı sınır
      "Schedule": {                           // D-F02 — zamanlanmış fetch
        "Enabled": false,                     // varsayılan KAPALI (ÜÇÜNCÜ kapı — §4.9)
        "CronExpression": "0 3 2 1 *",        // her yıl 2 Ocak 03:00 UTC (UTC ZORUNLU — §4.9)
        "YearOffsets": [ 0, 1 ],              // içinde bulunulan yıl + gelecek yıl
        "MaxBatchesPerRun": 50,               // koşum başına üretilecek EN FAZLA batch
        "IncludeNonPublicTypes": false        // zamanlanmış koşumun sabit tercihi
      }
    }
  }
}
```

**Bağlayıcı kurallar:**

- **Host allowlist fail-closed.** `BaseUrl`'ün host'u `AllowedHosts` içinde **değilse** uygulama
  `ValidateOnStart` ile **başlamaz** (`InvalidOperationException`) — `configuration-safety.md` fail-fast
  kuralı. Çalışma zamanında redirect başka bir host'a çıkarsa istek **iptal edilir**
  (`HttpClientHandler.AllowAutoRedirect = false`).
- **Kodda varsayılan URL YOKTUR.** `?? "https://date.nager.at"` gibi bir fallback **yasaktır**
  (`configuration-safety.md` §Hardcoded Veri Yasağı). Config eksikse `Provider` **offline-stub'a düşmez** —
  uygulama **hata verir**; sessiz düşüş, "tatiller neden boş?" sınıfı bir arızadır.
- **Secret alanı YOK.** Nager.Date API-key'sizdir. `ApiKey` / `Token` alanı açmak, ileride oraya bir secret
  yazılmasını **davet eder** ve MOD-0012 sınırını bulanıklaştırır. Sağlayıcı değişip kimlik gerektirirse bu
  **ayrı bir follow-up**tur → **F02-SECRET**.
- **Dev'de ağ kapalı → `offline-stub`.** `OfflineHolidayProvider`, repo içindeki küçük bir sabit veri
  kümesinden (TR/DE/US × 2 yıl gibi) `fetched` döner ve `ProviderEndpoint = "offline-stub://…"` yazar.
  Batch provenance'ı **hangi adaptörün** konuştuğunu her zaman gösterir — offline veri asla gerçek sağlayıcı
  verisi gibi görünmez (AC-EGRESS-4).
- **`FakeMessagingProvider` emsali.** Bu desen serviste zaten canlıdır
  (`Settings/FakeMessagingProviderOptions.cs` + `services.AddScoped<IMessagingProvider, FakeMessagingProvider>()`)
  — FU02 yeni bir desen icat etmez, mevcut olanı izler.
- **Timeout + tek deneme (istek başına).** `TimeoutSeconds` (varsayılan 10) aşılırsa `provider_unavailable`.
  Tek bir fetch içinde **HTTP retry yoktur**; başarısız batch `failed` olarak kapanır ve **yeni bir batch**
  ile denenir. Zamanlanmış koşumda bu "bir sonraki koşumda tekrar dene" anlamına gelir (§4.9/guard 5) —
  başarısız batch **silinmez**, kanıt olarak kalır.

---

## 4.9 Zamanlanmış fetch (D-F02) — MOD-0026 recurring job sözleşmesi

**Mekanizma: Hangfire *recurring job*, endpoint DEĞİL.** Zamanlamayı tetikleyen bir HTTP ucu **yoktur** ve
**açılmayacaktır** (§15). İş, MOD-0026'nın canlı desenine **birebir** oturur:

```csharp
// Features/WorkingCalendarImport/BackgroundJobs/HolidayAutoFetchJob.cs
public sealed class HolidayAutoFetchJob : IBackgroundJobHandler<HolidayAutoFetchJobArgs>
{
    public Task HandleAsync(HolidayAutoFetchJobArgs args, BackgroundJobContext context, CancellationToken ct);
}

public sealed record HolidayAutoFetchJobArgs(
    IReadOnlyList<int> YearOffsets,
    int MaxBatchesPerRun,
    bool IncludeNonPublicTypes);
```

**Kayıt — mevcut `PlatformRecurringJobRegistrar`'a TEK additive satır** (canlı emsal:
`CreateEmailDispatchSweepRegistration()` / `CreateWorkflowEscalationSweepRegistration()`):

```text
Descriptor.Id            : "Diten.Platform.WorkingCalendar.HolidayAutoFetchJob"
Descriptor.ServiceName   : "Diten.Platform"
Descriptor.JobName       : "HolidayAutoFetchJob"
Descriptor.Owner         : "working-calendar"          ← NÖTR SLUG, MOD/CAND literali DEĞİL (§19.2/15)
Descriptor.CronExpression: Schedule.CronExpression      (config'ten; >= 5 alan, RecurringJobRegistration.Validate)
Descriptor.TimeZoneId    : "UTC"                        ← ZORUNLU; başka değer BackgroundJobValidationException
Descriptor.Queue         : "platform"
Descriptor.MaxRetryAttempts : BackgroundJobSchedulerOptions.DefaultRetryAttempts
Descriptor.IsEnabled     : RegisterStandardJobs && EnabledJobs["Diten.Platform.WorkingCalendar.HolidayAutoFetchJob"]
Context.TriggerType      : Recurring
Context.TriggeredBy      : nameof(PlatformRecurringJobRegistrar)
Context.TenantId         : null                         ← ülke katmanı; tenant scope AÇILMAZ (§4.9/guard 3)
```

> **🔴 KİMLİK TUZAĞI — bu satır DCP-002 kapısını kırabilir.** Mevcut kayıtların **hepsi** `Owner`/`Id` alanına
> **MOD literali** yazıyor: `Diten.Platform.MOD-0027.EmailDispatchJob`, `Diten.Platform.MOD-0023.WorkflowEscalationSweepJob`,
> `Diten.Platform.MOD-0033.QuotaResetJob` … Aynı kalıbı izleyip `Diten.Platform.CAND-CAP-0008.HolidayAutoFetchJob`
> yazmak, `CAND-CAP-0008` dizesini **runtime'a sokar** ve parent candidate kapısını **kalıcı olarak** kırar
> (`runtime_hits` → `check_candidate` fail). v1 bunu açıkça yazmıştı: *"Bu capability'nin legacy istisnası
> YOKTUR ve olmayacaktır."* Bu yüzden id/owner **nötr slug** taşır: `WorkingCalendar` / `working-calendar`
> (AC-ID-1, AC-SCHED-1).

### Altı kilitli guard (kullanıcı kararı, 2026-08-27 — pack bunlara uyar)

| # | Guard | Pack'teki karşılığı |
|---|---|---|
| **1** | **İş YALNIZCA fetch→staging yapar. Auto-review YOK, auto-apply YOK.** | İş **yalnız** `StartWorkingCalendarImportCommand` gönderir; `Decide…`/`Apply…`/`Discard…` komutlarına **derleme zamanında bile erişmez** (AC-SCHED-2 grep ile doğrular). Canlı takvime yazan tek yol insan checker + `.apply` (§12.5) |
| **2** | **`RequestedBy` = sistem kimliği; `AppliedBy` hâlâ insan ve farklı** | `RequestedBy = "system:auto-fetch-scheduler"` sabiti. `system:` önekli aktör **hiçbir koşulda** `AppliedBy` olamaz (§14.2, AC-SCHED-3). SoD **kendiliğinden** korunur: her apply bir insan tıklamasıdır |
| **3** | **Hedef: AKTİF ülke takvimi olan her country/year** | İş, `CalendarYear ∈ {UtcNow.Year + YearOffsets}` olan **`ScopeType=country` + `CalendarStatus=active`** takvimleri listeler ve **her biri için** bir batch açar. `draft` takvim zamanlanmış koşuma **girmez** (manuel fetch'te girebilir — §12.1/V3); uygun takvim yoksa **sessizce atlanır ve loglanır** — iş takvim **oluşturmaz** (D-F09) |
| **4** | **İdempotent** | `ScheduledRunKey = {country}:{year}:{targetId}`. `pending-review`/`in-review` batch varsa **skip (reuse)**; `applied` batch varsa **skip**; `failed`/`discarded` varsa **yeni batch** (retry). Re-fetch olursa diff **tarihe göre** (§12.4, D-F08 ile aynı kural) → **DUP üretilmez** |
| **5** | **Fail-closed** | Sağlayıcı erişilemez ⇒ o hedefin batch'i `failed` + `FailureReason`, **loglanır**, sonraki koşumda **yeniden denenir**; **kısmi hiçbir şey** (§8.2). Bir hedefteki hata **diğer hedefleri durdurmaz** (`WorkflowEscalationSweepJob`'un "bir tenant'ın hatası diğerlerini durdurmaz" deseni) |
| **6** | **Config** | `Schedule.Enabled` + `CronExpression` (yıllık) + `YearOffsets` + `MaxBatchesPerRun`; egress allowlist ve `ValidateOnStart` **aynen korunur** (§4.8) |

### Üç kapı — hepsi kapalıyken hiçbir şey çalışmaz (fail-closed varsayılan)

```text
1. BackgroundJobs:RegisterStandardJobs = true
   VE BackgroundJobs:EnabledJobs["Diten.Platform.WorkingCalendar.HolidayAutoFetchJob"] = true   ← MOD-0026 kapısı
2. WorkingCalendar:HolidayProvider:Schedule:Enabled = true                                       ← FU02 zamanlama kapısı
3. WorkingCalendar:HolidayProvider:Enabled = true                                                ← FU02 egress kapısı
```

Üçü de **varsayılan `false`**'tur. 1. kapı kapalıysa iş **kaydedilir ama `IsEnabled=false`** olur (MOD-0026
deseni); 2. kapı kapalıysa iş kaydı **hiç üretilmez**; 3. kapı kapalıysa iş çalışır ama **her hedef için**
`auto_fetch_disabled` ile **hiçbir batch açmadan** çıkar ve loglar (AC-SCHED-8).

### İşin çalışma bağlamı

- **Tenant context YOKTUR ve bu DOĞRU davranıştır.** v1 §19.2/11 bunu bir **risk** olarak uyarmıştı:
  *"arka plan işi tenant bağlamı olmadan çalışırsa `HybridRepository.ExecutionFilter` yalnız global satırları
  döndürür."* FU02 için bu risk değil **kesin bir doğruluk özelliğidir**: iş **zaten yalnız** ülke katmanına
  (`TenantId=null`) bakar. `TenantScope.Begin` **kullanılmaz** (`WorkflowEscalationSweepJob`'dan **kasıtlı**
  ayrım — o iş tenant-scoped, bu iş değil).
- **`ICurrentUserContext` işin içinde BOŞTUR.** Bu yüzden `StartWorkingCalendarImportCommand` aktörü
  **parametre olarak** taşır (`RequestedBy` + `TriggerSource`); handler `ICurrentUserContext`'i **okumaz**.
  Manuel uçta controller JWT aktörünü, iş içinde job sistem kimliğini geçer (§12.8/V36).
- **Kaydı MOD-0026 tutar.** Koşum başlangıcı/sonu/hatası `IJobExecutionLogWriter` üzerinden `JobExecutionLog`'a
  düşer — FU02 **ikinci bir çalışma günlüğü açmaz**.
- **Lease/heartbeat/retry politikası MOD-0026'nındır.** FU02 kendi kilidini **yazmaz**; eşzamanlı iki koşum
  olursa guard 4'ün `ScheduledRunKey` kontrolü **zaten** DUP'ı engeller.

---

## 5. Repo Scope

**Backend — `services/Diten.Platform/`:**

```text
src/Diten.Platform.Domain/Entities/WorkingCalendar/WorkingCalendarImportBatch.cs   (aggregate + aday + vokabüler)
src/Diten.Platform.Domain/Entities/WorkingCalendar/WorkingCalendar.cs              (YALNIZ additive 3 alan — D-F07)
src/Diten.Platform.Domain/Repositories/IWorkingCalendarImportBatchRepository.cs
src/Diten.Platform.Application/Features/WorkingCalendarImport/**                   (§10 klasör sözleşmesi)
src/Diten.Platform.Application/Features/WorkingCalendar/WorkingCalendarValidation.cs (YALNIZ contract Limitations satırı + Writable kümesi)
src/Diten.Platform.Application/BackgroundJobs/PlatformRecurringJobRegistrar.cs     (YALNIZ TEK additive kayıt satırı — §4.9)
src/Diten.Platform.Infrastructure/Persistence/Repositories/WorkingCalendarImportBatchRepository.cs
src/Diten.Platform.Infrastructure/HolidayProviders/NagerHolidayProvider.cs
src/Diten.Platform.Infrastructure/HolidayProviders/OfflineHolidayProvider.cs
src/Diten.Platform.Infrastructure/Settings/HolidayProviderOptions.cs (+ Validator)
src/Diten.Platform.Infrastructure/DependencyInjection.cs   (YALNIZ class-map + index + DI + AddHttpClient + Options)
src/Diten.Platform.API/Controllers/WorkingCalendarImportsController.cs
src/Diten.Platform.API/Models/WorkingCalendarImportRequests.cs
tests/**/WorkingCalendarImport/**
```

**Frontend — `frontend/Diten.Web/` (TEK yüzey, platform shell):**

```text
Controllers/Platform/WorkingCalendarImportsController.cs           (same-origin proxy — v1 emsali)
Views/Platform/WorkingCalendarImports/**                           (§11.2 — 7 + 1 dosya)
wwwroot/assets/js/Platform/WorkingCalendarImports/**               (3 dosya)
Resources/Views/Platform/WorkingCalendarImports/WorkingCalendarImportsIndex.{en,tr}.resx   (platform shell = 2 dil)
Resources/SharedResource.{en,tr}.resx                              (YALNIZ WorkingCalendarImportsMenu + Nav anahtarları)
Views/Shared/_LayoutPlatformAdmin.cshtml                           (YALNIZ permission-guard'lı tek <li>)
Navigation/PlatformNavigationCatalog.cs                            (YALNIZ tek kayıt — v1 emsali)
```

**Bu pack (bugün geçerli olan TEK yazma alanı):**

```text
execution/domains/platform-shared-services/module-packs/CAND-CAP-0008-FU02-working-calendar-public-holiday-auto-fetch.md
```

---

## 6. Protected Paths

- `.antigravity/**` (global engineering system)
- `gateway/Diten.ApiGateway/**/ocelot.json` — **integration-agent owned**; §15 gereği **değişiklik GEREKMEZ**
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` (**FROZEN**)
- **FU01 yüzeyleri — okunur, DEĞİŞTİRİLMEZ:**
  - `Features/WorkingCalendar/Provider/**` (`IWorkingCalendarProvider`, `WorkingCalendarResolveEngine`,
    `WorkingCalendarContracts`) — **çözümleme sırası ve seam imzası DONDURULMUŞ**
  - `Features/WorkingCalendar/Handlers/**` (`WorkingCalendarWriteGuard` dâhil) — **çağrılır, düzenlenmez**
  - `Features/WorkingCalendar/WorkingCalendarPermissions.cs` — mevcut 5 anahtar **değişmez**
  - `Views/Platform/WorkingCalendars/**` · `Views/WorkingCalendar/Overrides/**` — FU01 UI'ı **dokunulmaz**
- `Features/Lookups/**` (`IPlatformLookupProvider` imzası, `PlatformLookupKeys`)
- `Domain/Entities/Organization/**` · `Domain/Entities/ReferenceDataEntities*.cs`
- `services/Diten.Platform.Common/**` (`EntityVariations.cs`, `Repositories.cs`)
- `services/Diten.Building.Blocks/**` — **tüketilir, DEĞİŞTİRİLMEZ**: `IRecurringJobRegistrar`,
  `IBackgroundJobHandler<T>`, `RecurringJobRegistration`, `BackgroundJobDescriptor`,
  `BackgroundJobSchedulerOptions` **imzaları** FU02 tarafından **okunur ve uygulanır**; tek satırı bile
  düzenlenmez (D-F02)
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/BackgroundJobs/**` (Hangfire executor,
  hosted service, dashboard authz) — **MOD-0026 owned**, FU02 **dokunmaz**
- `Domain/Entities/**JobExecutionLog**` + `Application/BackgroundJobs/JobExecutionLogWriter.cs` —
  **MOD-0026 owned**; FU02 yalnız `IJobExecutionLogWriter`'ı **çağırır**
- Diğer domain servisleri: `Diten.MdmService/**` · `Diten.CrmService/**` · `Diten.HcmService/**` ·
  `Diten.EnterpriseStrategyService/**` · `Diten.DevEnablementService/**` · `Diten.AuthService/**`
- RBAC katalog/seed dosyaları ve `rolePermissions` koleksiyonu (**F02-RBAC**)
- `execution/registries/**` (**D-F11** karara bağlanana kadar yazılmaz)
- Mongo hand-edit (**yasak**)

> **`WorkingCalendar.cs`'e dokunuş istisnası açıkça sınırlıdır (D-F07):** yalnız **üç yeni nullable/varsayılanlı
> alan** eklenir. Mevcut hiçbir alan yeniden adlandırılmaz, tipi değişmez, kaldırılmaz; `ActiveDays()`,
> `IsEffectiveOn()`, `IsWorkingDayOverride` gibi davranışlar **aynen kalır**. D-F07 reddedilirse bu dosyaya
> **hiç dokunulmaz** ve provenance yalnız batch tarafında yaşar (§19.1'deki alternatif).

---

## 7. Dependencies

| Bağımlılık | Tür | Durum (kod üzerinden doğrulandı, 2026-08-27) | Bu FU ne yapar |
|---|---|---|---|
| **CAND-CAP-0008 (FU01)** | **PARENT — tüketim + additive** | **SHIPPED** — `Domain/Entities/WorkingCalendar/WorkingCalendar.cs`, `Features/WorkingCalendar/**`, 2 controller, 12 endpoint, 42/42 test | Aggregate'i **tüketir**; gün yazımını **FU01 write path'i** üzerinden yapar; şemayı **additive** genişletir |
| `UpsertWorkingCalendarDayCommand` + `WorkingCalendarWriteGuard` + `WorkingCalendarValidation.ValidateDayInput` | **tüketim** | **SHIPPED** — `Handlers/CommandHandlers/` | Merge **aynı guard + aynı validator + aynı `ReplaceAsync(expectedVersion)`** ile yazar (D-F08) |
| **MOD-0048** (`countries`) | **read-only, in-process** | **SHIPPED** — `IPlatformLookupProvider`, `PlatformLookupKeys.Countries` | `CountryCode`'u doğrular; set publish **etmez** |
| **MOD-0018** RBAC | **tüketim** | SHIPPED | `[HasPermission]` guard'ları; **seed/grant yok** (**F02-RBAC**) |
| **MOD-0021** Audit | **gevşek** | SHIPPED | fetch/decision/apply audit'e düşmeli (**F02-AUDIT**) — blocker değil |
| **MOD-0026** Job Scheduler | **TÜKETİLİR — yalnız fetch zamanlaması** | **SHIPPED ve canlı doğrulandı**: `Diten.Building.Blocks/…/BackgroundJobs/**` (seam'ler) · `Diten.Platform.Infrastructure/BackgroundJobs/**` (Hangfire executor + `HangfireRecurringJobRegistrationHostedService`) · `Diten.Platform.Application/BackgroundJobs/PlatformRecurringJobRegistrar.cs` (**9 kayıt, 2'si gerçek sweep işi**) · `IJobExecutionLogWriter` | **Bir** recurring kayıt ekler (`Diten.Platform.WorkingCalendar.HolidayAutoFetchJob`) + **bir** `IBackgroundJobHandler<T>` yazar (§4.9). Scheduler'ı **değiştirmez**, kendi kilidini/retry'ını **yazmaz**, `JobExecutionLog`'u **sahiplenmez**. İş **yalnız fetch** yapar — **review/apply ASLA** |
| **MOD-0012** Secrets | **TÜKETİLMEZ** | SHIPPED | Nager API-key'siz → secret ihtiyacı **yok** (§4.8) |
| **MOD-0032** Gateway | **DEĞİŞİKLİK YOK** | SHIPPED | Route mevcut `/api/platform/working-calendars/{everything}` çiftinin **altına düşer** (§15) |
| **DEV-0000** Golden Slim | **şablon** | SHIPPED | §10/§11 birebir taklit |
| `HttpClient` altyapısı | **mevcut** | `services.AddHttpClient<...>` deseni Platform'da canlı (`DependencyInjection.cs:178-183`) | **İLK dış-internet tüketicisi** — §8.4 uyarısı |

---

## 8. Runtime Constraints

**8.1 Persistence.** MongoDB tek instance. `working_calendar_import_batches` **yeni** koleksiyondur ve
`GlobalEntity` olduğu için tenant filtresi taşımaz (§4.1). `WorkingCalendarImportBatch` **ve** gömülü
`WorkingCalendarImportCandidate` `DependencyInjection.cs`'e **class-map olarak kaydedilmezse** `Guid` alanları
binary yazılır ve filtreler **sessizce boş döner** (v1 §19.2/10 dersi — bu tuzak FU02'de **iki kat** geçerlidir
çünkü iki yeni tip vardır).

**8.2 Fail-closed izolasyon — FU02'nin varlık nedeni.**

```text
Sağlayıcı erişilemez / timeout / 5xx / bozuk JSON
   ⇒ ImportStatus = failed,  Candidates = [],  FailureReason dolu
   ⇒ hedef takvimde DEĞİŞEN BAYT YOK, IWorkingCalendarProvider'ın cevabı DEĞİŞMEZ
```

Sağlayıcı **boş dizi** dönerse bu `fetched` + `CandidateCount = 0`'dır ve **`failed` değildir** — ama yine de
**hiçbir şey silinmez**: FU02 **yalnız ekler**, **asla gün kaldırmaz/arşivlemez** (§8.7).

**8.3 Egress.** Tek host (`date.nager.at`), allowlist ile fail-closed, timeout `TimeoutSeconds`, redirect
kapalı, **retry yok**, secret yok (§4.8). Dış çağrı **yalnız** `NagerHolidayProvider` içinde yapılır;
Application katmanında `HttpClient` **görünmez**.

**8.4 Bu servisin ilk dış-internet çağrısıdır.** `Diten.Platform`'daki mevcut `AddHttpClient` kayıtlarının
**hepsi iç servis çağrısıdır** (`MdmLegalEntityReferenceValidator`, `AuthServiceUserReferenceValidator`).
FU02 ile servis **ilk kez** kurum dışına çıkar. Bu, deploy ortamında **firewall/proxy** gerektirebilir ve
`Enabled=false` varsayılanı bunun içindir: yetenek **açılana kadar kapalıdır**.

**8.5 Ham payload saklanmaz, hash'i saklanır.** Provenance için `ProviderPayloadHash` (SHA-256) yeterlidir.
Ham JSON'u saklamak doküman boyutunu şişirir ve **ikinci bir gerçek kaynağı** yaratır (MOD-0028-FU07'nin
*"the field-level finding set is never embedded in the manifest"* dersinin aynısı).

**8.6 Doküman büyümesi ve liste projeksiyonu.** `MaxCandidatesPerBatch = 400` (FU01'in
`MaxDaysPerCalendar` sınırıyla **aynı**). Liste sorgusu `Candidates` alanını **projeksiyonla dışarıda
bırakır**; DataTable yalnız sayaç rozetlerini gösterir (v1 §19.2/9 dersi).

**8.7 Merge yalnız EKLER.** FU02 hedef takvimde **hiçbir günü silmez, arşivlemez veya güncellemez**
— `already-present` adayı onaylansa bile **no-op**tur (§12.4). "Sağlayıcıda artık yok" durumu bir aday
**üretmez**: bir tatilin kaldırılması **insan kararıdır** ve FU01 gün editöründen yapılır → **F02-REMOVAL**.

**8.8 Concurrency — iki ayrı token.** Batch'in kendi `Version`'ı (karar yazımları) **ve** hedef takvimin
`Version`'ı (merge) **ayrı ayrı** kontrol edilir. Merge sırasında takvim değişmişse **409** ve batch
`in-review` kalır — **kısmi merge yoktur** (D-F08).

**8.9 Transaction gerekmez ama iki doküman vardır.** Apply işlemi (a) hedef takvimi replace eder, (b) batch'i
`applied` yapar. Standalone Mongo'da transaction **garanti edilemez** (CRM dersi), bu yüzden sıra
**takvim önce, batch sonra**dır: (a) başarılı + (b) başarısız ⇒ günler yazılmış ama batch `in-review`
kalmıştır; bu durum **`AppliedDayIds` + `already-present` diff'i** sayesinde **tekrar apply edildiğinde
idempotenttir** (aynı tarihler artık `already-present`, no-op). Ters sıra ise "batch applied ama takvim boş"
**yalanını** üretirdi.

**8.10 Provider cache YOK.** FU01 §8.8 aynen geçerli; FU02 ek bir cache **açmaz**.

**8.11 API/Gateway.** Frontend **Gateway 5000** üzerinden çağırır; browser JS **5057'ye gitmez**; same-origin
proxy (`/Platform/WorkingCalendarImports/api/...`) kullanılır.

**8.12 Localization — Platform = 2 dil.** `.resx` yalnız `en` + `tr` (PSS domain-config §Runtime Decisions +
v1 §19.2/3: **dil sayısı shell'e bağlıdır**, bu yüzey platform shell'dir).

**8.13 Zamanlanmış koşum (D-F02).** Cron **UTC**'dir (`BackgroundJobDescriptor.Validate` başka bir
`TimeZoneId`'yi **exception** ile reddeder). Koşum **tenant bağlamsızdır** ve bu doğrudur (§4.9). Koşum başına
en fazla `MaxBatchesPerRun` batch açılır; sınır aşılırsa kalanlar **bir sonraki koşuma** bırakılır ve
**loglanır** — sessizce düşürülmez. İş **hiçbir zaman** canlı takvime yazmaz, dolayısıyla eşzamanlı iki koşum
en kötü ihtimalle **fazladan bir staging batch'i** üretir; `ScheduledRunKey` kontrolü bunu da engeller.

---

## 9. Layout & Shell Contract

**TEK yüzey: Platform Admin → Working Calendar Imports (`shell: platform-admin`).** Tenant yüzeyi
**yoktur ve açılmayacaktır** (§2.3/1).

```cshtml
@{
    ViewData["Title"] = Localizer["PageTitle"];
    Layout = "_LayoutPlatformAdmin";   // shell: platform-admin — AÇIKÇA, _ViewStart varsayılanına GÜVENİLMEZ
}
```

Bu satır `Index.cshtml` **ve** `Review.cshtml`'in **her ikisinde** açıkça yazılır (AC-UI-1).

| Öğe | Değer |
|---|---|
| Razor layout | **`_LayoutPlatformAdmin`** (açıkça, her view'da) |
| View klasörü | `Views/Platform/WorkingCalendarImports/` |
| Frontend route | `/Platform/WorkingCalendarImports` → self-registration §2c gereği **PlatformAdmin** permission scope'u |
| Menü | `_LayoutPlatformAdmin.cshtml` içinde **tek** `<li>`, `platform.working-calendar.auto-fetch.read` guard'lı; Working Calendars menüsünün **hemen altında** |
| Ctrl+K | `PlatformNavigationCatalog` içinde **tek** kayıt |
| Manifest | `WorkingCalendarImportManifestProvider` — **zorunlu** (`module-self-registration-standard.md`; v1'in §19.4'te atladığı ve sonradan eklemek zorunda kaldığı şey, bu pack'te **baştan** yazılıdır) |

---

## 10. Backend File Convention

Golden Reference **Slim** (DEV-0000) naming'i birebir; canlı emsal `Features/WorkingCalendar/`:

```text
services/Diten.Platform/src/Diten.Platform.Application/Features/WorkingCalendarImport/
├── Commands/
│   ├── StartWorkingCalendarImportCommand.cs                (sealed record)
│   ├── DecideWorkingCalendarImportCandidateCommand.cs
│   ├── DecideWorkingCalendarImportBatchCommand.cs
│   ├── ApplyWorkingCalendarImportCommand.cs
│   └── DiscardWorkingCalendarImportCommand.cs
├── Queries/
│   ├── ListWorkingCalendarImportsQuery.cs                  (sealed record)
│   ├── GetWorkingCalendarImportByIdQuery.cs
│   ├── GetWorkingCalendarImportContractQuery.cs
│   └── GetHolidayProviderStatusQuery.cs
├── Handlers/
│   ├── CommandHandlers/                                    ← AYRI klasör (ZORUNLU)
│   │   ├── StartWorkingCalendarImportHandler.cs            (sealed class, Command/Query suffix YOK)
│   │   ├── DecideWorkingCalendarImportCandidateHandler.cs
│   │   ├── DecideWorkingCalendarImportBatchHandler.cs
│   │   ├── ApplyWorkingCalendarImportHandler.cs
│   │   └── DiscardWorkingCalendarImportHandler.cs
│   └── QueryHandlers/                                      ← AYRI klasör (ZORUNLU)
│       ├── ListWorkingCalendarImportsHandler.cs
│       ├── GetWorkingCalendarImportByIdHandler.cs
│       ├── GetWorkingCalendarImportContractHandler.cs
│       └── GetHolidayProviderStatusHandler.cs
├── Validators/
│   ├── StartWorkingCalendarImportValidator.cs              (Command suffix YOK)
│   └── DecideWorkingCalendarImportCandidateValidator.cs
├── Provider/
│   ├── IHolidayProvider.cs                                 (§4.5 seam)
│   └── HolidayProviderContracts.cs                         (HolidayFetchResult / ProviderHoliday / Outcome)
├── Mapping/
│   ├── HolidayCandidateMapper.cs                           (sağlayıcı → aday — TEK yer)
│   └── ImportDiffEngine.cs                                 (aday ↔ hedef takvim farkı — TEK yer)
├── BackgroundJobs/                                         ← D-F02 (emsal: Features/Workflow/BackgroundJobs/)
│   ├── HolidayAutoFetchJob.cs                              (IBackgroundJobHandler<HolidayAutoFetchJobArgs>)
│   └── HolidayAutoFetchJobArgs.cs                          (sealed record)
├── SelfRegistration/
│   └── WorkingCalendarImportManifestProvider.cs
├── WorkingCalendarImportPermissions.cs
├── WorkingCalendarImportValidation.cs                      (paylaşılan guard'lar — TEK yer, iki kopya YASAK)
└── WorkingCalendarImportModels.cs                          ← TEK dosyada tüm DTO/ViewModel'ler
```

**Adaptörler Application'da DEĞİL, Infrastructure'dadır** (`erp-architecture.md` katman kuralı):

```text
src/Diten.Platform.Infrastructure/HolidayProviders/NagerHolidayProvider.cs      (HttpClient — TEK dış çağrı yeri)
src/Diten.Platform.Infrastructure/HolidayProviders/OfflineHolidayProvider.cs    (ağsız stub)
src/Diten.Platform.Infrastructure/Settings/HolidayProviderOptions.cs (+ Validator, ValidateOnStart)
```

**Naming:** Command = `{Verb}WorkingCalendarImport…Command` (record) · Query = `{Get|List}…Query` (record) ·
Handler = `{Verb}…Handler` (class, **suffix YOK**) · Validator = `{Verb}…Validator` (**suffix YOK**).

> **⚠️ BEYAN EDİLEN ÜÇ SAPMA (gizlenmiyor):**
> 1. **`Delete`/`BulkDelete` YOKTUR.** Batch bir **denetim kaydıdır**; silinmez, `discard` edilir. Sonuç:
>    `verify_datatable_page.py`'ın bulk-delete kontrolleri **EXPECTED N/A** (v1 §19.5 ile **aynı aile**) —
>    beklenen sayı §17'de **koşumdan önce** ilan edilmiştir.
> 2. **`Update` YOKTUR.** Batch fetch anında donar; değişen tek şey **kararlar** ve **statü**dür. Golden
>    Reference'ın `Update{Module}Command` beklentisi bu modülde **anlamsızdır**.
> 3. **`Provider/` + `Mapping/` alt klasörleri** Golden Reference'ta yoktur. Emsal: FU01'in `Provider/`
>    klasörü, MOD-0165-FU03'ün `Resolve/`, MOD-0164-FU02'nin `Evaluation/` klasörü. Eşleme ve diff mantığını
>    handler'lara gömmek `handler-design.md` sınırını ihlal eder **ve** aynı kuralın iki kopyasını doğurur.

---

## 11. Frontend File Contract

### 11.1 Golden reference kararı — kullanıcı-form alanı türetmesi (GÖSTERİLİR)

Sayım kuralı (`module-pack-standard.md` §3): yalnız kullanıcının create/edit formunda **doldurduğu** modül
alanları sayılır. `Id`, audit alanları, **türetilmiş** alanlar, aksiyon-doldurmalı alanlar ve DataTable
checkbox/action kolonları **sayılmaz**.

**FU02'nin tek yazma formu "yeni fetch başlat" formudur.** §4.2'deki 22 satırdan form-içi olanlar:

| # | Kullanıcı-form alanı | Not |
|---|---|---|
| 1 | `CountryCode` | MOD-0048 `countries` seçici |
| 2 | `CalendarYear` | sayı |
| 3 | `TargetCalendarId` | ülke+yıla göre **filtrelenmiş** uygun takvim listesi (D-F09) |
| 4 | `IncludeNonPublicTypes` | checkbox |
| 5 | `Notes` | serbest metin |

*Form-dışı:* `BatchCode` (**üretilir**) · `ProviderKey`/`ProviderEndpoint` (**config**, §4.8) ·
`ImportStatus` (**akış**) · `Candidates` (**review grid'i, form alanı değil**) · tüm sayaçlar (**türetilmiş**)
· `RequestedBy/At`, `AppliedBy/At`, `AppliedDayIds`, `TargetCalendarVersionAtApply`, `ProviderFetchedAt`,
`ProviderPayloadHash`, `ProviderOutcome`, `FailureReason` (**aksiyon/kanıt**) · `BaseEntity` alanları.

→ **5 ≤ 8 ⇒ `golden_reference: slim`** (frontmatter `form_field_count: 5`).

> **⚠️ v1 Compact'tı, FU02 Slim — bu bir tutarsızlık değil, kuralın mekanik uygulanmasıdır.** v1'in formu
> takvimin 11 alanını taşıyor; FU02'nin formu **beş** alan taşıyor. Compact'a çıkmak için alan **uydurmak**
> (ör. `ProviderKey`'i kullanıcıya seçtirmek) hem gaming olurdu hem de §4.8'in *"sağlayıcı seçimi
> config'tir"* kararını bozardı. v1'in kendi §11.3'ünde aynı ilke **ters yönde** uygulanmıştı: *"eşiği
> tutturmak için alan düşürmek yanlış bir gerekçedir."*
>
> **Aday review grid'i ayrı bir golden-reference yüzeyi DEĞİLDİR** — v1'in gün alt-editörü için verdiği
> kararın aynısı (MOD-0162-FU05/S2 emsali): aday, kendi sayfası/CRUD'u olan bağımsız bir modül değil, bir
> batch'in **gömülü çocuğudur**. **İkinci bir Slim/Compact kararı doğurmaz.**

### 11.2 Dosya seti — TEK klasör, kanonik Slim 7 dosya + 1 BEYAN EDİLMİŞ ek

**`Views/Platform/WorkingCalendarImports/` (DEV-0000 Slim):**

| # | Dosya | Rol |
|---|---|---|
| 1 | `Index.cshtml` | Batch listesi kabuğu; `Layout = "_LayoutPlatformAdmin"` **açıkça**; ① Filter → ② DataTable. **TEK** DataTable |
| 2 | `_Filter.cshtml` | Inline collapsible filter: `countryCode` · `calendarYear` · `importStatus` · `targetCalendarId` · **`triggerSource`** (manuel/zamanlanmış) |
| 3 | `_DataTable.cshtml` | `data-dt-standard="v2"` + skeleton; kolonlar: batch kodu · ülke · yıl · hedef takvim · statü rozeti · **tetik rozeti (`manual` / `scheduled`)** · **aday sayaçları (onaylı/ret/karar-bekleyen)** · maker · tarih |
| 4 | `_CreateEditOffcanvas.cshtml` | **Slim-özel** — "Yeni fetch başlat" (5 alan). **Edit yolu YOKTUR**; offcanvas **yalnız create** modunda açılır (§10 sapma 2) |
| 5 | `_DetailsQuickView.cshtml` | **Slim-özel** — batch özeti: provenance (sağlayıcı, endpoint, hash, zaman), sayaçlar, statü, `FailureReason`. **Karar verilmez**, yalnız okunur |
| 6 | `_IndexL10n.cshtml` | JSON payload bridge |
| 7 | `WorkingCalendarImportsIndex.cs` | Marker class (RESX kökü) |
| **8** | **`Review.cshtml`** | **BEYAN EDİLMİŞ EK (kanonik Slim setinde yoktur)** — gün düzeyi review grid'i + approve/reject + apply/discard aksiyonları |

> **`Review.cshtml` neden ekleniyor ve neden `_DetailsQuickView` yetmiyor — gerekçe (D-F12):**
> Bir batch tipik olarak **15–30 aday** taşır ve her biri için ayrı bir karar, bir gerekçe kutusu, bir diff
> rozeti ve mevcut günle karşılaştırma gerekir. Bunu bir offcanvas QuickView'a sıkıştırmak, **kararın
> kalitesini** düşürür — operatör göremediği şeyi onaylar. Alternatif (aday grid'ini Index'e ikinci bir
> DataTable olarak koymak) **MOD-0162-FU03'ün hibrit-tek-sayfa hatasıdır**: tek sayfa iki golden-reference
> referansını birden geçemez ve `updateVisualState` global selector çakışması (bilinen tuzak) doğar.
> **Ayrı sayfa** her iki riski de **yapısal olarak** ortadan kaldırır: Index'te tek DataTable kalır,
> review grid'i kendi sayfasında yaşar.
>
> Bu sapma `verify_datatable_page.py` **Index kontrollerini etkilemez** (fazladan dosya bir eksiklik değildir);
> yine de §17'de **koşumdan önce** ilan edilmiştir.

**JS (Golden Slim seti + 1):**

```text
wwwroot/assets/js/Platform/WorkingCalendarImports/index.js       → DataTable (DtDefaults + v2), filtre, fetch offcanvas, quickview
wwwroot/assets/js/Platform/WorkingCalendarImports/index.l10n.js  → camelCase→PascalCase L10n köprüsü (ATLANMAZ)
wwwroot/assets/js/Platform/WorkingCalendarImports/review.js      → aday grid'i, gün-düzeyi karar, toplu fan-out, apply/discard
```

**RESX (tek klasör × 2 dil — platform shell standardı):**

```text
Resources/Views/Platform/WorkingCalendarImports/WorkingCalendarImportsIndex.{en,tr}.resx
Resources/SharedResource.{en,tr}.resx        → WorkingCalendarImportsMenu + Nav.Page.* anahtarları
```

**YASAK dosyalar:** `Create.cshtml` · `Edit.cshtml` · `Details.cshtml` · `_Form.cshtml` (**Slim yasağı** —
Compact-özel dosyalar) · `Views/CRM/**` · tenant shell altında herhangi bir FU02 view'ı ·
**hardcoded ülke veya vokabüler listesi** · Index'te ikinci bir DataTable.

**Kullanılan mevcut yüzeyler (yeni dosya değil):** ülke seçici `GET /api/lookups/countries` (PlatformActor —
bu yüzey platform aktörüdür, v1 §19.2/4'teki tenant tuzağı **burada geçerli değildir**); hedef takvim seçici
`GET /api/platform/working-calendars?scopeType=country&countryCode=…&calendarYear=…` (FU01'in **mevcut** list
endpoint'i — yeni endpoint açılmaz).

---

## 12. Validation Rules

### 12.1 Alan bazlı (fetch başlatma)

| # | Field | Required | Format / Rule | Pre-check |
|---|---|---|---|---|
| V1 | `CountryCode` | Evet | ISO alpha-2, upper-case; MOD-0048 `countries` üyesi | `IPlatformLookupProvider` — **in-process**, HTTP self-call YOK |
| V2 | `CalendarYear` | Evet | `1900 <= yıl <= 2200` | — |
| V3 | `TargetCalendarId` | Evet | Var olmalı; `ScopeType=country`; `TenantId=null`; `CalendarStatus ∈ {draft, active}` | `IWorkingCalendarRepository` |
| V4 | `TargetCalendarId` ↔ `CountryCode` | Evet | Hedefin `CountryCode`'u istekle **aynı** | 400 `target_country_mismatch` |
| V5 | `TargetCalendarId` ↔ `CalendarYear` | Evet | Hedefin `CalendarYear`'ı istekle **aynı** | 400 `target_year_mismatch` |
| V6 | Hedef `archived` | — | `archived` hedef **reddedilir** | 409 `calendar_archived` (FU01 guard'ının aynısı) |
| V7 | `IncludeNonPublicTypes` | Evet | bool | — |
| V8 | `Notes` | Hayır | Max 2000, trim | — |
| V9 | Sağlayıcı `Enabled` | — | `HolidayProviderOptions.Enabled=false` ⇒ **403 `auto_fetch_disabled`**; batch **oluşmaz** | Options |
| V10 | Açık batch tekilliği | — | Aynı `TargetCalendarId` için `pending-review`/`in-review` bir batch **varsa** ikinci fetch **409 `open_batch_exists`** | Repository |

> **V10 neden var:** aynı takvim için iki açık batch, iki farklı insanın **birbirinden habersiz** aynı günleri
> onaylamasına ve diff'in bayatlamasına yol açar. Operatör önce açık batch'i **apply** veya **discard** eder.

### 12.2 Aday üretimi (mapping) kuralları

| # | Kural |
|---|---|
| V11 | Sağlayıcı `Outcome != fetched` ⇒ **hiç aday üretilmez**, batch `failed` |
| V12 | Sağlayıcı `countryCode` istenenden farklıysa ⇒ **tüm batch** `failed` + `invalid_response` |
| V13 | `types` `Public` içermiyor **ve** `IncludeNonPublicTypes=false` ⇒ aday **üretilmez** (sessizce atlanır ama batch `SkippedNonPublicCount` ile **sayısını gösterir**) |
| V14 | `types` `Public` içermiyor **ve** `IncludeNonPublicTypes=true` ⇒ aday üretilir, `MappedDayType=null`, `Flags: type_not_public, unmapped_type` |
| V15 | `global=false` veya `counties` dolu ⇒ `Flags: subdivision_scoped` |
| V16 | `date.Year != CalendarYear` ⇒ `Flags: date_outside_calendar_year` (aday üretilir ama **onaylanırsa** FU01 validator'ı zaten `day_year_mismatch` ile reddeder → §12.5/V25) |
| V17 | Aynı `ProviderDayKey` iki kez gelirse ⇒ ikincisi **atlanır**; batch `DuplicateSourceRowCount` gösterir |
| V18 | `MappedDayName` boş/whitespace ⇒ aday `flagged` + `unmapped_type`; **isimsiz gün yazılmaz** |
| V19 | `Candidates.Count > MaxCandidatesPerBatch` ⇒ batch `failed` + `provider_payload_too_large`; **kısmi staging YOK** |

### 12.3 Batch statü geçişleri (state machine)

```text
(oluşturma) ─────────────→ fetching
fetching       ────────────→ pending-review        (Outcome = fetched)
fetching       ────────────→ failed                (Outcome != fetched  |  V12 / V19)
pending-review ────────────→ in-review             (ilk gün-düzeyi karar yazıldığında)
pending-review ────────────→ discarded             (operatör tüm batch'i reddeder)
in-review      ────────────→ in-review             (karar değişiklikleri; apply'a kadar serbest)
in-review      ────────────→ applied               (apply — §12.5 kapılarının HEPSİ geçilirse)
in-review      ────────────→ discarded
applied · discarded · failed  = TERMİNAL
```

**Yasak geçişler (hepsi 409):** `failed → *` · `applied → *` · `discarded → *` ·
`pending-review → applied` (**karar verilmemiş adayla apply YASAK** — implicit approve yoktur) ·
`fetching → applied`. **Retry diye bir geçiş yoktur**: yeniden denemek **yeni bir batch**tir (§8.3).

### 12.4 Diff (`ChangeKind`) kuralları — idempotency'nin kalbi

Karşılaştırma **hedef takvimin `ActiveDays()` kümesine** ve **etkin tarihe** (`ObservedDate ?? Date`) göre
yapılır (kullanıcı kilidi: *"Date'e göre eşleşir, DUP üretmez"*):

| # | Durum | `ChangeKind` | Varsayılan UI kararı | Apply davranışı |
|---|---|---|---|---|
| V20 | Hedefte o etkin tarihte **aktif gün yok** | `new` | (öneri: onay) | Yeni gün **eklenir** |
| V21 | Hedefte o tarihte **provider kaynaklı** aktif gün var | `already-present` | (öneri: ret) | **NO-OP** — onaylansa bile hiçbir şey yazılmaz (§8.7) |
| V22 | Hedefte o tarihte **manuel** aktif gün var | `conflicts-manual` | **ret** (`existing_manual_day`) | **NO-OP** — insan emeği asla ezilmez |
| V23 | Aynı `ProviderDayKey`'li provider günü hedefte **başka bir tarihte** var | `date-shift` | **ret** (dikkat gerektirir) | **NO-OP** — tarih taşımak bir **güncellemedir**, FU02 yalnız ekler (§8.7) → **F02-SHIFT** |

**İdempotency garantisi (test edilir):** aynı `country/year/target` için **ikinci** bir fetch, birinci batch
apply edildikten sonra çalıştırılırsa **tüm adaylar `already-present`** olur ve apply **sıfır gün yazar**.
Takvimde **DUP oluşmaz** (AC-IDEMP-1/2).

### 12.5 Apply (merge) kapıları — sırayla, hepsi fail-closed

```text
1.  Batch var mı, ImportStatus == in-review mi?                     değilse 409
2.  UndecidedCount == 0 mı?                                         değilse 409 undecided_candidates_remain
3.  ApprovedCount > 0 mı?                                           değilse 409 nothing_to_apply
4.  Aktör != RequestedBy mi?  (SoD)                                 değilse 403 maker_cannot_be_checker
5.  Aktör auto-fetch.apply taşıyor mu?                              yoksa   403
6.  Hedef takvim yükleniyor: FU01 WorkingCalendarWriteGuard         archived ⇒ 409, yok ⇒ 404
7.  Hedef ACTIVE ise: aktör platform.working-calendar.activate      yoksa   403  (D-F10)
8.  Hedefin CountryCode/CalendarYear'ı hâlâ batch ile aynı mı?      değilse 409 target_drifted
9.  Onaylı adaylar YENİDEN diff'lenir (bayat diff kullanılmaz)      no-op'lar elenir
10. Her uygulanabilir aday için FU01 ValidateDayInput               ilk hata ⇒ 400, HİÇBİR gün yazılmaz
11. TEK ReplaceAsync(calendar, expectedVersion)                     uyuşmazlık ⇒ 409, HİÇBİR gün yazılmaz
12. Batch applied + AppliedDayIds + AppliedBy/At yazılır            (§8.9 sırası)
```

| # | Kural |
|---|---|
| V24 | **Adım 9 zorunludur.** Fetch ile apply arasında geçen sürede takvime elle gün eklenmiş olabilir; **staging'deki diff bayattır** ve yeniden hesaplanmadan kullanılmaz |
| V25 | **Adım 10 FU01 validator'ının kendisidir** — kopyası değil. `day_year_mismatch`, `duplicate_day_date`, `unsupported_vocabulary_value` kuralları **aynen** uygulanır |
| V26 | **Adım 11 TEK yazımdır.** N onaylı gün, N replace **değil**, tek bir doküman replace'i ile yazılır (D-F08). Tek doküman replace'i Mongo'da atomiktir ⇒ **kısmi merge yapısal olarak imkânsızdır** |
| V27 | Adım 10'da **tek bir aday** bile reddedilirse **hiçbiri** yazılmaz (all-or-nothing) |

### 12.6 `DayCode` üretimi ve çakışma

| # | Kural |
|---|---|
| V28 | `MappedDayCode = "PF-" + Date.ToString("yyyyMMdd")` — deterministik ve okunabilir |
| V29 | Hedef takvimde aynı kod **zaten varsa** `-2`, `-3` … eklenir; `Flags: day_code_collision` ile aday **işaretlenir** (sessiz değişiklik yok) |
| V30 | Kod formatı FU01'in `^[A-Za-z0-9._-]+$` + max 64 kuralına **uyar** |

### 12.7 FU01 semantiğinin korunması (kırmızı çizgiler)

| # | Kural |
|---|---|
| V31 | `Source = provider-fetch` **yalnız apply yolundan** yazılabilir. FU01'in create/update formundan gönderilirse **hâlâ 400** (v1 §13 kuralı **korunur**, yalnız apply yolu istisna edilir) |
| V32 | FU02 hedef takvimin `CalendarStatus`'unu **değiştirmez** — draft'ı activate **etmez**, active'i archive **etmez** |
| V33 | FU02 `WeekendDays`'e **dokunmaz** |
| V34 | FU02 tenant satırına (`TenantId != null`) **hiçbir koşulda** yazmaz; `TargetCalendarId` tenant satırıysa **404** |
| V35 | `IWorkingCalendarProvider`'ın çözümleme sırası, `Resolution` kümesi ve `ReasonCodes` kümesi **değişmez** |

### 12.8 Zamanlanmış fetch kuralları (D-F02 — guard 1…6'nın test edilebilir hâli)

| # | Kural |
|---|---|
| V36 | `RequestedBy` **ve** `TriggerSource` komuta **parametre** olarak geçer; handler `ICurrentUserContext`'i **okumaz**. Manuel uç JWT aktörünü + `manual`, iş `system:auto-fetch-scheduler` + `scheduled` geçer. **HTTP payload'ından okunmaz** |
| V37 | İşin hedef kümesi: `ScopeType=country` **VE** `TenantId=null` **VE** `CalendarStatus=active` **VE** `CalendarYear ∈ {UtcNow.Year + YearOffsets}`. `draft` takvim zamanlanmış koşuma **girmez** |
| V38 | Uygun hedef yoksa iş **hata vermez**: `skipped_no_active_calendar` ile loglar ve devam eder. İş takvim **oluşturmaz** (D-F09) |
| V39 | İş, hedef başına **en fazla bir** batch açar; toplam `MaxBatchesPerRun` ile sınırlıdır; sınır aşılırsa kalanlar **loglanarak** bir sonraki koşuma bırakılır |
| V40 | **İdempotency**: `ScheduledRunKey = {country}:{year}:{targetId}` için `pending-review`/`in-review` batch varsa **skip (reuse)** · `applied` varsa **skip** · `failed`/`discarded` varsa **yeni batch** |
| V41 | **`applied ⇒ skip` bilinçli bir sınırlamadır.** Yıl içinde ilan edilen/kaydırılan bir tatili zamanlanmış iş **yakalamaz**; bunun için **manuel re-fetch** kullanılır (diff `already-present` + `new` verir) → **F02-SCHED-REFRESH** |
| V42 | Bir hedefteki başarısızlık **diğer hedefleri durdurmaz**; her hedef kendi batch'ini ve kendi `FailureReason`'ını taşır |
| V43 | `Schedule.Enabled=false` ⇒ recurring kayıt **hiç üretilmez**. `HolidayProvider.Enabled=false` ⇒ iş çalışır ama **hiçbir batch açmadan** `auto_fetch_disabled` loglayıp çıkar |
| V44 | İş **yalnız** `StartWorkingCalendarImportCommand` gönderir. `Decide…`, `Apply…`, `Discard…` komutlarına **referans vermez**; `IWorkingCalendarRepository`'ye **yazma** çağrısı yapmaz |

---

## 13. Failure Path to Verify

- **Sağlayıcı erişilemez / timeout** → batch `failed` + `provider_unavailable`; **aday yok**, takvim **değişmez**
- **Sağlayıcı 404 (ülke desteklenmiyor)** → `failed` + `country_not_supported`; kullanıcıya **sağlayıcının
  cevabı** gösterilir, uydurma mesaj yok
- **Bozuk/beklenmedik JSON** → `failed` + `invalid_response`; **kısmi staging YOK**
- **Allowlist dışı host** (config yanlış) → uygulama **başlamaz** (`ValidateOnStart`); çalışma anında
  redirect → istek **iptal**, `provider_unavailable`
- **`Enabled=false`** → fetch **403 `auto_fetch_disabled`**; batch **oluşmaz**
- **Aynı takvim için açık batch var** → **409 `open_batch_exists`**
- **Hedef takvim yok / tenant satırı** → **404**
- **Hedef ülke/yıl uyuşmazlığı** → **400 `target_country_mismatch` / `target_year_mismatch`**
- **Hedef `archived`** → **409 `calendar_archived`**
- **Karar verilmemiş aday varken apply** → **409 `undecided_candidates_remain`**; **hiçbir gün yazılmaz**
- **Maker kendi batch'ini apply eder** → **403 `maker_cannot_be_checker`**
- **Onaylı aday yokken apply** → **409 `nothing_to_apply`**
- **Hedef takvim apply sırasında değişmiş** (eski `Version`) → **409**; **hiçbir gün yazılmaz**
- **`active` hedefe apply, `activate` izni yok** → **403** (D-F10)
- **Terminal batch'e karar/apply/discard** → **409** (`applied`/`discarded`/`failed`)
- **Bilinmeyen ülke kodu** → **400 `country_unknown`**; **hardcoded fallback yok**
- **Set dışı `Decision` değeri** → **400 `unsupported_vocabulary_value`**
- **Aday sayısı sınırı aşıldı** → **409 `provider_payload_too_large`**; **kısmi staging YOK**
- **Yetkisiz aktör** → **403** (dört anahtarın her biri için ayrı test)

**Zamanlanmış koşum (D-F02) — iş asla exception ile ölmez, her durum bir kayda dönüşür:**

- **Uygun aktif takvim yok** → `skipped_no_active_calendar` log; batch **açılmaz**; iş **başarılı** biter
- **Açık/uygulanmış batch var** → `skipped_open_batch` / `skipped_already_applied`; **DUP açılmaz** (V40)
- **Sağlayıcı erişilemez** → o hedef `failed` + `FailureReason`; **diğer hedefler devam eder**; sonraki
  koşumda **yeniden denenir** (V42)
- **`HolidayProvider.Enabled=false`** → `auto_fetch_disabled` log; **hiçbir batch açılmaz** (V43)
- **`MaxBatchesPerRun` aşıldı** → kalanlar **loglanır**, bir sonraki koşuma bırakılır (V39)
- **Cron `TimeZoneId != UTC`** → `BackgroundJobValidationException` ile **kayıt sırasında** patlar (fail-fast)
- **`system:` aktörle apply denemesi** → **403**; sistem kimliği **hiçbir koşulda** checker olamaz (§14.2)

---

## 14. Authorization Convention

**TEK controller, TEK policy — FU01'in iki-controller zorunluluğu burada YOKTUR** (çünkü FU02'nin tenant
yüzeyi yoktur, §2.3/1):

```text
── WorkingCalendarImportsController ────────────────────────────────────────
Policy:     [Authorize(Policy = "PlatformActor")]              // shell: platform-admin
Permission: [HasPermission("platform.working-calendar.auto-fetch.{read|run|review|apply}")]
Actor:      platform_admin

PKS-001: lowercase-dotted, >= 3 segment, kebab-case multiword.
"auto-fetch" tek segmentte kebab'dır; anahtar 5 segmenttir ve ModulePageDescriptorNormalizer'ın
>= 3 segment kuralına uyar (canlı kod: IsCanonicalPermissionKey, parts.Length >= 3).
```

| Permission | Kapsadığı endpoint'ler | Rol |
|---|---|---|
| `platform.working-calendar.auto-fetch.read` | contract · list · get-by-id · provider-status | **Gözlemci** |
| `platform.working-calendar.auto-fetch.run` | **fetch başlat** | **Maker** |
| `platform.working-calendar.auto-fetch.review` | gün-düzeyi karar · toplu fan-out · **discard** | **Reviewer** |
| `platform.working-calendar.auto-fetch.apply` | **apply (merge)** | **Checker** |

**Neden dört anahtar (kullanıcı kilidi #6: "fetch izni ≠ approve/activate izni"):**

- **`.run` ayrıdır** çünkü fetch, dış dünyaya çıkan **tek** aksiyondur; kimin dışarı çıkabildiği ayrı bir
  sorudur.
- **`.review` ayrıdır** çünkü "hangi tatil doğru" bir **içerik** yargısıdır.
- **`.apply` ayrıdır** çünkü canlı takvime yazan **tek** aksiyondur.
- **`.read` ayrıdır** çünkü denetim/gözlem, karar yetkisi gerektirmemelidir.

**Permission SoD yetmez — kimlik SoD'si de zorunludur (§12.5/adım 4).** Aynı kişi hem `.run` hem `.apply`
taşısa bile **kendi başlattığı batch'i apply edemez** (`AppliedBy != RequestedBy`, **403
`maker_cannot_be_checker`**). Bu, MOD-0048'in publish/approve SoD deseninin aynısıdır ve **handler'da**
uygulanır, UI'da gizlemekle yetinilmez.

### 14.2 Zamanlanmış işin yetki modeli (D-F02) — permission YOK, yüzey YOK

Zamanlanmış iş **HTTP'den geçmez**, dolayısıyla `[HasPermission]` **değerlendirilmez**. Bu bir boşluk
**değildir**, çünkü işin yapabildiği tek şey **staging'e yazmaktır** ve staging'in canlı sisteme **hiçbir
etkisi yoktur** (§2.3/2). Yetki modeli üç kuralla kapatılır:

```text
1. İşe permission ATANMAZ; sistem kimliğine rol/anahtar verilmez (RBAC'ta "system" diye bir aktör YOK).
2. İş yalnız StartWorkingCalendarImportCommand'ı çağırır — review/apply komutlarına erişimi YOK (V44).
3. "system:" önekli aktör AppliedBy / DecidedBy alanlarına ASLA yazılamaz → checker HER ZAMAN insandır.
```

**Kural 3 fail-closed bir guard'dır, bir konvansiyon değil:** `Apply` ve `Decide` handler'ları aktörün
`system:` önekiyle başlamadığını **doğrular**; başlıyorsa **403 `system_actor_cannot_decide_or_apply`**.
Böylece bir gelecek FU yanlışlıkla apply'ı bir işten çağırsa bile **SoD delinemez** (AC-SCHED-3).

**SoD zamanlanmış batch'te kendiliğinden korunur:** `RequestedBy = "system:auto-fetch-scheduler"` ve
`AppliedBy` **her zaman** bir insan olduğu için `AppliedBy != RequestedBy` koşulu **yapısal olarak** sağlanır.
Yani otomasyon SoD'yi **zayıflatmaz**, aksine "maker asla checker olamaz" durumunu **kesinleştirir**.

**`active` hedefe apply'ın ek kapısı (D-F10 — KESİNLEŞTİ 2026-08-27):** aktif bir **ülke** takvimine gün eklemek, o ülkedeki **her
tenant'ın** çalışma günü cevabını **anında** değiştirir — yayılma alanı `activate` ile aynıdır. Bu yüzden
`active` hedefe apply, `.apply` **VE** FU01'in mevcut `platform.working-calendar.activate` anahtarını birlikte
ister. `draft` hedefe apply için yalnız `.apply` yeter.

**Bu pack hiçbir permission seed etmez, hiçbir role grant yazmaz.** Dört anahtar katalogda **yoktur**;
endpoint'ler ilk açılışta **403** verecektir — **beklenen** durumdur, **F02-RBAC** ile kapanır. **Fallback
anahtar kullanılmaz**: FU01'in `platform.working-calendar.manage` anahtarına yaslanmak, SoD'yi **doğduğu anda**
yok ederdi.

---

## 15. Gateway / API Routing Decision

**Karar: Gateway değişikliği GEREKMEZ.**

FU01 `/api/platform/working-calendars` **+** `/api/platform/working-calendars/{everything}` route çiftini
zaten gerektirdi (v1 §15 / F-GW). FU02'nin **tüm** yolları `working-calendars/imports…` alt-yoluna oturur ve
bu çiftin **altına düşer** — `TenantReferenceLookupsController`'ın `/api/lookups/reference/…` taktiğinin
**aynısı**, v1'in override endpoint'lerinde kullandığı taktiğin de aynısı.

> **⚠️ Route çakışması — FU01'in `{id:guid}` kısıtı bunu ZATEN çözüyor.** `working-calendars/{id}` kısıtsız
> olsaydı `working-calendars/imports` yolunu **yutardı**. v1 §15 / §19.2/2 tüm `{id}` parametrelerini
> **`{id:guid}`** yazdı ve **canlı kodda öyledir**; `imports` bir GUID olmadığı için doğru controller'a
> gider. **FU02 bu kısıtı korumak zorundadır** — FU01 controller'ında bir `{id}` kısıtı gevşetilirse FU02
> sessizce 403 vermeye başlar (AC-ROUTE-1).

### Zamanlanmış fetch: **Hangfire recurring job**, endpoint DEĞİL (D-F02)

| Soru | Cevap |
|---|---|
| Zamanlama nasıl kurulur? | `PlatformRecurringJobRegistrar`'a **tek additive kayıt** + `HolidayAutoFetchJob` (§4.9). **Kod/config**, HTTP değil |
| "Şimdi çalıştır" endpoint'i var mı? | **YOK ve açılmayacak.** Manuel tetik zaten endpoint 5'tir (`POST /imports`, `auto-fetch.run`); ikinci bir tetik yolu **aynı işi iki farklı yetki modeliyle** yapılabilir kılardı |
| Zamanlama UI'dan değiştirilebilir mi? | **Hayır.** Cadence/yıl offset'i **config**tir; UI **yalnız okur** (endpoint 10) |
| Job durumu nereden görülür? | Hangfire dashboard (MOD-0026, `PlatformActor` korumalı) + `JobExecutionLog` (MOD-0026) + FU02'nin **kendi** okuma ucu (endpoint 10) |
| Endpoint sayısı | **9 → 10**: yalnız **salt-okunur** bir zamanlama durumu ucu eklendi. Mutasyon yüzeyi **büyümedi** |

**Endpoint yüzeyi (10) — tek controller:**

| # | Metot | Route | Permission | Not |
|---|---|---|---|---|
| 1 | GET | `/api/platform/working-calendars/imports/contract` | `auto-fetch.read` | Vokabüler + limitler + `providerEnabled` bayrağı |
| 2 | GET | `/api/platform/working-calendars/imports` | `auto-fetch.read` | Liste (**`Candidates` projeksiyon dışı**, §8.6) |
| 3 | GET | `/api/platform/working-calendars/imports/{id:guid}` | `auto-fetch.read` | Batch + **tüm adaylar** |
| 4 | GET | `/api/platform/working-calendars/imports/providers/status` | `auto-fetch.read` | Aktif adaptör adı, `Enabled`, host — **secret/sorgu dizesi YOK** |
| 4a | GET | `/api/platform/working-calendars/imports/schedule` | `auto-fetch.read` | **YENİ (D-F02)** — salt-okunur zamanlama durumu: `enabled` (üç kapının **üçü**), `cronExpression`, `yearOffsets`, `maxBatchesPerRun`, son N `scheduled` batch'in özeti. **Config'ten ve FU02'nin kendi koleksiyonundan** üretilir; Hangfire storage'a **sorgu atmaz** (MOD-0026 sınırı). **Mutasyon yok** |
| 5 | POST | `/api/platform/working-calendars/imports` | `auto-fetch.run` | **Manuel fetch başlat** → **201** + batch (senkron). Zamanlanmış iş **aynı komutu** çağırır ama bu ucu **kullanmaz** (in-process MediatR) |
| 6 | POST | `/api/platform/working-calendars/imports/{id:guid}/candidates/{candidateId:guid}/decision` | `auto-fetch.review` | Gün-düzeyi karar (**otorite budur**) |
| 7 | POST | `/api/platform/working-calendars/imports/{id:guid}/decisions` | `auto-fetch.review` | Toplu fan-out (`approveAll` / `rejectAll` / filtreli) — **aynı** gün-düzeyi kararları yazar |
| 8 | POST | `/api/platform/working-calendars/imports/{id:guid}/apply` | `auto-fetch.apply` (+ `activate`, D-F10) | **Merge** |
| 9 | POST | `/api/platform/working-calendars/imports/{id:guid}/discard` | `auto-fetch.review` | Terminal ret |

**Metotlar: GET, POST, OPTIONS. DELETE YOKTUR** ve Gateway'de de açılmaz.

> **Manuel fetch neden hâlâ 202 değil 201:** MOD-0028-FU07 `202 Accepted` + polling kullanıyor çünkü orada iş
> **çok fazlı, uzun süren, devam-ettirilebilir** bir workbook import'udur. Burada iş **tek bir HTTP GET + ~30
> satır eşleme**dir; timeout 10 saniyedir ve sonuç **zaten insan onayı bekleyecektir**. Zamanlanmış fetch'in
> v1'e girmiş olması bunu değiştirmez: **asenkronluk zaten job tarafında** çözülüyor, manuel uçta polling
> yüzeyi açmanın **hiçbir kazancı yok** — operatör sonucu **aynı istekte** görüyor.

---

## 16. Acceptance Criteria

> Her madde §17'de **bir teste** eşlenir. Belirsiz ifade (`iyi çalışıyor`, `düzgün`) **yoktur**.

**AC-ID — kimlik hijyeni**

- [ ] **AC-ID-1** `services/`, `frontend/`, `gateway/`, `tests/` altında **`CAND-CAP-0008` ve
      `CAND-CAP-0008-FU02` dizeleri hiç geçmez** — koleksiyon, route, permission, enum, sınıf, dosya adı veya
      **yorum** dâhil.
- [ ] **AC-ID-2** `py .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0008 --name "Working
      Calendar & Public Holidays"` implementasyondan **sonra da exit 0** döner (regression gate).
- [ ] **AC-ID-3** Runtime adları §Kimlik tablosuyla birebir aynıdır (`working_calendar_import_batches` ·
      `/api/platform/working-calendars/imports` · `platform.working-calendar.auto-fetch.*` ·
      `WorkingCalendarImportBatch` · `IHolidayProvider`).

**AC-EGRESS — dış dünya sınırı**

- [ ] **AC-EGRESS-1** Dış HTTP çağrısı **yalnız** `NagerHolidayProvider` içinde yapılır; Application
      katmanında `HttpClient`/`HttpRequestMessage` sembolü **yoktur** (grep ile doğrulanır).
- [ ] **AC-EGRESS-2** `BaseUrl` host'u `AllowedHosts` içinde değilse uygulama **başlamaz** (`ValidateOnStart`);
      test bunu `InvalidOperationException` ile doğrular.
- [ ] **AC-EGRESS-3** Kod tabanında **hardcoded** `date.nager.at` **veya** herhangi bir `?? "https://…"`
      fallback'i **yoktur** (`configuration-safety.md`).
- [ ] **AC-EGRESS-4** `Provider=offline-stub` iken batch `ProviderKey="offline-stub"` ve
      `ProviderEndpoint="offline-stub://…"` taşır; offline veri **gerçek sağlayıcı verisi gibi görünmez**.
- [ ] **AC-EGRESS-5** Adaptörde/config'te **`ApiKey` / `Token` / `Secret` adlı alan yoktur** (grep).
- [ ] **AC-EGRESS-6** `AllowAutoRedirect = false`; farklı host'a redirect **istek iptali** ile sonuçlanır.
- [ ] **AC-EGRESS-7** `Enabled=false` (varsayılan) iken fetch **403** verir ve **batch dokümanı oluşmaz**.

**AC-STAGE — staging izolasyonu**

- [ ] **AC-STAGE-1** Fetch sonrası hedef takvim dokümanı **byte-identical** kalır (`Version` dâhil).
- [ ] **AC-STAGE-2** `IWorkingCalendarProvider`'ın `pending-review` bir batch'ten **haberi yoktur**: aynı
      tarih için `IsWorkingDayAsync` cevabı fetch **öncesi ve sonrası aynıdır**.
- [ ] **AC-STAGE-3** Sağlayıcı erişilemezken batch `failed`, `Candidates` **boş**, `FailureReason` **dolu**;
      hedef takvim **değişmez**.
- [ ] **AC-STAGE-4** `provider_payload_too_large` durumunda **hiç aday yazılmaz** (kısmi staging yok).
- [ ] **AC-STAGE-5** Batch'te **hard delete yoktur**; `discard` terminal bir statüdür ve kayıt **okunabilir kalır**.

**AC-SOD — maker-checker**

- [ ] **AC-SOD-1** Dört permission anahtarının **her biri** ayrı ayrı 403 üretir (dördü için dört test).
- [ ] **AC-SOD-2** `.run` taşıyan ama `.apply` taşımayan aktör apply denerse **403**.
- [ ] **AC-SOD-3** `RequestedBy == aktör` olan apply denemesi, aktör `.apply` taşısa bile **403
      `maker_cannot_be_checker`**; batch `in-review` kalır.
- [ ] **AC-SOD-4** `UndecidedCount > 0` iken apply **409**; **implicit approve yoktur** — bayraklı adaylar
      bile otomatik reddedilmiş sayılmaz.
- [ ] **AC-SOD-5** `active` hedefe apply, `platform.working-calendar.activate` olmadan **403**; `draft` hedefe
      apply aynı aktörle **200** (D-F10 asimetrisi).
- [ ] **AC-SOD-6** Reddedilen aday **silinmez**: `Decision=rejected` + `DecisionReason` + `DecidedBy/At`
      kayıtta kalır ve API yanıtında görünür.

**AC-MERGE — atomik birleştirme**

- [ ] **AC-MERGE-1** N onaylı gün, hedef takvime **TEK** `ReplaceAsync` ile yazılır (repository mock'u
      **bir kez** çağrılır — N kez değil).
- [ ] **AC-MERGE-2** 10 onaylı adaydan biri FU01 validator'ından geçemezse **hiçbiri** yazılmaz (400,
      takvim `Version`'ı **değişmez**).
- [ ] **AC-MERGE-3** Apply sırasında takvim başkası tarafından değiştirilmişse **409**; **hiçbir gün yazılmaz**
      ve batch `in-review` kalır (yeniden apply edilebilir).
- [ ] **AC-MERGE-4** Merge, FU01'in **kendi** `WorkingCalendarValidation.ValidateDayInput`'unu çağırır —
      kural kopyası **yoktur** (grep: ikinci bir gün-validasyon implementasyonu yok).
- [ ] **AC-MERGE-5** Merge, FU01'in `WorkingCalendarWriteGuard`'ını kullanır; `archived` hedefe apply **409**.
- [ ] **AC-MERGE-6** `AppliedDayIds` yazılan **gerçek** `DayId`'leri taşır ve `AppliedDayIds.Count ==`
      gerçekten eklenen gün sayısı (`already-present` no-op'lar **dâhil değildir**).

**AC-IDEMP — idempotent re-fetch (kullanıcı kilidi #5)**

- [ ] **AC-IDEMP-1** Aynı `country/year/target` için apply sonrası ikinci fetch: **tüm** adaylar
      `already-present`; apply **sıfır** gün yazar; takvimin gün sayısı **değişmez**.
- [ ] **AC-IDEMP-2** Takvimde **DUP tarih oluşmaz**: aynı etkin tarihte iki aktif gün **hiçbir senaryoda**
      yaratılmaz (FU01'in `duplicate_day_date` kuralı merge yolunda da **canlıdır**).
- [ ] **AC-IDEMP-3** Elle girilmiş bir gün (`Source=manual`) ile aynı tarihe gelen aday `conflicts-manual`
      olur ve onaylansa bile **no-op**tur — manuel gün **ezilmez**.
- [ ] **AC-IDEMP-4** `date-shift` adayı onaylansa bile mevcut gün **taşınmaz/güncellenmez** (§8.7).
- [ ] **AC-IDEMP-5** Aynı takvim için ikinci **açık** batch **409 `open_batch_exists`**.

**AC-SCHED — zamanlanmış fetch (D-F02) — SoD'yi KORUYAN aile**

- [ ] **AC-SCHED-1** Recurring kayıt `Id` **`Diten.Platform.WorkingCalendar.HolidayAutoFetchJob`**,
      `Owner` **`working-calendar`**'dır; **hiçbir MOD/CAND literali taşımaz** (grep — mevcut kayıtların
      `Diten.Platform.MOD-00xx.…` kalıbı **taklit EDİLMEZ**, §19.2/15).
- [ ] **AC-SCHED-2** **Guard 1 (auto-apply yasağı) makine ile doğrulanır:** `HolidayAutoFetchJob.cs`
      `ApplyWorkingCalendarImportCommand`, `DecideWorkingCalendarImportCandidateCommand`,
      `DecideWorkingCalendarImportBatchCommand`, `DiscardWorkingCalendarImportCommand` sembollerinin
      **hiçbirine referans vermez** (grep) ve `IWorkingCalendarRepository`'ye **yazma** çağrısı yapmaz.
- [ ] **AC-SCHED-3** **Guard 2:** `system:` önekli aktörle apply/decide → **403
      `system_actor_cannot_decide_or_apply`**; `AppliedBy`/`DecidedBy` alanlarında `system:` **hiçbir zaman**
      görünmez. Zamanlanmış bir batch **yalnız** insan checker ile `applied` olur.
- [ ] **AC-SCHED-4** **Guard 3:** iş `CalendarYear ∈ {UtcNow.Year + YearOffsets}` olan **aktif ülke**
      takvimleri için batch açar; `draft` takvim, tenant satırı ve `archived` takvim **hedef seçilmez**.
- [ ] **AC-SCHED-5** **Guard 3 (negatif):** uygun aktif takvim yokken iş **hata vermez**, batch **açmaz**,
      takvim **oluşturmaz** ve `skipped_no_active_calendar` loglar.
- [ ] **AC-SCHED-6** **Guard 4 (idempotency):** aynı `ScheduledRunKey` için `pending-review`/`in-review`
      batch varken ikinci koşum **yeni batch açmaz**; `applied` varken de **açmaz**; `failed`/`discarded`
      varken **açar**.
- [ ] **AC-SCHED-7** **Guard 5 (fail-closed):** sağlayıcı erişilemezken o hedefin batch'i `failed` +
      `FailureReason`, **diğer hedefler işlenmeye devam eder**, hedef takvimlerin **hiçbiri değişmez** ve iş
      **exception fırlatmaz** (MOD-0026 log'una `Succeeded` düşer, içerik başarısızlığı batch'te yaşar).
- [ ] **AC-SCHED-8** **Üç kapı:** `EnabledJobs[...]=false` ⇒ kayıt `IsEnabled=false` · `Schedule.Enabled=false`
      ⇒ kayıt **hiç üretilmez** · `HolidayProvider.Enabled=false` ⇒ iş çalışır, **hiçbir batch açmaz**,
      `auto_fetch_disabled` loglar. **Varsayılan konfigürasyonda üçü de kapalıdır.**
- [ ] **AC-SCHED-9** `TimeZoneId` `"UTC"` dışında verilirse kayıt **`BackgroundJobValidationException`** ile
      **başlangıçta** patlar; cron **>= 5 alan** olarak doğrulanır.
- [ ] **AC-SCHED-10** Zamanlanmış batch `TriggerSource="scheduled"` + `RequestedBy="system:auto-fetch-scheduler"`
      + `ScheduledRunKey` taşır; manuel batch `TriggerSource="manual"` + JWT aktörü taşır ve `ScheduledRunKey`
      **null**'dır. `TriggerSource` **payload'dan okunmaz** (gönderilse bile yok sayılır).
- [ ] **AC-SCHED-11** İş **tenant scope AÇMAZ** (`TenantScope.Begin` referansı **yoktur**); `BackgroundJobContext.TenantId`
      **null**'dır ve yalnız global (ülke) satırlar görülür.
- [ ] **AC-SCHED-12** `MaxBatchesPerRun` aşıldığında kalan hedefler **loglanır** ve bir sonraki koşuma
      bırakılır; **sessizce düşürülmez**.
- [ ] **AC-SCHED-13** `GET /imports/schedule` **salt-okunur**dur, üç kapının durumunu + son `scheduled`
      batch'leri döndürür ve **Hangfire storage'a sorgu atmaz** (MOD-0026 sınırı); **"şimdi çalıştır" ucu
      kod tabanında yoktur** (grep).
- [ ] **AC-SCHED-14** Koşum başlangıcı/bitişi/hatası **MOD-0026'nın** `IJobExecutionLogWriter`'ı üzerinden
      `JobExecutionLog`'a düşer; FU02 **ikinci bir çalışma günlüğü koleksiyonu açmaz**.

**AC-MAP — sağlayıcı eşlemesi (D-F04 / D-F06)**

- [ ] **AC-MAP-1** `types: ["Public"]` → `MappedDayType = public-holiday`.
- [ ] **AC-MAP-2** `types: ["Bank"]` + `IncludeNonPublicTypes=false` → aday **üretilmez**; sayaç gösterilir.
- [ ] **AC-MAP-3** `types: ["Bank"]` + `IncludeNonPublicTypes=true` → aday üretilir, `MappedDayType=null`,
      `Flags: type_not_public, unmapped_type`, `Decision=undecided`.
- [ ] **AC-MAP-4** **`religious-holiday` / `moveable-holiday` hiçbir adaya otomatik atanmaz** (grep + test).
- [ ] **AC-MAP-5** `Recurrence` **her zaman `none`**; sağlayıcının `fixed` alanı **hiç okunmaz**.
- [ ] **AC-MAP-6** `ObservedDate` **her zaman `null`**; sağlayıcı `date`'inden **türetilmez**.
- [ ] **AC-MAP-7** `global=false` / `counties` dolu → `Flags: subdivision_scoped`.
- [ ] **AC-MAP-8** `MappedDayName = localName ?? name`; ikisi de boşsa aday `flagged` ve **isimsiz gün yazılmaz**.
- [ ] **AC-MAP-9** Bilinmeyen sağlayıcı alanları **yok sayılır** — şema genişlemesi batch'i `failed` yapmaz.

**AC-BOUNDARY — FU01 dokunulmazlığı**

- [ ] **AC-BOUNDARY-1** `Features/WorkingCalendar/Provider/**` **git diff ∅** — seam imzası, çözümleme sırası,
      `Resolution`/`ReasonCodes` kümeleri **değişmez**.
- [ ] **AC-BOUNDARY-2** FU01'in 42 testi **değişmeden** yeşil kalır (regression).
- [ ] **AC-BOUNDARY-3** `WorkingCalendar.cs`'te **yalnız 3 additive alan** eklenmiştir; mevcut hiçbir alan/metot
      değişmemiştir (diff satır bazında doğrulanır).
- [ ] **AC-BOUNDARY-4** `WorkingCalendarPermissions.All` **beş** anahtarda kalır; FU02 anahtarları **ayrı** bir
      sabit sınıfındadır.
- [ ] **AC-BOUNDARY-5** FU02 **hiçbir takvim oluşturmaz**, **activate/archive etmez**, **`WeekendDays`'e
      dokunmaz**, **gün silmez/arşivlemez** (grep + test).
- [ ] **AC-BOUNDARY-6** Tenant satırı (`TenantId != null`) hedef olarak verilirse **404**; kod tabanında FU02'ye
      ait bir tenant controller'ı/view'ı **yoktur**.
- [ ] **AC-BOUNDARY-7** *(D-F02 ile TERSİNE ÇEVRİLDİ)* MOD-0026 **yalnız zamanlama için** tüketilir:
      `Diten.Building.Blocks/**` ve `Infrastructure/BackgroundJobs/**` → **git diff ∅**;
      `PlatformRecurringJobRegistrar.cs`'te değişiklik **tek bir additive kayıt satırıyla sınırlıdır**
      (mevcut 9 kaydın hiçbiri değişmez); FU02 **kendi scheduler'ını, kendi kilidini, kendi
      `JobExecutionLog`'unu yazmaz**.
- [ ] **AC-BOUNDARY-8** FU01 contract'ının `Limitations` listesindeki *"External holiday auto-fetch is not
      implemented; source 'provider-fetch' is rejected."* satırı **gerçeğe uygun** hâle getirilir; contract
      `contractVersion` **bump** edilir ve UI hardcoded liste **kullanmaz**.

**AC-UI — Platform Admin Slim konsolu**

- [ ] **AC-UI-1** `Views/Platform/WorkingCalendarImports/*.cshtml` **tümünde** `Layout = "_LayoutPlatformAdmin"`
      **açıkça** yazılı.
- [ ] **AC-UI-2** Klasörde §11.2'deki **7 kanonik Slim dosyası + `Review.cshtml`** vardır; `Create.cshtml`,
      `Edit.cshtml`, `Details.cshtml`, `_Form.cshtml` **yoktur**.
- [ ] **AC-UI-3** `_DataTable.cshtml` `data-dt-standard="v2"` + skeleton taşır; **Index'te tek** DataTable,
      **Review'da tek** grid → `updateVisualState` global selector çakışması **yok**.
- [ ] **AC-UI-4** Tüm dropdown/rozet değerleri `contract`'tan, ülke listesi `/api/lookups/countries`'ten gelir;
      **hardcoded liste yok**.
- [ ] **AC-UI-5** Hedef takvim seçici **yalnız** `ScopeType=country` + aynı ülke/yıl + `draft|active` takvimleri
      listeler; uygun takvim yoksa **açık bir mesaj** gösterir ("önce takvimi oluşturun") — sessiz boş liste değil.
- [ ] **AC-UI-6** Review ekranı her aday için **`ChangeKind` + `Flags`**'i **adıyla** gösterir; `already-present`
      ve `conflicts-manual` adaylarında onay kutusu **no-op olduğu açıkça yazılarak** sunulur.
- [ ] **AC-UI-7** `approveAll` **karar verilmemiş** adayları onaylar; **bayraklı** adayları **atlar** ve kaç
      tanesinin atlandığını **söyler** (sessiz toplu onay yok).
- [ ] **AC-UI-8** Apply butonu `UndecidedCount > 0` iken **disabled** ve **nedeni** yazılı; buton gizlenmez.
- [ ] **AC-UI-9** 400/409/403 yanıtları kullanıcıya **birebir** gösterilir (uydurma/parafraz yok — v1 §19.6 kuralı).
- [ ] **AC-UI-10** Browser JS **5057**'yi çağırmaz; yalnız same-origin proxy kullanır.
- [ ] **AC-UI-11** `.resx` **en + tr** paritesi tam (platform shell standardı — 7 dil **değil**);
      `window.L10n` anahtarları `undefined` dönmez (camelCase→PascalCase köprüsü **atlanmaz**).
- [ ] **AC-UI-12** Provenance paneli (sağlayıcı, endpoint, zaman, hash) QuickView'da **görünür** — operatör
      "bu veri nereden geldi" sorusunu UI'dan cevaplayabilir.

**AC-ROUTE / AC-MANIFEST**

- [ ] **AC-ROUTE-1** FU01 controller ①'in `{id:guid}` kısıtı **korunur**; `GET /working-calendars/imports`
      isteği FU02 controller'ına gider (regression testi).
- [ ] **AC-ROUTE-2** `ocelot.json` **değişmez** (git diff ∅) ve endpoint Gateway üzerinden **200** döner
      (FU01'in route çifti eklendikten sonra).
- [ ] **AC-MANIFEST-1** `WorkingCalendarImportManifestProvider` sayfa(lar)ı ve aksiyonları **gerçek permission
      sabitleriyle** beyan eder; `RoutePath` `/Platform/…` altında olduğu için scope **PlatformAdmin** çıkar.

---

## 17. Test Expectations

**17.1 Backend unit/integration — hedef ≥ 52 test** (D-F02 ile +12)

| Küme | Kapsam |
|---|---|
| 1. Fetch validasyonu | V1–V10'un her biri için pozitif + negatif |
| 2. Sağlayıcı outcome'ları | Dört `HolidayProviderOutcome` değeri + timeout + bozuk JSON + ülke uyuşmazlığı |
| 3. **Egress** | Allowlist ihlali (startup fail) · redirect iptali · `Enabled=false` · offline stub provenance'ı |
| 4. Mapping | V11–V19 + AC-MAP-1…9 (özellikle **religious/moveable asla atanmaz** ve **ObservedDate null**) |
| 5. Diff / `ChangeKind` | V20–V23'ün dördü + boş takvim + dolu takvim + arşivli günün **görmezden gelinmesi** |
| 6. **İdempotency** | AC-IDEMP-1…5 — çift fetch, çift apply, manuel çakışma, tarih kayması, açık batch |
| 7. State machine | §12.3'ün her geçişi + **her yasak geçiş** (özellikle `pending-review → applied`) |
| 8. **SoD** | AC-SOD-1…6 — dört anahtar × 403, maker=checker, undecided, `active` hedef ek kapısı |
| 9. **Merge atomikliği** | AC-MERGE-1…6 — tek replace (mock çağrı sayısı), all-or-nothing, 409, guard/validator yeniden kullanımı |
| 10. Concurrency | Batch `Version` + takvim `Version` **ayrı ayrı**; apply sırasında drift |
| 11. FU01 dokunulmazlığı | AC-BOUNDARY-1…8 + FU01'in mevcut 42 testinin **değişmeden** yeşil kalması |
| 12. Contract | FU02 contract alanları + FU01 contract `Limitations`/`contractVersion` güncellemesi |
| 13. Repository/persistence | Class-map (iki yeni tip) · index'ler · `$ne`'siz partial filter · `Candidates` projeksiyonu · `ScheduledRunKey` sorgusu |
| **14. Zamanlanmış iş (D-F02)** | **AC-SCHED-1…14** — job id/owner nötrlüğü · **auto-apply yasağının grep testi** · sistem aktörü apply/decide 403 · hedef seçimi (aktif/draft/tenant/archived) · idempotency dört hâli (`pending`/`in-review`/`applied`/`failed`) · sağlayıcı hatasında **diğer hedeflerin devam etmesi** · üç kapı matrisi · UTC/cron doğrulaması · `TenantScope` yokluğu · `MaxBatchesPerRun` taşması · `JobExecutionLog` yazımı |
| **15. Registrar regresyonu** | `PlatformRecurringJobRegistrar` **10 kayıt** döndürür (9 mevcut **değişmeden** + 1 yeni); mevcut kayıtların id/cron/owner değerleri **birebir aynı** kalır |

**17.2 Frontend / smoke**

- Authenticated smoke — **tek oturum (platform_admin), ≥ 24 adım**:
  fetch (offline stub) → batch `pending-review` → **takvim byte-identical** doğrulaması → aday listesi →
  gün-düzeyi approve × 3 → gün-düzeyi reject × 1 (+gerekçe) → `approveAll` (bayraklıları **atladığını**
  doğrula) → undecided varken apply **409** → maker=checker apply **403** → ikinci aktörle apply **201** →
  takvimde günlerin görünmesi → **`resolve` çağrısının artık tatil demesi** → **ikinci fetch** →
  tüm adaylar `already-present` → apply **0 gün** → DUP **yok** → `Enabled=false` ile fetch **403** →
  sağlayıcı erişilemez senaryosu (stub'ı `provider_unavailable` moduna al) → batch `failed`, takvim **değişmez**.
- **Zamanlanmış koşum smoke'u (D-F02, ≥ 8 adım):** üç kapı açılır (`EnabledJobs`, `Schedule.Enabled`,
  `HolidayProvider.Enabled`) → iş **elle bir kez tetiklenir** (Hangfire dashboard "Trigger now" — **uygulama
  kodunda böyle bir uç yoktur**, MOD-0026 dashboard'u kullanılır) → aktif ülke takvimi başına **bir**
  `scheduled` batch oluşur → batch `TriggerSource=scheduled`, `RequestedBy=system:auto-fetch-scheduler`,
  `ImportStatus=pending-review` → **hedef takvim byte-identical** → **ikinci tetik DUP üretmez** (V40) →
  insan checker apply eder → `applied` + `AppliedBy` **insan** → `Schedule.Enabled=false` ile tetik
  **hiçbir batch üretmez**.
- **PowerShell 5.1 tuzağı:** `@(... | Where-Object ...).Count` sarmalaması **zorunlu**; `Add-Result`
  çağrıları script yazıldıktan sonra **gerçekten çalıştırılarak** doğrulanır (MOD-0162-FU04 dersi).
- **Orkestratör self-report'una güvenilmez:** verifier/test sayıları **kendi koşumdan** okunur.

**17.3 Quality gates — beklenen sonuçlar KOŞUMDAN ÖNCE ilan edilir**

| Gate | Beklenti |
|---|---|
| Build | `Diten.Platform.{Domain,Application,Infrastructure,API}` + `Diten.Web` → **0 hata** |
| `verify_module_id.py --candidate CAND-CAP-0008` | **exit 0** (AC-ID-2) |
| `CAND-CAP-0008` / `CAND-CAP-0008-FU02` grep | `services/ frontend/ gateway/ tests/` → **0 hit** (AC-ID-1) |
| `verify_datatable_page.py` | **TEK koşu** — `--area Platform --module WorkingCalendarImports`, **Slim** referansına karşı. **Beklenen FAIL'ler (önceden ilan):** ① `personalizationClient sends tenant header only for tenant users` (**repo geneli borç** — Golden Reference'ın kendisi de kırmızı) ② `dt-checkboxes-select-all` ③ `bulkOptions/bulkBarSelector` ④ `getSelectedIds/onBulkAction` ⑤ `.../bulk` endpoint ⑥ bulk-delete tetikleyicisi ⑦ `clearSelection` — **②–⑦ EXPECTED N/A**: modülde silme/bulk **yoktur** (§10 sapma 1), v1'in 7 FAIL'iyle **aynı aile**. `Review.cshtml`'in fazladan dosya olması **FAIL üretmez** (eksiklik değil, fazlalık) |
| `quality-gate-datatable` | PASS |
| RESX parite | **en + tr** × `WorkingCalendarImportsIndex` + `SharedResource.WorkingCalendarImportsMenu` — anahtar kümesi farkı **0** |
| Boundary diff | `Features/WorkingCalendar/Provider/**`, `Features/Lookups/**`, `Diten.Platform.Common/**`, `Diten.Building.Blocks/**`, `Infrastructure/BackgroundJobs/**`, `gateway/**` → **git diff ∅** |
| **Registrar diff** | `PlatformRecurringJobRegistrar.cs` → **yalnız additive**: +1 kayıt satırı (+ yardımcı metot), mevcut 9 kaydın **hiçbir alanı değişmemiş** (AC-BOUNDARY-7) |
| **Job id grep** | `services/` altında `Diten.Platform.CAND-CAP` / `Diten.Platform.MOD-….HolidayAutoFetch` → **0 hit**; yalnız `Diten.Platform.WorkingCalendar.HolidayAutoFetchJob` (AC-SCHED-1) |
| FU01 regresyon | FU01'in 42 testi **değişmeden** yeşil |

**17.4 Endpoint'ler fleet restart'a kadar 404'tür**; `.resx` değişiklikleri **tam restart** ister.

---

## 18. Ready-for-dev Checklist

- [x] Parent DCP-002 candidate kapısı **PASS** (`CAND-CAP-0008`, exit 0, 2026-08-27) — çıktı §Kimlik'te birebir
- [x] FU id kapısının **BLOCKED** olduğu **gizlenmeden** raporlandı ve **D-F11**'e bağlandı
- [x] Runtime nötr adları sabitlendi (`working-calendar-import` ailesi); AC-ID-1/2 makine-doğrulanabilir
- [x] Zorunlu bağlam okundu: `AGENTS.md` · PSS `domain-config.md` · `module-pack-standard.md` ·
      `permission-key-standard.md` · `configuration-safety.md` · `module-self-registration-standard.md` ·
      **FU01 pack'i (1624 satır, tamamı)** · **FU01 canlı kodu** (entity, permissions, upsert handler, write
      guard, contract builder, controller'lar) · MOD-0026 pack'i + **canlı Hangfire altyapısı** ·
      MOD-0028-FU07 (async import emsali) · Golden Reference Slim
- [x] Golden reference kararı **türetilerek gösterildi** (5 alan ⇒ slim, §11.1) ve v1'den farkı gerekçelendirildi
- [x] Gateway kararı **kod üzerinden** doğrulandı: yeni route **gerekmiyor** (§15)
- [x] Beklenen verifier FAIL'leri **koşumdan önce** ilan edildi (§17.3)
- [x] Sağlayıcı sözleşmesi (Nager v3 alanları) ve eşleme kuralları **alan alan** yazıldı (§4.6)
- [x] SoD **iki katmanlı** tanımlandı: permission + kimlik (maker ≠ checker)
- [x] Fail-closed davranış **her başarısızlık yolu için** yazıldı (§13)
- [x] **12 D-kararının TAMAMI kapandı** (§19.1 — 2026-08-27 kullanıcı kararları)
- [x] **D-F02 zamanlanmış fetch** MOD-0026'nın **canlı deseni** üzerinden tasarlandı (registrar, descriptor,
      handler imzası, `EnabledJobs` kapısı, `IJobExecutionLogWriter`) ve **altı kilitli guard** §4.9'a işlendi
- [x] Job kimliğinin **nötr slug** taşıması gerektiği tespit edildi ve AC-SCHED-1 ile makine-doğrulanabilir
      yapıldı (mevcut kayıtların `MOD-00xx` kalıbı DCP-002 kapısını kırardı)
- [ ] `status` → `approved`/`ready-for-dev` **ve** `runtime_code_allowed` → `true` flip'i ← **AÇIK (tek kalan kapı)**
- [ ] F02-RBAC (4 anahtar katalog + grant) planlandı ← **AÇIK, blocker değil** (403 beklenen durumdur)
- [ ] Deploy ortamında `date.nager.at` egress'inin (firewall/proxy) açık olduğu doğrulandı ← **AÇIK, ops işi**

---

## 19. Implementation Notes

### 19.1 KARARLAR (D-F01 … D-F12) — **12'sinin TAMAMI KAPALI** (2026-08-27)

> Kararlar kullanıcı tarafından verilmiştir; **gerekçeler ve reddedilen alternatifler korunmuştur** — bir
> karar ileride yeniden açılırsa neyin neden elendiği görünsün diye.

| # | Karar | **Durum / verilen karar** | Gerekçe / reddedilen alternatif |
|---|---|---|---|
| **D-F01** | **Staging aggregate şekli:** batch mi, gün-koleksiyonu mu? | ✅ **KAPALI — BATCH aggregate + GÖMÜLÜ aday listesi** (`GlobalEntity`, `working_calendar_import_batches`) | Batch, **provenance'ın doğal birimidir**: bir sağlayıcı çağrısı = bir kanıt kaydı (endpoint, zaman, hash, outcome). Adaylar gömülü olunca FU01/D3 deseni tekrarlanır: **tek `Version` token'ı, tek doküman, ikinci repository yok**; bir batch en fazla 400 aday taşır (§8.6). **Reddedilen:** düz "aday günler koleksiyonu" — batch olmadan "bu 30 gün hangi çağrıdan geldi, hangileri birlikte onaylandı" sorusu cevapsız kalır ve fail-closed'un birimi kaybolur. **Reddedilen:** batch + ayrı aday koleksiyonu — iki repository, iki concurrency token, standalone Mongo'da transaction ihtiyacı (CRM dersi) |
| **D-F02** | **Zamanlanmış fetch v1'de mi?** | ✅ **KAPALI — MANUEL + ZAMANLANMIŞ** (kullanıcı kararı 2026-08-27). Yıllık Hangfire recurring job **v1 kapsamında**; iş **yalnız fetch→staging** yapar | **Author'ın erteleme önerisi kullanıcı tarafından reddedildi** — ve önerinin dayandığı üç endişenin üçü de tasarımla kapatıldı: **(a) maker kimliği:** `RequestedBy = "system:auto-fetch-scheduler"` + `system:` önekinin `AppliedBy`/`DecidedBy` olamaması kuralı (§14.2) SoD'yi **zayıflatmıyor, kesinleştiriyor** — maker **hiçbir zaman** checker olamaz; **(b) tenant bağlamı:** v1 §19.2/11'in "arka plan işi yalnız global satırları görür" uyarısı bu iş için **risk değil, doğruluk özelliği** — iş **zaten yalnız** ülke katmanına bakıyor (§4.9); **(c) MOD-0026 gate ailesi:** lease/heartbeat/retry **MOD-0026'nın kendi sorumluluğu**, FU02 yalnız **bir kayıt** ekliyor ve `IJobExecutionLogWriter`'ı çağırıyor — kendi scheduler'ını yazmıyor. **Korunan kısıt:** zamanlanmış fetch **hiçbir şeye karar vermez**; ürünü hâlâ `pending-review` bir kuyruktur (guard 1). **Reddedilen alternatif:** "job apply'a kadar gitsin" — SoD'yi tamamen ortadan kaldırırdı. **Kabul edilen sınırlama:** `applied ⇒ skip` (V41) yüzünden yıl-içi tatil değişikliğini job yakalamaz → manuel re-fetch, **F02-SCHED-REFRESH** |
| **D-F03** | **Review granülerliği:** batch mi, gün mü, ikisi mi? | ✅ **KAPALI — GÜN düzeyi otoriter + batch düzeyi FAN-OUT kolaylığı** | Otorite gün düzeyindedir: her aday kendi `Decision`/`DecidedBy`/`DecidedAt`'ini taşır. `approveAll`/`rejectAll` **ayrı bir statü değildir** — aynı gün-düzeyi kararları **yazan** bir yardımcıdır (§15/endpoint 7) ve **bayraklı adayları atlar** (AC-UI-7). **Reddedilen:** yalnız batch düzeyi onay — 30 günün 29'u doğru, 1'i yanlışsa operatör ya hepsini reddeder ya yanlış günü canlıya sokar. **Reddedilen:** ayrı bir "batch approved" bayrağı — gün kararlarını **atlayan** ikinci bir yol doğurur ve implicit approve kapısını açar |
| **D-F04** | **Nager `types` → `WorkingCalendarDayType` eşlemesi** | ✅ **KAPALI (ONAYLANDI) — yalnız `type=Public` → `public-holiday`; başka hiçbir otomatik eşleme YOK** | Sağlayıcı `Public`/`Bank`/`School`/`Authorities`/`Optional`/`Observance` verir; bunların **hiçbiri** "dinî" veya "hareketli" demez. `religious-holiday`/`moveable-holiday` atamak **veri uydurmaktır** — hicri takvim ve ülkeye göre ilan farkı yüzünden **sessizce yanlış** üretir (FU01/D6'nın tam olarak kaçındığı şey). `Public` dışı tipler `IncludeNonPublicTypes` kapalıyken **hiç staging'e girmez**, açıkken **bayraklı ve karar-bekleyen** girer. **Reddedilen:** `Bank`→`public-holiday` — banka tatilini herkes için çalışma-günü-değil saymak **yanlış bir çalışma-günü cevabı** üretir |
| **D-F05** | **Çok-yıl / çok-ülke tek batch mi?** | ✅ **KAPALI — HAYIR; bir batch = bir (ülke, yıl, hedef takvim)** | Batch, **fail-closed'un birimidir**: "sağlayıcı erişilemez ⇒ batch failed" cümlesi ancak batch **tek bir çağrıya** karşılık gelirse anlamlıdır. Çok-yıllı batch'te TR-2027 başarılı, TR-2028 başarısız olduğunda batch **ne olur?** — bu soru kısmi-başarı semantiği doğurur ve kullanıcı kilidi #5'i bulanıklaştırır. Ayrıca **hedef takvim yıl bazlıdır** (FU01/D1), yani çok-yıllı batch'in **tek bir hedefi olamaz**. **UI çözümü:** "2027, 2028, 2029" seçilirse UI **üç ayrı batch** açar ve üçünü listede yan yana gösterir. **Reddedilen:** tek batch + çoklu hedef — merge atomikliği (D-F08) **birden fazla dokümanı** kapsamak zorunda kalırdı; standalone Mongo'da bu **garanti edilemez** |
| **D-F06** | **`ObservedDate` haritalaması** | ✅ **KAPALI — HER ZAMAN `null`** | Nager v3'te **"observed" diye bir alan yoktur**; kaydırılan tatili çoğu ülkede *ayrı bir kayıt* olarak döner. `Date`'ten `ObservedDate` türetmek (ör. "Pazar'a denk gelirse Pazartesi'ye kaydır") **ülkeye özgü bir kuralı uydurmaktır** ve FU01 provider'ı `ObservedDate ?? Date` ile **doğrudan** çalışma-günü cevabını değiştirir — yani uydurma **anında yanlış cevaba** dönüşür. Operatör gerekirse merge sonrası FU01 gün editöründen doldurur. **Reddedilen:** hafta sonu kaydırma kuralı — hangi ülkenin kaydırdığı sağlayıcıdan **öğrenilemez** |
| **D-F07** | **`Source` provenance nerede yaşar?** | ✅ **KAPALI (ONAYLANDI 2026-08-27) — GÜN düzeyinde yeni alanlar** (additive, nullable) (`WorkingCalendarDay.Source` = `manual` varsayılan, `.ProviderBatchId`, `.ProviderRef`) **+** merge sonrası takvim kökünün `Source`'u `provider-fetch` olur | **Bulgu (kod okuması):** `WorkingCalendarDay`'de **`Source` alanı YOKTUR** — `Source` yalnız **takvim kökündedir**. Yani kullanıcı kilidi #4'ün ("gömülü gün listesine `Source="provider-fetch"` ile merge") **bugünkü şemada karşılığı yoktur**; bir karar gerekir. Gün düzeyi provenance **fonksiyonel olarak zorunludur**: D-F04/V22'nin "manuel günü asla ezme" kuralı, bir günün manuel mi provider mı olduğunu **bilmeyi** gerektirir; kök `Source`, karışık içerikli bir takvimde bu soruyu **cevaplayamaz**. Kök `Source = provider-fetch` ise v1'in *"üreticisi FU02"* sözünü yerine getirir ve **"bu takvimde sağlayıcı içeriği var"** anlamına gelir (bir kez set edilir, geri alınmaz). **Alternatif (D-F07 reddedilirse):** provenance yalnız batch'te yaşar (`AppliedDayIds` ile geriye izlenir); bedeli: `conflicts-manual` tespiti **her seferinde tüm batch geçmişini taramak** zorunda kalır ve `WorkingCalendar.cs`'e hiç dokunulmaz |
| **D-F08** | **Merge nasıl yazılır?** (kullanıcı kilidi #4 ile #5 arasındaki gerilim) | ✅ **KAPALI (ONAYLANDI 2026-08-27) — onaylı günler bellekte uygulanır + TEK `ReplaceAsync(calendar, expectedVersion)`; DÖNGÜ YOK** — FU01'in **aynı** write guard'ı ve **aynı** validator'ıyla | **Dürüst uyarı:** kilit #4 *"FU01 write path'i üzerinden yazar, yeni gün-yazma yolu icat etme"* der; kilit #5 *"KISMİ merge YOK"* der. `UpsertWorkingCalendarDayCommand` **gün başına bir tam-doküman replace** yapar (canlı kod) — 15 günü döngüyle yazmak **15 ayrı replace** demektir ve 8'incide 409 alınırsa takvim **yarı dolu** kalır: **kilit #5 ihlal edilir**. Önerim bu gerilimi şöyle çözer: merge, FU01'in **guard'ını** (`WorkingCalendarWriteGuard.LoadWritableAsync`), **validator'ını** (`WorkingCalendarValidation.ValidateDayInput`) ve **repository yazımını** (`ReplaceAsync` + `expectedVersion`) **birebir** kullanır; **tek fark** N günün **tek dokümanda** uygulanmasıdır. Yani **yeni bir kural, yeni bir guard, yeni bir repository, pozisyonel dizi güncellemesi yoktur** — kilit #4'ün korumak istediği şey (bypass edilen kural) **korunur**, kilit #5'in istediği atomiklik **sağlanır** (tek doküman replace'i Mongo'da atomiktir). **Reddedilen:** per-day komut döngüsü (kısmi merge) · **Reddedilen:** `$set: days.$[…]` pozisyonel güncelleme (v1 §8.4 **açıkça yasaklıyor**) |
| **D-F09** | **Hedef takvimi FU02 oluşturabilir mi?** | ✅ **KAPALI — HAYIR; hedef önceden var olmalı** (`country` scope, aynı ülke/yıl; manuel `draft\|active`, **zamanlanmış yalnız `active`** — §4.9/guard 3) | Takvim **oluşturmak** FU01'in işidir ve `CalendarCode`, `WeekendDays`, `CalendarName` gibi **FU02'nin bilmediği** kararlar içerir (özellikle `WeekendDays` — sağlayıcı hafta sonu bilgisi **vermez**; FU01 activate'i `weekend_days_required` ile zaten reddeder). FU02'nin oluşturduğu bir takvim ya eksik olurdu ya da hafta sonu **uydururdu**. **UI karşılığı:** uygun hedef yoksa ekran *"önce bu ülke/yıl için takvim oluşturun"* der ve FU01 sayfasına **link verir** (AC-UI-5). **Reddedilen:** otomatik draft takvim oluşturma |
| **D-F10** | **`active` hedefe apply ek kapı ister mi?** | ✅ **KAPALI (ONAYLANDI 2026-08-27) — `.apply` + `platform.working-calendar.activate`** | Aktif bir **ülke** takvimine gün eklemek, o ülkedeki **her tenant'ın** cevabını **anında** değiştirir. v1 §14 `activate`'i tam olarak bu yayılma alanı için ayrı bir SoD anahtarı yaptı; aynı yayılma alanına **arka kapıdan** (apply) ulaşmak, o kararı **etkisiz** kılardı. `draft` hedefte kapı yoktur (yayılma alanı sıfır). **Reddedilen:** tek `.apply` anahtarı — SoD'yi FU02 üzerinden delerdi |
| **D-F11** | **FU kimliği ve registry satırı** | ✅ **KAPALI (ONAYLANDI 2026-08-27) — parent kapısına dayanılır** (`CAND-CAP-0008`, exit 0); FU02 için **registry satırı AÇILMAZ** | `verify_module_id.py` FU sonekini formatça kabul eder ama **kendi registry + ledger satırını** arar; `CAND-CAP-0008-FU02` için ikisi de yok ⇒ **BLOCKED (exit 2)**, çıktı §Kimlik'te birebir. Repo emsali **açık**: `CAND-CAP-0002-FU01…FU05` ve `CAND-CAP-0003-FU01/FU02` pack'leri **var**, ama registry yalnız parent satırlarını taşıyor. **Öneri:** FU02 yeni kimlik **mint etmez**; kapı **parent** id ile koşulur ve pack `parent: CAND-CAP-0008` frontmatter alanıyla bağını beyan eder. **Alternatif (istenirse):** registry + reconciliation ledger'a FU02 satırı eklenir ve kapı FU id ile de exit 0 verir — bu **registry write**tir, protected path'tir ve **ayrı kullanıcı onayı** ister → **F02-REG** |
| **D-F12** | **UI: Slim + ayrı `Review.cshtml`** | ✅ **KAPALI — EVET** (Slim, 5 alan) **+ beyan edilmiş `Review.cshtml`** | Kural mekaniktir: 5 ≤ 8 ⇒ slim. Aday review grid'i bir **form** değil bir **karar yüzeyidir** ve QuickView offcanvas'a sığmaz (§11.2 gerekçesi); Index'e ikinci DataTable koymak **MOD-0162-FU03 hibrit hatasıdır**. **Alternatif:** review'ı QuickView'a sıkıştırmak (kanonik set korunur, karar kalitesi düşer) veya modülü Compact'a çıkarmak (alan uydurmak = gaming) |

### 19.2 Bu FU'yu doğrudan vuran tuzaklar

1. **Kilit #4 ile kilit #5'in gerilimi (D-F08, KAPALI).** Per-day komut döngüsü **kısmi merge** üretir;
   uygulama **tek `ReplaceAsync`** ile yazmak zorundadır. Kod incelemesinde ilk bakılacak yer burasıdır.
2. **`WorkingCalendarDay`'de `Source` alanı YOK (D-F07, KAPALI ⇒ eklenecek).** Alan **additive ve nullable**
   eklenir (`Source` varsayılan `manual`); kök `Source` **ezilmez**, merge sonrası `provider-fetch` olur.
3. **Class-map iki yeni tip için de gerekir.** `WorkingCalendarImportBatch` **ve** gömülü
   `WorkingCalendarImportCandidate` `DependencyInjection.cs`'e kaydedilmezse `Guid` alanları binary yazılır ve
   filtreler **sessizce boş döner** (v1 §19.2/10; MOD-0151-FU05 dersi).
4. **Partial index `$ne` yasak.** `Filter.Ne(x, null)` içeren partial index Platform'u **crash-loop**'a sokar
   (bu servisin **kendi** dersi). Küme pozitif `$in` / `Filter.Type` ile yazılır.
5. **İki `DateTimeOffset` alanını birlikte sort/index etme.** Liste sıralaması **tek** alan (`RequestedAt`)
   üzerindedir; `ProviderFetchedAt` ile birlikte sort **500 üretir** (parallel-arrays dersi).
6. **`DateOnly` + Mongo serializer.** Aday `Date` alanı `DateOnly`'dir; serializer kaydı FU01'de yapıldı,
   FU02'de **yeniden yapılmasına gerek yoktur** ama kaldırılmadığı **doğrulanmalıdır**.
7. **`{id:guid}` kısıtı FU02'nin yaşam sigortasıdır** (§15). FU01 controller'ında gevşetilirse FU02
   **sessizce 403** verir ve hata **yetkilendirme arızası gibi görünür**.
8. **`HttpClient` timeout'u `TimeoutSeconds`'tan gelmeli.** `HttpClient.Timeout` varsayılanı 100 saniyedir;
   config'e bağlanmazsa asılı bir bağlantı isteği **100 saniye** bloklar ve senkron fetch (D-F02) kabul
   edilemez hâle gelir.
9. **Sağlayıcı boş dizi ≠ hata.** `fetched` + 0 aday geçerli bir sonuçtur (ör. desteklenen ama tatili
   listelenmemiş bir yıl). **`failed` yapmak yanlıştır**; ama **hiçbir gün silinmez** (§8.7) — bu iki kural
   birlikte okunmalıdır.
10. **`already-present` no-op'u sayaçlara yansımalı.** `AppliedDayIds.Count`, `ApprovedCount`'tan **küçük
    olabilir** ve bu **normaldir**; UI bunu "3 gün eklendi, 5 gün zaten vardı" diye **açıkça** söylemelidir.
11. **L10n bridge.** `index.l10n.js` camelCase→PascalCase dönüşümü atlanırsa `window.L10n` anahtarları
    `undefined` döner (toast `"(undefined: corrId)"`).
12. **RBAC ilk açılışta 403.** Dört anahtar katalogda yok; **fallback kullanılmadığı** için endpoint'ler
    F02-RBAC tamamlanana kadar 403 verir. **Beklenen** durumdur, bug değildir.
13. **`rolePermissions` el ile yazılmaz.** Yanlış GUID subtype'ı **tüm tenant login'lerini kırar**.
14. **Fleet restart.** Yeni endpoint'ler ve `.resx` değişiklikleri **tam restart** ister; restart öncesi 404
    bir arıza **değildir**.
15. **🔴 Job id/owner'a MOD literali yazmak DCP-002 kapısını kırar (D-F02).** `PlatformRecurringJobRegistrar`
    içindeki **dokuz kaydın hepsi** `Diten.Platform.MOD-0027.…` / `MOD-0023` / `MOD-0033` kalıbını taşıyor;
    kalıbı taklit edip `CAND-CAP-0008` yazmak `runtime_hits` taramasını **kalıcı olarak** kırmızıya çevirir.
    Nötr slug **zorunludur**: `Diten.Platform.WorkingCalendar.HolidayAutoFetchJob` / `Owner="working-calendar"`.
16. **Cron UTC dışında olamaz.** `BackgroundJobDescriptor.Validate` `TimeZoneId != "UTC"` için
    `BackgroundJobValidationException` **fırlatır** ve uygulama **başlangıçta** ölür. "Türkiye saatiyle 03:00"
    diye yazmak servisi düşürür — offset **cron ifadesine** gömülür.
17. **Arka planda `ICurrentUserContext` BOŞTUR.** `RequestedBy`/`TriggerSource` komuta **parametre** olarak
    geçmezse zamanlanmış batch'in maker'ı `null`/anonim olur ve **SoD karşılaştırması anlamsızlaşır** (V36).
18. **İki config kapısı yetmez, üç tanedir.** `Schedule.Enabled` açık ama MOD-0026'nın
    `EnabledJobs["Diten.Platform.WorkingCalendar.HolidayAutoFetchJob"]` girdisi **yoksa** iş
    `IsEnabled=false` ile kaydedilir ve **hiç çalışmaz** — "job neden tetiklenmiyor?" arızasının **birinci**
    nedeni budur (§4.9).
19. **İş exception fırlatarak "başarısız" olmamalıdır.** Sağlayıcı hatası bir **iş** hatası değil, bir
    **içerik** sonucudur: batch `failed` olur, iş **başarıyla** biter. Aksi hâlde Hangfire retry'ı devreye
    girer ve **aynı hedef için tekrar tekrar batch** açılmaya çalışılır (V40 skip'i bunu tutar ama gürültü
    üretir).

---

## 20. Follow-up Items

| # | Follow-up | Neden |
|---|---|---|
| ✅ **D-F01 … D-F12** | **12 kararın TAMAMI KAPANDI** (2026-08-27) — artık follow-up değildir | §19.1 |
| **F02-RBAC** | `platform.working-calendar.auto-fetch.{read,run,review,apply}` katalog + grant (**4 anahtar**) | §14 — fallback **bilinçli olarak** kullanılmadı; o zamana kadar yüzey **403** |
| **F02-SCHED-REFRESH** | `applied ⇒ skip` sınırlaması: yıl-içi ilan edilen/kaydırılan tatilin zamanlanmış işçe yakalanması (ör. "son apply'dan N ay sonra yeniden çek") | **V41** — bugün yalnız **manuel** re-fetch; kullanıcı kilidi guard 4 böyle sabitledi |
| **F02-SCHED-OBS** | Zamanlanmış koşum gözlemlenebilirliği: başarısız hedef sayısı için uyarı/bildirim (MOD-0027) ve dashboard rozeti | §4.9 — bugün yalnız log + `JobExecutionLog` + endpoint 10 |
| **F02-SECRET** | Kimlik gerektiren bir sağlayıcıya geçilirse MOD-0012 secrets entegrasyonu | §4.8 — bugün API-key **yok**, alan **açılmıyor** |
| **F02-SUBDIV** | Bölge (subdivision) bazlı tatiller — FU01'de bölge ekseni **yok** | §4.6/V15 — bugün yalnız **bayrak** |
| **F02-RECLASS** | Merge sonrası `public-holiday` → `religious-holiday`/`moveable-holiday` yeniden sınıflandırma ergonomisi | **D-F04** — otomatik atama **yasak**, elle düzeltme FU01 editöründen |
| **F02-SHIFT** | `date-shift` adaylarının **güncelleme** olarak uygulanabilmesi | §8.7 — bugün FU02 **yalnız ekler** |
| **F02-REMOVAL** | "Sağlayıcıda artık yok" durumunun ele alınması (arşivleme önerisi) | §8.7 — bugün aday **üretilmez** |
| **F02-AUDIT** | fetch / decision / apply olaylarının MOD-0021 audit trail'ine düşmesi | Canlı ülke takvimine yazan bir akış için denetim **gereklidir** |
| **F02-REG** | `CAND-CAP-0008-FU02` için registry + reconciliation ledger satırı (**yalnız D-F11 (b) seçilirse**) | §Kimlik — registry **protected path**, ayrı onay ister |
| **F02-BULK** | FU01'in `F-BULK-ARCHIVE`'ı geldiğinde FU02 verifier bulk FAIL'lerinin yeniden değerlendirilmesi | §17.3 — bugün **EXPECTED N/A** |
| **F02-STATUS** | Closeout'ta `execution/registries/module-implementation-status.md` satırı | **Yalnız kullanıcı onayıyla** |

---

## 21. Consumers and Downstream Contracts

**FU02'nin yeni tüketicisi YOKTUR ve olmayacaktır.**

`IWorkingCalendarProvider` FU02'den **habersizdir**: merge sonrası günler, FU01'in gözünde **sıradan
`public-holiday` günleridir** ve çözümleme sırası (v1 §4.5) **tek satır değişmez**. MOD-0155 (MicroTarget /
Visit / Route Planning), MOD-0280 ve gelecek PPM/Finance tüketicileri **FU02'yi görmez, bilmez, çağırmaz**.

Değişen **tek** dış gözlem: FU01 contract'ının `Limitations` listesindeki *"External holiday auto-fetch is not
implemented; source 'provider-fetch' is rejected."* satırı **gerçeğe uygun** hâle gelir ve `contractVersion`
bump edilir (AC-BOUNDARY-8). Contract'ı okuyan UI'lar bunu **otomatik** alır — hardcoded liste **yoktur**.

**Tüketici sözleşmesinin dört maddesi (v1 §21) aynen geçerlidir** ve FU02 hiçbirini gevşetmez:
kendi takvimini tutma · aritmetiği yeniden yazma · `calendar_missing`/`year_missing`/`country_unknown`
durumunu görünür kıl · aynı süreçteysen HTTP kullanma.

---

## Handoff

Module pack **`draft`** — **kod yetkisi yoktur**, build çalıştırılmadı, hiçbir runtime dosyası açılmadı.
**2026-08-27 revizyonu uygulandı: 12 D-kararının tamamı KAPALI.**

| Karar | Durum |
|---|---|
| **D-F02** — zamanlanmış fetch | ✅ **MANUEL + ZAMANLANMIŞ** (yıllık Hangfire job, **yalnız fetch→staging**) — §4.9'da altı guard, §12.8'de V36–V44, §16'da AC-SCHED-1…14 |
| **D-F10** — `active` hedefe apply | ✅ **KESİNLEŞTİ** — `.apply` + `platform.working-calendar.activate` |
| **D-F04 · D-F07 · D-F08 · D-F11** | ✅ **ONAYLANDI** — gerekçeleri §19.1'de korundu |
| **D-F01 · D-F03 · D-F05 · D-F06 · D-F09 · D-F12** | ✅ **KAPALI** — author önerileri kabul edildi |

**Otomasyonun SoD'ye etkisi — tek cümleyle:** zamanlanmış iş sistemi **daha az** değil **daha çok** denetlenebilir
yapar; çünkü maker artık **hiçbir zaman** checker olamayan bir sistem kimliğidir (`system:` öneki
`AppliedBy`/`DecidedBy` alanlarına **yazılamaz**, §14.2). Canlı takvime yazan tek yol **hâlâ** bir insanın
`apply` tıklamasıdır.

**Geriye kalan tek kapı:** `status` → `approved`/`ready-for-dev` **ve** `runtime_code_allowed` → `true` flip'i
(**ayrı kullanıcı kararı**). Ops tarafında ayrıca `date.nager.at` egress'inin açık olması gerekir.

Geliştirme için status `approved` veya `ready-for-dev` olmalı **ve** `runtime_code_allowed: true` yapılmalıdır;
sonra `@orchestrator CAND-CAP-0008-FU02-working-calendar-public-holiday-auto-fetch` çağrılır.

Hazırlık sırasında **Golden Reference Slim (DEV-0000)** şablon olarak alındı — naming'de sapma yok; üç yapısal
sapma (`Delete`/`BulkDelete` yokluğu, `Update` yokluğu, `Provider/`+`Mapping/` klasörleri) §10'da, bir frontend
sapması (`Review.cshtml`) §11.2'de, `entity_base` sapması §4.1'de **açıkça beyan edildi**. Zamanlanmış iş
**MOD-0026'nın canlı desenini** (registrar + `IBackgroundJobHandler<T>` + `EnabledJobs` kapısı +
`IJobExecutionLogWriter`) **birebir** izler; yeni bir scheduler deseni **icat edilmedi**.

> **Son hatırlatma:** `CAND-CAP-0008` ve `CAND-CAP-0008-FU02` **governance kimlikleridir**. Bu dizeler
> `services/`, `frontend/`, `gateway/` veya `tests/` altına **tek bir yorum satırında bile** girerse DCP-002
> candidate kapısı kalıcı olarak kırmızıya döner (AC-ID-1 / AC-ID-2). Runtime'da yalnız **`working-calendar`**
> ve **`working-calendar-import`** aileleri kullanılır.
