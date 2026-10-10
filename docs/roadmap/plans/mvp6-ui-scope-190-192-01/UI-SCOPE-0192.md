# MOD-0192 Capacity Planning — tenant UI scope DRAFT (Q78)

2026-09-26 · Documents only. **This draft authorizes nothing:** no pack edit, UI code, gateway, shared registration or
dispatch. Measured on `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. Format follows the
Returns/Claims precedent (`docs/roadmap/plans/mvp6-ui-pack-drafts-01/`).

## 1. Identity and inputs

| Item | Value |
|---|---|
| Kind | **UI revision inside the existing MOD-0192 pack** (no new ID). Pack §11 (lines 145–148) requires a revised/follow-up pack for any UI. No object or operation is added. |
| Pack | `execution/domains/supply-chain-execution/module-packs/MOD-0192-capacity-planning.md` sha256 `9b8b90f1…f813`, `status: ready-for-dev` (Q28 + Q75 labels) |
| Contract | `docs/analysis/contracts/sandop-capacity.openapi.yaml` sha256 `5213b535…adab`, published 3.0.0 / wire v1 (pack §22; 3.0.0 changed only the `createCapacityScenario` 409 for name conflicts) |
| Accepted backend | `docs/records/audits/2026-09/mvp6-bc-successor-exec-02/BC-SOURCE.tar.gz` sha256 `ebd5d80c…7064` (manifest `dec28b6a…`). Controller `…Api/Features/CapacityPlans/CapacityPlansController.cs` (`c710d361…`); permissions `…Infrastructure/Features/CapacityPlans/CapacityPermissions.cs` (`7dd343d0…`). |
| Identity check before the pack revision | `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0192 --name "Capacity Planning"` (pack line 20; not rerun here) |

## 2. Published operations (the only ones the UI may call)

| operationId | Method + path (contract line) | Controller (archive line) | Permission (constant, line) | UI use |
|---|---|---|---|---|
| `createCapacityPlan` | POST `/capacity-plans` (340–342) | `Create`, line 15 | `supplychain.capacity-plans.create` (`Create`, 5) | Create-plan offcanvas |
| `getCapacityPlan` | GET `/capacity-plans/{capacityPlanId}` (399–401) | `GetPlan`, line 18 | `supplychain.capacity-plans.read` (`Read`, 4) | Plan workspace header; "Open plan" |
| `createCapacityScenario` | POST `/…/scenarios` (442–444) | `CreateScenario`, line 21 | `supplychain.capacity-plans.scenario.create` (`ScenarioCreate`, 6) | Create-scenario offcanvas |
| `getCapacityScenario` | GET `/…/scenarios/{scenarioId}` (508–510) | `GetScenario`, line 24 | `…read` | Scenario panel; "Open scenario" |
| `evaluateCapacityScenario` | POST `/…/scenarios/{scenarioId}/evaluations` (546–548), **202** | `Evaluate`, line 27 | `supplychain.capacity-plans.evaluate` (`Evaluate`, 7) | Evaluate offcanvas |
| `getCapacityEvaluation` | GET `/…/evaluations/{evaluationId}` (599–601) | `GetEvaluation`, line 30 | `…read` | Evaluation panel (status + bottlenecks) |

Pack cross-reference: §3 Owned Objects (lines 46–57), §14 Authorization (lines 174–184).

**FINDING F192-LIST (main):** the contract has **no list operation at all** — no plan list, no scenario list for a
plan, no evaluation list for a scenario (pack §3 line 54: "Queries | Get capacity plan, get scenario, get evaluation").
`CapacityPlan` (contract 934) does not embed its scenarios. Without a new versioned operation (pack §5 lines 98–99), a
user can reach a plan, scenario or evaluation only by its ID — the IDs returned after create/evaluate or pasted in.
This is a stronger limitation than in S&OP.

**FINDING F192-DEMAND:** plan provenance (`demandPlanId`, `demandPlanVersion`, `sourceCapturedAt`, `sourceChecksum`) and
constraint references have no lookup source; this slice validates exact test fixtures only (pack §2 line 41, §12
lines 158–159). The UI can offer free text only.

## 3. Screens (option A, recommended — see DECISION-TEXT.md)

Tenant shell, area `Views/SupplyChain/CapacityPlans/`; same-origin MVC adapter `/SupplyChain/CapacityPlans/api/…`
(`proxy-profile`); the browser never calls the Gateway or 5061.

| Screen | Route (proposed) | Content | Display gate |
|---|---|---|---|
| Plans entry | GET `/SupplyChain/CapacityPlans` | "Create plan"; "Open plan" by plan ID. **No list** (F192-LIST). | `…read` |
| Create plan (offcanvas) | POST adapter `/…/api` | 7 fields (§4); on 201 opens the workspace of the returned `capacityPlanId` | `…create` |
| Plan workspace | GET `/SupplyChain/CapacityPlans/Details/{capacityPlanId:guid}` | Header from `getCapacityPlan` (name, horizon, provenance, status, created); "Create scenario"; "Open scenario" by scenario ID | `…read` |
| Create scenario (offcanvas) | POST adapter `/…/api/{planId}/scenarios` | name + two repeaters (§4); on 201 shows the returned scenario | `…scenario.create` |
| Scenario panel (in workspace) | GET adapter `/…/api/{planId}/scenarios/{scenarioId}` | name, status, constraint refs, adjustments; "Evaluate" | `…read` |
| Evaluate (offcanvas) | POST adapter `/…/api/{planId}/scenarios/{scenarioId}/evaluations` | `evaluationMode`, `resourceRefs[]` (§4); on 202 opens the evaluation panel | `…evaluate` |
| Evaluation panel | GET adapter `/…/api/{planId}/evaluations/{evaluationId}` | mode, status (Accepted / Running / Completed / Failed), submitted/completed times, bottleneck table (resource, period, required, available, shortfall, UoM — decimal strings shown as returned); manual "Refresh" while Accepted/Running | `…read` |

**FINDING F192-POLL:** automatic polling is not specified anywhere; this draft proposes a manual Refresh button only
(each click = one `getCapacityEvaluation`). Automatic polling would be an owner choice.

## 4. Fields (published request schemas; nothing added)

| Form | Fields (contract schema, line) | Required |
|---|---|---|
| Create plan — `CreateCapacityPlanRequest` (904) | `name` (minLength 1, no trim), `horizonStart`, `horizonEnd` (≥ start), `demandPlanId`, `demandPlanVersion`, `sourceCapturedAt` (UTC date-time), `sourceChecksum` | all 7 |
| Create scenario — `CreateCapacityScenarioRequest` (991) | `name`; repeater `constraintRefs[]` = `ConstraintReference` (967): `constraintId`, `source`, `sourceVersion`; repeater `adjustments[]` = `CapacityAdjustment` (978): `resourceRef`, `period`, `availableCapacityDelta` (Decimal string `^-?\d+(\.\d+)?$`, 702), `uomId` | `name`, both arrays (may be empty per schema; each row all fields) |
| Evaluate — `EvaluateCapacityScenarioRequest` (1043) | `evaluationMode` (Finite / Infinite), `resourceRefs[]` (text list, minItems 1) | both |

`form_field_count: 7` (create-plan form) → **GoldenReferenceSlim** (≤ 8, add-module row 6). Decimal values are
entered and shown as strings, never converted to floating point. Headers as in S&OP (fresh correlation UUID,
per-intent `Idempotency-Key` forwarded exactly; pack §12 lines 154–155); tenant/legal entity never from the browser
(pack §2 line 44).

## 5. Errors shown (published codes)

400 `INVALID_REQUEST` / `INVALID_CORRELATION_ID`; 403; 404 `UNKNOWN_CAPACITY_PLAN`, `UNKNOWN_CAPACITY_SCENARIO`,
`UNKNOWN_CAPACITY_EVALUATION`; 409 `CAPACITY_PLAN_ALREADY_EXISTS`, `CAPACITY_PLAN_STATE_CONFLICT`,
`CAPACITY_SCENARIO_NAME_CONFLICT`, `EVALUATION_ALREADY_ACTIVE`, `IDEMPOTENCY_KEY_REUSED`; 422
`INVALID_DEMAND_REFERENCE`, `INVALID_CONSTRAINT_REFERENCE`; 503 `DEPENDENCY_UNAVAILABLE`, `COMMIT_RESULT_UNRESOLVED`.
Pack §13 lines 163–172.

## 6. OUT (explicitly not in scope)

| # | Item | Reason |
|---|---|---|
| O-01 | Plan / scenario / evaluation lists, search, paging | No list operations (F192-LIST) |
| O-02 | Edit or delete plan/scenario; approve/archive plan | No such operations; plan status is `Draft` only in this slice (pack §4 line 70) |
| O-03 | Cancel or rerun an evaluation other than a new submit | No operation; one active evaluation per scenario (pack §4 line 87, §13 line 170) |
| O-04 | Charts, optimizer, what-if calculation in the browser | Results are the backend's fixture-literal output only (pack §16 lines 197–198) |
| O-05 | DEMAND / constraint lookups, demand quantities | Fixture-only; no copy of DEMAND data (F192-DEMAND) |
| O-06 | Event Bus, publisher, notifications, live producers | Excluded (pack §2 line 41) |
| O-07 | Import/export, column visibility, saved views | No approved surface |
| O-08 | S&OP (MOD-0190) data in this UI | Disjoint peer |

## 7. Localization (7 tenant languages later, with RTL)

Proposed keys (marker `CapacityPlansIndex` / `CapacityPlanDetails`): `CapacityPlansTitle`, `CapacityPlansPageDescription`,
`CreatePlan`, `OpenPlan`, `PlanId`, `PlanName`, `HorizonStart`, `HorizonEnd`, `DemandPlanId`, `DemandPlanVersion`,
`SourceCapturedAt`, `SourceChecksum`, `Status`, `PlanStatus.Draft|Evaluating|Ready|Approved|Archived`, `Scenarios`,
`CreateScenario`, `OpenScenario`, `ScenarioId`, `ScenarioName`, `ScenarioStatus.Draft|Evaluating|Evaluated|Archived`,
`ConstraintRefs`, `ConstraintId`, `Source`, `SourceVersion`, `Adjustments`, `ResourceRef`, `Period`,
`AvailableCapacityDelta`, `UomId`, `AddRow`, `RemoveRow`, `Evaluate`, `EvaluationMode`, `EvaluationMode.Finite`,
`EvaluationMode.Infinite`, `ResourceRefs`, `EvaluationStatus.Accepted|Running|Completed|Failed`, `SubmittedAt`,
`CompletedAt`, `Bottlenecks`, `RequiredCapacity`, `AvailableCapacity`, `Shortfall`, `Refresh`, one key per error code in §5.

## 8. Self-registration outline (SR-D4 overlay)

ModuleCode `capacity-planning`, Domain `SupplyChainExecution`, Service `DitenSupplyChainService`, Tenant scope.
DCP-009 §21.1 excludes MOD-0192 for "no UI scope" until an approved UI scope and a later pack/DCP patch.

| PageCode | RoutePath | RequiredPermission | Nav | PageType |
|---|---|---|---|---|
| `CAPACITY_PLANS` | `/SupplyChain/CapacityPlans` | `supplychain.capacity-plans.read` | true | List (entry page; no table — F192-LIST) |
| `CAPACITY_PLAN_DETAILS` | `/SupplyChain/CapacityPlans/Details/{capacityPlanId:guid}` | `…read` | false | Detail |

| Page | ActionCode | PermissionKey | Dangerous |
|---|---|---|---|
| `CAPACITY_PLANS` | `CREATE` | `supplychain.capacity-plans.create` | no |
| `CAPACITY_PLAN_DETAILS` | `CREATE_SCENARIO` | `supplychain.capacity-plans.scenario.create` | no |
| `CAPACITY_PLAN_DETAILS` | `EVALUATE` | `supplychain.capacity-plans.evaluate` | no |

Nav keys: `Nav.Module.CAPACITYPLANNING`, `Nav.Page.CAPACITY_PLANS`.

## 9. Open points

1. F192-LIST — accept ID-only navigation (A) or add versioned list operations first (B).
2. F192-DEMAND — provenance and constraint references are free text, usable with fixture values only.
3. F192-POLL — manual Refresh only, unless the owner wants automatic polling.
4. Plan status stays `Draft` in this slice; the other enum values are shown only if the server returns them.
