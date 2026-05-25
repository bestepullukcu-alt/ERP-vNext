# Platform Delivery Board

## Metadata
- **Title**: Platform Delivery Board
- **Canonical Path**: [platform-delivery-board.md](file:///Users/alitufanoglu/ERP-vNext/execution/delivery/platform-delivery-board.md)
- **Legacy Source**: [master-plan.md](file:///Users/alitufanoglu/ERP-vNext/docs/platform/master-plan.md)
- **Migration Phase**: IA Phase 3B-2A
- **Status**: Seeded from active delivery, progress, and gap sections
- **Usage Warning**: This is an active work coordination board. It is **not** a canonical module registry, not a high-level roadmap, and not a linter rules definition source.

---

## Purpose
This document tracks active development items, pending work package candidates, known operational gaps, blockers, and recently completed tasks for coordination between developers and AI agents.

---

## Authority Boundaries
For other phases of governance, consult the relevant source of truth:
* **Canonical Module ID Allocation**: [module-id-registry.md](file:///Users/alitufanoglu/ERP-vNext/execution/registries/module-id-registry.md)
* **High-Level Roadmap & Wave sequencing**: [master-development-plan.md](file:///Users/alitufanoglu/ERP-vNext/execution/portfolio/master-development-plan.md)
* **Module specifications & design contracts**: `execution/domains/{domain}/module-packs/`
* **Test results and evidence**: `docs/qa/acceptance-reports/`
* **Static code style guidelines**: `.antigravity/rules/`

---

## Active Work Candidates
Below is the list of active development tasks and carry-overs extracted from the codebase audit (Section 2.1), progress tab (Section 9), and improvement lists (Section 9.4).

| Work ID | Module ID | Work Type | Source Section | Summary | Status | Priority | Suggested Tool | Suggested Agent / Workflow | Branch | Owner | Blocker | Next Action |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **WIP-MOD-0012-prod-vault** | MOD-0012 | Hardening | Section 9.1 | Implement production Vault adapter & integration tests | planned | High | Antigravity / Gemini | `orchestrator` | TBD | TBD | None | Create Key Vault integration |
| **WIP-MOD-0009-activated-events** | MOD-0009 | Feature | Section 9.1 | Emit missing TenantActivated, Provisioning Success/Failure events | planned | High | Antigravity / Gemini | `orchestrator` | TBD | TBD | None | Implement event emitters |
| **WIP-MOD-0014-boundary-code** | MOD-0014 | Foundation | Section 9.1 | Write repository implementation for module boundaries checking | planned | High | Antigravity / Gemini | `/add-module` | TBD | TBD | None | Create boundary check handler |
| **WIP-MOD-0018-rbac-wiring** | MOD-0018 | Integration | Section 9.1 | Wire RequiresModule/RequiresFeature decorators, invalidate cache, link audit sink | planned | High | Antigravity / Gemini | `orchestrator` | TBD | TBD | None | Decorate platform controllers |
| **WIP-MOD-0298-cache-bulk-ops** | MOD-0298 | Hardening | Section 9.1 | Complete cache TTL, invalidation triggers, bulk operations and audit retrofit | planned | Medium | Antigravity / Gemini | `orchestrator` | TBD | TBD | None | Wire invalidation events |
| **WIP-MOD-0035-live-smoke** | MOD-0035 | Testing | Section 9.1 | Validate local/live RabbitMQ outbox recovery and smoke tests | planned | High | Antigravity / Gemini | `testing-agent` | TBD | TBD | None | Build smoke runner test |
| **WIP-MOD-0027-notification-harden** | MOD-0027 | Hardening | Section 9.1 | Add throttling, locale fallbacks, sensitive-variable guards, template UI | planned | High | Antigravity / Gemini | `orchestrator` | TBD | TBD | None | Implement sensitive guards |
| **WIP-MOD-0263-messaging-adapters** | MOD-0263 | Feature | Section 9.1 | Implement Twilio/SendGrid adapters, failover, bounce webhooks | planned | Medium | Antigravity / Gemini | `orchestrator` | TBD | TBD | None | Integrate MailKit fallback |
| **WIP-NEW-003-template-ui** | NEW-003 | UI | Section 9.2 | Create Notification Templates UI (grid, preview, test send, versioning) | planned | High | Antigravity / Gemini | `/add-module` | TBD | TBD | None | Create Razor forms view |
| **WIP-MOD-0032-gateway-harden** | MOD-0032 | Infrastructure | Section 9.3 | Wire rate limits, circuit breaker, CORS whitelist, delete direct fallbacks | planned | High | Antigravity / Gemini | `orchestrator` | TBD | TBD | None | Remove PlatformServiceUrl |
| **WIP-MOD-0033-quota-automation** | MOD-0033 | Feature | Section 9.3 | Configure reset scheduler, dashboard UI, and 80% alert emails | planned | High | Antigravity / Gemini | `orchestrator` | TBD | TBD | None | Code cron reset job |
| **WIP-MOD-0046-monitoring-ui** | MOD-0046 | UI | Section 9.3 | Add system monitoring, audit deep-link, and files tab to Tenant UI | planned | High | Antigravity / Gemini | `/add-module` | TBD | TBD | None | Expand Tenant core views |
| **WIP-MOD-0041-opentelemetry** | MOD-0041 | Infrastructure | Section 9.3 | Wire OpenTelemetry, live Prometheus scraping, and dashboard JSON | planned | Medium | Antigravity / Gemini | `orchestrator` | TBD | TBD | None | Verify OTLP exporters |
| **WIP-MOD-0002-openapi-ingestion** | MOD-0002 | Feature | Section 9.3 | Automate OpenAPI routes ingestion and ownership policies | planned | Medium | Antigravity / Gemini | `orchestrator` | TBD | TBD | None | Code metadata ingestion |
| **WIP-PSS-011-integration-test** | PSS-011 | Testing | Section 9.4 | Setup Platform WebApplicationFactory integration test infrastructure | planned | Medium | Antigravity / Gemini | `testing-agent` | TBD | TBD | None | Create shared factory base |
| **WIP-PLATFORM-TEST-smoke** | TBD | Testing | Section 9.4 | Automate browser smoke tests (happy-path/forbidden) for Platform forms | planned | Medium | Antigravity / Gemini | `testing-agent` | TBD | TBD | None | Write smoke playbook |
| **WIP-PLATFORM-TEST-datatable-v2** | TBD | Standardization| Section 9.4 | Audit all platform grids to enforce data-dt-standard="v2" contract | planned | Medium | Antigravity / Gemini | `/quality-gate-datatable` | TBD | TBD | None | Standardize data tags |
| **WIP-PSS-XCUT-SV-savedviews** | PSS-XCUT | Security | Section 9.4 | Resolve personalization ownership, add tenant isolation checks | planned | Medium | Claude Code / Opus | Manual Review | TBD | TBD | None | Audit personalization queries. Ownership ve architecture kararı netleşmeden functional package gibi geliştirme başlatılmamalı. |
| **WIP-MOD-0021-RET-smoke** | MOD-0021 | Testing | Section 9.4 | Add AuditRetention UI to browser smoke test packs | planned | Medium | Antigravity / Gemini | `testing-agent` | TBD | TBD | None | Add smoke step |
| **WIP-NEW-002-FU1-audit-hookup** | NEW-002 | Integration | Section 9.4 | Hook up NEW-002 commands to MOD-0021 Audit Trail | planned | High | Antigravity / Gemini | `orchestrator` | TBD | TBD | None | Inherit IAuditableCommand |
| **WIP-PSS-PLAN-RECON-merge** | MOD-0012 | Refactoring | Section 9.4 | Merge legacy NEW-001 vault references into MOD-0012 across 9 files | planned | Medium | Antigravity / Gemini | `orchestrator` | TBD | TBD | None | Update config schemas |
| **WIP-MOD-0021-FU-Partner-scope**| MOD-0021 | Feature | Section 9.4 | Implement partner_admin filters and scoped audit redaction | planned | Medium | Antigravity / Gemini | `orchestrator` | TBD | TBD | None | Add partner criteria query |
| **WIP-MOD-0021-FU-CarryOver** | MOD-0021 | Hardening | Section 9.4 | Apply GUID v6/v7 support, streaming export, and 12+ audit fixes | planned | Medium | Codex / GPT-5.5 | micro-refactor / targeted fix / testing-agent | TBD | TBD | None | Update PII actor masking. Carry-over listesi tek büyük geliştirme değil; küçük, izole hardening patch'lerine bölünmeli. |
| **WIP-MOD-0043-DRIFT-verify** | MOD-0043 | Investigation | Section 9.4 | Investigate DitenAuditService / MdmService directory drift in repo | planned | Medium | Claude Code / Opus | architecture diagnostics / explorer-agent / Manual Review | TBD | TBD | None | Run tree diagnostics. Repo drift, missing service directories ve diagnostic tree doğrulanmadan kod üretimi yapılmamalı. |
| **WIP-PSS-009-T1-test-coverage** | PSS-009 | Testing | Section 9.4 | Increase backend test coverage for profile edits from 4/9 to 9/9 | planned | Medium | Antigravity / Gemini | `/test` | TBD | TBD | None | Add tampering fail tests |
| **WIP-PSS-010-FU1-mfa-ui** | PSS-010 | UI | Section 9.4 | Build Platform admin MFA settings and active sessions UI | planned | High | Antigravity / Gemini | `/add-module` | TBD | TBD | None | Create HTML settings cards |

---

## Blockers / Gap Candidates
Below are identified gaps (Section 13) that pose structural risks to subsequent development waves if not addressed before tenant module rollout.

| Gap ID | Related Module | Gap | Severity | Source Section | Blocking Dependency | Recommended Resolution | Target Location |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **GAP-13-1** | AuthService / Platform | Identity verification and ownership matrix is missing between services | High | Section 13.1 | None | Define sync mechanism and domain authority model under Section 1.6 | [blueprint-master-plan-reconciliation.md](file:///Users/alitufanoglu/ERP-vNext/execution/portfolio/blueprint-master-plan-reconciliation.md) |
| **GAP-13-2** | BuildingBlocks | Sibling services face package version drift risk due to missing policy | High | Section 13.2 | None | Define semver guidelines and per-feature owner mapping inside rules | [blueprint-master-plan-reconciliation.md](file:///Users/alitufanoglu/ERP-vNext/execution/portfolio/blueprint-master-plan-reconciliation.md) |
| **GAP-13-3** | Security / Tenancy | Missing dedicated verification matrix to test X-Tenant-Id header manipulation | Critical | Section 13.3 | None | Enforce TenantResolutionMiddleware scenarios as mandatory checks in module-pack ACs | [work-package-checklist-template.md](file:///Users/alitufanoglu/ERP-vNext/execution/delivery/checklists/work-package-checklist-template.md) |
| **GAP-13-4** | NEW-003 | Notification Template UI is missing while backend commands are completed | Medium | Section 13.4 | MOD-0027 | Develop preview, test send, and versioning interfaces using grid cards | `execution/domains/platform-shared-services/module-packs/NEW-003-notification-template-management-ui.md` |
| **GAP-13-5** | Auth / Invite | Legacy invite controllers use raw SMTP mailer instead of INotificationService | High | Section 13.5 | MOD-0027-FU1 | Migrate invitation templates to the generic notification dispatcher pathway in Track D | `execution/domains/platform-shared-services/module-packs/MOD-0027-central-tenant-email-notification-service.md` |

---

## Recently Completed / Historical Notes
The following milestones are officially marked as completed:
* **MOD-0026**: Background Job Scheduler foundation implemented successfully (PASS). Mongo-backed enqueue verification complete (2026-05-15).
* **PSS-006**: hardcoded fallback configuration removed and integrated with PSS-011 dropdown values.
* **MOD-0021-5C-H1**: Current policies load verified on Retention page (`GET /api/platform/audit/retention`).
* **MOD-0021-5C-H2**: Redact-actor modal scripts integrated into `AuditLog/index.js` localization bundles.
* **MOD-0021-5C-H3**: Audit menu nodes and state-tracking toggles added to PSS master layout sidebar.
* **MOD-0021-5C-H4**: Ayrı `_DetailsModal.cshtml` partial extracted successfully from inline index views.
* **PSS-PLAN-RECON-2**: Aggregated reporting alias reconciliation complete.

---

## Migration Notes
- **Migration Scope**: This board has been seeded solely from Section 2.1, Section 9, and Section 13 of the legacy `docs/platform/master-plan.md`.
- **Status of Specs**: Functional module specs are not part of this document. It serves strictly as a task and blocker queue.
- **Contract Boundary**: The tasks outlined here do not replace Module Packs. A developer or agent cannot initiate coding without an approved Module Pack matching the work item.

---

## Not Allowed Content
The following data classifications are strictly forbidden from this file:
- Canonical identity allocations (use [module-id-registry.md](file:///Users/alitufanoglu/ERP-vNext/execution/registries/module-id-registry.md)).
- High-level wave schedules (use [master-development-plan.md](file:///Users/alitufanoglu/ERP-vNext/execution/portfolio/master-development-plan.md)).
- Complete backend schemas, DTOs, or endpoints parameters list (use Module Packs).
- Static design linter codes (use `.antigravity/rules/`).
