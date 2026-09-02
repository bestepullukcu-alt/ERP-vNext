---
id: CAND-CAP-0021
name: Candidate Response & Dispute Management
domain: talent-ecosystem-platform
service: Diten.TalentEcosystemService
shell: none
golden_reference: none
entity_base: none
status: done
owner: enterprise-architect / tep-domain-owner / platform-team / security-owner / legal-owner
branch: feature/tep/cand-cap-0021-candidate-response-dispute-management
started: 2026-08-31
target: 2026-12-02
form_field_count: 0
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/blueprint-master-plan-reconciliation.md
candidate_identity: CAND-CAP-0021
legacy_excel_id: MOD-0331
canonicalization_status: candidate / EA-waived-for-runtime-first-slice
runtime_service_candidate: Diten.TalentEcosystemService
bootstrap_type: runtime-ready-first-slice
runtime_owner_key: tep.candidate-disputes
permission_namespace: tep.candidate-disputes.*
runtime_repo_scope: services/Diten.TalentEcosystemService/**
---

# CAND-CAP-0021 - Candidate Response & Dispute Management

> Status: done. The first metadata-only backend/API candidate
> response/dispute readiness contract slice passed implementation review,
> done-promotion readiness audit, and done promotion. This pack does not authorize
> frontend, Gateway exposure, candidate-facing self-service UX, real dispute
> workflow execution, notification/document integration, automated decision
> override, recommendation recalculation, marketplace transaction workflow, raw
> provider payload persistence, credential/token/secret persistence, or
> PII-heavy persistence.
> `CAND-CAP-0021` remains a governance/documentation identity only and must not
> be written into runtime literals. `MOD-0331` remains blocked for TEP use until
> future EA canonical MOD assignment.

## 1. Module Summary

`CAND-CAP-0021` reserves the temporary DCP-002 candidate identity for the native
Talent Ecosystem Platform R2-D capability named `Candidate Response & Dispute
Management`.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`.
DCP-008 proposed legacy Excel ID `MOD-0331`, but DCP-002 blocks that ID because
it is not Blueprint-backed and has no canonical registry row. EA/registry owner
reservation records `CAND-CAP-0021` as a temporary governance identity pending
future canonical MOD allocation.

EA candidate-runtime policy waiver authorized the narrow metadata-only
backend/API readiness contract, and the completed slice is now reconciled as
`done`. Candidate-facing self-service, real dispute workflow execution,
notification/document integration, automated decision override, recommendation
recalculation, and marketplace transaction workflows remain out of scope.

## 2. Ownership and Boundaries

Owned by this done pack:

- TEP-native metadata-only backend/API readiness contract boundary.
- Runtime owner/key `tep.candidate-disputes`.
- Permission namespace `tep.candidate-disputes.*`.
- Fail-closed readiness/evaluation metadata for response/dispute preconditions.
- Dependency sequencing after completed TEP foundation modules through
  `CAND-CAP-0020`.
- Explicit prohibition of candidate-facing self-service and real dispute
  workflow execution in the first slice.

Not owned by this pack:

- Frontend, Gateway routing, menu entries, or candidate-facing UX.
- Candidate-facing self-service response, dispute, appeal, or resolution flows.
- Notification, notification provider, controlled document, or external document
  repository integration.
- Automated decision override, recommendation recalculation, or marketplace
  transaction workflow.
- HCM, PSS directory, HRIS, payroll, time, or provider data ownership.

## 3. Owned Objects

First-slice runtime metadata objects:

| Object | Type | Purpose |
|---|---|---|
| TepCandidateDisputeReadinessMetadata | Domain entity | Persists tenant-owned metadata-only readiness records without response bodies, dispute narratives, document bodies, raw evidence, or PII-heavy data. |
| CandidateDisputeReadinessDto | API response DTO | Exposes only the approved metadata contract through `Response<T>`. |
| CreateCandidateDisputeReadinessCommand | CQRS command | Creates metadata-only readiness records; `TenantId` is resolved server-side. |
| EvaluateCandidateDisputeReadinessCommand | CQRS command | Produces fail-closed readiness/deferred metadata without dispute workflow execution. |
| CandidateDisputeReadinessRepository | Persistence contract | Provides Mongo-backed, tenant-aware storage with active tenant `Code` uniqueness. |

Deferred runtime objects:

| Object | Type | Purpose |
|---|---|---|
| CandidateResponseWorkflow | Deferred runtime | Candidate response, appeal, and self-service execution. |
| DisputeReviewWorkflow | Deferred runtime | Dispute intake, adjudication, escalation, and resolution execution. |
| NotificationDocumentIntegration | Deferred dependency | Notification and document repository integrations. |
| RecommendationMarketplaceOverride | Deferred runtime | Any recommendation recalculation, automated decision override, or marketplace transaction behavior. |

## 4. Entity Fields

The first backend/API slice must expose and persist only this metadata contract:

- `Code`
- `DisplayName`
- `CandidateProfileReference`
- `ExitReferenceRecordReference`
- `ReferenceExchangeReference`
- `RehireRecommendationReference`
- `ResponseBoundaryState`
- `DisputeIntakeState`
- `DisputeReviewState`
- `ResolutionLifecycleState`
- `ContestabilityState`
- `HumanReviewState`
- `ConsentPreconditionState`
- `VisibilityApprovalState`
- `DataScopeState`
- `EvidenceRetentionState`
- `AuditReadinessState`
- `LegalHoldState`
- `DeletionPolicyState`
- `SelfServiceBoundaryState`
- `NotificationDependencyState`
- `DocumentDependencyState`
- `AutomatedDecisionBoundaryState`
- `MarketplaceBoundaryState`
- `DependencyStates`
- `SourceContractVersion`
- `LastEvaluatedAt`
- `DisputeReadinessVersion`
- `DeferredReason`

Contract rules:

- Candidate response and dispute workflow details must be represented as
  metadata states, not executable workflow logic.
- Evaluation must be fail-closed and may set explicit deferred metadata.
- No request DTO may contain `TenantId`; tenant ownership is resolved through
  server-side tenant context.

Forbidden fields:

- `CAND-CAP-0021` or `MOD-0331` as runtime module literals.
- Candidate response body, dispute narrative body, message body, document body,
  or raw evidence/provider payload.
- Credentials, tokens, secrets, passwords, bank/tax/payroll data, payslips,
  biometric/geolocation data, national ID, DOB, home address, or PII-heavy
  fields.
- Recommendation scores, ranks, model outputs, automated decision results,
  marketplace transactions, or override decisions.

## 5. Repo Scope

Authorized future implementation scope for the first metadata-only slice:

- `services/Diten.TalentEcosystemService/**`

Authorized scope for this promotion change:

- `execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0021-candidate-response-dispute-management.md`

No changes are authorized under:

- `frontend/**`
- `gateway/**`
- `services/Diten.HumanCapitalService/**`
- `services/Diten.Platform/**`
- global `tests/**`
- notification/document modules listed as out of scope.

## 6. Protected Paths

This done pack must not change these paths during promotion:

- `services/Diten.TalentEcosystemService/**`
- `services/Diten.HumanCapitalService/**`
- `services/Diten.Platform/**`
- `frontend/Diten.Web/**`
- `gateway/Diten.ApiGateway/**`
- `tests/**`
- external notification/document module packs listed as out of scope.

During a later implementation prompt, only `services/Diten.TalentEcosystemService/**`
may be changed.

## 7. Dependencies

Governance dependencies:

- `execution/domains/talent-ecosystem-platform/domain-config.md`
- `execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`
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
- `CAND-CAP-0019` - Reference Exchange Marketplace, `done`.
- `CAND-CAP-0020` - Rehire Recommendation Network, `done`.

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

External modules deferred/out of scope:

- `MOD-0027` - Notification.
- `MOD-0263` - Notification Provider / Delivery.
- `MOD-0029` - Controlled Documents.
- `MOD-0262` - External Docs Repository.

## 8. Upstream Inputs

- DCP-008 R2-D candidate response/dispute roadmap item with blocked legacy
  Excel ID `MOD-0331`.
- EA/registry reservation for `CAND-CAP-0021` in
  `execution/registries/module-id-registry.md`.
- Reconciliation ledger reservation in
  `execution/portfolio/blueprint-master-plan-reconciliation.md`.
- Completed TEP CAND-CAP-0011 through CAND-CAP-0020 module packs.
- HR/TEP/PSS frontend completion reconciliation evidence.

## 9. Downstream Consumers

Potential future consumers:

- Candidate-facing response and dispute portals.
- Employer-facing dispute review and resolution workspaces.
- Review board escalation and evidence review workflows.
- Future notification and document/evidence integrations.
- Future audit, retention, legal-hold, and deletion workflows.

Downstream runtime integration remains closed. Any candidate-facing,
cross-company, document-backed, notification-backed, or adjudication workflow
requires a later approved pack and runtime-ready decision gate.

## 10. Runtime Constraints

Authorized runtime boundary:

- Runtime owner/key: `tep.candidate-disputes`.
- Permission namespace:
  - `tep.candidate-disputes.read`
  - `tep.candidate-disputes.manage`
  - `tep.candidate-disputes.evaluate`
  - `tep.candidate-disputes.audit.read`
- Runtime repo scope: `services/Diten.TalentEcosystemService/**`.
- First slice: metadata-only candidate response/dispute readiness contract.
- Persistence: Mongo-backed, tenant-aware repository only.
- Uniqueness: active tenant `Code` unique index.
- Deletion: soft delete with `IsDeleted` and `DeletedAt`.

Runtime constraints:

- `CAND-CAP-0021` remains a governance/documentation identity only.
- `MOD-0331` remains blocked and must not be used as a runtime literal.
- API/controller/entity/repository/service code is authorized only for the
  metadata-only backend/API readiness contract and only after a direct
  implementation prompt.
- Candidate-facing self-service UX is closed.
- Real candidate response/dispute workflow execution is closed; only
  local/deferred metadata is allowed.
- Legal/privacy/consent is represented only as metadata-only waiver state.
- Retention/evidence/audit/legal-hold/deletion are local/deferred metadata.
- Tenant isolation must be server-side through tenant context; request DTOs must
  not carry `TenantId`.
- TEP CAND-CAP-0012 through CAND-CAP-0020 may be consumed only as
  dependency/precondition references.
- HCM and PSS dependencies remain context/reference/backbone only.
- Notification and document dependencies remain deferred/out of scope.
- Automated decision override, recommendation recalculation, and marketplace
  transaction workflows are closed.
- Raw provider payload, credential/token/secret/password, and PII-heavy
  persistence are forbidden.

## 11. Frontend File Contract

Frontend implementation is N/A for this done backend/API metadata pack.

No Razor, JavaScript, DataTable, RESX, menu, route, API client, GatewayUrl
consumer, candidate-facing page, or self-service UX work is authorized. Any
future UI must be a separate approved slice after backend/API implementation
review and Gateway exposure.

## 12. Data, Security, and Privacy

The first slice may persist only metadata states and references listed in
Section 4.

Approved metadata-only waivers:

- Legal/privacy/consent basis may be represented as waiver metadata.
- Retention/evidence/audit/legal-hold/deletion may be represented as
  local/deferred metadata.
- Candidate self-service authentication remains out of scope/deferred.
- Candidate response and dispute workflow execution may be represented only as
  boundary/deferred metadata.
- Notification and document dependencies remain deferred/out of scope.
- Automated decision override, recommendation recalculation, and marketplace
  transaction workflows remain out of scope.

Forbidden data categories:

- Raw HRIS, payroll, time, provider, candidate, reference exchange, exit
  reference, recommendation, response, dispute, or evidence payloads.
- Credential/token/secret/password values.
- Payroll amount/detail, payslip, bank, tax, national ID, DOB, home address,
  biometric/geolocation, or PII-heavy profile/reference details.
- Automated decision results, override results, recommendation scores/ranks, or
  marketplace transactions.

## 13. Failure Path to Verify

Future gates must verify these failure paths:

- Reusing `MOD-0331` as the TEP identity.
- Treating `CAND-CAP-0021` as a runtime module ID.
- Creating runtime/API/controller/entity/repository/database/service scaffold
  outside the approved metadata-only backend/API readiness contract.
- Creating frontend or Gateway routes from this done backend/API metadata pack.
- Starting candidate-facing self-service response/dispute UX.
- Starting notification/document integration in this pack.
- Starting automated decision override, recommendation recalculation, or
  marketplace transaction behavior.
- Treating HCM, PSS directory, HRIS, payroll, time, or provider records as this
  module's owned data.
- Persisting raw provider payloads, credentials, tokens, secrets, passwords, or
  PII-heavy data.

## 14. Authorization Convention

Runtime owner/key:

- `tep.candidate-disputes`

Permission namespace:

- `tep.candidate-disputes.read`
- `tep.candidate-disputes.manage`
- `tep.candidate-disputes.evaluate`
- `tep.candidate-disputes.audit.read`

Permission enforcement must remain backend-authoritative through the existing
permission pattern. Any future frontend permission behavior is UX-only.

## 15. Gateway / API Routing Decision

Gateway routing is N/A for the first backend/API implementation slice.

No Ocelot route may be created during this promotion or during the initial
backend/API slice. If future external API exposure is approved, Gateway routing
must be handled by a separate integration-agent workflow after backend/API
implementation gates pass.

## 16. Acceptance Criteria

1. AC-01: EA/registry owner reservation exists for `CAND-CAP-0021`.
2. AC-02: Pack status is `done`.
3. AC-03: `MOD-0331` remains blocked and no canonical MOD is invented.
4. AC-04: `CAND-CAP-0021` is documented as governance/documentation identity
   only and not a runtime literal.
5. AC-05: Candidate gate status is recorded; if the script is unavailable, the
   pack records "candidate gate not executable in this checkout; reservation
   recorded".
6. AC-06: Runtime owner/key is exactly `tep.candidate-disputes`.
7. AC-07: Frontend and Gateway implementation are not authorized.
8. AC-08: Candidate-facing self-service UX is not authorized.
9. AC-09: Notification and document dependencies remain deferred/out of scope.
10. AC-10: Automated decision override, recommendation recalculation, and
    marketplace transaction workflow remain out of scope.
11. AC-11: Permission namespace is limited to
    `tep.candidate-disputes.read`, `tep.candidate-disputes.manage`,
    `tep.candidate-disputes.evaluate`, and
    `tep.candidate-disputes.audit.read`.
12. AC-12: Runtime implementation scope is limited to
    `services/Diten.TalentEcosystemService/**`.
13. AC-13: Completed TEP foundation dependencies through `CAND-CAP-0020` are
    listed as dependencies.
14. AC-14: HCM dependencies are context-only.
15. AC-15: PSS/backbone dependencies are context-only.
16. AC-16: External notification/document modules are not pulled into scope.
17. AC-17: Minimal metadata field list in Section 4 is implemented without
    response bodies, dispute narratives, document bodies, raw evidence, or
    PII-heavy profile details.
18. AC-18: Request DTOs do not contain `TenantId`; tenant resolution is
    server-side through tenant context.
19. AC-19: Evaluation fails closed and may produce explicit deferred metadata
    without dispute workflow execution.
20. AC-20: Runtime literal scan for `CAND-CAP-0021|MOD-0331` across runtime,
    frontend, gateway, and tests returns no matches.

## 17. Test Expectations

Governance validation commands:

- `python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0021 --name "Candidate Response & Dispute Management"`
- `rg -n "CAND-CAP-0021|MOD-0331" services/Diten.TalentEcosystemService frontend gateway tests`

Candidate gate note:

- Candidate gate was not executable during pack preparation because
  `.antigravity/scripts/verify_module_id.py` was not present in this checkout.
  Reservation is recorded in `execution/registries/module-id-registry.md` and
  `execution/portfolio/blueprint-master-plan-reconciliation.md`.

Runtime implementation validation commands for the future backend/API slice:

- `dotnet build services/Diten.TalentEcosystemService/src/Diten.TalentEcosystemService.Api/Diten.TalentEcosystemService.Api.csproj -c Debug --no-restore /nr:false -m:1 --tl:off`
- `dotnet test services/Diten.TalentEcosystemService/tests/Diten.TalentEcosystemService.Application.Tests/Diten.TalentEcosystemService.Application.Tests.csproj -c Debug --no-restore --filter CandidateDispute`
- `dotnet test services/Diten.TalentEcosystemService/tests/Diten.TalentEcosystemService.Application.Tests/Diten.TalentEcosystemService.Application.Tests.csproj -c Debug --no-restore`
- `rg -n "CAND-CAP-0021|MOD-0331" services/Diten.TalentEcosystemService frontend gateway tests`
- `rg -n "InMemory.*CandidateDispute|CandidateDispute.*InMemory" services/Diten.TalentEcosystemService/src`
- `rg -n "raw|payload|national|birth|dob|bank|payroll|tax|payslip|biometric|geolocation|home address|secret|token|credential|password|response body|dispute narrative|document body|evidence body|automated decision|override|recalculation|marketplace transaction" services/Diten.TalentEcosystemService/src/Diten.TalentEcosystemService.Domain/Entities services/Diten.TalentEcosystemService/src/Diten.TalentEcosystemService.Application/Features/CandidateDisputes`

Frontend and Gateway validation: N/A until a separate Gateway exposure and
frontend read-only slice are approved.

## 18. Governance Approval and Runtime-Ready Checklists

### Governance Approval Checklist

- [x] EA/registry owner reservation exists for `CAND-CAP-0021`.
- [x] `MOD-0331` remains blocked and must not be used.
- [x] `CAND-CAP-0021` remains governance/documentation identity only.
- [x] TEP domain config exists.
- [x] `CAND-CAP-0011` TEP Shell dependency is `done`.
- [x] `CAND-CAP-0012` Association dependency is `done`.
- [x] `CAND-CAP-0013` Consent/Visibility dependency is `done`.
- [x] `CAND-CAP-0014` Verified Access dependency is `done`.
- [x] `CAND-CAP-0015` Review Board dependency is `done`.
- [x] `CAND-CAP-0016` Trust Level dependency is `done`.
- [x] `CAND-CAP-0017` Candidate Profile dependency is `done`.
- [x] `CAND-CAP-0018` Exit Reference Record dependency is `done`.
- [x] `CAND-CAP-0019` Reference Exchange dependency is `done`.
- [x] `CAND-CAP-0020` Rehire Recommendation dependency is `done`.
- [x] HCM foundation completion dependencies are listed as context.
- [x] PSS/backbone dependencies are listed as context.
- [x] Notification/document dependencies are deferred and out of scope.
- [x] Candidate-facing self-service UX remains closed.
- [x] Raw provider payload, credential/token/secret/password, and PII-heavy
  persistence remain forbidden.
- [x] Candidate gate script unavailability is recorded and accepted by
  EA/registry owner.

Open blockers for governance continuation:

- none.

### Runtime-Ready Checklist

- [x] EA candidate-runtime waiver approved.
- [x] `CAND-CAP-0021` remains governance/documentation identity and not runtime
  literal.
- [x] `MOD-0331` remains blocked and not runtime literal.
- [x] Runtime owner/key approved as `tep.candidate-disputes`.
- [x] Runtime permission namespace approved.
- [x] Runtime repo scope approved as `services/Diten.TalentEcosystemService/**`.
- [x] First metadata-only backend/API contract defined and approved.
- [x] Legal/privacy/consent basis approved as metadata-only waiver.
- [x] Visibility/data-scope/minimization approved as fail-closed metadata.
- [x] Candidate self-service authentication remains out of scope/deferred.
- [x] Candidate response intake approved as metadata-only boundary.
- [x] Dispute review and resolution lifecycle approved as local/deferred
  metadata.
- [x] Contestability and human review approved as metadata-only policy state.
- [x] Retention/evidence/audit/legal-hold/deletion approved as local/deferred
  metadata.
- [x] Notification dependency remains deferred/out of scope.
- [x] Document/evidence repository dependency remains deferred/out of scope.
- [x] Automated decision override and recommendation recalculation explicitly
  excluded or separately approved.
- [x] Marketplace transaction workflow explicitly excluded or separately
  approved.
- [x] Raw/sensitive/PII-heavy persistence exclusion approved.
- [x] Gateway/frontend remain closed until backend/API slice passes review.
- [x] Runtime literal scan requirement retained for `CAND-CAP-0021|MOD-0331`.

Open blockers for runtime-ready:

- none.

## 19. Implementation Notes

- DCP-008 identifies a candidate response/dispute capability in the TEP R2-D
  sequence with legacy Excel ID `MOD-0331`.
- DCP-002 blocks `MOD-0331` for TEP use because it is not Blueprint-backed and
  lacks a canonical registry row.
- EA/registry owner reservation is recorded for `CAND-CAP-0021`.
- `CAND-CAP-0021` remains a governance/documentation identity only and
  `MOD-0331` remains blocked; neither may appear in runtime literals.
- `CAND-CAP-0011` through `CAND-CAP-0020` are completed TEP dependencies.
- HCM and PSS dependencies remain context/reference/backbone only.
- Notification and document modules remain deferred/out of scope in this turn.
- EA candidate-runtime policy waiver is approved for the first metadata-only
  backend/API readiness contract.
- Runtime owner/key is approved as `tep.candidate-disputes`.
- Runtime permission namespace is limited to `tep.candidate-disputes.read`,
  `tep.candidate-disputes.manage`, `tep.candidate-disputes.evaluate`, and
  `tep.candidate-disputes.audit.read`.
- Runtime repo scope is limited to `services/Diten.TalentEcosystemService/**`.
- Candidate response, contestability, dispute intake, dispute review, and
  resolution lifecycle are metadata-only readiness boundaries for the first
  runtime slice.
- Candidate-facing self-service UX, real dispute workflow execution,
  notification/document integration, automated decision override,
  recommendation recalculation, and marketplace transaction workflow remain
  closed until later gates.
- Candidate gate was not executable in this checkout; reservation is recorded.
- Governance-only approval, dated 2026-08-31: EA approved candidate-based
  governance continuation for `CAND-CAP-0021`. EA/registry owner accepted the
  candidate gate script absence as a governance note. This approval did not by
  itself make the pack ready-for-dev and did not authorize runtime/API/frontend/
  Gateway implementation.
- Ready-for-dev promotion, dated 2026-08-31: EA candidate-runtime policy waiver
  approved first metadata-only backend/API slice. Runtime owner/key
  `tep.candidate-disputes`, permission namespace, and runtime repo scope are
  approved. Frontend and Gateway remain closed until backend/API implementation
  review passes.
- Review-ready reconciliation, dated 2026-08-31: implementation review PASS for
  the metadata-only backend/API slice. Build PASS with 0 warning and 0 error.
  CandidateDispute targeted tests PASS, 28/28. Full TEP Application tests PASS,
  219/219. Runtime literal scan for `CAND-CAP-0021|MOD-0331` returned no
  matches. Production in-memory repository scan PASS. Runtime owner/key stayed
  `tep.candidate-disputes`; permission namespace stayed limited to
  `tep.candidate-disputes.read`, `tep.candidate-disputes.manage`,
  `tep.candidate-disputes.evaluate`, and
  `tep.candidate-disputes.audit.read`.
- Backend/API implementation remained metadata-only. Candidate-facing
  self-service UX, real dispute workflow execution, notification/document
  integration, automated decision override, recommendation recalculation,
  marketplace transaction workflow, raw provider payload, credential/token/
  secret/password, dispute body/free-text/document payload, and PII-heavy
  persistence remained out of scope.
- Done-promotion reconciliation, dated 2026-08-31: done-promotion readiness
  audit PASS. Pack status moved from `review` to `done`. Open blockers remain
  none. Gateway exposure and restricted read-only frontend slice remain future
  follow-ups after this governance promotion.

## 20. Open Blockers and Waivers

Open blockers:

- none.

Approved waivers:

- EA/registry owner approved candidate reservation for `CAND-CAP-0021`.
- EA candidate-runtime policy waiver for first metadata-only backend/API
  readiness contract.
- Legal/privacy/consent metadata-only waiver.
- Retention/evidence/audit/legal-hold/deletion local/deferred metadata waiver.
- Candidate self-service authentication deferred/out-of-scope waiver.
- Notification/document dependencies deferred/out-of-scope waiver.
- Candidate gate script not executable in this checkout; reservation recorded as
  governance note.

Future follow-ups:

- Gateway exposure.
- Restricted read-only frontend slice after Gateway exposure.
- Candidate-facing UX.
- Real dispute workflow execution.
- Notification/document integrations.
- Export governance.
- Real audit/evidence/retention integration.
