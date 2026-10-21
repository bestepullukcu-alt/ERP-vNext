# MVP6 development plan v9.6 — 2026-10-17

Supersedes v9.5 (2026-10-13). Everything here is measured; where a number moved, the lane that moved it
is named so the claim can be checked rather than believed.

## 1. What changed since v9.5, and what v9.5 could not know

Two merges landed, run by the owner in the primary checkout because `git merge` was refused to CT for
the whole session: **`681bb44d3`** (the lane branch) and **`1b3408c2b`** (`origin/main` `3912d568b`,
PR #146, **0 conflicts**). The branch is now 0 behind / 29 ahead of `origin/main`.

**v9.5's §8 is stale in its own terms.** It recorded "1 commit, nothing since" and "10 sealed lanes";
there are now 29 commits ahead of main and the sealed-lane count was itself miscounted — see §6.

**One claim of v9.5 was wrong and is corrected here.** §6.1 reported the SupplyChain `JwtSettings` fix
as landed. It was not: the edit sat uncommitted in a worktree, `Program.cs:46` still threw, and the
service could not boot from a clone. Committed as `4d3f67f72` with the secret left **empty** by design
— propagating the sibling value would have been a fifteenth copy of the digest the 2026-10-05 decision
classifies as class B, *"sanitised in the merge result"*.

## 2. Measured state — the gate, and the number that actually matters

| | MET | NOT MET | NOT MEASURED | |
|---|---:|---:|---:|---|
| **published — `origin/main`** | **37** | 9 | 10 | **66.1%** |
| local, unpushed | 43 | 4 | 9 | 76.8% |

v9.5 and every CT report since have quoted 43/56. **37/56 is what the programme has.** The six-cell
difference is four `MOVED.tsv` overlay lanes and **all four have 0 files on `origin/main`**; REP-001 §3
excludes a branch that exists only locally from counting as evidence at all. Nine of this session's
lanes are in the same position.

The row lists for both columns are in
`docs/records/audits/2026-10/ct-discipline-and-readiness-audit-2026-10-17-01/evidence/gate-published-vs-local.txt`,
because §2 forbids a percentage written without them.

**One push closes that gap.** It is the highest-value single action available and it is the owner's
(GIT-002 §5).

## 3. The merge completion gate — six of seven

`docs/records/audits/2026-10/mvp6-main-merge-completion-gate-01/` · 11 sealed artefacts, 0 untracked.

| item | verdict |
|---|---|
| 1 conflicts resolved | PASS — 0 across both merges |
| 2 zero known secret material | PASS — the one new secret-bearing file arrived with `Secret` empty |
| 3 Platform / Auth regression | PARTIAL — Auth 2817/2817; Platform 8645 passed / 3 failed / 2 skipped of 8650 |
| 4 gateway validation | PASS — 244/244 |
| 5 frontend baseline | FAIL — 24 red of 5195; exactly one was the merge's, and it is closed |
| 6 five SupplyChain golden flows | **NOT RUN** — deferred by owner decision, on disk |
| 7 required MVP-6 gates | PASS — SupplyChain 529/1/530, identical to pre-merge |

**No integrated PASS is claimed** (K18). The merge touched **0** files under
`services/Diten.SupplyChainService`, and its 32 frontend files are all MOD-0188 plus the seven
`SharedResource.*.resx`.

Of Platform's three reds, two are pre-existing E-signature authorization rows and one is a 4-second
wall-clock bound that passed 3 of 3 alone — none merge-attributable, each measured rather than assumed.

## 4. The product is already on main — only the evidence layer is not

Worth stating because it reframes "readiness": `origin/main` carries **373 of 384** SupplyChain service
files and **all nine** module packs, with identical status for 7 of 9. The bounded backend cores reached
main through PRs; commits `4a8d4d4b3` (2026-09-16) through `f405cc582` (2026-10-09) are shared
ancestors.

Local-only is **29 commits**, of which **4 files are production**: `ClaimContextMiddleware.cs`,
`Program.cs`, `appsettings.Development.json` and `scripts/test-env/mvp6-test-mongo-env.sh`. Everything
else is 90 audit paths, 15 plan paths and 11 test files.

**So no module waits on a merge to exist on main.** What waits is the evidence, the two test files two
MET cells rest on, and the Q495/Q492 fixes — which means from `origin/main`'s point of view the F1/F3
reproducibility defect and the boot defect are **unfixed**.

## 5. Owner queue

| # | decision | state |
|---|---|---|
| 1 | **Push the branch** | unblocks §2: 37/56 → 43/56, and turns this session's measurements from E0 into evidence |
| 2 | **Q496** — one write giving the lane account the 25 `supplychain.*` keys | nine of thirteen open cells; 43/56 → **52/56** |
| 3 | **Q484** — the Capacity context middleware re-pin | CT refuses the release's re-pin; recommends the test-host option. Unchanged |
| 4 | **MOD-0192 `:199` / `:200`** | integration-owner authority for two `Program.cs` lines plus a re-pin of both packs' composition hashes |
| 5 | Rows 3 gateway explicit routes | decided 2026-10-05, sequence gate open since 10-10, **still nobody assigned** |
| 6 | **Q322** — the four UI pack revisions | proposed and sealed; needs owner approval per pack |
| 7 | **Q498** — `GET /shipments` under-declares; `GET /claims` omits 404 | contract edit, inherited from 3.2.0. §7 adds a sibling finding |
| 8 | **MOD-0184 runtime-write release** (`:376`) | unchanged |
| 9 | **Q15** integration execution in one registered checkout | unchanged, still the Stage 2 gate |
| 10 | **The SupplyChain CI row** — two deny rules, or copy five prepared files | `docs/roadmap/plans/mvp6-supplychain-ci-row-01/` |
| 11 | **BL-650** — CT decision D's intent vs what its guard measures | three options, all cheap |

## 6. Discipline, and a rule that cannot be obeyed

`docs/records/audits/2026-10/ct-discipline-and-readiness-audit-2026-10-17-01/`

The consequential finding is not CT's conduct. REP-001 §6 requires a front-matter künye on every CT
decision record *"for the script, not for humans"*, and §7's script produces §2's mandatory table from
it. Measured: **1202 record files, 0 carry it**, and the script's default glob reaches
`audits/*/*.md` (309 files) while lane records live at `audits/*/*/*.md` (764). The script therefore
prints `kayıt yok` for every module and `CT karar kaydı olan 0`, against the pushed ref and the working
tree alike.

**§2's table has never been producible by its own instrument.** That is why every status report in this
programme, CT's included, has been hand-written — which §2's first line forbids.

CT's own findings, four of them repeats of 2026-10-12: nothing pushed (so everything is E0), run output
recorded as prose rather than trx/log/json, the readiness percentages hand-computed in the wrong
columns, and GIT-002 §3's two-stage staged check skipped for 8 of 9 commits. Two improvements worth
keeping: the commit gate was satisfied by an explicit standing instruction, and a permission denial was
reported rather than routed around — which is exactly what went wrong on 2026-10-12.

**v9.5's "10 sealed lanes today" was also a miscount**, of the same family: CT counted `#` annotation
lines inside `ARTIFACTS.sha256` as artefacts. The five Stage R lanes hold **35** sealed artefacts, 9
of which were already tracked and **26** of which were the real gap — not the "48 hashed / 39
untracked" CT reported twice.

## 7. Contract conformance — measured, and one gap

`mvp6-sandop-capacity-conformance-01` and `-02` closed the static half of MOD-0190 `:201` and MOD-0192
`:201`: **12 of 12 operations** implemented, **22 of 22** declared error codes present, **11 of 11**
response and event schemas field-exact, `contractVersion: "v1"` on every envelope, and both context
middlewares matching the annex's `401 → 403 → 400` ordering.

One gap: **500 `INTERNAL_ERROR`** is reachable from `SandopRepository.cs:91` and the contract declares
no 500 on any of the twelve operations. Nine such last-resort sites exist across the service. Same
family as Q498 — the contract narrower than the behaviour — and an owner contract edit.

The job MOD-0192 `:201` names as the way to close itself, `g5_live_conform.py`, **does not exist in the
repository**; the pack line is its only occurrence.

## 8. Measures

| | |
|---|---|
| gate cells MET, **published** | **37 of 56** |
| gate cells MET, local and unpushed | 43 of 56 |
| cells one authorization away (Q496) | 9 |
| cells with the integration owner | 2 |
| cells stale in the TSV only (rows 10, waived 2026-10-05) | 2 |
| open acceptance lines, seven logistics packs | 61 of 129 |
| modules at `Release Done` | **0** |
| modules with a künye-backed CT decision record | **0 of 9** |
| commits ahead of `origin/main` | 29 |
| commits the pushed MVP-6 branch is behind main | **1012** |
| new backlog rows this session | 5 (BL-646…BL-650) |
