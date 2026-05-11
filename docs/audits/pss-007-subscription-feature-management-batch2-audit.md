# PSS-007 Subscription Feature Management - Batch 2 Audit

Date: 2026-05-08

## Scope

Batch 2 completes the MVP surface that remained after the data foundation:

- `PlanFeatureMapping` global persistence with unique `SubscriptionPlanId + FeatureDefinitionId` index.
- Plan mapping CQRS/API endpoints for plan-level reads and updates.
- Feature-scoped mapping read endpoint to support the MVP edit drawer.
- Archive and deactivate commands with `RowVersion` conflict handling.
- Platform Admin card-grid UI with feature create/edit, category create, filter/search states, and plan availability editing.
- MVC same-origin proxy so browser JavaScript does not call Platform service ports directly.
- Explicit Ocelot routes for subscription features and feature categories.

## Standards Check

- `SubscriptionPlan` is consumed through `ISubscriptionPlanRepository`; no plan entity duplication or creation path was added.
- Mapping rejects missing/inactive plans, duplicate mapping payloads, missing features, and archived feature mappings except `NotAvailable`.
- Runtime entitlement enforcement, tenant overrides, billing, quota enforcement, and tenant self-service flows remain out of scope.
- Platform API controllers use `[Authorize(Policy = "PlatformActor")]` and PSS-007 permission keys.
- Frontend uses `_LayoutPlatformAdmin`, `Views/Platform/SubscriptionFeatures`, partialized Razor, and same-origin proxy calls under `/Platform/SubscriptionFeatures/api`.
- Gateway routes were added narrowly for `/api/platform/subscription-features` and `/api/platform/feature-categories`.

## Verification

- `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug -p:OutDir=C:\Users\user\Desktop\ERP-vNext\.tmp\build\platform-api\` passed.
- `dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug -p:OutDir=C:\Users\user\Desktop\ERP-vNext\.tmp\build\frontend-web\` passed.
- `dotnet build gateway/Diten.ApiGateway/Diten.ApiGateway.csproj -c Debug -p:OutDir=C:\Users\user\Desktop\ERP-vNext\.tmp\build\gateway\` passed.
- `dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests/Diten.Platform.Application.Tests.csproj -c Debug -p:OutDir=C:\Users\user\Desktop\ERP-vNext\.tmp\test\platform-app\ --no-restore` remains blocked by pre-existing `TenantHandlersTests.cs` compile errors.

## Residual Risk

- Full runtime browser smoke was not executed in this pass.
- Platform test project cannot run until the existing `TenantHandlersTests.cs` compile errors are fixed.
