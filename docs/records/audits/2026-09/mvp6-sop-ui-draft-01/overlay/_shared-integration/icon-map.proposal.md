# MOD-0190 S&OP — field icon proposal (G-ICONMAP; integration owner decides)

`frontend/Diten.Web/tests/diten-field-icons.test.js` holds an `ICON_MAP` bound to the Task form (`FORM()`), so the S&OP
fields cannot be added without a shared-test change. That change belongs to the integration owner (pack §23.10, §23.13
G-ICONMAP). The S&OP draft uses these `.diten-field` icons:

| Field id (view) | Icon |
|---|---|
| `openPlanId` | `bx-hash` |
| `planName` | `bx-detail` |
| `planHorizonStart` | `bx-calendar` |
| `planHorizonEnd` | `bx-calendar-check` |
| `planDemandPlanId`, `captureDemandPlanId` | `bx-line-chart` |
| `planDemandPlanVersion`, `captureDemandPlanVersion`, repeater `resourceVersion` | `bx-git-commit` |
| `captureSourceCapturedAt` | `bx-time-five` (already used for time fields) |
| `captureSourceChecksum` | `bx-fingerprint` |
| repeater `source` | `bx-server` |
| repeater `resourceId` | `bx-hash` |
| `signOffSnapshotId` | `bx-camera` |
| `signOffRole` | `bx-user-check` |
| `signOffDecision` | `bx-check-shield` |
| `signOffComment` | `bx-align-left` (already used for multi-line text) |

Module icon for the navigation (pack §24, SOFT): `bx-check-double`, SortOrder 440.
