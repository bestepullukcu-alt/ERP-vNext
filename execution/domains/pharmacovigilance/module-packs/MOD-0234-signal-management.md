---
id: MOD-0234
name: Signal Management
domain: pharmacovigilance
service: TBD
shell: none
golden_reference: none
entity_base: TBD
status: draft
owner: TBD
branch: feature/pvg/mod-0234-signal-management
started: 2026-08-04
target: TBD
form_field_count: TBD
---

# MOD-0234 - Signal Management

> Draft planning artifact only. This module pack does not authorize runtime work. DCP-004 remains `draft`;
> production implementation stays blocked until DCP-004 is `approved` / `ready-for-execution`, this module pack
> is `approved` / `ready-for-dev`, upstream MOD-0230 / MOD-0231 / MOD-0232 contracts are approved, W-3A0 blockers
> are resolved or accepted through production-grade external contracts, and Signal MVP data-product / metric gates
> are closed.

> DCP-002 gate (2026-08-04): `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0234 --name "Signal Management"` -> `OK  MOD-0234: proven against Blueprint/registry.`

## Module Summary

MOD-0234 Signal Management is the canonical Pharmacovigilance signal-management module. Under DCP-004, this draft
is limited to Signal MVP contract, workflow boundary, object model, and interface gates only. It explicitly does
not create a Signal Management runtime shell, placeholder dashboard, endpoint, menu entry, fake data surface,
service scaffold, frontend page, gateway route, seed, job, collection, or test.

Signal MVP planning defines the minimum contract for consuming upstream case intake, signal-minimum case
processing, and MedDRA-coded terms, then shaping a signal hypothesis / evaluation / review decision boundary that
can later be implemented only after hard data-product, semantic metric, evidence, workflow, audit, masking, and
traceability gates are approved.

Blueprint / DCP-004 context:

| Field | Value |
|---|---|
| Canonical ID | MOD-0234 |
| Canonical name | Signal Management |
| DCP-004 delivery label | Urgent W-3D |
| DCP-004 first-stage scope | Signal MVP contract, workflow boundary, object model, and interface gates only |
| Runtime shell | Explicitly excluded |
| Upstream dependencies | MOD-0230, MOD-0231, MOD-0232 |
| Hard Signal MVP runtime gates | MOD-0004 Metric & Semantic Registry and MOD-0063 Data Warehouse / Lakehouse |
| Regulated-data posture | PHI/PII-sensitive, audit-grade, evidence-linked, workflow-gated, data-product-gated |

## Ownership and Boundaries

In scope for this draft:

- Signal MVP contract boundary for downstream Signal Management planning.
- Signal hypothesis, evaluation, review decision, linked evidence, and traceability object model planning.
- Interface gates for upstream MOD-0230, MOD-0231, and MOD-0232 consumption.
- Workflow boundary for signal review, triage, evaluation, decision, and closure planning.
- Data-product and semantic metric gates for MOD-0063 and MOD-0004.
- W-3A0, masking, evidence, audit, workflow, TRACE-BUNDLE, and regulated error-model blocker map.
- Open decisions required before any future `ready-for-dev` transition.

Out of scope for this draft:

- Runtime implementation of MOD-0234.
- Runtime shell, placeholder dashboard, placeholder endpoint, menu entry, seed, fake dashboard, fake data, or
  route shell.
- Frontend screens, Razor views, JavaScript, localization, gateway routes, service scaffold, database collections,
  migrations, jobs, tests, permission seeds, module catalog entries, appsettings changes, or runtime config.
- Full W-4 Signal Management workbench or production signal operations runtime.
- Data warehouse/lakehouse implementation, semantic metric registry implementation, workflow engine, evidence-link
  implementation, masking implementation, audit implementation, or governed-AI implementation.
- AI signal detection, AI summarization, AI recommendation, or automated signal scoring until governed-AI controls
  are approved.

## Owned Objects

Planned logical objects for Signal MVP planning, not runtime classes yet:

| Object | Ownership | Runtime status |
|---|---|---|
| Signal Candidate | MOD-0234 owns the candidate signal boundary after approved upstream handoff | Planned only |
| Signal Hypothesis | MOD-0234 owns hypothesis wording / classification contract | Planned only |
| Signal Evaluation | MOD-0234 owns evaluation state and minimum assessment boundary | Planned only |
| Signal Review Decision | MOD-0234 owns review outcome, decision rationale, and closure/escalation contract | Planned only |
| Signal Evidence Set | MOD-0234 defines required evidence boundaries; MOD-0031 owns evidence-link behavior | Planned only |
| Signal Workflow Instance Reference | MOD-0234 consumes MOD-0023 workflow/inbox semantics | Planned only |
| Signal Data Product Reference | MOD-0234 consumes MOD-0063 data-product contracts | Planned only |
| Signal Metric Reference | MOD-0234 consumes MOD-0004 semantic metric IDs | Planned only |
| Upstream Case Handoff | MOD-0234 consumes MOD-0231 signal-minimum handoff and MOD-0232 coded-term summary | Planned only |
| Signal Audit / Trace Contract | MOD-0234 defines auditable operation boundaries; MOD-0021 and TRACE-BUNDLE own platform behavior | Planned only |

Future runtime objects, repositories, commands, queries, DTOs, endpoints, frontend routes, and permissions are not
authorized by this draft. They must be finalized only after open decisions close.

## Entity Fields

No create/edit runtime surface is approved for the DCP-004 first-stage Signal MVP contract. Therefore:

- `form_field_count: TBD`
- `golden_reference: none`
- `shell: none`
- Slim/Compact decision for a future runtime Signal Management surface: **OPEN BLOCKER**

`golden_reference: none` is used because this first-stage pack is no-shell contract/object-model/interface-gate
planning only. It is not a DataTable module at this stage. If a future approved implementation introduces a
create/edit UI, this pack must be revised with a concrete field count and then choose `slim` or `compact`.

Approved Signal MVP object model fields for planning only:

| Field | Required | Source type | Sensitivity class | Notes / blocker |
|---|---|---|---|---|
| SignalCandidateId | Yes | server-resolved | regulated-safety | Generated under Blueprint MOD-0040 / TRACE-BUNDLE policy; no client-supplied ID. |
| TenantId | Yes | server-resolved | confidential | Server-resolved only; cross-tenant access must return 404/empty result without metadata leak. |
| CorrelationId | Yes | server-resolved | confidential | Required for trace stitching and regulated error model; blocked by TRACE-BUNDLE. |
| SourceCaseProcessingId | Yes | upstream-derived | regulated-safety, PHI | Same-tenant MOD-0231 Signal Minimum Scope handoff reference. |
| SourceCaseLifecycleState | Yes | upstream-derived | regulated-safety | Must be consumable state from MOD-0231; no assumed lifecycle progress. |
| SignalHandoffReadiness | Yes | upstream-derived | regulated-safety | Must be approved by MOD-0231 handoff contract. |
| SourceCaseSummary | Yes | upstream-derived | PHI, regulated-safety | Safe bounded summary only; raw narratives must be masked/redacted. |
| UpstreamEvidenceLinkIds | Yes | upstream-derived | confidential, PHI | MOD-0031 owns link/query/evidence-pack behavior. |
| CodedTermSetReference | Yes | upstream-derived | licensed-dictionary, regulated-safety | Approved MOD-0232 coded output only. |
| MeddraDictionaryVersionId | Yes | upstream-derived | licensed-dictionary | Inherited from MOD-0232; no unversioned coded-term consumption. |
| CodingApprovalStatus | Yes | upstream-derived | regulated-safety | Only approved coded output is consumable unless a degraded contract is explicitly approved. |
| SignalDataProductContractId | Yes | data-product-derived | data-product | MOD-0063 contract ID; hard Signal MVP runtime gate. |
| SignalCohortId | Yes | data-product-derived | data-product | Cohort identity from MOD-0063; no local fake cohort or aggregate. |
| SignalCohortAsOfUtc | Yes | data-product-derived | data-product | As-of timestamp and refresh semantics owned by MOD-0063. |
| SignalDataLineageReference | Yes | data-product-derived | data-product | Source lineage, extraction run, and contract version required. |
| SignalMetricId | Yes | metric-derived | semantic-metric | MOD-0004 semantic metric ID; no local metric literal. |
| SignalThresholdId | Yes | metric-derived | semantic-metric | MOD-0004 threshold concept; no invented threshold. |
| ObservedMetricValue | Yes | metric-derived | semantic-metric, data-product | Derived from approved MOD-0063 data product and MOD-0004 metric definition. |
| ThresholdComparisonResult | Yes | metric-derived | semantic-metric | Above, below, within, insufficient-data, or approved equivalent; exact enum owned by MOD-0004. |
| SignalHypothesis | Yes | user-entered | PHI, regulated-safety | Bounded hypothesis text; raw free text excluded from logs/traces/audit payloads unless redacted. |
| EvaluationSummary | Yes | user-entered | PHI, confidential, regulated-safety | Bounded evaluation summary; requires masking and audit payload allow-list. |
| ReviewDecision | Yes | user-entered | regulated-safety | Approved decision values only; workflow and RBAC gated. |
| DecisionRationale | No | user-entered | PHI, confidential, regulated-safety | Optional bounded rationale; not exportable unless masked policy permits. |
| WorkflowState | Yes | server-resolved | regulated-safety | MOD-0023 owned; invalid or unavailable workflow fails closed. |
| AssignedReviewQueue | No | server-resolved | confidential | Queue semantics owned by workflow; person/org/position references use MOD-0288 if required. |
| ReviewerActorReference | No | server-resolved | confidential | Actor reference only; organization/person/position details are not owned by MOD-0234. |
| ArchiveReason | No | user-entered | confidential, regulated-safety | Only usable after retention/legal-hold approval; archive remains blocked now. |

Server-resolved, upstream-derived, metric-derived, and data-product-derived fields are not create/edit user-entered
fields and do not resolve `form_field_count`. No field is implementation-ready until validation, masking, row/field
and aggregate access, audit, evidence, workflow, MOD-0004, MOD-0063, and TRACE-BUNDLE rules are approved.

### Upstream Inputs Consumed

MOD-0234 consumes upstream contracts. It must not re-own intake, case-processing, MedDRA coding, dictionary,
evidence-link, workflow, metric, or data-product ownership.

MOD-0231 Signal Minimum Scope inputs:

| MOD-0231 field / output | MOD-0234 use | Status |
|---|---|---|
| Case Processing ID | Required same-tenant source case-processing reference | BLOCKED until MOD-0231 contract approved |
| CaseProcessingPriority | Signal review prioritization context | BLOCKED by MOD-0231 / workflow policy |
| CaseValidityStatus | Candidate eligibility and trace context | BLOCKED by CASE-LIFECYCLE |
| CaseValidityReason | Restricted context; masked/redacted | BLOCKED by MOD-0019 / MOD-0021 |
| ProcessingOwnerQueue | Handoff and review queue context | BLOCKED by MOD-0023 |
| ProcessingDueAtUtc | SLA/timeliness context | BLOCKED by workflow/SLA policy |
| ProductExposureAssessment | Product/event signal context | BLOCKED by MOD-0231 policy |
| SeriousnessConfirmed | Safety context for Signal MVP | BLOCKED by MOD-0231 contract |
| EventAssessmentSummary | Restricted event context | BLOCKED by MOD-0019 / MOD-0021 |
| PreliminaryExpectedness | Preliminary expectedness context only | BLOCKED by MOD-0231 boundary approval |
| EvidenceCompletenessStatus | Evidence-readiness gate | BLOCKED by MOD-0031 |
| EvidenceGapReason | Restricted evidence context | BLOCKED by MOD-0031 / MOD-0019 |
| SignalRelevanceFlag | Candidate precondition / eligibility input | BLOCKED by MOD-0231 handoff contract |
| SignalRelevanceReason | Restricted handoff rationale | BLOCKED by MOD-0019 / MOD-0021 |
| SignalHandoffReadiness | Required Signal MVP consumption gate | BLOCKED until approved consumable state |
| SignalHandoffSummary | Safe bounded source summary | BLOCKED by MOD-0019 and MOD-0234 handoff contract |

MOD-0232 MedDRA Coding inputs:

| MOD-0232 field / output | MOD-0234 use | Status |
|---|---|---|
| sourceTermCandidateId | Trace back to coded source term | BLOCKED until MOD-0232 contract approved |
| meddraDictionaryVersionId | Version-bound dictionary context | BLOCKED by CODESET / MedDRA governance |
| meddraLltCode | Coded term input for signal grouping | BLOCKED by MOD-0232 approval |
| Server-resolved PT / HLT / HLGT / SOC hierarchy | Signal grouping and aggregate dimensions if license permits | BLOCKED by CODESET / license policy |
| codingMatchType | Coding quality/context signal | BLOCKED by MOD-0232 policy |
| codingDecisionReasonCode | Coding decision context | BLOCKED by MOD-0021 / MOD-0232 policy |
| codingRationale | Restricted coding rationale if allowed | BLOCKED by MOD-0019 / MOD-0021 |
| evidenceLinkIds | Coding evidence context | BLOCKED by MOD-0031 |
| CodingApprovalStatus | Only approved coded output is consumable by default | BLOCKED until MOD-0232 workflow approved |
| CodingDiffExportReference | Recoding/diff lineage for signal review packet | BLOCKED by MOD-0021 / MOD-0232 |

Inherited MOD-0230 trace context through MOD-0231:

| MOD-0230 field / output | MOD-0234 use | Status |
|---|---|---|
| Safety Case Intake ID | Upstream trace to original intake | BLOCKED until MOD-0230 handoff contract approved |
| TenantId | Server-resolved tenant isolation | BLOCKED by tenant/security gate |
| System-generated case/intake number | Display/trace reference only | BLOCKED by TRACE-BUNDLE |
| IntakeChannel | Source context | BLOCKED by MOD-0230 option-set contract |
| SourceType | Source context | BLOCKED by MOD-0230 option-set contract |
| SourceReference | External source trace, masked/redacted | BLOCKED by MOD-0019 and TRACE-BUNDLE |
| ReceivedAtUtc | Timeliness and as-of context | BLOCKED by workflow/SLA policy |
| ReporterType | Restricted source context | BLOCKED by MOD-0230 policy |
| PatientSubjectCode | Restricted PHI subject reference | BLOCKED by MOD-0019 |
| Seriousness | Initial seriousness baseline | BLOCKED by MOD-0230 seriousness contract |
| EvidenceLinkReferences | Intake evidence boundary | BLOCKED by MOD-0031 |
| Correlation ID / trace bundle | Trace stitching across intake, case processing, coding, and signal review | BLOCKED by Blueprint MOD-0040 / TRACE-BUNDLE |
| Workflow instance ID | Workflow continuity if approved | BLOCKED by MOD-0023 |

### MOD-0004 Metric Concepts

Minimum MOD-0004 concepts required for Signal MVP runtime:

| Concept | Required | Sensitivity class | Notes / blocker |
|---|---|---|---|
| SignalMetricId | Yes | semantic-metric | Stable semantic metric identifier; no local metric literals. |
| SignalMetricVersion | Yes | semantic-metric | Versioned definition for audit and reproducibility. |
| SignalMeasureDefinition | Yes | semantic-metric | Numerator, denominator, population, and time-window semantics. |
| SignalThresholdId | Yes | semantic-metric | Stable threshold identifier owned by MOD-0004. |
| ThresholdOperator | Yes | semantic-metric | Greater-than, less-than, range, trend, or approved equivalent. |
| ThresholdValueOrBand | Yes | semantic-metric | Value/band semantics owned by MOD-0004; no invented thresholds. |
| MetricObservationWindow | Yes | semantic-metric | Window boundaries and timezone/as-of semantics. |
| MetricInterpretationBand | No | semantic-metric | Optional severity/priority interpretation if approved. |
| InsufficientDataRule | Yes | semantic-metric | Required fail-closed rule when metric input is incomplete. |

### MOD-0063 Data Product / Cohort Concepts

Minimum MOD-0063 concepts and metadata required for Signal MVP runtime:

| Concept | Required | Sensitivity class | Notes / blocker |
|---|---|---|---|
| DataProductContractId | Yes | data-product | Stable contract id for signal source data product. |
| DataProductVersion | Yes | data-product | Versioned schema/contract for reproducibility. |
| CohortId | Yes | data-product | Cohort identity for aggregate input; no local fake cohorts. |
| CohortDefinitionHash | Yes | data-product | Immutable reference to cohort criteria/version. |
| CohortAsOfUtc | Yes | data-product | Data freshness and reproducibility timestamp. |
| RefreshCadence | Yes | data-product | Expected refresh semantics and stale-data behavior. |
| SourceLineage | Yes | data-product | Upstream source modules, extraction run, and transformation lineage. |
| QualityCompletenessStatus | Yes | data-product | Completeness/confidence gate for Signal MVP consumption. |
| AggregatePrivacyRule | Yes | data-product, confidential | Minimum cell-size, membership privacy, masking, and aggregate-only behavior. |
| AccessPolicyReference | Yes | data-product | Row/field/cohort access policy reference aligned with MOD-0019. |

## Repo Scope

Authorized by this draft:

- `execution/domains/pharmacovigilance/module-packs/MOD-0234-signal-management.md`

Future only, blocked until DCP-004 and this module pack pass approval gates:

- PVG runtime service path - TBD.
- PVG frontend paths - not authorized for this no-shell first-stage pack.
- PVG gateway route paths - not authorized for this no-shell first-stage pack.
- Signal data product / metric contract paths - TBD by MOD-0063 and MOD-0004 owners.
- PVG tests - TBD only after runtime scope is approved.

## Protected Paths

- `.antigravity/**`.
- `services/**` - no PVG runtime service scaffold is authorized by this draft.
- `frontend/**` - no Signal Management UI, shell, dashboard, page, menu entry, or fake data is authorized.
- `gateway/**` - no gateway route or placeholder endpoint is authorized.
- `gateway/Diten.ApiGateway/**/ocelot.json` - integration-agent owned if a future route is approved.
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`.
- `frontend/Diten.Web/Controllers/Archive/**`.
- `frontend/Diten.Web/Views/Archive/**`.
- Runtime appsettings, seed files, tests, menu/module catalog files, and service configuration files.
- `execution/portfolio/delivery-capability-packs/DCP-004-pvg-urgent-w3-development-block.md` - status remains unchanged.
- `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md` - consumed as upstream draft, not edited by this pack.
- `execution/domains/pharmacovigilance/module-packs/MOD-0231-case-processing.md` - consumed as upstream draft, not edited by this pack.
- `execution/domains/pharmacovigilance/module-packs/MOD-0232-meddra-coding.md` - consumed as upstream draft, not edited by this pack.
- Other domain module packs and runtime internals unless explicitly authorized by the user.

## Dependencies

W-3A0, upstream PVG modules, data-product, metric, masking, evidence, workflow, audit, and trace dependencies are
blockers, not waived:

| Dependency | Owning module / source | Status for MOD-0234 Signal MVP |
|---|---|---|
| DCP-004 | PVG Urgent W-3 Development Block | BLOCKER - currently `draft`; execution not authorized |
| W-3A0 REG-PV-BASE | PVG foundation remediation | BLOCKER |
| W-3A0 REG-SIGNAL-BASE | Signal foundation remediation | BLOCKER |
| MOD-0230 Case Intake & Triage | intake baseline, triage/routing, evidence boundary | BLOCKER - must be approved and compatible |
| MOD-0231 Case Processing | Signal Minimum Scope handoff, lifecycle state, signal-ready case data | BLOCKER - must be approved and compatible |
| MOD-0232 MedDRA Coding | coded-term summary, dictionary-version binding, coding diff/export | BLOCKER - must be approved and compatible |
| MOD-0004 Metric & Semantic Registry | semantic metric IDs, threshold/measure definitions | HARD SIGNAL MVP RUNTIME GATE |
| MOD-0063 Data Warehouse / Lakehouse | data-product contract IDs, cohorts, aggregate data products | HARD SIGNAL MVP RUNTIME GATE |
| MOD-0019 Data Masking & Row/Field Security | PHI/PII masking, row/field and aggregate access policy | BLOCKER |
| MOD-0021 Audit Trail Service | AuditEvent v1, safe metadata, regulated mutation audit | BLOCKER |
| MOD-0023 Workflow Designer / Workflow-Inbox v1 | review workflow, inbox assignment, decision state transitions | BLOCKER |
| MOD-0031 Evidence Linking Service | signal evidence links and evidence-pack completeness | BLOCKER |
| Blueprint MOD-0040 / TRACE-BUNDLE | canonical ID, external ID, correlation header, trace stitching, regulated error model | BLOCKER |
| OTel / operational telemetry | regulated observability and trace continuity | BLOCKER |
| CODESET / MedDRA source governance | inherited through MOD-0232 coded-term consumption | BLOCKER through MOD-0232 |

MOD-0004 and MOD-0063 are direct MOD-0234 Signal MVP runtime gates. Unlike MOD-0230 / MOD-0231 / MOD-0232,
Signal Management cannot treat them as downstream-only unless the approved scope removes signal analytics,
semantic metric IDs, cohorts, aggregates, and data-product outputs entirely.

### Required Interface Contracts Before `ready-for-dev`

| Owner | Required contract for MOD-0234 | Required MOD-0234 decision | Status |
|---|---|---|---|
| MOD-0230 | intake context and evidence boundary available to downstream signal trace | exact fields inherited or visible through MOD-0231 handoff | OPEN / BLOCKER |
| MOD-0231 | Signal Minimum Scope handoff, lifecycle state, signal-ready status, safe summary, evidence readiness | exact Signal MVP input shape and fail-closed behavior when case-processing handoff is unavailable | OPEN / BLOCKER |
| MOD-0232 | coded-term summary, MedDRA version, coding status, coding diff/export | exact coded-term consumption shape and behavior when coding is unavailable or unapproved | OPEN / BLOCKER |
| MOD-0004 | semantic metric ID contract, threshold IDs, signal measure definitions | minimum metric IDs and threshold semantics for Signal MVP | OPEN / HARD GATE |
| MOD-0063 | data-product contract IDs, cohort definitions, refresh/as-of semantics, aggregate lineage | minimum data products and lineage required for Signal MVP | OPEN / HARD GATE |
| MOD-0018 RBAC / permissions | permission keys, actor context, grant ownership, tenant authorization context | actor roles and permission matrix for read/review/evaluate/decide/escalate/export/archive or explicit de-scope | OPEN / BLOCKER |
| MOD-0019 masking / row-field security | field and aggregate sensitivity vocabulary, masking/omit/deny behavior, row/field/data-product access | per-field and per-aggregate sensitivity matrix for list/detail/review/export/audit | OPEN / BLOCKER |
| MOD-0021 AuditEvent v1 | append/event shape, safe metadata, redaction, critical audit failure policy, correlation propagation | audited operations, payload allow-list, and unavailable audit behavior | OPEN / BLOCKER |
| MOD-0023 Workflow/Inbox v1 | signal review workflow, inbox routing, assignment, escalation, closure transitions | review states and fail-closed behavior when workflow is unavailable | OPEN / BLOCKER |
| MOD-0031 Evidence-Link | object reference shape, link/query API, evidence set completeness, evidence-pack boundary | evidence required for candidate creation, evaluation, decision, escalation, and closure | OPEN / BLOCKER |
| Blueprint MOD-0040 / TRACE-BUNDLE | canonical/external ID semantics, `X-Correlation-Id`, trace stitching, regulated error model | signal candidate ID policy, external source ID policy, correlation propagation, error reason-code policy | OPEN / BLOCKER |

### MOD-0040 / MOD-0288 Identity Clarification

- Use **Blueprint MOD-0040 / TRACE-BUNDLE** for canonical IDs, external IDs, correlation headers, trace stitching,
  and regulated error-model decisions.
- Use **MOD-0288 Organization, Person & Position Directory** only if reviewer, escalation, assignment, queue,
  organization, person, or position references consume organization/person/position data.
- Do **not** use legacy deprecated repo `MOD-0040` as the organization/person source. In this repo,
  organization/person/position ownership is canonicalized to MOD-0288.

## Runtime Constraints

- No runtime service scaffold is authorized.
- No service port is reserved.
- No gateway route is authorized.
- No database collection, index, migration, seed, job, data-product publication, or metric registration is authorized.
- No runtime shell, frontend page, placeholder dashboard, placeholder endpoint, menu entry, or fake data is authorized.
- `shell: none` is intentional for this DCP-004 first-stage pack.
- `golden_reference: none` is intentional for this no-shell contract/object-model/interface-gate stage.
- `Diten.PvgService` cannot be created until DCP-004 is `approved` / `ready-for-execution` and the active member
  module pack is `approved` / `ready-for-dev`.
- Future runtime, if approved, is expected to use a dedicated `Diten.PvgService` boundary, but `service` remains
  `TBD` until explicit scaffold approval.
- `entity_base` remains `TBD` because this draft authorizes no runtime entity. Future recommendation:
  `EntityBase` only if later Diten-owned tenant signal runtime records are approved; data-product records and
  aggregates may instead be governed by MOD-0063 contracts.
- Tenant-owned runtime data, if approved, must carry server-resolved `TenantId`; client payloads must not accept
  `TenantId`. Cross-tenant reads or mutations must return 404/empty result with no metadata leak.
- Missing MOD-0231 handoff, missing MOD-0232 coded-term contract, missing MOD-0004 metric ID, missing MOD-0063
  data-product contract, missing workflow, missing evidence-link, missing audit, missing masking policy, or missing
  TRACE-BUNDLE context must fail closed.
- Raw PHI/PII, patient identifiers, reporter identifiers, source document content, free-text case narratives,
  signal rationale text, licensed dictionary text, unrestricted cohort details, and sensitive aggregate membership
  must not be written to logs, traces, metrics, audit payloads, validation errors, or regulated error responses
  unless explicitly allow-listed with redaction.
- Delete, archive, retention, and legal-hold behavior is undecided. Soft delete alone is not accepted for regulated
  signal records until retention/legal-hold rules are explicitly approved.

## Layout & Shell Contract

`shell: none`

MOD-0234 DCP-004 first-stage scope is no-shell contract/object-model/interface-gate planning only:

- No Razor layout is authorized.
- No MVC view folder is authorized.
- No frontend route is authorized.
- No placeholder dashboard is authorized.
- No menu entry is authorized.
- No fake data or sample signal screen is authorized.

If a later approved revision authorizes a Signal Management runtime UI, this section must be rewritten with the
exact shell, route surface, view root, layout, field count, Golden Reference choice, localization scope, and
DataTable verifier expectations.

## Backend File Convention

`service: TBD`

No backend implementation is authorized by this draft. The section below is a future convention only if MOD-0234
is later approved for runtime implementation:

```text
Features/SignalManagement/
├── Commands/
├── Queries/
├── Handlers/CommandHandlers/
├── Handlers/QueryHandlers/
├── Validators/
└── SignalManagementModels.cs
```

Possible future naming rules, not implementation authorization:

- Commands: `CreateSignalManagementCommand`, `UpdateSignalManagementCommand`, `ReviewSignalManagementCommand`,
  `EvaluateSignalManagementCommand`, `EscalateSignalManagementCommand`, `CloseSignalManagementCommand`, and
  archive/void commands only if corresponding operations are approved.
- Queries: `GetSignalManagementListQuery`, `GetSignalManagementByIdQuery`, `GetSignalManagementCandidateQuery`,
  and signal-data-product queries only if list/detail/review/data-product surfaces are approved.
- Handlers: `*Handler` only; no `CommandHandler`, `QueryHandler`, or `RequestHandler` suffix.
- Validators: `*Validator` only; no `CommandValidator` suffix.
- Forbidden: delete commands, bulk-delete commands, DELETE endpoints, bulk-delete endpoints, placeholder endpoints,
  fake dashboard endpoints, and data-product stubs that do not use approved MOD-0063 contracts.

## Frontend File Contract

`golden_reference: none`

No frontend implementation is authorized by this draft. No Signal Management UI shell exists in this first-stage
scope.

Future rules if runtime UI is later approved:

- If `form_field_count <= 8`, use Golden Reference Slim and Index-hosted create/edit offcanvas.
- If `form_field_count > 8`, use Golden Reference Compact and separate Create/Edit/Details pages.
- If the runtime remains backend/contract-only, keep `golden_reference: none` with a clear no-UI justification.

Until field count, UI scope, shell, route surface, data-product display authority, masking matrix, and metric
semantics are approved, no frontend files may be created.

## Validation Rules

Field-level validation is blocked until Signal MVP fields and data-product/metric contracts are approved. Minimum
validation topics that must be resolved before any future `ready-for-dev`:

| Field / rule area | Required | Rule | DB-level | Pre-check | Sensitivity / fail-closed requirement |
|---|---|---|---|---|---|
| Signal candidate identity | TBD | generated/external/duplicate policy TBD | TBD | TRACE-BUNDLE | no untraceable candidate state |
| MOD-0231 handoff reference | TBD | approved same-tenant signal-minimum handoff required | TBD | MOD-0231 | missing/unavailable handoff blocks candidate creation/review |
| MOD-0232 coded-term summary | TBD | approved coded-term summary and dictionary-version binding required if used | TBD | MOD-0232 / CODESET | unavailable or unapproved coding blocks coded-term-dependent signal decisions |
| Signal hypothesis | TBD | bounded hypothesis shape and text policy TBD | TBD | MOD-0019 / MOD-0021 | PHI/PII and free text masked/redacted; no raw audit/log payload |
| Signal evaluation status | TBD | approved workflow state set | TBD | MOD-0023 | invalid/unapproved transition fails closed |
| Review decision | TBD | approved decision values and reviewer authority | TBD | MOD-0018 / MOD-0023 | unauthorized decisions denied; audit required |
| Evidence set | TBD | evidence completeness rule TBD | TBD | MOD-0031 | no fake evidence pack when evidence service unavailable |
| Data-product reference | TBD | approved contract id, as-of time, lineage, and cohort policy | TBD | MOD-0063 | missing data-product contract blocks Signal MVP runtime |
| Semantic metric reference | TBD | approved metric id, threshold id, and measure semantics | TBD | MOD-0004 | missing metric id blocks Signal MVP runtime |
| Export/review packet | TBD | bounded payload and redaction policy TBD | TBD | MOD-0019 / MOD-0021 / MOD-0063 | PHI/PII, licensed terms, and sensitive aggregates excluded unless approved |

Every final field must have tests proving unauthorized, cross-tenant, missing-policy, masking-denied, evidence
unavailable, workflow unavailable, audit unavailable, metric unavailable, data-product unavailable, and upstream
handoff unavailable behavior.

### Signal MVP Workflow States

Approved Signal MVP workflow states for planning only:

```text
CandidateIdentified
CandidateScreening
EvidencePending
MetricReviewPending
EvaluationInProgress
ReviewRequired
DecisionPending
SignalConfirmed
SignalRefuted
Monitor
Escalated
Closed
Archived
```

Workflow ownership remains with MOD-0023. These states do not authorize runtime implementation and do not resolve
Workflow/Inbox, audit, evidence, masking, TRACE-BUNDLE, MOD-0004, MOD-0063, or upstream PVG blockers. `Archived`
is unusable until retention/legal-hold approval.

## Failure Path to Verify

Future implementation must verify at least these paths:

- **Missing approved MOD-0231 Signal Minimum Scope handoff**
  - Expected: candidate creation/review/evaluation blocks; no assumed signal candidate is created.
- **Missing approved MOD-0232 coded-term contract**
  - Expected: coded-term-dependent candidate creation/evaluation/export blocks; no unversioned coded-term signal data.
- **Missing MOD-0004 semantic metric ID**
  - Expected: Signal MVP runtime blocks; no local metric literal or invented threshold is used.
- **Missing MOD-0063 data-product contract**
  - Expected: Signal MVP runtime blocks; no fake cohort, aggregate, or data-product output is created.
- **Unauthorized actor**
  - Expected: 401/403 according to policy; no signal, case, evidence, cohort, or metric metadata leak.
- **Cross-tenant access**
  - Expected: 404 or empty result; no cross-tenant data or aggregate membership leak.
- **Missing MOD-0019 policy for sensitive fields or aggregates**
  - Expected: field/aggregate omitted, masked, aggregate-only, or operation denied; no permissive fallback.
- **Workflow/Inbox unavailable**
  - Expected: review/evaluation/decision/escalation blocks; no untraceable signal progression.
- **Evidence-link unavailable**
  - Expected: candidate/evaluation/decision blocks or follows an explicitly approved degraded path; no fake evidence set.
- **Audit sink unavailable**
  - Expected: regulated mutation is blocked or queued according to approved MOD-0021 contract; no unaudited mutation.
- **Correlation/trace context missing**
  - Expected: behavior follows Blueprint MOD-0040 / TRACE-BUNDLE decision; no untraceable regulated state changes.
- **Sensitive content appears in audit/log/trace/metric payload**
  - Expected: test fails; raw PHI/PII/free text/licensed dictionary text and sensitive aggregate membership must not
    be persisted to logs, traces, metrics, audit payloads, validation errors, or regulated error details.
- **Placeholder shell/endpoint/dashboard/menu attempted**
  - Expected: absent or denied; DCP-004 first-stage scope authorizes no runtime shell.

## Authorization Convention

Permission prefix proposal for future tenant/domain implementation:

```text
pvg.signal-management.read
pvg.signal-management.screen-candidate
pvg.signal-management.review
pvg.signal-management.review-metrics
pvg.signal-management.evaluate
pvg.signal-management.decide
pvg.signal-management.escalate
pvg.signal-management.export
pvg.signal-management.archive
```

Explicitly excluded unless a later retention/legal-hold decision approves otherwise:

```text
pvg.signal-management.delete
pvg.signal-management.bulk-delete
```

Approved actor role / permission matrix for planning only:

| Role | read | screen-candidate | review | review-metrics | evaluate | decide | escalate | export | archive |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| PVG Signal Reviewer | Assigned / permitted signals | Yes | Yes | Read metric packet | Yes | Recommend only | No | No | No |
| PVG Signal Lead | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Masked only | Only after retention/legal-hold approval |
| PVG Safety Manager | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Masked only | Only after retention/legal-hold approval |
| PVG Compliance Auditor | Read-only masked | No | No | Masked audit packet | No | No | No | Masked audit packet only | No |
| Data Product Integration Actor | Contract-only | No | No | Contract-only | No | No | No | Contract-only | No |
| Metric Registry Integration Actor | Contract-only | No | No | Contract-only | No | No | No | Contract-only | No |
| PVG System Integration | Approved handoff only | Contract-limited | No | No | No | No | Event/API only | No | No |

Open authorization decisions:

- Final actor role names, actor type mapping, and seed/grant ownership require MOD-0018 / AuthService approval.
- Data-product and metric access scopes require MOD-0063 and MOD-0004 approval.
- Archive permission is unusable until retention/legal-hold policy is approved.
- Export is masked-only unless a later approved field and data-product policy permits more.
- PHI/PII, licensed-dictionary, semantic-metric, and data-product field-level authorization must align with
  MOD-0019, MOD-0004, and MOD-0063 before runtime.
- Permission seed/grant ownership must remain with MOD-0018 / AuthService; this draft authorizes no seed.

No permission seed is authorized by this draft.

## Gateway / API Routing Decision

Decision: no gateway route is authorized by this draft.

DCP-004 first-stage MOD-0234 is no-shell contract/object-model/interface-gate planning only. It must not create:

- Signal Management API route shell;
- placeholder endpoint;
- fake dashboard endpoint;
- menu route;
- frontend proxy route;
- data-product stub endpoint.

If a future approved revision authorizes runtime API, the route decision must define service/deployment owner,
upstream API base path, downstream path, auth/correlation/error-model behavior, data-product and metric access
contracts, OPTIONS/CORS handling if applicable, and integration-agent ownership for
`gateway/Diten.ApiGateway/**/ocelot.json`.

Direct service-port calls from frontend remain forbidden.

## Acceptance Criteria

Acceptance criteria for this draft pack:

- [x] Pack exists at `execution/domains/pharmacovigilance/module-packs/MOD-0234-signal-management.md`.
- [x] Status is `draft`.
- [x] Canonical name is exactly `Signal Management`.
- [x] DCP-002 preflight passed for MOD-0234.
- [x] DCP-004 remains `draft`; no execution is authorized.
- [x] MOD-0234 is recorded as Signal MVP contract, workflow boundary, object model, and interface gates only.
- [x] No runtime shell, frontend page, service scaffold, gateway route, menu entry, seed, fake dashboard,
      placeholder endpoint, appsettings change, or test is authorized.
- [x] W-3A0, MOD-0230, MOD-0231, MOD-0232, MOD-0004, MOD-0063, MOD-0019, MOD-0031, workflow, audit,
      TRACE-BUNDLE, and evidence dependencies are recorded as blockers.
- [x] MOD-0004 and MOD-0063 are recorded as hard Signal MVP runtime gates.
- [x] `shell` is `none` for this no-shell first-stage pack.
- [x] `golden_reference` is `none` because this first-stage pack has no DataTable/runtime UI.
- [x] `form_field_count` remains `TBD`; future Slim/Compact choice is an open decision, not guessed.
- [x] Signal MVP object model fields are recorded for planning, with required/optional status, source type, and
      sensitivity class.
- [x] Exact upstream input lists are recorded for MOD-0231 Signal Minimum Scope, MOD-0232 MedDRA Coding, and
      inherited MOD-0230 trace context.
- [x] Minimum MOD-0004 metric and threshold concepts are recorded as hard gates, not resolved.
- [x] Minimum MOD-0063 data-product/cohort concepts and metadata requirements are recorded as hard gates, not resolved.
- [x] Signal MVP workflow states are recorded for planning.
- [x] Actor roles and permission matrix are recorded for planning.
- [x] Delete and bulk-delete are explicitly excluded.
- [x] Archive remains blocked until retention/legal-hold approval.
- [x] MOD-0040 / TRACE-BUNDLE vs MOD-0288 identity distinction is recorded.

Acceptance criteria before any future implementation can start:

- [ ] DCP-004 is `approved` / `ready-for-execution`.
- [ ] This module pack is `approved` / `ready-for-dev`.
- [ ] MOD-0230 intake, triage, routing, and evidence boundary contract is approved and compatible.
- [ ] MOD-0231 Signal Minimum Scope handoff contract is approved and compatible.
- [ ] MOD-0232 coded-term, dictionary-version, and coding diff/export contract is approved and compatible.
- [ ] W-3A0 REG-PV-BASE and REG-SIGNAL-BASE dependencies are closed or explicitly satisfied by production-grade
      external contracts.
- [ ] MOD-0004 semantic metric IDs and threshold/measure definitions are approved.
- [ ] MOD-0063 data-product contract IDs, cohort definitions, lineage, and refresh/as-of semantics are approved.
- [ ] Required contracts are concrete for MOD-0018, MOD-0019, MOD-0021, MOD-0023, MOD-0031, Blueprint MOD-0040 /
      TRACE-BUNDLE, OTel, MOD-0004, MOD-0063, MOD-0230, MOD-0231, and MOD-0232.
- [ ] Runtime/no-runtime decision, service boundary, `entity_base`, create/edit fields, field count,
      Slim/Compact/none decision, workflow states, permissions, gateway routing, retention/legal-hold policy, and
      tests are fully specified.

## Test Expectations

No runtime tests are expected for this draft because no runtime files are authorized.

Future implementation test expectations must include:

- DCP-002 identity proof remains valid.
- Tests proving no runtime shell, placeholder dashboard, placeholder endpoint, menu entry, fake data, route, seed,
  or service scaffold is introduced by this first-stage pack.
- Tenant isolation and regulated-data masking tests.
- MOD-0230 / MOD-0231 / MOD-0232 upstream contract tests.
- MOD-0004 semantic metric ID and threshold contract tests.
- MOD-0063 data-product contract, lineage, cohort, refresh/as-of, and unavailable-data-product tests.
- Per-field and per-aggregate PHI/PII/licensed-dictionary/data-product sensitivity, masking, row/field deny, and
  missing-policy fail-closed tests.
- Audit, correlation/TRACE-BUNDLE, evidence-link, workflow/inbox, metric, and data-product failure-path tests.
- Tests proving raw PHI/PII/free text/licensed dictionary text and sensitive aggregate membership are absent from
  logs, traces, metrics, audit payloads, validation errors, and regulated error responses unless explicitly
  allow-listed with redaction.
- Frontend build and DataTable verifier only if a later approved revision authorizes frontend and chooses
  Slim/Compact.
- Gateway route smoke only after integration-agent-owned route approval.

## Ready-for-dev Checklist

- [x] Required governance files read.
- [x] Golden Reference Slim and Compact module packs read.
- [x] DCP-002 preflight passed.
- [x] Pack status is `draft`.
- [x] No-shell first-stage posture recorded.
- [ ] DCP-004 promoted to `approved` / `ready-for-execution`.
- [ ] MOD-0230 contract approved.
- [ ] MOD-0231 Signal Minimum Scope handoff contract approved.
- [ ] MOD-0232 coded-term / dictionary-version / diff-export contract approved.
- [ ] W-3A0 owner and closure criteria recorded for REG-PV-BASE and REG-SIGNAL-BASE.
- [ ] MOD-0004 semantic metric and threshold contracts approved.
- [ ] MOD-0063 data-product, cohort, lineage, and refresh/as-of contracts approved.
- [ ] MOD-0018 RBAC actor/permission matrix approved.
- [ ] MOD-0019 per-field/per-aggregate sensitivity, masking, row/field access, and fail-closed tests approved.
- [ ] MOD-0021 AuditEvent v1 event names, payload allow-list, signal decision audit shape, and failure behavior approved.
- [ ] MOD-0023 workflow/inbox states, assignments, review gates, escalation, closure, and unavailable-workflow behavior approved.
- [ ] MOD-0031 evidence-link object reference shape and evidence completeness behavior approved.
- [ ] Blueprint MOD-0040 / TRACE-BUNDLE ID, correlation header, trace stitching, and regulated error model approved.
- [x] Signal MVP object model fields, required/optional classification, source type, and sensitivity class recorded
      for planning.
- [x] MOD-0231, MOD-0232, and inherited MOD-0230 input lists recorded for planning.
- [x] Minimum MOD-0004 metric/threshold concepts recorded for planning.
- [x] Minimum MOD-0063 data-product/cohort concepts and metadata requirements recorded for planning.
- [x] Signal MVP workflow states recorded for planning.
- [x] Actor role and permission matrix recorded for planning.
- [x] Delete and bulk-delete exclusions recorded.
- [x] Archive/legal-hold blocker recorded.
- [x] MOD-0040 / TRACE-BUNDLE vs MOD-0288 distinction recorded.
- [ ] Runtime/no-runtime decision after DCP-004 first stage approved.
- [ ] `service`, deployment boundary, `entity_base`, future `shell`, route surface, `form_field_count`, and
      `golden_reference` resolved if runtime is added.
- [ ] Create/edit fields, required/optional classification, validation rules, workflow rules, data-product rules,
      semantic metric rules, and field-level tests approved if runtime is added.
- [ ] Delete/retention/legal-hold policy approved.
- [ ] Build/buy/partner boundary for Signal Management and data products approved.

## Implementation Notes

- Use canonical name exactly: `Signal Management`.
- Treat DCP-004 W-3D as delivery planning context only; it does not authorize runtime work.
- Treat this pack as no-shell contract/object-model/interface-gate only. A shell, menu entry, dashboard, endpoint,
  fake data, or service scaffold would violate DCP-004.
- Frontmatter decisions preserved after Signal MVP planning reconciliation: `service: TBD`, `shell: none`,
  `golden_reference: none`, `entity_base: TBD`, `status: draft`, and `form_field_count: TBD`.
- Entity-base recommendation recorded: use `EntityBase` only if later Diten-owned tenant signal runtime records are
  approved; data-product records and aggregates remain governed by MOD-0063 contracts.
- Signal MVP object model fields, upstream input lists, MOD-0004 metric/threshold concepts, MOD-0063 data-product /
  cohort concepts, workflow states, actor roles, permission matrix, and MOD-0040 / MOD-0288 clarification were
  recorded for planning. They do not resolve W-3A0, MOD-0230, MOD-0231, MOD-0232, MOD-0004, MOD-0063, MOD-0019,
  MOD-0021, MOD-0023, MOD-0031, TRACE-BUNDLE, OTel, evidence, workflow, audit, masking, retention, or runtime gates.
- Treat MOD-0230, MOD-0231, and MOD-0232 as hard upstream blockers. MOD-0234 must not redefine intake records,
  case-processing state, MedDRA coding, dictionary versioning, or evidence-link ownership.
- Treat MOD-0004 and MOD-0063 as hard Signal MVP runtime gates. Do not invent metric literals, threshold IDs,
  cohorts, aggregate objects, or data-product IDs inside MOD-0234.
- Use Blueprint MOD-0040 / TRACE-BUNDLE for canonical ID, correlation header, trace stitching, and regulated error
  behavior if runtime is later approved.
- Use MOD-0288 only if reviewer, escalation, assignment, organization, person, or position references consume
  organization/person/position data. Do not use legacy deprecated repo MOD-0040 as the organization/person source.
- Keep AI signal detection, AI summarization, AI recommendation, and automated scoring out of scope until governed-AI
  controls are explicitly available and accepted.
- No service, frontend, gateway, runtime, appsettings, seed, menu, fake dashboard, placeholder endpoint, or test file
  is in scope for this draft.

## Follow-up Items

- Close W-3A0 owner and closure criteria for REG-PV-BASE and REG-SIGNAL-BASE.
- Approve MOD-0230 intake/triage/evidence handoff contract.
- Approve MOD-0231 Signal Minimum Scope handoff, lifecycle, signal-ready, and evidence-readiness contract.
- Approve MOD-0232 coded-term summary, MedDRA version, coding diff/export, and CODESET dependency contract.
- Define MOD-0004 semantic metric IDs, threshold IDs, and measure semantics required for Signal MVP.
- Define MOD-0063 data-product contract IDs, cohorts, aggregates, lineage, refresh/as-of semantics, and access model.
- Approve concrete MOD-0004 semantic metric IDs, threshold IDs, measure semantics, insufficient-data behavior, and
  threshold comparison enums against the planning concepts recorded here.
- Approve concrete MOD-0063 data-product contract IDs, cohorts, aggregates, lineage, refresh/as-of semantics,
  quality/completeness rules, aggregate privacy policy, and access model against the planning concepts recorded here.
- Resolve detailed masking behavior, row/field and aggregate access rules, audit payload allow-list, evidence
  requirements, workflow transition rules, and export/review packet payloads for the recorded Signal MVP fields.
- If runtime UI is later approved, define create/edit field count and revise Slim/Compact/none decision.
- Decide runtime/no-runtime boundary after the first-stage contract is reviewed.
- Decide delete/retention/legal-hold policy.
- Decide build/buy/partner boundary for Signal Management runtime, data products, and optional AI-assisted signal
  behavior.
