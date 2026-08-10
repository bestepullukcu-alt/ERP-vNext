---
id: MOD-0230
name: Case Intake & Triage
domain: pharmacovigilance
service: Diten.PvgService
service_port: 5011
shell: tenant
golden_reference: compact
entity_base: EntityBase
status: ready-for-dev
build_gate: open
operational_runtime_gate: closed
owner: NY (ny@gmgroup.ch)
branch: feature/pvg/mod-0230-case-intake-triage
started: 2026-08-04
approved: 2026-08-09
target: 2026-08-28
form_field_count: 16
slice: "Slice 1 - intake + triage + route. Archive/void and export are out of scope."
---

# MOD-0230 - Case Intake & Triage

> **Status change 2026-08-09.** DCP-004 is `approved` and this pack is `ready-for-dev`. Both CAP-001 §7
> conditions are satisfied, so implementation may begin - **under the build/test gate only.**
>
> **Two gates, not one:**
>
> | Gate | State | Authorizes |
> |---|---|---|
> | **Build / test** | **OPEN** as of 2026-08-09 | Backend, tests, gateway route, tenant UI, in local / dev / CI |
> | **Operational runtime** | **CLOSED** | Production, supplier qualification, validation |
>
> The operational runtime gate stays closed until real MOD-0019, MOD-0023, and MOD-0031 ship and a retention /
> legal-hold owner is named. That row in the evidence table below is still `[ ]` and must not be checked.
> See DCP-004 §10 "Build gate vs operational runtime gate". Any detailed fast-track plan remains a pending
> support package and is not normative until committed.

## Build Authorization (Non-Production) - 2026-08-09

**Authorized now:**

- `services/Diten.PvgService/**` - dedicated service on port **5011** (OD-7).
- `frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/**` and the matching JS / l10n / resource files.
- `gateway/Diten.ApiGateway/**/ocelot.json` - one route family, via integration-agent.
- `tests/**` for the above.

**Slice 1 scope - commands and queries:**

| Surface | In slice 1 | Note |
|---|---|---|
| Create, Update | Yes | - |
| GetList, GetById | Yes | - |
| Triage, Route | Yes | Gated by `IPvgWorkflowTransitionGate` |
| **Archive / Void** | **No** | Requires `PVG-MOD0230-RetentionLegalHoldArchiveVoid-v1`; no compliance / legal-hold owner exists |
| **Export (incl. masked export)** | **No** | Requires a real MOD-0019 approval; masked-only export cannot be proven without a masking owner |
| Delete, BulkDelete | **Never** | Permanently excluded |
| Any AI behaviour | **No** | Governed-AI gates absent |

**Still forbidden:** production deployment, supplier qualification, validation approval, seed data,
`.antigravity/**` edits (including `ports.md`), other domains' packs, and any MedDRA data.

**Prerequisite before scaffolding:** delete the stale ignored `services/Diten.PvgService/bin` and `obj`
folders. They are generated build metadata with no tracked source and will collide with a real scaffold.

**Implementation work packs:** slice 1 work-pack details are pending support package material and are not normative
until committed. WP-01 (REG-PV-BASE ports) remains the intended gate on downstream build/test work. WP-08
(module manifest / catalog) remains **blocked** pending a governance decision.

> DCP-002 gate (2026-08-04): `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0230 --name "Case Intake & Triage"` -> `OK MOD-0230: proven against Blueprint/registry.`

## Module Summary

MOD-0230 Case Intake & Triage is the first PVG member module in DCP-004. Its purpose is to define the
regulated Pharmacovigilance intake baseline: Safety Case intake records, intake artifacts, triage state,
routing decisions, and the evidence-pack boundary required before downstream Case Processing, MedDRA Coding,
and Signal Management planning can proceed.

Blueprint context:

| Field | Value |
|---|---|
| Domain / Landscape | Regulated Life-Sciences Extensions |
| Suite / Platform | Pharmacovigilance (PV) |
| Capability Group | Safety Case Operations |
| Blueprint Wave | W-3 |
| Placement | Domain App (Regulated Workflow) |
| Delivery Outcome / Value | PV intake baseline; controlled routing |
| Soft Pages | PV Case Inbox; Intake Forms; Triage Rules; Evidence Pack |
| Minimum Integration Contract | REG-PV-BASE |
| SoR Primary Object | Safety Cases |
| AI Enablement Tier / Risk | Governed-AI / High |

## Ownership and Boundaries

In scope for this build/test-ready pack:

- Safety Case intake contract boundary.
- Intake artifact contract boundary.
- Triage state and routing decision contract boundary.
- Evidence-pack handoff boundary.
- W-3A0 dependency map and production blockers.
- Future module-pack readiness questions.

Out of scope for this build/test-ready pack:

- Operational runtime, production deployment, supplier qualification, validation, seed data, migration, background
  job, permission seed implementation, archive/void, export, delete, bulk-delete, and any surface outside slice 1.
- W-3A0 foundation remediation development.
- Full Case Processing, MedDRA Coding, Signal Management, reporting/submission, or PV quality runtime.
- AI summarization, extraction, recommendation, or routing implementation.

## Owned Objects

Planned logical objects, not runtime classes yet:

| Object | Ownership | Runtime status |
|---|---|---|
| Safety Case Intake | MOD-0230 SoR for intake-stage Safety Case creation boundary | Planned only |
| Intake Artifact | MOD-0230 SoR for intake-stage submitted artifacts and source references | Planned only |
| Triage State | MOD-0230 SoR for initial triage status and routing outcome | Planned only |
| Routing Decision | MOD-0230 SoR for controlled routing decision metadata | Planned only |
| Evidence Pack Boundary | MOD-0230 produces intake evidence-link requirements; MOD-0031 owns evidence-link service | Planned only |

Slice 1 runtime objects, endpoints, controllers, commands, queries, DTOs, frontend routes, and permissions may be
defined only inside the build/test gate recorded above. Operational runtime remains intentionally unauthorized.

## Entity Fields

Create/edit user-entered field count recorded for draft planning: `16`.

Golden Reference decision: `compact`, because the create/edit form has more than 8 user-entered fields and
regulated intake requires separate Create/Edit/Details review surfaces rather than Index-hosted offcanvas panels.

Excluded from field count: `Id`, `TenantId`, audit fields, correlation id, workflow instance id, created/updated
metadata, system-generated case number, and computed SLA/status timestamps.

| Field | Required | Sensitivity class | User-entered | Notes / blocker |
|---|---|---|---|---|
| IntakeChannel | Yes | public-metadata | Yes | Allowed values / source of options still requires approval. |
| SourceType | Yes | public-metadata | Yes | Allowed source taxonomy still requires approval. |
| SourceReference | No | confidential | Yes | External source ID policy must align with Blueprint MOD-0040 / TRACE-BUNDLE. |
| ReceivedAtUtc | Yes | regulated-safety | Yes | Must be UTC-normalized; client timestamp trust policy still requires approval. |
| ReporterType | Yes | public-metadata | Yes | Allowed values still require approval. |
| ReporterContactSummary | No | PII | Yes | Requires MOD-0019 masking / row-field policy before runtime. |
| PatientSubjectCode | No | PHI | Yes | Requires MOD-0019 masking / row-field policy before runtime. |
| EventOnsetDate | No | PHI | Yes | Requires MOD-0019 masking / row-field policy before runtime. |
| AdverseEventNarrative | Yes | PHI | Yes | Raw narrative prohibited in logs/traces/audit payloads. |
| SuspectProductText | No | regulated-safety | Yes | Terminology/reference policy still requires approval. |
| Seriousness | Yes | regulated-safety | Yes | Allowed values still require approval. |
| IntakePriority | Yes | regulated-safety | Yes | Priority taxonomy and SLA linkage still require approval. |
| TriageOutcome | Yes | regulated-safety | Yes | Allowed states and transition rules require Workflow/Inbox contract. |
| TriageReason | No | PHI | Yes | Raw reason text prohibited in logs/traces/audit payloads. |
| RouteTargetQueue | Yes | confidential | Yes | Requires Workflow/Inbox route target contract and permission-filtered visibility. |
| EvidenceLinkReferences | No | confidential | Yes | Requires MOD-0031 Evidence-Link contract; no fake evidence pack fallback. |

Every field included in create/edit/list/detail/export surfaces must receive masking behavior, row/field access
rules, audit payload rules, evidence-link rules, and fail-closed tests before slice 1 acceptance. Export remains
out of slice 1 and still requires a real MOD-0019 approval before it can be added.

## Repo Scope

Authorized as of 2026-08-09 (build/test gate):

- `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md`
- `services/Diten.PvgService/**` - dedicated service, port **5011** (OD-7)
- `frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/**` plus the matching
  `wwwroot/assets/js/Pharmacovigilance/CaseIntakeTriage/**` and
  `Resources/Views/Pharmacovigilance/CaseIntakeTriage/**`
- `gateway/Diten.ApiGateway/**/ocelot.json` - one route family, integration-agent-owned
- `tests/**` covering the above

Still blocked:

- Production deployment, supplier qualification, and validation approval (operational runtime gate).
- Archive, void, and export surfaces (out of slice 1).
- Seed data and permission seeding - MOD-0230 consumes permission keys; MOD-0018 / AuthService owns seed/grant.
- `.antigravity/**`, including `ports.md`. Assigning port 5011 to `Diten.PvgService` requires explicit
  approval before that protected file is edited.

## Protected Paths

- `.antigravity/**` (including `rules/ports.md` - port 5011 registration needs explicit approval)
- `services/**` **except `services/Diten.PvgService/**`**
- `frontend/**` **except `frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/**` and its JS / resource siblings**
- `gateway/**` except the single MOD-0230 route family in `ocelot.json`, via integration-agent
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- Other domain module packs and runtime internals unless explicitly authorized by the user.

This draft does not modify DCP-004, services, frontend, gateway, runtime code, or any other module-pack file.

## Dependencies

W-3A0 dependencies are production blockers, not waived:

| Dependency | Owning module / source | Status for MOD-0230 |
|---|---|---|
| REG-PV-BASE | DCP-004 minimum integration contract | BLOCKER |
| SSO + RBAC/ABAC | MOD-0018 RBAC / permissions plus Platform/Auth foundations | BLOCKER |
| PHI/PII masking hooks | MOD-0019 Data Masking & Row/Field Security | BLOCKER |
| AuditEvent v1 | MOD-0021 Audit Trail Service | BLOCKER |
| Workflow/Inbox v1 | MOD-0023 Workflow Designer | BLOCKER |
| Evidence-Link | MOD-0031 Evidence Linking Service | BLOCKER |
| TRACE-BUNDLE: canonical ID, Correlation-ID, trace stitching, regulated error model | Blueprint MOD-0040 / platform trace standards | BLOCKER |
| OTel / operational telemetry | Platform observability foundations | BLOCKER |
| DCP-004 | PVG Urgent W-3 Development Block | **`approved` 2026-08-09**; build/test gate open, operational runtime gate closed |

MOD-0004 Metric & Semantic Registry and MOD-0063 Data Warehouse / Lakehouse are not direct MOD-0230 runtime
blockers unless this module's approved scope emits signal analytics, semantic metric IDs, or data-product outputs.
They remain downstream DCP-004 / MOD-0234-facing gates for Signal Management. If MOD-0230 later adds signal
analytics or data-product output, the pack must be revised to promote the relevant MOD-0004/MOD-0063 contracts
from downstream gates to direct MOD-0230 blockers.

### Required Interface Contracts for Build/Test and Operational Runtime

MOD-0230 separates build/test gate substitution from operational runtime approval. The table below records which
contracts are closed, ported, or still blocking. Ported contracts satisfy local build/test only; they do not approve
operational runtime.

| Owner | Required contract for MOD-0230 | Required MOD-0230 decision | Status |
|---|---|---|---|
| MOD-0018 RBAC / permissions | canonical permission keys, seed/grant ownership, actor context, tenant authorization context, optional data-scope shape | actor roles and permission matrix for read/create/update/triage/route; archive/export out of slice 1; delete and bulk-delete permanently excluded | **CLOSED 2026-08-09** - consumed directly from merged `Diten.Platform.Common/Authorization` |
| MOD-0019 masking / row-field security | field sensitivity vocabulary, masking/omit/deny behavior, row-scope and field-scope evaluation, unavailable-policy behavior | per-field sensitivity matrix and fail-closed behavior for list/detail/create/update/audit; export out of slice 1 | **PORTED** - `IPvgFieldSecurityPolicy` + `DenyAllFieldSecurityPolicy`. Build gate satisfied; **operational runtime still BLOCKER**. MOD-0019 registry row exists for planning traceability only; it is still unowned, missing a module pack, and has no runtime (see Implementation Notes) |
| MOD-0021 AuditEvent v1 | append/event shape, safe metadata envelope, redaction rules, critical audit failure policy, correlation propagation | audited operations, payload allow-list, failure behavior when audit append/outbox is unavailable | **CLOSED 2026-08-09** - consumed directly from merged `Diten.Platform` audit feature |
| MOD-0023 Workflow/Inbox v1 | transition gate or inbox handoff API/event, assignment semantics, routing state, fail-closed behavior | triage states, routable states, route targets, transition reason codes, blocked/allowed behavior | **PORTED** - `IPvgWorkflowTransitionGate` + `DenyAllWorkflowTransitionGate`. Build gate satisfied; **operational runtime still BLOCKER**. Queue registry, assignment, and SLA remain MOD-0023's and are not ported |
| MOD-0031 Evidence-Link | object reference shape, link/query API, evidence requirement/completeness rule, evidence-pack boundary | whether artifacts require evidence links at create, triage, route, and downstream handoff | **PORTED** - `IPvgEvidenceLinkPort` + `DenyAllEvidenceLinkPort`. Build gate satisfied; **operational runtime still BLOCKER**. The non-production adapter may only record evidence as `Pending`, never as satisfied |
| Blueprint MOD-0040 / TRACE-BUNDLE | canonical/external ID semantics, `X-Correlation-Id`, trace stitching, regulated error model | generated/manual intake ID policy, external source ID policy, correlation propagation, error reason-code policy | **CLOSED 2026-08-09** - consumed from merged `Diten.Platform.Common/Observability` + `Tenancy` |

### MOD-0230 Owner-Approval Evidence Intake Template

This template records the evidence required to convert a governance-only packet or approval gate into an
owner-approved MOD-0230 input. Every approval requires owner/team, approver, approval date, evidence artifact/link,
approved version, fail-closed proof, required test evidence, and caveats/exclusions. Empty or placeholder values mean
the approval remains blocked. Recording this template does not approve operational runtime and does not authorize any
surface outside the MOD-0230 build/test gate.

| Approval | Owner/team | Approver | Approval date | Evidence artifact/link | Approved version | Fail-closed proof | Required test evidence | Caveats / exclusions | Readiness decision |
|---|---|---|---|---|---|---|---|---|---|
| `PVG-MOD0230-RBAC-Contract v1` | MOD-0018 / AuthService / Platform access governance | NY (ny@gmgroup.ch), against merged MOD-0018 runtime | 2026-08-09 | `services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/` - `IEntitlementChecker`, `ITenantAuthorizationContext`, `IDataScopeResolver`, `EntitlementCheckResult`, `EntitlementDenyReason`, `IEntitlementAuditSink`, `RequiresFeatureAttribute`, `RequiresModuleAttribute`; tests under `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/` | MOD-0018 FU10a + FU10b + FU12 merged runtime | Missing - required proof: deny on missing actor, tenant, permission, scope, seed/grant catalog, or auth context; cross-tenant reads return 404/empty and mutations/exports deny. | Missing - required tests: role/action allow-deny matrix, missing-permission denial, cross-tenant denial, platform/partner/tenant actor behavior, seed/grant ownership proof, and no delete/bulk-delete surface. | **Approved for the build/test gate.** Permission keys `pvg.case-intake-triage.{read,create,update,triage,route}` are consumed, not seeded, by MOD-0230; seed/grant ownership stays with MOD-0018 / AuthService. `archive` and `export` keys are out of slice 1. Delete and bulk-delete keys are permanently excluded. | [x] Owner-approved for MOD-0230 `ready-for-dev` consumption |
| `PVG-MOD0230-FieldSecurity-Contract v1` | MOD-0019 masking / row-field security owner | Not approved - no registered owner contract | Open | Build-gate substitute: `IPvgFieldSecurityPolicy` with `DenyAllFieldSecurityPolicy`; detailed port contract material is pending support package content and is not normative until committed | n/a - MOD-0019 contract does not exist | Missing - required proof: deny or omit/mask when field policy is missing or unavailable; raw PHI/PII/free text cannot enter UI/API output, logs, traces, metrics, audit metadata, validation errors, or exports. | Missing - required tests: all 16 fields across list/detail/create/update/export/audit, missing-policy denial, raw-value leak scans, and cross-tenant checks. | **Not owner-approved. Satisfied for the build/test gate only** by a deny-by-default port: every behaviour this row requires is a denial behaviour, and `DenyAllFieldSecurityPolicy` denies unconditionally. This row continues to **block operational runtime** until MOD-0019 ships and its owner signs. The port stores no policy data, hosts no engine, and persists nothing. | [ ] Owner-approved for MOD-0230 `ready-for-dev` consumption - **build gate satisfied by fail-closed port; operational runtime still blocked** |
| `PVG-MOD0230-AuditEvent-v1` | MOD-0021 AuditEvent / audit owner | NY (ny@gmgroup.ch), against merged MOD-0021 runtime | 2026-08-09 | `services/Diten.Platform/src/Diten.Platform.Domain/Entities/Audit/AuditEvent.cs`, `Repositories/IAuditEventRepository.cs`, `Application/Contracts/Audit/AuditBehaviorOptions.cs`, audit outbox worker, `RedactAuditActorHandler`, `AuditExportSerializer`; tests under `.../Application.Tests/Audit/` | MOD-0021 `ready-for-dev / implemented evidence` | Missing - required proof: no unaudited regulated mutation succeeds; audit outage blocks the mutation or uses only an owner-approved durable outbox path; payload redaction happens before persistence/export. | Missing - required tests: create, update, triage, route, archive, export, denial/failure audit events, outbox outage behavior, redaction, and correlation propagation. | **Approved for the build/test gate.** Audited operations for slice 1 are create, update, triage, route, and denial/failure. Archive and export audit events are out of slice 1. Payload allow-list excludes every PHI/PII field per the sensitivity matrix. | [x] Owner-approved for MOD-0230 `ready-for-dev` consumption |
| `PVG-MOD0230-WorkflowTransitionGate-v1` | MOD-0023 Workflow/Inbox owner | Not approved - no registered owner contract | Open | Build-gate substitute: `IPvgWorkflowTransitionGate` with `DenyAllWorkflowTransitionGate`; detailed port contract material is pending support package content and is not normative until committed | n/a - MOD-0023 contract does not exist | Missing - required proof: gate runs before commit; blocked, unavailable, missing queue, missing assignment policy, missing reason code, tenant/object mismatch, or unapproved `NotApplicable` prevents lifecycle mutation. | Missing - required tests: gate-before-commit, allowed/blocked/not-applicable, outage, missing queue/assignment policy, cross-tenant denial, reason-code validation, correlation propagation, and no-PHI workflow event/log/error checks. | **Not owner-approved. Satisfied for the build/test gate only** by a deny-by-default port: every behaviour this row requires is a denial behaviour, and `DenyAllWorkflowTransitionGate` denies unconditionally. This row continues to **block operational runtime** until MOD-0023 ships and its owner signs. The port stores no policy data, hosts no engine, and persists nothing. | [ ] Owner-approved for MOD-0230 `ready-for-dev` consumption - **build gate satisfied by fail-closed port; operational runtime still blocked** |
| `PVG-MOD0230-EvidenceLink-v1` | MOD-0031 Evidence-Link owner | Not approved - no registered owner contract | Open | Build-gate substitute: `IPvgEvidenceLinkPort` with `DenyAllEvidenceLinkPort`; detailed port contract material is pending support package content and is not normative until committed | n/a - MOD-0031 contract does not exist | Missing - required proof: missing required evidence or unavailable Evidence-Link blocks triage, route, archive/void, or handoff unless MOD-0031 owner approves a durable pending-evidence state; no fake pack or duplicated content. | Missing - required tests: link/query shape, completeness, outage, cross-tenant denial, link/unlink audit, correlation propagation, workflow handoff blocked on missing evidence, no duplicated document content, and no-PHI evidence-content checks. | **Not owner-approved. Satisfied for the build/test gate only** by a deny-by-default port: every behaviour this row requires is a denial behaviour, and `DenyAllEvidenceLinkPort` denies unconditionally. This row continues to **block operational runtime** until MOD-0031 ships and its owner signs. The port stores no policy data, hosts no engine, and persists nothing. | [ ] Owner-approved for MOD-0230 `ready-for-dev` consumption - **build gate satisfied by fail-closed port; operational runtime still blocked** |
| `PVG-MOD0230-TraceBundle-v1` | Enterprise Architecture / platform trace authority for Blueprint MOD-0040 / TRACE-BUNDLE | NY (ny@gmgroup.ch), as Enterprise Architect | 2026-08-09 | `services/Diten.Platform.Common/src/Diten.Platform.Common/Observability/` - `ICorrelationContext`, `CorrelationContext`, `CorrelationIdMiddleware`; `Tenancy/ITenantContext`, `TenantResolutionMiddleware`; `Persistence/BaseEntity` server-generated `Id` | Blueprint MOD-0040 Canonical ID & Correlation Standard, v1 | Missing - required proof: no untraceable regulated mutation succeeds; external IDs are non-authoritative; duplicate or mismatch ambiguity rejects, conflicts safely, or routes only through owner-approved durable duplicate review/outbox. | Missing - required tests: server-generated canonical IDs, client-supplied ID rejection, external-ref non-authority, duplicate/mismatch handling, missing/valid/invalid `X-Correlation-Id`, and trace propagation through intake, audit, workflow, evidence, error, and outbox/events. | **Approved for the build/test gate.** Canonical IDs are server-generated; client-supplied `Id` and `TenantId` are rejected. `SourceReference` is explicitly non-authoritative. Duplicate handling returns 409 with no silent overwrite. Note: the repo registry row for `MOD-0040` is a deprecated alias to MOD-0288 and is **not** the authority here - the Blueprint MOD-0040 standard is. | [x] Owner-approved for MOD-0230 `ready-for-dev` consumption |
| `PVG-MOD0230-ObservabilityErrorModel-v1` | MOD-0041 / Ops / platform observability and regulated error-model owner | NY (ny@gmgroup.ch), against merged MOD-0041 runtime | 2026-08-09 | `services/Diten.Platform.Common/src/Diten.Platform.Common/Observability/` - `SensitiveDataRedactor`, `SensitiveDataLogEventEnricher`, `ObservabilityOptions`, `ObservabilityServiceCollectionExtensions`, `HealthCheckResponseWriter` | MOD-0041 `approved` | Missing - required proof: raw PHI/PII/free text never enters logs, traces, metrics, validation errors, or error payloads; missing approved telemetry/error policy blocks regulated mutation or uses an explicitly approved degraded path. | Missing - required tests: trace/log/error redaction, correlation propagation across UI/API/service/audit/workflow/evidence/outbox, invalid/missing correlation behavior, safe metric labels, and telemetry outage behavior. | **Approved for the build/test gate.** Reason codes are taxonomy values only; raw exception text, field values, narratives, patient codes, and reporter details are excluded from logs, traces, metrics, validation errors, and error payloads. Leak-scan tests are mandatory before slice 1 is accepted. | [x] Owner-approved for MOD-0230 `ready-for-dev` consumption |
| `PVG-MOD0230-RetentionLegalHoldArchiveVoid-v1` | Compliance / legal-hold / records-retention owner, with MOD-0019, MOD-0021, trace, workflow, and evidence owner alignment where applicable | Not approved - no compliance / legal-hold owner assigned | Missing | Missing | Missing | Missing - required proof: archive/void remains unavailable before approval; legal hold blocks archive and void; missing retention, legal-hold, masking, audit, trace, workflow, or evidence policy denies or blocks with a regulated safe error and no fallback mutation. | Missing - required tests: archive/void blocked before approval, blocked under legal hold, denied for unauthorized actors, denied or masked when MOD-0019 is unavailable, blocked or queued only if MOD-0021 approves durable audit behavior, required metadata captured on allowed paths, evidence/trace references preserved, and hard delete/bulk delete absent. | **Not owner-approved. Removed from slice 1 scope instead:** archive and void surfaces are not implemented in slice 1, so this approval is not on the critical path. It becomes required the moment archive/void is added. No market-specific PV retention period is accepted. | [ ] Owner-approved for MOD-0230 `ready-for-dev` consumption |
| MOD-0230 operational runtime authorization | User / PVG system owner / Enterprise Architecture, with platform operations and validation approval where required | Missing | Missing | Missing | Missing | Missing - required proof: approved runtime scope, service boundary, port/topology, appsettings policy, tenant isolation, no client `TenantId`, safe telemetry/errors/audit metadata, no delete/bulk-delete, archive/void absent or approved, and all exposed-surface contracts fail closed. | Missing - required tests: startup/config fail-closed checks, no port/appsettings/Gateway/frontend/collection/seed/job without approval, tenant isolation, no-PHI telemetry/errors, RBAC/masking/audit/workflow/evidence/trace outage behavior, and phase-gate evidence for every authorized surface. | **NOT AUTHORIZED.** Build/test gate only: local, dev, and CI. Not operational runtime, not production use, not supplier qualification, not validation approval. Requires real MOD-0019, MOD-0023, MOD-0031 and a named retention / legal-hold owner. | [ ] Operational runtime authorized - **remains closed as of 2026-08-09** |

### External Owner-Evidence Submission Checklist

External reviewers must answer against the exact MOD-0230 approval artifact named below. A design basis, draft
packet, generic platform capability, or informal note can support review, but it cannot approve the MOD-0230
contract unless the owner explicitly approves the named artifact/version and supplies fail-closed and test evidence.

| Required approval artifact | Who must approve | Evidence reviewers must supply | What cannot count as approval | Exact condition to mark approved |
|---|---|---|---|---|
| `PVG-MOD0230-RBAC-Contract v1` | MOD-0018 / AuthService / Platform access governance | Approver, approval date, artifact/link, approved version, RBAC deny proof, role/action allow-deny tests, cross-tenant denial, platform/partner/tenant actor behavior, seed/grant ownership proof, no delete/bulk-delete proof | Packet recorded only, draft matrix, informal email, unversioned notes, partial permission list | All evidence fields are supplied and owner explicitly approves MOD-0230 `ready-for-dev` consumption |
| `PVG-MOD0230-FieldSecurity-Contract v1` | MOD-0019 masking / row-field security owner | Approver, approval date, artifact/link, version, all 16 field rules, missing-policy fail-closed proof, raw PHI/PII/free-text leak tests, list/detail/create/update/export/audit tests | Sensitivity vocabulary alone, draft field matrix, generic masking standard, untested policy | Owner-approved artifact proves allow/mask/omit/deny behavior and tests pass for every required surface |
| `PVG-MOD0230-AuditEvent-v1` | MOD-0021 AuditEvent / audit owner | Approver, approval date, artifact/link, version, append/event shape, outage fail-closed/outbox proof, redaction proof, create/update/triage/route/archive/export/denial tests | Generic audit capability, unapproved event names, audit notes without failure-mode proof | Owner approves the PVG event contract and required audit/outage/redaction tests are supplied |
| `PVG-MOD0230-WorkflowTransitionGate-v1` | MOD-0023 Workflow/Inbox owner | Approver, approval date, artifact/link, version, gate-before-commit proof, queue/assignment/reason-code behavior, outage and blocked transition tests, cross-tenant denial, no-PHI workflow event/log/error proof | Draft workflow states, queue names without owner approval, untested `NotApplicable` behavior | Owner approves the transition gate contract and all blocked/unavailable/missing-policy paths fail closed |
| `PVG-MOD0230-EvidenceLink-v1` | MOD-0031 Evidence-Link owner | Approver, approval date, artifact/link, version, link/query shape, completeness rules, outage behavior, cross-tenant denial, link/unlink audit, no duplicated content, no-PHI evidence-content tests | Fake evidence pack, duplicated documents, live URL references as authority, generic evidence-link notes | Owner approves the EvidenceLink contract and evidence completeness/outage behavior is test-proven |
| `PVG-MOD0230-TraceBundle-v1` | Enterprise Architecture / platform trace authority for Blueprint MOD-0040 / TRACE-BUNDLE | Approver, approval date, artifact/link, version, canonical ID policy, external ID non-authority proof, duplicate/mismatch handling, correlation header behavior, trace propagation tests | External IDs as primary keys, unversioned trace notes, generic correlation convention only | Owner approves trace identity/correlation behavior and tests prove no untraceable regulated mutation |
| `PVG-MOD0230-ObservabilityErrorModel-v1` | MOD-0041 / Ops / platform observability and regulated error-model owner | Approver, approval date, artifact/link, version, safe reason-code taxonomy, no-PHI telemetry/error policy, redaction tests, correlation propagation, safe metric labels, telemetry outage behavior | Raw exception logging, generic OTel policy, untested error envelope, PHI/PII-bearing logs/traces | Owner approves error/telemetry model and tests prove no PHI/PII/free-text leakage |
| `PVG-MOD0230-RetentionLegalHoldArchiveVoid-v1` | Compliance / legal-hold / records-retention owner, aligned with MOD-0019, MOD-0021, trace, workflow, evidence owners as applicable | Approver, approval date, artifact/link, version, retention/legal-hold/archive/void rules, archive/void blocked-before-approval proof, legal-hold block proof, no hard delete/bulk-delete proof | Market-generic retention assumption, draft retention matrix, unapproved archive/void wording, missing legal-hold proof | Owner approves class-specific policy and tests prove archive/void/delete paths fail closed as required |
| MOD-0230 operational runtime authorization | User / PVG system owner / Enterprise Architecture, with platform operations and validation approval where required | Approver, approval date, artifact/link, version, approved runtime scope, service boundary, port/topology, appsettings policy, tenant isolation, safe telemetry/errors/audit metadata, no delete/bulk-delete, archive/void status, all exposed-surface fail-closed tests | Local scaffold approval, docs-only planning approval, supplier paper, draft service boundary, untested startup/config behavior | Explicit operational runtime authorization is granted and the row can be marked approved |

### MOD-0040 / MOD-0288 Identity Clarification

- Use **Blueprint MOD-0040 / TRACE-BUNDLE** for canonical ID, external ID, correlation header, trace stitching, and
  regulated error-model decisions.
- Use **MOD-0288 Organization, Person & Position Directory** only if MOD-0230 routing, assignment, or search
  explicitly consumes organization/person/position references.
- Do **not** reference legacy deprecated repo `MOD-0040` as the organization/person source. In this repo,
  organization/person/position ownership is canonicalized to MOD-0288.

## Runtime Constraints

- Runtime service scaffold **authorized** for `Diten.PvgService` on port **5011** (OD-7, 2026-08-09).
- Gateway route **authorized** for one MOD-0230 route family, integration-agent-owned.
- Database collection, index, and migration **authorized** for the MOD-0230 intake collection. **Seed data and
  background jobs remain unauthorized.**
- Tenant UI (Golden Reference Compact) **authorized** under the MOD-0230 view root.
- Everything above is authorized for **local / dev / CI only**. Production deployment, supplier qualification,
  and validation approval remain unauthorized.
- All three REG-PV-BASE consumption ports must be registered deny-by-default. A non-production adapter must throw
  at startup when `ASPNETCORE_ENVIRONMENT=Production`. Detailed conformance material remains a pending support
  package and is not normative until committed.
- Archive, void, export, delete, and bulk-delete surfaces must not exist in slice 1.
- The service boundary is a dedicated `Diten.PvgService` with a hybrid partner-aware integration
  posture. The service is expected to own the Diten-controlled intake contract, tenant workflow boundary, audit /
  evidence / workflow integration, and partner adapter boundary if a PV safety partner system is selected.
- `service` is resolved to `Diten.PvgService` and `service_port` to `5011` (verified free; 5056-5060 are taken).
  The stale ignored `services/Diten.PvgService/bin` and `obj` folders must be deleted before scaffolding.
- `entity_base: EntityBase` remains correct for Diten-owned tenant records, projections, archive/void metadata, and
  outbox/audit-linked records under the future PVG boundary. Partner-native records remain outside the repo entity
  model unless an adapter contract later maps them.
- Future runtime must resolve tenant isolation, regulated data masking, audit, evidence links, workflow/inbox handoff, OTel, correlation ID, and error model before acceptance.
- Tenant-owned runtime data, if approved, must carry server-resolved `TenantId`; client payloads must not accept
  `TenantId`. Cross-tenant reads or mutations must return 404/empty result with no metadata leak.
- Any sensitive field whose MOD-0019 policy cannot be evaluated must fail closed: deny the field, omit/mask the
  field, or deny the operation according to the field matrix recorded for draft planning. No implementation may invent a permissive
  fallback.
- Raw PHI/PII, reporter identifiers, patient identifiers, source document content, free-text safety narratives,
  and unrestricted search/export payloads must not be written to logs, traces, metrics, or audit payloads.
- Delete policy is locked for this draft: no hard delete, no bulk delete, and no normal user delete. Archive/void
  may be introduced only after retention/legal-hold approval; it requires reason, actor, UTC timestamp,
  correlation id, and AuditEvent. Archive/void is blocked under legal hold.

## Layout & Shell Contract

`shell: tenant`

MOD-0230 is a tenant/domain operational workflow surface, not a platform-admin configuration module.

- Razor layout: every future `.cshtml` page must explicitly set `Layout = "_LayoutTenantShell";`.
- MVC route: `/Pharmacovigilance/CaseIntakeTriage`.
- View root: `frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/**`.
- Frontend API profile: same-origin MVC proxy profile. Browser JavaScript must call the MVC proxy surface, not call
  Gateway directly and never call a service port directly.

Frontend implementation is authorized for the build/test gate as of 2026-08-09. It remains unauthorized for
production until the operational runtime gate opens.

## Backend File Convention

`service: Diten.PvgService` (port 5011)

Boundary resolved by OD-7: dedicated `Diten.PvgService` with a hybrid partner-aware integration posture.

- Do not host MOD-0230 inside `Diten.Platform`, `Diten.AuthService`, `Diten.DevEnablementService`, or
  `Diten.EnterpriseStrategyService`.
- If a buy/partner PV safety system is selected, `Diten.PvgService` should act as the controlled wrapper /
  orchestration layer for Diten tenant UI, intake contract, audit, evidence, workflow, correlation, and adapter
  semantics. It must not become a direct frontend-to-partner bridge.
- Internal build scope is limited to the Diten-controlled contract, tenant UI boundary, workflow/audit/evidence
  integration, and adapter layer after approval; it must not become a full standalone PV safety platform under
  MOD-0230.

If a PVG runtime service is later approved, backend implementation must follow the Golden Reference CQRS shape:

```text
Features/CaseIntakeTriage/
├── Commands/
├── Queries/
├── Handlers/CommandHandlers/
├── Handlers/QueryHandlers/
├── Validators/
└── CaseIntakeTriageModels.cs
```

Naming rules for future implementation:

- Slice 1 commands: `CreateCaseIntakeTriageCommand`, `UpdateCaseIntakeTriageCommand`, `TriageCaseIntakeTriageCommand`, `RouteCaseIntakeTriageCommand`.
- Slice 1 queries: `GetCaseIntakeTriageListQuery`, `GetCaseIntakeTriageByIdQuery`.
- **Out of slice 1:** `ArchiveCaseIntakeTriageCommand`, `VoidCaseIntakeTriageCommand`, and any export command.
- Handlers: `*Handler` only; no `CommandHandler`, `QueryHandler`, or `RequestHandler` suffix.
- Validators: `*Validator` only; no `CommandValidator` suffix.
- Permanently forbidden: `DeleteCaseIntakeTriageCommand`, `BulkDeleteCaseIntakeTriageCommand`, DELETE endpoints, and bulk-delete endpoints.

## Frontend File Contract

`golden_reference: compact`

The create/edit field count recorded for draft planning is 16, so MOD-0230 follows Golden Reference Compact:

- `Index.cshtml`.
- `Create.cshtml`.
- `Edit.cshtml`.
- `Details.cshtml`.
- `_Form.cshtml`.
- `_Filter.cshtml`.
- `_DataTable.cshtml`.
- `_IndexL10n.cshtml`.
- `CaseIntakeTriageIndex.cs`.
- `wwwroot/assets/js/Pharmacovigilance/CaseIntakeTriage/index.js`.
- `wwwroot/assets/js/Pharmacovigilance/CaseIntakeTriage/index.l10n.js`.
- `Resources/Views/Pharmacovigilance/CaseIntakeTriage/CaseIntakeTriageIndex.{lang}.resx`.

Compact must not include Slim-only `_CreateEditOffcanvas.cshtml` or `_DetailsQuickView.cshtml`.

Future frontend API calls must use the same-origin MVC proxy profile. Direct browser-to-Gateway calls are not the
preferred profile for this regulated tenant surface; direct service-port calls are forbidden.

## Validation Rules

Resolved 2026-08-09 for slice 1. All values below are owner decisions and are binding on implementation.
`Pre-check` is server-side validation before persistence. `DB-level` records the Mongo index/constraint decision.

**Route is intentionally unsatisfiable in slice 1.** `IPvgWorkflowTransitionGate.ResolveRouteTargetAsync` denies
unconditionally because the queue registry belongs to MOD-0023 and does not exist. The `Route` command, endpoint,
permission key, audit event, and fail-closed test are all built in slice 1 and become functional the day a real
MOD-0023 client replaces the deny adapter. Any implementation that makes Route succeed by inventing a queue list
must be rejected in review.

| Field | Required | Rule | DB-level | Pre-check | Sensitivity / fail-closed requirement |
|---|---|---|---|---|---|
| IntakeChannel | Yes | Controlled option set; no free text | Indexed `(TenantId, IntakeChannel)` | Value must exist in the PVG lookup; unknown value rejected 400 | unknown channel fails closed for routing decisions |
| SourceType | Yes | Controlled option set; no free text | Indexed `(TenantId, SourceType)` | Value must exist in the PVG lookup; unknown value rejected 400 | unknown source fails closed for routing decisions |
| SourceReference | No | Max 128 chars; trimmed; non-authoritative external ID | Non-unique index `(TenantId, SourceType, SourceReference)` | Duplicate `(SourceType, SourceReference)` in the same tenant returns 409 with a duplicate-candidate reason code; never a silent overwrite | confidential; never authoritative, never a primary key, never raw in logs/audit |
| ReceivedAtUtc | Yes | UTC instant; rejected if later than server time + 5 min skew or earlier than 1900-01-01 | Indexed `(TenantId, ReceivedAtUtc desc)` | Server re-normalizes to UTC; a client-supplied offset is converted, never trusted as-is | regulated-safety; server-side UTC normalization is mandatory |
| ReporterType | Yes | Controlled option set | Indexed `(TenantId, ReporterType)` | Value must exist in the PVG lookup | public-metadata |
| ReporterContactSummary | No | Max 256 chars; summary only - no full address, no national ID, no account number | Not indexed | Minimum-necessary check: rejected if it exceeds 256 chars | PII; deny/mask/omit when field policy is unavailable |
| PatientSubjectCode | No | Max 64 chars; pseudonymous code only; no whitespace; must not contain `@` or more than 2 consecutive digits groups resembling a national identifier | Not indexed | Rejected if it fails the pseudonymity pattern | PHI; never returned or audited raw without an approved policy |
| EventOnsetDate | No | Date-only; not in the future; not before 1900-01-01; must be on or before `ReceivedAtUtc` date | Not indexed | Cross-field check against `ReceivedAtUtc` | PHI; mask/omit by actor policy |
| AdverseEventNarrative | Yes | Max 8000 chars; required non-empty after trim | Not indexed; **excluded from any text index** | Length and non-empty check | PHI; raw free text prohibited in logs/traces/metrics/audit payloads and error responses |
| SuspectProductText | No | Max 512 chars | Not indexed | Length check only until a terminology contract exists | regulated-safety; omit/deny if unapproved |
| Seriousness | Yes | Controlled option set | Indexed `(TenantId, Seriousness)` | Value must exist in the PVG lookup | regulated-safety |
| IntakePriority | Yes | Controlled option set; SLA linkage deferred to MOD-0023 | Indexed `(TenantId, IntakePriority)` | Value must exist in the PVG lookup; no SLA is computed in slice 1 | regulated-safety |
| TriageOutcome | Yes at Triage, absent at Create | Controlled option set: `Triaged`, `Rejected`, `Duplicate` | Indexed `(TenantId, TriageOutcome)` | Transition must return `Allowed` from `IPvgWorkflowTransitionGate` **before** commit | invalid or unapproved transition fails closed |
| TriageReason | Required when `TriageOutcome` is `Rejected` or `Duplicate`, otherwise optional | Max 1000 chars; a taxonomy reason code is mandatory, free text is supplementary only | Not indexed | Reason code must exist in the taxonomy; free text alone is rejected | PHI; raw reason text prohibited in logs/traces/metrics/audit payloads |
| RouteTargetQueue | Yes at Route | Value must be resolved by `IPvgWorkflowTransitionGate.ResolveRouteTargetAsync` | Not indexed | **Denies unconditionally in slice 1** - the queue registry is MOD-0023's and does not exist | confidential; visibility must be permission-filtered; no hardcoded queue list is permitted |
| EvidenceLinkReferences | No | Max 20 references; each is an object reference only, never document content | Not indexed | `IPvgEvidenceLinkPort` records requirements as `Pending`; completeness never returns `Allowed` in slice 1 | confidential; no fake evidence pack when the evidence service is unavailable |

### Option-Set Ownership

Option-set ownership recorded for draft planning for future validation and UI contracts:

| Option set | Owner / source | Ownership type |
|---|---|---|
| IntakeChannel | PVG-controlled lookup | Static controlled PVG lookup |
| SourceType | PVG-controlled lookup, partner/source-system extensible | Static controlled PVG lookup with controlled extensions |
| ReporterType | PVG-controlled lookup | Static controlled PVG lookup |
| Seriousness | PVG/compliance-controlled lookup | Static controlled PVG lookup |
| IntakePriority | PVG lookup plus workflow/SLA policy owner | Workflow/reference-owned |
| TriageOutcome | MOD-0023 Workflow/Inbox transition contract plus PVG state model | Workflow-owned |
| RouteTargetQueue | MOD-0023 Workflow/Inbox queue registry, possibly backed by reference service | Workflow/reference-owned |

Static controlled PVG lookups may be planned as PVG-owned reference data only after the approved reference-data
publication path is selected. Workflow/reference-owned option sets must fail closed when the owning contract is
unavailable; no hardcoded UI fallback is allowed.

Every field included in the final create/edit/list/detail/export surface must have at least one matching test
expectation proving unauthorized, cross-tenant, missing-policy, and masking-denied behavior.

## Failure Path to Verify

Future implementation must verify at least these paths:

- **Missing required intake field**
  - Expected: 400 validation response; no intake record created.
- **Duplicate or conflicting intake identifier**
  - Expected: 409 or approved duplicate-handling response; no silent overwrite.
- **Unauthorized actor**
  - Expected: 401/403 according to policy; no data leakage.
- **Cross-tenant access**
  - Expected: 404 or approved forbidden behavior; no cross-tenant data returned.
- **Unmasked sensitive field**
  - Expected: request/response blocked or masked according to MOD-0019 contract.
- **Missing MOD-0019 policy for a sensitive field**
  - Expected: field omitted/masked or operation denied according to the field matrix; no permissive fallback.
- **Unauthorized field-level read**
  - Expected: non-sensitive envelope may return only if approved; restricted field is masked/omitted or request is denied.
- **Sensitive input appears in audit/log/trace**
  - Expected: test fails; raw PHI/PII/free text must not be persisted to audit payloads, logs, traces, metrics, or error details.
- **Evidence-link unavailable**
  - Expected: fail-closed or explicitly degraded behavior; no fake evidence pack.
- **Workflow/Inbox unavailable**
  - Expected: triage/routing transition blocked; no untraceable routing.
- **Audit sink unavailable**
  - Expected: regulated mutation blocked or queued according to approved audit contract; no unaudited mutation.
- **Correlation/trace context missing**
  - Expected: behavior follows Blueprint MOD-0040 / TRACE-BUNDLE decision; runtime must not create untraceable regulated state changes.
- **Delete/archive attempted before retention/legal-hold decision**
  - Expected: operation absent or denied; no regulated intake record is removed or hidden without approved retention/legal-hold policy.

## Authorization Convention

Permission prefix proposal for future tenant/domain implementation:

```text
pvg.case-intake-triage.read
pvg.case-intake-triage.create
pvg.case-intake-triage.update
pvg.case-intake-triage.triage
pvg.case-intake-triage.route
pvg.case-intake-triage.archive
pvg.case-intake-triage.export
```

Explicitly excluded permission keys:

```text
pvg.case-intake-triage.delete
pvg.case-intake-triage.bulk-delete
```

Initial role / permission matrix proposal:

| Role | read | create | update | triage | route | archive | export |
|---|---:|---:|---:|---:|---:|---:|---:|
| PVG Intake Agent | Assigned / own queue | Yes | Pre-triage only | No | No | No | No |
| PVG Triage Lead | Yes | Yes | Yes | Yes | Yes | Only after retention/legal-hold approval | No |
| PVG Safety Manager | Yes | Yes | Yes | Yes | Yes | Only after retention/legal-hold approval | Masked export only |
| PVG Compliance Auditor | Read-only | No | No | No | No | No | Masked export only |
| PVG System Integration | Approved intake contract only | Yes | No | No | No | No | No |

Open authorization decisions:

- Final actor role names, actor type mapping, and seed/grant ownership require MOD-0018 / AuthService approval.
- Archive permission is unusable until retention/legal-hold policy is approved.
- PHI/PII field-level authorization must align with MOD-0019 before runtime.
- Permission seed/grant ownership must remain with MOD-0018 / AuthService; MOD-0230 may only define/consume keys
  after the owning security contract is approved.
- Export is masked-only unless a later field policy approval explicitly permits more.

No permission seed is authorized by this draft.

## Gateway / API Routing Decision

Decision: one Gateway route family is **authorized for the build/test gate** as of 2026-08-09,
integration-agent-owned.

**Correction 2026-08-09 (NET-001).** The earlier draft proposed `/api/v1/pharmacovigilance/case-intake-triage`
as the **upstream** template. NET-001 requires upstream `/api/{resource}` with the `v1` prefix on the
**downstream** template only, and every existing route in `ocelot.json` follows that form. The corrected
mapping is:

| | Template |
|---|---|
| Upstream (Gateway, port 5000) | `/api/pv-case-intake-triage` and `/api/pv-case-intake-triage/{everything}` |
| Downstream (`Diten.PvgService`, port 5011) | `/api/v1/pv-case-intake-triage` and `/api/v1/pv-case-intake-triage/{everything}` |

Slice 1 sub-resources under that family:

```text
/api/pv-case-intake-triage
/api/pv-case-intake-triage/{id}
/api/pv-case-intake-triage/{id}/triage
/api/pv-case-intake-triage/{id}/route
```

Out of slice 1 - must not be routed:

```text
/api/pv-case-intake-triage/{id}/archive
/api/pv-case-intake-triage/export
```

Permanently excluded:

```text
DELETE /api/pv-case-intake-triage/{id}
/api/pv-case-intake-triage/bulk-delete
```

Future route implementation must define:

- service/deployment owner;
- upstream API base path;
- downstream path;
- auth/correlation/error-model behavior;
- OPTIONS/CORS handling if applicable;
- integration-agent task for `gateway/Diten.ApiGateway/**/ocelot.json`.

Frontend consumption must use a same-origin MVC proxy profile for this tenant UI. Direct browser-to-Gateway calls
are not the preferred profile for MOD-0230; direct service-port calls from frontend remain forbidden.

## Acceptance Criteria

Acceptance criteria for the original planning pack:

- [x] Pack exists at `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md`.
- [x] Status started as `draft`; later promoted to `ready-for-dev` for the build/test gate only.
- [x] Canonical name is exactly `Case Intake & Triage`.
- [x] DCP-002 preflight passed for MOD-0230.
- [x] W-3A0 dependencies are recorded as production blockers, not waived.
- [x] No operational runtime implementation is authorized.
- [x] Form field count recorded for draft planning as `16`.
- [x] Golden Reference resolved for draft planning as `compact`.
- [x] Shell resolved for draft planning as `tenant`.
- [x] Entity base recorded for draft planning as `EntityBase` for a future dedicated PVG service boundary.
- [x] Delete and bulk-delete are explicitly excluded; archive/void is blocked until retention/legal-hold approval.
- [x] Service boundary recorded as dedicated `Diten.PvgService`; frontmatter `service` is resolved for the
      build/test gate only.
- [x] Build/buy/partner strategy proposed and recorded as hybrid, partner-aware internal control wrapper.
- [x] Tenant MVC route, view root, same-origin MVC proxy profile, and Gateway route proposals are recorded.
- [x] Option-set ownership is proposed and recorded for the draft field model.

Acceptance criteria for the **build / test gate** (all closed 2026-08-09):

- [x] DCP-004 is `approved` / `ready-for-execution`.
- [x] This module pack is `approved` / `ready-for-dev`.
- [x] `service` resolved to `Diten.PvgService`, `service_port` to `5011` (OD-7).
- [x] W-3A0-Lite: three consumption ports specified with deny-by-default adapters and a conformance suite.
- [x] Interface contracts closed for MOD-0018, MOD-0021, and Blueprint MOD-0040 / TRACE-BUNDLE against merged runtime.
- [x] Interface contracts for MOD-0019, MOD-0023, and MOD-0031 satisfied for the build gate by fail-closed ports.
- [x] Retention / legal-hold removed from the critical path by dropping archive/void from slice 1.
- [x] Build/buy/partner integration boundary decided as hybrid, partner-aware internal control wrapper.
- [x] Gateway route corrected to NET-001 upstream/downstream form.

Acceptance criteria for the **operational runtime gate** (all open - do not check without owner evidence):

- [ ] MOD-0019 Data Masking & Row/Field Security ships and its owner signs `PVG-MOD0230-FieldSecurity-Contract v1`.
- [ ] MOD-0023 Workflow/Inbox v1 ships and its owner signs `PVG-MOD0230-WorkflowTransitionGate-v1`.
- [ ] MOD-0031 Evidence Linking Service ships and its owner signs `PVG-MOD0230-EvidenceLink-v1`.
- [ ] A retention / legal-hold owner is named and signs `PVG-MOD0230-RetentionLegalHoldArchiveVoid-v1` before archive/void is added.
- [ ] All three non-production port adapters are removed from the environment.
- [ ] MOD-0230 operational runtime authorization is granted.

## Test Expectations

Slice 1 test expectations - **required before slice 1 is accepted**:

- DCP-002 identity proof remains valid.
- Backend build and unit/integration tests for the approved PVG service boundary.
- Tenant isolation and regulated-data masking tests.
- Per-field PHI/PII sensitivity, masking, row/field deny, and missing-policy fail-closed tests.
- Audit, correlation/TRACE-BUNDLE, evidence-link, workflow/inbox failure-path tests.
- Tests proving raw PHI/PII/free text is absent from logs, traces, metrics, audit payloads, validation errors, and
  regulated error responses.
- REG-PV-BASE port conformance suite C-01 through C-17, pending support package detail not normative until
  committed, including the assertion that the host **throws** when a non-production adapter is configured in a
  Production environment.
- Gate-before-commit: no state-changing handler commits without an `Allowed` transition result in the same correlation scope.
- Frontend build and DataTable verifier (Compact).
- Gateway route smoke after the integration-agent route lands.
- Static scan proving no production appsettings file contains `Pvg:RegPvBase:UseNonProductionAdapters`.

## Ready-for-dev Checklist

- [x] Required governance files read.
- [x] DCP-002 preflight passed.
- [x] Pack status is `ready-for-dev` (2026-08-09) - **build/test gate only**.
- [x] DCP-004 promoted to `approved` / `ready-for-execution` (2026-08-09).
- [x] W-3A0 scope resolved per OD-2: W-3A0-Lite gates build/test; W-3A0-Full gates operational runtime.
- [x] MOD-0018 RBAC/permission contract and actor matrix resolved against merged runtime.
- [~] MOD-0019 masking / row-field security - **ported**, not resolved. Build gate satisfied by `DenyAllFieldSecurityPolicy`; operational runtime blocked.
- [x] MOD-0021 AuditEvent v1 append/redaction/failure contract resolved against merged runtime.
- [~] MOD-0023 Workflow/Inbox v1 - **ported**, not resolved. Build gate satisfied by `DenyAllWorkflowTransitionGate`; operational runtime blocked.
- [~] MOD-0031 Evidence-Link - **ported**, not resolved. Build gate satisfied by `DenyAllEvidenceLinkPort`; operational runtime blocked.
- [x] Blueprint MOD-0040 / TRACE-BUNDLE canonical ID, correlation, trace-stitching, and error-model contract resolved against merged runtime.
- [x] `service` resolved to `Diten.PvgService`, port 5011.
- [x] `shell` resolved as `tenant`.
- [x] `entity_base` recorded as `EntityBase` for the dedicated PVG service boundary.
- [x] Create/edit user-entered fields defined.
- [x] Create/edit fields marked required/optional.
- [x] PHI/PII/sensitive-field matrix recorded for draft planning for every intake field.
- [x] `form_field_count` resolved for draft planning as `16`.
- [x] `golden_reference` resolved for draft planning as `compact`.
- [x] Entity fields and validation rules fully specified (2026-08-09) - max lengths, cross-field rules, index decisions, and fail-closed behaviour are binding in the Validation Rules table.
- [x] Option-set ownership recorded for intake, source, reporter, seriousness, priority, triage outcome, and route queue.
- [x] Authorization actor/role matrix approved for slice 1 (read/create/update/triage/route). Archive and export rows are out of slice 1.
- [n/a] Retention, archive/void activation, and legal-hold policy - **removed from slice 1 scope**; required before archive/void is ever added.
- [x] Build/buy/partner integration boundary proposed and recorded as hybrid, partner-aware internal control wrapper.
- [x] Gateway route family defined per NET-001 and assigned to integration-agent (Day 9).
- [x] Test expectations are concrete: slice 1 list above plus port conformance suite C-01 to C-17.

## Implementation Notes

- **2026-08-09 promotion.** DCP-004 is `approved`; this pack is `ready-for-dev` for the **build/test gate only**.
  The operational runtime gate remains closed. Do not read `ready-for-dev` as production authorization.
- **W-3A0-Lite ports.** MOD-0019, MOD-0023, and MOD-0031 do not exist as runtime. MOD-0230 consumes them through
  three PVG-owned ports with deny-by-default adapters. A port is an interface plus a deny default and nothing
  else - it stores no policy data, hosts no workflow engine, and persists no evidence. If a port starts making
  regulated decisions on its own authority it has become an unauthorized reimplementation and must be rejected in
  review. Detailed port contract material remains a pending support package and is not normative until committed.
- **Registry defect - MOD-0019.** As of 2026-08-09, `MOD-0019 Data Masking & Row/Field Security` is
  Blueprint-canonical (W-3, Build, `SEC-DATA-BUNDLE`) but had **no row** in
  `execution/registries/module-id-registry.md`. The DCP-002 identity gate passed only because the Blueprint
  workbook carries it. A row has been added, but MOD-0019 still has no owner and no module pack, which is why
  `PVG-MOD0230-FieldSecurity-Contract v1` could never have been signed. Raised with `platform-shared-services`.
- **Registry defect - MOD-0040.** The repo registry row for `MOD-0040` is a **deprecated alias to MOD-0288**
  (Organization, Person & Position Directory). The Blueprint MOD-0040 is `Canonical ID & Correlation Standard`.
  This pack means the Blueprint one. Do not resolve `MOD-0040` through the registry row.
- **Route correction.** The earlier `/api/v1/pharmacovigilance/case-intake-triage` **upstream** proposal violated
  NET-001. Corrected to upstream `/api/pv-case-intake-triage`, downstream `/api/v1/pv-case-intake-triage`.
- **Port assignment.** `Diten.PvgService` = 5011, verified free. `.antigravity/rules/ports.md` documents only up
  to 5058 while 5059 (MDM) and 5060 (HCM) are live; that file is a protected path and needs explicit approval
  before the 5011 registration is written into it.
- **Stale folder.** `services/Diten.PvgService/` currently exists as ignored `bin` / `obj` output with no tracked
  source. Delete it before scaffolding.
- Frontmatter decisions reconciled 2026-08-09: `shell: tenant`, `entity_base: EntityBase`,
  `form_field_count: 16`, `golden_reference: compact`, `service: Diten.PvgService`, and `service_port: 5011`
  for the build/test gate only.
- Service boundary reconciled 2026-08-09: boundary is dedicated `Diten.PvgService` with a hybrid
  partner-aware integration posture for local / dev / CI build-test preparation. Operational runtime remains closed.
- Route/API profile reconciled 2026-08-09: tenant MVC route is `/Pharmacovigilance/CaseIntakeTriage`,
  view root is `frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/**`, frontend profile is same-origin
  MVC proxy, and Gateway family is `/api/pv-case-intake-triage` upstream to `/api/v1/pv-case-intake-triage`
  downstream.
- Option-set ownership reconciled 2026-08-04: static PVG controlled lookups cover IntakeChannel, SourceType,
  ReporterType, and Seriousness; IntakePriority, TriageOutcome, and RouteTargetQueue require workflow/reference
  ownership contracts as recorded in Validation Rules.
- Delete policy reconciled 2026-08-04: no hard delete, no bulk delete, no normal user delete. Archive/void only
  after retention/legal-hold approval, with reason, actor, UTC timestamp, correlation id, and AuditEvent; blocked
  under legal hold.
- MOD-0230 is Blueprint W-3. MOD-0231/MOD-0232/MOD-0234 urgent W-3 slice handling is governed by DCP-004 and does not change this pack's canonical identity.
- REG-PV-BASE is the minimum integration contract for this module: SSO+RBAC/ABAC, PHI/PII masking hooks, AuditEvent v1, Workflow/Inbox v1, Evidence-Link, OTel, Correlation-ID, and Error Model.
- Blueprint MOD-0040 / TRACE-BUNDLE is the intended reference for canonical/external IDs, correlation header, trace
  stitching, and regulated error-model decisions. Repo legacy MOD-0040 must not be used as an organization/person
  source; use MOD-0288 only for organization/person/position references if routing requires them.
- MOD-0004 and MOD-0063 remain downstream DCP-004 / MOD-0234-facing gates unless MOD-0230 explicitly emits
  signal analytics, semantic metric IDs, or data-product outputs in a later approved scope.
- Governed-AI / High-risk Blueprint markers are recorded as blockers for AI behavior, not implementation permission.

## Follow-up Items

Blocking the operational runtime gate:

- Real MOD-0019, MOD-0023, and MOD-0031 modules, each replacing one deny-by-default adapter and closing one evidence row.
- A named retention / legal-hold owner, required before archive or void is ever added.
- Removal of all three non-production adapters and the `Pvg:RegPvBase:UseNonProductionAdapters` switch.

Governance follow-ups:

- Raise MOD-0019 ownership with `platform-shared-services`; hand them the 16-field sensitivity matrix in this pack as the first concrete consumer requirement.
- Hand the triage state set and route-target requirement to the MOD-0023 owner.
- Hand the evidence-completeness requirement to the MOD-0031 owner.
- Obtain approval to register port 5011 in the protected `.antigravity/rules/ports.md`, and correct the 5059 / 5060 drift while doing so.
- Finalize seed/grant ownership of the `pvg.case-intake-triage.*` permission keys with MOD-0018 / AuthService.
- Add MOD-0230 rows to `execution/portfolio/master-development-plan.md` and the platform delivery board.

Out of slice 1, requiring their own approval:

- Archive / void surfaces, export surfaces, background jobs, seed data.
- Any AI-assisted intake, extraction, summarization, or routing behaviour.
