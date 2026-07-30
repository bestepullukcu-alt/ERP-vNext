using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Common.Authorization;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Common.Services;

/// <summary>
/// Applies the locked entitlement → role-permission revoke semantics
/// (services/Diten.AuthService/docs/entitlement-permission-bridge.md, S2) over the S1 grant-source
/// fields. Pure-ish (repository-backed, no transport) so it is unit-testable and reused by the
/// eventing consumer.
/// </summary>
public sealed class EntitlementPermissionSyncService : IEntitlementPermissionSyncService
{
    // Role targeting (S2 left this to S3): the default-provisioned tenant roles receive module grants.
    // Admin gets the module's full permission set; Viewer gets the module's read permissions only —
    // mirroring the S1 baseline role semantics (administrative vs read-only), scoped to the module.
    private static readonly string[] TargetRoleNames =
        [DefaultRolePermissionTemplate.AdminRole, DefaultRolePermissionTemplate.ViewerRole];

    private readonly IPermissionRepository _permissions;
    private readonly IRoleRepository _roles;
    private readonly IRolePermissionRepository _rolePermissions;
    private readonly IEntitlementPermissionPolicy _policy;
    private readonly ILogger<EntitlementPermissionSyncService> _logger;

    public EntitlementPermissionSyncService(
        IPermissionRepository permissions,
        IRoleRepository roles,
        IRolePermissionRepository rolePermissions,
        IEntitlementPermissionPolicy policy,
        ILogger<EntitlementPermissionSyncService> logger)
    {
        _permissions = permissions;
        _roles = roles;
        _rolePermissions = rolePermissions;
        _policy = policy;
        _logger = logger;
    }

    public async Task GrantModuleAsync(Guid tenantId, string moduleCode, string actor, CancellationToken ct = default)
    {
        var code = ModulePermissionResolver.NormalizeModuleCode(moduleCode);
        if (code.Length == 0)
        {
            return; // fail-safe: blank module code is a no-op
        }
        if (_policy.Resolve(code) == EntitlementPermissionMode.ExplicitOnlyPreserveOnEntitlementRemoval)
        {
            return;
        }

        var catalog = await _permissions.GetAllAsync(ct);
        var modulePermissions = ModulePermissionResolver.ResolvePermissions(moduleCode, catalog);
        // unmatched / platform module → no-op (resolver already excludes platform)
        await GrantPermissionsToRolesAsync(tenantId, code, modulePermissions, actor, ct);
    }

    public async Task GrantModuleWithKeysAsync(
        Guid tenantId,
        string moduleCode,
        IReadOnlyCollection<string> permissionKeys,
        string actor,
        CancellationToken ct = default)
    {
        var code = ModulePermissionResolver.NormalizeModuleCode(moduleCode);
        if (code.Length == 0)
        {
            return; // fail-safe: blank module code is a no-op
        }
        if (_policy.Resolve(code) == EntitlementPermissionMode.ExplicitOnlyPreserveOnEntitlementRemoval)
        {
            return;
        }

        var keySet = (permissionKeys ?? Array.Empty<string>())
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // No declared keys (module ships no descriptors yet, or the catalog pull failed) → fall back to the
        // convention + allow-list resolver so workflow / goldencompact and friends still get granted.
        if (keySet.Count == 0)
        {
            await GrantModuleAsync(tenantId, moduleCode, actor, ct);
            return;
        }

        // Catalog is authoritative: grant exactly the permissions the module DECLARES, by Key — namespace-agnostic,
        // so organization's platform.organization-units.* / platform.positions.* keys resolve where the convention
        // (Module==ModuleCode) could not. Per-role selection (Admin=full, Viewer=read) is preserved.
        var catalog = await _permissions.GetAllAsync(ct);
        var modulePermissions = catalog
            .Where(p => !p.IsDeleted && keySet.Contains(p.Key))
            .ToList();

        await GrantPermissionsToRolesAsync(tenantId, code, modulePermissions, actor, ct);
    }

    // Shared role-grant body: assigns the resolved module permissions to the target roles as Module-grants
    // (Admin = full set, Viewer = read-only), idempotently. Empty set → no-op.
    private async Task GrantPermissionsToRolesAsync(
        Guid tenantId,
        string code,
        IReadOnlyList<Permission> modulePermissions,
        string actor,
        CancellationToken ct)
    {
        if (modulePermissions.Count == 0)
        {
            return;
        }

        foreach (var roleName in TargetRoleNames)
        {
            var role = await _roles.GetByNameAndTenantAsync(roleName, tenantId, ct);
            if (role is null)
            {
                continue; // role not provisioned yet → skip (idempotent / fail-safe)
            }

            var rolePermissions = SelectForRole(roleName, modulePermissions);
            if (rolePermissions.Count == 0)
            {
                continue;
            }

            var existing = await _rolePermissions.GetByRoleAsync(role.Id, tenantId, ct);

            foreach (var permission in rolePermissions)
            {
                // Align with the unique index (RoleId, PermissionId, TenantId): if this permission is ALREADY
                // granted to the role by ANY source (System baseline / Manual operator / another Module), do NOT
                // insert — that would hit E11000 and abort the whole sync. Safe-skip keeps baseline/manual intact
                // (we never downgrade or duplicate); the permission is already effective for the role.
                var alreadyGranted = existing.Any(rp => rp.PermissionId == permission.Id);
                if (alreadyGranted)
                {
                    continue; // idempotent
                }

                await _rolePermissions.AssignAsync(
                    RolePermission.ModuleGrant(role.Id, permission.Id, tenantId, actor, code),
                    ct);
            }
        }
    }

    public async Task RevokeModuleAsync(Guid tenantId, string moduleCode, string actor, CancellationToken ct = default)
    {
        var code = ModulePermissionResolver.NormalizeModuleCode(moduleCode);
        if (code.Length == 0)
        {
            return;
        }
        if (_policy.Resolve(code) == EntitlementPermissionMode.ExplicitOnlyPreserveOnEntitlementRemoval)
        {
            return;
        }

        foreach (var roleName in TargetRoleNames)
        {
            var role = await _roles.GetByNameAndTenantAsync(roleName, tenantId, ct);
            if (role is null)
            {
                continue;
            }

            var existing = await _rolePermissions.GetByRoleAsync(role.Id, tenantId, ct);

            // Drop ONLY this module's grants. System (baseline) and Manual (operator) grants — and
            // Module grants from other source modules (shared permissions) — are left untouched, so a
            // shared permission survives until its last contributing entitlement is removed.
            var toRemove = existing
                .Where(rp => rp.GrantSource == GrantSource.Module
                             && string.Equals(rp.SourceModuleCode, code, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var rp in toRemove)
            {
                await _rolePermissions.RemoveByIdAsync(rp.Id, tenantId, ct);
            }
        }
    }

    public async Task SyncTenantModulesAsync(
        Guid tenantId,
        IReadOnlyCollection<string> entitledModuleCodes,
        string actor,
        CancellationToken ct = default)
    {
        // Normalize + dedupe the authoritative entitled set (blank codes dropped).
        var entitled = (entitledModuleCodes ?? Array.Empty<string>())
            .Select(ModulePermissionResolver.NormalizeModuleCode)
            .Where(c => c.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 1) Grant every currently-entitled module (idempotent; no-op for already-granted). Best-effort PER
        //    module: one module's failure (e.g. a transient repo error) must not abort the rest — workflow must
        //    still be granted even if goldenslim hiccuped.
        foreach (var code in entitled)
        {
            try
            {
                await GrantModuleAsync(tenantId, code, actor, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "entitlement.sync.grant_failed TenantId={TenantId} ModuleCode={ModuleCode}", tenantId, code);
            }
        }

        // 2) Revoke Module-grants whose source module is no longer entitled (plan downgrade / removal). System
        //    (baseline) and Manual (operator) grants are never considered. Other modules' grants are preserved.
        await RevokeStaleModulesAsync(tenantId, entitled, actor, ct);
    }

    public async Task SyncTenantModulesWithKeysAsync(
        Guid tenantId,
        IReadOnlyCollection<EntitledModulePermissionKeys> modules,
        string actor,
        CancellationToken ct = default)
    {
        var list = (modules ?? Array.Empty<EntitledModulePermissionKeys>())
            .Where(m => m is not null && !string.IsNullOrWhiteSpace(m.ModuleCode))
            .ToList();

        // The authoritative entitled set (normalized + deduped) for the revoke pass.
        var entitled = list
            .Select(m => ModulePermissionResolver.NormalizeModuleCode(m.ModuleCode))
            .Where(c => c.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 1) Grant every entitled module from its DECLARED catalog key set (per-module convention fallback when a
        //    module declares no keys). Best-effort PER module: one module's failure must not abort the rest.
        foreach (var module in list)
        {
            try
            {
                await GrantModuleWithKeysAsync(tenantId, module.ModuleCode, module.PermissionKeys, actor, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "entitlement.sync.grant_failed TenantId={TenantId} ModuleCode={ModuleCode}", tenantId, module.ModuleCode);
            }
        }

        // 2) Revoke Module-grants whose source module is no longer entitled. Identical semantics to the
        //    convention-based sync — System/Manual grants are never touched.
        await RevokeStaleModulesAsync(tenantId, entitled, actor, ct);
    }

    // Drops Module-grants whose source module is no longer in the entitled set. System (baseline) and Manual
    // (operator) grants are never considered; other modules' grants are preserved (shared-permission safe).
    private async Task RevokeStaleModulesAsync(
        Guid tenantId,
        IReadOnlySet<string> entitled,
        string actor,
        CancellationToken ct)
    {
        foreach (var roleName in TargetRoleNames)
        {
            var role = await _roles.GetByNameAndTenantAsync(roleName, tenantId, ct);
            if (role is null)
            {
                continue;
            }

            var existing = await _rolePermissions.GetByRoleAsync(role.Id, tenantId, ct);
            var staleSourceCodes = existing
                .Where(rp => rp.GrantSource == GrantSource.Module && !string.IsNullOrWhiteSpace(rp.SourceModuleCode))
                .Select(rp => ModulePermissionResolver.NormalizeModuleCode(rp.SourceModuleCode))
                .Where(c => c.Length > 0 && !entitled.Contains(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var stale in staleSourceCodes)
            {
                try
                {
                    await RevokeModuleAsync(tenantId, stale, actor, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "entitlement.sync.revoke_failed TenantId={TenantId} ModuleCode={ModuleCode}", tenantId, stale);
                }
            }
        }
    }

    private static IReadOnlyList<Permission> SelectForRole(string roleName, IReadOnlyList<Permission> modulePermissions)
        => roleName switch
        {
            DefaultRolePermissionTemplate.AdminRole => modulePermissions,
            DefaultRolePermissionTemplate.ViewerRole => modulePermissions
                .Where(p => string.Equals(p.Action, DefaultRolePermissionTemplate.ReadAction, StringComparison.OrdinalIgnoreCase))
                .ToList(),
            _ => Array.Empty<Permission>()
        };
}
