using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Domain.Authorization;

/// <summary>
/// FEAT-ROLE-GRANT-RECONCILE — pure decision core for the startup backfill that gives EXISTING tenant Admin roles
/// the self-service permissions added to <see cref="DefaultRolePermissionTemplate.TenantSelfServicePermissions"/>
/// after those tenants were provisioned. Kept free of persistence so the "which grants to add" logic is unit-tested
/// in isolation (the <c>DataSeeder</c> only wires it to Mongo + the version bump), mirroring how the shared template
/// separates selection from the seeder.
/// </summary>
public static class TenantAdminSelfServiceReconciler
{
    /// <summary>A role considered for reconciliation (id + role name + owning tenant).</summary>
    public readonly record struct RoleRef(Guid RoleId, string Name, Guid TenantId);

    /// <summary>A missing grant the caller must persist (as a System grant) for the given tenant.</summary>
    public readonly record struct PlannedGrant(Guid RoleId, Guid PermissionId, Guid TenantId);

    /// <summary>
    /// Plans the missing self-service grants. ONLY roles named <see cref="DefaultRolePermissionTemplate.AdminRole"/>
    /// are considered (SuperAdmin, Viewer and custom roles are ignored), and ONLY the curated self-service subset of
    /// the catalog is granted (never the full Admin template, so a customized Admin role is not clobbered). Additive:
    /// a grant is planned for each (Admin role × self-service permission) pair NOT already in <paramref name="existingGrants"/>.
    /// Deterministic and idempotent — feeding the resulting grants back into <paramref name="existingGrants"/> yields none.
    /// </summary>
    /// <param name="roles">All roles in scope (mixed role names are fine; non-Admin roles are skipped).</param>
    /// <param name="catalog">The permission catalog; the self-service subset is selected by Key.</param>
    /// <param name="existingGrants">The (RoleId, PermissionId) pairs already granted.</param>
    public static IReadOnlyList<PlannedGrant> PlanMissingGrants(
        IEnumerable<RoleRef> roles,
        IEnumerable<Permission> catalog,
        ISet<(Guid RoleId, Guid PermissionId)> existingGrants)
    {
        var selfService = catalog
            .Where(p => !p.IsDeleted && DefaultRolePermissionTemplate.TenantSelfServicePermissions.Contains(p.Key))
            .ToList();

        var planned = new List<PlannedGrant>();
        if (selfService.Count == 0) return planned;

        foreach (var role in roles)
        {
            if (!string.Equals(role.Name, DefaultRolePermissionTemplate.AdminRole, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var perm in selfService)
            {
                if (existingGrants.Contains((role.RoleId, perm.Id)))
                {
                    continue;
                }

                planned.Add(new PlannedGrant(role.RoleId, perm.Id, role.TenantId));
            }
        }

        return planned;
    }
}
