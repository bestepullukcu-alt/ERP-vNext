# PSS-008 Module Details Assignment Inspection Audit

Date: 2026-05-08
Status: review

## Scope Delivered
- Added read-only Module Assignment inspection API contracts under Platform service.
- Added backed Subscription Plan assignment rows from `SubscriptionPlan.IncludedModuleKeys`.
- Added explicit tenant assignment degraded dependency state because no module-to-tenant assignment SoR/API exists.
- Extended Module Catalog Details `Assignments` tab with summary cards, plan section, tenant section, filters, loading, empty, error and degraded states.
- Preserved no-mutation boundary: no billing, quota, runtime enforcement, provisioning or assignment create/update/delete.

## SoR / Boundary Check
- Module identity is validated through Module Catalog.
- Plan assignment data is read from Subscription Plan ownership.
- Tenant assignment rows are not synthesized; tenant section degrades until a real Tenant Module Assignment source exists.
- Gateway `ocelot.json` was not edited because it is protected. Backend supports canonical `/api/platform/modules/{moduleCode}/assignments/*` and existing gateway-compatible `/api/platform/module-catalog/{moduleCode}/assignments/*`.

## Verification
- `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug -o .build/verify/platform-api` passed.
- `dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug -o .build/verify/frontend-web` passed.
- `node --check frontend/Diten.Web/wwwroot/assets/js/Platform/ModuleCatalog/module-assignments.js` passed.

## Test Notes
- Added focused Module Assignment query tests for overview, plan filters, missing module, degraded tenant dependency and invalid tenant source filter.
- `dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests/Diten.Platform.Application.Tests.csproj --filter ModuleAssignmentQueryTests -p:OutDir=.build/verify/platform-tests/` is blocked by existing non-PSS-008 compile errors in `ModulePageDescriptorRulesTests.cs` and `TenantHandlersTests.cs`.

## Remaining Gaps
- Tenant Module Assignment SoR/API remains missing; tenant rows/detail stay degraded by design.
- Gateway canonical `/api/platform/modules/{moduleCode}/assignments/*` route needs an integration-agent route task if the canonical route must be used through Ocelot.
- Browser smoke was not completed in this pass because the running local services are already active and auth/runtime state was not exercised.
