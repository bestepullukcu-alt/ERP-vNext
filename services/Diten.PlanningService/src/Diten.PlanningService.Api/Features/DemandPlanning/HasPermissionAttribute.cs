using Microsoft.AspNetCore.Authorization;

namespace Diten.PlanningService.Api.Features.DemandPlanning;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission)
    {
        Policy = permission;
    }
}