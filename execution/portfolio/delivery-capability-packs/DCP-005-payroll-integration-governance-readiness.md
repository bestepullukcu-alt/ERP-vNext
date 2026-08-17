---
id: DCP-005
slug: payroll-integration-governance-readiness
name: Payroll Integration & Governance Readiness
type: Delivery Capability Pack
standard: CAP-001
status: approved
owner_domain: platform-shared-services
owner: enterprise-architect / platform-team / integration-ops
branch: feature/governance-hris-source-readiness
created: 2026-07-13
canonical_module_id: MOD-0281
canonical_module_name: "Payroll Integration & Governance"
canonical_source: "docs/System Capability & Implementation Blueprint - master 5.xlsx#Blueprint_Data row 282"
---

# DCP-005 - Payroll Integration & Governance Readiness

> **Artifact type:** This is a Delivery Capability Pack (CAP-001 governance /
> orchestration contract). It is not a runtime entity, not a module pack, not a
> service scaffold, not a route change, and not approval to implement production
> code.

> **Canonical-name guard:** The canonical Blueprint identity is
> `MOD-0281 - Payroll Integration & Governance`. The name must not be replaced by
> `Payroll Governance & Payroll Controls` unless a future Enterprise Architect
> supersession updates DCP-002, the verifier, registry, and repository Blueprint
> authority.

> **Premature-coding guard:** Production implementation remains blocked until
> this Delivery Capability Pack is approved / ready-for-execution and the
> required member module packs are approved or ready-for-dev in the ordered
> delivery sequence below.

## 1. Identity and status

| Field | Value |
|---|---|
| ID | `DCP-005` |
| Slug | `payroll-integration-governance-readiness` |
| Name | Payroll Integration & Governance Readiness |
| Type | Delivery Capability Pack |
| Status | `draft` |
| Canonical member | `MOD-0281 - Payroll Integration & Governance` |
| Release / lane | W-4 / payroll integration readiness |
| Owner domain | `platform-shared-services` |
| Runtime owner candidate | `Diten.Platform` as Platform Shared Service, pending module-pack approval |
| Standard | CAP-001 - `.antigravity/rules/capability-pack-standard.md` |

Canonical ID preflight:

```text
OK  MOD-0281: proven against Blueprint/registry.
```

Registry state at authoring: `MOD-0281` is proven by Blueprint and has no
observed registry collision, but no direct row exists in
`execution/registries/module-id-registry.md`.

## 2. Business outcome

Establish a governed, payroll-sensitive integration assurance path before any
runtime MOD-0281 work begins. The outcome is a clean separation where:

- MOD-0281 governs payroll integration runs, mappings, exceptions,
  reconciliation state, retry/replay governance, and evidence-grade audit export;
- MOD-0279 remains the external payroll system-of-record boundary;
- MOD-0280 remains the external time-and-attendance provider boundary;
- platform services remain owners of gateway, event bus, data contracts,
  integration monitoring, audit, workflow, records, and secrets capabilities;
- payroll data, employee identifiers, provider credentials, and audit exports are
  handled as highly sensitive data.

## 3. Problem statement

Blueprint MOD-0281 is a W-4 Platform Shared Service under Integration &
Interoperability / Integration Assurance. It depends on multiple platform and
external-provider capabilities: API Gateway, Event Bus, Data Contract Registry,
Integration Monitoring & Reconciliation, Audit Trail, Workflow, Records
Management, MOD-0279 external payroll SoR, and MOD-0280 external time and
attendance provider.

A single module pack would overload MOD-0281 with cross-cutting readiness
decisions and could accidentally duplicate upstream platform capabilities.
Therefore this Delivery Capability Pack coordinates prerequisites, ownership
boundaries, and sequencing before a MOD-0281 module pack or runtime work starts.

## 4. Capability boundary

### In scope for this Delivery Capability Pack

- MOD-0281 identity and canonical-source confirmation.
- Cross-capability dependency readiness analysis.
- Delivery order for predecessor and substrate capabilities.
- Payroll integration governance boundary.
- Provider connector configuration metadata boundary.
- Data-contract mapping governance boundary.
- Payroll integration run monitoring boundary.
- Reconciliation mismatch and exception-routing boundary.
- Retry/replay governance and idempotency expectations.
- Evidence-grade audit export orchestration expectations.
- Security, tenant-isolation, payroll-data handling, and audit gates.

### Candidate MOD-0281 ownership

MOD-0281 may own, after module-pack approval:

- payroll provider integration configuration metadata;
- payroll data-contract mapping governance records;
- payroll integration run status projections;
- reconciliation status and mismatch references;
- integration exception assignment and resolution metadata;
- retry/replay governance records;
- audit export request metadata;
- contract-version compatibility decision records;
- integration health and operational visibility records.

### Explicit exclusions

MOD-0281 does not own:

- payroll calculation;
- gross/net pay computation;
- earnings or deductions master data;
- payslip master records;
- employee, job, organization, position, or employment master data;
- time entry, attendance, leave, roster, or leave-balance records;
- workflow engine implementation;
- shared audit event store;
- shared records-management / retention / legal-hold engine;
- shared event bus;
- shared API gateway;
- shared data-contract registry;
- provider credential vault;
- external payroll provider SoR data.

## 5. Member modules and follow-ups

| Member | Role in this DCP | Current state |
|---|---|---|
| `MOD-0281` | Payroll integration governance and operational control facade | Blueprint-proven; registry row missing; module pack missing |
| `MOD-0251` | HRIS source readiness for employee/person identifiers | Draft DCP-004 exists; registry row and module pack missing |
| `MOD-0279` | Payroll Engine external SoR boundary | Draft module pack exists; not approved |
| `MOD-0280` | Time & Attendance external provider boundary | Draft module pack exists; not approved |
| `MOD-0032` | API Gateway | Registry `review / partial`; module pack exists |
| `MOD-0035` | Event Bus / Message Queue | Registry `partial`; module pack exists |
| `MOD-0003` | Data Contract Registry | Registry `planned / missing`; no module pack found |
| `MOD-0037` | Integration Monitoring & Reconciliation | Registry `review / planned`; deferred pack exists |
| `MOD-0021` | Audit Trail Service | Registry `ready-for-dev / implemented evidence` |
| `MOD-0023` | Workflow Designer / approvals | Registry `review / planned`; module pack exists |
| `MOD-0030` | Records Management / Retention / Legal Hold | Blueprint dependency; no clear repository module-pack evidence found |

This DCP does not allocate child IDs or follow-up IDs.

## 6. Ownership map

| Concern | Authoritative owner | MOD-0281 posture |
|---|---|---|
| Payroll external SoR | MOD-0279 / external payroll provider | References approved payroll results and pay-cycle events only |
| Time and attendance external provider | MOD-0280 / UKG/Kronos-style provider | References approved time/attendance outputs only |
| Employee/person identity | MOD-0251 / MOD-0288 contracts as approved | Stores identifiers and mapping references, not employee masters |
| API gateway mechanics | MOD-0032 | Consumes approved routes only; no Ocelot ownership |
| Event bus mechanics | MOD-0035 | Consumes/publishes approved events only; no broker ownership |
| Data contract registry | MOD-0003 | Registers/uses contract versions; no registry ownership |
| Integration monitoring | MOD-0037 | Links run/reconciliation evidence; no monitoring platform ownership |
| Audit events | MOD-0021 | Emits bounded audit events; no parallel audit store |
| Workflow approvals | MOD-0023 | References workflow instances/tasks; no workflow engine ownership |
| Records/retention/legal hold | MOD-0030 or approved records capability | Links export/evidence metadata; no records engine ownership |
| Provider credentials | MOD-0012 or approved secret mechanism | Stores secret references only |

## 7. Dependency graph

```text
MOD-0251 HRIS source readiness
   +--> employee/person identifier readiness

MOD-0279 Payroll Engine external SoR
MOD-0280 Time & Attendance external provider
   +--> payroll/time source contracts

MOD-0032 API Gateway
MOD-0035 Event Bus
MOD-0003 Data Contract Registry
MOD-0037 Integration Monitoring & Reconciliation
MOD-0021 Audit Trail
MOD-0023 Workflow Designer
MOD-0030 Records Management / Retention / Legal Hold
   +--> platform substrate readiness

All of the above
   +--> MOD-0281 Payroll Integration & Governance module pack
       +--> approved runtime implementation
```

## 8. Ordered delivery sequence

### Stage 0 - Governance and identity

1. Keep `docs/System Capability & Implementation Blueprint - master 5.xlsx` as
   the active MOD-0281 identity authority.
2. Keep this DCP as `draft` until human review.
3. Decide whether and when to add a registry row for MOD-0281.
4. Confirm that MOD-0281 remains an integration facade, not payroll calculation.

### Stage 1 - Predecessor readiness

1. Approve or revise DCP-004 for MOD-0251.
2. Prepare and approve MOD-0251 module pack if HRIS source readiness proceeds.
3. Review and approve MOD-0279 external payroll SoR pack.
4. Review and approve MOD-0280 external time-and-attendance provider pack.

### Stage 2 - Platform substrate readiness

1. Confirm API Gateway route ownership and integration-agent workflow.
2. Confirm Event Bus duplicate-delivery, inbox/outbox, and schema-version
   behavior.
3. Create or approve Data Contract Registry readiness for payroll contracts.
4. Confirm Integration Monitoring & Reconciliation handoff.
5. Confirm Audit Trail append/export evidence behavior.
6. Confirm Workflow approval/reference integration.
7. Resolve Records Management / Retention / Legal Hold ownership.

### Stage 3 - MOD-0281 module pack

1. Prepare a MOD-0281 `status: draft` module pack.
2. Define owned objects, referenced objects, surfaces, permissions, routes,
   contracts, and test gates.
3. Keep UI shell, form counts, and Golden Reference decisions surface-specific.
4. Promote only after explicit approval.

### Stage 4 - Runtime implementation

Runtime starts only after this DCP is approved / ready-for-execution and the
MOD-0281 module pack is approved / ready-for-dev.

## 9. Prerequisites

- MOD-0281 DCP-002 identity gate remains passing.
- MOD-0251, MOD-0279, and MOD-0280 predecessor planning is approved or explicitly
  waived by Enterprise Architect.
- MOD-0003, MOD-0037, MOD-0023, and MOD-0030 readiness gaps are resolved or
  explicitly waived.
- Payroll-data sensitivity, audit export purpose, retention, masking, and
  authorization policies are approved.
- Provider credentials are handled only through approved secret references.
- Gateway, event, and contract ownership is assigned outside MOD-0281.

## 10. Architecture decisions

| ID | Decision | Status |
|---|---|---|
| AD-1 | MOD-0281 is a Platform Shared Service integration assurance facade. | Proposed, Blueprint-backed |
| AD-2 | MOD-0281 does not calculate payroll or own payroll SoR data. | Locked by boundary |
| AD-3 | MOD-0279 remains the external payroll SoR boundary. | Locked by current Blueprint |
| AD-4 | MOD-0280 remains the external time-and-attendance provider boundary. | Locked by current Blueprint |
| AD-5 | Provider-specific credential values are never persisted in MOD-0281. | Locked by security rules |
| AD-6 | Data contract versions are validated fail-closed. | Proposed |
| AD-7 | Payroll run and event processing must be idempotent and retry-safe. | Proposed |
| AD-8 | Audit export requires separate high-privilege authorization and purpose metadata. | Proposed |
| AD-9 | Reconciliation reruns must be safe and auditable. | Proposed |
| AD-10 | A single module pack is insufficient until cross-capability readiness is settled. | Locked by CAP-001 classification |

## 11. Scope

This DCP coordinates:

- MOD-0281 canonical identity and planning route;
- predecessor readiness for MOD-0251, MOD-0279, and MOD-0280;
- platform substrate readiness for gateway, events, contracts, monitoring,
  audit, workflow, and records;
- payroll-data security posture;
- future MOD-0281 module-pack prerequisites;
- production implementation blockers.

## 12. Explicit exclusions

- production code;
- branch creation;
- service/domain scaffold;
- controllers, handlers, repositories, Razor views, JavaScript, CSS, RESX;
- gateway routes;
- provider adapters;
- provider credentials;
- registry edits;
- changes to MOD-0251, MOD-0279, or MOD-0280 artefacts;
- changes to `.antigravity/**`;
- approval or ready-for-dev promotion.

## 13. Governance drift risks

- Treating MOD-0281 as payroll calculation owner.
- Absorbing MOD-0279 external payroll SoR responsibilities into MOD-0281.
- Absorbing MOD-0280 time/attendance provider records into MOD-0281.
- Creating a local workflow, audit, records, event, gateway, or data-contract
  implementation inside MOD-0281.
- Logging payroll amounts, employee PII, tax identifiers, bank data, provider
  payloads, or credentials.
- Advancing implementation while predecessor packs remain draft.
- Using a stale non-repository Blueprint to rename MOD-0281 without a DCP-002
  supersession.

## 14. Review questions

1. Should MOD-0281 receive a registry row before module-pack authoring?
2. Is PSS the final owner, or is a payroll/integration operations domain needed?
3. What is the approved MOD-0030 records/retention/legal-hold implementation
   owner?
4. What interim path is allowed if MOD-0003 Data Contract Registry remains
   missing?
5. What interim path is allowed if MOD-0037 remains deferred?
6. Which MOD-0281 UI surfaces are in the first module pack?
7. Which surfaces are read-only dashboards versus editable DataTable surfaces?
8. Which permissions are required for run visibility, retry, reconciliation,
   exception resolution, and audit export?
9. What payroll fields are mask-only, exportable, or forbidden?
10. What is the approved audit export purpose and retention policy?

## 15. Gate criteria

- DCP-002 identity preflight passes for MOD-0281.
- This DCP is approved or ready-for-execution.
- MOD-0251 predecessor readiness is approved or explicitly waived.
- MOD-0279 module pack is approved or explicitly waived for MOD-0281 planning.
- MOD-0280 module pack is approved or explicitly waived for MOD-0281 planning.
- MOD-0281 module pack exists and is approved or ready-for-dev.
- Data Contract Registry readiness is resolved.
- Integration Monitoring & Reconciliation readiness is resolved.
- Records Management / Retention / Legal Hold readiness is resolved.
- Security, authorization, idempotency, concurrency, audit, and export test gates
  are accepted.

## 16. Acceptance criteria

1. MOD-0281 identity verification uses the exact Blueprint canonical name.
2. No production implementation starts from this draft DCP.
3. MOD-0281 remains an integration assurance facade.
4. Payroll calculation and payroll SoR data stay outside MOD-0281.
5. Time entry, attendance, leave, roster, and leave balances stay outside
   MOD-0281.
6. Missing platform dependencies are recorded as blockers or approved waivers,
   not reimplemented locally.
7. Future module pack defines every UI surface separately, including editable
   field count and Golden Reference decision where applicable.
8. Future API endpoints require server-side tenant resolution and permission
   checks.
9. Cross-tenant access returns `404`.
10. Payroll amounts, tax identifiers, bank data, provider credentials, and
    unnecessary employee PII do not appear in logs or browser payloads.
11. Duplicate events, duplicate runs, retries, and replay commands are
    idempotent.
12. Reconciliation reruns are safe and auditable.
13. Concurrent exception resolution is controlled by optimistic concurrency or an
    approved equivalent.
14. Unsupported contract version fails closed.
15. Audit export requires explicit permission, purpose, tenant, actor, time
    range, and correlation metadata.

## 17. Downstream business-module impacts

- Payroll consumers must use approved MOD-0279/MOD-0281 contracts and cannot
  read provider payloads directly.
- Time and attendance consumers must use approved MOD-0280 contracts and cannot
  treat MOD-0281 as a source of attendance truth.
- HR/person consumers must use approved MOD-0251/MOD-0288 contracts and cannot
  create employee masters through MOD-0281.
- Platform operations must use MOD-0037/MOD-0021/MOD-0035 handoffs rather than
  local MOD-0281 clones of monitoring, audit, and eventing.

## 18. Open decisions

- Registry row timing for MOD-0281.
- MOD-0030 records/retention/legal-hold ownership and readiness.
- MOD-0003 interim contract governance if the registry remains missing.
- MOD-0037 interim monitoring/reconciliation handoff if the module remains
  deferred.
- MOD-0281 first-slice UI surface list.
- Permission namespace and sensitive-action separation.
- Payroll-data masking and export policy.
- Predecessor waiver policy, if any.

## 19. Future follow-ups

- Prepare or approve MOD-0251 module pack.
- Review and approve MOD-0279 draft pack.
- Review and approve MOD-0280 draft pack.
- Add a MOD-0281 registry row if approved by the registry owner.
- Prepare MOD-0281 module pack after this DCP is approved or explicitly accepted
  as planning basis.
- Prepare MOD-0003 and MOD-0037 readiness work if still missing.
- Resolve MOD-0030 records/retention/legal-hold capability evidence.

## 20. Audit and reconciliation notes

Authoring evidence:

- `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0281 --name "Payroll Integration & Governance"` returned OK.
- Blueprint `Blueprint_Data` row 282 places MOD-0281 in Platform & Shared
  Services / Integration & Interoperability / Integration Assurance.
- Blueprint `Dependencies_Normalized` marks MOD-0032, MOD-0035, MOD-0003,
  MOD-0037, MOD-0021, MOD-0023, MOD-0030, MOD-0279, and MOD-0280 as HARD
  dependencies.
- Blueprint `Module Pages` lists Payroll Provider Connector Setup, Payroll
  Contract Mapping, Payroll Run Monitor, Payroll Exception Inbox, and Payroll
  Audit Export as NEW soft pages.
- Current repository evidence has no direct MOD-0281 registry row and no
  MOD-0281 module pack.
- MOD-0279 and MOD-0280 draft packs exist in the worktree and were used only as
  read-only references.
- DCP-004 exists as draft and records MOD-0251 readiness gaps.

No production code, branch, gateway route, frontend file, service scaffold,
configuration, localization file, commit, push, or PR was created by this DCP.

## Appendix A - Hard dependency matrix

| Dependency | Repository status | Pack/DCP evidence | Runtime evidence | Contract/test readiness | MOD-0281 impact | Implementation blocker |
|---|---|---|---|---|---|---|
| MOD-0032 API Gateway | `review / partial` | Pack exists | Gateway project exists | Route work remains integration-agent owned | Required for all approved HTTP/UI/webhook paths | Yes for runtime routes |
| MOD-0035 Event Bus / Message Queue | `partial` | Pack exists | In-memory/MassTransit/outbox design evidence in pack | Live broker proof remains a readiness concern | Required for event-driven runs and duplicate delivery handling | Yes for event consumers |
| MOD-0003 Data Contract Registry | `planned / missing` | No pack found | No runtime evidence confirmed | Not ready | Required for Payroll Result, Run Status Event, Canonical ID/Correlation contracts | Yes |
| MOD-0037 Integration Monitoring & Reconciliation | `review / planned` | Deferred pack exists | Not implemented per pack | Not ready | Required for run health, reconciliation, exception visibility | Yes |
| MOD-0021 Audit Trail | `ready-for-dev / implemented evidence` | Pack and planning evidence exist | Implemented evidence noted in registry | Partial readiness, export policy still needed | Required for bounded audit events and export evidence | Partial |
| MOD-0023 Workflow Designer | `review / planned` | Pack exists | No production-ready evidence confirmed | Not ready | Required for approval/escalation references | Yes for workflow-backed actions |
| MOD-0030 Records Management / Retention / Legal Hold | Not found in registry excerpt | No module pack found | No runtime evidence confirmed | Not ready | Required for legal-hold/retention tags on evidence exports | Yes |
| MOD-0279 Payroll Engine external SoR | Blueprint-proven; no registry row | Draft module pack exists | No runtime approved | Not ready | Required payroll source boundary | Yes |
| MOD-0280 Time & Attendance external provider | Blueprint-proven; no registry row | Draft module pack exists | No runtime approved | Not ready | Required time/attendance source boundary | Yes |
