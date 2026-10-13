# MOD-0190 S&OP Workflow & Sign-offs — tenant UI scope DRAFT (Q78)

2026-09-26 · Documents only. **This draft authorizes nothing:** no pack edit, UI code, gateway, shared registration or
dispatch. Measured on `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. Format follows the
Returns/Claims precedent `docs/roadmap/plans/mvp6-ui-pack-drafts-01/` (approved as scope in
`docs/records/decisions/2026-09/mvp6-returns-claims-ui-scope-owner-decision-01.md`).

## 1. Identity and inputs

| Item | Value |
|---|---|
| Kind | **UI revision inside the existing MOD-0190 pack** (no new ID). Pack §11 (lines 146–149) requires "a revised/follow-up pack defining tenant shell, exact form-field count, localization, authorization surface and Slim/Compact golden reference". The UI adds no object or operation. |
| Pack | `execution/domains/supply-chain-execution/module-packs/MOD-0190-sop-workflow-signoffs.md` sha256 `56fb8e7d…ac41`, `status: ready-for-dev` (Q27 + Q75 labels) |
| Contract | `docs/analysis/contracts/sandop-capacity.openapi.yaml` sha256 `5213b535…adab` (3.0.0). The six S&OP operations are unchanged since 2.0.0; the MOD-0190 evidence stays bound to 2.0.0 (`9543e3f2…`) until a re-pin (pack §22 lines 268). **FINDING F190-PIN:** the pack UI revision must name the contract version it binds; this draft binds the operations by `operationId`, which is identical in both versions. |
| Accepted backend | `docs/records/audits/2026-09/mvp6-mod0190-test-oracle-rework-01/source.tar.gz` sha256 `8fa00d40…b745` (379 manifest `ae7ef59e…`). Controller `…Api/Features/SandopPlans/SandopPlansController.cs` (`e5d43935…`); permissions `…Infrastructure/Features/SandopPlans/SandopPermissions.cs` (`47eb6b51…`). |
| Identity check before the pack revision | `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0190 --name "S&OP Workflow & Sign-offs"` (pack line 20; not rerun here) |

## 2. Published operations (the only ones the UI may call)

| operationId | Method + path (contract line) | Controller (archive line) | Permission (constant) | UI use |
|---|---|---|---|---|
| `createSandopPlan` | POST `/sandop-plans` (30–32) | `Create`, line 9 | `supplychain.sandop-plans.create` (`SandopPermissions.Create`, line 3) | Create-plan offcanvas |
| `getSandopPlan` | GET `/sandop-plans/{sandopPlanId}` (83–85) | `Get`, line 11 | `supplychain.sandop-plans.read` (`Read`) | Plan workspace header; "Open plan" |
| `captureSandopSnapshot` | POST `/…/snapshots` (122–124) | `Capture`, line 13 | `supplychain.sandop-plans.snapshot.capture` (`Capture`) | Capture-snapshot offcanvas |
| `listSandopSnapshots` | GET `/…/snapshots` (188–190) | `ListSnapshots`, line 15 | `…read` | Snapshots table (client-side) |
| `recordSandopSignOff` | POST `/…/sign-offs` (239–241) | `SignOff`, line 17 | `supplychain.sandop-plans.sign-off.record` (`SignOff`) | Record-sign-off offcanvas |
| `listSandopSignOffs` | GET `/…/sign-offs` (292–294) | `ListSignOffs`, line 19 | `…read` | Sign-offs table (client-side) |

Pack cross-reference: §3 Owned Objects (lines 47–58), §14 Authorization (lines 175–185).

**FINDING F190-LIST (main):** there is **no list-plans query** in the contract, the pack (§3: "Queries | Get plan, list
snapshots, list sign-offs") or the controller. A standard DataTable page over S&OP plans is therefore not possible
without a new, versioned contract operation (pack §5 lines 101–102: contract changes need a separate versioned
single-writer specification task). Option A in `DECISION-TEXT.md` works without it; option B adds it first.

**FINDING F190-DEMAND:** the demand reference (`demandPlanId`, `demandPlanVersion`, and for snapshots
`sourceCapturedAt`, `sourceChecksum`) has no lookup source — DEMAND v1 exposes no ID+version endpoint and this slice
validates against exact test fixtures only (pack §7 line 119, §12 line 159). The UI can only offer free-text inputs;
real use needs live DEMAND (CAP-LIVE, excluded).

## 3. Screens (option A, recommended — see DECISION-TEXT.md)

Tenant shell, area `Views/SupplyChain/SandopPlans/`. Browser → same-origin MVC adapter `/SupplyChain/SandopPlans/api/…`
→ Gateway → SupplyChain 5061 (`proxy-profile`, as decided for Shipment in PC-28). The browser never calls the Gateway
or 5061 and never holds a token.

| Screen | Route (proposed) | Content | Display gate |
|---|---|---|---|
| Plans entry | GET `/SupplyChain/SandopPlans` | "Create plan" button; "Open plan" by plan ID (UUID input). **No plan list** (F190-LIST). | `…read` |
| Create plan (offcanvas on entry page) | POST adapter `/SupplyChain/SandopPlans/api` | 5 fields (§4); on 201 navigates to the workspace of the returned `sandopPlanId` | `…create` |
| Plan workspace | GET `/SupplyChain/SandopPlans/Details/{sandopPlanId:guid}` | Header from `getSandopPlan` (name, horizon, demand reference, status, current snapshot, created); tab/section **Snapshots** (table over `listSandopSnapshots`: captured at, provenance, supply input refs count; current snapshot marked); section **Sign-offs** (table over `listSandopSignOffs`: snapshot, role, decision, comment, decided by, decided at) | `…read` |
| Capture snapshot (offcanvas in workspace) | POST adapter `/…/api/{id}/snapshots` | 4 fields + supply-input-reference repeater (§4); shown only when status is Draft or InReview (pack §13 line 169) | `…snapshot.capture` |
| Record sign-off (offcanvas in workspace) | POST adapter `/…/api/{id}/sign-offs` | snapshot (select from the listed snapshots), role, decision, comment; shown only when status is InReview (pack §13 line 170) | `…sign-off.record` |

Status is never changed by the UI: five approvals do not advance the plan (pack §16 line 197, §19 line 233). The UI
shows the server status as returned.

## 4. Fields (from the published request schemas; nothing added)

| Form | Fields (contract schema, line) | Required |
|---|---|---|
| Create plan — `CreateSandopPlanRequest` (705) | `name` (text, minLength 1, no trim), `horizonStart` (date), `horizonEnd` (date, ≥ start), `demandPlanId` (text), `demandPlanVersion` (text, minLength 1) | all 5 |
| Capture snapshot — `CaptureSandopSnapshotRequest` (764) | `demandPlanId`, `demandPlanVersion`, `sourceCapturedAt` (UTC date-time), `sourceChecksum`; repeater `supplyInputRefs[]` = `ExternalInputReference` (812): `source`, `resourceId`, `resourceVersion` | 4 scalars; repeater optional (default `[]`), each row all 3 |
| Record sign-off — `RecordSignOffRequest` (850) | `snapshotId` (UUID, chosen from list), `role` (enum DemandPlanning / SupplyPlanning / Finance / Operations / Executive), `decision` (Approved / Rejected), `comment` (optional, maxLength 2000) | 3 of 4 |

`form_field_count: 5` (create form) → **GoldenReferenceSlim** by add-module row 6 (≤ 8). Client-side checks mirror the
contract only (required, date order, enum, length); no tightening (pack §8 line 129, §12).

Headers set by the adapter: `X-Correlation-Id` (fresh UUID per request), `Idempotency-Key` (browser per-intent key,
forwarded exactly; pack §12 line 155), `Authorization` from the server session only; tenant/legal entity never from
the browser (pack §2 line 45).

## 5. Errors shown (published codes; message by code, no retries invented)

400 `INVALID_REQUEST` / `INVALID_CORRELATION_ID`; 403; 404 `UNKNOWN_SANDOP_PLAN` (also for foreign scope — no leak);
409 `SANDOP_PLAN_ALREADY_EXISTS`, `SANDOP_PLAN_STATE_CONFLICT`, `SANDOP_SIGN_OFF_STATE_CONFLICT`,
`SIGN_OFF_ALREADY_RECORDED`, `IDEMPOTENCY_KEY_REUSED`; 422 `INVALID_DEMAND_REFERENCE`, `INVALID_SNAPSHOT_REFERENCE`;
503 `DEPENDENCY_UNAVAILABLE`, `COMMIT_RESULT_UNRESOLVED` (the same intent may be retried with the same key). Pack §13
lines 164–173.

## 6. OUT (explicitly not in scope)

| # | Item | Reason |
|---|---|---|
| O-01 | Plan list / search / paging / filters | No list operation (F190-LIST) |
| O-02 | Edit plan, delete, archive, bulk actions | No such operations; status is server-owned |
| O-03 | Approve/Reject/Archive the plan (status change) | No transition operation; sign-offs do not change status (pack §19 line 233) |
| O-04 | Overwrite or delete a snapshot or sign-off | Append-only evidence (pack §8 line 130) |
| O-05 | DEMAND lookup, forecast series display, demand quantities | DEMAND is not copied; fixture-only (pack §2 line 44, F190-DEMAND) |
| O-06 | Workflow tasks, notifications, Event Bus, live producers | Excluded (pack §7 lines 121–122) |
| O-07 | Import/export, column visibility, saved views, QuickView of foreign data | No approved surface |
| O-08 | Capacity (MOD-0192) data in this UI | Disjoint peer (pack §7 line 123) |

## 7. Localization (7 tenant languages later: en, tr, fr, es, zh, ar, ru with RTL)

Proposed resource keys (marker class `SandopPlansIndex`/`SandopPlanDetails`; values not written here):
`SandopPlansTitle`, `SandopPlansPageDescription`, `CreatePlan`, `OpenPlan`, `PlanId`, `PlanName`, `HorizonStart`,
`HorizonEnd`, `DemandPlanId`, `DemandPlanVersion`, `Status`, `Status.Draft`, `Status.InReview`, `Status.Approved`,
`Status.Rejected`, `Status.Archived`, `CurrentSnapshot`, `CreatedAt`, `Snapshots`, `CaptureSnapshot`,
`SourceCapturedAt`, `SourceChecksum`, `SupplyInputRefs`, `Source`, `ResourceId`, `ResourceVersion`, `AddRow`,
`RemoveRow`, `CapturedAt`, `SignOffs`, `RecordSignOff`, `Snapshot`, `Role`, `Role.DemandPlanning`,
`Role.SupplyPlanning`, `Role.Finance`, `Role.Operations`, `Role.Executive`, `Decision`, `Decision.Approved`,
`Decision.Rejected`, `Comment`, `DecidedBy`, `DecidedAt`, one key per error code in §5. Common words
(Save, Cancel, Close) come from `SharedResource`.

## 8. Self-registration outline (SR-D4: overlay built with this UI, applied only at final integration)

ModuleCode `sop-workflow-signoffs` (pack slug), Domain `SupplyChainExecution`, Service `DitenSupplyChainService`,
Tenant scope (routes start `/SupplyChain/`). DCP-009 §21.1 lists MOD-0190 as excluded "no UI scope"; an approved UI
scope lifts that, through a later pack/DCP patch.

| PageCode | RoutePath | RequiredPermission | Nav | PageType |
|---|---|---|---|---|
| `SANDOP_PLANS` | `/SupplyChain/SandopPlans` | `supplychain.sandop-plans.read` | true | List (entry page; no table — F190-LIST) |
| `SANDOP_PLAN_DETAILS` | `/SupplyChain/SandopPlans/Details/{sandopPlanId:guid}` | `…read` | false | Detail |

| Page | ActionCode | PermissionKey | Dangerous |
|---|---|---|---|
| `SANDOP_PLANS` | `CREATE` | `supplychain.sandop-plans.create` | no |
| `SANDOP_PLAN_DETAILS` | `CAPTURE_SNAPSHOT` | `supplychain.sandop-plans.snapshot.capture` | no |
| `SANDOP_PLAN_DETAILS` | `RECORD_SIGN_OFF` | `supplychain.sandop-plans.sign-off.record` | no (decision is final; confirm dialog) |

Nav keys (7 languages): `Nav.Module.SOPWORKFLOWSIGNOFFS`, `Nav.Page.SANDOP_PLANS`. Provider, nav keys and completeness
tests are built in the isolated environment with the UI and applied to shared files only by the integration owner.

## 9. Open points

1. F190-LIST — accept a UI without plan browsing (A) or add a versioned list operation first (B).
2. F190-DEMAND — free-text demand references are usable only with fixture values until live DEMAND.
3. F190-PIN — contract version the UI revision binds (2.0.0 as accepted vs 3.0.0 canonical).
4. F190-ENT — backend entity base differs from the pack (see PH15-UI-190.md row 4); recorded only.
5. Sign-off `decidedBy` is a UUID; showing a person's name needs a user lookup that is not in scope (shown as ID).
