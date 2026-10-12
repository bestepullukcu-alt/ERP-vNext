# MOD-0192 independent bounded acceptance matrix

Controlling sources: published `sandop-capacity.openapi.yaml` SHA-256 `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3`, published semantics annex SHA-256 `eb1df1383e637c744179abe4c8faa3b3b7ebe4cccd19738aadf41896a9311bda`, promoted pack SHA-256 `d01bf7a050472a9708a989d7bd4d13ab0a41a81984a272bbb15848f93fcac6e0`. `PASS` below is limited to the named isolated core/fixture assertion. No composed HTTP/JWT evidence exists.

| ID | Independent disposition | Controlling observation and limit |
|---|---|---|
| A192-01 | PARTIAL | Six route methods compile (`CapacityPlansController.cs:11-34`); `Program.cs` has no Capacity registration, so route/header/permission HTTP is open. |
| A192-02 | PARTIAL | Exact-source Mongo test persists scoped Draft plan, provenance, receipt/audit/Pending event (`CapacityAtomicityTests.cs:14-34`); no composed JWT→DB proof. |
| A192-03 | PARTIAL | Fixture tuple and checksum rejection are tested (`CapacityAtomicityTests.cs`, `CapacityIsolationTests.cs`); no real DEMAND producer, and all wrong ID/version variants are not exercised. |
| A192-04 | PARTIAL | Tenant-first active-plan unique index exists (`CapacitySchema.cs:20`); concurrent duplicate-plan/scope race is unproved. |
| A192-05 | PARTIAL | Opaque constraint and decimal-string adjustment persist; UoM mismatch returns 422 (`CapacityIsolationTests.cs`). No live constraint source or HTTP wire proof. |
| A192-06 | PARTIAL | Foreign tenant/LE and deleted plan reads are absent (`CapacityIsolationTests.cs`); all nested scenario/evaluation and HTTP 404 cases remain open. |
| A192-07 | NOT REACHED | Bounded path only produces Draft; no approved operation reaches the forbidden plan state branch. |
| A192-08 | PARTIAL | Accepted evaluation, receipt and slot persist in Mongo (`CapacityAtomicityTests.cs:42-50`); no composed 202/GET proof. |
| A192-09 | PARTIAL | Twenty distinct-key calls yield one Accepted evaluation and 19 active-slot conflicts (`CapacityConcurrencyTests.cs:30-42`); same-key race and HTTP unproved. |
| A192-10 | PARTIAL | Claim/renew/fence/terminal repository tests pass; exact-source restart passes in targeted run but fails in full suite because parent and child use different GUID BSON serialization (details in SOP §22). Hosted executor not composed. |
| A192-11 | PASS, literal fixture only | Finite `line-4/2027-W03` result and Infinite empty result in `CapacityLifecycleTests.cs:32-44`; no general optimizer. |
| A192-12 | PARTIAL | Normal transaction paths and single terminal effect tested (`CapacityAtomicityTests.cs:14-88`); stage fault and unknown-commit injection absent. |
| A192-13 | PARTIAL | Pending event writes exist (`CapacityRepository.cs:42-46`, `CapacityLeaseStore.cs:99-107`); no publisher/bus delivery and no composed event-wire proof. |
| A192-14 | OPEN | Context/permission source exists (`CapacityContextMiddleware.cs:30-65`); no composed authenticated 401/403/404 HTTP. |
| A192-15 | PARTIAL | DTO/context validation source compiles; exact malformed JSON/header/date/decimal HTTP envelope cases untested. |
| A192-16 | PARTIAL | Changed fingerprint and stale fence tested; client Version and unknown-commit cases untested. |
| A192-17 | PARTIAL | Fixed DB, scoped indexes and foreign plan read exercised; architecture result is historical DEV 15/18, not independent fresh PASS. |
| A192-18 | PARTIAL | Fresh exact-source build 0/0 and Capacity 11/11. Fresh full suite 149/150 with one Capacity restart failure, so repository gate FAIL. E5/G5 open. |

## Executor X01–X10

| ID | Independent disposition | Evidence and gap |
|---|---|---|
| X01 | OPEN | No kill/fault before Accepted transaction commit; rollback at this cut unproved. |
| X02 | PARTIAL | Durable repository receipt/replay observed; lost HTTP 202 and restart scan unproved. |
| X03 | PARTIAL | Targeted exact test covers actual 30-second lease and stale fence, but full-suite cross-process restart failed; parent stored string GUIDs while child without serializer looked for BSON UUIDs. No composed service restart. |
| X04 | PARTIAL | Two child claims race only at initial Accepted lease; expired-lease simultaneous race unproved. |
| X05 | PARTIAL | Renewal increments version and rejects stale terminal; renewal outage/cancellation unproved. |
| X06 | OPEN | No terminal precommit fault across evaluation, slot, audit and outbox writes. |
| X07 | OPEN | No unknown terminal commit/result-loss reconciliation injection. |
| X08 | PARTIAL | Attempt 1→2→3/Failed tested with forced expiry; three independent process kills/restarts unproved. |
| X09 | PARTIAL | Pending outbox and child continuation are observed; hosted BackgroundService restart is not composed. |
| X10 | PARTIAL | Foreign plan read/soft-delete tested; cross-scope executor scan/claim/renew/terminal and scoped side-effect counts unproved. |

The independent disposable duplicate-name probe is outside the approved 43-file source set. Sequential and racing requests both produced one 201 and one `409 CAPACITY_SCENARIO_NAME_CONFLICT`; the published `createCapacityScenario` 409 permits only `CAPACITY_PLAN_STATE_CONFLICT` and `IDEMPOTENCY_KEY_REUSED` (`sandop-capacity.openapi.yaml:1619`). This exact branch is **REWORK**, not an accepted A192-05/15 wire result.
