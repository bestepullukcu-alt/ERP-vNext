# SOP §22 — MVP6-SHIPMENT-A12-RUNTIME-INDEPENDENT-VER-02 (queue Q04, attempt 2)

Role: MVP6 Terminal runtime lane — independent A12 runtime verifier (`@testing-agent`, `@devops-agent`). This lane did not write the A12 patch.
Run: Claude Code in Terminal, native macOS (arm64), 2026-09-26 10:08–10:5x Europe/Istanbul.
Result: **all A12 runtime criteria PASS; the A08, A09-409 and A09-422 regressions PASS; PNG PASS via `page.screenshot({path})` (CT to rule on the mechanism, see §6).** This is evidence for CT. It is not CT acceptance, full Shipment UI or module acceptance, E5/G5 or rollout.

## 1. Identity and inputs

| Item | Value | Evidence |
|---|---|---|
| Repo / branch / HEAD | `/Users/natig/Projects/ERP-vNext-recovery`, `feature/mvp6-logistics`, `4a8d4d4b339528a88e6220fb8402e5a2c771136c` (matches; checked again at cleanup) | `raw/02-compose-source.txt`, `CLEANUP.tsv` |
| A12 patch | `b3c7dcbb68636b123f3ea3ad7c3c902709fef6a9775c66cc3d464073653ecefb` | `raw/01-input-hashes.txt` |
| A12 successor archive | `7b6a0d1abea649d920040a039972c5d4928825e9cf2cc185487b1fe5f0f3314d` | same |
| A12 360 manifest | `8ffa6c96d29f8004940f310ed704a2c05eaa38ffa501d6114a5f9251e6e00d36` | same |
| Auth overlay archive | `f50350b8e541a4c1cbc640926c581cf510b4c2dbb57b31ef4193bf4c25c9e2cd` | same |
| Auth FINAL-22 manifest | `b9713185aba02bf325a9149b37a8cc6ffe3b1dd0e979febdaa773946ae9b1731` | same |
| Package self-check | `shasum -c ARTIFACTS.sha256` for the rework package: 7/7 OK | same |

## 2. Preflight

| # | Step | Result | Evidence |
|---|---|---|---|
| P1 | Five input hashes | **PASS** (5/5 exact) | `raw/01-input-hashes.txt` |
| P2 | New `/private/tmp/mvp6-a12-rtver02.S0rbZc`: `git archive 4a8d4d4` (14,251 files) → A12 360 overlay → Auth 22 overlay. No dirty-checkout source (388 working-tree status entries ignored). | **PASS**: overlap 0 archive paths / 0 manifest paths; **360/360** and **22/22** after each overlay and again at the end. Source-tree manifest `e4528e14d63a3c0bf70c9e1aa10dc54937b61cd547ce0d508f523044aa14b162` | `raw/02-compose-source.txt`, `raw/source-manifest-verification.txt`, `raw/SOURCE-TREE-MANIFEST.tsv`, `scripts/compose_source.py` |
| P3 | Disposable gateway config from the HEAD `ocelot.json`, lane ports only; repo `ocelot.json` never edited | **PASS**: 275 downstream entries, 94 mapped to lane ports, 181 sent to the closed sink 5499. Source `ocelot.json` `b0121d2f…`, runtime copy `0de8d3eeb022477e8d1d7f187b6b453da1c23c75d127bb01930584fd2be81be6` | `raw/gateway-routes.tsv` |
| P4 | `BUILD-INPUT-MANIFEST.tsv`, kept separate from the owned 360 manifest; missing-dependency check | **PASS**: 6 entry projects, 29-project closure, 6,968 files (254 from the A12 overlay, 2,798 from the Auth archive, 3,916 from HEAD); **0 missing** | `BUILD-INPUT-MANIFEST.tsv`, `raw/03-build-inputs.txt` |
| P5 | Native .NET | **PASS**: `/Users/natig/.dotnet/dotnet`, SDK 8.0.417, runtimes 8.0.23; all six processes loaded `Microsoft.NETCore.App/8.0.23`; `DOTNET_ROLL_FORWARD=LatestPatch`; no .NET 10 | `raw/runtime/toolchain.txt`, `raw/processes.tsv` |
| P6 | Release build, online restore | **PASS**: 6/6 restore 0, build 0 (`-c Release`, no shared compiler); runtimeconfig `net8.0` / `8.0.0` | `raw/build/`, `raw/binary-sha256.txt` |
| P7 | Isolated Mongo (DB-010) | **PASS**: lane-owned `mongod` 8.0.18, replica set `rsShipmentA12RtVer02` PRIMARY on `127.0.0.1:34994`, DBs `Diten<Svc>_ShipmentA12RtVer02`. Effective config was resolved before start (K04 check: 0 findings, no 27017). `netcheck` ran after start and again before cleanup: 0 connections to 27017 | `raw/mongo-rs-status.json`, `raw/effective-config-*.json`, `raw/effective-config-findings.txt`, `raw/netcheck*.tsv` |
| P8 | Fresh ports (slot 4; unused by earlier lanes, which used slots 6 and 9) | Gateway 5400, Web 5401, Auth 5456, Platform 5457, MDM 5459, SupplyChain 5461, sink 5499 (closed), Mongo 34994 | `raw/PORTS-before.tsv`, `raw/ports.json` |

## 3. Source → binary → process → browser

`SOURCE-BINARY-PROCESS.tsv` (evidence-kit K09, captured while running): six components bound to source tree `e4528e14…`. Each DLL hash matched its build hash, each PID was still the listener, and every loaded runtime was 8.0.23. Browser binding (`raw/browser-binding.tsv`, 23/23 match):

- the served `/assets/js/SupplyChain/Shipments/details.js` is byte-identical to the A12 target, `69626e6143855b0f0929d6079c7b85b43fd63b46ec0519eaf3e49c9d34ae3bda`;
- the served `index.l10n.js` equals its source;
- every browser URL used was on `127.0.0.1:5401`.

| Component | DLL SHA-256 | PID | Port |
|---|---|---|---|
| Auth | `e1760930cd7ccd98d409c9fe32bcb9f929f5d78c438ac90395a8bc4b12d78a80` | 16224 | 5456 |
| Platform | `b79e657bc9bef5018b9e49e8a8c3fba5f5b5c7d547df11784a923725f320d16b` | 16257 | 5457 |
| MDM | `a8005ab5fd2bdc7b9f95d1d080d7edfe555151b6aa322cf39de4154aa7bf6fd0` | 16319 | 5459 |
| SupplyChain | `d0e1888c757a88c265339123b056cb86097f869f1ba5cb9e4ec412b547234eb6` | 16336 | 5461 |
| Gateway | `6a86303976baf6a312e9ee620272817b175f8059863ae0dd36784c53e7476e85` | 16357 | 5400 |
| Web | `25e52b72c5f1b2b64b47b767cd826a575d5aee5b808d695f84594af7a55105fa` | 16378 | 5401 |

## 4. Identities and fixtures (setup, not acceptance)

- **Actors (fresh real Auth):** actor-a `john.doe.t97` (T1/LE-A), actor-b `jane.smith.t97` (T1/LE-A), actor-le-b `bob.johnson.t97` (T1/LE-B); tenant `97c59330-…`.
  - Passwords came from `secrets.token_urlsafe(24)` inside the lane supervisor process and were never written to a file.
  - Each actor's password hash was replaced in the lane Auth DB with bcrypt(12) of that password. The logins went through the real Web login page (actors) or the Gateway `/api/tenant-auth/login` (fixture calls). The Auth-issued claims were checked: tenant; `legal_entity_id` LE-A `06df9bbb-…` / LE-B `6566b5a3-…`; 5 `supplychain.shipments.*` permissions (`raw/fixture/identity-org-fixture.json`).
- **Browsers:** three Playwright persistent profiles, one per actor (`W/browser/<actor>`). There was no cookie copying between them.
- **Organisation fixture, created through the product APIs:** fixture-admin `alice.williams.t97`, whose password existed only in the fixture process, created LE-A and LE-B (MDM create + activate, lookup-validation referenceable), two org units, three positions and three position assignments (Platform). The legal-entity claim then came from the product chain Auth → Platform internal scope → MDM service-identity validation.
- **Lane-DB-only fixture writes** (no product API exists for these):
  - Platform tenant record for 97c5 (`scripts/platform_tenant_fixture.js`);
  - Auth roles `A12VerShipmentOperator` and `A12VerFixtureAdmin` with their grants (`scripts/auth_fixture.js`);
  - soft-delete of the stale actor-a assignment from a failed fixture attempt (`scripts/stale_assignment_cleanup.js` + `lane-scripts/stale_assignment_unset.js`);
  - `IsDeleted=true` on shipment DELETED and on both LATE fixtures (late-async attempts 1 and 2), each by the harness with a DB before/after pair.
- **Shipments:** created by actor-a through the Gateway ShipmentBundle API with its real token (`raw/fixture/shipment-fixtures*.json`): VS, NORMAL, PODVIEW (Delivered with POD), DELETED, LATE, A08 (Draft), A09DUP and A09STATE (Dispatched), plus a fresh LATE for the late-async rerun.

## 5. Acceptance matrix

All browser rows ran in the real-Auth Playwright sessions against the frozen successor. DB pairs are in `raw/db/*.json`. Every zero-write assertion covers the whole SupplyChain lane DB (every collection's count) plus the shipment's counts and state (`raw/db/db-assertions.tsv`).

| Criterion | Result | Key evidence |
|---|---|---|
| **Early vertical slice** (first, before the negative matrix): actor-a UI login → Web → Gateway → SupplyChain → Mongo, normal detail | **PASS** | `raw/browser/vs.json`, `png/vs-vertical-slice-normal-detail.png`; GET 200, request correlation = response correlation; zero write |
| A12 normal detail (summary / lines / POD / actions) | **PASS** | `a12.json` `normal-detail-draft`: status, source and root equal the fixture, 2 line rows, POD placeholder, actions `[Change Status]`, keyboard reaches actions. `normal-detail-delivered-pod`: POD section shows `Recipient PODVIEW`, no Capture POD action. PNGs `a12-normal-detail-*.png` |
| A12 cross-LE detail → only localized safe-not-found + support reference | **PASS** (en, tr, ar) | `a12.json` `cross-le-en/tr/ar`: HTTP 404 `SHIPMENT_NOT_FOUND`; heading = localized not-found (`The shipment could not be found.` / `Sevkiyat bulunamadı.` / `تعذر العثور على الشحنة.`, `dir=rtl`); alert exactly `<notFound> <supportReference>: <correlation>`; no "Loading…"; summary/lines/POD row, actions and both offcanvases hidden + `inert` + `aria-hidden=true`; no shipment values. PNGs `a12-cross-le-*.png` |
| A12 unknown detail | **PASS** | `unknown-actor-le-b` (profile culture was `ar`, carried over from the previous case's cookie) and `unknown-actor-a` (en) |
| A12 soft-deleted detail | **PASS** | `deleted-fixture-visible-before-delete` (control: actor-a sees it while live) → lane-DB soft delete (DB pair PASS) → `soft-deleted-actor-a` safe 404 |
| A12 hidden surfaces hidden/inert, not keyboard-reachable | **PASS** | In every 404 case, 45 Tab presses reached 0 elements inside the hidden surfaces, and a programmatic `focus()` into the offcanvas submit button did not move focus. Positive control: the normal detail reached the actions by Tab. Limit: on the pre-A12 file the 404 state also had no tab stops in those areas, so this row confirms the behaviour but is not itself sabotage-discriminating. The A12-specific `hidden`/`inert`/`aria-hidden` state is discriminated (baseline: `offcanvasesHiddenInert=false`) |
| A12 late async responses do not re-expose stale surfaces | **PASS** | `raw/browser/late.json`: the first real GET (200, shipment live) was held; the shipment was soft-deleted; a second load returned a real 404 and rendered safe-not-found; the stale 200 was then released and dropped by the load-version guard; final DOM safe, no data; zero write. Attempt 1 is kept (`late-attempt1-harness-verdict-defect.json`): same UI outcome, but the harness verdict read the wrong network entry (§7) |
| A12 backend 404 `SHIPMENT_NOT_FOUND` + correlation unchanged | **PASS** | Browser adapter: request `X-Correlation-Id` = response header = body `correlationId` in every 404 case. Direct Gateway probe with real tokens (`raw/backend-404-probe.json`): cross-LE, unknown ×2 and soft-deleted all return 404, `SHIPMENT_NOT_FOUND`, `contractVersion v1`, request = header = body correlation. Positive control: actor-a on a live shipment returns 200 |
| A12 DB before/after zero writes | **PASS** | 17 zero-write pairs PASS; fixture mutations are asserted separately |
| A12 data/API isolation (EVIDENCE-REUSE row 2: rerun) | **PASS** | actor-le-b same-origin transition + POD on the LE-A shipment: 404 `SHIPMENT_NOT_FOUND`, root correlation, zero write (`cross-le-mutation-isolation`). List: actor-a sees its 7 live fixtures and not the deleted one; actor-le-b sees 0 (`raw/browser/list.json`) |
| **Negative controls** | **PASS** | (1) Sabotage: the pre-A12 `details.js` (`1f7b36fc…`, from the pre-A12 360 archive `490d51be…`) served by a test route only: the same detector FAILs (localized "Loading…" heading, visible skeleton, no inert) — defect reproduced, `png/negctl-sabotage-baseline-detailsjs-cross-le.png`. (2) The pre-A12 file with a held 200 + a later 404: stale data is re-exposed (the successor drops it). (3) Positive controls: normal detail, deleted-before-delete, backend 200, list visibility for the owner LE |
| **Regression A08** stale transition (two browsers) | **PASS** | `raw/browser/a08.json`: both rendered Draft; actor-b opened its form before actor-a committed; actor-a Draft→Planned 200 (+1 history/receipt/audit/outbox); actor-b stale submit → 422 `INVALID_SHIPMENT_TRANSITION`, alert with root `a7b8b8fe…`, refreshed to Planned, actions `[Change Status]`; zero write. PNG `a08-actor-b-after-stale-transition.png` |
| **Regression A09** duplicate POD (409) | **PASS** | `a09dup.json`: both opened the POD form before the mutation; actor-a POD → Delivered; actor-b stale → 409 `POD_ALREADY_CAPTURED`, root reference, refreshed to the authoritative POD (`Recipient A`), POD action gone; zero write |
| **Regression A09** stale eligibility (422) | **PASS** | `a09state.json`: actor-b held the POD form open; actor-a Dispatched→Exception; actor-b → 422 `INVALID_SHIPMENT_TRANSITION`, refreshed to Exception, POD action gone; zero write |
| Accessibility (PRES-183-01) | **INHERITED — no impact** (EVIDENCE-REUSE row 3); not re-verified, **not counted as PASS here** | — |
| A03 / PRES-183-02 | **INHERITED — no impact** (EVIDENCE-REUSE row 4); not re-verified, **not counted as PASS here** | — |
| A10 fault proxy | **NOT RUN — unauthorised** | — |
| **PNG** via a supported save path | **PASS (method for CT to confirm)** | 16 PNGs written with Playwright `page.screenshot({path, fullPage:true})` from the same real-Auth sessions; SHA-256 at capture = on disk (`png/PNG-INDEX.tsv`); no base64, data-URL, raw CDP call or tunnel. See §6 |

## 6. PNG method note for CT

The lane prompt named Playwright's `page.screenshot` to a file as the supported mechanism, and that is the only one used. Two facts for CT's ruling, stated without reinterpretation:

- KIT-SPEC §5 and `ENVIRONMENT.md` describe Playwright on the Mac as driving Chromium over CDP. Playwright does that internally through its own supported API. This lane issued no raw CDP command.
- Unlike the earlier IAB lanes, the browser here is the Playwright Chromium itself, and it is the same real-Auth session that produced the acceptance evidence. There is no separate capture session.

## 7. Deviations, defects found, and how each was handled (none changes a product file)

1. **Kit K01 archive layout.** The A12 archive wraps its members in `mvp6-shipment-a12-rework-src/`, and K01 would have nested them. `scripts/compose_source.py` adapts K01. Its first wrapper heuristic wrongly stripped `services/` from the Auth archive (reported 0/22). The rule was fixed to "strip only a top directory absent from HEAD", and the tree was recomposed from scratch (360/360, 22/22).
2. **Kit K04/K07 secrets on disk.** These conflict with the instruction "never written to files". Instead, K04 rendered placeholders and `scripts/lane_supervisor.py` held the real values in memory, passing them only to child processes. K08's exact scan reads `W/secrets` (placeholders here), so `lane-scripts/exact_secret_scan.py` ran inside the supervisor instead.
3. **Kit K04 forced `BackgroundJobs__Enabled=false`.** Platform crashed at DI validation in Development (`raw/runtime/platform-startup-attempt1-di-failure.txt`). Override: jobs enabled, standard jobs off, dashboard off, Hangfire on lane DB `DitenHangfire_ShipmentA12RtVer02`.
4. **Internal-key pairings (kit open item §7.1), measured.**
   - Auth `InternalEventAuth:ApiKey`, SupplyChain `PlatformRegistration:InternalApiKey` and Web `Platform:InternalApiKey` must all equal Platform `AuthService:InternalApiKey`. Pairing them fixed a 401 on the permission sync.
   - Platform `MdmServiceIdentity` and MDM `PlatformServiceIdentity` got an in-memory lane key.
   - The services were restarted with clean lane DBs; the first startup is kept as `raw/processes-startup-attempt-1.tsv`.
5. **Fixture attempts.**
   - 97c5 was missing from the lane Platform, so login returned "temporarily unavailable"; fixed with the tenant record.
   - The fixture-admin lacked `auth.users.lookup-validation`.
   - A position admits one primary assignment, so a 409 followed; fixed with one position per actor.
   - A stale assignment from a failed attempt made actor-a's LE ambiguous. The product correctly omitted the claim; the stale assignment was soft-deleted in the lane DB.
   - The fixture was rerun, so the first rotation's `previousHashWasSharedSeedHash` value was not captured; later runs show `false` because the hash had already been rotated.
   - Leftover LEs (created and activated), org units and positions from the failed attempts stay in the lane DB without a live assignment; the DB was destroyed at cleanup.
   - Attempt logs are kept in `raw/fixture/`.
6. **Harness defects (not product).**
   - The list probe first used pageSize 100 and later omitted `X-Correlation-Id`, so the backend returned 400. The `a12.json` `list-isolation` FAIL is superseded by `list.json` PASS, which uses the page's own headers.
   - The late-async verdict read the last network entry (the deliberately late 200) instead of the rendering 404. It was fixed and rerun on a fresh fixture; attempt 1 is kept.
   - K09 needed its multi-value arguments in one list; it was rerun.
7. **Local permissions.** CT added `Write(scripts/**)` at 10:27, which also matches this folder's `scripts/` by name. Before the first denial (≈10:35) four lane scripts there were written via Bash; afterwards nothing more was written there, and new scripts went to `lane-scripts/` (`AUTHORITY.md`).
8. **Time zone.** The browsers ran with `timezoneId=UTC`. The forms' default `toISOString().slice(0,16)` in a `datetime-local` input is interpreted as local time; explicit times after the fixture's dispatch time were entered. This is an observation only, outside A12.

## 8. Environment observations (not A12, not relabelled)

- MDM probe path `/api/legal-entities` answers 400 without a tenant (kit health path choice); MDM served every real call.
- Platform logged 20 `AuditOutboxPayloadMappingException` dead-letters (audit outbox) and 26 `{State:l}` warnings; 0 error-level lines in any service.
- The tenant shell sidebar is empty for these actors (no navigation grants in the lane fixture).
- A `?culture=` visit persists as a culture cookie in that profile.

## 9. Evidence reuse (rows for CT to add to `EVIDENCE-REUSE.tsv`; this lane may not edit it)

| evidence | criterion | decision | reason |
|---|---|---|---|
| mvp6-shipment-a12-runtime-independent-ver-02 browser runs | UI183-A08, A09-409, A09-422 | rerun done (targeted regression) | `details.js` `load()` changed by A12; rows 1–2 of `EVIDENCE-REUSE.tsv` executed |
| same | UI183-A12 data/API isolation + safe-not-found DOM | rerun done | new A12 VER |
| PRES-183-01, A03/PRES-183-02 | accessibility, JSON 401 adapters, DataTables modal L10n | inherited (not rerun) | no A12 impact (rows 3–4) |

## 10. Pointers

`AUTHORITY.md` · `EVIDENCE-CHECKLIST.md` · `BUILD-INPUT-MANIFEST.tsv` · `SOURCE-BINARY-PROCESS.tsv` · `CLEANUP.md` / `CLEANUP.tsv` · `SECRET-SCAN.txt` / `SECRET-SCAN-EXACT.txt` / `SECRET-RESCAN-AFTER-CLEANUP.txt` · `COMMANDS.tsv` · `raw/` (browser, db, fixture, runtime, build, effective config) · `png/` + `png/PNG-INDEX.tsv` · `scripts/` + `lane-scripts/` (all lane code; kit files used listed in `raw/kit-files-used.sha256`) · `ARTIFACTS.sha256`.

Return to CT; CT decides.

## 11. Timing (Europe/Istanbul, UTC+03:00; measured, not estimated)

| Event | Time |
|---|---|
| Lane start (repo identity check) | 2026-09-26 10:08:28 |
| Preflight P1 hashes | 10:13:25 |
| Source composed (360/360, 22/22) | 10:16–10:17 |
| Release builds (background) | ≈10:19–10:24 (compile time per project 3–47 s; overlapped with fixture analysis) |
| Mongo PRIMARY / services up (after two config fixes) | 10:25:46 / 10:28:58–10:29:01 |
| Identity + organisation fixture complete | ≈10:36 |
| Shipment fixtures | 10:37 |
| Early vertical slice PASS | 10:41:32–10:41:38 |
| A12 matrix · list · late-async · negative controls | 10:41:55 – 10:44:50 |
| A08 · A09-409 · A09-422 regressions | 10:45:15 – 10:45:46 |
| Binding, backend probe, secret scans, cleanup (K11) | 10:46 – 10:48:22 |
| Lane end (records sealed) | 2026-09-26 10:53:00 +0300 |

- Agent run time: ≈46 min, from lane start to record sealing, all agent-executed.
- Environment waits: builds ≈5 min wall (background), service start-ups ≈1 min in total, browser phases ≈4 min in total.
- Owner waits: none (no question was asked of the owner). Human active time: none.
