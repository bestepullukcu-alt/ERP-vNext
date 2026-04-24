using Microsoft.AspNetCore.Authorization;

namespace Diten.DevEnablementService.Infrastructure.Authorization;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission)
        => Policy = $"Permission:{permission}";
}
