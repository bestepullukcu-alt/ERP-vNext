---
id: DEV-0001
name: Golden Reference Compact
domain: developer-enablement
service: Diten.DevEnablementService
shell: tenant
golden_reference: compact
entity_base: EntityBase
status: in-progress
owner: developer-enablement
branch: feature/dev/dev-0001-golden-reference-compact
started: 2026-04-27
target: 2026-05-15
form_field_count: 13
---

# DEV-0001 - Golden Reference Compact

## Module Summary
Golden Reference Compact is the official working reference for DataTable modules with more than eight create/edit form fields. It mirrors the Golden Reference Slim backend pattern while demonstrating full-page create, edit, and details flows instead of Index-hosted create/edit offcanvas panels.

This pack documents the live Compact reference implementation. New Compact modules must copy its structure and naming and change only module-specific names, fields, routes, validation, and domain rules.

## Ownership and Boundaries
- In-scope:
  - Compact DataTable reference pattern for developer enablement.
  - Tenant shell layout example using `Layout = "_LayoutTenantShell"`.
  - Backend CQRS folder and naming convention shared with Slim.
  - Frontend Compact page set with separate `Create`, `Edit`, `Details`, and shared `_Form`.
  - DataTable v2 static verification contract.
- Out-of-scope:
  - Production business ownership.
  - Platform Admin shell decisions.
  - Index-hosted create/edit offcanvas behavior.
  - `.antigravity/**` rule changes unless explicitly requested by user.
  - Refactoring existing business modules.

## Owned Objects
- Entity/reference aggregate:
  - `GoldenReferenceCompact`.
- Commands:
  - `CreateGoldenReferenceCompactCommand`.
  - `UpdateGoldenReferenceCompactCommand`.
  - `DeleteGoldenReferenceCompactCommand`.
  - `BulkDeleteGoldenReferenceCompactCommand`.
- Queries:
  - `GetGoldenReferenceCompactListQuery`.
  - `GetGoldenReferenceCompactByIdQuery`.
- Handlers:
  - `CreateGoldenReferenceCompactHandler`.
  - `UpdateGoldenReferenceCompactHandler`.
  - `DeleteGoldenReferenceCompactHandler`.
  - `BulkDeleteGoldenReferenceCompactHandler`.
  - `GetGoldenReferenceCompactListHandler`.
  - `GetGoldenReferenceCompactByIdHandler`.
- Validators:
  - `CreateGoldenReferenceCompactValidator`.
  - `UpdateGoldenReferenceCompactValidator`.
- Models:
  - `GoldenReferenceCompactModels.cs` contains DTO records for list/detail.
- API endpoints:
  - `GET /api/golden-reference-compact`.
  - `GET /api/golden-reference-compact/{id}`.
  - `POST /api/golden-reference-compact`.
  - `PUT /api/golden-reference-compact/{id}`.
  - `DELETE /api/golden-reference-compact/{id}`.
  - `DELETE /api/golden-reference-compact/bulk`.
- Frontend:
  - `GoldenReferenceCompactController`.
  - Route: `/GoldenReferenceCompact`.
  - Views under `Views/DevEnablement/GoldenReferenceCompact/`.
  - Scripts under `wwwroot/assets/js/DevEnablement/GoldenReferenceCompact/`.

## Entity Fields
`GoldenReferenceCompact` reference contract:

| Field | Type | Rules |
|---|---|---|
| Base | `EntityBase` | Tenant-owned reference entity pattern for DevEnablement. |
| Code | `string` | Required, max 64. |
| Name | `string` | Required, max 200. |
| Description | `string?` | Optional descriptive text. |
| ReferenceType | `string?` | Optional, max 80. |
| Category | `string?` | Optional, max 120. |
| GroupKey | `string?` | Optional, max 120. |
| SourceSystem | `string?` | Optional, max 120. |
| Owner | `string?` | Optional, max 120. |
| Version | `string?` | Existing live reference field, max 40. Future business modules should avoid naming semantic fields `Version` unless the standard explicitly allows it. |
| EffectiveDate | `DateTime?` | Optional start date. |
| ExpirationDate | `DateTime?` | Optional end date; must be >= EffectiveDate when both are provided. |
| Priority | `int` | 0 through 100. |
| IsActive | `bool` | User-editable active flag, defaults to true. |

Form field count: 13 user-editable fields. This makes the module the Compact reference.

## Repo Scope
- `execution/domains/developer-enablement/module-packs/DEV-0001-golden-reference-compact.md`.
- `services/Diten.DevEnablementService/src/Diten.DevEnablementService.Application/Features/GoldenReferenceCompact/**`.
- `services/Diten.DevEnablementService/src/Diten.DevEnablementService.Api/Controllers/GoldenReferenceCompactController.cs`.
- `frontend/Diten.Web/Controllers/GoldenReferenceCompactController.cs`.
- `frontend/Diten.Web/Views/DevEnablement/GoldenReferenceCompact/**`.
- `frontend/Diten.Web/wwwroot/assets/js/DevEnablement/GoldenReferenceCompact/**`.
- `frontend/Diten.Web/Resources/Views/DevEnablement/GoldenReferenceCompact/**`.
- `gateway/Diten.ApiGateway/**` for existing route reference only; direct route changes remain protected.

## Protected Paths
- `.antigravity/**`.
- `frontend/Diten.Web/Controllers/Archive/**`.
- `frontend/Diten.Web/Views/Archive/**`.
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`.
- `gateway/Diten.ApiGateway/**/ocelot.json` unless explicitly approved for route coordination.
- `services/Diten.AuthService/**`.
- `services/Diten.Platform/**`.
- `services/Diten.EnterpriseStrategyService/**`.
- `services/Diten.MdmService/**`.
- Business-domain module internals outside Golden Reference scope.

## Dependencies
- Developer Enablement domain config.
- DataTable v2 frontend verifier contract.
- Razor MVC tenant shell.
- Gateway route for `/api/golden-reference-compact`.
- Shared `Response<T>` envelope and `CustomBaseController` conventions.
- Golden Reference Slim as the sibling low-field-count reference.

## Runtime Constraints
- This reference is tenant-shell oriented: `shell: tenant`.
- Frontend calls the Gateway route; it must not directly call the DevEnablement service port.
- DataTable pages must include `data-dt-standard="v2"`.
- Compact modules use separate create, edit, and details pages.
- Compact modules use shared `_Form.cshtml`.
- Compact modules must not use `_CreateEditOffcanvas.cshtml` or `_DetailsQuickView.cshtml`.
- `TenantId` is not accepted from client payload; tenant context is server-resolved for tenant-owned modules.
- Action-based separation follows the Golden Reference folder structure exactly.

## Layout & Shell Contract
- `shell: tenant`.
- Razor layout: every `.cshtml` page sets `Layout = "_LayoutTenantShell";` explicitly.
- View folder: `frontend/Diten.Web/Views/DevEnablement/GoldenReferenceCompact/`.
- Frontend route: `/GoldenReferenceCompact`.
- New tenant-shell Compact modules should map this pattern to `Views/{Area}/{Module}/`.

## Backend File Convention
Golden Reference Compact uses this exact backend shape:

```text
Features/GoldenReferenceCompact/
├── Commands/
├── Queries/
├── Handlers/CommandHandlers/
├── Handlers/QueryHandlers/
├── Validators/
└── GoldenReferenceCompactModels.cs
```

Naming convention:
- Commands are sealed records ending in `Command`.
- Queries are sealed records ending in `Query`.
- Handlers are sealed classes ending only in `Handler`; `CommandHandler`, `QueryHandler`, and `RequestHandler` suffixes are not used.
- Validators are sealed classes ending only in `Validator`; `CommandValidator` suffix is not used.
- DTO/view model records for the application feature live in `{Module}Models.cs`.

## Frontend File Contract
Compact file set:
- `Index.cshtml`.
- `Create.cshtml`.
- `Edit.cshtml`.
- `Details.cshtml`.
- `_Form.cshtml`.
- `_Filter.cshtml`.
- `_DataTable.cshtml`.
- `_IndexL10n.cshtml`.
- `GoldenReferenceCompactIndex.cs`.
- `wwwroot/assets/js/DevEnablement/GoldenReferenceCompact/index.js`.
- `wwwroot/assets/js/DevEnablement/GoldenReferenceCompact/index.l10n.js`.
- `Resources/Views/DevEnablement/GoldenReferenceCompact/GoldenReferenceCompactIndex.{lang}.resx`.

Compact must not include Slim-only `_CreateEditOffcanvas.cshtml` or `_DetailsQuickView.cshtml`.

## Validation Rules
| Field | Required | Rule | DB-level | Pre-check |
|---|---|---|---|---|
| Code | Yes | Max 64 | Reference-specific uniqueness if implemented | Check duplicate if persistence supports it |
| Name | Yes | Max 200 | None | Validator |
| ReferenceType | No | Max 80 | None | Validator |
| Category | No | Max 120 | None | Validator |
| GroupKey | No | Max 120 | None | Validator |
| SourceSystem | No | Max 120 | None | Validator |
| Owner | No | Max 120 | None | Validator |
| Version | No | Max 40 | None | Validator |
| EffectiveDate | No | Date | None | Validator |
| ExpirationDate | No | Must be >= EffectiveDate when both exist | None | Validator |
| Priority | Yes | 0 through 100 | None | Validator |
| IsActive | Yes | Boolean | None | Default true |

## Failure Path to Verify
- Missing `Code`: validator returns 400 and create/update is blocked.
- Missing `Name`: validator returns 400 and create/update is blocked.
- Invalid expiration date before effective date: validator returns 400.
- Unauthorized tenant actor: protected runtime should return 401 or 403 according to active policy.
- Deleted item reload: get-by-id/list no longer returns the deleted item.
- Compact route opens full create/edit/details pages rather than offcanvas panels.

## Authorization Convention
- Tenant-shell reference modules use the tenant authorization convention active in DevEnablement.
- Permission format for tenant service modules is `Modules.{ModuleName}.{Action}` when permissions are enforced.
- Standard actions: Read, Create, Update, Delete, BulkDelete.
- Platform Admin `Platform.*` permissions are not used by this reference.

## Gateway / API Routing Decision
- Decision: Gateway route exists and is part of the reference surface.
- Upstream base path: `/api/golden-reference-compact`.
- Direct modifications to `gateway/Diten.ApiGateway/**/ocelot.json` remain protected and require explicit approval or integration-agent ownership.

## Acceptance Criteria
- [x] Live code is under `DevEnablement` area and `Diten.DevEnablementService`.
- [x] Backend feature uses `Commands`, `Queries`, `Handlers/CommandHandlers`, `Handlers/QueryHandlers`, `Validators`, and `{Module}Models.cs`.
- [x] Handler names use `*Handler` without `Command`, `Query`, or `Request` suffix.
- [x] Index explicitly sets `Layout = "_LayoutTenantShell"`.
- [x] Compact page set includes `Create.cshtml`, `Edit.cshtml`, `Details.cshtml`, and `_Form.cshtml`.
- [x] Compact page set does not use Slim create/edit offcanvas.
- [x] DataTable partial includes the v2 contract marker.
- [ ] DataTable verifier remains green after future edits.
- [ ] Derived Compact module packs cite this pack as the canonical reference.

## Test Expectations
- Build DevEnablement API.
- Build frontend.
- Build gateway when route changes are touched.
- Run DataTable contract verifier for `GoldenReferenceCompact`.
- Manually smoke: list load, filter, create page, edit page, details page, delete, bulk delete.
- Confirm layout is explicitly set in `Index.cshtml`, `Create.cshtml`, `Edit.cshtml`, and `Details.cshtml`.

## Ready-for-dev Checklist
- [x] Golden Reference Compact live code exists.
- [x] Frontmatter includes service, shell, golden_reference, entity_base, and form_field_count.
- [x] Layout & Shell Contract is explicit.
- [x] Backend File Convention mirrors live code.
- [x] Frontend File Contract lists the Compact file set.
- [x] Validation Rules are documented.
- [x] Failure Path to Verify is documented.
- [x] Authorization Convention is documented.
- [x] Gateway routing decision is documented.
- [x] Acceptance criteria and test expectations are testable.

## Implementation Notes
- This pack is documentation for an existing reference implementation; it does not request code changes.
- Existing route/controller placement is intentionally documented as the live reference reality.
- This reference is used for DataTable modules with more than eight create/edit fields.
- The live Compact command includes a `Version` field. New business modules should follow the current entity naming rule and avoid semantic `Version` naming unless that rule is explicitly changed.

## Follow-up Items
- Keep this pack synchronized whenever Golden Reference Compact code intentionally changes.
- Decide whether the live `Version` field should be renamed in a future reference cleanup.
- Consider adding an automated verifier that compares derived Compact module packs against this file contract.
- Keep `.antigravity` rules pointing at this pack and live code as the single reference standard.
