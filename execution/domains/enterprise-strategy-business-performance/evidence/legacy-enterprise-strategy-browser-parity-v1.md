# Enterprise Strategy Legacy Browser Parity Evidence v1

Status: **OPEN / USER-ACCEPTANCE-PENDING / RETIREMENT-BLOCKED**

Authority: [CAND-CAP-0007-FU01](../module-packs/CAND-CAP-0007-FU01-enterprise-strategy-security-tenancy-data-migration.md)
bounded `LEGACY-PARITY-EVIDENCE / GOVERNANCE-ONLY / NON-RUNTIME / NON-MIGRATING` amendment.

This evidence is a 14-page executable browser checklist. It does not replace the
[full static parity ledger](../legacy-enterprise-strategy-parity-ledger.md), whose 142 Initiative/Project
field rows remain authoritative evidence obligations. Legacy code is evidence, not canonical authority.
Route existence is not feature parity, browser PASS or user acceptance.

## Execution preconditions

1. Record `OLD_BASE`, `NEW_BASE` and both commit SHAs. The two bases must use different ports.
2. Sign in separately on both origins. Cookies, bearer tokens and tenant headers are not copied manually.
3. Record HTTP status, screenshot/network evidence, visible empty/null behavior and permission/tenant result.
4. Never fabricate a GUID, tenant, permission, record or browser route to make a row executable.
5. A data-dependent row stays `Pending` when authoritative test data or Mongo is unavailable.
6. Only the user may change `User acceptance` from `Pending` after side-by-side review.

## Page matrix

| # | Area | Exact old URL | Evidence-backed new URL | Field/action mapping | Disposition | Migration requirement | Browser evidence | User acceptance |
|---:|---|---|---|---|---|---|---|---|
| 1 | 1.3 Portfolio Management | `/management-governance/modules/mod-portfolio-management` | `/ppm/portfolios` | Registry placeholder is compared with MOD-0117 Portfolio list only; no placeholder field becomes business truth. | Replaced | No placeholder-data migration. | Pending: compare shell, list, permissions and empty state. | Pending |
| 2 | 1.3 Investment Governance | `/management-governance/modules/mod-investment-governance` | `/ppm/investment-cases` | Investment identity stays MOD-0117; approval truth remains a typed MOD-0023 reference and is never synthesized. | Replaced | No placeholder migration; approval reference mapping remains separate. | Pending: compare permissions, actions and approval-unavailable behavior. | Pending |
| 3 | 1.3 Benefits & Value Tracking | `/management-governance/modules/mod-benefits-value` | `/ppm/benefit-commitments` | Planned commitment stays MOD-0117; realized/measured Outcome stays MOD-0072. | Missing | Do not copy Outcome truth; field/ownership mapping remains open. | Pending: compare fields and explicit unavailable/empty states. | Pending |
| 4 | 1.3 Capital Allocation | `/management-governance/modules/mod-capital-allocation` | **Missing** | MOD-0136 Budgeting remains financial SoR; this repository contains only typed BudgetVersion reference evidence, not a Capital Allocation browser page. | Out of Scope | Typed financial reference mapping only; no local approval or copied Budgeting truth. | Pending: no approved new route exists. | Pending |
| 5 | 1.4 Initiatives list | `/management-governance/delivery-execution/initiatives` | `/ppm/initiatives` | Lifecycle → MOD-0117; strategy link → MOD-0352; person/org → MOD-0288; budget → MOD-0136. | Missing | Complete all 68 legacy field dispositions and quarantine rules before migration. | Pending: compare columns, filters, actions and null rendering. | Pending |
| 6 | 1.4 Initiative create/edit/detail | `/management-governance/delivery-execution/initiatives/new`; `/management-governance/delivery-execution/initiatives/{id}/edit`; `/management-governance/delivery-execution/initiatives/{id}` | `/ppm/initiatives` (list-owned create/edit/detail UI; no distinct new browser route evidenced) | Same owner split as row 5; no second Initiative lifecycle. | Missing | Exact create/update payload and rejected-field ledger required. | Pending: use only controls discoverable from the list; do not invent `/ppm/initiatives/new` or detail routes. | Pending |
| 7 | 1.4 Projects list | `/management-governance/delivery-execution/projects` | `/ppm/projects` | Lifecycle → MOD-0117; lineage → MOD-0352; approval → MOD-0023; budget → MOD-0136; evidence → typed shared reference. | Missing | Complete all 74 legacy field dispositions and quarantine rules before migration. | Pending: compare columns, filters, actions and null rendering. | Pending |
| 8 | 1.4 Project create/detail | `/management-governance/delivery-execution/projects/new`; `/management-governance/delivery-execution/projects/{id}` | Create: `/ppm/projects` (list-owned UI); detail: `/ppm/projects/{id:guid}` | Project lifecycle remains MOD-0117; foreign references remain typed and owner-specific. | Missing | Exact payload/state-transition mapping required. | Pending: create from list; open detail only with a real authoritative GUID. | Pending |
| 9 | 1.4 Programs | `/management-governance/delivery-execution/programs` | `/ppm/programs` | Legacy shell proves no authoritative aggregate; MOD-0117 owns the new Program lifecycle. | Replaced | None unless later legacy data evidence appears. | Pending: compare legacy shell and new list/empty state. | Pending |
| 10 | 1.4 Delivery Map / Dependencies | `/management-governance/delivery-execution/delivery-map` | `/management-governance/delivery-execution/structures` | Pure hierarchy/order/structural dependency → MOD-0354; task/status/due-date/owner → MOD-0024/MOD-0117; scheduling dependency semantics remain unresolved. | Missing | Quarantine ambiguous legacy rows; no blind migration. | Pending: route exists default-off; compare tree/validation/503 states, never task-like behavior. | Pending |
| 11 | 1.6 Process Architecture | `/management-governance/modules/mod-process-architecture` | `/management-governance/process-modeling/models` | Registry placeholder maps to MOD-0355 process-model catalog only; no legacy entity is assumed. | Replaced | None. | Pending: compare placeholder with list, permissions and default-off empty/503 state. | Pending |
| 12 | 1.6 Process Design & Modeling | `/management-governance/modules/mod-process-design` | List: `/management-governance/process-modeling/models`; editor: `/management-governance/process-modeling/models/{id:guid}` | MOD-0355 model/version/activity/control/topology contract is authoritative. | Replaced | None. | Pending: open editor only with a real model GUID; compare lifecycle and read-only published state. | Pending |
| 13 | 1.6 Process Performance | `/management-governance/modules/mod-process-performance` | **Missing** | MOD-0355 KPI Binding is dependency-gated; exact MOD-0059/MOD-0060 reference contracts are absent and successful handling remains 503/zero-residue. | Missing | No placeholder migration; reference contracts, host/API and browser slice required. | Pending: no approved new browser route exists. | Pending |
| 14 | 1.6 Process Improvement | `/management-governance/modules/mod-process-improvement` | **Missing** | MOD-0355 owns bounded Opportunity/Outcome contracts and persistence, but they are not host- or browser-reachable. | Missing | No placeholder migration; host/API and browser slice required. | Pending: no approved new browser route exists. | Pending |

## Route and source evidence

- PPM browser resource allowlist, list route and Project workspace:
  [`PpmController.cs`](../../../../frontend/Diten.Web/Controllers/PPM/PpmController.cs) lines 9–55.
- Legacy Delivery Execution routes:
  [`DeliveryExecutionManagementController.cs`](../../../../frontend/Diten.Web/Controllers/DeliveryExecutionManagementController.cs)
  lines 8–116.
- MOD-0354 structural browser route:
  [`DwsStructuresFrontendController.cs`](../../../../frontend/Diten.Web/Controllers/DwsStructuresFrontendController.cs)
  lines 8–52.
- MOD-0355 model list/editor routes:
  [`ProcessModelingFrontendController.cs`](../../../../frontend/Diten.Web/Controllers/ProcessModelingFrontendController.cs)
  lines 8–60.
- Legacy module placeholders:
  [`ManagementGovernanceRegistry.cs`](../../../../frontend/Diten.Web/Config/ManagementGovernanceRegistry.cs)
  lines 108–111 and 171–174. These rows are navigation metadata, not implementation evidence.

## Missing-surface ownership and authority audit

| Surface | Exact owner and current executable evidence | Route/source/test slice | Existing implementation authority | Result |
|---|---|---|---|---|
| Capital Allocation | MOD-0136 Budgeting is financial SoR. Current materialization has no `Diten.FpaService`, MOD-0136 pack or Capital Allocation controller/page; only MOD-0117 Gate I-B typed BudgetVersion reference and Auth/Eventing contracts are present. | Proposed future owner slice must name the materialized MOD-0136 pack, FPA API/controller, tenant UI and tests. No exact route/source/test path can be inferred here. | **No.** Current FU01/MOD-0117 authority permits typed references, not a Capital Allocation UI/API. | Fail closed; separate materialization and bounded frontend/API amendment required. |
| Delivery Map / Dependencies | MOD-0354 owns pure structure/hierarchy/order/dependency. Task/status/due-date/owner remain MOD-0024/MOD-0117; FS/SS/FF/SF scheduling ownership is unresolved. | Existing route `/management-governance/delivery-execution/structures`; source `DwsStructuresFrontendController.cs`, `Models/ManagementGovernance/Dws/**`, `Services/ManagementGovernance/Dws/**`, `Views/ManagementGovernance/Dws/**`; tests `mg-dws-frontend-contract.test.js`, `mg-dws-legacy-parity.test.js`; owner ledger `execution/domains/management-governance/evidence/MOD-0354-legacy-parity.md`. | **Yes, bounded/default-off/non-production.** MOD-0354 §19 exact frontend allowlist is implemented, but Gateway, populated browser evidence and user acceptance remain open. | Route is not Missing; **parity remains Missing** until browser/user evidence and ambiguous dependency disposition close. |
| Process Performance | MOD-0355 owns the bounded KPI-binding contract, but the exact MOD-0059/MOD-0060 metric/calculation reference contracts are absent. `ProcessKpiBindingDependencyGate` returns 503 and no KPI entity/collection/success handler is authorized. | Existing source/test authority is only `Modules/ProcessModeling/PerformanceImprovement/**`; no controller, DI, host route or frontend path is authorized for this subpath. | **No successful executable/browser authority.** Only dependency-gate contracts/tests are authorized. | Separate producer-contract closure, host/API amendment, frontend allowlist and tests required. |
| Process Improvement | MOD-0355 owns bounded Opportunity/Outcome Domain/Application/Persistence and tests under `Modules/ProcessModeling/PerformanceImprovement/**`. | Existing tests: `PerformanceImprovementContractTests.cs`, `PerformanceImprovementFixtureTests.cs`, `PerformanceImprovementMongoTests.cs`, `PerformanceImprovementArchitectureTests.cs`; no controller/DI/browser route exists. | **Partial only.** Contract/persistence implementation is authorized and present, explicitly not composed into a host. | Separate local API/DI/controller amendment plus tenant frontend route/resources/tests required before browser evidence. |

## Mongo-free same-origin security checklist

| Check | Can run without Mongo? | Expected result | Evidence state |
|---|---:|---|---|
| Anonymous request to `[Authorize]` PPM or Process Modeling page | Yes | Login redirect or 401 before backend read | Pending browser run |
| Authenticated user without exact `ppm.{resource}.read` | Yes | 403 before PPM proxy call | Pending browser run |
| Authenticated user without `management-governance.process-modeling.models.read` | Yes | 403 before Process Modeling proxy call | Pending browser run |
| Unknown PPM resource | Yes | 404 from the closed resource allowlist | Pending browser run |
| Header/JWT tenant mismatch | Only if the exact tenant-security middleware is on the tested path | 400 with zero business reads | Pending; static route evidence is insufficient |
| Foreign/missing tenant-owned object | No | Non-disclosing 404 requires authoritative persistence fixture | Blocked without approved data fixture |
| Own-tenant populated page or real model editor | No | 200 plus real data/rendering evidence | Blocked without approved data fixture |

## Browser evidence record

Repeat for every row:

```text
Row:
OLD_BASE / commit:
NEW_BASE / commit:
Old HTTP status / screenshot / network reference:
New HTTP status / screenshot / network reference:
Fields, actions and lifecycle differences:
Empty/null behavior:
Permission and tenant behavior:
Disposition confirmed: Same | Replaced | Missing | Intentionally Removed | Out of Scope
Migration requirement confirmed:
User decision: Pending
User/date: Pending
```

## Gate result

- Page rows: **14/14 documented**.
- Route-backed candidate target: **11/14**.
- No approved new browser route: **3/14** — Capital Allocation, Process Performance, Process Improvement.
- Browser accepted: **0/14**.
- User accepted: **0/14; all Pending**.
- Legacy retirement, delete, redirect and `full parity complete`: **BLOCKED**.
