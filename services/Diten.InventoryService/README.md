# Diten.InventoryService — MOD-0173 Inventory Ledger & Valuation

The **single system of record for stock** in ERP-vNext (DCP-009 OD-5, AGENTS.md §3). Every stock change is an
append-only, idempotent `InventoryTransaction`; `InventoryBalance` is a projection of those transactions and is never
edited directly. Scope is Tenant + Legal Entity, taken from the signed JWT only.

| | |
|---|---|
| Module pack | `execution/domains/supply-chain-execution/module-packs/MOD-0173-inventory-ledger-valuation.md` (`ready-for-dev`) |
| Owned contract | `INVENTORY-BUNDLE` v1.0.0, `x-status: FROZEN` (`inventory-bundle.openapi.yaml`) |
| Owned contract (FU02) | `LOCATION` v1.1.0 (`location.openapi.yaml`, `x-owner: MOD-0173-FU02`) — v1 reads frozen, owner operations added in 1.1.0 |
| Consumed contracts | `PRODUCT-MASTER-BUNDLE` v1 (MOD-0290, Model A) — HTTP only. LOCATION is served by this service and read by the ledger in-process (FU02 LOC1) |
| Port | **5066** |
| Base paths | `/api/inventory` (ledger) · `/api/locations` (location master, MOD-0173-FU02) |
| Slices implemented | **S1 — Ledger core** + **S2 — Reservations** (+ FU-S1-01, FU-S1-02) + **FU02 LOC1 — Location master API**; valuation = S3 |
| API narrative | `docs/reference/architecture/api/mod-0173-inventory-bundle.md` · `docs/reference/architecture/api/mod-0173-location.md` |

## Layout

```
src/
  Diten.InventoryService.Api             Program.cs, InventoryController, CustomBaseController, request reader
  Diten.InventoryService.Application     CQRS (MediatR), 4 pipeline behaviors, validators, upstream client contracts
  Diten.InventoryService.Domain          entities, enums, direction matrix, bucket rule, UoM conversion (no Mongo/framework)
  Diten.InventoryService.Persistence     Mongo mappings, indexes, ledger + reservation repositories (atomic), SKU positions, expiry sweeper
  Diten.InventoryService.Infrastructure  JWT scope + correlation middleware, permissions, PRODUCT-MASTER / LOCATION HTTP clients
tests/
  Diten.InventoryService.Tests           domain, API (real host + real Mongo), architecture, contract schema, Prism smoke
  Diten.InventoryService.RuntimeProbe    runtime E4 probe console (used by runtime-probe/run-runtime-probe.ps1)
  runtime-probe/run-runtime-probe.ps1    real process on 5066 + Prism PRODUCT-MASTER mock + restart (locations via the owner API)
```

## Operations (S1 + S2)

| Method | Path | Permission | Notes |
|---|---|---|---|
| POST | `/api/inventory/movements` | `inventory.movements.create` | the ONLY way to change stock; `Idempotency-Key` header = body `idempotencyKey` |
| POST | `/api/inventory/reservations` | `inventory.reservations.manage` | SKU-level hold of AVAILABLE stock; `Idempotency-Key` = body key; 422 `INSUFFICIENT_AVAILABLE` |
| POST | `/api/inventory/reservations/{id}/release` | `inventory.reservations.manage` | `If-Match: W/"<version>"` required; stale / not ACTIVE → 409 `RESERVATION_CONFLICT` |
| GET | `/api/inventory/availability` | `inventory.stock.read` | `itemId` or `skuId` required; `reserved` = SKU-level ACTIVE holds; no stock → PRODUCT-MASTER decides (200 zeros / 404 / 503) |
| GET | `/api/inventory/balance` | `inventory.stock.read` | paged projection rows |
| GET | `/api/inventory/transactions` | `inventory.stock.read` | immutable history, newest posting first |
| GET | `/health` | anonymous | `{ "status": "Healthy", "module": "MOD-0173" }` |

There is no PUT / PATCH / DELETE on the ledger. A correction is a `REVERSAL` movement.

## Location master (MOD-0173-FU02, LOC1)

Eleven routes under `/api/locations`: the frozen v1 reads (`GET /{id}`, `GET /`, `GET /{id}/path`, `POST /validate`) and the
v1.1 owner operations (`POST /`, `PATCH /{id}`, `POST /{id}/status`, `POST /{id}/bins/bulk`, `GET /search`,
`GET /{id}/history`, `GET /{id}/stock-presence`). Reads need `inventory.locations.read`, writes
`inventory.locations.manage`. No DELETE — a location is archived. Every ledger posting re-checks its locations inside
the posting transaction (Active, stockable, under the warehouse) — details in `mod-0173-location.md`.

## Run locally

Prerequisites: .NET 8 runtime (SDK 8+), MongoDB **replica set** (multi-document transactions are required — the
local Windows service `MongoDB` runs `rs0` on 27017), Node 24 for the Prism mocks.

```bash
# Development: gateway-routed masters (http://localhost:5000/api/product-master/, /api/locations/)
dotnet run --project services/Diten.InventoryService/src/Diten.InventoryService.Api --launch-profile http

# Mock: Prism mocks on 4010 / 4011; JWT secret must be supplied by environment (appsettings.Mock.json has none)
JwtSettings__Secret=<32+ chars> dotnet run --project services/Diten.InventoryService/src/Diten.InventoryService.Api --launch-profile mock
```

Configuration (all overridable by environment variables, `__` as separator):

| Key | Meaning |
|---|---|
| `Mongo:ConnectionString` / `Mongo:DatabaseName` | secret-validated at startup (`ValidateRequiredSecrets`) |
| `JwtSettings:Secret` / `PreviousSecrets` / `Issuer` / `Audience` | same validation as every service (`JwtSecretRotationResolver`, `JwtValidationDefaults.ClockSkew`) |
| `Inventory:Upstream:ProductMasterBaseUrl` / `TimeoutSeconds` | consumed PRODUCT-MASTER; unreachable → 503, never a silent pass |
| `Inventory:Upstream:LocationMasterBaseUrl` | only for the LOCATION HTTP client's Prism contract smoke; the ledger reads locations in-process (FU02) |
| `Inventory:Upstream:OrgUnitDirectoryBaseUrl` | Platform organization units through the Gateway (`…/api/platform/organization-units/`), FU02: checks a location `orgUnitRef` on write; unreachable → 503 `ORG_UNIT_DIRECTORY_UNAVAILABLE` |
| `Inventory:Upstream:LegalEntityMasterBaseUrl` | MDM legal-entity master through the Gateway (`…/api/legal-entities/`), W-0173-02: verifies the `X-Legal-Entity-Id` of a token without a `legal_entity_id` claim; unreachable → 503 `LEGAL_ENTITY_MASTER_UNAVAILABLE` |
| `Inventory:Policy:EnforceLocationLegalEntityMatch` | default `true`; the host refuses to start with `false` outside the `Mock` environment |
| `Inventory:Reservations:SweeperEnabled` / `SweepIntervalSeconds` | periodic expiry sweeper (default on, 60 s); expiry also runs inside every create / release / decrease of the SKU |

⚠ Startup creates the MOD-0173 indexes and rebuilds the S2 `inventory_sku_positions` projection from the ledger +
reservations (idempotent, per-SKU transaction, nothing deleted), and **refuses to start** if either fails: the
unique indexes carry idempotency / single reversal / one row per dimension, and the reservation guards rely on
correct positions. Groups the rebuild cannot attribute (no complete tenant / LE / item / SKU / level scope) are logged
as a WARNING with their count — never guessed, never deleted; on a database that is not a DB-010 test database (name
without a `test` / `tests` / `itest` segment) any such group also **stops the start** (FU-S2-01).

Permissions are in the `inventory.*` namespace owned by this service (PKS-001 §4; owner decision 2026-10-01). The
former `supplychain.*` names have no alias.

Reservations (S2) hold quantity at **Tenant + LE + item + SKU + level** against AVAILABLE stock (pack
ASSUMPTION-0173-12..17): balance rows keep `reserved 0`; `/availability` shows the SKU-level total. A movement whose
`(sourceModule, sourceDocumentId, sourceLineId)` equals an ACTIVE reservation consumes it; reserved stock cannot be
issued to anyone else. There is no SuperAdmin shortcut: every endpoint needs its explicit permission (FU-S1-02).

Gateway route `/api/inventory/**` → 5066 is in `gateway/Diten.ApiGateway/ocelot.json` (INT-1). Registering the
permissions (module manifest) is **not** part of this service — it belongs to the UI follow-up (FU01).

## Tests

```bash
dotnet test services/Diten.InventoryService/tests/Diten.InventoryService.Tests
```

- Needs a transaction-capable Mongo (`INVENTORY_TEST_MONGO`, default `mongodb://127.0.0.1:27017`) and Node for the
  Prism smoke tests. Nothing is skipped: a missing prerequisite fails the run.
- DB-010: one fixed database `diten_inventory_s1_tests`, isolation by a fresh TenantId per test; the tenants a test
  host created are removed when it is disposed.
- Every API response is validated against the frozen `inventory-bundle.openapi.yaml` (status + schema + correlation
  header) by `ContractValidatingHandler`.

Runtime probe (real process, Prism mocks, restart):

```powershell
services/Diten.InventoryService/tests/runtime-probe/run-runtime-probe.ps1 -EvidenceDir <folder>
```
