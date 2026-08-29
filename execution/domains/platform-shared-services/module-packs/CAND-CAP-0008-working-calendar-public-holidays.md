---
id: CAND-CAP-0008
name: Working Calendar & Public Holidays
runtime_slug: working-calendar
domain: platform-shared-services
service: Diten.Platform + frontend/Diten.Web
shell: platform-admin          # BİRİNCİL yüzey; İKİNCİ yüzey shell: tenant — §9 (frontmatter tek değer taşır)
shell_secondary: tenant        # Tenant Admin → Working Calendar Overrides (v1'e DAHİL, 2026-08-27 kararı)
golden_reference: compact      # HER İKİ yüzey de Compact — türetme §11.1 (11 alan) ve §11.3 (9 alan)
entity_base: HybridEntity
status: ready-for-dev
runtime_code_allowed: true
runtime_code_scope: "AÇIK (ready-for-dev, flip 2026-08-27). Yetkilendirilen kapsam: `WorkingCalendar` aggregate runtime (country-scoped global satır + tenant/org override satırı, gömülü gün listesi, CRUD-minus-delete + archive + activate + in-domain vokabüler + contract) `Diten.Platform` içinde, READ-ONLY `IWorkingCalendarProvider` working-day seam'i, VE İKİ ayrı UI yüzeyi `frontend/Diten.Web` içinde: (1) Platform Admin → Working Calendars Compact sayfası (ülke katmanı), (2) Tenant Admin → Working Calendar Overrides Compact sayfası (yalnız kendi override katmanı). Dış sağlayıcıdan otomatik tatil çekme (auto-fetch/staging/approve), scheduler/rezervasyon/kapasite motoru, çalışma saati (shift/mesai) modeli, izin-tatil (leave) yönetimi, MOD-0048 set publish, RBAC seed/grant, Gateway config yazımı, registry write ve Mongo hand-edit YASAKTIR."
owner: module-pack-author
branch: feature/pss/cand-cap-0008-working-calendar-public-holidays
started: 2026-08-26
revised: 2026-08-27          # F-TENANT-UI v1'e alındı; D2 + D5 onaylandı
target: TBD (kullanıcı onayı + ready-for-dev flip sonrası)
form_field_count: 9                   # Platform Admin yüzeyi (§11.1, 2026-08-27 scope-removal sonrası: 11 - ScopeType - OrganizationUnitId)
form_field_count_secondary: 9         # Tenant Override yüzeyi (§11.3)
dependencies:
  - MOD-0048 (read-only — country vokabüleri; in-process IPlatformLookupProvider "countries" seam'i, HTTP self-call YOK)
  - MOD-0288 (read-only — OrganizationUnit; org-scope override'ın GERÇEK ve doğrulanabilir FK'sı, aynı serviste)
  - MOD-0018 (RBAC — gerçek HasPermission; katalog/grant bu pack'te YOK → F-RBAC)
  - MOD-0021 (audit — activate/archive olaylarının audit'e düşmesi → F-AUDIT)
  - MOD-0026 (job scheduler — YALNIZ FU02 auto-fetch zamanlaması için; bu FU'da tüketilmez)
  - MOD-0032 (gateway — yeni route çifti integration-agent task'ı → F-GW)
  - DEV-0001 (Golden Reference Compact — tek yüzey, tek klasör)
consumers:
  - MOD-0155-FU05 (MicroTarget — F-CALENDAR bağımlılığı; §21)
  - MOD-0155-FU01 / FU03 (Visit Planning / Route Planning — çalışma günü doğrulaması)
  - MOD-0280 (Time Entry — gelecek)
  - PPM / Finance scheduler'ları (gelecek)
---

# CAND-CAP-0008 — Working Calendar & Public Holidays

> **⛔ DRAFT — KOD YETKİSİ YOKTUR (`runtime_code_allowed: false`).**
> Bu pack **yeni bir shared foundation capability**'nin hazırlık dokümanıdır. `status: ready-for-dev` +
> `runtime_code_allowed: true` flip'i **AYRI bir kullanıcı kararıdır**; `@orchestrator` bu pack ile kod yazamaz.
>
> ### 🔒 Kimlik — CAND-CAP-0008, runtime literal'e ASLA yazılmaz
>
> **DCP-002 candidate kapısı — PASS (2026-08-26):**
> `py .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0008 --name "Working Calendar & Public Holidays"`
> → `OK  candidate CAND-CAP-0008: temporary governance identity, pending EA, not Blueprint-backed, not in runtime.`
> (**exit 0**).
>
> Kapı **dört** şeyi birden doğruladı ve dördü de bu pack için bağlayıcıdır:
> 1. `execution/registries/module-id-registry.md` satır 213'te **kayıtlı** (`Candidate Capability`,
>    `candidate / pending-EA`, domain `platform-shared-services`, EA rezervasyonu kullanıcı tarafından
>    **2026-08-26'da yetkilendirilmiş**);
> 2. `execution/portfolio/blueprint-master-plan-reconciliation.md` **ledger'ında** kayıtlı;
> 3. adı registry satırıyla **birebir** aynı;
> 4. **`runtime_hits` taraması temiz** — `services/`, `frontend/`, `gateway/`, `tests/` altında
>    `CAND-CAP-0008` **hiç geçmiyor**.
>
> **Bu son madde makine ile zorlanan bir kuraldır.** `CAND-CAP-0008` bir koleksiyon adına, route'a, permission
> anahtarına, enum değerine, sınıf adına, dosya adına veya yoruma yazılırsa **kapı bir daha asla geçmez**
> (`verify_module_id.py:179` `runtime_hits` → `check_candidate` fail). Runtime'da kullanılacak **nötr ad**
> baştan sabitlenmiştir:
>
> ```text
> Governance identity (yalnız doküman/registry) : CAND-CAP-0008
> Runtime slug        : working-calendar
> Koleksiyon          : working_calendars
> Route               : /api/platform/working-calendars
> Permission          : platform.working-calendar.{read|manage|activate}
> Aggregate / sınıf   : WorkingCalendar · WorkingCalendarDay · IWorkingCalendarProvider
> View klasörü        : Views/Platform/WorkingCalendars/
> ```
>
> Kıyas: `CAND-CAP-0002` ve `CAND-CAP-0005` registry satırları, legacy `MOD-0297`/`MOD-0299` Hangfire job
> literal'lerini **açıkça istisna** olarak taşır. **Bu capability'nin böyle bir legacy istisnası yoktur ve
> olmayacaktır** — greenfield olduğu için runtime adı ilk günden nötr doğar.
>
> ### Bu neden CRM değil
> Türkiye'nin resmî tatili **CRM'e ait bir gerçek değildir**. Aynı takvimi saha ziyaret planı da, proje
> planı da, ödeme vade hesabı da, izin hesabı da okur. `crm-sor-boundary.md` CRM'in *"MOD-0048/MOD-0288/MDM/
> MOD-0018 aggregate'lerini fork etmediğini, yalnız okuyucu olarak tükettiğini"* söyler; çalışma takvimi de
> aynı kategoridedir — **yatay bir platform yeteneğidir** ve PSS domain'inin *"yatay yetenekleri sahiplenir"*
> tanımına girer. Registry satırı bunu zaten böyle kaydetmiştir: *"NOT reference-data, NOT CRM-owned; a shared
> foundation capability."*
>
> Otorite sırası: bu pack > [Domain Config](../domain-config.md) > `AGENTS.md` > `.antigravity/rules/`.

---

## 1. Module Summary

Bu capability **tek bir soruyu** kesin ve denetlenebilir biçimde cevaplar: **"Bu ülkede / bu organizasyonda,
bu tarih bir çalışma günü müdür?"** — ve bunun üzerine kurulu dört türev hesabı: bir sonraki çalışma günü,
n çalışma günü sonrası, iki tarih arasındaki çalışma günü sayısı, ve bir tarihin hangi tatile denk geldiği.

İki katman vardır ve **ikisi de aynı aggregate'te** yaşar (D2):

| Katman | `TenantId` | Kim yazar | İçerik |
|---|---|---|---|
| **Ülke katmanı** (global) | `null` | Platform admin | Hafta sonu tanımı (ülkeye göre Cts/Paz **veya** Cum/Cts) + resmî/dinî/hareketli tatiller |
| **Tenant/org katmanı** (override) | JWT'den | Tenant admin — **kendi konsolundan** (Tenant Admin → Working Calendar Overrides, §9.2) | Şirket tatili/kapanışı, telafi çalışma günü (`working-day-override`), opsiyonel hafta sonu override'ı |

**Asıl teslimat yüzeyi UI değil, `IWorkingCalendarProvider` read-only seam'idir** (§4.5). Takvim verisi
girilir; tüketiciler (MOD-0155 MicroTarget başta olmak üzere — §21) hesabı **tek bir yerden** sorar. Hiçbir
tüketici kendi hafta sonu/tatil listesini tutmaz.

**Hedef kullanıcı:** platform admin (ülke takvimlerini kuran/aktive eden), tenant admin (şirket kapanış
günlerini giren — v1'de API üzerinden), ve **kod** (provider'ı tüketen modüller).

**MOTOR YOK (D6).** Bu servis **takvim verisi + çalışma-günü aritmetiği**dir. Kaynak tahsisi yapmaz, kapasite
hesaplamaz, iş sıralamaz, rezervasyon tutmaz, vardiya/mesai saati modellemez, izin (leave) yönetmez.
Aritmetik deterministiktir ve takvim verisinden **birebir** türer.

---

## 2. Ownership and Boundaries

### 2.1 Kapsam

| Kapsam | Karar |
|---|---|
| **In-scope (v1)** | `WorkingCalendar` aggregate (gömülü `WorkingCalendarDay` listesi) + repository + CQRS + persistence + **12 API endpoint** (2 controller) + contract yüzeyi + **`IWorkingCalendarProvider` read-only seam** (`Diten.Platform`) **ve İKİ ayrı UI konsolu** (`frontend/Diten.Web`): ① **Platform Admin → Working Calendars** (ülke katmanı) · ② **Tenant Admin → Working Calendar Overrides** (yalnız kendi override katmanı) |
| **Out-of-scope (AÇIKÇA ERTELENİR)** | **Auto-fetch** (dış sağlayıcı → staging → review/approve → activate) → **FU02 / F-AUTOFETCH** · çalışma saati / vardiya / mesai modeli → **F-SHIFT** · izin (leave/absence) yönetimi → HCM · scheduler / kapasite / rezervasyon motoru · takvim bazlı SLA hesabı (tüketici sorumluluğu) |

> **📌 REVİZYON (kullanıcı kararı, 2026-08-27) — `F-TENANT-UI` ERTELENMEDİ, v1'e alındı.**
> Tenant override authoring UI artık **in-scope**'tur ve out-of-scope listesinden **çıkarılmıştır**. Bununla
> birlikte iki karar da onaylandı ve gövde zaten onlara göre yazılıydı: **D2 = `HybridEntity` tek-aggregate ✅**
> ve **D5 = `Resolution` + `ReasonCodes` sonuç sözleşmesi ✅**. Bu revizyonun dokunmadığı hiçbir şey
> değişmemiştir: provider seam'in 5 metodu, auto-fetch'in FU02'de kalması, D6 motor-yok, D7 vokabüler,
> `OrganizationUnitId`'nin gerçek FK oluşu ve `CAND-CAP-0008`'in runtime literal yasağı **aynen** geçerlidir.
>
> **İki yüzey = iki ayrı klasör = iki ayrı verifier koşusu.** Bu, MOD-0162-FU03'ün hibrit-tek-sayfa
> probleminden (tek sayfanın iki golden-reference referansını birden geçememesi) **yapısal olarak** kaçınır:
> her yüzeyin kendi klasörü, kendi form'u ve kendi tek DataTable'ı vardır.

### 2.2 Ne sahiplenir, ne yalnız tüketir

| Nesne | Sahip | Bu FU'da |
|---|---|---|
| `WorkingCalendar` (ülke + tenant/org katmanı) | **Bu capability** | **AÇILIR** — tek aggregate |
| `WorkingCalendarDay` (tatil / kapanış / telafi günü) | **Bu capability** | **AÇILIR** — gömülü (D3) |
| Working-day aritmetiği (`isWorkingDay`, `nextWorkingDay`, …) | **Bu capability** | **AÇILIR** — read-only provider |
| Ülke listesi / ISO country kodu | **MOD-0048** | **read-only** — `IPlatformLookupProvider` `"countries"` |
| `OrganizationUnit` (org-scope override hedefi) | **MOD-0288** | **read-only** — gerçek FK, doğrulanır (D4) |
| Permission değerlendirme motoru | **MOD-0018** | **tüketim** — yeni engine yok |
| Vardiya / mesai saati / kapasite | — | **AÇILMAZ** (F-SHIFT) |
| İzin / devamsızlık (leave) | HCM | **AÇILMAZ** — tatil ≠ izin (§2.3) |
| Ziyaret planı / rota / MicroTarget | MOD-0155 | **AÇILMAZ** — tüketici (§21) |

### 2.3 Sınır netleştirmeleri (karıştırılması kolay üç şey)

1. **Tatil ≠ izin.** Resmî tatil **herkes için** geçerli bir takvim gerçeğidir; izin **bir kişiye** aittir ve
   HCM'e aittir. Bu aggregate kişi bilmez, kişi alanı **yoktur**.
2. **Çalışma günü ≠ çalışma saati.** v1 **gün granülaritesindedir**. "09:00–18:00 mesai", "yarım gün 13:00'te
   biter" gibi saat bilgisi **modellenmez** (F-SHIFT). `IsHalfDay` yalnız bir **etikettir** (§4.4/D5).
3. **Takvim ≠ reference data.** Ülke *listesi* reference data'dır (MOD-0048). Ülkenin *takvimi* yıl bazlı,
   onaylanan, sürümlenen ve **hesaba giren** bir operasyonel kayıttır — bir kod-değer listesi değildir.
   Registry satırı bunu açıkça *"NOT reference-data"* diye kaydeder.

### 2.4 Kalıcı yasaklar

```text
Contact.IsWorkingDay / Account.Holidays          ❌  tüketici kendi takvimini tutmaz
PlannedVisit.WeekendDays                         ❌  hafta sonu tanımı kopyalanmaz, sorulur
<herhangi bir modül>.PublicHolidayList           ❌  ikinci bir tatil listesi = ikinci gerçek
WorkingCalendar.EmployeeId / PersonId            ❌  takvim kişi bilmez (§2.3/1)
WorkingCalendar.ShiftStart / WorkingHours        ❌  gün granülaritesi (§2.3/2, F-SHIFT)
"CAND-CAP-0008" (runtime literal)                ❌  MAKİNE İLE ZORLANIR (§başlık)
```

---

## 3. Owned Objects

| Katman | Nesne |
|---|---|
| **Entity** | `WorkingCalendar : HybridEntity` (aggregate root) + gömülü `WorkingCalendarDay` |
| **Repository** | `IWorkingCalendarRepository` → `HybridRepository<WorkingCalendar>` üzerine (mevcut altyapı, D2) |
| **Commands** | `CreateWorkingCalendarCommand` · `UpdateWorkingCalendarCommand` · `ActivateWorkingCalendarCommand` · `ArchiveWorkingCalendarCommand` · `UpsertWorkingCalendarDayCommand` · `ArchiveWorkingCalendarDayCommand` |
| **Queries** | `ListWorkingCalendarsQuery` · `GetWorkingCalendarByIdQuery` · `GetWorkingCalendarContractQuery` · `ResolveWorkingDayQuery` |
| **Provider seam** | **`IWorkingCalendarProvider`** + `WorkingDayResult` / `HolidayInfo` / `WorkingCalendarScope` / `WorkingCalendarReasonCodes` (§4.5) |
| **DTOs** | `WorkingCalendarDto` · `WorkingCalendarListDto` · `WorkingCalendarDayDto` · `WorkingCalendarContractDto` |
| **Controllers** | `WorkingCalendarsController` (`PlatformActor` policy) **+** `WorkingCalendarOverridesController` (tenant aktörü) — **ayrı olmak ZORUNDA**, §14/§19.2/1 |
| **API endpoints** | §15 tablosu — **12 endpoint**, hepsi `/api/platform/working-calendars*` |
| **Frontend routes** | `/Platform/WorkingCalendars` (platform shell) **+** `/WorkingCalendarOverrides` (tenant shell) |
| **Permissions** | `platform.working-calendar.read` · `.manage` · `.activate` **+** `.override.read` · `.override.manage` (§14) |
| **Vokabüler (in-domain)** | `WorkingCalendarScopeType` · `WorkingCalendarDayOfWeek` · `WorkingCalendarDayType` · `WorkingCalendarRecurrence` · `WorkingCalendarStatus` · `WorkingCalendarSource` · `WorkingCalendarReasonCodes` |
| **AÇIKÇA sahiplenilmeyen** | auto-fetch staging aggregate'i (FU02) · vardiya/mesai · izin · scheduler · ülke listesi |

---

## 4. Entity Fields

### 4.1 `entity_base: HybridEntity` — gerekçe (D2)

`services/Diten.Platform.Common/src/Diten.Platform.Common/Persistence/EntityVariations.cs` **üç** taban sunar
ve üçü de canlı koddadır:

```csharp
public abstract class GlobalEntity : BaseEntity { }                    // No TenantId here
public abstract class TenantScopedEntity : BaseEntity { public required Guid TenantId { get; init; } }
public abstract class HybridEntity : BaseEntity {                      // ← seçilen
    public Guid? TenantId { get; init; }
    public bool IsGlobal => TenantId == null;
}
```

Ve `Persistence/Repositories.cs:97` zaten şunu taşır:

```csharp
/// Repository for Hybrid entities (Global default + Tenant override).
public class HybridRepository<TEntity> : RepositoryBase<TEntity> where TEntity : HybridEntity
{
    protected override FilterDefinition<TEntity> ExecutionFilter =>
        Filter.And(Filter.Or(Filter.Eq(e => e.TenantId, null),
                             Filter.Eq(e => e.TenantId, TenantContext.TenantId)),
                   Filter.Eq(e => e.IsDeleted, false));
}
```

**"Global default + Tenant override"** bu capability'nin tanımının birebir kendisidir: ülke takvimi global
satır (`TenantId = null`), şirket kapanışı tenant satırı. Altyapı **zaten var**; ikinci bir aggregate,
ikinci bir koleksiyon ve elle yazılmış bir birleştirme filtresi **gereksizdir**.

> **⚠️ İKİ BEYAN (gizlenmiyor):**
> 1. `module-pack-standard.md` §2'deki `entity_base` enum'ı `EntityBase | BaseEntity | GlobalEntity` der;
>    **`HybridEntity` listede yoktur**. Bu, standarda karşı **bilinçli ve gerekçeli** bir sapmadır — sınıf
>    gerçek koddadır, `Diten.Platform`'a aittir ve standardın `GlobalEntity` istisnası için istediği gerekçe
>    burada verilmiştir. Standardın §14 kuralı gereği: **global satırın DTO'sunda `TenantId` bulunmaz**;
>    tenant satırında `TenantId` **JWT claim'inden** server-side çözülür ve **payload'da asla taşınmaz**.
> 2. **`HybridEntity`'nin bugün üretimde tek bir kullanıcısı yoktur** — sınıf ve repository tanımlı, ama
>    hiçbir entity ondan türemiyor (kod taraması: yalnız `EntityVariations.cs` + `Repositories.cs`). Bu
>    capability **ilk tüketici** olacaktır; `HybridRepository`'nin `ExecutionFilter`'ı ilk kez gerçek yükle
>    çalışacağı için §17'ye **açık bir hybrid-filtre test kümesi** (küme 6) konmuştur.
>
> **Reddedilen alternatif:** iki ayrı aggregate (`WorkingCalendar : GlobalEntity` + `WorkingCalendarOverride
> : TenantScopedEntity`). Reddetme gerekçesi: iki koleksiyon → iki repository → iki CRUD yüzeyi → iki
> verifier koşusu → ve en kötüsü, katman birleştirmesinin **her tüketicide** değil ama **her sorguda** elle
> yazılması. `HybridRepository` bunu altyapı seviyesinde zaten çözüyor.

Miras alınan alanlar (`Diten.Platform.Common.Persistence.BaseEntity`): `Guid Id` · `DateTimeOffset CreatedAt`
· `string CreatedBy` · `DateTimeOffset? UpdatedAt` · `string? UpdatedBy` · `bool IsDeleted` ·
**`int Version`** (teknik concurrency token — iş alanı **değildir**).

> **Dikkat — iki farklı `BaseEntity` var.** `Diten.Platform.Domain.Common.BaseEntity` (legacy: `string Id`,
> `bool Status`, `DateTime CreatedDate`) **KULLANILMAZ**. Yeni iş `Diten.Platform.Common.Persistence`
> ailesini kullanır — `OrganizationUnit` (MOD-0288 v1) emsali. Yanlış tabanı seçmek `Guid` vs `string` id
> ve `DateTime` vs `DateTimeOffset` uyumsuzluğu doğurur.

### 4.2 `WorkingCalendar` — aggregate root

| # | Alan | Tip | Zorunlu | Form? | Kural / Not |
|---|---|---|---|---|---|
| 1 | `Id` | Guid | Evet | ✗ | `BaseEntity` |
| 2 | `TenantId` | Guid? | Koşullu | ✗ | **`null` ⇒ ülke katmanı** (platform SoR) · dolu ⇒ tenant/org override. **JWT'den** çözülür, payload'da **asla** gelmez |
| 3 | `CalendarCode` | string | Evet | ✓ | Stabil iş anahtarı; **scope içinde** unique — yalnız canlı (`draft`/`active`) satırlar arasında; arşiv kodu serbest bırakır (§4.6 index, AC-CORE-2a); rename edilmez |
| 4 | `CalendarName` | string | Evet | ✓ | Trim, max 200 |
| 5 | `Description` | string? | Hayır | ✓ | Max 1000 |
| 6 | `CountryCode` | string | Evet | ✓ | **ISO-3166-1 alpha-2**; MOD-0048 `"countries"` lookup'ından **doğrulanır** (§4.7); serbest metin **değil** |
| 7 | `CalendarYear` | int | Evet | ✓ | `1900 <= yıl <= 2200`. **Bu aggregate'in tek zaman eksenidir** (D1) |
| 8 | `ScopeType` | string | Evet | ✓ | In-domain: `country` · `tenant` · `organization-unit` (§4.4). `TenantId` ile **tutarlı olmak zorunda** (§12.2) |
| 9 | `OrganizationUnitId` | Guid? | Koşullu | ✓ | **GERÇEK FK** — `ScopeType=organization-unit` ise zorunlu ve `OrganizationUnit` aggregate'inde **var olduğu doğrulanır** (D4) |
| 10 | `WeekendDays` | string[]? | Koşullu | ✓ (multi-select) | In-domain gün adları. **Ülke katmanında ZORUNLU**; override katmanında `null` ⇒ **ülke katmanından devralınır** (D3) |
| 11 | `Days` | `WorkingCalendarDay[]` | Hayır | ✗ (alt-editör) | Gömülü liste (§4.3). Alan değil, **repeater** |
| 12 | `CalendarStatus` | string | Evet | ✓ | `draft` · `active` · `archived` (§12.3 state machine) |
| 13 | `Source` | string | Evet | ✓ | `manual` (v1'de **tek üreticisi olan** değer) · `imported` · `provider-fetch` (**rezerve** — üreticisi FU02) |
| 14 | `Notes` | string? | Hayır | ✓ | Max 2000 |
| 15 | `ActivatedAt` / `ActivatedBy` | DateTimeOffset? / string? | Hayır | ✗ | `activate` aksiyonu doldurur |
| 16 | `ArchivedAt` / `ArchivedBy` | DateTimeOffset? / string? | Hayır | ✗ | `archive` aksiyonu doldurur |
| 17 | `CreatedAt/By` · `UpdatedAt/By` · `IsDeleted` · `Version` | — | — | ✗ | `BaseEntity` |

### 4.3 `WorkingCalendarDay` — gömülü (D3)

| # | Alan | Tip | Zorunlu | Alt-editör? | Kural / Not |
|---|---|---|---|---|---|
| 1 | `DayId` | Guid | Evet | ✗ (üretilir) | Gömülü kimlik |
| 2 | `DayCode` | string | Evet | ✓ | Takvim içinde unique (handler + validator; **dizi-içi DB index YOKTUR** — §19/3) |
| 3 | `DayName` | string | Evet | ✓ | "Cumhuriyet Bayramı" · "Şirket kapanışı" |
| 4 | `Date` | DateOnly | Evet | ✓ | Yılı `CalendarYear` ile **aynı olmak zorunda** (§12.4) |
| 5 | `ObservedDate` | DateOnly? | Hayır | ✓ | Tatilin **fiilen kullanıldığı** gün (bazı ülkeler tatili Pazartesi'ye kaydırır). Provider `ObservedDate ?? Date` kullanır |
| 6 | `DayType` | string | Evet | ✓ | In-domain (§4.4): `public-holiday` · `religious-holiday` · `moveable-holiday` · `company-holiday` · `company-closure` · **`working-day-override`** |
| 7 | `Recurrence` | string | Evet | ✓ | In-domain: `none` · `annual-fixed` · `annual-moveable`. **Yalnız bir beyandır** — v1'de hiçbir gün otomatik türetilmez (D6) |
| 8 | `IsHalfDay` | bool | Hayır | ✓ | **Yalnız etiket.** v1 provider yarım günü **ÇALIŞMA GÜNÜ** sayar (§2.3/2, D5) |
| 9 | `DayStatus` | string | Evet | ✗ (archive aksiyonu) | `active` · `archived` |
| 10 | `Notes` | string? | Hayır | ✓ | Max 500 |

> **`working-day-override` neden var:** Türkiye'de bir Cumartesi telafi/köprü günü olarak **çalışma günü**
> ilan edilebilir. Bu tip olmadan hafta sonu tanımı **delinemez** ve `isWorkingDay` gerçeğe aykırı cevap
> verir. Bu tip, hafta sonu **ve** tatil kuralını ezer (§4.5 çözümleme sırası).

### 4.4 In-domain vokabüler (D7 — fail-closed)

```text
WorkingCalendarScopeType  : country · tenant · organization-unit
WorkingCalendarDayOfWeek  : monday · tuesday · wednesday · thursday · friday · saturday · sunday
WorkingCalendarDayType    : public-holiday · religious-holiday · moveable-holiday ·
                            company-holiday · company-closure · working-day-override
WorkingCalendarRecurrence : none · annual-fixed · annual-moveable
WorkingCalendarStatus     : draft · active · archived
WorkingCalendarSource     : manual · imported · provider-fetch
```

Vokabüler `Domain/Entities/WorkingCalendar.cs` içinde `static class` olarak yaşar; **set dışı değer → 400**;
**hardcoded fallback listesi yasaktır** — tüm dropdown'lar `contract` endpoint'inden beslenir
(`platform-lookups-reference-data.md`: *"Hardcoded fallback lookup listeleri kabul edilmez"*).

`WorkingCalendarDayOfWeek` **yapısaldır** (ISO haftanın günü, `MOD-0150 ContactAvailability.Weekday` emsali:
*"not a localized label and not tenant vocabulary, so it is validated in-domain rather than via MOD-0048"*).
`DayType`/`Recurrence` de yapısaldır: her biri provider'ın çözümleme sırasında **davranış** değiştirir, bu
yüzden tenant tarafından serbestçe genişletilemez. Tenant'a özel etiket ihtiyacı doğarsa bu MOD-0048'e
gider — **F-RD**.

### 4.5 `IWorkingCalendarProvider` — asıl tüketim yüzeyi (read-only)

```csharp
/// Read-only working-day seam. Tek gerçek kaynağıdır: hiçbir tüketici hafta sonu/tatil listesi kopyalamaz,
/// hiçbir tüketici bu aritmetiği yeniden yazmaz. ASLA yazmaz, ASLA tüketiciye exception fırlatmaz.
public interface IWorkingCalendarProvider
{
    Task<WorkingDayResult>  IsWorkingDayAsync(DateOnly date, WorkingCalendarScope scope, CancellationToken ct);
    Task<HolidayLookupResult> GetHolidayAsync(DateOnly date, WorkingCalendarScope scope, CancellationToken ct);
    Task<WorkingDateResult> NextWorkingDayAsync(DateOnly date, WorkingCalendarScope scope, CancellationToken ct);
    Task<WorkingDateResult> AddWorkingDaysAsync(DateOnly start, int days, WorkingCalendarScope scope, CancellationToken ct);
    Task<WorkingDayCountResult> WorkingDaysBetweenAsync(DateOnly from, DateOnly to, WorkingCalendarScope scope, CancellationToken ct);
}

public sealed record WorkingCalendarScope(string CountryCode, Guid? OrganizationUnitId = null);
```

**Çözümleme sırası (deterministik, §12.5'te test edilir):**

```text
1. tenant/org katmanında `working-day-override` var mı?   → ÇALIŞMA GÜNÜ  (en yüksek öncelik)
2. ülke katmanında `working-day-override` var mı?          → ÇALIŞMA GÜNÜ
3. tenant/org katmanında aktif bir gün (tatil/kapanış) var mı? → ÇALIŞMA GÜNÜ DEĞİL
4. ülke katmanında aktif bir tatil var mı?                 → ÇALIŞMA GÜNÜ DEĞİL
5. gün, geçerli `WeekendDays` setinde mi? (override varsa o, yoksa ülkeninki) → ÇALIŞMA GÜNÜ DEĞİL
6. aksi hâlde                                              → ÇALIŞMA GÜNÜ
```

**Sonuç sözleşmesi — sessiz varsayım yok (D5):** her metot `Resolution` alanı taşıyan bir **record** döner:

| `Resolution` | Anlamı | Değer alanı |
|---|---|---|
| `resolved` | Aktif bir takvimden **kesin** cevap üretildi | dolu |
| `calendar_missing` | O ülke için **hiç** aktif takvim yok | **`null`** |
| `year_missing` | Ülke takvimi var ama **istenen yıl** yok (çok yıllı aralıkta kısmen) | **`null`** |
| `country_unknown` | `CountryCode` MOD-0048 `"countries"` setinde yok | **`null`** |

Her sonuç ayrıca `IReadOnlyList<string> ReasonCodes`, `Guid? ResolvedCalendarId`,
`Guid? ResolvedOverrideCalendarId` ve `string SelectionReason` taşır.

> **Neden `bool` değil de record (D5).** `IsWorkingDayAsync` düz `bool` dönseydi, takvim **yokken** cevap
> `true` ya da `false` olurdu ve her iki durumda da tüketici **uydurulmuş bir gerçeğe** göre plan yapardı.
> MOD-0164'ün *"unknown is NOT allowed — a default is never invented"* ve MOD-0165'in *"no policy matches →
> the frequency is genuinely unknown"* kuralları burada da geçerlidir: **takvim yoksa cevap yoktur**,
> tüketici bunu görmek zorundadır. `WorkingDaysBetween`'in `null` dönmesi bir hata değil, **dürüstlüktür**.
>
> `ReasonCodes` kanonik sabitleri: `working_day` · `weekend_day` · `public_holiday` · `company_closure` ·
> `working_day_override_applied` · `half_day_treated_as_working` · `tenant_override_applied` ·
> `weekend_inherited_from_country` · `calendar_missing` · `year_missing` · `country_unknown` ·
> `calendar_not_active`.

**Provider'ın yapmadıkları:** yazmaz · takvim üretmez · eksik yılı komşu yıldan **türetmez** ·
`Recurrence`'tan gelecek yılı **hesaplamaz** (D6) · saat/vardiya bilmez · kişi/izin bilmez · cache
invalidation politikası **bu FU'da yoktur** (F-CACHE).

### 4.6 MongoDB index ihtiyacı

| Index | Alanlar | Not |
|---|---|---|
| Scope + kod | `TenantId` + `CountryCode` + `CalendarYear` + `CalendarCode` | Unique **partial** (`IsDeleted=false` **AND** `CalendarStatus $in [draft, active]`). `TenantId=null` global satırları da kapsar. Arşivli satır index dışıdır → kodu serbest bırakır (AC-CORE-2a). **`$ne` KULLANILMAZ** (partial filter kısıtı) → küme pozitif `$in` ile yazılır (§19/4) |
| Provider ana sorgu | `TenantId` + `CountryCode` + `CalendarYear` + `CalendarStatus` | `IsWorkingDay` yolunun tek sorgusu |
| Org override | `TenantId` + `OrganizationUnitId` + `CalendarYear` | Sparse |
| **YASAK** | iki `DateTimeOffset` alanının **birlikte** index'lenmesi/sort'u | Parallel-arrays 500 dersi. `Date`/`ObservedDate` bu yüzden **`DateOnly`**'dir |

### 4.7 Reference data sınırı (MOD-0048)

`CountryCode`, **MOD-0048 Business Reference Data**'nın `"countries"` setinden doğrulanır. Tüketim
**in-process**tir — `IPlatformLookupProvider.GetLookupOptionsAsync("countries", ct)`
(`Features/Lookups/Services/IPlatformLookupProvider.cs`), **aynı serviste**; Gateway üzerinden kendi
servisine HTTP **atılmaz**.

- UI tarafı mevcut endpoint'i tüketir: **`GET /api/lookups/countries`**
  (`[Authorize(Policy = "PlatformActor")]` + `[HasPermission("platform.lookups.read")]`), yanıt
  **`LookupOptionDto(Code, Name, Value, Group?, SortOrder?, Metadata?)`**.
- **Yeni Platform lookup key'i GEREKMEZ** — `PlatformLookupKeys.Countries = "countries"` zaten mevcut.
- **Hardcoded ülke listesi / fallback yasaktır.** Lookup boş dönerse form **açılır ama kaydetmez** (400
  `country_unknown`); sessizce serbest metin kabul **edilmez**.
- Bu capability MOD-0048'e **hiçbir set publish etmez** ve reference-data aggregate'lerini **mutate etmez**.

---

## 5. Repo Scope

**Backend — `services/Diten.Platform/`:**

```text
src/Diten.Platform.Domain/Entities/WorkingCalendar.cs              (aggregate + gömülü gün + vokabüler)
src/Diten.Platform.Domain/Repositories/IWorkingCalendarRepository.cs
src/Diten.Platform.Application/Features/WorkingCalendar/**         (§10 klasör sözleşmesi)
src/Diten.Platform.Infrastructure/Persistence/WorkingCalendarRepository.cs
src/Diten.Platform.Infrastructure/Persistence/DependencyInjection.cs   (YALNIZ class-map + index + DI kaydı)
src/Diten.Platform.API/Controllers/WorkingCalendarsController.cs           (PlatformActor)
src/Diten.Platform.API/Controllers/WorkingCalendarOverridesController.cs   (tenant aktörü — AYRI, §19.2/1)
tests/**/WorkingCalendar/**
```

**Frontend — `frontend/Diten.Web/` — YÜZEY ①: Platform Admin (platform shell):**

```text
Controllers/PlatformWorkingCalendarsController.cs                  (same-origin proxy)
Views/Platform/WorkingCalendars/**                                 (§11.2 — 9 dosya)
wwwroot/assets/js/Platform/WorkingCalendars/**                     (3 dosya)
Resources/Views/Platform/WorkingCalendars/WorkingCalendarsIndex.{en,tr}.resx    (platform shell = 2 dil)
Resources/SharedResource.{en,tr}.resx                              (YALNIZ WorkingCalendarsMenu anahtarı)
Views/Shared/_LayoutPlatformAdmin.cshtml                           (YALNIZ permission-guard'lı tek <li>)
```

**Frontend — `frontend/Diten.Web/` — YÜZEY ②: Tenant Admin (tenant shell):**

```text
Controllers/WorkingCalendarOverridesController.cs                  (same-origin proxy — tenant)
Views/WorkingCalendarOverrides/**                                  (§11.4 — 9 dosya)
wwwroot/assets/js/WorkingCalendarOverrides/**                      (3 dosya)
Resources/Views/WorkingCalendarOverrides/WorkingCalendarOverridesIndex.{ar,en,es,fr,ru,tr,zh}.resx
                                                                   (tenant shell = 7 dil — §11.4 kanıtı)
Resources/SharedResource.{ar,en,es,fr,ru,tr,zh}.resx               (YALNIZ WorkingCalendarOverridesMenu)
Views/Shared/_LayoutTenantShell.cshtml                             (YALNIZ permission-guard'lı tek <li>)
```

**Bu pack (bugün geçerli olan tek yazma alanı):**

```text
execution/domains/platform-shared-services/module-packs/CAND-CAP-0008-working-calendar-public-holidays.md
```

---

## 6. Protected Paths

- `.antigravity/**` (global engineering system)
- `gateway/Diten.ApiGateway/**/ocelot.json` — **integration-agent owned**; §15 ayrı task (**F-GW**)
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` (**FROZEN**)
- `frontend/Diten.Web/Controllers/Archive/**`, `Views/Archive/**` (**FROZEN**)
- **Tüketilen yüzeyler — okunur, DEĞİŞTİRİLMEZ:**
  - `Features/Lookups/**` (özellikle `IPlatformLookupProvider` **imzası** ve `PlatformLookupKeys`)
  - `Domain/Entities/ReferenceDataEntities.cs` · `ReferenceDataEntitiesv2.cs` (MOD-0048 aggregate'leri)
  - `Domain/Entities/Organization/**` (`OrganizationUnit`, `Position`, `PositionAssignment`)
  - `services/Diten.Platform.Common/**` (`EntityVariations.cs`, `Repositories.cs` — **tüketilir, düzenlenmez**)
- Diğer domain servisleri: `services/Diten.MdmService/**` · `Diten.CrmService/**` · `Diten.HcmService/**` ·
  `Diten.EnterpriseStrategyService/**` · `Diten.DevEnablementService/**`
- `services/Diten.AuthService/**` (RBAC engine — yalnız tüketilir)
- RBAC katalog/seed dosyaları ve `rolePermissions` koleksiyonu (**F-RBAC**)
- `execution/registries/**` (registry satırı **zaten var**; bu pack yazmaz — §20/F-REG)
- Mongo hand-edit (**yasak** — GUID subtype hatası tüm login'leri kırar)
- **ERP tarafı reference/master yolları** (`platform-lookups-reference-data.md` §13 gereği out-of-scope):
  ERP Account, General Reference, Financial Reference, Territory Reference, MDM product/brand master

---

## 7. Dependencies

| Bağımlılık | Tür | Durum (kod üzerinden doğrulandı) | Bu FU ne yapar |
|---|---|---|---|
| **MOD-0048** Reference Data (`"countries"`) | **read-only, in-process** | **SHIPPED** — `IPlatformLookupProvider`, `PlatformLookupKeys.Countries`, `GET /api/lookups/countries` | `CountryCode`'u **doğrular**; set publish etmez; **yeni lookup key gerekmez** |
| **COUNTRY_CODES** BRD seti (paylaşılan, `Global` scope) | **read-only tüketim (Overrides)** | Set **MEVCUT** ama `publishedVersionId: null`, `governanceStatus: Draft`, draft sürümde **0 değer** (2026-08-27 canlı doğrulama) | **Yüzey ② (Overrides)** ülke seçeneklerini `GET /api/v1/reference-data/sets/COUNTRY_CODES/published-values` (scope_key YOK — global) üzerinden okur. **Yüzey ① (Platform) hardcoded `/api/lookups/countries` olarak KALIR — kabul edilen durum.** Bu capability seti publish ETMEZ / mutate ETMEZ. Set publish edilene kadar Overrides ülke listesi **BOŞ** (fail-closed, fallback yok) → **F-COUNTRY-SoT** |
| **MOD-0288** `OrganizationUnit` | **read-only, aynı servis** | **SHIPPED** — `Domain/Entities/Organization/OrganizationUnit.cs` | `OrganizationUnitId`'yi **doğrular** (gerçek FK, D4) |
| **`Diten.Platform.Common`** `HybridEntity` / `HybridRepository<T>` | **altyapı** | **Tanımlı, üretimde kullanıcısı YOK** | **İlk tüketici** olur (§4.1 uyarısı, §17 küme 6) |
| **MOD-0018** RBAC | **tüketim** | SHIPPED | `[HasPermission]` guard'ları; **seed/grant yok** (**F-RBAC**) |
| **MOD-0021** Audit Trail | **gevşek** | SHIPPED (planlı kapsam) | `activate`/`archive` audit'e düşmeli (**F-AUDIT**) — v1 blocker **değil** |
| **MOD-0026** Job Scheduler (Hangfire) | **YOK (FU02)** | SHIPPED | Bu FU **tüketmez**; auto-fetch zamanlaması **FU02**'ye ait |
| **MOD-0032** Gateway | **config** | SHIPPED | Yeni route çifti **gerekli** (§15, **F-GW**) |
| **DEV-0001** Golden Compact | **şablon** | SHIPPED | §10/§11 birebir taklit |

**Tüketiciler ayrı bir bölümdedir → §21.**

---

## 8. Runtime Constraints

**8.1 Persistence.** MongoDB tek instance. Bu aggregate **hibrit**tir: `TenantId = null` satırlar
cross-tenant okunur, `TenantId` dolu satırlar yalnız sahibi tenant'a görünür. Bu ayrım **`HybridRepository`
`ExecutionFilter`'ı tarafından** uygulanır; elle filtre yazılmaz.

**8.2 Tenant sızıntısı — tek gerçek risk.** Hibrit modelin bedeli şudur: `TenantId` **yanlışlıkla `null`
bırakılırsa satır her tenant'a görünür. Bu yüzden yazma yolunda kural mutlaktır:

```text
ScopeType = country              ⇒ TenantId = null   VE  aktör platform_admin olmak ZORUNDA
ScopeType = tenant | organization-unit ⇒ TenantId = JWT claim'inden, ASLA payload'dan
```

`TenantId`'nin request payload'ından okunduğu **tek bir kod yolu bile** kabul edilmez (§13 + AC-SEC-1/2).

**8.3 Soft delete / hard delete yok.** `Delete` **endpoint'i yoktur**; `BulkDelete` **yoktur**. Takvim
`archive` edilir, gün `DayStatus=archived` olur, geçmiş **okunabilir kalır**. Bu, Golden Reference'ın
`BulkDelete{Module}Command` beklentisinden **beyan edilmiş** bir sapmadır (§10).

**8.4 Concurrency.** `BaseEntity.Version` optimistic concurrency token'ıdır. Takvim **ve** gömülü gün
yazımları **kökü** beklenen `Version` ile replace eder; uyuşmazlık **409**. Pozisyonel dizi güncellemesi
(`$set: days.$[…]`) ile tam-doküman replace **karıştırılmaz** — tek kod yolu (MOD-0162-FU05 dersi).

**8.5 Transaction gerekmez.** Tek aggregate, tek doküman; çok-doküman atomiklik yoktur.

**8.6 `active` takvim dondurulur (kısmen).** `CalendarStatus = active` olduktan sonra `CountryCode`,
`CalendarYear`, `ScopeType`, `OrganizationUnitId` **değiştirilemez** (§12.3). `WeekendDays` ve `Days`
değiştirilebilir — çünkü resmî tatiller yıl içinde ilan edilir/kaydırılır ve takvimin bunu yansıtması
gerekir. Bu bilinçli bir asimetridir: **kimlik donar, içerik yaşar.**

**8.7 Motor yok (D6).** `Recurrence` **yalnız bir beyandır** — v1'de hiçbir gün otomatik türetilmez, gelecek
yıl **kendiliğinden oluşmaz**. Bir sonraki yılın takvimi ya elle girilir ya da FU02 auto-fetch ile gelir.

**8.8 Provider cache YOK.** `IWorkingCalendarProvider` her çağrıda okur. `IPlatformLookupCache` emsali bir
cache katmanı **v1'de açılmaz** — çünkü invalidation politikası (takvim `activate` edildiğinde tüm
tüketicilerin görmesi) ayrı bir tasarım gerektirir (**F-CACHE**). Erken cache, yanlış tatil listesiyle
plan yapmaktan **daha kötüdür**.

**8.9 API/Gateway.** Frontend **Gateway 5000** üzerinden çağırır; browser JS Platform portuna (**5057**)
doğrudan **gitmez** (`platform-lookups-reference-data.md` kuralı). Tarayıcı tarafı **same-origin proxy**
(`/Platform/WorkingCalendars/api/...`) kullanır.

**8.10 Localization — Platform = 2 dil.** `.resx` yalnız **`en` + `tr`** (PSS domain-config §Runtime
Decisions: *"Platform tarafı için yalnızca en + tr"*). CRM'in 7 dil standardı **burada geçerli değildir**.

---

## 9. Layout & Shell Contract

Bu modülün **İKİ** UI yüzeyi vardır (2026-08-27 kararı). İkisi **ayrı klasör, ayrı layout, ayrı permission,
ayrı RESX dili** kullanır; ortak olan tek şey **aynı aggregate** ve **aynı contract**'tır.

### 9.1 Yüzey ① — Platform Admin → Working Calendars (`shell: platform-admin`)

**Kapsam:** **yalnız ülke katmanı** (`ScopeType=country`, `TenantId=null`) — hafta sonu tanımı + resmî/dinî
tatiller. Platform admin bu ekranda **tenant override satırı authorlamaz**.

```cshtml
@{
    ViewData["Title"] = Localizer["PageTitle"];
    Layout = "_LayoutPlatformAdmin";   // shell: platform-admin — AÇIKÇA, _ViewStart varsayılanına GÜVENİLMEZ
}
```

- View klasörü: `Views/Platform/WorkingCalendars/` · Frontend route: `/Platform/WorkingCalendars`
- Canlı emsal: `Views/Platform/ModuleCatalog/` ve `Views/Platform/Tenants/`
- Menü: `_LayoutPlatformAdmin.cshtml` içine **tek** `<li>`, `platform.working-calendar.read` guard'ıyla
- RESX: **`en` + `tr`** (2 dil)
- **AC-UI-1** olarak test edilir

**Scope-removal (2026-08-27 kullanıcı kararı — UYGULANDI).** Bu yüzey country-only olduğu için create/edit
formunda **Scope seçici ve OrganizationUnit alanı YOKTUR**; `ScopeType` sabit `country` olarak hidden input ile
post edilir. Üç yerde birden kapatılmıştır (UI konvansiyonu değil, kural):

| Katman | Değişiklik |
|---|---|
| Contract (`BuildContract`, `TenantSlice:false`) | `ScopeTypes = WorkingCalendarScopeType.PlatformAuthorable` = **`[country]`** — `All` DEĞİL |
| Validation (`ValidateScope`) | `isPlatformActor && scopeType != country` ⇒ **400 `platform_surface_is_country_only`** |
| UI (`_Form.cshtml` / `form.js` / `Details.cshtml` / `details.js`) | Scope + OrganizationUnit kontrolleri ve Details satırları kaldırıldı; `ScopeType` RESX anahtarı düşürüldü |

- `Activate` akışı **DEĞİŞMEDİ**. Tenant override yüzeyi (§9.2, `scope ∈ {tenant, organization-unit}`) **DEĞİŞMEDİ**.
- **Golden reference `compact` KORUNUR:** §11.1 sayımı 11 → **9** (`ScopeType` + `OrganizationUnitId` düşer);
  9 > 8 ⇒ `compact`. Frontmatter `form_field_count: 9`. Slim/Compact kararı değişmez (Rebuild Guard).
- Section haritası parite'si korunur: Section 2 "Scope" kartı **kalır**, içinde yalnız `CountryCode` + `CalendarYear`
  vardır ve `Details.cshtml` ile birebir aynıdır.

**Country kaynağı.** `CountryCode` seçenekleri **`GET /api/lookups/countries`** (platform) ve
**`GET /api/lookups/reference/countries`** (tenant override) üzerinden gelir. İkisi de **aynı**
`GetLookupOptionsQuery("countries")` handler'ına iner — yani iki yüzeyin ülke kodları yapısal olarak hizalıdır ve
override, platform ülke takvimine kendiliğinden eşleşir (HybridEntity resolve). Bu, **Platform → Tenants → Create**
ekranındaki *Locale Defaults → Country* alanının bağlandığı kaynağın aynısıdır (2026-08-27 kullanıcı kararı).
Sayfa içinde hardcoded ülke listesi **yasaktır** (PSS-LOOKUPS-001).

### 9.2 Yüzey ② — Tenant Admin → Working Calendar Overrides (`shell: tenant`)

**Kapsam:** **yalnız o tenant'ın override katmanı** (`ScopeType = tenant | organization-unit`,
`TenantId = <JWT tenant>`) — şirket tatili/kapanışı, telafi günü (`working-day-override`), opsiyonel
`WeekendDays` override'ı.

```cshtml
@{
    ViewData["Title"] = Localizer["PageTitle"];
    Layout = "_LayoutTenantShell";     // shell: tenant — AÇIKÇA, _ViewStart varsayılanına GÜVENİLMEZ
}
```

- View klasörü: `Views/WorkingCalendarOverrides/` · Frontend route: `/WorkingCalendarOverrides`
- Menü: `_LayoutTenantShell.cshtml` içine **tek** `<li>`, `platform.working-calendar.override.read` guard'ıyla
- RESX: **7 dil** (`ar,en,es,fr,ru,tr,zh`) — gerekçe §11.4
- **AC-UI-9** olarak test edilir


**Country kaynağı — Yüzey ② AYRIŞTI (2026-08-27 kullanıcı kararı).** Override yüzeyi ülke seçeneklerini artık
platform provisioning lookup'ından değil, governed MOD-0048 **`COUNTRY_CODES`** (`Global` scope) setinden okur:

```text
/WorkingCalendar/Overrides/api/countries
  → GET /api/v1/reference-data/sets/COUNTRY_CODES/published-values      (scope_key YOK — global set)
  → {code, name, value, sortOrder} olarak normalize edilir (LookupOptionDto ile aynı şekil)
```

- **Yüzey ① (`/Platform/WorkingCalendars`) DEĞİŞMEDİ** — hâlâ `/api/lookups/countries`. Legal Entity ve
  Tenants → Create de değişmedi.
- **Eşleşme `CountryCode` (ISO alpha-2) ile kurulur.** Backend (`CreateWorkingCalendarHandler`) `CountryCode`'u
  hâlâ platform `countries` lookup'ına karşı doğrular → COUNTRY_CODES'ta olup platform listesinde OLMAYAN bir kod
  **400 `country_unknown`** ile reddedilir. İki liste aynı kodları taşımak zorundadır; proxy çeviri YAPMAZ.
- **Cross-tenant sızıntı yok:** okuma çağıranın kendi token'ıyla yapılır ve hiçbir yerde cache'lenmez (platform
  lookup cache'inin aksine — onun key'inde tenant segmenti yoktur).
- **Fail-closed:** set publish edilmemişse/boşsa seçenek listesi **boş** döner; hardcoded fallback
  **yasaktır** (PSS-LOOKUPS-001 + §4.7). Form açılır, kaydetmez.
- **Bilinen sınır:** BRD okumaları `TenantContext.TenantId` ile filtrelenir; `Global` scope yalnız `ScopeKey`
  eşleşmesini gevşetir, **cross-tenant depolama sağlamaz**. COUNTRY_CODES bugün yalnız tenant `…0001` altında
  durur — başka bir tenant'ın Overrides sayfası boş liste görür.

### 9.3 Tenant ülke katmanını READ-ONLY OKUR / DEĞİŞTİREMEZ — ve SONUCUNU görür

Bu, revizyonun en kritik sınırıdır ve dört ayrı yerde birden uygulanır. **2026-08-27 kullanıcı kararı** sınırı
tek bir eksende gevşetti: tenant ülke takvimlerini **okur** (resmî tatiller kamuya açık bilgidir ve tenant neyi
override ettiğini görmeden override yazamaz). **Yazma ve cross-tenant görünürlük aynen kapalı kaldı.**

| Katman | Tenant ne yapar | Nasıl uygulanır |
|---|---|---|
| Ülke satırı (`TenantId=null`) | **Authorlayamaz** — create/update/activate/archive **hiçbiri** | `ScopeType=country` tenant aktöründen gelirse **403** (§12.2) |
| Ülke satırı — liste | **Yüzey ②'de READ-ONLY LİSTELENİR** *(2026-08-27 kararı)* | Override listesi = tenant satırları + **aktif** ülke satırları; ülke satırları `isReadOnly=true` gelir ve Edit/Activate/Archive aksiyonu almaz (AC-SEC-6/6a/6b/6c) |
| Ülke satırı — detay/düzenleme | **Aktifse salt-okunur detayı erişilebilir; düzenlenemez** | `GET /overrides/{id}` önce own override'ı, yoksa **aktif** country satırını dener ve `isReadOnly=true` ile **200** döner. Draft/archived country satırı GET'te **404**; `PUT`/activate/archive ise her durumda yalnız `GetOwnOverrideByIdAsync` kullandığı için **404** kalır (AC-SEC-7) |
| Ülke satırının **etkisi** | **GÖRÜR** — çünkü sonucu bilmeden override yazamaz | Salt-okunur **"devralınan"** paneli + çözümleme önizlemesi (aşağıda) |

**D3 devralma UI'da görünür olmak ZORUNDA.** `WeekendDays` alanı boş bırakıldığında (`null`) ekran
*"Ülke takviminden devralınıyor: Cumartesi, Pazar"* şeklinde **çözülmüş** değeri gösterir (AC-UI-11).
Aksi hâlde tenant admin boş bir çoklu-seçim görür ve "hafta sonu tanımsız" sanar — `null` ile boş dizi
arasındaki **anlamlı** farkı (§4.3/D3) kullanıcıya taşımayan bir ekran, bu modelin en kolay yanlış anlaşılan
yerini gizler.

Aynı ekranda **çözümleme önizlemesi** (§4.5 sırası) ülke + override **birleşimini** gösterir: seçilen tarih
için `Resolution` + `ReasonCodes` + hangi katmandan geldiği (`tenant_override_applied` /
`weekend_inherited_from_country` / `public_holiday`) — **AC-UI-12**. Bu, "ülke katmanını göremez" kuralını
ihlal etmez: tenant ülke satırının **içeriğini** (kod, ad, tüm tatil listesi) değil, kendi tarihine ilişkin
**sonucu ve gerekçesini** görür.

`_Layout.cshtml` **FROZEN** — her iki yüzeyde de dokunulmaz.

---

## 10. Backend File Convention

Golden Reference Compact (DEV-0001) **naming**'i birebir; canlı Platform emsali `Features/Lookups/Handlers/QueryHandlers/`:

```text
services/Diten.Platform/src/Diten.Platform.Application/Features/WorkingCalendar/
├── Commands/
│   ├── CreateWorkingCalendarCommand.cs          (sealed record)
│   ├── UpdateWorkingCalendarCommand.cs
│   ├── ActivateWorkingCalendarCommand.cs
│   ├── ArchiveWorkingCalendarCommand.cs
│   ├── UpsertWorkingCalendarDayCommand.cs
│   └── ArchiveWorkingCalendarDayCommand.cs
├── Queries/
│   ├── ListWorkingCalendarsQuery.cs             (sealed record)
│   ├── GetWorkingCalendarByIdQuery.cs
│   ├── GetWorkingCalendarContractQuery.cs
│   └── ResolveWorkingDayQuery.cs
├── Handlers/
│   ├── CommandHandlers/                         ← AYRI klasör (ZORUNLU)
│   │   ├── CreateWorkingCalendarHandler.cs      (sealed class, Command/Query suffix YOK)
│   │   ├── UpdateWorkingCalendarHandler.cs
│   │   ├── ActivateWorkingCalendarHandler.cs
│   │   ├── ArchiveWorkingCalendarHandler.cs
│   │   ├── UpsertWorkingCalendarDayHandler.cs
│   │   └── ArchiveWorkingCalendarDayHandler.cs
│   └── QueryHandlers/                           ← AYRI klasör (ZORUNLU)
│       ├── ListWorkingCalendarsHandler.cs
│       ├── GetWorkingCalendarByIdHandler.cs
│       ├── GetWorkingCalendarContractHandler.cs
│       └── ResolveWorkingDayHandler.cs
├── Validators/
│   ├── CreateWorkingCalendarValidator.cs        (Command suffix YOK)
│   ├── UpdateWorkingCalendarValidator.cs
│   └── UpsertWorkingCalendarDayValidator.cs
├── Provider/
│   ├── IWorkingCalendarProvider.cs              (§4.5 seam)
│   ├── WorkingCalendarResolveEngine.cs          (deterministik çözümleme sırası — TEK yer)
│   └── WorkingCalendarContracts.cs              (WorkingDayResult / HolidayInfo / Scope / ReasonCodes)
├── WorkingCalendarPermissions.cs
├── WorkingCalendarValidation.cs                 (paylaşılan guard'lar — TEK yer, iki kopya YASAK)
└── WorkingCalendarModels.cs                     ← TEK dosyada tüm DTO/ViewModel'ler
```

**Naming:** Command = `{Verb}WorkingCalendar[Day]Command` (record) · Query = `{Get|List|Resolve}…Query`
(record) · Handler = `{Verb}…Handler` (class, **suffix YOK**) · Validator = `{Verb}…Validator` (**suffix YOK**).

> **⚠️ BEYAN EDİLEN İKİ SAPMA (gizlenmiyor):**
> 1. **`DeleteWorkingCalendarCommand` ve `BulkDeleteWorkingCalendarCommand` YOKTUR** (§8.3). Golden Reference
>    DataTable modülünde bunları bekler. Sonuç: `verify_datatable_page.py`'ın bulk-delete kontrolleri
>    **EXPECTED N/A** verecektir (MOD-0162-FU02 emsali) — beklenen sayı §17'de **koşumdan önce** ilan edilir.
> 2. **`Provider/` alt klasörü** Golden Reference'ta yoktur. Çözümleme motorunu handler'lara gömmek
>    `handler-design.md` sınırını ihlal eder ve **aynı sıranın iki kopyası** riskini doğurur (§4.5 sırası
>    tek bir yerde yaşamak zorundadır). MOD-0165-FU03'ün `Resolve/` klasörü ve MOD-0164-FU02'nin
>    `Evaluation/` klasörü aynı deseni zaten kullanıyor — **emsal var**.

**İKİ controller — tek Feature klasörü (2026-08-27 revizyonu).** Yukarıdaki klasör **ikiye bölünmez**:
komut, sorgu, handler, validator ve provider **tek** `Features/WorkingCalendar/` altında kalır. Bölünen tek
katman **API controller**'dır:

```text
src/Diten.Platform.API/Controllers/
├── WorkingCalendarsController.cs           [Authorize(Policy = "PlatformActor")]  → ülke katmanı
└── WorkingCalendarOverridesController.cs   [Authorize]                            → tenant override katmanı
```

Gerekçe **teknik zorunluluktur, tercih değil** (§19.2/1): sınıf seviyesindeki `PlatformActor` policy'si
tenant aktörünü 403'ler ve **aksiyon seviyesindeki `[Authorize]` sınıf policy'sini gevşetemez**. Aynı
komut/handler'lar **her iki** controller'dan çağrılır; scope guard'ı (§12.2 + §12.6) tek yerde —
`WorkingCalendarValidation` — yaşar, **iki kopya YASAK**.

---

## 11. Frontend File Contract

> **İKİ yüzey ⇒ İKİ dosya seti ⇒ İKİ verifier koşusu.** §11.1–11.2 Platform Admin yüzeyini, §11.3–11.4
> Tenant Override yüzeyini tanımlar. Her yüzeyin **kendi** klasörü, **kendi** `_Form`'u ve **tek** DataTable'ı
> vardır; bu yüzden MOD-0162-FU03'ün hibrit-tek-sayfa verifier problemi burada **yapısal olarak oluşmaz**.

### 11.1 Yüzey ① golden reference kararı — kullanıcı-form alanı türetmesi (GÖSTERİLİR)

Sayım kuralı (`module-pack-standard.md` §3): yalnız kullanıcının create/edit formunda **doldurduğu** modül
alanları sayılır. `Id`, `TenantId`, audit alanları, **türetilmiş** alanlar, aksiyon-doldurmalı alanlar
(`ActivatedAt/By`, `ArchivedAt/By`) ve DataTable checkbox/action kolonları **sayılmaz**.

**Golden-reference yüzeyi ① — `WorkingCalendar` (Platform Admin):** §4.2'deki 17 satırdan form-dışı olanlar
düşüldükten sonra kalan **11**:

| # | Kullanıcı-form alanı | # | Kullanıcı-form alanı |
|---|---|---|---|
| 1 | `CalendarCode` | 7 | `OrganizationUnitId` (`ScopeType`'a bağlı, koşullu görünür) |
| 2 | `CalendarName` | 8 | `WeekendDays` (multi-select — **tek kontrol**) |
| 3 | `Description` | 9 | `CalendarStatus` |
| 4 | `CountryCode` (MOD-0048 `countries` seçici) | 10 | `Source` |
| 5 | `CalendarYear` | 11 | `Notes` |
| 6 | `ScopeType` | | |

*Form-dışı (6 + türetilmişler):* `Id` · `TenantId` (**JWT/scope'tan türetilir, forma ASLA konmaz**) ·
`Days` (alt-editör, alan değil) · `ActivatedAt/By` · `ArchivedAt/By` · `CreatedAt/By` · `UpdatedAt/By` ·
`IsDeleted` · `BaseEntity.Version`.

→ **11 > 8 ⇒ `golden_reference: compact`** (frontmatter `form_field_count: 11`).

**Gömülü gün alt-editörü — ayrı golden-reference yüzeyi DEĞİLDİR** (MOD-0162-FU05/S2 emsali). Gün, kendi
sayfası/DataTable'ı olan bağımsız bir modül değil, takvim formunun **içindeki bir repeater**dır; **ikinci bir
Slim/Compact kararı doğurmaz** ve **ikinci bir verifier koşusu gerektirmez**. Tamlık için alan sayımı (**8** —
`DayId` üretilir, `DayStatus` archive aksiyonudur):

| # | Alt-editör alanı | # | Alt-editör alanı |
|---|---|---|---|
| 1 | `DayCode` | 5 | `DayType` |
| 2 | `DayName` | 6 | `Recurrence` |
| 3 | `Date` | 7 | `IsHalfDay` |
| 4 | `ObservedDate` | 8 | `Notes` |

**Sonuç:** modülde **tek** golden-reference yüzeyi, **tek** klasör, **tek** verifier koşusu vardır ve **hiç
Slim dosyası yoktur**.

### 11.2 Yüzey ① dosya seti — TEK klasör, kanonik Compact 9 dosya (TEK TEK enumerasyon)

**`Views/Platform/WorkingCalendars/` (DEV-0001 Compact — tam ve tek set):**

| # | Dosya | Rol |
|---|---|---|
| 1 | `Index.cshtml` | Liste kabuğu; `Layout = "_LayoutPlatformAdmin"` **açıkça**; bölüm sırası ① Filter → ② BulkActionBar → ③ DataTable |
| 2 | `Create.cshtml` | **Compact-özel** sayfa kabuğu + `_Form` |
| 3 | `Edit.cshtml` | **Compact-özel** sayfa kabuğu + `_Form` |
| 4 | `Details.cshtml` | **Compact-özel** detay sayfası; salt-okunur gün listesi (`Date` sıralı, `DayType` rozetli) + `activate` / `archive` aksiyonları + **çözümleme önizlemesi** (bir tarih seç → `WorkingDayResult` + `ReasonCodes`) |
| 5 | `_Form.cshtml` | Create/Edit ortak formu: takvimin **11 alanı** + **gömülü gün alt-editörü (repeater)** — ayrı partial açılmaz, klasör kanonik 9 dosyada kalır |
| 6 | `_Filter.cshtml` | Inline collapsible filter: `countryCode` · `calendarYear` · `scopeType` · `calendarStatus` · `organizationUnitId` · `includeArchived` |
| 7 | `_DataTable.cshtml` | `data-dt-standard="v2"` + skeleton loader; **TEK** DataTable; kolonlar: kod · ülke · yıl · kapsam · hafta sonu · **gün sayısı rozeti** · statü |
| 8 | `_IndexL10n.cshtml` | JSON payload bridge |
| 9 | `WorkingCalendarsIndex.cs` | Marker class (RESX kökü) |

**JS (Golden Compact seti — 3 dosya):**

```text
wwwroot/assets/js/Platform/WorkingCalendars/index.js       → DataTable (DtDefaults + v2), filtre, activate/archive
wwwroot/assets/js/Platform/WorkingCalendars/index.l10n.js  → camelCase→PascalCase L10n köprüsü
wwwroot/assets/js/Platform/WorkingCalendars/form.js        → ülke seçici + ScopeType↔OrganizationUnit zinciri + GÜN repeater
```

`index.l10n.js` **camelCase→PascalCase** dönüşümünü atlamaz (aksi hâlde `window.L10n` anahtarları `undefined`
döner, toast `"(undefined: corrId)"` olur). API profili **`proxy`** (same-origin); browser **5057'yi çağırmaz**.
Sayfada **tek** DataTable vardır → `updateVisualState` global selector çakışması **yapısal olarak yoktur**.

**RESX (tek klasör × 2 dil — Platform standardı):**

```text
Resources/Views/Platform/WorkingCalendars/WorkingCalendarsIndex.{en,tr}.resx
Resources/SharedResource.{en,tr}.resx        → WorkingCalendarsMenu
```

**YASAK dosyalar:** `_CreateEditOffcanvas.cshtml` · `_DetailsQuickView.cshtml` (**Compact yasağı**) ·
`Views/Platform/WorkingCalendarDays/**` (gün ayrı yüzey değil) · `Views/CRM/**` (bu CRM modülü **değildir**) ·
Index içinde create/edit offcanvas · **hardcoded ülke veya vokabüler listesi**.

**Kullanılan mevcut yüzeyler (yeni dosya değil):** ülke seçici `GET /api/lookups/countries`'i, org-unit
seçici `GET /api/platform/organization-units`'i **proxy üzerinden** okur — bu modüllerin
view/JS/controller dosyalarına **dokunulmaz** (§6).

### 11.3 Yüzey ② golden reference kararı — Tenant Override (TÜRETİLDİ, GÖSTERİLİR)

Tenant override formu **aynı aggregate'i** yazar ama alanların bir kısmı bu yüzeyde **yapısal olarak
düşer**. Düşme gerekçeleri (sayıyı tutturmak için değil, **anlam** gereği):

| Düşen alan | Neden bu yüzeyde form alanı DEĞİL |
|---|---|
| `Source` | Tenant için **tek yasal değer** `manual`'dır (`imported`/`provider-fetch` platform + FU02 işidir, §12.1/V10) → server-set |
| `CalendarStatus` | Statü **aksiyonla** değişir (`activate` / `archive` endpoint'leri, §12.3) — seçim kutusu değil |
| `TenantId` | JWT'den türetilir, forma **asla** konmaz (§8.2) |

Kalan **9** alan:

| # | Kullanıcı-form alanı | # | Kullanıcı-form alanı |
|---|---|---|---|
| 1 | `CalendarCode` | 6 | `ScopeType` (**yalnız** `tenant` / `organization-unit` — `country` seçilemez) |
| 2 | `CalendarName` | 7 | `OrganizationUnitId` (`ScopeType`'a bağlı, koşullu) |
| 3 | `Description` | 8 | `WeekendDays` (opsiyonel override — boş ⇒ **devral**, §9.3) |
| 4 | `CountryCode` (hangi ülke katmanının üstüne biner) | 9 | `Notes` |
| 5 | `CalendarYear` | | |

→ **9 > 8 ⇒ `golden_reference: compact`** (`form_field_count_secondary: 9`).

> **⚠️ Beklentiden sapma — dürüst rapor.** Revizyon talimatı bu yüzey için *"muhtemelen Slim — override az
> alan"* diyordu. Türetme **9** veriyor, yani eşiğin **bir üstünde**; kural mekaniktir (`>8 ⇒ compact`), bu
> yüzden **Slim'e zorlamadım**. Slim'e inmenin tek yolu `Description`'ı formdan çıkarmaktır (9 → 8) — bu
> teknik olarak savunulabilir (kayıtta hem `Description` hem `Notes` var ve kısa bir şirket-kapanış satırı
> için ikisi fazladan gelir), ama **eşiği tutturmak için** alan düşürmek yanlış bir gerekçedir.
> **Compact önerimin bağımsız nedeni:** iki yüzey **aynı** aggregate'i, **aynı** validator'ları ve **aynı**
> vokabüleri paylaşır; birini Compact diğerini Slim yapmak, aynı varlık için iki farklı form idiomu
> (ayrı sayfa vs offcanvas) demektir ve bakım maliyetini kalıcı olarak artırır.
> `Description`'ı düşürüp Slim'e inmek isterseniz bu **tek satırlık** bir değişikliktir → §20/**D-OVR-UI**.

**Gömülü gün alt-editörü** bu yüzeyde de **ayrı bir golden-reference yüzeyi DEĞİLDİR** (§11.1 ile aynı
gerekçe); aynı 8 alanlı repeater kullanılır, tek farkla: tenant tarafında `DayType` seçenekleri
`company-holiday` · `company-closure` · `working-day-override` ile **sınırlıdır** (`public-holiday` /
`religious-holiday` / `moveable-holiday` ülke katmanına aittir → §12.6).

### 11.4 Yüzey ② dosya seti — TEK klasör, kanonik Compact 9 dosya (TEK TEK enumerasyon)

**`Views/WorkingCalendarOverrides/` (DEV-0001 Compact — tam ve tek set):**

| # | Dosya | Rol |
|---|---|---|
| 1 | `Index.cshtml` | Liste kabuğu; `Layout = "_LayoutTenantShell"` **açıkça**; ① Filter → ② BulkActionBar → ③ DataTable |
| 2 | `Create.cshtml` | **Compact-özel** sayfa kabuğu + `_Form` |
| 3 | `Edit.cshtml` | **Compact-özel** sayfa kabuğu + `_Form` |
| 4 | `Details.cshtml` | **Compact-özel**; salt-okunur gün listesi + **devralınan ülke değerleri paneli** (§9.3) + **çözümleme önizlemesi** (ülke+override birleşimi) + `activate` / `archive` |
| 5 | `_Form.cshtml` | Create/Edit ortak formu: override'ın **9 alanı** + gömülü gün repeater'ı (tenant `DayType` alt kümesi) + `WeekendDays` boşken **devralınan değeri gösteren** salt-okunur satır |
| 6 | `_Filter.cshtml` | `countryCode` · `calendarYear` · `scopeType` · `organizationUnitId` · `calendarStatus` · `includeArchived` |
| 7 | `_DataTable.cshtml` | `data-dt-standard="v2"` + skeleton loader; **TEK** DataTable; kolonlar: kod · ülke · yıl · kapsam · **hafta sonu (devralındı/override)** rozeti · gün sayısı · statü |
| 8 | `_IndexL10n.cshtml` | JSON payload bridge |
| 9 | `WorkingCalendarOverridesIndex.cs` | Marker class (RESX kökü) |

**JS (Golden Compact seti — 3 dosya):**

```text
wwwroot/assets/js/WorkingCalendarOverrides/index.js       → DataTable (DtDefaults + v2), filtre, activate/archive
wwwroot/assets/js/WorkingCalendarOverrides/index.l10n.js  → camelCase→PascalCase L10n köprüsü
wwwroot/assets/js/WorkingCalendarOverrides/form.js        → ülke seçici + ScopeType↔OrganizationUnit zinciri
                                                            + GÜN repeater + DEVRALMA göstergesi
```

**RESX — 7 dil (tenant shell), 2 dil DEĞİL:**

```text
Resources/Views/WorkingCalendarOverrides/WorkingCalendarOverridesIndex.{ar,en,es,fr,ru,tr,zh}.resx
Resources/SharedResource.{ar,en,es,fr,ru,tr,zh}.resx        → WorkingCalendarOverridesMenu
```

> **Dil sayısı domain'e değil, SHELL'e bağlıdır — kod üzerinden doğrulandı.** PSS `domain-config.md`
> *"Platform tarafı için yalnızca en + tr"* der; ama bu kural **platform shell**'i kasteder. Aynı PSS
> domain'indeki MOD-0028/0029 **tenant shell** ekranları 7 dil taşır:
> `Resources/Views/DocumentManagement/ControlledDocuments/ControlledDocumentsIndex.{ar,en,es,fr,ru,tr,zh}.resx`
> (7 dosya) — buna karşılık `Resources/Views/Platform/Tenants/TenantsIndex.{en,tr}.resx` (2 dosya).
> Bu yüzden yüzey ① **2 dil**, yüzey ② **7 dil**tir. Tek bir kurala indirgemek RESX parite gate'ini
> **kesin olarak** kırardı.

**YASAK dosyalar (yüzey ②):** `_CreateEditOffcanvas.cshtml` · `_DetailsQuickView.cshtml` (**Compact
yasağı**) · `Views/Platform/**` altına tenant view'ı koymak · ülke katmanını listeleyen herhangi bir view/JS
yolu · **hardcoded ülke veya vokabüler listesi**.

**Kullanılan mevcut yüzeyler (yeni dosya değil):** ülke seçici tenant aktörü için
**`GET /api/lookups/reference/countries`**'i kullanır — `TenantReferenceLookupsController`, `[Authorize]`
(herhangi bir kimlikli aktör). **`/api/lookups/countries` KULLANILAMAZ**: o controller
`[Authorize(Policy = "PlatformActor")]`'dır ve tenant aktörünü **403**'ler (§19.2/1). Org-unit seçici
`GET /api/platform/organization-units`'i proxy üzerinden okur.

---

## 12. Validation Rules

### 12.1 Alan bazlı

| # | Field | Required | Format / Rule | DB-level | Pre-check |
|---|---|---|---|---|---|
| V1 | `CalendarCode` | Evet | Trim, max 64, `^[A-Za-z0-9._-]+$` | Unique **partial** (scope içinde, **yalnız `draft`/`active`**) | `ExistsByCodeAsync` |
| V2 | `CalendarName` | Evet | Trim, max 200 | — | — |
| V3 | `Description` | Hayır | Max 1000 | — | — |
| V4 | `CountryCode` | Evet | ISO-3166-1 alpha-2, upper-case normalize | — | **MOD-0048 `"countries"`** (§4.7); yoksa **400** `country_unknown` |
| V5 | `CalendarYear` | Evet | `1900 <= yıl <= 2200` | — | — |
| V6 | `ScopeType` | Evet | In-domain set | — | `TenantId` ile tutarlılık (§12.2) |
| V7 | `OrganizationUnitId` | Koşullu | `ScopeType=organization-unit` ⇒ zorunlu; aksi hâlde **boş olmalı** | — | `OrganizationUnit` **var mı + aynı tenant mı** (D4) |
| V8 | `WeekendDays` | Koşullu | In-domain gün adları, **tekrarsız**, max 7 | — | **`ScopeType=country` ⇒ ZORUNLU ve boş olamaz**; override'da `null` = devral |
| V9 | `CalendarStatus` | Evet | In-domain + §12.3 geçişi | — | — |
| V10 | `Source` | Evet | In-domain; **v1'de yalnız `manual`** yazılabilir | — | `provider-fetch` → **400** (**rezerve**, üreticisi FU02) |
| V11 | `Notes` | Hayır | Max 2000 | — | — |
| V12 | `DayCode` | Evet | Trim, max 64; takvim içinde **unique** | **YOK** (dizi-içi index yok) | Handler + validator **tek savunma hattı** (§19/3) |
| V13 | `DayName` | Evet | Trim, max 200 | — | — |
| V14 | `Date` | Evet | Yılı **`CalendarYear` ile aynı** | — | Farklıysa **400** `day_year_mismatch` |
| V15 | `ObservedDate` | Hayır | Verilirse yılı `CalendarYear` ile aynı | — | — |
| V16 | `DayType` | Evet | In-domain set | — | — |
| V17 | `Recurrence` | Evet | In-domain set | — | — |
| V18 | `IsHalfDay` | Hayır | `working-day-override` ile **birlikte kullanılamaz** | — | **400** `half_day_on_override` |
| V19 | `Notes` (gün) | Hayır | Max 500 | — | — |

### 12.2 Scope ↔ `TenantId` tutarlılığı (fail-closed — en kritik kural)

| `ScopeType` | `TenantId` | Aktör | Aksi hâlde |
|---|---|---|---|
| `country` | **`null` olmak ZORUNDA** | **`platform_admin`** | **403** `country_scope_requires_platform_actor` |
| `tenant` | JWT claim'inden **dolu** | tenant aktörü | `null` kalırsa **400** `tenant_scope_requires_tenant` |
| `organization-unit` | JWT claim'inden **dolu** + `OrganizationUnitId` dolu | tenant aktörü | **400** `org_scope_requires_organization_unit` |

`TenantId` **hiçbir koşulda** request payload'ından okunmaz (§8.2). `ScopeType` bir kez kaydedildikten sonra
**değiştirilemez** (§8.6) — bir tenant satırını global'e terfi ettirmek, sessizce cross-tenant veri
yayınlamak demektir.

### 12.3 Statü geçiş kuralları

```text
draft ──────► active ──────► archived        (archived TERMINAL; unarchive YOK)
  │                             ▲
  └─────────────────────────────┘
```

- `activate`: `WeekendDays` **boş olamaz** (ülke katmanında) ve takvimde **en az 0 gün** olabilir (tatili
  olmayan takvim geçerlidir) → **400** `weekend_days_required`.
- `active` iken **donan** alanlar: `CountryCode` · `CalendarYear` · `ScopeType` · `OrganizationUnitId`
  → değiştirilirse **409** `calendar_identity_frozen` (§8.6).
- `active` iken **değişebilen**: `WeekendDays` · `Days` · `CalendarName` · `Description` · `Notes`.
- `archived` satır **hiçbir** update/activate kabul etmez → **409** `calendar_archived`.
- **Aynı scope + ülke + yıl için aynı anda birden fazla `active` takvim olamaz** → **409**
  `active_calendar_already_exists`. (Provider'ın deterministik olabilmesi bunu şart koşar.)

### 12.4 Gün kuralları

| # | Kural | Sonuç |
|---|---|---|
| V20 | Aynı takvimde aynı **etkin tarihe** (`ObservedDate ?? Date`) iki **aktif** gün | **409** `duplicate_day_date` |
| V21 | `Date` yılı ≠ `CalendarYear` | **400** `day_year_mismatch` |
| V22 | Gün sayısı üst sınırı **400/takvim** | **400** `day_limit_exceeded` (doküman büyümesi — §19/5) |
| V23 | `archived` gün | Provider çözümlemesinde **aday değildir** |

### 12.5 Provider çözümleme kuralları (§4.5 sırası — test edilir)

| # | Senaryo | Beklenen |
|---|---|---|
| V24 | Tenant override'ta `working-day-override`, ülkede tatil | **çalışma günü** + `working_day_override_applied` + `tenant_override_applied` |
| V25 | Ülkede tatil, tenant override yok | **çalışma günü değil** + `public_holiday` |
| V26 | Tenant `WeekendDays = null` | Ülke hafta sonu **devralınır** + `weekend_inherited_from_country` |
| V27 | Tenant `WeekendDays = [friday, saturday]`, ülke `[saturday, sunday]` | **Tenant kazanır** (devralma yok) |
| V28 | `IsHalfDay = true` bir tatil | **çalışma günü** + `half_day_treated_as_working` (D5) |
| V29 | O ülke/yıl için `active` takvim yok | `Resolution = calendar_missing`, değer **`null`**, **varsayım yok** |
| V30 | `CountryCode` MOD-0048'de yok | `Resolution = country_unknown`, değer **`null`** |
| V31 | `AddWorkingDays(start, 0)` | `start` **çalışma günü ise** kendisi; değilse **`nextWorkingDay`** (sözleşmede yazılı, keyfi değil) |
| V32 | `WorkingDaysBetween(a, b)` ile `a > b` | **400** `invalid_date_range` (negatif sayı uydurulmaz) |
| V33 | Aralık birden çok **yıla** yayılıyor ve bir yılın takvimi yok | `Resolution = year_missing`, değer **`null`** — kısmi sayı **döndürülmez** |

### 12.6 Override yüzeyi kısıtları (2026-08-27 revizyonu)

Tenant override yüzeyi aynı aggregate'i yazar ama **daha dar** bir sözleşmeye tabidir. Bu kurallar
**backend'de** uygulanır — UI'da gizlemek yeterli değildir (AC-SEC-7/8):

| # | Kural | Sonuç |
|---|---|---|
| V34 | Override yüzeyinden `ScopeType = country` | **403** `country_scope_requires_platform_actor` (§12.2 ile aynı guard) |
| V35 | Override satırında `DayType ∈ { public-holiday, religious-holiday, moveable-holiday }` | **400** `day_type_reserved_for_country_layer` — resmî/dinî tatil **ülke katmanının gerçeğidir**; tenant onu kendi katmanında **yeniden tanımlayamaz** (yoksa iki farklı "resmî tatil" doğar) |
| V36 | Override satırında izinli `DayType` | `company-holiday` · `company-closure` · **`working-day-override`** (telafi günü) |
| V37 | Override `WeekendDays = null` | **Geçerli ve anlamlıdır** — ülke katmanından devralınır (§9.3, D3). Boş dizi `[]` ise "hafta sonu yok" demektir ve **devralma değildir** |
| V38 | Override `CountryCode`'u için **aktif ülke takvimi yok** | Override **yine de kaydedilir** (draft/active), fakat çözümleme `calendar_missing` döner ve UI bunu **uyarı** olarak gösterir. Kayıt engellenmez: tenant, ülke takvimi girilmeden önce şirket kapanışını **planlayabilmelidir** |
| V39 | Bir tenant'ın aynı ülke+yıl+scope için ikinci `active` override'ı | **409** `active_calendar_already_exists` (§12.3 ile aynı kural, tenant scope'unda) |

---

## 13. Failure Path to Verify

- **Duplicate `CalendarCode`** (aynı scope+ülke+yıl) → **409** + field-level hata + kayıt **oluşmaz**
- **Missing `CountryCode` / `CalendarYear` / `ScopeType`** → **400** + validator mesajı
- **Bilinmeyen ülke** (`CountryCode` MOD-0048'de yok) → **400** `country_unknown`; **hardcoded fallback yok**
- **Set dışı vokabüler** (`DayType = "xyz"`) → **400** `unsupported_vocabulary_value`
- **Concurrency conflict** (eski `Version`) → **409** + UI "veri değişti, yeniden yükleyin"; sessiz overwrite **YOK**
- **Unauthorized actor** (`platform.working-calendar.manage` yok) → **403** + UI aksiyonu disabled
- **`country` scope'u tenant aktörüyle** → **403** `country_scope_requires_platform_actor` (§12.2)
- **`TenantId` payload'da gönderilirse** → **yok sayılır** (400 değil, **ignore**) ve satır JWT tenant'ına yazılır
- **Cross-tenant erişim** (başka tenant'ın override takvimi) → **404**
- **İkinci `active` takvim** (aynı scope+ülke+yıl) → **409** `active_calendar_already_exists`
- **`active` takvimde `CountryCode` değişimi** → **409** `calendar_identity_frozen`
- **`archived` takvimde update/activate** → **409** `calendar_archived`
- **Gün yılı takvim yılıyla uyuşmuyor** → **400** `day_year_mismatch`
- **Aynı etkin tarihe ikinci aktif gün** → **409** `duplicate_day_date`
- **Var olmayan / başka tenant'ın `OrganizationUnitId`'si** → **400** `organization_unit_not_found`
- **`Source = provider-fetch` yazma denemesi** → **400** (**FU02 rezervi**)
- **Provider: takvim yok** → **500 DEĞİL**; `Resolution = calendar_missing` + değer `null` (*controlled*)
- **Provider: `a > b` aralığı** → **400** `invalid_date_range`

---

## 14. Authorization Convention

**İKİ ayrı controller, İKİ ayrı policy — bu bir tercih değil, teknik zorunluluk (§19.2/1):**

```text
── Controller ① WorkingCalendarsController  (ÜLKE katmanı) ──────────────────────
Policy:     [Authorize(Policy = "PlatformActor")]              // shell: platform-admin
Permission: [HasPermission("platform.working-calendar.{read|manage|activate}")]
Actor:      platform_admin

── Controller ② WorkingCalendarOverridesController  (TENANT override katmanı) ───
Policy:     [Authorize]                                        // shell: tenant — HERHANGİ kimlikli aktör
Permission: [HasPermission("platform.working-calendar.override.{read|manage}")]
Actor:      tenant_user (platform_admin da geçer)

PKS-001: lowercase-dotted, >= 3 segment, kebab-case multiword. `.override.` 4. segmenti kuralı BOZMAZ.
```

| Permission | Kapsadığı endpoint'ler | Katman |
|---|---|---|
| `platform.working-calendar.read` | list · get-by-id · contract · resolve | Ülke |
| `platform.working-calendar.manage` | create · update · archive · gün upsert/archive | Ülke |
| `platform.working-calendar.activate` | **activate** (ayrı anahtar — aşağıdaki gerekçe) | Ülke |
| **`platform.working-calendar.override.read`** | override list · get-by-id · contract · **resolve** | **Tenant** |
| **`platform.working-calendar.override.manage`** | override create · update · **activate** · archive · gün upsert/archive | **Tenant** |

**`activate` neden ülke katmanında ayrı, override katmanında ayrı DEĞİL:** bir **ülke** takvimini aktive
etmek, o ülkedeki **her tenant'ın** çalışma günü hesabını değiştirir — sistemdeki en geniş yayılma alanına
sahip aksiyonlardan biridir ve yazma yetkisiyle aynı anahtara düşmesi, bir veri giriş hatasının doğrudan
üretime akması demektir. Bir **override**'ı aktive etmek ise yalnız **o tenant'ı** etkiler; yayılma alanı
zaten tenant sınırıyla çevrilidir, dolayısıyla ayrı bir SoD anahtarı **hak edilmiş bir maliyet değildir**.
Bu asimetri bilinçlidir.

**`.override.read` neden ayrı bir anahtar (`.read`'i tenant'a vermek yerine):** `platform.working-calendar.read`
ülke katmanının **tamamını** (tüm ülkeler, tüm tatil listeleri) açar. Tenant'a bu anahtarı vermek, §9.3'teki
*"tenant ülke katmanını göremez"* kuralını **permission seviyesinde** çiğnerdi. Ayrı anahtar, kuralı
RBAC'a taşır — UI'da gizlemeye bağlı kalmaz.

**Bu pack hiçbir permission seed etmez, hiçbir role grant yazmaz.** Katalog `platform.working-calendar.*`
anahtarlarının **hiçbirini** (override dâhil) taşımadığı için **her iki yüzeyin de** endpoint'leri ilk
açılışta **403** verecektir — bu **beklenen** durumdur ve **F-RBAC** ile kapanır. Fallback anahtar
**kullanılmaz**: mevcut hiçbir Platform permission'ı bu kaynağın yayılma alanına uygun bir vekil değildir;
`platform.lookups.read` gibi bir anahtara yaslanmak **yanlış olurdu**.

> **Not — `Platform.*` PascalCase yasağı.** Kod tabanında iki nesil var: legacy
> `Platform.BusinessReferenceData.*` (PascalCase) ve PKS-001 `platform.administrators.*` / `platform.audit.*`
> (lowercase). **Yeni iş yalnız lowercase-dotted yazar.** PSS `domain-config.md`'deki
> `[HasPermission("Platform.X.Y")]` örneği **eskimiştir**; PKS-001 otoritedir.

---

## 15. Gateway / API Routing Decision

**Karar: Gateway değişikliği GEREKLİDİR.**

`ocelot.json` **doğrulandı**: Platform route'ları **kaynak bazlı explicit çiftler** hâlinde tanımlı
(`/api/platform/organization-units`, `/api/platform/positions`, `/api/platform/position-assignments`,
`/api/platform/navigation/{everything}`, `/api/platform/tenant-security/{everything}`,
`/api/platform/administrators`, … ) ve **`/api/platform/{everything}` catch-all YOKTUR**. Dolayısıyla
`/api/platform/working-calendars` Gateway'e **eklenmeden 404** alır.

```text
Gerekli route çifti (integration-agent task'ı — bu pack ocelot.json'a YAZMAZ):
  /api/platform/working-calendars                → Diten.Platform
  /api/platform/working-calendars/{everything}   → Diten.Platform
  Metotlar: GET, POST, PUT, PATCH, OPTIONS   (DELETE YOK — §8.3)
```

`/api/lookups/{everything}` route'u **zaten mevcuttur** (`ocelot.json:1058`) → ülke seçici için Gateway
değişikliği **gerekmez**.

**Endpoint yüzeyi (12) — İKİ controller:**

*Controller ① — ülke katmanı (`PlatformActor`):*

| Metot | Route | Permission |
|---|---|---|
| GET | `/api/platform/working-calendars/contract` | `read` |
| GET | `/api/platform/working-calendars` | `read` |
| GET | `/api/platform/working-calendars/{id:guid}` | `read` |
| **GET** | **`/api/platform/working-calendars/resolve`** (`?date=&countryCode=&organizationUnitId=&op=`) | `read` |
| POST | `/api/platform/working-calendars` | `manage` |
| PUT | `/api/platform/working-calendars/{id:guid}` | `manage` |
| POST | `/api/platform/working-calendars/{id:guid}/{activate\|archive\|days\|days/{dayId}/archive}` | `activate` / `manage` |

*Controller ② — tenant override katmanı (`[Authorize]`, tenant aktörü):*

| Metot | Route | Permission |
|---|---|---|
| GET | `/api/platform/working-calendars/overrides/contract` | `override.read` |
| GET | `/api/platform/working-calendars/overrides` | `override.read` |
| GET | `/api/platform/working-calendars/overrides/{id:guid}` | `override.read` |
| **GET** | **`/api/platform/working-calendars/overrides/resolve`** (ülke+override **birleşimi**) | `override.read` |
| POST | `/api/platform/working-calendars/overrides` | `override.manage` |
| PUT | `/api/platform/working-calendars/overrides/{id:guid}` | `override.manage` |
| POST | `/api/platform/working-calendars/overrides/{id:guid}/{activate\|archive\|days\|days/{dayId}/archive}` | `override.manage` |

**Gateway açısından bedava:** override route'ları `/api/platform/working-calendars/{everything}` çiftinin
**altına** düşer → **ek route çifti GEREKMEZ**. Bu, `TenantReferenceLookupsController`'ın
`/api/lookups/reference/…` alt-yolunu seçerken kullandığı **aynı** taktiktir (*"Sits under the existing
'/api/lookups/{everything}' gateway route (sub-path 'reference/…'), so no gateway change is required"*).

> **⚠️ Route çakışması tuzağı.** `working-calendars/{id}` kısıtsız bırakılırsa `working-calendars/overrides`
> yolunu **yutar** ve tenant istekleri Controller ①'e (PlatformActor) düşüp **403** verir. Bu yüzden
> Controller ①'in **tüm** `{id}` parametreleri **`{id:guid}`** kısıtıyla yazılır (yukarıdaki tabloda
> gösterildiği gibi). Bu, teşhisi zor bir hata sınıfıdır: yetki hatası gibi görünür, aslında routing hatasıdır.

**DELETE endpoint'i yoktur** ve Gateway'de de **açılmaz**.

> **HTTP `resolve` yalnız bir vitrindir.** Asıl tüketim **in-process** `IWorkingCalendarProvider`'dır (§21).
> Aynı süreçteki bir tüketicinin Gateway üzerinden kendi servisine HTTP atması **yasaktır** (MOD-0165-FU03
> kuralı: *"no consumer re-implements the engine, and there is no HTTP self-call"*). HTTP yüzeyi
> **servis-dışı** tüketiciler (CRM, HCM, ileride PPM) ve teşhis içindir.

---

## 16. Acceptance Criteria

> Her madde §17'de **bir teste** eşlenir. Belirsiz ifade (`iyi çalışıyor`, `düzgün`) **yoktur**.

**AC-ID — kimlik hijyeni (CAND-CAP-0008)**

- [ ] **AC-ID-1** `services/`, `frontend/`, `gateway/`, `tests/` altında **`CAND-CAP-0008` dizesi hiç
      geçmez** — koleksiyon, route, permission, enum, sınıf, dosya adı veya **yorum** dâhil.
- [ ] **AC-ID-2** `py .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0008 --name "Working
      Calendar & Public Holidays"` implementasyondan **sonra da exit 0** döner (regression gate).
- [ ] **AC-ID-3** Runtime adları §başlıktaki nötr tabloyla birebir aynıdır (`working_calendars` ·
      `/api/platform/working-calendars` · `platform.working-calendar.*` · `WorkingCalendar`).

**AC-SEC — tenant sızıntısı (hibrit modelin tek gerçek riski)**

- [ ] **AC-SEC-1** `TenantId` request payload'ında gönderilse bile **yok sayılır**; tenant satırı **daima**
      JWT claim'ine yazılır.
- [ ] **AC-SEC-2** `ScopeType=country` bir tenant aktörüyle denendiğinde **403**; oluşan satır **yoktur**.
- [ ] **AC-SEC-3** A tenant'ının override takvimi, B tenant'ının list/get/resolve çağrılarında **hiçbir
      koşulda görünmez**; get **404** döner.
- [ ] **AC-SEC-4** Global satır (`TenantId=null`) **her** tenant'ın resolve çağrısında görünür — hibrit
      `ExecutionFilter`'ın `Or(TenantId==null, TenantId==current)` davranışı doğrulanır.
- [ ] **AC-SEC-5** `ScopeType` bir kez kaydedildikten sonra update ile **değiştirilemez** (409).
- [ ] **AC-SEC-6** *(2026-08-27 kullanıcı kararıyla GEVŞETİLDİ — eski hâli: "ülke satırı listeye hiç girmez")*
      **Tenant ülke katmanını READ-ONLY LİSTELER:** `GET /working-calendars/overrides` yanıtı tenant'ın kendi
      override'ları **+ AKTİF** ülke satırlarını (`TenantId=null`) döndürür; ülke satırları `isCountryLayer=true`
      **ve `isReadOnly=true`** taşır. Gerekçe: resmî tatiller kamuya açık bilgidir ve tenant neyi override
      ettiğini görmeden override yazamaz. Kapalı kalan: **yazma** (AC-SEC-7) ve **başka tenant** (AC-SEC-3/9).
- [ ] **AC-SEC-6a** **Yalnız `active` ülke satırı devralınır.** `draft`/`archived` ülke takvimi override
      listesinde **görünmez** — `includeArchived=true` ile bile. Çözülmeyen bir mirasın reklamı yapılmaz.
- [ ] **AC-SEC-6b** **`isReadOnly` bir RENDER ipucudur, yaptırım DEĞİLDİR.** Tenant by-id READ aktif ülke
      satırını ayrı `GetCountryLayerByIdAsync` fallback'iyle `isReadOnly=true` döndürebilir; UI bayrağı yok saysa
      bile write guard yalnız `GetOwnOverrideByIdAsync` kullanır → update/activate/archive **404**. Yaptırım
      backend'de, tek noktada kalır.
- [ ] **AC-SEC-6c** Override listesinde ülke satırı **Edit/Activate/Archive aksiyonu OLMADAN**, "Country
      (inherited)" rozetiyle render edilir; tenant kendi satırları düzenlenebilir kalır.
- [ ] **AC-SEC-7** **Tenant aktif ülke katmanını salt-okunur OKUR, YAZAMAZ:** aktif ülke satırının id'siyle
      `GET /overrides/{id}` → **200**, `isCountryLayer=true`, `isReadOnly=true`; draft/archived ülke id'siyle
      GET → **404**. Override endpoint'inden `ScopeType=country` → **403**; aynı aktif ülke id'siyle
      `PUT`/activate/archive → **404** (write guard country fallback'i yapmaz).
- [ ] **AC-SEC-8** Override yüzeyinden `DayType = public-holiday` (veya `religious-`/`moveable-`) → **400**
      `day_type_reserved_for_country_layer`; kural **backend'de** uygulanır, UI'da gizlemek yeterli değildir.
- [ ] **AC-SEC-9** A tenant'ının override'ı B tenant'ının override list/get/resolve çağrılarında **görünmez**;
      B'nin `PUT`/`activate` denemesi **404**.
- [ ] **AC-SEC-10** Platform admin, **ülke** controller'ından tenant override satırı **authorlayamaz**
      (`ScopeType=tenant` + `TenantId` çözümlenemez → **400**); iki katman iki kapıdan yazılır.
- [ ] **AC-SEC-11** Controller ①'in `{id}` parametreleri **`{id:guid}`** kısıtlıdır; `GET
      /working-calendars/overrides` isteği Controller ①'e **düşmez** (403 değil, doğru controller'a gider).

**AC-CORE — aggregate ve yaşam döngüsü**

- [ ] **AC-CORE-1** Geçerli payload ile create **201**; yanıt `Id` taşır.
- [ ] **AC-CORE-2** Aynı scope+ülke+yıl için aynı `CalendarCode` ikinci create **409**; ikinci doküman **yok**.
      Benzersizlik **YALNIZ CANLI satırlar** (`draft`/`active`) kapsamındadır.
- [ ] **AC-CORE-2a** *(2026-08-27 bug fix)* **`archived` satır kodunu SERBEST BIRAKIR.** Arşivli `TR-2026`
      varken yeni `TR-2026` create **201** döner. Gerekçe: kod yıl/ülke şeklindedir (`TR-2026`) ve delete
      endpoint'i **yoktur** — arşivliyi sayan bir benzersizlik kodu kalıcı olarak yakardı.
- [ ] **AC-CORE-2b** **Guard ve index aynı kümeyi kullanır.** Hem `ExistsByCodeAsync` hem unique index'in
      `partialFilterExpression`'ı `WorkingCalendarStatus.CodeHolding` (= `[draft, active]`) listesinden okur.
      Guard index'ten gevşek olursa dostane 409 yerine **E11000 → 500** çıkar; bu senaryo test edilir.
      Index `$ne` **kullanamaz** (partial filter kısıtı) → küme **pozitif** (`$in`) ifade edilir; MongoDB 7.0'da
      `$in` desteği canlı doğrulandı.
- [ ] **AC-CORE-2c** **Tek-aktif AYRI bir invariant'tır ve değişmedi.** `ExistsActiveAsync` scope+ülke+yıl için
      en fazla bir `active` satır garantisini sürdürür; arşivde kod serbest bırakmak bunu zayıflatmaz.
      Farklı kodlu ikinci `active` → benzersizlik geçer, **tek-aktif 409** verir.
- [ ] **AC-CORE-3** `DELETE` route'u **mevcut değildir** (405/404); `BulkDelete` komutu kod tabanında **hiç yok**.
- [ ] **AC-CORE-4** `archive` sonrası satır `includeArchived=true` ile görünür, `false` ile görünmez;
      **unarchive endpoint'i yoktur**.
- [ ] **AC-CORE-5** Eski `Version` ile update **409**; doküman **değişmez**.
- [ ] **AC-CORE-6** `activate` `WeekendDays` boşken (ülke katmanı) **400** `weekend_days_required`.
- [ ] **AC-CORE-7** Aynı scope+ülke+yıl için ikinci `activate` **409** `active_calendar_already_exists`.
- [ ] **AC-CORE-8** `active` takvimde `CountryCode`/`CalendarYear`/`ScopeType`/`OrganizationUnitId` değişimi
      **409** `calendar_identity_frozen`; `WeekendDays`/`Days`/`CalendarName` değişimi **200** (§8.6 asimetrisi).

**AC-REF — MOD-0048 / MOD-0288 tüketimi**

- [ ] **AC-REF-1** `CountryCode` doğrulaması **in-process** `IPlatformLookupProvider` ile yapılır; Gateway
      üzerinden **HTTP self-call yoktur**.
- [ ] **AC-REF-2** Lookup'ta olmayan ülke kodu → **400** `country_unknown`; **hardcoded ülke listesi kod
      tabanında yoktur** (grep ile doğrulanır).
- [ ] **AC-REF-3** `ScopeType=organization-unit` ile var olmayan **veya başka tenant'a ait**
      `OrganizationUnitId` → **400** `organization_unit_not_found`.
- [ ] **AC-REF-4** `Features/Lookups/**`, `Domain/Entities/Organization/**`, `Diten.Platform.Common/**`
      yüzeylerinde **hiçbir dosya değişmez** — git diff ∅.

**AC-PROV — provider seam (asıl ürün)**

- [ ] **AC-PROV-1** Beş metot da (`IsWorkingDay` · `GetHoliday` · `NextWorkingDay` · `AddWorkingDays` ·
      `WorkingDaysBetween`) `Resolution` + `ReasonCodes` + `ResolvedCalendarId` taşıyan bir record döner;
      **hiçbiri düz `bool`/`int` dönmez**.
- [ ] **AC-PROV-2** Aktif takvim yokken `Resolution = calendar_missing` ve değer **`null`**; **hafta sonu
      varsayımı yapılmaz**, exception fırlatılmaz.
- [ ] **AC-PROV-3** §4.5 çözümleme sırası V24–V28'in **hepsinde** beklenen sonucu verir.
- [ ] **AC-PROV-4** `WeekendDays = null` olan override, ülke hafta sonunu **devralır** ve
      `weekend_inherited_from_country` reason'ını taşır.
- [ ] **AC-PROV-5** `IsHalfDay = true` gün **çalışma günü** sayılır ve `half_day_treated_as_working`
      reason'ı döner (D5 — sessizce değil, **görünür** biçimde).
- [ ] **AC-PROV-6** Çok yıllı aralıkta bir yılın takvimi yoksa `year_missing` + **`null`**; **kısmi sayı
      döndürülmez**.
- [ ] **AC-PROV-7** `AddWorkingDays(start, 0)` sözleşmede yazılı davranışı verir (V31); `a > b` → **400**.
- [ ] **AC-PROV-8** Provider **hiçbir yazma** yapmaz — repository write metotları çağrılmaz (mock ile doğrulanır).

**AC-BOUNDARY — motor yok / sınır disiplini**

- [ ] **AC-BOUNDARY-1** `Recurrence`'tan **hiçbir gün otomatik üretilmez**; gelecek yıl takvimi
      kendiliğinden **oluşmaz** (D6).
- [ ] **AC-BOUNDARY-2** Kod tabanında vardiya/mesai/kapasite/rezervasyon/izin sembolü **yoktur**
      (`ShiftStart`, `WorkingHours`, `Capacity`, `LeaveRequest` gibi).
- [ ] **AC-BOUNDARY-3** Aggregate'te **kişi alanı yoktur** (`EmployeeId` / `PersonId` / `UserId`).
- [ ] **AC-BOUNDARY-4** Provider'da cache **yoktur** (§8.8) — her çağrı repository'ye gider.
- [ ] **AC-BOUNDARY-5** Auto-fetch / dış HTTP çağrısı kod tabanında **yoktur**; `Source = provider-fetch`
      yazma denemesi **400** verir.

**AC-UI — YÜZEY ① Platform Admin Compact konsol**

- [ ] **AC-UI-1** `Views/Platform/WorkingCalendars/*.cshtml` **tümünde** `Layout = "_LayoutPlatformAdmin"`
      **açıkça** yazılı.
- [ ] **AC-UI-2** Klasörde **tam olarak** §11.2'deki 9 dosya var; `_CreateEditOffcanvas.cshtml` ve
      `_DetailsQuickView.cshtml` **yok**.
- [ ] **AC-UI-3** `_DataTable.cshtml` `data-dt-standard="v2"` + skeleton loader taşır; sayfada **tek** DataTable.
- [ ] **AC-UI-4** Tüm dropdown değerleri `contract`'tan (vokabüler) ve `/api/lookups/countries`'ten (ülke)
      gelir; **hardcoded liste yok**.
- [ ] **AC-UI-5** `ScopeType` değiştiğinde `OrganizationUnitId` alanı koşullu görünür/gizlenir ve
      `country` seçiliyken **gönderilmez**.
- [ ] **AC-UI-6** Details sayfasındaki **çözümleme önizlemesi** bir tarih için `Resolution` + `ReasonCodes`
      gösterir; `calendar_missing` durumunu **"bilinmiyor" olarak** ayırt eder (yanlışlıkla "çalışma günü" demez).
- [ ] **AC-UI-7** Browser JS **5057**'yi çağırmaz; yalnız same-origin proxy kullanır.
- [ ] **AC-UI-8** `.resx` **en + tr** paritesi tam (**platform shell** standardı — 7 dil **değil**);
      `window.L10n` anahtarları `undefined` dönmez.

**AC-UI — YÜZEY ② Tenant Override Compact konsol (2026-08-27 revizyonu)**

- [ ] **AC-UI-9** `Views/WorkingCalendarOverrides/*.cshtml` **tümünde** `Layout = "_LayoutTenantShell"`
      **açıkça** yazılı; klasörde **tam olarak** §11.4'teki 9 dosya var (`_CreateEditOffcanvas.cshtml` /
      `_DetailsQuickView.cshtml` **yok**).
- [ ] **AC-UI-10** `ScopeType` seçicisinde **`country` seçeneği HİÇ RENDER EDİLMEZ**; contract yanıtı
      tenant yüzeyi için yalnız `tenant` + `organization-unit` döner.
- [ ] **AC-UI-11** **Devralma görünür (D3):** `WeekendDays` boş bırakıldığında form ve DataTable
      *"Ülke takviminden devralınıyor: …"* şeklinde **çözülmüş** değeri gösterir; boş dizi `[]` ("hafta sonu
      yok") ile `null` (devral) **görsel olarak ayırt edilir**.
- [ ] **AC-UI-12** **Birleşim görünür (D5):** çözümleme önizlemesi seçilen tarih için ülke+override
      **birleşimini** `Resolution` + `ReasonCodes` ile gösterir ve hangi katmanın kazandığını
      (`tenant_override_applied` / `weekend_inherited_from_country` / `public_holiday`) **adıyla** belirtir.
- [ ] **AC-UI-13** Gün repeater'ında `DayType` seçenekleri **yalnız** `company-holiday` · `company-closure` ·
      `working-day-override`; `public-holiday` seçeneği **listelenmez**.
- [ ] **AC-UI-14** Ülke seçici **`/api/lookups/reference/countries`**'i çağırır
      (**`/api/lookups/countries` DEĞİL** — o PlatformActor'dür ve tenant'ı 403'ler).
- [ ] **AC-UI-15** `.resx` **7 dil** (`ar,en,es,fr,ru,tr,zh`) paritesi tam — **tenant shell** standardı
      (MOD-0028/0029 emsali); `window.L10n` anahtarları `undefined` dönmez.
- [ ] **AC-UI-16** Browser JS **5057**'yi çağırmaz; yalnız same-origin proxy kullanır. Sayfada **tek**
      DataTable vardır.
- [ ] **AC-UI-17** İki yüzey **ayrı klasörlerdedir**; `verify_datatable_page.py` **her biri için ayrı ayrı**
      koşar ve **ikisi de** Compact referansını geçer (hibrit-tek-sayfa çakışması **yok**).

---

## 17. Test Expectations

**17.1 Backend unit/integration — hedef ≥ 45 test**

| Küme | Kapsam |
|---|---|
| 1. Validation | V1–V19'un her biri için pozitif + negatif |
| 2. Scope tutarlılığı | §12.2 tablosunun **9 hücresi** (3 scope × doğru/yanlış tenant/aktör) |
| 3. State machine | §12.3'ün her geçişi + her **yasak** geçiş + donan/değişebilen alan asimetrisi |
| 4. Concurrency | Doğru `Version` → 200; eski → 409, doküman değişmez |
| 5. Tenant izolasyonu | Cross-tenant get/update/archive → 404; global satırın **her** tenant'a görünmesi |
| 6. **Hibrit filtre** | `HybridRepository.ExecutionFilter`'ın `Or(null, current)` davranışı — **ilk üretim kullanımı** olduğu için ayrı küme (§4.1 uyarısı) |
| 7. Gün kuralları | V20–V23 + limit + arşivlenmiş günün çözümlemeye girmemesi |
| 8. **Provider çözümleme** | V24–V33'ün **tamamı** + her `Resolution` değeri + her `ReasonCode` |
| 9. Provider read-only | Write metotlarının çağrılmadığının mock ile doğrulanması |
| 10. Reference tüketimi | Geçerli/geçersiz ülke; org-unit var/yok/başka tenant |
| 11. Soft delete | Archive sonrası list davranışı; hard-delete yolunun **yokluğu** |
| 12. Sınır | `provider-fetch` reddi; `Recurrence`'ın gün üretmemesi |
| **13. Override yüzeyi** | **V34–V39** + AC-SEC-6…11: aktif ülke satırının listede ve by-id Details'ta **read-only** görünmesi; draft/archived country by-id 404; country id'ye write 404 · `country` scope reddi · rezerve `DayType` reddi · `WeekendDays` `null` vs `[]` ayrımı · aktif ülke takvimi yokken override kaydının **kabul edilmesi** (V38) · tenant-scope'ta ikinci `active` 409 |
| **14. İki controller / routing** | `{id:guid}` kısıtı olmadan `overrides` yolunun Controller ①'e düşmesi (regression testi) · aynı komutun iki controller'dan çağrıldığında **aynı** scope guard'ını çalıştırması |

**17.2 Frontend / smoke**

- Authenticated smoke — **İKİ ayrı oturum** (2026-08-27 revizyonu):
  - **① platform_admin (≥ 20 adım):** create (country) → gün ekle → activate → resolve (çalışma günü) →
    resolve (tatil) → resolve (hafta sonu) → ikinci activate **409** → archive → arşiv görünürlüğü →
    `calendar_missing` yolu.
  - **② tenant_user (≥ 20 adım):** override list **aktif ülke satırlarını read-only** gösterir, tenant satırı yoksa yalnız onları (AC-SEC-6) → override create
    (`ScopeType=tenant`) → şirket kapanışı ekle → `working-day-override` (telafi) ekle → `WeekendDays` boş
    bırak → **devralma** görünür (AC-UI-11) → activate → resolve (**ülke+override birleşimi**, AC-UI-12) →
    `ScopeType=country` denemesi **403** → `DayType=public-holiday` denemesi **400** → ülke satırının
    id'siyle `PUT /overrides/{id}` **404** → cross-tenant **404**.
- **PowerShell 5.1 tuzağı:** `@(... | Where-Object ...).Count` sarmalaması **zorunlu**; `Add-Result`
  çağrıları script yazıldıktan sonra **gerçekten çalıştırılarak** doğrulanır (MOD-0162-FU04 dersi).
- **Orkestratör self-report'una güvenilmez:** verifier/test sayıları **kendi koşumdan** okunur.

**17.3 Quality gates**

| Gate | Beklenti |
|---|---|
| Build | `Diten.Platform` + `Diten.Platform.Common` + `Diten.Web` + gateway **PASS** |
| **`verify_module_id.py --candidate`** | **exit 0** (AC-ID-2 — implementasyondan sonra da) |
| **`CAND-CAP-0008` grep** | `services/ frontend/ gateway/ tests/` → **0 hit** (AC-ID-1) |
| `verify_datatable_page.py` | **İKİ ayrı koşu** — ① `Views/Platform/WorkingCalendars/` ② `Views/WorkingCalendarOverrides/`; **ikisi de Compact** referansına karşı. **Bulk-delete kontrolleri her ikisinde de EXPECTED N/A** (§10 sapma 1) — beklenen sayı **koşumdan önce** pack'e yazılır, sonradan rasyonalize edilmez |
| `quality-gate-datatable` | **Her iki yüzey** için PASS |
| RESX parite | ① **en + tr** × `WorkingCalendarsIndex` + `SharedResource.WorkingCalendarsMenu` · ② **7 dil** × `WorkingCalendarOverridesIndex` + `SharedResource.WorkingCalendarOverridesMenu` (§11.4 — dil sayısı **shell'e** bağlı) |
| Boundary diff | `Features/Lookups/**`, `Domain/Entities/Organization/**`, `Diten.Platform.Common/**`, `ReferenceDataEntities*.cs` → **git diff ∅** |
| Gateway | `/api/platform/working-calendars` **200** (route eklendikten sonra); eklenmeden **404** beklenir |

**17.4 Endpoint'ler fleet restart'a kadar 404'tür**; `.resx` değişiklikleri **tam restart** ister.

---

## 18. Ready-for-dev Checklist

- [x] DCP-002 **candidate** kapısı **PASS** (exit 0, 2026-08-26) — komut ve çıktı §başlıkta
- [x] Registry + reconciliation ledger satırları **doğrulandı** (registry:213, EA rezervasyonu 2026-08-26)
- [x] **Runtime nötr adı sabitlendi** (`working-calendar`) ve `CAND-CAP-0008`'in runtime yasağı **AC-ID-1/2**
      ile makine-doğrulanabilir hâle getirildi
- [x] Zorunlu bağlam okundu: `AGENTS.md` · PSS `domain-config.md` · `module-pack-standard.md` ·
      `platform-lookups-reference-data.md` · Golden Compact (DEV-0001) + canlı `Views/Platform/ModuleCatalog/`
- [x] Golden Reference (DEV-0001 **Compact**) referans alındı; alan sayımı **gösterildi** (§11.1 — 11 > 8)
- [x] Frontend dosya seti **tek tek** enumere edildi (§11.2 — tek klasör, 9 dosya + 3 JS + **2 dil** RESX)
- [x] Frontmatter zorunlu alanların tümü dolu (`service`, `shell`, `golden_reference`, `entity_base`,
      `form_field_count`)
- [x] `entity_base` **gerekçelendirildi** (§4.1 — `HybridEntity`, standart enum dışı **beyan edilmiş** sapma)
- [x] Layout & Shell Contract'ta Razor `Layout` **açıkça** yazıldı → **AC-UI-1**
- [x] Backend File Convention Golden Reference naming'iyle birebir; **iki sapma açıkça beyan edildi** (§10)
- [x] **Platform Lookup & Reference Data kararı yazıldı** (§4.7 — mevcut `/api/lookups/countries`,
      **yeni Platform lookup key gerekmez**, hardcoded fallback yasak)
- [x] Validation Rules her alan için yazıldı (§12 — 33 kural + scope tablosu + state machine + çözümleme)
- [x] Failure Path ≥ 4 senaryo (§13 — **18 senaryo**)
- [x] Authorization Convention: **5 anahtar** (3 ülke + **2 override**) + **2 policy / 2 controller** + actor
      + **`activate` SoD asimetrisi** + `.override.read`'in ayrı olma gerekçesi + fallback **kullanılmama** gerekçesi
- [x] Gateway kararı **açık ve doğrulandı**: **GEREKLİ** (catch-all yok); `/api/lookups` **zaten var**
- [x] Acceptance Criteria konsolide + test edilebilir; her madde §17'de bir teste eşlendi
- [x] Test Expectations build + candidate-gate + grep + verifier + RESX + smoke + **boundary diff ∅** kapsıyor
- [x] Protected Paths eksiksiz (§6) — tüketilen 4 yüzey, diğer servisler, ocelot, RBAC, registry, Mongo,
      ERP reference yolları
- [x] **Sahte-FK yasağı** uygulandı: `OrganizationUnitId` **gerçek ve doğrulanan** FK (aynı serviste);
      kişi/employee alanı **hiç açılmadı** (D4)
- [x] **Consumers bölümü** yazıldı (§21) ve **MOD-0155 F-CALENDAR** bağımlılığı adıyla kaydedildi
- [x] ✅ **D2 ONAYLANDI (2026-08-27)** — `HybridEntity` tek aggregate (infra kontrol-kulesi doğrulandı: EntityVariations.cs:13 / Repositories.cs:97)
- [x] ✅ **D5 ONAYLANDI (2026-08-27)** — provider `Resolution`+ReasonCodes; takvim yoksa `null` (fail-closed, tüm tüketicileri bağlar)
- [x] ✅ **F-TENANT-UI KARARI: v1'e DAHİL (2026-08-27)** — tenant override authoring UI **ertelenmez**
- [x] ✅ **F-TENANT-UI REVİZYONU UYGULANDI (2026-08-27)** — §2.1 out-of-scope'tan çıkarıldı · §9 iki shell
      kontratı (9.1/9.2/9.3) · §10 iki controller · §11.3 türetme (**9 alan ⇒ Compact**) + §11.4 dosya seti
      (9 view + 3 JS + **7 dil** RESX) · §12.6 override kısıtları (V34–V39) · §14 iki policy + 5 anahtar ·
      §15 12 endpoint + `{id:guid}` tuzağı · AC-SEC-6…11 + AC-UI-9…17 · §17 küme 13/14 + iki verifier koşusu
      + iki smoke oturumu
- [x] **İki yüzey = iki klasör = iki verifier koşusu** — MOD-0162-FU03 hibrit-tek-sayfa problemi **yapısal
      olarak yok** (§11 giriş notu)
- [x] ✅ **D-OVR-UI KAPANDI = COMPACT (kontrol-kulesi + kullanıcı, 2026-08-27)** — 9 gerçek alan = Compact;
      `Description` düşürüp Slim'e inmek field-count-gaming anti-pattern'i olurdu. İki yüzey de Compact = tutarlı.
- [x] `status: ready-for-dev` + `runtime_code_allowed: true` (2026-08-27) — D2/D5/F-TENANT-UI/D-OVR-UI hepsi kapandı; kontrol-kulesi infra+revizyon doğruladı

---

## 19. Implementation Notes

### 19.0 Teslim durumu (2026-08-27) — BACKEND TAMAM, FRONTEND AÇIK

| Katman | Durum |
|---|---|
| `WorkingCalendar` entity + gömülü gün + 7 in-domain vokabüler | ✅ `Domain/Entities/WorkingCalendar/WorkingCalendar.cs` |
| `IWorkingCalendarRepository` + `HybridRepository` implementasyonu | ✅ **HybridEntity'nin ilk üretim tüketicisi** |
| `IWorkingCalendarProvider` (5 metot) + resolve engine + contracts | ✅ D5 sözleşmesi (`Resolution` + `ReasonCodes`, çözülemezse `null`) |
| CQRS: 6 command · 4 query · 9 handler · paylaşılan write-guard | ✅ |
| 2 controller (`PlatformActor` + tenant), 12 endpoint, `{id:guid}` kısıtlı | ✅ |
| DI + Mongo index (partial filter `Eq(IsDeleted,false)`, `$ne` YOK) | ✅ |
| **ModuleManifestProvider** (8 sayfa, 12 aksiyon, 2 shell) | ✅ **pack'te YOKTU — §19.4'e bakın** |
| Testler | ✅ **42/42 PASS** (resolve engine 16 + validation 26) |
| **Frontend — YÜZEY ① Platform Admin** | ✅ `Views/Platform/WorkingCalendars/` 9 view + 4 JS + proxy controller + RESX **{en,tr}** |
| **Frontend — YÜZEY ② Tenant Override** | ✅ `Views/WorkingCalendar/Overrides/` 9 view + 4 JS + proxy controller + RESX **7 dil** |
| **`verify_datatable_page.py` × 2** | ✅ **88 PASS / 7 FAIL, her iki yüzeyde birebir aynı** — 7'si de §19.5'te koşumdan ÖNCE ilan edilmişti |
| Nav L10n (`Nav.Module.WORKINGCALENDAR` + `Nav.Page.…`) | ✅ 7 dil `SharedResource.*.resx` |
| Platform sidebar + Ctrl+K (`PlatformNavigationCatalog`) | ✅ tek kayıt — tenant yüzeyi kasıtlı olarak **eklenmedi** |

### 19.6 F-DAY-EDITOR (2026-08-27) — gün authoring UI

Gömülü gün alt-editörü her iki yüzeyde de canlı. **Hiçbir yeni backend yazılmadı**; mevcut
`UpsertWorkingCalendarDayCommand` / `ArchiveWorkingCalendarDayCommand` uçları ve mevcut proxy metotları kullanıldı.

| Karar | Gerekçe |
|---|---|
| Diyalog `Details` sayfasında, **`_CreateEditOffcanvas.cshtml` DEĞİL** | O ad Compact'ta yasak ve Index'e aittir. Burada düzenlenen şey aggregate değil, **zaten açık bir takvimin gömülü çocuğu** — yeri detay sayfasıdır. Verifier'ın "Compact Index create/edit offcanvas içermez" kontrolü etkilenmez |
| `DayType` seçenekleri **contract**'tan | Tenant diliminde `public/religious/moveable` **hiç yok** → tenant diyaloğu ülke-katmanı tipini yapısal olarak sunamaz. UI atlansa bile backend **400 `day_type_reserved_for_country_layer`** ile reddeder (AC-SEC-8 hâlâ tek doğrulama noktası) |
| `working-day-override` seçilince **yarım gün kutusu devre dışı** | Backend zaten `half_day_on_override` ile reddediyor; kullanıcıya önce sunup sonra reddetmek yerine baştan kapatıldı |
| **Arşivlenmiş gün listede kalır**, aksiyonsuz ve soluk | Hard delete yok. Gizlemek, takvimin geçmişte ne dediğini sessizce yeniden yazmak olurdu |
| Gün yazma **`archived` takvimde kapalı**, `active` takvimde **açık** | §8.6 asimetrisi: kimlik donar, **içerik yaşar** — resmî tatiller yıl içinde ilan edilir/kaydırılır |
| Vokabüler etiketleri **iç içe sözlükte** (`DayTypeLabels` / `RecurrenceLabels` / `DayStatusLabels`) | Slug'lar (`working-day-override`) C# tanımlayıcısı olamaz. Bu olmadan kullanıcı ham slug görürdü — 9 RESX dosyasına da gerçek etiketler eklendi |
| 400/409 **birebir** gösterilir | Diyalogdaki hata kutusu sunucunun cümlesini aynen basar; uydurma/parafraz yok |

**RESX:** platform **2 dil × 73 anahtar**, tenant **7 dil × 73 anahtar** — anahtar kümesi farkı **0**.

### 19.5 `--area` kararı ve beklenen verifier FAIL'leri (koşumdan ÖNCE ilan edildi)

**`--area` (kullanıcı kararı 2026-08-27):** tenant yüzeyi verifier'ın `Views/{Area}/{Module}/` beklentisine uyması
için **area-şeklinde** bir yola alındı: `Views/WorkingCalendar/Overrides/` →
`--area WorkingCalendar --module Overrides`. **Route değişmedi**: `/WorkingCalendar/Overrides`, tenant-shell.
Bu ayrım önemli, çünkü §2c gereği permission scope **route**'tan türer — klasörden değil. Route `/Platform/…`
altına girmediği sürece `platform.working-calendar.override.*` **Tenant** scope'unda kalır.

**Beklenen 7 FAIL (her iki yüzeyde aynı, gate'i çalıştırmadan önce yazıldı):**

| # | FAIL | Neden beklenen |
|---|---|---|
| 1 | `personalizationClient sends tenant header only for tenant users` | **Paylaşılan dosya**, modüle ait değil: `DevEnablement/GoldenReferenceCompact` altın referansının **kendisi de** bu kontrolde kırmızı (93 PASS / 1 FAIL). Repo geneli bir borç |
| 2–7 | `dt-checkboxes-select-all` · `bulkOptions/bulkBarSelector` · `getSelectedIds/onBulkAction` · `.../bulk` endpoint · bulk-delete tetikleyicisi · `clearSelection` | Modül **CRUD-minus-delete**: silme yok, `/bulk` endpoint'i yok. Hiçbir şeye bağlı olmayan bir bulk bar render etmek, olmamasından **daha kötü** olurdu (§8.3 + §10 sapma 1) |

Bulk ailesini yeşile çevirmenin tek dürüst yolu **bulk-archive endpoint'i** açmaktır (`POST …/bulk`); bu bir
**backend** eklemesidir ve bu artımın (frontend) kapsamı dışında bırakıldı → §20/**F-BULK-ARCHIVE**.

**Build:** `Diten.Platform.Domain` · `.Application` · `.Infrastructure` · `.API` → **0 hata**.
**Regresyon:** tüm Application test paketi 2293 test; 28 kırmızı **tamamen** DocumentManagement /
BusinessReferenceData / Mod0029 alanlarında ve bu çalışma ağacında **zaten commit'lenmemiş** olan başka bir
işe ait — WorkingCalendar dosyalarıyla kesişmiyor.

### 19.4 Pack'in atladığı zorunluluk: self-registration (uygulandı)

`module-self-registration-standard.md` **her tenant-atanabilir modül için zorunlu** bir `ModuleManifestProvider`
ister ve bu pack §1–§21 boyunca bundan **hiç söz etmiyordu**. Eksik implementasyonda kapatıldı, çünkü kural
aynı zamanda §2c ile bu modülün **güvenlik sınırını** belirliyor:

> Permission scope her sayfanın **RoutePath**'inden türer: `/Platform/…` → `PlatformAdmin`, diğer her şey →
> `Tenant`.

Bu, §14'teki iki-anahtar ayrımını **kendiliğinden** doğru yere oturtuyor:
`/Platform/WorkingCalendars` → `platform.working-calendar.*` **PlatformAdmin**;
`/WorkingCalendarOverrides` → `platform.working-calendar.override.*` **Tenant** (yani `platform.` namespace'inde
olmasına rağmen tenant rollerine atanabilir — `TenantSelfServicePermissions` allow-list'ine **gerek yok**).
Route'lardan biri taşınırsa izin **sessizce yanlış scope'a** düşer. Manifest bu yüzden 8 sayfayı ve 12 aksiyonu
gerçek permission sabitleriyle beyan eder.

**Açık kalan (§3 completeness testi):** manifest ↔ frontend route eşleşmesini iki yönlü doğrulayan test,
frontend yazılmadan anlamlı değil → frontend teslimiyle birlikte gelmeli.

### 19.1 Kararlar (D1–D7)

| # | Karar | Gerekçe / reddedilen alternatif |
|---|---|---|
| **D1** | **Tek zaman ekseni: `CalendarYear`.** Policy-tipi `EffectiveFrom`/`EffectiveTo` çifti **AÇILMAZ**. | Takvim zaten yıl bazlıdır; ikinci bir zaman ekseni "2026 takvimi ama 2025-06'dan geçerli" gibi anlamsız durumlar ve iki farklı "hangi takvim geçerli?" cevabı doğurur. Ayrıca iki `DateTimeOffset` alanının birlikte sort/index edilmesi bilinen **parallel-arrays 500** tuzağıdır. `Date`/`ObservedDate` bu yüzden **`DateOnly`**'dir. (MOD-0155-FU01/D1 ile aynı ilke) |
| **D2** | **Tek aggregate, `HybridEntity`.** Ülke katmanı `TenantId=null`, override katmanı `TenantId=<tenant>`. | `HybridRepository<T>`'nin XML doc'u birebir *"Global default + Tenant override"* diyor ve `ExecutionFilter` bunu zaten uyguluyor — altyapı **hazır**. Reddedilen: iki aggregate + iki koleksiyon + elle katman birleştirme (§4.1). **Bedeli** §8.2'deki tenant-sızıntısı riskidir ve AC-SEC-1…5 ile kapatılır. **Kullanıcı teyidine açık** |
| **D3** | **Günler gömülü; `WeekendDays` devralınır.** Override'da `WeekendDays = null` ⇒ ülke katmanından devral. | Gömülü liste = tek `Version` token'ı, tek doküman, ikinci repository yok (MOD-0162-FU04/D2 emsali). Devralma, "şirket sadece tatil ekliyor, hafta sonunu değiştirmiyor" olan **baskın** senaryoyu tek satırla çözer; `null` ile boş dizi arasındaki fark **anlamlıdır** ve V26/V27'de test edilir |
| **D4** | **`OrganizationUnitId` GERÇEK FK'dır ve doğrulanır.** Kişi/employee alanı **hiç açılmaz**. | Sahte-FK yasağı "doğrulanamayan Guid açma" der — burada `OrganizationUnit` **aynı serviste**, `Domain/Entities/Organization/OrganizationUnit.cs`'te canlıdır, dolayısıyla FK **gerçek ve doğrulanabilir**dir (MOD-0155-FU01'deki `ResourceId` string kararının **tersi** yönde, aynı ilkeyle). Kişi alanı ise doğrulanamaz **ve** kavramsal olarak yanlıştır (§2.3/1) |
| **D5** | **Provider `bool` değil, `Resolution` taşıyan record döner; takvim yoksa değer `null`.** `IsHalfDay` = **çalışma günü**. | Düz `bool`, takvim yokluğunu bir cevaba dönüştürür ve tüketici **uydurulmuş gerçekle** plan yapar. MOD-0164 (*"unknown is NOT allowed"*) ve MOD-0165 (*"a default is NEVER invented"*) ilkesi burada da geçerli. Yarım günün çalışma günü sayılması Türkiye arife pratiğiyle uyumludur **ve** her zaman `half_day_treated_as_working` ile **görünür** kılınır. **Kullanıcı teyidine açık** |
| **D6** | **Motor yok.** `Recurrence` bir **beyandır**; hiçbir gün otomatik türetilmez, gelecek yıl kendiliğinden oluşmaz. | Otomatik türetme, hareketli dinî bayramlarda **sessizce yanlış** üretir (hicri takvim, ülkeye göre ilan farkı). Türetme işi, insan onayı olan **FU02 auto-fetch** akışına aittir (§20/F-AUTOFETCH) |
| **D7** | **Vokabüler in-domain, fail-closed.** Ülke ise MOD-0048'den. | Gün adı ve gün tipi **yapısaldır** — provider'ın davranışını değiştirirler, tenant serbestçe genişletemez (`ContactAvailability.Weekday` emsali). Ülke listesi ise gerçek reference data'dır ve MOD-0048'e aittir. Hardcoded fallback her iki tarafta da **yasaktır** |

### 19.2 Bu FU'yu doğrudan vuran tuzaklar

1. **Sınıf seviyesindeki `PlatformActor` policy'si gevşetilemez — İKİ controller ZORUNLU.** Kod tabanında
   bunun bedeli zaten ödenmiş ve yorumla kayda geçirilmiş: `TenantReferenceLookupsController` şöyle diyor —
   *"The main LookupsController is `[Authorize(Policy = "PlatformActor")]` and 403s tenant_user actors
   (İş3 tenant-vs-platform boundary); **an action-level `[Authorize]` cannot relax a class-level policy**, so
   these two universal, non-sensitive keys live here."* Tenant override endpoint'lerini
   `WorkingCalendarsController`'a **aksiyon seviyesinde `[Authorize]` ile** eklemeye çalışmak **sessizce
   403** üretir ve permission hatası gibi görünür. Çözüm §10/§14'teki ikinci controller'dır.
2. **Route çakışması: `{id}` `overrides`'ı yutar.** Controller ①'de `working-calendars/{id}` kısıtsız
   bırakılırsa `working-calendars/overrides` isteği oraya düşer ve tenant **403** alır. **Tüm** `{id}`
   parametreleri **`{id:guid}`** yazılır (§15 uyarısı). Bu, teşhisi zor bir hata sınıfıdır: yetkilendirme
   arızası gibi görünür, aslında routing arızasıdır.
3. **RESX dil sayısı domain'e değil SHELL'e bağlıdır.** Yüzey ① **2 dil** (`en,tr`), yüzey ② **7 dil**
   (`ar,en,es,fr,ru,tr,zh`). Kanıt aynı PSS domain'inden: `Views/Platform/Tenants/TenantsIndex.{en,tr}.resx`
   (2) vs `Views/DocumentManagement/ControlledDocuments/ControlledDocumentsIndex.{ar,…,zh}.resx` (7). Tek
   kurala indirgemek RESX parite gate'ini **kesin olarak** kırar.
4. **Tenant ülke lookup'ı farklı endpoint'tir.** Yüzey ② ülke seçicisi
   `/api/lookups/reference/countries` (`[Authorize]`) kullanır; `/api/lookups/countries` **PlatformActor**'dür
   ve tenant'ı 403'ler (§11.4).
5. **Yanlış `BaseEntity`.** İki tane var (§4.1 uyarısı). `Diten.Platform.Domain.Common.BaseEntity` legacy'dir
   (`string Id`, `DateTime`); doğru olan `Diten.Platform.Common.Persistence` ailesidir (`Guid Id`,
   `DateTimeOffset`, **`Version`**). Yanlış seçim id tipi ve concurrency token'ı kaybettirir.
6. **`TenantId = null` sızıntısı.** Hibrit modelde **tek** ciddi risk budur (§8.2). `ScopeType` ile `TenantId`
   tutarlılığı **tek bir yerde** (`WorkingCalendarValidation`) uygulanmalı; iki kopya = iki farklı davranış.
7. **Dizi-içi unique index YOK.** `DayCode` ve etkin tarih tekilliği Mongo index'iyle zorlanamaz; handler +
   validator **tek savunma hattıdır** (MOD-0162-FU05 dersi).
8. **Partial index `$ne` yasak.** `Filter.Ne(x, null)` içeren partial index servisi başlangıçta
   **crash-loop**'a sokar; `Filter.Type(...)` / `$lt` kullanılır (Platform 5057 dersi — **bu servis**).
9. **Doküman büyümesi.** Gömülü gün listesi 16MB Mongo limitine tabidir; §12.4/V22 sınırı (400 gün/takvim)
   contract'ta ilan edilir. Liste sorgularında `Days` alanı **projeksiyonla dışarıda bırakılır** (DataTable
   yalnız sayaç rozeti gösterir).
10. **`GuidRepresentationMode` / class-map.** `WorkingCalendar` **ve** gömülü `WorkingCalendarDay`
   `DependencyInjection.cs`'e kaydedilmezse `Guid` alanları binary yazılır ve filtreler **sessizce boş döner**
   (MOD-0151 FU05 dersi).
11. **Provider'ın `TenantContext`'i.** `HybridRepository.ExecutionFilter` `TenantContext.TenantId` okur. Bir
   **arka plan işi** (ileride FU02) tenant bağlamı olmadan çalışırsa filtre yalnız global satırları döndürür —
   bu **doğru** davranıştır ama beklenmediğinde şaşırtır; FU02'de bilinçli ele alınmalı.
12. **`DateOnly` + Mongo.** `DateOnly` için BSON serializer kaydı gerekir; kaydedilmezse tarih alanları
   beklenmedik biçimde saklanır. Alternatif `DateTime` (UTC midnight) değil **`DateOnly`** tercih edilmiştir
   çünkü saat dilimi kayması bir takvimde **doğrudan yanlış gün** demektir.
13. **L10n bridge.** `index.l10n.js` camelCase→PascalCase dönüşümü atlanırsa `window.L10n` anahtarları
   `undefined` döner (toast `"(undefined: corrId)"`).
14. **RBAC ilk açılışta 403.** `platform.working-calendar.*` katalogda yok; **fallback kullanılmadığı** için
    (§14) endpoint'ler F-RBAC tamamlanana kadar 403 verir. Bu **beklenen** durumdur, bug değildir.
15. **`rolePermissions` el ile yazılmaz.** Yanlış GUID subtype'ıyla tek bir kayıt **tüm tenant login'lerini
    kırar**.
16. **Menü görünürlük zinciri.** Modül Platform sidebar'ında çıkmıyorsa permission guard'ı ve `<li>` kontrol
    edilir; Platform tarafında entitlement zinciri tenant modüllerinden **farklıdır**.

---

## 20. Follow-up Items

| # | Follow-up | Neden |
|---|---|---|
| ✅ **D2** | **ONAYLANDI (2026-08-27)** — `HybridEntity` tek aggregate (infra doğrulandı) | §4.1 |
| ✅ **D5** | **ONAYLANDI (2026-08-27)** — provider `Resolution`+ReasonCodes, takvim yoksa `null` (fail-closed) | §4.5 |
| ✅ **F-TENANT-UI** | **KAPANDI — v1'e DAHİL (2026-08-27)** ve pack revizyonu **uygulandı**: §2.1 · §9.1–9.3 · §10 (2 controller) · §11.3–11.4 · §12.6 · §14 · §15 · AC-SEC-6…11 · AC-UI-9…17 · §17 küme 13/14. **Artık bir follow-up değildir** | §9/§11 |
| ✅ **D-OVR-UI** | **KAPANDI = COMPACT (2026-08-27)** — 9 gerçek alan = Compact; alan düşürüp Slim'e inmek gaming olurdu | §11.3 |
| **F-AUTOFETCH** | **FU02 — dış sağlayıcıdan tatil çekme** (aşağıda ayrı tasarım notu) | Kullanıcı talebi; v1 core = **manuel + seam** |
| **F-REG** | Registry satırının EA canonical `MOD-xxxx`'e terfi ettirilmesi | Satır **zaten var** (`candidate / pending-EA`); terfi **EA aksiyonudur**, bu pack yazmaz |
| **F-GW** | `ocelot.json`'a `/api/platform/working-calendars` + `/{everything}` çifti (OPTIONS dâhil) | §15 — catch-all yok; **integration-agent** task'ı |
| **F-RBAC** | `platform.working-calendar.{read,manage,activate}` **+ `.override.{read,manage}`** katalog + grant (**5 anahtar**); override anahtarları **tenant rollerine**, ülke anahtarları **platform rollerine** | §14 — fallback **bilinçli olarak kullanılmadı**; o zamana kadar **her iki yüzey de** 403 |
| **F-AUDIT** | `activate` / `archive` olaylarının MOD-0021 audit trail'ine düşmesi | Ülke takvimi aktive etmek geniş yayılma alanlı bir aksiyon (§14) |
| **F-BULK-ARCHIVE** | `POST /api/platform/working-calendars/bulk` (bulk **archive**, asla delete) + iki yüzeye bulk bar | Verifier'ın 6 bulk kontrolünü dürüstçe yeşile çeviren tek yol; geçmiş yılların takvimlerini toplu arşivlemek gerçek bir ihtiyaç. **Backend** işi olduğu için frontend artımının dışında bırakıldı |
| ✅ **F-DAY-EDITOR** | **KAPANDI (2026-08-27)** — gün alt-editörü her iki yüzeyde canlı: `AddDay` açık, offcanvas Add/Edit diyaloğu, satır-içi düzenle/arşivle, `Upsert`/`ArchiveDay` uçlarına bağlı, 400/409 kullanıcıya **birebir** gösteriliyor. **Yeni backend yok.** Artık follow-up değildir | §19.6 |
| **F-MANIFEST-TEST** | Manifest ↔ frontend route iki-yönlü completeness testi (self-registration §3) | Artık her iki yüzeyin route'ları da var; test bu artımdan sonra anlamlı hâle geldi |
| **F-CACHE** | Provider cache + `activate` anında invalidation | §8.8 — erken cache **yanlış tatil** demektir; invalidation ayrı tasarım |
| **F-SHIFT** | Çalışma saati / vardiya / yarım-gün saat modeli | §2.3/2 — v1 **gün** granülaritesi; `IsHalfDay` bugün yalnız etiket |
| **F-RD** | Tenant'a özel `DayType` etiketi ihtiyacı doğarsa MOD-0048 set'i | §4.4 — bugün yapısal olduğu için in-domain |
| **F-CALENDAR-155** | **MOD-0155 tüketimi** — MicroTarget + Visit/Route planlamasının bu provider'a bağlanması | **§21** — bu capability'nin **birincil varlık nedeni** |
| **F-MULTI-YEAR** | Çok yıllı aralık sorgularının ergonomisi (`year_missing` yerine kısmi sonuç + uyarı seçeneği) | §12.5/V33 bugün **kısmi sonuç vermiyor**; gerçek kullanım verisiyle gözden geçirilir |
| **F-STATUS** | Closeout'ta `execution/registries/module-implementation-status.md` satırı | Kod-izli modül durum takibi — **yalnız kullanıcı onayıyla** |

### F-AUTOFETCH — FU02 tasarım notu (bu FU'da AÇILMAZ)

Kullanıcı talebi netti: dış sağlayıcıya **körlemesine güvenilmez**. Akış **dört durak**lıdır ve her durak
insan onayına açıktır:

```text
1. FETCH     — MOD-0026 (Hangfire) zamanlı iş; ülke+yıl için dış sağlayıcıdan çeker.
               Sağlayıcı yanıtı HAM hâliyle saklanır (provenance: sağlayıcı adı, sürüm, çekilme anı, ham payload hash'i).
2. STAGING   — ayrı bir staging kaydı (aggregate ADI bu pack'te REZERVE EDİLMEZ — FU02 kararı).
               Aktif takvime HİÇBİR ETKİSİ YOKTUR. Mevcut takvimle DIFF üretir: eklenen / kaldırılan / tarihi kayan gün.
3. REVIEW    — insan diff'i inceler, satır satır kabul/ret eder. Reddedilen satır iz bırakır (sessizce kaybolmaz).
4. ACTIVATE  — onaylanan set, YENİ bir takvim sürümü olarak yazılır ve `platform.working-calendar.activate`
               ile aktive edilir. Source = provider-fetch (v1'de bu değer REZERVE, üreticisi yok).
```

FU02'nin taşıması gereken ek kısıtlar: dış HTTP egress politikası (hangi host'lara çıkılabilir), sağlayıcı
kimlik/anahtar yönetimi (MOD-0012 secrets), rate-limit ve **sağlayıcı çöktüğünde aktif takvime hiçbir şey
olmaması** (staging'in tanım gereği izole olması). **Bu pack bunların hiçbirini yetkilendirmez.**

---

## 21. Consumers and Downstream Contracts

> Bu capability'nin **ürünü UI değil, `IWorkingCalendarProvider`'dır**. Bölüm, tüketicilerin ne alacağını ve
> neyi **asla** yapmayacaklarını bağlar.

| Tüketici | Ne için | Nasıl | Durum |
|---|---|---|---|
| **MOD-0155-FU05 — MicroTarget** | Hedefleme cadence'i ve ziyaret yükünü **çalışma günü** üzerinden hesaplamak; "ayda 4 ziyaret" gibi bir frequency'yi gerçek takvime oturtmak | in-process `IWorkingCalendarProvider` | **F-CALENDAR** — MOD-0155-FU05 pack'i yazılırken **bu bağımlılık adıyla kaydedilecektir** |
| **MOD-0155-FU01 — Visit Planning** | `PlannedDate`'in çalışma günü olup olmadığını **uyarı** olarak göstermek | in-process | FU01 bugün takvim bilmiyor; additive |
| **MOD-0155-FU03 — Route Planning** | Gün doldurma / Daywork hesabının takvimle hizalanması | in-process | FU03 yazılırken |
| **MOD-0280 — Time Entry** | Beklenen çalışma günü sayısı | in-process / HTTP | Gelecek |
| **PPM / Finance** | Proje takvimi, vade/ödeme günü hesabı | HTTP `resolve` | Gelecek |

**Tüketici sözleşmesi — dört madde, pazarlığa kapalı:**

1. **Kendi takvimini tutma.** Hiçbir tüketici hafta sonu listesi, tatil listesi veya "iş günü mü" bayrağı
   **kopyalamaz**. Kopyalanan takvim **bayatlar** ve ikinci bir gerçek doğurur (§2.4).
2. **Aritmetiği yeniden yazma.** `nextWorkingDay` / `addWorkingDays` **burada** yaşar. MOD-0165-FU03'ün
   kuralı burada da geçerlidir: *"no consumer re-implements the engine."*
3. **`calendar_missing` / `year_missing` / `country_unknown` durumunu görünür kıl.** Bunları sessizce
   "çalışma günü" veya "tatil" olarak yorumlamak **yasaktır** (D5). Tüketici bunu kullanıcıya
   *"takvim tanımlı değil"* olarak göstermek zorundadır.
4. **Aynı süreçteyse HTTP kullanma.** `Diten.Platform` içindeki tüketici in-process provider'ı çağırır;
   Gateway üzerinden kendi servisine HTTP **atmaz** (§15).

**Provenance saklama:** bir tüketici kararını denetlenebilir kılmak isterse, takvim **verisini** değil
`ResolvedCalendarId` + `Resolution` + `ReasonCodes` + sorgu anını saklar — MOD-0164/MOD-0165 provenance
deseninin aynısı.

---

## Handoff

Module pack **`draft`** — **2026-08-27 revizyonu uygulandı**. Üç kararın **üçü de kapandı**:

| Karar | Durum | Nerede |
|---|---|---|
| **D2** — `HybridEntity` tek aggregate | ✅ **ONAYLANDI** (altyapı doğrulandı: `EntityVariations.cs:13` / `Repositories.cs:97`) | §4.1 |
| **D5** — provider `Resolution` + `ReasonCodes`, takvim yoksa `null` | ✅ **ONAYLANDI** (fail-closed; tüm tüketicileri bağlar) | §4.5 |
| **F-TENANT-UI** — tenant override authoring UI | ✅ **v1'e DAHİL** ve pack'e **işlendi** | §9 · §11.3–11.4 · §12.6 · §14 · §15 · AC |

**Geriye tek bir düşük-etkili soru kaldı — `D-OVR-UI` (§11.3):** tenant override formu **9 kullanıcı-form
alanı** türetti, yani eşiğin **bir üstünde** ⇒ `compact`. Talimat "muhtemelen Slim" diyordu; eşiği tutturmak
için alan düşürmedim. Slim istenirse `Description`'ı formdan çıkarmak yeterlidir (9 → 8) — **tek satırlık**
değişiklik. **Önerim Compact kalması**: iki yüzey aynı aggregate'i, aynı validator'ları ve aynı vokabüleri
paylaşıyor; birini Compact diğerini Slim yapmak aynı varlık için iki farklı form idiomu demektir.

Geliştirme için status `approved` veya `ready-for-dev` olmalı **ve** `runtime_code_allowed: true`
yapılmalıdır; sonra `@orchestrator CAND-CAP-0008-working-calendar-public-holidays` çağrılır.

Hazırlık sırasında **Golden Reference Compact (DEV-0001)** şablon olarak alındı — naming'de sapma yok; iki
yapısal sapma (`Delete`/`BulkDelete` yokluğu, `Provider/` klasörü) §10'da, `entity_base` sapması §4.1'de
**açıkça beyan edildi**. **İki yüzey ayrı klasörlerdedir** → iki ayrı verifier koşusu, hibrit-tek-sayfa
çakışması **yapısal olarak yok**.

> **Son hatırlatma:** `CAND-CAP-0008` bir **governance kimliğidir**. Implementasyon sırasında bu dize
> `services/`, `frontend/`, `gateway/` veya `tests/` altına **tek bir yorum satırında bile** girerse DCP-002
> candidate kapısı kalıcı olarak kırmızıya döner (AC-ID-1/AC-ID-2). Runtime'da yalnız **`working-calendar`**
> ve türevleri kullanılır.
