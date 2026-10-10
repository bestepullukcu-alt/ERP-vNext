# Returns UI draft v3: CHANGES

WP Q188 · Template T1 · LANE 2 (frontend-ui-ux) · 2026-10-01
Base Stack: BASE-STACK v2 `ce8d60ab959ec7018c255ab1152983cfd8e301390f54b3d0ba23961bf2c4d882` (unchanged) · HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c`
Archive-only draft. No tracked repo file was edited.

## Why

- **F-Q65b-2 / OD-F-Q65b-2.** Q65b (`docs/records/audits/2026-09/mvp6-q65b-returns-mac-01/REPORT.md` `60965bd6…`) failed O-3: after a 403 on create, or a 403/404 on a transition, focus went to BODY or to the disabled Add button. v3 sends focus to a stable page title instead, using the same pattern as S&OP v3 (`sop-ui-draft-overlay-v3.tar.gz` `716e7c4c…`, `#sandopPlansTitle`).
- **OD-F-Q65b-1.** Record `docs/records/audits/2026-10/mvp6-ct-verdicts-q183-q184-q65b-2026-10-01.md` (`b226a4f4…`, line 48) says there is no generic transition key. A transition is gated by its target-state key only. v3 removes the `supplychain.returns.transition` conjunction from the UI draft.
- **Backend half: Q187** (LANE 3, per-target guard; record line 60). Q189 verifies Q187 and Q188 together (record line 62).

## Changes (v2 → v3)

### R1: page title (F-Q65b-2)
`overlay/frontend/Diten.Web/Views/SupplyChain/Returns/Index.cshtml:28` now reads `<h5 class="mb-0" id="returnsTitle" tabindex="-1">`. The id appears once.

### R2: focus fallback (F-Q65b-2)
All changes are in `overlay/frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Returns/index.js`.

- **New flags** `createDenied` and `transitionDenied` (:177–178).
- **New `focusStableIfLost(surface)`** (:192–196). It focuses `#returnsTitle` with preventScroll, but only when the active element is BODY or inside the closed surface.
- **`restoreCreateOpener()`** (:179–190). When create was denied, or when the opener/Add is disabled or missing, it calls `focusStableIfLost`. Otherwise it focuses the opener as before.
- **`closeCreateAsForbidden`** (:507–516). It sets `createDenied` when the panel was open; if the panel was already closed, it falls back at once. The toast and Add-disabled behaviour are unchanged (Q174 O-1).
- **Transition 403/404 branch** (:783–791). It sets `transitionDenied` when the panel was open; if the panel was closed, it falls back at once. The toast is unchanged.
- **Hidden handler** (:1097–1104). For `offcanvasReturnTransition`, it calls `focusStableIfLost(el)` when `transitionDenied` is set. The `restoreCreateOpener();` call stays (:1103), so the existing Q160 facts still hold.

### R3: no generic transition key (OD-F-Q65b-1)
- **`overlay/frontend/Diten.Web/Models/SupplyChain/Returns/ReturnViewModels.cs`.** The `Transition` constant is removed. Each `Can*` is now `shipmentRead && has(<target key>)`. The doc comment says the gating is by target key only.
- **`overlay/frontend/Diten.Web.Tests/Controllers/SupplyChainReturnsControllerTests.cs`.** The Transition constant is removed, and the "base .transition missing" case is dropped. Positive cases use `[target key, ShipmentRead]`. The last denial case is `[Close]` (G-SHIPREAD missing).
- **Scope addition** (owner answer, 2026-10-01, "Include both as R3"). Two more files also referenced `ReturnUiPermissions.Transition`, and v3 would not compile without changing them:
  - `overlay/frontend/Diten.Web/Controllers/SupplyChainReturnsController.cs:164–168`. The generic-key check is removed. G-SHIPREAD stays, and the target-key check at :179–183 is unchanged.
  - `overlay/frontend/Diten.Web.Tests/Forms/ReturnFormContractTests.cs:58–82`. The fact is renamed `Page_permissions_require_shipments_read_and_the_target_key_for_every_mutation` and has no Transition key.

### R4: manifest provider (OD-F-Q65b-1)
- **`ReverseLogisticsManifestProvider.cs:11–14`.** Doc comment only: transitions are gated by the target key only, there is no generic key, and the backend guard is Q187. G-SHIPREAD (`supplychain.shipments.read`) is kept.
- **`ReverseLogisticsManifestProviderTests.cs`.** `ApiOnlyAllowList` is now empty (:29–30), and :87 asserts `Assert.Empty(ApiOnlyAllowList)`.

### R5: platform registration
`overlay/_shared-integration/platform-registration.md` item 5 (:16–17): the "every transition actor also needs `supplychain.returns.transition`" sentence is removed. Role templates grant the target keys only.

### R6: tests
- `ReturnIndexBehaviorTests.cs:244–266` adds `Denied_create_and_transition_send_focus_to_the_page_title_not_a_disabled_control`.
- `ReturnFormContractTests.cs:200–206` adds `Page_title_is_a_stable_focus_target` (id and `tabindex="-1"`, single id).
- No test file was added; the file list is equal to v2's.

## Unchanged
- Routes, permission keys, page code, nav key (`Nav.Page.RETURNS`), resx keys, nav fragments.
- 26 of 36 files are byte-identical to v2.
- The archive uses the same paths, modes (0700/0600), owner 0:0 and mtime 1970-01-01 as v2.

## Observation for Q189 and CT
The M-03 manifest test (`ReverseLogisticsManifestProviderTests`) now expects an empty `ApiOnlyAllowList`. It passes only after Q187 removes `ReturnPermissions.Transition` from the backend constants. On the current backend, without Q187, it would fail. Verify the two together.

No build or test run was done in the LANE: tests were checked by textual emulation, and JavaScript by `node --check` (CHECK-RUN.tsv).

## Files
- `returns-ui-draft-overlay-v3.tar.gz` — the v3 overlay archive.
- `FILE-PLAN.tsv` — 10 changed files, each with its v2 and v3 sha256.
- `CHECK-RUN.tsv` — 13 checks, all PASS.
- `SHA256SUMS`

Agent PASS ≠ CT ACCEPTED — return to CT.
