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
- `DemandPlanning__HistoryUnitMode` (non-secret; `SingleUnitInterim` until MOD-0290 is live,
  then `ProductMasterV1` after the producer route is verified)

Demand v2 DRAFT event delivery is disabled by default and cannot be enabled in
Production. Development or IntegrationTest may opt in with
`EventTransport__Enabled=true`, `EventTransport__RabbitMqHost`,
`EventTransport__RabbitMqPort`, `EventTransport__RabbitMqVirtualHost`,
`EventTransport__RabbitMqUsername` and `EventTransport__RabbitMqPassword`.
The worker claims already committed revision outbox intents and records retry or
delivery state. Delivery is at least once; consumers must deduplicate by event ID.
The local Mongo/fake-transport tests do not prove RabbitMQ delivery or authorize
live Demand v2 consumers. Superseded content remains closed until MOD-0189
provides an approved server-verifiable bound-run contract.

`JwtSettings__Issuer` and `JwtSettings__Audience` are non-secret and have the
same values as the existing Auth/CRM configuration. No Gateway route is part of
this service-owned setup.

## Open authorization and data-source ledger

- **Yetkili şirketlerim bekliyor (central team, after all MVPs):** The interim
  selection lists all active LegalEntities of the tenant via MDM
  `GET /api/legal-entities/lookup` through Gateway. A single Planning-owned
  LegalEntity scope reader must validate the selected ID through MDM
  `lookup-validation` on every operation; unavailable MDM fails closed with 503.
  The interim rule does **not** check the user's position-based entitlement to
  that company. Demand action permission, tenant and record/series scope checks
  remain required. Do not report AC-D27 as complete. The eventual Platform
  reader replaces this reader's implementation, not its consumers.
- **Accepted history is separate from batch approval:** only explicit, audited
  acceptance of validated file rows can supply Phase 1 forecast input. Code and
  Mongo tests exist; real-data browser proof remains open.
  MOD-0183 realized shipment rows and file/live deduplication are Phase 2 and
  await the producer contract addendum. Unknown stockout is not zero demand.
- **Coverage declaration (Phase 1):** each multipart history import requires a
  `CoverageFile` JSON attachment in addition to `File` CSV. Each entry names one
  complete ISO Monday week for one `skuId`, `skuLevel`, `warehouseId`, and `uomId`:

  ```json
  [{"skuId":"00000000-0000-0000-0000-000000000001","skuLevel":"Lsku","warehouseId":"wh-01","uomId":"EA","weekStart":"2026-09-14","stockState":"InStock"}]
  ```

  `stockState` may be omitted; `OutOfStock` or `Unknown` prevents that week from
  becoming reliable demand. A week outside the declaration is unknown. An empty
  declared week is zero only after independent batch review and separate acceptance
  of every relevant file row. The coverage file hash is included in the batch
  idempotency fingerprint and review audit; older batches without a declaration
  cannot be newly approved for accepted history.
- **Other producer gates:** Platform Working Calendar supplies business days and
  holidays, while ISO Monday supplies week boundaries. Calendar year plus ISO
  week is the interim fiscal rule; MDM `FiscalYearVariant`
  is not read (BL-629). Tenant `Timezone` is interim (BL-628). Frozen
  PRODUCT-MASTER v1.0 `getSku`/`getSkuUom` can be tested with Prism responses,
  but no live producer route is claimed. V1.0 only permits same-base-unit
  verification; a different unit needs dated `conversionId` and `asOf` from
  v1.2 (no delivery date), or stays quarantined.
