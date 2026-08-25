---
id: CAND-CAP-0007-FU01
name: Process Architecture & Modeling
domain: business-process-management
service: TBD
shell: none
golden_reference: none
entity_base: TenantScopedEntity
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
- lifecycle `Draft | Review | Published | Retired`;
- `isExecutable=false` invariant;
- notation/artifact type plus external artifact reference metadata;
- typed owner/interface/approval-policy references;
- effective dates, publication metadata, concurrency version and standard soft-delete/audit metadata.

Published model content is immutable. Revise/clone creates a new monotonic Draft version; retire is terminal and
preserves history. Published/Retired never reopen.

Typed contracts:

- `ProcessOwnerReference`: `OwnerRegistryModuleId=MOD-0001`, `OwnerRecordId`, `OwnerType`, optional `RoleCode`,
  `ValidFrom`, optional `ValidTo`. BPM owns the process-role binding only; MOD-0001 owns assignment/person/org
  truth. Labels are non-authoritative read projections.
- `ClassificationReference`: `ProviderModuleId=MOD-0048`, `CodeSetId`, `ValueId`, `ValueVersion`. FU01 owns
  ProcessArchitecture/ProcessArea hierarchy, never shared classification catalogs or local fallback enums.
- `ProcessModelArtifact`: opaque provider/object/version IDs, media/notation type, byte length, SHA-256,
  canonicalization profile ID/version and `isExecutable=false`. Generic XML/binary bytes remain external.

If BPMN/XML is accepted, the initial canonicalization proposal is XML C14N 1.1 without comments over UTF-8
canonical bytes (`bpm-xml-c14n11-no-comments-v1`). DTD, external entities, XInclude, network resolution,
scripts/active content and executable BPMN extensions are rejected. The storage/security owner must approve the
profile, MIME/size/depth limits and provider before ready-for-dev.

## 5. Repo Scope

Governance-only scope for this draft:

- this file;
- BPM domain README/config;
- DCP-005;
- candidate reservation records;
- narrow AGENTS.md BPM registration.

No implementation path is authorized because service/topology ownership, base soft-delete composition and
production authority remain unresolved even though the projected base type is selected.

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
- MOD-0023: mandatory model publish/retire approval gate; it owns all approval decisions/history.
- MOD-0024: future task/checklist reference, not a FU01 runtime dependency.
- MOD-0025: future rule-set/evaluation reference; governance readiness must be reconciled.
- MOD-0059: KPI reference only.
- MOD-0117: future improvement handoff; outside FU01.

Dependencies are ownership boundaries, not claims that integrations are currently available.

## 8. Runtime Constraints

- `production_authority: none`; `implementation_authority: none`.
- `service: TBD`; primary proposal is a new bounded `Diten.BusinessProcessManagementService`, while the mutually
  exclusive fallback is an isolated module in a future approved Management Governance owning service. No port,
  API, event or job is reserved and neither topology authorizes a scaffold.
- The concrete projected base is `Diten.Platform.Common.Persistence.TenantScopedEntity`. Its missing required
  `DeletedAt` contract must be resolved by approved composition/amendment before ready-for-dev.
- `CAND-CAP-0007*` never appears in runtime literals, namespaces, permission keys, collection names, or event
  `ModuleCode` values.
- `ProcessInstance` and executable BPMN/token execution are prohibited.
- Planned tenant isolation is server-derived, fail-closed and metadata-safe.

Persistence projection (not implementation authority):

- Mongo collections: `bpm_process_architectures`, `bpm_process_areas`, `bpm_process_definitions`,
  `bpm_process_models`, `bpm_process_model_versions`, `bpm_process_relationships`,
  `bpm_process_interface_refs`, `bpm_process_owner_refs`, `bpm_process_model_artifacts`,
  `bpm_process_publications`, `bpm_command_receipts`, `bpm_outbox_messages`.
- IDs are GUID subtype 4. Every lookup/index/CAS/idempotency/outbox key is tenant-first.
- Every mutable command filters `TenantId + Id + Version + IsDeleted:false`, increments Version atomically and
  returns controlled `409` on stale concurrency after non-disclosing tenant resolution.
- Required unique active indexes: definition `(TenantId, NormalizedCode)`; architecture code; area
  `(TenantId, ArchitectureId, ParentAreaId, NormalizedCode)`; model `(TenantId, ProcessDefinitionId,
  NormalizedCode)`; version `(TenantId, ProcessModelId, VersionNumber)`; one open Draft/Review fence per model;
  relationship `(TenantId, SourceDefinitionId, TargetDefinitionId, RelationshipType)`; publication
  `(TenantId, ProcessModelVersionId)`; command receipt `(TenantId, Operation, IdempotencyKey)`.
- Query indexes are tenant-first for area/status, owner ref, lifecycle/effective dates and due outbox messages.
- Submit/publish/retire/create-revision require idempotency key + immutable payload hash. Same key/hash returns
  the original result; same key/different hash returns `409`.
- Publication changes version state, publication pointer, receipt and audit/outbox intent in one same-database
  Mongo replica-set transaction. Without proven transaction support, ready-for-dev fails closed; no partial
  publication or non-atomic audit claim is allowed.
- MOD-0021 owns general audit persistence. BPM outbox contains metadata/correlation only, never XML/binary,
  signed URLs, secrets or sensitive owner attributes. No integration event is published until an EA-assigned
  canonical runtime `ModuleCode` exists.

## 9. Layout & Shell Contract

`shell: none` remains truthful because no frontend authority exists. The decided future projection is tenant
shell + Golden Compact: Architecture register; Process Definitions Index/Create/Edit/Details; dedicated
non-executable Model Editor; Version History; Review and Publish/Retire actions. ProcessDefinition has 12
user-entered fields: Code, Name, Description, Architecture, Area, Parent Process, ClassificationRef, OwnerRef,
StewardRef, Criticality, EffectiveFrom, EffectiveTo. Existing `mod-process-*` identifiers remain prototype/
navigation identities and are not aliases to this pack.

## 10. Backend File Convention

No backend file convention is selected because service topology is open. A future promotion must name the
service, entity base and exact repo paths, then adopt the applicable repository architecture/golden-reference
rules. This draft cannot be used to scaffold a service.

## 11. Frontend File Contract

None authorized. Frontmatter remains `golden_reference: none` while production/frontend authority is `none`.
The future target is already decided as tenant shell + Golden Compact with the §9 12-field count, but activation
still requires an explicit pack amendment, localization plan, exact routes/files and browser acceptance criteria.

## 12. Validation Rules

Future implementation must prove:

- tenant-unique normalized process code;
- valid hierarchy without cycles;
- typed references resolve through their authoritative owner;
- model version numbers are monotonic;
- published content cannot be mutated;
- only Draft can be edited; Review content is frozen; only Published can be retired;
- every model artifact has `isExecutable=false`;
- client-supplied `TenantId` is rejected/ignored as authoritative input;
- cross-tenant access returns `404`/empty with no metadata leakage.
- one open Draft/Review fence per ProcessModel and one non-overlapping published effective timeline;
- mandatory approval evidence matches TenantId, model-version ID, checksum/version and correlation;
- approval outcome alone never publishes and never substitutes for `bpm.process-models.publish` permission;
- artifact reference is opaque/version-pinned, same-tenant, allowlisted, canonicalized and checksum-verified;
- no FU02 metric/performance or FU03 improvement/PPM field exists on FU01 aggregates.

## 13. Failure Path to Verify

- duplicate process code;
- hierarchy cycle;
- missing owner/interface/artifact reference fails closed; cross-tenant references return `404`/empty with no
  metadata leakage;
- attempted mutation of a Published version;
- attempted executable flag or ProcessInstance creation;
- stale concurrency version;
- unauthorized publish/retire;
- unavailable MOD-0023 contract blocks submit/publish/retire and fails closed rather than creating a local
  approval substitute; MOD-0025 absence blocks rule integration only, not core FU01 modeling.
- concurrent revision/open-draft creation permits one success and one `409`;
- idempotency replay mismatch returns `409`; transaction rollback leaves no pointer/receipt/outbox fragment;
- cross-tenant/absent/deleted references are indistinguishable and leak no checksum, owner, count, version,
  redirect, signed URL or timing-friendly validation detail;
- XML XXE/entity-expansion/external URI/script/oversize/wrong MIME/checksum mismatch is rejected;
- an attempted rule binding fails the contract gate while MOD-0025 registry/pack/contract is absent.

## 14. Authorization Convention

Exact draft proposal; `bpm.*` itself is never an enforceable/grantable wildcard:

- `bpm.architectures.read`
- `bpm.architectures.create`
- `bpm.architectures.update`
- `bpm.architectures.archive`
- `bpm.process-definitions.read`
- `bpm.process-definitions.create`
- `bpm.process-definitions.update`
- `bpm.process-definitions.archive`
- `bpm.process-models.read`
- `bpm.process-models.create`
- `bpm.process-models.update`
- `bpm.process-models.request-review`
- `bpm.process-models.return-to-draft`
- `bpm.process-models.publish`
- `bpm.process-models.retire`
- `bpm.process-models.create-revision`

Read is independent; a write permission does not imply it. Publish/retire permission is always required in
addition to a matching MOD-0023 approval. Exact keys, seed/grant and role matrix still require domain/security
and MOD-0018/Auth owner approval. Forbidden ownership claims include
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
9. Lifecycle is exactly Draft -> Review -> Published -> Retired with frozen Review, terminal Retired and no
   Published reopen; revision creates a new monotonic Draft.
10. Publish/retire require both exact BPM permission and matching mandatory MOD-0023 approval evidence.
11. One open Draft/Review and a single non-overlapping effective timeline are concurrency-fenced.
12. FU01 collection/index/CAS/idempotency/transaction/audit-outbox projection is testable and contains no
    candidate runtime ModuleCode.
13. Future UI projection is tenant-shell Golden Compact with exact 12-field ProcessDefinition form, but no
    frontend authority is granted.

## 17. Test Expectations

No test code or runtime test is authorized now. Before any future implementation, the approved pack must require
unit/contract/integration tests for lifecycle, immutability, tenant isolation, typed-reference validation,
authorization, concurrency and every §13 failure path. Required persistence proofs include unique-index races,
CAS, idempotency fingerprint replay, replica-set transaction rollback and outbox payload redaction. Security
tests cover every exact permission, wildcard rejection, MOD-0023 evidence mismatch/replay, cross-tenant
non-disclosure and hostile XML. Browser tests remain N/A until UI is separately approved; a future authorized UI
must prove Compact routes/forms plus the specialized model editor separately.

## 18. Ready-for-dev Checklist

- [ ] DCP-005 explicitly approved/ready-for-execution.
- [ ] Named Business Process Management / Process Excellence business owner assigned.
- [ ] Named BPM Domain Technical Owner assigned.
- [ ] Primary versus fallback topology selected exclusively; service/repo scope and production authority
  explicitly approved.
- [x] Entity-base projection selected: `TenantScopedEntity`.
- [ ] Base owner resolves mandatory `DeletedAt`/`DeletedBy` composition.
- [ ] Artifact storage/reference owner decided.
- [ ] MOD-0001 owner contract and MOD-0048 classification sets/provider readiness approved.
- [x] MOD-0023 publish/retire approval baseline decided: mandatory; approval ownership remains MOD-0023.
- [ ] MOD-0023 typed request/outcome/SoD contract approved by its owner.
- [x] MOD-0025 blocker isolated to future rule integration, not core FU01.
- [ ] Permission prefix approved by domain/security owner.
- [ ] Tenant and typed-reference contract reviewed.
- [ ] Same-database Mongo replica-set transaction support proven.
- [ ] Canonical runtime ModuleCode assigned before event publication.
- [ ] Exact implementation acceptance/tests and rollback boundary approved.

## 19. Implementation Notes

Lifecycle is `Draft -> Review -> Published -> Retired`. Draft is editable. Submit freezes content/checksum and
enters Review. Withdraw or rejected review returns the same never-published version to Draft with a new review
cycle/concurrency version. An approved MOD-0023 outcome is evidence only; an idempotent BPM Publish command with
independent publish permission performs Review -> Published. Published is immutable. CreateRevision clones a
Published version to N+1 Draft with `PredecessorVersionId`; publishing the successor records lineage outside the
predecessor content. Retired is terminal. Parallel variants and `VariantKey` are absent; future-dated successors
must not overlap. BPMN/XML, if later accepted, remains non-executable and provider-version/checksum pinned.

Ready-for-dev is evidence-gated, not inferred from prose. Scoring rubric: ownership/topology 20%, persistence
20%, security 20%, lifecycle/contracts 20%, external integrations 10%, UI/test contract 10%. This decision pass
closes the technical projection but leaves named owners, exclusive topology authority, base soft-delete closure,
provider contracts, permissions, transaction proof and runtime canonicalization open. Estimated readiness is
`82%`; status remains draft and cannot become ready-for-dev until every mandatory gate is 100% regardless of the
aggregate score.

## 20. Follow-up Items

- FU02 Process Performance Management — future-member plan only; no pack file/reservation here.
- FU03 Process Improvement Management — future-member plan only; typed MOD-0117 handoff required.
- Enterprise Architect canonical MOD allocation.
- Optional executable-runtime discovery under a new Control Tower decision.
- Optional frontend scope only through an expressly approved amendment/new member pack.
- MOD-0025 registry/module-pack/evaluation contract reconciliation before any rule binding; core FU01 does not
  wait for it.
- FU02/FU03 must consume published FU01 IDs/version/checksum via typed contracts and may not add anticipatory
  nullable metric, observation, threshold, improvement, benefit, PPM handoff or runtime-instance fields to FU01.
