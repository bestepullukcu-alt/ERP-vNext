---
id: PSS-004
name: Tenant Login Security Settings
domain: platform-shared-services
status: in-progress
owner: codex
branch: feature/pss/pss-004-tenant-login-security-settings
started: 2026-04-27
target: 2026-05-15
---

# PSS-004 — Tenant Login Security Settings

## Module Summary
Phase 2 delivers tenant-level login and security settings management for Platform administration and AuthService tenant-login enforcement. The Platform service stores settings, creates defaults during tenant registration, exposes admin CRUD contracts, and exposes a narrow internal read contract consumed only by AuthService.

## Ownership and Boundaries
- In-scope: Platform tenant login/security settings CRUD, Tenant Details Login & Security UI, AuthService tenant login enforcement, fail-closed settings reads, lockout/session settings, and Email OTP MFA.
- Out-of-scope: SSO/OIDC/SAML/device trust/adaptive MFA, active phone login route, and SMS provider delivery.
- Gateway route changes are out-of-scope; existing admin tenant catch-all route is assumed.

## Repo Scope
- `services/Diten.Platform/**`
- `services/Diten.Platform/tests/**`
- `frontend/Diten.Web/Controllers/TenantsController.cs`
- `frontend/Diten.Web/Views/Platform/Tenants/**`
- `frontend/Diten.Web/wwwroot/assets/js/Platform/Tenants/**`
- `frontend/Diten.Web/Resources/Views/Platform/Tenants/**`
- `frontend/Diten.Web/Views/Shared/_LayoutPlatformAdmin.cshtml`
- `services/Diten.AuthService/**`

## Protected Paths
- `.antigravity/**`
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`
- `gateway/Diten.ApiGateway/**/ocelot.json`
- Domain dışı servisler

## Dependencies
- MOD-0044 Tenant Manager backend contracts.
- MOD-0046 Tenant Core UI patterns.

## Runtime Constraints
- Store settings as platform-level `GlobalEntity` records linked by `TenantRefId`.
- Preserve `PlatformActor` authorization and `Response<T>` envelope.
- AuthService reads Platform settings through `GET /api/internal/tenants/{tenantId}/login-settings` with internal API key authentication.
- AuthService must fail tenant login closed when settings cannot be read; Platform admin login must not use tenant login settings.
- Phone Login is stored for future channel support. Phone OTP delivery is disabled until SMS provider configuration is enabled.

## Acceptance Criteria
- [ ] Tenant registration creates default login/security settings.
- [ ] `GET /api/admin/tenants/{id}/login-settings` returns existing settings or creates defaults.
- [ ] `PUT /api/admin/tenants/{id}/login-settings` validates, normalizes, persists, and records tenant activity.
- [ ] MongoDB has a unique `TenantRefId` index for `tenant_login_settings`.
- [ ] Platform Admin has a dedicated `/Platform/TenantSecurity` page.
- [ ] Tenant Detail `Access > Login & Security` contains an editable settings form.
- [ ] UI localization keys exist for the supported Platform UI languages: en and tr.
- [ ] Internal settings endpoint returns exact default settings when a record is missing.
- [ ] AuthService tenant login fails closed when settings cannot be read.
- [ ] AuthService lockout/session/refresh lifetime uses tenant settings for tenant users only.
- [ ] MFA Required cannot be saved unless Two-Factor Authentication and Email Login are enabled.
- [ ] Email OTP challenge creates no access token, refresh token, auth cookie, or open session before successful verification.
- [ ] OTP and raw challenge identifiers are never stored or logged in plaintext.

## Test Expectations
- Unit tests cover create defaults, GET default creation, PUT update, and validator ranges.
- Unit tests cover internal settings auth, fail-closed login, tenant lockout settings, OTP challenge/verify safety, and Platform login isolation.
- Frontend JavaScript passes `node --check`.
- Platform application tests, AuthService build, and Platform API build pass.

## Implementation Notes
- Default settings: EmailLoginEnabled=true, PhoneLoginEnabled=false, TwoFactorEnabled=false, MfaRequired=false, PasswordMinLength=10, PasswordExpirationDays=null, PasswordRequireUppercase=true, PasswordRequireSpecialChar=true, SessionTimeoutMinutes=60, MaxFailedLoginAttempts=5, LockoutDurationMinutes=15, RefreshTokenLifetimeDays=14.
- Phone Login helper text must explain that the setting is stored for future channel support and Phone OTP delivery is disabled until SMS provider configuration is enabled.
- Verify receives only raw challengeId and code. AuthService hashes challengeId for lookup and uses stored TenantId/UserId context.

## Follow-up Items
- Add reset password flow using the shared tenant password policy validator.
- Enable Phone OTP delivery when SMS provider configuration is available.
