---
id: DCP-007
slug: hris-source-readiness
name: HRIS Source Readiness
type: Delivery Capability Pack
status: approved
owner: enterprise-architect / platform-team / proposed-hr-workforce-domain
canonical_module_id: MOD-0251
canonical_module_name: "HRIS (Workday/SuccessFactors/Oracle HCM) [External SoR]"
branch: feature/governance/hris-source-readiness
created: 2026-07-21
---

# DCP-007 - HRIS Source Readiness

## 1. Identity and status
**Status**: `draft`
**Canonical Identity**: `MOD-0251` - `HRIS (Workday/SuccessFactors/Oracle HCM) [External SoR]` (Friendly name: HRIS External SoR)
This artifact coordinates governance, ownership, sequencing, and approval requirements for MOD-0251.

## 2. Business outcome
Establish a standardized, auditable HRIS integration boundary to allow internal ERP/HCM modules to consume Employee, Org, and Job data without the platform owning the external HR master data or provider-specific calculations.

## 3. Problem statement
Currently, there is no formally governed boundary between external HRIS platforms (Workday, SuccessFactors, Oracle HCM) and the internal platform directory (MOD-0288). To prevent tight coupling, data duplication, and security risks, a clear capability boundary and provider-neutral contract must be established before implementation.

## 4. Capability boundary
MOD-0251 acts as the external HRIS source boundary. It abstracts provider-specific connections, maps external formats to neutral internal contracts, and governs the ingestion flow (checkpoints, mode).

## 5. Member modules and follow-ups
- `MOD-0251` - HRIS (Workday/SuccessFactors/Oracle HCM) [External SoR] (Primary member)
- `MOD-0288` - Organization, Person & Position Directory (Downstream consumer)
- Future provider-specific adapter work (pending ID allocation).

## 6. Ownership map
- **Platform/shared services**: Generic integration substrate (registry, scheduler, event bus, monitoring).
- **Proposed HR/workforce domain**: HR-specific source configuration semantics, mapping, policies.
- **MOD-0251**: Provider-neutral external HRIS source definition, identity mapping, sync mode.
- **MOD-0288**: Internal person/org/position directory.

## 7. Dependency graph
| ID | Canonical Name | Status | Domain | Role | Mandatory? | Blocker? | Fallback/Deferral |
|---|---|---|---|---|---|---|---|
| `MOD-0002` | Interface Registry | planned / missing | PSS | Data contract governance | Yes | Yes | Defer until MOD-0002 ready |
| `MOD-0012` | Secrets / Configuration Vault | unknown | PSS | Credential abstraction | Yes | Yes | EA decision required |
| `MOD-0021` | General Audit Trail | ready-for-dev | PSS | Traceability | Yes | No | Fallback to basic logging |
| `MOD-0026` | Background Job Scheduler | unknown | PSS | Polling sync trigger | No | No | Webhook based sync |
| `MOD-0035` | Event Bus / Message Queue | unknown | PSS | Outbox propagation | Yes | Yes | Wait for readiness |
| `MOD-0037` | Integration Monitoring | unknown | PSS | Assurance | Yes | No | File-based fallback |
| `MOD-0279` | Payroll Engine [External SoR] | approved | PSS | Separate external SoR | No | No | - |
| `MOD-0281` | Payroll Integration & Governance | unknown | PSS | Payroll governance | No | No | - |
| `MOD-0288` | Org, Person & Position Directory | approved/ready | PSS | Internal directory | Yes | Yes | - |

## 8. Ordered delivery sequence
1. **Stage 0 - Governance**: Canonical registry mapping, HR ownership decision, service decision, dependency checks.
2. **Stage 1 - MOD-0251 Module Pack**: Prepare draft pack, define contracts and test expectations.
3. **Stage 2 - Generic contract integration**: Integrate with interface registry, secrets, scheduler, audit, event bus.
4. **Stage 3 - Provider-neutral source runtime**: Registration, validation abstraction, checkpoint model.
5. **Stage 4 - Consumer integration**: MOD-0288 consumption, conflict handling, sync testing.
6. **Stage 5 - Provider adapters**: Separately approved provider adapters.

## 9. Prerequisites
- `MOD-0251` canonical registry row validation and creation (EA approved).
- Domain proposal decision.
- Service proposal decision.

## 10. Architecture decisions
- **Split Ownership Model**: Generic infrastructure remains in PSS. HR semantics belong to proposed HR/workforce domain. MOD-0251 governs the boundary.
- **Provider-Neutrality**: Must preserve vendor neutrality through an abstraction layer for contract, mapping, auth, sync, and ingestion. No provider is selected in this DCP.
- **Service Proposal**: 
  1. New HR/workforce service
  2. Isolated HR module in platform service
  3. Dedicated integration service
  *Recommendation (pending EA approval)*: New HR/workforce service for scaling and future module fit.
- **Domain Proposal**: Propose new "HR/workforce domain" for HR business capability aggregation. Requires EA approval.

## 11. Scope
- Provider-neutral contract definition.
- Upstream sync policies, checkpoints, mappings.
- Integration abstraction layer.

## 12. Explicit exclusions
- Full employee lifecycle (recruitment, onboarding, leave, benefits, performance, time/attendance).
- Payroll functionality (delegated to MOD-0279 / MOD-0281).
- Native internal person/org directory (MOD-0288).
- Generic infrastructure (scheduler, event bus, secret storage).

## 13. Governance drift risks
- Duplication of Person/Org entities across MOD-0251 and MOD-0288.
- Provider-specific logic leaking into neutral contracts.

## 14. Review questions
- Does the Enterprise Architect approve the new HR/workforce domain?
- Should the registry be updated with MOD-0251 prior to the module pack approval?
- Which service proposal is accepted?

## 15. Gate criteria
- Enterprise Architect domain decision.
- Enterprise Architect service decision.
- Canonical registry mapping finalization.

## 16. Acceptance criteria
- Canonical identity `MOD-0251` verified with no registry collision.
- Ownership model (split ownership) documented.
- Domain and Service decisions isolated as approval gates.
- MOD-0251 vs MOD-0288 boundary clearly documented.
- Provider neutrality requirements documented.
- Dependency readiness recorded.
- Security and compliance requirements (secret management, least privilege, tenant isolation) documented.
- Sequence and protected paths documented.
- No production code created, no provider selected, no unapproved IDs created.
- Next module-pack step defined.

## 17. Downstream business-module impacts
- MOD-0288 must correctly consume Employee, Org, and Job identifiers from MOD-0251 contracts without mutating the SoR.

## 18. Open decisions
- Final domain allocation.
- Final service mapping.
- Formal registry insertion of MOD-0251.

## 19. Future follow-ups
- Provider-specific adapters (Workday, SuccessFactors, Oracle HCM) requiring separate follow-up packs and IDs.

## 20. Audit and reconciliation notes
- (To be updated after implementation phases)

---
### Security and Compliance
- Tenant isolation (TenantId server-side enforcement).
- Cross-tenant requests return 404.
- Least privilege access and strict PII classification for HR data.
- Secret references only (no raw credentials in MongoDB or logs).
- SSRF protection, endpoint allow-listing, TLS enforcement.
- Webhook signature verification and replay prevention.
- Data residency and payload redaction for auditing.
