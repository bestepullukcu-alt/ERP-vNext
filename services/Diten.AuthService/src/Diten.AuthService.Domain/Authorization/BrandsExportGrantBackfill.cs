using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Domain.Authorization;

/// <summary>
/// WP-MDM-BRAND-ACCEPT-01 (M-4, Control Tower decision) — pure decision core of the ONE-WAY backfill that ships with the
/// new <c>mdm.brands.export</c> key. A deliberate parallel of <see cref="UsersExportGrantBackfill"/> (same shape, same
/// rules, own keys), not a refactor of it.
///
/// <para>Before the key existed, the browser-side copy / print / file entries of the Brands list needed only
/// <c>mdm.brands.read</c>. So on the upgrade every role that holds read must also get export — nobody loses a capability
/// they had yesterday. It must happen ONCE per tenant: a backfill that re-ran on every start would hand export back to
/// every reading role forever, and an administrator could never take it away again (revokes are hard deletes).</para>
///
/// <para><b>The once-per-tenant gate is a PERSISTENT MARK</b>: a <c>permissionReconciliations</c> row
/// (tenant, <see cref="ExportKey"/>, date) written for every tenant the backfill has looked at. A marked tenant is never
/// looked at again, whatever its roles hold — so an administrator's revoke sticks. The role-state snapshot stays as a
/// second guard for tenants seen for the first time: one whose non-SuperAdmin role already held export BEFORE the run is
/// marked without granting anything. SuperAdmin is excluded from that snapshot because its full-catalog grant is
/// automatic.</para>
/// </summary>
public static class BrandsExportGrantBackfill
{
    public const string ReadKey = "mdm.brands.read";
    public const string ExportKey = "mdm.brands.export";

    /// <summary>The tenants the backfill looks at in this run and must mark afterwards: every tenant with a role, not yet marked.</summary>
    public static IReadOnlySet<Guid> TenantsToMark(IEnumerable<TenantAdminSelfServiceReconciler.RoleRef> roles, IReadOnlySet<Guid> alreadyMarked)
        => roles.Select(r => r.TenantId).Where(t => !alreadyMarked.Contains(t)).ToHashSet();

    /// <summary>The tenants that already hold export on a non-SuperAdmin role. Empty when the key is not in the catalog yet.</summary>
    public static IReadOnlySet<Guid> TenantsAlreadyOnExport(
        IEnumerable<TenantAdminSelfServiceReconciler.RoleRef> roles,
        ISet<(Guid RoleId, Guid PermissionId)> grants,
        Guid? exportPermissionId)
    {
        var tenants = new HashSet<Guid>();
        if (exportPermissionId is not { } exportId) return tenants;

        foreach (var role in roles)
        {
            if (string.Equals(role.Name, DefaultRolePermissionTemplate.SuperAdminRole, StringComparison.Ordinal)) continue;
            if (grants.Contains((role.RoleId, exportId))) tenants.Add(role.TenantId);
        }

        return tenants;
    }

    /// <summary>
    /// For every role that holds read and not export, in a tenant NOT in <paramref name="tenantsAlreadyOnExport"/>, one
    /// export grant. Additive only (never removes), deterministic, idempotent: feeding the result back into
    /// <paramref name="grants"/> plans nothing.
    /// </summary>
    public static IReadOnlyList<TenantAdminSelfServiceReconciler.PlannedGrant> PlanMissingGrants(
        IEnumerable<TenantAdminSelfServiceReconciler.RoleRef> roles,
        ISet<(Guid RoleId, Guid PermissionId)> grants,
        Guid readPermissionId,
        Guid exportPermissionId,
        IReadOnlySet<Guid> tenantsAlreadyOnExport)
    {
        var planned = new List<TenantAdminSelfServiceReconciler.PlannedGrant>();
        foreach (var role in roles)
        {
            if (tenantsAlreadyOnExport.Contains(role.TenantId)) continue;
            if (!grants.Contains((role.RoleId, readPermissionId))) continue;
            if (grants.Contains((role.RoleId, exportPermissionId))) continue;

            planned.Add(new TenantAdminSelfServiceReconciler.PlannedGrant(role.RoleId, exportPermissionId, role.TenantId));
        }

        return planned;
    }
}
