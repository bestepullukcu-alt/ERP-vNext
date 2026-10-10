# MVP6-SHIPMENT-A12-SAFE404-INDEPENDENT-VER-01

Role: independent verifier who did not write `MVP6-SHIPMENT-A12-SAFE404-REWORK-01`.

## Inputs

- Package: `docs/records/audits/2026-09/mvp6-shipment-a12-safe404-rework-01/`
- Successor source archive: `successor-source.tar.gz`
- Archive SHA-256: `7b6a0d1abea649d920040a039972c5d4928825e9cf2cc185487b1fe5f0f3314d`
- Successor manifest: `SUCCESSOR-360-SOURCE-MANIFEST.tsv`
- Manifest SHA-256: `8ffa6c96d29f8004940f310ed704a2c05eaa38ffa501d6114a5f9251e6e00d36`
- Patch SHA-256: `b3c7dcbb68636b123f3ea3ad7c3c902709fef6a9775c66cc3d464073653ecefb`
- Controlling failure: `../mvp6-shipment-remaining-acceptance-disposition-01/evidence/a08-a09-a12-01/`

## Verify

1. Verify artifact hashes and reconstruct the successor source into a disposable workspace.
2. Confirm only these two source files changed from the input 360-source baseline:
   - `frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Shipments/details.js`
   - `frontend/Diten.Web.Tests/JavaScript/ShipmentDetailActionTests.cs`
3. Use native .NET 8 and an isolated Mongo/port set. Do not use operational `27017`.
4. Reproduce the A12 browser/runtime cases from the controlling evidence:
   - normal authorized detail still renders summary, lines, POD and actions as allowed;
   - cross-LE detail 404 shows only the localized safe-not-found support-reference surface;
   - unknown detail 404 shows only the localized safe-not-found support-reference surface;
   - soft-deleted detail 404 shows only the localized safe-not-found support-reference surface;
   - the hidden safe-404 surfaces are not keyboard reachable;
   - backend 404 status/code/correlation and zero-write behavior remain unchanged;
   - late async detail responses do not re-expose stale shipment surfaces.
5. Preserve inherited A08/A09 PASS and A10 unauthorized/open status; do not rerun unrelated suites without a concrete drift reason.
6. Write a separate SOP §22 report, raw evidence archive, manifest and cleanup record. Do not modify product source.
