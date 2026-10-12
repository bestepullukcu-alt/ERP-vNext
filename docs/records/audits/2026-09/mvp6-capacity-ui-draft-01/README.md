# Q88 — MOD-0192 Capacity tenant UI: DRAFT code overlay (chat lane)

🤖 Applying knowledge of @frontend-ui-ux + @l10n-agent + @integration-agent (integration items as overlay only).
Chat lane on the linked Mac folder (Linux VM bridge). Repo `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`.
Start 2026-09-26T19:13:35+03:00 (Europe/Istanbul). Git read-only (`GIT_OPTIONAL_LOCKS=0`); no commit (Q03a). **Status: DRAFT — not
built, not tested, not run, not writer-complete.** The only writes are this folder.

Authority: pack §23/§24 approved (`mvp6-sop-capacity-ui-pack-signoff-owner-decision-01.md`); draft overlays in chat lanes
(`mvp6-draft-overlays-owner-decision-01.md` `05033624…a67c`); CT verdict F6 (offcanvas + showConfirm) / F8 (root never reaches the
browser) (`mvp6-ct-verdicts-q64a-q77a-q78-2026-09-26.md` `356cd273…05f3`).

## MOD-0192 pack hash read

Read at start: **`7c3678bc5cbd646c8c2b6f9b9e99089cba9e03b34aed4e74359d52078975171f`** (working copy `/tmp/q88/pack.md`). During the run
the parallel lane (Q86) changed it to **`f6b4d0f3ae971666ea630e23b1edae501c226503aad8ed2b7a7ca804e314e445`**; `diff` shows exactly one
changed line, pack line 263 in §22 (the "It is a proposal until the owner decision … is recorded" sentence → "is recorded in the
`Approved:` record above"). §23/§24 are byte-identical in both, so the specification used here is the same. Both hashes are
accepted by the dispatch; no other hash was seen.

## Inputs (read-only, sha256)

| Input | sha256 |
|---|---|
| Pack `execution/domains/supply-chain-execution/module-packs/MOD-0192-capacity-planning.md` (§23, §24) | `7c3678bc…171f` at read; `f6b4d0f3…e445` at end (see above) |
| Contract `docs/analysis/contracts/sandop-capacity.openapi.yaml` (3.0.0; six Capacity operations of §23.3 only) | `5213b5353267ad4c25d150975d6f38422bced72f678c02271b0cccf6e768adab` |
| Semantics annex `docs/analysis/contracts/sandop-capacity-semantics-v3.0.0.md` | `f9d9d5553d70e1c54af3e03924600f4c8f426de50355ae861a4d3d5cef21ee64` |
| Scope `docs/roadmap/plans/mvp6-ui-scope-190-192-01/UI-SCOPE-0192.md` / `PH15-UI-192.md` / `SHA256SUMS` | `656ef7701798dd7cd7a8cd2367a19f8d16f1bc645cdecd33a21db66c9c9c03b3` / `5dee65d1d07b81974de600991ae8ed110ae2c0a1a6edf2eb093cc2dd55af4116` / `83c6b7e96331e246cacc9f75890b8282c32fb6bda066b2d504d66e22e0f1e985` |
| Accepted Capacity backend (isolated source, pack §22/§24) `docs/records/audits/2026-09/mvp6-bc-successor-exec-02/BC-SOURCE.tar.gz` | `ebd5d80ca00541235868b94ed9917a53fb63be6e15aa78d0450fa90f33737064` |
| — Api `Features/CapacityPlans/CapacityPlansController.cs` / `CapacityContextMiddleware.cs` / `CapacityContractError.cs` | `c710d361…4b7b` / `3c7bb5e3…be8e` / `c55ad161…cd7e` |
| — Infrastructure `CapacityPermissions.cs`; Application `CapacityPlanModels.cs` | `7dd343d0…1c3b`; `e66712cc…0984` |
| — Validators `CreateCapacityPlanValidator.cs` / `CreateCapacityScenarioValidator.cs` / `EvaluateCapacityScenarioValidator.cs` | `c2cdc985…8fd6` / `1b88af49…6bb8` / `3f6f6b7f…32b3` |
| — Domain `CapacityScenario.cs` / `CapacityEvaluation.cs`; Api `Program.cs` | `a2b5acea…6ae5` / `4020898d…0c85`; `50c48a2b…30f0` |
| Precedent `docs/records/audits/2026-09/mvp6-sop-ui-draft-01/` (closest): `SHA256SUMS` / `README.md` / archive | `0aafc3402511f95fd8638a4325bb032dfbf2ca4bafb5bf0b3f3d05c03f568093` / `08010992…c68d` / `fcf52d82…5e30` |
| Precedent `docs/records/audits/2026-09/mvp6-claims-ui-draft-01/SHA256SUMS` | `80da27c94c6e8a124e4636e679f95b39ef7e67ad5ea489a097e84d801df608d3` |
| CT verdict `docs/records/audits/2026-09/mvp6-ct-verdicts-q64a-q77a-q78-2026-09-26.md` (F6, F8) | `356cd273ef994c9ac46767c8767b8d209ecb8602d727b2f477fd6df7b3c805f3` |
| A12 360 overlay (conventions + build dependency) `docs/records/audits/2026-09/mvp6-shipment-a12-safe404-rework-01/successor-source.tar.gz` | `7b6a0d1abea649d920040a039972c5d4928825e9cf2cc185487b1fe5f0f3314d` |
| `AGENTS.md`; `.antigravity/scripts/verify_module_id.py`; Golden reference slim (read through the S&OP precedent's conventions) | `51d92c75b761e5c7195eda102d08f2b7f472b979d9940bdf21ae72cb7cab7fcb`; `3d20892853a526f14255ed2d2ff7afbf9a2fcce43e45f229e3a8947385792b63`; — |

Backend citations (BC-SOURCE, file:line): routes and permission attributes `CapacityPlansController.cs:11-32`, wire envelope
`CapacityPlansController.cs:33-34`; header/scope gate `CapacityContextMiddleware.cs:30-64` — scope from token claims only (46-49),
correlation non-nil "D" (11-12, 34-38, 50), one Idempotency-Key + JSON on POST (51-57), non-nil route IDs (58-59), `tenantId`/`legalEntityId`
query keys rejected (60-61); error messages `CapacityContractError.cs:39-55`; keys `CapacityPermissions.cs:4-7`; request records
`CapacityPlanModels.cs:4-27`, response records 28-31; validators `CreateCapacityPlanValidator.cs:8-13`,
`CreateCapacityScenarioValidator.cs:8-11`, `EvaluateCapacityScenarioValidator.cs:8-11`; row shapes `CapacityScenario.cs:3-4`,
`CapacityEvaluation.cs:3`; receipt replay `CapacityRepository.cs:21-31` (Persistence) with `CapacityMutation.Replay` (`ICapacityRepository.cs:2`).

## What was produced

| Part | Content |
|---|---|
| `overlay/` module files (32 = §23.10) | adapter controller (2 view routes + 6 adapters, nothing else), view models, 2 marker classes, 7 views (entry page without list; plan workspace; 3 offcanvases; 2 L10n bridges), 4 JS files, 14 resx (2 markers × 7 languages incl. ar RTL; 49 index keys, 106 details keys), 3 test files |
| `overlay/services/…` (2 = §24) | `CapacityPlanningManifestProvider` + M-01…M-08 tests |
| `overlay/_shared-integration/` (13) | Program.cs line (spec), gateway fragment (6 explicit routes on 5061, OPTIONS, no collection GET), frontend note, icon/SortOrder proposal (`bx-bar-chart-alt-2`, 450), platform checklist, nav keys × 7 — **nothing applied** |
| `runtime-scenarios/` | Playwright spec + README for Q88b (never run) |
| Records | `FILE-PLAN.tsv` (`8569b7a2606a2c0ff0a753f30ef6f8fe3445b3639acdeaf93d1c79985b1158f4`, 30 section + 32 owned + 2 SR-D4 rows), `SOURCE-MANIFEST.tsv` (`ae3889d474fc3e73a6586d9539b3566de3154282cae7ce2a8582974ee3fb8757`, 47 rows), `STATIC-CHECKS.txt` (`e022bad3702698f18e8c5b31cc26910abdeebf4a2f37f618df0732b32efe73c4`, **127 PASS / 0 FAIL**), `NOT-VERIFIED.md` (`332aad35…ccd8`), archive `capacity-ui-draft-overlay.tar.gz` (`dd290891d67eb1ccb6943acd25f855902b86b00af6384d0ec43525e1ee162818`, 47 files, deterministic, byte-identical to `overlay/`), `SHA256SUMS` |

Behaviour in short: no list request anywhere (F192-LIST); "Open plan by ID" validates the UUID (non-nil) and only navigates;
create plan sends the 7 schema fields exactly as typed (progress 0/7 on first open, nothing prefilled) and navigates to the
returned ID; the workspace shows the plan summary, lets the user open a scenario by ID or create one (both arrays always sent,
`[]` when empty; the capacity delta stays the typed JSON string), and run an evaluation (Finite/Infinite, ≥ 1 resource in the
order shown); after every 201/202 the created resource is read back with one GET, never shown from the reply body; the
evaluation panel changes only on a manual **Refresh** click (exactly one `getCapacityEvaluation` per click, disabled while
pending, hidden for Completed/Failed, no timer anywhere); bottlenecks appear in a bounded client-side DataTables v2 table when
the evaluation is Completed, decimals shown as the wire strings, LTR; every POST carries a per-intent Idempotency-Key reused only
with the same target and identical body text; 404 is safe-not-found per resource (plan → whole workspace removed; scenario or
evaluation → that panel's content removed) and late responses are ignored; the 16 published codes are localized; tenant and
legal entity never leave the server (the adapter sends no scope header at all — the backend reads the signed token).

## Acceptance coverage (pack §23.11)

| Row | Covered by (static, this lane) | Runtime (Q88b) |
|---|---|---|
| CP-VS1 | controller + form + JS tests (bodies, keys, decimal string, Refresh) | spec "Early vertical slice" (BLOCKED: target, gateway, permission seed, fixture seed, executor) |
| CP-01 | controller tests (token server-side, no scope header, browser scope headers dropped); JS tests (no :5000/:5061, no token/scope) | spec "Entry page" (BLOCKED: gateway) |
| CP-02 | JS test (single fetch on the entry page); index.js openPlan; details.js openScenarioById (malformed/nil blocked) | spec "Entry page", "unknown scenario ID" |
| CP-03 | form test (empty/skeleton/error/content per panel, no "Loading") | NOT-VERIFIED NV-06 |
| CP-04 | form test (UAS-001 gate first) | spec "Permissions" |
| CP-05 | controller tests (403, zero gateway calls, per key); JS test (key + loaded-state gating) | spec "Permissions" |
| CP-06…CP-08 | form tests (7 fields, repeaters, select, no tightening); JS tests (arrays always sent, ≥ 1 resource, enums) | spec slice |
| CP-09 | form test (text + inputmode=decimal, no type=number); JS test (no Number/parseFloat; pattern mirrored); textContent/`bdi dir=ltr` rendering | spec slice + "invalid decimal" |
| CP-10 | JS test (no setTimeout/setInterval/EventSource/WebSocket; canRefreshNow; one loadEvaluation per click) | spec slice (10 s idle, one GET per click) |
| CP-11 | JS test (`EVALUATION_ALREADY_ACTIVE` blocks; Evaluate withheld while Accepted/Running) | spec direct POST (timing-sensitive, F9) |
| CP-12 | details.js/index.js failure handling (inputs kept; state conflict reloads the plan) | spec duplicate plan |
| CP-13 | JS test (per-resource 404, generation guards, workspace hidden); controller test (code-less 404 → resource code) | spec unknown plan / scenario (foreign-scope and soft-deleted: NV-06) |
| CP-14 | JS test (intent reuse by target + body, block on key reuse) | BLOCKED (DN-01) |
| CP-15, CP-16 | controller tests (503 mapping, mutation timeout unresolved, unpublished code never relayed); JS failure handling | NV-06 |
| CP-17 | resx parity 7/7, ar Arabic script, no English in zh/ar/ru, LTR `bdi` | spec "Localization" |
| CP-18 | controller route-table tests (no list route); gateway fragment explicit paths | BLOCKED |
| CP-19 | SOURCE-MANIFEST + archive hash | Q88b binding record |
| CP-20 | — | BLOCKED (PRES-183-04) |
| CP-SCR-01…10 | route/JS tests and STATIC-CHECKS mirror rows (no list, edit, delete, approve/archive, cancel, chart/optimizer, lookup, import/export, colvis, saved view, quick view, polling) | CP-SCR-10 verifier record in Q88b (NV-08) |

## Build note for Q88b (exact pieces; same pattern as S&OP F2 / Claims F3)

The overlay compiles only in a composed tree, in this order:
1. HEAD archive `4a8d4d4b339528a88e6220fb8402e5a2c771136c` (common checkout baseline).
2. **BC-SOURCE** `docs/records/audits/2026-09/mvp6-bc-successor-exec-02/BC-SOURCE.tar.gz` `ebd5d80c…7064` — the accepted Capacity
   backend (43 paths + composition, executor, `CapacityPermissions`, fixtures). Its Api `.csproj` and `Program.cs` (`50c48a2b…30f0`)
   carry **no** ModuleRegistration reference or registration foundation (checked by grep).
3. **A12 360 overlay** `docs/records/audits/2026-09/mvp6-shipment-a12-safe404-rework-01/successor-source.tar.gz` `7b6a0d1a…314d` —
   `Diten.Web.Security.JsonAdapterEndpointAttribute`, the JSON challenge branch in frontend `Program.cs`, the SupplyChain
   registration foundation (`IModuleManifestProvider`, `ModuleRegistrationHostedService`, `PlatformRegistrationOptions`, the Api
   `.csproj` reference to `Diten.BuildingBlocks.ModuleRegistration.Abstractions`), `Nav.Domain.SUPPLYCHAINEXECUTION`, ocelot with
   5061 = Supply Chain. **Reconcile** SupplyChain `Program.cs` and the Api `.csproj` between BC-SOURCE and A12 (both touch them).
4. Auth 22 `f50350b8…` (real-Auth tokens with `tenant_id`, `legal_entity_id`, `sub`, `permission` claims — the Capacity middleware
   requires each exactly once, `CapacityContextMiddleware.cs:18-29, 46-49`).
5. This lane's `overlay/frontend/**` and `overlay/services/**` (new files only).
6. For routing and the nav guard in the environment only: `_shared-integration/` items on the environment copy (6 gateway routes,
   the `AddSingleton` line, the 2 nav keys × 7).
Not needed: the S&OP isolated source (`8fa00d40…b745`) — Capacity has no dependency on it.

## FINDINGS

- **F-Q79-05 — Scenario/evaluation IDs lost on reload (recorded, not solved).** Pack §23.4 defines only two view routes
  (`/SupplyChain/CapacityPlans` and `/Details/{capacityPlanId:guid}`) and no scenario or evaluation route segment or query key;
  §23.13 ("Session memory") records the gap as a consequence of F192-LIST. Putting `scenarioId`/`evaluationId` into the URL would
  add a route or query the pack does not define (a hard-stop class change), and browser storage is excluded. So the page keeps
  the IDs in memory only; after a reload the plan reopens from its route, the scenario can be reopened by its (copyable) ID, and
  an evaluation **cannot** be reopened because the scope has no "open evaluation by ID" control (the adapter route exists; adding
  the control needs a scope row). Options for CT/owner: (a) a §23.4 amendment adding `?scenarioId=&evaluationId=` to the Details
  route; (b) an "open evaluation by ID" field in the evaluation panel (no new endpoint); (c) accept as is until F192-LIST lists.
- **F2 — Build dependency.** The overlay builds only on BC-SOURCE + the A12 360 overlay (build note above); BC-SOURCE lacks the
  registration foundation and the Abstractions reference.
- **F3 — Port 5061 in the common checkout** (carried from S&OP F3). The checkout `ocelot.json` routes `/api/crm/*` to 5061; the
  fragment follows the A12 (integrated-target) shape. Not changed here.
- **F4 — Replay is not distinguishable on the wire.** `CapacityMutation.Replay` (`ICapacityRepository.cs:2`) is not exposed; a replayed
  201/202 looks like the first one, so the UI shows "completed" and reads the resource back (meets §23.8; A7).
- **F5 — Ocelot placeholder matching.** Upstream templates ending in a placeholder (`/{capacityPlanId}`) may match deeper paths
  depending on Ocelot version and route priority; the six explicit routes use the same verbs and downstream paths, so a mismatch
  would still reach the right backend route, but CP-18 must be checked on the target (NV-06).
- **F6 — `INVALID_CORRELATION_ID` wire message.** The Capacity backend returns "Invalid request" for it (`CapacityContractError.cs:53-54`
  default branch); the adapter mirrors that text (the S&OP adapter used a different one). The page shows localized text only.
- **F7 — No 415 code.** A non-JSON POST is answered 400 `INVALID_REQUEST`, as the backend does (`CapacityContextMiddleware.cs:56`).
- **F8 — Key echo vs the UI-SCOPE-0192 §7 key list.** Keys whose English value would equal the key name carry a descriptive English
  value instead: `Status` "Plan Status", `Source` "Source System", `Scenarios` "Capacity Scenario", `Adjustments` "Capacity
  Adjustments", `Period` "Planning Period", `Evaluate` "Run Evaluation", `Evaluation` "Capacity Evaluation", `Refresh` "Refresh
  Status", `Bottlenecks` "Capacity Bottlenecks", `Shortfall` "Capacity Shortfall", `NoneListed` "None" (A3).
- **F9 — CP-11 is timing-sensitive.** The second-submit scenario needs the first evaluation still Accepted/Running when the second
  POST arrives; a fast executor makes the runtime row flaky. Q88b records the executor timing.
- **F10 — "Permission denied" in the controller.** The pack grep (§23.12) for "Permission denied"/"Forbidden" text hits the adapter's
  `DefaultMessage` — that is the backend's wire text for API clients (`CapacityContractError.cs:53`), never rendered. Views, JS and
  resx are clean (STATIC-CHECKS). Q88b scopes the grep to UI files or records this line.
- Carried, not decided here (pack §23.13): F192-LIST, F192-DEMAND (free-text references), F192-POLL, verifier tension (CP-SCR-10),
  G-TARGET, DN-01, PNG, nav codes, DCP-009 §21.1 exclusion, G-ICONMAP.

## ASSUMPTIONS

- **A1** Built against the canonical 3.0.0 YAML `5213b535…` bound by §23.3; only the six Capacity operations were read.
- **A2** Adapter policy: only published codes originate (non-contract 5xx → 503 `DEPENDENCY_UNAVAILABLE` for reads /
  `COMMIT_RESULT_UNRESOLVED` for mutations; transport timeout/failure likewise; code-less 404 → the resource's `UNKNOWN_CAPACITY_*`;
  any other code-less or unpublished failure → 400 `INVALID_REQUEST`); bad or nil trace → 400 `INVALID_CORRELATION_ID`; nil route ID
  → 400 `INVALID_REQUEST`; **no tenant/legal-entity header is sent** because the Capacity backend takes scope from the signed token
  only and ignores such headers (unlike S&OP, whose middleware compares them).
- **A3** Resx keys follow UI-SCOPE-0192 §7 with dotted enum keys (`PlanStatus.Draft`, `ScenarioStatus.Evaluated`,
  `EvaluationStatus.Running`, `EvaluationMode.Finite`); the bridge drops the dot. Extra keys cover help, empty, error, success and
  label texts (`ScenarioStatusLabel`, `EvaluationStatusLabel`). Translations for keys shared with S&OP reuse the S&OP draft values.
- **A4** Horizon dates use `<input type="date">` (schema `yyyy-MM-dd`).
- **A5** `sourceCapturedAt` is an LTR text field for an ISO-8601 UTC date-time (help text shows the form), sent exactly as typed and
  never prefilled, so the first-open progress is 0/7; the client mirrors "non-empty" only and the server validates the date-time.
- **A6** Timestamps are shown as wire text (LTR), not reformatted.
- **A7** After 201/202 the created resource is read back with one GET; a replayed 201/202 is shown as completed (F4).
- **A8** The Evaluate form opens with one empty resource row; mode starts unselected.
- **A9** Constraint references and adjustments of the loaded scenario are plain read-only tables built with `textContent`; only
  the bottleneck table uses the bounded DataTables v2 profile (§23.5 names that one table).
- **A10** Bottleneck decimal columns are not sortable (wire strings are never parsed); the table is shown for Completed only, a
  Failed evaluation shows its status without a table.
- **A11** Manifest action sort orders (pack silent): `CREATE` 10, `CREATE_SCENARIO` 10, `EVALUATE` 20 (S&OP precedent).
- **A12** Nav key values in 7 languages are proposals (the pack supplies none; l10n review).
- **A13** Evaluate stays withheld for a scenario while an evaluation this page submitted for it is not shown as Completed/Failed,
  and for the rest of the page session after `EVALUATION_ALREADY_ACTIVE` (the active evaluation's ID is unknown to the page).
- **A14** Opening a scenario that has an evaluation submitted in this page session loads that evaluation once (one GET, no polling).
- **A15** On `CAPACITY_PLAN_STATE_CONFLICT` the message stays in the form and the plan summary reloads; "Create scenario" stays
  visible because the server decides the state (§23.7).
- **A16** Provenance shows demand plan ID/version, checksum and captured-at; `sourceContract`/`sourceContractVersion` are not shown
  (no key in UI-SCOPE-0192 §7).
- **A17** Static checks ran in the VM workspace (`/tmp/q88`), not on the Mac; `verify_datatable_page.py` is left to Q88b (record-only).

## Applying the overlay (Q88b, isolated environment only)

1. Compose the tree (build note); verify `SHA256SUMS` and every `SOURCE-MANIFEST.tsv` hash.
2. Copy `overlay/frontend/**` and `overlay/services/**` to the same repo-relative paths (new files only).
3. Apply `_shared-integration/` items to the environment copy only; the integration owner applies them to the real shared files at final integration (SR-D4).
4. `dotnet build` / `dotnet test` (frontend + SupplyChain), static checks again, `verify_datatable_page.py` (record), then `runtime-scenarios/`.

## To-do

1. CT: review the draft; decide F-Q79-05 (a / b / c).
2. Q88b (local Mac): compose, build, test, runtime; record evidence.
3. Integration owner (later): `_shared-integration/` items, port reconciliation (F3), Ocelot check (F5), DCP-009 §21.1 patch.

Hand-off: 2026-09-26T19:40+03:00 — Q88 writer hand-off (MOD-0192 Capacity UI draft, chat lane): DRAFT, not writer-complete, uncommitted; 56 files (47 overlay + archive + 5 records + 2 runtime-scenarios + SHA256SUMS), SHA256SUMS 55/55, static checks 127 PASS / 0 FAIL; MOD-0192 read at 7c3678bc…171f, f6b4d0f3…e445 at end (Q86 §22 line only). The ledger is owned by Q86; CT records this hand-off.
