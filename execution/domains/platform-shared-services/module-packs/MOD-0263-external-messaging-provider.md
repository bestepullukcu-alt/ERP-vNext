---
id: MOD-0263
name: External Messaging Provider
title: External Messaging Provider Adapters (SMTP / SendGrid) for MOD-0027
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: BaseEntity
status: approved
owner: Notification
branch: feature/email-service
started: 2026-05-18
target: 2026-06-15
form_field_count: 0
---

# MOD-0263 - External Messaging Provider

## 1. Module Summary
- **Purpose:** Implement production-ready external messaging provider adapters behind the existing MOD-0027 `IMessagingProvider` boundary so production email can leave the system. MOD-0027 already owns orchestration, templates, queue/dispatch, retry, audit, lifecycle events, and the fake/development provider; MOD-0263 adds only the concrete transport-level adapters.
- **Primary outcome (MVP):** Platform API can send email through SMTP/MailKit in non-development environments. Production no longer depends on `FakeMessagingProvider`.
- **Optional outcome:** SendGrid HTTP API adapter, deferred unless a separate scope/approval explicitly accepts SendGrid SDK or HTTP client dependency, configuration shape, and operational ownership.
- **Tenant rule:** Adapters are tenant-agnostic transport. Tenant context is already resolved by MOD-0027 through `TenantMessagingSettings` / `ResolvedMessagingSettingsDto`; MOD-0263 only receives the resolved per-dispatch settings via the existing `MessagingProviderEmailRequest` and pre-resolved settings DTO.
- **Provider rule:** MOD-0263 does not change `IMessagingProvider`, `IMessagingProviderResolver`, `MessagingProviderEmailRequest`, `MessagingProviderResult`, `NotificationDispatch`, status transitions, audit pipeline, scheduler usage, or event publication.
- **MVP UI decision:** None. No Platform Admin UI, no tenant UI, no DataTable, no menu entry, no Razor view, no RESX.
- **Golden Reference decision:** `golden_reference: none` because this is not a CRUD/DataTable module.
- **Readiness decision:** `status: ready-for-dev`. All eight pre-dev blocker decisions in §1.1 are **Accepted for MVP** as of 2026-05-18. Batch 1 (SMTP/MailKit) may begin.

### 1.1 Pre-dev blocker decisions (Accepted for MVP — 2026-05-18)
| Decision | MVP decision | Blocker status |
|---|---|---|
| SMTP client library | `MailKit` (MimeKit + MailKit) — widely used, async, modern TLS, no `System.Net.Mail.SmtpClient` (Microsoft-deprecated for new code). | **Accepted for MVP.** |
| SendGrid in MVP | **Defer.** Batch 1 ships only SMTP/MailKit. SendGrid adapter is gated to Batch 2 behind a separate amendment that explicitly accepts SDK/transport dependency, configuration shape, and operational ownership. | **Accepted for MVP.** |
| Provider selection mechanism | Keep existing `IMessagingProviderResolver`; register each new provider as `IMessagingProvider` keyed by `MessagingProviderCode` enum value. No new resolver, no factory, no service locator. | **Accepted for MVP.** |
| Secret resolution surface | Use existing `Diten.BuildingBlocks.Security.Secrets.ISecretsProvider.GetSecretAsync(key, ct)` only. SMTP password is resolved from `TenantMessagingSettings.CredentialSecretRef` per send. No raw secret in appsettings, request body, log, audit, event, or persisted dispatch field. | **Accepted for MVP.** |
| Provider observability seam | Use existing MOD-0041 conventions only: `ILogger<T>`, correlation propagation, Serilog structured properties, and any `OpenTelemetry` instrumentation already published by MOD-0041 Batch 1. No new logging sink, no new metrics library, no new tracing instrumentation introduced by MOD-0263. | **Accepted for MVP.** |
| Audit emission | Rely on MOD-0027 lifecycle audit (`MarkNotificationDispatchSentCommand` / `MarkNotificationDispatchFailedCommand` are already `IAuditableCommand` via MOD-0021). MOD-0263 emits no extra audit in MVP unless the existing `IAuditService` seam makes a single redacted append trivial at provider boundary; otherwise defer. No second audit table or writer. | **Accepted for MVP.** |
| Event publication | Rely on MOD-0027 lifecycle events (`NotificationDispatchSentV1` / `NotificationDispatchFailedV1` / `NotificationDispatchCancelledV1`). Provider-level events (e.g. `notifications.provider.config_invalid.v1`) are **deferred**; if ever added, only through MOD-0035 `IEventBus`. | **Accepted for MVP.** |
| Production safety | `FakeMessagingProvider` registration is preserved and remains environment-gated by MOD-0027 Batch 1B. In `Production`, the provider resolver routes only to non-Fake adapters; Fake refusal to run in `Production` is unchanged. | **Accepted for MVP.** |

## 2. Ownership and Boundaries
### In-scope
- Concrete `IMessagingProvider` adapters for `MessagingProviderCode.Smtp` (and optionally `SendGrid` in a later batch).
- SMTP transport implementation through MailKit (or accepted equivalent).
- Provider-specific options (host, port, TLS mode, timeout, sender-domain hint) bound from configuration **at the provider level only**; per-tenant SMTP host/port/sender already live in `TenantMessagingSettings`.
- Credential resolution from `TenantMessagingSettings.CredentialSecretRef` through MOD-0012 `ISecretsProvider`.
- Provider error classification: auth failure, TLS failure, timeout, DNS/connectivity, provider rejection, config invalid.
- Mapping of provider outcomes to `MessagingProviderResult` (`Accepted`, `ProviderMessageId`, `ErrorCode`, redacted `ErrorMessage`).
- Structured logging through MOD-0041 conventions (`ILogger`, correlation id, dispatch id, tenant id, provider code, error code, duration; never secrets/body/recipients).
- Optional health/config probe surface only if existing MOD-0041 health-check pattern in the branch supports it without inventing a new framework.
- Test doubles: in-process SMTP fake/listener for unit/integration tests, no external network dependency for the standard test suite.
- Provider registration in `Diten.Platform.Infrastructure.DependencyInjection`.

### Out-of-scope
- MOD-0027 orchestration: no changes to `QueueEmailNotificationCommand`, dispatch state machine, retry metadata, sweep job, lifecycle event records, audit metadata providers, or any handler under `Features/Notifications/Handlers/**`.
- Template CRUD, template rendering, settings CRUD, dispatch repository methods.
- Retry policy or scheduler: `EmailDispatchJob` and `EmailDispatchSweepJob` already own retry. MOD-0263 returns failure metadata and stops there.
- New event bus or audit system. Direct `RabbitMQ.Client` / MassTransit consumer/producer code inside provider adapters is forbidden.
- New scheduler / hosted service / background loop inside provider adapters.
- New logging/metrics/tracing framework. New Serilog sinks, new Prometheus libraries, new OpenTelemetry instrumentations beyond what MOD-0041 has already declared MVP.
- SMS, push, WhatsApp, Slack, Teams, webhook, or any non-email channel.
- Notification preference filtering (MOD-0287).
- Tenant notification settings UI (deferred MOD-0027 UI pack).
- Migration of existing Auth/Platform invitation/password reset/MFA email senders (tracked in `docs/audits/mod-0027-email-migration-inventory.md` — separate migration pack required).
- Gateway/Ocelot route changes (MOD-0263 introduces no HTTP endpoints).
- `.antigravity/**`, archive controllers/views, frontend Razor/DataTable/RESX, other domain services.

### Ownership rule
- **MOD-0263** owns concrete external messaging adapters, provider-specific options binding, provider-specific error classification, and the SMTP/SendGrid transport behavior.
- **MOD-0027** owns notification orchestration, templates, queue, dispatch, retry business behavior, lifecycle events, the `IMessagingProvider` boundary contract, and the `FakeMessagingProvider`.
- **MOD-0012** owns secret storage and resolution; MOD-0263 only consumes via `ISecretsProvider`.
- **MOD-0026** owns scheduler mechanics; MOD-0263 does not schedule.
- **MOD-0035** owns event bus mechanics; MOD-0263 publishes only through `IEventBus` if/when it publishes at all.
- **MOD-0021** owns audit; MOD-0263 emits only through `IAuditService` if/when it audits at all.
- **MOD-0041** owns logging/monitoring conventions; MOD-0263 follows them and does not redefine them.

## 3. Owned Objects
### Concrete provider adapters
- `SmtpMessagingProvider` — implements `IMessagingProvider` with `ProviderCode => MessagingProviderCode.Smtp`.
- *(optional, batch 2)* `SendGridMessagingProvider` — implements `IMessagingProvider` with `ProviderCode => MessagingProviderCode.SendGrid`.

### Provider options (configuration-bound)
- `SmtpProviderOptions` — transport-level defaults: connect/send timeout, max recipients per message, default TLS mode hint, allowed sender-domain hint (if used). **Does not store** host/port/credentials — those come from per-tenant `TenantMessagingSettings`.
- *(optional)* `SendGridProviderOptions` — HTTP base URL override, timeout, default reply-to policy hint.

### Internal infrastructure types
- `SmtpProviderClientFactory` (or equivalent) — produces a configured MailKit `SmtpClient` per send call; handles disposal; never caches credentials.
- `MessagingProviderErrorMapper` (or equivalent) — maps MailKit/SendGrid exceptions to a small stable `ErrorCode` vocabulary: `ProviderAuthFailed`, `ProviderTlsFailed`, `ProviderTimeout`, `ProviderConnectivityFailed`, `ProviderRejected`, `ProviderConfigInvalid`, `ProviderSecretUnresolved`, `ProviderUnknown`.
- `SecretReferenceResolver` — thin helper around `ISecretsProvider.GetSecretAsync` that enforces "non-empty `CredentialSecretRef` required" and never logs the resolved value.

### Application abstractions
- **None new.** MOD-0263 consumes existing `Diten.Platform.Application.Features.Notifications.Services.IMessagingProvider`, `IMessagingProviderResolver`, `MessagingProviderEmailRequest`, `MessagingProviderResult`.

### Commands / Queries / DTOs / API endpoints / Frontend routes / Permissions
- **None.** MOD-0263 introduces no MediatR commands, no HTTP endpoints, no DTOs, no controllers, no permissions. All HTTP is owned by MOD-0027's existing `NotificationsController`.

## 4. Entity Fields
**No new entities.** Provider credentials remain `TenantMessagingSettings.CredentialSecretRef` (string, max 512, validated by existing MOD-0027 validator). MOD-0263 reads but never persists provider credentials, raw or otherwise.

If a future enhancement persists per-provider validation results (e.g. last successful connect), it must reuse `TenantMessagingSettings.LastValidatedAt` / `ValidationStatus` / `ValidationError` fields owned by MOD-0027; no new repository or entity.

## 5. Repo Scope
- `execution/domains/platform-shared-services/module-packs/MOD-0263-external-messaging-provider.md` (this file)
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/Notifications/`
  - `SmtpMessagingProvider.cs`
  - *(optional)* `SendGridMessagingProvider.cs`
  - `SmtpProviderClientFactory.cs` (or equivalent helper)
  - `MessagingProviderErrorMapper.cs` (or equivalent helper)
  - `SecretReferenceResolver.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Settings/`
  - `SmtpProviderOptions.cs`
  - *(optional)* `SendGridProviderOptions.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/DependencyInjection.cs`
  - Register `SmtpMessagingProvider` as `IMessagingProvider` (additive only; `FakeMessagingProvider` registration remains).
  - Bind `SmtpProviderOptions` from configuration.
- `services/Diten.Platform/src/Diten.Platform.API/appsettings.json`, `appsettings.Development.json`
  - Add `MessagingProviders:Smtp:*` configuration **schema only** (no real credentials checked in). Local development continues to use `Fake` provider.
- `services/Diten.Platform.Common/**`
  - Only if a shared cross-service error-code constant set is needed. Not expected in MVP.
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Notifications/`
  - `NotificationsProviderTests.cs` (or `MessagingProviderSmtpTests.cs`) — adapter tests using an in-process fake SMTP listener or fully mocked transport.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Diten.Platform.Infrastructure.csproj`
  - `PackageReference Include="MailKit"` (or accepted equivalent).

## 6. Protected Paths
- `.antigravity/**`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Notifications/**` — MOD-0027 orchestration; **do not modify** any handler, command, query, validator, lifecycle event publishing, or `INotificationEventMapper<T>` seam.
- `services/Diten.Platform/src/Diten.Platform.Domain/Entities/Notifications/**` — entities and state machine owned by MOD-0027.
- `services/Diten.Platform/src/Diten.Platform.Domain/Repositories/INotificationDispatchRepository.cs` and Mongo implementation — owned by MOD-0027.
- `services/Diten.Platform/src/Diten.Platform.Application/BackgroundJobs/**` — scheduler/registrar; owned by MOD-0026/MOD-0027.
- `services/Diten.Platform/src/Diten.Platform.Application/Services/Eventing/EventBus.cs` and `Diten.BuildingBlocks.Eventing/**` — MOD-0035 internals.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/**` and `IAuditService` implementation — MOD-0021 internals.
- `services/Diten.Building.Blocks/Diten.BuildingBlocks.Security.Secrets/**` — MOD-0012 internals. Consume `ISecretsProvider` only.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/Notifications/FakeMessagingProvider.cs` — kept as-is; MOD-0263 does not modify or remove the fake provider.
- `services/Diten.AuthService/**` — existing Auth invitation/MFA/password-reset email senders are migration candidates tracked in the migration inventory; MOD-0263 does not migrate them.
- `services/Diten.MdmService/**`, `services/Diten.EnterpriseStrategyService/**`, `services/Diten.DevEnablementService/**` — other domains.
- `gateway/Diten.ApiGateway/**/ocelot.json` — no route changes (MOD-0263 has no HTTP endpoints).
- `frontend/Diten.Web/**` — no UI changes.
- `archive/**` and any `Controllers/Archive`, `Views/Archive` paths.

## 7. Dependencies
- **MOD-0012 Secrets & Configuration Vault** — `Diten.BuildingBlocks.Security.Secrets.ISecretsProvider`. SMTP password / SendGrid API key resolved through `GetSecretAsync(CredentialSecretRef, ct)`. Raw secret values are never logged, persisted, or returned. `ISecretRedactor` is reused for any log/audit redaction need.
- **MOD-0027 Central Tenant Email / Notification Service** — entire orchestration pipeline. MOD-0263 plugs into the existing `IMessagingProvider` boundary and `IMessagingProviderResolver`. No contract changes.
- **MOD-0026 Background Job Scheduler** — used indirectly: `EmailDispatchJob` (per-dispatch) and `EmailDispatchSweepJob` (recurring) call provider adapters. MOD-0263 does not schedule.
- **MOD-0035 Event Bus / Internal Events** — used indirectly: MOD-0027 publishes `NotificationDispatchSentV1` / `NotificationDispatchFailedV1` / `NotificationDispatchCancelledV1` already. MOD-0263 publishes additional provider-level events only if MVP scope is explicitly expanded.
- **MOD-0021 General Audit Trail** — used indirectly: MOD-0027 `MarkNotificationDispatchSentCommand`/`MarkNotificationDispatchFailedCommand` are already `IAuditableCommand`. MOD-0263 emits additional audit only if MVP scope is explicitly expanded.
- **MOD-0041 Logging / Monitoring** — `ILogger<T>` conventions, correlation propagation, redaction. No new sinks.
- **External package:** `MailKit` (and transitively `MimeKit`). License: MIT. Trim/AoT compatibility is not a concern for Platform API.

## 8. Runtime Constraints
- `Diten.Platform.API` host runs provider adapters in-process; no out-of-process SMTP daemon.
- Provider adapters are **stateless and request-scoped or singleton**. They MUST NOT cache credentials across requests. Credentials are resolved per send call via `ISecretsProvider`.
- Per-send timeout is bounded by `SmtpProviderOptions.SendTimeoutSeconds` (proposed default: 30s) plus a hard ceiling enforced via `CancellationToken`. A provider hang must not stall the job worker.
- All exceptions thrown by MailKit (or equivalent) are caught inside `SmtpMessagingProvider.SendEmailAsync` and mapped to `MessagingProviderResult.Fail(errorCode, redactedMessage)`. The job worker continues; this is already the MOD-0027 contract for provider failures.
- Provider credentials are secret references only; **no raw secret** appears in logs, traces, metrics labels, audit metadata, exception messages, or any persisted field.
- Recipient addresses, full rendered body, full subject MAY appear in adapter memory transiently for the duration of the send call only. They MUST NOT be logged in full. Only counts and dispatch id appear in logs.
- TLS: SMTP connections use `SecureSocketOptions.StartTlsWhenAvailable` by default; `UseSsl=false` allowed only when `TenantMessagingSettings.UseSsl=false` is explicitly set and (recommended) `appsettings` flag permits insecure dev SMTP. Production behavior must NOT downgrade silently.
- DNS / network errors must be classified as `ProviderConnectivityFailed`, not as `ProviderUnknown`.
- Authentication errors must be classified as `ProviderAuthFailed`.
- TLS handshake/cert errors must be classified as `ProviderTlsFailed`.
- Timeouts (transport-level, configured-level, cancellation-level) must be classified as `ProviderTimeout`.
- SMTP 4xx/5xx responses other than auth/TLS map to `ProviderRejected` with the numeric response code preserved as part of `ErrorCode` only if it is a stable SMTP code (e.g. `ProviderRejected:550`); free-form server text is redacted out of `ErrorMessage`.
- `ProviderMessageId`: for SMTP, use the message-id MailKit generates and report it back so downstream tracing can correlate. For SendGrid (optional batch), use the SendGrid `x-message-id` header.
- Concurrent sends are safe (no static mutable state in the adapter).
- **Options validation (startup gate):** `SmtpProviderOptions` MUST be validated at startup, not lazily. Use either the existing options-validation pattern in `Diten.Platform.Infrastructure` (e.g. `ValidateOnStart()` + `IValidateOptions<SmtpProviderOptions>` implementation) or, if no existing pattern is reused, register a dedicated `IValidateOptions<SmtpProviderOptions>` implementation alongside the `services.Configure<SmtpProviderOptions>(...)` call. The validator MUST fail Platform API startup (not first-send) when any of the following are true: `SendTimeoutSeconds <= 0` or `> 300`; `MaxRecipientsPerMessage <= 0` or `> 1000`; `AllowInsecureTlsInDevelopment == true` while `IHostEnvironment.IsProduction()`. Production must never boot with insecure TLS configuration.

## 9. Layout & Shell Contract
- `shell: none`.
- No Razor layout, no `_LayoutPlatformAdmin`, no `_LayoutTenantShell`. No view files of any kind.
- No Platform Admin menu entry.
- If a future Platform Admin "Provider health / test connection" UI is approved, it belongs to a separate pack and must use `Layout = "_LayoutPlatformAdmin"` explicitly. MOD-0263 itself does not introduce it.

## 10. Backend File Convention
This is a backend infrastructure/adapter module, not a CQRS feature module. Golden Reference folder convention does NOT apply (`golden_reference: none`).

### Expected folder
```text
services/Diten.Platform/src/Diten.Platform.Infrastructure/
├── Services/Notifications/
│   ├── FakeMessagingProvider.cs              (OWNED BY MOD-0027 — do not modify)
│   ├── SmtpMessagingProvider.cs              (NEW)
│   ├── SmtpProviderClientFactory.cs          (NEW, optional helper)
│   ├── MessagingProviderErrorMapper.cs       (NEW)
│   ├── SecretReferenceResolver.cs            (NEW)
│   └── SendGridMessagingProvider.cs          (NEW, batch 2 optional)
├── Settings/
│   ├── FakeMessagingProviderOptions.cs       (OWNED BY MOD-0027 — do not modify)
│   ├── SmtpProviderOptions.cs                (NEW)
│   └── SendGridProviderOptions.cs            (NEW, batch 2 optional)
└── DependencyInjection.cs                    (MODIFY: additive registration only)
```

### Naming rules
- Each provider adapter class is `{Provider}MessagingProvider` (e.g. `SmtpMessagingProvider`).
- Each options class is `{Provider}ProviderOptions` with a `public const string SectionName = "MessagingProviders:{Provider}"`.
- One public class per file. No multi-class files except `MessagingProviderErrorMapper` may collocate its small `ErrorCode` constants record/enum if it stays under 200 LOC total.
- Adapter constructor takes: `IOptions<{Provider}ProviderOptions>`, `ISecretsProvider`, `ISecretRedactor`, `ILogger<{Provider}MessagingProvider>`, and (optionally) `IHostEnvironment` only if production-specific guards are needed at the adapter level (MOD-0027 already gates `FakeMessagingProvider` by environment).
- Adapter MUST NOT take `IMediator`, `IEventBus`, `IAuditService`, `IBackgroundJobScheduler` in MVP. Those are for orchestration layers, not transport adapters.

### Forbidden
- Direct `RabbitMQ.Client` / `MassTransit` references inside any file under `Services/Notifications/`.
- Direct `Hangfire.*` references inside provider files.
- `IHostedService` or background loop inside provider files.
- Caching credentials in static fields, singleton dictionaries, or process-level memory beyond one send call.
- Logging at any level with the raw `CredentialSecretRef` resolved value, raw secret, full body, full subject, full recipient list, or MailKit/SendGrid raw response payload.

## 11. Frontend File Contract
- **No frontend files in scope.**
- No `Index.cshtml`, `_Filter.cshtml`, `_DataTable.cshtml`, `_IndexL10n.cshtml`, JS, or RESX files are created.
- No menu/navigation changes.
- No `wwwroot/assets` changes.
- No `frontend/Diten.Web/**` changes.
- If a future "Test connection" Platform Admin UI is approved, it belongs to a separate UI pack and follows Slim/Compact decision per Golden Reference standard.

## 12. Validation Rules
| Field | Required | Format/Rule | DB-level | Pre-check |
|---|---|---|---|---|
| `SmtpProviderOptions.SectionName` | n/a | const `"MessagingProviders:Smtp"` | — | binds via `services.Configure<SmtpProviderOptions>(configuration.GetSection(SmtpProviderOptions.SectionName))` |
| `SmtpProviderOptions.SendTimeoutSeconds` | Yes | int, 1..300, default 30 | — | reject value <= 0 at startup |
| `SmtpProviderOptions.AllowInsecureTlsInDevelopment` | Yes | bool, default false | — | must be false in `Production`; startup throws if true while `IHostEnvironment.IsProduction()` |
| `SmtpProviderOptions.MaxRecipientsPerMessage` | Yes | int, 1..1000, default 100 | — | reject value <= 0 at startup |
| `SmtpProviderOptions` (whole) | Yes | Validated by `IValidateOptions<SmtpProviderOptions>` with `ValidateOnStart()` (or equivalent existing options-validation pattern) | — | Startup fails if `SendTimeoutSeconds` out of range, `MaxRecipientsPerMessage` out of range, or `AllowInsecureTlsInDevelopment=true` in `Production`. |
| `TenantMessagingSettings.Host` | per MOD-0027 validator (already enforced) | hostname only, no scheme, no credentials embedded | — | adapter re-checks non-empty before connecting |
| `TenantMessagingSettings.Port` | per MOD-0027 validator (already enforced) | 1..65535 | — | adapter re-checks range before connecting |
| `TenantMessagingSettings.UseSsl` | per MOD-0027 (bool) | true → SSL/TLS implicit; false + present `STARTTLS` capability → STARTTLS upgrade required unless `AllowInsecureTlsInDevelopment` and `IHostEnvironment.IsDevelopment()` | — | adapter re-checks |
| `TenantMessagingSettings.CredentialSecretRef` | Yes for SMTP | non-empty string, max 512, format already validated by MOD-0027 (no raw secret) | — | adapter rejects send with `ErrorCode=ProviderConfigInvalid` if empty/null |
| `TenantMessagingSettings.SenderEmail` | per MOD-0027 validator | valid email, max 256, normalized lowercase | — | adapter uses verbatim |
| `MessagingProviderEmailRequest.To` | per MOD-0027 validator | at least one recipient | — | adapter re-checks count > 0 |
| `(To + Cc + Bcc).Count` | Yes | <= `SmtpProviderOptions.MaxRecipientsPerMessage` | — | adapter returns `ProviderRejected:RecipientLimit` if exceeded; provider not called |
| Resolved secret value | n/a | non-empty after `ISecretsProvider.GetSecretAsync` | — | adapter returns `ProviderSecretUnresolved` if empty/null; raw value never logged |

Adapter-level validation is a thin re-check; it does not duplicate or replace MOD-0027's FluentValidation in `QueueEmailNotificationValidator` and `TenantMessagingSettingsUpsertValidator`. Its role is defensive: catching cases where settings are technically valid but operationally broken for this transport (e.g. SMTP-only field missing).

## 13. Failure Path to Verify
- **Missing `CredentialSecretRef`**
  - Expected: provider returns `MessagingProviderResult.Fail("ProviderConfigInvalid", "Credential reference is missing.")`. MailKit is never constructed. MOD-0027 transitions dispatch to `Failed` with `RetryCount += 1` and `NextRetryAt` set by job; sweep eventually retries (still fails until config fixed). Log includes `ErrorCode`, `DispatchId`, `TenantId`, `ProviderCode`. No raw secret reference value beyond the opaque key.
- **Secret resolution failure** (`ISecretsProvider` throws or returns empty)
  - Expected: provider catches, returns `Fail("ProviderSecretUnresolved", "Secret could not be resolved.")`. No raw secret value logged. No stack trace persisted. Dispatch transitions to `Failed`.
- **SMTP authentication failure** (e.g. MailKit `AuthenticationException`)
  - Expected: `Fail("ProviderAuthFailed", "<redacted>")`. Server text passed through `ISecretRedactor` before logging. Dispatch transitions to `Failed`.
- **TLS handshake / certificate failure** (MailKit `SslHandshakeException`, certificate validation error)
  - Expected: `Fail("ProviderTlsFailed", "<redacted>")`. No raw certificate details in audit. Dispatch transitions to `Failed`.
- **Connect timeout / send timeout / cancellation**
  - Expected: `Fail("ProviderTimeout", "Operation timed out.")`. Worker is not blocked beyond the configured timeout. Dispatch transitions to `Failed`.
- **DNS or TCP connectivity failure**
  - Expected: `Fail("ProviderConnectivityFailed", "<redacted>")`. No hostname/IP in logs beyond what `TenantMessagingSettings.Host` already carries. Dispatch transitions to `Failed`.
- **SMTP server rejection** (4xx / 5xx response)
  - Expected: `Fail("ProviderRejected" or "ProviderRejected:<code>", "<redacted>")`. If the SMTP code is a well-known stable code (`421`, `450`, `451`, `452`, `530`, `535`, `550`, `552`, `553`), append it to `ErrorCode`. Free-form server text is dropped. Dispatch transitions to `Failed`.
- **Recipient limit exceeded** (To+Cc+Bcc > options max)
  - Expected: `Fail("ProviderRejected:RecipientLimit", "Recipient limit exceeded.")`. MailKit is never invoked. Dispatch transitions to `Failed`.
- **Raw secret in logs (test gate)**
  - Expected: any test that scans captured log output for known secret values, password substrings, `Bearer ...`, SMTP `AUTH` payloads, or full body strings FAILS the build if found.
- **Provider unavailable for selected `ProviderCode`** (e.g. SendGrid provider not registered while `ProviderCode=SendGrid`)
  - Expected: existing `IMessagingProviderResolver` returns `Response<IMessagingProvider>.Fail(...)`; MOD-0027 already handles this. MOD-0263 only ensures registrations are correctly wired.
- **Fake provider in Production**
  - Expected: out of MOD-0263 scope — already enforced by MOD-0027 Batch 1B. Test gate stays in MOD-0027 test suite.
- **Concurrency / multiple workers sending same dispatch**
  - Expected: out of MOD-0263 scope — `EmailDispatchJob` uses `TryMarkSent`/`TryMarkFailed` state machine which rejects double transitions with 409.

## 14. Authorization Convention
- **No new HTTP endpoints**, therefore no new policies and no new `[HasPermission(...)]` attributes.
- Existing `Platform.Notifications.Dispatches.Queue` permission (MOD-0027) continues to gate `POST /api/platform/notifications/email/queue` regardless of which `IMessagingProvider` adapter MOD-0263 ships.
- Future "Test connection" or "Send synthetic test email" admin endpoints, if approved, MUST live in a separate pack with permission `Platform.Notifications.Configure` and `[Authorize(Policy = "PlatformActor")]` and MUST NOT be added by MOD-0263.

## 15. Gateway / API Routing Decision
- **Decision: Gateway change NOT required.**
- MOD-0263 introduces zero HTTP endpoints. All existing `/api/platform/notifications/**` routes are already owned by MOD-0027 and already (or already not) registered in `gateway/Diten.ApiGateway/**/ocelot.json` as needed.
- This pack author does not edit `gateway/Diten.ApiGateway/**/ocelot.json`. If a future "test connection" admin endpoint is added by a separate UI pack, that pack creates an integration-agent task; MOD-0263 does not.

## 16. Acceptance Criteria
- [ ] `SmtpMessagingProvider` is registered as `IMessagingProvider` keyed by `MessagingProviderCode.Smtp`; `IMessagingProviderResolver.Resolve(MessagingProviderCode.Smtp)` returns success.
- [ ] `FakeMessagingProvider` registration is preserved unchanged and `IMessagingProviderResolver.Resolve(MessagingProviderCode.Fake)` continues to work.
- [ ] Selecting `ProviderCode=Smtp` on a tenant's `TenantMessagingSettings` causes `QueueEmailNotificationHandler` to route through `SmtpMessagingProvider` end-to-end (verified by integration test using an in-process SMTP fake).
- [ ] On send success, `MessagingProviderResult.Success(providerMessageId)` is returned with a non-empty `ProviderMessageId`; MOD-0027 transitions dispatch to `Sent` and publishes `NotificationDispatchSentV1` (already covered by MOD-0027 tests).
- [ ] All 8 failure categories in §13 produce the correct `ErrorCode` value, the provider does not throw out of the adapter, and the dispatch transitions to `Failed`.
- [ ] `ISecretsProvider.GetSecretAsync` is the only path used to materialize the SMTP password; raw password is never present in any captured log, exception message persisted in `NotificationDispatch.ErrorMessage`, audit metadata, event payload, or test output.
- [ ] No file under `services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/Notifications/` references `RabbitMQ.Client`, `MassTransit`, or `Hangfire.*` (verified by reflective or grep-based test).
- [ ] No file under that folder declares an `IHostedService` or background loop.
- [ ] `SmtpProviderOptions.AllowInsecureTlsInDevelopment=true` combined with `IHostEnvironment.EnvironmentName="Production"` causes Platform API startup to throw a clear configuration error.
- [ ] Recipient count exceeding `SmtpProviderOptions.MaxRecipientsPerMessage` returns `ProviderRejected:RecipientLimit` without calling MailKit.
- [ ] SendGrid is either: (a) explicitly deferred to Batch 2 with no `SendGridMessagingProvider` file shipped, or (b) implemented with the same failure-path and redaction guarantees.
- [ ] Existing MOD-0027 notification tests (Batch 1A + 1B + 2) remain green: 42/42 notification tests pass. Full Platform suite remains green at or above the baseline before MOD-0263.
- [ ] No file under `services/Diten.Platform/src/Diten.Platform.Application/Features/Notifications/**` is modified by MOD-0263.
- [ ] No file under `services/Diten.Platform/src/Diten.Platform.Domain/Entities/Notifications/**` is modified.
- [ ] No frontend, gateway, archive, `.antigravity`, or cross-domain service path is modified.
- [ ] Logs emitted from `SmtpMessagingProvider` contain only safe metadata: `ProviderCode`, `DispatchId`, `TenantId`, `CorrelationId`, `Status`, `ErrorCode`, `DurationMs`. They do not contain recipient lists, body content, subject lines beyond the first 80 chars (optionally redacted), provider raw response, or secret values.
- [ ] Migration of existing Auth/Platform ad-hoc email senders (A1/A2/P1/P2 in `docs/audits/mod-0027-email-migration-inventory.md`) is **NOT** performed by MOD-0263 and remains owned by a future migration pack.

## 17. Test Expectations
### Build
- `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug`
- Adding `MailKit` package must build cleanly with no new warnings introduced by MOD-0263 files.

### Test strategy (binding)
- **Unit and integration tests use a mocked SMTP transport / client factory only.** No live network, no external SMTP container, no `localhost:25` dependency in CI. Implement an abstraction seam (e.g. `ISmtpClientFactory` returning an interface a test can stub, or an injected `IMailTransport` from MailKit) so MailKit's `SmtpClient` can be replaced with a fake that returns scripted responses (success, auth failure, TLS failure, timeout, connectivity failure, SMTP 4xx/5xx).
- **`smtp4dev` / `MailHog` / `Papercut` are reserved for manual smoke proof only** (a developer runs the catcher locally, queues an email, and visually verifies delivery + redaction). They MUST NOT be wired into the automated test suite, MUST NOT be required for CI green, and MUST NOT appear in `Diten.Platform.Application.Tests.csproj` as a test dependency.
- Reflective assertion: a unit test scans `services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/Notifications/` for MailKit `SmtpClient` direct construction; only the client factory may instantiate it.

### Unit tests
- `SmtpMessagingProvider_ShouldReturnFail_WhenCredentialSecretRefMissing`
- `SmtpMessagingProvider_ShouldReturnFail_WhenSecretsProviderThrows`
- `SmtpMessagingProvider_ShouldReturnFail_WhenSecretsProviderReturnsEmpty`
- `SmtpMessagingProvider_ShouldClassifyAuthFailure`
- `SmtpMessagingProvider_ShouldClassifyTlsFailure`
- `SmtpMessagingProvider_ShouldClassifyTimeout`
- `SmtpMessagingProvider_ShouldClassifyConnectivityFailure`
- `SmtpMessagingProvider_ShouldClassifySmtpRejectionWithStableCode`
- `SmtpMessagingProvider_ShouldRejectExcessiveRecipientCount_WithoutInvokingTransport`
- `SmtpMessagingProvider_ShouldReturnAccepted_WithProviderMessageId_OnSuccess` (in-process fake transport)
- `SmtpMessagingProvider_ShouldNeverLogRawSecret_OrFullBody_OrFullRecipientList` (log capture assertion)
- `SmtpMessagingProvider_ShouldNeverReferenceRabbitMqOrMassTransitOrHangfire` (reflective assembly scan, mirroring MOD-0027's pattern)
- `SmtpProviderOptions_ShouldThrowAtStartup_WhenAllowInsecureTlsInDevelopmentTrueInProduction`

### Integration tests
- `QueueEmail_EndToEnd_WithSmtpProvider_AndMockedTransport_ShouldReachSent` — drives through `QueueEmailNotificationCommand`; provider resolver picks `Smtp`; the mocked transport returns a synthetic `ProviderMessageId`; assertion: dispatch ends `Sent`, `ProviderMessageId` non-empty, `NotificationDispatchSentV1` recorded by recording event bus. **No external SMTP server is used.**
- `QueueEmail_WithBrokenSmtp_ShouldReachFailed_WithRedactedErrorMetadata` — mocked transport throws scripted auth failure; assertion: dispatch `Failed`, `ErrorCode=ProviderAuthFailed`, `ErrorMessage` is redacted, retry sweep eventually re-enqueues per MOD-0027 contract (covered indirectly).
- Tenant A cannot use Tenant B's settings — already covered by MOD-0027; integration test re-runs to prove MOD-0263 did not regress.

### Smoke / manual proof (developer-driven, NOT in CI)
- Start Platform API locally and start a local SMTP catcher chosen by the developer (e.g. `smtp4dev`, `MailHog`, or `Papercut`). Configure a tenant `TenantMessagingSettings` row pointing `Host`/`Port` at the catcher and `ProviderCode=Smtp`.
- Queue a test email via `POST /api/platform/notifications/email/queue` (Platform Admin actor + `Platform.Notifications.Dispatches.Queue` permission).
- Verify the email lands in the catcher; verify `NotificationDispatch` is `Sent`; verify logs include correlation id, dispatch id, status; verify logs do NOT include password, body, full recipient dump, or MailKit raw response.
- Repeat with deliberately broken credentials; verify dispatch `Failed`, redacted `ErrorMessage`, no raw secret in any log line or in the `NotificationDispatch.ErrorMessage` MongoDB document.
- This smoke flow is a **manual gate** before merging to main; it is not added to the automated test suite.

### Validation commands (final)
- `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug`
- `dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests/Diten.Platform.Application.Tests.csproj -c Debug --filter FullyQualifiedName~Notifications`
- `dotnet test services/Diten.Platform -c Debug`

### Frontend / RESX / DataTable
- Not applicable. No frontend, no DataTable, no RESX.

## 18. Ready-for-dev Checklist
- [x] User accepted MailKit as the SMTP client library (2026-05-18).
- [x] User accepted SendGrid defer-to-Batch-2 (2026-05-18); Batch 1 ships SMTP/MailKit only.
- [x] User accepted that MOD-0263 introduces no new HTTP endpoints, no permissions, no policies, no UI, no menu entries.
- [x] User accepted that MOD-0263 introduces no new entities, no new dispatch fields, no new repositories, no new commands/queries/handlers.
- [x] User accepted that MOD-0263 introduces no new event bus, no new audit system, no new scheduler, no new logging/monitoring framework.
- [x] User accepted that provider credentials are resolved only through `ISecretsProvider` and never persisted/serialized/logged in raw form.
- [x] User accepted that all 8 failure categories (auth, TLS, timeout, connectivity, rejection, config invalid, secret unresolved, unknown) must be classified and redacted.
- [x] User accepted that adapter logs are limited to: `ProviderCode`, `DispatchId`, `TenantId`, `CorrelationId`, `Status`, `ErrorCode`, `DurationMs`.
- [x] User accepted that `FakeMessagingProvider` registration remains untouched and continues to be environment-gated by MOD-0027 Batch 1B.
- [x] User accepted that Auth/Platform invitation/MFA/password-reset migration is NOT in scope and remains tracked in the existing migration inventory.
- [x] User accepted that `IValidateOptions<SmtpProviderOptions>` (or equivalent existing options-validation pattern) fails startup on invalid configuration, including `AllowInsecureTlsInDevelopment=true` in `Production`.
- [x] User accepted that automated tests use mocked SMTP transport only; smtp4dev/MailHog/Papercut are reserved for manual smoke proof and are not added to CI.
- [x] `golden_reference: none` is accepted because no DataTable UI is included.
- [x] `shell: none` is accepted because no Razor UI is included.
- [x] `entity_base: BaseEntity` is accepted as a placeholder; no new entities are actually declared.
- [x] Gateway routing decision (no change) is recorded.
- [x] Status changed from `draft` to `ready-for-dev` (2026-05-18).

## 19. Implementation Batches
### Batch 1 — SMTP/MailKit adapter (MVP, ready-for-dev)
**In-scope (Batch 1 only):**
- Add `MailKit` package reference to `Diten.Platform.Infrastructure.csproj`.
- Create `SmtpProviderOptions` + `IValidateOptions<SmtpProviderOptions>` (or equivalent existing options-validation pattern) wired with `ValidateOnStart()`.
- Create `SecretReferenceResolver` (thin wrapper over `ISecretsProvider`; never logs resolved value).
- Create `MessagingProviderErrorMapper` (MailKit exception → stable `ErrorCode` vocabulary).
- Create `SmtpMessagingProvider : IMessagingProvider` with `ProviderCode => MessagingProviderCode.Smtp`.
- Create an `ISmtpClientFactory` (or equivalent transport seam) so MailKit's `SmtpClient` is replaceable in tests; production binding produces the real `SmtpClient` per send and disposes it.
- Register `SmtpMessagingProvider` as an additional `IMessagingProvider` in `Diten.Platform.Infrastructure.DependencyInjection` (additive only — do not touch `FakeMessagingProvider` registration).
- Bind `SmtpProviderOptions` from configuration; register validator with `ValidateOnStart`.
- Add `MessagingProviders:Smtp:*` schema (NOT real credentials) to `appsettings.json` / `appsettings.Development.json`; development keeps tenant default at `ProviderCode=Fake`.
- Add unit tests covering: all 8 failure classifications, redaction guarantee, recipient-limit guard, no direct MailKit `SmtpClient` construction outside the factory, no `MassTransit` / `RabbitMQ.Client` / `Hangfire.*` references inside the new files, options-validation startup failure for insecure prod TLS.
- Add one integration test driving `QueueEmailNotificationCommand` end-to-end through `SmtpMessagingProvider` with the mocked transport returning a synthetic `ProviderMessageId`.
- Confirm full Platform test suite remains green (≥ 241/241 baseline at MOD-0027 closure).

**Out of Batch 1 (explicitly deferred):**
- SendGrid adapter, SendGrid options, SendGrid tests.
- Provider-level events on `IEventBus`.
- Extra provider-level audit on `IAuditService` beyond what MOD-0027 already emits.
- Health-check surface.
- Any migration of Auth/Platform invitation/MFA/password-reset flows.

### Batch 2 — SendGrid HTTP adapter (optional, gated, NOT ready-for-dev)
- Only proceed after a separate user-approved amendment lists: (a) accepted SDK or `HttpClient` library, (b) accepted configuration shape, (c) accepted operational ownership, (d) accepted egress / API key rotation policy.
- Same failure-classification and redaction guarantees as Batch 1.
- Same test strategy: mocked `HttpMessageHandler` for HTTP responses; no live SendGrid network calls in CI.

### Batch 3 — Provider health surface (optional, gated)
- Only if MOD-0041 exposes a public health-check seam at the time MOD-0263 reaches this batch. If MOD-0041 has not landed the health-check standard, defer and document `BLOCKED: MOD-0041 health-check seam not available`.
- Optional `IHealthCheck` for SMTP connectivity (no actual auth) under a per-tenant or per-options key; consumed by existing `/health/ready` endpoint.
- No new health framework, no new endpoint, no new options binding outside MOD-0041 conventions.

### Batch 4 — Migration enablement (NOT executed by this pack)
- Hand-off to a dedicated migration pack covering A1/A2/P1/P2 from `docs/audits/mod-0027-email-migration-inventory.md`.
- MOD-0263 does not perform any migration.

## 20. Follow-up Items
- [x] Confirmed with user (2026-05-18): MailKit accepted as SMTP client library.
- [x] Confirmed with user (2026-05-18): SendGrid deferred to Batch 2.
- [ ] After MVP ships, schedule a security review focused on log/audit redaction proof and the "no secret in any captured artifact" assertion.
- [ ] If MOD-0041 Batch 2/3 lands OpenTelemetry tracing for outbound network calls, evaluate whether `SmtpMessagingProvider` should add `Activity` instrumentation — no work until that seam is public.
- [ ] If MOD-0035 stabilizes a `notifications.provider.*` event family, MOD-0263 may optionally publish provider-level events. Until then, rely on MOD-0027 lifecycle events.
- [ ] Track outbound port (25/465/587) policy for production network egress with ops; documentation only — not a code task.
- [ ] After Batch 1 ships, kick off the migration pack for A1 (Platform admin password reset) and A2 (tenant MFA OTP) per inventory recommendations M1/M2/M3.
- [ ] If Auth invitation/MFA email migration eventually moves into MOD-0027, revisit whether MOD-0263's `SmtpMessagingProvider` needs per-tenant connection pooling. Not in MVP.
