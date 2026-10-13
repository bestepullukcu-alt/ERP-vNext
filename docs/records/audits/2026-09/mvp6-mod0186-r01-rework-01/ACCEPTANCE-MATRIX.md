# Acceptance matrix

| Requirement | Fresh result | Evidence |
|---|---:|---|
| Exact producer `500 SHIPMENT_ROOT_INVALID` maps to Returns `502 RETURN_SHIPMENT_ROOT_INVALID` | PASS | `raw/http/025-producer-root-bad-direct-detail.json`, `raw/http/028-producer-root-bad.json`, `raw/main-results.json` |
| Missing and explicit-null root remain `503 RETURN_SHIPMENT_ROOT_UNAVAILABLE` | PASS | `raw/http/026-producer-root-missing.json`, `raw/http/027-producer-root-null.json` |
| Malformed root in successful producer payload remains `502 RETURN_SHIPMENT_ROOT_INVALID` | PASS | `raw/green/unit-r01.log` |
| Unrelated/malformed producer 5xx is not promoted to root-invalid | PASS | `raw/green/unit-r01.log` (unknown 500, invalid JSON, wrong version, 501) |
| Producer auth failure, refusal and timeout remain unavailable | PASS | `raw/green/unit-r01.log` |
| Correlation/root precedence remains unchanged | PASS | main assertions `root mismatch 409` and `root precedes payload drift`; corresponding `raw/http/007-*` and `010-*` |
| Fresh composed HTTP/JWT/RBAC/tenant/LE/soft-delete suite | PASS 52/52 | `raw/main.stdout`, `raw/main-results.json`, `raw/http/` |
| Five transaction failpoints roll back all five write groups | PASS 5/5 | `raw/main-results.json`, `raw/http/044-*` through `048-*` |
| Unknown-commit and response-loss recovery | PASS | `raw/http/049-*` through `052-*`, `raw/main-results.json` |
| Restart persistence/replay, new process | PASS 4/4 | PIDs 87872 → 88363 in `raw/api-process-*.txt`; `raw/restart-results.json` |
| Full Returns-owned regression | PASS 78/78 | `raw/green/returns-regression.log` |
| Fresh Release build | PASS, 0 warnings / 0 errors | `raw/fresh-build.log` |
| Pending-only outbox; no worker outcome | PASS, 19 Pending / 0 non-Pending | `raw/db-final-state.json`, `raw/restart-results.json` |
| Inventory/Warehouse HTTP and stock collections remain absent | PASS, 0 / 0 | empty `raw/inventory-warehouse-http.txt` with grep exit 1; `raw/db-final-state.json` |
| Patch applies to exact two-file baseline | PASS | `raw/patch-apply.log`, `raw/patch-target-hashes.txt` |
| 46-path boundary and protected composition/Claims snapshot | PASS | 2 changed + 44 unchanged in `manifests/returns-source-test-manifest.tsv`; 2 changed + 95 unchanged in `manifests/combined-97-drift.tsv` |

The first HTTP setup attempt is retained under `raw/discarded/setup-attempt-01/`. It stopped because `enableTestCommands` was missing and contributes no PASS count.
