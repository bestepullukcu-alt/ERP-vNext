# R01–R18 and R21 fresh E4 matrix

The main probe executed 172 named HTTP/DB observations. Supplemental probes add real
Shipment/Carrier uptake, connection refusal, storage/internal failures, ASCII header
boundaries and the omitted/null/fingerprint/foreign-scope cases. `PASS` below applies only
to this policy lane's stated observable.

| Row | Verdict | Fresh evidence | Exact remaining condition or finding |
|---|---|---|---|
| R01 | PASS | `E4-R01-S01..S08` (8) in `policy-results.json` | All eight states; five 201, three exact 422; rejected four-collection delta zero. |
| R02 | PASS | `E4-R02-C01..C12` (12), fixture request log, `producer-uptake.json` | Controlled full Carrier wire/status/field matrix passed; real unfiltered Carrier list and Suspended match uptake passed. |
| R03 | PASS | `E4-R03-O01..O04` (4) | Observation-time snapshot stayed Delivered after fixture changed to Cancelled; replay/transition added no reference read. |
| R04 | PASS | `E4-R04-F01..F05` (5), `refusal.json` | 404/malformed/identity/incomplete/timeout/refusal mappings and zero writes passed. |
| R05 | PASS | `E4-R05-E01..E05` (5) | Omission/empty/duplicate/order/null and zero evidence lookup passed. |
| R06 | PASS | `E4-R06-A01..A12` (12), `policy-supplement.json` | Claimed/approved bounds, explicit null and omission, zero/-zero, retention passed. |
| R07 | PASS | `E4-R07-M01..M03` (3), `policy-supplement.json` | Multiple same-Shipment claims, empty/null/omitted audit values and no finance collection passed. |
| R08 | PARTIAL | `E4-R08-P01` | Long value passed HTTP/list/BSON in one process. Two-process restart, capacity and unknown-commit evidence remains Lane C (`MOD0187-E4-PERSISTENCE-01`). |
| R09 | PASS | `E4-R09-L01..L14` (14) | Decimal/currency lexical positive and negative matrix passed. |
| R10 | PASS | `E4-R10-I01..I06` (6), `policy-supplement.json`, R16 cases | Object order/escape equivalence, null/omission equivalence, array order, lexical amount/time conflict and root precedence passed. |
| R11 | PASS | `E4-R11-P01..P03` (3), `policy-supplement.json` | Withdrawn grant and known foreign-tenant target hiding passed. |
| R12 | PASS | `E4-R12-P01..P04` (4) | Closed/decide, settle-only denial and Open-target lifecycle behavior passed. |
| R13 | PASS | `E4-R13-P01..P02` (2) | Same-creator create-only denial and same-creator decide approval passed. |
| R14 | **FAIL** | `E4-R14-H01..H14` (14), `header-boundary.json` | ASCII 128/129 and duplicate/missing headers passed. A valid 128-scalar non-ASCII key is rejected by Kestrel before Claims middleware; see `F-E4-01`. |
| R15 | PASS | `E4-R15-O01..O05` (5), R16 precedence cases | Auth/context/header/media/body/target/receipt/root ordering passed in the exercised conflicts. |
| R16 | PASS | `E4-R16-R01..R03` (3), R03/R10 create cases | Create and transition replay, payload conflict and wrong-root precedence passed. |
| R17 | PASS | `E4-R17-00..48` (49) | Seven legal arrows returned 200; all 42 other HTTP pairs returned 422 `INVALID_CLAIM_TRANSITION`. |
| R18 | PASS | `E4-R18-E01..E16` plus `error-matrix-extra.json` | All 18 public codes/messages used by the matrix, correlation parity, Bearer challenge, controlled 500 and storage 503 passed without raw-data leak. |
| R21 | **FAIL** | `E4-R21-R01..R06`, `producer-uptake.json` | Controlled missing/null/empty/malformed/mismatch/nil passed. Real non-nil and missing producer uptake passed, but real nil root fails at Shipment correlation validation; see `F-E4-02`. |

R19–R20 and R22–R26 were not assessed and receive no verdict from this lane. R27–R30 were not rerun; their historical independent-ver status is not fresh evidence here.
