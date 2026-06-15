using System.Security.Claims;

namespace Diten.Web.Security;

public static class PermissionClaims
{
    private static readonly char[] PermissionSeparators = [',', ';', ' ', '\t', '\r', '\n'];

    public static bool HasPermission(ClaimsPrincipal? user, string permission)
    {
        if (user?.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(permission))
        {
            return false;
        }

        var target = permission.Trim();
        return EnumeratePermissions(user).Any(value =>
            string.Equals(value, "*", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, target, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> EnumeratePermissions(ClaimsPrincipal user)
    {
        foreach (var claim in user.Claims)
        {
            if (!IsPermissionClaim(claim.Type))
            {
                continue;
            }

            foreach (var value in SplitClaimValue(claim.Value))
            {
                yield return value;
            }
        }
    }

    private static bool IsPermissionClaim(string claimType)
    {
        return string.Equals(claimType, "permission", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(claimType, "permissions", StringComparison.OrdinalIgnoreCase) ||
               claimType.EndsWith("/permission", StringComparison.OrdinalIgnoreCase) ||
               claimType.EndsWith("/permissions", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> SplitClaimValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        foreach (var part in value.Split(PermissionSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return part;
        }
    }
}
