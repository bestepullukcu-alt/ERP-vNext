using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Diten.Web.Filters;

public sealed class ShellAccessFilter : IAuthorizationFilter
{
    private static readonly string[] PlatformActors = { "platform_admin", "partner_admin" };

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var endpoint = context.HttpContext.GetEndpoint();
        if (endpoint?.Metadata?.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() != null)
        {
            return;
        }

        var request = context.HttpContext.Request;
        var path = request.Path;

        if (path.StartsWithSegments("/account", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var actorType = context.HttpContext.User.FindFirst("actor_type")?.Value?.Trim().ToLowerInvariant();
        var isPlatformPath = path.StartsWithSegments("/platform", StringComparison.OrdinalIgnoreCase) ||
                             request.Host.Host.StartsWith("admin.", StringComparison.OrdinalIgnoreCase);

        if (isPlatformPath)
        {
            if (string.IsNullOrWhiteSpace(actorType))
            {
                context.Result = BuildLoginRedirect("/platform/login", request);
                return;
            }

            if (Array.IndexOf(PlatformActors, actorType) < 0)
            {
                context.Result = new ForbidResult();
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(actorType))
        {
            context.Result = BuildLoginRedirect("/account/login", request);
            return;
        }

        if (!string.Equals(actorType, "tenant_user", StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new ForbidResult();
        }
    }

    private static RedirectResult BuildLoginRedirect(string loginPath, Microsoft.AspNetCore.Http.HttpRequest request)
    {
        var returnUrl = request.Path + request.QueryString;
        return new RedirectResult($"{loginPath}?returnUrl={Uri.EscapeDataString(returnUrl)}");
    }
}
