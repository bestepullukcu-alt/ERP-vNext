---
id: MOD-0027
name: Central Tenant Email / Notification Service
title: Tenant-aware Central Email and Notification Service
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: BaseEntity
status: approved
owner: Notification
branch: feature/email-service
started: 2026-05-18
target: 2026-06-05
form_field_count: 0
---

# MOD-0027 - Central Tenant Email / Notification Service

## 1. Module Summary
- **Purpose:** Provide the central tenant-aware notification and email orchestration foundation for ERP-vNext.
- **Primary outcome:** Platform and future tenant ERP modules can request templated email notifications through one application service instead of using ad-hoc email services.
- **Tenant rule:** Each tenant may have its own email settings. If tenant settings are missing, the module uses a deterministic platform-default fallback policy.
- **Provider rule:** Concrete external providers such as SMTP, SendGrid, or future SMS/WhatsApp adapters are owned by MOD-0263 External Messaging Provider. MOD-0027 owns orchestration, templates, dispatch records, retry metadata, and the provider abstraction boundary.
- **MVP UI decision:** No custom Platform Admin or tenant UI in this first pack. Configuration APIs and service contracts are backend-first.
- **Golden Reference decision:** `golden_reference: none` because this is not a CRUD/DataTable frontend module.
- **Readiness decision:** `status: ready-for-dev` is approved for Batch 1A because the pre-dev blocker decisions below are accepted for MVP.

### Pre-dev blocker decisions
These decisions are accepted for MVP and are binding for Batch 1A/1B implementation.

| Decision | Recommended MVP | Blocker status |
|---|---|---|
| Platform default settings/template storage strategy | Use an explicit platform/global record for default templates/settings. Do not rely on a normal tenant-owned record masquerading as platform defaults. | Accepted for MVP |
| Target tenant id rules | Platform Admin HTTP APIs may use `/tenant-settings/{tenantId}` as a target tenant id only for `PlatformActor` plus required `Platform.Notifications.*` permission. Request bodies must never accept `TenantId`. | Accepted for MVP |
| Fake provider behavior | Fake provider is allowed only for development/test/smoke and must be opt-in by environment/configuration. Production must not silently fall back to fake provider. | Accepted for MVP |
| Queue-only vs queue-and-send behavior | Batch 1 should queue and immediately call the fake provider in non-production smoke flow so the dispatch can reach `Queued` or `Sent`; real provider send/retry remains behind MOD-0263/MOD-0026. | Accepted for MVP |
| MOD-0263 provider boundary confirmation | MOD-0027 owns orchestration and the minimal provider boundary; concrete SMTP/SendGrid/MailKit adapters are deferred to MOD-0263. | Accepted for MVP |

## 2. Ownership and Boundaries
### In-scope
- Tenant-aware email settings model and resolution rules.
- Platform-default fallback decision for missing tenant settings.
- Central notification/email application abstraction.
- Template-based email rendering.
- Notification dispatch persistence and status tracking.
- Retry metadata needed by MOD-0026 scheduled dispatch/retry jobs.
- Secret reference handling for provider credentials, with MOD-0012 as the secure storage dependency.
- Fake/development provider path for local tests and non-production smoke tests.
- Migration target for existing invitation email services, documented but not fully migrated unless explicitly approved in implementation scope.

### Out-of-scope
- Full external provider implementation beyond a fake/development provider. SMTP/SendGrid/MailKit adapters belong to MOD-0263.
- SMS, push, WhatsApp, Slack, Teams, or webhook delivery channels.
- Notification preference management. That belongs to MOD-0287.
- Webhook delivery. That belongs to MOD-0034.
- Alerting and incident channels. Those belong to MOD-0042.
- Custom Platform Admin UI for templates/settings.
- Tenant shell self-service UI for email settings.
- Gateway route edits by this pack author. Route edits, if required, are integration-agent work.
- Changes to `.antigravity/**`, archive views/controllers, or other domain services.

### Ownership rule
- MOD-0027 owns notification orchestration, template rendering, dispatch status, and tenant email configuration resolution.
- MOD-0263 owns provider adapters and external messaging transport behavior.
- MOD-0026 owns background scheduling mechanics; MOD-0027 owns the `EmailDispatchJob` business behavior when that job is implemented.
- MOD-0035 owns event bus mechanics; MOD-0027 consumes events through public event abstractions only.
- MOD-0012 owns secret storage and rotation; MOD-0027 stores secret references, not raw secrets.

## 3. Owned Objects
### Domain entities / value objects
- `TenantMessagingSettings`
- `NotificationTemplate`
- `NotificationDispatch`
- `EmailRecipient`
- `EmailMessage`

### Enums
- `MessagingProviderCode`: `Fake`, `Smtp`, `SendGrid`
- `NotificationChannelCode`: `Email`
- `NotificationDispatchStatus`: `Pending`, `Queued`, `Sent`, `Failed`, `Cancelled`
- `NotificationTemplateStatus`: `Draft`, `Active`, `Archived`
- `TemplateVariableType`: `String`, `Number`, `Boolean`, `Date`, `Url`

### Application abstractions
- `INotificationService`
- `IEmailTemplateRenderer`
- `ITenantMessagingSettingsResolver`
- `INotificationDispatchWriter`
- `IMessagingProvider` as a boundary interface, unless implementation determines this must move fully to MOD-0263. If moved, this pack must keep an explicit dependency and adapter contract.

### Commands
- `UpsertTenantMessagingSettingsCommand`
- `DeleteTenantMessagingSettingsCommand`
- `CreateNotificationTemplateCommand`
- `UpdateNotificationTemplateCommand`
- `ArchiveNotificationTemplateCommand`
- `QueueEmailNotificationCommand`
- `MarkNotificationDispatchSentCommand`
- `MarkNotificationDispatchFailedCommand`
- `CancelNotificationDispatchCommand`

### Queries
- `GetTenantMessagingSettingsQuery`
- `GetResolvedTenantMessagingSettingsQuery`
- `GetNotificationTemplateByKeyQuery`
- `GetNotificationDispatchByIdQuery`
- `GetNotificationDispatchListQuery`

### DTOs / models
- `TenantMessagingSettingsDto`
- `TenantMessagingSettingsUpsertRequest`
- `ResolvedMessagingSettingsDto`
- `NotificationTemplateDto`
- `NotificationTemplateUpsertRequest`
- `QueueEmailNotificationRequest`
- `NotificationDispatchDto`
- `NotificationDispatchListItemDto`
- `EmailRecipientDto`
- `EmailMessageDto`
- `TemplateVariableDefinitionDto`

### API endpoints
- `GET /api/platform/notifications/tenant-settings/{tenantId}`
- `PUT /api/platform/notifications/tenant-settings/{tenantId}`
- `DELETE /api/platform/notifications/tenant-settings/{tenantId}`
- `GET /api/platform/notifications/tenant-settings/{tenantId}/resolved`
- `GET /api/platform/notifications/templates`
- `GET /api/platform/notifications/templates/{templateKey}`
- `POST /api/platform/notifications/templates`
- `PUT /api/platform/notifications/templates/{id}`
- `POST /api/platform/notifications/templates/{id}/archive`
- `GET /api/platform/notifications/dispatches`
- `GET /api/platform/notifications/dispatches/{id}`
- `POST /api/platform/notifications/email/queue`

### Tenant targeting contract
- Request bodies and DTOs must never expose or accept `TenantId`.
- For Platform Admin APIs, route segment `{tenantId}` means **target tenant id**, not caller tenant context.
- Target tenant id routes are allowed only for `PlatformActor` and the required `Platform.Notifications.*` permission.
- Future tenant-side APIs must resolve `TenantId` from the tenant context and must not use route `{tenantId}` unless a separate approved tenant pack defines that route.
- Cross-tenant access to tenant-owned settings, templates, and dispatches must return `404 Not Found`.

### Permissions
- `Platform.Notifications.Read`
- `Platform.Notifications.Configure`
- `Platform.Notifications.Templates.Read`
- `Platform.Notifications.Templates.Create`
- `Platform.Notifications.Templates.Update`
- `Platform.Notifications.Templates.Archive`
- `Platform.Notifications.Dispatches.Read`
- `Platform.Notifications.Dispatches.Queue`
- Future tenant-side permissions, if tenant self-service UI is later approved:
  - `Modules.NotificationSettings.Read`
  - `Modules.NotificationSettings.Update`

## 4. Entity Fields
### TenantMessagingSettings
| Field | Type | Required | Rule |
|---|---|---|---|
| Base | `BaseEntity` | Yes | Tenant-owned Platform record. Includes Id, TenantId, IsDeleted, DeletedAt, CreatedAt, UpdatedAt, and concurrency field. |
| ProviderCode | `MessagingProviderCode` | Yes | `Fake`, `Smtp`, or `SendGrid`; concrete non-fake providers depend on MOD-0263. |
| SenderEmail | `string` | Yes | Valid email; max 256; normalized lowercase for comparison. |
| SenderName | `string` | No | Max 160; trimmed. |
| ReplyToEmail | `string?` | No | Valid email when supplied; max 256. |
| Host | `string?` | Conditional | Required for SMTP provider; max 256; no credentials embedded. |
| Port | `int?` | Conditional | Required for SMTP provider; 1-65535. |
| UseSsl | `bool` | Yes | Defaults to true for SMTP unless provider contract says otherwise. |
| ApiBaseUrl | `string?` | Conditional | Absolute URL for API-based providers; no API key in URL. |
| CredentialSecretRef | `string?` | Conditional | Reference to MOD-0012 secret. Raw password/API key is forbidden. |
| IsEnabled | `bool` | Yes | Disabled settings cannot send; resolver may fallback only when fallback policy allows. |
| FallbackPolicy | `string` | Yes | `UsePlatformDefault`, `DisableSending`, or `FailFast`. |
| LastValidatedAt | `DateTimeOffset?` | No | UTC timestamp from config validation. |
| ValidationStatus | `string?` | No | `Unknown`, `Valid`, `Invalid`; max 32. |
| ValidationError | `string?` | No | Redacted; max 1000. |

### Platform default settings/templates
Recommended MVP storage: explicit platform/global record for default settings and templates.

| Option | Description | MVP decision |
|---|---|---|
| Platform tenant context record | Store defaults under a reserved platform tenant id. | Not recommended: can blur tenant-owned and platform-global records unless the platform tenant model is already canonical. |
| Explicit global record | Store defaults as records marked as platform/global defaults and not owned by a business tenant. | Recommended MVP. Use for platform default templates/settings with clear repository filters. |
| Configuration-backed default | Store fallback defaults in app configuration only. | Not recommended for templates/settings that need lifecycle, audit, and query support. |

Resolver order must be deterministic:
1. Tenant-specific active settings/template.
2. Platform default active settings/template.
3. Controlled failure according to `FallbackPolicy`.

Production must never silently use the fake provider when tenant-specific or platform-default provider configuration is missing.

### NotificationTemplate
| Field | Type | Required | Rule |
|---|---|---|---|
| Base | `BaseEntity` | Yes | Tenant-owned when customized per tenant; platform-default templates use explicit platform/global records in the recommended MVP. |
| TemplateKey | `string` | Yes | Stable key, e.g. `tenant.invite.email`; max 160; lowercase dotted format. |
| Channel | `NotificationChannelCode` | Yes | MVP supports `Email` only. |
| Locale | `string` | Yes | BCP-47-like code; platform defaults start with `en` and `tr`. |
| SubjectTemplate | `string` | Yes | Max 300; rendered with validated variables. |
| BodyHtmlTemplate | `string` | Yes | Sanitized template source; max 100000. |
| BodyTextTemplate | `string?` | No | Plain-text fallback; max 100000. |
| Variables | `IReadOnlyList<TemplateVariableDefinition>` | Yes | Required variables and types. |
| Status | `NotificationTemplateStatus` | Yes | Only `Active` templates can be used for normal dispatch. |
| SemanticVersion | `string?` | No | Business template version; `Version` field name is forbidden. |

### NotificationDispatch
| Field | Type | Required | Rule |
|---|---|---|---|
| Base | `BaseEntity` | Yes | Tenant-owned dispatch record. TenantId is server-side resolved. |
| TemplateKey | `string` | Yes | Stable template key; max 160; indexed. |
| TemplateId | `Guid?` | No | Template selected at queue time. |
| Locale | `string` | Yes | Locale resolved at queue time. |
| Channel | `NotificationChannelCode` | Yes | MVP supports `Email` only. |
| ProviderCode | `MessagingProviderCode` | Yes | Provider selected at dispatch time. |
| ProviderMessageId | `string?` | No | External provider id; max 256; indexed when present. |
| Status | `NotificationDispatchStatus` | Yes | Valid transitions only. |
| To | `IReadOnlyList<EmailRecipient>` | Yes | At least one recipient. |
| Cc | `IReadOnlyList<EmailRecipient>` | No | Optional; total recipient count limit applies. |
| Bcc | `IReadOnlyList<EmailRecipient>` | No | Optional; never exposed in normal read models except secure audit/admin detail if approved. |
| Subject | `string` | Yes | Rendered subject; max 300. |
| BodyHtml | `string?` | Conditional | Rendered HTML; redacted/truncated in logs. |
| BodyText | `string?` | Conditional | Rendered text fallback. |
| VariablesJson | `string` | Yes | Small sanitized variable snapshot for debugging; secrets forbidden. |
| QueuedAt | `DateTimeOffset` | Yes | UTC timestamp. |
| SentAt | `DateTimeOffset?` | No | UTC timestamp when provider accepted the message. |
| FailedAt | `DateTimeOffset?` | No | UTC timestamp for final/current failure. |
| RetryCount | `int` | Yes | Non-negative. |
| NextRetryAt | `DateTimeOffset?` | No | UTC timestamp, owned by retry policy. |
| ErrorCode | `string?` | No | Redacted provider error code; max 128. |
| ErrorMessage | `string?` | No | Redacted; max 2000. |
| CorrelationId | `string?` | No | Propagated from request/event/job context. |
| CausationId | `Guid?` | No | Source event or command id when available. |

### EmailRecipient
| Field | Type | Required | Rule |
|---|---|---|---|
| Email | `string` | Yes | Valid email; max 256; normalized lowercase for comparison. |
| DisplayName | `string?` | No | Max 160; trimmed. |

## 5. Repo Scope
- `execution/domains/platform-shared-services/module-packs/MOD-0027-central-tenant-email-notification-service.md`
- `services/Diten.Platform/src/Diten.Platform.Domain/**`
  - Entities, value objects, enums, repository interfaces.
- `services/Diten.Platform/src/Diten.Platform.Application/**`
  - Features/Notifications commands, queries, handlers, validators, models.
  - Application abstractions for notification orchestration and template rendering.
- `services/Diten.Platform/src/Diten.Platform.Persistence/**`
  - Mongo repositories, collection configuration, indexes.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/**`
  - Fake/development messaging provider, template renderer implementation, secret resolver adapter.
  - External provider adapter implementations only if MOD-0263 is approved or this pack is explicitly amended.
- `services/Diten.Platform/src/Diten.Platform.API/**`
  - Platform notifications controller, DI registration, options binding.
- `services/Diten.Platform.Common/**`
  - Only if shared cross-service notification contracts are needed for future ERP modules.
- `frontend/Diten.Web/**`
  - Out of MVP scope. Only future separate UI pack may add Platform Admin or tenant shell screens.
- `gateway/Diten.ApiGateway/**`
  - Only through integration-agent if explicit routes are required.

## 6. Protected Paths
- `.antigravity/**`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- `gateway/Diten.ApiGateway/**/ocelot.json` unless a separate integration-agent task is approved.
- `services/Diten.MdmService/**`
- `services/Diten.EnterpriseStrategyService/**`
- `services/Diten.DevEnablementService/**`
- `services/Diten.AuthService/**` unless a later migration task explicitly moves Auth/invitation email calls to MOD-0027.
- Existing MOD-0026 scheduler internals, except through public job abstractions.
- Existing MOD-0035 event bus internals, except through public event abstractions.
- MOD-0012 secret provider internals, except through public secret abstraction.

## 7. Dependencies
- **MOD-0012 Secrets & Configuration Vault:** Provider credentials must be stored as secret references. Raw passwords/API keys are never persisted in notification entities or returned from APIs.
- **MOD-0026 Background Job Scheduler:** Email dispatch retry and periodic pending-dispatch processing use the scheduler's public job abstraction. `EmailDispatchJob` business logic belongs to MOD-0027.
- **MOD-0035 Event Bus / Internal Events:** Event-driven notifications consume public event abstractions only. No direct RabbitMQ/MassTransit calls from notification handlers.
- **MOD-0263 External Messaging Provider:** Owns SMTP/SendGrid/MailKit and future external provider adapters. MOD-0027 may provide a fake/development provider and a stable provider interface boundary.
- **MOD-0021 General Audit Trail:** Settings/template changes and dispatch queue actions should emit audit metadata when audit hooks are available.
- **MOD-0287 User Notification Preferences:** Future preference filtering must happen before dispatch but is not part of this pack.
- **MOD-0041 Logging / Monitoring:** Dispatch and provider errors must use structured, redacted logs and correlation metadata.
- **PSS-011 Lookups / Reference Data:** No lookup UI is introduced in this pack. If provider-code or locale dropdown UI is later added, it must consume `/api/lookups/...` through Gateway/proxy and must not use hardcoded fallback lists.

## 8. Runtime Constraints
- `Diten.Platform` is the system-of-record for notification orchestration in this pack.
- Tenant-scoped records use `BaseEntity`; `TenantId` is server-side resolved and never accepted in request bodies.
- Platform-default templates/settings use explicit platform/global records in the recommended MVP; user acceptance is required before coding.
- Platform Admin route `{tenantId}` values are target tenant ids and require `PlatformActor` plus the applicable `Platform.Notifications.*` permission.
- Tenant-side future APIs resolve `TenantId` from tenant context and must not introduce route tenant ids without a separate approved tenant pack.
- Cross-tenant reads/writes must return `404 Not Found` for tenant-owned records that do not belong to the active tenant context.
- Platform admin APIs use `[Authorize(Policy = "PlatformActor")]`.
- API responses use `Response<T>` envelope and `CustomBaseController`.
- Handlers use MediatR/CQRS and return `Response<T>`.
- FluentValidation runs through the existing validation pipeline.
- Soft delete uses `IsDeleted` and `DeletedAt`.
- Update/delete commands use concurrency where existing Platform base/repository conventions support it.
- Provider credentials are secret references only; no raw secret is stored, logged, indexed, serialized, or returned.
- Logs must not include full email bodies, full recipient lists, API keys, passwords, tokens, connection strings, or rendered payload dumps.
- MVP dispatch storage decision:
  - Do not store full rendered email body by default.
  - Store subject, metadata, template key, template id, variables snapshot, provider status, redacted error metadata, and optionally a truncated/sanitized preview.
  - Full body storage requires explicit later approval due to privacy, logging, and audit risk.
- A failed provider call must not crash the request/job process; it writes a failed dispatch state with redacted error metadata.
- Future tenant ERP modules call the central notification abstraction or API through Gateway/service client patterns; they do not call provider adapters directly.

## 9. Golden Flow
Platform Admin or a test harness creates tenant messaging settings using the fake provider, then creates an active email notification template. The caller queues an email notification with all required variables. The template renders successfully, and `NotificationDispatch` is persisted with server-side `TenantId`, correlation id, provider code, and `Queued` or `Sent` fake-provider status according to the selected queue/send behavior. Reload/query returns the same dispatch. Logs contain status and correlation metadata, but do not contain raw secrets, full email body, full rendered payload, or a full recipient dump.

## 10. Audit and Event Seams
MOD-0027 must emit audit through the MOD-0021 public abstraction for settings upsert/delete, template create/update/archive, email queued, and dispatch sent/failed/cancelled when the abstraction exists in the branch. If the MOD-0021 abstraction is unavailable, the implementation report must mark audit as `BLOCKED`/deferred with the exact missing contract. Do not implement a second audit system.

Event bus publication remains owned by MOD-0035 public abstractions. If MOD-0035 is not ready in the implementation branch, event bus mapping is explicitly deferred and the command handlers must keep local audit metadata ready for later mapping.

### Expected audit events
| Operation | Event name / seam |
|---|---|
| Tenant messaging settings upsert | `notifications.tenant_messaging_settings.upserted` through MOD-0021 audit seam; MOD-0035 mapping deferred if unavailable. |
| Tenant messaging settings delete | `notifications.tenant_messaging_settings.deleted` through MOD-0021 audit seam; MOD-0035 mapping deferred if unavailable. |
| Notification template create | `notifications.template.created` through MOD-0021 audit seam; MOD-0035 mapping deferred if unavailable. |
| Notification template update | `notifications.template.updated` through MOD-0021 audit seam; MOD-0035 mapping deferred if unavailable. |
| Notification template archive | `notifications.template.archived` through MOD-0021 audit seam; MOD-0035 mapping deferred if unavailable. |
| Email notification queued | `notifications.email.queued` through MOD-0021 audit seam and MOD-0035 public abstraction when ready. |
| Dispatch marked sent | `notifications.dispatch.sent` through MOD-0021 audit seam and MOD-0035 public abstraction when ready. |
| Dispatch marked failed | `notifications.dispatch.failed` through MOD-0021 audit seam and MOD-0035 public abstraction when ready. |
| Dispatch cancelled | `notifications.dispatch.cancelled` through MOD-0021 audit seam and MOD-0035 public abstraction when ready. |

## 11. Layout & Shell Contract
- `shell: none`.
- No Razor layout is required in this MVP pack.
- No `Diten.Web` view is created.
- No Platform Admin menu entry is created.
- If a future Platform Admin settings/template UI is approved, it must use `Layout = "_LayoutPlatformAdmin"` explicitly.
- If a future tenant self-service settings UI is approved, it must use `Layout = "_LayoutTenantShell"` explicitly and be introduced through a separate tenant module pack.

## 12. Backend File Convention
This is a backend foundation module, not a Golden Reference CRUD/DataTable module. `golden_reference: none` is intentional.

### Expected feature folder
```text
services/Diten.Platform/src/Diten.Platform.Application/Features/Notifications/
├── Commands/
│   ├── UpsertTenantMessagingSettingsCommand.cs
│   ├── DeleteTenantMessagingSettingsCommand.cs
│   ├── CreateNotificationTemplateCommand.cs
│   ├── UpdateNotificationTemplateCommand.cs
│   ├── ArchiveNotificationTemplateCommand.cs
│   ├── QueueEmailNotificationCommand.cs
│   ├── MarkNotificationDispatchSentCommand.cs
│   ├── MarkNotificationDispatchFailedCommand.cs
│   └── CancelNotificationDispatchCommand.cs
├── Queries/
│   ├── GetTenantMessagingSettingsQuery.cs
│   ├── GetResolvedTenantMessagingSettingsQuery.cs
│   ├── GetNotificationTemplateByKeyQuery.cs
│   ├── GetNotificationDispatchByIdQuery.cs
│   └── GetNotificationDispatchListQuery.cs
├── Handlers/
│   ├── CommandHandlers/
│   └── QueryHandlers/
├── Validators/
└── NotificationModels.cs
```

### Naming rules
- Command records use `{Verb}{ModuleOrEntity}Command`.
- Query records use `Get{ModuleOrEntity}{Qualifier}Query`.
- Handler classes use `{Verb}{ModuleOrEntity}Handler`; `CommandHandler`/`QueryHandler` suffix is forbidden.
- Validator classes use `{Verb}{ModuleOrEntity}Validator`; `Command` suffix is forbidden.
- One public class/record per file, except `NotificationModels.cs` DTO grouping.
- Controller remains thin and delegates to MediatR.
- Provider calls are isolated behind application/infrastructure abstractions; handlers do not send email directly.

## 13. Frontend File Contract
- No frontend files are in scope for this pack.
- No DataTable v2 verifier applies.
- No `Index.cshtml`, `_Filter.cshtml`, `_DataTable.cshtml`, `_IndexL10n.cshtml`, JS, or RESX files are created.
- No Platform Admin or tenant shell navigation is changed.
- If UI is later approved:
  - Platform UI localization is `en` + `tr`.
  - Tenant UI localization is `en`, `fr`, `es`, `zh`, `ar`, `ru`, `tr`.
  - Golden Reference Slim/Compact must be selected by create/edit user form field count at that time.

## 14. Validation Rules
| Field | Required | Format/Rule | DB-level | Pre-check |
|---|---|---|---|---|
| TenantId | Server-side | GUID from tenant context or platform operation target; not accepted in body | Tenant index | Reject body/query TenantId usage. |
| ProviderCode | Yes | Known enum; fake allowed for development/test | Indexed with TenantId | Non-fake providers require MOD-0263 readiness. |
| SenderEmail | Yes | Valid email, lowercase normalized, max 256 | Optional unique per tenant/provider | Validate format and domain. |
| SenderName | No | Trim, max 160 | — | Empty string becomes null. |
| ReplyToEmail | No | Valid email when supplied, max 256 | — | Validate format. |
| Host | Conditional | Required for SMTP; max 256; hostname only | — | Required when ProviderCode=`Smtp`. |
| Port | Conditional | 1-65535 | — | Required when ProviderCode=`Smtp`. |
| ApiBaseUrl | Conditional | Absolute HTTP/HTTPS URL | — | Required for API-based provider if SendGrid adapter is approved. |
| CredentialSecretRef | Conditional | Secret reference string, max 512; no raw secret | Indexed optional | Resolve through MOD-0012 public abstraction before send. |
| FallbackPolicy | Yes | `UsePlatformDefault`, `DisableSending`, `FailFast` | — | Must be deterministic. |
| TemplateKey | Yes | Lowercase dotted key, max 160 | Unique with TenantId or global scope + Locale + Channel + IsDeleted | Check duplicate active template. |
| Locale | Yes | Supported locale code | Indexed | Platform defaults at least `en`, `tr`. |
| SubjectTemplate | Yes | Max 300 after trim | — | Validate variables. |
| BodyHtmlTemplate | Conditional | Required if no text body; max 100000 | — | Validate variables and disallow unsafe template operations. |
| BodyTextTemplate | Conditional | Required if no HTML body; max 100000 | — | Validate variables. |
| Variables | Yes | Names are alphanumeric/dot/underscore; required flags explicit | — | Missing variable definitions fail template save. |
| Recipient.Email | Yes | Valid email, max 256 | — | At least one `To` recipient. |
| Recipient.DisplayName | No | Trim, max 160 | — | Empty string becomes null. |
| Dispatch.Status | Yes | Known enum transition only | Indexed | Invalid transition rejected. |
| RetryCount | Yes | `>= 0`; implementation-owned | Indexed optional | Client cannot set directly. |
| ErrorMessage | No | Redacted, max 2000 | — | Strip secrets/tokens/payloads. |
| CorrelationId | No | Safe string; max 128 | Indexed optional | Use ambient correlation when available. |

## 15. Failure Path
- **Required concise failure flow:** Queue email with a missing required template variable or invalid recipient. The request fails with controlled `Response<T>.Fail(...)` and HTTP `400`. The provider is not called. No queued dispatch is created, unless the implementation explicitly selects failed-dispatch persistence for validation failures; if selected, the dispatch must be persisted as `Failed` with redacted reason metadata, never `Queued`. Logs remain redacted.
- **Missing tenant settings**
  - Expected: resolver applies `FallbackPolicy`. If platform default is available and policy allows fallback, queue/send continues with default settings. Otherwise the operation fails with `400` or a controlled failed dispatch state.
- **Missing platform default when fallback is required**
  - Expected: fail fast with a clear `Response<T>.Fail(...)`; no fake provider is silently used in production.
- **Raw secret submitted**
  - Expected: validation rejects payload containing password/API key fields or secret-like values outside `CredentialSecretRef`.
- **Secret reference cannot be resolved**
  - Expected: dispatch becomes `Failed` with redacted error metadata; no raw secret appears in logs or API response.
- **Template key duplicate**
  - Expected: `409 Conflict`; no second active template for same TenantId+Locale+Channel+TemplateKey.
- **Template variable missing at queue time**
  - Expected: `400 Bad Request`; dispatch is not queued unless implementation chooses to persist a failed dispatch with explicit reason.
- **Invalid email recipient**
  - Expected: `400 Bad Request`; provider is not called.
- **Provider unavailable**
  - Expected: dispatch moves to `Failed` or remains retryable according to retry policy; platform does not crash.
- **Cross-tenant settings or dispatch access**
  - Expected: `404 Not Found`; tenant A cannot read/update tenant B records.
- **Unauthorized actor**
  - Expected: `401/403`; platform notification APIs are not accessible without PlatformActor and required permission.
- **Concurrency conflict on settings/template update**
  - Expected: `409 Conflict`; no silent overwrite.
- **Payload logging attempt**
  - Expected: tests/review fail if full body, full recipient list, secret, token, or provider payload is logged.

## 16. Authorization Convention
- Platform controller policy: `[Authorize(Policy = "PlatformActor")]`.
- Permission format: `Platform.Notifications.{Action}`.
- Permissions:
  - `Platform.Notifications.Read`
  - `Platform.Notifications.Configure`
  - `Platform.Notifications.Templates.Read`
  - `Platform.Notifications.Templates.Create`
  - `Platform.Notifications.Templates.Update`
  - `Platform.Notifications.Templates.Archive`
  - `Platform.Notifications.Dispatches.Read`
  - `Platform.Notifications.Dispatches.Queue`
- Platform admin actor:
  - `actor_type=platform_admin` passes all platform permissions according to existing platform rule.
- Partner admin behavior:
  - If partner-admin access is introduced, it must be scoped to tenants owned/managed by that partner and documented before implementation.
- Tenant-side future UI/API:
  - Must use tenant actor authorization and `Modules.NotificationSettings.*` permissions in a separate approved pack.
- Internal service usage:
  - Future ERP services must call via approved service client/event contract and must not bypass tenant or entitlement checks.

## 17. Gateway / API Routing Decision
- Decision: Gateway route change is **likely required** only if the Platform API exposes new `/api/platform/notifications/...` endpoints that are called through Gateway or frontend proxy.
- This pack author does not edit `gateway/Diten.ApiGateway/**/ocelot.json`.
- If routes are required, create an integration-agent task:
  - Add explicit upstream/downstream pairs for `/api/platform/notifications` and `/api/platform/notifications/{everything}`.
  - Downstream service: Diten.Platform API on port `5057`.
  - Include `GET`, `POST`, `PUT`, `PATCH`, `DELETE`, and `OPTIONS` as applicable.
  - Keep explicit routes before catch-all routes.
- Frontend, if later added, must call Gateway or same-origin MVC proxy. It must never call service port `5057` directly.
- No Gateway route is needed for internal-only service contracts until an HTTP API consumer exists.

## 18. Acceptance Criteria
- [ ] `TenantMessagingSettings` is tenant-scoped with server-side `TenantId`; request/DTO payloads do not accept `TenantId`.
- [ ] Platform Admin `{tenantId}` route parameters are treated only as target tenant ids and require `PlatformActor` plus `Platform.Notifications.*` permission checks.
- [ ] Tenant messaging settings can be created, updated, read, soft-deleted, and resolved through CQRS handlers.
- [ ] Tenant settings resolution supports deterministic fallback to platform default or controlled failure based on `FallbackPolicy`.
- [ ] Platform default fallback uses the accepted storage strategy and deterministic resolver order: tenant-specific active settings, platform default active settings, then controlled failure.
- [ ] `NotificationTemplate` supports key, locale, channel, subject, HTML/text body, variable definitions, active/archive status, and duplicate prevention.
- [ ] Duplicate active template for the same TenantId/global-scope + Locale + Channel + TemplateKey is rejected with `409 Conflict`.
- [ ] Template rendering validates required variables before dispatch queueing.
- [ ] Missing required template variables produce a controlled validation error.
- [ ] `NotificationDispatch` records queued, sent, failed, cancelled, retry count, next retry time, correlation id, provider code, and redacted error metadata.
- [ ] Dispatch status transitions are explicit and invalid transitions are rejected with a controlled `Response<T>.Fail(...)` result.
- [ ] Test/development environment can use a fake provider without external SMTP/SendGrid credentials, and fake-provider smoke proves `Queued`/`Sent` behavior.
- [ ] Fake provider is never used as a silent production fallback.
- [ ] Non-fake provider implementation is either behind MOD-0263 or explicitly documented as deferred.
- [ ] Provider credentials are stored only as MOD-0012 secret references.
- [ ] Raw provider secrets in request payloads are rejected; only secret reference values are accepted.
- [ ] API responses mask credential references and never return raw secrets.
- [ ] Logs do not include raw secrets, tokens, connection strings, full email bodies, full recipient dumps, or full rendered payloads; redaction proof is captured in smoke/test output.
- [ ] Cross-tenant access to settings/templates/dispatches returns `404`.
- [ ] Platform APIs require `[Authorize(Policy = "PlatformActor")]` and the concrete `Platform.Notifications.*` permissions listed in this pack.
- [ ] Existing ad-hoc invitation email services are identified as migration targets in implementation notes; full migration can be deferred unless explicitly added to implementation scope.
- [ ] `EmailDispatchJob` dependency on MOD-0026 is documented and does not implement a second scheduler.
- [ ] Event-driven notification dependency on MOD-0035 is documented and does not implement a second event bus.
- [ ] Audit/event seams for settings, templates, queue, sent, failed, and cancelled transitions are emitted through public abstractions or explicitly marked as deferred MOD-0035 mappings.
- [ ] Gateway routing decision is recorded; any Ocelot change is delegated to integration-agent.
- [ ] No frontend UI, DataTable, menu item, or Razor layout change is introduced in this pack.

## 19. Test Expectations
### Build
- `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug`
- `dotnet build services/Diten.Platform.Common/src/Diten.Platform.Common/Diten.Platform.Common.csproj -c Debug` if shared contracts are added there.
- `dotnet build gateway/Diten.ApiGateway/Diten.ApiGateway.csproj -c Debug` only if integration-agent route changes are approved.

### Unit tests
- Template render succeeds with all required variables.
- Template render fails when a required variable is missing.
- Template render rejects unknown/unsafe template operations.
- Tenant settings resolver uses tenant-specific settings when present.
- Tenant settings resolver falls back to platform default when allowed.
- Tenant settings resolver fails deterministically when fallback is disabled or missing.
- Secret masking removes raw password/API key/token-like values from DTOs/log metadata.
- Raw secret payload submission is rejected.
- Dispatch status transition rules accept valid transitions and reject invalid transitions.
- Provider failure creates a failed dispatch result with redacted error metadata.
- Fake provider records an accepted test dispatch without external network dependency.
- Queue validation failure does not call the provider and does not create a `Queued` dispatch.
- Platform default resolver order is tenant-specific active settings, platform default active settings, then controlled failure.
- Audit/event seam mapping emits expected event names or records explicit deferred MOD-0035 mapping.

### Integration tests
- Tenant A cannot read/update Tenant B messaging settings.
- Tenant A cannot read Tenant B dispatch record.
- Duplicate active template key for same TenantId+Locale+Channel returns `409`.
- PlatformActor without required permission receives `403` where existing permission behavior supports it.
- Anonymous request receives `401`.
- Soft-deleted settings/templates do not appear in normal queries.
- Queue email endpoint creates a `Pending` or `Queued` dispatch with server-side TenantId.
- Queue email endpoint rejects request-body `TenantId`.
- Platform Admin target tenant routes require `PlatformActor` and the relevant `Platform.Notifications.*` permission.
- Secret reference resolution failure does not leak raw secret or stack trace.

### Smoke / manual proof
- Start Platform API with fake provider enabled.
- Queue a test email through API.
- Verify `NotificationDispatch` was persisted with `Queued`/`Sent` fake-provider status.
- Verify response uses `Response<T>` envelope.
- Verify logs contain correlation/status metadata but no full body, credentials, or provider payload.
- Verify logs do not contain full recipient dumps.
- If Gateway routes are added, call through Gateway `5000`; do not call service port `5057` from frontend/browser JS.

### Frontend / RESX / DataTable
- Not applicable for this backend foundation pack.
- If UI is added later, create/update a separate pack section with GoldenReferenceSlim/Compact decision, RESX parity checks, browser smoke, and DataTable verifier expectations.

## 20. Ready-for-dev Checklist
- [x] User reviewed this pack and confirmed backend-foundation scope.
- [x] Status changed from `draft` to `ready-for-dev`.
- [ ] `golden_reference: none` is accepted because no DataTable UI is included.
- [ ] `shell: none` is accepted because no Razor UI is included.
- [ ] `entity_base: BaseEntity` is accepted for tenant-owned Platform records.
- [x] Platform default settings/template persistence strategy is selected; recommended MVP is explicit platform/global record.
- [x] Target tenant routing rule is accepted: Platform Admin `{tenantId}` route means target tenant id only, with no request-body `TenantId`.
- [x] Fake provider scope is accepted: development/test/smoke only, no silent production fallback.
- [x] Queue/send behavior is selected: queue-and-send fake-provider smoke for non-production Batch 1B.
- [x] Audit/event seam is documented and accepted, including deferred MOD-0035 mapping if event bus is not ready.
- [x] Implementation batches are accepted.
- [x] MOD-0263 boundary is confirmed: concrete SMTP/SendGrid provider implementation deferred unless explicitly amended.
- [ ] MOD-0012 dependency is confirmed: raw provider secrets must not be stored.
- [ ] MOD-0026 dependency is confirmed: email retry/dispatch job uses existing scheduler abstraction.
- [ ] MOD-0035 dependency is confirmed: event-driven notifications use existing event bus abstraction.
- [ ] Backend feature folder/naming follows command/query/handler/validator separation.
- [ ] Validation Rules cover tenant settings, templates, recipients, and dispatch transitions.
- [ ] Failure Path includes missing fallback, missing template variables, invalid recipients, raw secret, cross-tenant, unauthorized, provider failure, and concurrency.
- [ ] Authorization Convention includes concrete `Platform.Notifications.*` permission list.
- [ ] Gateway routing decision is explicit and integration-agent-owned.
- [ ] No frontend UI work is included in this implementation.
- [ ] Test expectations include build, unit, integration, and fake-provider smoke checks.

## 21. Implementation Batches
### Batch 1A - Backend foundation and resolver/template proof
- Entities and enums.
- Repository interfaces.
- Mongo collection mappings and indexes.
- Tenant messaging settings resolver.
- Accepted platform/global default strategy.
- Template entity and render validation tests.

### Batch 1B - Queue, dispatch, fake-provider proof
- `QueueEmailNotificationCommand` and validation failure handling.
- Dispatch persistence with server-side `TenantId`, provider code, correlation id, and status.
- Fake provider for development/test/smoke only.
- Fake-provider smoke.
- Redaction proof.
- Tenant isolation and authorization tests.

### Batch 2 - Integrations and migration inventory
- Retry job integration through MOD-0026 public abstraction only.
- Event-driven notification mapping through MOD-0035 public abstraction only, or explicit deferred event bus mapping if MOD-0035 is not ready.
- Existing invitation email migration inventory only; no full migration unless this pack is amended or a migration pack is approved.

### Batch 3 - Deferred UI
- Optional Platform Admin template/settings UI belongs in a separate pack.
- No frontend, DataTable, Razor layout, menu, or RESX work is part of this pack.

## 22. Implementation Notes
- This pack intentionally treats central email as notification orchestration, not a low-level SMTP adapter.
- Existing ad-hoc invite services should be inventoried during implementation. Candidate migration targets include platform administrator invitations and tenant invitation/welcome flows.
- Migration of existing services should happen only after the central service proves fake-provider dispatch and template rendering.
- `IMessagingProvider` may live in MOD-0027 as a minimal abstraction boundary, but concrete provider packages/adapters should be implemented by MOD-0263 unless the user explicitly expands this pack.
- A future `NotificationTemplate Management UI` pack can add Platform Admin CRUD and then must choose Slim/Compact based on actual form field count.
- A future tenant self-service settings pack can expose tenant email settings in `_LayoutTenantShell` and must include seven-language localization.
- Dispatch records may contain rendered bodies only if the implementation team accepts storage, privacy, and redaction implications. Metadata-only or truncated body snapshots are preferred.
- Do not mark this module done from class/file creation alone. Completion requires tenant isolation, fake-provider smoke, template render tests, and redaction proof.
- No code was written outside module-pack preparation.

## 23. Follow-up Items
- [ ] After approval, run an implementation pre-audit for existing email/invitation services and list migration candidates.
- [ ] Prepare or approve MOD-0263 External Messaging Provider pack for SMTP/SendGrid adapter implementation.
- [ ] Decide platform default settings storage strategy before coding.
- [ ] Decide whether initial implementation queues only or also sends synchronously through fake provider.
- [ ] Define `EmailDispatchJob` implementation scope after MOD-0026 scheduler abstractions are confirmed in the current branch.
- [ ] Define event-to-notification mapping after MOD-0035 live broker validation and event naming are stable.
- [ ] Prepare future Platform Admin template/settings UI pack if operational users need configuration screens.
- [ ] Prepare future tenant notification preferences pack under MOD-0287.
- [ ] Add master-plan reconciliation note after implementation begins from `ready-for-dev`.

## Output Contract
Implementation final report must include:
- Module status: PASS / PARTIAL / FAIL / BLOCKED
- Changed files
- Provider boundary decision: fake only, MOD-0263 adapter, or expanded provider scope
- Platform default fallback strategy
- Secret handling proof
- Tenant isolation proof
- Template render proof
- Dispatch status proof
- Fake-provider smoke proof
- Gateway route decision and proof, if routes were added
- Validation commands and results
- Boundary check against protected paths
- Open blockers / assumptions
- Next recommended step
