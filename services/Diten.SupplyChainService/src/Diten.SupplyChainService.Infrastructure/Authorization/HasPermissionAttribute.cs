using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Diten.SupplyChainService.Application.Common;
using Microsoft.AspNetCore.Mvc.Filters;
namespace Diten.SupplyChainService.Infrastructure.Authorization;
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class HasPermissionAttribute(params string[] permissions) : Attribute, IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var status = context.HttpContext.User.Identity?.IsAuthenticated != true ? 401 :
         !permissions.Any(p => context.HttpContext.User.HasClaim("permission", p)) ? 403 : 200;
        if (status != 200)
        {
            var request = context.HttpContext.RequestServices.GetRequiredService<RequestContext>();
            context.Result = new ObjectResult(new { error = new { code = "INVALID_REQUEST", message = status == 403 ? "Required shipment permission is missing." : "Authentication required.", correlationId = request.CorrelationId }, contractVersion = "v1" }) { StatusCode = status };
        }
        return Task.CompletedTask;
    }
}
