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
- Draft model versions may change; a published version is immutable and superseded only by a new version.
- `ProcessInstance` is not a FU01 owned object.
- Executable Process Runtime, BPMN token execution, job orchestration and activity execution are deferred.
- MOD-0023 `WorkflowTemplate`, `WorkflowInstance`, and `ApprovalTask` remain external objects.

## Tenant and reference boundary

- Planned BPM records are tenant-owned; server-side tenant context is mandatory.
- Client payloads must never be authoritative for `TenantId`.
- Cross-tenant reads and mutations fail closed without metadata leakage.
- Owner, metric, KPI, rule, task, workflow, audit and PPM links are typed references, not copied masters.

## Permission proposal — not approved

The draft proposes `bpm.*` families for future review. No permission key is authorized or final until the domain
owner approves the prefix and the FU01 pack is separately promoted. `bpm.workflows.*`, `bpm.tasks.*`,
`bpm.rules.*`, `bpm.kpis.*`, `bpm.metrics.*`, and `bpm.projects.*` are forbidden ownership claims.

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

1. Business owner and technical owner.
2. Production topology and service ownership; currently `TBD`.
3. Final permission prefix.
4. Process-owner reference contract with MOD-0001 and organization-directory owner.
5. Model-artifact metadata versus binary storage boundary.
6. Classification/reference-data provider.
7. Publish approval policy and MOD-0023 binding.
8. Future performance measurement source and PPM handoff contract.
