---
id: MOD-0232
name: MedDRA Coding
domain: pharmacovigilance
service: TBD
shell: tenant
golden_reference: slim
entity_base: EntityBase
status: draft
owner: TBD
branch: feature/pvg/mod-0232-meddra-coding
started: 2026-08-04
target: TBD
form_field_count: 7
---

# MOD-0232 - MedDRA Coding

> Draft planning artifact only. This module pack does not authorize runtime work. DCP-004 remains `draft`;
> production implementation stays blocked until DCP-004 is `approved` / `ready-for-execution`, this module pack
> is `approved` / `ready-for-dev`, MOD-0231 Case Processing contracts are approved, CODESET and MedDRA source
> governance are closed, and W-3A0 blockers are resolved or accepted through production-grade external contracts.

> DCP-002 gate (2026-08-04): `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0232 --name "MedDRA Coding"` -> `OK  MOD-0232: proven against Blueprint/registry.`

## Module Summary

MOD-0232 MedDRA Coding is the canonical Pharmacovigilance terminology-coding module for assigning MedDRA-coded
terms to case-processing source terms. Under DCP-004, this draft covers the urgent W-3C planning contract only:
dictionary-version binding, coded-term assignment boundaries, coding audit trail, and diff/export semantics needed
by downstream PVG signal work.

This draft does not implement MedDRA runtime, import a dictionary, redistribute dictionary content, create a coding
workbench, add static UI terms, scaffold a PVG service, create frontend pages, add gateway routes, seed
permissions, or change runtime files.

Blueprint / DCP-004 context:

| Field | Value |
|---|---|
| Canonical ID | MOD-0232 |
| Canonical name | MedDRA Coding |
| DCP-004 delivery label | Urgent W-3C |
| Primary upstream dependency | MOD-0231 Case Processing |
| Foundation blocker | CODESET plus W-3A0 PV foundations |
| Dictionary blocker | MedDRA source, license, versioning, import policy, and validation governance |
| Downstream consumer | MOD-0234 Signal Management / Signal MVP |
| Regulated-data posture | PHI/PII-sensitive, audit-grade, evidence-linked, workflow-gated |

## Ownership and Boundaries

In scope for this draft:

- MedDRA Coding canonical boundary for PVG coded-term assignment planning.
- Case-processing source term to MedDRA coded-term assignment contract.
- MedDRA dictionary source, license, version, import, validation, and update-cadence governance blockers.
- Dictionary-version binding rule for every coding assignment.
- Coding audit trail, review workflow, evidence-link, and diff/export contract requirements.
- W-3A0, MOD-0231, CODESET, masking, audit, workflow, evidence, and trace dependency map.
- Future readiness questions and blockers before `ready-for-dev`.

Out of scope for this draft:

- Production runtime implementation of MOD-0232.
- MedDRA dictionary import, storage, search, browse, update, or redistribution implementation.
- Hardcoded MedDRA terms in UI, source code, seeds, fixtures, or static assets.
- Full case processing, owned by MOD-0231.
- Signal hypothesis, review, and evaluation, owned by MOD-0234.
- W-3A0 foundation remediation development.
- Runtime service scaffold, frontend UI, gateway routes, database collections, seed data, jobs, tests, permission
  seeds, module catalog/menu entries, or appsettings changes.
- AI coding recommendation, auto-coding, or medical coding decision support unless explicitly added by a later
  approved revision with governance controls.

## Owned Objects

Planned logical objects for MOD-0232, not runtime classes yet:

| Object | Ownership | Runtime status |
|---|---|---|
| Coding Work Item | MOD-0232 owns the coding task boundary for case-processing source terms | Planned only |
| Case Term Candidate | MOD-0232 consumes source term candidates from MOD-0231; it does not own the case master | Planned only |
| MedDRA Dictionary Version Reference | MOD-0232 binds assignments to an approved dictionary source/version contract | Planned only |
| Coded Term Assignment | MOD-0232 owns selected MedDRA code reference, assignment status, and version binding | Planned only |
| Coding Decision / Reason | MOD-0232 owns coding reason/status metadata, subject to masking and audit allow-lists | Planned only |
| Coding Review Workflow Reference | MOD-0232 consumes MOD-0023 workflow/inbox states for review and approval | Planned only |
| Coding Audit Trail | MOD-0232 defines audit-event requirements; MOD-0021 owns AuditEvent v1 | Planned only |
| Coding Evidence Requirement | MOD-0232 defines evidence-link requirements; MOD-0031 owns link/query behavior | Planned only |
| Coding Diff / Export Contract | MOD-0232 owns bounded diff/export semantics for downstream consumers | Planned only |

Future runtime objects, repositories, commands, queries, DTOs, endpoints, frontend routes, and permissions are not
authorized by this draft. They must be finalized after open decisions close.

## Entity Fields

Recorded draft-planning create/edit user-entered field count: `7`.

Golden Reference decision: `slim`, because the create/edit form has 8 or fewer user-entered fields. The future
tenant UI may use Index-hosted create/edit offcanvas only after runtime gates and MedDRA display authority are
approved.

Excluded from field count: PT/HLT/HLGT/SOC resolved hierarchy fields, coder identity, reviewer identity,
timestamps, workflow state, tenant, audit fields, trace/correlation fields, assignment IDs, case-processing
reference metadata, generated coding work item IDs, and dictionary import/source metadata.

| Field | Required | Sensitivity class | User-entered | Notes / blocker |
|---|---|---|---|---|
| sourceTermCandidateId | Yes | PHI, regulated-safety | Yes | Must reference an approved same-tenant MOD-0231 source term candidate; raw source term display is policy-controlled. |
| meddraDictionaryVersionId | Yes | licensed-dictionary | Yes | Must reference an approved MedDRA source/version; CODESET and license governance remain blockers. |
| meddraLltCode | Yes | licensed-dictionary | Yes | Must validate against the selected dictionary version; PT/HLT/HLGT/SOC are server-resolved. |
| codingMatchType | Yes | regulated-safety | Yes | Controlled option set; invalid match type fails closed. |
| codingDecisionReasonCode | Yes | regulated-safety | Yes | Controlled reason code; free-text reason must not replace reason-code governance. |
| codingRationale | No | confidential, PHI, regulated-safety | Yes | Raw rationale prohibited in logs/traces/metrics/audit payloads unless redacted and allow-listed. |
| evidenceLinkIds | Conditionally required | confidential, PHI | Yes | Required when the recorded workflow/review rule requires evidence; MOD-0031 owns link validity. |

Server-resolved fields must not be user-entered or counted as form fields:

- PT, HLT, HLGT, and SOC terms/codes derived from `meddraLltCode` and `meddraDictionaryVersionId`.
- Coder identity, reviewer identity, assignment IDs, timestamps, workflow state, tenant, audit, trace/correlation,
  and generated coding work item IDs.
- MOD-0231 case/source-term context that is resolved from `sourceTermCandidateId`.

No field is implementation-ready until validation, masking, row/field access, audit, evidence, workflow, CODESET,
and MedDRA source/license/versioning rules are approved.

## Repo Scope

Authorized by this draft:

- `execution/domains/pharmacovigilance/module-packs/MOD-0232-meddra-coding.md`

Future only, blocked until DCP-004 and this module pack pass approval gates:

- PVG runtime service path - planned future dedicated `Diten.PvgService`; frontmatter `service` remains `TBD`
  until explicit service scaffold approval.
- PVG frontend paths - planned tenant MVC surface under
  `frontend/Diten.Web/Views/Pharmacovigilance/MeddraCoding/**`.
- PVG gateway route paths - future API remains Gateway-owned, TBD, and not authorized by this draft.
- PVG tests - TBD after service/frontend boundaries are approved.
- MedDRA dictionary import/storage/search paths - TBD and additionally blocked by CODESET and source/license policy.

## Protected Paths

- `.antigravity/**`.
- `services/**` - no PVG runtime service scaffold is authorized by this draft.
- `frontend/**` - no PVG UI is authorized by this draft.
- `gateway/**` - no gateway route is authorized by this draft.
- `gateway/Diten.ApiGateway/**/ocelot.json` - integration-agent owned if a future route is approved.
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`.
- `frontend/Diten.Web/Controllers/Archive/**`.
- `frontend/Diten.Web/Views/Archive/**`.
- Runtime appsettings, seed files, tests, and service configuration files.
- `execution/portfolio/delivery-capability-packs/DCP-004-pvg-urgent-w3-development-block.md` - status remains unchanged.
- `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md` - consumed as an upstream draft, not edited by this pack.
- `execution/domains/pharmacovigilance/module-packs/MOD-0231-case-processing.md` - consumed as an upstream draft, not edited by this pack.
- Other domain module packs and runtime internals unless explicitly authorized by the user.

## Dependencies

W-3A0, MOD-0231, CODESET, and MedDRA source governance dependencies are blockers, not waived:

| Dependency | Owning module / source | Status for MOD-0232 |
|---|---|---|
| DCP-004 | PVG Urgent W-3 Development Block | BLOCKER - currently `draft`; execution not authorized |
| MOD-0231 Case Processing | source term/case-processing contract and Signal Minimum Scope handoff | BLOCKER - must define approved source term and lifecycle contract |
| CODESET | foundation dictionary/codeset authority | BLOCKER - dictionary version binding, validation, and code reference contract required |
| MedDRA source/license/versioning/import policy | business/legal/governance decision | BLOCKER - source, license, allowed storage/use, import validation, release cadence, and redistribution policy are open |
| REG-PV-BASE | DCP-004 minimum integration contract | BLOCKER |
| CASE-LIFECYCLE | W-3A0 foundation dependency | BLOCKER insofar as coding is tied to case state and review progression |
| SSO + RBAC/ABAC | MOD-0018 RBAC / permissions plus Platform/Auth foundations | BLOCKER |
| PHI/PII masking hooks | MOD-0019 Data Masking & Row/Field Security | BLOCKER |
| AuditEvent v1 | MOD-0021 Audit Trail Service | BLOCKER |
| Workflow/Inbox v1 | MOD-0023 Workflow Designer | BLOCKER |
| Evidence-Link | MOD-0031 Evidence Linking Service | BLOCKER |
| TRACE-BUNDLE: canonical ID, Correlation-ID, trace stitching, regulated error model | Blueprint MOD-0040 / platform trace standards | BLOCKER |
| OTel / operational telemetry | Platform observability foundations | BLOCKER |
| MOD-0234 Signal Management | downstream coded-term consumer | Downstream consumer; requires bounded coding diff/export contract |

MOD-0004 Metric & Semantic Registry and MOD-0063 Data Warehouse / Lakehouse are not direct MOD-0232 runtime
blockers unless this module's approved scope emits signal analytics, semantic metric IDs, or data-product outputs.
They remain downstream DCP-004 / MOD-0234-facing gates unless explicitly added to MOD-0232 scope.

### Required Interface Contracts Before `ready-for-dev`

| Owner | Required contract for MOD-0232 | Required MOD-0232 decision | Status |
|---|---|---|---|
| MOD-0231 | case-processing source term reference, lifecycle state allowed for coding, same-tenant case context, source-term sensitivity metadata | exact source fields consumed, required/optional classification, and fail-closed behavior when MOD-0231 contract is unavailable | OPEN / BLOCKER |
| CODESET | codeset identity, dictionary version model, validation API/contract, allowed code-reference shape, version immutability rules | concrete MedDRA code reference model and invalid/unavailable dictionary behavior | OPEN / BLOCKER |
| MedDRA source governance | source/provider, license, release/version, import validation, storage/search/redistribution policy, update cadence | whether dictionary terms may be stored, searched, displayed, exported, cached, or logged | OPEN / BLOCKER |
| MOD-0018 RBAC / permissions | canonical permission keys, seed/grant ownership, actor context, tenant authorization context, optional data-scope shape | actor roles and permission matrix for read/create/update/assign-code/review/export/archive or explicit de-scope | OPEN / BLOCKER |
| MOD-0019 masking / row-field security | field sensitivity vocabulary, masking/omit/deny behavior, row-scope and field-scope evaluation, unavailable-policy behavior | per-field sensitivity matrix for source terms, coded terms, decisions, review notes, exports, list/detail/search/audit | OPEN / BLOCKER |
| MOD-0021 AuditEvent v1 | append/event shape, safe metadata envelope, redaction rules, critical audit failure policy, correlation propagation | audited operations, coding diff allow-list, dictionary-version audit payload, and unavailable audit behavior | OPEN / BLOCKER |
| MOD-0023 Workflow/Inbox v1 | coding assignment, review, approval, return, hold, inbox handoff, transition failure behavior | coding workflow states and whether assignment/review blocks without workflow | OPEN / BLOCKER |
| MOD-0031 Evidence-Link | object reference shape, link/query API, evidence requirement/completeness rule, evidence-pack boundary | evidence required for assignment, review, recoding, and export | OPEN / BLOCKER |
| Blueprint MOD-0040 / TRACE-BUNDLE | canonical/external ID semantics, `X-Correlation-Id`, trace stitching, regulated error model | coding work item ID policy, assignment ID policy, correlation propagation, regulated error reason codes | OPEN / BLOCKER |
| MOD-0234 | downstream coded-term consumption, signal-ready coding summary, diff/export payload | bounded coded-term handoff shape and consumer acceptance criteria | OPEN / BLOCKER |

### MOD-0040 / MOD-0288 Identity Clarification

- Use **Blueprint MOD-0040 / TRACE-BUNDLE** for canonical IDs, external IDs, correlation headers, trace stitching,
  and regulated error-model decisions.
- Use **MOD-0288 Organization, Person & Position Directory** only if coder/reviewer assignment, queue ownership,
  escalation, organization, person, or position references consume organization/person/position data.
- Do **not** use legacy deprecated repo `MOD-0040` as the organization/person source. In this repo,
  organization/person/position ownership is canonicalized to MOD-0288.

## Runtime Constraints

- No runtime service scaffold is authorized.
- No service port is reserved.
- No gateway route is authorized.
- No database collection, index, migration, seed, import job, scheduler, cache, or search index is authorized.
- No UI shell, DataTable page, coding workbench, dictionary browser, or static MedDRA data is authorized.
- `Diten.PvgService` cannot be created until DCP-004 is `approved` / `ready-for-execution` and the active member
  module pack is `approved` / `ready-for-dev`.
- Recommended future service boundary is a dedicated `Diten.PvgService`. The same future service boundary should
  host MOD-0230, MOD-0231, and MOD-0232 PVG runtime behavior if approved.
- `service` remains `TBD` in frontmatter until explicit service scaffold approval. This draft does not reserve a
  service port or create a service folder.
- `entity_base: EntityBase` remains correct for Diten-owned tenant coding work items, coding assignments,
  append-only recoding/diff records, archive/void metadata if later approved, and audit/evidence/workflow-linked
  records under the future PVG boundary. Partner-native or dictionary-provider records remain outside the repo
  entity model unless an approved adapter/source contract maps them.
- MedDRA dictionary terms must not be hardcoded into source code, UI, static assets, seed data, tests, or fixtures
  unless a later approved license/source policy explicitly allows the specific use.
- Every coding assignment must bind to an approved dictionary source/version. Assignments must be immutable for
  their original dictionary version; recoding or version updates require append-only diff/audit behavior.
- Future runtime must resolve tenant isolation, regulated data masking, audit, evidence links, workflow/inbox
  handoff, OTel, correlation ID, licensed dictionary handling, and regulated error model before acceptance.
- Tenant-owned runtime data, if approved, must carry server-resolved `TenantId`; client payloads must not accept
  `TenantId`. Cross-tenant reads or mutations must return 404/empty result with no metadata leak.
- Missing MOD-0231 source contract, missing CODESET/dictionary version, missing MedDRA license/source approval,
  missing workflow gate, missing evidence-link contract, missing audit contract, or missing masking policy must fail
  closed. The module must not create unversioned, unaudited, unlicensed, or untraceable coding assignments.
- Raw PHI/PII, patient identifiers, reporter identifiers, source document content, free-text source terms,
  unrestricted coding notes, and licensed dictionary text must not be written to logs, traces, metrics, audit
  payloads, or regulated error responses unless explicitly allow-listed with redaction.
- Delete and bulk-delete are excluded. Archive/void remains blocked until retention/legal-hold approval. Soft delete
  alone is not accepted for regulated coding records until retention/legal-hold rules are explicitly approved.

## Layout & Shell Contract

`shell: tenant`

MOD-0232 MedDRA Coding is a tenant/domain operational workflow surface, not a platform-admin configuration module.

- Razor layout: every future `.cshtml` page must explicitly set `Layout = "_LayoutTenantShell";`.
- Future MVC route proposal: `/Pharmacovigilance/MeddraCoding`.
- Future view root proposal: `frontend/Diten.Web/Views/Pharmacovigilance/MeddraCoding/**`.
- Future API remains Gateway-owned and not authorized now.
- Frontend API profile: same-origin MVC proxy profile. Browser JavaScript must call the MVC proxy surface, not call
  Gateway directly and never call a service port directly.
- Dictionary browsing/search/display remains blocked until MedDRA source/license governance approves the allowed
  display, cache, pagination, export, and audit behavior.

Frontend implementation remains blocked until DCP-004, this pack, service boundary, Gateway routing, MOD-0231
source-term contract, CODESET, MedDRA governance, and W-3A0 production blockers are approved.

## Backend File Convention

`service: TBD`

Recommended future boundary: dedicated `Diten.PvgService`.

- Do not host MOD-0232 inside `Diten.Platform`, `Diten.AuthService`, `Diten.DevEnablementService`, or
  `Diten.EnterpriseStrategyService`.
- If a buy/partner coding or dictionary-provider system is selected, `Diten.PvgService` should act as the controlled
  wrapper / orchestration layer for Diten tenant UI, source-term candidate contract, coding assignment, audit,
  evidence, workflow, correlation, MOD-0234 handoff, and adapter/source-governance semantics.
- Internal build scope is limited to the MedDRA Coding assignment contract, tenant UI boundary, workflow/audit /
  evidence integration, diff/export contract, and approved adapter layer after approval.

If a PVG runtime service is later approved, backend implementation must follow the Golden Reference CQRS shape:

```text
Features/MeddraCoding/
├── Commands/
├── Queries/
├── Handlers/CommandHandlers/
├── Handlers/QueryHandlers/
├── Validators/
└── MeddraCodingModels.cs
```

Naming rules for future implementation:

- Commands: `CreateMeddraCodingCommand`, `UpdateMeddraCodingCommand`, `AssignMeddraCodeCommand`,
  `ReviewMeddraCodingCommand`, `ReturnMeddraCodingCommand`, `ExportMeddraCodingDiffCommand`, and archive/void
  commands only if the corresponding operations are approved.
- Queries: `GetMeddraCodingListQuery`, `GetMeddraCodingByIdQuery`, `GetMeddraCodingForCaseProcessingQuery`, and
  dictionary-version reference queries only if list/detail/handoff surfaces are approved and license policy allows.
- Handlers: `*Handler` only; no `CommandHandler`, `QueryHandler`, or `RequestHandler` suffix.
- Validators: `*Validator` only; no `CommandValidator` suffix.
- Forbidden future conventions: `DeleteMeddraCodingCommand`, `BulkDeleteMeddraCodingCommand`, DELETE endpoints,
  and bulk-delete endpoints. Archive/void commands remain blocked until retention/legal-hold policy is approved.
- Forbidden until CODESET and MedDRA governance approve it: dictionary import/search/cache commands, static
  dictionary fixtures, and source-code literals containing MedDRA terms.

This section is a future convention statement, not implementation authorization.

## Frontend File Contract

`golden_reference: slim`

The recorded draft-planning create/edit field count is 7, so MOD-0232 MedDRA Coding follows Golden Reference Slim:

- `Index.cshtml`.
- `_CreateEditOffcanvas.cshtml`.
- `_DetailsQuickView.cshtml`.
- `_Filter.cshtml`.
- `_DataTable.cshtml`.
- `_IndexL10n.cshtml`.
- `MeddraCodingIndex.cs`.
- `wwwroot/assets/js/Pharmacovigilance/MeddraCoding/index.js`.
- `wwwroot/assets/js/Pharmacovigilance/MeddraCoding/index.l10n.js`.
- `Resources/Views/Pharmacovigilance/MeddraCoding/MeddraCodingIndex.{lang}.resx`.

Slim must not include Compact-only separate `Create.cshtml`, `Edit.cshtml`, `Details.cshtml`, or `_Form.cshtml`
unless a later approved revision changes the field count or UI scope.

Additional frontend blockers:

- MedDRA dictionary terms cannot be embedded in static UI, localization resources, JavaScript, HTML, fixtures, or
  demos until source/license governance explicitly permits that use.
- Source terms and coding notes require field-level sensitivity, masking, and row/field access decisions before
  list/detail/search/export UI is approved.
- Dictionary search/browse UI is blocked until CODESET and MedDRA source/version contracts define allowed display,
  cache, pagination, export, and audit behavior.

Future frontend API calls must use the same-origin MVC proxy profile. Direct browser-to-Gateway calls are not the
preferred profile for this regulated tenant surface; direct service-port calls are forbidden.

No frontend files may be created until runtime gates, masking matrix, and dictionary display authority are approved.

## Validation Rules

MedDRA Coding fields are recorded for draft planning. Detailed validation, masking, workflow, evidence, dictionary,
license, audit, and export behavior still must be resolved before `ready-for-dev`:

| Field | Required | Rule | DB-level | Pre-check | Sensitivity / fail-closed requirement |
|---|---|---|---|---|---|
| sourceTermCandidateId | Yes | Existing same-tenant MOD-0231 source term candidate required | TBD | MOD-0231 contract | PHI / regulated-safety; missing source contract blocks create/assign/review |
| meddraDictionaryVersionId | Yes | Approved active or explicitly allowed historical MedDRA dictionary version | TBD | CODESET + MedDRA governance | licensed-dictionary; missing/unlicensed/unvalidated version blocks assignment |
| meddraLltCode | Yes | Selected LLT code must exist in selected dictionary version | TBD | CODESET validation | licensed-dictionary; invalid or version-mismatched code returns 400/409 and no assignment |
| codingMatchType | Yes | Approved controlled option set | TBD | MOD-0023 / coding policy | regulated-safety; unknown match type fails closed |
| codingDecisionReasonCode | Yes | Approved controlled reason-code set | TBD | MOD-0021 / coding policy | regulated-safety; missing reason code blocks assignment/review |
| codingRationale | No | Max length, redaction, masking, and audit allow-list TBD | TBD | MOD-0019 / MOD-0021 | confidential / PHI / regulated-safety; raw rationale prohibited in logs/traces/metrics/audit payloads |
| evidenceLinkIds | Conditionally required | Required when workflow/review state requires evidence | TBD | MOD-0031 | confidential / PHI; invalid or unavailable evidence link blocks approval/export |

Every final field must have tests proving unauthorized, cross-tenant, missing-policy, masking-denied, evidence
unavailable, workflow unavailable, audit unavailable, dictionary unavailable, license unavailable, and invalid-code
behavior.

### MOD-0231 Fields Consumed by MOD-0232

MOD-0232 consumes MOD-0231 Case Processing source-term and lifecycle context. It must not re-own the case master,
case-processing assessment, or lifecycle state.

| MOD-0231 field / output | MOD-0232 use | Status |
|---|---|---|
| Case Processing ID | Same-tenant parent case-processing reference | BLOCKED until MOD-0231 contract approved |
| Source Term Candidate ID | Required input for `sourceTermCandidateId` | BLOCKED until MOD-0231 source-term contract approved |
| TenantId | Server-resolved tenant isolation only; never client-supplied | BLOCKED by tenant/security gate |
| ProductExposureAssessment | Source context for product-related coding | BLOCKED by MOD-0231 masking/audit policy |
| EventAssessmentSummary | Source context for event term coding | BLOCKED by MOD-0019 / MOD-0021 |
| SeriousnessConfirmed | Regulated-safety context for coding review | BLOCKED by MOD-0231 contract |
| PreliminaryExpectedness | Context only; MOD-0232 does not own case expectedness | BLOCKED by MOD-0231 / MOD-0232 boundary approval |
| EvidenceCompletenessStatus | Evidence readiness gate | BLOCKED by MOD-0031 |
| EvidenceLinkReferences / evidenceLinkIds | Evidence links for coding rationale/review/export | BLOCKED by MOD-0031 |
| SignalHandoffReadiness | Downstream sequencing context | BLOCKED by MOD-0234 handoff contract |
| Correlation ID / trace bundle | Audit and trace continuity | BLOCKED by Blueprint MOD-0040 / TRACE-BUNDLE |
| Workflow instance ID | Coding workflow continuity if approved by MOD-0023 | BLOCKED by MOD-0023 |

Missing MOD-0231 source-term/case-processing contract must block MOD-0232 create, assign-code, review, approve,
recode, and export. No untraceable coding work item may be created from incomplete or cross-tenant source data.

### MedDRA Source / License / Version / Import Governance

Recommended governance posture:

- MedDRA dictionary source/provider, license, allowed storage, allowed display/search, export, cache, test fixture,
  and redistribution behavior must be approved before runtime.
- CODESET must own dictionary identity, version model, validation behavior, code-reference shape, version
  immutability, and invalid/unavailable dictionary behavior.
- MOD-0232 may reference approved dictionary version IDs and validated code references; it must not embed MedDRA
  dictionary terms in source code, UI, seed data, fixtures, localization files, or tests unless explicitly
  permitted by the approved license/source policy.
- Every coding assignment must bind to the dictionary version used at assignment time. Recode/version update
  behavior must be append-only and auditable, never a silent overwrite.
- PT/HLT/HLGT/SOC values are server-resolved derived outputs from the approved dictionary service/contract and are
  excluded from create/edit field count.

### Workflow States

Recorded draft workflow states for MOD-0232 planning:

```text
ReadyForCoding
CodingInProgress
Coded
ReviewRequired
Approved
ReturnedForRevision
QueryNeeded
RecodeRequired
Uncodable
ExportReady
Archived
```

MOD-0234 consumption gate: only `Approved` is consumable as final coded output unless a later degraded contract is
explicitly approved. `ExportReady` is an export packaging state, not a substitute for final coded-output approval.

## Failure Path to Verify

Future implementation must verify at least these paths:

- **Missing approved MOD-0231 source contract**
  - Expected: create/assign/review/export is blocked; no untraceable coding work item is created.
- **Missing CODESET or dictionary-version contract**
  - Expected: code assignment and dictionary lookup are blocked; no unversioned MedDRA assignment is stored.
- **Missing MedDRA source/license approval**
  - Expected: dictionary import, display, search, export, and assignment are blocked; no static terms are exposed.
- **Invalid code for dictionary version**
  - Expected: 400/409 validation response; no assignment or overwrite is committed.
- **Dictionary version changed after assignment**
  - Expected: original assignment remains version-bound; recoding creates append-only diff/audit trail.
- **Unauthorized actor**
  - Expected: 401/403 according to policy; no source term, coded term, or metadata leak.
- **Cross-tenant access**
  - Expected: 404 or empty result; no cross-tenant data or metadata returned.
- **Missing MOD-0019 policy for a sensitive field**
  - Expected: field omitted/masked or operation denied according to the field matrix; no permissive fallback.
- **Sensitive or licensed content appears in audit/log/trace**
  - Expected: test fails; raw PHI/PII/free text and licensed dictionary text must not be persisted to audit
    payloads, logs, traces, metrics, or error details.
- **Workflow/Inbox unavailable**
  - Expected: coding assignment, review, approval, return, or export blocks unless an explicit degraded mode is
    approved; no unreviewed state progression.
- **Evidence-link unavailable**
  - Expected: assignment/review/export blocks or follows an explicitly approved degraded path; no fake evidence pack.
- **Audit sink unavailable**
  - Expected: regulated mutation is blocked or queued according to approved MOD-0021 contract; no unaudited mutation.
- **Correlation/trace context missing**
  - Expected: behavior follows Blueprint MOD-0040 / TRACE-BUNDLE decision; runtime must not create untraceable
    regulated state changes.
- **Delete/archive attempted before retention/legal-hold decision**
  - Expected: operation absent or denied; no regulated coding record is removed or hidden without approved policy.

## Authorization Convention

Permission prefix proposal for future tenant/domain implementation:

```text
pvg.meddra-coding.read
pvg.meddra-coding.create
pvg.meddra-coding.update
pvg.meddra-coding.assign-code
pvg.meddra-coding.review
pvg.meddra-coding.recode
pvg.meddra-coding.export-diff
pvg.meddra-coding.archive
```

Explicitly excluded permission keys:

```text
pvg.meddra-coding.delete
pvg.meddra-coding.bulk-delete
```

Initial role / permission matrix proposal:

| Role | read | create | update | assign-code | review | recode | archive | export-diff |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| PVG Medical Coder | Assigned queue | From approved MOD-0231 source only | Yes | Yes | No | No | No | No |
| PVG Coding Reviewer | Yes | No | Yes | No | Yes | Recommend only | No | Masked / approved only |
| PVG Safety Manager | Yes | Yes | Yes | Yes | Yes | Yes | Only after retention/legal-hold approval | Masked / approved only |
| PVG Signal Liaison | Approved coded output only | No | No | No | No | No | No | Approved output only |
| PVG Compliance Auditor | Read-only | No | No | No | No | No | No | Masked / audit export only |
| PVG System Integration | Approved contract only | From MOD-0231 handoff only | No | Contract-limited | No | Contract-limited only | No | Contract-limited only |

Open authorization decisions:

- Final actor role names, actor type mapping, and seed/grant ownership require MOD-0018 / AuthService approval.
- Create is limited to approved MOD-0231 source-term handoff or workflow-assigned coding work if later approved.
- Archive permission is unusable until retention/legal-hold policy is approved.
- Export/diff is limited to masked / approved output unless a later approved field and license policy permits more.
- Dictionary search/display, source-term detail, reviewer override, and recoding behavior must be finalized before
  `ready-for-dev`.
- PHI/PII and licensed-dictionary field-level authorization must align with MOD-0019 and CODESET before runtime.
- Permission seed/grant ownership must remain with MOD-0018 / AuthService; this draft authorizes no seed.

No permission seed is authorized by this draft.

## Gateway / API Routing Decision

Decision: Future API remains Gateway-owned, but no Gateway route is authorized by this draft.

No DELETE or bulk-delete route may be introduced for MOD-0232. Any future Gateway/API route must be additive around
read, create/update, assign-code, review, recode, export/diff, and archive only after the relevant approvals;
archive remains blocked until retention/legal-hold approval.

Future route decision must define:

- service/deployment owner;
- upstream API base path;
- downstream path;
- auth/correlation/error-model behavior;
- dictionary search/display endpoint authority, if any;
- OPTIONS/CORS handling if applicable;
- integration-agent task for `gateway/Diten.ApiGateway/**/ocelot.json`.

Direct service-port calls from frontend remain forbidden.

## Acceptance Criteria

Acceptance criteria for this draft pack:

- [x] Pack exists at `execution/domains/pharmacovigilance/module-packs/MOD-0232-meddra-coding.md`.
- [x] Status is `draft`.
- [x] Canonical name is exactly `MedDRA Coding`.
- [x] DCP-002 preflight passed for MOD-0232.
- [x] DCP-004 remains `draft`; no execution is authorized.
- [x] W-3A0, MOD-0231, CODESET, and MedDRA source/license/versioning dependencies are recorded as production
      blockers, not waived.
- [x] Audit, evidence, masking, workflow, correlation, and tenant isolation dependencies are recorded as blockers.
- [x] No runtime implementation is authorized.
- [x] Form field count recorded for draft planning as `7`.
- [x] Golden Reference recorded for draft planning as `slim`.
- [x] Shell recorded for draft planning as `tenant`.
- [x] Entity base recorded for draft planning as `EntityBase`.
- [x] Future service boundary recorded as dedicated `Diten.PvgService`; frontmatter `service` remains `TBD` until
      explicit scaffold approval.
- [x] MOD-0231 fields consumed by MOD-0232 are recorded.
- [x] MedDRA source/license/version/import governance recommendations are recorded.
- [x] Workflow states and MOD-0234 `Approved` consumption gate are recorded.
- [x] Actor roles and permission matrix are recorded, including `pvg.meddra-coding.recode`.
- [x] Delete and bulk-delete are explicitly excluded.
- [x] Archive remains blocked until retention/legal-hold approval.

Acceptance criteria before any future implementation can start:

- [ ] DCP-004 is `approved` / `ready-for-execution`.
- [ ] This module pack is `approved` / `ready-for-dev`.
- [ ] MOD-0231 Case Processing source-term/case-processing contract is approved and compatible with this pack.
- [ ] CODESET contract is approved for dictionary identity, version binding, validation, and code-reference shape.
- [ ] MedDRA source, license, versioning, import validation, update cadence, storage, display, export, cache, and
      redistribution policy are approved.
- [ ] `service` is resolved through explicit service scaffold approval; frontmatter currently remains `TBD`.
- [ ] W-3A0 REG-PV-BASE, CASE-LIFECYCLE, and CODESET dependencies are closed or explicitly satisfied by
      production-grade external contracts.
- [ ] Required interface contracts are concrete for MOD-0018, MOD-0019, MOD-0021, MOD-0023, MOD-0031, Blueprint
      MOD-0040 / TRACE-BUNDLE, MOD-0231, CODESET, MedDRA source governance, and MOD-0234.
- [ ] Detailed validation rules, masking behavior, row/field access behavior, audit payload rules, evidence-link
      rules, workflow transition rules, Gateway routing, dictionary display/search/export behavior, and tests are
      fully specified from the recorded draft-planning field model.
- [ ] Delete/retention/legal-hold behavior is decided.
- [ ] Build/buy/partner integration boundary for MedDRA dictionary sourcing and coding tooling is finalized.

## Test Expectations

No runtime tests are expected for this draft because no runtime files are authorized.

Future implementation test expectations must include:

- DCP-002 identity proof remains valid.
- Backend build and unit/integration tests for the approved PVG service boundary.
- Tenant isolation and regulated-data masking tests.
- MOD-0231 source-term/case-processing contract tests.
- CODESET dictionary-version binding, version immutability, invalid-code, unavailable-dictionary, and dictionary
  update/recoding tests.
- MedDRA source/license governance tests proving no unlicensed dictionary use, no hardcoded static terms, and no
  unauthorized dictionary export/display/cache.
- Per-field PHI/PII/licensed-dictionary sensitivity, masking, row/field deny, and missing-policy fail-closed tests.
- Audit, coding diff/export, correlation/TRACE-BUNDLE, evidence-link, workflow/inbox failure-path tests.
- Signal handoff contract tests for MOD-0234 consumption.
- Tests proving raw PHI/PII/free text and licensed dictionary text are absent from logs, traces, metrics, audit
  payloads, validation errors, and regulated error responses unless explicitly allow-listed with redaction.
- Frontend build and DataTable verifier only if frontend is approved and Slim/Compact is decided.
- Gateway route smoke only after integration-agent-owned route approval.

## Ready-for-dev Checklist

- [ ] DCP-004 status changed outside this pack to `approved` / `ready-for-execution`.
- [ ] MOD-0232 status changed outside this draft to `approved` / `ready-for-dev`.
- [ ] MOD-0231 Case Processing source-term and lifecycle contract approved.
- [ ] CODESET owner, interface, dictionary-version model, validation behavior, and unavailable-code behavior approved.
- [ ] MedDRA source/license/versioning/import policy approved, including allowed storage, search, display, export,
      cache, test fixture, and redistribution behavior.
- [ ] W-3A0 owner and closure criteria recorded for REG-PV-BASE, CASE-LIFECYCLE, and CODESET.
- [ ] MOD-0018 RBAC actor/permission matrix approved.
- [ ] MOD-0019 per-field PHI/PII/licensed-dictionary sensitivity, masking, row/field access, and fail-closed tests
      approved.
- [ ] MOD-0021 AuditEvent v1 event names, payload allow-list, diff/export audit shape, and failure behavior approved.
- [ ] MOD-0023 workflow/inbox states, assignments, review gates, and unavailable-workflow behavior approved.
- [ ] MOD-0031 evidence-link object reference shape and evidence completeness behavior approved.
- [ ] Blueprint MOD-0040 / TRACE-BUNDLE ID, correlation header, trace stitching, and regulated error model approved.
- [ ] MOD-0234 downstream coded-term handoff and diff/export consumer contract approved.
- [ ] `service` resolved through explicit service scaffold approval.
- [x] Future service/deployment boundary recorded as dedicated `Diten.PvgService`; scaffold approval still required
      before frontmatter `service` changes.
- [x] `shell` recorded for draft planning as `tenant`.
- [x] Route surface recorded as `/Pharmacovigilance/MeddraCoding` with
      `frontend/Diten.Web/Views/Pharmacovigilance/MeddraCoding/**`.
- [x] `entity_base` recorded for draft planning as `EntityBase`.
- [x] `form_field_count` recorded for draft planning as `7`.
- [x] `golden_reference` recorded for draft planning as `slim`.
- [x] Create/edit fields and required/optional classification recorded for draft planning.
- [ ] Detailed validation rules and field-level tests approved.
- [ ] Delete/retention/legal-hold policy approved.
- [ ] Build/buy/partner boundary for dictionary source and coding tools finalized.

## Implementation Notes

- Use canonical name exactly: `MedDRA Coding`.
- Treat DCP-004 W-3C as delivery planning context only; it does not authorize runtime work.
- Frontmatter decisions recorded for draft planning 2026-08-04: `shell: tenant`, `entity_base: EntityBase`,
  `form_field_count: 7`, and `golden_reference: slim`. `service` remains TBD.
- Service boundary reconciled 2026-08-04: future boundary is dedicated `Diten.PvgService`. Frontmatter `service`
  remains TBD until explicit service scaffold approval.
- Route/UI profile reconciled 2026-08-04: future tenant MVC route is `/Pharmacovigilance/MeddraCoding`, view root
  is `frontend/Diten.Web/Views/Pharmacovigilance/MeddraCoding/**`, layout is `_LayoutTenantShell`, and frontend
  profile is same-origin MVC proxy. Future API remains Gateway-owned and not authorized now.
- MedDRA Coding field model recorded for draft planning 2026-08-04 with 7 user-entered create/edit fields and Slim Golden
  Reference. PT/HLT/HLGT/SOC, coder identity, timestamps, workflow state, tenant, audit, trace, and assignment IDs
  are server-resolved and excluded from field count.
- MOD-0231 consumed fields, MedDRA source/license/version/import governance recommendations, workflow states,
  MOD-0234 `Approved` consumption gate, and actor/permission matrix were recorded 2026-08-04 as planning decisions.
  They do not resolve MOD-0231, CODESET, MedDRA governance, W-3A0, MOD-0023, MOD-0031, MOD-0021, MOD-0019, or
  TRACE-BUNDLE blockers.
- Delete and bulk-delete policy reconciled 2026-08-04: delete and bulk-delete are excluded. Archive/void remains
  blocked until retention/legal-hold approval.
- Treat MOD-0231 as the required upstream source of case-processing source terms. MOD-0232 must not duplicate the
  case master or invent source-term shape before MOD-0231 approves it.
- Treat CODESET and MedDRA source/license/versioning as hard blockers. No local static data, seed, fixture, or UI
  term list is authorized by this draft.
- Use Blueprint MOD-0040 / TRACE-BUNDLE for canonical ID, correlation header, trace stitching, and regulated error
  behavior if runtime is later approved.
- Use MOD-0004 and MOD-0063 only as downstream gates unless MOD-0232 explicitly emits analytics, semantic metric
  IDs, or data-product outputs in a later approved revision.
- Keep dictionary assignments append-only and version-bound. Recoding must be represented as an auditable diff, not
  as silent overwrite.
- No service, frontend, gateway, runtime, appsettings, seed, or test file is in scope for this draft.

## Follow-up Items

- Close W-3A0 owner and closure criteria for REG-PV-BASE, CASE-LIFECYCLE, and CODESET.
- Approve concrete MOD-0231 source-term and lifecycle handoff contract and fail-closed behavior against consumed
  fields recorded here.
- Approve concrete MOD-0234 consumption contract for `Approved` coded output, or explicitly approve any degraded
  consumption contract later.
- Obtain explicit approval before changing frontmatter `service` from TBD or creating `Diten.PvgService`.
- Define same-origin MVC proxy endpoints after frontend implementation is approved.
- Finalize MedDRA source/provider, license, allowed usage, import validation, versioning, release cadence, and
  redistribution policy.
- Resolve detailed validation, masking, audit payload, evidence-link, workflow transition, dictionary display/search,
  export, and fail-closed tests for the recorded draft-planning 7-field model.
- Finalize actor roles and permission matrix with MOD-0018 / AuthService seed/grant ownership.
- Resolve retention/legal-hold policy before any archive/void operation is introduced.
- Finalize build/buy/partner boundary for MedDRA dictionary sourcing, coding tools, search, and optional assisted
  coding behavior.
