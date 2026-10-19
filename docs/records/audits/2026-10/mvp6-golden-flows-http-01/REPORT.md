---
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E4
decision: conditional
commit: 67671dc41
modules: [MOD-0183, MOD-0184, MOD-0185, MOD-0186, MOD-0187]
---

# All five golden flows ran — over HTTP, not in a browser — and found four things no record named

    lane      : mvp6-golden-flows-http-01
    measured  : 2026-10-17 on a live six-service lane stack, after the owner granted Q496
    scope     : gate item 6 of the main-merge completion gate — "five SupplyChain golden flows rerun"
    verdict   : all five modules create and list successfully; persistence and audit are 1:1 in every
                module. FOUR findings, none of them previously recorded.
    stack     : gateway 5000 · auth 5056 · platform 5057 · mdm 5059 · supplychain 5066 · mongod 34994
    actor     : admin@diten.com (SuperAdmin), tenant 00000000-…-0001, legal entity b91b3bc1-… ACTIVE
    caveat    : HTTP, not browser. See "Scope".

## The five flows

| # | module | flow | result |
|---|---|---|---|
| 1 | MOD-0183 Shipment | `POST /shipments` | **201** `status: Draft` |
| 1b | | `POST /{id}/transition` → Planned → Dispatched → Delivered | **200, 200, 200** |
| 2 | MOD-0184 Carrier | `POST /carriers` | **201** `status: Active` |
| 3 | MOD-0185 Load | `POST /loads` | **201** `LOAD-…`, `status: Draft` |
| 4 | MOD-0186 Return | `POST /returns` | **201** `RMA-…` |
| 5 | MOD-0187 Claim | `POST /claims` | **201** `CLM-…` |
| — | all five | `GET /shipments｜carriers｜loads｜returns｜claims` | **200** each |

**Persistence and audit are 1:1 in every module** (`evidence/lane-db-all-modules.txt`):

```
shipments  3 aggregates, 6 mutations (3 create + 3 transition) -> 6 receipts, 6 history, 6 audit, 6 outbox
carriers   1 -> 1 carrier_idempotency, 1 carrier_audit
loads      1 -> 1 loads_receipts, 1 loads_audit, 1 loads_outbox, 1 load_assignments
returns    1 -> 1 returns_receipts, 1 returns_audit, 1 returns_outbox, 1 return_entitlements
claims     1 -> 1 claims_receipts, 1 claims_audit, 1 claims_outbox
```

No orphans in either direction. That is the SOP §23 E4 shape — persistence plus audit plus the
failure paths exercised below.

## What the flows refused, correctly — and it is worth recording as behaviour

Three refusals were **right**, and each one is a rule no record states plainly:

- **`409 CORRELATION_ROOT_MISMATCH` / `CLAIM_CORRELATION_MISMATCH`.** A Return or Claim must carry the
  **shipment's lifecycle-root correlation**, not a fresh one. Reusing the shipment's creation
  correlation turned both into 422s, then 201s.
- **`422 SHIPMENT_NOT_RETURNABLE` / `CLAIM_SHIPMENT_INELIGIBLE`.** A `Draft` shipment is neither
  returnable nor claimable. The lifecycle has to be driven Draft → Planned → Dispatched → Delivered
  first (`ShipmentLifecycle.Allows`), which the flow did.
- **`422 CLAIM_CARRIER_MISMATCH`.** A claim naming a `carrierId` that is not the shipment's is
  refused. The claim succeeded once the optional field was omitted.

These are the fail-closed behaviours the packs promise, observed rather than cited.

## Four findings

### F1 · The 25 `supplychain.*` keys are not sufficient — a 26th, from another module, is required

`MdmLegalEntityReferenceValidator:111-115` **forwards the caller's `Authorization` header** to MDM's
`GET /api/legal-entities/{id}/lookup-validation`. That endpoint carries
`[HasPermission("mdm.legal-entities.read")]`. So a caller holding all 25 `supplychain.*` keys and not
that one gets **503 `DEPENDENCY_UNAVAILABLE`** on every create.

Measured both ways: with the 25 keys alone, `POST /carriers` → 503. With `mdm.legal-entities.read`
added to the same token, same payload → **201**.

**This is a direct correction to Q496's premise.** Granting the 25 module keys does not make the
modules usable; it is 25 **plus one** from MDM. The coupling follows from R-2b removing the internal
service-identity path, which left the public `lookup-validation` route as the only one — so the
caller's own permissions now gate it. Nothing recorded that consequence.

### F2 · A dependency failure is reported to the caller as a schema error

`CarrierContractError.cs:12` maps status → message with no `503` case, so its `_ =>` default applies:
**"Request schema validation failed."** The 503 above therefore arrives as a *schema* complaint. A
developer reading it would check their payload — which was valid — instead of the dependency. CT lost
time to exactly that before reading the mapping.

### F3 · The gateway sends TWO correlation headers, and four of five modules reject that

Through the gateway, `POST /carriers｜loads｜returns` answer **400 `INVALID_REQUEST`** with
*"X-Correlation-Id must contain exactly one UUID value."* Direct on 5066, the identical request is
accepted. So `AddEdgeCorrelation()` **adds** its own value without removing the caller's, and the
middlewares' `Count != 1` check fires.

The contract declares `X-Correlation-Id` as a **required** parameter, so a caller doing what the
contract says cannot use four of the five modules through the public boundary. `POST /shipments`
tolerates it, which is why this went unnoticed — one module of five behaves differently.

This **extends BL-651**, which recorded the edge *replacing* the correlation and breaking replay. It
is worse than replacement: for four modules the first call fails too.

### F4 · The real gate was never permissions — it was legal-entity activation

A freshly created legal entity is `DRAFT` / `referenceable:false`, and `lookup-validation` answers
**404 "Legal Entity is not referenceable."** `PATCH /{id}/activate` moves it to ACTIVE, after which
the endpoint answers 200 and every flow works. No record names this step, and it gates all five
modules. It is also the thing v9.5 §1 half-found and attributed to "an unseeded scope".

## Scope — what this lane does and does not establish

- **It is HTTP, not browser.** The gate's row-1 evidence was taken through the UI in the r4a/r4b/r2/r4c
  lanes. This is a different instrument and does **not** replace it; a browser rerun is still open.
- **No GATE-18-0 cell is moved.** Rows 1 are UI-measured, and this lane measured the service boundary.
  The matrix stays at 43/56 local, 37/56 published.
- **Item 6 of the merge gate is satisfied in substance, not in form:** the five flows ran and passed at
  the service boundary, which is the first time they have run since the merges. Whether that discharges
  a requirement written when the flows were browser-measured is the owner's call, not CT's.
- Idempotent replay was measured in `mvp6-gateway-idempotency-replay-01`, not here.
