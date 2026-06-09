---
description: "PKS-001 — Diten ERP vNext Permission-Key Standard (canonical RBAC permission-key convention + existing-key catalog baseline). Produced by AG-STEP-004."
---

# Permission-Key Standard — PKS-001

> **Scope of this document.** This is the single canonical convention for **RBAC permission keys** — the strings carried by the JWT `permission` claim and checked by `[HasPermission("…")]` / the per-service `PermissionAuthorizationHandler`. It locks the convention and records the existing-key catalog baseline. It is a **governance decision + catalog only**: it introduces **no runtime, schema, seed, DTO, endpoint, frontend, gateway or build change**. The actual key migration (renames, alias wiring, validator widening, standards edits) is the separate implementation milestone **AG-STEP-004B**.
>
> **Canonical capability scope.** This standard governs the permission-key convention under **MOD-0018** (Permission evaluation / RBAC). It introduces **no new MOD-xxxx identifier**.
>
> **Authority.** This file is a `.antigravity` coding standard. On the format of RBAC permission keys it is **authoritative and supersedes** the PascalCase examples/rules in `erp-architecture.md` (§"RBAC Permission Key Formatı"), `module-pack-standard.md` (Authorization Convention), `security-jwt.md` (SEC-001 example) and `response-envelope.md`. Those documents are reconciled to this standard as part of AG-STEP-004B (see §7). The repository authority order is otherwise unchanged: Module Pack > Domain Config > AGENTS.md > .antigravity standards > live governance records.

---

## 0. Decision record — OD-C (locked)

| Field | Value |
|---|---|
| Decision | **OD-C — canonical RBAC permission-key format** |
| Outcome | **(a) `module.resource.action`, lowercase-dotted, hyphen-in-segment, hierarchical depth ≥ 3 permitted** |
| Confirmed by | User / EA (AG-STEP-004) |
| Rejected alternative | (b) PascalCase two-format (`Platform.{Resource}.{Action}` / `Modules.{Module}.{Action}`) |
| Why | Matches the **runtime enforcement that already ships**: `Permission.Key` is hard-forced to `…ToLowerInvariant()`, the JWT `permission` claim is therefore always lowercase-dotted, and the `PermissionAuthorizationHandler` does an exact **case-sensitive** match. Format (a) needs **no permission-catalog / claim data migration**; only the PascalCase `[HasPermission]` attributes and the Pascal-mandating standards are reconciled (AG-STEP-004B). |
| Implication | The 56 PascalCase `[HasPermission]` keys and the underscore/`Modules.*`/deep deviations become **migration targets** for AG-STEP-004B; the Pascal rule in the three standards above is **superseded** by this file. |

---

## 1. Canonical format (locked)

A permission key is a **dot-separated, all-lowercase** string with **three or more segments**:

```
<module>.<resource>[.<sub-resource> …].<action>
```

- **First segment** = the owning **module namespace** (see §4).
- **Last segment** = the **action**, drawn from the action dictionary (see §5).
- **Middle segment(s)** = the **resource path** (one or more; nesting allowed).

**Segment grammar** (each segment): `^[a-z][a-z0-9-]*$`

- lowercase ASCII only; must start with a letter;
- digits allowed after the first character;
- **hyphen `-`** is the multi-word separator inside a segment (e.g. `legal-entities`, `assign-role`, `bulk-delete`). **Underscore `_` is not permitted.**
- no leading/trailing/double hyphen.

**Whole-key rules**

- total length ≤ **200** characters;
- **≥ 3** segments (`module.resource.action` is the minimum; deeper resource paths such as `platform.tenants.commercial.subscription.activate` are valid);
- case-sensitive at the enforcement layer — **always emit and store lowercase** (the `Permission` entity already enforces this).

> **Relationship to the existing ModulePage validator.** `ModulePageDescriptorNormalizer` + `IsCanonicalPermission` already enforce this *shape* for module-page action descriptors, but require **exactly 3** segments. This standard **relaxes that to ≥ 3** so that legitimately nested keys validate. Widening that validator is an AG-STEP-004B task (see §6 / §7).

**Canonical examples**

```
auth.users.create
auth.roles.assign-permission
mdm.legal-entities.bulk-delete
platform.administrators.read
platform.tenants.quotas.view
platform.tenants.commercial.subscription.activate
strategy.goal.read
```

---

## 2. Immutable-key rule (locked)

Once a permission key has been **published** — seeded into the catalog, granted via a role-permission row, emitted in a JWT claim, or enforced by a shipped `[HasPermission]` — its **meaning is frozen**. A published key is **never silently repurposed** to mean a different capability.

- To change what a capability covers, **mint a new key** and deprecate the old one via an alias (§3).
- Widening/narrowing the access a key grants without renaming it is **forbidden** (it would silently change every existing grant).
- Deleting a published key is a breaking change and follows the deprecation lifecycle (§3), not an in-place edit.

---

## 3. Deprecated-alias rule (locked)

Old/legacy keys are **retained as deprecated aliases** that map to their canonical replacement; they are never hard-deleted in the same change that introduces the canonical key.

- A **compatibility map** (`alias → canonical`) is the single source of truth for the migration and is owned by AG-STEP-004B.
- During migration the enforcement/seeding layer resolves alias → canonical (**dual-read**: a grant of either the alias or the canonical key satisfies the check) so no tenant loses access mid-migration.
- Aliases are **time-boxed**: an alias is removed only after **every** surface (seed, role-permission rows, JWT issuance, `[HasPermission]`, frontend, tests, audit references) has migrated to the canonical key, verified by the AG-STEP-004B exit criteria.
- Aliases are **directional and non-transitive**: an alias points to exactly one canonical key; canonical keys are never aliased to other canonical keys.

---

## 4. Module ownership (locked)

Each **namespace** (first segment) is owned by **exactly one** module/service. The owner is the sole authority that may define, deprecate or alias keys in its namespace. No key may be created outside its owning namespace.

| Namespace | Owning module / service | Canonical capability |
|---|---|---|
| `auth.*` | `Diten.AuthService` — RBAC primitives (Users, Roles, Permissions) | MOD-0018 (RBAC) |
| `mdm.*` | `Diten.MdmService` (e.g. `mdm.legal-entities.*`) | MDM (MOD-0220 family) |
| `platform.*` | `Diten.Platform` — platform-admin surfaces (administrators, audit, tenants, subscription plans/features, notifications, interface-registry, lookups, org master data, module catalog) | Platform / MOD-0018 enforcement |
| `strategy.*` | `Diten.EnterpriseStrategyService` | Enterprise Strategy |
| *(future business domains)* `crm.*`, `hr.*`, `ppm.*`, … | the respective business-domain module | Blueprint-selected (per AG-STEP-022) |

**Legacy prefixes pending remap (AG-STEP-004B):**

- The PascalCase `Platform.*` keys map to canonical `platform.*`.
- The PascalCase `Modules.*` keys do **not** keep a literal `modules` namespace; each maps to the **owning module's** namespace. For example `Modules.LegalEntity.*` → `mdm.legal-entities.*` (owned by MDM); `Modules.OrganizationUnit.*` / `Modules.Position.*` / `Modules.PositionAssignment.*` / `Modules.ModuleCatalog.*` → their owning Platform namespace. The exact target namespace per key is fixed by the AG-STEP-004B compatibility map, not by this document.

---

## 5. Action-suffix dictionary (locked closed set)

The **last segment** of every key MUST be an action from the sets below. The dictionary is **closed**: adding a new Tier-1/Tier-2 verb is a change to this document; Tier-3 domain actions are registered in the owning module's pack.

### Tier 1 — Core CRUD (canonical; every resource uses these names)

| Canonical action | Meaning | Legacy spellings to migrate (alias → canonical) |
|---|---|---|
| `read` | view / list / detail | `view`, `list`, `get` → `read` |
| `create` | create a new record | `add`, `new` → `create` |
| `update` | modify an existing record | `edit`, `modify` → `update` |
| `delete` | remove a record | `remove` → `delete` |

### Tier 2 — Standard extended actions (approved closed set)

`bulk-delete`, `bulk-update`, `bulk-create`, `export`, `import`, `approve`, `reject`, `archive`, `restore`, `activate`, `deactivate`, `assign`.

### Tier 3 — Domain-specific actions (module-owned)

Compound, lowercase-hyphenated verbs that a module needs and registers in its module pack. They MUST still satisfy the segment grammar (§1) and MUST be documented by the owning module. Examples already in the codebase:

`assign-role`, `assign-permission`, `lookup-validation`, `read-manager-chain`, `manage`, `configure`, `review`, `deprecate`, `manage-mappings`, `redact-actor`, `suspend`, `reactivate`, `cancel`, `expire`, `renew`, `validate`, `link`, `unlink`, `sync`, `publish`, `retire`, `instantiate`.

> PascalCase legacy actions (`Read`, `Create`, `BulkDelete`, `AssignRoles`, `ReadManagerChain`, `RedactActor`, `ManageMappings`, …) are reconciled to their lowercase-hyphenated canonical form (`read`, `create`, `bulk-delete`, `assign-roles`, `read-manager-chain`, `redact-actor`, `manage-mappings`, …) under AG-STEP-004B.

---

## 6. Migration principles (locked)

1. **No blind mass-rename.** Keys are migrated through the compatibility map (§3), surface by surface, with verification — never a global find/replace.
2. **Alias-first, dual-read.** The canonical key is introduced alongside an alias for the legacy key; both satisfy enforcement until the alias is retired.
3. **Every surface accounted for.** A migration must enumerate and address **all** of: seeded catalog, role-permission rows, JWT issuance/claims, `[HasPermission]` attributes (incl. the case-sensitivity gap, see §8), frontend references, tests, and audit references.
4. **Validator widening is part of migration.** Relaxing the ModulePage validator from "exactly 3" to "≥ 3" segments is an AG-STEP-004B change (with its tests), not assumed here.
5. **Standards reconciliation is part of migration.** Editing `erp-architecture.md`, `module-pack-standard.md`, `security-jwt.md`, `response-envelope.md` and the affected agent docs to the canonical format is AG-STEP-004B (§7).
6. **Runtime migration is AG-STEP-004B.** This document changes no runtime; it is the convention + baseline that 004B consumes.

---

## 7. Standards reconciliation register (for AG-STEP-004B)

Documents that currently assert the rejected PascalCase format and must be reconciled to PKS-001 under AG-STEP-004B (this file already supersedes them on format):

| Document | Current assertion | Action under 004B |
|---|---|---|
| `.antigravity/rules/erp-architecture.md` §"RBAC Permission Key Formatı" (≈ L112–127) | `Platform.{Resource}.{Action}` / `Modules.{Module}.{Action}`, Pascal actions | rewrite to PKS-001 |
| `.antigravity/rules/module-pack-standard.md` (Authorization Convention; ≈ L246, L300–305, L524) | `{Prefix}.{Resource}.{Action}` Pascal, `Platform.* vs Modules.*` | rewrite to PKS-001 |
| `.antigravity/rules/security-jwt.md` (SEC-001, L26) | example `Modules.SampleModule.Delete` | update example to lowercase-dotted |
| `.antigravity/rules/response-envelope.md` (L152) | `[HasPermission(ProductPermissions.Products.Create)]` Pascal constants | update example + constant naming |
| `.antigravity/agents/{security-agent,backend-architect,module-pack-author,product-manager}.md` | Pascal `[HasPermission(...)]` examples | update examples |

> `.antigravity/ARCHITECTURE.md` (L211) already uses `[HasPermission("module.resource.action")]` — it is **already consistent** with PKS-001.

---

## 8. Existing-key catalog baseline (audit @ branch `feature/governance/access-governance-execution`, HEAD `70fbe18`)

### 8.1 Surfaces where RBAC keys live

| Surface | Evidence | Volume | Style today |
|---|---|---|---|
| `[HasPermission]` attributes | 17 Platform ctrls, 3 AuthService ctrls, 1 MDM ctrl | **174 uses / 78 distinct keys** | mixed (see 8.2) |
| Seeded permission catalog | `Diten.AuthService.Persistence/Seed/DataSeeder.cs` → Mongo `permissions` | **17 keys** | **all lowercase-dotted** |
| Permission key composition | `Diten.AuthService.Domain/Entities/Permission.cs:12` | `Key = "{module}.{resource}.{action}".ToLowerInvariant()` | **forces lowercase-dotted** |
| JWT claims | `Diten.AuthService.Infrastructure/Services/TokenService.cs:46` `Claim("permission", key)` | inherits seeded keys | **lowercase-dotted** |
| Enforcement match | per-service `PermissionAuthorizationHandler` (exact, case-sensitive `Contains`); `actor_type=platform_admin` bypass | — | exact-match |
| Strategy permission constants | `EnterpriseStrategy.Application/.../EnterpriseStrategyPlatform.cs` (`EnterpriseStrategyPermissions`) | **34 constants** (not yet `[HasPermission]`-wired) | lowercase-dotted (4 use underscore) |
| Sibling space — module-page action keys | `ModulePageDescriptorNormalizer` + `IsCanonicalPermission` (+ tests `ppm.*`, `crm.*`, `mdm.legal-entities.view`) | validator-enforced | lowercase-dotted, exactly-3-segment |
| Frontend | MVC views/JS | **zero** permission gating (strings are descriptor/config samples only) | n/a |

### 8.2 `[HasPermission]` keys by style (78 distinct)

| Style | Count | Share | Example |
|---|---|---|---|
| PascalCase `Platform.*` | 32 | 41% | `Platform.Administrators.Create` |
| PascalCase `Modules.*` | 24 | 31% | `Modules.OrganizationUnit.Read` |
| lowercase-dotted | 22 | 28% | `auth.users.create`, `platform.tenants.quotas.view` |
| **PascalCase subtotal** | **56** | **72%** | — |

The roadmap's OD-C note framed (a) as "the majority." That holds for the **persisted catalog / JWT claims / runtime enforcement** (100% lowercase) but **not** for `[HasPermission]` attributes (Pascal is the 72% majority there). OD-C (a) was chosen because the **runtime enforcement** — not the attribute text — is the binding reality, and it already requires lowercase.

### 8.3 Full distinct-key inventory

**PascalCase `Platform.*` (32) — migrate → `platform.*`:**
`Platform.Administrators.AssignRoles · Platform.Administrators.Create · Platform.Administrators.Read · Platform.Administrators.Suspend · Platform.Administrators.Update · Platform.Audit.Export · Platform.Audit.Read · Platform.Audit.RedactActor · Platform.Audit.Retention.Update · Platform.InterfaceRegistry.Deprecate · Platform.InterfaceRegistry.Import · Platform.InterfaceRegistry.Read · Platform.InterfaceRegistry.Review · Platform.Lookups.Read · Platform.Notifications.Configure · Platform.Notifications.Dispatches.Queue · Platform.Notifications.Dispatches.Read · Platform.Notifications.Read · Platform.Notifications.Templates.Archive · Platform.Notifications.Templates.Create · Platform.Notifications.Templates.Read · Platform.Notifications.Templates.Update · Platform.SubscriptionFeatures.Archive · Platform.SubscriptionFeatures.Create · Platform.SubscriptionFeatures.ManageMappings · Platform.SubscriptionFeatures.Read · Platform.SubscriptionFeatures.Update · Platform.SubscriptionPlans.Activate · Platform.SubscriptionPlans.Create · Platform.SubscriptionPlans.Deactivate · Platform.SubscriptionPlans.Read · Platform.SubscriptionPlans.Update`

**PascalCase `Modules.*` (24) — migrate → owning module namespace:**
`Modules.LegalEntity.Create · Modules.LegalEntity.Delete · Modules.LegalEntity.Read · Modules.LegalEntity.Update · Modules.ModuleCatalog.BulkDelete · Modules.ModuleCatalog.Create · Modules.ModuleCatalog.Delete · Modules.ModuleCatalog.Read · Modules.ModuleCatalog.Update · Modules.Organization.ReadManagerChain · Modules.OrganizationUnit.Archive · Modules.OrganizationUnit.Create · Modules.OrganizationUnit.Delete · Modules.OrganizationUnit.Read · Modules.OrganizationUnit.Update · Modules.Position.Archive · Modules.Position.Create · Modules.Position.Delete · Modules.Position.Read · Modules.Position.Update · Modules.PositionAssignment.Create · Modules.PositionAssignment.Delete · Modules.PositionAssignment.Read · Modules.PositionAssignment.Update`

**lowercase-dotted in attributes (22) — already canonical-shaped:**
`auth.roles.assign-permission · auth.roles.create · auth.roles.delete · auth.roles.read · auth.roles.update · auth.users.assign-role · auth.users.create · auth.users.delete · auth.users.lookup-validation · auth.users.read · auth.users.update · platform.tenants.commercial.subscription.activate · platform.tenants.commercial.subscription.assign · platform.tenants.commercial.subscription.cancel · platform.tenants.commercial.subscription.expire · platform.tenants.commercial.subscription.history.view · platform.tenants.commercial.subscription.reactivate · platform.tenants.commercial.subscription.renew · platform.tenants.commercial.subscription.suspend · platform.tenants.commercial.subscription.view · platform.tenants.quotas.manage · platform.tenants.quotas.view`

**Seeded catalog (17) — lowercase-dotted:**
`auth.users.{create,read,update,delete,assign-role,lookup-validation} · auth.roles.{create,read,update,delete,assign-permission} · mdm.legal-entities.{create,read,update,delete,bulk-delete,export}`

**EnterpriseStrategy constants (34) — lowercase-dotted; underscore deviations to migrate → hyphen:**
`strategy.planning_cycle.create → strategy.planning-cycle.create · strategy.planning_cycle.view → …-cycle.view · strategy.strategy_period.create → strategy.strategy-period.create · strategy.strategy_period.view → …-period.view` (remaining 30 already grammar-clean; verbs `view/edit` normalize to `read/update`).

### 8.4 Deviation register (inputs for AG-STEP-004B)

| # | Deviation | Where | Canonical target |
|---|---|---|---|
| D-1 | PascalCase keys | 56 `[HasPermission]` attrs (Platform + MDM) | lowercase-dotted |
| D-2 | `Modules.*` cross-cutting prefix | 24 attrs | owning-module namespace |
| D-3 | Underscore-in-segment | 4 strategy keys (`planning_cycle`, `strategy_period`) | hyphen-in-segment |
| D-4 | Legacy verbs (`view`/`edit`) | strategy + some Platform | `read`/`update` (alias) |
| D-5 | **Seed ↔ attribute mismatch** | MDM seeds `mdm.legal-entities.read` but controller enforces `Modules.LegalEntity.Read` | unify on `mdm.legal-entities.read`; with case-sensitive exact-match these **never match today** → effective non-enforcement except via `platform_admin` bypass |
| D-6 | Validator stricter than convention | `IsCanonicalPermission` requires exactly 3 segments; 18 live keys have > 3 | widen validator to ≥ 3 |

> **D-5 is a latent enforcement gap, not fixed here.** Cataloged for AG-STEP-004B. The convention and the alias/compat-map are the mechanism to close it; the runtime change is out of scope for AG-STEP-004.

---

## 9. Authoring rule for new keys (effective now, for new code)

New business-domain and platform code SHOULD author keys in the canonical PKS-001 format from the first commit (this aligns with AG-STEP-006A "every privileged endpoint `[HasPermission]`-guarded"). Existing keys are migrated under AG-STEP-004B and MUST NOT be renamed ad-hoc.

---

*PKS-001 — produced by AG-STEP-004 (governance: decision + catalog). MOD-0018 scope; no new MOD identifier; no runtime change. Migration = AG-STEP-004B.*
