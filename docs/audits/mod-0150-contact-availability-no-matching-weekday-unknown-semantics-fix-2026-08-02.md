# MOD-0150-FU — No-Matching-Weekday Unknown Semantics Fix

- **Tarih:** 2026-08-02
- **Tenant:** `97c59330-dbc4-4665-b29c-0c26dbb5cc93`
- **Fixture:** Contact `13a3c0c0-d060-4651-9f6c-231a139d3b1e`; Account `88c1b88a-53e5-4098-8c7e-18bb4d7fec02`; Link `8c34d497-cbd0-49e5-be7f-aca41df3e79d`
- **Görev tipi:** Mevcut MOD-0150 lookup semantiğine minimal backend düzeltmesi ve targeted live smoke retry
- **Final verdict:** **PASS**

## 1. Preflight

| Kontrol | Sonuç |
|---|---|
| Gateway 5000 | **PASS** — `/health` HTTP 200 |
| Web 5001 | **PASS** — uygulama HTTP 200 ve authenticated CRM UI erişilebilir |
| Auth 5056 | **PASS** — `/health` HTTP 200 |
| Platform 5057 | **PASS** — `/health` HTTP 200 |
| CRM 5061 | **PASS** — yalnız `/health` çağrıldı, HTTP 200 |
| MongoDB 27017 | **PASS** — TCP listener erişilebilir |
| Business trafik | **PASS** — authenticated Web MVC proxy üzerinden Gateway 5000 zinciri kullanıldı; direct 5061 business çağrısı yok |
| Tenant contract | **PASS** — target tenant oturumu kullanıldı; create/exception payload'larına `TenantId` konmadı |

Pack gate: `execution/domains/commercial-suite/module-packs/MOD-0150-contact-relationship-management.md` `ready-for-dev`; D15 bu düzeltmeyi yetkilendiriyor. MOD-0151 FU09A R11 boundary'si “veri yok ≠ uygun değil” ayrımını zorunlu kılıyor.

## 2. Previous Failure

Referans smoke raporunda aynı link için active Monday availability varken `2026-08-04` Tuesday lookup şu yanlış sonucu verdi:

- `AvailabilityStatus = unavailable`
- `ReasonCodes = [not_available_on_day]`

Beklenen sonuç `unknown + no_availability_data` idi. Önceki smoke'un Monday positive, exception override, overlap, idempotency ve cleanup kontrolleri geçmesine rağmen bu tek semantik sapma final verdict'i FAIL yapmıştı.

## 3. Root Cause

`LookupContactAvailabilityHandler.BuildRows` active lifecycle satırlarını önce weekday'e, sonra effective date'e göre filtreliyordu. `effectiveDayRows.Count == 0` dalı iki ayrı durumu tek unavailable kararı altında birleştiriyordu:

1. İstenen weekday için hiç satır yoktu.
2. İstenen weekday için satır vardı fakat tarih effective aralığın dışındaydı.

Dal, ilk durumda `not_available_on_day` üretiyordu. Böylece başka bir weekday'de verinin bulunması, hedef weekday hakkında açık bir olumsuz veriymiş gibi yorumlanıyordu. Exception override daha önce çalıştığı için kök neden exception logic değil, weekday absence branch'iydi.

## 4. Implementation Fix

`effectiveDayRows.Count == 0` dalı `hasRowsForWeekday` ile ayrıldı:

- `dayRows.Count == 0` → `unknown + no_availability_data`
- aynı weekday satırı var fakat effective date dışında → mevcut `unavailable + outside_effective_window` davranışı korunur
- active `IsAvailable=false` exception → mevcut `unavailable + exception_unavailable` davranışı korunur

`not_available_on_day` sabiti geriye dönük kontrat/negatif assertion amacıyla korunuyor fakat no-matching-weekday dalı artık bu kodu üretmiyor. DTO, controller, route, persistence, frontend ve gateway kontratı değişmedi.

Lookup yalnız tarih aldığı için bir “requested time” değerlendirmesi yapmaz. Preferred ve avoid pencereleri DTO'da taşınır; `preferred_window_defined`, `avoid_window_defined` ve `appointment_required` non-blocking reason/warning olarak status'ü değiştirmez. Gerçek `outside_preferred_window` değerlendirmesi requested-time sahibi FU09A consumer boundary'sine aittir; bu fix yeni zaman/route/visit engine açmadı.

## 5. Test Updates

`Lookup_Other_Weekday_Returns_Unavailable` testi `Lookup_Other_Weekday_Returns_Unknown_Not_Unavailable` olarak güncellendi ve şu üç assertion ile regresyonu sabitledi:

- status `unknown`
- reasons `no_availability_data` içerir
- reasons `not_available_on_day` içermez

Mevcut targeted suite ayrıca şunları doğruluyor:

- hiç availability yok → `unknown + no_availability_data`
- unavailable exception → `unavailable + exception_unavailable`
- preferred window, avoid window ve appointment-required sinyalleri available status'ü bozmaz
- overlap controlled conflict ve exact duplicate idempotency korunur
- DTO'da route, sequence/order, distance/travel, score/rank, plan, frequency/cadence, territory veya due alanı yoktur
- Contact master availability alanı taşımamaya devam eder; hard-delete kontratı yoktur

Komut ve sonuçlar:

- `dotnet test ...Diten.CrmService.Application.Tests.csproj -c Debug --filter FullyQualifiedName~ContactAvailabilityTests --no-restore` → **48/48 PASS**, failed 0, skipped 0
- `dotnet build ...Diten.CrmService.Api.csproj -c Debug --no-restore` → **PASS**, 0 warning, 0 error
- API validator betiği denenmiş ancak host'ta `python`/`python3` bulunmadığı için başlatılamamıştır; .NET build ve targeted suite canonical doğrulama olarak geçmiştir.

## 6. Targeted Live Smoke Retry

Tüm write/lookup adımları authenticated Web UI'nin Gateway proxy akışıyla yapıldı.

| Adım | Kanıt | Sonuç |
|---|---|---|
| Minimal availability create | Availability `4445c617-ee24-4450-b6b8-51ca88ed2812`; Monday `09:00–13:00`; preferred `10:00–12:00`; appointment `true`; average `20`; type `working-hours`; source `manual`; active | **PASS** |
| Monday lookup `2026-08-03` | `Available`; `09:00-13:00`; preferred `10:00-12:00`; reasons `availability_ok`, `appointment_required`, `preferred_window_defined` | **PASS** |
| Tuesday lookup `2026-08-04` | `Unknown`; reasons yalnız `no_availability_data`; `not_available_on_day` yok | **PASS** |
| Unavailable exception | Exception `ea4281f3-3179-4c3c-b2a7-b0fd25c3fbff`; date `2026-08-10`; `IsAvailable=false`; reason `smoke unavailable` | **PASS** |
| Exception lookup | `Unavailable`; exception applied; reason `exception_unavailable` | **PASS** |
| Exact duplicate | Aynı availability payload'ı tekrar gönderildi; success/no-op, satır sayısı 2 (önceki archived + yeni active) kaldı | **PASS** |
| Overlap | Monday `11:00–14:00` denemesi mevcut `4445c617-... 09:00-13:00` ile controlled conflict verdi; yeni satır yazılmadı | **PASS (409 contract)** |
| Soft cleanup | Availability `4445c617-...` → `archived`; exception `ea4281f3-...` → `inactive`; history satırları korunuyor | **PASS** |

Account ve Contact master oluşturulmadı/değiştirilmedi. Eski archived availability `286109d9-4dc0-4ac3-be5a-43a2abdc76a9` ve inactive exception `eb992781-3e50-4c0d-9dc9-f49d1a7d67eb` history olarak korunmaya devam ediyor.

## 7. Guard Checks

| Guard | Sonuç |
|---|---|
| Runtime code changed? | **Yes** — yalnız minimal lookup semantic branch + açıklayıcı status doc comment |
| Backend / tests changed? | **Yes / Yes** |
| Frontend / gateway changed? | **No / No** |
| RBAC seed/grant changed? | **No** |
| MOD-0048 publish changed? | **No** |
| MOD-0151 / MOD-0155 code changed? | **No / No** |
| Route / visit planning opened? | **No / No** |
| Frequency / campaign engine opened? | **No / No** |
| Territory assignment / `ContactTerritoryAssignment` opened? | **No / No** |
| Account / Contact master mutation? | **No / No** |
| Patient data / workflow / import-export opened? | **No / No / No** |
| Hard delete / Mongo hand-edit used? | **No / No** |
| Direct 5061 business call? | **No** — direct port yalnız health için |
| Payload'da `TenantId`? | **No** |

## 8. Created / Updated Files

- **Updated:** `services/Diten.CrmService/src/Diten.CrmService.Application/Features/ContactAvailability/Handlers/ContactAvailabilityQueryHandlers.cs`
- **Updated:** `services/Diten.CrmService/src/Diten.CrmService.Application/Features/ContactAvailability/ContactAvailabilityModels.cs`
- **Updated:** `services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/ContactAvailabilityTests.cs`
- **Created:** `docs/audits/mod-0150-contact-availability-no-matching-weekday-unknown-semantics-fix-2026-08-02.md`

Repo'nun önceden mevcut kirli çalışma ağacındaki ilgisiz değişikliklere dokunulmadı.

## 9. Final Verdict

**PASS**

- No-matching-weekday artık `unknown + no_availability_data` döndürüyor.
- `not_available_on_day` üretilmiyor.
- Monday available ve appointment warning davranışı korunuyor.
- Exception unavailable, overlap conflict ve duplicate idempotency korunuyor.
- Contact/Account master ve tenant izolasyon kontratı korunuyor.
- Route/visit/frequency/campaign/workflow/territory scope'u açılmadı.
- Build, 48 targeted test ve canlı smoke retry geçti; smoke verileri soft-state ile temizlendi.

## 10. Next Recommended Prompt

`MOD-0151 FU09A — Visit/Route Readiness Implementation`
