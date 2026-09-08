---
id: CAND-CAP-0034
name: HR KPI & Analytics Facade
domain: human-capital-management
service: Diten.HumanCapitalService
shell: tenant
golden_reference: golden-compact
entity_base: BaseEntity
status: draft
owner: enterprise-architect / hcm-domain-owner / platform-team / security-owner / legal-owner
branch: feature/hcm/cand-cap-0034-hr-kpi-analytics-facade
started: 2026-09-07
target: 2026-12-15
form_field_count: 0
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/blueprint-master-plan-reconciliation.md
candidate_identity: CAND-CAP-0034
legacy_excel_id: MOD-0312
canonicalization_status: candidate / EA-waived-for-runtime-first-slice
runtime_service_candidate: Diten.HumanCapitalService
runtime_service_port: 5059
gateway_port: 5080
bootstrap_type: metadata-only-backend-api-readiness
runtime_owner_key: hcm.hr-kpi-analytics
permission_namespace:
  - hcm.hr-kpi-analytics.read
  - hcm.hr-kpi-analytics.manage
  - hcm.hr-kpi-analytics.evaluate
  - hcm.hr-kpi-analytics.audit.read
runtime_repo_scope: services/Diten.HumanCapitalService/**
---

# CAND-CAP-0034 - HR KPI & Analytics Facade

> Status: draft. This pack records the first metadata-only backend/API
> HR KPI and analytics facade readiness contract slice under
> `services/Diten.HumanCapitalService/**`, plus a golden-compact readiness CRUD
> frontend under `frontend/Diten.Web/**` and gateway exposure through the API
> gateway (`5080` -> HCM `5059`). Production HR KPI and analytics facade
> workflow, KPI catalog management, metric definition, dashboard, analytics
> query, data export, automated decision behavior, manager/employee analytics
> UX, notification/document integration, and sensitive persistence remain
> closed. This is a FACADE over the SHARED analytics platform; it does not own
> or compute analytics. `CAND-CAP-0034` remains a governance/documentation
> identity only and must not be written into runtime literals. `MOD-0312`
> remains blocked for HCM use until future EA canonical MOD assignment.

## 1. Module Summary

`CAND-CAP-0034` reserves the temporary DCP-002 candidate identity for the native
Human Capital Management R3-C capability named
`HR KPI & Analytics Facade`.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`.
DCP-008 proposed legacy Excel ID `MOD-0312`, but DCP-002 blocks that ID because
it is not Blueprint-backed and has no canonical registry row. EA/registry owner
reservation records `CAND-CAP-0034` as a temporary governance identity pending
future canonical MOD allocation.

This pack records the first metadata-only backend/API HR KPI and analytics facade
readiness contract slice, mirroring the Headcount & Position Budget Planning slice
(`CAND-CAP-0033` / `MOD-0311`) and the completed Workforce Planning slice
(`CAND-CAP-0032` / `MOD-0310`) exactly. This capability is a FACADE over the
shared analytics platform: it is metadata-only readiness and does NOT own or
compute analytics. HR KPI and analytics facade workflow, KPI catalog management,
metric definition, dashboard, analytics query, data export, automated decision
behavior, manager/employee analytics UX, notification/document integration, and
sensitive persistence remain closed.

## 2. Ownership and Boundaries

Owned by this pack:

- HCM-native HR KPI and analytics facade governance boundary.
- Sequencing after completed HCM employee, governance, position, offboarding,
  applicant intake, candidate pipeline, offer management, onboarding,
  employment change, performance review, competency/skills, development plan,
  learning/training, succession/high-potential, workforce planning, and
  headcount/position budget planning foundation modules.
- Candidate identity reservation and blocked legacy ID rationale.
- Metadata-only HR KPI and analytics facade readiness backend/API
  contract.
- Golden-compact readiness CRUD frontend and gateway exposure limited to the
  metadata-only readiness contract.
- Explicit exclusion of HR KPI and analytics facade workflow
  execution.
- Explicit exclusion of KPI catalog management, metric definition,
  dashboard, analytics query, data export, model output, and
  automated decision behavior.
- Explicit exclusion of manager-facing and employee-facing analytics
  UX beyond the readiness-metadata CRUD (those fields are reserved and out of
  scope).
- Explicit exclusion of free-text analytics notes, analytics narrative,
  attachment payload, raw provider payload,
  credential/token/secret/password, and PII-heavy persistence.

Not owned by this pack:

- Real HR KPI and analytics facade execution, KPI computation, metric
  calculation, dashboard rendering, analytics query execution, report
  generation, data export/extract runs, ratings, rankings, or model
  output.
- Analytics platform ownership beyond dependency/context references.
- Data source ownership beyond dependency/context references.
- Notification, notification provider, controlled document, or external
  document repository implementation.
- Development plan, competency/skills, performance review, learning/training,
  succession, workforce planning, headcount/budget, shared analytics platform,
  data warehouse, TEP, or PSS ownership transfer.

## 3. Owned Objects

Governance-only objects:

| Object | Type | Purpose |
|---|---|---|
| HrKpiAnalyticsCapabilityBoundary | Governance boundary | Defines what a future HCM HR KPI and analytics facade module may own. |
| HrKpiAnalyticsReadinessBoundary | Governance boundary | Separates readiness metadata from analytics facade workflow execution. |
| KpiMetricCatalogBoundary | Governance boundary | Blocks KPI catalog management, metric definition, and dashboard execution. |
| DashboardAnalyticsQueryBoundary | Governance boundary | Blocks analytics query and data export execution. |
| HrKpiAnalyticsDecisionBoundary | Governance boundary | Blocks scoring, rating, ranking, model-output, and automated decision persistence. |
| HrKpiAnalyticsUxBoundary | Governance boundary | Blocks manager and employee analytics UX beyond readiness-metadata CRUD. |
| HrKpiAnalyticsSensitiveDataBoundary | Governance boundary | Blocks narrative, attachment, credential, computed analytics value, and PII-heavy persistence. |
| HrKpiAnalyticsDependencyBoundary | Governance boundary | Keeps analytics-platform/data-source/notification/document dependencies deferred and out of scope. |

Deferred runtime objects:

| Object | Type | Purpose |
|---|---|---|
| HrKpiAnalyticsFacadeWorkflow | Deferred runtime | HR KPI and analytics facade workflow execution; not authorized. |
| KpiMetricCatalogEngine | Deferred runtime | KPI catalog management, metric definition, and dashboard; not authorized. |
| DashboardAnalyticsQueryEngine | Deferred runtime | Analytics query, data export, and model output; not authorized. |
| ManagerEmployeeAnalyticsExperience | Deferred frontend/runtime | Manager/employee analytics UX beyond readiness CRUD; not authorized. |
| HrKpiAnalyticsDocumentIntegration | Deferred dependency | Controlled/external document use; out of scope. |
| HrKpiAnalyticsNotificationIntegration | Deferred dependency | Notification and reminder delivery; out of scope. |

Approved runtime/frontend objects (metadata-only readiness slice):

| Object | Type | Purpose |
|---|---|---|
| HrKpiAnalyticsReadinessMetadata | Domain entity | Metadata-only readiness contract; `Domain/Entities/HrKpiAnalyticsReadinessMetadata.cs`. |
| HrKpiAnalyticsReadinessState | Domain enum | Readiness state; `Domain/Enums/HrKpiAnalyticsReadinessState.cs` (Draft=0, Ready=1, Deferred=2, Blocked=3, NotRequired=4, Archived=5). |
| IHrKpiAnalyticsReadinessMetadataRepository | Domain repository | Tenant-aware repository contract; `Domain/Repositories/IHrKpiAnalyticsReadinessMetadataRepository.cs`. |
| MongoHrKpiAnalyticsReadinessMetadataRepository | Persistence repository | Mongo-backed repository; collection `hcm_hr_kpi_analytics_readiness`. |
| HrKpiAnalytics Application features | Application (CQRS/MediatR) | `Application/Features/HrKpiAnalytics/**` (Models/Guard/Commands/Queries/Handlers). |
| HrKpiAnalyticsController | API controller | Thin controller; `Api/Controllers/Hcm/HrKpiAnalyticsController.cs`. |
| HrKpiAnalytics golden-compact CRUD | Frontend | Controller + `Views/HumanCapital/HrKpiAnalytics/**` + `wwwroot/assets/js/HumanCapital/HrKpiAnalytics/**` + Resources + nav entry under `frontend/Diten.Web/**`. |

## 4. Entity Fields

Approved metadata-only persisted contract:

Minimal testable metadata fields (all readiness-state fields are of type
`HrKpiAnalyticsReadinessState`):

- `Code`
- `DisplayName`
- `HrKpiAnalyticsReadinessState`
- `KpiCatalogBoundaryState`
- `MetricDefinitionBoundaryState`
- `DashboardBoundaryState`
- `AnalyticsQueryBoundaryState`
- `DataExportBoundaryState`
- `AutomatedDecisionBoundaryState`
- `AnalyticsPlatformDependencyState`
- `DataSourceDependencyState`
- `DocumentDependencyState`
- `NotificationDependencyState`
- `ConsentPreconditionState`
- `DataMinimizationState`
- `RetentionPolicyState`
- `EvidencePolicyState`
- `DependencyStates`
- `SourceContractVersion`
- `LastEvaluatedAt`
- `HrKpiAnalyticsReadinessVersion`
- `DeferredReason`
- `IsDeleted`
- `DeletedAt`
- `CreatedAt`
- `UpdatedAt`

Reserved / out-of-scope fields (must not be added in this slice):

- Manager-facing analytics fields (manager dashboard access, manager
  review, manager action UX state) are reserved and out of scope.
- Employee-facing analytics fields (employee self-service input,
  employee acknowledgement, employee self-service UX state) are reserved and
  out of scope.
- No computed analytics field (computed KPI value/metric result/dashboard
  dataset/exported analytics data). This is a
  metadata-only readiness slice with no computed analytics attribute. Computed
  KPI VALUES, metric RESULTS, dashboard DATASETS, and exported analytics DATA
  are FORBIDDEN; only boundary/readiness STATE metadata is persisted, never any
  computed KPI value, metric result, dashboard dataset, or exported analytics
  data.

Forbidden field classes:

- HR KPI and analytics facade workflow body, KPI catalog
  submission payload, metric definition payload, dashboard payload, analytics
  query payload, data export payload, query decision body, action evidence, or
  completed analytics content.
- Readiness score, rating value, rank, analytics outcome, model output,
  automated decision result, recommendation output, or evaluator scoring
  payload.
- Free-text analytics notes, analytics narrative, manager comments,
  employee comments, HR notes, or sensitive analytics body.
- Attachment payload, controlled document body, external document content, raw
  evidence, raw provider payload, or uploaded file metadata.
- Computed KPI value, metric result, dashboard dataset, exported analytics data,
  score, rating, model output, benefits election, payroll details, tax
  details, bank details, or payment instruction fields.
- Credential, token, secret, password, activation code, provider connection
  value, device secret, or account bootstrap secret.
- National ID, DOB, home address, biometric/geolocation data, medical data, or
  PII-heavy employee/profile fields.

## 5. Repo Scope

Authorized governance scope:

- `execution/domains/human-capital-management/module-packs/CAND-CAP-0034-hr-kpi-analytics-facade.md`

Candidate future runtime service:

- `Diten.HumanCapitalService` (port `5059`)

Runtime repo scope:

- `services/Diten.HumanCapitalService/**`

Frontend/gateway scope (metadata-only readiness CRUD only):

- `frontend/Diten.Web/**` (Controller, `Views/HumanCapital/HrKpiAnalytics/**`,
  `wwwroot/assets/js/HumanCapital/HrKpiAnalytics/**`, Resources, nav entry).
- Gateway route exposure through the API gateway (`5080` -> HCM `5059`).

## 6. Protected Paths

This pack authorizes only the first metadata-only backend/API readiness slice
under `services/Diten.HumanCapitalService/**` plus its golden-compact readiness
CRUD frontend and gateway route, and does not authorize changes to:

- `frontend/**` outside the owned HrKpiAnalytics CRUD objects listed in
  §3/§5.
- `gateway/**` outside the owned HrKpiAnalytics readiness route.
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
- `CAND-CAP-0032` - Workforce Planning, done.
- `CAND-CAP-0033` - Headcount & Position Budget Planning, done.

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

- Runtime owner/key: `hcm.hr-kpi-analytics`.
- Permission namespace:
  - `hcm.hr-kpi-analytics.read`
  - `hcm.hr-kpi-analytics.manage`
  - `hcm.hr-kpi-analytics.evaluate`
  - `hcm.hr-kpi-analytics.audit.read`
- Runtime repo scope: `services/Diten.HumanCapitalService/**`.
- Thin controller (`Api/Controllers/Hcm/HrKpiAnalyticsController.cs`),
  CQRS/MediatR, `Response<T>` envelope.
- Server-side tenant context; `TenantId` must not be accepted in request DTOs.
- Mongo-backed tenant-aware repository; collection
  `hcm_hr_kpi_analytics_readiness`.
- Active tenant `Code` uniqueness.
- Soft delete through `IsDeleted` and `DeletedAt`.
- Fail-closed activation/evaluation.
- Non-activating evaluation may produce explicit `Deferred` metadata.
- Forbidden-marker guard rejecting forbidden field classes at the application
  boundary. This slice fixes the forbidden-marker matcher to use WORD/TOKEN
  boundaries (`\b` regex) instead of a bare substring `Contains`, so legitimate
  domain words (`kpi`, `analytics`, `metric`, `dashboard`) are not
  falsely rejected while true forbidden markers (`score`, `rating`,
  `model_output`, `salary`, ...) are still caught.

Explicitly unauthorized:

- HR KPI and analytics facade workflow.
- KPI catalog management, metric definition, dashboard, analytics query, data
  export.
- Scoring/rating/ranking/model-output persistence.
- Automated decision behavior.
- Manager/employee analytics UX beyond readiness-metadata CRUD.
- Notification and document integrations.
- Sensitive payload and computed analytics value persistence.

## 9. Frontend / UX Boundary

Golden-compact readiness CRUD frontend and gateway route are authorized, limited
to the metadata-only readiness contract.

Authorized UX:

- Tenant shell nav entry `HR KPI & Analytics Facade`.
- Golden-compact readiness CRUD (list/get/create/evaluate/delete) bound to the
  metadata-only contract.
- List-DTO to JS column parity with no drift.
- `tr`/`en` localization parity through Resources.

Unauthorized UX:

- Manager analytics UX beyond readiness-metadata CRUD.
- Employee analytics/self-service UX beyond readiness-metadata CRUD.
- KPI catalog, metric definition, dashboard, analytics query, data
  export, scoring, or model-output UI.
- Document, notification, export, upload, or attachment UI.

## 10. Backend / API Boundary

Only metadata-only backend/API implementation is authorized, exposed through the
gateway (`5080` -> HCM `5059`). This is a FACADE that owns no analytics runtime:
it does NOT authorize real KPI computation, metric calculation, dashboard
rendering, analytics query execution, report generation, data export/extract,
or ownership of the shared analytics platform / data warehouse.

Authorized API surface:

- `GET /api/hr-kpi-analytics` - list readiness metadata.
- `GET /api/hr-kpi-analytics/{id}` - get readiness metadata by id.
- `POST /api/hr-kpi-analytics` - create readiness metadata.
- `POST /api/hr-kpi-analytics/{id}/evaluate` - fail-closed evaluation.
- `DELETE /api/hr-kpi-analytics/{id}` - soft-delete.
- `GET /api/hr-kpi-analytics/{id}/audit-metadata` - audit metadata read.

The first slice must stay limited to:

- Create/list/get HR KPI and analytics facade readiness metadata.
- Fail-closed evaluation/deferred metadata.
- Audit metadata read endpoint.
- Soft delete.
- Tenant-aware persistence and active-code uniqueness.
- No analytics facade workflow, KPI catalog management, metric definition,
  dashboard, analytics query, data export, scoring/model-output persistence,
  automated decision behavior, notification/document integration, or
  sensitive/computed-analytics payload persistence.

## 11. Data Boundary

Allowed in the first runtime slice:

- Metadata-only HR KPI and analytics facade readiness state.
- Boundary-state metadata for KPI catalog, metric definition, dashboard,
  analytics query, data export, automated decision,
  consent, minimization, retention, and evidence.
- Dependency-state metadata for analytics platform, data source,
  document, and notification dependencies.
- Dependency/precondition metadata (`DependencyStates` dictionary).

Forbidden:

- HR KPI and analytics facade workflow payload.
- KPI-catalog/metric-definition/dashboard/analytics-query/data-export execution
  payload.
- Readiness score/rating/rank/model output.
- Automated decision result.
- Free-text analytics notes or analytics narrative.
- Analytics query transcript payload.
- Attachment payload.
- Raw provider payload.
- Credential/token/secret/password values.
- PII-heavy employee/profile details.
- Computed KPI value, metric result, dashboard dataset, exported analytics data,
  and compensation/payroll/benefits payload.

## 12. Permission Boundary

Approved permission namespace:

- `hcm.hr-kpi-analytics.read`
- `hcm.hr-kpi-analytics.manage`
- `hcm.hr-kpi-analytics.evaluate`
- `hcm.hr-kpi-analytics.audit.read`

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
- Analytics data minimization fail-closed behavior.
- Retention/evidence/audit/legal-hold/deletion as local/deferred metadata.
- Exclusion of analytics narrative, analytics payloads, computed analytics
  values, and PII-heavy data.

## 15. Integration Boundary

Deferred/out-of-scope modules:

- `MOD-0027` Notification.
- `MOD-0263` Notification Provider / Delivery.
- `MOD-0029` Controlled Documents.
- `MOD-0262` External Docs Repository.

The shared analytics platform / data warehouse dependency is deferred and out of
scope: this facade only consumes readiness metadata and owns no analytics
runtime. No integration implementation, provider payload persistence, or
document/notification UI is authorized.

## 16. Acceptance Criteria

Runtime-testable acceptance criteria:

1. AC-01: Pack status is `draft`.
2. AC-02: Candidate identity is `CAND-CAP-0034`.
3. AC-03: Legacy Excel ID `MOD-0312` remains blocked/unsafe until EA canonical
   MOD assignment.
4. AC-04: Runtime owner/key is `hcm.hr-kpi-analytics`.
5. AC-05: Permission namespace is limited to `hcm.hr-kpi-analytics.read`,
   `hcm.hr-kpi-analytics.manage`, `hcm.hr-kpi-analytics.evaluate`, and
   `hcm.hr-kpi-analytics.audit.read`.
6. AC-06: Runtime repo scope is limited to
   `services/Diten.HumanCapitalService/**`.
7. AC-07: The metadata contract supports create -> list -> get -> evaluate ->
   delete end-to-end (E3).
8. AC-08: `TenantId` is not accepted in request DTOs; tenant is resolved
   server-side.
9. AC-09: Mongo persistence (collection `hcm_hr_kpi_analytics_readiness`)
   includes active tenant `Code` uniqueness and tenant isolation (E4).
10. AC-10: Soft delete uses `IsDeleted` and `DeletedAt` and hides deleted
    records.
11. AC-11: Evaluation/activation remains fail-closed and can produce
    non-activating `Deferred` metadata.
12. AC-12: RBAC is enforced server-side across the controller surface.
13. AC-13: HR KPI and analytics facade workflow, KPI catalog management, metric
    definition, dashboard, analytics query, data export, model output, and
    automated decision behavior remain unauthorized.
14. AC-14: Manager/employee analytics fields remain reserved/out of
    scope; no computed analytics field is present.
15. AC-15: Free-text analytics notes, analytics narrative, attachment
    payload, raw provider payload, credential/token/secret/password, and
    PII-heavy persistence remain unauthorized.
16. AC-16: Notification and document dependencies remain deferred/out of scope.
17. AC-17: HCM, TEP, and PSS dependency boundaries are recorded.
18. AC-18: `tr`/`en` localization parity is present.
19. AC-19: Golden-compact parity and list-DTO to JS column parity hold with no
    drift.
20. AC-20: Runtime literal scan for `CAND-CAP-0034|MOD-0312` passes under
    runtime/frontend/gateway/test paths.

## 17. Test Expectations

Runtime validation expectations:

- Confirm pack file exists.
- Confirm frontmatter `status: draft`.
- Confirm all 20 sections are present.
- Build HCM API:
  `dotnet build services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Api/Diten.HumanCapitalService.Api.csproj -c Debug --no-restore /nr:false -m:1 --tl:off`
- Run targeted application tests with `HrKpiAnalytics` filter
  (`Application.Tests/HrKpiAnalyticsTests.cs`, handler behavior; fix-absent
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
  not bare substring `Contains`, so legitimate words (`kpi`, `analytics`,
  `metric`, `dashboard`) are not falsely rejected.
- Verify forbidden workflow/kpi-catalog/metric-definition/dashboard/analytics-query/
  data-export/scoring/decision/sensitive/computed-analytics fields are absent.
- Verify manager/employee analytics fields are reserved/absent and no
  computed analytics field exists.
- Verify no production in-memory repository exists.
- Verify golden-compact parity and list-DTO to JS column parity (no drift).
- Verify `tr`/`en` localization parity.
- Verify runtime literal scan:
  `rg -n "CAND-CAP-0034|MOD-0312" services/Diten.HumanCapitalService frontend gateway tests`

## 18. Governance Approval Checklist

- [x] Pack status is reviewed for governance approval as a draft slice.
- [x] Candidate reservation is accepted as the governing identity.
- [x] Legacy ID block is accepted.
- [x] Candidate gate script not-executable note is accepted.
- [x] Metadata-only backend/API + golden-compact readiness CRUD + gateway scope
  is accepted.
- [x] HR KPI and analytics facade workflow prohibition is accepted.
- [x] KPI catalog/metric definition/dashboard/analytics
  query/data export prohibition is accepted.
- [x] Scoring/model-output prohibition is accepted.
- [x] Automated decision behavior prohibition is accepted.
- [x] Reserved manager/employee analytics fields and no-computed-analytics-field
  constraint are accepted.
- [x] Sensitive payload and computed analytics value prohibition is accepted.
- [x] Notification / document dependency deferral is accepted.
- [x] HCM/TEP/PSS dependency context is accepted.

## 19. Runtime-Ready Checklist

- [x] EA candidate-runtime waiver or canonical MOD assignment.
- [x] Runtime owner/key decision.
- [x] Permission namespace decision.
- [x] Runtime repo scope decision.
- [x] Minimal metadata-only HR KPI and analytics facade readiness
  contract.
- [x] Tenant isolation decision.
- [x] Fail-closed activation/evaluation decision.
- [x] Legal/privacy/consent metadata-only waiver.
- [x] Analytics data minimization policy.
- [x] Retention/evidence/audit/legal-hold/deletion boundary.
- [x] Golden-compact readiness CRUD frontend + gateway scope decision.

Runtime-ready blockers:

- Draft implementation in progress; done-promotion audit not yet executed.

Review-ready reconciliation note:

- Metadata-only backend/API slice plus golden-compact readiness CRUD frontend
  and gateway route are scoped under `services/Diten.HumanCapitalService/**`
  and the owned `frontend/Diten.Web/**` HrKpiAnalytics objects.
- Runtime owner/key remains limited to `hcm.hr-kpi-analytics`.
- Permission namespace remains limited to `hcm.hr-kpi-analytics.read`,
  `hcm.hr-kpi-analytics.manage`, `hcm.hr-kpi-analytics.evaluate`, and
  `hcm.hr-kpi-analytics.audit.read`.
- Section 4 metadata fields are aligned with the intended runtime
  public/persisted contract.
- `AnalyticsPlatformDependencyState`, `DataSourceDependencyState`,
  `DocumentDependencyState`, and `NotificationDependencyState` remain
  metadata-only dependency states.
- Manager/employee analytics UX fields remain reserved/out of scope
  and no computed analytics field is present.
- TEP, PSS, and Platform scopes remain closed.
- HR KPI and analytics facade workflow, KPI catalog
  management, metric definition, dashboard, analytics query, data export,
  model output, automated decision behavior,
  notification/document integration, free-text analytics notes, analytics
  narrative, attachment payload, raw provider payload,
  credential/token/secret/password, computed analytics value, and PII-heavy
  persistence remain out of scope.

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
  `python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0034 --name "HR KPI & Analytics Facade"`
  exits non-zero (exit 2, file not found).
- EA/registry owner accepted candidate-based governance continuation for
  `CAND-CAP-0034`.
- EA/registry owner accepted verifier absence as a governance note.
- EA candidate-runtime policy waiver approved for the first metadata-only
  backend/API readiness slice (K19-complete), with golden-compact readiness
  CRUD frontend and gateway route in scope because the Blueprint has no
  canonical MOD for this capability yet.
- Legal/privacy/consent metadata-only waiver approved.
- Analytics data minimization fail-closed policy approved.
- Retention/evidence/audit/legal-hold/deletion local/deferred metadata policy
  approved.

Runtime literal scan:

- Required command:
  `rg -n "CAND-CAP-0034|MOD-0312" services/Diten.HumanCapitalService frontend gateway tests`

Next safe step:

- Complete the draft metadata-only backend/API + golden-compact readiness CRUD
  slice and run the done-promotion readiness audit.

Future follow-ups:

- Real HR KPI and analytics facade workflow.
- KPI catalog management and metric definition.
- Dashboard, analytics query, and data export governance.
- Automated decision approval.
- Manager/employee analytics UX beyond readiness CRUD.
- Notification/document integrations.
- Export governance.
- Real audit/evidence/retention integration.
