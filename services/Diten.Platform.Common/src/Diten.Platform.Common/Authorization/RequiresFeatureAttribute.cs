using Microsoft.AspNetCore.Authorization;

namespace Diten.Platform.Common.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequiresFeatureAttribute : AuthorizeAttribute
{
    public RequiresFeatureAttribute(string featureCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(featureCode);

        FeatureCode = featureCode;
        Policy = $"{EntitlementAuthorizationPolicies.RequiresFeaturePrefix}{featureCode}";
    }

    public string FeatureCode { get; }
}
