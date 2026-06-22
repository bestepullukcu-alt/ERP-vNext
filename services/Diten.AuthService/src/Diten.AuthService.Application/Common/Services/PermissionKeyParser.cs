namespace Diten.AuthService.Application.Common.Services;

/// <summary>
/// Parses a catalog permission key into its <c>(module, resource, action)</c> components.
/// Mirrors the Platform catalog grammar (PKS-001 §1 / AG-STEP-004B D-6): the key is a
/// dot-separated, lowercase string of at least three segments, each matching <c>[a-z][a-z0-9-]*</c>.
/// The first segment is the module, the last is the action, and any segments in between form the
/// resource (joined with dots), so <c>Key</c> reconstructs losslessly even for deep resource paths
/// (e.g. <c>platform.tenants.commercial.subscription.activate</c>).
/// </summary>
public static class PermissionKeyParser
{
    public static bool TryParse(string? permissionKey, out string module, out string resource, out string action)
    {
        module = string.Empty;
        resource = string.Empty;
        action = string.Empty;

        if (string.IsNullOrWhiteSpace(permissionKey))
        {
            return false;
        }

        var key = permissionKey.Trim().ToLowerInvariant();
        var parts = key.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            return false;
        }

        foreach (var part in parts)
        {
            if (!IsValidSegment(part))
            {
                return false;
            }
        }

        module = parts[0];
        action = parts[^1];
        resource = string.Join('.', parts[1..^1]);
        return true;
    }

    private static bool IsValidSegment(string segment)
    {
        if (segment.Length == 0 || segment[0] is < 'a' or > 'z')
        {
            return false;
        }

        foreach (var ch in segment)
        {
            var allowed = ch is >= 'a' and <= 'z' or >= '0' and <= '9' or '-';
            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }
}
