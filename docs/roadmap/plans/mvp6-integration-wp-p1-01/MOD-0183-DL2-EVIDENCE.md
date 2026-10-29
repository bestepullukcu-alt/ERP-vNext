# MOD-0183 DL-2 — evidence (Q501, S23-7)

Pack: `execution/domains/supply-chain-execution/module-packs/MOD-0183-shipment-tracking-pod.md` r7
(sha256 `22fe550daa31c20c3957730fffce82fd4ea726d401c82a450c3f2ab758ea8309`), §23.3 / §23.4 / §23.5 S23-7.
Branch `feature/sce/mod0183-dl2-01`, base `7ac15454e` (main after #294). Worker PASS is not closure; the CT verifies.

## What changed

| File | Change |
|---|---|
| `ShipmentsIndex.{en,tr,fr,es,zh,ar,ru}.resx` | 33 keys per language: 25 `InventoryRefused{Code}` (closed list), `InventoryRefusedUnknown`, `ErrInventoryRefused`, `ErrShipmentLineStockIncomplete`, `ErrDispatchInProgress`, `ErrDispatchCompensationFailed`, `ErrDependencyUnavailable`, `DispatchStateInProgress`, `DispatchStateCompensationFailed`. 72 → 105 keys in each file |
| `Views/SupplyChain/Shipments/_IndexL10n.cshtml` | the texts in the page L10n payload; `inventoryCodes` (code → text) and `dispatchStates` dictionaries |
| `wwwroot/assets/js/SupplyChain/Shipments/details.js` | `errorText(status, body)` and `statusText(status, dispatchState)`; no layout change (the status cell shows `Planned · Reversal failed`) |
| `Diten.Web.Tests/JavaScript/ShipmentDispatchTextTests.cs`, `…/Node/shipment-details-error-text.test.mjs` | tests below |

Final texts: `MOD-0183-DL2-TEXTS-FINAL.tsv` (this folder). Differences from the CT draft `MOD-0183-DL2-TEXTS.tsv`:

1. **Key names** follow the house naming (PascalCase, no dots — no resx in SupplyChain has a dotted key):
   `InventoryRefused.LOT_EXPIRED` → `InventoryRefusedLotExpired`, `InventoryRefused.Unknown` → `InventoryRefusedUnknown`
   (the pack names `ShipmentsIndex.InventoryRefused.Unknown`), `DispatchState.X` → `DispatchStateX`.
2. **`ErrDependencyUnavailable` is neutral** ("A required service is not reachable right now."): the Shipments API also
   answers 503 `DEPENDENCY_UNAVAILABLE` when legal-entity validation is down (`ShipmentContextMiddleware.cs:42`), on every
   call — "Inventory is not reachable" would be wrong there.
3. **`InventoryRefusedUnknown`** is a reason sentence ("Unrecognized reason (code: {0}).") because the screen shows it after
   `ErrInventoryRefused` ("Inventory refused the stock issue for line {0}."); the draft text repeated that sentence.
4. **House terms:** zh shipment = 货运 (not 发运单); ar line = البند and SKU kept as `SKU`; ru item = позиция, dispatch =
   отправка, Inventory = «Модуль запасов» (склад = warehouse in this screen); tr «Stok servisi …»; fr typographic `’`.

Screen behaviour: `INVENTORY_REFUSED` → line text + reason of `details.inventoryCode` (own-property lookup; any other code,
including `constructor`, → `InventoryRefusedUnknown` with the code); `SHIPMENT_LINE_STOCK_INCOMPLETE` → text with
`details.lineNumber`; `DISPATCH_IN_PROGRESS`, `DISPATCH_COMPENSATION_FAILED`, `DEPENDENCY_UNAVAILABLE` → own texts; 403
(`INVALID_REQUEST`, e.g. `CROSS_LE_FORBIDDEN`) → existing `ErrForbidden`. `dispatchState` exists only on `ShipmentDetail`
(not on `ShipmentSummary`), so the label is shown on Details only; the list has no new column.

## Tests

- New: `ShipmentDispatchTextTests` — 4 cases (view maps exactly the 25 closed-list codes; every mapped key non-empty in 7
  languages and different from English; `{0}` kept in the three templates; key parity of the 7 resx; Node bridge
  `# pass 5`). Node file: 5 cases, run against the shipped `details.js`.
- Filtered run (`ShipmentDispatchTextTests|ShipmentDetail`): 14/14 green.
- Full `frontend/Diten.Web.Tests`: **4102 total, 4100 passed, 2 failed** —
  `NavManifestL10nGuardTests.Every_manifest_module_has_a_Nav_Module_key_in_all_seven_languages` (missing
  `Nav.Module.MRPREPLENISHMENT` / `SAFETYSTOCKOPTIMIZATION` in SharedResource) and
  `…Every_manifest_provider_in_the_repo_is_parsed_and_yields_a_module_code_and_pages` (`MrpManifestProvider.cs: no pages
  parsed`). Both read only `services/Diten.PlanningService/**` manifests and `SharedResource.*.resx`; `git diff 7ac15454e HEAD`
  touches neither, so their inputs equal main's — **pre-existing on main by input identity, not re-run on a main checkout**.
- UI compliance: `python3 scripts/uic_changed_pages.py --base 7ac15454e` → exit 0, no `YENİ KIRMIZI`; page resx CI-13 PASS in
  7 languages; `details.js` CI-6 = 1 = recorded baseline (FU-14).

## Sabotage (each applied alone, filtered run, file restored after; tree clean)

| # | Sabotage | Result |
|---|---|---|
| S1 | ru resx: `InventoryRefusedLotUnderRecall` removed | RED (parity + closed-list) |
| S2 | ar resx: `DispatchStateCompensationFailed` empty | RED |
| S3 | tr resx: `ErrDispatchInProgress` = English text | RED |
| S4 | tr resx: `ErrInventoryRefused` loses `{0}` | RED |
| S5 | view: `LOT_EXPIRED` dropped from `inventoryCodes` | RED |
| S6 | view: `dispatchStates.InProgress` dropped | RED |
| S7 | view: `dispatchInProgress` payload removed | RED |
| S8 | js: `DISPATCH_IN_PROGRESS` mapping removed | RED (C# + Node) |
| S9 | js: `SHIPMENT_LINE_STOCK_INCOMPLETE` branch removed | RED (C# + Node) |
| S10 | js: unknown inventoryCode → validation text | RED (C# + Node) |
| S11 | js: own-property guard dropped (`constructor`) | RED (Node) |
| S12 | js: dispatchState label not shown | RED (Node) |
| S13 | js: 403 `INVALID_REQUEST` mapped to validation text | RED (Node) |

Not covered: the DOM (alert element, status cell) is not executed — no live lane run in this WP.
