# MOD-0192 Capacity Planning — field icon and module icon proposal (G-ICONMAP; integration owner decides). NOT APPLIED.

`frontend/Diten.Web/tests/diten-field-icons.test.js` holds an `ICON_MAP` bound to the Task form (`FORM()`), so the Capacity
fields cannot be added without a shared-test change. That change belongs to the integration owner (pack §23.10, §23.13
G-ICONMAP). The Capacity draft uses these `.diten-field` icons:

| Field id / repeater class (view) | Icon |
|---|---|
| `openPlanId`, `openScenarioId` | `bx-hash` |
| `planName` | `bx-detail` |
| `planHorizonStart`, adjustment `js-period` | `bx-calendar` |
| `planHorizonEnd` | `bx-calendar-check` |
| `planDemandPlanId` | `bx-line-chart` |
| `planDemandPlanVersion`, constraint `js-constraint-version` | `bx-git-commit` |
| `planSourceCapturedAt` | `bx-time-five` (already used for time fields) |
| `planSourceChecksum` | `bx-fingerprint` |
| `scenarioName` | `bx-git-branch` |
| constraint `js-constraint-id` | `bx-lock-alt` |
| constraint `js-constraint-source` | `bx-server` |
| adjustment / evaluate `js-resource-ref` | `bx-cube` |
| adjustment `js-delta` | `bx-transfer-alt` |
| adjustment `js-uom` | `bx-ruler` |
| `evaluationMode` | `bx-slider-alt` |

Module icon for the navigation (pack §24, SOFT, proposed): **`bx-bar-chart-alt-2`**, module **SortOrder 450** (after S&OP 440).
Page sort: `CAPACITY_PLANS` 10, `CAPACITY_PLAN_DETAILS` 11. Action sort (not fixed by the pack; README A11): `CREATE` 10,
`CREATE_SCENARIO` 10, `EVALUATE` 20.
