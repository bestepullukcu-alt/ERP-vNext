using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Common.Services;

/// <summary>
/// BL-521 FIX4 item 2 — AUTH'S COPY OF A PLATFORM ADMINISTRATOR'S ROLES FOLLOWS THE PLATFORM RECORD. Platform writes the
/// record and syncs Auth, but a sync can be lost (Platform's update syncs Auth before its own write; a write refused
/// afterwards converges Auth to the record — and that can fail too), and some record changes never sync at all. So every
/// door that mints a platform token (login, refresh, forced password change) converges the copy to the roles the record
/// names right before minting, and the sync / provision use the same rule. Only the four platform roles are touched; any
/// other role the account holds is left as it is.
///
/// <para>FIX5 item 4 — the rule itself is robust now (this file used to live inside the status-client interface file):
/// (a) a role is recognised in any letter case, so a stored "superadmin" is revoked like "SuperAdmin"; (b) ROLES ARE
/// REVOKED FIRST, THEN GRANTED — a convergence that stops half way leaves the account with fewer rights, never more;
/// (c) a role is revoked by every role document that carries the name, and a grant is an idempotent upsert; a role the
/// account holds that cannot be revoked stops the door (nothing is minted from a copy that cannot be corrected).</para>
///
/// <para>FIX5 item 3 — TWO DEFAULTS, ON PURPOSE. The provisioning doors (provision, sync) keep their old rule: a platform
/// account never ends up without a platform role, so an empty list becomes ReadOnly. The MINT doors do NOT: an empty or
/// unrecognised role list in the record's answer means the record grants no platform role, so none is granted and the
/// sign-in is refused 403 as it always was for an account without a platform role. (FIX4 shared the provisioning default
/// with the mint doors, which gave every record with no roles a ReadOnly sign-in.)</para>
/// </summary>
public static class PlatformAdministratorRoleConvergence
{
    /// <summary>The description a platform role is created with.</summary>
    public const string RoleDescription = "Platform administrator role";

    /// <summary>The role a provisioned account gets when the record names none (provision / sync only).</summary>
    private const string ProvisioningDefaultRole = "ReadOnly";

    private static readonly string[] PlatformRoles = ["SuperAdmin", "BillingAdmin", "SupportAdmin", "ReadOnly"];

    /// <summary>One of the four platform roles (any letter case).</summary>
    public static bool IsPlatformRole(string role) => Canonical(role) is not null;

    /// <summary>
    /// The platform roles a record's role list stands for: known names only, canonical spelling, each once. May be empty.
    /// </summary>
    public static IReadOnlyList<string> Known(IEnumerable<string>? recordRoles) =>
        (recordRoles ?? [])
            .Select(Canonical)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// PROVISION / SYNC — brings the account's platform roles to the record's, and an empty list becomes ReadOnly (the rule
    /// the provisioning doors always had: a platform account never ends up without a platform role). Returns whether
    /// anything changed.
    /// </summary>
    public static Task<bool> ConvergeProvisionedAsync(
        IUserRoleRepository userRoles,
        IRoleRepository roles,
        Guid userId,
        Guid tenantId,
        IEnumerable<string>? recordRoles,
        CancellationToken ct)
    {
        var desired = Known(recordRoles);
        return ConvergeAsync(userRoles, roles, userId, tenantId, desired.Count == 0 ? [ProvisioningDefaultRole] : desired, ct);
    }

    /// <summary>
    /// SIGN-IN / REFRESH / FORCED CHANGE — brings the account's platform roles to exactly the record's: no role is added
    /// that the record does not name, and an empty or unrecognised list revokes every platform role the account holds
    /// (the sign-in is then refused 403). Returns whether anything changed.
    /// </summary>
    public static Task<bool> ConvergeAtMintAsync(
        IUserRoleRepository userRoles,
        IRoleRepository roles,
        Guid userId,
        Guid tenantId,
        IEnumerable<string>? recordRoles,
        CancellationToken ct) =>
        ConvergeAsync(userRoles, roles, userId, tenantId, Known(recordRoles), ct);

    private static async Task<bool> ConvergeAsync(
        IUserRoleRepository userRoles,
        IRoleRepository roles,
        Guid userId,
        Guid tenantId,
        IReadOnlyList<string> desired,
        CancellationToken ct)
    {
        var held = (await userRoles.GetRolesByUserAsync(userId, tenantId, ct))
            .Select(Canonical)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var missing = desired.Except(held, StringComparer.Ordinal).ToList();
        var extra = held.Except(desired, StringComparer.Ordinal).ToList();
        if (missing.Count == 0 && extra.Count == 0)
        {
            return false;
        }

        // REVOKE FIRST. A role is revoked through EVERY role document of the tenant whose name is that role in any letter
        // case (an account may hold "superadmin", a duplicate document may exist), and the repository removes every
        // matching assignment row. A role the account holds that no role document explains cannot be revoked — and the
        // token would then be minted with it — so the door stops here (fail closed; the account keeps what it had).
        if (extra.Count > 0)
        {
            var tenantRoles = (await roles.GetAllByTenantAsync(tenantId, ct)).ToList();
            foreach (var roleName in extra)
            {
                var documents = tenantRoles.Where(role => string.Equals(Canonical(role.Name), roleName, StringComparison.Ordinal)).ToList();
                if (documents.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"A platform role the account holds could not be found among the tenant's roles, so it cannot be revoked. UserId={userId}");
                }

                foreach (var document in documents)
                {
                    await userRoles.RevokeAsync(userId, document.Id, tenantId, ct);
                }
            }
        }

        // THEN GRANT, idempotently (the repository's assignment is an upsert on user + role + tenant).
        foreach (var roleName in missing)
        {
            var role = await roles.UpsertSystemRoleAsync(roleName, roleName, RoleDescription, tenantId, ct);
            await userRoles.AssignAsync(new UserRole(userId, role.Id, tenantId, "system"), ct);
        }

        return true;
    }

    private static string? Canonical(string? role) =>
        string.IsNullOrWhiteSpace(role)
            ? null
            : PlatformRoles.FirstOrDefault(known => string.Equals(known, role.Trim(), StringComparison.OrdinalIgnoreCase));
}
