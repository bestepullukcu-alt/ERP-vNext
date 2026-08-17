---
id: CAND-CAP-0013
name: Consent, Visibility & Access Policy
domain: talent-ecosystem-platform
service: Diten.TalentEcosystemService
shell: none
golden_reference: none
entity_base: BaseEntity
status: done
owner: enterprise-architect / tep-domain-owner / platform-team / security-owner / legal-owner
branch: feature/tep/cand-cap-0013-consent-visibility-access-policy
started: 2026-08-13
target: 2026-10-07
form_field_count: 0
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
runtime_authorization_source: execution/portfolio/delivery-capability-packs/DCP-011-tep-consent-visibility-runtime-authorization.md
shell_dependency: execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0011-talent-ecosystem-platform-shell.md
association_dependency: execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0012-association-membership-member-company-registry.md
candidate_identity: CAND-CAP-0013
legacy_excel_id: MOD-0325
canonicalization_status: candidate / pending-EA
runtime_service_candidate: Diten.TalentEcosystemService
bootstrap_type: runtime-authorized-first-slice
runtime_owner_key: tep.consent-visibility-policies
permission_namespace: tep.consent-visibility-policies.*
---

# CAND-CAP-0013 - Consent, Visibility & Access Policy

> Status: done. First TEP backend/API metadata/policy decision contract slice
> has been implemented, reviewed, and promoted to done. This
> pack does not authorize a broad consent engine, service scaffold, frontend,
> Gateway, Association runtime, candidate/talent identity, reference exchange,
> or RBAC/ABAC engine copy. `CAND-CAP-0013` remains a governance/documentation
> identity only and must never be written into runtime literals. `MOD-0325`
> remains blocked for TEP use.

## 1. Module Summary

`CAND-CAP-0013` reserves the temporary DCP-002 candidate identity for the native
Talent Ecosystem Platform R2-B capability named `Consent, Visibility & Access
Policy`.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`.
DCP-008 lists this row as legacy Excel ID `MOD-0325` with output role "Consent
and visibility rules". DCP-002 blocks `MOD-0325` because it is absent from the
Blueprint and has no canonical registry row. EA/registry owner approval reserved
`CAND-CAP-0013` as a temporary governance identity pending future canonical MOD
allocation.

This ready-for-dev pack defines the TEP policy boundary that may be required before
`CAND-CAP-0012` Association Membership & Member Company Registry can become
runtime-ready. It plans consent state, visibility scope, data-scope, and access
policy decisions without building a broad consent engine, RBAC/ABAC engine,
association runtime, candidate identity, reference exchange, or frontend/Gateway
surface.

## 2. Ownership and Boundaries

Owned by this ready-for-dev pack:

- TEP-native consent, visibility, and access policy governance boundary.
- Association runtime blocker boundary for `CAND-CAP-0012`.
- Planning contract for consent state, visibility scope, data-scope, and
  policy-unavailable behavior.
- Dependency mapping to `CAND-CAP-0011` TEP Shell and `CAND-CAP-0012`
  Association governance.
- Boundary decision that `MOD-0018` RBAC/ABAC remains PSS substrate only.
- Boundary decision that HCM sensitive access `CAND-CAP-0008` remains dependency
  context only.

Not owned by this pack:

- Broad RBAC/ABAC authorization engine or copy of `MOD-0018`.
- Runtime consent engine, consent capture UX, preference center, signature
  workflow, legal document execution, or policy automation.
- Association membership runtime, member company registry runtime, verified HR
  participant runtime, candidate/talent identity, reference exchange
  marketplace, reputation, risk, analytics, dispute, or review board runtime.
- HCM employee projection, sensitive access, assignment, or offboarding
  ownership.
- PSS directory/reference ownership.
- Frontend menus, Razor views, JavaScript, DataTables, RESX, Gateway routes,
  provider adapters, webhooks, background jobs, database collections, or
  raw/sensitive payload persistence.

## 3. Owned Objects

Governance-only planning objects:

| Object | Type | Purpose |
|---|---|---|
| TepConsentPolicyBoundary | Governance contract | Defines TEP consent state and consent-required boundaries. |
| TepVisibilityPolicyBoundary | Governance contract | Defines visibility scopes and who may see association/member-company data. |
| TepDataScopeBoundary | Governance contract | Defines data-scope prerequisites and fail-closed/deferred handling. |
| TepAssociationPolicyConsumptionContract | Governance contract | Defines how CAND-CAP-0012 must consume policy decisions before runtime. |
| TepPolicyRuntimeBlockerLog | Governance plan | Tracks runtime owner/key, permission namespace, legal/privacy, and validation blockers. |

Runtime objects authorized by this ready-for-dev pack are limited to the first
metadata/policy decision contract slice in `services/Diten.TalentEcosystemService/**`.
The implementation may persist minimal policy metadata, evaluation outcomes,
Association consumption/precondition metadata, and local/deferred audit/evidence/
retention references. Broad consent engine behavior, consent capture UX, legal
document execution, Association runtime, candidate/talent identity, and frontend/
Gateway work remain excluded.

## 4. Entity Fields

Minimal runtime metadata field categories authorized for the first backend/API slice:

- `ConsentState` / consent requirement state.
- `VisibilityScope` / visibility classification.
- `DataScopeState` / data-scope availability state.
- `AccessPolicyState` / policy decision state.
- `AssociationConsumptionState` / Association dependency precondition state.
- `PolicyUnavailableBehavior` / fail-closed or explicit `Deferred` state.
- Source contract version, stale/immutable metadata, and evaluation timestamps.

Forbidden for this pack:

- `CAND-CAP-0013` or `MOD-0325` as runtime module literals.
- `MOD-0018` as TEP-native consent/visibility identity.
- Candidate/talent profile fields.
- Association membership or member company registry runtime fields.
- Reference exchange marketplace fields.
- Broad RBAC/ABAC permission model fields copied from PSS.
- Raw HRIS/provider payloads, credentials, tokens, secrets, bank/tax/payroll,
  payslip, biometric/geolocation, national ID, DOB, home address, or PII-heavy
  fields.

## 5. Repo Scope

Authorized scope for this ready-for-dev pack:

- `execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0013-consent-visibility-access-policy.md`
- `services/Diten.TalentEcosystemService/**` for the first backend/API metadata/
  policy decision contract slice only.

Runtime implementation must remain inside `services/Diten.TalentEcosystemService/**`
and must use runtime owner/key `tep.consent-visibility-policies`. Frontend,
Gateway, HCM, PSS, Association runtime, and service scaffold changes remain
outside scope.

## 6. Protected Paths

This ready-for-dev pack must not change:

- `services/**`, except `services/Diten.TalentEcosystemService/**` for the
  approved first metadata/policy decision contract slice
- `frontend/**`
- `gateway/**`
- `execution/domains/platform-shared-services/**`
- `execution/domains/human-capital-management/**`
- `execution/domains/master-data-management/**`
- `execution/domains/developer-enablement/**`
- `execution/domains/talent-ecosystem-platform/domain-config.md`
- `execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0011-talent-ecosystem-platform-shell.md`
- `execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0012-association-membership-member-company-registry.md`
- `execution/registries/module-id-registry.md`, except a separate EA-approved
  identity reservation or canonicalization update.
- `.antigravity/**`

## 7. Dependencies

Governance dependencies:

- `AGENTS.md`
- `execution/domains/talent-ecosystem-platform/domain-config.md`
- `execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`
- `execution/portfolio/delivery-capability-packs/DCP-010-tep-runtime-authorization.md`
- `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`
- `execution/registries/module-id-registry.md`
- `execution/portfolio/blueprint-master-plan-reconciliation.md`

TEP dependencies:

- `CAND-CAP-0011` - Talent Ecosystem Platform Shell, `done`.
- `CAND-CAP-0012` - Association Membership & Member Company Registry,
  governance-only `approved`; this policy pack may be required before
  Association runtime-ready promotion.

HCM dependency context:

- `CAND-CAP-0008` - HR Governance & Sensitive Access Controls, `done`, as
  sensitive access context only.
- `CAND-CAP-0007` - Employee Profile & Employment Record Projection, `done`.
- `CAND-CAP-0009` - Position & Organization Assignment, `done`.
- `CAND-CAP-0010` - Offboarding & Exit Management, `done`.

PSS substrate dependencies referenced only as dependencies:

- `MOD-0018` - RBAC / ABAC Authorization substrate, not this TEP identity.
- `MOD-0021` - Audit Trail Service substrate.
- `MOD-0028` - Documentation and evidence storage substrate.
- `MOD-0030` - Records / Retention / Legal Hold substrate.
- `MOD-0031` - Evidence Linking substrate.
- `MOD-0048` - Reference Data Management.
- `MOD-0057` - Taxonomy / Tagging Service.

TEP R2 sibling dependencies to reconcile before runtime-ready:

- `MOD-0324` / future candidate - Verified HR Participant & Company Access.
- `MOD-0326` / future candidate - Talent Ecosystem Governance & Review Board.
- `MOD-0330` / future candidate - Trust Level & Multi-Signature Engine.

## 8. Runtime Constraints

- Runtime implementation is authorized only for the first metadata/policy
  decision contract slice while status is `ready-for-dev`.
- `Diten.TalentEcosystemService` scaffold exists and is the only runtime host
  authorized for this slice.
- Runtime owner/key is `tep.consent-visibility-policies`.
- Runtime permission namespace is `tep.consent-visibility-policies.*`.
- `CAND-CAP-0013` is a governance identity only and must never be written into
  runtime literals, permission seeds, route metadata, database records, telemetry
  owner fields, config keys, Hangfire owner names, or test fixtures.
- `MOD-0325` must not be used for this TEP capability.
- `MOD-0018` must remain PSS RBAC/ABAC substrate and must not be copied or
  reused as this TEP-native identity.
- Association runtime must fail closed or explicitly defer if consent,
  visibility, or data-scope policy is unavailable.
- Blind Association activation without policy precondition is forbidden.

## 9. Layout & Shell Contract

Current shell decision:

- `shell: none`
- `golden_reference: none`
- UI/frontend: N/A

No consent/visibility UI, policy administration page, preference center,
navigation, Razor view, JavaScript, DataTable, RESX, or menu entry is authorized.
Future UI must be handled by a separate module pack after legal/privacy, UX,
Gateway, and runtime ownership decisions are approved.

## 10. Backend File Convention

Backend implementation is authorized only for the first metadata/policy decision
contract slice. Any backend/API implementation must follow the repo's 5-layer
.NET service convention, CQRS/MediatR patterns where applicable, `Response<T>`
envelope, tenant server-side resolution, soft delete rules where persisted
metadata is used, MongoDB repository/index standards, JWT/RBAC authorization
conventions, and fail-closed/deferred privacy/legal gates defined by DCP-011.

## 11. Frontend File Contract

Frontend implementation is N/A.

No Razor view, JavaScript file, DataTable contract, menu item, localization
resource, layout binding, shell route, or frontend navigation entry may be
added from this pack.

## 12. Validation Rules

Planning validation rules:

- DCP-002 candidate gate for `CAND-CAP-0013` and `Consent, Visibility & Access
  Policy` must pass before this pack is promoted.
- `MOD-0325` remains blocked and must not be reused for TEP.
- `CAND-CAP-0013` must remain governance/documentation identity only.
- `MOD-0018` must remain PSS RBAC/ABAC substrate only and must not be reused as
  this TEP-native identity.
- `CAND-CAP-0011` TEP Shell must remain completed dependency.
- `CAND-CAP-0012` Association must remain governance-only until this policy
  boundary and its runtime-ready decisions are explicit.
- Association consumption contract must define which membership/company states
  require consent/visibility approval before `CAND-CAP-0012` runtime can become
  ready-for-dev.
- Policy unavailable behavior must be explicit: fail-closed or `Deferred`; blind
  policy assumptions are forbidden.
- Candidate/talent identity, reference exchange, reputation, risk, analytics,
  dispute, and review board features cannot be inferred from this pack.

## 13. Failure Path to Verify

Before governance approval or runtime-ready promotion, verify these failure
paths remain blocked:

- Reusing `MOD-0325` for TEP.
- Treating `CAND-CAP-0013` as a runtime module ID.
- Reusing `MOD-0018` as the TEP consent/visibility identity.
- Copying broad RBAC/ABAC authorization engine behavior into TEP.
- Starting consent engine runtime from this draft pack.
- Creating API controllers, entities, repositories, database collections,
  frontend, or Gateway routes from this draft pack.
- Promoting Association runtime without explicit consent/visibility/data-scope
  preconditions.
- Starting candidate/talent identity, reference exchange, reputation, risk,
  analytics, dispute, or review board runtime from this policy governance pack.
- Moving HCM sensitive access or PSS authorization ownership into TEP.

## 14. Authorization Convention

Runtime owner/key: `tep.consent-visibility-policies`.

Approved permission namespace for the first slice:

- `tep.consent-visibility-policies.read`
- `tep.consent-visibility-policies.manage`
- `tep.consent-visibility-policies.evaluate`
- `tep.consent-visibility-policies.audit.read`

`CAND-CAP-0013`, `MOD-0325`, and `MOD-0018` must not be used as TEP-native
runtime permission seeds, route metadata, policy attributes, database records,
telemetry owner fields, config keys, or tests.

## 15. Gateway / API Routing Decision

Gateway routing is N/A.

No Ocelot route may be created. If future external API exposure is needed, the
Gateway route must be handled by the integration-agent workflow after a
ready-for-dev policy backend/API slice is separately approved and reviewed.

## 16. Acceptance Criteria

1. AC-01: DCP-002 candidate gate passes for `CAND-CAP-0013` and `Consent,
   Visibility & Access Policy`.
2. AC-02: Pack status is `done` after first backend/API metadata/policy
   decision contract slice implementation, read-only implementation review PASS,
   and done-promotion reconciliation.
3. AC-03: `MOD-0325` is not used as the TEP identity.
4. AC-04: `CAND-CAP-0013` is documented as governance/documentation identity
   only and not a runtime literal.
5. AC-05: `MOD-0018` remains PSS RBAC/ABAC substrate only.
6. AC-06: `CAND-CAP-0011` TEP Shell dependency is listed as `done`.
7. AC-07: `CAND-CAP-0012` Association dependency/blocker target is listed.
8. AC-08: Association consumption contract topics are listed:
   consent-required states, visibility approval, fail-closed/deferred behavior,
   and dependency/precondition boundary.
9. AC-09: Candidate/talent identity is explicitly out of scope.
10. AC-10: Reference exchange marketplace is explicitly out of scope.
11. AC-11: Broad RBAC/ABAC engine copy is explicitly out of scope.
12. AC-12: Runtime owner/key is `tep.consent-visibility-policies` and
    permission namespace is approved for read, manage, evaluate, and audit.read.
13. AC-13: Frontend/Gateway remain closed.
14. AC-14: Runtime literal scan for `CAND-CAP-0013|MOD-0325` across runtime,
    frontend, gateway, and tests returns no matches.
15. AC-15: UI/DataTable/l10n verifier is N/A.
16. AC-16: DCP-011 is referenced as the runtime authorization source for this
    policy slice; DCP-010 remains shell-only.
17. AC-17: HCM sensitive access is dependency context only.
18. AC-18: PSS substrate dependencies are listed as dependencies only.
19. AC-19: Ready-for-dev checklist is complete with open blockers set to none
    for the approved first slice.
20. AC-20: Future EA canonical MOD assignment remains a follow-up and does not
    authorize runtime.

## 17. Test Expectations

Governance validation commands:

- `python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0013 --name "Consent, Visibility & Access Policy"`
- `rg -n "CAND-CAP-0013|MOD-0325" services/Diten.TalentEcosystemService frontend gateway tests`

Runtime validation commands for implementation follow-up:

- `dotnet build services/Diten.TalentEcosystemService/src/Diten.TalentEcosystemService.Api/Diten.TalentEcosystemService.Api.csproj -c Debug --no-restore /nr:false -m:1 --tl:off`
- TEP consent/visibility targeted tests
- `rg -n "CAND-CAP-0013|MOD-0325" services/Diten.TalentEcosystemService frontend gateway tests`

Frontend/DataTable/l10n verifier: N/A because `shell: none` and
`golden_reference: none`.

## 18. Governance Approval and Runtime-Ready Checklists

### Governance Approval Checklist

- [x] EA/registry owner reservation exists for `CAND-CAP-0013`.
- [x] DCP-002 candidate gate PASS recorded before pack authoring.
- [x] `MOD-0325` remains blocked and must not be used.
- [x] `CAND-CAP-0013` remains governance/documentation identity only.
- [x] `MOD-0018` remains PSS RBAC/ABAC substrate only.
- [x] TEP domain config exists.
- [x] `CAND-CAP-0011` TEP Shell dependency is `done`.
- [x] `CAND-CAP-0012` Association blocker target is listed.
- [x] HCM sensitive access dependency context is listed.
- [x] PSS substrate dependencies are listed as dependencies only.
- [x] Association consumption contract topics are listed.
- [x] UI/frontend/gateway remains closed.
- [x] Candidate/talent identity remains outside this pack.
- [x] Reference exchange marketplace remains outside this pack.
- [x] TEP ownership remains separate from HCM and PSS.

Open blockers for governance continuation: none if candidate gate remains PASS.

### Ready-for-Dev Checklist

- [x] EA candidate-runtime waiver approved for CAND-CAP-0013.
- [x] Runtime owner/key approved: `tep.consent-visibility-policies`.
- [x] Runtime permission namespace approved.
- [x] Runtime repo scope approved: `services/Diten.TalentEcosystemService/**`.
- [x] Minimal consent/visibility/access policy field/object list approved.
- [x] Legal/privacy metadata-only waiver approved for first slice.
- [x] Association consumption contract approved: consent-required membership/
  company states, fail-closed/deferred behavior, and dependency/precondition
  boundary for `CAND-CAP-0012`.
- [x] Data-scope and visibility classification model approved for metadata-only
  first slice.
- [x] HCM sensitive access interaction decision approved as dependency/precondition.
- [x] MOD-0018 substrate integration pattern / no engine copy confirmation approved.
- [x] Audit/evidence/retention local/deferred metadata waiver approved.
- [x] Gateway direct edit remains closed; integration-agent follow-up only if needed.
- [x] UI/frontend remains closed.
- [x] Runtime literal scan requirement retained for `CAND-CAP-0013|MOD-0325`.

Open blockers: none for the approved first backend/API metadata/policy decision
contract slice.

## 19. Implementation Notes

- DCP-008 lists Consent, Visibility & Access Policy in R2-B with legacy Excel ID
  `MOD-0325` and output role "Consent and visibility rules".
- DCP-002 blocks `MOD-0325` for TEP use because it is not Blueprint-backed and
  has no canonical registry row.
- EA approved `CAND-CAP-0013` as a temporary candidate identity for governance
  documentation only.
- Ready-for-dev reconciliation: DCP-011 approved the candidate-runtime waiver,
  runtime owner/key, permission namespace, metadata-only legal/privacy waiver,
  Association consumption contract, MOD-0018 no-engine-copy pattern, and local/
  deferred audit/evidence/retention waiver for the first backend/API metadata/
  policy decision contract slice.
- Review-ready reconciliation: read-only implementation review PASS. Runtime
  owner/key remained `tep.consent-visibility-policies`; permission namespace was
  limited to read/manage/evaluate/audit.read; Mongo-backed tenant-aware
  persistence, active tenant Code unique index, soft delete, Association
  fail-closed/Deferred behavior, local/deferred audit/evidence/retention
  metadata, literal scan, build, targeted tests, full application tests, and
  frontend/Gateway/HCM/PSS scope closure were verified. Low residual gap: the
  DCP-002 candidate gate could not be rerun from the active Desktop workspace
  because `.antigravity/scripts/verify_module_id.py` is absent there; existing
  governance gate evidence remains recorded.
- Done-promotion reconciliation: implementation review PASS, pack status review,
  open blockers none, build PASS, TEP consent/visibility targeted tests PASS
  16/16, full TEP Application tests PASS 31/31, runtime literal scan PASS
  (`CAND-CAP-0013|MOD-0325` no matches), production in-memory repository scan
  PASS, frontend/Gateway/HCM/PSS scope closed, and Association consumption
  fail-closed/Deferred behavior verified.
- `CAND-CAP-0013` is not an EA canonical MOD allocation and remains a
  governance/documentation identity.
- `CAND-CAP-0011` TEP Shell is done.
- `CAND-CAP-0012` Association is governance-only approved and remains
  runtime-blocked until this policy boundary is explicit.
- `MOD-0018` remains PSS RBAC/ABAC substrate only and is not reused as this
  TEP-native policy identity.
- DCP-010 authorizes the TEP service scaffold and completed TEP Shell first
  slice only; DCP-011 authorizes this consent/visibility pack for ready-for-dev
  promotion and first-slice implementation.

## 20. Follow-up Items

- Implement the first backend/API metadata/policy decision contract slice under
  `services/Diten.TalentEcosystemService/**` only after explicit user
  implementation prompt.
- Reconcile CAND-CAP-0012 Association runtime-ready blockers against the
  implemented and reviewed CAND-CAP-0013 policy contract.
- Decide future EA canonical MOD assignment for Consent, Visibility & Access
  Policy.
- Route any future Gateway exposure through integration-agent.
