---
id: MOD-0231
name: Case Processing
domain: pharmacovigilance
service: TBD
shell: tenant
golden_reference: compact
entity_base: EntityBase
status: draft
owner: TBD
branch: feature/pvg/mod-0231-case-processing
started: 2026-08-04
target: TBD
form_field_count: 16
---

# MOD-0231 - Case Processing

> Draft planning artifact only. This module pack does not authorize runtime work. DCP-004 remains `draft`;
> production implementation stays blocked until DCP-004 is `approved` / `ready-for-execution`, this module pack
> is `approved` / `ready-for-dev`, MOD-0230 dependencies are contract-closed, and W-3A0 blockers are resolved or
> accepted through production-grade external contracts.

> DCP-002 gate (2026-08-04): `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0231 --name "Case Processing"` -> `OK  MOD-0231: proven against Blueprint/registry.`

## Module Summary

MOD-0231 Case Processing is the canonical Pharmacovigilance case-processing module. Under DCP-004, this draft
covers only the urgent W-3B delivery slice named **Signal Minimum Scope**. "Signal Minimum Scope" is a delivery
slice / MVP scope label only; it is not the canonical module name and must not replace `Case Processing` in
frontmatter, registry references, routes, permissions, or runtime literals.

The Signal Minimum Scope exists to define the minimum Safety Case master-record, lifecycle, trace, and assessment
contract that downstream MOD-0234 Signal Management can consume without authoring the full W-4 Case Processing
module. This draft does not implement case processing runtime, service scaffold, frontend, gateway, database,
seed, workflow, evidence, audit, masking, or analytics behavior.

Blueprint / DCP-004 context:

| Field | Value |
|---|---|
| Canonical ID | MOD-0231 |
| Canonical name | Case Processing |
| DCP-004 delivery label | Urgent W-3B |
| DCP-004 delivery slice / MVP scope | Signal Minimum Scope |
| Blueprint wave note | Blueprint places MOD-0231 in W-4; DCP-004 treats it as urgent W-3 delivery-slice planning metadata only |
| Upstream dependency | MOD-0230 Case Intake & Triage |
| Downstream consumer | MOD-0234 Signal Management |
| Regulated-data posture | PHI/PII-sensitive, audit-grade, workflow/evidence-gated |

## Ownership and Boundaries

In scope for this draft:

- Case Processing canonical boundary for the Signal Minimum Scope only.
- Safety Case master-record contract needed after MOD-0230 intake and before MOD-0234 signal review.
- Minimum lifecycle state model and trace outputs needed by Signal MVP planning.
- Minimum assessment fields needed for signal handoff, without full case processing workbench behavior.
- W-3A0, MOD-0230, workflow, evidence, audit, masking, correlation, and regulated error-model dependency map.
- Future module-pack readiness questions and blockers.

Out of scope for this draft:

- Production runtime implementation of MOD-0231.
- Full W-4 Case Processing.
- Case narrative authoring workbench, full medical review, full causality model, full regulatory assessment,
  submission/reporting, quality workflow, or PV operations beyond Signal Minimum Scope.
- MedDRA coding implementation and dictionary/version binding, owned by MOD-0232.
- Signal hypothesis, evaluation, and review decision ownership, owned by MOD-0234.
- W-3A0 foundation remediation development.
- Runtime service scaffold, frontend UI, gateway routes, database collections, seed data, migrations, jobs,
  tests, permission seeds, or module catalog/menu entries.
- AI extraction, summarization, recommendation, or routing implementation.

## Owned Objects

Planned logical objects for the Signal Minimum Scope, not runtime classes yet:

| Object | Ownership | Runtime status |
|---|---|---|
| Safety Case Master | MOD-0231 owns the post-intake case-processing master boundary | Planned only |
| Case Lifecycle State | MOD-0231 owns lifecycle state semantics for the Signal Minimum Scope | Planned only |
| Case Processing Trace | MOD-0231 owns audit-grade processing trace outputs consumed by downstream signal work | Planned only |
| Signal Minimum Assessment | MOD-0231 owns the minimal assessment contract needed before Signal MVP consumption | Planned only |
| Intake Handoff Reference | MOD-0231 consumes MOD-0230 intake record and evidence boundary; it does not own intake artifacts | Planned only |
| Evidence Pack Requirement | MOD-0231 defines case-processing evidence requirements; MOD-0031 owns evidence links | Planned only |
| Signal Handoff Summary | MOD-0231 produces bounded output for MOD-0234; MOD-0234 owns signal hypothesis/evaluation | Planned only |

Future runtime objects, repositories, commands, queries, DTOs, endpoints, frontend routes, and permissions are not
authorized by this draft. They must be finalized after open decisions close.

## Entity Fields

Create/edit user-entered field count recorded for draft planning for the Signal Minimum Scope: `16`.

Golden Reference decision: `compact`, because the create/edit form has more than 8 user-entered fields and the
case-processing review surface needs separate Create/Edit/Details pages.

Excluded from field count: `Id`, `TenantId`, audit fields, correlation id, workflow instance id, created/updated
metadata, system-generated case number, MOD-0230 intake reference metadata, lifecycle trace events, computed SLA /
status timestamps, and MOD-0234 downstream signal identifiers.

| Field | Required | Sensitivity class | User-entered | Notes / blocker |
|---|---|---|---|---|
| CaseProcessingPriority | Yes | regulated-safety | Yes | Priority taxonomy and SLA linkage require workflow/SLA policy approval. |
| CaseValidityStatus | Yes | regulated-safety | Yes | Allowed values and transition impact require CASE-LIFECYCLE approval. |
| CaseValidityReason | No | PHI | Yes | Raw free text prohibited in logs/traces/metrics/audit payloads. |
| ProcessingOwnerQueue | Yes | confidential | Yes | Requires MOD-0023 Workflow/Inbox queue contract and permission-filtered visibility. |
| ProcessingDueAtUtc | No | regulated-safety | Yes | UTC normalization and SLA source policy require approval. |
| ProductExposureAssessment | Yes | regulated-safety | Yes | Minimum signal-relevant product exposure assessment only; full causality is out of scope. |
| SeriousnessConfirmed | Yes | regulated-safety | Yes | May reconcile MOD-0230 seriousness but cannot redefine intake ownership. |
| EventAssessmentSummary | Yes | PHI | Yes | Raw narrative prohibited in logs/traces/metrics/audit payloads. |
| PreliminaryExpectedness | No | regulated-safety | Yes | Preliminary only; MedDRA/dictionary coding remains MOD-0232-owned. |
| EvidenceCompletenessStatus | Yes | regulated-safety | Yes | Requires MOD-0031 Evidence-Link completeness contract. |
| EvidenceGapReason | No | confidential | Yes | No fake evidence readiness when evidence contract is unavailable. |
| SignalRelevanceFlag | Yes | regulated-safety | Yes | Bounded Signal Minimum Scope indicator for MOD-0234, not signal hypothesis ownership. |
| SignalRelevanceReason | No | PHI | Yes | Raw reason text prohibited in logs/traces/metrics/audit payloads. |
| SignalHandoffReadiness | Yes | regulated-safety | Yes | Must align with lifecycle and MOD-0234 acceptance gate. |
| SignalHandoffSummary | No | PHI | Yes | Bounded handoff text only; no analytics/data-product output unless separately approved. |
| ProcessingNotesInternal | No | confidential | Yes | Internal notes are not exportable unless masked/export policy later permits it. |

Every field included in create/edit/list/detail/export surfaces must later receive masking behavior, row/field
access rule, audit payload rule, evidence-link rule, workflow rule, and fail-closed tests before `ready-for-dev`.

## Repo Scope

Authorized by this draft:

- `execution/domains/pharmacovigilance/module-packs/MOD-0231-case-processing.md`

Future only, blocked until DCP-004 and this module pack pass approval gates:

- PVG runtime service path - planned future dedicated `Diten.PvgService`; frontmatter `service` remains `TBD`
  until explicit service scaffold approval.
- PVG frontend paths - planned tenant MVC surface under
  `frontend/Diten.Web/Views/Pharmacovigilance/CaseProcessing/**`.
- PVG gateway route paths - TBD and integration-agent-owned.
- PVG tests - TBD after service/frontend boundaries are approved.

## Protected Paths

- `.antigravity/**`.
- `services/**` - no PVG runtime service scaffold is authorized by this draft.
- `frontend/**` - no PVG UI is authorized by this draft.
- `gateway/**` - no gateway route is authorized by this draft.
- `gateway/Diten.ApiGateway/**/ocelot.json` - integration-agent owned if a future route is approved.
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`.
- `frontend/Diten.Web/Controllers/Archive/**`.
- `frontend/Diten.Web/Views/Archive/**`.
- `execution/portfolio/delivery-capability-packs/DCP-004-pvg-urgent-w3-development-block.md` - status remains unchanged.
- `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md` - consumed as an upstream draft, not edited by this pack.
- Other domain module packs and runtime internals unless explicitly authorized by the user.

## Dependencies

W-3A0 and upstream MOD-0230 dependencies are blockers, not waived:

| Dependency | Owning module / source | Status for MOD-0231 Signal Minimum Scope |
|---|---|---|
| DCP-004 | PVG Urgent W-3 Development Block | BLOCKER - currently `draft`; execution not authorized |
| MOD-0230 Case Intake & Triage | Intake baseline, triage/routing boundary, evidence-pack contract | BLOCKER - MOD-0230 remains draft and must define approved handoff contract |
| REG-PV-BASE | DCP-004 minimum integration contract | BLOCKER |
| CASE-LIFECYCLE | W-3A0 foundation dependency | BLOCKER |
| SSO + RBAC/ABAC | MOD-0018 RBAC / permissions plus Platform/Auth foundations | BLOCKER |
| PHI/PII masking hooks | MOD-0019 Data Masking & Row/Field Security | BLOCKER |
| AuditEvent v1 | MOD-0021 Audit Trail Service | BLOCKER |
| Workflow/Inbox v1 | MOD-0023 Workflow Designer | BLOCKER |
| Evidence-Link | MOD-0031 Evidence Linking Service | BLOCKER |
| TRACE-BUNDLE: canonical ID, Correlation-ID, trace stitching, regulated error model | Blueprint MOD-0040 / platform trace standards | BLOCKER |
| OTel / operational telemetry | Platform observability foundations | BLOCKER |
| MOD-0232 MedDRA Coding | Coding contract and dictionary-version binding | Downstream / parallel gate; not implemented here |
| MOD-0234 Signal Management | Signal MVP consumer | Downstream consumer; requires this slice contract |

MOD-0004 Metric & Semantic Registry and MOD-0063 Data Warehouse / Lakehouse are not direct MOD-0231 runtime
blockers unless this module's approved scope emits signal analytics, semantic metric IDs, or data-product outputs.
They remain downstream DCP-004 / MOD-0234-facing gates unless explicitly added to MOD-0231 scope.

### Required Interface Contracts Before `ready-for-dev`

| Owner | Required contract for MOD-0231 | Required MOD-0231 decision | Status |
|---|---|---|---|
| MOD-0230 | intake handoff object reference, triage/routing outcome, evidence boundary, safe metadata | exact fields consumed from MOD-0230 and fail-closed behavior when intake contract is unavailable | OPEN / BLOCKER |
| MOD-0018 RBAC / permissions | canonical permission keys, seed/grant ownership, actor context, tenant authorization context, optional data-scope shape | actor roles and permission matrix for read/create/update/process/assess/handoff/archive/export or explicit de-scope | OPEN / BLOCKER |
| MOD-0019 masking / row-field security | field sensitivity vocabulary, masking/omit/deny behavior, row-scope and field-scope evaluation, unavailable-policy behavior | per-field sensitivity matrix and fail-closed behavior for list/detail/create/update/export/audit | OPEN / BLOCKER |
| MOD-0021 AuditEvent v1 | append/event shape, safe metadata envelope, redaction rules, critical audit failure policy, correlation propagation | audited operations, payload allow-list, and behavior when audit append/outbox is unavailable | OPEN / BLOCKER |
| MOD-0023 Workflow/Inbox v1 | lifecycle transition gate, inbox handoff API/event, assignment semantics, fail-closed behavior | minimum lifecycle states, routable/processable states, transition reason codes, blocked/allowed behavior | OPEN / BLOCKER |
| MOD-0031 Evidence-Link | object reference shape, link/query API, evidence requirement/completeness rule, evidence-pack boundary | evidence required for processing, assessment, and signal handoff | OPEN / BLOCKER |
| Blueprint MOD-0040 / TRACE-BUNDLE | canonical/external ID semantics, `X-Correlation-Id`, trace stitching, regulated error model | case ID policy, external source ID policy, correlation propagation, error reason-code policy | OPEN / BLOCKER |
| MOD-0234 | Signal MVP intake contract | exact Signal Handoff Summary shape and downstream acceptance criteria | OPEN / BLOCKER |

### MOD-0230 Handoff Fields Consumed by MOD-0231

MOD-0231 consumes the approved MOD-0230 handoff contract as an upstream reference. It must not re-own intake
records, intake artifacts, triage state, or routing decisions.

| MOD-0230 field / output | MOD-0231 Signal Minimum Scope use | Status |
|---|---|---|
| Safety Case Intake ID | Required upstream same-tenant intake reference | BLOCKED until MOD-0230 handoff contract approved |
| TenantId | Server-resolved tenant isolation only; never client-supplied | BLOCKED by tenant/security gate |
| System-generated case/intake number | Trace/display reference | OPEN / TRACE-BUNDLE dependent |
| IntakeChannel | Source context | BLOCKED until MOD-0230 option-set contract approved |
| SourceType | Source context | BLOCKED until MOD-0230 option-set contract approved |
| SourceReference | External source trace, masked/redacted | BLOCKED by MOD-0019 and TRACE-BUNDLE |
| ReceivedAtUtc | Lifecycle/SLA baseline | BLOCKED by workflow/SLA policy |
| ReporterType | Case context | BLOCKED until MOD-0230 option-set contract approved |
| ReporterContactSummary | Restricted PII context, masked | BLOCKED by MOD-0019 |
| PatientSubjectCode | Restricted PHI subject reference | BLOCKED by MOD-0019 |
| EventOnsetDate | Event timeline | BLOCKED by MOD-0019 and date policy |
| AdverseEventNarrative | Restricted PHI assessment input | BLOCKED by MOD-0019 / MOD-0021 |
| SuspectProductText | Product assessment input | BLOCKED by product/reference policy |
| Seriousness | Initial seriousness baseline | BLOCKED until MOD-0230 seriousness contract approved |
| IntakePriority | Initial priority baseline | BLOCKED by workflow/SLA policy |
| TriageOutcome | Required handoff gate | BLOCKED by MOD-0023 |
| TriageReason | Restricted triage rationale | BLOCKED by MOD-0019 / MOD-0021 |
| RouteTargetQueue | Initial processing queue | BLOCKED by MOD-0023 |
| EvidenceLinkReferences | Evidence boundary; MOD-0031 owns link/query/evidence pack | BLOCKED by MOD-0031 |
| Correlation ID / trace bundle | Audit and trace continuity | BLOCKED by Blueprint MOD-0040 / TRACE-BUNDLE |
| Workflow instance ID | Lifecycle continuity if approved by MOD-0023 | BLOCKED by MOD-0023 |

Missing MOD-0230 handoff contract must block MOD-0231 create, process, assessment, and signal handoff. No assumed
Safety Case master state may be created from incomplete or cross-tenant intake data.

### Minimum Lifecycle States Before MOD-0234 Consumption

Recommended minimum lifecycle states for Signal Minimum Scope:

| State | Meaning | MOD-0234 consumption status |
|---|---|---|
| IntakeAccepted | Valid same-tenant MOD-0230 handoff accepted | Not consumable |
| ProcessingInProgress | Case-processing review started | Not consumable |
| EvidencePending | Required evidence links incomplete or unavailable | Not consumable |
| AssessmentPending | Minimum signal-relevance assessment incomplete | Not consumable |
| SignalMinimumReady | Required signal-minimum fields complete and validated | Consumable if MOD-0234 contract accepts pull/query |
| HandoffToSignalQueued | Handoff event/API queued for MOD-0234 | Consumable if MOD-0234 accepts queued handoff |
| HandoffToSignalAccepted | MOD-0234 accepted the bounded handoff contract | Already consumed / traceable |
| ClosedNoSignal | Case closed for signal-minimum purposes without signal handoff | Not consumable except audit/read-only |
| VoidOrArchived | Only if retention/legal-hold approval later permits it | Not consumable |

MOD-0234 should consume only `SignalMinimumReady`, `HandoffToSignalQueued`, or `HandoffToSignalAccepted`,
depending on the final MOD-0023 workflow and MOD-0234 interface contract.

## Runtime Constraints

- No runtime service scaffold is authorized.
- No service port is reserved.
- No gateway route is authorized.
- No database collection, index, migration, seed, or job is authorized.
- No UI shell or DataTable page is authorized.
- `Diten.PvgService` cannot be created until DCP-004 is `approved` / `ready-for-execution` and the active member
  module pack is `approved` / `ready-for-dev`.
- Recommended future service boundary is a dedicated `Diten.PvgService` with a hybrid partner-aware integration
  posture. The same future service boundary should host MOD-0230 and MOD-0231 PVG runtime behavior if approved.
- `service` remains `TBD` in frontmatter until explicit service scaffold approval. This draft does not reserve a
  service port or create a service folder.
- `entity_base: EntityBase` remains correct for Diten-owned tenant case-processing records, lifecycle state,
  archive/void metadata if later approved, and audit/evidence/workflow-linked records under the future PVG boundary.
  Partner-native records remain outside the repo entity model unless an approved adapter contract maps them.
- Future runtime must resolve tenant isolation, regulated data masking, audit, evidence links, workflow/inbox
  handoff, OTel, correlation ID, and error model before acceptance.
- Tenant-owned runtime data, if approved, must carry server-resolved `TenantId`; client payloads must not accept
  `TenantId`. Cross-tenant reads or mutations must return 404/empty result with no metadata leak.
- Missing MOD-0230 handoff, missing workflow gate, missing evidence-link contract, missing audit contract, or missing
  masking policy must fail closed. The module must not create assumed lifecycle progress or fake evidence state.
- Raw PHI/PII, patient identifiers, reporter identifiers, source document content, free-text narratives, processing
  notes, and unrestricted search/export payloads must not be written to logs, traces, metrics, audit payloads, or
  regulated error responses.
- Delete, archive, retention, and legal-hold behavior is undecided. Soft delete alone is not accepted for regulated
  case processing records until retention/legal-hold rules are explicitly approved.
- Full W-4 Case Processing remains out of scope; this draft cannot grow the slice into full runtime scope without
  explicit user approval and pack revision.

## Layout & Shell Contract

`shell: tenant`

MOD-0231 Signal Minimum Scope is a tenant/domain operational workflow surface, not a platform-admin configuration
module.

- Razor layout: every future `.cshtml` page must explicitly set `Layout = "_LayoutTenantShell";`.
- Future MVC route proposal: `/Pharmacovigilance/CaseProcessing`.
- Future view root proposal: `frontend/Diten.Web/Views/Pharmacovigilance/CaseProcessing/**`.
- Frontend API profile: same-origin MVC proxy profile. Browser JavaScript must call the MVC proxy surface, not call
  Gateway directly and never call a service port directly.

Frontend implementation remains blocked until DCP-004, this pack, service boundary, Gateway routing, MOD-0230
handoff, and W-3A0 production blockers are approved.

## Backend File Convention

`service: TBD`

Recommended future boundary: dedicated `Diten.PvgService` with a hybrid partner-aware integration posture.

- Do not host MOD-0231 inside `Diten.Platform`, `Diten.AuthService`, `Diten.DevEnablementService`, or
  `Diten.EnterpriseStrategyService`.
- If a buy/partner PV safety system is selected, `Diten.PvgService` should act as the controlled wrapper /
  orchestration layer for Diten tenant UI, case-processing contract, MOD-0230 handoff, audit, evidence, workflow,
  correlation, MOD-0234 handoff, and adapter semantics.
- Internal build scope is limited to the Signal Minimum Scope contract, tenant UI boundary, workflow/audit/evidence
  integration, and adapter layer after approval; it must not become full W-4 Case Processing.

If a PVG runtime service is later approved, backend implementation must follow the Golden Reference CQRS shape:

```text
Features/CaseProcessing/
├── Commands/
├── Queries/
├── Handlers/CommandHandlers/
├── Handlers/QueryHandlers/
├── Validators/
└── CaseProcessingModels.cs
```

Naming rules for future implementation:

- Commands: `CreateCaseProcessingCommand`, `UpdateCaseProcessingCommand`, `ProcessCaseProcessingCommand`,
  `AssessCaseProcessingCommand`, `HandoffCaseProcessingToSignalCommand`, and archive/void commands only if the
  corresponding operations are approved.
- Queries: `GetCaseProcessingListQuery`, `GetCaseProcessingByIdQuery`, and signal-minimum handoff queries only if
  list/detail/handoff surfaces are approved.
- Handlers: `*Handler` only; no `CommandHandler`, `QueryHandler`, or `RequestHandler` suffix.
- Validators: `*Validator` only; no `CommandValidator` suffix.
- Forbidden future conventions: `DeleteCaseProcessingCommand`, `BulkDeleteCaseProcessingCommand`, DELETE endpoints,
  and bulk-delete endpoints. Archive/void commands remain blocked until retention/legal-hold policy is approved.

This section is a future convention statement, not implementation authorization.

## Frontend File Contract

`golden_reference: compact`

The create/edit field count recorded for draft planning is 16, so MOD-0231 Signal Minimum Scope follows Golden Reference Compact:

- `Index.cshtml`.
- `Create.cshtml`.
- `Edit.cshtml`.
- `Details.cshtml`.
- `_Form.cshtml`.
- `_Filter.cshtml`.
- `_DataTable.cshtml`.
- `_IndexL10n.cshtml`.
- `CaseProcessingIndex.cs`.
- `wwwroot/assets/js/Pharmacovigilance/CaseProcessing/index.js`.
- `wwwroot/assets/js/Pharmacovigilance/CaseProcessing/index.l10n.js`.
- `Resources/Views/Pharmacovigilance/CaseProcessing/CaseProcessingIndex.{lang}.resx`.

Compact must not include Slim-only `_CreateEditOffcanvas.cshtml` or `_DetailsQuickView.cshtml`.
No frontend files may be created until runtime gates are approved.

Future frontend API calls must use the same-origin MVC proxy profile. Direct browser-to-Gateway calls are not the
preferred profile for this regulated tenant surface; direct service-port calls are forbidden.

## Validation Rules

Signal Minimum Scope fields are recorded for draft planning. Detailed validation, masking, workflow, evidence, and
audit behavior still must be resolved before `ready-for-dev`:

| Field | Required | Rule | DB-level | Pre-check | Sensitivity / fail-closed requirement |
|---|---|---|---|---|---|
| CaseProcessingPriority | Yes | Controlled priority option set and SLA linkage TBD | TBD | workflow/SLA policy | regulated-safety; unknown priority policy fails closed |
| CaseValidityStatus | Yes | Proposed validity state set | TBD | MOD-0023 / CASE-LIFECYCLE | regulated-safety; invalid state fails closed |
| CaseValidityReason | No | Max length, redaction, and reason-code policy TBD | TBD | MOD-0019 / MOD-0021 | PHI; raw text prohibited in logs/traces/metrics/audit payloads |
| ProcessingOwnerQueue | Yes | Proposed queue/route target list | TBD | MOD-0023 | confidential; visibility must be permission-filtered |
| ProcessingDueAtUtc | No | UTC value; SLA source policy TBD | TBD | workflow/SLA policy | regulated-safety; server-side UTC normalization required |
| ProductExposureAssessment | Yes | Proposed bounded assessment values/text policy TBD | TBD | product/PVG policy | regulated-safety; full causality remains out of scope |
| SeriousnessConfirmed | Yes | Proposed boolean/status behavior and reconciliation with MOD-0230 seriousness | TBD | MOD-0230 contract | regulated-safety; no silent overwrite of intake seriousness |
| EventAssessmentSummary | Yes | Max length, redaction, and audit policy TBD | TBD | MOD-0019 / MOD-0021 | PHI; raw free text prohibited in logs/traces/metrics/audit payloads |
| PreliminaryExpectedness | No | Proposed preliminary expectedness values; no MedDRA ownership | TBD | MOD-0232 boundary | regulated-safety; dictionary/version behavior not owned here |
| EvidenceCompletenessStatus | Yes | Proposed evidence completeness state set | TBD | MOD-0031 | regulated-safety; no fake evidence readiness |
| EvidenceGapReason | No | Max length and reason-code policy TBD | TBD | MOD-0031 / MOD-0021 | confidential; no raw sensitive details in logs/audit |
| SignalRelevanceFlag | Yes | Proposed bounded flag behavior | TBD | MOD-0234 contract | regulated-safety; no signal hypothesis ownership |
| SignalRelevanceReason | No | Max length, redaction, and audit policy TBD | TBD | MOD-0019 / MOD-0021 | PHI; raw reason text prohibited in logs/traces/metrics/audit payloads |
| SignalHandoffReadiness | Yes | Proposed readiness state set aligned with lifecycle | TBD | MOD-0023 / MOD-0234 | regulated-safety; invalid readiness fails closed |
| SignalHandoffSummary | No | Bounded summary shape and redaction policy TBD | TBD | MOD-0234 / MOD-0019 | PHI; no analytics/data-product output unless separately approved |
| ProcessingNotesInternal | No | Max length, masking, storage minimization, and export policy TBD | TBD | MOD-0019 / MOD-0021 | confidential; not exportable unless masked policy permits |

Every final field must have tests proving unauthorized, cross-tenant, missing-policy, masking-denied, evidence
unavailable, workflow unavailable, and audit unavailable behavior.

## Failure Path to Verify

Future implementation must verify at least these paths:

- **Missing approved MOD-0230 handoff**
  - Expected: create/process/handoff is blocked; no assumed Safety Case master state is created.
- **Missing required case-processing field**
  - Expected: 400 validation response; no processing record or lifecycle transition is committed.
- **Duplicate or conflicting case identity**
  - Expected: 409 or approved duplicate/case-link handling; no silent overwrite.
- **Unauthorized actor**
  - Expected: 401/403 according to policy; no case data or metadata leak.
- **Cross-tenant access**
  - Expected: 404 or empty result; no cross-tenant data or metadata returned.
- **Missing MOD-0019 policy for a sensitive field**
  - Expected: field omitted/masked or operation denied according to the field matrix; no permissive fallback.
- **Sensitive input appears in audit/log/trace**
  - Expected: test fails; raw PHI/PII/free text must not be persisted to audit payloads, logs, traces, metrics, or error details.
- **Workflow/Inbox unavailable**
  - Expected: lifecycle transition, assessment, or signal handoff blocks; no untraceable case progression.
- **Evidence-link unavailable**
  - Expected: processing/evidence readiness/handoff blocks or follows an explicitly approved degraded path; no fake evidence pack.
- **Audit sink unavailable**
  - Expected: regulated mutation is blocked or queued according to approved MOD-0021 contract; no unaudited mutation.
- **Correlation/trace context missing**
  - Expected: behavior follows Blueprint MOD-0040 / TRACE-BUNDLE decision; runtime must not create untraceable regulated state changes.
- **Delete/archive attempted before retention/legal-hold decision**
  - Expected: operation absent or denied; no regulated case-processing record is removed or hidden without approved policy.

## Authorization Convention

Permission prefix proposal for future tenant/domain implementation:

```text
pvg.case-processing.read
pvg.case-processing.create
pvg.case-processing.update
pvg.case-processing.process
pvg.case-processing.assess
pvg.case-processing.handoff-to-signal
pvg.case-processing.archive
pvg.case-processing.export
```

Explicitly excluded permission keys:

```text
pvg.case-processing.delete
pvg.case-processing.bulk-delete
```

Initial role / permission matrix proposal:

| Role | read | create | update | process | assess | handoff-to-signal | archive | export |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| PVG Case Processor | Assigned queue | From approved intake only | Yes | Yes | No | No | No | No |
| PVG Safety Reviewer | Yes | No | Yes | Yes | Yes | Recommend only | No | Masked only |
| PVG Signal Liaison | Signal-ready only | No | No | No | Read assessment | Yes | No | Masked only |
| PVG Safety Manager | Yes | Yes | Yes | Yes | Yes | Yes | Only after retention/legal-hold approval | Masked only |
| PVG Compliance Auditor | Read-only | No | No | No | No | No | No | Masked only |
| PVG System Integration | Approved contract only | From MOD-0230 handoff only | No | No | No | Event/API handoff only | No | No |

Open authorization decisions:

- Final actor role names, actor type mapping, and seed/grant ownership require MOD-0018 / AuthService approval.
- Create is limited to approved MOD-0230 handoff or privileged safety-manager correction flow if later approved.
- Archive permission is unusable until retention/legal-hold policy is approved.
- Export is masked-only unless a later field policy approval explicitly permits more.
- PHI/PII field-level authorization must align with MOD-0019 before runtime.
- Permission seed/grant ownership must remain with MOD-0018 / AuthService; this draft authorizes no seed.

No permission seed is authorized by this draft.

## Gateway / API Routing Decision

Decision: Gateway route is required for any future runtime API, but no route is authorized by this draft.

Future route decision must define:

- service/deployment owner;
- upstream API base path;
- downstream path;
- auth/correlation/error-model behavior;
- OPTIONS/CORS handling if applicable;
- integration-agent task for `gateway/Diten.ApiGateway/**/ocelot.json`.

Direct service-port calls from frontend remain forbidden.

## Acceptance Criteria

Acceptance criteria for this draft pack:

- [x] Pack exists at `execution/domains/pharmacovigilance/module-packs/MOD-0231-case-processing.md`.
- [x] Status is `draft`.
- [x] Canonical name is exactly `Case Processing`.
- [x] `Signal Minimum Scope` is recorded only as delivery slice / MVP scope, never as canonical module name.
- [x] DCP-002 preflight passed for MOD-0231.
- [x] DCP-004 remains `draft`; no execution is authorized.
- [x] W-3A0 dependencies and MOD-0230 dependency are recorded as production blockers, not waived.
- [x] No runtime implementation is authorized.
- [x] Form field count recorded for draft planning as `16`.
- [x] Golden Reference resolved for draft planning as `compact`.
- [x] Shell resolved for draft planning as `tenant`.
- [x] Entity base recorded for draft planning as `EntityBase` for a future dedicated PVG service boundary.
- [x] Future service boundary recorded as dedicated `Diten.PvgService`; frontmatter `service` remains `TBD` until
      explicit scaffold approval.
- [x] MOD-0230 handoff fields consumed by MOD-0231 are recorded.
- [x] Minimum lifecycle states before MOD-0234 consumption are recorded.
- [x] Actor roles and permission matrix are recorded.
- [x] Delete and bulk-delete are explicitly excluded.
- [x] Archive remains blocked until retention/legal-hold approval.

Acceptance criteria before any future implementation can start:

- [ ] DCP-004 is `approved` / `ready-for-execution`.
- [ ] This module pack is `approved` / `ready-for-dev`.
- [ ] MOD-0230 handoff contract is approved and compatible with this pack.
- [ ] `service` is resolved through explicit service scaffold approval; frontmatter currently remains `TBD`.
- [ ] W-3A0 REG-PV-BASE and CASE-LIFECYCLE dependencies are closed or explicitly satisfied by production-grade external contracts.
- [ ] Required interface contracts are concrete for MOD-0018, MOD-0019, MOD-0021, MOD-0023, MOD-0031,
      Blueprint MOD-0040 / TRACE-BUNDLE, MOD-0230, and MOD-0234.
- [ ] Detailed validation rules, masking behavior, row/field access behavior, audit payload rules, evidence-link
      rules, workflow transition rules, gateway routing, and tests are fully specified from the draft field model.
- [ ] Delete/retention/legal-hold behavior is decided.
- [x] Build/buy/partner integration boundary proposed and recorded as hybrid, partner-aware internal control wrapper.

## Test Expectations

No runtime tests are expected for this draft because no runtime files are authorized.

Future implementation test expectations must include:

- DCP-002 identity proof remains valid.
- Backend build and unit/integration tests for the approved PVG service boundary.
- Tenant isolation and regulated-data masking tests.
- MOD-0230 handoff contract tests.
- CASE-LIFECYCLE / workflow transition and inbox failure-path tests.
- Per-field PHI/PII sensitivity, masking, row/field deny, and missing-policy fail-closed tests.
- Audit, correlation/TRACE-BUNDLE, evidence-link, workflow/inbox failure-path tests.
- Signal handoff contract tests for MOD-0234 consumption.
- Tests proving raw PHI/PII/free text is absent from logs, traces, metrics, audit payloads, validation errors, and
  regulated error responses.
- Frontend build and DataTable verifier only if frontend is approved and Slim/Compact is decided.
- Gateway route smoke only after integration-agent-owned route approval.

## Ready-for-dev Checklist

- [x] Required governance files read.
- [x] Golden Reference Slim and Compact module packs read.
- [x] DCP-002 preflight passed.
- [x] Pack status is `draft`.
- [ ] DCP-004 promoted to `approved` / `ready-for-execution`.
- [ ] MOD-0230 handoff contract approved.
- [ ] W-3A0 dependency owner/scope resolved or production-grade contracts accepted.
- [ ] MOD-0018 RBAC/permission contract and actor matrix resolved.
- [ ] MOD-0019 masking / row-field security contract resolved.
- [ ] MOD-0021 AuditEvent v1 append/redaction/failure contract resolved.
- [ ] MOD-0023 Workflow/Inbox v1 handoff/transition contract resolved.
- [ ] MOD-0031 Evidence-Link object/evidence-pack contract resolved.
- [ ] Blueprint MOD-0040 / TRACE-BUNDLE canonical ID, correlation, trace-stitching, and error-model contract resolved.
- [ ] MOD-0234 signal handoff consumption contract approved.
- [ ] `service` resolved.
- [x] Future service/deployment boundary recorded as dedicated `Diten.PvgService`; scaffold approval still required
      before frontmatter `service` changes.
- [x] `shell` resolved for draft planning as `tenant`.
- [x] `entity_base` recorded for draft planning as `EntityBase` for future dedicated PVG service boundary.
- [x] Signal Minimum Scope create/edit fields defined.
- [x] Signal Minimum Scope fields marked required/optional.
- [x] PHI/PII/sensitive-field matrix recorded for draft planning for every field.
- [x] `form_field_count` resolved for draft planning as `16`.
- [x] `golden_reference` resolved for draft planning as `compact`.
- [ ] Entity fields and validation rules fully specified.
- [x] Authorization actor/role matrix recorded for draft planning.
- [ ] Delete, retention, archive, and legal-hold policy approved.
- [x] Build/buy/partner integration boundary proposed and recorded as hybrid, partner-aware internal control wrapper.
- [ ] Gateway route decision approved and assigned to integration-agent if needed.
- [ ] Test expectations are concrete enough for implementation.

## Implementation Notes

- This pack is intentionally incomplete because it is a draft planning artifact.
- DCP-004 is still `draft`; this pack cannot be used to start runtime work.
- MOD-0231 is Blueprint W-4, but DCP-004 permits urgent W-3 delivery-slice planning for Signal Minimum Scope while
  preserving the canonical name and Blueprint wave metadata.
- Signal Minimum Scope is a delivery slice only. Do not use it as a module name, registry name, runtime literal,
  permission prefix, route segment, or frontmatter `name`.
- Frontmatter decisions reconciled 2026-08-04: `shell: tenant`, `entity_base: EntityBase`,
  `form_field_count: 16`, and `golden_reference: compact`. `service` remains TBD.
- Service boundary reconciled 2026-08-04: future boundary is dedicated `Diten.PvgService` with a hybrid
  partner-aware integration posture. Frontmatter `service` remains TBD until explicit service scaffold approval.
- Route/UI profile reconciled 2026-08-04: future tenant MVC route is `/Pharmacovigilance/CaseProcessing`, view root
  is `frontend/Diten.Web/Views/Pharmacovigilance/CaseProcessing/**`, layout is `_LayoutTenantShell`, and frontend
  profile is same-origin MVC proxy.
- Signal Minimum Scope field model reconciled 2026-08-04 with 16 user-entered create/edit fields and compact
  Golden Reference. Detailed validation, masking, audit, workflow, evidence, and test rules remain blockers.
- MOD-0230 handoff and minimum MOD-0234 consumption lifecycle states were recorded 2026-08-04 as planning
  decisions. They do not resolve MOD-0230, MOD-0023, MOD-0031, MOD-0021, MOD-0019, or TRACE-BUNDLE blockers.
- Delete and bulk-delete policy reconciled 2026-08-04: delete and bulk-delete are excluded. Archive/void remains
  blocked until retention/legal-hold approval.
- MOD-0230 is a hard upstream dependency. MOD-0231 must not redefine intake records, intake artifacts, triage state,
  or routing decision ownership.
- REG-PV-BASE and CASE-LIFECYCLE are minimum integration contracts for this slice: RBAC/ABAC, PHI/PII masking hooks,
  AuditEvent v1, Workflow/Inbox v1, Evidence-Link, OTel, Correlation-ID, regulated error model, case lifecycle state
  machine, audit-grade workflow trace, and evidence-pack assembly contract.
- Blueprint MOD-0040 / TRACE-BUNDLE is the intended reference for canonical/external IDs, correlation header, trace
  stitching, and regulated error-model decisions. Repo legacy MOD-0040 must not be used as an organization/person
  source; use MOD-0288 only for organization/person/position references if routing or assignment requires them.
- MOD-0004 and MOD-0063 remain downstream DCP-004 / MOD-0234-facing gates unless MOD-0231 explicitly emits signal
  analytics, semantic metric IDs, or data-product outputs in a later approved scope.
- Governed-AI / High-risk Blueprint markers are recorded as blockers for AI behavior, not implementation permission.

## Follow-up Items

- Approve concrete MOD-0230 handoff contract and fail-closed behavior against the consumed fields recorded here.
- Approve concrete MOD-0234 Signal MVP consumption contract for `SignalMinimumReady`, `HandoffToSignalQueued`, and
  `HandoffToSignalAccepted`.
- Obtain explicit approval before changing frontmatter `service` from TBD or creating `Diten.PvgService`.
- Define same-origin MVC proxy endpoints after frontend implementation is approved.
- Resolve W-3A0 foundation remediation owner and closure criteria.
- Resolve MOD-0018, MOD-0019, MOD-0021, MOD-0023, MOD-0031, Blueprint MOD-0040 / TRACE-BUNDLE, and MOD-0234
  interface contracts.
- Resolve detailed validation, masking, audit payload, evidence-link, workflow transition, and fail-closed tests for
  the 16-field model recorded for draft planning.
- Resolve retention/legal-hold policy before any archive/void operation is introduced.
- Finalize actor roles and permission matrix with MOD-0018 / AuthService seed/grant ownership.
- Prepare separate planning for W-3A0 if requested.
- Update this pack toward `approved` / `ready-for-dev` only with explicit user approval.
