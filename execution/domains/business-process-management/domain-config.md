# Business Process Management — Domain Config

> Governance-only boundary. This file grants no production, runtime, frontend, Gateway, WorkCenter, service
> scaffold, or executable BPMN authority.

## Purpose

BPM owns business-process architecture and versioned non-executable process models. Later, separately governed
members may own process-performance bindings/observations and process-improvement cases. Shared platform and
business systems remain authoritative for workflow, tasks, rules, semantics, KPI masters, audit, and delivery.

## In-scope governance identities

- Parent candidate: `CAND-CAP-0007 — Business Process Management` (`candidate / pending-EA`).
- First planned member: `CAND-CAP-0007-FU01 — Process Architecture & Modeling` (`draft`).
- Future members in DCP-005 only: FU02 Process Performance Management; FU03 Process Improvement Management.

## Ownership boundaries

| Concern | Owner |
|---|---|
| Process architecture, definition, model and immutable published model version | BPM FU01 |
| Approval workflow, SLA, escalation, transition gate | MOD-0023 |
| Generic task/checklist templates and task engine | MOD-0024 |
| Business rules, rule versions and rule-test evidence | MOD-0025 |
| Enterprise owner/SoR assignments | MOD-0001 |
| Metric/semantic definitions and certification | MOD-0004 / MOD-0060 |
| KPI definitions and ownership | MOD-0059 |
| General audit trail | MOD-0021 |
| PPM demand/work/project/benefit delivery records | MOD-0117 |

BPM keeps typed references to external owners. It does not fork their master objects.

## Lifecycle and execution boundary

- FU01 models are documentation/design artifacts and must carry `isExecutable=false`.
- Exact states are `Draft`, `Review`, `Published`, `Retired`.
- Draft is editable. Submit freezes the review content/checksum. Review is not editable; withdraw/reject returns
  the same pre-publication version to Draft and advances its concurrency/review cycle.
- Published is immutable. Revision clones Published to a new monotonic Draft with predecessor/supersession
  lineage; it never mutates the predecessor. Retired is terminal and never reopens.
- Publish and retire require mandatory MOD-0023 approval bound to tenant, exact model version, checksum and
  correlation. Approval does not publish and never grants BPM publish permission.
- At most one open `Draft` or `Review` exists per ProcessModel. Expected-version CAS plus a unique working fence
  prevents concurrent draft creation.
- Parallel variants are excluded. One definition has a single non-overlapping effective timeline and at most
  one effective Published version at an instant; `asOf` resolution is deterministic and implies no scheduler.
- `ProcessInstance` is not a FU01 owned object.
- Executable Process Runtime, BPMN token execution, job orchestration and activity execution are deferred.
- MOD-0023 `WorkflowTemplate`, `WorkflowInstance`, and `ApprovalTask` remain external objects.

## Tenant and reference boundary

- Planned BPM records are tenant-owned; server-side tenant context is mandatory.
- Client payloads must never be authoritative for `TenantId`.
- Cross-tenant reads and mutations fail closed without metadata leakage.
- Owner, metric, KPI, rule, task, workflow, audit and PPM links are typed references, not copied masters.

## Permission proposal — exact but not approved

`bpm.*` is descriptive family notation only and is never grantable. The exact proposed keys are:

- `bpm.architectures.read|create|update|archive`
- `bpm.process-definitions.read|create|update|archive`
- `bpm.process-models.read|create|update|request-review|return-to-draft|publish|retire|create-revision`

These pipes abbreviate separate exact literals; the FU01 pack enumerates every one. Domain/security owner and
MOD-0018/Auth seed/grant approval remain mandatory. No permission is implemented by this governance decision.
`bpm.workflows.*`, `bpm.tasks.*`, `bpm.rules.*`, `bpm.kpis.*`, `bpm.metrics.*`, and `bpm.projects.*` are
forbidden ownership claims.

## Topology and persistence projection

- Primary: new bounded `Diten.BusinessProcessManagementService` (proposal; scaffold unauthorized).
- Fallback: isolated BPM module in a future explicitly approved Management Governance owning service.
- Exclusivity: exactly one write owner before ready-for-dev; no Platform/legacy Enterprise Strategy owner, dual
  write, mirrored store or runtime topology fallback.
- Entity base projection: `Diten.Platform.Common.Persistence.TenantScopedEntity`; required `DeletedAt` contract
  mismatch must be closed by approved base composition/amendment.
- Mongo projection uses tenant-first `bpm_process_*`, `bpm_command_receipts` and `bpm_outbox_messages`
  collections without candidate IDs. Multi-document publish/retire/revise requires a same-database replica-set
  transaction; otherwise readiness fails closed.
- Every mutation CAS-filters `TenantId + Id + Version + IsDeleted:false`. Submit/publish/retire/revise use
  `(TenantId, Operation, IdempotencyKey)` plus payload hash; same replay returns prior result, mismatched replay
  returns `409`.
- General audit storage remains MOD-0021. BPM owns business history and an atomic audit/integration intent only;
  no event is published before a canonical runtime ModuleCode exists.

## UI projection — no frontend authority

Future target is tenant shell + Golden Compact: Process Architecture register, Process Definitions Index/Create/
Edit/Details, dedicated non-executable Model Editor, Version History, Review and Publish/Retire actions. The exact
ProcessDefinition create/edit count is 12: Code, Name, Description, Architecture, Area, Parent Process,
ClassificationRef, OwnerRef, StewardRef, Criticality, EffectiveFrom, EffectiveTo. Model canvas/artifact controls
are a separate specialized surface and not part of that form count. Existing `mod-process-*` placeholders remain
untouched and non-canonical.

## Governance repo scope

- `execution/domains/business-process-management/**`
- `execution/portfolio/delivery-capability-packs/DCP-005-business-process-management.md`
- candidate rows in `execution/registries/module-id-registry.md`
- candidate reservation in `execution/portfolio/blueprint-master-plan-reconciliation.md`
- the narrowly authorized BPM entries in `AGENTS.md`

## Protected paths

- `.antigravity/**`
- `services/**`
- `frontend/**`
- `gateway/**`
- WorkCenter files and contracts
- archive/frozen paths
- every other domain's module packs and domain configs

## Open decisions

Technical recommendations are closed above. Human/provider authority is still required to appoint named
business/technical owners; select the exclusive deployment topology; approve permissions/role seeds; approve
MOD-0001 owner and MOD-0048 classification contracts; approve MOD-0028/MOD-0262 artifact storage/security;
resolve `DeletedAt` base composition; prove Mongo transaction topology; decide approval SoD; and allocate a
canonical runtime ModuleCode before events. FU02 metric ownership and FU03 PPM handoff remain later decisions.
