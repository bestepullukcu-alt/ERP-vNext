using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Diten.Platform.API.Security;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class HasPermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string _permission;

    public HasPermissionAttribute(string permission)
    {
        _permission = permission;
    }

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return Task.CompletedTask;
        }

        var actorType = user.FindFirst("actor_type")?.Value;
        var isPlatformActor = string.Equals(actorType, "platform_admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actorType, "partner_admin", StringComparison.OrdinalIgnoreCase);
        if (isPlatformActor || HasPermissionClaim(user.Claims))
        {
            return Task.CompletedTask;
        }

        context.Result = new ForbidResult();
        return Task.CompletedTask;
    }

    private bool HasPermissionClaim(IEnumerable<System.Security.Claims.Claim> claims) =>
        claims.Any(claim =>
            IsPermissionClaim(claim.Type)
            && claim.Value.Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(_permission, StringComparer.OrdinalIgnoreCase));

    private static bool IsPermissionClaim(string claimType) =>
        string.Equals(claimType, "permission", StringComparison.OrdinalIgnoreCase)
        || string.Equals(claimType, "permissions", StringComparison.OrdinalIgnoreCase)
        || claimType.EndsWith("/permission", StringComparison.OrdinalIgnoreCase)
        || claimType.EndsWith("/permissions", StringComparison.OrdinalIgnoreCase);
}
