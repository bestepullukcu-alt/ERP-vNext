# Gateway parity and two contract response gaps, measured against the running stack

**Date:** 2026-10-13 · **Branch:** `feature/sce/mod-0190-0192-bounded-slice`
**Instrument:** the live stack from `mvp6-stage-r-stack-up-01`, a token for a user holding none of the 25
`supplychain.*` keys, and the frozen contract on this branch.

## 1. The gateway catch-all routes the family faithfully

Rows 3 for MOD-0186 and MOD-0187 are NOT MET on *"pack routing contract contradicted by the running
catch-all"*. That is the integration owner's to resolve (`gateway/**` is Protected Paths in every pack), but
what the catch-all actually **does** is measurable, and it had not been measured.

The same request, same headers, same token, sent twice — once straight at the service, once through the
gateway:

| path | direct 5066 | gateway 5000 |
|---|---:|---:|
| `/api/shipment-bundle/returns` | 403 | 403 |
| `/api/shipment-bundle/claims` | 403 | 403 |
| `/api/shipment-bundle/carriers` | 403 | 403 |
| `/api/shipment-bundle/loads` | 403 | 403 |
| `/api/shipment-bundle/shipments` | 404 | 404 |

**Identical on every path.** So `/api/shipment-bundle/{everything}` → `localhost:5066` is not swallowing,
rewriting or misrouting anything: the gateway is transparent for this family, and the 403s are SupplyChain's
own permission refusals.

That does not close rows 3. It narrows them: the contradiction the cells name is **documentary** — the packs
declare explicit per-module routes while the running gateway serves one catch-all — and not a live routing
defect. The owner is deciding about contract text, not about broken traffic.

## 2. A finding this lane started to write and then withdrew

The 404 on `/shipments` looked at first like a list endpoint answering an empty scope with an error instead
of an empty list, which would have borne directly on row 12. It is not that, and the service log says so:

```
GET /api/shipment-bundle/shipments
  → GET http://127.0.0.1:5059/api/legal-entities/0000…0001/lookup-validation   → 404
  → 404  SHIPMENT_NOT_FOUND
```

The legal entity in the request headers does not exist in this freshly seeded MDM, so `R-2`'s fail-closed
validator refused the request before any list ran. `GetShipmentListHandler` has no 404 path at all — it
always returns `Success`.

And the code it refuses with is **deliberate**, not a misattribution:
`MdmLegalEntityScopeValidator` maps MDM `NotFound` → `NotReferenceable` and
`ShipmentContextMiddleware:40` turns that into `404 SHIPMENT_NOT_FOUND` "Shipment not found.", while
`Forbidden` and every other outcome map to `503 DEPENDENCY_UNAVAILABLE`. Answering "shipment not found"
rather than "that legal entity does not exist" is a non-disclosure choice — the other answer would tell a
caller which legal entities exist outside its own scope. There is even a `ShipmentTelemetry.ScopeDenials`
counter on that path.

Recorded because the withdrawal is the useful part: the measurement that looked like a defect was the
tenant-isolation design working.

## 3. Two real gaps, in the contract's declared responses

Measured by parsing the contract on this branch, GET on each list path:

| path | declared responses |
|---|---|
| `/shipments` | 200, 400, 503 |
| `/carriers` | 200, 400, 401, 403, 404, 500, 503 |
| `/loads` | 200, 400, 401, 403, 404, 500, 503 |
| `/returns` | 200, 400, 401, 403, 404, 500, 503 |
| `/claims` | 200, 400, 401, 403, 500, 503 |

**`/shipments` GET under-declares what its own service returns.** Three codes, where its four siblings
declare seven. And `ShipmentContextMiddleware` demonstrably answers **401** (`:19`), **403** (`:25`),
**404** (`:29`, `:40`) and **503** (`:42`) on exactly that path — the 404 was observed live in §2 above.
So a consumer generating a client from the contract would not know that four of the statuses it will meet
exist. MOD-0183 is the lead module of the bundle, which makes it the worst place for this gap.

**`/claims` GET omits 404** while carriers, loads and returns declare it. Documentary only: this lane could
not call `/claims` for want of permissions, so whether the service returns 404 there is unmeasured.

Neither gap was introduced by the 3.3.0 amendment pushed today on
`contracts/shipment-bundle-3.3-demand-history`: that change was additive and verified node-by-node to remove
nothing and to change no existing meaning. Both gaps are in 3.2.0 and are inherited.
