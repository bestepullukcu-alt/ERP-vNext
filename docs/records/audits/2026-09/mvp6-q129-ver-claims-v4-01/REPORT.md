# Q129 — MOD-0187 Claims UI DRAFT v4: independent VER (build, sabotage proof, CU-25 re-run, regression) — SOP §37

| Field | Value |
|---|---|
| WP / prompt / lane | WP-MVP6-VER-129 · Q129 v1 · independent VER lane (`/read-only-audit` + testing-agent). Mode: **worktree-read-only**; the only repo write is this folder |
| Writer / independence | v4 was written by Cowork LANE 2 (Q64f). This session did **not** write v3 or v4. It earlier ran Q121b and Q121c (Platform/SupplyChain test proofs); disclosed |
| Pattern (§17.3) | table/bulk list + offcanvas (pack §32); unchanged |
| Where | Claude Code on the owner's Mac (Darwin), .NET SDK 8.0.417 / runtime 8.0.23, mongod 8.0.x, Playwright 1.59.1 (Chromium headless), evidence kit v1.2 |
| Repo | `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`, READ-ONLY |
| Workspace | `~/mvp6-env/q129/` (new). New folders only; **no `rm`** by this lane. The kit's K11 cleaned its own `ek-work` |
| Time | preflight 2026-09-27 16:15:57 +03:00 → end ~16:50 +03:00 |

## 0. Verification report (SOP §37)

```text
VERIFICATION REPORT

WP ID:                WP-MVP6-VER-129 (Q129 v1)
Verifier:             Q129 independent VER lane (Claude Code, Mac)
Verification date:    2026-09-27
Branch/HEAD:          feature/mvp6-logistics @ 4a8d4d4b339528a88e6220fb8402e5a2c771136c (start and end)

Agent Verdict:        PASS — Q122-D5 is fixed. CU-25 PASS in en and ar at runtime; sabotage RED on v3 / GREEN on v4;
                      Web 246/246; SupplyChain 416/417 (= Q122, 0 outcome differences); all requested regressions PASS.
                      One observation: the writer's Playwright CU-21 stale-transition test is racy (spec-a1 FAIL, spec-a2 PASS;
                      test unchanged since v3; product behaviour verified correct from the DB and by the harness CU-21).
Verification Verdict: PASS (every DOĞRULA item met; one LOW test-quality observation O-Q129-1)
CT Status:            returning to CT — Agent PASS ≠ CT ACCEPTED

Evidence level achieved:  EXECUTED (build/test/sabotage) + OBSERVED at runtime (real Auth, real services, isolated replica set,
                          headless Chromium through Web only, K09 freshness, K10 DB pairs, kit seal)
Required evidence level:  EXECUTED + OBSERVED runtime for CU-25 and the named regressions

Checks:
- scope:          v3→v4 = exactly the 4 declared files (index.js, ClaimIndexBehaviorTests.cs, spec, runtime README); v4 39/39 = FILE-PLAN
- build:          v4 Web.Tests 0 E / 18 W (= Q122), SupplyChain.sln 0 E / 0 W, 0 warnings in Claims files; v3 sabotage tree 0 E / 18 W
- tests:          Web 246/246 (Claims 88/88; ClaimIndexBehaviorTests 16/16); sabotage v3 15/16 (the new fact FAILS) → v4 16/16;
                  SupplyChain single-process 416/417 (the 1 = ClaimReplayTests…DurableRecovery, by design), identical to Q122 test by test
- runtime:        kit slot 8 (fresh); browser → Web 5801 → Gateway 5800 → services; CU-25 en/ar PASS; D-01, D-02, D1, D2, D4, CU-05, CU-11,
                  Save View/Reset PASS; freshness K09 6/6 + served index.js = v4 bytes
- persistence:    12 K10 pairs, 12 PASS (whole-DB rule); CU-25 and every keyboard/negative phase = zero write
- RBAC:           not re-tested (unchanged since Q122); spec-a1/a2 read-only / no-read / cross-LE rows PASS
- tenant:         cross-LE spec rows PASS; not otherwise re-tested (out of Q129 scope)
- concurrency:    CU-21 harness PASS; writer spec CU-21 racy (O-Q129-1); DB shows single effect in both spec runs
- idempotency:    not in scope (unchanged since Q122)
- audit/evidence: claims_audit order used to explain spec-a1 (logs/lane/spec-cu21-db-probe-a1.txt)
- observability:  CU-05 error state shows support reference = X-Correlation-Id
- migration/rollback: n/a (UI draft)
- integration:    env-copy only (_shared-integration applied to the copy, as Q122); CU-28 remains BLOCKED (not in scope)
- console/security leakage: 0 root leaks, 0 native dialogs, 0 page errors (11 phase files); kit seal (see §8)

Failed criteria:  none (O-Q129-1 is a test-quality observation on a row outside the CU-25 fix)
Rework required:  no (optional: harden the writer spec CU-21 test — O-Q129-1)
Next gate:        CT disposition of Q129 → Claims writer-complete + hours credit; CU-28 on the integrated target
```

## 1. Preflight and inputs

| Check | Result |
|---|---|
| `uname -s` · `GIT_OPTIONAL_LOCKS=0` · `.git/index.lock` | Darwin · set · absent (start and end) |
| Branch / HEAD | `feature/mvp6-logistics` / `4a8d4d4b…` = expected |
| Diff list | **19 paths** recorded at 16:15:57; `git diff` sha256 `95a5a1e04f7cbf75…efa1` (start = end) |
| BASE `~/mvp6-env/base/BASE-MANIFEST.tsv` | `a8a236de82a9f2b5d4c0f3560aa13894c3740c6e187cb0140f5da1b08109c661` ✓; `src` 14,566/14,566, 0 writable (verified this lane, `verify_base.py` from Q121b) |
| Q117 overlay | `83e6322c1d4dd46c80b0b4de0a2b60493883a7b2d923c7685da689de702f800d` ✓ (11 members, 0 unsafe) |
| v3 archive | `66e72bf6af1ebb6fffbcedf5f8b7934cd9a5a0f95cb1abf558b92fa3b500ce0c` ✓; draft-03 SHA256SUMS all OK |
| v4 archive | `2b34741af91dc640862cd6e460809e860040e21747a66849aa5b61bfa099293f` ✓; draft-04 SHA256SUMS 4/4 OK; **39/39** files = FILE-PLAN sha256; 0 unsafe members |
| v3 → v4 | `diff -rq`: exactly `index.js` (`c8ef218e…` → `77733718…`), `ClaimIndexBehaviorTests.cs`, `claims-ui.spec.mjs`, runtime `README.md` |
| Port 57192 | free at 16:21:52 (no listener, no socket); started only then, ownership checked before `rs.initiate` |
| 27017 | never addressed by this lane (§8) |

## 2. Composition (`compose/`)

- `compose/compose_q129.py` = Q122's `compose_q122.py` parametrised by version. Fail-closed; it never deletes. The diff against Q122 is 5 lines: paths and names.
- `~/mvp6-env/q129/v4/src` and `~/mvp6-env/q129/v3/src` were built the same way: `cp -c -Rp` of BASE (made writable on the copy only) → L4 Q117 (10 replaced, 1 new) → L5 module files (23, all new paths) → L6 `_shared-integration` inside the copy only (SupplyChain provider line, ocelot 275 → 277, nav keys ×7). **14,590** files each.
- Derived archives (`compose/DERIVED-OVERLAYS.sha256`):
  - `q117-files` = `835b6088…`, `claims-v3-module` = `3f896415…` and `claims-*-env-integration` = `bc398cb8…` are **byte-identical to Q122's** (independent reproduction);
  - `claims-v4-module` = `e51263a3…`.
- `v3/src` vs `v4/src`: exactly `index.js` and `ClaimIndexBehaviorTests.cs` (`compose/C1-…`).
- The **kit's own K01 tree** (HEAD + 6 overlays, `lane-config/overlays.tsv`; tree `05084467…`) is **byte-identical to `v4/src`**: 14,590/14,590, 0 differing, after excluding the K06 `bin/obj` outputs (`compose/K01-vs-compose.txt`).
- Both `src` trees were set read-only. Builds ran in APFS clones: `v4/build`, and `v3/sab` = v3 + the v4 test file.

## 3. Build and tests (`BUILD-SUMMARY.tsv`, `TEST-SUMMARY.tsv`, `logs/build`, `logs/test`)

| Target / suite | Result |
|---|---|
| v4 `Diten.Web.Tests` (builds Web) | 0 E / 18 W (= Q122); 0 warnings in Claims files |
| v4 `Diten.SupplyChainService.sln` | 0 E / 0 W |
| v3/sab `Diten.Web.Tests` | 0 E / 18 W |
| **v4 Web.Tests** | **246/246**. Claims **88/88**: ClaimFormContractTests 40, SupplyChainClaimsControllerTests 32, ClaimIndexBehaviorTests **16** (Q122 87 + the new fact) |
| **SupplyChain single-process** (v4; lane test mongod 127.0.0.1:57192 `rsmod192`, `enableTestCommands=1`, own dbpath `~/mvp6-env/q129/test-mongo-192`) | **416/417**. The 1 = `ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery` ("explicit write/read restart mode required", excluded by design) |

**SupplyChain vs Q122 test-a2** (`SUPPLYCHAIN-VS-Q122.txt`):
- the same 417 test names (GUIDs normalised), with **0 different outcomes**;
- per module: CapacityPlans 39/39, Carriers 36, Claims 127/128 names, Loads 33, ModuleRegistration 9, Returns 78, SandopPlans 19, Shipment and Source groups green;
- 10 test DBs were created on 57192 and all dropped; the mongod was shut down cleanly (`logs/M1-…`).

## 4. Sabotage proof (SOP §24.2 vacuity; `SABOTAGE.txt`)

- Same test file (v4 `ClaimIndexBehaviorTests.cs`, `b802ca25…`), same filter (`FullyQualifiedName~ClaimIndexBehaviorTests`). The two trees differ **only** in `index.js`.
- **Fix absent** (v3 `index.js` `c8ef218e…`): `Create_surface_keeps_keyboard_focus_after_a_rejected_submit` **FAILED**. The failure message names the missing `const restoreCreateFocus`. The other 15 passed (**15/16**). TRX: `logs/test/v3-sab--web-ClaimIndexBehaviorTests-SABOTAGE.trx`.
- **Fix present** (v4 `index.js` `77733718…`): **16/16 PASS**. TRX: `logs/test/v4-build--web-ClaimIndexBehaviorTests.trx`.

## 5. Runtime (evidence kit v1.2, `kit/`)

- **Slot 8 (fresh):** Gateway 5800, Web 5801, Auth 5856, Platform 5857, MDM 5859, SupplyChain 5861, sink 5899, Mongo **38994** (`rsClaimsQ129Ver01`, DB suffix `ClaimsQ129Ver01`). All were free before start.
- **Lane config:** `lane-config/` copies Q122's (slot and paths changed; overrides identical except `Claims__ReferenceBaseUrl` → 5800).
  - `up-q129.sh` = kit `run-kit.sh up` + the K07pre tenant step (known lane deviation since Q64b).
  - K05 is the **unmodified kit file**: Q122's DV-1 lane copy was not needed, because the runtime needs no test commands.
- **`up`:** PASS on the first attempt (`logs/kit/up-a1.log`): K01, K03, K02, 6 × K06 build, K04b, K04 (no 27017, no literal secret), K05, 6 launches, netcheck (0 × 27017), K07pre, K07.
- **Hand-off:**
  - carrier-management, shipment-tracking-pod, claims-management, legal-entity and product-item-sku-master self-registered (Attempt=1);
  - MDM audit appends returned **202 ×6** (`kit/raw/logs-excerpt/registration-audit-excerpt-a1.log`, redacted through K08: 0 replacements).
- **Freshness (SOP §25), K09 PASS:**
  - 6/6 services: running DLL = K06 Release build, still the listener;
  - served `/assets/js/SupplyChain/Claims/index.js` = **`77733718f5976c90…` = the v4 archive file**; `index.l10n.js`, `personalization-client.js` and `dt-defaults.js` equal their source;
  - 4 browser URLs are on 127.0.0.1:5801 (`kit/SOURCE-BINARY-PROCESS.tsv`, `kit/raw/browser-binding.tsv`);
  - each process started after its DLL was written, e.g. Web DLL 16:30:12 → process 16:31:02 (`logs/F1-process-start.txt`);
  - end recheck: every PID unchanged (`kit/raw/k09-recheck-end.tsv`).
- **Fixture:** Q122's `org_fixture.py`, with only its labels changed Q122 → Q129. It created 9 shipments (DISPATCHED, CARRIER, DRAFT, SEED, SOFTDEL, NULLROOT, BADROOT, FLOW, T2SHIP) and 10 claims through product APIs, with a K10 pair of exactly Q122's deltas. Logins: 5 actors through the real Web login; storage states only in `~/mvp6-env/q129/state-162443/` (0600).
- **Harness:** `lane-scripts/claims_runtime.mjs` = Q122's harness + 2 new phases (57 added lines; nothing else changed):
  - `cu25`: en and ar;
  - `cu11`: the CU-11 part of Q122 `extra`, which needed Q122's matrix fixture for its other parts.
  The v4 writer spec was run through the same `spec` phase (`~/mvp6-env/q129/runtime`, spec byte-identical to the v4 archive, `ab3cb56d…`).

### 5.1 CU-25 (Q122-D5), `kit/browser/cu25-a1.json`, K10 zero write

| | en | ar |
|---|---|---|
| `dir` | ltr | **rtl** |
| create submit | Enter on focused Save → **400 `INVALID_REQUEST`** | same |
| `document.activeElement` after the 400 | **`btnSaveClaim`**, inside `#offcanvasCreateEdit` (1 ms) | **`btnSaveClaim`**, inside (2 ms) |
| Save enabled · alert · inputs kept | yes · yes · `1e2`/`EUR` kept | yes · yes · kept |
| Tab pressed before Escape | **no** | **no** |
| Escape closes the panel | **yes** | **yes** |
| focus after close | **`.add-new`** | **`.add-new`** |
| new JS errors | 0 | 0 |
| PNG | `03a-create-400-focus-en.png`, `03b-create-400-escape-en.png` | `03a-create-400-focus-ar.png`, `03b-create-400-escape-ar.png` |

For comparison, Q122 on v3 recorded `activeAfterResponse: BODY` and `closedByEscape: false`. The writer's own Playwright scenario for this row passed in both spec runs.

### 5.2 Regression (`ACCEPTANCE.tsv`)

| Row | Result | Evidence |
|---|---|---|
| D-01 reload after create / filter / transition | PASS | `ui-flows-a1` (3 D-01 cases), `regd01-a1`, `regression-a1` |
| D-02 single tenant shell | PASS (1 wrapper, 1 `main.js`, 1 footer) | `ui-flows-a1`, `png/d02-single-shell-en.png` |
| D1 skeleton then table | PASS | `cu05-a1`, `regression-a1` |
| D2 transition focus/Escape | PASS: `transitionTarget` at 50 ms and 900 ms; Escape closes | `kbd-a1`, `regression-a1` |
| D4 focus back on Add | PASS after Escape (incl. the new post-400 path) and after Save | `kbd-a1`, `cu25-a1`, `regd01-a1` |
| CU-05 skeleton / empty / error | PASS | `cu05-a1` |
| CU-11 lexical 400/422, UI keeps inputs | PASS | `cu11-a1` |
| Save View / Reset (6 checks) | PASS | `saveview-a1` |
| Also exercised | CU-02, CU-08, CU-21 (harness), CU-31, CU-25b/c, logins ×5: PASS | `ui-flows-a1`, `kbd-a1`, `login-a1` |
| v4 writer spec | a1: 16 passed / **1 failed** (CU-21 stale transition) / 1 skipped · a2: **17 passed** / 0 failed / 1 skipped (CU-19 outline, by design) | `spec-a1.json`, `spec-a2.json`, `logs/spec-report-a1-copy.json`, `kit/browser/spec-report.json` (a2) |

**F8** (`F8-SCAN-ALL.tsv`, 11 phase files, 31 cases):
- **0** lifecycle-root leaks, **0** native dialogs, **0** page errors;
- the console `error` lines are only the browser's own network lines for the intended 400/422/503 responses;
- the only foreign hosts are the shell's Google Fonts.

**K10:** 12 pairs, **12 PASS** (`kit/raw/db/db-assertions.tsv`).

## 6. Findings

| ID | Sev | What | Evidence | Proposed fix | Owner |
|---|---|---|---|---|---|
| **O-Q129-1** | LOW (test quality) | **The writer's Playwright CU-21 test is racy.** Actor A clicks Confirm on Approve, but the test does not wait for A's response before actor B submits Reject. Whichever request the server receives first wins. spec-a1: only **one** transition committed (Investigating → **Rejected** by B, 13:36:03Z), so B got 200 and the test expected 422. spec-a2: A's Approve committed first (13:37:43Z), B got 422 and the test passed. The server enforced a single effect both times, so the product is correct. The test is byte-identical in the v3 and v4 specs (it passed once in Q122). The harness CU-21, which awaits A's 200 before B submits, PASSED here. | `logs/lane/spec-cu21-db-probe-a1.txt`, `kit/browser/spec-a1.json`, `kit/browser/spec-a2.json` | In the spec, `await` A's transition response (as the harness does) before B's submit | UI writer (spec) |
| O-Q129-2 | INFO (harness) | The Q122 harness `spec` phase writes `spec-report.json` and `png/spec/*.png` to fixed paths. A second run overwrites the first run's copies. The a1 report was copied beforehand (`logs/spec-report-a1-copy.json`); a1's PNG hashes stay in `spec-a1.json`, and `kit/PNG-INDEX.tsv` marks the 12 overwritten rows. | `kit/PNG-INDEX.tsv` | Give the spec output a per-attempt folder | kit/harness owner |
| O-Q129-3 | INFO (env) | Still open from Q122: the Capacity tests hard-code `127.0.0.1:57192/rsmod192` (O-Q122-3). This lane checked the port was free and owned before use. | `logs/M0-57192-precheck.txt` | as O-Q122-3 | SupplyChain test owner |

Items carried from Q122 and not re-tested (unchanged by v4): CU-28 BLOCKED (integrated target), F-Q122-01, O-01 (401 code), O-Q122-1/2/4, F-0183-500, Q64d-D3 (Enter on Add).

## 7. Deviations and harness notes

- **DV-1:** Gateway ran on kit slot port 5800, not 5000, as accepted for Q64d/Q64e/Q122.
- **DV-2:** Harness phases:
  - `cu25` and `cu11` were added to the Q122 harness; everything else was reused unchanged.
  - Phases not re-run: vs1, checks-neg/mut, crafted, cu12, cu18, cu19, cu20, cu22, l10n, cu26, nf-ui, matrix, extra, extra2, kbd2. They are not in the Q129 scope, and v4 does not touch them (CHANGES.md: the transition surface is untouched).
- **DV-3:** K09 ran twice.
  - Run 1 passed `--served`/`--browser-url` repeatedly. Those arguments are `nargs="*"`, so argparse kept only the last value: 6/6 processes bound, but only 2 browser rows.
  - Run 2 passed all values after one flag: 4 served assets + 4 URLs.
  - Both rows are in `kit/COMMANDS.tsv`; the run-1 browser table is kept as `logs/browser-binding-k09-run1.tsv`.
- **DV-4:** The writer spec was run twice (spec-a1 FAIL, spec-a2 PASS) to characterise O-Q129-1. Both results are reported; neither is hidden.
- **DV-5:** Two stray files were written to the per-user temp folder `$TMPDIR`: `k01list` (a list of file names, from the K01 comparison) and `q129-final.txt` (a copy of `FINAL-CHECK.txt`). Neither holds a secret. Both are left in place under the no-`rm` rule.

## 8. Hygiene

- **Repo:** branch/HEAD unchanged, 19 diff paths, `git diff` hash = preflight, no `index.lock`, no git write (`FINAL-CHECK.txt`). Only this folder was written. Untracked went 478 → 480: this folder and `mvp6-q101b-control-audit-01/` (another writer, 16:42).
- **BASE, overlays, draft folders:** not modified. BASE was verified 14,566/14,566 with 0 writable files; archives were hash-checked before extraction.
- **27017:** never addressed. Kit netcheck a1/a2 shows 0 connections. All test and lane URIs point at 57192 or 38994; the fixture and tenant scripts refuse 27017.
- **Processes:**
  - the 6 lane services (killed after the stop timeout, K-F5), the lane mongod 38994 and the test mongod 57192 are stopped;
  - the supervisor shut down after the seal;
  - K11 CLEANUP has 0 FAIL, and all 8 slot ports have no listener.
- **No `rm`** by this lane. K11 removed its own `~/mvp6-env/q129/ek-work`, including the lane Mongo data and service logs; the excerpt was redacted first. The test-mongo dbpath and the build trees remain in `~/mvp6-env/q129/`.
- **Secrets:**
  - generated by the supervisor in memory only and never printed;
  - kit seal `kit/SECRET-SCAN-FINAL-a1.txt`;
  - root pattern scan `SECRET-SCAN-ROOT.txt`, written before `SHA256SUMS`.

## 9. Files

| Path | What |
|---|---|
| `REPORT.md`, `ACCEPTANCE.tsv` | this report; one row per DOĞRULA item with evidence paths |
| `BUILD-SUMMARY.tsv`, `TEST-SUMMARY.tsv`, `SABOTAGE.txt`, `SUPPLYCHAIN-VS-Q122.txt`, `F8-SCAN-ALL.tsv` | summaries |
| `compose/` | compose tool, logs (v3, v4), per-layer before/after, derived manifests + hashes, file-plan / tree / sabotage / K01 checks |
| `lane-config/`, `lane-scripts/`, `lane-tools/` | lane parameters (no secrets), `up-q129.sh`, pair helpers; harness + fixtures; build/test scripts and Playwright config |
| `logs/` | build and test logs + TRX; kit up/down/seal logs; K10 pair logs; 57192 pre-check and drop; process start times; spec-a1 report copy; K09 run-1 table |
| `kit/` | sealed kit evidence: COMMANDS.tsv, SOURCE-BINARY-PROCESS.tsv, CLEANUP.tsv, PNG-INDEX.tsv, png/ (38), browser/ (11 phase files + spec report), raw/, ARTIFACTS.sha256, SECRET-SCAN-FINAL-a1.txt |
| `FINAL-CHECK.txt`, `SECRET-SCAN-ROOT.txt`, `SHA256SUMS` | end-of-lane repo check, root secret scan, hashes of every file in this folder |

## 10. To-do list

1. **CT:** Q129 disposition. With it, Claims v4 (Q64f) closes Q122-D5 (CU-25). CT decides writer-complete and the hours credit.
2. **UI writer:** O-Q129-1: make the spec's CU-21 test await actor A's response before B submits (1-line change; product unaffected).
3. **Kit/harness owner:** O-Q129-2: per-attempt spec output folder. K-F5 (stop timeout) and O-Q122-4 are still open.
4. **Integration owner:** CU-28 on the integrated target; O-01 (401 code); re-confirm CU-27 on the real ocelot diff.
5. **SupplyChain test owner:** O-Q122-3 / O-Q129-3 (Capacity tests hard-code 57192).

Agent PASS ≠ CT ACCEPTED — returning to CT.
