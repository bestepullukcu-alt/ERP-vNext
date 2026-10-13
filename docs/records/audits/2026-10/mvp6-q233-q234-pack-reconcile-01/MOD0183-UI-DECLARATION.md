# Q234 — MOD-0183 UI declaration: field count and golden-reference decision (PROPOSED — not applied)

| Pack | sha256 before (unchanged in the repo) | sha256 after (proposed) | Lines | Proposed text | Diff |
|---|---|---|---|---|---|
| `MOD-0183-shipment-tracking-pod.md` | `2a65ce1d516c1850bef74f9d26e5d54dfc6042acae45ad5199730ef43ddf2c83` | `7dea2f64eff7e823ff713b6c44f53b378a097c7bf7d99162d66754a3d4360d6a` | 395 → 546 | `proposed/MOD-0183-shipment-tracking-pod.md.txt` | `proposed/MOD-0183-shipment-tracking-pod.md.diff.txt` |

## Frontmatter

| Field | Now | Proposed | Basis |
|---|---|---|---|
| `shell` | `none` | `tenant` | three page views state `Layout = "_LayoutTenantShell";` (`Index.cshtml:7`, `Create.cshtml:8`, `Details.cshtml:8`); F-Q231-3 |
| `form_field_count` | `0` | `13` | derivation below |
| `golden_reference` | `none` | `compact` | 13 > 8, AGENTS.md §6 |
| `status` | `ready-for-dev` | unchanged | rule |
| `status_note` | unchanged | unchanged | it still says "first executable slice is backend/contract only"; left as history, superseded for the UI by the new §23 |

## Field count — derivation

Source: `frontend/Diten.Web/Views/SupplyChain/Shipments/_Form.cshtml` (sha256 `c22e1b0b589cc5ecfb892ff6a60a71c496140766f7aab9d3c9d5e4fe5b33dbea`) and
`frontend/Diten.Web/Models/SupplyChain/Shipments/ShipmentViewModels.cs:5-25`; cross-checked with the payload builder `frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Shipments/create.js:44-51`.

| Group | Fields | Count |
|---|---|---:|
| Header inputs (`_Form.cshtml:10-18`) | sourceModule, sourceType, sourceDocumentId, warehouseReferenceId, shipToReference, plannedShipAt, plannedDeliverAt | 7 |
| Line inputs, one repeatable template (`_Form.cshtml:23`) | lineNumber, itemId, skuId, quantity, uomId, inventoryReferenceId | 6 |
| **Counted** | | **13** |
| Not counted | the `lines` container itself; the antiforgery token; `shipmentNumber`, `status` (server-set); Id, TenantId, audit fields (none is in the form) | — |

The three sources agree: 13 inputs in the view, 7 + 6 properties in the two view models (`Lines` is the container), 7 + 6 names in the payload.

## Golden-reference decision

- Rule (AGENTS.md §6; `module-pack-standard.md` §3): 8 or fewer → slim; more than 8 → compact.
- **13 → compact.**
- The file layout agrees (separate `Create.cshtml`, `Details.cshtml`, `_Form.cshtml`; no create/edit offcanvas in `Index.cshtml`), but the count decides, not the layout.
- **Sensitivity (F-Q234-1).** The rule does not say how a repeatable line group is counted. Counting each line field type once gives 13.
  MOD-0186 §32.6 counts the same way ("the `lines` container is not counted"). Counting header fields only would give 7 and `slim`,
  which the tree does not follow. CT to confirm the counting rule.

## What the proposed §23 adds

Screens and routes (10 rows), the 13 create fields, the two Details forms (8 fields), the list profile, the files that exist, the
differences from the Compact file set, 14 UI acceptance rows (SU-01…SU-14, none ticked), and 10 open items (`OPEN-ITEMS-ADDED.tsv`).
It changes no line of §§1–22 and supersedes, for the UI only, the statements listed in its first paragraph.

## Differences between the tree and the Compact set (F-Q234-2)

| Compact expects | Tree | Note |
|---|---|---|
| `Edit.cshtml` | absent | the frozen contract has no update operation |
| no offcanvas for create/edit/quick view | two offcanvas action forms on Details (Change Status, Capture POD) | not the forbidden files; CT to rule |
| `_Form.cshtml` shared by Create and Edit | used by Create only | follows from the first row |
| — | `_LineEditor.cshtml` is a one-line comment | placeholder |

## Not done

- `verify_module_id.py` and `verify_datatable_page.py` were not run (this WP starts nothing).
- The Golden Reference Compact pack and code were not opened beyond a directory listing of `Views/DevEnablement/GoldenReferenceCompact/` (it has `Edit.cshtml`).
- No runtime statement is made; every "State" in SU-01…SU-14 is Q231's static reading.
