# R22 / R25 row results

| Row / case | Verdict | HTTP | Scoped DB result | Evidence |
|---|---|---:|---|---|
| Normal environment selects NoOp | PASS | 200 | Read succeeds despite invalid evidence config | `raw/process-boundary.json`, `raw/logs/api-normal-invalid-ignored.log` |
| ClaimsEvidence missing bounded config | PASS with lazy-resolution note | 500 | No mutation | `raw/logs/api-evidence-missing.log` |
| ClaimsEvidence malformed FixedId | PASS with lazy-resolution note | 500 | No mutation | `raw/logs/api-evidence-invalid-fixed.log` |
| ClaimsEvidence unsupported FaultStage | PASS with lazy-resolution note | 500 | No mutation | `raw/logs/api-evidence-invalid-stage.log` |
| R22 exact `claim_number` collision | PASS | 503 `CLAIM_STORAGE_UNAVAILABLE` | Seed stays; `1/0/0/0` before and after | `raw/process-boundary.json` |
| R25 `aggregate` | PASS | 503 | `0/0/0/0` | `raw/logs/api-r25-stage-aggregate.log` |
| R25 `receipt` | PASS | 503 | `0/0/0/0` | `raw/logs/api-r25-stage-receipt.log` |
| R25 `audit` | PASS | 503 | `0/0/0/0` | `raw/logs/api-r25-stage-audit.log` |
| R25 `outbox` | PASS | 503 | `0/0/0/0` | `raw/logs/api-r25-stage-outbox.log` |
| R25 `beforeCommit` | PASS | 503 | `0/0/0/0` | `raw/logs/api-r25-stage-beforeCommit.log` |
| R25 `afterCommit` | PASS; not Mongo unknown | 201 replay | `1/1/1/1` | `raw/logs/api-r25-stage-afterCommit.log` |
| R25 Mongo unknown: committed branch | PASS | First 503; restart same-key 201 replay | `1/1/1/1`, unchanged after replay | `raw/unknown-commit-rerun.json` |
| R25 Mongo unknown: not-committed state | PASS as measurement | First 503 | `0/0/0/0` | `raw/unknown-not-committed-251.json` |
| R25 not-committed immediate restart | FAIL | 503 | Still `0/0/0/0` | `raw/unknown-not-committed-251.json` |
| R25 not-committed eventual retry | PASS with delay | 201 non-replay | Exactly `1/1/1/1` | `raw/unknown-not-committed-eventual.json` |

R22 is closed by this lane. R25 remains PARTIAL because the immediate restart recovery criterion did not pass, even though eventual recovery produced one durable write group.

