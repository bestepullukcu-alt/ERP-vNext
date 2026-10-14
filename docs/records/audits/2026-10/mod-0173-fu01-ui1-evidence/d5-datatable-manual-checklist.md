# WP-0173-UI1 D5 — DataTable template, checked by hand (Python verifier not runnable)

**BLOCKED-ENV: no Python on this machine.** `python3 .antigravity/scripts/verify_datatable_page.py . --area Inventory
--module StockLedger|StockMovements --reference compact --data-mode server --api-profile proxy --format gaps` and the
`quality-gate-datatable` run were **not executed** and are **not** reported as PASS. In their place, each rule of
`.antigravity/rules/frontend-datatable-template.md` (and the `frontend-js-standard.md` items it points to) is checked
below against the shipped files, with `file:line` evidence (paths relative to `frontend/Diten.Web/`; line numbers at
the evidence commit). Mechanical rules that a test already measures name that test.

**Compact file-set gap (closed by UI-2, not by UI-1):** `StockMovements/Create.cshtml`, `Edit.cshtml` ("Correct",
KD-1), `Details.cshtml`, `_Form.cshtml` and `StockLedger/Create.cshtml` (KD-2) are **absent on purpose** — the
movement form, Correct and the inbound entry are UI-2 scope. Until UI-2 lands, a Compact verifier run on either module
would report those files missing; that is the expected interim state, not a deviation of UI-1.

| # | Rule (template / JS standard) | StockLedger (S-1) | StockMovements (S-2) | Result |
|---|---|---|---|---|
| 1 | Layout explicit, from pack `shell: tenant` | `Views/Inventory/StockLedger/Index.cshtml:8` | `Views/Inventory/StockMovements/Index.cshtml:8` | PASS (grep 2/2, `d5-greps.log`) |
| 2 | `{Area}` is a folder, not ASP.NET Areas | `Views/Inventory/…`; no `Areas/` (`d5-greps.log`) | same | PASS |
| 3 | Common file set: Index, _Filter, _DataTable, _IndexL10n, `{Module}Index.cs`, js index.l10n.js + index.js | `Views/Inventory/StockLedger/{Index,_Filter,_DataTable,_IndexL10n}.cshtml`, `StockLedgerIndex.cs`, `wwwroot/assets/js/Inventory/StockLedger/{index.l10n,index}.js` | same set under `StockMovements/` | PASS |
| 4 | Compact extra set (Create/Edit/Details/_Form) | — | — | **UI-2** (see above) |
| 5 | `_Filter` partial on every list page | `Index.cshtml:24` (+ contract marker comment `:23`, golden-reference form) | `Index.cshtml:25` | PASS |
| 6 | `_Filter` = inline collapse bar: `#inlineFilterHost` › `.collapse#inlineFilterCollapse` › `form#filterForm[data-no-tracker]`, Apply/Reset | `_Filter.cshtml:11,12,14,61,64` | `_Filter.cshtml:10,11,13` + Apply/Reset | PASS |
| 7 | Bounded value sets are Select2 chips, single-select has `value=""` ShowAll | stock status / warehouse / location: `_Filter.cshtml:31,37,47` | warehouse / location `_Filter.cshtml:28` (+ location) | PASS |
| 8 | Unbounded values not faked as chips | product / SKU = server-searched Select2 (W-UI-01), lot = typed (BG-02), dates = date inputs | product, lot, from/to | PASS (deliberate, pack §19) |
| 9 | L10n bridge: `_IndexL10n` JSON payload + `index.l10n.js` merge, no long `window.L10n.X =` blocks | `_IndexL10n.cshtml:6`, `index.l10n.js:27,33` | `_IndexL10n.cshtml:6`, `index.l10n.js` | PASS |
| 10 | Marker class name == resx file name | `StockLedgerIndex.cs` ↔ `Resources/Views/Inventory/StockLedger/StockLedgerIndex.{7}.resx` | `StockMovementsIndex` ↔ `…/StockMovementsIndex.{7}.resx` | PASS (render test resolves keys: `InventoryAccessSurfaceTests`, `InventoryResxParityTests`) |
| 11 | v2 marker `data-dt-standard="v2"` + `id` | `_ListShell` renders it; `_DataTable.cshtml:14` TableId `dt-stockledger` | `_DataTable.cshtml:15` `dt-stockmovements` | PASS (`InventoryAccessSurfaceTests.With_read_…` asserts the rendered attribute) |
| 12 | data_mode triple: pack `server` = `<table data-dt-data-mode="server">` = `createList({dataMode:'server'})` | `_DataTable.cshtml:15`, `index.js:166` | `_DataTable.cshtml:16`, `index.js:257` | PASS (rendered attr asserted by test) |
| 13 | Server mode: page JS writes no `serverSide`, `ajax.data`, `ext.search`, `matches:`, `pageSize=` | grep 0 (`d5-greps.log`) | grep 0 | PASS |
| 14 | `stateSave: false`; Save View via personalization (`moduleKey` + `pageKey`) | factory forces `stateSave:false`; `index.js:173-174` `Inventory/StockLedger` | `index.js:264-265` `Inventory/StockMovements` | PASS |
| 15 | Save View CTA rendered (d-none until dirty) | factory `saveFilterBtn` — seen live (`toolbarButtons` dt-save-filter-btn, smoke) | same | PASS (runtime) |
| 16 | Skeleton = shared `_TableSkeleton` via `_ListShell`; no spinner / "Loading" text | `_DataTable.cshtml:36` | `_DataTable.cshtml:38` | PASS |
| 17 | No selection / no bulk (read-only lists) → `HasSelection = false`, no `_BulkActionBar` | `_DataTable.cshtml:17` | `_DataTable.cshtml:18` | PASS (render test: no `dt-checkboxes-select-all`, no `bulkActionBar`) |
| 18 | Row actions via `renderActions` + `onRowAction` dispatcher; no `quickView` | `index.js:169,228` (History) | `index.js:240,260` (Reversal) | PASS |
| 19 | Confirmation through the shared `window.showConfirm` (MOD-0013), no raw `Swal.fire` | — | Reversal: `showConfirm?.(…)` with `didOpen` (keeps the dialog open on a refusal) | PASS (rule of `tests/dialog-one-implementation.test.js` replicated with node: 0 offenders) |
| 20 | No import entry (BL-441) | none | none | PASS |
| 21 | Compact Add New → Create page | `addNewText: ''` (`index.js:171`): no Create page in UI-1 → no button, no dead link | `index.js:262` | N/A in UI-1 (UI-2 adds NEWMOVEMENT) |
| 22 | `DtDefaults.exportButtons()` Action/Export collection | **removed** for both lists in `initComplete` (`index.js:185`) | `index.js:276` | **DEVIATION (pack-mandated):** pack §2/§20 + WP "CSV export yok" until the export gate; `frontend-js-standard` §3 assumes the collection. Recorded as ASSUMPTION-UI1-07 |
| 23 | Search box | none (`searching:false`) — contract has no search (BG-04) | none (BG-09) | PASS (pack UI-02) |
| 24 | Column sorting | none (`ordering:false`, all `orderable:false`) | none | PASS (pack UI-02) |
| 25 | A11y: icon-only buttons have title + aria-label | History / Reversal / passive button carry both | same | PASS |
| 26 | UAS-001 gate, no redirect, no skeleton | `Index.cshtml:17` | `Index.cshtml:18` | PASS (render tests + live smoke + D4(b) sabotage) |
| 27 | Required-contract parity / form sections | — | — | N/A (no form in UI-1; UI-2) |
