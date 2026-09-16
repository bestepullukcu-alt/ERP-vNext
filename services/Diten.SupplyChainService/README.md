# MOD-0183 bounded Shipment Tracking & POD

Five-layer .NET 8 service on 127.0.0.1:5061. Backend R1 core only, not complete
SHIPMENT-BUNDLE, module pack or DCP-009 acceptance.

## Configuration

Required environment variables: Mongo__ConnectionString, Mongo__DatabaseName,
JwtSettings__Issuer, JwtSettings__Audience and JwtSettings__Secret.
Use a replica set or mongos supporting transactions. The JWT HMAC secret must
be at least 32 UTF-8 bytes and supplied through deployment secrets.

JWT requires exactly one UUID tenant_id, legal_entity_id and sub. X-Tenant-Id
and X-Legal-Entity-Id must match the signed claims. Live issuer integration for
LE scope is held; unsigned headers never confer scope. JWT uses the existing
shared clock-skew default and matching IdentityModel parser dependencies.

Every operation requires UUID X-Correlation-Id; mutations require Idempotency-Key
(1–128 characters). Under approved readiness A5, all mutations of a shipment
use the first command's UUID. Different-root mutation returns containment 400;
GAP-0183-04 still needs contract-owner resolution.

## HTTP surface

| Method | Path | Permission suffix |
|---|---|---|
| GET | /api/shipment-bundle/shipments | read |
| POST | /api/shipment-bundle/shipments | create |
| GET | /api/shipment-bundle/shipments/{shipmentId} | read |
| POST | /api/shipment-bundle/shipments/{shipmentId}/transition | cancel for Cancelled; otherwise dispatch |
| POST | /api/shipment-bundle/shipments/{shipmentId}/pod | pod.capture |

Permission prefix: supplychain.shipments.
Health reports process startup only. OPTIONS bypasses scope validation;
CORS/gateway registration is integration-owned.

## Persistence and consistency

Application uses CQRS/MediatR, four behaviors and internal Response<T>.
CustomBaseController adapts to exact unwrapped wire payloads. Domain has no
driver/framework dependency; Persistence owns Mongo. Lines and POD are embedded
in the scoped Shipment. History, sanitized audit, receipts and outbox are separate
collections written in the same snapshot/majority transaction.

Replay key is tenant + LE + operation + idempotency key; fingerprint includes
normalized payload and target ID. Replay returns its original durable snapshot,
not current state. Create replay returns 200, POD replay 201. Authorization,
scope and soft-delete visibility also apply to replay. Mongo transaction/version
checks serialize competing updates. No HTTP failure-injection endpoint exists.

Outbox ID, payload bytes and correlation remain immutable. Without a registered
integration-owned transport, events remain Pending. Tests use the production
processor and Mongo store with a failing transport mock; this proves no broker delivery.
Warehouse/Inventory references are opaque, not validated master records.
No automatic Warehouse intake, Inventory client, stock balance, foreign database,
other modules, UI or shared registrations are implemented.

## Reproduce

Use only an isolated Mongo replica set. Fixed test databases are
diten_mod0183_tests and diten_mod0183_runtime_tests; each test has fresh tenant
scope. Tests retain only isolated fixtures for evidence.

~~~sh
dotnet restore services/Diten.SupplyChainService/Diten.SupplyChainService.sln
dotnet build services/Diten.SupplyChainService/Diten.SupplyChainService.sln --no-restore
MOD0183_TEST_MONGO='mongodb://127.0.0.1:27027/?replicaSet=mod0183verify' dotnet test services/Diten.SupplyChainService/Diten.SupplyChainService.sln --no-restore
dotnet test tests/architecture/TenantArchitecture.ArchitectureTests --no-restore
MOD0183_TEST_MONGO='mongodb://127.0.0.1:27027/?replicaSet=mod0183verify' python3 services/Diten.SupplyChainService/tests/runtime_probe.py /private/tmp/mod0183-evidence docs/analysis/contracts/shipment-bundle.openapi.yaml
python3 services/Diten.SupplyChainService/tests/startup_probe.py /private/tmp/mod0183-evidence
~~~

Python probes require PyYAML, jsonschema and mongod/mongosh. They refuse occupied
ports. Runtime probe starts/stops its own service process twice; startup probe
uses disposable standalone Mongo on 27028.

Startup creates additive indexes and checks transactions before listening.
Rollback is stopping the service while retaining records and pending events.
Never delete operational data or point these tests at operational databases.
