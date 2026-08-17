# MOD-0297 Tenant Subscription Management Audit

Date: 2026-05-11

## Scope
- Added `TenantSubscription` as the commercial subscription system-of-record for platform tenant lifecycle management.
- Added commercial subscription CQRS commands/queries, validators, repository implementation, MongoDB indexes, API controller, Gateway routes, and Tenant Details Commercial tab integration.

## Standards Check
- Module pack status was `approved`.
- Platform scope stayed inside `services/Diten.Platform`, `frontend/Diten.Web`, `gateway/Diten.ApiGateway`, and docs.
- Browser JavaScript uses the existing same-origin `Platform/Tenants` proxy profile; it does not create bearer tokens or call service ports directly.
- UI uses Bootstrap/Sneat utility classes and existing components. No new CSS file or inline style block was added.
- Platform localization keys were added for `en` and `tr`.
- API responses use the existing `Response<T>` envelope and `CustomBaseController`.
- Lifecycle write actions validate legal state transitions and use `RowVersion` concurrency checks.

## Verification
- `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug -o .build\platform-api` passed.
- `dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug -o .build\diten-web` passed.
- `gateway/Diten.ApiGateway/ocelot.json` parses successfully as JSON.

## Notes
- The normal Platform API build output was locked by a running `Diten.Platform.API` process, so build verification used a separate output directory.
- Runtime browser smoke test was not executed in this pass.
