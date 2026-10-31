using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
namespace Diten.SupplyChainService.Infrastructure.Features.SupplierPerformance;
[AttributeUsage(AttributeTargets.Method)]
public sealed class SupplierPerformancePermissionAttribute(string permission) : Attribute, IAuthorizationFilter
{
    public string Permission { get; } = permission;
    /// <summary>The non-disclosing 404 code of §0.5 stages 3a / 3b: the operation's declared 404 object, NOT_FOUND for lists.</summary>
    public string ScopeNotFoundCode { get; init; } = "NOT_FOUND";
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        // Middleware performs the ordered, contract-shaped authentication gate first.
        if (context.HttpContext.User.Identity?.IsAuthenticated != true || !context.HttpContext.User.HasClaim("permission", Permission))
            context.Result = new StatusCodeResult(403);
    }
}
