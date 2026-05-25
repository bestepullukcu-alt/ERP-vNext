# Module ID Registry

## Purpose
The Module ID Registry is the canonical index for reserving Module IDs, module names, slugs, and mapping domain ownership. It ensures module identity integrity and prevents ID collisions across the entire ERP-vNext system.

## Scope
Maintains the master list of all planned, approved, and implemented Module IDs, names, slugs, types, and owner domains. It covers both executable modules and associated planning aliases, deprecated references, and follow-ups.

## Not the source for
The registry is strictly for identity management and governance rules. It is **NOT** a tracking tool and must **never** contain:
- Completion percentages (`%` completion).
- Active branches, developer names, or daily developer assignments.
- Work package task lists.
- QA evidence or test reports.
- Release notes or changelogs.
- Detailed "What's done" / "What's missing" task lists.

## Current status
Fully migrated from the legacy location.

## Source / migration note
- **Legacy Source Path**: [module-registry.md](file:///Users/alitufanoglu/ERP-vNext/docs/governance/module-registry.md)
- **Canonical Path after Migration**: [module-id-registry.md](file:///Users/alitufanoglu/ERP-vNext/execution/registries/module-id-registry.md)
- **Temporary Co-existence Note**: The legacy file at `docs/governance/module-registry.md` will remain temporarily unchanged to prevent breaking existing path-dependent AGENTS, workflows, or validation scripts until they are updated in a later phase.

## Owner / update rule
- Owner: Lead Architect
- Update Rule: Modifying or adding records requires reserving an ID here before creating a Module Pack. No progress metrics are permitted in this registry.

---

## Identity Reservation Rules

- `MOD-0021` is reserved for General Audit Trail / Audit Trail Service only.
- `MOD-0041` is reserved for Logging / Monitoring / Observability only.
- `MOD-0040` is reserved for Tenant Organization Foundation and is required before Tenant Users / Tenant Roles implementation.
- `MOD-0046-QG` is not a standalone module ID. Tenant Quota Governance UI is tracked as `MOD-0033-FU01`.
- Slug-only IDs are not canonical IDs. Slugs belong in the `Slug` field.

---

## Canonical Registry Table

| Canonical ID | Canonical Module Name | Slug | Type | Status | Deprecated Alias | Replacement ID | Owner Domain | Notes |
|---|---|---|---|---|---|---|---|---|
| MOD-0002 | Interface Registry | interface-registry | Module | approved | MOD-0002-interface-registry |  | platform-shared-services | Module pack exists. |
| MOD-0003 | Data Contract Registry | data-contract-registry | Module | planned / missing | MOD-0003-data-contract-registry |  | platform-shared-services | Referenced by master plan and Interface Registry dependencies; no module pack found. |
| MOD-0008 | Module Catalog Assignable Expose | module-catalog-assignable-expose | Module | partial / master-plan only |  |  | platform-shared-services | Keep meaning unchanged until a dedicated pack exists. |
| MOD-0009 | Tenant Registry Lifecycle Events | tenant-lifecycle-events | Module | review | MOD-0009-tenant-lifecycle-events; MOD-0009-owned |  | platform-shared-services | Module pack exists. |
| MOD-0009-CLOSEOUT | Tenant Lifecycle Events Closeout | tenant-lifecycle-events-closeout | Follow-up | planned |  | MOD-0009 | platform-shared-services | Master-plan follow-up under MOD-0009. [Non-executable] |
| MOD-0012 | Secrets & Configuration Vault | secrets-configuration-vault | Foundation | review | NEW-001; MOD-0012-secrets-configuration-vault | MOD-0012 | platform-shared-services | `NEW-001` is legacy alias only. |
| MOD-0013 | Premium Modal Standard | premium-modal-standard | Standard | referenced |  |  | global / platform-shared-services | Referenced by PSS-010; not a PSS module pack in this folder. [Non-executable spec] |
| MOD-0014 | Module Boundary Registry | module-boundary-registry | Module | in-progress | MOD-0014-module-boundary-registry |  | platform-shared-services | Module pack exists. |
| MOD-0018 | RBAC / Entitlement Production Wiring | rbac-abac-authorization | Foundation | ready-for-dev | MOD-0018-rbac-abac-authorization |  | platform-shared-services | Parent authorization foundation. |
| MOD-0018-FU1 | Authorization Foundation Follow-up 1 | authorization-foundation-fu1 | Follow-up | planned |  | MOD-0018 | platform-shared-services | Master-plan follow-up. [Non-executable] |
| MOD-0018-FU10 | Authorization Decision Contract Extension | authorization-decision-contract-extension | Follow-up | ready-for-dev | FU10; MOD-0018-FU10-authorization-decision-contract-extension |  | platform-shared-services | Parent record for FU10a/FU10b. [Non-executable] |
| MOD-0018-FU10a | Pure Authorization Decision Contract Extension | authorization-decision-contract-extension-contract | Follow-up | implemented / referenced | FU10a | MOD-0018-FU10 | platform-shared-services | Shorthand `FU10a` maps here. [Non-executable] |
| MOD-0018-FU10b | EntitlementChecker ResolvedFrom Mapping | entitlementchecker-resolvedfrom-mapping | Follow-up | implemented / referenced | FU10b | MOD-0018-FU10 | platform-shared-services | Shorthand `FU10b` maps here. [Non-executable] |
| MOD-0018-FU11 | Temporary Access Pipeline Binding | temporary-access-pipeline-binding | Follow-up | planned | FU11 | MOD-0018-FU11 | platform-shared-services | Must not be confused with MOD-0041. [Non-executable] |
| MOD-0018-FU12 | Tenant Authorization Context Foundation | tenant-authorization-context-foundation | Follow-up | draft | FU12; MOD-0018-FU12-tenant-authorization-context-foundation |  | platform-shared-services | Module pack exists. [Non-executable] |
| MOD-0018-FU13 | Permission Convention + Cache Invalidation Events | permission-convention-cache-invalidation | Follow-up | planned | FU13 |  | platform-shared-services | Master-plan follow-up under MOD-0018. [Non-executable] |
| MOD-0018-FU14 | Effective Access Explain + Allow Audit | effective-access-explain-allow-audit | Follow-up | planned | FU14 |  | platform-shared-services | Master-plan follow-up under MOD-0018. [Non-executable] |
| MOD-0018-FU15 | Real DataScopeResolver | real-data-scope-resolver | Follow-up | planned / reserved | NEW-MOD-0041 | MOD-0018-FU15 | platform-shared-services | Replacement for deprecated `NEW-MOD-0041`; depends on MOD-0040 backing data. [Non-executable] |
| MOD-0021 | General Audit Trail | general-audit-trail | Module | ready-for-dev / implemented evidence | MOD-0021-audit-trail-service; MOD-0021-general-audit-trail | MOD-0021 | platform-shared-services | Canonical owner of audit trail. No other module may claim this ID. |
| MOD-0021-PLAN | General Audit Trail - All Phases Implementation Plan | general-audit-trail-all-phases-plan | Planning Artifact | implementation-plan | MOD-0021 as planning-file frontmatter; MOD-0021-all-phases-implementation-plan; MOD-0021-phase-2-handoff-plan | MOD-0021 | platform-shared-services | Not an executable module ID; references the MOD-0021 source pack. [Non-executable] |
| MOD-0021-RET | Audit Retention Surface Visibility / Smoke | audit-retention-surface-smoke | Follow-up | planned |  | MOD-0021 | platform-shared-services | Master-plan follow-up under MOD-0021. [Non-executable] |
| MOD-0021-5C-H1 | Retention Page Loads Existing Policies | audit-retention-load-existing-policies | Follow-up | done |  | MOD-0021 | platform-shared-services | Master-plan hardening item. [Non-executable] |
| MOD-0021-5C-H2 | Redact Actor UI | redact-actor-ui | Follow-up | done |  | MOD-0021 | platform-shared-services | Master-plan hardening item. [Non-executable] |
| MOD-0021-5C-H3 | Sidebar Navigation Entry | audit-sidebar-navigation-entry | Follow-up | done |  | MOD-0021 | platform-shared-services | Master-plan hardening item. [Non-executable] |
| MOD-0021-5C-H4 | Details Modal Partial | audit-details-modal-partial | Follow-up | done |  | MOD-0021 | platform-shared-services | Master-plan hardening item. [Non-executable] |
| MOD-0021-FU-Partner | Partner Admin Audit Scope | partner-admin-audit-scope | Follow-up | planned |  | MOD-0021 | platform-shared-services | Partner-scoped audit follow-up. [Non-executable] |
| MOD-0021-FU-CarryOver | Audit Carry-over Hardening | audit-carry-over-hardening | Follow-up | planned |  | MOD-0021 | platform-shared-services | Non-blocker carry-over list. [Non-executable] |
| MOD-0023 | Workflow Designer | workflow-designer | Module | review / planned | MOD-0023-workflow-designer; MOD-0023-workflow-designerController |  | platform-shared-services | Module pack exists; controller-style suffix is not a module ID. |
| MOD-0024 | Task & Checklist Engine | task-checklist-engine | Module | review / planned | MOD-0024-task-checklist-engine; MOD-0024-task-checklist-engineController |  | platform-shared-services | Module pack exists; controller-style suffix is not a module ID. |
| MOD-0026 | Background Job Scheduler | background-job-scheduler | Foundation | done | MOD-0026-background-job-scheduler; MOD-0026-owned |  | platform-shared-services | Module pack exists. |
| MOD-0027 | Central Tenant Email / Notification Service | central-tenant-email-notification-service | Module | approved | MOD-0027-central-tenant-email-notification-service |  | platform-shared-services | Module pack exists. |
| MOD-0027-FU1 | Notification Service Follow-up 1 | notification-service-fu1 | Follow-up | planned |  | MOD-0027 | platform-shared-services | Master-plan follow-up. [Non-executable] |
| MOD-0028 | Document Management | document-management | Module | review / planned | MOD-0028-document-management; MOD-0028-document-managementController |  | platform-shared-services | Module pack exists; controller-style suffix is not a module ID. |
| MOD-0031 | Evidence Linking Service | evidence-linking-service | Module | review / planned | MOD-0031-evidence-linking-service; MOD-0031-evidence-linking-serviceController |  | platform-shared-services | Module pack exists; controller-style suffix is not a module ID. |
| MOD-0032 | API Gateway Hardening | api-gateway | Foundation | review / partial | MOD-0032-api-gateway; MOD-0032-api-gatewayController |  | platform-shared-services | Module pack exists; controller-style suffix is not a module ID. |
| MOD-0032-FU1 | Frontend Direct-service Fallback Removal | frontend-direct-service-fallback-removal | Follow-up | planned |  | MOD-0032 | platform-shared-services | Master-plan follow-up. [Non-executable] |
| MOD-0033 | Consumer / Quota Model | consumer-quota-model | Module | in-progress | MOD-0033-consumer-quota-model |  | platform-shared-services | Meaning unchanged. |
| MOD-0033-FU01 | Tenant Quota Governance UI | tenant-quota-governance-ui | Feature Slice | approved | MOD-0046-QG; MOD-0046-tenant-quota-governance-ui; MOD-0033-FU01-tenant-quota-governance-ui | MOD-0033-FU01 | platform-shared-services | UI slice rendered in MOD-0046 Tenant UI; owned by MOD-0033. |
| MOD-0033-FU1 | Quota Runtime Automation | quota-runtime-automation | Follow-up | planned |  | MOD-0033 | platform-shared-services | Existing master-plan follow-up for period reset, warning, breach notification. Kept distinct from MOD-0033-FU01. [Non-executable] |
| MOD-0034 | Webhook Delivery | webhook-delivery | Module | planned / missing |  |  | platform-shared-services | Referenced in master plan; no module pack found. |
| MOD-0035 | Event Bus / Internal Events | event-bus-message-queue | Foundation | partial | MOD-0035-event-bus-message-queue |  | platform-shared-services | Module pack exists. |
| MOD-0037 | Integration Monitoring & Reconciliation | integration-monitoring | Module | review / planned | MOD-0037-integration-monitoring; MOD-0037-integration-monitoringController |  | platform-shared-services | Module pack exists; controller-style suffix is not a module ID. |
| MOD-0038 | Event Taxonomy / Naming | event-taxonomy-naming | Module | planned / missing |  |  | platform-shared-services | Referenced in master plan; no module pack found. |
| MOD-0039 | Schema Compatibility Governance | schema-compatibility-governance | Module | planned / missing | MOD-0039-schema-compatibility-governance |  | platform-shared-services | Referenced in master plan and packs; no module pack found. |
| MOD-0040 | Tenant Organization Foundation | tenant-organization-foundation | Foundation | Planned / Reserved | NEW-MOD-0040 | MOD-0040 | platform-shared-services | Required before Tenant Users / Tenant Roles implementation. |
| MOD-0041 | Logging / Monitoring / Observability | logging-monitoring | Foundation | approved | MOD-0041-logging-monitoring |  | platform-shared-services | Reserved for observability only. Do not use for DataScopeResolver. |
| MOD-0041-FU | Observability Exporter / Prometheus / Grafana Follow-up | observability-exporter-follow-up | Follow-up | planned |  | MOD-0041 | platform-shared-services | Master-plan roadmap follow-up under MOD-0041. [Non-executable] |
| MOD-0042 | Alerting / Incident Runbooks | alerting-incident-runbooks | Module | approved / planned | MOD-0042-alerting-incident-runbooks; MOD-0042-alerting-incident-runbooksController; MOD-0042-ready |  | platform-shared-services | Module pack exists; controller-style/readiness suffixes are not module IDs. |
| MOD-0043 | Tenant Architecture Foundation | tenant-architecture-foundation | Module | done | MOD-0043-tenant-architecture-foundation |  | platform-shared-services | Meaning unchanged. |
| MOD-0043-DRIFT | Tenant Architecture Pack/Audit Drift | tenant-architecture-drift | Governance Finding | open |  | MOD-0043 | platform-shared-services | Master-plan reconciliation item. [Non-executable status tag] |
| MOD-0043+MOD-0044+MOD-0046 | Tenant Management Aggregate Index | tenant-management-aggregate-index | Reporting Alias | partial | MOD-0043/44/46 | MOD-0043; MOD-0044; MOD-0046 | platform-shared-services | Reporting row only; not an executable module pack. [Non-executable] |
| MOD-0044 | Tenant Manager Backend | tenant-manager | Module | in-progress | MOD-0044-tenant-manager |  | platform-shared-services | Meaning unchanged. |
| MOD-0045 | Tenant Management Legacy / Gap Reference | tenant-management-gap-reference | Reference | unknown |  |  | platform-shared-services | Referenced by MOD-0046 pack; no canonical pack found. [Non-executable] |
| MOD-0046 | Tenant Core UI | tenant-core-ui | Module | in-progress | MOD-0046-tenant-core-ui |  | platform-shared-services | Meaning unchanged. |
| MOD-0046+ | Tenant Core UI Extensions | tenant-core-ui-extensions | Reporting Alias | partial |  | MOD-0046 | platform-shared-services | Aggregate extension label only. [Non-executable] |
| MOD-0046-QG | Tenant Quota Governance UI | tenant-quota-governance-ui | Deprecated Alias | deprecated | MOD-0046-QG | MOD-0033-FU01 | platform-shared-services | Not standalone; use MOD-0033-FU01. [Non-executable] |
| MOD-0169 | Platform Reference | platform-reference-mod-0169 | Reference | unknown |  |  | platform-shared-services | Observed in master-plan references; owner/status not resolved in current module packs. [Non-executable] |
| MOD-0262 | External Document Provider | external-document-provider | Module | planned / missing |  |  | platform-shared-services | Referenced in master plan; no module pack found. |
| MOD-0263 | External Messaging Provider | external-messaging-provider | Module | approved | MOD-0263-external-messaging-provider; MOD-0263-owned |  | platform-shared-services | Module pack exists. |
| MOD-0265 | SIEM / Observability Provider | siem-observability-provider | Module | planned / missing |  |  | platform-shared-services | Consumes MOD-0041 signals; no module pack found. |
| MOD-0266 | Blob / File Storage Provider | blob-file-storage-provider | Module | planned / missing |  |  | platform-shared-services | Referenced in master plan; no module pack found. |
| MOD-0287 | User Notification Preferences | user-notification-preferences | Module | planned / missing |  |  | platform-shared-services | Referenced in master plan; no module pack found. |
| MOD-0297 | Tenant Subscription Management | tenant-subscription-management | Module | review | tenant-subscription-management; MOD-0297-tenant-subscription-management | MOD-0297 | platform-shared-services | Frontmatter uses canonical ID and slug separately. |
| MOD-0297-FU1 | Subscription Runtime Automation | subscription-runtime-automation | Follow-up | planned |  | MOD-0297 | platform-shared-services | Trial expiry, renewal, past-due automation. [Non-executable] |
| MOD-0298 | Tenant Module Entitlements | tenant-module-entitlements | Module | approved | MOD-0298-tenant-module-entitlements-module-pack |  | platform-shared-services | Canonical module pack filename is `MOD-0298-tenant-module-entitlements.md`; deprecated filename alias retained only for historical lookup. |
| MOD-0298-FU1 | Entitlement Cache Invalidation Consumer | entitlement-cache-invalidation-consumer | Follow-up | planned |  | MOD-0298 | platform-shared-services | Master-plan follow-up. [Non-executable] |
| MOD-0299 | SaaS Billing & Invoicing | saas-billing-invoicing | Module | planned / missing |  |  | platform-shared-services | Referenced in master plan; no module pack found. |
| PSS-001 | Identity Access | identity-access | Legacy Reference | unknown / external | PSS-001-identity-access |  | platform-shared-services | Historical/upstream reference only in current scan. [Non-executable] |
| PSS-004 | Tenant Login Security Settings | tenant-login-security-settings | Module | in-progress | PSS-004-tenant-login-security-settings |  | platform-shared-services | Module pack exists. |
| PSS-005 | Tenant Module Catalog | tenant-module-catalog | Module | review | PSS-005-tenant-module-catalog; pss-pss-005-module-catalog-audit.md | PSS-005 | platform-shared-services | Double-prefix audit file name replaced with `pss-005-module-catalog-audit.md`. |
| PSS-006 | Tenant Subscription Plan Catalog | tenant-subscription-plan-catalog | Module | approved | PSS-006-tenant-subscription-plan-catalog |  | platform-shared-services | Meaning unchanged. |
| PSS-007 | Platform Subscription Feature Management | subscription-feature-management | Module | review | PSS-007-subscription-feature-management; pss-pss-007-* audit files | PSS-007 | platform-shared-services | Double-prefix audit file names replaced with `pss-007-*`. |
| PSS-008 | Module Details Assignment Inspection | module-details-assignment-inspection | Module | review | PSS-008-module-details-assignment-inspection; pss-pss-008-module-details-assignment-inspection-audit.md | PSS-008 | platform-shared-services | Double-prefix audit file name replaced with `pss-008-module-details-assignment-inspection-audit.md`. |
| PSS-009 | Platform Admin Profile & Settings | platform-admin-profile-settings | Module | ready-for-dev | PSS-009-platform-admin-profile; PSS-009-platform-admin-profile-settings |  | platform-shared-services | Module pack exists. |
| PSS-009-FU1 | Platform Admin Avatar Upload | platform-admin-avatar-upload | Follow-up | planned |  | PSS-009 | platform-shared-services | Master-plan follow-up. [Non-executable] |
| PSS-009-FU2 | Platform Admin Activity Timeline | platform-admin-activity-timeline | Follow-up | planned |  | PSS-009 | platform-shared-services | Master-plan follow-up. [Non-executable] |
| PSS-009-FU3 | Platform Admin Password Change | platform-admin-password-change | Follow-up | planned |  | PSS-009 | platform-shared-services | Master-plan follow-up. [Non-executable] |
| PSS-009-FU4 | Preferred Locale + Timezone | preferred-locale-timezone | Follow-up | planned |  | PSS-009 | platform-shared-services | Master-plan follow-up. [Non-executable] |
| PSS-009-T1 | Platform Account Backend Test Coverage | platform-account-backend-tests | Test Follow-up | planned |  | PSS-009 | platform-shared-services | Master-plan test follow-up. [Non-executable] |
| PSS-009-T2 | Platform Account Browser Smoke | platform-account-browser-smoke | Test Follow-up | planned |  | PSS-009 | platform-shared-services | Master-plan test follow-up. [Non-executable] |
| PSS-010 | Platform Admin Password & MFA Security Settings | platform-admin-security | Module | draft | PSS-010-platform-admin-security |  | platform-shared-services | Meaning unchanged. |
| PSS-010-FU1 | Platform Admin MFA + Active Sessions UI | platform-admin-mfa-active-sessions | Follow-up | planned |  | PSS-010 | platform-shared-services | Master-plan follow-up. [Non-executable] |
| PSS-011 | Lookups / Reference Data | lookups-reference-data | Module | ready-for-dev | PSS-011-lookups-reference-data |  | platform-shared-services | Module pack exists. |
| PSS-011-FU1 | Lookups Follow-up 1 | lookups-fu1 | Follow-up | planned |  | PSS-011 | platform-shared-services | Master-plan follow-up. [Non-executable] |
| PSS-011-FU2 | Lookups Follow-up 2 | lookups-fu2 | Follow-up | planned |  | PSS-011 | platform-shared-services | Master-plan follow-up. [Non-executable] |
| PSS-011-FU3 | Test Runner Unblock | test-runner-unblock | Test Follow-up | planned |  | PSS-011 | platform-shared-services | Master-plan test follow-up. [Non-executable] |
| PSS-PLAN-RECON-1 | NEW-001 / MOD-0012 ID Merge | new001-mod0012-id-merge | Governance Follow-up | planned |  | MOD-0012 | platform-shared-services | Keep NEW-001 as legacy alias or retire after reconciliation. [Non-executable] |
| PSS-PLAN-RECON-2 | MOD-0043/44/46 Aggregate Reconciliation | tenant-aggregate-reconciliation | Governance Follow-up | done | MOD-0043/44/46 executable module assumption | MOD-0043; MOD-0044; MOD-0046 | platform-shared-services | Aggregate row is reporting-only. [Non-executable] |
| PSS-XCUT-SV | SavedViews / Personalization | savedviews-personalization | Cross-cutting | partial |  |  | platform-shared-services | Code exists; dedicated module pack not found. [Non-executable] |
| NEW-001 | Secrets Management | secrets-management | Deprecated Alias | deprecated | NEW-001 | MOD-0012 | platform-shared-services | Use MOD-0012. [Non-executable] |
| NEW-002 | Platform Administrators Management | platform-administrators | Module | in-progress | NEW-002-platform-administrators |  | platform-shared-services | Meaning unchanged. |
| NEW-002-FU1 | Platform Administrators Audit Hookup | platform-administrators-audit-hookup | Follow-up | planned |  | NEW-002 | platform-shared-services | Master-plan follow-up. [Non-executable] |
| NEW-003 | Notification Template Management UI | notification-template-management-ui | Module | planned / partial backend |  |  | platform-shared-services | Meaning unchanged. |
| NEW-004 | Tenant Impersonation Tooling | tenant-impersonation-tooling | Module | planned / missing |  |  | platform-shared-services | Meaning unchanged. |
| NEW-MOD-0040 | Tenant Org Master Data Foundation | tenant-organization-foundation | Deprecated Alias | deprecated | NEW-MOD-0040 | MOD-0040 | platform-shared-services | Use MOD-0040. [Non-executable] |
| NEW-MOD-0041 | Real DataScopeResolver | real-data-scope-resolver | Deprecated Alias | deprecated | NEW-MOD-0041 | MOD-0018-FU15 | platform-shared-services | Must not be used because MOD-0041 is reserved for Logging / Monitoring. [Non-executable] |
| FU1 | Generic Follow-up 1 Shorthand | fu1 | Shorthand Alias | deprecated | FU1 | parent-specific FU ID | platform-shared-services | Avoid bare FU IDs outside local prose; use parent-prefixed IDs. [Non-executable] |
| FU10 | Authorization Decision Follow-up Shorthand | authorization-decision-follow-up | Shorthand Alias | deprecated | FU10 | MOD-0018-FU10 | platform-shared-services | Use parent-prefixed ID. [Non-executable] |
| FU10a | Authorization Decision Contract Extension Shorthand | authorization-decision-contract-extension-contract | Shorthand Alias | deprecated | FU10a | MOD-0018-FU10a | platform-shared-services | Use parent-prefixed ID in registry/frontmatter. [Non-executable] |
| FU10b | EntitlementChecker Mapping Shorthand | entitlementchecker-mapping | Shorthand Alias | deprecated | FU10b | MOD-0018-FU10b | platform-shared-services | Use parent-prefixed ID in registry/frontmatter. [Non-executable] |
| FU11 | Temporary Access Follow-up Shorthand | temporary-access-follow-up | Shorthand Alias | deprecated | FU11 | MOD-0018-FU11 | platform-shared-services | Use parent-prefixed ID in registry/frontmatter. [Non-executable] |
| FU12 | Tenant Authorization Context Shorthand | tenant-authorization-context | Shorthand Alias | deprecated | FU12 | MOD-0018-FU12 | platform-shared-services | Use parent-prefixed ID in registry/frontmatter. [Non-executable] |
| FU13 | Permission Convention Shorthand | permission-convention | Shorthand Alias | deprecated | FU13 | MOD-0018-FU13 | platform-shared-services | Use parent-prefixed ID in registry/frontmatter. [Non-executable] |
| FU14 | Effective Access Explain Shorthand | effective-access-explain | Shorthand Alias | deprecated | FU14 | MOD-0018-FU14 | platform-shared-services | Use parent-prefixed ID in registry/frontmatter. [Non-executable] |

---

## Open Governance Notes

- `MOD-0021` duplicate frontmatter use was removed from the all-phases planning file by assigning `MOD-0021-PLAN` and linking it back to canonical `MOD-0021`.
- `MOD-0297` frontmatter now uses `id: MOD-0297` and `slug: tenant-subscription-management`.
- `MOD-0041` remains observability-only. Data scope resolution is reserved as `MOD-0018-FU15`.
- `MOD-0040` is reserved for Tenant Organization Foundation and remains a prerequisite for Tenant Users / Tenant Roles.
- `MOD-0033-FU1` and `MOD-0033-FU01` are both observed. `MOD-0033-FU01` is the canonical Quota Governance UI slice requested by governance; `MOD-0033-FU1` remains the existing runtime automation follow-up until a later normalization pass.

---

## Cleanup Candidates / Future Normalization

The following entries are non-executable items that should eventually be cleaned up or normalized in the next phases:
1. **Planning & Governance Aliases**: `MOD-0021-PLAN`, `MOD-0043-DRIFT`, `MOD-0043+MOD-0044+MOD-0046`, `MOD-0046+`, and `PSS-PLAN-RECON-2` are reporting-only or metadata indicators. They do not represent functional packages and should be mapped strictly as metadata links or handled in wave reconciliation documents.
2. **Follow-ups (FU) / Hardening Slices**: Slices like `MOD-0018-FU*`, `MOD-0021-5C-H*`, and `PSS-009-FU*` should be fully migrated into active sprints or tracked on the Platform Delivery Board rather than remaining in the canonical registry.
3. **Deprecated Shorthands**: Bare aliases such as `FU1` and `FU10-FU14` are fully deprecated and should be archived once references in legacy files are cleared.
