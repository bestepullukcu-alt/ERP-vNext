# MOD-0187 Claims — field icon proposal (G-ICONMAP; integration owner decides)

`frontend/Diten.Web/tests/diten-field-icons.test.js` holds an `ICON_MAP` bound to the Task form (`FORM()`), so the
Claims fields cannot be added to it without a shared-test change. That change belongs to the integration owner
(pack §32.10, §32.13 G-ICONMAP). The Claims draft uses these `.diten-field` icons:

| Field id (view) | Icon | Already used in ICON_MAP? |
|---|---|---|
| `claimShipmentId` | `bx-package` | no |
| `claimReasonCode` | `bx-purchase-tag` | yes (`taskTypeId`) |
| `claimClaimedAmount` | `bx-money` | no |
| `claimCurrency` | `bx-coin` | no |
| evidence rows (`.claim-evidence-input`) | `bx-paperclip` | no |
| `transitionTarget` | `bx-git-branch` | no |
| `transitionOccurredAt` | `bx-time-five` | yes (`taskEstimateHours`, `taskSpentHours`) |
| `transitionApprovedAmount` | `bx-check-shield` | no |
| `transitionResolutionCode` | `bx-purchase-tag` | yes |
| `transitionNote` | `bx-align-left` | yes (`taskDescription`) |

The carrier link is a checkbox (no `.diten-field` icon), matching the golden-slim switch pattern.
