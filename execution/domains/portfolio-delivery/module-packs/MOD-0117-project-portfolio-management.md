---
id: MOD-0117
name: Project & Portfolio Management (PPM)
domain: portfolio-delivery
service: Diten.PpmService
shell: tenant
golden_reference: slim
entity_base: EntityBase
status: review
owner: Portfolio Governance Process Owner / enterprise-architect
branch: feature/es/enterprise-strategy
started: 2026-07-29
target: phase-2a
form_field_count: 8
implementation_authorization: phase-2a
implementation_authorized_on: 2026-07-29
implementation_authority: explicit-user-control-tower
---

# MOD-0117 — Project & Portfolio Management (PPM)

> **SCOPED IMPLEMENTATION AUTHORITY — 2026-07-29:** The user explicitly approved `Diten.PpmService`
> scaffolding and MOD-0117 backend plus tenant frontend implementation for **Phase 2A — Context &
> Referenceability Core**. The pack is now in `review` on the strength of scoped Phase 2A
> implementation evidence. The separately approved ExternalContextReference provider slice is limited to
> the internal endpoint and contract recorded below; this does not authorize Phase 2B, DWS runtime integration, any Gateway change beyond
> the scoped PPM mappings, legacy migration or any WorkCenter hazard. Those boundaries remain fail-closed.

> **Current-main semantic integration note — 2026-08-29:** The PPM-owned backend scaffold and bounded Gate I
> contracts are now materialized on the current-main integration chain. Checkpoint `a22a872f` reconstructs the
> PPM-owned base service and Gate I-A/B/C contract-test foundation; parent-owned MOD-0018 governance checkpoint
> `457edbdd` and neutral shared request-binding checkpoint `92eb29ea` replace the historical colliding FU-named
> shared contract; checkpoint `8c659594` adds the PPM-owned, default-off relationship, receipt, audit and signed-outbox
> composition. The verified current-main result is build `0` warnings / `0` errors, unit `286/286`, dynamic-Mongo
> integration `82/82` with `0` skips, architecture `11/11`, and physical mutation evidence `6/6` with restore SHA-256
> `61e79023258a6086db98f52378a7c86bf611f309d71a83979f7368b056d68170`. This closes only the backend/default-off
> Gate I evidence named here. The pack remains `review`; MOD-0023 remains `ExcludedV1`, and full 1.3, browser,
> live-provider, bilateral, WorkCenter and production-activation gates remain open.

> **Composite-pack UI decision:** `form_field_count: 8` is the maximum create/edit field count across the
> four authorized Phase 2A surfaces, not a single composite form. All four select Golden Slim.

> **Initiative Core v2 amendment — 2026-09-02, GOVERNANCE-ONLY / NON-EXECUTABLE:** This amendment supersedes
> the 2026-09-01 six-field Initiative surface decision. Initiative create/edit now has exactly eight user
> fields and remains Golden Slim. It fixes lifecycle, vocabulary, closure, supersession, typed-link and
> WorkCenter boundaries below, but creates no runtime/frontend/service/Gateway/migration/seed/deployment or
> production-activation authority.

> **Initiative surface reconciliation — 2026-09-01, SUPERSEDED BY INITIATIVE CORE V2:** The legacy Enterprise Strategy
> Initiative wizard was audited field by field against the Blueprint and current repository code. It is not
> a PPM template: its persistent `InitiativeStrategyLinkAggregate` and several browser-only mock actions
> are neither a second PPM system of record nor implementation authority. At that checkpoint the Initiative
> surface remained six-field Golden Slim; the 2026-09-02 Initiative Core v2 amendment above supersedes that
> field contract. The historical amendment created no runtime code, entity field, DTO, API, Gateway
> route, reference-data set, external client, browser card, migration, activation or completion claim.

## 1. Module Summary

MOD-0117 is the Blueprint-canonical PPM parent and the future authoritative system of record for Portfolio,
Initiative, Program, Project and their controlled investment/value linkage. The permanent institutional
business owner is **Portfolio Governance Process Owner (PPM Business Owner)**. The Enterprise Architect owns
technical/governance consistency only and does not replace PPM business acceptance.

The authorized production placement is `services/Diten.PpmService/`; the Phase 2A scaffold and vertical
slice now exist and are under review. The current
Enterprise Strategy mock/resilient adapters, browser-held state, Delivery Execution frontend prototypes and
gateway routes targeting legacy ES port `5004` are code-reality evidence only. They are not a production
baseline, capability completion proof, data migration source of truth or implementation authority.

Delivery is staged through Phase 2A, Gate L and three OPEN Gate I contract slices:

- **Phase 2A — Context & Referenceability Core (authorized):** service scaffold; Portfolio, Initiative,
  Program and Project backend plus tenant UI; Mongo persistence; tenant isolation; soft delete; concurrency;
  lifecycle-derived referenceability; producer-local audit-intent/outbox foundation; and gateway-ready API
  contracts.
- **Phase 2B Gate L — Local Investment & Planned Benefit Core (implemented, isolated evidence):**
  `InvestmentCase` and `BenefitCommitment`, owned entirely by MOD-0117, were implemented at immutable
  checkpoint `536aa68556f165db45d9860444d3de39757b5e58`. Gate L contains no external-module field, DTO,
  client, projection or placeholder reference. This is the PPM-owned local-slice checkpoint, not production
  activation or full 1.3 completion.
- **Gate I-A — Decision Trace (`OPEN`, non-executable):** MOD-0007 governing/supporting decision references
  and conditional MOD-0023 authoritative ApprovalOutcome reference.
- **Gate I-B — Funding & Scenario (`OPEN`, non-executable):** MOD-0136 selected BudgetVersion plus MOD-0138
  scenario/comparator references.
- **Gate I-C — Benefit Realization (`OPEN`, non-executable):** MOD-0072 outcome/realization references.

The internal ExternalContextReference provider slice is implemented and tested in this branch at checkpoints
`eddabab05c7254b469155c803a4e444a841b4932` and
`682b0afbeaec2df184f20cc47b390707ac4f22d1`; it remains default-disabled and has no production activation.
MOD-0354 consumer implementation and DWS runtime consumption remain separate and blocked. Only Gate L + Gate I-A + Gate I-B +
Gate I-C + cross-service security/compatibility tests + integrated browser flow may support full 1.3
completion wording; even that cannot by itself mark the Blueprint-wide MOD-0117 product `done`.

## 2. Ownership and Boundaries

### Owned by MOD-0117

- Portfolio identity and owner-approved lifecycle.
- Initiative identity and owner-approved lifecycle.
- Program identity and owner-approved lifecycle.
- Project identity and owner-approved lifecycle.
- PPM actor visibility and referenceability decisions.
- InvestmentCase as the PPM-owned investment-planning/case record.
- BenefitCommitment as the PPM-owned planned-benefit commitment; it is not actual or realized value.
- Authoritative `ppm.external-context-reference` version `1.0` referenceability result.

### Cannot become a second PPM SoR

- `Diten.EnterpriseStrategyService`
- `Diten.Platform`
- `Diten.ManagementGovernanceService`
- MOD-0354
- frontend browser state, mock data or prototype pages

### Strict out of scope

- Demand implementation or Demand lifecycle.
- ES `TaskAggregate`.
- Generic task/checklist ownership; MOD-0024 owns it.
- PPM-local WorkCenter or WorkCenter lifecycle.
- Task assignment, progress, due date, execution dependency or scheduling.
- Workflow, approval, SLA or escalation engine; MOD-0023 owns these.
- DWS structure, node, structural dependency or baseline; MOD-0354 owns these.
- Budget, scenario, outcome or value SoR copies; MOD-0136/MOD-0138/MOD-0072 remain authoritative.
- Capacity/resource scheduling.
- Legacy mock/prototype data migration.
- Workstream, PPM Task/My Tasks, Calendar, Project Effort Log, Meeting/Status Report.
- Free-text external identity.
- New `Workflow*` route, permission, class, namespace, collection, UI label, pack or branch naming.

### WorkCenter Control Tower hazards

This pack cannot create or alter an ES TaskAggregate, add task lifecycle/assignment behavior to DWS nodes,
introduce Approve UI/action/route/command behavior, or project free-text Demand identities into WorkCenter.
This authoring task is governance-only and does not call Gate 2. If a later pack revision or implementation
touches one of those hazards, Claude WorkCenter Control Tower Gate 2 is mandatory before the first dangerous
production change.

### DCP-004 Gate-2 WorkCenter disposition — GOVERNANCE-ONLY / DEFAULT-OFF / NON-PRODUCTION

The Control Tower has completed the current-state Gate-2 disposition against the authoritative DCP-004
provider contract and current MOD-0117 code. **The current eligible MOD-0117 WorkCenter item-type count is
exactly `0`, and the current WorkCenter action map is empty.** This is a deliberate fail-closed result, not a
missing implementation claim: none of the six MOD-0117-owned business types currently has the authoritative
assignment, admission, normalized-lifecycle and due-date semantics required to project a truthful work item.
No lifecycle endpoint below is therefore a WorkCenter action endpoint.

| MOD-0117-owned type | Current disposition | Exact reason |
|---|---|---|
| `Portfolio` | `Unsupported` | Governance context only; no assignee, admission or deadline authority. |
| `Initiative` | `Unsupported` | Governance context only; no assignee, admission or deadline authority. |
| `Program` | `Unsupported` | Governance context only; no assignee, admission or deadline authority. |
| `Project` | `Unsupported` | Delivery context is not task ownership; no assignee, admission or deadline authority. |
| `InvestmentCase` | `ConditionalFutureCandidate` | Title, lifecycle, planning dates and technical `Version` exist, but assignee, admission and priority semantics require an explicit business-owner decision. |
| `BenefitCommitment` | `ConditionalFutureCandidate` | Title, lifecycle, target date and technical `Version` exist, but assignee, admission and priority semantics require an explicit business-owner decision. |

The required DCP-004 31-field projection inventory is fixed as follows. `Unsupported` means the field must be
omitted or the item must not be projected; it does not authorize a placeholder, inferred value or browser-only
fallback.

| # | DCP-004 projection field | Current MOD-0117 disposition |
|---:|---|---|
| 1 | `FixtureKind` | Shape-supported only as fixed `workItem` after a future type becomes eligible. |
| 2 | `Id` | Supported from the authoritative aggregate GUID. |
| 3 | `WorkIntent` | `Unsupported` and mandatory; no authoritative mapping exists. |
| 4 | `AssignmentMode` | `Unsupported` and mandatory; no authoritative mapping exists. |
| 5 | `OwnershipState` | `Unsupported` and mandatory; no authoritative mapping exists. |
| 6 | `AdmissionState` | `Unsupported` and mandatory; no authoritative mapping exists. |
| 7 | `NormalizedStatus` | `Unsupported` pending explicit per-type lifecycle normalization approval. |
| 8 | `TaskLifecycle` | `Unsupported`; blocked by the missing `WorkIntent` contract. |
| 9 | `ExecutionState` | `notApplicable`; MOD-0117 aggregates are not task execution records. |
| 10 | `TimerState` | `notApplicable`; MOD-0117 owns no task timer. |
| 11 | `SystemState` | Future projection may use `fresh` only from an authoritative current read; cache/LKG inference is forbidden. |
| 12 | `ActionDepth` | `inline` only while the action set is empty; no deep link is authorized. |
| 13 | `Title` | Supported from `Name` or `Title`; locale remains `und` until an authoritative localization source exists. |
| 14 | `NativeStatus` | Supported from the exact owner lifecycle value. |
| 15 | `Source` | `Unsupported` and mandatory until provider code/version identity is approved. |
| 16 | `LifecycleOwner` | `Unsupported` and mandatory until the owner mapping is approved. |
| 17 | `WorkItemCapabilities` | Exact empty set. |
| 18 | `Actions` | Exact empty set. |
| 19 | `Concurrency` | Technical `Version` can supply a future version token; no WorkCenter action consumer is authorized. |
| 20 | `WaitingContext` | Omitted; no authoritative waiting state exists. |
| 21 | `Escalation` | Omitted; MOD-0023 owns escalation semantics. |
| 22 | `DueAt` | Omitted; planning `DateTime`/`DateOnly` values are not an approved work deadline and timezone policy is open. |
| 23 | `PrimaryActionCode` | Omitted; the action map is empty. |
| 24 | `OverflowActionCodes` | Omitted; the action map is empty. |
| 25 | `Assignee` | `Unsupported` and blocking; `CreatedBy` is not assignment. |
| 26 | `Requester` | Omitted until a canonical person-reference contract is approved. |
| 27 | `Checklist` | Omitted; MOD-0024 owns checklist templates and state. |
| 28 | `Subtasks` | Omitted; MOD-0117 owns no task/subtask aggregate. |
| 29 | `ParentTaskItemId` | Omitted; PPM hierarchy is not task parentage. |
| 30 | `Gates` | Omitted; Gate-I references are not WorkCenter gates and MOD-0023 remains `ExcludedV1`. |
| 31 | `Priority` | Omitted; no authoritative MOD-0117 work-priority field exists. |

The observed owner lifecycle mutation surface remains ordinary MOD-0117 API evidence only:

| Type | Existing lifecycle behavior | Existing permission | WorkCenter action disposition |
|---|---|---|---|
| `Portfolio` | `Draft -> Active|Archived`; `Active -> Archived` | `ppm.portfolios.change-lifecycle` | None |
| `Initiative` | `Proposed -> Active|Cancelled`; `Active|OnHold ->` allowed owner transitions | `ppm.initiatives.change-lifecycle` | None |
| `Program` | `Draft -> Active|Cancelled`; `Active|OnHold ->` allowed owner transitions | `ppm.programs.change-lifecycle` | None |
| `Project` | `Draft -> Planned|Cancelled`; `Planned|Active|OnHold ->` allowed owner transitions | `ppm.projects.change-lifecycle` | None |
| `InvestmentCase` | `Draft -> UnderAnalysis|Withdrawn`; `UnderAnalysis -> Closed|Withdrawn` | `ppm.investment-cases.change-lifecycle` | None; enabled Gate-I close guard may require a governing-decision reference. |
| `BenefitCommitment` | `Draft -> Planned|Cancelled`; `Planned -> Active|Cancelled`; `Active -> Closed|Cancelled` | `ppm.benefit-commitments.change-lifecycle` | None |

The existing refusal classes remain module API semantics: entitlement/permission denial `403`, dependency
authority unavailable `503`, missing/cross-tenant `404`, and invalid/stale transition `409`. They are not a
DCP-004 stable WorkCenter reason-code dictionary. A later action map cannot be created until exact stable
reason codes are owner-approved and executable against the real module endpoints.

This amendment grants **no** provider class, projection/action endpoint, configuration row, Platform source,
frontend, Gateway, credential, secret, activation, deployment or migration authority and does not imply
WorkCenter readiness, browser readiness, parity or completion. A future bounded implementation amendment must
close all of these gates before any provider code is written:

1. choose the exact eligible owned type or types;
2. approve assignment and admission semantics for each chosen type;
3. approve exact native-to-normalized lifecycle mappings;
4. reserve one stable provider code;
5. bind the exact DCP-004 contract version;
6. authorize exact module-owned GET/POST paths without adding a Platform bridge class;
7. approve a stable refusal/reason-code matrix;
8. bind the exact action request payload and concurrency token;
9. decide `self|team` query semantics;
10. approve an exact source/test/config allowlist; and
11. hand-project and accept one real item end to end before implementation promotion.

Until all eleven gates close and Gate-2 is explicitly accepted, MOD-0117 remains default-off for WorkCenter,
projects zero items, exposes zero WorkCenter actions and has no remote-provider configuration row.

## 3. Owned Objects

| Object | Phase | Ownership purpose | Required owner decision before promotion |
|---|---|---|---|
| `Portfolio` | 2A | Portfolio identity and governance context | CLOSED for Phase 2A |
| `Initiative` | 2A | Finite initiative identity within PPM | CLOSED for Phase 2A |
| `Program` | 2A | Program identity and grouping context | CLOSED for Phase 2A |
| `Project` | 2A | Finite project identity within PPM | CLOSED for Phase 2A |
| `InvestmentCase` | 2B Gate L | PPM-owned investment planning/case record | CLOSED by the Gate L ownership, lifecycle and cardinality decision |
| `BenefitCommitment` | 2B Gate L | PPM-owned planned-benefit commitment | CLOSED by the Gate L ownership, lifecycle and cardinality decision |
| `ExternalContextReferenceValidationProjection` | Provider slice | Minimal authoritative typed-reference result | IMPLEMENTED / ISOLATED EVIDENCE; default-disabled, production activation and DWS consumption not authorized |

No generic `PpmContext` entity may hide all business semantics behind one discriminator. Shared typed value
objects may reduce duplication, but Portfolio, Initiative, Program and Project remain separate domain types,
repositories and business rules.

Command/query families are object-specific and cannot collapse into arbitrary
`CreatePpmContext`/`UpdatePpmContext` handlers. Phase 2A authorizes object-specific create/read/update,
controlled lifecycle transitions, soft delete and local derived-referenceability queries. The narrowly
authorized read-only external-context provider does not authorize any Phase 2B command.

### Phase 2A lifecycle and relationship decisions

- Defaults: Portfolio `Draft`; Initiative `Proposed`; Program `Draft`; Project `Draft`.
- Portfolio: `Draft → Active`; `Draft|Active → Archived`; `Archived` is terminal.
- Initiative: `Proposed → Active|Cancelled`; `Active → OnHold|Completed|Cancelled`;
  `OnHold → Active|Completed|Cancelled`; `Completed|Cancelled` are terminal.
- Program: `Draft → Active|Cancelled`; `Active → OnHold|Completed|Cancelled`;
  `OnHold → Active|Completed|Cancelled`; `Completed|Cancelled` are terminal.
- Project: `Draft → Planned|Cancelled`; `Planned → Active|OnHold|Cancelled`;
  `Active → OnHold|Completed|Cancelled`; `OnHold → Active|Completed|Cancelled`;
  `Completed|Cancelled` are terminal.
- Initiative has an optional Portfolio; Program has an optional Portfolio.
- Project has exactly one parent: Initiative XOR Program. Null, dual-parent, self and cyclic relationships
  are invalid; cross-tenant parent lookup returns 404.
- Referenceability is derived from tenant visibility, soft-delete and lifecycle. New references are allowed
  for Portfolio `Draft|Active`, Initiative `Proposed|Active|OnHold`, Program `Draft|Active|OnHold`, and
  Project `Draft|Planned|Active|OnHold`. Terminal states reject new references without rewriting sealed
  DWS history.

## 4. Entity Fields

### 4.1 Common planned `EntityBase` contract

This is the intended local `Diten.PpmService` base contract, not permission to copy or inherit a base from
Platform.Common or EnterpriseStrategyService.

| Field | CLR/BSON direction | Required | Rule |
|---|---|---:|---|
| `Id` | `Guid`, BSON subtype 4 | Yes | Server-generated; `Guid.Empty` forbidden |
| `TenantId` | `Guid`, BSON subtype 4 | Yes | Authenticated server context only; never client payload |
| `CreatedAtUtc` | scalar UTC `DateTime` | Yes | Server-generated UTC |
| `UpdatedAtUtc` | nullable scalar UTC `DateTime` | No | Server-generated UTC |
| `IsDeleted` | `bool` | Yes | Defaults false; all reads filter false |
| `DeletedAtUtc` | nullable scalar UTC `DateTime` | No | Required when soft-deleted |
| `Version` | `int` | Yes | Technical optimistic concurrency only; starts at 1 |
| `CreatedBy` | `Guid` | Yes | Authenticated actor; never client payload |
| `UpdatedBy` | nullable `Guid` | No | Authenticated actor on mutation |

Local/Unspecified timestamps must fail closed unless the service explicitly creates and normalizes the value
to UTC. Exact serializer registration and cold-start BSON round-trip evidence remain promotion blockers.

### 4.2 Portfolio — Phase 2A fields

The Phase 2A baseline below is preserved. Later first-delivery business decisions and future deltas are
separately recorded in the [2026-09-08 DRAFT / NON-EXECUTABLE amendment](#portfolio-first-delivery-draft);
they grant no code-start authority and do not assert that the budget/access gates already exist.

| Field | Type | Required | Phase 2A invariant |
|---|---|---:|---|
| `Code` | string | Yes | Trim + NFC; max 64; unique per active tenant |
| `Name` | string | Yes | Trim + NFC; non-empty; max 200 |
| `Description` | string? | No | Trim + NFC; empty becomes null; max 2000 |
| `LifecycleState` | closed string enum | Yes | `Draft`, `Active`, `Archived`; default `Draft` |
| `VisibilityPolicyKey` | string? | No | Max 128; non-null requires authoritative MOD-0018 validation; arbitrary ACL JSON forbidden |

Index direction: unique partial `(TenantId, Code)` for `IsDeleted=false`; list index starts with
`TenantId, IsDeleted`. Referenceability is derived from owner-approved lifecycle + actor visibility +
soft-delete, never from a stored standalone boolean.

### 4.3 Initiative — Phase 2A fields

| Field | Type | Required | Phase 2A invariant |
|---|---|---:|---|
| `Code` | string | Yes | Trim + NFC; max 64; unique per active tenant |
| `Name` | string | Yes | Trim + NFC; non-empty; max 200 |
| `Description` | string? | No | Trim + NFC; max 2000 |
| `PortfolioId` | Guid? | No | Same-tenant non-deleted Portfolio if supplied; optional-one cardinality |
| `InitiativeTypeCode` | string? | No for `Proposed`; yes before `Active` | Tenant-managed MOD-0048 option; supplied values require authoritative validation, unknown is `400`, unavailable/indeterminate provider is `503` |
| `PriorityCode` | string? | No for `Proposed`; yes before `Active` | Tenant-managed MOD-0048 option; supplied values require authoritative validation, unknown is `400`, unavailable/indeterminate provider is `503` |
| `PlannedStartDate` | `DateOnly?` | No for `Proposed`; yes before `Active` | Planning date; when both dates exist cannot be after `PlannedEndDate` |
| `PlannedEndDate` | `DateOnly?` | No for `Proposed`; yes before `Active` | Planning date; when both dates exist cannot precede `PlannedStartDate` |
| `LifecycleState` | closed string enum | Yes | Read-only/action-based: `Proposed`, `Active`, `OnHold`, `Completed`, `Cancelled`; default `Proposed` |
| `VisibilityPolicyKey` | string? | No | Max 128; non-null requires authoritative MOD-0018 validation |
| `SupersedesInitiativeId` | Guid? | No | Create-only server-validated reference to a same-tenant terminal Initiative; immutable after create; cycles forbidden |

Index direction: unique active `(TenantId, Code)`; relationship index
`(TenantId, PortfolioId, IsDeleted)` if the relationship is approved.

#### 4.3.1 Initiative cross-module detail registry — GOVERNANCE-ONLY / DEFAULT-UNAVAILABLE

The Initiative create/edit contract is superseded by the exact eight-field Initiative Core v2 contract in
§4.3.2. No legacy wizard field is copied
into an Initiative entity, DTO, command, API payload or local lookup merely because it appeared on a legacy
screen. `LifecycleState` remains an owner-controlled transition, not an ordinary metadata selector; a
non-null `VisibilityPolicyKey` remains unavailable for free-form UI entry until its authoritative
MOD-0018 validation path is promoted.

The following is a future **detail-card registry**, not authority to render a card now. If a later bounded
amendment authorizes a card, it must consume only the named owner contract and preserve these states:

| Future detail concept | Owner / authority state | PPM behavior until an approved contract exists |
|---|---|---|
| Strategy alignment (objective, goal, period, horizon) | MOD-0352; no Initiative reference contract | Unavailable; no objective/goal field, free-text alignment note or local strategy copy |
| Ownership and organization (org, person, position, sponsor) | MOD-0288; relationship semantics and cardinality open | Unavailable; no owner/sponsor text, person snapshot or organization collection |
| Planning context (phase, wave, cadence, readiness) | PPM candidate decisions are open; taxonomy/period semantics are not approved | Unavailable; no second status truth, hardcoded enum or inferred readiness; Core v2 priority and planning dates are governed separately below |
| Metric and contribution | Canonical owner requires reconciliation; legacy MOD-0004 evidence is not an executable MOD-0059/MOD-0060 contract | Unavailable; no metric text, unit, calculation or contribution-plan copy |
| Investment, funding, scenario and benefits | InvestmentCase/BenefitCommitment are separate MOD-0117 aggregates; MOD-0136/MOD-0138/MOD-0072 links remain Gate I boundaries | Unavailable unless a later exact Initiative cardinality and producer contract are approved; no amount, currency, scenario or realized-value copy |
| Decision and approval | MOD-0007 decision links remain Gate I-A; MOD-0023 is `ExcludedV1` | Unavailable; no approval state, decision note or inferred governing status |
| Evidence and documents | MOD-0028 document ownership and MOD-0031 evidence relationship ownership | Unavailable; no binary, document list, evidence count or local fallback |
| Structural dependencies | MOD-0354 | Unavailable; no boolean dependency flag, dependency graph or local DWS copy |

`available` is permitted only after the owner contract, same-tenant/referenceability rule and required
permission are all approved and executable. `unauthorized` is reserved for a same-tenant resource the actor
may know exists but cannot use. Missing, deleted, invisible and cross-tenant records remain indistinguishable
`404`; provider timeout, unavailable authority or malformed authoritative data remains `503` and must never
be collapsed to `404`. A future card must not show mock rows, synthetic zero counts, stale local cache,
free-text external identifiers or a browser-only fallback.

A later Initiative-card implementation requires a separate approved amendment defining, for each card: owner
module, contract name/version, cardinality, source of truth, tenant/actor propagation, permission, error
matrix, snapshot/freshness decision, bilateral fixture and browser acceptance evidence. A Blueprint row or a
legacy field alone is insufficient.

#### 4.3.2 Initiative Core v2 — governance baseline / non-executable

The exact create/edit user fields are `Code`, `Name`, `Description`, optional `PortfolioId`,
`InitiativeTypeCode`, `PriorityCode`, `PlannedStartDate` and `PlannedEndDate`. Therefore
`form_field_count: 8` and `golden_reference: slim`. `LifecycleState` is read-only and changed only by explicit
actions. `VisibilityPolicyKey`, `SupersedesInitiativeId`, tenant, audit, soft-delete and concurrency values are
not ordinary create/edit fields. `SupersedesInitiativeId` is accepted only by the dedicated create-new-from-
terminal contract below; it is not exposed by normal edit.

`InitiativeTypeCode` and `PriorityCode` are tenant-managed business classifications owned by MOD-0048, matching
the configurable classification/priority model used by the benchmarked enterprise PPM products. They are
nullable while an Initiative is `Proposed`, but both must be present and authoritatively validated before
`Active`. A supplied unknown value is `400`; unavailable, malformed or indeterminate MOD-0048 authority is
`503` with zero mutation. The PPM contract endpoint may project the authoritative options but may not create a
second local catalogue. Frontend options must be loaded through the same-origin PPM proxy and Gateway.

Cancellation reason, hold reason, completion outcome, closure reason and benefit disposition remain PPM-owned,
closed, in-domain lifecycle vocabularies. Every submitted code outside the returned closed set is `400` with
zero mutation. Hardcoded arrays, stale cache/LKG allow, synthetic defaults and frontend fallback values are
forbidden for both MOD-0048 classifications and PPM lifecycle vocabularies.

The exact approved Core v2 sets are:

```text
InitiativeCancellationReason : strategic-realignment · funding-withdrawn · business-case-rejected ·
                               duplicate-initiative · regulatory-block · capacity-unavailable ·
                               no-longer-viable · superseded
InitiativeHoldReason         : funding-paused · capacity-constraint · dependency-blocked ·
                               governance-review · strategy-review · external-constraint
InitiativeCompletionOutcome  : delivered-as-planned · delivered-with-variance · partially-delivered ·
                               transferred-to-operations
InitiativeClosureReason      : scope-completed · planned-end-reached · governance-directed-close ·
                               early-completion
InitiativeBenefitDisposition : tracking-required · tracking-in-progress ·
                               handed-off-to-outcome-owner · no-benefit-commitment
```

These values are exact lowercase kebab-case tokens. There is deliberately no `other`: an unclassified new
business reason requires an additive governed vocabulary decision instead of degrading portfolio analytics.

##### Lifecycle and required companion data

| Transition | Required permission / authority | Required companion data | Result and side effects |
|---|---|---|---|
| `Proposed -> Active` | `ppm.initiatives.change-lifecycle`; MOD-0023 only when authoritative PPM policy requires approval | Authoritatively validated non-null `InitiativeTypeCode` and `PriorityCode`; non-null valid planning dates; policy-required immutable MOD-0023 ApprovalOutcome reference | Direct transition when policy says no approval; otherwise Workflow-governed; no WorkCenter item for the direct path |
| `Proposed -> Cancelled` | `ppm.initiatives.change-lifecycle` | PPM cancellation reason code | Terminal `Cancelled` |
| `Active -> OnHold` | `ppm.initiatives.change-lifecycle` | PPM hold reason code | `OnHold`; notification only under the verified MOD-0288 recipient rule below |
| `OnHold -> Active` | `ppm.initiatives.change-lifecycle`; MOD-0023 only when authoritative PPM policy requires approval | Policy-required immutable MOD-0023 ApprovalOutcome reference | `Active`; no WorkCenter item when approval is not required |
| `Active|OnHold -> Completed` | `ppm.initiatives.change-lifecycle` | Valid PPM-owned `InitiativeClosure` | Atomic terminal `Completed` plus closure creation |
| `Active|OnHold -> Cancelled` | `ppm.initiatives.change-lifecycle` plus approved MOD-0023 Workflow outcome | PPM cancellation reason code and immutable approval reference | Atomic terminal `Cancelled`; unavailable/indeterminate authority is `503`, never local approval |

`Completed` and `Cancelled` are terminal and reject every lifecycle/edit mutation with `409`. WorkCenter does
not own or execute Initiative lifecycle. It may display only a genuine MOD-0023 approval work item created by
the Workflow owner. A transition that does not require approval creates no WorkCenter item, and ordinary
Initiative records must never be transformed into a WorkCenter provider item.

After `Active -> OnHold`, a notification may be requested only if MOD-0288 exposes a verified, same-tenant,
versioned owner/governance recipient contract and returns an authoritative recipient. Missing, ambiguous,
cross-tenant or unavailable recipient authority does not block the lifecycle transition and produces no
notification. The same transaction records the durable `recipient-unresolved` audit/outbox disposition, and
the mutation response carries a stable warning for the initiating UI. The implementation must not route to a
fabricated user, creator, free-text address or generic administrator, must not create a WorkCenter item and
must not reinterpret the warning as successful notification delivery.

##### InitiativeClosure ownership

`InitiativeClosure` is PPM-owned and is not a copy, subtype or adapter of the MOD-0024 Task closure contract.
It contains exactly these business fields: required `OutcomeCode`, required `ClosureReasonCode`, server-required
`CompletedAt`, required non-empty `CompletionSummary`, optional `EvidenceReferences` (`0..n`), optional
`FollowUpTaskReferences` (`0..n`), and required `BenefitDisposition`.
`OutcomeCode` and `ClosureReasonCode` use PPM-owned closed vocabularies. `CompletedAt` is server-controlled UTC
and cannot precede Initiative creation. `EvidenceReferences` are typed MOD-0031 references only; PPM stores no
evidence/document payload. `FollowUpTaskReferences` are typed MOD-0024 references only; PPM stores no
task/checklist/closure payload and creates no Task lifecycle truth. `BenefitDisposition` is a PPM closure
statement and cannot copy or infer MOD-0072 actual outcome, measurement, realization or benefit SoR data.
The reference collection cardinalities above are closed. Exact MOD-0031/MOD-0024 producer contract versions
remain blockers; no untyped ID, payload copy or local fallback may substitute for them.

##### Terminal supersession instead of reopen

A terminal Initiative is immutable and cannot be reopened. A replacement is a new `Proposed` Initiative whose
create contract records `SupersedesInitiativeId`. The referenced Initiative must exist, be non-deleted,
visible, in the same tenant and be exactly `Completed` or `Cancelled`; missing/cross-tenant/invisible is `404`,
non-terminal is `409`. The old terminal record is never mutated. The new record may supersede at most one old
record; an old terminal record may have at most one active, non-deleted direct successor. Self-reference,
duplicate successor and every direct or transitive cycle are `409`. Cycle validation and link write occur in
the same tenant-scoped transaction and use optimistic concurrency.

##### Typed links on Details; no foreign snapshots

Strategy, ownership/organization, KPI, benefit, budget/scenario, governance/workflow, evidence/document and
dependency data are never copied into the Initiative aggregate. Details renders them only as typed links
resolved from authoritative owner modules, with separate loading/unavailable/unauthorized/not-found states and
without mock, cached-authority or free-text fallback. Owners remain MOD-0352 (strategy), MOD-0288 (ownership),
the reconciled KPI owner, MOD-0117 `BenefitCommitment` plus MOD-0072 (planned versus realized benefit),
MOD-0136/MOD-0138 (budget/scenario), MOD-0023/MOD-0007 (workflow/governance decision), MOD-0031/MOD-0028
(evidence/document), MOD-0354 (dependency), and MOD-0024 (follow-up task). A link is renderable only after an
exact bilateral contract, cardinality, permission and tenant/non-disclosure matrix is approved.

##### Exact future implementation allowlist

This governance checkpoint may change only this module-pack file. A later separately authorized Initiative
Core v2 implementation is limited to these exact roots/files; anything else requires another amendment:

- `services/Diten.PpmService/src/Diten.PpmService.Domain/Entities/Initiative.cs`
- `services/Diten.PpmService/src/Diten.PpmService.Domain/Entities/InitiativeClosure.cs`
- `services/Diten.PpmService/src/Diten.PpmService.Domain/Initiatives/**`
- `services/Diten.PpmService/src/Diten.PpmService.Application/Features/Initiatives/**`
- `services/Diten.PpmService/src/Diten.PpmService.Persistence/Repositories/InitiativeRepository.cs`
- `services/Diten.PpmService/src/Diten.PpmService.Persistence/Mongo/PpmMongoContext.cs` (additive Initiative indexes/transaction registration only)
- `services/Diten.PpmService/src/Diten.PpmService.Persistence/DependencyInjection.cs` (additive Initiative registration only)
- `services/Diten.PpmService/src/Diten.PpmService.Infrastructure/Initiatives/**`
- `services/Diten.PpmService/src/Diten.PpmService.Infrastructure/DependencyInjection.cs` (additive Initiative typed clients only)
- `services/Diten.PpmService/src/Diten.PpmService.Api/Controllers/InitiativesController.cs`
- `services/Diten.PpmService/tests/Diten.PpmService.Tests/Initiatives/**`
- `services/Diten.PpmService/tests/Diten.PpmService.IntegrationTests/Initiatives/**`
- `frontend/Diten.Web/Controllers/PPM/PpmController.cs` (Initiative proxy actions only)
- `frontend/Diten.Web/Models/PPM/InitiativeModels.cs`
- `frontend/Diten.Web/Models/PPM/PpmViewModels.cs` (Initiative projection/configuration only)
- `frontend/Diten.Web/Views/PPM/Initiatives/**`
- `frontend/Diten.Web/wwwroot/assets/js/PPM/Initiatives/**`
- `frontend/Diten.Web/Resources/Views/PPM/Initiatives/**`
- `frontend/Diten.Web/tests/js/ppm-initiative-*.test.mjs`

Protected even for that later slice: `.antigravity/**`, Gateway/`ocelot.json`, Platform, Auth,
WorkCenter/WorkCenterNext, MOD-0023/MOD-0024/MOD-0031/MOD-0072/MOD-0288/MOD-0352/MOD-0354 owner runtime,
other PPM aggregate roots, shared layouts/assets, migrations, seeds, deployment and production configuration.

##### Initiative Core v2 API / HTTP matrix (future contract; no endpoint authority in this checkpoint)

| Method/path | Permission | Success | Fail-closed contract |
|---|---|---:|---|
| `GET /api/v1/ppm/initiatives/contracts/v2` | `ppm.initiatives.read` | `200` | Authoritative MOD-0048 type/priority options plus PPM-closed cancellation, hold, completion-outcome, closure-reason and benefit-disposition options; `401/403/503`; no fallback |
| `GET /api/v1/ppm/initiatives` | `ppm.initiatives.read` | `200` | Tenant-scoped, non-deleted list |
| `GET /api/v1/ppm/initiatives/{id}` | `ppm.initiatives.read` | `200` | Missing/cross-tenant/invisible/deleted `404` |
| `POST /api/v1/ppm/initiatives` | `ppm.initiatives.create` | `201` | Invalid field/vocabulary/date `400`; duplicate Code `409` |
| `POST /api/v1/ppm/initiatives/{terminalId}/successors` | `ppm.initiatives.create` | `201` | `404` non-disclosure; non-terminal/duplicate/cycle `409`; old record unchanged |
| `PUT /api/v1/ppm/initiatives/{id}` | `ppm.initiatives.update` | `200` | Terminal/stale `409`; lifecycle/visibility/supersession fields rejected `400` |
| `POST /api/v1/ppm/initiatives/{id}/lifecycle` | `ppm.initiatives.change-lifecycle` | `200` | Matrix/required reason/closure enforced; invalid input `400`, stale/invalid state `409`, owner dependency indeterminate `503` |
| `GET /api/v1/ppm/initiatives/{id}/details/links` | `ppm.initiatives.read` plus owner-specific read permission | `200` | Typed references only; owner `403/404/503` preserved without foreign payload copy |

All browser calls use the same-origin `/ppm/initiatives/api...` proxy and Gateway `5000`; direct `5062` calls,
browser bearer-token construction and Gateway changes are forbidden by this amendment. Every response uses
`Response<T>`/`CustomBaseController`; authenticated server context supplies tenant and actor.

### 4.4 Program — Phase 2A fields

| Field | Type | Required | Phase 2A invariant |
|---|---|---:|---|
| `Code` | string | Yes | Trim + NFC; max 64; unique per active tenant |
| `Name` | string | Yes | Trim + NFC; non-empty; max 200 |
| `Description` | string? | No | Trim + NFC; max 2000 |
| `PortfolioId` | Guid? | No | Same-tenant non-deleted Portfolio if supplied; optional-one cardinality |
| `LifecycleState` | closed string enum | Yes | `Draft`, `Active`, `OnHold`, `Completed`, `Cancelled`; default `Draft` |
| `VisibilityPolicyKey` | string? | No | Max 128; non-null requires authoritative MOD-0018 validation |

Whether a Program may contain Initiatives, Projects, both, or neither is an explicit owner decision. No
implicit hierarchy may be copied from the current frontend prototype.

### 4.5 Project — Phase 2A fields

| Field | Type | Required | Phase 2A invariant |
|---|---|---:|---|
| `Code` | string | Yes | Trim + NFC; max 64; unique per active tenant |
| `Name` | string | Yes | Trim + NFC; non-empty; max 200 |
| `Description` | string? | No | Trim + NFC; max 2000 |
| `ParentType` | closed string enum | Yes | `Initiative` or `Program` only |
| `ParentId` | Guid | Yes | Non-empty same-tenant non-deleted typed parent |
| `LifecycleState` | closed string enum | Yes | `Draft`, `Planned`, `Active`, `OnHold`, `Completed`, `Cancelled`; default `Draft` |
| `VisibilityPolicyKey` | string? | No | Max 128; non-null requires authoritative MOD-0018 validation |

`ParentType + ParentId` represents exactly one parent. No project task, progress, assignment, due-date,
schedule or DWS node fields are allowed here.

### 4.6 InvestmentCase — Gate L fields and invariants

| Field | Type | Required | Proposed invariant / open business decision |
|---|---|---:|---|
| `Code` | string | Yes | Trim + NFC; unique per active tenant |
| `Title` | string | Yes | Trim + NFC; non-empty |
| `Description` | string? | No | Trim + NFC |
| `PortfolioId` | Guid | Yes | Same-tenant, active, visible Portfolio |
| `PlannedStartDate` | scalar UTC `DateTime`? | No | Planning date only |
| `PlannedEndDate` | scalar UTC `DateTime`? | No | Cannot precede `PlannedStartDate` |
| `LifecycleState` | closed string enum | Yes | `Draft`, `UnderAnalysis`, `Closed`, `Withdrawn`; default `Draft` |

Each InvestmentCase belongs to exactly one Portfolio; one Portfolio may contain many InvestmentCases.
`PortfolioId` is immutable after creation. Lifecycle transitions are exactly `Draft → UnderAnalysis`,
`UnderAnalysis → Closed`, `Draft → Withdrawn` and `UnderAnalysis → Withdrawn`; `Closed` and `Withdrawn` are
terminal. `UnderAnalysis` is not workflow/approval state and `Closed` does not mean approved, selected or
funded. `IsApproved`, `ApprovedAt`, `ApprovedBy`, decision/rationale payloads and approve/reject/select/fund
commands, routes or UI are forbidden.

### 4.7 BenefitCommitment — Gate L fields and invariants

| Field | Type | Required | Proposed invariant / open business decision |
|---|---|---:|---|
| `Code` | string | Yes | Trim + NFC; unique per active tenant |
| `Title` | string | Yes | Trim + NFC; non-empty |
| `Description` | string? | No | Trim + NFC |
| `InvestmentCaseId` | Guid | Yes | Same-tenant, active, visible InvestmentCase; immutable after creation |
| `TargetDescription` | string | Yes | Planned-benefit commitment only; non-empty |
| `TargetDate` | scalar UTC `DateTime`? | No | Planning date only |
| `LifecycleState` | closed string enum | Yes | `Draft`, `Planned`, `Active`, `Closed`, `Cancelled`; default `Draft` |

Each BenefitCommitment belongs to exactly one InvestmentCase; one InvestmentCase may contain many
BenefitCommitments. A second `PortfolioId` is forbidden: Portfolio is resolved only through the authoritative
InvestmentCase relationship. Lifecycle transitions are exactly `Draft → Planned`, `Planned → Active`,
`Active → Closed` and `Draft|Planned|Active → Cancelled`; `Closed` and `Cancelled` are terminal. `Active` does
not mean realized and `Closed` is not MOD-0072 outcome validation. Actual/realized value, evidence, outcome,
budget/scenario payload or snapshot is forbidden.

### 4.8 Gate I PPM-owned consumer profile ledger — governance closed, non-executable

All names below are exact, case-sensitive PPM-owned contract/profile names with exact `ContractVersion = 1.0`.
They close only MOD-0117 consumer shape, cardinality and relationship ownership. They do not approve a producer
contract, alter a producer pack/status, choose transport/topology or grant runtime authority.

| PPM-owned type | Exact `ContractName` | Persisted fields, exactly | Cardinality / rule |
|---|---|---|---|
| `InvestmentCaseContextV1` | `ppm.investment-case-context` | `ContractName`, `ContractVersion`, `InvestmentCaseId` | Typed consumer context only; no free-text kind/discriminator |
| `GoverningDecisionReferenceV1` | `ppm.investment-case-governing-decision-reference` | `ContractName`, `ContractVersion`, `InvestmentCaseContext`, `DecisionRevisionReference` | `0..1`; exact one valid reference before `UnderAnalysis → Closed` |
| `SupportingDecisionReferenceV1` | `ppm.investment-case-supporting-decision-reference` | `ContractName`, `ContractVersion`, `InvestmentCaseContext`, `DecisionRevisionReference` | `0..n`; duplicate exact revision rejected |
| `InvestmentCaseApprovalOutcomeReferenceV1` | `ppm.investment-case-approval-outcome-reference` | `ContractName`, `ContractVersion`, `InvestmentCaseContext`, `ApprovalOutcomeReference` | Conditional `0..1`; only when authoritative approval policy requires approval; non-selection contract |
| `SelectedBudgetVersionReferenceV1` | `ppm.investment-case-selected-budget-version-reference` | `ContractName`, `ContractVersion`, `InvestmentCaseContext`, `BudgetVersionReference` | `0..1`; relationship is not selected-baseline truth |
| `InvestmentCaseScenarioVersionReferenceV1` | `ppm.investment-case-scenario-version-reference` | `ContractName`, `ContractVersion`, `InvestmentCaseContext`, `ScenarioVersionReference` | `0..n` |
| `InvestmentCaseComparatorOutputReferenceV1` | `ppm.investment-case-comparator-output-reference` | `ContractName`, `ContractVersion`, `InvestmentCaseContext`, `ComparatorOutputReference` | `0..n` |
| `SelectedScenarioReferenceV1` | `ppm.investment-case-selected-scenario-reference` | `ContractName`, `ContractVersion`, `InvestmentCaseContext`, `ScenarioVersionReference` | `0..1`; pinned minimal reference only, never selection occurrence/current-state truth |
| `BenefitCommitmentOutcomeReferenceV1` | `ppm.benefit-commitment-outcome-reference` | `ContractName`, `ContractVersion`, `BenefitCommitmentId`, `OutcomeReference` | `0..n`; attach/detach/retire relationship lifecycle is MOD-0117-owned |

Nested producer values are exact and unmodified: MOD-0007 `DecisionRevisionReferenceV1`, MOD-0136
`BudgetVersionReferenceV1`, MOD-0138 `ScenarioVersionReferenceV1` / `ComparatorOutputReferenceV1`, and MOD-0072
`OutcomeReferenceV1`. `Governing`/`Supporting` is encoded by the two distinct PPM wrapper identities and cannot
be added to the generic MOD-0007 tuple. `InvestmentCaseContextV1` may be supplied as validation-request context
to MOD-0136 FundingBaselineSelection and MOD-0138 ScenarioSelection; it gives neither producer ownership of an
InvestmentCase field, cardinality, transition or lifecycle.

The conditional `InvestmentCaseApprovalOutcomeReferenceV1` exact nested values are:

- `InvestmentCaseContext`: exact `ppm.investment-case-context` / `1.0`, with only `ContractName`,
  `ContractVersion`, `InvestmentCaseId`.
- `ApprovalOutcomeReference`: exact `platform.approval-outcome-reference` / `1.0`, with only `ContractName`,
  `ContractVersion`, `ApprovalOutcomeId`.

No outcome version, decision payload, actor, time, comment, task, route, assignee or workflow payload may be
added. MOD-0023 owns workflow/template/transition, `ApprovalTask`, effective approver eligibility/delegation
consumption and immutable terminal truth. MOD-0018 remains authoritative for eligibility/delegation; PPM may
neither recalculate it nor create approval commands/routes or local `Approved`/`Rejected` truth. PPM owns only
the conditional relationship/cardinality and persists the opaque wrapper. A renewed decision requires a new
MOD-0023 `WorkflowInstance`; its new outcome never replaces the old PPM reference automatically. Reference
replacement is a separate explicit, transactional and audited MOD-0117 lifecycle mutation.

The bilateral trusted-server-context profile is exact `ppm.investment-case-source-attestation` / `1.0` with
closed claims `ContractName`, `ContractVersion`, `TenantId`, `SubmittingServiceId`, `EffectiveActorId`,
`DelegatedActorChain`, `InvestmentCaseId`, `WorkflowTemplateVersionId`, `SubmissionRequestDigest`,
`IssuedAtUtc`, `ExpiresAtUtc`, `Nonce`. It is not a browser/client payload and carries no PPM aggregate payload.
PPM emits it only after an authoritative tenant-first InvestmentCase lookup. Missing, cross-tenant or invisible
InvestmentCase returns PPM-owned `404` and no MOD-0023 submission occurs. Signing mechanism, credential
rotation, validity window, nonce retention and replay fixtures remain runtime-promotion blockers; this
governance baseline grants no attestation implementation authority.

`TenantId`, `ActorId`, delegated actor, permission, S2S identity, correlation and transport metadata are never
payload fields in these persisted profiles; trusted server context supplies them. Free-text consumer kind,
discriminator and producer payload copying are forbidden. In particular PPM stores no decision/rationale/
evidence; budget amount/currency/period/line/certification or selected-baseline truth; scenario assumptions,
comparator inputs/algorithm/ranking/metric/output payload or selection occurrence/current-selection truth; or
actual value/measurement/period/evidence/realization state.

### 4.9 Gate I consumer validation modes and failure semantics — governance closed

The closed modes are `HistoricalResolve`, `NewReferenceEligibility`, and—only for selection contracts—
`CurrentSelectionEligibility`. A non-selection adapter must reject `CurrentSelectionEligibility` as malformed
caller input. Producer-specific request/response DTOs remain producer-owned and cannot be coerced into one common
DTO. Each bilateral adapter maps the exact producer compatibility contract; an `IsReferenceable` boolean alone
is never sufficient authority.

| Condition | Exact consumer result |
|---|---:|
| Malformed caller request, field set or mode | `400` |
| Authentication or trusted-context failure | `401` |
| Permission or dedicated S2S entitlement failure | `403` |
| Missing, cross-tenant, invisible or otherwise non-disclosable reference | `404` |
| Visible but retired, ineligible, stale or not current for the requested mode | `409` |
| Unknown/unsupported version, timeout, provider unavailable or malformed authoritative response | `503` |

Historical resolution never authorizes a new relationship. New-reference eligibility never proves current
selection. Current-selection eligibility is used only for the MOD-0136 selected funding baseline and MOD-0138
selected scenario contracts after their producer-owned selection profiles are approved.

`InvestmentCaseApprovalOutcomeReferenceV1` is a non-selection contract. It supports `HistoricalResolve` and
`NewReferenceEligibility`; `CurrentSelectionEligibility` is rejected with `400`. For
`NewReferenceEligibility`, the MOD-0023 outcome must be immutable and terminal, visible in the authenticated
tenant, produced by a workflow instance bound to the exact `InvestmentCaseContext`, and eligible for a new
attach under the policy-required approval relationship. The exact conditional failure mapping is: malformed
profile/request/mode `400`; authentication/trusted-context failure `401`; permission/S2S entitlement denial
`403`; missing/cross-tenant/invisible/non-disclosable MOD-0023 outcome `404`; visible but source-binding,
policy or lifecycle ineligible outcome, or conflicting replacement, `409`; unsupported version, timeout,
provider unavailable, malformed or indeterminate authoritative response `503`.

Historical resolution never validates a new attach and never changes which reference PPM stores. The
conditional consumer slice remains non-executable until the MOD-0023 PPM amendment is promoted, Gate 2 is
passed immediately before protected runtime work, bilateral fixtures are approved and explicit runtime
authority is recorded. MOD-0023 parent `ready-for-dev` status does not promote its PPM amendment, which remains
`DRAFT / NON-EXECUTABLE` at checkpoint `0ef0a517840d1d8c7d0bbd2fdb2d5d443f0d8470`.

#### 4.9.1 Gate I parallel consumer contract-test handoffs — AUTHORIZED / NON-RUNTIME

The Control Tower authorizes three independent **consumer contract-test-only** lanes. This authority is limited
to pure PPM-owned reference/wrapper records, strict serializers/validators, typed producer-port interfaces,
typed fail-closed result mapping and unit/contract/architecture fixtures. It does not authorize a production
provider client, HTTP endpoint/controller, DI registration, configuration/feature flag, Mongo collection/index,
relationship mutation handler, audit/outbox writer, worker/listener, Gateway/frontend/WorkCenter surface,
credential, deployment or activation. No lane is runtime-executable and no lane completion is a full Gate I,
full 1.3 or production-parity claim.

The exact source allowlist is additive and create-if-absent under these roots only:

- `services/Diten.PpmService/src/Diten.PpmService.Domain/GateI/DecisionTrace/**`
- `services/Diten.PpmService/src/Diten.PpmService.Domain/GateI/FundingScenario/**`
- `services/Diten.PpmService/src/Diten.PpmService.Domain/GateI/BenefitRealization/**`
- `services/Diten.PpmService/src/Diten.PpmService.Application/Features/InvestmentCases/GateI/DecisionTrace/**`
- `services/Diten.PpmService/src/Diten.PpmService.Application/Features/InvestmentCases/GateI/FundingScenario/**`
- `services/Diten.PpmService/src/Diten.PpmService.Application/Features/BenefitCommitments/GateI/BenefitRealization/**`

The exact test allowlist is:

- `services/Diten.PpmService/tests/Diten.PpmService.Tests/GateI/DecisionTrace/**`
- `services/Diten.PpmService/tests/Diten.PpmService.Tests/GateI/FundingScenario/**`
- `services/Diten.PpmService/tests/Diten.PpmService.Tests/GateI/BenefitRealization/**`
- `services/Diten.PpmService/tests/Diten.PpmService.IntegrationTests/GateI/DecisionTrace/**`
- `services/Diten.PpmService/tests/Diten.PpmService.IntegrationTests/GateI/FundingScenario/**`
- `services/Diten.PpmService/tests/Diten.PpmService.IntegrationTests/GateI/BenefitRealization/**`
- `services/Diten.PpmService/tests/Diten.PpmService.Architecture.Tests/GateI/DecisionTrace/**`
- `services/Diten.PpmService/tests/Diten.PpmService.Architecture.Tests/GateI/FundingScenario/**`
- `services/Diten.PpmService/tests/Diten.PpmService.Architecture.Tests/GateI/BenefitRealization/**`

Existing project files may receive only the minimum additive compile/test reference required for these roots.
`Persistence`, `Infrastructure`, `Api`, composition roots and every other source/test path remain outside this
handoff.

##### I-A — Decision Trace

The authorized part consumes MOD-0007 checkpoint `9968ecede48822f95a74461a4959c94b23abbc9b`
for bilateral fixture/provenance evidence and core checkpoint
`2d354a97bfbe09ed665e44dba8665181d2a56d78`. It owns only the PPM wrappers already fixed in §4.8:

- `GoverningDecisionReferenceV1` = exact four-field wrapper nesting the unmodified five-field
  `management-governance.decision-reference` / `1.0` `DecisionRevisionReferenceV1`;
- `SupportingDecisionReferenceV1` = the same exact nested tuple under its distinct four-field wrapper identity;
- `InvestmentCaseContextV1` = exact three-field `ppm.investment-case-context` / `1.0`.

The producer call contract is exact operation
`decision-registry.decision-references.validate.v1`, permission
`management-governance.decision-references.validate`, audience
`diten-management-governance-service`, Client ID `diten.management-governance`, owner `MOD-0007` and protocol
scope `diten.s2s.delegated.invoke`. Only `HistoricalResolve` and `NewReferenceEligibility` are valid;
`CurrentSelectionEligibility` is `400`. The read-only validation port creates no receipt, audit, cache or PPM
mutation. Later attach/replace/remove commands remain unauthorized and must use the PPM-owned idempotency,
tenant-first CAS, receipt, audit-intent and outbox transaction when separately approved.

Conditional MOD-0023 `InvestmentCaseApprovalOutcomeReferenceV1` remains **BLOCKED / TEST-PLAN-ONLY**. Its nested
`platform.approval-outcome-reference` / `1.0` tuple is exactly three fields, but checkpoint
`0ef0a517840d1d8c7d0bbd2fdb2d5d443f0d8470` remains `DRAFT / NON-EXECUTABLE`; the attestation signing mechanism,
validity/nonce decisions and bilateral executable fixtures are not approved. Therefore no MOD-0023 DTO,
serializer, port, fake, source file or positive acceptance fixture may be materialized under this handoff.
Negative architecture tests must prove its absence until the producer amendment is promoted and Gate 2 plus
explicit user/runtime authority are recorded.

##### I-B — Funding & Scenario, atomic coordinated lane

This lane may be developed in parallel internally, but its consumer checkpoint and later activation are atomic:
both the MOD-0136 and MOD-0138 contract-test sets must be green; one producer cannot be substituted for, merged
with or used to infer the other. The inputs are producer-owned checkpoints:

- MOD-0136 fixture closure `711962a3fdc1226d947672dc9b48d29296c960a0` and core
  `1949b93ead3dc1ac3234673bbe00ed67e3615743`;
- MOD-0138 fixture/security closure `3df680d6e006bfce19e382253ddd1f2f873c2295` and core
  `acae87090f35e5e0a7f37ad66dd8e98fc69c07bb`.

The exact PPM values are `SelectedBudgetVersionReferenceV1`,
`InvestmentCaseScenarioVersionReferenceV1`, `InvestmentCaseComparatorOutputReferenceV1` and
`SelectedScenarioReferenceV1` from §4.8. Their nested producer tuples are exact five-field
`fpa.budget-version-reference` / `1.0`, exact five-field `fpa.scenario-planning-reference` / `1.0`
`ScenarioVersionReferenceV1`, or its separate exact five-field `ComparatorOutputReferenceV1`. No budget amount,
currency, period, line, certification or selected-baseline truth; no scenario assumption, compared list,
algorithm, ranking, metric, output payload, occurrence, disposition or current-state flag may be copied.

MOD-0136 validation is exact operation/permission
`budgeting.budget-version-references.validate`, audience `diten-fpa-service`, Client ID `diten.fpa`, owner
`MOD-0136`. MOD-0138 validation is exact operation/permission
`fpa.scenario-planning.references.validate`, the same workload audience/Client ID, and distinct owner
`MOD-0138`. Shared workload identity never merges module entitlement, operation, permission, signing or fixture
ownership. `HistoricalResolve`, `NewReferenceEligibility` and `CurrentSelectionEligibility` are supported only
as fixed by each wrapper: Current is valid for selected Budget and selected Scenario only and is `400` for
scenario-version/comparator analytical wrappers. Producer success never makes the PPM wrapper its selection SoR.

##### I-C — Benefit Realization

This lane consumes MOD-0072 fixture/security checkpoint
`b4589139e8c9db544de5b66300640b214db3acf4` and core checkpoint
`5e937d79d3c2824e1647e8cd105b45c53d19c74c`. PPM owns only exact four-field
`ppm.benefit-commitment-outcome-reference` / `1.0` `BenefitCommitmentOutcomeReferenceV1`, nesting the unmodified
five-field, lower-camel producer tuple `diten.decision-intelligence.outcome-reference` / `1.0`. Actual value,
measurement identity/period/value, evidence and realization state are forbidden; no
`OutcomeMeasurementReference` identity is minted.

The producer call contract is exact operation
`outcome-tracking.outcome-references.validate`, permission
`decision-intelligence.outcome-references.validate`, audience `diten-decision-intelligence-service`, Client ID
`diten.decision-intelligence`, owner `MOD-0072` and protocol scope `diten.s2s.delegated.invoke`.
`HistoricalResolve` and `NewReferenceEligibility` are valid; `CurrentSelectionEligibility` is `400`.

##### Common fixture, security, idempotency and composition gates

Every lane must verify exact ordinal contract/type/version/property names; missing, duplicate, extra,
case-changed, normalized, aliased or unsupported values fail closed. Tests read producer evidence from the exact
immutable checkpoint and repository path (`git show <checkpoint>:<path>` or an independently checksum-bound
test artifact); they do not copy, regenerate or reinterpret producer fixtures. Each lane must cover exact
round-trip bytes/field allowlists, all supported and forbidden modes, `400/401/403/404/409/503`, missing and
cross-tenant non-disclosure, provider timeout/unavailable/malformed/indeterminate responses, and no-copy scans.

Trusted `TenantId`, effective actor, delegation, S2S identity, operation, permission and request hash come only
from the validated parent-MOD-0018 S2S trusted request context. Payload/header overrides, generic internal keys, role-name inference,
wildcards, aliases, cached allow, last-known-good allow and producer error reclassification are forbidden.
Provider validation is read-only and has no idempotency receipt; future PPM relationship mutation must be
tenant-scoped, idempotent and atomically persist business relationship, receipt, local audit intent and outbox,
with same-key/same-payload stable replay, changed payload/provenance `409`, body-once unknown-commit
reconciliation and cancellation propagation.

The three contract-test lanes may be built and reviewed concurrently. Production provider implementations,
Persistence/Infrastructure/Api composition, credentials, routes and relationship mutation remain a later
per-adapter runtime gate. Cross-lane composition starts only after I-A Decision, atomic I-B Budget+Scenario and
I-C Outcome contract-test checkpoints pass, MOD-0023 is either formally promoted or explicitly excluded by
policy, producer runtime evidence and parent-MOD-0018 S2S provisioning are approved, and a new explicit Control Tower runtime
authorization is recorded. Legacy parity/browser acceptance occurs after composition and never retires or
changes a legacy surface through this amendment.

#### 4.9.2 Gate I runtime-composition amendment R1 — BOUNDED IMPLEMENTATION / DEFAULT-OFF / NON-ACTIVATING

The user explicitly authorizes a bounded **runtime-composition implementation handoff** for the three PPM
consumer lanes, but not their activation. This amendment keeps the pack in `review`, does not change
or create production authority, and grants no deployment, production credential/key, secret, broker, listener,
endpoint/controller, Gateway, frontend, WorkCenter, migration execution, live traffic or provider-service
mutation authority. All composed code must remain internal-only and default-off. Missing configuration,
provider placement, credential, authority or compatibility evidence returns `503`; it can never be interpreted
as absence, denial, `404`, cached allow or local truth.

The immutable PPM contract-test composition is exact:

| Lane | Source checkpoint | Generated composed checkpoint | Scope/result |
|---|---|---|---|
| I-A Decision Trace | `fd3699956bab53d44ade6d08d22f3345d2445857` | `f63baf4a0440b9e3a5ec0f7ad47c4b926e4ab582` | MOD-0007 only; 38 focused unit tests |
| I-B Funding & Scenario | `a64139023ba596113c3c260cac175a8841a909e2` | `16df5472e136f9bff761532128c2dc9b490ea031` | atomic MOD-0136 + MOD-0138 lane; 20 focused unit tests |
| I-C Benefit Realization | `f6e9ec3db984a376ec863cab511d30d587130570` | `20c95c16b65b0733120d74020508c13dd56abe22` | MOD-0072; 48 focused unit tests; final composed HEAD |

The composed branch has exact 19 Domain/Application/test files, builds with zero warnings/errors, passes the
combined 106 focused unit tests, 34 immutable/checkpoint integration tests and the full 244-test unit/contract
suite. This is **composition-ready contract evidence**, not an activation-ready runtime result. The existing
Mongo-dependent integration baseline still requires externally provisioned `27018` replica-set and `27019`
standalone test authorities; their absence is not converted into a product PASS.

##### Exact dependency checkpoint composition order

The following is an ordered, scope-verified materialization plan, not authority to merge unrelated histories
or overwrite another owner’s service. Every group remains in its owner worktree and must be reconciled by exact
file scope before the PPM composition is enabled:

1. **Governance and entitlement authority:** parent `MOD-0018` S2S foundation
   `856a960d51f19ebf62b924a81cc5cdc1e66d2b8f` through closure
   `e61cf115` (current-main reconstruction ending `f972b01c`), MOD-0035
   `f1568ace514a33e951c652802347dd22ddd1ac11`, MOD-0021
   `525f6275a91eab7892741d8239dc1d0390915c3c` and Model-A governance
   `94f11ce83b5935f418565c312f237a45e54b7750` are authority prerequisites; they are not runtime code
   cherry-picks. Platform physical/applicability/subscription transaction chain is
   `2a66f0e911344ff0cdaa64415478638735fe63f8` →
   `5fab3b77189cd03e91d58c531eeb5da589b7fa08` →
   `941a60985433de04c162a24b0470c1775349c0db` →
   `e163bd8cc0b2c8a3c22f1c7d158ba48960a72562`; then Platform attestation producer
   `ea2638f0e851863c6bcf29fa394ee22279a6fc2f`.
2. **Historical Auth evidence, reconciled under parent MOD-0018:** Auth foundation chain
   `84ad2793f6d3f405205fa4467e581f220e78b041` →
   `56acefd9cacfa3cb57a54a6d6c193500a20acbcc` →
   `197f351a5e1e3c38db6698c95d2517428ddeb05c` →
   `f7dd4a61d8d8910dd1ede56e00e6aeb531ae7305`; then entitlement-attestation consumer
   `b952bbe40b4281d5fb8e9a75d5dc2a2f6b41e7cd` and exact fence reconciliation
   `1f5c69a63faf3e973153bf5efc6d873d59acbd43`. The last checkpoint is provenance from the combined
   Platform/Auth evidence branch; only its exact AuthService delta may be reconciled into an Auth-owned branch.
3. **Signed eventing and audit intake:** BuildingBlocks security foundation
   `3bd29dd18d5f42ceaebc6ab44a13d2961458fbc8` → Platform Gate I audit acceptance
   `f4f10b419df9b7ab2f807c7979ca4003e80e742b` → bilateral immutable fixture evidence
   `1ec4ad62c4cc70021114b7da544f4d9652d5be35`.
4. **Producer authorities:** MOD-0007 fixture/core
   `9968ecede48822f95a74461a4959c94b23abbc9b` / `2d354a97bfbe09ed665e44dba8665181d2a56d78`;
   MOD-0136 fixture/core `711962a3fdc1226d947672dc9b48d29296c960a0` /
   `1949b93ead3dc1ac3234673bbe00ed67e3615743`; MOD-0138 fixture/core
   `3df680d6e006bfce19e382253ddd1f2f873c2295` /
   `acae87090f35e5e0a7f37ad66dd8e98fc69c07bb`; MOD-0072 fixture/core
   `b4589139e8c9db544de5b66300640b214db3acf4` /
   `5e937d79d3c2824e1647e8cd105b45c53d19c74c`. Core checkpoints do not themselves prove or authorize a
   live validation endpoint; each producer owner must supply its provider implementation and runtime evidence.
5. **PPM consumer composition:** exact contract-test base `20c95c16b65b0733120d74020508c13dd56abe22`,
   followed only by the default-off implementation roots below. Composition tests must prove all dependency
   versions and identities before any flag can become eligible for a later activation amendment.

##### Exact bounded implementation allowlist

Future work under this amendment is create-if-absent/additive-only and limited to:

- the six Domain/Application Gate I roots already listed in §4.9.1;
- `services/Diten.PpmService/src/Diten.PpmService.Domain/Entities/InvestmentCase.cs` and
  `services/Diten.PpmService/src/Diten.PpmService.Domain/Entities/BenefitCommitment.cs`, only for the exact
  no-copy reference relationships fixed in §4.8;
- `services/Diten.PpmService/src/Diten.PpmService.Persistence/GateI/**`;
- `services/Diten.PpmService/src/Diten.PpmService.Persistence/Mongo/PpmMongoContext.cs` and
  `services/Diten.PpmService/src/Diten.PpmService.Persistence/DependencyInjection.cs`, additive composition
  only; existing Phase 2A/Gate L collections and registrations cannot be replaced;
- `services/Diten.PpmService/src/Diten.PpmService.Infrastructure/GateI/**` and
  `services/Diten.PpmService/src/Diten.PpmService.Infrastructure/DependencyInjection.cs`, additive typed
  provider/option registration only;
- the exact Unit/Integration Gate I test roots listed in §4.9.1;
- `services/Diten.PpmService/src/Diten.PpmService.Api/appsettings.json` and
  `services/Diten.PpmService/src/Diten.PpmService.Api/appsettings.Development.json`, only for explicit Boolean
  `false` defaults. Provider URI, credential, key, secret, broker address and activation value are forbidden.

The exact internal flags are `GateI:Composition:Enabled`, `GateI:DecisionTrace:Enabled`,
`GateI:FundingScenario:Enabled` and `GateI:BenefitRealization:Enabled`; all default to `false`, and the common
flag plus the named lane flag must both be true before a provider call is even considered. This amendment does
not authorize any code or deployment to set them true. There is no Gate I browser or Gateway route. The
producer validation services remain owned respectively by MOD-0007 / `Diten.ManagementGovernanceService`,
MOD-0136+MOD-0138 / `Diten.FpaService`, and MOD-0072 / `Diten.DecisionIntelligenceService`; PPM owns only its
typed clients, reference relationships and transactional local receipt/audit/outbox behavior. Platform owns
entitlement-attestation production, AuthService owns parent-MOD-0018 S2S enforcement, BuildingBlocks owns event mechanics and
Platform remains the audit-intake consumer.

##### MOD-0023 ExcludedV1 — first runtime release

MOD-0023 is explicitly excluded from this first composition release. `ExcludedV1` is a PPM release
disposition, not a producer contract or runtime identity. No ApprovalOutcome DTO, serializer, provider/client,
DI registration, positive fixture, persistence field or migration may be added. Any policy or lifecycle path
that requires `ApprovalOutcome` must return `503` before provider lookup or business mutation, with zero
relationship, receipt, audit-intent and outbox residue. It cannot fall back to MOD-0007, a local approval flag,
cache, user role or inferred policy. Later inclusion requires a separate MOD-0023 amendment promotion, Gate 2,
bilateral fixtures and explicit runtime plus activation authority.

##### Composition-ready versus activation-ready

`Composition-ready` means only that default-off code builds and that contract, security, tenant isolation,
same-key replay/changed-payload conflict, transaction rollback/unknown-commit, no-copy and cross-lane tests pass
against exact immutable evidence. `Activation-ready` additionally requires all owner-issued provider
placements/contracts, offline migration plan and rehearsal, production credential/vault/key provisioning,
entitlement/grant provisioning, signing identity, broker/DLQ/replay/observability evidence, live bilateral and
normal-port delivery, deployment review and a new explicit Control Tower activation amendment. Until then the
flags remain false, missing dependencies remain `503`, no migration runs, no live listener/worker starts and
neither Gate I nor full 1.3 may be reported complete.

#### 4.9.3 Gate I local API-test amendment R2 — OWNER-BOUNDED / LOCAL-ONLY / NON-PRODUCTION

The user authorizes the owner-scoped work needed to make the first Gate I release **Local API-Test Ready**.
This is one coordinated authorization, not a transfer of ownership between module packs. MOD-0117 may change
only the PPM-owned relationships, commands, API, transaction participants and typed consumers below. MOD-0007,
MOD-0136, MOD-0138, MOD-0072 and PSS each require their own owner-pack amendment and checkpoint before their
runtime files may be composed. Production activation, production credentials/keys/secrets, deployment,
migration execution, live broker/listener traffic, frontend, Gateway, WorkCenter, legacy deletion and parity
or retirement decisions remain unauthorized.

##### Exact local relationship and API surface

No foreign aggregate is introduced. `InvestmentCase` and `BenefitCommitment` reuse their existing PPM entity
base, technical `Version`, tenant isolation and soft-delete semantics. Only the exact no-copy references in
§4.8 may be stored. The API is explicit; a generic kind/discriminator endpoint is forbidden:

- `PUT|DELETE /api/v1/ppm/investment-cases/{id}/gate-i/governing-decision`;
- `POST /api/v1/ppm/investment-cases/{id}/gate-i/supporting-decisions` and
  `DELETE /api/v1/ppm/investment-cases/{id}/gate-i/supporting-decisions/{referenceId}`;
- `PUT|DELETE /api/v1/ppm/investment-cases/{id}/gate-i/selected-budget-version`;
- `POST /api/v1/ppm/investment-cases/{id}/gate-i/scenario-versions` and
  `DELETE /api/v1/ppm/investment-cases/{id}/gate-i/scenario-versions/{referenceId}`;
- `POST /api/v1/ppm/investment-cases/{id}/gate-i/comparator-outputs` and
  `DELETE /api/v1/ppm/investment-cases/{id}/gate-i/comparator-outputs/{referenceId}`;
- `PUT|DELETE /api/v1/ppm/investment-cases/{id}/gate-i/selected-scenario`;
- `POST /api/v1/ppm/benefit-commitments/{id}/gate-i/outcomes` and
  `DELETE /api/v1/ppm/benefit-commitments/{id}/gate-i/outcomes/{referenceId}`.

Every route uses `[Authorize]`, MediatR and the existing `Response<T>` / `CustomBaseController` envelope.
`TenantId`, actor, delegated actor and service identity are server-derived and are forbidden request fields.
Every write requires the current aggregate `ExpectedVersion`. The exact local permission is
`ppm.investment-cases.update` or `ppm.benefit-commitments.update`; no new permission, alias or wildcard is
created. Producer validation completes before the local transaction, but its result is bound into the
idempotency payload hash and is revalidated fail-closed when its authoritative freshness contract requires it.

##### Exact idempotency, receipt and transaction contract

Every relationship mutation requires one non-empty `Idempotency-Key` header. The authoritative V1 scope is
`TenantId + OperationId + IdempotencyKey`; an ordinal unique tenant-first index enforces it. `OperationId` is
the exact route/command identity and cannot be a user-supplied alias. The canonical payload hash is lowercase
SHA-256 over exact canonical request bytes plus trusted tenant, actor/delegation, S2S principal, producer
module/operation/permission, validation mode and authoritative reference/provenance binding. Same scope and
same hash returns the stored stable result. The same scope with changed request bytes or provenance returns
`409` without provider or mutation replay.

V1 receipts are durable financial/governance evidence: TTL, delete, purge and key reuse are forbidden.
Relationship mutation, receipt, local audit intent and event outbox are participants in one PPM-owned Mongo
replica-set transaction. The command body executes once. `UnknownTransactionCommitResult` permits only
same-session commit retry and majority-read receipt reconciliation; matching durable receipt returns its stored
result, changed hash returns `409`, and absent or indeterminate evidence returns `503`. Cancellation propagates
unchanged. Handlers/controllers never publish through RabbitMQ, MassTransit or `IEventBus` directly; only the
existing post-commit outbox path may publish.

##### Security, provider and release behavior

All reads and writes use `TenantId + Id + IsDeleted=false`; cross-tenant, missing, soft-deleted, invisible or
non-disclosable references return `404`. The independent gates are active `ModuleCode = PPM` entitlement,
exact PPM mutation permission, parent-MOD-0018 S2S trusted context and the exact producer operation/permission from §4.9.1.
Cache/LKG allow, role-name inference, client-supplied context and direct producer persistence/session access are
forbidden. Producer mappings preserve exact `400/401/403/404/409/503` semantics and never collapse dependency
failure to absence.

MOD-0023 remains exact `ExcludedV1`. No ApprovalOutcome field, DTO, serializer, client, DI registration,
positive fixture or persistence participant may be created. Any path whose policy requires ApprovalOutcome
returns `503` before producer lookup or transaction start with zero relationship, receipt, audit-intent and
outbox residue.

Local API testing uses canonical PPM port `5062`. Auth `5056` and Platform `5057` retain their canonical local
ports. Producer URIs are environment-injected only after their canonical port-registry records and owner
checkpoints are materialized; this amendment invents no producer port. The four Gate I flags remain committed
as `false`. A later controlled local evidence run may set them only through process environment variables after
all owner checkpoints pass. No committed true value, URI, credential or secret is allowed.

##### Exact additive implementation allowlist

In addition to the §4.9.2 allowlist, this amendment authorizes only:

- `services/Diten.PpmService/src/Diten.PpmService.Application/Features/InvestmentCases/InvestmentCaseFeature.cs`,
  only to preserve default-off Gate L closure while enforcing the governing-decision close guard when the
  common Gate I composition and DecisionTrace lane are locally enabled;
- `services/Diten.PpmService/src/Diten.PpmService.Api/Controllers/InvestmentCaseGateIReferencesController.cs`;
- `services/Diten.PpmService/src/Diten.PpmService.Api/Controllers/BenefitCommitmentGateIReferencesController.cs`;
- `services/Diten.PpmService/tests/Diten.PpmService.Tests/GateLDomainTests.cs`, only for the paired default-off
  and Gate-I-enabled lifecycle regression;
- minimum additive project references required by the already allowlisted Gate I source/test roots.

It does not reopen the broader Phase 2A frontend/Gateway scope. `.antigravity/**`, other services, existing
non-Gate-I PPM handlers, frontend, Gateway, WorkCenter and deployment files remain protected.

##### Local API-Test Ready evidence gate

The exact claim **Local API-Test Ready** is permitted only when all owner amendments and exact-scope runtime
checkpoints are composed and the following are green:

1. exact producer fixture/checkpoint bytes, schema and operation/permission binding for I-A, atomic I-B and
   I-C; MOD-0023 absence architecture tests;
2. `400/401/403/404/409/503`, tenant non-disclosure, stale/revoked/expired authority, timeout/malformed and
   no-copy matrices;
3. real Mongo tenant isolation, CAS and concurrency; same-key replay/conflict; every participant rollback;
   unknown-before/after durable commit, commit-only exhaustion, cancellation and standalone fail-closed;
4. exact receipt/index snapshot with zero TTL and no purge/delete path;
5. Auth parent-MOD-0018 S2S, Platform entitlement attestation, BuildingBlocks eventing and MOD-0021 audit bilateral evidence,
   while existing HS256 user/session and PPM/MDM regression suites remain green;
6. mutation evidence killing tenant, soft-delete, entitlement, permission, provider binding, no-copy, CAS,
   payload hash, transaction participant, body-retry, direct-publish, ExcludedV1 and default-off guard mutants;
7. all owner builds/tests, MOD preflights, repository architecture tests, diff/secret/artifact/protected-scope
   scans and disposable test-process cleanup;
8. local API smoke on `5062` using environment-only local evidence configuration and test identities that
   production composition must reject.

This wording does not mean browser-test ready, full Gate I, full 1.3, production-ready or parity-complete.
Gateway/frontend browser work, WorkCenter integration and production provisioning/activation remain later
separately authorized gates.

#### 4.9.4 Parent-MOD-0018 S2S outbound local-evidence reconciliation R3 — TEST-HOST-OWNED / DEFAULT-UNAVAILABLE

The Control Tower closes the local-evidence transport ambiguity without inventing an AuthService endpoint.
Production and default PPM composition have no proof issuer and return typed `503` before an owner HTTP call.
Only a test host may inject an ephemeral-RSA proof provider; the provider, key and token never enter
configuration, logs, persistence, receipts, audit intents or outbox documents. This amendment creates no Auth
endpoint, production token service, credential, secret, JWKS route, cache, deployment or activation authority.

The canonical HTTP request binding for this slice is the PPM-local, FU-neutral
`S2SCanonicalRequestBinding` in the exact Application Gate-I allowlist below. Its output is exactly 64
lowercase hexadecimal SHA-256 characters over the exact method, absolute path, raw body bytes, tenant,
operation and ordinal permission sequence. The older lane-local 43-character Base64URL test bindings are not
accepted as HTTP proof bindings and must be reconciled; dual-format acceptance, normalization and fallback are
forbidden. This decision does not silently change AuthService's internal issuance contract. Production
compatibility remains a later Auth-owner transport amendment.

The local-evidence proof provider is an application port with typed `Issued`, `Unauthenticated`, `Forbidden`,
`Conflict` and `Unavailable` outcomes. It receives only the exact receiver profile, trusted tenant/effective
actor/delegation, HTTP method/path/raw body and requested operation/permission. It returns an opaque proof only
to the Infrastructure HTTP handler. Application/domain code never receives the raw token. Default DI binds the
port to `Unavailable`; the test-host binding is explicit and cannot be selected by configuration or a request.
Proof reuse, refresh, retry after terminal failure, LKG/cache allow and forwarding an inbound HS256 user/session
token are forbidden. Cancellation propagates unchanged.

The receiver table is closed:

| Owner | Method and exact path | Audience / client | Operation / permission |
|---|---|---|---|
| `MOD-0007` | `POST /internal/v1/decision-registry/decision-references/validate` | `diten-management-governance-service` / `diten.management-governance` | `decision-registry.decision-references.validate.v1` / `management-governance.decision-references.validate` |
| `MOD-0136` | `POST /internal/v1/fpa/budgeting/budget-version-references/validate` | `diten-fpa-service` / `diten.fpa` | `budgeting.budget-version-references.validate` / `budgeting.budget-version-references.validate` |
| `MOD-0138` | `POST /internal/v1/fpa/scenario-planning/references/validate` | `diten-fpa-service` / `diten.fpa` | `fpa.scenario-planning.references.validate` / `fpa.scenario-planning.references.validate` |
| `MOD-0072` | `POST /internal/v1/decision-intelligence/outcome-tracking/outcome-references/validate` | `diten-decision-intelligence-service` / `diten.decision-intelligence` | `outcome-tracking.outcome-references.validate` / `decision-intelligence.outcome-references.validate` |

MOD-0136 and MOD-0138 share a workload identity but never an owner, entitlement, operation, permission or
request-binding profile. Any cross-profile substitution is terminal. The local owner URI is injected only by
the test host after the corresponding owner checkpoint is composed; committed URI values remain forbidden.

The additive source allowlist for this reconciliation is exact:

- `services/Diten.PpmService/src/Diten.PpmService.Application/GateI/S2SOutboundProofContracts.cs`;
- `services/Diten.PpmService/src/Diten.PpmService.Infrastructure/GateI/S2SOutboundProofProvider.cs`;
- `services/Diten.PpmService/src/Diten.PpmService.Infrastructure/GateI/GateIOwnerReferenceHttpClients.cs`;
- `services/Diten.PpmService/src/Diten.PpmService.Infrastructure/GateI/GateIComposition.cs`;
- `services/Diten.PpmService/src/Diten.PpmService.Infrastructure/DependencyInjection.cs`;
- the existing Application and Infrastructure project files only for minimum project-local compilation references
  required by these exact files. A new Platform.Common authentication subtree/reference is forbidden.

`S2SOutboundProofContracts.cs` is the sole PPM-local Model A contract owner. It may define only the exact receiver
profile, canonical request-binding input, opaque issued proof, typed issue disposition and immutable trusted-context
projection needed by the four producer adapters. It cannot define a permission catalog, grant store, key provider,
authentication scheme, endpoint or shared cross-service framework. AuthService remains the parent-MOD-0018 authority;
PPM consumes its proof/decision result and never recalculates entitlement, permission or delegation.

Only these four existing reconciliation files may replace their lane-local HTTP binding/context projection:

- `services/Diten.PpmService/src/Diten.PpmService.Application/Features/InvestmentCases/GateI/DecisionTrace/DecisionTraceValidation.cs`;
- `services/Diten.PpmService/src/Diten.PpmService.Application/Features/InvestmentCases/GateI/FundingScenario/FundingScenarioValidationContracts.cs`;
- `services/Diten.PpmService/src/Diten.PpmService.Application/Features/BenefitCommitments/GateI/BenefitRealization/OutcomeReferenceValidation.cs`;
- `services/Diten.PpmService/src/Diten.PpmService.Application/Features/InvestmentCases/GateI/DecisionTrace/GateIRelationshipMutations.cs`.

Tests are limited to the existing Gate I unit/integration/architecture roots in §4.9.1. Evidence must prove:
default/provider-unavailable owner-call count zero; exact four-profile binding; raw-byte request hash; the named
parent-MOD-0018 S2S bearer header only; test-identity production rejection; `400/401/403/404/409/503` preservation; terminal
zero relationship/receipt/audit/outbox residue; receipt conflict and ExcludedV1 before proof acquisition;
cancellation propagation; and expected-red mutations for every binding dimension, HS256 forwarding, default
scheme fallback, cache/retry, status reclassification and producer-profile substitution. This closes a local
evidence seam only; it does not make Gate I production-issuance or activation ready.

#### 4.9.5 Gate I-A local evidence closure R4 — TEST-OWNED / NON-PRODUCTION / NON-ACTIVATING

The Control Tower authorizes only the test-owned infrastructure needed to execute the remaining Gate I-A
local evidence without relying on developer-managed Mongo ports or a long-running PPM host. This amendment
does not change production source, committed runtime configuration, activation flags, credentials, secrets,
deployment, Gateway, frontend or WorkCenter. It grants no production or browser authority and does not make
Gate I-A, Gate I or MOD-0117 complete.

The real-Mongo evidence harness is exact:

- new test-only file
  `services/Diten.PpmService/tests/Diten.PpmService.IntegrationTests/GateI/DecisionTrace/GateIDisposableMongoReplicaSet.cs`;
- the executable is exact `/opt/homebrew/bin/mongod`; download, Docker and a repository-owned Mongo binary are
  forbidden;
- the operating system selects a free loopback port and the harness rejects `27017`, `27018`, `27019` and
  `27021` before process start; the selected port must be `>=27022`;
- every run uses a unique database and private temporary data/log/pid paths, initializes a single-node replica
  set, serializes only this disposable lifecycle and proves process, listener, database and directory cleanup;
- an occupied or forbidden port is never killed, reused or reconfigured;
- the existing
  `services/Diten.PpmService/tests/Diten.PpmService.IntegrationTests/GateI/DecisionTrace/GateIRelationshipMutationMongoTests.cs`
  may be changed only to obtain its Mongo URI/database/lifecycle from this harness. It cannot fall back to
  `PPM_GATE_I_TEST_MONGO_URI`, `27017`, `27018`, `27019` or `27021`.

The local API smoke is test-owned and exact:

- new test-only file
  `services/Diten.PpmService/tests/Diten.PpmService.IntegrationTests/GateI/DecisionTrace/GateILocalApiSmokeTests.cs`;
- the IntegrationTests project may add only one project reference to
  `services/Diten.PpmService/src/Diten.PpmService.Api/Diten.PpmService.Api.csproj`;
- the test launches the already compiled PPM API as a child process bound to exact `127.0.0.1:5062`; it first
  proves the port is free and fails closed without killing or reusing any listener when occupied;
- all test configuration is process-environment-only and ephemeral. The smoke verifies `/health`, an
  authenticated Gate I route, the default-off `503` boundary and complete child-process/temp cleanup;
- production `Program.cs`, controllers, application settings, launch profiles, DI, endpoint and route files
  cannot be edited by this amendment. No Gateway or browser traffic is involved.

Mutation evidence remains limited to the existing
`services/Diten.PpmService/tests/Diten.PpmService.Tests/GateI/DecisionTrace/verify_mutation_evidence.py` and
`decision-trace-mutation-evidence.json`. The verifier must rerun against the current
`DecisionTraceValidation.cs`, apply only transient test-owned mutations, compile and obtain the expected-red
targeted failure for every declared non-equivalent mutant, then restore the exact source bytes and SHA-256.
It may update stale evidence identities/hashes only from the fresh executable run. No mutant, marker, backup,
generated source or test artifact may remain.

R4 evidence is accepted only when the focused Gate I-A unit/contract suite, disposable real-Mongo suite,
local API smoke, mutation verifier, MOD-0117 preflight, repository architecture guard, build, diff/scope,
secret/artifact scan and cleanup checks all pass. Any occupied `5062`, missing `/opt/homebrew/bin/mongod`,
forbidden-port selection, cleanup residue, stale mutation restore or protected-path diff is one fail-closed
blocker. Implementation changes remain unstaged until review and require a separate checkpoint authorization;
this governance checkpoint alone performs no activation.

### 4.10 ExternalContextReference validation projection

This is the authorized provider contract for the internal endpoint
`POST /internal/v1/ppm/external-context-references/validate`:

| Field | Type | Rule |
|---|---|---|
| `ContractName` | string | Exact `ppm.external-context-reference` |
| `ContractVersion` | string | Exact `1.0` |
| `ContextKind` | closed string enum | `Portfolio`, `Initiative`, `Program`, `Project` only |
| `ContextId` | Guid | Canonical non-empty Guid; opaque to consumer |

`TenantId` and `ActorId` come only from authenticated server context. Success returns only the typed
reference, never a full PPM object. Missing, soft-deleted, cross-tenant, invisible or not-referenceable all
produce 404 without existence disclosure. Referenceability is derived, not a stored `IsReferenceable`
business truth.

The provider is default-disabled and browser/Gateway-inaccessible. The first allowed consumer identity is
`Diten.ManagementGovernanceService`; it must present a dedicated consumer credential and a forwarded signed
user JWT. Activation/configuration is checked before the credential; framework JWT authentication and a
strict endpoint-specific `tenant_id`/`sub` context gate follow before PPM entitlement, exact permission and
tenant-first lookup. Missing or malformed non-empty tenant/actor claims return `401`; `NameIdentifier`
fallback is allowed only when `sub` is absent, matching the shared evaluator contract.

The provider applies a lookup-only bounded timeout from
`ExternalContextProvider:LookupTimeoutMilliseconds`: default `2000 ms`, inclusive minimum `100 ms` and
maximum `5000 ms`. Enabled deployments fail startup validation outside that range. This deadline wraps
exactly one authoritative context lookup and does not alter global Mongo or normal PPM CRUD timeouts.
Provider-owned timeout returns the same generic `503` dependency response without existence disclosure;
caller/request cancellation propagates unchanged and is never converted to `503`. No retry, fallback or
cache is authorized by this setting.

Permission mapping is closed and exact: `Portfolio -> ppm.portfolios.read`,
`Initiative -> ppm.initiatives.read`, `Program -> ppm.programs.read`, and
`Project -> ppm.projects.read`. No `.view` alias, wildcard, prefix, case normalization, trimming or new
permission is authorized. Provider v1 does not evaluate object-level `VisibilityPolicyKey`: null proceeds
through the remaining checks, while non-null fails closed with indistinguishable `404`. Future support needs
a MOD-0018-owned versioned visibility contract and a separate provider contract revision/FU.

All writes use tenant-first filters and optimistic concurrency:
`TenantId + Id + IsDeleted=false + Version`. A version mismatch produces 409; silent overwrite is forbidden.

## 5. Repo Scope

The authorized Phase 2A implementation may create only:

- `services/Diten.PpmService/**` — user-approved Phase 2A scaffold/backend.
- `frontend/Diten.Web/Views/PPM/**` — user-approved tenant-shell Phase 2A surfaces.
- `frontend/Diten.Web/Controllers/PpmController.cs` or equivalent same-origin proxy — exact decision pending.
- `frontend/Diten.Web/Models/PPM/**` — frontend-only form/list models for the authorized six surfaces.
- `frontend/Diten.Web/Navigation/PpmModuleManifest.cs` — discovery metadata for the exact `MOD-0117` / `PPM`
  route and permission inventory; it grants no entitlement, role or effective permission.
- `frontend/Diten.Web/wwwroot/assets/js/PPM/**`.
- `frontend/Diten.Web/Resources/Views/PPM/**`.
- `frontend/Diten.Web/tests/js/ppm-add-new-delegation.test.mjs` and
  `frontend/Diten.Web/tests/js/ppm-gate-l-contract.test.mjs` — frontend-only executable contract evidence.
- `services/Diten.PpmService/tests/**`.
- Gateway route work only through a separate `integration-agent` task after route/port approval.

The later Gate I contract-test-only authority is narrower and is limited to the exact Domain/Application and
test roots enumerated in §4.9.1. It does not inherit the broad Phase 2A/frontend/Gateway scope above and grants
no Persistence, Infrastructure, Api, composition or runtime change.

Gateway configuration remains unauthorized and integration-agent-only.

### Current-main frontend materialization amendment — NON-PRODUCTION / NON-ACTIVATING

The explicit Control Tower decision dated 2026-08-29 authorizes semantic materialization of the already
approved MOD-0117 frontend from historical branch `feature/ppm/mod-0117-phase2a-integration` onto the
current-main integration line. The scope is exactly the frontend paths listed above, including the equivalent
same-origin proxy at `frontend/Diten.Web/Controllers/PPM/PpmController.cs`. Existing frontend composition may
receive only the minimum additive registration needed by these exact paths. WorkCenter, WorkCenterNext,
Gateway, Platform, other module UI, shared layouts and shared assets remain protected.

The historical source is evidence, not automatically correct code. Materialization must preserve the current
HttpOnly user/session flow, derive tenant identity only from authenticated server context, forward the exact
tenant/correlation headers required by the Gateway contract, use no browser token access or direct service
port, and keep all six surfaces on `_LayoutTenantShell`. It must preserve the exact twenty-four permissions,
seven-language resource parity and Golden Slim/DataTable v2 profile. No bulk-delete capability may be added.
Successful build, focused frontend tests, localization parity, route/proxy checks and the applicable DataTable
verifier results are local evidence only; this amendment grants no production activation, push, merge,
WorkCenter integration, legacy migration or retirement authority.

## 6. Protected Paths

- `.antigravity/**`
- `gateway/Diten.ApiGateway/**/ocelot.json` — integration-agent only.
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` — FROZEN.
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- `services/Diten.EnterpriseStrategyService/**`
- `services/Diten.Platform/**`
- `services/Diten.Platform.Common/**`
- `services/Diten.AuthService/**`
- `services/Diten.ManagementGovernanceService/**`
- MOD-0354 code and collections.
- Existing delivery-execution prototype/mock code; containment/migration requires a separate approved pack.
- Office documents and Blueprint workbooks.

## 7. Dependencies

| Dependency | Use | Current gate |
|---|---|---|
| DCP-006 | Active Slice 2 orchestration | Approved; OD-03 closed, OD-04 OPEN/PARTIAL |
| MOD-0018 | Independent tenant entitlement plus JWT permission enforcement and actor context | PARTIAL; two-gate policy fixed, real PSS-owned AuthService catalog/grant provisioning and entitlement runtime evidence open |
| MOD-0021 / MOD-0035 | Immutable audit integration over shared eventing mechanics | PARTIAL; event and final Minimal Mutation Audit v1 consumer contract fixed; publisher credential and runtime evidence open |
| MOD-0007 — Decision & Rationale Log | Mandatory Gate I-A governing/supporting decision producer | Parent pack `ready-for-dev` at `ac6ca5e6ed194e640c933c3ccce82b8fac8608d6`; bounded core checkpoint `2d354a97bfbe09ed665e44dba8665181d2a56d78`; PPM bilateral adapter/runtime authority still open |
| MOD-0023 — Workflow Designer (Approvals/SLAs/Escalations) | Conditional Gate I-A ApprovalOutcome producer | Existing pack/runtime gives no PPM integration authority; separate PSS-owned amendment required |
| MOD-0136 — Budgeting | Mandatory Gate I-B selected BudgetVersion producer | Parent pack `ready-for-dev` at `c12110491abd353ced31cd5a51a0142ad6e99ef1`; bounded core checkpoint `1949b93ead3dc1ac3234673bbe00ed67e3615743`; PPM bilateral adapter/runtime authority still open |
| MOD-0138 — Scenario Planning | Gate I-B scenario/comparator producer, mandatory for full 1.3 completion | Parent pack `ready-for-dev` at `6bd456f68c43e5e73fcde52bcf6f15b9fceab42e`; bounded core checkpoint `acae87090f35e5e0a7f37ad66dd8e98fc69c07bb`; PPM bilateral adapter/runtime authority still open |
| MOD-0072 — Decision Logs & Outcome Tracking | Mandatory Gate I-C outcome/realization producer | Parent pack `ready-for-dev` at `79a71cf6aa14e637277258dbecc257dc30125c5a`; bounded core checkpoint `5e937d79d3c2824e1647e8cd105b45c53d19c74c`; PPM bilateral adapter/runtime authority still open |
| MOD-0354 | Consumer of typed ExternalContextReference only | Draft; provider runtime evidence blocks it |
| MOD-0024 | Task/checklist boundary only | No local implementation |

The existing MDM/Auth lookup-validation clients are pattern evidence only. Their bearer/tenant propagation
does not settle PPM S2S identity/delegation. The MDM validator behavior that collapses transport, timeout,
malformed response and all non-success responses into 404 must not be copied.

The four canonical producer parent packs and bounded core checkpoints now exist in separate immutable branch
histories. They are not composed into this PPM branch, do not choose physical service placement for PPM and do
not themselves approve a PPM bilateral consumer adapter. MOD-0023 requires promotion of its separate PSS-owned
amendment. A PPM Gate I
consumer adapter becomes executable only after its producer contract, the bounded §4.9.1 consumer contract-test
checkpoint and a matching later MOD-0117 runtime amendment are approved and explicit runtime authority is
recorded.

## 8. Runtime Constraints

- MongoDB, single database, tenant-owned collections.
- TenantId and ActorId are resolved from authenticated server context; client payload values are forbidden.
- Unknown/unresolved tenant fails closed; no default-tenant fallback.
- Gate I reference contracts require server-derived TenantId/ActorId, dedicated authenticated S2S identity,
  separately validated delegated actor, exact permission, opaque canonical non-empty Guid, immutable
  contract/revision version, correlation, idempotency and compatibility policy. Failure semantics are exact:
  authentication/context `401`, permission/eligibility `403`, missing/soft-delete/cross-tenant/invisible `404`,
  immutable/version/idempotency conflict `409`, timeout/malformed/unknown-version/unavailable `503`.
- PPM cannot fail open, infer foreign ownership from local cache or copy foreign decision/rationale/evidence,
  approval, budget/scenario output, actual measure/period/evidence or realized-state payloads.
- Current MOD-0023 code reality is not a consumable approval contract: client-supplied ActorId, free-text
  ObjectType/ObjectId/ObjectRef, non-authoritative candidate principals, missing PPM-facing typed/versioned
  outcome, unproven atomic transition persistence and missing immutable authoritative outcome reference remain
  PSS-owned remediation blockers. No runtime authority is created here.
- Every read/write filters `TenantId` and `IsDeleted=false`.
- Cross-tenant reads and references return 404.
- Soft delete uses `IsDeleted` and `DeletedAtUtc`; physical delete is forbidden.
- Tenant-first indexes and owner-approved partial unique active-code indexes are required.
- Optimistic concurrency uses technical `Version`; semantic states cannot reuse that name.
- Phase 2A mutation and producer-local technical audit intent/outbox are written in the same Mongo
  replica-set transaction; inability to persist either rolls back the mutation.
- The approved producer event identity is `PpmAuditIntentSubmittedV1`; EventName/routing key is
  `ppm.audit-intent.submitted.v1`. PPM handlers/controllers never call RabbitMQ or MassTransit directly;
  a future authorized producer worker uses only MOD-0035's public `IEventBus`/outbox abstraction.
- Its `ppm-event-hmac-sha256.v1` input signs exact newline-delimited envelope provenance in this order:
  scheme, EventId, EventName, EventVersion, TenantId, CorrelationId, Producer, CausationId (or literal `-`),
  OccurredAtUtc and payload byte length, followed by exact canonical payload bytes. The wire signature is
  lowercase `[0-9a-f]{64}` only.
- Shared `EventEnvelope`, `IEventBus`, outbox and inbox mechanics belong to
  `Diten.BuildingBlocks.Eventing`. MOD-0117 owns this logical PPM event at the planned
  `services/Diten.PpmService/src/Diten.PpmService.Contracts/Events/**` path. Platform is consumer-only;
  `Diten.Platform.Contracts` does not own this PPM event.
- Future producer implementation is restricted to narrow
  `services/Diten.PpmService` Infrastructure/Application/Persistence worker-outbox paths and PPM tests.
  It requires a separately reviewable MOD-0117 atomic change after the DCP-006 MOD-0035/PSS status gate and
  explicit user runtime approval; the present `review` status does not authorize it.
- Delivery is durable at-least-once with idempotent consumption; exactly-once is not claimed. Post-commit
  failure uses retry, dead-letter, alarm and authorized replay.
- The final MOD-0021 payload/consumer mapping is **Minimal Mutation Audit v1**, containing exactly
  `auditIntentId`, `actorId`, `entityType`, `entityId`, `mutation` and `occurredAtUtc`. It evidences only
  actor, minimal mutation, PPM aggregate and time—not authorization/entitlement, a business snapshot or
  complete lifecycle history. The authenticated publisher credential remains a runtime evidence gate;
  local intent is not the business audit SoR. Platform `audit_outbox`/`audit_events`, the shared-key
  internal append endpoint, full snapshots, secrets, tokens and raw permission inventories are forbidden.
- Delivery uses 5 total attempts: 10 seconds after the first failure, then exponential backoff with jitter
  capped at 5 minutes; the fifth failed attempt causes DLQ plus alarm. Authorized replay preserves the same
  `EventId` and identical canonical bytes; changed bytes are rejected. If first delivery was not accepted,
  replay may create exactly one `AuditEvent`; if accepted, it creates none. Idempotency is
  `ConsumerName + EventId`; unauthorized replay and replay UI/API are forbidden.
- A boolean `IsApproved` or `IsReferenceable` cannot be business truth.
- Lifecycle and referenceability states cannot be copied from mock/prototype code.
- `init-001`, `prj-001` and any candidate/legacy identity are forbidden runtime literals.
- Service port is `5062`. Frontend remains `5001`; browser traffic uses Gateway `5000`, never direct `5062`.
- Existing port `5004` delivery-execution routes are legacy ES evidence, not MOD-0117 route allocation.
- Phase 2A may expose gateway-ready object CRUD/lifecycle contracts. The internal provider endpoint and
  dedicated service-credential plus delegated-actor model are authorized only as specified in §4.8; no
  provider Gateway route or DWS integration is authorized.
- Provider outage, timeout or malformed transport maps to 503, never 404.
- Fail-open and local-cache ownership/existence inference are forbidden.
- The read-only provider writes no business mutation, audit intent, cache or idempotency receipt. This is
  `N/A` only for this validation operation and does not close Phase 2B mutation idempotency decisions.

## 9. Layout & Shell Contract

- Frontmatter `shell: tenant`.
- Every future MOD-0117 Razor page must explicitly set:

```cshtml
@{
    Layout = "_LayoutTenantShell";
}
```

- `_ViewStart.cshtml` is not used to infer the shell and `_Layout.cshtml` remains unchanged.
- Planned view root: `frontend/Diten.Web/Views/PPM/{Surface}/`.
- Seven languages are required: en, fr, es, zh, ar, ru, tr.
- Future UI must cover loading, empty, 400, 401, 403, 404, 409 and 503 states.
- Referenceability/visibility must be explainable without disclosing inaccessible object existence.
- Existing Management Governance / Delivery Execution mock pages are not production templates.

## 10. Backend File Convention

The authorized service scaffold and object features follow:

```text
services/Diten.PpmService/src/Diten.PpmService.Application/Features/{Object}/
├── Commands/                       # one sealed command record per file
├── Queries/                        # one sealed query record per file
├── Handlers/
│   ├── CommandHandlers/            # separate, mandatory
│   └── QueryHandlers/              # separate, mandatory
├── Validators/                     # one validator per file
└── {Object}Models.cs
```

- Commands: `{Verb}{Object}Command`.
- Queries: `Get{Object}{Qualifier}Query`.
- Handlers: `{Verb}{Object}Handler`; no `CommandHandler`/`QueryHandler` suffix.
- Validators: `{Verb}{Object}Validator`; no `Command` suffix.
- Commands/queries use `IRequest<Response<T>>`.
- Controllers contain no business logic.
- External validation is consumed behind an Application interface and implemented in Infrastructure.
- MongoDB driver types remain in Persistence.

Scaffold authorization is recorded. Gate L and the bounded provider are implemented with isolated evidence;
provider activation/DWS consumption and every Gate I adapter remain blocked.

## 11. Frontend File Contract

| Surface | Authorized capability | Form-field count | Golden reference | State |
|---|---|---:|---|---|
| Portfolio | `/ppm/portfolios`; Code, Name, Description, LifecycleState, VisibilityPolicyKey | 5 | slim | AUTHORIZED 2A |
| Initiative | `/ppm/initiatives`; Code, Name, Description, optional PortfolioId, InitiativeTypeCode, PriorityCode, PlannedStartDate, PlannedEndDate; lifecycle read-only/action-based | 8 | slim | INITIATIVE CORE V2 GOVERNANCE-CLOSED; implementation not authorized by this amendment |
| Program | `/ppm/programs`; Code, Name, Description, PortfolioId, LifecycleState, VisibilityPolicyKey | 6 | slim | AUTHORIZED 2A |
| Project | `/ppm/projects`; Code, Name, Description, ParentType, ParentId, LifecycleState, VisibilityPolicyKey | 7 | slim | AUTHORIZED 2A |
| InvestmentCase | `/ppm/investment-cases`; list/create/view/edit/soft-delete/lifecycle | 7 | slim | Gate L implemented at `536aa685`; isolated review evidence, not production activation |
| BenefitCommitment | `/ppm/benefit-commitments`; list/create/view/edit/soft-delete/lifecycle | 7 | slim | Gate L implemented at `536aa685`; isolated review evidence, not production activation |

For each surface:

- `≤8` approved user-entered fields selects Slim with `_CreateEditOffcanvas.cshtml` and
  `_DetailsQuickView.cshtml`.
- `>8` selects Compact with separate `Create.cshtml`, `Edit.cshtml`, `Details.cshtml` and `_Form.cshtml`.
- List surfaces use DataTable v2, skeleton loader, filter, L10n bridge and seven-language RESX parity.
- Navigation must support Portfolio/Initiative/Program/Project context without manufacturing an unapproved
  hierarchy.
- InvestmentCase and BenefitCommitment are separate tenant surfaces, not generic context tabs. Both use
  DataTable v2 and Golden Slim create/edit offcanvas. BenefitCommitment selectors display `Code — Title`,
  never a raw InvestmentCase Guid.
- Browser code cannot embed mock rows or fallback to ES prototype endpoints.
- Initiative remains a Golden Slim surface with the exact eight user fields in §4.3.2. Its current quick view
  may show only the core plus its resolvable Portfolio relationship and lifecycle actions authorized by the
  v2 matrix. The §4.3.1 registry grants no uncontracted card markup, disabled input, mock
  value, client call or endpoint; a later owner-approved amendment is required before any cross-module card
  is rendered.

The four Phase 2A frontend surfaces remain in `review`. Gate L frontend/backend implementation exists at
`536aa68556f165db45d9860444d3de39757b5e58`, including separate InvestmentCase and BenefitCommitment
surfaces and contract tests. This evidence grants neither production activation nor full 1.3 completion.

## 12. Validation Rules

| Field / boundary | Required | Format/rule | DB/pre-check |
|---|---:|---|---|
| Common `Id` | Yes | Canonical non-empty Guid | Tenant-scoped lookup |
| Common `Code` | Yes | Strict decode, Trim → NFC, owner-approved length | Active tenant unique partial index |
| Common name/title | Yes | Strict decode, Trim → NFC, non-empty | — |
| Description | No | Strict decode, Trim → NFC; empty → null | — |
| Lifecycle/state | Yes | Closed owner-approved value | Unknown value fails closed |
| Parent PPM reference | When supplied | Correct typed ID, same tenant, active and visible | Authoritative repository check |
| External contract name | Yes | Exact `ppm.external-context-reference` | Unknown → 400 |
| External contract version | Yes | Exact `1.0` | Unknown → 400 |
| External ContextKind | Yes | Portfolio/Initiative/Program/Project only | Demand/task/workflow/approval rejected |
| External ContextId | Yes | Canonical non-empty Guid | Authoritative referenceability check |
| TenantId/ActorId | No client field | Authenticated server context only | Client value rejected |
| Concurrency Version | Mutations | Positive expected technical version | Atomic compare-and-update |
| Gate I-A decision references | Gate I-A | Governing `0..1` (required before `UnderAnalysis → Closed`); supporting `0..n`; exact approved contract/version/Guid | MOD-0007 authoritative validation |
| Gate I-A ApprovalOutcome reference | Conditional Gate I-A | `0..1`; only when authoritative policy requires; distinct from generic Decision | MOD-0023 authoritative validation |
| Gate I-B funding/scenario references | Gate I-B | Selected BudgetVersion `0..1`; scenario/comparator `0..n`; selected Scenario `0..1` | MOD-0136/MOD-0138 authoritative validation |
| Gate I-C outcome/realization references | Gate I-C | BenefitCommitment `0..n`; planned target only in PPM | MOD-0072 authoritative validation |

Gate I delivery is split into four independently approved executable slices: **I-A Decision**, **I-B Budget**,
**I-B Scenario**, and **I-C Outcome**. §4.9.1 records their completed bounded pure consumer contract-test
handoffs; §4.9.2 now authorizes only internal default-off composition code for I-A Decision, atomic I-B and I-C.
No slice is activation-ready. Provider placement, migration, credentials, live bilateral evidence and explicit
activation authority remain later gates. Conditional MOD-0023 `ApprovalOutcome` is ExcludedV1 and remains a
separate blocked PSS-owned amendment and adapter.

For checkpoint provenance, those four atomic adapters were grouped into three contract-test lanes now composed
at `20c95c16b65b0733120d74020508c13dd56abe22`:

1. **I-A Decision Trace:** MOD-0007 governing/supporting decision contract tests; the conditional MOD-0023
   ApprovalOutcome adapter stays blocked until its amendment promotion, Gate 2 and explicit runtime authority.
2. **I-B Funding & Scenario:** separate MOD-0136 Budget and MOD-0138 Scenario atomic adapters under one
   coordinated lane; neither adapter may infer or copy producer-owned selection/analytical truth.
3. **I-C Benefit Realization:** MOD-0072 outcome-reference contract tests with planned target remaining in PPM.

The next executable/runtime handoff still requires a separately approved MOD-0117 runtime amendment for the
named lane. Cross-lane composition, production activation and legacy page-parity acceptance occur only after
the three lanes close; the bounded contract-test handoff is not runtime implementation authority.

Invalid Unicode, unpaired surrogate and ambiguous normalized values fail closed. Validation, persistence and
unique indexes must use the same normalized values.

## 13. Failure Path to Verify

| Scenario | Expected result |
|---|---|
| Missing/invalid required field | 400; no mutation |
| Duplicate active Code in tenant | 409; no duplicate |
| Same Code in different tenant | Allowed without information leak |
| Missing/soft-deleted/cross-tenant PPM object | 404 |
| Actor cannot see/reference context | 404; existence not disclosed |
| Actor lacks DWS command permission | MOD-0018 returns 403 before provider call |
| Invalid contract name/version/kind/Guid | 400 |
| External provider timeout/unavailable/malformed response | 503; never converted to 404 |
| Attempted MOD-0354 reference replacement | Consumer-owned 409 |
| Stale Version | 409; no silent overwrite |
| Repeated idempotent request | Stable approved outcome; mutation not repeated |
| Same idempotency key with different request | 409; no mutation |
| Local cache contains context while provider is unavailable | 503; cache cannot prove existence/ownership |
| Provider success contains full PPM entity | Contract rejection; success must be minimal typed reference |
| Unknown tenant | Authentication/tenant resolution failure; never default tenant |
| Governing decision missing at `UnderAnalysis → Closed` | Contract/business conflict; transition does not commit |
| Approval policy requires outcome but authoritative outcome is absent/non-terminal | No local approval inference; transition does not commit |
| Gate I dependency returns malformed/unknown-version response | 503; never interpreted as deny, missing or local truth |

Soft deletion of a referenced PPM object prevents new references but cannot delete or rewrite already sealed
MOD-0354 revision/baseline history.

## 14. Authorization Convention

- Authentication: JWT.
- Enforcement owner: MOD-0018.
- Authorization is two independent gates: active tenant PPM module entitlement first, then the required
  canonical `ppm.*` permission. Missing/disabled/suspended/expired/indeterminate entitlement is `403` and
  cannot be bypassed by a stale JWT permission claim.
- Canonical entitlement/catalog identity is exactly `ModuleCode = PPM`; lowercase `ppm.*` values are
  permission keys, not module-code aliases.
- The 16 lowercase-dotted manifest keys may exist in the global AuthService permission catalog; catalog
  presence grants nothing. Only explicit tenant-scoped `RolePermission` grants produce user access.
- PPM is not part of FU9's locked Auth+MDM default grants. Tenant administrators receive no implicit PPM
  permissions; aliases, role-name bypasses, raw token access and hard-coded allow paths are forbidden.
- Entitlement removal leaves role grants dormant rather than deleting them. Entitlement invalidation must
  deny immediately on every instance; token refresh/revocation follows MOD-0018-FU13.
- Current code reality is incompatible with this PPM rule: generic `GrantModuleWithKeysAsync` auto-grants
  Admin/Viewer, while `RevokeModuleAsync`/reconcile deletes module-source grants. These generic operations
  must not process `PPM` unchanged. Existing MDM/other-module behavior remains protected.
- Control Tower selected the PPM-specific strategy. Re-entitlement makes only still-existing explicit grants
  held by current role memberships effective; deleted grants are not reconstructed and no automatic new
  grant is created. Re-entitlement and authorized administrator visibility of the current grant/role
  inventory are audited.
- `IEntitlementChecker` evaluates only module/feature entitlement. AuthService owns grants and JWT permission
  claims; PPM does not recalculate roles, grants or effective permission.
- After both gates allow, missing/soft-deleted/cross-tenant objects return indistinguishable `404`.
- Actor: authenticated tenant user; exact service-to-service identity + actor delegation remains OPEN.
- DWS permission is evaluated by MOD-0018 before MOD-0117 provider invocation.
- MOD-0117 evaluates only PPM context visibility/referenceability; it does not recalculate DWS permission.
- **Phase 2A canonical permission contract (closed set):**
  - `ppm.portfolios.read`
  - `ppm.portfolios.create`
  - `ppm.portfolios.update`
  - `ppm.portfolios.change-lifecycle`
  - `ppm.initiatives.read`
  - `ppm.initiatives.create`
  - `ppm.initiatives.update`
  - `ppm.initiatives.change-lifecycle`
  - `ppm.programs.read`
  - `ppm.programs.create`
  - `ppm.programs.update`
  - `ppm.programs.change-lifecycle`
  - `ppm.projects.read`
  - `ppm.projects.create`
  - `ppm.projects.update`
  - `ppm.projects.change-lifecycle`
- The list above is exact and exhaustive for Phase 2A. Wildcards and alias permissions are forbidden.
  `ppm.portfolios.archive` is not canonical and must be reconciled to
  `ppm.portfolios.change-lifecycle` in the PPM branch before final Phase 2A closure.
- **Phase 2B Gate L canonical permission contract (closed set):**
  - `ppm.investment-cases.read`
  - `ppm.investment-cases.create`
  - `ppm.investment-cases.update`
  - `ppm.investment-cases.change-lifecycle`
  - `ppm.benefit-commitments.read`
  - `ppm.benefit-commitments.create`
  - `ppm.benefit-commitments.update`
  - `ppm.benefit-commitments.change-lifecycle`
- Gate L adds no `.view`, `.delete`, wildcard, alias or uppercase permission. Soft delete remains governed by
  the existing update/surface contract. The external-context validation permission is not part of Gate L.
- Catalog registration may contain exactly these 16 keys under `ModuleCode = PPM`; catalog presence does
  not grant access and cannot add PPM to default Admin/Viewer role templates.
- Service-specific HasPermission/filter/evaluator code cannot be copied from another service.
- Existing shared `X-Internal-Api-Key` alone is insufficient for actor visibility and cannot be adopted as
  the authoritative decision without an approved service-identity + actor-delegation design.

## 15. Gateway / API Routing Decision

**Decision:** `Diten.PpmService` local port `5062`; frontend `5001`; browser entry Gateway `5000`.

**Phase 2A PPM object API Gateway mapping authorized; integration-agent only.** The authorization is limited
to `/api/v1/ppm` and `/api/v1/ppm/{everything}` → port `5062`; it does not authorize provider, DWS, Phase 2B
or any other Gateway route.

- Future browser traffic must use Gateway `5000`; direct backend-port calls are forbidden.
- Existing `/api/v1/delivery-execution*` routes to ES port `5004` are legacy/prototype evidence and cannot
  silently become MOD-0117 routes.
- Phase 2A object APIs and the integration-agent-owned Gateway mapping are implemented and evidenced by
  the targeted route test plus a temporary end-to-end verification chain.
- ExternalContextReference provider endpoint, exact v1 contract, strict S2S validation and bounded lookup
  timeout are implemented at `eddabab0` + `682b0afb`; the endpoint remains internal, default-disabled and
  absent from Gateway/browser routing.
- Production credential provisioning/activation, MOD-0354 consumer/DWS runtime, live compatibility and
  operational evidence remain blocked. Retry/cache/fallback is not authorized by the provider contract.

## 16. Acceptance Criteria

- [x] Phase 2A implements distinct Portfolio, Initiative, Program and Project domain types; no generic
  PpmContext entity exists.
- [x] The recorded lifecycle and visibility rules derive referenceability with soft-delete; standalone
  `IsApproved`/`IsReferenceable` truth does not exist.
- [x] Phase 2A objects are provider-ready; the authorized internal integration provides the exact
  `ppm.external-context-reference` `1.0` runtime contract.
- [x] The default-disabled internal provider enforces dedicated consumer credential plus framework-validated
  delegated JWT in the required order and uses the exact four `.read` permissions.
- [x] Provider v1 returns 404 for non-null `VisibilityPolicyKey` and exposes no policy metadata.
- [x] Invalid contract name/version/kind/Guid returns 400.
- [x] Missing, soft-deleted, cross-tenant, invisible or not-referenceable context returns indistinguishable 404.
- [ ] MOD-0018 DWS command-permission denial returns 403 before provider invocation; this is a MOD-0354
  consumer/DWS runtime acceptance gate, not provider-side PPM `.read` authorization evidence.
- [x] Provider outage, timeout and malformed transport return 503 and never 404.
- [x] Fail-open and local-cache existence/ownership inference are absent.
- [x] Gate L implements distinct InvestmentCase and BenefitCommitment objects with the exact local ownership,
  lifecycle and cardinality above and no external-contract field.
- [ ] Gate I-A consumes approved MOD-0007 governing/supporting Decision references and, only when policy
  requires, a distinct approved MOD-0023 ApprovalOutcome reference; PPM owns no approval behavior/state.
- [x] Conditional approval consumer governance fixes exact four-field
  `InvestmentCaseApprovalOutcomeReferenceV1`, exact three-field nested `ApprovalOutcomeReference`, conditional
  `0..1` ownership and non-selection modes; this acceptance records shape only and grants no runtime authority.
- [x] PPM source-attestation governance fixes the exact 1.0 claims and the PPM pre-submission source `404`
  boundary: missing/cross-tenant/invisible InvestmentCase causes no MOD-0023 call; attestation failures use the
  separate `400/401/403/503` mapping and never become source `404`.
- [ ] Gate I-B consumes approved MOD-0136 selected BudgetVersion and MOD-0138 scenario/comparator references;
  budget/scenario payload and selected truth stay with their producers.
- [ ] Gate I-C consumes approved MOD-0072 outcome/realization references; planned BenefitCommitment stays PPM
  while actual measure/period/evidence/realized state stays MOD-0072.
- [ ] Gate I-A/B/C common security, compatibility, idempotency and no-copy tests pass, followed by the
  integrated browser flow.
- [x] PPM-owned Gate I v1 wrapper/profile names, exact minimal fields, shared InvestmentCase context,
  closed validation modes and 400/401/403/404/409/503 consumer mapping are governance-closed without runtime
  authority or producer ownership transfer.
- [x] All tenant-owned queries/writes use server TenantId, soft-delete filters and tenant-first indexes.
- [x] Optimistic concurrency produces 409 on stale Version.
- [x] Authorized mutations and producer-local audit intent commit atomically in a replica-set transaction.
- [x] Architecture tests prevent ES, Platform, ManagementGovernanceService, MOD-0354 and frontend state from
  becoming a second PPM SoR.
- [x] No Demand, TaskAggregate, task/checklist, WorkCenter, workflow, approval, SLA, escalation, scheduling,
  DWS structure/node/dependency/baseline or capacity ownership is introduced.
- [x] No runtime mock literals such as `init-001`, `prj-001`, candidate IDs or legacy IDs exist.
- [x] `/ppm`, `/ppm/portfolios`, `/ppm/initiatives`, `/ppm/programs` and `/ppm/projects` use
  `_LayoutTenantShell`, DataTable v2, Golden Slim, SweetAlert2 and seven-language RESX parity.
- [x] UI shows lifecycle badge, parent and derived referenceability; has no bulk delete/lifecycle or Approve
  action and excludes tenant/audit fields.
- [x] Legacy Initiative wizard reconciliation is recorded: the former six-field Golden Slim surface is
  superseded by the eight-field Initiative Core v2 Golden Slim contract, and every cross-module concern is
  either a named future owner typed-link contract or default-unavailable;
  no legacy field became an implicit PPM entity field or UI requirement.
- [x] Initiative Core v2 governance fixes exactly eight create/edit user fields; lifecycle and
  `VisibilityPolicyKey` are not user fields. Type/priority are nullable in `Proposed`, required and
  authoritatively MOD-0048-validated before `Active`; the five PPM-owned lifecycle/closure vocabularies reject
  out-of-set values with `400`. Frontend options come only from the contract endpoint without hardcoded fallback.
- [x] Initiative lifecycle transition/permission/reason/closure/Workflow matrix is explicit; terminal records
  cannot reopen, WorkCenter owns no Initiative lifecycle/provider item, and non-approval transitions create no
  WorkCenter item.
- [x] `InitiativeClosure` has the exact seven PPM-owned business fields and preserves MOD-0031, MOD-0024 and
  MOD-0072 ownership without copying their payloads or lifecycle truth.
- [x] Terminal supersession creates a new same-tenant `Proposed` record, leaves the old terminal record
  immutable, and requires terminal-only validation plus duplicate/self/direct/transitive cycle prevention.
- [x] Strategy, ownership, KPI, benefit, budget/scenario, governance/workflow, evidence/document and dependency
  concerns remain authoritative-owner typed links on Details and are not Initiative aggregate snapshots.
- [x] Initiative Core v2 future repo allowlist, protected paths and API/HTTP matrix are exact; this checkpoint
  modifies only this pack and grants no runtime, frontend, Gateway, migration, seed, deployment or activation.
- [x] Browser traffic uses Gateway 5000 only; no direct service-port JavaScript call exists.
- [x] Loading/empty/400/401/403/404/409/503 states are testable without existence disclosure.
- [x] Real Mongo evidence proves tenant isolation, unique indexes, concurrency and required transactions.
- [x] Phase 2A alone does not unblock MOD-0354 runtime; provider compatibility/security evidence is separate.
- [ ] MOD-0117 remains not-done after Gate L or any/all Gate I-A/B/C slices until its Blueprint-wide product
  scope is separately reconciled.

## 17. Test Expectations

### Unit

- Normalization and validation for each proposed field.
- Owner-approved lifecycle transition matrix and derived referenceability.
- Actor visibility decisions and non-disclosure mapping.
- Duplicate active Code and soft-deleted Code policy.
- Optimistic concurrency and idempotency outcomes.
- External contract name/version/kind/Guid validation.
- Gate L lifecycle, immutable-parent, date and no-second-PortfolioId guards.
- Initiative Core v2 exact lifecycle transition table; mandatory cancellation/hold reason and closure guards;
  vocabulary membership/date ordering; terminal immutability; supersession terminal-only/same-tenant,
  duplicate/self/transitive-cycle prevention; notification recipient fail-closed behavior.

### Integration

- Real Mongo tenant/cross-tenant and soft-delete behavior.
- Tenant-first unique/list indexes and concurrency filters.
- Same-tenant/different-tenant duplicate cases.
- Provider 404 versus outage/timeout/malformed 503 separation.
- No fail-open/cache inference.
- Minimal typed success response; full object rejection.
- MOD-0018 403 before provider call.
- MOD-0021 approved audit producer behavior.
- S2S service identity + actor delegation, replay and revocation cases.
- Runtime compatibility tests for supported/unknown contract versions.
- Conditional approval exact-serialization fixtures: wrapper exact four fields, context exact three fields,
  nested outcome exact three fields, and unknown/extra/outcome-version field rejection.
- Conditional approval mode fixtures: `HistoricalResolve` and `NewReferenceEligibility` supported;
  `CurrentSelectionEligibility` returns `400`; source-binding/policy/lifecycle conflicts return `409`.
- PPM source-attestation fixtures prove exact claims, trusted-server-only transport, signing/rotation,
  validity/nonce/replay behavior, PPM source `404` with zero MOD-0023 calls, and attestation
  `400/401/403/503` separation.
- Renewed approval uses a new MOD-0023 instance; a new outcome never silently replaces the old PPM reference,
  and explicit replacement is transactional and audited.
- Initiative Core v2 contract endpoint exact-set round trip; out-of-set `400`; same-tenant and cross-tenant
  supersession; atomic closure/transition and successor-link transactions; MOD-0023 required/not-required
  branches; MOD-0288 verified/missing/ambiguous/unavailable recipient branches; durable `recipient-unresolved`
  audit/outbox plus stable UI warning on unresolved recipient; zero fake recipient and zero WorkCenter
  item/provider projection on direct transitions.

### Architecture and negative tests

- No repository/collection/entity duplication in ES, Platform, ManagementGovernanceService or MOD-0354.
- No task, workflow, approval, WorkCenter, DWS structure or external budget/scenario/outcome lifecycle types.
- No `Workflow*`, free-text external identity or runtime candidate/legacy/mock literals.
- No direct Mongo driver dependency outside Persistence.
- No direct service-port browser calls.
- No Initiative reopen command, normal Initiative WorkCenter provider, hardcoded vocabulary fallback, foreign
  owner snapshot, fake notification recipient, MOD-0024 closure copy or unallowlisted Initiative v2 file.

### Frontend

- Per-surface Slim/Compact verifier after decisions are approved.
- DataTable v2/skeleton behavior where list tables are selected.
- `_LayoutTenantShell` explicitly present.
- Seven-language RESX parity.
- Loading/empty/error/non-disclosure smoke tests.
- Gateway-only browser integration.
- Golden Slim eight-field create/edit parity; lifecycle read-only actions; contract-endpoint-only options;
  no fallback under `401/403/503`; typed-link error-state/non-disclosure checks; terminal action suppression;
  `verify_datatable_page.py . --area PPM --module Initiative --reference slim` and seven-language RESX parity.

### DataTable verifier disposition

The four repeated verifier findings on Portfolio, Initiative, Program and Project are formally dispositioned
as policy/profile mismatches rather than MOD-0117 product defects:

- `BulkDelete` and `BulkDeleteConfirm` are intentionally absent because bulk delete is prohibited for all
  four Phase 2A PPM objects. Their absence must not be remediated by adding a bulk-delete UI or API.
- Browser-side `getAuthHeaders()` is intentionally absent and must not be added. MOD-0117 uses the
  same-origin MVC proxy with the authenticated HttpOnly cookie; browser JavaScript must not read or expose
  the JWT.
- The protected `.antigravity` verifier is not changed by this disposition. Until the verifier supports this
  approved profile, its four findings per surface are recorded as expected advisory/profile findings while
  the applicable DataTable v2, localization, layout and gateway-only checks remain mandatory.

Phase 2A evidence is recorded by the immutable clean PPM checkpoint chain cited in this pack; its historical audit
file is not present on current main and is therefore not linked or treated as current-main evidence. The recorded
results were shared JWT evaluation 21/21, PPM 17/17, targeted Gateway 1/1, isolated Mongo replica-set 9/9,
Web 22/22, delegated jsdom PASS, and real browser CRUD through a temporary full chain. DataTable
verification reports 60 PASS plus four formally dispositioned policy/profile findings per surface.

Later immutable evidence is additive to that historical audit. Gate L checkpoint `536aa685` contains the
current 5 domain, 7 application, 10 Gate L Mongo integration, 5 Web contract and 5 JavaScript contract test
methods for InvestmentCase/BenefitCommitment. Provider checkpoints `eddabab0` + `682b0afb` contain the current
18 contract/security/timeout test methods plus the tenant-first/soft-delete/visibility Mongo lookup test.
These are exact committed test inventories and checkpoint provenance; this governance reconciliation does not
claim a fresh test execution or production activation.

## 18. Ready-for-dev Checklist

- [x] DCP-006 is approved.
- [x] DCP-006 OD-03 is closed.
- [x] MOD-0117 canonical ID/name preflight passed on 2026-07-29.
- [x] Permanent owner role and Phase 2A/2B high-level boundary are recorded.
- [x] ExternalContextReference shape is an approved governance baseline, not a runtime contract.
- [x] Control Tower recorded Phase 2A lifecycle, referenceability and cardinality decisions.
- [x] Phase 2A object/UI scope and Golden Slim selections are approved.
- [x] Gate L exact InvestmentCase/BenefitCommitment fields, states, invariants and cardinalities are approved.
- [x] Gate I-A MOD-0007 exact contract is approved for bounded consumer contract tests; conditional MOD-0023
  remains explicitly blocked/test-plan-only while its amendment is DRAFT / NON-EXECUTABLE.
- [x] Gate I-B MOD-0136 and MOD-0138 exact versioned typed contracts and immutable producer checkpoints are
  approved for the atomic bounded consumer contract-test lane.
- [x] Gate I-C MOD-0072 exact versioned typed contract and immutable producer checkpoint are approved for the
  bounded consumer contract-test lane.
- [x] The three exact NON-RUNTIME consumer contract-test handoffs are authorized in §4.9.1.
- [x] The three immutable contract-test checkpoints are composed at `20c95c16`; §4.9.2 authorizes only a
  bounded, internal, default-off implementation handoff and explicitly excludes MOD-0023 from release V1.
- [x] Current-main semantic reconstruction is checkpointed at `a22a872f`, with MOD-0018 authority and neutral
  shared request binding at `457edbdd` + `92eb29ea`, and PPM-owned default-off Gate I composition at `8c659594`.
- [x] Current-main backend/default-off evidence passes build `0` warnings / `0` errors, unit `286/286`, dynamic-Mongo
  integration `82/82` with `0` skips, architecture `11/11`, and physical mutation `6/6`; restored source SHA-256 is
  `61e79023258a6086db98f52378a7c86bf611f309d71a83979f7368b056d68170`.
- [ ] Gate I runtime composition is activation-ready. Provider placement, migration rehearsal, credentials,
  entitlement/grants, signed audit transport, broker/live evidence and explicit activation authority remain open.
- [ ] Each Gate I production adapter has a later runtime amendment, explicit runtime authority and live bilateral
  evidence after its producer contract-test checkpoint.
- [x] Every Phase 2A frontend surface has an exact field count and Golden Slim decision.
- [x] Phase 2A hub/routes and visibility/referenceability presentation are approved.
- [x] Exact S2S service identity and actor delegation are approved for the internal provider slice.
- [x] Exact physical endpoint and contract version are approved for the internal provider slice; no Gateway route is authorized.
- [x] Provider lookup timeout is exact (`100..5000 ms`, default `2000 ms`); retry/cache/fallback remains
  intentionally unauthorized rather than an open implementation assumption.
- [x] MOD-0018 reusable signed-JWT enforcement integration and PPM adapter are exact and evidenced; real
  AuthService PPM grant provisioning remains open.
- [x] MOD-0021 Minimal Mutation Audit v1 contract and isolated PPM delivery evidence are exact; production
  publisher credential, activation, replay operations and observability remain deployment/runtime gates.
- [x] Backend/default-off idempotency key scope, canonicalization, receipt and retention are implemented and
  evidenced at `8c659594`; live delivery, replay operations and production activation remain open.
- [x] EntityBase CLR/BSON representation and isolated real-Mongo replica-set evidence pass.
- [ ] Full bilateral runtime compatibility/security evidence remains open. The isolated provider-side contract
  and security suite passes, but production activation and MOD-0354/DWS consumer evidence are not complete.
- [x] Service port `5062`, frontend `5001` and browser Gateway `5000` boundaries are approved; the
  integration-agent-owned Phase 2A mapping has targeted and end-to-end evidence.
- [x] Explicit user approval to scaffold `Diten.PpmService` and implement Phase 2A backend/frontend is
  recorded on 2026-07-29.
- [x] Human approval promoted this pack to `approved`; scoped implementation and runtime evidence now
  promote Phase 2A to `review`.
- [ ] Any later WorkCenter hazard has Gate 2 PASS before production change.

Unchecked provider, shared-contract and Phase 2B items do not revoke the scoped Phase 2A authority; they
block only their named runtime boundaries. This pack is not unconditional `ready-for-dev`.

Gate L completion may be reported only as **“1.3 PPM-owned local slice complete; external integration Gate I
remains open.”** Gate I-A completion may be reported only as **“Decision-to-investment trace integrated;
funding, scenario and realization slices remain open.”** Only Gate L + Gate I-A + Gate I-B + Gate I-C +
cross-service security/compatibility tests + integrated browser flow may yield **“1.3 Portfolio, Investment &
Value Management integrated business flow complete.”** No partial gate automatically makes the whole
Blueprint-wide MOD-0117 product `done`; this pack remains `review` until separately reconciled.

## 19. Implementation Notes

- Target `phase-2a` is scoped authorization, not whole-module completion.
- Current-main semantic integration is deliberately split into four reviewable checkpoints: PPM-owned base and
  contract reconstruction `a22a872f`, parent MOD-0018 governance `457edbdd`, neutral shared request binding
  `92eb29ea`, and PPM-owned default-off Gate I relationship/outbox composition `8c659594`.
- Checkpoint `8c659594` was verified with build `0` warnings / `0` errors, unit `286/286`, dynamic-Mongo integration
  `82/82` with `0` skips, architecture `11/11`, and mutation `6/6`; the post-restore source SHA-256 is
  `61e79023258a6086db98f52378a7c86bf611f309d71a83979f7368b056d68170`. This is backend/default-off evidence,
  not full 1.3, browser, live-provider, bilateral, WorkCenter or production-activation evidence.
- DCP-006 is the sole active 1.3/1.4/1.6 orchestration contract. DCP-003 remains deferred/non-executable
  legacy safe-parity planning and gives no implementation authority.
- Registry and master-plan delivery status are reconciled in the same governance closure.
- Master 8.1 places MOD-0117 in Portfolio/Investment and Delivery/Execution context; MOD-0354 remains the
  structural engine. V5 sequences WS-C1 MOD-0117 context before WS-C2 MOD-0354/MOD-0355.
- Current PPM mock adapters use string IDs and static `init-001`/`prj-001` data; resilient wrappers can fall
  back to process-local cache. Neither pattern is authoritative or reusable for validation.
- Existing frontend routes and gateway port 5004 routes remain prototype/legacy evidence. No migration or
  route reuse is implied.
- Existing MDM/Auth reference validators demonstrate typed-client and tenant/bearer propagation patterns,
  but the MDM client collapses dependency/transport failures to 404. MOD-0117 must preserve the 404/503
  boundary.
- Artifact-tool workbook import produced no inspect result in this environment. The canonical verifier passed,
  and previously verified narrow workbook rows in this Control Tower task chain support the identity/sequence
  statements; workbook files were not modified.
- Temporary review topology `:5191 → :5200 → :5061 → :27018` produced real CRUD evidence and is not a
  production URL. Its signed JWT bootstrap was ephemeral and changed no repository/Auth data.

### Open business decisions

1. Business-owner and UX acceptance of the implemented Gate L InvestmentCase/BenefitCommitment slice.
2. Gate I-A/B/C producer contract-owner sequencing, cardinality acceptance and integrated business-flow acceptance.
3. Exact MOD-0031 and MOD-0024 producer contract versions for the already-closed optional `0..n`
   `EvidenceReferences` and `FollowUpTaskReferences`; `BenefitDisposition` is required and PPM-owned.

### Open technical decisions

1. PSS-owned AuthService registration of the 16 PPM catalog keys and explicit tenant-scoped role-grant
   provisioning remains evidenced by PSS-A. The PSS-owned
   `platform.ppm-entitlement-decision.v1` provider uses the fixed `ModuleCode = PPM`, an endpoint-specific
   PPM service credential, and fail-closed `200 allow/deny` versus `503 indeterminate` semantics. The
   provider is default-disabled; disabled deployments return `503` without entitlement lookup, while enabled
   deployments require a valid dedicated secret at startup. Disabled is not a business entitlement deny.
   The PPM-service consumer and final normal-port evidence remain separate work.
2. Executable PSS authority for the selected PPM-specific strategy and MOD-0035 slice; existing generic
   MDM/other-module behavior remains unchanged.
3. ExternalContextReference provider production credential/activation and MOD-0354 consumer/DWS live
   compatibility evidence; the provider implementation and isolated contract/security evidence already exist.
4. Exact allowlisted `PpmAuditIntentSubmittedV1` payload/consumer mapping, authenticated publisher credential
   and MOD-0035/MOD-0021 delivery integration.
5. Gate I-A/B/C bounded default-off composition may proceed only under §4.9.2. Physical provider placement,
   migration execution, credentials, activation and live bilateral evidence remain later owner/runtime gates.
6. MOD-0023 must publish an approved, versioned PPM Initiative approval-policy and immutable outcome contract
   for the policy-dependent transitions; current `ExcludedV1` remains a runtime blocker.
7. MOD-0288 must publish the versioned same-tenant owner/governance recipient resolution contract before
   OnHold notification can exist; no inferred recipient is allowed.
8. MOD-0031 and MOD-0024 must publish exact typed reference contracts/cardinalities for InitiativeClosure;
   MOD-0072 must approve the boundary for `BenefitDisposition` without transferring realized-benefit truth.
9. MOD-0352, the canonical KPI owner, MOD-0136, MOD-0138, MOD-0007, MOD-0028 and MOD-0354 must each provide
   bilateral typed-link contracts before their Initiative Details section can become available.

### Open UI decisions

1. Gate L InvestmentCase and BenefitCommitment interaction models and browser evidence.
2. Later composite Project Workspace integrations are tracked in the
   [R1 PPM MVP backlog](../../../release/release-backlog/R1-ppm-mvp-backlog.md); Phase 2A keeps PPM,
   DWS, WorkCenter, finance, resource/capacity, document, compliance and audit ownership separate.
3. Initiative cross-module detail cards remain a future, owner-by-owner decision under §4.3.1. The first
   implementation amendment must choose only contracts that are executable, tenant-safe and bilaterally
   evidenced; it cannot promote the whole registry at once.

### Amendment — 2026-09-03 — Initiative Core v2 frontend implementation authority

**Authority class:** bounded frontend implementation handoff; governance checkpoint only in this commit.

**Production authority:** `production_authority: none`.

**Activation posture:** default-off / non-activating. No deployment, production configuration, feature
activation, migration, seed, secret, credential, Gateway, backend/service or shared-runtime change is
authorized. The pack remains `review`. This amendment does not itself implement frontend code. A later
frontend implementation run requires separate, explicit user approval against this amendment.

This amendment authorizes a future implementation to project the existing Initiative Core v2 backend
contract into one tenant-shell frontend surface. It does not widen or reinterpret the 2026-09-02 backend
baseline. The Initiative create/edit surface remains exactly eight user-entered fields and therefore uses
Golden Reference Slim:

1. `Code`
2. `Name`
3. `Description`
4. `PortfolioId` (optional)
5. `InitiativeTypeCode`
6. `PriorityCode`
7. `PlannedStartDate`
8. `PlannedEndDate`

Lifecycle is not a ninth form field. It is read-only in the table and quick view and changes only through
separate controlled actions backed by the existing backend transition contract/result.

#### Exact frontend implementation allowlist

Only the following paths may change in the separately approved frontend implementation:

- `frontend/Diten.Web/Controllers/PPM/PpmController.cs` — Initiative same-origin proxy actions only.
- `frontend/Diten.Web/Models/PPM/InitiativeModels.cs`.
- `frontend/Diten.Web/Models/PPM/PpmViewModels.cs` — Initiative projection/configuration only.
- `frontend/Diten.Web/Views/PPM/Initiatives/**`.
- `frontend/Diten.Web/wwwroot/assets/js/PPM/Initiatives/**`.
- `frontend/Diten.Web/Resources/Views/PPM/Initiatives/**`.
- `frontend/Diten.Web/tests/js/ppm-initiative-*.test.mjs`.

No shared PPM partial is currently allowlisted. If implementation proves that an existing shared PPM
partial must change, work stops and a governance amendment must name the exact file and prove that only
Initiative behavior changes; a wildcard over shared PPM surfaces is forbidden.

#### Protected paths and non-authority

Everything outside the exact allowlist is protected, including `.antigravity/**`, `AGENTS.md`, Gateway and
every `ocelot.json`, Platform, Auth, all PPM backend/service sources, DWS, BPM, WorkCenter, Enterprise Strategy
legacy sources, shared layouts, `appsettings*`, `launchSettings*`, migrations, indexes, seeds, secrets,
credentials, deployment/production-activation assets and every other module's frontend files. The future
implementation may not copy legacy Enterprise Strategy persistence or browser state into PPM and may not
add bulk lifecycle or bulk delete.

#### Data, dependency and fail-closed UI contract

- The page explicitly uses `Layout = "_LayoutTenantShell"`, DataTable v2
  (`data-dt-standard="v2"`), Golden Slim create/edit offcanvas, inline filter, Save View, skeleton/loading,
  empty and error states, `_DetailsQuickView.cshtml`, premium SweetAlert2 and seven-language RESX plus
  `window.L10n` bridging.
- Browser requests are same-origin and go through the narrowly scoped `PpmController` proxy to Gateway;
  browser code never calls a service port directly.
- `InitiativeTypeCode` and `PriorityCode` options come exclusively from successful authoritative
  `contracts/v2` payloads. When the provider is unavailable, malformed or indeterminate, create/edit is
  safely disabled and a localized dependency error is shown. Hardcoded arrays, stale cache, synthetic
  defaults and frontend fallback vocabularies are forbidden.
- Planning dates may be null while Proposed. The UI may explain backend activation requirements but cannot
  duplicate or become owner of the business rule; the backend remains authoritative before Active.
- Allowed transitions are driven only by backend contract/result. Cancel, OnHold and Complete collect the
  exact required reason/closure inputs in separate premium SweetAlert2 or equivalently controlled action
  surfaces. Completed and Cancelled rows show no edit, delete or lifecycle actions.
- Completion shows only the exact fields required by the backend. Until executable MOD-0031 evidence and
  MOD-0024 follow-up-reference contracts exist, the UI exposes neither fake inputs nor free-text ID fields.
- A terminal Initiative may expose a separate **Create successor** action. It never edits or reopens the
  terminal record.
- Details/quick view shows PPM-owned core fields. A foreign-owner card appears only when its executable,
  typed, tenant-safe contract exists. A `503` owner-contract condition shows no mock, placeholder or locally
  copied data.
- An Initiative record or lifecycle state never becomes a WorkCenter item. Only a genuine MOD-0023 approval
  item, owned and projected by its separate authority, may appear there.
- HTTP outcomes remain distinguishable: `400` validation, `401` refresh/login, `403` permission,
  non-disclosing `404`, `409` stale version/conflict and `503` dependency unavailable. They must not collapse
  into one generic error message.

#### Implementation acceptance criteria and gates

A separately approved frontend implementation is acceptable only when all of the following are evidenced:

- Exact create/edit parity for the eight fields above; `PortfolioId` is optional; lifecycle is absent from
  the form and read-only in table/quick view.
- Golden Slim and DataTable v2 verifiers pass for `PPM/Initiatives` using the same-origin proxy profile.
- `dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug` and the applicable PPM/Web tests pass.
- All `frontend/Diten.Web/tests/js/ppm-initiative-*.test.mjs` contract tests pass.
- Initiative RESX keys have semantic parity in `en`, `fr`, `es`, `zh`, `ar`, `ru`, `tr`; no English
  placeholders or hardcoded UI fallback text remains.
- Negative scans find no hardcoded type/priority/lifecycle/closure vocabulary, fallback arrays, stale-cache
  option source, synthetic default, direct service-port URL, legacy ES persistence or browser-state copy.
- Same-origin proxy behavior is verified; loading, empty and differentiated error states are exercised.
- Browser acceptance covers create, edit, quick view and the transitions `Proposed -> Active`,
  `Active -> OnHold`, `OnHold -> Active`, `Active|OnHold -> Completed`, and
  `Proposed|Active|OnHold -> Cancelled`.
- Browser acceptance proves terminal immutability, successor creation, authoritative failure on unavailable
  closure/owner contracts and distinct `400/401/403/404/409/503` UI behavior.
- Browser evidence confirms `_LayoutTenantShell`, inline filter, Save View, responsive DataTable v2,
  accessible action controls, premium SweetAlert2 and no console/network contract errors.
- `.local-test/**` and generated artifacts remain uncommitted; secret/credential and artifact scans pass.
- `git diff --check`, exact-allowlist comparison and full staged-diff review pass before commit.

#### Open owner-contract blockers

- `contracts/v2` must return an authoritative, well-formed type/priority payload; otherwise create/edit stays
  disabled with `503`-class dependency UX and no fallback.
- MOD-0031 evidence and MOD-0024 follow-up-reference contracts remain unavailable for closure input until
  separately executable and bilaterally accepted.
- Foreign-owner Initiative Details cards remain unavailable until each owner publishes an executable typed
  link contract; `503` remains the truthful state.
- MOD-0023 is the only possible owner of an approval WorkCenter item. This amendment grants PPM no
  WorkCenter projection or approval implementation authority.

Passing these gates proves only the bounded Initiative frontend implementation. It does not activate
production, close the owner-contract blockers, promote MOD-0117 to `done`, or establish full MOD-0117 parity.

### Correction — 2026-09-03 — Initiative frontend test and verifier disposition

**Authority class:** narrow governance/test correction. **Production authority:** `production_authority: none`.
The pack remains `review`, default-off and non-activating. This correction grants no production-code,
shared-partial, `.antigravity` verifier, backend, Gateway, deployment or activation authority.

For the approved Initiative Core v2 frontend implementation only,
`frontend/Diten.Web/tests/js/ppm-add-new-delegation.test.mjs` is added to the exact test allowlist. Within that
file, only superseded Initiative assertions may change: the existing Portfolio delegated Add New and shared
CRUD proofs must remain intact. Initiative assertions must exercise the Initiative-owned script behavior with
DOM/fetch stubs and prove that table initialization waits for the authoritative `lifecycle-contracts/v2`
response, lifecycle data comes from that endpoint, row lifecycle actions come from record-specific
`availableActions`, no hardcoded transition/vocabulary fallback is introduced, and classification
`contracts/v2` failure disables create/edit save. This authority does not permit `PpmCrud` compatibility code,
hardcoded transitions or vocabularies, bulk UI/API, or changes to any production/shared source.

For this Initiative surface, generic verifier expectations for a select-all checkbox, bulk endpoint, bulk-delete
control and clear-selection lifecycle are **inapplicable by design** because Initiative bulk lifecycle and bulk
delete remain forbidden. Acceptance is based on the applicable same-origin proxy, Golden Slim, DataTable v2,
inline-filter, Save View, loading, empty and differentiated-error gates. “Verifier fully passes” wording in the
preceding acceptance criteria is therefore read as “all applicable verifier gates pass”; the named forbidden-bulk
findings are recorded advisories and must never be remediated by adding bulk behavior or changing the protected
verifier.

### Correction — 2026-09-03 — Initiative lifecycle contract prerequisite

**Authority class:** governance correction / executable frontend blocker. This correction does not amend
`c9799a14`; it is a separate checkpoint and supersedes any reading of the preceding frontend amendment that
would allow Initiative lifecycle UI implementation or acceptance to start against the current backend
contract.

**Observed executable contradiction:** `InitiativeV2Dto` and `InitiativeLifecycleResult` do not currently
carry authoritative `AllowedTransitions` or an equivalent lifecycle-action contract, while the existing
Initiative frontend hardcodes a transition matrix. In addition, the current `InitiativeService.GetContracts()`
resolves the MOD-0048-owned type and priority authorities before returning the combined payload. If either
classification provider is unavailable, the whole request returns `503`, also withholding PPM-owned
cancellation reasons, hold reasons, completion outcomes, closure reasons and benefit dispositions. Therefore
server-driven lifecycle actions, no hardcoded lifecycle/closure vocabulary and passing lifecycle browser
acceptance cannot all be achieved with the current executable contract.

#### Corrected ownership and contract boundary

- MOD-0048-owned Initiative Type and Priority remain on the existing `contracts/v2` fail-closed boundary.
  Their unavailable, malformed or indeterminate provider state continues to produce `503`; no cache,
  synthetic option or frontend fallback is permitted.
- The PPM-owned lifecycle transition matrix and PPM-owned reason/closure vocabularies must not depend on
  MOD-0048 classification-provider availability. They remain PPM-owned authoritative server contracts and
  must never be hardcoded in frontend code.
- Before Initiative frontend implementation begins, an additive PPM-owned lifecycle contract endpoint or
  equivalent authoritative server projection must be implemented. The preferred contract is
  `GET /api/v1/ppm/initiatives/lifecycle-contracts/v2`.
- The lifecycle contract must carry, at minimum: allowed target states by source state; cancellation reasons;
  hold reasons; completion outcomes; closure reasons; and benefit dispositions.
- This PPM-owned endpoint/projection must be independent of the MOD-0048 classification provider. An unknown,
  malformed or internally indeterminate lifecycle contract returns `503`; frontend fallback remains forbidden.
- When actor, permission, policy or record data affect action eligibility, the list/detail projection must
  additionally carry server-calculated record-specific available actions. Frontend may not infer an
  authorization result from the general state matrix.
- Transitions requiring MOD-0023 approval retain their existing fail-closed boundary. This correction neither
  invents an approval outcome nor weakens the `503` behavior while authoritative approval evidence is absent.

#### Authority and sequencing correction

This correction grants **no backend implementation authority** and changes no runtime source allowlist.
Implementing the prerequisite requires a separate, exact backend allowlist amendment and separate explicit
user approval. Until that approved backend prerequisite is implemented and contract-tested:

- the Initiative frontend implementation authorized in `c9799a14` must not start;
- the `c9799a14` frontend allowlist remains governance-defined but non-executable;
- frontend lifecycle/closure browser acceptance, terminal-action visibility and successor-action acceptance
  remain blocked and must not be reported as passable or passed;
- hardcoded transition matrices or PPM-owned reason/closure vocabularies remain prohibited; and
- create/edit classification behavior continues to use the independent existing `contracts/v2` fail-closed
  MOD-0048 boundary.

After the prerequisite is implemented under separate authority, frontend implementation still requires the
separate explicit user approval already required by `c9799a14`. The pack remains `review` with
`production_authority: none` and default-off/non-activating posture.

### Amendment — 2026-09-03 — Initiative Lifecycle Contracts v2 exact backend prerequisite authority

**Authority class:** bounded backend prerequisite implementation handoff; governance checkpoint only.

**Production authority:** `production_authority: none`.

**Activation posture:** default-off / non-activating. The pack remains `review`. This amendment implements no
backend, frontend, runtime or test code. Backend implementation requires separate explicit user approval;
frontend implementation remains blocked until the prerequisite is merged and its contract gates pass.

This amendment authorizes only an additive PPM-owned lifecycle discovery contract. It does not alter mutation
rules, create a second policy engine or transfer approval ownership from MOD-0023.

#### Endpoint and lifecycle contract

The exact authorized endpoint is `GET /api/v1/ppm/initiatives/lifecycle-contracts/v2`. A successful response
contains at least the contract version; allowed target states grouped by every canonical source state; and,
for every transition, source state, target state, required companion-data kind and approval-dependency
disposition. It also contains the exact cancellation reasons, hold reasons, completion outcomes, closure
reasons and benefit dispositions.

Required companion-data kind is the closed set `none`, `cancellation-reason`, `hold-reason`, `closure`.
Approval-dependency disposition is the closed set `direct`, `approval-authority-required`. States and
vocabularies are generated server-side from canonical Initiative transition rules and
`InitiativeVocabularies`; consumers do not keep copied sets. Duplicate, blank, unknown or internally
indeterminate values fail the whole contract as `503` and are never silently skipped.

Missing/invalid authentication returns `401`; permission denial returns `403`; malformed, unknown or
internally inconsistent lifecycle authority returns `503`. Tenant context remains mandatory, the endpoint
emits no cross-tenant record data, and frontend fallback or a synthetic contract is forbidden.

#### MOD-0048 separation

- Lifecycle-contracts v2 never calls MOD-0048 Initiative Type or Priority providers and returns canonical
  PPM-owned lifecycle data with `200` while MOD-0048 is unavailable.
- Existing classification `GET /api/v1/ppm/initiatives/contracts/v2` behavior is unchanged: Type/Priority stay
  MOD-0048-authoritative and unavailable, malformed or indeterminate authority remains `503`.
- Create/edit consumers still use classification `contracts/v2` and close safely on its failure. Lifecycle
  availability cannot substitute Type/Priority values.

#### Record-specific action projection

The general matrix is descriptive, not user/tenant/record authorization. List and detail `InitiativeV2Dto`
therefore carry server-calculated actions. Every action contains at least target state, availability, stable
reason code and required companion-data kind. Availability is the closed set `available`, `forbidden`,
`dependency-unavailable`, `record-not-ready`.

- `forbidden`: the backend actor/tenant access authorizer denies lifecycle mutation. Read permission does not
  imply mutation permission, and frontend code never derives the result from JWT claims.
- `dependency-unavailable`: `Proposed -> Active` and `OnHold -> Active` while required MOD-0023 policy/outcome
  authority is unavailable; also `Active|OnHold -> Cancelled` while required approval authority is unavailable.
- `record-not-ready`: activation readiness data is incomplete independently of permission/dependency state.
- `available`: only a direct transition allowed by canonical domain rules, actor/tenant authorization,
  readiness and dependency results.

Terminal records expose no lifecycle/edit/delete actions. The projection is UX guidance only; mutation
endpoints independently re-run permission, tenant, CAS/version, readiness, canonical-transition and dependency
checks and never trust projected availability as authority.

#### Exact future backend implementation allowlist

Only these paths may change in the separately approved prerequisite implementation:

- `services/Diten.PpmService/src/Diten.PpmService.Api/Controllers/InitiativesController.cs` — lifecycle GET only.
- `services/Diten.PpmService/src/Diten.PpmService.Application/Features/Initiatives/InitiativeContractsV2.cs` —
  additive classification-contract separation only, if required.
- `services/Diten.PpmService/src/Diten.PpmService.Application/Features/Initiatives/InitiativeLifecycleContractsV2.cs`.
- `services/Diten.PpmService/src/Diten.PpmService.Application/Features/Initiatives/InitiativeLifecycleTransitionContract.cs`.
- `services/Diten.PpmService/src/Diten.PpmService.Application/Features/Initiatives/InitiativeActionAvailability.cs`.
- `services/Diten.PpmService/src/Diten.PpmService.Application/Features/Initiatives/IInitiativeLifecycleContractAuthority.cs`.
- `services/Diten.PpmService/src/Diten.PpmService.Application/Features/Initiatives/InitiativeV2Dto.cs`.
- `services/Diten.PpmService/src/Diten.PpmService.Application/Features/Initiatives/InitiativeMapping.cs`.
- `services/Diten.PpmService/src/Diten.PpmService.Application/Features/Initiatives/Services/InitiativeService.cs`.
- `services/Diten.PpmService/src/Diten.PpmService.Application/Features/Initiatives/Queries/GetInitiativeLifecycleContractsV2Query.cs`.
- `services/Diten.PpmService/src/Diten.PpmService.Application/Features/Initiatives/Handlers/QueryHandlers/GetInitiativeLifecycleContractsV2Handler.cs`.
- `services/Diten.PpmService/src/Diten.PpmService.Domain/Initiatives/InitiativeVocabularies.cs`.
- `services/Diten.PpmService/tests/Diten.PpmService.Tests/Initiatives/**`.
- `services/Diten.PpmService/tests/Diten.PpmService.IntegrationTests/Initiatives/**`.

No additional model file is authorized. If one is necessary, work stops until a later amendment names the
exact file under `Application/Features/Initiatives/`. No broader PPM-service wildcard is permitted.

The allowlisted `IInitiativeLifecycleContractAuthority` is a PPM-owned canonical contract-production and
fail-closed test seam only. It may be used to inject malformed, duplicate, blank or unknown lifecycle-contract
fixtures without mutating shared backing arrays or making parallel tests order-dependent. It grants no external
provider, alternate system of record, DI registration, cache, Persistence, Infrastructure, frontend, Gateway,
configuration or production-activation authority. Production behavior continues to use the PPM-owned canonical
builder; the seam cannot replace lifecycle ownership or weaken validation.

#### Protected paths and non-authority

Everything outside the exact allowlist is protected, especially Persistence/Mongo/index/migration/seed,
Infrastructure/DI, Gateway, frontend, Platform, Auth, MOD-0023 implementation, WorkCenter, `appsettings*`,
`launchSettings*`, secrets/credentials, deployment/production activation, `.antigravity/**`, `AGENTS.md`,
other modules and services. No permission/catalog change, route, configuration, migration or activation is
authorized.

#### Acceptance and test gates

- With MOD-0048 unavailable, lifecycle-contracts v2 returns `200` with exact canonical PPM-owned payload;
  existing classification `contracts/v2` remains `503`.
- Contract version, companion-data and approval-dependency values match the closed sets above.
- The transition matrix exhaustively equals canonical domain `CanTransitionTo`; adding a state breaks the
  exhaustive test until intentionally contracted.
- Reason/closure vocabularies equal exact canonical `InitiativeVocabularies` sets.
- Duplicate, blank, unknown or inconsistent values produce `503`, never partial output or fallback.
- Read-only actors receive `forbidden`; terminal records have no actions; `record-not-ready` and
  `dependency-unavailable` are tested separately, including the named MOD-0023-dependent transitions.
- Mutation endpoints retain server-side permission, tenant, CAS/version, readiness, transition and dependency
  checks; `401/403/503` and tenant non-disclosure behavior are verified.
- Targeted Initiative unit tests pass. Disposable Mongo/HTTP integration, if necessary, uses only existing
  Initiative test infrastructure within the allowlist.
- PPM build is zero-warning/zero-error. Existing baseline failures are separately attributed and are not
  claimed as newly introduced.
- `git diff --check`, exact allowlist, full staged-diff review, secret/artifact scans pass; `.local-test/**`
  and generated artifacts remain uncommitted.

Passing these gates proves only the default-off backend prerequisite. It does not activate production,
authorize frontend implementation, close MOD-0023 authority or promote MOD-0117 to `done`.

<a id="portfolio-first-delivery-draft"></a>

### Amendment — 2026-09-08 — Portfolio first delivery — DRAFT / NON-EXECUTABLE

**WP:** `PPM-PORTFOLIO-GOV-01`, prompt v1; narrow correction v2 on 2026-09-09; PPM-GOV / DEV;
Profile B, governance-only. Technical-report reconciliation: 2026-09-09.
**Amendment status:** `draft`; **implementation authority:** none; **production authority:**
`production_authority: none`. This is an amendment of existing canonical `MOD-0117 — Project & Portfolio
Management (PPM)`, not a new module/FU. Pack-wide `status: review`, earlier scoped approvals, delivered
slices, Initiative decisions and other surfaces' Golden choices remain unchanged. No implementation-ready
allowlist is established. Only this pack and the linked control plan are writable in this work package.

#### Source, authority and unresolved source documents

Source: the user's relayed 8 September 2026 “Portfolio İlk Teslimat Kararları” response, attributed by the
user to **Natig Yusubov / CEO**. The business decisions and activation clarification below are recorded as conveyed; attribution is
not independent signature verification, an updated v1.5.7 ZIP, formal approval of that external package or
repository implementation authority. The historical ZIP/checker evidence remains the control plan's §2
record; it was not rerun in this amendment. Repository authority remains Module Pack > Domain Config >
AGENTS.md > `.antigravity`; Blueprint Master 8.1 remains the identity authority.

The source-dependent **OD-04/07 (finance) and OD-05 (confidentiality) below belong to the external PPM
Governance Pack**, not the same-numbered DCP-006 decisions. None is closed by implication here.
Authoritative SOP-0029 levels and role/visibility mapping, the Portfolio-applicable SOP-0004 scale/version,
and the Performance Status runtime codes/provider remain OPEN. Repository filename/content checks found
SOP-number references/registries, not a verified Portfolio-applicable scale or access matrix; similarly
numbered QMS/PV documents are not substituted. No level, rating value, role, creator-only rule or fallback
is invented. The control plan tracks execution dependencies and deferred integrations in
[§10](../../../../docs/records/audits/2026-09/dcp-006-ppm-governance-reconciliation-control-plan.md#portfolio-first-delivery-control).

#### Business decisions and first Draft delivery boundary

Correction v2 explicitly preserves the activation requirement conveyed by the user from the 8 September
response: “Strategic objective, accountable owner, funding ceiling and review frequency all set.” These
are four cumulative business prerequisites for activation, not proof of current implementation or a rule
making all four mandatory on Draft save. The financial requirement is governed by the MOD-0136 typed-link
and external OD-04/07 boundary below; it does not authorize a PPM-native Funding Ceiling field.

| Topic | Conveyed business decision | Remaining boundary / dependency |
|---|---|---|
| Budget / activation | Draft record management may be the first partial delivery. Draft → Active must remain closed until a typed reference to a verified MOD-0136 budget/funding commitment is established. No PPM-native Funding Ceiling field before external OD-04/07 close. | Strategic Objective, accountable owner, verified funding condition and Review Frequency are cumulative activation prerequisites; a budget reference alone is insufficient. Other applicable access/approval conditions also remain. Finance custody, approved version, amount/currency, freshness and validation contract remain OPEN. Draft acceptance is neither full Portfolio acceptance nor Budget integration closure. |
| Review Frequency | A review frequency must be set before activation; this business requirement was explicitly conveyed in correction v2. | Unit, controlled values, default, storage type and create/edit versus separate-action placement are OPEN; no source decision for these details is established. Draft-save requiredness is not decided by activation requiredness. No automatic calendar, notification or Workflow job is implied. |
| Confidentiality | SOP-0029 governs. Seeing a record is separate from changing or approving it. The conveyed interim intent is the most restrictive level until external OD-05 closes. | Actual levels and role/visibility mapping have not been supplied, so this intent is **not executable**. Draft records also require an authoritative access policy; Draft delivery cannot bypass this dependency. Unknown policy must safely deny the affected operation/disclosure in the future implementation. |
| Performance Status | Portfolio owner manually chooses **Ahead of Plan / On Track / At Risk / Off Track**, with mandatory rationale and full audit. | These are business labels only, not approved runtime codes, defaults or a selected authoritative vocabulary provider. General update permission does not prove record ownership. Technical provider/code/change-governance design may proceed without waiting for SOP-0004/SOP-0029 sources; risk-scale and access acceptance still need those sources. Automatic calculation is a separate OPEN follow-up. |
| Portfolio Risk | Portfolio owner manually evaluates against SOP-0004, with mandatory rationale and full audit. | The actual applicable approved scale is OPEN; no values are synthesized. No automatic risk aggregation is included. Record owner/actor validation is a separate prerequisite. |
| Capacity Allocation | Expected, **not mandatory**; first delivery uses a free-text explanation. | No reservation, person assignment or capacity calculation. Empty explanation does not alone block Draft save. Length/validation and final placement are OPEN technical design; the real resource model/integration is deferred. |

The intended partial delivery comprises governed Draft create/read/update and the above manual assessment
and descriptive behavior where their contracts are resolved. This is a **scope draft**, not an executable
subset selected around missing dependencies. Access, owner validation and required vocabularies must be
resolved before affected behavior can be implemented and accepted. Full activation, finance ownership,
automatic performance/risk, resource integration, other PPM surfaces and WorkCenter approval/lifecycle
ownership remain outside this first Draft acceptance. PPM owns lifecycle; MOD-0023 owns approval execution;
WorkCenter can only display a real approval work item under its own contract and gains no new authority here.

#### Form inputs, separate actions, read-only details and deferred links

| Classification | Portfolio item | Counting / design disposition |
|---|---|---|
| Verified current create/edit inputs | `Code`, `Name`, `Description` | **3** user inputs. Code required/max 64; Name required/max 200; Description optional/max 2000; existing normalization and tenant uniqueness retained. |
| Proposed descriptive input | Capacity Allocation explanation | Expected, optional. Count it if placed in create/edit; final form and validation are OPEN. Do not infer a final total from this row. |
| Separate assessment actions | Manual Performance Status and Portfolio Risk; each with mandatory rationale | Action inputs do not count in create/edit. Applicable lifecycle states, DTOs, paths, permissions and versioned vocabulary contracts remain OPEN. |
| Separate lifecycle action | Draft → Active and other existing permitted transitions | No editable status shortcut in create/edit; Strategic Objective, accountable owner, verified funding condition and Review Frequency plus applicable authorization/approval gates remain necessary. |
| Read-only/system detail | Current lifecycle, technical Version, IDs, tenant, timestamps, audit attribution/history, latest assessment | Not create/edit inputs; authoritative audit/assessment data only. Technical CAS Version is not an approved business revision. |
| Required before activation; placement OPEN | Review Frequency | Business requirement exists; unit, controlled values, default, storage type and create/edit or separate-action placement are OPEN. Count only if user-entered in create/edit after placement is agreed; no inferred Draft-save requirement or automation. |
| Typed strategic reference | Strategic Objective — MOD-0352 / ESBP | Required before activation; Portfolio-consumable provider/version/access contract remains unverified/OPEN. Separate from Organization; read-only provider data is not counted. |
| Typed organization reference | Organization — MOD-0288 / Organization-FU02 (Claude lane) | Typed identity/access/selection contract remains OPEN; it cannot supply Strategic Objective authority. Read-only provider data is not counted. |
| Record responsibility | Accountable Portfolio owner | Required before activation; PPM record responsibility and authoritative person/account/access validation must be reconciled. `CreatedBy` is not owner identity. Placement and Draft-save requiredness remain OPEN; no free-text identity or mock option. |
| Deferred connection | MOD-0136 budget/funding commitment and future resource integration | No editable local Funding Ceiling or invented external payload. Budget is tracked only in control-plan §10.5. |
| Pending access design | SOP-0029 classification/policy | No made-up level selector; existing `VisibilityPolicyKey` is not evidence that OD-05 is implemented. Placement/count awaits the real contract. |

**Full Portfolio form count: OPEN; full-form Golden decision: OPEN.** The first partial user delivery now
has a [four-input Slim proposal](#portfolio-user-delivery-bundle), pending the single bundled approval.
The historical 13/15 estimates are not
used. Current three-input Portfolio is compatible with Slim; after the complete target create/edit inventory
is settled, ≤8 selects Slim and >8 Compact. Pack frontmatter `form_field_count: 8` remains the existing
composite-slice maximum and is not repurposed as the target Portfolio count. Tenant shell remains explicit
`Layout = "_LayoutTenantShell"`, DataTable v2 and seven-language localization remain required. The Slim
reference pack and live frontend/backend were read as structural references, not as new approval authority.

#### Narrow code evidence, retained behavior and future deltas

Read-only evidence at **`e72701fa565942187e7dd85c1e92fdcc25080969`**, in the user-designated worktree on
2026-09-08; this is not a claim about a freshly fetched remote main or new runtime test results.

| Observed source | Code reality / retained foundation | Required future delta; not implemented by this amendment |
|---|---|---|
| `services/Diten.PpmService/src/Diten.PpmService.Domain/Entities/Portfolio.cs` (`CanTransitionTo`); `services/Diten.PpmService/src/Diten.PpmService.Application/Features/Portfolios/Services/PortfolioService.cs` (`Transition`) | Code permits Draft → Active, Draft/Active → Archived; Archived is terminal. Service checks general lifecycle permission, tenant lookup and transition matrix. | Enforce Strategic Objective, accountable owner, verified MOD-0136 funding condition and Review Frequency at activation, retaining applicable access/approval conditions. Review Frequency is not present in the inspected Portfolio entity/service; no implementation is claimed. **The new budget block is not currently implemented.** Do not relabel the existing matrix as proof of the new governance model. |
| Same `PortfolioService.cs` (`Update`, `GetById`, `List`) and Portfolio entity | General resource permissions and tenant lookup exist; entity has no Portfolio owner/assessment model. | Record-specific owner/actor and SOP-0029 visibility decisions for reads and mutations, including Draft; update/read grants are not proof of ownership or confidentiality enforcement. |
| `frontend/Diten.Web/Views/PPM/Portfolios/_CreateEditOffcanvas.cshtml` → `Views/PPM/Shared/_CreateEditOffcanvas.cshtml` | Code, Name, Description are current user inputs; lifecycle and visibility controls are disabled; IDs/Version are hidden. | Add only later-contracted inputs/actions; disabled UI controls are not server authorization. No field-count extrapolation from shared forms for other types. |
| `services/Diten.PpmService/src/Diten.PpmService.Domain/Entities/EntityBase.cs`; `services/Diten.PpmService/src/Diten.PpmService.Persistence/Repositories/MongoRepository.cs` | Local PPM EntityBase; authenticated tenant/actor IDs; UTC audit fields; technical Version; tenant/IsDeleted filter and expected-version CAS on replacement. | Preserve the local base, tenant isolation, soft-delete and CAS for every future assessment/lifecycle mutation; no migration or imported base class. |
| `PortfolioService.cs` (`Persist`, `SoftDelete`); `services/Diten.PpmService/src/Diten.PpmService.Persistence/PpmUnitOfWork.cs` | Mutation and local audit intent share a transaction; exception path aborts; soft-delete retains its InvestmentCase dependency/fence checks. | Full assessment audit needs rationale, previous/new assessment, actor, time, record/tenant and relevant version provenance. Existing minimal mutation audit is not proof of this full history. PPM owns the assessment/rationale business model and atomic mutation/history/intent/Version persistence; the MOD-0021 owner contract governs transport schema and confidentiality/redaction boundaries. Do not silently expand the shared envelope. |

Compatibility with existing **Active/Archived** records is OPEN and belongs to the PPM business/technical
owners, consulting access/finance owners for their policy/funding constraints: assess read/update/assessment/referenceability
and archive behavior under the new access/funding rules, including missing historical policy/funding data.
No auto-demotion, backfill, default classification, grandfathered activation or destructive conversion is
authorized. Existing history and links must be preserved; no migration is performed. Existing Draft/Active
referenceability is observed baseline behavior, not proof that the new confidentiality policy is satisfied.

#### PPM responsibilities and shared contracts — no implementation assignment

PPM owns the Portfolio assessment/rationale business model; atomic mutation, relevant local history/audit
intent and Version persistence; and idempotency/CAS when applying an authoritative Workflow outcome.
At mutation time PPM rechecks authorization, record version and business prerequisites. Workflow-side
idempotency does not replace PPM mutation idempotency. Repeated delivery must not apply the same business
effect twice; no exactly-once delivery guarantee is asserted. Required local transaction failure rolls back
the mutation; post-commit external audit transport failure uses the agreed durable retry/consumer contract.

#### Technical-report reconciliation — observed contracts and bounded gaps

Source: the user's supplied corrected Claude READ-ONLY AUDIT at reported HEAD b6fcfe97, attachment
03215463-4a21-469d-b99b-dbb8508123a7/pasted-text.txt, reconciled on 2026-09-09. That report's main/branch
comparison is its own historical assertion, not a fresh main verification in this worktree. The narrow
source paths below were also read at this worktree's e72701fa565942187e7dd85c1e92fdcc25080969.
No runtime tests were run and no reported D1–D10 item becomes an approved work order.

- **Actor semantics:** WorkflowTaskTransitionSupport sets ActionedBy to the action actor on approve/reject;
  it is not a preparer. StartedBy is the initiator and feeds existing submitter SoD, not authoritative PPM
  preparer identity. Other task actions also set ActionedBy, so it is not universally an approver field.
  FU02 does not supply Portfolio record responsibility.
- **Exact instance read exists:** GET /api/v1/workflow/instances/{id:guid}, protected by
  WorkflowPermissions.InstancesView, reads the exact instance; GetWorkflowInstanceByIdHandler returns
  non-leaking 404 for absent/invisible records. TaskApprovalService is an internal MOD-0024 service, not
  PPM's external contract. EvaluateWorkflowTransitionGateHandler uses GetLatestByObjectRefAsync and cannot
  substitute for an exact approval binding.
- **Version/time evidence:** StartWorkflowInstanceHandler requires a Published, Immutable template version
  and pins TemplateVersionId. This is not PPM record Version. WorkflowInstanceDto has CompletedAt and
  LastTransitionAt; generic completion time is not a complete approval outcome attesting decision actor,
  reason, requested operation and relevant PPM revision. Those outcome/binding semantics remain OPEN.
  Existing conditional InvestmentCase approval-reference governance in §4.9.1 remains non-executable and
  is neither promoted nor assumed to be a Portfolio contract.
- **Audit is not absent:** AuditService.BuildPayload redacts BeforeState, AfterState and Metadata via the
  general pattern-based SensitiveFieldRedactor. This is not SOP-0029 level/role policy evidence.
  POST /api/v1/platform/audit/events has permission platform.audit.events.append, tenant-mismatch rejection
  and idempotent append/Duplicate response. GovernedAuditAppendRequest lacks BeforeState/AfterState;
  that transport shape alone does not mandate a Platform API extension or replace this pack's §8 transport.
- **BRD consumer/error contract exists:** GET /api/v1/reference-data/sets/{setCode}/published-values
  (scope_key) and /values (scope_key, version, as_of_date and include flags) use the existing
  Platform.BusinessReferenceData.Consumer.Read permission. Published payload is SetCode, VersionNumber,
  PublishedAt and Items(Code, Label, Description, IsActive, SortOrder, Attributes) in Response<T>.
  BusinessReferenceDataExceptionBehavior is registered in Application DependencyInjection;
  GetBusinessReferenceDataPublishedValuesQuery implements IBusinessReferenceDataRequest. Coded errors
  already map, including no_published_version → 404 and specified dependency errors → 503; unknown
  exceptions go to the global pipeline. Specialized resolver timeout rules are not a blanket guarantee
  for every published-values failure. The missing Portfolio set/code/version/scope binding is separate.
- **Identity adapter's proven narrow gap:** AuthServiceUserReferenceValidator forwards caller bearer and
  tenant to GET /api/users/{userId}/lookup-validation. Invalid/unreferenceable results, non-success HTTP
  and handled transport/parse failures collapse to the same 404. It is fail-closed but does not preserve
  definitive-denial versus unavailable/indeterminate semantics; caller cancellation is rethrown.

Source files are under services/Diten.Platform/src/: API/Controllers/WorkflowDefinitionsController.cs,
API/Controllers/Platform/PlatformAuditAppendController.cs, API/Controllers/BusinessReferenceDataController.cs
(each project prefix is Diten.Platform.API); Application/Features/Workflow/WorkflowModels.cs and
Handlers/CommandHandlers/StartWorkflowInstanceHandler.cs, WorkflowTaskTransitionSupport.cs,
Handlers/QueryHandlers/GetWorkflowInstanceByIdHandler.cs, EvaluateWorkflowTransitionGateHandler.cs;
Application/Features/Audit/AuditService.cs, AuditAppendApiModels.cs; Application/Features/BusinessReferenceData/
BusinessReferenceDataExceptionBehavior.cs, Queries/GetBusinessReferenceDataPublishedValuesQuery.cs,
Models/BusinessReferenceDataStewardshipModels.cs; Application/DependencyInjection.cs and
Infrastructure/Services/Auth/AuthServiceUserReferenceValidator.cs (project prefixes Diten.Platform.Application
and Diten.Platform.Infrastructure respectively). These are read-only evidence paths, not a write allowlist.

<a id="portfolio-four-design-proposals"></a>

#### Four concrete Portfolio design proposals — PROPOSED / DRAFT / NON-EXECUTABLE, 2026-09-09

These replace the earlier option sketch with one recommended approach per design. They are not DECIDED,
implemented fields, published data, endpoint contracts or implementation authority. Every model/field
name below is **proposed, not existing**, unless explicitly identified as an existing source contract.
The existing Portfolio Version/CAS and §8 Minimal Mutation Audit v1 are retained, not redesigned.
Business decisions are listed for approval in the control-plan
[decision table](../../../../docs/records/audits/2026-09/dcp-006-ppm-governance-reconciliation-control-plan.md#portfolio-four-design-decisions).
This section neither approves a ready subset nor changes earlier scoped approvals.

<a id="portfolio-owner-proposal"></a>

##### 1. Accountable owner — one PPM responsibility with a named User principal

**Recommended approach:** PPM owns the assignment and its history; for this first delivery recommend a
single named human User account as the typed principal. Existing authenticated actor identity and account
validation make the actor-to-owner comparison explicit without inventing Person/account or Position/holder
resolution. This is a business choice about accountability at account level, not proof that User is the
permanent enterprise responsibility model. Active account validation proves reference eligibility only;
the PPM assignment plus effective validity and operation authorization prove record responsibility.

| Principal option | Business meaning / limit |
|---|---|
| User — recommended for first delivery | A specific named login is accountable and can be compared to the authenticated assessment actor. Account disablement affects ability to act; it does not erase responsibility history. Service/shared accounts must not qualify; authoritative eligibility evidence is still required. |
| Person | Accountability follows the person across account changes. Requires an authoritative same-tenant Person → active acting-account mapping and validity rules; no automatic link is proven. |
| Position | Accountability follows an organizational office. Requires effective holder, vacancy, acting/delegated holder and multiple-holder decisions; position membership alone cannot authorize assessment. FU02 is not the PPM assignment. |

**Proposed responsibility record, not existing fields:** stable assignment identity, Portfolio reference,
typed principal kind/ID, valid-from UTC, optional valid-to UTC, assigning actor/time, assignment/transfer
reason, and PPM Version before/after. Server-resolved tenant is mandatory; neither tenant nor acting actor
comes from a caller-supplied identity. Reference IDs cannot silently change meaning across principal kinds.

- Recommend **zero or one effective owner while Draft**, with absence shown truthfully; **exactly one
  eligible effective owner at activation**. Draft optionality is an approval proposal, not a new decision.
  Optional owner does not relax SOP-0029 visibility or permit assessment without a verified owner.
- Recommend assignment/transfer as a separate controlled action, not ordinary metadata edit. Transfer
  validates the replacement and atomically closes the old interval and opens the new one at server time,
  with reason/history/CAS. No overlapping effective owners, future scheduling or backdated assignment in
  the proposed first delivery. These are proposed lifecycle constraints, not implemented restrictions.
- **APPROVED BUSINESS DECISION — assigning persona only (2026-09-10):** the Portfolio owner is assigned
  by a person authorized on behalf of portfolio management/PMO. The user confirmed decision authority
  and explicitly approved this business persona. General system administration, update permission or
  current ownership does not automatically grant assignment/transfer rights. Transfer semantics remain
  proposed; no existing PMO role/permission/grant is asserted. Exact permission mapping remains technical
  preparation. See the [bounded owner implementation proposal](#portfolio-owner-assignment-boundary).
- Only the effective owner acting through that verified account, with assessment entitlement/permission
  and SOP-0029 access, performs manual performance/risk assessment. Assignment authority does not itself
  grant assessment authority. No CreatedBy, general-update, shared-account or unspecified delegation
  fallback. This preserves the already agreed owner-only manual assessment rule.
- Disabled/deleted/ineligible account, expired assignment or indeterminate validation prevents new
  assessment and activation. Preserve the old attribution and show unresolved/ineligible state only to
  authorized readers; a designated administrator can transfer after independently satisfying access and
  replacement checks. Do not silently select a replacement, demote Active, delete history or deny every
  unrelated authorized Draft operation. Existing Active/Archived compatibility remains the existing
  [§10.6–10.7 decision](../../../../docs/records/audits/2026-09/dcp-006-ppm-governance-reconciliation-control-plan.md#portfolio-technical-work-split).
- Future principal change creates a new typed assignment linked to its predecessor. Earlier assignment,
  actor and assessment IDs keep their original type, source identity and effective interval; a new
  Person/Position mapping cannot rewrite who acted historically. Any lawful display/redaction/retention
  policy is separate; no migration/backfill or permanent replication of the identity directory is proposed.

**Existing mechanism / remaining evidence:** authenticated actor, tenant checks and the inspected
lookup-validation path can support a User reference. Its fail-closed 404 collapse still needs
denial-versus-unavailable semantics for the chosen consumer path; named-human/active/tenant eligibility
and permitted reference access need an authoritative contract, not an inference from the adapter's name.
PPM owns the relationship, validity and actor comparison. Business approval is needed for User versus
Person/Position and proposed cardinality/Draft optionality/transfer behavior. The assigning business
persona is APPROVED BUSINESS DECISION; it is no longer an open item.

<a id="portfolio-performance-proposal"></a>

##### 2. Performance Status — PPM business meaning, MOD-0048 controlled publication

**Recommended approach:** PPM owns assessment semantics and the four already approved business labels
(Ahead of Plan / On Track / At Risk / Off Track); MOD-0048 is the target SSOT/publication mechanism for the
controlled values. This follows [domain-config Ownership Boundaries](../domain-config.md#ownership-boundaries).
The former permanent PPM-local lookup option is not recommended or adopted; no local enum/lookup exception
is requested. The four labels are not being re-opened as a business vote.

**Proposed binding, not existing data/fields:** configured set identifier, authoritative scope selector,
published vocabulary version, stable value code and relevant publication provenance. No set code, actual
runtime value code, scope value or published Portfolio dataset has been found/assigned by this proposal.
Scope must be derived from approved tenant/global eligibility; a UI-supplied scope cannot cross tenant
boundaries. Code is a stable machine identity; label is the authorized localized presentation, not the
saved identifier, an ordinal, a score or a default.

- Use the existing GET /api/v1/reference-data/sets/{setCode}/published-values for the approved current
  selection, and evaluate existing /values with version/as_of_date/include_deprecated for historical
  resolution. Existing payload/error evidence is in the reconciliation above. Historical retention,
  version immutability, retired-value and locale coverage for the intended dataset still need provider
  confirmation; endpoint existence alone is not that evidence.
- Recommend that a new assessment use an active value from the current eligible published version.
  Persist the exact version/code used. Revalidate that binding when saving; if publication changed since
  selection, reject the stale selection and require a fresh choice, rather than silently remap or accept
  an obsolete version. Consistent resolution token/as-of semantics and the mutation-time validity window
  need bilateral agreement; no cross-service atomic publication guarantee is asserted.
- Unpublished/missing set, wrong scope, inactive/deprecated value, incompatible version or indeterminate
  provider response prevents a new performance assessment. Existing BRD coded errors remain distinct;
  the consumer does not turn outage into missing data or supply hardcoded labels/defaults.
- Never reuse an old code for a different meaning. Meaning changes require a governed new code/version;
  retirement removes it from new choices but preserves historical meaning. Earlier assessments retain
  code/version/publication provenance plus the bounded historical display evidence described below.
  Do not rewrite earlier assessments to the latest label/version or silently change the current stored
  assessment merely because a value is retired.
- The PPM server validates and projects authoritative eligible options and permitted historical display
  through the normal Gateway path; frontend does not build an English array, contact a service port, or
  infer authorization. Existing BRD Label alone does not prove seven-language coverage. Approved locale
  presentation/provenance is a provider-consumer contract; historical display evidence is read-only,
  never a fallback selection list. Missing historical resolution is shown as unavailable where authorized,
  without inventing a replacement value.
- PPM process owner governs meaning and retirement impact; MOD-0048's authorized stewardship/publication
  process governs publication. Exact steward assignment and scope/version/locale binding remain open.
  A provider change is required only if an identified retention/resolution/locale/validation need cannot
  be met by the existing mechanisms; configuration, dataset publication and PPM consumption are separate
  from a proven API defect. No new MOD-0048 endpoint or implementation task is declared.

This design can be prepared without SOP-0004. Risk still waits for its existing approved-scale row and
SOP-0029 still gates all affected access. The record of those dependencies remains control-plan §10.7;
there is no new source blocker or newly approved dataset here.

<a id="portfolio-assessment-history-proposal"></a>

##### 3. Assessment history — PPM business record plus existing minimal shared evidence

**Recommended approach:** record every accepted manual assessment as a PPM business-history entry and
project each assessment kind's current value from its own latest accepted entry; a risk review cannot
replace the performance projection. History is append-only for business edits:
a correction is another authorized entry with reason and a link to the superseded entry. This is not a
second shared audit service, nor an indefinite-retention or exemption-from-erasure decision.

| Proposed field group — not existing assessment fields | Intended traceability |
|---|---|
| History identity / context | Stable assessment-entry ID, Portfolio ID, server tenant and assessment kind (performance or risk; proposed categories, not published codes). |
| Responsibility / actor | Effective responsibility-assignment reference, typed owner reference at action time, authenticated acting account and server UTC action time. |
| Before / after value | Previous entry reference and previous/new value bindings; a first assessment has explicit no-previous-value. Each nonempty side retains its own set/scope/version/code and publication/scale provenance. |
| Historical presentation evidence | Bounded label/locale/meaning evidence as actually resolved from the approved source at action time, including its source version. This is a per-assessment fact, not a local vocabulary or an editable source directory. |
| Rationale / correction | Required reason text, optional predecessor/correction reference; no blank reason and no silent in-place replacement of an earlier business entry. Length, access and retention follow the pending approved policy/schema. |
| PPM revision / trace | PPM Version before/after, associated local auditIntentId and minimal-mutation classification; no new shared payload field or invented mutation literal. |

The same structure may support manual risk only after SOP-0004 scale/source binding is agreed; it does
not supply that scale. Earlier owner assignment and original actor attribution remain intact after
transfer or principal conversion. Sensitive rationale/history read access is checked separately from
permission to view the Portfolio; generic audit redaction does not provide that SOP-0029 policy.

**Atomic boundary:** after authoritative owner/access/vocabulary validation, compare expected Portfolio
Version and atomically persist the new assessment entry, that kind's current-assessment pointer/projection,
business mutation, incremented Version, required local audit intent and accepted-request receipt. A failed
CAS or required history/intent/receipt write leaves none of those changes committed. If a latest-value projection is stored, it is derived and
committed with its entry, not an independently editable competing truth. A same-value assessment with a
new valid rationale is proposed as a new review entry, not a silent no-op; business approval of that
review/correction behavior remains needed. Retries of the same accepted assessment command use a stable
PPM request identity and recover its receipt; they do not create another entry or version. A materially
different request cannot reuse that identity.

**Shared audit choice:** retain §8 Minimal Mutation Audit v1 exactly:
auditIntentId, actorId, entityType, entityId, mutation, occurredAtUtc.
Use the same local auditIntentId on the history entry/receipt to correlate the PPM business evidence with
the existing minimal Portfolio mutation event; entityId still identifies the Portfolio aggregate.
No rationale, before/after snapshot, assessment-entry ID, vocabulary or owner directory is added to the
shared payload. Authorized reconciliation resolves the intent to the local history; the shared event
alone is not full history or proof of authorization. The existing transport and credential gates remain.

Local transaction failure rolls back. A post-commit delivery failure leaves the committed assessment
intact and follows §8's existing durable retry, stable EventId/bytes, idempotent consumer and authorized
DLQ/replay contract; it never re-applies the assessment. Do not replace the pack transport with the
inspected HTTP append endpoint or expand its BeforeState/AfterState model merely to transport full
history. No sensitive rationale in unlimited Metadata is proposed. Retention/minimization and authorized
historical display await the existing policy/compatibility decisions, not a new audit backlog.

<a id="portfolio-approval-application-proposal"></a>

##### 4. Approval outcome — exact instance read on an explicit apply request

**Recommended approach:** PPM keeps the exact external WorkflowInstanceId and reads the existing
GET /api/v1/workflow/instances/{id:guid} when an authorized actor explicitly requests application of the
approved operation. This on-demand server read avoids assuming a background scheduler/push delivery
contract and rechecks near the actual mutation. It does not promise automatic activation at workflow
completion; that user-visible behavior needs approval. A UI refresh may display permitted status without
applying it. No polling schedule, push subscription, PPM endpoint or new Workflow-prefixed PPM type is
declared to exist. Only an operation whose approved policy requires approval uses this design.

**Proposed PPM approval-request/application record, not existing fields:** local request/attempt identity,
server tenant, Portfolio ID, requested operation, frozen target PPM Version, authoritative preparer
reference/attestation, exact external instance ID when resolved, pinned template-version evidence,
provider outcome identity/revision/finality evidence, and local application receipt/version.
The formal bilateral contract and field/schema names are still OPEN.

1. Before external start, PPM captures the complete tenant–Portfolio–operation–Version request and the
   authorized preparer. Recommend that the authorized human submitting the frozen request explicitly
   attests that they prepared that request's business content; PPM records their authenticated identity
   and attestation. This proposed business definition requires approval. Workflow StartedBy, Portfolio
   CreatedBy, the last editor or a client-provided preparer ID cannot substitute; automation/another actor
   starting the engine does not change the frozen preparer. No unspecified preparer delegation.
2. Reserve one local submission attempt for the same tenant/record/operation/target version before start;
   retain its correlation/retry identity. Store the returned exact instance and bind it to that request
   before using a result. After an ambiguous start response, reconcile that same attempt with provider
   evidence; do not blindly start a second instance or choose the latest by ObjectRef. Provider start
   idempotency/recovery and authoritative binding must be agreed. Technical request/receipt persistence
   alone must not advance the Portfolio business Version and invalidate its own approval; business
   changes still use Version/CAS. This split needs a reviewed persistence contract, not new runtime code.
3. On explicit apply, read only the recorded instance and verify the server tenant context, object,
   operation, frozen PPM version and complete authoritative outcome. Existing DTO supplies Id,
   TemplateId/TemplateVersionId, object coordinates, status and general completion timestamps; the endpoint
   supplies existing permission/tenant/non-leaking lookup behavior. It does not currently supply a complete
   bound decision result with preparer, required approving actors, decision reasons/times and finality.
   Task ActionedBy is an action actor, not universal approval proof. CompletedAt is generic instance
   completion, not an attestation of every approval. Full authoritative outcome, approved/rejected/cancelled/
   pending interpretation, required-step completion and preparer–approver separation remain the narrow
   shared contract need; no internal TaskApprovalService dependency is introduced.
4. Recommend that any intervening Portfolio business Version change invalidates the request for future
   application: mark it superseded in local request state, retain its outcome/history and require a fresh
   prepared request/version. Never rebase an old approval onto a new revision. No automatic external
   cancellation, replacement instance or activation is implied. Owner transfer and assessment mutations
   count as business changes. Compatibility of existing records remains the already open decision.
5. Recheck current permission, SOP-0029 access, effective owner, current Version, Strategic Objective,
   verified funding, Review Frequency and applicable approval/business conditions before activation.
   Provider unavailable/indeterminate means no effect; missing/invisible and definitive rejection retain
   distinct semantics. Agree outcome immutability/revocation, freshness and response-binding guarantees:
   a GET followed by local CAS is not a distributed transaction or proof against an intervening external
   revocation. No stale cached approval is sufficient.
6. Atomically claim the local request/outcome as applied and persist the business effect, resulting Version,
   related history and local audit intent with the consumption receipt. Serialize contenders through the
   same request/target-version identity plus existing CAS; one wins. A duplicate matching the stored
   receipt returns only an authorized replay result and performs no second write, Version increment or
   success history. A changed payload under the same identity is rejected. Check identity/access before
   disclosing even a receipt; no replay bypass of current access. Local failure leaves no application
   receipt/effect; after commit, response loss or external audit failure recovers the receipt/retries
   transport without re-applying the approved operation. PF-AC12 remains the future acceptance gate.

ObjectRef encoding can carry correlation only under a canonical agreed representation; neither it,
latest-by-object, template-version pinning nor Workflow-side idempotency establishes the full binding or
PPM at-most-once business effect. Prefer a narrow outcome/binding addition compatible with the existing
exact-instance read; whether it can reuse an existing public result contract or needs an additive provider
change remains bilateral technical work, not an executive endpoint choice or an assigned Platform task.

**Boundaries carried forward:** Review Frequency remains required at activation, with values/default/
placement undecided. Target create/edit field count and Golden selection remain OPEN: owner assignment,
manual assessments and approval application are proposed separate actions; historical/current assessment
projections are read-only; any eventual create/edit inputs are counted only after their placement is
approved. None of the conceptual fields above is a form-count decision. Remaining SOP, strategy, budget
and compatibility dependencies stay in their existing control-plan rows; budget remains only §10.5.

The single classified work table in control-plan [§10.7](../../../../docs/records/audits/2026-09/dcp-006-ppm-governance-reconciliation-control-plan.md#portfolio-technical-work-split)
separates existing-contract PPM work, proven shared gaps, source/business decisions, conditional development
and deferred work, with separate Draft create/read/edit, assessment and activation impact. Existing identity,
audit or BRD endpoints do not establish a ready subset: confidentiality and owner controls remain mandatory.
No Platform, Organization, ES or Workflow implementation work is dispatched.

**Separate Strategic Objective dependency:** canonical business ownership is **MOD-0352 — Enterprise
Strategy Management**, ESBP goals/objectives/cascade, verified from the
[module registry](../../../registries/module-id-registry.md) MOD-0352 row and
[ESBP domain-config, Ownership Boundaries](../../enterprise-strategy-business-performance/domain-config.md#ownership-boundaries).
This is separate from MOD-0288 Organization identity and from Claude's Organization/FU02 contract.
An approved, tenant-safe, Portfolio-consumable executable Strategic Objective provider/version/access/failure
contract is **not verified / OPEN**; canonical ownership and legacy Enterprise Strategy code do not prove
provider readiness. Seek that contract from the MOD-0352 business/technical owner; no new API, ID, ownership
decision or ES implementation task is created. MOD-0136 finance coordination remains only control-plan §10.5.
Active/Archived compatibility remains the PPM owners' decision, with access/finance owner consultation
only for the respective external constraints. This correction assigns implementation work to no party.


<a id="portfolio-owner-assignment-boundary"></a>

#### Owner assignment — one APPROVED BUSINESS DECISION; implementation boundary DRAFT / NON-EXECUTABLE — 2026-09-10

**APPROVED BUSINESS DECISION — assigning business persona only:** Kullanıcı bu iş kararını onaylama
yetkisini teyit ederek şu öneriyi açıkça onayladı: **“Portfolio sorumlusunu portföy yönetimi/PMO adına
yetkilendirilmiş kişi atar.”** Atayıcı persona artık açık karar değildir. Bu, genel sistem yöneticisine
veya mevcut owner'a otomatik atama/devir yetkisi vermez; mevcut bir PMO rolü, permission veya gerçek
kullanıcı yetkilendirmesi bulunduğunu göstermez. Aynı kişinin devir yapmasının kapsamı aşağıdaki dar
davranış önerisidir; atayıcı kararı bütün devir kurallarını kendiliğinden onaylamaz.

Yalnız bu iş kararı onaylandı. User principal seçimi, cardinality/Draft opsiyonellik/devir sınırları ve
diğer PROPOSED tasarımlar topluca onaylanmadı. Gizlilik/risk/preparer, kaynak ve sağlayıcı kapıları,
önceki scoped onaylar, pack review ve production_authority: none korunur. Bu bölüm kod/test/runtime/DB/
provisioning yetkisi değildir. Aşağıdaki fiziksel kapsam yalnız öneridir. Tarihsel
[detached dilim](#portfolio-isolated-test-slice) **NOT SELECTED — implementation not authorized** kalır.

##### Mevcut zincir ve en küçük tutarlı delta

Mevcut Portfolio.cs owner taşımıyor; PortfolioService ayrı Create/Update/Transition metotlarından
IPortfolioRepository, PpmUnitOfWork ve AuditIntentRepository'ye gidiyor. MongoRepository.ReplaceAsync
tenant/IsDeleted/ExpectedVersion filtresi ve tek Version artışı zorunluluğunu zaten uyguluyor.
Bu temel korunur; yeni servis, PortfolioDraftState, ikinci aggregate veya test persistence modeli yoktur.

Öneri: aynı Portfolio'ya nullable güncel atama bağı ve sahip olunan immutable atama girdileri ekle.
Girdi yeni aggregate/collection değil; atama kimliği, önceki atama bağı, typed User referansı,
server actor/UTC zamanı, gerekçe, önceki/yeni Version ve request/result/auditIntent korelasyonunu tutar.
Devir eski girdiyi silmez veya yeniden yazmaz; önceki aralığın kapanışı successor bağı/zamanından
türetilir, güncel bağ tek yeni girdiye taşınır. Ayrı kişi dizini veya kopya User profili tutulmaz.
Geçmiş silme/retention politikası uydurulmaz; BSON boyut/persistence hatası atomik ret olmalıdır,
sessiz geçmiş kısaltma veya ikinci store'a geçiş değil.

Mevcut PortfolioService içinde tek owner-change metodu, Assign/Transfer ayrımını açıkça doğrular.
Önerilen **yeni, henüz mevcut olmayan** endpoint:
POST /api/v1/ppm/portfolios/{id}/owner-assignments. Girdiler: Operation (Assign veya Transfer),
NewOwnerUserId, ExpectedAssignmentId (Assign için boş, Transfer için mevcut atama),
Reason, ExpectedVersion ve RequestId. Id route'tan; tenant/acting actor, UTC zaman ve permission
server context'ten gelir. Bu girdiler genel create/edit formuna eklenmez.
Yanıt yalnız yetkili çağırana atama/işlem kimliği ve Version verir; genel PortfolioDto/list/detail'e
owner, kimlik profili veya gerekçe/history eklenmez. Yeni history GET ve frontend bu sınırda yoktur.

İşlem sırası: mevcut entitlement/context + ayrı permission; tenant-scoped Portfolio lookup; bağımsız
kayıt/işlem erişimi ve PMO adına yetkilendirme kanıtı; yetkili replay kontrolü; yeni etki için hedef
User'ın aynı tenant/aktif/adlandırılmış insan/atanabilirlik kanıtı; state/expected assignment/Version;
aynı gerçek transaction'da entity + owner history/receipt + mevcut minimal audit intent commit.
RequestId içeriği tenant/Portfolio/actor/operation/hedef/önceki atama/gerekçe/expected Version'a bağlanır.
Aynı kabul edilmiş istek yeniden gelirse güncel erişim kontrolünden sonra eski receipt döner; yeni
Version/history/intent yoktur. İçeriği farklı tekrar reddedilir. Yarış kaybedeni önce conflict alabilir;
yetkili aynı-istek retry'ı commit edilmiş receipt'i okur. Kayıp commit cevabı yeni atama üretmez.

Genel update/lifecycle izni, sistem yöneticisi rol etiketi veya mevcut owner olmak atama izni değildir.
Atama izni değerlendirme izni/owner kanıtı vermez. Mevcut metadata Update yeni owner verisini değiştiremez.
Assessment/activation kurallarını bu owner işi içinde uygulama veya tamamlanmış sayma; mevcut
Transition davranışındaki açık aktivasyon koşulları mevcut planda kalır. Yeni atama yalnız onaylanacak
Draft sınırında çalışır; Active/Archived/soft-deleted kayıtları dönüştürme veya backfill yoktur.

Yeni dış authority portu kimlik, nesne erişimi ve yetkilendirmeyi ayrı kanıt sonuçlarıyla taşır;
PPM owner geçmişini, Version'ı veya persistence'ı uygulamaz. Missing/Denied/Indeterminate/malformed
sonuçların hiçbiri Allowed sayılmaz. Tenant/actor/record/operation/target bağı doğrulanır; görünmez veya
cross-tenant kayıt 404, kesin işlem reddi 403, bozuk istek 400, state/CAS conflict 409, sağlayıcı/kanıt
belirsizliği 503; hedef kimlik ayrıntıları ifşa edilmez. Caller cancellation başarıya çevrilmez.

Mevcut DI PortfolioService'i kaydediyor, handler/validator assembly taraması yapıyor. Yeni command
runtime zincirinin gerçek parçasıdır; detached/test-only servis değildir. Bu öneride dış authority
constructor bağımlılığı optional-null ise yalnız owner aksiyonu 503 döner; olumlu üretim implementasyonu,
fallback veya fake DI kaydı eklenmez. Null dependency genel CRUD'u kırmaz ve genel CRUD'un mevcut
gizlilik açığını kapatmış sayılmaz. Gerçek sağlayıcı/DI bağlama sonraki mutabakata bağlıdır.

<a id="portfolio-owner-assignment-files"></a>

##### Exact gelecek PPM dosya kapsamı — mevcut/yeni, henüz yetki değil

Aşağıdaki 5 mevcut + 8 yeni dosya yalnız yukarıdaki önerilen seçim için aday kapsamdır; farklı iş veya
ortak sözleşme seçimi bunu kendiliğinden genişletmez. İki mevcut governance belgesi ayrıca yalnız
scoped karar/onay ve gerçek kanıt kaydı için güncellenebilir. Başka dosya veya wildcard allowlist yoktur.

| Durum | Exact repository-relative dosya | Gerekçe |
|---|---|---|
| EXISTING — proposed extension | services/Diten.PpmService/src/Diten.PpmService.Domain/Entities/Portfolio.cs | Mevcut aggregate'e nullable güncel atama bağı, sahip olunan immutable atama geçmişi ve Assign/Transfer invariant'ları; metadata Update owner'a dokunmaz. |
| EXISTING — proposed extension | services/Diten.PpmService/src/Diten.PpmService.Application/Features/Portfolios/Services/PortfolioService.cs | Aynı serviste ayrı owner aksiyonu; permission + kayıt erişimi + kimlik kanıtı; mevcut repository/UoW/audit/CAS, replay kontrolü. |
| EXISTING — proposed extension | services/Diten.PpmService/src/Diten.PpmService.Application/Common/PpmPermissions.cs | Yalnız Claude ile mutabık kalınan yeni owner-action permission sabiti; mevcut update/lifecycle anahtarlarının anlamı genişletilmez. |
| EXISTING — proposed extension | services/Diten.PpmService/src/Diten.PpmService.Api/Controllers/PortfoliosController.cs | Mevcut controller'a tek owner-assignment POST; mevcut MediatR zincirine iletim, ayrı controller yok. |
| EXISTING — proposed extension | services/Diten.PpmService/src/Diten.PpmService.Persistence/Mongo/PpmBsonConfiguration.cs | Yalnız Portfolio'nun sahip olduğu yeni geçmiş tipinin/özel koleksiyonunun BSON eşlemesi; mevcut genel serializer/collection/index davranışını değiştirmez. |
| NEW — proposed | services/Diten.PpmService/src/Diten.PpmService.Domain/Entities/PortfolioOwnerAssignment.cs | Yeni destekleyici immutable atama/geçmiş girdisi; EntityBase türevi veya ayrı aggregate/repository değil. Önceki atama bağı, typed User, actor/time/reason/Version ve request receipt bilgisi. |
| NEW — proposed | services/Diten.PpmService/src/Diten.PpmService.Application/Features/Portfolios/PortfolioOwnerModels.cs | Yeni yalnız owner aksiyonu request/result/evidence tipleri; liste/detail DTO'suna owner veya gizli geçmiş eklemez. |
| NEW — proposed | services/Diten.PpmService/src/Diten.PpmService.Application/Features/Portfolios/IPortfolioOwnerActionAuthority.cs | Yeni dış kanıt portu: hedef User uygunluğu ile actor/Portfolio/işlem bazlı erişim ve PMO adına yetkilendirme ayrı sonuçlar; hiçbir PPM state/store davranışı içermez. |
| NEW — proposed | services/Diten.PpmService/src/Diten.PpmService.Application/Features/Portfolios/Commands/ChangePortfolioOwnerCommand.cs | Yeni sealed command; açık Assign/Transfer ayrımı, caller actor/tenant alanı yok. |
| NEW — proposed | services/Diten.PpmService/src/Diten.PpmService.Application/Features/Portfolios/Handlers/CommandHandlers/ChangePortfolioOwnerHandler.cs | Yeni ince handler; mevcut PortfolioService'e delegasyon. |
| NEW — proposed | services/Diten.PpmService/src/Diten.PpmService.Application/Features/Portfolios/Validators/ChangePortfolioOwnerValidator.cs | Yeni request şekli, non-empty kimlikler/gerekçe, ExpectedVersion ve Assign/Transfer girdisi doğrulaması; iş yetkisini validator vermez. |
| NEW — proposed | services/Diten.PpmService/tests/Diten.PpmService.Tests/Portfolios/PortfolioOwnerAssignmentTests.cs | Yeni mevcut Portfolio üzerinde domain, metadata korunması ve BSON round-trip/eski doküman şekli testleri; ikinci davranış modeli yok. |
| NEW — proposed | services/Diten.PpmService/tests/Diten.PpmService.IntegrationTests/Portfolios/PortfolioOwnerAssignmentMongoTests.cs | Yeni mevcut PortfolioService + gerçek PortfolioRepository/PpmUnitOfWork/AuditIntentRepository testleri. Yalnız dış kimlik/erişim/entitlement/permission kanıtları test doubles; test store yok. |

Korunan mevcut temel: IPortfolioRepository.cs, PortfolioRepository.cs, MongoRepository.cs,
PpmUnitOfWork.cs, PpmMongoContext.cs, AuditIntentRepository.cs, EntityBase.cs, PortfolioDto.cs ve
DtoMapping.cs; mevcut create/update/lifecycle command/handler/validator'ları ve DI dosyaları.
Bunların yeni yetkiyle genişletilmesi gerekirse bu listeden örtülü izin çıkarılmaz. Yeni collection,
repository, index, migration, seed, csproj/package/config/secret değişikliği hedeflenmez.
BSON testi eksik owner alanlarını “bilinmiyor/atanmamış” olarak okur; CreatedBy veya default User ile
doldurmaz. Atama yapılmamış eski dokümanın sıradan edit'inde boş owner alanlarının gereksiz yazımı
önlenir. Eski binary'nin yeni BSON alanlarını okuma uyumu deployment gate'idir; bu tur dönüşüm yetkisi yoktur.

##### Yalnız kalan zorunlu kararlar ve Claude temasları

| Açık madde | Somut öneri | Gerçek engel / sınır |
|---|---|---|
| Owner principal ve Draft cardinality | Adlandırılmış insan User; Draft 0–1 etkin atama; CreatedBy'dan türetme yok. | Bu iki seçim yeni alan/invariant tasarımını doğrudan belirler; kod başlangıcından önce açıkça seçilmeli. Atayıcı persona tekrar sorulmaz. |
| Devir ve ilk state kapsamı | Atamaya yetkilendirilmiş PMO adına kişi gerekçeli devir de yapabilsin; ilk owner aksiyonu yalnız Draft'ta, server anında; geleceğe/geçmişe tarih, owner kaldırma, kendiliğinden vekâlet yok. Active/Archived için ret, mevcut veri aynen korunur. | Atama rolü onayı devir semantiğini kapatmaz. Bu dar seçim kod başlangıcını belirler; tüm Active/Archived ürün politikasını şimdi çözmek gerekmez. |
| Kayıt erişimi ve kimlik kanıtı | SOP-0029 seviyeleri icat edilmeden, actor+record+operation için authoritative Allow ve PMO adına yetkilendirme; hedefte authoritative same-tenant/active/named-human/reference eligibility. Belirsizde ret. | Olumlu gerçek kullanım için kaynak/teknik sözleşme zorunlu; port + kapalı davranış kodunu engelleyen genel iş-onayı maddesi değildir. Gizlilik olmadan güvenli Draft/browser kabulü yoktur. |

Atama dışı vocabulary/SOP-0004 risk/preparer/activation/finans/Review Frequency kararları bu dar kod
sınırının yeni önkoşulları değildir; ilgili gerçek Portfolio davranışlarını bloklamaya devam eder.
Saklama/ifşa ve mevcut kayıt uyumu [kontrol planı §10.7](../../../../docs/records/audits/2026-09/dcp-006-ppm-governance-reconciliation-control-plan.md#portfolio-technical-work-split)
üzerinden izlenir; yeni mükerrer iş kaydı açılmaz.

**Claude'a ait dar ortak temaslar — bu PPM allowlist'inin dışında, görev gönderilmiş değildir:**

- Permission anlamı ve catalog/manifest eşleşmesi: önerilen, **mevcut olmayan**
  ppm.portfolios.assign-owner anahtarı (devir seçilirse aynı kontrollü aksiyon); kesin literal/action
  sözlüğü Claude ile mutabık kalmadan kodlanmaz. PMO metni hardcode rol kontrolü değildir.
  services/Diten.Platform/src/Diten.Platform.Application/Features/Ppm/SelfRegistration/PpmManifestProvider.cs
  ve services/Diten.Platform/tests/Diten.Platform.Application.Tests/Ppm/PpmManifestProviderTests.cs
  Platform-owned temaslardır. frontend/Diten.Web/Navigation/PpmModuleManifest.cs discovery eşleşmesi
  Claude koordinasyonundadır; bu işe frontend düğmesi ekleme yetkisi çıkarılmaz.
- Mevcut exact signed permission değerlendirmesi
  services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/SignedJwtPermissionClaimEvaluator.cs
  içinde rol adı bypass'ı yoktur; korunur. Claude catalog/grant bağının yalnız açıkça yetkilendirilmiş
  kişiyi temsil ettiğini, generic admin/owner/update yoluyla otomatik verilmediğini sözleşmeye bağlar.
  Permission tanımlamak gerçek kullanıcıya grant/provisioning yapmak değildir.
- Kimlik yolu: services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/Auth/AuthServiceUserReferenceValidator.cs
  mevcut non-success/parse/kesintiyi 404'e indirger. Auth'taki
  services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/QueryHandlers/ValidateUserReferenceQueryHandler.cs
  tenant/aktiflik kontrol eder; tek başına named-human veya PPM adına atama yetkisi kanıtı değildir.
  Claude, mevcut kimlik/access sözleşmesinin uygunluğunu ve denial/unavailable ayrımını Auth/Platform
  sahipleriyle daraltır; PPM bu protected dosyaları değiştirmez veya yeni hazır provider varsaymaz.
  Kayıt erişimi/SOP-0029 authoritative Allow sözleşmesi olmadan portun olumlu runtime kaydı yapılmaz.

##### Gerçek Portfolio test kapıları — PENDING / NOT RUN

| Test yüzeyi | Olumlu kanıt | Olumsuz/yarış kanıtı |
|---|---|---|
| PortfolioOwnerAssignmentTests | Mevcut Portfolio ilk atama/devir; tek güncel bağ, gerekçe/actor/UTC/önceki atama korunumu; her yeni etki tek Version. | Gerekçesiz, çakışan owner, yanlış expected assignment, uygunsuz state reddi; metadata Update owner/history değiştirmez. |
| Aynı unit dosyasında BSON | Gerçek Portfolio + yeni sahip olunan girdi round-trip; eksik alanlı eski BSON atanmış owner üretmez. | CreatedBy/backfill/default kimlik yok; bilinmeyen/bozuk geçmiş sessizce düzeltilmez; eski doküman şekli/regression kanıtı. |
| PortfolioOwnerAssignmentMongoTests | Mevcut PortfolioService/command/handler zinciri, gerçek repository/UoW/audit ile atama ve devir; yeniden okuyunca aynı history/receipt. | Editor-only, admin etiketi-only, current-owner-only, PMO permission var ama kayıt erişimi yok, hedef kimlik geçersiz/belirsiz, cross-tenant/deleted: hiçbir owner/history/Version/intent değişmez. |
| Aynı gerçek Mongo testi | Kabul edilen request retry'ı/lost-response sonrası okuma aynı receipt; audit korelasyonu mevcut minimal mutation “updated” ile aynı transaction. | Farklı içeriğe aynı RequestId ret; concurrent aynı/farklı istek tek etki; stale CAS no residue; gerçek UoW callback'inde writes sonrası abort entity/history/receipt/intent'i birlikte geri alır. Sonuncusu transaction kanıtıdır, service hata yolunun tamamı geçti diye sunulmaz. |
| Mevcut zincir/regression | Mevcut Portfolio CRUD/tenant/CAS testleri korunur; new handler mevcut DI taramasıyla bulunur, provider yokken owner aksiyonu kapalıdır. | Test assembly dışında concrete fake; ikinci aggregate/store; genel DTO'ya history ifşası; permission yerine rol etiketi; protected-path değişimi kabulü engeller. |

Test doubles yalnız test assembly'sinde dış identity/access/entitlement/permission cevaplarıdır.
Portfolio, repository, audit repository ve UoW yerine in-memory davranış modeli yazılmaz.
Mevcut PpmDisposableMongo/PpmMongoCollection/PpmMongoTestDatabase test altyapısı değişmeden kullanılır;
Mongo testleri gerçek test-owned süreç/DB başlattığı için ayrıca açık test-runtime yetkisi gerektirir.
Bu tur çalıştırılmaz; kod başlangıcı onayı bu test DB yetkisini örtülü vermez. Salt unit test başarısı
persistence/CAS/rollback, gerçek sağlayıcı, gizlilik veya browser kabulü sayılmaz. Test DB yetkisi yoksa
integration kapıları PENDING kalır; test store ile ikame edilmez.

##### Önceki owner-only dar onay metni — tarihsel öneri, verilmiş onay değil

Bu ayrı metin yerine [tek kullanıcı teslimatı toplu onayı](#portfolio-user-delivery-bundle)
değerlendirilir; aşağıdaki kayıt tarihsel kapsamı korur ve bağımsız sonraki adım değildir.

Bu metin atayıcı rolünü yeniden onaylatmaz; kalan iki owner davranış seçimini açıkça ayırır.
Permission literal/consumer kanıt biçimi Claude ile mutabık olduktan sonra kullanılabilecek metin:

> Owner principal için adlandırılmış insan User ve Draft 0–1 seçimini; gerekçeli, anlık, yalnız Draft'ta
> PMO adına yetkilendirilmiş kişinin devir yapabilmesi sınırını ayrıca onaylıyorum. MOD-0117
> “Owner assignment — one APPROVED BUSINESS DECISION” bölümündeki 5 mevcut + 8 yeni dosyada mevcut
> Portfolio entity/service zincirinin owner atama/devir için genişletilmesine ve yalnız ilgili unit
> testlerinin çalıştırılmasına izin veriyorum. İki mevcut belge yalnız scoped onay/kanıt için
> güncellenebilir. Gerçek kimlik/erişim sağlayıcısı yoksa owner aksiyonu kapalı kalacak; test doubles
> yalnız dış bağımlılıklar olacak. Test Mongo/süreç başlatma, gerçek veri, Platform/Auth/shared veya
> frontend/Gateway değişikliği, provisioning, assessment/activation, config/secret, production ve
> commit/push/PR bu onayın dışındadır. NOT SELECTED dilim kapalı; pack review ve production_authority: none.

Bu yalnız taslak onay metnidir; bu tur implementation authority verilmedi. Atayıcı rolü kapandı diye
diğer PROPOSED kararlar approved olmaz. Gerçek persistence test-runtime yetkisi ve provider/manifest
mutabakatı ayrı kanıt kapılarıdır. Aynı anlamlı Portfolio teslimatı içinde kalınır; küçük governance PR'ı yoktur.


<a id="portfolio-user-delivery-bundle"></a>

#### Portfolio first user delivery — single approval bundle — DRAFT / NON-EXECUTABLE — 2026-09-10

> **SCOPED USER IMPLEMENTATION APPROVAL — 2026-09-10:** Kullanıcı dört alanlı Portfolio ekranı ve
> Draft'ta sorumlu atama/devir geliştirmesini açıkça onayladı; mevcut Active/Archived read-only,
> lifecycle ve silme kapalı seçimini kabul etti. Aşağıdaki tarihsel DRAFT/onay-bekliyor ifadeleri
> bu seçilen PPM geliştirme sınırı için bu kayıtla aşılmıştır; toplu taslak metnin tamamı verilmiş
> onay gibi okunmaz. Diğer PROPOSED politikalar, permission katalog yayını, gerçek kullanıcı grant'i,
> Auth/Platform değişikliği, provisioning, ortak/production DB ve canlıya alma kapsam dışıdır.
> Pack review, önceki scoped yetkiler ve production_authority: none korunur.
> Test koşulu: açıkça belirlenmiş, repository kurallarına uygun disposable profil.
> Seçilen Mongo profili mevcut PpmMongoCollection / MOD-0117-disposable-Mongo + PpmDisposableMongo:
> yalnız test-owned loopback süreçleri, dinamik port >=27022, geçici dbpath ve sabit
> diten_ppm_integration_tests DB; test başına tenant izolasyonu. Ortak/production bağlantısı veya
> uygulama config'i tüketilmez. Unit/JS kontrolleri DB'siz disposable çalışma çıktılarıyla sınırlıdır.
> CT external endpoint/schema ve güvenli katalog checkpoint'i olmadan runtime authority adapter/DI
> veya frontend assign-owner manifest yayını yapılmaz; eksik authority işlemleri kapalı tutar.


Bu bölüm liste + create/edit + details + sorumlu atama/devir yüzeylerini **tek kısmi Draft kullanıcı
teslimatı** olarak birleştirir. Önceki analizlerin tekrarı veya yeni module/backlog değildir.
Yalnız atayıcı iş rolü APPROVED BUSINESS DECISION'dır: **portföy yönetimi/PMO adına yetkilendirilmiş kişi**.
Aşağıdaki owner/form/state/permission seçimleri PROPOSED; bu hazırlık kod/test/runtime yetkisi vermez.
Pack review, mevcut implementation-authority alanları, production_authority: none ve önceki scoped
onaylar değişmez. Detached dilim NOT SELECTED kalır.

Bu yeni toplu öneri seçilirse önceki owner-only 5+8 dosya önerisinin **ayrı onay metni yerine** buradaki
tek onay metni kullanılır. Owner-only tarihsel kapsam silinmez; kendiliğinden genişlemiş onay sayılmaz.
İlk kullanıcı teslimatı için form OPEN kaydı aşağıdaki **dört alanlı öneriyle** somutlaştırılmıştır;
Portfolio'nun daha sonraki tam formu, diğer sınıflar ve composite frontmatter alan sayısı bundan türetilmez.

##### Kullanıcı alanları ve işlemleri — hedef net, onay henüz yok

| Yüzey | Kullanıcının girdiği alan / işlem | İlk teslimat önerisi ve açılma şartı |
|---|---|---|
| Create/edit — alan 1 | Code | Required, max 64; mevcut normalizasyon ve tenant içinde uniqueness korunur. |
| Create/edit — alan 2 | Name | Required, max 200. |
| Create/edit — alan 3 | Description | Optional, max 2000. |
| Create/edit — alan 4 | Capacity Allocation açıklaması | Optional serbest metin; teknik limit önerisi max 2000. Rezervasyon, atama, hesap veya zorunluluk değil. |
| Liste | Arama/filtre, izinli kayıt seçimi | Yalnız erişim kararı doğrulanmış kayıtlar; görünmeyen kayıt sayısı/etiketi/export verisi sızmaz. Draft ile mevcut Active/Archived ayrımı açık. |
| Details | Salt okunur hızlı görünüm | Dört alan, lifecycle, izinli güncel owner özeti ve ayrıca history-read kararı varsa gerekçeli atama geçmişi. Liste satırına tam history gömülmez; açılışta güncel detail GET. |
| Atama/devir | Uygun User seçimi + gerekçe | Ayrı offcanvas; server'dan Assign/Transfer bağlamı. Hedef adaylar scoped authoritative arama ile gelir; serbest GUID veya hardcoded kullanıcı listesi yok. |
| Backend/system | Id, tenant, actor, Version, RequestId, ExpectedAssignmentId, UTC zamanlar, lifecycle, izin/action/policy kanıtları | Kullanıcı form alanı değildir. Teknik concurrency/idempotency alanları server/istemci protokolünce taşınır; yetki kaynağı sayılmaz. |
| Kapsam dışı | Performance/risk değerlendirmesi, Review Frequency, Strategic Objective/funding girişi, gizlilik seviyesi seçimi | İlk dört-alan formuna eklenmez; uydurma değer/default veya aktif görünen boş kontrol yok. İlgili bağımlılık nedeniyle kullanılmadığı açıklanır. |

**Create/edit kullanıcı alan sayısı = 4; Golden = Slim (4 ≤ 8).** Index içinde create/edit offcanvas,
details için _DetailsQuickView; ayrı Compact Create.cshtml/Edit.cshtml/Details.cshtml/_Form.cshtml yok.
Atama/devir aksiyonunun iki girdisi create/edit sayımına eklenmez. Referans: AGENTS.md §6,
DEV-0000 pack ve canlı GoldenReferenceSlim Index/_DetailsQuickView/Create command. Tenant layout
_LayoutTenantShell; DataTable v2, yedi dil ve mevcut Premium SweetAlert2 kontratı korunur.
Başka yüzeyleri değiştiren shared refactor veya ikinci frontend ürün akışı hedeflenmez.

| İşlem | Hedef kullanılabilirlik | Şu anda gerçek kullanımın kapalı olmasının nedeni |
|---|---|---|
| Page/list/detail | Read permission + authoritative kayıt erişimi; mevcut Active/Archived yalnız izinli read-only | SOP-0029 uygulanabilir politika/record-access provider doğrulanmadı. Önce sayfa erişimi; yetkisizde iskelet/tablo/aksiyon çizilmez, yalnız açıklama gösterilir. |
| Draft create/edit | Dört metadata alanı; create policy ve mevcut kayıtta edit kararı ayrıca doğrulanır | Gizlilik/erişim eksik. Create, provider'ın seçilen dört alanla resolve ettiği authoritative policy bağını kullanır; creator-only/en kısıtlı default uydurulmaz. |
| Draft atama/devir | Ayrı assign-owner permission + PMO adına yetkilendirme + kayıt erişimi + hedef eligibility; reason/CAS/replay | Owner seçimleri hâlâ PROPOSED; CT auto-grant/catalog ve gerçek identity/access sözleşmeleri açık. |
| Aktivasyon / diğer lifecycle | İlk kabulde kapalı; doğrudan API çağrısı da açamaz | MOD-0136 finans, stratejik hedef, accountable owner, Review Frequency ve ilgili onay/erişim koşulları birlikte kapanmadı. Mevcut kodun Draft→Active izni bu teslimatta server-side kapatılmalı. |
| Archive/delete/bulk mutation | Bu kullanıcı kabulünde kullanılmaz; UI düğmesi yok, Portfolio API bypass'ı da reddedilir | Bu teslimat talebinin dışında; Active/Archived mutation uyumluluğu onaylanmış değil. Entity soft-delete/dependency/fence temeli silinmez. |
| Assessment ve diğer entegrasyonlar | Kapalı, tamamlanmış sayılmaz | Performans yayını/history, SOP-0004 risk, tam approval/preparer ve diğer mevcut §10.7/§10.5 bağımlılıkları sürer. |

Aktivasyonun kapatılması yeni bir otomatik Draft dönüşümü değildir. Eski Active/Archived kayıtlar
yeniden sınıflanmaz, silinmez veya owner/default ile doldurulmaz. Policy kararı olmayan eski kayıt
açıklanmaz. Eski BSON'da owner/capacity alanı yokluğu doğru temsil edilir; toplu migration/backfill yok.
Create için gerçek politika ayrıca kullanıcıdan bir sınıflandırma girdisi gerektirirse bu dört alanlı
öneri onun yerine geçmez: ilgili create yolu kapalı kalır, kapsam sessizce beşinci alanla genişletilmez.
Bu nedenle paket **onaya sunulabilir tasarımdır**, bugün güvenli gerçek-veri teslimatı hazır değildir.

##### Tek karar tablosu — kapanan atayıcı rolü tekrar sorulmaz

| Karar / durum | Tek seferde değerlendirilecek öneri | Sahip / sınır |
|---|---|---|
| Atayıcı persona — APPROVED BUSINESS DECISION | PMO/portföy yönetimi adına yetkilendirilmiş kişi | Kullanıcının önceki açık onayı korunur; admin, current owner veya update otomatik yetki değildir. |
| Owner principal — PROPOSED | Adlandırılmış insan User; shared/system/service hesabı uygun sayılmaz | PPM iş seçimi; authoritative insan/aktiflik/tenant/atanabilirlik kanıtı Auth/CT sözleşmesidir. |
| Draft cardinality — PROPOSED | Draft'ta 0–1 etkin owner; yokluğu açık göster | PPM iş seçimi; owner yokluğu gerçek assessment/activation hakkı vermez. |
| Devir — PROPOSED | Aynı yetkilendirilmiş iş personası gerekçeli, server anında, yalnız Draft'ta atama/devir yapabilsin | Owner kaldırma, backdate/scheduling ve vekâlet kapsam dışında; kişi kendiliğinden devir yetkisi kazanmaz. |
| İlk yüzey/form/state kapsamı — PROPOSED | Dört alan + Slim + ayrı owner aksiyonu; mevcut Active/Archived read-only; assessment/lifecycle/delete bu kabul dışında | PPM kapsam seçimi. Capacity optional iş kararı korunur; 2000 limit teknik öneridir. |
| Permission governance — PROPOSED | ppm.portfolios.assign-owner; Assign/Transfer aynı kontrollü izin, farklı request/state kontrolleri | MOD-0117 Tier-3 anlam kaydı; assessment veya genel edit'e alias değil. CT katalog/grant güvenliği şart. |

##### Platform–Auth teslim sırası ve açık dış kapılar

Bu bölüm assign-owner için tek kanonik governance kaydıdır; [kontrol planı §10.11](../../../../docs/records/audits/2026-09/dcp-006-ppm-governance-reconciliation-control-plan.md#portfolio-owner-assignment-control)
yalnız CT koordinasyonunu bu kayda bağlar. **Auth/Platform kodunun tek yazarı altyapı CT'dir; başka sohbete gönderilmez.**
Önceki read-only bulgular yeniden tamamlanmış iş sayılmaz; altyapı kusurları CT kuyruğunda bekler.

1. **Governance seçimi:** Yukarıdaki tek permission literal/anlamı toplu onay içinde açıkça seçilir.
   Adlandırma PKS Tier-3'e uygundur; henüz runtime/canonical katalog kaydı yoktur.
2. **CT altyapı düzeltmesi önce:** catalog create/reactivate full-catalog auto-grant, SuperAdmin baseline
   ve PPM module-code case/sync yolları yeni kontrollü izni otomatik vermemeli. JWT resolver'ın exact
   authoritative PPM kabulü sessizce genişletilmez. Düzeltme + negatif grant/sync kanıtı gelmeden
   permission yayınlama/etkinleştirme yok. Gerçek role/user grant/provisioning bu paketin yetkisi değildir.
3. **Platform → Auth kayıt eşleşmesi:** CT PpmManifestProvider PORTFOLIOS'a tek ASSIGN_OWNER aksiyonu
   ve testi (24→25 izin; yalnız Portfolio 3→4 aksiyon) için checkpoint hazırlar. Auth PpmPermissionCatalog'a
   aynı **tek anahtar** eklenir; ortak Actions dizisi genişletilmez. Manifest sync'i bu kapalı canonical
   liste ve grant koruması hazır olduktan sonra kontrollü non-production doğrulanır. Tek taraflı katalog
   değişikliği teslimat değildir. PPM sabiti + frontend discovery + exact-set testleri aynı literal ile eşleşir.
4. **Kimlik ve erişim sözleşmesi:** CT/Auth hedefin named-human/same-tenant/active/assignable kanıtını;
   erişim kaynağı sahibi SOP-0029'a uygun page/create/record/read/edit/history/owner kararını sağlar.
   auth.users.lookup-validation endpoint'i yalnız kısmi tenant/aktiflik kanıtıdır. Platform iç adapter'ı
   PPM dış API'si değildir; hazır Portfolio-specific provider iddiası yoktur. Denial/unavailable ayrımı,
   trusted tenant/actor/Portfolio/operation/target/Version/request/policy-version/freshness bağı gerekir.
   Candidate sorgusu da kayıt-scope izinli ve masked/bounded olmalı; tam User dizinine otomatik erişim yok.
5. **PPM tüketimi ve non-production kabul:** Exact CT schema/endpoint/auth/failure sözleşmesi geldikten
   sonra aşağıdaki PPM adapter'ı bağlanır. Null/unavailable port 503, kesin yetki reddi 403, görünmez/
   cross-tenant kayıt 404; istemci 400/409/503 farkını gösterir. Positive provider yokken test double'ı
   runtime'a koyma, fake lookup veya creator-only fallback yasaktır.

| Dış bağımlılık | Sahip | İlk kabulde gerçek engel |
|---|---|---|
| Auto-grant/case kusurları + Platform/Auth katalog eşleşmesi | Altyapı CT — mevcut kuyruk | Assign-owner yayını ve gerçek yetkilendirme güvenliği; checkpoint/test kanıtı henüz yok. |
| SOP-0029 politika/erişim kaynağı; create policy resolve ve history-read | Yetkili politika/belge sahibi + altyapı CT; PPM consumer | Liste/create/edit/details ve history gerçek veri kabulü; uydurularak kapatılamaz. |
| User insan/aktiflik/tenant/atanabilirlik; scoped candidate sözleşmesi | Auth/kimlik sahibi + altyapı CT; PPM consumer | Atama/devir olumlu gerçek kullanım; API/adapter varlığı tam kanıt değildir. |
| MOD-0136 finans, MOD-0352 strateji, Review Frequency, approval/preparer, SOP-0004 ve MOD-0048 | Mevcut §10.7 sahipleri; finans yalnız §10.5 | Kendi kapsamındaki aktivasyon/assessment/entegrasyonlar kapalı. Dört alanlı metadata'yı fake değerle tamamlamak gerekmez. |

##### Dar permission governance ve kayıt erişimi karar tablosu — CT handoff

<a id="portfolio-owner-explicit-grant-decision"></a>

**APPROVED BUSINESS DECISION — 2026-09-11; yalnız otomatik grant dışlama:** Kullanıcı,
`ppm.portfolios.assign-owner` izninin SuperAdmin dahil hiçbir role otomatik verilmemesini,
yalnız yetkili kişinin açık izin atamasıyla verilmesini açık soruya verdiği “evet” yanıtıyla onayladı.
Kapsam; tam katalog grant'i, kiracı Admin modül eşitlemesi ve başlangıç rol şablonları dahil
tüm otomatik grant yollarıdır. Diğer izinlerin davranışı değişmez.
Bu karar mevcut gerçek grant'leri değiştirme/kaldırma, provisioning, Auth/Platform uygulaması,
commit/push veya production yetkisi değildir. Parent pack `review` ve `production_authority: none`
kalır; kimlik ve kayıt erişimi gerçek kullanım kapıları olarak ayrıca açıktır. Parent statüsü için
yeniden CT teyidi istenmez.

| Konu | Mevcut onay / kanıt | Eksik karar | Öneri — runtime politikası değildir | Karar sahibi |
|---|---|---|---|---|
| \`ppm.portfolios.assign-owner\` | Yalnız Portfolio Assign/Transfer içindir; genel edit, değerlendirme veya kayıt okuma yetkisi değildir. PMO adına yetkilendirilmiş kişinin atayıcı iş rolü onaylıdır. Otomatik grant dışlama iş kararı yukarıdaki 2026-09-11 kaydıyla onaylandı. | Katalog yayını ve dışlama uygulaması henüz tamamlanmadı. | Bu satırdaki otomatik grant dışlama artık öneri değildir; onaylı iş kararıdır. Runtime uygulaması ve kanıtı ayrı kapılardır. | İş kararı: Kullanıcı — onaylandı. Katalog/grant uygulaması: altyapı CT. |
| Parent pack / scoped amendment | MOD-0117 \`review\`, \`production_authority: none\` kalır. FU01'in yalnız permission-yayımı kapsamı kullanıcı tarafından onaylandı; bu parent pack'i \`approved\` / \`ready-for-dev\` yapmaz. | Claude'un 2026-09-11 teyidi: parent review kalabilir; own approved/ready FU origin SHA ile okunabilir. | CT'nin kesinleştirilmiş write scope'u, gerekli yetki girdileri ve test kapıları ayrıdır; onay gerçek grant/provisioning, kimlik/kayıt erişimi, browser veya production yetkisi değildir. | FU kapsam onayı: Kullanıcı — onaylandı. CT scope/test: altyapı CT. |
| Owner kimliği | Adlandırılmış aktif insan User yaklaşımı korunur. | Mevcut Auth tenant/aktiflik kanıtı hesabın insan olduğunu kanıtlamaz; atanabilirlik provider sözleşmesi yoktur. | Yeni çalışan/aktif pozisyon şartı veya “her aktif kullanıcı uygundur” kapsamı eklenmez. Target yalnız authoritative named-human/tenant/active/assignable kanıtıyla seçilir; belirsizlikte ret. | Auth/kimlik sahibi + altyapı CT; iş kuralı sahibi |
| Owner’sız Draft oluşturma ve sonradan erişim | Owner'ın Draft'ta yokluğu temsil edilir; owner aksiyonu ayrıdır. | Oluşturanın veya başka actorün sonraki read/edit erişimini belirleyen gizlilik kararı yoktur. | Create ve sonraki record access ayrı authoritative policy kararlarıdır; creator-only veya owner fallback yoktur. Provider yok/belirsizse fail-closed. | SOP-0029 / yetkili politika sahibi; PPM tüketici |
| Liste/detail/arama/export görünürlüğü | Cross-tenant ve hidden kayıtta no-leak hedefi vardır; genel CRUD permission tek başına yeterli değildir. | List/detail/search/export, count ve cache/DOM sızıntısı için gizlilik politikası seçilmemiştir. | Her operation ayrı authoritative record-access kararı ister; list görünürlüğü detail/history/export izni üretmez. Belirsizlikte fail-closed. | SOP-0029 / yetkili politika sahibi; PPM tüketici |
| Metadata düzenleme | Dört alanlı Draft metadata düzenleme kapsamda; Active/Archived salt-okunur, lifecycle/delete kapalıdır. | Hangi actorların hangi Draft kaydını güncelleyebileceği açık değildir. | Record + operation-bound policy kanıtı gerekir; generic update, atayıcı rol veya owner olmak tek başına yetki değildir. | SOP-0029 / yetkili politika sahibi; PPM tüketici |
| Owner atama/devir | Atayıcı iş rolü onaylı; Assign/Transfer genel editten ayrı; eksik/belirsiz authority'de işlem kapalıdır. | Actor'ın kayda bağlı yetkisi, target named-human uygunluğu ve Draft/cardinality/devir ayrıntılarının authoritative sözleşmesi açık değildir. | Actor + tenant + record + operation + target kanıtı save-time yeniden doğrulanır; PMO rol adı hardcode edilmez. | Yetkili iş/politika sahibi; Auth/kimlik sahibi + altyapı CT; PPM tüketici |
| Owner ve gerekçe geçmişi | Geçmiş aggregate içinde append-only tutulur; liste/detail görünürlüğüne otomatik eklenmez. | History'nin kimlere, hangi gerekçe/snapshot alanlarıyla açılacağı belirlenmemiştir. | History-read ayrı record-access operation'dır; current-record read veya owner görünürlüğü history izni değildir. | SOP-0029 / yetkili politika sahibi; PPM tüketici |
| Active/Archived kayıtları okuma | Kayıtlar salt-okunur kalır; Draft'a dönüştürme ve varsayılan owner üretimi yoktur. | Kimlerin list/detail/search/export ile okuyacağı belirlenmemiştir. | Read-only state erişim grant'i değildir; aynı authoritative read policy gerekir ve belirsizlikte fail-closed kalır. | SOP-0029 / yetkili politika sahibi; PPM tüketici |

**CT'ye dar handoff — henüz SHA yok:** “Push edilmiş sabit governance SHA oluştuğunda CT,
\`git show <sha> -- execution/domains/portfolio-delivery/module-packs/MOD-0117-project-portfolio-management.md
docs/records/audits/2026-09/dcp-006-ppm-governance-reconciliation-control-plan.md\` ile bu kaydı
main merge beklemeden okuyabilir. PPM governance kaydı CT'nin ilgili Auth/Platform commitlerinden önce
veya onlarla aynı anda main'e girmelidir. Parent review kalabilir; FU kendi approved/ready statüsü ve
origin SHA ile okunur. Bu teyit FU onayı veya implementation yetkisi değildir. Sonrasında aynı literal
için açık-yetkilendirme, automatic-grant negatifleri ve discovery/catalog teslim sırası beklenir. Commit/push/PR/merge ve
production activation ayrı kapılardır; bu turda hiçbiri yapılmamıştır.”

**FU kapsam ayrımı — 2026-09-11:** [MOD-0117-FU01 — Portfolio Assign-Owner Permission Publication](MOD-0117-FU01-portfolio-assign-owner-permission-publication.md)
bu kanonik kaydın yalnız permission-yayımı follow-up’ıdır. Parent pack `review` kalır; FU `approved` ve
`production_authority: none` durumundadır. Kullanıcı onayı yalnız Auth/Platform’un CT tarafından, CT’nin
kendi kesinleştirilmiş write scope’u ve test kapılarıyla yürütülebilecek publication kapsamıdır; gerçek
grant/provisioning, kimlik/kayıt erişimi, browser ve production kapsam dışıdır. PPM’de mevcut `PpmPermissions` consumer literal’i ve contract
beklentisi yeniden yapılacak iş değildir. Platform manifest/catalog, Auth canonical ayna, BL-359
automatic-grant dışlama ve BL-360 case-consistency yalnız altyapı CT’nin ayrı write scope’udur. FU yeni
endpoint, UI, entity, servis, migration veya genel record-access motoru açmaz.

<a id="portfolio-user-delivery-files"></a>

##### Tek PPM backend/frontend/test exact gelecek allowlist — yalnız PROPOSED

Aşağıdaki **53 dosya** (35 EXISTING, 18 NEW) tek kullanıcı teslimatı için aday kapsamdır.
Bu tur yalnız iki governance belgesi değişebilir. Liste hiçbir mevcut implementation yetkisini
genişletmez; seçilirse önceki owner-only listeye açık ekleme/yerine geçme olarak kullanılır.
NEW satırları bugün yoktur. Runtime provider dosya yeri önerisi, CT dış API sözleşmesinin hazır
olduğu anlamına gelmez; bilinmeyen endpoint/schema/config değeri icat edilmez.

| Katman / durum | Exact repository-relative dosya | Yalnız izin verilecek dar değişiklik |
|---|---|---|
| Backend / EXISTING | services/Diten.PpmService/src/Diten.PpmService.Domain/Entities/Portfolio.cs | CapacityAllocationDescription + aynı aggregate'te owner/history; mevcut metadata/tenant/Version temeli. |
| Backend / EXISTING | services/Diten.PpmService/src/Diten.PpmService.Application/Features/Portfolios/Services/PortfolioService.cs | Liste/detail/create/edit/owner için authoritative erişim; Draft sınırı; aktivasyon ve kapsam dışı mutasyonlar kapalı; aynı repository/UoW/CAS/audit. |
| Backend / EXISTING | services/Diten.PpmService/src/Diten.PpmService.Application/Common/PpmPermissions.cs | Yalnız onaylanacak assign-owner sabiti. |
| Backend / EXISTING | services/Diten.PpmService/src/Diten.PpmService.Api/Controllers/PortfoliosController.cs | Mevcut API'ye page-access, owner-candidates ve owner-assignments aksiyonları; DTO/response uyumu. |
| Backend / EXISTING | services/Diten.PpmService/src/Diten.PpmService.Persistence/Mongo/PpmBsonConfiguration.cs | Yalnız Portfolio capacity/embedded-owner/history BSON eşlemesi ve eski şekil uyumu. |
| Backend / EXISTING | services/Diten.PpmService/src/Diten.PpmService.Application/Features/Portfolios/Commands/CreatePortfolioCommand.cs | Dördüncü optional metadata girdisi; owner/lifecycle/tenant/actor girdisi yok. |
| Backend / EXISTING | services/Diten.PpmService/src/Diten.PpmService.Application/Features/Portfolios/Commands/UpdatePortfolioCommand.cs | Aynı dört metadata girdisi + teknik expected Version. |
| Backend / EXISTING | services/Diten.PpmService/src/Diten.PpmService.Application/Features/Portfolios/Validators/CreatePortfolioValidator.cs | Dört alanın doğrulaması; serbest visibility/default yok. |
| Backend / EXISTING | services/Diten.PpmService/src/Diten.PpmService.Application/Features/Portfolios/Validators/UpdatePortfolioValidator.cs | Aynı doğrulama; domain/service yetki ve state kontrolünün yerine geçmez. |
| Backend / EXISTING | services/Diten.PpmService/src/Diten.PpmService.Application/Common/PortfolioDto.cs | Capacity, yalnız izinli owner özeti/action availability; details'e mahsus izinli history projection, listeye history yok. |
| Backend / EXISTING | services/Diten.PpmService/src/Diten.PpmService.Application/Common/DtoMapping.cs | Yalnız Portfolio mapping; policy değerlendirmesi mapping'de yapılmaz, yetkisiz owner/history serialize edilmez. |
| Backend / EXISTING | services/Diten.PpmService/src/Diten.PpmService.Infrastructure/DependencyInjection.cs | Yalnız Portfolio dış authority consumer bağlama; eksik/bozuk provider config kapalı, sahte provider yok. |
| Backend / NEW — proposed | services/Diten.PpmService/src/Diten.PpmService.Domain/Entities/PortfolioOwnerAssignment.cs | Sahip olunan immutable girdi + predecessor/request receipt/audit korelasyonu; ikinci aggregate/repository değil. |
| Backend / NEW — proposed | services/Diten.PpmService/src/Diten.PpmService.Application/Features/Portfolios/PortfolioOwnerModels.cs | Owner, masked candidate, permission/action ve history/evidence DTO'ları; gerçek kimlik dizini değil. |
| Backend / NEW — proposed | services/Diten.PpmService/src/Diten.PpmService.Application/Features/Portfolios/IPortfolioOwnerActionAuthority.cs | Dış actor/işlem/PMO yetkilendirmesi ve hedef insan User eligibility/candidate kanıtı; ayrı sonuçlar. |
| Backend / NEW — proposed | services/Diten.PpmService/src/Diten.PpmService.Application/Features/Portfolios/IPortfolioRecordAccessAuthority.cs | Create/page/list/detail/edit/history için dış erişim kararları; tenant/record/operation/policy sürüm bağı. |
| Backend / NEW — proposed | services/Diten.PpmService/src/Diten.PpmService.Application/Features/Portfolios/Commands/ChangePortfolioOwnerCommand.cs | Assign/Transfer açık; gerekçe/target/ExpectedAssignmentId/ExpectedVersion/RequestId. |
| Backend / NEW — proposed | services/Diten.PpmService/src/Diten.PpmService.Application/Features/Portfolios/Handlers/CommandHandlers/ChangePortfolioOwnerHandler.cs | Mevcut PortfolioService'e delegasyon. |
| Backend / NEW — proposed | services/Diten.PpmService/src/Diten.PpmService.Application/Features/Portfolios/Validators/ChangePortfolioOwnerValidator.cs | Şekil ve gerekçe/sürüm kontrolleri; rol adı kontrolü yok. |
| Backend / NEW — proposed | services/Diten.PpmService/src/Diten.PpmService.Application/Features/Portfolios/Queries/GetPortfolioPageAccessQuery.cs | Kayıt taşımayan page/read/create availability isteği. |
| Backend / NEW — proposed | services/Diten.PpmService/src/Diten.PpmService.Application/Features/Portfolios/Handlers/QueryHandlers/GetPortfolioPageAccessHandler.cs | Mevcut PortfolioService üzerinden authoritative page/create kararı. |
| Backend / NEW — proposed | services/Diten.PpmService/src/Diten.PpmService.Application/Features/Portfolios/Queries/GetPortfolioOwnerCandidatesQuery.cs | Portfolio-scope, sınırlandırılmış search/paging; tam dizin export'u yok. |
| Backend / NEW — proposed | services/Diten.PpmService/src/Diten.PpmService.Application/Features/Portfolios/Handlers/QueryHandlers/GetPortfolioOwnerCandidatesHandler.cs | Mevcut PortfolioService üzerinden yetkili candidate sorgusu. |
| Backend / NEW — proposed | services/Diten.PpmService/src/Diten.PpmService.Infrastructure/Portfolios/PortfolioAuthorityClient.cs | CT'nin doğrulanmış dış sözleşmesine PPM adapter'ı; endpoint/schema henüz hazır değil, sözleşme kapısı açık. |
| Backend / NEW — proposed | services/Diten.PpmService/src/Diten.PpmService.Infrastructure/Portfolios/PortfolioAuthorityOptions.cs | Typed consumer ayar/bağı doğrulaması; repo config/secret dosyası veya default endpoint ekleme yetkisi değil. |
| Frontend / EXISTING | frontend/Diten.Web/Views/PPM/Portfolios/Index.cshtml | Mevcut Portfolio partial'ını gerçek Slim yüzeyine genişlet; dört alan, izinli details ve ayrı owner aksiyonu; diğer PPM partial'larına değişiklik yok. |
| Frontend / EXISTING | frontend/Diten.Web/Views/PPM/Portfolios/_Filter.cshtml | Mevcut Portfolio partial'ını gerçek Slim yüzeyine genişlet; dört alan, izinli details ve ayrı owner aksiyonu; diğer PPM partial'larına değişiklik yok. |
| Frontend / EXISTING | frontend/Diten.Web/Views/PPM/Portfolios/_DataTable.cshtml | Mevcut Portfolio partial'ını gerçek Slim yüzeyine genişlet; dört alan, izinli details ve ayrı owner aksiyonu; diğer PPM partial'larına değişiklik yok. |
| Frontend / EXISTING | frontend/Diten.Web/Views/PPM/Portfolios/_CreateEditOffcanvas.cshtml | Mevcut Portfolio partial'ını gerçek Slim yüzeyine genişlet; dört alan, izinli details ve ayrı owner aksiyonu; diğer PPM partial'larına değişiklik yok. |
| Frontend / EXISTING | frontend/Diten.Web/Views/PPM/Portfolios/_DetailsQuickView.cshtml | Mevcut Portfolio partial'ını gerçek Slim yüzeyine genişlet; dört alan, izinli details ve ayrı owner aksiyonu; diğer PPM partial'larına değişiklik yok. |
| Frontend / EXISTING | frontend/Diten.Web/Views/PPM/Portfolios/_IndexL10n.cshtml | Mevcut Portfolio partial'ını gerçek Slim yüzeyine genişlet; dört alan, izinli details ve ayrı owner aksiyonu; diğer PPM partial'larına değişiklik yok. |
| Frontend / EXISTING | frontend/Diten.Web/wwwroot/assets/js/PPM/Portfolios/index.js | Portfolio'ya ait field/action/error wiring ve L10n; server authority sonucunu tüketir. |
| Frontend / EXISTING | frontend/Diten.Web/wwwroot/assets/js/PPM/Portfolios/index.l10n.js | Portfolio'ya ait field/action/error wiring ve L10n; server authority sonucunu tüketir. |
| Frontend / EXISTING | frontend/Diten.Web/Resources/Views/PPM/Portfolios/PortfoliosIndex.en.resx | Mevcut en kaynaklarında dört alan, owner/details ve hata/açılmama nedenleri. |
| Frontend / EXISTING | frontend/Diten.Web/Resources/Views/PPM/Portfolios/PortfoliosIndex.tr.resx | Mevcut tr kaynaklarında dört alan, owner/details ve hata/açılmama nedenleri. |
| Frontend / EXISTING | frontend/Diten.Web/Resources/Views/PPM/Portfolios/PortfoliosIndex.fr.resx | Mevcut fr kaynaklarında dört alan, owner/details ve hata/açılmama nedenleri. |
| Frontend / EXISTING | frontend/Diten.Web/Resources/Views/PPM/Portfolios/PortfoliosIndex.es.resx | Mevcut es kaynaklarında dört alan, owner/details ve hata/açılmama nedenleri. |
| Frontend / EXISTING | frontend/Diten.Web/Resources/Views/PPM/Portfolios/PortfoliosIndex.zh.resx | Mevcut zh kaynaklarında dört alan, owner/details ve hata/açılmama nedenleri. |
| Frontend / EXISTING | frontend/Diten.Web/Resources/Views/PPM/Portfolios/PortfoliosIndex.ar.resx | Mevcut ar kaynaklarında dört alan, owner/details ve hata/açılmama nedenleri. |
| Frontend / EXISTING | frontend/Diten.Web/Resources/Views/PPM/Portfolios/PortfoliosIndex.ru.resx | Mevcut ru kaynaklarında dört alan, owner/details ve hata/açılmama nedenleri. |
| Frontend / EXISTING | frontend/Diten.Web/Controllers/PPM/PpmController.cs | Shared PPM controller: yalnız portfolios page-access gate + yeni same-origin proxy yolları; mevcut diğer resource davranışları korunur. |
| Frontend / EXISTING | frontend/Diten.Web/wwwroot/assets/js/PPM/ppm-crud.js | Shared PPM yardımcı: opt-in Portfolio field/action/detail-load/error hooks; varsayılan diğer modül davranışını değiştirmez. |
| Frontend / EXISTING | frontend/Diten.Web/Navigation/PpmModuleManifest.cs | CT'nin aynı literal/katalog checkpoint'iyle yalnız assign-owner discovery kaydı; grant değil. |
| Frontend / NEW — proposed | frontend/Diten.Web/Models/PPM/PortfolioPageModels.cs | Yalnız Portfolio page/form/availability modelleri; shared PpmViewModels ve başka aggregate üretmez. |
| Frontend / NEW — proposed | frontend/Diten.Web/Views/PPM/Portfolios/_OwnerAssignmentOffcanvas.cshtml | Ayrı kullanıcı seçimi + gerekçe; Assign/Transfer bağlamı ve teknik alanlar server'dan. |
| Test / EXISTING | services/Diten.PpmService/tests/Diten.PpmService.Tests/ApplicationContractTests.cs | Exact permission setine tek anahtar ve mevcut Portfolio regression beklentileri; diğer kaynakların kümesi genişletilmez. |
| Test / EXISTING — 2026-09-11 dar onay | services/Diten.PpmService/tests/Diten.PpmService.Tests/PpmEntitlementAuthorizationTests.cs | Yalnız dış record-access authority olumlu test cevabı; create \`201\` ile entitlement → mutation → audit → dispatch correlation zinciri korunur. |
| Test / EXISTING — 2026-09-11 dar onay | services/Diten.PpmService/tests/Diten.PpmService.IntegrationTests/MongoPersistenceIntegrationTests.cs | Yalnız dış authority fixture'ı ve create/CAS beklentileri: normalized duplicate \`409\`, cross-tenant \`404\`, stale version \`409\`, reddedilen işlemde Mongo/audit değişmez. Gerçek Mongo repository/UoW/CAS/audit korunur. |
| Test / EXISTING | frontend/Diten.Web/tests/js/ppm-add-new-delegation.test.mjs | Mevcut Portfolio add/edit delegasyonu yeni dört alanla; diğer yüzeylerin davranışı korunur. |
| Test / EXISTING | frontend/Diten.Web/tests/js/ppm-gate-l-contract.test.mjs | Shared PpmCrud/controller değişiminde mevcut Gate L ve diğer PPM regressions. |
| Test / NEW — proposed | services/Diten.PpmService/tests/Diten.PpmService.Tests/Portfolios/PortfolioOwnerAssignmentTests.cs | Gerçek Portfolio owner/capacity invariant'ları, metadata korunumu, BSON eski/yeni şekil. |
| Test / NEW — proposed | services/Diten.PpmService/tests/Diten.PpmService.IntegrationTests/Portfolios/PortfolioOwnerAssignmentMongoTests.cs | Gerçek PortfolioService/repository/UoW/audit ile CRUD, access, history, owner, CAS/replay/rollback ve activation reddi. |
| Test / NEW — proposed | frontend/Diten.Web/tests/js/ppm-portfolio-delivery.test.mjs | Gerçek Portfolio JS/DOM/proxy contract'ı; alan sayısı, server actions, errors, no-leak. Browser kullanıcı kabulünün yerine geçmez. |

**Shared/protected sınır:** PpmController.cs, ppm-crud.js ve DtoMapping.cs shared temasları yukarıda
bilerek açıkça listelendi; değişim Portfolio opt-in branch/hook ile sınırlandırılır, diğer modüllerin
regressions zorunludur. Portfolio Index mevcut shared _ListShell'i genişletmek yerine mevcut yerel
Portfolio partial'larını gerçek Slim kompozisyonunda kullanabilir; aynı PpmCrud yardımcısı korunur.
Shared _ListShell/_Layout/diğer PPM partial'ları, .antigravity, diğer domain servisleri, csproj/package,
repo config/secret, seed/index/migration dosyaları PPM uygulama allowlist'inde değildir.
Mevcut Gateway /api/v1/ppm/{everything} GET/POST/PUT yollarını taşıyor; yeni route dosyası deltası
gerekli görünmedi. Gateway değişikliği gerekirse integration-agent/protected kapısı ayrı kalır.

**CT-owned temaslar — PPM allowlist'inin dışında, yeni atama değil, mevcut kuyrukla bağlantı:**
services/Diten.Platform/src/Diten.Platform.Application/Features/Ppm/SelfRegistration/PpmManifestProvider.cs;
services/Diten.Platform/tests/Diten.Platform.Application.Tests/Ppm/PpmManifestProviderTests.cs;
services/Diten.AuthService/src/Diten.AuthService.Application/Common/Authorization/PpmPermissionCatalog.cs;
services/Diten.AuthService/src/Diten.AuthService.Application/Common/Authorization/PpmEntitlementPermissionPolicy.cs;
services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/EntitlementPermissionSyncService.cs;
services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/InternalPermissionsController.cs;
services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/FullCatalogPermissionGrantService.cs;
services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/DefaultRolePermissionTemplate.cs.
Bunlar incelenmiş temas noktalarıdır, hepsinin mutlaka değişeceği veya CT allowlist'inin bu belgede
onaylandığı iddia edilmez. CT kendi dar düzeltme/test kapsamını mevcut kuyruğunda tutar.
Kimlik/record-access provider'ın yeni dış endpoint dosyaları henüz sözleşmeyle belirlenmedi; PPM
bu boşluğu protected dosya değişikliği veya hazır provider varsayımıyla doldurmaz.

##### Mevcut zincir, API ve kabul senaryoları — NOT RUN / PENDING

Mevcut Portfolio/PortfolioService/IPortfolioRepository/PpmUnitOfWork/Mongo CAS/minimal audit zinciri
korunur. Capacity ve owner/history aynı aggregate'tedir; yeni store/aggregate/outbox/kimlik dizini yok.
Genel update owner/history'yi değiştiremez. Atama yetkisi assessment yetkisi değildir.
Önceki owner reason/Version/request-replay/transaction tasarımı sadece seçilen sınırda genişletilir.
Mevcut DTO'nun owner/history eklemesi **yalnız izinli projection** içindir: history sadece detail GET'te,
history-read Allow varsa; liste yanıtında/row data-json'da/history yetkisi olmayan detail'de bulunmaz.

Yeni **önerilen, mevcut olmayan** PPM yolları:
GET /api/v1/ppm/portfolios/page-access;
GET /api/v1/ppm/portfolios/{id}/owner-candidates;
POST /api/v1/ppm/portfolios/{id}/owner-assignments.
Mevcut list/detail/create/update yolları genişletilir. Frontend aynı-origin /PPM/Portfolios/api
proxy ailesini, backend'e Gateway üzerinden kullanır. Bunlar PPM consumer yollarıdır; yeni dış
Platform/Auth API'si hazır ilan edilmez. Owner formunda yalnız User seçimi ve reason görünür;
Assign/Transfer/ExpectedAssignmentId/Version/RequestId protokol alanlarıdır.

| Kabul kapısı | Birlikte aranacak olumlu/olumsuz kanıt |
|---|---|
| Dört alan / Slim | Code/Name zorunlu; Description/Capacity optional ve max limitler; tenant duplicate; backend/source HTML/JS alanları aynı. System/owner/visibility/frequency alanı create/edit sayımına veya gizli iş girdisine sızmaz. |
| Liste + detail gizliliği | Read permission yetmez: kayıt erişimi, history-read ve create ayrı. Cross-tenant/hidden 404; unavailable 503; sayfa yetkisizken iskelet yok. Search/count/export/client cache/DOM sadece izinli veri taşır. Detail açılırken güncel GET; görünür listeden geçmişe otomatik izin yok. |
| Create/edit + eski kayıt | Gerçek policy resolve olmadan create yok; Draft edit reasonless metadata yalnız kendi alanlarını değiştirir. Owner/history/Version ve mevcut bağımlılıklar korunur. Eski Active/Archived read-only; eski eksik alanlardan owner/default üretme veya migration yok. |
| Owner atama/devir | Authoritative scoped candidate + save-time yeniden doğrulama; generic admin/current-owner/update tek başına ret. Gerekçesiz/yanlış target veya scope/stale Version ret; tek etkin owner, append-only predecessor, replay tek etki. |
| Gerçek persistence | Mevcut PortfolioService + gerçek repository/UoW/audit ile commit/yeniden okuma; concurrent CAS; replay/lost-response; gerçek transaction abort'ta entity/history/receipt/intent birlikte geri alınır. Sahte store ile kabul yok. |
| Aktivasyon / kapsam dışı bypass | UI'da kapalı işlemin doğrudan API çağrısı da reddedilir; finans tek başına yeterli olmaz. Lifecycle/delete/assessment kapalı; mevcut Active kayıt Draft'a dönüştürülmez. |
| Permission zinciri | CT katalog 25 exact set, yalnız Portfolio eklemesi; auto-grant/seed/sync/case negatifleri; JWT role∩entitlement∩canonical; PMO rol adı hardcode değil; kayıt erişimi bağımsız. CT checkpoint'i gelmeden bu kapı kapanmaz. |
| Hata/UX/shared regression | 400 alan hatası; 401 oturum; 403 yetki; 404 görünmez/kayıp; 409 refresh/retry, sessiz overwrite yok; 503 bağımlılık, başarı toast'ı yok. Çift gönderim/retry ikinci etki üretmez; seven-locale, Slim/DataTable v2, shared diğer PPM davranışı korunur. |
| Kullanıcı eski/yeni ekran kontrolü | Aynı seçilmiş non-production test kapsamı/izinler ile eski ve yeni ekran yan yana: alanlar, details, atama/devir, kapalı aksiyon ve hata örnekleri. Eski Archive/legacy dosyaları yalnız referans; kopyalama/değişiklik yok. |

**Mongo test profili kapısı:** Mongo entegrasyon testleri yalnız repository kurallarına uygun,
açıkça belirlenmiş disposable test profili üzerinde çalışabilir. Ortak veya production veritabanı
kullanılamaz. Uygun profil belirlenmemişse Mongo testi başlatılmaz; mevcut test fixture'ının bulunması
profil seçimi veya test yürütme onayı sayılmaz. Bu dar koşul implementation/test yetkisi vermez.

Teknik testler mevcut projelerdeki unit, gerçek test-owned Mongo entegrasyon, ilgili frontend JS ve
DataTable/Slim/lokalizasyon kontrollerini içerir; build/test bu tur çalıştırılmaz. Test doubles sadece
dış bağımlılık cevaplarıdır ve test assembly'sinde kalır. Pozitif browser kabulü fake authority ile
yapılamaz; mevcut yetkili non-production hesap/veri ortamı ve gerçek CT provider gerektirir. Test
ortamı yoksa kabul kapısı bekler; bu onay user/role provisioning veya secret işlemini örtülü vermez.
Provider options/transport bağlama, doğrulanmış CT kontratına göre ve test/non-production sınırında;
bilinmeyen config dosyası veya üretim endpoint'i listeden türetilmez.

##### Tek teslim düzeni ve toplu onay metni

Teknik test → kullanıcının eski/yeni ekran kontrolü → Claude uygunluk incelemesi → aynı kapsamda
düzeltmeler ve etkilenen kontroller → **tek anlamlı Portfolio teslimatı/PR**.
Gerekli CT checkpoint'leri ve güvenlik/kimlik kapıları kullanıcı kabulünden önce kapanmalıdır.
Sonraki sayfa bu kabul ve teslimat kapanmadan başlamaz. Sırf doküman/owner/backend/frontend bitti diye
küçük PR üretilmez. Aynı feature branch ve mevcut dirty işler korunur; ileride güncel main mutabakatı
ayrıca yapılır. Canlıya alma ayrıca açık yetki, veri/rollback uyumluluğu ve güvenlik kontrolü ister.

**Kullanıcının tek seferde değerlendireceği önerilen onay — şu anda verilmiş değildir:**

> MOD-0117 “Portfolio first user delivery — single approval bundle” kapsamındaki dört alanlı Golden
> Slim liste/create-edit/details ve ayrı owner atama/devir teslimatını seçiyorum. Adlandırılmış insan
> User, Draft'ta 0–1 owner, PMO adına yetkilendirilmiş kişinin gerekçeli ve anlık yalnız Draft atama/devir
> yapması; owner kaldırma/backdate/scheduling olmaması; mevcut Active/Archived kayıtların izinli
> read-only kalması ve ilk kabulde aktivasyon/diğer lifecycle/delete/assessment'ın kapalı tutulması
> seçimlerini açıkça onaylıyorum. ppm.portfolios.assign-owner anlamını Assign/Transfer için aynı
> kontrollü izin olarak governance düzeyinde onaylıyorum; otomatik grant veya gerçek kullanıcıya
> yetki vermiyorum. Belirtilen 51 PPM/backend/frontend/test dosyasında mevcut uygulama zincirinin
> geliştirilmesine, ilgili teknik testlerin yalnız test-owned non-production ortamda yapılmasına
> ve güvenlik/kimlik/CT kapıları kapandıktan sonra tek kullanıcı ekran kontrolüne izin veriyorum.
> Mongo entegrasyon testleri yalnız repository kurallarına uygun, açıkça belirlenmiş disposable test
> profilinde çalışabilir; ortak veya production veritabanı kullanılamaz. Uygun profil belirlenmemişse
> Mongo testi başlatılmayacak.
> İki mevcut belge yalnız scoped onay ve kanıt için güncellenebilir. Auth/Platform işleri mevcut
> altyapı CT kuyruğunda kalır; bu onay başka sohbete devir veya protected Auth/Platform uygulama yetkisi
> değildir. Eksik SOP-0029/provider/grant koruması gerçek veri kabulünü açmaz; diğer PROPOSED tasarımlar,
> risk/preparer/finans politikası onaylanmış sayılmaz. Provisioning, secret/config/migration işlemi,
> canlıya alma ve merge bu onayın dışındadır. Tek anlamlı PR aşamasına yalnız teknik test, kullanıcı
> kontrolü, Claude incelemesi ve düzeltmeler tamamlandıktan, mevcut Git güvenlik koşulları sağlandıktan
> sonra geçilsin; küçük ara PR oluşturulmasın. Pack review ve production_authority: none korunur;
> NOT SELECTED detached dilim açılmaz.

Bu metnin hazırlanması onay yerine geçmez; statü veya implementation/production authority yükseltilmedi.
Kaynak/CT sözleşmeleri onay metninin yerine geçmediği gibi onay metni de eksik kaynakları üretmez.
Kısmi Draft kullanıcı kabulü, Portfolio tamamlandı/finans entegre/production hazır anlamına gelmez.

<a id="portfolio-isolated-test-slice"></a>

#### Isolated Domain/Application test slice — 2026-09-10 — NOT SELECTED — implementation not authorized

**CONTROL TOWER disposition:** this detached 14-file proposal was not selected for implementation.
PortfolioDraftState, a separate service and a test store create a second behavior model independent of
the existing Portfolio implementation; passing its tests would not count as progress on the real
Portfolio delivery. The proposal, file list, test matrix and approval wording below are retained only
as historical material, not an executable next step or an invitation to request approval.
This disposition supersedes the historical conditional execution/approval language in this section.
No code or test execution is authorized.

Any subsequent real development must extend the existing Portfolio entity/service chain. Test doubles
may represent external dependencies only; they must not create a second Portfolio aggregate or an
alternative persistence reality. Earlier Portfolio designs, dependency decisions, scoped approvals,
pack review and production_authority: none remain unchanged.

**Authority:** the user accepted preparation of this bounded non-production test slice, not its coding.
This is a scoped proposal inside MOD-0117, not a new module/FU or an approved runtime allowlist.
Pack status remains review; earlier scoped approvals remain unchanged; production_authority: none.
The four business designs remain PROPOSED. The test scenario neither approves company role/access policy
nor grants any real user assignment/approval rights, establishes a corporate preparer definition, closes
SOP-0029/SOP-0004, or authorizes code, service, database or production operations. An informal "ok" is
not approval of all those matters. The former code-start approval text below is historical and inactive.

**Historical proposed boundary — not selected:** add a detached, immutable Draft evaluation model and a non-registered plain
Application service. Only the selected unit tests construct and invoke them. The live Portfolio entity,
PortfolioService, CRUD/lifecycle endpoints, persistence, permissions and runtime composition stay untouched.
This section supplies the exact *future* file proposal; it is non-executable until separately authorized.
The broader source/policy gates continue to block real implementation/use. As a specific bounded exception,
this isolated test implementation may proceed after its own explicit scoped approval without treating
those corporate/source decisions as closed; they are not prerequisites for synthetic test preparation.

##### Included and excluded behavior

Included only as proposed business-design tests:

- Draft state can explicitly have no owner. A successful test assignment has one effective named User
  reference, and a transfer closes the old entry, records a reason and establishes one replacement.
- Owner-only assessment requires a valid effective assignment, matching actor and affirmative identity/
  operation evidence. General edit evidence grants neither assignment nor assessment rights.
- Performance and risk have separate append-only histories and separate current-entry references.
  A new reasoned assessment/correction appends an entry; it cannot overwrite previous attribution.
- Expected-Version and a request-bound receipt gate local effects. Replayed identical requests cannot
  increment Version or append history/intent twice; changed content under one request ID is rejected.
- Missing, denied, malformed, wrong-scope or indeterminate identity/authorization/vocabulary evidence
  rejects the affected operation with no accepted-state change. Denial and dependency uncertainty remain
  distinguishable application results; this slice defines no new public HTTP mapping.
- A negative-only activation check proves that owner absence/invalidity cannot permit activation.
  Even with a valid owner it returns OutsideSlice, never Allowed, never Active and never a success receipt.

Excluded: live Draft CRUD changes; actual activation or full eligibility calculation; Strategic Objective/
funding/Review Frequency implementation; complete approval/preparer semantics; Workflow start/read/result
consumption; user provisioning; real provider or external-reference integration; confidentiality levels;
SOP risk values; MOD-0048 set/code/publication; runtime permission/manifest entries; browser/API behavior;
Mongo/persistence/index/migration/seed; external audit dispatch; service/config/secret/network/real data.
No automatic demo data, production fallback or generalized allow-all policy is introduced.

##### Existing architecture and exact isolation decision

Evidence at e72701fa565942187e7dd85c1e92fdcc25080969:

- Application/DependencyInjection.cs AddApplication scans its assembly for MediatR handlers and
  FluentValidation validators (lines 20–21), then registers the existing PortfolioService (line 22).
  Therefore merely omitting a new explicit AddScoped entry would be insufficient for new discoverable
  handlers/validators. **For this slice only**, use plain request/result DTOs, explicit service methods
  and constructor-supplied ports: no IRequest/IRequestHandler/INotificationHandler, AbstractValidator/
  IValidator, pipeline behavior, registration attribute, initializer or hosted worker. Existing runtime
  CQRS stays unchanged. This bounded non-runtime experiment is not an exemption for later runtime code.
- The existing Domain project has no project dependencies; Application references Domain. Existing
  tests/Diten.PpmService.Tests/Diten.PpmService.Tests.csproj already references Application and also
  Infrastructure/Api/Contracts. Reuse that project unchanged and select only the new test namespace.
  Transitive compilation is not claimed to be a Domain-only build. Tests must not construct an API host,
  call Program, register Infrastructure/Persistence, create a service provider or connect to a real source.
  No csproj/solution/package/config change is allowed. If this project cannot run the isolated selection
  without forbidden initialization, stop and report the exact dependency; do not broaden the scope.
- Portfolio.cs, EntityBase.cs and PortfolioService.cs stay read-only. Their tenant/Version/soft-delete/
  lifecycle behavior, normalization and InvestmentCase fences are preserved. The proposed detached state
  uses explicit tenant/actor/time/request inputs from the test driver, not an HTTP DTO or trusted real-user
  context. It does not subclass, mutate, save, automatically map into or replace the live Portfolio.
- Existing PpmUnitOfWork/MongoRepository transaction and CAS are later persistence references only.
  Reuse the existing Domain.Repositories.AuditIntent shape as an in-memory intent descriptor, with the
  Portfolio aggregate identity and existing updated mutation literal; do not dispatch it or change §8.
  No actual Portfolio insert/update or audit event is produced.

**Application commit model:** read an immutable snapshot, collect request-bound evidence and stage a
candidate state/history/receipt/intent bundle without mutating the published snapshot. The abstract store
performs one compare-and-commit for expected Version and request identity. Its only implementation in this
slice is an internal test-assembly store with a lock or equivalent deterministic synchronization.
Failure before publication leaves the entire old state unchanged. An injected lost response after a
successful in-memory commit is recovered from the matching receipt, not by repeating the mutation.
This demonstrates the Application contract only: no Mongo rollback, durable outbox, multi-process
linearizability, crash durability or distributed transaction is proven.

Receipt binding includes tenant, Portfolio test identity, actor, operation, expected Version and canonical
semantic request content; timestamps generated by the service are not regenerated into a different retry
fingerprint. Evidence must match the addressed tenant/principal/operation/reference; an affirmative result
for another request is not accepted. Read/replay still checks current identity and operation authorization
before exposing a receipt. Matching replay returns the original accepted result; changed-content reuse and
a different stale request fail. The store race test synchronizes contenders explicitly, without sleeps.
No existing HTTP request, real JWT, role grant or corporate authorization service feeds this model.

<a id="portfolio-isolated-test-files"></a>

##### Exact future file allowlist — closed set, not current write authority

All 14 source/test paths below are **NEW / proposed / currently absent**, verified during this preparation.
No other source file, test helper or project file is implicitly included. Paths are repository-relative
to /Users/alitufanoglu/ERP-vNext-codex-current; the list is exact, not a directory wildcard.

| State | Exact future path | Sole purpose |
|---|---|---|
| NEW / proposed | services/Diten.PpmService/src/Diten.PpmService.Domain/PortfolioDraftEvaluation/PortfolioDraftState.cs | Detached immutable Draft state, identity/scope, Version and invariants; no inheritance from or writes to the live Portfolio. |
| NEW / proposed | services/Diten.PpmService/src/Diten.PpmService.Domain/PortfolioDraftEvaluation/PortfolioDraftOwnerAssignment.cs | Single effective assignment and immutable transfer entries for the proposed test scenario. |
| NEW / proposed | services/Diten.PpmService/src/Diten.PpmService.Domain/PortfolioDraftEvaluation/PortfolioDraftAssessmentEntry.cs | Separate performance/risk entry identity, opaque source-reference evidence, rationale and attribution. |
| NEW / proposed | services/Diten.PpmService/src/Diten.PpmService.Domain/PortfolioDraftEvaluation/PortfolioDraftMutationReceipt.cs | Request identity/content binding, accepted result and local Version provenance. |
| NEW / proposed | services/Diten.PpmService/src/Diten.PpmService.Application/Features/PortfolioDraftEvaluation/PortfolioDraftEvaluationModels.cs | Plain request/result and evidence DTOs; explicit denial/indeterminate/outside-slice results; no IRequest or public endpoint. |
| NEW / proposed | services/Diten.PpmService/src/Diten.PpmService.Application/Features/PortfolioDraftEvaluation/IPortfolioDraftIdentityAuthority.cs | Abstract identity-evidence port; no concrete provider/default implementation. |
| NEW / proposed | services/Diten.PpmService/src/Diten.PpmService.Application/Features/PortfolioDraftEvaluation/IPortfolioDraftAuthorizationAuthority.cs | Abstract operation-evidence port; no company roles/permission keys/grants. |
| NEW / proposed | services/Diten.PpmService/src/Diten.PpmService.Application/Features/PortfolioDraftEvaluation/IPortfolioDraftVocabularyAuthority.cs | Abstract opaque reference-validation port; no lookup list, set code or real risk scale. |
| NEW / proposed | services/Diten.PpmService/src/Diten.PpmService.Application/Features/PortfolioDraftEvaluation/IPortfolioDraftStateStore.cs | Abstract snapshot/read-receipt/compare-and-commit contract; candidate state, history, receipt and local intent submitted together; no persistence implementation. |
| NEW / proposed | services/Diten.PpmService/src/Diten.PpmService.Application/Features/PortfolioDraftEvaluation/Services/PortfolioDraftEvaluationService.cs | Constructor-only composition and explicit method invocation; stages immutable mutations, gates evidence, Version and replay; no DI registration/discoverable handlers. |
| NEW / proposed | services/Diten.PpmService/tests/Diten.PpmService.Tests/PortfolioDraftEvaluation/PortfolioDraftEvaluationTestDoubles.cs | Internal deterministic identity/auth/vocabulary doubles and atomic in-memory store with controlled failures/barriers; clock/IDs are explicit test inputs. |
| NEW / proposed | services/Diten.PpmService/tests/Diten.PpmService.Tests/PortfolioDraftEvaluation/PortfolioDraftEvaluationDomainTests.cs | Detached-state/owner/history and immutability assertions. |
| NEW / proposed | services/Diten.PpmService/tests/Diten.PpmService.Tests/PortfolioDraftEvaluation/PortfolioDraftEvaluationApplicationTests.cs | Evidence denial, staging/commit, replay, stale-version and controlled concurrency scenarios. |
| NEW / proposed | services/Diten.PpmService/tests/Diten.PpmService.Tests/PortfolioDraftEvaluation/PortfolioDraftEvaluationIsolationTests.cs | Assembly/source/dependency/registration boundary checks without building a host or resolving runtime providers. |

**Existing files allowed later only for scoped approval/evidence bookkeeping:**
this MOD-0117 pack and
[the existing control plan](../../../../docs/records/audits/2026-09/dcp-006-ppm-governance-reconciliation-control-plan.md#portfolio-isolated-test-control).
No existing code file is in the write allowlist. The two documents may record only this slice's explicit
authorization and actual test results; pack-wide review/production_authority and other scoped approvals
must not be promoted or overwritten. In this preparation turn, only those two existing documents are writable.

Read-only/protected boundary includes every DependencyInjection.cs; Api/Controllers and Program.cs;
Portfolio.cs/PortfolioService.cs and existing CRUD commands/validators; all Persistence/Infrastructure/
Contracts code; PpmPermissions.cs; frontend PpmModuleManifest.cs and Platform PpmManifestProvider.cs;
all project/solution files; frontend/Gateway/Platform/Auth/shared rules/config/secrets. No runtime DI,
controller, manifest, permission registry or provider implementation may acquire a reference to the new
slice. The new sources will compile into Domain/Application assemblies, but must remain unreachable from
runtime composition; absence of registration is not claimed to mean absence from the compiled artifact.

##### Deterministic test doubles — synthetic authority only, no invented business vocabulary

Only PortfolioDraftEvaluationTestDoubles.cs inside Diten.PpmService.Tests may implement the four ports.
All doubles are internal, explicitly named TestOnly, and assert their containing assembly is the test
assembly. The state-store double is a test repository simulation, not a future persistence fallback.
All fixtures use synthetic identities and explicit fixed UTC inputs; no real account, tenant or directory
is read. Authorization evidence expresses test operation outcomes, not a role named by the company.
Identity eligibility is distinct from the PPM responsibility assignment.

Vocabulary fixtures use opaque synthetic reference IDs and a conspicuous test-only provenance marker.
They contain **no risk rating/score/level, SOP scale value, confidentiality level, real set code or purported
published version**. Distinct opaque test reference revisions may exercise provenance mismatch; they are
not declared to be MOD-0048 publication versions. A positive "risk history" test proves only that the risk
channel stores a reasoned opaque reference separately; it does not validate or demonstrate a usable risk
assessment scale. Four approved performance labels remain business input in the earlier amendment; this
slice does not mint their runtime codes or a production selection list. Missing-source scenarios reject;
test evidence is never exported to application DI, runtime data, config, manifest, seed or real providers.

<a id="portfolio-isolated-test-matrix"></a>

##### Future test matrix — all PENDING / NOT RUN

Local row numbers below are checklist labels only, not new module/backlog identities. Every rejected or
indeterminate mutation must assert unchanged state, Version, history, receipts and local intent count.

| # / behavior | Positive/control scenario | Negative/race scenario and required result | Test file |
|---|---|---|---|
| 1 — ownerless Draft | Detached Draft explicitly has no assignment; no CreatedBy-derived owner | Assessment rejects MissingOwner; negative-only activation rejects; no effects | DomainTests + ApplicationTests |
| 2 — first assignment | Affirmative same-scope test identity and assignment authority establish exactly one effective User reference and one versioned history entry | General edit alone, denied/unknown assignment authority, wrong tenant, ineligible principal or a second overlapping owner reject | DomainTests + ApplicationTests |
| 3 — reasoned transfer | Replacement validated; old interval/history retained, new effective assignment starts at explicit test time, one Version increment | Blank reason, stale Version, invalid replacement or pre-publication store failure leaves the old owner/history intact; former owner cannot assess afterward | DomainTests + ApplicationTests |
| 4 — owner-only assessment | Effective owner + separate assessment authorization + matching identity/reference evidence append a result | Owner with no operation permission; editor/assigner who is not owner; inactive/expired/indeterminate owner; unspecified delegation all reject | ApplicationTests |
| 5 — separate histories | Performance and risk-channel opaque entries each update only their own current-entry pointer | Interleaved changes cannot overwrite the other channel or reinterpret a prior source revision; no actual risk value is fabricated | DomainTests + ApplicationTests |
| 6 — rationale/correction | Reasoned correction appends and links the old entry; same selected reference with a new explicit review request remains a separate proposed review | Empty/whitespace reason rejects; historical actor/time/reference/Version cannot be edited in place; exact retry is not a new review | DomainTests + ApplicationTests |
| 7 — external evidence | Matching test-only identity/auth/reference results allow only the addressed detached operation | Missing/Denied/Indeterminate, mismatched tenant/actor/operation/reference, malformed evidence or simulated provider exception yields a bounded rejection/uncertainty result; no fail-open | ApplicationTests |
| 8 — CAS and commit boundary | Candidate state/history/intent/receipt publish together once | Two different requests from the same Version: one wins, the other conflicts; injected pre-publication failure publishes nothing; original immutable snapshot is unchanged | ApplicationTests |
| 9 — request replay | Identical accepted request recovers original result without Version/history/intent increment | Changed content/actor/operation/tenant under reused identity rejects; concurrent identical requests cause one effect; simulated post-commit response loss recovers receipt; unauthorized receipt read rejects | ApplicationTests |
| 10 — activation exclusion | Valid owner control case still returns OutsideSlice, never an activation success | Missing/ineligible owner rejects specifically; no test can produce Active, an approval instance, an activation receipt or a real transition call | ApplicationTests + IsolationTests |
| 11 — no implicit runtime discovery | Reflection/source checks find new types only in expected Domain/Application/test locations; no handler/validator/worker registration interfaces | A runtime reference/DI descriptor to the slice, linked test source or concrete source-side port implementation fails isolation acceptance | IsolationTests |
| 12 — test-only provenance and scope | Port implementations/fixtures reside only in the test assembly; source project references do not point to tests; forbidden-path diff is empty | Real provider/host/HttpClient/Mongo/credential/config access, production fallback, added permission/manifest, or scope drift blocks completion instead of triggering a workaround | IsolationTests + delivery diff review |

Test-file short labels refer only to the exact four test paths in the allowlist. No acceptance row is
passed by writing this matrix. Test isolation checks inspect types/source/project edges without starting
a host; they must not register Infrastructure/Persistence or resolve live services merely to test absence.

##### Test gates and explicit future approval

After explicit authorization only, the selected namespace must be
Diten.PpmService.Tests.PortfolioDraftEvaluation. The future command from this worktree is:

    dotnet test services/Diten.PpmService/tests/Diten.PpmService.Tests/Diten.PpmService.Tests.csproj --filter "FullyQualifiedName~Diten.PpmService.Tests.PortfolioDraftEvaluation"

This is a proposed future selected unit-test run, **not executed now**, not permission to run the full
unit/integration/architecture suites, and not permission to launch services. Existing pinned dependencies
and project references are reused; no dependency/config workaround is authorized. Restore/build failure
requiring out-of-scope changes or external setup must be reported without changing the scope.

Completion gates: all 12 rows have actual non-skipped evidence in the selected tests; test-only
implementation/provenance checks pass; no new runtime reference or automatic registration exists; only
the exact new files plus bounded documentation evidence differ from the code-start baseline; original
dirty documentation is preserved; protected DI/controller/manifest/config paths have an empty diff;
full changed-file review and git diff --check pass. Source scanning plus reflection cannot prove browser,
network or durable persistence behavior, and those claims are explicitly excluded.

**Historical proposed code-start approval text — NOT SELECTED; not an executable next step:**

> MOD-0117 içindeki "Isolated Domain/Application test slice — 2026-09-10" bölümünü yalnız belirtilen
> 14 yeni Domain/Application/test dosyası için uygulama ve seçili izole unit testlerini çalıştırma
> kapsamında onaylıyorum. Mevcut iki belge yalnız bu scoped onay ve gerçek test kanıtları için
> güncellenebilir. Runtime DI/controller/manifest, mevcut Portfolio runtime kodu, Persistence/Mongo,
> frontend/Gateway, Platform/Auth, config/secret ve gerçek veri kapsam dışıdır. Bu onay şirket
> rol/erişim veya preparer politikasını, SOP kaynaklarını, gerçek aktivasyonu ya da production
> kullanımını onaylamaz. Pack review ve production_authority: none korunur; commit/push/PR yoktur.

The quoted wording is retained as proposal history only. It is no longer the next approval or execution
step; no approval is requested. This detached slice is NOT SELECTED — implementation not authorized.
Pack-wide review and the broader Portfolio amendment remain unchanged.

##### Later slices and completion language

Follow-on persistence/provider/frontend preparation remains in the existing control-plan
[§10.7 work split](../../../../docs/records/audits/2026-09/dcp-006-ppm-governance-reconciliation-control-plan.md#portfolio-technical-work-split):
PPM mutation/history/CAS and real atomic persistence; owner identity; MOD-0048 publication; SOP-0029
policy; SOP-0004 scale; full outcome binding and replay; Strategic Objective; Review Frequency;
Active/Archived compatibility. Finance remains only
[§10.5](../../../../docs/records/audits/2026-09/dcp-006-ppm-governance-reconciliation-control-plan.md#portfolio-budget-integration).
PF-AC01–PF-AC12 remain broader future acceptance, not passed by the in-memory slice; particularly PF-AC12
Workflow outcome consumption and real activation are excluded here.

Allowed completion wording is only "the isolated Domain/Application proposed-behavior tests passed"
with actual evidence. Never "Portfolio completed", "secure browser acceptance passed", "integration works",
"real risk scale/visibility policy validated" or "durable persistence proven".
This backend-only test slice creates no form fields or UI; the target Portfolio form count/Golden
remain OPEN and existing other-surface decisions remain intact.
No separate PR is targeted. This unselected detached proposal and any hypothetical passing tests do
not count as progress on the real Portfolio delivery on codex/ppm-portfolio-first-delivery.
Updated main reconciliation belongs to the later combined delivery;
no fetch/merge or synchronization claim is made here.

#### Future acceptance criteria — all pending, not executed or passed

| ID (local checklist only) | Future scenario and required observable result |
|---|---|
| PF-AC01 | Entitled same-tenant actor with general update permission but without verified Portfolio ownership attempts performance/risk assessment: reject server-side; assessment, rationale history and Version remain unchanged. Missing or indeterminate owner proof never allows the mutation. |
| PF-AC02 | Otherwise authorized actor addresses another tenant's Portfolio through read/update/assessment/lifecycle or a typed link: 404 without foreign data disclosure or mutation; tenant/actor payload spoofing cannot override authenticated context. |
| PF-AC03 | Required performance/risk vocabulary or its approved version is absent/unavailable/unsupported: affected action cannot save; no hardcoded labels-as-codes, fabricated rating or default option; manual valid vocabulary path is tested separately. |
| PF-AC04 | Verified owner submits empty or whitespace-only rationale for either assessment: reject with no state/history change. Valid manual assessment records before/after, rationale, authenticated actor, UTC time and relevant version provenance; UI does not claim automatic calculation. |
| PF-AC05 | Two writers use the same expected Version: only one valid mutation commits; stale request receives 409 and cannot overwrite assessment/lifecycle or append a successful mutation audit. |
| PF-AC06 | Required local assessment history/audit-intent append fails: entire mutation transaction rolls back, including lifecycle/assessment and Version. Post-commit downstream audit transport failure follows durable retry/idempotency; it is not falsely described as local rollback. |
| PF-AC07 | Draft activation lacks a verified typed MOD-0136 budget/funding commitment, or reference is deleted/invalid/wrong tenant/stale or provider unavailable: no Active state. Even with valid funding, missing Strategic Objective, accountable owner or Review Frequency independently prevents activation; recheck all four and applicable approval/access gates at mutation time. This is a future delta, not a claim about existing code or Draft-save requiredness. |
| PF-AC08 | Confidentiality policy, level or role/visibility mapping is indeterminate: no disclosure or successful affected operation, including Draft list/detail/create/update; no invented most-restrictive code, creator-only fallback or UI-only protection. A see grant alone never grants change/approve. |
| PF-AC09 | Capacity explanation omitted: no mandatory-field rejection solely for that omission; supplied text creates no resource reservation/person assignment/calculation. Final string limits and form placement must be contracted before implementation. |
| PF-AC10 | Approved compatibility cases for existing Active/Archived and soft-deleted records preserve history, tenant/CAS, dependency checks and terminal-state constraints; no automatic migration/demotion/backfill or reactivation is performed. |
| PF-AC11 | Future authorized UI demonstrates the final counted form, separate assessments/lifecycle, truthful read-only data, explicit tenant layout, DataTable v2 and seven-language parity; unauthorized access discloses no record/page content. |
| PF-AC12 | The same authoritative Workflow outcome is delivered repeatedly or concurrently: PPM applies the business effect at most once, with mutation, consumption/idempotency state, relevant local history/audit intent and Version in the same atomic boundary. No second effect, Version increment or successful-mutation history is created by replay. Before a new effect PPM rechecks permission, record version and all business conditions; an outcome with mismatched instance/tenant/record/operation/version cannot apply. Test local rollback and post-commit external audit transport failure separately; Workflow idempotency or delivery count is not proof of this behavior. |

Final endpoint/DTO/permission and HTTP mapping must distinguish invalid request, definitive denial,
missing/invisible resource, stale state and unavailable authoritative dependency per control-plan §10.2.
Undefined outcomes are not all collapsed into 400/409. These are future test expectations, not newly run
tests or an implementation allowlist. No build, runtime, DB, migration/index/seed or browser tests were run
for this governance-only amendment. Authoring validation: canonical MOD-0117 preflight passed against
Master 8.1/registry; full two-document diff review and `git diff --check` are required before handoff.

Implementation remains blocked for this amendment until the missing source/owner contracts, compatibility
and exact scope are reconciled and separately approved with explicit repository implementation authority.
Deferred performance automation, risk aggregation and resource integration remain OPEN under control-plan
§10.7; Portfolio–Budget remains only [§10.5](../../../../docs/records/audits/2026-09/dcp-006-ppm-governance-reconciliation-control-plan.md#portfolio-budget-integration).

### Change log

| Date | Change | Authority |
|---|---|---|
| 2026-09-03 | Added the exact legacy delegated-click test file solely to replace superseded Initiative assertions with authoritative lifecycle/classification behavioral proof while preserving Portfolio/shared CRUD coverage. Recorded generic select-all/bulk endpoint/bulk-delete/clear-selection verifier expectations as inapplicable to the explicitly no-bulk Initiative surface. Preserved `review`, `production_authority: none`, and prohibited production/shared/verifier changes. | User / Portfolio Governance Process Owner |
| 2026-09-03 | Added only `IInitiativeLifecycleContractAuthority.cs` to the Initiative Lifecycle Contracts v2 exact backend allowlist as a PPM-owned canonical-production and fail-closed malformed-contract test seam. Explicitly prohibited external providers, alternate ownership, DI/cache, Persistence/Infrastructure, frontend/Gateway/configuration and production activation. No runtime or test code changed in this governance checkpoint. | User / Portfolio Governance Process Owner |
| 2026-09-03 | Added exact Initiative Lifecycle Contracts v2 backend prerequisite authority: `GET /api/v1/ppm/initiatives/lifecycle-contracts/v2`, PPM-owned lifecycle matrix/vocabularies independent of MOD-0048, server-calculated record action availability, strict `401/403/503`, exact backend/test allowlist and exhaustive gates. Preserved `review`, `production_authority: none`, default-off/non-activating posture and separate explicit approval; frontend stays blocked until prerequisite merge and verification. No backend/frontend/runtime/test code changed in this checkpoint. | User / Portfolio Governance Process Owner |
| 2026-09-03 | Corrected the Initiative frontend handoff after executable feasibility review: the current DTO/result exposes no authoritative allowed-transition/action projection, and combined `contracts/v2` couples PPM-owned lifecycle vocabularies to MOD-0048 availability. Required an independent additive PPM-owned lifecycle contract (preferred `GET /api/v1/ppm/initiatives/lifecycle-contracts/v2`) plus record-specific server-calculated actions where needed before frontend lifecycle work can start. This correction grants no backend authority; an exact backend allowlist amendment and separate explicit user approval are required. | User / Portfolio Governance Process Owner |
| 2026-09-03 | Added bounded Initiative Core v2 frontend implementation authority: exact eight-field Golden Slim tenant UI, same-origin Initiative proxy, authoritative `contracts/v2` vocabulary with fail-closed create/edit, action-only lifecycle/closure/supersession, typed-link-only details, HTTP-specific UX and exact frontend/test allowlist. Preserved `review`, set `production_authority: none`, retained default-off/non-activating posture and required separate explicit user approval before frontend implementation. No runtime/frontend/Gateway/backend/deployment code was changed by this governance checkpoint. | User / Portfolio Governance Process Owner |
| 2026-09-02 | Added and corrected the governance-only Initiative Core v2 baseline: exact eight-field Golden Slim create/edit contract; nullable-in-Proposed and required-before-Active MOD-0048-owned type/priority classifications and planning dates; exact approved values for five PPM-owned lifecycle/closure vocabularies with no `other`; action-based lifecycle and Workflow/WorkCenter boundaries; verified-recipient-only OnHold notification plus non-blocking durable `recipient-unresolved` disposition/UI warning; exact InitiativeClosure requiredness/cardinalities; terminal supersession; authoritative-owner typed links; repository-accurate future allowlist/protected paths; HTTP matrix, acceptance/test gates and explicit owner blockers. No runtime/frontend/service/Gateway/migration/seed/deployment/activation authority was created. | User / Portfolio Governance Process Owner |
| 2026-09-01 | Reconciled the Initiative legacy wizard against Blueprint ownership and current PPM code. Retained the six-field Golden Slim form; registered future strategy, organization, planning, metric, investment, funding, decision, evidence and dependency detail concepts as governance-only/default-unavailable. No PPM field, runtime card, producer call, mock fallback or activation authority was created. | User / Portfolio Governance Process Owner |
| 2026-08-30 | Recorded the DCP-004 Gate-2 current-state disposition as governance-only, default-off and non-production: exact eligible type count `0`, empty action map, six owned-type dispositions, all 31 projection fields and eleven future decision gates. No provider/endpoint/configuration/runtime/activation authority or WorkCenter completion claim was created. | User / Enterprise Strategy Control Tower |
| 2026-08-29 | Reconciled the canonical local PPM port to `5062`, superseding the earlier `5061` allocation while preserving CRM on `5061`. The integration-agent-owned Gateway authority remains exactly `/api/v1/ppm` plus `/api/v1/ppm/{everything}`; no production activation, deployment or broader route authority was granted. | User / Enterprise Strategy Control Tower |
| 2026-08-29 | Reconciled the current-main semantic checkpoint chain: PPM base/contracts `a22a872f`, parent MOD-0018 governance `457edbdd`, neutral S2S request binding `92eb29ea`, and PPM-owned default-off Gate I relationship/outbox composition `8c659594`. Recorded build `0/0`, unit `286/286`, dynamic-Mongo integration `82/82` with zero skips, architecture `11/11`, mutation `6/6`, and restore SHA-256 `61e79023258a6086db98f52378a7c86bf611f309d71a83979f7368b056d68170`. This closes only backend/default-off evidence; `review`, MOD-0023 `ExcludedV1`, and full 1.3/browser/live-provider/bilateral/WorkCenter/production gates remain unchanged and open. | Enterprise Strategy Control Tower — current-main evidence reconciliation |
| 2026-08-26 | Authorized the bounded, internal-only, default-off Gate I runtime-composition implementation handoff after exact I-A/I-B/I-C contract checkpoints were composed. Recorded dependency checkpoint order, exact source/config/test allowlist and provider ownership. MOD-0023 is `ExcludedV1`: no DTO/provider/positive fixture, and every ApprovalOutcome-required path remains zero-residue `503`. This grants no activation, secret/key, deployment, broker/live, frontend, Gateway or WorkCenter authority. | User / Enterprise Strategy Control Tower |
| 2026-08-26 | Authorized exact pure Domain/Application and test roots for three parallel NON-RUNTIME consumer contract-test lanes: I-A MOD-0007 Decision Trace with MOD-0023 explicitly blocked/test-plan-only, atomic I-B MOD-0136+MOD-0138 Funding & Scenario, and I-C MOD-0072 Benefit Realization. Bound exact producer checkpoints, wrappers, operations, permissions, service identities, mode/error/no-copy/security/idempotency fixtures and later composition gates. No Persistence/Infrastructure/Api/runtime/frontend/Gateway/WorkCenter authority, status promotion or production activation was granted. | User / Enterprise Strategy Control Tower |
| 2026-08-26 | Reconciled factual Gate L (`536aa685`) and default-disabled ExternalContextReference provider (`eddabab0` + `682b0afb`) implementation evidence, replaced stale missing-producer-pack wording with immutable ready-for-dev/core checkpoint provenance, and recorded three parallel NON-EXECUTABLE I-A/I-B/I-C planning lanes. MOD-0117 remains `review`; Gate I adapters, composition, activation, parity acceptance and production authority remain open. | Enterprise Strategy Control Tower governance reconciliation |
| 2026-08-02 | Closed only the PPM-owned Gate I consumer profile ledger: exact `1.0` wrapper names/field allowlists, shared typed InvestmentCase context, four executable-slice boundaries and Historical/New/Current validation plus 400/401/403/404/409/503 mapping. Producer contracts/statuses, MOD-0023, runtime authority, MOD-0117 `review` status and OD-04 remain unchanged/open as applicable. | User / Portfolio Governance Process Owner |
| 2026-08-02 | Staged the OPEN Gate I authority into I-A Decision Trace, I-B Funding & Scenario and I-C Benefit Realization. Recorded exact cardinalities, institutional owner roles, common fail-closed/no-copy baseline, MOD-0023 hazards and conjunctive full-1.3 wording. This amendment is non-executable: no producer contract name/version, physical placement, adapter/runtime authority, status promotion or completion claim was created. | User / Enterprise Strategy Control Tower |
| 2026-08-01 | Reconciled Phase 2B into Gate L `InvestmentCase` + `BenefitCommitment` local ownership and Gate I external integration. Locked exact fields, immutable-parent cardinalities, neutral lifecycles, eight lowercase permissions, Golden Slim surfaces and scoped completion language. MOD-0117 remains `review`; no runtime authority or external contract was created. | User / Enterprise Strategy Control Tower |
| 2026-08-01 | Bound the internal ExternalContextReference provider lookup to configurable `100..5000 ms`, default `2000 ms`; internal timeout is generic `503`, while caller cancellation propagates unchanged. The limit is lookup-only and does not alter global Mongo/CRUD behavior or authorize retry/cache/fallback. | User / Enterprise Strategy Control Tower |
| 2026-08-01 | Authorized the default-disabled internal `ppm.external-context-reference` `1.0` provider slice. Control Tower corrected the canonical permissions to exact `.read` keys (not `.view`), approved endpoint-specific strict tenant/sub `401` semantics and the v1 null-only `VisibilityPolicyKey` boundary. No new permission was created; MOD-0018 object-level visibility and MOD-0354 consumer integration remain future/blocked. | User / Enterprise Strategy Control Tower |
| 2026-07-29 | Formally dispositioned the four repeated DataTable verifier findings per Phase 2A surface as approved policy/profile mismatches: bulk delete is prohibited, and authentication uses a same-origin MVC proxy plus HttpOnly cookie rather than browser-side `getAuthHeaders()`. No bulk-delete surface, browser JWT exposure or verifier change is authorized. | User / Enterprise Strategy Control Tower |
| 2026-07-30 | Locked independent PPM tenant-entitlement and user-permission gates, dormant grants after entitlement removal, and the `PpmAuditIntentSubmittedV1` / `ppm.audit-intent.submitted.v1` producer identity. Minimal Mutation Audit v1 is final; publisher credential, runtime evidence and PSS production authorization remain open. | User / Enterprise Strategy Control Tower |
| 2026-07-30 | Code-reality correction fixed `ModuleCode = PPM` and blocked the existing generic Admin/Viewer auto-grant plus destructive revoke/reconcile flow from unchanged PPM use. PPM-specific versus generic bridge revision and dormant-grant reactivation remain PSS/security human-review decisions. | User / Enterprise Strategy Control Tower |
| 2026-07-30 | Recorded PSS-B1 physical provider contract `platform.ppm-entitlement-decision.v1`, dedicated PPM caller credential, exact minimal response and fail-closed `503` boundary. PSS owns the provider and every-instance invalidation; the PPM-service consumer remains unimplemented and this pack stays `review`. | User / Enterprise Strategy Control Tower |
| 2026-07-29 | Promoted the authorized Phase 2A slice to `review` after shared JWT 21/21, PPM 17/17, targeted Gateway 1/1, isolated Mongo replica-set 9/9, Web 22/22, delegated jsdom PASS and real browser CRUD evidence. The smoke used an ephemeral signed JWT bootstrap because the real tenant-admin token has zero PPM permissions; AuthService grant provisioning and MOD-0021 delivery remain open. DataTable reports 60 PASS plus four policy/profile findings per surface. | Orchestrator — Phase 2A verification evidence |
| 2026-07-29 | Promoted pack to `approved` and recorded explicit user authorization for the `Diten.PpmService` scaffold plus Phase 2A backend/frontend. Fixed lifecycle, cardinality, referenceability, Golden Slim surfaces, port `5061`, Gateway `5000` boundary and transactional local audit-intent foundation. Provider transport, DWS runtime integration, Phase 2B and WorkCenter hazards remain blocked. | User / Enterprise Strategy Control Tower |
| 2026-07-29 | Reconciled stale pack branch metadata to the active Enterprise Strategy worktree `feature/es/enterprise-strategy`; scope and authorization are unchanged. | Orchestrator — active Enterprise Strategy worktree reconciliation |
| 2026-07-29 | Phase 1.5 plan explicitly approved by the user. Reconciled exact Phase 2A field limits, `ParentType + ParentId`, and Golden Slim form counts `5/6/6/7` before production coding. | User / Orchestrator |
| 2026-07-29 | Authorized the narrow Phase 2A PPM object API Gateway mapping; implementation is restricted to `integration-agent`. | Enterprise Strategy Control Tower |

## 20. Follow-up Items

- Product Owner/PMO classification of every
  [deferred Project Workspace integration](../../../release/release-backlog/R1-ppm-mvp-backlog.md) after
  its named dependency gates close; backlog presence alone is not implementation or R1 authority.
- Portfolio Governance Process Owner review of all open business decisions.
- AuthService/PSS provisioning of real PPM grants; MOD-0021 runtime integration remains open.
- Contract-owner review for MOD-0136, MOD-0138 and MOD-0072 typed references.
- Reconcile the existing immutable MOD-0007, MOD-0136, MOD-0138 and MOD-0072 parent-pack/core checkpoints
  into each later bilateral handoff; create no new MOD/FU identity and do not treat isolated checkpoints as
  PPM runtime authority.
- Prepare a separate PSS-owned MOD-0023 amendment for the PPM-facing ApprovalOutcome contract and remediation;
  do not promote its status or begin runtime through this pack.
- After each producer contract is approved, prepare the corresponding non-executable MOD-0117 consumer
  amendment, obtain approval and seek explicit runtime authority separately.
- Exact ExternalContextReference provider/consumer runtime design and compatibility/security evidence.
- Keep the formal DataTable verifier disposition above visible until the protected verifier supports the
  no-bulk-delete and same-origin HttpOnly-cookie profile; no product remediation is required.
- MOD-0354 promotion only after its MOD-0117 provider blocker and other OD-04 subsets close.
- Any legacy mock/prototype containment or migration through a separate approved pack.
- Any future WorkCenter-related behavior through DCP-004 and the applicable Gate 2 process.
- For every Initiative detail concept in §4.3.1, obtain an owner-approved executable contract and a separate
  MOD-0117 consumer amendment before adding UI, entity fields, DTOs, API routes or data relationships.

##### Scoped implementation checkpoint — 2026-09-11 — USER ACCEPTANCE OPEN

Dört alanlı Slim liste/create-edit/details ve ayrı Draft owner aksiyonu mevcut Portfolio
entity/service/controller zincirinde uygulandı. Owner/history/receipt aynı aggregate içinde;
gerekçeli devir append-only, aynı istek tekrarında ikinci etki yok; mevcut UoW/CAS/minimal audit
korundu. DTO owner/history projection'ı ayrı erişim kararlarına bağlıdır. Genel edit atama yetkisi
değildir. Active/Archived edit ve Portfolio API lifecycle/delete kapalıdır; eski veri dönüştürülmedi.

Exact aday listenin içinde **44 kod/test dosyası + mevcut iki belge** değişti (30 mevcut, 16 yeni).
CT dış sözleşmesi olmadan PortfolioAuthorityClient/Options oluşturulmadı; Infrastructure DI ve
frontend PpmModuleManifest değiştirilmedi. ppm.portfolios.assign-owner yalnız PPM tüketici sabitidir;
katalog yayını, gerçek grant, provider veya canlı entegrasyon değildir. Diğer PROPOSED politikalar,
SOP-0029 ve gerçek kimlik/erişim bağımlılıkları kapanmadı. Runtime'ta optional authority kaydı yok;
gerekli doğrulanmış olumlu kanıt olmadan ilgili Portfolio işlemi kapalıdır.

| Doğrulama | Gerçek sonuç / sınır |
|---|---|
| PPM backend derleme + Portfolio/Application seçili unit | Derleme başarılı; seçili unit 29/29. BSON zaman hassasiyeti düzeltildi. |
| Yeni gerçek Portfolio Mongo senaryoları | Seçili MOD-0117-disposable-Mongo profilinde 12/12; gerçek service, repository, UoW, audit, concurrent aynı/farklı istek, replay, stale CAS ve writes-sonrası rollback. Cleanup fixture tarafından tamamlandı. |
| Frontend derleme | Başarılı, 0 hata; değiştirilmeyen CRM/WorkCenter/ESBP dosyalarında 15 uyarı. |
| Portfolio + mevcut shared JS regresyonları | node --test ile 12/12; gerçek Razor alan sayımı, server aksiyonları, metin güvenliği, owner retry ve yedi dil. Browser kabulü değildir. |
| Golden Slim statik kontrol | --api-profile proxy ile 64/64; mevcut shared kompozisyon işaretleri kullanılır, page-local DataTable kopyası eklenmedi. Statik işaret kontrolü runtime kanıtı değildir. |
| Tüm PPM unit paketi | Repository kökünü görebilen DB'siz disposable artifact profilinde 386/389. Kalan 3 test aşağıdaki eski correlation beklentileridir; atlama/bypass uygulanmadı. |
| Mevcut MongoPersistenceIntegrationTests | Aynı disposable Mongo profilinde 16/19. Kalan 3 eski test, authority'siz Portfolio create beklediği için başarısız. |
| Repository mimari muhafızları | Repository kökü görünürken 16/17. Tek kırmızı DB-010 kontrolü aşağıdaki değiştirilmeyen Platform dosyalarıdır; istisna listesi genişletilmedi. |

**Dar açık regresyon kapsamı:** Hazırlanan exact liste iki gerekli mevcut PPM test dosyasını
atlamıştır; bu tur sessizce genişletilmedi:
- services/Diten.PpmService/tests/Diten.PpmService.Tests/PpmEntitlementAuthorizationTests.cs:
  One_scoped_correlation_flows_through_entitlement_mutation_and_dispatch üç varyantta provider'sız
  create için 201 bekliyor; yeni doğru cevap 503. Olumlu correlation zinciri testi yalnız dış
  record-authority test double'ıyla uyarlanmalı; runtime bypass veya ikinci aggregate eklenmemeli.
- services/Diten.PpmService/tests/Diten.PpmService.IntegrationTests/MongoPersistenceIntegrationTests.cs:
  Duplicate_normalized_code_returns_409, Stale_version_is_rejected_with_concurrency_contract ve
  Cross_tenant_entity_is_hidden_with_404 aynı eski create fixture'ına bağlı. Gerçek repository/UoW
  korunarak yalnız dış authority cevabı ve yeni 409 response beklentisi uyarlanmalı.
Bu iki dosya değişmedi; kapsam eklemesi yapılmış/onaylanmış sayılmaz. Aynı uniqueness, cross-tenant,
stale CAS ve correlation/receipt davranışları yeni gerçek Portfolio Mongo testinde de doğrulandı.

##### Dar test allowlist ek onayı — 2026-09-11

Kullanıcı yalnız aşağıdaki iki mevcut test dosyasını exact Portfolio teslimat kapsamına ekledi:

- \`services/Diten.PpmService/tests/Diten.PpmService.Tests/PpmEntitlementAuthorizationTests.cs\`
- \`services/Diten.PpmService/tests/Diten.PpmService.IntegrationTests/MongoPersistenceIntegrationTests.cs\`

Bu ekleme ürün kodu, Auth/Platform, DI, configuration veya disposable Mongo test profilini değiştirme
yetkisi vermez. İlk dosyada yalnız dış record-access authority test cevabı eklendi; olumlu
correlation senaryolarında create \`201\` ve entitlement → mutation → audit → dispatch zinciri aynen
korundu. İkinci dosyada gerçek Mongo repository/UoW/CAS/audit korunarak yalnız dış authority fixture'ı
ve create/CAS beklentileri uyarlandı: normalized duplicate \`409\`, cross-tenant \`404\`, stale version
\`409\`; duplicate, stale ve authority-yok retlerinde ilgili Mongo/audit etkisinin oluşmadığı ayrıca
doğrulandı. Authority yokluğu create'i \`503\` ile kapatır.

\`Portfolio_delete_and_investment_create_never_commit_an_orphan\` bu ek onayın parçası olarak
değiştirilmedi. Portfolio delete artık koşulsuz \`409\` döndüğü için testin \`204 && 201\` imkânsızlık
assertion'ı gerçek delete–create yarışını kanıtlamaz; mevcut hali sahte güvence üretebilir. Bu ayrı
test semantiği bulgusudur; dar izin dışındaki uyarlama ve lifecycle kararı olmadan değişiklik yapılmaz.

**Mevcut CT kuyruğunda kalan DB-010 bulgusu:** services/Diten.Platform/tests/Diten.Platform.Application.Tests/Audit/PpmAuditRetentionPolicySeedMongoTests.cs
ve services/Diten.Platform/tests/Diten.Platform.Application.Tests/Persistence/DisposableStandaloneMongo.cs.
Mimari tarama bu iki mevcut dosyadaki koşu başına GUID'li DB adını işaretledi; bu testler
çalıştırılmadı ve Platform dosyaları değiştirilmedi. Seçili Portfolio profilinin sabit DB adı +
test-owned süreç + TenantId izolasyonu bu ihlalden ayrıdır.

Test çıktı profilleri: /private/tmp/portfolio-owner-delivery-artifacts ve DB'siz kök-tarama
kontrolleri için services/Diten.PpmService/tests/Diten.PpmService.Tests/bin/portfolio-owner-disposable-artifacts
(gitignore kapsamı, bu tur oluşturulan geçici çıktılar). İlk repository-dışı çalıştırmalarda kökü
bulamayan testler bu yerleşimle ayrıştırıldı; bunlar ürün regresyonu diye raporlanmadı.
Ortak/production DB, provisioning, uygulama servislerini başlatma veya canlıya alma yapılmadı.

Tek teslimat hâlâ teknik regresyon kapanışı → kullanıcının eski/yeni ekran kontrolü →
Claude uygunluk incelemesi → correction → tek anlamlı PR sırasındadır. Kullanıcı ekran kabulü,
Claude incelemesi, live provider ve production henüz yoktur; sonraki sayfa başlatılmadı.
Pack review / production_authority: none ve NOT SELECTED detached dilim korunur.
Commit, push veya PR yapılmadı.
