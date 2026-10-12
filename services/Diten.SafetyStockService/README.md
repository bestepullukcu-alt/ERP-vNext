# Diten.SafetyStockService (MOD-0191)

Temporary service on `127.0.0.1:5069`. The four GET routes under
`/api/v1/safety-stock/policies` are read-only. They do not expose Method A
fixtures or the in-memory policy state as persisted business records.

Configuration values are supplied outside source control:

- `JwtSettings__Secret`: required at startup, at least 32 UTF-8 bytes.
- `JwtSettings__Issuer` and `JwtSettings__Audience`: token validation values.
- `Mdm__LegalEntitiesUrl`: base URL ending in `/api/legal-entities/`; the
  caller's bearer token is forwarded to `lookup-validation`. An unavailable or
  malformed response returns 503.

The temporary read boundary is exact read permission + token Tenant +
MDM-referenceable selected LegalEntity. It does **not** prove that the actor is
assigned to the company or entitled to particular SKU/Warehouse records. The
Platform company-entitlement and series-scope contracts remain open. There is
no persisted policy/evaluation reader yet, so authorized requests currently
return 503. Do not bind a record store without resolving and reviewing this
scope risk. Browser traffic must use a separately approved Gateway route;
the direct port is for service tests only.
