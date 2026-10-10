# MOD-0187 Claims UI DRAFT — v3 → v4 changes (Q64f)

- **v3:** `mvp6-claims-ui-draft-03/claims-ui-draft-overlay-v3.tar.gz` `66e72bf6af1ebb6fffbcedf5f8b7934cd9a5a0f95cb1abf558b92fa3b500ce0c` (39 files).
- **v4:** `claims-ui-draft-overlay-v4.tar.gz` `2b34741af91dc640862cd6e460809e860040e21747a66849aa5b61bfa099293f` (39 files; same member list and order as v3).
- **Totals:** 0 added · **4 modified** · 0 removed · 35 unchanged. Compared file by file (sha256), per archive path.
- **Source of the defect:** Q122 verification, `mvp6-q122-ver-claims-v3-01/REPORT.md` §6 **Q122-D5** (CU-25 FAIL).
- **Authority:** owner decision ~15:45, CU-25 / Q122-D5 = A (fix in v4).

| # | File (archive path) | v3 sha256 → v4 sha256 | Change | Reason |
|---|---|---|---|---|
| F-01 | `overlay/frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Claims/index.js` | `c8ef218e…5058` → `77733718…f39c` | New `restoreCreateFocus()` (lines 177–188). It runs at the end of `submitCreate`'s `finally`, right after Save is re-enabled (line 573). While the create surface is still open and not hiding, and focus is outside it, focus goes to the first `[aria-invalid="true"]` / `.is-invalid` field, else Save when enabled, else Shipment ID. | **Q122-D5:** Save is disabled while the request is pending, so after a rejected submit (400/409/415/422/5xx/network) focus fell to BODY. Bootstrap 5.3.3's offcanvas Escape handler listens on the element, so Escape was ignored until one Tab. This is the approach Q122 proposed (§6), and it breaks no rule in frontend-js-standard / premium-modal-standard. The `aria-disabled` alternative was not chosen: it would change Save's disabled semantics and the CU-17 ineligible-state check. |
| F-02 | `overlay/frontend/Diten.Web.Tests/JavaScript/ClaimIndexBehaviorTests.cs` | `16a5ef85…69de` → `b802ca25…eff5` | New `[Fact] Create_surface_keeps_keyboard_focus_after_a_rejected_submit`. It asserts the helper's guards and targets, that the call sits after the Save re-enable in the create `finally`, and that the transition submit does not call it. | Contract test for F-01. On v3 it fails at `Slice("const restoreCreateFocus", …)` (a Python replay of the same assertions gives v3 FAIL / v4 PASS). The Mac run in Q129 is the proof. |
| F-03 | `runtime-scenarios/claims-ui.spec.mjs` | `5c30b903…49a1` → `ab3cb56d…e64e5c` | New Playwright scenario: create with `usd` → 400, then focus is inside the surface, Escape closes it without Tab, and focus is back on Add (Q64d-D4); PNG `03b-create-400-escape`. | The runtime row Q122 asked for (CU-25). |
| F-04 | `runtime-scenarios/README.md` | `c9dba2fc…80ba` → `9989946d…45b8` | One scenario-table row for F-03. | Keeps the scenario index complete. |

## Not changed

- **Transition surface:** unchanged. It does not have this problem, because focus returns to Submit after the shared confirm. F-02 asserts that the transition code does not call the helper.
- **Other files:** no view, resx (byte-identical 7/7), controller, model, provider or `_shared-integration` item changed. No shared file changed (`dt-defaults.js`, `backbone-custom.css`, bootstrap).
- **Scope:** no endpoint, permission or route was added, and `supplychain.carriers.read` was not added.
- **Still open from v3:** CU-28 (integrated target), F-Q122-01, O-01, Q64d-D3 (template owner).
