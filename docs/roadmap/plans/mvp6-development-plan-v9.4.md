# MVP6 development plan v9.4 — 2026-10-12

Successor to [v9.3](mvp6-development-plan-v9.3.md), written the same day and correcting it. v9.0–v9.3
stay as records.

v9.3 was wrong about six queue items because CT built its queue from `CT-QUEUE.tsv`'s narrative rows and
Q446's carried-forward table without reading `docs/records/decisions/`. The corrections are measured in
[`mvp6-q446-date-recheck-01`](../../records/audits/2026-10/mvp6-q446-date-recheck-01/REPORT.md).

**Standing rule this plan adds:** `CT-QUEUE.tsv` rows and audit reports are snapshots.
`docs/records/decisions/` is newer than both. Nothing enters the owner's queue before that directory is read.

This plan grants no execution, publication, promotion, commit, push or rollout authority. Per GIT-002 §4
that includes every commit.

## 1. What v9.3 got wrong

| v9.3 | measured |
|---|---|
| Row 3 ruling "assigned 2026-09-27, never made" | **made 2026-10-05** — `mvp6-gateway-explicit-routes-owner-decision-01.md`, ACCEPTED, `ledger: Q397, Q446, Q456` |
| Row 10 waiver pending | **granted 2026-10-05** — `mvp6-returns-claims-evidence-row10-waiver-owner-decision-01.md`, ACCEPTED |
| Q414 "one line, highest impact" | **done 2026-10-06** — `c74ea4839`; the tracked `appsettings.Development.json` carries `Loads:ReferenceBaseUrl` |
| 0147/0148 awaiting a CT ruling on five concurrences | **ruled 2026-10-05** — `mvp6-ct-ruling-dc01-dc05-concurrence-01.md`, whose own header says the ruling "was owed for eight days" |
| 0190/0192 fixture-bound status quo unrecorded | **recorded 2026-10-05** — `mvp6-0190-0192-fixture-bound-owner-decision-01.md`, ACCEPTED |
| Q399 as an undecided 2026-11-03 threat | **conditioned 2026-10-03** — `mvp6-shipment-admin-baseline-temporary-widening-owner-decision-01.md:38`: on that date the widening is re-approved or retracted, it "does not lapse quietly" |

CT also asked the owner on 2026-10-12 to grant the row-10 waiver, which already existed. That is in the
[discipline self-audit](../../records/audits/2026-10/ct-discipline-self-audit-2026-10-12-01/REPORT.md).

## 2. Measured state

Acceptance counts from the branch holding each module's newest copy; four branches diverged at 15:01 on
2026-10-11 and the main checkout is older than all of them.

| module | status | newest on | open | closed | §18.0 after re-check |
|---|---|---|---:|---:|---|
| MOD-0183 Shipment/POD | ready-for-dev | golden-flow | **3** | 20 | not in Q446's scope |
| MOD-0184 Carriers | ready-for-dev | golden-flow | 1 | 9 | 10/14 · 4 open |
| MOD-0185 Loads | ready-for-dev | golden-flow | 6 | 12 | 11/14 · **1 open** (was 3) |
| MOD-0186 Returns | ready-for-dev | golden-flow | 5 | 13 | 8/14 · **3 open + 2 waived/decided** |
| MOD-0187 Claims | ready-for-dev | golden-flow | 6 | 13 | 8/14 · **3 open + 2 waived/decided** |
| MOD-0190 S&OP | ready-for-dev | bounded-slice | 1 | 18 | not in Q446's scope |
| MOD-0192 Capacity | ready-for-dev | bounded-slice | 4 | 18 | not in Q446's scope |
| MOD-0147 Supplier perf | **draft** | golden-flow | 11 | 4 | — |
| MOD-0148 Supplier portal | **draft** | golden-flow | 13 | 4 | — |
| | | | **50** | **107** | |

MOD-0183 is 3 open because `:430` closed on 2026-10-12 by owner decision
(`mvp6-q420-forbidden-unavailable-owner-decision-01.md`): the `Forbidden → Unavailable` mapping stays and
503 is the answer. Its remaining three lines all wait on **MOD-0178** (Putaway/Picking/Packing, W-3, no
pack in recovery), as does MOD-0192 `:203`.

Percentages omitted: REP-001 §2 forbids one without its row list.

## 3. Q446's nineteen open cells, after the date re-check

| state | cells | who |
|---|---:|---|
| decided 10-05, gateway edit pending; sequence gate **now open** (`origin/main` = `a814bad0f`, merged 10-10) | 2 — rows 3, MOD-0186/0187 | **integration owner** (`gateway/**` is Protected Paths in every pack) |
| **CLOSED UNDER WAIVER** 10-05, not NOT MET | 2 — rows 10, MOD-0186/0187 | done |
| closed by code 10-06 | 1 — row 1, MOD-0185 | done |
| code complete, needs only a re-measure | 3 — row 13, MOD-0185/0186/0187 | **live lane** |
| NOT MET, not actionable by CT | 1 — row 13, MOD-0184 | owner: `:376` withholds runtime write |
| NOT MEASURED, unchanged | 10 — rows 2, 5, 7, 12 | **live lane** |

## 4. Critical path

```
R  live lane       one session: 13 cells  (10 unmeasured + 3 re-measures)   <- highest leverage
   evidence        re-run today's measurements with output into the record  <- fixes REP-001 B2
2  integration     gateway explicit routes (decided, unblocked) + Q15
4  UI wave         Q322: MOD-0183 first, then the four as one wave behind Q320
6  release         unchanged
```

### Stage R — one session, thirteen cells

| row | modules | what the session produces |
|---|---|---|
| 2 No-shell | 0184, 0185, 0186, 0187 | Save View, column visibility, Reset, each with a reload |
| 5 Concurrency | 0186 | one conflicting concurrent transition, two actors on one Return, recorded inside `docs/records` |
| 7 Validation | 0184, 0187 | one client-side rejection and one server 400/422 that keeps inputs |
| 12 UX states | 0184, 0186, 0187 | the states not yet produced, notably one empty list |
| 13 re-measure | 0185, 0186, 0187 | outcome lines with replay, now that the code carries them |

Measuring MOD-0184 is permitted: `:376` withholds runtime write, not observation.

### The evidence debt, and it is CT's

Ten cold runs, five baseline runs, 48/48, 134/135, 10/10 were all parsed from stdout and **no run
artefact was committed**. REP-001 §3 calls that E0. It affects MOD-0192 `:197`, `:198`, `:202` and the
three atomicity timings. Remediation is a re-run with output captured into
`docs/records/audits/2026-10/<lane>/evidence/` and cited as `path@commit`. Until then those ticks are
stronger in the prose than in the record.

## 5. Owner queue, corrected — four items, none of them dated

| # | decision | state |
|---|---|---|
| 1 | **Integration-owner release** for the gateway explicit routes (rows 3) | decided 10-05, sequence gate open since 10-10, nobody assigned |
| 2 | **Integration-owner release** for the `Program.cs` capacity branch + re-pin of both composition hashes | owner deferred 2026-10-12 to the §22 uptake package; closes MOD-0192 `:199`/`:200`, measurement already done |
| 3 | **MOD-0184 runtime-write release** | `:376` blocks row 13's pack-required signals |
| 4 | **Q15** integration execution in one registered checkout | unchanged since v9.2; still the Stage 2 gate |

Decided on 2026-10-12 and recorded: Q420 (`:430` closes, 503 stays), the integration-owner deferral
above, and git authority for the ten pushed commits.

## 6. Two tracks that are not MVP-6 but touch it

The Treasury work package and its plan are not in this repository; the owner holds them. Recorded here
only where they touch MVP-6, and both measurements below were taken in this repository.

| track | MVP-6 intersection |
|---|---|
| **MOD-0220 LE relationship model** (recovery / `Diten.MdmService`, `mdm-domain-team`) | **Real.** `lookup-validation` has five production consumers in four services and one is MVP-6's `MdmLegalEntityScopeValidator.cs`, whose seam `ReturnReferenceReader` and `ClaimReferenceReader` share. Relationship-aware eligibility answering 404 for an entity with no approved relationship would start rejecting entities that work today. The backward-compatibility test must cover five, not three. The same five are the blast radius of the Q420 decision in §2, in both directions. |
| **MOD-0003 Data Contract Registry** (recovery, `platform-shared-services`/`Diten.Platform`, final phase) | **Almost none.** Only MOD-0184 declares it, as "opaque reference validation only", and `SupplyChainService/src` has zero references. MVP-6 finishes without it; it gates four Treasury modules. |

Neither is CT's to build.

## 7. Measures

Method unchanged from v9.2 §6. Figures not restated: v9.2's predate every record since 2026-09-26, and
no writer-complete, independent-VER or CT-decision event has been recorded since.
