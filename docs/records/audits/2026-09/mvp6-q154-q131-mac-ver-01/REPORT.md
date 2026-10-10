# Q154 — Q131 Mac build/test + independent VER: overlay-q131a on BASE-STACK v1 — SOP §22 / §37

| Field | Value |
|---|---|
| WP | Q154 · testing-agent (lead) + read-only-auditor (independent VER) + security-agent · `/test` · SOP v2.4 §17.1, §20, §22, §25, §32, §37 |
| Writer / independence | Q131a was written in LANE 4 (Cowork). This Mac Claude Code session did **not** write it. It did investigate UC-01 (Q152), where the Codex candidate the overlay reviews came from. **Not a new session** (DV-1) |
| Base stack | BASE-STACK v1 — `docs/records/audits/2026-09/mvp6-base-stack-v1/BASE-STACK-v1.md` sha256 `80e31c156f08747c934097dcd5a26add8581e357b9f20e9300f4a0fac33534fe` (BASE a8a236de → Q117 83e6322c → Q121 93bf1c07 → overlay-q131a `4a4a0860954f437265e21b90a4c028ac282a7444343b5d6d8ea8fa31b7434acf`) |
| Repo | `feature/mvp6-logistics` @ `4a8d4d4b…`, READ-ONLY; `git status --porcelain` only; working tree and UC-01 files not used and not touched |
| Workspace | `~/mvp6-env/q154/` (kept as evidence, 5.6 GB) |
| Time | preflight 19:58:11 → end 20:30 +03:00, 2026-09-27 |

## 0. Verification report (SOP §37)

```text
VERIFICATION REPORT

WP ID:                Q154
Verifier:             Mac Claude Code (testing-agent + read-only-auditor + security-agent)
Verification date:    2026-09-27
Branch/HEAD:          feature/mvp6-logistics @ 4a8d4d4b339528a88e6220fb8402e5a2c771136c (start and end)

Agent Verdict:        PASS WITH OBSERVATIONS — overlay-q131a works as designed: builds 0 errors; fail-closed on unset and on every
                      sabotage value, with 0 connection attempts; CapacityPlans 48/48 via MVP6_MOD0192_MONGO_URI (child worker inherits);
                      architecture 18/18; 0 new 27017 connections; two slots in parallel without collision.
                      Two B-only failures in the single full runs, both shown NOT to be caused by the overlay (O-1 intermittent
                      pre-existing BRD drop race; O-2 environment-dependent Loads test that fails identically on tree A in the same env).
Verification Verdict: PASS (criterion 4a "B 0 new failures" met on the evidence of O-1/O-2; CT to confirm this reading)
CT Status:            returning to CT — Agent PASS ≠ CT ACCEPTED

Evidence level achieved:  EXECUTED (build + tests + negative/sabotage + parallel slots on the Mac, lane-local mongod)
Required evidence level:  EXECUTED

Checks:
- scope:          20 overlay files = OVERLAY-MANIFEST (15 modified, 5 new); every preimage = its BASE-STACK layer
- build:          A and B: 5 targets each, 0 errors; warnings A = B (127 · 1 · 0 · 0 · 0); 0 warnings in overlay files
- tests:          see TEST-A-vs-B.tsv
- security:       no credential accepted; URIs never echoed; only loopback, only non-operational ports
- 27017:          0 new connections (dev log + lsof sampler, both with positive controls)
- console/security leakage: n/a (no UI)

Failed criteria:  none (see O-1, O-2)
Rework required:  no (overlay); separate follow-ups O-1, O-2, O-3
Next gate:        CT disposition of Q131a/Q154 → BASE-STACK v2 candidate; R4 (per-session ports) may proceed on this evidence
```

## 1. Preflight and inputs

- **Environment:** Darwin; `GIT_OPTIONAL_LOCKS=0`; no `index.lock`. The CT-QUEUE row Q154 exists (line 193, READY).
- **Baseline:** 29 ` M` + 2 `??` UC-01 (`git status --porcelain` hash `a33f7e01…`, the same at start and end).
- **BASE-STACK v1:** file `80e31c15…` ✓; folder SHA256SUMS 2/2.
- **Layer evidence folders:**
  - Q103: 14/14 · Q117: 12/12 · Q121: 3/3;
  - BASE-MANIFEST `a8a236de…`;
  - `~/mvp6-env/base/src` 14,566/14,566, 0 writable (before and after).
- **Overlay:**
  - `overlay-q131a.tar` `4a4a0860…` ✓;
  - folder SHA256SUMS 5/5 (file `35c9e0fe…`);
  - 20 members = OVERLAY-MANIFEST; 0 unsafe or AppleDouble members.

## 2. Composition (`tools/compose_q154.py`; `logs/COMPOSE-LOG.txt`)

| Tree | Recipe | Result |
|---|---|---|
| A | BASE clone (`cp -c -Rp`) → Q117 (10 replaced, 1 added) → Q121 (2 replaced); `cp -p` from staging | **14,567** files, 0 mismatch / extra / missing vs BASE-MANIFEST + layer postimages |
| B | A clone → overlay-q131a: **every preimage = the tree-A file**; 15 replaced, 5 added | **14,572** files, 0 mismatch / extra / missing |
| A vs Q121b `src` | `diff -rq` | **0 lines** → tree A is the exact tree of Q121b/Q121c, so their TRX are valid A references (DV-2) |

Both trees were set read-only; builds ran in clones. Staging (`stage/q117`, `stage/q121`, `stage/q131a`) is kept.

## 3. Build (`BUILD-SUMMARY.tsv`)

Platform Application.Tests, BackgroundJobs.Tests, Eventing.Tests, SupplyChainService.sln and TenantArchitecture, for A and B: **0 errors**. Warnings are identical: 127 / 1 / 0 / 0 / 0. None of B's warnings is in `CapacityTestMongo*`, `PlatformMongoTestConnection*` or the script.

## 4. Tests (environment: slot 6, `mongod 127.0.0.1:36994 --replSet rsq154s6 --setParameter enableTestCommands=1`, variables only from the overlay's `mvp6-test-mongo-env.sh --slot 6 --rs rsq154s6 [--all-supplychain]`)

| Req. | Result |
|---|---|
| **4a Platform A vs B** | Application: A 4333/4397 (64) → **B 4343/4408 (65)**. The 11 new helper cases pass, and the 64 A failures are unchanged. The **1** B-only failure is intermittent and pre-existing (**O-1**). BackgroundJobs B 19/21: the 2 failures are the same `BackgroundJobContractsTests` that fail on A live; the overlay-touched `PlatformContainerValidationTests` **passes**. Eventing A = B 65/68 (3 opt-in skips) |
| **4b Negative control** | All variables unset: 159 Platform + 32 CapacityPlans + 1 BackgroundJobs tests fail with "… is not set … fail closed". **0** connection attempts; runs finish in ms (`NEGATIVE-CONTROL.txt`) |
| **4c Sabotage** | 27017, omitted port, fake credentials, non-loopback host and `mongodb+srv` are each **rejected** in the Platform, BackgroundJobs and Capacity sets. Capacity without `replicaSet` is rejected too. No URI or credential echoed. The env script refuses 8/8 bad inputs with empty stdout (`SABOTAGE.txt`) |
| **4d CapacityPlans** | **48/48** (39 + 9 helper) with `MVP6_MOD0192_MONGO_URI`. `CapacityRestartTests` main **and ChildWorker** pass, so the child inherits the variable |
| **4e SupplyChain** | A (Q121c) 407/408 → **B 415/417**. The 9 new cases pass. The 1 B-only failure fails identically on tree A in the same environment (**O-2**) |
| **4f Architecture** | **18/18** on A and B |
| **5 Netcheck** | **0 new connections to 27017** over 20:04:58–20:27:23: dev log 0 `Connection accepted` (positive control 44); lsof sampler 1,256 samples, 0 new sockets (positive control PASS) (`NETCHECK-27017.txt`) |
| **6 Parallel slots** | Slot 6 (36994) and slot 7 (37994) at the same time: CapacityPlans 48/48 on each (2 m 07 s overlap), then Platform Mongo 14/14 on each. **No collision** (`PARALLEL-SLOTS.txt`) |
| **7 Cleanup** | Both lane mongods shut down cleanly. Only this run's test DBs were dropped, and they are listed: slot 6 **110** (incl. about 95 per-run `diten_platform_brd_itest_*_<guid>` DBs), slot 7 **2** (`DitenSupplyChain_Mod0192_Test`, `diten_platform_itest_task_comment_order`). 27017 untouched; `~/mvp6-env/q154` kept |

## 5. Observations

| ID | Sev | What | Evidence | Owner |
|---|---|---|---|---|
| **O-1** | LOW | `BusinessReferenceDataMongoResidueSweeperTests.Sweep_DropsOnlyStaleOwnedMarkedDatabaseFromPreviousRun` failed once in B's full run: "Item not found" + "dropDatabase failed: The database is currently being dropped". It is the pre-existing concurrent-drop race between BRD test classes; A already has 6 failures with this signature (Q132 family). The class alone on B passes 3/3. The BRD namespace on B gave r1 = A's 49 + this test and r2 = exactly A's 49. The overlay changes only where the file gets its connection string | `logs/CMP-platform-A-vs-B.txt`, `logs/RERUN-SUMMARY.txt` | Platform test owner (Q132) |
| **O-2** | LOW (env) | `Loads.LoadAtomicityTests.UnknownCommitResultRetriesAndCommitsExactlyOnce` fails on the slot-6 mongod on **both** A and B (Loads namespace 32/33 ×2 each). It passed in Q121c on a fresh 57192 mongod. Not overlay-related (Loads is untouched). It is sensitive to the Mongo environment: possibly the shared lane mongod already holding other suites' DBs, or fail-point timing | `logs/RERUN-SUMMARY.txt`, `logs/CMP-supplychain-A-vs-B.txt` | SupplyChain test owner |
| O-3 | INFO | A full Platform run creates ~95 per-run BRD databases (GUID names; `KnownPerRunDatabase` exceptions). With Q131a they land on the lane mongod instead of the dev 27017, which is the intended improvement, but they must still be dropped after each run | `logs/CLEAN-slot6-dbs.txt` | Platform test owner |
| O-4 | INFO | `PlatformContainerValidationTests`' local check reports a `mongodb+srv` URI as "non-loopback" rather than "not plain mongodb://". It is still rejected; the message is less precise than the shared helper's | `SABOTAGE.txt` | overlay writer (cosmetic) |
| O-5 | INFO | ENV-VARS §3 expectations confirmed: Platform = Q121b + 11 helper cases; SupplyChain = Q121c + 9 (apart from O-2) | this report | — |

## 6. Deviations

- **DV-1:** This is the continuing Mac session (Q121b/Q121c/Q129/Q152), not a new one. It did not write Q131a.
- **DV-2:** "Same environment for A and B" could not be met literally for two suites. Tree A's Platform Mongo tests hard-code 27017 and its CapacityPlans tests hard-code 57192, both forbidden here.
  - A's references are the Q121b (Platform) and Q121c (SupplyChain) runs, on a tree proven byte-identical to A.
  - Everything in A that can run without 27017/57192 ran live in the same environment: Eventing, Architecture, BackgroundJobContractsTests, Loads.
  - A's full BackgroundJobs run was skipped, because A's container-validation test would probe the dev 27017.
- **DV-3:** One of this lane's shell commands contained a stray `rm -f /dev/null` (leftover text). It had no effect: `/dev/null` is root-owned, and it was verified intact right after. No file of the repo, BASE, `~/mvp6-env` or the workspace was removed.
- **DV-4:** `~/mvp6-env/q154` is kept (5.6 GB). The per-user `$TMPDIR` got 2 small scratch files (`q154-err`, `q154-final.txt`), which were not deleted. `logs/TIMES.txt` and `logs/PARALLEL-TIMES.txt` are identical copies.
- **DV-5:** Extra reruns were made beyond the WP to classify O-1/O-2: BRD class ×3, BRD namespace ×2, Loads A/B ×2, A BackgroundJobContractsTests. All ran on lane mongods.
- **DV-6:** The first pattern secret scan of this folder had 2 hits.
  - Both were the deliberate **fake** sabotage userinfo (case S3) inside the archived `tools/test-neg-sab.sh`; it is not a credential.
  - `logs/TOOLS.tar.gz` was rebuilt from a copy with that value replaced by `<FAKE-USERINFO-REDACTED>` (`logs/TOOLS-REDACTION-NOTE.txt` holds the original script's sha256; the executed script stays in `~/mvp6-env/q154/tools/`).
  - The rescan passes: `SECRET-SCAN.txt`, 0 hits.

## 7. Files

`REPORT.md` · `BUILD-SUMMARY.tsv` · `TEST-A-vs-B.tsv` · `NEGATIVE-CONTROL.txt` · `SABOTAGE.txt` · `NETCHECK-27017.txt` · `PARALLEL-SLOTS.txt` · `FINAL-CHECK.txt` · `SECRET-SCAN.txt` ·
`logs/` (`RAW-LOGS.tar.gz` = all build/test/negsab/rerun/parallel logs + TRX; compose log; layer checks; A-vs-B comparisons; classified negative/sabotage; env-script refusals; netcheck samples + dev-log parse; mongod start records; DB drop lists; `TOOLS.tar.gz` (redacted, see `TOOLS-REDACTION-NOTE.txt`)) · `SHA256SUMS`

## 8. To-do (CT)

1. Dispose Q131a on this evidence, e.g. as a BASE-STACK v2 candidate (BASE → Q117 → Q121 → Q131a), and decide the UC-01 files per Q152 R-1/R-2.
2. O-1: fix the BRD residue-sweeper race (Q132 family), e.g. by serializing the BRD classes into one xUnit collection.
3. O-2: investigate the Loads UnknownCommit test's environment sensitivity on a shared lane mongod.
4. R4 / Q146: the parallel-slot trial found no collision; keep one build tree per session.
5. Kit: wire `mvp6-test-mongo-env.sh` + `enableTestCommands=1` into K05 (separate kit change, as ENV-VARS §2 says).

Agent PASS ≠ CT ACCEPTED — returning to CT.
