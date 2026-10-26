---
module: MOD-0183
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E4
decision: conditional
commit: 7b168c4fa
---

# Idempotent replay is impossible through the gateway — two deliberate designs that cannot both hold

    lane      : mvp6-gateway-idempotency-replay-01
    measured  : 2026-10-17, on a live six-service lane stack
    scope      : MOD-0183 golden flow (gate item 6 of the main-merge completion gate), run after the owner granted Q496
    verdict   : DEFECT. A retried create answers 200 with the stored body direct at 5066 and 400 INVALID_REQUEST
                through the gateway, because the edge replaces the caller's correlation id and the receipt check
                requires it to match.
    stack     : gateway 5000 · web 5001 · auth 5056 · platform 5057 · mdm 5059 · supplychain 5066 · mongod 34994
    actor     : admin@diten.com (SuperAdmin), tenant 00000000-…-0001, legal entity dd674dbb-… ACTIVE/referenceable

## The measurement

Identical payload, identical `Idempotency-Key`, identical `X-Correlation-Id` sent on both calls
(`evidence/replay-direct-vs-gateway.txt`):

| path | call 1 | call 2 | correlation echoed |
|---|---|---|---|
| **direct 5066** | 201 `idempotentReplay:false` | **200 `idempotentReplay:true`, same `shipmentId`** | the one sent, both calls |
| **gateway 5000** | 201 `idempotentReplay:false` | **400 `INVALID_REQUEST`** | **a different value each call** |

Reproduced four times with both opaque and UUID-shaped keys, and with the correlation held constant and
varied. The direct path is the contract's behaviour; the gateway path is not.

## Why — and neither component is wrong on its own

- `gateway/Diten.ApiGateway/Program.cs:97-98` — *"INTX FIX2 — the edge does not adopt a caller's
  `X-Correlation-Id` (EdgeCorrelation)."* The edge mints its own, deliberately, so a caller cannot
  inject one.
- `ShipmentRepository.cs:70` — on a receipt hit, if `receipt["CorrelationId"] != correlationId` the
  mutation returns **400 `INVALID_REQUEST`**. Only a differing *fingerprint* gives 409
  `IDEMPOTENCY_KEY_REUSED` (`:69`); a matching fingerprint with a different correlation is rejected
  outright rather than replayed.

Put together: every retry through the edge arrives with a fresh correlation, so the receipt check can
never pass, so **`:70` fires on every replay at the public boundary.**

**What that costs:** the whole purpose of an idempotency key is a safe retry after an uncertain
outcome — a lost response, a timeout. At the edge, that retry now returns 400 instead of the stored
original, so the caller cannot distinguish "already applied" from "rejected" and the key buys nothing.

## This narrows Q497, which is the record that said the opposite

Q497 concluded the catch-all is *"transparent for the shipment-bundle family"* — measured as the same
request answering **identically** direct at 5066 and through the gateway on all five paths. That
holds for a **first** call. It does not hold for a **replay**, because transparency was measured on
status and body and not on the one header the idempotency contract depends on. Q497 is not wrong; its
scope was narrower than its sentence.

## What else the flow proved, and it is the stronger half

Gate item 6 had never run. On this stack, MOD-0183's golden flow executed end to end:

| step | result |
|---|---|
| `POST /shipments` (gateway) | **201**, `status: Draft`, `contractVersion: v1`, correlation echoed |
| `GET /shipments` (gateway) | **200**, contract envelope `{items,page,pageSize,total,contractVersion}` |
| `GET /shipments/{id}` (gateway) | **200**, the created shipment |
| replay, direct | **200 `idempotentReplay:true`**, same id |
| persistence and audit, lane DB | **8 shipments · 8 receipts · 8 history · 8 audit · 8 outbox** — exactly 1:1:1:1:1 |

That 1:1:1:1:1 is the state-changing acceptance shape (SOP §23 E4): every create produced exactly one
aggregate, one receipt, one history row, one audit row and one pending outbox row, with no orphans.

Also confirmed live, which v9.5 recorded as failing: **`GET /shipments` answers 200**. v9.5 §1 had
recorded a supposed contract violation there and corrected it to R-2's fail-closed legal-entity
validator refusing an unseeded scope. With an ACTIVE, referenceable legal entity the path works —
which closes that thread by construction rather than by argument.

## Two corrections this lane owes

**1. Q496 was not the blocker, and CT's measurement of it was wrong.** CT reported *"no role holds any
of the 25 `supplychain.*` keys (0 `rolePermissions` rows)"*. That was a **field-name error** — the query
filtered `Module:/supply/i` where the field is `Key`. Measured properly: the seeded **SuperAdmin** role
already held all 25, `CreatedBy=system`, `GrantSource=0`, from startup. The owner-granted Q496 write
added 25 rows to the **Admin** role (`CreatedBy=Q496-owner-granted-2026-10-17`, `GrantSource=1`); it is
correct and harmless, and it was **not required** for this flow, which ran as SuperAdmin.

**2. The real blocker was never permissions.** It was the legal entity. A freshly created one is
`DRAFT` / `referenceable:false`, and `lookup-validation` — the endpoint SupplyChain's validator calls —
answers **404 "Legal Entity is not referenceable."** `PATCH /{id}/activate` moves it to ACTIVE and the
endpoint then answers 200. That is the step no record named, and it is the one that gates every flow.

## Scope — what this lane does not claim

- **Not the five golden flows.** One ran: MOD-0183. Carriers, Loads, Returns and Claims were not run.
- **Not the browser flows.** These are HTTP measurements through the gateway and direct. The gate's
  row-1 evidence was taken in a browser; this is a different instrument and does not replace it.
- **No gate cell is moved.** MOD-0183 is not in the GATE-18-0 matrix, and nothing here was measured
  against the four modules that are.
