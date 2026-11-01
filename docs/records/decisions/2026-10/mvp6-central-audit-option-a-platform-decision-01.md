# MVP-6 — central audit detail: Option A (Platform owner decision, 2026-10-31)

- owner: **Beste** (Platform owner), decided by message 2026-10-31, relayed by Natig (K1) and recorded by the MVP-6 Control Tower. Ali
  routed the question to the Platform owner the same day.
- status: **ACCEPTED**, delivery not scheduled
- why: MOD-0183, MOD-0186 and MOD-0147 ask for audit detail that does not reach the central record. The forwarder carries four fields, and a
  failed command leaves no record at Platform.
- modules: MOD-0183, MOD-0186, MOD-0147 (SCE); `Diten.BuildingBlocks.CentralAudit` (BL-501)

## Decision

| # | Decision |
|---|---|
| 1 | Option A. The change is made in the shared forwarder library (BL-501, `Diten.BuildingBlocks.CentralAudit`). |
| 2 | No new `details` field. Per the owner, `/api/internal/audit/append` already carries `BeforeState`, `AfterState`, `Metadata` and a `Failed` / `Denied` outcome. The MVP-6 side did not measure this. |
| 3 | Values are schema-bound and size-limited, and carry no personal data or secrets. The forwarder applies the forbidden-key list. |
| 4 | A failed outcome that is stored as state (for example the MOD-0183 compensation result) is written in the same transaction as the business data. |
| 5 | The `Failed` record of a command rolled back by an exception is written outside the transaction, best effort. If it cannot be written, a warning is logged. |
| 6 | Not a v1 condition. It arrives in the first shared-library work package after MVP-6 v1. Work package id and date: not set. |
| 7 | Shared code stays with Platform. MVP-6 wires the write side of the three modules. The field schema is agreed when the work package is ready. |

## Time-limited interim

Detail stays in the modules' own records (`sce_shipment_audit`, `returns_audit`, the MOD-0147 business record). This is not permanent. The
packs are not changed for Option B.

## Recorded deviation

| Module | Rule | Deviation | Closes when |
|---|---|---|---|
| MOD-0147 DL-2 (#327) | AUD-001 §3 row 10 | §21 Before/After and the event name are not in the central record | Option A is delivered |

## Not decided here

- The field schema for each module.
- The owner and date of the work package.
