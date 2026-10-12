# Q208 — Provisioning steps (reproducible)

## 1. State found at start

- The Q205 lane (`rsq205s1`, `/private/tmp/q205-testenv-01`) was **gone**: no listener on 31994, the scratch folder
  absent. The homebrew mongod has a new pid (825; it was 758 during Q205). The machine was restarted, which is what
  Q205 predicted (F-Q205-4). Nothing to reuse.
- Listeners before start: `127.0.0.1:27017` and `[::1]:27017` (pid 825) only. Ports 31994, 27886, 57191, 57192: 0 listeners.
- No `dotnet` / `msbuild` / `vstest` / `testhost` process was running. No `.git/index.lock`.

## 2. The accepted script

`scripts/test-env/mvp6-test-mongo-env.sh` — on disk, mode `-rwxr-xr-x`, sha256
`6c936b4a84bb89a27115190c2ea0c87488e690e8ef7dcf1e383ffaeff54d13b1` ✔. It only prints `export` lines (F-Q205-1).

## 3. One replica set serves all seven folders

Every lane variable takes the same URI (as in Q103, Q154 and the script's own `--all-supplychain`). Each folder uses
its own fixed database name, so one mongod is enough.

| Item | Value |
|---|---|
| Slot / port | 1 / 31994 |
| Replica set | `rsq208s1`, single member `127.0.0.1:31994`, `enableTestCommands=1` |
| mongod | `/opt/homebrew/bin/mongod` 8.0.18, pid 2363 |
| Scratch | `/private/tmp/q208-suite-baseline-01/` (`db/`, `logs/`, `results/`) |

## 4. Steps

```bash
S=/private/tmp/q208-suite-baseline-01
cd /Users/natig/Projects/ERP-vNext-recovery
mkdir -p $S/db $S/logs $S/results

# start + initiate (BASE-STACK v2 §4 step 6)
/opt/homebrew/bin/mongod --replSet rsq208s1 --port 31994 --bind_ip 127.0.0.1 \
  --dbpath $S/db --logpath $S/logs/mongod.log --pidfilepath $S/mongod.pid \
  --setParameter enableTestCommands=1 --fork
mongosh --quiet --port 31994 --eval \
  'rs.initiate({_id:"rsq208s1",members:[{_id:0,host:"127.0.0.1:31994"}]})'

# variables: the script's output minus the Platform variable (not read by this test project)
scripts/test-env/mvp6-test-mongo-env.sh --slot 1 --rs rsq208s1 --all-supplychain > $S/env-all.txt
grep -v DITEN_PLATFORM $S/env-all.txt > $S/env-seven.sh
eval "$(cat $S/env-seven.sh)"

# build, then one full run with a hang cap
dotnet build services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests -c Debug
dotnet test  services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests --no-build \
  --blame-hang --blame-hang-timeout 180s \
  --logger "trx;LogFileName=full.trx" --results-directory $S/results
```

The seven variables exported: `MOD0183_TEST_MONGO`, `MOD0184_TEST_MONGO`, `MOD0185_TEST_MONGO`,
`MVP6_MOD0190_MONGO_URI`, `MVP6_MOD0192_MONGO_URI`, `RETURNS_MONGO_URI`, `CLAIMS_TEST_MONGO` — all
`mongodb://127.0.0.1:31994/?replicaSet=rsq208s1&serverSelectionTimeoutMS=5000`.

## 5. Proof

```text
initiate ok=1
setName=rsq208s1 primary=true me=127.0.0.1:31994 state=PRIMARY version=8.0.18
commit count=1      # transaction committed
abort count=1       # aborted insert is gone
enableTestCommands=true
```

## 6. Caps

- Per test: `--blame-hang-timeout 180s` (aborts the run and names the test if one test makes no progress for 180 s).
- Whole run: 25 minutes on the shell command.
- Neither cap was hit. The run took 4 min 22 s wall (19:40:11 → 19:44:33).

## 7. 27017

Not contacted. pid 825 and its two listeners are the same before and after. No variable points at 27017–27021;
the script refuses that band.

## 8. Left behind / restart

- Lane mongod **still running**: pid 2363, `127.0.0.1:31994`. Stop:
  `mongosh --quiet --port 31994 --eval 'db.adminCommand({shutdown:1})'`.
- Databases on it (fixed names, 27 MB dbpath): `diten_mod0183_tests`, `diten_mod0184_tests`,
  `diten_mod0184_index_failure_tests`, `diten_mod0185_tests`, `diten_returns_tests`,
  `diten_returns_collation_tests`, `diten_test_claims`, `diten_test_claims_noindexes`,
  `DitenSupplyChain_Mod0190_Test`, `DitenSupplyChain_Mod0192_Test`.
- **Does not survive a restart** (forked process, `/private/tmp` dbpath, shell-scoped variables) — already proven
  once today. Rebuild = §4, about 10 seconds.
- Idle MSBuild worker nodes from this WP's builds may linger for a few minutes; they are not a build in flight.
