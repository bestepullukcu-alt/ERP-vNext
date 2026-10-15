# MOD-0188 PRODUCT-MASTER UoM evidence

- Source branch: `feature/sce/mod-0188-product-master-uom`, based on `origin/main` `3912d568b18193f627539a1b27965167ac4077b6`.
- PRODUCT-MASTER v1.1 `getSku` and `getSkuUom` are read with the caller token. The base units must agree. Only an active SKU and an unambiguous path of at most four current conversion edges can enter a new accepted file row. The source amount/unit, path hash, numerator/denominator and rounded base amount remain separate.
- Approved rounding: four decimal places, `AwayFromZero`, only when a unit conversion is needed. A row already in the base unit retains its original decimal precision. The ratio is kept exact until the final converted quantity round. `conversionId` and `asOf` are not present in v1.1; BL-584 remains open.
- Older accepted rows and their original acceptance audit are immutable. A fresh read-time PRODUCT-MASTER check clears only the *effective* warning if the original unit matches the current base. A different current base produces `DifferentFromCurrentBase` on the read view, without creating a review queue or silently summing different units. The stored historical pending flag and acceptance hash are unchanged.

## Checks

| Check | Result |
|---|---|
| PlanningService full suite on isolated MongoDB 7.0.43 replica set, final source | 370 passed, 0 failed, 0 skipped |
| New verified-base unit-slot Mongo regression | 1 passed, 0 failed, 0 skipped |
| Demand Planning frontend tests | 25 passed, 0 failed |
| API build | 0 errors; one NU1900 (NuGet advisory feed unavailable) |
| Web build | 0 errors; 26 warnings outside the changed Demand Planning files |
| Seven RESX key sets | Equal |
| Rounding sabotage: temporary scale 4 to 3 | 5 of 6 focused tests failed; restored scale 4, 6 of 6 passed |
| Base-unit precision and focused import/conversion/reference tests | 74 passed, 0 failed |

Each Mongo run used a new repo-external temp directory, one-node `rs-mod0188` bound only to `127.0.0.1:31994`, and `mod0188_tests`. Owned PIDs 27268, 32160 and 19644 stopped; all three temp directories were deleted and the port had zero listeners. No operational Mongo port was used.

## Not proven

- The real MDM, Gateway, Auth, Web and PlanningService processes were not running here. Consumer tests use v1.1-shaped HTTP fixtures; no live MDM call or S1/S3 browser acceptance is claimed.
- An old provisional unit different from the current base remains visible with a warning. The existing unit-slot rule still prevents a new differently denominated row for that same series from being accepted without a separate reconciliation rule; mixed units are not added together.
- Historical effective verification is a read-time projection, not a rewrite of the original Mongo row. V1.2 dated conversion, MOD-0183 shipment history, BL-501, BL-628, BL-629 and position-based LegalEntity authorization remain open.
- No AC is closed by these tests.
