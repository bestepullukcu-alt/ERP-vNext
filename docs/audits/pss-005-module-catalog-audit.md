# Module Catalog Audit

Module pack: `execution/domains/platform-shared-services/module-packs/PSS-005-tenant-module-catalog.md`

## Phase Summary

- Phase 0: Approved module pack confirmed; scope limited to `Diten.Platform` and `frontend/Diten.Web`.
- Phase 1: 12 create/edit fields confirmed; `golden_reference: compact` confirmed.
- Phase 1.5: Entity, CQRS, repository, DataTable and localization plan aligned to module pack.
- Phase 2: `ModuleCatalogItem` aggregate and MongoDB repository created for `platform_module_catalog`.
- Phase 3: CQRS commands/queries/handlers/validators, response envelope, pipeline behavior support and Platform API controller implemented.
- Phase 3.5: Gateway route is required but missing from `ocelot.json`; file was not modified because it is integration-agent owned.
- Phase 4: DataTable v2 compact UI, Create/Edit/Details/_Form pages, JS and 7 language resources implemented.
- Phase 5: Unit tests, backend build, frontend build, gateway build, JS syntax check and DataTable verifier executed.
- Phase 6: API/user documentation and this audit report created.

## Rule Coverage

- CQRS + MediatR: implemented under `Application/Features/ModuleCatalog`.
- Response envelope: new Module Catalog handlers return `Response<T>`.
- Pipeline behaviors: existing four behaviors remain registered; validation/exception behaviors now support `Response<T>` failures.
- MongoDB persistence: `ModuleCatalogRepository` uses `platform_module_catalog`.
- Soft delete: delete paths set `IsDeleted=true` via repository base.
- RBAC/JWT: controller uses `[Authorize(Policy = "PlatformActor")]` and `[HasPermission]`.
- Localization: `ModuleCatalogIndex.{en,fr,es,zh,ar,ru,tr}.resx` added.
- DataTable v2: verifier passed for Platform/ModuleCatalog compact.

## Open Risk

Gateway route `/api/platform/module-catalog` is absent in `gateway/Diten.ApiGateway/ocelot.json`. Per module pack instructions, this must be added later by the integration-agent; otherwise frontend calls through Gateway will return 404.

