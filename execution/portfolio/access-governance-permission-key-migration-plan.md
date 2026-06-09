---
id: AG-STEP-004B-MIGRATION-PLAN
title: Permission-Key Migration Plan & Compatibility Map
status: draft
owner: enterprise-architect / platform-team
authority: ".antigravity/rules/permission-key-standard.md (PKS-001)"
produced_by: AG-STEP-004B (design)
branch: feature/governance/access-governance-execution
inventory_head: cd22d64
---

# Permission-Key Migration Plan & Compatibility Map (AG-STEP-004B)

> **Artifact type:** governance **design document** for the AG-STEP-004B permission-key migration. It defines the
> exact `legacy → canonical` compatibility map, the alias-first dual-read strategy, and the controlled
> implementation slices. **It changes no runtime, seed, migration, controller, validator, or test.** PKS-001
> (`.antigravity/rules/permission-key-standard.md`) is the format authority and is **not** modified by this plan;
> this plan **consumes** PKS-001 §3–§8 and turns the §8.4 deviation register (D-1…D-6) into an executable sequence.
>
> **No new `MOD-xxxx`.** This is MOD-0018 (RBAC) scope only.

## 0. Inventory (read-only, confirmed @ HEAD `cd22d64`)

PKS-001 §8 cataloged the baseline at HEAD `70fbe18`; re-confirmed live on this branch:

| Surface | Live finding @ `cd22d64` | File(s) |
|---|---|---|
| 1. Seeded permission catalog | **17 keys, all lowercase-dotted** (`auth.users.*`, `auth.roles.*`, `mdm.legal-entities.*`) | `Diten.AuthService.Persistence/Seed/DataSeeder.cs` |
| 2. Role-permission persistence | Mongo `permissions` + role-permission rows reference **seeded (lowercase) keys**; `Permission.Key` is forced `…ToLowerInvariant()` | `Diten.AuthService.Domain/Entities/Permission.cs:12` |
| 3. JWT issuance / permission claims | `Claim("permission", key)` emits the **seeded lowercase** keys | `Diten.AuthService.Infrastructure/Services/TokenService.cs:46` |
| 4. `[HasPermission]` attributes | **164 live uses / 78 distinct keys** — **56 PascalCase** (32 `Platform.*` + 24 `Modules.*`) + 22 lowercase-dotted | 17 Platform ctrls, 3 AuthService ctrls, 1 MDM ctrl |
| 5. Frontend nav / permission refs | **zero** permission gating (strings are descriptor/config samples only) | MVC views / JS |
| 6. Test references | per-service authorization tests assert today's keys; ModulePage validator tests assert exactly-3 | `services/**/tests/**` |
| 7. Audit references | audit sink records the resolved entitlement; no separate PascalCase key store | `Diten.Platform.*/Authorization/*Audit*` |
| 8. ModulePage validator | `IsCanonicalPermission` requires **exactly 3 segments** (`parts.Length == 3`) | `Diten.Platform.Application/Features/ModulePages/Validators/ModulePageActionDescriptorRequestValidator.cs:55–59` |
| 9. Enforcement match | policy-based (`Policy = "Permission:{key}"`) → per-service `PermissionAuthorizationHandler`; DevEnablement uses **`OrdinalIgnoreCase`**; `actor_type=platform_admin` bypass | `services/**/Authorization/PermissionAuthorizationHandler.cs`, `**/Security/HasPermissionAttribute.cs` |
| — Standards docs conflicting with PKS-001 | 5 docs + agent docs assert PascalCase (PKS-001 §7 register) | see §8 below |

**Distinct-key set is unchanged from PKS-001 §8.3** (78 distinct; 56 PascalCase). The only drift is the raw
attribute *use* count (174 → 164), which does not affect the compatibility map.

### Deviation register (PKS-001 §8.4 — the migration backlog)

| # | Deviation | Surface | Canonical target |
|---|---|---|---|
| **D-1** | PascalCase keys | 56 `[HasPermission]` attrs | lowercase-dotted |
| **D-2** | `Modules.*` cross-cutting prefix | 24 attrs | owning-module namespace |
| **D-3** | underscore-in-segment | 4 strategy keys | hyphen-in-segment |
| **D-4** | legacy verbs (`view`/`edit`) | strategy + 3 `platform.tenants.*.view` | `read`/`update` |
| **D-5** | **seed ↔ attribute mismatch** (fail-closed break) | MDM `LegalEntitiesController` enforces `Modules.LegalEntity.*`; only `mdm.legal-entities.*` is seeded/granted/claimed | unify on `mdm.legal-entities.*` |
| **D-6** | validator stricter than convention | `IsCanonicalPermission` exactly-3 | widen to ≥ 3 |

> **D-5 confirmed live:** `LegalEntitiesController.cs` carries `[HasPermission("Modules.LegalEntity.{Read,Create,Update,Delete}")]`,
> but **no `Modules.LegalEntity.*` key is seeded or granted anywhere** (grep: zero). The JWT claim carries
> `mdm.legal-entities.*`. Because the attribute key and the granted key differ in **namespace and resource words**
> (not merely casing), they never match under any case rule → **non-`platform_admin` actors are denied every Legal
> Entity endpoint today** (effective fail-closed break; only the `platform_admin` bypass reaches it).

---

## 1. Compatibility map — exact `legacy → canonical`

Resource segments are **plural** kebab-case (matching PKS-001 examples `legal-entities`, `users`, `roles`,
`tenants`, `administrators`); actions follow the §5 dictionary. Every row is an **alias** (legacy) → **canonical**.

### 1.1 `Modules.*` (24) → owning-module namespace

| Legacy (alias) | Canonical | Owner | Segments |
|---|---|---|---|
| `Modules.LegalEntity.Read` | `mdm.legal-entities.read` | MDM | 3 |
| `Modules.LegalEntity.Create` | `mdm.legal-entities.create` | MDM | 3 |
| `Modules.LegalEntity.Update` | `mdm.legal-entities.update` | MDM | 3 |
| `Modules.LegalEntity.Delete` | `mdm.legal-entities.delete` | MDM | 3 |
| `Modules.ModuleCatalog.Read` | `platform.module-catalog.read` | Platform | 3 |
| `Modules.ModuleCatalog.Create` | `platform.module-catalog.create` | Platform | 3 |
| `Modules.ModuleCatalog.Update` | `platform.module-catalog.update` | Platform | 3 |
| `Modules.ModuleCatalog.Delete` | `platform.module-catalog.delete` | Platform | 3 |
| `Modules.ModuleCatalog.BulkDelete` | `platform.module-catalog.bulk-delete` | Platform | 3 |
| `Modules.OrganizationUnit.Read` | `platform.organization-units.read` | Platform | 3 |
| `Modules.OrganizationUnit.Create` | `platform.organization-units.create` | Platform | 3 |
| `Modules.OrganizationUnit.Update` | `platform.organization-units.update` | Platform | 3 |
| `Modules.OrganizationUnit.Delete` | `platform.organization-units.delete` | Platform | 3 |
| `Modules.OrganizationUnit.Archive` | `platform.organization-units.archive` | Platform | 3 |
| `Modules.Position.Read` | `platform.positions.read` | Platform | 3 |
| `Modules.Position.Create` | `platform.positions.create` | Platform | 3 |
| `Modules.Position.Update` | `platform.positions.update` | Platform | 3 |
| `Modules.Position.Delete` | `platform.positions.delete` | Platform | 3 |
| `Modules.Position.Archive` | `platform.positions.archive` | Platform | 3 |
| `Modules.PositionAssignment.Read` | `platform.position-assignments.read` | Platform | 3 |
| `Modules.PositionAssignment.Create` | `platform.position-assignments.create` | Platform | 3 |
| `Modules.PositionAssignment.Update` | `platform.position-assignments.update` | Platform | 3 |
| `Modules.PositionAssignment.Delete` | `platform.position-assignments.delete` | Platform | 3 |
| `Modules.Organization.ReadManagerChain` | `platform.organization.read-manager-chain` | Platform | 3 |

### 1.2 `Platform.*` (32) → `platform.*`

| Legacy (alias) | Canonical | Segments |
|---|---|---|
| `Platform.Administrators.Read` | `platform.administrators.read` | 3 |
| `Platform.Administrators.Create` | `platform.administrators.create` | 3 |
| `Platform.Administrators.Update` | `platform.administrators.update` | 3 |
| `Platform.Administrators.Suspend` | `platform.administrators.suspend` | 3 |
| `Platform.Administrators.AssignRoles` | `platform.administrators.assign-roles` | 3 |
| `Platform.Audit.Read` | `platform.audit.read` | 3 |
| `Platform.Audit.Export` | `platform.audit.export` | 3 |
| `Platform.Audit.RedactActor` | `platform.audit.redact-actor` | 3 |
| `Platform.Audit.Retention.Update` | `platform.audit.retention.update` | **4** (needs D-6) |
| `Platform.InterfaceRegistry.Read` | `platform.interface-registry.read` | 3 |
| `Platform.InterfaceRegistry.Import` | `platform.interface-registry.import` | 3 |
| `Platform.InterfaceRegistry.Review` | `platform.interface-registry.review` | 3 |
| `Platform.InterfaceRegistry.Deprecate` | `platform.interface-registry.deprecate` | 3 |
| `Platform.Lookups.Read` | `platform.lookups.read` | 3 |
| `Platform.Notifications.Read` | `platform.notifications.read` | 3 |
| `Platform.Notifications.Configure` | `platform.notifications.configure` | 3 |
| `Platform.Notifications.Dispatches.Read` | `platform.notifications.dispatches.read` | **4** (needs D-6) |
| `Platform.Notifications.Dispatches.Queue` | `platform.notifications.dispatches.queue` | **4** (needs D-6) |
| `Platform.Notifications.Templates.Read` | `platform.notifications.templates.read` | **4** (needs D-6) |
| `Platform.Notifications.Templates.Create` | `platform.notifications.templates.create` | **4** (needs D-6) |
| `Platform.Notifications.Templates.Update` | `platform.notifications.templates.update` | **4** (needs D-6) |
| `Platform.Notifications.Templates.Archive` | `platform.notifications.templates.archive` | **4** (needs D-6) |
| `Platform.SubscriptionFeatures.Read` | `platform.subscription-features.read` | 3 |
| `Platform.SubscriptionFeatures.Create` | `platform.subscription-features.create` | 3 |
| `Platform.SubscriptionFeatures.Update` | `platform.subscription-features.update` | 3 |
| `Platform.SubscriptionFeatures.Archive` | `platform.subscription-features.archive` | 3 |
| `Platform.SubscriptionFeatures.ManageMappings` | `platform.subscription-features.manage-mappings` | 3 |
| `Platform.SubscriptionPlans.Read` | `platform.subscription-plans.read` | 3 |
| `Platform.SubscriptionPlans.Create` | `platform.subscription-plans.create` | 3 |
| `Platform.SubscriptionPlans.Update` | `platform.subscription-plans.update` | 3 |
| `Platform.SubscriptionPlans.Activate` | `platform.subscription-plans.activate` | 3 |
| `Platform.SubscriptionPlans.Deactivate` | `platform.subscription-plans.deactivate` | 3 |

### 1.3 Already-canonical lowercase (22) — no rename, except 3 D-4 verb aliases

The 22 lowercase `[HasPermission]` keys (`auth.*`, `platform.tenants.commercial.subscription.*`,
`platform.tenants.quotas.*`) are grammar-canonical and **not renamed**. Three carry the legacy verb `view`
(D-4) and alias to `read`:

| Legacy (alias) | Canonical |
|---|---|
| `platform.tenants.quotas.view` | `platform.tenants.quotas.read` |
| `platform.tenants.commercial.subscription.view` | `platform.tenants.commercial.subscription.read` |
| `platform.tenants.commercial.subscription.history.view` | `platform.tenants.commercial.subscription.history.read` |

### 1.4 EnterpriseStrategy constants (D-3 underscore + D-4 verbs)

| Legacy (alias) | Canonical |
|---|---|
| `strategy.planning_cycle.create` | `strategy.planning-cycle.create` |
| `strategy.planning_cycle.view` | `strategy.planning-cycle.read` |
| `strategy.strategy_period.create` | `strategy.strategy-period.create` |
| `strategy.strategy_period.view` | `strategy.strategy-period.read` |

(The other 30 strategy constants are grammar-clean; any `view`/`edit` verbs normalize `→ read`/`update` by the same
rule. These constants are not yet `[HasPermission]`-wired, so their migration is doc-and-constant only until wired.)

> The compatibility map is **directional and non-transitive** (PKS-001 §3): each alias points to exactly one
> canonical key; canonical keys are never aliased to other canonical keys.

---

## 2. Alias-first, dual-read strategy (PKS-001 §3)

1. **Introduce canonical alongside alias.** For each migrated key the canonical key is seeded; the legacy key is
   retained as a **deprecated alias** that resolves to the canonical (`alias → canonical`).
2. **Dual-read enforcement.** During migration, a grant of **either** the alias **or** the canonical key satisfies
   the `[HasPermission]` check (the resolver expands the requirement to `{canonical} ∪ {aliases-of-canonical}`).
   No tenant loses access mid-migration.
3. **Single source of truth.** §1 of this document is the compatibility map; the runtime alias table is generated
   from it (one slice introduces that table). It is never hand-edited in two places.
4. **Forward-only writes.** New seeds, new JWT claims, and new `[HasPermission]` attributes emit the **canonical**
   key only; aliases are read-side compatibility, never newly written.

---

## 3. D-5 — priority fix (FIRST controlled slice)

**Goal:** close the Legal Entity fail-closed enforcement break before anything else.

- Switch `Diten.MdmService.../LegalEntitiesController.cs` from `Modules.LegalEntity.{Read,Create,Update,Delete}`
  to the **canonical, already-seeded** `mdm.legal-entities.{read,create,update,delete}`.
- Register the alias rows `Modules.LegalEntity.* → mdm.legal-entities.*` so any pre-existing external grant of the
  legacy key keeps working (dual-read).
- **Effect:** a non-`platform_admin` actor granted `mdm.legal-entities.read` can finally reach the endpoints
  (today: 403 for everyone except `platform_admin`).
- This is a 3-segment canonical key → **does not depend on D-6**, so it can ship first.

---

## 4. D-6 — validator widening (second slice)

- Widen `IsCanonicalPermission` from `parts.Length == 3` to `parts.Length >= 3` (keep all other grammar checks:
  lowercase, segment regex, no underscore, action-suffix).
- Required **before** introducing the 8 four-segment canonical keys (`platform.audit.retention.update`,
  `platform.notifications.dispatches.*`, `platform.notifications.templates.*`).
- Tests updated: accept 3- and 4-segment canonical keys; still reject < 3 segments and grammar violations.

---

## 5. D-1 / D-2 / D-3 / D-4 — migration surfaces

| Deviation | Surfaces touched | Mechanism |
|---|---|---|
| **D-1** PascalCase | 56 `[HasPermission]` attrs; seeded catalog (add canonical); role-permission alias rows | alias-first dual-read; attrs flipped to canonical per slice |
| **D-2** `Modules.*` prefix | the 24 `Modules.*` attrs → owning namespace (`mdm.*` / `platform.*`) | same map (§1.1); no literal `modules` namespace survives |
| **D-3** underscore | 4 strategy constants | rename constant value `_ → -`; alias old value |
| **D-4** legacy verbs | 3 `platform.tenants.*.view` + strategy `view`/`edit` | alias `view→read`, `edit→update` |

---

## 6. No blind mass-rename (PKS-001 §6.1)

Migration is **surface-by-surface through the compatibility map**, never a global find/replace. Each slice
enumerates and verifies its surfaces; a key is flipped to canonical only after its canonical seed + alias row exist
and its dual-read test passes. A repo-wide `sed` of `Modules.`/`Platform.` is **forbidden**.

---

## 7. Controlled implementation slices

Each slice: **scope · surfaces · test gate · rollback boundary · commit boundary.** D-5 is Slice 1.

| # | Slice | Scope | Surfaces changed | Test gate | Rollback boundary | Commit boundary |
|---|---|---|---|---|---|---|
| **1** | **D-5 LegalEntity break** | Close fail-closed break | MDM `LegalEntitiesController` attrs → `mdm.legal-entities.*`; compat-map/alias rows; MDM authz tests | granted user reaches endpoints; legacy-alias grant passes; `platform_admin` still bypasses | revert MDM ctrl + alias rows (1 service) | MDM ctrl + alias table + MDM tests only |
| **2** | **D-6 validator** | exactly-3 → ≥3 | `ModulePageActionDescriptorRequestValidator` + its tests | accepts 3 & 4-seg canonical; rejects <3 & bad grammar | revert validator + tests | validator + tests only |
| **3** | **Alias/dual-read infra** | runtime alias table generated from §1 + dual-read resolver | seeder (canonical keys), enforcement resolver, alias table, resolver tests | dual-read: alias-grant ∨ canonical-grant both pass; forward-writes emit canonical only | revert resolver + table | AuthService seed/resolver + tests |
| **4** | **D-1/D-2 Platform.* attrs** | 32 `Platform.*` → `platform.*` | Platform controllers (per resource group: Administrators, Audit, InterfaceRegistry, Lookups, Notifications, SubscriptionFeatures, SubscriptionPlans), seed, Platform authz tests | each canonical enforces; legacy-alias grant passes | per resource-group revert | one commit per resource group |
| **5** | **D-2 Modules.* org attrs** | 20 remaining `Modules.*` → `platform.*` | Platform org controllers (OrganizationUnit, Position, PositionAssignment, Organization, ModuleCatalog), seed, tests | as Slice 4 | per resource revert | one commit per resource |
| **6** | **D-3/D-4 strategy + verbs** | underscores → hyphens; `view/edit` → `read/update` | EnterpriseStrategy constants (+ wiring if any), 3 `platform.tenants.*.view`, seed, tests | canonical enforces; verb-alias passes | revert per surface | strategy + tenants-view commit |
| **7** | **Standards reconciliation** | docs only (§8) | the 5 standards docs + agent docs | grep: no PascalCase mandate remains; no runtime diff | revert docs | docs-only commit |
| **8** | **Alias retirement** | remove aliases | alias table (remove rows); confirm zero legacy refs | full suite green; grep: zero legacy keys anywhere | re-add alias rows | retirement commit per namespace |

> Slices 1–2 are independent and high-value; Slice 3 (dual-read infra) gates Slices 4–6; Slice 7 is docs-only and
> can run in parallel; Slice 8 runs last, after every surface is canonical.

---

## 8. Standards-document reconciliation list (PKS-001 §7)

Rewrite to PKS-001 lowercase-dotted format (Slice 7; PKS-001 already supersedes them on format):

- `.antigravity/rules/erp-architecture.md` — §"RBAC Permission Key Formatı" (`Platform.*`/`Modules.*` Pascal) → PKS-001.
- `.antigravity/rules/module-pack-standard.md` — Authorization Convention (`{Prefix}.{Resource}.{Action}` Pascal) → PKS-001.
- `.antigravity/rules/security-jwt.md` — SEC-001 example `Modules.SampleModule.Delete` → lowercase-dotted.
- `.antigravity/rules/response-envelope.md` — `[HasPermission(ProductPermissions.Products.Create)]` Pascal constant → PKS-001.
- `.antigravity/agents/{security-agent,backend-architect,module-pack-author,product-manager}.md` — Pascal `[HasPermission(...)]` examples → PKS-001.
- *(Already consistent — do not touch:* `.antigravity/ARCHITECTURE.md`, `.antigravity/rules/permission-key-standard.md` (PKS-001), `.antigravity/rules/business-module-enforcement-standard.md` (BME-001).*)*

---

## 9. Alias retirement criteria (PKS-001 §3)

An alias is removed (Slice 8) **only when all** of the following hold for its canonical key:

1. Seeded catalog contains the canonical key; the alias is not newly seeded.
2. All role-permission rows reference the canonical key (alias rows are read-compat only).
3. JWT issuance emits the canonical key only.
4. Every `[HasPermission]` attribute uses the canonical key (grep: zero legacy occurrences).
5. Frontend / tests / audit references use the canonical key (frontend has none today).
6. A deprecation window has elapsed and the dual-read tests have been green across it.
7. `verify` of the slice shows the alias has zero remaining consumers.

Retirement is per-namespace, never a single bulk deletion.

---

## 10. Pre-push / PR / merge verification checklist (every slice)

- [ ] Affected services build clean (`dotnet build`).
- [ ] Affected test suites green (`dotnet test`), incl. the slice's new dual-read / fail-closed regression tests.
- [ ] **D-5 regression:** a user granted `mdm.legal-entities.read` reaches the Legal Entity endpoints; pre-fix this returned 403.
- [ ] Dual-read proven: a grant of the **alias** and a grant of the **canonical** both satisfy enforcement.
- [ ] Validator (Slice 2+): accepts ≥3-segment canonical, rejects <3 and grammar violations.
- [ ] `grep` shows no **new** PascalCase `[HasPermission]`; legacy occurrences only where this slice has not yet migrated.
- [ ] Diff is scoped to the slice's declared surfaces (no out-of-slice file).
- [ ] **No change** to PKS-001, the module-id registry, DCP-002, the roadmap, or master-plan — unless the slice is the standards-reconciliation slice (which touches only the §8 docs).
- [ ] Compatibility map (§1) and the runtime alias table agree (single source of truth).
- [ ] Merge-freeze respected: push to the integration branch; PR/merge only by explicit user/EA decision.

---

*AG-STEP-004B migration plan — design only. Consumes PKS-001 (format authority); changes no runtime/seed/test/standard. MOD-0018 scope; no new MOD identifier.*
