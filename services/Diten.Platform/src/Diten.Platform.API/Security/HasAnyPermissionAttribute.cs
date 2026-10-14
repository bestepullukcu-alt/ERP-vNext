using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Diten.Platform.API.Security;

/// <summary>
/// WP-MVP1-SEC-ESIGN1 — "ANY ONE of these keys is enough". <see cref="HasPermissionAttribute"/> takes one key and is
/// <c>AllowMultiple</c>; several of them on one action are separate filters that must ALL pass (AND). An action whose rule is
/// "key A OR key B" had to check the keys in its body, where startup auto-registration (A1, <see cref="HasPermissionReflector"/>)
/// and the endpoint guards cannot see them.
/// <para>The rule is not re-implemented: each key is evaluated by a real <see cref="HasPermissionAttribute"/> (anonymous → 401,
/// the platform_admin / partner_admin path, canonical + alias claim matching), in the declared order; the first key that passes
/// authorizes. An unauthenticated caller stops at the first answer (401). When no key passes, the last denial (403) stands.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class HasAnyPermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string[] _permissions;

    public HasAnyPermissionAttribute(params string[] permissions)
    {
        if (permissions is null || permissions.Length == 0 || permissions.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("At least one non-blank permission key is required.", nameof(permissions));
        }

        _permissions = permissions;
    }

    /// <summary>The accepted keys, in order. Exposed so startup auto-registration (A1) registers every one of them.</summary>
    public IReadOnlyList<string> Permissions => _permissions;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        IActionResult? denial = null;
        foreach (var permission in _permissions)
        {
            context.Result = null;
            await new HasPermissionAttribute(permission).OnAuthorizationAsync(context);
            if (context.Result is null)
            {
                return; // this key is enough
            }

            denial = context.Result;
            if (denial is UnauthorizedResult)
            {
                break; // not signed in: no other key changes that
            }
        }

        context.Result = denial;
    }
}
