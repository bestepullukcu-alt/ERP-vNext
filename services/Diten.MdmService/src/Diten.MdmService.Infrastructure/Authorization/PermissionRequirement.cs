using Microsoft.AspNetCore.Authorization;

namespace Diten.MdmService.Infrastructure.Authorization;

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// WP-MDM-BRAND-ACCEPT-01 FIX1 (item 11) — separates the alternatives of an any-of policy ("a|b"). No permission key
    /// contains it, so a single-key policy is read exactly as before.
    /// </summary>
    public const string AnyOfSeparator = "|";

    public PermissionRequirement(string permission)
    {
        Permission = permission;
        AnyOf = permission.Split(AnyOfSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>The policy's permission text as written (one key, or several joined by <see cref="AnyOfSeparator"/>).</summary>
    public string Permission { get; }

    /// <summary>The keys of which ANY admits; one entry for an ordinary single-key policy.</summary>
    public IReadOnlyList<string> AnyOf { get; }
}
