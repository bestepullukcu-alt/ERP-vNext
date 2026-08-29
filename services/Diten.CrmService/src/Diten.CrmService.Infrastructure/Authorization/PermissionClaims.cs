using System.Security.Claims;

namespace Diten.CrmService.Infrastructure.Authorization;

/// <summary>
/// Reads permission claims off the caller principal. Mirrors <see cref="PermissionAuthorizationHandler"/>'s claim match
/// so controllers can make a soft, non-401 decision (e.g. mask a Contact 360 sub-block) rather than hard-deny the page.
/// </summary>
public static class PermissionClaims
{
    public static bool HasPermission(ClaimsPrincipal user, string permission)
        => user.Claims.Any(claim =>
            (string.Equals(claim.Type, "permission", StringComparison.OrdinalIgnoreCase)
             || string.Equals(claim.Type, "permissions", StringComparison.OrdinalIgnoreCase))
            && string.Equals(claim.Value, permission, StringComparison.OrdinalIgnoreCase));
}
