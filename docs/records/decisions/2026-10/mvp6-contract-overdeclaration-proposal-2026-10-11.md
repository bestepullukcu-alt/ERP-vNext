# Proposal — SHIPMENT-BUNDLE declares three statuses the service cannot produce

- date: 2026-10-11
- by: Control Tower, from live conformance measurement against the running stack
- decision needed from: the contract owner — the file is `x-status: FROZEN`
- affects: `MOD-0184:119`, `MOD-0185:125`, `MOD-0187:122`, which all say "implementation matches **all**"

## Why this is in front of you

Those three acceptance lines ask that the implementation match every declared schema, example, header and
error. Measured against the live stack: **66 of 83 declared statuses are reached and conform, with zero
undeclared statuses and zero schema violations.** Of the seventeen that are not reached, twelve cannot be
reached:

| | count | why |
|---|---|---|
| over-declared | **3** | the operation cannot produce the status — this proposal |
| no store path | 9 | every injected store error (2, 13, 14, 96, 121) answers 503; the declared 500s are a catch-all for an unhandled exception, which is correct design and not a defect |
| needs a proxy | 3 | 502 needs a dependency answering with an invalid body |
| not yet driven | 2 | reachable; the driver does not build the state yet |

So "all" is not an achievable target against the contract as written, and the three acceptance lines cannot
close while it says so. The nine 500s are fine as declared. The three below are not.

## The three

### 1 and 2 — `queryCarriers` and `queryLoads` declare a 404 a list cannot produce

Measured, three ways:

```
GET /loads?carrierId=<unknown>   -> 200
GET /loads?status=Planned        -> 200
GET /carriers?status=Active      -> 200
```

A list answers 200 with whatever matches, including nothing. The declared example on `queryLoads` 404 is
`LOAD_NOT_FOUND`, which belongs to the by-id operations — the shape of a copy forward.

The contract already disagrees with itself here: **`queryClaims` declares no 404**, and it is the same kind of
operation. Two of the three list operations carry a status the third correctly omits.

**Proposed:** remove the `404` response from `GET /carriers` and `GET /loads`.

### 3 — `createCarrier` 422 has no rule behind it, and the contract says so

`components/responses/CarrierCreateUnprocessable` is declared with a schema and **zero examples** — the single
coverage gap in the whole document, found independently by an inspection lane before this measurement.

Its own description explains why:

> *"Is kurali veya lifecycle gecisi gecersiz; **preserved existing create422, no new Carrier create business
> rule.** X-Correlation-Id MUST be present."*

And the source agrees: the only 422 in the Carrier feature is `INVALID_CARRIER_TRANSITION` at
`CarrierRepository.cs:66`, which is on the **status-change** path. There is no create-side rule to trigger it.

**Proposed:** remove the `422` response from `POST /carriers`, or — if it is being held for a rule that is
coming — give it at least one example so "matches all examples" has a target and a reader can tell what it
means. Removing is the honest option; keeping it undocumented is the one that cannot be verified either way.

## What this changes

| | now | after |
|---|---|---|
| declared statuses | 83 | 80 |
| reached and conforming | 66 | 66 |
| unreachable by contract defect | 3 | **0** |
| remaining unreached | 17 | 14 — nine 500 catch-alls, three 502 needing a proxy, two the driver will drive next |

The three acceptance lines still would not close on this alone. They would, for the first time, have a target
that is achievable.

## The part that is not CT's to decide

The contract is `x-status: FROZEN` and its pin has already drifted once without a record: the file hashes
`c3c4e37a…` while the Q218 pin at `mvp6-q218-contract-pin-01/HASHES.md:5` still reads `6dc1dd48…` (3.1.0).
Amending it again means a version decision (3.2.1 → 3.2.2), a re-pin, and whatever consumer notice the
freeze implies. CT has changed nothing and proposes only the three removals above.

## How this was measured

`scripts/g5/g5_live_conform.py` on branch `feature/sce/contract-example-walker`, against Gateway 5000 /
SupplyChain 5066 with the runtime lane's stack. It enumerates operations and statuses from the document
rather than a hardcoded list, validates each response body against the schema declared for the status
actually returned, treats an undeclared status as a hard failure, and classifies everything it could not
reach instead of pooling it. Fault injection is a MongoDB `failCommand` fail point armed for one request and
disarmed in a `finally`.
