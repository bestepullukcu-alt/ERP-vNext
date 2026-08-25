---
id: DCP-005
slug: business-process-management
name: Business Process Management
candidate_id: CAND-CAP-0007
type: Delivery Capability Pack
standard: CAP-001
status: draft
owner_domain: business-process-management
owner: enterprise-architect / process-excellence (TBD)
branch: feature/bpm/dcp-005-business-process-management
created: 2026-08-25
canonical_source: "Blueprint_Data: no exact BPM MOD row"
production_authority: none
---

# DCP-005 — Business Process Management

> **Governance-only / non-executable.** This draft is a Delivery Capability Pack, not a module pack, runtime
> capability, service, collection, route, permission grant, or production authority. `CAND-CAP-0007` is a
> temporary governance identity pending Enterprise Architect canonicalization and never enters runtime
> literals, namespaces, permission keys, collection names, or event `ModuleCode` values.

## 1. Identity and status

| Field | Value |
|---|---|
| DCP | `DCP-005` |
| Family candidate | `CAND-CAP-0007 — Business Process Management` |
| Status | `draft` |
| Production authority | `none` |
| First member | `CAND-CAP-0007-FU01 — Process Architecture & Modeling` (`draft`) |
| Candidate gate | PASS after registry + reconciliation reservation, 2026-08-25 |
| Blueprint status | No exact canonical BPM MOD row; no MOD invented |

## 2. Business outcome

Establish governed process landscapes and immutable published process-model versions, then add separately
governed performance and improvement slices without duplicating workflow, task, rule, metric/KPI, audit, or
PPM systems of record.

## 3. Problem statement

The Management Governance frontend lists four `mod-process-*` prototype/navigation identities, but Blueprint,
registry, backend and governance contain no canonical BPM family. Treating the prototypes as canonical or
placing BPM under MOD-0023 would create ownership drift across process design, approvals, tasks, rules,
semantics and delivery.

## 4. Capability boundary

**Inside the family:** process architecture/model governance; later process-performance bindings/observations;
later process-improvement cases and typed delivery handoff.

**Outside:** executable Process Runtime/BPMN engine, `ProcessInstance`, token/activity/job execution, approval
workflow, task/checklist engine, rule master, KPI/metric master, enterprise owner master, audit store, and PPM
delivery records.

## 5. Member modules and follow-ups

| Order | Member | State | Scope |
|---|---|---|---|
| 1 | `CAND-CAP-0007-FU01 — Process Architecture & Modeling` | draft pack | First and only authored member |
| 2 | FU02 Process Performance Management | future plan only | No ID reservation or pack file in this phase |
| 3 | FU03 Process Improvement Management | future plan only | No ID reservation or pack file in this phase |
| Deferred | Executable Process Runtime / BPMN engine | excluded | New Control Tower architecture decision required |

## 6. Ownership map

| Object/concern | SoR owner | BPM relationship |
|---|---|---|
| ProcessArchitecture/Definition/Model/PublishedVersion | FU01 | owns |
| Enterprise ownership assignment | MOD-0001 | typed reference only |
| Metric/semantic definition | MOD-0004 / MOD-0060 | typed reference only; owner overlap is an EA decision |
| KPI definition/ownership | MOD-0059 | typed reference only |
| Audit trail | MOD-0021 | emit/correlate; never persist general audit master |
| Approval/SLA/escalation | MOD-0023 | optional lifecycle gate binding |
| Task/checklist | MOD-0024 | future template/action contract only |
| Business rule/version/test evidence | MOD-0025 | future rule-set/evaluation reference only |
| PPM demand/work/project/benefit | MOD-0117 | future typed handoff/read reference only |

## 7. Dependency graph

```text
Identity reservation + candidate gate
  -> DCP-005 draft + BPM domain governance
  -> FU01 draft + owner/topology decisions
  -> explicit DCP approval + explicit FU01 ready-for-dev
  -> FU01 implementation (separate authorization)
  -> future FU02
  -> future FU03 -> typed MOD-0117 handoff

MOD-0001/0004/0021/0023/0024/0025/0059/0060/0117 are external owners/contracts,
not implementation dependencies that this draft pretends already exist.
```

## 8. Ordered delivery sequence

1. Reserve candidate and pass DCP-002 candidate gate.
2. Author DCP/domain/FU01 drafts; grant no execution authority.
3. Resolve business owner, technical owner, service topology, artifact storage, reference providers,
   lifecycle policy and permission prefix.
4. Review and explicitly promote DCP-005.
5. Review and explicitly promote FU01 separately.
6. Implement FU01 only under a new Control Tower authorization.
7. Author FU02 only after FU01 contract evidence.
8. Author FU03 only after performance/PPM handoff decisions.
9. Treat executable runtime as a separate future discovery, not an implied FU.

## 9. Prerequisites

- Candidate registry and reconciliation records remain consistent and gate PASS.
- Blueprint ownership contradiction scan remains clean.
- DCP and member pack retain independent approval gates.
- Domain owner and topology decisions close before `ready-for-execution`/`ready-for-dev`.
- MOD-0025 registry/pack readiness is reconciled before any rule integration is claimed.

## 10. Architecture decisions

- BPM is not a child of MOD-0023, MOD-0024 or MOD-0025.
- One candidate parent coordinates sequential member slices.
- FU01 model artifacts are design/documentation artifacts with `isExecutable=false`.
- Published model versions are immutable; revision creates a new monotonic version.
- Typed references carry external owner/resource IDs; BPM never forks external masters.
- Candidate IDs are documentation identities only and never serialized as runtime module codes.
- Primary topology proposal is a separate bounded `Diten.BusinessProcessManagementService`; fallback is an
  isolated `BusinessProcessManagement` module in a future explicitly approved Management Governance owning
  service. They are mutually exclusive: no dual write, mirrored collection, runtime failover or compatibility
  write is permitted. `Diten.Platform` and legacy `Diten.EnterpriseStrategyService` are excluded owners.
- The projected base is `Diten.Platform.Common.Persistence.TenantScopedEntity`; its missing `DeletedAt`
  contract must be resolved by approved base composition/amendment before implementation.
- FU01 persists exact lifecycle states `Draft -> Review -> Published -> Retired`. Published/Retired never reopen;
  revision creates a new monotonic Draft and never rewrites a predecessor.
- Publish and retire approval are mandatory for FU01. MOD-0023 alone decides approval; BPM independently checks
  BPM permission and idempotently applies a matching terminal approved result.
- Parallel effective-dated variants are excluded. A definition has one non-overlapping published timeline and
  at most one effective Published version at an instant.
- `bpm.*` is family notation only, never an enforceable wildcard; exact proposed keys are listed in the FU01
  pack and remain subject to domain/security owner approval.
- FU01 owns model metadata, canonical content manifest, canonicalization profile/version and SHA-256 checksum;
  external content/document storage owns XML/binary bytes. No arbitrary URL/path is a valid artifact reference.
- FU01 has zero metric/KPI, performance-observation, improvement, benefit or PPM-handoff schema.

## 11. Scope

This governance phase is limited to identity reservation, this DCP, BPM domain README/config, FU01 draft pack,
and narrow AGENTS.md domain/branch documentation. It does not authorize product files.

## 12. Explicit exclusions

- Runtime/service/test project or scaffold.
- Frontend, placeholder, navigation, Gateway or WorkCenter change.
- Executable BPMN, ProcessInstance, token/activity/job state or orchestration.
- Local WorkflowTemplate/Instance/ApprovalTask, task/checklist, rule, KPI/metric, owner, AuditEvent, project,
  work-record or benefit masters.
- FU02/FU03 pack files.
- `.antigravity/**` changes.

## 13. Governance drift risks

- Mistaking `mod-process-*` navigation IDs for canonical aliases.
- Treating visual BPMN authoring as an executable engine.
- Duplicating MOD-0001 owner assignments or MOD-0004/0059/0060 semantic masters.
- Recreating MOD-0023 approvals, MOD-0024 tasks or MOD-0025 rules.
- Turning improvement targets/outcomes into MOD-0117 benefit realization.
- Treating an unavailable external contract as implemented.
- Writing `CAND-CAP-0007*` into runtime metadata.

## 14. Review questions

Technical decisions are closed by §10 and the FU01 pack. The remaining human/provider-owner decisions are:

1. Appoint the accountable `Business Process Management / Process Excellence Owner`.
2. Appoint the `BPM Domain Technical Owner` and approve primary versus fallback topology/deployment ownership.
3. MOD-0001/provider owner approves owner types, mandatory/optional binding and non-disclosing lookup contract.
4. MOD-0048 owner approves exact process classification sets and tenant/global applicability.
5. MOD-0028/MOD-0262 and security/data owners approve binary/XML storage, retention, legal hold, malware scan,
   encryption/KMS and signed-download contract.
6. MOD-0018/Auth and domain/security owners approve the exact permission literals, seeds and role matrix.
7. MOD-0023/security/business owners approve SoD/self-approval details within mandatory publish/retire approval.
8. Platform/base owner resolves `TenantScopedEntity` plus required `DeletedAt`/`DeletedBy` composition.
9. Infrastructure owner proves same-database Mongo replica-set transactions for publication + receipt + outbox.
10. Enterprise Architect allocates a canonical runtime MOD/ModuleCode before any integration event publication.

MOD-0004/MOD-0060 metric-contract resolution and MOD-0117 handoff target remain FU02/FU03 decisions and do not
enter FU01 schema.

## 15. Gate criteria

- Candidate gate PASS and no DCP-005 collision.
- All 20 DCP and FU01 sections present.
- Ownership scan preserves every external SoR.
- Relative links resolve.
- Runtime/frontend/Gateway/WorkCenter/`.antigravity` diff is zero.
- Domain-owner/topology decisions close before any promotion.
- DCP approval and FU01 approval are separate explicit user decisions.

## 16. Acceptance criteria

1. Family and first-member identities resolve through registry and reconciliation records.
2. DCP lists FU02/FU03 only as future members and creates no pack for them.
3. Executable runtime is explicitly deferred in boundary, sequence and exclusions.
4. Ownership matrix names MOD-0001/0004/0021/0023/0024/0025/0059/0060/0117.
5. Frontend prototype and legacy parity dispositions are explicit.
6. No production authority is implied.

## 17. Downstream business-module impacts

- MOD-0023 may later gate model publish/retire decisions while retaining all approval semantics.
- MOD-0024 and MOD-0025 remain future referenced providers, not copied dependencies.
- Semantic/KPI owners provide IDs; BPM later binds/observes them.
- MOD-0117 may later accept a typed improvement-delivery handoff and then owns delivery lifecycle.
- No downstream runtime impact exists in this draft.

## 18. Open decisions

The human/provider-owner decisions in §14 remain blockers. MOD-0025 registry/pack absence does not block core
FU01 architecture/model authoring; it becomes a hard gate before `RuleSetRef`, rule-conditioned validation,
rule evaluation or a rule-dependent lifecycle acceptance criterion is introduced. No local fallback rule engine
is permitted.

## 19. Future follow-ups

- FU02 Process Performance Management authoring.
- FU03 Process Improvement Management authoring.
- Canonical Blueprint MOD allocation by Enterprise Architect.
- Optional executable-runtime discovery under a new DCP amendment/Control Tower authorization.
- Frontend mapping only after an approved member pack expressly authorizes it.

## 20. Audit and reconciliation notes

- Legacy Enterprise Strategy counterpart: **not found**.
- Classification: **new capability candidate**.
- Migration evidence: **none**.
- Existing `mod-process-*` IDs: prototype/navigation only, not aliases or implementation evidence.
- Reservation-only checkpoint precedes this authoring checkpoint.
- Governance reviews must record findings without changing runtime truth.
