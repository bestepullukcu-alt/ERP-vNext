# Audit Reports Index

This folder is the central audit archive for ERP-vNext. Audit reports stay here for all domains; domain documentation folders such as `docs/platform/` link back to these files instead of owning separate audit folders.

Audit reports are retained artifacts. Do not delete them after a module or cleanup task is completed.

## Naming Standard

| Scope | Pattern | Example |
|---|---|---|
| Platform Shared Services | `pss-{module-id}-{slug}-audit.md` | `pss-mod-0298-tenant-module-entitlements-audit.md` |
| Master Data Management | `mdm-{module-id-or-slug}-audit.md` | `mdm-product-management-audit.md` |
| Enterprise Strategy | `esbp-{module-id-or-slug}-audit.md` | `esbp-strategy-core-audit.md` |
| Developer Enablement | `deven-{module-id-or-slug}-audit.md` | `deven-golden-reference-slim-audit.md` |
| Cross-system engineering | `system-{slug}.md` | `system-antigravity-module-pack-standard-corrections.md` |
| Frontend/static cleanup | `frontend-{slug}.md` | `frontend-wwwroot-cleanup-safety-report.md` |

Use `-batchN-` for split delivery reports and `-YYYY-MM-DD` when the audit is date-specific.

## Current Reports

| File | Domain | Module / Subject | Date / Batch | Related Documentation |
|---|---|---|---|---|
| [frontend-wwwroot-cleanup-safety-report.md](frontend-wwwroot-cleanup-safety-report.md) | Frontend | `wwwroot` cleanup safety | 2026-05-17 | N/A |
| [mdm-product-management-audit.md](mdm-product-management-audit.md) | MDM | Product Management | 2026-04-09 | N/A |
| [pss-mod-0012-secrets-configuration-vault-audit-2026-05-12.md](pss-mod-0012-secrets-configuration-vault-audit-2026-05-12.md) | PSS | MOD-0012 Secrets & Configuration Vault | 2026-05-12 | [Technical Reference](../platform/secrets-configuration-vault/technical-reference.md) |
| [pss-mod-0018-fu13-permission-convention-cache-invalidation-status-2026-08-06.md](pss-mod-0018-fu13-permission-convention-cache-invalidation-status-2026-08-06.md) | PSS | MOD-0018-FU13 Permission Convention + Cache Invalidation Events (implementation status) | 2026-08-06 | [MOD-0018-FU13 module pack](../../execution/domains/platform-shared-services/module-packs/MOD-0018-FU13-permission-convention-cache-invalidation.md) |
| [pss-mod-0027-fu02-notification-template-ui-smoke-2026-07-08.md](pss-mod-0027-fu02-notification-template-ui-smoke-2026-07-08.md) | PSS | MOD-0027-FU02 Notification Template Management UI (live smoke) | 2026-07-08 | [MOD-0027 email migration inventory](mod-0027-email-migration-inventory.md) |
| [pss-mod-0027-fu03-notification-event-catalog-smoke-2026-07-08.md](pss-mod-0027-fu03-notification-event-catalog-smoke-2026-07-08.md) | PSS | MOD-0027-FU03 Notification Event Catalog & Template Binding (smoke + closeout) | 2026-07-08 | [MOD-0027-FU02 smoke](pss-mod-0027-fu02-notification-template-ui-smoke-2026-07-08.md) |
| [pss-mod-0027-fu03a-notification-event-sourcetype-platformseed-bridge-smoke-2026-07-08.md](pss-mod-0027-fu03a-notification-event-sourcetype-platformseed-bridge-smoke-2026-07-08.md) | PSS | MOD-0027-FU03A Notification Event SourceType & PlatformSeed Bridge (smoke + closeout) | 2026-07-08 | [MOD-0027-FU03 smoke](pss-mod-0027-fu03-notification-event-catalog-smoke-2026-07-08.md) |
| [pss-mod-0027-fu04a-tenant-management-notification-event-opt-in-smoke-2026-07-08.md](pss-mod-0027-fu04a-tenant-management-notification-event-opt-in-smoke-2026-07-08.md) | PSS | MOD-0027-FU04A Tenant Management Notification Event Opt-in (smoke + closeout) | 2026-07-08 | [MOD-0027-FU03A smoke](pss-mod-0027-fu03a-notification-event-sourcetype-platformseed-bridge-smoke-2026-07-08.md) |
| [pss-mod-0027-fu04b-eventcode-dispatch-adapter-smoke-2026-07-08.md](pss-mod-0027-fu04b-eventcode-dispatch-adapter-smoke-2026-07-08.md) | PSS | MOD-0027-FU04B EventCode Dispatch Adapter (smoke + closeout) | 2026-07-08 | [MOD-0027-FU04A smoke](pss-mod-0027-fu04a-tenant-management-notification-event-opt-in-smoke-2026-07-08.md) |
| [pss-mod-0033-consumer-quota-model-audit.md](pss-mod-0033-consumer-quota-model-audit.md) | PSS | MOD-0033 Consumer / Quota Model | 2026-05-12 | [API](../platform/consumer-quota-model/api.md), [Operations Guide](../platform/consumer-quota-model/operations-guide.md) |
| [pss-mod-0043-tenant-architecture-foundation-audit.md](pss-mod-0043-tenant-architecture-foundation-audit.md) | PSS | MOD-0043 Tenant Architecture Foundation | Foundation audit | [Tenant Management API](../platform/tenant-management/api.md) |
| [pss-mod-0043-tenant-foundation-verification-2026-04-16.md](pss-mod-0043-tenant-foundation-verification-2026-04-16.md) | PSS | MOD-0043 Tenant Foundation Verification | 2026-04-16 | [Tenant Management API](../platform/tenant-management/api.md) |
| [pss-mod-0297-tenant-subscription-management-audit.md](pss-mod-0297-tenant-subscription-management-audit.md) | PSS | MOD-0297 Tenant Subscription Lifecycle | 2026-05-11 | [Tenant Management API](../platform/tenant-management/api.md) |
| [pss-mod-0298-tenant-module-entitlements-audit.md](pss-mod-0298-tenant-module-entitlements-audit.md) | PSS | MOD-0298 Tenant Module Entitlements | 2026-05-11 | [API](../platform/tenant-module-entitlements/api.md), [User Manual](../platform/tenant-module-entitlements/user-manual.md) |
| [pss-new-002-platform-administrators-audit.md](pss-new-002-platform-administrators-audit.md) | PSS | NEW-002 Platform Administrators | 2026-05-12 | [API](../platform/administrators-management/platform-administrators-api.md), [User Guide](../platform/administrators-management/platform-administrators-user-guide.md) |
| [pss-005-module-catalog-audit.md](pss-005-module-catalog-audit.md) | PSS | PSS-005 Module Catalog | Module audit | [API](../platform/module-catalog/api.md), [User Manual](../platform/module-catalog/user-manual.md) |
| [pss-007-subscription-feature-management-batch1-audit.md](pss-007-subscription-feature-management-batch1-audit.md) | PSS | PSS-007 Subscription Feature Management | Batch 1 | [API](../platform/subscription-features/api.md), [User Manual](../platform/subscription-features/user-manual.md) |
| [pss-007-subscription-feature-management-batch2-audit.md](pss-007-subscription-feature-management-batch2-audit.md) | PSS | PSS-007 Subscription Feature Management | Batch 2 | [API](../platform/subscription-features/api.md), [User Manual](../platform/subscription-features/user-manual.md) |
| [pss-008-module-details-assignment-inspection-audit.md](pss-008-module-details-assignment-inspection-audit.md) | PSS | PSS-008 Module Details Assignment Inspection | 2026-05-08 | [Module Catalog API](../platform/module-catalog/api.md) |
| [system-antigravity-module-pack-standard-corrections.md](system-antigravity-module-pack-standard-corrections.md) | System | Antigravity module pack standard corrections | 2026-05-12 | N/A |
