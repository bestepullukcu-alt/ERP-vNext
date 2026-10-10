# Per-module manifest sections (proposals, not code)

Common to all five: `Domain: "SupplyChainExecution"`, `Service: "DitenSupplyChainService"`, `ModuleVersion: "1.0.0"`,
`IsTenantAssignable: true`, `IsBaseline: false`. ModuleCode = the existing pack file slug (clean lowercase slug, not the service, not a
namespace; independent of permission keys, per standard §1). No MOD, DCP or other ID is created. DisplayName/Domain/Service/SortOrder/Icon
are SOFT (seed-once). ModuleCode, pages, actions and keys are HARD.

**Scope check (standard §2c):** every RoutePath below starts with `/SupplyChain/` and none with `/Platform/`, so every permission
derives **Tenant** scope, which is correct for tenant modules. `/api/...` adapter routes are not view routes and are not pages.

**Source status legend:** BUILT-ISOLATED = UI exists in an isolated baseline, not in the common checkout · APPROVED-NOT-BUILT = owner-approved
UI scope, no code · HELD-NOT-BUILT = UI scope prepared but not approved. Permission legend: CONST = a real `public const` in a `*Permissions`
class · CONST-ISOLATED = real, but only in the accepted isolated source (absent from the common checkout) · MAP-ONLY = a real key
returned by a mapping method, not a constant · MISSING = no such key.

---

## 1. MOD-0183 Shipment Tracking & POD — `shipment-tracking-pod`

| Field | Value |
|---|---|
| ModuleCode / ModuleName / DisplayName | `shipment-tracking-pod` / `ShipmentTrackingPod` / `Shipment Tracking & POD` |
| SortOrder / Icon (SOFT, proposed) | 390 / `bx-package` |
| UI source | BUILT-ISOLATED: `frontend/Diten.Web/Controllers/SupplyChainShipmentsController.cs` in A12 successor archive `docs/records/audits/2026-09/mvp6-shipment-a12-safe404-rework-01/successor-source.tar.gz` (`7b6a0d1a…314d`). `[Route("SupplyChain/Shipments")]`; view routes `""`, `"Create"`, `"Details/{shipmentId:guid}"` |
| Permission source | CONST: `Diten.SupplyChainService.Infrastructure/Authorization/ShipmentPermissions.cs` (common checkout) |

| PageCode | DisplayName | RoutePath (verbatim) | RequiredPermission | Parent | Nav | PageType | Sort |
|---|---|---|---|---|---|---|---|
| `SHIPMENTS` | Shipments | `/SupplyChain/Shipments` | `supplychain.shipments.read` (CONST) | — | **true** | List | 10 |
| `SHIPMENT_CREATE` | Create Shipment | `/SupplyChain/Shipments/Create` | `supplychain.shipments.create` (CONST) | `SHIPMENTS` | false | Detail | 11 |
| `SHIPMENT_DETAILS` | Shipment Details | `/SupplyChain/Shipments/Details/{shipmentId:guid}` | `supplychain.shipments.read` (CONST) | `SHIPMENTS` | false | Detail | 12 |

| Page | ActionCode | DisplayName | PermissionKey | Placement | Dangerous | UI evidence |
|---|---|---|---|---|---|---|
| `SHIPMENT_CREATE` | `SAVE` | Save | `supplychain.shipments.create` | Toolbar | no | Create page submit (Compact pattern; the list's "Create Shipment" button is navigation, as in GoldenCompact) |
| `SHIPMENT_DETAILS` | `CHANGE_STATUS` | Change Status | `supplychain.shipments.dispatch` | Toolbar | no | details.js renders one button per allowed target in `#shipmentActions`; every non-Cancelled target uses `canDispatch` |
| `SHIPMENT_DETAILS` | `CANCEL` | Cancel Shipment | `supplychain.shipments.cancel` | Toolbar | **yes** | Cancelled target uses `canCancel` |
| `SHIPMENT_DETAILS` | `CAPTURE_POD` | Capture POD | `supplychain.shipments.pod.capture` | Toolbar | no | shown when `canCapturePod` and status Dispatched/InTransit |

Not modeled: the list's row "details" link (navigation); `supplychain.shipments.reconcile` (a real constant, but no UI route or button; API-only,
like `goldencompact.reports.view`). **Gaps:** (a) the RoutePath keeps the `:guid` constraint verbatim; other manifests use `{id}`, so the platform
normalizer's handling must be confirmed in the reconcile-state test. (b) The details action bar is a page-level bar, not a DataTable toolbar;
"Toolbar" placement is the nearest supported value. (c) The Shipment UI is not in the common checkout; route truth is the archived baseline until integration.

## 2. MOD-0184 Carrier Management — `carrier-management`

| Field | Value |
|---|---|
| ModuleCode / ModuleName / DisplayName | `carrier-management` / `CarrierManagement` / `Carrier Management` (as the unapproved candidate `MODULE-REGISTRATION.patch`) |
| SortOrder / Icon | 400 / `bx-truck` (as candidate) |
| UI source | BUILT-ISOLATED: v2 Carrier UI (21 files; VER `docs/records/audits/2026-09/mvp6-carrier-ui-ver-01/`, Index `da254157…`); source **not archived** in the repo. Route per `mvp6-carrier-ui-scope-01/SCREEN-ROUTE-PERMISSION-MATRIX.tsv`: one view route `/SupplyChain/Carriers` |
| Permission source | CONST: `Infrastructure/Features/Carriers/CarrierPermissions.cs` (common checkout) |

| PageCode | DisplayName | RoutePath | RequiredPermission | Parent | Nav | PageType | Sort |
|---|---|---|---|---|---|---|---|
| `CARRIERS` | Carriers | `/SupplyChain/Carriers` | `supplychain.carriers.read` (CONST) | — | **true** | List | 10 |

| ActionCode | DisplayName | PermissionKey | Placement | Dangerous |
|---|---|---|---|---|
| `CREATE` | Create | `supplychain.carriers.create` | Toolbar | no |
| `CHANGE_STATUS` | Change Status | `supplychain.carriers.status.change` | RowAction | no |

**Gap:** the Carrier view-route set can only be proven against real source once the v2 UI is integrated; the repo holds VER evidence, not the source.

## 3. MOD-0185 Routing & Load Planning — `routing-load-planning`

| Field | Value |
|---|---|
| ModuleCode / ModuleName / DisplayName | `routing-load-planning` / `RoutingLoadPlanning` / `Routing & Load Planning` |
| SortOrder / Icon (SOFT, proposed) | 410 / `bx-map-alt` |
| UI source | **HELD-NOT-BUILT**: `mvp6-loads-ui-scope-01/SCOPE.md` (list + create; transition UI excluded, ROOT-UI-01) |
| Permission source | CONST: `Infrastructure/Features/Loads/LoadPermissions.cs` |

| PageCode | DisplayName | RoutePath | RequiredPermission | Parent | Nav | PageType | Sort |
|---|---|---|---|---|---|---|---|
| `LOADS` | Loads | `/SupplyChain/Loads` (proposed, not a route yet) | `supplychain.loads.read` (CONST) | — | **true** | List | 10 |

| ActionCode | DisplayName | PermissionKey | Placement | Dangerous |
|---|---|---|---|---|
| `CREATE` | Create | `supplychain.loads.create` | Toolbar | no |

Not modeled: `supplychain.loads.transition` (a real constant; the approved-for-prep UI scope has no transition button). **Gate:** no provider until the Loads UI scope is approved and built.

## 4. MOD-0186 Reverse Logistics — `reverse-logistics`

| Field | Value |
|---|---|
| ModuleCode / ModuleName / DisplayName | `reverse-logistics` / `ReverseLogistics` / `Reverse Logistics` |
| SortOrder / Icon (SOFT, proposed) | 420 / `bx-undo` |
| UI source | **APPROVED-NOT-BUILT**: owner decision `mvp6-returns-claims-ui-scope-owner-decision-01` + drafts `mvp6-ui-pack-drafts-01/returns/`; pack UI revision pending in `mvp6-pack-alignment-03-returns/` |
| Permission source | `ReturnPermissions.cs` in accepted isolated source (`returns46.json` entry `9c3f64ed…`; content read from `mvp6-mod0187-normal-baseline-integration-01/normal-source.tar.gz`): CONST-ISOLATED `Read`, `Create`, `Transition`; the six target keys are **MAP-ONLY** in `ForTarget(string)` |

| PageCode | DisplayName | RoutePath | RequiredPermission | Parent | Nav | PageType | Sort |
|---|---|---|---|---|---|---|---|
| `RETURNS` | Returns | `/SupplyChain/Returns` (approved, not a route yet) | `supplychain.returns.read` (CONST-ISOLATED) | — | **true** | List | 10 |

| ActionCode | DisplayName | PermissionKey | Placement | Dangerous | Key status |
|---|---|---|---|---|---|
| `CREATE` | Create Return | `supplychain.returns.create` | Toolbar | no | CONST-ISOLATED |
| `AUTHORIZE` | Authorize | `supplychain.returns.authorize` | RowAction | no | MAP-ONLY |
| `REJECT` | Reject | `supplychain.returns.authorize` | RowAction | **yes** | MAP-ONLY |
| `MARK_IN_TRANSIT` | Mark In Transit | `supplychain.returns.transit` | RowAction | no | MAP-ONLY |
| `CANCEL` | Cancel Return | `supplychain.returns.cancel` | RowAction | **yes** | MAP-ONLY |
| `RECEIVE` | Receive (manual assertion) | `supplychain.returns.receive` | RowAction | no | MAP-ONLY |
| `DISPOSITION` | Disposition | `supplychain.returns.disposition` | RowAction | no | MAP-ONLY |
| `CLOSE` | Close | `supplychain.returns.close` | RowAction | no | MAP-ONLY |

QuickView is navigation (row summary), not an action. **Gaps:** (a) D5, MAP-ONLY keys. (b) Every row action also needs
`supplychain.returns.transition` **and** (UI prerequisite G-SHIPREAD) `supplychain.shipments.read`; `CREATE` also needs
`supplychain.shipments.read`. The manifest action has a single `PermissionKey`, so these conjunctions are **not representable** in the catalog;
the target key is the one declared. This is recorded, not worked around. (c) `ReturnPermissions.cs` is absent from the common checkout.

## 5. MOD-0187 Claims Management — `claims-management`

| Field | Value |
|---|---|
| ModuleCode / ModuleName / DisplayName | `claims-management` / `ClaimsManagement` / `Claims Management` |
| SortOrder / Icon (SOFT, proposed) | 430 / `bx-receipt` |
| UI source | **APPROVED-NOT-BUILT**: same owner decision + `mvp6-ui-pack-drafts-01/claims/`; pack UI revision pending in `mvp6-ui-pack-revisions-01/claims/` |
| Permission source | `ClaimPermissions.cs` in accepted isolated source (content read from `normal-source.tar.gz` `edb759a0…5a21`, file `03c69751…`): CONST-ISOLATED `Read`, `Create`, `Investigate`, `Decide`, `Settle` |

| PageCode | DisplayName | RoutePath | RequiredPermission | Parent | Nav | PageType | Sort |
|---|---|---|---|---|---|---|---|
| `CLAIMS` | Claims | `/SupplyChain/Claims` (approved, not a route yet) | `supplychain.claims.read` | — | **true** | List | 10 |

| ActionCode | DisplayName | PermissionKey | Placement | Dangerous |
|---|---|---|---|---|
| `CREATE` | Create Claim | `supplychain.claims.create` | Toolbar | no |
| `INVESTIGATE` | Investigate | `supplychain.claims.investigate` | RowAction | no |
| `WITHDRAW` | Withdraw | `supplychain.claims.investigate` | RowAction | **yes** |
| `APPROVE` | Approve | `supplychain.claims.decide` | RowAction | no |
| `REJECT` | Reject | `supplychain.claims.decide` | RowAction | **yes** |
| `SETTLE` | Settle (no payment posted) | `supplychain.claims.settle` | RowAction | no |
| `CLOSE` | Close | `supplychain.claims.decide` | RowAction | no |

**Gaps:** `CREATE` and every row action also need `supplychain.shipments.read` (G-SHIPREAD), which is not representable in a single-key action.
`ClaimPermissions.cs` is absent from the common checkout.

## 6. Excluded

MOD-0190 S&OP Workflow & Sign-offs and MOD-0192 Capacity Planning: `shell: none`, `draft`, no UI scope → no manifest now.
MOD-0147/0148 (other packs in the folder) are outside the MVP6 logistics scope of this lane.

## 7. Cross-module checks

- ModuleCodes unique across the five providers; no existing manifest uses them.
- Nav-visible PageCodes `SHIPMENTS`, `CARRIERS`, `LOADS`, `RETURNS`, `CLAIMS`: no existing provider uses them (checked 2026-09-26). Note that
  `Nav.Page.{PageCode}` keys are global across modules, so a later module reusing one of these codes would share its label.
- One nav-visible page per module, with `ParentPageCode: null`; every other page has a parent.
- Action display names above are English defaults (HARD in the manifest). They are not nav labels, so they need no `Nav.*` key.
