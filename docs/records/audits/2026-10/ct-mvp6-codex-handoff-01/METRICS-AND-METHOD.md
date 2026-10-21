# METRICS AND METHOD

## 0. Two rules this document obeys

1. **Estimated effort is never presented as elapsed work.** Where a figure is an estimate, it says so.
2. **No completion percentage is derived from prompt or message counts.** Message volume measures
   conversation, not delivery. Any such number would be fabricated and none appears here.

## 1. The last CT-accepted readiness measure

**GATE-18-0 (SOP §18.0) — the only acceptance matrix with a CT-accepted numeric state.**

Scope: **four** modules (MOD-0184 Carriers, MOD-0185 Loads, MOD-0186 Returns, MOD-0187 Claims)
× **14** gate rows = **56 cells**. MOD-0183, 0190, 0192, 0147 and 0148 are **not** in this matrix.

| state | v9.4 (2026-10-12) | v9.5 (2026-10-13) |
|---|---:|---:|
| MET | 37 | **43** |
| NOT MET | 9 | 4 |
| NOT MEASURED | 10 | 9 |

**Denominator: 56.** `43 / 56 = 76.8%` of gate cells MET.

This percentage is **cells measured MET, nothing else**. It is not programme completion, not module
completion and not effort. Thirteen cells are open and the modules also carry 61 open acceptance
checkboxes that this matrix does not count.

### How 43 was reconstructed, independently, on 2026-10-16

The base matrix and the movements live in different files on purpose, so the figure has to be
recomputed rather than read:

1. Read `docs/records/audits/2026-10/mvp6-q446-gate-18-0-four-modules-01/GATE-18-0.tsv`
   (57 lines = header + 56 cells) and counted verdicts → **MET 37, NOT MET 9, NOT MEASURED 10**.
   This reproduces v9.4 exactly.
2. Overlaid the four `MOVED.tsv` files, 6 cells in total:

   | cell | from | to | lane |
   |---|---|---|---|
   | MOD-0184 row 13 Observability | NOT MET | MET | `mvp6-stage-r-row13-01` |
   | MOD-0185 row 13 Observability | NOT MET | MET | `mvp6-stage-r-row13-01` |
   | MOD-0186 row 13 Observability | NOT MET | MET | `mvp6-stage-r-row13-02-returns-claims` |
   | MOD-0187 row 13 Observability | NOT MET | MET | `mvp6-stage-r-row13-02-returns-claims` |
   | MOD-0186 row 5 Concurrency | NOT MEASURED | MET | `mvp6-stage-r-row5-returns-01` |
   | MOD-0185 row 1 Golden flow | NOT MET | MET | `mvp6-stage-r-row1-loads-config-01` |

3. `37 + 6 = 43` MET; `9 − 5 = 4` NOT MET; `10 − 1 = 9` NOT MEASURED. **Reconciles to v9.5 exactly.**

`GATE-18-0.tsv` was **not edited** — it sits inside its own lane's `ARTIFACTS.sha256` and editing it
would break a dated seal (Q487).

### The 13 open cells, by why they are open

| count | cells | why |
|---:|---|---|
| **9** | rows 2 (×4), 7 (×2), 12 (×3) | NOT MEASURED — browser halves only; rows 7 and 12 already have their server halves in the suites. **All nine behind one authorization** (Q496). |
| **2** | rows 3, MOD-0186 / MOD-0187 | NOT MET — with the **integration owner**. Measured documentary-only: identical answers direct at 5066 and through the gateway on all five paths (Q497). |
| **2** | rows 10, MOD-0186 / MOD-0187 | NOT MET **in a stale TSV only** — waived by owner decision 2026-10-05. |

So of 13 open cells, **9 need one decision**, 2 need an assigned owner for already-decided work, and
2 are a bookkeeping artifact.

## 2. Evidence freshness — fresh / inherited / static-only / NOT RUN

| class | what it covers |
|---|---|
| **FRESH (2026-10-13)** | the six moved cells; the Stage R stack measurement; the gateway transparency measurement; the live UAS-001 observation on all four modules. All measured on a running stack. |
| **FRESH (2026-10-16)** | everything in `OPEN-FINDINGS-AND-GATES.tsv` with `measured_on` = 2026-10-16: the git geometry, the merge-conflict test, the secret classification, the CI coverage gap, the untracked-artifact audit and its 35 hash verifications, the acceptance checkbox counts. Static reads of the tree — **no build and no runtime were executed by this package.** |
| **INHERITED** | MOD-0186/0187 independent VER results from 2026-09 lanes; MOD-0183/0184 E4 acceptance. Carried from sealed records, not re-run. |
| **VALID_PRE_MERGE / INTEGRATION_STALE** | explicitly set by the owner merge decision for Q372-R2 row 13, the Q425 suite baseline and the five module golden flows. **Diagnostic and reference only** — the decision forbids claiming an integrated PASS from them (K18). 6 `CT-QUEUE` rows carry this state. |
| **STATIC-ONLY** | the gate rows closed by reading code rather than running it — row 13 rested on controller log lines already present (`LoadsController.cs:21`, `CarriersController.cs:27`, `ReturnsController.cs:26`, `ClaimsController.cs:28`), confirmed by HTTP logs in the lanes. |
| **NOT RUN** | the 7-item main-merge completion gate (**F5**). Any SupplyChain CI (**F4** — there is no job). The nine browser cells (**F6**). |

**Nothing is hidden.** Failing and excluded results carried forward verbatim:

- MOD-0184 §25 Phase 1.5 approval: **HELD** — "no user approval claimed"; the pack's own §—
  line reads *"Status remains draft. DEV NO-GO"* for the historical scope that §§29–32 superseded.
- The Claims test that **refuses the operational Mongo by design**: Shipments measured 80 of 81, the
  single failure being that deliberate refusal.
- `ContractErrorCodes` parity test: **deliberately not written**, because Q215 (test-code
  authorization) was ungranted and CT had already recorded writing test code without it as
  self-violation V-2. Recorded as stopped, not as done.
- Q488: the Returns transition race has one winner in 13 of 13 measured races and **CT did not
  establish why**. Open question, not a closed finding.

## 3. Acceptance checkboxes — a second, independent denominator

Measured 2026-10-16, `[ ]` vs `[x]` per pack:

| module | open `[ ]` | closed `[x]` | pack total | closed % |
|---|---:|---:|---:|---:|
| MOD-0183 | 12 | 11 | 23 | 47.8% |
| MOD-0184 | 7 | 3 | 10 | 30.0% |
| MOD-0185 | 12 | 6 | 18 | 33.3% |
| MOD-0186 | 12 | 6 | 18 | 33.3% |
| MOD-0187 | 13 | 6 | 19 | 31.6% |
| MOD-0190 | 1 | 18 | 19 | **94.7%** |
| MOD-0192 | 4 | 18 | 22 | **81.8%** |
| **seven logistics packs** | **61** | **68** | **129** | **52.7%** |
| MOD-0147 | 11 | 4 | 15 | 26.7% |
| MOD-0148 | 13 | 4 | 17 | 23.5% |
| **all nine** | **85** | **76** | **161** | **47.2%** |

Method and its limits: a checkbox is a **declared** acceptance line in the pack, counted by regex
(`^\s*[-*]?\s*\[ \]` against `\[[xX]\]`). Checkboxes are **not** equal in size — one may be a
sentence and another a week — so these percentages measure *declared lines closed*, **not effort and
not progress**. The two denominators (56 gate cells, 161 checkboxes) are different instruments and
must not be averaged.

**What the second instrument shows that the first cannot:** MOD-0190 and MOD-0192 are the *most*
closed modules by checkbox (94.7% and 81.8%) and appear in no gate cell at all, because they are
fixture-bound for all of MVP-6 by owner decision. A gate-only view makes them invisible.

## 4. Frozen baseline, delivered, accepted, remaining

| | |
|---|---|
| **frozen baseline** | GATE-18-0 as sealed in `mvp6-q446-gate-18-0-four-modules-01` — 37/9/10, denominator 56. Frozen because it sits inside a dated `ARTIFACTS.sha256`. |
| **current forecast** | **no CT-accepted forecast exists.** No record in this set projects a completion date for the 13 open cells or the 61 checkboxes. Stating one here would be invention. |
| **delivered (CT-accepted)** | bounded backend cores for MOD-0183/0184/0185/0186/0187/0190/0192; UI DEV for MOD-0184 (R-4a) and MOD-0185 (R-4b); 43 of 56 gate cells. |
| **accepted but conditional** | every pack reads `ready-for-dev` for a **bounded** scope only. Common-checkout integration, gateway, live producer uptake, UI, E5/G5 and rollout are open in all of MOD-0186/0187/0190/0192. Nothing in this programme is RELEASED. |
| **remaining, measured** | 13 gate cells · 61 acceptance checkboxes (7 packs) · 8 owner-queue items · 1 unassigned integration release · 0 CI coverage for SupplyChain · 1 un-run completion gate. |
| **unestimated** | MOD-0147 and MOD-0148 in full — both `draft`, both blocked on DC-01 and DC-02, 24 checkboxes between them, no dispatch and therefore no estimate. |

## 5. Reserve overlap and method decisions

- **Overlap to avoid double-counting:** closing Q496 moves **nine** cells, of which rows 7 and 12
  (five cells) already have server-half evidence inherited from the suites. The nine are not nine
  units of fresh work; they are nine cells needing one browser session on an authorized account.
- Six cells moved on 2026-10-13 **produced one new guard and one near miss**, not six units of
  feature work. Row 13 needed no new production code at all. Counting moved cells as delivered
  effort would overstate the day by a wide margin.
- **Method decision, Q487:** sealed matrices are never edited; movements go in `MOVED.tsv`. The
  consequence for any future metric is that **the current state is a base file plus N overlays**, and
  reading the base alone under-reports by 6 cells today.
- **Method decision, K4:** a written record is never corrected. A wrong audit is superseded by a new
  one and the old one stays. Metrics must therefore be read from the newest overlay, not the newest
  edit — there are no edits.
