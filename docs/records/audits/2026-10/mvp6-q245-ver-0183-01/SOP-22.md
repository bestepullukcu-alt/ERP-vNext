# Q245 — VER MOD-0183 Shipment Tracking & POD against its own pack · SOP §22 structured report

| Field | Value |
|---|---|
| Work Package / Lane / Evidence | Q245 · `AL-SCM-VER-0183` (VER) · required E1 — **reached: E1** (static; nothing built, run or started) |
| Placement | Claude app → Code tab → Local, `uname -s` = Darwin. **G2 placement waiver applied — Cowork withdrawn by owner.** |
| Agents | Phase A `read-only-auditor` — mode **strict**, no fixes, no Write. Phase B `documentation-writer` — this folder only |
| Measured against | The pack as it stands: `MOD-0183-shipment-tracking-pod.md`, 395 lines, sha256 `2a65ce1d…` (the Q233+Q234 correction is not applied) |
| Read in full | 40 feature files (876 lines) · 21 shared host files · 7 test files (962 lines) · the pack · the manifest provider. Frontend: inventoried, not re-walked (Q231 did) |
| Start / End (Europe/Istanbul) | 2026-10-03 00:37:46 +03 / see `ARTIFACTS.sha256` header |

```text
Agent Verdict:   BLOCKED — not ready for CT acceptance. Agent verdict ≠ CT ACCEPTED.
Branch / HEAD:   feature/mvp6-logistics @ 4a8d4d4b339528a88e6220fb8402e5a2c771136c (matched)
Worktree:        git status --porcelain: 665 at start and at end (see ARTIFACTS.sha256). No .git/index.lock.
                 No git diff, no fetch, no git write, no gh.
Build / Tests:   NOT RUN. Outcomes cited from Q208 (Shipments 74/74).
Fixes:           none. No pack checkbox ticked.
```

## 1. The number

**105 pack rows: 73 MET · 16 NOT MET · 13 INSUFFICIENT EVIDENCE · 3 REQUIRES RUNTIME.** Every row is in `ACCEPTANCE-MATRIX.tsv`.

| Pack section | Rows | MET | NOT MET | INSUFFICIENT | REQUIRES RUNTIME |
|---|---:|---:|---:|---:|---:|
| §16 Acceptance | 14 | 10 | 0 | 3 | 1 |
| §18 Ready-for-dev | 12 | 9 | 2 | 1 | 0 |
| §13 Failure paths | 8 | 7 | 0 | 1 | 0 |
| §12 Validation | 11 | 4 | 0 | 7 | 0 |
| §8 Runtime | 9 | 9 | 0 | 0 | 0 |
| §17 Test expectations | 8 | 4 | 2 | 0 | 2 |
| §3 Owned objects | 10 | 7 | 3 | 0 | 0 |
| §4 Fields | 7 | 7 | 0 | 0 | 0 |
| §4 Indexes | 5 | 3 | 2 | 0 | 0 |
| §10 File convention | 3 | 3 | 0 | 0 | 0 |
| §14 Authorization | 7 | 4 | 2 | 1 | 0 |
| §15 Gateway | 1 | 0 | 1 | 0 | 0 |
| §9/11 UI | 1 | 0 | 1 | 0 | 0 |
| §22 Self-registration | 9 | 6 | 3 | 0 | 0 |
| **Total** | **105** | **73** | **16** | **13** | **3** |

The two lists the WP names:

- **§16 Acceptance criteria — 11 boxes in the pack.** 7 fully MET (lines 243, 244, 246, 247, 248, 250, 251).
  2 partly MET: line 245 (create and POD replay proven, transition replay untested) and line 249 (reads and 403
  proven, cross-tenant mutations untested). 1 REQUIRES RUNTIME (line 252, live mock smoke and payload match).
  1 INSUFFICIENT EVIDENCE (lines 253–254, no G5 evidence plan found). The three compound boxes are split, hence 14 rows.
- **§18 Ready-for-dev — 12 items.** 9 MET, 2 NOT MET (line 272 "no UI invented" — the UI exists; line 279
  Warehouse gates, open by design), 1 INSUFFICIENT EVIDENCE (line 270, "was read" cannot be verified).

How to read MET: the code does it **and** a test that passed in Q208 proves it, or reading alone proves it.
For every behavioural row the proof is a test on the in-process test host. **No MET row is runtime evidence.**

## 2. One thing the dispatch assumed that is not so

The WP says every criterion needing HTTP, JWT or RBAC evidence is insufficient today because nothing in this
service has executed a controller, handler, validator or real token. For MOD-0183 that is measured **false**
(`TEST-REACH.md`): 12 of the 74 tests send 62 HTTP requests through the composed host with a signed HS256
token. So those rows are marked MET, with the limit stated: test host, test-signed token, `Testing`
environment. If CT wants the stricter reading — nothing counts until it runs behind the gateway — then
AC-02 to AC-07, the failure paths and the authorization rows all move to REQUIRES RUNTIME, and the MET count
falls to the structural and document rows only. That is CT's call; the matrix gives both the verdict and the test.

## 3. Scope (`SCOPE-DELTA.tsv`)

- Backend: 40 feature files and 21 host files are inside the pack's Repo Scope. 2 feature files and 3 test files
  are untracked; 6 feature files and `ShipmentTests.cs` are modified against HEAD.
- **Present, not authorized by the pack:** the manifest provider and its test (§22 line 328 "authorizes no
  code"), three shared module-registration files, and the whole frontend — 29 files (11 view-folder files,
  controller, view models, 4 scripts, 7 resx, 5 test files). The frontend is F-Q231-3, not new. The prepared
  Q233+Q234 correction would authorize those 29 and replace matrix rows RD-04 and UI-01.
- **Required, absent:** the generated API reference under `docs/reference/architecture/api/`; the
  `ShipmentException` record; lifecycle and reconcile queries; the WarehouseReferenceId index; architecture
  tests for this service.
- The frozen contract file shows as modified in the worktree; the pack forbids Phase B to edit it. Q218 owns it.

## 4. Boundary (`BOUNDARY-REFS.tsv`)

- **Into MOD-0183:** Loads, Returns and Claims each read one shipment by HTTP GET. 0 type references to the
  Shipments or SourceIntake namespaces from any other feature; 0 access to the eight `sce_shipment*`
  collections from outside. **No downstream module mutates shipment or intake state.**
- "Triage state" has no counterpart in this service: the word occurs 0 times in its code. Intake state is the
  three `sce_shipment_source_*` collections, written only by `SourceIntakeStore` and `ShipmentRepository`.
- **Out of MOD-0183:** 0 references to any other feature. Warehouse and Inventory are read by GET-only clients
  that are not registered.
- Two couplings that are not mutations: five other middlewares write tenant and correlation into the shared
  `RequestContext`, whose scope type is a MOD-0183 domain record; and `Program.cs:68` runs Returns and Claims
  requests through the Shipment middleware (Q217).
- The ownership claims of pack §2 hold in both directions.

## 5. Findings

| ID | Severity | Finding | Evidence |
|---|---|---|---|
| **F-Q245-1** | 🔴 Blocker | MOD-0183 has no runtime evidence. The pack's own §17 asks for E4 on state changes; the service does not start in Development and nothing outside the test host has called it. | TE-07; Q217; Q232 |
| **F-Q245-2** | 🟠 High | Three behaviours the pack names have code and no test: transition replay with the same key, cross-tenant mutation → 404, POD without its permission → 403. | AC-03b, AC-07b, AU-04 |
| **F-Q245-3** | 🟠 High | No test asserts an error code or the correlation id of an error body. The pack's failure paths name `INVALID_SHIPMENT_TRANSITION` and `POD_ALREADY_CAPTURED`; tests check 422 and 409 only. A wrong code with the right status would pass. | `TEST-REACH.md`; FP-02, FP-05 |
| **F-Q245-4** | 🟠 High | One of the 74 green tests asserts nothing: `ShipmentRootHttpTests.cs:12-13`. It defers to Python probes outside the suite. | `TEST-REACH.md` |
| **F-Q245-5** | 🟠 High | "API contract tests for every success and declared error response" is not met: 12 of 17 declared responses are exercised, and no body is validated against the OpenAPI file. | TE-05 |
| **F-Q245-6** | 🟠 High | The manifest provider disagrees with pack §22: page codes `SHIPMENTS_CREATE` / `SHIPMENTS_DETAILS` (pack: `SHIPMENT_…`), route without `:guid`, four actions on the list page, `DISPATCH` instead of `CHANGE_STATUS`. Its test pins the provider's values, so it is green. | M-00, M-04, M-05 |
| **F-Q245-7** | 🟡 Medium | Seven validation rules are in code with no test: empty warehouse reference, empty ship-to, empty lines, date order and UTC, POD state and time, recipient name, evidence list. | V-03 … V-10 |
| **F-Q245-8** | 🟡 Medium | Owned objects short of the pack: no `ShipmentException` record; no lifecycle or reconcile query; no assign command (the pack defers it); `ShipmentLifecycleEntry` is declared and unused; the `reconcile` permission guards nothing. | OB-03, OB-05 … OB-07, AU-06 |
| **F-Q245-9** | 🟡 Medium | Two of the five indexes the pack lists are not created: WarehouseReferenceId, and the POD unique index (the POD is embedded; its uniqueness is enforced and tested another way). | IX-03, IX-05 |
| **F-Q245-10** | 🟡 Medium | The architecture test project does not mention this service; the pack requires it to. | TE-08 |
| **F-Q245-11** | 🟡 Medium | The inventory read client and the warehouse intake are tested as components and are not wired into the host. "Inventory is read only through the client" holds because nothing reads inventory at all. | AC-08, AC-09 |
| **F-Q245-12** | ⚪ Info (positive) | MOD-0183 is the one module whose tests go through HTTP on a composed host with signed tokens. Persistence, atomicity, idempotent create and POD, read isolation and the transition matrix are well proven at that level. | `TEST-REACH.md` |
| **F-Q245-13** | ⚪ Info | The WP's first-read list names `REACHABILITY.md` in the Q231 folder. That folder has no such file (it has `SOP-22.md`, `SLICE-GATE-MATRIX.tsv`, `RUNTIME-REQUIRED.tsv`, `UX-STATES.tsv`). Q231's `SOP-22.md` was read instead. | folder listing |
| **F-Q245-14** | ⚪ Info | The dispatch says disk is at 2.8 GiB; `df` at preflight showed 6 GiB free. Not relevant to a static WP. | preflight |

Not re-reported as new: F-Q231-1 (gateway), F-Q231-2 (permissions), F-Q231-3 (UI outside the pack), the unapplied Q233+Q234 correction.

## 6. Shared seams MOD-0183 needs to become real (named, not patched)

| # | Seam | State | Owner per the pack |
|---:|---|---|---|
| 1 | Service starts in Development — `Program.cs` / `Application/DependencyInjection.cs:11` | fails at startup | Q236 |
| 2 | Gateway route `/api/shipment-bundle/shipments**` → 5061 with header passthrough | 0 routes | integration-agent (pack §15) |
| 3 | Permission seed for the six `supplychain.shipments.*` keys | none | single owner / integration WP (pack §14) |
| 4 | Manifest provider registration + `ModuleRegistrationHostedService` + `PlatformRegistration` settings | present, unregistered | integration owner (pack §22) |
| 5 | Three navigation keys × 7 languages in `SharedResource.{lang}.resx` | Q231 found them in `en`; other languages not checked here | integration owner + l10n (pack §22) |
| 6 | Event transport (`IEventTransportPublisher`) | unregistered; outbox rows stay Pending | platform / integration |
| 7 | Configuration source for `JwtSettings:*` and `Mongo:*` | not in appsettings (Q217) | undecided |
| 8 | Warehouse and Inventory read clients: registration and base URLs; GAP-0183-01/02/03 | unregistered; gates open | central CT (pack §20) |
| 9 | Contract pin 3.0.0 / 3.1.0 | open | Q218 |
| 10 | Architecture tests covering this service | absent | not assigned |
| 11 | Pack text for the UI (Q233+Q234 correction) | prepared, unapplied | CT / owner |

## 7. Verdict — BLOCKED. What is missing, in order

1. A started service: no runtime evidence is possible until Development boot is restored (Q236).
2. E4: one authenticated request per state change against the started service, read back (pack §17).
3. Gateway route and permission seed, so that the request in 2 can go through the gateway (seams 2, 3).
4. Tests for the three untested named behaviours: transition replay, cross-tenant mutation, POD permission.
5. Error codes asserted, and the five unexercised declared responses covered (create 422, create 503,
   transition 409, POD 404, POD 422).
6. A live mock smoke and schema check against the contract file now in the tree (pack line 252).
7. A CT ruling on the pack-versus-tree gaps: `ShipmentException`, lifecycle and reconcile queries, two indexes,
   architecture tests, the API reference, the manifest deviations.
8. The pack corrected for the UI (apply or reject Q233+Q234), so that 29 present files stop being outside scope.
9. The vacuous test replaced or removed, so that "74" means 74.
10. A G5 evidence plan, or a pointer to where it is.

Items 4, 5 and 9 are test work inside the module. Items 1–3 and 6 are seams. Items 7, 8 and 10 are decisions.

## 8. Refused / not done

- No build, no test run, no service start, no edit, no checkbox ticked, no pack patched.
- Q231's §18.0 walk was not repeated; gateway, permissions and UI rows cite it.
- The frontend files were inventoried for scope only. `tests/root_uptake/` was listed, not opened.
- The MVP-6 development plans were not read (AC-11). DCP-002 was not re-run.
- No `git diff`. The no-change proof is porcelain-based.

## 9. Files

`SOP-22.md` · `ACCEPTANCE-MATRIX.tsv` (105 rows) · `SCOPE-DELTA.tsv` (22 rows) · `BOUNDARY-REFS.tsv` (13 rows) ·
`TEST-REACH.md` · `DEPENDENCY-EVIDENCE.md` · `ARTIFACTS.sha256`

Return to CT; CT decides.
