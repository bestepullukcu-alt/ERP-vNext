# MOD-0192 acceptance disposition — isolated DEV 01

Authority: published SANDOP-CAPACITY YAML `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3` and annex `eb1df1383e637c744179abe4c8faa3b3b7ebe4cccd19738aadf41896a9311bda`; wire `v1`. `PASS (bounded)` below means only the stated fixture/core observation, never composed HTTP or product acceptance. Source acceptance IDs are `mod-0192-spec-01/ACCEPTANCE.md`; its old frozen hash is superseded by the published two-file pin. Raw evidence: `capacity-tests.log`, `SOURCE-MANIFEST.tsv` and the isolated Mongo database `DitenSupplyChain_Mod0192_Test` on port 57192.

| ID | DEV disposition | Evidence and exact remaining gap |
|---|---|---|
| A192-01 | PARTIAL | Six controller methods/routes and permission constants compile; no shared Program.cs registration or composed bearer/header HTTP test. |
| A192-02 | PARTIAL | Real Mongo plan creation stores scope, Draft, provenance and Pending event; body DTO rejects `tenantId`. No composed JWT-to-DB proof. |
| A192-03 | PARTIAL | Exact fixture tuple creates; wrong checksum returns 422 with zero receipt/outbox. Other wrong ID/version and live DEMAND are not exercised/proved. |
| A192-04 | PARTIAL | Tenant-first unique active-plan index exists; one scoped plan persisted. No concurrent duplicate-plan or separate authorized fixture-scope race. |
| A192-05 | PARTIAL | Exact opaque constraint and decimal-string adjustment persisted; mismatched UoM returns 422 without scenario write. HTTP and empty-array wire still untested. |
| A192-06 | PARTIAL | Foreign tenant/LE and soft-deleted plan read is absent; scenario/evaluation foreign-scope and HTTP 404 envelopes untested. |
| A192-07 | N/A in bounded slice | Plan/scenario remain Draft; no approved state-producing operation reaches a disallowed state. No test-only mutation was added to claim this branch. |
| A192-08 | PARTIAL | Mongo Accepted insert, active slot, 202 receipt and read path exist; no composed 202/GET or no-side-effect HTTP trace. |
| A192-09 | PARTIAL | Twenty concurrent distinct-key repository calls: one 202, 19 `EVALUATION_ALREADY_ACTIVE`, one active slot/evaluation/receipt. Same-key evaluation race and HTTP are untested. |
| A192-10 | PARTIAL | Claim/renew/terminal CAS and actual two-child-process restart after real 30s server lease passed. X01–X10 are not all closed. No hosted-service registration was authorized. |
| A192-11 | PASS (literal fixture unit) | Finite exact ordered `line-4/2027-W03` `520.000/480.000/40.000/HOUR`; Infinite `[]`; no general optimizer or live constraint correctness. |
| A192-12 | PARTIAL | Normal plan/scenario/evaluation and terminal Mongo transactions persist aggregate/receipt/audit/Pending event or slot release together. Fault-at-each-stage and unknown-commit injection absent. |
| A192-13 | PARTIAL | Three allowed event types stored Pending with v1 payload/correlation/causation; terminal one-effect count tested. No publisher exists, so bus delivery/retry and envelope compatibility remain open. |
| A192-14 | OPEN | Module middleware/permission code compiles; no composed JWT/permission request because shared Program.cs is excluded. |
| A192-15 | PARTIAL | DTO rejects unknown client scope; no composed invalid JSON/header/date/decimal/status/header/error-body tests. |
| A192-16 | PARTIAL | Same plan key/different fingerprint returns 409; stale lease version/fence cannot terminal write. No composed client Version or unknown-commit case. |
| A192-17 | PARTIAL | Dedicated fixed DB, tenant-first indexes, foreign plan scope and soft-delete query checked. Full architecture gate 15/18; three unrelated baseline failures in Platform/HumanCapital/Talent. |
| A192-18 | PARTIAL | Api build 0 errors/warnings; CapacityPlans 11/11; independent VER/source→binary→process→HTTP/DB pending. Full SupplyChain suite 57/150 due pre-existing shared GUID serializer double registration in unrelated feature WebApplicationFactory runs. E5/G5 open. |

The approved Phase 1.5 proposed unique scenario-name index has a **wire GAP**: published `createCapacityScenario` 409 lists only `CAPACITY_PLAN_STATE_CONFLICT` and `IDEMPOTENCY_KEY_REUSED`. The duplicate-name branch currently emits `CAPACITY_SCENARIO_NAME_CONFLICT`, which is not a published code. Independent VER must mark this branch rejected/open; a contract owner must provide an exact published disposition before composed HTTP acceptance. It must not be silently equated with a plan-state conflict.

## Executor X01–X10

| ID | DEV disposition | Evidence and exact remaining gap |
|---|---|---|
| X01 | OPEN | No kill before Accepted transaction commit/fault hook; atomic normal commit alone cannot prove rollback at this cut. |
| X02 | PARTIAL | Accepted receipt/replay is durable in repository test; no lost HTTP 202, restart scan or same-key recovery trace. |
| X03 | PASS (bounded process restart) | Two child test processes exited after claim; while lease valid one claim persisted, after actual Mongo server-time 30s expiry a new child claimed attempt 2; stale fence terminal returned false. No app clock determined expiry. |
| X04 | PARTIAL | Two separate child processes raced Accepted claim and only one won; expired-lease simultaneous race was not run. |
| X05 | PARTIAL | Renewal increments version without attempt; old version terminal rejected. Delayed/failed DB renewal and cancellation behavior untested. |
| X06 | OPEN | No injected terminal precommit fault across all four writes. |
| X07 | OPEN | No unknown terminal commit/result-loss failpoint or scoped reconciliation trace. |
| X08 | PARTIAL | Real Mongo attempt 1→2→3 and forced lease expiry produced Failed, no fourth claim, one terminal effect. The expiry was forced in one test process; three process kills/restarts were not run. |
| X09 | PARTIAL | Terminal event remains Pending with no publisher; second child resumes after process exit. The BackgroundService was not composed/started after restart. |
| X10 | PARTIAL | Foreign tenant/LE and soft-deleted plan reads are absent. Cross-scope executor scan/claim/renew/terminal with foreign slot/audit/outbox counts untested. |

Intervals `10s/30s/3` are configured policy bounds, not a recovery deadline. A fixture computation may repeat; only durable terminal effect is fenced. No composed HTTP GO, product acceptance, E5 or G5 is claimed.
