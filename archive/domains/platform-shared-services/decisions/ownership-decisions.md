# ownership-decisions.md — Platform & Shared Services

## Domain ownership principles
1. Each platform object has one authoritative owner module.
2. Platform owns reusable primitives; it does not own business-domain meaning.
3. Control-point modules in Domain 0 remain dependencies, not absorbed into Platform scope.
4. When an external provider is adopted later, runtime SoR may move external for selected operational modules; until then, keep the internal seam explicit and minimal.

## Module ownership register

| Module | Authoritative objects | Ownership notes |
|---|---|---|
| MOD-0012 | Secret, ConfigProfile, RotationPolicy | Current MVP implements a thin config/env abstraction rather than a full external vault. |
| MOD-0018 | Role, Permission, Assignment, ABAC Policy | Owns authorization primitives; does not own user directory or IdP. |
| MOD-0021 | AuditEvent, AuditQueryView | Owns append-only audit infrastructure and derived query surface. |
| MOD-0023 | WorkflowDefinition, WorkflowInstance, ApprovalTask, SLA/EscalationRule | Owns approval semantics and workflow versioning. |
| MOD-0024 | Task, ChecklistTemplate, ChecklistRun, TaskAssignment | Owns operational task/checklist semantics only. |
| MOD-0028 | Document, DocumentVersion, Template, Folder/Collection | Owns document metadata/versioning and templates, not evidence meaning. |
| MOD-0031 | EvidenceLink, EvidenceBundle (optional), EvidenceRequirement (optional) | Owns object ↔ evidence linkage and reusable evidence UI surfaces. |
| MOD-0032 | ApiService, ApiRoute, Credential, RateLimitPolicy | Target-state owner only; current MVP keeps the module deferred. |
| MOD-0035 | Topic, Subscription, DeadLetterQueueRecord | Target-state owner; current MVP reduces this to an internal event seam via MediatR. |
| MOD-0037 | IntegrationRun, MessageRecord, ReconciliationCase | Target-state owner only; current MVP keeps the module deferred. |
| MOD-0041 | LogSignal, MetricSignal, TraceLink (optional) | Target-state owner; current MVP reduces this to lightweight telemetry hooks. |
| MOD-0042 | AlertRule, IncidentRunbook, NotificationRoute | Target-state owner only; current MVP keeps the module deferred. |

## Non-negotiable boundary rules
### Workflow vs Tasks
- MOD-0023 owns **ApprovalTask** semantics: approve, reject, delegate, SLA, escalation.
- MOD-0024 owns **Task / Checklist** semantics: execute, complete, due dates, checklist progress.
- MOD-0023 may emit work into MOD-0024.
- MOD-0024 must never implement approval semantics.

### Documents vs Evidence
- MOD-0028 stores artifacts, metadata, templates, and versions.
- MOD-0031 owns object ↔ evidence linkage and evidence completeness state.
- Evidence semantics must not be collapsed into Document Management.

### Audit vs Evidence
- MOD-0021 owns immutable audit events.
- MOD-0031 owns reusable evidence-linking relationships.
- Audit is not a substitute for the evidence-linking SoR.

### Platform vs business domains
The following remain outside Platform ownership:
- goals, objectives, initiatives, projects, workstream objects
- ERP transaction objects
- domain-specific approvals that hardcode business semantics
- business-domain UI pages located under protected paths

## Repo ownership rule
Use approved Platform paths only. Do not place Platform code into Demand, ES&BP, or Delivery & Execution feature trees.
