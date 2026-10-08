# Diten.PlanningService (temporary)

The local API listens on `127.0.0.1:5068`. Its `/health` endpoint is anonymous and
reports Mongo connectivity without exposing tenant data. A missing JWT signing
secret prevents startup; an unavailable Mongo connection makes `/health` return
503 rather than reporting a healthy service.

Supply secrets through environment variables or .NET user-secrets, not tracked
settings files. Configuration names:

- `JwtSettings__Secret`
- `Mongo__ConnectionString`
- `Mongo__SupplyChainDatabaseName` (non-secret; defaults to the approved `DitenERP` name)
- `DemandPlanning__SnapshotCursorSigningKey`

`JwtSettings__Issuer` and `JwtSettings__Audience` are non-secret and have the
same values as the existing Auth/CRM configuration. No Gateway route is part of
this service-owned setup.
