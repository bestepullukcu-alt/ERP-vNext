---
id: MOD-0018-FU15
name: Real DataScopeResolver
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: GlobalEntity
status: ready-for-dev
owner: platform-team
branch: feature/governance/access-governance-execution
started: 2026-06-09
target: ""
form_field_count: 0
---

# MOD-0018-FU15 — Real DataScopeResolver

> **Ready-for-dev note.** This pack is the mandatory module-pack contract authored **before** real
> `IDataScopeResolver` development begins (Access Governance step AG-STEP-008). It was reviewed, revised against
> read-only repo evidence, and promoted `draft → ready-for-dev`; all design/scope decisions are locked (§4, §18).
> Authoring + promotion made **no** runtime, seed, migration, test, frontend, gateway, `.antigravity`, registry, or
> roadmap change. `ready-for-dev` authorizes implementation **planning/handoff** only; production code still passes
> the orchestrator / add-module gate. Of the §7 dependency gates, **`AG-STEP-003` (MOD-0220 `LegalEntityId`
> contract) is verified / satisfied** (read-only audit), so `LegalEntity`-scope is governance-ungated;
> **`MOD-0288-FU01` (Position Assignment `UserId`) remains the open gate**. Runtime behavior stays fail-closed.

> **Identity (DCP-002, proven).** Canonical ID `MOD-0018-FU15`, canonical name **Real DataScopeResolver**, parent
> **MOD-0018** (RBAC / ABAC Authorization). Verified fail-closed with
> `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0018-FU15 --name "Real DataScopeResolver" --parent MOD-0018`
> (exit 0). The deprecated alias **`NEW-MOD-0041`** must **not** be used — `MOD-0041` is reserved for
> Logging / Monitoring. No new `MOD-xxxx` is minted by this pack.

> **Golden Reference decision:** This is a backend / shared-runtime authorization slice, not a UI/DataTable module.
> `shell: none`, `golden_reference: none`, `form_field_count: 0`. Razor layout, DataTable verifier, RESX, and the
> frontend file set are N/A.

> **entity_base rationale:** Frontmatter requires `entity_base`; FU15 adds **no persisted entity**. `GlobalEntity`
> is recorded to match the MOD-0018 / FU12 sibling convention. FU15 only **reads** MOD-0288-owned tenant entities
> (`Organization Unit`, `Position`, `Position Assignment`); it owns no aggregate of its own.

## 1. Module Summary

MOD-0018-FU15 replaces the placeholder **`NoOpDataScopeResolver`** with the **real `IDataScopeResolver`**
implementation. The NoOp resolver today returns `Array.Empty<EntitlementDataScope>()` for every call
([services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/NoOpDataScopeResolver.cs](../../../../services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/NoOpDataScopeResolver.cs)),
which means the tenant authorization context (MOD-0018-FU12) hydrates **empty** org-scope fields and no row-level
data scope is computed anywhere in the platform.

The real resolver computes, per `(tenantId, userId, moduleCode, featureCode)`, the **set of data scopes** a user is
entitled to — i.e. **which rows** the user may see/act on — by reading the **MOD-0288 Organization, Person &
Position Directory** master data (Organization Unit tree, Position, effective-dated Position Assignment, derived
Manager Chain = the Position reporting chain) and emitting a normalized `IReadOnlyList<EntitlementDataScope>` over the existing
`EntitlementDataScopeKind` enum
([services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/EntitlementDataScopeKind.cs](../../../../services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/EntitlementDataScopeKind.cs)).

The pack is anchored on one invariant that the whole Access Governance capability depends on:

- **Role = action permission** (what a user *may do*; owned by MOD-0018 / the JWT `permission` claim / `[HasPermission]`).
- **Data Scope = the set of rows a user may do it on** (owned by this resolver).

These two axes stay **orthogonal**. FU15 never grants or denies an *action*; it only narrows the *row set*. A user
with no matching data scope and a business module that has opted in sees **zero rows** (fail-closed), never "all
rows". This pack does not change any allow/deny authorization decision produced by MOD-0018.

The pack focuses on four goals:

1. Implement a real `IDataScopeResolver` in `Diten.Platform` that reads MOD-0288 org master data.
2. Swap the DI registration from `NoOpDataScopeResolver` to the real implementation (single seam), so
   MOD-0018-FU12's `JwtTenantAuthorizationContext.InitializeAsync()` hydrates real org-scope fields.
3. Define the **business-module opt-in + fail-closed** consumption contract so row-level enforcement is explicit
   and never silently "open".
4. Keep MOD-0288 as the single org-structure source of truth and MOD-0220 (Legal Entity) as a **read-only external
   reference** only — never duplicated, never authored here.

## 2. Ownership and Boundaries

**In scope:**

- The real `IDataScopeResolver` implementation (new class in `Diten.Platform`, e.g.
  `OrgDataScopeResolver` / `TenantDataScopeResolver`) that consumes MOD-0288 org master data.
- The mapping from MOD-0288 structures to `EntitlementDataScope` values over the existing
  `EntitlementDataScopeKind` enum. **v1 emits exactly four kinds** (locked, §4): `OrgUnit`, `Position`,
  `ManagerChain`, and `LegalEntity` (AG-STEP-003 gate now **satisfied** — §7). No other kind is emitted and no enum value is
  invented — every other kind has no FU12 consumer field and/or no MOD-0288 backing.
- The DI seam swap: replace `services.AddScoped<IDataScopeResolver, NoOpDataScopeResolver>();`
  ([services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs:51](../../../../services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs)) with the real resolver registration (Scoped).
- Read-only consumption of the MOD-0288 Organization Unit tree, Position, effective-dated Position Assignment, and
  derived Manager Chain (as-of "now") to compute the user's scope set.
- Read-only consumption of the MOD-0288 `LegalEntityId` reference (already validated by MOD-0288 against MOD-0220)
  to emit a `LegalEntity` data scope — **without** re-validating, duplicating, or authoring Legal Entity data.
- The **business-module opt-in + fail-closed** consumption contract (acceptance criteria + design; no business
  module is wired by this pack).
- New unit/integration tests proving scope computation, empty/fail-closed behavior, memoization compatibility with
  FU12, and the DI swap.

**Out of scope:**

- Any change to the `IDataScopeResolver` **contract signature** or to `EntitlementDataScope` /
  `EntitlementDataScopeKind` (FU10a froze these; FU15 only **produces** values).
- Any change to the allow/deny authorization decision, `[HasPermission]`, `[RequiresModule]`, `[RequiresFeature]`,
  or the MOD-0018 handlers (FU12 already consolidated claim parsing; FU15 only fills org-scope fields).
- **MOD-0288** org master-data ownership, schema, CRUD, persistence, or migrations — owned by MOD-0288.
- **MOD-0220** Legal Entity ownership, persistence, lifecycle, API, validation re-implementation — MDM-owned.
- The **AG-STEP-004B** permission-key migration (PascalCase → `module.resource.action`) — separate milestone; see §14.
- Position → permission binding, role binding, or making Position a permission store (explicitly forbidden; see §8 / AD-1).
- Business-module repository/query row-level filtering implementation (each consuming module owns its own filter;
  FU15 only supplies the scope set + the opt-in/fail-closed contract).
- Seed data, migrations, frontend, gateway routing.
- Full ABAC engine / OPA / Cedar / policy DSL.
- Partner-admin / service-to-service runtime scope hardening (GAP-13-1 / S2S follow-ups).
- Field-level (column) access control.

## 3. Owned Objects

**New implementation type (owned by FU15):**

- Real `IDataScopeResolver` implementation class — `Diten.Platform` (Infrastructure or Application layer, decided at
  implementation start; lives where it can inject MOD-0288 org repositories, **not** in `Diten.Platform.Common`).
  The `Diten.Platform.Common` contract + `NoOpDataScopeResolver` default remain owned by MOD-0018-FU10a.

**Modified registration (single seam):**

- [services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs:51](../../../../services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs) —
  `IDataScopeResolver` Scoped registration changes from `NoOpDataScopeResolver` to the real implementation.

**Consumed, not owned (read-only):**

- MOD-0288 Organization Unit / Position / Position Assignment repositories (`Diten.Platform` domain/persistence).
- MOD-0288 derived Manager Chain contract (derived on-read from `Position.ReportsToPositionId`, cycle-safe, max
  depth 32 per MOD-0288 §4).
- The `LegalEntityId` reference already stored on MOD-0288 Organization Unit (validated by MOD-0288 against MOD-0220).

**Consumed contract (unchanged):**

- `IDataScopeResolver` / `EntitlementDataScope` / `EntitlementDataScopeKind` — frozen by FU10a.
- `ITenantAuthorizationContext` (FU12) — calls `IDataScopeResolver.ResolveAsync` inside `InitializeAsync()`.

## 4. Entity Fields

This pack adds **no persisted entity**. The table below documents the **resolver output contract** (already frozen
by FU10a) and the MOD-0288 **inputs** read to produce it.

**Contract (frozen — produced, not defined, by FU15):**

| Object | Field | Type | Notes |
|---|---|---|---|
| `IDataScopeResolver` | `ResolveAsync(tenantId, userId, moduleCode, featureCode, ct)` | `Task<IReadOnlyList<EntitlementDataScope>>` | Signature frozen by FU10a; FU15 returns real values instead of empty |
| `EntitlementDataScope` | `Kind` | `EntitlementDataScopeKind` | One of the enum kinds backed by MOD-0288 data in v1 |
| `EntitlementDataScope` | `ScopeId` | `Guid?` | e.g. Organization Unit Id / Position Id / Legal Entity Id |
| `EntitlementDataScope` | `ScopeCode` | `string?` | e.g. Org Unit `Code` / Position `Code` when useful for filters |
| `EntitlementDataScope` | `IsInclude` | `bool` | `true` for granted scope; exclusion semantics deferred unless a backed source exists |

**Inputs read from MOD-0288 (read-only):**

| Source (MOD-0288) | Field(s) used | Resolver use |
|---|---|---|
| Position Assignment | `UserId`, `PositionId`, `EffectiveFrom`, `EffectiveTo` | Resolve the user's active Position(s) as-of now (`[EffectiveFrom, EffectiveTo)`) |
| Position | `Id`, `Code`, `OrganizationUnitId`, `ReportsToPositionId` | Emit `Position` scope; resolve owning Org Unit; derive Manager Chain (Position reporting chain) |
| Organization Unit | `Id`, `Code`, `ParentOrganizationUnitId`, `LegalEntityId`, `IsArchived` | Emit `OrgUnit` scope as **own + subtree** (descendants pre-expanded into the flat `OrgUnitIds` list — see OD-FU15-1 locked); emit `LegalEntity` scope (gated, §7) |
| Manager Chain | derived from `Position.ReportsToPositionId` | Emit `ManagerChain` scope as a list of **Position IDs** in the user's Position reporting chain (cycle-safe, max depth 32) |

**Output kind coverage (v1 — LOCKED by AG-STEP-008 read-only audit).**

The **only** runtime consumer of resolver output is FU12 `JwtTenantAuthorizationContext.HydrateDataScopes`
([services/Diten.Platform/src/Diten.Platform.Infrastructure/Authorization/JwtTenantAuthorizationContext.cs:119](../../../../services/Diten.Platform/src/Diten.Platform.Infrastructure/Authorization/JwtTenantAuthorizationContext.cs)).
It binds exactly five kinds to context fields (`OrgUnit→OrgUnitIds`, `Position→PositionIds`,
`LegalEntity→LegalEntityId`, `Country→Country`, `ManagerChain→ManagerChain`); **every other enum value is silently
dropped** because no consumer field exists for it. v1 therefore produces only kinds that are both **backed by
MOD-0288** and **consumed by FU12**. No new enum value is invented.

**v1 producible kinds (emit):**

| `EntitlementDataScopeKind` | FU12 field | MOD-0288 backing | Notes |
|---|---|---|---|
| `OrgUnit` | `OrgUnitIds` | Organization Unit tree | **own + subtree**, descendants pre-expanded into the flat list (OD-FU15-1 locked) |
| `Position` | `PositionIds` | Position + effective-dated Position Assignment | user's active Position(s) as-of now |
| `ManagerChain` | `ManagerChain` | derived `GetManagerChain` (Position reporting chain) | list of **Position IDs** up the reporting chain; cycle-safe, max depth 32 |
| `LegalEntity` | `LegalEntityId` | Org Unit `LegalEntityId` (read-only MOD-0220 ref) | **AG-STEP-003 verified (§7) → emission enabled**; runtime stays fail-closed (non-referenceable → no scope) |

**Excluded from v1 (do NOT emit):**

| `EntitlementDataScopeKind` | Reason excluded |
|---|---|
| `Country` | FU12 **consumes** it, but MOD-0288 owns **no** Country master data (MOD-0288 §6); would always resolve empty → not produced in v1 |
| `Company`, `Own`, `Assigned`, `ProcessRelatedRecord`, `Department`, `Team`, `Region`, `RecordOwner` | **No FU12 consumer field** — output would be silently dropped (dead output). Deferred until a consumer field **and** a backing source exist |

> **OD-FU15-1 — RESOLVED (locked by AG-STEP-008 read-only audit):** `OrgUnit` scope is **own + subtree**. The
> resolver **pre-expands** the user's own Organization Unit plus its descendants into the existing flat
> `OrgUnitIds` (`IReadOnlyList<Guid>`) list, reusing MOD-0288's `ParentOrganizationUnitId` tree and existing
> cycle-safety. There is **no** subtree-marker field and **no** new `EntitlementDataScopeKind`. (The "support own
> and subtree as two separate scope kinds" option was rejected: it would require inventing an enum value.)

## 5. Repo Scope

**This milestone (AG-STEP-008, governance-only):**

- `execution/domains/platform-shared-services/module-packs/MOD-0018-FU15-real-data-scope-resolver.md` (this pack).

No other file is created or modified by AG-STEP-008. No staging, commit, push, PR, or merge.

**Future implementation repo scope (conceptual; applies only after `ready-for-dev`):**

- `services/Diten.Platform/src/Diten.Platform.Infrastructure/**` (or `.Application/**`) — real resolver implementation + DI wiring.
- `services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs` — the single DI seam swap.
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/**` — resolver + scope-mapping + DI swap tests.

Exact files are finalized at implementation start; listed for boundary visibility only.

## 6. Protected Paths

- `.antigravity/**` — global engineering system; not modified by this pack (incl. `permission-key-standard.md` / PKS-001).
- `services/Diten.Platform.Common/src/.../Authorization/IDataScopeResolver.cs` — **FU10a-frozen contract**; FU15 implements, never changes the signature.
- `services/Diten.Platform.Common/src/.../Authorization/EntitlementDataScope.cs` / `EntitlementDataScopeKind.cs` — FU10a-frozen; FU15 produces values, does not edit.
- `services/Diten.Platform.Common/src/.../Authorization/NoOpDataScopeResolver.cs` — retained as the default/fallback; FU15 does not delete it (kept for tests / anonymous / no-org tenants).
- `services/Diten.Platform.Common/src/.../Authorization/ITenantAuthorizationContext.cs` / `JwtTenantAuthorizationContext.cs` — **FU12-owned**; FU15 does not modify (the resolver is injected, not changed).
- MOD-0288 org master-data domain/persistence/features — **MOD-0288-owned**; FU15 reads via existing repositories, never edits schema/CRUD.
- `services/Diten.MdmService/**` — MOD-0220 Legal Entity; MDM-owned; FU15 never modifies.
- `services/Diten.AuthService/**` — Tenant User / Role / permission CRUD; not FU15.
- `gateway/Diten.ApiGateway/**/ocelot.json` — no gateway change.
- `frontend/Diten.Web/**` — no UI.
- `execution/registries/module-id-registry.md`, `execution/portfolio/**` roadmap — **not** modified by this pack (see §20 / handoff if a change ever seems required).

## 7. Dependencies

**Hard prerequisites (must be satisfied before FU15 code work starts):**

- **MOD-0018-FU10a (done):** `IDataScopeResolver` contract, `EntitlementDataScope`, `EntitlementDataScopeKind`,
  `NoOpDataScopeResolver` Scoped registration. FU15 implements this contract.
- **MOD-0018-FU12 (done):** `ITenantAuthorizationContext` / `JwtTenantAuthorizationContext`. FU12's
  `InitializeAsync()` already calls `IDataScopeResolver.ResolveAsync` once per request and memoizes the result;
  FU15 is the implementation that finally returns non-empty scopes. **FU15 must preserve FU12's call shape**
  (single call per request, memoization, fail-safe-on-throw) so no FU12 behavior changes.
- **MOD-0288 (done):** Organization Unit / Position / effective-dated Position Assignment / derived Manager Chain
  master data and repositories. FU15 reads these as the org-structure source of truth.

**AG-STEP-003 dependency — MOD-0220 LegalEntityId read-only contract — ✅ SATISFIED (verified present):**

- MOD-0220 (Corporate Secretarial / Entity Management, MDM Legal Entity Foundation) is a **read-only external
  reference** for FU15. FU15 consumes only the `LegalEntityId` already stored and validated on MOD-0288 records; it
  does **not** re-validate, duplicate, persist, or author Legal Entity data.
- **Gate status — SATISFIED by AG-STEP-003 (read-only audit, this branch).** The read-only lookup-validation
  contract is confirmed present and matching on **both** sides:
  - **Provider (MOD-0220, `Diten.MdmService`):** `GET /api/legal-entities/{id}/lookup-validation` →
    `ValidateLegalEntityReferenceQuery` → `ValidateLegalEntityReferenceHandler` → `RepositoryBase.GetByIdAsync`
    (`TenantFilter` enforces same-tenant + `IsDeleted == false`) + `LifecycleStatus == Active`; returns
    `LegalEntityLookupDto(LegalEntityId, LegalName, DisplayName, LifecycleState, Referenceable)`. This maps 1:1 to
    the MOD-0288 §7 locked validation (exists / same-tenant / ACTIVE / not-deleted) and return shape.
  - **Consumer (MOD-0288, `Diten.Platform`):** `ILegalEntityReferenceValidator` /
    `MdmLegalEntityReferenceValidator` (HTTP GET, **fail-closed** on non-2xx, ID-mismatch, non-ACTIVE,
    `Referenceable != true`, and network/JSON errors), registered Scoped with `TenantPropagationHandler` and
    consumed by `Create`/`UpdateOrganizationUnitCommandHandler`.
- **Governance outcome:** `LegalEntity`-scope emission is **ungated at the governance level** — no narrow MOD-0220
  follow-up pack is required. **Runtime behavior stays fail-closed regardless:** the resolver/consumer still treats
  a non-referenceable or unresolvable Legal Entity as "no scope" (zero rows), exactly as the verified consumer does.
- **Note (no rename here):** the provider permission `Modules.LegalEntity.Read` is PascalCase — an **AG-STEP-004B**
  permission-key migration target only; FU15 performs no key rename (§14).
- Ref: `execution/portfolio/access-governance-completion-plan.md` AG-STEP-003 (verified / complete).

**Critical guard inherited from MOD-0288 §7 (Tenant User reference):**

- `PositionAssignment.UserId` is an **external** AuthService Tenant User reference. MOD-0288 §7 states explicitly
  that **MOD-0018-FU15 must not consume Position Assignment `UserId` as authoritative** until the AuthService-owned
  read-only Tenant User validation contract exists and MOD-0288 Position Assignment integration validation is
  completed (tracked by MOD-0288-FU01). FU15 honors this: until that contract is confirmed, FU15 resolves scopes
  for the `userId` it is **given** by the trusted JWT/`ITenantAuthorizationContext` (already authenticated), but
  does **not** treat the MOD-0288 `UserId` linkage as an independent authority and does not bypass the FU01 gate.

**Downstream consumers (depend on FU15; not wired here):**

- Business modules (CRM / Track-H / any row-scoped module) — consume the resolved scope set via the **opt-in +
  fail-closed** contract (§16). Each owns its own repository/query filter.
- MOD-0018-FU14 (Effective Access Explain) — can surface the resolved scopes in an explain endpoint.

## 8. Runtime Constraints

- **Role ≠ Data Scope.** FU15 never produces an allow/deny *action* decision. It only computes the **row set**. The
  permission gate (`[HasPermission]`, JWT `permission` claim, PKS-001) is untouched; a user must still pass the
  action permission check **and** fall within a data scope for an opted-in module.
- **Position is not a permission store.** FU15 reads Position purely as an **organizational scope seat** to derive
  Org Unit / Manager Chain / Position scopes. It must **not** read or infer permissions from Position, and must not
  introduce any Position→permission binding (MOD-0288 AD-1 / DCP-001 AD-1).
- **Fail-closed.** For a business module that has **opted in** to data-scope enforcement, an empty resolved scope
  set means **zero rows**, never all rows. Resolver errors are fail-safe for the *authorization context* (FU12
  swallows resolver exceptions and keeps org fields empty) — and because the consuming module is fail-closed,
  empty-on-error collapses to zero rows, not an open door.
- **Opt-in.** Modules that have **not** opted in are unaffected (no silent global row filtering). Opt-in is explicit
  per module/feature; FU15 supplies the scope set and the contract, not an implicit global filter.
- **FU12 call-shape preserved.** `ResolveAsync` is invoked once per request by `InitializeAsync()` and memoized;
  FU15 keeps this. No `Task.GetAwaiter().GetResult()`, no per-property async.
- **Scoped lifetime.** The real resolver is registered **Scoped** (matching the NoOp registration and FU12's
  per-request context). Singleton is forbidden (per-request tenant/user data).
- **Effective-dated reads.** Position Assignment is read with `[EffectiveFrom, EffectiveTo)` as-of "now"; expired/
  future assignments do not contribute scope.
- **Tenant isolation.** All MOD-0288 reads are tenant-scoped by `tenantId`; cross-tenant resolution fails closed.
- **Manager Chain safety.** `ManagerChain` scope is the list of **Position IDs** in the user's Position reporting
  chain (derived on-read from `Position.ReportsToPositionId`, **not** Org Unit IDs). Cycle-safe, max depth 32
  (MOD-0288 §4); a detected cycle fails closed.
- **Org Unit scope shape (locked).** `OrgUnit` scope = **own + subtree**: the resolver pre-expands the user's own
  Organization Unit plus all descendants into the flat `OrgUnitIds` list. No subtree marker, no new enum value.
- **No-org tenants.** A tenant/user with no Position Assignment resolves to an empty scope set (→ zero rows for
  opted-in modules); this is valid, not an error.
- **LegalEntity gate (satisfied).** AG-STEP-003 has verified the MOD-0220 read-only contract (§7), so
  `LegalEntity`-scope emission is enabled; runtime stays fail-closed (a non-referenceable Legal Entity → no scope).

## 9. Layout & Shell Contract

`shell: none`. Backend / shared-runtime work.

- No Razor view, no `Layout = "..."`.
- No frontend route, DataTable, RESX, or Ctrl+K registry entry. `golden_reference: none` is therefore correct.

## 10. Backend File Convention

Not a CRUD/DataTable module, so the Golden Reference CQRS folder set does not apply. Minimal addition to existing
authorization/infrastructure structure:

- One new public resolver type in its own file, in `Diten.Platform` (Infrastructure or Application — wherever it can
  inject MOD-0288 org repositories), e.g. `OrgDataScopeResolver.cs`. **Not** in `Diten.Platform.Common`.
- Namespace follows the host project's existing authorization/infrastructure convention.
- DI registration is a one-line swap at
  [services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs:51](../../../../services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs).
- The FU10a contract (`Diten.Platform.Common.Authorization.IDataScopeResolver`) is consumed unchanged.

## 11. Frontend File Contract

No frontend files. No DataTable, no Razor partials, no RESX.

## 12. Validation Rules

| Rule | Applies to | Expected |
|---|---|---|
| Contract immutable | `IDataScopeResolver` signature | Unchanged from FU10a; FU15 only returns real values |
| Non-null result | `ResolveAsync` return | Always a non-null `IReadOnlyList<EntitlementDataScope>`; empty when no scope |
| Tenant isolation | All MOD-0288 reads | Scoped by `tenantId`; cross-tenant fails closed |
| Effective-dated | Position Assignment | Only `[EffectiveFrom, EffectiveTo)` active as-of now contributes |
| Manager Chain | derived chain | Cycle-safe; max depth 32; cycle → fail closed |
| Role ≠ scope | resolver output | No `EntitlementDataScope` ever encodes an action/permission |
| Position not a permission store | Position reads | Org/Manager/Position scope only; no permission inference |
| Fail-closed | opted-in consuming module | Empty scope set → zero rows; resolver throw → empty → zero rows |
| Opt-in | non-opted modules | Unaffected; no implicit global filter |
| LegalEntity gate | `LegalEntity` scope | AG-STEP-003 verified (§7) → emission enabled; non-referenceable Legal Entity → no scope (fail-closed) |
| Tenant User gate | Position Assignment `UserId` | Not treated as independent authority until MOD-0288-FU01 / AuthService contract confirmed |
| DI lifetime | resolver registration | Scoped (Singleton forbidden) |
| FU12 compatibility | `ResolveAsync` call | One call per request, memoized by FU12; behavior preserved |

## 13. Failure Path to Verify

- **User has no active Position Assignment** → empty scope set → opted-in modules return **zero rows** (fail-closed),
  not all rows.
- **Resolver throws (e.g. MOD-0288 read error)** → FU12 `InitializeAsync()` swallows fail-safe; org fields stay
  empty; opted-in module → zero rows. Authorization allow/deny unaffected.
- **Expired Position Assignment only** → contributes no scope (effective-dated filter).
- **Manager Chain cycle in MOD-0288 data** → cycle detection fails closed; partial chain up to the cycle, no infinite loop.
- **Cross-tenant Position/Org Unit lookup** → fails closed; no cross-tenant scope leaks.
- **Legal Entity not referenceable** (archived / soft-deleted / cross-tenant / `Referenceable != true`) → the
  MOD-0220 lookup-validation fails closed → `LegalEntity` scope **not** emitted (no assumption-based scope).
  (AG-STEP-003 verified the contract; this is the remaining *runtime* fail-closed path.)
- **MOD-0288 `UserId` linkage unverified (MOD-0288-FU01 open)** → resolver uses the authenticated `userId` from the
  trusted context; does not elevate the MOD-0288 `UserId` to an independent authority.
- **Module has not opted in** → no row filtering applied for it; resolver output simply unused (no silent breakage).
- **Anonymous / unauthenticated** → FU12 anonymous semantics; resolver yields empty; zero rows for opted-in modules.
- **Archived Org Unit / Position** → excluded from scope per MOD-0288 `IsArchived` semantics.

## 14. Authorization Convention

- FU15 produces **data scope**, not action permission. The action gate stays:
  `[HasPermission("<module.resource.action>")]` per **PKS-001** (`.antigravity/rules/permission-key-standard.md`),
  the canonical lowercase-dotted `module.resource.action` convention (OD-C). FU15 introduces **no** permission key.
- **PKS-001 reference (no migration here):** FU15 references PKS-001 as the permission-key authority but performs
  **no** key rename/alias work — that is **AG-STEP-004B** and is explicitly out of scope (§2). Any `[HasPermission]`
  string touched by future FU15 code uses the PKS-001 canonical form; FU15 does not migrate existing keys.
- Module gate `[RequiresModule(...)]` and feature gate `[RequiresFeature(...)]` — unchanged.
- Effective access (`module.feature`) entitlement remains MOD-0018-owned; FU15 only fills the **row** dimension.
- No new permission, endpoint, controller, or action is introduced by FU15.

## 15. Gateway / API Routing Decision

No gateway change. FU15 opens no public endpoint; it is internal runtime resolution behind the existing
authorization context. `gateway/Diten.ApiGateway/**/ocelot.json` is not modified. (If a future explain endpoint is
wanted, that is MOD-0018-FU14, not FU15.)

## 16. Acceptance Criteria

> Implementation acceptance criteria — apply only after the user promotes this pack to `approved` / `ready-for-dev`
> **and** the §7 dependency gates are satisfied. Authoring this pack (AG-STEP-008) creates none of the runtime below.

1. **DI swap:** `IDataScopeResolver` resolves to the **real** implementation (not `NoOpDataScopeResolver`) after
   Platform DI is built; registered **Scoped**. The existing `DataScopeResolverRegistrationTests` expectation is
   updated to assert the real type (behavior-preserving update at implementation time).
2. **Role/scope orthogonality:** no resolver output encodes an action/permission; allow/deny decisions of MOD-0018
   are byte-for-byte unchanged (existing handler/probe tests pass unchanged).
3. **Org Unit scope (own + subtree, locked):** a user with an active Position resolves to `OrgUnit` scope covering
   the user's own Organization Unit **and all descendants**, pre-expanded into the flat `OrgUnitIds` list,
   tenant-isolated. No subtree marker, no new enum value.
4. **Position scope:** the user's active Position(s) (effective-dated) are emitted as `Position` scope.
5. **Manager Chain scope:** emitted as the list of **Position IDs** in the user's Position reporting chain
   (derived from `Position.ReportsToPositionId`), cycle-safe, max depth 32 — not Org Unit IDs.
5a. **v1 kind set (locked):** the resolver emits **only** `OrgUnit`, `Position`, `ManagerChain`, and `LegalEntity`
   (gated). It emits **no** `Country` (no MOD-0288 backing) and **no** `Company`/`Own`/`Assigned`/
   `ProcessRelatedRecord`/`Department`/`Team`/`Region`/`RecordOwner` (no FU12 consumer field).
6. **LegalEntity scope (AG-STEP-003 verified):** emission is governance-ungated; uses the MOD-0288-stored
   `LegalEntityId` reference (validated via the MOD-0220 read-only lookup-validation contract), never
   re-validates/duplicates Legal Entity. Runtime stays fail-closed: a non-referenceable Legal Entity → no scope.
7. **Fail-closed:** for an opted-in module, an empty resolved scope set yields **zero rows**; never all rows.
8. **Opt-in:** modules that have not opted in are unaffected; no implicit global row filter is applied.
9. **Resolver fail-safe:** a resolver exception keeps FU12 org fields empty and does not change any allow/deny
   decision; combined with fail-closed, an opted-in module yields zero rows on error.
10. **Effective-dated correctness:** expired/future Position Assignments contribute no scope.
11. **Tenant isolation:** cross-tenant Org/Position/Assignment lookups fail closed; no scope leaks across tenants.
12. **Position is not a permission store:** no permission is read/inferred from Position; no Position→permission
    binding is added.
13. **Tenant User guard:** MOD-0288 Position Assignment `UserId` is not consumed as an independent authority until
    the AuthService Tenant User validation contract (MOD-0288-FU01) is confirmed.
14. **FU12 compatibility:** `ResolveAsync` is still called once per request and memoized; no FU12 test changes
    except those asserting non-empty org fields where NoOp previously returned empty.
15. **No-op default retained:** `NoOpDataScopeResolver` still exists and is still usable for tests / anonymous /
    no-org scenarios.
16. **PKS-001:** any `[HasPermission]` literal introduced (if any) uses the canonical `module.resource.action`
    form; no existing-key migration is performed (that is AG-STEP-004B).
17. **Build PASS:** `Diten.Platform.Common`, `Diten.Platform.API`, `Diten.Platform.Application.Tests`.
18. **All existing tests pass**, with only the DI-type and empty→non-empty org-field assertions updated.
19. **No `.antigravity`, registry, roadmap, seed, migration, gateway, or frontend change** in the implementation PR.

## 17. Test Expectations

**New / updated test classes (implementation time):**

- **`OrgDataScopeResolverTests`** (new) —
  - User with one active Position → correct `OrgUnit` (own + subtree) + `Position` scopes.
  - **Org Unit subtree expansion** → user's own unit and all descendants present in `OrgUnitIds`; sibling/unrelated
    units absent.
  - User with multiple concurrent Positions → union of scopes.
  - User with no active Position → empty list.
  - Expired-only assignment → empty list.
  - Manager Chain derivation → chain emitted as **Position IDs** up the reporting chain; cycle → fail-closed partial.
  - Cross-tenant data → no leak (tenant isolation).
  - Archived Org Unit / Position → excluded.
  - `LegalEntity` scope: referenceable Legal Entity → scope emitted; non-referenceable (archived/deleted/
    cross-tenant) → fail-closed, no scope (test both states). AG-STEP-003 contract verified.
  - **Kind-set guard:** resolver output contains **only** `OrgUnit`, `Position`, `ManagerChain`, `LegalEntity`
    (gated) — never `Country` or any other kind.
  - Resolver internal read error → fail-safe empty (no throw escaping to authorization decision).
- **`DataScopeResolverRegistrationTests`** (update) — assert `IDataScopeResolver` resolves to the **real** type,
  Scoped, distinct per scope.
- **FU12 integration** (`TenantAuthorizationContextDataScopeIntegrationTests`, existing) — update the
  NoOp-returns-empty assertions to the real resolver returning populated org fields after `InitializeAsync()`;
  memoization (single call) preserved.
- **Fail-closed consumer contract test** — a representative opted-in consumer with empty scope returns zero rows;
  a non-opted consumer is unaffected.

**Build / smoke commands:**

- `dotnet build services/Diten.Platform.Common/src/Diten.Platform.Common/Diten.Platform.Common.csproj -c Debug`
- `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug`
- `dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests --filter FullyQualifiedName~Authorization`
- `dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests`

**Frontend / Browser smoke:** N/A (no UI). **RESX parity:** N/A. **DataTable verifier:** N/A (`golden_reference: none`).

## 18. Ready-for-dev Checklist

> Governance gate completed at AG-STEP-008. The pack is promoted `draft → ready-for-dev`: all design/scope
> decisions are locked. Two items are **intentionally deferred** (resolver host layer = implementation-start
> decision; pilot consuming-module binding = downstream) and do not block ready-for-dev. Two runtime prerequisites
> (`AG-STEP-003`, `MOD-0288-FU01`) remain **fail-closed gates before code consumes the gated outputs** — satisfied
> here by the pack's explicit deferral/guard, not by their completion.

- [x] User reviewed this draft and approved scope/boundaries (AG-STEP-008 review + revision).
- [x] Status promoted to `ready-for-dev`.
- [x] **AG-STEP-003 gate SATISFIED** — MOD-0220 read-only `LegalEntityId` contract **verified present both sides**
      (read-only audit, §7); `LegalEntity`-scope is now **ungated at governance level**, runtime stays fail-closed.
- [x] **MOD-0288-FU01** consumption rule confirmed and recorded — Position Assignment `UserId` is not treated as an
      independent authority until the AuthService Tenant User validation contract is confirmed (§7, §16 AC#13).
- [x] FU10a contract immutability + FU12 call-shape preservation confirmed (AG-STEP-008 audit against runtime).
- [x] OD-FU15-1 (Org Unit scope shape) decided — **own + subtree, pre-expanded into flat `OrgUnitIds`** (AG-STEP-008 audit).
- [x] Output kind coverage (§4 table) confirmed — v1 emits **`OrgUnit`, `Position`, `ManagerChain`, `LegalEntity`** (gated) only (AG-STEP-008 audit).
- [ ] Resolver host layer (Infrastructure vs Application) — **deferred to implementation start** (§3/§10); not a ready-for-dev blocker.
- [x] DI Scoped swap + NoOp-retention confirmed (§3, §8, §16 AC#1/#15).
- [ ] Fail-closed + opt-in consumer contract — **downstream**: pilot consuming-module owner sign-off happens when the first module opts in (§16 AC#7/#8, §20); the contract itself is locked here.
- [x] AG-STEP-004B (permission-key migration) confirmed out of scope for FU15 (§2, §14).

## 19. Implementation Notes

- **Why a single DI seam:** FU12 already routes every authorization context through `IDataScopeResolver`; FU15 is
  intentionally a **drop-in implementation swap** so the blast radius is one registration line plus a new class.
  This is the payoff of FU12's design (org fields placeholdered behind the resolver).
- **Why `Diten.Platform`, not `Diten.Platform.Common`:** the real resolver must inject MOD-0288 org repositories,
  which live in `Diten.Platform`. The `Common` contract + NoOp stay where they are; `Common` must not depend on
  Platform persistence.
- **Role vs Data Scope (the core invariant):** keeping these orthogonal is the whole point of the Access Governance
  capability — a user's *permissions* say what actions are allowed; the *data scope* says on which rows. FU15 owns
  only the second axis. Mixing them (e.g. deriving permissions from Position) is explicitly forbidden (AD-1).
- **Fail-closed + opt-in together:** fail-closed without opt-in would silently break every existing module that
  reads rows; opt-in without fail-closed would leak rows when scope is empty. Both are required and both belong in
  the consuming module's contract, supplied by FU15.
- **AG-STEP-003 coupling — resolved:** the gate was held until a formal read-only audit (not just the runtime
  `ValidateLegalEntityReferenceQuery` evidence) verified the MOD-0220 contract on both provider and consumer sides.
  That audit is **complete**: the contract is present and matching, so `LegalEntity`-scope is governance-ungated.
  This honored "verify, do not assume" — emission was never shipped on assumption (per the Access Governance
  completion plan, AG-STEP-003 row, now `verified / complete`).
- **NoOp retained, not deleted:** anonymous requests, background jobs, and tenants with no org structure still need
  a valid empty resolution; the NoOp resolver remains the natural fallback and the existing FU12 anonymous tests
  keep using it.

## 20. Follow-up Items

- **OD-FU15-1:** RESOLVED (AG-STEP-008 read-only audit) — `OrgUnit` scope = **own + subtree**, pre-expanded into
  the flat `OrgUnitIds` list; no subtree marker, no new enum value.
- **AG-STEP-003:** MOD-0220 read-only `LegalEntityId` contract verification — **COMPLETE / verified present both
  sides**; `LegalEntity`-scope is governance-ungated (runtime stays fail-closed). No follow-up pack needed.
- **MOD-0288-FU01:** AuthService Tenant User validation contract — guards Position Assignment `UserId` consumption.
- **AG-STEP-004B:** permission-key migration (PascalCase → PKS-001 `module.resource.action`) — separate milestone;
  FU15 only references PKS-001, performs no migration.
- **MOD-0018-FU14:** Effective Access Explain — can surface FU15 resolved scopes in an explain endpoint.
- **Business-module pilot:** first opted-in consumer (e.g. CRM / Track-H) to validate the fail-closed + opt-in
  contract end-to-end; owns its own repository/query filter.
- **Additional scope kinds:** `Company` / `Country` / `Department` / `Team` / `Region` and exclusion (`IsInclude=false`)
  semantics — deferred until a backed MOD-0288 (or other) source exists.
- **Partner-admin / S2S runtime scope:** GAP-13-1 and service-actor scope hardening — separate follow-ups.
