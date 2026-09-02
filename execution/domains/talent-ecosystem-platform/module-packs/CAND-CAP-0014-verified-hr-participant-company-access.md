---
id: CAND-CAP-0014
name: Verified HR Participant & Company Access
domain: talent-ecosystem-platform
service: Diten.TalentEcosystemService
shell: none
golden_reference: none
entity_base: BaseEntity
status: done
owner: enterprise-architect / tep-domain-owner / platform-team / security-owner / legal-owner
branch: feature/tep/cand-cap-0014-verified-hr-participant-company-access
started: 2026-08-24
target: 2026-10-14
form_field_count: 0
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/blueprint-master-plan-reconciliation.md
shell_dependency: execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0011-talent-ecosystem-platform-shell.md
association_dependency: execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0012-association-membership-member-company-registry.md
consent_visibility_dependency: execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0013-consent-visibility-access-policy.md
candidate_identity: CAND-CAP-0014
legacy_excel_id: MOD-0324
canonicalization_status: candidate / pending-EA
runtime_service_candidate: Diten.TalentEcosystemService
bootstrap_type: runtime-ready metadata-only backend/API slice
runtime_owner_key: tep.verified-participants
permission_namespace: tep.verified-participants.*
---

# CAND-CAP-0014 - Verified HR Participant & Company Access

> Status: done. First metadata-only TEP backend/API verified HR participant
> and member company access contract slice is implemented, reviewed, and closed.
> This pack does not authorize frontend, Gateway,
> HCM/PSS implementation changes, candidate/talent identity, reference exchange,
> reputation/risk/analytics, dispute/review board, broad verification or consent
> engines, real verified-company external integration, or raw/sensitive payload
> persistence. `CAND-CAP-0014` remains a governance/documentation identity only
> and must never be written into runtime literals. `MOD-0324` remains blocked for
> TEP use.

## 1. Module Summary

`CAND-CAP-0014` reserves the temporary DCP-002 candidate identity for the native
Talent Ecosystem Platform R2-A capability named `Verified HR Participant &
Company Access`.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`.
DCP-008 places this row in R2-A after TEP Shell and Association membership as
legacy Excel ID `MOD-0324`, with output role "Verified HR/company access".
DCP-002 blocks `MOD-0324` because it is absent from the Blueprint and has no
canonical registry row. EA/registry owner approval reserved `CAND-CAP-0014` as
a temporary governance identity pending future canonical MOD allocation.

This done pack records the completed narrow metadata-only backend/API contract
for verifying HR participant and member company access after the completed TEP
foundation packs: `CAND-CAP-0011`, `CAND-CAP-0012`, and `CAND-CAP-0013`. It
does not create candidate/talent identity, external verified-company
integration, broad access grants, or user-facing exchange behavior.

## 2. Ownership and Boundaries

Owned by this done pack:

- TEP-native verified HR participant metadata contract.
- TEP-native member company access metadata contract.
- Association/member-company verification access precondition metadata.
- Consent/visibility policy precondition metadata.
- Dependency mapping to completed TEP foundation and HCM foundation context.

Not owned by this pack:

- Candidate/talent identity, talent profile, candidate account, sector talent
  profile ownership, or talent marketplace identity.
- Association membership registry implementation; `CAND-CAP-0012` owns that
  completed first metadata registry slice.
- Consent/visibility/access policy implementation; `CAND-CAP-0013` owns that
  completed first policy decision contract slice.
- HCM employee projection, sensitive access, position/org assignment, or
  offboarding ownership.
- PSS person/org/position directory ownership or external provider ownership.
- Reference exchange, reputation, risk, analytics, dispute, review board, broad
  consent engine, RBAC/ABAC engine copy, verified-company external integration,
  frontend, Gateway, or raw/sensitive payload persistence.

## 3. Owned Objects

Authorized metadata-only runtime planning objects:

| Object | Type | Purpose |
|---|---|---|
| TepVerifiedParticipantAccess | Metadata entity | Stores tenant-scoped verified HR participant/member company access metadata only. |
| TepVerifiedAccessDependencyState | Metadata value object | Records Association, Consent/Visibility, HCM context, audit/evidence/retention dependency states. |
| TepVerifiedAccessEvaluation | API contract | Returns fail-closed or Deferred access evaluation metadata. |
| TepVerifiedAccessAuditMetadata | Metadata value object | Stores local/deferred audit, evidence, and retention references without real MOD-0021/MOD-0028/MOD-0030 integration. |

No candidate/talent identity, association registry ownership, consent engine
ownership, reference exchange, reputation/risk/analytics, dispute/review board,
or external verified-company integration object is authorized.

## 4. Entity Fields

Authorized minimal metadata field list for the first backend/API slice:

- `Code`
- `DisplayName`
- `AssociationMembershipId`
- `MemberCompanyReference`
- `HrParticipantReference`
- `HcmFoundationReference`
- `ConsentVisibilityPolicyId`
- `VerificationState`
- `AccessState`
- `PolicyEvaluationState`
- `VisibilityApprovalState`
- `HcmValidationState`
- `AssociationValidationState`
- `VerifiedCompanyAccessState`
- `DependencyStates`
- `SourceContractVersion`
- `LastEvaluatedAt`
- `VerificationVersion`
- `LocalAuditEvidenceRetentionState`
- `DeferredReason`

Forbidden for this pack:

- `CAND-CAP-0014` or `MOD-0324` as runtime module literals.
- Candidate/talent identity, reference exchange, reputation, risk, analytics,
  dispute, review board, broad consent engine, or verified-company external
  integration fields.
- Raw HRIS/provider payloads, credentials, tokens, secrets, bank/tax/payroll,
  payslip, biometric/geolocation, national ID, DOB, home address, or PII-heavy
  fields.

## 5. Repo Scope

Authorized repo scope for the completed backend/API slice:

- `execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0014-verified-hr-participant-company-access.md`
- `services/Diten.TalentEcosystemService/**`

Runtime implementation remained inside `services/Diten.TalentEcosystemService/**`.
No changes are authorized under `frontend/**`, `gateway/**`, HCM, or PSS
implementation paths.

## 6. Protected Paths

This done pack did not change:

- `services/Diten.HumanCapitalService/**`
- `services/Diten.Platform/**`
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
- `MOD-0288-FU02` - Person Reference Directory Projection.

## 8. Runtime Constraints

- Runtime implementation is authorized only for the first metadata-only
  verified HR participant/member company access backend/API contract slice.
- `CAND-CAP-0014` is a governance identity only and must never be written into
  runtime literals, permission seeds, route metadata, database records, telemetry
  owner fields, config keys, background job owner names, or tests.
- `MOD-0324` must not be used as the TEP identity or remapped to TEP.
- No TEP verified access runtime may bypass Association and Consent/Visibility
  foundation contracts.
- No verified HR participant or member company access state may be trusted from
  raw HRIS, PSS person reference projection, or HCM context alone.
- Runtime owner/key is `tep.verified-participants`.
- Permission namespace is `tep.verified-participants.*`.
- Privacy/legal metadata-only waiver is approved for this first slice only.
- Verified-company access behavior is local/deferred metadata only; real
  integration is a future follow-up.
- Audit/evidence/retention behavior is local/deferred metadata only; real
  integration is a future follow-up.

## 9. Layout & Shell Contract

Current shell decision:

- `shell: none`
- `golden_reference: none`
- UI/frontend: N/A

No TEP verified participant UI, navigation, Razor view, JavaScript, DataTable,
RESX, or menu entry is authorized. Future UI requires a separate module pack.

## 10. Backend File Convention

Backend/API implementation is authorized only under
`services/Diten.TalentEcosystemService/**` for a metadata-only contract slice.
The implementation must follow the repo's 5-layer .NET service convention,
CQRS/MediatR patterns where applicable, `Response<T>` envelope, tenant
server-side resolution, soft delete rules where metadata is persisted, MongoDB
repository/index standards, JWT/RBAC authorization conventions, and
fail-closed/deferred privacy/legal gates.

## 11. Frontend File Contract

Frontend implementation is N/A.

No Razor view, JavaScript file, DataTable contract, menu item, localization
resource, layout binding, shell route, or frontend navigation entry may be
added from this pack.

## 12. Validation Rules

Runtime validation rules:

- DCP-002 candidate gate for `CAND-CAP-0014` and `Verified HR Participant &
  Company Access` must pass before governance approval if the verifier is
  available.
- Candidate gate note: `.antigravity/scripts/verify_module_id.py` was not
  present in this checkout during pack preparation; candidate gate is not
  executable in this checkout; reservation is recorded in the registry and
  reconciliation ledger.
- Runtime literal scan for `CAND-CAP-0014|MOD-0324` across
  `services/Diten.TalentEcosystemService`, `frontend`, `gateway`, and `tests`
  must return no matches.
- `MOD-0324` remains blocked and must not be reused for TEP.
- `CAND-CAP-0014` must remain governance/documentation identity only.
- `CAND-CAP-0011`, `CAND-CAP-0012`, and `CAND-CAP-0013` must remain completed
  dependencies.
- HCM foundation dependencies must remain dependency context only.
- `AssociationMembershipId` must be same-tenant validated against
  CAND-CAP-0012 Association when a contract/repository exists; missing,
  deleted, cross-tenant, or unavailable active association preconditions must
  fail closed or produce explicit `Deferred` metadata without activating
  verified access.
- `ConsentVisibilityPolicyId` must be same-tenant validated against
  CAND-CAP-0013 Consent/Visibility when a contract/repository exists; policy
  approval missing or unavailable must fail closed or produce explicit
  `Deferred` evaluation metadata. Blind verified access is forbidden.
- HCM foundation references are dependency context only; unavailable validation
  must fail closed or produce explicit `Deferred` metadata.
- Candidate/talent identity, reference exchange, reputation/risk/analytics,
  dispute/review board, raw/sensitive payload persistence, frontend, Gateway,
  and service scaffold cannot be inferred from this pack.

## 13. Failure Path to Verify

Before implementation review, verify these failure paths remain blocked:

- Reusing `MOD-0324` for TEP.
- Treating `CAND-CAP-0014` as a runtime module ID.
- Starting runtime/API/controller/entity/repository/database work outside
  `services/Diten.TalentEcosystemService/**`.
- Creating frontend or Gateway routes from this pack.
- Creating verified HR participant access without CAND-CAP-0012 Association and
  CAND-CAP-0013 Consent/Visibility preconditions.
- Treating HCM employee/sensitive access/assignment/offboarding context as TEP
  ownership.
- Starting candidate/talent identity, reference exchange, reputation, risk,
  analytics, dispute, or review board behavior.
- Persisting raw provider payloads, credentials, tokens, secrets, or PII-heavy
  data.

## 14. Authorization Convention

Runtime owner/key: `tep.verified-participants`.

Runtime permission namespace:

- `tep.verified-participants.read`
- `tep.verified-participants.manage`
- `tep.verified-participants.verify`
- `tep.verified-participants.evaluate`
- `tep.verified-participants.audit.read`

`CAND-CAP-0014` and `MOD-0324` must never be used in runtime permission seeds,
route metadata, policy attributes, database records, telemetry owner fields,
config keys, or tests.

## 15. Gateway / API Routing Decision

Gateway routing is N/A.

No Ocelot route may be created. If future external API exposure is needed, the
Gateway route must be handled by the integration-agent workflow after a
separate ready-for-dev runtime module pack is approved and reviewed.

## 16. Acceptance Criteria

1. AC-01: EA/registry owner reservation exists for `CAND-CAP-0014`.
2. AC-02: Pack status is `done` after first metadata-only backend/API contract
   slice implementation and read-only implementation review PASS.
3. AC-03: `MOD-0324` is not used as the TEP identity.
4. AC-04: `CAND-CAP-0014` is documented as governance/documentation identity
   only and not a runtime literal.
5. AC-05: Candidate gate status is recorded; if the script is unavailable, the
   pack records "candidate gate not executable in this checkout; reservation
   recorded".
6. AC-06: Runtime literal scan for `CAND-CAP-0014|MOD-0324` across runtime,
   frontend, gateway, and tests returns no matches.
7. AC-07: `CAND-CAP-0011` TEP Shell dependency is listed as `done`.
8. AC-08: `CAND-CAP-0012` Association dependency is listed as `done`.
9. AC-09: `CAND-CAP-0013` Consent/Visibility dependency is listed as `done`.
10. AC-10: HCM foundation dependencies are listed as context:
    `CAND-CAP-0007`, `CAND-CAP-0008`, `CAND-CAP-0009`, and `CAND-CAP-0010`.
11. AC-11: Verified HR participant and member company access are scoped as TEP
    metadata-only access contract boundaries.
12. AC-12: Association/member-company verification access metadata is included.
13. AC-13: Consent/visibility policy precondition behavior is testable as
    fail-closed or explicit `Deferred` metadata.
14. AC-14: Candidate/talent identity is explicitly out of scope.
15. AC-15: Reference exchange, reputation/risk/analytics, dispute, and review
    board are explicitly out of scope.
16. AC-16: Frontend/Gateway remain closed.
17. AC-17: Runtime/API/controller/entity/repository/database work is scoped only
    to `services/Diten.TalentEcosystemService/**`.
18. AC-18: No service scaffold work is authorized because the service scaffold
    already exists.
19. AC-19: Runtime-ready checklist is closed.
20. AC-20: Open blockers are `none`; EA canonical MOD assignment remains a
    future governance follow-up.

## 17. Test Expectations

Runtime validation commands:

- `python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0014 --name "Verified HR Participant & Company Access"`
- `rg -n "CAND-CAP-0014|MOD-0324" services/Diten.TalentEcosystemService frontend gateway tests`
- `dotnet build services/Diten.TalentEcosystemService/src/Diten.TalentEcosystemService.Api/Diten.TalentEcosystemService.Api.csproj -c Debug --no-restore /nr:false -m:1 --tl:off`
- TEP verified participant/member company access targeted tests.
- Full TEP Application tests.
- Production in-memory repository scan must return no production repository
  registration for this slice.

Candidate gate note:

- Candidate gate was not executable during pack preparation because
  `.antigravity/scripts/verify_module_id.py` was not present in this checkout.
  Reservation is recorded in `execution/registries/module-id-registry.md` and
  `execution/portfolio/blueprint-master-plan-reconciliation.md`.

Test expectations:

- Tenant isolation.
- Duplicate active `Code` tenant scope conflict.
- Soft delete with `IsDeleted` and `DeletedAt`.
- Runtime owner/key remains `tep.verified-participants`.
- Permission checks for `read`, `manage`, `verify`, `evaluate`, and
  `audit.read`.
- Association precondition fail-closed/Deferred behavior.
- Consent/visibility precondition fail-closed/Deferred behavior.
- HCM foundation context unavailable fail-closed/Deferred behavior.
- Verified-company access local/deferred metadata only.
- Audit/evidence/retention local/deferred metadata only.
- No candidate/talent identity, reference exchange, reputation/risk/analytics,
  dispute/review board, raw/sensitive payload persistence, frontend, or Gateway
  scope.

Frontend/DataTable/l10n verifier: N/A because `shell: none` and
`golden_reference: none`.

## 18. Governance Approval and Runtime-Ready Checklists

### Governance Approval Checklist

- [x] EA/registry owner reservation exists for `CAND-CAP-0014`.
- [x] DCP-002 candidate gate PASS recorded, or verifier unavailability accepted
  as governance note.
- [x] `MOD-0324` remains blocked and must not be used.
- [x] `CAND-CAP-0014` remains governance/documentation identity only.
- [x] TEP domain config exists.
- [x] `CAND-CAP-0011` TEP Shell dependency is `done`.
- [x] `CAND-CAP-0012` Association dependency is `done`.
- [x] `CAND-CAP-0013` Consent/Visibility dependency is `done`.
- [x] HCM foundation completion dependencies are listed as context.
- [x] UI/frontend/gateway remains closed.
- [x] Candidate/talent identity remains outside this pack.
- [x] Reference exchange, reputation/risk/analytics, dispute, and review board
  remain outside this pack.
- [x] TEP ownership remains separate from HCM and PSS.

Open blockers for governance continuation: none.

Governance note: DCP-002 candidate gate script is not present in this checkout;
EA/registry owner accepted verifier unavailability for governance continuation.
The gate must be rerun if/when the verifier is restored before any later
runtime-ready decision.

### Runtime-Ready Checklist

- [x] EA candidate-runtime waiver approved for CAND-CAP-0014.
- [x] Runtime owner/key approved: `tep.verified-participants`.
- [x] Runtime permission namespace approved:
  `tep.verified-participants.read`, `tep.verified-participants.manage`,
  `tep.verified-participants.verify`, `tep.verified-participants.evaluate`,
  `tep.verified-participants.audit.read`.
- [x] Runtime repo scope approved: `services/Diten.TalentEcosystemService/**`.
- [x] Minimal verified HR participant and member company access field list
  approved.
- [x] Privacy/legal metadata-only waiver approved.
- [x] Association dependency consumption contract approved.
- [x] Consent/visibility policy precondition contract approved for this module.
- [x] HCM foundation validation/consumption boundary approved.
- [x] Verified-company access behavior approved as local/deferred metadata.
- [x] Audit/evidence/retention metadata behavior approved as local/deferred
  metadata.
- [x] Gateway direct edit remains closed; integration-agent follow-up only if
  needed.
- [x] UI/frontend remains closed.
- [x] Runtime literal scan requirement retained for `CAND-CAP-0014|MOD-0324`.

Open blockers: none.

## 19. Implementation Notes

- DCP-008 lists Verified HR Participant & Company Access in R2-A with legacy
  Excel ID `MOD-0324` and output role "Verified HR/company access".
- DCP-002 blocks `MOD-0324` for TEP use because it is not Blueprint-backed and
  has no canonical registry row.
- EA approved `CAND-CAP-0014` as a temporary candidate identity for governance
  documentation only.
- Runtime-ready reconciliation: EA approved candidate-runtime policy waiver for
  CAND-CAP-0014. `CAND-CAP-0014` remains a governance/documentation identity and
  `MOD-0324` remains blocked; neither may appear in runtime literals.
- Review-ready reconciliation: read-only implementation review PASS. Runtime
  owner/key remained `tep.verified-participants`; permission namespace was
  limited to read/manage/verify/evaluate/audit.read; thin controller, MediatR/
  CQRS, `Response<T>` envelope, server-side tenant resolution, Mongo-backed
  tenant-aware persistence, active tenant Code unique index, soft delete,
  Association and Consent/Visibility fail-closed/Deferred precondition behavior,
  local/deferred verified-company access and audit/evidence/retention metadata,
  build, targeted tests, full TEP application tests, runtime literal scan,
  production in-memory scan, and frontend/Gateway/HCM/PSS scope closure were
  verified.
- Done promotion reconciliation: implementation review PASS; pack status
  advanced to `done`; open blockers remain `none`; build PASS; TEP verified
  participant targeted tests PASS 18/18; full TEP Application tests PASS 66/66;
  runtime literal scan PASS; production in-memory repository scan PASS; and
  frontend/Gateway/HCM/PSS scope remained closed.
- `CAND-CAP-0011`, `CAND-CAP-0012`, and `CAND-CAP-0013` are completed TEP
  dependencies.
- HCM foundation is available as prerequisite context, but this pack does not
  consume HCM runtime APIs or create HCM ownership.
- CAND-CAP-0012 Association and CAND-CAP-0013 Consent/Visibility are required
  preconditions for any future verified access runtime behavior.
- This pack authorizes only the first metadata-only backend/API contract slice
  under `services/Diten.TalentEcosystemService/**`. It does not authorize
  service scaffold, frontend, Gateway, HCM/PSS implementation changes,
  candidate/talent identity, reference exchange, reputation/risk/analytics,
  dispute/review board, broad verification or consent engines, real
  verified-company external integration, or raw/sensitive payload persistence.

## 20. Follow-up Items

- Rerun DCP-002 candidate gate when `.antigravity/scripts/verify_module_id.py`
  is available before final canonicalization or any future identity-sensitive
  governance change.
- Decide future EA canonical MOD assignment for Verified HR Participant &
  Company Access.
- Keep real audit/evidence/retention integration as future follow-up.
- Keep verified-company external integration as future follow-up.
- Route any future Gateway exposure through integration-agent.
