---
id: DCP-004
slug: hris-source-readiness
name: HRIS Source Readiness
type: Delivery Capability Pack
standard: CAP-001
status: approved
owner_domain: pending-enterprise-architect-decision
owner: enterprise-architect / platform-team / proposed-hr-workforce-domain
branch: feature/governance-hris-source-readiness
created: 2026-07-10
canonical_module_id: MOD-0251
canonical_module_name: "HRIS (Workday/SuccessFactors/Oracle HCM) [External SoR]"
friendly_name: "HRIS External SoR"
canonical_source: "docs/System Capability & Implementation Blueprint - master 5.xlsx#Blueprint_Data"
---

# DCP-004 - HRIS Source Readiness

> **Artifact type:** This is a Delivery Capability Pack (CAP-001 governance /
> orchestration contract). It is not a runtime entity, not a module pack, not a
> service scaffold, and not approval to implement production code.

> **Canonical-name guard:** The canonical Blueprint name is
> `HRIS (Workday/SuccessFactors/Oracle HCM) [External SoR]`. The friendly name
> `HRIS External SoR` may be used only in explanatory text, navigation wording,
> or future UI-label recommendations. It must not replace the canonical name in
> identity, registry, pack frontmatter, or ownership records.

> **Premature-coding guard:** Production work begins only after this Delivery
> Capability Pack is approved / ready-for-execution and the member MOD-0251
> module pack is separately approved / ready-for-dev.

## 1. Identity and status

| Field | Value |
|---|---|
| ID | `DCP-004` |
| Slug | `hris-source-readiness` |
| Name | HRIS Source Readiness |
| Type | Delivery Capability Pack |
| Status | `draft` |
| Canonical member | `MOD-0251 - HRIS (Workday/SuccessFactors/Oracle HCM) [External SoR]` |
| Friendly name | HRIS External SoR |
| Release / lane | R0 / R0-D |
| Capability block | Platform Backbone + HR Source Readiness |
| Branch | `feature/governance-hris-source-readiness` |
| Owner domain | Pending Enterprise Architect decision |
| Standard | CAP-001 - `.antigravity/rules/capability-pack-standard.md` |

Canonical ID preflight:

```text
OK  MOD-0251: proven against Blueprint/registry.
```

Registry state at authoring: `MOD-0251` is proven by Blueprint and has no
registry collision, but no row exists yet in `execution/registries/module-id-registry.md`.
Because the registry table requires ownership/status fields and ownership is the
subject of this DCP, registry finalization is an explicit approval gate.

Proposed registry row after approval:

| Canonical ID | Canonical Module Name | Slug | Type | Status | Deprecated Alias | Replacement ID | Owner Domain | Notes |
|---|---|---|---|---|---|---|---|---|
| MOD-0251 | HRIS (Workday/SuccessFactors/Oracle HCM) [External SoR] | hris-external-sor | External SoR | planned / DCP-required |  |  | pending-approved-HR-workforce-domain | Blueprint-backed external SoR for Employee, Org and Job. Friendly label: HRIS External SoR. |

## 2. Business outcome

Establish a governed, provider-neutral HRIS source-readiness path before any
runtime HR integration begins. The outcome is a clean ownership model where:

- generic integration substrate remains in Platform Shared Services;
- HR-specific source semantics, mapping, synchronization policy, checkpoints,
  and provenance are owned by an approved HR/workforce domain or service;
- MOD-0251 governs the external HRIS source boundary for Employee, Org, and Job;
- MOD-0288 consumes governed contracts without duplicating HRIS transactional
  ownership.

## 3. Problem statement

The Blueprint places MOD-0251 as an external system of record, not as an internal
employee-management module. The repository does not yet have an HR & TEP domain,
an HR-specific production service, or a MOD-0251 module pack. At the same time,
Platform Shared Services already owns generic integration foundations such as
secrets, scheduler, event bus/outbox, interface registry, audit, and integration
monitoring. If MOD-0251 is implemented without a Delivery Capability Pack, the
project risks:

- putting HR business semantics into Platform Shared Services;
- duplicating MOD-0288 internal directory ownership;
- creating provider-specific Workday, SuccessFactors, or Oracle HCM behavior too
  early;
- storing HR PII or secrets in the wrong place;
- implementing synchronization before ownership, conflict handling, and audit
  contracts are approved.

## 4. Capability boundary

### In scope for this Delivery Capability Pack

- HR/workforce domain ownership decision.
- Service ownership decision.
- Registry mapping gate for MOD-0251.
- MOD-0251 module pack preparation sequence.
- Dependency readiness and fallback decisions.
- MOD-0251 / MOD-0288 boundary.
- Provider-neutral source, mapping, checkpoint, provenance, and sync contracts.
- Security, compliance, and tenant-isolation gates.

### Candidate MOD-0251 ownership

MOD-0251 owns or coordinates:

- provider-neutral external HRIS source definition;
- source-system identity and display metadata;
- external company / tenant identifier metadata;
- connection profile references, never raw credentials;
- source capability declarations;
- source-of-truth ownership declarations;
- Employee / Org / Job mapping metadata;
- external-to-internal identifier mapping governance;
- synchronization mode, schedule reference, cursor, and checkpoint metadata;
- source health and synchronization status references;
- mapping/schema version metadata;
- provenance and operational audit references.

### Explicit exclusions

MOD-0251 does not own:

- full employee lifecycle management;
- recruitment;
- onboarding;
- payroll calculation;
- leave;
- benefits;
- performance;
- learning;
- workforce planning;
- time and attendance calculations;
- internal person/org/position directory ownership;
- generic secret storage;
- generic scheduler implementation;
- generic event bus implementation;
- generic integration monitoring infrastructure;
- provider-specific adapter implementation.

### Relationship to MOD-0288

- MOD-0251 is the external source boundary for Employee, Org, and Job.
- MOD-0288 owns the internal platform person/org/position directory.
- MOD-0288 consumes governed data/contracts from MOD-0251.
- MOD-0251 must not duplicate MOD-0288 OrganizationUnit, Person, Position, or
  PositionAssignment ownership.
- Transformation, identity resolution, conflict handling, and acceptance tests
  must be assigned before MOD-0251 implementation starts.

## 5. Member modules and follow-ups

| Member | Role in this DCP | Current state |
|---|---|---|
| `MOD-0251` | External HRIS source boundary; future module pack required | Blueprint-proven; registry row missing; pack missing |
| `MOD-0288` | Internal Organization, Person & Position Directory consumer | Registry `done`; module pack `done`; runtime merged evidence |
| `MOD-0002` | Interface Registry / contract metadata | Registry `approved`; module pack `approved` |
| `MOD-0012` | Secrets & Configuration Vault | Registry `review`; module pack `review`; shared secret library exists |
| `MOD-0021` | Audit Trail Service / General Audit Trail | Registry `ready-for-dev / implemented evidence`; pack `ready-for-dev` |
| `MOD-0026` | Scheduler / Job Orchestration | Registry and pack `done` |
| `MOD-0035` | Event Bus / Message Queue / Outbox | Registry `partial`; pack `partial`; live broker proof pending |
| `MOD-0037` | Integration Monitoring & Reconciliation | Registry `review / planned`; pack deferred / not implemented |
| `MOD-0279` | Payroll Engine external SoR | Blueprint-only in current repo evidence; no registry row found |
| `MOD-0281` | Payroll Integration & Governance | Blueprint-only in current repo evidence; no registry row found |

Future work items may include provider adapters, MOD-0288 consumer integration,
sync orchestration, conflict resolution, and HR-specific admin UI. This DCP does
not allocate child or follow-up IDs for those items.

## 6. Ownership map

| Concern | Authoritative owner | MOD-0251 storage posture | Consumers / notes |
|---|---|---|---|
| Employee | External HRIS upstream SoR; HR/workforce domain to govern integration semantics | References, mappings, metadata only unless later approved | MOD-0288 and future HR modules |
| Organization / Org | External HRIS upstream for HR source data; MOD-0288 owns internal org directory | Mapping metadata and provenance only | MOD-0288 consumes governed inputs |
| Job | External HRIS upstream SoR | Mapping metadata and external identifiers | MOD-0288 Position/Job mapping contract to be defined |
| Person | MOD-0288 internal directory | Reference only | No duplicate person aggregate in MOD-0251 |
| Position | MOD-0288 internal directory | Reference / mapping metadata only | No Position persistence ownership in MOD-0251 |
| Employment | External HRIS / future HR domain | Metadata only in MOD-0251 | Future HR modules |
| Compensation | Separate HR/payroll capability | Not stored by MOD-0251 | PII/high sensitivity; out of scope |
| Payroll | MOD-0279 external payroll SoR and MOD-0281 payroll integration governance | Not stored by MOD-0251 | Explicitly separate |
| Time and attendance | Future time/attendance capability | Not stored by MOD-0251 | Explicitly separate |
| External identifiers | MOD-0251 candidate ownership | Stores mapping keys and provenance metadata | Required for sync idempotency |
| Source definitions | MOD-0251 candidate ownership | Stores source metadata and enabled state | Tenant/admin operations |
| Sync checkpoints | MOD-0251 candidate ownership | Stores cursor/checkpoint metadata, no raw payload by default | Scheduler / sync workers |
| Integration history | MOD-0037 future owner for generic integration assurance; MOD-0251 may link HRIS-specific run refs | References and HRIS-specific status metadata | Ops and audit |
| Mapping definitions | MOD-0251 candidate ownership | Stores mapping version metadata and field/aggregate mapping config | MOD-0288 consumer contract |
| Secrets | MOD-0012 | Secret references only | No raw credentials in MongoDB, logs, API, or UI |
| Audit events | MOD-0021 | Audit reference / correlation metadata only | General audit trail |

## 7. Dependency graph

```text
Blueprint MOD-0251 external HRIS SoR
   |
   +-- requires ownership decision: HR/workforce domain and service model
   |
   +-- consumes PSS substrate
   |     MOD-0002 Interface Registry
   |     MOD-0012 Secrets & Configuration Vault
   |     MOD-0021 Audit Trail Service
   |     MOD-0026 Scheduler / Job Orchestration
   |     MOD-0035 Event Bus / Message Queue
   |     MOD-0037 Integration Monitoring & Reconciliation
   |
   +-- produces governed contracts for
         MOD-0288 Organization, Person & Position Directory

Payroll remains separate:
   MOD-0279 Payroll Engine external SoR
   MOD-0281 Payroll Integration & Governance
```

## 8. Ordered delivery sequence

### Stage 0 - Governance

1. Approve or revise this draft DCP.
2. Approve exact registry row for MOD-0251.
3. Decide HR/workforce domain ownership and domain short code.
4. Decide whether a new HR production service is required.
5. Record protected paths and repository scope.

### Stage 1 - MOD-0251 module pack

1. Prepare a `status: draft` module pack for MOD-0251.
2. Resolve owned objects and base-entity model.
3. Define source, mapping, identifier, checkpoint, and provenance contracts.
4. Define authorization model and UI requirement.
5. Define acceptance criteria and test expectations.

### Stage 2 - Generic substrate integration

1. Register contracts through MOD-0002 where applicable.
2. Use MOD-0012 secret references.
3. Use MOD-0026 scheduling abstractions.
4. Use MOD-0021 audit correlation.
5. Use MOD-0035 event/outbox only when live-readiness gates are satisfied.
6. Link to MOD-0037 monitoring when the deferred module is available.

### Stage 3 - Provider-neutral source runtime

1. Implement provider-neutral source registration only after module pack approval.
2. Add connection-validation abstraction without selecting a vendor.
3. Add mapping metadata and checkpoint model.
4. Add synchronization orchestration behind approved abstractions.

### Stage 4 - Consumer integration

1. Define MOD-0288 consumer contract.
2. Define identity resolution and conflict policy.
3. Verify cross-module acceptance tests for MOD-0251 -> MOD-0288.
4. Confirm no duplicate MOD-0288 directory ownership.

### Stage 5 - Provider adapters

1. Prepare separately approved provider-specific work items.
2. Do not invent child IDs in this DCP.
3. Do not select Workday, SAP SuccessFactors, or Oracle HCM as the first vendor
   without an approved downstream decision.

## 9. Prerequisites

- MOD-0251 canonical identity preflight remains passing.
- MOD-0251 registry row is approved by the registry owner / Enterprise Architect.
- HR/workforce domain ownership is approved.
- Service model is approved.
- MOD-0251 module pack is prepared as `draft` and later explicitly approved.
- Security and compliance requirements are accepted.
- Provider-neutral architecture is accepted.
- Dependency readiness is reviewed before implementation.

## 10. Architecture decisions

| ID | Decision | Status |
|---|---|---|
| AD-1 | Use split ownership: PSS owns generic substrate; HR/workforce owns HRIS semantics; MOD-0251 governs the external source boundary. | Recommended; approval required |
| AD-2 | Create no production service scaffold in this DCP. | Locked for this task |
| AD-3 | Do not place MOD-0251 runtime ownership inside MOD-0288. | Locked |
| AD-4 | Keep the core provider-neutral despite vendor names in the canonical title. | Locked |
| AD-5 | Store secret references only; raw credentials are forbidden. | Locked by MOD-0012 / security rules |
| AD-6 | Use MOD-0026 for scheduling if synchronization is approved. | Recommended |
| AD-7 | Use MOD-0035 event/outbox only through existing contracts and only after readiness gates. | Recommended |
| AD-8 | Treat MOD-0037 as deferred; do not block source governance on full monitoring UI. | Recommended |
| AD-9 | Payroll is separate and must not be folded into MOD-0251. | Locked by Blueprint boundary |
| AD-10 | UI need remains open; if needed, it is likely platform-admin or tenant-admin governance UI with sensitive-field masking. | Open |

## 11. Scope

This DCP coordinates:

- canonical identity and registry alignment;
- proposed HR/workforce domain ownership;
- service-model decision;
- MOD-0251 module pack preparation;
- dependency readiness and fallback paths;
- MOD-0251 / MOD-0288 boundary and future integration tests;
- provider-neutral architecture;
- security, compliance, and audit planning.

## 12. Explicit exclusions

- production code;
- service scaffold;
- controllers, handlers, repositories, Razor views, JavaScript, CSS;
- gateway routes;
- provider adapter code;
- provider selection;
- domain registration in `AGENTS.md`;
- `.antigravity/**` changes;
- registry changes before approval;
- unapproved module, follow-up, child, or candidate IDs.

## 13. Governance drift risks

- PSS accidentally owning HR business semantics.
- HR/workforce domain created without Enterprise Architect approval.
- MOD-0251 duplicating MOD-0288 internal directory entities.
- Provider-specific adapters added before provider-neutral contracts.
- Secrets stored as MongoDB fields, logs, API responses, or UI payloads.
- Sync jobs bypassing scheduler/event/outbox standards.
- Integration history implemented locally instead of aligning to MOD-0037.
- Payroll or time/attendance data entering MOD-0251 scope.
- Registry row added with an invented owner domain or noncanonical name.

## 14. Review questions

1. What is the approved HR/workforce domain name?
2. What is the approved domain short code for future branches?
3. Is a new HR production service required, or should service creation be
   deferred until after the module pack?
4. Who owns Employee, Employment, and Job mapping semantics?
5. Which team owns conflict resolution between external HRIS and MOD-0288?
6. Should MOD-0251 have UI in its first implementation slice?
7. Should sync start with pull, webhook, file import, API integration, or an
   adapter-neutral contract only?
8. What retention policy applies to HRIS payload excerpts, errors, and sync logs?
9. Which provider adapter receives the first approved downstream work item?
10. When is MOD-0035 live broker proof sufficient for HRIS event publication?

## 15. Gate criteria

- Canonical identity check passes for MOD-0251.
- Registry row is approved and uses the exact Blueprint name.
- Domain ownership decision is approved.
- Service model decision is approved.
- MOD-0251 module pack is authored as `draft`.
- MOD-0251 module pack later becomes `approved` or `ready-for-dev` by explicit
  user approval before implementation.
- No production code starts from this DCP alone.
- Provider-specific adapter work receives separate approved identities.
- Gateway route work is assigned to integration-agent only after approved scope.

## 16. Acceptance criteria

1. Canonical identity is verified against Blueprint.
2. No registry collision exists for MOD-0251.
3. Registry finalization is recorded as an explicit approval gate.
4. Split ownership model is documented.
5. Domain decision is isolated as an approval gate.
6. Service decision is isolated as an approval gate.
7. MOD-0251 and MOD-0288 ownership boundary is documented.
8. Provider-neutral architecture is documented.
9. Dependency readiness and blocker status are recorded.
10. Security and compliance requirements are documented.
11. Secret-management boundary is documented.
12. Delivery sequencing is documented.
13. Protected paths are documented.
14. No production code is created by this DCP task.
15. No provider is selected.
16. No unapproved IDs are created.
17. Next module-pack step is clearly defined.

## 17. Downstream business-module impacts

- MOD-0288 must consume governed MOD-0251 contracts without becoming an HRIS
  adapter owner.
- Future HR & TEP modules must consume HRIS-derived references through approved
  HR/workforce contracts.
- Payroll modules remain separate and must use MOD-0279 / MOD-0281 boundaries.
- Business modules must not assume MOD-0251 stores full HR data copies.
- Access, audit, and tenant isolation tests must be updated in future module
  packs to verify MOD-0251 boundaries.

## 18. Open decisions

- Approved HR/workforce domain name.
- Approved domain short code.
- Approved service model and service name, if any.
- Registry owner approval for the MOD-0251 row.
- MOD-0251 initial UI decision.
- Synchronization strategy and minimum viable ingestion pattern.
- Payload retention and redaction policy for HR PII.
- Provider adapter identity allocation and sequencing.
- MOD-0288 transformation and conflict resolution owner.

## 19. Future follow-ups

Future follow-ups are intentionally unnumbered until approved by the Enterprise
Architect or registry owner:

- MOD-0251 module pack.
- HR/workforce domain scaffold.
- HR production service scaffold, if approved.
- Provider-neutral runtime implementation.
- MOD-0288 consumer integration.
- Provider-specific adapter packs.
- HRIS sync monitoring and reconciliation integration.
- HRIS security hardening / penetration-test evidence.

## 20. Audit and reconciliation notes

Authoring evidence:

- `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0251 --name "HRIS (Workday/SuccessFactors/Oracle HCM) [External SoR]"` returned OK.
- Blueprint `Blueprint_Data` row for MOD-0251 proves placement `External SoR`,
  build/buy `Partner`, contract bundle `EXT-BASE`, and primary objects
  `Employee; Org; Job`.
- Blueprint dependency row shows MOD-0288 depends on MOD-0251.
- Blueprint SoR map states HRIS remains upstream employee/job SoR while the
  platform directory owns routing/binding references.
- Current repository evidence has no HR & TEP domain, no HR service, and no
  MOD-0251 module pack.
- The registry has no MOD-0251 row and no observed collision.
- This authoring task created no production code, no service scaffold, no module
  pack, no gateway route, and no provider adapter.

Reconciliation to perform after review:

- Update this DCP if the Enterprise Architect changes the domain or service
  recommendation.
- Add the approved registry row when ownership fields are settled.
- Prepare the MOD-0251 module pack only after this DCP is approved or explicitly
  accepted as the planning basis for module-pack authoring.
