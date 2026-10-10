# Q214 — Recommendation (not implemented)

Nothing below was applied. Editing tests needs separate authorization; editing product code needs its own WP.

## Recommended: change the fault, scope it, and count it — in the three exposed classes

One pattern already exists in the repo and is immune: `CapacityPlans/CapacityAtomicityTests.cs:108-127`
(error 8, `appName` scope, hit count read back). Bring the other three classes to it.

| # | Change | Where | Why | Evidence that it works |
|---|---|---|---|---|
| R1 | Inject a code that is **not** a state-change error — `8` with the `UnknownTransactionCommitResult` label, as Capacity does — instead of `91` | `Loads/LoadAtomicityTests.cs:19` · `Returns/ReturnAtomicityTests.cs:28` · `SandopPlans/SandopAtomicityTests.cs:55` (and the `writeConcernError` code 91 at `:72` → 64, as Capacity) | 91 makes the driver drop its only server for up to 10 s. That is the whole flip. | Harness H4: second commit OK in 0.011 s, server stays `Connected`. Capacity's 12 episodes: all sub-second. |
| R2 | Scope the fail point with `appName` (per test or per class) and give the client under test that application name | same three classes | Removes the server-global blast radius: another class, a child process or a second lane on the same mongod cannot be hit, and cannot switch it off. | Capacity: `data.appName` at `CapacityAtomicityTests.cs:110`; the harness uses the same scope. |
| R3 | Assert **how many times** the fail point fired, from the `count` that `configureFailPoint` returns | Loads `:48-52`, Returns `:28-31`, Sandop `:56-59` | Today "503 after `times: 3`" passes with one firing. With R1 the retries actually run; R3 proves they did. | `failpoint-episodes.tsv`: `times_fired` = 1 where 3 or 5 were armed. |

Expected effect, to be proven by the WP that implements it (not claimed here):

- `UnknownCommitResultRetriesAndCommitsExactlyOnce` becomes deterministic, alone and in the suite.
- The two "unresolved → 503" tests start exercising real retry exhaustion. **They may then go red and expose a real
  difference** — for example Loads arms `times: 3` while `LoadRepository.cs:75` allows three commits; whether the
  driver's own retries change the count has to be measured. That would be new information, not a regression.
- About 80 s of the 250 s suite time disappears (Returns 70 s, Sandop 16 s, Loads 13 s are spent waiting on the
  10 s cycle or on the 60 s transaction lifetime).

## Options considered and not recommended as the fix

| Option | Verdict |
|---|---|
| **Serialised collection** | Already in place: the whole assembly is serial (`LoadContractTests.cs:29`). It does not help — the flip happens with one test alone. Keep it; it is what makes the shared fail point safe today. |
| **Per-class isolation by separate mongod** | Not needed for the flip (it is client-side). R2 gives the isolation at no cost. |
| **Raise `serverSelectionTimeoutMS` in the lane script** (`mvp6-test-mongo-env.sh:57`) | Makes the test green (r03, r05) but slow (9 s) and for the wrong reason: it hides the 91 problem and removes the fast fail-closed behaviour Q131 wanted. Do not use as the fix. |
| **Add `heartbeatFrequencyMS=1000` to the lane URI** | Works (r04, H5) and is harmless, but it is an environment patch over a test-design fault, and the "unresolved" tests would still fire once instead of N times. Acceptable only as a stop-gap, by CT decision. |
| **Product change** — let the commit loop survive a `TimeoutException` during re-selection (`LoadRepository.cs:75`, and the same shape in Returns / Claims / Carriers) | Not required by the pack (`MOD-0185…md:268, :402`). A robustness question for CT / the owner. If taken, it is a product WP with its own acceptance text, not part of this test fix. |

## Until the fix lands

- Do not count `Loads.LoadAtomicityTests.UnknownCommitResultRetriesAndCommitsExactlyOnce` as evidence in either
  direction. A baseline of "416 / 417" can become "415 / 417" with no code change.
- Do not cite the three "unresolved unknown commit" tests (Loads, Returns, S&OP) as proof that retry exhaustion is
  handled. They prove the single-failure path only.
- Run the suite with the assembly serial, one process per mongod. Two lanes on one mongod are unsafe while the
  fail point is server-global.
