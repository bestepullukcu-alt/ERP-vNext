using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Diten.HcmService.Infrastructure.Authorization;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (HasPermission(context.User, requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static bool HasPermission(ClaimsPrincipal user, string permission)
    {
        return user.Claims.Any(claim =>
            IsPermissionClaim(claim.Type)
            && string.Equals(claim.Value, permission, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPermissionClaim(string claimType)
    {
        return string.Equals(claimType, "permission", StringComparison.OrdinalIgnoreCase)
            || string.Equals(claimType, "permissions", StringComparison.OrdinalIgnoreCase);
    }
}
