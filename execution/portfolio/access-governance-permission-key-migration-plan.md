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

> **Dual-read requires the Platform-only alias-resolution seam (§7 Slice 1B).** Today the enforcement points do a
> **single-value exact match** with no alias map — there is **no reusable alias seam anywhere in the repo**. The
> `{canonical} ∪ {aliases-of-canonical}` expansion is therefore a **prerequisite** that must land (Slice 1B)
> **before** any rename slice that relies on dual-read. **Scope (locked by read-only audit @ `bf0f82c`):** only
> `Diten.Platform` enforces legacy aliasable keys — **125 `[HasPermission]` uses of `Platform.*`/`Modules.*`**.
> AuthService (`auth.*`, already canonical), DevEnablement (zero `[HasPermission]`), and MDM (canonical post-Slice-1A)
> enforce **no** legacy key, so the seam lives **in Platform only** and the other three services are untouched. Faking
> dual-read in a single controller or handler is **forbidden** (it is the one-off bypass PKS-001 §6.1 prohibits).
> **Slice 1A (D-5) is the one slice that needs no dual-read** — no legacy grant of `Modules.LegalEntity.*` exists, so
> nothing must be kept alive.

---

## 3. D-5 — priority fix: **Slice 1A, attribute-switch-only hotfix (no alias seam)**

**Goal:** close the Legal Entity fail-closed enforcement break first, with the smallest safe change.

**Grounding (read-only audit @ `ba57460`):**
- The legacy key `Modules.LegalEntity.*` exists **only** as the controller attribute in
  `Diten.MdmService.../LegalEntitiesController.cs` (6 uses). It is **never seeded, never granted, and appears in no
  role-permission row** (repo grep: zero). **No user or role holds a working grant of the legacy key.**
- The canonical keys are seeded exactly: `DataSeeder.cs` rows `new("mdm","legal-entities","{create,read,update,delete}")`
  ⇒ `Permission.Key = "{module}.{resource}.{action}".ToLowerInvariant()` = `mdm.legal-entities.{create,read,update,delete}`.

**Consequence — the alias seam is NOT required for D-5.** Because there is no legacy grant to preserve, **dual-read
is moot here**. The break is fully closed by switching the controller attributes to the already-seeded canonical
keys; nobody loses access. Any `Modules.LegalEntity.* → mdm.legal-entities.*` alias is deferred to the global alias
seam slice (§7 Slice 1B) and is informational only, since no consumer of the legacy grant exists.

**Slice 1A change:**
- Switch `LegalEntitiesController.cs` `[HasPermission]` keys `Modules.LegalEntity.{Read,Create,Update,Delete}` →
  `mdm.legal-entities.{read,create,update,delete}` (3-segment; **does not depend on D-6**).
- **Effect:** a non-`platform_admin` actor granted `mdm.legal-entities.read` can finally reach the endpoints
  (today: 403 for everyone except `platform_admin`).
- **No one-off bypass.** Slice 1A is a literal attribute correction — it adds **no** alias-resolution logic to the
  controller or handler. If dual-read for any *granted* legacy key is ever needed, it goes through the global alias
  seam (Slice 1B), never a controller/handler hack.

**Slice 1A test gate (locked):**
- canonical grant (`mdm.legal-entities.read`) → **access** (was 403 pre-fix);
- no grant → **deny** (fail-closed preserved);
- `platform_admin` bypass → **unchanged**.
- *(There is no "legacy-grant → alias access" test in 1A: no such grant exists by evidence, so it is N/A — not skipped.)*

---

## 4. D-6 — validator widening (Slice 2 ✅ DONE; prerequisite for the rename slices alongside Slice 1B)

> **Completed (integration branch, commit `20d9306`, audit PASS).** The exactly-3 rule lived in **two**
> `IsCanonicalPermission` validators — `ModulePageActionDescriptorRequestValidator` **and**
> `ModulePageDescriptorRequestValidator` (the page-descriptor validator that `CreateModulePageDescriptorCommandValidator`
> uses). **Both** were relaxed to `parts.Length >= 3`. Uppercase input is lowercased by `NormalizePermission` and
> accepted (PKS-001 "store lowercase"); no strict case-rejection was added.

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

Migration is **surface-by-surface through the compatibility map**, never a global find/replace. Each rename slice
enumerates and verifies its surfaces; a key is flipped to canonical only after its canonical seed + alias entry
exist (via Slice 1B) and its dual-read test passes. **Slice 1A is the single exemption:** it has no legacy grant to
preserve, so it needs no alias entry and no dual-read — but it is still a literal, scoped attribute correction, not
a `sed`. A repo-wide `sed` of `Modules.`/`Platform.` is **forbidden**.

---

## 7. Controlled implementation slices

Each slice: **scope · surfaces · test gate · rollback boundary · commit boundary.** The D-5 hotfix is **Slice 1A**;
the **Platform-only** alias seam is **Slice 1B** and is a **prerequisite for every dual-read rename slice (3–5)**
(those renames are all Platform-enforced).

| # | Slice | Scope | Surfaces changed | Test gate | Rollback boundary | Commit boundary |
|---|---|---|---|---|---|---|
| **1A** ✅ **DONE** | **D-5 LegalEntity hotfix (attribute-switch-only, NO alias seam)** — completed in integration branch, **runtime commit `64417c2`**; security audit **PASS**; tests **25 passed, 0 failed**; **not pushed / no PR / not merged to `main`** | Close fail-closed break with the smallest safe change | MDM `LegalEntitiesController` 6 attrs `Modules.LegalEntity.* → mdm.legal-entities.{read,create,update,delete}` (match seeded keys); MDM authz tests added; **no alias seam — not needed for D-5** | canonical grant → access; no grant → deny; `platform_admin` bypass unchanged; legacy grant → deny *(legacy-grant→alias test N/A — no such grant exists)* | revert MDM ctrl + tests (1 file + tests) | MDM ctrl + MDM authz tests only |
| **1B** ✅ **DONE** | **Platform-only alias-resolution seam (prerequisite for 3–5)** — completed in integration branch, **Commit A `129c62e`** (map + resolver + unit tests) + **Commit B `a800407`** (`HasPermissionAttribute` wiring + dual-read integration tests); security audit **PASS**; tests **554 passed, 0 failed**; **Commit B not pushed / no PR / not merged to `main`** | Built the dual-read seam where the legacy keys live: **55-entry immutable** alias map (from §1, Platform-enforced rows only) + `Expand(canonical) = {canonical} ∪ aliases`, consumed by Platform's `HasPermissionAttribute` non-bypass path | **`Diten.Platform` only.** `PermissionAliasMap.cs` + `PermissionAliasResolver.cs` (static, pure) + `HasPermissionClaim` expand+intersect; resolver unit + dual-read integration tests. **AuthService / DevEnablement / MDM NOT touched** | canonical req satisfied by canonical OR legacy alias claim → allow; unknown/no claim/unauthenticated → deny (fail-closed); legacy requirement **not auto-upgraded**; `platform_admin`/`partner_admin` bypass + admin-lifecycle side-effects **unchanged**; map directional & non-transitive, no duplicate alias | revert Platform map/resolver + attribute edit | **2 commits** (A `129c62e`; B `a800407`) — Platform-only |
| **2** ✅ **DONE** | **D-6 validator** — completed in integration branch, **runtime commit `20d9306`**; audit **PASS**; tests **532 passed, 0 failed**; **not pushed / no PR / not merged to `main`** | exactly-3 → ≥3 | **Both** `IsCanonicalPermission` validators (`ModulePageActionDescriptorRequestValidator` **and** `ModulePageDescriptorRequestValidator`) → `parts.Length >= 3` + their tests. **Note:** the plan named one validator, but two carry the rule; both updated for consistency. Uppercase input is normalized to lowercase by `NormalizePermission` and **accepted** (PKS-001 "store lowercase") — **no strict case-rejection added** (out of D-6 scope) | accepts 3 & 4/5-seg canonical; rejects <3 & bad grammar (underscore / illegal char); uppercase normalized-accepted | revert validators + tests | validators + tests only |
| **3** ✅ **DONE** | **D-1/D-2 Platform.* attrs** — completed in integration branch; audit **PASS**; tests **554 passed, 0 failed**; production-controller legacy `Platform.*` usage **0**; **not pushed / no PR / not merged to `main`** | **80 attribute uses / 32 distinct keys / 8 controllers** `Platform.* → platform.*` (canonical lowercase-dotted); alias-map coverage **100%** | 8 Platform controllers (Administrators, Audit, InterfaceRegistry, Lookups, Notifications, SubscriptionFeatures, FeatureCategories, SubscriptionPlans) + 1 audit surface test; **6 resource-group commits:** `ac12223` administrators · `4e94454` audit · `6f6673a` interface-registry · `8ba4aeb` lookups · `67518de` notifications · `2768b2c` subscriptions. Alias map / `HasPermissionAttribute` wiring **unchanged** | each canonical enforces; legacy-alias grant passes via 1B dual-read; bypass unchanged | per resource-group revert | one commit per resource group (6) |
| **4** ✅ **DONE** | **D-2 Modules.* org attrs** — completed in integration branch; audit **PASS**; tests **554 passed, 0 failed**; Platform production-source legacy `Modules.*` usage **0**; **not pushed / no PR / not merged to `main`** | **45 attribute uses / 20 distinct keys / 6 controllers** `Modules.* → platform.*` (canonical lowercase-dotted); alias-map coverage **100%** | 6 Platform org/module-catalog controllers (ModuleCatalog, ModulePages, ModuleAssignments, OrganizationUnits, Positions, PositionAssignments); **4 resource-group commits:** `749fc3d` module-catalog (3 files) · `d3d3fda` organization-units · `172c837` positions + manager-chain · `4395125` position-assignments. Alias map / `HasPermissionAttribute` wiring **unchanged**; intentional `Modules.OrganizationUnit.Read` dual-read fixture preserved | each canonical enforces; legacy-alias grant passes via 1B dual-read; bypass unchanged | per resource revert | one commit per resource (4) |
| **5A** ✅ **DONE** | **D-4 Platform tenant verb aliases** — completed in integration branch, **runtime commit `cb3fda5`**; tests **554 passed, 0 failed**; **not pushed / no PR / not merged to `main`** | `platform.tenants.quotas.view → …read`; `platform.tenants.commercial.subscription.view → …read`; `platform.tenants.commercial.subscription.history.view → …read` (**3 distinct keys / 10 enforcement points / 3 files**) | `QuotasController.cs` (2 attr), `TenantCommercialSubscriptionsController.cs` (5 attr), `TenantModuleEntitlementsController.cs` (`const ViewPermission` → `.read`, applied ×3). Alias map §1.3 covers all 3 (canonical `.read` ← legacy `.view`) → **dual-read safe via existing Platform seam**; canonical `.read` keys keep supporting legacy `.view` claims. Alias map / `HasPermissionAttribute` wiring **unchanged**; intentional seam fixtures kept; **EnterpriseStrategy untouched** | each canonical `.read` enforces; legacy `.view` grant passes via 1B dual-read; bypass unchanged | revert the 3 files | **single atomic runtime commit** `cb3fda5` |
| **5B** 🛑 **BLOCKED — Option C: external evidence required** *(grant-source audit done; no runtime change)* | **D-3 EnterpriseStrategy `_ → -`** *(out of Platform seam scope)* — split from old Slice 5; grant-source audit completed | `strategy.planning_cycle.{view,create} → strategy.planning-cycle.{view,create}`; `strategy.strategy_period.{view,create} → strategy.strategy-period.{view,create}` (4 const keys) | **`Diten.EnterpriseStrategyService`** — 4 consts in `EnterpriseStrategyPlatform.cs`, 27 applications via **`[EnterpriseStrategyPermission]`** → `DefaultEnterpriseStrategyAuthorizationService.HasPermissionAsync` (only impl). **Two grant paths:** (1) dev-bootstrap (in-repo default) — `KnownPermissions` built **from the consts** → const is single source, rename self-consistent; (2) enforced (`DITEN_ESBP_ENFORCE_PERMISSIONS`) — **external JWT `permission` claim, exact OrdinalIgnoreCase match, no normalization/alias**. **No ES-side alias/dual-read seam; no in-repo issuer/seeder grants these keys; 27 controller refs use the const NAME (0 literal), no test hardcodes the 4 strings.** D-3 only — strategy `.view/.edit` D-4 verb normalization out of scope | **none — runtime rename NOT done; EnterpriseStrategy untouched.** **External fact required:** does the production token issuer / AuthService grant these 4 keys with `_` (legacy) or `-` (canonical), and is ESBP live in enforced mode? **Option A** (1-file/4-line atomic const rename, à la 1A) if no live `_` grant / enforced mode off; **Option B** (ES-side `_↔-` compatibility shim + tests, then rename) if a live `_` grant can exist. **No rename without external evidence.** | Option A: revert 1 file · Option B: revert shim + rename | Option A: 1 atomic commit · Option B: 2 commits (shim+tests, then rename) |
| **6** | **Standards reconciliation** | docs only (§8) | the 5 standards docs + agent docs | grep: no PascalCase mandate remains; no runtime diff | revert docs | docs-only commit |
| **7** | **Alias retirement** | remove aliases | alias map (remove rows); confirm zero legacy refs | full suite green; grep: zero legacy keys anywhere | re-add alias rows | retirement commit per namespace |

> **Ordering (locked):** **Slice 1A** ships first and standalone (no dependency). **Slice 1B** (Platform-only alias
> seam) and **Slice 2** (D-6 validator) are the two prerequisites for the rename slices and may proceed in parallel;
> **Slices 3–5** (the dual-read renames, all Platform-enforced) must not start until **1B** lands. **Slice 6** is
> docs-only and can run any time. **Slice 7** (retirement) runs last, after every surface is canonical. No rename
> slice may fake dual-read without 1B. **No new shared authorization library is created** — the seam is Platform-owned;
> if a second service ever gains legacy keys, a shared home is evaluated separately at that point.
>
> **Slice 5 split (locked, per live-repo audit):** the original Slice 5 covered two structurally different surfaces and
> is split. **5A (Platform tenant verb aliases) is applicable now** — map-covered by §1.3, dual-read-safe through the
> existing Platform seam, one atomic runtime commit. **5B (EnterpriseStrategy `_ → -`) does NOT auto-proceed** — it is a
> different service with a different `[EnterpriseStrategyPermission]` enforcement stack, outside the Platform alias map,
> with an unverified grant source; it is **BLOCKED pending a separate scoping audit** (and, if needed, an
> EnterpriseStrategy-side compatibility shim) before any rename. **Blind / bulk cross-service rename is forbidden.** The
> **alias seam is preserved until Slice 7** (retirement) regardless of 5A/5B progress.

---

## 8. Standards-document reconciliation list (PKS-001 §7)

Rewrite to PKS-001 lowercase-dotted format (Slice 6; PKS-001 already supersedes them on format):

- `.antigravity/rules/erp-architecture.md` — §"RBAC Permission Key Formatı" (`Platform.*`/`Modules.*` Pascal) → PKS-001.
- `.antigravity/rules/module-pack-standard.md` — Authorization Convention (`{Prefix}.{Resource}.{Action}` Pascal) → PKS-001.
- `.antigravity/rules/security-jwt.md` — SEC-001 example `Modules.SampleModule.Delete` → lowercase-dotted.
- `.antigravity/rules/response-envelope.md` — `[HasPermission(ProductPermissions.Products.Create)]` Pascal constant → PKS-001.
- `.antigravity/agents/{security-agent,backend-architect,module-pack-author,product-manager}.md` — Pascal `[HasPermission(...)]` examples → PKS-001.
- *(Already consistent — do not touch:* `.antigravity/ARCHITECTURE.md`, `.antigravity/rules/permission-key-standard.md` (PKS-001), `.antigravity/rules/business-module-enforcement-standard.md` (BME-001).*)*

---

## 9. Alias retirement criteria (PKS-001 §3)

An alias is removed (Slice 7) **only when all** of the following hold for its canonical key:

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
- [ ] **D-5 regression (Slice 1A):** a user granted `mdm.legal-entities.read` reaches the Legal Entity endpoints (pre-fix 403); no grant → deny; `platform_admin` bypass unchanged. *(No legacy-grant→alias assertion — N/A in 1A.)*
- [ ] Dual-read proven **(Slice 1B and rename Slices 3–5; N/A for 1A)**: a grant of the **alias** and a grant of the **canonical** both satisfy enforcement; map stays directional & non-transitive.
- [ ] Validator (Slice 2): accepts ≥3-segment canonical, rejects <3 and grammar violations.
- [ ] **No rename slice (3–5) starts before Slice 1B lands** (no faked dual-read / one-off controller or handler bypass).
- [ ] `grep` shows no **new** PascalCase `[HasPermission]`; legacy occurrences only where this slice has not yet migrated.
- [ ] Diff is scoped to the slice's declared surfaces (no out-of-slice file).
- [ ] **No change** to PKS-001, the module-id registry, DCP-002, the roadmap, or master-plan — unless the slice is the standards-reconciliation slice (which touches only the §8 docs).
- [ ] Compatibility map (§1) and the runtime alias table agree (single source of truth).
- [ ] Merge-freeze respected: push to the integration branch; PR/merge only by explicit user/EA decision.

---

*AG-STEP-004B migration plan — design only. Consumes PKS-001 (format authority); changes no runtime/seed/test/standard. MOD-0018 scope; no new MOD identifier.*
