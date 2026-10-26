# Diten.SafetyStockService (MOD-0191)

Temporary service on `127.0.0.1:5069`. The policy GET routes read only verified
Mongo records. The evaluation GET remains unavailable until evaluation persistence
is approved. Method A fixtures and in-memory state are not production records.

Configuration values are supplied outside source control:

- `JwtSettings__Secret`: required at startup, at least 32 UTF-8 bytes.
- `JwtSettings__Issuer` and `JwtSettings__Audience`: token validation values.
- `Mdm__LegalEntitiesUrl`: base URL ending in `/api/legal-entities/`; the
  caller's bearer token is forwarded to `lookup-validation`. An unavailable or
  malformed response returns 503.
- `Mdm__ProductMasterUrl`: PRODUCT-MASTER base URL for `getSku`; the caller's
  bearer token is forwarded. Manual quantities are unavailable without an active
  SKU and its base unit. The endpoint does not convert units.
- `Mongo__ConnectionString`: required for policy writes and reads; use a
  deployment secret or environment variable, never source control.
- `Mongo__DatabaseName`: shared database name (default `DitenERP`).
- `PlatformRegistration__BaseUrl` and `PlatformRegistration__InternalApiKey`:
  Platform self-registration endpoint and internal key. Registration is skipped
  with a warning when these are not configured.

The temporary read boundary is exact read permission + token Tenant +
MDM-referenceable selected LegalEntity. It does **not** prove that the actor is
assigned to the company or entitled to particular SKU/Warehouse records. The
Platform company-entitlement and series-scope contracts remain open. The four
approved mutation permissions are API-only in the module manifest. Each
successful command and local audit record commit in one Mongo transaction;
BL-501 central audit and audit visibility remain open. Browser traffic must use a separately approved Gateway route;
the direct port is for service tests only.

Manual safety-stock quantities require a reason, approved policy revision and
independent review. Their effective read uses an explicit `asOf` date; MRP
would supply the ISO week's Monday. This service
does not yet publish a cross-module quantity contract, so MRP must continue to
mark safety stock as omitted until that contract is approved and integrated.
