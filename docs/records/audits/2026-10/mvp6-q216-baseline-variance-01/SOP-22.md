# Q216 — SupplyChain suite baseline variance (10 full runs) · SOP §22 structured report

| Field | Value |
|---|---|
| Work Package / Lane / Evidence | Q216 · `AL-SCM-BASELINE-VARIANCE` (INS) · required E2 — **reached: E2** (one build + 10 full suite runs, 4,170 test executions, 0 skipped, 0 timed out) |
| CT-QUEUE row | `docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv:312` |
| Placement | Claude app → Code tab → Local, `uname -s` = Darwin. **G2 placement waiver applied — Cowork withdrawn by owner.** |
| Agent | `devops-agent`. **G3 satisfied by analogy — §17.4 has no devops-agent row (F-8).** Mandatory fields supplied by CT in the prompt. |
| Base Stack (R9) | `docs/records/audits/2026-09/mvp6-base-stack-v2/BASE-STACK-v2.md` sha256 `ce8d60ab959ec7018c255ab1152983cfd8e301390f54b3d0ba23961bf2c4d882` — verified against its `SHA256SUMS` (2/2 OK). The runs were on the working tree, which holds the v2 layers since Q202a, not on a separately composed tree. |
| Read first | `AGENTS.md` and `devops-agent.md` (read in full earlier in this session; hashes re-checked, unchanged) · `dev-runbook.md` · `mongo-indexing.md` (DB-010) · `configuration-safety.md` · Q208 `PROVISIONING-STEPS.md` · Q214 `MECHANISM.md` |
| Session note | The prompt asks for its own session. This run shares a session with Q201, Q214, Q210 and Q212. |
| Start / End (Europe/Istanbul) | 2026-10-02 21:00:40 +03 / see `ARTIFACTS.sha256` header |

```text
Agent Verdict:        VARIANCE MEASURED. Agent PASS ≠ CT ACCEPTED.
Branch / HEAD:        feature/mvp6-logistics @ 4a8d4d4b339528a88e6220fb8402e5a2c771136c (matched)
Worktree status:      89 " M" · 576 "??" · 0 staged = 665 at start and at end (see ARTIFACTS.sha256).
                      No .git/index.lock. No git diff. No git write. No gh.
Other build in flight: none at start, and checked before every run (idle MSBuild worker nodes only)
Changed files:        this record folder + /private/tmp/q216-baseline-variance-01/ + git-ignored bin/ obj/
Build:                SupplyChain test project, once, before run 1: 0 errors, 0 warnings. No build between runs.
Tests:                10 full runs. 417 executed in every run. 3 runs: 416 passed. 7 runs: 415 passed.
Caps:                 per test --blame-hang-timeout 180 s; per run 20 min. Neither was hit. 0 TIMEOUT rows.
Persistence evidence: lane replica set rsq208s1 on 127.0.0.1:31994, PRIMARY before and after
Decisions:            none taken
Blockers:             none
Out-of-scope changes: none. No test, service, .csproj, appsettings or repo script was edited.
```

## 1. Distribution of the total (`RUN-TOTALS.tsv`)

| Passed / 417 | Runs | Which |
|---|---:|---|
| **416** (the Q208 figure) | **3** | 2, 7, 8 |
| **415** | **7** | 1, 3, 4, 5, 6, 9, 10 |
| anything else | 0 | — |

`dotnet test` exit code: 1 in 10 of 10. Wall time per run: 234–273 s.

## 2. Tests that were not green in all 10 runs (`VARIANCE.tsv`, 2 rows)

| Test | Passes | Failures | Timeouts | Assertion when it failed |
|---|---:|---:|---:|---|
| `Claims.ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery` | 0 | 10 | 0 | `InvalidOperationException: Explicit write/read restart mode required; exclude from ordinary suite.` — `Claims/ClaimReplayTests.cs:46` |
| `Loads.LoadAtomicityTests.UnknownCommitResultRetriesAndCommitsExactlyOnce` | **3** | **7** | 0 | `Assert.Equal() Failure — Expected: Created, Actual: ServiceUnavailable` — `Loads/LoadAtomicityTests.cs:37` |

Per-run sequence of the Loads test: `F P F F F F P P F F`.

The other **415 tests were green 10 of 10**.

## 3. Known wrong-reason greens vs anything new

### 3.1 The tests Q214 named — all confirmed, with one refinement

Server-side counts come from the lane mongod's log, matched to each test by time
(`evidence/failpoint-episodes-by-test.tsv`, 50 rows = 5 server-global episodes × 10 runs).

| Test | Armed | Fired, over 10 runs | Result | Q214 said | Q216 adds |
|---|---|---|---|---|---|
| Loads `UnknownCommitResultRetriesAndCommitsExactlyOnce` | times: 1, code 91 | 1 in 10/10 | 3 pass / 7 fail | passes in a full run, fails alone | **It fails in most full runs too: 7 of 10.** The Q208 green was the minority outcome. |
| Loads `UnresolvedUnknownCommitReturns503…` | times: 3, code 91 | **1 in 10/10** | 10 pass | fires once | confirmed; 9.8–9.9 s every run |
| Returns `UnknownCommit_Unresolved503_AllOrNoneThenSameKeyRecovery` | times: 5, code 91 | **1 in 7 runs, 2 in 3 runs** | 10 pass | fires once per run | **Refinement: not always once. It fired twice in runs 2, 3 and 10.** Still never 3, 4 or 5 — retry exhaustion remains untested. 63–90 s per run. |
| S&OP `Unknown_commit_without_receipt…` | times: 3, code 91 | **1 in 10/10** | 10 pass | weak green | confirmed; 10.0 s every run |
| S&OP `Committed_write_concern_uncertainty…` | times: 1, write-concern error 91 | 1 in 10/10 | 10 pass | accepts 201 or 503 | Duration ranges from **0.2 s to 9.7 s**: two different paths are being taken, and both are green |
| Capacity `CapacityFaultTests` | appName-scoped, code 8 / WCE 64 | 120 episodes, each fired once | all pass | correct pattern | confirmed |

### 3.2 New flips — none (`NEW-FLIPS.tsv` is empty)

**No test flipped that Q214 did not name.** The only test whose outcome changed between runs is the Loads test above.
The Claims test is not a flip: it fails every time, by design.

What *is* new information, though not a new flip, is in the last column of §3.1: the Loads pass rate (3/10) and the
Returns fired count (sometimes 2).

## 4. Why the Loads test lands where it does

- xUnit ran the test classes in a different order in every run: the Loads test finished at +10 s, +80 s, +149 s,
  +120 s, +110 s, +10 s, +180 s, +210 s, +130 s and +230 s into the ten runs.
- So the moment of the fault relative to the driver's 10-second monitor cycle is effectively random per run.
- If a random moment decided it alone, about half the runs would pass. Three of ten did. With ten runs that is not
  distinguishable from one half; the claim here is only the measured 3/10, not a rate.

## 5. Gate question → `CI-GATE-RECOMMENDATION.md`

**Not as it stands; yes with a named two-test exclusion.**

- Exit code: red 10/10. "= 416": red 7/10. "≥ 415": green 10/10 but unsafe, because a count hides which test failed.
- Recommended rule: exclude the Claims two-process test and the Loads flip **by name**, require exit code 0. The
  remaining 415 were green in 10 of 10.
- The filtered command was not run here. Ten clean runs bound a hidden flake rate at about 26% per run, not at zero.
- Four tests stay green in the gate without being evidence for unknown-commit handling. That needs Q215.

## 6. Findings

| ID | Severity | Finding | Evidence |
|---|---|---|---|
| **F-Q216-1** | 🟠 High | The baseline is bimodal: 416 in 3 runs, 415 in 7. "416/417" is the less common outcome. | `RUN-TOTALS.tsv` |
| **F-Q216-2** | 🟠 High | One test accounts for all of the variance: `Loads…UnknownCommitResultRetriesAndCommitsExactlyOnce`, 3 pass / 7 fail, always the same assertion at `LoadAtomicityTests.cs:37`. | `VARIANCE.tsv` |
| **F-Q216-3** | 🟢 Result | No new flip. 415 tests were green in 10 of 10 runs; 0 timeouts, 0 hangs, 0 skipped. | `VARIANCE.tsv`; `NEW-FLIPS.tsv` (empty); `evidence/per-test-outcomes.json` |
| **F-Q216-4** | 🟠 High | The wrong-reason greens are stable, which is what makes them dangerous: Loads `times: 3` fired once in 10/10; S&OP `times: 3` once in 10/10; Returns `times: 5` once or twice, never more. They will stay green in any gate. | `evidence/failpoint-episodes-by-test.tsv` |
| **F-Q216-5** | 🟡 Medium | Correction to Q214: the Returns test does not always fire once. It fired twice in 3 of 10 runs. | same |
| **F-Q216-6** | 🟡 Medium | S&OP `Committed_write_concern_uncertainty…` ran between 0.2 s and 9.7 s. It takes two different paths and reports green for both. | same, column `test_seconds` |
| **F-Q216-7** | 🟡 Medium | An unfiltered `dotnet test` can never exit 0: the Claims two-process test fails by design (F-Q208-3, confirmed 10/10). | `VARIANCE.tsv` |
| **F-Q216-8** | 🟡 Medium | A count-based gate ("≥ 415") would have been green in all ten runs and would hide a real single-test regression whenever the Loads test passes. | `CI-GATE-RECOMMENDATION.md` |
| **F-Q216-9** | ⚪ Info | Test-class order changes from run to run. Any timing-dependent test will therefore show up as a flake rather than as a steady red or green. | §4 |
| **F-Q216-10** | ⚪ Info | Two theory rows in `Claims.ClaimReferenceTests.Reader_OtherProducer5xx_RemainReferenceUnavailable` have the same display name in the result file (the data is truncated). 417 executions, 416 distinct names. Both rows passed in every run. | `evidence/per-test-outcomes.json` (the entry marked `#2`) |
| **F-Q216-11** | ⚪ Info | Free disk at preflight was 8.2 GiB, not the 9.1 GiB at dispatch. This WP used 20 MB. | `DISK-LOG.md` |
| **F-Q216-12** | ⚪ Info | During the health check before run 1, I sent `configureFailPoint … mode: off` once to the lane mongod to read its counter. It was already off; this changed no state the tests depend on. Stated for completeness. | preflight |

## 7. Environment

| Item | Value |
|---|---|
| Replica set | **reused** `rsq208s1`, single member `127.0.0.1:31994`, mongod 8.0.18, pid 2363, `enableTestCommands=1`. Healthy before (PRIMARY) and after (PRIMARY, uptime 7,465 s at the end). Not restarted. |
| Accepted script | `scripts/test-env/mvp6-test-mongo-env.sh`, mode `-rwxr-xr-x`, sha256 `6c936b4a84bb…54d13b1` ✔. Used only to print the variables. |
| Variables | The seven lane variables, byte-identical to Q208's `env-seven.sh`: all `mongodb://127.0.0.1:31994/?replicaSet=rsq208s1&serverSelectionTimeoutMS=5000`. Names in `evidence/env-seven-names.txt`. `RETURNS_MONGO_URI` set. |
| Command per run | `dotnet test <project> --no-build --blame-hang --blame-hang-timeout 180s --logger trx --results-directory <scratch>` |
| Binaries | `Diten.SupplyChainService.Tests.dll` sha256 `abd142fd9bf5…`; `Diten.SupplyChainService.Api.dll` `5d6068706a3b…` — the same for all ten runs |
| 27017 | Not contacted. pid 825 and its two listeners unchanged. No variable points at 27017–27021. |
| Scratch | `/private/tmp/q216-baseline-variance-01/` (20 MB). The ten `.trx` files stay there; their hashes are in `evidence/trx-sha256.txt`. |
| Left behind | The lane mongod is **still running**, as Q208 left it. Stop: `mongosh --quiet --port 31994 --eval 'db.adminCommand({shutdown:1})'`. |

## 8. Known gaps

- Ten runs. Enough to show the total is not stable and to name the test; not enough to state a pass rate.
- The filtered gate command was not run.
- All runs used one mongod that had already served Q208. A fresh set per run was not tried.
- Runs were on the working tree, not on a composed BASE-STACK v2 tree.
- The failpoint-to-test matching is by time order within each run (five episodes, five arming tests); the mongod
  log at default verbosity does not name the database of a failed command.

## 9. Refused / not done

- No test, guard or fail point was edited, and no flip was "fixed". Nothing from the recommendation was implemented.
- No variable at 27017; the homebrew mongod was not contacted or reconfigured.
- No git write, no `git diff`, no `gh`. No ledger row changed.

## 10. Files

`SOP-22.md` · `VARIANCE.tsv` · `RUN-TOTALS.tsv` · `NEW-FLIPS.tsv` (empty: no new flip) · `CI-GATE-RECOMMENDATION.md` ·
`DISK-LOG.md` · `ARTIFACTS.sha256` · `evidence/` (`runs.tsv`, 10 run logs, `build.log`, `per-test-outcomes.json`,
`episodes.tsv`, `failpoint-episodes-by-test.tsv`, `trx-sha256.txt`, `env-seven-names.txt`) · `tools/` (`run10.py`,
`analyze.py`, `episodes.py`, `join.py`)

Return to CT; CT decides.
