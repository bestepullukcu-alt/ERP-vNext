# Q121b — build/test proof of the Q121a overlay (Mongo test-DB guard fixes)

WP-MVP6-FIX-121b · Prompt Q121b v1 · Task class: build/test proof · Risk LOW · Lane DEV (build step), not the Q122 session ·
@testing-agent (/test) · Claude Code on the Mac (Darwin).
Repo `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`, `GIT_OPTIONAL_LOCKS=0`, read-only except this folder.
Start ~14:17, end ~15:02 +03:00, 2026-09-27. Workspace `~/mvp6-env/q121b/`.

```text
REPORT (SOP §22)

Agent Verdict:   PASS WITH DEVIATIONS: Q121 proven (build 0 errors; architecture 17/18 → 18/18; Platform before = after;
                 the 3 touched facts pass both times). SupplyChain 375/408 ≠ 407/408, caused by the environment and not
                 by Q121 (D1). Residue on the shared 27017 was created and then removed with the owner's consent (D2).
Evidence level:  EXECUTED (build + test on the Mac, before and after on the same machine)
CT Status:       returning to CT
```

## Inputs (verified before any work)

| Input | Result |
|---|---|
| `uname -s` | `Darwin`; no `.git/index.lock` |
| HEAD | `4a8d4d4b…` = expected; 19 tracked diff paths |
| BASE `~/mvp6-env/base/BASE-MANIFEST.tsv` | sha256 `a8a236de82a9f2b5d4c0f3560aa13894c3740c6e187cb0140f5da1b08109c661`; `src` 14,566/14,566 match, 0 writable (before `P1-…`, after `P5-…`) |
| Q117 overlay `q117-fixes-overlay.tar.gz` | `83e6322c1d4dd46c80b0b4de0a2b60493883a7b2d923c7685da689de702f800d`; Q117 SHA256SUMS 12/12 OK |
| Q121 overlay `q121-mongo-guard-fixes-overlay.tar.gz` | `93bf1c07249c43853870cd74150a1f0813bff1d74a8661259f8e14112258ea90`; Q121 SHA256SUMS 3/3 OK; 2 members, 0644, 0/0 |

## Composition (cp only)

- `before/` = `cp -Rc` (APFS clone) of BASE `src` + the Q117 overlay copied over it (11 files; 14,567 files in total).
- `src/` = `cp -Rc` of `before/` + the Q121 overlay copied over it.
- Both trees were set read-only afterwards. Builds ran in clones of them (`build-before`, `build-after`).
- `diff -rq before src` shows exactly the 2 Q121 paths. Hashes:
  - pre-images in `before/`: `c22f0cda…` and `0fb06a1d…`;
  - post-images in `src/`: `ea6fa362…` and `4aaec42c…`.
  These equal the Q121 README.
- The overlays were extracted into `stage/` and copied from there. The overlay archives themselves were not modified.

## Before → after (full table: `BEFORE-AFTER.tsv`; raw summaries: `BUILD-SUMMARY.tsv`, `TEST-SUMMARY.tsv`)

| Suite | before (BASE+Q117) | after (+Q121) | Expected | |
|---|---|---|---|---|
| Build: Platform Application.Tests | 0 E / 127 W | 0 E / 127 W | 0 E, W equal | ✓ |
| Build: Architecture | 0 / 0 | 0 / 0 | 0 E | ✓ |
| Build: SupplyChain.sln · Secrets · Web.Tests · Gateway.Tests | — | 0 E; W 0 · 0 · 18 · 5 | = Q119 | ✓ |
| **Platform Application.Tests** | **4333/4397** | **4333/4397** | identical | ✓ |
| └ the 3 touched facts (Ppm seed · Standalone 503 · Subscription standalone) | Passed ×3 | Passed ×3 | same outcome | ✓ |
| **Architecture** | **17/18** | **18/18** | 18/18 | ✓ |
| SupplyChain single process | — | **375/408** | 407/408 | **D1** |
| Gateway · Web · Secrets | — | 87/87 · 158/158 · 5/5 | 87 · 158 · 5 | ✓ |

### Platform: why the counts are identical and what the 64 failures are (`PLATFORM-BEFORE-AFTER.txt`, `PLATFORM-NAME-DIFF.txt`)

- The failing set is **identical** before and after: 64 = 64, with 0 only-before and 0 only-after.
- The 64 failures are pre-existing and unrelated to Q121:
  - 43 × `ObjectSerializer does not support BSON type 'Timestamp'`;
  - 10 × `Assert.True`;
  - 6 × `dropDatabase … currently being dropped (diten_platform_brd_it…)`;
  - 5 × string/value asserts.
- By class: BusinessReferenceData* 49, DocumentLifecycleStatus 6, DocumentTrainingMatrix 4, DocumentReleaseGate 4, CorporateCollectionInstanceFoundation 1.
- No Platform baseline existed before this WP (Q121 README step 4), so the "before" run *is* the baseline.
- The raw test-name sets differ in 38 names, 38 on each side. These are `BizCriticalAuditRejectionTests` theory cases whose display name embeds a per-run `Guid` (`Id = …`). With GUIDs normalised, the sets are equal.

### Architecture (`ARCHITECTURE-BEFORE-AFTER.txt`)

- **Before:** `MongoTestDatabaseGuardTests.NoTestCreatesItsOwnDatabasePerRun` failed on exactly `…/Audit/PpmAuditRetentionPolicySeedMongoTests.cs` and `…/Persistence/DisposableStandaloneMongo.cs`.
- **After:** it passes. `PerRunDatabaseExceptionListStaysHonest`, `SchemaBuildExceptionListStaysHonest`, `NoTestBuildsThePlatformSchema` and `TheScanActuallySeesTheTestTree` pass in both runs.

## Deviations

**D1: SupplyChain 375/408 instead of 407/408. Environmental; not caused by Q121.** (`SUPPLYCHAIN-VS-Q119.txt`)
- **Cause:** every Mongo test in `CapacityPlans/*.cs` hard-codes `mongodb://127.0.0.1:57192/?replicaSet=rsmod192`. This is not an env variable. The WP required a lane Mongo on a free port that is not Q122's, so the lane ran on **57212** (`rsq121b`), with all 6 env-configurable URIs pointing there.
- **Result:** 32 of the 39 CapacityPlans tests timed out (`TimeoutException … EndPoint "127.0.0.1:57192"`).
- **Evidence it is only that:**
  - Compared per test with Q119's `build-fix--supplychain-single.trx` (407/408), with GUIDs normalised, the test-name sets are equal.
  - The tests that passed in Q119 and failed here are 32, all CapacityPlans, all timeouts on 57192.
  - Nothing that failed in Q119 passes here.
  - The only common failure is the by-design `ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery`.
  - The SupplyChain source (350 files, excluding bin/obj) is **byte-identical** to Q119's `build-fix` tree. Q121 touches no SupplyChain file.
- **Re-run attempt (owner-approved, then abandoned):**
  - At ~14:52 I tried to start a lane `rsmod192` mongod on 57192. That port had been free at ~14:26.
  - Q122 had started its own mongod there at **14:49:43** (pid 91229, dbpath `~/mvp6-env/q122/test-mongo-192`).
  - My fork exited with code 48 (address in use); its empty dbpath is in `~/mvp6-env/q121b/test-mongo-192`.
  - One `rs.initiate` reached Q122's mongod and was **rejected** (`already initialized`), so it changed nothing. One read-only `rs.status` followed.
  - No re-run was made against Q122's instance.

**D2: Platform tests write to the shared 27017.**
- About 25 Platform test classes hard-code `localhost:27017`, for example `MongoIntegrationHarness`, the BusinessReferenceData* classes, Schema and Workflow. This cannot be redirected without a code change.
- The owner chose: full run, with snapshots of 27017 before and after.
- **Result:** 49 new DBs were created:
  - 43 × `diten_platform_brd_itest_{asn,gsku,pub}_<guid>`;
  - `diten_platform_itest`;
  - 5 × `diten_platform_itest_<fixed suffix>`.
- **Attribution:** none of them existed at the 14:26 snapshot. This lane was the only Platform runner between 14:34 and 14:43; Q122's dotnet (from 14:49) runs SupplyChain.
- **Clean-up:** with the owner's consent, exactly those 49 names were dropped (a guard allowed only `^diten_platform_(brd_)?itest`). 27017 now equals the 14:26 snapshot (50 DBs; `P2-*`, `P4-*`).
- **Observation for CT:** the `brd_itest_*_<guid>` pattern is a per-run database, and the architecture guard does not flag it. Presumably it is on the `KnownPerRunDatabase` exception list. It is not in Q121's scope.

## Residue and processes

- **Lane mongod 57212** (pid 87850):
  - its 9 test DBs were dropped (`P3-…`);
  - it was shut down, with 0 listeners and no `/tmp/mongodb-57212.sock` left;
  - its dbpath folder is kept in the workspace.
- **Test DBs:** 0 left on 57212; 0 added on 27017. `diten_platform_audit_seed` and `diten_platform_standalone` live only on the tests' own disposable mongods and are gone with them.
- **Temporary folders:** `TMPDIR` pointed at `~/mvp6-env/q121b/tmp/`. It holds 19 **empty** folders (16 GUID-named, plus `DitenStorageTests`, `DitenAttachmentTests` and `MSBuildTemp`) and **0 files**. They are left in place under the no-`rm` rule and are lane-local only.
- **Processes:** 0 processes carry `Q121B_LANE` or `mvp6-env/q121b`. The remaining mongods are the dev 27017 and Q122's instances, which were not touched.
- **No `rm`** was used anywhere.

## Repo unchanged (`V-repo.txt`, `V-overlays-after.txt`)

- HEAD `4a8d4d4b…`, no `index.lock`, 19 diff paths.
- `git diff` sha256 `95a5a1e0…` differs from Q119's `a705e73f…`, but no tracked file was modified during this lane: the newest tracked mtime is 14:13:19 (MOD-0187, another writer), and this lane started ~14:17. **Gap:** the diff hash was not captured at preflight; the claim rests on mtimes.
- New untracked items besides this folder come from other writers: `mvp6-q128-ver-q126-01/` (14:32) and `mvp6-q122-ver-claims-v3-01/` (14:58).
- BASE is 14,566/14,566 after the lane; the Q117 folder is 12/12 and the Q121 folder 3/3.

## Files

`README.md` · `BEFORE-AFTER.tsv` · `BUILD-SUMMARY.tsv` · `TEST-SUMMARY.tsv` · `PLATFORM-BEFORE-AFTER.txt` · `PLATFORM-NAME-DIFF.txt` ·
`ARCHITECTURE-BEFORE-AFTER.txt` · `SUPPLYCHAIN-VS-Q119.txt` · `P1-base-verify-before.txt` · `P5-base-verify-after.txt` ·
`P2-27017-dbs-before.txt` · `P2-27017-dbs-after.txt` · `P3-lane-57212-drop.txt` · `P4-27017-drop-list.txt` · `P4-27017-drop-result.txt` ·
`P4-27017-dbs-final.txt` · `V-repo.txt` · `V-overlays-after.txt` · `RAW-LOGS.tar.gz` (all build/test logs and TRX) ·
`TOOLS.tar.gz` (`build.sh`, `test.sh`, `test-rerun-57192.sh` (never run), `verify_base.py`, `trx.py`) · `SHA256SUMS`

No code fixes. No overlay modified.

## To do (for CT / owner)

1. **SupplyChain 407/408 on this exact tree:** re-run `build-after` when 57192 (`rsmod192`) is free again. Alternatively, make CapacityPlans read an env URI like the other modules do (a separate WP).
2. **Platform 64 pre-existing failures** (BSON `Timestamp` serializer ×43, BRD drop race ×6, and others): open a separate WP. They are not caused by Q121.
3. **Platform tests on 27017:** these ~25 classes should move to a lane-configurable URI so that future lanes do not write to the shared dev mongod.
4. Record CT disposition of Q121 (architecture 18/18 proven) → integration together with Q117.

Agent PASS ≠ CT ACCEPTED — returning to CT.
