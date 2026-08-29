# MOD-0150-FU — Contact Availability and Visit Preference — Implementation

- **Tarih:** 2026-08-01
- **Modül:** MOD-0150 — Contact & Relationship Management (`Diten.CrmService`, port 5061)
- **Task tipi:** Runtime implementation (backend + frontend + test + gateway-only smoke)
- **Scope:** `FU-contact-availability-visit-preference` (pack §20)
- **Owner:** orchestrator
- **Verdict:** **PARTIAL** (gerekçe §15)

---

## 1. Preflight

- Ön koşul **PASS**: `docs/audits/mod-0150-contact-availability-visit-preference-pack-authorization-2026-08-01.md`
  (pack §20 authorized scope + D8–D15 kararları).
- Bu task **route planning / visit planning / frequency engine / campaign engine / GPS-checkin değildir**; yalnız
  `AccountContactLink` bazlı availability + visit preference + date-specific exception **master data**'sını uygular.
- Okunan kaynaklar: MOD-0150 pack (§9, §10, §11, §13, §14, §20), pack authorization raporu, MOD-0151 FU09A
  authorization raporu, `Contact` / `AccountContactLink` aggregate + repository + handler'ları, `ContactsController`
  (frontend proxy deseni), Contact Details (360) view'ı, `TerritoryContractController` (contract deseni),
  `DependencyInjection` (class map + index + GUID-as-string konvansiyonu).
- Mimari kural korundu: *availability "bu kişi **burada** ne zaman ziyaret edilebilir" sorusunu cevaplar; "bugün kim,
  hangi sırayla ziyaret edilmeli" sorusunu asla cevaplamaz.*

## 2. Implementation Summary

| Katman | Çıktı |
|---|---|
| Domain | `ContactAvailability` (link-scoped) · `VisitPreference` (VO) · `ContactAvailabilityException` · `AvailabilityLifecycle` · `AvailabilityWeekday` (ISO gün + "HH:mm" parse/overlap yardımcıları) · 2 repository interface (**delete üyesi yok**) |
| Persistence | 2 Mongo repository · class map'ler (**tüm Guid FK'ler string-Guid**) · 7 index (3 + 3 lookup + 2 unique partial natural key) · DI kayıtları |
| Application | 8 command · 4 query · 10 handler · validation çekirdeği · mapper · lookup motoru · `IActorContext` provenance seam'i |
| API | `ContactAvailabilityController` (4 read + 8 write rotası, **delete yok**) · `ContactContractController` (MOD-0150 contract yüzeyi) · request modelleri (**payload'da TenantId/ContactId/AccountId yok**) |
| Frontend | Contact 360 read-only availability kartı · `/CRM/Contacts/Availability/{contactId}` yönetim sayfası (link bazlı paneller + lookup önizleme + create/deactivate/archive + exception formu) · JSON seam · **7 dil × 47 RESX anahtarı** |
| Test | **48 yeni test** (toplam suite 505: 500 geçti, 5 önceden atlanan) |
| Smoke | Gateway-only, tenant 97c5 — §12 |

**Değiştirilmeyenler:** `ocelot.json` (protected), Account/Contact master aggregate'leri, RBAC seed/grant,
MOD-0048 publish, MOD-0151/MOD-0155 kodu.

## 3. Domain Model

### `ContactAvailability` (aggregate, `EntityBase`)

`Id` (AvailabilityId) · `TenantId` · **`AccountContactLinkId`** · `ContactId` · `AccountId` · `Weekday` ·
`StartTime` · `EndTime` · `Preference` (VO) · `AverageVisitDurationMinutes` · `AvailabilityType` · `Source` ·
`Status` · `EffectiveFrom` · `EffectiveTo` · `Notes` · `CreatedAt/CreatedBy/UpdatedAt/UpdatedBy` · soft-delete.

- **`AccountContactLinkId` tek sahiplik anahtarıdır (D8).** `ContactId` / `AccountId` **link'ten türetilir** —
  request payload'ında bu alanlar **yoktur**, dolayısıyla bir çağrı link'in sahip olmadığı bir contact/account
  çiftini iddia edemez.
- `AppointmentRequired` / `AppointmentLeadTimeDays` **yalnız `Preference` içinde saklanır**; DTO'da satır seviyesinde
  de gösterilir ama **iki kopya tutulmaz** → drift imkânsız.
- Saatler `"HH:mm"` **local wall-clock** (account lokasyonunun saati). MOD-0150 timezone master'ı sahiplenmez;
  bu karar pack §20.2'de ve kod yorumunda açıkça yazılıdır.
- `Weekday` = ISO gün adı (`monday`…`sunday`). Bu bir **takvim gerçeğidir, tenant sözlüğü değildir** → MOD-0048'e
  değil, domain'e doğrulatılır.

### `VisitPreference` (value object)

`PreferredVisitDurationMinutes` · `PreferredVisitStartTime/EndTime` · `AvoidVisitStartTime/EndTime` ·
`AppointmentRequired` · `AppointmentLeadTimeDays` · `PreferredContactMethod` · `Notes`.
Link bağlamında okunur (D10); `Avoid*` preferred'ın tersi **değil**, available window içindeki **daha güçlü
kısıttır** (D13).

### `ContactAvailabilityException` (aggregate)

`Id` · `TenantId` · `AccountContactLinkId` · `ContactId` · `AccountId` · `Date` (`yyyy-MM-dd`) · `IsAvailable` ·
`StartTime?` · `EndTime?` · `Reason?` · `Notes?` · `Source` · `Status` · audit alanları.
Haftalık deseni **ezer** (D12); `(link, date)` için tek active kayıt.

### Persistence notları

- Class map: `AccountContactLinkId`, `ContactId`, `AccountId` **string-Guid**. Bu atlanırsa filtre string, doküman
  binary serileşir ve sorgular **sessizce boş döner** (MOD-0151 `AccountTerritoryAssignment`'ın yaşadığı hata).
- Index'ler: `tenant+link`, `tenant+contact`, `tenant+account`, exception için `tenant+link+date` + 2 **unique
  partial** (yalnız `status=active`) doğal anahtar. Partial filtrelerde **yalnız `Eq`** kullanıldı — `$ne`/`$not`
  partial index filtresinde desteklenmez ve servisi başlangıçta crash-loop'a sokar.

## 4. Validation

| Kural | Davranış | Kanıt |
|---|---|---|
| `StartTime` < `EndTime` | **400** | unit + live |
| Geçerli ISO weekday | **400** | unit + live |
| Preferred ⊆ available | **400** | unit |
| Avoid window available ile çakışabilir | **kabul** (amacı bu) | unit |
| `EffectiveTo` < `EffectiveFrom` | **400** | unit |
| Inactive/ended link üzerine `active` availability | **400** | unit |
| Cross-tenant contact/account/link | **404** | unit + live |
| Aynı `(link, weekday)` + örtüşen effective aralık | **409**, **iki kaydın kimliği mesajda** | unit |
| Birebir aynı satır | **idempotent no-op** (aynı Id, 200) | unit |
| `(link, date)` ikinci active exception | **409** | unit |
| MOD-0048 seti yayınlanmamış | **fail-closed 400** | unit + contract |
| `Status` ∉ {active, inactive, archived} | **400** ("availability is never hard-deleted") | unit |
| Delete isteği | **rota yok → 405** | live |

Conflict mesajı örneği: *"An active availability window already overlaps this one on monday (existing
availabilityId=… 09:00-13:00; requested 12:00-15:00)."* — **sessiz merge/overwrite yok**.

## 5. Exceptions

- `IsAvailable=false` → o gün ziyaret edilemez; verilen pencere **yarım uygulanmaz**, tamamen düşürülür.
- `IsAvailable=true` + pencere → haftalık desende olmayan **ad-hoc** pencere; lookup'ta haftalık pencereyi ezer.
- Deactivate/archive edilen exception lookup tarafından **yok sayılır** (test: `Deactivated_Exception_Is_Ignored_By_Lookup`).
- Hard delete yok.

## 6. Lookup

`GET /api/crm/contacts/availability-lookup?date=…&contactId=…&accountId=…&accountContactLinkId=…`

Dönen satır: `AccountContactLinkId` · `ContactId` · `ContactDisplayName` · `AccountId` · `AccountDisplayName` ·
`Weekday` · `AvailableWindow` · `PreferredWindow` · `AvoidWindow` · `AppointmentRequired` ·
`AppointmentLeadTimeDays` · `AverageVisitDurationMinutes` · `AvailabilityStatus` · `ExceptionApplied` ·
`ExceptionReason` · `ReasonCodes[]`.

Karar tablosu:

| Durum | `AvailabilityStatus` | Reason code |
|---|---|---|
| Exception `IsAvailable=false` | `unavailable` | `exception_unavailable` |
| Exception `IsAvailable=true` (+pencere) | `available` | `exception_window_applied` |
| Hiç **active** availability yok | **`unknown`** | `no_availability_data` (+ `availability_inactive`) |
| O gün pencere yok | `unavailable` | `not_available_on_day` |
| Pencere var ama effective değil | `unavailable` | `outside_effective_window` |
| Pencere var ve effective | `available` | `availability_ok` |
| `AppointmentRequired` | **statüyü değiştirmez** | `appointment_required` |
| Avoid / preferred tanımlı | **statüyü değiştirmez** | `avoid_window_defined` / `preferred_window_defined` |
| Link kapalı | `unavailable` | `link_inactive` |

**"Veri yok" asla "uygun değil" değildir** (D15 / MOD-0151 R11) — ayrı testle sabitlendi. Guard testi lookup DTO'sunda
`Route/Sequence/Order/Rank/Distance/Travel/Score/Priority/Plan/Frequency/Cadence/Territory/LastVisit/Due` içeren
**hiçbir alan bulunmadığını** doğrular.

## 7. UI

| Yüzey | Durum |
|---|---|
| Contact Details (360) → **Availability kartı** | ✅ read-only; link/lokasyon bazlı satırlar + exception satırları + "Manage availability" |
| `/CRM/Contacts/Availability/{contactId}` → **yönetim sayfası** | ✅ link başına panel: availability tablosu · exception tablosu · create formu · exception formu · deactivate/archive aksiyonları |
| Lookup önizleme (aynı sayfada) | ✅ tarih seç → satırlar + statü rozeti + reason code'lar + "veri yok ≠ uygun değil" ipucu |
| Account 360 → contact availability paneli | ⛔ **eklenmedi** (backend endpoint hazır: `GET /api/crm/accounts/{id}/contact-availability`) — §15 |
| 7 dil RESX paritesi | ✅ 47 anahtar × 7 dil (en/tr/ar/es/fr/ru/zh) |
| Yasaklı öğeler | ✅ rota/visit plan/GPS/campaign/frequency/territory/workflow/**hard-delete butonu yok** |

Tüm frontend trafiği **Gateway 5000** üzerinden; 5061'e doğrudan çağrı yok. Dropdown değerleri MOD-0048 published
value'lardan gelir (yayınlanmamışsa **boş liste**, asla local fallback).

## 8. Permissions

- Canonical: `crm.contact.availability.read` / `crm.contact.availability.manage` — **tanımlandı, seed edilmedi**.
- Endpoint'ler dokümante edilmiş **fallback** ile korunuyor: read → `crm.contact.read`, manage → `crm.contact.update`
  (MOD-0151 FU08 deseninin aynısı). Fallback **yetki genişletmez**: §4'teki tüm guard'lar yine çalışır.
- **Delete permission'ı tanımlanmadı** (hard delete yok) — test bunu reflection ile doğruluyor.
- Follow-up: **`MOD-0150-FU-RBAC — Contact Availability Permission Catalog Alignment`**.

## 9. Contract Flags

`GET /api/crm/contacts/contract` (canlı yanıt):

```json
{
  "supportsContactAvailability": true,
  "supportsAccountContactLinkAvailability": true,
  "supportsVisitPreference": true,
  "supportsAvailabilityExceptions": true
}
```

`supportsVisitPlanning` / `supportsRoutePlanning` / `supportsVisitFrequency` **yok** (test guard'ı ile sabitlendi).
Contract ayrıca 13 limitation string'i ve 3 reference set readiness satırı döner.

## 10. Tests

**48 yeni test** — `ContactAvailabilityTests.cs`. Kapsam:

- **Sahiplik:** create link'ten contact/account türetir · bilinmeyen link 404 · cross-tenant 404 · aynı contact'ın iki
  linki **bağımsız takvim** taşır.
- **Validation:** start≥end · geçersiz weekday · preferred ⊄ available · avoid overlap kabul · effective ters ·
  inactive link · yayınlanmamış set (fail-closed) · geçersiz source değeri · overlap 409 (**iki kimlik mesajda**) ·
  non-overlap kabul · **idempotent no-op** · kapalı satıra karşı overlap tetiklenmez · update overlap · update
  link/derived id'leri korur · desteklenmeyen status.
- **Lifecycle:** deactivate → inactive · archive → archived · satır **silinmez** · repository'lerde **delete üyesi
  yok** (reflection).
- **Master koruması:** yazma yolları Contact/Account/link master'ını **hiç güncellemez** (çağrı sayacı 0) ·
  `Contact` aggregate'inde availability/weekday/visitpreference alanı **yok** (reflection).
- **Exception:** create · duplicate 409 · geçersiz tarih · bozuk pencere · deactivate → lookup yok sayar.
- **Lookup:** pencere + preferred + appointment + duration · avoid reason · **veri yok → unknown** · başka gün →
  unavailable · exception override · ad-hoc pencere · effective tarih filtresi · yalnız kapalı satır → unknown ·
  geçersiz tarih 400 · filtresiz 400 · cross-tenant boş · **çok-account'lu contact → link başına satır**.
- **Guard/sözleşme:** lookup DTO'sunda rota/plan alanı yok · permission anahtarları canonical + delete yok ·
  contract flag'leri yalnız availability.

**Suite sonucu:** `Başarılı! - Başarısız: 0, Başarılı: 500, Atlanan: 5, Toplam: 505`.

> **Bulunan flaky test (bu FU'ya ait değil):** `ContactLocationPiiHardeningTests.PiiMasking_Redacts_Email_And_Phone_But_Keeps_Guid_And_Country`
> bir çalıştırmada **kırmızı** oldu. Sebep: test `Guid.NewGuid()` kullanıyor; üretilen GUID'in son bloğu uzun bir rakam
> dizisi içerdiğinde (`…dab113838954`) telefon maskeleme regex'i GUID'in içini maskeliyor. Rastgele ~%1 olasılıklı,
> **PiiMasking regex'inin gerçek bir kusuru**; benim değişikliklerimle ilgisi yok (`PiiMasking.cs` bu task'ta
> okunmadı bile) ve MOD-0150 PII hardening FU'suna ait olduğu için **kasıtlı olarak düzeltilmedi** — ayrı bir karar
> gerektirir. Sonraki 3 tekrar ve tam suite yeşildir.

## 11. Live Smoke — hazırlık

| Kontrol | Sonuç |
|---|---|
| Gateway 5000 / Web 5001 / Auth 5056 / Platform 5057 / CRM 5061 | **200 / 200 / 200 / 200 / 200** |
| Deploy doğrulaması | contract `runtimeScope` içinde **`FU-contact-availability-visit-preference`** görünüyor → çalışan assembly yeni kod |
| Oturum | `admin@diten.com` (SuperAdmin) — token `crm.contact.read/create/update/...` taşıyor |
| Tenant | CRM tenant'ı **`X-Tenant-Id` header'ından** çözüyor (payload'dan değil): header ile `97c59330-…cc93`, header'sız `…0001` |
| Direct 5061 | yalnız `/health`; business çağrısı **yok** |

## 12. Live Smoke — sonuçlar

### 12.1 Contract

```
moduleId=MOD-0150
flags: supportsContactAvailability=true · supportsAccountContactLinkAvailability=true ·
       supportsVisitPreference=true · supportsAvailabilityExceptions=true
isReady=false
missingSets=contact-availability-type, contact-availability-source, contact-availability-status
```

Üç MOD-0048 seti **yayınlanmamış** (valueCount=0) → yazma yolu tasarım gereği **fail-closed**.

### 12.2 Read + validation

| Çağrı | Sonuç |
|---|---|
| `GET /api/crm/contacts/{bilinmeyen}/availability` | **404** |
| `GET /api/crm/contacts/links/{bilinmeyen}/availability` | **404** |
| `GET /api/crm/accounts/{bilinmeyen}/contact-availability` | **404** |
| `GET .../availability-lookup?date=2026-08-03&contactId=…` | **200**, `weekday=monday`, rows=0 |
| `date=2026-08-04` / `2026-09-12` | `weekday=tuesday` / `saturday` ✅ (takvim doğru) |
| `date=not-a-date` | **400** |
| filtresiz lookup | **400** |

### 12.3 Write guard'ları

| Çağrı | Sonuç |
|---|---|
| `POST contacts/links/{bilinmeyen}/availability` | **404** |
| `POST contacts/links/{bilinmeyen}/availability-exceptions` | **404** |
| `PUT contacts/availability/{bilinmeyen}` | **404** |
| `POST contacts/availability/{bilinmeyen}/deactivate` | **404** |
| `POST contacts/availability/{bilinmeyen}/archive` | **404** |
| `DELETE contacts/links/{id}/availability` | **405** (rota yok) |
| `DELETE contacts/availability/{id}` | **405** (rota yok) |

### 12.4 Authorization + UI

| Kontrol | Sonuç |
|---|---|
| Token'sız `contract` / `availability` / `lookup` | **401 / 401 / 401** |
| Anonim `/CRM/Contacts` ve `/CRM/Contacts/Availability/{id}` | **302 → login** (rota mevcut) |
| Oturumlu `/CRM/Contacts` | **200** |
| Oturumlu `/CRM/Contacts/Availability/{bilinmeyen}` | **200** (kontrollü redirect → Contacts listesi; uydurma sayfa yok) |
| Oturumlu `/CRM/Contacts/availability-data/{bilinmeyen}` | **200** `{"success":false,"data":[]}` |

### 12.5 Çalıştırılamayan pozitif akış

Pazartesi 09:00–13:00 → lookup available → exception → unavailable → overlap 409 → idempotent tekrar **canlıda
çalıştırılamadı**. İki bağımsız sebep:

1. **MOD-0048 setleri yayınlanmamış** — availability create'i fail-closed 400 verir; publish bu task'ta **yasak**.
2. **Dev veritabanında CRM fixture'ı yok** — tenant 97c5 (ve …0001) içinde **0 contact / 0 account** var, dolayısıyla
   üzerine availability yazılacak bir `AccountContactLink` yok. Fixture üretmek Account/Contact **master yazımı**
   gerektirirdi; task bunu açıkça yasaklıyor, üstelik (1) nedeniyle akışı yine açmazdı.

Bu akışın tamamı **unit test seviyesinde uçtan uca kanıtlanmıştır** (§10) — canlı boşluk veri/publish kaynaklıdır,
kod kaynaklı değildir.

## 13. Guard Checks

| Kontrol | Sonuç |
|---|---|
| Availability Contact düz alanı mı oldu? | **Hayır** (reflection testi + link-scoped aggregate) |
| Route optimization / günlük rota / visit plan | **Yok** |
| Visit execution / check-in-out / GPS / visit report | **Yok** |
| Campaign engine / frequency-call-cycle engine | **Yok** |
| Territory assignment / `ContactTerritoryAssignment` | **Yok** |
| Account master mutate | **Hayır** (test: UpdateCalls=0) |
| Contact master mutate | **Hayır** (test: UpdateCalls=0) |
| Territory model mutate | **Yok** |
| Patient data | **Yok** |
| Workflow / ChangeRequest / MOD-0023 | **Yok** |
| Evidence pack | **Yok** |
| Yeni import/export scope | **Yok** |
| Brand/Product master | **Yok** |
| Hard delete | **Yok** (repository'de delete üyesi yok; DELETE rotası 405) |
| Mongo hand-edit | **Yok** |
| RBAC seed/grant değişikliği | **Yok** (yalnız fallback + follow-up) |
| MOD-0048 publish değişikliği | **Yok** (3 set öneri olarak kaldı) |
| `ocelot.json` değişikliği | **Yok** (protected; mevcut wildcard'lara alias verildi) |
| Direct 5061 business çağrısı | **Yok** (yalnız `/health`) |
| Payload'da `TenantId` | **Yok** (claim/header'dan) |
| Payload'da `ContactId`/`AccountId` | **Yok** (link'ten türetiliyor) |
| MOD-0151 / MOD-0155 kodu | **Değişmedi** |

## 14. Created / Updated Files

**Created — backend (11):**

- `services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/ContactAvailability.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/ContactAvailabilityException.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Domain/Repositories/IContactAvailabilityRepository.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Domain/Repositories/IContactAvailabilityExceptionRepository.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Persistence/Repositories/ContactAvailabilityRepository.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Persistence/Repositories/ContactAvailabilityExceptionRepository.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Application/Common/IActorContext.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Application/Features/ContactAvailability/` →
  `ContactAvailabilityModels.cs` · `ContactAvailabilityValidation.cs` · `ContactAvailabilityMapper.cs` ·
  `Commands/ContactAvailabilityCommands.cs` · `Queries/ContactAvailabilityQueries.cs` ·
  `Handlers/ContactAvailabilityCommandHandlers.cs` · `Handlers/ContactAvailabilityQueryHandlers.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Application/Features/Contact/Contract/ContactContract.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Infrastructure/HttpActorContext.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Api/Models/CRM/ContactAvailabilityRequests.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Api/Controllers/CRM/ContactAvailabilityController.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Api/Controllers/CRM/ContactContractController.cs`

**Created — frontend / test (3):**

- `frontend/Diten.Web/Models/CRM/ContactAvailabilityViewModels.cs`
- `frontend/Diten.Web/Views/CRM/Contacts/Availability.cshtml`
- `services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/ContactAvailabilityTests.cs`

**Updated (12):**

- `services/Diten.CrmService/src/Diten.CrmService.Persistence/DependencyInjection.cs` (repo kayıtları + class map'ler + 7 index)
- `services/Diten.CrmService/src/Diten.CrmService.Infrastructure/DependencyInjection.cs` (`IActorContext`)
- `frontend/Diten.Web/Controllers/CRM/ContactsController.cs` (availability sayfası + 6 aksiyon + 3 loader + 2 set kodu)
- `frontend/Diten.Web/Models/CRM/ContactViewModels.cs` (`ContactOverviewViewModel.Availability`)
- `frontend/Diten.Web/Views/CRM/Contacts/Details.cshtml` (read-only availability kartı)
- `frontend/Diten.Web/Resources/Views/CRM/Contacts/ContactIndex.{en,tr,ar,es,fr,ru,zh}.resx` (**7 × 47 anahtar**)

**Created — bu rapor:** `docs/audits/mod-0150-contact-availability-visit-preference-implementation-2026-08-01.md`

**Kasıtlı olarak değiştirilmeyen:** `gateway/Diten.ApiGateway/ocelot.json` · Account/Contact aggregate'leri ·
RBAC seed · MOD-0048 reference data · MOD-0151/MOD-0155 kaynakları · MOD-0150 module pack (§20 zaten bu scope'u
tanımlıyor).

## 15. Final Verdict

**PARTIAL**

**Tamamlanan (PASS kriterleri):**

- ✅ `ContactAvailability` **`AccountContactLink` bazlı** uygulandı; `ContactId`/`AccountId` link'ten türetiliyor.
- ✅ Contact master'a **düz availability alanı eklenmedi** (reflection guard'ı ile sabit).
- ✅ `VisitPreference` ve date-specific exception desteklendi; exception haftalık deseni eziyor.
- ✅ Validation / overlap (409, iki kimlik) / idempotency doğru; **sessiz merge/overwrite yok**.
- ✅ Lookup `available` / `unavailable` / **`unknown`** ayrımını doğru yapıyor; "veri yok ≠ uygun değil".
- ✅ MOD-0151 read-only tüketim ve MOD-0155 planning boundary'leri korunuyor (rota/sıra/skor alanı yok).
- ✅ UI minimal ve doğru; hard-delete/rota/GPS/frequency butonu yok; 7 dil RESX paritesi.
- ✅ Contract flag'leri doğru; planning flag'i eklenmedi.
- ✅ **Build 0 hata** (CrmService + Gateway + Web), **testler 500/505 yeşil** (5 önceden atlanan).
- ✅ Route/visit/frequency/campaign scope'u, patient data, `ContactTerritoryAssignment`, Account/Contact master
  mutasyonu **açılmadı**.

**PARTIAL nedenleri (üçü de task'ın kendi PARTIAL kriterlerinde yazılı):**

1. **Reference set'ler operator publish bekliyor.** `contact-availability-type` / `-source` / `-status` yayınlanmamış
   → canlı **pozitif yazma akışı** (create → lookup available → exception → 409 → idempotent) çalıştırılamadı.
   Fail-closed davranış **doğru** ve canlı contract çıktısıyla kanıtlı; akışın tamamı unit testlerde yeşil.
   *(Ek engel: dev DB'de tenant 97c5 için hiç Contact/Account/link fixture'ı yok ve master yazımı bu task'ta yasak.)*
2. **Permission catalog fallback kullanıldı** (`crm.contact.read` / `crm.contact.update`); canonical anahtarlar
   tanımlı ama seed edilmedi → `MOD-0150-FU-RBAC` follow-up'ı açık.
3. **UI browser etkileşimi sınırlı doğrulandı** (sayfa 200/302 + JSON seam doğrulandı; veri olmadığı için form
   gönderimi canlı denenemedi) ve **Account 360 read-only availability paneli eklenmedi** — backend endpoint'i
   (`GET /api/crm/accounts/{id}/contact-availability`) hazır, yüzey ayrı bir küçük iş olarak bırakıldı.

**Ek teknik borç (raporlandı, düzeltilmedi):**

- **Gateway rotaları:** `ocelot.json` `integration-agent` sahipliğinde olduğu için kanonik
  `/api/crm/contact-availability/*`, `/api/crm/contact-availability-exceptions/*` ve
  `/api/crm/account-contact-links/*` yolları Gateway üzerinden **404**'tür. Controller bu kanonik yolları **da**
  taşıyor; bugün çalışan yüzey mevcut wildcard'lara oturan alias'lardır
  (`/api/crm/contacts/links/{linkId}/availability`, `/api/crm/contacts/availability/{id}`,
  `/api/crm/contacts/availability-lookup`, `/api/crm/accounts/{accountId}/contacts/{linkId}/availability`).
  → **integration-agent follow-up**: 3 wildcard rota ekle.
- **Flaky test:** `PiiMasking_Redacts_…` (§10 notu) — bu FU'ya ait değil, ayrı karar gerektirir.

## 16. Next Recommended Prompt

Bu FU'yu PASS'e taşımak için (sırayla):

```
MOD-0048 operator — publish contact-availability-type / contact-availability-source / contact-availability-status (tenant 97c5)
@integration-agent — add ocelot routes: /api/crm/contact-availability/{everything}, /api/crm/contact-availability-exceptions/{everything}, /api/crm/account-contact-links/{everything}
MOD-0150-FU-RBAC — Contact Availability Permission Catalog Alignment
```

Sonraki modül adımı:

```
MOD-0151 FU09A — Visit/Route Readiness Implementation
```
