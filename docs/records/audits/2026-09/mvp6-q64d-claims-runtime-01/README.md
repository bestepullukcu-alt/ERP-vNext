# Q64d — MOD-0187 Claims UI DRAFT v2: runtime evidence on the accepted base (add-module Phase 4.5 + Phase 5 tests) — SOP §22

| Field | Value |
|---|---|
| WP / prompt / lane | WP-MVP6-187-UI-064d · Q64d v1 · INT environment lane (single); testing-agent `/test` with devops-agent (environment) |
| Where | Claude Code in the owner's macOS terminal (Darwin), .NET SDK 8.0.417 / runtime 8.0.23, mongod 8.0.18, Playwright 1.59.1 (Chromium headless) |
| Repo | `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`, READ-ONLY. The only repo write is this folder. |
| Base (D1) | `~/mvp6-env/base/src` = Q103 `BASE-MANIFEST.tsv`, BASE-HASH `a8a236de82a9f2b5d4c0f3560aa13894c3740c6e187cb0140f5da1b08109c661`: **14566/14566 match, 0 mismatch, 0 missing, 0 extra** (`compose/base-verify.txt`). Not modified. |
| Draft under test | `mvp6-claims-ui-draft-02/claims-ui-draft-overlay-v2.tar.gz` `1a57c879d1d0970f703b089704a304af5981b309e0be787ac659d3f28be711a0` (hash checked before extract; draft SHA256SUMS 10/10) |
| Pack | MOD-0187 `96c9a0ae…` §32/§33 · acceptance text `docs/roadmap/plans/mvp6-ui-pack-drafts-01/claims/ACCEPTANCE.md` `e5b7f766…` |
| Workspace | `~/mvp6-env/q64d/` (src, v2x, inputs, lane, runtime, state, test-mongo, logs). New folders only; **no `rm`** (see §9 for the kit's own deletions) |
| Time | start 2026-09-26T23:57:14+03:00; end in `FINAL-CHECK.txt` |

## 1. Verdict (agent)

**The v2 draft builds, passes its own tests and passes the runtime acceptance on the accepted base, with two FAIL rows (CU-05
skeleton, CU-25 keyboard) and one PARTIAL row (CU-18 malformed-root branch not reachable at runtime).** Q64b's D-01, D-02, S-01 and S-02
are fixed at runtime. Save View/Reset passes. Isolation and denied-access checks pass. Status stays **DRAFT**; Claims is **not**
writer-complete, because D1/D2 need a v3 revision.

§32.11 rows (32): **28 PASS · 2 FAIL · 1 PARTIAL · 1 BLOCKED**. Two of the passes are qualified: CU-10 passes only with a lane-only role
grant (F-CU10 → Q114), and CU-27 passes on the environment copy. CU-30 passes under the owner's PNG decision, and CT disposes it.
Details are in `ACCEPTANCE.tsv` (41 rows, including D-01/D-02/S-01/S-02/Save View/isolation/denied/F8/§33).

Evidence level (SOP §32.0): **OBSERVED at runtime.** That means local real services, real Auth, an isolated replica set, headless
Chromium through Web only, and §25 freshness proof (K09). Build and tests were executed. The crafted-response rows (CU-03/04/05/13) are browser-only.
**Agent PASS ≠ CT ACCEPTED — returning to CT.**

## 2. Preflight and composition

- **Preflight:** `uname -s` = Darwin; `GIT_OPTIONAL_LOCKS=0`; no `.git/index.lock`; branch and HEAD as expected; 19 diff paths.
- **Compose:** `compose/compose_q64d.py` builds `~/mvp6-env/q64d/src`. Order: Q103 base copy (`cp -c -Rp`) → L5 v2 module files → L6 env
  integration. `compose/COMPOSE-LOG.txt` is the log.
  - **L5 module files:** 23 files, all at new paths (no base file overwritten).
  - **L6 env integration** (draft `_shared-integration`, environment copy only; before/after hashes in `compose/L6-BEFORE-AFTER.tsv`):
    1. SupplyChain `Program.cs`: `AddSingleton<IModuleManifestProvider, ClaimsManagementManifestProvider>` after the Shipment provider line (`33027bcd…` → `fc82ad49…`).
    2. `gateway/Diten.ApiGateway/ocelot.json`: 2 Claims routes appended (275 → 277); confirm-route `GET /api/shipment-bundle/shipments/{shipmentId}` present.
    3. `frontend/Diten.Web/Resources/SharedResource.{en,tr,fr,es,zh,ar,ru}.resx`: `Nav.Module.CLAIMSMANAGEMENT` + `Nav.Page.CLAIMS`.
    4. Not applied: `gateway-tests-ocelot-count.patch.txt`, because Gateway tests were not in scope. The icon map and platform checklist are proposals only.
  - The L6 manifest is `b45b83ff…`, **byte-identical to Q97's env-integration manifest**.
- **Kit K01:** composed the same tree independently: HEAD + BC-SOURCE `ebd5d80c…` + A12 `7b6a0d1a…` + Auth 22 `f50350b8…` + derived
  v2-module `d73be494…` + env-integration `bc398cb8…` (`lane-config/overlays.tsv`; `compose/DERIVED-INPUTS.sha256`).
  - Tree manifest `3b5af23e9a4bb8d7e9e2541394bc46c1bd122b03dcaadb29c493f9547f9382d4`: **14589 files = my compose byte for byte**,
    which is the base + 23 added + 9 changed (`compose/k01-vs-src.txt`).

## 3. Build and tests (in `~/mvp6-env/q64d/src`; logs and TRX in `logs/`)

| Target | Result |
|---|---|
| SupplyChainService.sln | 0 errors, 0 warnings |
| Diten.Web.Tests (builds Diten.Web) | 0 errors, 18 warnings (same 15+3 as Q103; **0 in Claims files**) |
| Diten.ApiGateway.Tests | 0 errors, 5 warnings (built; not run) |
| TenantArchitecture.ArchitectureTests | 0 errors, 0 warnings |
| **SupplyChain Claims + manifest provider** (test mongod `127.0.0.1:57192` `rsmod192`, `enableTestCommands=1`, lane dbpath) | **137/138**. The 1 is `ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery`, excluded by design ("explicit restart mode required") |
| **Diten.Web.Tests** | **242/242** (Claims 84/84) |
| **Architecture** | **15/18**. Same 3 as HEAD (Q103-N2: JwtClockSkew ×2, MongoTestDatabaseGuard) |
| `verify_datatable_page.py --reference slim` (record only, CU-SCR-06) | 70 PASS / 21 FAIL (= Q64c; `kit/raw/verify_datatable_page-a1.txt`) |

The test mongod was shut down cleanly after the tests. Its data folder `test-mongo-000151/` stays.

## 4. Runtime environment (evidence kit v1.2, `kit/`)

- **Slot and ports:** slot **7**: Gateway **5700**, Web 5701, Auth 5756, Platform 5757, MDM 5759, SupplyChain 5761, sink 5799, Mongo 37994
  (`rsClaimsQ64d01`, DB suffix `ClaimsQ64d01`).
- **Browser path:** Web only (`127.0.0.1:5701`) → Gateway 5700 → services. The browser never called the Gateway or a service port.
- **Gateway port:** the owner chose the kit slot Gateway instead of literal 5000 (question tool, 2026-09-27 ~00:05).
- **`up`:** PASS on the first attempt. It used `lane-config/up-q64d.sh` = `run-kit.sh` plus the Q97 K07pre tenant-fixture step (K-F1; same one-step deviation as Q97).
- **Kit checks:** K02, K03, K04 (pairing, no literal secret, no 27017), K05, K06 (6/6 build and launch), netcheck (0 × 27017) and K07 (7 identities) all PASS.
- **Hand-off conditions:**
  - `claims-management`, `shipment-tracking-pod` and `carrier-management` self-registered with Platform. MDM's two modules registered.
  - MDM audit appends → **202** ×6 (`kit/raw/logs-excerpt/registration-audit-excerpt.log`).
- **Freshness (SOP §25), K09 PASS:**
  - 6 services are `match`/`listening` on tree `3b5af23e…`. Every binary hash at K09 equals its K06 Release build, and every PID is the listener.
  - The served `/assets/js/SupplyChain/Claims/index.js` = **`6a4c01cf…` = the v2 archive file**. `index.l10n.js`, `personalization-client.js`
    and `dt-defaults.js` equal their source bytes (`kit/SOURCE-BINARY-PROCESS.tsv`, `kit/raw/browser-binding.tsv`).
- **Fixtures** (`lane-scripts/org_fixture.py` + `org_fixture_cont.py`, run by the supervisor; ids and statuses only in `kit/raw/fixture/`):
  - roles on existing catalog keys;
  - MDM LE-A, LE-B, LE-T2; Platform OU, positions and assignments;
  - 1 carrier, 9 shipments (incl. NULLROOT, BADROOT, FLOW) and 9 seed claims, through the product APIs;
  - lane-DB-only: carrier link, soft deletes.
  - Attempt 1 stopped at `claim-SETTLE1-Approved` 403: my fixture-admin role lacked `supplychain.claims.decide`, which is correct product
    behaviour. The continuation added `decide` to that setup role in the lane Auth DB and ran only the missing steps (K10 pair `fixture-cont` PASS).
- **K10 pairs:** 21 pairs around every phase and setup step (strict whole-DB rule; `kit/raw/db/`). **18 PASS, 3 FAIL**; each failure is explained:
  - `fixture`: stopped fixture attempt 1, above;
  - `cu20`: a harness step stopped after the two 200 approvals; expected +3, observed +2. See CU-20 in ACCEPTANCE;
  - `spec`: zero write, because no test ran (CLI-path error). `spec2` is the valid run.
- **Actors** (real Web login, separate contexts): full (LE-A), read-only, no-read, LE-B full, T2 full. All auth cookies are `httpOnly`.
  The storage-state files lived only in `~/mvp6-env/q64d/state-001438/` (0600) and were **truncated to 0 bytes** at the end, not deleted.

## 5. Results

`ACCEPTANCE.tsv` has the full per-row table. Headlines:

- **Q64b defects fixed at runtime:**
  - **D-01:** the list reloads after create, filter and transition; 0 `xhr.abort` errors.
  - **D-02:** one shell (1 layout wrapper, 1 `main.js`, 1 footer).
  - **S-01/S-02:** v2 spec **16 passed / 0 failed / 1 skipped** (CU-19 outline, by design).
- **Save View / Reset:** Apply → dirty, Reset → factory + clean, Save → clean (POST 201), reload restores, Reset against a saved default → dirty. 0 JS errors.
- **Rows Q97 did not run:**
  - CU-03, CU-04, CU-12, CU-13, CU-19 (HTTP and browser fault proxy), CU-20, CU-22, CU-24 (7 languages) and CU-26 **PASS**.
  - CU-05 and CU-25 **FAIL**. CU-18 **PARTIAL**.
- **Isolation:** cross-LE, cross-tenant and soft-deleted claim/shipment all PASS, with identical safe-not-found shapes.
- **Denied:** 401 JSON with no redirect; no-read gets `_AccessDenied` + 403; read-only gets no CTA and a direct 403. All PASS.
- **F8:** 0 root leaks in 20 phase files. **0 native dialogs.** The only foreign requests are the shell's Google Fonts.
- **PNGs:** 58, listed in `kit/PNG-INDEX.tsv`:
  - 390/768/1024/1280/1440 for en and ar (RTL);
  - 1280 for tr/fr/es/zh/ru;
  - one per scenario, plus the spec's tr/ar screens.

## 6. Findings (proposed fixes only; no code changed)

| ID | Sev | What | Evidence | Proposed fix | Owner |
|---|---|---|---|---|---|
| **Q64d-D1** | MEDIUM | **Skeleton never visible** (CU-05). Shared CSS `backbone-custom.css:353` sets `.backbone-skeleton{display:none}`. Golden pages show the skeleton with jQuery `fadeIn` (`dt-defaults.js:580`). Claims `setListState` only toggles `d-none` (`index.js:245`), so the first load shows an empty area. | `kit/png/cu05-skeleton-en.png`; `kit/browser/crafted-a1.json` | In `setListState`, also set `skeleton.style.display = state === 'skeleton' ? 'block' : 'none'` (or use the same fadeIn/fadeOut as dt-defaults). Add a JS contract test. | frontend-ui-ux (v3 draft) |
| **Q64d-D2** | MEDIUM | **Transition offcanvas has no keyboard focus and Escape does not close it** (CU-25). It is opened from a row dropdown item. Focus stays outside the surface, and Bootstrap's Escape handler listens on the offcanvas element. The create offcanvas works. | `kit/browser/cu20b-a1.json` CU-25c; `cu20-a1.json` error | On `shown.bs.offcanvas`, focus `#transitionTarget` (or the first enabled field). Optionally open the surface after the dropdown's `hidden.bs.dropdown`. Add a Playwright keyboard row. | frontend-ui-ux (v3 draft) |
| **Q64d-D3** | LOW | **Enter on the focused Add button does not open create; Space does.** Golden slim wires Add the same way (`click` listener on `.add-new`, `GoldenReferenceSlim/index.js:955`), so this is a **shared DataTables Buttons / template pattern**. | `kit/browser/kbd-a1.json` | Template owner: pass the Add action through the button config (`action:`) or handle `keydown Enter`. Check golden slim and compact. | frontend template owner (not a module fix) |
| **Q64d-D4** | LOW | After the create offcanvas closes, focus does not return to Add. | `kit/browser/kbd-a1.json` | Return focus to the invoking element on `hidden.bs.offcanvas`. | frontend-ui-ux (v3 draft) |
| **CU-18-P** | INFO | The malformed-root 502 branch cannot be reached at runtime. With a malformed root in the lane DB, the Shipment API returns **500** (it cannot serialise the value), and Claims maps a producer 5xx to 503 `CLAIM_REFERENCE_UNAVAILABLE` (pack A3). | `kit/raw/logs-excerpt/cu18-root-seam-excerpt.log` | Keep it as unit evidence (`ClaimReferenceTests`, passed), or add a reference-base fault stub in a later lane. CT disposition. | CT / testing-agent |
| F-CU10 | MEDIUM | Carrier-linked create needs `supplychain.carriers.read` on the actor. Reproduced: 503 with zero write without it, 201 with a lane-only grant. | `kit/browser/checks-mut-a1.json` / `-a2.json` | Pack decision **Q114** (role prerequisite). No code change. | pack owner / CT |
| O-01 | LOW | 401 JSON body code is `INVALID_REQUEST`; pack §32.8 expects `UNAUTHENTICATED`. | `kit/browser/checks-neg-a1.json` | Shared frontend `Program.cs` (A12 layer), integration owner. | integration owner |
| O-Q64d-1 | INFO | The shell sidebar menu is empty for every module in this lane, not only Claims. Page-level routing works. | any `kit/png/*-en.png` at 1280 | Platform nav/menu registration in lane environments; not a Claims defect. | environment / Platform owner |

## 7. Harness notes (lane scripts, not product)

`lane-scripts/claims_runtime.mjs` is the Q64d harness, derived from Q97. Three phase runs stopped on harness issues:

| Run | Cause | Follow-up |
|---|---|---|
| `spec-a1` | CLI-path resolution; no test ran; zero write | fixed, re-run as `spec-a2` |
| `cu20-a1` | Escape did not close the transition surface (this became Q64d-D2) | follow-up phase `cu20b`; the two committed 200s are proven in `kit/raw/db/cu20-approved-a1.json` |
| `l10n-a1` | Enter on Add (this became Q64d-D3) | follow-up phase `kbd` |

Two harness verdicts are false positives and are corrected in `ACCEPTANCE.tsv` with the reason:
- CU-24: French "ACTIONS" is the correct French word.
- CU-05: the empty half; the text is the shared localized `DtEmptyTable`.

The phase files are kept as written.

## 8. Deviations and assumptions

- **D-1 (owner decision):** the Gateway runs on the kit slot port 5700, not 5000. The browser → Web → Gateway chain is unchanged.
- **D-2:** `up-q64d.sh` adds the K07pre tenant step (as Q97; K-F1).
- **D-3:** lane-DB setup writes where no product API exists:
  - carrier link and soft deletes (as Q97);
  - CU-18 root edits (`kit/raw/fixture/cu18-root-edit.json`, before/after values);
  - `decide` on the fixture-admin setup role;
  - `carriers.read` on the full role, for the CU-10 positive branch only.
  Each is inside a K10 pair or has a before/after record.
- **D-4:** some read-only and setup `mongosh` commands and the Playwright phases ran through my shell or the supervisor, not `ek_run`. So
  `kit/COMMANDS.tsv` has kit and harness rows, but not a row for each `mongosh` read. Every such command's output is a file under `kit/raw/`.
- **D-5:** CU-10's positive branch uses a lane-only role grant. No code or pack change was made (Q114 stays open).
- **A-1:** the crafted-response rows (CU-03/04/05/13) replace **API responses** in the browser (`route.fulfill` / delayed `route.fetch`), not assets. K09's served-asset rule
  therefore does not apply, and `kit/raw/served-overrides.tsv` is not used.

## 9. Isolation, hygiene and cleanup

- **Repo:** branch and HEAD unchanged; no `index.lock`; the 19 diff paths are unchanged (`FINAL-CHECK.txt`). No git write. Only this folder was written.
- **`~/mvp6-env/base`:** not modified. It stays read-only (`dr-xr-xr-x`); my copy was made writable with `chmod u+w` on the copy only.
- **Secrets:**
  - generated by the kit supervisor in memory only;
  - K08 seal: `kit/SECRET-SCAN-FINAL-a1.txt` → `artifacts_verified PASS`, `result PASS` (exact + pattern scan);
  - the root files of this folder were pattern-scanned before `SHA256SUMS` (`SECRET-SCAN-ROOT.txt`).
  - The Loads `runtime_probe.py` secret (Q101 F02) was not read.
- **Processes:** all lane services, the lane mongod and the supervisor stopped. K11 `CLEANUP.tsv` is PASS; the services were
  KILLED-after-timeout (K-F5). The test mongod on 57192 was shut down at ~00:02.
- **Another lane:** after my test mongod stopped, **another lane (`~/mvp6-env/q117`) started its own mongod on 57192/`rsmod192`** and a
  `dotnet test` run. It is not mine and was not touched; CT should know the port was shared in sequence.
- **Kit deletions (owner-approved "kit as-is", 2026-09-27):**
  - K11 removed the kit workspace sub-folders `shared/`, `env/` and `mongo/` of `~/mvp6-env/q64d/ek-work`;
  - K04b removed its temporary route folder;
  - the supervisor unlinked its socket.
  - `seal` recreated `ek-work/logs` (K-F6). I ran no `rm`.

## 10. Files

| Path | What |
|---|---|
| `ACCEPTANCE.tsv` | 41 rows: every §32.11 row plus D-01/D-02/S-01/S-02/Save View/401/isolation/F8/§33 |
| `kit/` | sealed kit evidence: COMMANDS.tsv, SOURCE-BINARY-PROCESS.tsv, CLEANUP.tsv, PNG-INDEX.tsv, png/ (58), browser/ (20 phase files + spec report), raw/ (fixture, db pairs, K01–K11 outputs, log excerpts), ARTIFACTS.sha256 (197), SECRET-SCAN-FINAL-a1.txt |
| `lane-scripts/` | harness dir used by the supervisor: `claims_runtime.mjs`, `org_fixture.py`, `org_fixture_cont.py`, `tenant_fixture.py` |
| `lane-config/` | `lane.env` (no secrets), `overlays.tsv`, `actors.tsv`, `overrides.tsv`, `pair.sh`, `up-q64d.sh` |
| `compose/` | `compose_q64d.py`, COMPOSE-LOG, L6 before/after, base-verify, K01-vs-src, derived manifests and input hashes |
| `logs/` | build logs + BUILD-SUMMARY, test TRX/logs + SUMMARY, kit up/down/seal console logs |
| `FINAL-CHECK.txt`, `SECRET-SCAN-ROOT.txt`, `SHA256SUMS` | end-of-lane repo check, root secret scan, hashes of every file in this folder |

Agent PASS ≠ CT ACCEPTED — returning to CT.
