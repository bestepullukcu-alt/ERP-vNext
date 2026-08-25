---
id: CAND-CAP-0007-FU01
name: Process Architecture & Modeling
domain: business-process-management
service: TBD
shell: none
golden_reference: none
entity_base: TBD
status: draft
owner: TBD
branch: feature/bpm/dcp-005-business-process-management
started: 2026-08-25
target: TBD
form_field_count: n/a
parent: CAND-CAP-0007
delivery_capability_pack: DCP-005
production_authority: none
implementation_authority: none
is_executable: false
executable: false
canonicalization: pending-enterprise-architect
---

# CAND-CAP-0007-FU01 — Process Architecture & Modeling

> **Draft governance contract only.** This pack does not authorize service/runtime/test/frontend/Gateway/
> WorkCenter scaffolding or code. `CAND-CAP-0007-FU01` is a candidate governance identity, not a runtime code.
> Every model artifact in this slice is non-executable (`isExecutable=false`).

## 1. Module Summary

FU01 governs tenant-scoped process landscapes, stable process definitions, model lineages and immutable
published model versions. It is the first member of [DCP-005](../../../portfolio/delivery-capability-packs/DCP-005-business-process-management.md).

## 2. Ownership and Boundaries

FU01 owns process architecture/model governance only. It does not own executable runtime, approvals, tasks,
rules, metrics/KPIs, enterprise owner masters, audit masters, or PPM delivery records. MOD-0023 remains the
approval/SLA/escalation owner; MOD-0024 remains task/checklist owner; MOD-0025 remains rule/version owner.

## 3. Owned Objects

Planned objects, subject to topology approval:

- `ProcessArchitecture`
- `ProcessArea`
- `ProcessDefinition`
- `ProcessModel`
- `ProcessModelVersion`
- `ProcessRelationship`
- `ProcessInterfaceReference`
- `ProcessOwnerReference` — non-authoritative typed reference
- `ProcessModelArtifact` — notation/file metadata, not binary SoR
- `ProcessModelPublication`

Explicitly not owned: `ProcessInstance`, activity/token/job instances, `WorkflowTemplate`/Version,
`WorkflowInstance`, `ApprovalTask`, task/checklist masters or runs, `BusinessRule`/`RuleVersion`, KPI/metric
definitions, projects/work records/benefits, or general `AuditEvent` persistence.

## 4. Entity Fields

This draft does not freeze a runtime schema. Minimum contract candidates:

- stable tenant-unique process code and display name;
- architecture/area hierarchy reference;
- definition and model lineage IDs;
- monotonic model version number and predecessor version reference;
- lifecycle `Draft | InReview | Published | Retired`;
- `isExecutable=false` invariant;
- notation/artifact type plus external artifact reference metadata;
- typed owner/interface/approval-policy references;
- effective dates, publication metadata, concurrency version and standard soft-delete/audit metadata.

Published model content is immutable. Revise/clone creates a new Draft version; retire preserves history.

## 5. Repo Scope

Governance-only scope for this draft:

- this file;
- BPM domain README/config;
- DCP-005;
- candidate reservation records;
- narrow AGENTS.md BPM registration.

No implementation path is authorized because `service`, `entity_base`, topology and production authority are
unresolved.

## 6. Protected Paths

- `.antigravity/**`
- all `services/**`
- all `frontend/**`
- all `gateway/**`
- WorkCenter files/contracts
- archive/frozen paths
- other domains' governance and module packs

## 7. Dependencies

- MOD-0001: owner/SoR assignment reference.
- MOD-0004 and MOD-0060: metric/semantic references only; no FU01 metric ownership.
- MOD-0021: future audit emission/contract owner.
- MOD-0023: optional model publish/retire approval gate.
- MOD-0024: future task/checklist reference, not a FU01 runtime dependency.
- MOD-0025: future rule-set/evaluation reference; governance readiness must be reconciled.
- MOD-0059: KPI reference only.
- MOD-0117: future improvement handoff; outside FU01.

Dependencies are ownership boundaries, not claims that integrations are currently available.

## 8. Runtime Constraints

- `production_authority: none`; `implementation_authority: none`.
- `service: TBD`; no port, database collection, API, event or job is reserved.
- `CAND-CAP-0007*` never appears in runtime literals, namespaces, permission keys, collection names, or event
  `ModuleCode` values.
- `ProcessInstance` and executable BPMN/token execution are prohibited.
- Planned tenant isolation is server-derived, fail-closed and metadata-safe.

## 9. Layout & Shell Contract

`shell: none`. No UI, layout, route, navigation or placeholder change is authorized. Existing frontend
`mod-process-*` identifiers remain prototype/navigation identities and are not aliases to this pack.

## 10. Backend File Convention

No backend file convention is selected because service topology is open. A future promotion must name the
service, entity base and exact repo paths, then adopt the applicable repository architecture/golden-reference
rules. This draft cannot be used to scaffold a service.

## 11. Frontend File Contract

None. `golden_reference: none` and no frontend authority. A future UI requires an explicit pack amendment,
shell decision, form-field count, localization plan and browser acceptance criteria.

## 12. Validation Rules

Future implementation must prove:

- tenant-unique normalized process code;
- valid hierarchy without cycles;
- typed references resolve through their authoritative owner;
- model version numbers are monotonic;
- published content cannot be mutated;
- only Draft can be edited and only Published can be retired;
- every model artifact has `isExecutable=false`;
- client-supplied `TenantId` is rejected/ignored as authoritative input;
- cross-tenant access returns `404`/empty with no metadata leakage.

## 13. Failure Path to Verify

- duplicate process code;
- hierarchy cycle;
- missing owner/interface/artifact reference fails closed; cross-tenant references return `404`/empty with no
  metadata leakage;
- attempted mutation of a Published version;
- attempted executable flag or ProcessInstance creation;
- stale concurrency version;
- unauthorized publish/retire;
- unavailable MOD-0023/0025 contract must fail closed rather than create a local substitute.

## 14. Authorization Convention

Draft proposal only:

- `bpm.architectures.*`
- `bpm.process-definitions.*`
- `bpm.process-models.*`

Candidate verbs include `read/create/update/archive/publish/retire`. The prefix and verbs are not approved and
block `ready-for-dev` until the domain owner decides them. Forbidden ownership claims include
`bpm.workflows.*`, `bpm.tasks.*`, `bpm.rules.*`, `bpm.kpis.*`, `bpm.metrics.*`, and `bpm.projects.*`.

## 15. Gateway / API Routing Decision

No API route or Gateway change is authorized or designed. A later implementation decision must define a
service and same-origin Gateway contract; only the authorized integration owner may change routing.

## 16. Acceptance Criteria

1. Pack remains `draft`, `service: TBD`, `production_authority: none`, `implementation_authority: none`.
2. FU01 scope contains architecture/model objects only.
3. Every planned model artifact is non-executable and no ProcessInstance is owned.
4. Published versions are immutable and revision creates a new version.
5. External owner, workflow, task, rule, KPI/metric, audit and PPM objects remain typed references.
6. Candidate identities are absent from runtime/code semantics.
7. No frontend/runtime/Gateway/WorkCenter/`.antigravity` diff is introduced by governance authoring.
8. Owner/topology/security/contract decisions remain explicit blockers.

## 17. Test Expectations

No test code or runtime test is authorized now. Before any future implementation, the approved pack must require
unit/contract/integration tests for lifecycle, immutability, tenant isolation, typed-reference validation,
authorization, concurrency and every §13 failure path. Browser tests remain N/A until UI is separately approved.

## 18. Ready-for-dev Checklist

- [ ] DCP-005 explicitly approved/ready-for-execution.
- [ ] Business and technical owners assigned.
- [ ] Service topology, entity base, repo scope and production authority explicitly approved.
- [ ] Artifact storage/reference owner decided.
- [ ] MOD-0001 owner/directory and classification providers decided.
- [ ] MOD-0023 publish-policy contract decided.
- [ ] MOD-0025 governance readiness reconciled.
- [ ] Permission prefix approved by domain/security owner.
- [ ] Tenant and typed-reference contract reviewed.
- [ ] Exact implementation acceptance/tests and rollback boundary approved.

## 19. Implementation Notes

Recommended lifecycle is `Draft -> InReview -> Published -> Retired`; Published is immutable and revision creates
a new Draft version with a predecessor reference. Only one current published version per definition/effective
context is the proposal; effective-dated parallel variants remain an open owner decision. BPMN may be stored as
non-executable design metadata/artifact reference only. MOD-0023 may gate lifecycle decisions but never loses
approval ownership.

## 20. Follow-up Items

- FU02 Process Performance Management — future-member plan only; no pack file/reservation here.
- FU03 Process Improvement Management — future-member plan only; typed MOD-0117 handoff required.
- Enterprise Architect canonical MOD allocation.
- Optional executable-runtime discovery under a new Control Tower decision.
- Optional frontend scope only through an expressly approved amendment/new member pack.
