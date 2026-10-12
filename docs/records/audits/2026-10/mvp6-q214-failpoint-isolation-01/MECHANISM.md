# Q214 — Why the Loads commit-retry test flips

Test: `Loads.LoadAtomicityTests.UnknownCommitResultRetriesAndCommitsExactlyOnce`
(`services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Loads/LoadAtomicityTests.cs:29-42`).

## 1. Answer

**The outcome is decided by a clock, not by what runs beside the test.**

The test injects error **91** (`ShutdownInProgress`) into `commitTransaction`. The MongoDB driver treats 91 as
"this server is going away": it marks the only server of the lane replica set **Unknown** and closes its
connections. The driver learns that the server is fine only when its monitoring connection's pending `hello`
returns, and that `hello` is a long poll of up to **10 s** (`maxAwaitTimeMS: 10000` = the default heartbeat).
The server was never really shutting down, so nothing wakes the poll early.

The product then retries the commit. The retry must first select a server. The lane URI sets
`serverSelectionTimeoutMS=5000`.

- If more than 5 s of the 10 s poll remain → server selection times out → the product returns **503**. Test red.
- If less than 5 s remain → the poll returns in time → the retry commits → **201**. Test green.

So the test passes only when the fault happens to land in the second half of the monitor's 10-second cycle.

- **Test alone:** the driver's cluster opens about 1.3 s before the fault. About 8.7 s remain. **Always red**
  (Q208 4/4, Q214 2/2).
- **Inside a larger run:** the cycle started when the process first used the URI. Where the fault lands depends on
  the summed duration of everything that ran before. Green in Q208's full run and in Q214's (2.7 s remained);
  green in Q214's Loads-folder run (3.2 s remained); red in Q208's Loads-folder run and in Q214's class run.

The neighbouring classes do not consume, disable or leave behind the fail point. See §4.

## 2. Evidence

### 2.1 Server side (lane mongod log, every command logged)

`evidence/failpoint-episodes.tsv` — one row per arm → hit → off. For the test under study:

| Run | Fault hit | Same transaction committed | Result |
|---|---|---|---|
| r01 alone | 19:58:46.115 | never — **no second `commitTransaction` reached the server** | red |
| r02 alone | 20:00:14.481 | never | red |
| r07 class | 20:02:07.427 | never | red |
| r06 Loads folder | 20:01:46.492 | 3.221 s later | green |
| r08 full suite | 20:06:37.241 | 2.719 s later | green |
| r03 alone, 30 s selection timeout | 20:00:26.681 | 9.504 s later | green |
| r05 alone, no timeout in the URI (driver default 30 s) | 20:00:44.900 | 9.114 s later | green |
| r04 alone, `heartbeatFrequencyMS=1000` | 20:00:40.063 | 0.174 s later | green |

In r01 the log shows, in order: fail point armed → the measured request's five writes → the fail point fires
once on that request's `commitTransaction` (error 91) → the connection is closed 11 ms later → **8.7 s of
silence** → one `find` on `loads_receipts` (the product's recovery lookup) → fail point off. The fail point was
consumed by the measured request, not by setup. The full-suite run shows the hello cycle directly: every
error-91 episode ends at a time ≡ 9.8–9.96 s (mod 10) — 20:02:59.835, 20:05:19.904, 20:06:39.965, 20:06:49.956.

### 2.2 Driver side (`evidence/harness.tsv`, source in `tools/harness/`)

A 60-line console program with the same driver (MongoDB.Driver 2.27.0) and the same commit loop as the product,
no product code, fail point scoped to its own `appName`:

| Case | Error | Selection timeout | Fault at | What the second commit did | Cluster state after the fault |
|---|---|---|---|---|---|
| H1 | 91 | 5 s | 0 s after open | `TimeoutException` after 5.0 s; 0 rows | `Unknown/Disconnected` until 10.1 s |
| H2 | 91 | 5 s | 7 s after open | OK after 2.9 s; 1 row | Unknown until 10.1 s |
| H3 | 91 | 30 s | 0 s | OK after 9.96 s; 1 row | Unknown until 10.1 s |
| H5 | 91 | 5 s, heartbeat 1 s | 0 s | OK after 0.9 s; 1 row | Unknown for 0.9 s |
| H4 | **8** | 5 s | 0 s | OK after **0.011 s**; 1 row | **stayed `Connected`** |

H1 and H2 differ only in *when* the fault lands. That is the flip, reproduced without any test class or product code.

### 2.3 Three predictions, made before the runs, all confirmed

1. A 30 s selection timeout makes the isolated test pass, slowly (r03, r05: 9.1–9.5 s).
2. A 1 s heartbeat makes it pass fast (r04: 0.17 s).
3. A non-state-change error code never produces the gap (H4).

## 3. Path through the product (`LoadRepository.cs`)

`services/Diten.SupplyChainService/src/Diten.SupplyChainService.Persistence/Features/Loads/LoadRepository.cs`

- `:75` — `for(commit=0;;commit++){ try{ CommitTransactionAsync } catch(MongoException ex) when (ex.HasErrorLabel("UnknownTransactionCommitResult") && commit<2){} }`
  - commit #0 throws `MongoNodeIsRecoveringException` with the label → caught → loop.
  - commit #1 cannot select a server → **`TimeoutException`**. That is not a `MongoException`, so the filter does
    not match and it leaves the loop.
- `:88` — `catch(Exception ex) when (ex is MongoException or TimeoutException)`.
- `:90` — because a commit was attempted: one receipt lookup. It waits for the server (≤ 5 s), finds no receipt.
- `:91` — returns `503 PERSISTENCE_UNAVAILABLE`.

The transaction was never committed (rows = 0), and the product did not report success or rollback.

## 4. The three candidate directions and the falsified lead

| Direction | Verdict | Evidence |
|---|---|---|
| A commit in the test's own setup consumes the `times: 1` failure | **No.** Setup issues three `find` + `abortTransaction` pairs (schema probes) *before* the fail point is armed, and no commit. The fail point fires on the measured request's commit. | r01 log sequence, `evidence/mongod-failpoint-and-commit-lines.log.gz` |
| A neighbouring class leaves the fail point off | **No.** Each test arms it itself, immediately before its request; it fired exactly once in 8/8 runs. | `failpoint-episodes.tsv`, column `times_fired` |
| Accumulated server state | **No.** The red result reproduces on a mongod that was 20 seconds old (r01), and the green result on the same mongod minutes later. The state that matters is in the *client*: the phase of the driver's monitor. | r01 vs r06/r08 |
| Q208 lead: a parallel class consumes the fail point | **Falsified** (as CT said): the assembly is serial (`LoadContractTests.cs:29`). Also measured: 0 hits on a foreign database in 27 episodes. | `BLAST-RADIUS.tsv` last row |

## 5. Which result is the true one

- **Neither result is a statement about the product.** Both are correct observations of the same code under two
  timings.
- **The isolated 503 is the true behaviour *of this test as written under the lane URI*:** given error 91 and a
  5 s selection timeout, 503 is what the code must return whenever more than 5 s of the monitor cycle remain.
- **Is the full-run green a false green?** For this test: it is a **coincidental green**. In that run the commit
  really was retried and really committed exactly once, so the assertion was met honestly — but only because the
  fault landed 2.7 s before the cycle boundary. It is not reproducible evidence and must not be counted as "the
  commit-retry path is verified". The ledger wording "full-run green is a false green"
  (`CT-QUEUE.tsv:310`) is right in effect: the green carries no information.
- **A true false green does exist next to it** — the `times: 3` and `times: 5` tests. See `BLAST-RADIUS.tsv`.

## 6. Classification: test design, not a product defect

Measured against the MOD-0185 pack
(`execution/domains/supply-chain-execution/module-packs/MOD-0185-routing-load-planning.md`):

- `:268` — "Uncertain commit never reported as definitely rolled back."
- `:402` — "Unknown commit resolves by durable receipt lookup, never blind insert."

The 503 path does exactly that: receipt lookup (`LoadRepository.cs:90`), no blind insert, no rollback claim, no
partial rows. The pack does not require that an unknown commit *must* end in 201.

**Test design — where:**

| Cause | Path:line |
|---|---|
| The injected code is 91, a state-change error that makes the driver drop the server | `Loads/LoadAtomicityTests.cs:19` |
| The fail point has no `appName` / namespace scope (server-global) | `Loads/LoadAtomicityTests.cs:14-22` |
| The lane URI caps server selection at 5 s, below the 10 s monitor cycle | `scripts/test-env/mvp6-test-mongo-env.sh:57` |
| The replica set has one member, so there is no other server to select | Q205 / Q208 provisioning (`PROVISIONING-STEPS.md`) |

**Product — an observation, not a defect by the pack:** the commit loop at `LoadRepository.cs:75` retries only
labelled `MongoException`s. A `TimeoutException` raised while re-selecting a server ends the retry at once. In
production (multi-node set, 30 s default selection timeout) a real step-down changes the topology and wakes the
monitor, so the 10-second blind window does not arise. The same loop shape is in `ReturnRepository.cs:139-140`,
`ClaimRepository.cs:139-140` and `CarrierRepository.cs:89-90`. Whether to harden it is a CT / owner decision.
