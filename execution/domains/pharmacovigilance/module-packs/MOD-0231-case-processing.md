---
id: MOD-0231
name: Case Processing
domain: pharmacovigilance
service: Diten.PvgService
shell: tenant
golden_reference: compact
entity_base: EntityBase
status: ready-for-dev
build_gate: open
operational_runtime_gate: closed
owner: TBD
branch: feature/pvg/mod-0231-build-test-governance
started: 2026-08-04
target: TBD
form_field_count: 16
---

# MOD-0231 - Case Processing

> **Status change 2026-08-10.** DCP-004 is `approved` and this pack is `ready-for-dev` for the
> **local/dev/CI build/test gate only**. This authorizes future non-operational class-library contracts and tests
> under `services/Diten.PvgService/**` for MOD-0231 Signal Minimum Scope only, after a separate work package.
> Operational runtime remains closed: no API host, Gateway route, frontend, appsettings, Mongo, collections,
> seeds, jobs, migrations, partner integration, AI, archive/void/export/delete/bulk-delete, production use,
> supplier qualification, or validation is authorized.

> DCP-002 gate (2026-08-04): `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0231 --name "Case Processing"` -> `OK  MOD-0231: proven against Blueprint/registry.`

## Module Summary

MOD-0231 Case Processing is the canonical Pharmacovigilance case-processing module. Under DCP-004, this build/test
pack covers only the urgent W-3B delivery slice named **Signal Minimum Scope**. "Signal Minimum Scope" is a delivery
slice / MVP scope label only; it is not the canonical module name and must not replace `Case Processing` in
frontmatter, registry references, routes, permissions, or runtime literals.

The Signal Minimum Scope exists to define the minimum Safety Case master-record, lifecycle, trace, and assessment
contract that downstream MOD-0234 Signal Management can consume without authoring the full W-4 Case Processing
module. This pack does not implement operational case-processing runtime, API host, frontend, gateway, database,
seed, workflow engine, evidence store, audit store, masking engine, or analytics behavior.

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

In scope for this build/test gate:

- Case Processing canonical boundary for the Signal Minimum Scope only.
- Safety Case master-record contract needed after MOD-0230 intake and before MOD-0234 signal review.
- Minimum lifecycle state model and trace outputs needed by Signal MVP planning.
- Minimum assessment fields needed for signal handoff, without full case processing workbench behavior.
- W-3A0, MOD-0230, workflow, evidence, audit, masking, correlation, and regulated error-model dependency map.
- Future module-pack readiness questions and blockers.
- Future non-operational class-library contracts and tests under `services/Diten.PvgService/**`, only after a
  separate MOD-0231 scaffold work package and only for Signal Minimum Scope.

Out of scope for this build/test gate:

- Operational runtime implementation of MOD-0231.
- Full W-4 Case Processing.
- Case narrative authoring workbench, full medical review, full causality model, full regulatory assessment,
  submission/reporting, quality workflow, or PV operations beyond Signal Minimum Scope.
- MedDRA coding implementation and dictionary/version binding, owned by MOD-0232.
- Signal hypothesis, evaluation, and review decision ownership, owned by MOD-0234.
- W-3A0 foundation remediation development.
- API host, frontend UI, gateway routes, appsettings, database collections, persistence, seed data, migrations,
  jobs, permission seeds, module catalog/menu entries, or runtime endpoints.
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

Operational runtime objects, repositories, endpoints, frontend routes, and permissions are not authorized by this
build/test gate. Non-operational command/query contracts, domain models, validators, fail-closed ports, and tests
may be added only under a separate MOD-0231 scaffold work package.

## Entity Fields

Create/edit user-entered field count recorded for build/test planning for the Signal Minimum Scope: `16`.

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

Every field included in future create/edit/list/detail/export surfaces must later receive masking behavior,
row/field access rule, audit payload rule, evidence-link rule, workflow rule, and fail-closed tests before any
operational runtime or exposed surface.

## Repo Scope

Authorized by this build/test gate:

- `execution/domains/pharmacovigilance/module-packs/MOD-0231-case-processing.md`
- Future non-operational class-library contracts and tests under `services/Diten.PvgService/**` for MOD-0231
  Signal Minimum Scope only, after a separate work package. This may define contracts, domain models, validators,
  fail-closed ports, and tests; it must not create an API host, runtime listener, persistence adapter, repository
  implementation, database collection, appsettings, Gateway route, frontend file, seed, job, partner integration,
  AI behavior, archive/void/export/delete/bulk-delete surface, or operational endpoint.

Still blocked until separate approval:

- Operational PVG runtime service behavior - `Diten.PvgService` is named in frontmatter only for local
  non-operational build/test class-library work.
- PVG frontend paths - planned tenant MVC surface under
  `frontend/Diten.Web/Views/Pharmacovigilance/CaseProcessing/**`.
- PVG gateway route paths - TBD and integration-agent-owned.
- Operational PVG tests - TBD after operational service/frontend boundaries are approved. Non-operational
  class-library tests may be added only under a separate MOD-0231 scaffold work package.

## Protected Paths

- `.antigravity/**`.
- `services/**` except future separately assigned non-operational class-library contracts/tests under
  `services/Diten.PvgService/**` for MOD-0231 Signal Minimum Scope.
- `frontend/**` - no PVG UI is authorized by this build/test gate.
- `gateway/**` - no gateway route is authorized by this build/test gate.
- `gateway/Diten.ApiGateway/**/ocelot.json` - integration-agent owned if a future route is approved.
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`.
- `frontend/Diten.Web/Controllers/Archive/**`.
- `frontend/Diten.Web/Views/Archive/**`.
- `execution/portfolio/delivery-capability-packs/DCP-004-pvg-urgent-w3-development-block.md` - status remains unchanged.
- `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md` - consumed as an upstream
  build/test dependency, not edited by this pack.
- Other domain module packs and runtime internals unless explicitly authorized by the user.

## Dependencies

W-3A0 and upstream MOD-0230 dependencies are blockers, not waived:

| Dependency | Owning module / source | Status for MOD-0231 Signal Minimum Scope |
|---|---|---|
| DCP-004 | PVG Urgent W-3 Development Block | SATISFIED for build/test - `approved`; operational runtime still closed |
| MOD-0230 Case Intake & Triage | Intake baseline, triage/routing boundary, evidence-pack contract | REQUIRED - build/test may define an unavailable/fail-closed handoff port only; owner-approved operational handoff remains BLOCKER |
| REG-PV-BASE | DCP-004 minimum integration contract | BUILD/TEST ONLY through fail-closed contracts; operational runtime BLOCKER |
| CASE-LIFECYCLE | W-3A0 foundation dependency | BUILD/TEST ONLY through local lifecycle contract/tests; operational runtime BLOCKER |
| SSO + RBAC/ABAC | MOD-0018 RBAC / permissions plus Platform/Auth foundations | REQUIRED; missing permission/actor/tenant context must fail closed |
| PHI/PII masking hooks | MOD-0019 Data Masking & Row/Field Security | REQUIRED; unavailable policy must deny/omit/mask with no permissive fallback |
| AuditEvent v1 | MOD-0021 Audit Trail Service | REQUIRED; build/test may create metadata-only audit intent only, not append/persist |
| Workflow/Inbox v1 | MOD-0023 Workflow Designer | REQUIRED; unavailable transition gate must block mutation |
| Evidence-Link | MOD-0031 Evidence Linking Service | REQUIRED; unavailable evidence link must block processing/handoff or remain pending only by approved contract |
| TRACE-BUNDLE: canonical ID, Correlation-ID, trace stitching, regulated error model | Blueprint MOD-0040 / platform trace standards | REQUIRED; missing/invalid correlation must fail closed before mutation |
| OTel / operational telemetry | Platform observability foundations | REQUIRED for safe error/telemetry shape; no operational telemetry sink authorized |
| MOD-0232 MedDRA Coding | Coding contract and dictionary-version binding | Downstream / parallel gate; not implemented here |
| MOD-0234 Signal Management | Signal MVP consumer | Downstream consumer; requires this slice contract |

MOD-0004 Metric & Semantic Registry and MOD-0063 Data Warehouse / Lakehouse are not direct MOD-0231 runtime
blockers unless this module's approved scope emits signal analytics, semantic metric IDs, or data-product outputs.
They remain downstream DCP-004 / MOD-0234-facing gates unless explicitly added to MOD-0231 scope.

### Required Interface Contracts for Build/Test and Operational Runtime

| Owner | Required contract for MOD-0231 | Required MOD-0231 decision | Status |
|---|---|---|---|
| MOD-0230 | intake handoff object reference, triage/routing outcome, evidence boundary, safe metadata | exact fields consumed from MOD-0230 and fail-closed behavior when intake contract is unavailable | BUILD/TEST MAY DEFINE FAIL-CLOSED PORT; operational handoff remains BLOCKER |
| MOD-0018 RBAC / permissions | canonical permission keys, seed/grant ownership, actor context, tenant authorization context, optional data-scope shape | actor roles and permission matrix for read/create/update/process/assess/handoff/archive/export or explicit de-scope | OPEN / BLOCKER |
| MOD-0019 masking / row-field security | field sensitivity vocabulary, masking/omit/deny behavior, row-scope and field-scope evaluation, unavailable-policy behavior | per-field sensitivity matrix and fail-closed behavior for list/detail/create/update/export/audit | OPEN / BLOCKER |
| MOD-0021 AuditEvent v1 | append/event shape, safe metadata envelope, redaction rules, critical audit failure policy, correlation propagation | audited operations, payload allow-list, and behavior when audit append/outbox is unavailable | OPEN / BLOCKER |
| MOD-0023 Workflow/Inbox v1 | lifecycle transition gate, inbox handoff API/event, assignment semantics, fail-closed behavior | minimum lifecycle states, routable/processable states, transition reason codes, blocked/allowed behavior | OPEN / BLOCKER |
| MOD-0031 Evidence-Link | object reference shape, link/query API, evidence requirement/completeness rule, evidence-pack boundary | evidence required for processing, assessment, and signal handoff | OPEN / BLOCKER |
| Blueprint MOD-0040 / TRACE-BUNDLE | canonical/external ID semantics, `X-Correlation-Id`, trace stitching, regulated error model | case ID policy, external source ID policy, correlation propagation, error reason-code policy | OPEN / BLOCKER |
| MOD-0234 | Signal MVP intake contract | exact Signal Handoff Summary shape and downstream acceptance criteria | OPEN / BLOCKER |

### MOD-0230 Handoff Fields Consumed by MOD-0231

MOD-0231 consumes MOD-0230 handoff evidence as an upstream reference. It must not re-own intake records, intake
artifacts, triage state, or routing decisions.

Current implemented build/test evidence is **`MOD0230HandoffReference v0.1`**. This v0.1 shape is not the future
owner-approved operational handoff contract. It exists only to support local/dev/CI build-test handoff and downstream
fail-closed proof.

| `MOD0230HandoffReference v0.1` field | MOD-0231 build/test use | Operational status |
|---|---|---|
| `IntakeDraftId` | Upstream intake draft reference | Build/test evidence only; v1 approval required for runtime |
| `IntakeNumber` | Trace/display reference | Build/test evidence only; canonical identity remains server-owned |
| `ReceivedAtUtc` | Lifecycle baseline context | Build/test evidence only; workflow/SLA policy still blocked |
| `TriageOutcomeCode` | Triage outcome reference | Build/test evidence only; MOD-0023 transition/routing approval still blocked |
| `RouteTargetQueueCode` | Initial queue reference | Build/test evidence only; MOD-0023 queue/assignment authority still blocked |
| `EvidenceLinkReferenceIds` | Evidence reference IDs | Build/test evidence only; MOD-0031 completeness and evidence-pack approval still blocked |

`TenantId` and correlation / trace context are external server context. They must be resolved by the server-side
tenant and TRACE-BUNDLE / Blueprint MOD-0040 infrastructure and must not be supplied as authoritative fields in a
client handoff payload.

The table below remains the planned **v1 operational handoff** consumption list. Fields that are not present in
`MOD0230HandoffReference v0.1` are not produced by BE-01 or BE-02 and must continue to block operational MOD-0231
runtime until owner-approved v1 evidence exists.

| MOD-0230 field / output | MOD-0231 Signal Minimum Scope use | Status |
|---|---|---|
| Safety Case Intake ID | Required upstream same-tenant intake reference | v0.1 maps only to `IntakeDraftId`; owner-approved v1 operational handoff remains BLOCKED |
| TenantId | Server-resolved tenant isolation only; never client-supplied | External server context; not a v0.1 or v1 client handoff field |
| System-generated case/intake number | Trace/display reference | v0.1 maps only to `IntakeNumber`; TRACE-BUNDLE/canonical identity approval still required |
| IntakeChannel | Source context | Not in BE-01/BE-02 v0.1; BLOCKED until MOD-0230 option-set contract approved |
| SourceType | Source context | Not in BE-01/BE-02 v0.1; BLOCKED until MOD-0230 option-set contract approved |
| SourceReference | External source trace, masked/redacted | Not in BE-01/BE-02 v0.1; BLOCKED by MOD-0019 and TRACE-BUNDLE |
| ReceivedAtUtc | Lifecycle/SLA baseline | Present in v0.1 for build/test only; workflow/SLA policy still blocked |
| ReporterType | Case context | Not in BE-01/BE-02 v0.1; BLOCKED until MOD-0230 option-set contract approved |
| ReporterContactSummary | Restricted PII context, masked | Not in BE-01/BE-02 v0.1; BLOCKED by MOD-0019 |
| PatientSubjectCode | Restricted PHI subject reference | Not in BE-01/BE-02 v0.1; BLOCKED by MOD-0019 |
| EventOnsetDate | Event timeline | Not in BE-01/BE-02 v0.1; BLOCKED by MOD-0019 and date policy |
| AdverseEventNarrative | Restricted PHI assessment input | Not in BE-01/BE-02 v0.1; BLOCKED by MOD-0019 / MOD-0021 |
| SuspectProductText | Product assessment input | Not in BE-01/BE-02 v0.1; BLOCKED by product/reference policy |
| Seriousness | Initial seriousness baseline | Not in BE-01/BE-02 v0.1; BLOCKED until MOD-0230 seriousness contract approved |
| IntakePriority | Initial priority baseline | Not in BE-01/BE-02 v0.1; BLOCKED by workflow/SLA policy |
| TriageOutcome | Required handoff gate | v0.1 maps only to `TriageOutcomeCode`; MOD-0023 approval still blocked |
| TriageReason | Restricted triage rationale | Not in BE-01/BE-02 v0.1; BLOCKED by MOD-0019 / MOD-0021 |
| RouteTargetQueue | Initial processing queue | v0.1 maps only to `RouteTargetQueueCode`; MOD-0023 queue/assignment approval still blocked |
| EvidenceLinkReferences | Evidence boundary; MOD-0031 owns link/query/evidence pack | v0.1 carries reference IDs only; MOD-0031 completeness/evidence-pack approval still blocked |
| Correlation ID / trace bundle | Audit and trace continuity | External server context; not a client-supplied handoff field; BLOCKED by Blueprint MOD-0040 / TRACE-BUNDLE |
| Workflow instance ID | Lifecycle continuity if approved by MOD-0023 | Not in BE-01/BE-02 v0.1; BLOCKED by MOD-0023 |

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

- Only non-operational local/dev/CI class-library contracts and tests may be authorized by a later work package.
- No API host, runtime listener, controller, health endpoint, repository implementation, Mongo/DbContext adapter,
  collection, migration, appsettings, launchSettings, seed, job, Gateway route, frontend file, partner integration,
  AI behavior, archive/void/export/delete/bulk-delete, or operational endpoint is authorized.
- No new service port is reserved for MOD-0231. It shares the already recorded `Diten.PvgService` build/test
  boundary, and that boundary remains non-operational for this pack.
- No gateway route is authorized.
- No database collection, index, migration, seed, or job is authorized.
- No UI shell or DataTable page is authorized.
- `Diten.PvgService` is the named service boundary for non-operational MOD-0231 build/test scaffold only. This
  does not approve operational runtime, production use, supplier qualification, validation, or exposed endpoints.
- Recommended future operational service boundary remains a dedicated `Diten.PvgService` with a hybrid
  partner-aware integration posture. The same future service boundary should host MOD-0230 and MOD-0231 PVG
  runtime behavior only if operational runtime is later approved.
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
- Full W-4 Case Processing remains out of scope; this build/test pack cannot grow the slice into full runtime scope without
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

`service: Diten.PvgService` - non-operational build/test class-library boundary only.

Recommended future boundary: dedicated `Diten.PvgService` with a hybrid partner-aware integration posture.

- Do not host MOD-0231 inside `Diten.Platform`, `Diten.AuthService`, `Diten.DevEnablementService`, or
  `Diten.EnterpriseStrategyService`.
- If a buy/partner PV safety system is selected, `Diten.PvgService` should act as the controlled wrapper /
  orchestration layer for Diten tenant UI, case-processing contract, MOD-0230 handoff, audit, evidence, workflow,
  correlation, MOD-0234 handoff, and adapter semantics.
- Internal build scope is limited to Signal Minimum Scope class-library contracts, domain models, validators,
  fail-closed ports, metadata-only audit intent, and tests after a separate work package; it must not become full
  W-4 Case Processing.

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

The create/edit field count recorded for build/test planning is 16, so MOD-0231 Signal Minimum Scope follows Golden Reference Compact:

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

Signal Minimum Scope fields are recorded for build/test planning. A future non-operational scaffold may define
validation contracts and fail-closed tests only. Detailed masking, workflow, evidence, audit, persistence, UI, and
Gateway behavior still must be resolved before any operational runtime or exposed surface:

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
- Permission seed/grant ownership must remain with MOD-0018 / AuthService; this build/test gate authorizes no seed.

No permission seed is authorized by this build/test gate.

## Gateway / API Routing Decision

Decision: Gateway route is required for any future runtime API, but no route is authorized by this build/test gate.

Future route decision must define:

- service/deployment owner;
- upstream API base path;
- downstream path;
- auth/correlation/error-model behavior;
- OPTIONS/CORS handling if applicable;
- integration-agent task for `gateway/Diten.ApiGateway/**/ocelot.json`.

Direct service-port calls from frontend remain forbidden.

## Acceptance Criteria

Acceptance criteria for this build/test gate pack:

- [x] Pack exists at `execution/domains/pharmacovigilance/module-packs/MOD-0231-case-processing.md`.
- [x] Status is `ready-for-dev` for local/dev/CI build/test only.
- [x] `build_gate: open`.
- [x] `operational_runtime_gate: closed`.
- [x] Canonical name is exactly `Case Processing`.
- [x] `Signal Minimum Scope` is recorded only as delivery slice / MVP scope, never as canonical module name.
- [x] DCP-002 preflight passed for MOD-0231.
- [x] DCP-004 is `approved`; MOD-0231 build/test gate may open without approving operational runtime.
- [x] W-3A0 dependencies and MOD-0230 dependency are recorded as operational blockers, not waived.
- [x] No operational runtime implementation is authorized.
- [x] Future non-operational scaffold is limited to class-library contracts/tests under `services/Diten.PvgService/**`
      and must not create API host, Gateway, frontend, appsettings, Mongo, collections, seeds, jobs, migrations,
      partner integration, AI, archive/void/export/delete/bulk-delete, or runtime endpoints.
- [x] Form field count recorded for build/test planning as `16`.
- [x] Golden Reference resolved for build/test planning as `compact`.
- [x] Shell resolved for build/test planning as `tenant`.
- [x] Entity base recorded for build/test planning as `EntityBase` for a future dedicated PVG service boundary.
- [x] Service boundary recorded as `Diten.PvgService` for non-operational build/test class-library work only.
- [x] MOD-0230 handoff fields consumed by MOD-0231 are recorded, including v0.1 build/test shape versus future
      v1 operational handoff blocker.
- [x] Minimum lifecycle states before MOD-0234 consumption are recorded.
- [x] Actor roles and permission matrix are recorded.
- [x] Delete and bulk-delete are explicitly excluded.
- [x] Archive remains blocked until retention/legal-hold approval.

Acceptance criteria before any future operational or exposed implementation can start:

- [x] DCP-004 is `approved` / `ready-for-execution`.
- [x] This module pack is `ready-for-dev` for the build/test gate only.
- [ ] MOD-0230 handoff contract is approved and compatible with this pack.
- [x] `service` is resolved as `Diten.PvgService` for non-operational local build/test scaffold only.
- [ ] W-3A0 REG-PV-BASE and CASE-LIFECYCLE dependencies are closed or explicitly satisfied by production-grade external contracts.
- [ ] Required interface contracts are concrete for MOD-0018, MOD-0019, MOD-0021, MOD-0023, MOD-0031,
      Blueprint MOD-0040 / TRACE-BUNDLE, MOD-0230, and MOD-0234.
- [ ] Detailed validation rules, masking behavior, row/field access behavior, audit payload rules, evidence-link
      rules, workflow transition rules, gateway routing, and tests are fully specified from the draft field model.
- [ ] Delete/retention/legal-hold behavior is decided.
- [x] Build/buy/partner integration boundary proposed and recorded as hybrid, partner-aware internal control wrapper.

## Test Expectations

Expected for the next non-operational build/test scaffold work package:

- Class-library build and unit tests only under `services/Diten.PvgService/**`.
- MOD-0230 handoff unavailable/fail-closed tests.
- 16-field domain validation tests with no raw PHI/PII/free-text echo.
- Command/query contract-shape tests with no client-supplied `TenantId`.
- Fail-closed tests for permission, masking, audit intent, workflow transition, evidence link, correlation, and
  regulated error/result shapes.
- Tests proving no API host, controller, Program.cs, appsettings, Gateway, frontend, persistence, repository,
  Mongo/DbContext, seed, job, archive/void/export/delete/bulk-delete, partner integration, or AI surface exists.

Future operational implementation test expectations must include:

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
- [x] Pack status is `ready-for-dev` for the build/test gate only.
- [x] DCP-004 promoted to `approved` / `ready-for-execution`.
- [~] MOD-0230 handoff contract required; next build/test scaffold may define fail-closed unavailable behavior only.
- [~] W-3A0 dependency owner/scope resolved for build/test through fail-closed local contracts only; operational
      runtime remains blocked until production-grade contracts are accepted.
- [ ] MOD-0018 RBAC/permission contract and actor matrix resolved.
- [ ] MOD-0019 masking / row-field security contract resolved.
- [ ] MOD-0021 AuditEvent v1 append/redaction/failure contract resolved.
- [ ] MOD-0023 Workflow/Inbox v1 handoff/transition contract resolved.
- [ ] MOD-0031 Evidence-Link object/evidence-pack contract resolved.
- [ ] Blueprint MOD-0040 / TRACE-BUNDLE canonical ID, correlation, trace-stitching, and error-model contract resolved.
- [ ] MOD-0234 signal handoff consumption contract approved.
- [x] `service` resolved as `Diten.PvgService` for non-operational build/test class-library work only.
- [x] Future service/deployment boundary recorded as dedicated `Diten.PvgService`; operational runtime approval still
      required before API host, persistence, Gateway, frontend, or deployment work.
- [x] `shell` resolved for build/test planning as `tenant`.
- [x] `entity_base` recorded for build/test planning as `EntityBase` for future dedicated PVG service boundary.
- [x] Signal Minimum Scope create/edit fields defined.
- [x] Signal Minimum Scope fields marked required/optional.
- [x] PHI/PII/sensitive-field matrix recorded for build/test planning for every field.
- [x] `form_field_count` resolved for build/test planning as `16`.
- [x] `golden_reference` resolved for build/test planning as `compact`.
- [ ] Entity fields and validation rules fully specified.
- [x] Authorization actor/role matrix recorded for build/test planning.
- [ ] Delete, retention, archive, and legal-hold policy approved.
- [x] Build/buy/partner integration boundary proposed and recorded as hybrid, partner-aware internal control wrapper.
- [ ] Gateway route decision approved and assigned to integration-agent if needed.
- [ ] Test expectations are concrete enough for implementation.

## Implementation Notes

- This pack is intentionally limited to the local/dev/CI build/test gate.
- DCP-004 is `approved`; this pack can be used to start a separate non-operational class-library scaffold work
  package only. It cannot be used to start operational runtime work.
- MOD-0231 is Blueprint W-4, but DCP-004 permits urgent W-3 delivery-slice planning for Signal Minimum Scope while
  preserving the canonical name and Blueprint wave metadata.
- Signal Minimum Scope is a delivery slice only. Do not use it as a module name, registry name, runtime literal,
  permission prefix, route segment, or frontmatter `name`.
- Frontmatter decisions reconciled 2026-08-10: `service: Diten.PvgService`, `shell: tenant`,
  `entity_base: EntityBase`, `form_field_count: 16`, and `golden_reference: compact`.
- Service boundary reconciled 2026-08-10: `Diten.PvgService` is the MOD-0231 build/test class-library boundary.
  This does not approve API host, persistence, appsettings, Gateway, frontend, deployment, production runtime,
  supplier qualification, or validation.
- Route/UI profile reconciled 2026-08-04: future tenant MVC route is `/Pharmacovigilance/CaseProcessing`, view root
  is `frontend/Diten.Web/Views/Pharmacovigilance/CaseProcessing/**`, layout is `_LayoutTenantShell`, and frontend
  profile is same-origin MVC proxy.
- Signal Minimum Scope field model reconciled 2026-08-04 with 16 user-entered create/edit fields and compact
  Golden Reference. Detailed validation, masking, audit, workflow, evidence, and test rules remain blockers.
- MOD-0230 handoff and minimum MOD-0234 consumption lifecycle states were recorded 2026-08-04 as planning
  decisions. `MOD0230HandoffReference v0.1` is recorded as build/test evidence only; it does not resolve the
  future v1 operational handoff, MOD-0023, MOD-0031, MOD-0021, MOD-0019, or TRACE-BUNDLE blockers.
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
  the 16-field model recorded for build/test planning.
- Resolve retention/legal-hold policy before any archive/void operation is introduced.
- Finalize actor roles and permission matrix with MOD-0018 / AuthService seed/grant ownership.
- Prepare separate planning for W-3A0 if requested.
- Move beyond the build/test gate only with explicit operational runtime approval.
