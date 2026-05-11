# PSS-007 Subscription Feature Management - Batch 1 Audit

Date: 2026-05-07

## Scope

Batch 1 implements the data foundation for Platform Subscription Feature Management:

- `FeatureDefinition` and `FeatureCategory` global catalog entities.
- MongoDB repositories and indexes for feature code, feature slug, category code, status, category, and sort order.
- CQRS commands, queries, handlers, validators, mapping profiles, and API controllers for feature catalog create/update/read and category create/read.
- Focused application tests for normalization, Active category validation, duplicate feature code, and archived category assignment.

Plan mapping, archive/deactivate actions, full UI, entitlement enforcement, and gateway route changes remain out of Batch 1 per the approved module pack split.

## Standards Check

- Entity base type: `GlobalEntity`, matching the approved platform global catalog exception.
- Soft delete: Repository reads and updates use `GlobalRepository` execution filter with `IsDeleted == false`.
- Tenant isolation: Tenant ID is intentionally not present because the module pack defines this as platform system-of-record master data.
- Authorization: API controllers use `[Authorize(Policy = "PlatformActor")]` and PSS-007 permission keys.
- Response envelope: Commands and queries return `Response<T>` or `Response<NoContent>`.
- CQRS structure: Commands, queries, handlers, and validators are separated by action.
- Controller discipline: Controllers are thin and delegate to MediatR.
- Gateway: `ocelot.json` is a protected path and was not modified. Routes for `/api/platform/subscription-features` and `/api/platform/feature-categories` remain an integration task blocker.

## Verification

- `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug -p:OutDir=C:\Users\user\Desktop\ERP-vNext\.tmp\build\platform-api\` passed.
- `dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests/Diten.Platform.Application.Tests.csproj -c Debug -p:OutDir=C:\Users\user\Desktop\ERP-vNext\.tmp\test\platform-app\ --no-restore` did not complete because existing `TenantHandlersTests.cs` tests fail to compile before runtime execution.

## Known Blockers

- Gateway integration must add explicit Ocelot routes through the integration owner.
- Existing Platform test project compile failures in `TenantHandlersTests.cs` block full test execution.
- Batch 2 is required for `PlanFeatureMapping`, archive/deactivate, and stricter RowVersion/mapping hardening.
