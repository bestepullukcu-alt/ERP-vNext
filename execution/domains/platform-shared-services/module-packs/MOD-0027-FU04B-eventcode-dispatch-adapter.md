---
id: MOD-0027-FU04B
name: EventCode Dispatch Adapter
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: custom-integration
entity_base: none
status: completed
parent: MOD-0027
depends_on:
  - MOD-0027-FU03
  - MOD-0027-FU03A
  - MOD-0027-FU04A
owner: ali.tufanoglu
branch: feature/pss/mod-0027-fu04b-eventcode-dispatch-adapter
started: 2026-07-08
target: TBD
form_field_count: 0
---

# MOD-0027-FU04B - EventCode Dispatch Adapter

> **Identity (DCP-002 — GATE PASSED, 2026-07-08):** `MOD-0027-FU04B`, Blueprint `MOD-0027` (Notification Service) parent'ının kanonik bir FU'sudur. Preflight **PASS**:
> `py .antigravity/scripts/verify_module_id.py . --check-id MOD-0027-FU04B --name "EventCode Dispatch Adapter" --parent MOD-0027` → `OK` (exit 0).
> **Registry satırı EKLENDİ** (`module-id-registry.md`: `MOD-0027-FU04B | Follow-up | reserved | parent MOD-0027`). **DCP-002 kimlik kapısı GEÇİLDİ.** Owner review (architecture-ba-reviewer) **PASS** — mimari/precision blocker yok (2026-07-08). **Status: `ready-for-dev`** — implementasyon başlatılabilir. Tek teslim kapısı: FU02/FU03/FU03A/FU04A (1153) regresyon (§16).

## 0. Konum & bağımlılık
FU02 (template/settings/dispatch foundation), FU03 (event catalog + template binding), FU03A (SourceType/PlatformSeed foundation) ve FU04A (3 tenant event Active PlatformSeed) **completed**. Katalog artık her Active event için `EventCode → DefaultTemplateKey → RequiredVariables` sözleşmesini tutuyor; ama **producer'lar hâlâ doğrudan `templateKey` ile** `QueueEmailNotificationCommand` çağırıyor. FU04B, producer'ların **eventCode** vererek dispatch başlatabilmesi için **ortak adapter/resolver** katmanını tasarlar.

## 1. Module Summary
- **Purpose:** `eventCode` alan, Active `NotificationEventDefinition`'ı çözen, `DefaultTemplateKey`'i türeten, `RequiredVariables`'ı doğrulayan ve **mevcut** `QueueEmailNotificationCommand` dispatch pipeline'ını çağıran **generic, tek** bir adapter sağlamak. Dispatch sonucu **mevcut MOD-0027 tracking/failure modeliyle** (`Response<NotificationDispatchDto>`, Queued/Sent/Failed) döner.
- **Kritik ayrım — sadece adapter:** FU04B **yalnızca ortak adapter'ı** sağlar. Producer flow'ların gerçekten eventCode ile çağırması **AYRI follow-up**'tır:
  - **FU04B-Tenant** — tenant producer migration (`AdminUserInvitationService`, `TenantLifecycleNotificationConsumer`, `TenantCreatedV1/SuspendedV1/ReactivatedV1` mapper'ları).
  - **FU04D** — workflow/document/import producer runtime wiring + manifest event opt-in.
- **Neden şimdi:** FU04A ile 3 tenant event Active/PlatformSeed olarak katalogta yaşıyor ve mevcut template'lere bağlı; adapter'ın **canlı proof**'u bu 3 event üzerinden yapılabilir (gerçek producer'a dokunmadan).
- **Runtime scope:** Adapter contract + resolver + validation + mevcut pipeline'a delegasyon + testler. **Yeni dispatch pipeline, yeni provider, yeni template YOK.**

### 1.1 Kesin yasaklar (bağlayıcı — ihlali FAIL)
- Tenant/workflow/document/import **producer flow migration YOK** (ayrı follow-up).
- `AdminUserInvitationService` / `TenantLifecycleNotificationConsumer` / `TenantCreatedV1NotificationMapper` **değiştirme YOK**.
- **Yeni template oluşturma YOK**; `NotificationTemplateSeed` değişmez.
- `QueueEmailNotificationCommand` / handler / dispatch pipeline **davranışı değiştirme YOK** (yalnızca çağrılır).
- Manifest event opt-in (Workflow/document/import) **YOK**.
- TenantShell bell / InApp `UserNotification` / SignalR / SMS / WhatsApp **YOK**.
- Module Catalog / ModulePages / PlatformNavigationCatalog / Gateway (`ocelot.json`) **değişmez**.
- IModuleManifestProvider / TenantManagementManifestProvider **YOK**.

## 2. Ownership and Boundaries
### In-scope
1. **EventCode dispatch adapter/resolver contract** (Application service + opsiyonel thin MediatR command).
2. `EventCode → Active NotificationEventDefinition` lookup (repository mevcut: `GetByEventCodeAsync`).
3. **Status validation** — yalnızca `Active` dispatch eder; `Draft`/`Deprecated`/`Archived` → controlled failure.
4. `DefaultTemplateKey` resolution (event'ten).
5. **RequiredVariables validation** — event'in required değişkenleri sağlanan variables içinde var mı (isim + boş-değil).
6. **OptionalVariables pass-through** (doğrulama zorunlu değil; olduğu gibi taşınır).
7. **Mevcut `QueueEmailNotificationCommand`** (veya dispatch pipeline) ile uyum — adapter onu çağırır, sonucu döndürür.
8. Controlled failure paths (§12).
9. Testler (§17).
10. **3 tenant event ile adapter proof** (`tenant.user.invited`, `tenant.lifecycle.suspended`, `tenant.lifecycle.reactivated`) — producer'a dokunmadan, adapter'ın uçtan uca çalıştığının kanıtı.

### Out-of-scope
- Tenant/workflow/document/import producer migration (→ FU04B-Tenant, FU04D).
- Yeni template / template seed değişikliği.
- Yeni provider / yeni dispatch pipeline / yeni kanal (InApp/SMS/Push/SignalR).
- Module Catalog / ModulePages / PlatformNavigationCatalog / Gateway.
- Tenant self-service UI, event catalog UI değişikliği.

### Ownership rule
FU04B, **eventCode→dispatch köprüsünü** sahiplenir. Event tanımlarını (FU03/FU03A/FU04A) **üretmez**, template'leri (FU02) **üretmez**, dispatch pipeline'ı (FU02) **değiştirmez** — yalnızca çözer, doğrular ve mevcut komuta **delege eder**.

## 3. KARAR — Adapter tasarımı (ÖNERİ, bağlayıcı olacak)
**Kabul edilen yaklaşım: Application-layer resolver service + thin MediatR command (ikisi de mevcut `QueueEmailNotificationCommand`'a delege eder).**

| Seçenek | Durum |
|---|---|
| **A — Yeni bağımsız dispatch pipeline (eventCode-native)** | **REJECTED** — FU02 pipeline'ını duplike eder; tracking/failure/masking/provider mantığı ikiye böler. |
| **B — `QueueEmailNotificationCommand`'a `eventCode` alanı ekleyip handler'ı şişirmek** | **REJECTED** — mevcut handler davranışını değiştirir (§1.1 yasak); templateKey-only çağıranları riske atar. |
| **C — Ayrı adapter (resolver) + thin command; mevcut `QueueEmailNotificationCommand`'a delege (ÖNERİ/KABUL)** | **ACCEPTED** — event çözümleme + validation adapter'da; gerçek dispatch mevcut komutta; tek pipeline, tek tracking modeli. |

**Kabul edilen (C):** Adapter, eventCode'u çözüp `RequiredVariables`'ı doğruladıktan sonra `DefaultTemplateKey` + sağlanan recipients/variables/locale ile **mevcut** `QueueEmailNotificationCommand`'ı (IMediator üzerinden) çağırır ve dönen `Response<NotificationDispatchDto>`'yu aynen döndürür. Böylece masking, dispatch record, event publish, provider, retry — **hepsi FU02'de kaldığı gibi** çalışır.

## 4. Command / Service önerisi
### 4.1 Adapter service (Application)
```
public interface INotificationEventDispatchAdapter
{
    // Resolves eventCode -> Active event -> defaultTemplateKey, validates required variables,
    // then delegates to the existing QueueEmailNotificationCommand. Returns the SAME dispatch result.
    Task<Response<NotificationDispatchDto>> DispatchByEventCodeAsync(
        NotificationEventDispatchRequest request, CancellationToken ct = default);
}
```
Konum: `Diten.Platform.Application/Features/Notifications/Services/NotificationEventDispatchAdapter.cs`. Bağımlılıklar: `INotificationEventDefinitionRepository` (event lookup), `IMediator` (mevcut komuta delege). **Yeni repository/entity YOK.**

### 4.2 Request sözleşmesi (öneri)
```
public sealed record NotificationEventDispatchRequest(
    Guid TenantId,
    string EventCode,
    IReadOnlyList<EmailRecipientDto> To,
    IReadOnlyDictionary<string, object?> Variables,
    string? Locale = null,
    IReadOnlyList<EmailRecipientDto>? Cc = null,
    IReadOnlyList<EmailRecipientDto>? Bcc = null,
    string? CorrelationId = null,
    string? CausationId = null);
```
- Recipients **producer tarafından sağlanır** (event catalog recipient tutmaz).
- Variables producer tarafından sağlanır; adapter yalnızca **RequiredVariables mevcudiyetini** doğrular (değer üretmez).

### 4.3 Thin MediatR command (opsiyonel, önerilir)
```
public sealed record DispatchNotificationByEventCodeCommand(NotificationEventDispatchRequest Request)
    : IRequest<Response<NotificationDispatchDto>>;
```
Handler yalnızca `INotificationEventDispatchAdapter`'ı çağırır. Producer'lar ister service'i inject eder, ister command'i send eder. (Not: bu command **FU04B kapsamında yalnızca tanımlanır**; producer'ların onu çağırması FU04B-Tenant/FU04D'dir.)

### 4.4 Dönüş
`Response<NotificationDispatchDto>` — **mevcut MOD-0027 tracking/failure modeli** (Queued/Sent/Failed, dispatchId, correlationId). Yeni DTO/tracking modeli **eklenmez**.

## 5. Adapter akışı (bağlayıcı)
1. Producer `eventCode` (+ recipients + variables + tenant) ile adapter'ı çağırır.
2. Adapter `GetByEventCodeAsync(eventCode)` ile `NotificationEventDefinition`'ı bulur.
3. Event **yoksa** → controlled failure (404).
4. Event **Active değilse** (Draft/Deprecated/Archived) → controlled failure (409).
5. `DefaultTemplateKey` **boş/invalid** ise → controlled failure (422).
6. `RequiredVariables` **eksikse** (isim yok veya boş değer) → controlled failure (422, eksik değişken adlarıyla).
7. Recipients **yoksa** (To boş) → controlled failure (400).
8. Adapter, `DefaultTemplateKey` + Locale + To/Cc/Bcc + Variables ile **`QueueEmailNotificationCommand`**'ı IMediator ile gönderir.
9. Mevcut handler: template lookup (Active değilse 404), render, dispatch record, provider, event publish, masking — **değişmeden** çalışır.
10. Dönen `Response<NotificationDispatchDto>` (Sent/Failed) **aynen** döndürülür. (Template runtime'da bulunamazsa mevcut handler'ın 404'ü taşınır — §12.)

> **Template resolution fallback (owner review — DOĞRULANDI, 2026-07-08):** Template mevcudiyeti tek otorite olarak FU02 `NotificationTemplateRepository.GetBestActiveByKeyAsync`'e bırakılır. Bu metod **tenant-specific → platform-default → neutral-locale** sırasıyla fallback yapar (kod: `GetActiveByKeyAsync(tenantId,false)` → `GetActiveByKeyAsync(null,true)` → neutral-locale). Bu nedenle FU04A'nın **platform-default** template'lere (`tenant.invite/suspended/reactivated.email`, `TenantId=null`, `IsPlatformDefault`) bağlı 3 tenant event'i, **gerçek bir TenantId ile dispatch edildiğinde çözülür** — adapter'ın çift-kontrol yapmasına gerek yoktur.

## 6. Repo Scope (öneri — implementation'da)
- `execution/domains/.../MOD-0027-FU04B-...md`
- **Yeni (adapter):**
  - `Features/Notifications/Services/INotificationEventDispatchAdapter.cs` + `NotificationEventDispatchAdapter.cs`.
  - `NotificationEventDispatchRequest` record (+ opsiyonel `DispatchNotificationByEventCodeCommand` + handler).
  - DI kaydı (`AddApplication` içinde `INotificationEventDispatchAdapter` scoped).
  - Testler.
- **Değiştirilmeyecek (Protected Paths — §1.1):** `QueueEmailNotificationCommand`/handler, `NotificationTemplateSeed`, `NotificationEventSeedCatalog` (FU04A içeriği), tenant producer/mapper/consumer, Module Catalog/ModulePages/PlatformNavigationCatalog/Gateway, event catalog entity/enum/sync (FU03/FU03A).

## 7. Runtime Constraints
- Adapter **global/platform** (tenant-agnostik lookup; event kayıtları platform-global). Dispatch mevcut komutta `TenantId` ile tenant-scope'lanır.
- Adapter **idempotent değildir** (her çağrı bir dispatch denemesidir) — bu mevcut `QueueEmailNotificationCommand` davranışıyla aynı; FU04B yeni idempotency getirmez.
- Secret/masking: adapter **variables'ı değiştirmez**; masking mevcut handler'da (`SanitizeVariables`/`MaskSensitiveValues`) kalır.
- Adapter **producer'ı ÇAĞIRMAZ**; producer adapter'ı çağırır (bağımlılık yönü). FU04B'de hiçbir producer adapter'ı çağırmaz (proof testleri hariç).

## 8. Validation Rules
- `EventCode`: canonical/lowercase dotted (mevcut `NotificationParsing.IsValidTemplateKey` ile normalize/valide).
- Event: repository'de var + `Status == Active`.
- `DefaultTemplateKey`: boş değil + format geçerli.
- `RequiredVariables`: event'in her required değişkeni `Variables` içinde mevcut ve boş-değil.
- `To`: en az 1 recipient.
- OptionalVariables: doğrulanmaz (pass-through).
- Template mevcudiyeti: adapter **önden kontrol etmez**; mevcut handler'ın template lookup'ı otoritedir (404 taşınır) — çift-kontrol yerine tek otorite (FU02 handler).

## 9. Failure Path Table (controlled — hepsi `Response<T>.Fail`, exception yok)
| # | Koşul | Sonuç (öneri) |
|---|---|---|
| 1 | EventCode invalid format | `Fail("Invalid event code.", 400)` |
| 2 | Event bulunamadı | `Fail("Notification event not found.", 404)` |
| 3 | Event `Draft` | `Fail("Event is not active (Draft).", 409)` |
| 4 | Event `Deprecated`/`Archived` | `Fail("Event is not active ({status}).", 409)` |
| 5 | `DefaultTemplateKey` boş/invalid | `Fail("Event has no valid default template key.", 422)` |
| 6 | RequiredVariables eksik | `Fail("Missing required variables: {names}.", 422)` |
| 7 | Recipients yok (To boş) | `Fail("At least one recipient is required.", 400)` |
| 8 | Template runtime'da yok (mevcut handler) | Mevcut `Fail("Notification template not found.", 404)` taşınır |
| 9 | Provider reddetti (mevcut handler) | Mevcut `Fail("Messaging provider rejected the message.", 400)` + dispatch `Failed` |
| 10 | Provider kabul etti | Mevcut `Success(dispatchDto, 201)` — dispatch `Sent` |

## 10. Failure Paths to Verify
- Bilinmeyen eventCode → 404, dispatch **oluşmaz**.
- Draft/Deprecated/Archived event → 409, dispatch **oluşmaz**.
- RequiredVariables eksik → 422 (eksik adlar), dispatch **oluşmaz**.
- Boş recipient → 400, dispatch **oluşmaz**.
- Geçerli akış → mevcut `QueueEmailNotificationCommand` çağrılır, `NotificationDispatchDto` döner.
- Secret (ör. `TemporaryPassword`) variables'ta → mevcut masking dispatch record'a sızmasını önler (adapter maskeyi değiştirmez).

## 11. Layout & Shell Contract
- **shell: none** — UI YOK. `/Platform/NotificationEvents` (FU03) değişmez.

## 12. Backend File Convention
- `Features/Notifications/Services/INotificationEventDispatchAdapter.cs` (interface).
- `Features/Notifications/Services/NotificationEventDispatchAdapter.cs` (sealed class).
- (Opsiyonel) `Features/Notifications/Commands/DispatchNotificationByEventCodeCommand.cs` + `Handlers/CommandHandlers/DispatchNotificationByEventCodeHandler.cs` (Command/Query suffix YOK — handler adı `DispatchNotificationByEventCodeHandler`).
- `Features/Notifications/NotificationEventDispatchContracts.cs` (request/DTO record'ları — mevcut `EmailRecipientDto` yeniden kullanılır).

## 13. Authorization Convention
- Adapter **internal application service** — kendi HTTP endpoint'i **yok** (FU04B'de). Producer'lar in-process çağırır. Yeni permission/policy **üretilmez**. (İleride bir admin "test dispatch by eventCode" endpoint'i istenirse ayrı follow-up + permission gerekir.)

## 14. Gateway / API Routing Decision
- **Gateway değişikliği YOK** — yeni HTTP endpoint yok; adapter in-process. integration-agent task'ı **gerekmez**.

## 15. Acceptance Criteria
- [ ] `INotificationEventDispatchAdapter` + impl eklendi; DI'da scoped kayıtlı.
- [ ] `eventCode → Active event → defaultTemplateKey` çözümlemesi çalışır; **yeni dispatch pipeline üretilmez** (mevcut `QueueEmailNotificationCommand` çağrılır).
- [ ] Status validation: yalnızca Active dispatch; Draft/Deprecated/Archived → controlled failure (dispatch yok).
- [ ] RequiredVariables validation: eksikse controlled failure (eksik adlarla); dispatch yok.
- [ ] OptionalVariables pass-through.
- [ ] **3 tenant event proof:** `tenant.user.invited`/`tenant.lifecycle.suspended`/`tenant.lifecycle.reactivated` için adapter, doğru templateKey'e (`tenant.invite/suspended/reactivated.email`) çözer ve dispatch komutunu çağırır (fake/mock mediator ile assert).
- [ ] **Mevcut `QueueEmailNotificationCommand`/handler DEĞİŞMEDİ** (git diff kanıtı).
- [ ] **Producer flow'lar DEĞİŞMEDİ** (AdminUserInvitationService/TenantLifecycle*/mapper'lar) — FU04B-Tenant/FU04D'ye bırakıldı.
- [ ] Yeni template/Module Catalog/PlatformNavigationCatalog/Gateway side-effect **yok**.
- [ ] `dotnet build Platform.API` 0 hata; FU02/FU03/FU03A/FU04A regresyonsuz.

## 16. Test Expectations
- `dotnet build Platform.API` (fleet kilidi → temp-output).
- **Adapter unit (fake IMediator + in-memory event repo):**
  - Bilinmeyen eventCode → 404, mediator **çağrılmaz**.
  - Draft/Deprecated/Archived event → 409, mediator **çağrılmaz**.
  - RequiredVariables eksik → 422 (eksik adlar), mediator **çağrılmaz**.
  - Boş To → 400, mediator **çağrılmaz**.
  - Geçerli akış → mediator'a **`QueueEmailNotificationCommand` doğru `TemplateKey` + variables + recipients ile** gönderilir; adapter dönen `Response<NotificationDispatchDto>`'yu aynen döndürür.
- **3 tenant event proof:** her biri için doğru `DefaultTemplateKey` çözülür (repo'da Active seed edilmiş event fixture'ıyla).
- **Regresyon:** FU02/FU03/FU03A/FU04A (1153) yeşil; `QueueEmailNotificationHandler` davranışı değişmez.
- (Opsiyonel) canlı smoke: 3 tenant event üzerinden adapter proof (authenticated değilse Mongo/log ile telafi).

> **Unit proof vs live smoke ayrımı (owner review Not #1):** Fake-`IMediator` + in-memory event repo ile yapılan **unit "proof"**, yalnızca `eventCode → Active event → DefaultTemplateKey` **çözümleme + delegasyonu** (doğru komut/argümanla `QueueEmailNotificationCommand` gönderildiği) kanıtlar. **Gerçek template render + provider gönderimi** unit seviyesinde kanıtlanmaz; bu, **opsiyonel canlı smoke** ile (3 tenant event, platform-default fallback üzerinden — §5) teyit edilir. Bu ayrım closeout'ta açıkça raporlanmalıdır.

## 17. Ready-for-dev Checklist / Open Blockers / TBD
### Çözülen (RESOLVED)
- [x] **DCP-002 preflight PASS** (2026-07-08): `MOD-0027-FU04B: proven against Blueprint/registry.` (exit 0).
- [x] Adapter tasarım kararı: **Seçenek C** (resolver + delege), mevcut pipeline korunur.
- [x] Bağımlılıklar (FU03/FU03A/FU04A) **completed**.

- [x] **Registry reservation (RESOLVED — 2026-07-08):** `module-id-registry.md`'ye `MOD-0027-FU04B | Follow-up | reserved | parent MOD-0027` satırı eklendi.
- [x] **Owner review (RESOLVED — 2026-07-08):** architecture-ba-reviewer **PASS**; mimari/precision blocker yok. 2 opsiyonel not eklendi (§5 template fallback, §16 unit/smoke ayrımı). Status `draft` → **`ready-for-dev`**.

### Durum (CLOSED)
- **İmplement + closed out 2026-07-08 (PASS-with-note).** `INotificationEventDispatchAdapter` + thin command/handler + DI; **13/0** adapter testi (tüm failure path + 3 tenant proof + passthrough), **suite 1166/0**, Platform.API 0 hata. `QueueEmailNotificationCommand`/handler + producer'lar değişmedi. Bkz. [FU04B smoke audit](../../../../docs/audits/pss-mod-0027-fu04b-eventcode-dispatch-adapter-smoke-2026-07-08.md). Note: unit proof yalnızca resolution+delegation; gerçek render/send FU02 handler'da. **Status: `completed`.**
- Açık governance/blocker: **YOK.**

### TBD (implementasyonda netleşir)
- [ ] `RequiredVariables` eksik-değer kontrolünün katılığı (null vs boş string vs whitespace).
- [ ] `DispatchNotificationByEventCodeCommand` (thin command) FU04B'de mi yoksa producer-migration pack'lerinde mi tanımlansın (öneri: FU04B'de tanımla, çağıran taraf ayrı).

### Follow-up (AYRI pack'ler — bağlayıcı ayrım)
- [ ] **FU04B-Tenant:** tenant producer migration (`AdminUserInvitationService` → `tenant.user.invited`; `TenantLifecycleNotificationConsumer`/`TenantSuspendedV1`/`TenantReactivatedV1` → `tenant.lifecycle.*`). Bu **producer davranışını değiştirir** → ayrı, dikkatli pack.
- [ ] **FU04D:** workflow/document/import producer runtime wiring + manifest event opt-in.

- [x] Status **`ready-for-dev`** (2026-07-08) — DCP + registry reservation + owner review PASS; implementasyon başlatılabilir.

## 18. Output Contract
Implementation report: status; changed files (yalnızca adapter service + opsiyonel thin command + DI + testler — **producer/pipeline/template DEĞİŞMEDİ**); DCP preflight + registry; eventCode→template çözümleme kanıtı; 3 tenant event proof; failure path kanıtı; `QueueEmailNotificationCommand` değişmediği kanıtı (git); producer migration'ın FU04B-Tenant/FU04D'ye bırakıldığı; regresyon kanıtı; next step.

---

## Özet karar tablosu
| Karar | Sonuç |
|---|---|
| Adapter tasarımı | **Seçenek C** — resolver service + thin command, mevcut `QueueEmailNotificationCommand`'a delege |
| Yeni pipeline/template/provider | **YOK** (FU02 pipeline korunur) |
| Producer migration | **AYRI follow-up** (FU04B-Tenant tenant producer; FU04D workflow/document/import) |
| Dönüş modeli | Mevcut `Response<NotificationDispatchDto>` (Queued/Sent/Failed) |
| 3 tenant event | Adapter proof kaynağı (producer'a dokunmadan) |
| UI / Gateway / Module Catalog / Nav | **Değişmez** |
| DCP-002 | Preflight PASS (exit 0) |
| Açık governance | **Yok** (DCP + registry reservation + owner review PASS RESOLVED) |
| Status | **ready-for-dev** — implementasyona hazır; teslim kapısı FU02/FU03/FU03A/FU04A (1153) regresyon |
