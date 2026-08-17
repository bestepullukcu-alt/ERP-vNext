using System.Security.Claims;

namespace Diten.Web.Services;

/// <summary>
/// Builds the UX-only permission snapshot from the current principal's "permission" claims. Bounded:
/// it surfaces only canonical permission keys, nothing else. See <see cref="IPermissionSnapshot"/>.
/// </summary>
public sealed class PermissionSnapshot : IPermissionSnapshot
{
    public const string PermissionClaimType = "permission";

    private readonly HashSet<string> _keys;

    public PermissionSnapshot(IHttpContextAccessor httpContextAccessor)
    {
        var user = httpContextAccessor.HttpContext?.User;

        _keys = user is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : user.FindAll(PermissionClaimType)
                  .Select(c => c.Value)
                  .Where(v => !string.IsNullOrWhiteSpace(v))
                  .Select(v => v.Trim().ToLowerInvariant())
                  .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public bool Has(string permissionKey)
        => !string.IsNullOrWhiteSpace(permissionKey) && _keys.Contains(permissionKey.Trim());

    public IReadOnlyCollection<string> Keys => _keys;
}
