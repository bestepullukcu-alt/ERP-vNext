# MOD-0189 PR #160 live-slice evidence

This is a development-only HTTP check against isolated, fixture-seeded MongoDB.
It is not production authorization, a live inventory posting, or full MOD-0189 acceptance.

## Sources and observed responses

- MongoDB 7.0 single-member replica set on `127.0.0.1:31994`, database
  `mod0189_http_evidence`. A fixture created a Draft, submitted it for independent
  approval, and published a 52-week revision using the PlanningService Mongo stores.
- A dated lead-time revision (5 days) was created, submitted, and independently
  approved through the same transaction/audit store.
- A temporary active LegalEntity was read by the real MDM service through Gateway:
  `lookup-validation` returned HTTP 200.
- The real InventoryService availability endpoint through Gateway returned HTTP
  200 for the scoped SKU and warehouse, `available=8.000` in `EA`. The
  inventory balance was fixture-seeded, not posted through Inventory commands.
- `GET /api/v1/mrp/calculations` on PlanningService returned HTTP 200,
  52 ISO weeks, the Published revision ID and checksum, the Inventory `asOf`,
  and the Approved lead-time revision. The first week had demand 5, opening
  stock 8, and a zero lot-for-lot proposal.
- The response had two separate visible warnings: open orders were not counted
  and approved safety stock was not counted. Neither is claimed to be a verified
  zero-valued source.
- Stopping only the task-owned InventoryService made Gateway return 502 and
  MRP return 503. Stopping only the task-owned MDM service made Gateway return
  502 and MRP return 503. Neither failure produced a plan.

## Automated checks

- Real temporary Mongo: PlanningService suite 381 passed, 0 failed, 0 skipped.
- Focused MRP tests: 31 passed, 0 failed. These include MDM/Inventory wire
  parsing, Published-demand race, tenant/company scope, and audit-insert
  exception rollback in the lead-time transaction.
- The Inventory availability wire response is unwrapped JSON. Its scoped
  company, SKU, warehouse, unit, quantity, and as-of fields are validated.

## Open acceptance limits

The live check used a test JWT and fixture-seeded company, balance, and demand.
It does not prove user-to-company authorization, durable Inventory snapshot
versioning, an open-PO source, approved safety-stock quantity, event delivery,
Gateway/UI flows, or MOD-0141 handoff. The central BL-501 audit connection
is still pending; the lead-time store uses the approved temporary local
transactional audit path.
