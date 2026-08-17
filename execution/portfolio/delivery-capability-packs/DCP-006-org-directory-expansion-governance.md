---
id: DCP-006
slug: org-directory-expansion-governance
name: Organization Directory Expansion Governance
type: Delivery Capability Pack
standard: CAP-001
status: approved
owner_domain: platform-shared-services
owner: enterprise-architect / platform-team / workflow-owners / integration-ops
branch: feature/governance-hris-source-readiness
created: 2026-07-13
canonical_module_id: MOD-0288
canonical_module_name: "Organization, Person & Position Directory"
canonical_source: "docs/System Capability & Implementation Blueprint - master 5.xlsx#Blueprint_Data"
---

# DCP-006 - Organization Directory Expansion Governance

> **Artifact type:** This is a Delivery Capability Pack (CAP-001 governance /
> orchestration contract). It is not a runtime entity, not a module pack, not a
> service scaffold, not a route change, and not approval to implement production
> code.

> **Immutable baseline guard:** The completed parent pack
> `execution/domains/platform-shared-services/module-packs/MOD-0288-organization-person-position-directory.md`
> remains `status: done`. This Delivery Capability Pack does not reopen,
> rewrite, downgrade, expand, or reinterpret that historical record.

> **Premature-coding guard:** Future runtime work begins only after this
> Delivery Capability Pack is approved / ready-for-execution and each child or
> follow-up module pack in the ordered sequence is separately approved or
> ready-for-dev.

## 1. Identity and status

| Field | Value |
|---|---|
| ID | `DCP-006` |
| Slug | `org-directory-expansion-governance` |
| Name | Organization Directory Expansion Governance |
| Type | Delivery Capability Pack |
| Status | `draft` |
| Parent baseline | `MOD-0288 - Organization, Person & Position Directory` |
| Owner domain | `platform-shared-services` |
| Runtime owner candidate | `Diten.Platform`, only where future packs prove MOD-0288 ownership |
| Standard | CAP-001 - `.antigravity/rules/capability-pack-standard.md` |

Parent identity preflight:

```text
OK  MOD-0288: proven against Blueprint/registry.
```

Repository authority currently selects
`docs/System Capability & Implementation Blueprint - master 5.xlsx` as the
Blueprint source for MOD identity validation. No repository Master 7 Blueprint
workbook was found during authoring.

## 2. Business outcome

Create a safe governance path for potential expansion around organization,
person, position, routing, delegation, workflow ownership binding,
module-specific synchronization state, frontend shell decisions, and integration
contracts without disturbing the completed MOD-0288 v1 baseline or duplicating
future HR/HCM, RBAC, audit, workflow, monitoring, or data-contract ownership.

## 3. Problem statement

MOD-0288 is already completed as a backend-only Platform Shared Service slice:
Organization Unit tree, Position, effective-dated Position Assignment, and a
derived Manager Chain input model. New planning asks for broader surfaces such
as Person Directory, Delegation & Assignment Matrix, Org Binding Sync Monitor,
frontend pages, source-managed field rules, and HR/HCM reconciliation.

Those requests are broader than the completed parent pack and touch several
owners: MOD-0251 HRIS source readiness, MOD-0018 RBAC / ABAC Authorization,
MOD-0021 Audit Trail, MOD-0023 Workflow Designer / approvals, MOD-0037
Integration Monitoring & Reconciliation, MOD-0003 Data Contract Registry, and
future HCM assignment ownership. A single MOD-0288 follow-up would silently
absorb unresolved cross-cutting decisions. This DCP coordinates the decisions
before any follow-up pack or implementation begins.

## 4. Capability boundary

### Immutable completed MOD-0288 v1 baseline

The completed parent pack owns:

- Organization Unit tree;
- Position;
- effective-dated Position Assignment;
- tenant ownership for MOD-0288-owned records;
- soft-delete and archive semantics for MOD-0288-owned records;
- minimal derived Manager Chain inputs and contract;
- Diten.Platform backend API, application, domain, infrastructure, and tests
  already merged for the v1 scope.

The completed parent pack explicitly excludes:

- frontend UI, DataTable, Razor, RESX, and gateway route ownership in v1;
- duplicate Legal Entity ownership;
- employee lifecycle, employment contracts, compensation, payroll, leave,
  recruitment, and HR job history ownership;
- permission storage and permission evaluation;
- `IDataScopeResolver` algorithm ownership;
- Tenant User / Tenant Role CRUD;
- delegation / substitution ownership by default.

### Expansion candidates coordinated by this DCP

- Person reference directory projection.
- Organization Directory enhancements beyond the completed tree.
- Position Directory enhancements beyond the completed position model.
- Delegation, ownership-routing, and assignment/routing bindings.
- Workflow owner / approver binding references.
- Module-specific sync ingestion, checkpoint reference, and reconciliation
  status.
- UI shell, DataTable strategy, localization, and gateway route decisions.
- Integration contract bundle proposals, including `ORG-DIR-BUNDLE`.
- Backward-compatible coexistence with existing MOD-0288 APIs and collections.

### Explicit boundary guard

This DCP does not decide that every expansion belongs to MOD-0288. Each row in
the ownership matrix below must be resolved before a child pack is prepared.

## 5. Member modules and follow-ups

| Member | Role in this DCP | Current repository state |
|---|---|---|
| `MOD-0288` | Completed parent baseline and possible consumer/producer for approved directory projections | Registry `done`; module pack `done`; runtime merged evidence |
| `MOD-0288-FU01` | Existing Position Assignment User Reference Validation child | Registry `done`; module pack exists and `done` |
| Future `MOD-0288-FUxx` | Possible single-module child packs after DCP approval | Not allocated by this DCP |
| `MOD-0251` | HRIS external source readiness and upstream employee/org/job source boundary | Draft DCP-004; registry row and module pack missing |
| `MOD-0018` | RBAC / ABAC Authorization and permission evaluation | Registry `ready-for-dev`; multiple FU records exist |
| `MOD-0021` | Audit Trail Service / General Audit Trail | Registry `ready-for-dev / implemented evidence` |
| `MOD-0023` | Workflow Designer / approvals | Registry evidence indicates review / planned |
| `MOD-0037` | Generic Integration Monitoring & Reconciliation | Registry `review / planned` |
| `MOD-0003` | Data Contract Registry | Registry `planned / missing` |
| `MOD-0032` | API Gateway | Registry `review / partial` |
| `MOD-0017-FU01` | Tenant Login & Security Settings under SSO/MFA | Registry `in-progress` |
| `CAND-CAP-0005` | SaaS Billing & Invoicing candidate, replacement for deprecated MOD-0299 | Not related to position assignment ownership |

This DCP does not allocate any new MOD, FU, CAND-CAP, or runtime identity.

## 6. Ownership map

| Concern | Authoritative owner decision | Producer | Consumer | Write authority | Projection role | Contract / follow-up need |
|---|---|---|---|---|---|---|
| Organization Unit directory/tree | Existing MOD-0288 baseline | MOD-0288 | RBAC, workflow, business modules | MOD-0288 v1 APIs | Source for org routing and scope derivation | No new pack unless enhancing beyond v1 |
| Organization Directory enhancements | MOD-0288 FU candidate | MOD-0288 plus approved HRIS source inputs | Workflow, IAM, tenant modules | Future MOD-0288 FU, if approved | Extended read model | Needs FU if fields, source rules, UI, or contracts change |
| Position directory | Existing MOD-0288 baseline | MOD-0288 | RBAC, workflow, business modules | MOD-0288 v1 APIs | Position read model and manager chain input | No new pack unless enhancing beyond v1 |
| Position Directory enhancements | MOD-0288 FU or future HCM owner decision | MOD-0288 / future HCM source | Workflow, IAM | Unresolved | Projection only if source-managed | Formal EA decision required |
| Person reference directory | MOD-0288 FU versus HR/HCM projection decision | MOD-0251 / future HR/HCM source | Workflow, IAM, payroll integration | Unresolved | Minimal PII routing projection only | Formal EA decision and child pack required |
| Legal employee / employment master | HRIS/HCM owner, not MOD-0288 | MOD-0251 / future HR/HCM | MOD-0288 as consumer only | HRIS/HCM | Reference only | MOD-0288 must not duplicate |
| Effective-dated position assignment | Existing MOD-0288 v1 for platform assignment; future HCM assignment owner unresolved | MOD-0288 / future HCM | RBAC, workflow | MOD-0288 v1 or future HCM | Scope and routing input | Decision needed before changing semantics |
| Delegation | Cross-cutting future follow-up; not MOD-0288-owned by default | Workflow/RBAC/MOD-0288 candidate | Workflow, approvals, IAM | Unresolved | Routing binding reference | DCP approval and child pack required |
| Workflow owner / approver binding | Workflow remains execution owner; MOD-0288 may provide references | Workflow + MOD-0288 references | Workflow, approvals | Workflow for execution, MOD-0288 only for references | Binding lookup | Contract with MOD-0023 required |
| Sync ingestion | Shared integration substrate plus module adapter | MOD-0251 or approved source adapter | MOD-0288 projection | Unresolved | Idempotent projection update | Data contract and monitoring gates required |
| Sync Monitor | MOD-0037 owns generic monitoring; module-specific MOD-0288 view may consume it | MOD-0037 + module status refs | Operations UI | MOD-0037 for generic state | Read-only module-specific status | Must not duplicate MOD-0037 |
| Audit evidence | MOD-0021 | MOD-0021 append/export contracts | All participants | MOD-0021 | Evidence links and correlation | Audit event contract required |
| Authorization policy | MOD-0018 | MOD-0018 | APIs and UI visibility | MOD-0018 | Permission checks and data-scope context | No MOD-0288 permission engine |
| Data contracts | MOD-0003 or approved contract governance owner | MOD-0003 | MOD-0288, MOD-0251, MOD-0037 | MOD-0003 | Versioned contract registry | Data Contract Registry readiness required |

## 7. Dependency graph

```text
Completed MOD-0288 v1 baseline
   |
   +--> DCP-006 governance decisions
          |
          +--> MOD-0251 HRIS source readiness / future HR-HCM source model
          +--> MOD-0018 RBAC / ABAC authorization
          +--> MOD-0021 audit evidence
          +--> MOD-0023 workflow owner / approver binding
          +--> MOD-0037 integration monitoring and reconciliation
          +--> MOD-0003 data contract registry
          +--> MOD-0032 gateway routing
          |
          +--> future approved child packs, if any
```

## 8. Ordered delivery sequence

1. Review and approve, reject, or revise this DCP.
2. Resolve repository-vs-external discrepancies for MOD-0251 and MOD-0299.
3. Decide whether Person Directory, delegation, routing bindings, sync monitor,
   and UI surfaces belong to MOD-0288 follow-ups or other module owners.
4. Approve Data Contract Registry readiness or define an interim contract
   governance mechanism.
5. Prepare only the child/follow-up module packs approved by this DCP.
6. For any MOD-0288 child pack, run the DCP-002 follow-up identity verifier
   with `--check-id MOD-0288-FUxx --name ... --parent MOD-0288`.
7. Promote child packs from `draft` only after user review.
8. Begin runtime implementation only through `/add-module` after both this DCP
   and the relevant child pack are approved or ready.
9. Reconcile this DCP after implementation phases complete.

## 9. Prerequisites

- MOD-0288 parent identity remains verifier-proven.
- Completed MOD-0288 pack remains unchanged and `done`.
- DCP-004 for MOD-0251 is approved or revised before HRIS source data is
  treated as production input.
- MOD-0299 is not used for Position & Organization Assignment unless Enterprise
  Architect remaps the registry in a separate governance decision.
- MOD-0003, MOD-0037, MOD-0021, MOD-0023, MOD-0032, and MOD-0018 readiness is
  proven for the specific child pack that consumes them.
- UI shell and DataTable strategy are decided in a child pack, not here.

## 10. Architecture decisions

| ID | Decision |
|---|---|
| AD-1 | The completed MOD-0288 v1 baseline is immutable and remains the source of current runtime truth. |
| AD-2 | This DCP is required because the proposed expansion crosses HRIS, workflow, RBAC, audit, monitoring, contracts, gateway, and frontend concerns. |
| AD-3 | No production implementation is authorized by this DCP while it is `draft`. |
| AD-4 | Person reference data may be a minimal routing projection only; employee or employment master data remains outside MOD-0288. |
| AD-5 | Delegation is not automatically MOD-0288-owned; it requires a formal owner decision. |
| AD-6 | A module-specific Sync Monitor must consume or reference MOD-0037 and must not duplicate generic monitoring infrastructure. |
| AD-7 | Frontend shell, field counts, and Golden Reference decisions belong in future child packs once surfaces are approved. |
| AD-8 | MOD-0299 currently cannot be used for Position & Organization Assignment because the registry maps it to SaaS Billing as a deprecated alias. |
| AD-9 | Master 5 remains the repository-authoritative Blueprint until an explicit supersession decision changes DCP-002 and verifier authority. |
| AD-10 | Backward compatibility with current MOD-0288 APIs and collections is mandatory for any child implementation. |

## 11. Scope

In scope for this DCP:

- governance reconciliation for potential MOD-0288 expansion;
- discrepancy analysis across repository authority and newer planning intent;
- ownership, SoR, and dependency sequencing;
- future child-pack gates and acceptance gates;
- security, tenant, PII, audit, integration, and UI decision gates.

Out of scope for this DCP:

- production backend, frontend, gateway, localization, route, permission seed,
  database, or test changes;
- edits to the completed MOD-0288 parent pack;
- registry remapping for MOD-0251 or MOD-0299;
- copying or promoting external Blueprint workbooks.

## 12. Explicit exclusions

- Rewriting MOD-0288 `status: done`, baseline scope, or acceptance criteria.
- Implementing Person Directory, Delegation Matrix, Org Binding Sync Monitor,
  frontend pages, or new API routes.
- Creating a second RBAC, audit, workflow, monitoring, event bus, or data
  contract registry.
- Duplicating HR legal employee, employment, compensation, payroll, leave,
  recruitment, or job-history ownership.
- Treating MOD-0251 as Core HR / Employee Master without repository proof.
- Treating MOD-0299 as Position & Organization Assignment under current
  registry authority.

## 13. Governance drift risks

- Reopening a completed module pack to smuggle new scope into history.
- Calling missing expansion features bugs instead of new scope.
- Overloading MOD-0288 with HR/HCM semantics.
- Assigning delegation to MOD-0288 when workflow or RBAC owns the behavior.
- Building a sync monitor that duplicates MOD-0037.
- Creating UI pages before shell and Golden Reference decisions are approved.
- Depending on a non-authoritative Master 7 workbook without repository
  supersession.
- Breaking existing MOD-0288 API/collection consumers.

## 14. Review questions

1. Which expansion surfaces, if any, should become MOD-0288 child packs?
2. Is Person Directory a MOD-0288-owned routing projection or a future HR/HCM
   projection consumed by MOD-0288?
3. Who owns delegation: MOD-0288, Workflow, RBAC, Tenant IAM, or a new candidate?
4. Is a module-specific Sync Monitor required before MOD-0037 is ready?
5. Should Organization/Position UI be tenant shell, platform admin shell, or
   split by actor?
6. What is the approved contract owner for `ORG-DIR-BUNDLE`?
7. Does any external Master 7 evidence require a formal Blueprint supersession?
8. Should MOD-0251 receive a registry row and module pack before MOD-0288
   consumes HRIS data?
9. What replaces the external-plan reference to MOD-0299 Position & Organization
   Assignment under current registry authority?

## 15. Gate criteria

- This DCP is approved or ready-for-execution.
- Parent MOD-0288 identity verifier still passes.
- Parent MOD-0288 pack remains unchanged and `done`.
- Each child/follow-up identity is verified before pack creation.
- MOD-0251 repository identity and ownership are reconciled before HR source
  data is used.
- MOD-0299 discrepancy is resolved or avoided.
- Data contract, audit, monitoring, workflow, RBAC, gateway, and UI shell
  dependencies are explicitly accepted or deferred per child pack.
- Backward compatibility and coexistence plan is approved for any runtime
  extension.

## 16. Acceptance criteria

1. The immutable MOD-0288 v1 baseline is recorded accurately.
2. Repository authority versus external/newer planning discrepancies are visible.
3. Expansion candidates are decomposed by owner, producer, consumer, write
   authority, projection role, and contract need.
4. No implementation is authorized while this DCP is draft.
5. No completed parent pack or production file is changed.
6. Future child packs have measurable approval gates.
7. Tenant isolation, PII minimization, audit evidence, source-managed field
   protection, idempotency, concurrency, and rollback expectations are defined.

## 17. Downstream business-module impacts

Business modules may continue consuming the completed MOD-0288 v1 baseline.
They must not assume Person Directory, delegation, workflow binding, sync
monitoring, or UI surfaces exist until approved child packs deliver them.

Future modules that need organization, position, person, or routing data must
depend on approved MOD-0288 / MOD-0251 / workflow / RBAC contracts rather than
forking local directory tables.

## 18. Open decisions

- Enterprise Architect: repository treatment of MOD-0251 versus external
  "Core HR / Employee Master" language.
- Enterprise Architect: current MOD-0299 collision with external Position &
  Organization Assignment planning language.
- Enterprise Architect: whether any Master 7 workbook supersedes Master 5.
- Platform team: UI shell model for future directory surfaces.
- Platform team and workflow owners: delegation and ownership-routing owner.
- Integration owners: `ORG-DIR-BUNDLE` contract governance and versioning.
- Security owners: PII minimization and export permission model for Person
  Directory projections.

## 19. Future follow-ups

Potential follow-up packs, none allocated here:

- MOD-0288-FUxx Organization/Position Directory UI and DataTable surfaces.
- MOD-0288-FUxx Person Reference Directory projection, if approved as MOD-0288.
- MOD-0288-FUxx Workflow owner / approver binding references, if approved as
  MOD-0288 reference data.
- Delegation / substitution pack under the owner selected by this DCP.
- MOD-0251 module pack and registry reconciliation.
- MOD-0003 data contract readiness for `ORG-DIR-BUNDLE`.
- MOD-0037 module-specific monitoring integration.

## 20. Audit and reconciliation notes

Authoring evidence:

- `git status --short` showed four pre-existing untracked governance files:
  MOD-0279 pack, MOD-0280 pack, DCP-004, and DCP-005. They were not modified.
- `git branch --show-current` returned
  `feature/governance-hris-source-readiness`.
- `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0288 --name "Organization, Person & Position Directory"`
  returned `OK  MOD-0288: proven against Blueprint/registry.`
- Blueprint inventory found only
  `docs/System Capability & Implementation Blueprint - master 5.xlsx`.
- Existing DCP files occupy DCP-001 through DCP-005 in this worktree; CAP-001
  defines `DCP-{NNN}-{slug}.md` naming and no separate reservation mechanism.
- Existing MOD-0288-FU evidence found `MOD-0288-FU01` already allocated and
  done. No new FU was allocated because this work is cross-cutting.

Reconciliation status: initial draft prepared. Runtime reconciliation is not
started.

## Appendix A - Repository versus external-plan discrepancy matrix

| Topic | Repository authority | External/newer planning claim | Decision |
|---|---|---|---|
| MOD-0288 | `done` backend-only v1; `shell: none`; no UI/gateway/DataTable; owns org unit, position, position assignment, manager-chain inputs | Broader platform directory with person, delegation, sync monitor, UI surfaces | Treat as expansion candidates, not defects |
| MOD-0251 | DCP-004 draft says `HRIS (Workday/SuccessFactors/Oracle HCM) [External SoR]`; registry row and module pack missing | "Core HR / Employee Master" | Do not use that name until repository authority proves it |
| MOD-0299 | Registry maps MOD-0299 to deprecated SaaS Billing alias replaced by `CAND-CAP-0005` | "Position & Organization Assignment" | Enterprise Architect conflict; do not use MOD-0299 for assignment |
| Master 5 vs Master 7 | Master 5 is the only repository Blueprint found and is named by DCP-002/verifier authority | Higher-version external workbook may propose broader scope | External evidence only until formal supersession |

## Appendix B - Security, data, and coexistence requirements

- Tenant-owned records require server-side tenant context and must not accept
  `TenantId` from DTOs or forms.
- Cross-tenant object access must return `404` or fail closed according to the
  owning service standard.
- Person reference projections must store minimum PII required for routing.
- Source-managed fields must be read-only unless a child pack defines a governed
  correction workflow.
- Export, sync retry, delegation changes, and ownership-binding changes require
  separate permissions and audit evidence.
- Sync ingestion must be idempotent and preserve correlation and trace context.
- Failed sync records must not corrupt successful records.
- Existing MOD-0288 APIs and collections must coexist with any new projection or
  surface until an approved migration plan retires them.
- Rollback must preserve the completed v1 baseline and allow new projections or
  UI surfaces to be disabled without deleting historical directory records.

