# MOD-0150-FU — Contact Availability Positive Live Smoke Retry

- **Tarih:** 2026-08-02
- **Tenant:** `97c59330-dbc4-4665-b29c-0c26dbb5cc93`
- **Modül:** MOD-0150 — Contact & Relationship Management
- **Servis:** `Diten.CrmService` (CRM 5061; business trafik Gateway 5000 üzerinden)
- **Görev tipi:** Mevcut implementasyon için canlı pozitif smoke retry; yeni feature/code expansion değildir
- **Final verdict:** **FAIL**

## 1. Preflight

| Kontrol | Sonuç |
|---|---|
| Gateway 5000 | **PASS** — `/health` 200 |
| Web 5001 | **PASS (reachable)** — uygulama ve authenticated CRM sayfaları çalıştı; `/health` bu uygulamada tanımlı değil ve 404 dönüyor |
| Auth 5056 | **PASS** — `/health` 200; MongoDB check healthy |
| Platform 5057 | **PASS** — `/health` 200; MongoDB/RabbitMQ/Hangfire healthy |
| CRM 5061 | **PASS** — yalnız `/health` çağrıldı, 200 |
| MongoDB 27017 | **PASS** — listener mevcut |
| Tenant | **PASS** — mevcut authenticated tenant oturumu ve tenant `97c5` verileri kullanıldı; payload'larda `TenantId` yok |
| Business trafik | **PASS** — Web MVC proxy çağrıları Gateway 5000 üzerinden yürüdü; direct 5061 business çağrısı yok |
| Pack gate | **PASS** — `MOD-0150-contact-relationship-management.md` status `ready-for-dev`, FU availability scope'u yetkili |

Anonim kontrol: Gateway contract, link availability ve lookup uçları token olmadan **401**; Web Availability sayfası **302 → login** döndürdü.

## 2. Reference Set Publish Verification

Platform Reference Data UI'da immutable published version'lar doğrulandı:

| Set | VersionId | Durum | Değerler | Duplicate code |
|---|---|---|---|---|
| `contact-availability-type` | `9c1489d5-556a-4e53-9fe1-3fb5a54d833f` | **Published** | 7: `working-hours`, `visiting-hours`, `preferred-window`, `restricted-window`, `appointment-only`, `temporary-exception`, `other` | Yok |
| `contact-availability-source` | `13f2d0d4-5007-4c6f-9fa6-99c8d74d12a5` | **Published** | 7: `manual`, `legacy-import`, `contact-confirmed`, `account-confirmed`, `field-observation`, `campaign-input`, `other` | Yok |
| `contact-availability-status` | `4047773f-c251-4b8a-a956-b43b3efd763e` | **Published** | 3: `active`, `inactive`, `archived` | Yok |

Pozitif `working-hours/manual/active` create'in başarılı olması, önceki `reference-missing` fail-closed blocker'ının kalktığını ayrıca kanıtladı. Bu task set/version/publish state değiştirmedi; yalnız okudu ve doğruladı.

Contract guard'ı: `ContactFeatureFlags` yalnız şu dört alanı taşıyor ve 48/48 targeted test geçti:

- `supportsContactAvailability = true`
- `supportsAccountContactLinkAvailability = true`
- `supportsVisitPreference = true`
- `supportsAvailabilityExceptions = true`
- `supportsVisitPlanning`, `supportsRoutePlanning`, `supportsVisitFrequency` yok

## 3. Fixture Selection / Setup

Tenant içinde mevcut master data kullanıldı; Account veya Contact oluşturulmadı/değiştirilmedi.

| Nesne | Kimlik / değer |
|---|---|
| Contact | `13a3c0c0-d060-4651-9f6c-231a139d3b1e` — Uz. Dr. Ahmet YALINKILINÇ (active) |
| Account | `88c1b88a-53e5-4098-8c7e-18bb4d7fec02` — Özel Keşan Hastanesi (active) |
| AccountContactLink | `8c34d497-cbd0-49e5-be7f-aca41df3e79d` (active) |

İlk incelenen `Dr. Beste Pullukçu` linkleri inactive olduğu için kontrollü 400 ile reddedildi; hiçbir availability yazılmadı. `Uğur Dilek Calap` linkinde mevcut Pazartesi penceresi bulunduğu için mevcut veriyi mutate etmeden bırakıldı. Yeni master fixture oluşturulmadı.

## 4. Positive Create

**PASS** — Web UI formu server-side Gateway proxy üzerinden gönderildi; başarı mesajı ve history satırı görüldü.

- AvailabilityId: `286109d9-4dc0-4ac3-be5a-43a2abdc76a9`
- LinkId: `8c34d497-cbd0-49e5-be7f-aca41df3e79d`
- Monday `09:00–13:00`
- Preferred `10:00–12:00`
- Avoid `12:00–13:00`
- AppointmentRequired `true`; lead time `1`; average duration `20`
- Type `working-hours`; source `manual`; status `active`

Payload'da `TenantId`, `ContactId`, `AccountId` yoktu. ContactId/AccountId link'ten türetildi. MVC form sonu 302 redirect olsa da Gateway backend write başarılıydı ve tek active satır canlı listede göründü.

## 5. Lookup Available

**PASS** — `2026-08-03` Pazartesi lookup:

- Status: `Available`
- Available: `09:00–13:00`
- Preferred: `10:00–12:00`
- Avoid: `12:00–13:00`
- ReasonCodes: `availability_ok`, `appointment_required`, `avoid_window_defined`, `preferred_window_defined`
- Appointment ve average duration create/list kontratında doğru kaldı; lookup status `unknown` olmadı.

## 6. Lookup Unknown

**FAIL** — availability satırı olmayan `2026-08-04` Salı lookup beklenen kontratı karşılamadı:

- **Beklenen:** `unknown` + `no_availability_data`; `contact_not_available_on_day` üretilmemeli.
- **Gerçek:** `Unavailable` + `not_available_on_day`.

Bu, task'ın açık FAIL kriteridir. Ayrıca repo içinde bir kontrat drift'i vardır: pack/contract limitation metni "missing availability is unknown" derken mevcut unit test listesi "başka gün → unavailable" davranışını sabitliyor. Bu smoke task'ında runtime kod değiştirilmedi.

## 7. Exception Unavailable Override

**PASS** — `2026-08-03` için active exception oluşturuldu:

- ExceptionId: `eb992781-3e50-4c0d-9dc9-f49d1a7d67eb`
- IsAvailable: `false`
- Reason: `congress`
- Source: `manual`
- Lookup: `Unavailable`, `Exception applied`, reason `exception_unavailable`

Haftalık active Pazartesi penceresi date-specific exception tarafından override edildi.

## 8. Exception Conflict

**PASS** — aynı link + `2026-08-03` için ikinci active exception denemesi controlled conflict verdi:

`An active availability exception already exists ... (exceptionId=eb992781-3e50-4c0d-9dc9-f49d1a7d67eb).`

İkinci satır yazılmadı; list count 1 kaldı; sessiz merge/overwrite olmadı. Controller/handler contract'ı bu sonucu HTTP 409 olarak üretir, MVC proxy backend mesajını UI'a taşıdı.

## 9. Overlap Conflict

**PASS** — aynı link + Monday `11:00–14:00` create denemesi controlled conflict verdi:

`An active availability window already overlaps ... (existing availabilityId=286109d9-4dc0-4ac3-be5a-43a2abdc76a9 09:00-13:00; requested 11:00-14:00).`

Yeni satır yazılmadı; active/list count 1 kaldı; sessiz merge/overwrite olmadı. Handler contract'ı HTTP 409'dur.

## 10. Duplicate Idempotency

**PASS** — ilk availability payload'ı birebir tekrar gönderildi. İşlem controlled success/no-op oldu; AvailabilityId değişmedi ve toplam satır/active count **1** kaldı.

## 11. Deactivate / Archive

**PASS** — smoke cleanup soft-state ile yapıldı:

- Exception `eb992781-...` → `inactive`; history listesinde görünmeye devam ediyor.
- Availability `286109d9-...` → `archived`; history listesinde tüm pencere/preference alanlarıyla görünmeye devam ediyor.
- Hard delete kullanılmadı.
- Archive + inactive exception sonrası `2026-08-03` lookup: `Unknown`, reasons `no_availability_data`, `availability_inactive`; archived satır active availability sayılmadı.

## 12. UI Smoke

| Kontrol | Sonuç |
|---|---|
| Contact Details → Availability | **PASS** — read-only Availability kartı ve `Manage availability` linki mevcut |
| Management page | **PASS** — link paneli, availability/exception tabloları, create, deactivate/archive ve lookup preview mevcut |
| Account 360 read-only panel | **PARTIAL / unchanged** — backend endpoint hazır; panel mevcut implementasyonda hâlâ yok |
| Yasaklı aksiyonlar | **PASS** — Route oluştur, Visit plan, GPS/check-in/out, campaign/frequency config, territory edit, workflow approval ve hard-delete butonu yok |
| RESX parity | **PASS** — `ContactIndex.{en,fr,es,zh,ar,ru,tr}.resx`: her dil 171 key, duplicate 0, missing/extra 0 |
| Raw key leakage | **PASS** — smoke edilen Contact Details/Availability yüzeylerinde raw resource key görülmedi |

## 13. Permission / Auth Smoke

- Token'sız Gateway contract/link/lookup: **401**.
- Anonim Web Availability: **302 → `/account/login`**.
- Read uçları fallback `crm.contact.read`; manage uçları fallback `crm.contact.update` ile canlı çalıştı.
- Canonical `crm.contact.availability.read/manage` kodda tanımlı, Auth seed/grant kataloğunda seed edilmemiş; `MOD-0150-FU-RBAC` follow-up'ı korunuyor.
- Delete permission yok; delete endpoint/hard delete yok.

## 14. Guard Checks

| Guard | Sonuç |
|---|---|
| Runtime code changed? | No |
| Backend/frontend/gateway changed? | No |
| RBAC seed/grant changed? | No |
| MOD-0048 publish changed during task? | No — yalnız doğrulandı |
| MOD-0151 / MOD-0155 code changed? | No / No |
| Route / visit / frequency / campaign engine opened? | No / No / No / No |
| Territory assignment / ContactTerritoryAssignment opened? | No / No |
| Account / Contact master mutation? | No / No; mevcut fixture kullanıldı |
| Patient data / workflow / import-export opened? | No / No / No |
| Hard delete / Mongo hand-edit used? | No / No |
| Direct 5061 business call? | No; yalnız `/health` |
| TenantId payload? | No |

Targeted regression: `dotnet test ... --filter FullyQualifiedName~ContactAvailabilityTests --no-restore` → **48/48 PASS**, skipped 0. Bu suite ownership derivation, master non-mutation, conflicts, idempotency, lifecycle, no-delete, permission ve flag guard'larını kapsıyor.

## 15. Created / Updated Files

- **Created:** `docs/audits/mod-0150-contact-availability-visit-preference-positive-live-smoke-retry-2026-08-02.md`
- Production code, tests, frontend, gateway, Auth seed/grant, module pack ve MOD-0048 reference data değiştirilmedi.
- Runtime smoke data: Availability oluşturuldu ve `archived`; exception oluşturuldu ve `inactive`. Hard delete yok.

## 16. Final Verdict

**FAIL**

Published reference set blocker'ı kalktı ve positive create, available lookup, exception override, iki conflict, exact duplicate idempotency ve archive lifecycle geçti. Ancak availability satırı bulunmayan Salı günü lookup'ı task'ın zorunlu `unknown/no_availability_data` kontratı yerine **`Unavailable/not_available_on_day`** döndürdü. Task'ın açık FAIL kriteri nedeniyle Account 360 paneli/RBAC fallback gibi non-blocking notlardan bağımsız olarak verdict PASS veya PARTIAL olamaz.

## 17. Next Recommended Prompt

```text
@orchestrator MOD-0150-FU — No-Matching-Weekday Unknown Semantics Fix + Positive Live Smoke Retry

Amaç: LookupContactAvailability içinde link için active haftalık satır bulunsa bile sorgulanan weekday'e eşleşen active/effective satır yoksa `AvailabilityStatus=unknown` ve `ReasonCodes=no_availability_data` dönmesini sağla; `unavailable/not_available_on_day` üretme. Mevcut pack/contract D15 ile unit test drift'ini hizala, targeted tests ekle/güncelle, sonra tenant 97c59330-dbc4-4665-b29c-0c26dbb5cc93 üzerinde Gateway-only positive smoke zincirini yeniden çalıştır. Route/visit/frequency/campaign/territory/workflow scope açma.
```
