# INVENTORY-BUNDLE 1.1.0 — proposal (additive)

**Status:** DRAFT proposal, MVP-1 Control Tower, 2026-10-09 — for review by MOD-0290 (MDM). Not frozen.
**Base:** `docs/analysis/contracts/inventory-bundle.openapi.yaml` 1.0.2 (FROZEN, owner MOD-0173).
**Why:** MDM PRODUCT-MASTER 1.1 (`origin/chore/ct-round-2`, `1.1.0-draft.7`) stocks material items at a new level `Item`
(`skuId == itemId`), and MDM needs a stock-presence check before it retires an item / SKU (requirement R-12,
`docs/roadmap/plans/mod-0290-product-master-bundle-requirements-for-mvp1-2026-10-02.md` §8.2, MDM expectation of 2026-10-09).
**Rule:** additive only — no existing path, parameter, property, type, required list or status code changes.

## Change 1 — `SkuLevel` gains `Item`

```yaml
SkuLevel:
  type: string
  enum: [Gsku, Lsku, FinishedGood, Item]
  description: >-
    Polymorphic stock level. Item = a material item (raw material, packaging, intermediate) that is its own SKU
    (skuId == itemId, PRODUCT-MASTER 1.1). Stock-level rules (OD-10): serial-tracked stock is held at Lsku / FinishedGood;
    a Gsku-level movement is accepted only when the product's serial flag is explicitly false. Item-level stock carries no
    serials (PRODUCT-MASTER returns serialControlled: false for items).
```

- Applies everywhere `SkuLevel` is used (`postMovement`, `getAvailability`, `getBalance`, `listTransactions`, reservations).
- Consumers of INVENTORY responses must accept the new value.
- No change of the default level for existing products.

## Change 2 — new operation `getStockPresence` (service-to-service)

| | |
|---|---|
| Path | `GET /api/internal/inventory/stock-presence` — on InventoryService directly (service-to-service). **Not routed through the Gateway**; the Gateway answers 404 for it (route-coverage test). |
| Caller | a service identity, never a user token: header `X-Internal-Api-Key` (one key per calling service, identifier + secret, the Platform internal-API pattern). First caller: MDM (`ditenmdmservice`). |
| Tenant | header `X-Tenant-Id` (UUID, required). The answer covers only that tenant. |
| Query | `skuId` (UUID, required), `skuLevel` (`SkuLevel`, required — `Item` for material items, `Gsku` / `Lsku` / `FinishedGood` later). |
| Answer 200 | `{ skuId, skuLevel, hasNonZeroStock, coverage, asOf, contractVersion }` — **no quantities**. |
| `hasNonZeroStock` | `true` when ANY legal entity of the tenant holds a non-zero balance of this SKU in ANY stock status (available, quarantine, quality inspection, blocked, in transit, reserved). A negative balance is non-zero. |
| `coverage` | `COMPLETE` when every legal entity of the tenant was read; `PARTIAL` reserved for a future partitioned store (MDM treats `PARTIAL` as no answer). Today a read failure is a 503, never `PARTIAL`. |
| `asOf` | UTC timestamp (`date-time`) of the projection read. |
| Never-stocked SKU | 200 with `hasNonZeroStock: false` (no 404 — INVENTORY does not validate the SKU against PRODUCT-MASTER here). |
| Errors (contract `Error` body) | 400 `INVALID_REQUEST` (missing / malformed `skuId`, unknown `skuLevel`, missing `X-Tenant-Id`); 401 (missing / wrong key); 503 `INVENTORY_UNAVAILABLE` (store unreadable). |
| Time | target p95 < 500 ms; MDM's budget is 2 s with no retry — any timeout / 5xx / unreadable answer makes MDM refuse the retirement (fail-closed). |
| Audit | every call is logged and audited: caller identifier, tenant, skuId, skuLevel, result, correlation id. |

```yaml
StockPresence:
  type: object
  required: [skuId, skuLevel, hasNonZeroStock, coverage, asOf, contractVersion]
  properties:
    skuId: { type: string, format: uuid }
    skuLevel: { $ref: '#/components/schemas/SkuLevel' }
    hasNonZeroStock: { type: boolean }
    coverage: { type: string, enum: [COMPLETE, PARTIAL] }
    asOf: { type: string, format: date-time }
    contractVersion: { type: string }
```

Not in 1.1 (later, additive): bulk form (`POST …/stock-presence:bulk`, at most 200 SKUs) when MDM needs it.

## Change log line (when frozen)

`1.1.0 — additive (MVP-1 CT, MOD-0290 R-12 and PRODUCT-MASTER 1.1): SkuLevel + Item; getStockPresence (service-to-service,
X-Internal-Api-Key + X-Tenant-Id, no quantities). No existing path, parameter, property, type, required list or status code
changed.`

## Open points for MDM

1. Confirm the path and that MDM calls InventoryService directly (not through the Gateway).
2. Confirm the key identifier (`ditenmdmservice`) and how the secret is provisioned (same as the Platform internal-event key).
3. Is `skuLevel` always known on the MDM side when it asks (the draft makes it required)?
