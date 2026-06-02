---
id: MOD-0040
name: Tenant Organization Foundation
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: BaseEntity
status: draft
owner: platform-team
branch: feature/pss/mod-0040-tenant-organization-foundation
started: ""
target: ""
form_field_count: 0
---

# MOD-0040 — Tenant Organization Foundation

> **Planning-only note:**
> This draft is planning-only.
> It is not ready-for-dev.
> It authorizes no production implementation.
>
> This pack is the keystone organization master-data dependency referenced by **DCP-001 — Access Governance**
> (CAP-001). Its v1 conceptual boundary is governed by [DCP-001 §11](../../../portfolio/delivery-capability-packs/DCP-001-access-governance.md)
> and ordered as step 3 of DCP-001 §8. Production code begins only when **both** the DCP is `approved` /
> `ready-for-execution` **and** this pack is `approved` / `ready-for-dev` (CAP-001 §7 dual gate).

> **Golden Reference decision:** This is a backend organization master-data foundation, not a UI/DataTable module.
> `shell: none`, `golden_reference: none`, `form_field_count: 0`. Razor layout, DataTable verifier, RESX, and the
> frontend file set are N/A for v1. UI is a later follow-up (see OD-MOD-shell).

> **entity_base rationale:** `entity_base: BaseEntity`. MOD-0040 records are **tenant-owned** and hosted in
> `Diten.Platform`; per the module-pack standard entity_base table, tenant-aware records in `Diten.Platform`
> use the concrete `BaseEntity` class (not `GlobalEntity`). `TenantId` is resolved server-side and is **not**
> present in any request DTO. (Owning service is itself an open decision — see OD-MOD-svc.)

> **Draft scope guard:** This pack **does not design persistence models, APIs, entities, or tests.** It defines
> the conceptual v1 boundary, dependencies, exclusions, and the open decisions that must be resolved before
> `ready-for-dev`. Concrete schema, field types, indexes, endpoints, validators, and test suites are authored at
> the `ready-for-dev` transition, not here.

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

This pack is a **conceptual planning draft**. v1 is backend-only; there is no UI and no field-level access model.

## 2. Ownership and Boundaries

**In scope — owned by MOD-0040 (v1 conceptual boundary):**

- Organization Unit tree
- Position
- Position Assignment
- effective-dated Position Assignment
- tenant ownership (for MOD-0040-owned records)
- soft-delete / archival semantics (for MOD-0040-owned records)
- minimal derived Manager Chain inputs and contract

**External dependency (consumed, not owned):**

- the **MDM-owned Legal Entity capability** — the Legal Entity master record lives in MDM
  (manager plan candidate ID: MOD-0220; canonical repo registration pending confirmation / reservation).
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

> Conceptual only. The concrete entity / repository / command / query / DTO / endpoint / permission inventory is
> **deferred to the `ready-for-dev` transition.** No objects are designed or created in this draft.

Conceptual aggregates owned by MOD-0040 v1:

- **Organization Unit** — node in the tenant Organization Unit tree (parent/child hierarchy).
- **Position** — a role-bearing seat within the organization (scope + role binding, not a permission store; see DCP-001 AD-1).
- **Position Assignment** — effective-dated binding of a user to a Position (DCP-001 AD-7).
- **Manager Chain** — **derived**, minimal; not a separately authored hierarchy (DCP-001 AD-3). Derivation depth/strategy is OD-4.

**Not owned here — external reference:** **Legal Entity** is an MDM-owned master record. MOD-0040 stores only a
read-only `LegalEntityId` reference to it (e.g., an Organization Unit's owning Legal Entity); it does **not**
author a Legal Entity aggregate, persistence, lifecycle, API, or UI. See §2 and OD-MOD-le-contract (§19).

Owned permissions, commands, queries, DTOs, API endpoints, and frontend routes: **none designed in this draft.**
They are authored at `ready-for-dev`, after the open decisions are resolved.

## 4. Entity Fields

> **Not designed in this draft.** Field-level schema (types, required/optional, validation rules, MongoDB index
> needs) is **deferred to `ready-for-dev`**, because it depends on unresolved open decisions: effective-dating
> depth (OD-6), Manager Chain derivation (OD-4), and the read-only Legal Entity reference contract (OD-MOD-le-contract).

Conceptual aggregate boundary (no field types — boundary description only):

| Aggregate | Conceptual purpose | Tenant-owned | Effective-dated (v1) |
|---|---|---|---|
| Legal Entity *(reference only)* | **MDM-owned** master record; MOD-0040 stores a read-only `LegalEntityId` | No — owned by MDM | N/A |
| Organization Unit | Node of the Org Unit tree (parent/child) | Yes (`BaseEntity`) | Out of v1 scope unless OD-6 widens |
| Position | Scope + role-binding seat (no permission storage) | Yes (`BaseEntity`) | Out of v1 scope unless OD-6 widens |
| Position Assignment | User ↔ Position binding | Yes (`BaseEntity`) | **Yes — mandatory v1** (DCP-001 AD-7) |
| Manager Chain | Minimal **derived** reporting chain | Derived from the above | Derived (OD-4) |

`TenantId`, soft-delete (`IsDeleted` / `DeletedAt`), and audit fields are inherited from `BaseEntity` and are not
modeled as user fields. Concrete field schemas and indexes are authored at `ready-for-dev`.

## 5. Repo Scope

**This milestone (draft authoring):** the only file authored is this pack —
`execution/domains/platform-shared-services/module-packs/MOD-0040-tenant-organization-foundation.md`. **No
production code, test code, gateway, or frontend file is touched.**

**Future implementation repo scope (conceptual, applies only after `ready-for-dev`):**

- `services/Diten.Platform/src/Diten.Platform.Domain/**` — org master-data aggregates.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/**` — org CQRS features.
- `services/Diten.Platform/src/Diten.Platform.Persistence/**` — MongoDB mappings/indexes.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/**` — DI wiring.
- `services/Diten.Platform/tests/**` — org master-data test suites.

Exact folders/files are finalized at `ready-for-dev`; listed here for boundary visibility only.

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
- Legal Entity is owned by the MDM Legal Entity capability (manager plan candidate ID: MOD-0220; canonical repo
  registration pending confirmation / reservation).
- MOD-0040 consumes only a read-only `LegalEntityId` reference / lookup-validation contract to that capability.
- The minimal cross-domain contract is **OD-MOD-le-contract** and must be resolved before MOD-0040 `ready-for-dev`.

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
- **Effective-dated Position Assignment** is mandatory in v1 (DCP-001 AD-7). Effective-dating of Organization Unit and Position is **OD-6** (out of v1 unless OD-6 widens it).
- **Manager Chain is derived and minimal** (DCP-001 AD-3); on-read vs materialized is **OD-4**.
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

> **Not designed in this draft.** Field-level FluentValidation rules are **deferred to `ready-for-dev`** (they
> depend on OD-6 effective-dating depth, OD-4 Manager Chain, and OD-MOD-le-contract).

Conceptual invariants to be enforced at implementation (boundary statements, not validators):

- Tenant ownership is mandatory on every aggregate; `TenantId` is never accepted from a request DTO.
- A Position Assignment must carry an effective-from date (effective-dated, AD-7).
- Organization Unit parent references must stay within the same tenant and avoid cycles.
- Archival/soft-delete must preserve history; references to archived entities are handled, not silently broken.
- `LegalEntityId` references are validated through the MDM Legal Entity read-only lookup / validation contract
  (OD-MOD-le-contract).

## 13. Failure Path to Verify

> Conceptual verification targets; concrete test scenarios are authored at `ready-for-dev`.

- **Cross-tenant access** → fails closed (404); no cross-tenant org data leakage.
- **Overlapping / contradictory effective-dated Position Assignments** → resolution rule applied (defined at ready-for-dev under OD-6).
- **Orphan / cyclic Organization Unit parent** → rejected.
- **Reference to archived (soft-deleted) entity** → handled per archival semantics, not a hard failure.
- **Unknown or inaccessible `LegalEntityId`** → rejected by the MDM Legal Entity lookup / validation contract
  (OD-MOD-le-contract).

## 14. Authorization Convention

- When MOD-0040 exposes endpoints (post-`ready-for-dev`), they must follow the canonical permission convention
  approved under MOD-0018-FU13. No concrete permission keys are fixed in this draft.
- **Permission evaluation remains in MOD-0018**; MOD-0040 stores no permissions and evaluates no policy.
- `partner_admin` runtime scope is **excluded** (fail-closed; DCP-001 GAP-13-1 / AD-8).
- The concrete permission list and actor policy are designed at `ready-for-dev`; none are defined in this draft.

## 15. Gateway / API Routing Decision

Gateway change: **none in this milestone** (no endpoints exist in the draft).

- When endpoints are implemented, the frontend calls via Gateway (5000); browser JS never targets `5057` directly.
- Whether a new explicit Ocelot route is required is decided at `ready-for-dev`; `gateway/Diten.ApiGateway/**/ocelot.json` is integration-agent owned and is not written by this pack.

## 16. Acceptance Criteria

Because this pack is a **draft** (planning-only, authorizes no implementation), acceptance is governance-level:

1. The v1 conceptual boundary lists only the MOD-0040-owned items (Organization Unit tree; Position; Position
   Assignment; effective-dated Position Assignment; tenant ownership; soft-delete / archival; minimal derived
   Manager Chain inputs and contract) and matches DCP-001 §11.
2. All explicit exclusions are recorded, including **permission evaluation (MOD-0018)** and the
   **IDataScopeResolver algorithm (MOD-0018-FU15)**.
3. Legal Entity is recorded as an external MDM-owned dependency through a read-only `LegalEntityId` lookup /
   validation contract, with duplicate Legal Entity aggregate, persistence, lifecycle, API, and UI excluded.
4. PSS-011 `countries` is recorded as Platform provisioning/support only, not the Legal Entity business-country
   source; MDM business-country ownership remains a separate follow-up outside MOD-0040.
5. Open decisions OD-MOD-svc, OD-MOD-shell, OD-MOD-wave, OD-MOD-le-contract, OD-4, OD-6 are recorded and bound to gates (see §18 / §19).
6. The pack authorizes **no** production implementation and **does not** design persistence, APIs, entities, or tests.
7. `entity_base: BaseEntity` and `service: Diten.Platform` are consistent with the module-pack standard for tenant-owned `Diten.Platform` records (subject to OD-MOD-svc confirmation).
8. **`ready-for-dev` is not granted by this draft.** Promotion requires resolving OD-MOD-svc, OD-MOD-shell, OD-MOD-wave, OD-MOD-le-contract, OD-4, and OD-6, and requires DCP-001 to be `approved` (DCP-001 G1).

## 17. Test Expectations

No tests are authored in this draft.

When implemented (post-`ready-for-dev`), minimum expectations will include:
- Tenant isolation (cross-tenant org data fails closed).
- Soft-delete / archival behavior.
- Effective-dated Position Assignment resolution.
- Minimal derived Manager Chain correctness (per OD-4 strategy).
- Build PASS for the affected `Diten.Platform` projects.

Concrete unit/integration coverage is specified at `ready-for-dev`.

## 18. Ready-for-dev Checklist

- [ ] User reviewed this draft and approved the v1 conceptual boundary.
- [ ] DCP-001 is `approved` (DCP-001 G1) — the dual gate's capability-level condition.
- [ ] **OD-MOD-svc** resolved — owning service (`Diten.Platform`) confirmed.
- [ ] **OD-MOD-shell** resolved — `shell: none` for backend-only v1 confirmed; UI deferred.
- [ ] **OD-MOD-wave** resolved — MOD-0040 delivery wave assigned.
- [ ] **OD-MOD-le-contract** resolved — minimal cross-domain read-only `LegalEntityId` lookup / validation contract approved with the MDM Legal Entity capability.
- [ ] **OD-4** resolved — Manager Chain derivation depth + on-read vs materialized strategy.
- [ ] **OD-6** resolved — effective-dating depth (Position Assignment only vs also Org Unit / Position).
- [ ] Entity schema, indexes, validators, and endpoints designed (authored at the `ready-for-dev` transition).
- [ ] Permission list + authorization policy defined.
- [ ] `started` / `target` dates set.

> MDM business-country reference ownership is a separate follow-up outside MOD-0040. PSS-011 `countries` remains
> Platform provisioning/support only and is not a MOD-0040 implementation dependency.

## 19. Implementation Notes

**Provenance.** This pack was authored during the **Access Governance Foundation Planning** milestone on branch
`feature/governance/access-governance-foundation-planning`, governance-only, with **no** changes to production
code, test code, CI files, gateway, or frontend. It is a planning draft and authorizes no implementation.

**Planning-only statement (restated):**
> This draft is planning-only.
> It is not ready-for-dev.
> It authorizes no production implementation.

**Governance bindings.** v1 boundary = DCP-001 §11; sequencing = DCP-001 §8 step 3 (MOD-0040 draft pack), step 4
(ready-for-dev review), step 5 (minimal implementation); baselines = DCP-001 AD-1 (Position = scope + role
binding, not a permission store), AD-2 (Country and Legal Entity are separate dimensions), AD-3 (minimal derived
Manager Chain), AD-7 (effective-dated Position Assignment mandatory).

**Open decisions:**

- **OD-MOD-svc:** Confirm `Diten.Platform` as the owning service.
- **OD-MOD-shell:** Confirm `shell: none` for backend-only v1; UI remains a later follow-up.
- **OD-MOD-wave:** Assign the MOD-0040 delivery wave. *(Resolve before MOD-0040 `ready-for-dev`.)*
- **OD-MOD-le-contract:** Define and approve the minimal cross-domain read-only `LegalEntityId` lookup / validation
  contract with the MDM Legal Entity capability. Resolve before MOD-0040 `ready-for-dev`.
- **OD-4:** Manager Chain derivation depth and computation strategy: on-read vs materialized. *(Aligns with DCP-001 OD-4.)*
- **OD-6:** Effective-dating depth: Position Assignment only vs Organization Unit and Position dating as well. *(Aligns with DCP-001 OD-6.)*

**Open-decision → gate binding:**

- **OD-MOD-svc, OD-MOD-shell, OD-MOD-wave, OD-MOD-le-contract, OD-4, OD-6 → resolve before MOD-0040 `ready-for-dev`.**

**Identity.** `MOD-0040` is registry-reserved (`execution/registries/module-id-registry.md`); the registry row
was updated `Planned / Reserved → draft` in this same milestone. `NEW-MOD-0040` is a deprecated alias for this ID.

## 20. Follow-up Items

- Department / Team granularity in the organization tree (DCP-001 §19; MOD-0040-owned org-foundation extension).
- Region dimension decision and ownership (DCP-001 OD-3 / §19).
- Historical restructuring / organization versioning (DCP-001 §19).
- UI screens for organization structure (excluded from v1; OD-MOD-shell).
- **Delegation / substitution** — cross-cutting future follow-up; **not** MOD-0040-owned by default (DCP-001 §19).
- **Tenant User** / **Tenant Role** packs — authored only after the MOD-0040 shape is locked (DCP-001 §8); their IDs (DCP-001 OD-1 / OD-2) are **not** reserved in this milestone.
- **MOD-0018-FU15** real `IDataScopeResolver` — consumes MOD-0040 org data once available.
