# Q84 — MOD-0190 S&OP tenant UI: DRAFT code overlay (chat lane)

🤖 Applying knowledge of @frontend-ui-ux + @l10n-agent + @integration-agent (integration items as overlay only).
Chat lane on the linked Mac folder (Linux VM bridge). Repo `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`.
Start 2026-09-26T18:19:08+03:00 (Europe/Istanbul). Git read-only; no commit (Q03a). **Status: DRAFT — not built, not tested, not
run, not writer-complete.** The only writes are this folder.

Authority: pack §23/§24 approved Q80 (`mvp6-sop-capacity-ui-pack-signoff-owner-decision-01.md` `37f3ff0d…0bea`); UI scope option A
(`mvp6-sop-capacity-ui-scope-owner-decision-01.md` `1814672b…1553`); draft overlays in chat lanes
(`mvp6-draft-overlays-owner-decision-01.md` `05033624…a67c`); modules first (`mvp6-ct-owner-decisions-modules-first-2026-09-26.md` `63e8601e…`).

## Inputs (read-only, sha256)

| Input | sha256 |
|---|---|
| Pack `execution/domains/supply-chain-execution/module-packs/MOD-0190-sop-workflow-signoffs.md` (§23, §24) | `2bdd533f7e558fc7bd18670d23db1bdf51aedd61d5dfa28e8dbd73b20757017f` (= required, checked at start and end) |
| Contract `docs/analysis/contracts/sandop-capacity.openapi.yaml` (3.0.0; six S&OP operations only) | `5213b5353267ad4c25d150975d6f38422bced72f678c02271b0cccf6e768adab` |
| Semantics `docs/analysis/contracts/sandop-capacity-semantics-v3.0.0.md` (x-semantics-annex) | `f9d9d5553d70e1c54af3e03924600f4c8f426de50355ae861a4d3d5cef21ee64` |
| Scope `docs/roadmap/plans/mvp6-ui-scope-190-192-01/UI-SCOPE-0190.md` / `PH15-UI-190.md` / `SHA256SUMS` | `2447669e…7b82` / `fb6ad9d8…5cf8` / `83c6b7e9…e985` |
| Accepted S&OP backend (isolated source) `docs/records/audits/2026-09/mvp6-mod0190-test-oracle-rework-01/source.tar.gz` | `8fa00d40814a81472ec4221ad909321d058dea18a11f64dd2ac16ea800ecb745` |
| — `SandopPlansController.cs` / `SandopContextMiddleware.cs` / `SandopContractError.cs` / `SandopPermissions.cs` / `SandopPlanModels.cs` / `SandopProjection.cs` | `e5d43935…` / `2ca4e8ce…` / `16e408e5…` / `47eb6b51…` / `60e2a3e3…` / `e18450c4…` |
| A12 360 overlay (conventions + build dependency) `docs/records/audits/2026-09/mvp6-shipment-a12-safe404-rework-01/successor-source.tar.gz` | `7b6a0d1abea649d920040a039972c5d4928825e9cf2cc185487b1fe5f0f3314d` |
| Precedent `docs/records/audits/2026-09/mvp6-claims-ui-draft-01/SHA256SUMS`; CT verdicts F3/F6/F8 `mvp6-ct-verdicts-q64a-q77a-q78-2026-09-26.md` | `80da27c9…08d3`; `356cd273…05f3` |
| `AGENTS.md`; `.antigravity/scripts/verify_module_id.py` | `51d92c75…7fcb`; `3d208928…2b63` |

Backend citations (accepted isolated source): routes and permission gates `SandopPlansController.cs:4-20`; header gate
`SandopContextMiddleware.cs:10-20` (X-Correlation-Id "D" form, tenant/LE headers must equal the JWT claims, one
Idempotency-Key on POST); error messages `SandopContractError.cs:4`; request validation `SandopPlanModels.cs:8-25`; response
shapes `SandopProjection.cs:6-9`; keys `SandopPermissions.cs:3`; receipt replay `SandopRepository.cs:20`.

## What was produced

| Part | Content |
|---|---|
| `overlay/` module files (32 = §23.10) | adapter controller (2 view routes + 6 adapters, nothing else), view models, 2 marker classes, 7 views (entry page without list; plan workspace; 3 offcanvases; 2 L10n bridges), 4 JS files, 14 resx (2 markers × 7 languages incl. ar RTL), 3 test files |
| `overlay/services/…` (2 = §24) | `SopWorkflowSignoffsManifestProvider` + M-01…M-08 tests |
| `overlay/_shared-integration/` (13) | Program.cs line (spec), gateway route fragment (4 explicit groups), frontend note, icon proposal, platform checklist, nav keys × 7 — **nothing applied** |
| `runtime-scenarios/` | Playwright spec + README for Q84b (never run) |
| Records | `FILE-PLAN.tsv` (`8667c707e4699e09a513aa4017b5a34166de62722df33b579e60b17eea27853d`), `SOURCE-MANIFEST.tsv` (`38d9533ed24430f49eb82a2fbf827b43ce9066ed85f22074e02e4f0253b06dc7`, 47 rows), `STATIC-CHECKS.txt` (`46a66d62e9e75c1bc04bfda8106a5d139a45efd9dade8f8350d9c04d097ce7ad`, 89 PASS / 0 FAIL), `NOT-VERIFIED.md`, archive `sop-ui-draft-overlay.tar.gz` (`fcf52d827914dc9c1d217b191a416821a024ba1e3314148f488ebbb2c81e5e30`, 47 files, byte-identical to `overlay/`), `SHA256SUMS` |

Behaviour in short: no plan list and no list request (F190-LIST); "Open plan by ID" validates the UUID and only navigates; create
navigates to the returned ID; the workspace shows the server status only and gates "Capture snapshot" (Draft/InReview) and "Record
sign-off" (InReview + ≥1 snapshot) by key and status; sign-off confirms through `window.showConfirm`; every POST carries a
per-intent Idempotency-Key reused with identical body text; after every 201 the workspace reloads from the server; 404 gives one
safe-not-found surface and late responses are ignored; the 14 published codes are localized; scope never comes from the browser.

## Acceptance coverage (pack §23.11)

| Row | Covered by (static, this lane) | Runtime (Q84b) |
|---|---|---|
| SU-VS1 | controller + form tests (bodies, keys) | spec "Early vertical slice" (BLOCKED: target, gateway, permission seed, fixture seed) |
| SU-01 | controller tests (headers, scope server-side); JS tests (no :5000/:5061, no token/scope) | spec "Entry page" (BLOCKED: gateway) |
| SU-02 | JS test (single fetch = create), index.js openPlan | spec "Entry page" |
| SU-03 | form test (3 states per section, no "Loading") | NOT-VERIFIED NV-06 |
| SU-04 | form test (UAS-001 gate first) | spec "Permissions" |
| SU-05 | controller tests (403, zero gateway calls); JS test (key + status gating) | spec "Permissions" |
| SU-06…SU-08 | form tests (5 fields, repeater, selects, no tightening); JS test (supplyInputRefs always, comment omitted when empty, enums) | spec slice |
| SU-09 | JS test (showConfirm, no native dialog) | spec slice (native dialog fails the test) |
| SU-10, SU-11 | details.js RELOAD_CODES; JS tests | spec (duplicate; Draft direct POST) |
| SU-12 | JS test (bodies carry no status; UI never changes status) | spec slice (status stays InReview) |
| SU-13 | JS test (closed flag, workspace hidden, late responses ignored) | spec "unknown ID" |
| SU-14 | JS test (intent reuse, block on key reuse) | BLOCKED (DN-01) |
| SU-15, SU-16 | controller tests (503 mapping, timeout unresolved); JS failure handling | spec (422); NV-06 (503) |
| SU-17 | resx parity 7/7, no English in zh/ar/ru, LTR bdi | spec "Localization" |
| SU-18 | controller route-table tests; gateway fragment explicit paths | BLOCKED |
| SU-19 | SOURCE-MANIFEST + archive hash | Q84b binding record |
| SU-20 | — | BLOCKED (PRES-183-04) |
| SU-SCR-01…09 | route/JS tests (no list, edit, delete, status, bulk, import/export, colvis, saved view, QuickView, DEMAND/Workflow/Capacity call) | SU-SCR-09 verifier record in Q84b |

## Build dependency note (for Q84b; same pattern as Claims F3)

The overlay compiles only in a composed tree. Order: HEAD archive `4a8d4d4b` → BC-SOURCE `ebd5d80c…` → **A12 360 overlay
`7b6a0d1a…314d`** → **accepted S&OP source `8fa00d40…b745`** (reconcile SupplyChain `Program.cs` `a2a216be…` and Api `.csproj`)
→ Auth 22 `f50350b8…` → this lane's `overlay/frontend/**` and `overlay/services/**` → for routing and the nav guard in the
environment only, the `_shared-integration/` items on the environment copy. Pieces needed, none in the common checkout:
`Diten.Web.Security.JsonAdapterEndpointAttribute` and the JSON challenge branch in frontend `Program.cs` (A12); SupplyChain
registration foundation — `IModuleManifestProvider`, `ModuleRegistrationHostedService`, the Api `.csproj` reference to
`Diten.BuildingBlocks.ModuleRegistration.Abstractions` (A12; the S&OP source lacks the reference); `SandopPermissions` and the
S&OP backend (S&OP source only); `Nav.Domain.SUPPLYCHAINEXECUTION` (A12).

## FINDINGS

- **F1 — Gateway GET on the collection.** Pack §23.10 lists "GET+POST `/api/supply-chain/sandop-plans`", but no bound operation
  uses GET there (the contract has no list operation). The fragment keeps GET as listed; the integration owner confirms or drops it.
- **F2 — Build dependency.** The overlay builds only on the A12 360 overlay plus the S&OP isolated source (note above).
- **F3 — Port 5061 in the common checkout.** `gateway/Diten.ApiGateway/ocelot.json` in the checkout routes `/api/crm/*` to
  `localhost:5061`; the A12 overlay maps 5061 to Supply Chain only. The S&OP fragment follows the A12 (integrated-target) shape;
  the integration owner reconciles. Not changed here.
- **F4 — Replay is not distinguishable.** A replayed 201 returns the stored receipt body without a marker (`SandopRepository.cs:20`),
  so the UI shows the same "completed" message for first and replayed 201 and reloads from the server (meets §23.8; A7).
- **F5 — No 415 code.** The contract publishes no UNSUPPORTED_MEDIA_TYPE; a non-JSON POST is answered by the adapter with 400
  `INVALID_REQUEST` (A2).
- **F6 — Key echo vs UI-SCOPE key list.** Seven keys from UI-SCOPE-0190 §7 would carry their own name as English value
  (Status, Snapshots, Snapshot, Source, Role, Decision, Comment). Their values are "Plan Status", "Input Snapshots", "Input
  Snapshot", "Source System", "Sign-off Role", "Sign-off Decision", "Decision Comment" (A3).
- Carried, not decided here (pack §23.13): F190-LIST, F190-DEMAND (free-text references), F190-PIN (A1), F190-ENT, decided-by shown
  as UUID, verifier tension (SU-SCR-09), G-TARGET, DN-01, PNG, nav codes, DCP-009 §21.1 exclusion, G-ICONMAP.

## ASSUMPTIONS

- **A1** Built against the canonical 3.0.0 YAML `5213b535…`; the six operations are identical in 2.0.0 (pack §23.3). F190-PIN stays open; the Q84b dispatch names the version.
- **A2** The adapter originates only published codes: non-contract 5xx → 503 `DEPENDENCY_UNAVAILABLE` (read) / `COMMIT_RESULT_UNRESOLVED` (mutation); transport timeout or failure on a mutation → `COMMIT_RESULT_UNRESOLVED`; non-JSON body or missing key → 400 `INVALID_REQUEST`; bad trace → 400 `INVALID_CORRELATION_ID`.
- **A3** Resx keys follow UI-SCOPE-0190 §7 including dotted enum keys (`Status.Draft`, `Role.Finance`, `Decision.Approved`); the JSON bridge drops the dot (`StatusDraft`). Extra keys cover help, empty, error and confirmation texts (44 index keys, 87 details keys).
- **A4** Horizon dates use `<input type="date">`, which yields the schema's `yyyy-MM-dd`; `sourceCapturedAt` is LTR text, prefilled with the current UTC ISO time when empty.
- **A5** The capture form prefills demand plan ID/version from the plan when empty; values stay editable and are sent as shown.
- **A6** Timestamps are shown as wire text (LTR), not reformatted.
- **A7** A replayed 201 is shown as completed (F4).
- **A8** `X-Tenant-Id`/`X-Legal-Entity-Id` are set server-side from the caller's claims (same resolution as the Claims/Shipment adapters), because the backend requires both equal to the JWT claims.
- **A9** The Playwright accept selector for the shared confirmation is assumed `.swal2-confirm`; Q84b confirms it.
- **A10** Nav key values in 7 languages are proposals; the pack supplies none (l10n review).
- **A11** `SANDOP_PLANS` keeps PageType "List" as the pack states, although the page has no table.
- **A12** Static checks ran in the VM workspace (`/tmp/q84`), not on the Mac; `verify_datatable_page.py` is left to Q84b (record-only).

## Applying the overlay (Q84b, isolated environment only)

1. Compose the tree (build dependency note); verify `SHA256SUMS` and every `SOURCE-MANIFEST.tsv` hash.
2. Copy `overlay/frontend/**` and `overlay/services/**` to the same repo-relative paths (new files only).
3. Apply `_shared-integration/` items to the environment copy only; the integration owner applies them to the real shared files at final integration (SR-D4).
4. `dotnet build` / `dotnet test` (frontend + SupplyChain), static checks again, `verify_datatable_page.py` (record), then `runtime-scenarios/`.

## To-do

1. CT: review the draft; confirm F1 (gateway GET) and A1 (contract version).
2. Q84b (local Mac): compose, build, test, runtime; record evidence.
3. Integration owner (later): `_shared-integration/` items, port reconciliation (F3), DCP-009 §21.1 patch.

Hand-off: 2026-09-26T18:45+03:00 — Q84 writer hand-off (AL S&OP UI draft, chat lane): DRAFT, not writer-complete, uncommitted; 56 files (47 overlay + archive + 5 records + 2 runtime-scenarios + SHA256SUMS), SHA256SUMS 55/55, static checks 89 PASS / 0 FAIL; pack 2bdd533f… unchanged. The ledger is owned by Q83; CT records this hand-off.
