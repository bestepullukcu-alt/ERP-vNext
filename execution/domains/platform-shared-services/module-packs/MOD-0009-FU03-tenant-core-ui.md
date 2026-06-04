---
id: MOD-0009-FU03
name: Tenant Core UI
slug: tenant-core-ui
domain: platform-shared-services
status: in-progress
owner: codex
branch: feature/pss/mod-0046-tenant-core-ui
golden_reference: slim
form_field_count: 7
dates:
  started: 2026-04-27
---

# MOD-0009-FU03: Tenant Core UI

> **Canonicalization (DCP-002):** Canonical ID is now **MOD-0009-FU03**, a child/FU (UI surface) of **MOD-0009 Tenant / Environment Management** (Blueprint canonical). Prior repo ID **MOD-0046** is a deprecated alias retained for traceability; repo MOD-0046 had drifted onto a Blueprint ID reserved for "Performance & Capacity Management". Body text below predates canonicalization and may reference MOD-0046; scope and meaning are unchanged. Ref: `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`.

## Purpose
Implement the Platform Tenant Core UI over the existing Tenant Management backend contracts from **MOD-0009-FU02** (Tenant Manager Backend; prior alias MOD-0044). The former **MOD-0045** "Tenant Mgmt Legacy / Gap Reference" is retired as a non-executable legacy reference (see DCP-002).

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
