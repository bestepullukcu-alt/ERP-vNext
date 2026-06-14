# Entitlement → Permission Bridge — design note

> **Scope:** AuthService side of the tenant-entitlement → role-permission bridge.
> **Status:** spec locked in **S2** (AG-INFRA-COMPLETION). Mapping + revoke **semantics** are fixed
> here; the runtime **consumer** that applies them is **S3** (`TenantEntitlement{Added,Enabled,Disabled}V1`).
> Authority order unchanged: Module Pack > Domain Config > AGENTS.md > .antigravity standards.
> Companion runtime artifacts: `Diten.AuthService.Domain/Authorization/ModulePermissionResolver.cs`
> (Part A), `RolePermission.GrantSource` / `SourceModuleCode` (S1, Part 0).

## Part A — Module → permission mapping (LOCKED)

A Platform entitlement carries a `ModuleCode` (e.g. `"MDM"`). AuthService permissions carry a
lowercase `Permission.Module` (e.g. `"mdm"`) and `Key = module.resource.action`. Historically the
relationship was an **implicit convention** — `DataSeeder` string-matched `Permission.Module`. There
was no explicit, testable registry.

**Decision: convention-first + override map.**

1. **Convention.** `normalize(ModuleCode) == Permission.Module`, where
   `normalize = trim + ToLowerInvariant`. Matching is case-insensitive. `"MDM" → "mdm"`.
2. **Override map.** `ModulePermissionResolver.ModuleCodeOverrides` (normalized-code → permission-module)
   takes precedence over the convention, for confirmed deviations only. It ships **empty** — no
   mapping is guessed; EA / the module pack adds entries as deviations are verified.
3. **Platform boundary.** A `ModuleCode` that resolves to the `platform` module returns **no**
   permissions. Platform permissions never enter a tenant via this bridge (consistent with
   `DefaultRolePermissionTemplate.IsPlatform`, the S1 escalation boundary).
4. **Fail-safe.** Unmatched / null / blank / platform-resolving `ModuleCode` → **empty set**, never an
   exception. A module with no catalog permissions is a no-op, not a failure.

The resolver is pure/stateless; the S3 consumer calls
`ModulePermissionResolver.ResolvePermissions(moduleCode, permissionCatalog)`.

## Part B — Revoke semantics (LOCKED; S3 implements)

Grants are distinguished by `RolePermission.GrantSource` (`System` | `Module` | `Manual`) and, for
module grants, `SourceModuleCode` (S1). The consumer enforces exactly these rules:

1. **Entitlement added / enabled →** for each permission of the module (Part A resolver), write a
   grant to the target tenant role(s) with **`GrantSource = Module` and `SourceModuleCode = <code>`**
   (normalized). One row per (role, permission, sourceModuleCode).
2. **Entitlement removed / disabled →** drop **only** grants where
   **`GrantSource == Module` AND `SourceModuleCode == <code>`** (normalized, case-insensitive).
3. **Never touch `System` (baseline) or `Manual` (operator) grants.** They are out of the
   entitlement bridge's authority — disabling a module must never strip a baseline or hand-assigned
   permission, even if the same permission key is also module-sourced.
4. **Shared permissions survive until the last entitlement is gone.** A permission granted by more
   than one module is held by one **distinct `Module` grant row per source module**. Removing module
   X deletes only the `SourceModuleCode == X` row(s); rows from other modules remain, so the
   permission stays effective until its final contributing entitlement is removed.

**Idempotency.** Add is a no-op if a `Module` grant with the same (role, permission, sourceModuleCode)
already exists; remove is a no-op if no such grant exists. Repeated add/remove events leave no
duplicates and no missing rows.

### Target-role question (deferred to S3)

*Which* role(s) receive module grants (e.g. the tenant `Admin` role, an entitlement-specific role, or
a configured mapping) is an S3 implementation decision and is **not** locked here. These semantics
hold regardless of the target-role selection.
