using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
namespace Diten.SupplyChainService.Infrastructure.Features.CapacityPlans;
[AttributeUsage(AttributeTargets.Method)]
public sealed class CapacityPermissionAttribute(string permission) : Attribute, IAuthorizationFilter
{
    public string Permission { get; } = permission;
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if(context.HttpContext.User.Identity?.IsAuthenticated!=true ||
           !context.HttpContext.User.HasClaim("permission",Permission))
            context.Result=new StatusCodeResult(403);
    }
}
