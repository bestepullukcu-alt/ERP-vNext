---
id: DEV-0001
name: Golden Reference Compact
domain: developer-enablement
status: in-progress
owner: developer-enablement
branch: feature/dev/dev-0001-golden-reference-compact
created: 2026-04-27
updated: 2026-04-27
---

# Golden Reference Compact

## Purpose
Reference implementation for DataTable modules with more than 8 create/edit form fields. It mirrors the Golden Reference Slim technical pattern while demonstrating full-page create/edit forms instead of offcanvas editing.

## Owned Objects
- DevEnablement API route: `/api/golden-reference-compact`
- Frontend route: `/GoldenReferenceCompact`
- Mongo collection: `golden_reference_compact`

## Repo Scope
- `services/Diten.DevEnablementService/**/GoldenReferenceCompact*`
- `frontend/Diten.Web/Controllers/GoldenReferenceCompactController.cs`
- `frontend/Diten.Web/Models/GoldenReferenceCompact/**`
- `frontend/Diten.Web/Views/DevEnablement/GoldenReferenceCompact/**`
- `frontend/Diten.Web/wwwroot/assets/js/DevEnablement/GoldenReferenceCompact/**`
- `frontend/Diten.Web/Resources/Views/DevEnablement/GoldenReferenceCompact/**`
- `gateway/Diten.ApiGateway/ocelot.json`

## Acceptance Criteria
- Index uses DataTables v2 with filter, saved view, quick view, row delete, and bulk delete.
- Create/Edit use full MVC pages, not offcanvas.
- Entity has more than 8 user-facing create/edit form fields: code, name, description, reference type, category, group key, source system, owner, version, effective date, expiration date, priority, active.
- GoldenReferenceSlim remains unchanged behaviorally.

## Test Expectations
- Build DevEnablement API, frontend, and gateway.
- Run DataTable contract verifier for `GoldenReferenceCompact`.
- Manually verify index, create, edit, quick view, delete, and bulk delete.
