# MOD-0298 Tenant Module Entitlements Audit

Date: 2026-05-11
Domain: platform-shared-services
Module pack: `execution/domains/platform-shared-services/module-packs/MOD-0298-tenant-module-entitlements.md`

## Scope

Implemented tenant module entitlement backend contracts and the Platform Tenant Details > Commercial > Module Entitlements surface.

## Standards Check

- Module pack status was `approved`.
- Physical entitlement source enum excludes `Plan`; plan access is query-time projection from active subscription plan `IncludedModuleKeys`.
- Physical records are stored in `tenant_module_entitlements` with `TenantId`, `IsDeleted`, `RowVersion`, expiry and reason fields.
- Repository methods always include `TenantId` and `IsDeleted=false` for tenant entitlement operations.
- API responses use `Response<T>` and `CustomBaseController`.
- Platform admin frontend uses same-origin MVC proxy under `/Platform/Tenants/api/...`; browser JavaScript does not create bearer tokens or call service ports directly.
- Commercial subtab uses DataTables v2 marker, inline filter collapse, Select2 filters, `_IndexL10n` JSON bridge, and `_LayoutPlatformAdmin` through the parent details view.
- Platform-specific localization keys were added for `en` and `tr`.

## Verification

- `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug -o .build/platform-api-check` passed.
- `dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug -o .build/web-check /p:UseAppHost=false` passed.
- Normal builds to `bin/Debug` were blocked by already-running `Diten.Platform.API (12076)` and `Diten.Web (9040)` processes locking output files.
- `dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests/Diten.Platform.Application.Tests.csproj -c Debug -o .build/platform-tests-check` did not reach execution because existing test sources currently fail to compile in unrelated `ModulePageDescriptorRulesTests` and `TenantHandlersTests` files.

## Notes

- Gateway `ocelot.json` was not edited because it is protected for integration-agent ownership. Existing `/api/platform/tenants/{everything}` route covers the new nested endpoints.
- Runtime browser smoke was not performed in this pass because the active app processes are already running with old binaries; a restart is required to load the new build artifacts.
