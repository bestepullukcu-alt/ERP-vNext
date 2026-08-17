using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Common.Authorization;

public sealed class EntitlementAuthorizationPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider fallbackProvider;

    public EntitlementAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        fallbackProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
    {
        return fallbackProvider.GetDefaultPolicyAsync();
    }

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
    {
        return fallbackProvider.GetFallbackPolicyAsync();
    }

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (TryGetPolicyTarget(policyName, EntitlementAuthorizationPolicies.RequiresModulePrefix, out var moduleCode))
        {
            return Task.FromResult<AuthorizationPolicy?>(new AuthorizationPolicyBuilder()
                .AddRequirements(new TenantModuleRequirement(moduleCode))
                .Build());
        }

        if (TryGetPolicyTarget(policyName, EntitlementAuthorizationPolicies.RequiresFeaturePrefix, out var featureCode))
        {
            return Task.FromResult<AuthorizationPolicy?>(new AuthorizationPolicyBuilder()
                .AddRequirements(new TenantFeatureRequirement(featureCode))
                .Build());
        }

        return fallbackProvider.GetPolicyAsync(policyName);
    }

    private static bool TryGetPolicyTarget(string policyName, string prefix, out string targetCode)
    {
        targetCode = string.Empty;

        if (!policyName.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var candidate = policyName[prefix.Length..];
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        targetCode = candidate;
        return true;
    }
}
