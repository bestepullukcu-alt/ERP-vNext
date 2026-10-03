# MOD-0192 independent hosted acceptance matrix

| Concern | Verdict | Fresh artifact |
|---|---|---|
| Immutable input | PASS 422/422 before and after | `raw/source-hash-check.log`, `raw/source-hash-check-after.log` |
| Restore/build | PASS | `raw/restore-success.log`, `raw/build.log` |
| Capacity tests | PASS 36/36 | `raw/capacity-ver.log`, `raw/capacity-ver.trx` |
| Five later-read faults | PASS 5/5 | `raw/capacity-ver.trx` |
| HTTP/JWT/six operations/replay | PASS | `raw/http-ver.jsonl` |
| Terminal DB effects | PASS | `raw/http-db-state.json` |
| Normal process restart | PASS | `raw/restart-ver.jsonl` |
| Two-host single terminal effect | PASS | `raw/two-host-http-ver.jsonl`, `raw/two-host-db-state.json`, `raw/two-host-listeners.txt` |
| Pending/no publisher | PASS | DB snapshots and host startup observation |
| Live-lease kill/renewal loss/stale worker | PARTIAL | No approved hosted delay/control seam |
| Transport response loss | PARTIAL | No approved hosted response-loss seam |
| Event/audit `_id` equality | N/A | Explicitly excluded by owner decision |
| X01 repository closure | PASS preserved | No contrary evidence |

