# Tenant Module Entitlements API

Base route:

`/api/platform/tenants/{tenantId}/commercial/module-entitlements`

All endpoints require `PlatformActor` authorization. Read endpoints use `platform.tenants.commercial.subscription.view`; mutation and assignment endpoints use `platform.tenants.commercial.subscription.assign`, matching the existing Commercial Subscription permission surface.

## Endpoints

- `GET /` returns merged plan projection and physical entitlement rows.
- `GET /effective-access/{moduleCode}` returns the effective runtime decision for one tenant/module pair.
- `GET /visible-modules` returns modules whose effective access is allowed.
- `GET /available-modules` returns assignable module catalog entries for the add entitlement form.
- `POST /` creates a physical `ManualOverride`, `Addon`, `Trial`, or `System` entitlement.
- `POST /{entitlementId}/enable` enables an existing physical entitlement.
- `POST /disable` disables an existing physical entitlement, or creates a disabled `ManualOverride` for a plan projection row.
- `PATCH /{entitlementId}/expiry` updates physical entitlement expiry.
- `DELETE /{entitlementId}/manual-override` soft-deletes a manual override.
- `POST /refresh-projection` is a no-op query-time projection refresh hook; it does not persist plan rows.

Plan source is never stored as `TenantModuleEntitlement`; it is computed from the tenant's active subscription plan `IncludedModuleKeys`.

## Related Tenant Details Surface

The Platform Tenant Details Commercial tab calls these endpoints through the same-origin `/Platform/Tenants/api/...` MVC proxy. Browser JavaScript must not call Platform service ports directly or create bearer tokens itself.
