---
id: CAND-CAP-0015
name: Talent Ecosystem Governance & Review Board
domain: talent-ecosystem-platform
service: Diten.TalentEcosystemService
shell: none
golden_reference: none
entity_base: BaseEntity
status: done
owner: enterprise-architect / tep-domain-owner / platform-team / security-owner / legal-owner
branch: feature/tep/cand-cap-0015-governance-review-board
started: 2026-08-24
target: 2026-10-21
form_field_count: 0
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/blueprint-master-plan-reconciliation.md
shell_dependency: execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0011-talent-ecosystem-platform-shell.md
association_dependency: execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0012-association-membership-member-company-registry.md
consent_visibility_dependency: execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0013-consent-visibility-access-policy.md
verified_access_dependency: execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0014-verified-hr-participant-company-access.md
candidate_identity: CAND-CAP-0015
legacy_excel_id: MOD-0326
canonicalization_status: candidate / pending-EA
runtime_service_candidate: Diten.TalentEcosystemService
bootstrap_type: runtime-ready-first-slice
runtime_owner_key: tep.review-board
permission_namespace: tep.review-board.*
runtime_repo_scope: services/Diten.TalentEcosystemService/**
---

# CAND-CAP-0015 - Talent Ecosystem Governance & Review Board

> Status: done. The first metadata-only TEP backend/API review-board case and
> decision contract slice under `services/Diten.TalentEcosystemService/**` has
> been implemented, implementation review is PASS, and done-promotion readiness
> review is PASS. This pack does not
> authorize dispute workflow runtime, external review-board integration,
> frontend, Gateway, HCM/PSS implementation changes, candidate/talent identity,
> reference exchange, reputation/risk/analytics, or raw/sensitive payload
> persistence. `CAND-CAP-0015` remains a governance/documentation identity only
> and must never be written into runtime literals. `MOD-0326` remains blocked
> for TEP use.

## 1. Module Summary

`CAND-CAP-0015` reserves the temporary DCP-002 candidate identity for the native
Talent Ecosystem Platform R2-B capability named `Talent Ecosystem Governance &
Review Board`.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`.
DCP-008 places this row in R2-B with legacy Excel ID `MOD-0326` and output role
"Review board workflow". DCP-002 blocks `MOD-0326` because it is absent from the
Blueprint and has no canonical registry row. EA/registry owner reservation
recorded `CAND-CAP-0015` as a temporary governance identity pending future
canonical MOD allocation.

This ready-for-dev pack authorizes only a metadata-only review-board case and
decision contract slice. It does not create dispute workflow runtime, external
committee integrations, adjudication engines, candidate-facing appeal flows, or
user-facing exchange behavior.

## 2. Ownership and Boundaries

Owned by this ready-for-dev first slice:

- TEP-native governance/review-board boundary.
- Metadata-only review-board case and decision contract.
- Review-board eligibility and escalation metadata.
- Governance decision-state and review outcome metadata.
- Dependency mapping to completed TEP foundation packs.
- Fail-closed/deferred precondition handling for TEP foundation dependencies.
- Local/deferred audit/evidence/retention metadata only.

Not owned by this pack:

- Dispute workflow runtime or candidate response flows.
- External review-board, committee, legal, or ethics integration.
- Association membership registry implementation; `CAND-CAP-0012` owns that
  completed first metadata registry slice.
- Consent/visibility/access policy implementation; `CAND-CAP-0013` owns that
  completed first policy decision contract slice.
- Verified HR participant/member company access implementation;
  `CAND-CAP-0014` owns that completed metadata access slice.
- Candidate/talent identity, talent profile, reference exchange, reputation,
  risk, analytics, dispute execution, or review-board workflow runtime.
- HCM or PSS ownership transfer.
- Frontend, Gateway, raw/sensitive payload persistence, broad workflow engine,
  broad audit/evidence/retention engine, or RBAC/ABAC engine copy.

Authorized runtime owner/key:

- `tep.review-board`

Authorized permission namespace:

- `tep.review-board.read`
- `tep.review-board.manage`
- `tep.review-board.review`
- `tep.review-board.audit.read`

Authorized runtime repo scope:

- `services/Diten.TalentEcosystemService/**`

## 3. Owned Objects

First-slice metadata objects:

| Object | Type | Purpose |
|---|---|---|
| TepReviewBoardCaseMetadata | Runtime metadata contract | Captures a tenant-scoped review-board case reference and state. |
| TepReviewBoardDecisionMetadata | Runtime metadata contract | Captures local/deferred review decision metadata without dispute workflow execution. |
| TepReviewBoardDependencySnapshot | Runtime metadata contract | Records Association, Consent/Visibility, Verified Access, and HCM foundation precondition states. |
| TepReviewerEligibilitySnapshot | Runtime metadata contract | Records reviewer eligibility and segregation-of-duties decision state. |
| TepReviewBoardAuditMetadata | Runtime metadata contract | Records bounded local/deferred audit, evidence, and retention state. |

No dispute workflow runtime or external review-board integration is authorized by
this pack.

## 4. Entity Fields

Authorized minimal metadata fields:

- `Code`
- `DisplayName`
- `ReviewBoardCaseState`
- `ReviewDecisionState`
- `AssociationMembershipRegistryId`
- `ConsentVisibilityPolicyId`
- `VerifiedParticipantAccessId`
- `ReviewerEligibilityState`
- `SegregationOfDutiesState`
- `LegalSecurityDecisionState`
- `ExternalReviewBoardState`
- `AuditEvidenceState`
- `RetentionState`
- `DependencyStates`
- `SourceContractVersion`
- `LastEvaluatedAt`
- `ReviewBoardVersion`

Forbidden for this pack:

- `CAND-CAP-0015` or `MOD-0326` as runtime module literals.
- Dispute workflow runtime, external review-board integration, adjudication
  engine, broad workflow engine, broad audit/evidence/retention engine, or
  RBAC/ABAC engine copy fields.
- Candidate/talent identity, reference exchange, reputation, risk, analytics,
  or marketplace fields.
- Raw HRIS/provider payloads, credentials, tokens, secrets, bank/tax/payroll,
  payslip, biometric/geolocation, national ID, DOB, home address, or PII-heavy
  fields.

## 5. Repo Scope

Authorized runtime scope for this ready-for-dev pack:

- `services/Diten.TalentEcosystemService/**`

Governance reconciliation may update only this pack. No changes are authorized
under `frontend/**`, `gateway/**`, HCM, or PSS implementation paths.

## 6. Protected Paths

This ready-for-dev pack must not change:

- `services/Diten.HumanCapitalService/**`
- `services/Diten.Platform/**`
- any service path outside `services/Diten.TalentEcosystemService/**`
- `frontend/**`
- `gateway/**`
- `execution/domains/platform-shared-services/**`
- `execution/domains/human-capital-management/**`
- `execution/domains/master-data-management/**`
- `execution/domains/developer-enablement/**`
- `execution/domains/talent-ecosystem-platform/domain-config.md`
- `execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0011-talent-ecosystem-platform-shell.md`
- `execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0012-association-membership-member-company-registry.md`
- `execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0013-consent-visibility-access-policy.md`
- `execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0014-verified-hr-participant-company-access.md`
- `execution/registries/module-id-registry.md`, except a separate EA-approved
  identity reservation or canonicalization update.
- `.antigravity/**`

## 7. Dependencies

Governance dependencies:

- `execution/domains/talent-ecosystem-platform/domain-config.md`
- `execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`
- `execution/portfolio/delivery-capability-packs/DCP-010-tep-runtime-authorization.md`
- `execution/portfolio/delivery-capability-packs/DCP-011-tep-consent-visibility-runtime-authorization.md`
- `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`
- `execution/registries/module-id-registry.md`
- `execution/portfolio/blueprint-master-plan-reconciliation.md`

Completed TEP foundation dependencies:

- `CAND-CAP-0011` - Talent Ecosystem Platform Shell, `done`.
- `CAND-CAP-0012` - Association Membership & Member Company Registry, `done`.
- `CAND-CAP-0013` - Consent, Visibility & Access Policy, `done`.
- `CAND-CAP-0014` - Verified HR Participant & Company Access, `done`.

HCM foundation dependency context:

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

## 8. Runtime Constraints

- Runtime implementation is authorized only for the first metadata-only
  review-board case and decision contract slice under
  `services/Diten.TalentEcosystemService/**`.
- `CAND-CAP-0015` is a governance identity only and must never be written into
  runtime literals, permission seeds, route metadata, database records, telemetry
  owner fields, config keys, background job owner names, or tests.
- `MOD-0326` must not be used as the TEP identity or remapped to TEP.
- Runtime owner/key is `tep.review-board`.
- Runtime permissions are limited to `tep.review-board.read`,
  `tep.review-board.manage`, `tep.review-board.review`, and
  `tep.review-board.audit.read`.
- Association, Consent/Visibility, and Verified Access preconditions must be
  consumed fail-closed or with explicit `Deferred` metadata when unavailable.
- Reviewer eligibility and segregation-of-duties unavailable states must block
  review decisions fail-closed or produce explicit deferred state.
- Legal/security review-board decision model is metadata-only; real dispute
  workflow and external review-board integration remain out of scope.
- CAND-CAP-0011/0012/0013/0014 completion does not authorize review-board
  runtime by itself.
- No review-board runtime may bypass Association, Consent/Visibility, and
  Verified Access preconditions.
- No review decision may be trusted from raw HRIS, HCM context, PSS reference
  projection, or Association metadata alone.
- Runtime owner/key, permission namespace, minimal field list, legal/security
  decision model, review-board eligibility policy, audit/evidence/retention
  behavior, and repo scope remain blockers.

## 9. Layout & Shell Contract

Current shell decision:

- `shell: none`
- `golden_reference: none`
- UI/frontend: N/A

No TEP review-board UI, navigation, Razor view, JavaScript, DataTable, RESX, or
menu entry is authorized. Future UI requires a separate module pack.

## 10. Backend File Convention

Backend/API implementation is authorized only for the first metadata-only slice
under `services/Diten.TalentEcosystemService/**`.

Backend/API work must follow the repo's 5-layer .NET service convention,
CQRS/MediatR patterns where applicable,
`Response<T>` envelope, tenant server-side resolution, soft delete rules where
metadata is persisted, MongoDB repository/index standards, JWT/RBAC
authorization conventions, and fail-closed/deferred privacy/legal gates.

## 11. Frontend File Contract

Frontend implementation is N/A.

No Razor view, JavaScript file, DataTable contract, menu item, localization
resource, layout binding, shell route, or frontend navigation entry may be
added from this pack.

## 12. Validation Rules

Planning validation rules:

- DCP-002 candidate gate for `CAND-CAP-0015` and `Talent Ecosystem Governance &
  Review Board` must pass before governance approval if the verifier is
  available.
- Candidate gate note: `.antigravity/scripts/verify_module_id.py` was not
  present in this checkout during pack preparation; candidate gate is not
  executable in this checkout; reservation is recorded in the registry and
  reconciliation ledger.
- Runtime literal scan for `CAND-CAP-0015|MOD-0326` across
  `services/Diten.TalentEcosystemService`, `frontend`, `gateway`, and `tests`
  must return no matches.
- `MOD-0326` remains blocked and must not be reused for TEP.
- `CAND-CAP-0015` must remain governance/documentation identity only.
- `CAND-CAP-0011`, `CAND-CAP-0012`, `CAND-CAP-0013`, and `CAND-CAP-0014` must
  remain completed dependencies.
- HCM foundation dependencies must remain dependency context only.
- Review-board eligibility, legal/security decision model, and reviewer access
  boundaries must be explicitly approved before runtime-ready promotion.
- Dispute workflow runtime, external review-board integration, candidate/talent
  identity, reference exchange, reputation/risk/analytics, raw/sensitive payload
  persistence, frontend, Gateway, service scaffold, and runtime implementation
  cannot be inferred from this pack.

## 13. Failure Path to Verify

Before implementation or review promotion, verify these failure paths remain
blocked:

- Reusing `MOD-0326` for TEP.
- Treating `CAND-CAP-0015` as a runtime module ID.
- Starting runtime/API/controller/entity/repository/database work outside the
  approved metadata-only slice and `services/Diten.TalentEcosystemService/**`
  scope.
- Creating frontend or Gateway routes from this pack.
- Creating review-board runtime without CAND-CAP-0012 Association,
  CAND-CAP-0013 Consent/Visibility, and CAND-CAP-0014 Verified Access
  preconditions.
- Treating HCM employee/sensitive access/assignment/offboarding context as TEP
  review-board ownership.
- Starting dispute workflow runtime, external review-board integration,
  candidate/talent identity, reference exchange, reputation, risk, or analytics
  behavior.
- Persisting raw provider payloads, credentials, tokens, secrets, or PII-heavy
  data.

## 14. Authorization Convention

Runtime owner/key: `tep.review-board`.

Approved permission namespace:

- `tep.review-board.read`
- `tep.review-board.manage`
- `tep.review-board.review`
- `tep.review-board.audit.read`

Only these permission strings may be used for the first metadata-only
review-board slice.

`CAND-CAP-0015` and `MOD-0326` must never be used in runtime permission seeds,
route metadata, policy attributes, database records, telemetry owner fields,
config keys, or tests.

## 15. Gateway / API Routing Decision

Gateway routing is N/A.

No Ocelot route may be created. If future external API exposure is needed, the
Gateway route must be handled by the integration-agent workflow after a
separate ready-for-dev runtime module pack is approved and reviewed.

## 16. Acceptance Criteria

1. AC-01: EA/registry owner reservation exists for `CAND-CAP-0015`.
2. AC-02: Pack status is `done` after first metadata-only review-board case
   and decision contract slice implementation, read-only implementation review
   PASS, and done-promotion readiness review PASS.
3. AC-03: `MOD-0326` is not used as the TEP identity.
4. AC-04: `CAND-CAP-0015` is documented as governance/documentation identity
   only and not a runtime literal.
5. AC-05: Candidate gate status is recorded; if the script is unavailable, the
   pack records "candidate gate not executable in this checkout; reservation
   recorded".
6. AC-06: Runtime literal scan for `CAND-CAP-0015|MOD-0326` across runtime,
   frontend, gateway, and tests returns no matches.
7. AC-07: `CAND-CAP-0011` TEP Shell dependency is listed as `done`.
8. AC-08: `CAND-CAP-0012` Association dependency is listed as `done`.
9. AC-09: `CAND-CAP-0013` Consent/Visibility dependency is listed as `done`.
10. AC-10: `CAND-CAP-0014` Verified Access dependency is listed as `done`.
11. AC-11: HCM foundation dependencies are listed as context:
    `CAND-CAP-0007`, `CAND-CAP-0008`, `CAND-CAP-0009`, and `CAND-CAP-0010`.
12. AC-12: Review-board boundary is scoped as metadata-only TEP review-board
    case and decision contract.
13. AC-13: Association, Consent/Visibility, and Verified Access precondition
    planning is included.
14. AC-14: Dispute workflow runtime is explicitly out of scope.
15. AC-15: Candidate/talent identity, reference exchange, reputation/risk/
    analytics, and external review-board integration are explicitly out of
    scope.
16. AC-16: Frontend/Gateway remain closed.
17. AC-17: Runtime/API/controller/entity/repository/database work is authorized
    only under `services/Diten.TalentEcosystemService/**`.
18. AC-18: Service scaffold already exists; no new service scaffold is
    authorized.
19. AC-19: Runtime owner/key, permission namespace, minimal fields, and
    precondition behavior are testable.
20. AC-20: Open blockers are `none` for the approved first backend/API slice.

## 17. Test Expectations

Governance validation commands:

- `python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0015 --name "Talent Ecosystem Governance & Review Board"`
- `rg -n "CAND-CAP-0015|MOD-0326" services/Diten.TalentEcosystemService frontend gateway tests`

Candidate gate note:

- Candidate gate was not executable during pack preparation because
  `.antigravity/scripts/verify_module_id.py` was not present in this checkout.
  Reservation is recorded in `execution/registries/module-id-registry.md` and
  `execution/portfolio/blueprint-master-plan-reconciliation.md`.

Runtime validation commands:

- `dotnet build services/Diten.TalentEcosystemService/src/Diten.TalentEcosystemService.Api/Diten.TalentEcosystemService.Api.csproj -c Debug --no-restore /nr:false -m:1 --tl:off`
- TEP review-board targeted tests must pass.
- Full TEP Application tests must pass.
- Production in-memory repository scan must return no matches under
  `services/Diten.TalentEcosystemService/src`.

Frontend/DataTable/l10n verifier: N/A because `shell: none` and
`golden_reference: none`.

## 18. Governance Approval and Runtime-Ready Checklists

### Governance Approval Checklist

- [x] EA/registry owner reservation exists for `CAND-CAP-0015`.
- [x] DCP-002 candidate gate PASS recorded, or verifier unavailability accepted
  as governance note.
- [x] `MOD-0326` remains blocked and must not be used.
- [x] `CAND-CAP-0015` remains governance/documentation identity only.
- [x] TEP domain config exists.
- [x] `CAND-CAP-0011` TEP Shell dependency is `done`.
- [x] `CAND-CAP-0012` Association dependency is `done`.
- [x] `CAND-CAP-0013` Consent/Visibility dependency is `done`.
- [x] `CAND-CAP-0014` Verified Access dependency is `done`.
- [x] HCM foundation completion dependencies are listed as context.
- [x] UI/frontend/gateway remains closed.
- [x] Dispute workflow runtime remains outside this pack.
- [x] Candidate/talent identity remains outside this pack.
- [x] Reference exchange, reputation/risk/analytics, external review-board
  integration, and review-board runtime remain outside this pack.
- [x] TEP ownership remains separate from HCM and PSS.

Open blockers for governance continuation: none.

Governance note: DCP-002 candidate gate script is not present in this checkout;
EA/registry owner reservation is recorded. The gate must be rerun if/when the
verifier is restored before any later runtime-ready decision.

Done promotion reconciliation: done-promotion readiness review PASS. First
metadata-only backend/API review-board case and decision contract slice is
implemented under `services/Diten.TalentEcosystemService/**`; runtime owner/key
is `tep.review-board`; permission surface is limited to
`tep.review-board.read`, `tep.review-board.manage`, `tep.review-board.review`,
and `tep.review-board.audit.read`. Build PASS with 0 warnings and 0 errors;
ReviewBoard targeted tests PASS 19/19; full TEP Application tests PASS 85/85;
runtime literal scan PASS for `CAND-CAP-0015|MOD-0326`; production in-memory
repository scan PASS; frontend/Gateway/HCM/PSS scope remains closed. Dispute
workflow runtime, external review-board integration, candidate/talent identity,
reference exchange, reputation/risk/analytics, and raw/sensitive payload
persistence remain out of scope.

### Runtime-Ready Checklist

- [x] EA candidate-runtime waiver approved for `CAND-CAP-0015`.
- [x] Runtime owner/key approved: `tep.review-board`.
- [x] Runtime permission namespace approved:
  `tep.review-board.read`, `tep.review-board.manage`,
  `tep.review-board.review`, `tep.review-board.audit.read`.
- [x] Runtime repo scope approved: `services/Diten.TalentEcosystemService/**`.
- [x] Minimal review-board metadata field list approved.
- [x] Legal/security review-board decision model approved for metadata-only
  local/deferred first slice.
- [x] Reviewer eligibility and segregation-of-duties policy approved with
  fail-closed or explicit deferred behavior.
- [x] Association dependency consumption contract approved.
- [x] Consent/visibility policy precondition contract approved for this module.
- [x] Verified Access precondition contract approved for this module.
- [x] HCM foundation validation/consumption boundary approved as dependency
  context only.
- [x] Dispute workflow boundary approved as deferred and out of scope.
- [x] External review-board integration behavior approved as local/deferred
  metadata only.
- [x] Audit/evidence/retention metadata behavior approved as local/deferred
  metadata; real integration is future follow-up.
- [x] Gateway direct edit remains closed; integration-agent follow-up only if
  needed.
- [x] UI/frontend remains closed.
- [x] Runtime literal scan requirement retained for `CAND-CAP-0015|MOD-0326`.

Open blockers: none for the approved first backend/API review-board metadata
slice.

Runtime-ready reconciliation: EA candidate-runtime policy waiver approved.
`CAND-CAP-0015` remains a governance/documentation identity and `MOD-0326`
remains blocked; neither may appear in runtime literals. Runtime owner/key is
`tep.review-board`, permission namespace is limited to the four permissions
listed above, and scope is limited to `services/Diten.TalentEcosystemService/**`.

## 19. Implementation Notes

- DCP-008 lists Talent Ecosystem Governance & Review Board in R2-B with legacy
  Excel ID `MOD-0326` and output role "Review board workflow".
- DCP-002 blocks `MOD-0326` for TEP use because it is not Blueprint-backed and
  has no canonical registry row.
- EA/registry owner reservation is recorded for `CAND-CAP-0015` in
  `execution/registries/module-id-registry.md` and
  `execution/portfolio/blueprint-master-plan-reconciliation.md`.
- `CAND-CAP-0015` remains a governance/documentation identity only and
  `MOD-0326` remains blocked; neither may appear in runtime literals.
- `CAND-CAP-0011`, `CAND-CAP-0012`, `CAND-CAP-0013`, and `CAND-CAP-0014` are
  completed TEP dependencies and may be consumed as precondition contracts by
  the first metadata-only review-board slice.
- HCM foundation is available as prerequisite context, but this pack does not
  consume HCM runtime APIs or create HCM ownership.
- This pack authorizes only the first metadata-only backend/API review-board
  case and decision contract slice under `services/Diten.TalentEcosystemService/**`.
- This pack does not authorize frontend, Gateway, HCM/PSS implementation
  changes, dispute workflow runtime, external review-board integration,
  candidate/talent identity, reference exchange, reputation/risk/analytics, or
  raw/sensitive payload persistence.

## 20. Follow-up Items

- Rerun DCP-002 candidate gate when `.antigravity/scripts/verify_module_id.py`
  is available before implementation review or future canonicalization.
- Start implementation only through `@orchestrator` and only within
  `services/Diten.TalentEcosystemService/**`.
- Decide future EA canonical MOD assignment for Talent Ecosystem Governance &
  Review Board.
- Decide whether dispute workflow remains deferred or needs a separate module
  identity before any real dispute workflow runtime.
- Keep real audit/evidence/retention integration as a future follow-up.
- Keep external review-board integration as a future follow-up.
- Route any future Gateway exposure through integration-agent.
