namespace Diten.AuthService.Domain.Authorization;

/// <summary>
/// WP-ROLES-CLOSE-01 — the ONE-WAY backfill that ships with the new <c>auth.roles.export</c> key; the role side of
/// <see cref="UsersExportGrantBackfill"/> and the same decision core (that class's rules take permission ids, not keys,
/// so they are reused, not copied).
///
/// <para>Before the key existed, exporting the Roles list needed only <c>auth.roles.read</c> (the browser's own CSV /
/// Excel buttons). So on the upgrade every role that holds read also gets export — nobody loses a capability they had
/// yesterday — ONCE per tenant, gated by a persistent <c>permissionReconciliations</c> mark (tenant,
/// <see cref="ExportKey"/>): a marked tenant is never looked at again, so an administrator's revoke sticks. A role
/// without read is never given export. A tenant whose non-SuperAdmin role already held export before the run is
/// marked without granting anything.</para>
///
/// <para>⚠ This WRITES AUTHORITY (role → permission grants). Every grant it makes is also written to
/// <c>authAuditLogs</c> as <c>role_permission_granted</c> with the system actor (see <c>DataSeeder</c>).</para>
/// </summary>
public static class RolesExportGrantBackfill
{
    public const string ReadKey = "auth.roles.read";
    public const string ExportKey = "auth.roles.export";

    /// <summary>What the audit row of a backfilled grant says about where it came from.</summary>
    public const string AuditSource = "roles-export-backfill";

    public static IReadOnlySet<Guid> TenantsToMark(IEnumerable<TenantAdminSelfServiceReconciler.RoleRef> roles, IReadOnlySet<Guid> alreadyMarked)
        => UsersExportGrantBackfill.TenantsToMark(roles, alreadyMarked);

    public static IReadOnlySet<Guid> TenantsAlreadyOnExport(
        IEnumerable<TenantAdminSelfServiceReconciler.RoleRef> roles,
        ISet<(Guid RoleId, Guid PermissionId)> grants,
        Guid? exportPermissionId)
        => UsersExportGrantBackfill.TenantsAlreadyOnExport(roles, grants, exportPermissionId);

    public static IReadOnlyList<TenantAdminSelfServiceReconciler.PlannedGrant> PlanMissingGrants(
        IEnumerable<TenantAdminSelfServiceReconciler.RoleRef> roles,
        ISet<(Guid RoleId, Guid PermissionId)> grants,
        Guid readPermissionId,
        Guid exportPermissionId,
        IReadOnlySet<Guid> tenantsAlreadyOnExport)
        => UsersExportGrantBackfill.PlanMissingGrants(roles, grants, readPermissionId, exportPermissionId, tenantsAlreadyOnExport);
}
