---
id: MOD-0352
name: Enterprise Strategy Management
domain: enterprise-strategy-business-performance
service: Diten.EnterpriseStrategyService
shell: none
golden_reference: none
entity_base: BaseEntity
entity_base_namespace: Diten.Domain.Common
status: ready-for-dev
owner: ali.tufanoglu / enterprise-architect
branch: feature/esbp/mod-0352-slice3-contract-reconciliation
started: 2026-08-28
target: 2026-08-28
form_field_count: 0
delivery_capability_pack: DCP-005
production_authority: none
execution_activation: none
authorized_slice: strategy-link-contract-reconciliation
---

# MOD-0352 — Enterprise Strategy Management

> **Bounded amendment — APPLICATION CONTRACT / READY-FOR-DEV / NON-ACTIVATED / NON-MIGRATING
> (2026-08-28):** implementation authority is limited to the exact application service and two test files
> listed in §5. It reconciles planning-cycle fixtures, Initiative identity/cardinality and trusted PPM
> Project link-only validation. It grants no persistence, API, frontend, Gateway, WorkCenter, migration,
> deployment or production activation authority.

> **WorkCenter onboarding amendment — DRAFT / NON-EXECUTABLE / DEFAULT-OFF (2026-08-28):** the DCP-004
> field/capability/action/permission handoff is recorded in
> `../workcenter-provider-onboarding-evidence.md`. Runtime provider/API/frontend configuration remains blocked:
> existing Strategy records have no authoritative `TenantId`, and the approved work explicitly excludes schema
> or data migration. The existing `TENANT_SAFETY_NOT_READY` 503 boundary must not be bypassed.

## 1. Module Summary

MOD-0352 owns Enterprise Strategy goal, objective, cascade, planning-cycle and strategy-alignment semantics.
This bounded Slice 3 closes three verified contract regressions without treating legacy code as canonical:
the canonical `Annual Plan` planning-cycle value, InitiativeId-scoped link identity with one objective able to
reference multiple initiatives, and a trusted PPM Project link-only projection that does not require foreign
PPM lifecycle fields to be recopied into Strategy.

## 2. Ownership and Boundaries

In scope: Strategy-owned goal/objective anchors, Initiative/Project strategy links, trusted PPM projection
validation and existing tests. Out of scope: PPM lifecycle ownership, portfolio/project CRUD, approval,
budget, task, workflow, evidence payload, migration, UI and WorkCenter behavior. MOD-0117 remains the
Initiative/Project SoR; MOD-0352 stores only Strategy-owned alignment.

## 3. Owned Objects

This slice changes no entity schema. It owns only existing `InitiativeStrategyLinkAggregate`,
`ProjectStrategyLinkAggregate`, their Strategy-owned parent references, existing orchestration validation
profiles and the exact tests that prove the boundary. `InitiativeId` is the natural Initiative-link identity;
`ParentObjectiveId` is not unique.

## 4. Entity Fields

No field is added or removed. Existing Strategy link fields retain their current types. A trusted PPM Project
projection is identified by exact ProjectId and existing source provenance. Link-only validation may consume
the trusted PPM projection but must not synthesize owner, scope, methodology, priority, dates or budget.

## 5. Repo Scope

Governance authority is exact:

- `execution/domains/enterprise-strategy-business-performance/module-packs/MOD-0352-enterprise-strategy-management.md`
- `execution/domains/enterprise-strategy-business-performance/module-packs/README.md`
- `execution/portfolio/delivery-capability-packs/DCP-005-management-governance-core.md`
- `execution/domains/enterprise-strategy-business-performance/workcenter-provider-onboarding-evidence.md`

Implementation authority is exact:

- `services/Diten.EnterpriseStrategyService/src/Diten.EnterpriseStrategy.Application/EnterpriseStrategy/Services/EnterpriseStrategyOrchestrationServices.cs`
- `services/Diten.EnterpriseStrategyService/tests/Diten.Application.Tests/UnitTest1.cs`
- `services/Diten.EnterpriseStrategyService/tests/Diten.EnterpriseStrategy.EndToEnd.Tests/EnterpriseStrategyLineageE2ETests.cs`

## 6. Protected Paths

- `.antigravity/**`
- `gateway/**`
- `frontend/**`
- `services/Diten.EnterpriseStrategyService/src/**` outside the exact application file in §5
- every persistence, infrastructure, API, configuration and migration path
- every other domain's governance and service paths
- legacy route deletion or retirement

## 7. Dependencies

- DCP-005 Slice 3 ownership and ordering
- MOD-0117 as authoritative Initiative/Project lifecycle owner
- existing `IPpmInitiativeReadAdapter` / `IPpmProjectReadAdapter` and caches
- existing Strategy goal/objective repositories
- CAND-CAP-0007-FU01 default-deny perimeter, which remains unchanged

## 8. Runtime Constraints

This is an application-contract-only local slice. It adds no listener, endpoint, route, database write model,
host activation, credential, secret, configuration or production authority. Trusted PPM link-only behavior is
available only through the existing orchestration call graph and existing test composition.

## 9. Layout & Shell Contract

`shell: none`. No frontend or Razor surface is authorized.

## 10. Backend File Convention

No new feature tree or public API is created. The existing orchestration service receives a private,
explicit validation profile: full local lifecycle versus trusted PPM link-only projection. Create, Update and
status-changing paths remain full-lifecycle strict; only strategy-link upsert using an exact trusted PPM
projection may use link-only validation.

## 11. Frontend File Contract

No frontend file, route, view, JavaScript, localization or DataTable artifact is authorized.

## 12. Validation Rules

1. Planning cycle type is exact ordinal `Annual Plan`; the stale `Annual` fixture is invalid.
2. Initiative link uniqueness is exact `InitiativeId`; multiple initiatives may reference one objective.
3. Link ProjectId must exactly match the trusted PPM projection/cache identity.
4. Trusted link-only validation preserves source provenance and validates Strategy anchors, dates, lineage,
   templates, budgets already present and optimistic concurrency.
5. Link-only validation skips only PPM-owned full-local-lifecycle required fields.
6. Local Project create/update/status paths and untrusted/cache-missing link attempts remain full strict and
   fail closed.

## 13. Failure Path to Verify

- stale `Annual` planning type is rejected;
- a duplicate exact InitiativeId conflicts, but a different InitiativeId under the same objective succeeds;
- missing/mismatched PPM Project evidence does not enter link-only mode;
- local non-draft Project data missing owner/scope/methodology/priority/dates/budget remains invalid;
- stale version, invalid lineage, incompatible template and budget constraints remain rejected;
- link-only mode never invents or copies absent PPM-owned fields.

## 14. Authorization Convention

No permission, role, grant, token or authentication behavior changes. Existing default-deny ES permission
perimeter remains authoritative. This pack grants no anonymous or production activation path.

## 15. Gateway / API Routing Decision

No Gateway or API route change is required or authorized.

## 16. Acceptance Criteria

- [ ] Canonical MOD-0352 identity preflight passes.
- [ ] Only the three implementation files in §5 change after governance checkpoint.
- [ ] Planning-cycle tests use and accept exact `Annual Plan`.
- [ ] Exact InitiativeId duplicate is rejected and two distinct initiatives may share one objective.
- [ ] Trusted PPM Project link-only lineage completes without synthetic PPM lifecycle fields.
- [ ] Local Project create/update/status validation remains full-lifecycle strict.
- [ ] Existing security, strategy and lineage regression suites pass.
- [ ] Non-equivalent mutations for the three decisions produce expected-red evidence and are restored.
- [ ] Build and `git diff --check` pass with no frontend/Gateway/WorkCenter diff.

## 17. Test Expectations

Run the Enterprise Strategy solution build, full `Diten.Application.Tests` and full
`Diten.EnterpriseStrategy.EndToEnd.Tests`. Targeted tests must cover canonical planning type, InitiativeId
identity/one-to-many objective cardinality, trusted PPM link-only acceptance and full-lifecycle rejection.
Physically mutate each new boundary, prove a targeted red test, restore byte-identically and rerun final green
gates. No external Mongo is required by this exact slice.

## 18. Ready-for-dev Checklist

- [x] MOD-0352 exists in Blueprint 8.1 and registry with exact canonical name.
- [x] DCP-005 Slice 3 separate 1.1 scope decision is recorded by this bounded pack.
- [x] Owner boundaries with MOD-0117 are explicit.
- [x] Exact three-file implementation allowlist is fixed.
- [x] User explicitly approved bounded governance, implementation, tests and local checkpoint commits.
- [x] Production, migration, frontend, Gateway, WorkCenter, push and merge authority remain absent.

## 19. Implementation Notes

Legacy behavior is evidence, not authority. `ProjectSaveProfile.FullLifecycle` remains the default. The
trusted-link profile is selected only after exact PPM projection resolution for the same ProjectId; local
repository presence alone is insufficient. Source provenance remains PPM-owned and Strategy stores only its
alignment link. The legacy parity ledger remains open and this slice does not authorize retirement.

## 20. Follow-up Items

- Complete field-by-field legacy Initiative/Project parity and browser acceptance.
- Deliver authenticated tenant-safe HTTP/browser evidence under separate approved UI/API scope.
- Keep production activation, migration and WorkCenter connection behind their own gates.
- Do not claim full 1.1 completion from this bounded contract reconciliation.
