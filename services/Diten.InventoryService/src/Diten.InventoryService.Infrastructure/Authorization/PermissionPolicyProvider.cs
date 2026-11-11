using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Diten.InventoryService.Infrastructure.Authorization;

public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallbackProvider;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallbackProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith("Permission:", StringComparison.OrdinalIgnoreCase))
        {
            var permission = policyName["Permission:".Length..];
            var policy = new AuthorizationPolicyBuilder()
                .AddRequirements(new PermissionRequirement(permission))
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        // WP-MVP1-FIX-LIVE3: [HasAnyPermission(a, b, …)] — any one key grants (AnyPermissionAuthorizationHandler)
        if (policyName.StartsWith(HasAnyPermissionAttribute.PolicyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var permissions = policyName[HasAnyPermissionAttribute.PolicyPrefix.Length..].Split('|', StringSplitOptions.RemoveEmptyEntries);
            return Task.FromResult<AuthorizationPolicy?>(new AuthorizationPolicyBuilder().AddRequirements(new AnyPermissionRequirement(permissions)).Build());
        }

        return _fallbackProvider.GetPolicyAsync(policyName);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() =>
        _fallbackProvider.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() =>
        _fallbackProvider.GetFallbackPolicyAsync();
}
