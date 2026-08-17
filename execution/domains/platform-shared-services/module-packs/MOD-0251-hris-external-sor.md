---
id: MOD-0251
name: HRIS (Workday/SuccessFactors/Oracle HCM) [External SoR]
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: BaseEntity
status: ready-for-dev
owner: enterprise-architect / platform-team
branch: feature/pss/mod-0251-hris-external-sor
started: 2026-08-10
target: 2026-08-24
form_field_count: 0
---

# MOD-0251 - HRIS (Workday/SuccessFactors/Oracle HCM) [External SoR]

> **Execution status:** `ready-for-dev` for the approved first slice. This pack
> authorizes production implementation only for the provider-neutral backend/API
> contract in `Diten.Platform`. It authorizes no UI, provider-specific adapter,
> raw HRIS payload persistence, payroll/time-attendance scope, or direct Gateway
> route edit.

> **Governance ownership note:** For this first slice,
> `platform-shared-services` is accepted as the governance and runtime owner, and
> `Diten.Platform` remains the runtime service. If EA later bootstraps a
> dedicated HR/workforce domain or service, that move must be handled by a
> separate approved governance change and revised module pack.

## 1. Module Summary

MOD-0251 defines the provider-neutral external HRIS source boundary for Workday,
SAP SuccessFactors, Oracle HCM, or equivalent HRIS platforms. It governs source
registration, external identifier mapping, synchronization checkpoints,
provenance, and integration contract metadata for Employee, Org, and Job source
data.

The module is an External SoR boundary, not an internal HCM module. It prepares
future HR/HCM and TEP delivery by allowing internal consumers to reference
approved HRIS source contracts without embedding vendor-specific logic or
duplicating internal directory ownership.

DCP-002 preflight:

```text
OK  MOD-0251: proven against Blueprint/registry.
```

Canonical Blueprint evidence:

- Blueprint_Data row 252: `MOD-0251` =
  `HRIS (Workday/SuccessFactors/Oracle HCM) [External SoR]`.
- Domain / Landscape: `7) External Systems & Providers`.
- Capability Group: `External SoR`.
- Primary output: External SoR supporting governed integration for Employee,
  Org, and Job.
- Minimum integration contract: `EXT-BASE`.

## 2. Ownership and Boundaries

### In scope

- Provider-neutral HRIS source profile metadata.
- Tenant-scoped HRIS source enablement and lifecycle.
- External company / tenant identifier metadata.
- Secret reference keys only, never raw credentials.
- Employee / Org / Job external-to-internal identifier mapping metadata.
- Mapping schema/version metadata.
- Synchronization mode, cursor, checkpoint, and last-run metadata.
- HRIS-specific provenance and correlation metadata.
- HRIS source health/status references.
- Contract metadata required by MOD-0288 and future HCM/TEP consumers.

### Out of scope

- Full employee lifecycle management.
- Recruitment, onboarding, offboarding, performance, learning, compensation, or
  benefits.
- Payroll calculation or payroll approval governance.
- Time and attendance calculation.
- Internal OrganizationUnit, Person, Position, or PositionAssignment ownership.
- Raw HRIS payload warehousing.
- Generic secret storage implementation.
- Generic scheduler implementation.
- Generic event bus implementation.
- Generic integration monitoring UI.
- Provider-specific adapter implementation.

### MOD-0251 / MOD-0288 boundary

- MOD-0251 is the external HRIS source boundary for Employee, Org, and Job.
- MOD-0288 owns the internal Organization, Person & Position Directory.
- MOD-0288 may consume governed MOD-0251 contracts for routing, binding,
  assignment, and reference validation.
- MOD-0251 must not persist or own MOD-0288 `OrganizationUnit`, `Person`,
  `Position`, or `PositionAssignment` aggregates.
- MOD-0251 stores external identifiers, mapping metadata, provenance, and sync
  status. MOD-0288 stores internal directory records.
- Identity resolution, conflict policy, and transformation ownership remain
  limited to provider-neutral reference validation in this first slice. Any
  deeper transformation ownership requires a follow-up decision.

## 3. Owned Objects

### First-slice entities

- `HrisSourceProfile`
- `HrisExternalIdentifierMap`
- `HrisMappingProfile`
- `HrisSyncCheckpoint`
- `HrisSourceHealthSnapshot`

### First-slice commands

- `CreateHrisSourceProfileCommand`
- `UpdateHrisSourceProfileCommand`
- `ArchiveHrisSourceProfileCommand`
- `ValidateHrisSourceConnectionCommand`
- `UpdateHrisMappingProfileCommand`
- `RecordHrisSyncCheckpointCommand`

### First-slice queries

- `GetHrisSourceProfileListQuery`
- `GetHrisSourceProfileByIdQuery`
- `GetHrisExternalIdentifierMapQuery`
- `GetHrisSyncCheckpointQuery`
- `GetHrisSourceHealthQuery`

### First-slice DTOs

- `HrisSourceProfileDto`
- `HrisSourceProfileListItemDto`
- `HrisSourceProfileCreateRequest`
- `HrisSourceProfileUpdateRequest`
- `HrisMappingProfileDto`
- `HrisSyncCheckpointDto`
- `HrisSourceHealthDto`

### First-slice API endpoints

Approved provider-neutral API shape for `Diten.Platform` implementation:

- `GET /api/hris-sources`
- `GET /api/hris-sources/{id}`
- `POST /api/hris-sources`
- `PUT /api/hris-sources/{id}`
- `PATCH /api/hris-sources/{id}/archive`
- `POST /api/hris-sources/{id}/validate-connection`
- `PUT /api/hris-sources/{id}/mapping-profile`
- `GET /api/hris-sources/{id}/sync-checkpoint`
- `GET /api/hris-sources/{id}/health`

### First-slice permissions

- `platform.hris-sources.read`
- `platform.hris-sources.create`
- `platform.hris-sources.update`
- `platform.hris-sources.archive`
- `platform.hris-sources.validate-connection`
- `platform.hris-sources.update-mapping`
- `platform.hris-sources.read-health`

## 4. Entity Fields

### HrisSourceProfile

| Field | Type | Required | Rules | Index |
|---|---|---|---|---|
| `Code` | string | Yes | Trim, uppercase, max 64, stable tenant-scoped source code | Unique with `TenantId`, `IsDeleted=false` |
| `DisplayName` | string | Yes | Trim, max 200 | Text/search optional |
| `ProviderKind` | enum/string | Yes | `Workday`, `SuccessFactors`, `OracleHcm`, `Other`; provider-neutral behavior only | Indexed |
| `ExternalTenantKey` | string | Conditional | Required when provider requires external tenant/company partition; max 128 | No raw secrets |
| `ConnectionProfileReference` | string | Yes | Secret/config reference key only; no raw credentials | Indexed |
| `LifecycleState` | enum/string | Yes | `Draft`, `Active`, `Archived`, `Suspended` | Indexed |
| `SyncMode` | enum/string | Yes | `Manual`, `ScheduledPull`, `Webhook`, `FileImport`, `Disabled` | Indexed |
| `MappingProfileId` | Guid? | Conditional | Required before activation | Tenant-checked |
| `LastValidatedAt` | DateTimeOffset? | No | Set only by approved validation command | - |
| `LastSyncCheckpointId` | Guid? | No | References latest checkpoint | Tenant-checked |
| `CorrelationId` | string? | No | Last operational correlation id, max 128 | - |

### HrisExternalIdentifierMap

| Field | Type | Required | Rules | Index |
|---|---|---|---|---|
| `SourceProfileId` | Guid | Yes | Same tenant; existing source profile | Compound |
| `ExternalObjectType` | enum/string | Yes | `Employee`, `Org`, `Job`; no payroll/time object types | Compound |
| `ExternalObjectId` | string | Yes | Trim, max 256 | Unique with source and type |
| `InternalReferenceType` | enum/string | Yes | `Person`, `OrganizationUnit`, `Position`, or approved future value | Indexed |
| `InternalReferenceId` | Guid? | No | MOD-0288 reference only after validation | Tenant-checked |
| `MappingState` | enum/string | Yes | `Unmapped`, `Mapped`, `Conflict`, `Ignored` | Indexed |
| `ProvenanceHash` | string? | No | Hash of mapped source reference metadata, not raw payload | - |

### HrisMappingProfile

| Field | Type | Required | Rules | Index |
|---|---|---|---|---|
| `Code` | string | Yes | Trim, uppercase, max 64 | Unique with `TenantId` |
| `DisplayName` | string | Yes | Trim, max 200 | - |
| `MappingProfileVersion` | string | Yes | Semantic/profile version; do not use reserved `Version` name | Indexed |
| `ExternalSchemaReference` | string | Yes | Contract/schema id reference, no raw schema payload by default | Indexed |
| `EffectiveFrom` | DateTimeOffset? | No | Optional activation date | - |
| `IsActive` | bool | Yes | Only one active mapping per source unless approved otherwise | Compound |

### HrisSyncCheckpoint

| Field | Type | Required | Rules | Index |
|---|---|---|---|---|
| `SourceProfileId` | Guid | Yes | Same tenant | Compound |
| `SyncRunId` | string | Yes | Stable external/internal run id, max 128 | Unique with source |
| `SyncMode` | enum/string | Yes | Must match approved mode | Indexed |
| `StartedAt` | DateTimeOffset | Yes | UTC | Indexed |
| `CompletedAt` | DateTimeOffset? | No | UTC; null while running | - |
| `Status` | enum/string | Yes | `Started`, `Succeeded`, `Failed`, `PartiallySucceeded`, `Cancelled` | Indexed |
| `CursorReference` | string? | No | Cursor/checkpoint reference; avoid raw sensitive payload | - |
| `RecordsSeen` | int | No | Non-negative | - |
| `RecordsAccepted` | int | No | Non-negative, <= `RecordsSeen` | - |
| `RecordsRejected` | int | No | Non-negative, <= `RecordsSeen` | - |
| `ErrorSummary` | string? | No | Redacted, max 1000 | - |

### HrisSourceHealthSnapshot

| Field | Type | Required | Rules | Index |
|---|---|---|---|---|
| `SourceProfileId` | Guid | Yes | Same tenant | Compound |
| `ObservedAt` | DateTimeOffset | Yes | UTC | Indexed |
| `HealthState` | enum/string | Yes | `Healthy`, `Degraded`, `Down`, `Unknown` | Indexed |
| `LatencyMs` | int? | No | Non-negative | - |
| `LastSuccessfulSyncAt` | DateTimeOffset? | No | UTC | - |
| `RedactedMessage` | string? | No | No secrets, tokens, raw PII, or provider stack traces | - |

All first-slice entities are tenant-owned and use `BaseEntity`; inherited fields
(`Id`, `TenantId`, `IsDeleted`, `DeletedAt`, `CreatedAt`, `UpdatedAt`, technical
`Version`) must not be redefined.

## 5. Repo Scope

This ready-for-dev pack authorizes this module pack plus the first backend/API
slice below:

- `execution/domains/platform-shared-services/module-packs/MOD-0251-hris-external-sor.md`
- `services/Diten.Platform/src/Diten.Platform.Domain/**` for MOD-0251 domain
  entities, enums, and repository contracts.
- `services/Diten.Platform/src/Diten.Platform.Application/**` for
  `Features/HrisSources` commands, queries, handlers, validators, DTOs, and
  provider-neutral application contracts.
- `services/Diten.Platform/src/Diten.Platform.Persistence/**` for MongoDB
  collections, indexes, and repository implementations.
- `services/Diten.Platform/src/Diten.Platform.API/**` for controller and
  dependency-registration changes needed by `/api/hris-sources`.
- `services/Diten.Platform/tests/**` or the existing repo-standard
  `Diten.Platform` test project paths for unit/integration/security tests.
- `gateway/Diten.ApiGateway/**` only as an integration-agent task if Gateway
  exposure is required; this pack does not authorize direct route edits by the
  module implementer.

## 6. Protected Paths

- `.antigravity/**`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- `gateway/Diten.ApiGateway/**/ocelot.json` unless assigned to
  integration-agent in an approved implementation task.
- `services/Diten.MdmService/**`
- `services/Diten.DevEnablementService/**`
- `services/Diten.EnterpriseStrategyService/**`
- `services/Diten.AuthService/**` except future approved auth/RBAC integration
  contracts.
- `services/Diten.Platform/**` outside the MOD-0251 first-slice backend/API
  scope defined in Section 5.
- Other domains' `execution/domains/**` paths unless EA approves a domain move.

## 7. Dependencies

| Module | Relationship | Readiness / gate |
|---|---|---|
| `DCP-004` | Source Delivery Capability Pack | This module pack is based on DCP-004. |
| `DCP-007` | Duplicate/superseded candidate | Same canonical module/capability; reconcile before treating it as independent authority. |
| `DCP-008` | HR & TEP roadmap | Classifies MOD-0251 as R0-D source readiness prerequisite. |
| `MOD-0002` Interface Registry | Contract metadata | Required before standardized public integration contracts are published. |
| `MOD-0012` Secrets & Configuration Vault | Secret reference storage | Raw credentials forbidden; only secret references in MOD-0251. |
| `MOD-0018` RBAC / ABAC Authorization | Permission enforcement | Required before API/UI execution. |
| `MOD-0021` Audit Trail Service | Access/change/sync audit | Required for create/update/archive/validation/sync events. |
| `MOD-0026` Scheduler / Job Orchestration | Scheduled pull or retry trigger | Required only if scheduled sync is approved. |
| `MOD-0035` Event Bus / Message Queue | Outbox/event propagation | Use only after readiness gates; no direct broker coupling. |
| `MOD-0037` Integration Monitoring & Reconciliation | Monitoring and assurance | Deferred dependency; link through contracts when available. |
| `MOD-0279` Payroll Engine | Separate external payroll SoR | Payroll data and payroll calculations must not be folded into MOD-0251. |
| `MOD-0280` Time & Attendance | Separate time/attendance external provider | Attendance, leave, roster, and schedule source readiness remain outside MOD-0251. |
| `MOD-0281` Payroll Integration & Governance | Payroll governance | Payroll integration controls, approvals, and reconciliation remain outside MOD-0251. |
| `MOD-0288` Organization, Person & Position Directory | Internal directory consumer | Consumes governed HRIS contracts; owns internal directory aggregates. |

## 8. Runtime Constraints

- Production runtime is authorized only for the approved provider-neutral
  backend/API slice in `Diten.Platform`.
- First-slice records are tenant-owned and must set `TenantId` server-side.
- DTO/request payloads must never accept `TenantId`.
- Cross-tenant access must fail closed with 404 where a resource lookup is
  attempted.
- All records use soft delete through `IsDeleted` and `DeletedAt`.
- `Version` is reserved for technical concurrency; use
  `MappingProfileVersion` for mapping semantics.
- Raw provider credentials, access tokens, refresh tokens, secrets, raw HRIS
  payloads, and unredacted PII must not be persisted in MOD-0251 records.
- Provider-specific behavior must live behind future approved adapter contracts;
  this first slice stays provider-neutral and must not implement Workday,
  SuccessFactors, Oracle HCM, or other vendor adapters.
- Use `BaseEntity` for tenant-aware records and `platform.hris-sources.*`
  permissions.

## 9. Layout & Shell Contract

- `shell: none`.
- No Razor view, frontend route, sidebar item, DataTable page, or layout is
  authorized by this first slice.
- If an admin UI is approved later, the pack must be revised with
  `shell: platform-admin`, explicit `Layout = "_LayoutPlatformAdmin"`, a
  concrete `form_field_count`, and `golden_reference: slim` or `compact`.
- If a tenant UI is approved later, the pack must be revised with
  `shell: tenant`, explicit `Layout = "_LayoutTenantShell"`, tenant-area paths,
  7-language localization, and the correct Golden Reference decision.

## 10. Backend File Convention

Backend files are authorized only for the provider-neutral first slice under
`Diten.Platform`. The implementation must follow the repository standard:

```text
services/Diten.Platform/src/Diten.Platform.Application/Features/HrisSources/
├── Commands/
├── Queries/
├── Handlers/
│   ├── CommandHandlers/
│   └── QueryHandlers/
├── Validators/
└── HrisSourceModels.cs
```

Naming rules:

- Commands: `{Verb}HrisSourceCommand`
- Queries: `GetHrisSource{Qualifier}Query`
- Handlers: `{Verb}HrisSourceHandler`; no `CommandHandler`, `QueryHandler`, or
  `RequestHandler` suffix.
- Validators: `{Verb}HrisSourceValidator`; no `Command` suffix.
- One public class/record per file.
- Controllers contain no business logic; handlers return `Response<T>`.
- Provider HTTP/API calls must not be implemented in this slice. Future
  provider-specific calls require separately approved adapter packs.

## 11. Frontend File Contract

- `golden_reference: none`.
- No frontend file set is authorized in this first slice.
- No DataTable contract applies until a UI is approved.
- If a Platform Admin UI is approved later, the revised pack must define the
  Slim/Compact file set under `frontend/Diten.Web/Views/Platform/HrisSources/`
  and the matching `wwwroot/assets/js/Platform/HrisSources/` files.
- If lookup/select fields are introduced in UI, the revised pack must document
  the exact lookup source, no hardcoded fallback, and same-origin proxy/Gateway
  usage.

## 12. Validation Rules

| Field | Required | Format/Rule | DB-level | Pre-check |
|---|---|---|---|---|
| `Code` | Yes | Trim, uppercase, max 64, stable per tenant | Unique with `TenantId`, `IsDeleted=false` | Duplicate source code |
| `DisplayName` | Yes | Trim, max 200 | - | Not empty |
| `ProviderKind` | Yes | Known provider enum or approved `Other`; no provider-specific branching in core | Indexed | Enum validation |
| `ExternalTenantKey` | Conditional | Max 128; required only for provider partitioning | - | No secret/token pattern |
| `ConnectionProfileReference` | Yes | Secret/config reference key, max 256 | Indexed | Must not contain raw credential material |
| `LifecycleState` | Yes | `Draft`, `Active`, `Archived`, `Suspended` | Indexed | Activation rules |
| `SyncMode` | Yes | `Manual`, `ScheduledPull`, `Webhook`, `FileImport`, `Disabled` | Indexed | Scheduler/webhook gate |
| `MappingProfileId` | Conditional | Required before activation | - | Same-tenant mapping exists |
| `MappingProfileVersion` | Yes for mapping profile | Semantic/profile version; not named `Version` | Indexed | Format validation |
| `ExternalObjectType` | Yes for identifier map | `Employee`, `Org`, `Job` only in MVP | Compound | Reject payroll/time objects |
| `ExternalObjectId` | Yes for identifier map | Trim, max 256 | Unique with source and type | Duplicate external id |
| `InternalReferenceId` | Optional | MOD-0288 reference only | - | Same-tenant MOD-0288 validation when set |
| `CursorReference` | Optional | Reference/cursor metadata only, no sensitive payload | - | Redaction check |
| `ErrorSummary` | Optional | Redacted, max 1000 | - | No secrets/raw PII |

## 13. Failure Path to Verify

- **Duplicate source code**
  - Expected: 409 response; no new source profile is created.
- **Missing required display name or provider kind**
  - Expected: 400 validation response before handler persistence.
- **Raw secret submitted in connection reference**
  - Expected: 400 validation response; raw credential value is rejected and not
    logged.
- **Cross-tenant source profile lookup**
  - Expected: 404 fail-closed response; no data leakage.
- **Archive source with active dependent mapping**
  - Expected: 409 unless archival policy explicitly allows deactivation.
- **Activate source without mapping profile**
  - Expected: 400 or 409 based on handler contract; source remains non-active.
- **MOD-0288 reference missing or tenant-mismatched**
  - Expected: fail-closed validation; identifier map remains unmapped or
    conflict state.
- **Provider validation timeout**
  - Expected: controlled failure response; no raw provider exception in API,
    audit, or logs.
- **Unauthorized actor**
  - Expected: 403 response; no create/update/archive/validation action occurs.
- **Concurrency conflict**
  - Expected: 409 response; no silent overwrite.

## 14. Authorization Convention

Approved first-slice convention in `Diten.Platform`:

- Policy: `[Authorize(Policy = "PlatformActor")]`
- Permission prefix: `platform.hris-sources`
- Actor types: `platform_admin`; future tenant/HR admin access requires an
  explicit EA/security decision and pack revision.

First-slice permissions:

- `platform.hris-sources.read`
- `platform.hris-sources.create`
- `platform.hris-sources.update`
- `platform.hris-sources.archive`
- `platform.hris-sources.validate-connection`
- `platform.hris-sources.update-mapping`
- `platform.hris-sources.read-health`

If EA later assigns a tenant HR service, permission keys must change through a
separate approved governance revision.

## 15. Gateway / API Routing Decision

Decision: Gateway route changes, if required, are **integration-agent only**.

- Frontend and external consumers use Gateway port 5000; browser JS must not
  call service port 5057 directly.
- Explicit routes must be added by integration-agent only.
- Resource path: `/api/hris-sources`.
- Both `/api/hris-sources` and `/api/hris-sources/{everything}` route pairs
  must include `GET`, `POST`, `PUT`, `PATCH`, `DELETE`, and `OPTIONS` as
  applicable.
- `gateway/Diten.ApiGateway/**/ocelot.json` remains protected and must not be
  edited by this module pack authoring task.

## 16. Acceptance Criteria

1. DCP-002 preflight for `MOD-0251` with exact canonical Blueprint name passes.
2. Module pack frontmatter uses exact canonical name:
   `HRIS (Workday/SuccessFactors/Oracle HCM) [External SoR]`.
3. Pack status is `ready-for-dev` for the approved provider-neutral backend/API
   first slice only.
4. DCP-004 is recorded as the source Delivery Capability Pack.
5. DCP-007 is recorded as duplicate/superseded candidate requiring
   reconciliation, not a separate implementation authority.
6. MOD-0251 / MOD-0288 boundary is enforced: MOD-0251 may store external
   identifiers, mapping metadata, provenance, checkpoints, and health snapshots,
   but must not own MOD-0288 directory aggregates.
7. MOD-0279, MOD-0280, and MOD-0281 remain separate dependencies; payroll,
   time, and attendance object types are rejected by validation.
8. API handlers return `Response<T>` through the repository controller pattern;
   controllers contain no business logic.
9. Create/update/archive/get/list endpoints enforce
   `platform.hris-sources.*` permissions and return 403 for unauthorized actors.
10. TenantId is set server-side only, never accepted from DTO payloads, and
    cross-tenant resource lookup returns 404.
11. Duplicate active source `Code` within a tenant returns 409 and does not
    create a second record.
12. Validators reject missing required fields, invalid provider kind, raw secret
    material in `ConnectionProfileReference`, raw payload fields, and unsupported
    payroll/time-attendance object types.
13. All persisted records use `BaseEntity`, soft delete, and do not redefine
    inherited base fields.
14. No raw HRIS payloads, access tokens, refresh tokens, secrets, provider stack
    traces, or unredacted PII are persisted, logged, returned by API, or written
    to audit events.
15. Provider validation is provider-neutral only; no Workday, SuccessFactors,
    Oracle HCM, payroll, or time-attendance adapter is implemented.
16. Sync checkpoint and source health APIs expose redacted metadata only.
17. MOD-0288 references are validated fail-closed for same tenant when provided;
    unresolved references remain unmapped or conflict state.
18. Gateway route work is recorded only as an integration-agent task and no
    direct `ocelot.json` edit is made by the module implementer.
19. No UI, Razor, JavaScript, sidebar, localization, or DataTable files are
    created by this first slice.
20. `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug`
    and the relevant `Diten.Platform` unit/integration/security tests pass.

## 17. Test Expectations

Implementation must add or update the relevant `Diten.Platform` tests for this
first slice:

- DCP-002 preflight remains passing.
- Build passes for `Diten.Platform`.
- Unit tests cover validation for source profile, mapping profile, external id
  map, and checkpoint rules.
- Integration tests cover tenant isolation, soft delete, duplicate source code,
  archive behavior, and concurrency conflict.
- Security tests verify no raw credentials, tokens, or unredacted PII appear in
  API responses, audit events, or logs.
- Authorization tests cover all first-slice permissions and unauthorized paths.
- MOD-0288 integration tests verify same-tenant reference validation and
  fail-closed behavior.
- If Gateway routes are approved, route smoke tests verify Gateway 5000 and
  `OPTIONS` support.
- UI tests, DataTable verifier, and localization/RESX gates are not applicable
  because `shell: none` and `golden_reference: none`.

## 18. Ready-for-dev Checklist

- [x] DCP-002 preflight passes for exact canonical name.
- [x] DCP-004 read and used as the source DCP.
- [x] DCP-007 duplicate/superseded candidate reviewed and recorded.
- [x] MOD-0251 / MOD-0288 boundary documented.
- [x] MOD-0279, MOD-0280, and MOD-0281 dependency boundaries documented.
- [x] Production implementation is limited to the approved provider-neutral
      backend/API first slice.
- [x] DCP-002 preflight proves the canonical ID/name against Blueprint and
      registry with no collision.
- [x] `platform-shared-services` accepted as first-slice governance/runtime
      owner.
- [x] `Diten.Platform` accepted as first-slice runtime service.
- [x] First slice has no UI: `shell: none`, `golden_reference: none`,
      `form_field_count: 0`.
- [x] Sync strategy is provider-neutral backend/API contract only; no pull,
      webhook, file import, scheduled sync adapter, or provider integration.
- [x] Payload policy for first slice forbids raw HRIS payload persistence.
- [x] MOD-0288 transformation and conflict resolution are limited to
      provider-neutral same-tenant reference validation.
- [x] Provider-specific adapter identities remain unallocated until separately
      approved.

Open blockers for this ready-for-dev first slice: none.

## 19. Implementation Notes

- DCP-004 is the source Delivery Capability Pack for this module pack.
- DCP-007 appears to describe the same MOD-0251 / HRIS Source Readiness
  capability. It has the same canonical module ID and canonical name, and its
  frontmatter/body status wording is internally inconsistent (`status:
  approved` in frontmatter, body says `draft`). Treat DCP-007 as duplicate or
  superseded until the portfolio owner reconciles it.
- DCP-008 classifies MOD-0251 as R0-D HR & TEP source-readiness prerequisite.
- Current repo domain inventory contains no HR/workforce domain. User decision
  accepts PSS as first-slice governance/runtime owner; future domain bootstrap is
  a follow-up, not a blocker for this slice.
- `MOD-0251` is not present as a registry table row at authoring time, but the
  DCP-002 preflight proves the ID and canonical name against Blueprint with no
  collision.
- `MOD-0288` registry row is `done`; it remains the internal directory owner.
- Blueprint Dependencies row 1272 states MOD-0288 depends on MOD-0251.
- Blueprint SoR_Map states HRIS remains upstream employee/job SoR while the
  platform directory owns routing/binding references.
- Production implementation is authorized only for the provider-neutral
  backend/API slice defined here. Route work remains integration-agent only; UI,
  provider adapters, raw HRIS payload persistence, payroll/time attendance,
  registry update, and domain bootstrap are outside this pack's implementation
  scope.

## 20. Follow-up Items

- Reconcile DCP-004 and DCP-007 so only one HRIS Source Readiness DCP is treated
  as authoritative.
- Add or approve MOD-0251 registry row with exact canonical name and owner
  domain as a governance cleanup item.
- Bootstrap approved HR/workforce domain if EA chooses a non-PSS owner later.
- Revisit final long-term service model if EA moves the capability out of
  `Diten.Platform`.
- Prepare MOD-0288 consumer-integration follow-up after source contract and
  conflict policy are approved.
- Prepare provider-specific adapter packs separately; do not invent child IDs in
  this pack.
- Define HRIS payload retention, redaction, and legal-hold interaction after
  MOD-0030 governance is settled.
