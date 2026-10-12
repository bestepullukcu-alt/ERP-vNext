# Q266 — Recipe: SupplyChain test suite as a regression gate

Measured 2026-10-03 12:46–12:54 +03 on Darwin, branch `feature/mvp6-logistics` @ `4a8d4d4b3`, working tree as it
stood at 12:47 (including uncommitted edits by other lanes — see F-Q266-6).

**Result with this recipe: 417 passed, 1 failed, 418 results.** The one failure is a restart-mode test that refuses
to run in an ordinary suite by design. No genuine assertion failure.

## 1. The variables — one per module

| Module | Variable the tests read | Read at | Replica-set name hard-coded? |
|---|---|---|---|
| Shipments (MOD-0183) | `MOD0183_TEST_MONGO` | `ShipmentTests.cs:30`, `SourceIntakeTests.cs:42` | no |
| Carriers (MOD-0184) | `MOD0184_TEST_MONGO` | `Carriers/CarrierContractTests.cs:24` | no |
| Loads (MOD-0185) | `MOD0185_TEST_MONGO` | `Loads/LoadContractTests.cs:54` | no |
| Returns (MOD-0186) | `RETURNS_MONGO_URI` | `Returns/ReturnAtomicityTests.cs:51` | only as the fallback when unset: `127.0.0.1:27886/?replicaSet=returns_dev` |
| Claims (MOD-0187) | `CLAIMS_TEST_MONGO` | `Claims/ClaimAtomicityTests.cs:75` | no; refuses port 27017 and non-loopback hosts |
| S&OP (MOD-0190) | `MVP6_MOD0190_MONGO_URI` | `SandopPlans/SandopContractTests.cs:5`, `SandopAtomicityTests.cs:15`, `SandopReplayTests.cs` | no; atomicity tests connect directly and drop the set name |
| Capacity (MOD-0192) | `MVP6_MOD0192_MONGO_URI` | `CapacityPlans/CapacityTestMongo.cs:16` | no; any set name accepted, must name one; ports 27017–27021 refused |

Paths are under `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/`.

Not URI variables (leave unset for an ordinary run): `MOD192_CHILD_MODE` (switch for the child worker that
`CapacityRestartTests` starts itself), `CLAIMS_RESTART_MODE` / `CLAIMS_RESTART_TENANT`, `RETURNS_RESTART_MODE` /
`RETURNS_RESTART_*`, `RETURNS_PROCESS_EVIDENCE`.

All seven take the same URI. Every module passes against one instance.

## 2. Provision — one mongod, all three causes covered

One instance covers all three environment causes:
- **Cause 1:** the variable is set.
- **Cause 2:** a loopback port that is not 27017.
- **Cause 3:** test commands are enabled for failpoints, and it is a replica set for transactions.

```bash
# 0. preflight: the port must be free; 27017 belongs to Lane A and is never touched
nc -z localhost 57061 && echo "57061 OPEN — STOP" || echo "57061 free"
df -g /System/Volumes/Data | tail -1        # refuse below 2.5 GiB free

# 1. new timestamped lane folder OUTSIDE the repo (no rm: a reset is a new folder)
E=~/mvp6-env/q266-$(TZ=Europe/Istanbul date +%Y%m%d-%H%M)
mkdir "$E" && mkdir -p "$E/db" "$E/log" "$E/src/services"
```

```bash
# 2. start mongod: single-node replica set, test commands on
mongod --port 57061 --bind_ip 127.0.0.1 --replSet rsq266 \
  --dbpath "$E/db" --logpath "$E/log/mongod.log" \
  --setParameter enableTestCommands=1 --wiredTigerCacheSizeGB 0.5 --fork
mongosh --quiet --port 57061 --eval 'rs.initiate({_id:"rsq266",members:[{_id:0,host:"127.0.0.1:57061"}]})'
```

```bash
# 3. verify (both must succeed before running anything)
mongosh --quiet --port 57061 --eval 'const s=rs.status(); printjson({set:s.set, members:s.members.length, state:s.members[0].stateStr})'
mongosh --quiet --port 57061 --eval "printjson(db.adminCommand({configureFailPoint:'failCommand', mode:'off'}))"
```

Expected: `{ set: 'rsq266', members: 1, state: 'PRIMARY' }` and `ok: 1`. Measured output is in the §REPORT of
Q266. The start options the server recorded are in `evidence/mongod-options.json`.

## 3. Build — from a copy, not in the repo

Other lanes run services and edit the working tree concurrently, so this lane built from a copy. The copy has no
`bin`, `obj` or `TestResults`. Proof that the copy equals the repo files at copy time is
`evidence/copy-manifest.sha256`: 434 files, compared with `cmp` against the same hashes taken in the repo.

```bash
R=/Users/natig/Projects/ERP-vNext-recovery
cd $R/services && rsync -a --exclude bin/ --exclude obj/ --exclude TestResults/ \
  Diten.SupplyChainService Diten.Building.Blocks "$E/src/services/"
cd "$E/src/services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests"
dotnet build -nodeReuse:false -p:UseSharedCompilation=false -v q     # 0 warnings, 0 errors, 28.5 s
```

## 4. Run — per module, only that module's variable set

The script is `evidence/run-modules.sh`. It unsets every variable first, then runs each module with exactly one set.
It uses a 10-minute alarm per module. Core of it:

```bash
URI='mongodb://127.0.0.1:57061/?replicaSet=rsq266'
env MOD0183_TEST_MONGO="$URI"     dotnet test --no-build --filter 'FullyQualifiedName~Diten.SupplyChainService.Tests.Shipment|FullyQualifiedName~Diten.SupplyChainService.Tests.Source'
env MOD0184_TEST_MONGO="$URI"     dotnet test --no-build --filter 'FullyQualifiedName~Diten.SupplyChainService.Tests.Carriers.'
env MOD0185_TEST_MONGO="$URI"     dotnet test --no-build --filter 'FullyQualifiedName~Diten.SupplyChainService.Tests.Loads.'
env RETURNS_MONGO_URI="$URI"      dotnet test --no-build --filter 'FullyQualifiedName~Diten.SupplyChainService.Tests.Returns.'
env CLAIMS_TEST_MONGO="$URI"      dotnet test --no-build --filter 'FullyQualifiedName~Diten.SupplyChainService.Tests.Claims.'
env MVP6_MOD0190_MONGO_URI="$URI" dotnet test --no-build --filter 'FullyQualifiedName~Diten.SupplyChainService.Tests.SandopPlans.'
env MVP6_MOD0192_MONGO_URI="$URI" dotnet test --no-build --filter 'FullyQualifiedName~Diten.SupplyChainService.Tests.CapacityPlans.'
```

The Shipments filter is by namespace. `FullyQualifiedName~Shipment` alone is wrong: it matches 81 tests, 18 of
them in Claims, Loads and Returns, and misses the `Source*` tests of MOD-0183.

The seven filters together cover every listed test exactly once (`evidence/all-tests.txt`: 418 lines, 0 unassigned).

## 5. Gate rule

A run is green when:
- every module except Claims shows 0 failed;
- Claims shows exactly 1 failed, `ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery`, with the
  message `Explicit write/read restart mode required; exclude from ordinary suite.`

Anything else is a candidate regression.

Run time: about 5 minutes for all seven (306 s wall).

## 6. Tear down

```bash
mongosh --quiet --port 57061 --eval 'db.adminCommand({shutdown:1})'
nc -z localhost 57061 && echo "57061 STILL OPEN" || echo "57061 free"
```

The data folder (`$E/db`, 27 MiB after a full run) stays in place: no `rm`. The next run uses a new `$E`.
