using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Domain.Authorization;

/// <summary>
/// Single source of truth for the baseline permissions each default/system role receives.
/// Shared by the persistence <c>DataSeeder</c> (default tenant) and the runtime
/// <c>RoleProvisioningService</c> (every tenant) so the two cannot drift (OD-FE9-03 Option B).
/// Platform permissions are never granted to tenant roles (privilege-escalation boundary).
/// </summary>
public static class DefaultRolePermissionTemplate
{
    public const string SuperAdminRole = "SuperAdmin";
    public const string AdminRole = "Admin";
    public const string ViewerRole = "Viewer";

    public const string PlatformModule = "platform";
    public const string ReadAction = "read";

    /// <summary>Modules whose permissions the tenant Admin role receives in full.</summary>
    public static readonly IReadOnlyList<string> AdminModules = new[] { "auth", "mdm" };

    /// <summary>
    /// Returns the catalog permissions that <paramref name="roleName"/> should be granted.
    /// Deleted permissions are always excluded; platform permissions are excluded from tenant
    /// roles. Unknown roles get nothing.
    /// </summary>
    public static IReadOnlyList<Permission> SelectFor(string roleName, IEnumerable<Permission> catalog)
    {
        var available = catalog.Where(p => !p.IsDeleted);

        return roleName switch
        {
            // SuperAdmin (default-tenant only) keeps the full catalog.
            SuperAdminRole => available.ToList(),

            // Tenant roles: never platform.* (escalation boundary).
            AdminRole => available
                .Where(p => !IsPlatform(p) && AdminModules.Contains(p.Module))
                .ToList(),

            ViewerRole => available
                .Where(p => !IsPlatform(p) && string.Equals(p.Action, ReadAction, StringComparison.OrdinalIgnoreCase))
                .ToList(),

            _ => Array.Empty<Permission>()
        };
    }

    public static bool IsPlatform(Permission permission)
        => string.Equals(permission.Module, PlatformModule, StringComparison.OrdinalIgnoreCase);
}
