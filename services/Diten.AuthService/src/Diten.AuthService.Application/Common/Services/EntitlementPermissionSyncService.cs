using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;

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

    public EntitlementPermissionSyncService(
        IPermissionRepository permissions,
        IRoleRepository roles,
        IRolePermissionRepository rolePermissions)
    {
        _permissions = permissions;
        _roles = roles;
        _rolePermissions = rolePermissions;
    }

    public async Task GrantModuleAsync(Guid tenantId, string moduleCode, string actor, CancellationToken ct = default)
    {
        var code = ModulePermissionResolver.NormalizeModuleCode(moduleCode);
        if (code.Length == 0)
        {
            return; // fail-safe: blank module code is a no-op
        }

        var catalog = await _permissions.GetAllAsync(ct);
        var modulePermissions = ModulePermissionResolver.ResolvePermissions(moduleCode, catalog);
        if (modulePermissions.Count == 0)
        {
            return; // unmatched / platform module → no-op (resolver already excludes platform)
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
                var alreadyGranted = existing.Any(rp =>
                    rp.PermissionId == permission.Id
                    && rp.GrantSource == GrantSource.Module
                    && string.Equals(rp.SourceModuleCode, code, StringComparison.OrdinalIgnoreCase));

                if (alreadyGranted)
                {
                    continue; // idempotent: this module already granted this permission to this role
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
