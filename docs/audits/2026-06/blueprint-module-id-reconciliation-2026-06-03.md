# Blueprint ↔ Registry Module ID Reconciliation Report

- **Date:** 2026-06-03
- **Branch:** feature/governance/blueprint-module-id-reconciliation
- **Source of truth:** `docs/System Capability & Implementation Blueprint - master 7.xlsx` → sheet `Blueprint_Data` (296 modules)
- **Reconciled against:** `execution/registries/module-id-registry.md` (99 records)
- **Status:** READ-ONLY analysis. No canonical document was modified. Respects AGENTS.md §12 (no mass rename).

## Methodology
Each registry record is matched to the Blueprint by (a) exact Module ID and (b) fuzzy module-name similarity. Follow-up packs (`-FUxx`, `-PLAN`, `-RET`, `-5C-Hx`, ...) inherit their base module's decision and are not flagged as wrong numbers.

## Summary
| Bucket | Count | Meaning |
|---|---|---|
| A. Genuine wrong number / collision | 15 | Repo ID maps to a *different* module in the Blueprint |
| B. Name drift (number OK) | 14 | Same module, wording differs; consider aligning the name |
| C. Exact match | 8 | ID and name already agree |
| D. Legacy ID (PSS-/NEW-) | 29 | Non-MOD ID; suggested canonical MOD by name |
| E. In registry, not in Blueprint | 3 | Investigate (repo-only or renamed) |
| F. Follow-ups (inherit base) | 30 | Not wrong numbers; depend on base decision |
| G. Out of scope (DEV-/DCP-) | 0 | Not product modules |

## A. Genuine wrong number / collision  ⚠️ highest priority
| Registry ID | Registry name | Blueprint says this ID = | Repo name best-matches | at Blueprint ID | conf. |
|---|---|---|---|---|---|
| MOD-0008 | Module Catalog Assignable Expose | Enterprise Capability / Product Catalog | Data Warehouse / Lakehouse | MOD-0063 | 0.42 |
| MOD-0009 | Tenant Registry Lifecycle Events | Tenant / Environment Management | Agent Registry & Lifecycle Management | MOD-0292 | 0.79 |
| MOD-0018 | RBAC / Entitlement Production Wiring | RBAC / ABAC Authorization | Management Reporting | MOD-0139 | 0.51 |
| MOD-0026 | Background Job Scheduler | Scheduler / Job Orchestration | Accounts Receivable (AR) | MOD-0120 | 0.43 |
| MOD-0033 | Consumer / Quota Model | API Consumer & Credential Management (Developer Portal) | Supplier Portal | MOD-0148 | 0.5 |
| MOD-0040 | Tenant Organization Foundation | Canonical ID & Correlation Standard | Organization, Person & Position Directory | MOD-0288 | 0.55 |
| MOD-0043 | Tenant Architecture Foundation | SLO/SLA Monitoring | Bank Reconciliation | MOD-0121 | 0.53 |
| MOD-0044 | Tenant Manager Backend | Backup & Restore | Lead Management | MOD-0152 | 0.59 |
| MOD-0045 | Tenant Management Legacy / Gap Reference | Disaster Recovery (RTO/RPO) | Management Reporting | MOD-0139 | 0.51 |
| MOD-0046 | Tenant Core UI | Performance & Capacity Management | e-Signature Service | MOD-0022 | 0.5 |
| MOD-0047 | Tenant User Foundation | Business Continuity & Crisis Management (BCM) | Lease Accounting | MOD-0135 | 0.58 |
| MOD-0169 | Platform Reference | Billing & Invoicing | Platform Standards Registry | MOD-0013 | 0.53 |
| MOD-0262 | External Document Provider | Docs Repository (SharePoint/M365/Google Drive) [External Provider] | Document Control | MOD-0206 | 0.52 |
| MOD-0263 | External Messaging Provider | Messaging Provider (Twilio/Infobip/SendGrid) [External Provider] | Data Masking & Row/Field Security | MOD-0019 | 0.52 |
| MOD-0287 | User Notification Preferences | User Preferences & Workspace Personalization | Service Certification | MOD-0016 | 0.56 |

## B. Name drift (number is correct, wording differs)
| ID | Registry name | Blueprint name | sim. |
|---|---|---|---|
| MOD-0013 | Premium Modal Standard | Platform Standards Registry | 0.49 |
| MOD-0021 | General Audit Trail | Audit Trail Service | 0.58 |
| MOD-0023 | Workflow Designer | Workflow Designer (Approvals/SLAs/Escalations) | 0.58 |
| MOD-0027 | Central Tenant Email / Notification Service | Notification Service (Email/SMS/WhatsApp) | 0.51 |
| MOD-0028 | Document Management | Documentation & Evidence Management | 0.72 |
| MOD-0031 | Evidence Linking Service | Evidence Linking Service (object ↔ evidence) | 0.74 |
| MOD-0032 | API Gateway Hardening | API Gateway | 0.69 |
| MOD-0034 | Webhook Delivery | Webhook Service | 0.65 |
| MOD-0035 | Event Bus / Internal Events | Event Bus / Message Queue | 0.64 |
| MOD-0038 | Event Taxonomy / Naming | Event Taxonomy & Naming Standard | 0.83 |
| MOD-0039 | Schema Compatibility Governance | Schema Compatibility & Deprecation Policy | 0.68 |
| MOD-0041 | Logging / Monitoring / Observability | Logging & Monitoring | 0.72 |
| MOD-0265 | SIEM / Observability Provider | Observability/SIEM (Splunk/Datadog/New Relic) [External Provider] | 0.51 |
| MOD-0266 | Blob / File Storage Provider | Cloud Infrastructure (AWS/Azure/GCP) [External Provider] | 0.49 |

## D. Legacy IDs → suggested canonical MOD (by name)
| Legacy ID | Registry name | Suggested MOD | Blueprint name | conf. |
|---|---|---|---|---|
| NEW-001 | Secrets Management | MOD-0201 | Spare Parts Management | 0.8 |
| NEW-002 | Platform Administrators Management | MOD-0151 | Territory Management | 0.63 |
| NEW-002-FU1 | Platform Administrators Audit Hookup | MOD-0013 | Platform Standards Registry | 0.48 |
| NEW-003 | Notification Template Management UI | MOD-0202 | Sample Management | 0.62 |
| NEW-004 | Tenant Impersonation Tooling | MOD-0007 | Decision & Rationale Log | 0.51 |
| NEW-MOD-0040 | Tenant Org Master Data Foundation | MOD-0173 | Inventory Ledger & Valuation | 0.53 |
| NEW-MOD-0041 | Real DataScopeResolver | MOD-0003 | Data Contract Registry | 0.45 |
| PSS-001 | Identity Access | MOD-0134 | Fixed Assets | 0.44 |
| PSS-004 | Tenant Login Security Settings | MOD-0130 | Statutory Reporting Packs | 0.47 |
| PSS-005 | Tenant Module Catalog | MOD-0052 | Metadata Catalog | 0.59 |
| PSS-006 | Tenant Subscription Plan Catalog | MOD-0052 | Metadata Catalog | 0.5 |
| PSS-007 | Platform Subscription Feature Management | MOD-0184 | Carrier Management | 0.59 |
| PSS-008 | Module Details Assignment Inspection | MOD-0084 | SoR Assignment Publication | 0.61 |
| PSS-009 | Platform Admin Profile & Settings | MOD-0019 | Data Masking & Row/Field Security | 0.51 |
| PSS-009-FU1 | Platform Admin Avatar Upload | MOD-0013 | Platform Standards Registry | 0.47 |
| PSS-009-FU2 | Platform Admin Activity Timeline | MOD-0046 | Performance & Capacity Management | 0.5 |
| PSS-009-FU3 | Platform Admin Password Change | MOD-0013 | Platform Standards Registry | 0.56 |
| PSS-009-FU4 | Preferred Locale + Timezone | MOD-0122 | Period Close & Consolidation | 0.53 |
| PSS-009-T1 | Platform Account Backend Test Coverage | MOD-0013 | Platform Standards Registry | 0.52 |
| PSS-009-T2 | Platform Account Browser Smoke | MOD-0175 | Quarantine / Blocked Stock | 0.47 |
| PSS-010 | Platform Admin Password & MFA Security Settings | MOD-0013 | Platform Standards Registry | 0.49 |
| PSS-010-FU1 | Platform Admin MFA + Active Sessions UI | MOD-0212 | Policy Management & Attestations | 0.46 |
| PSS-011 | Lookups / Reference Data | MOD-0164 | Consent & Preference Management | 0.6 |
| PSS-011-FU1 | Lookups Follow-up 1 | MOD-0190 | S&OP Workflow & Sign-offs | 0.4 |
| PSS-011-FU2 | Lookups Follow-up 2 | MOD-0190 | S&OP Workflow & Sign-offs | 0.4 |
| PSS-011-FU3 | Test Runner Unblock | MOD-0203 | Test Results & CoA | 0.5 |
| PSS-PLAN-RECON-1 | NEW-001 / MOD-0012 ID Merge | MOD-0079 | Conceptual Model Builder | 0.46 |
| PSS-PLAN-RECON-2 | MOD-0043/44/46 Aggregate Reconciliation | MOD-0133 | Corporate Card Reconciliation | 0.62 |
| PSS-XCUT-SV | SavedViews / Personalization | MOD-0287 | User Preferences & Workspace Personalization | 0.6 |

## E. In registry but NOT in Blueprint (investigate)
| ID | Registry name | Closest Blueprint match | at ID | conf. |
|---|---|---|---|---|
| MOD-0297 | Tenant Subscription Management | Tenant / Environment Management | MOD-0009 | 0.7 |
| MOD-0298 | Tenant Module Entitlements | Tenant / Environment Management | MOD-0009 | 0.57 |
| MOD-0299 | SaaS Billing & Invoicing | Billing & Invoicing | MOD-0169 | 0.88 |

## F. Follow-ups (inherit base module decision)
- **MOD-0009** → MOD-0009-CLOSEOUT
- **MOD-0018** → MOD-0018-FU1, MOD-0018-FU10, MOD-0018-FU10a, MOD-0018-FU10b, MOD-0018-FU11, MOD-0018-FU12, MOD-0018-FU13, MOD-0018-FU14, MOD-0018-FU15
- **MOD-0021** → MOD-0021-5C-H1, MOD-0021-5C-H2, MOD-0021-5C-H3, MOD-0021-5C-H4, MOD-0021-FU-CarryOver, MOD-0021-FU-Partner, MOD-0021-PLAN, MOD-0021-RET
- **MOD-0027** → MOD-0027-FU1
- **MOD-0032** → MOD-0032-FU1
- **MOD-0033** → MOD-0033-FU01, MOD-0033-FU1
- **MOD-0040** → MOD-0040-FU01
- **MOD-0041** → MOD-0041-FU
- **MOD-0043** → MOD-0043+MOD-0044+MOD-0046, MOD-0043-DRIFT
- **MOD-0046** → MOD-0046+, MOD-0046-QG
- **MOD-0297** → MOD-0297-FU1
- **MOD-0298** → MOD-0298-FU1

## C. Exact match (no action)
MOD-0002, MOD-0003, MOD-0012, MOD-0014, MOD-0024, MOD-0037, MOD-0042, MOD-0220

## G. Out of scope
—

## Open questions for human decision
1. For bucket A, confirm each repo module's *correct* canonical ID against the Blueprint (low-confidence matches need eyes).
2. For bucket B, decide policy: align registry names to Blueprint wording, or keep repo wording.
3. For bucket D, confirm legacy→MOD mappings; per AGENTS.md §12 these become registry alias/replacement entries, not file renames.
4. For bucket E, confirm whether MOD-0297/0298/0299 are repo-only or live under different Blueprint IDs.
