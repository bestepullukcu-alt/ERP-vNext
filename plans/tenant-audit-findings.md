# Tenant-side E2E Audit — Findings & Debts

> Started 2026-06-25. Walkthrough follows the real provisioning chain:
> **Module Catalog → Subscription Plan → assign module → Tenant → assign sub → admin invite → tenant login → nav → RBAC → use.**
> Method: Claude pre-checks backend (bearer) + static; owner walks the browser; bugs batched into one fix prompt at phase end (branch stays whole).

## Automated baseline (Track 1 + 2) — GREEN
- **Data/API (bearer sweep, 11/11 200):** Users 15 · Roles 5 · Permissions 144 · LegalEntities 5 · OrgUnits 2 · Positions 2 · PositionAssignments 1 · TenantSecurity · Workflow defs/my-tasks · Navigation 1. Backend healthy.
- **View-location (static):** all tenant view folders resolve under registered ViewLocationFormats (Governance/Organization/MasterData/Platform/root). No 500-render risk.

## Debt register (found during walkthrough)

### Domain & Service Management
| # | Bug | Sev | Detail / fix |
|---|---|---|---|
| DS-1 | Code not auto-derived from Display Name (inconsistent w/ Module Catalog) | 🟡 ✅FIXED | Domain+Service create forms had a manual Code field w/ normalize-preview but no DisplayName→Code binding. FIX-DS1: added auto-slug (create-only, until manual Code edit) to both `DomainManagement/index.js` + `ServiceManagement/index.js`. JS-only. Live-pending owner browser confirm. |

### Module Catalog
| # | Bug | Sev | Detail / fix |
|---|---|---|---|
| MC-1 | Beta/Preview KPI cards permanently 0 (dead) | 🔴 | Enum has Beta=4/Preview=5 + stats handler counts them, but Status dropdowns (`_Form`, `_Filter`, `PageDetails`, `Details`) only offer Draft/Active/Inactive/Deprecated; `formatStatus` JS lacks Beta/Preview colors. Fix: add Beta+Preview options to 4 dropdowns + 2 color cases. |
| MC-2 | Duplicate `DITENDOCUMENTSERVICE` in platform_module_services | 🟡 | Two identical service rows. Clean the dup; check seed path. |
| MC-3 | Module-Code lookup sources from raw permission top-segments | 🔴 | Lists infra (`auth`/`mdm`/`platform`) as if products; `platform` is an umbrella of 19 sub-modules; free-tag allows case-variant (`Platform` vs `platform`) breaking exact-match rule. Fix per [[project-module-identity-model]]: source from self-reg manifests, exclude infra, normalize/disable free-tag; make workflow/document-management self-register granular codes. |

## Known debts (pre-walkthrough)
- **Login `?tenantId=` fallback missing** — plain `/account/login` has no DefaultTenant fallback (invite-flow only). Dev-fallback proposed (Development-only → DefaultTenant; prod still requires explicit tenant). 🟡
- **MOD-0285 navigation management UI** not built (loader done, governance/admin UI deferred). 🟢

## Module Catalog / self-registration — FIX LOG (all live-verified, uncommitted)
| # | Fix | Status |
|---|---|---|
| MC-1 | Beta/Preview status dropdown options + badge colors | ✅ |
| MC-1b | Beta/Preview lifecycle transitions (Draft→Preview→Beta→Active→Inactive⇄Active, Active→Deprecated, fwd-jumps) | ✅ |
| MC-2 | dup service idempotent seed + dedup migration (flagged dup was pre-soft-deleted; no live violation) | ✅ |
| MC-3a | lookup excludes infra umbrellas (auth/platform/mdm); free-tag lowercase-normalize+slug-validate | ✅ |
| MC-3b pilot | workflow self-registers into catalog (in-process worker); idempotent; auth-sync | ✅ |
| MC-3b-fix | workflow manifest mirrors FRONTEND (9 pages incl. Designer/VisualDesigner/Versions); START→DEFINITIONS; bidirectional completeness test | ✅ |
| MC-4 | Origin (Manual/SelfRegistered) + badge + delete-guard(409) + Module Code dropdown→free-text input; obsolete permission-modules lookup removed | ✅ |
| MC-5 | Permissions tab wired (was placeholder) → read-only aggregated permission list | ✅ |
| MC-6 | reconcile AUTHORITATIVE prune (soft-delete orphaned pages/actions on manifest change; module-scoped; idempotent) | ✅ |
| MC-7 | self-registered module page/action mutations blocked (409) — extends MC-4 HARD guard to descriptors (else manual adds get pruned by MC-6); Details Add/Edit/Delete hidden + read-only | ✅ |
| DS-1 | Domain+Service Management: auto-derive Code from Display Name | ✅ |

**Governance (so future modules auto-get manifests):** new `.antigravity/rules/module-self-registration-standard.md` (clean-slug, mirror-frontend, bidirectional completeness, authoritative-prune, HARD/SOFT, lifecycle) + `add-module.md`/`add-page.md` BLOCKER steps. See [[project-module-identity-model]] + [[project-module-self-registration]].

## Remaining
- ✅ **MC-3b-expand DONE (live-verified):** Part A (organization 3p, document-management 7p, reference-data 13p — Platform in-process) + Part B (legal-entity 4p — MDM HTTP-push, new MDM registration infra). Catalog now FULLY self-registered: **6 modules** (workflow, organization, document-management, reference-data, goldenslim, legal-entity), all Origin=SelfRegistered, stats total=6, idempotent, prune-clean, auth-synced. Standard §2a tab/wizard≠page applied (legal-entity wizard = 1 page/route). Decision A baked into standard; B is additive-later (no regression). WorkCenter = cross-module aggregator (§2b), Phase B design.
- **MOD-0023 functional finding (other dev):** instances LIST API (`GetInstances`) takes no definitionId → `/Definitions/{id}/Instances` shows ALL instances, not definition-scoped. Workflow module's own logic, not catalog/self-reg. Log for MOD-0023 owner.
- Login `?tenantId=` dev-fallback (🟡); MOD-0285 nav management UI (🟢); domain typo "Platform Shared Servicec" (🟢, operator-editable).

## Status
Catalog/self-reg hardening done (10 fixes, all live-verified). Branch now very large + uncommitted (MOD-0285+0220+merge+A1+A2+A3+A3-ref+MC-1..6+DS-1+governance). Next: MC-3b-expand OR commit.
