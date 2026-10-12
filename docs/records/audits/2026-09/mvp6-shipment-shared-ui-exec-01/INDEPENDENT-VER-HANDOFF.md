# MVP6-SHIPMENT-SHARED-UI-INDEPENDENT-VER-01

Role: a verifier who did not write this integration successor; strict source read-only.

## Inputs

- `combined-source.tar.gz` and `COMBINED-360-SOURCE-MANIFEST.tsv` from this directory.
- Controlling candidate: `../mvp6-shipment-shared-ui-disposition-01/`.
- Accessibility successor: `../mvp6-shipment-line-accessibility-rework-01/`.

## Verify

1. Verify all artifact hashes and reconstruct the 360-source archive in a unique disposable directory.
2. Verify the exact 12 target hashes from `CHANGED-FILES.tsv` and all three rows in `PRESERVED-ACCESSIBILITY.tsv`.
3. Use native .NET 8. Fresh-build Diten.Web and independently run the focused controller/form/JavaScript test classes recorded in `TEST-RESULTS.tsv`.
4. Exercise the real pipeline: the five marked same-origin Shipment JSON adapters must return the established JSON/correlation 401 while the ordinary HTML page retains the login redirect; authenticated 403 behavior remains unchanged.
5. At 390 and 768 widths, verify all seven cultures for responsive-modal localized title and close accessible name, RTL, row/action preservation, and no regression in accepted repeatable-line IDs/labels/keyboard/focus behavior.
6. Keep durable PNG, PRES-183-03 and all unrelated Shipment UI criteria OPEN. Do not treat candidate evidence or this writer's 19/19 as independent PASS.
7. Write a separate SOP §22 report and evidence archive. Do not modify product source.
