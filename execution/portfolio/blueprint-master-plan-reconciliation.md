# Blueprint-Master Plan Reconciliation

## Purpose
This document acts as the reconciliation tracking file between the high-level business capabilities (Enterprise Blueprint) and the technical module development plan. It ensures mapping alignment and guards against missing dependencies, Source of Record (SoR) collisions, page coverage issues, and release scope drift.

## Scope
Maintains mapping records, coverage status, and reconciliation governance rules between `execution/portfolio/enterprise-blueprint.md` and `execution/portfolio/master-development-plan.md`.

## Not the source for
- Writing code or module packages (use Module Packs).
- Individual module specifications or schemas (use docs/modules/ or Module Packs).

## Current status
Active. The Blueprint↔Repo canonicalization ledger and the unresolved EA reservation ledger (below) are established per **DCP-002 — Module Identity Canonicalization**. Blueprint (`docs/System Capability & Implementation Blueprint - master 8.1.xlsx`, `Blueprint_Data`) is the canonical MOD-ID + name authority; Master 7 is historical predecessor evidence only. Canonical IDs not yet allocated by the Enterprise Architect remain **unresolved** (no placeholder IDs are assigned).

## Blueprint ↔ Repo Canonicalization Ledger (DCP-002, applied)

Documentation-only canonical alignment already applied to the working tree. Old IDs are deprecated aliases in [module-id-registry.md](../registries/module-id-registry.md) (chain resolves).

| Prior Repo ID | Prior Name | Canonical ID | Canonical Name | Type | Runtime literal touched? |
|---|---|---|---|---|---|
| MOD-0040 | Tenant Organization Foundation | **MOD-0288** | Organization, Person & Position Directory | rename (Blueprint canonical) | No (none in code) |
| MOD-0040-FU01 | Position Assignment User Reference Validation | **MOD-0288-FU01** | Position Assignment User Reference Validation | rename (child) | No |
| MOD-0043 | Tenant Architecture Foundation | **MOD-0009-FU01** | Tenant Architecture Foundation | tenant decomposition (child of MOD-0009) | No |
| MOD-0044 | Tenant Manager Backend | **MOD-0009-FU02** | Tenant Manager Backend | tenant decomposition | No |
| MOD-0046 | Tenant Core UI | **MOD-0009-FU03** | Tenant Core UI | tenant decomposition | No |
| MOD-0045 | Tenant Mgmt Legacy / Gap Reference | — (retired) | — | retire-legacy (non-executable) | No |
| MOD-0013 | Premium Modal Standard | — (standard) | (≈ Blueprint MOD-0286 UI Design System & Page Pattern Governance) | convert-to-standard; removed from module namespace | No |
| name-drift set (MOD-0018/0021/0023/0026/0027/0028/0032/0033/0034/0035/0038/0039/0041/0042/0262/0263/0265/0287) | various | same IDs | Blueprint canonical names | retain-id, name-align only | No (correct runtime literals unchanged) |

## Unresolved EA Reservation Ledger (pending explicit MOD-ID allocation)

> These records are **not** renamed in this pass. No placeholder MOD IDs are assigned. They are **not** converted to deprecated aliases until an exact replacement ID exists. No runtime literal is touched. State = pending explicit Enterprise Architect reservation/decision.

| Current ID | Current Meaning | Requested Canonical Capability | Existing Blueprint Candidate | Exact New ID Assigned? | Runtime Sensitive? | Next Decision Owner | Notes |
|---|---|---|---|---|---|---|---|
| MOD-0047 | Tenant User Foundation (AuthService read-only lookup-validation) | Tenant User / Identity Foundation primitive | none clean (≠ MOD-0288 org dir, ≠ MOD-0261 ext IdP, ≠ MOD-0018 authz) | **No — pending EA** | No (literals already correct) | Enterprise Architect | Squats Blueprint MOD-0047 (Business Continuity). Keep MOD-0047 active until exact new ID allocated; do not rename now. |
| PSS-004 | Tenant Login Security Settings | Tenant login/MFA security settings | none (platform-ops; not in Blueprint) | **No — pending EA** | No | EA | repo-only legacy prefix. |
| PSS-005 | Tenant Module Catalog | Tenant module catalog | partial: Blueprint MOD-0008 "Enterprise Capability / Product Catalog" (different scope) | **No — pending EA** | No | EA | MOD-0008 is a capability label within PSS-005 (see registry/DCP-002). |
| PSS-006 | Tenant Subscription Plan Catalog | Subscription plan catalog | none direct | **No — pending EA** | No | EA | repo-only. |
| PSS-007 | Platform Subscription Feature Management | Feature catalog / plan-feature mapping | none direct | **No — pending EA** | No | EA | repo-only. |
| PSS-008 | Module Details Assignment Inspection | Read-only assignment inspection | none | **No — pending EA** | No | EA | repo-only. |
| PSS-009 | Platform Admin Profile & Settings | Platform admin profile/settings | none | **No — pending EA** | No | EA | repo-only. |
| PSS-010 | Platform Admin Password & MFA Security | Platform admin security | none | **No — pending EA** | No | EA | repo-only. |
| PSS-011 | Lookups / Reference Data | Platform reference data / lookups | none direct | **No — pending EA** | No | EA | repo-only. |
| NEW-002 | Platform Administrators Management | Platform admin lifecycle | none | **No — pending EA** | No | EA | repo-only legacy prefix. |
| NEW-003 | Notification Template Management UI | Notification template CRUD/UI | possibly under MOD-0027 Notification Service | **No — pending EA** | No | EA | EA-003: standalone vs MOD-0027-FU; no active pack. |
| NEW-004 | Tenant Impersonation Tooling | Support/admin impersonation | none | **No — pending EA** | No | EA | EA-004: scope + security model undefined; no active pack. |
| MOD-0266 | Blob / File Storage Provider | Cloud/blob storage abstraction | Blueprint MOD-0266 "Cloud Infrastructure (AWS/Azure/GCP) [External Provider]" (same ID; likely same concept) | **No — pending EA confirm** | No | EA | Likely retain ID + name-align; confirm scope before applying. |
| MOD-0297 | Tenant Subscription Management | SaaS subscription lifecycle | none (not in Blueprint) | **No — pending EA** | **Yes** (Hangfire `TrialExpiryScanJob`, `SubscriptionRenewalJob`) | EA | repo-only; do NOT rename job literals. |
| MOD-0298 | Tenant Module Entitlements | Tenant module entitlements | none (not in Blueprint) | **No — pending EA** | No | EA | repo-only. |
| CAND-CAP-0002-FU05 | Tenant Module Entitlements | Child of `CAND-CAP-0002`; deprecated alias `MOD-0298` | none (not in Blueprint) | **No — pending EA** | No | platform-shared-services | Governance-only candidate child pending EA allocation; never a runtime literal. |
| MOD-0299 ↔ MOD-0169 | SaaS Billing & Invoicing (repo MOD-0299) | SaaS billing capability | Blueprint MOD-0169 "Billing & Invoicing" (ERP / O2C — different domain) | **No — pending EA** | **Yes** (Hangfire `SubscriptionRenewalJob` owner literal `"MOD-0297/MOD-0299"`) | EA | EA-001 / EA-005: disambiguate SaaS vs ERP billing; do NOT rename job owner literal. |
| MOD-0008 | Module Catalog Assignable Expose | Assignable-expose contract within module catalog | Blueprint MOD-0008 "Enterprise Capability / Product Catalog" | **No — pending EA** | No | EA | Recommended: consolidate as capability label within PSS-005; confirm before aliasing. |
| MOD-0169 | Platform Reference (repo; vague) | (clarify) | Blueprint MOD-0169 "Billing & Invoicing" (ERP) | **No — pending EA** | No | EA | EA-005: clarify repo MOD-0169 vs Blueprint ERP billing vs MOD-0299 SaaS billing. |

## CAND-CAP Resolution (second pass — applied)

The unresolved items above have been mapped: `PSS-011 → MOD-0048`, `PSS-004 → MOD-0017-FU01`, `NEW-003 → MOD-0027-FU02`, `PSS-XCUT-SV → MOD-0287`; `MOD-0266`/`MOD-0008` name-aligned to Blueprint; `MOD-0169` retired; and the remainder to the temporary candidate namespace — `CAND-CAP-0001` (Tenant User / Identity Foundation ← MOD-0047), `CAND-CAP-0002` (SaaS Subscription, Plan & Entitlement ← MOD-0297, PSS-005/006/007/008, MOD-0298), `CAND-CAP-0003` (Platform Administration & Operations ← NEW-002, PSS-009/010), `CAND-CAP-0004` (Tenant Impersonation / Support Tooling ← NEW-004), `CAND-CAP-0005` (SaaS Billing & Invoicing ← MOD-0299). Candidate IDs are temporary governance identities pending Enterprise Architect canonical `MOD-xxxx` allocation; lifecycle: legacy → CAND-CAP alias → EA MOD-xxxx. Runtime literals `MOD-0297` / `MOD-0299` remain unchanged as legacy compatibility literals.

### PPM audit transport cross-document lock

The named PPM audit transport slice uses **5 total attempts**. After the first failure the delay is 10
seconds; subsequent delays use exponential backoff with jitter and a 5-minute maximum. A failed fifth
attempt moves the message to DLQ and raises an alarm; the initial attempt is included, leaving four retry attempts.

Shared `EventEnvelope`, `IEventBus`, outbox and inbox mechanics are owned by
`Diten.BuildingBlocks.Eventing`. MOD-0117 owns the logical PPM event at the planned
`services/Diten.PpmService/src/Diten.PpmService.Contracts/Events/**` path. Platform is consumer-only.
`Diten.Platform.Contracts` may own other Platform events, but does not own the PPM event.

The final payload is **Minimal Mutation Audit v1**, containing exactly `auditIntentId`, `actorId`,
`entityType`, `entityId`, `mutation` and `occurredAtUtc`. It evidences only actor, minimal mutation, PPM
aggregate and time; it is not authorization/entitlement evidence, a business snapshot or lifecycle history.

Authorized replay uses the same `EventId` and identical canonical payload bytes; changed bytes are rejected.
If the first delivery was not accepted, replay may create exactly one `AuditEvent`; if accepted, replay
creates none. Idempotency is `ConsumerName + EventId`. Unauthorized replay is forbidden; no replay UI/API
is authorized.

### CAND-CAP-0006 reservation (Work Aggregation / Task Center — Görev Merkezi)

`CAND-CAP-0006` is a **newly reserved** temporary candidate identity (not a legacy migration like
`CAND-CAP-0001…0005`). It governs the cross-module personal work-aggregation surface (code name
`WorkCenterNext`; user-facing product name "Görev Merkezi / Task Center", SAP Task Center line). The Blueprint
(`Blueprint_Data`) has **no** matching MOD row — verified — so no `MOD-xxxx` is invented. Its governance charter
is [DCP-004](delivery-capability-packs/DCP-004-work-aggregation-task-center.md). Lifecycle: candidate →
pending-EA → future EA `MOD-xxxx`. The future canonical Blueprint `MOD-xxxx` allocation is a **separate
Enterprise Architect decision** (EA follow-up); the candidate ID is never written into runtime literals.
Identity gate: `verify_module_id.py --candidate CAND-CAP-0006 --name "Work Aggregation / Task Center (Görev Merkezi)"` → exit 0 (2026-07-24).

### CAND-CAP-0007…0009 canonicalization (Management & Governance core — 2026-07-28)

The Enterprise Blueprint has no canonical rows that can safely absorb the three business/runtime gaps
identified by the audited Management & Governance baseline: Enterprise Strategy goal/objective/cascade
records, a standalone cross-domain Decomposition & Work Structuring Engine, and Business Process Management
process architecture/model/version records. The Enterprise Architect approved temporary candidate treatment
on 2026-07-27; no numeric `MOD-xxxx` is invented.

| Candidate | Reserved name | Boundary | Governance charter |
|---|---|---|---|
| `CAND-CAP-0007 → MOD-0352` | Enterprise Strategy Management | Historical “Enterprise Strategy & Performance Management” is an EA-approved alias; Blueprint subdomain 1.1, outside active DCP-006 1.3/1.4/1.6 scope | DCP-005 |
| `CAND-CAP-0007-FU01` | Enterprise Strategy Security, Tenancy & Data Migration Foundation | Temporary technical hardening/migration slice under canonical parent MOD-0352; exact FU allocation still pending | DCP-005 |
| `CAND-CAP-0008 → MOD-0354` | Decomposition & Work Structuring Engine | Standalone structural mechanics; no generic task/checklist or workflow/approval ownership | DCP-006 |
| `CAND-CAP-0009 → MOD-0355` | Business Process Architecture & Modeling | Process architecture/model/version; no MOD-0023 workflow-run/approval duplication | DCP-006 |

Lifecycle: candidate → pending-EA → future EA canonical `MOD-xxxx`. Candidate IDs are governance-only and
must never appear in service code, API routes, permission prefixes, Mongo collection namespaces, events,
jobs or other runtime literals. Demand/idea is not assigned a candidate: its canonical SoR remains Blueprint
`MOD-0117`; any later Demand FU identity requires its own parent-aware preflight.

Identity gates (2026-07-27):

- `verify_module_id.py --check-id MOD-0352 --name "Enterprise Strategy Management"`
- `verify_module_id.py --candidate CAND-CAP-0007-FU01 --name "Enterprise Strategy Security, Tenancy & Data Migration Foundation"`
- `verify_module_id.py --check-id MOD-0354 --name "Decomposition & Work Structuring Engine"`
- `verify_module_id.py --check-id MOD-0355 --name "Business Process Architecture & Modeling"`

The three base candidate commands now fail closed with exit 2 and identify their canonical replacements.
That failure is intentional alias enforcement, not an unresolved identity. FU01 remains the only temporary
candidate in this group.

## Minimal SoR Reconciliation Records

| Field | Value |
|---|---|
| Business capability | Legal Entity Management |
| Canonical system-of-record | MDM Legal Entity capability |
| Reserved module ID | `MOD-0220` confirmed |
| Reservation basis | Explicit user decision after authoritative planning Excel mapping confirmation |
| Schema review | MOD-0220 minimal backend schema reconciliation reviewed and approved; Legal Entity Foundation is ready-for-dev for the narrow backend-only slice |
| Remaining reconciliation | Authoritative Enterprise Blueprint repository migration pending as non-blocking follow-up |
| Owner domain | MDM |
| Consumer module | `MOD-0288 Organization, Person & Position Directory` (canonicalized from MOD-0040 per DCP-002) |
| Consumer relationship | Read-only `LegalEntityId` reference / lookup validation dependency; validation contract locked and unchanged |
| Forbidden duplication | No Legal Entity aggregate, persistence, lifecycle, API or UI under MOD-0288 |
| Decision gate | `OD-MOD-le-contract` resolved; MOD-0288 pack ready-for-dev for the minimal backend-only v1 slice |
| Related follow-up | Complete authoritative Enterprise Blueprint repository migration; define MDM business-country reference source distinct from PSS-011 |

## Boundary Notes

- PSS-011 countries lookup is Platform provisioning/support only.
- It is not the MDM business-country system of record.
- MDM business-country reference ownership remains a separate follow-up.

## Source / migration note
New target file designed for governance alignment between the business capability matrix (Excel blueprint) and technical implementation modules.

**Workbook supersession (Enterprise Architect decision, 2026-07-28).** The prior planning workbook `execution/modules_pages_planning_v3.xlsx` remains intentionally retired. `docs/System Capability & Implementation Blueprint - master 8.1.xlsx` is the authoritative canonical enterprise module-ID source; Master 7 is its historical predecessor. External provenance is SHA-256 `f37120b0b0edfefe97a8baf6232da6a6bb47629ca7d285097d26993f1ee2c98c`; the workbook-internal abbreviated checksum is not authoritative. No claim is made that every historical sheet was migrated one-to-one.

## Owner / update rule
- Owner: Enterprise Architect / PMO
- Update Rule: Updated during wave planning alignment sessions and before release gate reviews. New canonical MOD-ID allocations for unresolved items above require explicit Enterprise Architect reservation recorded in [module-id-registry.md](../registries/module-id-registry.md) before any rename.
