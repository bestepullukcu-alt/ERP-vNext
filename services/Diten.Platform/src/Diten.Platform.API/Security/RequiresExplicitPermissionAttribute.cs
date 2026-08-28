using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Diten.Platform.API.Security;

/// <summary>Used only by maker/checker surfaces whose SoD keys must not inherit the platform-actor bypass.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequiresExplicitPermissionAttribute : Attribute, IAuthorizationFilter
{
    private readonly string _permission;
    public RequiresExplicitPermissionAttribute(string permission) => _permission = permission;
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (!PermissionClaimEvaluator.Evaluate(context.HttpContext.User.Claims, _permission).IsSatisfied)
            context.Result = new ObjectResult(new { message = "Permission denied.", reason_code = "permission_denied" }) { StatusCode = 403 };
    }
}
