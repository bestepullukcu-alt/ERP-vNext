---
module: MOD-0183
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E4
decision: pass
commit:
modules: [MOD-0183, MOD-0184, MOD-0185, MOD-0186, MOD-0187]
---

# G5 — end-to-end chain through the gateway: shipment → carrier/load → POD → return/claim, reconciled

    run    : 2026-10-19 by CT (gmg-10) · owner: "continue development MVP6 and try finish asap"
    why    : MOD-0183 §16 "G5 evidence plan (shipment→carrier/load→POD→return/claim, reconciliation, regression)" was the
             module's only NOT MET criterion (§16 evidence audit 2026-10-19). Q151 run 2 drove part of the chain via a
             direct transition to Delivered — no POD capture and no reconciliation.
    stack  : guarded lane — gateway 5000 (signing key), SupplyChain 5066, MDM 5059, mongod 127.0.0.1:35190 rs `rsg6`
             (27017 untouched). Token in the **R-2 shape AuthService issues** (tenant, actor, permissions; no
             legal_entity_id); legal entity from `X-Legal-Entity-Id`, validated by MDM. Script: lane scratchpad `g5_run.py`.
    verdict: **PASS.**

## Chain (all `http://localhost:5000/api/shipment-bundle`, one shipment, its gateway-signed root echoed on every mutation)

| step | call | result |
|---|---|---|
| 1 | `POST /shipments` | 201 Draft — root issued by the gateway |
| 2 | `POST /shipments/{id}/transition` Planned | 200 |
| 3 | `POST /carriers` | 201 Active |
| 4 | `POST /loads` (carrier + this shipment) | 201 Draft |
| 5 | load → Planned → Tendered → Accepted → Dispatched | 200 ×4 |
| 6 | shipment → Dispatched → InTransit | 200 ×2 |
| 7 | `POST /shipments/{id}/pod` | **201 → Delivered** (POD capture, not a direct transition) |
| 8 | load → Completed | 200 |
| 9 | `POST /returns` + → Authorized | 201, 200 |
| 10 | `POST /claims` + → Investigating | 201, 200 |
| 11 | `GET /shipments/{id}` | 200, status **Delivered** |

## Reconciliation (lane DB, these aggregates only — `evidence/reconcile.txt`)

| aggregate | rows | correlation |
|---|---|---|
| shipment | 1 doc · history 5 · audit 5 · outbox **6** (Created, Planned, Dispatched, InTransit, Delivered, PodCaptured) | every outbox row = root |
| load | audit 6 · outbox 6 (create + 5 arrows) | every envelope = root |
| return | audit 2 · outbox 2 | audit `CorrelationRoot` and envelopes = root |
| claim | outbox 2 | audit `CorrelationRoot` and envelopes = root |
| carrier | 1 | — (own request trace) |
| finance / stock | **no** collection matching payment/payable/receivable/invoice/ledger/stock/balance/movement in the module DB | — |

One fact found on the way (kept, not a defect): shipment transitions require **UTC** `occurredAt`
(`TransitionShipmentValidator`: `Offset == Zero`); loads accept an offset. The first attempt with `+01:00` answered
400 INVALID_REQUEST and wrote nothing; the UTC run is the one recorded.

## Regression

Same day: SupplyChain suite on the lane — see the commit that carries this record; new §16 guard facts
(`ShipmentTests.SameKeyTransitionReplay…`, `…CrossScopeMutationsAre404…`, `ClaimSettlementBoundaryTests`,
`ReturnEventCorrelationTests`, `LoadEventCorrelationTests`).
