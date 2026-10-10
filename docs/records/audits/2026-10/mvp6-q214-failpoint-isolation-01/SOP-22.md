# Q214 — Loads failpoint isolation · SOP §22 structured report

| Field | Value |
|---|---|
| Work Package / Lane / Evidence | Q214 · `AL-SCM-FAILPOINT-ISOLATION` (INS) · required E2 — **reached: E2** (build + 8 test runs + server-side trace + driver-level reproduction) |
| CT-QUEUE row | `docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv:310` |
| Placement | Claude app → Code tab → Local, `uname -s` = Darwin. **G2 placement waiver applied — Cowork withdrawn by owner.** |
| Agents | `devops-agent` (lane mongod, build, runs) → `testing-agent` (analysis). **G3 satisfied by analogy — §17.4 has no devops-agent row (F-8).** testing-agent §17.4 fields as given in the prompt. |
| Read first | `AGENTS.md` (full) · `devops-agent.md` · `testing-agent.md` · `docs-organization.md` · Q208 `SOP-22.md`, `PROVISIONING-STEPS.md`, `ENV-SURFACE.tsv` · MOD-0185 pack §atomicity |
| Start / End (Europe/Istanbul) | 2026-10-02 19:57:03 +03 / see `ARTIFACTS.sha256` header |

```text
Agent Verdict:        MECHANISM FOUND AND REPRODUCED. Agent PASS ≠ CT ACCEPTED.
Branch / HEAD:        feature/mvp6-logistics @ 4a8d4d4b339528a88e6220fb8402e5a2c771136c (matched)
Worktree status:      start 89 " M" · 576 "??" · 0 staged = 665 — equal to the Q208 end state.
                      (The prompt's "32 + 533" is the pre-Q202a baseline; see F-Q214-0.)
                      end: see ARTIFACTS.sha256 header. No .git/index.lock. No git diff. No git write. No gh.
Other build in flight: none (idle MSBuild worker nodes only)
Changed files:        this record folder + the session scratchpad + git-ignored bin/ obj/
Build:                SupplyChain test project: 0 errors, 0 warnings (evidence/build.log)
Tests:                8 runs (FLIP-REPRODUCTION.tsv). Full suite once: 417 total · 416 passed · 1 failed
                      (ClaimReplayTests…DurableRecovery, by design — same as the Q208 baseline)
Failure paths:        MECHANISM.md §3 (LoadRepository.cs:75 → :88 → :90 → :91)
Persistence evidence: lane mongod rsq214s2 on 127.0.0.1:32994, every command logged; 27 fail-point episodes
Decisions:            none taken
Blockers:             none
Known gaps:           §5
Out-of-scope changes: none. No test or service code edited. Nothing implemented from RECOMMENDATION.md.
```

## 1. Answers to the five tasks

1. **Why the outcome flips.** The test injects error 91 (`ShutdownInProgress`). The driver then treats its only
   server as gone until the monitor's pending 10-second `hello` returns. The product's commit retry needs server
   selection, capped at 5 s by the lane URI. More than 5 s left in the cycle → 503; less → 201. Alone, the test
   always faults about 1.3 s after the cluster opens, so it is always red. In a longer run the fault lands
   wherever the preceding tests' durations put it. → `MECHANISM.md`
2. **Which result is true.** Neither says anything about the product; both are correct for their timing. The
   isolated 503 is what the test, as written, must produce under the lane URI. **The full-run green is a
   coincidental green:** the assertion was honestly met in that run (one retry, exactly one commit), but only
   because the fault landed 2.7 s before the cycle boundary. It is not evidence and must not be counted.
3. **Classification: test design.** `Loads/LoadAtomicityTests.cs:19` (error code 91) and `:14-22` (unscoped fail
   point), together with `scripts/test-env/mvp6-test-mongo-env.sh:57` (5 s selection timeout) on a one-member set.
   The product's 503 satisfies the pack (`MOD-0185-routing-load-planning.md:268`, `:402`): receipt lookup, no blind
   insert, no rollback claim. **Not a product defect by the pack.** One product observation is recorded:
   `LoadRepository.cs:75` does not retry on `TimeoutException`.
4. **Blast radius.** → `BLAST-RADIUS.tsv`, 8 rows. Cross-module consumption of the shared fail point: **not
   observed** (27 episodes, every hit in the arming module's own database). But the same error-91 mechanism makes
   two tests green for the wrong reason and two more unable to tell:

   | Test | Verdict |
   |---|---|
   | Loads `UnresolvedUnknownCommitReturns503…` (`times: 3`) | **Green for the wrong reason** — fired 1 of 3 in 3/3 runs |
   | Returns `UnknownCommit_Unresolved503_AllOrNoneThenSameKeyRecovery` (`times: 5`) | **Green for the wrong reason** (503 half) — fired 1 of 5 |
   | S&OP `Unknown_commit_without_receipt_returns_unresolved…` (`times: 3`) | Weak green — fired 1 of 3; asserts only "≥ 1" |
   | S&OP `Committed_write_concern_uncertainty…` | Cannot distinguish — accepts 201 or 503 |
   | S&OP `Receipt_insert_failure…` | Right reason (namespace-scoped, code 2) |
   | Capacity `CapacityFaultTests` X01 / X06 / X07 | Right reason (appName-scoped, code 8 / WCE 64) |

5. **Recommendation.** Use Capacity's pattern in the other three classes: a non-state-change code, `appName`
   scope, and an assertion on the fired count. → `RECOMMENDATION.md`. Not implemented.

## 2. Findings

| ID | Severity | Finding | Evidence |
|---|---|---|---|
| **F-Q214-0** | ⚪ Low | The shared preamble's baseline (32 modified + 533 untracked) is stale. Measured at start: 89 + 576 = 665, identical to the Q208 end state after Q202a. Treated as the known state; no STOP. | `mvp6-q208-suite-baseline-01/SOP-22.md` "Worktree status" |
| **F-Q214-1** | 🟠 High | The flip is a timing race between the driver's 10 s monitor cycle and the lane's 5 s server-selection timeout, triggered by injecting error 91. Reproduced with and without product code. | `MECHANISM.md`; `FLIP-REPRODUCTION.tsv`; `evidence/harness.tsv` |
| **F-Q214-2** | 🟠 High | The full-run green of `UnknownCommitResultRetriesAndCommitsExactlyOnce` is coincidental. The Q208 baseline "Loads 33/33" and the total "416/417" can each drop by one with no code change. | `FLIP-REPRODUCTION.tsv` (Q214, lane default URI: 2 green / 3 red; Q208: 1 green / 5 red) |
| **F-Q214-3** | 🟠 High | Two tests are green for the wrong reason, in every run: Loads `times: 3` and Returns `times: 5` fire **once**. Retry exhaustion is not tested anywhere in Loads or Returns. | `BLAST-RADIUS.tsv:3-4`; `evidence/failpoint-episodes.tsv:8,10,26,28` |
| **F-Q214-4** | 🟡 Medium | The Q208 measurement "Loads folder alone → FAILS" is not stable: the same filter passed here (33/33), with 3.2 s of the cycle left. Folder scope does not determine the result. | `FLIP-REPRODUCTION.tsv` r06 vs Q208-folder |
| **F-Q214-5** | 🟡 Medium | Product observation, not a pack defect: the commit loops retry only labelled `MongoException`s; a `TimeoutException` during re-selection ends the retry. Same shape in four repositories. | `LoadRepository.cs:75`; `ReturnRepository.cs:139-140`; `ClaimRepository.cs:139-140`; `CarrierRepository.cs:89-90` |
| **F-Q214-6** | 🟡 Medium | The fail point is server-global in Loads, Returns and S&OP, and each class's "off" is global too. No cross-module hit was observed, because the assembly is serial. The exposure is latent: parallel classes, a child process, a second lane on the same mongod, or a killed test that leaves it armed. | `BLAST-RADIUS.tsv:9`; `LoadAtomicityTests.cs:26-27` |
| **F-Q214-7** | ⚪ Info | About 80 s of the 250 s suite is waiting caused by error 91: Returns 70.1 s, S&OP 15.9 s, Loads 12.7 s. | `evidence/r08-full-outcomes.tsv` |
| **F-Q214-8** | ⚪ Info | The falsified Q208 lead is confirmed falsified by measurement as well as by `LoadContractTests.cs:29`. The three candidate directions in the prompt are each ruled out with evidence. | `MECHANISM.md` §4 |
| **F-Q214-9** | ⚪ Info | S&OP's test already contains a workaround for this mechanism (`killAllSessions`, then a new client with `DirectConnection=true`). Its author met the unknown-server state and went around it. | `SandopAtomicityTests.cs:15, :60-62` |

## 3. Environment (devops-agent)

| Item | Value |
|---|---|
| Lane mongod | `/opt/homebrew/bin/mongod` 8.0.18, **own instance** for this WP: `127.0.0.1:32994` (slot 2), single-member set `rsq214s2`, `enableTestCommands=1`, `--slowms 0` (logs every command), `--nounixsocket`. pid 3865. |
| Variables | The seven lane variables from `scripts/test-env/mvp6-test-mongo-env.sh --slot 2 --rs rsq214s2 --all-supplychain`. Three diagnostic runs changed only the URI options (`FLIP-REPRODUCTION.tsv`, column `mongo_uri_options`). |
| Scratch | the session scratchpad, folder `q214/` (`db/`, `logs/`, `results/`, `harness-bin/`). Not in the repo. |
| 27017 | Not contacted. pid 825 and its two listeners unchanged. No variable pointed at 27017–27021. |
| Q208 lane (31994, pid 2363) | Not used, not touched; still running. |
| End state | The Q214 mongod was **shut down** by this lane (`shutdownServer`). Its dbpath and the 40 MB log stay in the scratchpad; nothing was deleted. |

## 4. How the observation was done without editing code

- `tools/run.sh` — `dotnet test --no-build` with a filter and a URI-option override.
- `tools/failpoints.py`, `tools/mlog.py` — read the lane mongod's log; nothing is sent to the server.
- `tools/harness/` — a separate console program (`Program.cs.txt`, `harness.csproj.txt`; stored as `.txt` so that no
  build or guard picks them up). Built and run in the scratchpad only. It contains no product or test code.

## 5. Known gaps

- The driver's internals were not read; the "pending 10 s `hello`" explanation rests on the server log
  (`maxAwaitTimeMS: 10000` on the monitor connection) and on the harness, which shows the cluster state as
  `Unknown/Disconnected` until 10.1 s.
- One full-suite run and one Loads-folder run. The pass rate inside a full run was not estimated; the claim is that
  it depends on timing, not that it has a given probability.
- Which branch (201 or 503) S&OP's write-concern test took: insufficient evidence.
- Not measured on a three-member replica set. There, another member could be selected; the mechanism was
  established for the single-member lane that Q205 / Q208 define.
- The full `r08-full.trx` (2.1 MB) and the full mongod log (40 MB) are not in the record (K7). Their sha256:
  `08a25ecd86a4fd0c52571cc1644857baa93cb3a3f5db3791b1eeda107554f532`,
  `7ed73d4d3993e0c3f28e1806fe6538dd3cc7b24d759d0c6c7b2ba35c56dc41a8`. Extracts are in `evidence/`.

## 6. Refused / not done

- No test or service code edited; no guard weakened; nothing from `RECOMMENDATION.md` implemented.
- No variable at 27017; the homebrew mongod not contacted or reconfigured; the Q208 mongod not touched.
- No git write, no `git diff`, no `gh`. No ledger row changed.

## 7. Files

`SOP-22.md` · `FLIP-REPRODUCTION.tsv` · `MECHANISM.md` · `BLAST-RADIUS.tsv` · `RECOMMENDATION.md` · `ARTIFACTS.sha256`
· `evidence/` (`runs.tsv`, `failpoint-episodes.tsv`, `harness.tsv`, `r08-full-outcomes.tsv`, 8 run logs, 2 `.trx`,
`build.log`, `mongod-failpoint-and-commit-lines.log.gz`) · `tools/` (`run.sh`, `failpoints.py`, `mlog.py`, `harness/`)

Return to CT; CT decides.
