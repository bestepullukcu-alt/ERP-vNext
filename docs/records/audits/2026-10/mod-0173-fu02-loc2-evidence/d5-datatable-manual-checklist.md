# WP-0173-LOC2 D5 — DataTable / form template, checked by hand (Python verifier not runnable)

**BLOCKED-ENV: no Python on this machine** (`python`, `python3`, `py` → not found; measured 2026-10-02).
`python3 .antigravity/scripts/verify_datatable_page.py . --area Inventory --module Locations --reference slim
--data-mode server --api-profile proxy --format gaps` and `quality-gate-datatable` were **not executed** and are **not**
reported as PASS. In their place each rule of `frontend-datatable-template.md` / `frontend-form-template.md` /
`frontend-js-standard.md` is checked below against the shipped files (paths relative to `frontend/Diten.Web/`; line
numbers at the evidence commit). Where a test measures the rule, it is named.

L-1 `/LocationTree` is **not** a DataTable (pack §1/§11 — custom tree component); only rows 1, 2, 9, 10, 19, 25, 26
apply to it, noted per row.

| # | Rule | L-2 `/Locations` (Slim, server) | Result |
|---|---|---|---|
| 1 | Layout explicit from pack `shell: tenant` | `Views/Inventory/Locations/Index.cshtml:10`; L-1 `Views/Inventory/LocationTree/Index.cshtml:13` | PASS (grep = 2 new pages, `d5-greps.log`) |
| 2 | `{Area}` is a folder, not ASP.NET Areas | `Views/Inventory/Locations/`, `Views/Inventory/LocationTree/`; no `Areas/` | PASS |
| 3 | Common file set: Index, _Filter, _DataTable, _IndexL10n, `{Module}Index.cs`, js index.l10n.js + index.js | `Views/Inventory/Locations/{Index,_Filter,_DataTable,_IndexL10n}.cshtml`, `LocationsIndex.cs`, `wwwroot/assets/js/Inventory/Locations/{index.l10n,index}.js` | PASS |
| 4 | Slim extra: `_CreateEditOffcanvas.cshtml` (create/edit in an offcanvas on the Index) | `Views/Inventory/Locations/_CreateEditOffcanvas.cshtml` rendered at `Index.cshtml:79` (and on L-1) | PASS — the offcanvas id is `offcanvasLocationForm` and it is driven by `form.js` (pack §11 lists `form.js`), not by the factory's `form:` option: the ONE form serves two pages, needs If-Match / per-field contract errors / a type-dependent field set (ASSUMPTION-LOC2-12) |
| 5 | `_Filter` partial on the list page | `Index.cshtml:43` (+ contract marker comment `:40`) | PASS |
| 6 | Inline collapse bar: `#inlineFilterHost › .collapse#inlineFilterCollapse › form#filterForm[data-no-tracker]`, Apply / Reset | `_Filter.cshtml:10,11,13,51,54` | PASS |
| 7 | Bounded value sets are Select2 chips; single-select has `value=""` ShowAll | type multi `:17`, status multi `:27`, stockable single + ShowAll `:35-36` | PASS (options = contract enums, never data) |
| 8 | Unbounded values not faked as chips | parent = server-searched Select2 (`:43`, `/Locations/parents`) | PASS |
| 9 | L10n bridge `_IndexL10n` JSON + `index.l10n.js` merge | `_IndexL10n.cshtml:9`, `index.l10n.js:6,29,35`; L-1 `LocationTree/_IndexL10n.cshtml` + `LocationTree/index.l10n.js` | PASS (`LocationResxParityTests.Every_key_the_location_scripts_read_is_delivered_by_a_payload`) |
| 10 | Marker class name == resx file name | `LocationsIndex` ↔ `Resources/Views/Inventory/Locations/LocationsIndex.{7}.resx`; `LocationTreeIndex` ↔ `…/LocationTree/LocationTreeIndex.{7}.resx` | PASS (render + parity tests) |
| 11 | v2 marker + table id | `_DataTable.cshtml:15` `dt-locations`, rendered by `_ListShell` | PASS (`LocationAccessSurfaceTests.The_list_page_is_a_server_mode_list_without_selection` asserts `id="dt-locations" data-dt-standard="v2" data-dt-data-mode="server"`) |
| 12 | data_mode triple: pack `server` = `data-dt-data-mode="server"` = `createList({ dataMode: 'server' })` | `_DataTable.cshtml:16`, `index.js:145` | PASS (AC-21) |
| 13 | Server mode: page JS writes no `serverSide`, `ajax.data`, `ext.search`, `matches:`, `pageSize=` | grep 0 (`d5-greps.log`) | PASS |
| 14 | `stateSave:false`; Save View via personalization `moduleKey` + `pageKey` | factory forces `stateSave:false`; `index.js:157-158` `Inventory/Locations` | PASS |
| 15 | Save View CTA rendered, visible when dirty | seen live after Apply (screenshot 13) | PASS (runtime) |
| 16 | Skeleton = shared `_TableSkeleton` via `_ListShell`; no spinner / "Loading" text | `_DataTable.cshtml:33`; L-1 tree/detail use `.shimmer.skeleton-row` shaped like the rows (`LocationTree/Index.cshtml`, `_DetailPanel.cshtml`) | PASS |
| 17 | No selection / no bulk → `HasSelection = false`, no `_BulkActionBar` | `_DataTable.cshtml:18` | PASS (render test) |
| 18 | Row actions via `renderActions` + `onRowAction` | `index.js:128,148` | PASS |
| 19 | Confirmations through the shared `window.showConfirm`, no raw `Swal.fire` | `form.js:552` (status change, `didOpen` keeps refusals in the dialog) | PASS (`dialog-one-implementation.test.js` executed unchanged with a node runner: same result as on base — `e1-js-guards-*.txt`) |
| 20 | No import entry (BL-441) | none | PASS |
| 21 | Slim Add New → offcanvas create | `index.js:151` `addNewText` only for `inventory.locations.manage` (absent otherwise) | PASS |
| 22 | `DtDefaults.exportButtons()` Action/Export collection | removed in `initComplete` (`index.js:168`) | **DEVIATION (pack-mandated):** FU02 pack §11 "no export button until the export gate" — same as FU01 (ASSUMPTION-UI1-07) |
| 23 | Search box | on (`index.js:165`) → `search` to `searchLocations` | PASS (pack §11: search offered) |
| 24 | Column sorting | on for code / name / type / status (`index.js:164`), off for parent path / stockable / mode / actions | PASS (pack §11) |
| 25 | A11y: icon-only buttons have title + aria-label; L-1 tree `role=tree/treeitem/group`, `aria-expanded`, `aria-selected`, keyboard | row actions carry both (`index.js:122`); L-1 `LocationTree/index.js` (createItem, onKey) | PASS (runtime keyboard / ARIA checked in the browser pane; screenshot 09) |
| 26 | UAS-001 gate, no redirect, no skeleton | `Locations/Index.cshtml:22`, `LocationTree/Index.cshtml:27` | PASS (render tests + live + D4(b) sabotage) |
| 27 | Required-contract parity (form) | required = Code, Name, Type (create), Parent (all but Site) — label `*` + `required` on code/name/type (`_CreateEditOffcanvas.cshtml`), parent `required` toggled by type in `form.js` (`applyType`); the service validators (LOC1) refuse the same four; no ViewModel binding (JSON proxy) | PASS (live: screenshot 04) |
| 28 | Every field wrapped in `.diten-field` with a leading icon (switch excepted) | `_CreateEditOffcanvas.cshtml`, `_BulkBinsModal.cshtml` | PASS |
| 29 | Dependent select rebuilds its options (no greyed-out choices) | type options rebuilt per parent (`form.js` `restrictTypes`) | PASS (screenshot of Zone/Bin-only list in `d6-runtime-smoke.json`) |
