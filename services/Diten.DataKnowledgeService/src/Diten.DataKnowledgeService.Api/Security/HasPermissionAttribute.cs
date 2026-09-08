using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Diten.DataKnowledgeService.Api.Security;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class HasPermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string _permission;

    public HasPermissionAttribute(string permission) => _permission = permission;

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return Task.CompletedTask;
        }

        var hasPermission = user.Claims.Any(claim =>
            IsPermissionClaim(claim.Type)
            && SplitPermissionClaim(claim.Value).Any(permission =>
                string.Equals(permission, _permission, StringComparison.OrdinalIgnoreCase)));

        if (!hasPermission)
        {
            context.Result = new ForbidResult();
        }

        return Task.CompletedTask;
    }

    private static bool IsPermissionClaim(string type) =>
        string.Equals(type, "permission", StringComparison.OrdinalIgnoreCase)
        || string.Equals(type, "permissions", StringComparison.OrdinalIgnoreCase)
        || string.Equals(type, "scope", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> SplitPermissionClaim(string value) =>
        value.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
