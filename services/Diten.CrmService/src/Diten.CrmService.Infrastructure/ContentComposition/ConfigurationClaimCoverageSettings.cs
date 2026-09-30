using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Microsoft.Extensions.Configuration;

namespace Diten.CrmService.Infrastructure.ContentComposition;

/// <summary>
/// WP-CL-BE-1 (claims v2) — reads <c>Crm:Claims:ExpiringWindowDays</c>. A missing or nonsensical value falls back to
/// the documented default (<see cref="ClaimCoverageDefaults.ExpiringWindowDays"/>) instead of failing startup.
/// </summary>
public sealed class ConfigurationClaimCoverageSettings : IClaimCoverageSettings
{
    public ConfigurationClaimCoverageSettings(IConfiguration configuration)
    {
        var days = configuration.GetValue<int?>("Crm:Claims:ExpiringWindowDays");
        ExpiringWindowDays = days is >= 0 and <= 3650 ? days.Value : ClaimCoverageDefaults.ExpiringWindowDays;
    }

    public int ExpiringWindowDays { get; }
}
