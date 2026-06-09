---
description: "BME-001 — Business-Module Authorization Enforcement Standard. Part I (AG-STEP-006A): mandatory [HasPermission] on new business-domain endpoints, default-deny, tenant isolation. Part II (AG-STEP-013): the uniform business-module enforcement contract + repo-grounded reference pattern (permission gate + tenant filter + EffectiveScopes row filter + empty-scope fail-closed)."
---

# Business-Module Authorization Enforcement Standard — BME-001

> **Scope of this document.** This standard locks the **enforcement rule** for **new business-domain modules**:
> every privileged backend endpoint must be guarded by an `[HasPermission(...)]` attribute from its **first
> commit**. It is a **forward-looking coding standard** — it governs how new modules are written. It is **not** a
> retrofit, migration, rename, seed, or runtime change of any existing surface.
>
> **Companion authorities.** This standard composes with, and does not replace:
> - **PKS-001** (`permission-key-standard.md`) — the **canonical permission-key format** authority (lowercase-dotted
>   `module.resource.action`). BME-001 governs *that a key is enforced*; PKS-001 governs *how the key string is
>   spelled*.
> - **SEC-001** (`security-jwt.md`) — JWT authentication, the default-deny baseline, `[Authorize]` on write
>   endpoints, and the `TenantId`/`X-Tenant-Id` isolation rule.
> - **RULE-002** (`multi-tenancy.md`) — single-DB tenant isolation at the data layer.
> - **module-pack-standard.md** §14 *Authorization Convention* — the per-module permission list a pack must declare.
>
> **Authority order is unchanged:** Module Pack > Domain Config > AGENTS.md > `.antigravity` standards > live
> governance records.

---

## 0. Decision record — AG-STEP-006A (locked)

| Field | Value |
|---|---|
| Decision | **Mandatory `[HasPermission]` enforcement for new business-domain modules** |
| Outcome | Every privileged backend endpoint of a **new** business-domain module carries `[HasPermission("<module>.<resource>.<action>")]` from the **first commit**; default-deny; tenant isolation mandatory |
| Key format | **PKS-001** — lowercase-dotted `module.resource.action`, ≥ 3 segments |
| Confirmed by | User / EA (AG-STEP-006A) |
| Explicitly NOT in scope | Retrofitting existing AuthService / MDM / Platform controllers (**AG-STEP-006B**); permission-key rename/alias migration (**AG-STEP-004B**) |
| Consumed by | **AG-STEP-013** — the business-module enforcement contract (§9 of the Access Governance plan) is now **defined in Part II of this document** with a repo-grounded reference pattern |

---

## 1. Who this applies to

**In scope — new business-domain modules.** Any module authored after this standard lands whose backend serves
tenant business data (e.g. CRM, inventory, finance, HR, Track-H domains). A module is "new" for this rule if its
endpoints did not exist before this standard.

**Out of scope (this step):**
- **Existing** AuthService / MDM / Platform controllers and any already-shipped endpoint — their retrofit is
  **AG-STEP-006B** and must not be done here.
- **Renaming / aliasing** existing PascalCase keys (`Modules.*`, `Platform.*`) to the PKS-001 form — that is
  **AG-STEP-004B** and must not be done here.

This standard never authorizes editing runtime controllers, seeds, migrations, or tests. It is documentation that
new code must satisfy.

---

## 2. The rule (normative)

For every **privileged backend endpoint** of a new business-domain module:

1. **Mandatory attribute from first commit.** The endpoint (controller action / minimal-API handler) carries
   `[HasPermission("<module>.<resource>.<action>")]`. There is no "add it later" — an endpoint without its
   permission gate is an **incomplete** endpoint and fails review.
2. **Key format follows PKS-001.** The key is **lowercase-dotted**, `module.resource.action`, with **≥ 3 segments**
   (nesting allowed). The first segment is the owning module namespace; the last is the action. No PascalCase, no
   `Modules.*` / `Platform.*` legacy prefixes for new keys.
3. **Default-deny is the substrate.** Per SEC-001, all `POST`/`PUT`/`PATCH`/`DELETE` endpoints are `[Authorize]` by
   default; opening anything to anonymous access requires an explicit, documented architectural exception
   (e.g. `/health`). `[HasPermission]` is **in addition to** `[Authorize]`, not a replacement.
4. **Tenant isolation is mandatory.** The JWT `tenant_id` claim must match the request `X-Tenant-Id` header, and the
   data layer must apply the tenant filter (RULE-002). A passing permission check **never** widens row visibility
   across tenants.
5. **Frontend visibility is UX-only.** Hiding a button, menu item, or route in the UI is a usability affordance —
   **never** a security control. The backend `[HasPermission]` gate is the sole authority; the same check must hold
   even when the request bypasses the UI (direct API call, scripted client, replayed request).

### Privileged endpoint — definition

- **Always privileged:** every state-changing endpoint — `POST`, `PUT`, `PATCH`, `DELETE`, and bulk variants.
- **Privileged reads:** any `GET` that returns tenant business data or is otherwise sensitive (lists, details,
  exports, lookups over business records). A read that exposes business rows is gated.
- **Genuinely public** endpoints (health/readiness, anonymous auth handshakes) are the **documented exception** and
  carry an explicit `[AllowAnonymous]` + a one-line rationale, not silence.

### Canonical examples (PKS-001 form)

```csharp
[HasPermission("crm.lead.create")]        // POST   /api/crm/leads
[HasPermission("crm.lead.read")]          // GET    /api/crm/leads (business rows → gated)
[HasPermission("crm.lead.update")]        // PUT    /api/crm/leads/{id}
[HasPermission("crm.lead.delete")]        // DELETE /api/crm/leads/{id}
[HasPermission("crm.lead.bulk-delete")]   // DELETE /api/crm/leads/bulk
[HasPermission("inventory.stock-item.read")]
```

> ⚠️ **Do not** write new keys as `Modules.Lead.Delete` or `Platform.Lead.Delete`. Those PascalCase forms are
> **legacy migration targets** owned by AG-STEP-004B — not the format for new code.

---

## 3. Role vs Data Scope (orthogonality)

This standard governs the **action** axis only. It composes with — and never substitutes for — the **data-scope**
axis:

- **Role / permission (this standard):** *what action* a user may perform. Owned by MOD-0018, carried in the JWT
  `permission` claim, checked by `[HasPermission]`.
- **Data scope (MOD-0018-FU15 `IDataScopeResolver`):** *which rows* a user may perform it on. Owned by the
  data-scope resolver and consumed by `ITenantAuthorizationContext`.

A request must pass the `[HasPermission]` action gate **and** fall within the resolved data scope (for an opted-in
module). Position is **not** a permission store — permissions are never derived from organizational position.

---

## 4. What this standard does NOT do

- It does **not** retrofit existing controllers (AuthService / MDM / Platform) — **AG-STEP-006B**.
- It does **not** rename, alias, or migrate any existing permission key — **AG-STEP-004B** owns the
  `Modules.*`/`Platform.*` → `module.resource.action` migration and the compatibility map.
- It does **not** add, change, or remove any runtime code, seed, migration, DTO, gateway route, frontend, or test.
- It does **not** mint any new `MOD-xxxx` identifier.

If a new module's required permission keys are not yet declared in its module pack §14, the module pack must be
revised first (module-pack-standard.md §14) — code does not invent enforcement keys outside the pack.

---

## 5. Relationship to AG-STEP-013 (business-module enforcement contract)

**AG-STEP-013** (Access Governance plan §9 — "Business-module enforcement contract") codifies the uniform contract
every new business module must satisfy at runtime. That contract is **defined in Part II below** and is built
directly on Part I: every privileged endpoint is `[HasPermission]`-gated, default-deny, tenant-isolated, and UX is
never enforcement.

No additional dependency was required: PKS-001 (key format) is locked, FU15 (`OrgDataScopeResolver`) is implemented
on the integration branch and hydrates `ITenantAuthorizationContext`, and SEC-001 / RULE-002 provide the
default-deny and tenant-isolation substrate. Part II only *cites and composes* these — it adds no runtime code.

---

## 6. Review checklist — Part I (new business-domain module)

- [ ] Every state-changing endpoint (`POST`/`PUT`/`PATCH`/`DELETE` + bulk) has `[HasPermission("module.resource.action")]`.
- [ ] Every business-data `GET` (list/details/export/lookup over business rows) has `[HasPermission(...)]`.
- [ ] All permission keys are PKS-001 lowercase-dotted, ≥ 3 segments; no PascalCase / `Modules.*` / `Platform.*` for new keys.
- [ ] Write endpoints are `[Authorize]` (default-deny); any anonymous endpoint is `[AllowAnonymous]` + documented rationale.
- [ ] `tenant_id` claim ↔ `X-Tenant-Id` enforced; data layer applies the tenant filter (RULE-002).
- [ ] No frontend hide/show is relied on as a security control; backend gate holds for direct API calls.
- [ ] The module pack §14 (Authorization Convention) declares exactly the permission keys the endpoints enforce.
- [ ] No existing endpoint retrofit (AG-STEP-006B) and no key rename/migration (AG-STEP-004B) was performed.

---

# Part II — AG-STEP-013 Business-Module Enforcement Contract

> **What Part II is.** The uniform runtime contract every **new business-domain module** must satisfy when it serves
> tenant business data, plus a **repo-grounded reference pattern**. It is documentation only — it composes existing
> runtime building blocks (FU12 `ITenantAuthorizationContext`, FU15 `OrgDataScopeResolver`,
> `HasPermissionAttribute`, the tenant-scoped repository base) and introduces **no** runtime code, controller
> retrofit, key migration, seed, migration, or test.

## 7. The enforcement contract (normative clauses)

A new business module that serves tenant business data MUST satisfy every clause below.

**C1 — Action gate on every privileged endpoint.** Each privileged endpoint (see Part I §2 for the definition)
carries `[HasPermission("<module>.<resource>.<action>")]` (the `Diten.Platform.API.Security.HasPermissionAttribute`
filter), key spelled per PKS-001. The action gate runs first; a failed action check denies before any data is read.

**C2 — Scoped resources opt in explicitly and consume the existing scope output.** Row-level data scoping is
**opt-in per resource**: a module declares (in its module pack) which resources are data-scoped. For a scoped
resource the module **MUST consume the existing scope output** and **MUST NOT author its own data-scope resolver or
re-derive organization structure**:
- the canonical surface is **FU12 `ITenantAuthorizationContext`** — after `await InitializeAsync(ct)` it exposes the
  hydrated `OrgUnitIds`, `PositionIds`, `LegalEntityId`, `ManagerChain` (sourced from the single
  `IDataScopeResolver` / FU15 `OrgDataScopeResolver`);
- equivalently, when a module performs an explicit entitlement check, it consumes
  **`EntitlementCheckResult.EffectiveScopes`** (`IReadOnlyList<EntitlementDataScope>`).
- Writing a second resolver, querying MOD-0288 org tables directly for scope, or inferring scope from any other
  source is **forbidden**.

**C3 — Empty scope is fail-closed.** For a **scoped** (opted-in) resource, an **empty** resolved scope set returns
**zero rows**. **Auto-open is forbidden** — a module must never interpret "no scope" as "all rows". The default for
a scoped resource with no matching scope is deny/empty, not allow.

**C4 — Tenant isolation is server-side only.** `TenantId` is taken **only** from the server-side context
(`ITenantAuthorizationContext.TenantId` / `ITenantContext.TenantId`, resolved from the JWT `tenant_id` claim, which
SEC-001 reconciles with `X-Tenant-Id`). A `TenantId` supplied in the **request body, query string, or route is
never accepted**. All reads are tenant-scoped at the data layer (the tenant-scoped repository base applies
`TenantId == context AND IsDeleted == false`, RULE-002).

**C5 — Row-level filters use only the supported scope kinds.** Row filtering draws **only** from the
v1-supported `EntitlementDataScopeKind` values that FU15 emits / FU12 hydrates:
- **`OrgUnit`** — `OrgUnitIds` (own + subtree, already pre-expanded into a flat list by the resolver);
- **`LegalEntity`** — `LegalEntityId`;
- **`ManagerChain`** — `ManagerChain` (Position IDs up the reporting chain);
- **`Position`** — `PositionIds`.

No other kind is used as a row filter in v1 (e.g. `Country` has no backing and is not a row filter). Modules do
**not** invent new scope kinds or enum values.

**C6 — Permission and Data Scope are separate axes.** They are checked independently and both must pass:
- **Permission** (`[HasPermission]`) = *the right to perform the action*.
- **Data Scope** (`EffectiveScopes` / hydrated context lists) = *which records the action may touch*.
A user with `crm.lead.read` still sees only the leads inside the resolved scope; a user inside a scope but without
the permission is denied the action. Position is never a permission store — permissions are not derived from
organizational position.

**C7 — Frontend visibility is UX only.** Hiding a control in the UI is never the enforcement. C1–C6 must hold for
direct API calls that bypass the UI (scripted client, replayed request). The backend is the sole authority.

**C8 — Audit / Explain / Cache hooks are deferred to later gates.** This contract is the enforcement substrate;
the following are **referenced, not implemented here**:
- **Allow/deny audit + Explain Access** (decision trace, `EntitlementResolutionSource`) → **AG-STEP-011 /
  MOD-0018-FU14**.
- **Scope/permission cache invalidation** on role/org change events → **AG-STEP-010 / MOD-0018-FU13**.
A module leaves these as integration points (it emits through the existing `EntitlementCheckResult` /
`ITenantAuthorizationContext` surfaces) and does not hand-roll its own audit or cache.

## 8. Reference implementation pattern (repo-grounded, illustrative)

Representative read flow for a scoped resource. **Illustrative only — do not create this as a runtime file.** Symbols
are the real ones: `HasPermissionAttribute` (`Diten.Platform.API.Security`), `ITenantAuthorizationContext`
(`Diten.Platform.Common.Authorization`), `EntitlementDataScopeKind`, and the tenant-scoped repository base.

```text
1. REQUEST           GET /api/crm/leads          (no TenantId in body/query/route — C4)
2. PERMISSION GATE   [HasPermission("crm.lead.read")]  → 401 if unauthenticated, 403 if key missing  (C1)
3. HANDLER           inject ITenantAuthorizationContext ctx  (+ tenant-scoped repository)
                     await ctx.InitializeAsync(ct);          // hydrate scopes once (FU12 memoized/fail-safe)
4. TENANT FILTER     repository reads are tenant-scoped by ctx.TenantId (server-side) — never from request  (C4)
5. SCOPE FILTER      if resource is scoped (opted-in):
                         scope = { OrgUnitIds, LegalEntityId, ManagerChain, PositionIds }   // supported kinds (C5)
                         if scope is empty  →  return EMPTY result   // fail-closed, auto-open forbidden  (C3)
                         else               →  apply scope as a row filter (e.g. OrgUnitId ∈ OrgUnitIds)
                     else (non-scoped resource): tenant filter alone applies
6. RESPONSE          return only rows passing (tenant filter ∧ scope filter)
```

Sketch (C#-shaped pseudocode; not a runtime artifact):

```csharp
// API layer — action gate (C1), default-deny (SEC-001)
[Authorize]
[HasPermission("crm.lead.read")]
public Task<IActionResult> GetLeads(CancellationToken ct) => /* dispatch query */;

// Application layer — scoped read handler
public async Task<Response<IReadOnlyList<LeadDto>>> Handle(GetLeadsQuery q, CancellationToken ct)
{
    await _ctx.InitializeAsync(ct);                       // FU12: once-per-request, memoized, fail-safe

    // Platform/partner actors are not tenant-row-scoped; tenant_user IS scoped.
    var scoped = !_ctx.IsPlatformAdmin;                   // business resources opt in; this resource is scoped

    if (scoped && _ctx.OrgUnitIds.Count == 0
               && _ctx.PositionIds.Count == 0
               && _ctx.LegalEntityId is null
               && _ctx.ManagerChain.Count == 0)
    {
        return Response<IReadOnlyList<LeadDto>>.Success(Array.Empty<LeadDto>());   // C3 empty-scope fail-closed
    }

    // Repository is tenant-scoped by _ctx.TenantId server-side (C4); TenantId never read from the request.
    var rows = await _leads.QueryAsync(
        // C5: filter only by supported scope kinds
        orgUnitIds:   _ctx.OrgUnitIds,
        positionIds:  _ctx.PositionIds,
        legalEntityId:_ctx.LegalEntityId,
        managerChain: _ctx.ManagerChain,
        applyScope:   scoped,
        ct: ct);

    return Response<IReadOnlyList<LeadDto>>.Success(rows);
}
```

Notes: the handler **consumes** `ITenantAuthorizationContext` (it does not build a resolver — C2); `TenantId` is
never a parameter of the query (C4); an empty scope returns empty, never unfiltered (C3); only the four supported
kinds drive the filter (C5).

## 9. Review checklist — Part II (scoped business resource)

- [ ] Action gate `[HasPermission("module.resource.action")]` on every privileged endpoint (C1).
- [ ] Scoped resources are declared opt-in in the module pack and consume `ITenantAuthorizationContext` /
      `EffectiveScopes`; **no module-authored resolver** and no direct org-table scope derivation (C2).
- [ ] Empty resolved scope → zero rows for scoped resources; **no auto-open** (C3).
- [ ] `TenantId` only from server-side context; **rejected** from body/query/route; reads tenant-scoped (C4).
- [ ] Row filters use only `OrgUnit` / `LegalEntity` / `ManagerChain` / `Position`; no new scope kinds (C5).
- [ ] Permission and data scope checked independently, both required (C6).
- [ ] No frontend hide/show relied on as enforcement (C7).
- [ ] Audit/Explain (AG-STEP-011/FU14) and cache invalidation (AG-STEP-010/FU13) left as referenced integration
      points, not hand-rolled (C8).

---
Diten ERP vNext — Business-Module Authorization Enforcement Standard · BME-001 · Part I produced by AG-STEP-006A · Part II (enforcement contract) produced by AG-STEP-013
