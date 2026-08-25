# PVG MOD-0230 GMG Input Annex - 2026-08-25

> Draft status: IT-side input annex material for GMG owner review.
> This document does not approve MOD-0230, does not sign any GMG record, and does not open operational runtime.

## 1. Gate Position

| Item | Position |
|---|---|
| Module | `MOD-0230 Case Intake & Triage` |
| Purpose | Draft IT inputs requested by the GMG MOD-0230 approval package |
| Runtime posture | **Operational runtime 0% / NO-GO** |
| Approval posture | Draft owner-review input only; no owner approval claimed |
| Evidence scope | GMG tenant/customer operational evidence only; not global PVG product architecture authority |
| GMG records | Records 1-9 remain unsigned; Record 10 remains open |
| Downstream runtime | MOD-0231, MOD-0232, and MOD-0234 runtime remains blocked |
| Forbidden surface | No delete, export, archive, void, bulk, bulk-delete, AI, MedDRA data/import/search/cache, menu/catalog, seed, job, migration, persistence expansion, Gateway expansion, frontend expansion, or runtime exposure is authorized by this annex |

## 2. Tenant/Customer Evidence Scope

The GMG approval package and this annex are tenant/customer-specific evidence for GMG operational go-live and
regulated-data use. They are not global architecture authority for the Diten PVG product, and they do not rename or
re-bound the vendor-neutral, multi-tenant `Diten.PvgService` product boundary.

GMG unsigned Records 1-9 do not block generic local/dev build-test development that stays inside the approved
module-pack and docs/test boundaries. GMG Record 10 blocks GMG tenant operational runtime only. Global PVG product
operational runtime remains separately gated by product/platform owner approvals, including FieldSecurity,
AuditEvent, WorkflowTransitionGate, EvidenceLink, TraceBundle/Observability, retention/legal-hold, and explicit
operational runtime authorization. MOD-0231, MOD-0232, and MOD-0234 runtime remains blocked unless each is separately
approved.

## 3. Field Definition Inputs

These 16 fields are the current MOD-0230 draft intake/triage field set for owner review. The types below describe
the IT contract shape, not an approved production data model. FieldSecurity, AuditEvent, WorkflowTransitionGate,
EvidenceLink, TraceBundle/Observability, retention/legal-hold, and explicit runtime authorization remain required
before operational runtime can open.

| # | Field | Draft data type | Cardinality | Free-text risk | PHI/PII sensitivity | Intended use |
|---:|---|---|---|---|---|---|
| 1 | `IntakeChannel` | Controlled code/string | Required; exactly 1 at create/update | Low; no free text intended | Public metadata | Classify how the intake was received for filtering, work distribution, and source controls. |
| 2 | `SourceType` | Controlled code/string | Required; exactly 1 at create/update | Low; no free text intended | Public metadata | Classify the source category for duplicate checks, routing context, and evidence requirements. |
| 3 | `SourceReference` | Trimmed string, max 128 | Optional; 0..1 | Medium; external references can identify a source record | Confidential | Record a non-authoritative external source reference; never a primary key and never raw in audit/log/error output. |
| 4 | `ReceivedAtUtc` | UTC instant / `DateTimeOffset` | Required; exactly 1 at create/update | Low | Regulated safety | Establish intake receipt time for ordering, SLA planning, and audit chronology after UTC normalization. |
| 5 | `ReporterType` | Controlled code/string | Required; exactly 1 at create/update | Low; no free text intended | Public metadata | Classify reporter category for intake review and regulatory workflow context. |
| 6 | `ReporterContactSummary` | String, max 256 | Optional; 0..1 | Medium; may contain contact clues | PII | Capture minimum-necessary reporter contact summary only when needed; requires masking/omit/deny rules before runtime. |
| 7 | `PatientSubjectCode` | Pseudonymous string, max 64 | Optional; 0..1 | Medium; can identify subject if uncontrolled | PHI | Hold a pseudonymous patient/subject reference for intake continuity; must not contain direct identifiers. |
| 8 | `EventOnsetDate` | Date only / `DateOnly` | Optional; 0..1 | Low text risk, high clinical sensitivity | PHI | Record event onset date for safety chronology and triage context; must not be future-dated or after received date. |
| 9 | `AdverseEventNarrative` | String, max 8000 | Required; exactly 1 at create/update | High; free clinical narrative | PHI | Capture the minimum intake adverse-event narrative needed for triage; raw text is prohibited in logs, traces, metrics, audit payloads, and errors. |
| 10 | `SuspectProductText` | String, max 512 | Optional; 0..1 | Medium; product text can contain uncontrolled terms | Regulated safety | Capture suspect product description pending product/reference governance; no search/cache/dictionary behavior is approved. |
| 11 | `Seriousness` | Controlled code/string | Required; exactly 1 at create/update | Low; no free text intended | Regulated safety | Support initial seriousness classification and prioritization. |
| 12 | `IntakePriority` | Controlled code/string | Required; exactly 1 at create/update | Low; no free text intended | Regulated safety | Support work prioritization; SLA computation remains deferred to approved workflow policy. |
| 13 | `TriageOutcome` | Enum/code: `Triaged`, `Rejected`, `Duplicate` | Required at triage; absent at create | Low; controlled value | Regulated safety | Record owner-approved triage outcome only after WorkflowTransitionGate allows the transition. |
| 14 | `TriageReason` | String, max 1000, supplementary to reason code | Optional generally; required when outcome is `Rejected` or `Duplicate` | High; free triage rationale | PHI | Capture supplemental rationale only with an approved reason-code taxonomy; raw text is prohibited in logs, traces, metrics, audit payloads, and errors. |
| 15 | `RouteTargetQueue` | Workflow queue code/string | Required at route; absent until routing | Medium; queue names can reveal operational handling | Confidential | Identify the workflow/inbox route target after MOD-0023 resolution; no hardcoded queue list is approved. |
| 16 | `EvidenceLinkReferences` | List of evidence object references | Optional; 0..20 references | Medium; references can identify evidence objects | Confidential | Carry EvidenceLink object references only; never document content and never a fake evidence pack. |

## 4. Process, State, Queue, and Routing Model

This model is a draft IT input for the GMG process owner, WorkflowTransitionGate owner, EvidenceLink owner, and
retention/legal-hold owner. It reflects the current MOD-0230 build-test slice and does not authorize operational use.

### Supported Business Operations

| Operation | Endpoint family | Draft permission key | Main guards before mutation/output |
|---|---|---|---|
| List drafts | `GET /api/pv-case-intake-triage` | `pvg.mod0230.intake.read` | Tenant, actor, correlation, permission, FieldSecurity |
| Get draft by id | `GET /api/pv-case-intake-triage/{intakeDraftId}` | `pvg.mod0230.intake.read` | Tenant, actor, correlation, permission, tenant-scoped lookup, FieldSecurity |
| Create draft | `POST /api/pv-case-intake-triage` | `pvg.mod0230.intake.create` | Validation, tenant, actor, correlation, permission, FieldSecurity, EvidenceLink when references are present |
| Update draft | `PUT /api/pv-case-intake-triage/{intakeDraftId}` | `pvg.mod0230.intake.update` | Validation, tenant, actor, correlation, permission, tenant-scoped lookup, FieldSecurity, EvidenceLink when references are present |
| Triage draft | `POST /api/pv-case-intake-triage/{intakeDraftId}/triage` | `pvg.mod0230.intake.triage` | Validation, tenant, actor, correlation, permission, tenant-scoped lookup, FieldSecurity, WorkflowTransitionGate, EvidenceLink |
| Route draft | `POST /api/pv-case-intake-triage/{intakeDraftId}/route` | `pvg.mod0230.intake.route` | Validation, tenant, actor, correlation, permission, tenant-scoped lookup, WorkflowTransitionGate route resolution, FieldSecurity, EvidenceLink |

### Draft State Model

| State | Meaning | Entry operation | Exit condition |
|---|---|---|---|
| `Draft` | Logical pre-create planning state; not an operational runtime state claim | None in current API response flow | Create request accepted after all required guards |
| `IntakeCreated` | Intake record accepted into the local/dev draft store | Create | Update, triage, or owner-approved future handoff |
| `IntakeUpdated` | Intake draft content replaced after update guard approval | Update | Further update, triage, or owner-approved future handoff |
| `TriagePending` | Reserved workflow state for future owner-approved process use | Not currently produced by successful slice-1 mutation | Triage after workflow owner approval |
| `Triaged` | Triage outcome captured after workflow and evidence gates allow | Triage | Route or owner-approved future handoff |
| `RoutePending` | Route target recorded as pending controlled handoff | Route | Future MOD-0023/MOD-0231 handoff after approvals |
| `Routed` | Reserved terminal/next-stage state for future owner-approved handoff | Not currently produced by successful slice-1 mutation | Downstream process ownership after approval |

### Queue and Routing Inputs

| Queue/routing item | Draft input | Owner dependency | Current runtime posture |
|---|---|---|---|
| Route target vocabulary | `RouteTargetQueue` code/reference | MOD-0023 Workflow/Inbox owner | Blocked; no hardcoded queue registry |
| Assignment semantics | Actor/queue ownership and handoff policy | MOD-0023 plus MOD-0018/RBAC | Blocked; no assignment seed/grant |
| SLA handling | Priority-to-SLA policy | MOD-0023 Workflow/Inbox owner | Deferred; no SLA computation |
| Downstream handoff | MOD-0230 handoff reference to MOD-0231 | MOD-0231 plus MOD-0021/MOD-0031/MOD-0023 approvals | Blocked for runtime |
| Cross-tenant behavior | Tenant-scoped lookup only; cross-tenant mutation returns not-found semantics | Multi-tenancy / security owners | Required fail-closed posture; no existence leak |

## 5. Object Classes for EvidenceLink and Retention Planning

These object classes are draft planning inputs for EvidenceLink completeness and retention class review. They do not
create repositories, collections, archive/void behavior, retention jobs, legal-hold policy, or operational records.

| Object class | Module owner | EvidenceLink completeness question | Retention/legal-hold planning question | Runtime status |
|---|---|---|---|---|
| `SafetyCaseIntake` | MOD-0230 | Which intake fields and source references require evidence before triage, route, or handoff? | What retention class applies to intake-stage safety records? | Local/dev build-test only |
| `IntakeArtifactReference` | MOD-0230 + MOD-0031 | Which submitted artifact references must be linked and queryable without duplicating content? | Does the reference inherit source artifact retention or intake retention? | Reference-only; no content store approved |
| `TriageDecision` | MOD-0230 + MOD-0023 | What evidence is required to support `Triaged`, `Rejected`, or `Duplicate` outcomes? | How long must triage rationale and reason-code evidence be retained? | Build-test only |
| `RoutingDecision` | MOD-0230 + MOD-0023 | What evidence is required before route target acceptance or downstream handoff? | Does routing metadata inherit workflow retention/legal-hold rules? | Build-test only |
| `EvidencePackBoundary` | MOD-0230 + MOD-0031 | What completeness state is required for create, update, triage, route, and handoff? | Which class owns the retention trigger for link metadata? | Pending owner approval |
| `AuditIntent` | MOD-0230 + MOD-0021 | Which event names and safe metadata must reference evidence without raw payloads? | Which audit retention class applies before operational runtime? | Tests-only evidence; owner approval required |
| `TraceContextReference` | MOD-0230 + TraceBundle/Observability owner | Which correlation/trace references are required for review without exposing raw IDs in responses? | How long are trace references retained and how are they linked to regulated review? | Tests-only evidence; owner approval required |
| `RetentionLegalHoldMarker` | Retention/legal-hold owner | Should evidence completeness block any retention or legal-hold transition? | Which fields are required for legal hold, archive, void, and release? | Not implemented; blocked |

## 6. MOD-0230 to MOD-00xx / Contract Mapping

| MOD-0230 concern | External module/contract | Draft dependency statement | Current status |
|---|---|---|---|
| Tenant/actor authorization and permission checks | MOD-0018 RBAC / AuthService / Platform access governance | MOD-0230 consumes permission decisions; seed/grant ownership remains outside MOD-0230. | Build-test evidence exists; owner approval remains required for runtime grants. |
| Field masking, field visibility, and sensitive-output behavior | MOD-0019 FieldSecurity | All 16 fields require allow/mask/omit/deny review before runtime; missing policy must fail closed. | Tests-only evidence exists; owner approval required. |
| Audit event shape and safe metadata | MOD-0021 AuditEvent | Create, update, triage, route, denial, and failure events need approved event names and redaction rules. | Tests-only evidence exists; owner approval required. |
| Workflow transition, queues, and routing | MOD-0023 WorkflowTransitionGate | Triage and route transitions require owner-approved gate, reason taxonomy, queue registry, and assignment semantics. | Tests-only evidence exists; owner approval required. |
| Evidence references and completeness | MOD-0031 EvidenceLink | Evidence references are object references only; no fake pack or duplicated content. | Tests-only evidence exists; owner approval required. |
| Correlation, canonical IDs, regulated errors | TraceBundle / Observability contracts | Every business operation requires safe correlation context and exposes safe reason codes only. | Tests-only evidence exists; owner approval required. |
| Retention, legal hold, archive, and void | Retention/legal-hold owner contract | Archive/void remain unavailable until owner policy, evidence, audit, workflow, trace, and legal-hold behavior are approved. | Tests-only blocker evidence exists; owner approval required. |
| Downstream case processing handoff | MOD-0231 Case Processing | MOD-0230 handoff remains non-operational until upstream gates and MOD-0231 runtime approval exist. | MOD-0231 class-library/test-only; runtime blocked. |
| MedDRA coding | MOD-0232 MedDRA Coding | No MedDRA dictionary data/import/search/cache is authorized by MOD-0230. | MOD-0232 class-library/test-only; runtime blocked. |
| Signal management | MOD-0234 Signal Management | No fake signal, metric, cohort, dashboard, shell, or runtime exposure is authorized. | MOD-0234 class-library/test-only; runtime blocked. |

## 7. Permission Key Grammar Note

The current implementation consumes canonical local/dev keys in this grammar:

```text
pvg.mod0230.intake.{read|create|update|triage|route}
```

The earlier governance proposal also documented the business-readable grammar:

```text
pvg.case-intake-triage.{read|create|update|triage|route}
```

GMG owner review must decide the approved operational grammar, aliasing policy, and seed/grant ownership before any
permission seed, menu/catalog entry, or operational runtime exposure is added. The following action names remain out
of scope or permanently excluded:

```text
archive
export
delete
bulk-delete
bulk
void
```

No permission seed or operational grant is authorized by this annex.

## 8. Open Owner Inputs

| Input | Required owner response |
|---|---|
| FieldSecurity matrix | Approve allow/mask/omit/deny behavior for all 16 fields across list, detail, create, update, triage, route, audit, error, and any future export. |
| Process/state model | Approve state transitions, reason-code taxonomy, queue semantics, route target resolution, and blocked/unavailable behavior. |
| EvidenceLink completeness | Approve object reference shape, completeness rules, outage behavior, and whether any pending-evidence state is allowed. |
| Retention/legal-hold | Approve retention classes, legal-hold blocking behavior, and whether archive/void can ever be introduced. |
| Permission grammar | Approve final key grammar, aliases if any, actor-role matrix, QPPV dependency, and AuthService seed/grant owner. |
| Operational runtime | Explicitly approve environment, service boundary, route/menu/catalog exposure, appsettings policy, non-production adapter removal, and follow-up tests. |

Until these responses are supplied and signed where applicable, this annex remains draft IT input only:

```text
PVG build-test readiness: recorded separately
PVG operational readiness: 0% / NO-GO
GMG Records 1-9: unsigned
GMG Record 10: open
MOD-0231/MOD-0232/MOD-0234 runtime: blocked
```
