---
id: MOD-0046-tenant-core-ui
name: Tenant Core UI
domain: platform-shared-services
status: in-progress
owner: codex
branch: feature/pss/mod-0046-tenant-core-ui
dates:
  started: 2026-04-27
---

# MOD-0046: Tenant Core UI

## Purpose
Implement the Platform Tenant Core UI over the existing Tenant Management backend contracts from MOD-0044 and MOD-0045.

## Repo Scope
- `frontend/Diten.Web/Controllers/TenantsController.cs`
- `frontend/Diten.Web/Views/Platform/Tenants/**`
- `frontend/Diten.Web/wwwroot/assets/js/Platform/Tenants/**`
- `frontend/Diten.Web/Resources/Views/Platform/Tenants/**`

## Protected Paths
- `.antigravity/**`
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`
- `gateway/Diten.ApiGateway/**/ocelot.json`
- Domain dışı servisler

## Acceptance Criteria
- [ ] Tenant Registry Index uses DataTable v2, inline `_Filter`, `_IndexL10n`, `index.l10n.js`, `DtDefaults.create()`, Save View CTA, and ColReorder.
- [ ] Create Tenant is route-based (`/Platform/Tenants/Create`) and supports the full Tenant Core request payload.
- [ ] Tenant Details is route-based (`/Platform/Tenants/Details/{id}`) and displays overview, legal/contact, locale defaults, provisioning/activity, modules, users, and settings.
- [ ] Quick View offcanvas remains preview-only and links to the full Details page.
- [ ] Validation and ProblemDetails errors are rendered in the UI.
- [ ] Module localization resources exist for 7 languages.

## Test Expectations
- Frontend build succeeds.
- Platform API build and tenant application tests remain green.
- DataTable static verifier passes for `--area Platform --module Tenants`.
