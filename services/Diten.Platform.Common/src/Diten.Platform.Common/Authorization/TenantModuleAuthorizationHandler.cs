using Microsoft.AspNetCore.Authorization;

namespace Diten.Platform.Common.Authorization;

public sealed class TenantModuleAuthorizationHandler : AuthorizationHandler<TenantModuleRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, TenantModuleRequirement requirement)
    {
        var allowedModules = context.User.FindAll("tenant_module")
            .Select(x => x.Value)
            .Concat((context.User.FindFirst("tenant_modules")?.Value ?? string.Empty)
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));

        if (allowedModules.Any(x => string.Equals(x, requirement.ModuleCode, StringComparison.OrdinalIgnoreCase)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
