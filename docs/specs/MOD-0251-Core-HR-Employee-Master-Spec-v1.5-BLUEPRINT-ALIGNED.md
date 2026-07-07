# MOD-0251 — Core HR / Employee Master Specification

**Version:** v1.10 P2 runtime smoke closure status
**Status:** Blueprint-aligned authoritative specification for the contained P2 baseline; P2 draft/reference-validation slice closed as CONDITIONAL PASS; full lifecycle remains gated by later approval
**Previous version:** v1.9 registry read-only scope contract; v1.4 is superseded and is not the governing spec file
**Architecture model:** Internal-only HCM
**Module code:** MOD-0251
**Module name:** Core HR / Employee Master
**Release:** R1 mapped to W-1 — Internal Human Capital Management Foundation MVP
**Lane:** R1-A — Internal HCM Master Foundation
**Persistence target:** L3 — survives full database/environment rebuild through migrations and seed/restore path
**Development rule:** Runtime vertical slice mandatory; no shell UI
**Recommended next Codex action:** Read-only post-containment inspection or governance-only next-scope decision; do not start submit/approval/activation or broader lifecycle work without a later approved scope

---

## 0. Version Changelog

| Version | Date | Change |
|---|---:|---|
| v1.0 | 2026-06-16 | Initial Core HR / Employee Master development handoff draft |
| v1.1 | 2026-06-16 | Closed pre-development P0 gaps: pattern decisions, API contract depth, workflow contract, evidence policy, employee-number policy, government-identifier policy, field sensitivity, concurrency, idempotency, implementation topology, build plan, and first inspection gate |
| v1.2 | 2026-06-16 | Tightened implementation gate; clarified MOD-0023 approval ownership; made direct approval endpoint a delegation/adapter only; confirmed implementation must not begin until inspection resolves topology and dependency contracts |
| v1.3 | 2026-06-16 | Closed residual contract-readiness gaps: aligned approval-decision endpoint with MOD-0023 ownership, reduced first runtime slice to activation-only, moved evidence/status/export/data-quality work to later build sequences, added deterministic evidence test policy, gated government identifier capture, added DTO/audit/NFR closure registers |
| v1.4 | 2026-06-16 | Final pre-inspection cleanup: clarified first-slice wizard evidence-step behavior, added MVP sequencing note, added page-level G/W/T acceptance checklist, tightened P1 contract-closure execution rule, and updated Codex prompt reference to v1.4 |
| v1.5 | 2026-06-17 | Blueprint v7 alignment cleanup; added Blueprint metadata, Core HR Master capability group, W-1 mapping, HCM-CORE-BUNDLE, support model, SLO tier, and Job Assignment ownership clarification. |
| v1.6 | 2026-06-20 | Governance wording reconciliation after final P2 browser PASS; clarified P2 draft/reference-validation completion and preserved full lifecycle exclusions. |
| v1.7 | 2026-06-21 | Spec/module-pack authority alignment after containment rework; declares this file as the active spec, records v1.4 as superseded/not standalone-authoritative, and documents blocked lifecycle routes, deferred gateway change, and next-sequence decisions. |
| v1.8 | 2026-06-21 | Registry/detail scope decision: P2 support visibility is limited to draft wizard/reload/reference-validation/review; employee registry and employee detail are not part of P2 runtime smoke and move to later approved read-only sequences. |
| v1.9 | 2026-06-21 | Governance-only Employee Registry read-only scope contract for later `MOD0251-P4-REGISTRY-READ-M1`; defines records, API/UI boundary, RBAC, masking, tenant isolation, tests, and deferred lifecycle/export/detail scope. |
| v1.10 | 2026-06-21 | P2 runtime smoke closure report recorded as CONDITIONAL PASS for Create Draft / reference-validation only; negative lifecycle route proof remains test/source-backed rather than browser-fetch-backed. |

---

## 1. Executive Summary

`MOD-0251 Core HR / Employee Master` is the internal authoritative employee and employment master module for the Human Capital Management Foundation.

It replaces the earlier external-HRIS assumption. The platform will not consume Workday, SuccessFactors, Oracle HCM, or any other external HRIS for employee master data in this architecture. Employee, employment status, and core employment records are created, governed, versioned, audited, and persisted internally.

### Future Product Objective

Enable HR users to create, approve, maintain, search, and audit employee master and employment records, while exposing stable contracts to downstream HCM and Talent Ecosystem modules.

This is the broader product objective for later approved sequences. It is not the current executable authority.

### Current Approved Runtime Outcome

```text
HR Admin opens Create Employee draft flow
→ creates a draft
→ saves valid draft steps with ETag
→ reloads the persisted draft
→ validates existing person/company/reference data where current P2 contracts support it
→ opens review/readiness state
→ draft state remains persisted
→ no submit/approval/activation behavior is reachable
```

### Development Readiness Position

Development **must not expand into broad implementation** from the completed P2 slice. P2 covers only draft create, save/update with ETag, reload, person/organization-unit/position/legal-entity reference validation, and non-submit review. Any full Employee Master lifecycle work must proceed in later approved vertical slices.

### Authoritative Baseline Decision

This file, `docs/specs/MOD-0251-Core-HR-Employee-Master-Spec-v1.5-BLUEPRINT-ALIGNED.md`, is the active MOD-0251 authoritative specification despite the historical `v1.5` filename. The internal document version is v1.10 after this closure status alignment. A standalone v1.4 spec file is not present in the current repository; v1.4 exists only as lineage in this document's changelog and is superseded by v1.5 Blueprint alignment, v1.6 P2 governance wording, v1.7 containment authority update, v1.8 registry/detail scope decision, v1.9 registry read-only scope contract, and this v1.10 closure status update.

Current implementation authority is jointly:

- this v1.10 authoritative spec;
- `execution/domains/human-capital-management/module-packs/MOD-0251-core-hr-employee-master.md`;
- `docs/qa/acceptance-reports/MOD-0251-scope-containment-rework-2026-06-21.md`.
- `docs/qa/acceptance-reports/MOD-0251-next-scope-decision-2026-06-21.md`;
- `docs/qa/acceptance-reports/MOD-0251-P4-registry-read-scope-contract-2026-06-21.md`.

The current contained baseline is explicit: submit/workflow-decision routes are blocked by design with controlled failure responses until later approval; Gateway remains unchanged; backend-level containment is the current control; evidence/status/export/data-quality and lifecycle finalization remain deferred.

### Next-Sequence Decision Table

| Sequence | Status | Owner / Dependency | Required Decision Before Runtime Work |
|---|---|---|---|
| P2 runtime smoke closure | CONDITIONAL PASS; closed only for Create Draft / reference-validation | MOD-0251 / HCM | Preserve draft create/save/reload/reference-validation/review only. Negative submit/workflow proof is test/source-backed rather than browser-fetch-backed and does not authorize lifecycle work. |
| Registry read-only sequence | Deferred, not P2 support | MOD-0251 + MOD-0314 for sensitive response shaping | Approve exact search/list DTO, non-sensitive columns, RBAC, tenant isolation, empty/error/forbidden states, and no export/status/evidence/actions before registry runtime smoke. |
| Detail read-only sequence | Deferred, not P2 support | MOD-0251 + MOD-0314 for sensitive response shaping | Approve exact detail projection, sensitive-field policy, read-only affordance rules, and no status/evidence/audit placeholders before detail runtime smoke. |
| Lifecycle contract authorization | Deferred | MOD-0251 + HCM product owner | Approve exact submit, approval, activation, persistence, audit, idempotency, and rollback contract before code changes. |
| MOD-0023 callback-source authorization closure | Deferred blocker | MOD-0023 Workflow | Define authenticated callback/event source, replay/idempotency, tenant validation, and allowed decision payloads before workflow decisions are consumed. |
| `employee.created.v1` event contract closure | Deferred blocker | MOD-0251 event owner + downstream HCM consumers | Close event name, schema, safe payload, outbox behavior, replay/idempotency, and consumer expectations before any employee-created/activation event is emitted. |
| MOD-0314 masking contract closure | Deferred blocker for sensitive reads/export | MOD-0314 HR Governance & Sensitive Access Controls | Close masking/deny policy evaluation and response shaping before sensitive detail, registry, or export fields are enabled. |
| Evidence/retention contract closure | Deferred blocker | MOD-0028 Documentation, MOD-0031 Evidence Linking, MOD-0030 Records/Retention | Close evidence attachment, evidence-link metadata, retention/legal-hold, and failure behavior before evidence controls or evidence-gated activation are enabled. |
| Export/masking contract closure | Deferred blocker | MOD-0251 + MOD-0314 + MOD-0021 Audit | Close governed export limits, masking, audit requested/completed events, file/job ownership, and retention before export affordances or endpoints are enabled. |
| Data-quality queue sequence | Deferred | MOD-0251 + data governance owner | Approve queue entity, duplicate policy, assignment/resolution workflow, audit, and UI scope before queue runtime behavior is added. |

### Registry / Detail Scope Decision

Prompt ID: `MOD0251-NEXT-SCOPE-DECISION-M1`.

Decision: Employee Registry and Employee Detail are not P2 support surfaces. They may be developed only as later approved, non-lifecycle, read-only sequences. P2 runtime smoke must not include `/HCM/Employees` or `/HCM/Employees/{employeeId}`. P2 evidence remains limited to `/HCM/Employees/Create` and the draft API/proxy flow required for draft create, save/update, reload, reference validation, and non-submit review.

Allowed in P2:

- Create Employee Draft wizard host.
- Draft reload/resume inside the Create Draft surface.
- Draft state, reference validation, and non-submit review evidence.
- Controlled disabled messaging for submit/approval/activation/evidence/export/status/data-quality/government identifier controls.

Not allowed in P2:

- Employee Registry as a support or smoke surface.
- Employee Detail as a support or smoke surface.
- Showing Active employee records as P2 evidence.
- Showing draft-only records in Employee Registry.
- Detail status history, evidence, audit, export, data-quality, or lifecycle action panels.

Later read-only registry/detail sequences must remain non-lifecycle: no submit, approval/rejection, activation, status mutation, evidence upload/link, export, Data Quality Queue workflow, government identifier capture, or employee-created event/outbox behavior is authorized by this decision.

### Employee Registry Read-Only Scope Contract

Prompt ID: `MOD0251-P4-REGISTRY-READ-SCOPE-CONTRACT-M1`.

This section is a governance-only contract for a later implementation prompt, expected to be `MOD0251-P4-REGISTRY-READ-M1`. It does not change runtime code and does not make Employee Registry a P2 support surface.

#### Purpose and Boundary

Employee Registry is a read-only table/bulk-view surface for authorized HR users to find tenant-scoped employee records. The first registry read sequence is not lifecycle execution, not draft review, not export, not Employee Detail editing, and not a status/evidence/audit display surface.

The registry sequence must not authorize:

- submit for approval;
- approval or rejection;
- employee activation;
- status mutation or status-history timeline;
- evidence upload/link/retention controls;
- audit panel/query display;
- export, download, print, or DataTables local export fallback;
- Data Quality Queue workflow;
- government identifier capture, tokenization, display, or search.

#### Allowed Records

First registry read sequence may show only approved read-only employee records from one of these sources:

- migrated or seeded read-only employee records explicitly marked as registry-safe for local/test or approved migration scenarios;
- controlled local/test fixture records created and cleaned up through an approved fixture endpoint/pattern;
- future Active employee records only after a later lifecycle/activation sequence exists and has its own runtime evidence.

Draft-only records must not appear in Employee Registry. P2 draft sessions must not be joined into registry rows. If no approved registry-safe records exist, the correct result is an empty state, not synthetic UI data.

#### API Contract Stub

Route: `GET /api/v1/hcm/employees`.

Required behavior:

- Server resolves `TenantId`; client-supplied `TenantId` is ignored or rejected.
- Permission: `mod0251.employee.search`.
- Response uses the standard `Response<T>` envelope.
- Pagination is mandatory; default `page=1`, default `pageSize=25`, maximum `pageSize=100`.
- Sorting is allowlisted only.
- Filtering is tenant-scoped and cannot reveal cross-tenant counts.
- Errors return controlled envelope/ProblemDetails without stack traces, tokens, connection strings, or secrets.
- Search logging/audit may record safe metadata only: tenant, actor, correlation id, filter names, result count bucket, and elapsed time. Search logs must not include raw search terms when they may contain PII unless a later MOD-0021/MOD-0314 policy explicitly allows redacted terms.

Allowed query parameters:

| Parameter | Type | Notes |
|---|---|---|
| `page` | integer | 1-based page number. |
| `pageSize` | integer | Default 25, max 100. |
| `search` | string | Matches allowed non-sensitive tokens such as employee number or approved display name; no government identifier search. |
| `status` | string | Read-only status filter; first sequence should usually allow approved registry-safe statuses only. |
| `workerType` | string | Controlled value. |
| `employmentType` | string | Controlled value. |
| `organizationUnitId` | GUID | Tenant-scoped reference filter. |
| `positionId` | GUID | Tenant-scoped reference filter. |
| `legalEntityId` | GUID | Tenant-scoped reference filter. |
| `sensitivityLevel` | string | Filter only; response must still enforce masking. |
| `sortBy` | string | Allowlist: `employeeNumber`, `displayName`, `workerType`, `employmentType`, `status`, `hireDate`, `updatedAt`. |
| `sortDirection` | string | `asc` or `desc`; default stable sort is `updatedAt desc`. |

Response DTO stub:

```text
EmployeeRegistrySearchResponse
  page
  pageSize
  totalCount
  items[]

EmployeeRegistryRowDto
  employeeId
  employeeNumber
  displayName
  workerType
  employmentType
  organizationUnitId
  organizationUnitDisplay
  positionId
  positionDisplay
  legalEntityId
  legalEntityDisplay
  status
  hireDate
  sensitivityLevel
  updatedAtUtc
  actions: { canView: boolean, canExport: false }
```

Masking / sensitive-field behavior:

- Do not return date of birth, personal email, phone, government identifier raw/token/hash, evidence ids, audit payloads, or private notes.
- `displayName`, `employeeNumber`, `hireDate`, and `sensitivityLevel` must be shaped by the current approved sensitive-read policy. If MOD-0314 masking is not contract-closed, the first registry implementation must either return only non-sensitive fixture-safe values or fail closed for sensitive fields.
- `canExport` must be `false` until export/masking contract closure.

#### UI Contract

Route: `/HCM/Employees`.

Layout and route boundary:

- Tenant shell only: `Layout = "_LayoutTenantShell";`.
- Same-origin frontend proxy only: browser calls `/HCM/Employees/api`; proxy calls Gateway `5000`.
- Browser JS must not call service ports `5056`, `5057`, `5059`, or `5060`.
- DataTable v2 with `data-dt-standard="v2"` and Compact registry/list structure.

Required UI columns for first read-only sequence:

- Employee Number.
- Display Name.
- Worker Type.
- Employment Type.
- Organization Unit.
- Position.
- Legal Entity.
- Status.
- Hire Date.
- Sensitivity.
- Last Updated.

Required UI states:

- Loading.
- Empty state when no approved registry-safe records exist.
- Filtered empty.
- Permission denied / permission-filtered.
- Controlled error state.
- Masked/sensitive-field unavailable state where applicable.

UI exclusions:

- No export button, DataTables export button, CSV/Excel/PDF/print action, or hidden export affordance.
- No row action opening Employee Detail unless a detail sequence is separately approved.
- No submit, approval, activation, evidence, status-history, audit, Data Quality Queue, or government identifier controls.
- No status/evidence/audit placeholder cards.

#### Registry Acceptance Criteria

| ID | Given | When | Then |
|---|---|---|---|
| REG-GWT-01 | An HR user has `mod0251.employee.search` and same-tenant approved registry-safe records exist. | The user opens `/HCM/Employees` and searches the registry. | The registry returns a paged tenant-scoped list through Gateway-backed API, with only approved columns and no lifecycle/export/detail controls. |
| REG-GWT-02 | The user lacks `mod0251.employee.search`. | The user opens or queries the registry. | Server-side authorization denies access with 401/403 or a permission-filtered state; no rows or counts leak. |
| REG-GWT-03 | Tenant A and tenant B both have employee records. | A tenant B user searches registry. | Tenant A rows, identifiers, and counts are not returned. |
| REG-GWT-04 | No approved registry-safe records exist for the tenant. | The user opens the registry. | The UI shows an empty state and does not synthesize rows from drafts or fixtures. |
| REG-GWT-05 | A field is sensitive or governed by MOD-0314 policy. | Registry data is returned. | The value is masked, omitted, or fail-closed according to the approved policy; DOB, personal email, phone, and government identifiers are never displayed. |
| REG-GWT-06 | The user can search registry. | The registry renders toolbar/actions. | Export is absent or disabled because export/masking is deferred. |
| REG-GWT-07 | Detail read scope has not been approved. | Registry rows render. | Row detail actions are absent or disabled; no `/HCM/Employees/{employeeId}` smoke evidence is required. |
| REG-GWT-08 | Filters/sort/page are supplied. | The registry query executes. | Only allowlisted filters/sorts are accepted; invalid filters return controlled validation errors without stack traces or secrets. |

#### Registry Test Expectations

- Backend query tests for pagination, sorting allowlist, filters, and response DTO shape.
- RBAC tests for `mod0251.employee.search` allowed/denied behavior.
- Tenant isolation tests proving cross-tenant rows and counts are not disclosed.
- Masking/sensitive-field tests proving no DOB, personal email, phone, government identifier raw/token/hash, evidence ids, or audit payloads are returned.
- Empty-state/fixture-safe tests proving draft sessions do not appear in registry results.
- Frontend UI state tests for loading, empty, filtered empty, error, permission-denied, and no export/detail/lifecycle controls.
- Gateway route test only if gateway scope is explicitly approved for the implementation prompt.
- No lifecycle dependency, no activation dependency, no export dependency, and no Employee Detail dependency.

#### Deferred From Registry Read Sequence

- Employee Detail.
- Export.
- Status history.
- Evidence panel or evidence upload/link.
- Audit panel/query.
- Data Quality Queue.
- Lifecycle activation, submit, approval/rejection, workflow decision processing, and `employee.created.v1`.

### v1.5 Blueprint Alignment Position

This version does not change the MOD-0251 architecture or runtime implementation plan. It restores the v1.4 inspection-ready specification and aligns its metadata, dependency gates, ownership notes, and interface registry with Blueprint v7:

- the completed P2 runtime slice is draft/reference-validation only;
- the Create Employee wizard must keep evidence upload/link and government identifier capture disabled in this slice;
- UI work must not expand from inferred DTOs;
- later write UI or lifecycle slices require exact DTOs, validation contracts, audit payload contracts, and contract tests before implementation;
- Blueprint v7 places MOD-0251 in Core HR Master, W-1, HCM Foundation App, Tier 2, with `HCM-CORE-BUNDLE` as the minimum integration contract bundle.

---

## 2. Strategic Framing

### Business Objective

Create a governed internal employee foundation that supports:

- employee legal profile management;
- employment record management;
- employment lifecycle state management;
- HR-sensitive access controls;
- auditability and evidence linkage;
- downstream HCM workflows;
- future Talent Ecosystem Platform handoff.

### Product Context

`MOD-0251` is part of the internal HCM foundation and sits upstream of these consumers.

| Consumer | Use of MOD-0251 |
|---|---|
| `MOD-0288 Organization, Person & Position Directory` | Person, organization, and position reference alignment |
| `MOD-0298 Employee Profile Workspace` | Employee profile surface and HR workspace |
| `MOD-0299 Position & Organization Assignment` | Employee-position-org-manager assignment |
| `MOD-0305 Offboarding & Exit Management` | Termination/offboarding context and status update |
| `MOD-0314 HR Governance & Sensitive Access Controls` | Sensitive access policy enforcement |
| `MOD-0322 Industry Candidate Identity & Talent Profile` | Future Talent Ecosystem identity mapping |
| `MOD-0327 Industry Exit Reference Record Registry` | Future reference-record creation after offboarding |

### Explicit Non-Goals

`MOD-0251` does **not** implement:

- payroll calculation;
- time, attendance, leave, or absence tracking;
- compensation or benefits management;
- performance reviews;
- skills or competency assessment;
- employee relations case management;
- offboarding workflow execution;
- Talent Ecosystem candidate identity;
- external HRIS integration;
- position directory ownership;
- organization-unit directory ownership;
- employee self-service editing in R1;
- multi-country statutory field packs in R1 beyond the minimal core defined in this spec.

---

## 2A. Blueprint Alignment

| Blueprint Field | MOD-0251 Value |
|---|---|
| Domain / Landscape | 4) Enterprise Application Ecosystem |
| Suite / Platform | Human Capital Management Foundation |
| Capability Group | Core HR Master |
| Wave | W-1 |
| Placement | Domain App (HCM Foundation) |
| Deployment Unit / Product | HCM Foundation App |
| Build / Buy / Partner | Build |
| SLO Tier | Tier 2 |
| L1 Queue | Service Desk |
| L2 Owning Ops | People Ops / IAM Ops |
| L3 Engineering | HCM Product Engineering |
| Implementation Phase | HR Foundation MVP |
| Min Integration Contract Bundle | HCM-CORE-BUNDLE |
| SoR Applicability | Y |
| SoR Notes | Internal HCM master with native HCM ownership. |

Blueprint v7 confirms `MOD-0251 Core HR / Employee Master` as a native HCM Foundation domain app. This section is metadata alignment only and does not authorize runtime expansion beyond the approved P2 draft/reference-validation slice.

---

## 3. Module Boundary Card

| Item | Specification |
|---|---|
| Module code | `MOD-0251` |
| Module name | Core HR / Employee Master |
| Domain / Landscape | 4) Enterprise Application Ecosystem |
| Suite / Platform | Human Capital Management Foundation |
| Capability Group | Core HR Master |
| Release / Wave | R1 mapped to W-1 |
| Implementation Phase | HR Foundation MVP |
| Placement | Domain App (HCM Foundation) |
| Deployment Unit / Product | HCM Foundation App |
| SLO Tier | Tier 2 |
| Lane | R1-A |
| Module type | Native HCM master |
| Primary users | HR Admin, HR Manager, HR Contributor, HR Data Steward, Audit Admin |
| Key owned objects | Employee, Employment Record, Job Assignment / Employment Assignment Snapshot, Employee Status History, Employee Identifier, Employee Document Link, Employee Data Quality Case, Employee Draft Session |
| Consumed platform services | RBAC, Audit, Workflow, Documentation/Evidence, Records Retention, Evidence Linking, Reference Data, Taxonomy, Observability |
| Exposed interfaces | Employee command/query APIs; employee lifecycle events; HCM-CORE-BUNDLE |
| Compliance posture | Sensitive HR data; audit, evidence, masking, retention, tenant isolation mandatory |
| Implementation shape | Runtime vertical slice, not shell UI |

---

## 4. Ownership and Boundary Map

### 4.1 Owned by MOD-0251

| Object / Capability | Ownership Decision |
|---|---|
| Employee legal profile | Owned by `MOD-0251`; MOD-0251 owns Employee |
| Employment record | Owned by `MOD-0251`; MOD-0251 owns Employment Record |
| Job Assignment / Employment Assignment Snapshot | Owned by `MOD-0251` as the employee master assignment snapshot; workflow/orchestration remains with `MOD-0299` |
| Employment status | Owned by `MOD-0251` |
| Employee status history | Owned by `MOD-0251`; immutable append-only |
| Employee identifiers | Owned by `MOD-0251`; raw government identifier not stored in R1 |
| Employee draft session | Owned by `MOD-0251`; used for guided creation only |
| Employee data-quality case | Owned by `MOD-0251` |
| Employee document link metadata | Owned by `MOD-0251`; binary/document storage is consumed from `MOD-0028` |
| Employee lifecycle event publication | Owned by `MOD-0251`; consumed by downstream modules |

### 4.2 Not Owned by MOD-0251

| Object / Capability | Owner |
|---|---|
| Platform person directory | `MOD-0288` |
| Organization unit directory | `MOD-0288`; MOD-0288 owns organization reference directories |
| Position directory | `MOD-0288`; MOD-0288 owns position reference directories |
| Assignment workflow/orchestration | `MOD-0299`; MOD-0299 owns assignment workflow/orchestration |
| HR workspace presentation | `MOD-0298` |
| Offboarding workflow | `MOD-0305` |
| HR-sensitive policy rules | `MOD-0314` |
| Payroll | `MOD-0279` |
| Time, attendance, leave | `MOD-0280` |
| Compensation and benefits | `MOD-0316` |
| Talent Ecosystem candidate identity | `MOD-0322` |
| Binary document storage | `MOD-0028` |
| Evidence link registry | `MOD-0031` |
| Retention/legal hold policy | `MOD-0030` |
| Workflow engine | `MOD-0023` |
| Audit event store | `MOD-0021` |
| RBAC policy engine | `MOD-0018` |

### 4.3 Boundary Rule

`MOD-0251` owns Employee, Employment Record, and the Job Assignment / Employment Assignment Snapshot needed by the employee master. It must not own assignment workflow/orchestration, organization reference directories, position reference directories, payroll, performance, offboarding, leave/absence, or Talent Ecosystem data.

---

## 5. Pattern Selection — Mandatory Before Development

| Surface / Flow | Pattern | Justification |
|---|---|---|
| Employee Registry | Table / bulk view | Expert HR users need to search, filter, and scan approved employee records efficiently. Export is a later governed sequence, not part of the first registry read-only contract. |
| Create Employee | Wizard with terminal approval submission | Employee creation is an infrequent, sequentially dependent, evidence-bearing submission that requires validation before activation. |
| Employee Master Detail | Single detail / section page | Employee records are repeatedly viewed and edited over the employment lifecycle. |
| Employment Record Section | Detail-page section with independent section save | Employment data may change independently from legal profile data and each mutation must be auditable. |
| Employee Status History | Report / timeline | Status history is immutable and read-oriented, with links to workflow/evidence/audit. |
| Employee Data Quality Queue | Inbox / table | HR Data Stewards process many duplicate/missing-data cases with assignment and resolution workflow. |
| Export | Restricted reporting action | Export is a governed output, not a CRUD page. |

### Pattern Boundary Rule

- Wizard is used **only** for initial employee creation and activation submission.
- After activation, repeat edits occur on the Employee Master Detail page, not in the wizard.
- Data Quality Queue may open Employee Master Detail but does not own employee objects.

---

## 6. Required Dependencies

### 6.1 Blueprint Dependency Gate

These Blueprint v7 dependency gates must exist before MOD-0251 implementation proceeds. If any contract is unavailable or ambiguous, the relevant runtime slice is blocked rather than inferred.

| Dependency | Purpose | Readiness Requirement Before MOD-0251 Implementation |
|---|---|---|
| `MOD-0018 RBAC / Authorization` | HR role and permission control | Permission seed pattern and server-side enforcement available |
| `MOD-0021 Audit Trail` | Access/change/export/approval audit | Audit emit/query contract available |
| `MOD-0023 Workflow` | Approval workflow for activation, suspension, termination, sensitive updates | Workflow start/approve/reject/status contract available |
| `MOD-0030 Records Management / Retention / Legal Hold` | Retention policy and legal hold enforcement | Retention policy lookup and legal hold check available |
| `MOD-0048 Reference Data / Lookups` | Employment statuses, worker types, contract types, reason categories | Controlled values seeded and queryable |
| `MOD-0057 Taxonomy / Semantic Tagging` | Employee classification, role/job categories, tags | Optional in R1; required only for tag visibility |

### 6.2 Additional Implementation Dependencies

These dependencies are required by the MOD-0251 implementation plan and downstream integration scope, but they are tracked separately from the Blueprint dependency gate.

| Dependency | Purpose | Readiness Requirement Before MOD-0251 Implementation |
|---|---|---|
| `MOD-0028 Documentation / Evidence Management` | Internal evidence/document storage | Document/evidence create or reference contract available |
| `MOD-0031 Evidence Linking Service` | Link evidence to employee and employment records | Evidence link create/query contract available |
| `MOD-0288 Organization, Person & Position Directory` | Person, organization, and position reference directories | Person, organization, and position reference contracts available; reference ownership remains with MOD-0288 |
| `MOD-0314 HR Governance & Sensitive Access Controls` | HR-sensitive visibility and masking rules | Policy evaluation/masking contract available |
| Tokenization/security service for government identifier capture | Token/hash capture for regulated identifiers | Raw government identifier storage remains prohibited; tokenization contract required before capture is enabled |

---

## 7. Core Data Model

### 7.1 Employee

| Field | Type | Required | Sensitivity | Notes |
|---|---:|---:|---|---|
| employee_id | UUID | Yes | Internal | Internal employee identifier |
| employee_number | string(32) | Yes | Internal | Unique per tenant; generated by policy |
| tenant_id | UUID | Yes | Internal | Tenant isolation |
| person_id | UUID | Yes | Confidential | Reference to `MOD-0288` person; must pre-exist in R1 |
| legal_first_name | string(100) | Yes | PII | Required before activation |
| legal_middle_name | string(100) | No | PII | Optional |
| legal_last_name | string(100) | Yes | PII | Required before activation |
| preferred_name | string(100) | No | PII | Optional |
| date_of_birth | date | Conditional | PII | Optional in R1 unless required by tenant country policy |
| nationality | string(2) | Conditional | PII | ISO-3166 alpha-2 where legally required |
| government_identifier_token | string | Conditional | Secret/PII | Token/hash only; raw value not stored |
| work_email | string(254) | No | Confidential | Unique per tenant if provided |
| personal_email | string(254) | No | PII | Restricted |
| phone | string(32) | No | PII | E.164 preferred |
| employee_status | enum | Yes | Internal | Draft / Pending Approval / Active / On Leave / Suspended / Terminated / Rehired / Rejected |
| worker_type | enum | Yes | Internal | Employee / Contractor / Intern / Consultant / Other |
| employment_type | enum | Yes | Internal | Full-time / Part-time / Temporary / Contract |
| hire_date | date | Conditional | Confidential | Required before activation |
| termination_date | date | Conditional | Confidential | Required when terminated |
| sensitivity_level | enum | Yes | Internal | Standard / Restricted / Legal Only |
| created_by | UUID | Yes | Internal | User reference |
| created_at | datetime UTC | Yes | Internal | System-generated |
| updated_by | UUID | Yes | Internal | User reference |
| updated_at | datetime UTC | Yes | Internal | System-generated |
| version | integer | Yes | Internal | Optimistic concurrency |
| etag | string | Yes | Internal | Response concurrency token |

### 7.2 Employment Record

| Field | Type | Required | Sensitivity | Notes |
|---|---:|---:|---|---|
| employment_record_id | UUID | Yes | Internal | Internal employment record ID |
| employee_id | UUID | Yes | Internal | Parent employee |
| tenant_id | UUID | Yes | Internal | Tenant isolation |
| company_id | UUID | Yes | Confidential | Company/legal-entity reference from `MOD-0288` or legal-entity provider confirmed by implementation inspection |
| start_date | date | Yes | Confidential | Employment start |
| end_date | date | Conditional | Confidential | Employment end |
| contract_type | enum | Yes | Internal | Permanent / Fixed-term / Contractor / Internship |
| probation_status | enum | No | Internal | Not Applicable / Active / Passed / Failed / Extended |
| probation_end_date | date | No | Confidential | Optional |
| employment_status | enum | Yes | Internal | Active / Leave / Suspended / Terminated |
| termination_reason_category | enum | Conditional | Confidential | Controlled value; required for termination |
| rehire_eligibility | enum | No | Confidential | Eligible / Conditional / Not Eligible / Not Assessed |
| source_creation_method | enum | Yes | Internal | Manual Entry / Migration / System Generated |
| approval_status | enum | Yes | Internal | Draft / Submitted / Approved / Rejected |
| version | integer | Yes | Internal | Optimistic concurrency |
| etag | string | Yes | Internal | Response concurrency token |

### 7.3 Employee Draft Session

| Field | Type | Required | Sensitivity | Notes |
|---|---:|---:|---|---|
| draft_session_id | UUID | Yes | Internal | Wizard draft session |
| tenant_id | UUID | Yes | Internal | Tenant isolation |
| created_by | UUID | Yes | Internal | Draft owner |
| resume_policy | enum | Yes | Internal | AuthorOnly / HRAdmin / Delegated |
| current_step | enum | Yes | Internal | PersonLink / LegalProfile / Employment / Evidence / Review |
| step_status_json | JSON | Yes | Internal | Deterministic step states |
| draft_schema_version | string | Yes | Internal | Example: `employee-create-wizard.v1` |
| draft_payload_json | JSON | Yes | PII | Encrypted at rest; redacted in logs |
| expires_at | datetime UTC | Yes | Internal | Default 30 days after last update |
| abandoned_at | datetime UTC | No | Internal | Set when expired/archived |
| submitted_at | datetime UTC | No | Internal | Set on terminal submit |
| version | integer | Yes | Internal | Optimistic concurrency |

### 7.4 Employee Status History

| Field | Type | Required | Sensitivity | Notes |
|---|---:|---:|---|---|
| status_history_id | UUID | Yes | Internal | Internal ID |
| employee_id | UUID | Yes | Internal | Employee reference |
| tenant_id | UUID | Yes | Internal | Tenant isolation |
| previous_status | enum | No | Internal | Null for first status |
| new_status | enum | Yes | Internal | New employee status |
| effective_date | date | Yes | Confidential | Effective date |
| reason_category | enum | Conditional | Confidential | Controlled value |
| reason_note | text | Conditional | PII/Confidential | Controlled free text; not for accusations |
| triggered_by_module | string | Yes | Internal | Example: `MOD-0251` or `MOD-0305` |
| workflow_reference_id | UUID | No | Internal | Workflow reference from `MOD-0023` |
| approval_reference_id | UUID | No | Internal | Approval reference |
| created_by | UUID | Yes | Internal | User ID |
| created_at | datetime UTC | Yes | Internal | Audit timestamp |

### 7.5 Employee Document Link

| Field | Type | Required | Sensitivity | Notes |
|---|---:|---:|---|---|
| employee_document_link_id | UUID | Yes | Internal | Internal ID |
| employee_id | UUID | Yes | Internal | Employee reference |
| tenant_id | UUID | Yes | Internal | Tenant isolation |
| evidence_id | UUID | Yes | Confidential | From `MOD-0028` / `MOD-0031` |
| document_type | enum | Yes | Confidential | Contract / ID / Certificate / Permit / Other |
| visibility_level | enum | Yes | Internal | Public HR / Restricted HR / Legal Only |
| retention_policy_id | UUID | Yes | Internal | From `MOD-0030` |
| linked_by | UUID | Yes | Internal | User ID |
| linked_at | datetime UTC | Yes | Internal | Audit timestamp |
| version | integer | Yes | Internal | Optimistic concurrency |

### 7.6 Employee Data Quality Case

| Field | Type | Required | Sensitivity | Notes |
|---|---:|---:|---|---|
| data_quality_case_id | UUID | Yes | Internal | Internal ID |
| tenant_id | UUID | Yes | Internal | Tenant isolation |
| employee_id | UUID | No | Internal | Optional when duplicate candidate is not confirmed |
| case_type | enum | Yes | Internal | Duplicate Candidate / Missing Required Data / Invalid Status / Conflicting Identifier |
| severity | enum | Yes | Internal | Low / Medium / High / Blocking |
| status | enum | Yes | Internal | Open / In Review / Resolved / Rejected |
| assigned_to | UUID | No | Internal | HR Data Steward |
| resolution_note | text | Conditional | Confidential | Required on close |
| created_at | datetime UTC | Yes | Internal | System-generated |
| resolved_at | datetime UTC | No | Internal | System-generated |
| version | integer | Yes | Internal | Optimistic concurrency |

---

## 8. Employee Lifecycle State Machine

```text
Draft
→ Pending Approval
→ Active
→ On Leave
→ Suspended
→ Terminated
→ Rehired
```

Additional terminal/non-terminal states:

```text
Pending Approval → Rejected → Draft
Draft → Abandoned
```

| From | To | Trigger | Approval Required | Evidence Required | Event |
|---|---|---|---:|---:|---|
| Draft | Pending Approval | HR submits employee record | No | Conditional | none |
| Pending Approval | Active | HR Manager approves | Yes | Conditional | `employee.created.v1` |
| Pending Approval | Rejected | HR Manager rejects | Yes | No | none |
| Rejected | Draft | HR Admin revises rejected record | No | No | none |
| Draft | Abandoned | Draft expires or HR cancels | No | No | none |
| Active | On Leave | HR status update | Conditional | Conditional | `employee.status.changed.v1` |
| Active | Suspended | Sensitive HR action | Yes | Yes | `employee.status.changed.v1` |
| Suspended | Active | Reinstatement | Yes | Conditional | `employee.status.changed.v1` |
| Active | Terminated | Offboarding / termination | Yes | Yes | `employee.terminated.v1` |
| Terminated | Rehired | Rehire process | Yes | Yes | `employee.status.changed.v1` |

### R1 State Scope

| State / Transition | R1 Scope |
|---|---|
| Draft → Pending Approval → Active | In scope |
| Pending Approval → Rejected → Draft | In scope |
| Active → Suspended → Active | In scope only as controlled HR status change |
| Active → Terminated | In scope as MOD-0305 command and HR Manager command if allowed by policy |
| Terminated → Rehired | Contract stub only; full rehire orchestration deferred |
| On Leave | Status field allowed; leave workflow deferred to leave/absence module |

---

## 9. Runtime Vertical-Slice Development Rules — Mandatory

Every implementation package for `MOD-0251` must be delivered as a runtime vertical slice.

### Mandatory Runtime Rules

| Rule | Requirement |
|---|---|
| Golden flow first | Build a complete user flow before broad feature expansion. |
| No shell UI | No operational-looking UI without real API, persistence, RBAC, audit, and validation. |
| Contract blocker | Missing API, DTO, event, or ownership decision must block implementation; do not invent contracts silently. |
| L3 persistence | Employee master, draft session, status history, audit, and evidence links must survive database rebuild/migration flow. |
| RBAC server-side | UI hiding is insufficient; every endpoint must enforce authorization. |
| Audit mandatory | Create, update, view-sensitive, export, approval, rejection, status change, and evidence-link actions must emit audit events. |
| Evidence linkage | Activation, suspension, termination, and rehire must support evidence links where policy requires. |
| Tenant isolation | Tenant A must not read or mutate Tenant B employee records. |
| Concurrency | Editable records require version/ETag; stale updates are rejected with controlled conflict state. |
| Idempotency | Create/submit/approve/status/document/export actions require idempotency keys. |
| Failure paths | Validation failure, permission denial, duplicate employee, workflow failure, audit failure, evidence failure, conflict, and duplicate-submit must be controlled. |
| Runtime smoke | Completion requires a runtime smoke test, not only compile/test pass. |
| Final verdict | Implementation report must end with PASS / CONDITIONAL PASS / PARTIAL / BLOCKED / FAIL. |

### Completed P2 Runtime Slice — Draft / Reference Validation Only

The completed P2 runtime slice proves draft/reference-validation behavior only. Evidence integration, submit, approval/rejection, activation, MOD-0023 workflow, `employee.created`, termination/status-change, export, Data Quality Queue, and government identifier capture/tokenization are intentionally sequenced later.

```text
HR Admin logs in
→ starts Create Employee wizard
→ links existing MOD-0288 person reference
→ completes approved draft/reference-validation fields
→ saves draft with ETag
→ reloads draft
→ validates person, organization-unit, position, and legal-entity references
→ enters non-submit review
→ review state is `reviewed` with `blockingReasons=[]`
```

### Sub-Flows Proven in the P2 Runtime Slice

- draft create with tenant context and `mod0251.employee.create_draft`;
- draft save/update with ETag;
- draft reload persistence by `draftSessionId`;
- reference validation for person, organization unit, position, and legal entity;
- non-submit review state with no submit/approval/Active controls;
- direct browser service-port prohibition.

### Explicitly Deferred from the P2 Runtime Slice

| Deferred Flow | Build Sequence | Reason |
|---|---:|---|
| Submit / approval / activation | 6 | Requires executable MOD-0023 workflow contract and later approved scope |
| `employee.created` event/outbox | 6 | Requires activation lifecycle scope |
| Evidence link integration | 8 | Requires MOD-0028/MOD-0031/MOD-0030 contracts confirmed by inspection |
| Active → Terminated status change | 9 | Separate status-change/workflow side-effect surface |
| Restricted masked export | 11 | Requires platform export pattern and masking policy hook |
| Data Quality Queue UI/actions | 10 | Separate inbox/table workflow surface |
| Government identifier capture/tokenization | later approved security/tokenization sequence | Tokenization/security contract is not approved for P2 |

---

## 10. Wizard Draft Rules — Create Employee

### 10.1 Step State Machine

```text
Not Started → In Progress → Complete → Error → Blocked
```

| Step | Step Code | Purpose | Required Before Next |
|---|---|---|---|
| 1 | `person-link` | Link existing person reference from `MOD-0288` | Valid person reference and tenant match |
| 2 | `legal-profile` | Capture legal name and sensitive fields | Required fields and sensitivity validation |
| 3 | `employment-record` | Capture company, worker type, employment type, hire date | Required employment fields and reference-data validation |
| 4 | `evidence` | Attach required evidence if policy requires | Required documents linked or waived by policy |
| 5 | `review-submit` | Review all data and submit for approval | All steps complete and server-side revalidation passed |

### 10.2 Draft Lifecycle

| Rule | Decision |
|---|---|
| Draft owner | Author by default |
| Resume policy | AuthorOnly for HR Contributor; HRAdmin may resume delegated drafts |
| Draft retention | 30 days after last update |
| Abandonment | Expired drafts move to Abandoned; no employee record activated |
| Terminal commit | Only final Submit creates/submits employee approval package |
| Intermediate saves | Draft only; not lifecycle commits |
| Draft schema version | `employee-create-wizard.v1` |
| Schema change behavior | If draft schema changes, attempt migration; if not possible, block with controlled message and preserve old payload for admin review |
| Audit | Draft create/update/submit/abandon audited with redacted payload |

---

## 11. Business Policies Closed for R1

### 11.1 Country Policy

| Policy Area | R1 Decision |
|---|---|
| Launch country mode | Geography-neutral minimal global core |
| Country-specific mandatory packs | Deferred to post-MVP country packs |
| DOB | Optional unless tenant country policy requires it |
| Nationality | Optional unless tenant country policy requires it |
| Government identifier | Token/hash only; raw value not stored |
| Statutory reporting | Deferred |

### 11.2 Employee Number Policy

| Rule | R1 Decision |
|---|---|
| Generation model | Auto-generated by MOD-0251 |
| Sequence scope | Tenant-scoped |
| Format | `EMP-{tenantShortCode}-{YYYY}-{sequence6}` |
| Collision behavior | Retry sequence allocation transactionally; if still collision, controlled validation error |
| Manual override | HR Admin only before activation, audited, must remain unique per tenant |
| Edit after activation | Not allowed except controlled correction workflow |
| Idempotency for create | `Idempotency-Key` header + `tenant_id + draft_session_id` during wizard submit; final employee number assigned once |

### 11.3 Government Identifier Policy

| Rule | R1 Decision |
|---|---|
| Raw government ID storage | Prohibited in MOD-0251 R1 |
| Stored value | Tokenized/hash reference only |
| Tokenization owner | Platform security/privacy service if present; otherwise implementation blocks until tokenization contract is identified |
| Duplicate detection | Use token/hash equality where legally allowed |
| Audit payload | Never store raw or token value in audit payload; store only `identifier_present: true/false` |
| Logs/traces | Never log raw or tokenized value |
| Display | Never display government ID in R1 except masked presence indicator |
| First implementation slice | Government identifier capture is excluded unless inspection confirms an approved tokenization/security contract |

### 11.4 Retention Policy

| Record Type | Default R1 Retention |
|---|---|
| Employee master | Retain for employment term + 7 years unless tenant policy overrides |
| Employment record | Retain for employment term + 7 years |
| Status history | Retain for employment term + 7 years; immutable |
| Employee document link | Retain according to linked retention policy from `MOD-0030` |
| Draft session | 30 days after last update; archived metadata retained 1 year |
| Data quality case | Retain 3 years after resolution |
| Audit event | Controlled by MOD-0021 audit retention policy |
| Legal hold | Legal hold from MOD-0030 blocks deletion/anonymization |

---

## 12. Evidence Policy Matrix

| Action / Transition | Evidence Required | Document Types | Visibility | Retention |
|---|---:|---|---|---|
| Draft creation | No | N/A | N/A | Draft retention |
| Activation approval | Conditional | Contract / Offer / Permit / Other | Restricted HR | MOD-0030 policy |
| Sensitive legal-field update | Conditional | Correction Evidence / Legal Document | Restricted HR / Legal Only | MOD-0030 policy |
| Suspension | Yes | HR Decision Evidence / Legal Document | Legal Only | MOD-0030 policy |
| Reinstatement | Conditional | Approval Evidence | Restricted HR | MOD-0030 policy |
| Termination | Yes | Termination Approval / Exit Record | Legal Only | MOD-0030 policy |
| Rehire | Yes | Rehire Approval / Eligibility Evidence | Restricted HR | MOD-0030 policy |
| Export | No document evidence, but audit required | N/A | N/A | Audit retention |

### 12.1 Deterministic R1 Evidence Test Policy

| Test Scope | Activation Evidence Policy | Rule |
|---|---|---|
| Future activation runtime slice | Not required unless policy says otherwise | A later approved activation slice may prove draft → submit → MOD-0023 approval → Active → reload without evidence dependency only after lifecycle authorization and workflow callback-source authorization are closed. |
| Evidence integration slice `MOD0251-P8-EVIDENCE-LINK-M1` | Required | Test tenant must set `activation_evidence_required = true` and verify evidence attach/link/retention before approval. |
| Production behavior | Tenant policy driven | Activation evidence remains conditional by tenant policy and must default to block when policy requires evidence but dependency contracts are unavailable. |

### Evidence Failure Behavior

| Failure | Required System Behavior |
|---|---|
| MOD-0028 unavailable | Block evidence attachment and show controlled error |
| MOD-0031 link failure | Do not mark evidence step complete |
| MOD-0030 retention lookup failure | Block link creation; no orphaned employee_document_link |
| Evidence permission denied | Return 403 and audit denial |
| Evidence missing where required | Block submit/approval |

---

## 13. Workflow Contract with MOD-0023

Current P2 containment status: MOD-0251 does not start MOD-0023 workflows and does not consume approval/rejection decisions in the approved runtime baseline. Submit and workflow-decision routes, where compiled, are controlled blocked surfaces until a later approved lifecycle contract closes MOD-0023 callback-source authorization, idempotency, audit, event, and Active-state materialization rules. This section is future contract planning, not current implementation authorization.

### 13.1 Workflow Types

| Workflow | Trigger | Owner | Consumer |
|---|---|---|---|
| `employee.activation.approval.v1` | HR Admin submits employee | MOD-0023 | MOD-0251 |
| `employee.sensitive-update.approval.v1` | Restricted field/status update | MOD-0023 | MOD-0251 |
| `employee.status-change.approval.v1` | Suspension, termination, rehire | MOD-0023 | MOD-0251 / MOD-0305 |

### 13.2 Approval Ownership Rule

Approval ownership remains with `MOD-0023`. `MOD-0251` may expose an approval-decision convenience endpoint only if it delegates to `MOD-0023` or processes authenticated `MOD-0023` decision events. `MOD-0251` must not implement an independent approval engine, duplicate workflow state ownership, or bypass the workflow service for approval-grade decisions.

| Rule | Requirement |
|---|---|
| Approval system of record | `MOD-0023` |
| MOD-0251 role | Subject module and employee-state owner after approved workflow decision |
| Direct UI approval endpoint | Allowed only as an adapter/delegation surface, not independent approval ownership |
| Workflow callback processing | Must authenticate source, enforce idempotency, and reject unauthorized callbacks |
| Audit ownership | MOD-0251 emits employee-domain audit events; MOD-0023 owns workflow-decision audit |
| Blocking condition | If MOD-0023 workflow contract is unavailable or ambiguous, employee activation approval is BLOCKED |

### 13.3 Workflow Commands

| Command | Direction | Purpose |
|---|---|---|
| `POST /api/v1/workflows/instances` | MOD-0251 → MOD-0023 | Start approval workflow |
| `workflow.approved.v1` | MOD-0023 → MOD-0251 | Notify approved decision |
| `workflow.rejected.v1` | MOD-0023 → MOD-0251 | Notify rejected decision |
| `workflow.cancelled.v1` | MOD-0023 → MOD-0251 | Notify cancelled workflow |

### 13.4 Workflow Payload

```json
{
  "workflow_type": "employee.activation.approval.v1",
  "tenant_id": "uuid",
  "subject_module": "MOD-0251",
  "subject_type": "Employee",
  "subject_id": "uuid",
  "requested_by": "uuid",
  "requested_at_utc": "datetime",
  "risk_level": "Standard | Restricted | LegalOnly",
  "evidence_required": true,
  "evidence_ids": ["uuid"],
  "correlation_id": "string",
  "idempotency_key": "string"
}
```

### 13.5 Workflow Failure Behavior

| Failure | Required Behavior |
|---|---|
| Workflow start fails | Employee remains Draft or Pending Submission; submit returns controlled error; no Active status |
| Workflow approval callback duplicate | Idempotent; no duplicate status history/audit/event |
| Workflow approval callback unauthorized | Reject callback, audit security event |
| Workflow unavailable during approval | Approval cannot complete; employee state unchanged |
| Workflow rejected | Employee moves to Rejected, rejection reason visible to permitted HR roles |

---

## 14. API Contract Matrix

Current P2 containment status: only draft create, draft patch/save, draft reload, draft reference validation, and non-submit review are operationally authorized. Submit, approval-decision, Active employee materialization, status-history, evidence, export, and data-quality endpoints are future/deferred unless a later approved module-pack/prompt explicitly promotes them. Backend-level route blocking is the current control for lifecycle routes; Gateway remains unchanged by the containment baseline.

### 14.1 Common API Rules

| Rule | Requirement |
|---|---|
| Base path | `/api/v1/hcm/employees` |
| Auth | Bearer token + server-side RBAC |
| Tenant | Tenant derived from authenticated context; request tenant must match context where provided |
| Correlation ID | Required; generate if missing |
| Idempotency | Required for all write/submit/export commands |
| Concurrency | `If-Match` header required for PATCH/status/document mutation after entity creation |
| Error shape | Standard problem-details format |
| Sensitive values | Masked/redacted in response according to MOD-0314 policy |
| Audit | Every create/update/view-sensitive/export/status/evidence action audited |
| Pagination | Cursor or page/size; default 25, max 100 for list |
| Export cap | Default max 10,000 rows unless tenant policy lower |

### 14.2 Error Model

```json
{
  "type": "https://errors.diten.local/hcm/employee-validation",
  "title": "Employee validation failed",
  "status": 400,
  "code": "employee.validation_failed",
  "correlation_id": "string",
  "errors": [
    {
      "field": "hire_date",
      "code": "required_before_activation",
      "message": "Hire date is required before activation."
    }
  ]
}
```

| Status | Code | Meaning |
|---:|---|---|
| 400 | `employee.validation_failed` | Field/business validation failed |
| 401 | `auth.unauthenticated` | Missing/invalid auth |
| 403 | `employee.permission_denied` | Server-side permission denied |
| 404 | `employee.not_found` | Employee not found or tenant mismatch |
| 409 | `employee.conflict` | Version/ETag conflict |
| 409 | `employee.duplicate_detected` | Duplicate candidate detected |
| 422 | `employee.workflow_blocked` | Workflow/evidence/retention dependency blocks operation |
| 503 | `employee.downstream_unavailable` | Required dependency unavailable |

### 14.3 Endpoints

#### POST `/api/v1/hcm/employees/drafts`

| Field | Specification |
|---|---|
| Owner | `MOD-0251` |
| Purpose | Start Create Employee wizard draft |
| Permission | `mod0251.employee.create_draft` |
| Idempotency | `Idempotency-Key` header required |
| Persistence | L3 |
| Audit | `employee_draft.created` |
| Response | Draft session ID, current step, version, etag |

#### PATCH `/api/v1/hcm/employees/drafts/{draftSessionId}`

| Field | Specification |
|---|---|
| Purpose | Save wizard step payload |
| Permission | `mod0251.employee.create_draft` or delegated resume permission |
| Concurrency | `If-Match` required |
| Idempotency | `Idempotency-Key` header required |
| Audit | `employee_draft.updated` |
| Response | Updated draft session, step statuses, etag |

#### POST `/api/v1/hcm/employees/drafts/{draftSessionId}/submit`

Current contained baseline: compiled route surfaces must return a controlled blocked response and must not start workflow, emit submit/activation events, create employee records, write status history, or mutate lifecycle state until lifecycle scope is approved.

| Field | Specification |
|---|---|
| Purpose | Terminal submit for approval |
| Permission | `mod0251.employee.submit` |
| Idempotency | `Idempotency-Key` header required |
| Side effects | Employee draft locked; employee pending approval; workflow started; audit emitted |
| Consistency | Atomic with audit; workflow start failure blocks submit |
| Audit | `employee.submitted_for_approval` |
| Response | Employee ID, workflow reference, status Pending Approval |

#### POST `/api/v1/hcm/employees/{employeeId}/approval-decision`

Current contained baseline: not authorized for implementation. Any compiled workflow-decision/callback route must return a controlled blocked response and must not process MOD-0023 decisions or mutate employee lifecycle state until callback-source authorization and lifecycle contracts are approved.

| Field | Specification |
|---|---|
| Purpose | Delegation/adapter endpoint for HR Manager approval action where UI approval is supported |
| Ownership rule | Approval remains owned by `MOD-0023`; this endpoint must not implement independent approval logic |
| Allowed behavior | Call/delegate to `MOD-0023` or process an authenticated `MOD-0023` workflow decision event |
| Permission | `mod0251.employee.approve` plus workflow permission/policy enforced by `MOD-0023` |
| Idempotency | `Idempotency-Key` header required; duplicate decision is no-op with prior result |
| Allowed MOD-0251 mutation | Employee status may change to Active or Rejected only after a valid MOD-0023 approval/rejection decision is confirmed |
| Side effects after valid workflow decision | Status history, employee version, MOD-0251 audit, employee lifecycle event/outbox if approved |
| Blocking condition | If MOD-0023 approval contract is unavailable or ambiguous, this endpoint must not be implemented |
| Audit | `employee.approved` / `employee.rejected` in MOD-0251; workflow-decision audit remains owned by MOD-0023 |
| Response | Employee summary, status, version, etag, workflow decision reference |

#### GET `/api/v1/hcm/employees/{employeeId}`

| Field | Specification |
|---|---|
| Purpose | Retrieve employee detail |
| Permission | `mod0251.employee.view` plus sensitive-field policy |
| Masking | Mandatory by role/sensitivity level |
| Audit | `employee.view_sensitive` if sensitive fields returned; normal metadata view may be sampled by policy |
| Response | Employee detail DTO with masked fields and etag |

#### GET `/api/v1/hcm/employees`

| Field | Specification |
|---|---|
| Purpose | Registry search/list |
| Permission | `mod0251.employee.search` |
| Filters | employee number, name, status, worker type, company, hire date, sensitivity level |
| Pagination | Required; default 25, max 100 |
| Sorting | employee_number, name, status, hire_date, updated_at |
| Audit | Export audited; search audit per policy |
| Response | Paged employee registry rows |

#### PATCH `/api/v1/hcm/employees/{employeeId}`

| Field | Specification |
|---|---|
| Purpose | Update employee legal/profile fields after activation |
| Permission | `mod0251.employee.edit_legal` or limited edit permission |
| Concurrency | `If-Match` required |
| Idempotency | `Idempotency-Key` header required |
| Workflow | Required for sensitive fields based on MOD-0314 policy |
| Audit | `employee.profile.updated` |
| Event | `employee.profile.updated.v1` after approval/effective mutation |
| Response | Updated employee detail, version, etag |

#### PATCH `/api/v1/hcm/employees/{employeeId}/employment-records/{employmentRecordId}`

| Field | Specification |
|---|---|
| Purpose | Update employment record |
| Permission | `mod0251.employee.edit_employment` |
| Concurrency | `If-Match` required |
| Idempotency | `Idempotency-Key` header required |
| Audit | `employee.employment_record.updated` |
| Event | `employee.profile.updated.v1` if downstream-relevant |
| Response | Updated employment record, version, etag |

#### POST `/api/v1/hcm/employees/{employeeId}/status`

| Field | Specification |
|---|---|
| Purpose | Change employment status |
| Permission | `mod0251.employee.change_status` |
| Consumers | HR Manager UI, `MOD-0305` |
| Concurrency | `If-Match` required |
| Idempotency | `Idempotency-Key` header required |
| Workflow | Required for termination, suspension, rehire |
| Side effects | Status history, employee version, audit event, lifecycle event |
| Persistence | L3 |
| Response | Employee status summary, status history ID, version, etag |

#### POST `/api/v1/hcm/employees/{employeeId}/documents`

| Field | Specification |
|---|---|
| Purpose | Link evidence/document to employee record |
| Permission | `mod0251.employee.attach_evidence` |
| Consumes | `MOD-0028`, `MOD-0031`, `MOD-0030` |
| Idempotency | `Idempotency-Key` header + `employee_id + evidence_id` |
| Audit | `employee.evidence_linked` |
| Response | Employee document link summary |

#### GET `/api/v1/hcm/employees/{employeeId}/status-history`

| Field | Specification |
|---|---|
| Purpose | Retrieve immutable status timeline |
| Permission | `mod0251.employee.view_status_history` |
| Sorting | `effective_date desc`, `created_at desc` |
| Audit | Sensitive view audit if reason notes visible |
| Response | Timeline rows with linked workflow/evidence references |

#### GET `/api/v1/hcm/employee-data-quality-cases`

| Field | Specification |
|---|---|
| Purpose | Data Quality Queue list |
| Permission | `mod0251.data_quality.view` |
| Filters | case_type, severity, status, assigned_to |
| Pagination | Required |
| Response | Paged data-quality cases |

#### PATCH `/api/v1/hcm/employee-data-quality-cases/{caseId}`

| Field | Specification |
|---|---|
| Purpose | Assign, resolve, or reject data-quality case |
| Permission | `mod0251.data_quality.resolve` |
| Concurrency | `If-Match` required |
| Idempotency | Required |
| Audit | `employee_data_quality_case.updated` |
| Response | Updated data-quality case |

#### POST `/api/v1/hcm/employees/export`

| Field | Specification |
|---|---|
| Purpose | Export employee registry results |
| Permission | `mod0251.employee.export` |
| Idempotency | Required |
| Masking | Export respects viewer masking and MOD-0314 policy |
| Formats | CSV for R1 |
| Limits | Max 10,000 rows unless tenant policy lower |
| Filename | `employees_{tenant}_{yyyyMMddHHmmss}_{correlationId}.csv` |
| Audit | `employee.export.requested` and `employee.export.completed` |
| Response | Export job or file metadata depending existing platform export pattern; endpoint remains deferred until `MOD0251-P11-EXPORT-M1` and inspection confirms platform export convention |

---

### 14.4 DTO Schema Closure Register — Required Before `MOD0251-P1-CONTRACTS-M1`

The endpoint tables above define behavior and ownership. `MOD0251-P1-CONTRACTS-M1` must close the exact request/response DTO schemas before controllers/UI are implemented.

| DTO | Direction | Required Closure |
|---|---|---|
| `EmployeeDraftCreateRequest` | Request | idempotency key, optional source context, no PII payload at creation |
| `EmployeeDraftCreateResponse` | Response | draft_session_id, current_step, step_statuses, version, etag |
| `EmployeeDraftStepPatchRequest` | Request | step_code, payload_schema_version, step payload, client validation state, idempotency key, if-match |
| `EmployeeDraftStepPatchResponse` | Response | updated step statuses, validation summary, version, etag |
| `EmployeeSubmitRequest` | Request | draft_session_id, final validation acknowledgment, idempotency key, workflow/evidence policy context |
| `EmployeeSubmitResponse` | Response | employee_id, employee_number if assigned, workflow_reference_id, status, version, etag |
| `EmployeeApprovalDecisionRequest` | Request | workflow_reference_id, decision, decision_reason, idempotency key; must delegate/validate against MOD-0023 |
| `EmployeeDetailResponse` | Response | employee header, legal profile, employment records, status, masked fields, sensitivity flags, version, etag |
| `EmployeeRegistryRowResponse` | Response | employee_number, masked name, worker_type, employment_type, company, status, sensitivity, updated_at, action permissions |
| `EmployeeProfilePatchRequest` | Request | changed fields only, if-match, idempotency key, sensitivity-policy evaluation context |
| `EmploymentRecordPatchRequest` | Request | changed employment fields only, if-match, idempotency key, reference-data validation context |
| `EmployeeStatusCommandRequest` | Request | new_status, effective_date, reason_category, reason_note, workflow/evidence refs, if-match, idempotency key |
| `EmployeeDocumentLinkRequest` | Request | evidence_id, document_type, visibility_level, retention_policy_id, idempotency key |
| `DataQualityCasePatchRequest` | Request | assignment/status/resolution fields, if-match, idempotency key |
| `EmployeeExportRequest` | Request | filters, columns, format, masking policy context, idempotency key; deferred to P11 |

### 14.5 Safe Audit Payload Register — Required Before Write Implementation

Every audit payload must use server-authoritative UTC time, authenticated actor, tenant, correlation ID, target reference, and safe metadata only. PII/secret values are prohibited.

| Audit Event | Safe Payload Minimum | Prohibited Payload |
|---|---|---|
| `employee_draft.created` | tenant_id, actor_id, draft_session_id, correlation_id | draft payload values |
| `employee_draft.updated` | tenant_id, actor_id, draft_session_id, step_code, version, correlation_id | legal names, DOB, gov-ID token, emails, phone |
| `employee.submitted_for_approval` | tenant_id, actor_id, employee_id, draft_session_id, workflow_reference_id, correlation_id | full employee payload |
| `employee.approved` | tenant_id, actor_id, employee_id, workflow_reference_id, status, version, correlation_id | approval comments containing PII unless redacted |
| `employee.rejected` | tenant_id, actor_id, employee_id, workflow_reference_id, status, reason_category, correlation_id | unredacted rejection free text |
| `employee.profile.updated` | tenant_id, actor_id, employee_id, changed_field_names, version, correlation_id | before/after PII values |
| `employee.employment_record.updated` | tenant_id, actor_id, employee_id, employment_record_id, changed_field_names, version, correlation_id | sensitive free-text values |
| `employee.status.changed` | tenant_id, actor_id, employee_id, previous_status, new_status, status_history_id, workflow_reference_id, correlation_id | reason_note raw text |
| `employee.evidence_linked` | tenant_id, actor_id, employee_id, evidence_id, document_type, visibility_level, correlation_id | document content or file metadata beyond approved reference |
| `employee.export.requested` | tenant_id, actor_id, filter_hash, requested_columns, requested_format, row_limit, correlation_id | exported data rows |
| `employee.export.completed` | tenant_id, actor_id, export_job_id, row_count, masked=true/false, correlation_id | exported data rows or file contents |
| `employee.access_denied` | tenant_id, actor_id, permission, target_type, target_id, correlation_id | target PII values |

### 14.6 Contract Closure Rule

`MOD0251-P1-CONTRACTS-M1` must produce DTO files, validation contracts, audit payload contracts, reference-data seed contracts, audit payload contracts, and contract tests. UI prompts must not start against inferred DTOs. UI static route shell work is allowed only when contracts are approved and the route/component convention is confirmed by inspection; otherwise it is BLOCKED by the no-shell rule. If a DTO, audit payload, dependency contract, migration location, or route convention cannot be confirmed, the implementation prompt returns BLOCKED.

---

## 15. Event Contract Stubs

### 15.1 Common Event Metadata

All events include:

```json
{
  "event_id": "uuid",
  "event_name": "employee.created.v1",
  "schema_version": "1.0",
  "tenant_id": "uuid",
  "correlation_id": "string",
  "occurred_at_utc": "datetime",
  "producer": "MOD-0251",
  "idempotency_key": "string"
}
```

### 15.2 `employee.created.v1`

| Field | Specification |
|---|---|
| Trigger | Employee record approved and becomes Active |
| Consumers | `MOD-0288`, `MOD-0298`, `MOD-0299`, `MOD-0314` |
| Payload | employee_id, person_id, status, worker_type, employment_type, hire_date, company_id, sensitivity_level, version, correlation_id |
| Delivery | At-least-once |
| Idempotency key | employee_id + version |
| Failure | If event publish fails after DB commit, outbox retry required; no direct duplicate publish |

### 15.3 `employee.status.changed.v1`

| Field | Specification |
|---|---|
| Trigger | Employment status changes |
| Consumers | `MOD-0298`, `MOD-0305`, `MOD-0314`, later `MOD-0322`, `MOD-0327` |
| Payload | employee_id, previous_status, new_status, effective_date, reason_category, status_history_id, version |
| Criticality | High |
| Delivery | At-least-once with idempotency key |
| Failure | Outbox retry with dead-letter after configured threshold |

### 15.4 `employee.terminated.v1`

| Field | Specification |
|---|---|
| Trigger | Employee status becomes Terminated |
| Consumers | `MOD-0305`, later `MOD-0327` |
| Payload | employee_id, termination_date, reason_category, rehire_eligibility, offboarding_reference_id, status_history_id, version |
| Criticality | High |
| Delivery | At-least-once |

### 15.5 `employee.profile.updated.v1`

| Field | Specification |
|---|---|
| Trigger | Employee profile or employment data changes after approval |
| Consumers | `MOD-0298`, `MOD-0314` |
| Payload | employee_id, changed_fields, version, updated_at_utc, correlation_id |
| Criticality | Medium |
| Privacy | Changed field names only; no sensitive values in payload |

---

## 16. RBAC Matrix

### 16.1 Roles

| Role | Scope |
|---|---|
| HR Admin | Full HR master management except technical admin |
| HR Manager | Approves and manages employee status within policy |
| HR Contributor | Draft creation and limited employee view |
| HR Data Steward | Data-quality queue and duplicate review |
| Department Manager | Team-only read; depends on MOD-0299 assignment availability |
| Employee | Own profile read only is deferred unless MOD-0319/self-service is enabled |
| Audit Admin | Audit/metadata review, no PII unless policy grants |
| Platform Admin | Technical metadata only, no HR PII |

### 16.2 Permission Codes

| Permission Code | Purpose |
|---|---|
| `mod0251.employee.search` | Search employee registry |
| `mod0251.employee.view` | View employee detail |
| `mod0251.employee.view_sensitive` | View sensitive fields |
| `mod0251.employee.create_draft` | Start/save employee creation draft |
| `mod0251.employee.submit` | Submit employee for approval |
| `mod0251.employee.approve` | Approve/reject employee activation |
| `mod0251.employee.edit_legal` | Edit legal profile |
| `mod0251.employee.edit_employment` | Edit employment record |
| `mod0251.employee.change_status` | Change employment status |
| `mod0251.employee.attach_evidence` | Link evidence/document |
| `mod0251.employee.export` | Export employee records |
| `mod0251.employee.view_status_history` | View status timeline |
| `mod0251.data_quality.view` | View data-quality cases |
| `mod0251.data_quality.resolve` | Assign/resolve/reject data-quality cases |

### 16.3 Role Matrix

| Role | Search | View | View Sensitive | Create Draft | Submit | Approve | Edit Legal | Edit Employment | Change Status | Attach Evidence | Export |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| HR Admin | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Yes |
| HR Manager | Yes | Yes | Limited | Yes | Yes | Yes | Limited | Yes | Yes | Yes | Restricted |
| HR Contributor | Yes | Yes | Limited | Yes | Draft only | No | No | Draft only | No | Limited | No |
| HR Data Steward | Yes | Yes | Limited | No | No | No | No | No | No | No | No |
| Department Manager | Team only | Team only | Limited | No | No | No | No | No | No | No | No |
| Employee | Deferred | Own only if enabled | Own allowed fields | No | No | No | No | No | No | No | No |
| Audit Admin | Metadata | Metadata | No by default | No | No | No | No | No | No | No | Metadata only |
| Platform Admin | Technical metadata | Technical metadata | No | No | No | No | No | No | No | No | No |

---

## 17. Page Specifications

### 17.1 Page 1 — Employee Registry

| Item | Specification |
|---|---|
| Archetype | Table / bulk view |
| Users | HR Admin, HR Manager, HR Contributor, HR Data Steward |
| Purpose | Find approved registry-safe employee records in a read-only table |
| Route | `/HCM/Employees` or existing platform route discovered by inspection |
| Data Source | `GET /api/v1/hcm/employees` |
| Pattern Justification | Table/bulk view because HR users scan many employee records. First registry read scope is read-only and does not include lifecycle actions, Employee Detail, status/evidence/audit panels, or export. |

#### Columns

| Column | Notes |
|---|---|
| Employee Number | Pinned |
| Name | Masked by policy |
| Worker Type | Reference data |
| Employment Type | Reference data |
| Company | From employment record |
| Status | Employee lifecycle status |
| Hire Date | Masked/visible by policy |
| Sensitivity | Badge |
| Approval Status | Deferred unless explicitly approved; first registry read must not surface draft sessions |
| Last Updated | UTC rendered in tenant timezone |
| Actions | None in first registry read-only sequence unless a later Employee Detail sequence is separately approved |

#### Filters

- Search: employee number and approved display name tokens only; no government identifier, DOB, personal email, phone, evidence, or audit-payload search.
- Status.
- Worker type.
- Employment type.
- Company.
- Hire date range.
- Sensitivity level.
- Approval status.

#### States

- Loading.
- Empty.
- Filtered empty.
- Permission-filtered.
- Error.
- Permission-filtered masking/unavailable state.

#### Actions

| Action | Permission | Audit |
|---|---|---|
| Create Employee | `mod0251.employee.create_draft` | Link/navigation to the separately approved draft wizard only; not a registry lifecycle action. |
| Open Detail | Deferred | Disabled/absent until Employee Detail read scope is separately approved. |
| Export | Deferred | Disabled/absent until governed export/masking scope is separately approved. |
| Status / Activation | Deferred | Disabled/absent until lifecycle/status scope is separately approved. |
| Open Data Quality Case | `mod0251.data_quality.view` | Case view audit if sensitive |

---

### 17.2 Page 2 — Create Employee Wizard

| Item | Specification |
|---|---|
| Archetype | Wizard |
| Users | HR Admin, HR Manager, HR Contributor |
| Purpose | Guided employee creation and approval submission |
| Route | `/HCM/Employees/Create` or existing route discovered by inspection |
| Data Source | Draft APIs + submit API |
| Save Model | Intermediate draft saves; terminal submit once |
| Pattern Justification | Wizard because employee creation is a sequential, evidence-bearing, approval-bound process. |

#### Step Actions

| Step | Primary Action | Secondary Actions |
|---|---|---|
| Person Link | Save & Next | Cancel, Save Draft |
| Legal Profile | Save & Next | Back, Save Draft |
| Employment Record | Save & Next | Back, Save Draft |
| Evidence | Skipped/disabled in first runtime slice when `activation_evidence_required = false`; Save & Next enabled only in `MOD0251-P8-EVIDENCE-LINK-M1` | Back, Request Waiver if policy allows |
| Review & Submit | Submit for Approval | Back |

#### First-Slice Evidence-Step Rule

For `MOD0251-P5-CREATE-WIZARD-UI-M1` and the first activation runtime slice, the Evidence step must be visibly marked as **Not required for this test policy** when `activation_evidence_required = false`. The step must not show shell upload controls. Evidence upload/link controls are implemented only in `MOD0251-P8-EVIDENCE-LINK-M1` after MOD-0028/MOD-0031/MOD-0030 contracts are confirmed.

---

### 17.3 Page 3 — Employee Master Detail

| Item | Specification |
|---|---|
| Archetype | Single detail / section page |
| Users | HR Admin, HR Manager, HR Contributor |
| Purpose | Maintain legal and employment data |
| Route | `/HCM/Employees/{employeeId}` |
| Data Source | `GET/PATCH /api/v1/hcm/employees/{employeeId}` |
| Save Model | Section-level independent save; each section save audited |
| Pattern Justification | Detail page because employee records are repeatedly viewed and updated. |

#### Sections

| Section | Save Strategy | Permission |
|---|---|---|
| Header/Summary | Read-only | `mod0251.employee.view` |
| Legal Profile | Independent save | `mod0251.employee.edit_legal` |
| Employment Record | Independent save | `mod0251.employee.edit_employment` |
| Documents/Evidence | Link-only | `mod0251.employee.attach_evidence` |
| Status History | Read-only timeline | `mod0251.employee.view_status_history` |
| Audit Metadata | Read-only | Audit Admin / HR Admin |

#### Conflict Behavior

If the section ETag has changed since load:

```text
Reject save → show "Record changed by another user" → offer Reload / Review Current Version.
Never silently overwrite.
```

---

### 17.4 Page 4 — Employee Status History

| Item | Specification |
|---|---|
| Archetype | Report / timeline |
| Users | HR Admin, HR Manager, Audit Admin |
| Purpose | Track employment lifecycle changes |
| Data Source | `GET /api/v1/hcm/employees/{employeeId}/status-history` |
| Sort | Effective date descending, created date descending |

#### Timeline Fields

| Field | Notes |
|---|---|
| Previous Status | Null for first status |
| New Status | Badge |
| Effective Date | Tenant timezone display |
| Reason Category | Controlled reference |
| Reason Note | Masked by policy |
| Triggered By Module | Example `MOD-0251`, `MOD-0305` |
| Workflow Reference | Deep link if permitted |
| Evidence Reference | Deep link if permitted |
| Created By / Created At | Audit-safe metadata |

---

### 17.5 Page 5 — Employee Data Quality Queue

| Item | Specification |
|---|---|
| Archetype | Inbox / table |
| Users | HR Admin, HR Data Steward |
| Purpose | Resolve duplicates, missing fields, invalid statuses |
| Data Source | Data-quality case APIs |
| Pattern Justification | Inbox/table because HR Data Stewards process multiple exception cases. |

#### R1 Actions

| Action | Scope |
|---|---|
| Assign case | In scope |
| Mark In Review | In scope |
| Resolve missing-data case | In scope |
| Reject duplicate candidate | In scope |
| Merge candidate duplicate | Deferred unless merge contract is separately approved |

---

### 17.6 Page-Level Acceptance Checklist

These checks are backlog/test hooks. They do not override the completed P2 draft/reference-validation-only boundary.

| Page / Surface | Given | When | Then | Build Sequence |
|---|---|---|---|---:|
| Employee Registry | HR user has `mod0251.employee.search` and approved registry-safe records exist | User opens registry | Tenant-scoped read-only rows load with pagination, masking, loading/empty/error states, and no export/detail/lifecycle controls | 4 |
| Employee Registry | User lacks search permission | User opens registry | Server returns 403 or permission-filtered state; denial is audited where policy requires | 4 |
| Create Employee Wizard | HR Admin starts creation | Draft API succeeds | Draft session is created, L3 persisted, audited, and reload-resumable | 3 / 5 |
| Create Employee Wizard | First-slice tenant has `activation_evidence_required = false` | User reaches Evidence step | Step is skipped/disabled with clear message; no shell upload controls are shown | 5 |
| Create Employee Wizard | Required legal/employment data is missing | User attempts submit | Server-side validation blocks submit with controlled accessible errors | 5 / 6 |
| Approval Adapter | MOD-0023 decision is valid | Approval event/callback is processed | Employee becomes Active; status history, audit, and created event/outbox evidence exist | 6 |
| Employee Detail | User saves stale section | ETag mismatch occurs | Save is rejected with 409 and reload/review-current-version option | 7 |
| Status History | Status event exists | User opens timeline | Immutable timeline rows render in descending effective-date order with safe metadata | 9 |
| Data Quality Queue | HR Data Steward opens queue | Cases exist | Cases load with assignment/resolution controls and no merge action unless separately specified | 10 |
| Export | Authorized user requests export | Export pattern is confirmed | Masked CSV/job result is produced and audited; no unbounded export | 11 |

## 18. Validation and Business Rules

| Rule | Requirement |
|---|---|
| Employee number uniqueness | Unique per tenant |
| Person reference | Must resolve to `MOD-0288` person and tenant scope |
| Person creation | Out of scope for MOD-0251 R1; person must pre-exist |
| Hire date | Required before activation |
| Termination date | Required when status = Terminated |
| Legal name | Required for activation |
| Worker type | Must come from `MOD-0048` |
| Employment status | Must come from controlled reference data |
| Status transition | Must follow lifecycle state machine |
| Sensitive field access | Must comply with `MOD-0314` policy |
| Evidence requirement | Required based on Evidence Policy Matrix |
| Duplicate prevention | Name + DOB + government identifier token where legally allowed |
| Active employment record | Only one active employment record per employee per tenant unless approved concurrent employment policy exists |
| Employment record date overlap | Overlap blocked unless worker type and policy allow |
| Audit | Every create, update, view-sensitive, status change, approval/rejection, evidence link, export must be audited |
| Idempotency | Duplicate submit/retry cannot create duplicate employee/workflow/audit/event |
| Concurrency | Stale ETag update rejected with 409 |

---

## 19. Security, Privacy, and Compliance

### 19.1 Field-Level Classification

| Field Group | Classification | Encryption | Logs/Traces | Audit Payload | Masking |
|---|---|---:|---|---|---|
| employee_id, tenant_id, version | Internal | Standard | Allowed | Allowed | No |
| employee_number | Internal | Standard | Allowed | Allowed | Role-based no mask |
| legal names | PII | Required | Redacted | Field name only | Role-based |
| preferred name | PII | Required | Redacted | Field name only | Role-based |
| DOB | PII | Required | Redacted | Presence only | Restricted |
| nationality | PII | Required | Redacted | Field name only | Restricted |
| government identifier token | Secret/PII | Required | Never | Presence only | Never shown |
| personal email/phone | PII | Required | Redacted | Field name only | Restricted |
| work email | Confidential | Required | Redacted unless policy allows | Field name only | Role-based |
| employment status/type | Internal | Standard | Allowed | Allowed | No |
| termination reason/note | Confidential | Required | Redacted | Category only | Restricted/Legal |
| evidence IDs | Confidential | Standard | ID allowed only if policy permits | Link reference only | Role-based |

### 19.2 Masking Matrix

| Viewer | Standard Employee | Restricted Employee | Legal Only Employee |
|---|---|---|---|
| HR Admin | Full permitted fields | Full permitted fields | Masked unless Legal permission |
| HR Manager | Full standard | Limited restricted | Masked |
| HR Contributor | Limited | Masked | Masked |
| HR Data Steward | Case-relevant fields only | Case-relevant masked | Masked |
| Department Manager | Team summary only | Masked | Masked |
| Employee | Deferred in R1 unless self-service enabled | N/A | N/A |
| Audit Admin | Metadata only by default | Metadata only | Metadata only |
| Platform Admin | Technical metadata only | Technical metadata only | Technical metadata only |

### 19.3 Access Denial Audit

| Event | Payload |
|---|---|
| `employee.access_denied` | tenant_id, actor_id, permission, target_type, target_id, correlation_id, occurred_at_utc; no PII values |

---

## 20. Observability

| Signal | Requirement |
|---|---|
| Logs | employee draft created/updated/submitted, employee created/updated, status changed, evidence linked, export requested, access denied, conflict, idempotency replay |
| Metrics | employee_create_total, employee_update_total, employee_status_change_total, employee_validation_error_total, employee_permission_denied_total, employee_conflict_total, employee_idempotency_replay_total |
| Traces | UI/API → validation → persistence → audit → workflow/event emission |
| Correlation ID | Required for every command and lifecycle event |
| Alerts | repeated status-change failures, high duplicate count, audit write failure, evidence link failure |
| Redaction | No PII/secret values in logs, metrics, traces, or audit payloads |

### 20.1 R1 Non-Functional Targets

| Surface / Contract | R1 Target | Notes |
|---|---|---|
| Employee registry search API | p95 ≤ 500 ms for default page size 25 on tenant-typical dataset | Requires indexed tenant/status/name/employee_number filters |
| Employee detail API | p95 ≤ 400 ms | Masking policy call included in budget where local/cacheable |
| Draft save API | p95 ≤ 500 ms | Includes validation and encrypted draft payload persistence |
| Submit for approval API | p95 ≤ 1000 ms excluding external workflow queue latency | Must block on audit and workflow-start failure |
| Page load — Registry | Initial interactive render ≤ 2.0 s on tenant-typical dataset | Server pagination required |
| Page load — Employee Detail | Initial interactive render ≤ 2.0 s | Sensitive sections may lazy-load if policy allows |
| Export | Async/job required above 1,000 rows or if platform export pattern requires it | Full export implementation deferred to P11 |
| Search result cap | Default 25, max 100 per page | No unbounded query |
| Export row cap | Max 10,000 unless tenant policy lower | Masking mandatory |

---

## 21. Data / Migration Plan

| Item | Requirement |
|---|---|
| Schema changes | Employee, employee_draft_session, employment_record, status_history, employee_document_link, data_quality_case |
| Migration | Forward migration required |
| Seed data | Worker types, employment statuses, employee statuses, contract types, termination reason categories, data-quality case types |
| Backfill | N/A for greenfield; migration tenants require separate backfill prompt |
| Rollback | Schema rollback cannot delete real employee data in production without approved rollback policy |
| Test cleanup | Per-test tenant cleanup required |
| Retention impact | Employee and employment records retained by HR retention policy |

### 21.1 Migration Safety

- Development/test rollback may drop test tables only when data is disposable.
- Production rollback must be additive/disable-only unless approved by data owner.
- Migration must include indexes for tenant, employee number, status, work email, person reference, and updated timestamp.
- Sensitive JSON draft payload must be encrypted-at-rest or stored via platform encrypted field mechanism.

---

## 22. Interface Registry

| Contract | Owner | Consumer | Type | Version | Auth | Idempotency | Error Behavior |
|---|---|---|---|---|---|---|---|
| HCM-CORE-BUNDLE | MOD-0251 | HCM Foundation consumers, MOD-0298, MOD-0299, MOD-0305, MOD-0314 | Contract bundle | v1 | HR/module RBAC + service auth | Required for write commands; N/A read | Block or fail closed on missing dependency contract |
| Employee Draft APIs | MOD-0251 | MOD-0251 UI | API | v1 | HR RBAC | Required | Problem-details |
| Employee Query APIs | MOD-0251 | MOD-0298, MOD-0299, MOD-0305, MOD-0314 | API | v1 | HR/module RBAC | N/A read | Mask or 403 |
| Employee Status Command | MOD-0251 | MOD-0305, HR UI | API | v1 | HR/module RBAC | Required | Block on invalid transition |
| Employee Evidence Link API | MOD-0251 | HR UI | API | v1 | HR RBAC | Required | Block on downstream failure |
| Workflow Start | MOD-0023 | MOD-0251 | API | v1 | service auth | Required | Block submit on failure |
| Workflow Decision Events | MOD-0023 | MOD-0251 | Event | v1 | service auth | Required | Idempotent callback |
| Evidence Document | MOD-0028 | MOD-0251 | API | v1 | HR RBAC | Required | Block link on failure |
| Evidence Link | MOD-0031 | MOD-0251 | API | v1 | HR RBAC | Required | Block completion on failure |
| Retention Policy Lookup | MOD-0030 | MOD-0251 | API | v1 | service auth | N/A read | Block link if missing |
| RBAC Check | MOD-0018 | MOD-0251 | API/service | v1 | service auth | N/A read | 403 |
| Audit Emit | MOD-0021 | MOD-0251 | API/event | v1 | service auth | Required | Block critical writes on failure |
| Sensitive Access Policy | MOD-0314 | MOD-0251 | API/service | v1 | service auth | N/A read | Default deny/mask |
| Person Reference Lookup | MOD-0288 | MOD-0251 | API | v1 | service auth | N/A read | Block if unresolved |

---

## 23. MVP Scope

### Included in R1 MVP

```text
- Employee draft wizard
- Employee master record create/view/update
- Employment record create/view/update
- Employee status lifecycle core
- Employee status history timeline
- Person reference to MOD-0288, pre-existing person only
- Document/evidence links
- Audit events
- RBAC and sensitive field masking
- APIs for MOD-0298, MOD-0299, MOD-0305, MOD-0314
- Events for employee creation, status change, termination, profile update
- Data Quality Queue basic assignment/resolution
```

### R1 MVP Sequencing Note

The MVP scope lists the full R1 capability target. It does **not** mean every listed capability belongs in the first runtime slice. Execution sequencing remains controlled by Section 28:

| Capability | First Slice? | Build Sequence |
|---|---:|---:|
| Draft wizard core | Yes | 3 / 5 |
| Submit to MOD-0023 approval | Yes | 6 |
| Employee Active after valid approval | Yes | 6 |
| Employee registry read/search | Yes, minimum support | 4 |
| Evidence link integration | No | 8 |
| Status change / termination | No | 9 |
| Data Quality Queue | No | 10 |
| Export | No | 11 |

### Deferred to HCM Completion

```text
- payroll calculation
- time and attendance
- compensation and benefits
- performance reviews
- skills assessments
- employee relations cases
- full self-service edits
- statutory reporting
- multi-country statutory field packs
- duplicate merge workflow unless separately specified
- full rehire orchestration
- full leave/absence integration
```

---

## 24. Acceptance Criteria

| Given | When | Then |
|---|---|---|
| HR Admin starts create employee wizard | Draft is created | Draft session is L3 persisted and audited |
| HR Admin creates employee | Record is submitted and approved | Employee becomes Active and audit is written |
| HR Manager rejects employee | Rejection is submitted | Employee becomes Rejected and can be revised to Draft |
| HR Manager changes status | Status change is approved | Status history is created and event emitted |
| Employee is terminated from `MOD-0305` | Offboarding completes | Employee status becomes Terminated |
| Unauthorized user opens sensitive data | Access is attempted | System masks fields or returns 403 and audits denial |
| Employee evidence is attached | Document is linked | Evidence link and retention policy are visible |
| Duplicate employee is entered | Validation runs | Duplicate warning or data-quality case is created |
| Stale detail page is saved | ETag mismatch occurs | System returns 409 and shows conflict state |
| Submit is double-clicked | Same idempotency key is replayed | No duplicate employee/workflow/audit/event is created |
| Employee is exported | Authorized user exports | Export is masked, audited, and access-controlled |
| Page reload occurs | Employee record is reopened | Persisted employee, evidence, status history, and audit remain visible |
| Tenant B user searches | Tenant A employee exists | Tenant B cannot see Tenant A employee |

---

## 25. Runtime Validation Plan

| Validation Item | Required Evidence |
|---|---|
| Build affected projects | Build log |
| Unit tests | Passing test output |
| Integration tests | API + DB persistence validation |
| Runtime golden flow | Screenshot/log sequence or smoke-test output |
| Validation failure path | Controlled validation error evidence |
| Permission denial path | 403 or masked-field evidence |
| Duplicate path | Data-quality case evidence |
| Concurrency path | 409 conflict evidence |
| Idempotency path | Duplicate submit replay evidence |
| Audit verification | Queryable audit row evidence |
| Tenant isolation | Tenant B cannot view Tenant A employee |
| Evidence linkage | Evidence linked and retained |
| Reload persistence | Employee visible after reload/server restart/cold replay |
| Event emission | Employee lifecycle event captured |
| Redaction | Logs/traces/audit contain no PII/secret values |
| Console | No console errors |
| Migration | Migration apply and safe rollback evidence in test environment |

---

## 26. Implementation Topology

**ASSUMPTION:** Actual repo paths must be confirmed by the first inspection prompt. Codex must not invent paths if the repository differs.

| Layer | Expected Placement |
|---|---|
| Backend service | Existing HCM service if present; otherwise service boundary to be confirmed before implementation |
| Backend module namespace | `Mod0251` / `HcmEmployees` naming aligned to repo convention |
| API route family | `/api/v1/hcm/employees` |
| Gateway route | Gateway mapping to HCM service; exact route file confirmed by inspection |
| Frontend registry route | `/HCM/Employees` or existing app convention |
| Frontend create route | `/HCM/Employees/Create` |
| Frontend detail route | `/HCM/Employees/{employeeId}` |
| DB migrations | Existing HCM persistence/migration project |
| Tests | Unit, integration, API contract, UI/runtime smoke tests in existing test conventions |
| Reports | `reports/MOD-0251/` |

### Implementation Blocker Rule

If the inspection cannot locate the owning backend service, migration project, gateway configuration, or frontend route convention, the first implementation prompt must return **BLOCKED**, not create ad hoc locations.

---

## 27. Implementation Gate — Mandatory

Before any implementation prompt after `MOD0251-P0-INSPECT-M1`, Codex must confirm the repository-specific facts below. If any item is unresolved, the next step is spec/topology closure, not coding.

| Gate Item | Required Evidence from Inspection | Coding Decision |
|---|---|---|
| Backend owner service | Existing service/project path identified | Required before entities/controllers |
| Migration location | Existing persistence/migration convention identified | Required before schema work |
| Gateway convention | Route mapping file/project identified | Required before API exposure |
| Frontend route convention | HCM page route/component pattern identified | Required before UI work |
| MOD-0023 workflow hook | Start workflow + decision event/callback pattern found | Required before approval implementation |
| MOD-0028/MOD-0031 evidence hook | Document/evidence link pattern found | Required before evidence step implementation |
| MOD-0030 retention hook | Retention lookup/legal hold pattern found | Required before document-link persistence |
| MOD-0288 person/company reference | Person and company/legal-entity SoR confirmed | Required before employee/employment validation |
| MOD-0314 masking hook | Sensitive access policy/masking adapter confirmed | Required before sensitive data response shaping |
| Tokenization/security service | Token/hash contract identified if gov-ID capture is enabled | Required before government identifier field is implemented |
| DTO and audit payload closure | Exact request/response DTOs and safe audit payloads approved in P1 | Required before UI wiring and write endpoints |
| Export platform pattern | Existing export job/file convention identified | Required before `MOD0251-P11-EXPORT-M1`; otherwise export remains deferred |

### Gate Outcome Rules

| Inspection Verdict | Allowed Next Action |
|---|---|
| PASS | Start `MOD0251-P1-CONTRACTS-M1` |
| CONDITIONAL PASS | Start only confirmed contracts; isolate unresolved dependencies in separate closure prompt |
| PARTIAL | Update this spec with inspection findings before coding |
| BLOCKED | Resolve missing owner/contract/path first |
| FAIL | Rework module boundary or dependency assumptions |

## 28. Build Plan and Parallelization

### 28.1 Build Sequences

| Sequence | Prompt ID | Profile | Scope | Depends On | Parallel-Safe With |
|---:|---|---|---|---|---|
| 0 | `MOD0251-P0-INSPECT-M1` | C | Read-only repo readiness inspection | None | Documentation/spec refinement |
| 1 | `MOD0251-P1-CONTRACTS-M1` | B | Entities, DTOs, API contracts, migrations, seeds | Seq 0 PASS/CONDITIONAL PASS | UI static route shell only if contract agreed |
| 2 | `MOD0251-P2-RBAC-AUDIT-SEED-M1` | B | Permission seeds, audit event names, sensitive policy hooks | Seq 0 | Some UI wiring |
| 3 | `MOD0251-P3-DRAFT-WIZARD-BE-M1` | B | Draft session APIs and L3 persistence | Seq 1,2 | Registry read UI |
| 4 | `MOD0251-P4-REGISTRY-READ-M1` | A | Employee Registry read-only/search table per the P4 registry-read contract; no detail/export/lifecycle actions | Seq 1,2 and `MOD0251-P4-REGISTRY-READ-SCOPE-CONTRACT-M1` | Draft wizard UI if APIs stable |
| 5 | `MOD0251-P5-CREATE-WIZARD-UI-M1` | A | Create wizard UI using draft APIs | Seq 3 | Data-quality queue basic read |
| 6 | `MOD0251-P6-WORKFLOW-APPROVAL-M1` | B/A | Submit, workflow start, approve/reject callback/direct UI | Seq 3,5 | Status history read |
| 7 | `MOD0251-P7-EMPLOYEE-DETAIL-M1` | A | Employee detail section saves with ETag | Seq 6 | Export if read contract stable |
| 8 | `MOD0251-P8-EVIDENCE-LINK-M1` | B/A | Evidence/document link integration | Seq 6 | Status history read |
| 9 | `MOD0251-P9-STATUS-CHANGE-M1` | B/A | Status change command, history, events | Seq 6,8 | Data-quality queue |
| 10 | `MOD0251-P10-DQ-QUEUE-M1` | A | Data Quality Queue assignment/resolution | Seq 1,2 | Export |
| 11 | `MOD0251-P11-EXPORT-M1` | A/B | Restricted masked CSV export | Seq 4,2 | DQ queue |
| 12 | `MOD0251-P12-RUNTIME-CLOSURE-M1` | C | Golden-flow runtime validation and closure report | Seq 1-11 | None |

### 28.2 Parallel Safety Rules

| Can Run Parallel | Condition |
|---|---|
| Registry read UI + backend draft API | If no shared DTO changes are occurring |
| Data Quality Queue UI + export | If both consume stable contracts |
| Status History UI + Evidence Link backend | If status-history DTO is stable |
| RBAC/audit seed + page UI | If permission codes already frozen |

| Cannot Run Parallel | Reason |
|---|---|
| Entity/migration changes + API contract changes | Same persistence surface |
| Draft wizard UI + draft API contract changes | Same DTO/flow surface |
| Approval workflow + status finalization | Same lifecycle side effects |
| Evidence link + activation approval if evidence is mandatory | Same approval gate |
| Export + masking policy changes | Same sensitive data surface |

---

## 29. First Codex Prompt — Inspection Gate

Use this before implementation.

```text
Prompt metadata:
- Prompt ID: MOD0251-P0-INSPECT-M1
- Scope: MOD-0251 Core HR / Employee Master readiness inspection
- Build lane: R1-A — Internal HCM Master Foundation
- Target branch: <current branch>
- Base commit: <current HEAD>
- Related spec/report: MOD-0251-Core-HR-Employee-Master-Spec-v1.5-BLUEPRINT-ALIGNED.md

Task:
Inspect readiness of MOD-0251 without changing code.

Objective:
Determine whether the MOD-0251 employee master golden flow can be implemented safely and identify blockers before development.

Read-only constraints:
- Do not change source code.
- Do not mutate database data.
- Do not run destructive fixtures.
- Do not stage, commit, reset, clean, or stash.
- Do not create placeholder files.

Inspection scope:
- Existing HCM backend service/project location.
- Existing API route conventions.
- Existing gateway route configuration.
- Existing EF/domain entity/migration conventions.
- Existing RBAC permission seed pattern.
- Existing audit emit/query contract.
- Existing workflow MOD-0023 integration hooks.
- Existing evidence/document MOD-0028/MOD-0031 integration hooks.
- Existing retention MOD-0030 hook.
- Existing MOD-0288 person reference contract.
- Existing MOD-0314 sensitive policy/masking hook.
- Existing frontend HCM route/page conventions.
- Existing UI component/style patterns.
- Existing test project conventions.
- Existing reports/MOD-0251 folder conventions if any.

Ownership and boundaries:
- Expected owner module: MOD-0251.
- Objects/capabilities inspected: Employee, Employee Draft Session, Employment Record, Status History, Employee Document Link, Data Quality Case.
- Consumed modules: MOD-0018, MOD-0021, MOD-0023, MOD-0028, MOD-0030, MOD-0031, MOD-0048, MOD-0288, MOD-0314.
- Potential ownership conflicts: person, organization, position, workflow, evidence, audit, sensitive-access policy.

Inspection checklist:
- Existing endpoints.
- Existing DTOs/contracts.
- Existing persistence/migrations.
- Existing UI routes/components.
- Existing tests.
- Existing audit/evidence/RBAC hooks.
- Existing tenant isolation hooks.
- Existing reference-data dependencies.
- Existing runtime blockers.
- Shell UI risk.
- Missing contracts.
- Boundary conflicts.

Validation:
- Build/check only if non-mutating.
- Runtime smoke only if environment is already running and no writes are required.
- Confirm no source changes.
- Confirm no DB mutation.
- Confirm no git operation changed worktree.

Output contract:
- Final verdict: PASS | CONDITIONAL PASS | PARTIAL | BLOCKED | FAIL.
- Evidence table.
- Existing capabilities.
- Missing capabilities.
- Contract blockers.
- Boundary/ownership issues.
- Recommended next implementation prompt.
- Remaining open decisions.
```

---

## 30. Developer Pack Summary

### Change Package MOD0251-P1

| Item | Scope |
|---|---|
| Data model | Employee, employee draft session, employment record, status history, document link, data-quality case |
| APIs | Draft create/update/submit, approval decision, create/get/search/update/status/link document/export |
| Events | employee.created, employee.status.changed, employee.terminated, employee.profile.updated |
| Pages | Employee Registry, Create Employee Wizard, Employee Detail, Status History, Data Quality Queue |
| Governance | RBAC, masking, audit, retention, evidence link, field sensitivity |
| Runtime slice | Create draft → submit → MOD-0023 approval decision → Active → reload → audit → employee.created event/outbox; evidence/status/export/DQ sequenced later |

### Definition of Done

`MOD-0251` is ready when:

```text
The platform can create and govern internal employee and employment records,
maintain employee lifecycle status,
link employee records to existing person references,
enforce HR-sensitive access controls,
write complete audit history,
attach evidence and retention policies,
emit employee lifecycle events,
and provide stable APIs to MOD-0298, MOD-0299, MOD-0305, MOD-0314, and later Talent Ecosystem modules.
```

---

## 31. Open Items

| Item | Status | Owner Decision |
|---|---|---|
| Country-specific mandatory employee fields | Deferred post-MVP | R1 uses geography-neutral minimal core |
| Government identifier storage rule | Closed for R1 | Token/hash only; raw storage prohibited |
| Employee number generation policy | Closed for R1 | Auto-generated tenant-scoped format with HR Admin pre-activation override |
| Approval threshold for sensitive updates | Partially closed | Sensitive status changes require approval; detailed field-level threshold delegated to MOD-0314 |
| Default retention period by employee record type | Closed for R1 default | Tenant policy can override |
| Whether employees can view own profile in R1 | Deferred | No full self-service until MOD-0319/self-service capability |
| Duplicate merge workflow | Deferred unless separately specified | Data-quality warning/case in R1, merge not implemented |
| Exact backend/frontend/gateway paths | Confirmed for the P2 draft/reference-validation slice | Future registry/detail/lifecycle paths remain blocked if later inspection cannot locate correct topology |
| Tokenization service owner | To be confirmed by inspection | BLOCKED if no platform tokenization/security contract exists and gov ID capture is required |

---

## 32. Final Development Readiness Verdict

**Verdict:** PASS for the completed P2 draft/reference-validation slice; full Employee Master lifecycle remains gated by later approved scope and dependency closure.

**Condition:** P2 is limited to create draft, save/update with ETag, reload, person/organization-unit/position/legal-entity reference validation, and non-submit review. Later lifecycle implementation may begin only after a new approved scope confirms the owning service, route conventions, migration location, gateway path, frontend route convention, RBAC/audit/evidence/workflow hooks, MOD-0288 reference contract, MOD-0314 masking hook, tokenization/security contract where required, DTO/audit payload closure path, export platform pattern where relevant, and no ownership conflicts.

**Do not expand beyond P2 from this specification alone. Broad implementation, submit, approval/rejection, activation, MOD-0023 workflow behavior, `employee.created`, evidence upload/link, export/status/Data Quality Queue, and government identifier capture/tokenization remain prohibited until a later approved prompt authorizes that exact scope.**
