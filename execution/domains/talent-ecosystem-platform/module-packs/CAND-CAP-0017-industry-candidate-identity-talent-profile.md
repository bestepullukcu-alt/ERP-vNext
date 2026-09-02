---
id: CAND-CAP-0017
name: Industry Candidate Identity & Talent Profile
domain: talent-ecosystem-platform
service: Diten.TalentEcosystemService
shell: none
golden_reference: none
entity_base: BaseEntity
status: done
owner: enterprise-architect / tep-domain-owner / platform-team / security-owner / legal-owner
branch: feature/tep/cand-cap-0017-industry-candidate-identity-talent-profile
started: 2026-08-25
target: 2026-10-28
form_field_count: 0
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/blueprint-master-plan-reconciliation.md
candidate_identity: CAND-CAP-0017
legacy_excel_id: MOD-0322
canonicalization_status: candidate / pending-EA
runtime_service_candidate: Diten.TalentEcosystemService
bootstrap_type: runtime-ready-first-slice
runtime_owner_key: tep.candidate-profiles
permission_namespace: tep.candidate-profiles.*
runtime_repo_scope: services/Diten.TalentEcosystemService/**
---

# CAND-CAP-0017 - Industry Candidate Identity & Talent Profile

> Status: done. The first metadata-only TEP backend/API candidate identity and
> talent profile contract slice under `services/Diten.TalentEcosystemService/**`
> has been implemented and implementation review retry is PASS after contract
> drift correction; done-promotion readiness review is PASS. This pack does not
> authorize candidate account lifecycle, self-service profile UX, reference
> exchange, reputation/risk analytics, marketplace behavior, recommendation
> modules, frontend, Gateway, HCM/PSS implementation changes,
> raw/sensitive/PII-heavy persistence, or production candidate profile ownership
> beyond the completed metadata contract. `CAND-CAP-0017` remains a
> governance/documentation identity only and must never be written into runtime
> literals. `MOD-0322` remains blocked for TEP use.

## 1. Module Summary

`CAND-CAP-0017` reserves the temporary DCP-002 candidate identity for the native
Talent Ecosystem Platform R2-C capability named `Industry Candidate Identity &
Talent Profile`.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`.
DCP-008 places this row in R2-C with legacy Excel ID `MOD-0322` and output role
"Sector candidate identity". DCP-002 blocks `MOD-0322` because it is absent
from the Blueprint and has no canonical registry row. EA/registry owner
reservation recorded `CAND-CAP-0017` as a temporary governance identity pending
future canonical MOD allocation.

This ready-for-dev pack authorizes only a metadata-only candidate identity and
talent profile contract slice. It does not create candidate account ownership,
self-service profile UX, reference exchange, reputation/risk analytics,
marketplace behavior, frontend, Gateway, or raw/sensitive payload persistence.

## 2. Ownership and Boundaries

Owned by this ready-for-dev first slice:

- TEP-native metadata-only industry candidate identity boundary.
- TEP-native metadata-only talent profile boundary.
- Sequencing relationship after completed TEP foundation modules.
- Privacy/legal/consent/data-scope/data-minimization enforcement contract.
- Boundary statement that candidate/talent identity must not be built directly
  on raw HRIS data or PSS person projection alone.

Not owned by this pack:

- Runtime candidate profile ownership.
- Runtime talent profile ownership.
- Candidate account lifecycle, authentication, self-service profile UX, or
  marketplace participation.
- Association membership, consent/visibility policy, verified access, review
  board, trust-level, or multi-signature implementation.
- HCM employee profile, offboarding, sensitive access, or position assignment
  implementation.
- PSS person/org/position directory ownership.
- Reference exchange, reputation/risk analytics, dispute workflow, rehire
  recommendation, or marketplace behavior.
- Frontend, Gateway, raw/sensitive payload persistence, broad workflow engine,
  broad audit/evidence/retention engine, or RBAC/ABAC engine copy.

Authorized runtime owner/key:

- `tep.candidate-profiles`

Authorized permission namespace:

- `tep.candidate-profiles.read`
- `tep.candidate-profiles.manage`
- `tep.candidate-profiles.evaluate`
- `tep.candidate-profiles.audit.read`

Authorized runtime repo scope:

- `services/Diten.TalentEcosystemService/**`

## 3. Owned Objects

First-slice metadata objects:

| Object | Type | Purpose |
|---|---|---|
| IndustryCandidateIdentityMetadata | Runtime metadata contract | Defines sector candidate identity metadata without account/profile-body ownership. |
| TalentProfileMetadata | Runtime metadata contract | Defines minimal talent profile summary metadata and sensitive-data limits. |
| CandidateConsentDataScopeSnapshot | Runtime metadata contract | Records privacy/legal/consent/data-scope decision states. |
| CandidateFoundationDependencySnapshot | Runtime metadata contract | Maps dependency readiness to completed TEP foundation and HCM context. |
| CandidateProfileAuditMetadata | Runtime metadata contract | Records local/deferred audit, evidence, and retention state. |

No candidate account lifecycle, self-service profile UX, reference exchange, or
marketplace object is authorized by this pack.

## 4. Entity Fields

Authorized minimal candidate identity metadata fields:

- `Code`
- `DisplayName`
- `CandidateReference`
- `AssociationMembershipId`
- `VerifiedParticipantId`
- `ConsentVisibilityPolicyId`
- `TrustLevelPolicyId`
- `CandidateIdentityState`
- `ConsentBasisState`
- `VisibilityApprovalState`
- `DataScopeState`
- `DependencyStates`
- `SourceContractVersion`
- `LastEvaluatedAt`
- `CandidateVersion`
- `DeferredReason`

Authorized minimal talent profile metadata fields:

- `TalentProfileReference`
- `ProfileState`
- `ProfileCompletenessState`
- `SkillSummaryMetadata`
- `CredentialSummaryMetadata`
- `ExperienceSummaryMetadata`
- `VisibilityClassification`
- `ProfilePolicyEvaluationState`
- `DataMinimizationState`
- `LocalAuditEvidenceRetentionState`

Forbidden for this pack:

- `CAND-CAP-0017` or `MOD-0322` as runtime module literals.
- Raw HRIS/provider payloads, credentials, tokens, secrets, bank/tax/payroll,
  payslip, biometric/geolocation, national ID, DOB, home address, or PII-heavy
  fields.
- Candidate account, self-service profile UX, identity verification runtime,
  reputation/risk analytics, reference exchange, dispute workflow, marketplace,
  or recommendation fields.
- Raw profile body, DOB, national ID, home address, payroll/bank/tax,
  biometric/geolocation, credential secrets, provider payloads, or PII-heavy
  profile details.

## 5. Repo Scope

Authorized runtime scope for this ready-for-dev pack:

- `services/Diten.TalentEcosystemService/**`

Governance reconciliation may update only this pack. No changes are authorized
under:

- `services/Diten.HumanCapitalService/**`
- `services/Diten.Platform/**`
- `frontend/**`
- `gateway/**`
- any service path outside `services/Diten.TalentEcosystemService/**`.

## 6. Protected Paths

This ready-for-dev pack must not change:

- any service path outside `services/Diten.TalentEcosystemService/**`,
- `frontend/**`,
- `gateway/**`,
- `execution/domains/platform-shared-services/**`,
- `execution/domains/human-capital-management/**`,
- `execution/domains/master-data-management/**`,
- `execution/domains/developer-enablement/**`,
- `execution/domains/talent-ecosystem-platform/domain-config.md`,
- completed TEP foundation packs,
- `execution/registries/module-id-registry.md`, except a separate EA-approved
  identity reservation or canonicalization update,
- `.antigravity/**`.

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
- `CAND-CAP-0015` - Talent Ecosystem Governance & Review Board, `done`.
- `CAND-CAP-0016` - Trust Level & Multi-Signature Engine, `done`.

HCM foundation dependency context:

- `CAND-CAP-0007` - Employee Profile & Employment Record Projection, `done`.
- `CAND-CAP-0008` - HR Governance & Sensitive Access Controls, `done`.
- `CAND-CAP-0009` - Position & Organization Assignment, `done`.
- `CAND-CAP-0010` - Offboarding & Exit Management, `done`.

PSS substrate dependencies remain dependency-only and do not transfer ownership
to TEP.

## 8. Upstream Inputs

- DCP-008 R2-C row: `MOD-0322` / `Industry Candidate Identity & Talent Profile`
  / `Sector candidate identity`.
- TEP domain-config sequencing: Industry Candidate Identity & Talent Profile
  follows Shell, Consent/Visibility, Association, and Verified Access
  foundation.
- EA/registry reservation for `CAND-CAP-0017` in
  `execution/registries/module-id-registry.md`.
- Reconciliation ledger reservation in
  `execution/portfolio/blueprint-master-plan-reconciliation.md`.
- Completed CAND-CAP-0011 through CAND-CAP-0016 module packs.

## 9. Downstream Consumers

Future consumers may include:

- Industry Exit Reference Record Registry.
- Reference Exchange Marketplace.
- Rehire Recommendation Network.
- Candidate Response & Dispute Management.
- R4 Talent Data Foundation and Industry Talent Pool.
- Future candidate-facing profile and consent experiences, if separately
  authorized.

Downstream runtime integration remains closed except for local/deferred
metadata references needed by the first candidate identity / talent profile
contract slice.

## 10. Runtime Constraints

Runtime implementation is authorized only for the first metadata-only candidate
identity and talent profile contract slice under
`services/Diten.TalentEcosystemService/**`.

Runtime constraints:

- `CAND-CAP-0017` remains a governance/documentation identity only.
- `MOD-0322` remains blocked and must not be used as a runtime literal.
- Runtime owner/key is `tep.candidate-profiles`.
- Runtime permissions are limited to `tep.candidate-profiles.read`,
  `tep.candidate-profiles.manage`, `tep.candidate-profiles.evaluate`, and
  `tep.candidate-profiles.audit.read`.
- Consent/Visibility precondition must fail closed or produce explicit
  `Deferred` metadata.
- Association, Verified Access, Review Board, and Trust-Level dependencies must
  be consumed as precondition contracts only.
- HCM foundation remains dependency context only.
- Data minimization and sensitive field exclusion must fail closed.
- Raw/sensitive/PII-heavy persistence remains closed.
- Audit/evidence/retention is local/deferred metadata only; real integration is
  future follow-up.

## 11. Frontend File Contract

Frontend implementation is N/A.

No Razor, JavaScript, DataTable, RESX, menu, or frontend route work is
authorized. Any future candidate-facing or HR-facing UI must be a separate
approved slice after runtime identity, consent, and privacy/legal decisions are
closed.

## 12. Data, Security, and Privacy

Runtime data persistence is authorized only for the minimal metadata fields in
Section 4.

Approved first-slice data/privacy policy:

- explicit consent basis is required for candidate identity/profile metadata,
- visibility and data-scope policy comes from CAND-CAP-0013 precondition,
- data minimization and sensitive field exclusion must fail closed,
- tenant isolation and cross-tenant access must fail closed,
- audit/evidence/retention is local/deferred metadata only,
- candidate access/request/response rights are future follow-up.

Raw HRIS/provider payloads, credentials, tokens, secrets, national ID, DOB, home
address, biometric/geolocation data, bank/tax/payroll data, payslips, and
PII-heavy profile details remain forbidden.

## 13. Failure Path to Verify

Before implementation or review promotion, verify these failure paths remain
blocked:

- Reusing `MOD-0322` for TEP.
- Treating `CAND-CAP-0017` as a runtime module ID.
- Starting candidate account/profile-body ownership runtime from this pack.
- Starting talent profile runtime outside the approved metadata-only fields.
- Creating runtime/API/controller/entity/repository/database work outside the
  approved metadata-only slice and `services/Diten.TalentEcosystemService/**`
  scope.
- Creating frontend or Gateway routes.
- Creating candidate/talent identity directly from raw HRIS payloads or PSS
  person projection without consent/data-scope decisions.
- Creating reference exchange, reputation/risk analytics, dispute workflow,
  marketplace, or recommendation behavior.
- Persisting raw provider payloads, credentials, tokens, secrets, or PII-heavy
  data.

## 14. Authorization Convention

Runtime owner/key:

- `tep.candidate-profiles`

Approved permission namespace:

- `tep.candidate-profiles.read`
- `tep.candidate-profiles.manage`
- `tep.candidate-profiles.evaluate`
- `tep.candidate-profiles.audit.read`

Only these permission strings may be used for the first metadata-only candidate
identity and talent profile slice.

## 15. Gateway / API Routing Decision

Gateway routing is N/A.

No Ocelot route may be created. If future external API exposure is needed, the
Gateway route must be handled by the integration-agent workflow after a
separate ready-for-dev runtime module pack is approved and reviewed.

## 16. Acceptance Criteria

1. AC-01: EA/registry owner reservation exists for `CAND-CAP-0017`.
2. AC-02: Pack status is `done` after first metadata-only candidate identity
   and talent profile contract slice implementation, contract drift correction,
   read-only implementation review retry PASS, and done-promotion readiness
   review PASS.
3. AC-03: `MOD-0322` is not used as the TEP identity.
4. AC-04: `CAND-CAP-0017` is documented as governance/documentation identity
   only and not a runtime literal.
5. AC-05: Candidate gate status is recorded; if the script is unavailable, the
   pack records "candidate gate not executable in this checkout; reservation
   recorded".
6. AC-06: Runtime literal scan for `CAND-CAP-0017|MOD-0322` across runtime,
   frontend, gateway, and tests returns no matches.
7. AC-07: `CAND-CAP-0011` through `CAND-CAP-0016` are listed as completed TEP
   foundation dependencies.
8. AC-08: HCM foundation dependencies are listed as context only.
9. AC-09: Candidate identity boundary is metadata-only first-slice runtime.
10. AC-10: Talent profile boundary is metadata-only first-slice runtime.
11. AC-11: Candidate account/profile-body ownership runtime is explicitly
    closed.
12. AC-12: Talent profile runtime is limited to minimal metadata fields.
13. AC-13: Privacy/legal/consent/data-scope/data-minimization decisions are
    closed for the metadata-only first slice.
14. AC-14: Candidate/talent identity must not be created directly from raw HRIS
    payloads or PSS person projection alone.
15. AC-15: Reference exchange, reputation/risk analytics, dispute workflow,
    marketplace, and recommendation behavior are explicitly out of scope.
16. AC-16: Raw/sensitive/PII-heavy persistence is explicitly out of scope.
17. AC-17: Frontend/Gateway remain closed.
18. AC-18: Runtime/API/controller/entity/repository/database work is authorized
    only under `services/Diten.TalentEcosystemService/**`.
19. AC-19: Runtime owner/key, permission namespace, minimal fields,
    preconditions, and privacy/data-minimization behavior are testable.
20. AC-20: Open blockers are `none` for the approved first backend/API slice.

## 17. Test Expectations

Governance validation commands:

- `python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0017 --name "Industry Candidate Identity & Talent Profile"`
- `rg -n "CAND-CAP-0017|MOD-0322" services/Diten.TalentEcosystemService frontend gateway tests`

Candidate gate note:

- Candidate gate was not executable during pack preparation because
  `.antigravity/scripts/verify_module_id.py` was not present in this checkout.
  Reservation is recorded in `execution/registries/module-id-registry.md` and
  `execution/portfolio/blueprint-master-plan-reconciliation.md`.

Runtime validation commands:

- `dotnet build services/Diten.TalentEcosystemService/src/Diten.TalentEcosystemService.Api/Diten.TalentEcosystemService.Api.csproj -c Debug --no-restore /nr:false -m:1 --tl:off`
- TEP candidate profile targeted tests must pass.
- Full TEP Application tests must pass.
- Production in-memory repository scan must return no matches under
  `services/Diten.TalentEcosystemService/src`.

Frontend/DataTable/l10n verifier: N/A because `shell: none` and
`golden_reference: none`.

## 18. Governance Approval and Runtime-Ready Checklists

### Governance Approval Checklist

- [x] EA/registry owner reservation exists for `CAND-CAP-0017`.
- [x] `MOD-0322` remains blocked and must not be used.
- [x] `CAND-CAP-0017` remains governance/documentation identity only.
- [x] TEP domain config exists.
- [x] `CAND-CAP-0011` TEP Shell dependency is `done`.
- [x] `CAND-CAP-0012` Association dependency is `done`.
- [x] `CAND-CAP-0013` Consent/Visibility dependency is `done`.
- [x] `CAND-CAP-0014` Verified Access dependency is `done`.
- [x] `CAND-CAP-0015` Review Board dependency is `done`.
- [x] `CAND-CAP-0016` Trust Level dependency is `done`.
- [x] HCM foundation completion dependencies are listed as context.
- [x] UI/frontend/gateway remains closed.
- [x] Candidate profile ownership runtime remains outside this pack.
- [x] Talent profile runtime remains outside this pack.
- [x] Candidate/talent identity privacy, legal, consent, data-scope, and
  data-minimization blockers are retained for runtime-ready.

Open blockers for governance continuation: none.

Governance note: candidate gate script is not present in this checkout;
EA/registry owner accepted verifier unavailability as a governance note for
governance-only approval. The gate must be rerun if/when the verifier is
restored before implementation review or future canonicalization.

Governance-only approval reconciliation: EA approved candidate-based governance
continuation for `CAND-CAP-0017`. This approval is limited to the industry
candidate identity / talent profile boundary and R2-C sequencing contract; it
remains the reservation baseline; the runtime-ready reconciliation below
authorizes only the first metadata-only backend/API slice.

### Runtime-Ready Checklist

- [x] EA candidate-runtime waiver approved for `CAND-CAP-0017`.
- [x] Runtime owner/key approved: `tep.candidate-profiles`.
- [x] Runtime permission namespace approved:
  `tep.candidate-profiles.read`, `tep.candidate-profiles.manage`,
  `tep.candidate-profiles.evaluate`, `tep.candidate-profiles.audit.read`.
- [x] Runtime repo scope approved: `services/Diten.TalentEcosystemService/**`.
- [x] Minimal candidate identity metadata field list approved.
- [x] Minimal talent profile metadata field list approved.
- [x] Candidate consent basis approved for metadata-only first slice.
- [x] Visibility and data-scope policy approved through CAND-CAP-0013
  precondition.
- [x] Data-minimization and sensitive field exclusion policy approved
  fail-closed.
- [x] Association dependency consumption contract approved.
- [x] Consent/visibility policy precondition contract approved.
- [x] Verified access dependency contract approved.
- [x] Review board and trust-level dependency behavior approved.
- [x] HCM foundation validation/consumption boundary approved as dependency
  context only.
- [x] Audit/evidence/retention metadata behavior approved as local/deferred
  metadata; real integration is future follow-up.
- [x] Gateway direct edit remains closed; integration-agent follow-up only if
  needed.
- [x] UI/frontend remains closed.
- [x] Runtime literal scan requirement retained for `CAND-CAP-0017|MOD-0322`.

Open blockers: none for the approved first backend/API candidate identity and
talent profile metadata slice.

Runtime-ready reconciliation: EA candidate-runtime policy waiver approved.
`CAND-CAP-0017` remains a governance/documentation identity and `MOD-0322`
remains blocked; neither may appear in runtime literals. Runtime owner/key is
`tep.candidate-profiles`, permission namespace is limited to the four
permissions listed above, and scope is limited to
`services/Diten.TalentEcosystemService/**`.

Done promotion reconciliation: done-promotion readiness review PASS after
read-only implementation review retry PASS. Previous contract drift is fixed:
`ProfileCompletenessState`,
`CredentialSummaryMetadata`, `VisibilityClassification`, and
`ProfilePolicyEvaluationState` are present in the runtime contract; public
CandidateProfile contract exposes `VerifiedParticipantId`; and
`VerifiedParticipantAccessId` is removed from the CandidateProfiles public/API/
Application/entity contract. Build PASS with 0 warnings and 0 errors;
CandidateProfile targeted tests PASS 20/20; full TEP Application tests PASS
121/121; runtime literal scan PASS for `CAND-CAP-0017|MOD-0322`; production
in-memory repository scan PASS; raw/PII-heavy persistence scan PASS; frontend/
Gateway/HCM/PSS scope remains closed.

## 19. Implementation Notes

- DCP-008 lists Industry Candidate Identity & Talent Profile in R2-C with
  legacy Excel ID `MOD-0322` and output role "Sector candidate identity".
- DCP-002 blocks `MOD-0322` for TEP use because it is not Blueprint-backed and
  has no canonical registry row.
- EA/registry owner reservation is recorded for `CAND-CAP-0017` in
  `execution/registries/module-id-registry.md` and
  `execution/portfolio/blueprint-master-plan-reconciliation.md`.
- `CAND-CAP-0017` remains a governance/documentation identity only and
  `MOD-0322` remains blocked; neither may appear in runtime literals.
- `CAND-CAP-0011`, `CAND-CAP-0012`, `CAND-CAP-0013`, `CAND-CAP-0014`,
  `CAND-CAP-0015`, and `CAND-CAP-0016` are completed TEP dependencies.
- HCM foundation is available as prerequisite context, but this pack does not
  consume HCM runtime APIs or create HCM ownership.
- Runtime implementation is open only for the first metadata-only backend/API
  candidate identity and talent profile contract slice under
  `services/Diten.TalentEcosystemService/**`.
- Candidate gate script is not present in this checkout; reservation evidence is
  the registry and reconciliation ledger.

## 20. Follow-up Items

- Run candidate gate if `.antigravity/scripts/verify_module_id.py` is restored.
- Start implementation only through `@orchestrator` and only within
  `services/Diten.TalentEcosystemService/**`.
- Decide future EA canonical MOD assignment.
- Keep real candidate account/profile-body ownership runtime as future
  follow-up.
- Keep frontend/Gateway exposure as future follow-up unless separately
  authorized.
- Keep frontend/Gateway closed unless a separate approved slice is created.
- Keep real audit/evidence/retention integration as future follow-up.
- Keep candidate-facing UX, reference exchange, reputation/risk analytics,
  marketplace behavior, and recommendation modules as future follow-up.
