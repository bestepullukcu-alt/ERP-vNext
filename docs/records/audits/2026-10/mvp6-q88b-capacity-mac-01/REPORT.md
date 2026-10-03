# Q88b — MOD-0192 Capacity Mac build + test + runtime: BASE-STACK v2 + Capacity overlay v3 (SOP v2.5 §17/§36.2)

**Agent verdict: FAIL.** One acceptance line fails: **"all runtime checks PASS in en + ar"**. Two checks fail, each in both
locales:

- **Q173 O-2** — an evaluation is shown under a scenario that does not own it, and a queued `evaluationId` is applied after a
  failed scenario load (all-real responses).
- **O-3** — after a 403 on *create plan* focus falls to BODY; after a 403 on *create scenario* / *evaluate* focus goes to the
  scenario section heading, not the page title.

Everything else passes: P1–P6; compose hash-exact (tree A 14,572); build 0 errors; **new test failures vs A = 0** with
`NavManifestL10nGuardTests` 7/7 on B; the MOD-0192 happy path, UI-PM-01/05/09/10/12, nav (sidebar + Ctrl+K) and Q173 O-1 in
en + ar with DB before/after evidence; NETCHECK 27017 = 0; FINAL-CHECK clean; SHA256SUMS present.

Separately, **F-Q88b-1**: the Capacity backend accepts plans, scenarios and evaluations only in one hard-coded fixture
tenant/legal entity, so no real tenant can use the module; the happy path ran only with lane-only test identities (D-2).

Agent PASS/FAIL ≠ CT ACCEPTED — returning to CT.

| Field | Value |
|---|---|
| WP / prompt / template | Q88b · Prompt Q88b v1 · T2 v1 · role T2 Mac build/test/runtime · CT-QUEUE row Q88b (scope v3; after Q184, Q185) |
| Where | Claude app → Code tab → Local session on the Mac, folder ERP-vNext-recovery (`uname -s` = Darwin). .NET SDK 8.0.417 / runtime 8.0.23, mongod 8.0.18, Playwright (Chromium, headless) |
| Repo | `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`, read-only. `GIT_OPTIONAL_LOCKS=0`; `git status --porcelain` only; no diff/add/commit/push |
| Base stack | BASE-STACK v2 `ce8d60ab959ec7018c255ab1152983cfd8e301390f54b3d0ba23961bf2c4d882` |
| Overlay v3 | `mvp6-capacity-ui-draft-03/capacity-ui-draft-overlay-v3.tar.gz` `5c0a3b61…96d5` · SHA256SUMS `997d8f9b…fd94` (4/4 OK) · FILE-PLAN v2→v3 `f1e66d0f…f939` · v2 FILE-PLAN `b12557cf…3ac9` |
| Pack / UI rules | MOD-0192 `ace0f58f…8f7d` · `.antigravity/agents/frontend-ui-ux.md` `90247ddc…f044` |
| Reference runs | `mvp6-q84b-sop-mac-01/` (`a324b35b…`) and `mvp6-q185-sop-mac-retest-01/` (`f0058664…`), read-only; copies adapted in `~/mvp6-env/q88b/` |
| Workspace | `~/mvp6-env/q88b/`: new folders only, nothing deleted, kept |
| Time | start 2026-10-01 14:58:36 +03 · end 2026-10-01 16:25 +03 |

## 1. Preflight (all PASS)

| # | Check | Result |
|---|---|---|
| P1 | `uname -s` | Darwin |
| P2 | `.git/index.lock` | absent; `GIT_OPTIONAL_LOCKS=0`; porcelain only |
| P3 | HEAD | `4a8d4d4b339528a88e6220fb8402e5a2c771136c` |
| P4 | 9 sha256 values + v3 SHA256SUMS | all equal the prompt values; `shasum -a 256 -c` 4/4 OK. The v1, v2 and v3 draft folders' SHA256SUMS: 0 bad lines |
| P5 | `docs/records/audits/2026-10/mvp6-q88b-capacity-mac-01/` | absent at start |
| P6 | other run / slot listeners | none. Slots 1–9: 0 listeners. Seven `MSBuild.dll /nodeReuse:true` processes were alive: idle worker nodes (parent pid 1, 0 % CPU) left by the Q185 kit's own service builds at 14:44 +03, not a build or test run. They were not touched (`logs/p6-idle-msbuild-nodes.txt`) |

## 2. Compose (`COMPOSE.tsv`, `logs/compose/`, `tools/compose_q88b.py`)

- **Tree A** `~/mvp6-env/q88b/treeA/src`: BASE 14,566/14,566 → Q117 → Q121 → Q131, preimages checked. **14,572 files, 0 mismatch / extra / missing.**
- **Overlay** (as Q84b/Q185): archives v1 `dd290891…`, v2 `48b60b78…`, v3 `5c0a3b61…` extracted to staging and checked against their members.
  - **v2 FILE-PLAN 9/9** (v1 member = `v1_sha256`, v2 member = `v2_sha256`).
  - **v3 FILE-PLAN 8/8** (v2 member = `v2_sha256`, **v3 member = `v3_sha256`**).
  - The other 39 v3 files are byte-equal to v2. 34 module files, all new tree paths (before = absent).
- **L6 environment integration** (environment copy only): `CapacityPlanningManifestProvider` line; 6 gateway routes (275 → 281); v3 nav keys × 7 (`Nav.Module.CAPACITYPLANNING`, `Nav.Page.CAPACITYPLANS`).
- **Tree B** `~/mvp6-env/q88b/treeB/src`: 14,606 files, 0 mismatch / extra / missing. **Kit K01 tree = tree B byte for byte** (0 differing paths).

## 3. Build (`BUILD-SUMMARY.tsv`, `logs/build/`)

| Tree | SupplyChainService.sln | Diten.Web | Diten.Web.Tests |
|---|---|---|---|
| A | 0 errors, 0 warnings | 0 errors, 15 warnings | 0 errors, 3 warnings |
| B | 0 errors, 0 warnings | 0 errors, 15 warnings | 0 errors, **4** warnings |

Warning sets: B = A plus **one** warning in an overlay test file — `frontend/Diten.Web.Tests/Forms/CapacityPlanFormContractTests.cs(121,9)`: xUnit2013 (use `Assert.Single`) (F-Q88b-4).

## 4. Tests (`TEST-SUMMARY.tsv`, `logs/test/`)

Environment only from each tree's `scripts/test-env/mvp6-test-mongo-env.sh --slot 5 --rs rsq88bs5 --all-supplychain`
(`DITEN_PLATFORM_TEST_MONGO_URI`, `MVP6_MOD0192_MONGO_URI` set by the script). Lane mongod `127.0.0.1:35994`, `enableTestCommands=1`.

| Suite | A | B | New failures B vs A |
|---|---|---|---|
| Diten.Web.Tests | 158/158 | **223/223** (+65 overlay tests) | 0 |
| SupplyChainService.Tests | 415/417 | 425/426 (+9 overlay tests) | 0 |

- `NavManifestL10nGuardTests` on B: **7/7 passed**.
- Known, not new: `ClaimReplayTests…DurableRecovery` fails on A and B (by design). `LoadAtomicityTests…UnknownCommitResult` failed on A only in this run (intermittent).

## 5. Runtime (`RUNTIME.tsv`, `browser/`, `png/` 78 files, `kit-a1/`)

- **Environment:** evidence kit v1.2, slot 5. Gateway 5500, Web 5501, Auth 5556, Platform 5557, MDM 5559, SupplyChain 5561, Mongo **35994** (`rsCapQ88b01`). Kit up PASS on the first attempt. K09 PASS: 6/6 processes bound to tree `ef71f5b4…`; served `index.js` `bb213d72…` and `details.js` `582156de…` = v2 FILE-PLAN `v2_sha256` (unchanged in v3).
- **Identities (D-2):** actors for the product checks are in the Capacity fixture tenant `19200000-…-01` with legal entity `19200000-…-02` — real Auth logins, real tokens. Nav checks use the platform system tenant (Q185 D-1).
- Browser path: Web only. 0 foreign requests besides the shell's Google Fonts.

| Check | en | ar | DB before/after (K10, strict) |
|---|---|---|---|
| **MOD-0192 happy path** (all real): entry page (0 API GETs) → create plan 201 → details → create scenario 201 → evaluate Finite 202 → Refresh until Completed (1 bottleneck row `line-4 2027-W03 520.000 480.000 40.000 HOUR`) → reload restores scenario + evaluation from the address | PASS (3 rows) | PASS (3 rows) | PASS: `plans +1, scenarios +1, evaluations +1, active_slots 0, receipts +3, audit +4, outbox +3` |
| UI-PM-01 one shell (entry + details) | PASS | PASS | (same pair) |
| UI-PM-05 skeletons visible while pending (FAKED delay, real responses) | PASS | PASS | PASS 0 write |
| UI-PM-12 error state + Retry for plan / scenario / scenario-malformed / evaluation (FAKED failures; real retry) | PASS | PASS | PASS 0 write |
| UI-PM-09 Create plan (Escape + close), Create scenario, Evaluate return focus to the opener | PASS | PASS | PASS 0 write |
| UI-PM-10 real 422 on create plan, create scenario, evaluate: panel open, focus inside, Escape closes | PASS | PASS | PASS 0 write |
| **O-3** 403 → page title focus, no raw error | **FAIL** | **FAIL** | PASS 0 write |
| **Q173 O-1** evaluationId in the address only after a successful read-back | PASS | PASS | PASS: `scenarios +1, evaluations +1, receipts +2, audit +3, outbox +2` (setup scenario B + the O-1 evaluation) |
| **Q173 O-2** queued id not applied after a failed scenario load; evaluation never shown under a foreign scenario | **FAIL** | **FAIL** | (same pair) |
| Nav: sidebar entry localized | PASS | PASS | PASS 0 write |
| Nav: Ctrl+K by `Nav.Page.CAPACITYPLANS` value opens the page | PASS | PASS | (same pair) |

26 check rows: **22 PASS, 4 FAIL**. 10/10 K10 pairs PASS (4 write, 6 zero-write). Every phase file: 0 native dialogs, 0 foreign requests, 0 page errors.

**O-3 detail (FAKED 403s; real 403s pass):**

- Create plan (entry page): the panel closes, the CTA is removed, the toast is localized — and **focus is on BODY**. The entry title `<h5>` has no id and no `tabindex`.
- Create scenario / evaluate (details): the opener is removed and focus goes to `#scenarioTitle` ("Capacity Scenario"). That is a stable heading, not BODY, but it is not the page title `#planTitle` (which has no `tabindex`).
- Page GET 403: `#planAlert` localized, title shown, workspace hidden — fine.
- No raw error text in any case. Real 403s: read-only has no CTAs and a direct POST returns 403; no-read sees `_AccessDenied` only and a direct GET returns 403.

**Q173 O-2 detail (all real; `png/q173-o2*`):** S1 and S2 are two scenarios of the same plan; E1 is S1's evaluation.

- (a) Address `scenarioId=<unknown>&evaluationId=E1` → scenario safe-not-found, evaluation panel empty. The user then opens S2 by ID: the page **requests E1 and shows it** under S2, and writes `evaluationId=E1` to the address.
- (b) Address `scenarioId=S2&evaluationId=E1` → E1 is **shown under S2**; the evaluation panel prints S1's id while the scenario panel shows S2.
- Cause as Q173 noted: `addressEvaluationId` is not cleared when the scenario load fails (`details.js:376-388`), and `isEvaluation` does not compare `evaluation.scenarioId` with the open scenario (`details.js:297-300`).

**Q173 O-1 detail:** POST 202 real; the read-back GET is FAKED 503 → address `evaluationId` absent, evaluation panel in error; after the real retry (200) the address carries the submitted id.

**Nav detail:** sidebar text "Capacity Planning" / "تخطيط السعة" = the v3 fragment `Nav.Module.CAPACITYPLANNING` value (single-page module: module name shown — Q185 D-2). Ctrl+K "Capacity Plans" / "خطط السعة" → 1 hit → page opens; the localized page name is in `/TenantSearch/data`.

## 6. NETCHECK 27017 (`NETCHECK-27017.txt`): **0 lane connections**

- Kit netcheck: 0 connections to 27017 for every lane PID (2 runs). Config: K04 "no 27017". 57192 never used.
- The sampler (1,470 samples) and the dev mongod log both show **3 new sockets** on 27017 during the run. All three carry the client name `NetworkInterfaceTL-ReplicaSetMonitor-TaskExecutor 8.0.18` — the dev mongod's own internal replica-set monitor reconnecting to itself — and both ends of each socket belong to pid 758. No lane process (.NET driver, mongosh) connected.

## 7. Cleanup and FINAL-CHECK (`FINAL-CHECK.txt`, `kit-a1/CLEANUP-a1.tsv`)

- Stop only. 6 services and 2 lane mongods stopped; 8/8 lane ports closed; 0 lane processes left.
- Kit seal PASS (`kit-a1/SECRET-SCAN-FINAL-a1.txt`); root pattern scan 229 units, 0 hits. Storage states truncated to 0 bytes.
- No rm, no rmdir, no kit K11.

## 8. Deviations

| ID | What | Why / effect | Reference |
|---|---|---|---|
| D-1 | `_shared-integration` items applied to the environment copy (provider line, 6 routes, v3 nav keys × 7) | As Q84b D-1; allowed by the draft README | `COMPOSE.tsv` rows `L6-env` |
| D-2 | **Lane-only test identities in the Capacity fixture scope** (lane DBs only, before/after recorded): (1) Platform tenant record `19200000-…-01`, cloned from a seeded tenant; (2) four Auth users cloned from seeded 97c5 users into that tenant; (3) the legal entity created through the MDM API was cloned in the lane MDM DB under id `19200000-…-02`; roles on existing catalog keys; OU / position / assignment through the product APIs | `DemandFixtureReader.cs:6-11` and `ConstraintFixtureReader.cs` accept a plan, scenario and evaluation only for that tenant + legal entity. Without D-2 every create returns 422, so the pack's happy path cannot run. Test data only; no code or config change; reported as F-Q88b-1 | `lane-scripts/tenant_fixture.py`, `lane-scripts/cap_fixture.py`, `kit-a1/raw/fixture/tenant-fixture.json`, `cap-fixture-a2.json` (`laneMdmLegalEntityClone`) |
| D-3 | Fixture attempt a1 added a `CreatedBy` field to the cloned legal entity; MDM could not read it (500). a2 removed the field from the lane clone and passed | My fixture error, corrected in the lane DB; no product effect | `lane-scripts/cap_fixture.a1.py`, `kit-a1/raw/fixture/cap-fixture.json` vs `-a2.json` |
| D-4 | Nav checks in the platform system tenant (`john.doe.def`); sidebar judged on the module label | As Q185 D-1 / D-2 | `lane-config/actors.tsv`, `RUNTIME.tsv` |
| D-5 | FAKED browser responses: UI-PM-05 (delay), UI-PM-12 (GET failures), O-3 (403 on three POSTs and the plan GET), Q173 O-1 (read-back 503) | These cannot be produced on demand with real services. Marked FAKED in `RUNTIME.tsv`. The happy path, UI-PM-09/10, O-2, nav and the real-403 rows use real responses only | `lane-scripts/cap_runtime.mjs` |
| D-6 | K11 not run (stop-only via `lane-config/stop-lane.sh`); supervisor ran with `~/mvp6-env/venv` on PATH | Prompt step 7 and the owner no-rm rule; bcrypt for K07 | `kit-a1/CLEANUP-a1.tsv`, `logs/kit/kit-up-a1.log` |
| D-7 | O-3 for scenario / evaluate judged FAIL although focus is on a stable heading | The check is worded "page title focus"; the observed target is the scenario section heading. CT may read this part differently; the create-plan part (BODY) fails either way | `browser/o3-{en,ar}-a1.json` |

## 9. Findings

| ID | Sev | What | Proposed owner |
|---|---|---|---|
| **F-Q88b-1** | HIGH (by design of the bounded fixture; blocks real use) | Capacity create plan / scenario / evaluate succeed only for tenant `19200000-…-01` + legal entity `19200000-…-02` (`DemandFixtureReader.cs`, `ConstraintFixtureReader.cs`, `CapacityEvaluationExecutor.cs`). Every real tenant gets 422. The UI happy path is reachable only with lane test identities (D-2) | pack owner / CT (DEMAND and constraint source integration) |
| **F-Q88b-2** | MEDIUM (Q173 O-2 FAIL) | Queued `evaluationId` survives a failed scenario load and is applied to the next scenario; an evaluation is displayed under a scenario that does not own it | frontend-ui-ux (draft v4): clear `addressEvaluationId` on scenario-load failure; reject an evaluation whose `scenarioId` ≠ the open scenario |
| **F-Q88b-3** | MEDIUM (O-3 FAIL) | 403 on create plan → focus BODY (entry title not focusable). 403 on create scenario / evaluate → focus on `#scenarioTitle`, not the page title | frontend-ui-ux (draft v4): title id + `tabindex="-1"` and a title fallback, as S&OP v2 |
| F-Q88b-4 | LOW | New build warning in an overlay test: `CapacityPlanFormContractTests.cs(121,9)` xUnit2013 | frontend-ui-ux |
| O-Q88b-1 | INFO | The "Required: 0 / 1" badge next to Scenario ID stays red when the field is filled from the address (`png/q173-o2b-*`) | frontend-ui-ux |
| O-Q88b-2 | INFO | `LoadAtomicityTests…UnknownCommitResult` intermittent (failed on A only here) | Loads owner |
| O-Q88b-3 | INFO | Kit service builds leave idle MSBuild reuse nodes for some minutes; a later preflight sees them (P6) | kit owner |

## 10. Counts

- **Build:** 6/6 targets, 0 errors; B has 1 extra warning (overlay test file).
- **Tests:** Web A 158/158, B 223/223; SupplyChain A 415/417, B 425/426; **0 new failures**; nav guard 7/7 on B.
- **Runtime:** 26 check rows — **22 PASS, 4 FAIL** (O-3 en/ar, Q173 O-2 en/ar); 4 logins PASS; 10/10 K10 pairs PASS; 78 PNGs.
- **NETCHECK 27017:** 0 lane connections.

## 11. Files

`REPORT.md` · `COMPOSE.tsv` · `BUILD-SUMMARY.tsv` · `TEST-SUMMARY.tsv` · `RUNTIME.tsv` · `NETCHECK-27017.txt` · `FINAL-CHECK.txt` ·
`SECRET-SCAN-ROOT.txt` · `SHA256SUMS` · `logs/` · `png/` (78) · `browser/` · `kit-a1/` (sealed) · `lane-config/` · `lane-scripts/` · `tools/`.

No tracked repo file was edited. No write outside the Allowed Paths. No rm or rmdir. No ledger write. commit YOK, push YOK. No prompt was written for another WP.
