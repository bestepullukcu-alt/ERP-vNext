# Findings

## GAP-SHIPMENT-ROOT-EMISSION-01 — OPEN product defect

The accepted Root R2 rule requires an upgraded producer to persist and emit the authoritative lifecycle root. The fresh create stored `CorrelationId` but did not store `LifecycleCorrelationId`; the detail response consequently returned `lifecycleCorrelationId: null` before and after restart.

Source evidence in the immutable snapshot:

- `Shipment.cs:24` defines only `CorrelationId`.
- `CreateShipmentHandler.cs:42` writes only `CorrelationId`.
- `ShipmentDetailMaterializer.cs:9` reads the distinct `LifecycleCorrelationId` BSON field.

Runtime evidence: `raw/db/final-persistence.json`, `raw/http/detail-delivered.json`, and `raw/http/restart-detail.json`.

No derivation, backfill, serializer change, or product fix was made. This prevents acceptance of the explicit root-emission criterion while leaving the other measured HTTP behavior intact.

## Evidence corrections

The first local probe assumed a response envelope although this controller returns the response body directly. Its test DB was dropped and every scenario was rerun. A later Mongo measurement queried tenant/LE as UUID values while the collections store strings; `raw/http/zero-write-recheck.json` and `raw/db/final-persistence.json` supersede that measurement with successful string-scoped queries.
