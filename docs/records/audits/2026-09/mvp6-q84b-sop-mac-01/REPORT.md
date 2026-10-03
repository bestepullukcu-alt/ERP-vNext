# Q84b — MOD-0190 S&OP Mac build + test + runtime: BASE-STACK v2 + S&OP overlay v2 (SOP v2.5 §17/§36.2)

**Agent verdict: FAIL.** The failure is one acceptance line: **new test failures vs baseline = 1, not 0.** It is a Web.Tests nav-l10n
guard, caused by the draft's navigation-key fragment. Every other line passes:

- preflight P1–P6;
- compose (14,572 files, overlay rows hash-exact);
- build (0 errors);
- runtime (all checks PASS in en and ar, with DB before/after evidence);
- NETCHECK 27017 = 0;
- FINAL-CHECK clean;
- SHA256SUMS present.

Agent PASS/FAIL ≠ CT ACCEPTED — returning to CT.

| Field | Value |
|---|---|
| WP / prompt / template | Q84b · Prompt Q84b v5 · T2 v1 · role T2 Mac build/test/runtime (SOP v2.5 §17/§36.2) · CT-QUEUE row Q84b (READY, Q178) |
| Where | Claude app → Code tab → Local session, folder ERP-vNext-recovery, on the Mac (`uname -s` = Darwin). .NET SDK 8.0.417 / runtime 8.0.23, mongod 8.0.18, Playwright (Chromium, headless), node 25.5.0 |
| Repo | `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`, read-only. `GIT_OPTIONAL_LOCKS=0`; `git status --porcelain` only; no `git diff`, add, commit or push |
| Base stack | BASE-STACK v2 `docs/records/audits/2026-09/mvp6-base-stack-v2/BASE-STACK-v2.md` sha256 `ce8d60ab959ec7018c255ab1152983cfd8e301390f54b3d0ba23961bf2c4d882` (BASE a8a236de → Q117 83e6322c → Q121 93bf1c07 → Q131 4a4a0860 → S&OP overlay v2 7a9666c7…) |
| Overlay | `mvp6-sop-ui-draft-02/sop-ui-draft-overlay-v2.tar.gz` `7a9666c7ee1dfab4f85c1324db38ab06ed87ed1c385de706629e60ed68e394e3` · SHA256SUMS `0bbb22a8…6cc1` · FILE-PLAN `a4e093ec…d56a` |
| Pack / UI rules | MOD-0190 `c076790d…5142` · `.antigravity/agents/frontend-ui-ux.md` `90247ddc…f044` (checklist r2; OD-F-Q145-1 narrow UI-PM-07/09/10; OD-Q164-09) |
| Workspace | `~/mvp6-env/q84b/`. New folders only; nothing under it was deleted. Kept as evidence |
| Time | start 2026-09-30 22:21:35 +03 (preflight) · end 2026-09-30 22:58 +03 |

## 1. Preflight (all PASS)

| # | Check | Result |
|---|---|---|
| P1 | `uname -s` | `Darwin` |
| P2 | `.git/index.lock` | absent; `GIT_OPTIONAL_LOCKS=0` on every git call; `git status --porcelain` only |
| P3 | `git rev-parse HEAD` | `4a8d4d4b339528a88e6220fb8402e5a2c771136c` |
| P4 | sha256 of the 6 inputs | all 6 equal the prompt values: Base Stack `ce8d60ab…`, overlay `7a9666c7…`, SHA256SUMS `0bbb22a8…`, FILE-PLAN `a4e093ec…`, pack `c076790d…`, UI rules `90247ddc…` |
| P5 | `mvp6-q84b-sop-mac-01/` | absent at start |
| P6 | other Mac build/test run | none. The only related process was the owner's Homebrew mongod on 27017 (pid 758, running since Tuesday; not a lane run) |

Also checked before use: the SHA256SUMS of all 7 input evidence folders pass (93/93 lines OK). Each folder's hash matches BASE-STACK v2 `LAYERS.tsv`:

- base-stack-v2 2/2;
- Q103 14/14;
- Q117 12/12;
- Q121 3/3;
- Q131a 5/5;
- sop-ui-draft-01 55/55;
- sop-ui-draft-02 4/4 (`shasum -a 256 -c`).

## 2. Compose (`COMPOSE.tsv`, `logs/compose/COMPOSE-LOG.txt`, `tools/compose_q84b.py`)

The recipe is BASE-STACK v2 §4: cp only, fail-closed, never deletes.

- **Tree A** (base-only baseline = BASE-STACK v2):
  - `~/mvp6-env/base/src` was cloned (`cp -c -Rp`). It matches BASE-MANIFEST `a8a236de…` 14,566/14,566 before any layer.
  - Q117 (10 replaced, 1 added) → Q121 (2 replaced) → Q131 (15 replaced, 5 added). Every preimage was checked, including the Q131 preimage of `CapacityRestartTests.cs` = the Q117 postimage.
  - Result: **14,572 files, 0 mismatch / extra / missing.**
- **Overlay v2:**
  - The archive was extracted to `stage/sop-v2/`. Its bytes were checked against the archive members; the v1 archive `fcf52d82…` was also extracted.
  - **FILE-PLAN 11/11:** v1 member = `v1_sha256` and v2 member = `v2_sha256`. The other 36 files are byte-equal to v1.
  - All 34 module files are **new tree paths** (before = absent, allowed by the prompt); after = the v2 member.
- **L6 environment integration** (the draft's `_shared-integration` README allows this "for the isolated environment only (Q84b)"; environment copy only):
  - SupplyChain `Program.cs`: `AddSingleton<IModuleManifestProvider, SopWorkflowSignoffsManifestProvider>()` after the Shipment provider line;
  - `ocelot.json`: the 4 S&OP routes (275 → 279);
  - `SharedResource.{7}.resx`: the 2 nav keys each.
  - Not applied: the icon-map proposal, the platform checklist, and the frontend `Program.cs` note (no change needed).
- **Tree B:** 14,606 files (14,572 + 34), 0 mismatch / extra / missing. Both trees were set read-only; the builds ran in clones.
- **Attempt 1** (`~/mvp6-env/q84b/A`, `/B`) composed the same trees but stopped while writing the log (the `out/` folder was missing). It is kept. The valid compose is `~/mvp6-env/q84b/c2/`.

## 3. Build (`BUILD-SUMMARY.tsv`, `logs/build/`)

| Tree | SupplyChainService.sln | Diten.Web | Diten.Web.Tests |
|---|---|---|---|
| A | 0 errors, 0 warnings | 0 errors, 15 warnings | 0 errors, 3 warnings |
| B | 0 errors, 0 warnings | 0 errors, 15 warnings | 0 errors, 3 warnings |

The A and B warning sets are byte-identical (18 unique; `logs/build/warn-{A,B}.txt`). **0 warnings are in S&OP files.**

## 4. Tests (`TEST-SUMMARY.tsv`, `logs/test/`, TRX)

The environment came only from each tree's own `scripts/test-env/mvp6-test-mongo-env.sh --slot 8 --rs rsq84bs8 --all-supplychain`. The lane mongod was `127.0.0.1:38994`, single-member replica set `rsq84bs8`, `--setParameter enableTestCommands=1`, dbpath `~/mvp6-env/q84b/test-mongo-s8`. `DITEN_PLATFORM_TEST_MONGO_URI` and `MVP6_MOD0192_MONGO_URI` were set by the script.

| Suite | A (baseline) | B (composed) | New failures B vs A |
|---|---|---|---|
| Diten.Web.Tests | 158/158 | 215/216 (+58 overlay tests) | **1** |
| SupplyChainService.Tests | 416/417 | 425/426 (+9 overlay tests) | 0 |

- **New failure (B only), F-Q84b-1:** `NavManifestL10nGuardTests.Every_nav_visible_manifest_page_has_a_Nav_Page_key_in_all_seven_languages`.
  - Message: "7 nav l10n key problem(s) … `Nav.Page.SANDOPPLANS` MISSING" in en/tr/fr/es/zh/ar/ru.
  - Cause: the runtime `NavNameLocalizer.Normalize` turns page code `SANDOP_PLANS` (`SopWorkflowSignoffsManifestProvider.cs:30`) into `Nav.Page.SANDOPPLANS`. The draft's fragments `_shared-integration/sharedresource-nav-keys/SharedResource.{7}.resx.fragment.xml` add `Nav.Page.SANDOP_PLANS`, which the runtime never looks up.
  - The module key `Nav.Module.SOPWORKFLOWSIGNOFFS` is correct.
  - Without the fragment the guard would fail too, because the key would be absent.
  - Proposed fix (draft v3, not applied here): rename the fragment key to `Nav.Page.SANDOPPLANS` in all 7 fragments, and correct `platform-registration.md` item 5.
- **Pre-existing failure (A and B):** `ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery` fails with "Explicit write/read restart mode required; exclude from ordinary suite". It fails the same way on the baseline, by design (as in Q64d).

## 5. Runtime (`RUNTIME.tsv`, `browser/`, `png/` 56 files, `kit-a3/`)

- **Environment:** evidence kit v1.2 files in place, unchanged. Lane driver `lane-config/up-q84b.sh` = Q64d `up-q64d.sh` plus the K11 change in D-3.
- **Slot 8:** Gateway 5800, Web 5801, Auth 5856, Platform 5857, MDM 5859, SupplyChain 5861, sink 5899, Mongo **38994** (`rsSopQ84b01`, DB suffix `SopQ84b01`).
- **Browser path:** Web only (127.0.0.1:5801). 0 foreign requests besides the shell's Google Fonts.
- **Kit up:** attempt 3 PASS (K01, K02, K03, K04b, K04, K05, K06 ×6, netcheck, K07pre, K07).
  - **K01 source tree = tree B byte for byte:** 14,606 files, 0 differing paths. The K01 manifest is `a28253c0…`; the derived overlay has 75 files (32 base-stack fix files + 34 module + 9 environment), with its manifest in `lane-config/`.
- **Fixture:** `lane-scripts/sop_fixture.py`, derived from the Q64d org fixture parts A and B.
  - The 4 S&OP keys reached the Auth catalog through the S&OP manifest provider's self-registration within 1.3 s.
  - Roles were built on existing keys only: full = 4 keys; read-only = `.read`; no-read = none; LE-B full.
  - MDM LE-A `134756de…` and LE-B `6d95219f…` were created through the product APIs, together with the OU, position and assignment.
  - Every actor token carries the expected `legal_entity_id` (`kit-a3/raw/fixture/sop-fixture.json`).
- **Freshness (K09):**
  - The served `index.js` `a4a08426…`, `details.js` `2e269356…`, `index.l10n.js` and `details.l10n.js` are byte-equal to their source. The first two equal FILE-PLAN `v2_sha256`.
  - Every DLL matches its build. 6/6 services were listening on their build.
  - The K09 run reports "FAIL: supplychain" only for the stale first-launch row of SupplyChain (D-2). The relaunched pid 47008 row is `match / listening`.

| Check | en | ar | Evidence (PNG + browser JSON) | DB before/after (K10, strict) |
|---|---|---|---|---|
| MOD-0190 happy path: entry (no list request) → create → details → capture snapshot → sign-off via showConfirm → reload (sign-off kept, status In Review) | PASS | PASS | `happy-01…08-{en,ar}.png`, `browser/happy-{en,ar}-a1.json` | PASS: `sandop_plans +1, snapshots +1, sign_offs +1, receipts +3, audit +3, outbox +3` (exact, whole-DB totals) |
| UI-PM-01: one shell on entry and details with every partial rendered; `dir` rtl for ar | PASS | PASS | `happy-01`, `happy-08` | (same pair) |
| UI-PM-05: skeleton `display:block`, height > 0 while the section GETs are pending (delayed 3 s in the browser); `none` after | PASS | PASS | `uipm05-*` | PASS 0 write |
| UI-PM-12: summary 503, snapshots 500 and sign-offs 200-malformed show the error state (skeleton hidden, localized text, support reference). The retry of each section re-requests it and shows the content | PASS | PASS | `uipm12-*` | PASS 0 write |
| UI-PM-09 (r2, OD-F-Q145-1): stable openers Create plan (Escape and close button), Capture snapshot and Record sign-off get focus back on hidden | PASS | PASS | `uipm09-*` | PASS 0 write |
| UI-PM-10 (r2, OD-F-Q145-1): **real** 422 `INVALID_DEMAND_REFERENCE` on create and on capture. The panel stays open, focus is inside it (first field), inputs are kept, and Escape closes it. The sign-off submit is exempt (showConfirm) | PASS | PASS | `uipm10-*` | PASS 0 write |
| O-3: see the O-3 notes below | PASS | PASS | `o3-*` | PASS 0 write |

O-3 covers these cases:

- **Crafted 403 on create:** focus goes to `#sandopPlansTitle`, the CTA is removed, and the toast is localized.
- **Crafted 403 on capture:** focus goes to `#planTitle`, the button is removed, and the workspace is kept.
- **Crafted 403 on the page GET:** a localized `#planAlert` is shown, the title is shown, and the workspace is hidden.
- **No raw error text** in any of these cases.
- **Real 403s:**
  - read-only: no CTA, and a direct POST returns 403 `FORBIDDEN`;
  - no-read: entry and details show `_AccessDenied` only (0 skeleton or form), with no redirect, and a direct GET returns 403.
- **Information only:** a sign-off 403 through showConfirm removes the opener and focuses the title.

In every phase file: **0 native dialogs, 0 foreign requests, 0 page errors.** The only console entries are the browser's "Failed to load resource" lines for the intended 4xx/5xx responses. Superseded attempt: `browser/uipm-en-a1.json` has a UI-PM-12 FAIL that was a **harness defect**. Every `.js-retry` calls `loadAll()` (`details.js:712-714`), so one click reloaded all three sections, and the harness waited for per-section requests. The product behaviour is correct. The harness was fixed (`lane-scripts/sop_runtime.mjs` vs `sop_runtime.a1.mjs`) and re-run as a2 in its own zero-write pair.

## 6. NETCHECK 27017 (`NETCHECK-27017.txt`): **0**

- **lsof sampler:** 1,667 one-second samples from before the build to after cleanup. 0 new client ports; only the 18 pre-existing dev connections were seen.
- **Dev mongod log:** 0 "Connection accepted" lines in the window.
- **Kit netcheck:** 0 connections to 27017 for every lane PID, 3 runs.
- **Configuration:** K04 "no 27017". Port 57192 was never used.

## 7. Cleanup and FINAL-CHECK (`FINAL-CHECK.txt`, `kit-a3/CLEANUP-a1.tsv`)

- All lane services and both lane mongods (the test mongod and the kit mongod) were stopped, the mongods with `shutdownServer`. The 8 lane ports have no listener, and no lane process remains.
- **Kit seal:** `kit-a3/SECRET-SCAN-FINAL-a1.txt`, `artifacts_verified PASS`, `result PASS`. The supervisor then shut down and its in-memory lane values were cleared.
- **Root pattern scan:** `SECRET-SCAN-ROOT.txt`, 268 units, 0 hits.
- **Browser storage states:** truncated to 0 bytes in `~/mvp6-env/q84b/state/`, not deleted.
- **Repo:** HEAD unchanged, no `index.lock`, and `git status --porcelain` differs from the start only by the new evidence folder.

## 8. Deviations (each with its file reference)

| ID | What | Why / effect | Reference |
|---|---|---|---|
| D-1 | L6 environment integration applied to the environment copy (provider line, 4 routes, 2 nav keys × 7) | The draft README allows it for Q84b only. Without it there are no routes and no registration, so no runtime. Tree B = BASE-STACK v2 + overlay v2 + L6 | `COMPOSE.tsv` rows `L6-env` |
| D-2 | SupplyChain ran with `ASPNETCORE_ENVIRONMENT=Testing` plus `Sandop__DemandFixtures__0__*` = T1 / LE-A / `DP-Q84B-01` / `1` / `sha256:q84b-demand-01` / published, and was relaunched once after the org fixture | `Program.cs:78-81` loads S&OP demand fixtures only in `Testing`, and the fixture needs the lane LE id that exists only after the org fixture. Config only; no code or appsettings change. The first SupplyChain PID became a zombie until the supervisor exited, so the helper's `kill -0` check stopped it; the remaining launch and netcheck steps were run by hand. K09 shows the stale first row | `lane-config/overrides.tsv`, `lane-config/relaunch-supplychain.sh`, `kit-a3/SOURCE-BINARY-PROCESS.tsv` |
| D-3 | K11 was not run. The kit's `rm -rf` of the lane workspace (and the abort path's K11) was replaced by `lane-config/stop-lane.sh` (the same ownership-checked stop and port checks, no deletion) | Owner rule "no rm, use new folders". `~/mvp6-env/q84b` stays; prompt step 8 allows this | `kit-a3/CLEANUP-a1.tsv`, `lane-config/up-q84b.sh` |
| D-4 | Kit `up` needed 3 attempts | a1: K07 failed because the supervisor's Python lacked `bcrypt`; re-run with `~/mvp6-env/venv` first on PATH (as Q64d). a2: K03 found 38994 not yet bindable just after the a1 mongod shutdown. a3: PASS. Each attempt had its own `ek-work-aN` and evidence folder (`kit/` = a1, `kit-a2/`, `kit-a3/`); the a1 lane mongod was stopped with `shutdownServer` after an ownership check | `logs/kit/kit-up-a{1,2,3}.log`, `kit/`, `kit-a2/` |
| D-5 | Crafted responses in the browser (`route.fulfill` / delayed `route.continue`) for UI-PM-05, UI-PM-12 and the O-3 403s | These states cannot be reached with the real services on demand. API responses were replaced, never assets (Q64d A-1). UI-PM-10 used real 422s | `lane-scripts/sop_runtime.mjs` |
| D-6 | Harness attempt `uipm-en-a1` (UI-PM-12 retry assumption) was superseded by a2 | Harness defect, not a product defect (§5) | `browser/uipm-en-a1.json`, `RUNTIME.tsv` last row |
| D-7 | **One `rmdir`:** the empty `runtime/` sub-folder that this run had created inside the new evidence folder was removed after its two sub-folders were moved to `png/` and `browser/` | Slip against the owner no-rm rule. It held no file, and no file was lost | this report |
| D-8 | Compose attempt 1 stopped at the log write (the `out/` folder was missing). The trees were composed again in a new folder `c2/` | No-rm rule: attempt 1 is kept | `tools/compose_q84b.a1.py`, `~/mvp6-env/q84b/{A,B}` |

## 9. Findings for CT

| ID | Sev | What | Proposed owner |
|---|---|---|---|
| **F-Q84b-1** | MEDIUM (blocks the acceptance line "new failures = 0") | Nav page key `Nav.Page.SANDOP_PLANS` in the draft fragments ≠ runtime key `Nav.Page.SANDOPPLANS`. The guard fails in 7 languages, and the sidebar would print raw English | frontend-ui-ux (draft v3), plus the integration owner for `platform-registration.md` item 5 |
| O-Q84b-1 | INFO | The shell sidebar menu is empty in this lane for every module (as O-Q64d-1), so F-Q84b-1 is not visible at runtime. Page routing works | environment / Platform owner |
| O-Q84b-2 | INFO | In ar, the shared DataTables search placeholder "Search..." stays English (shared DtDefaults, not S&OP) | template owner |
| O-Q84b-3 | LOW | Kit: `relaunch` has no supervisor op, and `kill -0` treats the supervisor's unreaped child as alive (D-2). K09 keeps the stale first row after a relaunch | kit owner |
| O-Q84b-4 | INFO | The pack text (§23.2) vs UI-PM-01 conflict raised by Q158 (A1) is still open. The runtime confirms that partials without Layout render one shell | CT / pack owner |

## 10. Counts

- **Build:** 6/6 targets, 0 errors (A and B), warnings A = B.
- **Tests:**
  - Web: A 158/158, B 215/216 → **1 new failure**;
  - SupplyChain: A 416/417, B 425/426 → 0 new failures.
- **Runtime:** 18 check rows (9 checks × en and ar) **all PASS**, plus 3 logins PASS. 7 K10 pairs PASS (2 write, 5 zero-write). 56 PNGs.
- **NETCHECK 27017:** 0.

## 11. Files

`REPORT.md` · `COMPOSE.tsv` · `BUILD-SUMMARY.tsv` · `TEST-SUMMARY.tsv` · `RUNTIME.tsv` · `NETCHECK-27017.txt` · `FINAL-CHECK.txt` ·
`SECRET-SCAN-ROOT.txt` · `SHA256SUMS` (covers every other file) · `logs/` (compose, build, test incl. TRX, kit up, net) · `png/` (56) ·
`browser/` (harness phase files) · `kit/` (attempt 1) · `kit-a2/` · `kit-a3/` (valid run: COMMANDS, K01–K10 raw, db pairs, CLEANUP, seal) ·
`lane-config/` · `lane-scripts/` · `tools/`.

No tracked repo file was edited. There were no writes outside the Allowed Paths and no ledger writes. commit YOK, push YOK. No prompt was written for another WP.
