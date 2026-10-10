# Exact acceptance matrix

| Row | Requirement | Verdict | Independent evidence |
|---|---|---:|---|
| V01 | Exact patch SHA | PASS | `raw/input-package-verification.txt`, `product.patch` |
| V02 | Exact DEV evidence archive SHA and internal manifest | PASS | `raw/input-package-verification.txt` |
| V03 | Writer-complete and published input bindings | PASS | `input-writer-complete.json`, `raw/input-package-verification.txt` |
| V04 | Baseline/target manifest; only two Returns-owned changes | PASS | `manifests/baseline-target-341.tsv`: 2 changed verified, 339 unchanged |
| V05 | Baseline producer 500 reaches consumer as pre-fix 503 (RED) | PASS | `raw/red/unit-r01.log`: expected 502, actual 503 |
| V06 | Producer `500 SHIPMENT_ROOT_INVALID` maps to Returns `502 RETURN_SHIPMENT_ROOT_INVALID` | PASS | `raw/green/unit-r01.log`; `raw/http/025-*`, `raw/http/028-*` |
| V07 | Unrelated 5xx and malformed error body remain unavailable | PASS | exact target tests in `raw/green/unit-r01.log` |
| V08 | Wrong version, 501, producer 401/403 remain unavailable | PASS | exact target tests in `raw/green/unit-r01.log` |
| V09 | Refusal and timeout remain unavailable | PASS | exact target tests in `raw/green/unit-r01.log` |
| V10 | Missing/null successful root remain 503 unavailable | PASS | `raw/http/026-*`, `raw/http/027-*`; 78-run root theory |
| V11 | Malformed successful root remains 502 invalid | PASS | `raw/green/unit-r01.log`; 78-run root theory |
| V12 | Fresh Release build | PASS | `raw/fresh-build.log`: 0 warnings, 0 errors |
| V13 | Exact Returns regression | PASS | `raw/green/returns-regression.log`: 78/78 |
| V14 | Fresh HTTP/DB suite | PASS | `raw/main-results.json`: 52/52 |
| V15 | Restart checks on a new PID | PASS | `raw/restart-results.json`: 4/4; PID records |
| V16 | JWT, RBAC, tenant/LE, conflict/replay/concurrency | PASS | named assertions in `raw/main-results.json` and raw HTTP |
| V17 | Five rollback stages and scoped zero-write behavior | PASS | assertions 44–48 and raw HTTP/DB details |
| V18 | Unknown commit and response-loss recovery | PASS | assertions 49–52 and raw HTTP |
| V19 | No Inventory/Warehouse/stock write surface | PASS | `raw/db-final-state.json`, `raw/inventory-warehouse-http.txt` |
| V20 | Source→binary→process freshness | PASS | `SOURCE-BINARY-PROCESS.md`, binary and process records |
| V21 | Exact probes/config/exits/raw HTTP/DB packaged | PASS | `scripts/`, `raw/`, `COMMANDS.tsv`, permanent archive |
| V22 | Historical old “null” explanation retained | PASS | `raw/historical-null-record.txt`; no edit by verifier |
| V23 | No bearer token or secret archived | PASS | redacted HTTP/launch; `raw/secret-scan.rg-exit` = 1 |
| V24 | No product/canonical/guard/pack/git mutation | PASS | `NO-CHANGE.md` and baseline/final records |
| V25 | Process cleanup | PASS | API and Mongo listener-after-shutdown exits = 1 |

All rows above are bounded to R01. The matrix does not convert test totals into full-module coverage and does not state CT acceptance or E5/G5.
