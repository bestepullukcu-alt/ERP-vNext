# Diten.InventoryService — MOD-0173 Inventory Ledger & Valuation

The **single system of record for stock** in ERP-vNext (DCP-009 OD-5, AGENTS.md §3). Every stock change is an
append-only, idempotent `InventoryTransaction`; `InventoryBalance` is a projection of those transactions and is never
edited directly. Scope is Tenant + Legal Entity, taken from the signed JWT only.

| | |
|---|---|
| Module pack | `execution/domains/supply-chain-execution/module-packs/MOD-0173-inventory-ledger-valuation.md` (`ready-for-dev`) |
| Owned contract | `INVENTORY-BUNDLE` v1.0.0, `x-status: FROZEN` (`inventory-bundle.openapi.yaml`) |
| Consumed contracts | `PRODUCT-MASTER-BUNDLE` v1 (MOD-0290, Model A), `LOCATION` v1 — HTTP only |
| Port | **5066** |
| Base path | `/api/inventory` |
| Slice implemented | **S1 — Ledger core** (reservations = S2, valuation = S3) |
| API narrative | `docs/reference/architecture/api/mod-0173-inventory-bundle.md` |

## Layout

```
src/
  Diten.InventoryService.Api             Program.cs, InventoryController, CustomBaseController, request reader
  Diten.InventoryService.Application     CQRS (MediatR), 4 pipeline behaviors, validators, upstream client contracts
  Diten.InventoryService.Domain          entities, enums, direction matrix, bucket rule, UoM conversion (no Mongo/framework)
  Diten.InventoryService.Persistence     Mongo mappings, indexes, InventoryLedgerRepository (atomic posting)
  Diten.InventoryService.Infrastructure  JWT scope + correlation middleware, permissions, PRODUCT-MASTER / LOCATION HTTP clients
tests/
  Diten.InventoryService.Tests           domain, API (real host + real Mongo), architecture, contract schema, Prism smoke
  Diten.InventoryService.RuntimeProbe    runtime E4 probe console (used by runtime-probe/run-runtime-probe.ps1)
  runtime-probe/run-runtime-probe.ps1    real process on 5066 + Prism mocks + restart
```

## Operations (S1)

| Method | Path | Permission | Notes |
|---|---|---|---|
| POST | `/api/inventory/movements` | `supplychain.inventory.movements.post` | the ONLY way to change stock; `Idempotency-Key` header = body `idempotencyKey` |
| GET | `/api/inventory/availability` | `supplychain.inventory.read` | `itemId` or `skuId` required |
| GET | `/api/inventory/balance` | `supplychain.inventory.read` | paged projection rows |
| GET | `/api/inventory/transactions` | `supplychain.inventory.read` | immutable history, newest posting first |
| GET | `/health` | anonymous | `{ "status": "Healthy", "module": "MOD-0173" }` |

There is no PUT / PATCH / DELETE. A correction is a `REVERSAL` movement.

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
| `Inventory:Upstream:ProductMasterBaseUrl` / `LocationMasterBaseUrl` / `TimeoutSeconds` | consumed masters; unreachable → 503, never a silent pass |
| `Inventory:Policy:EnforceLocationLegalEntityMatch` | default `true`; the host refuses to start with `false` outside the `Mock` environment |

⚠ Startup creates the MOD-0173 indexes and **refuses to start** if it cannot: the unique indexes carry idempotency,
single reversal and one-row-per-dimension, so the ledger must not accept postings without them.

⚠ Gateway route `/api/inventory/**` → 5066 and the permission seed are **not** part of this service (INT-1,
integration-agent). Until INT-1 lands, callers reach the service directly on 5066.

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
