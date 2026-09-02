---
id: CAND-CAP-0018
name: Industry Exit Reference Record Registry
domain: talent-ecosystem-platform
service: Diten.TalentEcosystemService
shell: none
golden_reference: none
entity_base: BaseEntity
status: done
owner: enterprise-architect / tep-domain-owner / platform-team / security-owner / legal-owner
branch: feature/tep/cand-cap-0018-industry-exit-reference-record-registry
started: 2026-08-28
target: 2026-11-04
form_field_count: 0
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/blueprint-master-plan-reconciliation.md
  - execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md
candidate_identity: CAND-CAP-0018
legacy_excel_id: MOD-0327
canonicalization_status: candidate / pending-EA
runtime_service_candidate: Diten.TalentEcosystemService
bootstrap_type: runtime-ready-first-slice
runtime_owner_key: tep.exit-reference-records
permission_namespace: tep.exit-reference-records.*
runtime_repo_scope: services/Diten.TalentEcosystemService/**
---

# CAND-CAP-0018 - Industry Exit Reference Record Registry

> Status: done. The first metadata-only backend/API exit reference record
> registry contract slice under `services/Diten.TalentEcosystemService/**` is
> implemented, reviewed, and closed. This pack does not authorize frontend,
> Gateway, reference exchange marketplace, rehire recommendation, candidate
> dispute or response workflow runtime, raw provider payload persistence,
> credential/token/secret persistence, or PII-heavy persistence. `CAND-CAP-0018`
> remains a governance/documentation identity only and must never be written into
> runtime literals. `MOD-0327` remains blocked for TEP use.

## 1. Module Summary

`CAND-CAP-0018` reserves the temporary DCP-002 candidate identity for the native
Talent Ecosystem Platform R2-D capability named `Industry Exit Reference Record
Registry`.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`.
DCP-008 places this row in R2-D with legacy Excel ID `MOD-0327` and output role
"Cross-company exit reference records". DCP-002 blocks `MOD-0327` because it is
absent from the Blueprint and has no canonical registry row. EA/registry owner
reservation recorded `CAND-CAP-0018` as a temporary governance identity pending
future canonical MOD allocation.

This done pack records the implemented narrow metadata-only registry contract
for exit reference records. Cross-company sharing, legal/privacy/consent,
retention/evidence, audit/legal-hold/deletion, and review/dispute decisions are
approved only as local/deferred metadata boundaries for the first slice.

## 2. Ownership and Boundaries

Owned by this done first slice:

- TEP-native metadata-only industry exit reference record registry boundary.
- Cross-company exit reference record metadata boundary with local/deferred
  sharing state only.
- Sequencing after the completed TEP R2-A/R2-B/R2-C foundation.
- Boundary between local/deferred exit reference metadata planning and later
  reference exchange marketplace behavior.
- Legal/privacy/consent, retention/evidence, and review/dispute behavior as
  metadata-only waiver states for the first slice.

Not owned by this pack:

- Runtime exit reference exchange, publication, or external consumption.
- Reference exchange marketplace.
- Rehire recommendation network.
- Candidate dispute or response workflow runtime.
- Verified participant, candidate profile, association, consent/visibility,
  review-board, trust-level, or multi-signature implementation.
- HCM employee projection, offboarding, sensitive access, or position assignment
  implementation.
- PSS Organization, Person & Position Directory or external provider ownership.
- Frontend, Gateway, raw/sensitive payload persistence, broad workflow engine,
  broad audit/evidence/retention engine, or RBAC/ABAC engine copy.

## 3. Owned Objects

First-slice metadata objects:

| Object | Type | Purpose |
|---|---|---|
| IndustryExitReferenceRecordMetadata | Runtime metadata contract | Defines the native TEP metadata-only registry record. |
| CrossCompanyReferenceSharingSnapshot | Runtime metadata contract | Stores local/deferred sharing state without external exchange. |
| ExitReferenceConsentPreconditionSnapshot | Runtime metadata contract | Captures consent/visibility/data-scope precondition state. |
| ExitReferenceEvidenceRetentionSnapshot | Runtime metadata contract | Records local/deferred evidence, retention, audit, legal-hold, and deletion state. |
| ExitReferenceReviewDisputeSnapshot | Runtime metadata contract | Captures review/dispute/candidate-response boundary state without workflow runtime. |

No reference exchange marketplace, rehire recommendation, candidate
dispute/response workflow runtime, external sharing integration, or raw evidence
store is authorized by this pack.

## 4. Entity Fields

Authorized minimal metadata fields:

- `Code`
- `DisplayName`
- `CandidateProfileReference`
- `OffboardingCaseReference`
- `VerifiedParticipantReference`
- `AssociationMembershipReference`
- `ConsentVisibilityPolicyReference`
- `ReviewBoardCaseReference`
- `TrustLevelPolicyReference`
- `ReferenceRecordState`
- `ReferenceSharingState`
- `ConsentPreconditionState`
- `VisibilityApprovalState`
- `DataScopeState`
- `EvidenceRetentionState`
- `ReviewDisputeBoundaryState`
- `DependencyStates`
- `SourceContractVersion`
- `LastEvaluatedAt`
- `ReferenceRecordVersion`
- `DeferredReason`

Forbidden for this pack:

- `CAND-CAP-0018` or `MOD-0327` as runtime module literals.
- Raw HRIS, payroll, time, provider, offboarding, or candidate profile payloads.
- Credentials, tokens, secrets, bank/tax/payroll, payslip,
  biometric/geolocation, national ID, DOB, home address, or PII-heavy fields.
- Reference exchange marketplace, rehire recommendation, candidate dispute or
  response workflow runtime fields.

## 5. Repo Scope

Authorized runtime scope for this done first slice:

- `services/Diten.TalentEcosystemService/**`

No changes are authorized under:

- `services/Diten.HumanCapitalService/**`
- `services/Diten.Platform/**`
- `frontend/**`
- `gateway/**`
- any service path outside `services/Diten.TalentEcosystemService/**`.

## 6. Protected Paths

This draft pack must not change:

- `services/**`
- `frontend/**`
- `gateway/**`
- `tests/**`
- `execution/domains/platform-shared-services/**`
- `execution/domains/human-capital-management/**`
- completed TEP foundation packs,
- `execution/domains/talent-ecosystem-platform/domain-config.md`,
- `.antigravity/**`.

Registry and reconciliation files may only be changed by separate EA/registry
owner reservation or canonicalization tasks.

## 7. Dependencies

Governance dependencies:

- `execution/domains/talent-ecosystem-platform/domain-config.md`
- `execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`
- `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`
- `execution/registries/module-id-registry.md`
- `execution/portfolio/blueprint-master-plan-reconciliation.md`
- `execution/portfolio/frontend-completion-reconciliation.md`

Completed TEP foundation dependencies:

- `CAND-CAP-0011` - Talent Ecosystem Platform Shell, `done`.
- `CAND-CAP-0012` - Association Membership & Member Company Registry, `done`.
- `CAND-CAP-0013` - Consent, Visibility & Access Policy, `done`.
- `CAND-CAP-0014` - Verified HR Participant & Company Access, `done`.
- `CAND-CAP-0015` - Talent Ecosystem Governance & Review Board, `done`.
- `CAND-CAP-0016` - Trust Level & Multi-Signature Engine, `done`.
- `CAND-CAP-0017` - Industry Candidate Identity & Talent Profile, `done`.

HCM foundation dependency context:

- `CAND-CAP-0007` - Employee Profile & Employment Record Projection, `done`.
- `CAND-CAP-0008` - HR Governance & Sensitive Access Controls, `done`.
- `CAND-CAP-0009` - Position & Organization Assignment, `done`.
- `CAND-CAP-0010` - Offboarding & Exit Management, `done`.

PSS/backbone context:

- `MOD-0288` - Organization, Person & Position Directory.
- `MOD-0251` - HRIS External SoR.
- `MOD-0279` - Payroll Engine External SoR.
- `MOD-0280` - Time & Attendance External Provider.
- `MOD-0281` - Payroll Integration & Governance.

PSS and HCM dependencies remain context/substrate only and do not transfer
ownership into this TEP-native capability.

## 8. Upstream Inputs

- DCP-008 R2-D row: `MOD-0327` / `Industry Exit Reference Record Registry` /
  "Cross-company exit reference records".
- Frontend completion reconciliation PASS dated 2026-08-28.
- EA/registry reservation for `CAND-CAP-0018` in
  `execution/registries/module-id-registry.md`.
- Reconciliation ledger reservation in
  `execution/portfolio/blueprint-master-plan-reconciliation.md`.
- DCP-002 candidate namespace update for `CAND-CAP-0018`.
- Completed CAND-CAP-0011 through CAND-CAP-0017 module packs.

## 9. Downstream Consumers

Future consumers may include:

- Reference Exchange Marketplace.
- Rehire Recommendation Network.
- Candidate Response & Dispute Management.
- Future candidate-facing response and visibility experiences.
- Future HR-facing reference consumption workflows.
- Future audit/evidence/retention and legal hold integrations.

Downstream runtime integration remains closed except for local/deferred metadata
references needed by the first exit reference record registry contract slice.

## 10. Runtime Constraints

Runtime implementation is authorized only for the first metadata-only exit
reference record registry contract slice under
`services/Diten.TalentEcosystemService/**`.

Runtime constraints:

- `CAND-CAP-0018` remains a governance/documentation identity only.
- `MOD-0327` remains blocked and must not be used as a runtime literal.
- Runtime owner/key is `tep.exit-reference-records`.
- Runtime permissions are limited to `tep.exit-reference-records.read`,
  `tep.exit-reference-records.manage`, `tep.exit-reference-records.evaluate`,
  and `tep.exit-reference-records.audit.read`.
- Runtime repo scope is limited to `services/Diten.TalentEcosystemService/**`.
- Cross-company sharing is local/deferred metadata only; no external sharing or
  exchange integration is approved.
- Legal/privacy/consent basis is approved only through metadata-only waiver
  state.
- Retention/evidence/audit/legal-hold/deletion behavior is approved only as
  local/deferred metadata.
- Review/dispute/candidate-response behavior is out of scope except for
  precondition/deferred metadata.
- CAND-CAP-0012 Association, CAND-CAP-0013 Consent/Visibility,
  CAND-CAP-0014 Verified Access, CAND-CAP-0015 Review Board, CAND-CAP-0016
  Trust Levels, and CAND-CAP-0017 Candidate Profiles must be consumed as
  precondition contracts only.
- No reference exchange marketplace or rehire recommendation runtime is
  approved.
- No dispute workflow runtime is approved.
- No raw provider payload, credential/token/secret, or PII-heavy persistence is
  approved.

## 11. Frontend File Contract

Frontend implementation is N/A.

No Razor, JavaScript, DataTable, RESX, menu, or frontend route work is
authorized. Any future UI must be a separate approved slice after runtime-ready,
legal/privacy/consent, cross-company sharing, and retention/evidence decisions
are closed.

## 12. Data, Security, and Privacy

Runtime data persistence is authorized only for the minimal metadata fields in
Section 4.

Approved first-slice data/privacy policy:

- cross-company sharing is metadata-only and local/deferred,
- legal/privacy/consent basis is metadata-only waiver state,
- consent/visibility/data-scope precondition behavior comes from CAND-CAP-0013,
- candidate profile and verified participant linkage is reference metadata only,
- review-board escalation and dispute/response behavior is deferred metadata
  only,
- evidence retention, audit, legal hold, and deletion behavior is local/deferred
  metadata only,
- sensitive field exclusion and data minimization must fail closed.

Raw HRIS/provider payloads, payroll/time/provider payloads, credentials, tokens,
secrets, national ID, DOB, home address, biometric/geolocation data,
bank/tax/payroll data, payslips, and PII-heavy profile details remain forbidden.

## 13. Failure Path to Verify

Before implementation or review promotion, verify these failure paths remain
blocked:

- Reusing `MOD-0327` for TEP.
- Treating `CAND-CAP-0018` as a runtime module ID.
- Creating frontend or Gateway routes.
- Creating reference exchange marketplace, rehire recommendation, or candidate
  dispute/response workflow runtime.
- Treating HCM offboarding or PSS directory records as the exit reference
  registry owner.
- Persisting raw provider payloads, credentials, tokens, secrets, or PII-heavy
  data.
- Enabling cross-company sharing without legal/privacy/consent, visibility, and
  retention/evidence decisions.

## 14. Authorization Convention

Runtime owner/key:

- `tep.exit-reference-records`

Approved permission namespace:

- `tep.exit-reference-records.read`
- `tep.exit-reference-records.manage`
- `tep.exit-reference-records.evaluate`
- `tep.exit-reference-records.audit.read`

Only these permission strings may be used for the first metadata-only exit
reference record registry slice.

## 15. Gateway / API Routing Decision

Gateway routing is N/A for this backend/API first slice.

No Ocelot route may be created. If future external API exposure is needed, the
Gateway route must be handled by the integration-agent workflow after a
separate gateway/frontend exposure slice is explicitly approved after
done-promotion readiness.

## 16. Acceptance Criteria

1. AC-01: EA/registry owner reservation exists for `CAND-CAP-0018`.
2. AC-02: Pack status is `done` after the metadata-only backend/API slice,
   implementation review retry, contract drift fix, and done-promotion
   readiness audit pass with no blockers.
3. AC-03: `MOD-0327` is not used as the TEP identity.
4. AC-04: `CAND-CAP-0018` is documented as governance/documentation identity
   only and not a runtime literal.
5. AC-05: Candidate gate status is recorded; if the script is unavailable, the
   pack records "candidate gate not executable in this checkout; reservation
   recorded".
6. AC-06: Runtime literal scan for `CAND-CAP-0018|MOD-0327` across runtime,
   frontend, gateway, and tests returns no matches.
7. AC-07: `CAND-CAP-0011` through `CAND-CAP-0017` are listed as completed TEP
   foundation dependencies.
8. AC-08: HCM foundation dependencies are listed as context only.
9. AC-09: PSS/backbone dependencies are listed as context only.
10. AC-10: Exit reference record registry runtime is limited to metadata-only
    first-slice contract fields.
11. AC-11: Cross-company sharing is local/deferred metadata only.
12. AC-12: Legal/privacy/consent basis is represented as approved
    metadata-only waiver state.
13. AC-13: Retention/evidence/audit/legal-hold/deletion behavior is
    local/deferred metadata only.
14. AC-14: Review/dispute/candidate-response runtime is out of scope except
    for precondition/deferred metadata.
15. AC-15: Reference exchange marketplace is explicitly out of scope.
16. AC-16: Rehire recommendation is explicitly out of scope.
17. AC-17: Candidate dispute/response workflow runtime is explicitly out of
    scope.
18. AC-18: Raw provider payload, credential/token/secret, and PII-heavy
    persistence are explicitly out of scope.
19. AC-19: Frontend/Gateway remain closed.
20. AC-20: Runtime/API/controller/entity/repository/database work is authorized
    only under `services/Diten.TalentEcosystemService/**` for the first
    metadata-only contract slice.

## 17. Test Expectations

Governance validation commands:

- `python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0018 --name "Industry Exit Reference Record Registry"`
- `rg -n "CAND-CAP-0018|MOD-0327" services/Diten.TalentEcosystemService frontend gateway tests`

Candidate gate note:

- Candidate gate was not executable during pack preparation because
  `.antigravity/scripts/verify_module_id.py` was not present in this checkout.
  Reservation is recorded in `execution/registries/module-id-registry.md`,
  `execution/portfolio/blueprint-master-plan-reconciliation.md`, and
  `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`.

Runtime validation commands:

- `dotnet build services/Diten.TalentEcosystemService/src/Diten.TalentEcosystemService.Api/Diten.TalentEcosystemService.Api.csproj -c Debug --no-restore /nr:false -m:1 --tl:off`
- TEP exit reference record targeted tests must pass.
- Full TEP Application tests must pass.
- Production in-memory repository scan must return no matches under
  `services/Diten.TalentEcosystemService/src`.

Frontend/DataTable/l10n verifier: N/A because `shell: none` and
`golden_reference: none`.

## 18. Governance Approval and Runtime-Ready Checklists

### Governance Approval Checklist

- [x] EA/registry owner reservation exists for `CAND-CAP-0018`.
- [x] `MOD-0327` remains blocked and must not be used.
- [x] `CAND-CAP-0018` remains governance/documentation identity only.
- [x] TEP domain config exists.
- [x] `CAND-CAP-0011` TEP Shell dependency is `done`.
- [x] `CAND-CAP-0012` Association dependency is `done`.
- [x] `CAND-CAP-0013` Consent/Visibility dependency is `done`.
- [x] `CAND-CAP-0014` Verified Access dependency is `done`.
- [x] `CAND-CAP-0015` Review Board dependency is `done`.
- [x] `CAND-CAP-0016` Trust Level dependency is `done`.
- [x] `CAND-CAP-0017` Candidate Profile dependency is `done`.
- [x] HCM foundation completion dependencies are listed as context.
- [x] PSS/backbone dependencies are listed as context.
- [x] UI/frontend/gateway remains closed.
- [x] Runtime implementation remains closed.
- [x] Reference exchange, rehire recommendation, and dispute/response workflow
  runtime remain outside this pack.
- [x] Cross-company sharing, legal/privacy/consent, retention/evidence, and
  review/dispute blockers are retained for runtime-ready.

Open blockers for governance continuation: none.

Governance note: candidate gate script is not present in this checkout;
EA/registry owner accepted verifier unavailability as a governance note.
Reservation evidence is the registry, reconciliation ledger, and DCP-002 update.
The gate must be rerun if/when the verifier is restored before runtime-ready
promotion, implementation review, or future canonicalization.

Governance-only approval reconciliation: EA approved candidate-based governance
continuation for `CAND-CAP-0018`. This approval is limited to the industry exit
reference record registry boundary and R2-D sequencing contract. It is not
runtime-ready and does not authorize backend/API, service scaffold, frontend, or
Gateway work.

### Runtime-Ready Checklist

- [x] EA candidate-runtime waiver approved for `CAND-CAP-0018`.
- [x] Runtime owner/key approved: `tep.exit-reference-records`.
- [x] Runtime permission namespace approved:
  `tep.exit-reference-records.read`, `tep.exit-reference-records.manage`,
  `tep.exit-reference-records.evaluate`, and
  `tep.exit-reference-records.audit.read`.
- [x] Runtime repo scope approved: `services/Diten.TalentEcosystemService/**`.
- [x] Minimal metadata field list approved.
- [x] Cross-company sharing model approved as local/deferred metadata only.
- [x] Legal/privacy/consent basis approved as metadata-only waiver state.
- [x] Consent/visibility/data-scope precondition behavior approved.
- [x] Candidate profile and verified participant linkage behavior approved as
  reference metadata only.
- [x] HCM offboarding dependency consumption behavior approved as reference
  metadata only.
- [x] Association dependency consumption behavior approved.
- [x] Review Board escalation and dispute/response boundary approved as
  precondition/deferred metadata only.
- [x] Trust-level dependency behavior approved.
- [x] Evidence retention, audit, legal hold, and deletion behavior approved as
  local/deferred metadata only.
- [x] Raw/sensitive/PII-heavy persistence exclusion approved.
- [x] Gateway/frontend remain closed.
- [x] Runtime literal scan requirement retained for `CAND-CAP-0018|MOD-0327`.

Open blockers: none for the approved first backend/API metadata-only exit
reference record registry contract slice.

Runtime-ready reconciliation: EA candidate-runtime policy waiver approved.
`CAND-CAP-0018` remains a governance/documentation identity and `MOD-0327`
remains blocked; neither may appear in runtime literals. Runtime owner/key is
`tep.exit-reference-records`, permission namespace is limited to the four
permissions listed above, and scope is limited to
`services/Diten.TalentEcosystemService/**`. Cross-company sharing,
legal/privacy/consent, retention/evidence/audit/legal-hold/deletion, and
review/dispute/candidate-response behavior are approved only as metadata-only
local/deferred/precondition states for the first slice.

Review-ready reconciliation: implementation review retry found no blockers
after the contract drift fix. Extra dependency validation state fields were
removed from the ExitReference public and persisted contract; dependency and
precondition status now flows only through `DependencyStates`. Build passed,
ExitReference targeted tests passed, full TEP Application tests passed, runtime
literal scan for `CAND-CAP-0018|MOD-0327` returned no matches, and production
in-memory repository scan returned no matches. Frontend, Gateway, HCM, PSS, and
Platform scopes remain closed.

Done-promotion reconciliation: done-promotion readiness audit PASS. Pack status
advanced to `done`; open blockers remain `none`. Runtime owner/key remains
limited to `tep.exit-reference-records`, and permission namespace remains
limited to `tep.exit-reference-records.read`,
`tep.exit-reference-records.manage`, `tep.exit-reference-records.evaluate`, and
`tep.exit-reference-records.audit.read`. Contract drift fix evidence is
retained: dependency/precondition state is carried only by `DependencyStates`,
and no extra ExitReference validation state fields are exposed or persisted.
Build PASS with 0 warnings and 0 errors; ExitReference targeted tests PASS
21/21; full TEP Application tests PASS 142/142; runtime literal scan for
`CAND-CAP-0018|MOD-0327` returned no matches; production in-memory repository
scan returned no matches. Frontend/Gateway remain future follow-up only.

## 19. Implementation Notes

- DCP-008 lists Industry Exit Reference Record Registry in R2-D with legacy
  Excel ID `MOD-0327` and output role "Cross-company exit reference records".
- DCP-002 blocks `MOD-0327` for TEP use because it is not Blueprint-backed and
  has no canonical registry row.
- EA/registry owner reservation is recorded for `CAND-CAP-0018`.
- `CAND-CAP-0018` remains a governance/documentation identity only and
  `MOD-0327` remains blocked; neither may appear in runtime literals.
- `CAND-CAP-0011` through `CAND-CAP-0017` are completed TEP dependencies.
- HCM foundation and PSS/backbone modules remain context only.
- Candidate gate script is not present in this checkout; EA/registry owner
  accepted verifier unavailability as a governance note. Reservation evidence is
  the registry, reconciliation ledger, and DCP-002 update.
- Runtime implementation for the first metadata-only backend/API exit reference
  record registry contract slice exists under
  `services/Diten.TalentEcosystemService/**` and is done.
- Contract drift fix is closed: dependency/precondition state is represented
  only by `DependencyStates`; no extra ExitReference validation state fields are
  exposed or persisted by the CAND-CAP-0018 runtime contract.
- Frontend, Gateway, reference exchange marketplace, rehire recommendation, and
  dispute workflow runtime remain closed.

## 20. Follow-up Items

- Run candidate gate if `.antigravity/scripts/verify_module_id.py` is restored.
- Keep this done pack as the closed first metadata-only backend/API slice.
- Decide future EA canonical MOD assignment.
- Add Gateway exposure only through a separate integration-agent slice after
  done-promotion readiness.
- Add restricted read-only frontend only through a separate frontend slice after
  Gateway exposure.
- Integrate real audit/evidence/retention, legal hold, deletion, and
  cross-company sharing hardening only through future approved packs.
- Keep reference exchange marketplace, rehire recommendation, and
  candidate-response/dispute workflow runtime as separate future modules.
- Keep frontend/Gateway exposure closed unless a separate approved slice is
  created after done-promotion readiness.
