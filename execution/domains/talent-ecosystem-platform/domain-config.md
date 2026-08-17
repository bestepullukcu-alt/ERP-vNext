# Talent Ecosystem Platform — Domain Config

> Governance-only bootstrap. This file defines ownership boundaries and
> planning rules for native TEP delivery. It does not authorize production
> service scaffolding, runtime code, API routes, frontend pages, gateway
> changes, database collections, or module implementation.

## Purpose

The Talent Ecosystem Platform (TEP) domain owns native cross-company talent,
reference-network, consent/visibility, association, candidate, reputation,
risk, and sector intelligence capabilities after EA identity and legal/privacy
decisions are recorded.

## Bootstrap Decisions

| Decision | Value |
|---|---|
| Domain slug | `talent-ecosystem-platform` |
| Branch short code | `tep` |
| Runtime service candidate | `Diten.TalentEcosystemService` |
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
- `execution/domains/human-capital-management/domain-config.md`

## In-Scope Planning Areas

- TEP workspace shell and navigation ownership.
- Association membership and member-company registry governance.
- Verified HR participant / company access.
- Consent, visibility, access policy, and review board governance.
- Industry candidate identity and talent profile after HCM/consent contracts.
- Reference exchange, exit reference record registry, rehire recommendation, and
  candidate response/dispute management after legal/privacy approval.
- R4 risk, reputation, talent pool, skill passport, benchmarking, and sector
  intelligence capabilities after R2 MVP and legal gates.

## Explicit Out-of-Scope

- Production implementation.
- `services/Diten.TalentEcosystemService/**` scaffold.
- API controllers, frontend views, JavaScript, DataTables, RESX, menus, gateway
  routes, or database collections.
- Raw HRIS, payroll, time/attendance, or provider payload ownership.
- Direct candidate/talent identity creation from PSS person reference projection
  without HCM foundation and consent/visibility contracts.
- HCM employee/employment master ownership.
- Payroll/time/attendance ownership.
- Provider adapters, background sync, credentials, tokens, secrets, bank/tax,
  payslip, biometric/geolocation, or PII-heavy HRIS payload storage.

## PSS and HCM Dependencies

TEP consumes these PSS capabilities as dependencies only:

- `MOD-0251` HRIS External SoR.
- `MOD-0279` Payroll Engine External SoR.
- `MOD-0280` Time & Attendance External Provider.
- `MOD-0281` Payroll Integration & Governance.
- `MOD-0288-FU02` Person Reference Directory Projection.

TEP must consume HCM foundation/contracts before native candidate/talent
identity work. TEP must not bypass HCM by building directly on raw HRIS or on
PSS person reference projection alone.

## CAND-CAP Required — EA Decision

The following DCP-008 TEP native rows are absent from Blueprint and require EA
decision before module-pack creation:

- R2: `MOD-0321`, `MOD-0323`, `MOD-0324`, `MOD-0325`, `MOD-0326`,
  `MOD-0330`, `MOD-0322`, `MOD-0327`, `MOD-0328`, `MOD-0329`,
  `MOD-0331`.
- R4: `MOD-0332`, `MOD-0333`, `MOD-0334`, `MOD-0335`, `MOD-0336`,
  `MOD-0337`, `MOD-0338`, `MOD-0339`, `MOD-0340`, `MOD-0341`,
  `MOD-0342`, `MOD-0343`, `MOD-0344`, `MOD-0345`, `MOD-0346`,
  `MOD-0347`, `MOD-0348`, `MOD-0349`, `MOD-0350`, `MOD-0351`.

## Module-Pack Preparation Gate

Before any native TEP module pack:

1. Run DCP-002 preflight for the proposed canonical identity or candidate.
2. Record EA decision for missing Blueprint IDs.
3. Confirm HCM foundation/contracts exist for any TEP module that consumes HR
   participant, employee, offboarding, or handoff data.
4. Confirm legal/privacy decisions for consent, reference exchange, reputation,
   restricted integrity, and risk modules.
5. Keep runtime service candidate as candidate-only unless explicitly approved.
6. Start with R2 MVP shell/policy modules before R4 intelligence modules.

## First Module-Pack Candidates

After EA identity approval and HCM foundation readiness, the safe first TEP
module-pack candidate is:

1. Talent Ecosystem Platform Shell.

Next candidates:

2. Consent, Visibility & Access Policy.
3. Association Membership & Member Company Registry.
4. Verified HR Participant & Company Access.
5. Industry Candidate Identity & Talent Profile.

## Protected Paths

- `.antigravity/**`
- `services/**`
- `frontend/**`
- `gateway/**`
- `execution/domains/platform-shared-services/**`
- `execution/domains/human-capital-management/**`
- `execution/domains/master-data-management/**`
- `execution/domains/developer-enablement/**`
- `execution/registries/module-id-registry.md` unless a registry-specific EA
  update is explicitly requested.

## Runtime Decisions

No runtime decision is finalized by this bootstrap. If the candidate service is
later approved, all module packs must restate repo scope, protected paths,
tenant isolation, soft delete, authorization, response envelope, persistence,
gateway ownership, privacy/legal gates, and test expectations before
implementation.
