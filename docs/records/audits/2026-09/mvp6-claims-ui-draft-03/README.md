# MOD-0187 Claims — tenant UI DRAFT overlay v3 (Q64e) — SOP §22

> **Status: DRAFT (not writer-complete).** v3 fixes Q64d-D1, D2 and D4. The CU-05 and CU-25 re-runs and the D1/D2/D4 regression pass
> **pass on the accepted base**. Per owner decision **D6** the draft code is here **only as an archive**. Agent PASS ≠ CT ACCEPTED.

| Field | Value |
|---|---|
| WP / prompt | WP-MVP6-187-UI-064e · Q64e v1 · DEV lane (`@orchestrator` + `/add-module` Phase 4 rework → frontend-ui-ux → testing-agent) |
| Pattern (§17.3) | table/bulk list + offcanvas actions (pack §32); no Details page |
| Repo | `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`, READ-ONLY. The only write is this folder. |
| Base | `~/mvp6-env/base/src` = Q103 BASE-MANIFEST, BASE-HASH `a8a236de…`: 14566/14566 at start and end (`compose/base-verify.txt`, `FINAL-CHECK.txt`). Not modified. |
| From | v2 `mvp6-claims-ui-draft-02/claims-ui-draft-overlay-v2.tar.gz` `1a57c879…` (hash checked before extract) |
| **v3** | **`claims-ui-draft-overlay-v3.tar.gz` `66e72bf6af1ebb6fffbcedf5f8b7934cd9a5a0f95cb1abf558b92fa3b500ce0c`**: 39 files; deterministic (a rebuild gives the same hash); no AppleDouble |
| Pack | MOD-0187 `96c9a0ae…` §32/§33 (`ready-for-dev`) |
| Workspace | `~/mvp6-env/q64e/`. New folders only, **no `rm`**. `v3-rc1/`, `diff-*`, `pack-*` and all `inputs/` are kept |

## 1. What changed (details in `CHANGES.md`)

v2 → v3: **2 files modified**, 0 added, 0 removed (`compose/v2-v3-diff.tsv`).

| File | Lines | Changes |
|---|---|---|
| `index.js` | +34/−2 | E-01 and E-02 (D1), E-03 (D2), E-04 (D4) |
| `ClaimIndexBehaviorTests.cs` | +36/−0 | E-05: three contract tests |

**D1 had two causes; Q64d saw one.** The first v3 candidate (**rc1**) fixed only the shared-CSS cause, exactly as Q64d described it. Its runtime run
still failed CU-05. A skeleton timeline (`runtime-kit/browser/diag-v3-a1.json`) showed the second cause: the **shared `DtDefaults`
drawCallback fades the skeleton out on DataTables' empty first draw**, before any data exists. The final v3 stops that fade from the
module side. No shared file was changed. The rc1 lane is kept sealed as the record of the finding (`runtime-kit/`); rc1 is **not** delivered.

**D2 was narrower than Q64d reported.** Measured on v2 in the same lane:
- the surface gets no keyboard focus and ignores Escape **during the ~300 ms slide-in**;
- once settled, Bootstrap's focus trap holds the container and Escape does close it.

Q64d's harness pressed Escape inside the slide-in window. v3 focuses the first field at once and again after the slide-in, so Escape works in both windows.

**D3 (Enter on Add) is not changed.** It belongs to the template owner, and golden slim has the same wiring. It is listed in §5.

## 2. Build and tests (in `~/mvp6-env/q64e/src` = base copy + v3 module files + draft `_shared-integration` on the copy only)

| Check | Result |
|---|---|
| Compose | 23 module files at new paths; env integration 9 files = **Q64d byte for byte** (`bc398cb8…` / manifest `b45b83ff…`). Kit K01 tree `77360116…` = my tree, 14589/14589 (`compose/k01-final-vs-src.txt`) |
| Build `Diten.Web.Tests` (builds Diten.Web) | **0 errors**; 18 warnings on the full build (same 15+3 as Q103), **0 in Claims files** (`logs/build/`) |
| **Diten.Web.Tests** | **245/245** (Claims **87/87** = v2's 84 + 3 new) on rc1 and on final v3 (`logs/test/web-tests-v3*.trx`) |
| **Sabotage proof** (3 new tests; `logs/test/sabotage2-*.trx`) | v2 `index.js` → **3 FAIL** · rc1 → **1 FAIL** (the D1 test) · final → **3 PASS** |
| Static (`static/`) | `node --check` 3/3; xmllint 7 resx + 7 nav fragments + gateway JSON; resx parity **7/7** (95 keys); error codes **18 = 18 = 18** (UI / pack §32.8 / contract §D187-05); greps: native dialogs 0, inline handlers 0, browser storage 0, token/ports 0, English in views/sinks 0. Only `index.js` and the test changed after the full run; both were re-checked (`STATIC-CHECKS-final.txt`) |

SupplyChain and architecture tests were not re-run, because v3 changes no backend or architecture-relevant file. The Q64d totals still apply to the unchanged files.

## 3. Runtime (evidence kit v1.2, kit slot 8, browser → Web 5801 → Gateway 5800 → services)

Two lanes, both run to completion: `up` PASS → fixture → phases → K09 → `down` (K11 PASS) → ARTIFACTS → seal PASS.

| Lane | Folder | DB suffix | Tree / served `index.js` | Purpose |
|---|---|---|---|---|
| rc1 | `runtime-kit/` (sealed, 86 files) | `ClaimsQ64e01` | `fbf1c7e7…` / `95f79684…` | CU-05 v2 and rc1 → still FAIL → skeleton timeline (second D1 cause) |
| **final** | **`runtime-kit-final/`** (sealed, 117 files) | `ClaimsQ64e02` | **`77360116…` / `c8ef218e…`** | CU-05 and CU-25 on v2 and v3, D1/D2/D4 regression |

- **Freshness (SOP §25):** K09 PASS in both lanes. Every service is `match`/`listening` against its own K06 Release build.
- **v2 in the same lane:** the v2 `index.js` was served through the kit's `fulfillHashed`. It was recorded before serving (`record-served`), and every
  served copy was hashed and matched (`raw/served-overrides.tsv`).
- **Final lane totals:** 0 root leaks, 0 native dialogs and 0 page errors in all 7 phase files. K10 pairs **7/7 PASS**; every non-regression phase wrote nothing,
  and the regression wrote exactly +1 claim and +2 receipt/audit/outbox. PNGs: **29** (`runtime-kit-final/PNG-INDEX.tsv`).
- `RUNTIME-RESULTS.tsv` has the per-row table. Summary:

| Row | v2 | v3 final |
|---|---|---|
| **CU-05** skeleton / empty / error | **FAIL**: skeleton never visible | **PASS**: skeleton visible through the whole delayed load; empty (shared localized text) and 503 error states distinct |
| **CU-25a** responsive (en/ar at 390/768/1024/1280/1440; 5 languages at 1280; ar RTL) | — | **PASS** |
| **CU-25b** create keyboard | **FAIL**: focus not returned (D4) | **PASS**: Space opens, focus inside, Escape closes, inert, focus back on Add. Enter = D3 (unchanged) |
| **CU-25c** transition keyboard | **FAIL**: during the slide-in focus is on `BODY` and Escape is ignored | **PASS**: focus on `#transitionTarget` at 50 ms and at 900 ms; Escape closes |
| **Regression** D-02 shell, D1 after load, filter + Save View, QuickView, D-01 create → reload + D4 focus, D2 flow Open → Investigating (200) | — | **6/6 PASS** |

## 4. Deviations and assumptions

- **DV-1:** the first `up` served rc1. After rc1 failed CU-05, the lane was bound (K09), taken down and sealed before the code changed. The final v3 ran in a
  **second, fresh lane** with its own suffix, workspace, socket and evidence folder. Nothing was overwritten.
- **DV-2:** `up-q64e.sh` = `run-kit.sh` plus the K07pre tenant step (K-F1; same as Q97/Q64d).
- **DV-3:** the fixture is the Q64d fixture reduced to what CU-05/CU-25 need. The fixture-admin role holds `claims.decide` from the start, which avoids the
  Q64d attempt-1 stop. No `carriers.read` grant was used.
- **DV-4:** the harness `diag` phase was added during the lane to measure the skeleton; it writes nothing (K10 PASS).
- **A-1:** `DtDefaults` and `backbone-custom.css` are shared files and protected. The D1 fix lives only in the module. The template owner may want to
  review why `DtDefaults` fades `#skeleton-loader` on every draw (see §5).

## 5. Findings and open items

| ID | Sev | What | Owner |
|---|---|---|---|
| Q64d-D3 | LOW | Enter on the focused Add button does not open create; Space does. Golden slim wires Add the same way. Not changed. | frontend template owner |
| Q64e-S1 | INFO | Shared `DtDefaults` fades `#skeleton-loader` in `drawCallback` on every draw, including DataTables' empty first draw of an ajax-function table. Every module that shows its own first-load skeleton hits this. Claims now protects itself. | frontend template owner |
| Q64e-C1 | INFO | Correction to Q64d-D2: on v2, Escape works once the surface has settled. The defect was the slide-in window plus no field focus. | CT (record) |
| — | — | Still open from Q64d: CU-18 (malformed root not reachable at runtime), F-CU10 / **Q114** (`carriers.read`), O-01 (401 body code), CU-27/28 on the integrated target, CU-30 disposition | CT / owners |

## 6. Hygiene

- **Repo:** unchanged: 19 diff paths, no `index.lock`, no git write (`FINAL-CHECK.txt`). **No `rm`** by this lane.
- **Kit deletions:** as approved for Q64d, the kit removed its own workspace sub-folders (K11), K04b's temporary route folder and its sockets.
- **Browser storage states:** truncated to 0 bytes, not deleted.
- **Secrets:**
  - both lanes sealed with `result PASS` (exact + pattern);
  - this folder's root was pattern-scanned before `SHA256SUMS` (`SECRET-SCAN-ROOT.txt`);
  - the Loads `runtime_probe.py` secret (Q101 F02) was not read.
- **Processes:** every lane service, lane mongod and supervisor stopped. No test mongod was used in Q64e.
- **Another lane on 57192:** a mongod on **57192** belongs to **another lane (`~/mvp6-env/q119`, started 12:51)**. It is not mine and was not touched.
  57192 was not used here (WP step 4: "only if no other lane is on it").

## 7. Files

| Path | What |
|---|---|
| `claims-ui-draft-overlay-v3.tar.gz` | **the draft** (39 files: `overlay/frontend/**`, `overlay/services/**`, `overlay/_shared-integration/**`, `runtime-scenarios/**`) |
| `SOURCE-MANIFEST.tsv`, `FILE-PLAN.tsv`, `CHANGES.md` | per-file hashes; 39-row plan with v3 change and reason; v2 → v3 changes with reasons |
| `RUNTIME-RESULTS.tsv` | CU-05 / CU-25 / regression per row and version |
| `runtime-kit-final/`, `runtime-kit/` | sealed kit evidence for the final and rc1 lanes (COMMANDS, SOURCE-BINARY-PROCESS, CLEANUP, browser/, png/, raw/, ARTIFACTS, SECRET-SCAN-FINAL) |
| `lane-scripts/` | harness folder used by the supervisor (`claims_runtime.mjs`, `org_fixture.py`, `tenant_fixture.py`) |
| `lane-config/`, `compose/`, `static/`, `logs/` | lane parameters (no secrets), compose tool and checks, static checks, build/test logs + TRX (incl. sabotage), kit up/down/seal logs |
| `FINAL-CHECK.txt`, `SECRET-SCAN-ROOT.txt`, `SHA256SUMS` | end-of-lane repo check, root scan, hashes of every file here |

Agent PASS ≠ CT ACCEPTED — returning to CT.
