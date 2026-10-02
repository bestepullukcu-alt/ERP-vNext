using System.Security.Cryptography;
using System.Text;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Domain.Authorization;

/// <summary>
/// The pure decision core of the ONE-WAY export backfill, shared by every "{module}.export" key that used to be covered
/// by its "{module}.read" key (BL-452: <c>auth.users.export</c>; WP-ROLES-CLOSE-01: <c>auth.roles.export</c>).
///
/// <para><b>What it does.</b> Before an export key existed, exporting a list needed only its read key. On the upgrade
/// every role of an OLD tenant that holds read also receives export — nobody loses a capability they had yesterday —
/// once per tenant and key.</para>
///
/// <para><b>Old or born — a PERSISTENT MARK, never a guess.</b> A tenant is decided by its
/// <c>permissionReconciliations</c> row (tenant, export key), not by what its roles happen to hold:
/// <list type="bullet">
/// <item><c>born</c> — written BEFORE a build that knows the key creates the tenant's first role (seeder, role
/// repository). Such a tenant was configured with the key in the catalog; it never receives a backfill, so a reading
/// role its administrator opened without export stays that way.</item>
/// <item><c>backfilled</c> — written after the backfill finished that tenant. It is never looked at again, so an
/// administrator's later revoke sticks.</item>
/// <item>no row, and the tenant has roles — an OLD tenant: processed, whatever its roles already hold (an Admin that got
/// the key from the template or from an entitlement sync before the first run does not make the tenant "done").</item>
/// </list></para>
///
/// <para><b>Restartable.</b> A tenant is marked only after all of its grants are written; a run that stops half-way
/// leaves it unmarked and the next run finishes it. The audit row and the mark carry DETERMINISTIC ids
/// (<see cref="AuditId"/>, <see cref="MarkId"/>), so a re-run or a second instance starting at the same time writes
/// the same documents and a duplicate key means "already done".</para>
///
/// <para>⚠ This plans AUTHORITY WRITES (role → permission grants made by the system, not by a person).</para>
/// </summary>
public static class ExportGrantBackfill
{
    public const string OriginBorn = "born";
    public const string OriginBackfilled = "backfilled";

    /// <summary>The audit event a backfilled grant is written as — the same name a person's grant carries.</summary>
    public const string AuditEventName = "role_permission_granted";

    /// <summary>One read → export pair and what its audit rows name as their source.</summary>
    public sealed record KeyPair(string ReadKey, string ExportKey, string AuditSource);

    public static readonly KeyPair Users = new("auth.users.read", "auth.users.export", "users-export-backfill");
    public static readonly KeyPair Roles = new("auth.roles.read", "auth.roles.export", "roles-export-backfill");

    /// <summary>Every pair the backfill covers. A new "{module}.export" key is one line here.</summary>
    public static readonly IReadOnlyList<KeyPair> Keys = [Users, Roles];

    public sealed record RoleState(Guid RoleId, string Name, Guid TenantId, bool IsSystem);

    public sealed record PlannedGrant(Guid RoleId, string RoleName, Guid TenantId, Guid PermissionId, GrantSource Source);

    /// <summary>One unmarked tenant: the grants it still lacks (possibly none) — after which it is marked.</summary>
    public sealed record TenantPlan(Guid TenantId, IReadOnlyList<PlannedGrant> Grants);

    /// <summary>
    /// The tenants to process, in a stable order, each with the grants it still lacks. A tenant in
    /// <paramref name="markedTenants"/> (born or already backfilled) is not looked at; neither is a role without a tenant
    /// (<see cref="Guid.Empty"/>). Additive only, deterministic, idempotent: a role that already holds export — from a
    /// template, an entitlement sync, a person or an earlier, interrupted run — plans no grant.
    /// </summary>
    /// <param name="templateGrantsExport">True when the default role template itself gives export to this (system) role.</param>
    public static IReadOnlyList<TenantPlan> Plan(
        IEnumerable<RoleState> roles,
        ISet<(Guid RoleId, Guid PermissionId)> grants,
        Guid readPermissionId,
        Guid exportPermissionId,
        IReadOnlySet<Guid> markedTenants,
        Func<RoleState, bool> templateGrantsExport)
    {
        return roles
            .Where(r => r.TenantId != Guid.Empty && !markedTenants.Contains(r.TenantId))
            .GroupBy(r => r.TenantId)
            .OrderBy(g => g.Key)
            .Select(tenant => new TenantPlan(
                tenant.Key,
                tenant
                    .Where(r => grants.Contains((r.RoleId, readPermissionId)) && !grants.Contains((r.RoleId, exportPermissionId)))
                    .OrderBy(r => r.RoleId)
                    .Select(r => new PlannedGrant(r.RoleId, r.Name, r.TenantId, exportPermissionId, SourceFor(r, templateGrantsExport)))
                    .ToList()))
            .ToList();
    }

    /// <summary>
    /// How a backfilled grant is filed. A SYSTEM role the default template itself gives export to (Admin) keeps a
    /// template-managed <see cref="GrantSource.System"/> grant, like the rest of its baseline. Everything else — a
    /// custom role, and a system role the template does NOT give export to (Viewer) — gets a
    /// <see cref="GrantSource.Manual"/> grant: nothing re-provisions it, so the tenant's administrator must be able to
    /// take it away on the Role Permissions screen (a System grant answers 409 there and can never be removed).
    /// </summary>
    public static GrantSource SourceFor(RoleState role, Func<RoleState, bool> templateGrantsExport)
        => role.IsSystem && templateGrantsExport(role) ? GrantSource.System : GrantSource.Manual;

    /// <summary>The id of THE mark of a tenant and key: two writers produce the same document.</summary>
    public static Guid MarkId(Guid tenantId, string exportKey) => Deterministic($"export-backfill-mark|{tenantId:N}|{exportKey}");

    /// <summary>The id of THE audit row of a backfilled grant: written once however often the run is repeated.</summary>
    public static Guid AuditId(Guid tenantId, Guid roleId, string exportKey) => Deterministic($"export-backfill-audit|{tenantId:N}|{roleId:N}|{exportKey}");

    private static Guid Deterministic(string name) => new(MD5.HashData(Encoding.UTF8.GetBytes(name)));
}
