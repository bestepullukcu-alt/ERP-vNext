# ERP-vNext Master Development Plan

## Metadata
- **Title**: ERP-vNext Master Development Plan
- **Canonical Path**: [master-development-plan.md](./master-development-plan.md)
- **Legacy Source**: [master-plan.md](../../docs/platform/master-plan.md)
- **Migration Phase**: IA Phase 3B-1
- **Status**: Migrated high-level roadmap and inventory only
- **Usage Warning**: This file is a target representation and not a complete system-wide replacement until AGENTS, workflows, and prompts are officially updated in later phases.

---

## Purpose
This document serves as the high-level roadmap and scheduling reference for the ERP-vNext project. It is used to align waves, dependency sequencing, and macro module lifecycles.

### This file is for:
- Platform/Admin high-level development roadmap.
- Wave/Track sequencing and priorities.
- Macro module inventory mapping.
- Strategic development planning references.

### This file is NOT for:
- Canonical Module ID reservation (use [module-id-registry.md](../registries/module-id-registry.md)).
- Active daily sprint task and progress tracking (use [platform-delivery-board.md](../delivery/platform-delivery-board.md)).
- Verification logs, test reports, or QA evidence (use `docs/qa/`).
- Release notes or deployment changelogs (use `docs/releases/`).
- Developer coding standards or architectural linter rules (use `.antigravity/rules/`).
- Module-specific specifications, database models, or API endpoints (use Module Packs).

---

## Authority Boundary Mappings
For all other project phases and roles, consult the following sources of truth:
* **Canonical Identity & ID Reservation**: [module-id-registry.md](../registries/module-id-registry.md)
* **Active Work Package & Branch Coordination**: [platform-delivery-board.md](../delivery/platform-delivery-board.md)
* **Module Specifications & Design Contracts**: `execution/domains/{domain}/module-packs/`
* **Static Coding Standards & Rules**: [Rules Directory](../../.antigravity/rules/)
* **Post-Development Reference Documentation**: [Docs Directory](../../docs/)

---

## Module Inventory
Below is the macro status catalog. 

> [!NOTE]
> The **Legacy Completeness %** column serves as a historical macro reference indicator from the legacy plan. It is **not** an active progress tracker. For daily task status and active branches, refer to [platform-delivery-board.md](../delivery/platform-delivery-board.md).

| ID | Module Name | Wave | Priority | Lifecycle Status | Legacy Completeness % | Notes |
|---|---|---|---|---|---|---|
| **MOD-0043/44/46** | Tenant Management Aggregate Index | — | — | partial | 84% | Reporting row only; not an executable module pack. |
| **MOD-0043** | Tenant Architecture Foundation | — | High | in-progress | 80% | Foundation layer. |
| **MOD-0044** | Tenant Manager Backend | — | High | in-progress | 85% | API & handlers. |
| **MOD-0046** | Tenant Core UI | W3-A | High | in-progress | 82% | Admin UI portal. |
| **MOD-0033-FU01** | Tenant Quota Governance UI | W3-A | Medium | partial | 60% | UI slice; owned by MOD-0033. |
| **PSS-006** | Tenant Subscription Plan Catalog | — | — | approved | 96% | Package catalog. |
| **PSS-007** | Platform Subscription Feature Management | — | — | partial | 85% | Feature definitions. |
| **PSS-005** | Tenant Module Catalog | — | — | approved | 93% | Modules list. |
| **MOD-0298** | Tenant Module Entitlements | — | — | partial | 90% | Commercial access mapping. |
| **MOD-0297** | Tenant Subscription Lifecycle | — | — | partial | 75% | Automation hookups. |
| **PSS-004** | Tenant Login & Security Settings | — | — | partial | 86% | MFA and login guards. |
| **PSS-011** | Lookups / Reference Data | — | — | in-progress | 93% | System dropdowns. |
| **PSS-009** | Platform Admin Profile & Settings | — | Medium | in-progress | 85% | Profile details. |
| **PSS-008** | Module Details Assignment Inspection | — | Medium | partial | 65% | Verification screens. |
| **PSS-010** | Platform Admin Password & MFA Security | — | High | partial | 60% | Auth settings. |
| **MOD-0012** | Secrets & Configuration Vault | W1-* | Blocker | in-progress | 85% | Vault settings. |
| **MOD-0014** | Module Boundary Registry | W1-B | High | planned | 0% | Boundaries checking. |
| **MOD-0023** | Workflow Designer | W1 | High | planned | 0% | SLA and approvals. |
| **MOD-0024** | Task & Checklist Engine | W1-W2 | High | planned | 0% | System actions runner. |
| **MOD-0031** | Evidence Linking Service | W1 | Medium | planned | 0% | Proof links metadata. |
| **MOD-0037** | Integration Monitoring & Reconciliation | W2-W3 | Medium | planned | 0% | Reconciler. |
| **NEW-001** | Secrets Management | W1-* | Blocker | deprecated | — | Deprecated alias; use MOD-0012 instead. |
| **NEW-002** | Platform Administrators Management | W1-* | High | in-progress | 95% | Admins CRUD. |
| **MOD-0009** | Tenant Registry Lifecycle Events | W1-A | Blocker | partial | 80% | Event emitters. |
| **MOD-0008** | Module Catalog Assignable Expose | W1-B | Blocker | partial | 80% | catalog mapping. |
| **MOD-0018** | RBAC / Entitlement Enforcement | W1-B | Blocker | partial | 65% | Security enforcement wiring. |
| **MOD-0026** | Background Job Scheduler | W1-C | Blocker | approved | 90% | Cron engine. |
| **MOD-0035** | Event Bus / Internal Events | W1-C | Blocker | partial | 85% | MassTransit broker mapping. |
| **MOD-0027** | Central Tenant Email / Notification Service | W1-D | Blocker | in-progress | 82% | Notification sender. |
| **MOD-0263** | External Messaging Provider | W1-D | Blocker | partial | 55% | Provider adapter. |
| **MOD-0028** | Document / Evidence Metadata | W2-A | High | planned | 0% | Document model storage. |
| **MOD-0266** | Blob / File Storage Provider | W2-A | High | planned | 0% | S3 storage backend. |
| **MOD-0262** | External Document Provider | W2-A | High | planned | 0% | Integrations metadata. |
| **MOD-0021** | General Audit Trail | W2-B | High | in-progress | 98% | System logger mapping. |
| **MOD-0287** | User Notification Preferences | W2-C | High | planned | 0% | Settings CRUD. |
| **MOD-0034** | Webhook Delivery | W2-C | High | planned | 0% | Outbound HTTP triggers. |
| **NEW-003** | Notification Template Management UI | W2-D | High | partial | 35% | UI templates editor. |
| **NEW-004** | Tenant Impersonation Tooling | W2-D | Medium | planned | 0% | Support helper. |
| **MOD-0032** | API Gateway Hardening | W3-A | High | partial | 60% | Gateway filters. |
| **MOD-0033** | Consumer / Quota Model | W3-A | High | partial | 78% | Resource limitation. |
| **MOD-0046+** | Tenant Core UI Extensions | W3-A | High | partial | 60% | UI extensions. |
| **MOD-0299** | SaaS Billing & Invoicing | W3-B | High | planned | 0% | Payments gateway. |
| **MOD-0041** | Logging / Monitoring | W3-C | Medium | partial | 65% | Observability trace provider. |
| **MOD-0042** | Alerting / Incident Runbooks | W3-C | Medium | planned | 0% | Operations alerting. |
| **MOD-0265** | SIEM / Observability Provider | W3-C | Medium | planned | 0% | Security events. |
| **MOD-0038** | Event Taxonomy / Naming | W3-D | Medium | planned | 0% | Naming rules catalog. |
| **MOD-0039** | Schema Compatibility Governance | W3-D | Medium | planned | 0% | Contracts check registry. |
| **MOD-0040** | Tenant Organization Foundation | — | High | ready-for-dev | 0% | Track G-prime org master-data keystone (platform-shared-services). Minimal backend-only schema reconciliation promoted; see DCP-001. Depends on MDM Legal Entity read-only LegalEntityId contract. Tenant User validation and Position-role binding are deferred behind explicit guards. |
| **MOD-0047** | Tenant User Foundation | — | High | done | 100% | Track G Tenant IAM foundation first slice only. AuthService-owned read-only Tenant User lookup-validation contract implemented and validated: locked route, auth model, IsActive referenceability, minimal return shape, fail-closed tenant mismatch policy, permission seed, and 15 passing tests. Broader Tenant User CRUD/lifecycle, frontend, gateway, Tenant Role, and MOD-0040 PositionAssignment UserId validation integration remain follow-ups. |
| **MOD-0220** | Corporate Secretarial / Entity Management | — | High | ready-for-dev | 0% | MDM Legal Entity Foundation slice. Minimal backend schema reconciliation completed and approved. Implementation not started. Allowed first slice remains `services/Diten.MdmService/**` plus repo-standard service tests. Provides read-only LegalEntityId lookup / validation contract for MOD-0040. Enterprise Blueprint repository migration remains non-blocking follow-up. MDM business-country ownership remains separate follow-up. |
| **MOD-0002** | Interface Registry | W3-E | Medium | in-progress | 80% | API routes mapping. |
| **MOD-0003** | Data Contract Registry | W3-E | Medium | planned | 0% | Payload contracts schemas. |
| **PSS-XCUT-SV** | SavedViews / Personalization | Cross-cutting | Medium | partial | 55% | User grid saving options. |

---

## Wave Roadmap Sequencing
Development work package sequencing follows a logical dependency track (Track A → B → (C ∥ D) → E → F → G → H → I):

### Track A — Wrap-up & Plan Senkronizasyonu
* **Purpose**: Synchronize the master plan with actual code and finalize small pending emitters.
* **Involved Module IDs**: `MOD-0009-CLOSEOUT`, `MOD-0297-FU1`, `MOD-0035`
* **Dependencies / Prerequisites**: Background job scheduler (`MOD-0026`) must be active first.
* **Notes**: Adds lifecycle events producer emit triggers and activates subscription suspending automation jobs.

### Track B — RBAC Production Wiring
* **Purpose**: Prepare the RBAC engine on the Platform/Auth services side to be consumed by tenant endpoints.
* **Involved Module IDs**: `MOD-0018-FU1`
* **Dependencies / Prerequisites**: Entitlement check rules (`MOD-0018`) must be wireable.
* **Notes**: Integrates cache invalidation and configures concrete audit sinks to record authorization failure logs.

### Track C — Storage Stack (Tenant Critical Boundary)
* **Purpose**: Establish file storage capabilities. Tenant modules utilizing file uploads must not start before this track.
* **Involved Module IDs**: `MOD-0266`, `MOD-0028`
* **Dependencies / Prerequisites**: Configuration vault credentials (`MOD-0012`) must be accessible.
* **Notes**: Implements `IBlobStorageProvider` for MinIO/Local systems and creates the document metadata entities.

### Track D — Notification Hardening (Parallel to Track C)
* **Purpose**: Elevate the notification service to production standards.
* **Involved Module IDs**: `MOD-0027-FU1`, `NEW-003`
* **Dependencies / Prerequisites**: Standard notification service (`MOD-0027`) must be built.
* **Notes**: Adds variable redactions, locale fallbacks, template manager UI, and rate-limiting rules.

### Track E — Gateway & Quota Closeout
* **Purpose**: Secure the API gateway and activate quota reset/alert automations.
* **Involved Module IDs**: `MOD-0032-FU1`, `MOD-0033-FU1`
* **Dependencies / Prerequisites**: Ocelot Gateway (`MOD-0032`) and Consumer/Quota model (`MOD-0033`) must be implemented.
* **Notes**: Configures Polly circuit breakers and runs monthly period reset automation jobs.

### Track F — Architecture Governance
* **Purpose**: Prevent domain boundary leaks and file duplication before HR/CRM scaling starts.
* **Involved Module IDs**: `MOD-0014`
* **Dependencies / Prerequisites**: None.
* **Notes**: Set up the authoritative boundary registry.

### Track G-prime — Authorization Foundation Extension (Gating Track)
* **Purpose**: Extend MOD-0018 to support enterprise authorization needs (org hierarchy, scoping, temporary access grants).
* **Involved Module IDs**: `MOD-0018-FU10a`, `MOD-0018-FU10b`, `MOD-0018-FU11`, `MOD-0018-FU12`, `MOD-0018-FU13`, `MOD-0018-FU14`, `MOD-0040`, `MOD-0018-FU15`
* **Dependencies / Prerequisites**: `FU10a + FU12 + minimal MOD-0040` (MVF Gate) must be completed before launching Track G role mappings.
* **Notes**: Establishes tenant organization units data context and maps dynamic permission keys.

### Track G — Tenant IAM Baseline
* **Purpose**: Implement tenant user, roles, permissions CRUD and security screens.
* **Involved Module IDs**: `MOD-0047`, Tenant Role Module (ID to reserve)
* **Dependencies / Prerequisites**: Track G-prime MVF threshold must be fully implemented.
* **Notes**: Configures role-permission scopes and tenant user invitation pipelines.

### Track H — Tenant Modülleri Başlangıç
* **Purpose**: Develop target pilot business logic modules in a low-to-high complexity sequence.
* **Involved Module IDs**: `MOD-0046+`, Organization Hierarchy, HR Lite, DevEnablement context cleanup.
* **Dependencies / Prerequisites**: Tracks A through G must be finished.
* **Notes**: Tenant details views are populated with actual data, and the employee metadata structure is connected.

### Track I — Wave 3 Production Hardening
* **Purpose**: Operations hardening, integrations scaling, and long-term contract monitoring.
* **Involved Module IDs**: `MOD-0034`, `MOD-0042`, `MOD-0041`, `MOD-0299`, `NEW-004`, `MOD-0038`, `MOD-0039`, `MOD-0003`
* **Dependencies / Prerequisites**: Active tenant deployments must be stable.
* **Notes**: Integrates SIEM monitors, OTLP Prometheus feeds, SaaS billing platforms, and HMAC webhook signing rules.

---

## Migration Notes
- **Migration Scope**: Only Section 2 (Module Inventory) and Section 12 (Wave Track Sequencing) have been migrated into this document.
- **Excluded Content**: Detailed module specifications, active checklists, codebase test statistics, and linter rule definitions are excluded from this file.
- **Legacy Monolith State**: The file `docs/platform/master-plan.md` remains temporarily active to prevent build script errors and path dependency faults.
