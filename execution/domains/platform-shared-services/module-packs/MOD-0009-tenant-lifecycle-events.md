---
id: MOD-0009
name: Tenant Registry Lifecycle Events
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: BaseEntity
status: review
owner: platform-shared-services
branch: feature/email-service
started: 2026-05-19
target: 2026-06-16
form_field_count: 0
---

# MOD-0009 — Tenant Registry Lifecycle Events

## 0. Pre-dev Blocker Decisions

Pack `ready-for-dev`'a alınmadan önce kullanıcı aşağıdaki kararları kabul etmelidir. Recommended kolonu Batch-split mantığına göre önerilen MVP varsayılanıdır.

| # | Decision | Recommended | User accepted? |
|---|---|---|---|
| D1 | **MOD-0035 live RabbitMQ validation** — broker-backed publish/consume proof bu pack'in **DONE** koşulu mu? | Evet — Batch 3 koşulu. Batch 1/2 PASS için live broker şart değil; in-memory + outbox testleri yeterli. | ✅ Accepted for MVP |
| D2 | **Batch 1 in-memory only başlatma izni** — live RabbitMQ kurulumu beklenmeden contract + producer emit + in-memory test'lerle başla. | Evet — Batch 1 in-memory + outbox fake ile PASS edilebilir. Live broker Batch 3'e devredilir. | ✅ Accepted for MVP |
| D3 | **MOD-0027 notification consumer readiness** — `INotificationEventMapper<TEvent>` seam'i ve `QueueEmailNotificationCommand` queue API'si bu branch'te hazır mı? | ✅ Hazır (branch `feature/email-service` üzerinde). Batch 2 doğrudan mapper register edebilir. | ✅ Accepted for MVP |
| D4 | **MOD-0263 production SMTP readiness** — `SmtpMessagingProvider` (MailKit) prod path'e yetiyor mu? SendGrid follow-up'a mı? | ✅ Hazır (SmtpMessagingProvider + FakeMessagingProvider + SMTP integration tests mevcut). SendGrid follow-up. | ✅ Accepted for MVP |
| D5 | **MOD-0021 audit consumer seam** — `IAuditService.AppendAsync(AuditAppendRequest)` public abstraction'ı tenant lifecycle audit için kullanılabilir mi? | ✅ Hazır. Batch 2 audit consumer doğrudan `IAuditService` çağırır; yeni audit entity/repo açılmaz. | ✅ Accepted for MVP |
| D6 | **Provisioning events (`TenantProvisioningCompletedV1` / `TenantProvisioningFailedV1`)** — Batch 1B'de mi (mevcut orchestration handler varsa) yoksa Batch 2'ye mi ertelenecek? | **Conditional:** Mevcut provisioning orchestration handler'ı tespit edilirse Batch 1B; aksi halde contract eklenir, producer emit Batch 2'ye ertelenir (provisioning orchestration **icat edilmez**). | ✅ Accepted for MVP |
| D7 | **Browser smoke** — MVP için Platform Admin UI üzerinden manual/Playwright smoke gerekli mi? | Hayır — backend/eventing modülü olduğu için API + eventing smoke yeterli. Browser smoke Wave 2 follow-up. | ✅ Accepted for MVP |
| D8 | **Notification template authorship** — `tenant.invite.email`, `tenant.suspended.email`, `tenant.reactivated.email` template'leri bu pack'te mi tanımlanacak yoksa MOD-0027 / ayrı seed pack'i mi? | MOD-0027 / ayrı seed pack'i. Bu pack template **tanımlamaz**; sadece template key string'i ile referans verir. Template eksikse consumer controlled-failure → retry/DLQ. | ✅ Accepted for MVP |
| D9 | **Tenant handler folder refactor** — Mevcut flat `Handlers/` yapısı Golden Reference `CommandHandlers/QueryHandlers/` ayrımına bu pack içinde refactor edilsin mi? | Hayır — sadece emit satırı eklenir. Folder refactor ayrı brownfield pack'i. | ✅ Accepted for MVP |

**Status promotion gate:** D1–D9 MVP için kabul edildi; pack `ready-for-dev`. Batch 1 implementation, aşağıdaki Phase 0 gate PASS olduktan sonra başlar.

### Phase 0 — Implementation Gate (Batch 1 öncesi zorunlu)

Batch 1 kodu yazılmadan önce implementer repo üzerinde aşağıdaki mevcut gerçekleri doğrular:

- `IEventBus.PublishAsync(...)` mevcut implementation'ı outbox-backed olmalı; doğrudan RabbitMQ/MassTransit publish yapıyorsa veya outbox insert yoksa **BLOCKED/PARTIAL** raporlanır, MOD-0035 tamamlanmadan infrastructure icat edilmez.
- `TenantActivatedV1` mevcut pattern'i (`Name`, `Version`, `IInternalEvent` projection, payload şekli) doğrulanır; pattern yoksa **BLOCKED/PARTIAL** raporlanır, event contract şablonu uydurulmaz.
- Tenant lifecycle handler path'leri (`RegisterTenantCommandHandler`, `SuspendTenantCommandHandler`, `ReactivateTenantCommandHandler`, `DeleteTenantCommandHandler`) mevcut dosya yollarıyla doğrulanır; handler yoksa ilgili producer maddesi **BLOCKED/PARTIAL** raporlanır.
- Mevcut consumer registration pattern'i (`MassTransit`/`IConsumer<EventTransportMessage>` veya branch'teki onaylı event consumer pattern'i) doğrulanır; yeni eventing mimarisi kurulmaz.
- Phase 0 çıktısı Implementation Notes'a kısa evidence listesi olarak yazılır. Eksiklerden biri varsa Batch 1 emit kodu yazılmaz; önce kullanıcıya `BLOCKED`/`PARTIAL` raporu verilir.

---

## 1. Module Summary

- **Purpose:** Tenant registry lifecycle değişikliklerini (created, activated, suspended, reactivated, cancelled, provisioning-completed, provisioning-failed) merkezi event bus (MOD-0035) üzerinden yayınlamak; downstream consumer'lar (notification mapper'ları, audit, entitlement refresh, subscription lifecycle) bu event'lere abone olarak reaktif çalışsın.
- **Wave / Priority:** W1-A / Blocker. FAZ A foundation kapanışının bir parçası ([docs/platform/execution-roadmap.md](../../../../docs/platform/execution-roadmap.md) Dev1 sırası 5).
- **Scope shape:** Backend / infrastructure-only modül. UI yok, public REST yok, gateway route yok.
- **Producer side (Batch 1):** Mevcut tenant lifecycle command handler'larına MOD-0035 public `IEventBus.PublishAsync(...)` (outbox-backed) çağrıları ekler. Doğrudan RabbitMQ/MassTransit API çağrısı yasaktır.
- **Consumer side (Batch 2):** MOD-0027'nin `INotificationEventMapper<TEvent>` seam'i için 3 concrete mapper (`TenantCreatedV1` → `tenant.invite.email`, `TenantSuspendedV1` → `tenant.suspended.email`, `TenantReactivatedV1` → `tenant.reactivated.email`) + MOD-0021 `IAuditService.AppendAsync(...)` çağıran 1 audit consumer. Tenant lifecycle handler'ları içinde `QueueEmailNotificationCommand` send'i veya `IAuditService` çağrısı yapılmaz; tetikleyici yol yalnızca event consume yoluyladır.
- **Existing seed:** `TenantActivatedV1` zaten `services/Diten.Platform.Contracts/Events/TenantActivatedV1.cs`'de tanımlı + `TenantActivatedV1Consumer` referans pattern. Bu pack onları **yeniden yazmaz**, kalan 6 event contract'ı aynı şablonla ekler.
- **Production prerequisite:** Mainline'a broker-backed emission alınması Batch 3'te live external/local RabbitMQ doğrulamasına bağlıdır ([docs/platform/master-plan.md](../../../../docs/platform/master-plan.md) satır 924).

## 2. Ownership and Boundaries

### In-scope
- 6 yeni Platform-owned tenant lifecycle event contract'ı (`Diten.Platform.Contracts/Events/`).
- Mevcut tenant lifecycle command handler'larında MOD-0035 public `IEventBus.PublishAsync(...)` çağrıları (outbox write).
- 3 `INotificationEventMapper<TenantXxxV1>` concrete implementation (`Diten.Platform.Application/Features/Tenants/Notifications/`).
- Mapper DI registration (mevcut Application/Infrastructure registration noktalarında).
- 1 audit consumer (7 event family için, `IAuditService.AppendAsync(...)` çağırır).
- Idempotent consumer pattern (MOD-0035 ConsumedEvent inbox üzerinden).
- Contract test'leri (EventName/EventVersion uyumu + payload anti-pattern).
- Handler emit assertion test'leri (in-memory bus + fake outbox repository).
- Batch 3'te live external/local RabbitMQ doğrulama testleri.

### Out-of-scope
- Eventing altyapısı (publish/consume mechanics, outbox worker, broker adapter) — MOD-0035 sahipliğinde.
- Notification template engine, template authorship, throttling, retry, SMTP — MOD-0027 + MOD-0263 sahipliğinde.
- Notification template tanımlama (`tenant.invite.email`, vb.) — D8 kararıyla bu pack dışı.
- Audit entity, repository, retention worker — MOD-0021 sahipliğinde (yalnızca public `IAuditService` consume edilir).
- Subscription lifecycle event'ları — MOD-0297.
- Entitlement refresh consumer business logic — MOD-0298.
- ProvisioningRetryJob iş mantığı — MOD-0026 hosting + MOD-0009 follow-up.
- Frontend UI, gateway route, public REST endpoint.
- Event Catalog / DLQ viewer UI — MOD-0035 follow-up.
- Handler folder refactor (`Handlers/CommandHandlers/QueryHandlers/` ayrımı) — ayrı brownfield pack'i.

### Ownership rule
- Platform-owned tenant lifecycle event contract'larını yalnızca PSS sahiplenir.
- Producer emit'i yalnızca MOD-0035 public `IEventBus` üzerinden outbox'a yapılır; RabbitMQ.Client/MassTransit doğrudan API çağrısı **yasak**.
- Notification trigger'ı yalnızca event mapper aracılığıyla MOD-0027 kuyruğuna gider; tenant lifecycle command handler içinden `QueueEmailNotificationCommand` send'i veya `INotificationEventMapper` çağrısı yapılmaz.
- Audit trigger'ı yalnızca event consumer aracılığıyla `IAuditService.AppendAsync(...)` çağırır.

## 3. Owned Objects

### Event contract'ları (7 toplam — 1 mevcut + 6 yeni)
- ✅ `TenantActivatedV1` (mevcut, **yeniden yazılmaz** — pattern referansı)
- 🆕 `TenantCreatedV1`
- 🆕 `TenantSuspendedV1`
- 🆕 `TenantReactivatedV1`
- 🆕 `TenantCancelledV1`
- 🆕 `TenantProvisioningCompletedV1`
- 🆕 `TenantProvisioningFailedV1`

Hepsi `IInternalEvent` (`Diten.BuildingBlocks.Eventing`) marker'ı uygular; `sealed record` + `Name`/`Version` const + projection property — mevcut `TenantActivatedV1.cs` ile birebir paralel.

### Routing key'leri (lowercase dot notation, MOD-0035 §"Event Naming and Versioning Standard")
- `tenant.created.v1`
- `tenant.activated.v1` (mevcut)
- `tenant.suspended.v1`
- `tenant.reactivated.v1`
- `tenant.cancelled.v1`
- `tenant.provisioning.completed.v1`
- `tenant.provisioning.failed.v1`

### Producer noktaları (mevcut handler'lar — sadece `IEventBus.PublishAsync(...)` satırı eklenir)
- `RegisterTenantCommandHandler` → `TenantCreatedV1` (+ koşullu olarak `TenantActivatedV1` mevcut emit zaten yapılıyorsa dokunulmaz)
- `SuspendTenantCommandHandler` → `TenantSuspendedV1`
- `ReactivateTenantCommandHandler` → `TenantReactivatedV1`
- `DeleteTenantCommandHandler` → `TenantCancelledV1` (soft delete = cancel semantiği)
- Tenant provisioning orchestration handler (Batch 1B koşullu — handler mevcut değilse producer emit Batch 2'ye ertelenir, contract'lar yine eklenir)

### Consumer noktaları (yeni)
- `TenantCreatedV1NotificationMapper : INotificationEventMapper<TenantCreatedV1>` → `tenant.invite.email`
- `TenantSuspendedV1NotificationMapper : INotificationEventMapper<TenantSuspendedV1>` → `tenant.suspended.email`
- `TenantReactivatedV1NotificationMapper : INotificationEventMapper<TenantReactivatedV1>` → `tenant.reactivated.email`
- `TenantLifecycleAuditConsumer` (1 consumer, 7 event family abonesi) → `IAuditService.AppendAsync(...)`

### Notification template key'leri (MOD-0027 owned, bu pack tanımlamaz — sadece referans verir)
- `tenant.invite.email` — yeni tenant create / admin invite
- `tenant.suspended.email`
- `tenant.reactivated.email`

Bu key'lerin NotificationTemplate seed'i bu pack scope'unda **değil** (D8 kararı). Template eksikse mapper `null` dönmez (event-suitable); consumer yan QueueEmailNotificationCommand handler failed dispatch state'ine düşer ve MOD-0027 retry/DLQ akışı işler.

### API endpoint / Frontend route / Permission
- Yok. Bu pack public yüzey üretmez.

## 4. Entity Fields

Bu pack yeni persistence entity tanımlamaz. Outbox/inbox kayıt yapısı MOD-0035 sahipliğindedir; notification dispatch ve audit event kayıtları MOD-0027/MOD-0021 sahipliğindedir.

### Event payload contract'ları (sealed record alanları)

#### `TenantCreatedV1` (`tenant.created.v1`)
| Field | Type | Required | Rule |
|---|---|---|---|
| TenantId | Guid | Yes | Not empty |
| CreatedAtUtc | DateTimeOffset | Yes | UTC only |
| PlanId | Guid? | No | Subscribe edilen plan varsa propagate edilir |
| CreatedBy | Guid? | No | Platform admin Guid; system-initiated ise null |
| TenantDisplayName | string | Yes | Max 200; mapper'da template variable olarak kullanılır |
| AdminEmail | string? | No | Tenant invite mapper'ı için recipient çözümlemesi; PII — audit/log redaction ve contract test kapsamı zorunlu |
| Locale | string | Yes | BCP-47 (`en-US`, `tr-TR`, ...); template locale seçimi |

**AdminEmail PII decision:** Batch 1 implementer önce mevcut tenant/admin user lookup yüzeyini doğrular. Güvenli recipient çözümü `AdminUserId` veya mevcut admin/user lookup ile yapılabiliyorsa `TenantCreatedV1` payload'ı ID-only tasarlanır ve email mapper/consumer içinde çözülür. `AdminEmail` payload'da kalırsa audit/log çıktılarında redacted/masked olmalı ve reflection/contract testleri email alanının PII olarak işlendiğini kanıtlamalıdır.

#### `TenantSuspendedV1` (`tenant.suspended.v1`)
| Field | Type | Required | Rule |
|---|---|---|---|
| TenantId | Guid | Yes | Not empty |
| SuspendedAtUtc | DateTimeOffset | Yes | UTC only |
| Reason | string | Yes | Max 500 |
| SuspendedBy | Guid? | No | Actor Guid |

#### `TenantReactivatedV1` (`tenant.reactivated.v1`)
| Field | Type | Required | Rule |
|---|---|---|---|
| TenantId | Guid | Yes | Not empty |
| ReactivatedAtUtc | DateTimeOffset | Yes | UTC only |
| ReactivatedBy | Guid? | No | Actor Guid |

#### `TenantCancelledV1` (`tenant.cancelled.v1`)
| Field | Type | Required | Rule |
|---|---|---|---|
| TenantId | Guid | Yes | Not empty |
| CancelledAtUtc | DateTimeOffset | Yes | UTC only |
| EffectiveAtUtc | DateTimeOffset | Yes | `>= CancelledAtUtc` |
| Reason | string? | No | Max 500 |
| CancelledBy | Guid? | No | Actor Guid |

#### `TenantProvisioningCompletedV1` (`tenant.provisioning.completed.v1`)
| Field | Type | Required | Rule |
|---|---|---|---|
| TenantId | Guid | Yes | Not empty |
| CompletedAtUtc | DateTimeOffset | Yes | UTC only |
| Steps | IReadOnlyList\<string\> | Yes | En az 1 eleman; her eleman max 128 char |

#### `TenantProvisioningFailedV1` (`tenant.provisioning.failed.v1`)
| Field | Type | Required | Rule |
|---|---|---|---|
| TenantId | Guid | Yes | Not empty |
| FailedAtUtc | DateTimeOffset | Yes | UTC only |
| FailedStep | string | Yes | Max 128 |
| Error | string | Yes | Max 2000, sensitive data redacted (token/secret/connection-string pattern scrub) |
| AttemptCount | int | Yes | >= 1 |

### Payload anti-pattern
- Hiçbir event `Tenant`/`BaseEntity`/`GlobalEntity` tipini barındırmaz.
- Navigation property, full collection-of-entities, password/token/secret alanı bulunmaz.
- Yalnızca ID + primitive/value-object/küçük list alanları taşınır (MOD-0035 §13 contract gate).

## 5. Repo Scope

### Batch 1 (event contracts + producer emit + handler emit tests)
- `execution/domains/platform-shared-services/module-packs/MOD-0009-tenant-lifecycle-events.md` (bu dosya)
- `services/Diten.Platform.Contracts/Events/TenantCreatedV1.cs` (yeni)
- `services/Diten.Platform.Contracts/Events/TenantSuspendedV1.cs` (yeni)
- `services/Diten.Platform.Contracts/Events/TenantReactivatedV1.cs` (yeni)
- `services/Diten.Platform.Contracts/Events/TenantCancelledV1.cs` (yeni)
- `services/Diten.Platform.Contracts/Events/TenantProvisioningCompletedV1.cs` (yeni)
- `services/Diten.Platform.Contracts/Events/TenantProvisioningFailedV1.cs` (yeni)
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Handlers/RegisterTenantCommandHandler.cs` (emit satırı)
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Handlers/SuspendTenantCommandHandler.cs` (emit satırı)
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Handlers/ReactivateTenantCommandHandler.cs` (emit satırı)
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Handlers/DeleteTenantCommandHandler.cs` (emit satırı)
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Tenants/` — handler emit assertion (in-memory bus + fake `IOutboxEventRepository`)
- `services/Diten.Platform/tests/Diten.Platform.Eventing.Tests/` — contract test'leri (EventName/EventVersion uyumu, payload anti-pattern)

### Batch 1B (provisioning emit — koşullu)
- Yalnızca **mevcut** provisioning orchestration handler'ı tespit edilirse:
  - `services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/**` altında mevcut provisioning handler dosyasına emit satırı eklenir
- Handler yoksa contract'lar Batch 1'de eklenir; producer emit eksik handler path'iyle birlikte Batch 2'ye **deferred** olarak işaretlenir (provisioning orchestration **icat edilmez**).

### Batch 2 (consumers)
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Notifications/TenantCreatedV1NotificationMapper.cs` (yeni)
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Notifications/TenantSuspendedV1NotificationMapper.cs` (yeni)
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Notifications/TenantReactivatedV1NotificationMapper.cs` (yeni)
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Audit/TenantLifecycleAuditConsumer.cs` (yeni)
- `services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs` veya `services/Diten.Platform/src/Diten.Platform.Infrastructure/DependencyInjection.cs` (mapper + consumer DI registration; mevcut registration pattern üzerine satır ekleme)
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Tenants/` — mapper unit testleri + audit consumer testleri (in-memory bus, fake `IAuditService`, fake mediator)

### Batch 3 (broker-backed integration proof)
- `services/Diten.Platform/tests/Diten.Platform.Eventing.Tests/` — live external/local RabbitMQ test'leri (yalnızca `Eventing__RabbitMq__IntegrationTestsEnabled=true` ile çalışır)

### Batch 4 (reconciliation/documentation, kod değişikliği yok)
- `docs/platform/master-plan.md` §9.4 MOD-0009 satır güncellemesi
- `execution/domains/platform-shared-services/module-packs/MOD-0009-tenant-lifecycle-events.md` `status: done` + Implementation Report eklenmesi

## 6. Protected Paths

- `.antigravity/**`
- `frontend/Diten.Web/**` (frontend üretilmez)
- `gateway/Diten.ApiGateway/**/ocelot.json` (gateway route eklenmez)
- `frontend/Diten.Web/Controllers/Archive/**`, `frontend/Diten.Web/Views/Archive/**`, `frontend/Diten.Web/Views/Shared/_Layout.cshtml`
- `services/Diten.AuthService/**`
- `services/Diten.DevEnablementService/**`
- `services/Diten.EnterpriseStrategyService/**`
- `services/Diten.MdmService/**`
- `services/Diten.Platform.Contracts/Events/TenantActivatedV1.cs` (mevcut — **yeniden yazılmaz**)
- `services/Diten.Platform.Contracts/Events/Notifications/**` (MOD-0027 owned — bu pack tüketmez ve değiştirmez)
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Notifications/**` (MOD-0027 owned application surface — bu pack mapper register etmek için **Infrastructure DependencyInjection** satırına dokunur, Notifications klasörünün kendisine **dokunmaz**)
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/**` (MOD-0021 owned)
- `services/Diten.Platform/src/Diten.Platform.Application/Contracts/Audit/**` (MOD-0021 public seam — sadece tüketilir)
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/Notifications/**` (MOD-0263 SMTP/Fake provider — bu pack dokunmaz)
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Eventing/Consumers/TenantActivatedV1Consumer.cs` (mevcut — **yeniden yazılmaz**)
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/**` (REST yüzeyi açılmaz; mevcut `NotificationsController` ve tenant controller'lar dokunulmaz)
- MOD-0035 internal'ları: `OutboxPublisherWorker`, `EventEnvelope`, `MassTransitRabbitMqEventBus`, `InMemoryEventBus`, `IOutboxEventRepository`, `IConsumedEventRepository` (bu pack tüketir, **değiştirmez**)

## 7. Dependencies

| Dependency | Rol | Branch state (2026-05-19, `feature/email-service`) | Bu pack için gereksinim |
|---|---|---|---|
| **MOD-0035** Event Bus | Foundation — `IEventBus.PublishAsync` (outbox-backed) + consumer registration | 🟢 Abstractions hazır (in-memory + MassTransit/RabbitMQ adapter + outbox); 🟡 live external/local RabbitMQ proof bekliyor | Batch 1/2 in-memory + fake outbox ile PASS. Batch 3 live broker proof zorunlu. |
| **MOD-0027** Notification Service | Public seam — `INotificationEventMapper<TEvent>` + `QueueEmailNotificationCommand` (MediatR) + `EmailDispatchJob` retry/DLQ | 🟢 Backend foundation hazır (branch'te `Features/Notifications/**` tamamı + `INotificationEventMapper.cs` seam + dispatch job + Application.Tests `NotificationsBatch1A/Batch2Tests.cs`) | Batch 2 mapper'lar bu seam'i implement eder + DI register eder. Tenant lifecycle handler'ından `QueueEmailNotificationCommand` send'i veya mapper çağrısı yapılmaz. |
| **MOD-0263** Messaging Provider | Provider — `SmtpMessagingProvider` (MailKit) prod path + `FakeMessagingProvider` dev | 🟢 Hazır (`SmtpMessagingProvider.cs` + `FakeMessagingProvider.cs` + `NotificationsSmtpIntegrationTests.cs`). SendGrid adapter follow-up. | Bu pack doğrudan tüketmez; MOD-0027 transitive. SendGrid bu pack için **dışarıda**. |
| **MOD-0021** Audit Trail | Public seam — `IAuditService.AppendAsync(AuditAppendRequest)` + `AuditBehavior` MediatR pipeline | 🟢 Hazır (`Contracts/Audit/IAuditService.cs` + `Features/Audit/AuditService.cs` + `AuditBehavior.cs` + `AuditEventRepository.cs`) | Batch 2 audit consumer doğrudan `IAuditService.AppendAsync(...)` çağırır; yeni audit entity/repo açılmaz. Audit payload redacted (PII alan maskeleme MOD-0021 standardı). |
| **MOD-0026** Background Job Scheduler | Hosting — Hangfire scheduler mechanics; `EmailDispatchJob` zaten MOD-0027 altında çalışıyor | 🟢 Hazır (scheduler + EmailDispatchJob/EmailDispatchSweepJob mevcut) | Bu pack yeni job yazmaz. `ProvisioningRetryJob` follow-up. |
| **MOD-0297** Subscription Lifecycle | Downstream consumer (default trial subscription on `TenantCreatedV1`) | 🟡 Partial | Bu pack consumer wiring yapmaz; MOD-0297 kendi pack'inde subscribe eder. Blocker değil. |
| **MOD-0298** Tenant Module Entitlement | Downstream consumer (entitlement cache invalidation on `TenantCancelledV1`) | 🟢 Hazır | Bu pack consumer wiring yapmaz; MOD-0298 kendi pack'inde subscribe eder. Blocker değil. |
| **NEW-001** Secrets Vault | Indirect — RabbitMQ credentials | 🔴/🟡 pending | Bu pack credential storage yapmaz; MOD-0035'in appsettings + env var standardını miras alır. Blocker değil. |

## 8. Runtime Constraints

- **Outbox boundary:** Tenant aggregate save + outbox row insert aynı logical unit-of-work içinde (MOD-0035 §"Outbox Transaction Boundary Standard").
- **At-least-once delivery:** Consumer'lar idempotent (MOD-0035 ConsumedEvent inbox; `ConsumerName + EventId` unique).
- **CorrelationId propagation:** Tenant lifecycle command'dan envelope'a, oradan consumer log + audit + notification queue command'ına aynı CorrelationId aktarılır.
- **Routing key formatı:** Lowercase `tenant.{action}.v1` (compound action için `tenant.provisioning.completed.v1`). Breaking change → `v2`; v1 consumer çalışmaya devam eder.
- **Timestamp:** Tüm `*AtUtc` alanları `DateTimeOffset` (UTC); `DateTime` kullanılmaz.
- **Broker abstraction discipline:**
  - Application/handler katmanından `RabbitMQ.Client` veya `MassTransit.IBus.Publish` doğrudan çağrı **YASAK**.
  - Tüm emit `Diten.BuildingBlocks.Eventing.IEventBus.PublishAsync(...)` (outbox'a yazan public abstraction) üzerinden gider.
  - Eğer mevcut `IEventBus.PublishAsync` outbox'a yazmıyorsa (Batch 1 doğrulama gate'i): pack **BLOCKED**, MOD-0035 §"Outbox Transaction Boundary Standard" tamamlanana kadar implementation duraklatılır.
- **Notification trigger discipline:**
  - Tenant lifecycle command handler içinden `IMediator.Send(new QueueEmailNotificationCommand(...))` çağrısı **YASAK**.
  - Tenant lifecycle command handler içinden `INotificationEventMapper<T>` doğrudan çağrısı **YASAK**.
  - Notification trigger'ı yalnızca MOD-0027'nin generic event consumer'ı tarafından mapper aracılığıyla yapılır. Bu pack mapper'ı tanımlar ve DI'a kaydeder; consumer infrastructure'ı tüketmez.
- **Audit trigger discipline:**
  - Tenant lifecycle command handler içinden `IAuditService.AppendAsync(...)` çağrısı **YASAK** (mevcut `AuditBehavior` pipeline'ı zaten command-level audit yapıyor; bu pack event-level audit ekler).
  - Audit yalnızca `TenantLifecycleAuditConsumer` event consumer'ı tarafından `IAuditService.AppendAsync(...)` ile yapılır.
  - Audit `AuditAppendRequest.Metadata` yalnızca metadata + ID alanlarını taşır; PII (email, display name) redaction policy'sine tabi.
- **Tenant entity içine event emit YASAK:** Domain entity'lerden `IEventBus.PublishAsync` çağrılmaz; emit yalnızca command handler katmanında.
- **Live broker prerequisite:** Batch 3 PASS olmadan modül `done` işaretlenmez. Batch 1/2 PASS mainline'a alınabilir (in-memory + outbox testleriyle).
- **`entity_base: BaseEntity` gerekçesi:** Bu pack yeni MongoDB entity üretmez. Frontmatter alanı doldurma amaçlı en yakın eşleşme.

## 9. Layout & Shell Contract

- `shell: none`. Razor layout gerekmez.
- View klasörü yok, frontend route yok.
- Future operatör görselleştirme gerekirse ayrı `shell: platform-admin` pack'i açılır; orada `Layout = "_LayoutPlatformAdmin"` açıkça yazılır.

## 10. Backend File Convention

`golden_reference: none` — bu pack DataTable/CRUD modülü değildir.

### Event contract'ları (`services/Diten.Platform.Contracts/Events/`)
- Her event ayrı `.cs` dosyasında, dosya adı event sınıfıyla aynı.
- `sealed record` + primary constructor.
- `IInternalEvent` (`Diten.BuildingBlocks.Eventing`).
- `public const string Name = "tenant.{action}.v1";` + `public const int Version = 1;` + projection property — mevcut `TenantActivatedV1.cs` ile birebir paralel.
- Bir dosyada birden fazla `public record` YASAK.

### Tenant lifecycle handler'ları (mevcut — emit eklenir, refactor yapılmaz)
Mevcut Platform tenant handler'ları `services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Handlers/` altında **flat**. Bu pack **refactor yapmaz** (D9 kararı):
- Sadece emit satırı eklenir: `await _eventBus.PublishAsync(new TenantXxxV1(...), ct);` (CorrelationId envelope'a ICurrentRequestContext/Activity üzerinden geçirilir).
- Constructor'a `IEventBus` parametresi eklenir (mevcut DI registration üzerine).
- Class rename, file move, naming refactor **yok**.

### Notification mapper'ları (`services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Notifications/`)
- Dosya adı sınıf adıyla aynı: `TenantCreatedV1NotificationMapper.cs`, vb.
- `sealed class` + `INotificationEventMapper<TenantXxxV1>` implement eder.
- `Map(EventEnvelope<TenantXxxV1> envelope)` çağrısı `QueueEmailNotificationRequest?` döner.
- Event suitable değilse (suppressed/missing variable) `null` döner — controlled skip.
- TenantId envelope'tan alınır (out-of-band tenant context yüklemez — `INotificationEventMapper` interface kontratı).
- Template key string'i sabit (`tenant.invite.email`, vb.).
- Locale event payload'undan veya tenant default'undan çözülür.

### Audit consumer (existing consumer pattern'a göre yerleştirilir)
- Placement implementation sırasında Phase 0'da doğrulanan mevcut event consumer layer/folder pattern'ini takip eder. Mevcut pattern `Diten.Platform.Infrastructure/Eventing/*Consumer.cs` ise audit consumer da aynı registration/layer yaklaşımını kullanır; branch'te onaylı Application-level event consumer pattern'i varsa o pattern izlenir.
- Yeni eventing mimarisi, yeni consumer abstraction'ı veya paralel broker/inbox altyapısı **oluşturulmaz**.
- `sealed class TenantLifecycleAuditConsumer` + mevcut consumer interface/pattern ile `TenantCreatedV1`, `TenantSuspendedV1`, ... (7 event family) handle edilir.
- Idempotency: handler içinde `IConsumedEventRepository` ile `ConsumerName + EventId` check (MOD-0035 inbox standardı).
- `IAuditService.AppendAsync(AuditAppendRequest)` çağrısı; `AuditCategory.TenantLifecycle` veya benzeri enum value (MOD-0021'in mevcut enum'una uyacak şekilde — implementation aşamasında onaylanır).
- Metadata: EventId, EventName, EventVersion, CorrelationId, TenantId, OccurredAtUtc, Actor (Guid), Reason (redacted/truncated).
- Logging: yalnızca metadata (EventId, EventName, EventVersion, CorrelationId, TenantId, ConsumerName, Status, AttemptCount). Payload alanları log'a yazılmaz.

### Naming kuralları
- Event record: `Tenant{Action}V1` (PascalCase).
- Routing key: `tenant.{action}.v1` (lowercase, dot notation).
- Consumer/mapper class: `Tenant{Action}V1{Suffix}` (`Consumer` veya `NotificationMapper`).

## 11. Frontend File Contract

- N/A. Bu pack frontend dosyası üretmez.

## 12. Validation Rules

Event payload doğrulaması Bölüm 4 tabloları + MOD-0035 §12 standardıyla birleşik.

### Cross-event
| Field | Required | Format/Rule | Pre-check |
|---|---|---|---|
| TenantId | Yes | Guid not empty | Tenant aggregate mevcut olmalı; state transition handler validator'larında check edilir |
| `*AtUtc` | Yes | `DateTimeOffset` UTC | Handler `DateTimeOffset.UtcNow` veya `IClock` üzerinden çözer |
| Actor (`CreatedBy` / `SuspendedBy` / ...) | No | Guid set edilmişse, null izinli (system) | `ICurrentUserContext` |

### Event-specific
- `TenantCreatedV1.TenantDisplayName` — Required, max 200, trim.
- `TenantCreatedV1.Locale` — Required, BCP-47 (`en-US`, `tr-TR`, vb.); default `en-US`.
- `TenantSuspendedV1.Reason` — Required, max 500, boş string reddedilir.
- `TenantCancelledV1.EffectiveAtUtc` — Required, `>= CancelledAtUtc`.
- `TenantProvisioningCompletedV1.Steps` — Required, en az 1 eleman, her eleman max 128.
- `TenantProvisioningFailedV1.FailedStep` — Required, max 128.
- `TenantProvisioningFailedV1.Error` — Required, max 2000, sensitive pattern scrub.
- `TenantProvisioningFailedV1.AttemptCount` — Integer >= 1.

### Routing key validation
- `EventName` ↔ `EventVersion` uyumu (MOD-0035 contract test gate).

## 13. Failure Path to Verify

- **Duplicate event delivery (consumer-side)** — Aynı `EventId + ConsumerName` ikinci kez geldiğinde business side effect tetiklenmez; `ConsumedEvent.Status = SkippedDuplicate`; `event.consumer.duplicate_skipped` log atılır.
- **Missing required payload field** — Validation aşamasında reddedilir; outbox satırı oluşmaz; correlation id ile log.
- **Invalid `EventName` / `EventVersion`** — `tenant.suspended.v1` routing key + `EventVersion = 2` ⇒ publish öncesi reject; outbox yazılmaz; `event.outbox.publish_rejected` log.
- **Broker (RabbitMQ) down** — Tenant suspend command commit edilir; outbox `Pending` kalır; broker recovery → publish; business transaction rollback **yok**.
- **Outbox publish başarısız (max retry exhausted)** — 5 deneme sonra `OutboxEvent.Status = DeadLettered`; DLQ retention 30 gün; `event.deadlettered` log.
- **Notification mapper `null` döner (suppressed)** — Consumer skip eder; audit consumer paralel başarılı tamamlanır; controlled flow, hata değil.
- **Notification template eksik (`tenant.invite.email` NotificationTemplateRepository'de yoksa)** — `QueueEmailNotificationCommand` handler failed dispatch state'i döner; MOD-0027 retry/DLQ akışı işler; audit consumer paralel başarılı (notification dependency audit'i blok etmez).
- **Audit consumer down** — Lifecycle akışı bloklanmaz; MOD-0021 retry/DLQ; notification mapper akışı paralel başarılı.
- **Tenant aggregate save başarılı + outbox insert başarısız** — Aynı unit-of-work invariant; integration test bu invariant'ı doğrular.
- **Sensitive data leak (provisioning error)** — `TenantProvisioningFailedV1.Error` credential/connection-string scrub edilmeden yazılırsa contract test fail; PR merge edilmez.
- **Concurrency: Suspend during active provisioning** — Her event Tenant state'inin kendi snapshot'unu taşır; idempotency CorrelationId + Tenant state machine validation handler-side'da garanti edilir.

## 14. Authorization Convention

- Bu modül user-facing endpoint açmaz; authorization kuralı uygulanmaz.
- Event publish yalnızca application servis / command handler / internal worker katmanından DI ile (MOD-0035 §"Service/Internal Publish Security Standard").
- Public publish endpoint **yasak**.
- Future operatör replay endpoint açılırsa ayrı pack + `Platform.EventBus.Replay` permission.

## 15. Gateway / API Routing Decision

- **Karar:** Gateway değişikliği bu pack için **gereksiz**.
- Public REST endpoint, frontend route veya OPTIONS yüzeyi üretilmez.
- `gateway/Diten.ApiGateway/**/ocelot.json` protected path olarak korunur; bu pack dokunmaz.
- Mevcut tenant lifecycle command'ları zaten Gateway üzerinden expose edilmiş; bu pack o yüzeye dokunmaz.

## 16. Acceptance Criteria (Batch-Split)

### Batch 1 — Event Contracts + Producer Emit + Contract/Handler Tests

- [ ] 6 yeni event contract sealed record olarak tanımlı: `TenantCreatedV1`, `TenantSuspendedV1`, `TenantReactivatedV1`, `TenantCancelledV1`, `TenantProvisioningCompletedV1`, `TenantProvisioningFailedV1` — hepsi `Diten.Platform.Contracts/Events/` altında, `IInternalEvent` implementasyonu, `Name`/`Version` const + projection — mevcut `TenantActivatedV1.cs` ile birebir şablon.
- [ ] `TenantActivatedV1.cs` rewrite **yok**.
- [ ] Her event payload'ı yalnızca ID + primitive/value-object/küçük list alanları; `Tenant`/`BaseEntity`/navigation/secret/token/blob **yok** (reflection-based contract test PASS).
- [ ] Routing key contract test: 6 yeni key `tenant.{action}.v1` formatına uyuyor, `EventName` ↔ `EventVersion` uyumlu.
- [ ] `RegisterTenantCommandHandler` `TenantCreatedV1` event'ini `IEventBus.PublishAsync(...)` ile outbox'a yazıyor (aynı unit-of-work).
- [ ] `SuspendTenantCommandHandler` `TenantSuspendedV1` outbox'a yazıyor.
- [ ] `ReactivateTenantCommandHandler` `TenantReactivatedV1` outbox'a yazıyor.
- [ ] `DeleteTenantCommandHandler` `TenantCancelledV1` outbox'a yazıyor (soft delete = cancel semantiği).
- [ ] HTTP/handler katmanından `RabbitMQ.Client` veya `MassTransit.IBus.Publish` doğrudan çağrı **yok** (statik analiz / mock-strict test).
- [ ] Tenant lifecycle handler içinden `IMediator.Send(new QueueEmailNotificationCommand(...))` veya `IAuditService.AppendAsync(...)` çağrısı **yok** (lifecycle path event-only).
- [ ] Tenant domain entity'lerinden `IEventBus.PublishAsync` çağrısı **yok**.
- [ ] Handler emit assertion testleri: in-memory bus + fake `IOutboxEventRepository` ile 4 producer handler'ı doğrulanıyor.
- [ ] Build: `dotnet build services/Diten.Platform/Diten.Platform.sln -c Debug` PASS, warning sıfır.
- [ ] **Live RabbitMQ proof Batch 1 için gerekmez** — in-memory + fake outbox ile PASS edilir.

### Batch 1B — Provisioning Producer Emit (Koşullu)

- [ ] Mevcut tenant provisioning orchestration handler'ı **tespit** edildi (file path Implementation Notes'a yazıldı); aksi halde Batch 1B atlandı ve emit Batch 2'ye deferred olarak işaretlendi.
- [ ] Handler tespit edildiyse: `TenantProvisioningCompletedV1` ve `TenantProvisioningFailedV1` outbox'a yazılıyor; handler emit assertion testi PASS.
- [ ] Provisioning orchestration **icat edilmedi**; handler yoksa contract'lar mevcut kalır, producer eksikliği documented.

### Batch 2 — Consumers (Notification Mapper'ları + Audit Consumer)

- [ ] 3 notification mapper register edildi: `TenantCreatedV1NotificationMapper`, `TenantSuspendedV1NotificationMapper`, `TenantReactivatedV1NotificationMapper` — hepsi `INotificationEventMapper<TenantXxxV1>` implement ediyor.
- [ ] Mapper'lar `tenant.invite.email` / `tenant.suspended.email` / `tenant.reactivated.email` template key'lerini kullanıyor; template tanımlama bu pack scope'unda **yok**.
- [ ] Mapper'lar event suitable değilse (suppressed/missing variable) `null` dönüyor (controlled skip).
- [ ] DI registration: mapper'lar `Diten.Platform.Application/DependencyInjection.cs` veya `Diten.Platform.Infrastructure/DependencyInjection.cs` üzerinden register edildi (mevcut pattern üzerine satır ekleme).
- [ ] `TenantLifecycleAuditConsumer` 7 event family'sine subscribe oluyor, `IAuditService.AppendAsync(...)` çağırıyor — yeni audit entity/repository açılmadı.
- [ ] Audit payload yalnızca metadata + ID alanları içeriyor; PII alanlar (email, display name) redacted/masked.
- [ ] Idempotent consumer: aynı `EventId + ConsumerName` ikinci kez consume edildiğinde business side effect tetiklenmez; test PASS.
- [ ] CorrelationId publish → consume → audit/notification queue zincirinde propagate; log korelasyonu doğrulandı.
- [ ] Mapper unit testleri: 3 mapper'ın `Map(envelope)` çıktısı `QueueEmailNotificationRequest` shape doğrulandı; suppressed senaryosu PASS.
- [ ] Audit consumer testleri: in-memory bus + fake `IAuditService` ile 7 event consume → audit append assertion PASS.

### Batch 3 — Broker-Backed Integration Proof (Live RabbitMQ)

- [ ] `Eventing__RabbitMq__IntegrationTestsEnabled=true` flag + reachable broker ile `Diten.Platform.Eventing.Tests` 7 lifecycle event publish-consume PASS.
- [ ] Idempotency live broker'da PASS.
- [ ] Broker-down senaryosu: tenant suspend commit → outbox `Pending` → broker recovery → publish → consumer success.
- [ ] Consumer failure retry/DLQ: 5 deneme sonra DLQ transition; retry exponential backoff (10s → 5m max) doğrulandı.
- [ ] CorrelationId live publish-consume zincirinde propagate.
- [ ] Notification queue end-to-end: live broker üzerinden `TenantCreatedV1` consume → mapper → `QueueEmailNotificationCommand` → `EmailDispatchJob` → `FakeMessagingProvider` (test mode) → dispatch SENT; integration test PASS.
- [ ] Audit live broker üzerinden: 7 event audit append edildiği doğrulandı.

### Batch 4 — Reconciliation/Documentation (Kod Yok)

- [ ] `docs/platform/master-plan.md` §9.4 MOD-0009 satırı `50` → `≥85` veya `done` güncellendi; reconciliation notu eklendi.
- [ ] Pack `status: draft` → `ready-for-dev` → `in-progress` → `review` → `done` lifecycle'ı tamamlandı.
- [ ] Pack'in Implementation Notes bölümüne final implementation report eklendi (hangi provisioning handler bulundu, hangi follow-up'lar açıldı).

**Module DONE = Batch 1 + Batch 2 + Batch 3 PASS.** Batch 1B koşullu olabilir; Batch 4 her zaman son adım.

## 17. Test Expectations

### Build (her batch sonrası)
- `dotnet build services/Diten.Platform.Contracts/Diten.Platform.Contracts.csproj -c Debug`
- `dotnet build services/Diten.Platform/src/Diten.Platform.Application/Diten.Platform.Application.csproj -c Debug`
- `dotnet build services/Diten.Platform/src/Diten.Platform.Infrastructure/Diten.Platform.Infrastructure.csproj -c Debug`
- `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug`

### Batch 1 testleri
- Unit / contract (`Diten.Platform.Eventing.Tests` veya `Diten.Platform.Application.Tests`):
  - Her event'in `EventName`/`EventVersion`/projection property'leri doğru.
  - Routing key regex tüm 7 routing key'i kabul ediyor.
  - Payload contract test reflection-based: hiçbir event `Tenant`/`BaseEntity`/`Diten.Platform.Domain.Entities.*` tipi içermiyor.
  - Field-level kurallar (Reason boş reddi, EffectiveAtUtc >= CancelledAtUtc, Steps boş reddi, Error sensitive scrub).
- Handler emit assertion (`Diten.Platform.Application.Tests/Tenants/`):
  - `RegisterTenantCommandHandler.Handle(...)` sonrası fake `IOutboxEventRepository.AddAsync` `TenantCreatedV1` ile çağrıldı.
  - Aynı assertion 4 lifecycle handler için.
  - Strict mock: `IEventBus.PublishAsync` dışında broker publish API çağrılmadı.

### Batch 1B testleri (koşullu)
- Provisioning handler tespit edildiyse: handler emit assertion `TenantProvisioningCompletedV1` / `TenantProvisioningFailedV1` için.

### Batch 2 testleri
- Mapper unit testleri:
  - `TenantCreatedV1NotificationMapper.Map(envelope)` doğru `QueueEmailNotificationRequest` döner (TemplateKey, Locale, To, variables).
  - Suppressed senaryosu (`null` dönüş) PASS.
  - TenantId envelope'tan alınıyor, out-of-band yüklenmiyor.
- Audit consumer testleri:
  - 7 event family'si için `IAuditService.AppendAsync` çağrıldı, payload metadata-only.
  - PII alanlar redacted.
  - Idempotency: ikinci consume `SkippedDuplicate`.

### Batch 3 testleri (live broker, opsiyonel flag)
- `Eventing__RabbitMq__IntegrationTestsEnabled=true` iken `Diten.Platform.Eventing.Tests`:
  - 7 event publish-consume PASS.
  - Idempotency, broker-down recovery, consumer-failure retry/DLQ, CorrelationId propagation.
  - End-to-end notification queue: live RabbitMQ → mapper → MOD-0027 dispatch job → FakeMessagingProvider SENT.
- Flag false ise skip with clear reason.

### Smoke
- Backend/eventing modülü olduğu için **browser smoke gerekmez** (D7 kararı).
- API/eventing smoke: Platform API'ye tenant suspend POST → outbox row + audit append + notification dispatch queued doğrulandı (manuel HTTP veya integration test).

### Quality gate
- DataTable verifier **N/A**.
- RESX checker: bu pack yeni RESX eklemez; regression olmamalı.

## 18. Ready-for-dev Checklist

Pack `draft` → `ready-for-dev` geçişi için aşağıdaki maddeler kullanıcı onayıyla işaretlenir:

- [x] §0 Pre-dev Blocker Decisions tablosundaki D1–D9 kararları kabul edildi.
- [x] Dependency tablosu güncel branch state'iyle (`feature/email-service`) doğrulandı (MOD-0027/MOD-0263/MOD-0021 ✅, MOD-0035 abstractions ✅ + live broker 🟡).
- [x] Batch 1 / 1B / 2 / 3 / 4 split kabul edildi.
- [x] Live RabbitMQ Batch 3 koşulu olarak kabul edildi (Batch 1 in-memory + outbox testleriyle PASS edilebilir).
- [x] Notification consumer pattern `INotificationEventMapper<TEvent>` seam'i üzerinden kabul edildi (consumer infrastructure MOD-0027 sahipliğinde).
- [x] Audit consumer pattern `IAuditService.AppendAsync(...)` ve mevcut event consumer layer/folder pattern'i üzerinden kabul edildi (yeni audit entity/repo veya yeni eventing mimarisi açılmaz).
- [x] Provisioning event kararı (Batch 1B koşullu, handler icat edilmez) kabul edildi.
- [x] Browser smoke ertelendi; API/eventing smoke yeterli sayıldı.
- [x] UI ve gateway değişikliği olmadığı kabul edildi.
- [x] Protected paths listesi (özellikle MOD-0027 Notifications klasörü, MOD-0021 Audit klasörü, MOD-0263 SMTP provider klasörü, `TenantActivatedV1.cs`/Consumer'ı) kabul edildi.
- [x] Tenant handler folder refactor bu pack scope'unda **yok** kararı kabul edildi.
- [x] Phase 0 implementation gate kabul edildi: outbox-backed `IEventBus`, `TenantActivatedV1` pattern'i, lifecycle handler path'leri ve consumer registration pattern'i doğrulanmadan Batch 1 kodu yazılmaz.
- [x] AdminEmail PII kararı kabul edildi: mümkünse ID-only payload tercih edilir; email payload'da kalırsa audit/log redaction + contract test zorunludur.

## 19. Implementation Notes

- **Sıralama:** Batch 1 → Batch 1B (koşullu) → Batch 2 → Batch 3 → Batch 4. Batch 1/2 mainline PR'ları paralel olabilir; Batch 3 ayrı PR live broker config ile.
- **Phase 0 gate:** Batch 1 implementer önce `IEventBus.PublishAsync` outbox-backed mı, `TenantActivatedV1` pattern'i mevcut mu, tenant lifecycle handler path'leri doğru mu ve consumer registration pattern'i nerede/ nasıl yapılmış mı doğrular. Bu evidence yazılmadan contract/producer kodu yazılmaz; eksik varsa `BLOCKED/PARTIAL` raporlanır.
- **Batch 1 Phase 0 evidence (2026-05-19):**
  - `services/Diten.Platform/src/Diten.Platform.Application/Services/Eventing/EventBus.cs` `PublishAsync(...)`, payload validation sonrası `OutboxEvent.FromEnvelope(envelope)` ile `_outboxRepository.AddAsync(...)` çağırıyor; doğrudan RabbitMQ/MassTransit publish yok.
  - `services/Diten.Platform.Contracts/Events/TenantActivatedV1.cs` mevcut pattern'i doğrulandı: `sealed record`, `IInternalEvent`, `Name = "tenant.activated.v1"`, `Version = 1`, `EventName`/`EventVersion` projection.
  - Tenant lifecycle handler path'leri doğrulandı: `RegisterTenantCommandHandler.cs`, `SuspendTenantCommandHandler.cs`, `ReactivateTenantCommandHandler.cs`, `DeleteTenantCommandHandler.cs`.
  - Mevcut consumer registration pattern'i yalnızca Batch 2 planlama için incelendi: `Infrastructure/Eventing/TenantActivatedV1Consumer.cs` + `Infrastructure/DependencyInjection.cs` `AddConsumer<TenantActivatedV1Consumer>()` / `ConfigureEndpoints(context)`.
  - Tenant initial admin için domain modelde `TenantAdminUser.Id` mevcut olduğu için Batch 1 `TenantCreatedV1` ID-only payload kullanır (`InitialAdminUserId`); `AdminEmail` event payload'ına eklenmedi.
  - Tenant provisioning orchestration handler'ı bulunmadı; `TenantProvisioningCompletedV1` / `TenantProvisioningFailedV1` producer emit Batch 1B için deferred, orchestration icat edilmedi.
- **Brownfield handler durumu:** Tenant lifecycle handler'ları flat `Features/Tenants/Handlers/` altında; refactor **yok** (D9). Sadece constructor'a `IEventBus` injection + emit satırı eklenir.
- **`TenantActivatedV1` mevcudiyeti:** Re-create/rename **yok**. Mevcut emit yapılıyorsa dokunulmaz; yapılmıyorsa eklenir. Implementation aşamasında `RegisterTenantCommandHandler` activation step doğrulanır.
- **`TenantCancelledV1` semantiği:** `DeleteTenantCommand` soft delete uyguluyorsa cancel semantiği bu event ile temsil edilir.
- **`TenantProvisioningCompletedV1.Steps`:** Step kodları mevcut provisioning orchestration tespitinde netleşir; bu pack step taxonomy'sini icat etmez.
- **`INotificationEventMapper<T>` seam'i:** Branch'te `Diten.Platform.Application/Features/Notifications/Eventing/INotificationEventMapper.cs` mevcut. Interface contract'ı açıkça **mapping ownership'i source event publisher'a (MOD-0009)** bırakıyor. Mapper konkre'leri `Features/Tenants/Notifications/` altında durur (MOD-0027'nin Notifications klasörüne **dokunulmaz**).
- **`IAuditService.AppendAsync` çağrısı:** Branch'te `Diten.Platform.Application/Contracts/Audit/IAuditService.cs` mevcut. `AuditAppendRequest` shape ve `AuditCategory` enum'u implementation aşamasında MOD-0021'in mevcut enum/contract'larına göre netleşir.
- **Audit consumer placement:** Audit consumer, Phase 0'da bulunan mevcut event consumer layer/folder pattern'iyle aynı yere yerleştirilir ve aynı DI/registration yaklaşımını kullanır. Yeni eventing architecture veya ayrı consumer abstraction açılmaz.
- **AdminEmail PII:** `TenantCreatedV1` için güvenli lookup mümkünse email yerine ID-only payload tercih edilir; `AdminEmail` kalırsa audit/log redaction ve contract test zorunludur.
- **CorrelationId kaynağı:** Mevcut `ICurrentRequestContext`/`Activity.Current` üzerinden çözüm; outbox envelope'a yazılır, consumer'lar `Activity.Current` ve log scope'larına aktarır.
- **`QueueEmailNotificationCommand` correlation:** Command zaten `CorrelationId` string parametresi alıyor; mapper bunu envelope'tan doldurmalı.
- **MassTransit registration:** Mevcut `Diten.Platform.Infrastructure/DependencyInjection.cs` üzerinde consumer/mapper registration satırı eklenir. Yeni eventing infrastructure registration yapılmaz.
- **Live RabbitMQ test environment:** `Eventing__RabbitMq__IntegrationTestsEnabled=true` flag + reachable broker; Dev1 sorumluluğunda.
- **Master-plan reconciliation:** Implementation sonrası §9.4 satırı + §"MOD-0009" %100 için kalanlar listesi güncellenir.
- **Kod yazılmadı:** Pack hazırlığı kapsamında hiçbir kod dosyası eklenmedi/değiştirilmedi.
- **Batch 2 implementation evidence (2026-05-20):**
  - 3 MOD-0009-owned notification mapper eklendi: `Features/Tenants/Notifications/TenantCreatedV1NotificationMapper.cs`, `TenantSuspendedV1NotificationMapper.cs`, `TenantReactivatedV1NotificationMapper.cs`.
  - Mapper DI registration `Diten.Platform.Application/DependencyInjection.cs` içinde mevcut Application registration pattern'ine eklendi; MOD-0027 `Features/Notifications/**` dosyaları değiştirilmedi.
  - `QueueEmailNotificationRequest` mevcut contract'i yalnızca concrete email recipient (`EmailRecipientDto.Email`) destekliyor; tenant lifecycle event payload'ları ID-only/recipient-free olduğu için 3 mapper controlled `null` skip döner. TemplateKey sabitleri korunur: `tenant.invite.email`, `tenant.suspended.email`, `tenant.reactivated.email`. Bu Batch 2 notification delivery kısmı PARTIAL olarak raporlanır; recipient-ID resolution contract'i veya ayrı recipient resolver olmadan fake email üretilmedi.
  - `TenantLifecycleAuditConsumer` mevcut consumer layer pattern'ine uygun olarak `Infrastructure/Eventing/` altında eklendi ve `Infrastructure/DependencyInjection.cs` içinde `AddConsumer<TenantLifecycleAuditConsumer>()` ile register edildi.
  - Audit consumer 7 event family'yi handle eder: created, activated, suspended, reactivated, cancelled, provisioning completed, provisioning failed. Side effect yalnızca MOD-0021 public `IAuditService.AppendAsync(AuditAppendRequest)` çağrısıdır; yeni audit entity/repository yoktur.
  - Idempotency mevcut MOD-0035 `ConsumedEventStore.ExecuteOnceAsync(..., nameof(TenantLifecycleAuditConsumer), ...)` pattern'i ile sağlandı; aynı `EventId + ConsumerName` ikinci append'i tetiklemez.
  - Audit metadata `EventId`, `EventName`, `EventVersion`, `TenantId`, `CorrelationId`, `CausationId`, `OccurredAtUtc`, `Producer`, actor id ve event-specific metadata taşır. Tenant display name/reason gibi PII/business text alanları `[REDACTED]` olarak yazılır; `InitialAdminUserId` ID metadata olarak korunur.
  - `TenantProvisioningCompletedV1.Steps` mevcut contract as-is ile audit metadata'da step code listesi olarak tüketildi; Batch 2 consumer path'inde MOD-0035 validator collection conflict'i tetiklenmedi çünkü publish contract validation internals değiştirilmedi.
  - Test evidence: tenant-filtered Application tests 24/24 PASS; Eventing tests 39 PASS / 2 live RabbitMQ SKIP; full `dotnet test services/Diten.Platform -c Debug` Application suite 278/278 PASS.
- **Batch 2.1 recipient-resolution evidence (2026-05-20):**
  - Decision: Option A uygulandı. Tenant lifecycle event payload'ları ID-only kalır; `AdminEmail` geri eklenmedi. Recipient resolution event consumer layer'da `ITenantRegistryRepository` üzerinden tenant aggregate `AdminUsers` listesinden yapılır.
  - Yeni consumer: `services/Diten.Platform/src/Diten.Platform.Infrastructure/Eventing/TenantLifecycleNotificationConsumer.cs`. Existing MOD-0035 pattern'iyle `IConsumer<EventTransportMessage>` + `ConsumedEventStore.ExecuteOnceAsync(..., nameof(TenantLifecycleNotificationConsumer), ...)` kullanır.
  - Consumer registration: `Infrastructure/DependencyInjection.cs` içinde `AddConsumer<TenantLifecycleNotificationConsumer>()` eklendi; yeni eventing architecture veya transport publish yolu açılmadı.
  - `TenantCreatedV1` recipient strategy: `InitialAdminUserId` ile tenant aggregate içindeki matching, disabled olmayan `TenantAdminUser` bulunur; email yalnızca `QueueEmailNotificationRequest.To` içinde MOD-0027 queue path'ine verilir. Event payload ve audit/event metadata email taşımaz.
  - `TenantSuspendedV1` / `TenantReactivatedV1` recipient strategy: tenant owner/admin yüzeyi olarak `Tenant.AdminUsers` içindeki `Active` veya `Invited` admin users hedeflenir; `Disabled` ve recipient'sız durumlar controlled skip olarak tüketilir.
  - Mapper update: tenant-owned mapper'lar resolved-recipient overload aldı; interface `Map(envelope)` recipient-free contract nedeniyle controlled `null` kalır, consumer resolved recipients ile non-null request üretir.
  - Queue path: consumer `QueueEmailNotificationCommand(tenantId, request, envelope.CorrelationId.ToString("N"))` gönderir. SMTP/template rendering/provider çağrısı yapılmaz.
  - Failure behavior: recipient yoksa controlled skip + consumed; queue command başarısızsa exception ile MOD-0035 consumed-event failure path'e düşer.
  - PII/logging: consumer/mapper path'inde email loglanmaz; email yalnızca existing MOD-0027 queue request recipient alanına girer.
  - Test evidence: tenant-filtered Application tests 30/30 PASS; API build PASS; full `dotnet test services/Diten.Platform -c Debug` Application suite 284/284 PASS.
- **Batch 3 live RabbitMQ evidence (2026-05-20):**
  - Yeni live proof test'i eklendi: `services/Diten.Platform/tests/Diten.Platform.Eventing.Tests/TenantLifecycleRabbitMqIntegrationTests.cs`.
  - Flag behavior: `Eventing__RabbitMq__IntegrationTestsEnabled` `true` değilse test `Skip` olur. Flag false eventing suite sonucu: 39 PASS / 3 SKIP / 0 FAIL.
  - Live broker config env/default üzerinden okunur: `Eventing__RabbitMq__Host`, `Port`, `Username`, `Password`, `VirtualHost`; test run default local RabbitMQ (`localhost:5672`, vhost `/`, username `guest`, password redacted) ile PASS verdi.
  - Test fixture yalnızca test DB'sinde tenant aggregate, platform-default `Fake` messaging settings ve 3 active notification template seed eder: `tenant.invite.email`, `tenant.suspended.email`, `tenant.reactivated.email`. Production template seed yoktur.
  - Publish/consume proof gerçek outbox path'iyle çalışır: `EventBus.PublishAsync(...)` -> Mongo outbox -> `OutboxPublisherProcessor` -> `MassTransitRabbitMqEventPublisher` -> RabbitMQ -> registered `TenantLifecycleAuditConsumer` / `TenantLifecycleNotificationConsumer`.
  - Event family coverage: created, activated, suspended, reactivated, cancelled, provisioning failed; provisioning completed publish de denenir ve mevcut validator kabul ederse audit hedef sayısına dahil edilir, collection validator reject ederse test core events ile devam eder.
  - Audit live proof: capturing `IAuditService` üzerinden metadata `EventId`, `EventName`, `EventVersion`, `TenantId`, `CorrelationId` doğrulanır; admin email ve raw sensitive/business text audit JSON içinde bulunmaz, redaction marker doğrulanır.
  - Notification live proof: tenant admin recipient `Tenant.AdminUsers` üzerinden resolve edilir; `QueueEmailNotificationCommand` capture edilir; `QueueEmailNotificationHandler` + `FakeMessagingProvider` ile 3 dispatch `Sent` olur ve correlation id dispatch kayıtlarına taşınır.
  - Idempotency live proof: duplicate delivery outbox unique `EventId` invariant'ını delmeden aynı broker transport message tekrar publish edilerek yapılır. Audit append ve notification queue side effect count değişmez. Mevcut `ConsumedEventStore` duplicate replay'de persisted status'u `SkippedDuplicate` olarak güncellemez; status `Consumed` kalır.
  - Live flag-on result: `Eventing__RabbitMq__IntegrationTestsEnabled=true dotnet test ... --filter FullyQualifiedName~TenantLifecycle` sonucu 26 PASS / 0 SKIP / 0 FAIL.
  - Broker-down/outbox-pending/recovery için mevcut MOD-0035 live harness kontrollü broker interruption sağlamıyor; Batch 3 raporunda PARTIAL/deferred olarak bırakılır.
  - Audit/notification-specific retry-DLQ için dedicated failure injection harness eklenmedi; mevcut eventing suite generic failing RabbitMQ consumer için retry/error-queue proof'u flag-on altında korur.
  - Full Platform validation: `dotnet test services/Diten.Platform -c Debug` Application suite 284/284 PASS.
- **Batch 4 final reconciliation (2026-05-20):**
  - Module status `approved` -> `review` yapıldı. Core tenant lifecycle MVP flow complete kabul edilir; strict `done` için Batch 3'te kalan MOD-0035 harness edge-case'leri kapatılmalı veya owning hardening pack'e taşınmalı.
  - Batch 1 PASS summary: 6 yeni event contract eklendi; mevcut `TenantActivatedV1` contract'ı untouched kaldı; tenant lifecycle command handler'ları outbox-backed `IEventBus.PublishAsync(...)` üzerinden event emit eder; `AdminEmail` payload'a geri eklenmedi ve `InitialAdminUserId` ID metadata olarak kullanıldı; full Platform suite PASS.
  - Batch 1B DEFERRED summary: provisioning producer emit eklenmedi; reason: mevcut tenant provisioning orchestration handler bulunmadı. Bu pack provisioning orchestration icat etmez.
  - Batch 2 PASS summary: tenant lifecycle audit consumer MOD-0021 public `IAuditService.AppendAsync(...)` abstraction'ı ile PASS; 3 notification mapper eklendi; mapper-only delivery ilk etapta recipient-free ID-only event payload nedeniyle PARTIAL olarak raporlandı.
  - Batch 2.1 PASS summary: tenant lifecycle notification consumer eklendi; recipients `Tenant.AdminUsers` üzerinden resolve edilir; queueing yalnızca `QueueEmailNotificationCommand` ile yapılır; event payload ID-only kaldı ve `AdminEmail` reintroduced edilmedi.
  - Batch 3 core PASS / harness PARTIAL summary: live RabbitMQ publish-consume, audit live proof, notification live proof, idempotent side effects, correlation propagation, redaction ve full Platform suite PASS. Flag false eventing 39 PASS / 3 SKIP; flag true TenantLifecycle live 26 PASS / 0 SKIP; full Platform 284 PASS.
  - Events proven: `TenantCreatedV1`, `TenantActivatedV1`, `TenantSuspendedV1`, `TenantReactivatedV1`, `TenantCancelledV1`, `TenantProvisioningFailedV1`; `TenantProvisioningCompletedV1` current validator kabul ederse live proof'a dahil edilir, aksi halde validator decision'a deferred kalır.
  - Boundary confirmation: MOD-0035 production internals, MOD-0027 notification internals, MOD-0021 audit internals, MOD-0263 provider, frontend, gateway, archive ve `.antigravity` değiştirilmedi. Tenant lifecycle handler'ları direct notification/audit/MassTransit/RabbitMQ call kazanmadı.
  - Master-plan reconciliation yapıldı: MOD-0009 core lifecycle event flow completed / review olarak işaretlendi; recovery/DLQ harness edge cases MOD-0035 hardening'e deferred bırakıldı.

## 20. Follow-up Items

- [ ] **MOD-0035 controlled broker-down/outbox-pending/recovery harness** — broker interruption + recovery test harness'i event bus hardening pack'inde eklenmeli.
- [ ] **MOD-0035 audit/notification-specific retry-DLQ failure injection harness** — tenant lifecycle audit/notification consumer failure simulation + DLQ proof için test harness.
- [ ] **MOD-0035 duplicate consumed status policy** — duplicate replay side effect skip PASS; persisted status `Consumed` kalıyor. `SkippedDuplicate` state zorunluysa `ConsumedEventStore` behavior owner module'de netleştirilmeli.
- [ ] **`TenantProvisioningCompletedV1.Steps` validator decision** — Option A: MOD-0035 payload validator primitive `IReadOnlyList<string>` allow eder; Option B: future MOD-0009 contract amendment ile `StepsCsv` / summary field.
- [ ] **Tenant provisioning producer emit** — gerçek tenant provisioning orchestration handler ortaya çıkana kadar deferred; orchestration bu pack'te icat edilmez.
- [ ] **`ProvisioningRetryJob`** — `TenantProvisioningFailedV1` consume edip MOD-0026 Hangfire üzerine retry kuyruğu (master-plan §"Background Job Catalog" placeholder).
- [ ] **MOD-0297 Subscription Lifecycle consumer** — `TenantCreatedV1` consume edip default trial subscription (MOD-0297 kendi pack'inde subscribe etmeli).
- [ ] **MOD-0298 Entitlement Cache Invalidation consumer** — `TenantCancelledV1` consume edip entitlement cache invalidation (MOD-0298 kendi pack'inde subscribe etmeli).
- [ ] **Operatör DLQ / Replay UI** — Future ops console pack'i; `Platform.EventBus.Replay` permission.
- [ ] **MOD-0038 Event Taxonomy** — Tenant event ailesi taxonomy registry'sine eklenmesi.
- [ ] **MOD-0039 Schema Compatibility Governance** — V1 → V2 breaking change için contract test gate.
- [ ] **Tenant lifecycle handler folder refactor** — `Handlers/` flat → `Handlers/CommandHandlers/QueryHandlers/` Golden Reference ayrımı; ayrı brownfield pack'i.
- [ ] **`tenant.cancelled.email` + `tenant.provisioning.failed.email` template'ları** — MOD-0027 template seed pack'i altında.
- [ ] **SendGrid messaging provider** — MOD-0263 follow-up; bu pack scope'unda değil.
