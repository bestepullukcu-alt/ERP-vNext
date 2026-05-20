using Microsoft.AspNetCore.Authorization;

namespace Diten.Platform.Common.Authorization;

public sealed class TenantFeatureRequirement : IAuthorizationRequirement
{
    public TenantFeatureRequirement(string featureCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(featureCode);

        FeatureCode = featureCode;
    }

    public string FeatureCode { get; }
}
