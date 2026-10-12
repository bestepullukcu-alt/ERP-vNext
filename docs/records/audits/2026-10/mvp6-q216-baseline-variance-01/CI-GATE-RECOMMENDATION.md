# Q216 — Can CI gate on the SupplyChain suite today?

A recommendation only. Nothing here was implemented.

## Answer

**Not as it stands. Yes, with a named two-test exclusion — and with one limit on what the gate means.**

## Why not as it stands

Measured over 10 unfiltered full runs (`RUN-TOTALS.tsv`):

| Possible gate rule | What the 10 runs say | Verdict |
|---|---|---|
| "`dotnet test` exits 0" | Exit code 1 in **10 of 10** runs. `ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery` fails every time by design (`ClaimReplayTests.cs:46`: "exclude from ordinary suite"). | Unusable: always red |
| "passed = 416" (the Q208 number) | True in **3 of 10** runs. | Unusable: red 70% of the time with no code change |
| "passed ≥ 415" | True in 10 of 10. | **Unsafe.** When the Loads test happens to pass, a real regression in any other test still gives 415 and the gate stays green. A count cannot tell which test failed. |

So neither the exit code nor a count is a gate today.

## What works: gate on names, not on a number

**Rule:** run the suite with two tests excluded by name, and require exit code 0.

```text
dotnet test services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests \
  --filter "FullyQualifiedName!~ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery&FullyQualifiedName!~LoadAtomicityTests.UnknownCommitResultRetriesAndCommitsExactlyOnce"
```

| Excluded test | Why | When it comes back |
|---|---|---|
| `Claims.ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery` | 0/10. Not a flake: it needs an outer two-process driver and says so itself. | When that driver is the thing CI runs for it (a separate job), or permanently excluded from the ordinary suite |
| `Loads.LoadAtomicityTests.UnknownCommitResultRetriesAndCommitsExactlyOnce` | 3/10. The Q214 timing race; its result carries no information either way. | After Q215 changes the injected fault, and a repeat of this WP shows it stable |

Evidence for the rule: the other **415 tests were green in 10 of 10 runs** (4,150 executions, 0 failures, 0 timeouts).

Three cautions:

1. **The filtered command itself was not run.** This WP ran the unfiltered suite ten times. The 415 are stable *inside* an
   unfiltered run. Run the filtered form a few times before turning the gate on.
2. **Ten runs bound the risk; they do not remove it.** Zero flips in 10 runs is consistent with a true per-run flake
   rate of up to about 26% (95% confidence). Keep counting for the first weeks: log every red gate run with the failing
   test name.
3. **Each lane needs its own replica set.** The fail point is server-global (Q214 F-Q214-6). Two CI jobs sharing one
   mongod could fail each other's commits. One mongod per job, the seven lane variables set, `RETURNS_MONGO_URI`
   included.

## The limit on what a green gate means

Four tests stay in the gate and stay green, and their green is **not evidence** for the behaviour in their names:

| Test | Armed | Fired, over 10 runs | What the green does not show |
|---|---|---|---|
| Loads `UnresolvedUnknownCommitReturns503WithoutPretendingRollbackOrSuccess` | times: 3 | 1 in 10/10 | Three consecutive unknown commit results |
| Returns `UnknownCommit_Unresolved503_AllOrNoneThenSameKeyRecovery` | times: 5 | 1 in 7/10, 2 in 3/10 | Retry exhaustion (never more than 2 of 5) |
| S&OP `Unknown_commit_without_receipt_returns_unresolved_then_exact_key_recovers` | times: 3 | 1 in 10/10 | More than one unknown result; it asserts only "at least 1" |
| S&OP `Committed_write_concern_uncertainty_resolves_original_receipt` | times: 1 | 1 in 10/10 | Which outcome happened: it accepts 201 or 503, and took between 0.2 s and 9.7 s |

They are useful as tripwires — if one goes red, something changed — but a CI report must not cite them as proof that
unknown-commit handling works. That stays open until Q215.

## What must change before the exclusions can be dropped

1. **Q215** (owner authorization needed): inject a non-state-change error, scope the fail point by `appName`, assert
   the fired count — the pattern Capacity already uses. Capacity's 120 scoped episodes in these ten runs all behaved
   as armed.
2. Re-run this variance measurement after Q215. The exclusion of the Loads test ends when it is green N of N.
3. Decide where the Claims two-process test runs. It cannot pass in an ordinary run by construction.

## Cost note

A full run takes 234–273 s. About 60–90 s of that is the Returns unknown-commit test alone (63–90 s per run), and
about 10 s each for three more error-91 tests. Q215 would shorten the gate as well as steady it.
