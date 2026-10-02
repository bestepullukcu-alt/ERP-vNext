# WP-0173-UI2 D5 — Compact file set and form / details template, checked by hand (Python verifier not runnable)

**BLOCKED-ENV: no Python on this machine** (`python`, `python3`, `py` → not found; measured 2026-10-02, same as UI1/LOC2).
`python3 .antigravity/scripts/verify_datatable_page.py . --area Inventory --module StockMovements --reference compact
--data-mode server --api-profile proxy --format gaps` (and `--module StockLedger`) and `quality-gate-datatable` were
**not executed** and are **not** reported as PASS. In their place each rule of `frontend-form-template.md`,
`frontend-details-template.md`, `premium-modal-standard.md`, `unauthorized-surface-standard.md` and the FU01 pack
§9/§11/§12 is checked below against the shipped files (paths relative to `frontend/Diten.Web/`, line numbers at the
code commit `38911b95f`). Where a test measures the rule, it is named. The two list pages keep the UI-1 checklist
(`mod-0173-fu01-ui1-evidence`); only what UI-2 changed on them is listed here (rows 24–27).

| # | Rule | Where | Result |
|---|---|---|---|
| 1 | Compact file set on the movement screen: Index, **Create, Edit, Details, _Form**, _Filter, _DataTable, _IndexL10n, marker | `Views/Inventory/StockMovements/{Index,Create,Edit,Details,_Form,_FormL10n,_Filter,_DataTable,_IndexL10n}.cshtml`, `StockMovementsIndex.cs` | PASS (U2-12) |
| 2 | StockLedger Create (KD-2 compliant mapping) hosts the SAME `_Form` partial | `Views/Inventory/StockLedger/Create.cshtml` → `~/Views/Inventory/StockMovements/_Form.cshtml` | PASS (`MovementPagesSurfaceTests.The_stock_entry_page_…`) |
| 3 | `Layout = "_LayoutTenantShell";` explicit on every page (VIEW-001, pack §9) | Create/Edit/Details + StockLedger/Create: 4/4 (`d5-greps.log`) | PASS |
| 4 | UAS-001: without the page permission only `_AccessDenied` — no heading, form, button; no redirect | `Create.cshtml:16`, `Edit.cshtml:17`, `Details.cshtml:21`, `StockLedger/Create.cshtml:20` | PASS (render tests ×4 + live personas, screenshots 25/26) |
| 5 | Compact action header: `h5.mb-0` + breadcrumb `{List}Title › Current`, Cancel + primary submit; no `PageDescription` repeat | `_Form.cshtml` header block (list title of the hosting page via `ViewData["ListTitle"]`) | PASS |
| 6 | Cards `row g-4`, fields `row g-3`; no `col-lg-10 mx-auto` | `_Form.cshtml` (`row g-4` form, `row g-3` per card) | PASS |
| 7 | Section headings: `.card-section-title` with a glyph; description line uses `card-section-desc` | `_Form.cshtml:157,196,229,297,335,373,397` | PASS |
| 8 | Pack §12 card order ① Movement (1, 2) ② Item (3, 4) ③ Source & target (5–9) ④ Quantity (10–12) ⑤ Document & dates (13–15) | `_Form.cshtml:157 / 196 / 229 / 297 / 335` | PASS — inside ④ the serial box (10) is drawn AFTER quantity / unit (11, 12) because its counter is derived from them (ASSUMPTION-UI2-22) |
| 9 | Every field wrapped in `.diten-field` with a leading `aria-hidden` icon; label outside, feedback after | 14 drawn fields (`_Form.cshtml:163…357`) | PASS (`MovementPagesSurfaceTests.Every_form_field_carries_an_icon_that_exists_in_the_icon_set` — wrapper, glyph in `iconify-icons.css`, count pinned at 14) |
| 10 | textarea icon uses `diten-field-icon--top` | `serialNumbers` | PASS |
| 11 | Date fields: the icon OPENS the picker through the shared `diten-datefield.js` (not re-implemented) | `form.js` `initDates` → `DitenDateField.enhance(form, { enableTime … })`; pages load `diten-datefield.js` | PASS (E1: `date-field-never-dies-silently` unchanged vs base) |
| 12 | Dependent select rebuilt, no greyed-out options | SKU rebuilt per product (`form.js` `loadSkus` → `fill`), type list per page (7 / 2 types), units per SKU | PASS |
| 13 | `required` + `*` parity with the server rule (direction matrix) | `_Form.cshtml` asterisks; `form.js` `applyType` toggles `required` per matrix; `MovementRules.Check` is the server twin | PASS (`MovementFormTests` matrix tests; D4 (b) sabotage) |
| 14 | Hidden by the matrix = not posted | `form.js` `readBody` per direction; `MovementRules.Check` ignores hidden fields | PASS (`An_inbound_… never posts a source`, `An_outbound_… never posts a target`) |
| 15 | No `was-validated` (it re-pads select2 over the field icon, LOC2-09); select2 fields show the sentence only | `form.js` `fieldError` | PASS (grep 0 code hits) |
| 16 | All visible text from resx (7 languages); no English fallback in script | `_Form.cshtml`, `_FormL10n.cshtml`, `form.js` `t()` | PASS (`InventoryResxParityTests` incl. `Every_key_a_movement_script_reads_is_delivered_by_its_payload`) |
| 17 | Confirm through the shared `window.showConfirm` (one dialog implementation), danger type for the irreversible Correct | `form.js` `submit` (Correct) | PASS (E1 `dialog-one-implementation` unchanged; screenshot 11) |
| 18 | Skeletons, no spinner / "Loading" text | stock-effect `.shimmer.skeleton-row`; buttons `aria-busy` | PASS (grep 0) |
| 19 | No delete / import / export; no balance write path (KD-2) | grep 0; `StockLedgerController` GET only | PASS (`StockLedger_has_no_write_action…`) |
| 20 | Details: same section order as `_Form`; read-only `backbone-preview-section` cards | `Details.cshtml` | PASS — follows the live Golden Compact Details (`row g-4`, `backbone-preview-field`) rather than the template text (`row g-6`, `dl.row`), exactly as Compact does (ASSUMPTION-UI2-13) |
| 21 | Details: UTC shown in the reader's zone; decimals formatted from strings | `details.js` (`Intl.DateTimeFormat`, `Intl.NumberFormat` on the string) | PASS |
| 22 | Self-registration: every new route is a manifest page; form pages declare no actions | `InventoryLedgerManifestProvider` | PASS (completeness 4/4; live catalog 8 pages / 19 actions, 0 duplicates — `d6-platform-catalog.json`) |
| 23 | 7 manual types only, grouped Inbound / Outbound / Transfer; inbound status exactly 3 values | `_Form.cshtml:163` (grouped), `:269` | PASS (render test + screenshot 17; entry page 2 types, screenshot 16) |
| 24 | S-1 toolbar "New" only for `movements.create` (KD-2 entry), row "Movement" likewise | `StockLedger/index.js` `addNewText` / `visible: canCreate()` | PASS (live viewer: absent, screenshot 23) |
| 25 | S-2 toolbar "New movement" + row "Correct" only for `movements.create` and only on eligible rows | `StockMovements/index.js` `addNewText`, `visible: !!full.canCorrect` (server `CanCorrect`) | PASS (`Correct_is_offered_only…`; D4 (a); screenshots 10/24) |
| 26 | Lot shown as "unregistered lot (pre-MOD-0174)" in lists and receipt (W-UI-02) | S-1 / S-2 lot columns, `_Form` hint, `Details.cshtml` badge | PASS |
| 27 | Manifest action per rendered button (both directions) | NEWMOVEMENT (S-1, S-2), ROWMOVEMENT, CORRECTION | PASS (`InventoryLedgerManifestCompletenessTests` 4/4) |
