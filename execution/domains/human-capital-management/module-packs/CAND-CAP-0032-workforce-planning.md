---
id: CAND-CAP-0032
name: Workforce Planning
domain: human-capital-management
service: Diten.HumanCapitalService
shell: tenant
golden_reference: golden-compact
entity_base: BaseEntity
status: draft
owner: enterprise-architect / hcm-domain-owner / platform-team / security-owner / legal-owner
branch: feature/hcm/cand-cap-0032-workforce-planning
started: 2026-09-07
target: 2026-12-15
form_field_count: 0
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/blueprint-master-plan-reconciliation.md
candidate_identity: CAND-CAP-0032
legacy_excel_id: MOD-0310
canonicalization_status: candidate / EA-waived-for-runtime-first-slice
runtime_service_candidate: Diten.HumanCapitalService
runtime_service_port: 5059
gateway_port: 5080
bootstrap_type: metadata-only-backend-api-readiness
runtime_owner_key: hcm.workforce-planning
permission_namespace:
  - hcm.workforce-planning.read
  - hcm.workforce-planning.manage
  - hcm.workforce-planning.evaluate
  - hcm.workforce-planning.audit.read
runtime_repo_scope: services/Diten.HumanCapitalService/**
---

# CAND-CAP-0032 - Workforce Planning

> Status: draft. This pack records the first metadata-only backend/API
> workforce planning readiness contract slice under
> `services/Diten.HumanCapitalService/**`, plus a golden-compact readiness CRUD
> frontend under `frontend/Diten.Web/**` and gateway exposure through the API
> gateway (`5080` -> HCM `5059`). Production workforce planning workflow,
> headcount plan management, demand forecast, supply forecast, gap analysis,
> scenario modeling, automated decision behavior, manager/employee workforce
> planning UX, notification/document integration, and sensitive persistence
> remain closed. `CAND-CAP-0032` remains a governance/documentation identity
> only and must not be written into runtime literals. `MOD-0310` remains blocked
> for HCM use until future EA canonical MOD assignment.

## 1. Module Summary

`CAND-CAP-0032` reserves the temporary DCP-002 candidate identity for the native
Human Capital Management R3-C capability named `Workforce Planning`.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`.
DCP-008 proposed legacy Excel ID `MOD-0310`, but DCP-002 blocks that ID because
it is not Blueprint-backed and has no canonical registry row. EA/registry owner
reservation records `CAND-CAP-0032` as a temporary governance identity pending
future canonical MOD allocation.

This pack records the first metadata-only backend/API workforce planning
readiness contract slice, mirroring the Succession & High-Potential Tracking
slice (`CAND-CAP-0031` / `MOD-0320`) and the completed Learning / Training
Records slice (`CAND-CAP-0030` / `MOD-0309`) exactly. Workforce planning
workflow, headcount plan management, demand forecast, supply forecast, gap
analysis, scenario modeling, automated decision behavior, manager/employee
workforce planning UX, notification/document integration, and sensitive
persistence remain closed.

## 2. Ownership and Boundaries

Owned by this pack:

- HCM-native workforce planning governance boundary.
- Sequencing after completed HCM employee, governance, position, offboarding,
  applicant intake, candidate pipeline, offer management, onboarding,
  employment change, performance review, competency/skills, development plan,
  learning/training, and succession/high-potential foundation modules.
- Candidate identity reservation and blocked legacy ID rationale.
- Metadata-only workforce planning readiness backend/API contract.
- Golden-compact readiness CRUD frontend and gateway exposure limited to the
  metadata-only readiness contract.
- Explicit exclusion of workforce planning workflow execution.
- Explicit exclusion of headcount plan management, demand forecast, supply
  forecast, gap analysis, scenario modeling, model output, and automated
  decision behavior.
- Explicit exclusion of manager-facing and employee-facing workforce planning
  UX beyond the readiness-metadata CRUD (those fields are reserved and out of
  scope).
- Explicit exclusion of free-text workforce planning notes, forecast narrative,
  attachment payload, raw provider payload,
  credential/token/secret/password, and PII-heavy persistence.

Not owned by this pack:

- Real workforce planning execution, headcount plan assignments, demand
  forecast runs, supply forecast submissions, gap scoring, scenario modeling
  sessions, ratings, rankings, or model output.
- Organization structure ownership beyond dependency/context references.
- Position framework ownership beyond dependency/context references.
- Notification, notification provider, controlled document, or external
  document repository implementation.
- Development plan, competency/skills, performance review, learning/training,
  succession, analytics, TEP, or PSS ownership transfer.

## 3. Owned Objects

Governance-only objects:

| Object | Type | Purpose |
|---|---|---|
| WorkforcePlanningCapabilityBoundary | Governance boundary | Defines what a future HCM workforce planning module may own. |
| WorkforcePlanningReadinessBoundary | Governance boundary | Separates readiness metadata from workforce planning workflow execution. |
| HeadcountDemandSupplyBoundary | Governance boundary | Blocks headcount plan management, demand forecast, and supply forecast execution. |
| GapScenarioBoundary | Governance boundary | Blocks gap analysis and scenario modeling execution. |
| WorkforcePlanningDecisionBoundary | Governance boundary | Blocks scoring, rating, ranking, model-output, and automated decision persistence. |
| WorkforcePlanningUxBoundary | Governance boundary | Blocks manager and employee workforce planning UX beyond readiness-metadata CRUD. |
| WorkforcePlanningSensitiveDataBoundary | Governance boundary | Blocks narrative, attachment, credential, and PII-heavy persistence. |
| WorkforcePlanningDependencyBoundary | Governance boundary | Keeps organization-structure/position-framework/notification/document dependencies deferred and out of scope. |

Deferred runtime objects:

| Object | Type | Purpose |
|---|---|---|
| WorkforcePlanningWorkflow | Deferred runtime | Workforce planning workflow execution; not authorized. |
| HeadcountDemandForecastEngine | Deferred runtime | Headcount plan management, demand forecast, and supply forecast; not authorized. |
| GapAnalysisScenarioModelingEngine | Deferred runtime | Gap analysis, scenario modeling, and model output; not authorized. |
| ManagerEmployeeWorkforcePlanningExperience | Deferred frontend/runtime | Manager/employee workforce planning UX beyond readiness CRUD; not authorized. |
| WorkforcePlanningDocumentIntegration | Deferred dependency | Controlled/external document use; out of scope. |
| WorkforcePlanningNotificationIntegration | Deferred dependency | Notification and reminder delivery; out of scope. |

Approved runtime/frontend objects (metadata-only readiness slice):

| Object | Type | Purpose |
|---|---|---|
| WorkforcePlanningReadinessMetadata | Domain entity | Metadata-only readiness contract; `Domain/Entities/WorkforcePlanningReadinessMetadata.cs`. |
| WorkforcePlanningReadinessState | Domain enum | Readiness state; `Domain/Enums/WorkforcePlanningReadinessState.cs` (Draft=0, Ready=1, Deferred=2, Blocked=3, NotRequired=4, Archived=5). |
| IWorkforcePlanningReadinessMetadataRepository | Domain repository | Tenant-aware repository contract; `Domain/Repositories/IWorkforcePlanningReadinessMetadataRepository.cs`. |
| MongoWorkforcePlanningReadinessMetadataRepository | Persistence repository | Mongo-backed repository; collection `hcm_workforce_planning_readiness`. |
| WorkforcePlanning Application features | Application (CQRS/MediatR) | `Application/Features/WorkforcePlanning/**` (Models/Guard/Commands/Queries/Handlers). |
| WorkforcePlanningController | API controller | Thin controller; `Api/Controllers/Hcm/WorkforcePlanningController.cs`. |
| WorkforcePlanning golden-compact CRUD | Frontend | Controller + `Views/HumanCapital/WorkforcePlanning/**` + `wwwroot/assets/js/HumanCapital/WorkforcePlanning/**` + Resources + nav entry under `frontend/Diten.Web/**`. |

## 4. Entity Fields

Approved metadata-only persisted contract:

Minimal testable metadata fields (all readiness-state fields are of type
`WorkforcePlanningReadinessState`):

- `Code`
- `DisplayName`
- `WorkforcePlanningReadinessState`
- `HeadcountPlanBoundaryState`
- `DemandForecastBoundaryState`
- `SupplyForecastBoundaryState`
- `GapAnalysisBoundaryState`
- `ScenarioModelingBoundaryState`
- `AutomatedDecisionBoundaryState`
- `OrganizationStructureDependencyState`
- `PositionFrameworkDependencyState`
- `DocumentDependencyState`
- `NotificationDependencyState`
- `ConsentPreconditionState`
- `DataMinimizationState`
- `RetentionPolicyState`
- `EvidencePolicyState`
- `DependencyStates`
- `SourceContractVersion`
- `LastEvaluatedAt`
- `WorkforcePlanningReadinessVersion`
- `DeferredReason`
- `IsDeleted`
- `DeletedAt`
- `CreatedAt`
- `UpdatedAt`

Reserved / out-of-scope fields (must not be added in this slice):

- Manager-facing workforce planning fields (manager plan submission, manager
  review, manager action UX state) are reserved and out of scope.
- Employee-facing workforce planning fields (employee self-service input,
  employee acknowledgement, employee self-service UX state) are reserved and
  out of scope.
- No money field (salary/compensation/cost/budget). This is a metadata-only
  readiness slice with no monetary attribute.

Forbidden field classes:

- Workforce planning workflow body, demand forecast submission payload, supply
  forecast payload, gap analysis payload, scenario modeling payload, approval
  decision body, action evidence, or completed workforce planning content.
- Readiness score, rating value, rank, forecast outcome, model output,
  automated decision result, recommendation output, or evaluator scoring
  payload.
- Free-text workforce planning notes, forecast narrative, manager comments,
  employee comments, HR notes, or sensitive workforce planning body.
- Attachment payload, controlled document body, external document content, raw
  evidence, raw provider payload, or uploaded file metadata.
- Salary amount, compensation amount, benefits election, payroll details, tax
  details, bank details, or payment instruction fields.
- Credential, token, secret, password, activation code, provider connection
  value, device secret, or account bootstrap secret.
- National ID, DOB, home address, biometric/geolocation data, medical data, or
  PII-heavy employee/profile fields.

## 5. Repo Scope

Authorized governance scope:

- `execution/domains/human-capital-management/module-packs/CAND-CAP-0032-workforce-planning.md`

Candidate future runtime service:

- `Diten.HumanCapitalService` (port `5059`)

Runtime repo scope:

- `services/Diten.HumanCapitalService/**`

Frontend/gateway scope (metadata-only readiness CRUD only):

- `frontend/Diten.Web/**` (Controller, `Views/HumanCapital/WorkforcePlanning/**`,
  `wwwroot/assets/js/HumanCapital/WorkforcePlanning/**`, Resources, nav entry).
- Gateway route exposure through the API gateway (`5080` -> HCM `5059`).

## 6. Protected Paths

This pack authorizes only the first metadata-only backend/API readiness slice
under `services/Diten.HumanCapitalService/**` plus its golden-compact readiness
CRUD frontend and gateway route, and does not authorize changes to:

- `frontend/**` outside the owned WorkforcePlanning CRUD objects listed in
  §3/§5.
- `gateway/**` outside the owned WorkforcePlanning readiness route.
- `services/Diten.TalentEcosystemService/**`
- `services/Diten.Platform/**`
- `services/Diten.PssService/**`
- global `tests/**`
- notification/document module packs listed as out of scope.

## 7. Dependencies

Governance dependencies:

- `execution/domains/human-capital-management/domain-config.md`
- `execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`
- `execution/registries/module-id-registry.md`
- `execution/portfolio/blueprint-master-plan-reconciliation.md`
- `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`

Completed HCM foundation dependencies:

- `CAND-CAP-0007` - Employee Profile & Employment Record Projection, done.
- `CAND-CAP-0008` - HR Governance & Sensitive Access Controls, done.
- `CAND-CAP-0009` - Position & Organization Assignment, done.
- `CAND-CAP-0010` - Offboarding & Exit Management, done.
- `CAND-CAP-0022` - Recruitment / Applicant Intake, done.
- `CAND-CAP-0023` - Candidate Pipeline & Interview Management, done.
- `CAND-CAP-0024` - Offer Management, done.
- `CAND-CAP-0025` - Employee Onboarding, done.
- `CAND-CAP-0026` - Employment Change / Transfer / Promotion, done.
- `CAND-CAP-0027` - Performance Review Management, done.
- `CAND-CAP-0028` - Competency & Skills Assessment, done.
- `CAND-CAP-0029` - Development Plan Management, done.
- `CAND-CAP-0030` - Learning / Training Records, done.
- `CAND-CAP-0031` - Succession & High-Potential Tracking, done.

TEP context dependencies:

- `CAND-CAP-0011` through `CAND-CAP-0021` remain dependency/context only.

PSS backbone context:

- `MOD-0288`, `MOD-0251`, `MOD-0279`, `MOD-0280`, and `MOD-0281` remain
  dependency/backbone context only.

## 8. Runtime Guard

Runtime/API/controller/entity/repository/database/service scaffold is
authorized only for the first metadata-only backend/API readiness slice, along
with the golden-compact readiness CRUD frontend and gateway route.

Authorized runtime boundary:

- Runtime owner/key: `hcm.workforce-planning`.
- Permission namespace:
  - `hcm.workforce-planning.read`
  - `hcm.workforce-planning.manage`
  - `hcm.workforce-planning.evaluate`
  - `hcm.workforce-planning.audit.read`
- Runtime repo scope: `services/Diten.HumanCapitalService/**`.
- Thin controller (`Api/Controllers/Hcm/WorkforcePlanningController.cs`),
  CQRS/MediatR, `Response<T>` envelope.
- Server-side tenant context; `TenantId` must not be accepted in request DTOs.
- Mongo-backed tenant-aware repository; collection
  `hcm_workforce_planning_readiness`.
- Active tenant `Code` uniqueness.
- Soft delete through `IsDeleted` and `DeletedAt`.
- Fail-closed activation/evaluation.
- Non-activating evaluation may produce explicit `Deferred` metadata.
- Forbidden-marker guard rejecting forbidden field classes at the application
  boundary. This slice fixes the forbidden-marker matcher to use WORD/TOKEN
  boundaries (`\b` regex) instead of a bare substring `Contains`, so legitimate
  domain words (`workforce`, `taxonomy`, `planning`, `forecast`) are not
  falsely rejected while true forbidden markers are still caught.

Explicitly unauthorized:

- Workforce planning workflow.
- Headcount plan management, demand forecast, supply forecast, gap analysis,
  scenario modeling.
- Scoring/rating/ranking/model-output persistence.
- Automated decision behavior.
- Manager/employee workforce planning UX beyond readiness-metadata CRUD.
- Notification and document integrations.
- Sensitive payload persistence.

## 9. Frontend / UX Boundary

Golden-compact readiness CRUD frontend and gateway route are authorized, limited
to the metadata-only readiness contract.

Authorized UX:

- Tenant shell nav entry `Workforce Planning`.
- Golden-compact readiness CRUD (list/get/create/evaluate/delete) bound to the
  metadata-only contract.
- List-DTO to JS column parity with no drift.
- `tr`/`en` localization parity through Resources.

Unauthorized UX:

- Manager workforce planning UX beyond readiness-metadata CRUD.
- Employee workforce planning/self-service UX beyond readiness-metadata CRUD.
- Headcount plan, demand forecast, supply forecast, gap analysis, scenario
  modeling, scoring, or model-output UI.
- Document, notification, export, upload, or attachment UI.

## 10. Backend / API Boundary

Only metadata-only backend/API implementation is authorized, exposed through the
gateway (`5080` -> HCM `5059`).

Authorized API surface:

- `GET /api/workforce-planning` - list readiness metadata.
- `GET /api/workforce-planning/{id}` - get readiness metadata by id.
- `POST /api/workforce-planning` - create readiness metadata.
- `POST /api/workforce-planning/{id}/evaluate` - fail-closed evaluation.
- `DELETE /api/workforce-planning/{id}` - soft-delete.
- `GET /api/workforce-planning/{id}/audit-metadata` - audit metadata read.

The first slice must stay limited to:

- Create/list/get workforce planning readiness metadata.
- Fail-closed evaluation/deferred metadata.
- Audit metadata read endpoint.
- Soft delete.
- Tenant-aware persistence and active-code uniqueness.
- No workforce planning workflow, headcount plan management, demand forecast,
  supply forecast, gap analysis, scenario modeling, scoring/model-output
  persistence, automated decision behavior, notification/document integration,
  or sensitive payload persistence.

## 11. Data Boundary

Allowed in the first runtime slice:

- Metadata-only workforce planning readiness state.
- Boundary-state metadata for headcount plan, demand forecast, supply forecast,
  gap analysis, scenario modeling, automated decision, consent, minimization,
  retention, and evidence.
- Dependency-state metadata for organization structure, position framework,
  document, and notification dependencies.
- Dependency/precondition metadata (`DependencyStates` dictionary).

Forbidden:

- Workforce planning workflow payload.
- Headcount/demand/supply/gap/scenario execution payload.
- Readiness score/rating/rank/model output.
- Automated decision result.
- Free-text workforce planning notes or forecast narrative.
- Scenario modeling transcript payload.
- Attachment payload.
- Raw provider payload.
- Credential/token/secret/password values.
- PII-heavy employee/profile details.
- Compensation/payroll/benefits payload.

## 12. Permission Boundary

Approved permission namespace:

- `hcm.workforce-planning.read`
- `hcm.workforce-planning.manage`
- `hcm.workforce-planning.evaluate`
- `hcm.workforce-planning.audit.read`

Candidate and blocked legacy module IDs must not be used as runtime literals.

## 13. Tenant / Security Boundary

Runtime work must be tenant-aware and fail closed. The backend/API slice must
resolve tenant server-side and must not expose `TenantId` in request DTOs.

Security guardrails:

- Candidate identity must not become a runtime literal.
- Blocked legacy ID must not become a runtime literal.
- Backend authorization is authoritative; RBAC is enforced server-side.
- Frontend permission checks remain UX-only.
- Tenant isolation applies to all persistence and query paths.

## 14. Compliance / Privacy Boundary

Legal/privacy/consent is approved only as metadata-only waiver state.

Approved first-slice waiver decisions:

- Metadata-only consent/precondition representation.
- Workforce planning data minimization fail-closed behavior.
- Retention/evidence/audit/legal-hold/deletion as local/deferred metadata.
- Exclusion of workforce planning narrative, forecast payloads, and PII-heavy
  data.

## 15. Integration Boundary

Deferred/out-of-scope modules:

- `MOD-0027` Notification.
- `MOD-0263` Notification Provider / Delivery.
- `MOD-0029` Controlled Documents.
- `MOD-0262` External Docs Repository.

No integration implementation, provider payload persistence, or
document/notification UI is authorized.

## 16. Acceptance Criteria

Runtime-testable acceptance criteria:

1. AC-01: Pack status is `draft`.
2. AC-02: Candidate identity is `CAND-CAP-0032`.
3. AC-03: Legacy Excel ID `MOD-0310` remains blocked/unsafe until EA canonical
   MOD assignment.
4. AC-04: Runtime owner/key is `hcm.workforce-planning`.
5. AC-05: Permission namespace is limited to `hcm.workforce-planning.read`,
   `hcm.workforce-planning.manage`, `hcm.workforce-planning.evaluate`, and
   `hcm.workforce-planning.audit.read`.
6. AC-06: Runtime repo scope is limited to
   `services/Diten.HumanCapitalService/**`.
7. AC-07: The metadata contract supports create -> list -> get -> evaluate ->
   delete end-to-end (E3).
8. AC-08: `TenantId` is not accepted in request DTOs; tenant is resolved
   server-side.
9. AC-09: Mongo persistence (collection `hcm_workforce_planning_readiness`)
   includes active tenant `Code` uniqueness and tenant isolation (E4).
10. AC-10: Soft delete uses `IsDeleted` and `DeletedAt` and hides deleted
    records.
11. AC-11: Evaluation/activation remains fail-closed and can produce
    non-activating `Deferred` metadata.
12. AC-12: RBAC is enforced server-side across the controller surface.
13. AC-13: Workforce planning workflow, headcount plan management, demand
    forecast, supply forecast, gap analysis, scenario modeling, model output,
    and automated decision behavior remain unauthorized.
14. AC-14: Manager/employee workforce planning fields remain reserved/out of
    scope; no money field is present.
15. AC-15: Free-text workforce planning notes, forecast narrative, attachment
    payload, raw provider payload, credential/token/secret/password, and
    PII-heavy persistence remain unauthorized.
16. AC-16: Notification and document dependencies remain deferred/out of scope.
17. AC-17: HCM, TEP, and PSS dependency boundaries are recorded.
18. AC-18: `tr`/`en` localization parity is present.
19. AC-19: Golden-compact parity and list-DTO to JS column parity hold with no
    drift.
20. AC-20: Runtime literal scan for `CAND-CAP-0032|MOD-0310` passes under
    runtime/frontend/gateway/test paths.

## 17. Test Expectations

Runtime validation expectations:

- Confirm pack file exists.
- Confirm frontmatter `status: draft`.
- Confirm all 20 sections are present.
- Build HCM API:
  `dotnet build services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Api/Diten.HumanCapitalService.Api.csproj -c Debug --no-restore /nr:false -m:1 --tl:off`
- Run targeted application tests with `WorkforcePlanning` filter
  (`Application.Tests/WorkforcePlanningTests.cs`, handler behavior; fix-absent
  should be RED).
- Run full HCM Application tests.
- Verify metadata contract create/list/get/evaluate/delete behavior (E3).
- Verify Mongo persistence, tenant isolation, and active tenant `Code`
  uniqueness (E4).
- Verify soft delete hides deleted records and sets `DeletedAt`.
- Verify permission-gated controller surface (RBAC server-side).
- Verify dependency/precondition fail-closed behavior.
- Verify non-activating evaluation deferred metadata behavior.
- Verify forbidden-marker guard rejects forbidden field classes.
- Verify the forbidden-marker matcher uses word/token boundaries (`\b` regex),
  not bare substring `Contains`, so legitimate words (`workforce`, `taxonomy`,
  `planning`, `forecast`) are not falsely rejected.
- Verify forbidden workflow/headcount/demand/supply/gap/scenario/scoring/
  decision/sensitive fields are absent.
- Verify manager/employee workforce planning fields are reserved/absent and no
  money field exists.
- Verify no production in-memory repository exists.
- Verify golden-compact parity and list-DTO to JS column parity (no drift).
- Verify `tr`/`en` localization parity.
- Verify runtime literal scan:
  `rg -n "CAND-CAP-0032|MOD-0310" services/Diten.HumanCapitalService frontend gateway tests`

## 18. Governance Approval Checklist

- [x] Pack status is reviewed for governance approval as a draft slice.
- [x] Candidate reservation is accepted as the governing identity.
- [x] Legacy ID block is accepted.
- [x] Candidate gate script not-executable note is accepted.
- [x] Metadata-only backend/API + golden-compact readiness CRUD + gateway scope
  is accepted.
- [x] Workforce planning workflow prohibition is accepted.
- [x] Headcount plan/demand forecast/supply forecast/gap analysis/scenario
  modeling prohibition is accepted.
- [x] Scoring/model-output prohibition is accepted.
- [x] Automated decision behavior prohibition is accepted.
- [x] Reserved manager/employee workforce planning fields and no-money-field
  constraint are accepted.
- [x] Sensitive payload prohibition is accepted.
- [x] Notification / document dependency deferral is accepted.
- [x] HCM/TEP/PSS dependency context is accepted.

## 19. Runtime-Ready Checklist

- [x] EA candidate-runtime waiver or canonical MOD assignment.
- [x] Runtime owner/key decision.
- [x] Permission namespace decision.
- [x] Runtime repo scope decision.
- [x] Minimal metadata-only workforce planning readiness contract.
- [x] Tenant isolation decision.
- [x] Fail-closed activation/evaluation decision.
- [x] Legal/privacy/consent metadata-only waiver.
- [x] Workforce planning data minimization policy.
- [x] Retention/evidence/audit/legal-hold/deletion boundary.
- [x] Golden-compact readiness CRUD frontend + gateway scope decision.

Runtime-ready blockers:

- Draft implementation in progress; done-promotion audit not yet executed.

Review-ready reconciliation note:

- Metadata-only backend/API slice plus golden-compact readiness CRUD frontend
  and gateway route are scoped under `services/Diten.HumanCapitalService/**`
  and the owned `frontend/Diten.Web/**` WorkforcePlanning objects.
- Runtime owner/key remains limited to `hcm.workforce-planning`.
- Permission namespace remains limited to `hcm.workforce-planning.read`,
  `hcm.workforce-planning.manage`, `hcm.workforce-planning.evaluate`, and
  `hcm.workforce-planning.audit.read`.
- Section 4 metadata fields are aligned with the intended runtime
  public/persisted contract.
- `OrganizationStructureDependencyState`, `PositionFrameworkDependencyState`,
  `DocumentDependencyState`, and `NotificationDependencyState` remain
  metadata-only dependency states.
- Manager/employee workforce planning UX fields remain reserved/out of scope
  and no money field is present.
- TEP, PSS, and Platform scopes remain closed.
- Workforce planning workflow, headcount plan management, demand forecast,
  supply forecast, gap analysis, scenario modeling, model output, automated
  decision behavior, notification/document integration, free-text workforce
  planning notes, forecast narrative, attachment payload, raw provider payload,
  credential/token/secret/password, and PII-heavy persistence remain out of
  scope.

Done-promotion reconciliation note:

- Pending. Done-promotion readiness audit will be recorded when the draft slice
  completes implementation review, with build/test/runtime-literal-scan and
  golden-compact/column-parity evidence attached.

## 20. Open Blockers / Waivers

Open blockers for governance continuation:

- none.

Open blockers for runtime-ready:

- Draft implementation and done-promotion readiness audit are pending.

Waivers recorded:

- Candidate gate script is not executable in this checkout because
  `.antigravity/scripts/verify_module_id.py` is missing; recorded as governance
  note. The specified command
  `python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0032 --name "Workforce Planning"`
  exits non-zero (exit 2, file not found).
- EA/registry owner accepted candidate-based governance continuation for
  `CAND-CAP-0032`.
- EA/registry owner accepted verifier absence as a governance note.
- EA candidate-runtime policy waiver approved for the first metadata-only
  backend/API readiness slice (K19-complete), with golden-compact readiness
  CRUD frontend and gateway route in scope because the Blueprint has no
  canonical MOD for this capability yet.
- Legal/privacy/consent metadata-only waiver approved.
- Workforce planning data minimization fail-closed policy approved.
- Retention/evidence/audit/legal-hold/deletion local/deferred metadata policy
  approved.

Runtime literal scan:

- Required command:
  `rg -n "CAND-CAP-0032|MOD-0310" services/Diten.HumanCapitalService frontend gateway tests`

Next safe step:

- Complete the draft metadata-only backend/API + golden-compact readiness CRUD
  slice and run the done-promotion readiness audit.

Future follow-ups:

- Real workforce planning workflow.
- Headcount plan management and demand forecast.
- Supply forecast, gap analysis, and scenario modeling governance.
- Automated decision approval.
- Manager/employee workforce planning UX beyond readiness CRUD.
- Notification/document integrations.
- Export governance.
- Real audit/evidence/retention integration.
