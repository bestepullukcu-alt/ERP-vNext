# Q231 — MOD-0183 against the §18.0 vertical-slice gate · SOP §22 structured report

| Field | Value |
|---|---|
| Work Package / Lane / Evidence | Q231 · `AL-SCM-0183-SLICE-GAP` (INS) · required E1 — **reached: E1** (static; nothing built, run or started) |
| Placement | Claude app → Code tab → Local, `uname -s` = Darwin. **G2 placement waiver applied — Cowork withdrawn by owner.** |
| Agents | Phase A `read-only-auditor` — mode **strict**, no fixes, no Write. Phase B `documentation-writer` — this folder only |
| Measured against | `docs/guides/operations/control-tower-sop.md` §18.0, lines 974–995, read verbatim. No other part of the SOP was read |
| Scope read | `src/*/Features/{Shipments,SourceIntake}/` and the shared host files MOD-0183 runs through (controller, middleware, schema, behaviors, Program.cs) · root test files + `Shipments/` · `Views/SupplyChain/Shipments/` (10 .cshtml) · `SupplyChainShipmentsController.cs` · 4 scripts · 7 resx · the MOD-0183 pack (395 lines, in full) |
| Start / End (Europe/Istanbul) | 2026-10-02 23:00:09 +03 / see `ARTIFACTS.sha256` header |

```text
Agent Verdict:        GAP LIST DELIVERED — 8 of 15 rows MET, 7 NOT MET. Agent verdict ≠ CT ACCEPTED.
Branch / HEAD:        feature/mvp6-logistics @ 4a8d4d4b339528a88e6220fb8402e5a2c771136c (matched)
Worktree status:      git status --porcelain: 665 at start and at end. No .git/index.lock.
                      No git diff, no fetch, no git write, no gh.
Changed files:        this record folder only
Build / Tests:        NOT RUN (Q217 owns the build). Test outcomes are cited from Q208 (Shipments 74/74).
Fixes:                none. No pack checkbox ticked.
```

## 1. The fourteen minimums and localization (`SLICE-GATE-MATRIX.tsv`)

| # | Minimum | Verdict | One line |
|---:|---|---|---|
| 1 | Golden flow | **NOT MET** | Backend sequence is written (readiness record) and tested; the five §18.0 elements are not written together, and nothing is written for the UI journey |
| 2 | No-shell | **NOT MET** | Controls have real code on both ends, but the gateway has no `/api/shipment-bundle` route and nothing seeds the permissions |
| 3 | Contract blocker | **NOT MET** | The UI exists without a UI contract in the pack; UI and backend disagree on 500/403 error codes. 3.0.0 vs 3.1.0 is Q218's |
| 4 | Persistence | MET | L3 chosen in the readiness record; majority-committed transaction; restart test. POD uses the same path |
| 5 | Concurrency | MET | Internal version + atomic state check; conflict = 422 / 409, tested. No etag on the wire |
| 6 | Idempotency | MET | Receipt replay, unique index, tested incl. 8 concurrent same-key creates |
| 7 | Validation | **NOT MET** | Server: yes, tested. Client: create form only; Change Status and Capture POD have none |
| 8 | RBAC / Tenant | MET | Server permission + tenant/LE isolation, tested through HTTP. UAS-001 gate present on all three pages; live check still required |
| 9 | Data classification | MET | No payload, token, recipient or note in any log statement; audit drops the note |
| 10 | Audit / Evidence | **NOT MET** | Every mutation audited. POD evidence is unverified free text; no shared evidence service is used |
| 11 | Consistency | MET | Atomic, decided in the pack, one transaction, tested |
| 12 | UX states | **NOT MET** | List shows a load failure as empty; server failures shown as validation errors; see `UX-STATES.tsv` |
| 13 | Observability | **NOT MET** | Correlation ID and redacted logs: yes. Metrics and traces: none exist |
| 14 | Do-not-change | MET | Explicit in pack §5–§6 for the backend; the frontend files are outside it |
| — | Localization | MET | 63 keys × 7 languages, none missing. Status names are not localized |

No row is left as REQUIRES RUNTIME as a whole: each could be decided from the code. Nine sub-questions inside the
rows cannot be, and are listed in `RUNTIME-REQUIRED.tsv` with the measurement that would settle each.

## 2. What the green 74/74 does and does not cover

The backend tests are real HTTP tests on a composed host with signed tokens and a replica set
(`ShipmentTests.cs:46-62`, `:120-300`). They prove rows 4, 5, 6, 8, 11 and the server half of 7.

They do not touch: the gateway, the web adapter's call to it, permission seeding, or any screen.
The frontend tests (`Diten.Web.Tests`, 20 cases for Shipments) check source text and the adapter in isolation.
So nothing in the tree has exercised browser → web → gateway → service for this module — and by row 2 it
cannot work today.

## 3. Findings

| ID | Severity | Finding | Evidence |
|---|---|---|---|
| **F-Q231-1** | 🔴 Blocker for a vertical slice | The UI's four operations all go through the gateway, and the gateway has no route for `/api/shipment-bundle`. Backend wired, frontend wired, the link between them missing — the K4 shape. | `ocelot.json` (0 hits); `SupplyChainShipmentsController.cs:63,72,82,96,108` |
| **F-Q231-2** | 🔴 Blocker | Nothing gives a user the six `supplychain.shipments.*` keys: no seed outside the service, and `ShipmentTrackingPodManifestProvider` / `ModuleRegistrationHostedService` exist but are not registered in `Program.cs`. With the UAS gate in place, every user would get the access-denied panel. Needs a run to confirm (R-02). | `Program.cs:43-49`; grep outside the service = 0 files |
| **F-Q231-3** | 🟠 High | The pack still says this module has no UI (`shell: none`, §9, §11, `form_field_count: 0`). The tree has 10 views, a controller, 4 scripts and 7 resx files. There is no written UI contract: no screens, fields, golden reference or UI acceptance rows. | pack lines 6-7, 15, 177, 191; `Views/SupplyChain/Shipments/` |
| **F-Q231-4** | 🟠 High | UI and backend disagree on error codes. The backend sends `INVALID_REQUEST` for 500 and for 403. The UI maps `INTERNAL_ERROR` and `PERSISTENCE_UNAVAILABLE`, which the backend never sends. Result: a server failure or a denied save is shown as "Check the entered values". | `ExceptionHandlingBehavior.cs:12`; `HasPermissionAttribute.cs:16`; `create.js:59-60`; `details.js:49-52` |
| **F-Q231-5** | 🟠 High | The list shows a failed load as an empty table ("No shipments were found") plus a toast. With F-Q231-1 that is what every list load would look like. | `index.js:51, 57-59, 77` |
| **F-Q231-6** | 🟠 High | No client validation on Change Status and Capture POD. An empty date throws inside the submit handler and the user sees nothing. | `details.js:132, 136`; both offcanvas files are `novalidate` |
| **F-Q231-7** | 🟠 High | POD is evidence-bearing, and the readiness record calls it regulated, but its evidence is a free-text list that nothing verifies. No shared evidence service is called. The pack prescribes "reference only", so this is a gate gap that needs an owner decision, not a coding slip. | `CapturePodValidator.cs:12`; `_PodOffcanvas.cshtml:4`; pack lines 110, 207 |
| **F-Q231-8** | 🟡 Medium | No golden flow document with the five §18.0 elements. The backend sequence is one sentence in the readiness record; the UI journey is unwritten. | readiness record `:293-294` |
| **F-Q231-9** | 🟡 Medium | No metrics and no traces in the service. §18.0 asks for "required" ones without a list; none is defined for this module. | grep = 0 |
| **F-Q231-10** | 🟡 Medium | Note length: 2000 in the UI, 1000 on the server. A 1500-character note passes the UI and the adapter and is rejected by the service. | `ShipmentViewModels.cs:34,42`; `ShipmentNoteRules.cs:15` |
| **F-Q231-11** | 🟡 Medium | The eight shipment status names are hard-coded English in the filter and shown raw in the list, the details page and the target dropdown — in all seven languages. | `_Filter.cshtml:4`; `index.js:19`; `details.js:77, 90` |
| **F-Q231-12** | 🟡 Medium | If the list API answers 403, the page shows a toast and an empty table — both forbidden by UAS-001 §6. Reachable only when the page-level snapshot and the API disagree. | `index.js:28, 35, 51` |
| **F-Q231-13** | ⚪ Low | The internal version-check miss returns 500 `INVALID_REQUEST`, not a defined conflict. Whether it can happen in practice needs a run (R-06). | `ShipmentRepository.cs:84` |
| **F-Q231-14** | ⚪ Low | `Transition` in the web adapter reads `model.TargetStatus` before the model-state check; a request with no body would fail there. | `SupplyChainShipmentsController.cs:92-95` |
| **F-Q231-15** | ⚪ Low | The details page needs a stored lifecycle root to act. A shipment without one cannot be transitioned from the UI, and the message shown is the validation one. | `details.js:15-16, 113-114` |
| **F-Q231-16** | ⚪ Low | L1/L2/L3 is named only in the readiness record, not in the pack. | readiness record `:161, :254` |
| **F-Q231-17** | ⚪ Info (positive) | Localization is complete: 63 keys in each of 7 languages, none missing, none empty, bridge complete. The backend rows for persistence, idempotency, consistency and isolation are well tested through HTTP. | `SLICE-GATE-MATRIX.tsv` rows 4, 6, 8, 11, L10n |

## 4. What would close the slice (not done here)

1. Gateway routes for the five shipment operations — integration-agent (Q209).
2. Permission registration: register the manifest provider and hosted service, or seed the six keys.
3. A UI section in the pack: screens, fields, golden reference, UI acceptance rows — and the written golden flow.
4. One error-code vocabulary between backend and UI (ties to Q218).
5. Client validation for the two offcanvas forms; distinct error state for the list.
6. An owner decision on POD evidence: free-text references, or the shared evidence service.
7. Then, and only then, the runtime rows R-01 … R-09.

## 5. Refused / not done

- No build, no test run, no service start, no browser run, no edit, no pack checkbox ticked.
- The 3.0.0 / 3.1.0 contract question was recorded as a dependency on Q218 and not examined.
- The definitions of L1/L2/L3 and of "regulated flow" lie outside §18.0 and were not read; rows 4 and 10 say so.
- SourceIntake (the warehouse adapter) was inventoried but not walked against the gate: the pack keeps automatic
  intake blocked (pack line 279) and no UI or HTTP route reaches it.
- No `git diff`. The no-change proof is porcelain-based (665 = 665).

## 6. Files

`SOP-22.md` · `SLICE-GATE-MATRIX.tsv` (14 rows + localization) · `RUNTIME-REQUIRED.tsv` (9 rows) ·
`UX-STATES.tsv` (10 views × 6 states) · `ARTIFACTS.sha256`

Return to CT; CT decides.
