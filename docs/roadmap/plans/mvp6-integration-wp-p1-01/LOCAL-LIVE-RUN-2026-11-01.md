# MOD-0183 / MOD-0186 — local live run (2026-11-01)

Not an acceptance record and not the MVP-1 tour. It measures the real services before that tour. Raw output stays outside the repository
(`ERP-vNext-lanes/tour-20261101`, `tour-20261101b`).

## Conditions

| Item | Value |
|---|---|
| Real | Gateway, SupplyChain, INVENTORY, MDM, Platform; Web and a real browser in the second run |
| Not real | Auth (hand-made token, cookie bridge), approval flows, e-signature, transport (`Eventing=InMemory`) |
| Runtime | owned replica set on lane ports, mongod 8.0.18 (CI 7.0.28) |
| Directly written data | `mdm_items`, `trace_lots`, `recall_lot_blocks`, one `sce_shipments` row, one location state; second run also the tenant row. Public routes needed approvals or returned 503 |
| Screen input | field values and clicks set by page script, not keyboard |

## Run 1 — `0504c96ea`

Thirteen rows passed: T1-1…T1-5, T2-1…T2-6, R1-a, R1-b. T2-1 passed only with `toLocationId`, see F-1.

| Finding | Result |
|---|---|
| F-1 | Received without `toLocationId` answered 500, left the line `Blocked` and the return `InTransit`. Fixed in #333 (400 before any call; pack §34.1 requires the field) |
| F-2 | R1-a: the REVERSAL takes stock out of QUARANTINE (4 → 0) but hold `KRN-2026-0001` stays `Open`, quantity 4. Code read: `QuarantineIntake.cs` ignores reversals. Open question to the MVP-1 CT: defect or accepted limit |
| R1-b | After a partial move, the same REVERSAL answers 422 `INSUFFICIENT_STOCK` (available 1, requested 4); balance and hold unchanged |
| Setup gaps | Tour role also needs `mdm.product-master.read` (missing: dispatch 503, not 403) and `supplychain.returns.authorize`. `.transit` and `.receive` read from code. A return needs a `Delivered` shipment. `ITEM_NOT_ACTIVE` needs `Retired`; `Deactivated` not measured |

## Run 2 — `8edb2e546` (#333), English, Turkish, Arabic

Six rows passed.

| # | Measured | Result |
|---|---|---|
| A1 | No location: 400, no movement, line not blocked; retry with location: 200, `Posted`, QUARANTINE +2 | PASS |
| A2 | Whitespace location: 400, no movement | PASS |
| A3 | Warehouse and location: 200, `Posted`, QUARANTINE 2 → 6 | PASS |
| B1 | Screen, empty location: no request, field marked, error shown | PASS |
| B2 | Screen, both fields: 200, line result table | PASS |
| B3 | `ITEM_NOT_ACTIVE`, then receive without stock record: empty location stops it; with location `unpostedLines` is sent, 200 `NotPosted` | PASS |

Visual note, not fixed: at 800 px in English and Turkish the transaction reference column of the result table is clipped; Arabic fits. Scrolling
was not tried.

## Not measured

Real Auth and roles, the login screen, a mobile width, transport delivery, the MVP-1 environment.
