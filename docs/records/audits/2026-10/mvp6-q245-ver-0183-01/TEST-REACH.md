# Q245 — What MOD-0183's 74 tests actually execute

Static reading of the seven test files; outcomes cited from Q208 (`NEW-BASELINE.tsv`: Shipments 74 / 74).
Paths are under `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/`. Nothing was run.

## Answer

**MOD-0183 is the exception.** Unlike Returns and Claims (Q210, Q211), twelve of its tests start the whole
service in-process and drive it over HTTP with a signed token. Controller, middleware, authorization filter,
MediatR pipeline, validators, handlers, repository and Mongo all execute.

What that host is: `WebApplicationFactory<Program>` with environment `Testing` (`ShipmentTests.cs:46,62`), an
in-memory test server, configuration injected in memory, and an HS256 token the test signs itself with a test
secret (`ShipmentTests.cs:28,88-95`). It is not the gateway, not a started process, not the Development
environment, and not a token from the Auth service.

## The 74 cases by what they reach

| Group | File | Cases | Host started | HTTP to controller | Signed JWT | Handler / validator | Mongo |
|---|---|---:|---|---|---|---|---|
| A. HTTP through the composed host | `ShipmentTests.cs` | 12 | yes | yes | yes | yes / yes | yes |
| B. Host started, HTTP not used | `SourceIntakeTests.cs` | 15 | yes | **no** | **no** | no handler; create validator called by the coordinator | yes |
| C. Pure unit — validators | `ShipmentValidatorContractTests.cs` | 32 | no | no | no | validators only | no |
| C. Pure unit — raw document reader | `ShipmentRootStorageTests.cs` | 6 | no | no | no | no | no |
| C. Pure unit — HTTP clients against a fake handler | `SourceClientTests.cs` | 7 | no | no | no | no | no |
| C. Pure unit — manifest values | `Shipments/ShipmentTrackingPodManifestProviderTests.cs` | 1 | no | no | no | no | no |
| D. Asserts nothing | `ShipmentRootHttpTests.cs` | 1 | no | no | no | no | no |
| **Total** | | **74** | 27 | 12 | 12 | | 27 |

Case arithmetic: 12 + 15 (5 facts + 8 + 2 theory rows) + 32 (12 + 10 + 10) + 6 (5 + 1) + 7 (4 facts + 3) + 1 + 1 = 74.

## Hit counts (occurrences in the seven files)

| Pattern | ShipmentTests | SourceIntakeTests | other five | Total |
|---|---:|---:|---:|---:|
| `WebApplicationFactory<Program>` | 1 | 1 | 0 | 2 |
| `CreateClient()` | 13 | 8 | 0 | 21 |
| `UseEnvironment("Testing")` | 1 | 1 | 0 | 2 |
| `new JwtSecurityToken(` / `SigningCredentials(` | 1 / 1 | 0 | 0 | 1 / 1 |
| HTTP requests sent through the helper (`Send(c, …)` / `Send(client, …)`) | 62 | 0 | 0 | 62 |
| A handler constructed directly (`new …Handler(` for a MediatR handler) | 0 | 0 | 0 | 0 |
| `ISender` / `IMediator` used by a test | 0 | 0 | 0 | 0 |
| A validator constructed directly (`new …Validator()`) | 0 | 0 | 3 | 3 |
| `IntakeAsync(` (coordinator called directly) | 0 | 17 | 0 | 17 |
| `Assert.` | 114 | 78 | 65 | 257 |

In group B, `CreateClient()` is called only to start the host; the client is never used
(`SourceIntakeTests.cs:103`, 0 requests).

## What the 12 HTTP tests never do

- Assert an error **code**. `INVALID_SHIPMENT_TRANSITION`, `POD_ALREADY_CAPTURED` and `IDEMPOTENCY_KEY_REUSED`
  occur 0 times in the Shipment test files. Every failure is asserted by HTTP status only, and the
  `correlationId` in an error body is never read.
- Validate a response against the OpenAPI file (0 references to it).
- Replay a transition with the same key, mutate across tenants, call POD without its permission, or capture a
  POD from a wrong state (`ACCEPTANCE-MATRIX.tsv` AC-03b, AC-07b, AU-04, V-08).
- Leave the test host: no gateway, no seeded permission, no Development start.

## A test that proves nothing (F-Q245-4)

`ShipmentRootHttpTests.cs:8` `AcceptanceContractRequiresScopedReadOnlyDetail` consists of
`Assert.Equal("DB-010", "DB-010"); Assert.True(true);` (lines 12–13). It is one of the 74 green cases. Its
comment says the runtime HTTP/JWT execution "is owned by root_uptake probes" — three Python scripts under
`services/Diten.SupplyChainService/tests/root_uptake/`, which are not part of the suite and were not opened here.

## The three highest-risk criteria and the test behind each

| Criterion | Test | What it proves | What it leaves open |
|---|---|---|---|
| Tenant / legal-entity isolation (pack line 249) | `ShipmentTests.cs:161` `Scope_Rbac_Schema_AndCorrelationFailClosed` (lines 164–169); `ShipmentTests.cs:276` (lines 282–283) | A foreign tenant or legal entity gets 404 on detail and total 0 on list, same status as an unknown id | Cross-tenant **mutations** are not tested — INSUFFICIENT EVIDENCE (AC-07b) |
| Idempotent replay without a second write or event (pack line 245) | `ShipmentTests.cs:190` `ReplayConflict_AndConcurrentCreateOrTransitionProduceOneWinner`; `ShipmentTests.cs:120` (lines 137, 149–152) | Create: 8 concurrent same-key calls → one 201, seven 200, one row set; replay survives a restart. POD: same key twice → 201 twice, one row set | **Transition** replay is not tested — INSUFFICIENT EVIDENCE (AC-03b) |
| POD → Delivered atomically, events once (pack line 247) | `ShipmentTests.cs:255` `AllowedBranchesAndPodFailureHaveAtomicHistory` (lines 265–273); `ShipmentTests.cs:120` (line 143) | Injected failure before commit leaves status, POD and outbox unchanged; the retry adds exactly two events; a race gives one 201 and one 409 | Event type names are counted, not asserted |

## Bottom line for the board

- "No controller, handler, validator or real token has ever executed in this service" is **false for MOD-0183**:
  12 tests, 62 requests.
- It stays true that nothing has executed **outside the test host**. Evidence level of the 74 is component
  through an in-process host — E2, not E4.
