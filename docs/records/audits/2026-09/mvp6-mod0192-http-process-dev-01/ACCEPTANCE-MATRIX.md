# MOD-0192 hosted acceptance matrix

| ID / concern | Verdict | Evidence |
|---|---|---|
| X01 repository classification | PASS, preserved | `raw/capacity-dev-final.trx`; prior independent X01/X07 VER |
| X07 exact scoped terminal effect | PASS | `raw/db-state.json`, `raw/two-host-db-state.json`, 36/36 TRX |
| X07 five later-read failures | PASS | `raw/x07-later-read-green.trx`, `raw/capacity-dev-final.trx` |
| HTTP/JWT/RBAC/isolation | PASS | `raw/http-probe-green.jsonl` |
| Six operations and replay | PASS | `raw/http-probe-green.jsonl`, `raw/restart-probe.jsonl` |
| Hosted normal restart | PASS | `raw/restart-probe.jsonl` |
| Two-host single terminal effect | PASS | `raw/two-host-http.jsonl`, `raw/two-host-db-state.json`, `raw/two-host-listeners.txt` |
| Pending-only / no publisher | PASS | DB snapshots and host warning; no publisher source/composition added |
| Hosted in-flight kill, renewal loss, stale fence | PARTIAL | repository tests PASS; no bounded hosted delay/control seam |
| Hosted transport response loss | PARTIAL | repository unknown-commit PASS plus normal restart replay; no hosted transport-loss seam |
| Event/audit physical `_id` | N/A | expressly excluded by owner decision |
| Duplicate-name behavior | OUT OF SCOPE | unchanged |
