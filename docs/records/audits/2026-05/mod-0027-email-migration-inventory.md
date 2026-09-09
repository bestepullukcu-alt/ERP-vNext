# MOD-0027 - Existing Email / Invitation Flow Migration Inventory

- **Module:** MOD-0027 Central Tenant Email / Notification Service
- **Batch:** Batch 2 - Integrations and migration inventory
- **Date:** 2026-05-18
- **Status:** Inventory only. **No existing flow is migrated in Batch 2.**
- **Owner:** Notification
- **Branch:** feature/email-service

## Purpose

This document catalogs every ad-hoc email/invitation/notification sending code path that currently lives outside MOD-0027. It is a planning artifact for future migration packs. Producing the inventory is in MOD-0027 Batch 2 scope; performing the migration is not - migration of any flow below requires either a dedicated migration pack or an explicit amendment to MOD-0027.

## Migration policy (binding for Batch 2)

1. Inventory only. No code in the services below is modified for migration in Batch 2.
2. AuthService is read-only for this inventory. Batch 2 does not edit `services/Diten.AuthService/**` for migration purposes.
3. Auth-critical flows (password reset, MFA OTP) must not be migrated in a single PR. Any future migration must run dual-send with a feature flag during cutover.
4. Each candidate below identifies the target MOD-0027 surface (template key, command, API). The template definitions themselves must be created as approved `NotificationTemplate` records before any flow is cut over.

## Inventory schema

| Field | Meaning |
|---|---|
| `ID` | Stable identifier for tracking (`A*` = AuthService, `P*` = Platform, `F*` = Frontend, `X*` = framework). |
| `Path` | Repository-relative file path. |
| `Current behavior` | What the file does today. |
| `Owner / service` | Service that owns the file. |
| `Template candidate key` | Proposed MOD-0027 template key (lowercase dotted). |
| `Required variables` | Variables the future MOD-0027 template must declare. |
| `Target MOD-0027 surface` | Command / API the migrated flow should call. |
| `Migration risk` | High = blocking auth flow / regulated communication; Medium = onboarding UX; Low/Informational = framework or non-sending file. |
| `Recommended migration batch` | When migration may be considered; not a commitment. |

## Candidates

### A1 - Platform admin password reset email

- **Path:** `services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Services/PlatformAuthEmailService.cs`
- **Current behavior:** Sends a password reset link email to platform administrators using `System.Net.Mail.SmtpClient` and a hardcoded HTML template (`PlatformPasswordResetEmailTemplate`). Configuration is read from the `Smtp:*` section.
- **Owner / service:** Diten.AuthService.
- **Template candidate key:** `platform.admin.password_reset.email`
- **Required variables:** `email`, `resetUrl`, `resetToken`, `expiryMinutes` (currently a hardcoded 60-minute expiry hint).
- **Target MOD-0027 surface:** `QueueEmailNotificationCommand` with the candidate template above; tenant id resolved by the calling AuthService context (this is a Platform-actor flow, so it should use the Platform default `TenantMessagingSettings` until a dedicated platform-tenant identity is introduced).
- **Migration risk:** **High.** Blocking platform-admin auth recovery.
- **Recommended migration batch:** After MOD-0263 SMTP adapter is approved and after a dedicated migration pack is created. Migration must run in dual-send mode for at least one release.

### A2 - Tenant login MFA email OTP

- **Path:** `services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Services/SmtpOtpDeliveryService.cs`
- **Current behavior:** Sends a time-limited numeric OTP code to tenant users during MFA challenge using `System.Net.Mail.SmtpClient`. Inline plain-text subject and body (subject: `Diten ERP verification code`; body: `Your Diten ERP verification code is {code}. It expires at {expiresAtUtc:HH:mm} UTC.`). Supports unauthenticated SMTP when `Smtp:Username` is blank.
- **Owner / service:** Diten.AuthService.
- **Template candidate key:** `auth.mfa.otp.email`
- **Required variables:** `email`, `code`, `expiresAtUtc`, optional `requestIp`, `userAgent` for telemetry-aware templates.
- **Target MOD-0027 surface:** `QueueEmailNotificationCommand`. Caller must resolve tenant id from session/login context (not from request body) because MFA is a tenant-context flow.
- **Migration risk:** **High.** Blocking login security control. Direct-call SMTP path is also load-bearing for resend cooldown timing.
- **Recommended migration batch:** Only after dual-send proof and rate-limit parity validation with `MfaChallengeService` resend semantics.

### A3 - MFA challenge orchestrator

- **Path:** `services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Services/MfaChallengeService.cs`
- **Current behavior:** Orchestrates MFA challenge lifecycle (create, resend, verify) and delegates email delivery to `IOtpDeliveryService` (A2).
- **Owner / service:** Diten.AuthService.
- **Template candidate key:** Not its own template; depends on A2.
- **Required variables:** Same as A2.
- **Target MOD-0027 surface:** No direct migration. After A2 migrates, this file's delegate point switches from `IOtpDeliveryService` to a MOD-0027 client.
- **Migration risk:** **High.** Co-migrated with A2.
- **Recommended migration batch:** Same as A2.

### P1 - Tenant admin user invitation email

- **Path:** `services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/AdminUserInvitationService.cs`
- **Current behavior:** Multi-step tenant admin onboarding: (1) provisions user via AuthService internal API, (2) builds tenant-specific login URL from `AuthService:TenantLoginUrlTemplate`, (3) sends HTML invitation with credentials and login button via `System.Net.Mail.SmtpClient`. Template lives at `AdminUserInvitationEmailTemplate.cs`. Subject: `Diten ERP invite - {tenantName}`.
- **Owner / service:** Diten.Platform.
- **Template candidate key:** `tenant.admin_user.invite.email`
- **Required variables:** `tenantName`, `tenantId`, `tenantSlug`, `tenantDomain`, `userEmail`, `userName`, `temporaryPassword`, `loginUrl`.
- **Target MOD-0027 surface:** `QueueEmailNotificationCommand` against the target tenant. The provisioning + email composition must remain ordered: provisioning success before queueing the email; queueing failure must not roll back provisioning.
- **Migration risk:** **Medium.** Onboarding UX flow. `temporaryPassword` must never be persisted in the dispatch body; pass it as a sanitized variable, render at template time, store only redacted metadata.
- **Recommended migration batch:** After MOD-0263 SMTP adapter approval; concurrent with P2.

### P2 - Platform administrator invitation email

- **Path:** `services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/PlatformAdministratorInvitationEmailService.cs`
- **Current behavior:** Sends platform-admin onboarding email after AuthService provisioning. Uses `System.Net.Mail.SmtpClient` and `PlatformAdministratorInvitationEmailTemplate`. Subject: `Your Di10 platform admin account`.
- **Owner / service:** Diten.Platform.
- **Template candidate key:** `platform.admin.invite.email`
- **Required variables:** `email`, `userName`, `displayName`, `temporaryPassword`, `loginUrl`, `expiryDays` (currently hardcoded 7).
- **Target MOD-0027 surface:** `QueueEmailNotificationCommand` against the platform default tenant context.
- **Migration risk:** **Medium.** Platform-admin onboarding; not user-blocking but visible.
- **Recommended migration batch:** Concurrent with P1.

### X1 - EmailTemplateRenderer (framework)

- **Path:** `services/Diten.Platform/src/Diten.Platform.Application/Features/Notifications/Services/EmailTemplateRenderer.cs`
- **Current behavior:** Generic template variable substitution engine (`{{var}}` syntax) used by MOD-0027 Batch 1. Already production-ready for MOD-0027 templates.
- **Owner / service:** Diten.Platform (MOD-0027).
- **Migration risk:** Informational. Not a sender, not a migration target.

### X2 - Notification enums (framework)

- **Path:** `services/Diten.Platform/src/Diten.Platform.Domain/Enums/NotificationEnums.cs`
- **Current behavior:** Declares `MessagingProviderCode`, `NotificationChannelCode`, `NotificationTemplateStatus`, `NotificationDispatchStatus`, `NotificationFallbackPolicy`, `TemplateVariableType`. `SendGrid` is listed but unimplemented; concrete adapter belongs to MOD-0263.
- **Migration risk:** Informational.

### F1 - Password reset UI

- **Path:** `frontend/Diten.Web/wwwroot/assets/js/Account/reset-password.js` and `frontend/Diten.Web/Controllers/AccountController.cs`
- **Current behavior:** Frontend form validation and submission for `/platform/reset-password` and `/platform/change-password`. Delegates to AuthService through `IAuthGateway` for the actual reset.
- **Migration risk:** Informational. No email sending; routing-only.
- **Note:** A1 migration may eventually allow this UI to display a "We sent an email - check inbox" copy that references the MOD-0027 dispatch id instead of being silent.

### F2 - MFA verification UI

- **Path:** `frontend/Diten.Web/Controllers/AccountController.cs` (`/account/login/mfa`, `/account/login/mfa/resend`)
- **Current behavior:** Frontend MFA verification and resend UI; calls AuthService through `IAuthGateway`. No email sending here.
- **Migration risk:** Informational.

## Shared configuration referenced

| Key | Files | Notes |
|---|---|---|
| `Smtp:Host`, `Smtp:Port`, `Smtp:Username`, `Smtp:Password`, `Smtp:EnableSsl`, `Smtp:FromEmail`, `Smtp:FromName`, `Smtp:Enabled` | A1, A2, P1, P2 | Both services bind `SmtpOptions`. `Smtp:Password` is validated at startup when `Smtp:Enabled=true`. After migration this surface should be replaced by `TenantMessagingSettings` per tenant + a MOD-0263-owned provider configuration. |
| `AuthService:FrontendBaseUrl` | P2 | Used to build login URLs. After migration, store as a template variable, not in code. |
| `AuthService:TenantLoginUrlTemplate` | P1 | Used to build tenant-specific login URLs. After migration, pass as a template variable. |
| `AuthService:InternalApiKey`, `PlatformService:InternalApiKey` | P1, P2 | Internal service-to-service auth. Unchanged by MOD-0027; provisioning still happens via internal API before queueing the invitation email. |

## Migration risk summary

| Risk | Count | IDs |
|---|---|---|
| High (auth-critical) | 3 | A1, A2, A3 |
| Medium (onboarding UX) | 2 | P1, P2 |
| Informational (framework/UI) | 4 | X1, X2, F1, F2 |

## Recommended future batches (proposal only - not binding)

- **Migration batch M1: Onboarding cutover (Medium risk).** Migrate P1 + P2 first because the failure mode is well understood and a dispatch delay does not lock out a user. Requires: MOD-0263 SMTP adapter approved; MOD-0027 templates seeded for `tenant.admin_user.invite.email` and `platform.admin.invite.email`; dual-send proof.
- **Migration batch M2: Password reset cutover (High risk).** Migrate A1 with feature flag dual-send. Requires: M1 stable for one release; explicit MOD-0027 amendment to cover Platform-actor sending without a tenant context.
- **Migration batch M3: MFA OTP cutover (High risk).** Migrate A2 (and the A3 delegate point) with feature flag dual-send and rate-limit parity tests. Requires: M2 stable for one release; explicit decision on whether OTP delivery should remain in AuthService (with MOD-0027 only providing the template) or move fully to MOD-0027.

## Out of scope for Batch 2

- Any change to the files listed under A1, A2, A3, P1, P2.
- Any change to `services/Diten.AuthService/**` beyond read-only inspection captured in this document.
- Any change to `.antigravity/**`, archive controllers/views, gateway routes, frontend Razor/DataTable/RESX, or other domain services.
- Real SMTP/SendGrid/MailKit adapter implementation (owned by MOD-0263).
- New scheduler or event bus (owned by MOD-0026 / MOD-0035; consumed through their public abstractions only).

## Next steps

- Hold this inventory as the canonical migration plan until a dedicated migration pack (`module-pack-author`) is approved.
- Do not delete this file when MOD-0027 Batch 2 closes; it is required for future migration scoping.

---

# Appendix A — Inbound event-mapper ownership

This appendix documents the ownership model for inbound notification event mappers introduced in MOD-0027 Batch 2. It is binding for any future module that wants to drive an email notification from an internal event.

## Ownership rules (binding)

1. **MOD-0027 owns the seam, not the mappings.** The interface [`INotificationEventMapper<TEvent>`](../../services/Diten.Platform/src/Diten.Platform.Application/Features/Notifications/Eventing/INotificationEventMapper.cs) and the target contract `QueueEmailNotificationRequest` are the only event-mapping surfaces MOD-0027 ships.
2. **Source-event-owning modules own concrete mappings.** A module that publishes a notification-worthy event (e.g. tenant onboarding, password reset, subscription expiry) is responsible for:
   - Declaring the event contract under `Diten.Platform.Contracts.Events.*` with `IInternalEvent` + a stable versioned name (`{domain}.{action}.v{n}`).
   - Implementing one `INotificationEventMapper<TEvent>` per event in its own Application layer.
   - Registering the mapper in its own DI module.
   - Implementing the consumer (via `IConsumer<EventTransportMessage>` and the existing MOD-0035 transport seam) that resolves the mapper, builds a `QueueEmailNotificationCommand`, and sends it through MediatR.
3. **Transport boundary.** Consumers route through MOD-0035 public abstractions only. MOD-0027 forbids direct RabbitMQ/MassTransit references inside `Diten.Platform.Application.Features.Notifications.*` (enforced by reflective test `NotificationFeature_ShouldNotReferenceRabbitMqOrMassTransitDirectly`).
4. **Tenant context.** Mappers must read `TenantId` from the event envelope (`EventEnvelope<TEvent>.TenantId`) — never from a request body or out-of-band lookup.
5. **No speculative mappings.** Adding a concrete mapper to MOD-0027 itself is forbidden without a real, approved source event. Orchestrator rule 5 (zero hallucination) applies; placeholder mappings shall not be merged.

## What "ownership" looks like in practice

When module `MOD-XXXX` decides one of its events should trigger an email:

1. `MOD-XXXX` pack lists the new event contract (name, version, payload fields) and the target template key in its own `module-pack` markdown.
2. `MOD-XXXX` publishes the event through `IEventBus` (from `Diten.BuildingBlocks.Eventing`).
3. `MOD-XXXX` ships a concrete `INotificationEventMapper<TheEvent>` and a consumer in its own service.
4. `MOD-XXXX` ensures the template exists by creating it through `CreateNotificationTemplateCommand` (or via its own seed) — MOD-0027 does not auto-create templates from event names.
5. No MOD-0027 source file changes; the only MOD-0027 surface touched is the public seam.

## When the seam should be revisited

- A future requirement that mappers need to filter by user preferences (MOD-0287) — that will live in the consumer layer, not in MOD-0027. Update this appendix when MOD-0287 is approved.
- A future requirement that the same mapping must produce multi-channel output (email + SMS + push) — that requires a broader notification orchestration contract; do not extend `INotificationEventMapper<TEvent>` in place.
