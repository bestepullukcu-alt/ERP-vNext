---
id: MOD-0280
name: Time & Attendance (UKG/Kronos) [External Provider]
friendly_name: Time & Attendance External Provider
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: BaseEntity
status: ready-for-dev
owner: enterprise-architect / platform-team / integration-ops
branch: feature/pss/mod-0280-time-attendance-ukg-kronos-external-provider
started: 2026-07-13
target: 2026-07-31
form_field_count: 0
canonical_source: "docs/System Capability & Implementation Blueprint - master 5.xlsx#Blueprint_Data row 281"
---

# MOD-0280 - Time & Attendance (UKG/Kronos) [External Provider]

> **Execution status:** This module pack is ready-for-dev for the first
> provider-neutral backend/API contract slice in `Diten.Platform` under
> `platform-shared-services`.
>
> **Authorized production scope:** Only MOD-0280 external time-and-attendance
> provider contract metadata, tenant-scoped API behavior, validation, persistence,
> authorization, source health, and tests described in this pack may be implemented.
>
> **Canonical-name guard:** The canonical identity is exactly
> `Time & Attendance (UKG/Kronos) [External Provider]`. Shortened labels such as
> `Time & Attendance` are friendly/display text only and must not replace the
> canonical name in identity or governance records.
>
> **Explicit exclusions:** No UI, Razor, JavaScript, DataTable, RESX,
> provider-specific UKG/Kronos adapter, internal time-entry engine, leave/accrual
> engine, roster optimization, payroll calculation, raw time/attendance payload,
> credential, biometric, geolocation, payroll data persistence, or direct Gateway
> `ocelot.json` edit is authorized by this pack.
>
> **Blueprint evidence:** `Blueprint_Data` row 281 classifies MOD-0280 under
> `7) External Systems & Providers`, suite `External Systems Register`, capability
> group `External Provider`, placement `External Provider`, delivery wave `W-3`,
> deployment unit `External (UKG/Kronos)`, and build model `Partner`.

## 1. Module Summary

- **Purpose:** Govern a provider-neutral external time-and-attendance provider
  boundary so ERP/HCM/payroll modules can reference approved source profiles,
  contract versions, employee mappings, time event references, attendance summary
  references, sync checkpoints, and source health without the ERP platform becoming
  an internal attendance/leave/payroll engine.
- **Primary outcome:** Standardized, tenant-scoped, auditable time-and-attendance
  external provider API surfaces are available without persisting raw provider
  payloads or sensitive workforce telemetry.
- **Blueprint goal:** "Provide time capture and attendance as an external service
  feeding payroll and workforce analytics through governed contracts."
- **Minimum Blueprint contracts:** Employee/Person Identifier Contract, Time Entry
  Contract, Attendance Event Contract.
- **Support model:** L1 ITSM Service Desk, L2 Integration Ops, L3 Vendor Support.
- **Integration contracts:** API/Event preferred; file fallback only if approved in
  a later provider-specific adapter pack.
- **MVP UI decision:** None in this first slice. No Platform Admin UI, no tenant UI,
  no DataTable, no Razor view, no JavaScript, and no RESX files.
- **Golden Reference decision:** `golden_reference: none` because the first slice is
  backend/API contract only, not a CRUD/DataTable UI module.
- **Ownership status:** User decision confirms `platform-shared-services` and
  `Diten.Platform` as the governance/runtime owner for this first slice. Future
  workforce/HCM domain relocation is a governance follow-up, not a blocker.

## 2. Ownership and Boundaries

### In scope

- Provider-neutral metadata for UKG/Kronos-style external time-and-attendance
  systems:
  - external provider profile and display identity;
  - provider family code without provider-specific adapter behavior;
  - contract version, lifecycle state, and effective-dated metadata;
  - employee/person identifier reference mapping metadata;
  - time entry reference metadata;
  - attendance event reference metadata;
  - attendance summary reference metadata;
  - sync checkpoint and source health metadata;
  - access, audit, correlation, and idempotency metadata.
- Tenant-scoped backend/API implementation in `Diten.Platform`.
- Validation that rejects raw credentials, raw tokens, secrets, raw provider
  payloads, biometric markers, geolocation markers, payroll markers, and internal
  leave/accrual/roster/payroll ownership objects.
- Same-tenant reference validation for approved canonical employee/person,
  organization, and position references where the first slice accepts those IDs.
- Gateway route need may be reported as an integration-agent task only.

### Out of scope

- Provider-specific UKG or Kronos adapter/client/webhook implementation.
- Internal time-entry business engine.
- Internal attendance policy engine.
- Internal leave-management engine, leave accrual, leave balances, carry-forward,
  leave approval, or leave cancellation workflow.
- Internal shift-planning, roster optimization, employee clock-in UI, or employee
  self-service timesheet UI.
- Employee, person, organization, position, employment contract, or payroll master
  ownership.
- Payroll calculation, tax calculation, salary/compensation, payslip generation,
  statutory payroll governance, payment execution, or payroll-ready amount
  computation.
- Raw time/attendance payload, biometric data, geolocation data, credential, token,
  secret, payroll data, provider response body, or confidential header persistence.
- Frontend pages, DataTables, localization resources, menu/navigation entries, or
  browser smoke tests.
- Direct edits to `gateway/Diten.ApiGateway/**/ocelot.json`.
- Edits to MOD-0251, MOD-0279, or MOD-0281 pack/implementation files.

### Ownership rule

- **MOD-0280** owns only external time-and-attendance provider boundary metadata,
  provider-neutral reference contracts, source health, checkpoints, and first-slice
  API surface.
- **MOD-0251** owns HRIS external SoR employee/employment source semantics and must
  not be duplicated here.
- **MOD-0279** consumes approved payroll-ready references downstream but does not
  own MOD-0280 imports and MOD-0280 does not calculate payroll.
- **MOD-0281** owns payroll integration governance, approval controls,
  reconciliation controls, and audit-oriented payroll control workflows if/when
  approved in the repository.
- **MOD-0288** owns Organization, Person & Position Directory and must not be
  duplicated here. MOD-0280 may store only same-tenant references after fail-closed
  validation.
- **PSS modules** own RBAC, audit, gateway, data contract registry, scheduler,
  event bus, secrets, and integration monitoring infrastructure.

## 3. Owned Objects

### Domain entities

- `TimeAttendanceExternalProviderProfile`
- `TimeAttendanceContractProfile`
- `TimeAttendanceEmployeeReferenceMap`
- `TimeAttendanceEventReference`
- `AttendanceSummaryReference`
- `TimeAttendanceSyncCheckpoint`
- `TimeAttendanceProviderHealthSnapshot`

### Repositories

- `ITimeAttendanceProviderRepository`

### Commands

- `CreateTimeAttendanceProviderProfileCommand`
- `UpdateTimeAttendanceProviderProfileCommand`
- `ArchiveTimeAttendanceProviderProfileCommand`
- `UpdateTimeAttendanceContractProfileCommand`
- `CreateTimeAttendanceEmployeeReferenceMapCommand`
- `UpdateTimeAttendanceEmployeeReferenceMapCommand`
- `RecordTimeAttendanceEventReferenceCommand`
- `RecordAttendanceSummaryReferenceCommand`
- `RecordTimeAttendanceSyncCheckpointCommand`
- `RecordTimeAttendanceProviderHealthSnapshotCommand`

### Queries

- `GetTimeAttendanceProviderProfileListQuery`
- `GetTimeAttendanceProviderProfileByIdQuery`
- `GetTimeAttendanceContractProfileQuery`
- `GetTimeAttendanceEmployeeReferenceMapsQuery`
- `GetTimeAttendanceEventReferencesQuery`
- `GetAttendanceSummaryReferencesQuery`
- `GetTimeAttendanceSyncCheckpointQuery`
- `GetTimeAttendanceProviderHealthQuery`

### API endpoints

- `GET /api/time-attendance-providers`
- `GET /api/time-attendance-providers/{id}`
- `POST /api/time-attendance-providers`
- `PUT /api/time-attendance-providers/{id}`
- `PATCH /api/time-attendance-providers/{id}/archive`
- `PUT /api/time-attendance-providers/{id}/contract-profile`
- `GET /api/time-attendance-providers/{id}/contract-profile`
- `POST /api/time-attendance-providers/{id}/employee-reference-maps`
- `PUT /api/time-attendance-providers/{id}/employee-reference-maps/{mapId}`
- `GET /api/time-attendance-providers/{id}/employee-reference-maps`
- `POST /api/time-attendance-providers/{id}/event-references`
- `GET /api/time-attendance-providers/{id}/event-references`
- `POST /api/time-attendance-providers/{id}/attendance-summary-references`
- `GET /api/time-attendance-providers/{id}/attendance-summary-references`
- `POST /api/time-attendance-providers/{id}/sync-checkpoints`
- `GET /api/time-attendance-providers/{id}/sync-checkpoint`
- `POST /api/time-attendance-providers/{id}/health-snapshots`
- `GET /api/time-attendance-providers/{id}/health`

### Not owned

- Native time-entry, leave, accrual, roster, or payroll calculation entities.
- Raw provider payload storage entities.
- Biometric or geolocation storage entities.
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

### `TimeAttendanceExternalProviderProfile`

| Field | Type | Required | Notes |
|---|---|---|---|
| TenantId | Guid | Yes | Server-resolved only; never accepted from client payloads. |
| Code | string | Yes | Tenant-unique where `IsDeleted = false`; trim, uppercase, max 64. |
| DisplayName | string | Yes | Max 200. |
| ProviderFamily | enum/string | Yes | `UKG`, `Kronos`, `OtherExternalTimeAttendance`; no adapter behavior implied. |
| ExternalProviderAccountId | string | Yes | External account/reference only, max 128; never a secret. |
| LifecycleState | enum/string | Yes | `Draft`, `Active`, `Suspended`, `Deprecated`, `Archived`. |
| ConnectionProfileReference | string? | No | Reference to approved secret/config only; no raw value. |
| SupportOwner | string? | No | Team/contact label, max 200; no sensitive personal data required. |
| Notes | string? | No | Max 1000; rejects raw payload, credential, biometric, geolocation, payroll markers. |

### `TimeAttendanceContractProfile`

| Field | Type | Required | Notes |
|---|---|---|---|
| TenantId | Guid | Yes | Server-resolved only. |
| ProviderProfileId | Guid | Yes | Same-tenant profile reference. |
| ContractVersion | string | Yes | Required, immutable once referenced; max 64. |
| EffectiveFrom | DateTimeOffset | Yes | Start of contract applicability. |
| EffectiveTo | DateTimeOffset? | No | Must be greater than `EffectiveFrom` when present. |
| SupportedObjectTypes | string[] | Yes | First slice permits `Employee`, `TimeEntryReference`, `AttendanceEventReference`, `AttendanceSummaryReference` only. |
| StatusVocabulary | string[] | Yes | Provider-neutral status values only. |
| ErrorVocabulary | string[] | No | Provider-neutral; no provider payload examples. |
| CorrelationIdPattern | string? | No | Max 128. |

### `TimeAttendanceEmployeeReferenceMap`

| Field | Type | Required | Notes |
|---|---|---|---|
| TenantId | Guid | Yes | Server-resolved only. |
| ProviderProfileId | Guid | Yes | Same-tenant profile reference. |
| ExternalEmployeeReference | string | Yes | External reference only, max 128; no raw PII or provider payload. |
| HrisReferenceId | Guid? | No | Same-tenant MOD-0251 HRIS reference if exposed by approved contract. |
| PersonReferenceId | Guid? | No | Same-tenant MOD-0288 Person reference; fail closed if unavailable/missing/cross-tenant. |
| OrganizationUnitReferenceId | Guid? | No | Same-tenant MOD-0288 OrganizationUnit reference. |
| PositionReferenceId | Guid? | No | Same-tenant MOD-0288 Position reference. |
| MappingState | enum/string | Yes | `Unmapped`, `Mapped`, `Suspended`, `Archived`. |
| LastValidatedAt | DateTimeOffset? | No | Set only after same-tenant reference validation. |

### `TimeAttendanceEventReference`

| Field | Type | Required | Notes |
|---|---|---|---|
| TenantId | Guid | Yes | Server-resolved only. |
| ProviderProfileId | Guid | Yes | Same-tenant profile reference. |
| ExternalEventId | string | Yes | Reference/idempotency value only, max 128. |
| EventType | enum/string | Yes | `TimeEntry`, `ClockIn`, `ClockOut`, `BreakStart`, `BreakEnd`, `AttendanceAdjustment`; metadata only. |
| ExternalEmployeeReference | string | Yes | Reference only, max 128. |
| ProviderTimestamp | DateTimeOffset | Yes | Provider timestamp and offset preserved. |
| ProviderTimeZoneId | string? | No | IANA preferred, max 128. |
| ProcessingState | enum/string | Yes | Provider-neutral lifecycle state. |
| IdempotencyKey | string | Yes | Tenant/provider/account/event deterministic key. |
| CorrelationId | string? | No | Max 128. |

### `AttendanceSummaryReference`

| Field | Type | Required | Notes |
|---|---|---|---|
| TenantId | Guid | Yes | Server-resolved only. |
| ProviderProfileId | Guid | Yes | Same-tenant profile reference. |
| ExternalSummaryId | string | Yes | Reference only, max 128. |
| ExternalEmployeeReference | string | Yes | Reference only, max 128. |
| SummaryPeriodStart | DateOnly | Yes | Metadata only; no payroll calculation. |
| SummaryPeriodEnd | DateOnly | Yes | Must be on or after `SummaryPeriodStart`. |
| SummaryState | enum/string | Yes | Provider-neutral status. |
| CorrelationId | string? | No | Max 128. |

### `TimeAttendanceSyncCheckpoint`

| Field | Type | Required | Notes |
|---|---|---|---|
| TenantId | Guid | Yes | Server-resolved only. |
| ProviderProfileId | Guid | Yes | Same-tenant profile reference. |
| SyncRunId | string | Yes | Reference only, max 128. |
| SyncMode | enum/string | Yes | `Manual`, `Incremental`, `Full`, `Replay`; no scheduler ownership. |
| StartedAt | DateTimeOffset | Yes | Sync metadata. |
| CompletedAt | DateTimeOffset? | No | Must be after `StartedAt` when present. |
| CheckpointReference | string? | No | Opaque reference only; no raw payload/token. |
| RecordsSeen | int | Yes | Count only. |
| RecordsAccepted | int | Yes | Count only. |
| RecordsRejected | int | Yes | Count only. |
| Status | enum/string | Yes | Provider-neutral status. |
| ErrorSummary | string? | No | Redacted only; no raw payload/secret/biometric/geolocation/payroll content. |

### `TimeAttendanceProviderHealthSnapshot`

| Field | Type | Required | Notes |
|---|---|---|---|
| TenantId | Guid | Yes | Server-resolved only. |
| ProviderProfileId | Guid | Yes | Same-tenant profile reference. |
| HealthState | enum/string | Yes | `Unknown`, `Ready`, `Degraded`, `Unavailable`, `Blocked`. |
| CheckedAt | DateTimeOffset | Yes | Snapshot timestamp. |
| RedactedMessage | string? | No | Max 1000; no secrets/raw payload/biometric/geolocation/payroll data. |
| CorrelationId | string? | No | Max 128. |

## 5. Repo Scope

Approved first backend/API slice scope:

- `execution/domains/platform-shared-services/module-packs/MOD-0280-time-attendance-ukg-kronos-external-provider.md`
- `services/Diten.Platform/src/Diten.Platform.Domain/**` for MOD-0280 domain
  entities, enums, and repository interfaces.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/TimeAttendanceProviders/**`
  for commands, queries, handlers, validators, models, and contracts.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/**` for
  MOD-0280 Mongo repositories, collection names, and indexes.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/**` only for MOD-0280
  dependency injection registration needed by repositories.
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/**` for the thin
  `TimeAttendanceProvidersController`.
- `services/Diten.Platform/src/Diten.Platform.API/**` only for MOD-0280 API DI or
  routing glue required by the controller.
- `services/Diten.Platform/tests/**` for MOD-0280 application/API/persistence tests.

Explicitly outside this first slice:

- `services/Diten.Platform.Contracts/**` unless a later approved amendment makes a
  shared contracts project necessary.
- `frontend/Diten.Web/**`
- `gateway/Diten.ApiGateway/**/ocelot.json` direct edits.
- `services/Diten.AuthService/**`
- `services/Diten.DevEnablementService/**`
- `services/Diten.EnterpriseStrategyService/**`
- `services/Diten.MdmService/**`
- MOD-0251, MOD-0279, and MOD-0281 pack/implementation files.

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
- `execution/domains/platform-shared-services/module-packs/MOD-0251-hris-external-sor.md`
- `execution/domains/platform-shared-services/module-packs/MOD-0279-payroll-engine-external-sor.md`
- Any MOD-0251, MOD-0279, MOD-0281, MOD-0288, MOD-0012, MOD-0018, MOD-0021,
  MOD-0032, or MOD-0037 owned implementation files unless a future approved pack
  explicitly grants a contract-only touch point.

## 7. Dependencies

| Dependency | Classification | MOD-0280 use |
|---|---|---|
| MOD-0251 HRIS External SoR | Boundary dependency | Employee/employment source semantics; MOD-0280 may reference HRIS identities but must not duplicate HRIS source ownership. |
| MOD-0279 Payroll Engine External SoR | Downstream dependency | Payroll consumes only approved canonical time/attendance references; MOD-0280 does not calculate payroll. |
| MOD-0281 Payroll Integration & Governance | Governance dependency | Payroll governance, approvals, reconciliation controls, and audit workflows remain outside MOD-0280. |
| MOD-0288 Organization, Person & Position Directory | Reference dependency | Same-tenant person/org/position references; no directory duplication. |
| MOD-0017 SSO / MFA | Runtime prerequisite | Authentication context for external provider admin/API operations. |
| MOD-0018 RBAC / ABAC Authorization | Runtime prerequisite | `[HasPermission]` authorization for provider profile, mapping, checkpoint, and health actions. |
| MOD-0012 Secrets & Configuration Vault | Runtime prerequisite | Credential references only; no raw secrets in MongoDB/source/logs/browser. |
| MOD-0021 Audit Trail Service | Runtime integration | Audits for metadata changes, mapping corrections, checkpoint changes, and operational actions. |
| MOD-0026 Scheduler / Job Orchestration | Future integration | Future polling/replay jobs only; not part of first provider-neutral API slice. |
| MOD-0003 Data Contract Registry | Contract governance | Broad contract publication later; first slice may store local contract metadata only. |
| MOD-0032 API Gateway | Routing dependency | Gateway exposure, if needed, is integration-agent owned. |
| MOD-0037 Integration Monitoring & Reconciliation | Future integration | Production external provider monitoring beyond local health snapshots. |

Missing dependencies must not be implemented or simulated inside MOD-0280.

## 8. Runtime Constraints

- Runtime implementation is limited to the provider-neutral backend/API contract
  slice in `Diten.Platform`.
- Tenant isolation is mandatory for all MOD-0280 tenant-scoped metadata.
- Cross-tenant access must return `404` fail-closed.
- Client payloads must never supply `TenantId`.
- Soft delete requires `IsDeleted` and `DeletedAt` on soft-deleted MOD-0280 records.
- Duplicate active `Code` within tenant scope must return `409`.
- Controllers must stay thin and delegate to MediatR.
- API responses must use repository-standard `Response<T>` envelope and
  `CustomBaseController`.
- Validators must reject raw time/attendance payloads, credentials, raw tokens, raw
  secrets, biometric data markers, geolocation data markers, payroll data markers,
  and provider-specific adapter execution fields.
- No internal time-entry, leave/accrual, roster optimization, or payroll
  calculation behavior is approved.
- No external provider calls may be made; provider-specific adapters are out of
  scope.
- Provider timestamps and timezone identifiers are metadata only; application
  server local time must not become business truth.
- Effective-dated contract metadata must define valid-from, optional valid-to,
  version, overlap behavior, and historical immutability after use.
- Any HRIS, Person, OrganizationUnit, or Position reference accepted by MOD-0280
  must be validated within the same tenant before the record is accepted as
  mapped/current. If a required validator/repository is unavailable, fail closed.

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
DataTables verifier expectation before frontend work begins.

## 10. Backend File Convention

Approved first-slice backend convention:

- Application feature folder:
  `services/Diten.Platform/src/Diten.Platform.Application/Features/TimeAttendanceProviders/`
- One public command/query/handler/validator per file where CQRS is used.
- Sealed records for commands/queries.
- Sealed classes for handlers.
- `Handlers/CommandHandlers` and `Handlers/QueryHandlers` separation where the
  repo pattern applies.
- Request/response DTOs may be grouped in `TimeAttendanceProviderModels.cs` if
  consistent with nearby Diten.Platform feature patterns.
- No business logic in controllers.
- No Domain or Application dependency on Infrastructure.
- Repositories and Mongo indexes live in Diten.Platform infrastructure/persistence
  scope.
- `Response<T>` envelope and `CustomBaseController` are mandatory for API
  endpoints.
- Provider HTTP clients, SDK wrappers, webhook handlers, background sync jobs,
  UKG/Kronos-specific mapping code, and adapter-specific serialization are not
  authorized.

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
- navigation/menu changes;
- employee clock-in UI, timesheet UI, leave request UI, leave balance UI, shift
  planning UI, or employee self-service surface.

## 12. Validation Rules

- Canonical ID must remain `MOD-0280`.
- Canonical name must remain `Time & Attendance (UKG/Kronos) [External Provider]`.
- `TenantId` is server-resolved; request DTOs must not expose it.
- `Code` is required, trimmed, uppercased, max 64, and unique for active records
  within the tenant.
- `DisplayName` is required and max 200.
- `ProviderFamily` must be one of the approved provider-neutral values; it must not
  invoke provider-specific execution behavior.
- `ExternalProviderAccountId`, external employee references, event IDs, summary
  IDs, checkpoint references, and idempotency keys are references only and must not
  contain raw payloads, credentials, tokens, biometric data, geolocation data, or
  payroll data.
- `ContractVersion` is required, max 64, and immutable once used by downstream
  references.
- `EffectiveTo`, when present, must be greater than `EffectiveFrom`.
- `SupportedObjectTypes` rejects internal attendance/leave/roster/payroll ownership
  objects and permits only `Employee`, `TimeEntryReference`,
  `AttendanceEventReference`, and `AttendanceSummaryReference` in this slice.
- `ProviderTimestamp` and `ProviderTimeZoneId` are metadata only and must not be
  used to infer payroll calculations.
- `RecordsAccepted + RecordsRejected` must not exceed `RecordsSeen`.
- Mapped employee references require at least one approved same-tenant canonical
  reference. Missing/cross-tenant/unavailable reference validation fails closed.
- `RedactedMessage`, `Notes`, `ErrorSummary`, and failure payloads must be
  scanned/rejected for raw credential/token/secret, raw time/attendance payload,
  biometric, geolocation, and payroll markers.
- Missing dependency contracts fail closed.

## 13. Failure Path to Verify

Implementation must verify at least:

1. canonical name mismatch fails module identity gate;
2. registry collision blocks pack approval;
3. duplicate active `Code` in the same tenant returns `409`;
4. same `Code` in another tenant does not collide;
5. cross-tenant provider profile lookup returns `404`;
6. update/archive/map/checkpoint/health operations for missing or cross-tenant
   provider profile return fail-closed `404`;
7. unauthorized access is denied before handler execution;
8. missing permission returns `403`;
9. raw time/attendance payload-like content is rejected with `400`;
10. raw credential/token/secret-like content is rejected with `400`;
11. biometric/geolocation/payroll-like fields or content are rejected with `400`;
12. provider-specific adapter selection or execution fields are rejected with `400`;
13. internal attendance/leave/roster/payroll ownership object types are rejected
    with `400`;
14. invalid or cross-tenant HRIS, Person, OrganizationUnit, or Position reference
    fails closed;
15. unresolved references are not accepted as `Mapped`;
16. stale contract version update returns `409`;
17. soft delete sets both `IsDeleted` and `DeletedAt`;
18. archived records are excluded from active list and duplicate checks;
19. no external provider network call is made by MOD-0280 handlers;
20. Gateway route requirement is reported as integration-agent task only, with no
    direct `ocelot.json` edit.

## 14. Authorization Convention

- Actor policy: `[Authorize]` plus repository-standard permission attribute.
- Permission prefix: `platform.time-attendance-providers.*`.
- Approved first-slice permissions:
  - `platform.time-attendance-providers.read`
  - `platform.time-attendance-providers.create`
  - `platform.time-attendance-providers.update`
  - `platform.time-attendance-providers.archive`
  - `platform.time-attendance-providers.update-contract`
  - `platform.time-attendance-providers.map-employee-reference`
  - `platform.time-attendance-providers.record-event`
  - `platform.time-attendance-providers.record-summary`
  - `platform.time-attendance-providers.record-checkpoint`
  - `platform.time-attendance-providers.record-health`
  - `platform.time-attendance-providers.read-health`

## 15. Gateway / API Routing Decision

- API resource path for service-local implementation:
  `/api/time-attendance-providers`.
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

1. `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0280 --name "Time & Attendance (UKG/Kronos) [External Provider]"` passes.
2. Frontmatter and body both show `status: ready-for-dev` intent and authorize only
   the provider-neutral backend/API contract slice.
3. Implementation touches only the repo scope listed in Section 5.
4. No UI, Razor, JavaScript, DataTable, RESX, menu, or frontend file is created or
   edited.
5. No direct Gateway `ocelot.json` edit is made; route need is reported as an
   integration-agent task if required.
6. No provider-specific UKG/Kronos adapter/client/webhook code is implemented.
7. No internal time-entry engine, attendance policy engine, leave/accrual engine,
   roster optimization, payroll calculation, biometric/geolocation storage,
   credential/token/secret storage, or raw provider payload field is persisted.
8. `TimeAttendanceProvidersController` is thin, uses `CustomBaseController`, and
   delegates to MediatR.
9. All MOD-0280 API responses use `Response<T>` envelope.
10. TenantId is resolved server-side and never accepted from client payloads.
11. Cross-tenant lookup/update/archive/map/checkpoint/health operations return
    `404` fail-closed.
12. Duplicate active `Code` within a tenant returns `409`.
13. Soft delete sets `IsDeleted` and `DeletedAt` and excludes archived records from
    active list results.
14. Validators reject raw credential/token/secret, raw time/attendance payload,
    biometric, geolocation, and payroll markers.
15. Validators reject internal attendance/leave/roster/payroll ownership object
    types outside this pack.
16. HRIS, Person, OrganizationUnit, and Position references are same-tenant
    validated before a mapping can become `Mapped`; unavailable validators fail
    closed.
17. Permission prefix `platform.time-attendance-providers.*` is enforced on
    endpoints.
18. Mongo indexes enforce tenant-aware active-code uniqueness and support list
    queries.
19. Diten.Platform build passes:
    `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug`.
20. MOD-0280 tests cover tenant isolation, duplicate code, validation,
    authorization, unauthorized/missing permission, soft delete, no raw
    secrets/payloads, no biometric/geolocation/payroll persistence, and reference
    fail-closed behavior.

## 17. Test Expectations

Required implementation validation:

- Module identity preflight command in Section 16.
- Diten.Platform API build command in Section 16.
- Application handler tests for create, update, archive, contract profile, employee
  reference map, event reference, attendance summary reference, sync checkpoint,
  and health snapshot flows.
- Tenant isolation tests for list, get, update, archive, mapping, checkpoint, and
  health endpoints.
- Duplicate active `Code` returns `409`; archived code reuse behavior is explicitly
  tested according to implementation choice.
- Authorization tests for unauthenticated and missing-permission access.
- Validation tests for raw secret/token/credential, raw time/attendance payload,
  biometric-like content, geolocation-like content, payroll-like content,
  provider-specific adapter fields, and internal attendance/leave/roster/payroll
  object types.
- MOD-0251/MOD-0288 same-tenant reference fail-closed tests where references are
  accepted by the implementation.
- Soft-delete tests proving `IsDeleted` and `DeletedAt`.
- Repository/index tests if existing Diten.Platform test patterns support them.
- UI/DataTable/l10n verifier: N/A for this first slice.

## 18. Ready-for-dev Checklist

- [x] Canonical Blueprint name verified.
- [x] DCP-002 module identity preflight passes for MOD-0280.
- [x] Friendly name separated from canonical identity.
- [x] Pack status set to `ready-for-dev`.
- [x] User confirmed MOD-0280 is the next module to develop.
- [x] User confirmed `platform-shared-services` as first-slice domain owner.
- [x] User confirmed `Diten.Platform` as first-slice runtime service.
- [x] User confirmed `shell: none`.
- [x] User confirmed `golden_reference: none`.
- [x] Provider-neutral backend/API contract scope approved.
- [x] Provider-specific UKG/Kronos adapters excluded.
- [x] Internal time-entry engine excluded.
- [x] Leave/accrual/roster optimization/payroll calculation excluded.
- [x] Raw time/attendance payload, credential, biometric, geolocation, and payroll
  data persistence excluded.
- [x] MOD-0251, MOD-0279, and MOD-0281 file edits excluded from this pack.
- [x] MOD-0251, MOD-0279, and MOD-0281 boundaries preserved as dependencies.
- [x] MOD-0288 boundary preserved through same-tenant reference validation.
- [x] Permission prefix approved: `platform.time-attendance-providers.*`.
- [x] Gateway route edits restricted to integration-agent task/reporting.
- [x] Acceptance Criteria are runtime-testable.
- [x] Test expectations include tenant isolation, duplicate code, validation,
  authorization, soft delete, reference validation, and no-raw-data checks.
- [x] Open blockers: none.

## 19. Implementation Notes

- The user-facing short description `Time & Attendance External Provider` is not
  the canonical module name.
- Blueprint row 281 describes an external provider integration boundary, not a
  native internal attendance/leave/timekeeping product module.
- The first slice intentionally stores reference metadata only. It does not store
  raw time/attendance payloads, biometric data, geolocation data, payroll data,
  credentials, tokens, or secrets.
- `Diten.Platform` and `platform-shared-services` are accepted for this first slice
  by user decision. A future workforce/HCM domain may assume long-term ownership
  only through a separate governance change.
- `services/Diten.Platform.Contracts/**` is not required for the first slice unless
  a later approved amendment changes the API contract distribution model.
- Gateway route creation, if required, remains an integration-agent responsibility.

## 20. Follow-up Items

- [ ] If Gateway exposure is required, create an integration-agent task for
  `/api/time-attendance-providers` routes.
- [ ] Define provider-specific UKG/Kronos adapters in separate approved module
  packs if/when needed.
- [ ] Define any native time-entry, leave/accrual, roster optimization, or payroll
  calculation capability under separate canonical IDs before implementation.
- [ ] Define broad contract publication through MOD-0003 or an approved interim
  mechanism if downstream services require shared contracts.
- [ ] Define integration monitoring handoff through MOD-0037 for production-grade
  external provider monitoring.
- [ ] Coordinate MOD-0251 HRIS, MOD-0279 payroll external SoR, and MOD-0281 payroll
  governance boundaries before downstream production workflows consume MOD-0280
  references.
