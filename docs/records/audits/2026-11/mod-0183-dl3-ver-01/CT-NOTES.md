# MOD-0183 DL-3 — CT notes on the VER · 2026-11-05

Branch `feature/sce/mod0183-dl3-01`. Commits: `f6d99b5fa` (3.5.0 contract and service), `d7ae760f2` (web), `5221b96ef`
(VER fixes), this record. Not pushed. Not an acceptance record: DL-3 cannot be accepted or merged before SB-24.

## Measured by the CT

- SupplyChain full suite, unfiltered, lane replica set on port 6155, mongod 8.0.18, on `d7ae760f2`: 1026 passed, 1 skipped,
  0 failed (`ERP-vNext-lanes/mod0183dl3/run-20261105-215959`).
- Web full test assembly on `d7ae760f2`: 4173 passed, 0 failed (`ERP-vNext-lanes/mod0183dl3/web-full.log`).
- After `5221b96ef`: Web Shipment tests 107 passed; `ServiceNeverCallsCrmTests` passed. The full suites were not re-run.
- Sabotage, each red and then restored: summary projection without `customerRef`; `CreateShipmentValidator` without the
  pattern; destination separator `" / "`; CRM body in the adapter log; a CRM route in the service; a lot id as option text.

## Dispositions

- F-1: owner decision, Natig (K1), 2026-11-05: keep the field on the create form as the approved pack writes it (dormant).
  Follow-up: entering the FEFO reason at dispatch needs a contract change (SHIPMENT-BUNDLE 3.6.0) and a MOD-0183 pack
  revision. It does not block DL-3.
- F-2, F-3, F-5, F-6, F-8: closed by `5221b96ef`.
- F-4 (SB-24): open, and it is the merge gate. MOD-0177 FU (MVP-1 CT) is not on `main`. Before merge the CT runs
  `git merge-base --is-ancestor <FU commit> <DL-3 base>` and records exit 0 here.
- SB-16 (real browser on the integrated target): open; run before acceptance.
- F-7, F-9, F-10, F-11, F-12, F-13, F-14: follow-ups, owner MVP-6 CT, by 2026-11-10. F-10 (status filter) and F-13 (switch
  to the frozen CRM-ACCOUNT-LOOKUP U-3) go together in one adapter change once the CRM status value is measured.

## Not measured

Gateway, CRM, LOCATION, PRODUCT-MASTER and TRACE live; mongod 7.0.28 (CI); rendered pages in 7 languages; Vitest.
