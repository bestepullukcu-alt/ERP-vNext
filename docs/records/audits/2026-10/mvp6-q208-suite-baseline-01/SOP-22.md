# Q208 — New SupplyChain suite baseline (seven folders) · SOP §22 structured report

| Field | Value |
|---|---|
| Work Package / Lane / Evidence | Q208 · `AL-SCM-TESTENV-FULL` (INS) · required E2 — **reached: E2** (build + one full suite run, 417/417 executed, 0 skipped, 0 timed out) |
| Placement | Claude app → Code tab → Local, `uname -s` = Darwin. **G2 placement waiver applied — Cowork withdrawn by owner.** |
| Agent | `devops-agent`. **G3 satisfied by analogy — §17.4 has no devops-agent row (F-8).** Mandatory fields supplied by CT in the prompt. |
| Read first | `AGENTS.md` (full, post-Q202a version) · `devops-agent.md` · `dev-runbook.md` · `mongo-indexing.md` (DB-010) · `configuration-safety.md` · Q205 `PROVISIONING-STEPS.md` · Q202a `SOP-22.md` |
| Start / End (Europe/Istanbul) | 2026-10-02 19:39:19 +03 / see `ARTIFACTS.sha256` header |

```text
Agent Verdict:        NEW BASELINE PUBLISHED. Agent PASS ≠ CT ACCEPTED.
Branch / HEAD:        feature/mvp6-logistics @ 4a8d4d4b339528a88e6220fb8402e5a2c771136c (matched)
Worktree status:      start 89 " M" · 576 "??" · 0 staged = 665
                      end   89 " M" · 576 "??" · 0 staged = 665
                      (this folder is inside the already-untracked docs/records/audits/2026-10/ line)
                      No .git/index.lock. No git diff. No git write. No gh.
Other build in flight: none at start (no dotnet/msbuild/vstest/testhost process)
Changed files:        this record folder + /private/tmp/q208-suite-baseline-01/ + git-ignored bin/ obj/
Build:                SupplyChain test project (incl. the service): 0 errors, 0 warnings
                      Platform Application.Tests / BackgroundJobs.Tests / Eventing.Tests: 0 errors (129 / 3 / 2 warnings)
Tests:                417 total · 416 passed · 1 failed · 0 skipped · 0 timed out   (4 min 22 s)
Persistence evidence: rsq208s1 PRIMARY on 127.0.0.1:31994; transaction commit + abort proven
Blockers:             none
Out-of-scope changes: none
```

## 1. New baseline (`NEW-BASELINE.tsv`)

| Folder | Module | Total | Passed | Failed | Skipped | Timed out |
|---|---|---:|---:|---:|---:|---:|
| Shipments (root files + `Shipments/`) | MOD-0183 | 74 | 74 | 0 | 0 | 0 |
| Carriers | MOD-0184 | 36 | 36 | 0 | 0 | 0 |
| Loads | MOD-0185 | 33 | 33 | 0 | 0 | 0 |
| Returns | MOD-0186 | 78 | 78 | 0 | 0 | 0 |
| Claims | MOD-0187 | 129 | 128 | 1 | 0 | 0 |
| SandopPlans | MOD-0190 | 19 | 19 | 0 | 0 | 0 |
| CapacityPlans | MOD-0192 | 48 | 48 | 0 | 0 | 0 |
| **Total** | | **417** | **416** | **1** | **0** | **0** |

Counts are executed test cases (a `[Theory]` counts once per data row), so they are higher than the attribute
counts in the prompt (Claims 51 → 129, Returns 43 → 78, CapacityPlans 27 → 48, SandopPlans 19 → 19).

## 2. Failures (`FAILURE-CLASSIFICATION.tsv`)

| Test | Class | Why |
|---|---|---|
| `Claims.ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery` | **ENV** — `CLAIMS_RESTART_MODE` | The test throws "Explicit write/read restart mode required; exclude from ordinary suite" unless an outer driver sets `CLAIMS_RESTART_MODE=write\|read` (`ClaimReplayTests.cs:46`). By design it cannot pass in an ordinary run. Same single failure in Q103 (128/129). |
| `Loads.LoadAtomicityTests.UnknownCommitResultRetriesAndCommitsExactlyOnce` | **UNKNOWN** — own row | **Passed** in the baseline run. Fails when the Loads folder runs alone (1/1) and when the test runs alone (4/4): expected `Created`, actual `ServiceUnavailable` (`LoadAtomicityTests.cs:37`). See F-Q208-2. |

PRODUCT: 0 rows. TIMEOUT: 0 rows.

## 3. Findings

| ID | Severity | Finding | Evidence |
|---|---|---|---|
| **F-Q208-1** | 🟢 Result | The post-Q202a tree **compiles** and the seven-folder suite stands at **416 / 417**. This closes F-Q202a-1 and F-Q202a-4 for SupplyChain: the stack files plus the working-tree `Program.cs` build and run. | `evidence/full.trx`, `evidence/build-supplychain.log` |
| **F-Q208-2** | 🟠 High | **The Loads survivor's result depends on what runs beside it — on the same mongod and the same build.** Full run: passed. Loads folder alone: failed. Test alone: failed 4/4. So the "single-member vs 3-node" question does not explain it here, and its green result in the baseline is **not** proof that it is fixed. It may be a real commit-retry defect that the full run masks, or a test-isolation defect. Unverified lead: the test arms a server-wide `failCommand` fail point (`times: 1`) that a parallel test class on the same mongod can consume. Not diagnosed further — test and service code are protected. | `evidence/loads-isolated.log`, `evidence/loads-folder.log`, `full.trx` |
| **F-Q208-3** | 🟡 Medium | The baseline's one red test is permanent in an unfiltered run: `ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery` self-declares "exclude from ordinary suite". An unfiltered `dotnet test` therefore always exits 1. Any gate that reads the exit code needs a filter or an agreed "416/417" rule. | `ClaimReplayTests.cs:45-46`; Q103 `README.md:74` |
| **F-Q208-4** | 🟡 Medium | `RETURNS_MONGO_URI` is the only lane variable that does **not** fail closed. Unset, it falls back to `mongodb://127.0.0.1:27886/?replicaSet=returns_dev` with no `serverSelectionTimeoutMS` (`ReturnAtomicityTests.cs:51`). The other six throw at once. This is the hang CT saw; with the variable set, Returns is 78/78 in 64 s. Not fixed here (test code is protected). | `ENV-SURFACE.tsv` |
| **F-Q208-5** | 🟡 Medium | `MVP6_MOD0190_MONGO_URI` is read with the null-forgiving operator in two places (`SandopReplayTests.cs:15`, `SandopAtomicityTests.cs:15`). They rely on `SandopContractTests.cs:5` having thrown first. Unset, the folder fails fast, but through a less clear path. | `ENV-SURFACE.tsv` |
| **F-Q208-6** | ⚪ Info | Full environment surface: **7 lane variables**, all Mongo URIs, all printed by the accepted script with `--all-supplychain`. 7 more names are internal child-process switches or an optional evidence path; the lane must not set them. One replica set serves all seven folders. | `ENV-SURFACE.tsv` (16 rows) |
| **F-Q208-7** | ⚪ Info | The Q205 lane did not survive: the machine was restarted between Q205 and Q208, as F-Q205-4 predicted. A new set `rsq208s1` was started on the same port. It is **left running** (pid 2363). | `PROVISIONING-STEPS.md` §1, §8 |
| **F-Q208-8** | 🟡 Medium | Free disk fell from 14 GiB to 9.1 GiB during the WP; this WP's own footprint is under 0.6 GiB. The rest is unexplained. | `DISK-BEFORE-AFTER.md` |
| **F-Q208-9** | ⚪ Info | Platform: the three test projects Q131 touched **build** (0 errors). They were not run — Platform tests are outside this WP and need `DITEN_PLATFORM_TEST_MONGO_URI`. | `evidence/build-Diten.Platform.*.log` |

## 4. Things left as they are (per the prompt)

- `SandopAtomicityTests.cs:31` — deliberately unreachable `127.0.0.1:57191`, 150 ms. Port 57191 had 0 listeners. Sandop 19/19.
- `CapacityTestMongoTests.cs` — `31994` inside `[InlineData]` is a parse fixture. Not a leak.

## 5. Refused / not done

- No test code, service code, `.csproj`, appsettings or repo script was edited. No variable points at 27017.
- The homebrew mongod (pid 825) was not contacted or reconfigured.
- The Loads test was not diagnosed inside product code; the Claims restart test was not driven with its mode.
- Platform tests were built, not run.
- Beyond "run the full suite once": the Loads test was re-run alone 4 times and the Loads folder once, to
  characterise F-Q208-2. These reruns are not part of the baseline numbers.

## 6. Files

`SOP-22.md` · `ENV-SURFACE.tsv` · `NEW-BASELINE.tsv` · `FAILURE-CLASSIFICATION.tsv` · `PROVISIONING-STEPS.md` ·
`DISK-BEFORE-AFTER.md` · `ARTIFACTS.sha256` · `evidence/` (`full.trx`, `test-full.log`, `run-times.txt`,
`build-supplychain.log`, 3 × `build-Diten.Platform.*.log`, `loads-isolated.log`, `loads-folder.log`,
`env-seven-names.txt`)

Return to CT; CT decides.
