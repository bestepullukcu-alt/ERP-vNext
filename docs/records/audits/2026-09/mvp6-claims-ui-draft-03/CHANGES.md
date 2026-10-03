# MOD-0187 Claims UI DRAFT — v2 → v3 changes (Q64e)

- **v2:** `mvp6-claims-ui-draft-02/claims-ui-draft-overlay-v2.tar.gz` `1a57c879d1d0970f703b089704a304af5981b309e0be787ac659d3f28be711a0`
  (39 files).
- **v3:** `claims-ui-draft-overlay-v3.tar.gz` `66e72bf6af1ebb6fffbcedf5f8b7934cd9a5a0f95cb1abf558b92fa3b500ce0c` (39 files).
- **Comparison:** file by file in `~/mvp6-env/q64e/diff-final-130134/` (`v2-v3-diff.tsv`).
- **Totals:** 0 added · **2 modified** · 0 removed · 37 unchanged.
- **Source of the defects:** Q64d runtime evidence, `mvp6-q64d-claims-runtime-01/` (README §6, ACCEPTANCE CU-05 / CU-25).

> **Intermediate candidate, not delivered — v3-rc1** (archive `8ed8abd7…`, index.js `95f79684…`). rc1 carried only the CSS half of the D1 fix,
> exactly as Q64d described it. Its runtime run (`runtime-kit/`, sealed) still failed CU-05. The skeleton timeline in
> `runtime-kit/browser/diag-v3-a1.json` shows why: the skeleton became visible at 811 ms, then the **shared `DtDefaults` drawCallback faded
> it out** (`dt-defaults.js:589`, 200 ms) on DataTables' empty first draw, before the data arrived (3586 ms). That is a second cause Q64d
> did not see. The final v3 adds E-02.

| # | File (archive path) | Change | Reason |
|---|---|---|---|
| E-01 | `overlay/frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Claims/index.js` — `setListState` | Besides toggling `d-none`, the skeleton gets `style.display = 'block'` in the skeleton state and `'none'` in every other state. | **Q64d-D1, cause 1 (CU-05).** The shared `backbone-custom.css:353` rule `.backbone-skeleton { display: none }` beat the `d-none` toggle. |
| E-02 | same file — `listState`, `setListState`, `drawCallback` | `setListState` records the state, stops any jQuery animation on the skeleton (`jQuery(skeleton).stop(true)`), clears its inline opacity and then owns the display. The module `drawCallback` re-asserts the skeleton while the state is still `skeleton`. | **Q64d-D1, cause 2 (found in Q64e rc1).** The shared `DtDefaults` drawCallback fades the skeleton out on DataTables' first draw, before any data exists. Shared files are not changed (protected); the module keeps its own first-load state. After data (`table`) or an error (`error`), nothing is re-asserted. |
| E-03 | same file — `focusTransitionField`, `openTransition`, `bindEvents` | `openTransition` focuses `#transitionTarget` right after `show()`, and again on `shown.bs.offcanvas` for the transition surface. | **Q64d-D2 (CU-25).** Opened from a row dropdown item, keyboard focus was not inside the surface. Bootstrap 5.3.3 binds the offcanvas Escape handler on the element and activates its focus trap only after the slide-in, then focuses the container. The first focus makes Escape work during the slide-in; the second puts focus back on the first field. |
| E-04 | same file — `createOpener`, `restoreCreateOpener`, `openCreate`, `hidden.bs.offcanvas` | `openCreate` remembers the opener (the focused element, or `.add-new`). When the create offcanvas is hidden, focus returns to it (`preventScroll`). | **Q64d-D4 (CU-25).** After the create surface closed, focus did not return to Add. |
| E-05 | `overlay/frontend/Diten.Web.Tests/JavaScript/ClaimIndexBehaviorTests.cs` | Three contract tests: `Skeleton_state_overrides_the_shared_display_none` (covers E-01 and E-02), `Transition_surface_takes_keyboard_focus_when_opened`, `Create_surface_returns_focus_to_its_opener_on_close`. | Sabotage proof (`logs/test/sabotage2-*.trx`): **v2 → 3 FAIL · rc1 → 1 FAIL (D1) · final → 3 PASS**. |

## Not changed

- **Q64d-D3 (Enter on the focused Add button does not open create; Space does):** belongs to the **template owner**. Golden slim wires Add
  the same way (`GoldenReferenceSlim/index.js:955`). Not changed here, as the WP requires, and still listed.
- **No other file changed:** no view, resx, controller, model, provider, `_shared-integration` item or runtime scenario. No shared file
  (`dt-defaults.js`, `backbone-custom.css`) changed either.
- **Scope limits:** no endpoint, permission or route was added, and `supplychain.carriers.read` was not added (Q114).
