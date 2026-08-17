---
id: MOD-0014
name: Module Boundary Registry
domain: platform-shared-services
status: in-progress
owner: codex
branch: feature/pss/mod-0014-module-boundary-registry
golden_reference: none
form_field_count: 0
started: 2026-04-28
target: 2026-06-30
---

# MOD-0014 — Module Boundary Registry

## Module Summary
Bu paket, source Excel hiyerarsisinden gelen global Domain / Suite / Capability Group / Module katalog omurgasini `Diten.Platform` icinde kurar. Tenant assignment, pricing, authorization, menu rendering ve entitlement bu fazin disindadir.

## Ownership and Boundaries
- SoR:
  - Global Domain / Landscape katalogu
  - Suite / Platform katalogu
  - Capability Group katalogu
  - Module Definition katalogu
  - Import / idempotent sync contract
- In-scope:
  - `execution/domains/platform-shared-services/module-packs/MOD-0014-module-boundary-registry.md`
  - `services/Diten.Platform.Common/**`
  - `services/Diten.Platform/src/**`
  - `services/Diten.Platform/tests/**`
  - `frontend/Diten.Web/**` (Module Catalog admin page + proxy wiring)
- Out-of-scope:
  - Tenant assignment / entitlement
  - Pricing
  - Permission generation
  - Navigation rendering
  - `gateway/Diten.ApiGateway/**/ocelot.json`

## Repo Scope
- `execution/domains/platform-shared-services/domain-config.md`
- `execution/domains/platform-shared-services/module-packs/MOD-0014-module-boundary-registry.md`
- `services/Diten.Platform.Common/src/**`
- `services/Diten.Platform/src/**`
- `services/Diten.Platform/tests/**`
- `frontend/Diten.Web/**`

## Protected Paths
- `.antigravity/**`
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`
- `gateway/Diten.ApiGateway/**/ocelot.json`
- `services/Diten.AuthService/**`
- `services/Diten.DevEnablementService/**`
- `services/Diten.EnterpriseStrategyService/**`

## Runtime Constraints
- Consumer-facing API base route `api/platform/catalog` olur; route icinde `mod0014` gecmez.
- Katalog globaldir; tenant assignment verisi tutulmaz.
- Import idempotent olur; ayni `ModuleId` ikinci kez duplicate olusturmaz.
- Invalid row partial parent/module olusturmaz.
- UI yalnizca backend contract'i olan aksiyonlari aktif eder.

## Acceptance Criteria
- [ ] Domain / Suite / Capability Group / Module katalog entity ve persistence katmani hazir.
- [ ] `GET /api/platform/catalog/*` endpointleri list/detail/hierarchy akislarini sagliyor.
- [ ] `POST /api/platform/catalog/import` valid importta create/update/skip/fail ozeti donuyor.
- [ ] Frontend `Platform/ModuleCatalog` list/filter/detail/import akisini calistiriyor.
- [ ] Duplicate `ModuleId` olusmuyor; invalid row partial olusturmuyor.

## Test Expectations
- Unit: validation, code normalization, idempotent import, duplicate prevention.
- API: valid import, list, detail by module id, duplicate re-import, invalid import, route naming verification.
- UI smoke: empty/loading/import/detail/filter/error akislari.
