# Q213 — The CapacityPlans fail-point pattern (reference for Q215)

Static description (E1). Nothing was run in this lane; run-time facts are cited from Q214.
All line numbers are in
`services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/CapacityPlans/CapacityAtomicityTests.cs`
(class `CapacityFaultTests`, lines 91-367) unless a file is named.

## 1. The pattern in one view

| Element | What Capacity does | Where |
|---|---|---|
| Fault code | `errorCode: 8`, with `errorLabels: ["UnknownTransactionCommitResult"]` when an *unknown* commit is wanted, `[]` when a *definite* failure is wanted | `:112` |
| Ack-loss fault | `writeConcernError { code: 64 }` — the command runs, the acknowledgement is reported as failed | `:111` |
| Scope | `data.appName = <one name per test>`; the client under test is built with that application name | `:110`, `:103`; `CapacityTestMongo.cs:26-27` |
| Control client | A separate plain client (no application name) arms and disarms the fail point | `:113`, `:120`, `:125` |
| Count | `configureFailPoint` returns `count`. Read it when arming (`before`), read it again when switching off, assert on the difference | `:127` (read), e.g. `:261-264` (assert) |
| Disarm | Always in `finally`, so a failed assertion cannot leave the fail point armed | `:264`, `:278`, `:301`, `:319`, `:340`, `:357`, `:222`, `:243-248` |
| Serial | All Mongo classes of the module are in one collection with `DisableParallelization = true` | `CapacityRestartTests.cs:11`; `[Collection("CapacityMongo")]` on every Mongo class |

## 2. The three helpers, exactly

```text
Fail(appName, command, times, writeConcern=false, unknown=false)          lines 108-115
  data = { failCommands: [command], appName }
  writeConcern  → data.writeConcernError = { code: 64, errmsg: … }
  otherwise     → data.errorCode = 8; data.errorLabels = unknown ? ["UnknownTransactionCommitResult"] : []
  admin.runCommand({ configureFailPoint: "failCommand", mode: { times }, data })   ← returns { count }

FailAfter(appName, command, skip)                                         lines 116-122
  same data with errorCode 8 and no labels; mode: { skip }  → the (skip+1)-th matching command fails

StopFail()                                                                lines 123-128
  admin.runCommand({ configureFailPoint: "failCommand", mode: "off" })  → returns the final count
```

Usage shape (X01, lines 259-267):

```text
const app = "mod192-x01-unknown";  db = Reset(app)            // client under test carries appName = app
before = Fail(app, "commitTransaction", 2, unknown: true).count
try     { result = repo.EvaluateAsync(…) }
finally { hits = StopFail() - before;  Assert.True(hits >= 1) }
Assert 503 COMMIT_RESULT_UNRESOLVED and zero rows in all five collections
```

## 3. Why each element removes the timing dependence

**Error code 8 instead of 91.**
Q214 established the mechanism: 91 is a state-change error; the driver marks its only server unknown and the next
operation waits for a heartbeat or times out in server selection. Whether a retry then succeeds depends on how the
10 s heartbeat cycle falls against the 5 s selection timeout — that is the flip.
Code 8 is not a state-change error. The server stays `Connected` (Q214 harness H4: second commit OK in 0.011 s), so
the next command runs immediately and the outcome depends only on the fault that was injected.
The meaning the product code needs is carried by the **label**, not the code: product code branches on
`HasErrorLabel("UnknownTransactionCommitResult")` (`CapacityRepository.cs:26`, `CapacityLeaseStore.cs:109`), and the
test attaches or omits that label explicitly (`:112`). So the same code 8 gives both cases:

| Wanted situation | Injection | Product result asserted |
|---|---|---|
| Commit result unknown, nothing committed | code 8 + label | 503 `COMMIT_RESULT_UNRESOLVED`, zero rows (`:265-267`) |
| Commit definitely rejected | code 8, no label | 503 `DEPENDENCY_UNAVAILABLE`, zero rows, same key then succeeds (`:279-289`) |
| Commit happened, acknowledgement lost | `writeConcernError` 64 | 202 recovered from the receipt, exactly one row set (`:302-309`) |

Write-concern error 64 instead of 91 for the third row, for the same reason: 64 does not change the driver's view
of the server.

**`appName` scope.**
`failCommand` is one server-wide fail point. Without `appName` it fires on the next matching command from *any*
connection: another test class, a child `dotnet test` process, a second lane on the same mongod.
With `appName`, only connections that announced that application name can be hit. Each test uses its own name
(`mod192-x01-unknown`, `mod192-x06-insert`, `mod192-x07-later-<boundary>`, …), so a hit is attributable to one test.
The control client has no application name, so arming, disarming and the count read can never be the command
that fails.

One limit to state plainly: `appName` scopes **who can be hit**. It does not scope **who can arm or disarm**.
Any `configureFailPoint … off` from anywhere still switches the fail point off, and a second arm replaces the first.
That is why the serial collection (§1, last row) is part of the pattern and must be kept.

**Fire-count assertion.**
A test that only asserts "503" passes whether the fail point fired three times or once (Q214: Loads armed 3, fired 1;
Returns armed 5, fired 1). Reading `count` before and after and asserting on the difference proves the fault
actually reached the code under test, independent of how long anything took.

Capacity uses two strengths:

| Assertion | Where | Proves |
|---|---|---|
| `hits >= 1` | X01 `:264`, `:278`, `:301`; X06 `:319`; X07 `:222`, `:340`, `:357` | the fault fired at least once |
| `entered == 1` (exact) | X07 later-read boundaries `:247` | exactly one failure, at exactly the intended read |

## 4. What Q215 must change when it applies this — and one thing Capacity does not give it

1. Replace `errorCode: 91` by `errorCode: 8` and keep the `UnknownTransactionCommitResult` label
   (Loads `LoadAtomicityTests.cs:19`, Returns `ReturnAtomicityTests.cs:28`, S&OP `SandopAtomicityTests.cs:55`);
   replace `writeConcernError` 91 by 64 (S&OP `:72`). (Locations from Q214 `RECOMMENDATION.md` R1.)
2. Add `appName` to the fail-point data and build the client under test with the same application name. In Capacity
   this is one helper: `CapacityTestMongo.WithApplicationName` (`CapacityTestMongo.cs:26-27`). Loads, Returns and S&OP
   start a host or a repository from a connection string, so the name has to go into that connection string.
3. Arm and disarm from a different client, disarm in `finally`, and assert on the count difference.
4. **Use an exact count where retry exhaustion is the claim.** Capacity's commit tests assert only `>= 1`, and that
   is enough for Capacity because its product code does **not** retry a commit: there is a single
   `CommitTransactionAsync` per attempt and no commit loop (`CapacityRepository.cs:92`, `:132`, `:168`;
   `CapacityLeaseStore.cs:107`). X01 arms `times: 2` and one firing is the whole story.
   Loads and Returns *do* loop on the commit, so their "unresolved after N" tests need
   `Assert.Equal(N, hits)`. `>= 1` copied as-is would leave them green for the wrong reason again.
   The exact form is at `:247`.
5. Keep the classes serial (§3, limit of `appName`).

## 5. Do any of the 48 Capacity tests still depend on timing?

Each test was read. "Deterministic" means the outcome is decided by an injected fault, a compare-and-set or pure
logic, not by how long something takes.

| Test (cases) | File:line | Depends on timing? |
|---|---|---|
| X01 unknown commit (1) | `CapacityAtomicityTests.cs:257` | No — code 8 + label, appName |
| X01 pre-commit failure then same key (1) | `:271` | No |
| X01 commit ack loss (1) | `:294` | No — write-concern error 64 |
| X06 terminal insert fault (1) | `:313` | No |
| X07 unknown terminal commit, not committed (1) | `:333` | No |
| X07 terminal commit ack loss (1) | `:350` | No |
| X07 reconciliation rejects inexact effects (13) | `:180` | No — data mutants, no fail point |
| X07 reconciliation read failure (1) | `:215` | No |
| X07 each later read failure (5) | `:232` | No — `skip` mode, exact count |
| Fixture mutations and fenced terminal (1) | `:13` | **Weakly.** It expires a lease by writing `LeaseUntil = client clock − 1 min` (`:75`, `:81`) while the product compares with server `$$NOW`. Safe while client and server clocks agree within a minute (same host here) |
| Renewal advances version (1) | `CapacityLeaseTests.cs:12` | No, unless the test itself takes longer than the 30 s lease |
| Name-index race loser (1) | `CapacityConcurrencyTests.cs:17` | No — the race is forced by a driver command-event hook (`:34-46`), not by timing |
| Duplicate policy (1) | `CapacityConcurrencyTests.cs:60` | No |
| **Twenty distinct keys → one active evaluation (1)** | `CapacityConcurrencyTests.cs:100` | **Yes, under load.** It asserts exactly 19 × 409. A loser that hits a write conflict retries at most 5 times with 10-50 ms back-off (`CapacityRepository.cs:146`, `:172-173`) and otherwise returns 503. If the winner's commit takes longer than that budget, a loser returns 503 and the test fails. Not observed (48/48 in Q208); the dependence is in the design |
| **Separate processes claim once, restart recovers (1)** | `CapacityRestartTests.cs:75` | **Yes, by design.** It sleeps 31 s for a real 30 s server-time lease (`:95`) and starts three `dotnet test` child processes. Direction is safe (waiting longer only helps) except one case: if the second child starts more than 30 s after the first claimed, it re-claims and `Attempt == 1` (`:93`) fails. It costs ≥ 31 s of the module's 138 s |
| `ChildWorker` (1) | `CapacityRestartTests.cs:20` | No — but it is **vacuous** in a normal run: it returns at `:23` when `MOD192_CHILD_MODE` is unset. It counts as one of the 48 passes and asserts nothing there |
| Literal oracle Finite / Infinite (2) | `CapacityLifecycleTests.cs:31` | No — fake lease store |
| Contract (3), fingerprint (1), URI guard (9) | `CapacityContractTests.cs`, `CapacityReplayTests.cs`, `CapacityTestMongoTests.cs` | No — pure |

Total 48. Timing-dependent: 2 (one by design, one under load), plus 1 weak clock assumption. Vacuous: 1.
None of the 12 fail-point episodes depends on timing.
