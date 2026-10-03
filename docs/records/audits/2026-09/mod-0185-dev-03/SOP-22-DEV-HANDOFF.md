# SOP §22 — MOD-0185 DEV-03 Handoff

WP: MVP6-MOD0185-DEV-03  
Scope: close `GAP-185-A07-STARTUP` and `GAP-185-FAILPOINT` using only isolated test-owned Mongo processes.

## Verdict

**DEV-03 PASS for the approved bounded evidence rework.** Both prior gaps have reproducible evidence. This is a developer handoff only; independent VER-02 and CT acceptance remain required.

## Baseline and preservation

- Branch: `feature/mvp6-logistics`
- HEAD: `4a8d4d4b339528a88e6220fb8402e5a2c771136c`
- Existing dirty worktree and DEV-01/DEV-02 historical reports were preserved.
- DEV-02 handoff hash: `a32ff496ce8986138e09ae5507efed652f5ccb32f97c870074cc6a05c6d63ffe`
- DEV-02 manifest hash: `f9eb90fa8fdf341f92540cbb12acb0f281be504be98ce612f93530d9efd4ddf0`
- Updated manifest: [`changed-files.json`](./changed-files.json)
- No frozen contract, shared, gateway, other-module, `.antigravity`, DCP or operational data path changed.

## GAP-185-FAILPOINT

The previous two red tests were rerun without skipping or changing assertions:

```text
MOD0185_TEST_MONGO=mongodb://127.0.0.1:27785/?replicaSet=mod0185dev03 \
dotnet test ... --filter FullyQualifiedName~Diten.SupplyChainService.Tests.Loads
```

The isolated Mongo was started with `--setParameter enableTestCommands=1`; `configureFailPoint` support was verified by `db.adminCommand({configureFailPoint:"failCommand",mode:"off"})`. Result: **33/33 PASS**, including both unknown-commit tests. TRX: `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/TestResults/mod0185-dev03-loads.trx`.

## GAP-185-A07-STARTUP

### Standalone Mongo

Command used an isolated standalone process on test port `27786`:

```text
mongod --dbpath /private/tmp/mod0185-dev03/a07-standalone \
  --port 27786 --bind_ip 127.0.0.1 --nounixsocket --fork
ASPNETCORE_URLS=http://127.0.0.1:5062 \
  Mongo__ConnectionString=mongodb://127.0.0.1:27786 \
  dotnet .../Diten.SupplyChainService.Api.dll
```

The service exited with code `134` during hosted schema startup. No healthy API was exposed. Evidence: `/private/tmp/mod0185-dev03/a07-standalone-api.log`.

### Unavailable Mongo

Command used an unused local port with a bounded server-selection timeout:

```text
ASPNETCORE_URLS=http://127.0.0.1:5063 \
  Mongo__ConnectionString='mongodb://127.0.0.1:27787/?serverSelectionTimeoutMS=3000' \
  dotnet .../Diten.SupplyChainService.Api.dll
```

The process was not alive after the startup window and exited with code `134`; health was never available. Evidence: `/private/tmp/mod0185-dev03/a07-unavailable-api.log`.

### Index-creation failure

On the isolated replica set, a one-shot `createIndexes` failpoint was injected:

```text
mongosh --port 27785 --eval \
 'db.adminCommand({configureFailPoint:"failCommand",mode:{times:1},data:{failCommands:["createIndexes"],errorCode:10107,errorLabels:["RetryableWriteError"]}})'
```

The API exited with code `134` during hosted schema index creation (`ShipmentSchema.StartAsync`, followed by Loads schema registration); no healthy process was advertised. Evidence: `/private/tmp/mod0185-dev03/a07-index-configure.json`, `/private/tmp/mod0185-dev03/a07-index-api.log`, `/private/tmp/mod0185-dev03/a07-index-clear.json`.

These are fail-closed startup outcomes. Recovery was verified by clearing the failpoint and rerunning the full isolated suite successfully.

## Regression evidence

- Build: existing approved Loads build remained valid; binary/source hashes are in the manifest.
- Full SupplyChain tests: **132/132 PASS**, 0 skipped. TRX: `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/TestResults/mod0185-dev03-full.trx`.
- Loads slice: **33/33 PASS** with real failpoint support.
- A12 runtime capture: **PASS**, 18 verifier checks; create/replay request bytes are 294 and bodyless GET is 0. Evidence: `/private/tmp/mod0185-dev03-runtime/runtime.json`.
- Restart/replay and Pending outbox: **PASS**. Evidence: `/private/tmp/mod0185-dev03-runtime/restart.json`.
- A04 connection-refused and timeout: **PASS**, both 503 `DEPENDENCY_UNAVAILABLE`, isolated database. Evidence: `/private/tmp/mod0185-dev03-runtime/failure-paths.json`.

## Scope and remaining gates

The DEV-02 A12/A04 corrections and all historical evidence remain unchanged. No operational MongoDB `27017`, migration, live ingress, publisher/worker, contract modification or shared composition change was performed. Independent VER-02 must reproduce these results; developer PASS is not CT acceptance or E5/G5.

Source writer work is complete. No remaining DEV-03 gap is known within the approved Loads evidence scope.
