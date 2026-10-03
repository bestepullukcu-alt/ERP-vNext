# Q205 — Provisioning steps (reproducible)

## 1. What the accepted script does — and does not do

`scripts/test-env/mvp6-test-mongo-env.sh` (sha256 `6c936b4a…13b1`, 66 lines), read in full before running.

- It **only prints `export` lines**. Its own header (lines 8–10): "The lane starts that mongod itself … This script
  only prints `export` lines: it starts nothing, connects to nothing and writes nothing."
- Port: `30994 + 1000 × slot`, slots 1–9 (line 42). `--port` may override.
- Replica-set name: **not chosen by the script** — the caller passes `--rs` (line 37, `[A-Za-z0-9_-]{1,64}`).
- 27017: **not touched.** The script opens no connection at all, and it refuses ports 27017–27021 (lines 48–51)
  and ports ≥ 49152 (lines 52–55).
- It always prints `DITEN_PLATFORM_TEST_MONGO_URI` and `MVP6_MOD0192_MONGO_URI`. With `--all-supplychain` it adds
  `MOD0183_TEST_MONGO`, `MOD0184_TEST_MONGO`, `MOD0185_TEST_MONGO`, `MVP6_MOD0190_MONGO_URI`, `RETURNS_MONGO_URI`,
  `CLAIMS_TEST_MONGO` (lines 59–65). All eight get the same URI.
- URI: `mongodb://127.0.0.1:<port>/?replicaSet=<rs>&serverSelectionTimeoutMS=5000`. No credentials.

**So the script cannot stand up a replica set (F-Q205-1).** The Q205 prompt assumed it could. This was put to the
owner in the session; the owner chose: *start the mongod by hand as BASE-STACK v2 §3 / §4 step 6 prescribes and as
Q154 did*. No provisioning script was written. `scripts/evidence-kit/k05_mongo.sh` was not used and not read.

## 2. Values used

| Item | Value |
|---|---|
| Slot | 1 |
| Port | 31994 (free before start: `lsof -nP -iTCP:31994 -sTCP:LISTEN` → nothing) |
| Replica set | `rsq205s1` (single member, `127.0.0.1:31994`) |
| mongod binary | `/opt/homebrew/bin/mongod`, db version v8.0.18 |
| Scratch dir | `/private/tmp/q205-testenv-01/` (`db/`, `logs/`, `results/`, `scripts/test-env/`) |
| Lane mongod pid | 25668 (at the time of writing — still running) |

## 3. Steps

```bash
S=/private/tmp/q205-testenv-01
REPO=/Users/natig/Projects/ERP-vNext-recovery
A=$REPO/docs/records/audits/2026-09/mvp6-q131a-port-uri-overlay-01

# 1. verify, extract one member, verify again (see HASH-VERIFICATION.md)
mkdir -p $S/db $S/logs
(cd $A && shasum -a 256 -c SHA256SUMS)
tar -xf $A/overlay-q131a.tar -C $S scripts/test-env/mvp6-test-mongo-env.sh
shasum -a 256 $S/scripts/test-env/mvp6-test-mongo-env.sh   # 6c936b4a84bb89a2…54d13b1

# 2. start the lane mongod (BASE-STACK v2 §4 step 6; same flags as Q154 REPORT line 74)
/opt/homebrew/bin/mongod --replSet rsq205s1 --port 31994 --bind_ip 127.0.0.1 \
  --dbpath $S/db --logpath $S/logs/mongod.log --pidfilepath $S/mongod.pid \
  --setParameter enableTestCommands=1 --fork

# 3. initiate
mongosh --quiet --port 31994 --eval \
  'rs.initiate({_id:"rsq205s1",members:[{_id:0,host:"127.0.0.1:31994"}]})'

# 4. variables — from the accepted script, only the three in the Q205 config surface
$S/scripts/test-env/mvp6-test-mongo-env.sh --slot 1 --rs rsq205s1 --all-supplychain > $S/env-all.txt
grep -E '^export MOD018[345]_TEST_MONGO=' $S/env-all.txt > $S/env-three.sh
eval "$(cat $S/env-three.sh)"

# 5. run
cd $REPO
dotnet test services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests --no-build \
  --logger "trx;LogFileName=after.trx" --results-directory $S/results
```

The script's full output is in `evidence/env-all.txt`. The other five variables it prints were **not** exported
(config surface = the three `MOD018x` variables only). The test project reads no other variable
(`grep GetEnvironmentVariable` → `MOD0183_TEST_MONGO` ×2, `MOD0184_TEST_MONGO`, `MOD0185_TEST_MONGO`).

## 4. Proof that it is a replica set and that transactions work

mongosh on port 31994, after `rs.initiate`:

```text
setName=rsq205s1 primary=true me=127.0.0.1:31994
rs.status ok=1 members=1 state=PRIMARY
txn committed count=2        # startTransaction → 2 inserts → commitTransaction
after abort count=2          # startTransaction → 1 insert → abortTransaction; the insert is gone
dropped=1                    # probe database q205_txn_probe dropped
```

## 5. 27017 was not touched

| Check | Before | After |
|---|---|---|
| homebrew mongod | pid 758, `--config /opt/homebrew/etc/mongod.conf` | pid 758, same command line |
| listeners | `127.0.0.1:27017`, `[::1]:27017` | the same two, plus `127.0.0.1:31994` (pid 25668) |

No command in this WP connected to 27017. The homebrew mongod was not stopped, reconfigured or restarted.

## 6. Does it survive a machine restart? — **No.**

| Part | After a restart |
|---|---|
| Lane mongod process | gone. It was started with `--fork` from a shell; no launchd / `brew services` entry exists |
| `/private/tmp/q205-testenv-01/` (dbpath, logs, the extracted script) | macOS clears `/private/tmp` at boot — treat as gone |
| The three environment variables | shell-scoped; they were only ever set inside the subshell that ran the tests |
| This record folder (incl. `evidence/`) | stays |

To bring it back: repeat §3 steps 1–4 (about 10 seconds). If `db/` is gone, step 3 (`rs.initiate`) is needed again;
if `db/` survived, skip step 3.

## 7. State left behind

- Lane mongod **still running**: pid 25668, `127.0.0.1:31994`. Stop it with
  `mongosh --quiet --port 31994 --eval 'db.adminCommand({shutdown:1})'`. Nothing was deleted (no `rm`).
- Databases the suite left on it: `diten_mod0183_tests`, `diten_mod0184_tests`,
  `diten_mod0184_index_failure_tests`, `diten_mod0185_tests`. Fixed names, not per-run GUID names.
- `dotnet test` wrote build output into the test project's `bin/` and `obj/` (git-ignored). `git status --porcelain`
  after the runs: 565 entries, unchanged.
