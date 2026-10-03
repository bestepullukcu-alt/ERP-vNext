# Q160 — Returns UI draft overlay v2 (MOD-0186): fixes for checklist r2 FAILs UI-PM-01/03/05/06/07/09/10

| Field | Value |
|---|---|
| WP / prompt | Q160 / v1 (T1 v1, SOP v2.5 §36.2) · CT-QUEUE `mvp6-process-pilot-01/CT-QUEUE.tsv:236` (READY at preflight) · frontend-ui-ux |
| Lane | LANE 4, Cowork Linux VM (`uname -s` = `Linux`); archive-only DEV draft; no build/test |
| Branch / HEAD | `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c` (unchanged) |
| Time | 2026-09-28 13:07 → 13:13 +03:00 (VM clock UTC 10:07 → 10:13) |
| Base (v1) | `mvp6-returns-ui-draft-01/` SHA256SUMS `a80047b50696c3a88a4512b1405ee0c0d9e3458ef09870d4f15c3862ef755905` (all OK); `returns-ui-draft-overlay.tar.gz` `35dd1489d293e881894ae63590f394543c33c886dfe4a969602c062523f1a014` |
| Inputs verified | Q164 `a9024ac5…` 5/5 · Q165 `c3c56ec6…` 2/2 · Claims v4 `mvp6-claims-ui-draft-04/` `cc3f4c7f…` 4/4 (archive `2b34741af91dc640862cd6e460809e860040e21747a66849aa5b61bfa099293f`) · PH15-UI-186 `mvp6-returns-ui-ph15-owner-decision-01.md` `e142b74e00d8a8b0ea7758923b0a0a828a21223aa89df05f845217faafc61929` |
| Checklist | UI-PM-01…12 r2 (Q164 patch postimage `90247ddc…`, now live in `.antigravity/agents/frontend-ui-ux.md`) |
| Output | `returns-ui-draft-overlay-v2.tar.gz` `8862b46e09a9da5283876cf5f0e9d34f1eb9ea11745c4436f2755fcab682ecfd` (36 files, same entry list as v1; 8 files changed, 28 byte-identical) |

Line numbers: v1 = `returns-ui-draft-overlay.tar.gz`, v2 = `returns-ui-draft-overlay-v2.tar.gz`. `js/…` = `overlay/frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Returns/index.js`; `Views/…` = `overlay/frontend/Diten.Web/Views/SupplyChain/Returns/`. Pattern source: Claims v4 `js/SupplyChain/Claims/index.js` (lines given per fix).

## UI-PM-01 — partials set no Layout (D-02)

- v1: `Layout = "_LayoutTenantShell";` at line 7 of `_CreateEditOffcanvas`, `_DataTable`, `_DetailsQuickView`, `_Filter`, `_IndexL10n.cshtml`.
- v2: that line and its "every .cshtml states the tenant shell" comment replaced by the Claims v4 comment "Partial view: no Layout…" (line 6 of each partial). `Index.cshtml:10` keeps the only Layout. Claims v4 pattern: `Views/SupplyChain/Claims/_*.cshtml:6-7`.
- Test: `ReturnFormContractTests.cs:179-186` — partials must not contain `Layout = `, page views must state the tenant shell (Claims v4 `ClaimFormContractTests.cs:169-174`). The v1 test asserted the Layout in every view and would fail on v2.

## UI-PM-03 — the ajax option is not an async function (D-01)

- v1: `ajax: loadReturns` (`js/…:920`) with `const loadReturns = async` (`:277`) → a Promise; `ajax.reload()` calls `.abort()` on it and throws.
- v2: `ajax: (data, callback) => { void loadReturns(data, callback); }` (`js/…:965`, comment `:963-964`). Claims v4 `:972-974`.
- Test: `ReturnIndexBehaviorTests.Ajax_option_is_not_an_async_function`.

## UI-PM-05 — the skeleton becomes visible through display (D1, CU-05)

- v1: `setListState` only toggled `d-none` on `#skeleton-loader` (`js/…:248`); shared `.backbone-skeleton { display:none }` kept it hidden.
- v2: `setListState` (`js/…:276-287`) sets `skeleton.style.display = 'block' | 'none'` (`:286`) as well as the `d-none` toggle (`:285`). Claims v4 `:271-282`.
- Test: `ReturnIndexBehaviorTests.Skeleton_state_overrides_the_shared_display_none_and_survives_the_first_empty_draw`.

## UI-PM-06 — the first empty draw does not hide the skeleton (D1, CU-05)

- v1: `drawCallback` (`js/…:944-946`) had no guard; DtDefaults fades `#skeleton-loader` on DataTables' empty first draw.
- v2: `let listState = 'skeleton'` (`js/…:105`), set in `setListState` (`:277`); `setListState` stops the shared fade (`:283-284`); `drawCallback` re-asserts the skeleton while the first load is pending: `if (listState === 'skeleton') setListState('skeleton');` (`:991`). Claims v4 `:97`, `:272`, `:278-279`, `:1008`.
- Test: same as UI-PM-05.

## UI-PM-07 — the transition panel takes focus on open (D2, CU-25)

- v1: transition surface opened from the row `dropdown-item js-transition` (`js/…:332` → `:968-969` → `openTransition :600-630`) with no `.focus()` and no `shown.bs.offcanvas` listener (`:1019-1029`).
- v2: `focusTransitionField` (`js/…:172`) focuses `#transitionTarget`; called right after `show()` (`:672`) and on `shown.bs.offcanvas` (`:1078`). Claims v4 `:167`, `:634`, `:1093`.
- Test: `ReturnIndexBehaviorTests.Transition_surface_takes_keyboard_focus_when_opened`.

## UI-PM-09 — the create panel returns focus to its opener (D4; stable opener = Add, OD-Q164-09)

- v1: `openCreate` (`js/…:439-449`) stored no opener; `hidden.bs.offcanvas` (`:1024-1028`) returned no focus.
- v2: `openCreate` stores `createOpener` (the focused element, else `.add-new`; `js/…:487-488`); `hidden.bs.offcanvas` for `offcanvasCreateEdit` calls `restoreCreateOpener()` (`:1073-1076`), which focuses the stored opener if still in the DOM, else the Add button (`:176-181`). Claims v4 `:170-175`, `:451-452`, `:1088-1091`.
- Test: `ReturnIndexBehaviorTests.Create_surface_returns_focus_to_its_opener_on_close`.

## UI-PM-10 — focus returns inside the create panel after a rejected save (D5, Q122-D5, CU-25)

- v1: create save disabled while pending (`js/…:553`); `finally` (`:572-575`) re-enabled Save but left focus on BODY.
- v2: `restoreCreateFocus` (`js/…:183-195`): while the panel is open and not hiding and focus is outside it, focus the first `[aria-invalid="true"]`/`.is-invalid` field, else Save when enabled, else `#returnShipmentId`. Called in the create `finally` right after Save is re-enabled (`:615-616`). Covers every rejected path (400/409/422/5xx/network) because it runs in `finally`; on success the panel is hiding, so it does nothing. Claims v4 `:177-188`, `:570-574`.
- Unchanged: the transition submit (`:734`) is reached only through `window.showConfirm` (`:713`) — exempt; no `restoreCreateFocus` in `submitTransition`.
- Test: `ReturnIndexBehaviorTests.Create_surface_keeps_keyboard_focus_after_a_rejected_submit`.

## Not changed

- Behaviour outside the seven items: no other line of `index.js` changed (9 hunks, all listed above). The error-code map (`const ERROR_KEY`) is byte-identical to v1 (21 codes, PH15-UI-186); controller, view models, resx (7), `Index.cshtml`, `index.l10n.js`, self-registration and `_shared-integration/` are byte-identical.
- No new L10n key, permission, route, endpoint or shared file.

## Static checks run (no build, no test)

- `node --check` on v2 `index.js`: syntax OK.
- The 12 checklist items on v2: 12 PASS, 0 FAIL, 0 N/A (`CHECK-RUN.tsv`).
- The new and changed test assertions were emulated with string/regex matching over v1 and v2 `index.js`: the 5 new contract tests fail on v1 and pass on v2; the v1 contract assertions (forbidden tokens, slices, no native dialog, no hard-coded text) still hold on v2. The C# tests themselves run at Q65b (Mac).

## ASSUMPTIONs

- **A1 (UI-PM-10 order):** the prompt says focus returns "before re-enable"; checklist r2 UI-PM-10 says the `finally` "re-enables it **and then** focuses", and Claims v4 does the same (`:572-573`). v2 follows the checklist and Claims v4 (re-enable, then focus), so Save itself can take focus when it is the right target.
- **A2 (UI-PM-01 vs pack):** pack MOD-0186 `:613` still says "every Returns `.cshtml` states `Layout = "_LayoutTenantShell";`" (and the v1 FILE-PLAN §32.2 row repeats it). The prompt (§17.3 "partials set no Layout"), checklist UI-PM-01 and Claims pack MOD-0187 `:562-563` say partials set none. v2 follows the prompt; the MOD-0186 pack line is not in this WP's allowed paths.
- **A3 (UI-PM-09 opener):** the stored opener is the focused element at open time (the Add button when opened by click), falling back to `.add-new`; this is the Claims v4 pattern. When create is closed as forbidden (403), `.add-new` is disabled (`:494`) and the fallback cannot take focus — same as Claims v4; not in UI-PM-09's scope (no control that stays usable).

## Deviations

- None from the allowed paths. `node --check` is a syntax parse only (no build/test).
- `git status --porcelain` vs preflight (541 lines, 10:07 UTC): this lane added only `?? docs/records/audits/2026-09/mvp6-returns-ui-draft-02/`. Other lanes changed the tree during the run and were not touched or used: ` M .antigravity/agents/frontend-ui-ux.md` and ` M .antigravity/workflows/test.md` with `?? …/mvp6-q170-ui-checklist-apply-01/` (Q170, checklist r2 apply — the live file is the r2 postimage `90247ddc…` this run used), and `?? …/mvp6-sop-ui-draft-02/` (S&OP fix lane). The v1 folder is intact (`a80047b5…`, all OK). No ledger write, no rm, no git write, no `git diff`.
