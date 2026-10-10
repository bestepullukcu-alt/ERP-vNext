# MOD-0183 R1 — SOP §22 developer validation report

> R2 evidence-path reconciliation: the protected baseline JSON payload is preserved byte-for-byte in
> baseline-preservation.json.txt. Historical R1 results below are superseded for current acceptance by
> [the R2 rework report](mod-0183-dev-r2-report-2026-09-16.md); the linked inventory now describes the current artifact set.

- WP: MVP6-MOD0183-DEV-01-R1; prompt MVP6-MOD0183-P01 v1.1.
- Lane: AL-MVP6-MOD0183-DEV / DEV; Profile B; HIGH; one writer.
- **Agent verdict:** bounded same-root HTTP core implemented; developer behavior checks PASS. **Overall WP gate NOT READY:** required repository architecture suite is not green; independent runtime acceptance pending.
- **Independent verdict:** specialist found no new concrete bounded-core bug in final static code/artifact review. Specialist did not execute runtime or observe restart; no CT acceptance.
- Branch: feature/mvp6-logistics.
- HEAD: bc109afa4c016877dc4ecf203f8a8e91b512e4dd.
- Worktree: /Users/natig/Projects/ERP-vNext-recovery.
- Changed files: complete [inventory and hashes](mod-0183-r1-evidence/changed-files.json). All new service/audit files; ignored build outputs excluded.
- Existing dirty inputs preserved. No commit, push, stash, branch switch or pack promotion.

## Authority and scope-specific DoR

Read AGENTS.md, supply-chain-execution domain config, ready-for-dev MOD-0183
pack, readiness section 10/approved Phase 1.5, MVP6 development plan, frozen
Shipment/Warehouse/Inventory contracts, orchestrator/add-module, backend rules
and CT SOP. R1 explicitly authorizes implementation; producing E4 is the task.

| Gate | Result |
|---|---|
| Branch/HEAD | Matched before and after work |
| Approved scope | ready-for-dev pack + explicit R1 authorization |
| DCP-002 | verify_module_id.py --check-id MOD-0183 --name "Shipment Tracking & POD": exit 0 |
| Phase 1.5 | Approved five layers, CQRS, L3 Mongo/atomic consistency applied |
| Ownership | One writer; new service and this implementation's audit evidence only |
| Frozen schemas | Unchanged; runtime validation against source YAML |
| Transactions | Dedicated replica set on 27027; fixed isolated test DBs |
| Trusted scope | Signed tenant/LE/actor fixtures; real issuer integration held |
| Baseline | 172 protected/input files SHA-256 unchanged, including packs/contracts |
| Known intake gaps | Automatic Warehouse event/poll adapters absent |

## Exact implemented scope

Five .NET 8 projects: Api, Application, Domain, Persistence, Infrastructure;
listener 5061. GET/POST shipments, GET by ID, POST transition and POST POD only.
List pagination/status/source-document filters, validation, JWT/RBAC, signed
tenant/LE matching, cross-scope 404, soft-delete visibility, normalized durable
replay, payload conflicts, frozen lifecycle, embedded POD, immutable history,
sanitized audit and transactional outbox. Four MediatR behaviors: Validation,
Logging, Exception, Performance. Internal Response<T>/CustomBaseController
adapts to unwrapped frozen payloads.

One transaction writes shipment/POD, history, audit, receipt and outbox. POD
adds Delivered and two frozen events, ShipmentDelivered and PodCaptured.
Only delivery metadata changes during outbox retry.

Warehouse/Inventory IDs are opaque references. No Warehouse master, stock
balance, foreign DB access, automatic intake, UI, gateway/shared registrations
or other modules. Pack-wide criteria remain unpromoted.

## Commands and results

Commands ran from repository root. Restore used local packages; build/test
execution required local IPC/loopback access.

~~~sh
python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0183 --name 'Shipment Tracking & POD'
dotnet restore services/Diten.SupplyChainService/Diten.SupplyChainService.sln --source /Users/natig/.nuget/packages --disable-parallel -p:NuGetAudit=false -m:1 --nologo
dotnet build services/Diten.SupplyChainService/Diten.SupplyChainService.sln --no-restore --nologo -m:1 -p:UseSharedCompilation=false
MOD0183_TEST_MONGO='mongodb://127.0.0.1:27027/?replicaSet=mod0183verify' dotnet test services/Diten.SupplyChainService/Diten.SupplyChainService.sln --no-restore -m:1 -p:UseSharedCompilation=false --logger 'trx;LogFileName=mod0183-r1.trx' --results-directory /private/tmp/mod0183-r1-results
dotnet test tests/architecture/TenantArchitecture.ArchitectureTests --no-restore -m:1 -p:UseSharedCompilation=false --logger 'trx;LogFileName=architecture.trx' --results-directory /private/tmp/mod0183-r1-results
MOD0183_TEST_MONGO='mongodb://127.0.0.1:27027/?replicaSet=mod0183verify' python3 services/Diten.SupplyChainService/tests/runtime_probe.py docs/records/audits/2026-09/mod-0183-r1-evidence docs/analysis/contracts/shipment-bundle.openapi.yaml
python3 services/Diten.SupplyChainService/tests/startup_probe.py docs/records/audits/2026-09/mod-0183-r1-evidence
~~~

| Check | Result | Evidence |
|---|---|---|
| Five-layer solution build | PASS, zero errors/warnings | [build.log](mod-0183-r1-evidence/build.log) |
| Service tests | PASS 9/9 | [mod0183-r1.trx](mod-0183-r1-evidence/mod0183-r1.trx) |
| Repository architecture | FAIL, 15 passed / 3 failed, no final new-service findings | [architecture.trx](mod-0183-r1-evidence/architecture.trx) |
| Real Kestrel and OS-process restart | PASS, **22 HTTP requests**, persisted snapshot equality | [runtime.json](mod-0183-r1-evidence/runtime.json) |
| Frozen schema checks | PASS: command example, responses, six lifecycle events; JSON Schema 2020-12 with formats | runtime.json / runtime_probe.py |
| Standalone Mongo rejection | PASS before listener starts | [startup-rejection.log](mod-0183-r1-evidence/startup-rejection.log) |
| Protected baseline | PASS 172 unchanged | [baseline-preservation.json.txt](mod-0183-r1-evidence/baseline-preservation.json.txt) |

Initial failures were diagnosed/fixed: test-host configuration timing; shared JWT
skew usage and parser dependency mismatch; permission error envelope; OPTIONS
scope bypass. Final results supersede those iterations. Folder formatting passed
after solution-format build-host IPC timed out.

## Golden flow and persistence

Actual HTTP flow: Create → reload Draft → Planned → Dispatched → POD Delivered →
reload → Closed. POD replay returns same ID, 201 and idempotentReplay=true;
distinct duplicate POD returns 409. After terminating and restarting the service
OS process, Closed/POD reload survives. Original create replays its stored Draft
snapshot, same ID and 200 without new records.

[Persisted state](mod-0183-r1-evidence/persisted-state.json): one shipment with
embedded lines/POD, five receipts, five lifecycle entries, five audit entries,
six Pending outbox events. Snapshot equality asserted across restart and replay.
Root correlation, actor, tenant and LE are explicit evidence. Fixture data only;
no JWTs or signing secrets are recorded.

## Failure paths, security and concurrency

Behavior tests run HTTP handlers and actual Mongo repositories:

- All 64 lifecycle pairs checked against independent expected matrix; **52 forbidden**
  pairs exercised through HTTP with unchanged status/history/outbox.
- Eight concurrent same-key creates: one 201 and seven 200 stored replays.
- Changed payload under same key: 409; normalized equivalent request: replay.
- Concurrent transitions: one success, one 422; concurrent POD: one 201, one 409.
- Injected failure after writes/before commit rolls back every collection.
- POD precommit failure preserves Dispatched/no POD and unchanged event counts.
- Missing permission: 403; cancel-only cannot dispatch, dispatch-only cannot cancel.
- Cross-tenant/LE GET, list and mutation isolation; unknown IDs: 404.
- Missing/invalid signatures or missing trusted LE scope fail closed.
- Missing/invalid correlation, missing/oversized key, body scope injection,
  invalid quantity/pagination rejected.
- Soft-deleted shipments hidden from reads, mutation and replay.
- Different-root mutation: containment 400, not acceptance of GAP-0183-04.

Forbidden-pair tests seed each starting state in their isolated tenant fixture
then verify real HTTP rejection. They do not claim all states were reached by
production flow.

Outbox test uses the real shared processor/Mongo store and a transport mock
that fails once. Retry preserves event ID, bytes and correlation; persisted
attempt count becomes one and status Published. Broker delivery, transport LE
binding and multi-worker recovery are held.

## Observability, migration and rollback

Structured application logs include scope/correlation; HTTP echoes correlation.
Audit omits recipient/note/evidence contents. [service.log](mod-0183-r1-evidence/service.log)
explicitly reports dormant transport. Health reports startup only.
Startup applies additive service indexes and validates transactions before
listening. Rollback: stop service, preserve records/pending events. No destructive
migration or operational data cleanup. Verification processes are stopped;
isolated fixtures retained in dedicated test storage.

## ASSUMPTION entries and held gaps

- **R1-A1:** approved readiness A5 same-root policy applies. GAP-0183-04
  needs owner resolution: per-operation header propagation vs immutable first
  command root. No frozen edits or silent UUID replacement.
- **R1-A2:** signed legal_entity_id fixture is trusted scope. Live issuer/shared
  scope integration is unverified. No unsigned header/default/order-based inference.
- **R1-A3:** frozen references are opaque. Inventory mock/read reconciliation
  and failure AC are **not implemented or accepted**; rsv-4001 is sample data.
- **R1-A4:** embedded POD/lines inherit aggregate scope; atomic update enforces
  one POD without an invented separately addressable endpoint.
- **R1-A5:** absent transport leaves Pending events; mock retry proves no live
  Event Bus delivery.

Existing GAP-0183-01/02 Warehouse correlation/LE and GAP-0183-03 frozen
Warehouse/Supplier schema issues remain central-owned. Automatic intake,
gateway, live issuer/permissions, Inventory reads, Event Bus, E5 and full G5
remain unverified. No gap waiver or pack promotion.

## Remaining blockers and CT recommendation

Required architecture checks fail in protected unrelated files:

1. Per-run GUID test databases:
   services/Diten.Platform/tests/Diten.Platform.Application.Tests/Audit/PpmAuditRetentionPolicySeedMongoTests.cs:79;
   services/Diten.Platform/tests/Diten.Platform.Application.Tests/Persistence/DisposableStandaloneMongo.cs:25.
2. Two JWT skew guard tests:
   services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Api/Program.cs:27–32;
   services/Diten.TalentEcosystemService/src/Diten.TalentEcosystemService.Api/Program.cs:26–31.

Route failures to their owners; no cross-owner fixes/waivers here.
Implementation runner produced **E4 evidence for the bounded same-root core**.
Recommend an independent VER WP; do not claim CT acceptance, entire API/pack,
E5 or G5. Do not start MOD-0184. Out-of-scope source changes: **none**.
