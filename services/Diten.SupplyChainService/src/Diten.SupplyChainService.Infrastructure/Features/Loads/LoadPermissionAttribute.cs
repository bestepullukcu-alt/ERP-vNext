using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
namespace Diten.SupplyChainService.Infrastructure.Features.Loads;
[AttributeUsage(AttributeTargets.Method)]
public sealed class LoadPermissionAttribute(string permission) : Attribute, IAuthorizationFilter
{
    public string Permission { get; } = permission;
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        // Middleware performs the ordered, contract-shaped authentication gate first.
        if (context.HttpContext.User.Identity?.IsAuthenticated != true || !context.HttpContext.User.HasClaim("permission", Permission))
            context.Result = new StatusCodeResult(403);
    }
}
