using Microsoft.AspNetCore.Authorization;

namespace Diten.HcmService.Infrastructure.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission)
    {
        Permission = permission;
        Policy = PermissionPolicyProvider.PolicyPrefix + permission;
    }

    public string Permission { get; }
}
