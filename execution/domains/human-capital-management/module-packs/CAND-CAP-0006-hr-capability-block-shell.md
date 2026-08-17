---
id: CAND-CAP-0006
name: HR Capability Block Shell
domain: human-capital-management
service: Diten.HumanCapitalService
shell: none
golden_reference: none
entity_base: BaseEntity
status: approved
owner: enterprise-architect / hcm-domain-owner / platform-team
branch: feature/hcm/cand-cap-0006-hr-capability-block-shell
started: 2026-08-10
target: 2026-08-31
form_field_count: 0
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
candidate_identity: CAND-CAP-0006
legacy_excel_id: MOD-0297
canonicalization_status: candidate / pending-EA
runtime_service_candidate: Diten.HumanCapitalService
bootstrap_type: governance-only
---

# CAND-CAP-0006 - HR Capability Block Shell

> Status: approved. Approved for governance continuation only; not
> ready-for-dev; no runtime implementation authorized. This native HCM module
> pack authorizes shell boundary and R1 sequencing governance only. It does not
> authorize production implementation, runtime service scaffolding, frontend,
> gateway routing, database collections, or orchestration for development.

## 1. Module Summary

`CAND-CAP-0006` reserves the temporary DCP-002 candidate identity for the
native HCM workspace shell and capability-boundary planning layer named
`HR Capability Block Shell`.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`,
where the Excel row arrived as `MOD-0297`. `MOD-0297` must not be used for
HR/HCM because it is already retained in the registry as a deprecated platform
alias for Tenant Subscription Management / `CAND-CAP-0002`.

This pack prepares the HCM domain shell boundary, future navigation grouping,
and first R1 foundation sequencing only. EA has approved candidate-based
governance continuation for `CAND-CAP-0006`; this is not an EA canonical MOD
allocation and is not runtime approval. The pack does not own employee master,
employment records, HR lifecycle behavior, or any runtime surface.

## 2. Ownership and Boundaries

Owned by this pack:

- Native HCM workspace shell governance and capability boundary.
- HR/HCM navigation grouping policy for future approved slices.
- HCM domain ownership boundary against Platform Shared Services and TEP.
- First R1 foundation sequencing guidance for future HCM module packs.
- Planning namespace for future HCM shell permissions, without runtime seeding.

Not owned by this pack:

- Employee profile, employment master, employment lifecycle, or employment
  record system-of-record behavior.
- HRIS, payroll, time-attendance, or payroll integration provider ownership.
- Organization, person, or position reference-directory ownership.
- TEP candidate, talent marketplace, succession, learning, or talent identity.
- Runtime service scaffold, API routes, Mongo collections, frontend menus, or
  gateway routes.

## 3. Owned Objects

Governance-only planning objects:

| Object | Type | Purpose |
|---|---|---|
| HcmWorkspaceShellContract | Governance contract | Defines the future HCM workspace shell boundary. |
| HcmNavigationBoundary | Governance contract | Defines which future HCM modules may appear in HCM navigation. |
| HcmR1FoundationGrouping | Governance plan | Orders first HCM foundation module packs after EA identity decisions. |
| HcmShellPermissionNamespace | Governance plan | Reserves a future permission prefix decision without runtime literals. |

No runtime entity, DTO, controller, repository, index, migration, or UI object is
authorized by this governance-approved pack.

## 4. Entity Fields

No runtime entity fields are authorized.

If this pack is later split into an implementation-ready runtime pack, entity
fields must be redefined there and must not use `CAND-CAP-0006` as a runtime
module literal. This governance-approved pack creates no Mongo collection and no
persistence contract.

## 5. Repo Scope

Authorized scope for this governance-approved pack:

- `execution/domains/human-capital-management/module-packs/CAND-CAP-0006-hr-capability-block-shell.md`

Runtime scope is explicitly closed. The candidate runtime service
`Diten.HumanCapitalService` is recorded for governance planning only and is not
scaffolded by this pack.

Future runtime scopes, if separately approved, must be authorized by a later
`approved` or `ready-for-dev` module pack and may include only explicitly listed
paths such as:

- `services/Diten.HumanCapitalService/**` after service scaffold approval.
- `frontend/Diten.Web/**` only after a UI shell slice is explicitly approved.
- `gateway/Diten.ApiGateway/**` only through the integration-agent ownership
  workflow.

## 6. Protected Paths

This governance-approved pack must not change:

- `services/**`
- `frontend/**`
- `gateway/**`
- `execution/domains/platform-shared-services/**`
- `execution/domains/talent-ecosystem-platform/**`
- `execution/domains/human-capital-management/domain-config.md`
- `execution/registries/module-id-registry.md`, except a separate EA-approved
  identity reservation or canonicalization update.
- `.antigravity/**`

## 7. Dependencies

Governance dependencies:

- `AGENTS.md`
- `execution/domains/human-capital-management/domain-config.md`
- `execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`
- `execution/registries/module-id-registry.md`
- `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`

PSS backbone dependencies referenced as substrate only:

- `MOD-0251` - HRIS (Workday/SuccessFactors/Oracle HCM) [External SoR]
- `MOD-0279` - Payroll Engine [External SoR]
- `MOD-0280` - Time & Attendance (UKG/Kronos) [External Provider]
- `MOD-0281` - Payroll Integration & Governance
- `MOD-0288-FU02` - Person Reference Directory Projection

R0 shared-control dependencies to verify before runtime HCM delivery:

- `MOD-0018` - RBAC / ABAC Authorization
- `MOD-0021` - Audit Trail Service
- `MOD-0030` - Records / Retention / Legal Hold
- `MOD-0023` - Workflow Designer (Approvals/SLAs/Escalations)
- `MOD-0028` - Document Management System
- `MOD-0031` - Evidence Linking
- `MOD-0048` - Reference Data Management
- `MOD-0057` - Taxonomy / Tagging Service

## 8. Runtime Constraints

- Runtime implementation is closed while status is `approved`.
- `CAND-CAP-0006` is a governance identity only and must never be written into
  runtime literals, permission seeds, Hangfire owner names, route metadata,
  database records, or telemetry owner fields.
- `MOD-0297` must not be used for this HCM capability.
- No production service scaffold is authorized.
- No API, controller, command, validator, repository, index, database collection,
  frontend menu, Razor view, JavaScript, DataTable, RESX, or gateway route is
  authorized.
- PSS backbone modules remain dependencies/substrate and do not transfer HR/HCM
  ownership into `platform-shared-services`.

## 9. Layout & Shell Contract

Current shell decision:

- `shell: none`
- `golden_reference: none`
- UI/frontend: N/A

Future HCM workspace shell UI must be authorized by a separate implementation
pack after EA canonicalization and service/shell decisions. This pack only
defines that the HCM workspace shell must not collapse into PSS and must not
bootstrap TEP identity directly from PSS person projection.

## 10. Backend File Convention

Backend implementation is N/A for this draft pack.

If a future runtime pack is approved, it must follow the repo's 5-layer .NET
service convention, CQRS/MediatR patterns, `Response<T>` envelope, tenant
server-side resolution, soft delete rules, and permission conventions defined by
the then-authoritative module pack and domain config.

## 11. Frontend File Contract

Frontend implementation is N/A for this draft pack.

No Razor view, JavaScript file, DataTable contract, menu item, localization
resource, layout binding, or frontend route may be added from this pack.

## 12. Validation Rules

Planning validation rules:

- DCP-002 candidate gate for `CAND-CAP-0006` and `HR Capability Block Shell`
  must pass before this pack is used for any governance decision.
- Any future runtime pack must fail closed if it attempts to use `MOD-0297` as
  the HCM identity.
- Any future runtime pack must fail closed if it writes `CAND-CAP-0006` into
  runtime literals.
- Future HCM foundation module packs must declare whether they depend on
  `MOD-0288-FU02`, `MOD-0251`, or other PSS substrate objects and must validate
  same-tenant references where applicable.
- Employee/employment ownership cannot be inferred from this shell pack.

## 13. Failure Path to Verify

Before promoting this pack or preparing a runtime follow-up, verify these failure
paths remain blocked:

- Reusing `MOD-0297` for HR/HCM.
- Treating `CAND-CAP-0006` as a runtime module ID.
- Creating `services/Diten.HumanCapitalService/**` without explicit scaffold
  approval.
- Adding HCM frontend menus or shell UI from this governance pack.
- Adding gateway routes directly.
- Claiming employee/employment master ownership in the shell pack.
- Building TEP candidate/talent identity directly on raw HRIS or PSS person
  projection without HCM contracts.

## 14. Authorization Convention

No runtime permission is authorized.

Planning-only future namespace candidate:

- `hcm.shell.*`

This namespace is not a seed, not a route permission, and not a runtime literal
until a future approved module pack explicitly authorizes it.

## 15. Gateway / API Routing Decision

Gateway/API routing is N/A for this draft pack.

No controller route and no Ocelot route may be created. If a future HCM runtime
or UI shell requires gateway exposure, that route must be handled only by the
integration-agent workflow after the relevant runtime pack is approved.

## 16. Acceptance Criteria

- AC-01: DCP-002 candidate gate passes for
  `CAND-CAP-0006` / `HR Capability Block Shell`.
- AC-02: Pack status is `approved` for governance continuation only and is not
  `ready-for-dev`.
- AC-03: The pack explicitly blocks `MOD-0297` for HR/HCM use.
- AC-04: The pack states that `CAND-CAP-0006` is governance-only and not a
  runtime literal.
- AC-05: Runtime implementation, service scaffold, API, frontend, gateway, and
  database work are closed.
- AC-06: PSS backbone modules are listed only as dependencies/substrate.
- AC-07: HCM ownership is not moved into PSS.
- AC-08: TEP ownership is not created or moved into HCM by this shell pack.
- AC-09: Employee/employment master ownership is explicitly out of scope.
- AC-10: The module pack includes all 20 required sections.

## 17. Test Expectations

Required governance check:

```bash
python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0006 --name "HR Capability Block Shell"
```

Expected result:

```text
OK  candidate CAND-CAP-0006: temporary governance identity, pending EA, not Blueprint-backed, not in runtime.
```

Build, unit tests, API tests, UI verifier, DataTable verifier, and localization
verifier are N/A because no runtime or frontend implementation is authorized.

## 18. Governance Approval Checklist

- [x] HCM governance-only domain exists.
- [x] `CAND-CAP-0006` candidate reservation exists in the registry.
- [x] DCP-002 candidate gate passes.
- [x] `MOD-0297` is documented as blocked for HR/HCM use.
- [x] PSS backbone dependencies are listed as substrate.
- [x] Runtime implementation is closed.
- [x] UI/frontend/gateway work is closed.
- [x] EA explicitly approves candidate-based governance continuation for
  `CAND-CAP-0006`.
- [x] User approves this pack for governance-only continuation.
- [x] Planning-only permission namespace remains `hcm.shell.*` and is not a
  runtime seed, route permission, or runtime literal.
- [ ] Runtime service scaffold decision is made separately.
- [ ] Runtime repo scope is approved separately.
- [ ] HCM shell UI decision is made separately.
- [ ] First R1 foundation runtime module pack is prepared after identity
  decisions.
- [ ] Privacy/security review confirms employee/employment ownership remains
  outside this shell pack.

## 19. Implementation Notes

Do not call `@orchestrator` for production implementation from this pack. The
`approved` status means governance continuation only; it does not mean
ready-for-dev.

The intended sequencing is:

1. Keep `CAND-CAP-0006` as the temporary governance identity for the HCM shell.
2. Resolve EA canonicalization for the first native HCM foundation identities.
3. Prepare focused module packs for actual HCM foundation runtime slices.
4. Treat `Employee Profile & Employment Record Projection` as the first real
   runtime module-pack candidate, pending EA identity decision. It must not use
   the blocked legacy Excel ID `MOD-0298`.
5. Only then consider service scaffold, backend/API, UI shell, and gateway
   decisions through explicit approved packs.

This shell pack should remain thin. It is a boundary and sequencing contract,
not an employee master or workspace implementation contract.

## 20. Follow-up Items

Open runtime blockers:

- EA canonical MOD assignment for `HR Capability Block Shell` is still pending.
- `CAND-CAP-0006` is not Blueprint-backed and cannot be a runtime literal.
- Runtime service scaffold authorization for `Diten.HumanCapitalService` is
  pending.
- Runtime repo scope is pending.
- HCM shell UI/golden-reference decision is not authorized.
- Permission namespace and seed policy are not authorized.
- First runtime module identity is pending. The first real runtime module-pack
  candidate is `Employee Profile & Employment Record Projection`, but it needs
  EA canonical MOD or CAND-CAP decision and must not use blocked `MOD-0298`.

Waivers:

- No runtime waiver is granted by this draft pack.
- PSS backbone dependency readiness may be used only as substrate evidence, not
  as HCM ownership transfer.

Recommended next safe step:

- Prepare EA identity decision for `Employee Profile & Employment Record
  Projection`, then prepare its module pack only after the DCP-002 candidate or
  canonical MOD gate passes.
