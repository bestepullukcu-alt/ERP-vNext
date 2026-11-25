using Microsoft.AspNetCore.Authorization;

namespace Diten.ProcurementService.Infrastructure.Authorization;

/// <summary>
/// MOD-0140 DL-3 (pack §14, Revizyon 3 F-6a) — "ANY ONE of these keys is enough". Copied from Inventory
/// (<c>services/Diten.InventoryService/src/Diten.InventoryService.Infrastructure/Authorization/HasAnyPermissionAttribute.cs</c>):
/// the keys become ONE policy (<c>AnyPermission:a|b|…</c>) that <see cref="PermissionPolicyProvider"/> turns into an
/// <see cref="AnyPermissionRequirement"/>, granted by <see cref="AnyPermissionAuthorizationHandler"/> when the token carries at
/// least one of the exact <c>permission</c> claims — no role shortcut. <see cref="HasPermissionAttribute"/> takes one key and
/// several of them on one action must ALL pass (AND); this attribute is the OR. All three parts are needed: without the
/// handler every caller gets 403, without the provider branch the policy is not found.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class HasAnyPermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "AnyPermission:";

    public HasAnyPermissionAttribute(params string[] permissions)
    {
        if (permissions is null || permissions.Length == 0 || permissions.Any(p => string.IsNullOrWhiteSpace(p) || p.Contains('|')))
        {
            throw new ArgumentException("At least one non-blank permission key (without '|') is required.", nameof(permissions));
        }

        Permissions = permissions;
        Policy = PolicyPrefix + string.Join('|', permissions);
    }

    /// <summary>The accepted keys, in order — read by the manifest guard so every one of them is declared.</summary>
    public IReadOnlyList<string> Permissions { get; }
}

/// <summary>Any one of <see cref="Permissions"/> grants the action.</summary>
public sealed class AnyPermissionRequirement(IReadOnlyList<string> permissions) : IAuthorizationRequirement
{
    public IReadOnlyList<string> Permissions { get; } = permissions;
}

/// <summary>Grants an <see cref="AnyPermissionRequirement"/> when the token carries at least one of its exact <c>permission</c> claims.</summary>
public sealed class AnyPermissionAuthorizationHandler : AuthorizationHandler<AnyPermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AnyPermissionRequirement requirement)
    {
        var held = context.User.Claims.Where(c => c.Type == "permission").Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (requirement.Permissions.Any(held.Contains))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
