# Q121c — SupplyChain re-run on the Q121b "after" tree, lane mongod 57192/rsmod192

WP-MVP6-FIX-121c · Prompt Q121c v1 · Task class: test re-run (environment conflict) · Risk LOW · Lane DEV ·
@testing-agent (/test) · Claude Code on the Mac (Darwin), terminal session (not a LANE chat).
Repo `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`, `GIT_OPTIONAL_LOCKS=0`, read-only except this folder.
2026-09-27, 15:57–16:07 +03:00. Workspace `~/mvp6-env/q121c/`. Closes Q121b deviation D1.

```text
REPORT (SOP §22)

Agent Verdict:   PASS: SupplyChain 407/408 on the Q121b after-tree; outcome-for-outcome identical to Q119 (0 differences);
                 27017 not touched (proven passively, with a positive control); 57192 cleaned and stopped.
                 One evidence-tool defect is disclosed (the live 27017 sampler was broken); replacement evidence is below.
Evidence level:  EXECUTED
CT Status:       returning to CT
```

## Preflight

| Check | Result |
|---|---|
| `uname -s`, index.lock | `Darwin`; `.git/index.lock` absent |
| HEAD / diff | `4a8d4d4b…`; 19 diff paths; `git diff` sha256 `95a5a1e0…efa1` (= Q121b end state); 475 untracked |
| **Port 57192** (15:57:58) | **free**: 0 listeners, no `/tmp/mongodb-57192.sock`; only mongod running was dev 27017 (pid 841). Q122's instances were gone. |
| Q121b evidence | `SHA256SUMS` 20/20 OK (`T0-…`) |
| **Tree** `~/mvp6-env/q121b/src` | 14,567/14,567 = BASE-MANIFEST (`a8a236de…`) with the Q117 and Q121 staged files over it; 0 mismatches; 0 extra (`T1-…`) |
| **Tree** `~/mvp6-env/q121b/build-after` (sources) | 14,567/14,567, 0 mismatches, 0 extra non-bin/obj files. The first pass counted 5 BASE files as missing: `Diten.CrmService…/TestResults/*.trx`, which my `TestResults` exclusion filtered out. They are present. |
| Q121 post-images in build-after | `ea6fa362…`, `4aaec42c…` (= Q121b) |
| SupplyChain sources | 350 files = the Q121b hash list (`sc-src-q121b.sha`, sha `7cfd6dcd…`) |
| Test binary | `Diten.SupplyChainService.Tests.dll` mtime 14:32:46 (Q121b build), sha256 `163eebe2…`. It was not rebuilt; the run used `--no-build`. |

## Run

- Lane mongod started at 15:58 (pid 48248):
  - `127.0.0.1:57192`, `--replSet rsmod192`, `--setParameter enableTestCommands=1`;
  - new dbpath `~/mvp6-env/q121c/test-mongo-192/db`.
- **Ownership was checked before `rs.initiate`** (the listener's command line must contain `mvp6-env/q121c`), which is the lesson from Q121b D1. The node was PRIMARY with only admin, config and local (`M1-…`).
- All 6 env URIs (`CLAIMS_TEST_MONGO`, `MOD0183/0184/0185_TEST_MONGO`, `MVP6_MOD0190_MONGO_URI`, `RETURNS_MONGO_URI`) were set to `mongodb://127.0.0.1:57192/?replicaSet=rsmod192`, the same as Q119. `TMPDIR` = `~/mvp6-env/q121c/tmp/`.
- `dotnet test …SupplyChainService.Tests.csproj --no-build` in `q121b/build-after`, 15:59:20–16:04:09.

| Suite | Q119 (build-fix) | Q121b (57212) | **Q121c (57192)** | Expected |
|---|---|---|---|---|
| SupplyChain single process | 407/408 | 375/408 | **407/408** | 407/408 ✓ |

### Test by test against Q119 (`SUPPLYCHAIN-VS-Q119.txt`)

- Q119 TRX was taken from `mvp6-q119-ver-q117-01/RAW-LOGS.tar.gz:test/build-fix--supplychain-single.trx`; the extracted copy's sha256 equals the archive member's.
- Both runs have 408 results and 407 distinct names; one theory name repeats, and GUIDs are normalised. The name sets are equal.
- **Tests with a different outcome: 0.**
- The only failure in both runs is `Claims.ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery` (`Explicit write/read restart mode required; exclude from ordinary suite.`), which is excluded by design.
- Per module: CapacityPlans **39/39** (Q121b: 7/39), Carriers 36/36, Claims 127/128 distinct names, Loads 33/33, Returns 78/78, SandopPlans 19/19, and the Shipment/Source groups all green.

## 27017 was not touched

1. **Static:** the 5 hits for `27017` in the SupplyChain test tree are all guards that *reject* 27017 (`T2-…`).
2. **Test log:** 0 occurrences of `27017`.
3. **Dev mongod log, read as a file** (`/opt/homebrew/var/log/mongodb/mongo.log`, local +03:00; `S2-…`):
   - During the run window 15:59:20–16:04:10 there were **0 `Connection accepted`**, out of 8 entries in total, all internal (WiredTiger, TTL and similar).
   - **Positive control:** the same parser finds 44 `Connection accepted` during Q121b's Platform window (14:34–14:43), so the method does see connections.
4. **Disclosed defect:** the live 1-second lsof sampler (`S-27017-conn-samples.txt`, empty) was **broken**.
   - zsh glob-expanded the `-iTCP@[::1]:27017` argument, so lsof never ran.
   - Its positive control (`S-sampler-positive-control.txt`) failed in the same way.
   - Its empty output proves nothing. Item 3 replaces it.

## 57192: databases created and dropped (`M2-…`)

- **Created by the run (10):**
  - `DitenSupplyChain_Mod0190_Test`, `DitenSupplyChain_Mod0192_Test`;
  - `diten_mod0183_tests`, `diten_mod0184_index_failure_tests`, `diten_mod0184_tests`, `diten_mod0185_tests`;
  - `diten_returns_collation_tests`, `diten_returns_tests`;
  - `diten_test_claims`, `diten_test_claims_noindexes`.
- All 10 were dropped. The remaining databases are admin, config and local.
- `shutdownServer` (clean); pid 48248 stopped; 0 listeners on 57192; socket removed by mongod.
- The dbpath folder is kept in the lane workspace, and its mongod log is attached (`lane-mongod-57192.log.gz`).

## Residue, processes, repo

- **Processes:** 0 carry `Q121C_LANE` or `mvp6-env/q121c`. The first count of 1 was the checking shell matching its own command line, and a re-check found 0. Only dev mongod 27017 runs.
- **Temp:** `~/mvp6-env/q121c/tmp/` holds 9 **empty** folders and 0 files. They are left in place: no `rm`, lane-local.
- **Repo:**
  - HEAD `4a8d4d4b…`, no index.lock, 19 diff paths.
  - `git diff` sha256 `95a5a1e0…` = preflight, so tracked files are unchanged.
  - Untracked went 475 → 477 besides this folder. Both new items come from other writers: `mvp6-claims-ui-draft-04/` (16:02) and `mvp6-ct-verdicts-q126-q128-q122-q121b-2026-09-27.md` (16:00).
- **BASE:** 14,566/14,566 files, 0 writable. The Q121b trees were only read. No `rm` anywhere.

## Files

`README.md` · `TEST-SUMMARY.tsv` · `SUPPLYCHAIN-VS-Q119.txt` · `T0-q121b-evidence-sums.txt` · `T1-tree-check.txt` · `T2-static-27017-grep.txt` ·
`M1-mongod.txt` · `M2-57192-dbs-created-dropped.txt` · `S-window.txt` · `S-27017-conn-samples.txt` (empty, broken sampler) ·
`S-sampler-positive-control.txt` · `S2-27017-log-window.txt` · `V-repo.txt` · `V-base.txt` · `V-procs-residue.txt` ·
`lane-mongod-57192.log.gz` · `RAW-LOGS.tar.gz` (test log + TRX) · `TOOLS.tar.gz` (`run.sh`) · `SHA256SUMS`

## To do

1. CT: Q121b D1 is closed. Q121 proof is now complete (architecture 18/18, Platform before = after, SupplyChain 407/408), and Q121 can go to integration with Q117.
2. Separate WP: have CapacityPlans read an env URI like the other modules do, so that lanes stop competing for 57192.
3. Lane tooling: quote lsof `[::1]` arguments in zsh, or use `setopt noglob`. Any future live sampler needs a passing positive control before its output counts.
4. Still open from Q121b: the 64 pre-existing Platform failures, and the Platform tests that write to 27017.

Agent PASS ≠ CT ACCEPTED — returning to CT.
