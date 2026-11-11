# MVP6 development plan v9.3 — 2026-10-12

Successor to [v9.2](mvp6-development-plan-v9.2.md) (v9.0–v9.2 stay as records). Written at the owner's
request together with [the CT discipline self-audit](../../records/audits/2026-10/ct-discipline-self-audit-2026-10-12-01/REPORT.md).

This plan grants no execution, publication, promotion, commit, push or rollout authority. Each such step
needs its own exact owner decision — and per GIT-002 §4, **that includes every commit.**

## 1. Why v9.2 cannot be followed

v9.2 is dated 2026-09-26 and its highest queue reference is **Q52**. `CT-QUEUE.tsv` is at **Q483**. Three
of its statements are now wrong:

| v9.2 says | measured 2026-10-12 |
|---|---|
| Q27 / Q28 S&OP and Capacity pack promotion **DECISION-REQUIRED** | `CT-QUEUE.tsv`: **DONE — CT ACCEPTED after Q72** |
| Critical path Stage R → 2 → 4 → 6, Stage R "READY (Terminal)" | Stage R's three items are untouched; the work since has been acceptance and remediation, not Stage R |
| §18.0 denominator missing for four modules (via Q418) | Q418 was **wrong and corrected by Q445**, then executed as **Q446** — a fourteen-row measurement for all four |

**`CT-QUEUE.tsv` is the live source, not this file or v9.2.** This plan reads it; it does not replace it.

## 2. Measured state, 2026-10-12

Acceptance counts are read from the branch holding each module's newest copy, because four branches have
diverged since 15:01 on 2026-10-11 and the main checkout is older than all of them.

| module | status | newest on | open | closed | §18.0 (Q446) |
|---|---|---|---:|---:|---|
| MOD-0183 Shipment/POD | ready-for-dev | golden-flow | 4 | 19 | not in Q446's scope |
| MOD-0184 Carriers | ready-for-dev | golden-flow | 1 | 9 | **10 / 14** |
| MOD-0185 Loads | ready-for-dev | golden-flow | 6 | 12 | **11 / 14** |
| MOD-0186 Returns | ready-for-dev | golden-flow | 5 | 13 | **8 / 14** |
| MOD-0187 Claims | ready-for-dev | golden-flow | 6 | 13 | **8 / 14** |
| MOD-0190 S&OP | ready-for-dev | bounded-slice | 1 | 18 | not in Q446's scope |
| MOD-0192 Capacity | ready-for-dev | bounded-slice | 4 | 18 | not in Q446's scope |
| MOD-0147 Supplier perf | **draft** | golden-flow | 11 | 4 | — |
| MOD-0148 Supplier portal | **draft** | golden-flow | 13 | 4 | — |
| | | | **51** | **106** | |

> CT reported "99 open" twice on 2026-10-11/12. That count came from the main checkout's stale copies.
> The figure is **51**. Percentages are deliberately omitted: REP-001 §2 forbids a percentage without the
> row list under it, and the row lists are the packs themselves.

**Evidence caveat, and it applies to this whole table.** Nine commits from 2026-10-12 are **unpushed**
(`origin/feature/sce/mod-0190-0192-bounded-slice` is at `24bad8c8d`, local HEAD `7d75431fa`). REP-001 §3
excludes a local-only branch from counting as evidence, and no run output was committed with them. Until
both are fixed, the MOD-0190 and MOD-0192 figures above rest on E0 evidence. See the self-audit, B1 and B2.

## 3. Critical path

```
R  runtime lane      one live session closes eleven cells across four modules   <- the highest-leverage item
1  owner decisions   five, two of them dated
2  integration       Q14 -> Q15, still DECISION-REQUIRED
4  UI wave           Q322: MOD-0183 first, then the four as one wave behind Q320
6  release           unchanged
```

### Stage R — one live session, eleven cells

Q446's remaining cells converge on a single requirement: a live stack with the UI. Nothing in source can
close them and CT proved today that a source reading cannot substitute for a log count.

| row | modules needing it | what the session produces |
|---|---|---|
| 2 No-shell | 0184, 0185, 0186, 0187 | Save View, column visibility, Reset, each with a reload |
| 7 Validation | 0184, 0187 | one client-side rejection and one server 400/422 that keeps inputs |
| 12 UX states | 0184, 0186, 0187 | the states not yet produced, notably one empty list |
| 13 Observability | 0185, 0186, 0187 | a re-measure only — the code is complete (see §4) |

Measuring 0184 is permitted: its `:376` withholds *runtime write*, not observation.

## 4. What changed on 2026-10-12

Nine commits, all local. Grouped by what they settle:

**Q214 R1+R2+R3 across four atomicity classes.** `errorCode 91` → `8` with the
`UnknownTransactionCommitResult` label, `appName` scoping, and exact fire-count assertions.

| class | before | after |
|---|---|---|
| MOD-0190 Sandop | 7/7, 19 s | 7/7, 0.3–0.9 s |
| MOD-0185 Loads | **9/10**, 19 s | **10/10**, 1 s — the one real flip, closed |
| MOD-0186 Returns | 8/8, 60–86 s | 8/8, 0.2–0.3 s |
| MOD-0192 Capacity | already immune | R3 exact, from the unmerged `419538d9d` |

R3 also produced new information: Loads fires 3 of 3 armed and Returns 3 of 5, where Q214 had recorded 1.
The retries only became real once `91` stopped dropping the server.

**MOD-0192 went 7 open → 4.** `:201` corrected to contract 3.0.0 (the delta was one error code, already
shipped and tested). `:202` closed, and its "frozen mocks" clause turned from an inspection into a guard
with sabotage proof. `:197` and `:198` re-ticked on **ten cold runs** after the retraction's outstanding
ten-run set was finally recorded — fourteen cold runs with the lane's four, no red, confirming the
retraction's own diagnosis that the cause had been the environment.

**Q446 row 13 re-checked.** Stale for three of four modules: all four log replay on their controllers'
outcome lines today, and R-2 measured Returns a day before that line existed. MOD-0187's half was real —
`ClaimContextMiddleware` had zero log calls — and is closed.

**Two things CT could not do and recorded instead.** `:199`/`:200` need a `Program.cs` pipeline line that
is hash-pinned as *Accepted composition* in two packs; CT edited it without authority, measured that the
fix works and changes nothing in production, then reverted. `MOD-0183`'s four open lines need an owner
design decision (Q420) and a warehouse producer (MOD-0178, W-3, no pack).

## 5. Owner decision queue, current — two of these are dated

| # | decision | why now |
|---|---|---|
| 1 | **Q399 entitlement bundling** | Q357's grant expires **2026-11-03**. Six cells — rows 1 and 8 of MOD-0185, 0186, 0187 — fall on that date. **22 days.** |
| 2 | **Q414** `Loads:ReferenceBaseUrl` into the tracked `appsettings.Development.example.json` | One line. Q446: without it "the golden flow fails in **every** environment built from the repository" |
| 3 | **Push** the nine local commits | REP-001 §3: a local-only branch is not evidence |
| 4 | **Row 3 ruling** (Q397/Q400) for 0186/0187 routing | A CT ruling was assigned **2026-09-27** and has never been made |
| 5 | **Row 10 waiver** for `evidenceReferenceIds`, 0186/0187 | Under a waiver the cell reads **CLOSED UNDER WAIVER**, not MET (MOD-0183:364 precedent) |
| 6 | **Integration-owner release** for the `Program.cs` capacity branch + re-pin of both composition hashes | Closes MOD-0192 `:199` and `:200`; the measurement is already done |
| 7 | **MOD-0183:430** — should `Forbidden → Unavailable` surface as 503 at all? | The only thing left on that line |
| 8 | **MOD-0184 runtime-write release** | `:376` blocks row 13's pack-required signals |
| 9 | **Q15** integration execution in one checkout | Unchanged from v9.2; still the Stage 2 gate |

## 6. Two tracks that are not MVP-6 but touch it

The Treasury work package and its plan are **not in this repository** — the owner holds them, and CT's
copies sit outside the repo, which REP-001 §3 excludes from counting as evidence. They are referenced here
only for the one place they touch MVP-6, and the measurements below were taken in this repository.

| track | what | MVP-6 intersection |
|---|---|---|
| **B · MOD-0220 LE relationship model** | recovery / `Diten.MdmService`, owner `mdm-domain-team`, parallel to Treasury | **Real.** `lookup-validation` has **five** production consumers, and one is MVP-6's `MdmLegalEntityScopeValidator.cs`, whose seam `ReturnReferenceReader` and `ClaimReferenceReader` also use. If the endpoint becomes relationship-aware and starts answering 404 for a legal entity with no approved relationship, MVP-6's shipment, returns and claims flows begin rejecting entities that work today. The backward-compatibility test must cover five consumers in four services, not the three the owner named. |
| **D · MOD-0003 Data Contract Registry** | recovery, `platform-shared-services` / `Diten.Platform`, final phase, timing is the owner's | **Almost none.** Only MOD-0184 declares it, as *"opaque reference validation only"*, and `SupplyChainService/src` has **zero** references. MVP-6 can finish without it. It gates four Treasury modules, not MVP-6. |

Neither is CT's to build: MOD-0220 has a named owning team, MOD-0003 is under DCP-004's approved scoped
deferral and `Program.cs`-class authority.

## 7. Measures

Unchanged from v9.2 §6 in method. The figures there (delivered 1,452 h / 51.1 %, CT-accepted 1,128 h /
39.7 %) are **not** restated here: they predate every record since 2026-09-26 and REP-001 §2 forbids a
percentage without its row list. Effort is updated only at writer complete, independent VER and CT
decision, and no such event has been recorded since.
