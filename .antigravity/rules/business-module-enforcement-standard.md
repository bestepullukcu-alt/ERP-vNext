---
description: "BME-001 — Business-Module Authorization Enforcement Standard (mandatory [HasPermission] on new business-domain endpoints, default-deny, tenant isolation). Produced by AG-STEP-006A. Basis for the AG-STEP-013 business-module enforcement contract."
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
| Consumed by | **AG-STEP-013** — business-module enforcement contract (§9 of the Access Governance plan) uses this standard as its normative basis |

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

**AG-STEP-013** (Access Governance plan §9 — "Business-module enforcement contract") will codify the uniform
contract every business module must satisfy at runtime. That contract is **built on this standard**: BME-001 is the
normative source for the "every privileged endpoint is `[HasPermission]`-gated, default-deny, tenant-isolated, UX is
not enforcement" requirements. AG-STEP-013 adds the reference implementation and the verification gate; it does not
restate or supersede BME-001's rule — it cites it.

No additional dependency is required for AG-STEP-013 to consume this standard: PKS-001 (format) is locked, FU15
(data-scope resolver) is implemented on the integration branch, and SEC-001 / RULE-002 already provide the
default-deny and tenant-isolation substrate. AG-STEP-013 remains a separate authoring step.

---

## 6. Review checklist (new business-domain module)

- [ ] Every state-changing endpoint (`POST`/`PUT`/`PATCH`/`DELETE` + bulk) has `[HasPermission("module.resource.action")]`.
- [ ] Every business-data `GET` (list/details/export/lookup over business rows) has `[HasPermission(...)]`.
- [ ] All permission keys are PKS-001 lowercase-dotted, ≥ 3 segments; no PascalCase / `Modules.*` / `Platform.*` for new keys.
- [ ] Write endpoints are `[Authorize]` (default-deny); any anonymous endpoint is `[AllowAnonymous]` + documented rationale.
- [ ] `tenant_id` claim ↔ `X-Tenant-Id` enforced; data layer applies the tenant filter (RULE-002).
- [ ] No frontend hide/show is relied on as a security control; backend gate holds for direct API calls.
- [ ] The module pack §14 (Authorization Convention) declares exactly the permission keys the endpoints enforce.
- [ ] No existing endpoint retrofit (AG-STEP-006B) and no key rename/migration (AG-STEP-004B) was performed.

---
Diten ERP vNext — Business-Module Authorization Enforcement Standard · BME-001 · produced by AG-STEP-006A · basis for AG-STEP-013
