using Microsoft.AspNetCore.Authorization;

namespace Diten.MdmService.Infrastructure.Authorization;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var actorType = context.User.FindFirst("actor_type")?.Value;
        if (string.Equals(actorType, "platform_admin", StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var hasPermission = context.User.Claims.Any(claim =>
            string.Equals(claim.Type, "permission", StringComparison.OrdinalIgnoreCase)
            && requirement.AnyOf.Contains(claim.Value, StringComparer.OrdinalIgnoreCase));

        if (hasPermission)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
