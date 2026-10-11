# MVP6-MOD0185-FRESH-EVIDENCE-VER-03 — SOP §22

Date: 2026-09-20. Role: independent verifier, no product rework.

## Verdict

**PASS for the requested fresh, source-bound bounded verification evidence.** Historical raw artifacts remain missing; these are new executions and new bytes. This is not a new CT acceptance, full-module completion, root uptake, E5/G5, gateway/UI, deployment or downstream authorization.

Input tables actually contain **19 missing rows**, not18. DEV02/DEV03/VER02 copies of the same runtime/restart/failure claims were deduplicated into one fresh execution group. `missing-to-fresh.tsv` maps every row. Old handoff/manifest bytes are not recreated; this report and fresh manifests replace their provenance claims only. A04/A07/A12 historical closures remain intact.

## Baseline and scope

Branch `feature/mvp6-logistics`, HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c`.
Disposable root: `/private/tmp/mvp6-mod0185-fresh-ver03-oapt3ppw/`.
Real checkout dirty state preserved. Only this new audit directory written by verifier. No source correction, contract/pack/guard/git mutation.

All47 unique Loads source/test files match consolidation pins, including the accepted A04 failure probe successor. Program.cs is pinned to `7fdb5ef0d322c3b9c814fab709dcfbaf1c9a2904f3d9a39e6f90bce077f204d8`.
Loads implementation source-set: `246f20f85b28a0c2abf5af40992a20056fce57ec0041dfd5cc58eb370eab33c3`.
Fresh API DLL: `01ff7878e3783fb2cb9d98db09f9ddc7daaa06666b698e60a49fe789887443b8`.

Concurrent producer changes were detected in GetShipmentByIdHandler, ShipmentProjection, IShipmentRepository and ShipmentRepository, plus new ShipmentDetailReadResult/ShipmentDetailMaterializer files. Only the disposable copy selects tracked producer bytes from the stated HEAD and excludes new producer files. Real files were untouched. This is the approved Loads composition with a pinned historical producer baseline, **not a verification of current producer root uptake**. Selection is recorded in `excluded-producer-delta.json`; full244-file build closure in `build-input-manifest.json` / `build-inputs.tar.gz`. Shared dependencies are byte-pinned. No assumption that HEAD contains the untracked approved Loads/Carrier additions: those additions are explicitly included from the snapshot.

Frozen SHIPMENT-BUNDLE2.0.0 hash `93c696e2fba13dbc8fbfcf2cd1ae0ae0bd93cd9d0935b3ee743229e810163571`; Loads annex `a2187c934cf9176cae4cb8b9c70dfd1298154e5124cdc197d3029103636c2be1`; Carrier annex `87557ef9f5b5a4427862effbece2bb528ebc1b95361c29416373709223021fee`. No R2/root contract substituted.

## Fresh command and result ledger

Exact commands/cwd/exits in archive `*.command.json`; API PIDs/commands/binary hashes in `*-processes.json`. `*-http.json` contains actual outbound requests and received responses, without bearer values. Source→fresh restore/build→binary manifest→spawned command/PID→request/evidence chain is explicit.

- Initial sandbox restore stalled and was stopped (exit143); retained. Fresh restore using local NuGet cache succeeded (exit0). Build succeeded, zero warnings/errors. No preexisting bin/obj reused.
- Loads regression: initial33 tests yielded27PASS/6FAIL with verifier-selected3000ms server selection timeout. FailCommand code91 invalidation plus heartbeat recovery exceeded that bound. **Only the six failures** rerun under driver-default selection timeout:6PASS/0FAIL. Source/assertions unchanged. Both TRXs retained;33 unique latest outcomes PASS, **not a claim of a single33/33 green run**. No broad132-test or architecture rerun.
- HTTP runtime: create201/list200/replay201. Actual request bytes294/0/294; two GET-only scoped mock reference calls; no reread on replay. A12 valid evidence18checks PASS; five fresh negative mutants rejected. Missing historical DEV01 bytes were not fabricated for the negative test.
- Two-process restart: PIDs25547→25548, durable original-result replay/list, exactly one Pending outbox row, no extra dependency reads; verifier7checks PASS.
- A04 refusal and timeout: each503 DEPENDENCY_UNAVAILABLE; matching request/body/header correlation. Same DB and tenant/LE scope; before/after/delta for loads, load_assignments, loads_receipts, loads_audit, loads_outbox all zero. Independent aggregation observations at all four before/after boundaries agree, exit0.
- A04 negative controls:20 distinct records (5collections × missing/invalid/nonzero/query-failure), all rejected. Five deliberate wrong-replica queries exit1; four real before/after queries exit0. Wrapper bounds only wrong-replica command selection to1.5s so failure produces real exit rather than supervisor timeout; effective commands preserved. This changes no application source or evidence predicate.
- A07: test-command enablement and configureFailPoint verified. Standalone27786 fails with transaction-support exception; unavailable27787 fails with selection timeout; createIndexes failpoint on27785 fails with MongoNotPrimaryException at ShipmentSchema startup. Each actual API exits SIGABRT(-6; shell134), no health success, no supervisor kill. After explicit failpoint clear, same binary starts and health200 returns. Logs and configure/clear outputs retained.

## Acceptance scope matrix

|AC|Fresh evidence|Disposition / boundary|
|---|---|---|
|A01|runtime.json; Loads TRX|Three bounded routes covered by HTTP and existing Loads tests|
|A02|Loads contract tests in TRX|Loads contract checks fresh; historical full132 results not repeated|
|A03|LoadIsolationTests in TRX; scoped HTTP/mock captures|Loads tenant/LE/RBAC checks fresh; no gateway claim|
|A04|failure-paths.json; independent-query.json; failure-http/processes|Two503 paths, five collections,20negatives fresh PASS; historical CT03 closure preserved|
|A05|lifecycle source×target test targeted recheck|Latest PASS; first failure preserved|
|A06|runtime/restart; LoadReplayTests recheck|Historical-result replay/no reread fresh PASS|
|A07|LoadAtomicityTests; startup-results/logs; startup-recovery|Failpoints/startup fresh PASS; initial timeout configuration failure disclosed|
|A08|LoadReferenceTests/LoadConcurrencyTests in TRX|Existing bounded rules fresh; no new business oracle|
|A09|restart.json plus raw mongosh/process record|Two actual processes and Pending outbox fresh PASS|
|A10|runtime/restart mock captures|GET-only mocked seam, no live Producer integration|
|A11|fresh restore/build +33 unique Loads checks|Necessary Loads regression only; wider132/architecture results remain historical|
|A12|runtime-http.json; a12-controls.json/logs|18positive checks +5fresh negative records; transport294/0/294|

## Persistence/security/operations

Only test-owned Mongo replica set27785 and standalone27786 were started;27787 is deliberately unused. No operational27017 use. DB-010 fixed names: diten_mod0185_tests, diten_mod0185_runtime_probe, diten_mod0185_restart_probe, diten_mod0185_failure_probe, diten_mod0185_startup_probe; each tenant-scoped scenario has fresh IDs, no GUID database names. Real Mongo transactions/failpoints for Loads tests, real Kestrel/JWT for probes, GET-only local dependency mocks. No operational database, migration/backfill, deployment, publisher activation or live root seam claim. Test-owned API/standalone/replica processes terminated; cleanup recorded.

Audit/observability: raw API/Mongo logs, HTTP bodies/headers, commands/exits, query outputs, TRXs and process/binary/source fingerprints archived. Fail-closed behavior, receipts/audit/outbox checks are fresh only for the tested slice.

## No-change and remaining limits

Build inputs, built binaries,47Loads pins and inspected records checked after execution. Concurrent producer changes prevent a repository-wide no-change assertion; `no-change.json`, preflight/final git inventories delimit the claim. No verifier source edits occurred. Product defects found: none established in the completed checks. First-run infrastructure configuration failures are retained, not erased.

Remaining blockers for **this fresh evidence task**: none. Historical byte recovery is still MISSING; old artifacts/old hashes are not returned. CT must consume this new package as fresh evidence, not retroactively insert it into older manifests. Full work-package acceptance and any downstream authorization remain separate decisions. Runtime root uptake, current Producer integration, E5/G5, operational data and deployment remain outside scope.

## Permanent inventory

`evidence.tar.gz` contains raw outputs, reproduction helpers, full selected source snapshot, internal SHA256SUMS and exact source/binary/process manifests. `evidence-manifest.sha256` mirrors internal inventory. `SHA256SUMS` seals this report, row mapping and archive. Raw historical records were read only. No commit/push/stash.
