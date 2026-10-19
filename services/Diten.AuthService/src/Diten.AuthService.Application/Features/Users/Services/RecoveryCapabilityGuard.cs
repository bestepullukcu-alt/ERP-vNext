using Diten.AuthService.Application.Common.Interfaces;

namespace Diten.AuthService.Application.Features.Users.Services;

/// <summary>
/// A change that can take the recovery capability (<see cref="RecoveryCapabilityGuard.RecoveryPermission"/>) away from
/// someone: the five administrator paths that can leave a tenant with nobody able to put an account back.
/// </summary>
public abstract record RecoveryCapabilityChange
{
    private RecoveryCapabilityChange() { }

    /// <summary>The user is soft-deleted (DELETE api/users/{id}).</summary>
    public sealed record RemoveUser(Guid UserId) : RecoveryCapabilityChange;

    /// <summary>The user goes from active to inactive (the kebab's disable, or the edit form's Active switch).</summary>
    public sealed record DeactivateUser(Guid UserId) : RecoveryCapabilityChange;

    /// <summary>One role is taken from one user (User Roles screen).</summary>
    public sealed record RevokeUserRole(Guid UserId, Guid RoleId) : RecoveryCapabilityChange;

    /// <summary>A custom role is deleted (Roles screen).</summary>
    public sealed record DeleteRole(Guid RoleId) : RecoveryCapabilityChange;

    /// <summary>A manual grant is taken from a role (Role Permissions screen); <paramref name="PermissionKey"/> may be null.</summary>
    public sealed record RevokeRolePermission(Guid RoleId, string? PermissionKey) : RecoveryCapabilityChange;
}

/// <summary>
/// ⚠ A TENANT MUST NOT BE ABLE TO LOCK ITSELF OUT — the rule, in one place (WP-USERS-ROLES-SMALL-02, BL-558 follow-up).
/// </summary>
public interface IRecoveryCapabilityGuard
{
    /// <summary>
    /// True — REFUSE — when the change takes the recovery capability from someone who holds it now (through a live role,
    /// active or not) AND afterwards no ACTIVE user is left holding it. Tenant-scoped on every read.
    /// </summary>
    Task<bool> WouldLeaveNoActiveHolderAsync(Guid tenantId, RecoveryCapabilityChange change, CancellationToken ct);
}

/// <summary>
/// The last-steward rule for every administrator path (delete, deactivate, revoke a user's role, delete a role, revoke a
/// role's permission). Until 2026-10 only the delete was guarded, and it counted inactive accounts as holders.
/// <list type="bullet">
///   <item><b>Who loses it.</b> A holder is a live user (not soft-deleted) assigned to a live role that carries
///   <see cref="RecoveryPermission"/>. The change's LOSERS are the holders who no longer hold it afterwards — an inactive
///   holder included: deleting the only, inactive administrator also destroys the way to switch them back on.</item>
///   <item><b>Who is left.</b> Only an ACTIVE holder (IsActive; a pending invitation is inactive; a temporary lockout
///   still counts) can put an account back, so only an active holder outside the losers keeps the tenant alive.</item>
///   <item><b>BL-558.</b> A change that makes nobody lose the capability (the user holds nothing, holds it through another
///   live role too, the role's members hold it elsewhere, or the permission is not the recovery one) is never refused —
///   a tenant that has already lost its last holder must still be able to tidy up.</item>
/// </list>
/// Users are read through the tenant-scoped <see cref="IUserRepository.GetByIdAndTenantAsync"/> (a platform account is
/// invisible to a tenant actor there, so it is never counted for one). SAP and Oracle protect the last administrator the
/// same way; the check runs before any write, there is no transaction (a concurrent pair of changes is not covered).
/// </summary>
public sealed class RecoveryCapabilityGuard : IRecoveryCapabilityGuard
{
    /// <summary>The capability that makes recovery possible: whoever holds it can put an account back.</summary>
    public const string RecoveryPermission = "auth.users.create";

    private readonly IUserRepository _users;
    private readonly IUserRoleRepository _userRoles;
    private readonly IRoleRepository _roles;
    private readonly IRolePermissionRepository _rolePermissions;

    public RecoveryCapabilityGuard(
        IUserRepository users,
        IUserRoleRepository userRoles,
        IRoleRepository roles,
        IRolePermissionRepository rolePermissions)
    {
        _users = users;
        _userRoles = userRoles;
        _roles = roles;
        _rolePermissions = rolePermissions;
    }

    public async Task<bool> WouldLeaveNoActiveHolderAsync(Guid tenantId, RecoveryCapabilityChange change, CancellationToken ct)
    {
        // A revoked permission that is not the recovery one (or unknown) takes nothing away.
        if (change is RecoveryCapabilityChange.RevokeRolePermission { PermissionKey: var key }
            && !string.Equals(key, RecoveryPermission, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Live roles carrying the capability, and their assignment rows (role → user ids), tenant-scoped.
        var holdersByRole = new Dictionary<Guid, IReadOnlyCollection<Guid>>();
        foreach (var role in await _roles.GetAllByTenantAsync(tenantId, ct))
        {
            var permissions = await _rolePermissions.GetPermissionsByRoleAsync(role.Id, tenantId, ct);
            if (permissions.Any(p => string.Equals(p, RecoveryPermission, StringComparison.OrdinalIgnoreCase)))
            {
                holdersByRole[role.Id] = await _userRoles.GetUserIdsByRoleAsync(role.Id, tenantId, ct);
            }
        }

        // What the change takes away: a whole role, one assignment row, or one person.
        Guid? lostRole = change switch
        {
            RecoveryCapabilityChange.DeleteRole c => c.RoleId,
            RecoveryCapabilityChange.RevokeRolePermission c => c.RoleId,
            _ => null
        };
        var lostAssignment = change as RecoveryCapabilityChange.RevokeUserRole;
        Guid? lostUser = change switch
        {
            RecoveryCapabilityChange.RemoveUser c => c.UserId,
            RecoveryCapabilityChange.DeactivateUser c => c.UserId,
            _ => null
        };

        // Who holds it before, and who still holds it after the change.
        var before = holdersByRole.SelectMany(r => r.Value).ToHashSet();
        var after = new HashSet<Guid>();
        foreach (var (roleId, userIds) in holdersByRole)
        {
            if (roleId == lostRole) continue;
            foreach (var userId in userIds)
            {
                if (userId == lostUser) continue;
                if (lostAssignment is not null && userId == lostAssignment.UserId && roleId == lostAssignment.RoleId) continue;
                after.Add(userId);
            }
        }

        // Is an ACTIVE holder left? The first one found settles it.
        foreach (var userId in after)
        {
            var user = await _users.GetByIdAndTenantAsync(userId, tenantId, ct);
            if (user is not null && user.IsActive)
            {
                return false;
            }
        }

        // Nobody active is left. Refuse only if the change itself takes the capability from a real (live) holder —
        // otherwise it shrinks nothing and a tenant without holders can still tidy up (BL-558).
        foreach (var userId in before.Where(userId => !after.Contains(userId)))
        {
            if (await _users.GetByIdAndTenantAsync(userId, tenantId, ct) is not null)
            {
                return true;
            }
        }

        return false;
    }
}
