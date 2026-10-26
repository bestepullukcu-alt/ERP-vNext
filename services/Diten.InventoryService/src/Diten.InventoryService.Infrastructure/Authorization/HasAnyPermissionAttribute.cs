using Microsoft.AspNetCore.Authorization;

namespace Diten.InventoryService.Infrastructure.Authorization;

/// <summary>
/// WP-MVP1-FIX-LIVE3 (AGENTS.md core rule 3, "every action carries [HasPermission]") — "ANY ONE of these keys is enough", the Inventory
/// counterpart of Platform's <c>HasAnyPermissionAttribute</c>. <see cref="HasPermissionAttribute"/> takes one key; several of them on one
/// action must ALL pass (AND). An action whose rule is "key A OR key B" checked its keys in the body, where the manifest / permission-catalogue
/// guards could not see them. Here the rule is a policy (<c>AnyPermission:a|b|…</c>) of the SAME pipeline: the same exact-claim match as
/// <see cref="PermissionAuthorizationHandler"/> (no role shortcut), the same contract 401 / 403 (<see cref="ContractAuthorizationResultHandler"/>).
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

    /// <summary>The accepted keys, in order — read by the manifest / catalogue guards so every one of them is declared.</summary>
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
