# Q65b — MOD-0186 Returns Mac build + test + runtime: BASE-STACK v2 + Returns overlay v2 (SOP v2.5 §17/§36.2)

**Agent verdict: FAIL.** One acceptance line fails: **RUNTIME, "all listed checks PASS in en + ar"**. O-3 fails in both
locales: after a 403 on create (or on a transition), keyboard focus falls to BODY. The Returns page title cannot take focus.

Every other line passes:

- preflight P1–P6;
- tree A 14,572 files, and the overlay rows are hash-exact;
- build 0 errors;
- **new test failures vs A = 0**, and `NavManifestL10nGuardTests` passes on B;
- all other runtime checks PASS in en + ar, with DB before/after evidence;
- NETCHECK 27017 = 0;
- FINAL-CHECK clean;
- SHA256SUMS present.

Separately, **F-Q65b-1** blocks every Returns transition for real users on this stack. The pack's next workflow step needed a lane-only catalog entry (D-3) to run.

Agent PASS/FAIL ≠ CT ACCEPTED — returning to CT.

| Field | Value |
|---|---|
| WP / prompt / template | Q65b · Prompt Q65b v1 · T2 v1 · role T2 Mac build/test/runtime · CT-QUEUE row Q65b (after Q84b; owner OD-Q65b-ORDER) |
| Where | Claude app → Code tab → Local session on the Mac, folder ERP-vNext-recovery (`uname -s` = Darwin). .NET SDK 8.0.417 / runtime 8.0.23, mongod 8.0.18, Playwright (Chromium, headless) |
| Repo | `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`, read-only. `GIT_OPTIONAL_LOCKS=0`; `git status --porcelain` only; no diff/add/commit/push |
| Base stack | BASE-STACK v2 `ce8d60ab959ec7018c255ab1152983cfd8e301390f54b3d0ba23961bf2c4d882` (BASE a8a236de → Q117 83e6322c → Q121 93bf1c07 → Q131 4a4a0860) |
| Overlay | `mvp6-returns-ui-draft-02/returns-ui-draft-overlay-v2.tar.gz` `8862b46e…ecfd` · SHA256SUMS `569f1cc9…8fd9f` · FILE-PLAN `abd913ad…96aa` |
| Pack / UI rules | MOD-0186 `933e8926…21fe` · `.antigravity/agents/frontend-ui-ux.md` `90247ddc…f044` (r2; OD-F-Q145-1; OD-Q164-09) |
| Reused kit | Copied from `mvp6-q84b-sop-mac-01/` (REPORT `a324b35b…`; not edited): its tools, lane-config and lane-scripts, adapted in `~/mvp6-env/q65b/`. The evidence kit `scripts/evidence-kit/` was used in place, unchanged |
| Workspace | `~/mvp6-env/q65b/`: new folders only, nothing deleted, kept |
| Time | start 2026-09-30 23:09:58 +03 · end 2026-09-30 23:41 +03 |

## 1. Preflight (all PASS)

| # | Check | Result |
|---|---|---|
| P1 | `uname -s` | Darwin |
| P2 | `.git/index.lock` | absent; `GIT_OPTIONAL_LOCKS=0`; porcelain only |
| P3 | HEAD | `4a8d4d4b339528a88e6220fb8402e5a2c771136c` |
| P4 | 7 sha256 values (Base Stack, overlay, SHA256SUMS, FILE-PLAN, pack, UI rules, Q84b REPORT) | all equal the prompt values |
| P5 | `mvp6-q65b-returns-mac-01/` | absent at start |
| P6 | other run / slot listeners | none. Slots 1–9 had 0 listeners; the only related process was the owner's dev mongod on 27017 (pid 758) |

Inputs checked:

- overlay folder SHA256SUMS 4/4 OK;
- v1 folder `mvp6-returns-ui-draft-01/` SHA256SUMS `a80047b5…` all OK (matches CHANGES.md);
- layer evidence folders as in BASE-STACK v2 (checked again by the compose script against the archive hashes).

## 2. Compose (`COMPOSE.tsv`, `logs/compose/`, `tools/compose_q65b.py`)

- **Tree A** `~/mvp6-env/q65b/treeA/src`:
  - BASE clone 14,566/14,566 → Q117 (10 replaced, 1 added) → Q121 (2 replaced) → Q131 (15 replaced, 5 added). Every preimage was checked.
  - Result: **14,572 files, 0 mismatch / extra / missing.**
- **Overlay v2:**
  - extracted to `stage/ret-v2`; its bytes equal the archive members; the v1 archive `35dd1489…` was also extracted;
  - **FILE-PLAN 8/8:** v1 member = `v1_sha256` and v2 member = `v2_sha256`; the other 28 files are byte-equal to v1;
  - 23 module files, all **new tree paths** (before = absent), each after = the v2 member.
- **L6 environment integration** (the draft README allows this "for the isolated environment only (Q65b)"; environment copy only):
  - SupplyChain `Program.cs`: `AddSingleton<IModuleManifestProvider, ReverseLogisticsManifestProvider>()`;
  - `ocelot.json`: 2 Returns routes (275 → 277), and the confirm route `GET /api/shipment-bundle/shipments/{shipmentId}` is present;
  - nav keys `Nav.Module.REVERSELOGISTICS` and `Nav.Page.RETURNS` × 7 resx.
  - Not applied: the icon map, the platform checklist, and the frontend `Program.cs` note.
  - Cosmetic: the log line says "4 ocelot routes" (text copied from Q84b). The data row in `COMPOSE.tsv` is correct: "append 2 routes (275->277)".
- **Tree B** `~/mvp6-env/q65b/treeB/src`: **14,595 files**, 0 mismatch / extra / missing.
- **Kit K01 source tree = tree B byte for byte:** 14,595 files, 0 differing paths; derived overlay of 64 files, manifest in `lane-config/`.

## 3. Build (`BUILD-SUMMARY.tsv`, `logs/build/`)

| Tree | SupplyChainService.sln | Diten.Web | Diten.Web.Tests |
|---|---|---|---|
| A | 0 errors, 0 warnings | 0 errors, 15 warnings | 0 errors, 3 warnings |
| B | 0 errors, 0 warnings | 0 errors, 15 warnings | 0 errors, 3 warnings |

The warning sets are byte-identical (18 unique; `logs/build/warn-tree{A,B}.txt`). 0 warnings are in Returns files.

## 4. Tests (`TEST-SUMMARY.tsv`, `logs/test/`)

The environment came only from each tree's `scripts/test-env/mvp6-test-mongo-env.sh --slot 7 --rs rsq65bs7 --all-supplychain`. The lane mongod was `127.0.0.1:37994`, replica set `rsq65bs7`, `enableTestCommands=1`, dbpath `~/mvp6-env/q65b/test-mongo-s7`.

| Suite | A | B | New failures B vs A |
|---|---|---|---|
| Diten.Web.Tests | 158/158 | **257/257** (+98 overlay tests) | 0 |
| SupplyChainService.Tests | 415/417 | 424/426 (+9 overlay tests) | 0 |

- **`NavManifestL10nGuardTests`: PASS on B.** The Returns nav keys match `NavNameLocalizer.Normalize`.
- **The 2 SupplyChain failures occur on both trees:**
  - `ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery`: explicit restart mode required, by design.
  - `LoadAtomicityTests.UnknownCommitResultRetriesAndCommitsExactlyOnce`: fails on A and B here but passed on both in Q84b 70 minutes earlier, so it is intermittent (O-Q65b-3).

## 5. Runtime (`RUNTIME.tsv`, `browser/`, `png/` 44 files, `kit-a1/`)

- **Environment:** evidence kit v1.2, slot 7. Gateway 5700, Web 5701, Auth 5756, Platform 5757, MDM 5759, SupplyChain 5761, sink 5799, Mongo 37994 (`rsRetQ65b01`, DB suffix `RetQ65b01`).
- **Kit up:** PASS on the first attempt (K01–K07).
- **Browser path:** Web only. 0 foreign requests besides the shell's Google Fonts.
- **Fixture** `lane-scripts/ret_fixture.py` (attempt a3; see D-3):
  - roles on catalog keys: full = all Returns keys + `supplychain.shipments.read` (G-SHIPREAD); read-only = `.read`; no-read = `shipments.read` only; LE-B full;
  - MDM LE-A and LE-B, with OU, position and assignment, through the product APIs;
  - **3 Delivered shipments + 1 Dispatched shipment through the product Shipment API** (Planned → Dispatched → Delivered), each with a non-null lifecycle root.
- **K09:** PASS. 6/6 processes bound to tree `2cd1304e…`. The served `/assets/js/SupplyChain/Returns/index.js` = `1549e984…` = FILE-PLAN `v2_sha256`; `index.l10n.js` equals its source.

| Check | en | ar | Evidence | DB before/after (K10, strict) |
|---|---|---|---|---|
| **MOD-0186 happy path (RU-VS1):** list (GET 200) → Add → resolve a Delivered shipment → create 1 line → 201 Requested, row in the list → details (QuickView: RMA, status and id; **0 by-ID requests**, RU-29) → next step **Requested → Authorized** (row menu → transition panel → showConfirm → 200) → kept after reload | PASS | PASS | `happy-01…07-{en,ar}.png` | PASS: `returns +1, return_entitlements +1, returns_receipts +2, returns_audit +2, returns_outbox +2` |
| UI-PM-01: one shell with every partial; `dir` rtl in ar | PASS | PASS | `happy-01` | (same pair) |
| UI-PM-05 (**faked delay**, real response): skeleton display block, height > 0, opacity > 0 while pending; none after | PASS | PASS | `uipm05-*` | PASS 0 write |
| UI-PM-12 (**faked** list 500 and 200-malformed): localized error, support reference, skeleton hidden; Retry re-requests the real list (200) | PASS | PASS | `uipm12-*` | PASS 0 write |
| UI-PM-09: stable opener Add; Escape and the close button return focus to Add | PASS | PASS | `uipm09-*` | PASS 0 write |
| UI-PM-10: **real** 422 `RETURN_QUANTITY_EXCEEDED`; the panel stays open, focus is inside it (Save), and Escape closes it. The transition submit is exempt (showConfirm) | PASS | PASS | `uipm10-*` | PASS 0 write |
| **O-3**: see the O-3 result below | **FAIL** | **FAIL** | `o3-create-403-*`, `o3-transition-403-*`, `o3-readonly-list-*`, `o3-noread-list-*` | PASS 0 write |
| Q174 O-1 (informational; **faked** create 403): Add stays disabled right after the 403 and 3 s later (`disabled` attribute and class) | observed: stays disabled | same | `o3-create-403-*` | (same pair) |

O-3 result, by part:

- **Faked 403 on create:** the toast is localized and no raw error is shown, but **focus = BODY**. The title `<h5>` has no id and no `tabindex`.
- **Faked 403 on the transition:** also focus = BODY.
- **Real 403s:** PASS. The read-only actor sees no Add, and a direct POST returns 403. The no-read actor sees `_AccessDenied` only (0 table, filter or skeleton; no redirect), and a direct GET returns 403.

Informational: UI-PM-07 (row-menu transition panel) — focus lands on `#transitionTarget` inside the panel.

In every phase file: **0 native dialogs, 0 foreign requests, 0 page errors.** Superseded attempt: `browser/uipm-en-a1.json` stopped at UI-PM-09 (harness: Enter on the DataTables Add button does not open the panel, Q64d-D3). UI-PM-05 and UI-PM-12 had already passed. The harness was fixed (record Enter, open with Space) and re-run as a2 in its own zero-write pair.

## 6. NETCHECK 27017 (`NETCHECK-27017.txt`): **0**

- **Sampler:** 1,644 one-second samples; 0 new client ports; only pid 758 (dev mongod) was seen.
- **Dev mongod log:** 0 "Connection accepted" lines.
- **Kit netcheck:** 0 connections for every lane PID (2 runs).
- Port 57192 was never used.

## 7. Cleanup and FINAL-CHECK (`FINAL-CHECK.txt`, `kit-a1/CLEANUP-a1.tsv`)

- **Stop only.** 6 services and 2 mongods (test and kit) were stopped; the 8 lane ports have no listener; 0 lane processes remain.
- **Kit seal:** `kit-a1/SECRET-SCAN-FINAL-a1.txt`, artifacts_verified PASS, result PASS. The supervisor then shut down.
- **Root pattern scan:** `SECRET-SCAN-ROOT.txt`, 188 units, 0 hits.
- **Storage states:** truncated to 0 bytes, not deleted.
- **Deletions:** no rm, no rmdir, and no kit K11.
- **Repo:** this lane's only `git status --porcelain` change vs the start is the new evidence folder. Four other untracked entries appeared during the run from **other lanes**, not from this session: the CT verdict on Q84b, `mvp6-sop-ui-draft-03/`, `mvp6-capacity-ui-draft-03/` and `mvp6-q184-ver-draft-03/` (birth times 23:12–23:34 +03). They were not touched (`FINAL-CHECK.txt`).

## 8. Deviations

| ID | What | Why / effect | Reference |
|---|---|---|---|
| D-1 | `_shared-integration` items applied to the environment copy (provider line, 2 routes, nav keys × 7) | The draft README allows it for Q65b. Without it there are no routes or registration | `COMPOSE.tsv` rows `L6-env` |
| D-2 | Config: `supplychain Returns__ReferenceBaseUrl = http://127.0.0.1:5700/` (the lane Gateway) | `ReturnReferenceReader.cs:71` needs `Returns:ReferenceBaseUrl`; no appsettings value exists. Config only (same as the Q64d Claims precedent) | `lane-config/overrides.tsv` |
| D-3 | **Lane Auth DB setup write:** one permission-catalog document `supplychain.returns.transition` (Module `reverse-logistics`, Resource `returns`, Action `transition`), cloned from the synced `supplychain.returns.authorize` document; before 0 → after 1 | The API requires it on every transition (`ReturnsController.cs:20`), but no manifest registers it (pack §33 open gap 3; M-03 "API-only allow-list"). It never reached the catalog (fixture a1: absent after 180 s), so no role could hold it. Without D-3 the pack's next workflow step cannot run for any real user. Data in the lane DB only; no code change (as Q64d D-3). Fixture a2 was rejected by the unique index (no write) | `kit-a1/raw/fixture/ret-fixture-a{2,3}.json` (`laneAuthPermissionInsert`), `lane-scripts/ret_fixture.py` vs `.a1`/`.a2` |
| D-4 | Faked browser responses: UI-PM-05 (delay only), UI-PM-12 (list 500 and malformed), O-3 and O-1 (create and transition 403) | A real failing list or a 403 for an actor who holds the key cannot be produced on demand. Each row is marked "FAKED" in `RUNTIME.tsv`. UI-PM-10 and the real-403 rows are real | `lane-scripts/ret_runtime.mjs` |
| D-5 | Harness attempt `uipm-en-a1` superseded by a2 | Enter on the DataTables Add button does not open the panel (Q64d-D3). a2 records `enterOpensPanel: false` and opens the panel with Space | `browser/uipm-en-a1.json`, `lane-scripts/ret_runtime.a1.mjs` |
| D-6 | K11 not run; `lane-config/stop-lane.sh` did the stop-only cleanup | Prompt step 8 and the owner no-rm rule | `kit-a1/CLEANUP-a1.tsv`, `lane-config/up-q65b.sh` |
| D-7 | The kit supervisor ran with `~/mvp6-env/venv` first on PATH | K07 needs `bcrypt` (Q84b D-4); first attempt PASS | `logs/kit/kit-up-a1.log` |

## 9. Findings for CT

| ID | Sev | What | Proposed owner |
|---|---|---|---|
| **F-Q65b-1** | HIGH | `supplychain.returns.transition` is required by the API on every transition, but no manifest registers it, so the catalog never has it. On an integrated target, **no user can transition a Return** (403). This run proceeded only through D-3 | pack owner + integration owner (pack §33 gap 3: register the key, or change the API guard) |
| **F-Q65b-2** | MEDIUM (O-3 FAIL) | After a 403 on create, `closeCreateAsForbidden` disables Add, and the `hidden` handler focuses the disabled Add, so focus falls to BODY. After a 403 on a transition, focus also falls to BODY. The page title `<h5>` has no id and no `tabindex`. Fix as S&OP v2 did: give the title a stable id and `tabindex="-1"`, and focus it when the opener is disabled or removed | frontend-ui-ux (draft v3) |
| O-Q65b-1 | INFO | Q174 O-1: Add stays disabled after a 403 on create (until reload) | CT decides |
| O-Q65b-2 | LOW | Enter on the focused DataTables Add button does not open the panel; Space does (shared template, as Q64d-D3) | template owner |
| O-Q65b-3 | INFO | `LoadAtomicityTests.UnknownCommitResultRetriesAndCommitsExactlyOnce` is intermittent: it failed on A and B here and passed on both in Q84b | Loads owner / testing |
| O-Q65b-4 | INFO | The shell sidebar is empty in the lane for every module (as O-Q64d-1) | environment / Platform |

## 10. Counts

- **Build:** 6/6 targets, 0 errors; warnings A = B.
- **Tests:**
  - Web: A 158/158, B 257/257;
  - SupplyChain: A 415/417, B 424/426;
  - **0 new failures.**
- **Runtime:** 18 check rows (9 checks × en and ar). **16 PASS, 2 FAIL (O-3 en and ar).** Plus 3 logins PASS and the O-1 observation. 7 K10 pairs PASS (2 write, 5 zero-write). 44 PNGs.
- **NETCHECK 27017:** 0.

## 11. Files

`REPORT.md` · `COMPOSE.tsv` · `BUILD-SUMMARY.tsv` · `TEST-SUMMARY.tsv` · `RUNTIME.tsv` · `NETCHECK-27017.txt` · `FINAL-CHECK.txt` ·
`SECRET-SCAN-ROOT.txt` · `SHA256SUMS` · `logs/` · `png/` (44) · `browser/` · `kit-a1/` (sealed) · `lane-config/` · `lane-scripts/` · `tools/`.

No tracked repo file was edited. No write outside the Allowed Paths. No rm or rmdir. No ledger write. commit YOK, push YOK. No prompt was written for another WP.
