---
id: DEV-0000
name: Golden Reference Slim
domain: developer-enablement
service: Diten.DevEnablementService
shell: tenant
golden_reference: slim
entity_base: EntityBase
status: ready-for-dev
owner: ai-orchestrator
branch: feature/dev/dev-0000-golden-reference-slim
started: 2026-04-22
target: 2026-05-15
form_field_count: 6
---

# DEV-0000 - Golden Reference Slim

## Module Summary
Golden Reference Slim is not a business module. It is the official working reference for DataTable modules with eight or fewer create/edit form fields. It proves the expected backend CQRS structure, frontend DataTable v2 page contract, Slim offcanvas editing, quick view, localization bridge, and gateway-backed frontend flow.

This pack documents the live reference implementation. New Slim modules must copy the structure and naming from this reference and change only module-specific names, fields, routes, validation, and domain rules.

## Ownership and Boundaries
- In-scope:
  - Slim DataTable reference pattern for developer enablement.
  - Tenant shell layout example using `Layout = "_LayoutTenantShell"`.
  - Backend CQRS folder and naming convention for `Commands`, `Queries`, `Handlers/CommandHandlers`, `Handlers/QueryHandlers`, `Validators`, and `{Module}Models.cs`.
  - Frontend Slim partial set with create/edit offcanvas and details quick view.
  - DataTable v2 static verification contract.
- Out-of-scope:
  - Production business ownership.
  - Production menu ownership.
  - Platform Admin shell decisions.
  - `.antigravity/**` rule changes unless explicitly requested by user.
  - Refactoring any business module to this shape.

## Owned Objects
- Entity/reference aggregate:
  - `GoldenReferenceSlim`.
- Commands:
  - `CreateGoldenReferenceSlimCommand`.
  - `UpdateGoldenReferenceSlimCommand`.
  - `DeleteGoldenReferenceSlimCommand`.
  - `BulkDeleteGoldenReferenceSlimCommand`.
- Queries:
  - `GetGoldenReferenceSlimListQuery`.
  - `GetGoldenReferenceSlimByIdQuery`.
- Handlers:
  - `CreateGoldenReferenceSlimHandler`.
  - `UpdateGoldenReferenceSlimHandler`.
  - `DeleteGoldenReferenceSlimHandler`.
  - `BulkDeleteGoldenReferenceSlimHandler`.
  - `GetGoldenReferenceSlimListHandler`.
  - `GetGoldenReferenceSlimByIdHandler`.
- Validators:
  - `CreateGoldenReferenceSlimValidator`.
  - `UpdateGoldenReferenceSlimValidator`.
- Models:
  - `GoldenReferenceSlimModels.cs` contains DTO records for list/detail.
- API endpoints:
  - `GET /api/golden-reference-slim`.
  - `GET /api/golden-reference-slim/{id}`.
  - `POST /api/golden-reference-slim`.
  - `PUT /api/golden-reference-slim/{id}`.
  - `DELETE /api/golden-reference-slim/{id}`.
  - `DELETE /api/golden-reference-slim/bulk`.
- Frontend:
  - `GoldenReferenceSlimController`.
  - Route: `/GoldenReferenceSlim`.
  - Views under `Views/DevEnablement/GoldenReferenceSlim/`.
  - Scripts under `wwwroot/assets/js/DevEnablement/GoldenReferenceSlim/`.

## Entity Fields
`GoldenReferenceSlim` reference contract:

| Field | Type | Rules |
|---|---|---|
| Base | `EntityBase` | Tenant-owned reference entity pattern for DevEnablement. |
| Code | `string` | Required, max 64. |
| Name | `string` | Required, max 200. |
| Description | `string?` | Optional descriptive text. |
| ReferenceType | `string?` | Optional low-cardinality type/category field. |
| Priority | `int` | User-editable ordering/weight field. |
| IsActive | `bool` | User-editable active flag, defaults to true. |

Form field count: 6 user-editable fields. This makes the module the Slim reference.

## Repo Scope
- `execution/domains/developer-enablement/module-packs/DEV-0000-golden-reference-slim.md`.
- `services/Diten.DevEnablementService/src/Diten.DevEnablementService.Application/Features/GoldenReferenceSlim/**`.
- `services/Diten.DevEnablementService/src/Diten.DevEnablementService.Api/Controllers/GoldenReferenceSlimController.cs`.
- `frontend/Diten.Web/Controllers/GoldenReferenceSlimController.cs`.
- `frontend/Diten.Web/Views/DevEnablement/GoldenReferenceSlim/**`.
- `frontend/Diten.Web/wwwroot/assets/js/DevEnablement/GoldenReferenceSlim/**`.
- `frontend/Diten.Web/Resources/Views/DevEnablement/GoldenReferenceSlim/**`.
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
- Gateway route for `/api/golden-reference-slim`.
- Shared `Response<T>` envelope and `CustomBaseController` conventions.

## Runtime Constraints
- This reference is tenant-shell oriented: `shell: tenant`.
- Frontend calls the Gateway route; it must not directly call the DevEnablement service port.
- DataTable pages must include `data-dt-standard="v2"`.
- Slim modules use create/edit offcanvas on Index, not separate create/edit pages.
- Slim modules include a details quick view offcanvas.
- `TenantId` is not accepted from client payload; tenant context is server-resolved for tenant-owned modules.
- Action-based separation follows the Golden Reference folder structure exactly.

## Layout & Shell Contract
- `shell: tenant`.
- Razor layout: every `.cshtml` page sets `Layout = "_LayoutTenantShell";` explicitly.
- View folder: `frontend/Diten.Web/Views/DevEnablement/GoldenReferenceSlim/`.
- Frontend route: `/GoldenReferenceSlim`.
- New tenant-shell Slim modules should map this pattern to `Views/{Area}/{Module}/`.

## Backend File Convention
Golden Reference Slim uses this exact backend shape:

```text
Features/GoldenReferenceSlim/
├── Commands/
├── Queries/
├── Handlers/CommandHandlers/
├── Handlers/QueryHandlers/
├── Validators/
└── GoldenReferenceSlimModels.cs
```

Naming convention:
- Commands are sealed records ending in `Command`.
- Queries are sealed records ending in `Query`.
- Handlers are sealed classes ending only in `Handler`; `CommandHandler`, `QueryHandler`, and `RequestHandler` suffixes are not used.
- Validators are sealed classes ending only in `Validator`; `CommandValidator` suffix is not used.
- DTO/view model records for the application feature live in `{Module}Models.cs`.

## Frontend File Contract
Slim file set:
- `Index.cshtml`.
- `_Filter.cshtml`.
- `_DataTable.cshtml`.
- `_IndexL10n.cshtml`.
- `_CreateEditOffcanvas.cshtml`.
- `_DetailsQuickView.cshtml`.
- `GoldenReferenceSlimIndex.cs`.
- `wwwroot/assets/js/DevEnablement/GoldenReferenceSlim/index.js`.
- `wwwroot/assets/js/DevEnablement/GoldenReferenceSlim/index.l10n.js`.
- `Resources/Views/DevEnablement/GoldenReferenceSlim/GoldenReferenceSlimIndex.{lang}.resx`.

Index contract:
- Absolute partial paths are used.
- Bulk action bar uses `DataTableBulkActionBarViewModel`.
- Render order is filter, bulk action bar, DataTable, offcanvas panels.
- Contract marker comments remain visible for verifier checks.

## Validation Rules
| Field | Required | Rule | DB-level | Pre-check |
|---|---|---|---|---|
| Code | Yes | Max 64 | Reference-specific uniqueness if implemented | Check duplicate if persistence supports it |
| Name | Yes | Max 200 | None | Validator |
| Description | No | Reference text | None | None |
| ReferenceType | No | Optional category/type | None | None |
| Priority | Yes | Numeric priority | None | Validator/domain default |
| IsActive | Yes | Boolean | None | Default true |

## Failure Path to Verify
- Missing `Code`: validator returns 400 and create/update is blocked.
- Missing `Name`: validator returns 400 and create/update is blocked.
- Unauthorized tenant actor: protected runtime should return 401 or 403 according to active policy.
- Deleted item reload: get-by-id/list no longer returns the deleted item.
- Bulk delete with empty id set: request is rejected or no-op behavior is explicitly handled by the implementation.

## Authorization Convention
- Tenant-shell reference modules use the tenant authorization convention active in DevEnablement.
- Permission format for tenant service modules is `Modules.{ModuleName}.{Action}` when permissions are enforced.
- Standard actions: Read, Create, Update, Delete, BulkDelete.
- Platform Admin `Platform.*` permissions are not used by this reference.

## Gateway / API Routing Decision
- Decision: Gateway route exists and is part of the reference surface.
- Upstream base path: `/api/golden-reference-slim`.
- Direct modifications to `gateway/Diten.ApiGateway/**/ocelot.json` remain protected and require explicit approval or integration-agent ownership.

## Acceptance Criteria
- [x] Live code is under `DevEnablement` area and `Diten.DevEnablementService`.
- [x] Backend feature uses `Commands`, `Queries`, `Handlers/CommandHandlers`, `Handlers/QueryHandlers`, `Validators`, and `{Module}Models.cs`.
- [x] Handler names use `*Handler` without `Command`, `Query`, or `Request` suffix.
- [x] Index explicitly sets `Layout = "_LayoutTenantShell"`.
- [x] Slim partial set includes `_CreateEditOffcanvas.cshtml` and `_DetailsQuickView.cshtml`.
- [x] DataTable partial includes the v2 contract marker.
- [ ] DataTable verifier remains green after future edits.
- [ ] Derived Slim module packs cite this pack as the canonical reference.

## Test Expectations
- Build DevEnablement API.
- Build frontend.
- Build gateway when route changes are touched.
- Run DataTable contract verifier for `GoldenReferenceSlim`.
- Manually smoke: list load, filter, create offcanvas, edit offcanvas, quick view, delete, bulk delete.
- Confirm layout is explicitly set in `Index.cshtml`.

## Ready-for-dev Checklist
- [x] Golden Reference Slim live code exists.
- [x] Frontmatter includes service, shell, golden_reference, entity_base, and form_field_count.
- [x] Layout & Shell Contract is explicit.
- [x] Backend File Convention mirrors live code.
- [x] Frontend File Contract lists the Slim file set.
- [x] Validation Rules are documented.
- [x] Failure Path to Verify is documented.
- [x] Authorization Convention is documented.
- [x] Gateway routing decision is documented.
- [x] Acceptance criteria and test expectations are testable.

## Implementation Notes
- This pack is documentation for an existing reference implementation; it does not request code changes.
- Existing route/controller placement is intentionally documented as the live reference reality.
- This reference is used for DataTable modules with eight or fewer create/edit fields.
- Future module packs should not copy the business semantics of this module, only the delivery pattern.

## Follow-up Items
- Keep this pack synchronized whenever Golden Reference Slim code intentionally changes.
- Consider adding an automated verifier that compares derived Slim module packs against this file contract.
- Keep `.antigravity` rules pointing at this pack and live code as the single reference standard.
