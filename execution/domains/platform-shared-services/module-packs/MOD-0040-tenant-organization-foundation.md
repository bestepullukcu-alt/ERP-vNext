---
id: MOD-0040
name: Tenant Organization Foundation
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: BaseEntity
status: ready-for-dev
owner: platform-team
branch: feature/pss/mod-0040-tenant-organization-foundation
started: ""
target: ""
form_field_count: 0
---

# MOD-0040 — Tenant Organization Foundation

> **Ready-for-dev note:**
> MOD-0040 is ready-for-dev for the explicitly authorized minimal backend-only v1 slice only.
> It does not authorize frontend, gateway, IDataScopeResolver, Tenant User CRUD, Tenant Role CRUD, permission
> evaluation, Legal Entity duplication, role binding, or external Tenant User runtime validation.
>
> **Promotion note - governance reconciliation:** `under-review` -> `ready-for-dev`.
> Reason: minimal backend-only schema reconciliation approved. Tenant User existence validation is explicitly
> deferred behind an AuthService-owned read-only validation contract and must be completed before FU15/runtime
> authorization consumption. Position-role binding is explicitly deferred to a separate Tenant Role integration
> slice. Permission key style is locked.
>
> This pack is the keystone organization master-data dependency referenced by **DCP-001 — Access Governance**
> (CAP-001). Its v1 conceptual boundary is governed by [DCP-001 §11](../../../portfolio/delivery-capability-packs/DCP-001-access-governance.md)
> and ordered as Capability B / step 5 of DCP-001 §8. Production code begins only when **both** the DCP is `approved` /
> `ready-for-execution` **and** this pack is `approved` / `ready-for-dev` (CAP-001 §7 dual gate).

> **Golden Reference decision:** This is a backend organization master-data foundation, not a UI/DataTable module.
> `shell: none`, `golden_reference: none`, `form_field_count: 0`. Razor layout, DataTable verifier, RESX, and the
> frontend file set are N/A for v1. UI is a later follow-up (see OD-MOD-shell).

> **entity_base rationale:** `entity_base: BaseEntity`. MOD-0040 records are **tenant-owned** and hosted in
> `Diten.Platform`; per the module-pack standard entity_base table, tenant-aware records in `Diten.Platform`
> use the concrete `BaseEntity` class (not `GlobalEntity`). `TenantId` is resolved server-side and is **not**
> present in any request DTO. Owning service decision is resolved as `Diten.Platform` (see OD-MOD-svc).

> **Ready-for-dev scope guard:** This pack authorizes only backend implementation planning for the locked v1 scope
> after explicit implementation handoff. Production code must still wait for the orchestrator/add-module gate.

## 1. Module Summary

MOD-0040 is the **source of truth for tenant organization structure**: the Organization Unit tree, Position, and
Position Assignment (effective-dated). It exists so that organization-aware authorization (the real data-scope
resolver, **MOD-0018-FU15**) and downstream tenant business modules (CRM / Track-H) can obtain correct, auditable
organization structure **without** each module re-implementing it. **Legal Entity is not owned here** — it is an
external **MDM-owned** master record that MOD-0040 consumes by read-only `LegalEntityId` reference (see §2 / §7).

MOD-0040 is **Capability B** in DCP-001 §4. It is consumed by, but does not own:
- permission evaluation (**MOD-0018**),
- the tenant authorization context (**MOD-0018-FU12**, already merged),
- data-scope resolution (**MOD-0018-FU15**),
- business-module row-level enforcement (CRM / Track-H).

This pack is **ready-for-dev** for the locked backend-only v1 slice; there is no UI and no field-level access model.

## 2. Ownership and Boundaries

**In scope — owned by MOD-0040 (v1 boundary):**

- Organization Unit tree
- Position
- Position Assignment
- effective-dated Position Assignment
- tenant ownership (for MOD-0040-owned records)
- soft-delete / archival semantics (for MOD-0040-owned records)
- minimal derived Manager Chain inputs and contract

**External dependency (consumed, not owned):**

- the **MDM-owned Legal Entity capability** — the Legal Entity master record lives in MDM
  (`MOD-0220` reserved for the MDM Legal Entity capability; authoritative Enterprise Blueprint repository
  migration pending).
- a **read-only `LegalEntityId` reference / lookup-validation contract** to that capability — see OD-MOD-le-contract (§19).

**Out of scope (explicit exclusions):**

- duplicate Legal Entity aggregate
- Legal Entity persistence
- Legal Entity lifecycle
- Legal Entity API
- Legal Entity UI
- Country master data ownership
- MDM business-country catalog design
- permission storage
- permission evaluation — remains owned by MOD-0018
- IDataScopeResolver algorithm — owned by MOD-0018-FU15
- business-module query enforcement
- partner_admin runtime security policy
- Territory
- matrix organization
- multiple concurrent reporting lines
- delegation / substitution ownership
- UI screens in v1

These exclusions mirror DCP-001 §11 and §12. Legal Entity ownership, permission evaluation, and data-scope
resolution are deliberately kept outside MOD-0040 so the organization-structure owner never duplicates the MDM
Legal Entity system-of-record and never becomes a policy engine.

## 3. Owned Objects

Aggregates owned by MOD-0040 v1:

- **Organization Unit** — node in the tenant Organization Unit tree (parent/child hierarchy).
- **Position** — a scope seat within the organization. Position-role binding is explicitly deferred; Position is
  not a permission store or evaluator (see DCP-001 AD-1).
- **Position Assignment** — effective-dated binding of a user to a Position (DCP-001 AD-7).
- **Manager Chain** — **derived**, minimal; not a separately authored hierarchy (DCP-001 AD-3). Derivation depth/strategy is OD-4.

**Not owned here — external reference:** **Legal Entity** is an MDM-owned master record. MOD-0040 stores only a
read-only `LegalEntityId` reference to it (e.g., an Organization Unit's owning Legal Entity); it does **not**
author a Legal Entity aggregate, persistence, lifecycle, API, or UI. See §2 and OD-MOD-le-contract (§19).

Frontend routes: **none** in v1. Concrete backend endpoint and permission proposals are recorded in §14 / §15 for
review, not implementation authorization.

## 4. Entity Fields

> Field-level schema is reconciled for review. It is not implementation authorization until this pack is promoted
> to `ready-for-dev`.

**Entity base reconciliation:** MOD-0040 should use `Diten.Platform.Common.Persistence.TenantScopedEntity` /
`BaseEntity` behavior for tenant-owned records: `Id`, `TenantId`, `IsDeleted`, `DeletedAt`, `CreatedAt`,
`CreatedBy`, `UpdatedAt`, `UpdatedBy`, and `Version`. The older `Diten.Platform.Domain.Common.BaseEntity` is not the target for
new tenant-owned Platform entities.

**Archive marker:** Diten.Platform uses business lifecycle/status markers such as `FeatureDefinitionStatus.Archived`
for archive semantics while technical soft-delete stays separate as `IsDeleted`. MOD-0040 v1 uses a compact
business archive marker (`IsArchived`) on Organization Unit and Position because there is no repo-standard
Organization lifecycle enum to reuse. No new broad lifecycle enum is introduced.

### Organization Unit

| Field | Required | Notes |
|---|---:|---|
| `Id` | Yes | Canonical entity identifier from repo-standard `BaseEntity`. |
| `TenantId` | Yes | Server-side tenant context only; never request payload. |
| `Code` | Yes | Normalized; unique within tenant for non-deleted records. |
| `Name` | Yes | Display label. |
| `LegalEntityId` | Yes | MDM MOD-0220 read-only reference; validation contract in §7. |
| `ParentOrganizationUnitId` | No | `null` means root. Parent must be same tenant and same `LegalEntityId`. |
| `IsArchived` | Yes | Business archive marker; separate from technical soft-delete. |
| Base audit / soft-delete fields | Yes | `IsDeleted`, `DeletedAt`, `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`, `Version`. |

Rules:

- `LegalEntityId` is required and validated through the MDM MOD-0220 contract.
- Parent is optional; root nodes use `ParentOrganizationUnitId = null`.
- Parent, when present, must be same tenant and same `LegalEntityId`.
- Parent cycle is rejected; cross-tenant parent lookup fails closed.
- `Code` is unique per tenant for non-deleted Organization Units.
- No implicit `LegalEntityId` inheritance in v1.
- Cross-Legal-Entity tree support is deferred.

### Position

| Field | Required | Notes |
|---|---:|---|
| `Id` | Yes | Canonical entity identifier from repo-standard `BaseEntity`. |
| `TenantId` | Yes | Server-side tenant context only; never request payload. |
| `Code` | Yes | Normalized; unique within tenant for non-deleted records. |
| `Name` | Yes | Display label. |
| `OrganizationUnitId` | Yes | Must resolve to same-tenant, non-deleted, non-archived Organization Unit. |
| `ReportsToPositionId` | No | Optional single reporting line. |
| `IsArchived` | Yes | Business archive marker; separate from technical soft-delete. |
| Base audit / soft-delete fields | Yes | `IsDeleted`, `DeletedAt`, `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`, `Version`. |

Rules:

- `OrganizationUnitId` is required and same tenant.
- `ReportsToPositionId` is optional and same tenant when present.
- Self-reference is rejected.
- Reporting cycle is rejected.
- Matrix reporting is out of scope.
- `Code` is unique per tenant for non-deleted Positions.
- Position role-binding fields are not added until the external role-binding contract is reviewed.

### Position Assignment

| Field | Required | Notes |
|---|---:|---|
| `Id` | Yes | Canonical entity identifier from repo-standard `BaseEntity`. |
| `TenantId` | Yes | Server-side tenant context only; never request payload. |
| `PositionId` | Yes | Must resolve to same-tenant, non-deleted, non-archived Position. |
| `UserId` | Yes | External AuthService Tenant User reference; no duplicate User aggregate. |
| `EffectiveFrom` | Yes | Start of interval. |
| `EffectiveTo` | No | End of interval. |
| Base audit / soft-delete fields | Yes | `IsDeleted`, `DeletedAt`, `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`, `Version`. |

Rules:

- Interval semantics are `[EffectiveFrom, EffectiveTo)`.
- `EffectiveTo`, when present, must be greater than `EffectiveFrom`.
- Overlap for the same `PositionId` is rejected.
- One Position may have only one assignee in the same interval.
- One User may hold multiple Positions in the same interval.
- Position must be same tenant.
- User is an external Tenant User reference; MOD-0040 does not duplicate the User aggregate.

### Manager Chain

- Derived on-read from `Position.ReportsToPositionId`.
- No persisted Manager Chain aggregate.
- Single reporting line only.
- Cycle detection is fail-closed.
- Max traversal depth: `32` for v1 proposal; no stronger repo-standard bound was found.
- Materialized chain and deep chain optimization are deferred.

## 5. Repo Scope

**This milestone (governance reconciliation):** allowed governance files are this pack and, if needed, registry /
master-plan status sync files. **No production code, test code, gateway, or frontend file is touched.**

**Future implementation repo scope (conceptual, applies only after `ready-for-dev`):**

- `services/Diten.Platform/src/Diten.Platform.Domain/**` — org master-data aggregates.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/**` — org CQRS features.
- `services/Diten.Platform/src/Diten.Platform.Persistence/**` — MongoDB mappings/indexes.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/**` — DI wiring.
- `services/Diten.Platform/tests/**` — org master-data test suites.

Exact files are finalized at implementation start after `ready-for-dev`; listed here for boundary visibility only.

## 6. Protected Paths

- `.antigravity/**` — global engineering system; not modified without explicit user approval.
- `services/Diten.AuthService/**` — tenant identity / role / permission CRUD is **not** MOD-0040 (Tenant IAM / Track G).
- `services/Diten.Platform.Common/src/.../Authorization/**` — permission evaluation + authorization context (MOD-0018 / FU12); MOD-0040 does not modify it.
- `services/Diten.MdmService/**`, `services/Diten.EnterpriseStrategyService/**`, `services/Diten.DevEnablementService/**` — other domains' services.
- `gateway/Diten.ApiGateway/**/ocelot.json` — integration-agent owned; not modified by this pack.
- `frontend/Diten.Web/**` — no UI in v1.
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` — FROZEN.
- PSS-011 `countries` lookup — Platform provisioning/support only. MOD-0040 must not use it as the Legal Entity
  business-country source and does not own or modify Country master data.

## 7. Dependencies

**External MDM Legal Entity dependency:**
- Legal Entity is owned by MDM MOD-0220 Legal Entity Foundation.
- MOD-0040 consumes only a read-only `LegalEntityId` reference / lookup-validation contract to that capability.
- **OD-MOD-le-contract is resolved.** The locked external dependency is MDM MOD-0220 Legal Entity Foundation,
  using a read-only `LegalEntityId` lookup / validation contract.

Locked validation:

- `LegalEntityId` exists.
- Legal Entity is in the same tenant.
- `LegalEntity.LifecycleStatus == ACTIVE`.
- `LegalEntity.IsDeleted == false`.

Locked return shape:

- `LegalEntityId`
- legal name
- display name
- lifecycle state
- `referenceable = true`

**Tenant User external dependency:**
- `PositionAssignment.UserId` is an external Tenant User reference.
- Canonical identifier: AuthService `User.Id`.
- MOD-0040 does not duplicate the User aggregate.
- MOD-0040 does not connect directly to AuthService persistence.
- MOD-0040 minimal v1 does not call remote User validation.
- MOD-0040 stores `UserId` as a required `Guid` external reference.
- Tenant User existence / same-tenant validation is a deferred integration follow-up behind an AuthService-owned
  stable read-only Tenant User validation contract.
- Critical guard: MOD-0018-FU15 real `IDataScopeResolver` or any other runtime authorization consumer must not
  consume MOD-0040 Position Assignment `UserId` as authoritative until the AuthService-owned Tenant User
  validation contract is added and MOD-0040 Position Assignment integration validation is completed.

**Tenant Role external dependency:**
- AuthService `Role.Id` is the existing Tenant Role identifier.
- Existing repository contract is tenant-scoped: `GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct)`.
- Existing assignment model uses single `RoleId` per `UserRole`; JWT authorization context carries `RoleIds`.
- Position role-binding is deferred from minimal v1.
- No `RoleId`, `RoleIds`, or placeholder role-binding field is added to Position in minimal v1.
- MOD-0040 does not store permissions and does not evaluate permissions.
- Tenant Role remains the role owner. Position-role binding is a separate follow-up slice after a stable
  AuthService-owned Tenant Role read-only validation contract exists.

**Country boundary:**
- PSS-011 `countries` lookup is Platform provisioning/support only.
- MOD-0040 must not use it as the Legal Entity business-country source.
- MDM business-country reference ownership is a separate follow-up outside MOD-0040.

**Upstream / already merged:**
- **MOD-0018-FU12** (Tenant Authorization Context Foundation) — merged; its org context fields
  (`OrgUnitIds`, `PositionIds`, `LegalEntityId`, `Country`, `ManagerChain`) are NoOp until a real resolver
  backed by MOD-0040 exists.

**Downstream (depend on MOD-0040; must not start before its shape is locked):**
- **MOD-0018-FU15** — real `IDataScopeResolver`; queries MOD-0040 org data (replaces NoOp).
- Business-module row-level enforcement (CRM / Track-H) — consumes FU15 output, not MOD-0040 directly.
- **Tenant User** / **Tenant Role** packs — DCP-001 §8 gates these on the MOD-0040 shape being locked. **No IDs are reserved for them in this milestone** (DCP-001 OD-1 / OD-2 remain open).

## 8. Runtime Constraints

- **Tenant ownership mandatory.** Every MOD-0040 record carries `TenantId` (resolved server-side via `BaseEntity`); cross-tenant access fails closed (404), per AGENTS.md §6.
- **Soft-delete / archival.** `IsDeleted` / `DeletedAt` inherited from `BaseEntity`; archival preserves history rather than hard-deleting.
- **Effective-dated Position Assignment** is mandatory in v1 (DCP-001 AD-7). Effective-dating of Organization Unit and Position is deferred.
- **Manager Chain is derived and minimal** (DCP-001 AD-3); on-read derivation is the v1 proposal.
- **Persistence:** MongoDB single instance, multi-tenant logical isolation (AGENTS.md §6). Concrete collections, indexes, and performance constraints are designed at `ready-for-dev`.
- **No permission evaluation, no data-scope resolution, no query enforcement** occur in MOD-0040 (those are MOD-0018 / FU15 / business modules).
- **partner_admin runtime security policy is excluded** (DCP-001 GAP-13-1 / AD-8 — separate hardening pack).

## 9. Layout & Shell Contract

`shell: none`. MOD-0040 v1 is a backend organization master-data foundation.

- No Razor view; `_LayoutPlatformAdmin` / `_LayoutTenantShell` are not used.
- No frontend route, DataTable, RESX, or Ctrl+K search registry.
- `golden_reference: none` is therefore correct.
- A UI surface (admin screens for org structure) is a later follow-up — confirmation tracked as **OD-MOD-shell**.

## 10. Backend File Convention

MOD-0040 is not a DataTable/CRUD-UI module, so the Golden Reference CQRS view/partial set does not apply. When
implemented, it follows the repo's standard 5-layer architecture (Api / Application / Domain / Persistence /
Infrastructure) + CQRS (MediatR) per AGENTS.md §6 and `.antigravity/rules/erp-architecture.md`.

- Each new public type lives in its own file; existing namespace patterns are preserved.
- Concrete folder/naming for org features (Commands / Queries / Handlers / Validators / Models) is **finalized at `ready-for-dev`** — not designed in this draft.

## 11. Frontend File Contract

No frontend files in v1 (`shell: none`).

- No DataTable, no Razor partial, no RESX.
- UI screens are explicitly excluded from v1 (see exclusions) and tracked as a follow-up (OD-MOD-shell).

## 12. Validation Rules

Invariants to be enforced at implementation:

- Tenant ownership is mandatory on every aggregate; `TenantId` is never accepted from a request DTO.
- A Position Assignment must carry an effective-from date (effective-dated, AD-7).
- `EffectiveTo` must be empty or later than `EffectiveFrom`.
- Position Assignment intervals must not overlap for the same Position.
- Organization Unit parent references must stay within the same tenant, same Legal Entity, and avoid cycles.
- Position `ReportsToPositionId` must stay within the same tenant, must not self-reference, and must not create cycles.
- Archived or soft-deleted records cannot be mutated except through explicit allowed lifecycle/technical-delete commands.
- `LegalEntityId` references are validated through the resolved MDM Legal Entity read-only lookup / validation
  contract: exists, same tenant, `LifecycleStatus == ACTIVE`, and `IsDeleted == false`.

## 13. Failure Path to Verify

- cross-tenant access
- missing `LegalEntityId`
- inactive `LegalEntityId`
- deleted `LegalEntityId`
- Organization Unit orphan parent
- Organization Unit cross-tenant parent
- Organization Unit cross-Legal-Entity parent
- Organization Unit cycle
- Position missing Organization Unit
- Position cross-tenant Organization Unit
- Position self `ReportsToPositionId`
- Position reporting cycle
- Position Assignment invalid date range
- Position Assignment overlap
- manager-chain max-depth exceeded
- soft-deleted mutation
- archived mutation

## 14. Authorization Convention

- MOD-0040 endpoint permission keys use the repo-supported `Modules.{Module}.{Action}` style.
- **Permission evaluation remains in MOD-0018**; MOD-0040 stores no permissions and evaluates no policy.
- `partner_admin` runtime scope is **excluded** (fail-closed; DCP-001 GAP-13-1 / AD-8).

Resolved permission keys:

| Resource | Permission keys |
|---|---|
| Organization Units | `Modules.OrganizationUnit.Read`, `Modules.OrganizationUnit.Create`, `Modules.OrganizationUnit.Update`, `Modules.OrganizationUnit.Archive`, `Modules.OrganizationUnit.Delete` |
| Positions | `Modules.Position.Read`, `Modules.Position.Create`, `Modules.Position.Update`, `Modules.Position.Archive`, `Modules.Position.Delete` |
| Position Assignments | `Modules.PositionAssignment.Read`, `Modules.PositionAssignment.Create`, `Modules.PositionAssignment.Update`, `Modules.PositionAssignment.Delete` |
| Manager Chain | `Modules.Organization.ReadManagerChain` |

## 15. Gateway / API Routing Decision

Gateway change: **none in v1 preparation**. Backend endpoints are proposed for Platform API only; gateway route and
frontend integration are deferred.

- When endpoints are implemented, the frontend calls via Gateway (5000); browser JS never targets `5057` directly.
- Whether a new explicit Ocelot route is required is deferred; `gateway/Diten.ApiGateway/**/ocelot.json` is
  integration-agent owned and is not written by this pack.

Endpoint proposal:

| HTTP method | Route | Command/query | Permission | Actor policy |
|---|---|---|---|---|
| `GET` | `/api/platform/organization-units` | `GetOrganizationUnitsQuery` | `Modules.OrganizationUnit.Read` | tenant actor |
| `GET` | `/api/platform/organization-units/{id:guid}` | `GetOrganizationUnitByIdQuery` | `Modules.OrganizationUnit.Read` | tenant actor |
| `POST` | `/api/platform/organization-units` | `CreateOrganizationUnitCommand` | `Modules.OrganizationUnit.Create` | tenant actor |
| `PUT` | `/api/platform/organization-units/{id:guid}` | `UpdateOrganizationUnitCommand` | `Modules.OrganizationUnit.Update` | tenant actor |
| `POST` | `/api/platform/organization-units/{id:guid}/archive` | `ArchiveOrganizationUnitCommand` | `Modules.OrganizationUnit.Archive` | tenant actor |
| `DELETE` | `/api/platform/organization-units/{id:guid}` | `DeleteOrganizationUnitCommand` | `Modules.OrganizationUnit.Delete` | tenant actor |
| `GET` | `/api/platform/positions` | `GetPositionsQuery` | `Modules.Position.Read` | tenant actor |
| `GET` | `/api/platform/positions/{id:guid}` | `GetPositionByIdQuery` | `Modules.Position.Read` | tenant actor |
| `POST` | `/api/platform/positions` | `CreatePositionCommand` | `Modules.Position.Create` | tenant actor |
| `PUT` | `/api/platform/positions/{id:guid}` | `UpdatePositionCommand` | `Modules.Position.Update` | tenant actor |
| `POST` | `/api/platform/positions/{id:guid}/archive` | `ArchivePositionCommand` | `Modules.Position.Archive` | tenant actor |
| `DELETE` | `/api/platform/positions/{id:guid}` | `DeletePositionCommand` | `Modules.Position.Delete` | tenant actor |
| `GET` | `/api/platform/position-assignments` | `GetPositionAssignmentsQuery` | `Modules.PositionAssignment.Read` | tenant actor |
| `POST` | `/api/platform/position-assignments` | `CreatePositionAssignmentCommand` | `Modules.PositionAssignment.Create` | tenant actor |
| `PUT` | `/api/platform/position-assignments/{id:guid}` | `UpdatePositionAssignmentCommand` | `Modules.PositionAssignment.Update` | tenant actor |
| `DELETE` | `/api/platform/position-assignments/{id:guid}` | `DeletePositionAssignmentCommand` | `Modules.PositionAssignment.Delete` | tenant actor |
| `GET` | `/api/platform/positions/{id:guid}/manager-chain` | `GetManagerChainQuery` | `Modules.Organization.ReadManagerChain` | tenant actor |

## 15A. Persistence / Index Proposal

Collection names:

- `organization_units`
- `positions`
- `position_assignments`

Tenant-scoped filters:

- All repository reads and mutations include `TenantId == current TenantId`.
- Standard execution filter includes `IsDeleted == false`.
- Default referenceability filters also include `IsArchived == false` for Organization Unit and Position.

Indexes:

- Organization Unit: unique partial index on `(TenantId, Code)` where `IsDeleted == false`.
- Organization Unit: index on `(TenantId, LegalEntityId, ParentOrganizationUnitId, IsDeleted, IsArchived)`.
- Position: unique partial index on `(TenantId, Code)` where `IsDeleted == false`.
- Position: index on `(TenantId, OrganizationUnitId, ReportsToPositionId, IsDeleted, IsArchived)`.
- Position Assignment: index on `(TenantId, PositionId, EffectiveFrom, EffectiveTo, IsDeleted)`.
- Position Assignment: index on `(TenantId, UserId, EffectiveFrom, EffectiveTo, IsDeleted)`.

Enforcement strategy:

- Position Assignment overlap is enforced in application logic with tenant-scoped interval overlap query before write.
- MongoDB cannot express the full interval-overlap exclusion as a simple unique index; race-condition behavior must
  be reviewed in implementation, with transaction/serialization or duplicate conflict translation if needed.
- Organization Unit and Position cycle detection is performed before write using tenant-scoped ancestor traversal.
- Manager Chain query uses on-read traversal bounded to depth `32`; depth overflow fails closed.

## 16. Acceptance Criteria

Ready-for-dev acceptance criteria for the minimal backend-only v1 slice:

1. The v1 conceptual boundary lists only the MOD-0040-owned items (Organization Unit tree; Position; Position
   Assignment; effective-dated Position Assignment; tenant ownership; soft-delete / archival; minimal derived
   Manager Chain inputs and contract) and matches DCP-001 §11.
2. All explicit exclusions are recorded, including **permission evaluation (MOD-0018)** and the
   **IDataScopeResolver algorithm (MOD-0018-FU15)**.
3. Legal Entity is recorded as an external MDM-owned dependency through a read-only `LegalEntityId` lookup /
   validation contract, with duplicate Legal Entity aggregate, persistence, lifecycle, API, and UI excluded.
4. PSS-011 `countries` is recorded as Platform provisioning/support only, not the Legal Entity business-country
   source; MDM business-country ownership remains a separate follow-up outside MOD-0040.
5. OD-MOD-svc, OD-MOD-shell, OD-MOD-wave, OD-6, OD-MOD-le-contract, and OD-4 are recorded as resolved for v1.
6. The pack authorizes no frontend UI, gateway route, IDataScopeResolver, Tenant User CRUD, Tenant Role CRUD,
   permission evaluator, Legal Entity duplicate aggregate, Position-role binding, or remote User validation in v1.
7. `service: Diten.Platform` is locked for backend-only v1.
8. Organization Unit, Position, Position Assignment, Manager Chain, endpoint, permission, persistence, and failure
   paths are locked for implementation.
9. `PositionAssignment.UserId` is a required external `Guid` reference only in minimal v1; runtime User existence /
   same-tenant validation is deferred behind an AuthService-owned read-only validation contract.
10. Position-role binding is deferred; no `RoleId`, `RoleIds`, or placeholder role-binding field is implemented.
11. MOD-0018-FU15 or any runtime authorization consumer must not consume Position Assignment `UserId` as
    authoritative until the deferred Tenant User validation integration is complete.

## 17. Test Expectations

When implemented (post-`ready-for-dev`), minimum expectations will include:
- Tenant isolation (cross-tenant org data fails closed).
- Server-side `TenantId`; `TenantId` absent from request DTOs.
- Soft-delete / archive mutation behavior.
- Organization Unit required LegalEntity validation through MOD-0220 contract.
- Organization Unit parent same tenant, same Legal Entity, and cycle rejection.
- Organization Unit duplicate Code per tenant rejected.
- Position required Organization Unit, same-tenant Organization Unit, self-reporting rejection, reporting-cycle rejection.
- Position duplicate Code per tenant rejected.
- Position Assignment required Position and User, invalid date range rejection, overlap rejection, `[EffectiveFrom, EffectiveTo)` semantics.
- Manager Chain on-read derivation, max-depth rejection, cycle fail-closed.
- Missing/inactive/deleted/cross-tenant `LegalEntityId` rejection.
- `UserId` is required, parses as a `Guid`, and is persisted as an external reference without remote validation.
- No `RoleId`, `RoleIds`, or placeholder role-binding field exists on Position.
- Minimal derived Manager Chain correctness (per OD-4 strategy).
- Build PASS for the affected `Diten.Platform` projects.

Mongo index / overlap integration tests, API authorization attribute tests, and DI/API startup smoke tests should be
included or explicitly justified as follow-up at implementation review. Missing external User runtime validation,
cross-tenant external User runtime validation, and invalid external Role rejection are deferred follow-up tests, not
minimal v1 implementation blockers.

## 18. Ready-for-dev Checklist

- [x] User reviewed this reconciliation draft and approved promotion.
- [x] DCP-001 is `approved` (DCP-001 G1) — the dual gate's capability-level condition.
- [x] service boundary locked — owning service is `Diten.Platform`.
- [x] shell none / backend-only locked — UI and gateway deferred.
- [x] **OD-MOD-wave** resolved — DCP-001 Capability B, critical-path minimal implementation, ordered delivery step 5.
- [x] **OD-MOD-le-contract** resolved — MDM MOD-0220 read-only `LegalEntityId` lookup-validation contract.
- [x] **OD-4** resolved — Manager Chain is minimal derived on-read; no materialized hierarchy; max traversal depth 32.
- [x] **OD-6** resolved — Position Assignment only is effective-dated; Organization Unit / Position dating deferred.
- [x] Org Unit schema locked.
- [x] Position schema locked.
- [x] Position Assignment schema locked.
- [x] Manager Chain on-read design locked.
- [x] LegalEntity dependency locked.
- [x] permission keys locked.
- [x] role-binding deferred explicitly.
- [x] Tenant User external-validation integration deferred explicitly.
- [x] FU15/runtime-consumption guard added.
- [x] repo scope locked.
- [x] protected paths locked.
- [x] acceptance criteria locked.
- [x] test expectations locked.

> MDM business-country reference ownership is a separate follow-up outside MOD-0040. PSS-011 `countries` remains
> Platform provisioning/support only and is not a MOD-0040 implementation dependency.

## 19. Implementation Notes

**Provenance.** This pack was authored during the **Access Governance Foundation Planning** milestone on branch
`feature/governance/access-governance-foundation-planning`, governance-only, with **no** changes to production
code, test code, CI files, gateway, or frontend. It is a planning draft and authorizes no implementation.

**Ready-for-dev statement (restated):**
> This pack is ready-for-dev for the locked minimal backend-only v1 slice.
> It authorizes no frontend UI, gateway route, IDataScopeResolver, Tenant User CRUD, Tenant Role CRUD, permission
> evaluator, Legal Entity duplicate aggregate, Position-role binding, or remote Tenant User validation.

**Governance bindings.** v1 boundary = DCP-001 §11; sequencing = DCP-001 Capability B / step 5 for minimal
implementation after ready-for-dev; baselines = DCP-001 AD-1 (Position = scope + role
binding, not a permission store), AD-2 (Country and Legal Entity are separate dimensions), AD-3 (minimal derived
Manager Chain), AD-7 (effective-dated Position Assignment mandatory).

**Resolved decisions:**

- **OD-MOD-svc:** Resolved. `Diten.Platform` is the owning service.
- **OD-MOD-shell:** Resolved. `shell: none`; backend-only v1; UI and gateway route deferred.
- **OD-MOD-wave:** Resolved. DCP-001 Capability B, critical-path minimal implementation, ordered delivery step 5.
- **OD-MOD-le-contract:** Resolved. MOD-0040 consumes MDM MOD-0220 Legal Entity Foundation through a read-only
  `LegalEntityId` lookup / validation contract. Validation requires `LegalEntityId` exists, same tenant, and
  `LegalEntity.LifecycleStatus == ACTIVE`, and `LegalEntity.IsDeleted == false`; return shape is `LegalEntityId`,
  legal name, display name, lifecycle state, and `referenceable = true`.
- **OD-4:** Resolved. Manager Chain is minimal derived on-read from `Position.ReportsToPositionId`; no materialized
  hierarchy; cycle fail-closed; v1 max traversal depth 32.
- **OD-6:** Resolved. Position Assignment only is effective-dated. Organization Unit and Position effective dating
  are deferred.

**Resolved promotion decisions:**

- **Tenant User external-reference decision:** `PositionAssignment.UserId` is a required external `Guid` reference
  to AuthService `User.Id`. MOD-0040 does not duplicate User, does not connect to AuthService persistence, and does
  not perform remote User validation in minimal v1.
- **FU15/runtime-consumption guard:** MOD-0018-FU15 real `IDataScopeResolver` or any runtime authorization consumer
  must not consume Position Assignment `UserId` as authoritative until the AuthService-owned Tenant User read-only
  validation contract and MOD-0040 integration validation are complete.
- **Position role-binding decision:** Position role-binding is deferred. Minimal v1 adds no `RoleId`, no `RoleIds`,
  and no placeholder role-binding field. Tenant Role remains the role owner; MOD-0040 stores no permissions and
  evaluates no permissions.
- **Permission key style:** Resolved as `Modules.{Module}.{Action}` for MOD-0040 endpoint permissions.

**Deferred-decision → guard binding:**

- Tenant User existence / same-tenant validation is deferred from minimal v1 but required before FU15/runtime
  authorization consumption.
- Position-role binding is deferred to a separate Tenant Role integration slice.

**Identity.** `MOD-0040` is registry-reserved (`execution/registries/module-id-registry.md`); the registry row
is updated to `ready-for-dev` for the locked minimal backend-only v1 slice. `NEW-MOD-0040` is a deprecated alias
for this ID.

## 20. Follow-up Items

- Department / Team granularity in the organization tree (DCP-001 §19; MOD-0040-owned org-foundation extension).
- Region dimension decision and ownership (DCP-001 OD-3 / §19).
- Historical restructuring / organization versioning (DCP-001 §19).
- UI screens for organization structure (excluded from v1; OD-MOD-shell).
- **Delegation / substitution** — cross-cutting future follow-up; **not** MOD-0040-owned by default (DCP-001 §19).
- **Tenant User** / **Tenant Role** packs — authored only after the MOD-0040 shape is locked (DCP-001 §8); their IDs (DCP-001 OD-1 / OD-2) are **not** reserved in this milestone.
- **MOD-0018-FU15** real `IDataScopeResolver` — consumes MOD-0040 org data once available.
- AuthService-owned Tenant User read-only validation contract.
- MOD-0040 PositionAssignment UserId integration validation.
- Tenant Role pack.
- AuthService-owned Tenant Role read-only validation contract.
- Position-role binding integration slice.
- Organization Unit effective dating.
- Position effective dating.
- Cross-Legal-Entity organization tree.
- LegalEntityId inheritance from parent Organization Unit.
- Materialized Manager Chain / deep-chain optimization.
