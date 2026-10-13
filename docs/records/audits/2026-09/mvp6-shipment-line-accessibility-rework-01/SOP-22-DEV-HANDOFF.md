# MVP6-SHIPMENT-LINE-ACCESSIBILITY-REWORK-01 — SOP §22 DEV Handoff

## Verdict

**DEV PASS — writer complete.** `SHIP-UI-PRES-183-01` is closed at the writer level for the exact bounded Shipment line-input surface. Independent VER remains required and is not implied by this verdict.

## Authority and baseline

- Branch / HEAD: `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`.
- Approved UI baseline: 354-entry manifest SHA-256 `e6551f4552bd05682dcd9f2753044aa0b803dd4df90875952cca89608ab98ab3`.
- The 354 source inputs were reproduced exactly before editing (`raw/preimage-manifest-check.json`).
- Existing repository dirt was preserved; implementation ran in `/private/tmp/mvp6-shipment-line-accessibility-rework-01/source` and only this audit directory was added to the repository.

## Exact change

Three approved Shipment UI/test paths changed:

1. `frontend/Diten.Web/Views/SupplyChain/Shipments/_Form.cshtml`
2. `frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Shipments/create.js`
3. `frontend/Diten.Web.Tests/Forms/ShipmentFormContractTests.cs`

The template marks the six labels by field. Runtime synchronization assigns `shipmentLine_<index>_<field>` IDs and matching `htmlFor` values after initial render, add, and remove. Keyboard add focuses the new line; removing a line focuses the successor line (or preceding final line). No `name` attributes were introduced and the existing `data-field`/DOM-order payload mapping remains unchanged.

- Patch SHA-256: `4c153b42890f10e289e421c0428ff1c1eff252f39892505ed0556cfa4efe5055`.
- Changed-file inventory SHA-256: `52ba260e00ff1c70d5205694426b9aa420a00a3519e939d8deb6a21d875f72ed`.
- Successor 354-entry manifest SHA-256: `dcc6662696db5e689d2d4f6facbb5106ca53d06a957caf21c5fabc912a0c7c88`.
- Built Web DLL SHA-256: `619eff09fb9b39aee3a44fbbcde8c034f7c63a0825617cd02fba79e5512c6ac7`.

## RED → GREEN

- RED: the new focused contract test failed `0/1` because each required `data-field-label` marker occurred zero times (`raw/red.trx`).
- GREEN: focused test `1/1`; bounded Shipment UI/controller regression `17/17`; Root R2 storage/HTTP regression `7/7`.
- Native runtime: SDK `8.0.417`, host `8.0.23`; Web Release build completed with zero warnings and zero errors.
- Patch passed `git apply --check`, applied to the exact preimage copy, and produced the three target hashes in `CHANGED-FILES.tsv`.

## Browser evidence

An isolated fixture ran on `127.0.0.1:6040` and the freshly built Web binary on `127.0.0.1:6041`. This is presentation/accessibility evidence only; it does not replace the inherited real-Auth/backend acceptance.

At 390px and 768px:

- one, two, and three lines had unique IDs and one matching label each;
- keyboard activation of Add Line focused the new line;
- deleting the middle line reindexed IDs and preserved label/control equality;
- focus moved to `shipmentLine_1_lineNumber` after deletion;
- clicking a label focused its corresponding input;
- Tab moved from planned delivery to Add Line and then the first line input;
- invalid submit focused the visible `role=alert` summary;
- page width did not overflow;
- payload field values/order and absence of `name` attributes were preserved.

Exact observations are in `raw/browser-accessibility.json`. No supported durable PNG export was available; PNG stays **OPEN** and no restriction bypass was attempted.

## Scope preservation

The patch does not touch shared layout, DataTable infrastructure, Auth, Gateway, backend validation, Root R2 persistence, contracts, guards, or Git state. B01/B02 and Root R2 received only proportionate regressions. No commit, push, stash, or rollout occurred.

## Handoff

A different read-only verifier must reconstruct the successor from the permanent source archive and patch, recheck the 354-entry manifest, run the focused and bounded regressions, and independently exercise browser add/remove/reindex/label/keyboard/focus behavior. The verifier must keep PNG OPEN if durable export remains unavailable.
