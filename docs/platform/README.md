# Platform Documentation Index

This folder contains operational, API, and user-facing documentation for the Platform & Shared Services domain. The implementation source of truth remains the code and the approved module packs; these documents describe the current live or partially-live Platform surface.

## Documentation Map

| Module | Status | Documentation | Audit |
|---|---|---|---|
| MOD-0043/0044/0046 Tenant Management | Partial/live | [API](tenant-management/api.md), [User Manual](tenant-management/user-manual.md) | [Foundation Audit](../audits/pss-mod-0043-tenant-architecture-foundation-audit.md), [Verification](../audits/pss-mod-0043-tenant-foundation-verification-2026-04-16.md) |
| PSS-006 Subscription Plan Catalog | Live | [API](subscription-plans/api.md), [User Manual](subscription-plans/user-manual.md) | Pending |
| PSS-007 Subscription Feature Management | Partial/live | [API](subscription-features/api.md), [User Manual](subscription-features/user-manual.md) | [Batch 1](../audits/pss-007-subscription-feature-management-batch1-audit.md), [Batch 2](../audits/pss-007-subscription-feature-management-batch2-audit.md) |
| PSS-005 Module Catalog | Live | [API](module-catalog/api.md), [User Manual](module-catalog/user-manual.md) | [Audit](../audits/pss-005-module-catalog-audit.md) |
| MOD-0298 Tenant Module Entitlements | Partial/live | [API](tenant-module-entitlements/api.md), [User Manual](tenant-module-entitlements/user-manual.md) | [Audit](../audits/pss-mod-0298-tenant-module-entitlements-audit.md) |
| MOD-0297 Tenant Subscription Lifecycle | Partial/live | Covered by [Tenant Management API](tenant-management/api.md) and [Tenant Management User Manual](tenant-management/user-manual.md) | [Audit](../audits/pss-mod-0297-tenant-subscription-management-audit.md) |
| PSS-004 Tenant Login & Security | Partial/live | [API](tenant-login-security/api.md), [User Manual](tenant-login-security/user-manual.md) | Pending |
| PSS-011 Lookups / Reference Data | Live | [API](lookups-reference-data/api.md), [Technical Reference](lookups-reference-data/technical-reference.md) | Pending |
| PSS-009 Platform Admin Profile & Settings | Live | [API](platform-account/api.md), [User Manual](platform-account/user-manual.md) | Pending |
| PSS-010 Platform Admin Password & MFA Security | Partial/live | [API](platform-security/api.md), [User Manual](platform-security/user-manual.md) | Pending |
| NEW-002 Platform Administrators | Live | [API](administrators-management/platform-administrators-api.md), [User Guide](administrators-management/platform-administrators-user-guide.md), [Implementation Plan](administrators-management/platform-administrators-implementation-plan.md) | [Audit](../audits/pss-new-002-platform-administrators-audit.md) |
| MOD-0021 General Audit Trail | Live | [API](audit-trail/api.md), [User Manual](audit-trail/user-manual.md) | Pending |
| MOD-0026 Background Job Scheduler | Live foundation | [Operations Guide](background-jobs/operations-guide.md) | Pending |
| MOD-0035 Event Bus / Internal Events | Partial/live foundation | [Technical Reference](event-bus/technical-reference.md), [Operations Guide](event-bus/operations-guide.md) | Pending |
| MOD-0033 Consumer / Quota Model | Partial/live | [API](consumer-quota-model/api.md), [Operations Guide](consumer-quota-model/operations-guide.md) | [Audit](../audits/pss-mod-0033-consumer-quota-model-audit.md) |
| MOD-0002 Interface Registry | Partial/live | [API](interface-registry/api.md), [Operations Guide](interface-registry/operations-guide.md) | Pending |
| MOD-0012 Secrets & Configuration Vault | Live foundation | [Technical Reference](secrets-configuration-vault/technical-reference.md) | [Audit](../audits/pss-mod-0012-secrets-configuration-vault-audit-2026-05-12.md) |
| MOD-0041 Logging / Monitoring | Partial/live foundation | Existing handoff docs in [observability](observability/) | Pending |

## Pending Modules

The following master-plan items do not have a current live Platform UI/API document in this folder because their implementation is missing, draft-only, or covered by another module boundary: MOD-0003, MOD-0008, MOD-0009, MOD-0014, MOD-0018, MOD-0023, MOD-0024, MOD-0027, MOD-0028, MOD-0031, MOD-0032, MOD-0034, MOD-0037, MOD-0038, MOD-0039, MOD-0042, MOD-0262, MOD-0263, MOD-0265, MOD-0266, MOD-0287, MOD-0299, NEW-001, NEW-003, NEW-004.
