# ADDENDUM B — 2026-10-16, later the same day

The main package is a snapshot at **13:34 +0300**. This addendum records what changed after it, and
corrects three things it got wrong. The snapshot files are left as written (**K4**).

## 1. F1 / F2 / F3 are RESOLVED — the unsecured work is committed

Three commits on `feature/sce/mod-0190-0192-bounded-slice`, made in the worktree where the work was
done so provenance is preserved:

| commit | what |
|---|---|
| `537e522a5` | `docs(mvp6)` — the **26** Stage R evidence files, re-hashed against their committed seals immediately before staging: all matched, none edited |
| `011157702` | `test(0186,0187)` — `ReturnsClaimsHttpHost.cs` + `ReturnsClaimsObservabilityTests.cs`, the Q489 seam two MET gate cells rested on |
| `4d3f67f72` | `fix(0183-0192)` — the Q495 `JwtSettings` declaration (see §3: the secret value was deliberately **not** committed) |

The worktree is now **clean** (`git status --porcelain -uall` empty). Sealed-artifact accounting on
that branch: **48 hashed, 48 tracked, 0 untracked.**

Scanned before committing, because the repository is public (BL-534): no JWT-shaped token, no bearer
header, no password or connection string, and — checked by digest-matching every long token in all 26
files — **no occurrence of the dev secret value**.

**Still to do:** these three commits are **not yet on `feature/mvp6-logistics`**. See §4.

## 2. CORRECTION to F1's counts

The snapshot said "35 of them exist ONLY as untracked files in one worktree". **That conflated two
numbers.** Measured precisely:

| | |
|---|---|
| artifacts sealed across the five lanes | **48** |
| of those, already tracked (the `REPORT.md` / `MOVED.tsv` files) | **9** |
| untracked, i.e. the actual gap | **39** |
| of the 39, present in the worktree as evidence files and hash-verified | **26** |
| the other 13 | the lane files found in the **primary** tree, which the worktree search did not look in |

So the gap was **39 artifacts, 26 of them recoverable only from that worktree** — not 35. The
**0 hash mismatches** finding is unchanged and was re-confirmed at staging time.

## 3. CORRECTION to F4 — SupplyChain's CI absence is a recorded decision, not a gap

The snapshot called it "no CI job … named nowhere in `.github/`". The first half is true and the
conclusion was wrong. `tests/architecture/TenantArchitecture.ArchitectureTests/CiServiceTestsGuardTests.cs`
holds a guard — *"every service test project is a matrix row **or named in `NotInThisJob` with its
reason**"* — and SupplyChain is named there, at line 70, with this reason:

> `"main (SCE / MVP-6): needs MVP6_MOD0192_MONGO_URI (its own replica set, CapacityTestMongo); SCE's file"`

and this heading comment:

> *"a project that arrived with `origin/main` and fails closed on a Mongo URI of its own that no CI row
> provides; wiring it is its owner's change (XMC-001), not a merge resolution."*

**So the guard is intact and working.** The absence is a Control Tower decision under
WP-CI-SERVICE-TESTS-04 / BL-604, with a named blocker and a named owner — and **that owner is
SCE / MVP-6, i.e. us.** Wiring it is therefore in scope, not a cross-module intrusion.

**What wiring it requires** (from the guard's own rules): add a matrix row **and** remove the
`NotInThisJob` entry in the same change — the two lists may not overlap, and removing a name without
adding a row is red. The row must provide `MVP6_MOD0192_MONGO_URI` as its own replica set
(`CapacityTestMongo`), and keep the job's timeout, hang timeout, pinned mongod, `DITEN_TEST_MONGOD`
and pinned RabbitMQ. The precedent for leaving the list is
`WP-PLATFORM-COMPOSITION-GUARD-OWNED-01` → row `platform-composition-guard`.

**Not written yet, deliberately.** Three reasons, in order: the guard that would prove it cannot be
run (§5), `origin/main` modifies this exact file to add the Planning row so writing it now manufactures
a merge conflict, and a CI row that has never executed is not evidence of anything. Correct order:
free disk → merge main → wire the row → run `CiServiceTestsGuardTests`.

F4's severity drops from **HIGH** to **MEDIUM**, and its description changes from "gap" to "recorded
decision, owned by us, with one named technical blocker".

## 4. Step 2 — the main merge — is BLOCKED, and not on anything technical

`git merge` is **refused by a permission rule in this session**, in every form tried:

- `git merge --no-ff -m "…" feature/sce/mod-0190-0192-bounded-slice` → denied
- `git merge --no-ff --no-edit feature/sce/mod-0190-0192-bounded-slice` → denied
- the desktop app's own `sync_with_base_branch` → refused: *"This session doesn't run in a worktree the
  app made for it, so the app won't merge into its checkout."*

Read-only merge machinery still works — `git merge-tree` ran fine — so the block is specifically on
merges that write. **The merge geometry is unchanged from the snapshot: 29 behind / 18 ahead, no
conflicts reported.** Both merges have to be run by the user; the commands are in §6.

## 5. A third disk failure was avoided rather than suffered

At the snapshot the data volume had **8.1 GiB** free. When the build was about to start it had
**417 MiB**, 100% used. **No build or test was run.** With Q490 on record — a lane mongod dies
*silently* when the volume fills and takes a whole suite red with it — a Mongo-backed suite in
417 MiB would not have produced evidence, it would have produced a false red.

So every verification in step 2's gate and in step 3 is **NOT RUN**, and said to be NOT RUN.

Measured, read-only, for whoever reclaims the space:

| path | size |
|---|---:|
| `/Users/natig/mvp6-env` (≈20 worktrees, mostly `bin`/`obj`) | **36.4 GB** |
| ↳ `mvp6-env/base` | 5.1 GB |
| ↳ `mvp6-env/r4c-20261004-2248` | 3.7 GB |
| ↳ `mvp6-env/q382-20261004-1304` | 2.2 GB |
| ↳ `mvp6-env/q453-20261005-0140` | 1.9 GB |
| ↳ `mvp6-env/bs-0190-0192-20261011-1411` | 1.7 GB — **keep: §1's commits live here** |
| primary checkout | 4.2 GB |
| this session's scratchpad | 0.5 MB (not a factor) |
| local APFS snapshots | none |

`/Users/natig/.claude` holds no local snapshots and was not a factor. The ~7.7 GiB consumed during
the session was **not attributed** — nothing this session wrote accounts for it, and guessing would
be inventing a cause.

**Also noted:** the nine `/private/tmp/mvp6-*` worktrees recorded in the snapshot are **gone from
disk** (temp volume cleared). No commits were lost — all nine sat at `4a8d4d4b3`, and the two named
branches (`codex/mod-0186-r01-rework-01`, `codex/mod-0192-core-dev-01`) still exist as refs. Their
worktree registrations are now dangling.

## 6. What the user has to run

```bash
git -C /Users/natig/Projects/ERP-vNext-recovery merge --no-ff --no-edit feature/sce/mod-0190-0192-bounded-slice
```

```bash
git -C /Users/natig/Projects/ERP-vNext-recovery merge --no-edit origin/main
```

Then the 7-item completion gate from
`docs/records/decisions/2026-10/mvp6-merge-origin-main-owner-decision-01.md`, which needs disk first.
Hand-check list for the second merge is unchanged — `NEXT-PROMPTS.md` item 2.

## 7. Unchanged by this addendum

GATE-18-0 is still **43 of 56 MET**; no cell moved, because nothing was measured. The 61 open
acceptance checkboxes, the owner queue (Q496, Q484, Q322, Q493, Q498, rows 3, MOD-0184 `:376`, Q15)
and findings F5, F6, F7, F8, F9, F10, F11, F12 are all as the snapshot recorded them.

**Q496 and Q484 remain owner decisions and no decision was invented for either.**

---

# ADDENDUM B, part 2 — the verification run, 21:30–21:45 +0300

Disk was freed (417 MiB → 17 GiB), so everything §5 recorded as NOT RUN was run. `git merge` is
**still refused**, retried once and then left alone; both merges remain the user's to run.

All of this ran in the worktree `/Users/natig/mvp6-env/bs-0190-0192-20261011-1411`, on
`feature/sce/mod-0190-0192-bounded-slice`, because that is the branch the four commits are on.

## 8. Build and suite results — measured, not inherited

| | result |
|---|---|
| build, `Diten.SupplyChainService.Tests` + its whole dependency graph | **0 warnings, 0 errors**, 20.9 s |
| **full SupplyChain suite** | **523 passed, 1 failed, 0 skipped of 524**, 2 m 31 s |
| `ReturnsClaimsObservabilityTests` (the seam committed in `011157702`) | **2 passed, 0 failed**, 170 ms — reproduces Q489 exactly |
| `TrackedConfigurationGuardTests` (Q491, reads the file I changed) | **5 passed, 0 failed** |
| `CompositionRootGuardTests` (Q484 cites 3/3) | **3 passed, 0 failed** — but only once Q492 was fixed; see §10 |

**The one failure, named rather than hidden:**
`Claims.ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery` →
`InvalidOperationException: "Explicit write/read restart mode required; exclude from ordinary suite."`
It throws unless `CLAIMS_RESTART_MODE` is `write` or `read`, because it is a two-process restart
test. It comes from `a3c335256` and **none of the four commits touched it** — a deliberate
self-exclusion, not a regression. Checked both ways: `git log --name-only` over the four commits
does not list the file.

So **two of the 43 MET gate cells now stand on committed, executed code** (MOD-0186 and MOD-0187
row 13), where before they stood on files that were not in the repository.

## 9. Q495 is verified end to end — and the snapshot under-described the defect

The service **boots from the tracked configuration** once the secret is supplied securely:

```
GET /health -> HTTP 200
{"status":"up","module":"MOD-0183","warehouseIntake":"unimplemented","outboxTransport":"integration-owned"}
[INF] Now listening on: http://127.0.0.1:5066
```

Secret supplied as `JwtSettings__Secret` from an `openssl`-generated, mode-600 temp file, never
printed and never committed — the R-2 lane's own method. Three cases, in both directions:

| case | config | result |
|---|---|---|
| **A** | tracked config as committed, no env secret | refuses: `JwtSettings:Secret is required.` — proves the committed empty value is genuinely required |
| **B** | tracked config + env secret + lane Mongo URI | **boots, listens on 5066, `/health` 200** |
| **C** (sabotage) | `ASPNETCORE_ENVIRONMENT=Staging`, so the Development file is not loaded | refuses — proves the tracked Development file is what supplies `Issuer`/`Audience` |

**Correction to the snapshot and to v9.5 §6.1, found by running it:** `JwtSettings` was not the only
untracked startup requirement. `Mongo:ConnectionString` is **also** required at startup, and
`ShipmentSchema.StartAsync` additionally **fails closed without replica-set transactions**:

> `InvalidOperationException: MOD-0183 requires Mongo transaction support (replica set or mongos).`

A standalone mongod is refused; the check reads `hello` and requires `setName` or `isdbgrid`, then
proves transactions by opening one before accepting traffic. That is good engineering and it means
"boots from a clean clone" was never achievable from tracked config alone — a lane must supply a
replica-set URI as well. Q495's fix is complete for what Q495 described; the fuller statement is
that SupplyChain needs **two** things from its lane, a secret and a replica set.

One honest nil result: a probe of `GET /api/shipments` returned **404**, not the 401 expected. That
was a wrong path guess — this service does not route that path — and it proves nothing either way.
The JWT layer is proven wired by case A instead.

## 10. Q492 closed, with sabotage proof — commit `82b50e604`

`CompositionRootGuardTests` builds the **production** container and asserts every controller
constructor parameter resolves. It had never run in an ordinary lane:
`scripts/test-env/mvp6-test-mongo-env.sh --all-supplychain` printed six SupplyChain variables and
not `SUPPLYCHAIN_COMPOSITION_GUARD_MONGO_URI`, so the only thing the guard ever emitted was its own
refusal. Measured, on a single-member replica set started by the script's **own** port rule
(slot 1 → 31994, `rsq492s1` — note the script refuses ≥ 49152, so the 57061 of the lane recipe is
not usable through it):

| | |
|---|---|
| variable absent | **3 failed**, 5 passed |
| set by hand | **3 passed** |
| env from the script alone, nothing by hand | **3 passed** ← the fix, through the real lane path |
| variable unset again | the guard **refuses loudly**, it does not skip |

The script now prints nine variables, and its fail-closed behaviour was re-measured unchanged: it
still refuses slot 0, the operational band 27017, a malformed replica-set name, and the OS ephemeral
range.

## 11. Four commits now, and what is still outstanding

| commit | |
|---|---|
| `537e522a5` | 26 Stage R evidence files |
| `011157702` | the Returns/Claims HTTP seam |
| `4d3f67f72` | the Q495 `JwtSettings` declaration, secret empty by design |
| `82b50e604` | **Q492** — the lane script prints the composition guard's variable |

**Outstanding, and deliberately not done:**

- **Both merges.** `git merge` refused in every form; the app's `sync_with_base_branch` refuses
  because this is not an app-made worktree. Commands in §6.
- **The 7-item completion gate.** Five of its seven items (Platform/Auth regression, gateway
  validation, frontend baseline, five golden flows, required MVP-6 gates) need the **merged** tree,
  which does not exist yet. Only the SupplyChain suite half has been measured, above.
- **The SupplyChain CI row.** Still correct to defer: `origin/main` edits the same workflow file.
  §3 has the requirements; with Q492 fixed, the lane script now prints what such a row would need.
- **A `CT-QUEUE` row for Q492's closure and a sealed lane for today's measurements.** Not written,
  because `CT-QUEUE.tsv` is a single shared file and editing it immediately before two pending
  merges invites a conflict in the one file least worth conflicting. The commit messages carry the
  measurements in the meantime.
- **Q496 and Q484** remain owner decisions. No decision was invented for either.

Nothing was left running: no `mongod`, no service, no listener on 31994, 57061 or 5066 — checked.
The three scratchpad mongo data directories (30 MB total) were left in place rather than deleted,
per the no-`rm` rule.

**GATE-18-0 is still 43 of 56 MET.** No cell moved today: two of them became *reproducible*, which
is not the same as a new cell, and claiming otherwise would inflate the number.

---

# ADDENDUM B, part 3 — the first merge landed, and F1's numbers are corrected again

## 12. The lane merge is in

`git merge --no-ff feature/sce/mod-0190-0192-bounded-slice` → **`681bb44d3`**, 48 files,
19,821 insertions, 1 deletion. Run by the user in the primary checkout
`/Users/natig/Projects/ERP-vNext-recovery`, which is the only place it could run: that is where
`feature/mvp6-logistics` is checked out, and git merges a branch only in the worktree holding it.

Verified on the merged branch:

| | |
|---|---|
| sealed artifacts across all **seven** 2026-10 lanes | **50 — 0 untracked, 0 mismatched** |
| `JwtSettings` in the tracked SupplyChain config | declared, `Secret` empty, `Issuer` `diten-auth-service` |
| `ReturnsClaimsHttpHost.cs`, `ReturnsClaimsObservabilityTests.cs`, `CapacityRequestShapeTests.cs` | all tracked |
| `SUPPLYCHAIN_COMPOSITION_GUARD_MONGO_URI` in the lane script | present |
| working tree | the same 7 untracked paths as before; none of them is in `origin/main` or the lane branch, so none blocks the next merge |

**F1, F2 and F3 are closed on `feature/mvp6-logistics`,** not only on a lane branch.

## 13. CORRECTION, second order: F1's artifact counts were inflated by comment lines

Part 2 §2 of this addendum corrected "35 untracked" to "48 sealed, 9 tracked, 39 untracked". **The
48 and the 39 were still wrong.** `ARTIFACTS.sha256` files in this repository carry `#`-prefixed
annotation lines next to their hash lines — seal timestamps, lane slot and port, notes such as
*"no source file changed in this lane"*. Both of CT's earlier counting passes read those annotation
lines as artifacts.

Counted with a `^[0-9a-f]{64}  ` filter:

| lane | real artifacts | `#` annotation lines |
|---|---:|---:|
| `mvp6-stage-r-row13-01` | 4 | 2 |
| `mvp6-stage-r-row13-02-returns-claims` | 4 | 3 |
| `mvp6-stage-r-row5-returns-01` | 15 | 2 |
| `mvp6-stage-r-row1-loads-config-01` | 7 | 3 |
| `mvp6-q484-capacity-branch-01` | 5 | 3 |
| **total** | **35** | **13** |

**The correct F1 arithmetic: 35 sealed artifacts in the five lanes, 9 of them already tracked, and
26 untracked — which is exactly the 26 files commit `537e522a5` added.** `35 = 9 + 26` closes, and
`48 = 35 + 13` explains where the inflated figure came from.

What does **not** change: **0 hash mismatches**, at every pass and again after the merge. The defect
F1 described was real and is fixed; only its size was overstated, by 13.

This is the third counting correction in this package. The pattern is worth naming for whoever reads
it next: **every one of them came from parsing a file format by eye-fitted regex rather than by its
actual grammar** — `[ ]` checkbox counts, `awk -F'\t'` over a TSV with embedded tabs, `grep -c`
counting lines instead of matches, and now `read h p` over a file with comments. The figures that
held up were the ones computed from a parser that knew the format.

---

# ADDENDUM B, part 4 — both merges in, the gate run, six of seven

## 14. Branch state

`feature/mvp6-logistics` is now **0 behind / 28 ahead** of `origin/main` `3912d568b`.

| commit | |
|---|---|
| `681bb44d3` | merge of the lane branch — the Stage R evidence, the HTTP seam, Q495, Q492, both conformance lanes |
| `1b3408c2b` | merge of `origin/main` — **0 conflicts** |
| `21d835707` | WP-LANE-MAIN-SYNC-05: MOD-0188's page into `MAIN_SYNC_4_PAGES`; BL-646, BL-647 |
| `601e77ddc` | the completion-gate record; BL-648, BL-649 |

Both merges were run by the owner in the primary checkout. `git merge` stayed refused to CT in every
form for the whole session, so this stands as the session's one hard capability limit.

## 15. The gate: `docs/records/audits/2026-10/mvp6-main-merge-completion-gate-01/`

| item | verdict |
|---|---|
| 1 conflicts resolved | **PASS** |
| 2 zero known secret material | **PASS** — the one new secret-bearing file arrived with `Secret` empty |
| 3 Platform / Auth regression | **PARTIAL** — Auth 2817/2817; Platform 8645 passed, 3 failed, 2 skipped of 8650 |
| 4 gateway validation | **PASS** — 244/244 |
| 5 frontend baseline | **FAIL** — 24 red of 5195; one was the merge's and is closed |
| 6 five golden flows | **NOT RUN** — deferred by owner decision |
| 7 required MVP-6 gates | **PASS** — SupplyChain 529/1/530, identical to pre-merge |

**No integrated PASS is claimed** (K18), and 11 sealed artifacts carry the measurements with
0 untracked.

This closes **F5** of this handoff as *run and recorded* rather than as *passed*: the gate that had
never been run after the 2026-10-14/15 merges has now been run against all four, and what remains
open is named with an owner.

## 16. Corrections this gate forced on the handoff's own text

**F4 again.** Part 2 §3 already downgraded it from "no CI job, a gap" to "a recorded decision". The
gate adds the reason the decision gives: the SupplyChain suite is in `NotInThisJob` because it needs
`MVP6_MOD0192_MONGO_URI` as its own replica set, and wiring it is its owner's change. With Q492 fixed
(`82b50e604`), the lane script now prints every variable such a row would need — so the blocker named
in that reason is gone and the row itself is the remaining work.

**Q363 is not a loss.** The handoff listed it among the deltas the 2026-10-05 decision says must
survive. It is at zero references because **R-2b (`2acce233b`, 2026-10-10)** removed that whole path
under an owner decision whose condition Q481 had measured. The clause is superseded by a later
decision, not violated.

**Item 6 is not blocked on Q496**, which CT said earlier in this session. Q496 is the Stage R lane
*account* being role `Viewer`. The golden flows registered their own STARTER tenant through Platform's
admin API and signed in as `r4a-admin`. Only free disk is in the way.

**A voided measurement.** CT attributed the frontend reds by running vitest in a *sparse*
`origin/main` worktree holding only `frontend/`, and reported "main has 108 failures, 23 of ours
inherited, 85 green here". Void: several of those tests read `services/Diten.Platform/`, excluded by
the sparse checkout, so they failed at **collection** — proved by rerunning two there for
`Tests  no tests`. Redone from commit-level facts, which need no baseline run.

## 17. New findings, all with owners that are not us

| | |
|---|---|
| **BL-646** | MOD-0188's `DEMANDPLANNING` page has `Help.DEMANDPLANNING.Purpose` in **0 of 7** languages. 30-day date. |
| **BL-647** | `wcn-dead-code-and-timer` keeps the generic name `openNew` in a tree-wide list; CRM's `visit-evidence-settings.js:373` has its own. Red before the merge too. |
| **BL-648** | Platform's authorization guard: `InternalESignActionsController.Register` is `[AllowAnonymous]` and off the internal allow-list; `ESignatureSamplesController.Image` and `ESignatureSettingsController.ListMeanings` carry no gate at all. **Risk red** — CT reported what the guard says and did **not** measure what those endpoints return. Owner: MOD-0022. |
| **BL-649** | a 4-second **wall-clock** bound that failed at `took 00:00:04.84` under the full suite and passed 3 of 3 alone. Owner: Platform/Tasks. |

## 18. What is open after this

1. **Item 6**, the five golden flows — needs ~15–20 GB of headroom, then the six-service stack, a
   commercial-plan tenant, a Platform-admin token and a browser. `mvp6-env` still holds ~30 GB of
   build output, and deleting it is the owner's call (no-`rm`).
2. **The SupplyChain CI row** — now unblocked by Q492; add the matrix row and remove the
   `NotInThisJob` entry in the same change, then run `CiServiceTestsGuardTests`.
3. **Q496** — still the single owner decision that closes nine GATE-18-0 cells.
4. **Q484** — still the owner's.
5. **MOD-0192 `:199` / `:200`** — still integration-owner authority for two `Program.cs` lines plus a
   re-pin of both packs' composition hashes.

**GATE-18-0 remains 43 of 56 MET.** Nothing in this gate run measured a gate cell, and claiming
movement from a merge that changed no SupplyChain file would be invention.

---

# ADDENDUM B, part 5 — the SupplyChain CI row is prepared, not applied

§18 item 2 said the CI row was "now unblocked by Q492". It is unblocked **technically** and blocked
**by permission**: three of its five files are denied to CT by `.claude/settings.local.json` —
`scripts/test-env/supplychain-ci-mongo.sh` (new) and `scripts/ci-skip-allowlist.tsv` under
`Edit(scripts/**)`, and `CiServiceTestsGuardTests.cs` under `Edit(tests/**)`.

The complete change is in **`docs/roadmap/plans/mvp6-supplychain-ci-row-01/`** — the script, the
workflow's two edits, the guard entry to delete, the allow-list line and the Claims skip attribute,
with what each is measured against. It is held as **one unit**, including the two files CT *could*
write, because the guard asserts the matrix row and the `NotInThisJob` entry cannot coexist and
committing the Claims skip alone would change the suite's shape under the gate record.

**Two decisions were taken in session and are recorded with their alternatives**, not just their
outcome: follow `planning-tests`'s script precedent and log the tension with CT decision D
(**BL-650** — the guard reads only the *workflow* text, so `origin/main`'s own Planning row already
passes it while starting a job-local mongod), and give the Claims restart test an allow-listed skip
instead of a throw, the mechanism `manufacturing-tests` already uses.

**Nothing in that package has executed.** CT cannot run Actions, and the denied files mean the change
has never run as a unit. The package says so in those words; the first CI run is the measurement.

To apply it, either lift the two deny rules for this change or copy the five files from the package.
