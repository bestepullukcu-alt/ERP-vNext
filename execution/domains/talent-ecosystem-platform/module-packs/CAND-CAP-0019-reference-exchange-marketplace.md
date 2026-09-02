---
id: CAND-CAP-0019
name: Reference Exchange Marketplace
domain: talent-ecosystem-platform
service: Diten.TalentEcosystemService
shell: none
golden_reference: none
entity_base: BaseEntity
status: done
owner: enterprise-architect / tep-domain-owner / platform-team / security-owner / legal-owner
branch: feature/tep/cand-cap-0019-reference-exchange-marketplace
started: 2026-08-29
target: 2026-11-18
form_field_count: 0
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/blueprint-master-plan-reconciliation.md
  - execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md
candidate_identity: CAND-CAP-0019
legacy_excel_id: MOD-0328
canonicalization_status: candidate / pending-EA
runtime_service_candidate: Diten.TalentEcosystemService
bootstrap_type: runtime-ready-first-slice
runtime_owner_key: tep.reference-exchange
permission_namespace: tep.reference-exchange.*
runtime_repo_scope: services/Diten.TalentEcosystemService/**
---

# CAND-CAP-0019 - Reference Exchange Marketplace

> Status: done. The first metadata-only backend/API reference exchange
> marketplace readiness contract slice under
> `services/Diten.TalentEcosystemService/**` passed done-promotion readiness
> review.
> This pack does not authorize Gateway routes, frontend pages, real
> cross-company marketplace request/match/transaction workflow, rehire
> recommendation, candidate dispute/response workflow, notification/document
> repository integration, raw provider payload persistence,
> credential/token/secret persistence, or PII-heavy persistence.
> `CAND-CAP-0019` remains a governance/documentation identity only and must
> never be written into runtime literals. `MOD-0328` remains blocked for TEP
> use until future EA canonical MOD assignment.

## 1. Module Summary

`CAND-CAP-0019` reserves the temporary DCP-002 candidate identity for the native
Talent Ecosystem Platform R2-D capability named `Reference Exchange
Marketplace`.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`.
DCP-008 places this row in R2-D with legacy Excel ID `MOD-0328` and output role
"HR-to-HR reference exchange". DCP-002 blocks `MOD-0328` because it is absent
from the Blueprint and has no canonical registry row. EA/registry owner
reservation records `CAND-CAP-0019` as a temporary governance identity pending
future canonical MOD allocation.

This done-stage pack confirms the narrow metadata-only readiness contract for
reference exchange marketplace governance. Cross-company exchange runtime
remains closed; first-slice behavior is limited to local/deferred metadata
state for eligibility, consent, visibility, trust, review, evidence, retention,
abuse-control, and legal policy readiness.

## 2. Ownership and Boundaries

Owned by this done first slice:

- TEP-native metadata-only readiness boundary for a future reference exchange
  marketplace.
- Local/deferred planning boundary for HR-to-HR reference request, offer,
  discovery, and fulfillment concepts without runtime exchange.
- Dependency sequencing after completed TEP foundation modules.
- Precondition boundary for participant eligibility, consent, visibility, trust
  level, review-board, and exit-reference record readiness.
- Privacy/legal/security waiver state for marketplace readiness metadata.

Not owned by this pack:

- Cross-company reference exchange runtime.
- Reference publication, request matching, bidding, fulfillment, or marketplace
  transaction execution.
- Rehire recommendation network.
- Candidate dispute or response workflow.
- Notification service, notification provider, controlled documents, or
  external document repository implementation.
- Verified participant, candidate profile, association, consent/visibility,
  review-board, trust-level, exit-reference record, HCM, or PSS implementation.
- Frontend, Gateway, real exchange workflow, broad audit/evidence/retention
  engine, notification/document runtime, recommendation runtime, dispute
  runtime, or RBAC/ABAC engine implementation.

## 3. Owned Objects

First-slice metadata objects:

| Object | Type | Purpose |
|---|---|---|
| ReferenceExchangeMarketplaceReadinessMetadata | Runtime metadata contract | Defines the native TEP metadata-only readiness record. |
| ExchangeEligibilityPreconditionSnapshot | Runtime metadata contract | Captures participant, association, verified access, consent, visibility, trust, review, and exit-reference prerequisites. |
| MarketplaceDataScopeSnapshot | Runtime metadata contract | Captures data-scope and minimization state without sensitive payloads. |
| ExchangeAbuseControlSnapshot | Runtime metadata contract | Captures abuse-control, misuse, throttling, and audit readiness as metadata. |
| DeferredExchangeWorkflowSnapshot | Runtime metadata contract | Keeps notification, document, dispute, recommendation, and exchange workflow runtime deferred. |

No Gateway route, Razor page, DataTable, JavaScript, RESX, real marketplace
workflow, notification/document integration, recommendation behavior, dispute
workflow, or service scaffold outside the TEP service is authorized by this
pack.

## 4. Entity Fields

Authorized minimal metadata fields:

- `Code`
- `DisplayName`
- `ExchangeReadinessState`
- `ExchangeAvailabilityState`
- `ParticipantEligibilityState`
- `AssociationMembershipReference`
- `VerifiedParticipantReference`
- `ConsentVisibilityPolicyReference`
- `CandidateProfileReference`
- `ExitReferenceRecordReference`
- `ReviewBoardCaseReference`
- `TrustLevelPolicyReference`
- `ConsentPreconditionState`
- `VisibilityApprovalState`
- `DataScopeState`
- `MinimizationState`
- `LegalPrivacyBasisState`
- `EvidenceRetentionState`
- `AuditReadinessState`
- `AbuseControlState`
- `ThrottlingPolicyState`
- `ReviewDisputeBoundaryState`
- `NotificationDependencyState`
- `DocumentDependencyState`
- `DependencyStates`
- `SourceContractVersion`
- `LastEvaluatedAt`
- `ReferenceExchangeVersion`
- `DeferredReason`

Forbidden for this pack:

- `CAND-CAP-0019` or `MOD-0328` as runtime module literals.
- Raw HRIS, payroll, time, provider, offboarding, exit-reference, candidate
  profile, or marketplace payloads.
- Credentials, tokens, secrets, passwords, bank/tax/payroll data, payslips,
  biometric/geolocation data, national ID, DOB, home address, or PII-heavy
  fields.
- Candidate-facing response, dispute workflow, recommendation, scoring, ranking,
  or marketplace transaction fields.

## 5. Repo Scope

Authorized runtime scope for this done first slice:

- `services/Diten.TalentEcosystemService/**`

No changes are authorized under:

- `services/**`
- `frontend/**`
- `gateway/**`
- `tests/**`
- `execution/domains/platform-shared-services/**`
- `execution/domains/human-capital-management/**`
- completed TEP foundation packs,
- `.antigravity/**`.

Registry and reconciliation files are already updated by the separate
EA/registry owner reservation task and are only sources for this pack.

## 6. Protected Paths

This done pack must not change:

- `services/Diten.HumanCapitalService/**`
- `services/Diten.Platform/**`
- `frontend/Diten.Web/**`
- `gateway/Diten.ApiGateway/**`
- `tests/**`
- external notification/document module packs listed as out of scope,
- completed TEP foundation packs.

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
- `CAND-CAP-0018` - Industry Exit Reference Record Registry, `done`.

HCM dependency context:

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

External modules deferred/out of scope for this pack:

- `MOD-0027` - Notification.
- `MOD-0263` - Notification Provider / Delivery.
- `MOD-0029` - Controlled Documents.
- `MOD-0262` - External Docs Repository.

## 8. Upstream Inputs

- DCP-008 R2-D row: `MOD-0328` / `Reference Exchange Marketplace` /
  "HR-to-HR reference exchange".
- EA/registry reservation for `CAND-CAP-0019` in
  `execution/registries/module-id-registry.md`.
- Reconciliation ledger reservation in
  `execution/portfolio/blueprint-master-plan-reconciliation.md`.
- DCP-002 candidate namespace update for `CAND-CAP-0019`.
- Completed TEP CAND-CAP-0011 through CAND-CAP-0018 module packs.
- Frontend completion and CAND-CAP-0018 end-to-end reconciliation evidence.

## 9. Downstream Consumers

Potential future consumers:

- HR-to-HR reference request and fulfillment workflows.
- Future rehire recommendation network.
- Future candidate response and dispute management.
- Future notification and document-template integrations.
- Future audit/evidence/retention/legal-hold integrations.
- Future employer-facing reference consumption workflows.

Downstream runtime integration remains closed except for the first
metadata-only readiness contract slice. Real exchange runtime requires a later
approved pack.

## 10. Runtime Constraints

Runtime implementation is authorized only for the first metadata-only reference
exchange marketplace readiness contract slice under
`services/Diten.TalentEcosystemService/**`.

Runtime constraints:

- `CAND-CAP-0019` remains a governance/documentation identity only.
- `MOD-0328` remains blocked and must not be used as a runtime literal.
- Runtime owner/key is `tep.reference-exchange`.
- Runtime permissions are limited to `tep.reference-exchange.read`,
  `tep.reference-exchange.manage`, `tep.reference-exchange.evaluate`, and
  `tep.reference-exchange.audit.read`.
- Runtime repo scope is limited to `services/Diten.TalentEcosystemService/**`.
- Cross-company reference exchange runtime is closed.
- Marketplace discovery, matching, request, offer, transaction, fulfillment, and
  external sharing behavior are closed.
- Rehire recommendation is closed.
- Candidate dispute/response workflow is closed.
- Notification, controlled document, and external document repository
  integrations are closed.
- Raw provider payload, credential/token/secret/password, and PII-heavy
  persistence are closed.

## 11. Frontend File Contract

Frontend implementation is N/A.

No Razor, JavaScript, DataTable, RESX, menu, route, API client, or GatewayUrl
consumer work is authorized. Any future UI must be a separate approved slice
after governance-only approval and runtime-ready decisions.

## 12. Data, Security, and Privacy

Runtime data persistence is authorized only for the minimal metadata fields in
Section 4.

Approved first-slice data/privacy policy:

- cross-company exchange runtime is closed and represented only as
  local/deferred metadata,
- legal/privacy/consent basis is metadata-only waiver state,
- consent/visibility/data-scope and minimization behavior must fail closed,
- participant eligibility and verified access are precondition metadata,
- trust-level, review-board, and exit-reference readiness are precondition
  metadata,
- retention/evidence/audit/legal-hold/deletion behavior is local/deferred
  metadata,
- abuse-control/misuse/throttling is metadata-only precondition state,
- candidate dispute/response runtime is out of scope,
- notification/document dependencies are deferred/out of scope.

Forbidden data categories:

- Raw HRIS, payroll, time, provider, candidate, offboarding, exit-reference, or
  marketplace payloads.
- Credential/token/secret/password values.
- Payroll amount/detail, payslip, bank, tax, national ID, DOB, home address,
  biometric/geolocation, or PII-heavy profile/reference details.

## 13. Failure Path to Verify

Before any approval or runtime-ready promotion, verify these failure paths remain
blocked:

- Reusing `MOD-0328` as the TEP identity.
- Treating `CAND-CAP-0019` as a runtime module ID.
- Creating backend/API/controller/entity/repository/database/service scaffold.
- Creating frontend or Gateway routes.
- Starting real cross-company reference exchange runtime.
- Starting rehire recommendation or candidate dispute/response workflow.
- Starting notification or document repository dependencies in this pack.
- Treating HCM, PSS directory, or external provider records as the marketplace
  owner.
- Persisting raw provider payloads, credentials, tokens, secrets, or PII-heavy
  data.

## 14. Authorization Convention

Runtime owner/key:

- Pending runtime-ready decision.

Permission namespace:

- Pending runtime-ready decision.

Future proposals should follow the TEP pattern and remain under a narrow
`tep.*` namespace, but no permission strings are approved by this
governance-approved pack.

## 15. Gateway / API Routing Decision

Gateway routing is N/A.

No Ocelot route may be created. If future external API exposure is approved, the
Gateway route must be handled by a separate integration-agent workflow after
runtime-ready and backend/API implementation gates pass.

## 16. Acceptance Criteria

1. AC-01: EA/registry owner reservation exists for `CAND-CAP-0019`.
2. AC-02: Pack status is `done` for the first metadata-only backend/API
   readiness contract slice.
3. AC-03: Runtime owner/key is `tep.reference-exchange`.
4. AC-04: `CAND-CAP-0019` is documented as governance/documentation identity
   only and not a runtime literal.
5. AC-05: Candidate gate status is recorded; if the script is unavailable, the
   pack records "candidate gate not executable in this checkout; reservation
   recorded".
6. AC-06: Permission namespace is limited to `tep.reference-exchange.read`,
   `tep.reference-exchange.manage`, `tep.reference-exchange.evaluate`, and
   `tep.reference-exchange.audit.read`.
7. AC-07: Runtime literal scan for `CAND-CAP-0019|MOD-0328` across runtime,
   frontend, gateway, and tests returns no matches.
8. AC-08: `CAND-CAP-0011` through `CAND-CAP-0018` are listed as completed TEP
   foundation dependencies.
9. AC-09: HCM foundation dependencies are listed as context only.
10. AC-10: PSS/backbone dependencies are listed as context only.
11. AC-11: External notification/document modules are listed only as deferred
    and out of scope.
12. AC-12: First slice is limited to the Section 4 metadata contract.
13. AC-13: Cross-company reference exchange runtime remains closed.
14. AC-14: Rehire recommendation remains out of scope.
15. AC-15: Candidate dispute/response workflow remains out of scope.
16. AC-16: Frontend/Gateway remain closed.
17. AC-17: Raw provider payload persistence remains forbidden.
18. AC-18: Credential/token/secret/password persistence remains forbidden.
19. AC-19: PII-heavy persistence remains forbidden.
20. AC-20: Data minimization, abuse-control, dependency precondition, and
    local/deferred audit/evidence/retention behavior are runtime-testable.

## 17. Test Expectations

Governance validation commands:

- `python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0019 --name "Reference Exchange Marketplace"`
- `rg -n "CAND-CAP-0019|MOD-0328" services/Diten.TalentEcosystemService frontend gateway tests`

Candidate gate note:

- Candidate gate was not executable during pack preparation because
  `.antigravity/scripts/verify_module_id.py` was not present in this checkout.
  Reservation is recorded in `execution/registries/module-id-registry.md`,
  `execution/portfolio/blueprint-master-plan-reconciliation.md`, and
  `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`.

Frontend and Gateway validation: N/A because this done pack only covers the
first backend/API metadata-only slice; Gateway and frontend remain future
follow-up work.

Runtime validation commands:

- `dotnet build services/Diten.TalentEcosystemService/src/Diten.TalentEcosystemService.Api/Diten.TalentEcosystemService.Api.csproj -c Debug --no-restore /nr:false -m:1 --tl:off`
- TEP reference exchange targeted tests must pass.
- Full TEP Application tests must pass.
- Production in-memory repository scan must return no matches under
  `services/Diten.TalentEcosystemService/src`.

## 18. Governance Approval and Runtime-Ready Checklists

### Governance Approval Checklist

- [x] EA/registry owner reservation exists for `CAND-CAP-0019`.
- [x] `MOD-0328` remains blocked and must not be used.
- [x] `CAND-CAP-0019` remains governance/documentation identity only.
- [x] TEP domain config exists.
- [x] `CAND-CAP-0011` TEP Shell dependency is `done`.
- [x] `CAND-CAP-0012` Association dependency is `done`.
- [x] `CAND-CAP-0013` Consent/Visibility dependency is `done`.
- [x] `CAND-CAP-0014` Verified Access dependency is `done`.
- [x] `CAND-CAP-0015` Review Board dependency is `done`.
- [x] `CAND-CAP-0016` Trust Level dependency is `done`.
- [x] `CAND-CAP-0017` Candidate Profile dependency is `done`.
- [x] `CAND-CAP-0018` Exit Reference Record dependency is `done`.
- [x] HCM foundation completion dependencies are listed as context.
- [x] PSS/backbone dependencies are listed as context.
- [x] Notification/document dependencies are deferred and out of scope.
- [x] Runtime/API/frontend/gateway remains closed.
- [x] Reference exchange runtime, rehire recommendation, and dispute/response
  workflow remain outside this governance-approved pack.
- [x] Raw provider payload, credential/token/secret/password, and PII-heavy
  persistence remain forbidden.
- [x] Candidate gate script unavailability is recorded.
- [x] EA governance-only approval is recorded for candidate-based continuation.

Open blockers for governance continuation: none.

Governance-only approval reconciliation: EA approved candidate-based governance
continuation for `CAND-CAP-0019`. This approval is limited to the Reference
Exchange Marketplace boundary and R2-D sequencing contract. At the
governance-only approval gate, it did not authorize backend/API, service
scaffold, frontend, or Gateway work; subsequent runtime-ready and done
promotion entries below supersede that earlier gate for the first metadata-only
backend/API slice only.

### Runtime-Ready Checklist

- [x] EA candidate-runtime waiver approved for `CAND-CAP-0019`.
- [x] Runtime owner/key approved: `tep.reference-exchange`.
- [x] Runtime permission namespace approved:
  `tep.reference-exchange.read`, `tep.reference-exchange.manage`,
  `tep.reference-exchange.evaluate`, and
  `tep.reference-exchange.audit.read`.
- [x] Runtime repo scope approved: `services/Diten.TalentEcosystemService/**`.
- [x] First metadata-only backend/API readiness contract slice defined.
- [x] Participant eligibility model approved as precondition metadata.
- [x] Consent/visibility/data-scope policy approved as fail-closed metadata.
- [x] Trust-level and review-board precondition policy approved.
- [x] Exit-reference consumption contract approved as reference metadata.
- [x] Cross-company sharing legal/privacy model approved as local/deferred
  metadata only.
- [x] Marketplace abuse, throttling, misuse, and audit policy approved as
  metadata-only precondition state.
- [x] Retention/evidence/legal-hold/deletion policy approved as local/deferred
  metadata.
- [x] Candidate dispute/response boundary explicitly deferred.
- [x] Notification/document dependency handling explicitly deferred and out of
  scope.
- [x] Raw/sensitive/PII-heavy persistence exclusion approved.
- [x] Gateway/frontend remain closed.
- [x] Runtime literal scan requirement retained for `CAND-CAP-0019|MOD-0328`.

Open blockers: none for the done first backend/API metadata-only reference
exchange marketplace readiness contract slice.

Runtime-ready reconciliation: EA candidate-runtime policy waiver approved.
`CAND-CAP-0019` remains a governance/documentation identity and `MOD-0328`
remains blocked; neither may appear in runtime literals. Runtime owner/key is
`tep.reference-exchange`, permission namespace is limited to the four
permissions listed above, and scope is limited to
`services/Diten.TalentEcosystemService/**`. Cross-company exchange,
legal/privacy/consent, visibility/data-scope/minimization,
retention/evidence/audit/legal-hold/deletion, participant eligibility,
verified access, trust-level, review-board, exit-reference consumption, and
abuse-control behavior are approved only as metadata-only local/deferred or
precondition states for the first slice.

Review-ready reconciliation: implementation review passed for the metadata-only
backend/API slice. Build passed with 0 warnings and 0 errors. ReferenceExchange
targeted tests passed 23/23 and the full TEP Application test suite passed
165/165. Runtime literal scan for `CAND-CAP-0019|MOD-0328` returned no matches.
Production in-memory repository scan passed. The reviewed implementation keeps
runtime owner/key limited to `tep.reference-exchange`, permission namespace
limited to `tep.reference-exchange.read`, `tep.reference-exchange.manage`,
`tep.reference-exchange.evaluate`, and `tep.reference-exchange.audit.read`, and
scope limited to `services/Diten.TalentEcosystemService/**`. Frontend, Gateway,
HCM, PSS, and Platform scopes remain closed.

Done-promotion reconciliation: done-promotion readiness audit passed. Pack
status moved from `review` to `done` for the first metadata-only backend/API
readiness contract slice. Open blockers remain none. Build evidence remains 0
warnings and 0 errors; ReferenceExchange targeted tests remain 23/23; full TEP
Application tests remain 165/165. Runtime literal scan for
`CAND-CAP-0019|MOD-0328` returned no matches and production in-memory
repository scan passed. Gateway and frontend remain future follow-up only.

## 19. Implementation Notes

- DCP-008 lists Reference Exchange Marketplace in R2-D with legacy Excel ID
  `MOD-0328` and output role "HR-to-HR reference exchange".
- DCP-002 blocks `MOD-0328` for TEP use because it is not Blueprint-backed and
  has no canonical registry row.
- EA/registry owner reservation is recorded for `CAND-CAP-0019`.
- `CAND-CAP-0019` remains a governance/documentation identity only and
  `MOD-0328` remains blocked; neither may appear in runtime literals.
- `CAND-CAP-0011` through `CAND-CAP-0018` are completed TEP dependencies.
- HCM foundation and PSS/backbone modules remain context only.
- `MOD-0027`, `MOD-0263`, `MOD-0029`, and `MOD-0262` are deferred/out of scope
  for this pack.
- Candidate gate script is not present in this checkout; EA/registry owner
  accepted verifier unavailability as a governance note. Reservation evidence is
  the registry, reconciliation ledger, and DCP-002 update.
- Runtime-ready approval is recorded for the first metadata-only backend/API
  reference exchange marketplace readiness contract slice.
- Implementation review passed for the first metadata-only backend/API
  reference exchange marketplace readiness contract slice.
- Done-promotion readiness audit passed for the first metadata-only backend/API
  reference exchange marketplace readiness contract slice.
- Open blockers: none.
- Build evidence: 0 warnings, 0 errors.
- Test evidence: ReferenceExchange targeted tests passed 23/23; full TEP
  Application tests passed 165/165.
- Scan evidence: runtime literal scan for `CAND-CAP-0019|MOD-0328` returned no
  matches; production in-memory repository scan passed.
- Runtime owner/key is `tep.reference-exchange`; permission namespace is limited
  to `tep.reference-exchange.read`, `tep.reference-exchange.manage`,
  `tep.reference-exchange.evaluate`, and `tep.reference-exchange.audit.read`.
- Frontend, Gateway, real marketplace workflow, rehire recommendation,
  candidate dispute/response workflow, notification/document integrations,
  export governance, and real audit/evidence/retention integrations remain
  future follow-up only.

## 20. Follow-up Items

- Run candidate gate if `.antigravity/scripts/verify_module_id.py` is restored.
- Plan TEP Reference Exchange Gateway exposure as the next integration-agent
  follow-up.
- Plan CAND-CAP-0019 restricted read-only frontend slice after Gateway exposure.
- Decide future EA canonical MOD assignment.
- Keep cross-company exchange runtime closed until legal/privacy/consent,
  visibility, trust, review, audit/evidence, retention, and abuse controls are
  approved.
- Keep rehire recommendation and candidate dispute/response workflow as
  separate future modules.
- Keep notification and document repository modules deferred/out of scope until
  explicitly scheduled.
