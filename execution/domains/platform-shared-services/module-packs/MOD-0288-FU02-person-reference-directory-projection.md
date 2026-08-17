---
id: MOD-0288-FU02
parent_id: MOD-0288
name: Person Reference Directory Projection
friendly_name: Person Reference Directory
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: BaseEntity
status: ready-for-dev
owner: enterprise-architect / platform-team / integration-ops
branch: feature/pss/mod-0288-fu02-person-reference-directory-projection
started: 2026-08-10
target: 2026-08-31
form_field_count: 0
parent_canonical_source: docs/System Capability & Implementation Blueprint - master 5.xlsx#Blueprint_Data
source_dcp: execution/portfolio/delivery-capability-packs/DCP-006-org-directory-expansion-governance.md
source_module_pack: execution/domains/platform-shared-services/module-packs/MOD-0288-organization-person-position-directory.md
upstream_source_pack: execution/domains/platform-shared-services/module-packs/MOD-0251-hris-external-sor.md
---

# MOD-0288-FU02 - Person Reference Directory Projection

> **Execution status:** `ready-for-dev` for the approved first backend/API
> contract slice. Runtime implementation is authorized only inside the repo
> scope in Section 5 and only for the minimal Person Reference Directory /
> HRIS-sourced person reference projection described here.

> **DCP-002 parent/FU gate evidence:** `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0288-FU02 --name "Person Reference Directory Projection" --parent MOD-0288` returned `OK  MOD-0288-FU02: proven against Blueprint/registry.`

> **Parent boundary:** MOD-0288 remains the completed source for Organization
> Unit, Position, Position Assignment, effective-dated assignment, tenant
> ownership, soft delete, and minimal derived manager-chain inputs. This
> follow-up does not rewrite the completed parent module pack.

## 1. Module Summary

MOD-0288-FU02 prepares a minimal tenant-scoped Person Reference Directory
projection under Platform Shared Services. The projection exists so HR/HCM,
TEP, workflow, IAM, payroll integration governance, and other downstream
modules can reference a person identity anchor without claiming employee,
employment, payroll, time, attendance, candidate, or lifecycle ownership.

This first slice is a provider-neutral backend/API contract pack. It models
and authorizes implementation of only minimum routing/reference metadata:

- HRIS source reference linkage to MOD-0251.
- Person/org/position same-tenant reference validation.
- HRIS-sourced person identity correlation metadata.
- Duplicate active person reference guard.
- Tenant-scoped API behavior and persistence expectations.

Runtime code is authorized only for this provider-neutral backend/API slice.
No UI, gateway direct edit, provider adapter, HR lifecycle, employment master,
payroll/time/attendance, or TEP identity work is authorized.

## 2. Ownership and Boundaries

**Owned by this follow-up pack:**

- Minimal Person Reference Directory projection contract.
- HRIS-sourced person reference correlation metadata.
- Same-tenant validation rules for HRIS source, Organization Unit, Position,
  and existing person reference records.
- Tenant-scoped duplicate active person reference detection.
- Backend/API contract implementation readiness for the Diten.Platform first
  slice.

**Explicitly not owned:**

- Employee master, employment record, HR lifecycle, compensation, performance,
  benefits, leave, recruitment, or TEP candidate identity.
- Payroll source ownership, time/attendance ownership, payslips, bank/tax data,
  payroll calculation, or raw provider data.
- Provider adapters, background sync, webhooks, gateway route edits, UI pages,
  DataTables, localization, menu entries, or frontend shells.

## 3. Owned Objects

Authorized runtime objects for the first backend/API slice:

- **PersonReferenceProjection** - tenant-owned minimal person reference anchor.
  Stores only routing-safe identity metadata and references to validated source
  contracts.
- **PersonReferenceExternalCorrelation** - tenant-owned HRIS external identity
  correlation metadata. Stores external reference keys and correlation state,
  never raw HRIS payload.
- **PersonReferenceDirectoryHealthSnapshot** - optional tenant-owned operational
  metadata for local projection health. This is not MOD-0037 monitoring and
  must not duplicate integration monitoring infrastructure.
- **IPersonReferenceDirectoryRepository** - tenant-aware repository contract for
  active lookups, duplicate checks, archive, and correlation queries.

No ownership is granted over MOD-0251, MOD-0279, MOD-0280, MOD-0281, HR/HCM
native modules, TEP modules, or MOD-0037 monitoring infrastructure.

## 4. Entity Fields

All planned records use `BaseEntity` behavior in Diten.Platform:
`Id`, server-side `TenantId`, `IsDeleted`, `DeletedAt`, `CreatedAt`,
`CreatedBy`, `UpdatedAt`, `UpdatedBy`, and version/concurrency fields where
the service standard provides them.

### PersonReferenceProjection

| Field | Required | Rule |
|---|---:|---|
| `Code` | Yes | Stable tenant-scoped reference code, trimmed/uppercased, unique where `IsDeleted=false`. |
| `ReferenceDisplayName` | Yes | Minimal routing label only; no PII-heavy profile fields. |
| `HrisSourceProfileId` | Yes | MOD-0251 HRIS source profile reference; same-tenant validation required before `Validated`. |
| `PrimaryExternalCorrelationId` | Conditional | Required before `Validated`; points to active same-tenant correlation record. |
| `OrganizationUnitId` | Optional | MOD-0288 Organization Unit reference; same-tenant validation when present. |
| `PositionId` | Optional | MOD-0288 Position reference; same-tenant validation when present. |
| `ReferenceState` | Yes | `Deferred`, `Validated`, `Suspended`, or `Archived`. |
| `SourceContractVersion` | Yes | Local contract-version metadata; stale values are rejected before validation. |
| `CorrelationKey` | Yes | Stable hash/key derived from allowed source reference metadata, not raw payload. |
| `LastValidatedAt` | Optional | Set after same-tenant validation succeeds. |
| `ValidationFailureReason` | Optional | Redacted summary only. |

### PersonReferenceExternalCorrelation

| Field | Required | Rule |
|---|---:|---|
| `PersonReferenceProjectionId` | Yes | Same-tenant PersonReferenceProjection reference. |
| `HrisSourceProfileId` | Yes | Same-tenant MOD-0251 source reference. |
| `ExternalObjectType` | Yes | `Employee` only for this slice. `Org`, `Job`, payroll, time, attendance, candidate, and provider-specific types are rejected. |
| `ExternalObjectReference` | Yes | External HRIS identifier reference only; no raw payload. |
| `CorrelationKey` | Yes | Unique active correlation key per tenant/source/object reference. |
| `CorrelationState` | Yes | `Deferred`, `Validated`, `Conflict`, `Suspended`, or `Archived`. |
| `SourceContractVersion` | Yes | Local contract-version metadata. |
| `LastSeenAt` | Optional | Source observation timestamp, if provided as safe metadata. |

### PersonReferenceDirectoryHealthSnapshot

| Field | Required | Rule |
|---|---:|---|
| `SnapshotKey` | Yes | Unique active tenant-scoped key. |
| `ProjectionCount` | No | Aggregate count only. |
| `ValidatedCount` | No | Aggregate count only. |
| `ConflictCount` | No | Aggregate count only. |
| `LastCheckedAt` | No | Timestamp for local projection health. |
| `RedactedStatus` | No | No raw HRIS payload, PII-heavy fields, secrets, stack traces, or provider responses. |

## 5. Repo Scope

The first backend/API slice is authorized and limited to:

- `services/Diten.Platform/src/Diten.Platform.API/**`
- `services/Diten.Platform/src/Diten.Platform.Application/**`
- `services/Diten.Platform/src/Diten.Platform.Domain/**`
- `services/Diten.Platform/src/Diten.Platform.Persistence/**`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/**`
- `services/Diten.Platform/tests/**`

Expected feature folder name for the slice:

```text
services/Diten.Platform/src/Diten.Platform.Application/Features/PersonReferenceDirectory/
```

Authorized implementation scope includes only tenant-scoped backend/API,
application, domain, persistence, infrastructure, and tests needed for
`PersonReferenceDirectory`. Any gateway exposure is an integration-agent task
only. No frontend scope is opened by this pack.

## 6. Protected Paths

- `.antigravity/**` - global engineering system; do not edit for this pack.
- `execution/domains/platform-shared-services/module-packs/MOD-0288-organization-person-position-directory.md` - completed parent pack remains immutable.
- `execution/domains/platform-shared-services/module-packs/MOD-0251-hris-external-sor.md` - upstream source pack remains unchanged.
- `execution/domains/platform-shared-services/module-packs/MOD-0279-payroll-engine-external-sor.md` - payroll source pack out of scope.
- `execution/domains/platform-shared-services/module-packs/MOD-0280-time-attendance-ukg-kronos-external-provider.md` - time/attendance pack out of scope.
- `execution/domains/platform-shared-services/module-packs/MOD-0281-payroll-integration-governance.md` - payroll integration governance pack out of scope.
- `frontend/Diten.Web/**` - no UI, Razor, JavaScript, DataTable, menu, or RESX work.
- `gateway/Diten.ApiGateway/**/ocelot.json` - integration-agent owned.
- `services/Diten.AuthService/**`
- `services/Diten.DevEnablementService/**`
- `services/Diten.EnterpriseStrategyService/**`
- `execution/domains/master-data-management/**`
- `execution/domains/developer-enablement/**`

## 7. Dependencies

| Dependency | Role | Decision |
|---|---|---|
| `MOD-0288` Organization, Person & Position Directory | Parent directory baseline | Completed parent remains unchanged; Org/Position same-tenant validation must use current contracts/repositories. |
| `MOD-0251` HRIS External SoR | HRIS source profile and external identifier map boundary | Required for source reference linkage; no provider adapter or raw payload use. |
| `MOD-0021` Audit Trail | Audit evidence consumer | Runtime slice must emit or reference audit evidence per existing standards when available. |
| `MOD-0032` API Gateway | External route exposure | Integration-agent owned only; direct `ocelot.json` edit forbidden. |
| `MOD-0037` Integration Monitoring & Reconciliation | Future monitoring handoff | Not a first-slice blocker; local health metadata cannot become generic monitoring. |
| HR/HCM native domain | Future employee/employment owner | Not required for minimal routing projection; no HR master ownership is granted here. |
| TEP native domain | Future candidate/talent owner | Out of scope; no candidate identity or talent profile fields. |

## 8. Runtime Constraints

- First slice is backend/API only in Diten.Platform.
- `TenantId` is resolved server-side only and is never accepted from request DTOs.
- Cross-tenant lookup, update, archive, correlation, validation, and health
  operations fail closed with 404.
- HRIS source/profile references must be same-tenant validated through the
  available MOD-0251 repository/contract; if the validator/read contract is not
  available, fail closed with 404.
- Duplicate active `Code` within a tenant returns 409.
- Duplicate active correlation key for the same tenant/source/object reference
  returns 409.
- Soft delete uses `IsDeleted` plus `DeletedAt`.
- Person references cannot become `Validated` unless HRIS source and required
  correlation records are same-tenant validated.
- OrganizationUnit and Position references must be same-tenant, active, and
  referenceable before the person projection is treated as `Validated`.
- Validators reject raw HRIS payload, raw credential/token/secret material,
  PII-heavy profile fields, bank/tax/payroll/payslip fields, biometric or
  geolocation fields, provider adapter/client fields, and TEP candidate fields.
- External object ownership is limited to HRIS-sourced `Employee` reference
  metadata. No payroll/time/attendance/candidate ownership object type is valid.

## 9. Layout & Shell Contract

`shell: none`.

No Razor layout is selected. No `_LayoutPlatformAdmin`, `_LayoutTenantShell`,
DataTable golden reference, menu entry, JavaScript, RESX, or frontend artifact
is authorized by this pack.

## 10. Backend File Convention

Implementation must follow the repo CQRS conventions used in Diten.Platform:

```text
services/Diten.Platform/src/Diten.Platform.Application/Features/PersonReferenceDirectory/
├── Commands/
├── Queries/
├── Handlers/
│   ├── CommandHandlers/
│   └── QueryHandlers/
├── Validators/
└── PersonReferenceDirectoryModels.cs
```

Controller expectations for the slice:

- Thin `PersonReferenceDirectoryController`.
- `CustomBaseController` plus MediatR.
- `Response<T>` envelope.
- No business logic in the controller.
- No DTO field that accepts `TenantId`.

## 11. Frontend File Contract

Frontend is out of scope.

- `golden_reference: none`.
- `form_field_count: 0`.
- No Razor views, JavaScript, DataTables, RESX files, menu entries, layout
  changes, or frontend routes.
- UI/DataTable/l10n verifiers are N/A for this pack.

## 12. Validation Rules

- `Code` is required, normalized, max 64, unique per tenant where active.
- `ReferenceDisplayName` is required, max 160, and limited to a routing-safe
  label. It must not carry PII-heavy profile data.
- `HrisSourceProfileId` is required and must resolve to an active same-tenant
  MOD-0251 source before validation.
- `ExternalObjectType` accepts only `Employee`.
- `ExternalObjectReference` is required, max 160, and must not contain raw JSON,
  XML, payload fragments, credentials, access tokens, refresh tokens, secrets,
  bank/tax/payroll/payslip markers, biometric markers, or geolocation markers.
- `OrganizationUnitId`, when provided, must resolve to an active same-tenant
  MOD-0288 Organization Unit.
- `PositionId`, when provided, must resolve to an active same-tenant MOD-0288
  Position.
- `PositionId`, when provided with `OrganizationUnitId`, must belong to or be
  valid for the same tenant/org context per existing MOD-0288 repository rules.
- `ReferenceState=Validated` requires validated HRIS source, active correlation,
  and all provided org/position references to pass same-tenant validation.
- Unresolved references remain `Deferred` or fail closed. They are never treated
  as `Validated`.
- Soft delete sets `IsDeleted=true` and `DeletedAt` to the archive timestamp.

## 13. Failure Path to Verify

The later implementation must verify:

- Cross-tenant person reference lookup returns 404.
- Cross-tenant HRIS source reference returns 404.
- Cross-tenant Organization Unit or Position reference returns 404.
- Missing HRIS source blocks `Validated` state.
- Missing or inactive correlation blocks `Validated` state.
- Duplicate active `Code` returns 409.
- Duplicate active correlation key returns 409.
- Raw HRIS payload fields are rejected and not persisted.
- Raw credential/token/secret markers are rejected and not persisted.
- PII-heavy fields such as DOB, national ID, home address, bank, tax, payroll,
  payslip, biometric, or geolocation markers are rejected and not persisted.
- TEP candidate or talent identity fields are rejected.
- Archive/soft delete sets both `IsDeleted` and `DeletedAt`.

## 14. Authorization Convention

Permission prefix for the later backend/API slice:

```text
platform.person-reference-directory.*
```

Expected permissions:

| Operation | Permission |
|---|---|
| List/Get | `platform.person-reference-directory.read` |
| Create | `platform.person-reference-directory.create` |
| Update | `platform.person-reference-directory.update` |
| Archive/Delete | `platform.person-reference-directory.archive` |
| Validate references | `platform.person-reference-directory.validate` |
| Manage correlation | `platform.person-reference-directory.correlation.manage` |
| Read health metadata | `platform.person-reference-directory.health.read` |

Unauthorized or missing-permission requests must fail per JWT/RBAC standards.

## 15. Gateway / API Routing Decision

No gateway file change is authorized by this pack.

Candidate internal Platform API routes for this backend/API slice:

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/person-reference-directory` | List tenant person references. |
| `GET` | `/api/person-reference-directory/{id:guid}` | Get a tenant person reference. |
| `POST` | `/api/person-reference-directory` | Create a person reference projection. |
| `PUT` | `/api/person-reference-directory/{id:guid}` | Update safe reference metadata. |
| `POST` | `/api/person-reference-directory/{id:guid}/archive` | Soft-delete/archive a projection. |
| `POST` | `/api/person-reference-directory/{id:guid}/validate` | Re-run same-tenant source/reference validation. |
| `POST` | `/api/person-reference-directory/{id:guid}/correlations` | Add/update HRIS external correlation metadata. |
| `GET` | `/api/person-reference-directory/health` | Read local projection health metadata. |

If the Gateway needs exposure, create an integration-agent task. Do not edit
`gateway/Diten.ApiGateway/**/ocelot.json` from this pack.

## 16. Acceptance Criteria

1. DCP-002 parent/FU preflight passes for `MOD-0288-FU02` under parent
   `MOD-0288`.
2. The completed MOD-0288 parent module pack is not modified.
3. The pack is `status: ready-for-dev` and authorizes only the backend/API
   repo scope listed in Section 5.
4. Scope is limited to minimal Person Reference Directory projection and
   correlation metadata.
5. HRIS source linkage references MOD-0251 without duplicating HRIS source
   ownership or provider adapters.
6. Employee/employment master ownership, HR lifecycle logic, payroll, time,
   attendance, TEP candidate identity, and provider-specific behavior are
   explicitly excluded.
7. TenantId is server-side only and absent from request DTOs.
8. Cross-tenant lookup/update/archive/correlation/validation behavior is
   specified as fail-closed 404.
9. Duplicate active `Code` and duplicate active correlation key are specified as
   409 conflict.
10. Soft delete contract requires both `IsDeleted` and `DeletedAt`.
11. Same-tenant HRIS source/profile, Organization Unit, and Position validation
    gates are enforced before `Validated` state is allowed; missing validators
    fail closed with 404.
12. Raw HRIS payload, raw credentials/tokens/secrets, PII-heavy fields, payroll,
    bank/tax/payslip, biometric, geolocation, provider adapter, and TEP
    candidate fields are forbidden.
13. Gateway ownership is integration-agent only.
14. UI/Razor/JS/DataTable/RESX are N/A and out of scope.
15. Ready-for-dev checklist is complete and open blockers are `none`.

## 17. Test Expectations

Implementation verification must include:

- `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0288-FU02 --name "Person Reference Directory Projection" --parent MOD-0288`
- `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug`
- Targeted Diten.Platform tests for:
  - tenant isolation and cross-tenant 404;
  - duplicate active `Code` 409;
  - duplicate active correlation key 409;
  - missing permission / unauthorized behavior;
  - soft delete `IsDeleted` plus `DeletedAt`;
  - same-tenant MOD-0251 HRIS source validation;
  - same-tenant MOD-0288 Organization Unit validation;
  - same-tenant MOD-0288 Position validation;
  - unresolved references cannot become `Validated`;
  - no raw HRIS payload persistence;
  - no raw secret/token/credential persistence;
  - no PII-heavy, payroll, bank/tax/payslip, biometric, geolocation, provider
    adapter, or TEP candidate fields accepted or persisted.
- UI/DataTable/l10n verifiers: N/A.

## 18. Ready-for-dev Checklist

- [x] DCP-002 parent/FU verifier passes for `MOD-0288-FU02`.
- [x] Parent MOD-0288 boundary preserved.
- [x] Source DCP-006 reviewed for child-pack gate and person reference
  projection decision.
- [x] MOD-0251 HRIS source boundary referenced without provider adapter or raw
  payload scope.
- [x] Backend-only repo scope defined.
- [x] UI/gateway/provider-adapter exclusions defined.
- [x] Acceptance criteria are testable.
- [x] User decision promotes this pack to `ready-for-dev`.
- [x] Status is promoted to `ready-for-dev`.
- [x] Final permission prefix is accepted: `platform.person-reference-directory.*`.
- [x] Minimal PII policy is accepted for `ReferenceDisplayName` and correlation
  metadata.
- [x] Implementation plan confirms available same-tenant validators/repository
  contracts for MOD-0251 and MOD-0288; missing validators must fail closed.

## 19. Implementation Notes

- Keep the first runtime slice provider-neutral and backend/API only.
- Do not store raw HRIS payload or provider responses.
- Do not store full employee profiles, demographic details, national IDs,
  addresses, payroll, bank/tax, payslip, biometric, or geolocation values.
- Do not duplicate MOD-0251 external source profiles or external identifier map
  ownership.
- Do not duplicate MOD-0288 Organization Unit, Position, or Position Assignment
  ownership.
- Use same-tenant repository checks before transitioning any record or
  correlation to `Validated`.
- If a validator or read contract is unavailable, fail closed with 404 or keep
  the reference explicitly `Deferred`; never silently trust a blind reference.
- Preserve backward compatibility with current MOD-0288 APIs and collections.

## 20. Follow-up Items

**Open blockers:** none.

**Waivers / deferrals:**

- MOD-0037 monitoring integration is deferred. Local health metadata is allowed
  only as module-local projection metadata and cannot replace monitoring.
- Gateway route exposure is deferred to integration-agent.
- UI/DataTable/l10n are deferred and require a separate approved slice.
- HR/HCM employee/employment ownership is deferred to future native HR/HCM
  governance.
- TEP candidate/talent identity is deferred to future native TEP governance.

**Safe next step:** `@orchestrator` may start only the Section 5 Diten.Platform
provider-neutral backend/API slice for `MOD-0288-FU02`. Gateway exposure, if
needed, remains an integration-agent task.
