using Microsoft.AspNetCore.Authorization;

namespace Diten.Platform.Common.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequiresModuleAttribute : AuthorizeAttribute
{
    public RequiresModuleAttribute(string moduleCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleCode);

        ModuleCode = moduleCode;
        Policy = $"{EntitlementAuthorizationPolicies.RequiresModulePrefix}{moduleCode}";
    }

    public string ModuleCode { get; }
}
