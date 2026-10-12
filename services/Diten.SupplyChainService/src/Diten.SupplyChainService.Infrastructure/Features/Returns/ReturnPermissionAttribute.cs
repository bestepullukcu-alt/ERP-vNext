using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
namespace Diten.SupplyChainService.Infrastructure.Features.Returns;
[AttributeUsage(AttributeTargets.Method)]
public sealed class ReturnPermissionAttribute(string permission) : Attribute, IAuthorizationFilter
{
    public string Permission { get; } = permission;
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        // Ordered middleware supplies the published envelope before MVC executes.
        if (context.HttpContext.User.Identity?.IsAuthenticated != true || !context.HttpContext.User.HasClaim("permission", Permission))
            context.Result = new StatusCodeResult(403);
    }
}
