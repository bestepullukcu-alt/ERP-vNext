---
id: MOD-0027-FU04C
name: Tenant Producer EventCode Migration
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: custom-integration
entity_base: none
status: ready-for-dev
parent: MOD-0027
depends_on:
  - MOD-0027-FU04A
  - MOD-0027-FU04B
owner: ali.tufanoglu
branch: feature/pss/mod-0027-fu04c-tenant-producer-eventcode-migration
started: 2026-07-09
target: TBD
form_field_count: 0
---

# MOD-0027-FU04C - Tenant Producer EventCode Migration

> **Identity (DCP-002 — GATE, ready-for-dev öncesi):** Talep edilen `MOD-0027-FU04B-Tenant` kimliği **DCP-002 fail-closed gate tarafından BLOCKED** (kanonik olmayan `-Tenant` suffix'i; Blueprint'te yok, repo-only EA reservation kanıtı yok). **Kanonik form: `MOD-0027-FU04C`** — preflight **PASS**:
> `py .antigravity/scripts/verify_module_id.py . --check-id MOD-0027-FU04C --name "Tenant Producer EventCode Migration" --parent MOD-0027` → `OK` (exit 0).
> `FU04B-Tenant` yalnızca **informal açıklayıcı etiket**tir; runtime/registry kimliği **FU04C**'dir. **Registry satırı EKLENDİ** (`MOD-0027-FU04C | Follow-up | reserved → ready-for-dev | parent MOD-0027`). **Owner review (architecture-ba-reviewer) PASS** — mimari/precision blocker yok; Karar A + Karar B netleşti (2026-07-09). **Status: `ready-for-dev`** — implementasyon başlatılabilir.

## 0. Konum & bağımlılık
FU04A (3 tenant event PlatformSeed Active) ve FU04B (EventCode Dispatch Adapter) **completed**. FU04C, **tenant producer'ları** mevcut `templateKey` tabanlı `QueueEmailNotificationCommand` çağrısından, FU04B adapter'ı üzerinden **eventCode** tabanlı dispatch'e taşır. Bu, **producer davranışını değiştiren** bir migration'dır → closeout'ta regresyon + smoke kritik.

## 1. Module Summary
- **Purpose:** 3 tenant notification producer akışını eventCode'a taşımak:
  - `AdminUserInvitationService` → `tenant.user.invited`
  - `TenantLifecycleNotificationConsumer` (suspended) → `tenant.lifecycle.suspended`
  - `TenantLifecycleNotificationConsumer` (reactivated) → `tenant.lifecycle.reactivated`
- **Nasıl:** Producer'lar artık `QueueEmailNotificationCommand(templateKey)` yerine **`DispatchNotificationByEventCodeCommand(eventCode)`** (FU04B) gönderir; FU04B adapter eventCode'u çözüp mevcut `QueueEmailNotificationCommand`'a delege eder.
- **Scope note:** Yalnızca bu 3 tenant producer. FU04B adapter/QueueEmailNotificationCommand/handler/template/seed **değişmez**. Workflow/document/import producer'ları **DAHİL DEĞİL** (FU04D / FU04M).

### 1.1 🔴 HEADLINE RISK — Variable alignment (BAĞLAYICI, ready-for-dev'i etkiler)
FU04B adapter, event'in `RequiredVariables`'ını **dispatch ÖNCESİ doğrular** (eksikse 422, dispatch olmaz). Mevcut producer'ların sağladığı değişkenler event sözleşmesiyle **her yerde uyumlu değil**:

| EventCode | Event RequiredVariables (FU04A) | Producer BUGÜN sağlıyor | Uyum |
|---|---|---|---|
| `tenant.user.invited` | **TenantDisplayName** | RecipientName, **TenantDisplayName**, Email, TemporaryPassword, LoginUrl | ✅ **Uyumlu** (invite hazır) |
| `tenant.lifecycle.suspended` | **TenantDisplayName**, Reason, SuspendedAtUtc | **TenantId**, Reason, SuspendedAtUtc | ❌ **TenantDisplayName EKSİK** → adapter 422 |
| `tenant.lifecycle.reactivated` | **TenantDisplayName**, ReactivatedAtUtc | **TenantId**, ReactivatedAtUtc (mapper deseni) | ❌ **TenantDisplayName EKSİK** → adapter 422 |

**Sonuç (BAĞLAYICI):** Suspended/reactivated migration'ı, mapper/consumer'ın **`TenantDisplayName`'i sağlaması** ile birlikte yapılmalıdır (consumer `tenant` objesine sahip: `tenant.DisplayName ?? tenant.Name`). Aksi halde her lifecycle bildirimi 422 alır ve (consumer throw davranışı nedeniyle) **retry loop**'a girer. Bu, migration'ın **asıl işi**dir — "sadece templateKey→eventCode swap" değildir.

> Not: `tenant.suspended.email` template'i `{{Reason}}`/`{{SuspendedAtUtc}}` kullanır; `TenantDisplayName`'i **render'da kullanmaz** ama event **required** ettiği için producer yine de sağlamalıdır (fazladan değişken pass-through; §FU04B adapter davranışı). Template **değiştirilmez** (§scope OUT).

## 2. Ownership and Boundaries
### In-scope
1. `AdminUserInvitationService`: `QueueEmailNotificationCommand("tenant.invite.email")` → `DispatchNotificationByEventCodeCommand("tenant.user.invited")`. Değişkenler zaten uyumlu; fail-soft davranış korunur.
2. `TenantLifecycleNotificationConsumer` **suspended** dalı → `tenant.lifecycle.suspended` eventCode; **TenantDisplayName** eklenir.
3. `TenantLifecycleNotificationConsumer` **reactivated** dalı → `tenant.lifecycle.reactivated` eventCode; **TenantDisplayName** eklenir.
4. Mapper'lar (`TenantSuspendedV1NotificationMapper`, `TenantReactivatedV1NotificationMapper`): variable seti eventCode sözleşmesine hizalanır (TenantDisplayName eklenir; eventCode dispatch request'i üretir veya consumer üretir — §3 KARAR 1).
5. Recipients mapping **korunur** (ResolveInitialAdminRecipient / ResolveTenantAdminRecipients davranışı aynı).
6. Locale / correlationId / causationId metadata **korunur** (mevcut değerler adapter request'ine taşınır).
7. Tests + regression.
8. Controlled failure davranışı kararı (§3 KARAR 2).

### Out-of-scope
- **FU04B adapter / `QueueEmailNotificationCommand` / handler değişikliği** (§1.1 yasak).
- **`NotificationEventSeedCatalog` / FU04A event tanımları / template / `NotificationTemplateSeed` değişikliği.**
- **`TenantCreatedV1NotificationMapper` / created dalı** — FU04A'da eşleşen `tenant.created` eventCode **YOK** → migration DIŞI (templateKey davranışı korunur).
- Workflow/document/import producer migration (FU04D), manifest event opt-in (FU04M).
- InApp/bell/SignalR/SMS/WhatsApp, Module Catalog/ModulePages/PlatformNavigationCatalog/Gateway.

### Ownership rule
FU04C, **tenant producer'ların dispatch çağrısını** eventCode'a taşır. Event/template/adapter/pipeline **üretmez/değiştirmez**; yalnızca çağrı yüzeyini (`templateKey` → `eventCode` via FU04B) değiştirir + değişken hizasını sağlar.

## 3. Kararlar

### KARAR 1 — Producer nasıl çağırsın? (ÖNERİ)
| Seçenek | Durum |
|---|---|
| Producer `INotificationEventDispatchAdapter`'ı doğrudan inject eder | Uygun ama yeni ctor bağımlılığı |
| **Producer `DispatchNotificationByEventCodeCommand`'ı `IMediator.Send` ile gönderir (ÖNERİ/KABUL)** | **ACCEPTED** — her iki producer **zaten `IMediator` inject ediyor**; mevcut MediatR desenine birebir uyar, yeni bağımlılık yok, en küçük değişiklik. |

**Kabul:** `IMediator.Send(new DispatchNotificationByEventCodeCommand(new NotificationEventDispatchRequest(TenantId, eventCode, To, Variables, Locale, Cc, Bcc, CorrelationId, CausationId)))`.

### KARAR 2 — Adapter failure business transaction'ı etkilemeli mi? (RESOLVED — owner review 2026-07-09)
- **Business state DEĞİŞMEZ:** invite provisioning + tenant lifecycle state change **notification'dan ÖNCE ve BAĞIMSIZ** commit edilir; bildirim başarısızlığı **business transaction'ı rollback ETMEZ**.
- **Invite (`AdminUserInvitationService`):** mevcut davranış **zaten fail-soft** (try/catch → log warning → `InvitationEmailSent=false`). **Migration bu davranışı KORUR** — adapter dönüşü `!IsSuccessful` ise yine catch/log, invite başarılı kalır.
- **Lifecycle consumer — ReasonCode-based ayrım (BAĞLAYICI):** `QueueIfMappedAsync` (veya eşdeğeri) adapter dönüşünün `Response.ReasonCode`'una göre ayrım yapar:
  - **Controlled catalog/validation 4xx** — `EVENT_NOT_FOUND` (404) / `EVENT_NOT_ACTIVE` (409) / `REQUIRED_VARIABLE_MISSING` (422) / `TEMPLATE_KEY_MISSING_OR_INVALID` (422): **non-retryable → log (reasonCode dahil) + swallow** (config/catalog hatası retry ile düzelmez; sonsuz retry loop önlenir).
  - **Provider/transient failure** — handler'dan gelen provider reddi (400, FU04B adapter reasonCode **YOK**): **mevcut throw/retry davranışı KORUNUR**.
  - Ayrım uygulanabilir: FU04B adapter controlled failure'da yukarıdaki reasonCode'ları set eder; FU02 handler provider-failure'da reasonCode set etmez → consumer bu ayrımı `Response.ReasonCode` ile yapar.
- **Rollback YASAK:** hiçbir producer, bildirim başarısızlığı nedeniyle tenant/invite state'ini geri almaz.

### KARAR A — TenantDisplayName geçiş yöntemi (RESOLVED — owner review 2026-07-09)
- **Consumer, `tenant.DisplayName ?? tenant.Name`'i variable set'ine ekler; MAPPER İMZASI GENİŞLETİLMEZ.**
- Gerekçe: `TenantSuspendedV1`/`TenantReactivatedV1` **payload'ları display adı taşımıyor** (yalnızca `TenantId`); display adı **consumer'ın yüklediği `tenant` objesindedir**. Mapper `Map(envelope, recipients, locale)` imzasında tenant yok.
- Uygulama: consumer, mapper'ın ürettiği variable dict'ine `TenantDisplayName` ekler (veya eventCode dispatch request'ini kurarken ekler). Mapper `TenantId` gibi pass-through değişkenleri **bırakabilir** (event required etmez, template kullanmaz — zararsız).
- **Edge case:** `DisplayName` ve `Name` ikisi de boşsa 422 riski → implementation'da güvenli fallback (ör. `tenant.Code`) değerlendirilir.

### KARAR 3 — Synchronous / asynchronous davranış
- Invite: **inline** (invite akışı içinde `await`), fail-soft. Korunur.
- Lifecycle: **event consumer** içinde (zaten asenkron, state change'den sonra). Korunur.
- Migration dispatch'i **mevcut senkronluk modelini değiştirmez** — yalnızca gönderilen command tipi değişir.

## 4. Değişme ihtimali olan dosyalar (implementation'da)
| Dosya | Değişiklik |
|---|---|
| `Infrastructure/Services/AdminUserInvitationService.cs` | `QueueEmailNotificationCommand("tenant.invite.email")` → `DispatchNotificationByEventCodeCommand("tenant.user.invited")`; değişkenler zaten uyumlu; try/catch fail-soft korunur |
| `Infrastructure/Eventing/TenantLifecycleNotificationConsumer.cs` | suspended + reactivated dalları eventCode dispatch'e taşınır; **TenantDisplayName** eklenir (`tenant.DisplayName ?? tenant.Name`); created dalı **değişmez**; failure davranışı §3 KARAR 2 |
| `Application/Features/Tenants/Notifications/TenantSuspendedV1NotificationMapper.cs` | variable seti eventCode sözleşmesine hizalanır (**TenantDisplayName** eklenir; TenantId opsiyonel/pass-through); eventCode request üretimi (veya consumer üretir) |
| `Application/Features/Tenants/Notifications/TenantReactivatedV1NotificationMapper.cs` | aynı (**TenantDisplayName** eklenir) |
| **Değişmeyecek:** `TenantCreatedV1NotificationMapper.cs`, `NotificationEventDispatchAdapter.cs` (FU04B), `QueueEmailNotificationCommand`/handler, `NotificationEventSeeding.cs` (FU04A catalog), `NotificationTemplateSeed.cs`, template'ler | |

> **Mapper imza notu:** Mevcut mapper `Map(envelope, recipients, locale)` — `tenant` objesine sahip değil (yalnızca envelope). `TenantDisplayName` için tenant display adı mapper'a **geçirilmeli** (imza genişletme) veya consumer variable'ı ekleyip dispatch request'i kendisi kurmalı. İkincisi (consumer kurar) daha az yüzey değiştirir — implementation kararı (§17 TBD).

## 5. Validation / Runtime Constraints
- eventCode dispatch, FU04B adapter validation'ına tabidir: event Active değil → 409; RequiredVariables eksik → 422; template yok → FU02 handler 404; recipient yok → 400. Bunlar **controlled** döner.
- Recipients / locale / correlation / causation **mevcut değerlerle** taşınır.
- Secret (`TemporaryPassword`) invite'ta değişken olarak taşınır; **masking mevcut FU02 handler'da** korunur (adapter/producer maskeyi değiştirmez).

## 6. Protected Paths (ihlali FAIL)
FU04B adapter, `QueueEmailNotificationCommand`/handler, `NotificationEventSeedCatalog`/FU04A event'leri, `NotificationTemplateSeed`/template'ler, `TenantCreatedV1NotificationMapper`, Module Catalog/ModulePages/PlatformNavigationCatalog/Gateway, IModuleManifestProvider, InApp/bell/SignalR/SMS/WhatsApp.

## 7. Acceptance Criteria
- [ ] `AdminUserInvitationService` artık `tenant.user.invited` eventCode ile dispatch eder (`DispatchNotificationByEventCodeCommand`); doğrudan `QueueEmailNotificationCommand` **çağırmaz**.
- [ ] Suspended akışı `tenant.lifecycle.suspended`; reactivated akışı `tenant.lifecycle.reactivated` eventCode ile dispatch eder.
- [ ] **RequiredVariables eksiksiz** map edilir — özellikle suspended/reactivated'a **TenantDisplayName** eklenir (adapter 422 vermez).
- [ ] Recipients + locale + correlation + causation **korunur**.
- [ ] Adapter failure'da business state **rollback edilmez**; invite fail-soft; lifecycle §3 KARAR 2 davranışı.
- [ ] **FU04B adapter / `QueueEmailNotificationCommand` / handler / FU04A seed / template DEĞİŞMEZ** (git diff kanıtı).
- [ ] `TenantCreatedV1NotificationMapper` / created dalı **değişmez**.
- [ ] Module Catalog / ModulePages / PlatformNavigationCatalog / Gateway side-effect **yok**; yeni template **yok**.
- [ ] `dotnet build Platform.API` 0 hata; FU02/FU03/FU03A/FU04A/FU04B regresyonsuz.

## 8. Test Plan
- **AdminUserInvitationService** (fake IMediator): invite akışı **`DispatchNotificationByEventCodeCommand`**'ı `EventCode="tenant.user.invited"` + Variables `TenantDisplayName` içeriyor + doğru recipient ile gönderir; `QueueEmailNotificationCommand` **doğrudan gönderilmez**.
- **Suspended** (consumer/mapper): `tenant.lifecycle.suspended` eventCode; Variables **TenantDisplayName, Reason, SuspendedAtUtc** içerir.
- **Reactivated**: `tenant.lifecycle.reactivated` eventCode; Variables **TenantDisplayName, ReactivatedAtUtc** içerir.
- **Failure davranışı:** adapter `!IsSuccessful` → invite fail-soft (invite başarılı, `InvitationEmailSent=false`); lifecycle §3 KARAR 2 (throw/retry veya controlled-swallow — seçilen davranış test edilir).
- **Regresyon:** mevcut `QueueEmailNotificationHandler` testleri + FU04B adapter testleri (13) + FU04A/FU03A/FU03/FU02 (**1166**) yeşil.
- **Created dalı** dokunulmadığı doğrulanır (hâlâ templateKey).
- `dotnet build Platform.API` 0 hata.
- **Closeout smoke (kritik — davranış değişikliği):** canlı fleet'te invite + suspend + reactivate akışlarının dispatch ürettiği (dispatch record / log / Mongo) doğrulanır; authenticated değilse log/Mongo ile telafi.

## 9. Failure / Risk notları
- **RequiredVariables eksik map edilirse → adapter 422** (özellikle TenantDisplayName — §1.1 headline risk).
- **Event inactive olursa → 409**, dispatch yok.
- **Template bulunamazsa → FU02 handler 404** (platform-default fallback mevcut; §FU04B).
- **Lifecycle consumer throw/retry:** controlled 4xx'te sonsuz retry riski → §3 KARAR 2 (reasonCode log + non-retryable ayrımı).
- **Producer davranış değişikliği** → closeout'ta **regresyon + canlı smoke ZORUNLU**.

## 10. Ready-for-dev Checklist / Open Blockers
### Çözülen (RESOLVED)
- [x] Bağımlılıklar FU04A + FU04B **completed**.
- [x] **DCP-002 preflight + registry reservation (RESOLVED — 2026-07-09):** `MOD-0027-FU04B-Tenant` DCP-reddedildi; kanonik **FU04C** preflight OK + registry satırı EKLENDİ.
- [x] **Owner review PASS (RESOLVED — 2026-07-09):** mimari/precision blocker yok (architecture-ba-reviewer).
- [x] KARAR 1 (DispatchNotificationByEventCodeCommand via IMediator), KARAR 3 (senkronluk korunur).
- [x] **KARAR A (RESOLVED):** TenantDisplayName **consumer**'dan eklenir (`tenant.DisplayName ?? tenant.Name`); mapper imzası genişletilmez (§3).
- [x] **KARAR B (RESOLVED):** lifecycle **ReasonCode-based** — controlled 4xx (EVENT_NOT_FOUND/EVENT_NOT_ACTIVE/REQUIRED_VARIABLE_MISSING/TEMPLATE_KEY_MISSING) non-retryable log+swallow; provider/transient throw/retry korunur; invite fail-soft; no rollback (§3).
- [x] Değişecek dosyalar + variable-alignment riski tespit edildi (§1.1, §4).

### Açık governance adımları
- **Yok.** DCP + registry reservation + owner review + Karar A + Karar B geçildi. FU04C **implementasyona hazır**.

### Implementation notları (blocker değil)
- Mapper `TenantDisplayName` için imza değiştirilmez; consumer variable'ı ekler (§3 KARAR A).
- `DisplayName`/`Name` boşsa güvenli fallback (ör. `tenant.Code`) — §3 KARAR A edge case.
- Teslim kapısı: FU02/FU03/FU03A/FU04A/FU04B (**1166+**) regresyon + **canlı smoke** (davranış değişikliği).

### Follow-up
- [ ] **FU04D:** workflow/document/import producer runtime wiring.
- [ ] **FU04M:** manifest-driven workflow/document/import event opt-in.

- [x] Status **`ready-for-dev`** (2026-07-09) — DCP + registry reservation + owner review PASS + Karar A/B RESOLVED; implementasyon başlatılabilir.

## 11. Output Contract
Implementation report: status; changed files (yalnızca 3 producer + 2 mapper hizalaması — **FU04B adapter / QueueEmailNotificationCommand / seed / template DEĞİŞMEZ**); DCP preflight (FU04C) + registry; 3 producer eventCode dispatch kanıtı; **TenantDisplayName dahil RequiredVariables hizası kanıtı**; failure/no-rollback kanıtı; created dalı değişmediği; regresyon (1166+) + canlı smoke; protected paths ihlali yok; next step (FU04D/FU04M).

---

## Özet karar tablosu
| Karar | Sonuç |
|---|---|
| Kimlik | `MOD-0027-FU04B-Tenant` **BLOCKED** → kanonik **`MOD-0027-FU04C`** (preflight OK) |
| Producer çağrısı | `DispatchNotificationByEventCodeCommand` via `IMediator` (mevcut desen) |
| Headline risk | **Variable alignment** — suspended/reactivated'a **TenantDisplayName** eklenmeli (yoksa 422) |
| Failure davranışı | Business rollback YOK; invite fail-soft (korunur); lifecycle throw/retry + reasonCode log (KARAR 2) |
| Created dalı | Kapsam DIŞI (eşleşen eventCode yok) |
| Değişmez | FU04B adapter, QueueEmailNotificationCommand/handler, FU04A seed, template'ler |
| Follow-up | FU04D (producer runtime wiring), FU04M (manifest opt-in) |
| Status | **ready-for-dev** — DCP + registry + owner review PASS + Karar A/B RESOLVED; teslim kapısı 1166+ regresyon + canlı smoke |
