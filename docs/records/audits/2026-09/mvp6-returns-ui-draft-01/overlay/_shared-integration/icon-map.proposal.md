# MOD-0186 Returns — field icon and module icon proposal (G-ICONMAP; integration owner decides). NOT APPLIED.

`frontend/Diten.Web/tests/diten-field-icons.test.js` holds an `ICON_MAP` bound to the Task form (`FORM()`), so the Returns
fields cannot be added to it without a shared-test change. That change belongs to the integration owner (pack §32.10,
§32.13 G-ICONMAP). The Returns draft uses these `.diten-field` icons:

| Field id / class (view) | Icon | Note |
|---|---|---|
| `returnShipmentId` | `bx-package` | same as the Claims draft |
| line rows `.js-line-quantity` | `bx-undo` | return quantity (text, `inputmode=decimal`) |
| `returnReasonCode` | `bx-purchase-tag` | already used in ICON_MAP (`taskTypeId`) |
| evidence rows (`.return-evidence-input`) | `bx-paperclip` | same as the Claims draft |
| `transitionTarget` | `bx-git-branch` | same as the Claims draft |
| `transitionOccurredAt` | `bx-time-five` | already used in ICON_MAP |
| `transitionDispositionCode` | `bx-purchase-tag` | already used in ICON_MAP |
| `transitionInventoryReference` | `bx-transfer-alt` | opaque reference |

The line-select control is a checkbox (no `.diten-field` icon), matching the golden-slim switch pattern.

Module icon for the navigation (pack §33, SOFT, seed-once): **`bx-undo`**, module **SortOrder 420**; page `RETURNS` sort 10.
