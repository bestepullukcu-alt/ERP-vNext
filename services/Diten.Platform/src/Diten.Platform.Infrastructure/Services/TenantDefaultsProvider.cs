using Diten.Platform.Application.Contracts;
using Diten.Platform.Infrastructure.Persistence.Settings;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Infrastructure.Services;

public sealed class TenantDefaultsProvider : ITenantDefaultsProvider
{
    private readonly TenantManagementOptions _options;

    public TenantDefaultsProvider(IOptions<TenantManagementOptions> options)
    {
        _options = options.Value;
    }

    public string DefaultRegion => _options.DefaultRegion;
    public string DefaultEnvironment => _options.DefaultEnvironment;
    public string DefaultTier => _options.DefaultTier;
    public string DefaultLanguage => _options.DefaultLanguage;
    public string DefaultTimezone => _options.DefaultTimezone;
    public string DefaultCurrency => _options.DefaultCurrency;
    public string AppUrlTemplate => _options.AppUrlTemplate;
}
