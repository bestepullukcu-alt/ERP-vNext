# Candidate validation results

Full OpenAPI 3.1 meta-schema validator was unavailable in the current offline environment; structural, local-ref, example-schema, parity, and mutation checks below ran. This is not a full OAS validation PASS.

| Check | Result | Detail |
|---|---|---|
| OAS version 3.1 | PASS |  |
| candidate metadata | PASS |  |
| twelve unchanged operations | PASS |  |
| paths bodies and statuses preserved | PASS |  |
| schemas parameters headers security events preserved | PASS |  |
| only target response changed | PASS |  |
| top-level other than info/components preserved | PASS |  |
| info other than candidate metadata preserved | PASS |  |
| 409 only createCapacityScenario uses response | PASS |  |
| retained and added exact codes | PASS |  |
| 409 headers/schema unchanged | PASS |  |
| new example status/error/correlation | PASS |  |
| new example Error schema | PASS |  |
| local refs (204) | PASS |  |
| annex points to candidate and exact narrow rule | PASS |  |
| mutant missing-code | PASS | rejected |
| mutant wrong-example | PASS | rejected |
| mutant changed-success-schema | PASS | rejected |
| mutant other-operation-drift | PASS | rejected |

Totals: 19 PASS / 0 FAIL.
