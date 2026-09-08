---
id: CAND-CAP-0041
name: Talent Data Foundation
domain: talent-ecosystem-platform
service: Diten.TalentEcosystemService
shell: tenant
golden_reference: golden-compact
entity_base: BaseEntity
status: draft
owner: enterprise-architect / tep-domain-owner / platform-team / security-owner / legal-owner
branch: feature/tep/cand-cap-0041-talent-data-foundation
started: 2026-09-07
target: 2026-12-08
form_field_count: 0
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/blueprint-master-plan-reconciliation.md
candidate_identity: CAND-CAP-0041
legacy_excel_id: MOD-0336
canonicalization_status: candidate / EA-waived-for-runtime-first-slice
runtime_service_candidate: Diten.TalentEcosystemService
runtime_service_port: 5060
gateway_port: 5080
bootstrap_type: runtime-ready-first-slice
runtime_owner_key: tep.talent-data-foundation
permission_namespace:
  - tep.talent-data-foundation.read
  - tep.talent-data-foundation.manage
  - tep.talent-data-foundation.evaluate
  - tep.talent-data-foundation.audit.read
runtime_repo_scope: services/Diten.TalentEcosystemService/**
---

# CAND-CAP-0041 - Talent Data Foundation

> Status: draft. This pack records the first metadata-only backend/API talent
> data foundation readiness contract slice under
> `services/Diten.TalentEcosystemService/**`, plus a golden-compact readiness
> CRUD frontend under `frontend/Diten.Web/**` and gateway exposure through the
> API gateway (`5080` -> TEP `5060`). Real talent data ingestion/ETL, raw
> talent/candidate record or resume/CV/profile payload persistence, identity
> resolution execution, data-quality scoring, model output or automated decision
> behavior, manager/employee talent-data UX, notification/document integration,
> and sensitive/PII-heavy persistence remain closed. `CAND-CAP-0041` remains a
> governance/documentation identity only and must not be written into runtime
> literals. `MOD-0336` remains blocked for TEP use until future EA canonical MOD
> assignment.

## 1. Module Summary

`CAND-CAP-0041` reserves the temporary DCP-002 candidate identity for the native
Talent Ecosystem Platform R4 capability named `Talent Data Foundation`.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`.
DCP-008 proposed legacy Excel ID `MOD-0336`, but DCP-002 blocks that ID because
it is not Blueprint-backed and has no canonical registry row. EA/registry owner
reservation records `CAND-CAP-0041` as a temporary governance identity pending
future canonical MOD allocation.

This pack records the first metadata-only backend/API talent data foundation
readiness contract slice, mirroring the completed TEP candidate readiness slices
(`CAND-CAP-0017` / `MOD-0322` through `CAND-CAP-0021` / `MOD-0331`) exactly. The
Talent Data Foundation is the foundational readiness layer for talent data in
the Talent Ecosystem Platform: it declares the readiness STATE of the talent
entity catalog, the data ingestion boundary, identity resolution, data quality,
and lineage tracking. It underpins downstream TEP talent modules by publishing
the readiness of the shared talent data foundation. Real talent data
ingestion/ETL, raw talent/candidate record persistence, identity resolution
execution, data-quality scoring, model output, automated decision behavior,
manager/employee talent-data UX, notification/document integration, and
sensitive/PII-heavy persistence remain closed.

## 2. Ownership and Boundaries

Owned by this pack:

- TEP-native talent data foundation governance boundary.
- Sequencing after completed HCM foundation modules and completed TEP foundation
  and candidate readiness modules through `CAND-CAP-0021`.
- Candidate identity reservation and blocked legacy ID rationale.
- Metadata-only talent data foundation readiness backend/API contract.
- Golden-compact readiness CRUD frontend and gateway exposure limited to the
  metadata-only readiness contract.
- Declaration of readiness STATE for the shared talent data foundation consumed
  by downstream TEP talent modules.
- Explicit exclusion of real talent data ingestion/ETL execution.
- Explicit exclusion of identity resolution execution, data-quality scoring,
  lineage capture execution, model output, and automated decision behavior.
- Explicit exclusion of manager-facing and employee-facing talent-data UX beyond
  the readiness-metadata CRUD (those fields are reserved and out of scope).
- Explicit exclusion of raw talent/candidate records, resume/CV content, profile
  payloads, attachment payload, raw provider payload,
  credential/token/secret/password, and any PII dataset persistence.

Not owned by this pack:

- Real talent data ingestion, ETL, streaming, or batch load execution.
- Raw talent/candidate record, resume/CV, or profile payload storage.
- Identity resolution execution, entity matching runs, or dedup/merge output.
- Data-quality scoring, profiling runs, or quality metric output.
- Lineage capture runtime, provenance graph storage, or transformation logs.
- Candidate identity/profile ownership beyond dependency/context references
  (owned by `CAND-CAP-0017`).
- Notification, notification provider, controlled document, or external document
  repository implementation.
- HCM, PSS directory, HRIS, payroll, time, analytics, or provider ownership
  transfer.

## 3. Owned Objects

Governance-only objects:

| Object | Type | Purpose |
|---|---|---|
| TalentDataFoundationCapabilityBoundary | Governance boundary | Defines what a future TEP talent data foundation module may own. |
| TalentDataFoundationReadinessBoundary | Governance boundary | Separates readiness metadata from talent data ingestion/ETL execution. |
| TalentEntityCatalogBoundary | Governance boundary | Blocks raw talent entity catalog record persistence. |
| DataIngestionBoundary | Governance boundary | Blocks real data ingestion/ETL, streaming, and batch load execution. |
| IdentityResolutionBoundary | Governance boundary | Blocks entity matching, dedup/merge, and identity resolution execution. |
| DataQualityBoundary | Governance boundary | Blocks data-quality scoring, profiling runs, and quality metric output. |
| LineageTrackingBoundary | Governance boundary | Blocks lineage capture runtime, provenance storage, and transformation logs. |
| TalentDataFoundationDecisionBoundary | Governance boundary | Blocks scoring, ranking, model-output, and automated decision persistence. |
| TalentDataFoundationUxBoundary | Governance boundary | Blocks manager and employee talent-data UX beyond readiness-metadata CRUD. |
| TalentDataFoundationSensitiveDataBoundary | Governance boundary | Blocks raw talent/candidate record, resume/CV, profile payload, and PII persistence. |

Deferred runtime objects:

| Object | Type | Purpose |
|---|---|---|
| TalentDataIngestionEngine | Deferred runtime | Real talent data ingestion/ETL, streaming, and batch load; not authorized. |
| IdentityResolutionEngine | Deferred runtime | Entity matching, dedup/merge, and identity resolution execution; not authorized. |
| DataQualityEngine | Deferred runtime | Data-quality scoring, profiling, and quality metric output; not authorized. |
| LineageTrackingEngine | Deferred runtime | Lineage capture, provenance graph, and transformation logs; not authorized. |
| ManagerEmployeeTalentDataExperience | Deferred frontend/runtime | Manager/employee talent-data UX beyond readiness CRUD; not authorized. |
| TalentDataDocumentIntegration | Deferred dependency | Controlled/external document use; out of scope. |
| TalentDataNotificationIntegration | Deferred dependency | Notification and reminder delivery; out of scope. |

Approved runtime/frontend objects (metadata-only readiness slice):

| Object | Type | Purpose |
|---|---|---|
| TalentDataFoundationReadinessMetadata | Domain entity | Metadata-only readiness contract; `Domain/Entities/TalentDataFoundationReadinessMetadata.cs`. |
| TalentDataFoundationReadinessState | Domain enum | Readiness state; `Domain/Enums/TalentDataFoundationReadinessState.cs` (Draft=0, Ready=1, Deferred=2, Blocked=3, NotRequired=4, Archived=5). |
| ITalentDataFoundationReadinessMetadataRepository | Domain repository | Tenant-aware repository contract; `Domain/Repositories/ITalentDataFoundationReadinessMetadataRepository.cs`. |
| MongoTalentDataFoundationReadinessMetadataRepository | Persistence repository | Mongo-backed repository; collection `tep_talent_data_foundation_readiness`; indexes `ux_tep_talent_data_foundation_tenant_code_active` and `ix_tep_talent_data_foundation_tenant_state`. |
| TalentDataFoundation Application features | Application (CQRS/MediatR) | `Application/Features/TalentDataFoundation/**` (Models/Guard/Commands x3/Queries x3/Handlers x4). |
| TalentDataFoundationController | API controller | Thin controller; `Api/Controllers/Tep/TalentDataFoundationController.cs`. |
| TalentDataFoundation golden-compact CRUD | Frontend | Controller + `Views/TalentEcosystem/TalentDataFoundation/**` + `wwwroot/assets/js/TalentEcosystem/TalentDataFoundation/**` + Resources + TEP nav entry under `frontend/Diten.Web/**`. |

## 4. Entity Fields

Approved metadata-only persisted contract:

Minimal testable metadata fields (all readiness/boundary/dependency/governance
state fields are of type `TalentDataFoundationReadinessState`):

- `Code`
- `DisplayName`
- `TalentDataFoundationReadinessState`
- `TalentEntityCatalogBoundaryState`
- `DataIngestionBoundaryState`
- `IdentityResolutionBoundaryState`
- `DataQualityBoundaryState`
- `LineageTrackingBoundaryState`
- `AutomatedDecisionBoundaryState`
- `HcmFoundationDependencyState`
- `ConsentPolicyDependencyState`
- `DocumentDependencyState`
- `NotificationDependencyState`
- `ConsentPreconditionState`
- `DataMinimizationState`
- `RetentionPolicyState`
- `EvidencePolicyState`
- `DependencyStates`
- `SourceContractVersion`
- `LastEvaluatedAt`
- `TalentDataFoundationReadinessVersion`
- `DeferredReason`
- `IsDeleted`
- `DeletedAt`
- `CreatedAt`
- `UpdatedAt`

Reserved / out-of-scope fields (must not be added in this slice):

- Manager-facing talent-data fields (manager catalog submission, manager review,
  manager action UX state) are reserved and out of scope.
- Employee-facing talent-data fields (employee self-service input, employee
  acknowledgement, employee self-service UX state) are reserved and out of
  scope.
- No money field (salary/compensation/cost/budget). This is a metadata-only
  readiness slice with no monetary attribute.
- No raw talent/candidate record, resume/CV content, profile payload, or PII
  dataset field of any kind.

Forbidden field classes:

- Talent entity catalog record body, ingestion/ETL payload, identity resolution
  match/merge payload, data-quality profiling payload, lineage/provenance
  payload, or completed talent data content.
- Readiness score, quality score, rating value, rank, match confidence, model
  output, automated decision result, recommendation output, or evaluator scoring
  payload.
- Raw talent/candidate records, resume/CV content, cover letter, free-text
  profile narrative, HR notes, or sensitive talent body.
- Attachment payload, controlled document body, external document content, raw
  evidence, raw provider payload, or uploaded file metadata.
- Salary amount, compensation amount, benefits election, payroll details, tax
  details, bank details, or payment instruction fields.
- Credential, token, secret, password, activation code, provider connection
  value, device secret, or account bootstrap secret.
- National ID, DOB, home address, biometric/geolocation data, medical data, or
  any PII-heavy talent/profile field.

## 5. Repo Scope

Authorized governance scope:

- `execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0041-talent-data-foundation.md`

Candidate runtime service:

- `Diten.TalentEcosystemService` (port `5060`)

Runtime repo scope:

- `services/Diten.TalentEcosystemService/**`

Frontend/gateway scope (metadata-only readiness CRUD only):

- `frontend/Diten.Web/**` (Controller, `Views/TalentEcosystem/TalentDataFoundation/**`,
  `wwwroot/assets/js/TalentEcosystem/TalentDataFoundation/**`, Resources, TEP nav
  entry).
- Gateway route exposure through the API gateway (`5080` -> TEP `5060`).

## 6. Protected Paths

This pack authorizes only the first metadata-only backend/API readiness slice
under `services/Diten.TalentEcosystemService/**` plus its golden-compact
readiness CRUD frontend and gateway route, and does not authorize changes to:

- `frontend/**` outside the owned TalentDataFoundation CRUD objects listed in
  §3/§5.
- `gateway/**` outside the owned TalentDataFoundation readiness route.
- `services/Diten.HumanCapitalService/**`
- `services/Diten.Platform/**`
- `services/Diten.PssService/**`
- global `tests/**`
- completed TEP foundation and candidate readiness packs.
- notification/document module packs listed as out of scope.

## 7. Dependencies

Governance dependencies:

- `execution/domains/talent-ecosystem-platform/domain-config.md`
- `execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`
- `execution/registries/module-id-registry.md`
- `execution/portfolio/blueprint-master-plan-reconciliation.md`
- `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`

Completed TEP foundation and candidate readiness dependencies:

- `CAND-CAP-0011` - Talent Ecosystem Platform Shell, done.
- `CAND-CAP-0012` - Association Membership & Member Company Registry, done.
- `CAND-CAP-0013` - Consent, Visibility & Access Policy, done.
- `CAND-CAP-0014` - Verified HR Participant & Company Access, done.
- `CAND-CAP-0015` - Talent Ecosystem Governance & Review Board, done.
- `CAND-CAP-0016` - Trust Level & Multi-Signature Engine, done.
- `CAND-CAP-0017` - Industry Candidate Identity & Talent Profile, done.
- `CAND-CAP-0018` - Industry Exit Reference Record Registry, done.
- `CAND-CAP-0019` - Reference Exchange Marketplace, done.
- `CAND-CAP-0020` - Rehire Recommendation Network, done.
- `CAND-CAP-0021` - Candidate Response & Dispute Management, done.

HCM foundation dependency context (prerequisite context only):

- `CAND-CAP-0007` - Employee Profile & Employment Record Projection, done.
- `CAND-CAP-0008` - HR Governance & Sensitive Access Controls, done.
- `CAND-CAP-0009` - Position & Organization Assignment, done.
- `CAND-CAP-0010` - Offboarding & Exit Management, done.

PSS backbone context:

- `MOD-0288`, `MOD-0251`, `MOD-0279`, `MOD-0280`, and `MOD-0281` remain
  dependency/backbone context only.

## 8. Runtime Guard

Runtime/API/controller/entity/repository/database/service scaffold is
authorized only for the first metadata-only backend/API readiness slice, along
with the golden-compact readiness CRUD frontend and gateway route.

Authorized runtime boundary:

- Runtime owner/key: `tep.talent-data-foundation`.
- Permission namespace:
  - `tep.talent-data-foundation.read`
  - `tep.talent-data-foundation.manage`
  - `tep.talent-data-foundation.evaluate`
  - `tep.talent-data-foundation.audit.read`
- Runtime repo scope: `services/Diten.TalentEcosystemService/**`.
- Thin controller (`Api/Controllers/Tep/TalentDataFoundationController.cs`),
  CQRS/MediatR, `Response<T>` envelope.
- Server-side tenant context; `TenantId` must not be accepted in request DTOs.
- Mongo-backed tenant-aware repository; collection
  `tep_talent_data_foundation_readiness`.
- Active tenant `Code` uniqueness through
  `ux_tep_talent_data_foundation_tenant_code_active`, with
  `ix_tep_talent_data_foundation_tenant_state` supporting tenant/state queries.
- Soft delete through `IsDeleted` and `DeletedAt`.
- Fail-closed activation/evaluation.
- Non-activating evaluation may produce explicit `Deferred` metadata.
- Forbidden-marker guard rejecting forbidden field classes at the application
  boundary. This slice uses a WORD/TOKEN boundary matcher (`\b` regex) instead of
  a bare substring `Contains`, so legitimate domain words (`talent`, `catalog`,
  `lineage`, `ingestion`, `foundation`) are not falsely rejected while true
  forbidden markers (raw record body, resume/CV, PII, credential, payload) are
  still caught.

Explicitly unauthorized:

- Real talent data ingestion/ETL, streaming, or batch load execution.
- Talent entity catalog record persistence.
- Identity resolution execution, entity matching, or dedup/merge output.
- Data-quality scoring, profiling runs, or quality metric output.
- Lineage capture runtime, provenance graph storage, or transformation logs.
- Scoring/rating/ranking/model-output persistence.
- Automated decision behavior.
- Manager/employee talent-data UX beyond readiness-metadata CRUD.
- Notification and document integrations.
- Raw talent/candidate record, resume/CV, profile, or PII payload persistence.

## 9. Frontend / UX Boundary

Golden-compact readiness CRUD frontend and gateway route are authorized, limited
to the metadata-only readiness contract.

Authorized UX:

- TEP tenant shell nav entry `Talent Data Foundation`.
- Golden-compact readiness CRUD (list/get/create/evaluate/delete) bound to the
  metadata-only contract.
- List-DTO to JS column parity with no drift.
- `tr`/`en` localization parity through Resources.

Unauthorized UX:

- Manager talent-data UX beyond readiness-metadata CRUD.
- Employee talent-data/self-service UX beyond readiness-metadata CRUD.
- Talent entity catalog, ingestion, identity resolution, data-quality, lineage,
  scoring, or model-output UI.
- Document, notification, export, upload, or attachment UI.
- Any raw talent/candidate record, resume/CV, or profile viewer.

## 10. Backend / API Boundary

Only metadata-only backend/API implementation is authorized, exposed through the
gateway (`5080` -> TEP `5060`).

Authorized API surface:

- `GET /api/talent-data-foundation` - list readiness metadata.
- `GET /api/talent-data-foundation/{id}` - get readiness metadata by id.
- `POST /api/talent-data-foundation` - create readiness metadata.
- `POST /api/talent-data-foundation/{id}/evaluate` - fail-closed evaluation.
- `DELETE /api/talent-data-foundation/{id}` - soft-delete.
- `GET /api/talent-data-foundation/{id}/audit-metadata` - audit metadata read.

The first slice must stay limited to:

- Create/list/get talent data foundation readiness metadata.
- Fail-closed evaluation/deferred metadata.
- Audit metadata read endpoint.
- Soft delete.
- Tenant-aware persistence and active-code uniqueness.
- No talent data ingestion/ETL, talent entity catalog record persistence,
  identity resolution, data-quality scoring, lineage capture,
  scoring/model-output persistence, automated decision behavior,
  notification/document integration, or raw/PII payload persistence.

Request/response rules:

- Responses use the `Response<T>` envelope.
- Tenant is resolved server-side; `TenantId` is not present in any request DTO.
- Forbidden-marker guard runs on create/update at the application boundary using
  a WORD/TOKEN boundary (`\b`) matcher, not a bare substring.

## 11. Data Boundary

Allowed in the first runtime slice:

- Metadata-only talent data foundation readiness state.
- Boundary-state metadata for talent entity catalog, data ingestion, identity
  resolution, data quality, lineage tracking, automated decision, consent,
  minimization, retention, and evidence.
- Dependency-state metadata for HCM foundation, consent policy, document, and
  notification dependencies.
- Dependency/precondition metadata (`DependencyStates` dictionary).

Forbidden:

- Talent data ingestion/ETL payload.
- Talent entity catalog record body.
- Identity resolution match/merge payload.
- Data-quality profiling/scoring payload.
- Lineage/provenance payload or transformation logs.
- Readiness score/quality score/rating/rank/match confidence/model output.
- Automated decision result.
- Raw talent/candidate records, resume/CV content, or free-text profile
  narrative.
- Attachment payload.
- Raw provider payload.
- Credential/token/secret/password values.
- PII-heavy talent/profile details.
- Compensation/payroll/benefits payload.

## 12. Permission Boundary

Approved permission namespace:

- `tep.talent-data-foundation.read`
- `tep.talent-data-foundation.manage`
- `tep.talent-data-foundation.evaluate`
- `tep.talent-data-foundation.audit.read`

Candidate and blocked legacy module IDs must not be used as runtime literals.

## 13. Tenant / Security Boundary

Runtime work must be tenant-aware and fail closed. The backend/API slice must
resolve tenant server-side and must not expose `TenantId` in request DTOs.

Security guardrails:

- Candidate identity (`CAND-CAP-0041`) must not become a runtime literal.
- Blocked legacy ID (`MOD-0336`) must not become a runtime literal.
- Backend authorization is authoritative; RBAC is enforced server-side.
- Frontend permission checks remain UX-only.
- Tenant isolation applies to all persistence and query paths, backed by
  `ux_tep_talent_data_foundation_tenant_code_active` and
  `ix_tep_talent_data_foundation_tenant_state`.

## 14. Compliance / Privacy Boundary

Legal/privacy/consent is approved only as metadata-only waiver state.

Approved first-slice waiver decisions:

- Metadata-only consent/precondition representation.
- Talent data minimization fail-closed behavior.
- Retention/evidence/audit/legal-hold/deletion as local/deferred metadata.
- Exclusion of raw talent/candidate records, resume/CV content, profile
  payloads, and any PII dataset.

The Talent Data Foundation declares readiness STATE only; it does not own or
store any raw talent/candidate data, and all downstream talent modules must treat
its output as boundary/readiness metadata, not as a data source.

## 15. Integration Boundary

Deferred/out-of-scope modules:

- `MOD-0027` Notification.
- `MOD-0263` Notification Provider / Delivery.
- `MOD-0029` Controlled Documents.
- `MOD-0262` External Docs Repository.

No integration implementation, provider payload persistence, ingestion/ETL
connector, or document/notification UI is authorized. HCM foundation
(`CAND-CAP-0007` through `CAND-CAP-0010`) is consumed only as prerequisite
dependency-state context, and TEP `CAND-CAP-0011` through `CAND-CAP-0021` are
consumed only as sibling/context references.

## 16. Acceptance Criteria

Runtime-testable acceptance criteria:

1. AC-01: Pack status is `draft`.
2. AC-02: Candidate identity is `CAND-CAP-0041`.
3. AC-03: Legacy Excel ID `MOD-0336` remains blocked/unsafe until EA canonical
   MOD assignment.
4. AC-04: Runtime owner/key is `tep.talent-data-foundation`.
5. AC-05: Permission namespace is limited to `tep.talent-data-foundation.read`,
   `tep.talent-data-foundation.manage`, `tep.talent-data-foundation.evaluate`,
   and `tep.talent-data-foundation.audit.read`.
6. AC-06: Runtime repo scope is limited to
   `services/Diten.TalentEcosystemService/**`.
7. AC-07: The metadata contract supports create -> list -> get -> evaluate ->
   delete end-to-end (E3).
8. AC-08: `TenantId` is not accepted in request DTOs; tenant is resolved
   server-side.
9. AC-09: Mongo persistence (collection `tep_talent_data_foundation_readiness`)
   includes active tenant `Code` uniqueness
   (`ux_tep_talent_data_foundation_tenant_code_active`), tenant/state indexing
   (`ix_tep_talent_data_foundation_tenant_state`), soft delete, and tenant
   isolation (E4).
10. AC-10: Soft delete uses `IsDeleted` and `DeletedAt` and hides deleted
    records.
11. AC-11: Evaluation/activation remains fail-closed and can produce
    non-activating `Deferred` metadata.
12. AC-12: RBAC is enforced server-side across the controller surface.
13. AC-13: Real talent data ingestion/ETL, talent entity catalog record
    persistence, identity resolution execution, data-quality scoring, lineage
    capture, model output, and automated decision behavior remain unauthorized.
14. AC-14: Manager/employee talent-data fields remain reserved/out of scope; no
    money field is present; no raw talent/candidate/PII payload field is present.
15. AC-15: Raw talent/candidate records, resume/CV content, profile payloads,
    attachment payload, raw provider payload,
    credential/token/secret/password, and PII-heavy persistence remain
    unauthorized.
16. AC-16: Notification and document dependencies remain deferred/out of scope.
17. AC-17: TEP, HCM foundation, and PSS dependency boundaries are recorded
    (TEP `CAND-CAP-0011` through `CAND-CAP-0021` as sibling/context; HCM
    `CAND-CAP-0007` through `CAND-CAP-0010` as prerequisite context).
18. AC-18: `tr`/`en` localization parity is present.
19. AC-19: Golden-compact parity and list-DTO to JS column parity hold with no
    drift.
20. AC-20: Runtime literal scan for `CAND-CAP-0041|MOD-0336` passes under
    runtime/frontend/gateway/test paths.

## 17. Test Expectations

Runtime validation expectations:

- Confirm pack file exists.
- Confirm frontmatter `status: draft`.
- Confirm all 20 sections are present.
- Build TEP API:
  `dotnet build services/Diten.TalentEcosystemService/src/Diten.TalentEcosystemService.Api/Diten.TalentEcosystemService.Api.csproj -c Debug --no-restore /nr:false -m:1 --tl:off`
- Run targeted application tests with `TalentDataFoundation` filter
  (`Application.Tests/TalentDataFoundationTests.cs`, handler behavior; fix-absent
  should be RED).
- Run full TEP Application tests.
- Verify metadata contract create/list/get/evaluate/delete behavior (E3).
- Verify Mongo persistence, tenant isolation, active tenant `Code` uniqueness
  (`ux_tep_talent_data_foundation_tenant_code_active`), and tenant/state indexing
  (`ix_tep_talent_data_foundation_tenant_state`) (E4).
- Verify soft delete hides deleted records and sets `DeletedAt`.
- Verify permission-gated controller surface (RBAC server-side).
- Verify dependency/precondition fail-closed behavior.
- Verify non-activating evaluation deferred metadata behavior.
- Verify forbidden-marker guard rejects forbidden field classes.
- Verify the forbidden-marker matcher uses word/token boundaries (`\b` regex),
  not bare substring `Contains`, so legitimate words (`talent`, `catalog`,
  `lineage`, `ingestion`, `foundation`) are not falsely rejected.
- Verify forbidden ingestion/catalog/resolution/quality/lineage/scoring/
  decision/sensitive fields are absent.
- Verify manager/employee talent-data fields are reserved/absent, no money field
  exists, and no raw talent/candidate/PII payload field exists.
- Verify no production in-memory repository exists.
- Verify golden-compact parity and list-DTO to JS column parity (no drift).
- Verify `tr`/`en` localization parity.
- Verify runtime literal scan:
  `rg -n "CAND-CAP-0041|MOD-0336" services frontend gateway`
  returns no runtime literal (candidate IDs governance-only).

## 18. Governance Approval Checklist

- [x] Pack status is reviewed for governance approval as a draft slice.
- [x] Candidate reservation is accepted as the governing identity.
- [x] Legacy ID block is accepted.
- [x] Candidate gate script not-executable note is accepted.
- [x] Metadata-only backend/API + golden-compact readiness CRUD + gateway scope
  is accepted.
- [x] Real talent data ingestion/ETL prohibition is accepted.
- [x] Talent entity catalog record persistence prohibition is accepted.
- [x] Identity resolution / data-quality / lineage execution prohibition is
  accepted.
- [x] Scoring/model-output prohibition is accepted.
- [x] Automated decision behavior prohibition is accepted.
- [x] Reserved manager/employee talent-data fields, no-money-field, and no-raw/
  PII-payload constraints are accepted.
- [x] Sensitive/PII payload prohibition is accepted.
- [x] Notification / document dependency deferral is accepted.
- [x] TEP/HCM/PSS dependency context is accepted.

## 19. Runtime-Ready Checklist

- [x] EA candidate-runtime waiver or canonical MOD assignment.
- [x] Runtime owner/key decision.
- [x] Permission namespace decision.
- [x] Runtime repo scope decision.
- [x] Minimal metadata-only talent data foundation readiness contract.
- [x] Tenant isolation decision.
- [x] Fail-closed activation/evaluation decision.
- [x] Legal/privacy/consent metadata-only waiver.
- [x] Talent data minimization policy.
- [x] Retention/evidence/audit/legal-hold/deletion boundary.
- [x] Golden-compact readiness CRUD frontend + gateway scope decision.

Runtime-ready blockers:

- Draft implementation in progress; done-promotion audit not yet executed.

Review-ready reconciliation note:

- Metadata-only backend/API slice plus golden-compact readiness CRUD frontend
  and gateway route are scoped under `services/Diten.TalentEcosystemService/**`
  and the owned `frontend/Diten.Web/**` TalentDataFoundation objects.
- Runtime owner/key remains limited to `tep.talent-data-foundation`.
- Permission namespace remains limited to `tep.talent-data-foundation.read`,
  `tep.talent-data-foundation.manage`, `tep.talent-data-foundation.evaluate`,
  and `tep.talent-data-foundation.audit.read`.
- Section 4 metadata fields are aligned with the intended runtime
  public/persisted contract.
- `HcmFoundationDependencyState`, `ConsentPolicyDependencyState`,
  `DocumentDependencyState`, and `NotificationDependencyState` remain
  metadata-only dependency states.
- Manager/employee talent-data UX fields remain reserved/out of scope, no money
  field is present, and no raw talent/candidate/PII payload field is present.
- HCM, PSS, and Platform scopes remain closed.
- Real talent data ingestion/ETL, talent entity catalog record persistence,
  identity resolution execution, data-quality scoring, lineage capture, model
  output, automated decision behavior, notification/document integration, raw
  talent/candidate records, resume/CV content, profile payloads, attachment
  payload, raw provider payload, credential/token/secret/password, and
  PII-heavy persistence remain out of scope.

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
  `python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0041 --name "Talent Data Foundation"`
  exits non-zero (exit 2, file not found).
- EA/registry owner accepted candidate-based governance continuation for
  `CAND-CAP-0041`.
- EA/registry owner accepted verifier absence as a governance note.
- EA candidate-runtime policy waiver approved for the first metadata-only
  backend/API readiness slice (K19-complete), with golden-compact readiness
  CRUD frontend and gateway route in scope because the Blueprint has no
  canonical MOD for this capability yet.
- Legal/privacy/consent metadata-only waiver approved.
- Talent data minimization fail-closed policy approved.
- Retention/evidence/audit/legal-hold/deletion local/deferred metadata policy
  approved.

Runtime literal scan:

- Required command:
  `rg -n "CAND-CAP-0041|MOD-0336" services frontend gateway`
- Candidate IDs are governance-only and must return no runtime literal match.

Next safe step:

- Complete the draft metadata-only backend/API + golden-compact readiness CRUD
  slice and run the done-promotion readiness audit.

Future follow-ups:

- Real talent data ingestion/ETL.
- Talent entity catalog record management.
- Identity resolution execution.
- Data-quality scoring and profiling.
- Lineage / provenance tracking.
- Automated decision approval.
- Manager/employee talent-data UX beyond readiness CRUD.
- Notification/document integrations.
- Export governance.
- Real audit/evidence/retention integration.
