---
id: MOD-0230
name: Case Intake & Triage
domain: pharmacovigilance
service: TBD
shell: tenant
golden_reference: compact
entity_base: EntityBase
status: draft
owner: TBD
branch: feature/pvg/mod-0230-case-intake-triage
started: 2026-08-04
target: TBD
form_field_count: 16
---

# MOD-0230 - Case Intake & Triage

> Draft planning artifact only. This module pack does not authorize runtime work. DCP-004 remains `draft`;
> production implementation stays blocked until DCP-004 is `approved` / `ready-for-execution` and this
> module pack is `approved` / `ready-for-dev`.

> DCP-002 gate (2026-08-04): `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0230 --name "Case Intake & Triage"` -> `OK MOD-0230: proven against Blueprint/registry.`

## Module Summary

MOD-0230 Case Intake & Triage is the first PVG member module in DCP-004. Its purpose is to define the
regulated Pharmacovigilance intake baseline: Safety Case intake records, intake artifacts, triage state,
routing decisions, and the evidence-pack boundary required before downstream Case Processing, MedDRA Coding,
and Signal Management planning can proceed.

Blueprint context:

| Field | Value |
|---|---|
| Domain / Landscape | Regulated Life-Sciences Extensions |
| Suite / Platform | Pharmacovigilance (PV) |
| Capability Group | Safety Case Operations |
| Blueprint Wave | W-3 |
| Placement | Domain App (Regulated Workflow) |
| Delivery Outcome / Value | PV intake baseline; controlled routing |
| Soft Pages | PV Case Inbox; Intake Forms; Triage Rules; Evidence Pack |
| Minimum Integration Contract | REG-PV-BASE |
| SoR Primary Object | Safety Cases |
| AI Enablement Tier / Risk | Governed-AI / High |

## Ownership and Boundaries

In scope for this draft:

- Safety Case intake contract boundary.
- Intake artifact contract boundary.
- Triage state and routing decision contract boundary.
- Evidence-pack handoff boundary.
- W-3A0 dependency map and production blockers.
- Future module-pack readiness questions.

Out of scope for this draft:

- Runtime service scaffold, frontend UI, gateway route, database collection, seed data, migration, background job, or permission seed implementation.
- W-3A0 foundation remediation development.
- Full Case Processing, MedDRA Coding, Signal Management, reporting/submission, or PV quality runtime.
- AI summarization, extraction, recommendation, or routing implementation.

## Owned Objects

Planned logical objects, not runtime classes yet:

| Object | Ownership | Runtime status |
|---|---|---|
| Safety Case Intake | MOD-0230 SoR for intake-stage Safety Case creation boundary | Planned only |
| Intake Artifact | MOD-0230 SoR for intake-stage submitted artifacts and source references | Planned only |
| Triage State | MOD-0230 SoR for initial triage status and routing outcome | Planned only |
| Routing Decision | MOD-0230 SoR for controlled routing decision metadata | Planned only |
| Evidence Pack Boundary | MOD-0230 produces intake evidence-link requirements; MOD-0031 owns evidence-link service | Planned only |

Future runtime objects, endpoints, controllers, commands, queries, DTOs, frontend routes, and permissions are
intentionally not authorized by this draft. They must be finalized after the open decisions in this pack close.

## Entity Fields

Create/edit user-entered field count recorded for draft planning: `16`.

Golden Reference decision: `compact`, because the create/edit form has more than 8 user-entered fields and
regulated intake requires separate Create/Edit/Details review surfaces rather than Index-hosted offcanvas panels.

Excluded from field count: `Id`, `TenantId`, audit fields, correlation id, workflow instance id, created/updated
metadata, system-generated case number, and computed SLA/status timestamps.

| Field | Required | Sensitivity class | User-entered | Notes / blocker |
|---|---|---|---|---|
| IntakeChannel | Yes | public-metadata | Yes | Allowed values / source of options still requires approval. |
| SourceType | Yes | public-metadata | Yes | Allowed source taxonomy still requires approval. |
| SourceReference | No | confidential | Yes | External source ID policy must align with Blueprint MOD-0040 / TRACE-BUNDLE. |
| ReceivedAtUtc | Yes | regulated-safety | Yes | Must be UTC-normalized; client timestamp trust policy still requires approval. |
| ReporterType | Yes | public-metadata | Yes | Allowed values still require approval. |
| ReporterContactSummary | No | PII | Yes | Requires MOD-0019 masking / row-field policy before runtime. |
| PatientSubjectCode | No | PHI | Yes | Requires MOD-0019 masking / row-field policy before runtime. |
| EventOnsetDate | No | PHI | Yes | Requires MOD-0019 masking / row-field policy before runtime. |
| AdverseEventNarrative | Yes | PHI | Yes | Raw narrative prohibited in logs/traces/audit payloads. |
| SuspectProductText | No | regulated-safety | Yes | Terminology/reference policy still requires approval. |
| Seriousness | Yes | regulated-safety | Yes | Allowed values still require approval. |
| IntakePriority | Yes | regulated-safety | Yes | Priority taxonomy and SLA linkage still require approval. |
| TriageOutcome | Yes | regulated-safety | Yes | Allowed states and transition rules require Workflow/Inbox contract. |
| TriageReason | No | PHI | Yes | Raw reason text prohibited in logs/traces/audit payloads. |
| RouteTargetQueue | Yes | confidential | Yes | Requires Workflow/Inbox route target contract and permission-filtered visibility. |
| EvidenceLinkReferences | No | confidential | Yes | Requires MOD-0031 Evidence-Link contract; no fake evidence pack fallback. |

Every field included in create/edit/list/detail/export surfaces must later receive masking behavior, row/field
access rule, audit payload rule, evidence-link rule, and fail-closed tests before `ready-for-dev`.

## Repo Scope

Authorized by this draft:

- `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md`

Future only, blocked until DCP-004 and this module pack pass approval gates:

- PVG runtime service path - planned future dedicated `Diten.PvgService`; frontmatter `service` remains `TBD`
  until explicit service scaffold approval.
- PVG frontend paths - planned tenant MVC surface under
  `frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/**`.
- PVG gateway route paths - proposed under `/api/v1/pharmacovigilance/case-intake-triage`; integration-agent-owned
  and not authorized by this draft.
- PVG tests - TBD after service/frontend boundaries are approved.

## Protected Paths

- `.antigravity/**`
- `services/**`
- `frontend/**`
- `gateway/**`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- Other domain module packs and runtime internals unless explicitly authorized by the user.

This draft does not modify DCP-004, services, frontend, gateway, runtime code, or any other module-pack file.

## Dependencies

W-3A0 dependencies are production blockers, not waived:

| Dependency | Owning module / source | Status for MOD-0230 |
|---|---|---|
| REG-PV-BASE | DCP-004 minimum integration contract | BLOCKER |
| SSO + RBAC/ABAC | MOD-0018 RBAC / permissions plus Platform/Auth foundations | BLOCKER |
| PHI/PII masking hooks | MOD-0019 Data Masking & Row/Field Security | BLOCKER |
| AuditEvent v1 | MOD-0021 Audit Trail Service | BLOCKER |
| Workflow/Inbox v1 | MOD-0023 Workflow Designer | BLOCKER |
| Evidence-Link | MOD-0031 Evidence Linking Service | BLOCKER |
| TRACE-BUNDLE: canonical ID, Correlation-ID, trace stitching, regulated error model | Blueprint MOD-0040 / platform trace standards | BLOCKER |
| OTel / operational telemetry | Platform observability foundations | BLOCKER |
| DCP-004 | PVG Urgent W-3 Development Block | Currently `draft`; execution not authorized |

MOD-0004 Metric & Semantic Registry and MOD-0063 Data Warehouse / Lakehouse are not direct MOD-0230 runtime
blockers unless this module's approved scope emits signal analytics, semantic metric IDs, or data-product outputs.
They remain downstream DCP-004 / MOD-0234-facing gates for Signal Management. If MOD-0230 later adds signal
analytics or data-product output, the pack must be revised to promote the relevant MOD-0004/MOD-0063 contracts
from downstream gates to direct MOD-0230 blockers.

### Required Interface Contracts Before `ready-for-dev`

MOD-0230 must reference concrete, owner-approved contracts for each dependency below before implementation is
allowed. This draft records the required contracts only; none are closed here.

| Owner | Required contract for MOD-0230 | Required MOD-0230 decision | Status |
|---|---|---|---|
| MOD-0018 RBAC / permissions | canonical permission keys, seed/grant ownership, actor context, tenant authorization context, optional data-scope shape | actor roles and permission matrix for read/create/update/triage/route/archive/export; delete and bulk-delete explicitly excluded | OPEN / BLOCKER |
| MOD-0019 masking / row-field security | field sensitivity vocabulary, masking/omit/deny behavior, row-scope and field-scope evaluation, unavailable-policy behavior | per-field sensitivity matrix and fail-closed behavior for list/detail/create/update/export/audit | OPEN / BLOCKER |
| MOD-0021 AuditEvent v1 | append/event shape, safe metadata envelope, redaction rules, critical audit failure policy, correlation propagation | audited operations, payload allow-list, failure behavior when audit append/outbox is unavailable | OPEN / BLOCKER |
| MOD-0023 Workflow/Inbox v1 | transition gate or inbox handoff API/event, assignment semantics, routing state, fail-closed behavior | triage states, routable states, route targets, transition reason codes, blocked/allowed behavior | OPEN / BLOCKER |
| MOD-0031 Evidence-Link | object reference shape, link/query API, evidence requirement/completeness rule, evidence-pack boundary | whether artifacts require evidence links at create, triage, route, and downstream handoff | OPEN / BLOCKER |
| Blueprint MOD-0040 / TRACE-BUNDLE | canonical/external ID semantics, `X-Correlation-Id`, trace stitching, regulated error model | generated/manual intake ID policy, external source ID policy, correlation propagation, error reason-code policy | OPEN / BLOCKER |

### MOD-0040 / MOD-0288 Identity Clarification

- Use **Blueprint MOD-0040 / TRACE-BUNDLE** for canonical ID, external ID, correlation header, trace stitching, and
  regulated error-model decisions.
- Use **MOD-0288 Organization, Person & Position Directory** only if MOD-0230 routing, assignment, or search
  explicitly consumes organization/person/position references.
- Do **not** reference legacy deprecated repo `MOD-0040` as the organization/person source. In this repo,
  organization/person/position ownership is canonicalized to MOD-0288.

## Runtime Constraints

- No runtime service scaffold is authorized.
- No service port is reserved.
- No gateway route is authorized.
- No database collection, index, migration, seed, or job is authorized.
- No UI shell or DataTable page is authorized.
- `Diten.PvgService` cannot be created until DCP-004 is `approved` / `ready-for-execution` and the active member module pack is `approved` / `ready-for-dev`.
- Recommended future service boundary is a dedicated `Diten.PvgService` with a hybrid partner-aware integration
  posture. The service is expected to own the Diten-controlled intake contract, tenant workflow boundary, audit /
  evidence / workflow integration, and partner adapter boundary if a PV safety partner system is selected.
- `service` remains `TBD` in frontmatter until explicit service scaffold approval. This draft does not reserve a
  service port or create a service folder.
- `entity_base: EntityBase` remains correct for Diten-owned tenant records, projections, archive/void metadata, and
  outbox/audit-linked records under the future PVG boundary. Partner-native records remain outside the repo entity
  model unless an adapter contract later maps them.
- Future runtime must resolve tenant isolation, regulated data masking, audit, evidence links, workflow/inbox handoff, OTel, correlation ID, and error model before acceptance.
- Tenant-owned runtime data, if approved, must carry server-resolved `TenantId`; client payloads must not accept
  `TenantId`. Cross-tenant reads or mutations must return 404/empty result with no metadata leak.
- Any sensitive field whose MOD-0019 policy cannot be evaluated must fail closed: deny the field, omit/mask the
  field, or deny the operation according to the field matrix recorded for draft planning. No implementation may invent a permissive
  fallback.
- Raw PHI/PII, reporter identifiers, patient identifiers, source document content, free-text safety narratives,
  and unrestricted search/export payloads must not be written to logs, traces, metrics, or audit payloads.
- Delete policy is locked for this draft: no hard delete, no bulk delete, and no normal user delete. Archive/void
  may be introduced only after retention/legal-hold approval; it requires reason, actor, UTC timestamp,
  correlation id, and AuditEvent. Archive/void is blocked under legal hold.

## Layout & Shell Contract

`shell: tenant`

MOD-0230 is a tenant/domain operational workflow surface, not a platform-admin configuration module.

- Razor layout: every future `.cshtml` page must explicitly set `Layout = "_LayoutTenantShell";`.
- Future MVC route proposal: `/Pharmacovigilance/CaseIntakeTriage`.
- Future view root proposal: `frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/**`.
- Frontend API profile: same-origin MVC proxy profile. Browser JavaScript must call the MVC proxy surface, not call
  Gateway directly and never call a service port directly.

Frontend implementation remains blocked until DCP-004, this pack, service boundary, Gateway routing, and
W-3A0 production blockers are approved.

## Backend File Convention

`service: TBD`

Recommended future boundary: dedicated `Diten.PvgService` with a hybrid partner-aware integration posture.

- Do not host MOD-0230 inside `Diten.Platform`, `Diten.AuthService`, `Diten.DevEnablementService`, or
  `Diten.EnterpriseStrategyService`.
- If a buy/partner PV safety system is selected, `Diten.PvgService` should act as the controlled wrapper /
  orchestration layer for Diten tenant UI, intake contract, audit, evidence, workflow, correlation, and adapter
  semantics. It must not become a direct frontend-to-partner bridge.
- Internal build scope is limited to the Diten-controlled contract, tenant UI boundary, workflow/audit/evidence
  integration, and adapter layer after approval; it must not become a full standalone PV safety platform under
  MOD-0230.

If a PVG runtime service is later approved, backend implementation must follow the Golden Reference CQRS shape:

```text
Features/CaseIntakeTriage/
├── Commands/
├── Queries/
├── Handlers/CommandHandlers/
├── Handlers/QueryHandlers/
├── Validators/
└── CaseIntakeTriageModels.cs
```

Naming rules for future implementation:

- Commands: `CreateCaseIntakeTriageCommand`, `UpdateCaseIntakeTriageCommand`, `ArchiveCaseIntakeTriageCommand`, `VoidCaseIntakeTriageCommand`, `TriageCaseIntakeTriageCommand`, `RouteCaseIntakeTriageCommand` if the corresponding operations are approved.
- Queries: `GetCaseIntakeTriageListQuery`, `GetCaseIntakeTriageByIdQuery` if list/detail is approved.
- Handlers: `*Handler` only; no `CommandHandler`, `QueryHandler`, or `RequestHandler` suffix.
- Validators: `*Validator` only; no `CommandValidator` suffix.
- Forbidden future conventions: `DeleteCaseIntakeTriageCommand`, `BulkDeleteCaseIntakeTriageCommand`, DELETE endpoints, and bulk-delete endpoints.

This section is a future convention statement, not implementation authorization.

## Frontend File Contract

`golden_reference: compact`

The create/edit field count recorded for draft planning is 16, so MOD-0230 follows Golden Reference Compact:

- `Index.cshtml`.
- `Create.cshtml`.
- `Edit.cshtml`.
- `Details.cshtml`.
- `_Form.cshtml`.
- `_Filter.cshtml`.
- `_DataTable.cshtml`.
- `_IndexL10n.cshtml`.
- `CaseIntakeTriageIndex.cs`.
- `wwwroot/assets/js/Pharmacovigilance/CaseIntakeTriage/index.js`.
- `wwwroot/assets/js/Pharmacovigilance/CaseIntakeTriage/index.l10n.js`.
- `Resources/Views/Pharmacovigilance/CaseIntakeTriage/CaseIntakeTriageIndex.{lang}.resx`.

Compact must not include Slim-only `_CreateEditOffcanvas.cshtml` or `_DetailsQuickView.cshtml`.
No frontend files may be created until runtime gates are approved.

Future frontend API calls must use the same-origin MVC proxy profile. Direct browser-to-Gateway calls are not the
preferred profile for this regulated tenant surface; direct service-port calls are forbidden.

## Validation Rules

Field-level validation is blocked until the intake form fields are approved. Minimum validation topics that must
be resolved before `ready-for-dev`:

| Field | Required | Rule | DB-level | Pre-check | Sensitivity / fail-closed requirement |
|---|---|---|---|---|---|
| IntakeChannel | Yes | Controlled option set; no free text | TBD | Lookup/contract source TBD | unknown channel policy fails closed for routing decisions |
| SourceType | Yes | Controlled option set; no free text | TBD | Lookup/contract source TBD | unknown source policy fails closed for routing decisions |
| SourceReference | No | Max length and external ID policy TBD | TBD | Duplicate/source policy TBD | confidential; no raw sensitive source ID in logs/audit unless approved and redacted |
| ReceivedAtUtc | Yes | UTC value; source trust policy TBD | TBD | clock/source policy TBD | regulated-safety; server-side UTC normalization required |
| ReporterType | Yes | Controlled option set | TBD | Lookup/contract source TBD | public-metadata |
| ReporterContactSummary | No | Max length, masking, and storage minimization TBD | TBD | MOD-0019 dependency | PII; deny/mask/omit when field policy unavailable |
| PatientSubjectCode | No | Pseudonymous/minimum necessary identifier only | TBD | MOD-0019 dependency | PHI; no patient PHI returned or audited raw without approved policy |
| EventOnsetDate | No | Date-only or UTC policy TBD | TBD | MOD-0019 dependency | PHI; mask/omit by actor policy |
| AdverseEventNarrative | Yes | Max length, redaction/audit policy TBD | TBD | MOD-0019 / MOD-0021 dependency | PHI; raw free text prohibited in logs/traces/metrics/audit payloads |
| SuspectProductText | No | Max length and terminology/reference policy TBD | TBD | dependency TBD | regulated-safety; omit/deny if unapproved |
| Seriousness | Yes | Controlled option set | TBD | option source TBD | regulated-safety |
| IntakePriority | Yes | Controlled option set and SLA linkage TBD | TBD | option source TBD | regulated-safety |
| TriageOutcome | Yes | Proposed state set and transition rules TBD | TBD | MOD-0023 dependency | invalid/unapproved transition fails closed |
| TriageReason | No | Max length, redaction/audit policy TBD | TBD | MOD-0019 / MOD-0021 dependency | PHI; raw reason prohibited in logs/traces/metrics/audit payloads |
| RouteTargetQueue | Yes | Proposed queue/route target list | TBD | MOD-0023 dependency | confidential; visibility must be permission-filtered |
| EvidenceLinkReferences | No | Evidence object reference shape TBD | TBD | MOD-0031 dependency | confidential; no fake evidence pack when evidence service unavailable |

### Option-Set Ownership

Option-set ownership recorded for draft planning for future validation and UI contracts:

| Option set | Owner / source | Ownership type |
|---|---|---|
| IntakeChannel | PVG-controlled lookup | Static controlled PVG lookup |
| SourceType | PVG-controlled lookup, partner/source-system extensible | Static controlled PVG lookup with controlled extensions |
| ReporterType | PVG-controlled lookup | Static controlled PVG lookup |
| Seriousness | PVG/compliance-controlled lookup | Static controlled PVG lookup |
| IntakePriority | PVG lookup plus workflow/SLA policy owner | Workflow/reference-owned |
| TriageOutcome | MOD-0023 Workflow/Inbox transition contract plus PVG state model | Workflow-owned |
| RouteTargetQueue | MOD-0023 Workflow/Inbox queue registry, possibly backed by reference service | Workflow/reference-owned |

Static controlled PVG lookups may be planned as PVG-owned reference data only after the approved reference-data
publication path is selected. Workflow/reference-owned option sets must fail closed when the owning contract is
unavailable; no hardcoded UI fallback is allowed.

Every field included in the final create/edit/list/detail/export surface must have at least one matching test
expectation proving unauthorized, cross-tenant, missing-policy, and masking-denied behavior.

## Failure Path to Verify

Future implementation must verify at least these paths:

- **Missing required intake field**
  - Expected: 400 validation response; no intake record created.
- **Duplicate or conflicting intake identifier**
  - Expected: 409 or approved duplicate-handling response; no silent overwrite.
- **Unauthorized actor**
  - Expected: 401/403 according to policy; no data leakage.
- **Cross-tenant access**
  - Expected: 404 or approved forbidden behavior; no cross-tenant data returned.
- **Unmasked sensitive field**
  - Expected: request/response blocked or masked according to MOD-0019 contract.
- **Missing MOD-0019 policy for a sensitive field**
  - Expected: field omitted/masked or operation denied according to the field matrix; no permissive fallback.
- **Unauthorized field-level read**
  - Expected: non-sensitive envelope may return only if approved; restricted field is masked/omitted or request is denied.
- **Sensitive input appears in audit/log/trace**
  - Expected: test fails; raw PHI/PII/free text must not be persisted to audit payloads, logs, traces, metrics, or error details.
- **Evidence-link unavailable**
  - Expected: fail-closed or explicitly degraded behavior; no fake evidence pack.
- **Workflow/Inbox unavailable**
  - Expected: triage/routing transition blocked; no untraceable routing.
- **Audit sink unavailable**
  - Expected: regulated mutation blocked or queued according to approved audit contract; no unaudited mutation.
- **Correlation/trace context missing**
  - Expected: behavior follows Blueprint MOD-0040 / TRACE-BUNDLE decision; runtime must not create untraceable regulated state changes.
- **Delete/archive attempted before retention/legal-hold decision**
  - Expected: operation absent or denied; no regulated intake record is removed or hidden without approved retention/legal-hold policy.

## Authorization Convention

Permission prefix proposal for future tenant/domain implementation:

```text
pvg.case-intake-triage.read
pvg.case-intake-triage.create
pvg.case-intake-triage.update
pvg.case-intake-triage.triage
pvg.case-intake-triage.route
pvg.case-intake-triage.archive
pvg.case-intake-triage.export
```

Explicitly excluded permission keys:

```text
pvg.case-intake-triage.delete
pvg.case-intake-triage.bulk-delete
```

Initial role / permission matrix proposal:

| Role | read | create | update | triage | route | archive | export |
|---|---:|---:|---:|---:|---:|---:|---:|
| PVG Intake Agent | Assigned / own queue | Yes | Pre-triage only | No | No | No | No |
| PVG Triage Lead | Yes | Yes | Yes | Yes | Yes | Only after retention/legal-hold approval | No |
| PVG Safety Manager | Yes | Yes | Yes | Yes | Yes | Only after retention/legal-hold approval | Masked export only |
| PVG Compliance Auditor | Read-only | No | No | No | No | No | Masked export only |
| PVG System Integration | Approved intake contract only | Yes | No | No | No | No | No |

Open authorization decisions:

- Final actor role names, actor type mapping, and seed/grant ownership require MOD-0018 / AuthService approval.
- Archive permission is unusable until retention/legal-hold policy is approved.
- PHI/PII field-level authorization must align with MOD-0019 before runtime.
- Permission seed/grant ownership must remain with MOD-0018 / AuthService; MOD-0230 may only define/consume keys
  after the owning security contract is approved.
- Export is masked-only unless a later field policy approval explicitly permits more.

No permission seed is authorized by this draft.

## Gateway / API Routing Decision

Decision: Gateway route is **required for any future runtime**, but no route is authorized by this draft.

Proposed Gateway API route family for future implementation:

```text
/api/v1/pharmacovigilance/case-intake-triage
/api/v1/pharmacovigilance/case-intake-triage/{id}
/api/v1/pharmacovigilance/case-intake-triage/{id}/triage
/api/v1/pharmacovigilance/case-intake-triage/{id}/route
/api/v1/pharmacovigilance/case-intake-triage/{id}/archive
/api/v1/pharmacovigilance/case-intake-triage/export
```

Explicitly excluded route families:

```text
DELETE /api/v1/pharmacovigilance/case-intake-triage/{id}
/api/v1/pharmacovigilance/case-intake-triage/bulk-delete
```

Future route implementation must define:

- service/deployment owner;
- upstream API base path;
- downstream path;
- auth/correlation/error-model behavior;
- OPTIONS/CORS handling if applicable;
- integration-agent task for `gateway/Diten.ApiGateway/**/ocelot.json`.

Frontend consumption must use a same-origin MVC proxy profile for this tenant UI. Direct browser-to-Gateway calls
are not the preferred profile for MOD-0230; direct service-port calls from frontend remain forbidden.

## Acceptance Criteria

Acceptance criteria for this draft pack:

- [x] Pack exists at `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md`.
- [x] Status is `draft`.
- [x] Canonical name is exactly `Case Intake & Triage`.
- [x] DCP-002 preflight passed for MOD-0230.
- [x] W-3A0 dependencies are recorded as production blockers, not waived.
- [x] No runtime implementation is authorized.
- [x] Form field count recorded for draft planning as `16`.
- [x] Golden Reference resolved for draft planning as `compact`.
- [x] Shell resolved for draft planning as `tenant`.
- [x] Entity base recorded for draft planning as `EntityBase` for a future dedicated PVG service boundary.
- [x] Delete and bulk-delete are explicitly excluded; archive/void is blocked until retention/legal-hold approval.
- [x] Future service boundary recorded as dedicated `Diten.PvgService`; frontmatter `service` remains `TBD` until
      explicit scaffold approval.
- [x] Build/buy/partner strategy proposed and recorded as hybrid, partner-aware internal control wrapper.
- [x] Tenant MVC route, view root, same-origin MVC proxy profile, and Gateway route proposals are recorded.
- [x] Option-set ownership is proposed and recorded for the draft field model.

Acceptance criteria before any future implementation can start:

- [ ] DCP-004 is `approved` / `ready-for-execution`.
- [ ] This module pack is `approved` / `ready-for-dev`.
- [ ] `service` is resolved through explicit service scaffold approval; frontmatter currently remains `TBD`.
- [ ] W-3A0 dependencies are closed or explicitly satisfied by production-grade external contracts.
- [ ] Required interface contracts are concrete for MOD-0018, MOD-0019, MOD-0021, MOD-0023, MOD-0031, and
      Blueprint MOD-0040 / TRACE-BUNDLE.
- [ ] Validation rules, masking behavior, row/field access behavior, audit payload rules, evidence-link rules,
      gateway routing, and tests are fully specified from the draft field model and proposed option-set ownership.
- [ ] Delete/retention/legal-hold behavior is decided.
- [x] Build/buy/partner integration boundary proposed and recorded as hybrid, partner-aware internal control wrapper.

## Test Expectations

No runtime tests are expected for this draft because no runtime files are authorized.

Future implementation test expectations must include:

- DCP-002 identity proof remains valid.
- Backend build and unit/integration tests for the approved PVG service boundary.
- Tenant isolation and regulated-data masking tests.
- Per-field PHI/PII sensitivity, masking, row/field deny, and missing-policy fail-closed tests.
- Audit, correlation/TRACE-BUNDLE, evidence-link, workflow/inbox failure-path tests.
- Tests proving raw PHI/PII/free text is absent from logs, traces, metrics, audit payloads, validation errors, and
  regulated error responses.
- Frontend build and DataTable verifier only if frontend is approved and Slim/Compact is decided.
- Gateway route smoke only after integration-agent-owned route approval.

## Ready-for-dev Checklist

- [x] Required governance files read.
- [x] DCP-002 preflight passed.
- [x] Pack status is `draft`.
- [ ] DCP-004 promoted to `approved` / `ready-for-execution`.
- [ ] W-3A0 dependency owner/scope resolved or production-grade contracts accepted.
- [ ] MOD-0018 RBAC/permission contract and actor matrix resolved.
- [ ] MOD-0019 masking / row-field security contract resolved.
- [ ] MOD-0021 AuditEvent v1 append/redaction/failure contract resolved.
- [ ] MOD-0023 Workflow/Inbox v1 handoff/transition contract resolved.
- [ ] MOD-0031 Evidence-Link object/evidence-pack contract resolved.
- [ ] Blueprint MOD-0040 / TRACE-BUNDLE canonical ID, correlation, trace-stitching, and error-model contract resolved.
- [ ] `service` resolved.
- [x] Future service/deployment boundary recorded as dedicated `Diten.PvgService`; scaffold approval still required
      before frontmatter `service` changes.
- [x] `shell` resolved for draft planning as `tenant`.
- [x] `entity_base` recorded for draft planning as `EntityBase` for future dedicated PVG service boundary.
- [x] Create/edit user-entered fields defined.
- [x] Create/edit fields marked required/optional.
- [x] PHI/PII/sensitive-field matrix recorded for draft planning for every intake field.
- [x] `form_field_count` resolved for draft planning as `16`.
- [x] `golden_reference` resolved for draft planning as `compact`.
- [ ] Entity fields and validation rules fully specified.
- [x] Option-set ownership recorded for intake, source, reporter, seriousness, priority, triage outcome, and route queue.
- [ ] Authorization actor/role matrix approved.
- [ ] Retention, archive/void activation, and legal-hold policy approved.
- [x] Build/buy/partner integration boundary proposed and recorded as hybrid, partner-aware internal control wrapper.
- [ ] Gateway route implementation assigned to integration-agent if needed.
- [ ] Test expectations are concrete enough for implementation.

## Implementation Notes

- This pack is intentionally incomplete because it is a draft planning artifact.
- DCP-004 is still `draft`; this pack cannot be used to start runtime work.
- Readiness checkpoint recorded 2026-08-07: MOD-0230 is the first PVG candidate to move toward readiness, but it
  remains `draft` and is not `ready-for-dev`. It is blocked by DCP-004 not being `approved` /
  `ready-for-execution`, W-3A0 owner/scope/foundation closure or accepted production-grade external contracts,
  `service: TBD`, retention/legal-hold, archive/void activation, and concrete interface contracts for MOD-0018,
  MOD-0019, MOD-0021, MOD-0023, MOD-0031, and Blueprint MOD-0040 / TRACE-BUNDLE.
- Frontmatter decisions reconciled 2026-08-04: `shell: tenant`, `entity_base: EntityBase`,
  `form_field_count: 16`, and `golden_reference: compact`. `service` remains TBD.
- Service boundary reconciled 2026-08-04: future boundary is dedicated `Diten.PvgService` with a hybrid
  partner-aware integration posture. Frontmatter `service` remains TBD until explicit service scaffold approval.
- Route/API profile reconciled 2026-08-04: future tenant MVC route is `/Pharmacovigilance/CaseIntakeTriage`,
  view root is `frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/**`, frontend profile is same-origin
  MVC proxy, and proposed Gateway family is `/api/v1/pharmacovigilance/case-intake-triage`.
- Option-set ownership reconciled 2026-08-04: static PVG controlled lookups cover IntakeChannel, SourceType,
  ReporterType, and Seriousness; IntakePriority, TriageOutcome, and RouteTargetQueue require workflow/reference
  ownership contracts as recorded in Validation Rules.
- Delete policy reconciled 2026-08-04: no hard delete, no bulk delete, no normal user delete. Archive/void only
  after retention/legal-hold approval, with reason, actor, UTC timestamp, correlation id, and AuditEvent; blocked
  under legal hold.
- Recommended draft-planning decisions remain: dedicated future `Diten.PvgService`, `EntityBase`, tenant shell,
  no hard delete, no bulk delete, no normal user delete, and archive/void only after retention/legal-hold approval.
- Actor roles and permission keys remain proposed only: PVG Intake Agent, PVG Triage Lead, PVG Safety Manager,
  PVG Compliance Auditor, and PVG System Integration using `pvg.case-intake-triage.read`, `.create`, `.update`,
  `.triage`, `.route`, `.archive`, and `.export`; delete and bulk-delete permission keys remain explicitly
  excluded.
- Explicit runtime exclusions remain in force: no service scaffold, frontend, Gateway, route, collection, seed,
  appsettings, menu, job, or runtime code is authorized by this draft.
- MOD-0230 is Blueprint W-3. MOD-0231/MOD-0232/MOD-0234 urgent W-3 slice handling is governed by DCP-004 and does not change this pack's canonical identity.
- REG-PV-BASE is the minimum integration contract for this module: SSO+RBAC/ABAC, PHI/PII masking hooks, AuditEvent v1, Workflow/Inbox v1, Evidence-Link, OTel, Correlation-ID, and Error Model.
- Blueprint MOD-0040 / TRACE-BUNDLE is the intended reference for canonical/external IDs, correlation header, trace
  stitching, and regulated error-model decisions. Repo legacy MOD-0040 must not be used as an organization/person
  source; use MOD-0288 only for organization/person/position references if routing requires them.
- MOD-0004 and MOD-0063 remain downstream DCP-004 / MOD-0234-facing gates unless MOD-0230 explicitly emits
  signal analytics, semantic metric IDs, or data-product outputs in a later approved scope.
- Governed-AI / High-risk Blueprint markers are recorded as blockers for AI behavior, not implementation permission.

## Follow-up Items

- Resolve field-level masking behavior, row/field access rules, audit payload allow-list, evidence-link rules,
  and fail-closed tests for the 16-field model recorded for draft planning.
- Obtain explicit approval before changing frontmatter `service` from TBD or creating `Diten.PvgService`.
- Assign any future Gateway route work to integration-agent after runtime approval.
- Define same-origin MVC proxy endpoints after frontend implementation is approved.
- Resolve W-3A0 foundation remediation owner and closure criteria.
- Resolve MOD-0018, MOD-0019, MOD-0021, MOD-0023, MOD-0031, and Blueprint MOD-0040 / TRACE-BUNDLE interface contracts.
- Resolve retention/legal-hold approval for archive/void activation.
- Finalize actor roles and permission matrix with MOD-0018 / AuthService seed/grant ownership.
- Prepare separate planning for W-3A0 if requested.
- After scaffold review, update this pack toward `approved` / `ready-for-dev` only with explicit user approval.
