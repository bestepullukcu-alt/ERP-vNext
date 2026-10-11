<!-- verify from: this folder -->
# Q425 — Vitest/jsdom baseline: is the suite reproducible, and does it guard what we need guarded?

    lane      : Q425 (ledger Q384) — READ-ONLY INSPECTION
    agent     : read-only-auditor
    authority : owner decision 2026-10-04 (approve inspection lane)
    branch    : feature/mvp6-logistics
    measured  : 2026-10-04
    verdict   : PASS
    suite class : B — REHABILITATABLE (bounded, named set)
    recommendation : 2 — rehabilitate, then activate

## The answer in three sentences

The declared state reproduces exactly: `npm ci` from the committed lockfile exits 0 and
vitest 2.1.9 runs on Node 25 without an engine complaint, so the one real compatibility
unknown is closed — **it is not finding D**. The suite is 153/167 files and 2571/2597 tests
green in 45.91s, with zero collection failures and zero unhandled rejections; the 26 failures
fall into **six named clusters across 14 files**, none of which is infrastructure.

But on the second axis the suite fails the question it was asked: it has **no direct
behavioural coverage of any of the five modules**, and it **would not have caught Q419**.

## Preflight verification

| Preflight claim | Result |
|---|---|
| `package-lock.json`, lockfileVersion 3, 149 packages | TRUE — 149 `packages` keys. Note: `npm ci` installs 102 (audited 103); the remainder are the root entry and other-platform optional binaries. Both numbers are correct and describe different things. |
| lockfile dated 2026-07-07 | TRUE by file mtime (2026-07-07 20:43). The last commit touching it is 2026-06-24 (`01859b79f`). Mtime, not provenance. |
| `npm ci` IS possible | TRUE — exit 0, 9s. |
| node v25.5.0, npm 11.8.0 | TRUE. |
| vitest.config.js correct (jsdom, `tests/**/*.test.js`, globals, restoreMocks, clearMocks) | TRUE, verbatim. |
| `"type": "commonjs"` + `module.exports` config, no ESM/CJS mismatch | TRUE. |
| 167 test files | TRUE. |
| 2296 it/test blocks | **FALSE.** Static count is 2356 (`\b(it|test)\s*\(`); runtime count is 2597, the difference being `it.each` expansion. 2296 is not reproducible by either method. |
| 116 files touching document/window | **FALSE** — 130. |
| 0 files importing jsdom directly | TRUE. |
| node_modules absent | TRUE at entry. |
| `run_phase1_gates.sh` runs exactly three dotnet test projects, no npm/npx/node | TRUE — lines 30, 33, 42; zero matches for npm/npx/node/vitest. Note line 42 is `frontend/Diten.Web.Tests` — a .NET frontend project. CI does test the frontend, just not this suite. |
| vitest 2.1.9 predates Node 25 → possible finding D | **RESOLVED, NOT A FINDING.** The engine starts and completes. |

CT's framing — "if the configuration is correct, why has it never run?" — is answered by the
evidence below: nothing prevents it from running. It was never wired in, and in the interval
the ratchet guards went red unobserved.

## Run

    npm ci        exit 0   102 packages added, 9s
    npm test      exit 1   (= vitest run, declared script, unmodified)

    Test Files    14 failed | 153 passed (167)
    Tests         26 failed | 2571 passed (2597)
    Duration      45.91s (transform 2.40s, collect 6.13s, tests 70.72s,
                          environment 130.00s, prepare 22.34s, setup 0ms)

Collection/startup failures: **none** — all 167 files collected and executed.
Unhandled rejections: **none**. The single "unhandled" string in the log is a test *name*
(`workcenter-next-detail-page` > "a failed click is reported instead of vanishing into an
unhandled rejection"), not an event.
Console noise: 577 `stderr` lines across 42 files, dominated by deliberate WorkCenterNext
missing-resource warnings that the tests themselves provoke. Not failures.
`npm ci` reported 7 vulnerabilities (3 moderate, 3 high, 1 critical). Recorded as observed
state only — **no `npm audit fix` was run** and no declared dependency was mutated.

## Failure clusters

Six clusters. `setup 0ms` and a clean collection are what rule out the infrastructure class
for every one of them.

### C1 — CRM UI contract tests assert source text that was never written · 11 failures
Files: `campaign-targeting-admin-ui.test.js` (7 of 27), `consent-preference-admin-ui.test.js` (4 of 31)
Representative: `expected '/**\n * MOD-0165-FU04 Campaigns — Dat…' to contain 'fetch(\'/CRM/Campaigns/api/contract\''`
These read application source as a string and assert substrings. The received text shows the
sources carry **MOD-0165-FU04** while the tests are titled **MOD-0165-FU05**; both test files
landed in one commit (`0f71a237c`, 2026-08-28). The expectations describe a follow-up that
does not exist in the source.
Class: **stale fixture/expectation — test written ahead of the implementation.** Not a regression.
Confidence: high.

### C2 — ratchet/debt guards that are correctly red · 5 failures
Files: `dialog-one-implementation.test.js`, `wcn-dialog-one-language.test.js`,
`diten-field-icons.test.js`, `diten-tags.test.js`, `global-confirm-input-type.test.js` (1 each)
Representative: `a dialog was opened without window.showConfirm — … expected [ 'PPM/Initiatives/index.js' ] to deeply equal []`
These are the pinned-debt guards. Every one is a **true positive at HEAD**:
- `PPM/Initiatives/index.js` (committed, not dirty) opens a raw `Swal.fire` and declares the
  dialog l10n package a sixth time — it trips two guards independently.
- `diten-field-icons` names **5 offcanvas offenders, all five of them SupplyChain** (see second axis).
- `global-confirm-input-type` counts 11 input-box callers against a pin of 8.
- `diten-tags` reports the SharedResource gap grew past its pin of 33. **Measured at HEAD and
  in the worktree independently: max gap = 34 in both.** The in-flight resx edit adds 4 keys
  symmetrically to all seven locales and does not move the metric, so this failure is not an
  artefact of the dirty worktree.
Class: **application regression**, already shipped and unobserved.
Confidence: high.

### C3 — stale fetch mock · 4 failures
File: `strategy-apis.test.js` (4 of 4)
Representative: `TypeError: response.text is not a function`
`wwwroot/assets/js/pages/enterprise-strategy/strategy-apis.js:18` does
`const rawText = await response.text();`, while the test mock (lines 17–20 and the per-case
mocks) resolves `{ ok: true, json: async () => … }` with no `text`. The application moved to
reading the raw body; the mock did not follow.
Class: **stale mock.** Remediation touches one file.
Confidence: high.

### C4 — register rows render empty · 2 failures
Files: `planning-cycles-register.test.js`, `strategy-periods-register.test.js` (1 each)
Representative: `expected [] to have a length of 3 but got +0`
Both assert a 3-row table and a goal-style 3-dot action menu; both get zero rows. Identical
signature, one shared rendering path. Both files date to the 2026-04-15 baseline commit.
Class: **unknown** — either a shared render regression or a fixture that no longer seeds rows;
distinguishing them requires reading the render path, which is implementation work this lane
does not do.
Confidence: medium.

### C5 — owner-position API fallback · 2 failures
Files: `planning-cycles-owner-position.test.js`, `strategy-periods-owner-position.test.js` (1 each)
Representative: `expected false to be true` on "falls back to API position list when
company-scoped positions are unavailable"
Same assertion text in both, one shared fallback behaviour.
Class: **unknown** — shared behaviour change or stale mock.
Confidence: medium.

### C6 — two single-file assertions · 2 failures
- `objectives-edit-hydration.test.js`: `expected 'Growth' to be 'Transformation'` — edit
  re-hydration picks the wrong option. Class: **unknown**, leaning application regression. Confidence: medium.
- `pvg-case-intake-triage-ui.test.js`: `expected '@using Diten.Web.Views.Pharmacovigila…' to
  match /id="skeleton-loader"/` — the view exists but lost (or never had) the skeleton loader
  id. Class: **stale expectation or regression**, undetermined. Confidence: low.

**No cluster is infrastructure, config, missing-jsdom-capability, import/module-resolution or
timing/flakiness.** That is the single most important structural result: nothing stands between
this suite and a green run except the clusters above.

## Findings beyond the preflight

**A · Four test files can never run.** `tests/js/*.test.mjs` — `ppm-initiative-localization`,
`ppm-initiative-contract`, `ppm-gate-l-contract`, `ppm-add-new-delegation` — are `.mjs`, and
`include: ["tests/**/*.test.js"]` does not match them. They are invisible to every run, including
this one. 167 is the count of *reachable* files; 171 test files exist.

**B · A polyfill was written and never wired in.** `tests/vitest-crypto-polyfill.cjs` exists;
`vitest.config.js` declares no `setupFiles` (`setup 0ms` confirms it at runtime), and the only
reference anywhere in the repo is a hash line in
`docs/roadmap/plans/mod-0183-root-uptake-recovery-01/baseline.json`. Someone hit a crypto gap,
wrote the fix, and it never reached the config. Relevant because the five modules' create paths
call `crypto.randomUUID()`.

**C · The guards have been red at HEAD, not just in this worktree.** C2's l10n gap was measured
at HEAD (34) and in the worktree (34) against a pin of 33. The debt grew in some earlier commit
and no one was told, because nothing runs this suite. This is the concrete cost of the status quo.

## Second axis — the five modules

The UI surface exists: 12 JS files under `wwwroot/assets/js/SupplyChain/` and 28 views under
`Views/SupplyChain/` covering all five modules.

| Module | Direct behavioural coverage | Indirect / shared coverage |
|---|---|---|
| Shipments | **NONE** | yes — and currently RED (2 offcanvas) |
| Carriers | **NONE** | yes — and currently RED (2 offcanvas) |
| Loads | **NONE** | yes — and currently RED (1 offcanvas) |
| Returns | **NONE** | yes — green |
| Claims | **NONE** | yes — green |

Direct coverage is zero, measured three ways: no test file is named for shipment/carrier/load/claim;
the only filename matching "return" is `wcn-returned-signal.test.js`, which is the WorkCenter task
`Returned` status and not MOD-0186; and the only test file containing the string "supply-chain" is
`tasks-form-checklist.test.js:302`, where it is a security metaphor in a comment. **No test reads
any `SupplyChain/**` JS or view by path — the matching set is empty.**

Indirect coverage is real and non-trivial. Four guards walk `Views/` and `wwwroot/assets/js/`
recursively and therefore do reach these files: `dialog-one-implementation` (raw `Swal.fire`),
`diten-field-icons` (`.diten-field` wrapper), `wcn-dialog-one-language` (one l10n package),
`global-confirm-input-type` (no `type` named by callers). Replicating the `diten-field-icons`
detection exactly — its own walk, control filter and `KNOWN_NO_ICONS` list, census 40, debt 29 —
yields all five offenders inside the new logistics UI:

    SupplyChain/Carriers/_ChangeStatusOffcanvas.cshtml
    SupplyChain/Carriers/_CreateEditOffcanvas.cshtml
    SupplyChain/Loads/_CreateEditOffcanvas.cshtml
    SupplyChain/Shipments/_PodOffcanvas.cshtml
    SupplyChain/Shipments/_TransitionOffcanvas.cshtml

So the suite is already telling us something true about three of the five modules, and has been
telling it to no one.

### Would this suite have caught Q419?

**No.** By inspection of what the tests read:

1. No test reads any SupplyChain JS or view by path — measured, empty set. There is no assertion
   on Shipments' create behaviour of any kind.
2. The four guards that *do* reach those files inspect dialogs, field wrappers, l10n package
   declarations and confirm input types. None of them reads request headers, and none reads the
   scope at which an identifier is minted.
3. The defect's shape is a scope question. In the current `Shipments/create.js`, `intentKey`
   (line 9) and `intentRoot` (line 14) are minted in the module closure — once per form instance —
   with the `submit` handler at line 72 inside that same closure. The Q419 defect was these mints
   living inside `submit`. **Nothing in the suite distinguishes those two placements.**

The sharp part: this is a failure of aim, not of capability. Nine test files already assert
correlation/idempotency behaviour — `global-products-register`, `lsku-register`, `gsku-register`,
`product-abbreviation-register`, `finished-good-draft-foundation`, `hcm-employee-draft-wizard`,
`mg-process-modeling-editor`, `mg-process-modeling-datatable-v2`, `dynamic-module-menu-never-silent`.
The suite can express exactly the assertion Q419 needed. It was simply never pointed at Shipments.

This is also why the first axis cannot decide the question alone: a fully green run of this suite,
today, would leave Q419 and its whole defect class undetected.

## Classification and recommendation

**Class B — REHABILITATABLE (bounded, named set).**
Not A: the declared script exits 1. Not C: 99.0% of tests pass and the failures carry meaning
rather than noise. Not D: the declared state reproduced exactly, and the Node 25 compatibility
risk did not materialise.

**Recommendation 2 — rehabilitate, then activate.**
Activating now (option 1) makes CI red on commit one, including C1, which is an expectation for
unbuilt work rather than a defect. Retaining it non-gating (option 3) is the status quo, and the
status quo is precisely what let the C2 ratchets go red unnoticed — a guard nobody reads teaches
the reader to ignore guards. Deprecating (option 4) would discard a suite that is 2571 tests green
and already carrying true findings about the logistics UI.

Remediation scope, as files and clusters:
- C3 — 1 file (`strategy-apis.test.js`); add `text` to the mock.
- C1 — 2 files; decide whether MOD-0165-FU05 / MOD-0164-FU03 are to be built or the expectations retired.
- C2 — 5 guards, 2 application files (`PPM/Initiatives/index.js`, the SharedResource set) plus the
  5 named SupplyChain offcanvas views. These are fixes to the product, not to the tests.
- C4, C5, C6 — 6 files, root cause undetermined; each needs its render/fallback path read.
- Finding A — 1 config line decides whether 4 `.mjs` files join the suite.
- Finding B — 1 config line decides whether the crypto polyfill is live.

**Carried with the recommendation:** activation protects the five modules only indirectly. Closing
the Q419 class requires new direct tests for Shipments, Carriers, Loads, Returns and Claims. That
is a separate scope and this lane does not estimate or schedule it.

## Read-only compliance

    worktree entries before : 59
    worktree entries after  : 60   (= 59 + this record directory, and nothing else)

The single new entry is `docs/records/audits/2026-10/mvp6-q425-vitest-baseline-01/` — this
record, which `git status --porcelain` counts as one untracked directory. **No other repo path
was changed, created or deleted by this lane, and no existing path was modified at all.** No application source, test source,
`package.json`, lockfile, CI script or gate script was touched; nothing was staged, committed,
reset, cleaned or stashed; no config was altered to improve any number. `node_modules/` was created
by the single permitted `npm ci` and is ignored by `.gitignore:11`, so it does not appear in the
worktree census — verified, zero `node_modules` entries in `git status --porcelain`.

`SOURCE-AS-MEASURED.sha256` is included although no path changed. It records the bytes that produced
these numbers, which matters here because the measurement was taken against a **worktree carrying 59
entries of another lane's in-flight work**. Per K6 it is a record, never `shasum -c`'d.

One diagnostic deviation, disclosed: to recover an offender list the reporter had truncated, I first
tried `npx vitest run tests/diten-field-icons.test.js --diff.truncateThreshold=200`. vitest 2.1.9
rejects that flag with a CLI parse error. I did not pursue it, changed no config, and instead
replicated the guard's own detection in a scratchpad script outside the repo.

**Verdict: PASS.** Both questions are answered with evidence; nothing was blocked.
