---
id: MOD-0279
name: Payroll Engine (ADP/Workday Payroll/SAP Payroll) [External SoR]
friendly_name: Payroll Engine
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: BaseEntity
status: ready-for-dev
owner: enterprise-architect / platform-team / integration-ops
branch: feature/pss/mod-0279-payroll-engine-external-sor
started: 2026-07-10
target: 2026-07-31
form_field_count: 0
canonical_source: "docs/System Capability & Implementation Blueprint - master 5.xlsx#Blueprint_Data row 280"
---

# MOD-0279 - Payroll Engine (ADP/Workday Payroll/SAP Payroll) [External SoR]

> **Execution status:** This module pack is ready-for-dev for the first
> provider-neutral backend/API contract slice in `Diten.Platform` under
> `platform-shared-services`.
>
> **Authorized production scope:** Only MOD-0279 payroll external SoR contract
> metadata, tenant-scoped API behavior, validation, persistence, authorization, and
> tests described in this pack may be implemented.
>
> **Canonical-name guard:** The canonical module-pack identity is exactly
> `Payroll Engine (ADP/Workday Payroll/SAP Payroll) [External SoR]`. The shorter
> label `Payroll Engine` is a friendly/display description only.
>
> **Explicit exclusions:** No UI, Razor, JavaScript, DataTable, provider-specific
> ADP/Workday Payroll/SAP Payroll adapter, native payroll calculation engine, raw
> payroll payload persistence, payslip persistence, bank/tax/secret data
> persistence, payroll/time-attendance ownership, or direct Gateway `ocelot.json`
> edit is authorized by this pack.
>
> **Blueprint evidence:** `Blueprint_Data` row 280 classifies MOD-0279 as an
> External SoR in Wave W-3, with deployment unit `External (ADP/Workday/SAP)` and
> build model `Partner`.

## 1. Module Summary

- **Purpose:** Govern a provider-neutral external payroll system-of-record
  boundary so ERP/HCM modules can reference payroll system profiles, contract
  versions, pay cycles, payroll result references, and source health without
  owning statutory payroll calculation or vendor runtime behavior.
- **Capability/domain evidence:** Repository Blueprint `Blueprint_Data` places
  MOD-0279 under `7) External Systems & Providers`, suite/platform
  `External Systems Register`, capability group `External SoR`.
- **Primary outcome:** Standardized, tenant-scoped, auditable payroll external SoR
  API surfaces are available to downstream modules without persisting sensitive
  payroll payloads.
- **Delivery wave:** W-3.
- **Implementation classification:** External Provider Onboarding / Partner.
- **MVP UI decision:** None in this slice. No Platform Admin UI, no tenant UI, no
  DataTable, no Razor view, no JavaScript, no RESX.
- **Golden Reference decision:** `golden_reference: none` because the first slice
  is backend/API contract only, not a CRUD/DataTable UI module.
- **Ownership status:** User decision confirms `platform-shared-services` and
  `Diten.Platform` as the governance/runtime owner for this first slice. Future
  HCM/payroll domain relocation is a governance follow-up, not a blocker for this
  approved provider-neutral slice.

## 2. Ownership and Boundaries

### In-scope

- Provider-neutral payroll external SoR metadata for:
  - payroll external system profile and display identity;
  - approved provider family code without provider-specific adapter behavior;
  - contract version, lifecycle state, and effective-dated metadata;
  - employee/person identifier reference mapping metadata;
  - pay cycle reference metadata;
  - payroll result reference metadata;
  - source health, readiness, and correlation metadata;
  - access and audit contract behavior.
- Tenant-scoped backend/API implementation in `Diten.Platform`.
- Validation that rejects raw credentials, raw tokens, secrets, raw payroll
  payloads, payslip content, bank details, tax identifiers, and payroll/time
  attendance objects.
- MOD-0288 reference validation for any Person, OrganizationUnit, or Position
  reference accepted by this module.
- Gateway route need may be reported as an integration-agent task only.

### Out-of-scope

- Native payroll calculation engine.
- Jurisdiction-specific payroll legislation, tax brackets, contribution rates,
  overtime rules, minimum wage rules, garnishment priorities, filing formats, or
  legal payslip field definitions.
- Payment execution, bank transfer files, GL posting, accounting mappings, and
  compensation planning.
- Raw payroll payload, payslip document/content, bank account, tax id, credential,
  token, secret, or provider response persistence.
- Employee legal master data, person directory, organization directory, position
  routing, or employment master ownership.
- Time, attendance, leave, roster, and payroll governance/approval workflows.
- Provider-specific ADP, Workday Payroll, or SAP Payroll adapter code.
- Frontend pages, DataTables, localization resources, menu/navigation entries, or
  browser smoke tests.
- Direct edits to `gateway/Diten.ApiGateway/**/ocelot.json`.
- Edits to MOD-0251, MOD-0280, or MOD-0281 pack/implementation files.

### Ownership rule

- **MOD-0279** owns only the external payroll SoR boundary, provider-neutral
  metadata, standardized payroll reference contracts, and first-slice API surface.
- **MOD-0288** owns Organization, Person & Position Directory and must not be
  duplicated here. MOD-0279 may store only same-tenant references to MOD-0288
  records after fail-closed validation.
- **MOD-0280** owns time, attendance, leave, roster, and payroll source readiness
  for those objects if/when approved in the repository.
- **MOD-0281** owns payroll integration governance, approval controls,
  reconciliation controls, and audit-oriented payroll control workflows if/when
  approved in the repository.
- **PSS modules** own RBAC, audit, gateway, interface/data contract registry,
  scheduler, event bus, secrets, and integration monitoring infrastructure.

## 3. Owned Objects

### Domain entities

- `PayrollExternalSystemProfile`
- `PayrollContractProfile`
- `PayrollEmployeeReferenceMap`
- `PayrollCycleReference`
- `PayrollResultReference`
- `PayrollSourceHealthSnapshot`

### Repositories

- `IPayrollExternalSystemProfileRepository`
- `IPayrollContractProfileRepository`
- `IPayrollEmployeeReferenceMapRepository`
- `IPayrollCycleReferenceRepository`
- `IPayrollResultReferenceRepository`
- `IPayrollSourceHealthSnapshotRepository`

### Commands

- `CreatePayrollExternalSystemProfileCommand`
- `UpdatePayrollExternalSystemProfileCommand`
- `ArchivePayrollExternalSystemProfileCommand`
- `UpdatePayrollContractProfileCommand`
- `CreatePayrollEmployeeReferenceMapCommand`
- `UpdatePayrollEmployeeReferenceMapCommand`
- `RecordPayrollCycleReferenceCommand`
- `RecordPayrollResultReferenceCommand`
- `RecordPayrollSourceHealthSnapshotCommand`

### Queries

- `GetPayrollExternalSystemProfileListQuery`
- `GetPayrollExternalSystemProfileByIdQuery`
- `GetPayrollContractProfileQuery`
- `GetPayrollEmployeeReferenceMapsQuery`
- `GetPayrollCycleReferencesQuery`
- `GetPayrollResultReferencesQuery`
- `GetPayrollSourceHealthQuery`

### API endpoints

- `GET /api/payroll-sources`
- `GET /api/payroll-sources/{id}`
- `POST /api/payroll-sources`
- `PUT /api/payroll-sources/{id}`
- `PATCH /api/payroll-sources/{id}/archive`
- `PUT /api/payroll-sources/{id}/contract-profile`
- `GET /api/payroll-sources/{id}/contract-profile`
- `POST /api/payroll-sources/{id}/employee-reference-maps`
- `PUT /api/payroll-sources/{id}/employee-reference-maps/{mapId}`
- `GET /api/payroll-sources/{id}/employee-reference-maps`
- `POST /api/payroll-sources/{id}/cycle-references`
- `GET /api/payroll-sources/{id}/cycle-references`
- `POST /api/payroll-sources/{id}/result-references`
- `GET /api/payroll-sources/{id}/result-references`
- `POST /api/payroll-sources/{id}/health-snapshots`
- `GET /api/payroll-sources/{id}/health`

### Not owned

- Payroll run calculation entities.
- Payslip storage entities.
- Earnings/deductions configuration entities.
- Employee, employment, person, position, or organization entities.
- Payroll approval/reconciliation workflow entities.
- Audit trail entities.
- Gateway route definitions.
- Provider credentials or secret storage internals.
- Provider adapter clients, SDK wrappers, webhooks, or background sync jobs.

## 4. Entity Fields

All tenant-owned records must inherit the service-standard base entity behavior,
include server-resolved `TenantId`, support `IsDeleted` and `DeletedAt`, and use
MongoDB indexes scoped by `TenantId` and `IsDeleted`.

### `PayrollExternalSystemProfile`

| Field | Type | Required | Notes |
|---|---|---|---|
| TenantId | Guid | Yes | Server-resolved only; never accepted from client payloads. |
| Code | string | Yes | Tenant-unique where `IsDeleted = false`; trim, uppercase, max 64. |
| DisplayName | string | Yes | Max 200. |
| ProviderFamily | enum/string | Yes | `ADP`, `WorkdayPayroll`, `SAPPayroll`, `OtherExternalPayroll`; no adapter behavior implied. |
| ExternalPayrollSystemId | string | Yes | External reference only, max 128; never a secret. |
| LifecycleState | enum/string | Yes | `Draft`, `Active`, `Suspended`, `Deprecated`, `Archived`. |
| ConnectionProfileReference | string? | No | Reference to approved secrets/integration config only; no raw secret/token value. |
| SupportOwner | string? | No | Team or contact label, max 200; no personal sensitive data required. |
| Notes | string? | No | Max 1000; validator rejects raw payload, credential, bank, tax, or payslip-like content. |

### `PayrollContractProfile`

| Field | Type | Required | Notes |
|---|---|---|---|
| TenantId | Guid | Yes | Server-resolved only. |
| PayrollExternalSystemProfileId | Guid | Yes | Same-tenant profile reference. |
| ContractVersion | string | Yes | Required, immutable once referenced; max 64. |
| EffectiveFrom | DateTimeOffset | Yes | Start of contract applicability. |
| EffectiveTo | DateTimeOffset? | No | Must be greater than `EffectiveFrom` when present. |
| SupportedObjectTypes | string[] | Yes | First slice permits `Employee`, `Org`, `Job`, `PayrollCycle`, `PayrollResultReference` only. |
| StatusVocabulary | string[] | Yes | Provider-neutral status values only. |
| ErrorVocabulary | string[] | No | Provider-neutral; no provider payload examples. |
| CorrelationIdPattern | string? | No | Max 128. |

### `PayrollEmployeeReferenceMap`

| Field | Type | Required | Notes |
|---|---|---|---|
| TenantId | Guid | Yes | Server-resolved only. |
| PayrollExternalSystemProfileId | Guid | Yes | Same-tenant profile reference. |
| ExternalEmployeeReference | string | Yes | External reference only, max 128; no raw PII or payroll payload. |
| PersonReferenceId | Guid? | No | Same-tenant MOD-0288 Person reference; fail closed if missing/cross-tenant. |
| OrganizationUnitReferenceId | Guid? | No | Same-tenant MOD-0288 OrganizationUnit reference; fail closed if missing/cross-tenant. |
| PositionReferenceId | Guid? | No | Same-tenant MOD-0288 Position reference; fail closed if missing/cross-tenant. |
| MappingState | enum/string | Yes | `Unmapped`, `Mapped`, `Suspended`, `Archived`. |
| LastValidatedAt | DateTimeOffset? | No | Reference validation timestamp. |

### `PayrollCycleReference`

| Field | Type | Required | Notes |
|---|---|---|---|
| TenantId | Guid | Yes | Server-resolved only. |
| PayrollExternalSystemProfileId | Guid | Yes | Same-tenant profile reference. |
| ExternalPayCycleId | string | Yes | Reference only, max 128. |
| CycleCode | string | Yes | Max 64; not a payroll calculation rule. |
| PeriodStart | DateOnly | Yes | Metadata only. |
| PeriodEnd | DateOnly | Yes | Must be on or after `PeriodStart`. |
| ProcessingState | enum/string | Yes | Provider-neutral lifecycle state. |
| CorrelationId | string? | No | Max 128. |

### `PayrollResultReference`

| Field | Type | Required | Notes |
|---|---|---|---|
| TenantId | Guid | Yes | Server-resolved only. |
| PayrollExternalSystemProfileId | Guid | Yes | Same-tenant profile reference. |
| PayrollCycleReferenceId | Guid | Yes | Same-tenant cycle reference. |
| ExternalPayrollResultId | string | Yes | Reference only, max 128. |
| ResultVersion | string | Yes | Max 64. |
| ResultState | enum/string | Yes | Provider-neutral status. |
| PublishedAt | DateTimeOffset? | No | External SoR publication timestamp. |
| CorrelationId | string? | No | Max 128. |

### `PayrollSourceHealthSnapshot`

| Field | Type | Required | Notes |
|---|---|---|---|
| TenantId | Guid | Yes | Server-resolved only. |
| PayrollExternalSystemProfileId | Guid | Yes | Same-tenant profile reference. |
| HealthState | enum/string | Yes | `Unknown`, `Ready`, `Degraded`, `Unavailable`, `Blocked`. |
| CheckedAt | DateTimeOffset | Yes | Snapshot timestamp. |
| RedactedMessage | string? | No | Max 1000; no secrets, tokens, raw payload, tax/bank/payslip content. |
| CorrelationId | string? | No | Max 128. |

## 5. Repo Scope

Approved first backend/API slice scope:

- `execution/domains/platform-shared-services/module-packs/MOD-0279-payroll-engine-external-sor.md`
- `services/Diten.Platform/src/Diten.Platform.Domain/**` for MOD-0279 domain
  entities, enums, and repository interfaces.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/PayrollSources/**`
  for commands, queries, handlers, validators, models, and contracts.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/**` for
  MOD-0279 Mongo repositories, collection names, and indexes.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/**` only for MOD-0279
  dependency injection registration needed by repositories.
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/**` for the thin
  `PayrollSourcesController`.
- `services/Diten.Platform/src/Diten.Platform.API/**` only for MOD-0279 API DI or
  routing glue required by the controller.
- `services/Diten.Platform/tests/**` for MOD-0279 application/API/persistence tests.

Explicitly outside this first slice:

- `services/Diten.Platform.Contracts/**` unless a later approved amendment makes a
  shared contracts project necessary.
- `frontend/Diten.Web/**`
- `gateway/Diten.ApiGateway/**/ocelot.json` direct edits.
- `services/Diten.AuthService/**`
- `services/Diten.DevEnablementService/**`
- `services/Diten.EnterpriseStrategyService/**`
- `services/Diten.MdmService/**`
- MOD-0251, MOD-0280, and MOD-0281 pack/implementation files.

Gateway route requirement, if discovered during implementation, must be captured as
an integration-agent task/blocker note only.

## 6. Protected Paths

- `.antigravity/**`
- `frontend/Diten.Web/**`
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`
- `gateway/Diten.ApiGateway/**/ocelot.json`
- `services/Diten.MdmService/**`
- `services/Diten.EnterpriseStrategyService/**`
- `services/Diten.DevEnablementService/**`
- `services/Diten.AuthService/**`
- `services/Diten.Platform/src/Diten.Platform.Domain/Entities/Organization/**`
- `services/Diten.Platform/src/Diten.Platform.Domain/Entities/Positions/**`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Organization/**`
- `execution/domains/platform-shared-services/module-packs/MOD-0251-hris-external-sor.md`
- `execution/domains/platform-shared-services/module-packs/MOD-0280-time-attendance-external-sor.md`
- `execution/domains/platform-shared-services/module-packs/MOD-0281-payroll-integration-governance.md`
- Any MOD-0288, MOD-0280, MOD-0281, MOD-0021, MOD-0018, MOD-0032, or MOD-0037
  owned implementation files unless a future approved pack explicitly grants a
  contract-only touch point.

## 7. Dependencies

### Hard Blueprint dependencies

| Dependency | Current repo evidence | MOD-0279 usage |
|---|---|---|
| MOD-0017 - SSO / MFA | Only follow-up `MOD-0017-FU01` is visible in registry/domain evidence; parent readiness must be confirmed during implementation. | Authentication context for external payroll access. |
| MOD-0003 - Data Contract Registry | Registry status `planned / missing`; no module pack found in current evidence. | Required before broad contract publication; first slice may store local contract metadata only. |
| MOD-0032 - API Gateway | Registry `review / partial`; module pack exists. | Required if external Gateway routes are needed; integration-agent owns route edits. |
| MOD-0037 - Integration Monitoring & Reconciliation | Registry `review / planned`; module pack exists. | Required for production external SoR monitoring beyond local health snapshots. |

### Additional readiness checks

- MOD-0018 RBAC / ABAC Authorization.
- MOD-0021 Audit Trail Service.
- MOD-0028 Documentation & Evidence Management.
- MOD-0031 Evidence Linking Service.
- MOD-0035 Event Bus / Message Queue.
- MOD-0288 Organization, Person & Position Directory.
- MOD-0280 Time, Attendance and Leave, if present/approved later.
- MOD-0281 Payroll Integration & Governance, if present/approved later.

Missing dependencies must not be implemented by MOD-0279.

## 8. Runtime Constraints

- Runtime implementation is limited to the provider-neutral backend/API contract
  slice in `Diten.Platform`.
- Tenant isolation is mandatory for all MOD-0279 tenant-scoped metadata.
- Cross-tenant access must return `404` fail-closed.
- Client payloads must never supply `TenantId`.
- Soft delete requires `IsDeleted` and `DeletedAt` on soft-deleted MOD-0279 records.
- Duplicate active `Code` within tenant scope must return `409`.
- Controllers must stay thin and delegate to MediatR.
- API responses must use repository-standard `Response<T>` envelope and
  `CustomBaseController`.
- Validators must reject raw payroll payloads, payslip content, bank details, tax
  identifiers, credentials, raw tokens, raw secrets, and secret-like fields.
- No monetary amount, earnings, deduction, tax, net pay, gross pay, bank account,
  or payslip body field is approved in the first slice.
- No external provider calls may be made; provider-specific adapters are out of
  scope.
- No statutory correctness may be claimed; payroll calculation is out of scope.
- Effective-dated contract metadata must define valid-from, optional valid-to,
  version, overlap behavior, and historical immutability after use.
- Any Person, OrganizationUnit, or Position reference accepted by MOD-0279 must be
  validated against MOD-0288 within the same tenant before the record is accepted
  as mapped/current.

## 9. Layout & Shell Contract

- `shell: none`.
- No Razor layout.
- No `_LayoutPlatformAdmin`.
- No `_LayoutTenantShell`.
- No frontend view folder.
- No menu/navigation entry.
- No DataTable verifier.
- No localization/RESX requirement for this first slice.

If a future UI is approved, this pack must be amended to set the shell, area, page
inventory, form field count, Golden Reference decision, localization scope, and
DataTables verifier expectation before any frontend work begins.

## 10. Backend File Convention

Approved first-slice backend convention:

- Application feature folder:
  `services/Diten.Platform/src/Diten.Platform.Application/Features/PayrollSources/`
- One public command/query/handler/validator per file where CQRS is used.
- Sealed records for commands/queries.
- Sealed classes for handlers.
- `Handlers/CommandHandlers` and `Handlers/QueryHandlers` separation where the
  repo pattern applies.
- Request/response DTOs may be grouped in `PayrollSourcesModels.cs` if consistent
  with nearby Diten.Platform feature patterns.
- No business logic in controllers.
- No Domain or Application dependency on Infrastructure.
- Repositories and Mongo indexes live in Diten.Platform infrastructure/persistence
  scope.
- `Response<T>` envelope and `CustomBaseController` are mandatory for API
  endpoints.
- Provider HTTP clients, SDK wrappers, webhook handlers, background sync jobs, and
  adapter-specific mapping code are not authorized.

## 11. Frontend File Contract

No frontend files are approved in this first slice.

Forbidden in this pack:

- `Index.cshtml`, create/edit/details pages, or offcanvas forms;
- DataTables markup;
- Razor layouts or partials;
- JavaScript files;
- RESX/localization files;
- direct calls from Diten.Web to service ports;
- inline `window.L10n` assignment blocks;
- navigation/menu changes.

## 12. Validation Rules

- Canonical ID must remain `MOD-0279`.
- Canonical name must remain
  `Payroll Engine (ADP/Workday Payroll/SAP Payroll) [External SoR]`.
- `TenantId` is server-resolved; request DTOs must not expose it.
- `Code` is required, trimmed, uppercased, max 64, and unique for active records
  within the tenant.
- `DisplayName` is required and max 200.
- `ProviderFamily` must be one of the approved provider-neutral values; it must not
  invoke provider-specific execution behavior.
- `ExternalPayrollSystemId` is required, max 128, and must not contain a secret,
  token, credential, raw URL credential, bank/tax id, or raw payload fragment.
- `ContractVersion` is required, max 64, and immutable once used by downstream
  references.
- `EffectiveTo`, when present, must be greater than `EffectiveFrom`.
- `SupportedObjectTypes` rejects payroll/time-attendance ownership objects and
  permits only `Employee`, `Org`, `Job`, `PayrollCycle`, and
  `PayrollResultReference` in this slice.
- `ExternalEmployeeReference`, `ExternalPayCycleId`, and
  `ExternalPayrollResultId` are references only and must not contain raw PII,
  payslip content, payroll amounts, bank/tax values, secrets, or raw provider
  payloads.
- `PersonReferenceId`, `OrganizationUnitReferenceId`, and `PositionReferenceId`
  must fail closed if missing in the same tenant or cross-tenant.
- `PayrollResultReference` must not accept monetary amount, payslip body, earnings,
  deductions, bank, or tax fields.
- `RedactedMessage`, `Notes`, and failure payloads must be scanned/rejected for
  raw credential/token/secret and raw payroll payload markers.
- Missing dependency contracts fail closed.

## 13. Failure Path to Verify

Implementation must verify at least:

1. canonical name mismatch fails module identity gate;
2. registry collision blocks pack approval;
3. duplicate active `Code` in the same tenant returns `409`;
4. same `Code` in another tenant does not collide;
5. cross-tenant payroll source lookup returns `404`;
6. update/archive of a missing or cross-tenant payroll source returns fail-closed
   `404`;
7. unauthorized access is denied before handler execution;
8. missing permission returns `403`;
9. raw payroll payload-like content is rejected with `400`;
10. raw credential/token/secret-like content is rejected with `400`;
11. bank/tax/payslip-like fields or content are rejected with `400`;
12. provider-specific adapter selection or execution fields are rejected with
    `400`;
13. payroll/time-attendance object types are rejected with `400`;
14. invalid or cross-tenant MOD-0288 Person, OrganizationUnit, or Position
    reference fails closed;
15. unresolved MOD-0288 references are not accepted as `Mapped`;
16. stale contract version update returns `409`;
17. soft delete sets both `IsDeleted` and `DeletedAt`;
18. archived records are excluded from active list and duplicate checks;
19. no external provider network call is made by MOD-0279 handlers;
20. Gateway route requirement is reported as integration-agent task only, with no
    direct `ocelot.json` edit.

## 14. Authorization Convention

- Actor policy: `[Authorize]` plus repository-standard permission attribute.
- Permission prefix: `platform.payroll-sources.*`.
- Approved first-slice permissions:
  - `platform.payroll-sources.read`
  - `platform.payroll-sources.create`
  - `platform.payroll-sources.update`
  - `platform.payroll-sources.archive`
  - `platform.payroll-sources.update-contract`
  - `platform.payroll-sources.map-employee-reference`
  - `platform.payroll-sources.record-cycle`
  - `platform.payroll-sources.record-result`
  - `platform.payroll-sources.record-health`
  - `platform.payroll-sources.read-health`

## 15. Gateway / API Routing Decision

- API resource path for service-local implementation: `/api/payroll-sources`.
- Direct `gateway/Diten.ApiGateway/**/ocelot.json` edits are not permitted by this
  pack.
- If Gateway exposure is required, create/report an integration-agent task to add
  Ocelot routes for the approved endpoints.
- Frontend is N/A for this slice. If a future frontend exists, it must call Gateway
  port `5000` only.
- Any route must preserve JWT, tenant context, correlation id, and response
  envelope conventions.

## 16. Acceptance Criteria

The first provider-neutral backend/API slice is accepted only when all are true:

1. `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0279 --name "Payroll Engine (ADP/Workday Payroll/SAP Payroll) [External SoR]"` passes.
2. Frontmatter and body both show `status: ready-for-dev` intent and authorize only
   the provider-neutral backend/API contract slice.
3. Implementation touches only the repo scope listed in Section 5.
4. No UI, Razor, JavaScript, DataTable, RESX, menu, or frontend file is created or
   edited.
5. No direct Gateway `ocelot.json` edit is made; route need is reported as an
   integration-agent task if required.
6. No provider-specific ADP, Workday Payroll, or SAP Payroll adapter/client/webhook
   code is implemented.
7. No native payroll calculation, statutory payroll rule, payslip persistence,
   payroll amount, bank/tax, credential, token, secret, or raw payroll payload field
   is persisted.
8. `PayrollSourcesController` is thin, uses `CustomBaseController`, and delegates
   to MediatR.
9. All MOD-0279 API responses use `Response<T>` envelope.
10. TenantId is resolved server-side and never accepted from client payloads.
11. Cross-tenant lookup/update/archive/map operations return `404` fail-closed.
12. Duplicate active `Code` within a tenant returns `409`.
13. Soft delete sets `IsDeleted` and `DeletedAt` and excludes archived records from
    active list results.
14. Validators reject raw credential/token/secret and raw payroll/payslip/bank/tax
    payload markers.
15. Validators reject payroll/time-attendance ownership object types outside this
    pack.
16. MOD-0288 Person, OrganizationUnit, and Position references are same-tenant
    validated before a mapping can become `Mapped`.
17. Permission prefix `platform.payroll-sources.*` is enforced on endpoints.
18. Mongo indexes enforce tenant-aware active-code uniqueness and support list
    queries.
19. Diten.Platform build passes:
    `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug`.
20. MOD-0279 tests cover tenant isolation, duplicate code, validation,
    authorization, unauthorized/missing permission, soft delete, no raw secrets,
    no raw payloads, and MOD-0288 reference fail-closed behavior.

## 17. Test Expectations

Required implementation validation:

- Module identity preflight command in Section 16.
- Diten.Platform API build command in Section 16.
- Application handler tests for create, update, archive, contract profile, employee
  reference map, cycle reference, result reference, and health snapshot flows.
- Tenant isolation tests for list, get, update, archive, and mapping endpoints.
- Duplicate active `Code` returns `409`; archived code reuse behavior is explicitly
  tested according to implementation choice.
- Authorization tests for unauthenticated and missing-permission access.
- Validation tests for raw secret/token/credential, raw payroll payload,
  payslip-like content, bank/tax-like content, provider-specific adapter fields,
  and payroll/time-attendance object types.
- MOD-0288 same-tenant Person, OrganizationUnit, and Position reference
  fail-closed tests.
- Soft-delete tests proving `IsDeleted` and `DeletedAt`.
- Repository/index tests if existing Diten.Platform test patterns support them.
- UI/DataTable/l10n verifier: N/A for this first slice.

## 18. Ready-for-dev Checklist

- [x] Canonical Blueprint name verified.
- [x] DCP-002 module identity preflight passes for MOD-0279.
- [x] Friendly name separated from canonical identity.
- [x] Pack status set to `ready-for-dev`.
- [x] User confirmed MOD-0279 is the next module to develop.
- [x] User confirmed `platform-shared-services` as first-slice domain owner.
- [x] User confirmed `Diten.Platform` as first-slice runtime service.
- [x] User confirmed `shell: none`.
- [x] User confirmed `golden_reference: none`.
- [x] Provider-neutral backend/API contract scope approved.
- [x] Native payroll calculation excluded.
- [x] Provider-specific ADP/Workday/SAP adapters excluded.
- [x] Raw payroll payload, payslip, bank, tax, credential, token, and secret
  persistence excluded.
- [x] MOD-0251, MOD-0280, and MOD-0281 file edits excluded from this pack.
- [x] MOD-0288 boundary preserved through same-tenant reference validation.
- [x] Permission prefix approved: `platform.payroll-sources.*`.
- [x] Gateway route edits restricted to integration-agent task/reporting.
- [x] Acceptance Criteria are runtime-testable.
- [x] Test expectations include tenant isolation, duplicate code, validation,
  authorization, soft delete, and no-raw-data checks.
- [x] Open blockers: none.

## 19. Implementation Notes

- The user-facing short description `Payroll Engine` is not the canonical module
  name.
- The label `Native Payroll Engine` conflicts with the current repository
  Blueprint for MOD-0279 and must not be used in identity fields.
- Blueprint row 280 describes an external payroll SoR, not a native internal
  payroll calculation engine.
- The first slice intentionally stores reference metadata only. It does not store
  payroll calculations, payroll values, payslip bodies, provider payloads, bank
  details, tax identifiers, credentials, tokens, or secrets.
- `Diten.Platform` and `platform-shared-services` are accepted for this first slice
  by user decision. A future HR/payroll domain may assume long-term ownership only
  through a separate governance change.
- `services/Diten.Platform.Contracts/**` is not required for the first slice unless
  a later approved amendment changes the API contract distribution model.
- Gateway route creation, if required, remains an integration-agent responsibility.

## 20. Follow-up Items

- [ ] If Gateway exposure is required, create an integration-agent task for
  `/api/payroll-sources` routes.
- [ ] Define provider-specific ADP, Workday Payroll, or SAP Payroll adapters in
  separate approved module packs if/when needed.
- [ ] Resolve whether a native payroll calculation capability exists under another
  Blueprint ID, FU, or future EA allocation before any native payroll engine work.
- [ ] Create or identify authoritative packs for MOD-0280 and MOD-0281 before
  consuming their responsibilities.
- [ ] Define broad payroll contract publication through MOD-0003 or an approved
  interim mechanism if downstream services require shared contracts.
- [ ] Define integration monitoring handoff through MOD-0037 for production-grade
  external SoR monitoring.
