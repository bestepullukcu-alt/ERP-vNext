---
id: MOD-0251
name: Core HR / Employee Master
domain: human-capital-management
service: Diten.HcmService
shell: tenant
golden_reference: compact
entity_base: EntityBase
status: approved
approval_status: conditionally-approved-for-draft-reference-validation-slice
owner: HCM / HR data owner TBD
branch: feature/hr/platform-backbone-hr-source-readiness-new
started: 2026-06-17
target: 2026-06-17
form_field_count: 18
---

# MOD-0251 - Core HR / Employee Master

## Blueprint Alignment

| Blueprint Field | Value |
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
| Minimum Integration Contract Bundle | HCM-CORE-BUNDLE |
| SoR Applicability | Y |
| SoR Notes | Internal HCM master with native HCM ownership. |

Identity gate: `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0251 --name "Core HR / Employee Master"` returns `OK  MOD-0251: proven against Blueprint/registry.` Registry collision check is clear because no active registry row maps `MOD-0251` to a different capability.

## Module Summary

`MOD-0251 Core HR / Employee Master` is the internal authoritative employee and employment master for the HCM Foundation MVP. It owns employee legal profile, employment record, employment status, and the employee-side job assignment / employment assignment snapshot needed by downstream HCM modules.

This pack is conditionally approved only for the reduced draft/reference-validation slice. That P2 slice is implemented and browser-validated for create draft, save/update with ETag, reload, person/organization-unit/position/legal-entity reference validation, and non-submit review. The HCM service scaffold exists under `services/Diten.HcmService`, but this pack does not authorize submit/approval/activation, full MOD-0251 lifecycle implementation, employee registry/detail lifecycle surfaces, evidence upload/link, export/status/Data Quality Queue, government identifier capture/tokenization, or employee Active-state APIs.

## Authoritative Baseline After Containment

Prompt ID: `MOD0251-SPEC-PACK-AUTHORITY-ALIGN-M1`.

Authority decision:

- Current active spec: `docs/specs/MOD-0251-Core-HR-Employee-Master-Spec-v1.5-BLUEPRINT-ALIGNED.md`.
- Active internal spec version: `v1.10 P2 runtime smoke closure status`, including the v1.9 registry read-only scope contract for later `MOD0251-P4-REGISTRY-READ-M1`.
- Standalone v1.4 spec status: not present as a separate governing file in the current repository; v1.4 is superseded lineage recorded in the active spec changelog.
- Current implementation evidence: `docs/qa/acceptance-reports/MOD-0251-scope-containment-rework-2026-06-21.md`.

Current approved runtime scope remains the contained P2 draft/reference-validation baseline:

- Draft create.
- Draft save/update with ETag.
- Draft reload.
- Person, organization-unit, position, and legal-entity reference validation.
- Non-submit review/readiness state.

Registry/detail scope decision:

- Employee Registry is not a P2 support surface.
- Employee Detail is not a P2 support surface.
- P2 runtime smoke is limited to the Create Employee Draft surface and draft API/proxy flow.
- Active employee records must not be used as P2 evidence.
- Draft-only records must not be surfaced through Employee Registry in P2.
- Registry read-only behavior moves to a later approved registry sequence.
- Detail read-only behavior moves to a later approved detail sequence.

Containment state:

- Submit and workflow-decision routes are blocked by design with controlled failure responses until later lifecycle authorization.
- MOD-0023 workflow start is not runtime-enabled for MOD-0251.
- Active employee materialization is not runtime-enabled.
- `employee.activated` is not emitted, and `employee.created.v1` is not implemented.
- Detail status/evidence/audit placeholder UI and registry export affordance are removed from the contained baseline.
- Gateway remains unchanged; backend-level route containment is the current control.
- Evidence/status/export/data-quality and government identifier capture remain deferred.

## Ownership and Boundaries

The following ownership lists describe MOD-0251 product ownership and future SoR responsibility. They do not broaden the current approved runtime scope beyond the contained P2 draft/reference-validation baseline above.

### In Scope

- Employee master creation, activation, search, detail, status history, and governed lifecycle contracts.
- Employment Record ownership for the employee master.
- Job Assignment / Employment Assignment Snapshot ownership as part of the employee master.
- Employee draft session for the Create Employee wizard.
- Employee lifecycle events and safe audit payload policy.
- HCM-CORE-BUNDLE interface bundle metadata.

### Out of Scope

- Assignment workflow/orchestration, owned by `MOD-0299`.
- Organization, person, and position reference directories, owned by `MOD-0288`.
- Approval engine ownership, owned by `MOD-0023`.
- Binary document storage, owned by `MOD-0028`.
- Evidence link registry, owned by `MOD-0031`.
- Retention/legal hold policy, owned by `MOD-0030`.
- HR sensitive access and masking policy engine, owned by `MOD-0314`.
- Payroll, time, leave, compensation, benefits, performance, offboarding execution, employee relations, and Talent Ecosystem modules.

## Owned Objects

- Domain entities:
  - Employee.
  - Employment Record.
  - Job Assignment / Employment Assignment Snapshot.
  - Employee Status History.
  - Employee Identifier token reference.
  - Employee Draft Session.
  - Employee Document Link metadata.
  - Employee Data Quality Case.
- Commands:
  - Create employee draft.
  - Update employee draft.
  - Submit employee draft.
  - Apply workflow approval decision.
  - Update employee profile.
  - Update employment record.
  - Request status change.
  - Link employee evidence.
  - Export employee list.
- Queries:
  - Employee registry list.
  - Employee detail.
  - Employee status history.
  - Employee data-quality case list.
- API/interface stubs:
  - Employee Draft APIs.
  - Employee Query APIs.
  - Employee Status Command.
  - Employee Evidence Link API.
  - HCM-CORE-BUNDLE.
- Frontend planning surfaces:
  - Employee Registry.
  - Create Employee Wizard.
  - Employee Master Detail.
  - Employee Status History.
  - Employee Data Quality Queue.

## Entity Fields

Field-level details are inherited from `docs/specs/MOD-0251-Core-HR-Employee-Master-Spec-v1.5-BLUEPRINT-ALIGNED.md` and must be contract-closed in `MOD0251-P1-CONTRACTS-M1` before implementation.

| Entity | Required Key Fields / Notes |
|---|---|
| Employee | `EmployeeId`, server-side `TenantId`, `PersonId`, legal names, employee number, worker type, employment type, hire date, status, sensitivity level, concurrency `version` / `etag`. |
| Employment Record | `EmploymentRecordId`, `EmployeeId`, server-side `TenantId`, company/legal-entity reference, start/end dates, contract type, probation fields, employment status, approval status, concurrency `version` / `etag`. |
| Job Assignment / Employment Assignment Snapshot | Employee-side assignment snapshot only; orchestration stays with MOD-0299. |
| Employee Draft Session | Wizard draft state, step status JSON, redacted/encrypted draft payload, expiration, submit timestamp, concurrency. |
| Employee Status History | Append-only status transition log with workflow/audit/evidence references. |
| Employee Document Link | Link metadata only; binary content stays with MOD-0028 and evidence links with MOD-0031. |
| Employee Data Quality Case | Duplicate/missing-data queue item, assignment, resolution status, related employee/draft references. |

## Repo Scope

Governance/documentation scope for this reconciliation prompt:

- `execution/domains/human-capital-management/README.md`.
- `execution/domains/human-capital-management/domain-config.md`.
- `execution/domains/human-capital-management/module-packs/MOD-0251-core-hr-employee-master.md`.

Completed P2 runtime scope is limited to the draft/reference-validation slice. Additional MOD-0251 business implementation remains blocked until a later approved prompt authorizes it and confirms:

- HCM backend service/project path. Scaffold exists at `services/Diten.HcmService`, but employee business implementation remains unapproved.
- HCM persistence/migration location.
- HCM frontend route/view convention.
- Gateway route ownership and downstream service port.
- Test project paths.

## Protected Paths

- `.antigravity/**`.
- `services/**` outside the approved `services/Diten.HcmService` scaffold and explicitly approved MOD-0251 draft/reference-validation implementation prompts.
- `frontend/**` outside the approved MOD-0251 P2 draft wizard/proxy work and later separately approved frontend prompts.
- `gateway/**` route files until an approved integration-agent task exists.
- `tests/**` outside the approved MOD-0251 P2 focused test scope and later separately approved test prompts.
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`.
- `frontend/Diten.Web/Controllers/Archive/**`.
- `frontend/Diten.Web/Views/Archive/**`.
- Other domain module packs and domain configs unless separately approved.
- Workbook and registry files except read-only inspection.

## Dependencies

### Blueprint Dependency Gate

| Dependency | Purpose | Fail-Closed Rule |
|---|---|---|
| `MOD-0018 RBAC / Authorization` | HR role and permission control | Missing permission seed/enforcement blocks all protected endpoints. |
| `MOD-0021 Audit Trail` | Access/change/export/approval audit | Missing audit emit/query contract blocks critical writes and sensitive reads. |
| `MOD-0023 Workflow` | Activation, sensitive update, suspension, termination, rehire approval workflow | Missing workflow contract blocks activation approval and approval-decision implementation. |
| `MOD-0030 Records Management / Retention / Legal Hold` | Retention and legal hold policy | Missing retention/legal hold contract blocks evidence integration and destructive lifecycle flows. |
| `MOD-0048 Reference Data / Lookups` | Employment statuses, worker types, contract types, reason categories | Missing controlled values blocks validation and create/submit. |
| `MOD-0057 Taxonomy / Semantic Tagging` | Classification, role/job categories, tags | Missing taxonomy blocks tag visibility only; optional for first approved draft/reference-validation slice unless required by contract. |

### Additional Implementation Dependencies

| Dependency | Purpose | Fail-Closed Rule |
|---|---|---|
| `MOD-0028 Documentation / Evidence Management` | Document/evidence storage reference | Missing contract blocks evidence attachment/integration. |
| `MOD-0031 Evidence Linking Service` | Evidence link registry | Missing contract blocks evidence completion. |
| `MOD-0288 Organization, Person & Position Directory` | Person, organization, position references | Missing person reference blocks employee creation; missing company/legal-entity SoR blocks employment record validation. |
| `MOD-0314 HR Governance & Sensitive Access Controls` | HR-sensitive masking and visibility policy | Missing masking hook blocks sensitive response shaping. |
| Tokenization/security service | Government identifier token/hash capture | Missing tokenization contract blocks government identifier capture. |

## Runtime Constraints

- First approved runtime slice is draft/reference-validation only: Create Employee Draft -> Save Draft Steps -> Validate Person/Organization/Position/Legal-Entity References -> Review Draft -> Reload Persisted Draft.
- Submit for approval, MOD-0023 approval decision processing, Active transition, `employee.created` event/outbox behavior, and approval audit events are excluded from the first approved slice.
- Evidence integration, termination/status changes, Data Quality Queue, and export are later sequences.
- No shell UI is allowed: UI pages must be backed by real API, persistence, RBAC, audit, validation, and failure handling.
- Tenant isolation is mandatory; `TenantId` is server-side and not accepted from client payload.
- Employee data is tenant-owned; `entity_base: EntityBase` is provisional and must be confirmed against the HCM service base class during P0/P1.
- Optimistic concurrency is mandatory for editable records.
- Idempotency is mandatory for create/submit/approval/status/document/export commands.

## Layout & Shell Contract

- `shell: tenant`.
- Frontend route planning from the spec: `/HCM/Employees`, `/HCM/Employees/Create`, `/HCM/Employees/{employeeId}`.
- Razor layout for future frontend pages: `Layout = "_LayoutTenantShell";`.
- Frontend expansion beyond the approved P2 draft wizard/proxy slice is blocked until a later approved scope confirms the route, service/API, and UI contracts.

## Backend File Convention

Backend service owner is `Diten.HcmService`. The scaffold exists at `services/Diten.HcmService`, and P2 draft/reference-validation runtime is implemented there. Full Employee Master lifecycle implementation remains unapproved.

Once confirmed, use repo-standard CQRS/MediatR conventions:

```text
services/{ConfirmedHcmService}/src/{ConfirmedHcmService}.Application/Features/CoreHrEmployeeMaster/
├── Commands/
├── Queries/
├── Handlers/CommandHandlers/
├── Handlers/QueryHandlers/
├── Validators/
└── CoreHrEmployeeMasterModels.cs
```

Naming must follow Golden Reference conventions: commands end in `Command`, queries end in `Query`, handlers end only in `Handler`, validators end only in `Validator`, and DTO/view records live in `{Module}Models.cs`.

## Frontend File Contract

Golden Reference decision: `compact`.

Justification: Create/edit user-entered fields exceed eight. Counted planning fields include person reference, legal first/middle/last/preferred names, date of birth, nationality, work email, personal email, phone, worker type, employment type, hire date, company/legal-entity reference, contract type, probation fields, sensitivity level, and related employment metadata. Government identifier capture is excluded until tokenization is confirmed but the form remains above the compact threshold.

Applicability:

- Employee Registry DataTable uses DataTable v2 and the Compact reference for table/list structure.
- Create Employee uses a wizard pattern, not a direct GoldenReferenceCompact create form clone.
- Employee Master Detail uses a single detail / section page pattern, not a simple CRUD details clone.
- Compact Golden Reference still governs backend CQRS naming and DataTable list page structure where applicable.

Future registry/detail frontend compact file planning, after a later approved scope:

- `frontend/Diten.Web/Views/HCM/Employees/Index.cshtml`.
- `frontend/Diten.Web/Views/HCM/Employees/Create.cshtml` for wizard host.
- `frontend/Diten.Web/Views/HCM/Employees/Details.cshtml`.
- `frontend/Diten.Web/Views/HCM/Employees/_Filter.cshtml`.
- `frontend/Diten.Web/Views/HCM/Employees/_DataTable.cshtml`.
- `frontend/Diten.Web/Views/HCM/Employees/_IndexL10n.cshtml`.
- `frontend/Diten.Web/Views/HCM/Employees/EmployeeMasterIndex.cs`.
- `frontend/Diten.Web/wwwroot/assets/js/HCM/Employees/index.js`.
- `frontend/Diten.Web/wwwroot/assets/js/HCM/Employees/index.l10n.js`.

No new frontend behavior may be created by this governance reconciliation prompt.

## Validation Rules

| Field / Rule | Requirement |
|---|---|
| `PersonId` | Required; must resolve to same-tenant MOD-0288 person before employee creation. |
| Legal first name | Required before activation; max 100; PII redaction in logs/audit. |
| Legal last name | Required before activation; max 100; PII redaction in logs/audit. |
| Employee number | Generated by MOD-0251 policy; unique per tenant. |
| Work email | Optional; if present, valid email and unique per tenant. |
| Worker type | Required controlled value from MOD-0048. |
| Employment type | Required controlled value from MOD-0048. |
| Hire date | Required before activation. |
| Company/legal-entity reference | Required before employment record validation; SoR must be confirmed by P0 inspection. |
| Contract type | Required controlled value from MOD-0048. |
| Government identifier | Raw value prohibited; token/hash only after tokenization contract exists. |
| Sensitivity level | Required; drives MOD-0314 masking policy. |
| TenantId | Server-side only; reject/ignore client-provided values. |
| Version/ETag | Required for editable updates; stale writes return controlled conflict. |

## Failure Path to Verify

| Failure Path | Expected Behavior |
|---|---|
| Missing dependency | Relevant runtime slice blocks with controlled error; no inferred contract. |
| Unauthorized access | 403 or masked response; audit access-denial event emitted. |
| Tenant mismatch | 404/fail-closed; no cross-tenant data disclosure. |
| Duplicate employee | Duplicate warning or Data Quality Case; no silent duplicate activation. |
| ETag/concurrency conflict | 409 with reload/review-current-version path. |
| Idempotency replay | No duplicate employee, workflow, audit, status history, or event side effect. |
| Workflow unavailable | Activation submit/approval cannot complete; employee state remains unchanged. |
| Audit unavailable | Critical writes blocked; sensitive read/export audited or denied. |
| Tokenization unavailable | Government identifier capture disabled/blocked. |

## Authorization Convention

Tenant HCM permission namespace is provisional and must be confirmed during P1 contract closure. Planned permission codes:

- `mod0251.employee.search`.
- `mod0251.employee.view`.
- `mod0251.employee.view_sensitive`.
- `mod0251.employee.create_draft`.
- `mod0251.employee.submit`.
- `mod0251.employee.approve`.
- `mod0251.employee.edit_legal`.
- `mod0251.employee.edit_employment`.
- `mod0251.employee.change_status`.
- `mod0251.employee.attach_evidence`.
- `mod0251.employee.export`.
- `mod0251.employee.view_status_history`.
- `mod0251.data_quality.view`.
- `mod0251.data_quality.resolve`.

Actor types: HR Admin, HR Manager, HR Contributor, HR Data Steward, Audit Admin, and service actors from MOD-0023/MOD-0021/MOD-0031 as applicable.

## MOD-0251 RBAC Contract

Prompt ID: `MOD0251-P1-RBAC-AUDIT-CONTRACT-M1`.

This section closes MOD-0251 RBAC planning only. It does not create permission seeds, roles, policies, controllers, UI guards, tests, gateway routes, or runtime code. Existing repo evidence shows executable permission enforcement patterns through `HasPermissionAttribute`, Auth role/permission controllers, and Platform controller permission usage; MOD-0251-specific seed and endpoint implementation remain blocked until this pack is promoted and an approved implementation prompt authorizes employee runtime behavior.

### Permission Key Catalog

| Permission Key | Purpose |
|---|---|
| `mod0251.employee.search` | Search and list tenant-scoped employee registry rows. |
| `mod0251.employee.view` | View non-sensitive employee detail. |
| `mod0251.employee.view_sensitive` | View sensitive employee fields when MOD-0314/tokenization policy also permits. |
| `mod0251.employee.create_draft` | Start and save employee draft sessions before submit. |
| `mod0251.employee.submit` | Submit a complete employee draft to the approval dependency. |
| `mod0251.employee.approve` | Receive/process an approval or rejection decision through the approved MOD-0023 adapter contract. |
| `mod0251.employee.edit_legal` | Edit legal profile fields after draft or approved controlled edit flow. |
| `mod0251.employee.edit_employment` | Edit employment record fields after draft or approved controlled edit flow. |
| `mod0251.employee.change_status` | Request or execute status transitions such as suspend, terminate, rehire, or leave. |
| `mod0251.employee.attach_evidence` | Link evidence to an employee/draft through MOD-0028/MOD-0031 contracts. |
| `mod0251.employee.export` | Request and download governed employee exports. |
| `mod0251.employee.view_status_history` | View immutable employee status history. |
| `mod0251.data_quality.view` | View duplicate/data-quality queue items for MOD-0251. |
| `mod0251.data_quality.resolve` | Resolve duplicate/data-quality cases for MOD-0251. |

### Role-Permission Matrix

| Role | Permissions |
|---|---|
| HR Admin | All MOD-0251 permissions listed in the catalog. |
| HR Manager | `mod0251.employee.search`, `mod0251.employee.view`, `mod0251.employee.view_sensitive`, `mod0251.employee.create_draft`, `mod0251.employee.submit`, `mod0251.employee.edit_employment`, `mod0251.employee.change_status`, `mod0251.employee.attach_evidence`, `mod0251.employee.export`, `mod0251.employee.view_status_history`, `mod0251.data_quality.view`. |
| HR Contributor | `mod0251.employee.search`, `mod0251.employee.view`, `mod0251.employee.create_draft`, `mod0251.employee.submit`, `mod0251.employee.attach_evidence`, `mod0251.employee.view_status_history`. |
| HR Data Steward | `mod0251.employee.search`, `mod0251.employee.view`, `mod0251.employee.edit_legal`, `mod0251.employee.edit_employment`, `mod0251.employee.view_status_history`, `mod0251.data_quality.view`, `mod0251.data_quality.resolve`. |
| Department Manager | `mod0251.employee.search`, `mod0251.employee.view`, `mod0251.employee.view_status_history` limited to permitted reporting scope. |
| Employee | `mod0251.employee.view` limited to self-service scope only; no sensitive, export, approval, evidence, status-change, or data-quality permissions. |
| Audit Admin | `mod0251.employee.search`, `mod0251.employee.view`, `mod0251.employee.export`, `mod0251.employee.view_status_history`, `mod0251.data_quality.view`; audit event access remains governed by MOD-0021/platform audit permissions. |
| Platform Admin | No implicit tenant HCM business access from platform role alone. Break-glass access requires explicit MOD-0251 tenant-scoped permission assignment and audit policy. |

### Protected Action / Endpoint Map

| Protected Action | Future API Surface | Required Permission | Fail-Closed Dependency |
|---|---|---|---|
| Registry search/list | `GET /api/v1/hcm/employees` | `mod0251.employee.search` | MOD-0314 masking unavailable returns non-sensitive rows only or denies. |
| Detail view | `GET /api/v1/hcm/employees/{employeeId}` | `mod0251.employee.view` | Tenant mismatch returns not found or denied. |
| Sensitive view | `GET /api/v1/hcm/employees/{employeeId}/sensitive` or sensitive field expansion | `mod0251.employee.view_sensitive` | MOD-0314 and tokenization contracts must also permit; otherwise denied. |
| Draft create/update | `POST /api/v1/hcm/employees/drafts`, `PATCH /api/v1/hcm/employees/drafts/{draftSessionId}` | `mod0251.employee.create_draft` | MOD-0288/MOD-0048 validation blockers prevent review readiness, not draft session creation unless required fields are validated at that step. |
| Draft reference validation | `POST /api/v1/hcm/employees/drafts/{draftSessionId}/validate-references` | `mod0251.employee.create_draft` | Missing person, organization-unit, position, or legal-entity provider/Gateway path blocks review readiness. |
| Draft review | `POST /api/v1/hcm/employees/drafts/{draftSessionId}/review` | `mod0251.employee.create_draft` | Non-submit review only; MOD-0023 unavailable keeps submit/approval/Active out of scope. |
| Submit | `POST /api/v1/hcm/employees/drafts/{draftId}/submit` | `mod0251.employee.submit` | MOD-0023 unavailable blocks submit/approval. |
| Approval adapter | `POST /api/v1/hcm/employees/{employeeId}/approval-decisions` or event consumer | `mod0251.employee.approve` for user/API command; service actor contract for MOD-0023 event | MOD-0023 unavailable blocks activation decisions. |
| Legal profile edit | `PATCH /api/v1/hcm/employees/{employeeId}/legal-profile` | `mod0251.employee.edit_legal` | Sensitive fields denied when masking/tokenization unavailable. |
| Employment record edit | `PATCH /api/v1/hcm/employees/{employeeId}/employment-record` | `mod0251.employee.edit_employment` | MOD-0288/MOD-0048 validation required. |
| Status change | `POST /api/v1/hcm/employees/{employeeId}/status-changes` | `mod0251.employee.change_status` | Workflow/evidence/retention blockers keep R1 status changes disabled. |
| Evidence link | `POST /api/v1/hcm/employees/{employeeId}/evidence-links` | `mod0251.employee.attach_evidence` | MOD-0028/MOD-0031 unavailable disables evidence link. |
| Export | `GET /api/v1/hcm/employees/export` | `mod0251.employee.export` | MOD-0314/audit/export policy unavailable disables export. |
| Status history | `GET /api/v1/hcm/employees/{employeeId}/status-history` | `mod0251.employee.view_status_history` | Tenant mismatch returns not found or denied. |
| Data quality view | `GET /api/v1/hcm/employee-data-quality` | `mod0251.data_quality.view` | Queue workflow remains deferred if P10 not approved. |
| Data quality resolve | `POST /api/v1/hcm/employee-data-quality/{caseId}/resolve` | `mod0251.data_quality.resolve` | Full resolution workflow remains deferred unless approved. |

### RBAC Fail-Closed Rules

- Missing MOD-0251 permission seed blocks endpoint enablement; no endpoint may silently use a broader or legacy permission.
- Unknown role, missing role mapping, or absent permission claim grants no access.
- Tenant mismatch returns not found or denied and must not disclose cross-tenant identifiers, counts, or existence.
- UI hiding is insufficient; every protected action requires server-side permission enforcement.
- Direct browser/front-end service-port bypass is prohibited; tenant HCM calls must enter through Gateway `5000` / approved same-origin MVC proxy patterns.

## Gateway / API Routing Decision

- Gateway route implementation is deferred.
- Frontend must call Gateway `5000` only.
- Browser JS must not call service ports directly.
- Gateway route changes must be assigned to `integration-agent` after service/API route ownership is confirmed.
- Expected API surface from the spec remains `/api/v1/hcm/employees...`, but the exact route must be confirmed by P1 contracts and gateway standards before implementation.

## API / Interface Registry Stubs

| Contract | Owner | Consumer | Type | Notes |
|---|---|---|---|---|
| HCM-CORE-BUNDLE | MOD-0251 | HCM Foundation consumers | Contract bundle | Minimum integration contract bundle from Blueprint v7. |
| Employee Draft APIs | MOD-0251 | MOD-0251 UI | API | Create/update/submit draft; idempotent submit. |
| Employee Query APIs | MOD-0251 | MOD-0298, MOD-0299, MOD-0305, MOD-0314 | API | Search/detail with masking. |
| Employee Status Command | MOD-0251 | HR UI, MOD-0305 | API | Status transitions after workflow policy. |
| Workflow Start / Decision | MOD-0023 | MOD-0251 | API/Event | MOD-0023 owns approval workflow. |
| Audit Emit | MOD-0021 | MOD-0251 | API/Event | Critical writes fail closed on audit failure. |
| Evidence Document | MOD-0028 | MOD-0251 | API | Evidence attachment dependency. |
| Evidence Link | MOD-0031 | MOD-0251 | API | Evidence link dependency. |
| Person Reference Lookup | MOD-0288 | MOD-0251 | API | Employee creation blocker. |
| Sensitive Access Policy | MOD-0314 | MOD-0251 | API/Service | Masking and visibility dependency. |

## Audit Event List and Safe Payload Policy

Planned events:

- `employee_draft.created`.
- `employee_draft.updated`.
- `employee.submitted_for_approval`.
- `employee.approved`.
- `employee.rejected`.
- `employee.profile.updated`.
- `employee.employment_record.updated`.
- `employee.status.changed`.
- `employee.evidence_linked`.
- `employee.export.requested`.
- `employee.export.completed`.
- `employee.access_denied`.
- `employee.conflict`.
- `employee.idempotency_replayed`.

Safe payload policy: audit payloads may include tenant ID, actor ID, employee ID, draft ID, workflow reference, correlation ID, event type, status, and version. They must not include full PII payloads, raw government identifiers, secrets, document content, unredacted comments, or exported row data.

## MOD-0251 Audit Contract

Prompt ID: `MOD0251-P1-RBAC-AUDIT-CONTRACT-M1`.

This section closes MOD-0251 audit payload planning only. It does not wire `IAuditService`, add audit behavior registrations, create event emitters, add tests, or create audit queries. Existing repo evidence shows executable audit primitives through `IAuditService`, `AuditBehavior`, `AuditAppendResult`, `AuditService`, redaction, and `PlatformAuditController`; MOD-0251 event wiring remains blocked until `Diten.HcmService` implementation is explicitly approved.

### Audit Event Catalog

| Event | Trigger | Criticality |
|---|---|---|
| `employee_draft.created` | Draft session created. | Critical write. |
| `employee_draft.updated` | Draft legal/employment/evidence metadata saved. | Critical write. |
| `employee.submitted_for_approval` | Complete draft submitted to MOD-0023. | Critical write. |
| `employee.approved` | Approved decision activates employee. | Critical write. |
| `employee.rejected` | Rejected decision returns draft/employee to revisable state. | Critical write. |
| `employee.profile.updated` | Legal/profile section changed. | Critical write. |
| `employee.employment_record.updated` | Employment record section changed. | Critical write. |
| `employee.status.changed` | Employee lifecycle status changed. | Critical write. |
| `employee.evidence_linked` | Evidence link attached to employee/draft. | Critical write when evidence is required. |
| `employee.export.requested` | Export requested before generation. | Sensitive access/export. |
| `employee.export.completed` | Export generated or completed with row count metadata. | Sensitive access/export. |
| `employee.access_denied` | RBAC, tenant, sensitive access, or dependency policy denies a protected action. | Security audit. |

### Safe Payload Matrix

| Payload Field Group | Allowed | Prohibited |
|---|---|---|
| Common envelope | `tenant_id`, `actor_id`, `correlation_id`, event name, source module, request type, outcome. | Actor email/display name unless masked by the audit infrastructure. |
| Target identifiers | `employee_id`, `draft_id`, `employment_record_id`, `status_history_id`, `evidence_link_id`, `data_quality_case_id`, workflow reference ID. | Raw document content, exported row data, or third-party payload bodies. |
| Change metadata | Entity version/ETag, previous and next status codes, changed field names, section name, idempotency key hash/reference. | Before/after values for PII or confidential fields, full object snapshots, raw validation payloads. |
| Sensitive identity | Token reference presence flag and token provider reference when safe. | Government identifier token value, raw government identifier, secrets, emails, phone numbers, date of birth, address, bank/tax values. |
| Denial/export metadata | Permission key, action name, dependency name, denial reason code, export format, bounded row count, filter hash/reference. | Free-text reason notes unless redacted, search text containing PII, unbounded filter details, generated file contents. |

### Audit Fail-Closed Rules

- Critical writes block or roll back if audit emit fails or returns a critical rejected result.
- Sensitive view paths must audit when sensitive fields are returned; if audit cannot be emitted, sensitive fields are denied.
- `employee.access_denied` payloads must not include PII, raw identifiers, request bodies, or sensitive search parameters.
- Export requires both `employee.export.requested` and `employee.export.completed`; failed export completion records safe status/error code only.
- Audit event payloads must be deterministic, redacted, tenant-scoped, correlation-aware, and safe for replay/idempotency checks.

## P0 Inspection Findings

P0 read-only inspection verdict: `BLOCKED` for approval/implementation.

Inspection evidence recorded that no implementation occurred and no files were changed during the inspection. The pack was later conditionally promoted for the draft/reference-validation slice only; runtime work outside that approved slice remains blocked until topology, service ownership, gateway, frontend, RBAC, audit, workflow, evidence, masking, tokenization, and dependency contracts are explicitly closed.

Key inspection findings:

- No HCM/HR backend service exists under `services/`.
- No HCM API, persistence, indexes, gateway routes, frontend routes, HCM RBAC permissions, HCM audit events, tokenization contract, MOD-0314 masking contract, MOD-0023 workflow contract, or MOD-0031 evidence contract is executable for MOD-0251.
- Platform audit capability exists, but MOD-0251 audit event wiring does not.
- Current MOD-0023 workflow and MOD-0031 evidence dependencies are mock/disabled or not implemented for executable MOD-0251 use.
- Gateway route work remains deferred to `integration-agent`.
- Frontend must use tenant shell and Gateway `5000` / same-origin MVC proxy patterns; browser code must never call service ports directly.

## Governance Closure Findings

Governance closure inspections GC-01, GC-02, and GC-03 are now recorded in this draft pack. These findings document blockers only; they do not approve employee business implementation, gateway routing, migrations, tests, workbook edits, registry edits, or status promotion.

### GC-01 MOD-0299 Reconciliation Finding

Blueprint master 5 and master 7 both map `Blueprint_Data` row 300 to:

| Field | Blueprint Value |
|---|---|
| Module ID | `MOD-0299` |
| Module Name | `Position & Organization Assignment` |
| Suite / Platform | `Human Capital Management Foundation` |
| Capability Group | `Foundation / Platform` |
| Wave | `W-2` |
| Placement | `Domain App (HCM Foundation)` |
| Minimum Integration Contracts | `HCM-CORE-BUNDLE (assignment context schema, position binding contract, manager hierarchy view contract, AuditEvent v1, Correlation-ID, OTel)` |

The current module ID registry maps `MOD-0299` to deprecated SaaS Billing & Invoicing and replacement candidate `CAND-CAP-0005`. That registry row is marked deprecated / non-executable and is not compatible with the Blueprint HCM meaning of `MOD-0299`.

Decision:

- MOD-0251 may reference `MOD-0299` only as a deferred/future HCM assignment dependency.
- MOD-0251 must not consume or implement an executable `MOD-0299` assignment workflow/orchestration contract until Enterprise Architect approval reconciles the registry conflict and the approved registry update is applied.
- No placeholder, alias, or runtime literal may be invented to bypass the conflict.

### GC-02 HCM Service Topology Finding

Recommended future HCM service boundary:

```text
services/Diten.HcmService/
├── Diten.HcmService.sln
├── src/
│   ├── Diten.HcmService.Api/
│   ├── Diten.HcmService.Application/
│   ├── Diten.HcmService.Domain/
│   ├── Diten.HcmService.Infrastructure/
│   └── Diten.HcmService.Persistence/
└── tests/
    └── Diten.HcmService.Application.Tests/
```

Recommended future port: `5060`.

This topology is now the approved planning target for MOD-0251 and the dedicated HCM service scaffold exists at `services/Diten.HcmService`. The scaffold must not be treated as approval to implement employee runtime behavior. MOD-0251 must not be placed in `Diten.Platform`, `Diten.AuthService`, `Diten.MdmService`, `Diten.DevEnablementService`, or `Diten.EnterpriseStrategyService`, because those services do not own native HCM employee master SoR data.

## P1 HCM Service Topology Contract

Prompt ID: `MOD0251-P1-HCM-TOPOLOGY-CONTRACT-M1`.

This P1 contract closes the service-boundary decision for planning purposes only. It does not create the service, authorize backend implementation, add gateway routes, create migrations, create tests, or promote this pack out of `draft`.

### Contract Decision

| Decision Point | Contract Value |
|---|---|
| Future service owner | `Diten.HcmService` |
| Future service path | `services/Diten.HcmService` |
| Future solution file | `services/Diten.HcmService/Diten.HcmService.sln` |
| Future API project | `services/Diten.HcmService/src/Diten.HcmService.Api` |
| Future application project | `services/Diten.HcmService/src/Diten.HcmService.Application` |
| Future domain project | `services/Diten.HcmService/src/Diten.HcmService.Domain` |
| Future infrastructure project | `services/Diten.HcmService/src/Diten.HcmService.Infrastructure` |
| Future persistence project | `services/Diten.HcmService/src/Diten.HcmService.Persistence` |
| Future test project | `services/Diten.HcmService/tests/Diten.HcmService.Application.Tests` |
| Recommended future port | `5060` |
| Browser/runtime ingress | Gateway `5000` only |
| Frontend shell | Tenant shell, `Layout = "_LayoutTenantShell";` |

### Scaffold Gate

The HCM service scaffold exists at `services/Diten.HcmService` on local port `5060`. It creates only the service boundary and does not authorize MOD-0251 employee runtime behavior.

## P1 HCM Service Scaffold / Port Decision

Prompt ID: `MOD0251-P1-HCM-SERVICE-SCAFFOLD-PORT-DECISION-M1`.

This decision closes the MOD-0251 HCM service owner and local port planning gate only. The follow-up scaffold prompt created the service boundary. It does not create frontend routes, gateway routes, migrations, MOD-0251 behavior tests, run-script entries, permission seeds, audit wiring, employee entities, employee DTOs, or employee APIs.

### Decision

| Decision Point | Approved Planning Decision |
|---|---|
| Service boundary | Dedicated HCM domain service |
| Service owner | `Diten.HcmService` |
| Service root | `services/Diten.HcmService` |
| API project | `services/Diten.HcmService/src/Diten.HcmService.Api` |
| Application project | `services/Diten.HcmService/src/Diten.HcmService.Application` |
| Domain project | `services/Diten.HcmService/src/Diten.HcmService.Domain` |
| Infrastructure project | `services/Diten.HcmService/src/Diten.HcmService.Infrastructure` |
| Persistence project | `services/Diten.HcmService/src/Diten.HcmService.Persistence` |
| Application test project | `services/Diten.HcmService/tests/Diten.HcmService.Application.Tests` |
| Local downstream port | `5060` |
| Browser/runtime ingress | Gateway `5000` only |
| Gateway route ownership | `integration-agent`, after HCM API route contract exists |
| Frontend service-port rule | No direct frontend calls to `5060` |

### Port Evidence

- Existing local service sequence uses Gateway `5000`, Frontend `5001`, Enterprise Strategy HTTP `5004`, Auth `5056`, Platform `5057`, DevEnablement `5058`, and MDM `5059`.
- Current `run_all.sh`, `run_watch.sh`, and `gateway/Diten.ApiGateway/ocelot.json` evidence reserve/use `5000`, `5001`, `5004`, `5056`, `5057`, `5058`, and `5059`.
- Repo search found no existing `Diten.HcmService`, `HcmService`, or `5060` runtime reservation outside this planning pack.
- `5060` is the next local service port after the approved MDM `5059` runtime decision and is approved as the HCM downstream port for future scaffold planning.

### Scaffold Boundaries

- The scaffold prompt created only the service boundary, solution/projects, local launch settings for `http://localhost:5060`, and minimal build/runtime wiring needed to prove the empty HCM service builds.
- MOD-0251 employee entities, CQRS handlers, controllers, persistence collections/indexes, permission seeds, audit emitters, frontend pages, gateway routes, and migrations remain outside the scaffold decision.
- HCM service code must not be placed under Platform, Auth, MDM, DevEnablement, or EnterpriseStrategy.
- Gateway ingress for MOD-0251 remains blocked until the HCM API route contract exists and an approved `integration-agent` prompt adds routes.

### Placement Guard

MOD-0251 must not be implemented inside:

- `services/Diten.Platform/**`.
- `services/Diten.AuthService/**`.
- `services/Diten.MdmService/**`.
- `services/Diten.DevEnablementService/**`.
- `services/Diten.EnterpriseStrategyService/**`.

Those services may be consumed through explicit contracts only. They do not own the native HCM employee master SoR.

### Topology Closure Impact

This section closes the service-name/path/port recommendation as the pack's planning topology. It does not close the remaining approval blockers:

- MOD-0299 registry reconciliation or de-scope.
- MOD-0288 person/reference contract confirmation.
- MOD-0023 workflow contract closure or activation blocked.
- MOD-0031 evidence contract closure or evidence deferred.
- MOD-0314 masking contract closure or sensitive reads denied.
- Tokenization/security contract closure or government identifier capture disabled.
- MOD-0251 permission seed and audit wiring implementation.
- Route, DTO, validation, and Given/When/Then acceptance criteria.

### GC-03 Dependency Contract Readiness Matrix

| Dependency | Readiness | Blocks Approval | Blocks Implementation | R1 Fail-Closed Position |
|---|---|---:|---:|---|
| `MOD-0018 RBAC / Authorization` | Executable infrastructure exists; MOD-0251 permission seed, role matrix, and endpoint mapping are not closed. | Yes | Yes | Deny protected endpoints until matrix and seeds exist. |
| `MOD-0021 Audit Trail` | Executable audit infrastructure exists; MOD-0251 audit payloads/events are not closed. | Yes | Yes | Block critical writes and sensitive reads until payload contract exists. |
| `MOD-0048 Reference Data / Lookups` | Executable Platform lookup surface exists; HR business controlled values are not closed for MOD-0251. | Yes | Yes | Block create/submit validation until HR lookup source is confirmed. |
| `MOD-0288 Organization, Person & Position Directory` | Conditional executable readiness for person reference now exists: backend + gateway + frontend proxy/picker are implemented; frontend Vitest remains blocked by missing local test dependency. Organization-unit and position providers plus read-only Gateway ingress are planning-closed to MOD-0288 / Platform runtime. | Conditional | Yes | MOD-0251 may consume existing `PersonId`, organization unit, and position references only through approved Gateway/MVC proxy paths; block employee creation/submission or employment validation when lookup-validation, gateway, picker/reference validation, or provider response is unavailable. |
| `MOD-0023 Workflow` | Missing / not executable for MOD-0251; inspected posture is mock/disabled or not implemented. | No for draft/reference-validation approval when submit/activation is de-scoped | Yes for submit, approval, and Active transition | Activation submit and approval decision remain blocked. |
| `MOD-0031 Evidence Linking Service` | Missing / not executable for MOD-0251; evidence dependency is mock/disabled or not implemented. | Yes unless evidence is formally deferred | Yes for evidence flows | Evidence step disabled/skipped when not required; evidence-linked activation blocked. |
| `MOD-0028 Documentation / Evidence Management` | Not executable for MOD-0251 evidence attachment. | No if first slice excludes evidence | Yes for evidence attachment | Evidence attachment deferred and disabled. |
| `MOD-0030 Records Management / Retention / Legal Hold` | Retention/legal-hold execution contract unavailable for MOD-0251 business records. | No if destructive/legal-hold flows are deferred | Yes for destructive lifecycle and legal hold | Destructive flows blocked; no retention inference. |
| `MOD-0314 HR Governance & Sensitive Access Controls` | Missing masking / sensitive access contract. | Yes | Yes for sensitive reads/responses | Deny sensitive reads or return non-sensitive shapes only. |
| Tokenization/security service | Missing government identifier tokenization contract. | Yes for government identifier capture | Yes for government identifier capture | Government identifier capture disabled. |

Approval impact:

- `MOD-0018`, `MOD-0021`, and `MOD-0048` are executable only as platform primitives; each still requires MOD-0251-specific contract closure before approval.
- `MOD-0288` person reference moved from hard blocker to conditional readiness for the person dependency only. MOD-0251-specific use must still consume only existing `PersonId` values through the approved Gateway/MVC proxy path and must not substitute Auth `UserId`.
- Non-person provider and read-only Gateway ingress decisions are planning-closed: organization units and positions are consumed from MOD-0288 / Platform runtime, and legal entity/company validation is assigned to the executable MDM legal-entity lookup-validation provider for R1 unless a later Blueprint/domain decision overrides it. Position assignment workflow remains deferred pending MOD-0299 reconciliation or explicit first-slice de-scope.
- `MOD-0023` no longer blocks approval of a draft/reference-validation slice because submit, approval, and Active transition are explicitly removed from first-slice scope. It still blocks activation implementation.
- `MOD-0031`, `MOD-0314`, and tokenization/security are missing or not executable and block approval unless the affected behavior is formally removed from approval scope or fail-closed.
- `MOD-0028` and `MOD-0030` may remain deferred only when evidence/destructive lifecycle flows are explicitly out of the first slice and fail-closed.
- MOD-0251 RBAC/audit planning contracts and Given/When/Then acceptance criteria are recorded; permission seed implementation, audit wiring implementation, DTO contracts, route contracts, and validation contracts remain blocked before implementation approval.

### MOD-0288 / Reference Contract Finding

Prompt ID: `MOD0251-P1-MOD0288-REFERENCE-PACK-UPDATE-M1`.

The MOD-0288/reference contract inspection originally returned `BLOCKED` for pack approval because no executable
person-reference contract was proven. Follow-up MOD-0288 person-reference work now provides conditional executable
readiness for the person dependency: backend read/search/lookup-validation, Gateway exposure, and frontend MVC
proxy/picker exist. This section records planning findings only; it does not create adapters, backend code, frontend
code, gateway routes, migrations, tests, workbook edits, registry edits, or implementation approval.

#### Reference Readiness Matrix

| Reference | Readiness | Impact | MOD-0251 Rule |
|---|---|---|---|
| Person | Implemented backend + gateway + frontend proxy/picker; conditional frontend test caveat remains because Vitest is not installed locally. | No longer a hard standalone person-contract blocker, but MOD-0251 approval remains blocked by other dependencies and route/DTO/validation/service blockers. | Employee creation requires a pre-existing same-tenant `PersonId`; MOD-0251 must consume person references through Gateway/MVC proxy paths only and must never substitute Auth `UserId`. |
| Organization Unit | Provider planning-closed to MOD-0288 / Platform organization-unit runtime. Executable service API and approved Gateway ingress are closed for read/reference use. | Provider and ingress decision closed; runtime employment validation may consume only the approved Gateway path. | MOD-0251 may consume same-tenant organization unit references only; it must not own organization directories. |
| Position | Provider planning-closed to MOD-0288 / Platform position runtime. Executable service API and approved Gateway ingress are closed for read/reference use. | Provider and ingress decision closed; runtime assignment snapshot validation may consume only the approved Gateway path. | MOD-0251 may validate existing same-tenant positions only; it must not own the position directory. |
| Position Assignment | Platform primitive exists, but MOD-0299 owns assignment workflow/orchestration after registry reconciliation. | Executable primitive only; workflow dependency remains deferred until MOD-0299 is reconciled or explicitly de-scoped. | MOD-0251 may store only employee-side assignment snapshot after valid person/org/position references; no executable assignment workflow dependency is approved. |
| Legal Entity / Company | Provider planning-closed for R1 to executable MDM legal-entity lookup-validation unless a later Blueprint/domain decision assigns a dedicated MOD-0220/legal-entity provider. MDM runtime port, restore/build, and approved Gateway ingress are closed for read/lookup-validation use. | Provider and ingress decision closed for R1; runtime employment record validation may consume only the approved Gateway path. | MOD-0251 consumes the legal-entity provider contract only; it must not own legal entity/company master data. |

#### Owner Decisions

- `MOD-0288` owns person, organization, and position references.
- `MOD-0251` consumes references only and must not own person, organization, position, or legal entity master directories.
- `MOD-0251` may consume MOD-0288 person references only as existing `PersonId` values through the approved Gateway path or frontend same-origin MVC proxy; AuthService `UserId` is not a person-reference substitute.
- MOD-0288 / Platform organization-unit runtime is the provider for organization-unit references.
- MOD-0288 / Platform position runtime is the provider for position references.
- MDM legal-entity lookup-validation is the R1 provider for legal entity/company validation unless a later Blueprint/domain decision assigns a dedicated MOD-0220/legal-entity provider.
- `MOD-0299` owns assignment workflow/orchestration after registry reconciliation is complete.
- MOD-0251 may store an employee-side assignment snapshot only after valid person, organization, position, and company/legal-entity reference confirmation.

#### MOD-0288 Person Dependency Executable Evidence

| Layer | Evidence |
|---|---|
| Backend routes | `GET /api/v1/platform/persons/{personId}`; `GET /api/v1/platform/persons`; `POST /api/v1/platform/persons/lookup-validation`. |
| Backend permission keys | `platform.person.view`; `platform.person.search`; `platform.person.lookup_validation`. |
| Gateway routes | Upstream `/api/v1/platform/persons`; upstream `/api/v1/platform/persons/{everything}`; downstream Platform service `localhost:5057`. |
| Frontend proxy/picker | MVC proxy `/Platform/PersonReferences/api`; reusable `window.PersonReferenceApi`; reusable `window.PersonReferencePicker`; browser calls same-origin proxy only; proxy forwards to Gateway path only. |
| Backend validation | Platform API build PASS with 0 warnings / 0 errors; focused TenantOrganization tests PASS with 60 passed / 0 failed. |
| Gateway validation | `ocelot.json` JSON validation PASS; route assertion PASS; Gateway build PASS with 0 warnings / 0 errors. |
| Frontend validation | `Diten.Web` build PASS with existing warnings / 0 errors; `node --check` for picker PASS. |
| Remaining caveat | Focused Vitest picker test is authored but not executable until the frontend test dependency (`vitest`) is installed/restored in `frontend/Diten.Web/node_modules`. |

#### Non-Person Reference Ingress / Provider Closure

This section closes provider ownership decisions for non-person references in planning terms only. It does not add
Gateway routes, frontend code, backend code, tests, adapters, migrations, workbooks, registries, or implementation
approval.

| Reference | Provider Decision | Readiness | MOD-0251 Behavior | Fail-Closed Rule |
|---|---|---|---|---|
| Organization Unit | MOD-0288 / Platform organization-unit runtime. | Executable service API and approved Gateway/reference ingress are closed through `/api/platform/organization-units` and `/api/platform/organization-units/{everything}`. | Consume existing same-tenant organization-unit references only through Gateway; no organization ownership or local directory copy. | Unresolved, tenant-mismatched, stale/non-referenceable, unavailable provider, or unavailable Gateway route blocks relevant employment validation. |
| Position | MOD-0288 / Platform position runtime. | Executable service API and approved Gateway/reference ingress are closed through `/api/platform/positions` and `/api/platform/positions/{everything}`. | Consume existing same-tenant position references only through Gateway; no position ownership or local directory copy. | Unresolved, tenant-mismatched, stale/non-referenceable, unavailable provider, or unavailable Gateway route blocks employment/assignment snapshot validation. |
| Position Assignment | Platform primitive exists; MOD-0299 owns assignment workflow/orchestration after registry reconciliation. | Executable primitive only; workflow dependency remains deferred. | Store employee-side assignment snapshot only after valid person, organization-unit, and position references; do not depend on executable assignment workflow in the first slice. | No assignment workflow dependency is allowed until MOD-0299 is reconciled or explicitly de-scoped; ambiguous workflow dependency blocks implementation. |
| Legal Entity / Company | MDM legal-entity lookup-validation is the R1 provider unless a later Blueprint/domain decision assigns a dedicated MOD-0220/legal-entity provider. | Executable MDM lookup-validation, runtime port `localhost:5059`, MDM restore/build, and approved Gateway/reference ingress are closed through `/api/legal-entities` and `/api/legal-entities/{everything}`. | Consume provider contract only through Gateway; no legal entity/company master ownership in MOD-0251. | Unresolved, inactive, non-referenceable, tenant-mismatched, unavailable provider, or unavailable Gateway route blocks employment record validation. |

Gateway/reference ingress plan:

- Person ingress is conditionally closed through `/api/v1/platform/persons` and `/api/v1/platform/persons/{everything}`.
- Non-person Gateway/reference ingress is closed for organization units, positions, and legal entities through approved read-only Gateway routes.
- Position assignment Gateway/reference ingress is intentionally not exposed for the MOD-0251 first slice and remains deferred pending MOD-0299 reconciliation or explicit de-scope.
- MDM Legal Entity downstream host/port decision is planning-closed for MOD-0251 purposes as `localhost:5059`; evidence is the existing Platform API `MdmService:BaseUrl` configuration in both base and development appsettings.
- MDM runtime launch setup is recorded as available for local development on `localhost:5059` through `Diten.MdmService.Api` launch settings and the local `run_all.sh` / `run_watch.sh` scripts.
- Legal Entity Gateway ingress is closed through downstream `localhost:5059` and the approved paths `/api/legal-entities` and `/api/legal-entities/{everything}`.
- Gateway implementation remains `integration-agent`-only and must not be added from this pack.
- Runtime approval still requires position assignment workflow de-scope/reconciliation and closure or deferral of the remaining non-reference blockers.

#### Non-Person Reference Executable Evidence

| Reference | Gateway Routes | Downstream | Methods | Validation Evidence |
|---|---|---|---|---|
| Organization Unit | `/api/platform/organization-units`; `/api/platform/organization-units/{everything}` | Platform `localhost:5057` | `GET`, `OPTIONS` | `ocelot.json` JSON validation PASS; focused route assertion PASS; Gateway build PASS with 0 warnings / 0 errors. |
| Position | `/api/platform/positions`; `/api/platform/positions/{everything}` | Platform `localhost:5057` | `GET`, `OPTIONS` | `ocelot.json` JSON validation PASS; focused route assertion PASS; Gateway build PASS with 0 warnings / 0 errors. |
| Legal Entity / Company | `/api/legal-entities`; `/api/legal-entities/{everything}` | MDM `localhost:5059` | `GET`, `OPTIONS` | MDM focused restore PASS; MDM API build PASS with 0 warnings / 0 errors; `ocelot.json` JSON validation PASS; focused route assertion PASS; Gateway build PASS with 0 warnings / 0 errors. |
| Position Assignment | Not exposed for MOD-0251 first slice. | N/A | N/A | Deferred pending MOD-0299 reconciliation or explicit de-scope. |

Auth convention:

- No anonymous routes were added for non-person reference ingress.
- Gateway auth was not weakened.
- Downstream controllers remain authoritative for `[Authorize]` and permission gates:
  - `platform.organization-units.read`
  - `platform.positions.read`
  - `platform.organization.read-manager-chain`, where relevant
  - `mdm.legal-entities.read`

#### Reference Fail-Closed Rules

- Unresolved reference blocks create/submit or employment validation.
- Tenant mismatch returns not found or denied and must not disclose cross-tenant existence.
- Dependency unavailable blocks the affected runtime slice; no inferred local replacement is allowed.
- Stale, deprecated, archived, inactive, or deleted reference is not referenceable.
- Missing person reference blocks employee creation/submission.
- If MOD-0288 person lookup-validation is unavailable, MOD-0251 employee creation/submission remains blocked.
- If the Gateway person route is unavailable, person selection in the UI remains blocked.
- If the frontend picker/proxy cannot validate the selected `PersonId`, submit remains blocked.
- Auth `UserId` remains an invalid substitute for MOD-0288 `PersonId`.
- If the organization-unit, position, or legal-entity Gateway route is unavailable, the related employment validation must be blocked.
- Organization-unit, position, and legal-entity provider/ingress decisions are planning-closed, but unresolved, inactive, non-referenceable, tenant-mismatched, stale/deprecated, archived/deleted, or unavailable provider responses still block employment validation.
- Position assignment workflow remains deferred until MOD-0299 reconciliation or explicit de-scope.

## Service Topology Blocker

The HCM backend service scaffold exists at `services/Diten.HcmService`. MOD-0251 must not be placed in `Diten.Platform`, `Diten.AuthService`, `Diten.MdmService`, `Diten.DevEnablementService`, or `Diten.EnterpriseStrategyService`, because those services do not own native HCM employee master SoR data.

P1 topology and scaffold/port decisions select a dedicated HCM service boundary at `services/Diten.HcmService` with `Diten.HcmService.Api`, `Application`, `Domain`, `Infrastructure`, `Persistence`, and `Diten.HcmService.Application.Tests` projects. Approved local downstream port is `5060`.

Employee runtime implementation remains fail-closed until a later approved MOD-0251 implementation prompt authorizes employee business behavior.

## Gateway Blocker

No HCM gateway route exists. Gateway work remains deferred to `integration-agent` after the HCM service/API route and downstream port are approved.

Frontend and browser traffic must use Gateway `5000` only. Direct calls from frontend code to service ports are prohibited.

## Frontend Topology Blocker

The tenant shell exists and remains the preferred shell for MOD-0251. No HCM Employee UI exists yet.

Frontend planning remains:

- Employee Registry uses DataTable v2; `GoldenReferenceCompact` applies to registry/list structure only.
- Create Employee uses a wizard pattern, not a direct CRUD create-form clone.
- Employee Detail uses a section/detail page pattern, not a simple CRUD details clone.
- No HCM frontend file may be created until service/API contracts and route topology are approved.

## Dependency Contract Blockers

| Dependency | P0 Finding | Blocker Rule |
|---|---|---|
| `MOD-0023 Workflow` | Contract is not executable for MOD-0251; current inspected adapters are mock/disabled or not implemented. | Activation workflow and approval-decision implementation remain blocked. |
| `MOD-0031 Evidence Linking Service` | Contract is not executable for MOD-0251; evidence dependency is mock/disabled or not implemented. | Evidence completion and evidence-linked activation remain blocked. |
| `MOD-0028 Documentation / Evidence Management` | Evidence/document integration is unavailable for executable MOD-0251 use. | Evidence attachment remains blocked. |
| `MOD-0030 Records Management / Retention / Legal Hold` | Retention/legal hold integration is unavailable for MOD-0251 execution. | Retention-aware destructive flows and legal-hold behavior remain blocked. |
| `MOD-0314 HR Governance & Sensitive Access Controls` | Masking contract is unavailable. | Sensitive response shaping and sensitive-read paths remain blocked. |
| Tokenization/security service | Contract is unavailable. | Government identifier capture remains disabled/blocked. |
| `MOD-0288 Organization, Person & Position Directory` | Person backend + Gateway + frontend proxy/picker are conditionally executable; Vitest verification remains blocked by missing frontend test dependency. Organization-unit and position provider decisions plus read-only Gateway ingress are planning-closed to MOD-0288 / Platform runtime. | Employee creation may consume existing same-tenant `PersonId` only through Gateway/MVC proxy validation; employment validation may consume organization-unit and position references only through approved Gateway paths. |
| Legal Entity / Company provider | R1 provider decision is planning-closed to executable MDM legal-entity lookup-validation unless a later Blueprint/domain decision assigns a dedicated MOD-0220/legal-entity provider. MDM runtime port, restore/build, and read-only Gateway ingress are closed. | Employment record validation may consume only the approved provider contract through Gateway; unresolved/inactive/non-referenceable/tenant-mismatched/unavailable legal entity blocks validation. |

## RBAC / Audit Blockers

- RBAC infrastructure exists and the MOD-0251 permission catalog, role-permission matrix, protected action map, and fail-closed rules are now closed for planning.
- MOD-0251 permission seed implementation, policy registration, controller attributes, and endpoint enforcement remain blocked until this pack is promoted and a separate approved implementation prompt authorizes them.
- Audit infrastructure exists and the MOD-0251 audit event catalog, safe payload matrix, and fail-closed rules are now closed for planning.
- MOD-0251 audit behavior wiring, event emitters, critical category registration, and implementation tests remain blocked until this pack is promoted and a separate approved implementation prompt authorizes them.
- Critical writes, sensitive reads, exports, workflow decisions, and access denials must remain blocked or fail-closed until the implementation wiring exists.

## MOD-0299 Defer / Reconcile Decision

Decision selected: **Option A - first-slice de-scope**.

Current conflict:

- Blueprint master 5 and Blueprint master 7 identify `MOD-0299` as `Position & Organization Assignment`.
- The MOD-0251 specification uses `MOD-0299` as the owner of assignment workflow/orchestration.
- The current module ID registry maps `MOD-0299` to deprecated SaaS Billing & Invoicing alias `CAND-CAP-0005`.
- MOD-0251 must not bind to any executable `MOD-0299` assignment workflow/orchestration contract until the registry is reconciled by Enterprise Architect/user approval.

First activation slice decision:

- Executable `MOD-0299` assignment workflow/orchestration is explicitly excluded from the MOD-0251 first approved draft/reference-validation slice.
- MOD-0251 may store only the employee-side job assignment / employment assignment snapshot after the person, organization-unit, position, and legal entity references are validated through their approved provider/Gateway paths.
- Position assignment workflow remains a later R1 or post-first-slice dependency.
- Position assignment Gateway ingress remains intentionally not exposed for MOD-0251 first-slice work.
- Registry reconciliation is still required before any executable `MOD-0299` workflow/orchestration implementation, dependency binding, Gateway route, or assignment console work.

Exact future registry reconciliation prompt, requiring EA/user approval before use:

`MOD0299-REGISTRY-RECONCILE-POSITION-ORG-ASSIGNMENT-M1`

Scope: reconcile `execution/registries/module-id-registry.md` so Blueprint `MOD-0299 Position & Organization Assignment` is no longer blocked by the deprecated SaaS Billing alias / `CAND-CAP-0005`, without changing MOD-0251 implementation code.

## Workflow / Evidence / Masking / Tokenization Dependency Decision

This section records first-slice governance decisions only. It does not create backend code, frontend code, gateway routes, tests, migrations, adapters, or implementation approval.

## Workflow First-Slice De-scope Decision

Prompt ID: `MOD0251-P1-WORKFLOW-FIRST-SLICE-DESCOPE-PACK-UPDATE-M1`.

Decision selected: **Option A - de-scope approval from first approved slice**.

The first approved implementation slice is redefined as a draft/reference-validation slice only:

- Create Employee Draft.
- Save Draft steps.
- Validate same-tenant person, organization-unit, position, and legal-entity references through approved provider/Gateway paths.
- Review Draft.
- Reload persisted draft.

Explicitly excluded from the first approved slice:

- Submit for approval.
- MOD-0023 workflow start.
- MOD-0023 approval/rejection decision processing.
- Active transition.
- `employee.created` event/outbox behavior.
- Approval audit events such as `employee.submitted_for_approval`, `employee.approved`, and `employee.rejected`, except draft/review audit events where applicable.

Activation remains a later sequence blocked until an executable MOD-0023 workflow contract exists. MOD-0251 must not implement local approval logic, independent approval state, or an approval bypass. This de-scope moves the pack closer to approval for a draft/reference-validation slice only; it does not approve activation, employee creation as Active, workflow integration, gateway routes, frontend pages, migrations, or employee business implementation.

## P1 Draft / Reference Validation Contract

Prompt ID: `MOD0251-P1-CONTRACTS-M1`.

This section closes contract documentation for the first approved draft/reference-validation slice only. It does not create backend code, frontend code, gateway routes, tests, migrations, employee entities, employee APIs, workbooks, registry entries, or implementation approval.

### First-Slice API Contract Stubs

| Method | Route | Purpose | Scope |
|---|---|---|---|
| `POST` | `/api/v1/hcm/employees/drafts` | Create a tenant-scoped employee draft session. | First slice |
| `PATCH` | `/api/v1/hcm/employees/drafts/{draftSessionId}` | Save one draft step or partial draft payload with ETag and idempotency. | First slice |
| `GET` | `/api/v1/hcm/employees/drafts/{draftSessionId}` | Reload persisted draft, step state, validation summary, and current ETag. | First slice |
| `POST` | `/api/v1/hcm/employees/drafts/{draftSessionId}/validate-references` | Validate person, organization-unit, position, and legal-entity references through approved provider/Gateway paths. | First slice |
| `POST` | `/api/v1/hcm/employees/drafts/{draftSessionId}/review` | Persist non-submit review state and return readiness summary. This route must not submit, activate, or start workflow. | Optional first slice |

Out of scope for this contract: submit, approval, activate, reject, `employee.created` event/outbox behavior, employee `Active` state, evidence link, export, status change, and data-quality workflow beyond a duplicate-warning/readiness placeholder.

### DTO Contract Stubs

| DTO | Direction | Required Shape |
|---|---|---|
| `EmployeeDraftCreateRequest` | Request | Optional `source_context`, optional `client_reference`, `idempotency_key`; no PII payload at creation; no client-supplied `tenant_id`. |
| `EmployeeDraftCreateResponse` | Response | `draft_session_id`, `draft_schema_version`, `current_step`, `step_statuses`, `validation_summary`, `version`, `etag`, `created_at`. |
| `EmployeeDraftPatchRequest` | Request | `step_code`, `payload_schema_version`, `step_payload`, `client_validation_state`, `idempotency_key`; `If-Match` header required; no raw government identifier values. |
| `EmployeeDraftResponse` | Response | `draft_session_id`, `draft_schema_version`, `current_step`, redacted `steps`, `step_statuses`, `reference_validation_summary`, `review_state`, `version`, `etag`, `updated_at`, `expires_at`. |
| `ReferenceValidationRequest` | Request | Optional explicit references to validate: `person_id`, `organization_unit_id`, `position_id`, `legal_entity_id`; no Auth `user_id`; no client-supplied tenant authority. |
| `ReferenceValidationResponse` | Response | Per-reference result with `reference_type`, `reference_id`, `status`, `is_referenceable`, `provider`, `reason_code`, safe display metadata, and aggregate `can_review`. |
| `DraftReviewRequest` | Request | `idempotency_key`, review acknowledgement flags, optional duplicate-warning acknowledgement, current `etag`; no submit or approval fields. |
| `DraftReviewResponse` | Response | `draft_session_id`, `review_state`, `can_submit_later`, `blocking_reasons`, `validation_summary`, `reference_validation_summary`, `version`, `etag`; submit/activation fields omitted. |
| `ProblemDetails` | Error | Standard problem-details shape with `type`, `title`, `status`, `code`, `trace_id`, safe `errors[]` entries containing `field`, `code`, and redacted `message`. |

### Validation Contract

- Draft save may persist incomplete legal/profile/employment steps, but Review Draft requires the draft to have required first-slice fields needed for reference validation: `person_id`, legal name fields needed for duplicate/readiness checks, worker type, employment type, hire date where policy requires it, organization-unit reference, position reference, legal-entity/company reference, and sensitivity level.
- Person reference validation must use the approved MOD-0288 person Gateway/MVC proxy path. MOD-0251 must consume an existing same-tenant `PersonId` only and must never substitute Auth `UserId`.
- Organization-unit validation must use the approved Platform Gateway path `/api/platform/organization-units` or `/api/platform/organization-units/{everything}`.
- Position validation must use the approved Platform Gateway path `/api/platform/positions` or `/api/platform/positions/{everything}`.
- Legal-entity validation must use the approved MDM Gateway path `/api/legal-entities` or `/api/legal-entities/{everything}`.
- Client-supplied `TenantId` is ignored/rejected; tenant authority comes only from server auth/tenant context.
- Government identifier capture is disabled; raw government identifiers are rejected and must not be stored, logged, audited, or returned.
- Sensitive fields are denied, omitted, or masked unless and until an executable MOD-0314 contract permits them.
- Evidence step is skipped/disabled for this slice; no evidence upload, evidence link, document link metadata, or orphan evidence state is allowed.
- Duplicate detection may return a readiness warning/blocking reason only; data-quality assignment/resolution workflow is out of scope.

### Persistence Contract

- First-slice persistence object: `EmployeeDraftSession` only.
- `draft_schema_version`: `employee-create-wizard.v1`.
- Draft payload must be encrypted or redacted according to HCM sensitive-data policy; audit/log payloads must carry safe metadata only.
- `TenantId` is server-side and mandatory on persisted draft/session records.
- Optimistic concurrency is mandatory through `version` and `etag`; stale writes return conflict.
- Idempotency key is mandatory for create, save, validate-references, and review operations.
- Reload persistence must return saved draft steps, reference-validation summary, review state, ETag/version, and safe audit references after service restart/reload.
- No Active employee record, employment record, status-history activation row, approval state, or employee-created event/outbox record may be created in this slice.

### Audit Contract For This Slice

| Event | Trigger | Safe Payload Only |
|---|---|---|
| `employee_draft.created` | Draft session created. | `tenant_id`, `actor_id`, `draft_session_id`, `correlation_id`, `idempotency_key_hash`, `draft_schema_version`. |
| `employee_draft.updated` | Draft step saved. | `tenant_id`, `actor_id`, `draft_session_id`, `step_code`, `changed_field_names`, `version`, `correlation_id`. |
| `employee_draft.references_validated` | Reference validation completed. | `tenant_id`, `actor_id`, `draft_session_id`, reference types checked, provider names, aggregate result, reason codes. |
| `employee_draft.reviewed` | Non-submit review state persisted. | `tenant_id`, `actor_id`, `draft_session_id`, review result, blocking reason codes, version, `correlation_id`. |
| `employee.access_denied` | RBAC, tenant, masking, dependency, or validation policy denies action. | `tenant_id`, `actor_id`, action, permission key, denial reason code, `correlation_id`; no PII values. |

Prohibited audit payloads: raw PII values, raw government identifiers, token values, full draft payloads, document contents, provider response bodies, unredacted free text, and exported row data.

### Fail-Closed Behavior

- Missing person Gateway/proxy route blocks person validation and draft review readiness.
- Missing organization-unit, position, or legal-entity Gateway route blocks the related reference validation and draft review readiness.
- Dependency unavailable, timeout, ambiguous status, stale/deprecated/inactive reference, or tenant mismatch blocks Review Draft.
- Tenant mismatch returns not found or denied without cross-tenant existence disclosure.
- Stale ETag returns conflict and must not overwrite newer draft data.
- Idempotency replay returns the original create/save/validate/review result or a controlled replay response without duplicate draft, review, validation, or audit effects.
- Audit failure for critical draft writes blocks or rolls back the write.
- MOD-0023 unavailable keeps submit/approval/Active unavailable; no local approval substitute is allowed.

### Contract-Test Plan

No implementation tests are created by this prompt. Future implementation prompts must add focused tests for:

- Draft create, save, get/reload, validate-references, and review.
- RBAC denial and tenant isolation.
- ETag conflict and idempotency replay.
- Safe audit payload emission and audit failure behavior.
- Person, organization-unit, position, and legal-entity validation success/failure paths.
- Government identifier rejection and sensitive-field mask/deny behavior.
- Confirming no submit, approval, Active transition, evidence link, export, status change, or data-quality workflow behavior appears in the first slice.

## Conditional Promotion Decision

Prompt ID: `MOD0251-P1-PACK-CONDITIONAL-PROMOTION-M1`.

Decision: `status: approved` with `approval_status: conditionally-approved-for-draft-reference-validation-slice`.

This promotion is scoped only to the reduced draft/reference-validation slice. It is not approval for activation, submit/approval, full lifecycle, evidence/export/status/data-quality flows, or full MOD-0251 implementation.

### Approved Slice Scope

The conditionally approved slice includes only:

- Draft create.
- Draft save/update.
- Draft reload.
- Person, organization-unit, position, and legal-entity reference validation through approved provider/Gateway paths.
- Review Draft as a non-submit, non-activation readiness state.
- Draft-slice audit events.
- RBAC enforcement for draft/reference actions.
- ETag and optimistic concurrency handling.
- Idempotency for create/save/validate/review.
- Tenant isolation and fail-closed tenant mismatch behavior.

### Explicit Exclusions

This conditional promotion does not approve:

- Submit for approval.
- MOD-0023 workflow start.
- Approval/rejection decision processing.
- Active transition.
- `employee.created` event/outbox behavior.
- Evidence upload/link.
- Export.
- Status change.
- Data-quality workflow beyond duplicate-warning/readiness placeholder.
- Employee activation.
- Full HR lifecycle.

### Promotion Conditions / Caveats

- Frontend person picker Vitest test exists but cannot run until `vitest` is installed/restored in `frontend/Diten.Web`.
- MOD-0023 workflow remains non-executable and blocks activation only.
- MOD-0031, MOD-0028, and MOD-0030 evidence/retention remain deferred/fail-closed.
- MOD-0314 masking contract is unavailable; sensitive reads deny or mask by default.
- Tokenization is unavailable; government identifier capture is disabled.
- MOD-0299 assignment workflow remains de-scoped from the first approved slice; registry reconciliation remains a future governance item.
- Permission seed and audit wiring are implementation tasks after promotion and are allowed only for the approved draft/reference-validation slice.
- HCM Gateway route for employee APIs does not exist yet and must wait until employee APIs exist.
- This promotion is not authorization for activation or full MOD-0251 implementation.

### Next Implementation Boundary

The next implementation prompt may only implement:

- Draft session persistence.
- Draft create/save/get APIs.
- Reference validation using existing Gateway/provider paths.
- Review-only non-submit endpoint, if included.
- RBAC and audit for those draft actions.

The next implementation prompt must not create:

- Active employee record behavior.
- Submit/approval.
- MOD-0023 workflow integration.
- `employee.created` event/outbox behavior.
- Evidence upload/link.
- Export/status/data-quality workflow.
- Frontend pages or Gateway routes unless separately approved in their own prompts.

| Dependency | Current State | First-Slice Decision | Fail-Closed Behavior | Approval Impact |
|---|---|---|---|---|
| `MOD-0023 Workflow` | Executable workflow start / decision contract for MOD-0251 is not proven. | Approval is removed from the first approved draft/reference-validation slice. Approval remains owned by MOD-0023 for any later activation sequence. MOD-0251 must not replace MOD-0023 with local approval logic or independent workflow state. | Submit/approval is disabled or blocked when MOD-0023 is unavailable. Employee must not become Active without a valid MOD-0023 decision contract. | Does not block approval of the draft/reference-validation slice. Still blocks activation implementation. |
| `MOD-0031 Evidence Linking` | Executable evidence link contract for MOD-0251 is not proven. | Evidence integration is deferred from the first approved draft/reference-validation slice when `activation_evidence_required = false`. Evidence-linked activation remains blocked when tenant policy requires evidence. | Evidence step is disabled/skipped when evidence is not required. Submit/approval is blocked when policy requires evidence but MOD-0031/MOD-0028/MOD-0030 contracts are unavailable. | Does not block first-slice approval only if evidence is formally disabled for that slice and policy is `activation_evidence_required = false`. |
| `MOD-0028 Documentation / Evidence Management` | Document/evidence attachment execution remains deferred for MOD-0251. | No upload/link UI in the first approved draft/reference-validation slice. Evidence attachment is a later R1 sequence. | No shell upload controls. No orphan document-link metadata. No document/content ownership in MOD-0251. | Deferred from first slice; required before evidence attachment implementation. |
| `MOD-0030 Records / Retention / Legal Hold` | Retention/legal-hold execution for employee evidence/destructive lifecycle is not closed. | Destructive lifecycle and legal-hold-sensitive flows remain deferred. No deletion, anonymization, destructive retention, or legal-hold-sensitive behavior in the first approved draft/reference-validation slice. | Destructive or legal-hold-sensitive flows are blocked until the contract exists. No retention inference is allowed. | Deferred from first slice; required before destructive lifecycle, retention, legal hold, and evidence retention behavior. |
| `MOD-0314 HR Governance & Sensitive Access Controls` | Executable masking / sensitive access policy hook is not proven. | Sensitive reads are denied or masked by default until MOD-0314 contract is available. First slice may expose only safe/minimal non-sensitive fields. | No sensitive PII is returned when policy hook is unavailable. `view_sensitive` is blocked or returns a masked/non-sensitive response. | Still blocks sensitive-read implementation; first slice may proceed only with non-sensitive/minimal response shapes. |
| Tokenization/security service | Government identifier tokenization contract is unavailable. | Government identifier capture is disabled/excluded from the first slice. Raw government identifier storage remains prohibited. | Government identifier fields are hidden, disabled, or rejected. No raw identifier or token value may appear in audit, logs, traces, events, or persisted employee data. | Government identifier capture remains blocked until tokenization/security contract is identified and approved. |

Approval gate interpretation:

- MOD-0023 does not block approval of the de-scoped draft/reference-validation slice because submit/approval and Active-state transition are excluded.
- MOD-0023 remains a hard blocker for any later approved slice that includes submit/approval or Active-state transition.
- MOD-0031/MOD-0028/MOD-0030 are formally deferred for first-slice evidence behavior only when `activation_evidence_required = false`; evidence-required tenants remain blocked.
- MOD-0314 absence requires default deny/mask for sensitive reads and non-sensitive-only response shapes.
- Tokenization absence requires government identifier capture to remain disabled and raw identifier storage prohibited.
- These decisions do not remove implementation-time checks; they define fail-closed behavior until executable dependency contracts exist.

## Approval Readiness

This pack is conditionally approved for a draft/reference-validation slice only. The P1 draft/reference-validation API, DTO, validation, persistence, audit, fail-closed, and future contract-test plan are closed in this pack. It cannot be marked `ready-for-dev` until remaining approval caveats are resolved or explicitly accepted.

Approval requires closure or formal fail-closed deferral of every blocker recorded in P0, GC-01/02/03, and P1 topology contract. At minimum, approval remains blocked until:

- HCM service scaffold exists at `services/Diten.HcmService` on approved local port `5060`; MOD-0251 employee runtime behavior still requires a separate approved implementation prompt.
- MOD-0299 assignment workflow/orchestration is explicitly de-scoped from the first approved draft/reference-validation slice. Registry reconciliation remains required before any executable MOD-0299 workflow/orchestration work.
- MOD-0288/reference contract is conditionally ready for person references and non-person provider/ingress decisions are closed for organization unit, position, and legal entity/company. It remains a blocker for approval only to the extent the frontend person-picker Vitest caveat must be accepted/resolved and position assignment workflow remains deferred/de-scoped.
- MOD-0288 person lookup/read/lookup-validation contract is confirmed for same-tenant employee creation and submission through Gateway/MVC proxy paths only; Auth `UserId` substitution remains prohibited.
- Company/legal-entity owner/provider decision is approved for R1 to MDM legal-entity lookup-validation, with future override allowed only by Blueprint/domain decision.
- Gateway/reference ingress is confirmed for organization unit, position, and legal entity validation through approved runtime paths. Position assignment ingress/workflow remains intentionally not exposed and is de-scoped from the first approved draft/reference-validation slice.
- Position assignment workflow remains deferred beyond the first approved draft/reference-validation slice until MOD-0299 registry reconciliation and a separately approved implementation sequence.
- MOD-0023 workflow contract no longer blocks approval of the draft/reference-validation slice because submit/approval and Active-state transition are explicitly removed from first approved scope.
- MOD-0023 workflow contract remains a blocker for any activation implementation, including submit for approval, approval/rejection decision processing, Active transition, `employee.created` event/outbox behavior, and approval audit events.
- P1 draft/reference-validation contract closure is complete for API stubs, DTO stubs, validation rules, persistence rules, audit events, fail-closed behavior, and future contract-test plan.
- MOD-0031 evidence contract remains unavailable, but evidence is formally deferred with disabled/skipped UI/API behavior when `activation_evidence_required = false`; evidence-required activation remains blocked.
- MOD-0028/MOD-0030 evidence, retention, and legal-hold behavior is formally deferred from the first slice with upload/link/destructive/legal-hold-sensitive flows blocked.
- MOD-0314 masking contract remains unavailable, so sensitive reads must be denied or return non-sensitive/masked shapes only.
- Tokenization/security contract remains unavailable, so government identifier capture remains disabled and raw identifier storage is prohibited.
- Given/When/Then acceptance criteria are added in this draft pack.
- MOD-0251 RBAC/audit contract closure planning is complete in this draft pack.
- MOD-0251 permission seed implementation remains pending after pack promotion and implementation approval.
- MOD-0251 audit wiring implementation remains pending after pack promotion and implementation approval.
- Submit/approval/activation contracts remain deferred and must not be implemented until executable MOD-0023 workflow contract exists.

## P2 Draft / Reference Validation Implementation Evidence

Prompt ID: `MOD0251-P2-EVIDENCE-PACK-UPDATE-M1`.

This section records evidence for the conditionally approved MOD-0251 draft/reference-validation implementation slice only. It does not authorize or record submit, approval, activation, Active employee behavior, evidence upload/link, export, status change, Data Quality Queue behavior, workbook changes, registry changes, or `.antigravity` changes.

### Backend Evidence

| Evidence Area | Result |
|---|---|
| Implemented runtime slice | HCM draft/reference APIs for draft create, draft save/update, draft reload, reference validation, and non-submit review scope. |
| Service owner | `Diten.HcmService` under `services/Diten.HcmService`. |
| Approved port | `5060`. |
| Build validation | HCM build PASS with 0 warnings / 0 errors. |
| Focused tests | HCM focused tests PASS: 5 passed / 0 failed. |
| Scope guard | No submit, approval, activation, evidence/export/status/DQ behavior is included in the approved P2 evidence. |

### Gateway Evidence

| Evidence Area | Result |
|---|---|
| Gateway route family | `/api/v1/hcm/employees/drafts`; `/api/v1/hcm/employees/drafts/{everything}`. |
| Downstream service | HCM downstream `localhost:5060`. |
| Gateway validation | Gateway build PASS with 0 warnings / 0 errors. |
| Scope guard | Gateway evidence is limited to draft/reference-validation ingress; no submit/approval/activation routes are recorded as approved. |

### Frontend Evidence

| Evidence Area | Result |
|---|---|
| Page route | `/HCM/Employees/Create`. |
| MVC proxy | Same-origin proxy under `/HCM/Employees/drafts/api...`. |
| Gateway-only path | Frontend proxy forwards to Gateway `/api/v1/hcm/employees/drafts...`; browser code must not call service ports directly. |
| Build validation | `Diten.Web` build PASS with 0 warnings / 0 errors. |
| Focused JS tests | Focused Vitest PASS: 3 tests. |
| Scope guard | UI scope is draft/reference-validation only; submit, approval, activation, evidence upload/link, export, status change, Data Quality Queue, and government identifier controls remain excluded or disabled. |

### HCM Startup Evidence

| Evidence Area | Result |
|---|---|
| Startup blocker | HCM startup hang was isolated to early host-builder startup before normal binding. |
| Fix recorded | HCM API startup uses `WebApplication.CreateEmptyBuilder(...)` with explicit configuration and Kestrel/URL setup instead of the default builder path that hung in local runtime. |
| Health check | HCM bound on `5060`; `/health` returned 200. |
| Runtime rule | Runtime smoke must use the explicit local .NET host rather than `DOTNET_ROLL_FORWARD=Major`, which can roll `net8.0` apps to a later runtime. |

### Unauthenticated Runtime Browser Smoke Evidence

| Evidence Area | Result |
|---|---|
| Services bound | HCM `5060`, Gateway `5000`, and Frontend `5001` bound during smoke retry. |
| Route reachability | `/HCM/Employees/Create` was reachable through Frontend `5001`. |
| Unauthenticated behavior | Route returned controlled redirect to `/account/login?ReturnUrl=%2FHCM%2FEmployees%2FCreate`. |
| Browser page | Login page loaded with title `Sign In - Di10`. |
| Console | No browser console errors or warnings were captured. |
| Direct service-port check | Browser-visible page URLs did not reference service ports `5056`, `5057`, `5059`, or `5060`. |
| Process cleanup | Smoke-started services were stopped cleanly; final listener check found no active listeners on `5000`, `5001`, or `5060`. |

### Final Authenticated Browser Runtime Evidence

Final authenticated browser replay closed the prior runtime evidence gap after the legal-entity helper fix.

| Evidence Area | Result |
|---|---|
| Tenant shell | Visible with `Employee Drafts`; `canCreateDraft=true`. |
| Draft create | PASS; draft session and ETag present. |
| Save/update | PASS; `If-Match` used and version advanced. |
| Reload | PASS; persisted draft reloaded by `draftSessionId`. |
| Reference validation | PASS; person, organization unit, position, and legal entity all valid/referenceable. |
| Legal entity picker | PASS; approved fixture selected and hidden legal entity value retained through save/reload. |
| Review | PASS; review state `reviewed`, `blockingReasons=[]`, and `canSubmitLater=false`. |
| Scope guard | PASS; no actionable submit, approval, activation, evidence upload/link, export/status/Data Quality Queue, or government identifier controls. |
| Direct service-port check | PASS; no direct browser references to service ports `5056`, `5057`, `5059`, or `5060`. |
| Console | PASS; no console errors. |

This evidence does not expand implementation scope and must not be used to add submit, approval, activation, evidence/export/status/DQ behavior, direct service-port calls, or broad seed/test data outside a later approved scope.

## P2 Conditional Closure / Handback

Prompt ID: `MOD0251-P2-CONDITIONAL-HANDBACK-M1`; final browser evidence reconciled by `MOD0251-P2-GOVERNANCE-WORDING-RECONCILE-M1`.

Final P2 verdict: `PASS`.

The MOD-0251 P2 draft/reference-validation implementation slice is closed as PASS for the approved slice only. The pack remains approved only for the draft/reference-validation scope: draft create, draft save/update, draft reload, person/organization-unit/position/legal-entity reference validation, and non-submit review. This handback does not approve submit, MOD-0023 workflow start, approval/rejection processing, Active transition, `employee.created` event/outbox behavior, evidence upload/link, export, status change, Data Quality Queue behavior, or government identifier capture.

### Closed P2 Evidence

| Evidence Area | Closure Status |
|---|---|
| Backend build/tests | Closed. HCM draft/reference API implementation evidence recorded; HCM build passed with 0 warnings / 0 errors; focused tests passed 5 / 0. |
| Gateway routes/build | Closed. HCM draft Gateway route family `/api/v1/hcm/employees/drafts` and `/api/v1/hcm/employees/drafts/{everything}` recorded with downstream `localhost:5060`; Gateway build passed with 0 warnings / 0 errors. |
| Frontend build/Vitest | Closed. `/HCM/Employees/Create`, MVC proxy `/HCM/Employees/drafts/api...`, legal-entity helper retention, and Gateway-only forwarding recorded; `Diten.Web` build passed with 0 warnings / 0 errors; focused Vitest passed 13 tests. |
| HCM startup `/health` | Closed. Startup blocker fixed; HCM bound on `5060`; `/health` returned 200. |
| Unauthenticated browser smoke | Closed. Frontend route was reachable, redirected unauthenticated users to login, login page loaded, no console errors/warnings were captured, and no direct browser calls to service ports were observed. |
| Final authenticated browser replay | Closed. Create, save/update with ETag, reload, all four references valid, review `reviewed`, `blockingReasons=[]`, no out-of-scope controls, no direct service-port references, and no console errors. |

### Runtime Sign-Off Status

Authenticated tenant browser smoke is closed for the P2 draft/reference-validation slice. Runtime sign-off remains limited to this slice and must not be used to add submit, approval, activation, evidence/export/status/DQ behavior, direct service-port calls, or broad seed/test data outside a later approved scope.

## P2 Scope Containment Baseline

Prompt ID: `MOD0251-OUT-OF-SCOPE-LIFECYCLE-REWORK-M1`.

Containment verdict: `PASS`.

The contained implementation baseline after the rework is:

- P2 draft create/save/reload/validate/review remains preserved.
- Submit for approval is controlled-blocked and does not start workflow.
- Workflow decision consumption is controlled-blocked and cannot mutate lifecycle state.
- Workflow start client is not runtime-registered for MOD-0251.
- Active employee materialization and `employee.activated` emission are not present in the runtime path.
- Status/evidence/audit placeholder UI is removed from the detail surface.
- Registry export affordance is removed pending governed export/masking contract closure.
- Gateway remains unchanged; backend route containment is the active control.

### Next-Sequence Decision Table

| Sequence | Status | Owner / Dependency | Required Decision Before Runtime Work |
|---|---|---|---|
| P2 runtime smoke closure | CONDITIONAL PASS; closed only for Create Draft / reference-validation | MOD-0251 / HCM | Preserve draft create/save/reload/reference-validation/review only. Negative submit/workflow proof is test/source-backed rather than browser-fetch-backed and does not authorize lifecycle work. |
| Registry read-only sequence | Deferred, not P2 support | MOD-0251 + MOD-0314 | Approve exact search/list DTO, non-sensitive columns, RBAC, tenant isolation, empty/error/forbidden states, no export/status/evidence/actions, and runtime smoke scope. |
| Detail read-only sequence | Deferred, not P2 support | MOD-0251 + MOD-0314 | Approve exact detail projection, sensitive-field policy, read-only affordance rules, no status/evidence/audit placeholders, and runtime smoke scope. |
| Lifecycle contract authorization | Deferred | MOD-0251 + HCM product owner | Approve exact submit, approval, activation, persistence, audit, idempotency, and rollback contract before code changes. |
| MOD-0023 callback-source authorization closure | Deferred blocker | MOD-0023 Workflow | Define authenticated callback/event source, replay/idempotency, tenant validation, and allowed decision payloads before workflow decisions are consumed. |
| `employee.created.v1` event contract closure | Deferred blocker | MOD-0251 event owner + downstream HCM consumers | Close event name, schema, safe payload, outbox behavior, replay/idempotency, and consumer expectations before any employee-created/activation event is emitted. |
| MOD-0314 masking contract closure | Deferred blocker for sensitive reads/export | MOD-0314 HR Governance & Sensitive Access Controls | Close masking/deny policy evaluation and response shaping before sensitive detail, registry, or export fields are enabled. |
| Evidence/retention contract closure | Deferred blocker | MOD-0028 Documentation, MOD-0031 Evidence Linking, MOD-0030 Records/Retention | Close evidence attachment, evidence-link metadata, retention/legal-hold, and failure behavior before evidence controls or evidence-gated activation are enabled. |
| Export/masking contract closure | Deferred blocker | MOD-0251 + MOD-0314 + MOD-0021 Audit | Close governed export limits, masking, audit requested/completed events, file/job ownership, and retention before export affordances or endpoints are enabled. |
| Data-quality queue sequence | Deferred | MOD-0251 + data governance owner | Approve queue entity, duplicate policy, assignment/resolution workflow, audit, and UI scope before queue runtime behavior is added. |

## P4 Employee Registry Read-Only Scope Contract

Prompt ID: `MOD0251-P4-REGISTRY-READ-SCOPE-CONTRACT-M1`.

This module pack authorizes governance planning only for the later `MOD0251-P4-REGISTRY-READ-M1` implementation prompt. It does not authorize runtime changes by itself and does not make Employee Registry part of P2 evidence.

### Registry Purpose and Boundary

Employee Registry is a tenant-shell, read-only DataTable for authorized HR users to search and scan approved employee records. It is not lifecycle execution, not draft review, not Employee Detail editing, not export, and not a status/evidence/audit panel.

First registry read scope must exclude:

- submit, approval/rejection, activation, and workflow decision processing;
- status mutation and status-history timeline;
- evidence upload/link/retention controls;
- audit panel/query display;
- export/download/print/DataTables local export;
- Data Quality Queue workflow;
- government identifier capture, tokenization, display, or search;
- Employee Detail row action unless a separate detail-read sequence is approved.

### Allowed Records

The first registry read implementation may show only approved registry-safe employee records:

- migrated or seeded read-only records explicitly approved for registry read;
- controlled local/test fixture records created and cleaned up through an approved fixture pattern;
- future Active employee records only after a later lifecycle/activation sequence exists and has its own runtime proof.

Draft-only records and P2 draft sessions must not surface through Employee Registry. If no approved registry-safe records exist, the registry must show an empty state.

### API Boundary

Target API stub: `GET /api/v1/hcm/employees`.

Required contract:

- Permission: `mod0251.employee.search`.
- Server-side tenant resolution only; client `TenantId` is ignored or rejected.
- `Response<T>` envelope.
- Pagination: `page`, `pageSize`, max page size `100`.
- Filters: `search`, `status`, `workerType`, `employmentType`, `organizationUnitId`, `positionId`, `legalEntityId`, `sensitivityLevel`.
- Sorting allowlist: `employeeNumber`, `displayName`, `workerType`, `employmentType`, `status`, `hireDate`, `updatedAt`.
- No government identifier, DOB, personal email, phone, evidence, audit payload, or private-note fields in response or searchable fields.
- Errors must be controlled and must not leak stack traces, secrets, tokens, connection strings, or internal implementation details.
- Search logging/audit uses safe metadata only; raw PII-like search terms remain prohibited unless a later MOD-0021/MOD-0314 contract permits redaction.

Registry row DTO may include only:

- `employeeId`;
- `employeeNumber`;
- `displayName`;
- `workerType`;
- `employmentType`;
- `organizationUnitId` / display;
- `positionId` / display;
- `legalEntityId` / display;
- `status`;
- `hireDate`;
- `sensitivityLevel`;
- `updatedAtUtc`;
- `actions.canView`;
- `actions.canExport=false`.

If MOD-0314 masking is not contract-closed, implementation must either return only non-sensitive fixture-safe values or fail closed for sensitive fields.

### UI Boundary

Target UI route: `/HCM/Employees`.

Required UI contract:

- Tenant layout: `_LayoutTenantShell`.
- Same-origin frontend proxy: `/HCM/Employees/api`.
- Browser JS must not call service ports `5056`, `5057`, `5059`, or `5060`.
- DataTable v2 with `data-dt-standard="v2"` and Compact registry/list structure.
- Columns: Employee Number, Display Name, Worker Type, Employment Type, Organization Unit, Position, Legal Entity, Status, Hire Date, Sensitivity, Last Updated.
- States: loading, empty, filtered empty, permission denied/filtering, controlled error, masked/unavailable sensitive field.
- No export button or hidden export affordance.
- No row detail action unless detail-read scope is separately approved.
- No status/evidence/audit placeholders.

### P4 Acceptance Criteria

| ID | Given | When | Then |
|---|---|---|---|
| REG-GWT-01 | An HR user has `mod0251.employee.search` and approved registry-safe records exist. | The user opens `/HCM/Employees` and searches. | A paged tenant-scoped list returns through Gateway-backed API with approved columns only. |
| REG-GWT-02 | A user lacks `mod0251.employee.search`. | The user opens or queries registry. | Server-side denial returns 401/403 or permission-filtered state with no rows/count leakage. |
| REG-GWT-03 | Tenant A and tenant B both have records. | Tenant B searches registry. | Tenant A rows, identifiers, and counts are not disclosed. |
| REG-GWT-04 | No approved registry-safe records exist. | The user opens registry. | Empty state is shown; no rows are synthesized from draft sessions. |
| REG-GWT-05 | Sensitive fields are governed by policy. | Registry data is returned. | Sensitive values are masked, omitted, or fail-closed; DOB/personal contact/government identifiers never appear. |
| REG-GWT-06 | Registry toolbar/actions render. | User reviews available commands. | Export is absent or disabled. |
| REG-GWT-07 | Detail-read scope is not approved. | Rows render. | Row detail action is absent or disabled. |
| REG-GWT-08 | Filters/sort/page are supplied. | Query executes. | Only allowlisted parameters are accepted; invalid input returns controlled validation errors. |

### P4 Test Expectations

- Backend query tests for pagination, filters, sort allowlist, and DTO shape.
- RBAC tests for `mod0251.employee.search`.
- Tenant isolation tests for rows and counts.
- Sensitive-field/masking tests proving no DOB, personal email, phone, government identifier raw/token/hash, evidence ids, or audit payloads are returned.
- Empty-state tests proving draft sessions do not appear in registry results.
- Frontend tests for loading, empty, filtered empty, error, permission-denied, and absent export/detail/lifecycle controls.
- Gateway route test only if gateway scope is approved in the implementation prompt.
- No lifecycle, activation, export, or Employee Detail dependency.

## Given / When / Then Acceptance Criteria

These scenarios are acceptance-contract closure only. They do not create MOD-0251 behavior tests, employee implementation, gateway routes, migrations, or approval to develop. Every scenario is aligned to one of: `First approved draft/reference-validation slice`, `Deferred R1 / activation sequence`, or `Governance blocker`.

### First Approved Draft / Reference-Validation Slice

| ID | Scenario | Given | When | Then |
|---|---|---|---|---|
| GWT-A01 | Draft creation | An authorized HR user has `mod0251.employee.create_draft`, an approved HCM service/API contract exists, audit is available, and tenant context is resolved server-side. | The user starts a Create Employee draft. | A tenant-scoped employee draft session is created with no Active employee record yet; client-provided `TenantId` is ignored/rejected; `employee_draft.created` is audited with safe payload only. |
| GWT-A02 | Draft update/save | An employee draft exists for the same tenant and the user has `mod0251.employee.create_draft`. | The user saves legal profile and employment draft step data with a current ETag. | The draft is saved, version/ETag advances, incomplete activation-required fields remain visibly incomplete, and `employee_draft.updated` is audited without full PII payload. |
| GWT-A03 | Person reference validation | A draft has a selected `PersonId` and the approved MOD-0288 person lookup-validation path is available. | The user validates or reviews the draft. | The person reference is confirmed as same-tenant and referenceable; unresolved, tenant-mismatched, inactive, or unavailable person validation blocks draft review/submit readiness without creating a person. |
| GWT-A04 | Organization-unit reference validation | A draft has an organization-unit reference and the approved Platform Gateway route is available. | The user validates or reviews the draft. | The organization-unit reference is confirmed as same-tenant/referenceable; unresolved, stale, tenant-mismatched, or unavailable provider responses block draft review/submit readiness. |
| GWT-A05 | Position reference validation | A draft has a position reference and the approved Platform Gateway route is available. | The user validates or reviews the draft. | The position reference is confirmed as same-tenant/referenceable; unresolved, stale, tenant-mismatched, or unavailable provider responses block draft review/submit readiness. |
| GWT-A06 | Legal-entity reference validation | A draft has a legal-entity/company reference and the approved MDM Gateway route is available. | The user validates or reviews the draft. | The legal entity is confirmed as referenceable through MDM lookup-validation; unresolved, inactive, tenant-mismatched, or unavailable provider responses block draft review/submit readiness. |
| GWT-A07 | Draft review state | A draft has saved legal/profile and employment draft steps plus validated references. | The user opens Review Draft. | MOD-0251 returns a reviewable persisted draft shape with validation summary and no submit/approval/Active transition controls in the first approved slice. |
| GWT-A08 | Reload draft persistence | A draft was created, saved, and reference-validated. | The user reloads the wizard/review page or the service restarts. | Draft data, step status, validation summary, ETag/version, and safe audit references remain retrievable from persistence without creating an Active employee. |
| GWT-A09 | Tenant isolation | Draft data exists in tenant A. | A user from tenant B requests the draft or review state. | MOD-0251 returns 404/fail-closed or an empty tenant-scoped result; no cross-tenant identifiers or counts are disclosed. |
| GWT-A10 | Server-side RBAC denial | A user lacks the required MOD-0251 permission for draft create, save, validate, review, or reload. | The user attempts the protected draft/reference action. | The server denies with 403 or a controlled denial shape, and `employee.access_denied` is audited where audit contract requires it. |
| GWT-A11 | Sensitive field masking/denial | A user can access draft/review data but lacks sensitive-field permission or MOD-0314 policy clearance. | The user opens draft review or requests sensitive fields. | Sensitive fields are masked/omitted or the request is denied; raw government identifiers are never returned; sensitive read/denial is audited. |
| GWT-A12 | Audit draft write success/failure | A user performs draft create, draft update, reference validation, or review-state persistence. | Audit emit succeeds or fails. | On success, the draft write commits with safe audit payload; on audit failure for critical draft writes, the write is blocked or rolled back per contract and the user receives a controlled failure. |
| GWT-A13 | ETag conflict | A draft section was changed after the user loaded it. | The user saves with a stale ETag. | Save is rejected with 409/conflict, current version can be reloaded, and `employee.conflict` is audited without overwriting newer data. |

### Governance Blocker Scenarios

| ID | Scenario | Given | When | Then |
|---|---|---|---|---|
| GWT-B01 | HCM scaffold without broader employee lifecycle approval | `services/Diten.HcmService` exists and P2 draft/reference-validation runtime is implemented, but full MOD-0251 employee lifecycle implementation is not approved. | Any prompt attempts employee Active-state entities/APIs, lifecycle persistence, migrations, gateway routes, frontend registry/detail lifecycle pages, permission seeds, audit wiring, or behavior tests outside the approved P2 slice without separate approval. | Work is blocked outside P2; MOD-0251 remains limited to draft/reference-validation and must not be placed in Platform, Auth, MDM, DevEnablement, or EnterpriseStrategy. |
| GWT-B02 | Missing MOD-0023 workflow contract | MOD-0023 workflow start/decision contract is unavailable, mock-only, disabled, or ambiguous. | A user attempts submit/approval processing. | Submit/approval is disabled or blocked with controlled error; employee remains Draft/Pending as applicable and no local approval engine is invented. |
| GWT-B03 | Missing MOD-0031 evidence contract | MOD-0031 evidence link contract is unavailable. | The wizard reaches Evidence step or policy requires activation evidence. | If evidence is not required for the first slice, the step is disabled/deferred; if policy requires evidence, submit/approval is blocked until evidence contract is executable. |
| GWT-B04 | Missing MOD-0314 masking contract | MOD-0314 masking/sensitive access policy contract is unavailable. | A user requests sensitive registry/detail fields. | Sensitive reads are denied or non-sensitive-only shapes are returned; no inferred masking policy is implemented. |
| GWT-B05 | Missing tokenization/security contract | Tokenization/security service ownership or API is unavailable. | A user attempts to capture government identifier data. | Government identifier capture is disabled; raw government identifiers are not stored, logged, audited, or returned. |
| GWT-B06 | Unresolved MOD-0299 identity conflict | MOD-0299 registry/Blueprint reconciliation remains unresolved. | MOD-0251 needs assignment workflow/orchestration dependency. | Assignment workflow dependency is deferred; MOD-0251 may store only its employee-side assignment snapshot and must not bind to executable MOD-0299 contracts. |
| GWT-B07 | Missing or unavailable MOD-0288 person validation path | Same-tenant person reference lookup-validation, Gateway route, or frontend proxy/picker validation is unavailable or ambiguous. | A user tries to create or submit an employee draft requiring `PersonId`. | Employee creation/submission is blocked with controlled validation/dependency error; MOD-0251 does not create or own person directory records and never accepts Auth `UserId` as a substitute. |

### Deferred R1 Sequence Scenarios

| ID | Scenario | Given | When | Then |
|---|---|---|---|---|
| GWT-D01 | Evidence attach integration | MOD-0028, MOD-0031, and MOD-0030 contracts are not closed for MOD-0251. | A user attempts to attach evidence. | Evidence upload/link controls remain disabled/deferred; evidence attach is implemented only in a later approved evidence sequence. |
| GWT-D02 | Termination/status-change | Employee is Active and a user attempts termination, suspension, rehire, or non-activation status change. | The status-change flow is requested before its R1 sequence is approved. | Flow is not implemented in the first approved draft/reference-validation slice; it remains blocked/deferred pending workflow/evidence/retention contracts. |
| GWT-D03 | Export | A user attempts restricted employee export before export platform and masking contracts are closed. | Export is requested. | Export remains disabled/deferred; no unbounded or unmasked export is produced. |
| GWT-D04 | Data Quality Queue full workflow | Duplicate/data-quality cases may be identified during create/submit. | A user attempts assignment, resolution, merge, or queue workflow operations before P10. | Full queue workflow remains deferred; only activation-slice duplicate detection/blocking or case creation may be represented by contract. |
| GWT-D05 | Self-service employee profile | Employee self-service/MOD-0319 is not approved for R1 first slice. | An employee attempts self-service profile editing. | Self-service edit is unavailable/deferred; MOD-0251 does not expose employee-owned edit surfaces in the first approved draft/reference-validation slice. |
| GWT-D06 | Submit for approval | A complete reviewed draft exists but executable MOD-0023 workflow contract is unavailable. | The user attempts Submit for Approval before the activation sequence is approved. | Submit is unavailable/blocked; no MOD-0023 workflow is started, no `employee.submitted_for_approval` audit event is emitted, and no Pending Approval or Active employee state is created. |
| GWT-D07 | Approval decision / Active transition | A draft or future submitted employee exists but executable MOD-0023 decision contract is unavailable. | MOD-0251 receives or attempts to process an approval/rejection decision before the activation sequence is approved. | Decision processing and Active transition are blocked; no `employee.approved`, `employee.rejected`, `employee.created`, status-history activation row, or employee-created outbox event is produced. |

### Acceptance Category Summary

| Category | Scenario IDs | Status |
|---|---|---|
| First approved draft/reference-validation slice | GWT-A01 through GWT-A13 | P2 implemented and browser-validated for create, save/update with ETag, reload, all four references valid, and non-submit review. |
| Governance blocker | GWT-B01 through GWT-B07 | Must fail closed until each blocker is resolved or formally de-scoped. |
| Deferred R1 / activation sequence | GWT-D01 through GWT-D07 | Explicitly out of first approved draft/reference-validation slice; requires later approved prompts. |

## Acceptance Criteria

- DCP-002 validation proves `MOD-0251` / `Core HR / Employee Master`.
- Domain scaffold exists under `execution/domains/human-capital-management/**`.
- Module pack status is `approved` with `approval_status: conditionally-approved-for-draft-reference-validation-slice`.
- Service owner is explicitly `Diten.HcmService`; no MOD-0251 employee business implementation may start from the scaffold alone.
- P0 inspection findings are recorded with verdict `BLOCKED` for approval/implementation.
- Service topology, gateway, frontend, dependency contract, RBAC/audit, and MOD-0299 reconciliation blockers are documented.
- MOD-0251 SoR ownership is explicit for Employee, Employment Record, and Job Assignment / Employment Assignment Snapshot.
- MOD-0299 and MOD-0288 boundaries are explicit.
- Dependencies are split into Blueprint Dependency Gate and Additional Implementation Dependencies.
- First approved runtime slice is draft/reference-validation only; submit, approval, Active transition, and employee-created event/outbox behavior are deferred.
- Gateway and frontend service-port constraints are documented.
- Sensitive/tokenization fail-closed rules are documented.
- Given/When/Then acceptance criteria cover first approved draft/reference-validation slice, governance blockers, and deferred R1/activation sequences.

## Test Expectations

No runtime tests are created by this governance reconciliation prompt.

Current and future implementation expectations:

- P2 focused backend/frontend tests and browser replay are the only completed runtime evidence for this approved slice.
- Frontend DataTable verifier applies only after registry/list pages are approved.
- Runtime smoke only after the approved implementation slice exists; activation smoke remains deferred until MOD-0023 activation is approved.
- Security/tenant tests for unauthorized access, tenant mismatch, masking, audit denial, and tokenization-disabled flows.

## Ready-for-dev Checklist

- [x] User review/promotion recorded for the draft/reference-validation slice.
- [x] Conditional promotion for draft/reference-validation slice recorded.
- [ ] Activation/full-lifecycle promotion recorded.
- [x] HCM service boundary selected for planning as `services/Diten.HcmService`.
- [x] HCM service scaffold created at `services/Diten.HcmService`; no MOD-0251 employee business implementation was created.
- [x] Future HCM port `5060` accepted as the approved local downstream port for `Diten.HcmService`.
- [x] MOD-0299 assignment workflow/orchestration explicitly de-scoped from the MOD-0251 first approved draft/reference-validation slice.
- [ ] MOD-0299 registry conflict reconciled by Enterprise Architect approval.
- [x] MOD-0288 person/reference contract confirmed for same-tenant employee creation through backend + Gateway + frontend proxy/picker, with frontend Vitest execution caveat.
- [x] Organization-unit provider decision approved for MOD-0251 employment validation: MOD-0288 / Platform organization-unit runtime.
- [x] Position provider decision approved for MOD-0251 assignment snapshot validation: MOD-0288 / Platform position runtime.
- [x] Legal entity/company provider decision approved for R1 MOD-0251 employment validation: MDM legal-entity lookup-validation unless a later Blueprint/domain decision overrides it.
- [x] Gateway/reference ingress plan confirmed for MOD-0288 person route through `/api/v1/platform/persons` and `/api/v1/platform/persons/{everything}`.
- [x] Frontend person picker/proxy exists through `/Platform/PersonReferences/api`, `window.PersonReferenceApi`, and `window.PersonReferencePicker`, with frontend Vitest execution caveat.
- [x] Gateway/reference ingress confirmed for non-person MOD-0288/MDM reference validation paths: organization units, positions, and legal entities.
- [x] Position assignment workflow is explicitly de-scoped from first approved draft/reference-validation slice.
- [x] Workflow approval de-scoped from first approved draft/reference-validation slice.
- [ ] MOD-0023 workflow contract executable.
- [ ] Activation transition approved for implementation.
- [x] DTO/API/validation contract for draft/reference-validation slice closed.
- [ ] Submit/approval/activation contract approved.
- [x] Activation submit/approval remains blocked unless executable MOD-0023 workflow contract exists.
- [ ] MOD-0031 evidence activation contract executable.
- [x] Evidence integration deferred from first slice when `activation_evidence_required = false`; evidence-required activation remains blocked.
- [ ] MOD-0028 evidence attachment contract executable.
- [x] MOD-0028 evidence attachment deferred from first slice; no upload/link UI or orphan document-link metadata.
- [ ] MOD-0030 retention/legal-hold contract executable.
- [x] MOD-0030 retention/legal-hold/destructive lifecycle behavior deferred from first slice; destructive/legal-hold-sensitive flows blocked.
- [ ] MOD-0314 masking contract executable for advanced sensitive flows.
- [x] MOD-0314 unavailable means sensitive reads are denied or return non-sensitive/masked shapes only.
- [ ] Tokenization/security contract identified for government identifier capture.
- [x] Tokenization unavailable means government identifier capture is disabled and raw identifier storage is prohibited.
- [x] MOD-0251 role-permission matrix added.
- [x] MOD-0251 audit payload contract added.
- [ ] MOD-0251 permission seed implementation completed after HCM service scaffold approval.
- [ ] MOD-0251 audit wiring implementation completed after HCM service scaffold approval.
- [x] G/W/T acceptance criteria added.
- [ ] HCM domain is added to any required global registries/AGENTS updates in a separate governance prompt if needed.
- [ ] P0 inspection confirms route, service, persistence, migration, test, frontend, RBAC, audit, workflow, evidence, masking, and tokenization contracts.
- [x] P1 draft/reference-validation contract closure produces API stubs, DTO stubs, validation rules, persistence rules, audit payload contracts, fail-closed rules, and future contract-test plan.
- [x] HCM implementation prompt allowed after conditional promotion, limited to draft/reference-validation boundary only.
- [x] P2 backend draft/reference-validation build and focused tests passed.
- [x] P2 HCM draft Gateway route ownership assigned to `integration-agent` and route/build evidence recorded.
- [x] P2 frontend draft wizard/proxy build and focused Vitest passed.
- [x] P2 HCM startup blocker isolated and fixed; HCM `/health` returned 200 on port `5060`.
- [x] P2 unauthenticated browser smoke passed with controlled login redirect, no console errors/warnings, and no direct browser calls to service ports.
- [x] P2 conditional handback recorded for draft/reference-validation slice.
- [x] P2 authenticated browser smoke completed for draft create/save/reload/reference validation/review: tenant shell visible, `canCreateDraft=true`, all four references valid, review `reviewed`, `blockingReasons=[]`, no out-of-scope controls, no direct browser service-port references, and no console errors.
- [x] Frontend shell and route conventions are confirmed for the approved draft/reference-validation slice.
- [ ] DataTable Golden Reference compact applicability is accepted for registry/list pages.
- [ ] Government identifier capture remains disabled until tokenization/security contract is confirmed.

## Implementation Notes

- This module is tenant-shell HCM, not Platform Admin.
- Backend service scaffold now exists at `services/Diten.HcmService`; P2 draft/reference-validation runtime is implemented there. Full MOD-0251 employee lifecycle implementation remains blocked pending later approval and contract closure.
- Repo packaging/commit remains unsafe until the no-valid-`HEAD` / all-untracked workspace condition is resolved.
- Existing services include Auth, Platform, Platform.Common, Platform.Contracts, DevEnablement, EnterpriseStrategy, and MDM; none is an HCM owner for MOD-0251.
- `MOD-0299` appears in the current registry as a deprecated SaaS Billing alias, while the Blueprint v7/spec use `MOD-0299 Position & Organization Assignment`. This must be reconciled before implementation depends on MOD-0299.
- Registry currently does not contain an active `MOD-0251` row, but the validator proves the ID/name from the Blueprint workbook and no active collision was detected.

## Follow-up Items

Development control plan:

- P0 inspection.
- P1 contracts.
- P2 RBAC/audit.
- P3 draft backend.
- P4 registry read.
- P5 wizard UI.
- P6 approval activation.
- P8 evidence.
- P9 status change.
- P10 Data Quality Queue.
- P11 export.
- P12 closure.

Do not start any follow-up outside the approved draft/reference-validation boundary until a later pack update explicitly approves that scope.
