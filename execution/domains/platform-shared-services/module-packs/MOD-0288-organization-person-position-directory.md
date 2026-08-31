---
id: MOD-0288
name: Organization, Person & Position Directory
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: BaseEntity
status: done
owner: platform-team
branch: feature/pss/mod-0040-tenant-organization-foundation
started: ""
target: ""
form_field_count: 0
---

# MOD-0288 — Organization, Person & Position Directory

> **Canonicalization (DCP-002):** Canonical ID is now **MOD-0288** and canonical name **Organization, Person & Position Directory** (Blueprint-aligned). Prior repo ID **MOD-0040** (and its earlier alias **NEW-MOD-0040**) and the prior name "Tenant Organization Foundation" are deprecated aliases retained for traceability; repo MOD-0040 had drifted onto a Blueprint ID reserved for "Canonical ID & Correlation Standard". The child follow-up is **MOD-0288-FU01**. Body text below predates canonicalization and references MOD-0040 / the prior name; scope, boundaries, and meaning are unchanged. Ref: `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`.

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

---

## Amendment — MOD-0007 Decision Authority Provider

> **Amendment status: DRAFT / NON-EXECUTABLE.** This amendment does not change the parent pack's `done` status,
> does not reopen MOD-0288, and does not authorize implementation. It adds a proposed read-only provider contract
> for the future MOD-0007 consumer. The V1 lifecycle semantics and bilateral governance fixtures are reconciled;
> execution remains blocked by the runtime evidence items in §DA-18 and by the dedicated S2S
> identity/delegation gate described in §DA-14.

### DA-1. Module Summary

MOD-0288 is the authoritative system of record for its tenant-scoped Person references and Positions. This
amendment proposes a dedicated internal provider through which **MOD-0007 — Decision Authority Provider** may
resolve a decision authority reference or test whether a new reference may be created. The provider is a
zero-write query surface; it does not change existing Person or Position CRUD behavior.

### DA-2. Ownership and Boundaries

- MOD-0288 owns authoritative Person/Position existence, tenant visibility, lifecycle facts, and referenceability.
- MOD-0007 owns decision/rationale records and the decision-authority reference it persists or consumes.
- MOD-0288 does not decide approval, task, WorkCenter, DWS, delegation, or decision-governance policy.
- MOD-0007 must not derive authority validity from display data or from a non-authoritative public endpoint.
- This amendment does not call or authorize DCP-006 Gate 2 and does not modify WorkCenter, task, DWS, or approval
  code. See [DCP-006](../../../portfolio/delivery-capability-packs/DCP-006-portfolio-delivery-process-core.md).

### DA-3. Owned Contract Object

The exact reference type is `DecisionAuthorityReferenceV1`. It contains **exactly four fields** and no extension
bag, display projection, tenant, actor, eligibility, lifecycle, or provenance field:

```text
DecisionAuthorityReferenceV1
  ContractName
  ContractVersion
  AuthorityKind
  AuthorityId
```

Locked values and domains:

| Field | Exact rule |
|---|---|
| `ContractName` | Must equal `management-governance.decision-authority-reference`. |
| `ContractVersion` | Must equal `1.0`. |
| `AuthorityKind` | Exact allowlist: `Person` or `Position`; no other value or alias. |
| `AuthorityId` | Required, non-empty GUID identifying the selected MOD-0288 record. |

Mode is request metadata and is not a fifth contract field. Tenant and actor come only from authenticated context
and are never accepted in the contract or query payload.

### DA-4. Provider Operations and Modes

The proposed provider accepts one `DecisionAuthorityReferenceV1` plus one exact mode:

| Mode | Supported | Meaning |
|---|---:|---|
| `HistoricalResolve` | Yes | Resolve a tenant-visible non-technically-deleted record for historical display/audit, even when it is no longer eligible for a new reference. |
| `NewReferenceEligibility` | Yes | Confirm Person as tenant-visible, active and not technically deleted; confirm Position as tenant-visible, active, non-archived, not technically deleted and effective at provider evaluation time. |
| `CurrentSelectionEligibility` | No | Always `400`; MOD-0288 does not expose this mode through this contract. |

Any missing, malformed, case-drifted, or unknown mode is `400`. The provider does not silently default a mode.

### DA-5. Repo Scope

This governance-only amendment changes only
`execution/domains/platform-shared-services/module-packs/MOD-0288-organization-person-position-directory.md`.
Any future implementation needs a separately approved executable amendment or follow-up pack with exact files.

### DA-6. Protected Paths

- `.antigravity/**`.
- `services/Diten.Platform/**`, `services/Diten.AuthService/**`, and `services/Diten.Platform.Common/**` in this
  governance-only amendment.
- `gateway/Diten.ApiGateway/**` and `frontend/Diten.Web/**`; the provider has no browser or Gateway route.
- All public Person/Position controllers, routes, DTOs, CRUD handlers, and their current behavior.
- WorkCenter, Tasks/MOD-0024, DWS, Workflow/Approvals/MOD-0023, and DCP-006 Gate 2 code and governance.
- Other domain services and module packs.

### DA-7. Dependencies

- MOD-0288 Person/Position repositories remain the authoritative data source.
- MOD-0007 checkpoint `7bdbd37e16c72cd80f081612a104cc3af7e2b4cd` records bilateral governance alignment
  for the exact four-field reference and mode-aware fixtures. Executable/live fixture evidence remains a runtime
  promotion gate, not an open lifecycle decision.
- Authentication, S2S scope, service identity, actor delegation, revocation, and replay handling depend on the
  bounded parent [MOD-0018](MOD-0018-rbac-abac-authorization.md) S2S/attestation amendment. Its §20 keeps
  `MOD-0018-FU16` exclusively for Global Product Permission Onboarding; this draft creates no S2S follow-up
  identity. Runtime provisioning and executable evidence remain required.
- No dependency on Person batch lookup-validation, Position public CRUD GET, Gateway, browser, Task, WorkCenter,
  DWS, approval, or DCP-006 Gate 2 is introduced.

### DA-8. Runtime Constraints

- Dedicated internal S2S provider only; no browser-facing or Gateway route.
- Zero writes, events, audit-side mutations, task creation, approval creation, or lifecycle changes.
- Authenticate and validate required context **before** any Person/Position lookup.
- Tenant and actor are read only from authenticated/delegated context; request-supplied tenant/actor is forbidden.
- Use a bounded timeout. The authority decision has no retry and no cache: a caller must not reuse stale provider
  output as current authority.
- Dependency timeout, unavailability, or malformed provider/dependency response fails closed as `503`.
- Person batch endpoint is not an authoritative-provider substitute. Position CRUD `GET` is not authoritative
  validation.

### DA-9. Layout and Shell Contract

`shell: none` remains unchanged. This amendment adds no Razor view, frontend route, JavaScript, DataTable, RESX,
browser call, or Gateway exposure.

### DA-10. Backend File Convention

No backend file is authorized by this draft. A future executable pack must define a dedicated internal query,
handler, provider interface, transport adapter, response envelope, S2S policy, timeout configuration, and tests;
it must not retrofit authoritative semantics into the existing public Person/Position CRUD handlers.

### DA-11. Frontend File Contract

No frontend files. The contract is not selectable or callable from a browser surface. Any future MOD-0007 UI
calls its own backend, which in turn uses the dedicated S2S provider after Gate I is satisfied.

### DA-12. Validation Rules and Lifecycle Matrix

Common validation precedes lookup: exact contract name/version, supported mode, `AuthorityKind` allowlist, and
non-empty `AuthorityId`. The authenticated tenant execution filter makes missing, cross-tenant, technically
soft-deleted, and otherwise invisible records indistinguishable.

| Kind | Mode | Authoritative lifecycle rule | Result |
|---|---|---|---|
| `Person` | `HistoricalResolve` | Same-tenant, `IsDeleted == false`, and business `Status != Deleted`; `Inactive` and `Deprecated` remain resolvable for history. | Success; missing/cross-tenant/invisible/business-deleted/technical soft-delete is indistinguishable `404`. |
| `Person` | `NewReferenceEligibility` | Same-tenant visibility, `IsDeleted == false`, and `Status == Active`. Current `IsReferenceable` is the derived consistency expression of those facts, not an additional effective-date predicate. | Success when all three authoritative predicates pass; otherwise visible-but-ineligible is `409`. |
| `Position` | `HistoricalResolve` | Same-tenant and `IsDeleted == false`; `IsArchived`, `Draft`, `Frozen`, `Closed`, future-effective, and past-effective records remain resolvable for history. | Success; missing/cross-tenant/technical soft-delete is `404`. |
| `Position` | `NewReferenceEligibility` | Same-tenant, `IsDeleted == false`, `IsArchived == false`, `Status == Active`, `EffectiveFrom <= now`, and (`EffectiveTo == null` or `EffectiveTo > now`). | Success when all predicates pass; otherwise visible-but-ineligible is `409`. |

Intervals use `[EffectiveFrom, EffectiveTo)`: equality at `EffectiveTo` is no longer effective. For Position,
`EffectiveFrom == null` is not proven effective for this stricter provider and is therefore `409` under
`NewReferenceEligibility`. `HistoricalResolve` never converts an inactive/archived/out-of-date visible record
into a `404`.

Person `Status == Deleted` is a **business status** and is distinct from technical `IsDeleted == true`, but this
provider discloses neither: both are indistinguishable from missing/cross-tenant/invisible as `404`.
The provider must not bypass the execution filter or probe tombstones to explain which predicate failed.

Person has no authoritative effective-date field in V1. The provider therefore creates no synthetic date,
fallback timestamp, inferred validity window, or hidden effective-date predicate for Person. `Status == Active`
is the complete V1 business-lifecycle test after tenant visibility and technical deletion filtering.

### DA-13. Failure Paths to Verify

| Failure | Required result |
|---|---:|
| Malformed contract, unsupported/case-drifted mode, unsupported/case-drifted version/kind, or `CurrentSelectionEligibility` | `400` |
| Missing/invalid authentication or required tenant/actor context | `401` |
| Authenticated caller lacks the exact S2S scope, consumer profile, or valid actor delegation | `403` |
| Missing, cross-tenant, otherwise invisible, technically soft-deleted Person/Position, or business `Status == Deleted` Person | indistinguishable `404` |
| Visible authority fails `NewReferenceEligibility` lifecycle/referenceability predicates | `409` |
| Provider/dependency timeout, unavailable transport/repository, or malformed dependency response | `503` |

Authentication/context failure must be returned before lookup; tests must prove the repository/provider lookup was
not invoked for `401` and `403` paths.

### DA-14. Authorization Convention and Exact Consumer Allowlist

The allowlist is closed and exact:

| Consumer profile | `HistoricalResolve` | `NewReferenceEligibility` | `CurrentSelectionEligibility` |
|---|---:|---:|---:|
| `MOD-0007` | Allow | Allow | Deny (`400`) |
| Any other profile, including generic platform/browser identities | Deny (`403`) | Deny (`403`) | Deny (`403`) |

The dedicated S2S service identity, exact scope literal, signed/delegated actor evidence, revocation, replay, and
tenant-binding rules are not redefined here. Their governance source is the parent MOD-0018 bounded
S2S/attestation amendment; no new FU identity is created. Provisioning and runtime evidence must pass before
any identity can activate this allowlist. Until then the provider remains non-executable.

### DA-15. Gateway and API Routing Decision

Gateway change is forbidden. The future route, if approved, lives only on an internal service-to-service surface
and is not registered in Ocelot, exposed through MVC, or callable by browser JavaScript. Existing public routes
`/api/v1/platform/persons*` and `/api/platform/positions*` remain unchanged and are not aliases for this provider.

### DA-16. Acceptance Criteria

1. Parent frontmatter remains `status: done`; the amendment is visibly `DRAFT / NON-EXECUTABLE`.
2. `DecisionAuthorityReferenceV1` has exactly the four fields in §DA-3, with the exact contract name/version and
   `AuthorityKind` limited to `Person | Position`.
3. Only `HistoricalResolve` and `NewReferenceEligibility` are supported; `CurrentSelectionEligibility` is `400`.
4. The Person/Position matrix in §DA-12 is implemented without broadening visibility or bypassing soft-delete.
5. Authentication/context and S2S authorization run before lookup; error mapping is exactly
   `400/401/403/404/409/503` as §DA-13 defines.
6. The provider is zero-write, bounded-timeout, no-retry, and non-authority-cacheable.
7. Only the exact `MOD-0007` consumer profile/mode pairs in §DA-14 are permitted.
8. The parent MOD-0018 bounded S2S/attestation amendment's identity/delegation, tenant-binding, replay, and
   revocation evidence is provisioned and verified, and the bilaterally aligned MOD-0007 four-field/mode
   fixtures pass as executable fixtures, before runtime activation.
9. Public Person/Position endpoints and WorkCenter/task/DWS/approval behavior remain unchanged; Gate 2 is not called.

### DA-17. Test Expectations

- Bilateral MOD-0007 fixtures, governance-aligned at checkpoint
  `7bdbd37e16c72cd80f081612a104cc3af7e2b4cd`, assert exact serialization/deserialization of all four fields and
  reject extras, wrong casing, wrong name, wrong version, unknown kind, and unknown mode.
- Matrix tests cover both kinds, both supported modes, all listed lifecycle states, both effective interval
  boundaries, null Position dates, archive, business Person `Deleted` as `404`, technical soft-delete, missing,
  and cross-tenant invisibility.
- Authorization-order tests prove `401/403` performs zero repository lookup.
- Status tests prove exact `400/401/403/404/409/503` mappings and indistinguishable `404` bodies.
- Timeout/unavailable/malformed-dependency tests prove `503`, bounded cancellation, no retry, and no cached
  authority fallback.
- Architecture tests prove no public controller/Gateway/browser route and no write/event/task/approval/DWS call.
- Regression tests prove existing Person batch and Position CRUD GET/public CRUD behavior is unchanged.

### DA-18. Draft / Non-Executable Checklist

- [x] Parent `done` status preserved.
- [x] Exact four-field reference and Person/Position-only kind allowlist recorded.
- [x] Provider modes, lifecycle matrix, failure mapping, protected paths, and no-Gateway boundary recorded.
- [x] Current code reality inspected: Person has no effective dates; Position has status/archive/effective dates.
- [x] **HUMAN REVIEW CLOSED:** Person V1 has no effective-date predicate; `NewReferenceEligibility` is exactly
  tenant-visible + `Status == Active` + `IsDeleted == false`, with no synthetic/fallback/inferred validity.
- [x] MOD-0007 checkpoint `7bdbd37e16c72cd80f081612a104cc3af7e2b4cd` bilaterally aligns the exact
  `DecisionAuthorityReferenceV1` four-field tuple and mode-aware fixtures with this provider matrix.
- [x] Parent MOD-0018 bounded S2S/attestation amendment is the governance source for exact S2S
  identity/delegation semantics; `MOD-0018-FU16` remains Global Product Permission Onboarding.
- [ ] Principal/credential provisioning and dedicated S2S runtime evidence under the parent MOD-0018 executable
  follow-up scope pass.
- [ ] Bilateral MOD-0007/MOD-0288 executable fixture evidence passes against the future provider implementation.
- [ ] Exact internal transport/route and bounded timeout value approved in an executable follow-up.

### DA-19. Implementation Notes

Code-reality basis for this draft:

- `PersonReference.IsReferenceable` currently means `!IsDeleted && Status == Active`; Person has no
  `EffectiveFrom`/`EffectiveTo` field. Human review therefore closes Person V1 eligibility without an
  effective-date predicate; no date is fabricated or inferred.
- Position has `IsArchived`, `PositionStatus` (`Draft|Active|Frozen|Closed`), `EffectiveFrom`, and `EffectiveTo`.
- `TenantRepository.ExecutionFilter` applies current `TenantId` and `IsDeleted == false`, which is why
  missing/cross-tenant/technical-delete outcomes collapse to `404` without tombstone probing.
- Existing Person lookup-validation is batched and emits per-item referenceability; it is not this authoritative
  provider. Existing Position `GET` returns a CRUD projection and does not validate this contract or mode.

No production behavior is authorized or changed by recording these observations.

**Bilateral reconciliation provenance.** MOD-0007 checkpoint
`7bdbd37e16c72cd80f081612a104cc3af7e2b4cd` records the same exact
`management-governance.decision-authority-reference/1.0` four-field tuple, Person/Position-only kinds,
`HistoricalResolve` / `NewReferenceEligibility` mode fixtures, rejected `CurrentSelectionEligibility`, lifecycle
matrix, and `404/409/503` authority outcomes. This is governance compatibility evidence only; it creates no
transport, endpoint, credential, runtime, or production authority.

### DA-20. Follow-up Items

- Parent MOD-0018 bounded S2S/attestation amendment, under separately approved executable follow-up scope, for
  runtime provisioning/evidence of dedicated S2S identity, exact scope/delegation, tenant binding, replay, and
  revocation.
- Bilateral MOD-0007/MOD-0288 executable fixtures and strict four-field/mode serialization tests.
- Executable MOD-0288 provider follow-up/amendment with exact internal route, timeout value, response envelope,
  observability boundary, and implementation file scope.
- Security, lifecycle-matrix, dependency-failure, and public-endpoint regression test implementation.
