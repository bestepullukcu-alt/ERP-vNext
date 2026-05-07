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
Phase 2 delivers tenant-level login and security settings management for Platform administration. The Platform service stores settings, creates defaults during tenant registration, and exposes an admin CRUD contract consumed by a dedicated Platform UI page.

## Ownership and Boundaries
- In-scope: Platform tenant login/security settings CRUD and Platform Admin UI.
- Out-of-scope: AuthService enforcement, real MFA/OTP/SSO/OIDC/SAML/device trust/adaptive MFA.
- Gateway route changes are out-of-scope; existing admin tenant catch-all route is assumed.

## Repo Scope
- `services/Diten.Platform/**`
- `services/Diten.Platform/tests/**`
- `frontend/Diten.Web/Controllers/TenantsController.cs`
- `frontend/Diten.Web/Views/Platform/Tenants/**`
- `frontend/Diten.Web/wwwroot/assets/js/Platform/Tenants/**`
- `frontend/Diten.Web/Resources/Views/Platform/Tenants/**`
- `frontend/Diten.Web/Views/Shared/_LayoutPlatformAdmin.cshtml`

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
- Do not call AuthService from this phase.

## Acceptance Criteria
- [ ] Tenant registration creates default login/security settings.
- [ ] `GET /api/admin/tenants/{id}/login-settings` returns existing settings or creates defaults.
- [ ] `PUT /api/admin/tenants/{id}/login-settings` validates, normalizes, persists, and records tenant activity.
- [ ] MongoDB has a unique `TenantRefId` index for `tenant_login_settings`.
- [ ] Platform Admin has a dedicated `/Platform/TenantSecurity` page.
- [ ] Tenant Detail links to the dedicated Login & Security page.
- [ ] UI localization keys exist for en, fr, es, zh, ar, ru, tr.

## Test Expectations
- Unit tests cover create defaults, GET default creation, PUT update, and validator ranges.
- Frontend JavaScript passes `node --check`.
- Platform application tests and Platform API build pass.

## Implementation Notes
- Settings are CRUD-only in this phase; AuthService enforcement is intentionally deferred.

## Follow-up Items
- Add AuthService read/enforcement contract in a later security enforcement phase.
