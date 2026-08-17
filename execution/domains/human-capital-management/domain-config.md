# Human Capital Management — Domain Config

> Governance-only bootstrap. This file defines ownership boundaries and
> planning rules for native HR/HCM delivery. It does not authorize production
> service scaffolding, runtime code, API routes, frontend pages, gateway
> changes, database collections, or module implementation.

## Purpose

The Human Capital Management (HCM) domain owns native HR/HCM application
capabilities after EA identity decisions are recorded. Its scope starts with
the HCM workspace shell and governed HR projections, then expands to HR
lifecycle, offboarding, compliance, employee relations, workforce planning, and
HR-facing analytics facades as defined by DCP-008.

## Bootstrap Decisions

| Decision | Value |
|---|---|
| Domain slug | `human-capital-management` |
| Branch short code | `hcm` |
| Runtime service candidate | `Diten.HumanCapitalService` |
| Bootstrap type | Governance-only |
| Production service scaffold | Not authorized |
| Native module packs | Not created by this bootstrap |

## Authority

- `AGENTS.md`
- `execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`
- `execution/portfolio/delivery-capability-packs/DCP-006-org-directory-expansion-governance.md`
- `execution/registries/module-id-registry.md`
- `execution/domains/platform-shared-services/domain-config.md`
- `execution/domains/platform-shared-services/module-packs/MOD-0288-FU02-person-reference-directory-projection.md`

## In-Scope Planning Areas

- HR/HCM workspace shell and navigation ownership.
- HRIS-sourced employee / employment projection governance after EA identity
  approval.
- HR-sensitive visibility, access, and data-scope rules.
- Offboarding and TEP handoff payload governance.
- Future R3 HCM lifecycle modules from DCP-008 after dedicated module packs.
- Consumption of PSS backbone contracts without copying PSS ownership.

## Explicit Out-of-Scope

- Production implementation.
- `services/Diten.HumanCapitalService/**` scaffold.
- API controllers, frontend views, JavaScript, DataTables, RESX, menus, gateway
  routes, or database collections.
- External HRIS/payroll/time provider ownership.
- Raw HRIS payload, credentials, tokens, secrets, bank/tax/payroll/payslip,
  biometric/geolocation data ownership.
- TEP candidate/talent identity ownership.
- Reusing `MOD-0297`, `MOD-0298`, or `MOD-0299` as HR/HCM identities while
  registry collisions remain open.

## PSS Dependencies / Substrate

HCM consumes these PSS capabilities as dependencies only:

- `MOD-0251` HRIS External SoR.
- `MOD-0279` Payroll Engine External SoR.
- `MOD-0280` Time & Attendance External Provider.
- `MOD-0281` Payroll Integration & Governance.
- `MOD-0288-FU02` Person Reference Directory Projection.

PSS remains the owner of those substrate modules. HR/HCM must not move those
ownership boundaries into this domain without a separate EA decision.

## Canonicalization Blockers

`MOD-0297`, `MOD-0298`, and `MOD-0299` must not be used for HR/HCM runtime
identity or module-pack identity until the registry is reconciled:

- `MOD-0297` is a deprecated platform alias for Tenant Subscription Management
  / `CAND-CAP-0002`.
- `MOD-0298` is a deprecated platform alias for Tenant Module Entitlements /
  `CAND-CAP-0002-FU05`.
- `MOD-0299` is a deprecated platform alias for SaaS Billing & Invoicing /
  `CAND-CAP-0005`.

## CAND-CAP Required — EA Decision

The following DCP-008 HR/HCM native rows are absent from Blueprint or otherwise
blocked and require EA decision before module-pack creation:

- R1: `MOD-0297`, `MOD-0298`, `MOD-0299`, `MOD-0314`, `MOD-0305`.
- R3: `MOD-0300`, `MOD-0301`, `MOD-0302`, `MOD-0303`, `MOD-0304`,
  `MOD-0306`, `MOD-0307`, `MOD-0308`, `MOD-0309`, `MOD-0320`,
  `MOD-0310`, `MOD-0311`, `MOD-0312`, `MOD-0313`, `MOD-0315`,
  `MOD-0316`, `MOD-0319`, `MOD-0317`, `MOD-0318`.

## Module-Pack Preparation Gate

Before any native HCM module pack:

1. Run DCP-002 preflight for the proposed canonical identity or candidate.
2. Record EA decision for missing Blueprint IDs and collision resolution.
3. Confirm this domain remains governance owner and PSS remains substrate owner.
4. Keep runtime service candidate as candidate-only unless explicitly approved.
5. Start with R1 HCM foundation before R3 completion modules.

## First Module-Pack Candidates

After EA identity approval, the safe first HCM module-pack candidate is:

1. HR Capability Block Shell.

Next candidates:

2. Employee Profile & Employment Record Projection.
3. HR Governance & Sensitive Access Controls.
4. Offboarding & Exit Management.

`Position & Organization Assignment` must not use `MOD-0299`; it must wait for
EA identity resolution and must consume MOD-0288 / MOD-0288-FU02 rather than
duplicating directory ownership.

## Protected Paths

- `.antigravity/**`
- `services/**`
- `frontend/**`
- `gateway/**`
- `execution/domains/platform-shared-services/**`
- `execution/domains/master-data-management/**`
- `execution/domains/developer-enablement/**`
- `execution/registries/module-id-registry.md` unless a registry-specific EA
  update is explicitly requested.

## Runtime Decisions

No runtime decision is finalized by this bootstrap. If the candidate service is
later approved, all module packs must restate repo scope, protected paths,
tenant isolation, soft delete, authorization, response envelope, persistence,
gateway ownership, and test expectations before implementation.
