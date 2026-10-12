# runtime-scenarios — MOD-0187 Claims UI (for Q64b on the Mac)

DRAFT. `claims-ui.spec.mjs` was written and syntax-checked (`node --check`) in the chat lane; it has **never run**.

Prerequisites on the Mac (isolated environment, not the common checkout):
- HEAD `4a8d4d4b` archive + BC-SOURCE `ebd5d80c…` + **A12 360 overlay `7b6a0d1a…`** (README FINDING F3) + Claims accepted
  source `normal-source.tar.gz` `edb759a0…` + Auth 22 `f50350b8…`, then this lane's `overlay/` module files, then the
  `_shared-integration/` items the environment needs to route (gateway routes, provider line, nav keys).
- Three real-Auth identities with separate storage states: full (claims read/create/investigate/decide/settle +
  shipments.read, LE-A), read-only (claims.read, LE-A), no-read, plus an LE-B identity.
- Seeded Shipments through the published Shipment API: Dispatched without carrier (non-null root), Dispatched with carrier,
  Draft. Claims in Open and Investigating for the transition scenarios.

| Scenario | Acceptance row |
|---|---|
| list via same-origin adapter, skeleton → table, no :5000/:5061 | CU-01, CU-05 |
| filter sends only `status` / `shipmentId` | CU-02 |
| QuickView makes no request, no Edit | CU-31 |
| create 201 `250.00 EUR`, exact body text, reload shows `250.00` | CU-VS1, CU-09, CU-04 |
| create 400 `usd`, inputs kept | CU-11 |
| ineligible shipment note + disabled submit | CU-17 |
| carrier checkbox gating | CU-10 |
| unknown shipment safe-not-found | CU-14 |
| transition Open → Investigating 200 | CU-08 |
| stale transition 422 `INVALID_CLAIM_TRANSITION` (two profiles) | CU-21 |
| idempotency 409 probe (outline, skipped) | CU-19 |
| no read → `_AccessDenied` only | CU-06 |
| read-only → no CTA, no row actions | CU-07, CU-08 |
| cross-LE list empty, cross-LE resolve 404 | CU-16, CU-14 |
| tr and ar (RTL) screens at 390/768/1024/1440, PNG | CU-24, CU-25 (PNG as evidence only; CU-30 stays BLOCKED) |

ASSUMPTION A11: culture is switched with `locale` + `?culture=&ui-culture=`; the shell also sets the
`.AspNetCore.Culture` cookie from its language menu — Q64b uses whichever the environment honours.
