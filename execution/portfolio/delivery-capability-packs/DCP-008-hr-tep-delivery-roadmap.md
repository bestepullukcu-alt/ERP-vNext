---
id: DCP-008
slug: hr-tep-delivery-roadmap
name: HR & TEP Delivery Roadmap
type: Delivery Capability Pack
standard: CAP-001
status: approved
owner_domain: pending-ea-domain-bootstrap
owner: enterprise-architect / platform-team / proposed-hr-hcm-domain / proposed-talent-ecosystem-domain
branch: feature/governance/hris-source-readiness
created: 2026-08-06
source_workbook: "/Users/cihan/Downloads/Project ongoing status report 15 June 2026 (1) (1).xlsx"
source_sheet: "HR & TEP"
production_implementation_authorized: false
---

# DCP-008 - HR & TEP Delivery Roadmap

> **Artifact type:** This is a Delivery Capability Pack. It is not a runtime
> entity, not a product module, and not a substitute for the member module
> packs required by `module-pack-standard.md`.

> **Execution authorization:** `draft` only. This pack authorizes no production
> code, no production service scaffold, no Gateway route change, and no
> `@orchestrator` development call. Runtime work remains blocked until this DCP
> is approved or ready-for-execution and each member module has its own
> approved or ready-for-dev module pack.

## 1. Identity and Status

| Field | Value |
|---|---|
| ID | `DCP-008` |
| Name | HR & TEP Delivery Roadmap |
| Type | Delivery Capability Pack |
| Standard | `CAP-001` |
| Status | `draft` |
| Source workbook | `/Users/cihan/Downloads/Project ongoing status report 15 June 2026 (1) (1).xlsx` |
| Source sheet | `HR & TEP` |
| Excel row count | 79 module rows, rows 4-82 |
| Owner domain | Pending EA domain bootstrap |
| Runtime owner | Pending EA service/domain decision |
| Production implementation | Not authorized |

### Why a single module pack is not sufficient

This work cannot be represented as one module pack because the Excel scope spans
79 module rows across five releases, multiple lanes, shared platform backbone
services, external source/provider integrations, native HCM modules, native TEP
modules, analytics backbone dependencies, and missing domain ownership. A module
pack is the contract for one module. This DCP is the governance and sequencing
contract that decides the delivery boundary, phase order, ownership, blockers,
and module-pack creation order before individual module packs are prepared.

### Read-only evidence used for this draft

- `AGENTS.md` lists only `developer-enablement`, `master-data-management`, and
  `platform-shared-services` under `execution/domains/`.
- `AGENTS.md` requires new module development to start only from an approved or
  ready-for-dev module pack.
- `capability-pack-standard.md` requires a Delivery Capability Pack when work
  crosses multiple modules, platform foundations, or business-module enforcement
  points.
- `module-id-registry.md` states the Blueprint workbook
  `docs/System Capability & Implementation Blueprint - master 5.xlsx` sheet
  `Blueprint_Data` is the canonical authority for every `MOD-xxxx` ID and
  canonical name.
- `module-id-registry.md` records `MOD-0297`, `MOD-0298`, and `MOD-0299` as
  deprecated aliases for unrelated platform subscription/billing candidates.

## 2. Business Outcome

Establish a governed delivery roadmap for Human Capital Management (HCM) and
the Talent Ecosystem Platform (TEP), starting with shared backbone readiness and
ending with HCM/TEP completion modules. The outcome is a safe, tenant-isolated,
auditable, phased delivery path that prevents identity collisions, domain leaks,
duplicate HR/person/org data ownership, and premature production implementation.

## 3. Problem Statement

The HR & TEP Excel sheet defines a broad delivery program, but the repository is
not yet ready to execute it directly:

- HR/HCM and TEP domains are not scaffolded in `execution/domains/`.
- Many Excel `MOD-xxxx` IDs do not exist in the Blueprint canonical source.
- Three R1 HCM IDs collide with deprecated platform alias chains.
- Many R0 backbone dependencies are platform/shared modules with their own
  readiness gates.
- Native HR and TEP modules require separate module packs, owner domains, service
  decisions, shell decisions, data ownership boundaries, authorization rules,
  retention rules, and PII/privacy decisions.

Without this DCP, a single module pack would hide cross-domain dependencies and
increase the risk of starting runtime work under invalid IDs.

## 4. Capability Boundary

This Delivery Capability Pack owns only governance and delivery sequencing for
the HR & TEP roadmap.

In boundary:

- R0 backbone/source-readiness sequencing for HR/TEP prerequisites.
- R1 HCM Foundation MVP sequencing.
- R2 Talent Ecosystem Platform MVP sequencing.
- R3 HCM Completion sequencing.
- R4 TEP Completion sequencing.
- Domain bootstrap gate for HR/HCM and TEP.
- DCP-002 canonicalization and collision gates.
- Required module-pack creation order.
- Cross-domain ownership map and downstream impact rules.

Out of boundary:

- Production code.
- Service scaffold implementation.
- Database schema implementation.
- Frontend pages.
- Gateway route edits.
- Provider selection/procurement.
- Any new `MOD-xxxx` invention.
- Direct replacement of individual module packs.

## 5. Member Modules and Follow-ups

### R0 - Platform Backbone + HR Source Readiness

| Release | Capability Block | Lane | Module Code | Module Name | Type | Output / Role |
|---|---|---|---|---|---|---|
| R0 | Platform Backbone + HR Source Readiness | R0-A | MOD-0018 | RBAC / Authorization | Backbone readiness | HR and TEP permission control |
| R0 | Platform Backbone + HR Source Readiness | R0-A | MOD-0021 | Audit Trail | Backbone readiness | Access/change/export audit |
| R0 | Platform Backbone + HR Source Readiness | R0-A | MOD-0030 | Records Management / Retention / Legal Hold | Backbone readiness | HR/TEP retention and legal hold |
| R0 | Platform Backbone + HR Source Readiness | R0-B | MOD-0023 | Workflow | Backbone readiness | Approval, dispute, review workflows |
| R0 | Platform Backbone + HR Source Readiness | R0-B | MOD-0028 | Documentation & Evidence Management | Backbone readiness | Evidence/document storage |
| R0 | Platform Backbone + HR Source Readiness | R0-B | MOD-0031 | Evidence Linking Service | Backbone readiness | Link evidence to HR/TEP objects |
| R0 | Platform Backbone + HR Source Readiness | R0-C | MOD-0048 | Reference Data / Lookups | Backbone readiness | HR/TEP controlled values |
| R0 | Platform Backbone + HR Source Readiness | R0-C | MOD-0057 | Taxonomy / Semantic Tagging | Backbone readiness | Skills, roles, categories, tags |
| R0 | Platform Backbone + HR Source Readiness | R0-D | MOD-0251 | HRIS External SoR | Existing HR source | Employee/job/org source |
| R0 | Platform Backbone + HR Source Readiness | R0-D | MOD-0279 | Payroll Engine | Existing HR source | Payroll/pay source readiness |
| R0 | Platform Backbone + HR Source Readiness | R0-D | MOD-0280 | Time & Attendance | Existing HR source | Attendance/leave/schedule source readiness |
| R0 | Platform Backbone + HR Source Readiness | R0-D | MOD-0281 | Payroll Integration & Governance | Existing HR integration | Payroll integration governance |
| R0 | Platform Backbone + HR Source Readiness | R0-D | MOD-0288 | Organization, Person & Position Directory | Existing platform foundation | Person/org/position foundation |

### R1 - Human Capital Management Foundation MVP

| Release | Capability Block | Lane | Module Code | Module Name | Type | Output / Role |
|---|---|---|---|---|---|---|
| R1 | Human Capital Management Foundation MVP | R1-A | MOD-0297 | HR Capability Block Shell | Native HCM MVP | HR workspace shell |
| R1 | Human Capital Management Foundation MVP | R1-A | MOD-0298 | Employee Profile & Employment Record Projection | Native HCM MVP | HRIS-sourced employee projection |
| R1 | Human Capital Management Foundation MVP | R1-B | MOD-0299 | Position & Organization Assignment | Native HCM MVP | Position/org/manager assignment |
| R1 | Human Capital Management Foundation MVP | R1-C | MOD-0314 | HR Governance & Sensitive Access Controls | Native HCM MVP | HR-sensitive visibility and access rules |
| R1 | Human Capital Management Foundation MVP | R1-D | MOD-0305 | Offboarding & Exit Management | Native HCM MVP | Governed offboarding + TEP handoff payload |

### R2 - Talent Ecosystem Platform MVP

| Release | Capability Block | Lane | Module Code | Module Name | Type | Output / Role |
|---|---|---|---|---|---|---|
| R2 | Industry Talent Ecosystem Platform - Reference Network MVP | R2-A | MOD-0321 | Talent Ecosystem Platform Shell | TEP MVP | TEP workspace shell |
| R2 | Industry Talent Ecosystem Platform - Reference Network MVP | R2-A | MOD-0323 | Association Membership & Member Company Registry | TEP MVP | Association/company membership registry |
| R2 | Industry Talent Ecosystem Platform - Reference Network MVP | R2-A | MOD-0324 | Verified HR Participant & Company Access | TEP MVP | Verified HR/company access |
| R2 | Industry Talent Ecosystem Platform - Reference Network MVP | R2-B | MOD-0325 | Consent, Visibility & Access Policy | TEP MVP | Consent and visibility rules |
| R2 | Industry Talent Ecosystem Platform - Reference Network MVP | R2-B | MOD-0326 | Talent Ecosystem Governance & Review Board | TEP MVP | Review board workflow |
| R2 | Industry Talent Ecosystem Platform - Reference Network MVP | R2-B | MOD-0330 | Trust Level & Multi-Signature Engine | TEP MVP | Trust level validation |
| R2 | Industry Talent Ecosystem Platform - Reference Network MVP | R2-C | MOD-0322 | Industry Candidate Identity & Talent Profile | TEP MVP | Sector candidate identity |
| R2 | Industry Talent Ecosystem Platform - Reference Network MVP | R2-D | MOD-0327 | Industry Exit Reference Record Registry | TEP MVP | Cross-company exit reference records |
| R2 | Industry Talent Ecosystem Platform - Reference Network MVP | R2-D | MOD-0328 | Reference Exchange Marketplace | TEP MVP | HR-to-HR reference exchange |
| R2 | Industry Talent Ecosystem Platform - Reference Network MVP | R2-D | MOD-0329 | Rehire Recommendation Network | TEP MVP | Rehire recommendation |
| R2 | Industry Talent Ecosystem Platform - Reference Network MVP | R2-D | MOD-0331 | Candidate Response & Dispute Management | TEP MVP | Candidate response/dispute flow |
| R2 | Industry Talent Ecosystem Platform - Reference Network MVP | R2-E | MOD-0027 | Notification | Backbone extension | HR/candidate/reference notifications |
| R2 | Industry Talent Ecosystem Platform - Reference Network MVP | R2-E | MOD-0263 | Notification Provider / Delivery | Backbone extension | Notification delivery channel |
| R2 | Industry Talent Ecosystem Platform - Reference Network MVP | R2-E | MOD-0029 | Controlled Documents | Backbone extension | Reference templates and controlled policies |
| R2 | Industry Talent Ecosystem Platform - Reference Network MVP | R2-E | MOD-0262 | External Docs Repository | Backbone extension | External document repository integration |

### R3 - Human Capital Management Foundation Completion

| Release | Capability Block | Lane | Module Code | Module Name | Type | Output / Role |
|---|---|---|---|---|---|---|
| R3 | Human Capital Management Foundation Completion | R3-A | MOD-0300 | Recruitment / Applicant Intake | HCM completion | Applicant intake |
| R3 | Human Capital Management Foundation Completion | R3-A | MOD-0301 | Candidate Pipeline & Interview Management | HCM completion | Recruiting pipeline and interviews |
| R3 | Human Capital Management Foundation Completion | R3-A | MOD-0302 | Offer Management | HCM completion | Offer workflow |
| R3 | Human Capital Management Foundation Completion | R3-A | MOD-0303 | Employee Onboarding | HCM completion | Onboarding lifecycle |
| R3 | Human Capital Management Foundation Completion | R3-B | MOD-0304 | Employment Change / Transfer / Promotion | HCM completion | Internal role-change workflow |
| R3 | Human Capital Management Foundation Completion | R3-C | MOD-0306 | Performance Review Management | HCM completion | Performance reviews |
| R3 | Human Capital Management Foundation Completion | R3-C | MOD-0307 | Competency & Skills Assessment | HCM completion | Skills/competency assessments |
| R3 | Human Capital Management Foundation Completion | R3-C | MOD-0308 | Development Plan Management | HCM completion | Development plans |
| R3 | Human Capital Management Foundation Completion | R3-C | MOD-0309 | Learning / Training Records | HCM completion | Employee training records |
| R3 | Human Capital Management Foundation Completion | R3-C | MOD-0320 | Succession & High-Potential Tracking | HCM completion | Succession and HiPo tracking |
| R3 | Human Capital Management Foundation Completion | R3-D | MOD-0310 | Workforce Planning | HCM completion | Workforce planning |
| R3 | Human Capital Management Foundation Completion | R3-D | MOD-0311 | Headcount & Position Budget Planning | HCM completion | Headcount and position budget |
| R3 | Human Capital Management Foundation Completion | R3-E | MOD-0312 | HR KPI & Analytics Facade | HCM completion | HR KPI facade over shared analytics |
| R3 | Human Capital Management Foundation Completion | R3-E | MOD-0313 | HR Documentation & Evidence Workspace | HCM completion | HR workspace over shared docs/evidence |
| R3 | Human Capital Management Foundation Completion | R3-E | MOD-0315 | Time, Attendance & Leave Interface | HCM completion | T&A/leave consumption interface |
| R3 | Human Capital Management Foundation Completion | R3-E | MOD-0316 | Compensation & Benefits Interface | HCM completion | Compensation/benefits consumption interface |
| R3 | Human Capital Management Foundation Completion | R3-E | MOD-0319 | Employee / Manager Self-Service Channel | HCM completion | Employee/manager interaction channel |
| R3 | Human Capital Management Foundation Completion | R3-F | MOD-0317 | Employee Relations & HR Case Management | HCM completion | Grievance/disciplinary/investigation cases |
| R3 | Human Capital Management Foundation Completion | R3-F | MOD-0318 | HR Compliance & Statutory Reporting | HCM completion | HR compliance reporting |
| R3 | Human Capital Management Foundation Completion | R3-G | MOD-0004 | Metric & Semantic Registry | Analytics backbone | Metric semantic governance |
| R3 | Human Capital Management Foundation Completion | R3-G | MOD-0059 | KPI Catalog | Analytics backbone | KPI catalog |
| R3 | Human Capital Management Foundation Completion | R3-G | MOD-0060 | Metric Definitions & Ownership | Analytics backbone | Metric definitions and ownership |
| R3 | Human Capital Management Foundation Completion | R3-G | MOD-0061 | Scorecards / Dashboards | Analytics backbone | HR/TEP dashboard layer |
| R3 | Human Capital Management Foundation Completion | R3-G | MOD-0062 | Baseline & Experiment Measurement | Analytics backbone | Program impact measurement |
| R3 | Human Capital Management Foundation Completion | R3-G | MOD-0063 | Data Warehouse / Lakehouse | Analytics backbone | Data foundation |
| R3 | Human Capital Management Foundation Completion | R3-G | MOD-0064 | ETL / ELT Pipelines | Analytics backbone | Data pipeline foundation |

### R4 - Talent Ecosystem Platform Completion

| Release | Capability Block | Lane | Module Code | Module Name | Type | Output / Role |
|---|---|---|---|---|---|---|
| R4 | Industry Talent Ecosystem Platform Completion | R4-A | MOD-0332 | Hiring Risk Indicators | TEP completion | Rule-based hiring risk signals |
| R4 | Industry Talent Ecosystem Platform Completion | R4-A | MOD-0333 | Early Warning Signals | TEP completion | Cross-company pattern detection |
| R4 | Industry Talent Ecosystem Platform Completion | R4-A | MOD-0334 | Restricted Integrity Registry | TEP completion | Serious restricted integrity cases |
| R4 | Industry Talent Ecosystem Platform Completion | R4-B | MOD-0335 | Professional Reputation Ledger | TEP completion | Positive professional reputation signals |
| R4 | Industry Talent Ecosystem Platform Completion | R4-B | MOD-0336 | Talent Data Foundation | TEP completion | Talent data foundation |
| R4 | Industry Talent Ecosystem Platform Completion | R4-C | MOD-0337 | Industry Talent Pool | TEP completion | Trusted talent pool |
| R4 | Industry Talent Ecosystem Platform Completion | R4-C | MOD-0338 | Industry Skill Passport | TEP completion | Verified skill passport |
| R4 | Industry Talent Ecosystem Platform Completion | R4-C | MOD-0339 | Candidate Career Passport | TEP completion | Career passport |
| R4 | Industry Talent Ecosystem Platform Completion | R4-C | MOD-0340 | Talent Development Network | TEP completion | Talent development pathways |
| R4 | Industry Talent Ecosystem Platform Completion | R4-C | MOD-0341 | Industry Succession Pool | TEP completion | Sector succession pool |
| R4 | Industry Talent Ecosystem Platform Completion | R4-C | MOD-0342 | Verified Certification Registry | TEP completion | Verified certifications |
| R4 | Industry Talent Ecosystem Platform Completion | R4-C | MOD-0343 | Mentorship & Recommendation Network | TEP completion | Mentorship/recommendation network |
| R4 | Industry Talent Ecosystem Platform Completion | R4-D | MOD-0344 | Shared Salary Benchmarking | TEP completion | Sector salary benchmarking |
| R4 | Industry Talent Ecosystem Platform Completion | R4-D | MOD-0345 | Workforce Analytics | TEP completion | Workforce analytics |
| R4 | Industry Talent Ecosystem Platform Completion | R4-D | MOD-0346 | Sector Talent Trends | TEP completion | Sector talent trends |
| R4 | Industry Talent Ecosystem Platform Completion | R4-D | MOD-0347 | Talent Supply & Demand Forecasting | TEP completion | Talent supply/demand forecasting |
| R4 | Industry Talent Ecosystem Platform Completion | R4-D | MOD-0348 | Skills Gap Heatmap | TEP completion | Skills gap intelligence |
| R4 | Industry Talent Ecosystem Platform Completion | R4-D | MOD-0349 | Sector Mobility Intelligence | TEP completion | Talent mobility intelligence |
| R4 | Industry Talent Ecosystem Platform Completion | R4-E | MOD-0350 | Association Operations | TEP completion | Association operating workflows |
| R4 | Industry Talent Ecosystem Platform Completion | R4-E | MOD-0351 | Industry Knowledge Network | TEP completion | Knowledge library/community layer |

## 6. Ownership Map

| Concern | Proposed owner | Current repo evidence / decision |
|---|---|---|
| Shared authorization and permission checks | `platform-shared-services` | PSS owns Auth/RBAC foundations and excludes tenant-side ERP business logic. |
| Audit trail | `platform-shared-services` | Registry reserves `MOD-0021` for Audit Trail Service only. |
| Workflow, documents, evidence, notifications | `platform-shared-services` | PSS domain config owns shared workflow/document/evidence/notification foundations. |
| Organization, Person & Position Directory | `platform-shared-services` | `MOD-0288` is canonical and done; no duplicate person/org/position SoR may be created. |
| HRIS source semantics | Proposed HR/HCM domain, pending EA | Existing DCP-007 proposes HR/workforce domain; no approved repo domain exists yet. |
| Native HCM modules | Proposed HR/HCM domain, pending EA | Missing `execution/domains/human-capital-management` or equivalent. |
| Native TEP modules | Proposed Talent Ecosystem domain, pending EA | Missing `execution/domains/talent-ecosystem-platform` or equivalent. |
| Analytics backbone | Pending EA domain mapping | `MOD-0004` and `MOD-0059`-`MOD-0064` are Blueprint-known but registry/module packs are missing. |
| External providers | PSS substrate plus owning business domain semantics | Provider-specific credentials, contracts, and monitoring must not be embedded in HCM/TEP modules without module packs. |

## 7. Dependency Graph

```text
R0-A Security/Audit/Retention
  MOD-0018 RBAC/Auth
  MOD-0021 Audit
  MOD-0030 Records retention/legal hold
        |
        v
R0-B Workflow/Documents/Evidence
  MOD-0023 Workflow
  MOD-0028 Documentation/evidence storage
  MOD-0031 Evidence linking
        |
        v
R0-C Controlled values and taxonomy
  MOD-0048 Lookups/reference data
  MOD-0057 Taxonomy/tagging
        |
        v
R0-D Source readiness
  MOD-0251 HRIS External SoR
  MOD-0279 Payroll Engine External SoR
  MOD-0280 Time & Attendance External Provider
  MOD-0281 Payroll Integration & Governance
  MOD-0288 Organization, Person & Position Directory
        |
        v
R1 HCM Foundation MVP
        |
        v
R2 TEP Reference Network MVP
        |
        v
R3 HCM Completion + Analytics backbone
        |
        v
R4 TEP Completion
```

## 8. Ordered Delivery Sequence

### Phase 0 - Governance bootstrap and canonicalization

Prerequisites:

- This DCP remains `draft` until reviewed.
- EA decides HR/HCM domain and TEP domain names, owner boundaries, domain short codes, and service ownership.
- DCP-002 is applied to every Excel `MOD-xxxx`.

Member modules:

- All 79 Excel rows are in discovery scope.

Blockers:

- Missing HR/HCM and TEP execution domains.
- Missing Blueprint IDs for most R1-R4 native modules.
- Registry collisions for `MOD-0297`, `MOD-0298`, `MOD-0299`.

Required module-pack creation order:

1. No native HR/TEP module pack may be created until EA canonicalization and domain bootstrap decisions are recorded.
2. Candidate identities must be validated through the candidate gate where Blueprint has no module.
3. Registry reservations must precede module-pack creation.

Acceptance gate:

- DCP approved or ready-for-execution.
- Domain bootstrap plan approved.
- ID collision table closed or explicitly marked as EA-owned open decision.

Downstream impact:

- Prevents invalid runtime literals and avoids building HR/TEP under squatted or deprecated IDs.

### Phase R0 - Backbone and source readiness

Prerequisites:

- Phase 0 governance decisions complete enough to classify owners.
- PSS backbone module packs are approved or ready-for-dev before runtime work.
- External SoR/provider identities use Blueprint canonical names, not friendly Excel names.

Member modules:

- `MOD-0018`, `MOD-0021`, `MOD-0030`, `MOD-0023`, `MOD-0028`,
  `MOD-0031`, `MOD-0048`, `MOD-0057`, `MOD-0251`, `MOD-0279`,
  `MOD-0280`, `MOD-0281`, `MOD-0288`.

Blockers:

- `MOD-0030`, `MOD-0057`, `MOD-0251`, and `MOD-0281` need registry/module-pack readiness.
- `MOD-0279` and `MOD-0280` are Blueprint-valid only under their canonical external SoR/provider names.
- `MOD-0288` is done but must remain the sole Organization/Person/Position Directory owner.

Required module-pack creation order:

1. Confirm existing PSS packs for `MOD-0018`, `MOD-0021`, `MOD-0023`,
   `MOD-0028`, `MOD-0031`, `MOD-0048`, `MOD-0279`, `MOD-0280`,
   and `MOD-0288`.
2. Prepare or reconcile missing R0 packs: `MOD-0030`, `MOD-0057`,
   `MOD-0251`, `MOD-0281`, and `MOD-0262` if used by R2-E.
3. Promote only the R0 member module packs required for the first executable
   slice to `approved` or `ready-for-dev`.

Acceptance gate:

- HR/TEP permission control is backed by `MOD-0018`.
- HR/TEP access/change/export audit is backed by `MOD-0021`.
- Retention/legal-hold policy owner is clear.
- Workflow/document/evidence contracts are available before HR/TEP consumes them.
- HRIS/payroll/time-attendance sources publish only approved contracts.
- `MOD-0288` remains canonical for person/org/position references.

Downstream impact:

- R1 HCM MVP and R2 TEP MVP can consume shared controls and source references
  without duplicating platform foundations.

### Phase R1 - Human Capital Management Foundation MVP

Prerequisites:

- Phase R0 acceptance gate closed.
- HR/HCM domain bootstrap approved.
- EA assigns valid identities or CAND-CAP placeholders for all R1 rows.
- Module packs for each R1 member are created after registry/candidate decisions.

Member modules:

- `MOD-0297` HR Capability Block Shell.
- `MOD-0298` Employee Profile & Employment Record Projection.
- `MOD-0299` Position & Organization Assignment.
- `MOD-0314` HR Governance & Sensitive Access Controls.
- `MOD-0305` Offboarding & Exit Management.

Blockers:

- `MOD-0297`, `MOD-0298`, and `MOD-0299` collide with existing deprecated
  platform aliases and cannot be used as HR/HCM runtime identities.
- `MOD-0314` and `MOD-0305` are missing from Blueprint/registry.
- HR/HCM domain and service are missing.

Required module-pack creation order:

1. HR/HCM shell pack after EA identity decision.
2. Employee projection pack after `MOD-0251` and `MOD-0288` boundaries are fixed.
3. Position/org assignment pack after `MOD-0288` reuse rules are confirmed.
4. Sensitive access controls pack after `MOD-0018` data-scope requirements are approved.
5. Offboarding pack after workflow, audit, evidence, retention, and TEP handoff
   contracts are defined.

Acceptance gate:

- No R1 pack uses `MOD-0297`, `MOD-0298`, or `MOD-0299` unless EA reassigns the
  IDs and reconciles existing registry aliases.
- Employee profile is a projection from HRIS/source contracts, not a duplicate
  external HRIS SoR.
- Position/org assignment consumes `MOD-0288` canonical references.
- HR-sensitive access has explicit permission and data-scope rules.

Downstream impact:

- Provides the first internal HCM workspace and the handoff payloads needed by
  TEP without authorizing the broader R3 HCM completion scope.

### Phase R2 - Talent Ecosystem Platform MVP

Prerequisites:

- Phase R1 foundation contracts exist.
- TEP domain bootstrap approved.
- Candidate/reference network privacy, consent, visibility, and dispute
  policies are approved by EA/legal stakeholders.
- Notification, provider delivery, controlled documents, and external docs
  repository dependencies have module-pack readiness.

Member modules:

- `MOD-0321`, `MOD-0323`, `MOD-0324`, `MOD-0325`, `MOD-0326`,
  `MOD-0330`, `MOD-0322`, `MOD-0327`, `MOD-0328`, `MOD-0329`,
  `MOD-0331`, `MOD-0027`, `MOD-0263`, `MOD-0029`, `MOD-0262`.

Blockers:

- Native TEP IDs `MOD-0321`-`MOD-0331` are missing from Blueprint/registry.
- TEP domain and service are missing.
- Legal/privacy/consent model is not yet recorded as an approved module pack.
- `MOD-0029` and `MOD-0262` require registry/module-pack readiness.

Required module-pack creation order:

1. TEP shell after EA identity/domain decision.
2. Association membership and verified HR/company access.
3. Consent, visibility, access policy, and governance review board.
4. Candidate identity and talent profile.
5. Exit reference record registry.
6. Reference exchange, rehire recommendation, dispute management.
7. Notification/document/provider extension packs where not already approved.

Acceptance gate:

- Consent and visibility rules are explicit, testable, and fail-closed.
- Candidate dispute workflow is available before negative reference exchange is
  treated as externally consumable.
- TEP does not copy HCM employee SoR data beyond approved projection/reference
  contracts.
- Notification/document dependencies are approved before user-facing exchange
  workflows.

Downstream impact:

- Enables the reference-network MVP while keeping reputational analytics,
  benchmarking, and advanced intelligence deferred to R4.

### Phase R3 - Human Capital Management Foundation Completion

Prerequisites:

- R1 HCM MVP stable.
- R0 shared services stable.
- Analytics backbone owner and identity decisions are approved.
- Each HCM completion module has a dedicated module pack.

Member modules:

- `MOD-0300`, `MOD-0301`, `MOD-0302`, `MOD-0303`, `MOD-0304`,
  `MOD-0306`, `MOD-0307`, `MOD-0308`, `MOD-0309`, `MOD-0320`,
  `MOD-0310`, `MOD-0311`, `MOD-0312`, `MOD-0313`, `MOD-0315`,
  `MOD-0316`, `MOD-0319`, `MOD-0317`, `MOD-0318`, `MOD-0004`,
  `MOD-0059`, `MOD-0060`, `MOD-0061`, `MOD-0062`, `MOD-0063`,
  `MOD-0064`.

Blockers:

- Native HCM completion IDs are missing from Blueprint/registry.
- Analytics backbone IDs exist in Blueprint but lack registry/module-pack
  readiness in this repo.
- HR compliance, statutory reporting, employee relations, and compensation
  interfaces require jurisdiction and privacy decisions.

Required module-pack creation order:

1. Recruitment through onboarding sequence: intake, pipeline, offer, onboarding.
2. Employment change workflow.
3. Performance, competency, development, learning, succession.
4. Workforce planning and headcount/budget planning.
5. HR documentation/evidence workspace and time/compensation interfaces.
6. Self-service channel.
7. Employee relations and HR compliance/reporting.
8. Analytics backbone packs before HR KPI/dashboard facade execution.

Acceptance gate:

- Each HCM lifecycle module consumes R0/R1 contracts rather than redefining
  person/org/position, audit, workflow, documents, evidence, or access logic.
- Analytics outputs use governed metric/KPI definitions.
- Compliance/reporting scope is jurisdiction-bound and tenant-isolated.

Downstream impact:

- Completes internal HCM workflows and creates governed analytics inputs for
  later TEP intelligence without starting R4 analytics prematurely.

### Phase R4 - Talent Ecosystem Platform Completion

Prerequisites:

- R2 TEP MVP stable.
- R3 HCM completion data contracts and analytics backbone available.
- EA/legal approve risk, reputation, salary benchmarking, mobility, and
  cross-company intelligence policy boundaries.

Member modules:

- `MOD-0332`, `MOD-0333`, `MOD-0334`, `MOD-0335`, `MOD-0336`,
  `MOD-0337`, `MOD-0338`, `MOD-0339`, `MOD-0340`, `MOD-0341`,
  `MOD-0342`, `MOD-0343`, `MOD-0344`, `MOD-0345`, `MOD-0346`,
  `MOD-0347`, `MOD-0348`, `MOD-0349`, `MOD-0350`, `MOD-0351`.

Blockers:

- All R4 IDs are missing from Blueprint/registry.
- Reputational and risk scoring modules require explicit legal/privacy
  decisions before any runtime design.
- Benchmarking and mobility intelligence require anonymization, aggregation,
  consent, and anti-reidentification gates.

Required module-pack creation order:

1. TEP risk and restricted integrity policy modules after EA/legal approval.
2. Talent data foundation before pools, passports, certifications, mentorship,
   and recommendations.
3. Analytics and benchmarking modules after metric/data contracts are ready.
4. Association operations and knowledge network after membership foundation is
   stable.

Acceptance gate:

- Every R4 module has EA-approved identity and module pack.
- Cross-company data processing has explicit consent, visibility, audit,
  dispute, retention, and aggregation policies.
- No risk/reputation signal is published without review, explainability, and
  dispute paths.

Downstream impact:

- Enables advanced talent network capabilities while protecting the R2 MVP from
  premature high-risk data products.

## 9. Prerequisites

- User review of this `draft` DCP.
- EA canonicalization decision for every missing or colliding `MOD-xxxx`.
- HR/HCM and TEP domain bootstrap decision.
- Domain short-code decisions for branch naming.
- Runtime service ownership decision for HR/HCM and TEP.
- Registry reservations or candidate records before any native HR/TEP module
  pack is created.
- Existing R0 PSS module packs reconciled to current standards where needed.
- All future DataTable module packs must declare `golden_reference` and
  `form_field_count`.

## 10. Architecture Decisions

| ID | Decision |
|---|---|
| AD-001 | This DCP is a governance roadmap only; it does not authorize production implementation. |
| AD-002 | HR/HCM and TEP are not forced into `platform-shared-services`; PSS remains shared foundation and external-provider substrate. |
| AD-003 | `MOD-0288` remains the canonical Organization, Person & Position Directory; HR/TEP modules consume it rather than duplicate it. |
| AD-004 | External HRIS, payroll, and time-attendance integrations must preserve provider-neutral contracts until provider-specific follow-ups are approved. |
| AD-005 | Excel friendly names are not canonical names when Blueprint_Data has a different name. Module packs must use Blueprint canonical names or approved aliases. |
| AD-006 | Missing Blueprint IDs must be marked `CAND-CAP REQUIRED — EA DECISION`; no new `MOD-xxxx` is invented in this pack. |
| AD-007 | `MOD-0297`, `MOD-0298`, and `MOD-0299` are blocked for HR/HCM use until EA resolves existing registry collisions. |
| AD-008 | R0 is prerequisite readiness, not optional parallel backlog, for R1/R2 native delivery. |
| AD-009 | Native HR and TEP module packs must be created only after the relevant domain config exists. |
| AD-010 | Gateway route changes, when later authorized, require the integration-agent ownership gate. |

## 11. Scope

In scope:

- DCP-002 fail-closed analysis for Excel HR & TEP rows.
- Delivery phase sequencing by Release and Lane.
- HR/HCM and TEP domain bootstrap blocker identification.
- R0 shared platform/source-readiness prerequisites.
- Member module list and module-pack creation order.
- Governance gates, acceptance gates, and downstream impact framing.

## 12. Explicit Exclusions

- Production code.
- Service/project scaffold creation.
- Frontend UI implementation.
- Gateway route changes.
- Registry modifications.
- Master plan modifications.
- `.antigravity/**` modifications.
- Branch, staging, commit, push, merge, reset, or clean.
- Provider selection or commercial procurement.
- Any new `MOD-xxxx` assignment.
- `@orchestrator` development prompt generation.

## 13. Governance Drift Risks

- Treating this DCP as a module pack.
- Starting implementation while this DCP is `draft`.
- Starting implementation while member module packs are missing or draft.
- Reusing `MOD-0297`, `MOD-0298`, or `MOD-0299` for HR/HCM despite existing
  deprecated alias chains.
- Creating HR/HCM or TEP runtime code inside PSS merely because the domain is
  missing.
- Duplicating `MOD-0288` person/org/position ownership in HR modules.
- Treating Excel names as canonical names when Blueprint_Data disagrees.
- Creating candidate capability IDs in runtime literals.
- Letting R4 risk/reputation intelligence leak into R2 MVP.
- Building analytics dashboards before metric definitions and ownership are
  governed.

## 14. Review Questions

| Question | Required owner | Status |
|---|---|---|
| Should HR/HCM and TEP be one domain or two domains? | Enterprise Architect | Open |
| What are the domain folder names and branch short codes? | Enterprise Architect | Open |
| What service owns native HCM modules? | Enterprise Architect / platform architect | Open |
| What service owns native TEP modules? | Enterprise Architect / platform architect | Open |
| Should `MOD-0251` remain PSS-governed external SoR or move under an HR domain after bootstrap? | Enterprise Architect | Open |
| How will `MOD-0297`, `MOD-0298`, and `MOD-0299` collisions be resolved? | Enterprise Architect / registry owner | Open |
| Which missing Blueprint IDs become EA-assigned canonical `MOD-xxxx`, and which remain candidate-only? | Enterprise Architect / registry owner | Open |
| What legal/privacy controls are mandatory before TEP reference exchange and reputation features? | Enterprise Architect / legal owner | Open |

## 15. Gate Criteria

### DCP approval gate

- [ ] This DCP is reviewed and promoted from `draft`.
- [ ] HR/HCM and TEP domain bootstrap decision is recorded.
- [ ] DCP-002 missing ID and collision findings are accepted.
- [ ] R0 prerequisite classification is accepted.
- [ ] R1-R4 phase order is accepted.

### Execution gate

- [ ] DCP status is `approved` or `ready-for-execution`.
- [ ] The target domain config exists before native HR/TEP module-pack authoring.
- [ ] Registry/candidate identity is valid before module-pack creation.
- [ ] The next module pack is `approved` or `ready-for-dev`.
- [ ] Protected paths and service boundaries are explicitly listed in the
      member module pack.
- [ ] Production route changes are assigned through the integration-agent gate.

## 16. Acceptance Criteria

- [ ] All 79 Excel HR & TEP rows are represented in this DCP by Release and Lane.
- [ ] The DCP clearly states why one module pack is insufficient.
- [ ] HR/HCM and TEP domain absence is listed as a blocker.
- [ ] Domain bootstrap is defined as a governance step before native module
      pack creation.
- [ ] DCP-002 fail-closed behavior is applied.
- [ ] Blueprint-missing IDs are marked `CAND-CAP REQUIRED — EA DECISION`.
- [ ] Registry collisions for `MOD-0297`, `MOD-0298`, and `MOD-0299` are
      blockers.
- [ ] R0 backbone modules are classified as HR/TEP prerequisites.
- [ ] R1, R2, R3, and R4 are separate delivery phases.
- [ ] Each phase lists prerequisites, member modules, blockers, module-pack
      creation order, acceptance gate, and downstream impact.
- [ ] Production implementation is explicitly blocked.
- [ ] No `@orchestrator` development call is generated.

## 17. Downstream Business-Module Impacts

- HCM modules must consume PSS authorization, audit, workflow, document,
  evidence, notification, and lookup foundations instead of copying them.
- HCM modules must consume HRIS/source and `MOD-0288` directory references
  through approved contracts.
- TEP modules must consume HCM projections and consent/visibility contracts,
  not raw HRIS/provider data.
- TEP reputation, risk, benchmarking, and mobility modules must wait for legal,
  privacy, audit, consent, and analytics gates.
- Analytics consumers must use metric/KPI registry definitions before exposing
  dashboards or cross-company intelligence.

## 18. Open Decisions

### DCP-002 canonicalization status

| Finding | IDs | Required decision |
|---|---|---|
| Registry collision | `MOD-0297`, `MOD-0298`, `MOD-0299` | `CAND-CAP REQUIRED — EA DECISION`; do not use these IDs for HR/HCM until registry is reconciled. |
| Missing Blueprint for native HCM/TEP IDs | `MOD-0300`-`MOD-0351` plus `MOD-0305`, `MOD-0314`, `MOD-0320` where listed in Excel | `CAND-CAP REQUIRED — EA DECISION` or EA-assigned canonical IDs. |
| Missing Blueprint for R1 shell/projection/assignment IDs | `MOD-0297`, `MOD-0298`, `MOD-0299` | Blocked by both missing Blueprint and registry collision. |
| Blueprint-known but name drift | `MOD-0018`, `MOD-0021`, `MOD-0030`, `MOD-0023`, `MOD-0031`, `MOD-0048`, `MOD-0057`, `MOD-0251`, `MOD-0279`, `MOD-0280`, `MOD-0027`, `MOD-0263`, `MOD-0029`, `MOD-0262`, `MOD-0064` | Use Blueprint canonical names or approved aliases in module packs. |
| Blueprint-known but registry/module-pack missing | `MOD-0030`, `MOD-0057`, `MOD-0251`, `MOD-0281`, `MOD-0029`, `MOD-0262`, `MOD-0004`, `MOD-0059`, `MOD-0060`, `MOD-0061`, `MOD-0062`, `MOD-0063`, `MOD-0064` | Registry owner and module-pack authoring required before runtime work. |

### Domain bootstrap status

| Decision | Status |
|---|---|
| HR/HCM domain folder name | Open |
| TEP domain folder name | Open |
| Whether HR/HCM and TEP share one service or split services | Open |
| Whether `MOD-0251` is owned by proposed HR/HCM domain or remains PSS external-source governance | Open |
| Whether analytics backbone belongs to PSS, ESBP, or a future data/analytics domain | Open |

## 19. Future Follow-ups

- Bootstrap HR/HCM execution domain after EA approval.
- Bootstrap TEP execution domain after EA approval.
- Reconcile `AGENTS.md` domain list and branch short-code rules after domain
  bootstrap approval.
- Update `execution/portfolio/master-development-plan.md` only after EA accepts
  the HR & TEP roadmap.
- Update `execution/registries/module-id-registry.md` only after EA
  canonicalization decisions.
- Prepare R0 missing module packs.
- Prepare R1 module packs after collision resolution.
- Prepare R2 module packs after TEP domain and privacy/legal policy decisions.
- Prepare R3/R4 module packs only after their prerequisite phases pass.

## 20. Audit and Reconciliation Notes

### Audit commands and observed outputs

- Excel read: `HR & TEP` sheet contained 79 non-empty module rows from row 4
  through row 82.
- Release counts observed: R0 = 13, R1 = 5, R2 = 15, R3 = 26, R4 = 20.
- Domain inventory read from `execution/domains/`: `developer-enablement`,
  `master-data-management`, `platform-shared-services`.
- Existing DCP files observed before authoring:
  `DCP-001-access-governance.md`, `DCP-002-module-identity-canonicalization.md`,
  `DCP-003-e-signature-delivery.md`, `DCP-004-hris-source-readiness.md`,
  `DCP-005-payroll-integration-governance-readiness.md`,
  `DCP-006-org-directory-expansion-governance.md`,
  `DCP-007-hris-source-readiness.md`.
- DCP-002 sample preflights observed during the prior read-only audit:
  `MOD-0288` with canonical name passed; `MOD-0279` and `MOD-0280` failed with
  Excel friendly names but passed with Blueprint canonical names; `MOD-0297`
  and `MOD-0321` failed closed because the IDs were not in Blueprint.

### Safe next step recommendation

Recommended next step is **EA canonicalization / CAND-CAP decisions first**,
then **domain bootstrap**, then targeted R0 module-pack preparation.

Rationale:

1. Native R1/R2/R3/R4 HR/TEP identities are mostly missing from Blueprint, so
   DCP-002 blocks module-pack creation under those `MOD-xxxx` values.
2. `MOD-0297`, `MOD-0298`, and `MOD-0299` are active collision blockers and
   must be resolved before HCM Foundation MVP planning can proceed.
3. Domain bootstrap needs the EA identity and ownership decision so the domain
   folder names, owner boundaries, branch short codes, and service mapping are
   not guessed.
4. While EA decisions are pending, the only potentially safe execution planning
   is limited to existing R0 PSS module packs whose identities already pass:
   `MOD-0018`, `MOD-0021`, `MOD-0023`, `MOD-0028`, `MOD-0031`, `MOD-0048`,
   `MOD-0279`, `MOD-0280`, and `MOD-0288`. Even there, runtime work still
   requires the relevant member module pack to be `approved` or `ready-for-dev`.

No implementation was started by this DCP.
