# Q64b — MOD-0187 Claims UI DRAFT: build, tests, runtime (WP-MVP6-ENV-097 Part 3, Q97 v2.1) — SOP §22 report

| Field | Value |
|---|---|
| WP / prompt / lane | WP-MVP6-ENV-097 · Q97 v2.1 · AL-MVP6-ENV-097 (INT, environment lane) |
| Agents | devops-agent (env, kit), testing-agent (/test) |
| Where | Claude Code in the owner's macOS terminal, Darwin arm64, .NET SDK 8.0.417 / runtime 8.0.23 |
| Repo | `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`, READ-ONLY (no git write; diff set 19 paths unchanged; no index.lock) |
| Workspace | `~/mvp6-env/` (composed trees, builds, test DBs, lane workspaces, venv, node_modules) — left in place |
| Draft under test | `mvp6-claims-ui-draft-01/claims-ui-draft-overlay.tar.gz` `bc0f5819…` (Q64a, CT ACCEPTED as DRAFT) |
| Pack | MOD-0187 `96c9a0ae…` §32/§33 (hash re-measured at preflight) |
| Start / end | 2026-09-26T19:08:57Z / see §11 |

## 1. Verdict (agent)

**Draft as delivered: builds (0 errors) and its own tests pass, but it FAILS runtime acceptance** on two draft defects that
break the list reload and every row action. **With FIXES (3 minimal corrections, `FIXES.patch`) the Q64a runtime scenarios
pass except two spec-selector defects, and the isolation / denied-permission / F8 checks pass in both runs.**
Evidence level (SOP §32.0): **runtime (local, real Auth, isolated DB, headless Chromium) with §25 freshness proof** for §6–§7;
build/test = executed; static items are labelled. **Agent PASS ≠ CT ACCEPTED — returning to CT.**

Preflight (SOP §20): `uname -s` Darwin; `GIT_OPTIONAL_LOCKS=0`; no `.git/index.lock`; branch/HEAD as expected;
`git diff --name-only HEAD` = 19 paths (same list at the end, §10).

## 2. Composed tree (Owner decision D1: never the working tree)

`lane-tools/compose_claims.py` → `~/mvp6-env/claims/`. Every archive hash was checked **before** extraction (`compose/COMPOSE-LOG.txt`):

| Layer | Input | sha256 | Check |
|---|---|---|---|
| L0 | `git archive HEAD` (`head.tar` `96ae026a…`) | — | 14,251 files |
| L1 | A12 360 successor `mvp6-shipment-a12-safe404-rework-01/successor-source.tar.gz` | `7b6a0d1a…` | wrapper stripped; 360/360 vs manifest `8ffa6c96…` |
| L2 | Auth 22 `mvp6-carrier-numericdate-exec-01/final-source.tar.gz` | `f50350b8…` | 3,333 files; FINAL-22 `b9713185…` 22/22; overlap with L1 = 0 |
| L3 | Claims draft module files (`overlay/frontend/**`, `overlay/services/**`) | `bc0f5819…` (+ draft SHA256SUMS `80da27c9…`, SOURCE-MANIFEST `9b468b37…`) | 23/23 files match the draft manifest |
| L4 | Draft `_shared-integration` applied to the ENV copy only (draft README step 3) | same archive | provider line in SupplyChain `Program.cs`; 2 Claims routes in `ocelot.json` (confirm-route present); 2 nav keys × 7 `SharedResource` files — 9 files, all previously from L1 |

**SOURCE-MANIFEST (composed tree, as delivered): `compose/SOURCE-MANIFEST.tsv` = `2dfebddb4beeab7d38bfeb58e99d0adfe28d108fa3727a672b457a9a57c6c24e` (14,589 files).**
The kit's independent K01 composition of the same inputs produced the **same** hash (`kit/raw/source-manifest-verification.txt`).
With FIXES the K01 tree is `e6185f1a2001577bde74db91586fd89f2f8be87563112e3e6f03280a4f42de52` (overlay 5 = 8 fixed files, `compose/derived-overlays/`).
BC-SOURCE and the Claims `normal-source` were **not** layered (ASSUMPTION A-01).

## 3. Build (`dotnet build -c Debug`, affected projects; kit K06 also built Release 6/6 in both lanes)

| Project | As delivered | With FIXES |
|---|---|---|
| Diten.SupplyChainService.Api / .Tests | 0 errors / 0 warnings / 0 warnings | unchanged (no fix touches them) |
| Diten.Web | 0 errors, 15 warnings | 0 errors (rebuilt with Web.Tests: 18 warnings = same 15 + 3) |
| Diten.Web.Tests | 0 errors, 3 warnings | 0 errors |
| Diten.ApiGateway / .Tests | 0 / 0 errors, 0 / 5 warnings | Tests: 0 errors, 5 warnings |
| TenantArchitecture.ArchitectureTests | 0 errors, 0 warnings | unchanged |

**0 warnings in any Claims draft file**: all 23 are pre-existing (CRM TerritoryManagementController, ESBP views, WorkCenter
DevScenarios, three Web.Tests fakes, ApiGateway TenantContradictionGuardTests) — `build/delivered/*.log`.

## 4. Tests (TRX in `tests/`)

| Suite | As delivered | With FIXES | Notes |
|---|---|---|---|
| Diten.Web.Tests (all) | **241/241** | **241/241** | Claims frontend files (Controller/Form/JS) **83/83** |
| SupplyChain.Tests, isolated test RS, no failpoints | 378/417 | — | 38 fail on environment only (Capacity tests hard-code `127.0.0.1:57192/rsmod192`; Loads/S&OP/Returns need `enableTestCommands`) + 1 by design |
| SupplyChain.Tests, env-complete (34794 + 57192, enableTestCommands) | **416/417** | same code | only `ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery`: "Explicit write/read restart mode required; exclude from ordinary suite" — NOT RUN by design |
| Claims backend + manifest provider (M-01…M-08 + key grammar) | **137/138** (manifest provider 16/16) | same | the 1 = restart-mode test above |
| ApiGateway.Tests | 86/87 | **87/87** | fail = `OcelotConfigurationTests` pins 6 shipment-bundle routes; the Claims gateway fragment makes 8 (FIX-01, shared seam) |
| ArchitectureTests | 15/18 | same | 3 fails in files NOT from the draft: JwtClockSkew (HCM, Talent from HEAD; MDM validator from Auth 22), MongoTestDatabaseGuard (Platform tests from Auth 22) |

A first run without any test DB (`tests/delivered-nodb-aborted/`) was stopped by the lane (timeouts only) and replaced.
Totals as delivered (env-complete SupplyChain): **passed 758, failed 5 (1 shared seam, 3 pre-existing guards, 1 by design), skipped 0 (763 total)**; with FIXES: gateway fail → pass.
`verify_datatable_page.py --area SupplyChain --module Claims --reference slim` (**record only**, CU-SCR-06): exit 1, **63 PASS / 27 FAIL** —
the FAILs are the OUT items (bulk/checkbox/import/export) and the direct-gateway `getAuthHeaders()` rules that do not apply to the
proxy profile (`verifier/verify_datatable_page.txt`).

## 5. Runtime environment (kit v1.2, see `../mvp6-q24b-kit-validation-01/`)

Two sealed kit lanes, each: fresh K01 source → K06 Release build → isolated replica set (DB-010 suffix) → six services on 127.0.0.1
→ real Auth identities (K07) → lane fixtures → browser via **Web only** (`127.0.0.1:<web>`; the browser never called Gateway/SupplyChain)
→ K09 binding → K11 cleanup → ARTIFACTS + exact-value seal.

| Lane | Folder | Slot / ports | DB suffix | Tree |
|---|---|---|---|---|
| as delivered | `kit/` (attempt 2; attempt 1 = K07 finding) | 5: Gateway 5500, Web 5501, SC 5561, Mongo 35994 | ClaimsQ64b01 | `2dfebddb…` |
| with FIXES | `kit-fixes/` | 6: Gateway 5600, Web 5601, SC 5661, Mongo 36994 | ClaimsFixQ64b01 | `e6185f1a…` |

Frontend → Gateway (the Web `GatewayUrl` is the lane Gateway; SupplyChain `Claims__ReferenceBaseUrl` = lane Gateway, set in
`lane-config/*/overrides.tsv`). Fixtures (`lane-scripts/`, run by the supervisor with actor passwords in memory only):
Platform tenant record (lane DB), Auth roles on existing catalog keys, MDM LE-A/LE-B/LE-T2 + Platform OU/positions/assignments
through the product APIs, 6 shipments + 1 carrier + 4 seed claims through the product APIs, then lane-DB-only: carrier link on
shipment CARRIER and soft-delete of shipment SOFTDEL and claim SOFTDEL1 (before/after values in `raw/fixture/org-fixture.json`).
Actors (real Web login, separate browser contexts): full (LE-A), read-only (LE-A), no-read (LE-A), LE-B full, T2 full (tenant `…0001`).

**Freshness (SOP §25):** both lanes — every service DLL hash at K09 time = the hash recorded at its K06 Release build; each PID is
still the listener; each process started after its own build ended (e.g. FIXES Web built 19:55:23Z, started 19:56:02Z; delivered Web
built 19:34:32Z, started 19:35:08Z); loaded runtime 8.0.23; served `/assets/js/SupplyChain/Claims/index.js` = source bytes
(`55f2c4fb…` = draft manifest as delivered; `bd15a9f3…` = FIXES). FIXES lane K09 **PASS**; delivered lane K09 prints FAIL only for the
6 dead attempt-1 rows (kit defect K-F4) — all 6 live rows `match`/`listening` (`kit/SOURCE-BINARY-PROCESS.tsv`).

## 6. Runtime results (scenario detail: `ACCEPTANCE.tsv`)

**Q64a scenarios (unchanged `claims-ui.spec.mjs`, sha256 `e421b2f6…` = draft SHA256SUMS), 17 tests:**

| Run | Passed | Failed | Skipped |
|---|---|---|---|
| as delivered | 12 | 2 — filter (D-01), stale transition (spec selector S-01) | 3 — QuickView and Open→Investigating (spec race S-02), CU-19 (by design) |
| with FIXES | 14 | 2 — Open→Investigating and stale transition (both spec selector S-01: after `.dropdown-toggle.first()` opens the shell navbar menu, the row item stays hidden) | 1 — CU-19 (by design) |

**Lane harness (row-scoped, `lane-scripts/claims_runtime.mjs`)** — early vertical slice first:

| Check | As delivered | With FIXES |
|---|---|---|
| CU-VS1 vertical slice | **FAIL** (201, exact body, DB +1/+1/+1/+1 Pending, root stored — but no list reload: D-01) | **PASS** |
| create → reload | FAIL (D-01) | PASS |
| CU-02 filter | FAIL (D-01) | PASS |
| CU-31 QuickView (no by-ID request) | PASS | PASS |
| CU-08 Open → Investigating | FAIL — unreachable (D-02) | PASS |
| CU-21 stale transition (two profiles) | FAIL — unreachable (D-02) | PASS (HTTP 422 + reload; support reference shown, PNG) |
| CU-10 carrier link | UI PASS; backend 503 until `supplychain.carriers.read` granted (F-CU10); then 201 | same |
| Isolation: cross-LE (CU-16), cross-tenant, soft-deleted claim/shipment (CU-14/15) | **PASS** | **PASS** |
| Denied permission: 401 JSON (no redirect), no-read `_AccessDenied` + 403 (CU-06), read-only no CTA + direct POST 403 (CU-07) | **PASS** (401 body code = O-01) | **PASS** |
| CU-17 ineligible (UI + direct 422), CU-11, CU-14 unknown | PASS | PASS |
| CU-23 support ref = header = `error.correlationId`, never a root | PASS | PASS |
| **F8 lifecycle root never in the browser** | **PASS** (0 hits, all instrumented phases) | **PASS** |

**DB pairs (K10, strict whole-DB):** delivered fixture/vs1/neg/cu10b PASS; cu10 (503) FAIL as zero-write (expected +1 → product);
delivered ui-flows FAIL (transitions unreachable). FIXES: fixture, vs1, spec, neg, flows (+1 claim, +3 receipts/audit/outbox), cu10 — **6/6 PASS**.
All negative checks (neg pairs) are zero-write in both lanes. Outbox rows are `Pending`; both `250.00` claims carry
`CorrelationRoot = 1445a5bc-…` = the DISPATCHED shipment's `lifecycleCorrelationId` (read-only mongosh query, lane DB).

**Correlation IDs captured:** every adapter call records request/response `X-Correlation-Id` and `error.correlationId` (`browser/*.json`).
**PNGs (38, Playwright `page.screenshot({path})`, evidence only — CU-30 stays BLOCKED):** `kit/png/` (18) and `kit-fixes/png/` (20),
including `vs1-after-create-en`, `uas001-noread-access-denied-en`, `readonly-list-no-cta-en`, `cu10-carrier-linked-create-en`,
`*-create-reload-en`, `*-quickview-en`, `fixed-native-transition-investigating-en`, `fixed-native-stale-transition-422-en`, and
`spec/01…06-{tr,ar}-{390,768,1024,1440}`. Delivered-lane PNGs show the repeated shell of D-02 (stacked footers).

## 7. Findings

| ID | Severity | What | Where | Disposition |
|---|---|---|---|---|
| D-01 | HIGH | `ajax: loadClaims` (async) → DataTables 2 calls `.abort()` on the returned Promise at `ajax.reload()` → `TypeError: xhr.abort is not a function`; no reload after create/transition/filter/retry; spurious "Create request failed" log after a 201 | draft `index.js` | FIX-02 (non-async wrapper) |
| D-02 | HIGH | the 5 partials set `Layout = "_LayoutTenantShell"`; ASP.NET Core applies an explicit Layout to partials → 6 full shells, 6× bootstrap/menu/main.js, `Identifier 'menu' has already been declared`, row dropdowns never open | draft `_*.cshtml` + `ClaimFormContractTests` | FIX-03 |
| F-PACK-32.2 | MEDIUM | pack §32.2 says every Claims `.cshtml` states the layout; followed literally it produced D-02 | pack MOD-0187 | CT / pack owner: scope to page views (no pack edit here) |
| F-SHARED-01 | LOW | `OcelotConfigurationTests` pins 6 shipment-bundle routes; the Claims gateway fragment adds 2 | shared seam (A12 test) | FIX-01; integration owner (SR-D4) |
| F-CU10 | MEDIUM | carrier-linked create needs `supplychain.carriers.read` on the actor: the backend forwards the user token to `GET /carriers`; without it 403 → 503 `CLAIM_REFERENCE_UNAVAILABLE` (zero write) | role design (like G-SHIPREAD) | CT / integration owner: add to the role prerequisite list |
| O-01 | LOW | 401 JSON challenge body code `INVALID_REQUEST`, pack §32.8 expects `UNAUTHENTICATED` | shared frontend `Program.cs:97` (A12 layer) | integration owner |
| S-01 / S-02 | LOW | Q64a spec: `.dropdown-toggle.first()` hits the shell navbar; `test.skip(!count)` evaluated before the table renders | `runtime-scenarios/claims-ui.spec.mjs` | spec author; lane used row-scoped equivalents |
| H-01 / H-02 | INFO | lane harness: CU-21 text check missed the toast container (PNG shows the reference); `cu10-carrier-linked-create-en.png` in `kit/` was overwritten by the second CU-10 run (both hashes in `checks-mut-a1/-a2.json`) | lane-scripts | recorded |
| Q64a F4/F5/F7/F9/F10/F13 | — | F4 confirmed (lowercase currency → 400, CU-11 PASS); F7 `occurredAt` has an offset (PASS); F9 no tenant/LE header from the browser (PASS); F5/F10/F13 not re-checked | — | — |

## 8. Persistence and security evidence

L1 only: lane replica sets `rsClaimsQ64b01` / `rsClaimsFixQ64b01` on 127.0.0.1, test sets `rsQ97Tests01`:34794 and `rsmod192`:57192
in `~/mvp6-env`; netcheck 0 connections to 27017 for every service (an operational mongod listens there and was never touched).
Secrets: generated inside the kit supervisor only; env files hold placeholders; actor passwords reached only supervisor tasks;
browser storage states (cookies) lived in `~/mvp6-env/tmp/state*` (0600) — never in evidence. Both kit folders sealed:
`SECRET-SCAN-FINAL-a1.txt` → `artifacts_verified PASS`, `result PASS`. The static secret of `tests/loads/runtime_probe.py`
(Q101 F02) was not read or copied. No sudo, no system-wide install (bcrypt in `~/mvp6-env/venv`, Playwright in `~/mvp6-env/node_modules`).

## 9. ASSUMPTIONs

- **A-01 Recipe.** HEAD → A12 360 → Auth 22 → Claims draft, as CT F3 and the A12 VER-02 run. BC-SOURCE `ebd5d80c…` and Claims
  `normal-source` `edb759a0…` were not layered because A12 is a measured superset: its 47+ Claims files equal `edb759a0…`, and its
  differing files add Shipment root emission (`LifecycleCorrelationId`), Capacity/S&OP and the registration foundation. Layering BC
  after A12 (the prompt's literal order) would have overwritten A12's `Program.cs`/`.csproj`.
- **A-02** `_shared-integration` was applied to the environment copy only (draft README step 3); `ocelot.json` was re-serialised (formatting only).
- **A-03** Auth 22 = the whole `f50350b8…` archive extracted, its 22 manifest rows verified (as K01 and A12 VER-02 do).
- **A-04** Lane-DB-only setup writes where no product API exists: Platform tenant 97c5, Auth role grants, carrier link, soft deletes.
- **A-05** CU-10 positive branch run with `supplychain.carriers.read` added (F-CU10); the first run without it is kept.
- **A-06** Test Mongo for SupplyChain tests: 34794 + the hard-coded 57192 (OS ephemeral range), `enableTestCommands=1`, lane-owned, stopped.
- **A-07** The Q64a spec ran unchanged; row-scoped lane flows cover what its selector/race defects could not reach — no new rule or endpoint.
- **A-08** "With FIXES" = `FIXES.patch` (verified: applied to the delivered files it reproduces the 8 fixed files byte-for-byte).

## 10. Changed files / cleanup

Written: this folder and `../mvp6-q24b-kit-validation-01/` only (+ `~/mvp6-env/`). No product, pack, contract, rule, ledger or git change.
All lane processes stopped (K11 both lanes; test mongods shut down; no Chromium left); `~/mvp6-env` kept.
End-of-lane repo check: see `FINAL-CHECK.txt`.

## 11. Start / end, to-do

Start 2026-09-26T19:08:57Z; end in `FINAL-CHECK.txt`.
To-do for CT: (1) rule on D-01/D-02 and FIXES.patch for the next draft revision; (2) pack §32.2 wording (F-PACK-32.2) and §32.14 (Q64a F2);
(3) role prerequisite `supplychain.carriers.read` (F-CU10); (4) integration owner: Ocelot count, 401 code (O-01); (5) spec author: S-01/S-02;
(6) kit owner: K-F1…K-F7; (7) open rows CU-03/04/05/12/13/18/19/20-boundaries/22/26 need a follow-up lane; CU-27/28/30 stay BLOCKED.

Agent PASS ≠ CT ACCEPTED — returning to CT.
