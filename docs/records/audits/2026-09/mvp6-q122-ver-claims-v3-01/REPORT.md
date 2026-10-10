# Q122 — MOD-0187 Claims UI DRAFT v3: independent full runtime acceptance (§32.11) — SOP §37

| Field | Value |
|---|---|
| WP / prompt / lane | WP-MVP6-VER-122 · Q122 v2 · independent VER lane (`/read-only-audit` + testing-agent). Not the Q64e writer session and not the Q121b session |
| Pattern (§17.3) | table/bulk list + offcanvas actions (pack §32); no Details page |
| Where | Claude Code on the owner's Mac (Darwin), .NET SDK 8.0.417 / runtime 8.0.23, mongod 8.0.18, Playwright 1.59.1 (Chromium headless) |
| Repo | `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`, READ-ONLY. The only repo write is this folder |
| Workspace | `~/mvp6-env/q122/` (new). New folders only; **no `rm`** by this lane (see §8 for the kit's own cleanup) |
| Time | preflight 2026-09-27 ~14:37 +03:00 → end ~15:45 +03:00 (kit rows 11:58–12:35Z in `kit/COMMANDS.tsv`) |
| Mode | independent: fresh evidence only. Q64d/Q64e **harness code** was reused and extended (listed in §7); none of their results was reused |

## 0. Verification report (SOP §37)

```text
VERIFICATION REPORT

WP ID:                WP-MVP6-VER-122 (Q122 v2)
Verifier:             Q122 independent VER lane (Claude Code, Mac)
Verification date:    2026-09-27
Branch/HEAD:          feature/mvp6-logistics @ 4a8d4d4b339528a88e6220fb8402e5a2c771136c

Agent Verdict:        NOT CLEAN — Claims v3 passes 28/30 §32.11 rows (CU-01…CU-30); 1 FAIL (CU-25, new keyboard defect
                      Q122-D5), 1 BLOCKED (CU-28, needs the integrated target). CU-VS1 and CU-31 PASS. All Q64d regressions
                      (D-01, D-02, D1, D2, D4, CU-05) PASS.
Verification Verdict: v3 is NOT writer-complete on a strict reading of CU-25 (one small module-owned fix, §6).
CT Status:            pending — Agent PASS ≠ CT ACCEPTED

Evidence level achieved:  OBSERVED at runtime (SOP §32.0): real Auth, real services, isolated replica set, headless
                          Chromium through Web only, K09 freshness, K10 DB pairs, kit seal
Required evidence level:  OBSERVED runtime (§32.11 BR/HTTP/DB/ST per row)

Checks:
- scope:          §32.3 bound operations only; no by-ID/edit/delete/bulk request observed; 0 native dialogs
- build:          4 targets 0 errors (SupplyChain 0 W, Web.Tests 18 W = Q103 15+3, Gateway.Tests 5 W, Architecture 0 W); 0 warnings in Claims files
- tests:          Web 245/245 (Claims 87/87); SupplyChain single-process 416/417 (the 1 = ClaimReplayTests…DurableRecovery, by design);
                  Architecture 17/18 (the 1 = MongoTestDatabaseGuard, 2 Platform files, Q121)
- runtime:        slot 9 kit lane; browser → Web 5901 → Gateway 5900 → services; 74 K10 pairs, 74 PASS
- persistence:    DB assertions for VS1 (root/Pending outbox), CU-12 (exact empty text), CU-10 (CarrierId)
- RBAC:           CU-06/07/08 incl. 6 grant configurations; 401 JSON / no-read _AccessDenied + 403 / read-only 403
- tenant:         cross-LE, cross-tenant, soft-delete: identical safe-not-found, lists isolated
- concurrency:    CU-21 two-profile stale transition 422 + reload
- idempotency:    CU-19 replay / 409 / lost-response retry / exact occurredAt retry
- audit/evidence: receipts/audit/outbox deltas exact per K10 pair; audit carries root and exact empty texts
- observability:  support reference = X-Correlation-Id = error.correlationId, never a root
- migration/rollback: n/a (UI draft)
- integration:    env-copy only (_shared-integration applied to the copy); CU-27 PASS on env copy; CU-28 BLOCKED
- console/security leakage: 0 root leaks, 0 native dialogs, 0 page errors (55 phase files); seal result PASS

Failed criteria:
- CU-25 (keyboard): after a rejected create submit (400) focus drops to BODY and Escape does not close the create surface (Q122-D5)

Rework required:  yes — one module-owned fix (Q122-D5, index.js + a contract test); CT may instead dispose it as LOW
Next gate:        CT disposition of CU-25/Q122-D5 and CU-28; v4 (if required) → re-run CU-25 only; CU-28 on the integrated target
```

## 1. Preflight and inputs

| Check | Result |
|---|---|
| `uname -s` | Darwin |
| `GIT_OPTIONAL_LOCKS=0`; `.git/index.lock` | set; absent (start and end) |
| Branch / HEAD | `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c` = expected |
| Diff list | 19 paths (start and end) |
| BASE `~/mvp6-env/base/src` vs `mvp6-q103-accepted-base-01/BASE-MANIFEST.tsv` | manifest `a8a236de82a9f2b5d4c0f3560aa13894c3740c6e187cb0140f5da1b08109c661` ✓; **14566/14566 OK**, 14566 files on disk |
| Q117 overlay `q117-fixes-overlay.tar.gz` | `83e6322c1d4dd46c80b0b4de0a2b60493883a7b2d923c7685da689de702f800d` ✓ |
| Claims v3 archive `claims-ui-draft-overlay-v3.tar.gz` | `66e72bf6af1ebb6fffbcedf5f8b7934cd9a5a0f95cb1abf558b92fa3b500ce0c` ✓ |
| Pack `MOD-0187-claims-management.md` | `3d1a00e2f0a0e7e58d19f44b340de9ab0d6bf84e7e2997db451c65d2ddb74cae` ✓ (P1 applied, P2 deferred) |
| Controlling row text | `docs/roadmap/plans/mvp6-ui-pack-drafts-01/claims/ACCEPTANCE.md` `e5b7f766…` (read; §32.11 says it controls) |

## 2. Composition (`~/mvp6-env/q122/src`, cp only; `compose/`)

`compose/compose_q122.py` (fail-closed, never deletes). Per-file before/after hashes: `compose/LAYERS-BEFORE-AFTER.tsv`.

| Layer | What | Files |
|---|---|---|
| L0–L3 | BASE copy (`cp -c -Rp`), made writable on the copy only | 14566 |
| L4 | Q117 overlay: 10 replaced, 1 new (`TestAssemblyBsonSetup.cs`) | 11 |
| L5 | Claims v3 module files (`overlay/frontend/**`, `overlay/services/**`), all new paths | 23 |
| L6 | `_shared-integration`, applied **only inside the copy**: | 9 |
| | (a) SupplyChain `Program.cs`: `AddSingleton<IModuleManifestProvider, ClaimsManagementManifestProvider>()` after the Shipment provider line (`33027bcd…` → `fc82ad49…`) | |
| | (b) `gateway/Diten.ApiGateway/ocelot.json`: `/api/shipment-bundle/claims` [GET, POST, OPTIONS] and `/api/shipment-bundle/claims/{claimId}/transition` [POST, OPTIONS] appended (275 → 277); confirm-route GET `/api/shipment-bundle/shipments/{shipmentId}` present | |
| | (c) `SharedResource.{en,tr,fr,es,zh,ar,ru}.resx`: `Nav.Module.CLAIMSMANAGEMENT` + `Nav.Page.CLAIMS` | |
| | Not applied: `gateway-tests-ocelot-count.patch.txt` (Gateway tests out of scope), `icon-map.proposal.md`, `platform-registration.md`, `frontend-Program.cs.note.txt` (notes/proposals) | |
| Result | 14590 files | |

Cross-checks: my derived module and env-integration archives are byte-identical to Q64e's (`3f896415…`, `bc398cb8…`;
`compose/DERIVED-OVERLAYS.sha256`). The kit's K01 composed its own tree from HEAD + 6 overlays (`lane-config/overlays.tsv`;
Q117 declared as overlaps of rows 1–3): tree `b1e10bfcea4a95245d40bfa0ee9e328ddc41f7356c7492aabf05c5619eb1c8eb`, **14590/14590 byte-identical to
my compose** (0 diff lines); every overlay manifest matched after-overlay and final (`kit/raw/source-manifest-verification.txt`).

## 3. Build and tests (`~/mvp6-env/q122/build` = copy of src; `logs/`)

| Target / suite | Result |
|---|---|
| Builds (`logs/build/BUILD-SUMMARY.tsv`) | SupplyChainService.sln 0 E / 0 W · Diten.Web.Tests (builds Web) 0 E / 18 W · Diten.ApiGateway.Tests 0 E / 5 W · TenantArchitecture 0 E / 0 W; **0 warnings in Claims files** |
| Diten.Web.Tests | **245/245**; Claims 87/87 (ClaimFormContractTests 40, SupplyChainClaimsControllerTests 32, ClaimIndexBehaviorTests 15) |
| SupplyChain single-process, attempt 1 (`logs/test-a1`, lane Mongo 57222) | 384/417 — **environment**: 32 Capacity tests hard-code `127.0.0.1:57192/rsmod192` (6 files) and could not connect; + the by-design restart test |
| **SupplyChain single-process, attempt 2** (`logs/test-a2`, lane test mongod 57192/`rsmod192`, `enableTestCommands=1`, own dbpath) | **416/417**; the 1 = `ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery` ("explicit restart mode required", excluded by design). Per module: Carriers 36, Loads 33, Returns 78, SandopPlans 19, CapacityPlans 39, Shipment suites 74, Claims 128/129, ModuleRegistration 9 |
| Claims + manifest provider | 137/138 (same by-design exclusion) |
| Architecture | **17/18**; the 1 = `MongoTestDatabaseGuardTests.NoTestCreatesItsOwnDatabasePerRun` on the 2 Platform files (Q121) |

**Total vs WP expectation:** the WP says 407/408. The Q117 total (408) predates v3; v3 adds 9 `ClaimsManagementManifestProviderTests`,
so the same run is 416/417 (= 407/408 + 9/9). Both test mongods were started with `--setParameter enableTestCommands=1` and shut down
cleanly after the runs (data folders kept).

## 4. Runtime environment (evidence kit v1.2, `kit/`, sealed)

- **Slot 9:** Gateway 5900, Web 5901, Auth 5956, Platform 5957, MDM 5959, SupplyChain 5961, sink 5999, Mongo **39994** (`rsClaimsQ122Ver01`,
  DB suffix `ClaimsQ122Ver01`). All free before start; Q121b used 57212 (test mongod) at the same time. Lane mongod started with
  **`--setParameter enableTestCommands=1`** (verified `true` via `getParameter`) through a lane copy of K05 (DV-1).
- **Traffic:** browser → Web 5901 → Gateway 5900 → services. The browser never called 5900/5961 (CU-01).
- **`up`:** PASS on the first attempt (`logs/kit/up-a1.log`): K01, K03, K02, 6 × K06 build, K04b (277 routes), K04 (no 27017, no literal
  secret), K05, 6 launches, netcheck (0 × 27017), K07pre tenant fixture, K07 identities.
- **Hand-off conditions:** claims-management, shipment-tracking-pod, carrier-management and MDM's two modules self-registered (Attempt=1);
  MDM audit appends → **202 ×6** (`kit/raw/logs-excerpt/registration-audit-excerpt-a2.log`).
- **Freshness (SOP §25) — K09 PASS:** 6/6 services `match` + `listening` on tree `b1e10bfc…`; every running DLL hash = its K06 Release build.
  Served `/assets/js/SupplyChain/Claims/index.js` = **`c8ef218e…` = the v3 archive file**; `index.l10n.js`, `personalization-client.js`,
  `dt-defaults.js` = source bytes (`kit/SOURCE-BINARY-PROCESS.tsv`, `kit/raw/browser-binding.tsv`). End-of-lane recheck: every PID still the
  listener (`kit/raw/k09-recheck-end.tsv`), so nothing restarted after the proof.
- **Fixtures** (`lane-scripts/org_fixture.py`, supervisor harness; ids/statuses only in `kit/raw/fixture/`): roles on existing catalog keys;
  MDM LE-A/LE-B/LE-T2 + Platform OU/positions/assignments; 1 carrier, 9 shipments (DISPATCHED, CARRIER, DRAFT, SEED, SOFTDEL, NULLROOT,
  BADROOT, FLOW, T2SHIP), 10 claims via product APIs; lane-DB-only: carrier link, 2 soft deletes. Matrix seed (`matrix_setup.py seed`):
  PLANNED + CANCELLED shipments, 9 claims in Open/Investigating/Approved/Rejected, one more carrier.
- **K10:** 74 pairs, **74 PASS** (strict whole-DB rule; `kit/raw/db/db-assertions.tsv`).
- **Actors** (real Web login, separate contexts, cookies httpOnly): full, read-only, no-read (shipments.read only), LE-B full, T2 full.
  Storage states lived only in `~/mvp6-env/q122/state-*/` (0600) and were **truncated to 0 bytes**, not deleted.

## 5. Results (`ACCEPTANCE.tsv`: one row per CU + regressions/isolation/denied, with evidence paths)

**§32.11 CU-01…CU-30: 28 PASS · 1 FAIL · 1 BLOCKED.** CU-VS1 PASS, CU-31 PASS.

| Row | Result | Row | Result | Row | Result |
|---|---|---|---|---|---|
| CU-VS1 | PASS | CU-11 | PASS | CU-22 | PASS |
| CU-01 | PASS | CU-12 | PASS | CU-23 | PASS |
| CU-02 | PASS | CU-13 | PASS | CU-24 | PASS |
| CU-03 | PASS | CU-14 | PASS | **CU-25** | **FAIL** (Q122-D5) |
| CU-04 | PASS | CU-15 | PASS (+F-Q122-01 LOW) | CU-26 | PASS |
| CU-05 | PASS | CU-16 | PASS | CU-27 | PASS (environment copy) |
| CU-06 | PASS | CU-17 | PASS | **CU-28** | **BLOCKED** (integrated target) |
| CU-07 | PASS | CU-18 | PASS (CT disposition) | CU-29 | PASS |
| CU-08 | PASS | CU-19 | PASS | CU-30 | PASS (owner decision) |
| CU-09 | PASS | CU-20 | PASS | CU-31 | PASS |
| CU-10 | **PASS with lane grant; prerequisite undocumented** | CU-21 | PASS | | |

CT dispositions applied:
- **CU-30** PASS under `MVP6-PNG-METHOD-OWNER-DECISION-01` (Playwright `page.screenshot({path})` only).
- **CU-18** runtime: null root → **503 `CLAIM_REFERENCE_INCOMPLETE`** by HTTP and UI (localized alert, inputs kept, the retry reuses the same
  key and identical body), zero write. The 502 branch is accepted from backend tests; this lane's own run has
  `ClaimReferenceTests…DistinguishesIncompleteFromMalformed(mode: "malformed", status: 502)` **Passed**. Malformed root at runtime:
  Shipment API `GET …/shipments/{id}` → **500** (reproduces **F-0183-500**, MOD-0183), which the adapter maps to 503 `CLAIM_REFERENCE_UNAVAILABLE`. Not a Claims defect.
- **CU-10** run twice. Without `supplychain.carriers.read`: carrierId sent → **503 `CLAIM_REFERENCE_UNAVAILABLE`, K10 zero write**. With the
  lane-only grant (`kit/raw/fixture/cu10-carriers-read-grant.json`) and a fresh login: **201**, DB `CarrierId` = the shipment's carrier. Also:
  unchecked → carrierId omitted → 201; an existing different carrier → 422 `CLAIM_CARRIER_MISMATCH`; a non-existent carrier id → safe 404.
- **CU-27** was not blocked: the lane Gateway carries the draft route fragment, so the criterion could be measured. `claimsXYZ` → 404
  (Ocelot `UnableToFindDownstreamRouteError`, never reached Supply Chain); `/SupplyChain/ClaimsXYZ` → 404. Re-confirm on the real
  integration-owner diff.
- **CU-28** is BLOCKED because it needs the integrated target: "green **on target**" and "Shipment UI works" cannot exist until the Claims backend
  (47 paths + approved `Program.cs` composition) is taken up into the common target. Env-copy regression evidence is attached (416/417).

Regressions and extra checks:

| Check | Result |
|---|---|
| D-01 list reload after create / filter / transition | PASS (the new claim is in the reloaded data, 16 rows over 2 pages; 0 `xhr.abort`) |
| D-02 single tenant shell | PASS (1 layout wrapper, 1 `main.js`, 1 footer) |
| D1 skeleton (v3 E-01/E-02) | PASS: skeleton `display:block` from 958 to 3357 ms through a 2.5 s delayed load, then table |
| D2 transition focus/Escape (v3 E-03) | PASS: focus on `#transitionTarget` at 50 ms and 900 ms, Escape closes in both |
| D4 create focus return (v3 E-04) | PASS after Escape and after Save |
| CU-05 / CU-25 re-runs | CU-05 PASS; CU-25 FAIL on a new state only (Q122-D5); the Q64d D2/D4 parts pass |
| v3 draft spec (S-01/S-02) | 16 passed / 0 failed / 1 skipped (CU-19 outline, by design) |
| Save View / Reset | Apply → dirty · Reset → factory + clean · sort → dirty · Save → POST 201 + clean · reload keeps the saved view · Reset vs saved default → dirty · 0 JS errors |
| Isolation | cross-LE, cross-tenant and soft-delete: identical safe-not-found (HTTP shape + one localized UI text), lists isolated |
| Denied | 401: JSON, no redirect (body code `INVALID_REQUEST` → O-01 still open); no-read: `_AccessDenied` in shell, adapters 403; read-only: no CTA/actions, direct calls 403 |
| §33 self-registration | PASS: catalog entry, page `CLAIMS` and 7 actions exactly as §33 (Platform stores the module code upper-case) |
| Scans (`F8-SCAN-ALL.tsv`, 55 phase files) | **0 lifecycle-root leaks** (9 roots, every same-origin response) · **0 native dialogs** · **0 page errors** · foreign hosts = shell Google Fonts only |
| Static (`static/STATIC-CHECKS.txt`) | resx 95 keys × 7, 0 empty; only fr `ActionsHeader` equals English ("Actions" is French); 18 = 18 error codes; 0 alert/confirm/prompt, `Swal.fire`, inline handlers, storage, ports or technical text; `Authorization` only server-side |
| `verify_datatable_page.py --reference slim` (record only, CU-SCR-06) | 70 PASS / 21 FAIL (`static/verify_datatable_page-slim.txt`), the same OUT-scope bulk items as before |
| PNGs | **91** (`kit/PNG-INDEX.tsv`, every hash verified): en and ar at 390/768/1024/1280/1440, tr/fr/es/zh/ru at 1280 (WP set 9/9), one per scenario, spec screens |

## 6. Findings (proposed fixes only; no code changed)

| ID | Sev | What | Evidence | Proposed fix | Owner |
|---|---|---|---|---|---|
| **Q122-D5** | LOW–MEDIUM | **Escape does not close the create surface after a rejected submit.** `submitCreate` disables `#btnSaveClaim` while pending, so the focused button drops focus to `BODY`. Bootstrap's offcanvas Escape handler listens on the element, so Escape is ignored until the user Tabs back in. Measured: `activeAfterResponse: BODY`, `closedByEscape: false`; after one Tab, Escape closes. The transition surface is not affected, because focus returns to Submit after the shared confirm. | `kit/browser/extra2-a1.json`, `kit/browser/kbd2-a2.json`, `kit/png/kbd-escape-after-400-en.png` | In the create `finally`, after re-enabling Save, restore focus when it fell to `BODY` and the surface is open (`save.focus({preventScroll:true})` or the first alerted field); or use `aria-disabled` instead of `disabled` while pending. Add a JS contract test and a Playwright row "Escape after rejected submit". Sabotage: current v3 → RED. | frontend-ui-ux (v4 draft) |
| F-Q122-01 | LOW | The same 404 `CLAIM_NOT_FOUND` carries different wire **message** text depending on the stage: shipment lookup gives "The requested record was not found."; claim lookup gives "Claim or referenced resource was not found." The UI shows one localized text (verified), so it is not visible to users. It is not an existence oracle, because it depends on the stage, not on the target. | `kit/browser/checks-neg-a1.json` (CU-15 cases) | Emit one safe message for `CLAIM_NOT_FOUND` in the MVC adapter regardless of stage. | UI writer / CT |
| F-0183-500 | (MOD-0183) | Reproduced: Shipment API returns 500 on a malformed stored root. | `kit/raw/logs-excerpt/cu18-root-seam.log` | As recorded by CT | Shipment owner |
| O-01 | LOW | Still open: 401 JSON body code is `INVALID_REQUEST`, pack §32.8 expects `UNAUTHENTICATED`. | `kit/browser/checks-neg-a1.json` | Shared frontend `Program.cs` (A12 layer) | integration owner |
| O-Q122-1 | INFO (env) | The lane Gateway runs `TenantResolution` dev bypass (`appsettings.Development.json: DevBypassEnabled true`). The kit overrides set it false only for Auth/Platform; unauthenticated gateway calls get tenant `0000…0001` before the 401. Isolation results are unaffected (tenant comes from the token). | `kit/raw/logs-excerpt/gateway-dev-bypass.log` | Add `gateway TenantResolution__DevBypassEnabled false` to the kit override template | kit owner |
| O-Q122-2 | INFO (env) | Platform audit outbox dead-letters 23 messages (`AuditOutboxPayloadMappingException`) during registration sync. | `kit/raw/logs-excerpt/platform-audit-deadletter.log` | Platform investigation (not Claims) | Platform owner |
| O-Q122-3 | LOW (test infra) | 6 Capacity test files hard-code `127.0.0.1:57192/rsmod192`, which forces every lane onto one shared port (attempt 1 failed 33 tests on a free port). | `logs/test-a1/supplychain-single.log` | Read a `MOD192_TEST_MONGO` variable like the other suites | SupplyChain test owner |
| O-Q122-4 | INFO (kit) | K05 has no `enableTestCommands` option (lane copy used, DV-1). K11 marks `repo-status` FAIL when another lane adds a folder in parallel (here `mvp6-q121b-build-test-01/`). K-F5: services stop only after the kill timeout. | `kit/CLEANUP.tsv`, `lane-config/k05_mongo_q122.sh` | K05 flag; K11 should compare against its own baseline minus other lanes' declared folders | kit owner |
| O-Q64d-1 | INFO (env) | The shell sidebar is still empty in lane environments. | `kit/png/l10n-en-1280.png` | Platform nav registration in lanes | environment |

## 7. Deviations and harness notes

- **DV-1:** `lane-config/k05_mongo_q122.sh` is the kit's K05 plus `--setParameter enableTestCommands=1` (the WP requires it) and an absolute
  `ek_lib.sh` path. `up-q122.sh` = `run-kit.sh up` + the K07pre tenant step (K-F1; same as Q64b/Q64d/Q64e) + this K05 line. Kit files were used in place, unchanged.
- **DV-2:** the Gateway ran on the kit slot port 5900, not 5000 (as accepted for Q64d/Q64e).
- **DV-3:** SupplyChain tests ran on a second lane test mongod on **57192** because the Capacity tests hard-code it (O-Q122-3). The port was
  free at the time (Q121b was on 57212), and the mongod was stopped right after the run.
- **DV-4:** the SupplyChain total is 417, not 408 (§3).
- **DV-5 — lane-DB setup writes** (no product API exists; each has a before/after file and a K10 pair): carrier link and 2 soft deletes
  (fixture); CU-18 roots (`cu18-root-edit.json`); CU-10 grant `carriers.read` on `Q122ClaimsFull` (`cu10-carriers-read-grant.json`);
  `shipments.cancel` on the fixture-admin role (`matrix-fixture.json`); per-configuration roles for the `readonly` actor (`matrix-role-*.json`),
  restored to `claims.read` only at the end (`matrix-role-restoreReadOnly.json`). The lane DB was removed by K11 with the kit workspace.
- **DV-6 — harness extended during the lane.** Q64d's harness and mine checked less than the controlling `ACCEPTANCE.md` text. These phases were
  added, each logged in its header comment and never overwriting earlier files: `nf-ui` (CU-14/15 browser halves), `cu18ui`, `regd01`,
  `matrix` + `matrixdom` (CU-07/08 grant matrix), `extra` + `extra2` (CU-10 unchecked/mismatch, CU-11 full list, CU-17 Planned/Cancelled, Q122-D5),
  `kbd2` (transition surface).
- **Harness false positives corrected in ACCEPTANCE** (the phase files are kept as written):
  - `checks-neg-a1` CU-15: an extra wire-message identity check beyond the row (became F-Q122-01);
  - `l10n-a1` CU-24: French "Actions";
  - `regression-a1` REG-D-01: its visible-page text check missed a row on page 2 (re-measured in `regd01-a1`);
  - `matrix-*-a1` DOM: the Open row was on page 2 (DOM re-measured in `matrixdom-*`; the HTTP calls in `matrix-*-a1` are valid);
  - `cu18-a1` null-root UI: resolve has no root by design (re-measured on create in `cu18ui-a1`).
- **Harness stops:**
  - `extra-a1` stopped after CU-11 because of Q122-D5 (its CU-10 mismatch used a non-existent carrier → 404; redone in `extra2-a1`);
  - `kbd2-a1`: pressing Enter on the confirmation popup did not confirm (redone with a click in `kbd2-a2`);
  - the matrix role step failed once on a unique `userRoles` index (setup-script defect, fixed as a2; the failed runs `matrixdom-*-a1` read `_AccessDenied`).
- **A-1:** the crafted-response rows (CU-03/04/05/13, CU-15 UI) replace **API responses** in the browser (`route.fulfill`/delayed routes), not
  assets. So `served-overrides.tsv` is not used, and the kit's served-asset rule does not apply.

## 8. Hygiene

- **Repo:** branch/HEAD unchanged, 19 diff paths, no `index.lock`, no git write (`FINAL-CHECK.txt`). Only this folder was written.
- **BASE, overlays, draft folders, Q folders:** not modified (BASE was re-verified 14566/14566 before copying; archives hash-checked before extract).
- **No `rm`** by this lane. The kit's own cleanup (K11) removed its disposable workspace `~/mvp6-env/q122/ek-work`, including the lane Mongo
  data and the service logs (excerpts were redacted into `kit/raw/logs-excerpt/` first). The supervisor removed its socket.
- **Secrets:**
  - generated by the supervisor in memory only; never printed;
  - kit seal `kit/SECRET-SCAN-FINAL-a1.txt`: `artifacts_verified PASS`, `result PASS` (exact + pattern, 389 artifacts);
  - the report root was pattern-scanned before `SHA256SUMS` (`SECRET-SCAN-ROOT.txt`).
  - The Loads `runtime_probe.py` content was not read.
- **Processes:** every lane service, the lane mongod (39994), both test mongods (57192, 57222) and the supervisor stopped. Only processes that
  predate the lane remain (operational 27017 mongod, the user's Chrome).

## 9. Files

| Path | What |
|---|---|
| `ACCEPTANCE.tsv` | 51 rows: every §32.11 row (CU-VS1, CU-01…CU-31) + D-01/D-02/D1/D2/D4, spec, Save View, isolation, denied, F8, dialogs, page errors, secrets, §33, freshness |
| `kit/` | sealed kit evidence: COMMANDS.tsv (285 rows), SOURCE-BINARY-PROCESS.tsv, CLEANUP.tsv, PNG-INDEX.tsv, png/ (91), browser/ (55 phase files + spec report), raw/ (fixture, db pairs, K01–K11, logs-excerpt, probes), ARTIFACTS.sha256, SECRET-SCAN-FINAL-a1.txt |
| `F8-SCAN-ALL.tsv` | root-leak / dialog / page-error / foreign-host scan over all 55 phase files (the sealed `kit/raw/F8-SCAN-SUMMARY.tsv` covers the first 23) |
| `lane-config/`, `lane-scripts/`, `lane-tools/` | lane parameters (no secrets), `up-q122.sh`, lane K05 copy, K10 pair helpers; harness + fixtures + setup scripts; build/test/probe scripts |
| `compose/` | compose tool, log, per-layer before/after hashes, derived overlay manifests and hashes |
| `static/` | static checks, verifier record |
| `logs/` | build logs + summary; test logs + TRX (attempt 1 and 2); kit up/down/seal logs |
| `FINAL-CHECK.txt`, `SECRET-SCAN-ROOT.txt`, `SHA256SUMS` | end-of-lane repo check, root secret scan, hashes of every file in this folder |

## 10. To-do list

1. **CT:** dispose CU-25 / **Q122-D5**: either a v4 fix (module-only, §6) followed by a CU-25-only re-run, or accept it as LOW.
2. **CT:** record CU-10 as "PASS with lane grant; prerequisite undocumented" and keep P2 (`supplychain.carriers.read` in §33/ACCEPTANCE) open.
3. **Integration owner:** CU-28 on the integrated target (Claims backend uptake); re-confirm CU-27 on the real ocelot diff; O-01 (401 code).
4. **UI writer / CT:** F-Q122-01 (one CLAIM_NOT_FOUND wire message).
5. **Shipment owner:** F-0183-500 (reproduced here).
6. **Kit owner:** O-Q122-1 (Gateway dev bypass override), O-Q122-4 (K05 flag, K11 parallel-lane status).
7. **SupplyChain test owner:** O-Q122-3 (Capacity tests hard-coded port). **Platform owner:** O-Q122-2 (audit dead-letters).
8. **Template owner:** Q64d-D3 (Enter on Add) is still open.

Agent PASS ≠ CT ACCEPTED — returning to CT.
