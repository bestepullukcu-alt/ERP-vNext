# CapacityPlans acceptance matrix — proposed, not executed

Every test must bind the frozen `SANDOP-CAPACITY` 1.0.0/YAML SHA `c255e92923ba91714cec8daf229262101b5d45648b7f714a03ab402811db683c`. An isolated fixture result is not live DEMAND/constraint uptake. Exact unresolved error outcomes stay conditional on owner decisions C192-02/05/06.

| ID | Observable acceptance | Evidence level / gate |
|---|---|---|
| A192-01 | Each of the six `/capacity-plans` operations has the frozen method/path, bearer security, required correlation header; POSTs require `Idempotency-Key`. No extra approval/scheduler endpoint appears. | Contract/static; fixture smoke |
| A192-02 | Create 201 persists one plan in JWT-resolved tenant+LE with immutable ID/version/capture/checksum and `Draft`. Client body cannot set tenant/LE; DEMAND quantity/confidence/series absent from Capacity collections. | E4 DB/HTTP |
| A192-03 | Declared Published fixture tuple succeeds; wrong ID/version or Draft tuple returns 422 `INVALID_DEMAND_REFERENCE` and no plan/receipt/audit/outbox. Checksum mismatch follows the exact C192-01 owner disposition. Mark **fixture only** until producer uptake. | E4 mock after C192-01; live producer OPEN |
| A192-04 | Same horizon+DEMAND version in one tenant+LE yields 409 `CAPACITY_PLAN_ALREADY_EXISTS`, one plan/event; a different scope does not leak or collide. | E4 unique-index/race |
| A192-05 | Scenario create 201 preserves exact opaque constraint refs and decimal-string deltas, in parent scope; no foreign base constraint fact copied. Empty arrays follow frozen schema unless owner changes it through contract process. | E4 HTTP/DB, fixture only |
| A192-06 | Unknown/cross-tenant/cross-LE plan, scenario and evaluation IDs return matching 404 (`UNKNOWN_CAPACITY_PLAN`, `UNKNOWN_CAPACITY_SCENARIO`, `UNKNOWN_CAPACITY_EVALUATION`) with no existence leak. | E4 JWT/DB |
| A192-07 | Scenario creation in owner-defined disallowed plan state returns 409 `CAPACITY_PLAN_STATE_CONFLICT` and no write. No uncontracted mutation is used merely to reach that state. | E4 once C192-06 decided |
| A192-08 | Evaluation submit 202 durably stores `Accepted`, `bottlenecks=[]`, `completedAt=null`, mode/resourceRefs and one active slot; GET reads same record without computation side effect. | E4 HTTP/DB |
| A192-09 | Twenty parallel evaluation submits for one scenario with distinct keys create one active evaluation; losers return 409 `EVALUATION_ALREADY_ACTIVE`; no second job/event. Same-key replay returns the original result and no second job. | E4 concurrency after C192-05 |
| A192-10 | Selected executor moves Accepted→Running→Completed/Failed under CAS; restart and duplicate dispatch obey C192-03. Terminal result persists across restart. | E4/E5 after C192-03/04/06 |
| A192-11 | Approved fixture oracle yields exact Finite/Infinite bottleneck order, required/available/shortfall decimal strings and UoM; no result is inferred from OpenAPI example alone. | E4 after C192-04 |
| A192-12 | Plan/scenario creation and terminal evaluation each commit aggregate/state, scoped receipt, actor/time audit and one outbox record atomically; injected precommit faults yield zero partial writes; postcommit uncertainty recovers from receipt. | E4 fault/restart |
| A192-13 | Only frozen events `capacity.plan.created.v1`, `capacity.scenario.created.v1`, `capacity.evaluation.completed.v1` are emitted. Stable event ID, payload, contractVersion, original correlation and causal link survive retry; publisher replay does not duplicate business event. | E4 outbox; E5 bus after C192-07 |
| A192-14 | Missing/invalid JWT is 401, missing permission 403, valid read/create/scenario/evaluate permission maps to the four pack keys; auth/permission runs before replay. | E4 security |
| A192-15 | Bad/missing UUID correlation, invalid JSON/date/decimal or missing required field returns contract-shaped 400 with no write; GET/POST response `X-Correlation-Id` and error correlation match request. | E4 HTTP/DB |
| A192-16 | Same key/different payload, stale Version and concurrent CAS follow exact owner disposition; no silent overwrite or second audit/outbox. | E4 after C192-05 |
| A192-17 | DB-010 uses one fixed test DB, separate tenant/LE scopes, tenant-first indexes and soft-delete visibility. No direct DEMAND or MOD-0190 DB/internal-type sharing. | Architecture/E4 |
| A192-18 | Relevant service build/tests and architecture gates pass; independent VER rebuilds exact source and traces source→binary→process→HTTP/DB. Gateway, live DEMAND/constraint, Event Bus and G5 are separately labeled. | E2/E4/E5 by scope |

For R22-like collision, unknown commit, long decimal and outbox failure evidence, the eventual DEV/VER must use exact failpoint mechanisms only if specifically authorized; this spec grants no runtime fault endpoint or product-source edit.
