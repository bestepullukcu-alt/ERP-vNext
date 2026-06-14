namespace Diten.Web.Services;

/// <summary>
/// UX-ONLY bounded snapshot of the current user's permission keys, read from the authenticated
/// principal's "permission" claims (already present in the cookie — no extra API call). Used only to
/// show/hide UI affordances. This is NOT an enforcement boundary: backend <c>[HasPermission]</c> and
/// the server-side <c>ShellAccessFilter</c> remain authoritative (BME-001). Exposes only canonical
/// PKS-001 permission keys — never roles, raw JWT, or other claims.
/// </summary>
public interface IPermissionSnapshot
{
    /// <summary>True if the current user holds the given canonical permission key (case-insensitive).</summary>
    bool Has(string permissionKey);

    /// <summary>The bounded set of canonical permission keys the current user holds.</summary>
    IReadOnlyCollection<string> Keys { get; }
}
