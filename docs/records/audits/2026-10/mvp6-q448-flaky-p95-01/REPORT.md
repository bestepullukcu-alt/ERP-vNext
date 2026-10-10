# verify from: cd docs/records/audits/2026-10/mvp6-q448-flaky-p95-01 && shasum -a 256 -c ARTIFACTS.sha256

# Q448 — `ShipmentTelemetryTests.cs:71`: a measured duration against an exact requested delay

| Field | Value |
|---|---|
| Lane / agent | Q448 · integration-agent · single writer on `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Shipments/ShipmentTelemetryTests.cs` |
| Placement | Claude app → Code tab → Local, Darwin. G2 placement waiver applied — Cowork withdrawn by owner. |
| Branch / HEAD | `feature/mvp6-logistics` @ `cea01354e`; staged 0 at start and end; no `.git/index.lock` |
| Start / end (Europe/Istanbul) | 2026-10-05 01:08:36 +03 / 01:35 +03 |
| Verdict | **Fixed in the test, proven over 200 isolated runs and 20 module runs; unstaged, uncommitted.** Agent verdict ≠ CT ACCEPTED. |

## 1. The mechanism, measured

`PerformanceBehavior.cs:10-14` times the handler with a high-resolution `Stopwatch` and records
`watch.Elapsed.TotalMilliseconds`. `Task.Delay(n)` schedules its due time on `Environment.TickCount64`, which advances
in whole OS timer ticks. So a delay can complete up to one tick earlier than the Stopwatch says `n` ms have passed.

A standalone probe (`evidence/delay-probe.cs.txt`) ran 1,200 delays of 3…60 ms, timed exactly as `PerformanceBehavior`
does:

| Probe run | Early returns | Worst early | Worst for 57 ms |
|---|---|---|---|
| 1 (01:10) | 10 of 1,200 | 0.835 ms | 57.160 ms (not early) |
| 2 (01:34, `evidence/delay-probe-output.txt`) | 5 of 1,200 | 0.613 ms | **56.484 ms** |

On macOS the tick is 1 ms and every early return was under 1 ms. On Windows the default tick is about 15.6 ms; that is
not measured here, it is the OS default.

## 2. Before the change

| Proof | Result |
|---|---|
| Shipments module ×20 (Q266 filter, own mongod 57448) | **20/20 passed**, 90/0/90 each; p95 57.2–58.4 ms. Margin as small as 0.2 ms. (`evidence/module-20x-before.txt`) |
| The p95 test alone ×200 | **197 passed, 3 failed (1.5 %)**; p95 56.4, 56.6 and 56.8 ms in the failures. (`evidence/p95-200x-before.txt`) |

Twenty module runs showed nothing: the per-run failure rate is about 1.5 %, not "1 in 4" (F-Q448-1). The 200-run
series is what demonstrates the defect.

## 3. The change

Test file only, `:71` → `:71-79`. Diff: `evidence/test.diff`, 10 changed lines. Production telemetry code not touched.

- `const double timerGranularityMs = 16;` — one Windows timer tick.
- `var floor = delays.OrderBy(d => d).ElementAt(18) - timerGranularityMs;` — 57 − 16 = 41 ms.
- `Assert.True(p95 >= floor, …)` — the message now states the floor and why.
- A comment says what the tolerance is for: the TickCount-vs-Stopwatch granularity, the measured numbers, and that the
  assertion still fails when durations are missing, zero or recorded in seconds.

No retry was added.

## 4. After the change

| Proof | Result |
|---|---|
| Shipments module ×20 | **20/20 passed**, 90/0/90 each; p95 57.2–58.5 ms (`evidence/module-20x-after.txt`) |
| The p95 test alone ×200 | **200 passed, 0 failed**; p95 min 56.5 ms. Five runs had p95 below 57 ms (56.5, 56.7, 56.8, 56.8, 56.9) and would have failed the old assertion. (`evidence/p95-200x-after.txt`) |

## 5. The tolerance does not blind the test (sabotage, scratch copy only)

`PerformanceBehavior.cs:14` in the **copy** was changed to record `TotalSeconds`. The p95 test failed 3 of 3:
"p95 0.1 ms is below 41.0 ms". The copy was then restored (`cmp` equal to the tree) and the test passed. The tree's
`PerformanceBehavior.cs` was never edited. Evidence: `evidence/sabotage-seconds-run-*.txt`.

## 6. Every other assertion in the file

Line numbers are the file before this change; lines after 71 now sit 8 lower.

| Line(s) | Assertion | Same shape (measurement vs exact requested value)? |
|---|---|---|
| 67 | `Assert.Equal(delays.Length, values.Length)` | No. A count of recorded values; deterministic. |
| 68 | `Assert.Equal("ms", recorder.Unit(...))` | No. The instrument's declared unit, a constant. |
| 71 (now 71-79) | p95 vs the 19th delay | **Yes — the one fixed here.** |
| 86, 88, 94 | `recorder.Sum("shipment.intake.blocked")` = 1, 2, 2 | No. Counter sums, deterministic. |
| 109, 115, 120, 126, 132, 149, 168, 169 | drift and blocked counter sums | No. |
| 116, 121, 150 | `DriftRowsAsync` row counts | No. Database row counts. |
| 85, 87, 92, 93, 107, 108, 114, 119, 125, 130, 131, 141, 148, 161, 167 | intake states and error codes | No. |
| 192-202, 211-216, 226-239 | HTTP statuses, replay and outbox counters, `commits.Failed` = 1 | No. Counters and statuses; the failpoint fires exactly `times: 1`. |
| 249-276 | outbox retry and dead-letter sums, row status | No. |

Nothing else in the file compares a measured time with a requested one. Nothing else was changed.

## 7. Findings

- **F-Q448-1 (dispatch premise overstated):** The flake rate is about 1.5 % per run here (3/200), not 1 in 4. Twenty
  module runs had a ~26 % chance of showing even one failure, and showed none. A pass count over 20 runs cannot
  separate fixed from unfixed; the 200-run series can.
- **F-Q448-2:** The cause is TickCount granularity, not load. Under load delays run late, which adds margin. Early
  completions happen at idle. Evidence: both probe runs.
- **F-Q448-3:** On Windows the same assertion would have failed far more often: Task.Delay can be early by up to one
  ~15.6 ms tick. The 16 ms tolerance covers that. Inference from the OS default, not measured here.
- **F-Q448-4:** MODULE-RECIPE row 8.2 ("read the SupplyChain suite as a range … fails about 1 run in 4") and ledger
  Q361 can be revisited once CT accepts this.

## 8. Boundaries

- No stage, no commit, no retry added.
- Production code untouched: the sabotage ran in the copy only, and the copy was restored.
- Ran in `~/mvp6-env/q448-20261005-0109/` (copy of SupplyChain, BuildingBlocks, packs and contracts). Own mongod 57448,
  stopped; 57448 is closed. The scratch folder stays (no `rm`).

Return to CT; CT decides.
