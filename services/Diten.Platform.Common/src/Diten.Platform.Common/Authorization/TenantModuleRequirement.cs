using Microsoft.AspNetCore.Authorization;

namespace Diten.Platform.Common.Authorization;

public sealed class TenantModuleRequirement : IAuthorizationRequirement
{
    public TenantModuleRequirement(string moduleCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleCode);

        ModuleCode = moduleCode;
    }

    public string ModuleCode { get; }
}
