---
id: CAND-CAP-0012
name: Association Membership & Member Company Registry
domain: talent-ecosystem-platform
service: Diten.TalentEcosystemService
shell: none
golden_reference: none
entity_base: BaseEntity
status: ready-for-dev
owner: enterprise-architect / tep-domain-owner / platform-team / security-owner / legal-owner
branch: feature/tep/cand-cap-0012-association-membership-member-company-registry
started: 2026-08-13
target: 2026-09-30
form_field_count: 0
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
runtime_authorization_source: execution/portfolio/delivery-capability-packs/DCP-010-tep-runtime-authorization.md
shell_dependency: execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0011-talent-ecosystem-platform-shell.md
consent_visibility_dependency: execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0013-consent-visibility-access-policy.md
candidate_identity: CAND-CAP-0012
legacy_excel_id: MOD-0323
canonicalization_status: candidate / pending-EA
runtime_service_candidate: Diten.TalentEcosystemService
bootstrap_type: runtime-authorized-first-slice
runtime_owner_key: tep.association-memberships
permission_namespace: tep.association-memberships.*
---

# CAND-CAP-0012 - Association Membership & Member Company Registry

> Status: ready-for-dev. Approved for the first TEP backend/API metadata
> association/member company registry contract slice only. This pack does not
> authorize candidate/talent identity, broad consent engine, reference exchange,
> reputation/risk/analytics, service scaffold, frontend, Gateway, HCM/PSS
> implementation changes, or verified-company runtime integration.
> `CAND-CAP-0012` remains a governance/documentation identity only and must
> never be written into runtime literals. `MOD-0323` remains blocked for TEP
> use.

## 1. Module Summary

`CAND-CAP-0012` reserves the temporary DCP-002 candidate identity for the native
Talent Ecosystem Platform R2-A capability named `Association Membership & Member
Company Registry`.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`.
DCP-008 places this row immediately after the TEP Shell in R2-A as legacy Excel
ID `MOD-0323`. DCP-002 blocks `MOD-0323` because it is absent from the
Blueprint and has no canonical registry row. EA/registry owner approval reserved
`CAND-CAP-0012` as a temporary governance identity pending future canonical MOD
allocation.

This ready-for-dev pack defines the association membership and member company registry
governance boundary after `CAND-CAP-0011` TEP Shell is done. It does not create
runtime association records, candidate/talent identities, consent engine,
reference exchange marketplace, external company verification runtime, or
frontend/Gateway surface.

## 2. Ownership and Boundaries

Owned by this ready-for-dev pack:

- TEP-native association membership governance boundary.
- TEP-native member company registry governance boundary.
- R2-A sequencing after `CAND-CAP-0011` TEP Shell.
- Dependency mapping to completed HCM foundation contracts.
- Runtime-ready contract for privacy/legal metadata, consent/visibility policy
  preconditions, and local/deferred verified-company access metadata.

Not owned by this pack:

- Candidate/talent identity, talent profile, candidate account, or sector talent
  profile ownership.
- Consent engine, privacy/legal policy engine, visibility policy engine, review
  board, trust engine, reference exchange, rehire recommendation, dispute
  management, reputation, risk, analytics, or marketplace runtime behavior.
- HCM employee/employment projection ownership.
- HCM sensitive access ownership.
- HCM position/org assignment ownership.
- HCM offboarding ownership or TEP handoff execution.
- PSS person/org/position directory ownership or external provider ownership.
- Frontend menus, Razor views, JavaScript, DataTables, RESX, Gateway routes,
  provider adapters, webhooks, background jobs, database collections, or
  raw/sensitive payload persistence.

## 3. Owned Objects

Governance-only planning objects:

| Object | Type | Purpose |
|---|---|---|
| TepAssociationMembershipBoundary | Governance contract | Defines the native TEP boundary for association membership planning. |
| TepMemberCompanyRegistryBoundary | Governance contract | Defines the native TEP boundary for member company registry planning. |
| TepAssociationSequencingPlan | Governance plan | Records that TEP Shell is completed and that Association is an R2-A follow-up. |
| TepAssociationRuntimeBlockerLog | Governance plan | Tracks privacy/legal/consent/visibility blockers before runtime-ready promotion. |
| TepAssociationDependencyContractMap | Governance plan | Maps HCM foundation and PSS substrate dependencies without ownership transfer. |

Runtime objects authorized by this ready-for-dev pack are limited to the first
metadata association/member company registry backend/API contract slice in
`services/Diten.TalentEcosystemService/**`. The implementation may persist
minimal registry metadata, policy precondition/evaluation state, HCM/PSS
dependency states, and local/deferred verified-company access metadata.
Candidate/talent identity, reference exchange, reputation/risk/analytics, broad
consent engine, and frontend/Gateway work remain excluded.

## 4. Entity Fields

Minimal runtime metadata field categories authorized for the first backend/API slice:

- `Code`
- `DisplayName`
- `AssociationMembershipState`
- `MemberCompanyState`
- `MemberCompanyReference`
- `HcmFoundationReference`
- `ConsentVisibilityPolicyId`
- `PolicyEvaluationState`
- `VisibilityApprovalState`
- `AssociationActivationState`
- `DependencyStates`
- `SourceContractVersion`
- `LastEvaluatedAt`
- `RegistryVersion`

Forbidden for this pack:

- `CAND-CAP-0012` or `MOD-0323` as runtime module literals.
- Candidate/talent profile fields.
- Consent records or broad policy engine fields.
- Reference exchange marketplace fields.
- Reputation, risk, analytics, or dispute workflow fields.
- Raw HRIS/provider payloads, credentials, tokens, secrets, bank/tax/payroll,
  payslip, biometric/geolocation, national ID, DOB, home address, or PII-heavy
  fields.

## 5. Repo Scope

Authorized scope for this ready-for-dev pack:

- `execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0012-association-membership-member-company-registry.md`
- `services/Diten.TalentEcosystemService/**` for the first backend/API metadata
  association/member company registry contract slice only.

Runtime implementation must remain inside `services/Diten.TalentEcosystemService/**`
and must use runtime owner/key `tep.association-memberships`. Frontend, Gateway,
HCM, PSS, candidate/talent identity, reference exchange, broad consent engine,
and verified-company runtime integration remain outside scope.

## 6. Protected Paths

This ready-for-dev pack must not change:

- `services/**`, except `services/Diten.TalentEcosystemService/**` for the
  approved first metadata association/member company registry contract slice
- `frontend/**`
- `gateway/**`
- `execution/domains/platform-shared-services/**`
- `execution/domains/human-capital-management/**`
- `execution/domains/master-data-management/**`
- `execution/domains/developer-enablement/**`
- `execution/domains/talent-ecosystem-platform/domain-config.md`
- `execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0011-talent-ecosystem-platform-shell.md`
- `execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0013-consent-visibility-access-policy.md`
- `execution/registries/module-id-registry.md`, except a separate EA-approved
  identity reservation or canonicalization update.
- `.antigravity/**`

## 7. Dependencies

Governance dependencies:

- `AGENTS.md`
- `execution/domains/talent-ecosystem-platform/domain-config.md`
- `execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`
- `execution/portfolio/delivery-capability-packs/DCP-010-tep-runtime-authorization.md`
- `execution/portfolio/delivery-capability-packs/DCP-011-tep-consent-visibility-runtime-authorization.md`
- `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`
- `execution/registries/module-id-registry.md`
- `execution/portfolio/blueprint-master-plan-reconciliation.md`

Completed TEP dependencies:

- `CAND-CAP-0011` - Talent Ecosystem Platform Shell, `done`.
- `CAND-CAP-0013` - Consent, Visibility & Access Policy, `done`; consumed as
  the consent/visibility policy precondition contract for Association activation.

Completed HCM foundation dependencies:

- `CAND-CAP-0007` - Employee Profile & Employment Record Projection, `done`.
- `CAND-CAP-0008` - HR Governance & Sensitive Access Controls, `done`.
- `CAND-CAP-0009` - Position & Organization Assignment, `done`.
- `CAND-CAP-0010` - Offboarding & Exit Management, `done`.

PSS substrate dependencies referenced only as dependencies:

- `MOD-0018` - RBAC / ABAC Authorization substrate.
- `MOD-0021` - Audit Trail Service substrate.
- `MOD-0028` - Documentation and evidence storage substrate.
- `MOD-0030` - Records / Retention / Legal Hold substrate.
- `MOD-0031` - Evidence Linking substrate.
- `MOD-0048` - Reference Data Management.
- `MOD-0057` - Taxonomy / Tagging Service.
- `MOD-0251` - HRIS External SoR.
- `MOD-0288-FU02` - Person Reference Directory Projection.

TEP R2 sibling dependencies/follow-ups:

- `MOD-0324` / future candidate - Verified HR Participant & Company Access;
  real integration remains future follow-up, first slice may use local/deferred
  metadata.
- `MOD-0326` / future candidate - Talent Ecosystem Governance & Review Board.

## 8. Runtime Constraints

- Runtime implementation is authorized only for the first metadata association/
  member company registry contract slice while status is `ready-for-dev`.
- `Diten.TalentEcosystemService` scaffold exists and is the only runtime host
  authorized for this slice.
- Runtime owner/key is `tep.association-memberships`.
- Runtime permission namespace is `tep.association-memberships.*`.
- `CAND-CAP-0012` is a governance identity only and must never be written into
  runtime literals, permission seeds, route metadata, database records, telemetry
  owner fields, config keys, Hangfire owner names, or test fixtures.
- `MOD-0323` must not be used for this TEP capability.
- TEP must not build association membership from raw HRIS data or PSS person
  projection alone. HCM foundation and consent/visibility contracts remain
  required prerequisites.
- CAND-CAP-0013 consent/visibility policy precondition is required for
  Association activation. If policy is unavailable, activation must fail closed;
  evaluation may return explicit `Deferred`.
- Any runtime dependency contract that is unavailable must result in explicit
  `Deferred` state or fail-closed behavior; blind references are not allowed.

## 9. Layout & Shell Contract

Current shell decision:

- `shell: none`
- `golden_reference: none`
- UI/frontend: N/A

No TEP association workspace UI, navigation, Razor view, JavaScript, DataTable,
RESX, or menu entry is authorized. Future UI must be handled by a separate
module pack after legal/privacy, consent/visibility, Gateway, and UX ownership
decisions are approved.

## 10. Backend File Convention

Backend implementation is authorized only for the first metadata association/
member company registry contract slice. Any backend/API implementation must
follow the repo's 5-layer .NET service convention, CQRS/MediatR patterns where
applicable, `Response<T>` envelope, tenant server-side resolution, soft delete
rules where persisted metadata is used, MongoDB repository/index standards,
JWT/RBAC authorization conventions, and fail-closed/deferred privacy/legal gates
from the CAND-CAP-0013 policy precondition contract.

## 11. Frontend File Contract

Frontend implementation is N/A.

No Razor view, JavaScript file, DataTable contract, menu item, localization
resource, layout binding, shell route, or frontend navigation entry may be
added from this pack.

## 12. Validation Rules

Planning validation rules:

- DCP-002 candidate gate for `CAND-CAP-0012` and `Association Membership &
  Member Company Registry` must pass before this pack is promoted.
- `MOD-0323` remains blocked and must not be reused for TEP.
- `CAND-CAP-0012` must remain governance/documentation identity only.
- `CAND-CAP-0011` TEP Shell must remain the completed TEP dependency.
- HCM foundation dependencies must remain dependency contracts only.
- Privacy/legal metadata-only waiver is approved for the first Association
  metadata registry slice.
- Domain-config sequencing nuance is resolved for the first slice: CAND-CAP-0013
  Consent, Visibility & Access Policy is `done` and must be consumed as the
  policy precondition contract before Association activation.
- Candidate/talent identity, consent engine, reference exchange, reputation,
  risk, analytics, and dispute features cannot be inferred from this pack.

## 13. Failure Path to Verify

Before implementation or review promotion, verify these failure paths remain blocked:

- Reusing `MOD-0323` for TEP.
- Treating `CAND-CAP-0012` as a runtime module ID.
- Starting association membership behavior beyond the approved metadata registry
  contract slice.
- Creating frontend or Gateway routes from this pack.
- Starting candidate/talent identity or real member-company verification runtime
  from this first slice.
- Building association membership directly from PSS person references or raw
  HRIS payloads without HCM foundation contracts.
- Moving HCM employee projection, sensitive access, assignment, offboarding, or
  PSS substrate ownership into TEP.
- Starting reference exchange, rehire recommendation, dispute, reputation, or
  risk runtime from this association governance pack.

## 14. Authorization Convention

Runtime owner/key: `tep.association-memberships`.

Approved permission namespace for the first slice:

- `tep.association-memberships.read`
- `tep.association-memberships.manage`
- `tep.association-memberships.archive`
- `tep.association-memberships.evaluate`
- `tep.association-memberships.member-company.manage`

`CAND-CAP-0012` and `MOD-0323` must never be used in runtime permission seeds,
route metadata, policy attributes, database records, telemetry owner fields,
config keys, or tests.

## 15. Gateway / API Routing Decision

Gateway routing is N/A.

No Ocelot route may be created. If future external API exposure is needed, the
Gateway route must be handled by the integration-agent workflow after a
ready-for-dev association backend/API slice is separately approved and reviewed.

## 16. Acceptance Criteria

1. AC-01: DCP-002 candidate gate passes for `CAND-CAP-0012` and `Association
   Membership & Member Company Registry`.
2. AC-02: Pack status is `ready-for-dev` for the first metadata association/
   member company registry backend/API contract slice only.
3. AC-03: `MOD-0323` is not used as the TEP identity.
4. AC-04: `CAND-CAP-0012` is documented as governance/documentation identity
   only and not a runtime literal.
5. AC-05: `CAND-CAP-0011` TEP Shell dependency is listed as `done`.
6. AC-06: HCM foundation dependencies are listed:
   `CAND-CAP-0007`, `CAND-CAP-0008`, `CAND-CAP-0009`, and `CAND-CAP-0010`.
7. AC-07: Association membership and member company registry are scoped as TEP
   governance boundaries only.
8. AC-08: Candidate/talent identity is explicitly out of scope.
9. AC-09: Consent engine ownership is explicitly out of scope.
10. AC-10: Reference exchange marketplace is explicitly out of scope.
11. AC-11: CAND-CAP-0013 Consent/Visibility is `done` and consumed as policy
    precondition for Association activation.
12. AC-12: Domain-config nuance is resolved for the first slice by requiring
    CAND-CAP-0013 policy precondition consumption.
13. AC-13: Runtime owner/key is `tep.association-memberships` and permission
    namespace is approved for read, manage, archive, evaluate, and
    member-company.manage.
14. AC-14: Frontend/Gateway remain closed.
15. AC-15: Runtime literal scan for `CAND-CAP-0012|MOD-0323` across runtime,
    frontend, gateway, and tests returns no matches.
16. AC-16: UI/DataTable/l10n verifier is N/A.
17. AC-17: DCP-010 remains shell/scaffold context; Association first slice is
    authorized by this ready-for-dev module-pack promotion and bounded to
    metadata registry behavior.
18. AC-18: PSS substrate dependencies are listed as dependencies only.
19. AC-19: Ready-for-dev checklist is complete with open blockers set to none
    for the approved first slice.
20. AC-20: Future EA canonical MOD assignment remains a follow-up and does not
    authorize runtime.

## 17. Test Expectations

Governance validation commands:

- `python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0012 --name "Association Membership & Member Company Registry"`
- `rg -n "CAND-CAP-0012|MOD-0323" services/Diten.TalentEcosystemService frontend gateway tests`

Runtime validation commands for implementation follow-up:

- `dotnet build services/Diten.TalentEcosystemService/src/Diten.TalentEcosystemService.Api/Diten.TalentEcosystemService.Api.csproj -c Debug --no-restore /nr:false -m:1 --tl:off`
- TEP association membership targeted tests
- `rg -n "CAND-CAP-0012|MOD-0323" services/Diten.TalentEcosystemService frontend gateway tests`

Frontend/DataTable/l10n verifier: N/A because `shell: none` and
`golden_reference: none`.

## 18. Governance Approval and Runtime-Ready Checklists

### Governance Approval Checklist

- [x] EA/registry owner reservation exists for `CAND-CAP-0012`.
- [x] DCP-002 candidate gate PASS recorded before pack authoring.
- [x] `MOD-0323` remains blocked and must not be used.
- [x] `CAND-CAP-0012` remains governance/documentation identity only.
- [x] TEP domain config exists.
- [x] `CAND-CAP-0011` TEP Shell dependency is `done`.
- [x] HCM foundation completion dependencies are listed.
- [x] PSS substrate dependencies are listed as dependencies only.
- [x] Domain-config sequencing nuance is recorded.
- [x] UI/frontend/gateway remains closed.
- [x] Candidate/talent identity remains outside this pack.
- [x] Consent engine and reference exchange marketplace remain outside this pack.
- [x] TEP ownership remains separate from HCM and PSS.

Open blockers for governance continuation: none if candidate gate remains PASS.

### Ready-for-Dev Checklist

- [x] EA candidate-runtime waiver approved for CAND-CAP-0012.
- [x] Runtime owner/key approved: `tep.association-memberships`.
- [x] Runtime permission namespace approved.
- [x] Runtime repo scope approved: `services/Diten.TalentEcosystemService/**`.
- [x] Minimal association membership and member company field/object list approved.
- [x] Privacy/legal metadata-only waiver approved for first slice.
- [x] CAND-CAP-0013 policy precondition contract approved and `done`.
- [x] Consent/visibility policy unavailable behavior approved: activation
  fail-closed; evaluation may be explicit `Deferred`.
- [x] Verified HR Participant / Company Access real integration deferred; first
  slice may use local/deferred metadata.
- [x] HCM foundation validation contract decisions approved as dependency context.
- [x] PSS substrate validation contract decisions approved as dependency context.
- [x] Gateway direct edit remains closed; integration-agent follow-up only if needed.
- [x] UI/frontend remains closed.
- [x] Runtime literal scan requirement retained for `CAND-CAP-0012|MOD-0323`.

Open blockers: none for the approved first backend/API metadata association/
member company registry contract slice.

## 19. Implementation Notes

- DCP-008 lists Association Membership & Member Company Registry in R2-A with
  legacy Excel ID `MOD-0323` and output role "Association/company membership
  registry".
- DCP-002 blocks `MOD-0323` for TEP use because it is not Blueprint-backed and
  has no canonical registry row.
- EA approved `CAND-CAP-0012` as a temporary candidate identity for governance
  documentation only.
- Ready-for-dev reconciliation: EA candidate-runtime waiver, runtime owner/key,
  permission namespace, repo scope, minimal field list, metadata-only privacy/
  legal waiver, CAND-CAP-0013 policy precondition contract, HCM/PSS dependency
  validation context, and local/deferred verified-company access metadata were
  approved for the first backend/API metadata association/member company
  registry contract slice.
- `CAND-CAP-0012` is not an EA canonical MOD allocation and remains a
  governance/documentation identity.
- `CAND-CAP-0011` TEP Shell is done and is the immediate TEP dependency.
- HCM R1 foundation is available as prerequisite contract context, but this
  pack does not consume HCM runtime APIs or create association runtime behavior.
- DCP-010 authorizes the TEP service scaffold and the completed TEP Shell first
  slice only. This pack now authorizes the Association first metadata registry
  slice through this ready-for-dev promotion.
- Domain-config lists Consent, Visibility & Access Policy before Association in
  the safe next-candidate section. CAND-CAP-0013 is now `done` and must be
  consumed as Association policy precondition.

## 20. Follow-up Items

- Implement the first backend/API metadata association/member company registry
  contract slice under `services/Diten.TalentEcosystemService/**` only after
  explicit user implementation prompt.
- Reconcile implementation against CAND-CAP-0013 policy precondition behavior
  before promoting this pack to review/done.
- Decide future EA canonical MOD assignment for Association Membership & Member
  Company Registry.
- Route any future Gateway exposure through integration-agent.
- Route any future Gateway exposure through integration-agent.
