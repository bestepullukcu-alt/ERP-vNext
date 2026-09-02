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
cross-tenant or unavailable recipient authority produces no notification; the implementation must not route
to a fabricated user, creator, free-text address or generic administrator. Whether missing recipient blocks
the lifecycle mutation or records a no-notification disposition remains an open Portfolio Governance Process
Owner + MOD-0288 contract decision; no implementation may choose implicitly.

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
  branches; MOD-0288 verified/missing/ambiguous/unavailable recipient branches; zero fake recipient and zero
  WorkCenter item/provider projection on direct transitions.

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
3. Portfolio Governance Process Owner approval of the exact closed code sets for cancellation reason, hold
   reason, completion outcome, closure reason and benefit disposition; Initiative type and priority remain
   tenant-managed MOD-0048 classifications and are not part of this closed PPM vocabulary decision.
4. Whether a missing/ambiguous/unavailable MOD-0288 owner/governance recipient blocks `Active -> OnHold` or
   permits the lifecycle transition with a durable no-notification disposition.
5. Exact MOD-0031 and MOD-0024 producer contract versions for the already-closed optional `0..n`
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

### Change log

| Date | Change | Authority |
|---|---|---|
| 2026-09-02 | Added and corrected the governance-only Initiative Core v2 baseline: exact eight-field Golden Slim create/edit contract; nullable-in-Proposed and required-before-Active MOD-0048-owned type/priority classifications and planning dates; five PPM-owned lifecycle/closure vocabularies; action-based lifecycle and Workflow/WorkCenter boundaries; verified-recipient-only OnHold notification; exact InitiativeClosure requiredness/cardinalities; terminal supersession; authoritative-owner typed links; repository-accurate future allowlist/protected paths; HTTP matrix, acceptance/test gates and explicit owner blockers. No runtime/frontend/service/Gateway/migration/seed/deployment/activation authority was created. | User / Portfolio Governance Process Owner |
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
